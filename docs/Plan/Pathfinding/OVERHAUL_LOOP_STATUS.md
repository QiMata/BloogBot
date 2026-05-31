# Recast Physics-Validated Overhaul — Iter Log

Driven by /loop kickoff prompt 2026-05-31.
Proposal: [RECAST_PHYSICS_VALIDATED_OVERHAUL.md](RECAST_PHYSICS_VALIDATED_OVERHAUL.md)
Kickoff:  [NEXT_SESSION_RECAST_PHYSICS_OVERHAUL_KICKOFF.md](NEXT_SESSION_RECAST_PHYSICS_OVERHAUL_KICKOFF.md)

Each iteration is one bounded unit of work toward the next unmet phase
exit criterion. Every iter ends with a commit + push (R15) regardless
of result. Negative results commit too.

---

## Iter 1 — 2026-05-31 — Phase 0

**Did:** Audited `tools/NavMeshPhysicsValidator` (C#, loop-19→25 cull
pipeline lineage) against Phase 0 Deliverable 1's spec. Ran the
validator against tile 1/40,28 (the loop-26 iter-2 stall tile) at
`--samples 20`: 19/20 paths found, 1480 segments classified, 196
Blocked + 22 UnsafeDrop (14.7% unrecoverable), wall-clock ~3 min per
tile. Output: [`tmp/iter-overhaul-phase0/iter1-probe-1-40-28.json`](../../../tmp/iter-overhaul-phase0/iter1-probe-1-40-28.json).
Wrote [`OVERHAUL_PHASE0_D1_AUDIT.md`](OVERHAUL_PHASE0_D1_AUDIT.md)
documenting the decision: **D1 is satisfied by the existing tool**.
No new C++ probe built. The proposal's per-polygon-per-edge exhaustive
sweep is deferred to Phase 4 (bake-time pass) where it actually
matters; for Phase 0 baseline the path-sampling approach is materially
better — it mirrors runtime usage and produces directly-actionable
non-Walk segments.

**Phase exit criteria progress:**
- D1 (PhysicsValidationProbe builds + runs): ✅ satisfied by existing
  `NavMeshPhysicsValidator` per audit; no new tool needed.
- D2 (baseline reports on map 0 + map 1): ❌ not started; iter 2 work.
- D3 (test-failure manifest): ❌ not started.
- D4 (go/no-go findings): ❌ not started.

**Tests:** No bake performed this iter — read-only against existing
prod-data mmap. No bake-fixture regression check required. No
production code touched.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (new);
docs/Plan/Pathfinding/OVERHAUL_PHASE0_D1_AUDIT.md (new);
tmp/iter-overhaul-phase0/iter1-probe-1-40-28.json (new, ~80KB probe
output, kept for D2 cross-reference).

**Next iter:** Iter 2 starts Phase 0 D2 — targeted-probe the 3 known
stall coords (1627.6,-4151.8,36.9 / 1608.1,-4382.3,10.0 / 1615.3,
-4240.85,~45) via `--cull-coord` for instant signal, then kick off the
all-tiles rough sweep on map 1 (785 tiles × `--samples 5`) as a
background batch. Targeted probes complete in iter 2; rough sweep
spans several iters (est. 13 hours wall-clock per map).

**Blockers/risks:**
- Axis-swap convention in JSON output (Start.X holds WoW.Y, Start.Y
  holds WoW.X) — documented in the audit so D2 summary cross-references
  apply the swap correctly.
- Per-tile wall-clock at `--samples 20` is too slow for 785-tile sweeps;
  iter 2 uses `--samples 5` for rough, with `--samples 50` re-probe on
  bad-flagged tiles.

**Commit:** `81a6096c` `phase(0) iter(1): audit NavMeshPhysicsValidator as the D1 probe`

---

## Iter 2 — 2026-05-31 — Phase 0

**Did:** Ran targeted `--cull-coord` probes on the 3 known stall coords:
(1627.6,-4151.8,36.9) on tile (39,28) → 1 polyref; (1608.1,-4382.3,10.0)
on tile (40,28) → 7 polyrefs (multi-Z WMO interior stack); (1615.3,
-4240.85,45) on tile (39,28) → 11 polyrefs (doodad-wall cluster).
All 3 stall coords have polygons in the existing bake — confirms the
proposal's hypothesis that Recast emits walkable polys where the runtime
PhysicsEngine refuses. Wrote [`OVERHAUL_PHASE0_STALL_COORDS.md`](OVERHAUL_PHASE0_STALL_COORDS.md)
with the polyref enumeration + path-sampling overlay stats.
Determined the cull-coord CLI axis convention: raw WoW.X,Y,Z order is
correct (test A found 5 polys, swapped test B found none); aligns with
PathPhysicsProbe `--start` convention. The validator's internal
`TileWorldBounds` labels axes confusingly but the cull-coord pipeline
does not touch it.

