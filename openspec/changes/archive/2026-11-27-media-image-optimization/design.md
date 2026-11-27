# Design

## Context

Pinveria runs as a containerized layered monolith on a single `1 vCore / 2 GB RAM / 40 GB NVMe` VDS (`postgres`, `api`, `web`, `caddy` in `compose.yml`). See `proposal.md` for motivation and `specs/media-image-optimization/spec.md` for behavioral contracts.

Key existing constraints:
- `Pinveria.Api` already ships `NetVips` (`libvips`) with verified `PNG`, `JPEG`, `WebP`, and `AVIF` codecs (`VipsMapImageProcessor.CheckCapabilities` and `RasterInspector`).
- Committed media files live in `media_storage` (`/var/lib/pinveria/media/assets/{guid:N}` without file extensions) as immutable originals up to `300 MiB` and `16384x16384px`, served through `MediaFileMiddlewareExtensions.UseMediaFiles()` under `/media/files/{id}`.
- `Caddyfile` already forwards `/media/files/*` unchanged to `api:8080`, and dev proxies (`apps/admin/proxy.conf.cjs`, `apps/web/proxy.conf.cjs`) already forward `/media/files/**` to `http://localhost:5041`.
- Both Angular apps (`web` SSR and `admin` SPA) render most media through shared `ui-media-thumbnail` / `ui-image-preview` (`NgOptimizedImage`), while map category icons use `maps-category-icon` / OpenLayers canvas loading and two public-map templates still use plain `[src]`.

## Goals / Non-Goals

**Goals:**
- Deliver an `ImageSharp.Web`-style transparent HTTP optimization middleware on `/media/files/{id}` without creating a separate HTTP endpoint family or reverse-proxy route.
- Keep `Pinveria.Domain` strictly free of HTTP, encoding, and caching concerns while modeling clear Application contracts and Infrastructure implementations.
- Bound CPU and memory usage safely on `1 vCore / 2 GB RAM`, including thumbnail generation for `16384x16384px` / `300 MiB` map originals.
- Provide a self-maintaining local filesystem cache in a dedicated Docker volume (`media_cache`) with reliable Linux LRU tracking, idle TTL eviction, and `15%` quota hysteresis.
- Integrate cleanly with Angular `NgOptimizedImage` (`IMAGE_LOADER`, `IMAGE_CONFIG`, `loaderParams.format`, `sizes`) without SSR hydration mismatches.

**Non-Goals:**
- Adding external image-proxy containers (`imgproxy`), custom Caddy cache builds (`xcaddy`), or Nginx caching layers.
- Modifying map tile delivery (`/map-tiles/*`), static frontend bundle assets, or original media storage semantics.
- Exposing unconstrained client-controlled dimensions, crop boxes, or quality parameters.

## Decisions

### 1. Embedded `NetVips` pipeline in `Pinveria.Api` over `imgproxy` + caching proxy

**Decision:** Implement image optimization directly inside `Pinveria.Api` using the already-bundled `NetVips` runtime and a dedicated filesystem cache volume (`media_cache`).

**Rationale:**
- `imgproxy` is stateless and has no built-in filesystem cache; adopting it on our stack would require adding both `imgproxy` and a caching proxy (`nginx` or custom `xcaddy` + `souin`) with custom `Accept`-header normalization—adding 1–2 containers and ~150–300 MB of baseline RAM on a 2 GB VDS.
- `Pinveria.Api` already loads `libvips` with AVIF/WebP/PNG/JPEG support, already inspects media signatures, already enforces the SVG `sandbox` CSP, and already owns `DeleteMediaAssetHandler` for immediate cache invalidation.

**Alternatives considered:**
- `imgproxy` + Nginx `proxy_cache`: Strong process isolation and zero custom image code, but heavy for 2 GB RAM, adds `Accept` cache-key normalization complexity, requires duplicating SVG CSP and video/map-original exclusions, and cannot immediately purge cache on asset deletion.
- Pre-generating all variants on upload: Wastes disk space and upload latency for widths/formats never requested.

### 2. `ImageSharp.Web`-inspired pipeline mapped to Clean Architecture & DDD

