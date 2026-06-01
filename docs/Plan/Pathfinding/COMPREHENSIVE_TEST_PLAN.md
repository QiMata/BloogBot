# Comprehensive Pathfinding + Long-Travel Test Plan

> Public-facing inventory + per-dungeon promotion checklist for the
> `RecordedTests.PathingTests/` harness. Companion to
> [`BAKE_RECIPE.md`](BAKE_RECIPE.md).
>
> Initial authoring: 2026-05-29, after loop 24 reached **20-row Stable baseline (23/0/0 prod-data sweep)** on
> prod-data and loop 25's long-travel decklip/wall-support tuning settled.

## Why

The pathfinding refactor (loops 17-24) closed at 23 routes / 0 failures
on prod-data, covering the canonical Eastern Kingdoms + Kalimdor surface
plus the OG zeppelin off-mesh. That's a solid floor but the actual
coverage gap is large:

- Only **2 of 21 vanilla dungeons** have any pathing test
  (Deadmines map 36 entrance→VanCleef; Wailing Caverns map 43 spiral).
- **No systematic cross-continent or capital-interior** routes.
- No multi-segment **boss-to-boss chain** test shape — the existing
  schema is single-segment Start→End only.

This plan delivers the inventory, schema, runner, and gating to expand
coverage methodically without breaking the green build.

## Phases

| Phase | Scope | Status | Estimated loops |
|---|---|---|---|
| 0 | Bake driver audit | **DONE** ([BAKE_RECIPE.md](BAKE_RECIPE.md)) | 1 |
| 1 | Schema + Status + seed all rows + plan doc | **DONE** (this commit) | 2-3 |
| 2 | Per-dungeon promotion (21 dungeons) | not started | 50-80 |
| 3 | Long-travel route promotion (~15 routes) | not started | 15-30 |
| 4 | FG parity sweep over all Stable rows | not started | 1-2 |

## Phase 0 outcome (foundational finding)

[BAKE_RECIPE.md](BAKE_RECIPE.md) documents that **every vanilla dungeon
plus several raids is already baked in prod-data** (maps 33, 34, 36, 43,
47, 48, 70, 90, 109, 129, 189, 209, 229, 230, 289, 329, 349, 389, 429,
plus raid maps 249, 309, 409, 469, 509, 531, 533). This contradicted the
initial plan assumption that 19 dungeons would need bake-tuning before
testing could begin.

**Impact**: Phase 2 reframes from "bake tuning per dungeon" to "waypoint
authoring + sweep validation per dungeon" — a much shorter loop. Total
Phase 2 estimate revised from 80-150 loops to **50-80 loops**.

## Phase 1 outcome (this commit)

### Schema extension

[`PathingTestDefinition`](../../../RecordedTests.PathingTests/Models/PathingTestDefinition.cs)
gained two additive fields (all 23 existing rows compile unchanged):

```csharp
public record PathingTestDefinition(
    ...,  // unchanged: Name, Category, Description, MapId, StartPosition,
          // EndPosition, SetupCommands, TeardownCommands, ExpectedDuration,
          // Transport, IntermediateWaypoint, EndMapId
    IReadOnlyList<NamedWaypoint>? Waypoints = null,    // NEW: multi-segment chain
    TestStatus Status = TestStatus.Stable,             // NEW: default-Stable gating
    string? StatusReason = null);                       // NEW: --list-skipped reason

public record NamedWaypoint(string Name, Position Position);

public enum TestStatus { Stable, Experimental, BakeBlocked, Skipped }
```

Validation (`Validate()`):
- Exactly one of `EndPosition` or `Waypoints` MUST be set.
- `Waypoints.Count >= 2` when set.

### Runner extension

[`BackgroundRecordedTestRunner`](../../../RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs)
now dispatches:
- `IsMultiSegment` (Waypoints set) → `RunChainAsync` iterates each
  `Waypoints[i] → Waypoints[i+1]` as a sub-path, asserting per-segment
  arrival; per-segment screenshot + position-history dump so partial
  failures are diagnosable.
- Otherwise → `RunSingleSegmentAsync` (legacy code path; zero behavior
  delta for existing rows).

`ForegroundRecordedTestRunner` is unchanged: it executes GM setup/teardown
commands but does no navigation. FG/BG parity testing happens in
`Tests/Navigation.Physics.Tests/`, not here.

### Status-aware filtering

