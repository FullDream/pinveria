# maps Specification

## Purpose

Manage independent game-owned coordinate spaces with map-owned content, optional aligned styles, durable originals, recoverable tile generation, and per-map progress.

## Requirements

### Requirement: Refuse unplanned legacy map data loss

The schema-only MapStyle upgrade SHALL refuse a database containing legacy maps, image versions, admission owners, or cleanup intents before dropping tables. Existing data and files SHALL remain unchanged on refusal. Transitioning a nonempty legacy store SHALL require a separately agreed data-and-file migration or explicit authorization to remove test maps. The already applied Maps migration SHALL NOT be rewritten or automatically applied at startup.

#### Scenario: Preserve an existing legacy upload

- **WHEN** the schema-only upgrade encounters a previously uploaded map
- **THEN** it fails before schema changes and keeps the map, image metadata, original files, and parent resources intact
- **AND** operators cannot silently upgrade by discarding image history or resetting processing state

#### Scenario: Initialize a fresh store

- **WHEN** an operator explicitly applies migrations to a fresh database or an agreed prepared empty legacy map store
- **THEN** the schema creates MapStyles with one original and processing state per style
- **AND** no version tables are used by the current application model

### Requirement: Scope multiple maps and their styles to a game

A game SHALL own zero or more independent maps. Map operations SHALL use /games/{gameId}/maps and /games/{gameId}/maps/{mapId}, with authentication for draft or published games. Styles SHALL belong to exactly one map. Valid requests for missing parents or resources outside the route game/map SHALL return HTTP 404 without changes. The former singular map routes SHALL NOT remain active aliases.

#### Scenario: Independent maps in one game

- **WHEN** a game already has a map and a valid request creates another with a different normalized name
- **THEN** both maps exist with independent identities, dimensions, styles, and content

#### Scenario: Reject foreign resources even within one game

- **WHEN** a route identifies a map of another game or a style of another map, including a sibling map of the same game
- **THEN** HTTP 404 preserves all resources and files

### Requirement: Create a named coordinate space without an image

POST /games/{gameId}/maps SHALL accept application/json with required name and slug, optional nullable description (defaulting to null when omitted or empty), and optional status (`draft` or `published`, defaulting to `draft` when omitted) and return 200 with a persisted map, UUID v7, UTC timestamps, string status, nullable description, styles=[] and defaultStyleId=null. Every map SHALL immediately use the fixed centered `[-100, 100]` logical coordinate space without storing map-level dimensions or waiting for a first style. No file or processing work SHALL be created.

#### Scenario: Create a blank map

- **WHEN** valid JSON contains name=World and slug=world and omits description and status
- **THEN** the map is persisted and returned with description=null, status `draft`, styles=[], and defaultStyleId=null without map-level dimension or geometry fields
- **AND** it survives restart with no styles, files, or processing work

#### Scenario: Create a map with explicit status

- **WHEN** valid JSON contains name=World, slug=world, and status=`published` (or `draft`)
- **THEN** the map is persisted and returned with the supplied status and description=null

#### Scenario: Create a map with optional description

- **WHEN** valid JSON contains name=World, slug=world, and description="Main overworld"
- **THEN** the map is persisted and returned with the supplied description

#### Scenario: Reject invalid creation

- **WHEN** name or slug is missing/null, blank or overlength, slug has invalid syntax, or status is null or an undefined enum value
- **THEN** 422 returns field errors; malformed JSON, incompatible shapes, unknown enum strings and supplied width/height return 400; unsupported media returns 415, without changes

#### Scenario: Create content before the first style

- **WHEN** groups, categories or finite-coordinate markers are added before any style is attached
- **THEN** content persists directly in the fixed `[-100, 100]` logical coordinate space and later style attachment preserves every marker coordinate

### Requirement: Publish a map and all owned draft content atomically

Authenticated POST /games/{gameId}/maps/{mapId}/publish SHALL atomically transition the scoped map and all of its owned draft category groups, categories, and markers to `published`, and return HTTP 200 with the complete map representation. Only records whose status changes from `draft` to `published` SHALL update `updatedAt`. Missing or foreign game/map routes SHALL return HTTP 404 without changes.

#### Scenario: Publish a draft map with mixed draft and published content

- **WHEN** POST /games/{gameId}/maps/{mapId}/publish is called for a draft map owning draft and published groups, categories, and markers
- **THEN** the map and every owned group, category, and marker persist with status `published`
- **AND** `updatedAt` advances only on the map and owned entities that were previously `draft`, while already-published entities retain their prior `updatedAt`

#### Scenario: Publish changes on an already published map

- **WHEN** POST /games/{gameId}/maps/{mapId}/publish is called for a published map that contains at least one draft group, category, or marker
- **THEN** the draft child entities transition to `published` with a new `updatedAt` while the map itself retains its existing `updatedAt`

#### Scenario: Idempotent publish when all content is already published

- **WHEN** POST /games/{gameId}/maps/{mapId}/publish is called for a published map whose groups, categories, and markers are all already `published`
- **THEN** HTTP 200 returns the complete map and no `updatedAt` timestamp changes on the map or any child entity

#### Scenario: Missing or foreign map on publish

- **WHEN** POST /games/{gameId}/maps/{mapId}/publish targets a nonexistent game, nonexistent map, or a map belonging to another game
- **THEN** HTTP 404 ProblemDetails is returned without modifying any map or content

### Requirement: Backfill existing map and content publication statuses

