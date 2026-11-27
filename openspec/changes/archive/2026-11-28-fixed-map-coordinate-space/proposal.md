# Proposal

## Why

Maps currently derive their logical `Width` and `Height` from the first attached style and lock those pixel dimensions permanently—even if every style is later deleted—forcing blank maps into an unusable "awaiting first style" state and stretching any subsequent style with a different aspect ratio into distorted imagery. Replacing per-map pixel dimensions with a universal, centered `[-100, 100]` logical coordinate space (`200 × 200` reference span) and uniform max-side scaling lets every map work immediately on creation, renders both square and rectangular styles without distortion, and removes sticky first-style state.

## What Changes

- **Universal fixed logical coordinate space**: Every map uses the fixed Cartesian plane centered at `(0, 0)` (X right, Y up) with reference span `200` (`[-100, -100, 100, 100]`). Maps are immediately usable for viewing and marker placement upon creation without attaching a style first.
- **Uniform aspect-preserving style placement**: Every style retains its own native-resolution `512 × 512` tile pyramid (`tileGeometry`), centered at `(0, 0)` and scaled uniformly by its longest side (`scale = 200 / max(sourceWidth, sourceHeight)`). Square styles occupy `[-100..100, -100..100]`, while rectangular styles occupy `[-W*scale/2..W*scale/2, -H*scale/2..H*scale/2]` without stretching, cropping, or distortion.
- **BREAKING — Remove map-level dimension and geometry properties**: Remove `Width`, `Height`, and `Geometry` from the `Map` domain entity and remove `width`, `height`, `extent`, `tileOrigin`, `tileSize`, `minZoom`, `maxZoom`, and `resolutions` from `MapDto`, `PublicMapDetailsDto`, and OpenAPI schemas. Regenerate frontend Orval API clients (`maps/data-access`).
- **Database schema and data migration**: Add an explicit EF Core migration that rescales existing persisted marker coordinates (`PositionX`, `PositionY`) on maps with previously initialized `Width`/`Height` by `200.0 / GREATEST(Width, Height)` before dropping `Maps.Width` and `Maps.Height`.
- **Viewer and admin/web updates**:
  - Update `MapViewport` and `MapEngine` to initialize immediately from `mapId` with the fixed `[-100, -100, 100, 100]` world space, uniform style transforms, active-style-aware `fit()`, and camera zoom bounds derived from the fixed span (also fixing `public-map-viewer` to forward each style's own `tileGeometry.minZoom`/`maxZoom`).
  - Remove "awaiting first style", read-only logical dimension displays, and the obsolete stretch-distortion aspect-ratio warning from admin map list, map editor, and style creation dialogs.

### Out of Scope

- Per-style manual alignment overrides (`offsetX`, `offsetY`, custom `scale` for shifted crops or DLC expansions).
- Changes to tile pyramid generation (`512 × 512` lossless WebP), tile URLs (`/map-tiles/{mapId}/{styleId}/{z}/{x}/{y}.webp`), or media library upload limits.
- Marker coordinate bounds restrictions (finite coordinates outside `[-100, 100]` remain valid).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `maps`: Replace first-style-initialized logical dimensions and independent X/Y style stretching with a universal fixed `[-100, 100]` centered coordinate space, uniform max-side style scaling, removal of map-level dimension/geometry fields from `Map` and map HTTP responses, and an explicit marker-rescaling migration.
- `public-maps`: Remove nullable map-level `width`, `height`, and logical geometry fields from `PublicMapDetailsDto` and OpenAPI schemas while preserving per-style `tileGeometry` and ready-style filtering.
- `map-viewer`: Initialize the viewer immediately without waiting for per-map geometry, project every ready style with uniform max-side scaling centered at `(0, 0)` without distortion, derive camera zoom bounds from the fixed `[-100, 100]` space, and fit to the active style's logical footprint (or full space when no ready style exists).
- `admin-maps`: Remove pending-first-style and read-only logical dimension displays from the map list and map editor modal, allow marker placement without waiting for a first style, and remove the obsolete aspect-ratio stretch-distortion warning from the style creation dialog.
- `web-map`: Initialize `MapViewport` directly from map identity and ready styles without gating on nullable map-level geometry, forwarding each style's native `tileGeometry` min/max zoom.

## Impact

- **Backend**: `Pinveria.Domain.Maps.Map`, `Pinveria.Application.Maps` DTOs/mappings (`MapDto`, `PublicMapDetailsDto`, `MapMappingExtensions`, `PublicMapMappingExtensions`), `Pinveria.Infrastructure.Persistence` (`MapConfiguration` + new EF Core migration), and backend unit/integration/contract tests.
- **API & Generated Clients**: Breaking removal of 8 map-level geometry fields from `Map` and `PublicMapDetails` OpenAPI schemas; regenerate `frontend/libs/maps/data-access` via Orval.
- **Frontend**: `frontend/libs/maps/ui` (`MapEngine`, `MapViewport`, `map-viewport-types.ts`), `frontend/libs/maps/feature-list`, `frontend/libs/maps/feature-editor`, and `frontend/libs/maps/feature-public-map`.
- **Documentation**: `src/Pinveria.Api/Maps/README.md`, `src/Pinveria.Infrastructure/Maps/README.md`, `frontend/libs/maps/ui/README.md`, `frontend/libs/maps/feature-list/README.md`.
