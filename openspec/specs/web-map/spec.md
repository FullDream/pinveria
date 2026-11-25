# web-map Specification

## Purpose

Provide a public interactive game map page with full-screen map navigation, offline-first client-side category visibility filtering, and marker completion tracking backed by Dexie and IndexedDB.

## Requirements

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

### Requirement: Filter category and group visibility

The map page SHALL provide a category filter panel displaying category groups and their child categories with icons, names, and marker counts. The user SHALL be able to toggle the visibility of individual categories and entire groups.

#### Scenario: Toggling group visibility

- **WHEN** a user unchecks a category group
- **THEN** all markers belonging to all categories within that group SHALL be hidden from the map viewport.

#### Scenario: Bulk visibility actions

- **WHEN** a user clicks "Hide all" or "Show all" in the filter panel
- **THEN** all categories and groups SHALL be updated simultaneously to hidden or visible respectively.

### Requirement: Persist visibility preferences in IndexedDB

The application SHALL persist category and group hidden states in an IndexedDB collection managed by Dexie under the current `mapId`.

#### Scenario: Restoring visibility preferences across reloads

- **WHEN** a user returns to a map where specific categories or groups were previously hidden
- **THEN** the application SHALL load the preferences from IndexedDB without server requests and initialize the map with those categories and groups hidden.

### Requirement: Track marker completion

The user SHALL be able to toggle any marker as completed (found/done). Completed markers SHALL be persisted in an IndexedDB collection managed by Dexie containing `id` (markerId), `categoryId`, `mapId`, `gameId`, and `completedAt`.

#### Scenario: Marking a marker as completed

- **WHEN** a user clicks the completion toggle on an uncompleted marker
- **THEN** the marker ID with its metadata SHALL be saved to the `completedMarkers` collection, and the marker on the map SHALL visually dim to `opacity: 0.35`.

#### Scenario: Unmarking a completed marker

- **WHEN** a user unchecks the completion toggle on a completed marker
- **THEN** the record SHALL be removed from the `completedMarkers` collection, and the marker on the map SHALL return to full opacity.

### Requirement: Toggle hiding completed markers

The filter panel footer SHALL provide a switch to hide completed markers. When enabled, completed markers SHALL be completely excluded from the map rendering.

#### Scenario: Enabling hide completed switch

- **WHEN** the user enables the "Hide completed" switch
- **THEN** all markers present in the `completedMarkers` collection for this map SHALL disappear from the map viewport.

#### Scenario: Disabling hide completed switch

- **WHEN** the user disables the "Hide completed" switch
- **THEN** all completed markers (not otherwise hidden by category filters) SHALL reappear on the map dimmed at `opacity: 0.35`.

### Requirement: Map navigation and style initialization

The map SHALL fill the remaining viewport below the global header. Theme selection SHALL remain in the header. The toolbar SHALL place the filter action before style selection and display concrete tile previews.

#### Scenario: Opening a published map

- **WHEN** ready styles arrive on initial load
- **THEN** the map SHALL use the default ready style or the first ready style when the default is unavailable.

### Requirement: Responsive category panel

Filters SHALL open in a left drawer on desktop and a bottom drawer on mobile. The panel SHALL provide category search, map switching and breadcrumbs. Public map controls SHALL use English labels.

#### Scenario: Navigating from the category panel

- **WHEN** a visitor opens the filter panel
- **THEN** its breadcrumbs SHALL link to the catalog and game details and identify the current map
- **AND** the visitor SHALL be able to select another available map for the game.

### Requirement: Desktop marker inspection

Selecting a marker SHALL open an anchored popover with its category, name, full description when available, media carousel and completion checkbox.

#### Scenario: Inspecting and expanding a marker

- **WHEN** a visitor selects a marker with multiple media items
- **THEN** the visitor SHALL be able to navigate the media and scroll the description within the popover
- **AND** the visitor SHALL be able to toggle completion, expand into a right drawer and return to the popover.

### Requirement: Mobile marker inspection

Selecting a marker on mobile SHALL open a bottom drawer at 50dvh with a completion checkbox in its header and scrollable description and media. The inspector SHALL allow interaction with the visible map.

#### Scenario: Selecting another marker

- **WHEN** a visitor selects another marker while the drawer is open
- **THEN** the drawer SHALL show the newly selected marker
- **AND** the viewport SHALL reveal it above the measured drawer height without changing zoom.

### Requirement: Emit map SEO metadata and semantic heading

The public map page (`/games/:slug/maps/:mapSlug`) SHALL render an accessible visually hidden `<h1>` identifying the map and game, and emit `<Map Name> - <Game Name> | Pinveria` as its title, `map.description` (or a generated template incorporating the game name and map name when `map.description` is null) as its description, canonical `<origin>/games/:slug/maps/:mapSlug`, the hashed map OG image URL, and `Map` + `BreadcrumbList` JSON-LD.

#### Scenario: Render map with custom description

- **WHEN** a crawler requests `/games/elden-ring/maps/lands-between` and `map.description` is non-null
- **THEN** SSR HTML includes the `sr-only` `<h1>`, `map.description` in `<meta name="description">` and `og:description`, canonical `<origin>/games/elden-ring/maps/lands-between`, `/og/games/elden-ring/maps/lands-between/{hash}.png`, and `Map` + `BreadcrumbList` JSON-LD

#### Scenario: Render map with null description

- **WHEN** a crawler requests `/games/elden-ring/maps/lands-between` and `map.description` is `null`
- **THEN** `<meta name="description">`, `og:description`, and `Map` JSON-LD use a fallback template incorporating both the game name and map name
