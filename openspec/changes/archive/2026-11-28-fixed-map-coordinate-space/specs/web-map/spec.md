## MODIFIED Requirements

### Requirement: Resolve and render public map page

The web application SHALL route `/games/:slug/maps/:mapSlug` to a dedicated public map component. It SHALL query published map details via `PublicMapsQueryOptions` (generated from the `Public Maps` OpenAPI tag) and display the map using `MapViewport` below the persistent global header, passing the map identity and ready styles (each forwarding its own `tileGeometry` including `minZoom` and `maxZoom`) without gating on nullable map-level geometry. HTTP 404 responses SHALL render a map-specific unavailable state (distinct from game-unavailable and default 404 copy) without a retry action, returning HTTP 404 and `noindex, follow` on SSR, while transient errors SHALL render a retryable error state.

#### Scenario: Successfully loading a published map

- **WHEN** a visitor navigates to `/games/:slug/maps/:mapSlug` for an existing published game and map
- **THEN** the application SHALL fetch map details, initialize `MapViewport` with the map identity and available ready styles using each style's own `tileGeometry`, and render published markers.

#### Scenario: Map or game not found

- **WHEN** a visitor or crawler navigates to an invalid slug or a draft/unpublished map and the backend returns HTTP 404
- **THEN** the SSR server responds with HTTP 404 and `noindex, follow`
- **AND** the page displays a map-specific unavailable state with a navigation link back to the game (or to the catalog if the game itself is unavailable) and no retry button

#### Scenario: Transient error while loading map

- **WHEN** loading map details fails with a non-404 network or server error
- **THEN** the page displays a friendly error state with both retry and back-to-game actions
