---
name: activity-catalog-bootstrap
description: Add or generate compiled activity-catalog rows (ActivityDefinition) from the leveling-guide source and keep them passing the catalog invariant tests + MaNGOS cross-validation. Use when adding an Activity to the catalog.
trigger: add an activity, activity catalog, ActivityDefinition row, compiled catalog, leveling guide activity, catalog invariant, TaskFamily
---

# Activity Catalog Bootstrap

## Goal

Add a well-formed `ActivityDefinition` row to the compiled activity catalog (id,
family, location, level range, role template, entry requirements, task family,
join/selection policies) so it passes the catalog invariant tests and validates
against MaNGOS.

## Inputs

- The Activity: family, location, level range, roles, entry requirements, task
  family, faction/join policies.
- Key files (verified):
  - Compiled catalog: `Services/WoWStateManager/Activities/ActivityCatalog.cs`
    (`BuildAll()`), row literals in
    `Services/WoWStateManager/Activities/ActivityCatalogRows.Shard1.cs` …
    `Shard5.cs`, interface `IActivityCatalog.cs`.
  - UI mirror: `UI/WoWStateManagerUI/Services/ActivityCatalogService.cs`.
  - Source rows (leveling guide): `docs/Plan/Activities/_catalog_rows/*.md`;
    status board `docs/Plan/Activities/00_INDEX.md`.
  - Invariant tests: `Tests/BotRunner.Tests/Activities/ActivityCatalogTests.cs`.
- Spec: `docs/Spec/04_ACTIVITIES.md` (the `ActivityDefinition` POCO + legality
  rules), `docs/architecture/aota/` (composition).
- Area rules: `.github/instructions/services.instructions.md`,
  `.github/instructions/config.instructions.md`.

## Preconditions

- The Activity's `Location` resolves to a named location and its `TaskFamily` is
  one of the fixed family heads (Travel, Combat, Questing, Dungeoneering, Raid, Bg,
  Gathering, Crafting, Economy, Social, Recovery, Equipment, WorldEvent, Loadout).
- `.\scripts\test-fast.ps1` is green before you start.

## Procedure

1. Capture the Activity in the leveling-guide source
   `docs/Plan/Activities/_catalog_rows/*.md`.
2. Add an `ActivityDefinition` literal in the appropriate
   `ActivityCatalogRows.Shard<N>.cs` with all fields (`Id`, `Family`, `Activity`,
   `Location`, `LevelRange`, `RoleTemplate`, `EntryRequirements`, `TaskFamily`,
   join/selection policies, rewards, faction policy, min/max players).
3. Include the row in `ActivityCatalog.BuildAll()`.
4. Ensure `Location` resolves (named-locations resolver) and `TaskFamily` is a
   valid head.
5. Update `docs/Plan/Activities/00_INDEX.md` with the matching row (same id /
   location / level range / faction).
6. Run the invariant tests.

## Verification

- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~ActivityCatalog"`
  — uniqueness, location resolution, level bounds, role-template sums, task-family
  validity, and markdown-drift checks must pass.
- `.\scripts\test-fast.ps1`.
- (Phase 2+) live legality tests cross-validate against MaNGOS
  `quest_template`/`item_template`/`faction_template`.

## Outputs

- New `ActivityDefinition` row (`Shard<N>.cs` + `BuildAll()`), source markdown,
  and `00_INDEX.md` entry.

## Failure modes and recovery

- **Markdown ⇄ compiled drift** → invariant test fails; keep `00_INDEX.md` and the
  shard row in sync.
- **Unresolvable `Location`** or **invalid `TaskFamily`** → invariant failure; use
  a known named location and a fixed family head.
- **Duplicate `Id`** → uniqueness failure.

## Related skills

- [[coordinator-implementation]] — drives the Activity at runtime.
- [[botrunner-task-implementation]] — the `TaskFamily` resolves to Tasks.
- [[loadout-template-authoring]] — the role/loadout the Activity expects.
- Reference: `docs/Spec/04_ACTIVITIES.md`, `docs/architecture/aota/`.
