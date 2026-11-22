# web-seo Specification

## Purpose

Provide declarative page SEO metadata management, server-rendered crawling endpoints, content-hashed dynamic Open Graph social preview images, and web manifest/icon metadata for the public `web` application.

## ADDED Requirements

### Requirement: Synchronize page title reactively with social metadata

The shared UI title strategy SHALL expose the active formatted page title reactively so social metadata (`og:title` and `twitter:title`) stays synchronized automatically whenever the title changes from route configuration or imperative page updates. Page titles in `web` SHALL follow the common `<Page Title> | GameAtlas` pattern.

#### Scenario: Title resolved by route configuration

- **WHEN** a route resolves a page title without overriding `title` on the page SEO declaration
- **THEN** `<title>`, `og:title`, and `twitter:title` all reflect the formatted `<Page Title> | GameAtlas` title

#### Scenario: Title supplied reactively by the page

- **WHEN** a page updates its title after asynchronous data loads
- **THEN** `<title>`, `og:title`, and `twitter:title` update together to the resolved game or map title

### Requirement: Manage head metadata declaratively per view

The shared UI library SHALL provide a declarative, non-DOM-rendering page SEO directive and service managing meta description, canonical link, robots, Open Graph, Twitter Cards, safely escaped JSON-LD scripts, and SSR HTTP status codes. Leaving a view SHALL restore application defaults and remove stale page-only tags.

#### Scenario: Render full head metadata during SSR

- **WHEN** a crawler requests a published game or map page
- **THEN** the server-rendered HTML `<head>` contains `description`, absolute `canonical`, `robots`, Open Graph, Twitter Card, and safely escaped JSON-LD tags matching the loaded resource

#### Scenario: Navigate between pages on the client

- **WHEN** a visitor navigates from a game page with JSON-LD and custom OG image back to the home catalog
- **THEN** previous page-specific JSON-LD and OG metadata are replaced with the home catalog metadata without leaving duplicate `<meta>`, `<link rel="canonical">`, or `<script type="application/ld+json">` tags

#### Scenario: Escape JSON-LD script payloads safely

- **WHEN** a game name or description contains `</script>` or `<` characters
- **THEN** serialized JSON-LD escapes `<` as `\u003c` so the script element cannot terminate prematurely

### Requirement: Resolve canonical origin and normalize URLs

The public `web` application SHALL resolve its canonical site origin from `PUBLIC_SITE_URL` when configured, falling back to the incoming request origin (honoring forwarded proxy headers) on SSR and `document.location.origin` in the browser. All `canonical`, `og:url`, `og:image`, `twitter:image`, and JSON-LD URLs SHALL be emitted as absolute URLs.

#### Scenario: Normalize relative paths with configured public origin

- **WHEN** `PUBLIC_SITE_URL` is set to `https://gameatlas.example` and a page declares canonical `/games/elden-ring` and a relative image path
- **THEN** rendered `canonical`, `og:url`, and `og:image` use `https://gameatlas.example/...` without duplicate slashes

#### Scenario: Fall back to proxy request origin during SSR

- **WHEN** `PUBLIC_SITE_URL` is unset and an SSR request arrives with `X-Forwarded-Proto: https` and `X-Forwarded-Host: maps.example.com`
- **THEN** canonical, sitemap, robots, and OG URLs resolve against `https://maps.example.com`

### Requirement: Emit schema.org JSON-LD structured data per page type

Each public page type SHALL emit valid schema.org JSON-LD structured data: `WebSite` (with `SearchAction`) and `Organization` on the home catalog; `VideoGame` and `BreadcrumbList` on game detail pages; and `Map` and `BreadcrumbList` on public map pages. Error and 404 pages SHALL omit entity JSON-LD.

#### Scenario: Game detail structured data

- **WHEN** a visitor or crawler loads `/games/elden-ring`
- **THEN** the rendered document includes `VideoGame` metadata (name, URL, image, optional description, developer, release date) and a `BreadcrumbList` linking home to the game

