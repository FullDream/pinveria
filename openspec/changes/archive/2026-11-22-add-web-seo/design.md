# Design

## Context

See `proposal.md` for motivation and scope. Currently:
- `apps/web` runs Angular 22 with `RenderMode.Server` over a Hono Node server (`apps/web/src/server.ts`) with `trustProxyHeaders: true`.
- `apps/web/src/app/app.config.server.ts` configures `GAMES_BASE_URL` via `provideGamesBaseUrl(...)` but omits `provideMapsBaseUrl(...)`, leaving public map SSR requests targeting `/api`.
- `libs/shared/ui` owns `PageTitleStrategy` (writes directly to `Title` with no reactive signal), `PageLoadingSource` (`<ng-container [uiPageLoading]>`), and `PageHeaderPortal`.
- Public backend endpoints live at `GET /public/games`, `GET /public/games/{gameSlug}`, and `GET /public/games/{gameSlug}/maps/{mapSlug}`; `Map` has no `Description` column, and no lightweight published-content index exists.
- `apps/web/public` contains only `favicon.ico` and `hero-atlas.webp`.

## Goals / Non-Goals

**Goals:**
- Provide a single, idiomatic, signal-based declarative SEO primitive (`uiPageSeo`) usable across all public pages and SSR/CSR navigation without duplicating `<meta>`, `<link>`, or `<script>` tags.
- Ensure search engines and social crawlers receive accurate HTTP statuses (200, 302, 404, 503), absolute URLs, JSON-LD, `/robots.txt`, `/sitemap.xml`, and cache-busted 1200×630 PNG OG images on first SSR response.
- Keep backend endpoints domain-oriented (`GET /public/index`, `Map.Description`) without leaking frontend SEO concepts into `.NET`.

**Non-Goals:**
- Admin UI forms for `Map.Description` or custom per-entity SEO override fields.
- Server-side disk/Redis caching for OG images or `sitemap.xml` (rely on HTTP `Cache-Control` and content-hashed OG URLs for now).
- Prerendering (`RenderMode.Prerender`) or changing the existing `RenderMode.Server` strategy.

## Decisions

### 1. Backend domain endpoint `GET /public/index` instead of `/public/sitemap`

- **Choice**: Add a dedicated read-only vertical slice `GetPublicIndex` exposed at `GET /public/index` under OpenAPI tag `Public Index` returning `{ games: [{ slug, updatedAt, maps: [{ slug, updatedAt }] }] }` (published games and published maps only, ordered by `createdAt` asc then `id` asc). Hono assembles `/sitemap.xml` by calling `GET /public/index`.
- **Rationale**: Keeps XML formatting and public URL routing inside `apps/web` while giving Hono a single lightweight SQL projection with real `updatedAt` timestamps for `<lastmod>`. Placing the route at `/public/index` avoids colliding with `/public/games/{gameSlug}` and keeps backend naming free of web/SEO terms. Using a separate `Public Index` OpenAPI tag avoids pulling server-only indexing operations into browser Orval clients.
- **Alternatives considered**:
  - `GET /public/sitemap` on `.NET` — rejected as leaking frontend SEO concerns into the domain API.
  - Iterating `/public/games` + `/public/games/{slug}` from Hono — rejected due to N+1 HTTP calls and missing `updatedAt`.
  - `GET /public/games/index` — rejected because it sits adjacent to `/public/games/{gameSlug}` and risks slug collisions.

### 2. `Map.Description` mirroring `Game.Description`

- **Choice**: Add nullable `string? Description` on `Map` with an explicit EF Core migration (`AddMapDescription`), accepting optional `description` on `CreateMap` and `Optional<string?> Description = default` on `UpdateMap` (normalizing `""` to `null`, preserving on omission, clearing on `null`/`""`, no-op preserving `UpdatedAt`). Expose `description` in `MapDto` and `PublicMapDetailsDto`, and regenerate `libs/maps/data-access`.
- **Rationale**: Matches existing `Game.Description` domain semantics and `docs/api-conventions.md` merge-patch rules with zero new abstractions. On the public map page, `map.description` is used directly when present, falling back to a generated template inserting the game name and map name when `null`.
- **Alternatives considered**:
  - Always generating map descriptions on the frontend without a backend field — rejected because curated per-map descriptions should be supported by the data model immediately.

### 3. Declarative `<ng-container uiPageSeo>` + reactive `PageTitleStrategy` in `libs/shared/ui`