An explicit migration SHALL add non-null string `Status` columns to `Maps`, `CategoryGroups`, `Categories`, and `Markers` and initialize all preexisting rows to `Published` (`published` in API responses) without changing their identities, ownership, coordinates, order, or timestamps. Applied migrations SHALL NOT be rewritten or run automatically on startup.

#### Scenario: Upgrade a populated store

- **WHEN** the publication status migration is applied to a database containing maps, category groups, categories, and markers
- **THEN** every preexisting row receives status `published` and retains its ID, relationships, and timestamps

### Requirement: Attach one library image as a map style

POST /games/{gameId}/maps/{mapId}/styles SHALL accept application/json with required name, backgroundColor, and sourceAssetId. It SHALL atomically attach one compatible committed image, enqueue one style, and return 200 with the complete map. Missing sources SHALL return 404; invalid names/colors or incompatible images 422; name conflicts 409; multipart or unsupported request media 415. No file upload SHALL occur here.

#### Scenario: Append one style independently of wire order

- **WHEN** valid JSON identifies an existing image asset with name and backgroundColor in any property order
- **THEN** exactly one queued style and its asset relationship commit before 200
- **AND** source bytes are not copied or moved and existing styles remain unchanged

#### Scenario: Reject a conflicting name

- **WHEN** the supplied style name conflicts with an existing style in that map
- **THEN** 409 rejects attachment without changing the source asset, files, existing styles, order, or defaults

#### Scenario: Reject batch or ambiguous input

- **WHEN** style creation receives multipart/file input or an array instead of the JSON object contract
- **THEN** unsupported media returns 415 and incompatible JSON shapes return 400
- **AND** field validation uses name, backgroundColor, and sourceAssetId rather than file or multipart metadata

### Requirement: Enforce configurable single-file upload limits

Original admission SHALL remain in the media library under its configurable default 314572800-byte limit and raster bounds 1..16384. Style attachment SHALL introduce neither another upload path nor a byte limit. Differences from logical dimensions or aspect ratio SHALL NOT reject eligible images.

#### Scenario: Accept configured boundaries

- **WHEN** an eligible source within library bounds differs from map dimensions
- **THEN** attachment succeeds without modifying or copying original bytes

#### Scenario: Reject oversize and dimension violations

- **WHEN** a file exceeds library byte or dimension limits
- **THEN** the library rejects admission under its existing contract before style attachment

#### Scenario: Apply changed configuration

- **WHEN** an operator changes the shared admission limit and restarts
- **THEN** later uploads use that limit without rewriting assets or logical geometry

### Requirement: Accept supported image originals without modifying them

Style sources SHALL reference verified static JPEG, PNG, WebP, or AVIF library images. SVG, videos, folders, and unsupported images SHALL return 422. Byte preservation and content identification SHALL be owned by the library; map attachment SHALL check current compatibility. Decode-only failures SHALL produce safe failed style state. Supporting a format SHALL require a working decoder on the deployment runtime.

#### Scenario: Preserve an accepted original

- **WHEN** a supported library image is attached to one or several styles
- **THEN** every style reads the same unchanged committed bytes through its asset reference
- **AND** neither filename nor MIME input selects a filesystem path

#### Scenario: Reject unsupported images

- **WHEN** a sourceAssetId identifies SVG, a video, or an incompatible image
- **THEN** 422 rejects the entire style creation without visible map changes

#### Scenario: Verify expanded raster formats

- **WHEN** matching static WebP or AVIF assets are attached
- **THEN** they generate the same geometry and transparent lossless WebP tile contract as JPEG/PNG
- **AND** animation or an unavailable decoder cannot be silently treated as a supported static original

### Requirement: Align all styles in the fixed centered coordinate space

Maps SHALL NOT store or initialize per-map logical dimensions from styles. Every committed style attachment SHALL validate its raster source within library bounds (`1..16384`) and produce its own native tile pyramid (`tileGeometry`). In the fixed `[-100, 100]` logical space, each style SHALL center at `(0, 0)` with uniform scale `200 / max(sourceWidth, sourceHeight)`, preserving native aspect ratio. Style mutations—including deleting all styles—SHALL preserve original bytes and marker coordinates. No resizing endpoint SHALL exist.

#### Scenario: Attach styles at different resolutions and aspect ratios

- **WHEN** `8000x8000`, `4000x2000`, and `8000x4000` styles are attached to the same map in any order
- **THEN** all attachments succeed without mutating map-level dimensions or moving markers
- **AND** `8000x8000` maps to `[-100, -100, 100, 100]`, while both `4000x2000` and `8000x4000` map to `[-100, -50, 100, 50]` with zero aspect distortion

#### Scenario: Remove every style and attach a new one

- **WHEN** all styles are deleted from a map and a new eligible style of any aspect ratio is later attached
- **THEN** no stale dimensions from deleted styles affect the new style, map content remains intact, and source assets remain in the library

#### Scenario: Preserve alignment when a style fails processing

- **WHEN** an attached style fails tile generation
- **THEN** the map's fixed coordinate space and existing markers remain unaffected and retrying or attaching another style requires no geometry reset

### Requirement: Validate unique user-defined style names

Styles SHALL support user-defined names rather than a fixed enum of styles. Names SHALL be trimmed, preserve display spelling, contain 1 through 200 characters, and be unique within their map after invariant case normalization. Invalid names SHALL return HTTP 422; conflicting names SHALL return HTTP 409, including recognized save-time uniqueness conflicts.

#### Scenario: Normalize and distinguish names

- **WHEN** a style is named ' Atlas '
- **THEN** it is returned as Atlas and an attempted ' atlas ' in the same map returns HTTP 409
- **AND** the same names are allowed on another game's map

