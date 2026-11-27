## ADDED Requirements

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

### Requirement: Migrate existing marker coordinates to the fixed logical space

An explicit EF Core migration SHALL rescale preexisting persisted marker coordinates (`PositionX`, `PositionY`) on maps with non-null positive `Width` and `Height` by multiplying both coordinates by `200.0 / GREATEST(Width, Height)` in IEEE-754 double precision, and then drop the `Maps.Width` and `Maps.Height` columns. Markers belonging to maps whose `Width` or `Height` was null SHALL remain unchanged. Already applied migrations SHALL NOT be rewritten or run automatically at startup.

#### Scenario: Upgrade a populated store with square and rectangular maps

- **WHEN** the migration is applied to a database containing markers on a `4096x4096` map at `(1024, -512)` and on a `4000x2000` map at `(1000, 500)`
- **THEN** the first marker migrates to `(50, -25)` and the second marker migrates to `(50, 25)`
- **AND** `Maps.Width` and `Maps.Height` columns are dropped without altering map, style, category, or marker identities

#### Scenario: Preserve markers on a previously uninitialized blank map

- **WHEN** the migration is applied to a database containing markers on a map whose `Width` or `Height` is null
- **THEN** those marker coordinates are preserved unchanged and the migration completes without division-by-zero errors

## REMOVED Requirements

### Requirement: Keep all styles aligned in the map's immutable geometry

**Reason**: Replaced by `Align all styles in the fixed centered coordinate space`, removing first-style logical dimension initialization and independent X/Y aspect-ratio stretching.

**Migration**: Apply the `RemoveMapLogicalDimensions` EF Core migration to rescale preexisting markers into `[-100, 100]` and drop `Maps.Width` and `Maps.Height`.

## RENAMED Requirements

- FROM: `### Requirement: Use centered original-pixel coordinates`
- TO: `### Requirement: Use centered normalized logical coordinates`

## MODIFIED Requirements

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

### Requirement: Return complete map metadata

GET /games/{gameId}/maps/{mapId} SHALL include id, gameId, required string name, required string slug, nullable string description, string status (`draft` or `published`), nullable defaultStyleId, styles in persisted display order, createdAt, and updatedAt. Map representations SHALL NOT expose map-level width, height, extent, tileOrigin, tileSize, minZoom, maxZoom, or resolutions fields. Style representations SHALL expose their own source-based `tileGeometry`. defaultStyleId SHALL identify the effective ready default or be null. IDs SHALL use UUID v7; internal paths SHALL remain private.

#### Scenario: Read a blank or styled map

- **WHEN** a client reads a map before attaching styles, with active styles, or after deleting all styles
- **THEN** the representation includes its metadata, nullable description, status, defaultStyleId, and ordered styles without map-level dimension or geometry fields

#### Scenario: Read ordered styles

- **WHEN** styles have been reordered
- **THEN** GET and mutation map responses return the same persisted order and each style's source-based `tileGeometry`

### Requirement: Use centered normalized logical coordinates

Logical coordinates SHALL use a universal fixed Cartesian coordinate space centered at `(0, 0)`, with X increasing right, Y increasing up, and reference span `200` (`[-100, -100, 100, 100]`). Each style SHALL map its source center `(0, 0)` to logical `(0, 0)` with uniform scale `200 / max(sourceWidth, sourceHeight)` so its logical footprint is `[-sourceWidth * scale / 2, -sourceHeight * scale / 2, sourceWidth * scale / 2, sourceHeight * scale / 2]`. Existing finite marker coordinates, including negative, fractional, or out-of-extent positions, SHALL remain valid without rounding or bounds rejection.

#### Scenario: Place the same marker on all styles

- **WHEN** a map contains a marker at `(0, 0)` and a marker at `(50, 25)` across `4096x4096` and `4096x2048` styles (or styles at different resolutions of the same aspect ratio)
- **THEN** `(0, 0)` remains at every style's center and `(50, 25)` remains at the same world position without distorting either image

#### Scenario: Preserve fractional geometry and existing markers

- **WHEN** style dimensions are odd or markers have negative, fractional, or beyond-`[-100, 100]` finite positions
- **THEN** half-unit style tile boundaries and marker coordinates are preserved without rounding or bounds validation

### Requirement: Document maps content ownership and rendering metadata

OpenAPI SHALL describe the ten map REST operations, including CreateMap with optional status, the map metadata PATCH with name, slug, status, defaultStyleId and styleOrder, PublishMap at POST /games/{gameId}/maps/{mapId}/publish, explicit map scope, complete map/style representations with map status and independent per-style `tileGeometry` (without map-level dimension/geometry fields), default/order updates, retry, and static WebP delivery, without the removed SSE operation. Separate documentation SHALL define SignalR methods, events, lifecycle, and proxy requirements. AddMapStyle SHALL retain JSON name/backgroundColor/sourceAssetId. Documentation SHALL explain the fixed `[-100, 100]` logical space, uniform max-side style scaling, transient progress, interruption failure, and shared-original preservation.

#### Scenario: Discover independent map and style creation

- **WHEN** a client reads OpenAPI and map transport documentation
- **THEN** CreateMap accepts name, slug, and optional status JSON, UpdateMap includes optional status, PublishMap is exposed under the Maps tag at POST /games/{gameId}/maps/{mapId}/publish returning Map without map-level geometry fields, and AddMapStyle remains name/backgroundColor/sourceAssetId JSON with no file picker
- **AND** maps remain complete arrays/DTOs while file uploads occur only through the library and legacy routes remain absent
- **AND** the old events route returns 404 and SignalR negotiation is not presented as a generated REST streaming operation

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
