## MODIFIED Requirements

### Requirement: Return complete public map data

Map details SHALL include id, gameId, name, slug, nullable description, nullable defaultStyleId, styles, categoryGroups and markers. Map details SHALL NOT include map-level width, height, or logical geometry fields because all maps share the universal fixed `[-100, 100]` logical coordinate space. Groups SHALL contain nested categories; markers SHALL be a flat array referencing categoryId. Empty collections SHALL be arrays. The response SHALL omit publication statuses, operational processing details and administrative timestamps.

#### Scenario: Load all map content in one response

- **WHEN** a visible map contains 2000 visible markers
- **THEN** one successful response includes all markers, categories, and the map's nullable description without pagination, viewport parameters, or map-level dimension/geometry fields
- **AND** media and tiles are represented by URLs rather than embedded bytes

### Requirement: Expose only ready rendering styles

Styles SHALL contain id, name, backgroundColor, tileGeometry and non-null tilesUrlTemplate, in persisted display order. Only ready styles SHALL appear in either visibility mode. defaultStyleId SHALL reference a returned ready style or be null. A map without ready styles SHALL remain readable with styles=[] and defaultStyleId=null.

#### Scenario: Mix processing states

- **WHEN** ready, queued, processing and failed styles belong to a map
- **THEN** only ready styles appear, preserving their relative display order and selected ready default
- **AND** no progress, failure diagnostics or source image metadata appear in style representations