[`Program.FilterTests`](../../../RecordedTests.PathingTests/Program.cs#L260)
now applies status gating BEFORE name/category filters. New CLI flags
([`ConfigurationParser`](../../../RecordedTests.PathingTests/Configuration/ConfigurationParser.cs)):

| Flag | Default | Effect |
|---|---|---|
| `--status <list>` | unset | Explicit comma-separated whitelist (e.g. `--status Stable,Experimental`). Overrides include flags. |
| `--include-experimental` | false | Add Experimental rows to the Stable default. |
| `--include-bake-blocked` | false | Add BakeBlocked rows to the Stable default. |
| `--list-skipped` | false | Print all non-Stable rows with `Status` + `StatusReason`, then exit without running. |

Default green-build behavior (`dotnet run` no args) is **still 20-row Stable baseline (23/0/0 prod-data sweep)**
because all newly-seeded rows have `Status = Experimental`.

### Seed inventory (this commit, all Experimental)

| Category | Rows | Schema shape | Notes |
|---|---|---|---|
| `Dungeon` (scaffolds) | **27** | Single-segment | One per vanilla dungeon wing (incl. SM × 4, DM × 3, Strat × 2, Maraudon × 2). Phase 2 replaces single segment with Waypoints[] boss chain. |
| `LongTravel.EK` | 8 | Single-segment | Pure-walk EK routes |
| `LongTravel.Kalimdor` | 6 | Single-segment | Pure-walk Kalimdor routes |
| `LongTravel.CrossContinent` | 4 | Single-segment | Boat/zeppelin handoff routes |
| `GrandTour` | 2 | **Multi-segment chain** (7 waypoints each) | Alliance + Horde capital + hub tours |
| `CapitalLoop` | 4 | **Multi-segment chain** (5 waypoints each) | Stormwind, Orgrimmar, Ironforge, Undercity interior loops |
| **Phase 1 total** | **51** | | All `Status = Experimental` |

### How to run the new rows

```powershell
# See what's gated (diagnostic; no execution):
dotnet run --project RecordedTests.PathingTests -- --list-skipped

# Run only the existing 20 Stable rows (default green build, 20-row Stable baseline (23/0/0 prod-data sweep) sweep):
dotnet run --project RecordedTests.PathingTests

# Run all Experimental rows:
dotnet run --project RecordedTests.PathingTests -- --include-experimental

# Run only capital-interior loops (multi-segment chain runner):
dotnet run --project RecordedTests.PathingTests -- --include-experimental --category CapitalLoop

# Run a specific dungeon scaffold:
dotnet run --project RecordedTests.PathingTests -- --include-experimental --test-filter Dungeon_RagefireChasm_Scaffold

# Run multiple status groups explicitly:
dotnet run --project RecordedTests.PathingTests -- --status Stable,Experimental
```

## Phase 2 — per-dungeon promotion checklist

For each of the 21 vanilla dungeons (27 row entries including wing
splits), one iteration loop:

1. **Verify bake**: confirm `D:/wwow-bot/prod-data/mmaps/<mapId>.mmap`
   exists and has multiple `.mmtile` files (per [BAKE_RECIPE.md](BAKE_RECIPE.md)).
2. **Author boss waypoints** via FG `/go` probing + screenshot pin
   (loop-24 pattern). Inside the instance, walk to each named boss
   encounter location, capture coord. Replace the scaffold row's
   `EndPosition` with a `Waypoints[]` chain.
3. **Wire off-mesh links** for in-dungeon teleports / elevators
   (BRD elevator, DM Tribute teleport, Gnomer launch pad, ST wing
   portals, Strat pylons, Maraudon waterfalls). Add entries to
   [`tools/MmapGen/offmesh.txt`](../../../tools/MmapGen/offmesh.txt)
   and re-bake the affected tile per [BAKE_RECIPE.md](BAKE_RECIPE.md).
4. **Run BG sweep**: `--include-experimental --test-filter Dungeon_<Name>_Scaffold`,
   iterate to 5/0 consecutive runs.
5. **Flip Status** from Experimental → Stable. Rename row from
   `_Scaffold` suffix to `_BossChain` (or similar). Commit + push per
   monorepo R15.
6. **Default sweep** now includes the dungeon — count goes 23 → 24 → … → 49.

### Recommended Phase 2 ordering

By complexity (per BAKE_RECIPE.md tile-count calibration):

**Trivial (2-3 loops each):** Stockades, Ragefire Chasm, Razorfen Kraul

**Medium (3-5 loops each):** Wailing Caverns, Shadowfang Keep, Blackfathom
Deeps, Razorfen Downs, Gnomeregan, Uldaman, Zul'Farrak, Sunken Temple,
Maraudon × 2, SM × 4 wings

**Complex (5-10 loops each):** Deadmines (extend existing test), Scholomance,
Dire Maul × 3, Stratholme × 2

**Most complex (10+ loops each):** Blackrock Depths, LBRS, UBRS (BRM
geometry is the loop-25 long-pole)

## Phase 3 — long-travel route promotion checklist

For each of the 16 long-travel/capital rows:

1. Run `--include-experimental --test-filter <RowName>` BG-only.
2. Iterate on any `BlockedReason` / stuck failures. Most failures will be:
   - Missing off-mesh for elevator/zeppelin handoff (add to offmesh.txt + rebake)
   - Long-travel decklip issue (loop-25 territory; see commits since `a0385e1f`)
3. 5/0 consecutive → flip Stable.

**Recommended Phase 3 ordering** (lowest risk first):

1. `CapitalLoop_Stormwind_TradeAH_Bank_MageQuarter_Cathedral` — short interior, validated capital geometry
2. `CapitalLoop_Orgrimmar_Valley_AH_Cleft_Drag` — same, Horde
3. `LongTravel_Kalimdor_Crossroads_To_Ratchet` — short road
4. `LongTravel_EK_Goldshire_To_Lakeshire` — short cross-zone
5. `CapitalLoop_Ironforge_*` + `CapitalLoop_Undercity_*`
6. `LongTravel_EK_BootyBay_To_Stormwind_NorthRoad`
7. `LongTravel_Kalimdor_Astranaar_To_Auberdine`
8. `LongTravel_EK_FlameCrest_To_Ironforge` — BRM long-pathing
9. `CrossContinent_*` (boats/zeppelins) — transport handoffs
10. `LongTravel_EK_Menethil_To_LightHopeChapel` — end-to-end EK
11. `LongTravel_Kalimdor_Gadgetzan_To_CenarionHold` — end-to-end Kalimdor
12. `GrandTour_*` (last; depends on all the above being green)

## Phase 4 — FG parity sweep

FG already executes alongside BG today (paired in [`Program.cs:317-336`](../../../RecordedTests.PathingTests/Program.cs#L317))
but performs no navigation — only GM commands. The "parity sweep" here
is a verification that, with all Phase 2+3 rows Stable:

- `--status Stable` runs cleanly on a full BG sweep.
- Any FG/BG navigation divergence files as a
  [`Tests/Navigation.Physics.Tests/`](../../../Tests/Navigation.Physics.Tests/)
  issue per [monorepo R13](../../../../CLAUDE.md), NOT a pathfinding
  issue.

## Out of scope

- Raid-tier maps (MC 409, BWL 469, ZG 309, AQ20 509, AQ40 531, Onyxia 249,
  Naxxramas 533) — explicitly deferred. Catalog them in a follow-up plan.
- Battleground maps (WSG 489, AB 529, AV 30) — deferred to a BG-specific
  test plan.
- StateManager orchestration changes — purely a `RecordedTests.PathingTests`
  scope expansion.
- Navmesh bake driver changes — [BAKE_RECIPE.md](BAKE_RECIPE.md) is the
  Phase 0 deliverable; per-tile knob tuning happens inside dungeon
  promotion loops (Phase 2) when failures surface.

## Definition of done

Phase 1: this commit
- [x] [`BAKE_RECIPE.md`](BAKE_RECIPE.md) ships
- [x] Schema extended (Waypoints, Status, StatusReason, NamedWaypoint, TestStatus, Validate)
- [x] BackgroundRecordedTestRunner supports multi-segment chains
- [x] Status-aware FilterTests + `--include-experimental` + `--include-bake-blocked` + `--status` + `--list-skipped`
- [x] 51 new rows seeded as Experimental
- [x] This plan doc
- [ ] Regression guard: `dotnet build` clean + unit tests for `Validate()` invariants

Phase 2 exit: 20 → ~47 Stable rows (each promoted dungeon adds 1)
Phase 3 exit: ~47 → ~63 Stable rows (each promoted long-travel/capital row adds 1)
Phase 4 exit: all Stable rows green on FG + BG paired execution