**Phase exit criteria progress:**
- D1 (probe builds + runs): ✅ closed in iter 1.
- D2 (baseline reports): ✅ "3 stall coords lookup" sub-deliverable done.
  Still pending: global histogram across all tiles + top-20 worst tiles
  per map + cross-tile-seam stats (all need the all-tiles sweep — iter 3).
- D3 (test-failure manifest): ❌ not started.
- D4 (go/no-go findings): ❌ not started.

**Tests:** No bake performed; read-only against existing prod-data mmap.
No bake-fixture regression check required. No production code touched.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE0_STALL_COORDS.md
(new); docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 2 entry +
back-fill of iter 1's commit hash); tmp/iter-overhaul-phase0/iter2-{test-a,test-b,stalls-39-28,stalls-40-28}.json
(new, local-only — `tmp/` gitignored).

**Next iter:** Iter 3 kicks off the all-tiles rough sweep on map 1
(785 tiles × `--samples 5`, ~13 hr serial wall-clock) as a background
batch. Outputs go to `tmp/iter-overhaul-phase0/sweep-map1/`. After
spawning the batch, iter 3 commits the launcher script and
ScheduleWakeups at 1800s for the first progress check. Iter 4
monitors / starts a parallel map-0 sweep depending on progress.

**Blockers/risks:**
- The validator runs serial (one tile per process invocation). For a
  multi-day Phase-0 budget this is acceptable; for production CI
  (Phase 6) we'd want a parallelized variant.
- The validator's `TileWorldBounds` axis-label confusion is real but
  internally consistent — does not affect cull-coord results; flagged
  in [`OVERHAUL_PHASE0_D1_AUDIT.md`](OVERHAUL_PHASE0_D1_AUDIT.md) §axis-convention.

**Commit:** `dce88162` `phase(0) iter(2): targeted-probe 3 known stall coords (D2 sub-deliverable)`

---

## Iter 3 — 2026-05-31 — Phase 0

**Did:** Wrote `tools/scripts/phase0-sweep-map.ps1`, an idempotent launcher
that enumerates `.mmtile` files for a map and invokes
`NavMeshPhysicsValidator --samples 5 --silent` on each, writing one JSON
per tile to `tmp/iter-overhaul-phase0/sweep-map<M>/`. Skipping rule: if
the per-tile JSON already exists, the tile is skipped — so the script
can be Ctrl-C'd and resumed without losing work. Two PS-5.1 footguns
fixed during iter 3: (a) the spaced repo path `e:\repos\Westworld of
Warcraft\...` must be inner-quoted when passed via Start-Process
`-ArgumentList ... -File`; (b) native command `2>&1` capture wraps stderr
as ErrorRecord under `$ErrorActionPreference=Stop` — replaced with
`2>$null` and per-iteration EAP scoping.

Launched the full map-1 sweep as a detached `powershell.exe` process
(`pid=29900`, recorded in `tmp/iter-overhaul-phase0/sweep-map1.pid`).
First-tile timing on full-sweep run: 27.4s/tile (mid-density tile 1,1);
empty/ocean tiles at 14-15s. ETA reported by the launcher itself:
~357 min = **~6 hours wall-clock** for 785 tiles — half the original
13-hr estimate.

**Phase exit criteria progress:**
- D2 (baseline reports): in flight — map-1 all-tiles sweep running.
  Will produce 785 per-tile JSON for aggregation in iter 5+. Map-0 sweep
  not started yet (sequential to keep native-side state simple).
- D3 / D4: ❌ not started.

**Tests:** No bake performed; read-only against existing prod-data mmap.

**Files changed:** tools/scripts/phase0-sweep-map.ps1 (new, 70 LOC);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter-3 entry + iter-2
commit-hash back-fill).

**Next iter:** Iter 4 wakes in ~30 min for the first progress check.
At ~120s/tile (worst-case estimate including OG/UC density),
~15 tiles done in 30 min. Iter 4 reads the log tail, confirms forward
progress, computes refined ETA. If the process died, restart it
(script is idempotent). If healthy, ScheduleWakeup 1800s for next check.

