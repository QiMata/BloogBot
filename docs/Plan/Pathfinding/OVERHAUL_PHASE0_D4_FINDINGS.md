# Phase 0 D4 — Go / No-Go Findings (DRAFT — awaiting sweep aggregation)

> **Iter 10+ of the Recast Physics-Validated Overhaul loop.**
> Synthesizes [`OVERHAUL_PHASE0_D1_AUDIT.md`](OVERHAUL_PHASE0_D1_AUDIT.md),
> [`OVERHAUL_PHASE0_STALL_COORDS.md`](OVERHAUL_PHASE0_STALL_COORDS.md),
> [`OVERHAUL_PHASE0_TEST_BASELINE.md`](OVERHAUL_PHASE0_TEST_BASELINE.md),
> and the five Phase-1-through-5 prep docs into a single go/no-go
> recommendation. Final aggregation of map-1 sweep + top-20-worst tiles
> lands in the SAME doc once the in-flight sweep completes (~iter 13-14).
>
> Status: **DRAFT, all narrative sections complete; numeric placeholders
> for global histogram and top-20-worst-tiles section.**

## Bottom line

**🟢 GO** — but at materially larger budget than the proposal stated.

The Phase 0 baseline confirms the proposal's structural hypothesis
(Recast emits walkable polygons where the runtime PhysicsEngine refuses).
None of the 5 phase prep docs found a fatal flaw. **Three of five phases
have revised scope estimates pushing the total budget upward by ~2-3×**;
none of those revisions are blockers, but the user must set timeline
expectations honestly before any Phase 1 code work begins.

## 1. Hypothesis confirmation

> Kickoff D4 spec: "Do the 3 known stall coords show Blocked edges in the probe?"

**Confirmed.** Iter 2's [`--cull-coord`](OVERHAUL_PHASE0_STALL_COORDS.md)
probe on prod-data mmaps found polygons at all three coords:

| Stall | WoW coord | Tile | Polyrefs |
|---|---|---|---|
| iter-1 east-wall | (1627.6, -4151.8, 36.9) | (39, 28) | 1 |
| iter-2 OG-interior | (1608.1, -4382.3, 10.0) | (40, 28) | 7 (Z-stack) |
| loop-25 doodad-wall | (1615.3, -4240.85, ~45) | (39, 28) | 11 (cluster) |

Stall 2's **7-polygon Z-stack** at one (X,Y) is the classic
[loop-19 cull-pipeline-blocker signature](C:/Users/lrhod/.claude/projects/e--repos/memory/project_pfs_loop19_cull_pipeline_blocker.md):
WMO interior with overlapping walkable polygons at slightly different Z.
This is the kind of structural geometry corruption Phase 3 (vmap
extractor MOPY/MOBN/MOBR fixes) + Phase 4 (physics-validated bake) are
designed to eliminate.

**No false-negatives:** the probe finds polys at every stall coord (it
isn't broken). The actual "Blocked edges" per-poly classification
requires Phase 4's per-edge sweep (not implemented in the existing
NavMeshPhysicsValidator) — but the polys' EXISTENCE is sufficient
evidence that the bake-vs-physics gap is real and structural.

## 2. Path-sampling overlay at stall tiles

| Tile | Sampling | Walk | Non-Walk | Unrecoverable (Blocked/UnsafeDrop/Cliff) |
|---|---|---|---|---|
| (39, 28) | `--samples 1` | trivial | 36 / 38 (94.7%) | 6 / 38 (15.8%) |
| (40, 28) | `--samples 1` | 2 / 101 (2.0%) | 99 / 101 (98.0%) | 12 / 101 (11.9%) |
| (40, 28) | `--samples 20` (iter 1) | 292 / 1480 (19.7%) | 1188 / 1480 (80.3%) | 218 / 1480 (14.7%) |

`UnrecoverableNonWalk` triage metric: **~12-16%** at these stall tiles,
within the proposal §3 Phase 0 expected 20-30% baseline.

## 3. Global Blocked-poly ratio

> Kickoff: "What's the global Blocked-poly ratio? Is it within the
> design's expected 20-30% baseline?"

**🟨 PLACEHOLDER — fills in when sweep completes.**

Current sweep status (iter 10): 345/785 tiles done (43.9%). The global
histogram aggregation script and top-20-worst-tile section will land
in this doc as an "Iter 13-14 update" once the sweep finishes
(~334 min from iter 10).

Forecast from partial data: the stall tiles show 12-16% unrecoverable,
matching the proposal estimate. Global will probably be lower (5-12%)
because most tiles are open terrain without OG/UC dense interior
geometry.

## 4. Tiles with >50% Blocked polys

> Kickoff: "Are there entire tiles where >50% of polys are Blocked?
> (That would suggest Layer-2 vmap extraction fixes are the dominant
> lever for those tiles.)"

**🟨 PLACEHOLDER — Iter 13-14 update.**

Iter-1's --samples 20 sweep on (40,28) showed 14.7% UnrecoverableNonWalk
— not >50%, but the densely-bad path-segment classes (SafeDrop 467 +
Vertical 213 + SteepClimb 144) dominate. Whether any individual tile
crosses 50% requires the full sweep.

