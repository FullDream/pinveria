# media-image-optimization Specification

## Purpose

Deliver on-demand resized and format-optimized raster images over stable media URLs with bounded local caching, safe passthrough for non-raster media, and responsive frontend integration.

## Requirements

### Requirement: Optimize raster media through orthogonal query parameters

`GET` and `HEAD /media/files/{id}` SHALL accept optional `width` and `format` query parameters for raster assets without changing the canonical URL. Omitting both SHALL serve the unmodified original. `width` SHALL match the configured allowlist (`160, 320, 480, 640, 960, 1280, 1600, 1920` by default) and `format` SHALL match `auto`, `avif`, `webp`, `jpeg`, or `png` (case-insensitive). Invalid, duplicate, or disallowed values SHALL return HTTP `400`.

#### Scenario: Serve a bare URL as an unmodified original

- **WHEN** a client requests `GET /media/files/{id}` without `width` or `format` query parameters
- **THEN** the response delivers the original committed bytes with the original `Content-Type` and no `Vary: Accept` header

#### Scenario: Resize while preserving original format

- **WHEN** a client requests `GET /media/files/{id}?width=640` for a 1600x900 PNG image without supplying `format`
- **THEN** the response returns a 640px-wide aspect-ratio-preserving PNG without `Vary: Accept`

#### Scenario: Reject disallowed width or format values

- **WHEN** a client requests `GET /media/files/{id}?width=500`, `GET /media/files/{id}?width=0`, `GET /media/files/{id}?format=gif`, or duplicate `width` parameters
- **THEN** the response returns HTTP `400` without reading or encoding the source image

#### Scenario: Reject non-GET and non-HEAD optimization requests

- **WHEN** a client sends an unsupported HTTP method to `/media/files/{id}?width=640`
- **THEN** image optimization does not execute

### Requirement: Negotiate and deliver optimal image formats

When `format=auto` is requested for a raster image, delivery SHALL select the best output format supported by `Accept` in priority order `image/avif`, `image/webp`, then a compatible fallback, and SHALL emit `Vary: Accept`. Explicit `format` values (`avif`, `webp`, `jpeg`, `png`) SHALL encode in that format without `Vary: Accept`. Format-only conversion SHALL NOT return a response larger than or equal to the original file.

#### Scenario: Negotiate AVIF for supporting browsers

- **WHEN** a client requests `GET /media/files/{id}?width=640&format=auto` with `Accept: image/avif,image/webp,image/*,*/*`
- **THEN** the response has `Content-Type: image/avif`, `Vary: Accept`, and width at most 640px

#### Scenario: Fall back to WebP when AVIF is unsupported

- **WHEN** a client requests `GET /media/files/{id}?format=auto` with `Accept: image/webp,image/png,*/*`
- **THEN** the response has `Content-Type: image/webp` and `Vary: Accept`

#### Scenario: Honor explicit format without Accept variation

- **WHEN** a client requests `GET /media/files/{id}?width=320&format=webp` even with `Accept: image/avif,image/webp,*/*`
- **THEN** the response has `Content-Type: image/webp` and omits `Vary: Accept`

#### Scenario: Avoid payload bloat on format-only conversion

- **WHEN** a client requests `GET /media/files/{id}?format=auto` for an image whose transcoded output would be greater than or equal to the original byte length
- **THEN** delivery serves the original bytes with the original `Content-Type` while retaining `Vary: Accept`

### Requirement: Prevent image upscaling and strip metadata

Resized raster variants SHALL preserve aspect ratio, SHALL strip embedded metadata such as EXIF profiles, and SHALL NEVER upscale an image beyond its intrinsic width. Requests whose target `width` is greater than or equal to the source intrinsic width SHALL clamp to the intrinsic dimensions and reuse the unresized variant for that format.

#### Scenario: Clamp requested width above intrinsic dimensions

- **WHEN** a 400x300 JPEG image is requested with `?width=640&format=webp` and `?width=1280&format=webp`
- **THEN** both responses return a 400x300 WebP image without upscaling and reuse the same cached original-width variant

#### Scenario: Preserve transparency when resizing

- **WHEN** a transparent PNG is requested with `?width=320&format=auto` and an `Accept` header supporting neither AVIF nor WebP
- **THEN** the fallback response preserves the alpha channel as PNG rather than flattening transparency

### Requirement: Pass non-raster media through safely

