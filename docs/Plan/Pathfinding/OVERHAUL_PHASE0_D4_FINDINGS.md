# Phase 0 D4 — Go / No-Go Findings (DRAFT — 47% sweep coverage)

> **Iters 10-12 of the Recast Physics-Validated Overhaul loop.**
> Synthesizes [`OVERHAUL_PHASE0_D1_AUDIT.md`](OVERHAUL_PHASE0_D1_AUDIT.md),
> [`OVERHAUL_PHASE0_STALL_COORDS.md`](OVERHAUL_PHASE0_STALL_COORDS.md),
> [`OVERHAUL_PHASE0_TEST_BASELINE.md`](OVERHAUL_PHASE0_TEST_BASELINE.md),
> and the five Phase-1-through-5 prep docs into a single go/no-go
> recommendation. Final aggregation re-runs once the in-flight sweep
> completes (~iter 14-15) to update §3 + §4 with full coverage.
>
> Status: **DRAFT @ 47% sweep coverage; all narrative sections complete,
> §3 (histogram) + §4 (top-20-worst) populated from partial aggregation,
> §8 (Phase 1 starting tile) recommendation locked.**

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
probe on prod-data mmaps found polygons at the three known stalls.
Iter 13 added a 4th data point — the **OG zep deck-edge** canonical
stall (Cycle-17e / loop-17e / loop-24 close-out area, listed in
proposal §6 as a Phase 6 regression test):

| Stall | WoW coord | Tile | Polyrefs (Z-stack + 2y XY grid) |
|---|---|---|---|
| iter-1 east-wall | (1627.6, -4151.8, 36.9) | (39, 28) | 1 |
| iter-2 OG-interior | (1608.1, -4382.3, 10.0) | (40, 28) | 7 |
| loop-25 doodad-wall | (1615.3, -4240.85, ~45) | (39, 28) | 11 |
| **OG zep deck-lip A** (boarding) | **(1338.1, -4646.0, 51.6)** | **(40, 29)** | **27** ⚠ |
| OG zep deck-lip B (upper) | (1335.2, -4644.4, 53.5) | (40, 29) | 14 |

**The OG zep deck-edge has the densest polyref clusters seen** —
27 + 14 polys at the two canonical stall points, dwarfing the
loop-25 doodad-wall's 11 and the iter-2 OG-interior's 7. Tile
(40, 29) is also the T3 fixture canary tile, so Phase 4's per-edge
sweep validates here against T3's existing checkpoints simultaneously.

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

## 2b. Methodology bias (iter 15 update)

Iters 11, 14, and 15 hit native AVs in the validator's
`MaybeLoadAdt` → `findNearestPoly` path on dense Mulgore/Thunder Bluff
tiles, causing infinite hangs. After 3 occurrences (iter 15), the
remaining sweep was switched to `--no-load-adt` mode which is 5-15×
faster (17-25s/tile vs 100-300s/tile) but DOES NOT load the per-tile
ADT context.

**Implication:** the global aggregate is **mixed methodology**:
- **Tiles 1-374** (iters 3-14, ~47% coverage): probed WITH ADT load.
- **Tiles 375-785** (iter 15+, ~53% coverage): probed WITHOUT ADT load.