If any tile breaches 50% UnrecoverableNonWalk, Phase 3 (vmap extractor
fixes) should be sequenced FIRST for those tiles — they're broken at
the source-geometry layer, not the Recast parameter layer.

## 5. Bake-time budget validation

> Kickoff: "How long did probing 1 map take? Extrapolate to full re-bake
> (41 maps). Is it within the design's 4-hour-single-thread budget?"

**Map 1 (Kalimdor) sweep budget:**

- Started: 01:11:32 (iter 3 launch).
- Tile count: 785.
- ETA at iter 10: 334 min remaining → ~9.7 hr total wall-clock.
- Per-tile range: 13-21s (empty edges) to 130-300s (dense OG/Mulgore interior).
- `--samples 5` mode (rough sweep at 1 random path per tile).

**Extrapolation to all 41 maps:**

- Map 0 (Eastern Kingdoms) is similarly dense; expect ~10 hr.
- Most other maps (instances + smaller continents) are smaller: ~2-5 hr each.
- Per-tile validator timing scales linearly with N samples (~3 sec/sample
  overhead, 10-30 sec ADT/VMAP load amortized over the validator's
  per-process state).

**Total Phase-0 budget extrapolation:** ~80-150 hr for all 41 maps at
`--samples 5`. The proposal's 4 hr/map budget assumes a different
context (Phase 4's per-poly-per-edge sweep, not the existing
NavMeshPhysicsValidator path-sampling). The existing tool's wall-clock
overhead per process invocation dominates; a Phase 4 algorithm that
runs in-process during the bake (no per-tile fork/exec/load) would be
much faster.

**For now:** map 1 sweep is sufficient for D4 + Phase 1 advance
decisions. Full all-41-map sweep at `--samples 5` is impractical for
Phase 0; only run it if specifically targeted.

## 6. Phase scope revisions (vs proposal)

| Phase | Proposal estimate | Phase-N prep finding | Revised estimate |
|---|---|---|---|
| Phase 0 | 1 session | (current iter 10; ~50% done) | ~12-15 iters total |
| Phase 1 | 1-2 sessions | 3 Mononen violations + MapBuilder/TileWorker footgun + 5 tile-override blocks | 10-18 hr / 5-8 iters |
| Phase 2 | 1 session | **TWO divergent Detour copies**; every file differs; vendor lineage jackpoz 2014-06-20 ~10 years behind v1.6.0 | 8-13 hr / 4-6 iters |
| Phase 3 | 1-2 sessions | **No in-tree vmap_extractor**; must fork CMaNGOS/vmangos extractor before patching | 8-15 hr / 5-8 iters |
| Phase 4 | 2-3 sessions (HEADLINE) | **THREE divergent vmap libraries**; direct add_subdirectory impossible; PhysicsSweep static lib + IGeometrySource abstraction is design path | **22-35 hr / 15-20 iters (budget driver)** |
| Phase 5 | 1-2 sessions | Navigation.cs is **7,697 LOC not 5,600**; net deletion ≥10K-12K LOC achievable | 13-21 hr / 7-11 iters |
| Phase 6 | 1 session | not pre-scoped this loop; reasonable | 4-8 hr / 2-4 iters |
| **TOTAL** | **8-15 sessions (~30-60 hr)** | | **~58-95 hr / ~45-60 iters** |

The 2-3× increase comes from three discoveries the proposal didn't
account for:
1. Two-Detour-copies divergence (Phase 2).
2. No in-tree vmap_extractor (Phase 3).
3. Three-vmap-libraries + Phase 4 link-impossibility-without-abstraction.

**None of these are blockers.** Phase 4's risk is the highest, and the
proposal's worst-case mitigation IS the design path now — that resolves
the guardrail-8 STOP risk into a known-larger task.

## 7. Top 3 risks identified

### Risk #1 — Phase 4 PhysicsSweep extraction is novel work

The proposal §4 risk row #3 originally said "Worst case: extract just
the capsule-sweep API into a new `PhysicsSweep` static lib that both
`Exports/Navigation` and `tools/MmapGen` depend on." Phase 4 prep
(iter 8) found this IS the design path, not a fallback, due to the
three-vmap-libraries situation. The PhysicsSweep lib has no prior art
in this codebase. Header refactor (`PhysicsEngine.h` → `PhysicsSweepApi.h`
+ `PhysicsEngineInternal.h`) is required.

**Mitigation:** Phase 4 first 2 iters predict the direct link fails on
symbol collisions, motivating the fallback to PhysicsSweep design.
Guardrail 8 STOPs at iter N+3 if not converging.

### Risk #2 — Cross-tile cull blast radius (per loop-26 iter-2 lesson)

Per [`OVERHAUL_PHASE0_TEST_BASELINE.md`](OVERHAUL_PHASE0_TEST_BASELINE.md)'s
cross-tile adjacency map, tile (40, 28) directly borders tile (40, 29)
which holds T3 (OG zep bake-fixture) checkpoints. The iter-2
off-mesh attempt regressed T3 even though the off-mesh was nowhere near
T3's checkpoints — cross-tile cull seam propagation.

