# public-index Specification

## Purpose

Provide an unauthenticated, lightweight index of published games and their published maps with slugs and last-modified timestamps for public indexing, sitemap assembly, and prefetching.

## ADDED Requirements

### Requirement: Expose published games and maps index

Unauthenticated `GET /public/index` SHALL return HTTP 200 with a JSON object containing a `games` array of all published games. Each game entry SHALL include `slug`, `updatedAt`, and a `maps` array containing only published maps owned by that game, where each map entry includes `slug` and `updatedAt`. Draft or deleted games and maps SHALL be excluded.

#### Scenario: Retrieve mixed published and draft content

- **WHEN** the store contains published and draft games, and a published game owns both published and draft maps
- **THEN** `GET /public/index` returns only published games with their published maps, each with its `slug` and UTC `updatedAt`
- **AND** draft games, draft maps, and published maps under draft games are omitted

#### Scenario: Published game with no published maps

- **WHEN** a published game has zero published maps
- **THEN** the game still appears in `games` with an empty `maps` array

#### Scenario: Empty public catalog

- **WHEN** no games are published
- **THEN** `GET /public/index` returns HTTP 200 with `{ "games": [] }`

### Requirement: Order and keep public index reads side-effect free

Games in `GET /public/index` SHALL be ordered deterministically by `createdAt` ascending then `id` ascending, and each game's `maps` array SHALL be ordered by `createdAt` ascending then `id` ascending. Reading the index SHALL NOT mutate persisted data or timestamps.

#### Scenario: Deterministic ordering across reads

- **WHEN** `GET /public/index` is called repeatedly without intervening mutations
- **THEN** games and nested maps appear in the same deterministic order and no timestamps change

### Requirement: Document public index contract in OpenAPI

OpenAPI SHALL expose `GetPublicIndex` under the `Public Index` tag with dedicated public schemas (`PublicIndex`, `PublicIndexGame`, `PublicIndexMap`) without `Dto` suffixes. Unlike general public game/map detail DTOs, public index entries SHALL intentionally expose `updatedAt` as the public last-modified timestamp for indexing and caching while omitting publication status and internal identifiers.

#### Scenario: Inspect OpenAPI schema

- **WHEN** a consumer inspects the OpenAPI document for `GET /public/index`
- **THEN** operation `GetPublicIndex` is listed under tag `Public Index` and documents `slug` and `updatedAt` without exposing `status`, `createdAt`, or database IDs