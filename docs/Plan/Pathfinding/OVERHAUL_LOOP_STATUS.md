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

**Commit:** _filled by commit step below_