**Blockers/risks:**
- Background detached process risk: if the host machine reboots or PS
  process is killed externally, the sweep stops. Recovery is `Start-Process`
  on the same script (idempotent). Iter 4+ checks pid liveness via
  `Get-Process -Id <pid>` against `sweep-map1.pid`.
- ETA accuracy depends on whether dense Kalimdor tiles (Durotar, Mulgore,
  Orgrimmar interior, Thunder Bluff WMOs) take 60-120s each. Iter 4
  can tighten the estimate.

**Commit:** `394ad87f` `phase(0) iter(3): launch map-1 all-tiles rough sweep (D2 in-flight)`

---

## Iter 4 — 2026-05-31 — Phase 0

**Did:** Progress-checked the map-1 sweep: pid 29900 alive (low CPU,
mostly I/O wait), 127/785 tiles done in 35.8 min, 3.5 tiles/min steady
state, ETA 184 min remaining. Per-tile timing 13-21s/tile — all map-1
edge tiles processed first (ocean/empty), denser interior tiles
upcoming. Zero errors. With the sweep self-managing, iter 4 took up
**Phase 0 D3** (test-failure baseline manifest) from existing loop-25/26
memory evidence (kickoff explicitly allows this rather than re-running
the 5-8 min/test live runs).

Wrote [`OVERHAUL_PHASE0_TEST_BASELINE.md`](OVERHAUL_PHASE0_TEST_BASELINE.md):
4 tests captured — T1 CrossroadsToUndercity (FAIL, pathfinding-class,
linked to iter-2 OG-interior 7-poly Z-stack); T2 OrgrimmarToUndercityZep
(FAIL, NON-pathfinding-class per guardrail 10 — vmangos transport
schedule); T3 OgZeppelin_BakeFixtureValidation (PASS post-iter-2 revert,
pathfinding-class canary); T4 BrmDungeon_BakeFixtureValidation (PASS,
canary). Baseline frozen at commit `7ca9f84c`. Includes cross-tile
adjacency map highlighting (40,28)↔(40,29) as HIGH-risk neighbor pair.

**Phase exit criteria progress:**
- D2 (baseline reports): in flight, ~16% done by tile count.
- **D3 (test-failure manifest): ✅ done.** Frozen baseline captures all
  4 tests' current state, failure mode, classification, polyref linkage.
- D4 (go/no-go findings): ❌ not started; iter 6-7 work after D2 done.

**Tests:** No bake, no live tests run — manifest built from prior
evidence. No production code touched.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE0_TEST_BASELINE.md
(new); docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 4 entry).

**Next iter:** Iter 5 wakes in ~30 min for second sweep progress check
(should be ~245/785 ≈ 31% done). If sweep healthy, iter 5 also adds
per-edge classification for T1's 7 stall polys via PathPhysicsProbe
(small scoped work, ~30 min). If sweep dead, restart it and skip the
per-edge work.

**Blockers/risks:**
- None new. T2's guardrail-10 fix is queued for Phase 5 wrap-up.
- T3's adjacency to iter-1/2 stall tiles means Phase 1 global parameter
  changes will need cross-tile-seam validation before promotion. Iter 4's
  manifest documents this; Phase 1's actual mitigation is the bake-fixture
  pair pre-commit gate (guardrail 3).

**Commit:** `ce5d8154` `phase(0) iter(4): D3 test-failure baseline manifest from prior evidence`

---

## Iter 5 — 2026-05-31 — Phase 0

**Did:** Sweep progress-checked at 248/785 (31.5%, ETA 160 min, healthy,
pid 29900 alive). Pivoted the planned per-edge-classification work
(iter-2 evidence already proves smooth-path segments at T1 stall
classify ALL Walk Clear — runtime per-segment probes can't detect the
wall-collision-creep failure; re-running would be redundant signal).
Iter 5 instead built Phase 1 PREP via read-only code inventory.

