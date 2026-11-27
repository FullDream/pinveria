# Media library

The media library is global. Logical folders are created manually; they are independent of games and storage paths.
The root is represented by an omitted or null folder ID, not by a persisted folder.

## Browsing

`GET /media?folderId={id}&offset=0&limit=24&search={text}` returns
`{ items, limit, offset, total }` using `PaginatedResult<MediaItemDto>`. Offset must be nonnegative; limit is 1 through 120 (default 24).
Omit `folderId` to browse the root. An unknown folder returns 404.
`GET /media/folders/{id}` returns `{ folder, ancestors }`. `folder` is the complete selected folder.
`ancestors` contains `{ id, name }` parents
from the topmost to the nearest, excluding the current folder and synthetic root; no request is made for the synthetic root.
Search and pagination never filter this navigation context. It can be cached independently of list pages.

Only immediate contents are returned. Folders come first, then image and video assets together;
each group is ordered by name and ID. Search is a trimmed case-insensitive literal name substring (`ToLower()` + `EF.Functions.Like`).
It does not recurse or search other folders.
`total` counts matches before pagination. Folder `itemsCount` includes immediate child folders and files,
independently of the search filter or physical file availability.

Every item has a required `type`: `folder`, `image`, or `video`. Asset-only responses exclude folders.
Image metadata includes nullable dimensions, shared `alt`, and nullable read-only `placeholder`.
Video metadata includes an optional shared image `preview`.

## Folders

- `POST /media/folders`: `{ "name": "Icons", "parentId": null }`, returning 200 with folder metadata.
- `PATCH /media/folders/{id}`: optional `name` and `parentId`, returning 200.
- `DELETE /media/folders/{id}`: 204 when empty, 404 when missing, 409 when it contains folders or assets.

Names contain 1 through 200 trimmed characters. Sibling folder names are unique after invariant uppercase
normalization, including Latin and Cyrillic. Root names use the same rule. Asset names may repeat.
Moving a folder changes its parent without changing descendant IDs. A null parent moves it to root.
Self and descendant moves return 422. Unknown parents return 404; name conflicts return 409.

## Shared metadata updates

`PATCH /media/assets/{id}` accepts optional `name`, `folderId`, `alt`, and `previewAssetId`.
Names are required if supplied. A null folder moves an asset to root. Images permit non-null `alt`;
videos permit an image preview ID or null to clear it. Incompatible subtype metadata returns 422.
Missing references return 404. An unchanged effective value preserves the asset timestamp.
Bytes, MIME, dimensions, original URL, and placeholder cannot be changed through metadata updates.

All PATCH routes accept `application/merge-patch+json` and `application/json`, including charset parameters,
with identical JSON Merge Patch semantics. Omitted fields remain unchanged. Empty objects return 422;
unknown/read-only fields, arrays, and malformed JSON return 400. Other media types return 415.

Names and alt are plain text, never HTML. Consumers must use text bindings and attribute escaping.
Display SVG only in image contexts, never insert its source through `innerHTML`.

## Inspection profile

Raster formats are static JPEG, PNG, WebP, and AVIF. Native dependencies are pinned in Infrastructure.
The image side limit is 16384 pixels. Raster images larger than 32 pixels on either side receive a
persisted low-quality WebP data URI, bounded to 8192 URI characters by default. Both sides at or below
32 pixels skip generation. SVG always has a null placeholder and does not require an SVG renderer.
Clients displaying placeholders need `data:` in their image CSP. Original bytes remain unchanged.

SVG uses an XmlReader allowlist for static shapes, paths, groups, gradients, clips, masks, and internal
paint references. Scripts, event handlers, foreignObject, style/CSS, animation, external references,
data URIs, processing instructions, custom namespaces, DTDs and entities are rejected, not sanitized.
The default budgets are depth 64, 100000 elements, 64 attributes per element, and 4194304 XML characters
(`Media:Limits:MaximumXmlCharacters`). These parsing budgets are separate from the 300 MiB upload limit;
the smaller bound always applies.

## Video inspection deployment

MP4/WebM metadata is inspected with `MediaInfo.Wrapper.Core` and its `MediaInfo.Core.Native` dependency,
pinned to version 26.10.0. Native binaries are distributed through NuGet for Windows and Linux;
no ffprobe/FFmpeg executable, global installation or PATH modification is required.
Publish for the server RID (for example `linux-x64`) and verify native loading on the actual Linux image.
Linux runtime prerequisites such as zlib must be present in that image.

Only an opened local stream is passed to MediaInfo; URLs, subtitle discovery and directory analysis are not used.
`Media:Limits:VideoInspectionBytes` defaults to 8 MiB of cumulative reads (including rereads after seeking),
`ConcurrentVideoInspections` defaults to 2, and `ProbeTimeoutSeconds` defaults to a cooperative 30-second deadline.
Cancellation is observed between native parsing blocks; it cannot forcibly terminate a synchronous native call.
The reader, request staging file and concurrency slot remain alive until that call returns.
Metadata is inspected once during upload without transcoding or automatic video preview generation.

## Upload, delivery, and deletion

`POST /media/assets` accepts one multipart `file` and optional text fields `name`, `folderId`, `alt` (images only), and `previewAssetId` (videos only). Fields may precede or follow the file. Missing name uses the basename, never a client filesystem path. Folder omission selects root; duplicate file names are allowed. Unknown/duplicate fields and multiple/missing files return 400. A missing destination/preview returns 404; unsupported content or incompatible metadata returns 422. Unsupported request media returns 415.

