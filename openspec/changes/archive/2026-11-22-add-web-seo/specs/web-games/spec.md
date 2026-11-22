# web-games Spec Delta

## ADDED Requirements

### Requirement: Apply query-aware SEO indexing and canonical rules on the catalog

The home catalog (`/`) SHALL emit `Interactive Game Maps | GameAtlas`, a default description, `WebSite` (`SearchAction`) and `Organization` JSON-LD, and the hashed home OG image. Clean default pagination SHALL emit `index, follow`, a self-canonical `/?offset=<offset>`, and a numbered page title; active search, non-default sort, or non-default page size SHALL emit `noindex, follow` with canonical `/`.

#### Scenario: Load clean home catalog

- **WHEN** a crawler requests `/` without query parameters
- **THEN** the response emits `index, follow`, canonical `<origin>/`, `WebSite` and `Organization` JSON-LD, and `/og/home/{hash}.png` as the Open Graph image

#### Scenario: Load clean second catalog page

- **WHEN** a crawler requests `/?offset=24` with default sort and default page size
- **THEN** the response emits `index, follow`, self-canonical `<origin>/?offset=24`, and title `Interactive Game Maps — Page 2 | GameAtlas`

#### Scenario: Load filtered or re-sorted catalog

- **WHEN** a visitor or crawler requests `/?search=elden` or `/?sortBy=updatedAt&sortDirection=desc`
- **THEN** the response emits `noindex, follow` and canonical `<origin>/`

### Requirement: Emit SEO metadata and HTTP 404 on game details

The game detail page (`/games/:slug`) SHALL emit `<Game Name> | GameAtlas` as its title, the game description (or a generated fallback mentioning the game name and map/marker counts when absent), canonical `<origin>/games/:slug`, the hashed game OG image URL, and `VideoGame` + `BreadcrumbList` JSON-LD. When the game returns HTTP 404, the page SHALL keep its game-specific unavailable copy, emit `noindex, follow`, omit `VideoGame` JSON-LD, and set the SSR response status to HTTP 404.

#### Scenario: Render SEO metadata for a published game

- **WHEN** a crawler requests `/games/elden-ring` for a published game
- **THEN** SSR HTML includes `<title>Elden Ring | GameAtlas</title>`, meta description, canonical `<origin>/games/elden-ring`, `/og/games/elden-ring/{hash}.png`, and `VideoGame` + `BreadcrumbList` JSON-LD

#### Scenario: Request an unavailable or draft game

- **WHEN** a browser or crawler requests `/games/unknown-game` and the backend returns 404
- **THEN** the SSR server responds with HTTP 404, renders the game-specific unavailable state, and emits `noindex, follow`