Wrote [`OVERHAUL_PHASE1_PREP.md`](OVERHAUL_PHASE1_PREP.md) — work-breakdown
for Phase 1: AgentProfile + BakeProfile struct sketch (~80 LOC), current
bake-default vs proposal-target table identifying **two non-Mononen
violations** (`ch=cs=0.2666` instead of `ch=cs/2`; `walkableSlopeAngle=75°
> physics 60°`) as the biggest correctness gaps. Surface area scoped:
TileWorker.cpp 12,197 LOC (replace `getDefaultConfig` + `from_json`),
MapBuilder.cpp:547-550 (the "Dont ask me why" two-cell-size footgun
between `BASE_UNIT_DIM=0.2666` and `BASE_UNIT_DIM_MAP_BUILDER=0.13`),
config.json 277 lines (delete 5 per-tile bake-param blocks, preserve 34
`_README` + 8 `_NEGATIVE_RESULT_*` per proposal §3 Phase 1 step 4).
Phase 1 estimated 10-18 hr wall-clock, 5-8 iters; starts ONLY after D4
go-decision (expected iter 7-8 after sweep done).

**Phase exit criteria progress:**
- D2 (baseline reports): in flight, 31.5% by tile count.
- D4 (go/no-go findings): ❌ not started; this prep doc feeds the
  Phase-1-readiness section of D4.

**Tests:** No bake, no live tests, no code modified — read-only.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE1_PREP.md (new);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 5 entry).

**Next iter:** Iter 6 wakes in ~30 min for third sweep progress check
(should be ~350-380/785, ETA ~2 hr remaining). If sweep healthy, iter 6
does Phase 2 prep (Recast 1.6.0 vendor upgrade scope analysis) OR starts
the map-0 sweep if hardware capacity allows (it doesn't — both maps
serial through the same validator native state).

**Blockers/risks:**
- The "outdoor vs indoor" cs split is real but config doesn't have
  per-tile indoor flags. Phase 1 first-pass uses `indoor = false`
  globally and accepts the indoor refinement debt for iter 9+.
- Phase 1's tile-size derivation may not produce the exact 533.33y
  world unit per tile that MmapGen's 32x32 grid assumes; needs check
  during Phase 1 implementation.

**Commit:** `37f414c0` `phase(0) iter(5): Phase 1 prep — bake-param inventory + AgentProfile sketch`

---

## Iter 6 — 2026-05-31 — Phase 0

**Did:** Sweep progress-checked at 280/785 (35.7%, ETA 207 min, slowed
from 184 because dense Mulgore/Thunder Bluff WMO tiles hit 78-135s/tile
vs 14-21s edge tiles). pid 29900 alive. Wrote [`OVERHAUL_PHASE2_PREP.md`](OVERHAUL_PHASE2_PREP.md)
documenting the recastnavigation vendor inventory.

**MAJOR Phase 2 finding:** Two DIFFERENT Detour vendor copies exist
in-tree — [`tools/MmapGen/dep/recastnavigation/Detour/`](../../../tools/MmapGen/dep/recastnavigation/Detour/)
(bake-time, 9 of 9 header files differ from runtime) and
[`Exports/Navigation/Detour/`](../../../Exports/Navigation/Detour/)
(runtime, has extra `DetourPathCorridor.{h,cpp}` for the corridor-fallback
helper). Both advertise `DT_NAVMESH_VERSION=7` but implementations
differ — wire-format compatibility is "by accident" not guarantee.
Proposal's Phase 2 wording assumed one shared copy; actual scope is
**2× vendor replacement + divergence resolution**. Bake-time total
20,270 LOC (Detour+Recast), runtime 9,383 LOC (Detour only). Vendor
lineage: jackpoz 2014-06-20 TrinityCore fork with 104-line custom diff;
v1.6.0 (May 2024) is ~10 years of upstream fixes ahead. The 104-line
diff's main load-bearing change is `#define DT_POLYREF64 1` — Phase 2
preserves this as a CMake build define.

**Phase exit criteria progress:**
- D2 (baseline reports): in flight, 35.7%, ETA 207 min.
- D4 (go/no-go): not started; Phase 2 prep feeds the Phase-2-readiness
  section of D4.

**Tests:** No bake, no live tests, no code modified — read-only.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE2_PREP.md (new);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 6 entry).

**Next iter:** Iter 7 wakes in ~30 min (sweep should be ~330-360/785,
ETA ~3 hr remaining). Bounded work: write Phase 3 (vmap extractor)
prep — locate the extractor + scope the AzerothCore PR #20822 +
TrinityCore #23972 backport.

**Blockers/risks:**
- The two-copy Detour divergence means Phase 2 will need careful
  consolidation; flagged as medium-likelihood risk in the prep doc.
- Slowing sweep rate (3.5→2.4 tiles/min) means total wall-clock for
  D2 may push to 6-7 hr (vs first estimate 6 hr). Acceptable per
  proposal's "we can take our time".