- **Choice**:
  - Add a reactive `readonly title = signal<string>(...)` (or `asReadonly()`) to `PageTitleStrategy` updated inside `setPageTitle(...)`.
  - Create `libs/shared/ui/src/lib/page-seo/` with:
    - `PAGE_SEO_CONFIG` / `providePageSeo(...)` (site name, default description, default image, locale, and origin resolver: `PUBLIC_SITE_URL` env → `REQUEST` headers/URL → `DOCUMENT.location.origin`).
    - `PageSeo` root/app-scoped service managing head mutations (`Meta`, `<link rel="canonical">`, managed `<script type="application/ld+json" data-page-seo="json-ld">` nodes with `<` escaped as `\u003c`, and optional `RESPONSE_INIT` HTTP status on SSR).
    - `PageSeoDirective` (`selector: 'ng-container[uiPageSeo]'`) with signal inputs (`title`, `description`, `image`, `type`, `canonical`, `noindex`, `nofollow`, `statusCode`, `jsonLd`).
  - Use a registration stack (mirroring `PageLoading` / `PageHeader`) so the active view owns head state and `DestroyRef.onDestroy` cleanly unregisters and restores defaults on navigation.
  - When `[title]` is omitted on the directive, `PageSeo` reactively mirrors `PageTitleStrategy.title()` into `og:title` and `twitter:title`. When `[title]` is provided, the directive calls `PageTitleStrategy.setPageTitle(...)` so components like `GamesFeatureDetail` no longer need manual title effects.
- **Rationale**: Matches existing `ng-container[uiPageLoading]` ergonomics while avoiding the pitfalls of portaling raw `<meta>`/`<script>` DOM from `ng-template` into `<head>` (hydration mismatch, Angular template sanitizer stripping `<script>`).
- **Alternatives considered**:
  - `ng-template` + `CdkPortal` into `<head>` — rejected due to hydration and `<script>` sanitization risks.
  - Imperative `SeoService.set(...)` calls in every component — rejected in favor of declarative template binding that automatically reacts to `@if` error/data branches and cleans up on destroy.

### 4. Nested web routes, temporary redirects, `paramsInheritanceStrategy: 'always'`, and three 404 scopes

- **Choice**:
  - Restructure `apps/web/src/app/app.routes.ts` into nested `children` (`''`, `games` → `''` redirect `/` and `:slug` → `''` detail + `maps` → `''` redirect `..` and `:mapSlug` public map, plus `**` → `NotFound`).
  - Add `withRouterConfig({ paramsInheritanceStrategy: 'always' })` in `app.config.ts` so `:slug` flows into `FeaturePublicMap`, `publicMapGameResolver`, and route title resolvers.
  - Add `provideMapsBaseUrl(process.env['GAME_ATLAS_API_URL'] ?? GAMES_SERVER_URL)` in `app.config.server.ts`.
  - Keep Angular SSR's default HTTP `302` for `/games` and `/games/:slug/maps` redirects so future standalone pages are not blocked by permanent `301` caches.
  - Keep distinct 404 copy in three places: `GamesFeatureDetail` (`Game unavailable`), `FeaturePublicMap` (`Map unavailable`, separated from transient retryable errors), and `apps/web` `NotFound` (`Page not found`), all declaring `uiPageSeo` with `[noindex]="true"` and `[statusCode]="404"`.
- **Rationale**: Prevents soft-404s, ensures SSR renders map pages from the backend origin, and avoids parameter inheritance regressions under nested routes.

### 5. Dynamic OG images with `satori` + `satori-html` + `@resvg/resvg-js` + `sharp` and content-hashed URLs

- **Choice**:
  - Implement Hono OG image handlers in `apps/web/src/server/og/` using `satori` + `satori-html` (HTML/CSS template strings without React/JSX) + `@resvg/resvg-js` (native PNG rendering) + `sharp` (decoding and resizing WebP/large raster backdrops into PNG/JPEG data URIs that Satori supports). Bundle a local sans-serif font (e.g., Inter Regular/Bold `.ttf`/`.woff`) for Satori text rendering.
  - Mark `@resvg/resvg-js` and `sharp` in `apps/web/project.json` `externalDependencies` so `@angular/build:application` esbuild leaves native Node bindings external.
  - Use unambiguous path segments for hashed OG URLs:
    - `/og/home/{hash}.png`
    - `/og/games/{slug}/{hash}.png`
    - `/og/games/{slug}/maps/{mapSlug}/{hash}.png`
  - Place a pure synchronous TypeScript hash + OG URL builder in `libs/shared/util` (using an `OG_TEMPLATE_VERSION` constant plus the visual inputs of each card, outputting an 8–12 char lowercase hex digest) so Angular SSR/browser templates and Hono handlers compute identical hashes without `node:crypto`.
  - Hono behavior on OG requests:
    - Malformed hash or missing/unpublished resource → HTTP `404` before rendering.
    - Matching hash → render 1200×630 PNG with `Cache-Control: public, max-age=31536000, immutable`.
    - Valid hash format that does not match current data → HTTP `302` redirect to the current hashed URL with `Cache-Control: no-store`.