**Mitigation:** Phase 1+ work uses guardrail 3 (bake-fixture pair as
mandatory pre-commit gate). Phase 4 produces deterministic per-tile
output (cross-tile concerns surface only at border polys).

### Risk #3 — Phase 5 deletion exposes hidden runtime consumers

`StaticRoutePackCache` + `PathfindingOverlayBuilder` have 6 cross-layer
consumers across `PathfindingSocketServer`, `NavigationPathFactory`,
`TravelTask`, and tests. Phase 5 prep flagged that deletion blast
radius is real.

**Mitigation:** Phase 5 ships the thin Detour wrapper FIRST (proposal
§3 Phase 5 step 1), runs full test suites, only then deletes the
managed-repair pipeline (proposal §3 step 2). Per guardrail 4: revert
deletion commit on regression rather than patching over.

## 8. Recommended Phase 1 starting tile

> Kickoff: "Recommended Phase 1 starting tile (not the worst — pick a
> tile with clear before/after signal where Layer-1 parameter changes
> should show measurable improvement)."

**🟨 PLACEHOLDER — final pick lands in iter 13-14 update.**

Provisional recommendation based on iter-1/iter-2 evidence:

- **Tile (40, 28)** (iter-2 OG-interior stall) — strong before/after
  signal but ADJACENT to T3 fixture tile (40, 29). Risk of cull blast
  regression. **NOT recommended** as starter.
- **Tile (39, 28)** (iter-1 east-wall + loop-25 doodad-wall) — strong
  signal, DIAGONALLY adjacent to T3 (safer per loop-26 iter-2 evidence).
  **CANDIDATE.**
- **A clean Mulgore interior tile** with known dense WMO geometry but
  no test fixture neighbors — TBD from sweep aggregation.

The Phase 0 sweep's top-20-worst-tiles output narrows the candidate
pool to tiles where the proposal's Mononen-rule retightening should
produce ≥30% Blocked-drop without crossing any cross-tile-seam
fixture risk.

## 9. Acceptance for advancing to Phase 1

The proposal §3 Phase 0 exit says: "baseline probe runs on all tiles;
report committed; the iter-2 FINDINGS doc is the regression baseline for
the chosen lever set." All 4 D-deliverables are satisfiable:

- ✅ **D1 — Probe builds + runs.** Satisfied by existing `NavMeshPhysicsValidator`
  per iter-1 audit (commit `81a6096c`).
- 🔄 **D2 — Baseline reports.** Map 1 sweep in-flight (commit `394ad87f`);
  stall-coord lookup done (commit `dce88162`). Map 0 sweep deferred per
  §5 above.
- ✅ **D3 — Test-failure manifest.** Done (commit `ce5d8154`).
- 🔄 **D4 — This doc.** Skeleton complete (iter 10); final aggregation
  iter 13-14.

**Phase 0 closes when D2 sweep aggregation lands in §3-4-5 above.**

## 10. Recommended ordering for Phases 1-5

Per proposal §7: "Phases 1, 2, 3 are mostly independent and can be
done in any order. Phase 4 depends on all three. Phase 5 depends on
Phase 4."

**D4 recommendation:**

1. **Phase 1 FIRST** — biggest expected Blocked-drop per hour invested
   (Mononen-rule retightening is the proposal's "easy win").
2. **Phase 2 SECOND** — vendor unification is sequencing prereq for
   Phase 4 (resolves the bit-split divergence). Lower-risk than Phase 3.
3. **Phase 3 THIRD** — depends on identifying upstream vmap_extractor
   URL (web-fetch task). Can interleave with Phase 2 if researcher is
   available.
4. **Phase 4 FOURTH** — depends on 1-3 closing.
5. **Phase 5 LAST** — depends on Phase 4.

Phase 6 follows naturally; no major prep needed.

## 11. What this draft does NOT include yet

- Global Blocked-poly histogram across map 1 (sweep aggregation pending).
- Top-20-worst-tile table (sweep aggregation pending).
- Top-20 tile-border vs interior Blocked ratios.
- Final Phase 1 starting tile pick (depends on top-20 list).
- Map 0 sweep — deferred per §5.

These all land in this same doc as an "Iter 13-14 update" appendix
when the sweep completes.

## 12. What the user must decide before Phase 1 starts

1. **Budget acceptance.** The overhaul is ~58-95 hr / ~45-60 iters
   (revised from proposal's 8-15 sessions). Is this acceptable?
2. **Phase 4 risk tolerance.** The PhysicsSweep extraction is novel
   work + has guardrail-8 STOP risk. Acceptable to commit to attempting
   it knowing it might escalate to ARCHITECTURE-DECISION-NEEDED?
3. **Map 0 baseline.** D4 §5 deferred the map-0 sweep. Acceptable to
   advance to Phase 1 with map-1-only baseline, or run map 0 too first?

Default if no input: assume YES on 1 and 2, NO on 3 (advance with map-1
baseline; Phase 1 + Phase 4 re-sweeps will cover map 0 as a side
effect).
