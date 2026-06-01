# Phase 4 Prep — PhysicsEngine→MmapGen Link Risk Inventory

> **Iter 8 of the Recast Physics-Validated Overhaul loop.**
> Read-only inventory of the `Exports/Navigation` PhysicsEngine module
> surface that Phase 4 must link into `tools/MmapGen` to run capsule sweeps
> at bake time. Phase 4 is the proposal's **HEADLINE** + carries
> guardrail-8 risk ("If the PhysicsEngine link into MmapGen surfaces
> include cycles or thread-unsafety that you cannot resolve in 2 iters
> of work, STOP and surface"). This doc surfaces the risk **before**
> any code change so Phase 4 doesn't blunder into it.

## PhysicsEngine module surface area

| File | LOC | Phase 4 purpose |
|---|---|---|
| [`PhysicsEngine.cpp`](../../../Exports/Navigation/PhysicsEngine.cpp) | 6,799 | Monolith: capsule sweep, ground snap, slope/step-up classification |
| [`PhysicsCollideSlide.cpp`](../../../Exports/Navigation/PhysicsCollideSlide.cpp) | 411 | Capsule slide-along-wall (extracted module) |
| [`PhysicsGroundSnap.cpp`](../../../Exports/Navigation/PhysicsGroundSnap.cpp) | 397 | Ground-Z resolution / snap |
| [`PhysicsMovement.cpp`](../../../Exports/Navigation/PhysicsMovement.cpp) | 308 | Movement integration, projection |
| [`SceneQuery.cpp`](../../../Exports/Navigation/SceneQuery.cpp) | 2,957 | VMap scene-cache extraction + collision queries |
| Helpers (Diagnostics, Liquid, Math, Select, Shape) | ~1,200 | Auxiliary headers + sources |
| **TOTAL** | **~10,872 LOC** | The core "physics engine" surface |

Per the proposal §3 Phase 4 step 1: "tools/MmapGen/CMakeLists.txt adds
dependency on Exports/Navigation's physics modules (PhysicsCollideSlide,
PhysicsGroundSnap, PhysicsMovement, SceneQuery)." That's the design intent.

## Risk #1 — THREE divergent VMap libraries

Iter 7 found that the vmap **extractor** isn't in-tree. Iter 8 found
that the vmap **consumer library** exists in TWO places:

| Path | Lineage | Used by |
|---|---|---|
| [`tools/MmapGen/src/game/vmap/`](../../../tools/MmapGen/src/game/vmap/) | CMaNGOS, 5,141 LOC. `MapTree` / `ModelInstance` / `WorldModel` / `BIH` | MmapGen bake-time rasterization |
| [`Exports/Navigation/`](../../../Exports/Navigation/) (mixed with PhysicsEngine) | Different lineage, 910+ LOC across `StaticMapTree.cpp` + `VMapManager2.cpp` + `ModelInstance.cpp` + `BIH.cpp` | Runtime PathFinder + PhysicsEngine + SceneQuery |

`SceneQuery.cpp` directly `#include`s the Exports/Navigation copy:
`StaticMapTree.h`, `ModelInstance.h`, `WorldModel.h`, `BIH.h`,
`VMapManager2.h`. These class names COLLIDE with MmapGen's CMaNGOS
copy.

**Phase 4 cannot just `add_subdirectory(Exports/Navigation)` into
`tools/MmapGen`.** Symbol conflicts will erupt at link time
(`MMAP::ModelInstance` vs `VMAP::ModelInstance`, `MMAP::BIH` vs
`VMAP::BIH`, etc.).

The proposal §4 risk row #3 anticipates this:

> "Worst case: extract just the capsule-sweep API into a new
> `PhysicsSweep` static lib that both `Exports/Navigation` and
> `tools/MmapGen` depend on."

**That worst case is the design path.** PhysicsSweep must be:
- Capsule sweep + ground snap + slope classification API.
- Parameterized over an abstract **geometry source** (interface).
- Two concrete adapters: `MmapGenGeometrySource` (uses CMaNGOS vmap)
  and `NavigationGeometrySource` (uses Exports/Navigation vmap).
- Zero `#include` of either vmap library from PhysicsSweep itself.

This is significantly more work than the proposal's "1-2 sessions"
Phase 4 budget. Realistic Phase 4 estimate revises upward.

## Risk #2 — Detour bit-split divergence (compounding)

Iter 6 found that the bake-time and runtime Detour copies differ. The
MmapGen CMakeLists comment (lines 70-77) explicitly documents:

> "vmangos's vendored `dep/recastnavigation/Detour/Include/` declares
> `DT_SALT_BITS=12`, `DT_TILE_BITS=21`, `DT_POLY_BITS=31`. The runtime
> `Exports/Navigation/Detour/Include/` uses `16/28/20`."

Three Detour configurations in the repo (MmapGen bake, Exports/Navigation
runtime, dep/recastnavigation generic copy). They all happen to agree on
the on-disk tile format because polyrefs are computed at load time, but
each thinks "the other consumer's view of dtPolyRef is wrong." Phase 4
linking PhysicsEngine into MmapGen brings this divergence into a single
process — if PhysicsEngine code touches `dtPolyRef` directly (it does in
PhysicsTestExports + SceneQuery), the bit-split clash is a live wire.

## Risk #3 — PhysicsEngine.cpp file-local globals

Quick grep for `static` keywords in PhysicsEngine.cpp: 32 hits, mostly
`static_cast`. Actual file-local state is minimal — but the engine is
historically single-threaded. The proposal §4 risk #1 says:

> "If a tile takes >10 min, sample fewer edge points or parallelize the
> validation pass within a tile."

