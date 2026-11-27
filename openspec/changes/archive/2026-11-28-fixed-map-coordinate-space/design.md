# Design

## Context

See [proposal.md](proposal.md) for motivation. Currently, `Pinveria.Domain.Maps.Map` persists nullable `Width` and `Height` columns initialized on first style attachment (`Width ??= style.Width; Height ??= style.Height`), `MapDto` and `PublicMapDetailsDto` expose 8 nullable map-level geometry properties (`width`, `height`, `extent`, `tileOrigin`, `tileSize`, `minZoom`, `maxZoom`, `resolutions`), and `MapEngine` maps every style onto `Map.Geometry.Extent` with independent `scaleX` and `scaleY` transforms. Each `MapStyle` already builds and exposes its own native-resolution `512 × 512` tile pyramid (`MapTileGeometryDto`) independently of map dimensions.

## Goals / Non-Goals

**Goals:**
- Remove map-level pixel dimensions (`Width`, `Height`, `Geometry`) from `Map`, persistence, and HTTP DTOs so every map immediately shares the fixed centered `[-100, 100]` coordinate space (`span = 200`, X right, Y up).
- Render both square and rectangular styles in `MapEngine` with uniform aspect-preserving max-side scaling centered at `(0, 0)` and zero distortion.
- Migrate persisted marker coordinates (`PositionX`, `PositionY`) cleanly into the new coordinate space before dropping `Maps.Width` and `Maps.Height`.
- Keep `MapGeometry` focused on per-style native tile pyramids (`512 × 512` WebP) without changing tile generation or tile URLs.

**Non-Goals:**
- Adding per-style manual alignment offsets (`offsetX`, `offsetY`, custom `scale`) in this change; the uniform model leaves room to layer optional style-level transforms later without breaking map coordinates.
- Enforcing hard bounds on marker coordinates (finite coordinates outside `[-100, 100]` remain valid).
- Changing `MapTileGeometryDto`, tile file layout, or media library admission bounds (`1..16384`).

## Decisions

### 1. Fixed centered `[-100, 100]` logical space (`LOGICAL_SPAN = 200`)

- **Choice**: Define the universal map logical space as `[-100, -100, 100, 100]` (width = `200`, height = `200`, center `(0, 0)`, X right, Y up).
- **Rationale**: `[-100, 100]` reads directly as percentages from center (`0` is center, `±50` is halfway to the edge, `±100` is the edge along the longest axis), making marker coordinates intuitive in admin forms, debugging, and future URLs. Because every map uses the same space, blank maps need no initialization step and deleting all styles leaves no stale state.
- **Alternatives considered**:
  - `[-10, 10]` (`span = 20`): smaller numbers, slightly less intuitive than percentage-from-center.
  - Keeping per-map `Width`/`Height` with a reset on last-style deletion: still leaves blank maps uninitialized and still stretches rectangular styles added to a square map (or vice versa).

### 2. Uniform max-side scaling centered at `(0, 0)`

- **Choice**: For any ready style with source dimensions `srcWidth × srcHeight` (derived from `style.tileGeometry.extent`), `MapEngine` computes:
  - `scale = 200 / Math.max(srcWidth, srcHeight)`
  - `halfW = (srcWidth * scale) / 2`, `halfH = (srcHeight * scale) / 2`
  - `styleExtent = [-halfW, -halfH, halfW, halfH]`
  - Forward transform (source → logical): `[x * scale, y * scale]`; inverse: `[x / scale, y / scale]`
  - `sourceProjection.metersPerUnit = scale`
  - `this.#layer.setExtent(styleExtent)`
- **Rationale**:
  - Preserves exact native aspect ratio for square, landscape, and portrait images alike—no stretching or cropping.
  - Replacing a style with higher/lower resolution of the same aspect ratio (`4000×2000` ↔ `8000×4000`) produces the exact same `styleExtent` (`[-100, -50, 100, 50]`), keeping every marker aligned.
  - Switching between a square style (`4096×4096`) and a cropped rectangular style sharing the same longest side (`4096×2048`) shares the exact same `scale`, keeping markers aligned inside the overlapping region.
  - Clipping the tile layer to `styleExtent` prevents OpenLayers from requesting out-of-bounds tiles outside a rectangular style's footprint while allowing `backgroundColor` to fill the surrounding viewport cleanly.
- **Alternatives considered**:
  - Independent X/Y stretching onto `[-100, -100, 100, 100]`: distorts any non-square map unless authors manually pad images with margins.
  - Fitting by width only: tall portrait maps would exceed `[-100, 100]` vertically and behave inconsistently with landscape maps.

### 3. OpenLayers camera bounds and `fit()` behavior

