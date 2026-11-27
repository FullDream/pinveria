## ADDED Requirements

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

## REMOVED Requirements

### Requirement: Render authoritative pixel geometry

**Reason**: Replaced by `Render fixed logical space with uniform style scaling`, which eliminates per-map pixel geometry initialization and independent X/Y stretching distortion.

**Migration**: Bind `[mapId]` on `MapViewport` instead of `[geometry]`, and remove `MapGeometry` consumer wiring.

## MODIFIED Requirements

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