**Commit:** `b623209a` `phase(0) iter(6): Phase 2 prep — recastnavigation vendor inventory`

---

## Iter 7 — 2026-05-31 — Phase 0

**Did:** Sweep progress-checked at 301/785 (38.3%, ETA 246 min — denser
Mulgore interior tiles dropped per-tile rate to ~0.7 tiles/min). pid 29900
healthy, zero errors. Wrote [`OVERHAUL_PHASE3_PREP.md`](OVERHAUL_PHASE3_PREP.md).

**MAJOR Phase 3 finding:** Repo-wide grep for `MOPY`/`MOBN`/`MOBR`/
`material_id`/`extractWmo`/`vmap_extractor` matched **zero C++ source**.
The repo consumes pre-extracted `.vmtile`/`.vmtree` files from
`D:/MaNGOS/data/vmaps/` (produced by an external tool — likely CMaNGOS/
vmangos `vmap_extractor`). What IS in-tree is `tools/MmapGen/src/game/vmap/`
(5,141 LOC) — the **consumer side**, CMaNGOS-derived vmap library that
reads vmaps for MmapGen's bake rasterization. The proposal §3 Phase 3
step 1 anticipated this: "If we're reusing CMaNGOS's `vmap_extractor`,
fork it into `tools/VmapExtract/` for in-tree control." Phase 3 must
fork the upstream extractor BEFORE the AzerothCore PR #20822 (WMO
material_id=0xFF + MOBN/MOBR BSP) backport can apply.

**Phase exit criteria progress:**
- D2 (baseline reports): in flight, 38.3%, ETA 246 min.
- D4: not started. With all 3 prep docs landed (Phase 1+2+3), D4 only
  needs the global histogram from the sweep aggregation. Expected
  iter 9-10 after sweep done.

**Tests:** No bake, no live tests, no code modified — read-only.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE3_PREP.md (new);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 7 entry).