**Decision:** Mirror `ImageSharp.Web`'s composable processing stages (`CommandParser` → `Middleware` → `VariantService` → `Cache` / `Processor`) across Pinveria's inward-facing layers, leaving `Pinveria.Domain` unchanged:

```text
Pinveria.Api/Media/
  ImageCommandParser.cs            // Pure HTTP query (?width, ?format) + Accept parser
  ImageOptimizationMiddleware.cs   // Thin GET/HEAD middleware ahead of UseStaticFiles
  MediaFileMiddlewareExtensions.cs // Wires middleware inside app.Map("/media/files", ...)

Pinveria.Application/Media/Optimization/
  ImageTransformCommand.cs         // Validated immutable command (AssetId, Width?, OutputFormat, VaryByAccept)
  ImageOutputFormat.cs             // Original, Avif, Webp, Jpeg, Png
  ImageVariantResult.cs            // Discriminated outcome: Variant | Passthrough | BusyFallback | NotFound
  ImageOptimizationOptions.cs      // AllowedWidths, encoder quality/effort, MaxConcurrency, QueueTimeoutSeconds
  ImageCacheOptions.cs             // CacheRoot, MaxBytes, EvictFraction, MaxIdleDays, TouchInterval, SweepInterval
  IImageVariantService.cs          // Application orchestration port
  IImageProcessor.cs               // Image transform port (hides NetVips)
  IImageVariantCache.cs            // Cache read/write/invalidation/sweep port

Pinveria.Infrastructure/Media/Optimization/
  MediaSignatureDetector.cs        // Shared header-byte detector reused by MediaFileContentTypeProvider
  VipsImageProcessor.cs            // NetVips implementation + SemaphoreSlim concurrency limiter
  FileSystemImageVariantCache.cs   // Atomic disk cache + throttled mtime touch + TTL & 15% LRU sweep
  ImageVariantService.cs           // Singleflight deduplication + SVG/video passthrough + size-bloat guard
  ImageCacheSweepService.cs        // BackgroundService strictly scoped to CacheRoot
  ImageOptimizationOptionsValidation.cs
```

**Rationale:**
- Follows `docs/architecture.md` and `docs/backend.md`: domain entities remain pure, Application owns ports/value objects/options (matching `IMediaStorage`, `IMediaInspector`, `IMapImageProcessor`), Infrastructure owns `NetVips` and filesystem mechanics, and Api owns HTTP parsing, headers, and `304` handling.
- Placing `ImageOptimizationMiddleware` directly ahead of `UseStaticFiles` inside `UseMediaFiles()` means bare `/media/files/{id}`, SVG assets, and videos fall through seamlessly to existing static-file delivery (`Range`, `ETag`, `CSP: sandbox`, `nosniff`) with zero duplicated file-serving code.

### 3. Orthogonal query contract (`width` and `format`) and `Accept` negotiation

**Decision:** Use explicit `ImageSharp.Web`-style query parameters `width` and `format` on `/media/files/{id}`:
- `width` allowlist (default): `160, 320, 480, 640, 960, 1280, 1600, 1920`. Unknown, non-integer, zero/negative, or duplicate values return HTTP `400`.
- `format` allowlist (case-insensitive): `auto`, `avif`, `webp`, `jpeg`, `png`. Unknown, empty, or duplicate values return HTTP `400`.
- When both are omitted: pass through to `UseStaticFiles` immediately (no `Vary: Accept`).
- When `format` is `auto`: negotiate `Accept` header (`image/avif` → `Avif`, else `image/webp` → `Webp`, else `Original`—using PNG/JPEG fallback preserving alpha if resized) and emit `Vary: Accept`.
- When `format` is explicit (`avif`, `webp`, `jpeg`, `png`): encode in that format and omit `Vary: Accept`.
- When `width` is supplied without `format`: resize in the source image's own format and omit `Vary: Accept`.
- **No-upscale key folding:** If `width >= sourceWidth`, clamp to intrinsic dimensions and key the cache entry under `orig.{ext}` rather than `{width}.{ext}` so requests for `640`, `960`, and `1280` on a `400px` source share one cached file.
- **Format-only bloat guard:** When `width` is omitted and the encoded output is `>= original.Length`, return the original bytes instead of bloating the payload and record a zero-byte sentinel (`orig.{ext}.pass`) in the cache so subsequent requests skip re-encoding.

