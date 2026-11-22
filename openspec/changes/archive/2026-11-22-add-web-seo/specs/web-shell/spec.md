# web-shell Spec Delta

## ADDED Requirements

### Requirement: Redirect intermediate collection paths without standalone pages

The `web` router SHALL structure game and map routes using nested `children` with `paramsInheritanceStrategy: 'always'` so ancestor parameters such as `:slug` are inherited by child resolvers, titles, and bound component inputs. Direct requests to `/games` SHALL redirect to `/` and direct requests to `/games/:slug/maps` SHALL redirect to `/games/:slug` using temporary redirects (HTTP 302 on SSR).

#### Scenario: Navigate to intermediate games path

- **WHEN** a visitor requests `/games`
- **THEN** the application redirects to `/` (HTTP 302 during SSR)

#### Scenario: Navigate to intermediate game maps path

- **WHEN** a visitor requests `/games/elden-ring/maps`
- **THEN** the application redirects to `/games/elden-ring` (HTTP 302 during SSR)

### Requirement: Serve a dedicated default 404 page for unmatched routes

Unmatched `web` routes (`/**`) SHALL render a dedicated default not-found page with copy distinct from unavailable-game and unavailable-map states, a link back to the catalog (`/`), `<meta name="robots" content="noindex, follow">`, and an actual HTTP 404 status code during SSR instead of redirecting to `/`.

#### Scenario: Request an unknown web path

- **WHEN** a browser or crawler requests `/does-not-exist`
- **THEN** the SSR server responds with HTTP 404, renders the default not-found page with a link to `/`, and emits `noindex` metadata

## MODIFIED Requirements

### Requirement: Transfer per-request TanStack Query state from SSR to browser

Each SSR request SHALL create an isolated `QueryClient` instance rather than sharing a process-global cache. SSR requests SHALL resolve both `GAMES_BASE_URL` and `MAPS_BASE_URL` to the backend origin (`GAME_ATLAS_API_URL` or `GAMES_SERVER_URL`), while browser requests resolve `/api`, dehydrating and rehydrating `QueryClient` via `TransferState`.

#### Scenario: Hydrate initial catalog without duplicate API fetch

- **WHEN** SSR resolves the initial `GET /public/games` query against the backend origin and returns HTML
- **THEN** the hydrated client `QueryClient` reuses the transferred query state within `staleTime` without re-fetching `/api/public/games` on initial hydration
- **AND** concurrent SSR requests remain isolated in separate `QueryClient` instances

#### Scenario: Resolve public map details during SSR

- **WHEN** SSR handles `/games/elden-ring/maps/lands-between`
- **THEN** `PublicMapsQueryOptions` queries the backend origin via `MAPS_BASE_URL` so map metadata and initial state are rendered and transferred before hydration