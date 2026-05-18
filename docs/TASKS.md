# TASKS — Live Dispatcher

> **This file is the rolling task board.** It shows what's in flight right
> now. Full slot enumeration is in [`Plan/`](Plan/). Phase-history detail
> lives in [`ARCHIVE.md`](ARCHIVE.md). Read [`SPEC.md`](SPEC.md) first if
> you have not.

Last refresh: 2026-05-18 (loop 24 / iteration 3 — Phase A3 bake regen attempted on tile (40,29) with single coordinated 2-knob delta: `walkableErosionRadius 0.2→0.3` + `walkableHeight 0→14`. Tile baked clean (md5 fbe57ed4, -8.4% size). Probe via Phase-A2-shipped `--dump-poly-stack`: **phantom stack at coord 2 UNCHANGED (63 phantoms + 1 legit polyref polyIdx shifted 18398→17389, surfaceZ=37.509 preserved); off-mesh entries dropped because --offMeshInput wasn't threaded (coord 1: 1→0; coord 3: 5→4)**. Knobs A+B don't fit the phantom class. Reverted prod-data + MaNGOS + config before live test cycle (off-mesh dropout = guaranteed OG-zep regression). NEGATIVE_RESULT note added to config.json. Candidate forensic copy at `/tmp/wwow-loop24-candidates/A3/`. Loop 24 tally remains 19/4/0 (A1 neutral commit c68197e1; A2 diagnostic commit 5c0db496; A3 reverted this commit). Advancing to Phase A4 (targeted cull, viable for coord 2 ONLY per A2 architectural refinement).).

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
| `S1.3` | PathfindingService stability sweep | `monorepo-worker` | full-coverage-green (2026-05-18 loop 24 / iter 3; 19/4 + 0 unrun; Phase A3 attempted: 2-knob bake regen on tile (40,29) `walkableErosionRadius 0.2→0.3` + `walkableHeight 0→14`. Bake clean (md5 fbe57ed4, -8.4% size). Probe revealed phantom stack at coord 2 UNCHANGED + off-mesh entries dropped (--offMeshInput not threaded). Reverted before live cycle. Tile back to baseline md5 cc0d89c4. NEGATIVE_RESULT note added to config.json. Phase A2 (iter 2) shipped `--dump-poly-stack` diagnostic (commit 5c0db496). Phase A1 (iter 1) PathFinder.cpp polyref==0 guard, NEUTRAL, reverted (commit c68197e1). Advancing to Phase A4 = validator-driven targeted cull on the 63 phantoms catalogued in Phase A2 (preserving polyIdx 18398 legit ground).) |
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
- Last iteration: **loop 24 / iteration 3, Phase A3 (NEGATIVE — knobs A+B don't fit phantom class + off-mesh dropout; reverted before live cycle)**
- Last commit: pending (this loop's docs commit, including config.json NEGATIVE_RESULT note)

### Track A — Close-23 (sequential phases)
- [x] **A1: Surface B at right layer** — 2026-05-18 NEUTRAL. PathFinder.cpp polyref==0 SKIP-then-bail guard at main `iterPos:1936` + `findStraightPath` post-process (default extents). +78 LOC, MSBuild green. OG zep 4/4 critical gate held; CriticalWalkLegs **19/4/0 unchanged**, no regression. Reverted. Root cause: failing corners are densifier midpoints (line 1919-1931), not iterPos main emit — loop 23 mis-identified the layer. See [[project_pfs_loop24_phase_a1_neutral]].
- [x] **A2: Probe coord-stack widening (diagnostic only)** — 2026-05-18 SHIPPED. New native export `EnumeratePolysAtCoord` (DllMain.cpp +162 LOC, direct tile-poly iteration + 8 neighbours, AABB intersect) + matching `[DllImport]` in NavigationInterop.cs + `--dump-poly-stack` in PathPhysicsProbe.exe (+135 LOC). Stack dump confirms: coord 1 has 1 poly (off-mesh only — true air); coord 2 has 64 polys = 63 phantoms + 1 legit ground polyref `281475331147742` (surfaceZ=37.509, posOverPoly=1, 3.5y above WP Z=34.0); coord 3 has 5 polys (4 deck-above + 1 off-mesh — true air). Cull viable for coord 2 only. See [[project_pfs_loop24_phase_a2_polystack]].
- [x] **A3: Multi-knob coordinated bake regen (1 attempt, calibrated from A2 data)** — 2026-05-18 NEGATIVE. Knobs `walkableErosionRadius 0.2 → 0.3` + `walkableHeight 0 → 14`. Bake clean (md5 fbe57ed4, size -8.4%). Probe revealed phantom stack at coord 2 UNCHANGED + off-mesh entries dropped (--offMeshInput not threaded). Reverted prod-data + MaNGOS + config before live cycle. NEGATIVE_RESULT note added to config.json `_4029_NEGATIVE_RESULT_loop24_A3`. Candidate forensic copy `/tmp/wwow-loop24-candidates/A3/`. See [[project_pfs_loop24_phase_a3_neutral]].
- [ ] **A4: Validator-driven targeted cull (using A2 stack data)** — `tools/MmapGen/build/NavMeshTileEditor.exe --cull-polys` to zero `area`+`flags` on the 63 phantom polyIdxes catalogued in Phase A2 (preserve polyIdx 18398, the legit ground at z=37.509). Probe-verify only the legit poly remains. CAVEAT: legit poly is 3.5y above WP Z=34.0, beyond Detour's default 1.8y `findNearestPoly` extent even after cull — cull-only may not close coord 2; if so, advance to A5. ONE cull attempt per iteration; revert on regression.
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
**Phase A4 — validator-driven targeted cull on coord-2 phantom stack.**

Per Phase A2 architectural refinement, cull is viable for **coord 2
ONLY** (coords 1 + 3 are true-air; cull can't help them). Use
`tools/MmapGen/build/NavMeshTileEditor.exe --cull-polys` to surgically
zero `area` + `flags` on the 63 phantom polyIdxes catalogued in
Phase A2. The legit ground polyIdx 18398 at surfaceZ=37.509,
posOverPoly=1, aabb [37.31, 37.81] MUST be preserved.

**Phantom polyIdx ranges to cull (from Phase A2's stack at coord 2):**
- 18285–18293 (upper phantom band, area=3 NAV_STEEP_SLOPES, aabb Z 38–49)
- 18302–18337 (mid phantom band, area=3, aabb Z 32.4–36.1)
- 18349–18372 (mid phantom band, area=1 AREA_GROUND, aabb Z 32.1–35.9)
- 18387–18397 (upper phantom mix)
- 18399–18400 (immediately after legit, no surfaceZ)

Total cull target: ~64 polys — minus polyIdx 18398 = 63 culled.

Procedure:
1. Snapshot tile to `/tmp/wwow-tile-backup/0012940.mmtile.loop24-A4-before`.
2. Run `NavMeshTileEditor.exe --cull-polys <polyIdx-list-or-file>`.
   Use `--cull-polys-file` with a TSV of polyIdxes excluding 18398.
3. Probe via `--dump-poly-stack` at coord 2 to verify the stack
   collapses to ~1-3 polys (just the legit ground + immediate neighbours).
4. Probe at coords 1 + 3 to confirm no collateral changes.
5. If stack improved, copy to `D:/wwow-bot/prod-data/mmaps/` and
   `docker restart wwow-pathfinding`.
6. Full 23-case sweep + adjacent suites.
7. Revert immediately on any regression in OG zep 4/4 or any of the
   19 passing CriticalWalkLegs cases.

**ONE cull attempt per iteration. If A4 attempt loses, advance to
Phase A5 (Navigation.cs off-mesh awareness). Do NOT retry within A4.**

**Expected outcome:** even with phantoms culled, the legit ground at
z=37.509 is 3.5y above WP Z=34.0 — beyond Detour's default 1.8y
findNearestPoly extent. A4 alone may not close coord 2 unless
paired with a smooth-path corner-Z snap (PathFinder.cpp change). If
probe confirms cull worked but sweep doesn't close, that's an A4
DIAGNOSTIC WIN (proves the cull architecture works, isolates the
remaining gap to the Detour query side) — proceed to A5.

### Blocked / questions for user
None as of 2026-05-18.

## History

Phase 0 closure detail, S1.0 / S1.15 / S1.17 / S1.19 landing detail, and the
2026-05-10 → 2026-05-16 pathfinding iteration log are archived in
[`ARCHIVE.md`](ARCHIVE.md). The slot/phase model landed 2026-05-11; the phase
reorder for action/task priority landed 2026-05-12; the AOTA architecture
deep-dive and end-state spec/plan reorganization landed 2026-05-17.
