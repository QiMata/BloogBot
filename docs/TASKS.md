# TASKS — Live Dispatcher

> **This file is the rolling task board.** It shows what's in flight right
> now. Full slot enumeration is in [`Plan/`](Plan/). Phase-history detail
> lives in [`ARCHIVE.md`](ARCHIVE.md). Read [`SPEC.md`](SPEC.md) first if
> you have not.

Last refresh: 2026-05-18 (loop 24 / iteration 6 — Phase A5.2 ships off-mesh detection substrate. New native export `IsOffMeshConnectionAtCoord` (DllMain.cpp +90 LOC, direct tile iteration over off-mesh polys, short-circuit on first match — bypasses findNearestPoly's off-mesh deprioritisation per loop-21). Managed `IsOffMeshSegment` helper (Navigation.cs +75 LOC, memoised per-CalculatePath via existing SegmentValidationCacheScope). Skip-check at `RepairLongLineOfSightBreaks:2877` skips densification + LOS on teleport segments. 4 new unit tests green (`IsOffMeshConnectionAtCoordTests`, +95 LOC). OG zep 4/4 critical gate held. Tally unchanged 19/4 (substrate, not closure — closure depends on A5.5). Loop 24 history: A1 c68197e1; A2 5c0db496; A3 37ee100e; A4 528eb958; A5.1 acf3a7e6; A5.2 this commit. Advancing to Phase A5.3 (apply same skip-check pattern to Phases 2-7).).

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
| `S1.3` | PathfindingService stability sweep | `monorepo-worker` | full-coverage-green (2026-05-18 loop 24 / iter 6; 19/4 + 0 unrun durable; Phase A5.2 SHIPS off-mesh detection substrate. New native `IsOffMeshConnectionAtCoord` export (DllMain.cpp +90 LOC, direct tile iteration short-circuit) + managed `IsOffMeshSegment` helper (Navigation.cs +75 LOC, memoised per-CalculatePath) + skip-check at `RepairLongLineOfSightBreaks:2877` (preserves teleport endpoints without densification/LOS) + 4 new unit tests in `IsOffMeshConnectionAtCoordTests.cs` (+95 LOC) all green. OG zep 4/4 critical gate held. Tally unchanged 19/4 (substrate, not closure). Loop 24 history: A1 c68197e1 (NEUTRAL); A2 5c0db496 (--dump-poly-stack SHIPPED); A3 37ee100e (NEGATIVE bake regen); A4 528eb958 (cull verified, list calibration regressed); A5.1 acf3a7e6 (audit catalogue); A5.2 this commit. Advancing to A5.3 = apply skip-check to Phases 2-7.) |
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
- Last iteration: **loop 24 / iteration 6, Phase A5.2 (off-mesh helper + Phase 1 skip-check SHIPPED; OG zep 4/4 held; 4 unit tests green)**
- Last commit: pending (this loop's A5.2 ship commit)

### Track A — Close-23 (sequential phases)
- [x] **A1: Surface B at right layer** — 2026-05-18 NEUTRAL. PathFinder.cpp polyref==0 SKIP-then-bail guard at main `iterPos:1936` + `findStraightPath` post-process (default extents). +78 LOC, MSBuild green. OG zep 4/4 critical gate held; CriticalWalkLegs **19/4/0 unchanged**, no regression. Reverted. Root cause: failing corners are densifier midpoints (line 1919-1931), not iterPos main emit — loop 23 mis-identified the layer. See [[project_pfs_loop24_phase_a1_neutral]].
- [x] **A2: Probe coord-stack widening (diagnostic only)** — 2026-05-18 SHIPPED. New native export `EnumeratePolysAtCoord` (DllMain.cpp +162 LOC, direct tile-poly iteration + 8 neighbours, AABB intersect) + matching `[DllImport]` in NavigationInterop.cs + `--dump-poly-stack` in PathPhysicsProbe.exe (+135 LOC). Stack dump confirms: coord 1 has 1 poly (off-mesh only — true air); coord 2 has 64 polys = 63 phantoms + 1 legit ground polyref `281475331147742` (surfaceZ=37.509, posOverPoly=1, 3.5y above WP Z=34.0); coord 3 has 5 polys (4 deck-above + 1 off-mesh — true air). Cull viable for coord 2 only. See [[project_pfs_loop24_phase_a2_polystack]].
- [x] **A3: Multi-knob coordinated bake regen (1 attempt, calibrated from A2 data)** — 2026-05-18 NEGATIVE. Knobs `walkableErosionRadius 0.2 → 0.3` + `walkableHeight 0 → 14`. Bake clean (md5 fbe57ed4, size -8.4%). Probe revealed phantom stack at coord 2 UNCHANGED + off-mesh entries dropped (--offMeshInput not threaded). Reverted prod-data + MaNGOS + config before live cycle. NEGATIVE_RESULT note added to config.json `_4029_NEGATIVE_RESULT_loop24_A3`. Candidate forensic copy `/tmp/wwow-loop24-candidates/A3/`. See [[project_pfs_loop24_phase_a3_neutral]].
- [x] **A4: Validator-driven targeted cull (using A2 stack data)** — 2026-05-18 cull-pipeline VERIFIED end-to-end but cull-list calibration regressed. `NavMeshTileEditor.exe --cull-polys-file` culled all 63 phantoms at coord 2 preserving legit polyIdx 18398. Probe verified mechanically (63 polys with area=0/flags=0, 1 surviving). OG zep 4/4 held; CriticalWalkLegs **18/5 (−1 regression)**. 12 culled polys at aabbMinZ ≥ 36.8 (deck region) were load-bearing. Reverted. See [[project_pfs_loop24_phase_a4_neutral]]. Future retry candidate: cull list filtered to aabbMaxZ<36.5 (51 polys, deck-safe) — logged for after A5 if needed.
- [ ] **A5: Navigation.cs off-mesh awareness (multi-iteration)**
  - [x] **A5.1: Audit 8 repair phases — DONE** (read-only). Finding: ZERO off-mesh awareness in Navigation.cs (0 matches for polyType/DT_POLYTYPE across 7448 LOC). Catalogued the 8 phases in `ApplyNativeSegmentValidationCore`: 1) RepairLongLineOfSightBreaks; 2) DensifyPath+NormalizeEarlySupportLayer+RemoveShortVerticalLayerSpikes+RemoveShortHorizontalDetourSpikes; 3) RepairEarlyStaticBreaks; 4) RepairAffordanceBreaks (1st); 5) re-Normalize; 6) RepairAffordanceBreaks (2nd, post-normalize); 7) RepairEarlyStaticBreaks (post-affordance); 8) NormalizeLocalPhysicsReachableLayers + FindFirstLocalPhysicsReachabilityBreak + RepairAffordanceBreaks (3rd, local-physics). See [[project_pfs_loop24_phase_a5_1_audit]].
  - [x] **A5.2: Ship `IsOffMeshSegment` helper + Phase 1 skip-check — DONE.** Native `IsOffMeshConnectionAtCoord` export (DllMain.cpp +90 LOC, direct tile iteration over off-mesh polys, short-circuit on first match — bypasses findNearestPoly's off-mesh deprioritisation). Managed `IsOffMeshSegment(uint, XYZ, XYZ)` helper in Navigation.cs (+75 LOC) memoised per-CalculatePath via the existing `SegmentValidationCacheScope`. Skip-check at `RepairLongLineOfSightBreaks:2877` preserves teleport endpoint pairs without densification/LOS. New test file `IsOffMeshConnectionAtCoordTests.cs` (+95 LOC, 4 tests green). OG zep 4/4 critical gate held. Tally unchanged 19/4 (substrate, not closure). See [[project_pfs_loop24_phase_a5_2_offmesh_helper]].
  - [ ] **A5.3: Apply skip-check to Phases 2-7 (6 functions)** — same `if (IsOffMeshSegment(mapId, segmentStart, segmentEnd)) continue;` pattern at the entry of: `DensifyPath` (2905), `NormalizeEarlySupportLayer` (3124), `RemoveShortVerticalLayerSpikes` (3302), `RemoveShortHorizontalDetourSpikes` (3350), `RepairEarlyStaticBreaks` (4026), `RepairAffordanceBreaks` (4103). Expected diff ≈ 30-40 LOC across the 6 functions.
  - [ ] A5.4: E2E test — new test asserting managed-validation wall time < 1s on an OG zep off-mesh path (was 15-20s).
  - [ ] A5.5: Deploy loop-23 Surface C's 4 off-mesh entries; bake; full 23-case sweep. Target: 23/0.
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
**Phase A5.3 — apply `IsOffMeshSegment` skip-check to Phases 2-7.**

Same pattern as A5.2's `RepairLongLineOfSightBreaks` skip-check, applied
to 6 more repair functions. Each addition is 2-3 lines at the per-segment
loop entry. Expected total diff ≈ 30-40 LOC across 6 functions.

Functions to modify (line numbers from current head):

1. `DensifyPath` (2905) — when off-mesh pair, append `end` directly (no
   midpoints).
2. `NormalizeEarlySupportLayer` (3124) — skip the ground-Z projection;
   off-mesh endpoints' Z is intentional, not an off-surface error.
3. `RemoveShortVerticalLayerSpikes` (3302) — don't classify the off-mesh
   Z-jump as a spike.
4. `RemoveShortHorizontalDetourSpikes` (3350) — don't classify dx=0 ridge
   as a detour spike.
5. `RepairEarlyStaticBreaks` (4026) — skip LOS+findPath repair on off-mesh
   segments; teleport endpoints are LOS-valid by contract.
6. `RepairAffordanceBreaks` (4103) — skip `RequiresAffordanceRepair` on
   off-mesh segments; affordance classifiers (Cliff/SteepClimb) mis-handle
   them and trigger the loop-23 hang loop.

Verification per phase:
- MSBuild + dotnet build green
- OG zep 4/4 (critical gate)
- IsOffMeshConnectionAtCoordTests still 4/4
- New regression test on a 3-corner synthetic path with an off-mesh middle
  segment: verify NO phase fires repair on the middle pair.

NO full 23-case sweep yet — that's A5.4 after all 7 phases are
off-mesh-aware. Closure depends on A5.5 (deploy Surface C's 4 new
off-mesh entries on tile 40,29).

**Helper signature** (managed, in `Navigation.cs`):

```csharp
private static bool IsOffMeshSegment(uint mapId, XYZ start, XYZ end)
{
    if (!GetPolyAtCoordNative(mapId, start, 2.0f, 1.8f,
            out _, out var startType, out _, out _, out _))
        return false;
    if (startType == 1 /* DT_POLYTYPE_OFFMESH_CONNECTION */)
        return true;
    if (!GetPolyAtCoordNative(mapId, end, 2.0f, 1.8f,
            out _, out var endType, out _, out _, out _))
        return false;
    return endType == 1;
}
```

**Phase 1 skip-check at `RepairLongLineOfSightBreaks:2877-2878`:**
guard the `if (horizontal > LongSegmentLosRepairThreshold && !HasLineOfSightSafe(...))` block
with `&& !IsOffMeshSegment(mapId, segmentStart, segmentEnd)`. Also
skip the subsequent densification (`AppendDensifiedSegment`) when the
pair is off-mesh — emit the endpoint directly without midpoints.

**Memoization** (similar to `_segmentValidationCache` at
Navigation.cs:535-583): a per-`CalculatePath` `_offMeshSegmentCache`
on `(mapId, x, y, z)` keys, cleared at `CalculateValidatedPath` entry.

**Unit test**: construct a synthetic 3-corner path where corners 1+2
are connected via a known OG zep off-mesh entry. Call
`RepairLongLineOfSightBreaks` directly. Assert: no LOS repair logged,
no densification midpoints inserted between corners 1+2.

**Regression test**: full
`OgZeppelinCliffFallParityTests` (existing 4/4) must remain green.

**Build**: `dotnet build PathfindingService.Tests` (the helper
lives in the `Navigation.cs` library project). No native rebuild.

**Acceptance**:
1. New unit test green.
2. `OgZeppelinCliffFallParityTests` 4/4 still green (no regression
   on existing off-mesh handling).
3. Helper added with documentation comment + memoization wired.
4. `RepairLongLineOfSightBreaks` has the skip-check guarded by the
   helper.

**Expected closure**: A5.2 alone does NOT close any of the 4 failing
tests. Closure depends on A5.5 deploying the 4 new off-mesh entries.
A5.2 is a substrate ship-able as a standalone improvement: it makes
the existing OG zep entries faster (per A5.4 measurement) and
removes the failure-mode risk for any future off-mesh deployment.

### Blocked / questions for user
None as of 2026-05-18.

## History

Phase 0 closure detail, S1.0 / S1.15 / S1.17 / S1.19 landing detail, and the
2026-05-10 → 2026-05-16 pathfinding iteration log are archived in
[`ARCHIVE.md`](ARCHIVE.md). The slot/phase model landed 2026-05-11; the phase
reorder for action/task priority landed 2026-05-12; the AOTA architecture
deep-dive and end-state spec/plan reorganization landed 2026-05-17.
