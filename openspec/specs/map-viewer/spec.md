# map-viewer Specification

## Purpose

Provide a reusable game-map viewport that renders aligned imagery in the map's original coordinate space for administration and public consumers.

## Requirements

### Requirement: Render fixed logical space with uniform style scaling

The viewer SHALL use the fixed logical extent `[-100, -100, 100, 100]` (span `200`, centered at `(0, 0)`, X right, Y up) for camera and content coordinates, and each ready style's `tileGeometry` for imagery. Each style SHALL project with uniform scale `200 / max(sourceWidth, sourceHeight)` centered at `(0, 0)` onto its aspect-preserving footprint without stretching, cropping, wrapping, or rounding. Camera zoom bounds SHALL derive from the fixed span, and background SHALL show through transparency and outside rectangular styles.

#### Scenario: Render a rectangular style with odd dimensions

- **WHEN** a ready style has odd rectangular source dimensions with transparent edge tiles
- **THEN** uniform max-side scaling preserves its exact aspect ratio centered at `(0, 0)` and edge padding does not enlarge or shift the image footprint

#### Scenario: Preserve alignment across resolutions and aspect ratios

- **WHEN** `8000x8000` and `1000x1000` styles are switched on the same map
- **THEN** both map to `[-100, -100, 100, 100]` and every fixed logical point remains fixed
- **WHEN** a `4000x2000` rectangular style is selected on the same map
- **THEN** it maps to `[-100, -50, 100, 50]` centered at `(0, 0)` without stretching into a square

### Requirement: Select aligned styles without moving the viewport

The viewer SHALL initially select the supplied displayable default style. Only ready styles with a tile URL SHALL be selectable. Switching styles SHALL retain the viewport center and resolution. Changing the server default or display order SHALL NOT replace a surviving local selection. Source resolution differences SHALL NOT reset or clamp the camera on selection. If the selected style disappears, selection SHALL fall back to the current displayable default or no style.

#### Scenario: Switch and remove a selected style

- **WHEN** a user switches between ready styles after panning and zooming
- **THEN** the same location remains visible at the same scale
- **WHEN** the selected style is removed
- **THEN** the next available default is shown without resetting the camera

### Requirement: Control the viewport

The viewer SHALL support pan, zoom in/out, fit-to-map and fullscreen controls with accessible names and keyboard operation. Initial load and explicit fit SHALL fit the active ready style's logical footprint when a ready style is displayed, or the full `[-100, -100, 100, 100]` logical extent when no ready style is active. Resizing SHALL update the rendered viewport. Fullscreen SHALL expand a consumer-supplied containing region when provided, otherwise the viewer itself; its active state SHALL follow that region. Unsupported fullscreen SHALL be unavailable.

#### Scenario: Resize and fit

- **WHEN** the viewport changes size or enters fullscreen
- **THEN** the map fills the available region without a stale canvas size
- **WHEN** the user invokes fit on a rectangular style
- **THEN** the active style's complete logical footprint is visible with padding without excessive empty margin
- **WHEN** the user invokes fit with no ready style
- **THEN** the full `[-100, -100, 100, 100]` logical space is visible

#### Scenario: Expand a consumer workspace

- **WHEN** the consumer supplies a containing region and the user enters fullscreen
- **THEN** that region and its panels appear together while the viewport resizes without resetting selection or camera
- **AND** exit through the control or browser restores the previous workspace with synchronized fullscreen state

### Requirement: Render without a displayable style

The viewer SHALL initialize its interactive logical coordinate space immediately from the map identity even when the map has no styles or no ready styles. Absent or non-ready styles SHALL display a no-ready-style state without requesting unavailable tiles while keeping the logical coordinate space usable. Source previews SHALL NOT substitute for completed tiles.

#### Scenario: Open a processing-only map

- **WHEN** every style on a map is queued, processing, or failed
- **THEN** the viewer retains the `[-100, -100, 100, 100]` coordinate space, shows the no-ready-style state, and requests no unavailable tiles

#### Scenario: Initialize an empty map

- **WHEN** a newly created map with no styles is opened
- **THEN** the viewer initializes the `[-100, -100, 100, 100]` logical space immediately and shows the no-ready-style state until a ready style is loaded

#### Scenario: Remove every style

- **WHEN** a map loses its final style
- **THEN** its coordinate space persists, the no-ready-style state is shown, and the camera does not reset

### Requirement: Support independent browser consumers

The viewer SHALL accept map identity and style presentation data independently of administration routes, per-map pixel geometry payloads, and API clients. It SHALL initialize browser resources only in the browser and release them on removal so navigation and repeated mounting do not retain obsolete maps.

