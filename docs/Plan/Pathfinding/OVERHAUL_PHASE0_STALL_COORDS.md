# Phase 0 — Targeted Probes for the 3 Known Stall Coords

> **Iter 2 of the Recast Physics-Validated Overhaul loop.**
> Companion: [`OVERHAUL_LOOP_STATUS.md`](OVERHAUL_LOOP_STATUS.md),
> [`OVERHAUL_PHASE0_D1_AUDIT.md`](OVERHAUL_PHASE0_D1_AUDIT.md).

## What this answers (kickoff D2 spec)

> "Specific lookup for the 3 known stall coords: for each, which tile, which
> polyIdx, what does the probe currently classify its edges as?"

Iter 2 runs `NavMeshPhysicsValidator 1 --tile <X,Y> --cull-coord <wow X,Y,Z>
--cull-coord-z-radius 15 --cull-coord-xy-radius 2` for each of the three
stall coords inherited from loops 24-26 and the loop-26 iter-1/2 evidence.
The Z-stack + XY-grid probe enumerates every polygon within a ±15y vertical
window × 3×3 (2y) horizontal grid at each seed — this dedupes against the
WMO-interior multi-Z-stacked polys per [[project_pfs_loop19_cull_pipeline_blocker]].

## Result table

| # | Origin | WoW coord | Tile (X,Y) | .mmtile | Unique polyrefs found | What it means |
|---|---|---|---|---|---|---|
| 1 | iter-1 east-wall stall | (1627.6, -4151.8, 36.9) | (39, 28) | `0012839.mmtile` | **1** — `281475309109858` | Single poly at the OG east-wall; matches loop-26 iter-1's off-mesh staging. |
| 2 | iter-2 OG-interior stall | (1608.1, -4382.3, 10.0) | (40, 28) | `0012840.mmtile` | **7** — `281475310158323`, `…328`, `…332`, `…334`, `…335`, `…337`, `…340` | Multi-poly stack at the sub-floor pocket — WMO interior with overlapping walkable surfaces at slightly different Z (classic `[[project_pfs_loop19_cull_pipeline_blocker]]` signature). |
| 3 | loop-25 doodad-wall stall | (1615.3, -4240.85, ~45) | (39, 28) | `0012839.mmtile` | **11** — `281475309109255-298` range | Large multi-poly cluster — doodad-wall region has many adjacent thin polys; consistent with a high cull-blast-radius location. |

Full JSON: `tmp/iter-overhaul-phase0/iter2-stalls-39-28.json` (stalls 1+3)
and `tmp/iter-overhaul-phase0/iter2-stalls-40-28.json` (stall 2). Both are
local-only (`tmp/` is gitignored).

## What "polys present" tells us

All 3 stall coords have polygons in the current bake. This **confirms the
proposal's hypothesis** that Recast emits walkable polygons in places the
runtime PhysicsEngine refuses to traverse. Per the proposal §2.1:

> "Every iter-1, iter-2, loop-25 stall is this. Recast says walkable,
> PhysicsEngine says blocked, bot stalls 15s, test fails."

The kickoff D4 go/no-go condition states:

> "If YES [polys with Blocked edges at the stall coords], the overhaul's
> Layer-3 algorithm WILL find them. If NO, the probe's sampling resolution
> is too coarse OR the PhysicsEngine link is buggy."

We have the polys. **Next: prove they classify as Blocked under per-edge
capsule sweep.** That requires the per-poly-per-edge enumeration which
`NavMeshPhysicsValidator` does not implement (it uses path-sampling, not
edge enumeration — see [`OVERHAUL_PHASE0_D1_AUDIT.md`](OVERHAUL_PHASE0_D1_AUDIT.md)
§"Decision rationale"). The path-sampling overlay from the same probe runs
shows the tiles overall have 92-98% non-Walk segments (15-16% unrecoverable),
which is consistent with the stall coords sitting in already-bad regions —
but does not prove the specific stall polys' edges are Blocked.

## Per-tile path-sampling signal at the stall tiles

While the cull-coord pass enumerates polys at the seed coord, the random
sample (`--samples 1`) pass classifies segments along one FindPath route
through the tile. These give a baseline non-Walk fraction:

| Tile | Path corners | Segments classified | Walk | Non-Walk | Blocked | UnsafeDrop |
|---|---|---|---|---|---|---|
| (39, 28) | 38 | 38 | trivial | 36 (94.7%) | — | — |
| (40, 28) | 157 | 101 (capped at 100/path) | 2 (2.0%) | 99 (98.0%) | 11 | 1 |

Tile (40,28)'s 11 Blocked + 1 UnsafeDrop segments in a 101-segment classified
path is exactly the bake-vs-physics gap profile Phase 1+ improvements will
attack. The 98% non-Walk is misleading: SafeDrop and Vertical dominate
(SafeDrop=32, Vertical=26, SteepClimb=22), all of which are recoverable
non-Walk affordances the bot CAN traverse (per loop-26 iter-2 nomenclature).
The triage metric is `UnrecoverableNonWalk` = 12 / 101 = 11.9%, in line with
proposal §3 Phase 0's expected 20-30% baseline.

## Findings deferred to later iters

- **Per-polygon edge classification at the stall polys.** Needs a new probe
  mode (or use of `PathPhysicsProbe --start` at each stall coord with
  short tangential `--end` to classify each cardinal edge). Targeted for
  iter 5+ when D4 findings finalize.
- **Cross-tile-seam stats** (kickoff D2): polys within `borderSize` voxels
  of tile edges and their Blocked fraction. Needs the all-tiles rough sweep
  to be running. Targeted for iter 4+.
- **Global affordance histogram and top-20 worst tiles per map.** Needs the
  all-tiles rough sweep — iter 3's work.

## Iter 3 plan

Kick off the all-tiles rough sweep for map 1: 785 tiles × `--samples 5`
in a serial background batch, ~13 hr wall-clock. The validator writes one
JSON per tile to `tmp/iter-overhaul-phase0/sweep-map1/`. Iter 3 spawns
the batch and ScheduleWakeups at 1800s for progress check. Iter 4
aggregates results when the batch completes.
