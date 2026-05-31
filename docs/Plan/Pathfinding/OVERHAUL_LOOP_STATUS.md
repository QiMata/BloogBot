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

**Commit:** _filled by commit step below_