#### Scenario: Validate uploaded and renamed names

- **WHEN** a new or patched name is null, blank, or longer than 200 trimmed characters
- **THEN** the operation returns HTTP 422 without partial state changes
- **AND** an added style conflicting with an existing normalized name returns HTTP 409

### Requirement: Keep background color as viewer metadata

Each style SHALL require backgroundColor in #RGB or #RRGGBB notation with ASCII hexadecimal digits. Short notation SHALL expand and responses SHALL use uppercase #RRGGBB. Invalid or null values SHALL return HTTP 422. The color SHALL apply only to the viewer background; changing it SHALL NOT recolor originals, tiles, or transparency or enqueue image processing.

#### Scenario: Normalize the viewer background

- **WHEN** a style supplies #abc or #aabbcc
- **THEN** its response contains #AABBCC

#### Scenario: Update color without regenerating tiles

- **WHEN** a valid PATCH changes only backgroundColor
- **THEN** the new metadata is returned and the style's image, tile URLs, processing state, and original bytes remain unchanged

### Requirement: Return complete map metadata

GET /games/{gameId}/maps/{mapId} SHALL include id, gameId, required string name, required string slug, nullable string description, string status (`draft` or `published`), nullable defaultStyleId, styles in persisted display order, createdAt, and updatedAt. Map representations SHALL NOT expose map-level width, height, extent, tileOrigin, tileSize, minZoom, maxZoom, or resolutions fields. Style representations SHALL expose their own source-based `tileGeometry`. defaultStyleId SHALL identify the effective ready default or be null. IDs SHALL use UUID v7; internal paths SHALL remain private.

#### Scenario: Read a blank or styled map

- **WHEN** a client reads a map before attaching styles, with active styles, or after deleting all styles
- **THEN** the representation includes its metadata, nullable description, status, defaultStyleId, and ordered styles without map-level dimension or geometry fields

#### Scenario: Read ordered styles

- **WHEN** styles have been reordered
- **THEN** GET and mutation map responses return the same persisted order and each style's source-based `tileGeometry`

### Requirement: Expose processing state directly on each style

Style representations SHALL retain id, name, backgroundColor, source, status, progress, error, tilesUrlTemplate, createdAt, and updatedAt. Status and safe error information SHALL be durable; intermediate progress SHALL be transient. source SHALL resolve the referenced shared image asset. Tile templates SHALL remain null until successful publication. Percentage changes alone SHALL NOT change persisted timestamps.

#### Scenario: Report queued and ready styles

- **WHEN** an accepted style has not completed processing
- **THEN** its status and transient progress describe its own processing attempt and tilesUrlTemplate is null
- **AND** after publication it becomes ready with progress 100 and a style-addressed tile template

#### Scenario: Report processing failure safely

- **WHEN** a style's processing fails
- **THEN** tilesUrlTemplate remains null and status is failed
- **AND** error exposes only a safe code and message, never paths, stack traces, or library diagnostics
- **AND** the last percentage is available only while its transient state is retained; absent transient progress is represented as 0

#### Scenario: Resolve source metadata without retiling

- **WHEN** a source asset is renamed, moved, or given new alt text
- **THEN** subsequent HTTP map/style representations resolve that metadata without changing style timestamps, attempts, pixels, or tile URLs
- **AND** processing notifications do not synchronize source metadata

### Requirement: Use centered normalized logical coordinates

Logical coordinates SHALL use a universal fixed Cartesian coordinate space centered at `(0, 0)`, with X increasing right, Y increasing up, and reference span `200` (`[-100, -100, 100, 100]`). Each style SHALL map its source center `(0, 0)` to logical `(0, 0)` with uniform scale `200 / max(sourceWidth, sourceHeight)` so its logical footprint is `[-sourceWidth * scale / 2, -sourceHeight * scale / 2, sourceWidth * scale / 2, sourceHeight * scale / 2]`. Existing finite marker coordinates, including negative, fractional, or out-of-extent positions, SHALL remain valid without rounding or bounds rejection.

#### Scenario: Place the same marker on all styles

- **WHEN** a map contains a marker at `(0, 0)` and a marker at `(50, 25)` across `4096x4096` and `4096x2048` styles (or styles at different resolutions of the same aspect ratio)
- **THEN** `(0, 0)` remains at every style's center and `(50, 25)` remains at the same world position without distorting either image

#### Scenario: Preserve fractional geometry and existing markers

- **WHEN** style dimensions are odd or markers have negative, fractional, or beyond-`[-100, 100]` finite positions
- **THEN** half-unit style tile boundaries and marker coordinates are preserved without rounding or bounds validation

### Requirement: Generate a transparent power-of-two tile pyramid

Each style SHALL provide native-source-resolution lossless WebP tiles of 512x512 without overlap. Its zoom 0 SHALL fit the source within one tile; its maxZoom SHALL represent original-pixel resolution. Adjacent source resolutions SHALL differ by two. Edge padding SHALL be transparent and backgroundColor SHALL NOT be baked in. Tile geometry SHALL be independent of logical map dimensions.

#### Scenario: Render original and reduced levels

- **WHEN** a source exceeds one tile
- **THEN** its pyramid covers 0..maxZoom with the last source resolution equal to one source pixel, including sources larger than the first style

#### Scenario: Keep edge and source transparency

- **WHEN** an odd-sized or transparent image is tiled
- **THEN** source alpha and transparent edge padding remain, with lossless tile encoding

#### Scenario: Handle a small map