The default file limit is exactly 314572800 bytes (300 MiB), counted while streaming without trusting Content-Length. Allow another 65536 bytes for multipart overhead at the reverse proxy. Text fields are individually bounded to 8192 bytes. Configure `Media:StorageRoot` (default `storage/media`) and `Media:Limits` for deployment; keep originals/staging on one durable volume for atomic promotion. Both media storage and map work/tiles are Git-ignored. Do not expose the media root through static-file middleware.

HTTP 200 means the immutable original and its metadata have committed. Each request uses a generated staging file and deletes that temporary file in try/finally after inspection/native readers finish, including rejection and cancellation. A lost response after commit does not delete an accepted asset: browse before uploading again. Committed unreferenced assets are retained indefinitely. Resources attach existing IDs; detaching or deleting a resource does not delete its assets.

`GET` and `HEAD /media/files/{assetId}` serve immutable originals through static-file middleware without database access or Mediator, including map originals, with MIME determined from admitted file signatures, `X-Content-Type-Options: nosniff`, ETag/Last-Modified validators and 304. Videos support byte ranges (206/416). Manually missing original bytes return 404. Delivery uses filesystem validators and public immutable caching, independently of asset records; crash-orphaned originals can remain accessible by known URL. File URLs are documented in DTOs rather than a separate OpenAPI operation. Metadata, folder counts, and placeholders remain available when bytes are manually deleted. Temporary paths and directory listings are never public.

For raster assets, `GET` and `HEAD /media/files/{assetId}` also accept optional `width` (`160, 320, 480, 640, 960, 1280, 1600, 1920` by default via `Media:ImageOptimization:AllowedWidths`) and `format` (`auto | avif | webp | jpeg | png`, case-insensitive) query parameters handled ahead of static-file delivery by `ImageOptimizationMiddleware`:
- Omitting both `width` and `format` passes straight through to unmodified original static-file delivery without `Vary: Accept`.
- Invalid, empty, disallowed, or duplicate `width` or `format` values return HTTP `400`.
- `format=auto` negotiates the request `Accept` header in priority order (`image/avif` → `image/webp` → alpha-preserving fallback) and emits `Vary: Accept`. Explicit `format` values (`avif`, `webp`, `jpeg`, `png`) or `width`-only requests omit `Vary: Accept`.
- Raster resizing uses `NetVips` shrink-on-load (`Image.Thumbnail` with `Size.Down`), strips metadata (`ForeignKeep.None`), and never upscales (`width >= intrinsicWidth` clamps to original dimensions and shares the `orig.{ext}` cache key). Format-only `format=auto` requests whose output would be `>=` the original byte length fall back to the original and record a `.pass` sentinel.
- `image/svg+xml` and `video/*` requests bypass raster optimization and pass through to `UseStaticFiles`, preserving the SVG `sandbox` CSP and video byte-range semantics.
- Concurrency is bounded by `Media:ImageOptimization:MaxConcurrency` (default `1`) and `QueueTimeoutSeconds` (default `8`); queue timeouts fall back to the original with `Cache-Control: no-store`.
- Generated variants are cached atomically under `Media:ImageCache:CacheRoot` (default `storage/media-cache`, mounted as `/var/lib/pinveria/media-cache` in production), separate from original `Media:StorageRoot`. `ImageCacheSweepService` cleans orphaned `*.tmp` files in `CacheRoot` on startup, evicts variants idle longer than `MaxIdleDays` (default `30`), and when total cache size exceeds `MaxBytes` (default `2 GiB`), evicts the oldest entries down to `85%` (`EvictFraction = 0.15`).

SVG responses include `sandbox; default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'`, also for HEAD/304. These headers supplement admission validation and plain-text UI bindings. On 2026-10-24, a local policy fixture in Chrome 155.0.8059.86 verified accepted gradients/clips/masks through canvas pixels and blocked document framing. HTTP integration tests independently verify actual API headers. Windows native fixtures pass; a linux-x64 publish includes MediaInfo/libzen binaries, but Linux runtime execution has not been performed in this environment. Repeat native checks on the deployed Linux image. Distribution license notices remain a release prerequisite as agreed.

`DELETE /media/assets/{id}` returns 409 for category icons, marker attachments, map-style sources, or video previews. An unused asset's physical original is deleted immediately, its record is removed, and any cached image variants under `{CacheRoot}/{id:N}` are removed on a best-effort basis before returning 204. Repeated deletion returns 404; missing bytes do not prevent deleting an unused record. Storage deletion failures return safe 500 and retain metadata, without scheduling retries. Once a style is deleted, an ongoing native reader does not independently protect the asset: platform file locks can cause a storage error, or disappearance can fail that attempt.

For original media storage (`Media:StorageRoot`), there are no media admission sessions, file leases, cleanup-intent tables, periodic checks, startup staging scans, expiry, or automatic recovery (only the disposable derivative cache under `Media:ImageCache:CacheRoot` runs `ImageCacheSweepService`). Capability checks still run explicitly at API startup. Process crashes may leave temporary or orphaned files in original storage. Filesystem operations and PostgreSQL commits are not atomic: DB failure after physical deletion can leave metadata pointing at missing bytes, and an interrupted promotion can leave an orphan. These accepted limitations do not trigger automatic reconciliation. There is no automatic detachment, unused-file expiry, or missing-file scanner.

## Game covers

Game coverId and bannerId references are protected uses: deleting a selected cover or banner returns 409, even if its original file is missing. Replacing a cover or deleting a game releases that reference without deleting the asset. Explicit asset deletion succeeds only after all game and other resource references have been removed.
