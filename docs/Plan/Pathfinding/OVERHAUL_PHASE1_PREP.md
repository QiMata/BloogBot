# Phase 1 Prep — Inventory of Current Bake Parameters

> **Iter 5 of the Recast Physics-Validated Overhaul loop.**
> Read-only code-inventory of `tools/MmapGen` to map the surface area
> Phase 1 will modify. Per kickoff guardrail 1, **no production code
> is touched in Phase 0**; this doc is a Phase 1 work-breakdown that
> finalizes once Phase 0 D4 (go/no-go findings) commits.

## Scope summary

Phase 1's job per proposal §3:

1. Create `tools/MmapGen/include/BakeProfile.h` — single struct holding
   all bake params, derived from a single `AgentProfile` (race-keyed
   PhysicsEngine constants).
2. Replace `JsonFloatOrDefault`/`from_json(rcConfig)` calls in `TileWorker.cpp`
   with `bakeProfile.cs`, `.ch`, etc.
3. Set defaults per Mononen rules: `cs=r/2`, `ch=cs/2`, etc.
4. Delete per-tile `cs/ch/tileSize/maxSimplificationError` overrides from
   `config.json`; keep `_NEGATIVE_RESULT_*` comments as institutional memory.
5. Rebake all tiles. Run Phase 0 probe; expect Blocked-poly count drop ~30-50%.
6. Run bake-fixture pair (likely fail; gate on Phase 0 probe improvement).

## Surface area

| File | LOC | Phase 1 action |
|---|---|---|
| [`tools/MmapGen/contrib/mmap/src/TileWorker.cpp`](../../../tools/MmapGen/contrib/mmap/src/TileWorker.cpp) | 12,197 | replace `from_json(rcConfig)` + `getDefaultConfig()` with AgentProfile-driven values |
| [`tools/MmapGen/contrib/mmap/src/MapBuilder.cpp`](../../../tools/MmapGen/contrib/mmap/src/MapBuilder.cpp) | (line 547-550) | consolidate `BASE_UNIT_DIM_MAP_BUILDER = 0.13f` vs `BASE_UNIT_DIM = 0.2666f` mismatch |
| [`tools/MmapGen/contrib/mmap/src/MapBuilder.h`](../../../tools/MmapGen/contrib/mmap/src/MapBuilder.h) | 41-46 | replace `BASE_UNIT_DIM` constant with `AgentProfile::Tauren.cs()` |
| [`tools/MmapGen/config.json`](../../../tools/MmapGen/config.json) | 277 | delete 5 per-tile override blocks, keep 8 `_NEGATIVE_RESULT_*` + 34 `_README` |
| **new** `tools/MmapGen/include/BakeProfile.h` | ~80 (sketch below) | declare `AgentProfile` + `BakeProfile` + `MakeBakeProfile(agent)` |

## Current bake defaults — proposal target deltas

