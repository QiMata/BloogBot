# TASKS — Live Dispatcher

> **This file is the rolling task board.** It shows what's in flight right
> now. Full slot enumeration is in [`Plan/`](Plan/). Phase-history detail
> lives in [`ARCHIVE.md`](ARCHIVE.md). Read [`SPEC.md`](SPEC.md) first if
> you have not.

Last refresh: 2026-05-18 (loop 24 — dual-track /loop "Pathfinding Close-Out" started; Phase A1 executed end-to-end: PathFinder.cpp polyref==0 guard at main `iterPos:1936` + `findStraightPath` post-process with DEFAULT extents (2.0, 1.8, 2.0), +78 LOC, MSBuild green. OG zeppelin 4/4 critical gate held. CriticalWalkLegs **19/4/0 unchanged** — no closure, no regression. Root cause of no-op: failing corner `(1347.3,-4540.6,35.8)` = loop-21 coord 1 IS a densifier midpoint at lines 1919-1931, NOT the iterPos main emit — loop 23 mis-identified the layer. No PathFinder.cpp-layer fix can close the 4 remaining failures (densifier-layer guards already proven to regress per loop-23 Surface B). Reverted to baseline; advancing to Phase A2 (probe `--dump-poly-stack` for full polyref enumeration at coord ±10y Z window). See "Pathfinding Close-Out" section below.).

## Rules

1. **One continuous session.** Auto-compaction handles context limits.
2. **Read [`SPEC.md`](SPEC.md) and [`Plan/00_OVERVIEW.md`](Plan/00_OVERVIEW.md)
   before claiming a slot.**
3. **The MaNGOS server is ALWAYS live** on the `Westworld-Test` realm
   (see [`Spec/16_REALMS_AND_ACCOUNTS.md`](Spec/16_REALMS_AND_ACCOUNTS.md)).
4. **WoW.exe binary parity is THE rule** for physics/movement.
5. **No `.gm on` in tests** — corrupts UnitReaction bits.
6. **Pathfinding freeze (since 2026-05-06).** Mesh fixes only in
   `tools/MmapGen/`; no new repair phases in `Navigation.cs`.
7. **Slot ownership is exclusive.** No two in-progress slots may write
   the same owned-path glob.
8. **No lease tracking.** Bots are always on; OnDemand uses a siloed
   reserved pool (per 2026-05-12 redesign).