#### Scenario: Mount after server rendering

- **WHEN** a consumer renders the viewer before browser initialization
- **THEN** rendering does not access unavailable browser APIs
- **AND** the interactive viewport initializes in the browser from map identity alone without requiring administration code

### Requirement: Render category-styled markers

The viewer SHALL accept transport-independent marker presentation data with stable identity, name, X/Y position, category icon/color, optional explicit `opacity?: number`, and optional badges and admin draft, pending and failure state metadata. It SHALL render distinguishable selected and pending markers and use a colored pin fallback for missing icons. When an explicit `opacity` is provided on a marker, the viewer SHALL render the marker icon and fallback pin at that opacity (for example, `0.35` for completed markers or `0.5` for pending placements). When `draftStates` is non-empty on a marker, the viewer SHALL draw a colored indicator dot with a contrasting outline in the top-right corner of the marker icon using the most specific draft level, and the marker hover tooltip SHALL display a draft level line. Marker updates and style switches SHALL preserve camera position and resolution.

#### Scenario: Render and update markers

- **WHEN** markers or category appearance change on a map
- **THEN** marker presentation updates without resetting the camera or tile selection
- **AND** missing images show a usable fallback

#### Scenario: Render draft indicator dot and multiline tooltip

- **WHEN** a marker has `draftStates` containing one or more of `'marker'`, `'category'`, `'group'`
- **THEN** the rendered marker icon includes the colored top-right corner dot corresponding to the first (most specific) level (`#F59E0B`, `#0EA5E9`, or `#8B5CF6`)
- **AND** hovering the marker displays its name on the first line and the active draft levels (`Draft: <levels>`) on the second line of the tooltip
- **AND** a marker without `draftStates` renders no corner dot and shows only its single-line name tooltip

#### Scenario: Retain markers without ready tiles

- **WHEN** a map has no styles or no ready style
- **THEN** marker rendering, placement, and movement interactions remain available in the `[-100, -100, 100, 100]` logical coordinate space

#### Scenario: Rendering marker with custom opacity

- **WHEN** a marker is supplied with `opacity: 0.35`
- **THEN** the marker icon on the vector layer SHALL be rendered with 35% opacity.

#### Scenario: Preserving default opacity

- **WHEN** a marker is supplied without an `opacity` property
- **THEN** it SHALL be rendered at full opacity (1.0) unless `pending` is true (0.5).

### Requirement: Expose mode-specific marker interactions

The viewer SHALL expose selection, placement and completed movement with marker identity or logical X/Y coordinates, without issuing persistence requests. Pan SHALL select markers and pan the map; Add SHALL place only on intentional map clicks; Move SHALL drag markers while empty-space dragging pans. Controls and cancelled gestures SHALL NOT emit placement or completed movement.

#### Scenario: Separate interactions

- **WHEN** an operator clicks, pans, drags a marker or uses an overlaid control
- **THEN** only the interaction enabled by the active mode is emitted
- **AND** a movement emits once on release, with no update for unchanged or cancelled gestures

### Requirement: Release marker browser resources

Marker layers, gesture listeners and image resources SHALL follow the viewer browser lifecycle. Changing maps or removing the viewer SHALL release old resources and prevent late interactions from affecting another map. Marker presentation SHALL remain independent of administration routes and API clients.

#### Scenario: Remount and switch maps

- **WHEN** a viewer is removed or its map changes during a gesture
- **THEN** the obsolete gesture cannot emit a completed operation into the new map
- **AND** later mounting creates only the current map's marker interactions

### Requirement: Reveal a marker within an unobscured viewport region

The viewer SHALL support revealing a selected marker within a supplied unobscured viewport region with padding. It SHALL pan only as much as necessary to fit the marker, preserving resolution and marker coordinates. This explicit reveal SHALL be distinct from ordinary marker-data updates, which preserve the camera.

#### Scenario: Reveal around an overlay

- **WHEN** the consumer requests reveal with the visible map region remaining above an overlay
- **THEN** an obscured marker is brought fully into that region with padding at the existing resolution
- **AND** a marker already fitting that region causes no camera movement or marker-movement event

### Requirement: Interact without an activation click

Pointer panning and wheel zoom SHALL work as soon as the initialized viewer receives those gestures, without requiring a preliminary click or browser focus. The viewer SHALL NOT steal focus on initialization or hover. Keyboard controls SHALL remain scoped to normal map focus.

#### Scenario: Zoom and pan before activating the map

- **WHEN** an operator moves the pointer over a newly initialized map and uses the wheel or begins dragging
- **THEN** the first gesture zooms or pans the map without an activation click
- **AND** wheel interaction does not move focus away from another control