#### Scenario: Public map structured data

- **WHEN** a visitor or crawler loads `/games/elden-ring/maps/lands-between`
- **THEN** the rendered document includes `Map` metadata (name, description, URL, image) and a `BreadcrumbList` linking home, the game, and the map

### Requirement: Serve dynamic robots.txt and sitemap.xml from Hono

The `web` SSR server SHALL serve `GET /robots.txt` and `GET /sitemap.xml`. `robots.txt` SHALL allow crawling public routes, disallow filtered search query URLs, and advertise the absolute `Sitemap:` URL. `sitemap.xml` SHALL fetch `GET /public/index` from the backend API and emit valid XML URL entries for `/`, every published `/games/:slug`, and every published `/games/:slug/maps/:mapSlug` with `<lastmod>` timestamps derived from `updatedAt`.

#### Scenario: Request robots.txt

- **WHEN** a crawler requests `GET /robots.txt`
- **THEN** the server responds with HTTP 200, `text/plain`, crawl rules, and `Sitemap: <origin>/sitemap.xml`

#### Scenario: Request sitemap.xml with published games and maps

- **WHEN** a crawler requests `GET /sitemap.xml` while the backend has published games and maps
- **THEN** the server responds with HTTP 200, `application/xml`, and `<url>` elements for `/`, each published game, and each published map with ISO 8601 `<lastmod>` values

#### Scenario: Backend unavailable during sitemap generation

- **WHEN** `GET /public/index` fails during a `GET /sitemap.xml` request
- **THEN** the server responds with HTTP 503 rather than emitting a broken or cached empty sitemap

### Requirement: Render content-hashed dynamic Open Graph PNG images

The `web` SSR server SHALL render 1200×630 PNG Open Graph preview images on demand for the home page (`/og/home/{hash}.png`), published games (`/og/games/{slug}/{hash}.png`), and published maps (`/og/games/{slug}/maps/{mapSlug}/{hash}.png`). The `{hash}` segment SHALL be deterministically derived from the OG template version and the card's visual inputs so changes invalidate social platform caches automatically.

#### Scenario: Fetch OG image with matching content hash

- **WHEN** a social crawler requests `/og/games/elden-ring/{hash}.png` and `{hash}` matches the current game data and template version
- **THEN** the server renders a 1200×630 `image/png` preview featuring the game banner/cover, title, and counts with `Cache-Control: public, max-age=31536000, immutable`

#### Scenario: Fetch OG image with stale valid hash

- **WHEN** a crawler requests `/og/games/elden-ring/{oldHash}.png` where `{oldHash}` has valid hash syntax for an existing published game whose data changed
- **THEN** the server responds with HTTP 302 redirecting to `/og/games/elden-ring/{currentHash}.png` with `Cache-Control: no-store`

#### Scenario: Fetch OG image for missing resource or invalid hash format

- **WHEN** a request targets a nonexistent or unpublished game/map slug, or supplies a malformed hash segment
- **THEN** the server responds with HTTP 404 without rendering an image

#### Scenario: Render home and map OG previews

- **WHEN** a crawler requests `/og/home/{hash}.png` or `/og/games/{slug}/maps/{mapSlug}/{hash}.png` with a matching hash
- **THEN** the server returns a 1200×630 `image/png` styled after the home hero or the map preview with game/map labels and counts

### Requirement: Provide web app manifest and icon links

The `web` application SHALL serve `/manifest.webmanifest`, a vector favicon (`favicon.svg`), and an Apple touch icon (`apple-touch-icon.png`), and link them from `index.html` alongside `favicon.ico`.

#### Scenario: Inspect document head and manifest

- **WHEN** a browser loads `/`
- **THEN** `index.html` links `manifest.webmanifest`, `favicon.svg`, `favicon.ico`, and `apple-touch-icon.png`, and `GET /manifest.webmanifest` returns valid web manifest JSON for GameAtlas