**Rationale:** Orthogonal parameters prevent unexpected format mutations on raw original endpoints (e.g., Hono OG PNG generation via `satori`/`resvg`, video players, direct downloads), keep `Vary: Accept` strictly scoped to content-negotiated responses, and eliminate cache-busting DoS without URL signing.

### 4. Resource safety on `1 vCore / 2 GB RAM`

**Decision:**
- Decode and downscale with `NetVips.Image.Thumbnail(sourcePath, targetWidth, height: 10_000_000, size: Enums.Size.Down, failOn: Enums.FailOn.Error)` (and `Image.NewFromFile(sourcePath, access: Enums.Access.Sequential)` when format-only without resize). `Thumbnail` activates shrink-on-load for JPEG/WebP and sequential streaming for PNG, keeping RAM peaks small even on `16384x16384px` map originals.
- Preserve sRGB colorspace and strip all metadata (`keep: Enums.ForeignKeep.None`). Flatten alpha onto opaque background only if an explicit `jpeg` format is requested on an image with alpha; for `format=auto` fallback on transparent images, emit `png`.
- Gate `NetVips` work behind `SemaphoreSlim(MaxConcurrency)` (default `1` on 1 vCore) with `QueueTimeoutSeconds` (default `8`). On timeout, return `ImageVariantResult.BusyFallback` so middleware falls through to `UseStaticFiles` with `Cache-Control: no-store` (never poisoning browser caches with an unoptimized fallback).
- Deduplicate concurrent misses on the same canonical `(AssetId, WidthOrOrig, ResolvedFormat)` key with an in-memory singleflight `ConcurrentDictionary<VariantKey, Lazy<Task<...>>>` (removing the entry in `finally` once settled).
- Default encoder tuning for `1 vCore`: AVIF `q = 50, effort = 4`; WebP `q = 80, effort = 4`; JPEG `q = 82`.
- Verify `libvips` codecs explicitly at startup (reusing `IMapImageProcessor.CheckCapabilities()` or shared startup check).

### 5. Smart local filesystem cache (`media_cache` volume)

**Decision:**
- Mount a dedicated Docker volume `media_cache` at `/var/lib/pinveria/media-cache` (`Media__ImageCache__CacheRoot`), isolated from `media_storage` so backups of originals never include regenerable derivatives.
- Reject symlinks/reparse points under `CacheRoot` (same safety check as `LocalMediaStorage`).
- Layout: `{CacheRoot}/{assetId:N}/{widthOrOrig}.{ext}` (and optional `{widthOrOrig}.{ext}.pass` bloat sentinel).
- Atomic writes: write to `{target}.{guid:N}.tmp`, flush, and `File.Move(tmp, target, overwrite: true)`. On startup, `ImageCacheSweepService` deletes any stale `*.tmp` files inside `CacheRoot` only.
- **Linux `relatime`/`noatime`-safe LRU tracking:** Cached variants are immutable, so `ETag` is derived deterministically from `"{assetId:N}-{widthOrOrig}-{ext}-{byteLength:x}"` (not `mtime`). On cache hits, if `utcNow - File.GetLastWriteTimeUtc(path) >= TouchInterval` (default `1 hour`), update `LastWriteTimeUtc` to `utcNow`. This survives container restarts with minimal NVMe write amplification.
- **Two-stage sweep (`SweepAsync`):**
  1. Scan `CacheRoot` and delete files where `utcNow - lastWriteTimeUtc > MaxIdleDays` (default `30` days).
  2. If remaining total bytes `> MaxBytes` (default `2 GiB`), sort remaining files by `lastWriteTimeUtc` ascending and delete oldest entries until remaining bytes `<= MaxBytes * (1 - EvictFraction)` (`EvictFraction = 0.15` → target `85%`).
  3. Delete empty `{assetId:N}` directories.
- **Triggers:** Periodic timer (`SweepIntervalMinutes`, default `30`) plus a coalesced async signal (`Channel.CreateBounded<bool>(1)` with `DropWrite`) fired whenever the in-memory tracked cache byte counter crosses `MaxBytes`, keeping eviction completely off the HTTP request path.
- **Asset deletion hook:** `DeleteMediaAssetHandler` calls `imageCache.InvalidateAsset(command.Id)` (best-effort; IO errors are logged and do not fail asset deletion).

