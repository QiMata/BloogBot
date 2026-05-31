# Phase 0 Deliverable 1 — Audit + Decision

> **Iter 1 of the Recast Physics-Validated Overhaul loop.**
> Companion: [`OVERHAUL_LOOP_STATUS.md`](OVERHAUL_LOOP_STATUS.md).

## Summary

The Phase 0 kickoff doc tells us to "build a new C++ tool `tools/PhysicsValidationProbe/`" that links the PhysicsEngine into a per-tile measurement probe. **A tool that satisfies this contract already exists** as [`tools/NavMeshPhysicsValidator/`](../../../tools/NavMeshPhysicsValidator/) — written in C# during the loop-19→25 cull pipeline work. Kickoff guardrail 9 explicitly says do not reimplement when the physics-engine link already exists.

**Decision:** Phase 0 Deliverable 1 is **satisfied by the existing NavMeshPhysicsValidator**. No new C++ probe is built. Phase 0 D2 (baseline reports) runs the existing tool. The per-polygon-per-edge exhaustive sweep envisioned in the proposal is deferred to Phase 4 — that's the bake-time pass where it actually matters and there it builds against a different surface (`dtNavMesh*` polygons in MmapGen, not runtime `FindPath` queries).

## Evidence — the tool runs and detects the right kind of thing

Iter 1 ran `NavMeshPhysicsValidator 1 --tile 40,28 --samples 20` against the prod-data mmap for the loop-26 iter-2 stall tile. Full output: [`tmp/iter-overhaul-phase0/iter1-probe-1-40-28.json`](../../../tmp/iter-overhaul-phase0/iter1-probe-1-40-28.json).

```
samples=20 pathsFound=19 segments=1480 nonWalk=1188 (80.3%) unrecoverable=218 (14.7%)
Blocked    196   SafeDrop  467   SteepClimb 144   StepUp    91
Vertical   213   Walk      292   JumpGap     55   UnsafeDrop 22
```

Exit code 2 is **expected** — it's the tool saying "found critical non-walk segments". 196 Blocked + 22 UnsafeDrop is exactly the bake-vs-physics gap Phase 4 will close.

Wall-clock time per tile at 20 samples: **~3 min**. Implication for Phase 0 D2 in §"D2 strategy" below.

## What the existing tool satisfies vs the kickoff doc's spec

| Kickoff D1 requirement | NavMeshPhysicsValidator | Gap? |
|---|---|---|
| Physics-engine link callable from probe | Yes — P/Invokes `ClassifyPathSegmentAffordance` via [`NavigationInterop.cs`](../../../Tests/Navigation.Physics.Tests/NavigationInterop.cs). Same C export the runtime BotRunner uses. | None for measurement |
| Per-tile JSON report with affordance histogram + worst-N segments | Yes | None |
| 5y XY heat-map of non-Walk segment clusters | Yes (`HotspotCells`) | None |
| Targeted lookup of specific WoW coord → polyRef + classification | Yes (`--cull-coord X,Y,Z` + Z-stack enumeration) | None |
| Per-polygon per-edge **exhaustive** capsule sweep | **No** — path-sampling, not polygon-edge enumeration | Deliberately not added; see §Decision rationale |
| All-tiles batch mode (one JSON per tile) | No — single-tile-per-invocation | **GAP — close in D2 wrapper** |
| Configurable agent profile (race) | Partial — hardcodes Tauren M (`radius=1.0247`, `height=2.625`) | Acceptable for D2 baseline |

The C++-vs-C# point in the kickoff doc is moot. The proposal envisioned C++ because PhysicsEngine is C++. In practice the runtime physics is exposed as a C export, and a C# P/Invoke wrapper drives it for measurement just as well as a C++ direct call would. The C++ link only becomes load-bearing in Phase 4 (bake-time pass inside `tools/MmapGen`), and that's a separate engineering exercise from D1's "measurement probe."

## Decision rationale — why path-sampling is **better** than per-edge enumeration for Phase 0

The proposal's per-polygon-per-edge algorithm (proposal §2 Layer 3) is the right design for **Phase 4** — the bake-time validation pass that classifies every poly's areaType before writing the `.mmtile`. It's an exhaustive scan over all `dtNavMesh*` polygons.

