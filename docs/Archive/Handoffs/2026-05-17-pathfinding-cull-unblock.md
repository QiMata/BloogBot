# Handoff: unblock the bake-validation cull pipeline so the 4 phantom-poly closures can finally land

You are picking up a fresh Claude session for `e:/repos/Westworld
of Warcraft/` (the WWoW repo on `main`). The S1.3 pathfinding
sweep is at **19/4 + 0 unrun** on prod-data — the best result
yet. Loop 17 shipped the 5-layer resolver-side fix; loop 18
shipped the `SceneCache::GetGroundZ` order-independence fix +
probe-backed threshold relax + closest-absolute semantic
refinement; loop 19 attempted Cycle 3 surgical cull and
**discovered a tool blocker** that prevented any further
progress.

Your job: **unblock the bake-validation cull pipeline**, then
close the 4 remaining failures via surgical poly cull on tile
(40, 29) `0012940.mmtile`. Each one closed is a win — full
closure of all 4 lands the 23/0 acceptance gate.

## Read first (in order)

1. This handoff.
2. `e:/repos/CLAUDE.md` (R1-R13).
3. `e:/repos/Westworld of Warcraft/CLAUDE.md` — pathfinding
   overhaul charter + Token-Efficient Tooling section.
4. `e:/repos/Westworld of Warcraft/docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`
   §S1.3 — the "Latest evidence (2026-05-16 loop 18 — bake-close
   session)" block has the full per-test trail + cumulative
   commit chain.
5. The prior handoff `docs/Plan/Handoffs/2026-05-16-pathfinding-bake-close.md`
   for the full Cycle 1-4 cycle structure (read its
   "Hard constraints", "Don't repeat", and "Skills to use"
   sections — they all still apply here).
6. Memory entries in `C:/Users/lrhod/.claude/projects/e--repos/memory/`:
   - `project_pfs_loop19_cull_pipeline_blocker` (the blocker —
     READ FIRST, has next-cycle unblock surfaces listed)
   - `project_pfs_scenecache_groundz_orderfix` (loop 18 fix
     chain + the 4 failure classifications)
   - `project_pfs_overhaul_006_brm_phase4_findings` (canonical
     NavMeshPhysicsValidator + NavMeshTileEditor pipeline)
   - `project_pfs_exterior_incline_los_smooth_expand` (the 5
     shipped resolver-side layers — DO NOT touch them)
   - `feedback_promote_mmaps_tile_arg_xy` (MmapGen `-Tiles X,Y`
     vs filename `<map><Y><X>` convention)
   - `project_pfs_overhaul_006_config_key_inversion` (per-tile
     config key convention + cs + tileSize coupling rule)
   - `feedback_pathfinding_docker_reload` (restart
     wwow-pathfinding after every tile change)
7. The `mmo-physics-pathing-probe` and `mmo-pathfinding` skills.

Then: run `git log --oneline -15` and `docker ps` to confirm
state. Containers expected up: `wow-mangosd`, `wow-realmd`,
`maria-db`, `wwow-pathfinding`, `wwow-scene-data`.

## State at start of this session

**Cumulative commits on origin/main (loop 17 + 18 + 19):**

| Commit | Description |
|---|---|
| `ba563ff7` | nav: 5-layer pathfinding fix |
| `13f2fbd7` | tests: PathRouteAssertions LOS-walkable fallback + 60-min budget |
| `7a692940` | docs: S1.3 + Plan/02 loop-17 evidence |
| `8707c475` | docs: handoff for tile (40, 29) bake-close session |
| `35f43d6a` | nav: SceneCache::GetGroundZ order-independence fix |
| `e90db16d` | tests: probe-backed maxResolvedWaypointZDelta 2.5 → 3.0 for OG underpass |
| `addf83af` | nav: refine GetGroundZ semantic to closest-absolute |
| `f5d91761` | docs: S1.3 + Plan/02 loop-18 evidence |
| `4506032e` | tasks: S1.3 status full-coverage-green for loop 18 (19/4/0) |