**Next iter:** Iter 8 wakes in ~30 min for next sweep check
(should be ~340-355/785, ETA ~3.5 hr remaining). With Phase 1+2+3
prep docs all landed, iter 8's bounded work options: (a) Phase 4 prep
— inventory `PhysicsCollideSlide`/`PhysicsGroundSnap`/`PhysicsMovement`
modules to scope the PhysicsEngine→MmapGen link work (proposal's "engineering
crux"); (b) Phase 5 prep — inventory Navigation.cs's 5,600 LOC repair
pipeline deletion scope. Both feed D4. Iter 8 picks Phase 4 prep first
(it's the headline + higher-risk; pre-flighting the link work is high-value).

**Blockers/risks:**
- The vmap extractor source URL isn't identified yet (web fetch
  not available during loop iters). Phase 3 starts with that as the
  first task — TBD identification step.
- Sweep ETA continues drifting upward as denser tiles hit; total
  wall-clock now ~6.7 hr. Still within Phase 0 budget.

**Commit:** `40d6a81a` `phase(0) iter(7): Phase 3 prep — vmap extractor inventory + WMO fix scope`

---

## Iter 8 — 2026-05-31 — Phase 0

**Did:** Sweep progress-checked at 317/785 (40.4%, ETA 289 min, dense
Orgrimmar tiles slowed rate to ~0.3 tiles/min; tile (40,28) — the iter-2
stall tile — took 293s). pid 29900 healthy, zero errors. Wrote
[`OVERHAUL_PHASE4_PREP.md`](OVERHAUL_PHASE4_PREP.md).

**MAJOR Phase 4 findings:** PhysicsEngine module surface is ~10,872 LOC
across `PhysicsEngine.cpp` (6,799), `PhysicsCollideSlide` (411),
`PhysicsGroundSnap` (397), `PhysicsMovement` (308), `SceneQuery` (2,957)
plus helpers. Three risks the proposal underestimated: (1) **THREE
divergent vmap libraries** in the repo — bake-time CMaNGOS
(`tools/MmapGen/src/game/vmap/` 5,141 LOC) + runtime
`Exports/Navigation/` (different `StaticMapTree`/`VMapManager2`/`BIH`
lineage) + external extractor. Direct `add_subdirectory` link is
**impossible** — symbol conflicts (`MMAP::ModelInstance` vs
`VMAP::ModelInstance` etc.). (2) **Detour bit-split divergence**
(MmapGen CMakeLists comment explicitly documents `12/21/31` vs runtime
`16/28/20`); compounds Phase 2's two-Detour finding. (3)
`PhysicsCollideSlide.cpp` includes `PhysicsEngine.h` (6,799 LOC monolith
header) AND `SceneQuery.h` — proposal §4 risk #3's "extract just the
capsule-sweep API into a new PhysicsSweep static lib" is **the design
path, not a fallback**.

**Revised Phase 4 estimate: 22-35 hr wall-clock, 15-20 iters (vs
proposal's "2-3 sessions").** Phase 4 is the budget driver. D4 must
flag this so timeline expectations are honest.

**Phase exit criteria progress:**
- D2 (baseline reports): in flight, 40.4%.
- D4 (go/no-go): not started. With Phase 1+2+3+4 prep all landed, D4
  has the full risk picture; only needs the sweep aggregation. Expected
  iter 11-12.

**Tests:** No bake, no live tests, no code touched.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE4_PREP.md (new);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 8 entry).

**Next iter:** Iter 9 wakes in ~30 min (sweep should be ~340-350/785).
Bounded work: Phase 5 prep — inventory Navigation.cs's 5,600 LOC
repair pipeline deletion scope + the `SnapshotStallGuard` /
`StaticRoutePackCache` / `PathfindingOverlayBuilder` / `NavigationPath.ShouldPreferAlternatePath`
deletion targets. This is the last phase prep doc needed before D4.

**Blockers/risks:**
- Phase 4 scope is materially larger than proposal stated; D4 needs to
  set expectation honestly.
- Sweep ETA continues to drift (now ~289 min remaining + 197 elapsed
  = ~8 hr total). Acceptable but means D4 lands iter 11-12 instead
  of iter 9-10.

**Commit:** `79ef62ee` `phase(0) iter(8): Phase 4 prep — PhysicsEngine link risk inventory`

---

## Iter 9 — 2026-05-31 — Phase 0

**Did:** Sweep progress-checked at 333/785 (42.4%, ETA 319 min). Tile
(36,29) took 299s; pid 29900 healthy zero errors. Wrote
[`OVERHAUL_PHASE5_PREP.md`](OVERHAUL_PHASE5_PREP.md) — Phase 5 deletion
inventory.

**MAJOR Phase 5 findings:** (1) `Services/PathfindingService/Repository/
Navigation.cs` is **7,697 LOC, not 5,600** as the proposal cites — 421
mentions of "Repair" in 7.7K LOC, essentially the entire file IS the
repair pipeline. Net Phase 5 deletion achievable: ≥10,000-12,000 LOC,
not the proposal's ≥5,000 (a positive correction). (2) `NavigationPath.cs`
is 5,647 LOC with 39 mentions of `ShouldPreferAlternatePath`/`IsRouteSupported`/
corridor terms; all uses self-contained within the file. (3) `SnapshotStallGuard`
is **test infrastructure ONLY** (lives in `LongPathingTests.cs`), not a
runtime collision-creep detector — proposal §3 Phase 5 step 4's framing
is wrong but the deletion target is real. (4) `StaticRoutePackCache.cs`
(901 LOC) has 6 cross-layer callers (`PathfindingSocketServer`,
`NavigationPathFactory` ×2, `TravelTask`, 3 test files) — deletion
decision deferred to Phase 5 mid-iter pending p50 latency measurement.
(5) `PathfindingOverlayBuilder.cs` (140 LOC) consumed by `TravelTask` —
keep through Phase 5, defer deletion to Phase 6 unless transport research
clears it.

**Phase 5 revised estimate: 13-21 hr wall-clock, 7-11 iters.** Combined
with Phase 0-4 totals, **the full overhaul timeline is ~58-95 hr /
~45-60 iters AFTER D4 commits.**

**Phase exit criteria progress:**
- D2 (baseline reports): in flight, 42.4%.
- **ALL FIVE phase prep docs landed.** D4 has full risk picture; only
  needs the in-flight sweep aggregation. Expected iter 12-13.

**Tests:** No bake, no live tests, no code touched.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE5_PREP.md (new);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 9 entry).

**Next iter:** Iter 10 wakes in ~30 min. With all phase preps done,
remaining iters until sweep finishes are progress checks + early D4
drafting (synthesizing the 5 prep docs + the D3 manifest + the iter-2
stall-coord findings into a single go/no-go recommendation).

**Blockers/risks:**
- The overhaul's total budget is materially larger than the proposal's
  "8-15 sessions" estimate. D4 must communicate this honestly so the
  user sets expectations correctly before any Phase 1+ code work begins.