Within-tile parallelism (PhysicsEngine called from multiple threads on
the same tile's polys) requires verifying no shared mutable state.
Phase 4 first-pass uses **single-threaded validation pass**; parallelism
is an optional speed optimization.

## Risk #4 — `PhysicsCollideSlide.cpp` include depth

```
PhysicsCollideSlide.cpp
  → PhysicsCollideSlide.h
  → PhysicsShapeHelpers.h
  → PhysicsEngine.h       (6,799 LOC monolith header)
  → PhysicsTolerances.h
  → SceneQuery.h          (pulls in StaticMapTree.h, VMapManager2.h indirectly)
  → VMapDefinitions.h
  → VMapLog.h
```

`PhysicsCollideSlide.cpp` already pulls in the full PhysicsEngine.h
header. To isolate the capsule-sweep public API for the proposed
PhysicsSweep static lib, **PhysicsEngine.h must be split** into:

- `PhysicsSweepApi.h` — capsule sweep + ground snap public API only
  (the methods `PhysicsValidationPass::Run` will call).
- `PhysicsEngineInternal.h` — everything else, runtime-only.

This is a header refactor inside `Exports/Navigation`, NOT a Phase 4
prerequisite — but Phase 4 must do it before the link is clean.

## Revised Phase 4 scope estimate

The proposal §3 Phase 4 says "2-3 sessions, the headline work". The
risk inventory pushes this up:

| Work unit | Wall-clock | Iters |
|---|---|---|
| Refactor `PhysicsEngine.h` → split `PhysicsSweepApi.h` + `PhysicsEngineInternal.h` | 2-4 hr | 1-2 |
| Create `Exports/PhysicsSweep/` static lib (geometry-source abstraction + 4 capsule-sweep methods + adapters) | 3-5 hr | 2 |
| Link PhysicsSweep into `tools/MmapGen/CMakeLists.txt` + verify build | 1-2 hr | 1 |
| Implement `PhysicsValidationPass::Run` algorithm (proposal §2 Layer 3) | 4-6 hr | 2 |
| Implement connectivity-prune flood-fill | 1-2 hr | 1 |
| Implement repair pass (inset poly + re-validate) | 2-3 hr | 1 |
| Wire into `TileWorker.cpp` after Detour build / before `dtCreateNavMeshData` | 2-3 hr | 1 |
| Add new `NAV_AREA_*` constants in `NavAreaTypes.h` | 0.5 hr | 1 |
| Per-tile validation report writing | 1 hr | 1 |
| Re-bake all 41 maps; validate ≤4 hr single-threaded budget | 3-6 hr (background) | 1 |
| Phase 0 probe re-run; verify Blocked=0, Walk≥60% | 2-3 hr | 1 |
| **Phase 4 total** | **22-35 hr wall-clock** | **15-20 iters** |

This is 2-3× the proposal's "2-3 sessions" estimate. **Phase 4 is the
budget driver.** D4 must flag this so the overall overhaul timeline
sets expectations correctly.

## Phase 4 acceptance criteria (per proposal §3)

- PhysicsValidationPass linked into MmapGen.
- Full re-bake of all 41 maps ≤4 hr single-threaded.
- Output Blocked-poly count = 0.
- Walk ≥60%, StepUp 20-30%, Repaired ≤5%, Unreachable ≤5%.
- Bake-fixture pair passes (T3 + T4 from D3 manifest).
- 4 long-pathing tests show no Blocked polys at stall coords.

## Guardrail-8 STOP condition

> "If the PhysicsEngine link into MmapGen surfaces include cycles or
> thread-unsafety that you cannot resolve in 2 iters of work, STOP
> and surface."

This prep doc forecasts where guardrail 8 fires:

- **Iter N+0 (Phase 4 start):** Attempt direct `add_subdirectory` link.
  Predict: symbol collisions between MmapGen's vmap lib and
  Exports/Navigation's vmap lib (`MMAP::*` vs `VMAP::*` classes).
- **Iter N+1:** Fall back to PhysicsSweep static lib design + header refactor.
- **Iter N+2:** Implement geometry-source abstraction.
- **Iter N+3:** If by this iter the link still fails, guardrail 8 fires
  → STOP, write findings, escalate.

## What this iter does NOT do

- No code modification.
- No CMakeLists edits.
- No build attempt.
- Phase 0 D2 sweep continues in background.

## Sequencing

Phase 4 depends on **all** of Phase 1, 2, 3 per proposal §7. Cannot start
until all three close. Conservative D4 estimate: with 15-20 iters for
Phase 4 alone + 5-8 for Phase 1 + 4-6 for Phase 2 + 5-8 for Phase 3, the
total Phase 1-4 budget is 29-42 iters AFTER D4 commits.

## Phase 4 pre-conditions

- Phase 1 done (AgentProfile + Mononen-tuned bake params).
- Phase 2 done (recast v1.6.0 vendor in both copies).
- Phase 3 done (vmap extractor forked + patched + re-extracted).
- D4 with `go` recommendation.

## Pointers for Phase 4 start

- `Exports/Navigation/PhysicsEngine.h` — needs header split.
- `Exports/Navigation/PhysicsTolerances.h` — capsule physics constants (input).
- `Exports/Navigation/CapsuleCollision.h` + `PhysicsShapeHelpers.h` — sweep primitives.
- `tools/MmapGen/CMakeLists.txt` — extend `target_link_libraries` for MmapGen.exe.
- `tools/MmapGen/contrib/mmap/src/TileWorker.cpp` — call site for PhysicsValidationPass.
- `Exports/Navigation/NavAreaTypes.h` — extend with `NAV_AREA_GROUND_CONFIRMED`, etc.