- **WHEN** both source dimensions are at most 512
- **THEN** its sole level is zoom 0 at source resolution 1 without enlarging the original

### Requirement: Deliver immutable style-addressed tiles

The system SHALL serve complete WebP tiles statically at /map-tiles/{mapId}/{styleId}/{z}/{x}/{y}.webp without Mediator or database access. IDs SHALL use UUID N format. GET/HEAD and cache revalidation SHALL be supported. Missing files or malformed paths SHALL return HTTP 404. Retained files MAY remain available after metadata changes or deletion until cleanup; caches MAY retain them longer.

#### Scenario: Locate and cache a published tile

- **WHEN** a valid tile URL is requested for a complete retained style
- **THEN** the correct WebP bytes and a cache validator are returned
- **AND** immutable style URLs address columns increasing right and rows down from tileOrigin
- **AND** revalidating unchanged content can return HTTP 304 without changing processing state or querying the database
- **AND** HEAD returns the same headers without a response body

#### Scenario: Keep original and incomplete files private

- **WHEN** a client requests a legacy original path, staging file, work tile, manifest, directory, or nonexistent tile coordinate
- **THEN** 404 preserves private working paths and only complete pyramids exist under the public tile root
- **AND** committed originals are intentionally available through their separate shared /media/files/{id} endpoint

#### Scenario: Serve complete files independently of metadata

- **WHEN** a validated pyramid has moved into the public tile root but its ready commit has not completed, or its metadata was deleted before cleanup
- **THEN** its existing WebP files may be served without checking database state
- **AND** static delivery does not recheck current ownership, processing state, or database existence
- **AND** metadata advertises its tile template and notifications advertise ready only after a successful ready commit
- **AND** startup recovery or cleanup eventually removes failed or unreferenced derived output without deleting ready output

### Requirement: Process admitted styles independently and sequentially

At most one style SHALL be tiled at a time in the initial deployment. Admitted work SHALL transition from queued to processing and then ready or failed. Committed admission and manual retry SHALL trigger processing without periodic database discovery or a connected viewer. Failure of one style SHALL NOT roll back admitted siblings or prevent their processing. Duplicate dispatch SHALL NOT duplicate a processing attempt.

#### Scenario: Continue after one style fails

- **WHEN** Atlas and Roads are admitted independently to a map and Atlas fails during processing
- **THEN** Atlas becomes failed and Roads can still become ready
- **AND** the original successful upload response and saved originals remain valid

#### Scenario: Avoid concurrent image processing

- **WHEN** several styles are queued for one or more maps and games
- **THEN** only one tiling operation runs at once while other work remains queued

#### Scenario: Dispatch after a successful commit

- **WHEN** style admission or retry commits successfully
- **THEN** its current attempt becomes eligible for processing without a periodic database scan
- **AND** a rolled-back transaction produces no runnable work
- **AND** request cancellation after commit does not discard committed work

### Requirement: Keep exactly one original per style

Each style SHALL reference exactly one immutable library image for its lifetime and own its derived tile pyramid. Several styles MAY reference the same asset without byte duplication. Image replacement/version history SHALL NOT be exposed. A different source SHALL require a new style. Metadata PATCH SHALL NOT change sourceAssetId or image bytes. Removing a style SHALL remove only its relationship and derived output.

#### Scenario: Refuse image replacement

- **WHEN** a client attempts an image replacement path or patches sourceAssetId on an existing style
- **THEN** no replacement occurs and the original reference remains unchanged

#### Scenario: Change visual styles without changing map content

- **WHEN** a style is added or removed
- **THEN** surviving styles retain their own references/pyramids, markers remain unchanged, and every source asset remains in the library

### Requirement: Retry failed processing from the retained original

POST /games/{gameId}/maps/{mapId}/styles/{styleId}/retry SHALL requeue only a failed style and return HTTP 200 with the complete style. It SHALL reuse retained original bytes without a new upload, clear the previous error, and reset progress to 0. Retrying queued, processing, or ready work SHALL return HTTP 409 without duplication.

#### Scenario: Retry failed style processing

- **WHEN** a client retries a failed style
- **THEN** the same style ID is queued again and its saved original is reused
- **AND** a fresh internal attempt token fences callbacks from the previous attempt

#### Scenario: Reject redundant retry

- **WHEN** a client retries work not in failed state
- **THEN** HTTP 409 is returned without creating another processing attempt

#### Scenario: Handle an out-of-band missing source

- **WHEN** retry encounters a library source whose physical file was removed manually
- **THEN** it fails safely under the existing processing-state contract without removing the asset or source relationship
- **AND** the failure does not block processing of unrelated styles

### Requirement: Recover interrupted processing after restart

Accepted queued work SHALL survive application restart and be restored once at startup. Styles left processing SHALL become failed with a safe interruption error and durable derived-output cleanup, without automatic retry. Ready styles and shared originals SHALL be preserved. Recovery SHALL finish before normal processing and subscriptions start and SHALL NOT require a viewer or recurring database scans.

#### Scenario: Restart with queued and interrupted work

- **WHEN** the application stops with queued and processing styles and later starts again
- **THEN** queued styles become eligible for sequential processing
- **AND** interrupted styles become failed with progress 0 and require explicit manual retry
- **AND** their work and tile files are removed, with durable retry if filesystem cleanup fails
- **AND** other ready styles and all shared originals are preserved

#### Scenario: Crash after moving a complete pyramid

- **WHEN** the server stops after moving output into public storage but before committing ready
- **THEN** startup treats the persisted processing style as interrupted and removes that output instead of completing or requeuing it

