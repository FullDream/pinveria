# Proposal

## Why

The public `web` application already renders via Hono SSR, but lacks essential search engine optimization and social sharing infrastructure: pages emit no meta descriptions, canonical links, Open Graph/Twitter tags, or structured data; unavailable games, maps, and unknown URLs return HTTP 200 or redirect to `/`; there is no `/robots.txt`, `/sitemap.xml`, or dynamic social preview image generation; and SSR map routes are missing `MAPS_BASE_URL` server configuration. Adding a complete, declarative SEO layer now ensures published games and interactive maps are accurately crawled, indexed, and previewed across search engines and messengers.

## What Changes

- **Backend public index endpoint (`GET /public/index`)**: Add an unauthenticated, read-only endpoint returning published games and their published maps with slugs and last-modified timestamps (`updatedAt`) without leaking web/SEO concepts into backend domain terminology.
- **Backend map description field**: Add an optional nullable `description` property on `Map` with an explicit EF Core migration, supported on `POST /games/{gameId}/maps`, `PATCH /games/{gameId}/maps/{mapId}` (JSON Merge Patch), `MapDto`, and `PublicMapDetailsDto`. Regenerate the `maps` frontend API client.
- **Reusable declarative SEO primitives (`libs/shared/ui`)**:
  - Expose a reactive signal for the active page title on `PageTitleStrategy`.
  - Add `PageSeo` service, `providePageSeo(...)` configuration (resolving site origin from `PUBLIC_SITE_URL`, SSR `REQUEST`, or browser location), and `<ng-container uiPageSeo ... />` directive managing `<title>`, `description`, `canonical`, `robots`, Open Graph, Twitter Cards, safely escaped JSON-LD `<script type="application/ld+json">`, and SSR HTTP response status codes (`RESPONSE_INIT`).
- **Web routing, redirects, and distinct 404 states (`apps/web`, `web-games`, `web-map`)**:
  - Restructure `apps/web` routes with nested `children`, enable `withRouterConfig({ paramsInheritanceStrategy: 'always' })`, redirect `/games` to `/` and `/games/:slug/maps` to `/games/:slug`, and replace the wildcard `redirectTo: ''` with a dedicated default `NotFound` page returning HTTP 404.
  - Provide distinct 404 copy and real HTTP 404 + `noindex` responses across three scopes: unavailable games (`/games/:slug`), unavailable maps (`/games/:slug/maps/:mapSlug`), and default unknown paths (`/**`).
  - Provide `MAPS_BASE_URL` on the SSR server config so `/games/:slug/maps/:mapSlug` resolves map data during SSR.
- **Page-level SEO metadata & semantics**:
  - Home catalog (`/`): common title pattern (`Interactive Game Maps | GameAtlas`, with page number for clean pagination), `WebSite` + `SearchAction` + `Organization` JSON-LD, and query-aware `robots`/`canonical` policy (self-canonical + `index, follow` for clean pagination; `noindex, follow` + canonical `/` for search, non-default sort, or non-default page size).
  - Game detail (`/games/:slug`): game title, description (with generated fallback), `VideoGame` + `BreadcrumbList` JSON-LD, canonical URL, and hashed OG image.
  - Public map (`/games/:slug/maps/:mapSlug`): map + game title, description from `map.description` or a game+map template fallback, `Map` + `BreadcrumbList` JSON-LD, accessible `sr-only` `<h1>`, canonical URL, and hashed OG image.
- **Hono crawling, dynamic OG images, and static web metadata (`apps/web`)**:
  - Serve `GET /robots.txt` and `GET /sitemap.xml` (assembled from `GET /public/index` with `<lastmod>`).
  - Render dynamic 1200×630 PNG Open Graph images on demand via `satori` + `satori-html` + `@resvg/resvg-js` (+ `sharp` for WebP/raster normalization) at content-hashed URLs (`/og/home/{hash}.png`, `/og/games/{slug}/{hash}.png`, `/og/games/{slug}/maps/{mapSlug}/{hash}.png`), with deterministic shared hashing (`OG_TEMPLATE_VERSION` + visual inputs), immutable caching on hash match, `302` redirect on stale valid hash, and `404` on unknown resources or invalid hash formats.
  - Add `manifest.webmanifest`, vector/touch icons (`favicon.svg`, `apple-touch-icon.png`), and `<link>` references in `index.html`.