- **Choice**:
  - `View` uses `maxResolution = 200 / 256` (so at minimum zoom the 200-unit span fits comfortably inside 256px) and `minResolution = 200 / (16384 * 4)` (allowing 400% pixel zoom even on maximum-dimension `16384px` styles).
  - `MapEngine` tracks `#fitExtent`, set to the active style's `styleExtent` when a ready style is attached or `[-100, -100, 100, 100]` when no style is ready.
  - `fit()` fits the view to `#fitExtent` with `[24, 24, 24, 24]` padding. `MapViewport` calls `setStyle(this.activeStyle())` right after instantiating `MapEngine`, before the `ResizeObserver` triggers the initial `fit()`, so initial framing automatically matches a rectangular style without excessive top/bottom margin. Switching styles afterward updates `#fitExtent` for future explicit Fit clicks without resetting or moving the current camera.
- **Rationale**: Decouples camera zoom limits completely from per-style tile grids while keeping initial load and explicit Fit tight to rectangular maps.

### 4. Remove map-level geometry fields from `Map`, DTOs, and `MapViewport`

- **Choice**:
  - Remove `Width`, `Height`, and `Geometry` from `Pinveria.Domain.Maps.Map` and `builder.Ignore(map => map.Geometry)` from `MapConfiguration`. Retain `MapGeometry` for `MapStyle` tile pyramid validation and generation (`IMapImageProcessor`, `MapMappingExtensions.ToDto`, `PublicMapMappingExtensions`).
  - Remove `Width`, `Height`, `Extent`, `TileOrigin`, `TileSize`, `MinZoom`, `MaxZoom`, `Resolutions` from `MapDto` and `PublicMapDetailsDto`.
  - Replace `[geometry]="..."` (`MapGeometry` with 7 fields) on `MapViewport` with `[mapId]="..."` (`string`), removing the `MapGeometry` frontend interface and the "awaiting first style" guard in `MapViewport`, `FeatureEditor`, and `PublicMapViewer`.
  - Fix `PublicMapViewer` to pass `style.tileGeometry.minZoom` and `style.tileGeometry.maxZoom` directly instead of reading removed map-level zoom fields.
- **Rationale**: Eliminates 8 redundant nullable wire fields per map and removes dead null-geometry branches across backend and frontend.

### 5. Database schema and marker coordinate migration

- **Choice**: Generate a new EF Core migration (`RemoveMapLogicalDimensions` or similar) that runs a single PostgreSQL `UPDATE` before dropping `Maps.Width` and `Maps.Height`:
  ```sql
  UPDATE "Markers" AS mk
  SET "PositionX" = mk."PositionX" * (CAST(200.0 AS double precision) / CAST(GREATEST(m."Width", m."Height") AS double precision)),
      "PositionY" = mk."PositionY" * (CAST(200.0 AS double precision) / CAST(GREATEST(m."Width", m."Height") AS double precision))
  FROM "Categories" AS c
  INNER JOIN "CategoryGroups" AS cg ON cg."Id" = c."CategoryGroupId"
  INNER JOIN "Maps" AS m ON m."Id" = cg."MapId"
  WHERE mk."CategoryId" = c."Id"
    AND m."Width" IS NOT NULL
    AND m."Height" IS NOT NULL
    AND GREATEST(m."Width", m."Height") > 0;
  ```
  Then drop columns `Width` and `Height` from `"Maps"`.
- **Rationale**: Preserves exact visual placement of every existing marker on both square and rectangular maps (`4096×4096` `(1024, -512)` → `(50, -25)`; `4000×2000` `(1000, 500)` → `(50, 25)`), skips blank maps without division by zero, and integrates automatically with the production `migrator` EF bundle during deployment.

## Risks / Trade-offs

- **One-way data migration on `Down()`** → Once `Maps.Width` and `Maps.Height` are dropped, `Down()` can re-add the nullable columns but cannot automatically invert marker rescaling without stored original pixel dimensions. Mitigation: Take a standard PostgreSQL backup before applying the production migration, and verify the migration in an integration test against a seeded database with square, rectangular, and blank maps.
- **Styles with different longest-side crops on the same map** → If a map previously had two styles with different aspect ratios (e.g., `8000×4000` and `1000×1000`), the old system stretched the square into 2:1 while the new system renders the square as 1:1 (`[-100..100, -100..100]`) and the rectangle as 2:1 (`[-100..100, -50..50]`). Mitigation: Markers aligned to the map's original `Width × Height` aspect ratio stay exact after migration; future optional per-style `scale`/`offset` can handle intentionally shifted/expanded DLC art if ever needed.
- **Breaking API contract change** → Admin and web frontends share the same repository and deploy atomically with the backend via `deploy.yml`; regenerate Orval clients in the same change and update contract tests.

## Migration Plan

1. Update domain entity, DTOs, mappings, and `MapConfiguration`.
2. Generate EF Core migration via `dotnet ef migrations add` and insert the `migrationBuilder.Sql(...)` marker rescaling step before `DropColumn` calls in `Up()`.
3. Update OpenAPI generation and regenerate `frontend/libs/maps/data-access` via Orval.
4. Update `MapEngine`, `MapViewport`, admin map features, and public map viewer.
5. Run backend unit/integration tests (including a dedicated migration test for square, rectangular, and blank maps), frontend Nx `lint` + production builds (`web`, `admin`), and documentation checks.
6. Production deployment runs the updated EF bundle (`migrator` container) before starting the updated `api` and `web`/`caddy` containers.
