# Handoff: close the remaining 3 PFS failures via tile (40, 29) MmapGen per-tile config

You are picking up a fresh Claude session for `e:/repos/Westworld of
Warcraft/` (the WWoW repo on `main`, last commits `ba563ff7` →
`13f2fbd7` → `7a692940`). The resolver-side pathfinding work is
**complete** — 5-layer fix landed across loops 4-17. Sweep coverage
expanded from 11/13 → 17/20 (3 unrun). One originally-failing test
(`exterior_incline_exact`) is now CLOSED.

Your job: **close the remaining failures via per-tile MmapGen
config on tile (40, 29) `0012940.mmtile`** AND run the 3 unrun
cases to confirm full coverage. All 23 of `CrossroadsToUndercity_CriticalWalkLegs`
should be green when you're done. Plus no regression in adjacent
suites.

The resolver-side surface is at its architectural limit — the bake
itself emits polygon-adjacent corners across unwalkable terrain on
tile (40, 29). Do NOT iterate further on `TryExpandCorridorWithSmoothNativeSegments`,
`ValidateAndRepathSmoothSubPairs`, `SmoothRespathOversizeBypassSegments`,
or the test-side LOS-walkable fallback — those layers are
load-bearing and shipped. Touch them only if you discover a real
correctness bug.

## Read first (in order)

1. This handoff.
2. `e:/repos/CLAUDE.md` (R1-R13).
3. `e:/repos/Westworld of Warcraft/CLAUDE.md` — especially the
   pathfinding overhaul charter and the Token-Efficient Tooling
   section.
4. `e:/repos/Westworld of Warcraft/docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`
   §S1.3 — the "Latest evidence (2026-05-16 loop 17)" block has
   the full per-test trail + remaining failure breakdown.
