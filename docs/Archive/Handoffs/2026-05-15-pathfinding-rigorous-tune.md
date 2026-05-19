# Handoff: rigorously diagnose + fix 1.12.1 WoW pathfinding so all live tests pass

You are picking up a fresh Claude session for `e:/repos/Westworld of Warcraft/` (the
WWoW repo on `main`, last commit `e406538c`). The pathfinding pipeline ships clean
from the previous session — Phase 1/2/3 commits validated, four BG frame slots
landed (S1.15/S1.17/S1.19) — but **17 live tests still fail across the
pathfinding surface**. Your job is to drive those to green for 1.12.1 WoW data,
using rigorous Codex-backed research and the established pathfinding skills.

## Background — what's broken

Three failure clusters from the 2026-05-15 validation run:

### Cluster A: PFS in-process suite — 12 failures, all OG city corridors

All in `Tests/PathfindingService.Tests/`:

- **10 of 12** in `LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes`,
  parameterized by route label. Failing labels (extract from
  `Tests/PathfindingService.Tests/TestResults/pfs-validation-2026-05-15.trx`):
  - `orgrimmar_city_live_vertical_replan_recovery`
  - `orgrimmar_city_hallway_live_wall_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery` (+ `_corridor` variant)
  - `orgrimmar_city_support_stall_exact_live_recovery`
  - `orgrimmar_city_support_stall_screenshot_recovery`
  - `orgrimmar_city_to_zeppelin_tower_lower_approach`
  - `orgrimmar_flight_master_to_zeppelin_tower_full_route`
  - `orgrimmar_flight_master_tower_descent_live_stall_recovery`
  - `orgrimmar_flight_master_tower_hover_stall_exact_live_recovery`
- **2 of 12** in `PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_*`
  (`ReroutesAroundBlockedDirectLine` + `StraightRequestCompletesWithinBudget`).

**Failure shape**: Detour returns a 4-corner partial corridor (e.g.
`(1545.0,-4434.5,11.1) → (1545.0,-4434.5,11.0) → (1535.3,-4437.9,13.9) → (1320.1,-4653.2,53.9)`
— the last segment is 313y straight). The path resolver hits the
`BuildUsablePathResult bypass pipeline reason=corridor-fallback kind=CorridorFirst`
branch (added in commit `f343ecbf` to make the 500y Durotar route pass) and skips
smooth-path expansion. The test asserts segments ≤ `maxSegmentLength: 8`; bare
corridor segments come in at 8.5y / 10.3y / 14.3y.