9. **Tests drive Activities, not Actions.** Activity × Objective is the
   test-naming convention; see [`CLAUDE.md → Test Isolation Rules`](../CLAUDE.md#test-isolation-rules--critical).

## Phase status

| Phase | File | Status |
|---|---|---|
| 0 — Spec hardening | [`Plan/01_PHASE0_SPEC_HARDENING.md`](Plan/01_PHASE0_SPEC_HARDENING.md) | **done** (closed 2026-05-12; details in [`ARCHIVE.md`](ARCHIVE.md#phase-0-closure-2026-05-12)) |
| 1 — Action / Task Foundation | [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md) | **in-progress** (S1.0 + S1.15 + S1.17 + S1.19 done; S1.1–S1.3 substrate green; S1.4–S1.14 + S1.16 + S1.18 + S1.20 open) |
| 2 — OnDemand Engine | [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](Plan/03_PHASE2_ONDEMAND_ENGINE.md) | not-started (waiting on Phase 1) |
| 3 — UI Default + Test Host | [`Plan/04_PHASE3_UI_DEFAULT.md`](Plan/04_PHASE3_UI_DEFAULT.md) | not-started |
| 4 — Activity Registry | [`Plan/05_PHASE4_ACTIVITY_REGISTRY.md`](Plan/05_PHASE4_ACTIVITY_REGISTRY.md) | not-started |
| 5 — Observability | [`Plan/06_PHASE5_OBSERVABILITY.md`](Plan/06_PHASE5_OBSERVABILITY.md) | not-started |
| 6 — Automated Progression | [`Plan/07_PHASE6_AUTOPROGRESSION.md`](Plan/07_PHASE6_AUTOPROGRESSION.md) | not-started |
| 7 — Pathfinding/Scene Scale | [`Plan/08_PHASE7_PATHFINDING_SCALE.md`](Plan/08_PHASE7_PATHFINDING_SCALE.md) | not-started |
| 8 — Living-Server Load | [`Plan/09_PHASE8_LOAD.md`](Plan/09_PHASE8_LOAD.md) | not-started |
| **9 — Catalog completeness** | [`Plan/13_PHASE9_CATALOG_FILL.md`](Plan/13_PHASE9_CATALOG_FILL.md) | **new (2026-05-17)** — Scarlet Monastery, Stockades, dungeon-quest catalogs, holiday events, mage-port / warlock-summon services, escort family |
| **10 — Decision-Engine integration** | [`Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md`](Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md) | **new (2026-05-17)** — wire `DecisionEngineService` into IActivity composer; ML-aided reward selection |
| **11 — Social fabric** | [`Plan/15_PHASE11_SOCIAL_FABRIC.md`](Plan/15_PHASE11_SOCIAL_FABRIC.md) | **new (2026-05-17)** — trade chat, guild events, mail traffic, whisper responsiveness |
| **12 — Behavioral variation** | [`Plan/16_PHASE12_BEHAVIORAL_VARIATION.md`](Plan/16_PHASE12_BEHAVIORAL_VARIATION.md) | **new (2026-05-17)** — per-bot personality knobs for indistinguishability |
| BRM bake-fidelity (parallel) | [`Plan/10_PARALLEL_BRM_BAKE.md`](Plan/10_PARALLEL_BRM_BAKE.md) | open (multi-cycle MmapGen) |
| Skill refinement (parallel) | [`Plan/11_PARALLEL_SKILL_REFINEMENT.md`](Plan/11_PARALLEL_SKILL_REFINEMENT.md) | open |
| Test isolation refactor (parallel) | [`Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md`](Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md) | open |

## Active slots — Phase 1

| Slot | Title | Owner | Status |
|---|---|---|---|
| `S1.0` | `IBotTask` contract migration | `monorepo-worker` | **done** (2026-05-12) |
| `S1.1` | Physics parity wrap-up | `monorepo-worker` | open (guard green 12/12 OG; need representative checkpoints per family) |
| `S1.2` | MovementController parity audit | `monorepo-worker` | audit green (2026-05-12, 33/33) |
| `S1.3` | PathfindingService stability sweep | `monorepo-worker` | full-coverage-green (2026-05-18 loop 24; 19/4 + 0 unrun; Phase A1 (`PathFinder.cpp` polyref==0 SKIP-then-bail guard at main `iterPos:1936` + `findStraightPath` post-process, DEFAULT extents) executed: OG zep 4/4 green, CriticalWalkLegs **unchanged** at 19/4/0, **no regression**. Failing corner 42 `(1347.3,-4540.6,35.8)` = loop-21 coord 1 is a DENSIFIER MIDPOINT (line 1919-1931) NOT iterPos:1936; loop-23 mis-identified the layer. Densifier-layer guards proven to regress (loop-23 Surface B). PathFinder.cpp-layer fix architecturally insufficient. Reverted; advancing to Phase A2 = probe `--dump-poly-stack` diagnostic.) |
| `S1.4..S1.14` | 11 family slots (Travel, Combat, Questing, Dungeon, BG, Gather, Craft, Economy, Social, Recovery, Raid-formation) | various | open (no dry-run yet) |
| `S1.15` | Trade null guards (6 actions) | `monorepo-worker` | implemented (2026-05-15; live TradeParityTests pending) |
| `S1.16` | Craft packet path (BG) | `monorepo-worker` | open |
| `S1.17` | Vendor merchant null handling | `monorepo-worker` | implemented (2026-05-15; live VendorParityTests pending) |
| `S1.18` | Taxi packet path (BG) | `monorepo-worker` | open |
| `S1.19` | Trainer/Talent/Gossip packet paths (BG) | `monorepo-worker` | implemented (2026-05-15; live parity tests pending) |
| `S1.20` | One-hour shake-out test | `monorepo-test-runner` | open (Phase 1 acceptance gate; depends on S1.1..S1.19) |

## Next pickup options

1. **Land any open Phase 1 family slot** (S1.4..S1.14). Pick a family with a representative live-validation test the bot can drive end-to-end.
2. **Close S1.16 / S1.18** (Craft + Taxi BG packet paths) — both follow the `Network*Frame` adapter pattern shipped in S1.15/17/19.
3. **Run S1.20 dry-run** to expose any cross-family interaction bugs before opening Phase 2.
4. **Pick up a Plan/13 (Phase 9) catalog-fill slot** in parallel with Phase 1; catalog rows are pure-data work that does not block on the substrate.

## Parallel tracks

| Track | Active slot | Owner | Status | File |
|---|---|---|---|---|
| BRM bake-fidelity | S9.1 — Triage post-cull stall coord | `monorepo-worker` or `codex:codex-rescue` | open | [`Plan/10_PARALLEL_BRM_BAKE.md`](Plan/10_PARALLEL_BRM_BAKE.md) |
| Skill refinement | S10.1 — `activity-catalog-bootstrap` skill | `monorepo-worker` | open | [`Plan/11_PARALLEL_SKILL_REFINEMENT.md`](Plan/11_PARALLEL_SKILL_REFINEMENT.md) |
| Test isolation refactor | (slots) | `monorepo-worker` | open (post Phase-2 S2.0) | [`Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md`](Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md) |

## Test baseline (refreshed 2026-05-15)

| Suite | Passed | Failed | Notes |
|---|---|---|---|
| WoWSharpClient.Tests | 1623 | 0 | Movement parity 30/30 green; iter-18 closed Update_IdleAuthoritativeRelocation flake |
| Navigation.Physics.Tests | 137 + 68 round-4 | 0 | All walkable checkpoints + 12/12 OG green |
| BotRunner.Tests (unit) | 1747 | 0 | NavigationPathTests 80/80 green |
| Validation harness (OG) | 12/12 | 0 | Cliff-fall fix landed 1c530288 |
| PathfindingService.Tests (full sweep) | 19 | 4 | tile (40,29) bake-side defects only; 0 unrun under 100-min budget |

## Open questions

[`Plan/QUESTIONS.md`](Plan/QUESTIONS.md). No entries blocking active slots as of 2026-05-17.

## Canonical commands

```powershell
# Repo-scoped process inspection/cleanup only
.\run-tests.ps1 -ListRepoScopedProcesses

# Build .NET + C++
dotnet build WestworldOfWarcraft.sln

# Tests (layered)
.\run-tests.ps1

# Targeted live test
$env:WWOW_DATA_DIR = 'D:\MaNGOS\data'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj `
  --configuration Release --no-restore `
  --filter "FullyQualifiedName~<TestClass>" `
  --logger "console;verbosity=minimal"

# Docker stack
docker compose -f docker-compose.vmangos-linux.yml up -d
docker restart wwow-pathfinding  # after MmapGen tile regen

# Validation harness (OG zeppelin)
dotnet test Tests/Navigation.Physics.Tests --filter "OgZeppelin"
```

## Pathfinding Close-Out (loop 24+, started 2026-05-18)

Dual-track /loop driven by
[`Plan/Handoffs/2026-05-17-pathfinding-close-23-parallel.md`](Plan/Handoffs/2026-05-17-pathfinding-close-23-parallel.md)
follow-on plan. Closes the 4 remaining tile (40,29) failures (Track A)
OR exhausts options and accepts 19/4 (A6). Track B prototypes a
skip-voxelization bake pipeline as a long-term replacement.

### State
- Current tile (40,29) md5: `cc0d89c42d9abf4737ba52a369c5f3f7` (baseline)
- Last CriticalWalkLegs tally: **19/4/0**
- Last iteration: **loop 24, Phase A1 (NEUTRAL — no closure, no regression, reverted)**
- Last commit: pending (this loop's docs commit)

### Track A — Close-23 (sequential phases)
- [x] **A1: Surface B at right layer** — 2026-05-18 NEUTRAL. PathFinder.cpp polyref==0 SKIP-then-bail guard at main `iterPos:1936` + `findStraightPath` post-process (default extents). +78 LOC, MSBuild green. OG zep 4/4 critical gate held; CriticalWalkLegs **19/4/0 unchanged**, no regression. Reverted. Root cause: failing corners are densifier midpoints (line 1919-1931), not iterPos main emit — loop 23 mis-identified the layer. See [[project_pfs_loop24_phase_a1_neutral]].
- [ ] **A2: Probe coord-stack widening (diagnostic only)** — extend `tools/PathPhysicsProbe` with `--dump-poly-stack` enumerating ALL `dtPoly` entries whose AABB overlaps probe coord ±10y Z window via direct tile poly iteration (not `findNearestPoly`). Output TSV with polyref, surfaceZ, posOverPoly, area, flags, vertCount. Dump full stacks at coords 1, 2, 3 to `/tmp/wwow-loop24-probes/`. **No live test impact. Always commits.**
- [ ] A3: Multi-knob coordinated bake regen (1 attempt, calibrated from A2 data)
- [ ] A4: Validator-driven targeted cull (using A2 stack data)
- [ ] A5: Navigation.cs off-mesh awareness (multi-iteration)
  - [ ] A5.1: Audit 8 repair phases, identify off-mesh-blind functions
  - [ ] A5.2: DT_POLYTYPE_OFFMESH_CONNECTION skip-checks for Phase 1 + unit test
  - [ ] A5.3: Repeat for Phases 2-8 of the repair pipeline
  - [ ] A5.4: E2E test against an existing OG zeppelin offmesh entry
  - [ ] A5.5: Add Surface C's 4 new offmesh entries, regen, sweep
- [ ] A6: Accept 19/4 as permanent (fallback if A1-A5 all attempted, no winner)

### Track B — Skip-voxelization bake prototype
- [ ] B1: Project scaffold (`tools/SkipVoxBake/`, smoke I/O round-trip on baseline mmtile)
- [ ] B2: Synthetic input harness (hand-crafted triangle soup for tile (40,29))
- [ ] B3: Walkable triangle tagging (slope + material/area flags)
- [ ] B4: 2D walkable region computation per Z layer (Clipper2)
- [ ] B5: Agent-radius erosion (Minkowski offset)
- [ ] B6: Polygonize into Detour polys (≤6 verts per polygon)
- [ ] B7: Inter-layer off-mesh detection
- [ ] B8: Emit Detour mmtile (bit-compare to Recast bake)
- [ ] B9: ADT terrain input adapter
- [ ] B10: WMO input adapter
- [ ] B11: GO collision adapter
- [ ] B12: Bake tile (40,29) end-to-end against full MaNGOS data
- [ ] B13: Probe candidate tile (`--dump-poly-stack` from A2 against skipvox tile)
- [ ] B14: Live sweep against skipvox tile (23/0 target, full regression)
- [ ] B15+: Generalize to other tiles

### Next iteration action
**Phase A2** — extend `tools/PathPhysicsProbe.exe` with `--dump-poly-stack`
flag that enumerates all `dtPoly` entries whose AABB overlaps a given
coord ±10y Z window via direct tile iteration (`dtMeshTile->polys[]`),
NOT `findNearestPoly` (which is loop-21's tool already). Output TSV
columns: `polyref`, `surfaceZ`, `posOverPoly`, `area`, `flags`,
`vertCount`. Dump stacks at coords 1 `(1347.3, -4540.6)`, 2 `(1350.2,
-4528.6)`, 3 `(1348.0, -4537.7)` to `/tmp/wwow-loop24-probes/`. The
stack data feeds Phase A3 + A4. **No live test impact. Always commits.
Always advances to Phase A3 (single attempt) regardless of stack
content.**

### Blocked / questions for user
None as of 2026-05-18.

## History

Phase 0 closure detail, S1.0 / S1.15 / S1.17 / S1.19 landing detail, and the
2026-05-10 → 2026-05-16 pathfinding iteration log are archived in
[`ARCHIVE.md`](ARCHIVE.md). The slot/phase model landed 2026-05-11; the phase
reorder for action/task priority landed 2026-05-12; the AOTA architecture
deep-dive and end-state spec/plan reorganization landed 2026-05-17.