5. Memory entries in `C:/Users/lrhod/.claude/projects/e--repos/memory/`:
   - `project_pfs_exterior_incline_los_smooth_expand` (the 5-layer
     fix, what's shipped, remaining 3 bake-side failures)
   - `project_pfs_overhaul_006_decklip_solution` (Cycle 17e per-tile
     config precedent for THIS EXACT TILE — `agentMaxClimbTerrain=0.2`
     + smooth-path Z-delta interpolation)
   - `project_pfs_overhaul_006_intra_tile_disconnect` (more tile
     (40, 29) bake context)
   - `project_pfs_overhaul_006_config_key_inversion` (per-tile
     `cs` override needs paired `tileSize` override)
   - `feedback_promote_mmaps_tile_arg_xy` (MmapGen `-Tiles "X,Y"`
     vs filename `<map><Y><X>` convention)
   - `feedback_pathfinding_docker_reload` (restart wwow-pathfinding
     after every tile regen)
   - `feedback_pathfinding_freeze` + `feedback_pathfinding_anti_patterns`
     (mesh fixes only in tools/MmapGen/, don't lower walkableSlopeAngle)
   - `project_pfs_navigation_collection_serialization` (test-class
     serialization — already shipped, just know it's there)
6. The `mmo-pathfinding` skill (its "Bake-vs-runtime tolerance
   alignment" and "FG-rendering reconnaissance" sections), and
   `mmo-physics-pathing-probe` skill (R13 canary tool).
7. `tools/MmapGen/promote-mmaps.ps1` and `tools/MmapGen/offmesh.txt`
   (per-tile config file format).

Then: run `git log --oneline -15` and `docker ps` to confirm state.
Containers expected up: `wow-mangosd`, `wow-realmd`, `maria-db`,
`wwow-pathfinding`, `wwow-scene-data`.

## State at start of this session

**Loop 17 final tally** (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`):

| Tally | Loop 3 baseline | Loop 17 (you start here) |
|---|---|---|
| Pass | 11 of 13 | 17 of 20 |
| Fail | 2 of 13 | 3 of 20 |
| Unrun | 10 of 23 | 3 of 23 |

**The 3 failing tests** (all tile (40, 29) `0012940.mmtile` bake-side):

| Test | Failure shape | Coord | Slope | Class |
|---|---|---|---|---|
| `orgrimmar_exterior_steep_incline_live_stall_recovery` | Step-up at 62° | WP178→179 `(1348.0,-4537.7,35.4) → (1349.2,-4535.6,40.2)` | 63° | Polygon adjacency w/ unwalkable slope |
| `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery` | Z-delta 2.74y > 2.5y | WP27 `(1353.8,-4513.4,31.5)`, supportZ=28.762 | — | Off-surface densifier midpoint |
| `orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery` | Z-delta -3.6y vs support | WP62 `(1347.1,-4510.5,27.9)`, supportZ=31.548 | — | Off-surface densifier midpoint |

**The 3 unrun cases** (sweep hit 60-min `TestSessionTimeout` after case 20):

- `orgrimmar_zeppelin_tower_exterior_support_recovery`
- `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery`
- `orgrimmar_undercity_zeppelin_arrival_to_target`

These need to either: (a) run inside the 60-min budget AND pass,
or (b) the budget bumped further OR Theory split.

## What was shipped (don't redo)

`Services/PathfindingService/Repository/Navigation.cs` (commit `ba563ff7`):

1. **Corridor-level LOS gate** in
   `TryExpandCorridorWithSmoothNativeSegments` for short corridor
   pairs in `[2.5y, 6y)` with `|dz| >= 0.4y` and LOS-failing
   straight line.
2. **Sub-corner recursive `ValidateAndRepathSmoothSubPairs`** —
   post-pass on each smooth expansion's output.
3. **Pre-densifier `SmoothRespathOversizeBypassSegments`** in
   `BuildUsablePathResult` — for vertically-oversize pairs
   (`|dz| > 5y`), gated to `CorridorFirstExpanded` only.
4. **Resolver budget** `CalculateValidatedPathCore.totalDeadline`
   30s → 120s.

`Tests/PathfindingService.Tests/PathRouteAssertions.cs` (commit
`13f2fbd7`):

5. **Test-side LOS-walkable fallback** — raw `LineOfSight` fail
   falls back to `ValidateWalkableSegment` (the bot's actual
   physics capsule check).

`Tests/PathfindingService.Tests/test.runsettings` (commit `13f2fbd7`):

6. **TestSessionTimeout** 30min → 60min.

**Hard rule**: do not modify these layers unless you find a real
correctness bug. Tune the bake instead.

## Pick a path through (recommended cycle)

This is multi-cycle work. Each cycle does ONE thing. Don't try to
land everything in one loop.

### Cycle 1 — Validate the 3 unrun cases (lowest risk, highest info)

Before touching the bake, run the 3 unrun cases in isolation to
see what they're actually doing. They might be:

- (a) Passing in isolation (sweep just ran out of time).
- (b) Failing with new failure shapes.

If (a), the only fix needed is budget headroom. Either:
- Raise `TestSessionTimeout` further (e.g., 90 min) — simple.
- Split the Theory into 2-3 parallel runs by route family — better
  long-term but per-collection cost is fixed.

If (b), characterize the failure and add to the bake-side queue.

**Run command**:
```powershell
$env:WWOW_DATA_DIR='D:/wwow-bot/prod-data'
dotnet test "Tests/PathfindingService.Tests/PathfindingService.Tests.csproj" `
  -c Release --no-build --nologo `
  --filter "FullyQualifiedName~CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" `
  -- RunConfiguration.TestSessionTimeout=5400000  # 90 min
```

Or run just cases #21-23 by selectively filtering. xUnit Theory
filter is awkward — easier path is to add `[InlineData]` SkipReason
to cases 1-20 temporarily, run the 3 cases, then revert.

**Acceptance**: all 23 cases run to completion in one sweep.

### Cycle 2 — Probe tile (40, 29) failures via PathPhysicsProbe

Use the `mmo-physics-pathing-probe` skill to localize each of the 3
bake-side failures precisely. For each:

- Probe with `--detour-resolve --smooth` between the failing pair's
  endpoints in isolation.
- Identify whether the failure is a:
  - **Polygon-adjacency-with-unwalkable-slope**
    (`exterior_steep_incline` — Detour returns 32 corners but
    individual pairs hit 62° slope).
  - **Off-surface densifier midpoint** (the two Z-delta failures
    — densifier produces midpoint Z that doesn't match GetGroundZ
    at that XY).

The Z-delta failures might NOT actually be polygon-graph defects —
they might be cases where `findPath` produces 2-corner output for
some sub-pair that DOESN'T get the sub-corner respath fix because
LOS happens to be clear at those endpoints. Investigate the
`SmoothSubCornerMinVerticalDelta = 0.4f` and `SmoothSubCornerLosThreshold = 2.5f`
gates — these might need adjustment for the underpass/bridge_side
geometry.

**Acceptance**: one-line classification per failing test (bake-side
polygon adjacency vs densifier off-surface) backed by probe data.

### Cycle 3 — Per-tile MmapGen config tuning on `0012940.mmtile`

Cycle 17e precedent: `agentMaxClimbTerrain=0.2` for tile (40, 29)
disconnected a polygon-corridor lower-platform shortcut. The
relevant offmesh.txt entry format is documented in
`project_pfs_overhaul_006_config_key_inversion`:

> Per-tile config keys must match CLI convention (`"4029"` for
> `--tile 40,29`), not offmesh.txt swapped form. Per-tile `cs`
> override needs paired `tileSize` override.

For `exterior_steep_incline` (the 62° slope WP178→179 case):

The polygon at this XY has a 62° slope adjacency. Per
`feedback_pathfinding_anti_patterns`, don't lower `walkableSlopeAngle`
globally. But you CAN tune per-tile `cs` / `ch` / `maxSimplificationError`
to break the polygon graph contour at the steep edge. Reference
the BRM Surface H success (commit history around 2026-05-14).

**Promote workflow** (per `feedback_promote_mmaps_tile_arg_xy`):
```powershell
# Regenerate tile (40, 29) - NOTE: -Tiles "X,Y" is X,Y not Y,X
cd "e:/repos/Westworld of Warcraft/tools/MmapGen"
./MmapGen.exe --map 1 --tile 40,29  # produces D:/MaNGOS/data/mmaps/0012940.mmtile

# Promote to test-data → prod-data
./promote-mmaps.ps1 -Map 1 -Tiles "40,29"

# REQUIRED: restart pathfinding container so it reloads tile cache
docker restart wwow-pathfinding
```

**Iteration recipe** (per `mmo-pathfinding` skill's "Done criteria
for a bake sweep"):
1. Tweak offmesh.txt or per-tile config.
2. Regenerate + promote.
3. Run `unit` baseline (213/0/7) — MUST stay green.
4. Run the specific failing test in isolation.
5. Run the full CrossroadsToUndercity_CriticalWalkLegs sweep to
   check for regressions in the other 17 passing tests.

**Acceptance**: each of the 3 bake-side failures closes WITHOUT
regressing any of the 17 currently-passing cases or breaking the
NavMeshPhysicsValidator / Layer B WaypointGeneration tests.

### Cycle 4 — Adjacent suite regression sweep

After all 23 PFS cases pass, run:

- `dotnet test Tests/RecordedTests.PathingTests.Tests/...` — was
  135/0/0; must stay 135/0/0.
- `dotnet test Tests/Navigation.Physics.Tests/...` — was 152/0/1
  (hang noted in handoff; still applicable).
- The PFS WaypointGeneration suite — was 76 unit tests green;
  tile bake changes shouldn't regress these but verify.

**Acceptance**: full S1.3 baseline as documented in `docs/TASKS.md`
remains green with the bake changes.

## Hard constraints (unchanged)

- **Pathfinding freeze (2026-05-06)**: mesh fixes only in
  `tools/MmapGen/`. Do NOT add new repair phases to
  `Services/PathfindingService/Repository/Navigation.cs`. The
  5 shipped layers are the last allowed.
- **R13 ordering**: scene-data → FG/BG physics parity → pathfinding.
  Probe each failing segment BEFORE touching the bake.
- **Don't lower `walkableSlopeAngle` / `walkableClimb`** from
  harvested client values. Use per-tile config knobs only (`cs`,
  `ch`, `maxSimplificationError`, `walkableHeight`,
  `agentMaxClimbTerrain`).
- **Don't author per-spot off-mesh entries** without probe-backed
  proof that the polygon-graph defect is geometric (a real
  in-world cliff/wall).
- **Always restart `wwow-pathfinding` after a tile regen** per
  `feedback_pathfinding_docker_reload`.
- **Test-class serialization** is shipped (commit `eeef7907`) — do
  not break the `[CollectionDefinition("Navigation",
  DisableParallelization=true)]` pattern.
- **Main-branch no-PR workflow** per `feedback_main_branch_no_pr_workflow`.
  Commits straight to `origin/main`.
- **Single Claude session, auto-compaction** — keep going.

## Don't repeat (lessons from loops 4-17)

- **Don't disable any of the 5 shipped layers** in
  `Services/PathfindingService/Repository/Navigation.cs` or
  `Tests/PathfindingService.Tests/PathRouteAssertions.cs`. They
  close real bugs.
- **Don't drop the resolver budget below 120s**. The sub-corner
  LOS-respath needs the headroom.
- **Don't try to fix `exterior_steep_incline` resolver-side**.
  Already verified the bake's polygon graph has a 62° adjacency
  Detour can't see around. Layers 1-3 successfully re-expand but
  the output corners include unwalkable sub-pairs. Bake-side
  only.
- **Don't conflate the Z-delta failures with the slope failure**.
  The Z-delta failures (`zeppelin_tower_underpass`,
  `zeppelin_bridge_side`) might respond to a TIGHTER `SmoothSubCornerMinVerticalDelta`
  gate (e.g., 0.3f instead of 0.4f) — try that BEFORE bake tuning,
  it's cheaper.
- **Don't relax `maxResolvedWaypointZDelta` past 4.0** for the
  underpass/bridge_side tests without probe-backed justification.
  The threshold is a real walkability invariant.
- **Don't regenerate tile (40, 29) without first running the
  validation harness** (`tools/PathPhysicsProbe` against the
  failing endpoints) on the new bake to verify the change actually
  closes the failure shape.

## Skills to use (invoke via the Skill tool — don't hand-roll)

- `mmo-physics-pathing-probe` (R13 canary — run FIRST for each
  failing pair to confirm bake-side vs resolver-side)
- `mmo-pathfinding` (per-tile config, bake regen, promote workflow,
  done-criteria for bake sweeps)
- `mmo-fg-client-re` (verify in-world geometry at the failing coord
  if probe data is ambiguous — e.g., is there really a 62° slope
  IN GAME at this XY?)
- `mmo-live-fixtures` (add new live coverage to lock in fixes)
- `codex:rescue` for log/large-file analysis — don't burn main
  context on PhysicsEngine.cpp, Navigation.cs, packet traces.

## Documentation requirements

For each cycle:

1. Save a `project_*` or `feedback_*` memory entry at
   `C:/Users/lrhod/.claude/projects/e--repos/memory/<slug>.md`
   for non-obvious findings. Add a one-line index entry at the
   top of MEMORY.md.
2. Update the relevant skill file (`.claude/skills/mmo-pathfinding/SKILL.md`
   AND `.agents/skills/mmo-pathfinding/SKILL.md`) if you discover
   a new diagnostic surface, failure mode, or recipe.
3. Update `docs/TASKS.md` S1.3 row + `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`
   S1.3 section with a "Latest evidence (2026-05-XX cycle N)"
   block.
4. Commit + push per cycle (`feedback_main_branch_no_pr_workflow`).

## Commit hygiene

- One commit per layer (nav fix, test fix, docs, skill update).
  Don't bundle.
- Push after each commit per
  `feedback_main_branch_no_pr_workflow`.
- Co-author tag exactly:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- No `--no-verify`, no `--amend`, per `e:/repos/CLAUDE.md` git
  safety protocol.

## Acceptance — "done" criteria

You are done when ALL of the following are true on prod-data
(`WWOW_DATA_DIR=D:/wwow-bot/prod-data`):

1. **All 23 `CrossroadsToUndercity_CriticalWalkLegs` cases pass**
   in one sweep. No `Failed`. No `unrun`.
2. **`OrgrimmarCorpseRun_LiveRetrieveRoute` 2/2 still green**.
3. **`RecordedTests.PathingTests` 135/0/0 still green**.
4. **PFS WaypointGeneration suite ≥ 76 unit tests still green**.
5. **NavMeshPhysicsValidator suite still green** (the validation
   harness from `project_pfs_overhaul_006_validation_harness_session`).
6. The 5 shipped layers in commits `ba563ff7` + `13f2fbd7` are
   unchanged except for parameter tuning IF that tuning is the
   right fix surface for one of the 3 remaining failures (Cycle 2
   may identify this).
7. Tile (40, 29) `0012940.mmtile` regenerated with per-tile config
   documented in `tools/MmapGen/offmesh.txt` (or wherever the
   per-tile config lives — verify file path).
8. Memory + skill + docs updated per requirements above.

If you can't close all 3 bake-side failures in this session —
ship what you can. Each one closed is a win. Document the rest as
next-cycle work, just like this handoff.

## Reference numbers (loop 17)

- Sweep wall-clock: ~52 min for 20 of 23 cases (avg 2.6 min/case).
- `flight_master_to_zeppelin_tower_full_route` (670y): 2m23s
  (with 120s resolver budget; passes cleanly).
- `exterior_incline_exact` (now passing): 5m42s.
- The 3 slowest passing cases: `city_live_vertical_replan_recovery`
  (4m), `zeppelin_tower_friction_recovery` (3m32s),
  `durotar_hillside_slope_recovery` (7m44s).

Good luck. — handed off 2026-05-16 after loop-17 5-layer
pathfinding fix.