Requests with `width` or `format` targeting an `image/svg+xml` asset or a video asset (`video/mp4`, `video/webm`) SHALL bypass raster optimization and serve the original asset unchanged. SVG responses SHALL preserve `Content-Type: image/svg+xml`, `X-Content-Type-Options: nosniff`, and the restrictive `sandbox` `Content-Security-Policy`, and video responses SHALL preserve byte-range support.

#### Scenario: Bypass optimization for SVG assets

- **WHEN** a client requests `GET /media/files/{svgId}?width=320&format=auto`
- **THEN** the original SVG bytes are returned with `Content-Type: image/svg+xml`, `X-Content-Type-Options: nosniff`, and the restrictive `sandbox` `Content-Security-Policy`

#### Scenario: Bypass optimization for video assets

- **WHEN** a client requests `GET /media/files/{videoId}?width=640&format=auto` with a valid `Range` header
- **THEN** the original video is served with byte-range semantics unchanged

### Requirement: Emit immutable HTTP caching validators and safe fallbacks

Optimized responses SHALL return `Cache-Control: public, max-age=31536000, immutable`, `X-Content-Type-Options: nosniff`, `Content-Length`, and a strong `ETag`, supporting empty-body `HEAD` and conditional `304 Not Modified`. Concurrent misses for the same variant SHALL deduplicate work. When processing capacity times out, delivery SHALL fall back to the original with `Cache-Control: no-store`.

#### Scenario: Validate cached variant with If-None-Match

- **WHEN** a client repeats `GET /media/files/{id}?width=640&format=webp` with an `If-None-Match` matching the variant's `ETag`
- **THEN** the response returns `304 Not Modified` with an empty body and preserves the `ETag` and `Cache-Control` headers

#### Scenario: Serve HEAD for an optimized variant

- **WHEN** a client sends `HEAD /media/files/{id}?width=640&format=webp`
- **THEN** the response returns `200` with accurate `Content-Type`, `Content-Length`, `ETag`, and `Cache-Control` headers and an empty body

#### Scenario: Fall back safely when processing queue times out

- **WHEN** processing concurrency is saturated longer than the configured wait timeout for an uncached variant request
- **THEN** the server serves the original image with `Cache-Control: no-store` instead of caching an unoptimized fallback as immutable or failing with a gateway timeout

### Requirement: Maintain a bounded self-evicting local variant cache

Generated variants SHALL be written atomically into a dedicated local cache directory separate from original media storage, recording recency reliably on `relatime`/`noatime` filesystems. Maintenance SHALL delete entries idle longer than the configured idle threshold (default `30` days), and when total size exceeds the configured limit (default `2 GiB`), SHALL evict least recently used entries down to `15%` below the limit.

#### Scenario: Evict idle variants past the TTL threshold

- **WHEN** cache maintenance runs and a cached variant has not been served for longer than the configured idle day limit
- **THEN** that variant file is deleted and any empty asset cache directory is removed without affecting original media files

#### Scenario: Evict oldest 15 percent when disk quota is exceeded

- **WHEN** total cached variant size exceeds the configured maximum byte limit
- **THEN** maintenance deletes the least recently used variants until total cache size is at or below 85 percent of the configured maximum limit

#### Scenario: Clean incomplete temporary cache files on startup

- **WHEN** temporary cache write files remain after an unexpected process termination
- **THEN** startup cache initialization removes orphaned temporary files inside the cache root without altering original media storage

### Requirement: Integrate responsive optimized image loading in frontend applications

Both `web` and `admin` SHALL configure a shared image loader and breakpoint list matching the backend `width` allowlist. For `/media/files/...` URLs, the loader SHALL default to `format=auto` unless an explicit `format` override is provided through loader options, and SHALL append the snapped `width` when requested. Shared image components SHALL propagate responsive `sizes` hints and leave non-media URLs unchanged.

#### Scenario: Build SSR-safe default image URLs with responsive srcset

- **WHEN** a component renders a `/media/files/{id}` image through the shared thumbnail or preview component without explicit format options
- **THEN** the generated image URLs include `format=auto` and backend-aligned `width` breakpoints across `srcset` without browser-specific SSR markup differences

#### Scenario: Override output format per image

- **WHEN** a consumer supplies an explicit format option such as `webp` to the image loader
- **THEN** the generated `/media/files/{id}` URLs include `format=webp` instead of `format=auto`

#### Scenario: Leave non-media asset URLs unchanged

- **WHEN** a component renders a static bundle asset or data URI through the image loader
- **THEN** the loader returns the URL unmodified without appending `width` or `format` query parameters
