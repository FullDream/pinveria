# Tasks

## 1. Domain, Application DTOs, and Mappings

- [x] 1.1 Remove `Width`, `Height`, `geometry`, and `Geometry` from `src/Pinveria.Domain/Maps/Map.cs` (and remove `Width ??= style.Width; Height ??= style.Height;` from `AppendPreparedStyle`), keeping `MapGeometry` for per-style tile pyramid validation and generation; update `tests/Pinveria.Domain.Tests/MapTests.cs` and `MapRenameTests.cs` and verify `dotnet test tests/Pinveria.Domain.Tests -c Release` passes.
- [x] 1.2 Remove `Width`, `Height`, `Extent`, `TileOrigin`, `TileSize`, `MinZoom`, `MaxZoom`, and `Resolutions` from `src/Pinveria.Application/Maps/MapDto.cs`, `src/Pinveria.Application/Maps/PublicMapDetailsDto.cs`, `MapMappingExtensions.cs`, and `PublicMapMappingExtensions.cs`, preserving per-style `TileGeometry`; verify `dotnet build src/Pinveria.Api/Pinveria.Api.csproj -c Release` succeeds.
- [x] 1.3 Update `src/Pinveria.Api/Maps/README.md` and `src/Pinveria.Infrastructure/Maps/README.md` to document the fixed centered `[-100, 100]` logical space, uniform max-side style scaling, and removal of map-level dimension/geometry fields; verify `node scripts/check-docs.mjs` passes.

## 2. EF Core Persistence and Marker-Coordinate Migration

- [x] 2.1 Remove `builder.Ignore(map => map.Geometry)` from `src/Pinveria.Infrastructure/Persistence/Configurations/MapConfiguration.cs` and generate a new EF Core migration (`RemoveMapLogicalDimensions`) via `dotnet ef migrations add`; insert the PostgreSQL `UPDATE "Markers"` coordinate-rescaling SQL (`CAST(200.0 AS double precision) / CAST(GREATEST(m."Width", m."Height") AS double precision)` where `Width` and `Height` are non-null and positive) in `Up()` immediately before dropping `"Width"` and `"Height"` from `"Maps"`. Verify `dotnet build src/Pinveria.Infrastructure/Pinveria.Infrastructure.csproj -c Release` succeeds.
- [x] 2.2 Add an integration test verifying the migration against a PostgreSQL test database populated with markers on a `4096x4096` map (`(1024, -512) -> (50, -25)`), a `4000x2000` map (`(1000, 500) -> (50, 25)`), and a null-dimension blank map (coordinates preserved unchanged), confirming `Maps.Width` and `Maps.Height` are dropped; verify the migration test passes with `dotnet test -c Release`.

## 3. Backend Integration Tests and OpenAPI Client Generation

- [x] 3.1 Update backend map and public-read integration/contract tests (`tests/Pinveria.IntegrationTests/Map*.cs`, `MultiMapApiTests.cs`, `PublicReadTests.cs`, `UpdateMapTests.cs`) to assert the updated map/public-map schemas without map-level dimension/geometry fields and confirm style attachment/deletion preserves the fixed coordinate space; verify `dotnet test tests/Pinveria.IntegrationTests -c Release` passes.
- [x] 3.2 Regenerate the frontend Orval map API clients in `frontend/libs/maps/data-access` from the updated backend OpenAPI specification (`Maps` and `Public Maps` tags) and verify no stale map-level dimension/geometry fields remain in generated models.

## 4. Frontend Map Viewer (`maps/ui`)

- [x] 4.1 Update `frontend/libs/maps/ui/src/lib/map-viewport-types.ts` to remove the `MapGeometry` type and export the fixed logical coordinate constants (`LOGICAL_SPAN = 200`, `LOGICAL_EXTENT = [-100, -100, 100, 100]`).
- [x] 4.2 Update `frontend/libs/maps/ui/src/lib/map-engine.ts` to construct the OpenLayers `Projection` and `View` directly from `mapId` and `LOGICAL_EXTENT` with `maxResolution = 200 / 256` and `minResolution = 200 / (16384 * 4)`, project every ready style with uniform max-side scale `scale = 200 / Math.max(srcWidth, srcHeight)` onto `styleExtent = [-srcWidth * scale / 2, -srcHeight * scale / 2, srcWidth * scale / 2, srcHeight * scale / 2]`, clip the tile layer to `styleExtent`, and frame `#fitExtent` (active style footprint or full `LOGICAL_EXTENT`) on initial load and explicit `fit()` without resetting the camera when styles switch.
- [x] 4.3 Update `frontend/libs/maps/ui/src/lib/map-viewport.ts`, `map-viewport.html`, and `frontend/libs/maps/ui/README.md` to accept `mapId = input.required<string>()` instead of `geometry`, initialize `MapEngine` immediately in the browser even without styles, and remove the "Awaiting first style" empty state; verify `npm exec nx -- lint maps-ui` and `node scripts/check-docs.mjs` pass.

## 5. Admin Maps and Public Map Consumers

- [x] 5.1 Update `frontend/libs/maps/feature-list/src/lib/feature-list.html`, `map-editor-dialog/map-editor-dialog.html`, and `frontend/libs/maps/feature-list/README.md` to remove logical dimension readouts and "Awaiting first style" / "Logical dimensions will be set by the first style" text; verify `npm exec nx -- lint maps-feature-list` and `node scripts/check-docs.mjs` pass.
- [x] 5.2 Update `frontend/libs/maps/feature-editor/src/lib/feature-editor.ts`, `feature-editor.html`, `map-styles/style-editor-dialog/style-editor-dialog.ts`, and `style-editor-dialog.html` to bind `[mapId]="current.id"` on `maps-viewport`, allow marker placement without checking `map.extent`, and remove the obsolete `aspectRatioMismatch` stretch-distortion warning and map-level dimension summary; verify `npm exec nx -- lint maps-feature-editor` passes.
- [x] 5.3 Update `frontend/libs/maps/feature-public-map/src/lib/public-map-viewer/public-map-viewer.ts` and `public-map-viewer.html` to bind `[mapId]="map.id"` unconditionally on `maps-viewport` (removing `mapGeometry` null gating) and forward each style's own `style.tileGeometry.minZoom` and `style.tileGeometry.maxZoom`; verify `npm exec nx -- lint maps-feature-public-map` passes.

## 6. Full Verification

- [x] 6.1 Run full backend test suite (`dotnet test Pinveria.slnx -c Release`), frontend lint and production builds (`npm exec nx -- run-many -t lint -p maps-data-access,maps-ui,maps-feature-list,maps-feature-editor,maps-feature-public-map,web,admin` and `npm exec nx -- run-many -t build -p web,admin --configuration=production`), and documentation checks (`node scripts/check-docs.mjs && node --test scripts/check-docs.test.mjs && git diff --check`).

## Workflow follow-up

- Sync delta specs and archive `fixed-map-coordinate-space` after implementation and review are complete.