From [`TileWorker.cpp:12042-12106`](../../../tools/MmapGen/contrib/mmap/src/TileWorker.cpp#L12042) (`getDefaultConfig()`) and `MapBuilder.h:41`:

| Param | Current default | Proposal target | Delta |
|---|---|---|---|
| `cs` | **0.2666** (`BASE_UNIT_DIM`) | `r/2 = 0.5124` outdoor, `r/3 = 0.3416` indoor | **too fine globally** — proposal §2.1 |
| `ch` | **0.2666** (=`cs`) | `cs/2 ≈ 0.13` | **violates Mononen `ch=cs/2`** — biggest non-compliance |
| `detailSampleDist` | 2.0 | `cs * 6 ≈ 1.6` | minor; currently ≈ `cs*7.5` |
| `detailSampleMaxError` | 0.5 | 0.5 | ✅ matches |
| `maxSimplificationError` | 1.8 | 1.3 | too coarse — reject above 1.5 per proposal |
| `maxVertsPerPoly` | `DT_VERTS_PER_POLYGON` (6) | 6 | ✅ matches at default; tile 4029 overrides to 3 (proposal: probe value, raise to 6) |
| `partitionType` | "watershed" | watershed | ✅ matches |
| `mergeRegionArea` | 10 | 40 | too small (TrinityCore default 40) |
| `minRegionArea` | 30 | 20 | mildly too large |
| `walkableSlopeAngle` (terrain) | **75.0** | `physics_max_slope = 60.0` | **bake accepts steeper than runtime walks** |
| `walkableSlopeAngleVMaps` (model) | 61.0 | ~60.0 | ✅ ~matches |
| `borderSize` | placeholder 0 (computed from json) | `walkableRadius_voxels + 3` | needs derivation in BakeProfile |
| `walkableRadius/Height/Climb` | 0 (auto-derived when 0 per PFS-OVERHAUL-006 from `agentRadius / cs`, etc.) | computed by AgentProfile | already correct pattern; just hoisting |

The "auto-derive when 0" pattern from PFS-OVERHAUL-006 (memory
[[project_pfs_overhaul_006_config_key_inversion]]) is the right foundation
— Phase 1 generalizes it into the AgentProfile struct.

## The two-cell-size footgun (MapBuilder vs TileWorker)

[`MapBuilder.cpp:547-550`](../../../tools/MmapGen/contrib/mmap/src/MapBuilder.cpp#L547) sets:

```cpp
const static float BASE_UNIT_DIM_MAP_BUILDER = 0.13f; // Differs from BASE_UNIT_DIM which is `0.2666666`. Dont ask me why.
config.cs = BASE_UNIT_DIM_MAP_BUILDER;
config.ch = BASE_UNIT_DIM_MAP_BUILDER;
```

while [`TileWorker.cpp:10121-10122`](../../../tools/MmapGen/contrib/mmap/src/TileWorker.cpp#L10121) sets:

```cpp
config.cs = MMAP::BASE_UNIT_DIM;  // 0.2666666f
config.ch = MMAP::BASE_UNIT_DIM;
```

The "Dont ask me why" comment is the maintainer's flag — two different
default cell sizes in two different code paths, depending on which builder
ran the bake. Phase 1 consolidates by deleting BOTH and computing
`cs = agent.radius / cs_indoor_divisor` from the single AgentProfile.

## Per-tile config.json blocks to delete

Grep counts:
- **34** `_README` blocks: keep all (institutional memory).
- **8** `_NEGATIVE_RESULT_*` blocks: keep all per proposal §3 Phase 1
  step 4.
- **5** actual tile-override blocks (with bake-param values):
  - `"0":"3345"` — walkableErosionRadius=0.2 + debug crop (3-line block)
  - `"0":"3446"` — walkableErosionRadius=0.2 + debug crop (4-line block)
  - `"0":"3546"` — cs=0.15, tileSize=142, walkableErosionRadius=0.2 + debug crop (6-line block)
  - `"1":"4029"` — OG zep tile, ~15 bake params + per-tile flags + debug crops (~30-line block; the BIG one)
  - `"1":"3928"` — debug crop only (2-line block)
- Five `default`-style blocks (top-level + per-map): keep but rewrite
  to reference `agentProfile.tauren_m` (no inline scalars).

Net config.json shrinkage estimate: ~50-80 lines (mostly the 4029 block).

## Sketch — `tools/MmapGen/include/BakeProfile.h`

```cpp
#pragma once
#include <string>

namespace MMAP {

// Physics-engine-derived constants per race. Sourced from
// Exports/Navigation/PhysicsTolerances.h via offline sync. Values below
// match Tauren M (the largest WoW capsule — see config.json _agentNotes).
struct AgentProfile {
    std::string name;
    float radius;          // y, capsule radius
    float height;          // y, capsule height
    float maxClimbTerrain; // y, vertical step-up on terrain
    float maxClimbModel;   // y, step-up on WMO/M2 transition
    float maxSlopeDegrees; // physics-engine accept threshold

    // Mononen derivations
    float csOutdoor() const { return radius * 0.5f; }      // r/2
    float csIndoor()  const { return radius / 3.0f; }      // r/3
    float ch(float cs) const { return cs * 0.5f; }         // cs/2
};

constexpr AgentProfile kTaurenM = {
    .name            = "tauren_m",
    .radius          = 1.0247f,
    .height          = 2.625f,
    .maxClimbTerrain = 1.8f,    // current PFS-OVERHAUL-006 default
    .maxClimbModel   = 1.8f,
    .maxSlopeDegrees = 60.0f,   // physics-engine MAX_SLOPE
};

// Voxel-space derivation. Outdoor variant is default; indoor tiles
// (city WMOs, dungeon) use the indoor variant.
struct BakeProfile {
    float cs;
    float ch;
    int   walkableRadius;     // ceil(radius / cs)
    int   walkableHeight;     // ceil(height / ch)
    int   walkableClimb;      // floor(maxClimb / ch)  — conservative
    float walkableSlopeAngle;
    int   borderSize;         // walkableRadius + 3
    int   maxEdgeLen;         // walkableRadius * 8
    float maxSimplificationError;
    float detailSampleDist;
    float detailSampleMaxError;
    int   minRegionArea;
    int   mergeRegionArea;
    int   maxVertsPerPoly;
    int   tileSize;           // chosen so tileSize * cs * 25 ≈ GRID_SIZE
    // partitionType = watershed (hard-coded; no more "layers" or "monotone")
};

BakeProfile MakeBakeProfile(const AgentProfile& agent, bool indoor = false);

} // namespace MMAP
```

`MakeBakeProfile` body sketch:

```cpp
BakeProfile MakeBakeProfile(const AgentProfile& agent, bool indoor) {
    BakeProfile p{};
    p.cs                     = indoor ? agent.csIndoor() : agent.csOutdoor();
    p.ch                     = agent.ch(p.cs);
    p.walkableRadius         = static_cast<int>(std::ceil(agent.radius / p.cs));
    p.walkableHeight         = static_cast<int>(std::ceil(agent.height / p.ch));
    p.walkableClimb          = static_cast<int>(std::floor(agent.maxClimbTerrain / p.ch));
    p.walkableSlopeAngle     = agent.maxSlopeDegrees;
    p.borderSize             = p.walkableRadius + 3;
    p.maxEdgeLen             = p.walkableRadius * 8;
    p.maxSimplificationError = 1.3f;
    p.detailSampleDist       = p.cs * 6.0f;
    p.detailSampleMaxError   = 0.5f;
    p.minRegionArea          = 20;
    p.mergeRegionArea        = 40;
    p.maxVertsPerPoly        = 6;
    // tileSize chosen so the tile covers GRID_SIZE/32 ≈ 533.33y world units.
    // GRID_SIZE / (p.cs * tile_side_count) where tile_side_count = 25.
    // For cs=0.5124 (outdoor Tauren): tileSize = 533.33 / (25 * 0.5124) ≈ 41.6 → 42.
    p.tileSize               = static_cast<int>(std::round(GRID_SIZE / (25.0f * p.cs)));
    return p;
}
```

Outdoor / indoor selection: tiles inside Orgrimmar (the loop-26 stall
area), BRM, dungeons → `indoor = true`. The tile/map mapping can come
from a known list (city tile coords are well-bounded) or a per-map
config key. **For Phase 1's first pass, use `indoor = false` globally
and document the discrepancy** — indoor refinement is a later iter.

## What Phase 1 deletes (per R18 same-commit)

In the same commit that adds `BakeProfile.h`:

1. `TileWorker.cpp::getDefaultConfig()` — replace the 60+ key dict with
   `MakeBakeProfile(kTaurenM).serialize()` or skip the json dict path entirely.
2. `TileWorker.cpp::from_json(rcConfig)` — derive `rcConfig` directly
   from BakeProfile, drop the json read path.
3. `MapBuilder.cpp::BASE_UNIT_DIM_MAP_BUILDER` constant.
4. `MapBuilder.h::BASE_UNIT_DIM` constant.
5. config.json's 5 per-tile bake-param blocks.

Keep:
1. config.json's `_NEGATIVE_RESULT_*` and `_README` comments (institutional).
2. config.json's per-map `agentRadius` / `agentHeight` blocks
   (rewrite to reference profile by name: `"agent": "tauren_m"`).
3. config.json's `debugStageCropWow` per-tile (debug-only, no bake impact).

## What Phase 1 does NOT touch

The `postDetourCull*`, `preRasterize*`, `preMedianCull*`, `preRegionCull*`
flags (TileWorker.cpp:12068-12106+) are the loop-19→25 **cull pipeline**
machinery. Phase 1 leaves them alone. Phase 5 deletes them together
with the runtime managed repair pipeline (since the cull pipeline IS
managed-repair-pipeline scaffolding at bake time).

## Phase 1 acceptance criteria (per proposal §3)

- Phase-0 probe re-run shows Blocked-poly count down ≥30% globally.
- No tile regressed from >90% Walk to <50% Walk.
- Tile size growth ≤2x.
- Bake-fixture pair LIKELY FAIL (gates on Phase 4 route-pack regen);
  for Phase 1 gate on Phase 0 probe improvement, not fixture passes.

## Pre-conditions before starting Phase 1

- ✅ **D0 — proposal exists** (commit `8de3cc87`).
- ✅ **D1 — probe builds + runs** (commit `81a6096c`).
- 🔄 **D2 — baseline reports** (in-flight; iter-3 sweep pid 29900,
  ETA 160 min as of iter 5).
- ✅ **D3 — test-failure manifest** (commit `ce5d8154`).
- ❌ **D4 — go/no-go findings** (depends on D2 completion; expected iter 7-8).

**Phase 1 starts only after D4 commits with a `go` recommendation.** This
prep doc is the work-breakdown to be ready; Phase 1 work itself begins
in iter 9+ at the earliest.

## Estimated Phase 1 scope (for iter budgeting)

| Work unit | Est. wall-clock | Iters |
|---|---|---|
| Author `BakeProfile.h` + `MakeBakeProfile` impl + tests | 1-2 hr | 1 |
| Wire into TileWorker.cpp's config path + delete the 60-key default dict | 2-3 hr | 1-2 |
| Delete per-tile config.json blocks + restructure per-map blocks | 1 hr | 1 |
| Full re-bake all 41 maps (single-threaded ~3-6 hr at observed iter-3 timing) | 3-6 hr | 1 (background) |
| Phase 0 probe re-run + diff analysis | 1-2 hr | 1 |
| Iterate Mononen-rule tightening if Blocked-drop <30% | 2-4 hr | 1-2 |

**Phase 1 total estimate:** 10-18 hr wall-clock, 5-8 iters.