### 6. Shared Angular image optimization integration

**Decision:**
- In `libs/shared/util` (`type:util, scope:shared, platform:shared`): export `MEDIA_IMAGE_BREAKPOINTS = [160, 320, 480, 640, 960, 1280, 1600, 1920]`, `MediaImageFormat = 'auto' | 'avif' | 'webp' | 'jpeg' | 'png'`, `snapMediaImageWidth(width)`, and `buildOptimizedMediaUrl(src, options?)`.
- In `libs/shared/ui` (`type:ui, scope:shared, platform:shared`): export `provideMediaImageOptimization()`, registering:
  - `IMAGE_CONFIG` with `breakpoints: [...MEDIA_IMAGE_BREAKPOINTS]`.
  - `IMAGE_LOADER`: only transforms URLs starting with `/media/files/` (leaving static assets like `/hero-atlas.webp` and `data:` URIs untouched); resolves `format` from `config.loaderParams?.['format'] ?? 'auto'`; appends `width` (snapped to the nearest breakpoint `>= requested`, capped at `1920`) when `config.width` is provided.
- Register `provideMediaImageOptimization()` in both `apps/web/src/app/app.config.ts` and `apps/admin/src/app/app.config.ts`. Because the default URL carries `format=auto` rather than browser-sniffed JS formats, SSR HTML is deterministic and hydration-safe across all browsers.
- Add optional `sizes` and `format` inputs to `MediaThumbnail` (`ui-media-thumbnail`) and `ImagePreview` (`ui-image-preview`), wire `[sizes]` and `[loaderParams]` onto `<img [ngSrc]>`, and pass accurate `sizes` hints from feature templates (game cards, banners, media library grid/details, pickers, map previews).
- Upgrade remaining raw media `<img>` elements in `libs/maps/feature-public-map` (`category-filter-panel.html`, `marker-popup.html`) to `NgOptimizedImage`.
- In `libs/maps/ui` (`CategoryIcon` and `map-markers.ts`), pass `/media/files/...` icon URLs through `buildOptimizedMediaUrl(url, { width: 160, format: 'auto' })`; raster icons become tiny AVIF/WebP downloads while SVG icons pass straight through with their original vector bytes.

## Risks / Trade-offs

- **[Cold-cache AVIF encode latency on 1 vCore]** → Mitigated with AVIF `effort = 4`, `SemaphoreSlim(1)` preventing CPU starvation, singleflight deduplication, shrink-on-load `Image.Thumbnail`, and `QueueTimeoutSeconds` graceful fallback (`Cache-Control: no-store`).
- **[Concurrent map tile generation competing with image optimization on 1 vCore]** → Both run on the single vCore; `SemaphoreSlim(1)` and `QueueTimeoutSeconds` ensure HTTP image requests never hang behind long map tiling jobs and degrade cleanly to originals until the cache warms.
- **[Disk wear or stale `atime` on Linux VDS]** → Throttled `LastWriteTimeUtc` touch (≥1 hour interval) works regardless of `noatime`/`relatime` mount options while avoiding per-request disk writes.
- **[Cache byte counter drift across restarts or manual disk edits]** → Startup sweep initializes the tracked byte total directly from disk, and every periodic sweep recomputes exact disk usage as the source of truth.
- **[Existing Angular `ui-media-thumbnail` unit test expectations]** → Updating `provideMediaImageOptimization()` is app-level (`apps/web`, `apps/admin`), while shared library tests using default `NgOptimizedImage` remain intact; any test explicitly asserting URLs will be checked during implementation.

## Migration Plan

- **No database migration required.**
- Add `media_cache` volume and `Media__ImageCache__CacheRoot: /var/lib/pinveria/media-cache` to `compose.yml` (local dev defaults to `{Media:StorageRoot}/cache` or a sibling path when `CacheRoot` is omitted).
- Deploy normally via existing CI/CD workflow. Cold cache warms lazily on first image requests.
- **Rollback:** Reverting the deployment restores direct original delivery immediately; the `media_cache` volume is purely disposable and can be left or pruned safely without affecting `media_storage` or PostgreSQL.