**Sweep tally (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`):**

| Tally | Loop 3 baseline | Loop 17 | Loop 18 | Loop 19 (this start) |
|---|---|---|---|---|
| Pass | 11/13 | 17/20 | **19/23** | 19/23 |
| Fail | 2/13 | 3/20 | 4/23 | 4/23 |
| Unrun (budget) | 10/23 | 3/23 | 0/23 | 0/23 |

**The 4 remaining failures (all tile (40, 29) `0012940.mmtile`
bake-side phantom polys):**

| Test | Phantom poly coord | Probe-classified shape |
|---|---|---|
| `orgrimmar_exterior_steep_incline_live_stall_recovery` | WP178 `(1348.0,-4537.7,35.4)` vs ADT=49.31 | 13.9y underground phantom poly |
| `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery` | WP92 `(1350.2,-4528.6,34.0)` supportZ=37.245 | 3.25y under terrain, densifier cascade |
| `orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery` | WP72 `(1347.3,-4540.6,35.8)` supportZ=38.93 | 3.16y under terrain, same defect class |
| `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery` | WP42 `(1347.3,-4540.6,35.8)` supportZ=38.93 | Same (1347.3,-4540.6,35.8) defect |

## The blocker (per `project_pfs_loop19_cull_pipeline_blocker`)

`NavMeshPhysicsValidator.exe` (the canonical cull pipeline from
[[project_pfs_overhaul_006_brm_phase4_findings]]) consistently
crashes with `System.AccessViolationException` inside
`Navigation.Physics.Tests.NavigationInterop.GetPolyAtCoord(...)`
when called against tile (40, 29) on prod-data — every cull-coord
invocation crashes after `InitializeMapLoader` + `LoadMapTile`
succeed, but before the `--cull-coord ... → N unique polys`
summary lines emit.

The per-call `try { GetPolyAtCoord(...) } catch { /* tolerate */ }`
at `tools/NavMeshPhysicsValidator/Program.cs:244-249` does NOT
catch the AV — classic `HandleProcessCorruptedStateExceptions`
situation where AV tears down the process despite the
try/catch.

NOT a signature mismatch — both
`Tests/PathfindingService.Tests/NavigationInterop.cs` and
`Tests/Navigation.Physics.Tests/NavigationInterop.cs` declare
the right 5-out signature matching the C export at
`Exports/Navigation/DllMain.cpp:813`. The
PathfindingService.Tests path through `NavigationFixture` does
NOT crash on the same call — so it's an init-order issue
specific to the validator's standalone `Main` entry.

## Pick a path through (cheapest first)

### Cycle 1 — Unblock the cull pipeline (do this first)

Three viable surfaces, in increasing scope:

**A. Add `[HandleProcessCorruptedStateExceptions]` + `[SecurityCritical]`**
to the `Main` method (or the cull-coord enumeration helper) in
`tools/NavMeshPhysicsValidator/Program.cs`. May or may not work
in .NET 8 — these attributes are deprecated; `runtime` legacy
behavior may need to be opted into via `<LegacyCorruptedStateExceptionsPolicy>true</LegacyCorruptedStateExceptionsPolicy>`
in the csproj. Try this first — smallest diff.

**B. Extend `tools/PathPhysicsProbe`** with a `--dump-polyrefs`
flag that emits the polyref+polyType at each Detour-resolved
corner (mirrors `GetPolyAtCoord` but uses the
PathfindingService.Tests interop path that DOESN'T crash). Then
the workflow becomes:
```
PathPhysicsProbe --map 1 --start <wp> --end <wp> --load-adt --dump-polyrefs
  → grep polyref → NavMeshTileEditor 0012940.mmtile --cull-polys <ref> --dry-run
  → verify dry-run output → drop --dry-run, regen, restart docker
```
Mid-complexity (~50 lines in `tools/PathPhysicsProbe/Program.cs`)
but very robust — sidesteps the validator entirely.

**C. Isolate the GetPolyAtCoord init-order bug.** Build a
minimal Program.cs that ONLY does
`InitializeMapLoader(D:/wwow-bot/prod-data/maps)` →
`LoadMapTile(1, 40, 29)` → `GetPolyAtCoord(1, (1347.3,-4540.6,35.8), 2, 1.8, ...)`
and observe whether it crashes there too. If yes, you've found
the root cause to fix in `Exports/Navigation/DllMain.cpp:813`
(`GetPolyAtCoord`) directly. Highest scope but biggest payoff
since it fixes the validator AND surfaces a real native bug.

**Recommended order: A → B → C.** A is the smallest possible
fix; if it works, stop there. If A doesn't apply on .NET 8, fall
back to B (most robust). C is investigative — pursue if A and B
both fail or you want the deeper fix anyway.

**Acceptance for Cycle 1:** be able to obtain a non-zero polyref
for each of the 4 trap coords above without a process crash, AND
feed that polyref into `tools/MmapGen/build/NavMeshTileEditor.exe
0012940.mmtile --cull-polys <ref> --dry-run` and get a
`range-cull polyIdx=<N> areaWas=1 flagsWas=0x1` line. Document
the polyref list per coord before doing anything destructive.

### Cycle 2 — Surgical cull tile (40, 29) at the 4 trap coords

Once the pipeline works:

```powershell
# Backup is already in place from loop 19:
ls /tmp/wwow-tile-backup/0012940.mmtile.{loop17,prod-loop17}

# Drive the cull with Z-stack + XY-stack to capture phantom poly siblings
# (per project_pfs_overhaul_006_brm_phase4_findings the WMO-interior
# traps stack multiple walkable polys at slightly different Z values):
$env:WWOW_DATA_DIR='D:/wwow-bot/prod-data'
./tools/scripts/validate-bake.ps1 `
  -Tiles '1:40,29' `
  -DataDir 'D:/wwow-bot/prod-data' `
  -Samples 30 `
  -CullCoords '1347.3,-4540.6,35.8;1350.2,-4528.6,34.0;1348.0,-4537.7,35.4;1349.2,-4535.6,40.2' `
  -CullCoordZRadius 2.0 `
  -CullCoordXyRadius 0.5 `
  -Cull

# REQUIRED after any tile change per feedback_pathfinding_docker_reload:
docker restart wwow-pathfinding
```

Then run the 4 failing tests in isolation, then the full
CrossroadsToUndercity sweep + regression suites.

**Acceptance for Cycle 2:** each of the 4 failures closes
WITHOUT regressing any of the 19 currently-passing cases.
Tile (40, 29) has **59,749 polygons** — random or range-based
cull would catastrophically break passing cases, so this MUST
be coord-driven only.

### Cycle 3 — Adjacent suite regression sweep

After all 23 PFS cases pass, verify:

- `OrgrimmarCorpseRun_LiveRetrieveRoute` still 2/2.
- `RecordedTests.PathingTests` still 135/0/0.
- `Navigation.Physics.Tests` still 152/0/1 (especially
  `OgZeppelinCliffFallParityTests` 4/4 — those are sensitive to
  GroundZ/walkable-probe semantic changes).
- PFS `WaypointGeneration` ≥ 39/0/3 green.

## Hard constraints (unchanged from prior handoffs)

- **Pathfinding freeze (2026-05-06).** Mesh fixes only in
  `tools/MmapGen/` or via `NavMeshTileEditor` cull. Do NOT
  add new repair phases to `Services/PathfindingService/Repository/Navigation.cs`.
  The 5 shipped resolver-side layers are the last allowed.
- **Do NOT touch the loop-18 `SceneCache::GetGroundZ` /
  `GetWalkableGroundZ` split** unless you find a real
  correctness bug. The closest-absolute fix shipped in
  `addf83af` is load-bearing for the 19 passing cases; the
  legacy semantic on `GetWalkableGroundZ` is load-bearing for
  `OgZeppelinCliffFallParityTests`.
- **R13 ordering:** scene-data → FG/BG physics parity →
  pathfinding. Probe each failing segment via
  `tools/PathPhysicsProbe` BEFORE touching the bake.
- **Don't lower `walkableSlopeAngle` / `walkableClimb`** from
  harvested client values (per `feedback_pathfinding_anti_patterns`).
- **Always restart `wwow-pathfinding`** after any tile change.
- **Test-class serialization** is shipped (`eeef7907`) — do not
  break the `[CollectionDefinition("Navigation",
  DisableParallelization=true)]` pattern.
- **Main-branch no-PR workflow** per
  `feedback_main_branch_no_pr_workflow`. Commits straight to
  `origin/main`.
- **Single Claude session, auto-compaction** — keep going.

## Don't repeat (lessons from loops 17/18/19)

- Don't disable any of the 5 shipped resolver-side layers
  (commits `ba563ff7` + `13f2fbd7`) or the closest-absolute
  `GetGroundZ` semantic (`addf83af`). They close real bugs.
- Don't drop the resolver budget below 120s or the test
  session timeout below 60min.
- Don't try to fix `exterior_steep_incline` resolver-side.
  Layers 1-3 successfully re-expand the corridor but the output
  corners include unwalkable sub-pairs through an underground
  phantom poly. Bake-side cull only.
- Don't attempt **full tile regen** unless surgical cull has
  been exhausted. Tile (40, 29) regen with config tweaks has
  high regression risk against the 19 passing cases (BRM
  precedent in `project_pfs_overhaul_006_brm_singletile_negative`
  documents multiple destructive attempts).
- Don't `--cull-polyidx-range` blindly. The tile has 59,749
  polygons; random range cull = total breakage.

## Commit hygiene

- One commit per layer (tool unblock, cull, doc update). Don't
  bundle.
- Push after each commit per
  `feedback_main_branch_no_pr_workflow`.
- Co-author tag exactly:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- No `--no-verify`, no `--amend`, per `e:/repos/CLAUDE.md` git
  safety protocol.

## Acceptance — "done" criteria

You are done when ALL of the following are true on prod-data
(`WWOW_DATA_DIR=D:/wwow-bot/prod-data`):

1. **All 23 `CrossroadsToUndercity_CriticalWalkLegs` cases
   pass** in one sweep.
2. **`OrgrimmarCorpseRun_LiveRetrieveRoute` 2/2 still green**.
3. **`RecordedTests.PathingTests` 135/0/0 still green**.
4. **PFS `WaypointGeneration` ≥ 39 unit tests still green**.
5. **`Navigation.Physics.Tests` ≥ 152/0/1, with
   `OgZeppelinCliffFallParityTests` 4/4 specifically green**.
6. Tile (40, 29) `0012940.mmtile` modified via surgical cull
   only (NavMeshTileEditor `--cull-polys` against probe-derived
   polyrefs); per-tile config in `tools/MmapGen/config.json`
   left unchanged unless a re-bake is explicitly justified by
   probe data.
7. Memory + skill + docs updated; loop-20 evidence block added
   to `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` §S1.3.

If you can't close all 4 bake-side failures in this session —
ship what you can. Each one closed is a win. Document the rest
as next-cycle work, just like this handoff.

## Reference numbers (loop 18 final)

- Full sweep wall-clock with closest-absolute semantic: 1h 5m
  for 23 cases (avg 2.8 min/case).
- Test session timeout 90min was sufficient; 100min budget gave
  ~5min headroom.
- 4 failure execution times: `exterior_steep_incline` 1m52s ·
  `tower_base_live_vertical` 7s · `bridge_side` 5m11s ·
  `tower_underpass` 10m6s.

Good luck. — handed off 2026-05-17 after loop-19 cull-pipeline
blocker discovery.
