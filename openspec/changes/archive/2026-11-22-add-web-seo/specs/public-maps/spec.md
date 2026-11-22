# public-maps Spec Delta

## MODIFIED Requirements

### Requirement: Return complete public map data

Map details SHALL include id, gameId, name, slug, nullable description, nullable width/height and logical geometry, nullable defaultStyleId, styles, categoryGroups and markers. Groups SHALL contain nested categories; markers SHALL be a flat array referencing categoryId. Empty collections SHALL be arrays. The response SHALL omit publication statuses, operational processing details and administrative timestamps.

#### Scenario: Load all map content in one response

- **WHEN** a visible map contains 2000 visible markers
- **THEN** one successful response includes all markers, categories, and the map's nullable description without pagination or viewport parameters
- **AND** media and tiles are represented by URLs rather than embedded bytes