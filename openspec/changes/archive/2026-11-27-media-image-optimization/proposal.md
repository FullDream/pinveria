# Proposal

## Why

Pinveria currently serves full-resolution original uploads for every image across the public `web` app and `admin` workspace—including multi-megabyte game covers, banners, marker photos, and map originals up to 300 MiB—without resizing or modern format negotiation (`AVIF` / `WebP`). Adding on-demand image resizing and format transcoding with a bounded local NVMe cache directly inside `Pinveria.Api` (reusing the existing `NetVips` / `libvips` runtime) dramatically reduces bandwidth and improves LCP on our `1 vCore / 2 GB RAM / 40 GB NVMe` VDS without introducing extra proxy/cache containers.

## What Changes

- **ImageSharp.Web-style HTTP middleware on `/media/files/{id}` (`Pinveria.Api`)**:
  - Intercept `GET` and `HEAD /media/files/{id}` ahead of `UseStaticFiles` when orthogonal optimization query parameters `width` and/or `format` are supplied.
  - Keep bare `/media/files/{id}` (no optimization query parameters) as a zero-overhead passthrough to `UseStaticFiles` serving immutable originals unchanged (preserving video byte ranges, downloads, and server-side OG image inputs).
  - Restrict `width` to a validated geometric allowlist (`160, 320, 480, 640, 960, 1280, 1600, 1920`) and `format` to `auto | avif | webp | jpeg | png`, rejecting invalid values with HTTP `400`.
  - Negotiate the best format from `Accept` (`image/avif` → `image/webp` → fallback) when `format=auto` (emitting `Vary: Accept`), honor explicit `format` values without `Vary: Accept`, prevent upscaling (clamping `width >= naturalWidth` to a shared original-size cache key), and pass `image/svg+xml` (preserving its `sandbox` CSP) and `video/*` straight through to `UseStaticFiles`.
- **Clean Architecture image optimization contracts & NetVips processor (`Pinveria.Application`, `Pinveria.Infrastructure`)**:
  - Introduce focused, DDD-aligned Application contracts (`ImageTransformCommand`, `ImageOutputFormat`, `ImageVariantResult`, `IImageVariantService`, `IImageProcessor`, `IImageVariantCache`) and validated options (`ImageOptimizationOptions`, `ImageCacheOptions`) while keeping `Pinveria.Domain` free of delivery/transcoding concerns.
  - Implement `VipsImageProcessor` using `NetVips` shrink-on-load / sequential decoding (`Image.Thumbnail` with `Size.Down`), metadata stripping, `SemaphoreSlim(1)` bounded concurrency for `1 vCore`, singleflight deduplication per variant key, size-bloat guard (falling back to original when format-only transcoding does not shrink the payload), and queue-timeout fallback (`Cache-Control: no-store`).
- **Smart local filesystem cache & Docker volume (`Pinveria.Infrastructure`, `compose.yml`)**:
  - Persist derived image variants in a dedicated `media_cache` Docker volume (`{CacheRoot}/{assetId:N}/{widthOrOrig}.{ext}`) via atomic temp-file writes and symlink rejection.
  - Track last access reliably on Linux `relatime`/`noatime` mounts via throttled `LastWriteTimeUtc` touches.
  - Run a dedicated cache-eviction `BackgroundService` (isolated strictly to the disposable derivative cache, never touching originals or upload staging) that deletes idle entries older than `MaxIdleDays` (default `30` days), evicts the oldest `15%` (`EvictFraction = 0.15`) whenever total cache size exceeds `MaxBytes` (default `2 GiB`), cleans orphaned `*.tmp` files on startup, and removes empty asset folders.
  - Invalidate an asset's cached variants immediately (best-effort) when `DELETE /media/assets/{id}` removes the asset.
- **Shared Angular image optimization integration (`frontend`)**:
  - Add shared URL/breakpoint constants (`MEDIA_IMAGE_BREAKPOINTS`, `MediaImageFormat`, `buildOptimizedMediaUrl`) in `libs/shared/util` and `provideMediaImageOptimization()` (`IMAGE_LOADER` + `IMAGE_CONFIG`) in `libs/shared/ui`, registered in both `apps/web` and `apps/admin`.
  - Default the Angular `IMAGE_LOADER` to `format=auto` for `/media/files/...` URLs while allowing explicit per-image overrides via `[loaderParams]="{ format: '...' }"`.
  - Add responsive `sizes` (and optional `format`) support to `ui-media-thumbnail` and `ui-image-preview`, upgrade remaining raw `<img>` tags rendering media assets in `web` (`category-filter-panel`, `marker-popup`) to `NgOptimizedImage`, and route raster category icons in `maps/ui` through `buildOptimizedMediaUrl` (`width=160&format=auto`, with SVGs transparently passed through).

### Out of Scope

- Map tile pyramids (`/map-tiles/*`)—already pre-generated as lossless WebP tiles by `VipsMapImageProcessor`.
- Static frontend bundle assets (such as the catalog hero `.webp` art)—handled as static build files, not backend media library assets.
- External image proxies (`imgproxy`), CDN/object storage (S3), or custom Caddy/Nginx caching plugins.
- Advanced image edits (arbitrary cropping, focal points, watermarks, DPR/quality query parameters exposed to untrusted clients).
- Frontend unit/E2E test creation (remains deferred under the project's current frontend verification policy; existing tests remain preserved and passing).

## Capabilities

### New Capabilities

- `media-image-optimization`: On-demand raster image resizing (`width`) and format transcoding (`format=auto|avif|webp|jpeg|png`) over `/media/files/{id}` through an `ImageSharp.Web`-style middleware pipeline, `NetVips` singleflight/bounded-concurrency processing, a smart NVMe filesystem variant cache (throttled LRU, idle TTL, 15% quota eviction), and shared Angular `NgOptimizedImage` integration across `web` and `admin`.

### Modified Capabilities

- `media-library`: Extend `/media/files/{id}` delivery with optional `width`/`format` query-driven image optimization while preserving bare-URL original delivery, video byte ranges, and SVG sandbox CSP; invalidate derived cached variants on `DELETE /media/assets/{id}`; clarify that the no-background-cleanup-worker constraint applies to upload staging/orphan recovery while the disposable derivative image cache runs its own bounded eviction service.

## Impact

- **Backend (`src/Pinveria.Application`, `src/Pinveria.Infrastructure`, `src/Pinveria.Api`, `tests/`)**: New `Media/Optimization` feature slices across Application, Infrastructure, and Api; shared media signature detector; updated `MediaFileMiddlewareExtensions` and `DeleteMediaAssetHandler`; new `Media:Optimization` and `Media:ImageCache` configuration options with startup validation; comprehensive unit and HTTP integration tests.
- **Infrastructure & Deployment (`compose.yml`, `docs/deployment.md`)**: New `media_cache` volume mounted at `/var/lib/pinveria/media-cache` and `Media__ImageCache__CacheRoot` env var on the `api` service; no new containers or Caddy routing changes required.
- **Frontend (`libs/shared/util`, `libs/shared/ui`, `libs/maps/ui`, `libs/maps/feature-public-map`, `apps/web`, `apps/admin`, consumers of `ui-media-thumbnail`/`ui-image-preview`)**: Shared media image loader/breakpoints provider, responsive `sizes` propagation, and `NgOptimizedImage` adoption for remaining media `<img>` elements.
- **Documentation (`src/Pinveria.Api/Media/README.md`, `docs/frontend/architecture.md`, `docs/deployment.md`)**: Document query-driven image optimization contract, cache configuration/volume, and Angular `IMAGE_LOADER` conventions.
