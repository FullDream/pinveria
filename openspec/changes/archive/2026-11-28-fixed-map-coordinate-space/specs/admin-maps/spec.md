## MODIFIED Requirements

### Requirement: Browse game maps

The Maps page SHALL display every map of the routed game in server order, showing name, publication status badge (`Published` or `Draft`), preview, style count and previews, createdAt and updatedAt. Missing previews SHALL have neutral fallbacks. No Main badge, pending-first-style dimension label, logical dimension readout, or fabricated category/marker counts SHALL appear.

#### Scenario: Open and reload the list

- **WHEN** an operator opens or reloads /games/{gameId}/maps
- **THEN** that game's full list loads with each map's `Published` or `Draft` status badge, dates and available style previews without map-level logical dimension labels
- **AND** empty, loading and request-failure states are distinct, with creation and error retry actions as applicable

### Requirement: Rename maps in a modal

Each map SHALL offer editing in a modal with editable name, slug, and `Published` switch (`hlm-switch`) fields, without read-only logical dimension or pending-first-style labels. Saving SHALL send only changed mutable values (including `status` when toggled) and display authoritative returned data. An unchanged form SHALL NOT send an empty PATCH.

#### Scenario: Save and reject renames

- **WHEN** an operator saves a valid changed name, slug, or publication status
- **THEN** the modal closes after success and the list shows the returned name, status badge, and updatedAt
- **AND** a rejected save preserves input and explains the error without losing another successful change

### Requirement: Create styles from library images

Add style SHALL collect a name, background color and one committed library image and submit the existing style creation contract. Names SHALL be trimmed to 1..200 characters; colors SHALL accept #RGB or #RRGGBB. Sources SHALL be supported raster images of any dimensions or aspect ratio admitted by the library, rendered centered at `(0, 0)` with uniform scaling and no distortion. The server SHALL remain authoritative for compatibility and conflicts.

#### Scenario: Attach and await processing

- **WHEN** a valid square or rectangular style is created
- **THEN** the returned map updates the list and the creation dialog closes without any stretch-distortion warning
- **AND** the new style follows live server processing updates without fabricated progress
- **AND** the existing displayed style remains selected while it is available

#### Scenario: Reject or cancel creation

- **WHEN** creation is cancelled
- **THEN** no style mutation is sent
- **WHEN** the source is incompatible or the server rejects the request
- **THEN** input is retained and the error is explained
- **AND** pending submission prevents duplicate requests

## REMOVED Requirements

### Requirement: Warn about aspect ratio differences without blocking attachment

**Reason**: Styles now render with uniform max-side scaling centered at `(0, 0)` in the fixed `[-100, 100]` logical space, so neither square nor rectangular images stretch or distort.

**Migration**: Remove the aspect-ratio stretch warning from the style creation dialog; users can attach any supported raster image aspect ratio directly.