- **Rationale**: `@vercel/og` targets Edge/Next.js with WASM loaders and React JSX, which frequently breaks under Angular's esbuild server bundler and cannot decode WebP inputs (`hero-atlas.webp` and uploaded media). `satori` + `satori-html` + `@resvg/resvg-js` + `sharp` runs natively in Node/Hono, supports WebP assets cleanly, and guarantees deterministic cache busting across Telegram/Discord/X via hashed URLs.
- **Alternatives considered**:
  - `@vercel/og` — rejected due to Edge/WASM bundling fragility with `@angular/build:application`, React/JSX coupling, and lack of native WebP support.
  - Suffixing `-hash` directly onto slugs (`/og/games/:slug-:hash.png`) — rejected because slugs contain hyphens and could ambiguously collide with hex suffixes.
  - Strict `404` on stale hashes — rejected because a brief race between HTML crawl and OG fetch right after an admin edit would break the social preview instead of transparently redirecting (`302`).

### 6. Catalog indexing policy and Hono route precedence

- **Choice**:
  - Home catalog (`/`):
    - Clean root (`offset === 0`, no `search`, default `sortBy='name'`, `sortDirection='asc'`, `limit=24`) → `index, follow`, canonical `/`, title `Interactive Game Maps | GameAtlas`.
    - Clean pagination (`offset > 0`, no `search`, default sort/limit) → `index, follow`, self-canonical `/?offset=<offset>`, title `Interactive Game Maps — Page <N> | GameAtlas`.
    - Any `search`, non-default sort, or non-default `limit` → `noindex, follow`, canonical `/`.
  - Register `/robots.txt`, `/sitemap.xml`, and `/og/*` in `apps/web/src/server.ts` **before** the static handler / `angularApp.handle(...)` catch-all so they never enter Angular SSR.

## Risks / Trade-offs

- **[Native dependencies in SSR server bundle]** (`@resvg/resvg-js`, `sharp`) → Declare them in `apps/web/project.json` `externalDependencies` and verify `web:build` and runtime startup in Node; production Docker/host environments must install platform-matching `node_modules` for the target OS/arch.
- **[Unbounded OG rendering CPU under abuse without server cache]** → Validate hash syntax and resource existence before rendering, resize source images via `sharp` before embedding in Satori, and send `immutable` HTTP caching headers so CDNs and crawlers fetch each hash once; an in-memory/LRU cache can be added later without changing URLs or specs.
- **[Upstream API failure during `/sitemap.xml` or `/og/*`]** → Return HTTP `503` for `/sitemap.xml` (never an empty 200 sitemap that could de-index the site) and safe HTTP `502`/`503` (or `404` on upstream 404) for OG routes with `Cache-Control: no-store`.
- **[SSR `RESPONSE_INIT` timing with async TanStack Query]** → Angular SSR awaits pending `HttpClient` requests via zone/pending tasks before flushing HTML; verify that `GamesFeatureDetail` and `FeaturePublicMap` set `statusCode: 404` within the SSR render pass when queries settle with 404.

## Migration Plan

1. Add `Map.Description` and generate an explicit EF Core migration (`AddMapDescription`) without modifying prior migrations; apply via `dotnet ef database update`.
2. Add `GET /public/index` and update OpenAPI + regenerate `libs/maps/data-access` (remembering existing backend contracts remain backward-compatible; `description` is optional on requests and nullable on responses).
3. Deploy `apps/web` with `PUBLIC_SITE_URL` configured in production (optional; safely falls back to forwarded proxy headers).
4. Rollback: `apps/web` changes are stateless; rolling back `.NET` leaves the nullable `Description` column harmlessly unused (or reversible via standard EF Core down migration).