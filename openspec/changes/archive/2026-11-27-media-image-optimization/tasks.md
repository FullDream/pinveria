# Tasks

## 1. Application Contracts, Options, and Asset-Deletion Cache Invalidation

- [x] 1.1 Create `src/Pinveria.Application/Media/Optimization/` value objects (`ImageTransformCommand`, `ImageOutputFormat`, `ImageVariantResult`), validated option models (`ImageOptimizationOptions`, `ImageCacheOptions`), and ports (`IImageVariantService`, `IImageProcessor`, `IImageVariantCache`) without introducing `Domain` dependencies, and verify `dotnet build src/Pinveria.Application/Pinveria.Application.csproj` succeeds.
- [x] 1.2 Wire best-effort `IImageVariantCache.InvalidateAsset(command.Id)` into `DeleteMediaAssetHandler` after `storage.DeleteAsset(command.Id)`, and verify with unit tests in `tests/Pinveria.Application.Tests` that successful asset deletion invalidates cached variants and cache invalidation failures do not block asset deletion or 409/404 behavior.

## 2. NetVips Image Processor, Smart Filesystem Cache, and Eviction Service

- [x] 2.1 Extract a shared `MediaSignatureDetector` (reused by `MediaFileContentTypeProvider`) and implement `VipsImageProcessor` in `src/Pinveria.Infrastructure/Media/Optimization/` using `NetVips` shrink-on-load (`Image.Thumbnail` with `Size.Down`), metadata stripping (`ForeignKeep.None`), alpha-preserving fallbacks, `SemaphoreSlim(MaxConcurrency)` with `QueueTimeoutSeconds`, and encoder defaults (`AVIF q=50 effort=4`, `WebP q=80 effort=4`, `JPEG q=82`), and verify with `tests/Pinveria.IntegrationTests` covering AVIF/WebP/JPEG/PNG output, no-upscale clamping, transparency preservation, and queue-timeout fallback.
- [x] 2.2 Implement `FileSystemImageVariantCache` in `src/Pinveria.Infrastructure/Media/Optimization/` with symlink rejection, atomic `*.tmp` writes, bloat-sentinel (`.pass`) support, throttled `LastWriteTimeUtc` touch, asset directory invalidation, and two-stage `SweepAsync` (idle TTL eviction older than `MaxIdleDays`, followed by oldest-first `EvictFraction = 0.15` quota eviction when exceeding `MaxBytes` and empty-folder cleanup), and verify with automated tests covering TTL expiry, 15% quota hysteresis, atomic writes, throttled touch, and asset invalidation.
- [x] 2.3 Implement `ImageVariantService` (singleflight deduplication, SVG/video passthrough, no-upscale `orig` key folding, format-only size-bloat guard) and `ImageCacheSweepService` (`BackgroundService` cleaning orphaned `*.tmp` files on startup and running periodic + threshold-triggered `SweepAsync` strictly within `CacheRoot`), register them with `ImageOptimizationOptionsValidation` in `Pinveria.Infrastructure/DependencyInjection.cs`, and verify startup validation and singleflight deduplication with tests in `tests/Pinveria.IntegrationTests`.

## 3. ImageSharp.Web-Style HTTP Middleware, Deployment Volume, and Backend Docs

- [x] 3.1 Implement `ImageCommandParser` (orthogonal `?width` allowlist and `?format=auto|avif|webp|jpeg|png` + `Accept` negotiation) and `ImageOptimizationMiddleware` in `src/Pinveria.Api/Media/`, wire them inside `UseMediaFiles()` ahead of `UseStaticFiles()`, and verify via HTTP tests in `tests/Pinveria.IntegrationTests` covering bare-URL passthrough, `width`-only resize, `format=auto` with `Vary: Accept`, explicit `format` without `Vary: Accept`, HTTP `400` on invalid/duplicate params, `HEAD` and `If-None-Match` `304`, SVG `CSP: sandbox` passthrough, video `Range` passthrough, `no-store` busy fallback, and cache cleanup after `DELETE /media/assets/{id}`.
- [x] 3.2 Add the dedicated `media_cache` volume and `Media__ImageCache__CacheRoot: /var/lib/pinveria/media-cache` environment variable to `compose.yml`, update `src/Pinveria.Api/Media/README.md` and `docs/deployment.md` with the query contract and cache volume/options, and verify `docker compose config` (or compose YAML validation) and `dotnet test` pass cleanly.

## 4. Shared Frontend Image Loader and Responsive Media Adoption

- [x] 4.1 Add `MEDIA_IMAGE_BREAKPOINTS`, `MediaImageFormat`, `snapMediaImageWidth`, and `buildOptimizedMediaUrl` to `frontend/libs/shared/util` and `provideMediaImageOptimization()` (`IMAGE_CONFIG` + `IMAGE_LOADER` defaulting to `format=auto` with `loaderParams.format` override and non-media URL passthrough) to `frontend/libs/shared/ui`, register `provideMediaImageOptimization()` in `frontend/apps/web/src/app/app.config.ts` and `frontend/apps/admin/src/app/app.config.ts`, and verify with `npm exec nx -- run-many -t lint build -p web,admin` from `frontend/`.
- [x] 4.2 Add optional `sizes` and `format` inputs to `MediaThumbnail` and `ImagePreview`, supply container-appropriate `sizes` hints across `web` and `admin` consumers, upgrade raw media `<img>` elements in `libs/maps/feature-public-map` (`category-filter-panel.html`, `marker-popup.html`) to `NgOptimizedImage`, route `/media/files/...` icon URLs in `libs/maps/ui` (`CategoryIcon`, `map-markers.ts`) through `buildOptimizedMediaUrl(url, { width: 160, format: 'auto' })`, update `docs/frontend/architecture.md`, and verify `npm exec nx -- run-many -t lint build -p web,admin` and existing `shared-ui` / `media-feature-list` tests continue to pass without regressions.

## 5. End-to-End Verification

- [x] 5.1 Run full backend test suite (`dotnet test`) and frontend lint/build checks (`npm exec nx -- run-many -t lint build -p web,admin` from `frontend/`) and verify zero regressions across media delivery, map tiles, SSR HTML rendering, and cache eviction.

## Workflow follow-up

- Review the proposal, specs, design, and tasks before starting implementation.
- Archive the change with `openspec archive` after implementation and verification are complete.
