# web-map Spec Delta

## ADDED Requirements

### Requirement: Emit map SEO metadata and semantic heading

The public map page (`/games/:slug/maps/:mapSlug`) SHALL render an accessible visually hidden `<h1>` identifying the map and game, and emit `<Map Name> - <Game Name> | GameAtlas` as its title, `map.description` (or a generated template incorporating the game name and map name when `map.description` is null) as its description, canonical `<origin>/games/:slug/maps/:mapSlug`, the hashed map OG image URL, and `Map` + `BreadcrumbList` JSON-LD.

#### Scenario: Render map with custom description

- **WHEN** a crawler requests `/games/elden-ring/maps/lands-between` and `map.description` is non-null
- **THEN** SSR HTML includes the `sr-only` `<h1>`, `map.description` in `<meta name="description">` and `og:description`, canonical `<origin>/games/elden-ring/maps/lands-between`, `/og/games/elden-ring/maps/lands-between/{hash}.png`, and `Map` + `BreadcrumbList` JSON-LD

#### Scenario: Render map with null description

- **WHEN** a crawler requests `/games/elden-ring/maps/lands-between` and `map.description` is `null`
- **THEN** `<meta name="description">`, `og:description`, and `Map` JSON-LD use a fallback template incorporating both the game name and map name

## MODIFIED Requirements

### Requirement: Resolve and render public map page

The web application SHALL route `/games/:slug/maps/:mapSlug` to a dedicated public map component. It SHALL query published map details via `PublicMapsQueryOptions` (generated from the `Public Maps` OpenAPI tag) and display the map using `MapViewport` below the persistent global header. HTTP 404 responses SHALL render a map-specific unavailable state (distinct from game-unavailable and default 404 copy) without a retry action, returning HTTP 404 and `noindex, follow` on SSR, while transient errors SHALL render a retryable error state.

#### Scenario: Successfully loading a published map

- **WHEN** a visitor navigates to `/games/:slug/maps/:mapSlug` for an existing published game and map
- **THEN** the application SHALL fetch map details, initialize the map viewport with logical geometry and available styles, and render published markers.

#### Scenario: Map or game not found

- **WHEN** a visitor or crawler navigates to an invalid slug or a draft/unpublished map and the backend returns HTTP 404
- **THEN** the SSR server responds with HTTP 404 and `noindex, follow`
- **AND** the page displays a map-specific unavailable state with a navigation link back to the game (or to the catalog if the game itself is unavailable) and no retry button

#### Scenario: Transient error while loading map

- **WHEN** loading map details fails with a non-404 network or server error
- **THEN** the page displays a friendly error state with both retry and back-to-game actions