**Pre-existing diagnosis** (Explore agent in prior session):
- `f343ecbf` ("nav+tests: Durotar 500y route tests PASS — corridor-fallback bypass +
  threshold relaxation") already had to relax `maxSegmentLength: 200 → 300` for
  the Durotar route. The 12 OG-city tests use `maxSegmentLength: 8` which
  `f343ecbf` could not relax without breaking the `CriticalWalkLegs` contract.
- Phase 1 commit `3b296ea4` is pure memoization — no behavioral change.
- Phase 2 commit `59d9647b` is a SceneCache union-grow tweak in
  `Exports/Navigation/SceneQuery.cpp` — no path-shape coupling.
- Memory entry `project_pfs_overhaul_brm_phase1_recon` flags that
  `D:/MaNGOS/data` and `D:/wwow-bot/prod-data` are **different bakes with
  different polyrefs at the same XY**. The 12 failures may be partially
  prod-data-bake-specific.

### Cluster B: BotRunner LiveValidation — 5 failures, mixed pathfinding/physics/transport

All in `Tests/BotRunner.Tests/LiveValidation/`:

| Test | Failure |
|---|---|
| `MovementSpeedTests.BG_Durotar_WindingPathSpeed` | BG bot moves at 2.24 y/s, min required 3.5 y/s |
| `LongPathingTests.OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` | Bot ends at OG city `(1320.1,-4653.2,53.9)`, 4902y from Undercity, transport=0x0 — never boarded |
| `TravelPlannerTests.TravelTo_ShortWalk_WithinOrgrimmar` | Bot doesn't move on short `TRAVEL_TO` action within 15s |
| `LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin` | FG bot doesn't settle at Crossroads staging |
| `MovementParityTests.TransportRide_FgBgParity` | BG bot doesn't settle at `.tele name … undercity`; pos=`(1584.1,242.0,-52.15)` map=0 |

These are game-integration scenarios — pathfinding *and* physics *and* transport
boarding *and* movement-speed parity. Several end at the same OG city stall coord
as Cluster A, which is suggestive.

### Cluster C: known-skipped diagnostic dumps (acceptable)

5 skips in TRX (`BrmAscentReconPolyrefDump.DumpReconPolyrefs`,
`DumpZJumpZone`, `BrmDungeonRouteDiagnostic.FlameCrestToUbrsCorridor_AvoidsSteepSlopePolys`,
`MmapMeshQualityTests.FlameCrestStall_HasNoTallSteepSlopeWallsNearStall`,
`FlameCrestStall_HasNoUnreasonableGroundBridgePolygons`). Pre-existing per
handoff — not in scope unless they signal something.

## Hard constraints — read these before designing any fix

1. **Pathfinding freeze (since 2026-05-06)** —
   [`docs/physics/PATHFINDING_OVERHAUL.md`](../physics/PATHFINDING_OVERHAUL.md)
   and memory `feedback_pathfinding_freeze`. Mesh fixes only in `tools/MmapGen/`.
   Do **not** add new repair phases in
   `Services/PathfindingService/Repository/Navigation.cs` (the 5,600-line legacy
   pipeline). Do **not** add per-spot constants, route-pack seeds, or jump-up
   shims.

2. **Validation order — R13 in `e:/repos/CLAUDE.md`**:
   `scene-data → FG/BG physics parity → pathfinding`. The validation harness's
   `FG_BG_PARITY_BREAK` Kind is the canary. If it fires on a checkpoint where
   SceneData delivers triangles, fix physics, NOT the pathfinder. Many of the
   failing tests above end at coords that match prior physics-parity gaps
   (`project_pfs_overhaul_006_round4_iter5_VICTORY` = OG cliff-fall fix);
   suspect physics first.

3. **Game data is 1.12.1 vanilla** — reference for what's correct lives in
   `D:/MaNGOS/data/maps/`, `D:/MaNGOS/data/vmaps/`, the LandSandBoat-equivalent
   server source. Avoid extrapolating from TBC/WotLK behaviors. WoW.exe binary
   parity (the running 1.12.1 client) is **the** rule for physics/movement —
   `e:/repos/Westworld of Warcraft/CLAUDE.md` rule 4.

4. **No `.gm on` in tests** — corrupts UnitReaction bits. If you add new live
   coverage, follow the `LiveBotFixture.EnsureCleanSlateAsync` pattern.

5. **Slot ownership** — open the work as new slots in
   `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` (S1.3 is the existing PFS
   stability slot — currently `blocked (red baseline)` per
   `docs/TASKS.md:74`). Update its status / owned paths if you take it on.

6. **Single Claude session, auto-compaction** — per
   `e:/repos/Westworld of Warcraft/CLAUDE.md` "Session Continuity" section. Do
   not start a new session to retry; commit + push frequently.

7. **Main-branch no-PR workflow** — per memory `feedback_main_branch_no_pr_workflow`.
   Commits go straight to `origin/main`.

## Skills you must use

These are the WWoW-monorepo skills exposed in this environment. Invoke them via
the `Skill` tool. **Use them — don't hand-roll equivalents.**

- **`mmo-pathfinding`** — Recast/Detour navmesh generation, MmapGen, route
  queries, off-mesh links. Primary skill for any bake-side fix.
- **`mmo-physics-pathing-probe`** — Drives `tools/PathPhysicsProbe/` to classify
  every segment of a path against the runtime physics engine and identify the
  FIRST segment that disagrees with the bake. **Use this first** to localize
  whether each Cluster A failure is a bake-mesh-vs-runtime-physics gap.
- **`mmo-movement-diagnostics`** — FG screenshot + snapshot/packet trace
  pairing. Use when the failure surface (e.g., "bot doesn't reach end") and the
  root cause are several layers apart. Cluster B's
  `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` is the canonical use case.
- **`mmo-fg-client-re`** — FG client reverse engineering for memory
  structures, object managers, packet capture. Use when you need to verify
  what the original 1.12.1 client actually does at a given coord.
- **`mmo-bg-client-parity`** — BackgroundBotRunner deterministic client
  parity. Use for the BG-side movement speed and transport-boarding failures
  in Cluster B.
- **`mmo-scene-data-service`** — local geometry / collision tile / 3x3 grid
  slice service. Relevant when physics needs scene data.
- **`mmo-live-fixtures`** — live integration fixtures and screenshot
  diagnostics. Use when adding new test coverage to lock in fixes.
- **`mmo-statemanager-orchestration`** — only relevant if you discover the
  failures are coordination-side (unlikely but possible for the transport
  failures).

Plus **`codex:rescue`** for Codex-driven investigation passes. Use it for:
- Reading `Exports/Navigation/PathFinder.cpp` (large C++) — pipe through Codex
  for "find the entry point that returns CorridorFirst kind" type questions.
- Analyzing physics replay frame output at scale.
- Cross-referencing FG packet captures against `WoWSharpClient.Tests` parity
  recordings.
- The WWoW CLAUDE.md "Token-Efficient Tooling" section already mandates
  `codex "..."` for log/large-file work — follow that.

## What "works in all cases" means — acceptance criteria

You are **done** when all three are true:

1. **Cluster A green**: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj -c Release` against `WWOW_DATA_DIR='D:/wwow-bot/prod-data'` reports **0 failures** in the
   parameterized `CrossroadsToUndercity_CriticalWalkLegs` + `OrgrimmarCorpseRun_LiveRetrieveRoute`
   test methods. The 5 pre-existing dump skips (`BrmAscentReconPolyrefDump`,
   `MmapMeshQualityTests.FlameCrestStall_*`, `BrmDungeonRouteDiagnostic.FlameCrest…`)
   may remain skipped — that's accepted.

2. **Cluster B green**: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release --filter "FullyQualifiedName~LiveValidation&(FullyQualifiedName~Pathing|FullyQualifiedName~Travel|FullyQualifiedName~Movement|FullyQualifiedName~CorpseRun)"`
   reports **0 failures** of the 21 executed tests. The 17 env-gated skips
   (`WWOW_OG_RAMP_CLIMB_TEST=1`, etc.) may remain.

3. **No regression in adjacent suites**: re-run also keeps green:
   - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj -c Release` Docker tests (`WWOW_RUN_DOCKER_VALIDATION=1`) — was 3/0/0
   - `dotnet test Tests/RecordedTests.PathingTests.Tests/...` — was 135/0/0
   - `dotnet test Tests/Navigation.Physics.Tests/...` per the "Test Baseline" line in `docs/TASKS.md`
   - `dotnet test Tests/WoWSharpClient.Tests/...` — was 1623/0/1 per memory

## Suggested investigation order

This is how I'd approach it from a cold start. Adjust if the data tells you
otherwise — use Codex liberally to read traces and confirm.

### Phase 0 — orient & reproduce (≤ 30 min)

1. Read these memory entries before touching code:
   - `project_wwow_validation_2026_05_15` — last session's results
   - `project_pfs_overhaul_brm_phase4_findings` — NavMeshPhysicsValidator
     + NavMeshTileEditor pipeline
   - `project_pfs_overhaul_006_round4_iter5_VICTORY` — established physics
     parity rules (Prime is airborne authority; gates respect Prime; ground
     probes filter walkability)
   - `feedback_fgbg_physics_parity_priority` — R13 ordering rule
   - `feedback_pathfinding_freeze` + `feedback_pathfinding_anti_patterns` —
     what NOT to do
   - `project_pfs_calculatepath_hang_durotar` — context for the
     corridor-fallback bypass
2. Re-run the failing PFS tests in isolation to confirm the failure shape is
   the same as the TRX captured by the prior session. Use a tighter filter
   to skip the unaffected 90+ tests:
   `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj -c Release --filter "FullyQualifiedName~CrossroadsToUndercity_CriticalWalkLegs|FullyQualifiedName~OrgrimmarCorpseRun_LiveRetrieveRoute"` with `WWOW_DATA_DIR='D:/wwow-bot/prod-data'`.
3. Confirm `wwow-pathfinding` Docker container is up and serving (it was
   healthy + 41 maps preloaded at end of prior session). If you rebuild any
   `Exports/Navigation/*.cpp`, restart the container per memory
   `feedback_pathfinding_docker_reload`.
4. **Stale Navigation.dll trap** — per memory entry of the same name. If you
   change C++, MSBuild `Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145`
   and copy the DLL to test output dirs, then re-run.

### Phase 1 — physics-parity gate (R13 demands this first)

Use **`mmo-physics-pathing-probe`** to classify every segment of each failing
route against the runtime physics engine. For each Cluster A failing label:

1. Capture the start + end coords from the `[InlineData]` rows in
   `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`.
2. Drive `tools/PathPhysicsProbe/PathPhysicsProbe.exe` for that route.
3. Identify the FIRST segment where the bake polygon graph and runtime
   physics disagree. Capture the `polyref`, the segment dz/slope, and the
   tile filename (use the polyref→polyIdx decoder from memory
   `project_pfs_overhaul_006_polyref_polyidx_decoding`: `polyref & 0xFFFFF`).
4. If the probe shows ALL segments are physics-walkable but the path resolver
   still produces the 4-corner partial: the bug is **resolver-side**, not
   bake-side. Skip to Phase 3.
5. If the probe finds a physics-vs-bake disagreement: this is a bake-fidelity
   issue. Open a sub-slot for `tools/MmapGen` per-tile config tuning
   (per-tile `cs`/`tileSize`/`agentMaxClimbTerrain`/`walkableHeight` overrides).
   Reference `project_pfs_overhaul_006_decklip_solution` for the OG zeppelin
   precedent and `project_pfs_overhaul_006_brm_singletile_negative` for the
   negative-result framing — confirm a tuning change improves on `cs=baseline`
   first instead of going straight to a destructive `0.2` clamp.

### Phase 2 — bake fixes (if Phase 1 finds bake-side issues)

Use **`mmo-pathfinding`** for any MmapGen tuning. Required:

1. Determine the tile under each failing OG-city coord. Use the WWoW
   coord-to-tile convention from `project_pathfinding_tile_coords` — `MmapGen.exe
   --tile X,Y` writes `<map><Y><X>.mmtile`. OG city tiles are around
   `(40,29)` (zeppelin ramp from `project_mmapgen_offmesh_axis_swap`).
2. Use `tools/MmapGen/promote-mmaps.ps1 -Map 1 -Tiles "X,Y"` to promote
   regenerated tiles from `D:/MaNGOS/data/mmaps/` → `D:/wwow-bot/test-data/`
   → `D:/wwow-bot/prod-data/`. Memory `feedback_promote_mmaps_tile_arg_xy`
   documents the X,Y vs Y,X convention gotcha.
3. After every tile regen: `docker restart wwow-pathfinding` (memory
   `feedback_pathfinding_docker_reload`). The service caches at startup.
4. Re-run the targeted failing test — DO NOT broad-test until you've
   localized the change.
5. **Use the validation harness**, not raw test reruns, to confirm a change
   improved physics-parity locally before re-baking globally. Per
   `project_pfs_overhaul_006_validation_harness_session` — Layers 1-5 ship +
   76 unit tests + BakeFixtureRecorder. Reference is
   `tools/PathPhysicsProbe/`.

### Phase 3 — resolver-side fixes (if Phase 1 shows bake is fine)

If the bake is good but the path resolver still produces 4-corner partials,
the bug is in `Exports/Navigation/PathFinder.cpp`'s smooth-path layer or the
managed `BuildUsablePathResult` bypass-trigger logic in
`Services/PathfindingService/Repository/Navigation.cs:~1772`.

1. Use **`codex:rescue`** to read PathFinder.cpp's smooth-path expansion
   path. Locate where the resolver decides "smooth path failed → use
   corridor fallback". The OG-city failures are SHORT routes (~315y) — the
   bypass was designed for 500y+ routes hitting truncation. Question: why
   is the bypass firing on 315y routes?
2. Investigate `MAX_POINT_PATH_LENGTH=1024` (memory entry
   `project_pfs_overhaul_006_brm_iteration_final` — discovered in iter 14).
   For 315y routes, 1024 should be plenty. If the smooth-path is still
   failing IsCompleteUsablePath, it's geometric truncation — check
   `HasUsableNativeEndpointAnchors` distance threshold (8y per the
   resolver).
3. Possible fix surfaces (do NOT apply blindly — verify via probe first):
   - Tighten the bypass-trigger condition so it fires only when smooth-path
     truncation is genuine (not when corridor-fallback gives shorter
     segments by accident).
   - Add a smooth-path-on-corridor-result post-pass that re-densifies the
     4-corner result back to ≤8y segments before returning.
   - Per-segment splitter in the test path-validation layer that's
     LESS strict for `kind=CorridorFirst` results when the underlying
     polygon corridor is short.
4. **Do NOT** add new repair phases to `Navigation.cs`. The freeze still
   applies.

### Phase 4 — cluster B (live integration failures)

Use **`mmo-movement-diagnostics`** for Cluster B. This skill is built for
exactly this situation — bot doesn't reach end, root cause is several layers
deep. Pair FG screenshots with snapshot/packet traces.

For each Cluster B failure:

1. **Reproduce in isolation** — run the single failing test with
   `WWOW_LONG_PATHING_TIMELINE` env var (per `LongPathingTests.cs:18-22`)
   to capture the timeline. Or `WWOW_OG_DECK_ANCHOR_VERIFY=1` for the
   zeppelin one.
2. **Capture FG screenshots** at start, mid-path, and stall (per
   `e:/repos/docs/TEST_SCREENSHOTS.md` — R11 in monorepo CLAUDE.md). These
   land in fixed paths overwritten per run for agent inspection.
3. **Pair with packet trace** — `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`
   captures CMSG/SMSG; ConnectionStateMachine.cs tracks state. Use Codex
   to summarize the packet trace — looking for transport-boarding state
   transitions, MOVE_HEARTBEAT cadence, or missing CMSG opcodes.
4. For `BG_Durotar_WindingPathSpeed` (BG too slow) — use **`mmo-bg-client-parity`**.
   This is a deterministic-client-parity issue. Compare BG MovementController
   speed against FG. Memory `project_pfs_overhaul_006_round4_iter5_VICTORY`
   established the architecture rules; check whether any rule was violated.
5. For zeppelin and transport tests — these end at the same OG city coord
   as Cluster A. Likely the path-quality fix from Phase 3 unblocks them. If
   not, dig further with the movement-diagnostics skill.

### Phase 5 — verify + lock in

1. Run all four acceptance test commands from "What 'works in all cases'
   means" above. All must be green.
2. Add new live coverage if a fix is non-obvious — use **`mmo-live-fixtures`**.
   Place new tests in `Tests/BotRunner.Tests/LiveValidation/` following the
   pattern in `Tests/CLAUDE.md` (LiveValidation Test Pattern MANDATORY
   structure).
3. **Document everything** — see "Documentation requirements" below.

## Documentation requirements (mandatory)

You **must** update memory + skills as you go:

### Memory entries

For each non-obvious finding, save a `project_*` or `feedback_*` memory entry
in `C:/Users/lrhod/.claude/projects/e--repos/memory/`. Use the existing
naming convention (slug-kebab-case). Add a one-line index entry to
`MEMORY.md`. Specifically required:

- One `project_pfs_og_city_resolver_*` entry per resolved cluster A failure
  category. If you find that all 10 fail the same way, ONE entry is fine.
- One `project_pfs_og_city_bg_speed_*` entry if Cluster B has a separate root
  cause from A.
- A `feedback_*` entry if you discover a new gotcha that future-you will
  forget — for example, "DON'T tune `walkableSlopeAngle` on tile (40,29)"
  or "ALWAYS run PathPhysicsProbe before assuming the bake is wrong."

### Skill updates

The skills you used (`mmo-pathfinding`, `mmo-physics-pathing-probe`,
`mmo-movement-diagnostics`, etc.) are versioned definitions in the
plugin/skill files. If you discover:

- A **new diagnostic surface** (e.g., a new probe flag, a new log line that
  helped localize a bug) — update the skill's "How to apply" section.
- A **new failure mode** (e.g., "this is what corridor-fallback bypass looks
  like in a probe trace") — add it to the skill's "Diagnostic patterns" or
  equivalent section.
- A **new tuning recipe** (e.g., a per-tile config combination that worked
  for OG city) — add it to the skill's "Recipes" or worked-examples section.
- A **changed contract** (e.g., a new env var, a renamed CLI flag) — update
  the skill's command syntax.

The skills live as plugin files. Find them by `Glob` for `*.skill.md` or
`*.yaml` in the plugin install dirs (start with `C:/Users/lrhod/.claude/plugins/`
or `~/.claude/plugins/` — confirm via `Skill` tool listing). If a skill is in
this repo, edit it directly. If it's in a plugin, propose an update via the
plugin's contribution path (or document the proposed update in
`docs/Plan/QUESTIONS.md` for the human to apply).

### TASKS.md + Plan/02_PHASE1

Following the conventions established by the prior session:

- Update `docs/TASKS.md` row for `S1.3` (PFS stability) — currently
  `blocked (red baseline)` — to `implemented` once green, with a citation
  line summarizing the fix and pointing at the new memory entries.
- Update `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` `S1.3` section
  with the same evidence + a "Latest evidence (2026-05-15)" block.
- If you open new sub-slots (e.g., a new bake-fidelity sub-slot), define
  them in `Plan/02_PHASE1` per the slot schema in
  `Plan/00_OVERVIEW.md` — owned-paths, dependencies, success criteria.

## Commit hygiene

- Commit each layer as a separate commit. Per the prior session's pattern:
  one commit per slot, one commit per memory-entry batch, one commit per
  skill update. Don't bundle.
- Push after each commit per `feedback_main_branch_no_pr_workflow`.
- Co-author tag your commits exactly as the prior session does:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.
- Avoid `--no-verify` and `--amend` per `e:/repos/CLAUDE.md` git safety
  protocol.

## What to read first (in order)

1. This handoff (you're reading it).
2. `e:/repos/CLAUDE.md` (monorepo-wide rules R1–R13).
3. `e:/repos/Westworld of Warcraft/CLAUDE.md` (WWoW-specific rules + token
   efficiency mandate).
4. `e:/repos/Westworld of Warcraft/docs/SPEC.md` (entry point) and
   `Plan/00_OVERVIEW.md` (dispatch model).
5. `Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` S1.3 + S1.1 + S1.2 sections.
6. The 6 memory entries listed in Phase 0 step 1 above.
7. `docs/physics/PATHFINDING_OVERHAUL.md` (the freeze charter).

Then start Phase 0. Use Codex liberally — you have a long road ahead and a
limited context budget. The skills exist precisely so you don't have to
reinvent investigation procedures from scratch.

Good luck. — handed off 2026-05-15 from the validation+frame-implementation session.
