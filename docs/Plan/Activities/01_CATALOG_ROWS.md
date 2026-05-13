# Catalog Rows — Index

> Authoritative `ActivityDefinition` C# literals for the 86 catalog rows
> in [`00_INDEX.md`](00_INDEX.md). Sharded across five files in
> [`_catalog_rows/`](_catalog_rows/) to support parallel authorship
> (S0.9). The S0.3 worker writing
> `Services/WoWStateManager/Activities/ActivityCatalog.cs` reads all
> five shards in catalog order and emits one `ActivityDefinition`
> literal per row inside a `static class ActivityCatalog`.

## Provenance

- Authored under Phase 0 spec-hardening slot **S0.9** (see
  [`docs/Plan/01_PHASE0_SPEC_HARDENING.md#s09--concrete-catalog-row-authorship`](../01_PHASE0_SPEC_HARDENING.md#s09--concrete-catalog-row-authorship)).
- Each row's `Location` matches the canonical string in
  [`00_INDEX.md`](00_INDEX.md) exactly.
- Each row's `TravelTarget.NamedLocation` matches a key in
  [`Bot/named-locations.json`](../../../Bot/named-locations.json) (86
  entries seeded under slot S0.12).
- Each row's `TaskFamily` is one of the fixed family-head strings in
  [`docs/Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families)
  (R16 in [`QUESTIONS.md`](../QUESTIONS.md)).
- The record shape is defined in
  [`docs/Spec/04_ACTIVITIES.md#activitydefinition`](../../Spec/04_ACTIVITIES.md#activitydefinition).

## Shards

| Shard | File | Rows | Families |
|---|---|---|---|
| 1 | [`_catalog_rows/01_questing_part1.md`](_catalog_rows/01_questing_part1.md) | 16 | Starter questing (6) + zone questing Westfall→Hillsbrad (10) |
| 2 | [`_catalog_rows/02_questing_part2.md`](_catalog_rows/02_questing_part2.md) | 19 | Zone questing Stonetalon→Felwood (13) + Un'Goro→Silithus (6) |
| 3 | [`_catalog_rows/03_dungeons.md`](_catalog_rows/03_dungeons.md) | 21 | All 21 dungeons (Ragefire Chasm → Stratholme Live) |
| 4 | [`_catalog_rows/04_raids_bg_attune.md`](_catalog_rows/04_raids_bg_attune.md) | 15 | Raids (7) + battlegrounds (3) + attunements (5) |
| 5 | [`_catalog_rows/05_misc.md`](_catalog_rows/05_misc.md) | 15 | Professions (4) + economy (2) + reputations (5) + world event (1) + world bosses (3) |
| **Total** | | **86** | matches the 86 rows in [`00_INDEX.md`](00_INDEX.md) |

## How S0.3 consumes this

The S0.3 worker (compiled `ActivityCatalog.cs`) does:

1. Read the shape contract in [`docs/Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md).
2. Add the records under
   `Exports/GameData.Core/Models/Activities/` per the S0.3 procedure.
3. Add `IActivityCatalog` interface under
   `Services/WoWStateManager/Activities/`.
4. Add `static class ActivityCatalog` with `IReadOnlyList<ActivityDefinition> All`
   and `TryGetById(...)`. The body of `All` is the **concatenation of
   the 86 fenced ```csharp blocks** across the 5 shards, in catalog
   order. The author may copy-paste blocks verbatim; the shapes are
   already complete.
5. Register `ActivityCatalog` in `Program.cs` DI as
   `IActivityCatalog` singleton.
6. Add `CatalogVersion = 1` constant.

## Open verification flags (Phase 2)

These rows ship with `⚠ Unverified` markers in the shard files. The
Phase 2 legality validator cross-checks against MaNGOS DB. See
[`QUESTIONS.md`](../QUESTIONS.md) for full context:

- Q-S0.9.3-1 — UBRS Seal of Ascension item triple `[12344, 12342,
  12343]`.
- Q-S0.9.3-2 — Scholomance Skeleton Key gate (catalog ships
  `RequiredItems = []`; Phase 2 may promote to `[13704]`).
- Q-S0.9.5-1 — Cenarion Circle faction id `609` (unverified).
- Q-S0.9.5-2 — Thorium Brotherhood faction id `59` (unverified).
- Q-S0.9.5-3 — Zandalar Tribe faction id `270` (unverified).
- Q-S0.9.4 set (filed inline in shard 4) — Mark of Honor item ids
  (20558/20559/20560), Onyxia OR-set semantics, BWL-on-MC soft prereq,
  BG capability key shape.

None of these block S0.3. The Phase 2 legality validator (slot
S2.x) is the right place to verify against the live DB.

## Style notes

The five shards were authored by separate workers. Minor stylistic
deltas exist (trailing commas, comment density) but the C# is uniform
enough that the S0.3 author can paste-and-tidy without re-deriving
field values. Substantive cleanup (renaming a property, changing an
enum value) should be a coordinated spec PR, not a per-shard ad-hoc
edit.