### Out of Scope

- Custom per-game or per-map SEO override fields in admin (the existing deferred SEO tab in `games/feature-editor` stays unchanged).
- Admin or public visible UI controls/displays for `Map.Description` (the field is backend/API-backed and consumed for map SEO metadata only for now).
- Server-side file/Redis caching of generated OG images (HTTP `Cache-Control` and content-hashed URLs handle downstream caching).
- Frontend unit/E2E test creation (remains deferred under the project's current frontend verification policy).

## Capabilities

### New Capabilities

- `public-index`: Unauthenticated `GET /public/index` read endpoint returning published games and their published maps with slugs and `updatedAt` timestamps for public indexing and sitemap generation.
- `web-seo`: Shared declarative SEO directive/service (`uiPageSeo`, `PageSeo`, reactive `PageTitleStrategy`), page head/social/JSON-LD metadata, Hono `/robots.txt` and `/sitemap.xml` endpoints, content-hashed dynamic OG image rendering, and web manifest/icon metadata.

### Modified Capabilities

- `maps`: Add optional nullable `description` to map creation, merge-patch updates, complete map responses, and explicit database migration.
- `public-maps`: Include nullable `description` in `GET /public/games/{gameSlug}/maps/{mapSlug}` (`PublicMapDetails`).
- `web-shell`: Restructure routes with nested `children`, add `/games` and `/games/:slug/maps` redirects, enable `paramsInheritanceStrategy: 'always'`, configure SSR `MAPS_BASE_URL`, and serve a dedicated default 404 page with HTTP 404 instead of redirecting `**` to `/`.
- `web-games`: Apply catalog query-parameter indexing/canonical rules, page-level SEO metadata, and real HTTP 404 + `noindex` on unavailable games.
- `web-map`: Separate 404 vs transport error UI with distinct copy and HTTP 404 + `noindex`, add an `sr-only` `<h1>`, and emit map SEO metadata using `map.description` with a game+map template fallback.

## Impact

- **Backend (`src/GameAtlas.Domain`, `GameAtlas.Application`, `GameAtlas.Infrastructure`, `GameAtlas.Api`, `tests/`)**: New `Map.Description` property and EF Core migration, updated map create/patch/DTO/public-map mapping, new `GetPublicIndex` slice and `GET /public/index` endpoint, updated backend tests and OpenAPI specification.
- **Frontend shared libraries (`libs/shared/ui`, `libs/shared/util`, `libs/maps/data-access`)**: Reactive `PageTitleStrategy`, new `page-seo` module (`PageSeo`, `PageSeoDirective`, `providePageSeo`), deterministic OG hash/URL helpers in `libs/shared/util`, regenerated `maps` API models/services.
- **Frontend web app & features (`apps/web`, `libs/games/feature-catalog`, `libs/games/feature-detail`, `libs/maps/feature-public-map`)**: Updated `app.routes.ts`, `app.config.ts`, `app.config.server.ts`, `index.html`, `server.ts`, `project.json` (`externalDependencies`), new `NotFound` page, new OG/robots/sitemap Hono handlers, manifest/icons in `apps/web/public`, and new Node runtime packages (`satori`, `satori-html`, `@resvg/resvg-js`, `sharp`, bundled font asset).
- **Documentation (`docs/api-conventions.md`, `docs/frontend/architecture.md`)**: Document `GET /public/index` (including public `updatedAt` scope), `Map.Description`, and the shared SEO primitives.