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

**Commit:** _filled by commit step below_
