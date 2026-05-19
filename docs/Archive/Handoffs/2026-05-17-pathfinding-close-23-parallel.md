# Handoff: close the 4 remaining tile (40, 29) failures via three parallel surfaces

You are picking up a fresh Claude session for `e:/repos/Westworld
of Warcraft/` (the WWoW repo on `main`). The S1.3 pathfinding
sweep has held steady at **19 / 4 / 0** on prod-data across
loops 19-22. The 4 remaining failures are all on tile (40, 29)
`0012940.mmtile` and have been exhaustively diagnosed.

The user has authorized **three scope expansions** for this
session that previous loops did not have. Read this whole doc
before doing anything else.

## Mission

Close all 4 remaining `CrossroadsToUndercity_CriticalWalkLegs`
failures and reach **23 / 0 on prod-data** without regressing
any of the 19 currently-passing cases or the adjacent suites.

## Read first (in order)

1. This handoff (you're here).
2. `e:/repos/CLAUDE.md` (root monorepo rules, R1-R13).
3. `e:/repos/Westworld of Warcraft/CLAUDE.md` (pathfinding
   overhaul charter + Token-Efficient Tooling section).
4. `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` §S1.3 — read
   the full sequence of "Latest evidence" blocks from loop 17
   through loop 22. They are the truth about what was tried.
5. The prior handoff `docs/Plan/Handoffs/2026-05-17-pathfinding-cull-unblock.md`
   (loop 20's framing — most of its "hard constraints" still
   apply; this doc only relaxes specific ones).
6. Memory entries in `C:/Users/lrhod/.claude/projects/e--repos/memory/`,
   in this order:
   - `project_pfs_loop22_threshold_cascade` (most recent —
     definitive bake-ceiling finding)
   - `project_pfs_loop21_trap_diagnosis` (the diagnostic recipe
     + the per-failure root cause)
   - `project_pfs_loop20_cull_pipeline_unblock` (validator AV
     fix + cull-radii calibration findings)
   - `project_pfs_scenecache_groundz_orderfix` (loop 18 fixes)
   - `project_pfs_overhaul_006_brm_phase4_findings` (the
     canonical NavMeshPhysicsValidator + NavMeshTileEditor
     pipeline)
   - `project_pfs_overhaul_006_brm_singletile_negative` (THE
     historical precedent for bake-regen regression risk —
     respect it)
   - `feedback_pathfinding_freeze` (read AND note the
     freeze-scope relaxation below)
   - `project_mmapgen_offmesh_axis_swap` (off-mesh axis
     conventions — required reading before adding off-mesh
     entries)

Then run `git log --oneline -20` and `docker ps` to confirm
state.

## State at start

**Cumulative commits on `origin/main` (relevant to S1.3):**

| Commit | What it shipped |
|---|---|
| `ba563ff7` | nav: 5-layer pathfinding fix (loop 17) — DO NOT modify |
| `13f2fbd7` | tests: PathRouteAssertions LOS-walkable fallback + 60-min budget (loop 17) — DO NOT modify |
| `35f43d6a` | nav: SceneCache::GetGroundZ order-independence (loop 18) — DO NOT modify |
| `addf83af` | nav: refine GetGroundZ to closest-absolute (loop 18) — DO NOT modify |
| `e90db16d` | tests: probe-backed threshold 2.5→3.0 for OG underpass (loop 18) — see threshold note below |
| `0b2164d9` | tools(NavMeshPhysicsValidator): tolerate AV via legacy CSE policy (loop 20) |
| `c4415201` | tools(PathPhysicsProbe): add --dump-polyrefs flag (loop 21) |
| `5480ab2c` | test+docs: S1.3 loop-22 evidence + TestSessionTimeout 60→100m (loop 22) |

**Sweep tally (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`,
Release config, 100-min `TestSessionTimeout`):**

| Tally | Loop 17 | Loop 18-22 (steady) |
|---|---|---|
| Pass | 17/20 | **19/23** |
| Fail | 3/20 | **4/23** |
| Unrun | 3/23 | **0/23** |

**Tile (40, 29) state:** md5 `cc0d89c42d9abf4737ba52a369c5f3f7`
(matches baseline). Backups at
`/tmp/wwow-tile-backup/0012940.mmtile.{loop17,prod-loop17}`.

**The 4 remaining failures (do not be confused by changing
failure messages — read the diagnosis carefully):**

| Test | Loop-18 baseline fail | Loop-22c fail (threshold=4.0 disabled the gate) |
|---|---|---|
| `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery` | WP92 Z-delta @ `(1350.2,-4528.6,34.0)` | local physics break @ seg 25→26 `(1354.12,-4506.36,29.12) → (1354.57,-4510.32,30.99)` |
| `orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery` | WP72 Z-delta @ `(1347.3,-4540.6,35.8)` | LOS fail @ seg 1→2 `(1337.2,-4654.8,49.9) → (1342.5,-4653.7,48.8)` |
| `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery` | WP42 Z-delta @ `(1347.3,-4540.6,35.8)` | LOS fail @ seg 6→7 `(1336.9,-4636.9,24.8) → (1335.7,-4634.1,24.1)` |
| `orgrimmar_exterior_steep_incline_live_stall_recovery` | seg 178→179 slope=63° @ `(1348.0,-4537.7,35.4) → (1349.2,-4535.6,40.2)` | unchanged (slope-check, not Z-delta) |

The Z-delta and LOS/physics failures are nested layers of the
SAME bake-fidelity issue on tile (40, 29). Loop 22 proved this:
relaxing one assertion exposes the next.

## User-authorized scope expansions for this session

The user explicitly answered four sign-off questions on
2026-05-17 after loop 22. Carry these into your iteration:

1. **Native `Exports/Navigation/PathFinder.cpp` IS OK to
   modify.** The pathfinding freeze covers only managed
   `Services/PathfindingService/Repository/Navigation.cs`
   repair phases. The smooth-path generator may add a
   `polyref==0` corner-detection pass that re-routes through
   adjacent legitimate polys.
   - Constraint: full regression-test pass required (all PFS
     suites + adjacent). The change affects every smooth path
     in every tile; high blast radius.

2. **Bake regen on tile (40, 29) requires ZERO regression.**
   Per BRM precedent, single-tile regens historically
   destabilize adjacent passing cases. If any of the 19
   currently-passing CriticalWalkLegs or any adjacent-suite
   test breaks, revert the regen immediately.
   - This means: probe BEFORE regen, probe AFTER regen, run
     full 23-case sweep + adjacent suites between each
     config-knob iteration.

3. **Off-mesh connections in `tools/MmapGen/offmesh.txt` ARE
   fine.** Detour treats them as teleport edges; the BG/FG
   runtime resolves them as "jump" or short walks. The existing
   gangplank entries for OG zeppelin are precedent.
   - Constraint: respect the axis convention in memory
     `project_mmapgen_offmesh_axis_swap`. Endpoints must bind
     to valid navmesh polys.

4. **Run all three surfaces in parallel via subagents** with
   git-worktree isolation, then reconcile at the end. See
   "Execution model" below.

## Execution model — three parallel worktrees

Launch the three surface attempts as concurrent agents in
isolated git worktrees. Each agent operates on its own branch
in its own worktree directory; the lead (you) reconciles the
results.

### Surface A — Tile (40, 29) bake regen (worktree A)

**Agent**: `monorepo-worker` with `isolation: "worktree"`.

**Goal**: produce a tile (40, 29) mmtile that doesn't have the
phantom-poly stack at coord 2 `(1350.2,-4528.6)` and doesn't
have air-interpolated coverage gaps at coords 1+3.

**Approach**:
- Read `tools/MmapGen/config.json` for tile 4029 overrides.
  Current overrides include `cs=0.1`, `tileSize=213`,
  `agentMaxClimbTerrain=0.2`, `treatOobNeighborAsCliff=false`,
  `mixedAreaUsesTerrainClimb=true`, `walkableErosionRadius=0.2`,
  `maxVertsPerPoly=3`.
- Try one knob change at a time. After each:
  1. `MmapGen.exe --tile 40,29` (note: `--tile` arg order is
     `X,Y` per `feedback_promote_mmaps_tile_arg_xy`, but the
     output filename is `<map><Y><X>.mmtile` so `--tile 40,29`
     writes `0012940.mmtile`)
  2. Copy regenerated tile to `D:/wwow-bot/prod-data/mmaps/`
  3. `docker restart wwow-pathfinding`
  4. Probe the failure coords via `PathPhysicsProbe
     --dump-polyrefs` to verify the phantom stack changed
  5. Run full 23-case sweep — ABORT and revert if any
     previously-passing test fails
- Candidate knobs NOT yet tried (per loop-18 memo's "Probable
  fix surfaces (untried)"):
  - `filterLedgeSpans` tuning (phantom polys may be
    `rcFilterWalkableLowHeightSpans` errors)
  - `walkableHeight` clearance tuning
  - `walkableErosionRadius` between 0.2 and 0.5
- **HARD STOP**: if you find yourself attempting BRM-style
  config sweeps that mutate >1 knob at once, you're
  off-track. Single-knob deltas only.

### Surface B — Native PathFinder.cpp air-interp detection (worktree B)

**Agent**: `monorepo-worker` with `isolation: "worktree"`.

**Goal**: modify the smooth-path generator in
`Exports/Navigation/PathFinder.cpp` to detect smooth-path
corners with `polyref==0` (interpolated midpoints in air) and
either re-route them or annotate them so the test's assertions
can recognize and tolerate them.

**Approach**:
- Read `Exports/Navigation/PathFinder.cpp` — find the smooth-
  path / stringPulling implementation that produces the corner
  sequence.
- The probe data (in
  `project_pfs_loop21_trap_diagnosis`) shows tower_underpass
  smooth-path has corners with polyref=0 at indices 25, 32, 33,
  34, 39, 41, 48-51. These are linearly-interpolated midpoints
  between anchor corners that are themselves on real polys.
- Options to consider:
  - At smooth-path emission time, skip corners whose midpoint
    is not on any walkable poly (`findNearestPoly` at the
    interpolated coord returns 0).
  - Or: snap interpolated corner Z to the nearest legitimate
    poly's surfaceZ within an expanded search extent (e.g.
    8y instead of 1.8y).
- Build the native DLL via `MSBuild Exports/Navigation/Navigation.vcxproj
  -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145`
  (per the WWoW CLAUDE.md "Always kill WoW.exe before building"
  rule — there should be none running here but check).
- After build:
  1. Run full PathfindingService.Tests + Navigation.Physics.Tests
     + RecordedTests.PathingTests + adjacent suites
  2. Compare with loop-18 baseline trx (19/4/0 + adjacent
     suites green per loop-18 memo)
- **HARD STOP**: if any test in
  `Navigation.Physics.Tests.OgZeppelinCliffFallParityTests`
  regresses (4/4 there is the most sensitive gate), abort.
  Those tests depend on the EXACT current smooth-path
  behavior.

### Surface C — Off-mesh connections (worktree C)

**Agent**: `monorepo-worker` with `isolation: "worktree"`.

**Goal**: add off-mesh entries in `tools/MmapGen/offmesh.txt`
that route the failing tests' start coords directly to
near-boarding-point coords, bypassing the OG harbor phantom
region.

**Approach**:
- Read current `tools/MmapGen/offmesh.txt`. Existing entries
  for tile (40, 29) target the BoardingPosition
  `(1320.14, -4653.16, 53.89)`.
- The 4 failing tests' start coords:
  - `tower_underpass`: `(1357.2, -4516.2, 32.0)`
  - `bridge_side`: `(1337.2, -4654.8, 49.8)`
  - `tower_base_live_vertical`: `(1342.4, -4652.1, 24.6)`
  - `exterior_steep_incline`: `(1381.0, -4380.9, 26.0)`
- Add discrete off-mesh entries connecting each start (or a
  point along the natural walk path) to a known-good waypoint
  near the boarding point. Use `bidir` direction only when
  symmetric is meaningful.
- Endpoint binding rule per `project_mmapgen_offmesh_axis_swap`:
  both endpoints must `findNearestPoly` to a real navmesh poly
  within a 4.0y XY extent. Use `PathPhysicsProbe --dump-polyrefs`
  to verify each endpoint binds before regen.
- Regenerate tile (40, 29) via `MmapGen.exe --tile 40,29`
  (single-tile regen with new offmesh; same args as Surface A).
- Probe each test's smooth path via `PathPhysicsProbe
  --detour-resolve --smooth --dump-polyrefs` to verify the
  off-mesh edge is used.
- Run full 23-case sweep + adjacent suites.
- **HARD STOP**: if the off-mesh edge isn't used by the
  failing tests (the smooth path still routes through the
  phantom region), the connection is mis-bound. Don't add
  more off-mesh entries — re-probe endpoint binding first.

## Reconciliation (you, the lead)

When all three surfaces complete (or hit their hard stops):

1. Compare results across the three worktrees:
   - Tally on CriticalWalkLegs sweep (target: 23/0)
   - Adjacent suite tallies
   - Any new regression
2. The "winning" surface is the one that produces a clean
   23/0 with no regression. If multiple win, prefer:
   - Off-mesh (lowest scope, most surgical) over
   - Bake regen (medium scope) over
   - Native PF.cpp (highest blast radius)
3. Cherry-pick / port the winning surface's commits to main.
4. Tear down the losing worktrees.
5. Document the reconciliation in
   `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` §S1.3 as a
   "Loop 23 evidence" block.
6. Write a `project_pfs_loop23_close_23_zero` memory entry
   with the full iteration log and which surface won.

If NO surface wins:
- Revert all worktrees.
- Document the failure: which knobs were tried, why they
  didn't close the failures, and what's left as multi-cycle
  next work.
- The user is willing to accept 19/4 as the durable resting
  state if all three surfaces genuinely fail (the cull-pipeline
  unblock from loop 20 and the diagnostic from loop 21 are
  permanent wins regardless).

## Hard constraints (unchanged from prior handoffs)

- **DO NOT modify** the 5 shipped resolver-side layers
  (commits `ba563ff7` + `13f2fbd7`).
- **DO NOT modify** the loop-18 `SceneCache::GetGroundZ`
  closest-absolute semantic (commit `addf83af`). The split
  with `GetWalkableGroundZ` is load-bearing for the
  `OgZeppelinCliffFallParityTests` 4/4.
- **DO NOT add new managed repair phases** to
  `Services/PathfindingService/Repository/Navigation.cs`. The
  managed freeze still holds. Only the native PF.cpp is
  unlocked.
- **Tile (40, 29) backups** are at
  `/tmp/wwow-tile-backup/0012940.mmtile.{loop17,prod-loop17}`.
  Before any tile-mutating attempt, snapshot the current tile
  as `0012940.mmtile.loop23-<surface>-before`.
- **Always restart `wwow-pathfinding`** after any tile change
  (per `feedback_pathfinding_docker_reload`).
- **Always run with `WWOW_DATA_DIR=D:/wwow-bot/prod-data`** and
  the `--settings Tests/PathfindingService.Tests/test.runsettings`
  (the 100-min `TestSessionTimeout` is needed for the slow
  cases).
- **Test-class serialization** is shipped (`eeef7907`) — do not
  break the `[CollectionDefinition("Navigation",
  DisableParallelization=true)]` pattern.
- **Main-branch no-PR workflow** per
  `feedback_main_branch_no_pr_workflow`. Cherry-pick to main
  after reconciliation; commits straight to `origin/main`.
- **Single Claude session for the lead** (subagents are
  separate Claude instances via the Agent tool).

## Don't repeat (lessons from loops 19-22)

- **Don't coord-cull at WP Z** — loop 21 + loop 22a proved
  this hits a phantom stack >22 polys deep at coord 2 and
  doesn't reach the legitimate ground. The trap polyref
  returned by zero-radius probe is a sibling, not the trap.
- **Don't relax `maxResolvedWaypointZDelta` above 3.0** —
  loop 22b/c proved this cascades to LOS / local-physics
  failures at the start of each route. Effectively just hides
  the Z-delta assertion without closing tests.
- **Don't attempt the full BRM-style multi-knob bake sweep**
  per `project_pfs_overhaul_006_brm_singletile_negative` — it
  destabilized BRM passing cases. Single-knob deltas only on
  Surface A.

## Tooling already available

- `tools/NavMeshPhysicsValidator.exe` — coord/sample cull
  driver with AV-tolerance (commit `0b2164d9`).
- `tools/PathPhysicsProbe.exe --dump-polyrefs` — per-corner
  polyref dumper (commit `c4415201`). Use the recipe in
  `project_pfs_loop21_trap_diagnosis` for triage.
- `tools/MmapGen/build/NavMeshTileEditor.exe` — surgical
  polyref cull. CLI: `--cull-polys <ref1,ref2,...>`
  or `--cull-polys-file <path>` or `--cull-polyidx-range
  MIN,MAX --dry-run`.
- `tools/scripts/validate-bake.ps1` — orchestrator for the
  validator + tile-cull pipeline.

## Acceptance — "done" criteria

ALL of the following on prod-data
(`WWOW_DATA_DIR=D:/wwow-bot/prod-data`, Release configuration,
100-min `TestSessionTimeout`):

1. **All 23 `CrossroadsToUndercity_CriticalWalkLegs` cases
   pass** in one sweep.
2. **`OrgrimmarCorpseRun_LiveRetrieveRoute` 2/2** still green.
3. **`RecordedTests.PathingTests` 135/0/0** still green.
4. **PFS `WaypointGeneration` ≥ 39 unit tests** still green.
5. **`Navigation.Physics.Tests` ≥ 152/0/1**, with
   `OgZeppelinCliffFallParityTests` 4/4 specifically green.
6. The winning surface is documented in Plan/02 §S1.3 loop-23
   evidence block + a new memory entry.
7. Any losing worktrees are torn down cleanly.

## Commit hygiene

- One commit per layer (per worktree). Don't bundle.
- After reconciliation, cherry-pick winning surface's commits
  to `main` and push.
- Co-author tag exactly:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- No `--no-verify`, no `--amend`, per `e:/repos/CLAUDE.md` git
  safety protocol.

Good luck. The user explicitly said: "Use the skills present
and don't repeat yourself. You can do it. I believe in you."

— handed off 2026-05-17 after loop 22 with user sign-off on
the three scope expansions.
