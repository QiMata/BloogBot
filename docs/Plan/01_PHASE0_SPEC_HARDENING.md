# Plan 01 — Phase 0: Spec Hardening

## Goal

Land the spec contracts (this file's siblings under `Spec/`) plus the
compiled activity catalog and its test enforcement. After Phase 0, an
autonomous agent reading only `SPEC.md` can pick up Phase 1.

## Entry pre-requisite

- `docs/SPEC.md`, `docs/Spec/*`, `docs/Plan/00_OVERVIEW.md` all merged.

## Exit criteria

- [ ] All 16 Spec/ contracts exist and pass markdown lint.
- [ ] `Services/WoWStateManager/Activities/ActivityCatalog.cs` exists
      with ~88 rows, compiles, and is wired through a singleton
      `IActivityCatalog`.
- [ ] `Tests/BotRunner.Tests/Activities/ActivityCatalogTests.cs`
      enforces the Phase 0 invariants from
      [`Spec/04_ACTIVITIES.md#catalog--hard-coded-source-of-truth`](../Spec/04_ACTIVITIES.md#catalog--hard-coded-source-of-truth):
  - Every row has unique `Id`.
  - Every `Location` resolves to a non-empty entry in
    `Bot/named-locations.json` (resolver injected for the test).
  - Every `LevelRange` is in [1, 60] with `Min ≤ Max`.
  - Every `RoleTemplate` sums to ≥ `MinPlayers` and ≤ `MaxPlayers`.
  - Every `TaskFamily` is one of the fixed family-head strings.
  - Every `Family` is a valid `ActivityFamily` enum value.
  - One catalog row per `Plan/Activities/00_INDEX.md` entry.
- [ ] `FailureReason` enum lives at
      `Exports/GameData.Core/Enums/FailureReason.cs` and a catalog test
      enforces every code-side reason is in the spec doc.
- [ ] `Plan/Activities/00_INDEX.md` lists every catalog row with its
      slot status (`spec` / `task-family` / `coordinator` / `tests`).
- [ ] `Plan/QUESTIONS.md` has no open entries for Phase 0.

## Slots

### S0.1 — Land the spec tree

- **Owner:** human (this session)
- **Status:** done
- **Owned paths:** `docs/SPEC.md`, `docs/Spec/`, `docs/Plan/00_OVERVIEW.md`,
  `docs/Plan/QUESTIONS.md`
- **Goal:** every Spec/* file exists and is internally consistent.

### S0.2 — Author all Plan/ phase files

- **Owner:** human (this session)
- **Status:** in-progress
- **Owned paths:** `docs/Plan/*.md`, `docs/Plan/Activities/*.md`
- **Goal:** the 11 Plan/ files + `Activities/*` family files exist.

### S0.3 — Compiled `ActivityCatalog.cs`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.1, S0.2
- **Owned paths:**
  - `Services/WoWStateManager/Activities/**`
  - `Exports/GameData.Core/Models/Activities/**`
- **Read-only paths:**
  - `docs/Spec/04_ACTIVITIES.md`
  - `docs/Plan/Activities/**`
  - `docs/leveling-guide/**`
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md)
- **Goal:** Add a `static class ActivityCatalog` exposing an
  `IReadOnlyList<ActivityDefinition> All` and a
  `TryGetById(string id, out ActivityDefinition def)` lookup, populated
  with the ~88 rows from
  [`Activities/00_INDEX.md`](Activities/00_INDEX.md).
- **Procedure:**
  1. Add `ActivityDefinition`, `ActivityFamily`, `LevelRange`,
     `RoleTemplate`, `EntryRequirements`, `FactionPolicy`,
     `HumanJoinPolicy`, `BotSelectionPolicy`, `TravelTarget`,
     `RewardDefinition` records to `Exports/GameData.Core/Models/Activities/`.
  2. Add `IActivityCatalog` to `Services/WoWStateManager/Activities/`.
  3. Implement `ActivityCatalog` static class with all rows.
  4. Register in `Program.cs` DI.
  5. Add `CatalogVersion` constant (start at `1`).
- **Success criteria:**
  - [ ] `dotnet build WestworldOfWarcraft.sln` succeeds.
  - [ ] `dotnet test Tests/BotRunner.Tests --filter
        "FullyQualifiedName~ActivityCatalogTests"` reports
        `passed: <row_count> + 6`.

### S0.4 — Catalog tests

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.3
- **Owned paths:**
  - `Tests/BotRunner.Tests/Activities/**`
- **Read-only paths:**
  - `Services/WoWStateManager/Activities/**`
  - `Exports/GameData.Core/Models/Activities/**`
  - `docs/Spec/04_ACTIVITIES.md`
- **Goal:** Tests assert every catalog invariant. Tests **fail** if a
  row is added without satisfying invariants.
- **Procedure:**
  1. Implement `ActivityCatalogTests` with one test per invariant.
  2. Add `CatalogMarkdownDriftTests` that scans
     `docs/Plan/Activities/00_INDEX.md` and asserts the catalog row
     count matches.
- **Success criteria:**
  - [ ] All catalog tests pass.
  - [ ] Deliberately bad row (in a separate unit test using a sample
        catalog) is rejected.

### S0.5 — `FailureReason` enum

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.1
- **Owned paths:**
  - `Exports/GameData.Core/Enums/FailureReason.cs`
  - `Exports/GameData.Core/Exceptions/BotTaskFailedException.cs`
  - `Tests/BotRunner.Tests/Spec/FailureReasonCatalogTests.cs` (relocated 2026-05-12 — `Tests.Infrastructure` is shared fixtures, not a test project per `Tests/CLAUDE.md`)
- **Read-only paths:** `docs/Spec/12_ERROR_TAXONOMY.md`
- **Goal:** Enum + exception type + drift test exist; no code wires
  yet (Phase 1 does the wiring).
- **Success criteria:**
  - [ ] Enum compiles and all values match
        [`Spec/12_ERROR_TAXONOMY.md`](../Spec/12_ERROR_TAXONOMY.md).
  - [ ] `FailureReasonCatalogTests` asserts drift between code and doc.

### S0.6 — `Plan/Activities/00_INDEX.md`

- **Owner:** human (this session) / `monorepo-worker` for revisions
- **Status:** in-progress
- **Owned paths:** `docs/Plan/Activities/**`
- **Goal:** Every catalog row has an entry with status fields:
  spec / task-family / coordinator / tests.

### S0.7 — Validate spec self-sufficiency

- **Owner:** `monorepo-reviewer`
- **Status:** open
- **Depends on:** S0.1, S0.2, S0.3, S0.4, S0.5, S0.6, S0.8, S0.9, S0.10, S0.11, S0.12
- **Goal:** A fresh agent that reads only `docs/SPEC.md` and
  `docs/Plan/00_OVERVIEW.md` can pick up the next open slot and
  produce a correct implementation without further human input.
- **Procedure:**
  1. Spawn a `monorepo-explorer` subagent with the instruction: "Read
     `docs/SPEC.md` then claim the next open slot in
     `docs/Plan/01_PHASE0_SPEC_HARDENING.md` and report what you would
     do, without doing it."
  2. Compare the report to S0.3's required procedure.
  3. If material drift, write a fix-up slot.
- **Success criteria:**
  - [ ] Subagent's plan matches the slot's procedure within editorial
        differences only.

## Spec-precision slots (added 2026-05-12)

The 2026-05-12 handoff added five spec-hardening slots. Goal: when a
Phase 1 `monorepo-worker` claims any S1.x slot, the spec leaves no
decision to the implementer. These slots produce **only docs, schemas,
and seed data** — no production code.

### S0.8 — Per-task-family detail in `Plan/Activities/`

Each `Plan/Activities/<family>.md` today is a placeholder: task names
with rough status, no method signatures, no test method names, no
opcode lists. Expand each family file so every task in the family has:

- **Class declaration:** exact class name + namespace + file path
  (e.g. `BloogBot.BotRunner.Tasks.Movement.GoToTask` at
  `Exports/BotRunner/Tasks/Movement/GoToTask.cs`).
- **Public surface — current shipped:** the method signatures that
  exist today (usually `void Update()` plus constructor + private
  state machine entry points). Document what's there, not what the
  spec says should be there.
- **Public surface — target (Phase 1):** the `IBotTask` overrides
  per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`:
  `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
  `OnChildFailedAsync`. Phase 1 slot S1.0 closes this gap; family
  workers code against the target after S1.0 lands (see R19 in
  `Plan/QUESTIONS.md`).
- **Snapshot contract:** the `WoWActivitySnapshot` fields the task
  reads from + the fields it writes/mutates (so tests know what to
  assert on).
- **BG protocol footprint:** the exact list of CMSG opcodes the task
  sends on BG (cite `Exports/WoWSharpClient/Opcodes/`).
- **FG memory footprint:** the FG `IObjectManager` reads + Lua calls
  the task makes on FG (cite `Services/ForegroundBotRunner/Objects/`
  and `Services/ForegroundBotRunner/Lua/`).
- **Test anchor:** the exact LiveValidation test class + test method
  name + the `dotnet test --filter` command to run it.
- **Catalog `TaskFamily` claim:** which catalog row(s) this task
  satisfies (cross-reference `Plan/Activities/00_INDEX.md`).

S0.8 is dispatched as 16 parallel sub-slots, one per family file
(disjoint owned paths). Sub-slot template:

> **S0.8.\<n\> — \<Family\> family detail**
> - **Owner:** `monorepo-worker`
> - **Status:** open
> - **Depends on:** S0.1, S0.2
> - **Owned paths:** `docs/Plan/Activities/<family>.md`
> - **Read-only paths:**
>   - `Exports/BotRunner/Tasks/**`
>   - `Exports/BotRunner/Activities/**`
>   - `Exports/WoWSharpClient/Opcodes/**`
>   - `Exports/WoWSharpClient/Agents/**`
>   - `Services/ForegroundBotRunner/**`
>   - `Tests/BotRunner.Tests/LiveValidation/**`
>   - `docs/Plan/Activities/00_INDEX.md`
>   - `docs/Spec/03_BOTRUNNER.md`
> - **Goal:** every task in the family has the six-bullet detail
>   block above.
> - **Procedure:**
>   1. Read the current family file to enumerate task names.
>   2. For each task name, find the implementing class (`Glob` on
>      `**/<TaskName>.cs`); if absent, flag as `not-started` and
>      include a "planned anchor file" line.
>   3. Extract **current shipped** method signatures via `Read` on
>      the class. The **target** surface is constant per
>      `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`
>      (write it verbatim once or by reference).
>   4. Extract snapshot reads/writes by grepping the class for
>      `_snapshot.` / `context.Snapshot.` / `BuildSnapshot`.
>   5. Extract BG opcodes by grepping for `Opcode.CMSG_*` in the
>      class + its agents.
>   6. Extract FG calls by grepping for `IObjectManager.` and
>      `LuaCall` in the class.
>   7. Find LiveValidation tests by grepping
>      `Tests/BotRunner.Tests/LiveValidation/` for the task name.
>   8. Rewrite the family file with the new structure. Preserve
>      existing slot definitions (ST.x, etc.); just add the
>      per-task detail block above the slot section.
> - **Success criteria:**
>   - [ ] Every task name in the family table has all six bullets.
>   - [ ] Markdown lints clean.
>   - [ ] The detail block is precise enough that a Phase 1 worker
>         can implement against it without further investigation.

The 16 family sub-slots:

| Sub-slot | Family | Owned path |
|---|---|---|
| S0.8.1 | Travel | `docs/Plan/Activities/travel.md` |
| S0.8.2 | Combat | `docs/Plan/Activities/combat.md` |
| S0.8.3 | Questing | `docs/Plan/Activities/quests.md` |
| S0.8.4 | Dungeons | `docs/Plan/Activities/dungeons.md` |
| S0.8.5 | Raids | `docs/Plan/Activities/raids.md` |
| S0.8.6 | Battlegrounds | `docs/Plan/Activities/battlegrounds.md` |
| S0.8.7 | Professions-gathering | `docs/Plan/Activities/professions-gathering.md` |
| S0.8.8 | Professions-crafting | `docs/Plan/Activities/professions-crafting.md` |
| S0.8.9 | Economy | `docs/Plan/Activities/economy.md` |
| S0.8.10 | Social | `docs/Plan/Activities/social.md` |
| S0.8.11 | Recovery | `docs/Plan/Activities/recovery.md` |
| S0.8.12 | Reputations | `docs/Plan/Activities/reputations.md` |
| S0.8.13 | Attunements | `docs/Plan/Activities/attunements.md` |
| S0.8.14 | World events | `docs/Plan/Activities/world-events.md` |
| S0.8.15 | World bosses | `docs/Plan/Activities/world-bosses.md` |
| S0.8.16 | PvP | `docs/Plan/Activities/pvp.md` |

### S0.9 — Concrete catalog row authorship

`Plan/Activities/00_INDEX.md` lists 88 rows by id + activity + location
only. S0.3 (`ActivityCatalog.cs`) will need the **complete
`ActivityDefinition` literal** for each row — every field spelled out
per the record shape in `Spec/04_ACTIVITIES.md`.

Deliverable: a new file `docs/Plan/Activities/01_CATALOG_ROWS.md`
containing a fenced C# block per row with every field populated:
`Id`, `Family`, `Activity`, `Location`, `LevelRange`, `FactionPolicy`,
`MinPlayers`, `MaxPlayers`, `RoleTemplate`, `EntryRequirements`,
`TravelTarget` (with `NamedLocation` key), `ExpectedDuration`,
`HumanJoinPolicy`, `BotSelectionPolicy`, `ProgressionTags`, `Rewards`,
`TaskFamily`.

Dispatched as **5 parallel sub-slots**, each owning a unique shard
file under `docs/Plan/Activities/_catalog_rows/`. Lead does a final
reconcile pass to concatenate the shards into `01_CATALOG_ROWS.md`.

Sub-slot template:

> **S0.9.\<n\> — \<Family group\> catalog rows**
> - **Owner:** `monorepo-worker`
> - **Status:** open
> - **Depends on:** S0.8 (family detail informs `TaskFamily` claims),
>   S0.12 (named locations).
> - **Owned paths:** `docs/Plan/Activities/_catalog_rows/<shard>.md`
>   (unique per sub-slot; lead reconciles into
>   `docs/Plan/Activities/01_CATALOG_ROWS.md`).
> - **Read-only paths:**
>   - `docs/Spec/04_ACTIVITIES.md`
>   - `docs/Plan/Activities/00_INDEX.md`
>   - `docs/Plan/Activities/<family>.md`
>   - `Bot/named-locations.json` (after S0.12)
>   - `docs/leveling-guide/**`
> - **Goal:** every row in the family has a complete
>   `ActivityDefinition` literal that compiles without further
>   editing once `ActivityCatalog.cs` is authored.
> - **Procedure:**
>   1. Open `00_INDEX.md`, read the family rows.
>   2. For each row, populate every field per the record in
>      `Spec/04_ACTIVITIES.md`.
>   3. `Location` = canonical name from `00_INDEX.md`.
>   4. `TravelTarget.NamedLocation` = same key (will resolve via
>      S0.12 seed).
>   5. `TravelTarget.MapId/X/Y/Z` = best-effort from leveling guide
>      or `0` (S0.12 seed is authoritative; this is a duplicate hint).
>   6. `TaskFamily` = the family-head string per
>      `Spec/03_BOTRUNNER.md#catalog-of-task-families`.
>   7. `Rewards` = at least one `RewardDefinition` (XpRange or
>      ItemId) per row; consult `docs/leveling-guide/` for hints.
>   8. `EntryRequirements` = items/quests/reps/attunements lists
>      (often empty) + `LockoutPolicy` (per `Spec/04` hint rules).
>   9. Append to `01_CATALOG_ROWS.md` under the family's H2 heading
>      using fenced ```csharp blocks, one per row.
> - **Success criteria:**
>   - [ ] Every row in the family has a complete literal.
>   - [ ] Every `Location` matches the seed in `named-locations.json`.
>   - [ ] Every `TaskFamily` is in the fixed family-head list.
>   - [ ] `LevelRange` matches the range in `00_INDEX.md`.

The 5 S0.9 shard sub-slots (balanced for ~16-21 rows each):

| Sub-slot | Shard file | Families covered | Rows |
|---|---|---|---|
| S0.9.1 | `_catalog_rows/01_questing_part1.md` | Starter questing (6) + zone questing Westfall→Hillsbrad (10) | 16 |
| S0.9.2 | `_catalog_rows/02_questing_part2.md` | Zone questing Stonetalon→Felwood (13) + Un'Goro→Silithus (6) | 19 |
| S0.9.3 | `_catalog_rows/03_dungeons.md` | All 21 dungeons (Ragefire Chasm → Stratholme Live) | 21 |
| S0.9.4 | `_catalog_rows/04_raids_bg_attune.md` | Raids (7) + battlegrounds (3) + attunements (5) | 15 |
| S0.9.5 | `_catalog_rows/05_misc.md` | Professions (4) + economy (2) + reputations (5) + world events (1) + world bosses (3) | 15 |

**Reconciliation:** after all 5 shards land, the lead writes
`docs/Plan/Activities/01_CATALOG_ROWS.md` with an H1 + intro followed
by `## <Family>` sections containing the shards' fenced C# blocks in
catalog order matching `00_INDEX.md`. The lead also removes the
shard directory or leaves it as an audit trail (decision at reconcile
time).

### S0.10 — `LoadoutSpec` schema

`Services/WoWStateManager/Settings/CharacterSettings.cs:148` already has
`LoadoutSpecSettings` as a POCO but no doc enumerates its fields. Author
`docs/Spec/17_LOADOUT.md` covering:

- Every property of `LoadoutSpecSettings`, with type + units + valid
  range.
- The JSON field → POCO property → proto message mapping (cite
  `Exports/BotCommLayer/Protos/` and the existing
  `communication.proto`).
- The GM-command translation per field (level, spells, skills, gear,
  talents, reputation) — exact `.character`/`.learn`/`.setskill`/etc.
  invocations.
- Validation rules + failure modes.

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.1
- **Owned paths:** `docs/Spec/17_LOADOUT.md`
- **Read-only paths:**
  - `Services/WoWStateManager/Settings/CharacterSettings.cs`
  - `Services/WoWStateManager/MangosServerBootstrapper.cs`
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Exports/BotCommLayer/Protos/**`
  - `docs/Spec/02_STATEMANAGER.md`, `docs/Spec/03_BOTRUNNER.md`
- **Goal:** an OnDemand engine implementer can author a per-activity
  loadout config and a `LoadoutTask` implementer can wire each field
  to GM commands without reading source.
- **Success criteria:**
  - [ ] `Spec/17_LOADOUT.md` enumerates every POCO field.
  - [ ] Each field has JSON → POCO → proto → GM-command mapping rows.
  - [ ] `docs/SPEC.md` table updated to include row 17.

### S0.11 — `ActivityConfig` JSON schema + per-family examples

Per `Plan/03_PHASE2_ONDEMAND_ENGINE.md` S2.7, each OnDemand activity
has a config at `Config/activities/<id>.json`. The shape is referenced
but not specified. Author:

- `Config/schema/activity.schema.json` (JSON Schema draft, draft-2020-12).
- One example config per family — at minimum:
  - `Config/activities/dungeon.ragefire-chasm.json`
  - `Config/activities/raid.zg.json`
  - `Config/activities/bg.wsg.json`
  - `Config/activities/prof.mining-route.json`
  - `Config/activities/quest.starter.durotar.json`
  - `Config/activities/attune.mc.json`
  - `Config/activities/econ.vendor-loop.json`
  - `Config/activities/rep.timbermaw-hold.json`
  - `Config/activities/event.stv-fishing-extravaganza.json`
  - `Config/activities/boss.azuregos.json`
- A `Tests/Tests.Infrastructure/ActivityConfigSchemaTests.cs` (stub —
  test file added; assertions land with S0.3) that validates every
  config under `Config/activities/` against the schema.

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.1
- **Owned paths:**
  - `Config/schema/activity.schema.json`
  - `Config/activities/**`
  - `Tests/BotRunner.Tests/Activities/ActivityConfigSchemaTests.cs` (relocated 2026-05-12 — see S0.5 note)
- **Read-only paths:**
  - `docs/Spec/02_STATEMANAGER.md` (OnDemand stages)
  - `docs/Spec/04_ACTIVITIES.md`
  - `docs/Plan/03_PHASE2_ONDEMAND_ENGINE.md` S2.7
  - `docs/Spec/17_LOADOUT.md` (S0.10 — `LoadoutSpec` reuse)
- **Goal:** Phase 2's OnDemand launcher reads schema-validated config
  per activity, with no implicit fields.
- **Success criteria:**
  - [ ] Schema covers: `activityId`, `loadout`, `lockoutSkip`,
        `levelOverride`, `gearOverrides`, `reputationOverrides`,
        `roleOverrides`, `stagingLocation`, `humanJoin`,
        `lootPolicy`, `tearDownPolicy`.
  - [ ] Every example config validates against the schema.
  - [ ] Markdown lints clean.

### S0.12 — `Bot/named-locations.json` schema + seed

Per R14, `ActivityDefinition.Location` resolves through
`Bot/named-locations.json`. Author:

- `Bot/named-locations.schema.json` (JSON Schema draft-2020-12).
- `Bot/named-locations.json` seed file with every `TravelTarget`
  referenced by the 88 catalog rows resolved to
  `{ MapId, X, Y, Z, Notes }`.

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.1
- **Owned paths:**
  - `Bot/named-locations.json`
  - `Bot/named-locations.schema.json`
- **Read-only paths:**
  - `docs/Plan/Activities/00_INDEX.md`
  - `docs/Spec/04_ACTIVITIES.md`
  - `docs/server-protocol/**`
  - existing `Exports/BotRunner/Movement/**`
  - existing `Bot/**` (if a similar file is present)
- **Goal:** every `Location` string in `00_INDEX.md` resolves to a
  non-empty entry. Phase 0 catalog tests will assert this.
- **Procedure:**
  1. Enumerate every distinct `Location` string in `00_INDEX.md`.
  2. For each, resolve `(MapId, X, Y, Z)` via:
     - `docs/leveling-guide/` zone summaries.
     - `mangos.creature_template` filtered by `npcflags`
       (innkeepers, flight masters) — read-only DB hint.
     - `mangos.areatrigger_teleport` (dungeon/raid portals).
     - Hand-author capital city centers.
  3. Write JSON Schema with required: `mapId` (uint), `x` (number),
     `y` (number), `z` (number), optional: `notes` (string).
  4. Write seed file with every location keyed by canonical name.
- **Success criteria:**
  - [ ] Every distinct `Location` in `00_INDEX.md` has an entry.
  - [ ] Seed validates against schema.
  - [ ] No entry has `(0, 0, 0)` coords (placeholder check).