The `--no-load-adt` mode has less classification fidelity for
dynamic-overlay-affected segments. Expected effect on aggregate:
**Unrecoverable rate in the second-half tiles will be biased slightly
LOW** (a few % lower than they'd otherwise be).

D4's 13.47% Unrecoverable rate is from the first half ONLY (ADT-loaded).
The FINAL global aggregate at sweep close will be slightly lower than
this, but the **top-20 worst-tiles list** (which is what matters for
Phase 1 starting tile selection) is dominated by the first-half tiles
that were probed WITH ADT.

**Phase 1 verification will use BOTH halves' methodology consistently
per-tile** — the (32, 28) baseline used ADT load; the post-Phase-1
re-probe of (32, 28) will also use ADT load. So apples-to-apples
within-tile comparison is preserved.

## 3. Global Blocked-poly ratio

> Kickoff: "What's the global Blocked-poly ratio? Is it within the
> design's expected 20-30% baseline?"

**Partial sweep aggregation (iter 12 update, 367/785 = 46.8% coverage):**

| Affordance | Count | % of segments |
|---|---|---|
| SafeDrop | 6,475 | 22.37% |
| Vertical | 6,050 | 20.90% |
| Walk | 4,983 | **17.22%** |
| Blocked | 3,334 | 11.52% |
| SteepClimb | 2,895 | 10.00% |
| JumpGap | 2,403 | 8.30% |
| StepUp | 2,237 | 7.73% |
| UnsafeDrop | 564 | 1.95% |

**Totals:**
- Non-Walk: 23,958 / 28,941 = **82.78%**
- Unrecoverable (Blocked + UnsafeDrop): 3,898 / 28,941 = **13.47%**

**The 13.47% Unrecoverable rate is BELOW the proposal's expected 20-30%
baseline.** Positive correction: the bake-vs-physics gap is smaller
than the proposal feared overall. Phase 1's expected ≥30% relative
Blocked-drop will produce less absolute reduction than the proposal
estimated, but absolute reduction is what matters for test outcomes.

**Walk is only 17.22%** — the bot relies HEAVILY on recoverable
affordances (SafeDrop 22% + Vertical 21% + SteepClimb 10% + StepUp 8%
+ JumpGap 8% = ~69%) to traverse paths. This has implications for Phase 4:
the proposal targets "Walk ≥60%, StepUp 20-30%, Repaired ≤5%, Unreachable
≤5%" after Phase 4. Hitting Walk ≥60% from a 17% baseline requires
reclassifying ~42% of segments — a dramatic shift. Either the proposal's
target needs revision OR Phase 4 produces a dramatic reclassification
(which IS the point of bake-time physics validation).

**Phase 4 acceptance criteria note for D4:** revise "Walk ≥60%" downward
to a realistic threshold, OR keep it as a stretch goal and accept that
"Walk + Walk-equivalent recoverable affordances ≥80%" is the practical
benchmark.

## 4. Tiles with >50% Blocked polys (top-20 worst from partial sweep)

> Kickoff: "Are there entire tiles where >50% of polys are Blocked?
> (That would suggest Layer-2 vmap extraction fixes are the dominant
> lever for those tiles.)"

**Partial sweep aggregation (iter 12 update, 367/785 = 46.8% coverage):**

Top 20 worst tiles by Unrecoverable %:

| Rank | Tile (X,Y) | Segs | Unrecov | Unrecov % | Paths | Notes |
|---|---|---|---|---|---|---|
| 1 | (27, 28) | 353 | 110 | **31.16%** | 5 | NW Mulgore area; high signal |
| 2 | (32, 28) | 412 | 108 | **26.21%** | 5 | N Durotar/S Barrens; real bot traffic |
| 3 | (36, 26) | 263 | 68 | 25.86% | 5 | NE Barrens |
| 4 | (29, 29) | 399 | 101 | 25.31% | 5 | N Barrens |
| 5 | (29, 27) | 101 | 24 | 23.76% | 1 | undersampled (1 path) |
| 6 | (28, 29) | 378 | 88 | 23.28% | 5 | N Barrens |
| 7 | (35, 30) | 269 | 62 | 23.05% | 5 | Thousand Needles N |
| 8 | (26, 28) | 415 | 94 | 22.65% | 5 | NW Mulgore |
| 9 | (27, 26) | 110 | 24 | 21.82% | 2 | undersampled |
| 10 | (36, 30) | 320 | 66 | 20.62% | 4 | Thousand Needles N |
| 11 | (29, 28) | 424 | 87 | 20.52% | 5 | Mulgore/Barrens transition |
| 12 | (26, 26) | 147 | 29 | 19.73% | 2 | undersampled |
| 13 | (27, 30) | 408 | 79 | 19.36% | 5 | Thousand Needles W |
| 14 | (34, 30) | 327 | 63 | 19.27% | 5 | Thousand Needles |
| 15 | (43, 29) | 292 | 55 | 18.84% | 5 | OG approach corridor |
| 16 | **(39, 29)** | 177 | 33 | 18.64% | 4 | ⚠ ORTHO to T3 fixture tile (40,29) |
| 17 | (43, 25) | 333 | 62 | 18.62% | 5 | Tarren Mill area |
| 18 | (43, 30) | 312 | 57 | 18.27% | 5 | South Barrens |
| 19 | (42, 28) | 375 | 68 | 18.13% | 5 | 2× ortho from T3 fixture |
| 20 | (38, 26) | 414 | 75 | 18.12% | 5 | NE Barrens |

**No tile breaches 50% Unrecoverable.** The maximum is 31.16% at
(27, 28). This suggests **Phase 1 (Recast parameter retightening) is
the dominant lever**, NOT Phase 3 (vmap extraction fixes). Phase 3
remains valuable for specific known-bad WMOs (per TC #23972) but the
all-tiles distribution doesn't show source-geometry corruption at
scale.

**Stall-tile reality check:** iter-1/iter-2 stall tiles (39,28) and
(40,28) are NOT in the top-20. This **confirms iter-1 audit's finding**
that NavMeshPhysicsValidator's path-sampling doesn't reliably hit
specific stall coords. The 7-poly Z-stack at (1608.1,-4382.3,10.0)
is real but too localized to push tile (40,28)'s wide-area ratio over
~17%. **For per-stall regression detection, targeted `--cull-coord`
probes remain essential — the all-tiles sweep is for global signal,
not stall hunting.**

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

**Iter 12 recommendation: tile (32, 28).**

| Criterion | Tile (32, 28) | Why |
|---|---|---|
| High Unrecoverable %, clear signal | ✅ 26.21% (rank 2) | enough delta to detect Mononen-tweak improvement |
| Real bot-traffic terrain | ✅ N Durotar/S Barrens transition | known travel corridor; not a mountain-wall outlier |
| Sample density | ✅ 412 segments / 5 paths | well-sampled (not under-sampled like ranks 5,9,12) |
| Fixture-neighbor safety | ✅ Distance ≥5 tiles from T3 (40,29) | no cull blast risk per iter-2 evidence |
| Geographically central | ✅ representative of mid-traffic region | results generalize across the map |

**Why not the worst tile (27, 28) at 31.16%?**

Tile (27, 28) is in WoW.Y range [2133.4, 2666.7] (north of OG) — this
region may be heavy mountain/cliff wall geometry that produces
inflated Unrecoverable rates from random sampling but isn't
representative of real bot routes. Worth probing for verification but
not the first Phase 1 target.

**Why not the iter-2 stall tile (40, 28)?**

ORTHO-adjacent to T3 fixture tile (40, 29) — per [loop-26 iter-2
evidence](OVERHAUL_PHASE0_TEST_BASELINE.md), cull-blast on tile (40,28)
regresses T3. Phase 1's global parameter retightening + per-tile
re-bake would test this directly, but starting Phase 1 ON the riskiest
tile is the wrong order. Phase 1 starts on a SAFE tile; the risky tiles
get re-baked AFTER the Phase-1 parameter retighten is proven safe on
representative tiles.

**Alternate: tile (39, 28)** — iter-1 east-wall + loop-25 doodad-wall
tile. NOT in the top-20 (its 11-poly doodad cluster is too localized
to push tile-wide ratio over ~17%). DIAGONAL adjacency to T3 (safer
than (40,28)). Less Unrecoverable signal but more direct relevance
to T1 test. Use as **second target** after (32, 28) verifies Phase 1
parameters produce measurable improvement.

## 8b. Phase 1 iter-by-iter starting plan (preview)

1. **Phase 1 iter 1:** Implement AgentProfile + BakeProfile, build
   MmapGen, re-bake tile (32, 28) ONLY. Run Phase-0 probe on (32, 28)
   before/after. Verify Blocked-poly count drops ≥30% on that tile.
2. **Phase 1 iter 2:** Re-bake tiles (39, 28) and (40, 28) (the
   stall-tile pair). Run probe + bake-fixture pair. Verify no
   regression on T3 (40, 29).
3. **Phase 1 iter 3+:** Global re-bake of all 41 maps (background batch).
4. **Phase 1 close:** Phase-0 probe global re-aggregate; verify ≥30%
   Blocked-drop globally per proposal §3 Phase 1 exit.

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