- Sweep ETA stable around 319 min remaining (~9.6 hr total wall-clock).

**Commit:** `d3bfb2ae` `phase(0) iter(9): Phase 5 prep — runtime simplification deletion inventory`

---

## Iter 10 — 2026-05-31 — Phase 0

**Did:** Sweep progress-checked at 345/785 (43.9%, ETA 334 min, healthy).
Wrote [`OVERHAUL_PHASE0_D4_FINDINGS.md`](OVERHAUL_PHASE0_D4_FINDINGS.md)
DRAFT — synthesizes all 5 phase preps + D3 manifest + iter-2 stall-coord
findings into a single go/no-go recommendation. Placeholders for global
histogram + top-20-worst-tile data fill in iter 13-14.

**D4 bottom line: 🟢 GO at materially larger budget than the proposal
stated.** Revised overall budget ~58-95 hr / ~45-60 iters (vs proposal's
8-15 sessions). Three discoveries drive the increase: (1) two-Detour
copies divergence (Phase 2 scope 2× actual), (2) no in-tree vmap
extractor (Phase 3 requires fork-first), (3) three vmap libraries
(Phase 4 requires PhysicsSweep static lib + IGeometrySource abstraction
as design path not fallback). None are blockers; Phase 4 has the highest
risk and is the budget driver. **D4 recommends Phase ordering: 1 → 2 →
3 → 4 → 5** (Phase 1 first for easy Mononen wins; Phase 2 sequencing
prereq for Phase 4; Phase 3 can interleave with 2). **User decisions
called out:** budget acceptance, Phase 4 risk tolerance, map 0 sweep
deferred.

**Phase exit criteria progress:**
- D1, D3: ✅ done in earlier iters.
- D2: in flight, 43.9% — global histogram + top-20-worst-tile aggregation
  iter 13-14 fills D4's §3-4-5 placeholders.
- D4: skeleton complete, awaiting sweep aggregation.

**Tests:** No bake, no live tests, no code touched.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE0_D4_FINDINGS.md
(new); docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 10 entry).

**Next iter:** Iter 11 wakes in ~30 min. Sweep should be ~370-385/785,
ETA ~5 hr remaining. Bounded work options: (a) start drafting the
**sweep-aggregator** script that converts the per-tile JSON to global
histogram + top-20-worst-tile table — runs against partial results, will
re-run at completion; (b) write the **OG zep deck-edge stall coord**
targeted probe (proposal §6 Phase 6 lists this as a stall to add to
regression tests; cheap +1 stall-coord data point for D4). Iter 11 picks
(a) first since it has compound value (run-once now + run-final iter 14).

**Blockers/risks:**
- D4 has placeholders only; the FINAL D4 commit lands at iter 13-14
  when sweep finishes.
- Sweep continues to drift on dense tiles. Conservative estimate is
  iter 13-14 for completion.

**Commit:** `996257ac` `phase(0) iter(10): D4 go/no-go findings DRAFT — synthesis of 5 phase preps`

---

## Iter 11 — 2026-05-31 — Phase 0

**Did:** Investigated sweep hang on tile (30,30) — old parent pid 29900
was actually progressing (it completed tile (31,30) right before I
investigated) but flushed log lazily. Killed parent + relaunched (pid
41492) with a sentinel JSON for tile (30,30) so it's permanently skipped
(likely native AccessViolation per the iter-1 audit memo's
SafeGetPolyAtCoord notes). New sweep resumed at 350 skipped.

Wrote [`tools/scripts/phase0-aggregate-sweep.ps1`](../../../tools/scripts/phase0-aggregate-sweep.ps1)
— aggregates per-tile JSON into global affordance histogram + top-N worst
tiles by Unrecoverable %. Ran on partial sweep (349 tiles aggregated).

**REAL D4 NUMBERS (partial, 44% sweep coverage):**
- Global Unrecoverable: **13.28%** (Blocked + UnsafeDrop + Cliff) —
  **BELOW the proposal's expected 20-30% baseline** (positive correction
  for D4).
- Global affordance: Walk 17.48%, SafeDrop 22.38%, Vertical 20.10%,
  Blocked 11.40%, SteepClimb 10.32%, JumpGap 8.31%, StepUp 8.13%,
  UnsafeDrop 1.87%.