#### Scenario: Crash between admission and dispatch

- **WHEN** a queued style commits but the process stops before dispatching it in memory
- **THEN** startup discovers and dispatches that queued attempt once without requiring a client retry

### Requirement: Stream current per-style progress through one map subscription

The system SHALL replace the map SSE route with a typed MapHub using JoinMap and LeaveMap to manage SignalR group membership by mapId. Handlers and the worker SHALL explicitly publish StyleUpdated with full committed styles and ProcessingUpdated with percentage/deletion deltas containing mapId, styleId, attemptId, status, progress, error, and deleted. Queue admission, database transaction coordination, and file cleanup SHALL NOT implicitly publish resource notifications. Lifecycle transitions SHALL be sent through StyleUpdated without a duplicate ProcessingUpdated event. The Hub SHALL NOT retain application-level subscriptions, snapshots or connection registries. Joining SHALL NOT read the database, validate resource existence, or send initial state. No streaming-query client is required.

#### Scenario: Connect during or after processing

- **WHEN** a client joins a map group
- **THEN** it obtains the current map and styles through the existing HTTP read after joining
- **AND** subsequent committed style changes and transient progress are sent directly to the group without database polling
- **AND** HTTP state and buffered events require client reconciliation rather than an atomic Hub snapshot guarantee

#### Scenario: Reconnect without interrupting work

- **WHEN** a viewer disconnects and reconnects while processing continues
- **THEN** it explicitly rejoins and refetches current HTTP state rather than replaying historical events
- **AND** disconnecting or never connecting does not cancel processing
- **AND** stale attempts, deleted resources, and delayed metadata require reconciliation without regressing cached state

#### Scenario: Observe independently

- **WHEN** two clients join the same map
- **THEN** SignalR sends the same change to both group members
- **AND** leaving one map preserves a connection's other memberships
- **AND** notification failures are bounded and cannot turn committed work into a failure

### Requirement: Report progress without promising unavailable exact work counts

Progress SHALL be transient, approximate, and nondecreasing within an attempt, reset to 0 on manual retry, and remain below 100 until ready commits. Intermediate changes SHALL NOT trigger database writes. Changed percentages SHALL be notified at most about once per second per map; committed lifecycle transitions SHALL bypass percentage throttling. Ready SHALL report 100; other states without retained transient progress SHALL report 0.

#### Scenario: Finish and restart progress accurately

- **WHEN** processing approaches completion but publication is not committed
- **THEN** reported progress remains below 100 until publication succeeds
- **AND** manual retry starts a new attempt at 0 and stale callbacks cannot update it
- **AND** failed progress is not promised to survive process restart

#### Scenario: Keep an idle subscription valid

- **WHEN** a subscribed map has no queued or processing styles
- **THEN** the client derives completion from style statuses while membership remains active until LeaveMap or disconnect
- **AND** no application-level idle heartbeat or recurring database read is retained, and later style changes are delivered to the same group

### Requirement: Patch style metadata with shared merge-patch semantics

PATCH /games/{gameId}/maps/{mapId}/styles/{styleId} SHALL accept optional name and backgroundColor and return HTTP 200 with the complete style. Both supported JSON Merge Patch media types and charset parameters SHALL work. Empty objects and null required values SHALL return HTTP 422; unknown/read-only fields or invalid JSON shapes HTTP 400; unsupported media types HTTP 415. No PUT route SHALL exist.

#### Scenario: Update metadata atomically without retiling

- **WHEN** a patch supplies a valid name and backgroundColor
- **THEN** both changes commit together without changing image source or processing status
- **AND** an invalid or conflicting supplied value rejects the complete patch

#### Scenario: Preserve omission and metadata no-ops

- **WHEN** a nonempty patch omits one field or supplies only current normalized values
- **THEN** omitted fields remain unchanged and a metadata no-op preserves updatedAt without a write
- **AND** metadata may be edited while image processing runs

### Requirement: Select the earliest displayable style by creation order

The system SHALL persist the default independently of display order. An explicit selection SHALL require a ready style belonging to the map. Without a surviving ready selection, the first ready style in display order SHALL be chosen and retained as default; no ready styles SHALL produce null. Reordering or another style becoming ready SHALL NOT replace a surviving ready default.

#### Scenario: Skip an unfinished first style

- **WHEN** a later style becomes ready before the first
- **THEN** the later one becomes the default and stays default when the first becomes ready

#### Scenario: Choose a new default after deletion

- **WHEN** the selected default is deleted
- **THEN** the first surviving ready style in display order becomes default, or null if none exists

#### Scenario: Select a ready default

- **WHEN** an administrator selects a ready style
- **THEN** the selection persists across reload and changes default-derived previews without changing order or geometry

### Requirement: Allow deletion of every style independently of the map

Style DELETE SHALL remove the scoped style/source relationship and durably schedule only its derived tile/work cleanup before returning empty 204. The last style SHALL remain removable in any processing state. Source assets SHALL remain in the library even without other uses. Missing/repeated deletion SHALL return 404. Existing map geometry/content/default rules SHALL remain unchanged.

#### Scenario: Remove the final queued ready or failed style

- **WHEN** the only style of a map is deleted in any processing state
- **THEN** the map remains readable with styles=[] and defaultStyleId=null, and its owned content remains available
- **AND** old processing cannot restore deleted style metadata

#### Scenario: Delete remaining styles concurrently

- **WHEN** distinct final styles are deleted concurrently from one map
- **THEN** both successful deletions may leave zero styles without deleting or resizing the map

