# Spec Delta

## MODIFIED Requirements

### Requirement: Deliver all committed files through stable library URLs

GET and HEAD /media/files/{id} SHALL serve immutable originals through static middleware without database access or Mediator when no image optimization query parameters (`width`, `format`) are supplied. Delivery SHALL provide correct MIME, nosniff, public caching, filesystem validators, conditional 304 and video byte ranges. When `width` or `format` query parameters are present, raster image optimization middleware SHALL run ahead of static delivery without altering the canonical asset URL or original bytes, while SVG and video assets SHALL continue to pass through to static original delivery. URLs SHALL survive metadata changes. Staging, cache internals, and directory listings SHALL remain private.

#### Scenario: Read seek and cache an asset

- **WHEN** a committed asset is requested without optimization query parameters, including an image used as a map original
- **THEN** its unchanged bytes and verified headers are available without a separate original-download restriction
- **AND** HEAD has no body, valid video ranges return 206, unsatisfiable ranges 416, and matching validators can return 304

#### Scenario: Serve a crash-orphaned original

- **WHEN** original bytes remain in the asset store without a matching database record after a crash
- **THEN** the original remains accessible by its known URL without requiring a database record
- **AND** no automatic cleanup or recovery service is introduced

#### Scenario: Optimize a committed raster asset without modifying the original

- **WHEN** a committed raster asset is requested with valid `width` or `format` query parameters
- **THEN** an optimized derivative is delivered according to the `media-image-optimization` specification without modifying the stored original
- **AND** requesting the bare `/media/files/{id}` URL continues to deliver the original unchanged

### Requirement: Delete only unused assets immediately

DELETE /media/assets/{id} SHALL return 409 while a game image, category icon, marker attachment, style, or video preview references it. Otherwise it SHALL delete the physical file immediately, remove its record, and invalidate any derived cached image variants for that asset on a best-effort basis before returning empty 204. Missing files SHALL be tolerated; repeated deletion SHALL return 404. Serialized relationship checks and restrictive FKs SHALL prevent dangling references. No active-reader leases, cleanup intents, deferred deletion, startup original-asset cleanup, or retry jobs SHALL be introduced.

#### Scenario: Reject persisted uses

- **WHEN** any current resource or preview references an asset
- **THEN** deletion returns 409 without detachment or byte changes
- **AND** uses across all games count even when original bytes are missing

#### Scenario: Delete an unused original

- **WHEN** the last relationship is removed and the client deletes the asset
- **THEN** the original is deleted synchronously, any derived image-cache variants for that asset are invalidated best-effort, and 204 follows record removal
- **AND** an IO failure deleting the original returns safe 500 while retaining metadata, without scheduling cleanup

#### Scenario: Accept independent storage and metadata failure boundaries

- **WHEN** a DB failure follows physical deletion or a crash interrupts the operation
- **THEN** metadata may reference a missing file or an orphan may remain
- **AND** no automatic reconciliation infrastructure is added

#### Scenario: Protect a game cover

- **WHEN** an image is selected by at least one game, including when its original file is missing
- **THEN** deleting the asset returns 409 and preserves both the asset metadata and game references

#### Scenario: Release a game cover

- **WHEN** every game using an image has been deleted or assigned another image
- **THEN** the former cover remains in the library and can be explicitly deleted only if no other resource references it

#### Scenario: Race image assignment and deletion

- **WHEN** a game image is assigned or replaced concurrently with deletion of that asset
- **THEN** the operations cannot commit a game referencing a deleted asset
- **AND** deletion refused for a committed game reference returns 409 before removing bytes

### Requirement: Use request-scoped staging without automatic recovery

Uploads SHALL use generated staging files with try/finally deletion after native readers finish. Committed unreferenced assets SHALL remain. The system SHALL NOT create media sessions, leases, expiry, cleanup-intent tables, or startup/periodic recovery workers for upload staging or orphaned original assets. Any background eviction service registered for derived image-cache variants SHALL operate strictly within the dedicated derivative cache root and SHALL NEVER scan or delete upload staging or original assets.

#### Scenario: Finish or cancel an ordinary upload

- **WHEN** upload handling completes, rejects input, or observes cancellation
- **THEN** finally removes its temporary file after readers return
- **AND** committed assets are not removed by temporary-file cleanup

#### Scenario: Accept files remaining after a crash

- **WHEN** the process crashes before finally or between file promotion and DB commit
- **THEN** temporary or orphaned files may remain
- **AND** startup and background processing do not scan, recover, or remove these files

#### Scenario: Keep native capability checks without a maintenance worker

- **WHEN** the API starts
- **THEN** necessary media processor capability checks still run explicitly
- **AND** no upload-staging or original-asset cleanup worker is registered, while derivative image-cache eviction remains isolated to the dedicated cache directory