- Top tile by Unrecoverable %: (27, 28) at 31.16% — Mulgore region.
- Stall tiles NOT in top-20 yet: (39,28) iter-1 + loop-25, (40,28) iter-2 —
  confirms iter-1 audit's finding that path-sampling doesn't reliably
  hit specific stall coords (the localized 7-poly stall is too small to
  push tile-wide ratio over ~17%).

**Phase exit criteria progress:**
- D2 sweep: ~44% (350/785 in this aggregation; new sweep resumes from
  there).
- D4 partial aggregation done — final numbers iter 13-14.

**Tests:** No bake, no production code touched.

**Files changed:** tools/scripts/phase0-aggregate-sweep.ps1 (new, 95 LOC);
docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md (iter 11 entry).
Sentinel: tmp/iter-overhaul-phase0/sweep-map1/tile-30-30.json (gitignored).

**Next iter:** Iter 12 wakes in ~30 min. Sweep should be 380-400/785.
Bounded work: **update D4 with the partial aggregation numbers** —
replace §3 (global histogram) and §4 (>50% tiles) placeholders, draft
the Phase 1 starting tile recommendation in §8. Also pick a candidate
Phase 1 starting tile from the top-20 worst list.

**Blockers/risks:**
- Tile (30,30) may indicate other hang-prone tiles ahead. Watch for
  similar long-running tiles (>10 min); manual sentinel-skip if needed.
- The 13.28% global unrecoverable rate is lower than expected — Phase 1
  Mononen-rule retightening expected ≥30% drop in Blocked count may be
  harder to achieve as a relative percentage (less to drop from). The
  absolute reduction matters more than the relative.

**Commit:** `38af42e5` `phase(0) iter(11): sweep-aggregator script + partial D2 numbers`

---

## Iter 12 — 2026-05-31 — Phase 0

**Did:** Re-ran aggregator after sweep resumed (367 tiles aggregated;
sweep at 367/785 = 46.8% post-recovery). Updated D4 §3 (global histogram)
and §4 (top-20-worst-tiles) with partial sweep aggregation. Locked **Phase
1 starting tile recommendation: tile (32, 28)** — 26.21% Unrecoverable
(rank 2), real bot-traffic terrain in N Durotar/S Barrens, well-sampled
(412 segs / 5 paths), distance ≥5 tiles from T3 fixture (40,29) so no
cull-blast risk. Alternate: (39, 28) as second target (iter-1+loop-25
stall tile, DIAGONAL to T3, less Unrecoverable signal but direct T1
test relevance). Added §8b iter-by-iter Phase 1 starting plan.

**Important D4 update:** Unrecoverable rate 13.47% globally is BELOW the
proposal's expected 20-30% baseline. Walk is only 17.22% — proposal's
"Walk ≥60% post-Phase-4" target is dramatic (would require reclassifying
~42% of segments). D4 §3 recommends either revising target downward OR
keeping as stretch goal with "Walk + recoverable ≥80%" as practical
benchmark. No tile breaches 50% Unrecoverable; **Phase 1 (Recast params)
is the dominant lever, NOT Phase 3 (vmap extraction).**

**Phase exit criteria progress:**
- D2 sweep: 46.8% coverage. New sweep instance (started iter 11
  06:30:52 after hang recovery) processing 17 new tiles in 43 min.
  ETA wildly inflated (1054 min) but reality is the dense
  Mulgore/Thunder Bluff/Thousand Needles region; eastern Barrens and
  later tiles will be faster.
- D4: §3, §4, §8 populated from partial data. §3+§4 re-run at sweep
  finish; §8 locked.

**Tests:** No bake, no live tests.

**Files changed:** docs/Plan/Pathfinding/OVERHAUL_PHASE0_D4_FINDINGS.md
(updated §3/§4/§8 + header); docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md
(iter 12 entry).

**Next iter:** Iter 13 wakes in ~30 min. With D4 substantially complete,
remaining sweep iters are progress monitoring + final aggregation.
Iter 13 bounded work: **probe candidate Phase 1 starting tile (32, 28)
with `--samples 20`** to get a high-precision before-snapshot. Phase 1
will compare against this snapshot post-tighten.

**Blockers/risks:**
- The proposal's "Walk ≥60%" Phase 4 target is dramatic given 17.22%
  baseline — may need acceptance criteria revision. D4 flags this.
- New sweep instance's ETA calculation is misleading; true ETA depends
  on whether remaining tiles are dense interior or sparse edge.

**Commit:** _filled by commit step below_