#### Scenario: Preserve a source shared by several styles

- **WHEN** two styles share an asset and one or both styles are deleted
- **THEN** only their derived output and references are removed; original bytes remain until explicit eligible asset deletion

### Requirement: Delete a map and its complete owned content

Map DELETE SHALL retain empty 204, ownership cascades, sibling isolation, and durable derived-output cleanup. Active processing subscribers SHALL receive ProcessingUpdated with deleted:true and let clients leave the group. Its style source links, category icon links, and marker attachment rows SHALL disappear with their resources, but shared assets, preview links owned by surviving videos, and original bytes SHALL remain. No asset deletion SHALL follow automatically from deleting a map or game.

#### Scenario: Delete one of several maps

- **WHEN** a map containing groups, categories, markers, media, and processing styles is deleted
- **THEN** only that ownership hierarchy disappears and its active processing subscribers receive ProcessingUpdated with deleted:true and let clients leave the groups
- **AND** sibling maps, their content/files/counts/coordinates, and game timestamps remain unchanged

#### Scenario: Delete an empty map or recreate a name

- **WHEN** a map without styles is deleted
- **THEN** deletion succeeds and frees its game-scoped name
- **AND** a later map using that name receives a new identity and does not inherit deleted content or subscriptions

### Requirement: Remove map files durably and prevent stale publication

Map/style/game deletion and processing failure SHALL durably clean only map-owned tiles and work, never shared originals. Cleanup SHALL start from committed operations, retry known failures at their due time, and resume after restart without periodic table scans. Stale processing SHALL NOT restore deleted metadata or advertise output. No source-file leases SHALL be introduced; removing a style SHALL NOT independently block eligible asset deletion.

#### Scenario: Delete during image processing

- **WHEN** a style, map, or game is deleted during tiling
- **THEN** resource metadata disappears after commit and stale output cannot be advertised
- **AND** source assets remain unless explicitly deleted through the library, and only derived files are eventually removed
- **AND** native writing stops before its output is removed

#### Scenario: Retry filesystem cleanup

- **WHEN** derived-output deletion fails and the application restarts
- **THEN** cleanup resumes without deleting shared originals or unrelated outputs
- **AND** source assets remain discoverable even after their final style is gone
- **AND** once known cleanup completes, the idle worker does not repeatedly query for more cleanup

#### Scenario: Retry while prior cleanup remains pending

- **WHEN** a failed style is retried before its previous cleanup succeeds
- **THEN** old cleanup cannot delete output of the new attempt
- **AND** processing unrelated styles can continue

### Requirement: Keep storage durable configurable and outside the repository history

Originals SHALL reside in the configured library asset store; tiles and work SHALL retain map/style-addressed storage. All generated stores SHALL be Git-ignored with server-owned paths and no path/link traversal. Asset/source relationships and existing map processing metadata SHALL survive restart. Physical paths SHALL remain private even though committed original bytes are publicly served through library URLs.

#### Scenario: Use safe owned storage

- **WHEN** an upload filename or logical folder name contains path-like input
- **THEN** it cannot select storage paths or access other assets/maps
- **AND** original, tile, staging, and work stores remain untracked

#### Scenario: Recover admission interruption

- **WHEN** a library upload is interrupted or style attachment fails before commit
- **THEN** request-level finally removes the temporary upload and failed attachment queues no style
- **AND** committed sources remain, while a process crash may leave temporary/orphaned files with no startup cleanup

### Requirement: Return safe errors across HTTP and SSE phases

HTTP failures SHALL retain ProblemDetails and safe unexpected HTTP 500 responses. Missing maps SHALL be reported through HTTP reads; group joins SHALL perform no resource lookup. Notification failures SHALL be logged without exposing internal diagnostics or changing committed work. Processing errors SHALL remain durable failed style state. Deletion SHALL emit ProcessingUpdated with deleted:true.

#### Scenario: Fail before and after response start

- **WHEN** a client joins a group for a missing map and requests its HTTP representation
- **THEN** HTTP returns the existing safe not-found response
- **AND** unexpected notification failures do not change a successful commit and current state remains recoverable through HTTP

#### Scenario: Surface a native processing failure safely

- **WHEN** an image decoder or tile write fails after admission
- **THEN** the style becomes failed and its original remains available for retry
- **AND** subscribers receive a safe code and message after failure commits, without exception details or physical paths

### Requirement: Document maps content ownership and rendering metadata

OpenAPI SHALL describe the ten map REST operations, including CreateMap with optional status, the map metadata PATCH with name, slug, status, defaultStyleId and styleOrder, PublishMap at POST /games/{gameId}/maps/{mapId}/publish, explicit map scope, complete map/style representations with map status and independent per-style `tileGeometry` (without map-level dimension/geometry fields), default/order updates, retry, and static WebP delivery, without the removed SSE operation. Separate documentation SHALL define SignalR methods, events, lifecycle, and proxy requirements. AddMapStyle SHALL retain JSON name/backgroundColor/sourceAssetId. Documentation SHALL explain the fixed `[-100, 100]` logical space, uniform max-side style scaling, transient progress, interruption failure, and shared-original preservation.

#### Scenario: Discover independent map and style creation

- **WHEN** a client reads OpenAPI and map transport documentation
- **THEN** CreateMap accepts name, slug, and optional status JSON, UpdateMap includes optional status, PublishMap is exposed under the Maps tag at POST /games/{gameId}/maps/{mapId}/publish returning Map without map-level geometry fields, and AddMapStyle remains name/backgroundColor/sourceAssetId JSON with no file picker
- **AND** maps remain complete arrays/DTOs while file uploads occur only through the library and legacy routes remain absent
- **AND** the old events route returns 404 and SignalR negotiation is not presented as a generated REST streaming operation