For **Phase 0** the goal is different: establish where the bake-vs-physics gap surfaces under realistic usage so later phases can measure improvement. Path-sampling has two advantages over edge enumeration here:

1. **It mirrors runtime usage.** The bot navigates paths (`FindPath` → segment chain), not polygon-edge boundaries. A segment that classifies as Blocked under `ClassifyPathSegmentAffordance` is exactly the kind of failure that stalls the bot in production. Per-edge enumeration would report many edges that are technically "Blocked under capsule sweep" but never appear on a route the bot would actually take.

2. **It produces directly-actionable failure data.** Each non-Walk segment carries (start, end, polyRefA, polyRefB, climb, drop, slope, validation reason). Phase 1+ improvements can be evaluated by re-running the same seed and comparing affordance distributions on identical sample sets.

Per-edge enumeration is *more thorough* but slower (every poly × every edge × N samples) and lower-signal (most edges never carry a path).

## D2 strategy — using the existing tool for baseline reports

The kickoff D2 spec wants per-map baseline JSON + a human-readable summary covering:
- global affordance histogram
- top 20 worst tiles per map (by Blocked-edge ratio)
- specific lookup for 3 known stall coords
- cross-tile-seam stats (polys within `borderSize` of tile edge)

At ~3 min per tile with `--samples 20`, all 785 tiles of map 1 would be ~40 hours; map 0 similarly. Two adjustments make this tractable:

1. **Sample-count rampdown for the all-tiles sweep.** `--samples 5` should be enough to surface "is this tile mostly clean / mostly broken" at ~1 min/tile (785 tiles × 1 min ≈ 13 hr per map). Tiles flagged as bad in the rough sweep get re-probed with `--samples 50` for detail.
2. **Targeted-coord probing for the 3 known stalls** runs in seconds via `--cull-coord X,Y,Z --cull-coord-z-radius 15` (single invocation per stall coord, no random-sample needed).

Iter 2's work is exactly this — start the all-tiles rough sweep on map 1 as a background batch + run the 3 stall-coord targeted probes for instant signal.

## Axis-convention note for the JSON output

`NavMeshPhysicsValidator` stores WoW coords flipped in its internal `Vector3`: the JSON's `Start.X` field actually holds WoW.Y, and `Start.Y` holds WoW.X. This matches the swap used by the runtime `FindPath` wrapper and Detour's tile bounds (per [`project_pathfinding_tile_coords`](../../../../../C:/Users/lrhod/.claude/projects/e--repos/memory/project_pathfinding_tile_coords.md)'s correction).

Example from iter 1: the worst segment is at JSON `(X=-4278.64, Y=1745.98, Z=232.35)`. In WoW coords this is `(X=1745.98, Y=-4278.64, Z=232.35)` — the OG east-cliff drop region, consistent with where loop-26's iter-1 east-wall off-mesh lived. When the D2 summary cross-references stall coords, the swap must be applied.

## Phase 4 implication — what this audit does NOT solve

The proposal's "engineering crux" for Phase 4 is linking `Exports/Navigation/PhysicsEngine.cpp` (and its `PhysicsCollideSlide`/`PhysicsGroundSnap`/`PhysicsMovement` modules) **directly into the C++ MmapGen bake pipeline** to run `SweepCapsule` against source geometry per polygon edge before `dtCreateNavMeshData` writes the tile. That's a different link than measurement-mode P/Invoke from a probe tool.

This audit confirms the **measurement-mode** link works (C export → managed → JSON report). The **bake-mode** link is still Phase 4's responsibility. The risk surfaced by guardrail 8 ("if PhysicsEngine link surfaces include cycles or thread-unsafety, STOP") only fires in Phase 4, not here.

## Action items spawned

- [ ] Iter 2: kick off all-tiles rough sweep for map 1 (785 tiles × `--samples 5`) as background batch; targeted-probe the 3 known stall coords for instant signal.
- [ ] Iter 3+: build the per-map aggregator (`baseline-map1.json`, `baseline-summary.md`).
- [ ] Iter 4+: Phase 0 D3 (test-failure baseline manifest) + D4 (findings/go-no-go).