### Requirement: Validate map names within their game

Map names SHALL be trimmed, preserve display spelling, contain 1..200 characters, and be unique per game after invariant case normalization. Duplicate names SHALL return HTTP 409 with name errors, including recognized save-time conflicts. Different games SHALL allow equal names. No editable map order or selected-map field SHALL be introduced. Map names SHALL also be editable through the scoped map metadata PATCH.

#### Scenario: Name uniqueness and scope

- **WHEN** a game already has World and another creation supplies ' world '
- **THEN** HTTP 409 preserves the existing map
- **AND** World is allowed in another game, and deleting the original frees its name

### Requirement: List all maps of a game

GET /games/{gameId}/maps SHALL return HTTP 200 with a plain array of complete map representations ordered by createdAt then id ascending. Existing games with no maps SHALL return []. Missing games SHALL return HTTP 404. Pagination, search, alternate sorting, and automatic selected-map state SHALL NOT be introduced.

#### Scenario: Complete deterministic isolated list

- **WHEN** a game owns blank and styled maps, including equal creation timestamps
- **THEN** each map appears once with its metadata and styles (without map-level geometry fields), ordered by createdAt then id
- **AND** maps of other games never appear

### Requirement: Patch map name without resizing

PATCH /games/{gameId}/maps/{mapId} SHALL accept optional name, slug, nullable description, status, defaultStyleId and styleOrder with shared merge-patch semantics and return 200 with the complete map. Name SHALL be non-null when supplied, trimmed, 1..200 characters and unique within the game excluding the current map. Description omission SHALL preserve the existing value, while explicit null or empty string SHALL clear it to null. Status SHALL be non-null (`draft` or `published`) when supplied and SHALL update only the map itself without cascading to owned category groups, categories, or markers. Map-level width, height, and geometry properties SHALL NOT exist, and supplied dimension or geometry fields SHALL be rejected. No PUT route SHALL be introduced.

#### Scenario: Rename a scoped map

- **WHEN** a valid patch changes the name of a map belonging to the route game
- **THEN** the name and normalized name are persisted with a new updatedAt
- **AND** createdAt, game timestamps, styles, description, status, and owned content remain unchanged; default and order remain unchanged when omitted

#### Scenario: Update and clear map description

- **WHEN** a valid patch supplies a non-empty description
- **THEN** the description is persisted and updatedAt advances
- **WHEN** a subsequent patch supplies `description: null` or `description: ""`
- **THEN** the description clears to `null` and updatedAt advances
- **WHEN** a patch omits `description`
- **THEN** the existing description is preserved

#### Scenario: Toggle map publication status without cascading to children

- **WHEN** a valid patch changes a map's status between `draft` and `published`
- **THEN** the map's status and updatedAt are updated while every owned category group, category, and marker retains its existing status and timestamps

#### Scenario: Reject invalid patches atomically

- **WHEN** a patch supplies null, blank or overlength name, null status, or an empty object
- **THEN** HTTP 422 returns field errors without changes
- **WHEN** the name conflicts with another map in the game, including a recognized save-time unique constraint conflict
- **THEN** HTTP 409 returns a name error without changes
- **WHEN** a patch contains width, height, geometry, other unknown/read-only properties, incompatible values, unknown status strings, a non-object body or malformed JSON
- **THEN** HTTP 400 preserves the existing map

#### Scenario: Preserve scope and supported media

- **WHEN** a valid identifier refers to a missing game/map or a map of another game
- **THEN** HTTP 404 preserves all resources
- **WHEN** the request uses application/json or application/merge-patch+json, including charset parameters
- **THEN** both use identical merge-patch behavior
- **WHEN** another media type is supplied
- **THEN** HTTP 415 rejects the request

#### Scenario: Preserve no-op timestamps

- **WHEN** a nonempty patch supplies the existing name after trimming, the existing description, and/or the existing status
- **THEN** HTTP 200 returns the unchanged complete map without changing updatedAt
- **AND** changing display casing alone is allowed without conflicting with the same map

#### Scenario: Patch slug with other metadata

- **WHEN** a patch supplies a valid unique slug with optional name, description, status, defaultStyleId or styleOrder changes
- **THEN** all supplied changes commit atomically and the complete response includes the new slug, description, and status
- **AND** omitted fields remain unchanged, actual changes update only the map timestamp, and a patch containing only its current trimmed slug preserves updatedAt
- **AND** renaming a map without supplying slug preserves its slug

### Requirement: Persist style order and validate map presentation patches

Map PATCH SHALL accept optional defaultStyleId and styleOrder alongside name and slug, returning 200 with the complete map under shared merge-patch rules. styleOrder SHALL be a complete permutation of current style IDs; new styles SHALL append. Changes SHALL be atomic, no-ops SHALL preserve timestamps, and effective changes SHALL update only the map timestamp.

#### Scenario: Validate default ownership and readiness

- **WHEN** defaultStyleId is supplied
- **THEN** a foreign or missing style returns 404; null or a non-ready style returns 422, with no accompanying changes

#### Scenario: Validate order and preserve fields

- **WHEN** styleOrder contains duplicates, null, or omits current IDs
- **THEN** 422 rejects the entire patch; foreign or missing IDs return 404 and omitted fields remain unchanged

#### Scenario: Reject stale membership

- **WHEN** styles are added or deleted before an older complete order is saved
- **THEN** the stale order is rejected without dropping styles or changing default/name

#### Scenario: Keep default independent

- **WHEN** a complete order permutation is saved
- **THEN** all style states participate in ordering, the ready default stays selected, and no tiles or markers change

### Requirement: Preserve existing map data during geometry transition

A new explicitly applied migration SHALL preserve every existing map dimension, marker coordinate, source and ready tile URL. Existing styles SHALL receive display order from creation sequence and existing effective ready defaults SHALL be preserved. Existing initialized maps without styles SHALL remain initialized. Applied migrations SHALL NOT be rewritten or run on startup.

#### Scenario: Upgrade a populated current store

- **WHEN** the migration runs against maps with styles, content and derived output
- **THEN** geometry and content survive, existing styles retain their order/default and existing tile bytes/URLs remain usable

### Requirement: Validate client-supplied map slugs

Map slugs SHALL be trimmed and contain 1..200 characters of lowercase ASCII letters or digits separated by single hyphens. Missing, null, empty, whitespace-only, overlength or invalid slugs SHALL return HTTP 422 with slug field errors without changes. New map slugs SHALL be client-supplied without generation, lowercasing or repair. PATCH omission SHALL preserve slug; explicit null SHALL be rejected.

#### Scenario: Accept a trimmed slug

- **WHEN** a valid create or PATCH supplies slug=" world-2 "
- **THEN** the persisted and returned slug is "world-2"

#### Scenario: Reject invalid slug values atomically

- **WHEN** creation omits slug or creation/PATCH supplies null, empty, whitespace-only, 201 characters, uppercase, non-ASCII letters, spaces, repeated hyphens, or leading/trailing hyphens
- **THEN** HTTP 422 identifies slug and preserves all resource fields and timestamps
- **AND** incompatible JSON types return 400 rather than being converted to strings

### Requirement: Enforce game-scoped map slug uniqueness

At most one map per game SHALL own a slug. Creation or PATCH using another map's slug in that game SHALL return HTTP 409, including recognized save-time conflicts. Keeping the target map's slug SHALL be allowed. Different games SHALL allow equal map slugs. Existing game-scoped name uniqueness SHALL remain independent. Deleting a map SHALL release its slug.

#### Scenario: Reject duplicate creation or update

- **WHEN** another map of the routed game owns the supplied slug
- **THEN** HTTP 409 rejects the complete creation or PATCH, including accompanying valid name and presentation changes

#### Scenario: Resolve competing writes

- **WHEN** otherwise valid competing creations or updates claim the same game-scoped slug
- **THEN** at most one map owns that slug and recognized constraint losers receive 409
- **AND** unrelated persistence errors remain safe 500 responses

#### Scenario: Preserve scope and reuse

- **WHEN** maps of different games use slug=world or a map keeps its existing slug
- **THEN** slug uniqueness permits the operation, subject to independent name validation
- **AND** deleting a map permits a later map of the same game to reuse its slug

### Requirement: Backfill map slugs without losing existing data

An explicit upgrade SHALL assign every preexisting map the slug map-{id:N}, where id:N is its existing UUID in lowercase without hyphens. It SHALL preserve map identities, game ownership, names, timestamps, geometry, defaults, styles, processing state, content and files. Existing game slugs SHALL remain unchanged. Upgrades SHALL NOT rewrite previously applied migrations or run automatically at API startup.

#### Scenario: Upgrade a populated store

- **WHEN** an operator applies the slug migration to a store containing maps with arbitrary names and content
- **THEN** every old map receives its deterministic valid slug and existing metadata, relationships and files survive
- **AND** required game-scoped slug uniqueness holds after restart

#### Scenario: Initialize a fresh store

- **WHEN** all migrations are explicitly applied to an empty database
- **THEN** map creation requires a valid client-supplied slug and equal slugs remain allowed across games

### Requirement: Persist optional map description via explicit migration

An explicit EF Core migration SHALL add a nullable `Description` column to `Maps`, initializing preexisting rows to `null` without altering existing map identities, slugs, geometry, styles, statuses, or timestamps. Applied migrations SHALL NOT be rewritten or run automatically at startup.

#### Scenario: Upgrade a populated store with map descriptions

- **WHEN** the map description migration is applied to a database with existing maps
- **THEN** preexisting maps receive `description: null` and retain all existing metadata, styles, content, and timestamps

### Requirement: Migrate existing marker coordinates to the fixed logical space

An explicit EF Core migration SHALL rescale preexisting persisted marker coordinates (`PositionX`, `PositionY`) on maps with non-null positive `Width` and `Height` by multiplying both coordinates by `200.0 / GREATEST(Width, Height)` in IEEE-754 double precision, and then drop the `Maps.Width` and `Maps.Height` columns. Markers belonging to maps whose `Width` or `Height` was null SHALL remain unchanged. Already applied migrations SHALL NOT be rewritten or run automatically at startup.

#### Scenario: Upgrade a populated store with square and rectangular maps

- **WHEN** the migration is applied to a database containing markers on a `4096x4096` map at `(1024, -512)` and on a `4000x2000` map at `(1000, 500)`
- **THEN** the first marker migrates to `(50, -25)` and the second marker migrates to `(50, 25)`
- **AND** `Maps.Width` and `Maps.Height` columns are dropped without altering map, style, category, or marker identities

#### Scenario: Preserve markers on a previously uninitialized blank map

- **WHEN** the migration is applied to a database containing markers on a map whose `Width` or `Height` is null
- **THEN** those marker coordinates are preserved unchanged and the migration completes without division-by-zero errors
