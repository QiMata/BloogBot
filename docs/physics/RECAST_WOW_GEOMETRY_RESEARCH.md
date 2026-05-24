# Recast And WoW Geometry Research

This note captures the current, source-backed answer to the recurring WWoW
question:

> Why do we sometimes get large, overlapping-looking navmesh slabs on top of
> shallow walls, aprons, stairs, or tower supports, and which knobs are worth
> tuning first?

The goal is not "random Recast lore." It is to keep future sessions from
re-learning the same lessons by trial and error.

## Current conclusion

For the Orgrimmar zeppelin tower class of artifact, the bad polygons are
usually **not** created first by Detour or by final polygon triangulation.
They are created earlier when shallow model faces survive rasterization and
filtering as walkable spans, then later get simplified into big floor-plus-wall
slabs.

The strongest local evidence is the stage probe bundle at
`tmp/test-runtime/visualization/pathfinding/og-zeppelin/hfprobe_3184/`:

- suspicious upper span bands already survive at `buildCHF`, e.g.
  `54.408905..79.908905` and `54.508904..80.008904`
- lower stacked bands are still alive at `erode`, e.g.
  `32.608902..51.408905`
- the final ugly polys are therefore downstream consequences of contaminated
  walkable cells, not the first point of failure

See:

- [PHASE4_GO_VARIANTS.md](./PHASE4_GO_VARIANTS.md)
- [PATHFINDING_VISUAL_DIAGNOSTICS.md](./PATHFINDING_VISUAL_DIAGNOSTICS.md)
- [TileWorker.cpp](../../../Westworld%20of%20Warcraft/tools/MmapGen/contrib/mmap/src/TileWorker.cpp)

## 2026-05-21 upstream review

The controlled upgrade review on 2026-05-21 produced this source-backed state:

- Official latest tagged release is still `1.6.0` (2023-05-21):
  https://github.com/recastnavigation/recastnavigation/releases
- Upstream `main` has moved well beyond that. The standalone checkout at
  `E:/repos/tools/recastnavigation` is already at `9f4ce64`
  (`Fix crash on large-scale navmesh generation`, 2025-11-15), which matches
  current upstream `main` as of this review:
  https://github.com/recastnavigation/recastnavigation/commits/main/
- WWoW's vendored MmapGen copy under
  `tools/MmapGen/dep/recastnavigation/` was still missing some merged Recast
  bake-side fixes that matter for tile generation even though it already
  carried later local geometry work in `TileWorker.cpp`.

Backport decision for WWoW's vendored copy:

- **PORT:** tile-border rasterization fix from upstream commit `3901c58`
  / PR `#476` (`Fix incorrect rasterization at tile borders`).
  Rationale: this is merged, small, and directly affects border-cell span
  correctness before filtering/regions/contours:
  https://github.com/recastnavigation/recastnavigation/pull/476
- **PORT:** exact-clearance low-height fix from upstream commit `9432fd6`
  / PR `#626` (`Fix spans being filtered even if they have just enough clearance`).
  Rationale: this keeps spans with exactly `walkableHeight` clearance alive,
  which matters on tight WoW deck/ramp geometry:
  https://github.com/recastnavigation/recastnavigation/pull/626
- **KEEP LOCAL:** the local `rcBuildPolyMesh` ear-clipping fix equivalent to
  PR `#734` is already present in both local trees. Upstream PR `#734` is still
  open, so there was nothing new to cherry-pick there:
  https://github.com/recastnavigation/recastnavigation/pull/734
- **DO NOT BLIND-PORT YET:** adjacent-box rasterization rounding PR `#766`
  is relevant to voxelization, but is still open upstream. It addresses
  `(-1,0)` rounding when clipping neighboring boxes and is worth keeping in the
  research queue, but it was not adopted blindly for WWoW without a local repro:
  https://github.com/recastnavigation/recastnavigation/issues/765
  https://github.com/recastnavigation/recastnavigation/pull/766

### 2026-05-22 migration outcome

The review phase was carried through into a controlled full vendor sync for the
Recast bake core used by WWoW MmapGen:

- `tools/MmapGen/dep/recastnavigation/Recast/Include/*` and
  `Recast/Source/*` now byte-match upstream `main` at `9f4ce64`. The earlier
  PR `#476` / `#626` backports are therefore subsumed by the full sync.
- The only local integration seam needed after the sync was replacing the
  retired helper `SortAndRasterizeTriangles(...)` with upstream
  `rcRasterizeTriangles(...)` in
  [TileWorker.cpp](/E:/repos/Westworld%20of%20Warcraft/tools/MmapGen/contrib/mmap/src/TileWorker.cpp:1749).
- WWoW-specific bake behavior remains local in
  [TileWorker.cpp](/E:/repos/Westworld%20of%20Warcraft/tools/MmapGen/contrib/mmap/src/TileWorker.cpp:77)
  and is intentionally **not** overwritten by the Recast vendor sync:
  custom ledge filtering, mixed-wall / shadowed-ledge culls, per-tile override
  handling, and GO bake plumbing remain ours.
- Split-root bake parity is now explicit: when baking into
  `D:/wwow-bot/test-data`, MmapGen falls back to
  `WWOW_VMANGOS_DATA_DIR/gameobject_spawns.json` if the mutable data root does
  not carry its own copy. This keeps the canonical test-data/prod-data flow
  compatible with GO-backed city/WMO tiles without reintroducing managed repair
  hacks.
- `NavDataAudit` now accepts explicit `--config-path` and `--spawns-path`
  inputs and treats `walkableRadius=0` / `walkableHeight=0` in
  `tools/MmapGen/config.json` as the intended "auto-derive from agent
  dimensions" contract instead of a false failure.

Focused proof on the Orgrimmar zeppelin tower tile (`map 1`, `tile 40,29`,
runtime `0012940.mmtile`) after the full sync:

- test-data bake log:
  `tmp/bake-sweeps/recast-full-sync-og-4029-go-fallback-20260522T000716Z/bake.log`
- audit manifest:
  `tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260522_full_recast_sync_testdata_manifest.json`
- mesh-quality gate:
  `tmp/test-runtime/results-pathfinding/mmap_mesh_quality_org_zeppelin_full_recast_sync_testdata.trx`
- route gate on test-data:
  `tmp/test-runtime/results-pathfinding/pathfinding_org_fm_static_blockers_full_recast_sync_testdata.trx`
- route gate on promoted prod-data:
  `tmp/test-runtime/results-pathfinding/pathfinding_org_fm_static_blockers_full_recast_sync_proddata.trx`

The migrated bake stayed green through the same OG proof slice. The refreshed
`og-zeppelin/latest` bundle now reports `268` polygons in the focused top
ramp/deck crop, `187` reachable, `81` unreachable, and worst `zRange=1.000y`,
which remains below the focused regression thresholds.

### 2026-05-22 anchor-stack follow-up

The next raw-Detour follow-up loop targeted the six remaining Orgrimmar city /
hallway / exterior dead-end reds with a tile-local final-tile cull driven by
verified probe anchors on tile `1:40,29`.

What the probes proved:

- direct queries at the dead-end anchor coordinates can often resolve to a good
  support polygon and route onward, while longer routes into the same XY choose
  a competing overlapping ground polygon and dead-end locally
- therefore the failure surface is still bake-side overlapping final-tile
  topology, not a smoothing-only bug and not a justification for post-path
  generation repair

Implementation findings:

- the new `postDetourCullAnchorPolyStacks` pass belongs in
  `TileWorker.cpp`, after the tile is added to a temporary `dtNavMesh`
- `dtNavMeshQuery::init(...)` for this pass must use a modest fixed node budget
  (`4096` worked). A `polyCount`-scaled budget failed query initialization on
  this large tile and silently disabled the pass until instrumented.
- verify serialized-tile changes with a hash and a follow-up probe. The tile's
  byte length stayed unchanged across these experiments even when the SHA256 and
  cull logs changed.

Negative result from this loop:

- removing the anchor-support overlap gate made the cull much more aggressive
  (`13/5/14/5` anchor-member disables across the four configured anchors), but
  it did **not** close the hallway/vertical dead-end routes and it regressed
  `orgrimmar_exterior_steep_incline_live_stall_recovery`, worsening the
  `CriticalWalkLegs` sweep from `17/23` to `16/23`
- conclusion: dead-end endpoint anchors alone are not sufficient. The corridor
  can remain on a larger approach polygon and still terminate in the same local
  basin. The overlap-less variant is a recorded negative result, not a
  promotable fix.

## What upstream Recast says

### 1. Rasterization and filtering are conservative

Official Recast documents the core raster/filter stages here:

- Recast API/group docs: https://recastnav.com/group__recast.html
- `rcConfig` docs: https://recastnav.com/structrcConfig.html

The key practical implication for WoW city/WMO geometry is that:

- `rcRasterizeTriangles` will happily emit multiple spans per cell when the
  geometry supports it
- `rcFilterLowHangingWalkableObstacles` can re-promote spans based on climb
  tolerance
- `rcFilterLedgeSpans` only kills spans that fail the local drop / climb rules

That means a shallow wall apron or support face can remain walkable if its
local neighbor relationships still satisfy the configured climb/height tests.

This matches what we see in WWoW's custom `filterLedgeSpans(...)` path at
[TileWorker.cpp](/E:/repos/Westworld%20of%20Warcraft/tools/MmapGen/contrib/mmap/src/TileWorker.cpp:750)
and the surrounding bake order at
[TileWorker.cpp](/E:/repos/Westworld%20of%20Warcraft/tools/MmapGen/contrib/mmap/src/TileWorker.cpp:1952).

### 2. Watershed partitioning has known overlap/hole corner cases

Upstream Recast's own sample comments warn that watershed partitioning has
corner cases with holes and overlaps, especially around narrow spiral corridors
and stairs. Our vendored sample still carries those comments:

- [Sample_TileMesh.cpp](/E:/repos/tools/recastnavigation/RecastDemo/Source/Sample_TileMesh.cpp:2070)

Relevant upstream discussion:

- Recast discussion on overlapping regions and partition tradeoffs:
  https://github.com/recastnavigation/recastnavigation/discussions/583

This does **not** mean "watershed is broken." It means that if bad spans make
it through into the compact heightfield, watershed can turn them into a more
visibly ugly final result than the earlier stages suggested.

### 3. The recent `rcBuildPolyMesh` fix is worth keeping, but it is not the main cause here

Upstream fixed a real concave/ear-clipping issue in `rcBuildPolyMesh`:

- https://github.com/recastnavigation/recastnavigation/pull/734

We should keep that fix, and the local trees already do. But it does not explain the Orgrimmar tower slab by
itself, because our local evidence shows the suspicious spans already exist
before `rcBuildContours` / `rcBuildPolyMesh`.

## What other WoW-server ecosystems do

A useful cross-check is AzerothCore's public mmaps generator documentation:

- tile override fields:
  https://www.azerothcore.org/doxygen/d3/dbc/structMMAP_1_1Config_1_1TileOverride.html
- config resolution / map->tile override order:
  https://www.azerothcore.org/doxygen/d8/da7/classMMAP_1_1Config.html

Why this matters for WWoW:

- per-map and per-tile navmesh tuning is not some weird local hack
- the common MMO-server pattern is to preserve a stable global baseline and
  use focused overrides for pathological tiles
- that lines up with the direction we already took for OG tile `1:40,29`

## Knobs that are actually worth researching first

These are the knobs that repeatedly matter for WoW-style ADT + WMO + GO city
geometry.

### `cellSize` / `cellHeight`

Why it matters:

- finer voxel resolution captures stairs, lips, and thin connector surfaces
  better
- it also dramatically increases tile complexity and can create more tiny spans
  to merge later

Working WWoW guidance:

- use fine `cs/ch` only where the geometry justifies it
- OG `1:40,29` is one of those tiles; broad global tightening is too expensive

### `walkableClimb`

Why it matters:

- it controls both low-hanging obstacle promotion and the ledge decision
- too high: wall aprons/support faces survive as reachable
- too low: legitimate stairs/ramps fracture

Working WWoW guidance:

- do not globally lower climb just to kill one bad slab
- if the problem is model/terrain transition behavior, investigate the
  transition rule rather than only the raw climb constant

### `walkableSlopeAngle` / model slope angle

Why it matters:

- it decides what is walkable before filtering
- global slope tightening can remove real footing on VMap/WMO decorations,
  rocks, and thin city surfaces

Working WWoW guidance:

- do **not** globally collapse VMap-side slope rules to the player physics
  threshold just because one city tile is bad
- our live BRM regressions already proved this can remove valid footholds

### erosion radius vs final agent metadata

Why it matters:

- tying support-surface erosion directly to the largest collision capsule can
  erase thin WMO deck connectors

Working WWoW guidance:

- separate "Recast source-support erosion" from "final Detour agent metadata"
  when the tile geometry is pathological
- OG `1:40,29` already benefited from this pattern

### `maxSimplificationError` / `maxEdgeLen`

Why it matters:

- looser simplification produces the exact long thin bridge/slab class we hate
- tighter simplification can explode polygon count and A* runtime

Working WWoW guidance:

- treat simplification changes as graph-structure changes, not cosmetic tweaks
- always pair them with route/runtime evidence, not only OBJ screenshots

### partitioning mode

Why it matters:

- watershed gives nicer regions most of the time but has overlap/hole corner
  cases on narrow stair-like geometry
- monotone/layers have different failure modes and are worth keeping in mind
  as diagnostic surfaces on pathological city/WMO tiles

Working WWoW guidance:

- if a tile's bad spans are already fixed, watershed is usually fine
- if a tile still shows overlap-like output after span cleanup, partition mode
  is a valid next experiment

## WWoW-specific working theory for the tower slab

The tower/ramp artifact happens because:

1. shallow model support/apron faces are rasterized into valid spans
2. low-hanging + ledge filtering do not kill them
3. the contaminated spans survive into `buildCHF`
4. region growth and simplification turn them into a broad floor-plus-wall poly

So the next fix surface is usually one of:

- stricter bake-side rejection of shadowed/shallow stacked spans before
  contours
- stronger post-polymesh or post-Detour culling for the mixed-wall class
- partition-mode experimentation for pathological tiles after the span issue is
  under control

It is usually **not**:

- Detour query-time special casing
- managed path repair
- route-specific hardcoded blockers

## Repeatable process for future sessions

1. Export the focused route/tile bundle with
   `tools/scripts/export-pathfinding-reference.ps1`.
2. Inspect source geometry, then runtime/generator poly reports, then stage
   heightfield/compact-heightfield CSVs.
3. Before touching knobs, read the current upstream docs/discussions/PRs for the
   specific Recast stage you suspect.
4. Record the research links and the hypothesis in repo docs before changing
   generator behavior.
5. Add or update a mesh-quality regression rather than relying on a viewer
   screenshot.

## 2026-05-22 Raw-Runtime Follow-Up

This follow-up happened after the managed/native repair path was retired from
the default runtime route and the remaining OG zeppelin failures were re-run
against raw Detour.

### Primary-source check that mattered

- RecastNavigation PR `#622`:
  https://github.com/recastnavigation/recastnavigation/pull/622
  documents a real Detour builder bug: off-mesh connections whose destination
  lies outside the source tile need one extra start-tile `dtLink` beyond the
  historical `offMeshConLinkCount * 2` sizing rule.
- RecastNavigation PR `#645`:
  https://github.com/recastnavigation/recastnavigation/pull/645
  is useful context, but its own description explicitly says it does not solve
  the "load tiles from binary data later" case by itself. That matters for
  WWoW because `.mmtile` files are serialized and then loaded by runtime
  `Navigation.dll`.

### WWoW-specific findings

- The canonical city lower-approach off-mesh entry in tile `1:40,28`
  (`1604.8,-4425.6,10.36 -> 1320.14,-4653.16,53.89`) was previously a likely
  weak point. After the focused `40,28` regen it now base-links cleanly at
  runtime (`[OFFLINK] LINKED dxz^2=0.51`), so the remaining city-start dead
  ends are not "the tile lost its off-mesh entry".
- On tile `1:40,29`, the current runtime still snaps several boarding-related
  off-mesh starts onto tiny tower-ramp support polys:
  - `1356.8,-4501.3,29.44` base-links to polyref `0x1000015204814`
  - `1357.2,-4516.2,32.0` base-links to polyref `0x10000152047F5`
  - `1381.0,-4380.9,26.0` base-links to polyref `0x1000015204E58`
- The raw-runtime top-ramp route improved anyway after the focused bake loop:
  `orgrimmar_zeppelin_tower_ramp` dropped out of the failing
  `CriticalWalkLegs` set, which is strong evidence that the remaining issue is
  narrower than the earlier "Detour is broadly broken on the tower" theory.

### Bake-side conclusion

The best low-risk fix surface for this pass was not a new route-time repair.
It was:

1. keep the raw Detour runtime path
2. patch the off-mesh link-capacity bug in the Detour builder
3. add an opt-in, tile-local final-tile cull for small steep components that
   are actually selected as off-mesh start landing polys

That is why `tools/MmapGen/config.json` now opts tile `4029` into
`postDetourCullOffMeshAnchorSteepTrim`.

### What remains

- The exact OG tower-ramp route is no longer one of the red cases.
- The remaining raw-runtime failures are now concentrated in:
  - city/interior starts that still dead-end locally before they ever reach the
    lower-approach/off-mesh corridor
  - the underpass screenshot recovery, which reaches the `1357.2,-4516.2`
    anchor region but still prefers a bad early ground climb shape

Those are now much better isolated than the original mixed-wall / shadowed trim
artifact class.

### 2026-05-22 Detour PR #725 follow-up

I tested upstream Detour PR
[#725](https://github.com/recastnavigation/recastnavigation/pull/725)
(`fix findNearestPoly result error`) against WWoW's OG tower tile because the
remaining city/hallway/exterior failures still looked like bad nearest-support
selection around stacked local basins.

What translated cleanly:

- The upstream diagnosis is relevant: our fork still had
  `header->bvNodeCount = params->polyCount*2`, and the PR's claim is that
  dummy zeroed BV nodes can be traversed and distort `findNearestPoly`.
- A direct one-line port was **not** safe in WWoW's fork. Here,
  `header->bvNodeCount` is also used by the loader to compute serialized
  section offsets, so shrinking the header count without shrinking the stored
  BV-tree block caused the off-mesh connection section to be read from the
  wrong offset.
- The correct local port is a two-step adaptation:
  1. build the BV tree into a temporary buffer
  2. size the serialized BV-tree block from the actual returned node count and
     then copy only those nodes into the final tile payload

Outcome of the corrected port:

- `0012940.mmtile` changed safely:
  - prior working hash:
    `6046E861EA352D8F00DE735591E15DCFF6785D6464FC9B0758EF97FE9D6E251D`
  - final PR-725-adapted hash:
    `43EACD6F5E53818F0478550EC8D4CB407F95C82528C79F7B07D060FCDCACC744`
  - byte size shrank by `16` bytes
- Focused OG checks stayed green.
- Full `CriticalWalkLegs` returned to the same `17/23` result as the previous
  non-PR-725 baseline.

Conclusion:

- PR `#725` is worth keeping as a **safe Detour correctness upgrade** in WWoW,
  but it did **not** solve the remaining six OG dead-end cases.
- Those failures are still a bake/topology problem first, not a runtime
  nearest-poly metadata bug.

### 2026-05-23 anchor-stack follow-up

I spent another bake-side loop on the remaining Orgrimmar city/hallway dead-end
cases, keeping the path runtime raw/native and only touching the final-tile
cull logic in `TileWorker.cpp`.

What I tested:

1. `ProbeAnchorPolyAtCoord(...)` support fallback from exact `getPolyHeight`
   to a conservative `closestPointOnPoly`-backed support surface when the
   nearest point stayed within `1.0y` XY.
2. A stricter anchor-support floor for `CullAnchorPolyStacks(...)`:
   support must be at/above the anchor Z (`surfaceDelta >= 0.0`) instead of
   accepting `-0.25y` lower fringe slabs as support.
3. Extra anchor-cull probe coords taken from the actual failed route collapse
   points:
   - vertical: `(1546.6,-4435.9,11.5)`
   - hallway: `(1521.3,-4422.5,17.1)`, `(1522.5,-4424.1,17.0)`,
     `(1523.8,-4425.9,17.1)`
   - hall-exit: `(1493.4,-4418.6,23.3)`
4. An anchor-local lower-fringe cull: when an anchor window had no exact
   support but *did* have a higher overlapping ground layer, the pass disabled
   only the lower fringe polys whose `maxY` stayed at/below the anchor plane.

What the probes proved:

- The remaining city/hallway/exact-start failures are still dominated by
  slightly-below-anchor nearest-poly winners:
  - vertical start `(1545.0,-4434.5,11.1)` -> winner
    `0x100001520BEEE`, `surfaceZ=11.009`, `posOverPoly=0`
  - hallway start `(1518.2,-4419.8,17.1)` -> winner
    `0x100001520ADA2`, `surfaceZ=16.885`, `posOverPoly=0`
  - hall-exit start `(1491.4,-4417.3,23.3)` -> winner
    `0x1000015209D5A`, `surfaceZ=23.209`, `posOverPoly=0`
- In other words, the current trap is not “Detour smooth-path clips corners.”
  It is “findNearestPoly still prefers a lower local basin/fringe slab at the
  start anchor.”
- Tightening the anchor-support floor to `>= anchorZ` proved that several of
  the old “supports” were actually those lower fringes; many exact-start
  anchors then became `supports=0`, which is strong evidence that the current
  verified start coords are seam-noisy rather than truly centered on the good
  support layer.

What did and did not improve:

- The “closest support fallback” branch engaged correctly and still held the
  focused OG checks green (`6/6`), but the full `CriticalWalkLegs` sweep
  stayed at `18/23`.
- The stricter support floor plus candidate collapse-point anchors also kept
  the focused OG checks green, but the full sweep stayed `18/23`.
- The new lower-fringe cull *did* materially change the serialized tile
  (current experimental hash
  `01629C2251081B8C00E1F546F1690053B70BD8C9491641696603F926D373F9F3`)
  and the bake log showed real lower-fringe removals on the city/hallway
  anchors, but it still did not move the total beyond `18/23`; the hallway
  failure even shortened from a 5-corner dead-end to a 2-corner dead-end, so
  that branch is **not** a promote candidate.

Takeaway from this loop:

- The remaining dead-end cases are now isolated to a narrower problem than
  “generic stacked walkable support.”
- The next likely fix surface is even more specific:
  - either cull the *actual nearest-poly winner component* at verified bad
    anchors when a better overlapping layer exists, or
  - move earlier than final Detour tile surgery and split these start-cell
    basin layers before they become merged Detour ground polys.

## Links

- Recast API docs: https://recastnav.com/group__recast.html
- `rcConfig` docs: https://recastnav.com/structrcConfig.html
- Detour `findNearestPoly` BV-tree fix PR:
  https://github.com/recastnavigation/recastnavigation/pull/725
- Recast region/overlap discussion: https://github.com/recastnavigation/recastnavigation/discussions/583
- Recast `rcBuildPolyMesh` fix: https://github.com/recastnavigation/recastnavigation/pull/734
- AzerothCore tile overrides: https://www.azerothcore.org/doxygen/d3/dbc/structMMAP_1_1Config_1_1TileOverride.html
- AzerothCore config resolution: https://www.azerothcore.org/doxygen/d8/da7/classMMAP_1_1Config.html

### 2026-05-23 preferred-support and minRegionArea probes

I ran two more upstream-backed loops against tile `1:40,29` after the
anchor-support-epsilon/source-backed state:

1. A final-tile `CullAnchorPolyStacks(...)` experiment that chose a preferred
   support poly per anchor and tried to trim redundant same-height support
   candidates.
2. A pure config experiment with tile-local `minRegionArea=60` via
   `tmp/config-experiments/og_4029_minRegionArea60.json`, based on the
   official `rcConfig` guidance that `minRegionArea` removes small isolated
   regions after watershed partitioning.

What happened:

- The preferred-support branch changed the serialized tile hash to
  `345FA5BBFF7BDFDCFE58B3B061C9E25D162B17723A536C8FDE85E2383FBBA671`, so it was
  a real bake change.
- But exact dead-end winner probes did **not** move:
  - `(1535.267,-4437.9,13.909)` -> `0x100001520BE35`
  - `(1521.267,-4425.6,17.609)` -> `0x100001520AD5D`
  - `(1479.867,-4425.8,25.309)` -> `0x1000015208D00`
  - `(1364.867,-4374.0,26.109)` -> `0x1000015204ECD`
  - `(1357.2,-4516.2,32.2)` -> `0x10000152047F5`
- Exact `--dump-poly-stack` probes still showed same-height competitor polys
  around those winners, and in each of the four city/hallway/exterior cases the
  exact winning poly itself remained the `posOverPoly=1` support. That means
  the remaining trap is no longer just “pick the right support shard”; the
  containing local basin/component is still wrong.
- Because the branch changed bits but did not change the exact winner set or
  the route score, I reverted it from source.

`minRegionArea=60` also proved to be a negative result:

- Focused OG mesh/route checks stayed green (`6/6`).
- Full raw-Detour `CriticalWalkLegs` stayed `17/23`, with the same six reds.
- The bake logs show that the earlier compact-span anchor cull still did real
  work in the in-range subtiles (`79`, `445`, `1237`, `349` culled spans on the
  relevant windows), but increasing isolated-region rejection alone did not
  move the final failure surface.

Current restored source-backed state:

- `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
- hash: `E299BDC34EEFD82F2B0466B66BE09E7BDCDC3A683C59106778D96A55C01824B4`
- focused OG checks: `6/6` green
- full `CriticalWalkLegs`: `17/23`

Takeaway:

- The remaining OG reds are not closing with support scoring churn or generic
  isolated-region tuning.
- The next useful fix surface is narrower:
  - a bake-side cull of the *actual winning local basin/component* at the
    verified bad anchors, or
  - an earlier region/contour change that prevents that basin from surviving
    into the final Detour tile at all.

### 2026-05-23 original-worker bake comparison and WoW geometry feed audit

I ran a direct comparison bake with the earliest in-repo `TileWorker.cpp`
baseline we can still build cleanly against the fully synced vendored Recast:
local commit `4e3716ae` (`2026-05-07`), with one scratch-only compatibility
edit replacing the removed legacy `SortAndRasterizeTriangles(...)` helper with
upstream `rcRasterizeTriangles(...)`.

Why this baseline matters:

- It is the closest buildable "original WWoW worker" baseline inside this repo.
- It predates the later GO-spawn ingestion, split-root fallback, debug stage
  crop/export, erosion overrides, partition overrides, suspicious-poly culls,
  and anchor-stack cleanup passes.
- It still already carries the earlier large-capsule/agent-dimension fixes, so
  the comparison is not polluted by the old `agentRadius=0.2` / `agentHeight=1.5`
  vmangos defaults.

Commands/results:

- Scratch baseline source:
  - `tmp/mmapgen-baseline-20260507/contrib/mmap/src/TileWorker.cpp`
- Scratch build:
  - `tmp/mmapgen-baseline-20260507/build/MmapGen.exe`
- Focused baseline bake log:
  - `tmp/bake-sweeps/tileworker_20260507_baseline_20260523T123759Z/bake.log`
- Baseline tile hash after bake:
  - `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  - `5EC417472F918E93A1255098FFFDD86B1F56CDE91E4BA0ED8235CCD004C49675`
- Focused audit manifest:
  - `tmp/test-runtime/results-navigation/tileworker_20260507_baseline_tile4029_manifest.json`
- Focused mesh-quality test:
  - `tmp/test-runtime/results-pathfinding/tileworker_20260507_baseline_focused_mesh_quality.trx`
- Focused static-blocker route gate:
  - `tmp/test-runtime/results-pathfinding/tileworker_20260507_baseline_route_gate.trx`

What the baseline produced:

- `NavDataAudit` still passed the Detour/Tauren-size header checks.
- But the audit failed both GO feed proofs:
  - `bake.log does not show gameobject spawn loading`
  - `bake.log has no GO geometry bake line for tile 40,29`
- `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck*` still passed `4/4`.
- `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  failed again, with the old-style blocker set:
  - lower flight-master bonfire
  - Z-hallway north early-cut corner
  - Z-hallway south early-cut corner
  - exterior steep incline
  - exterior rope-line support snag

That baseline result is important because it isolates one of the biggest real
gains from the current worker: even before the later anchor/dead-end
experiments, **feeding server-spawned GO geometry into the bake materially
improves this tile**.

Geometry-feed research from primary sources:

- Stock vmangos `TileWorker.cpp` on `development` still shows the classic input
  shape: `loadMap(...)`, `loadVMap(...)`, `loadOffMeshConnections(...)`, then
  pure Recast processing. It does not ingest `gameobject_spawns.json` or
  `temp_gameobject_models`, and it still carries the historical default
  `agentHeight = 1.5f`, `agentRadius = 0.2f` when config leaves those unset.
- TrinityCore `3.3.5` `MapBuilder.cpp` follows the same high-level input flow:
  `loadMap(...)`, `loadVMap(...)`, `loadOffMeshConnections(...)`, then tile
  build. AzerothCore `master` matches that same shape.
- TrinityCore's own discussion history explicitly acknowledges that the tools
  do not generate perfect navmeshes and that project maintainers already rely
  on manual map-specific generator adjustments for problematic cases.

What that implies for WWoW:

- "Switch to stock TrinityCore/AzerothCore mmaps_generator" is **not** a real
  fix for this tile class. Those generators do not feed server-spawned static
  GO supports/blockers into the bake the way WWoW now does.
- WWoW's current geometry-feed direction is the correct one:
  1. bake real GO world-model triangles when a display model exists
  2. fall back to conservative `rcMarkBoxArea(...)` null-area marking for
     modeled GO spawns whose full geometry is unavailable
  3. then debug/tune the raster/filter/region pipeline on that richer WoW input
- If we want *better* geometry feed than the current worker, the next likely
  improvements are WoW-specific and pre-rasterization:
  - richer provenance tagging in diagnostics (`terrain`, `vmap`, `go-baked`,
    `go-box-null`)
  - better GO model coverage / export completeness
  - bake-time physics-backed rejection of suspicious support layers before they
    survive into the compact heightfield / final Detour tile

Current restored state after the baseline comparison:

- `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
- restored hash:
  `E299BDC34EEFD82F2B0466B66BE09E7BDCDC3A683C59106778D96A55C01824B4`

Primary-source links:

- vmangos `TileWorker.cpp`:
  https://raw.githubusercontent.com/vmangos/core/development/contrib/mmap/src/TileWorker.cpp
- TrinityCore `3.3.5` `MapBuilder.cpp`:
  https://raw.githubusercontent.com/TrinityCore/TrinityCore/3.3.5/src/tools/mmaps_generator/MapBuilder.cpp
- TrinityCore `3.3.5` `TerrainBuilder.cpp`:
  https://raw.githubusercontent.com/TrinityCore/TrinityCore/3.3.5/src/tools/mmaps_generator/TerrainBuilder.cpp
- AzerothCore `MapBuilder.cpp`:
  https://raw.githubusercontent.com/azerothcore/azerothcore-wotlk/master/src/tools/mmaps_generator/MapBuilder.cpp
- AzerothCore `TerrainBuilder.cpp`:
  https://raw.githubusercontent.com/azerothcore/azerothcore-wotlk/master/src/tools/mmaps_generator/TerrainBuilder.cpp
- TrinityCore discussion on extractor/mmaps limitations and map-specific
  manual changes:
  https://github.com/TrinityCore/TrinityCore/discussions/26868

2026-05-23 source-support pre-region probe follow-up on tile `1:40,29`:

- Goal:
  - take the earlier "use source triangles as the support oracle" idea one step
    earlier and prove which bake stage actually loses the intended upper floor
  - do this without reintroducing route-time repair
- Code changes:
  - added source-support stage probes in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`
  - the new probes log `HF-SRC-ANCHOR` / `CHF-SRC-ANCHOR` summaries for the
    verified OG anchor list during raster/filter/compact/erode/median stages
- Commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_stage_support_probe -DataDir D:\wwow-bot\test-data -Quiet
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_stage_support_probe_v2 -DataDir D:\wwow-bot\test-data -Quiet
```

- Artifacts:
  - `tmp/bake-sweeps/og_4029_stage_support_probe-20260523T133327Z/`
  - `tmp/bake-sweeps/og_4029_stage_support_probe_v2-20260523T133825Z/`
- What this round proved:
  - the active `preRegionCullAnchorSourceSupportCompetingSpans` branch still
    did **not** produce a closing route fix
  - the v2 probe plumbing is useful as diagnostics, but the current per-subtile
    log stream is still noisy/repeated and is not yet clean enough to treat as
    a promotion-quality proof surface
  - because this branch remained unproven, it was demoted back to
    diagnostic-only in `tools/MmapGen/config.json`
- Practical conclusion:
  - keep the new stage-probe code as instrumentation
  - do not treat the source-support pre-region cull itself as a validated fix
  - next iterations should either tighten the probe to one known subtile/window
    or move to a different earlier-stage proof surface (tile-border clipping /
    rasterized support coverage / source-to-heightfield provenance)

2026-05-23 partition-vs-simplification isolation on tile `1:40,29`:

- Goal:
  - separate the effect of `partitionType=layers` from
    `maxSimplificationError=1.3`
  - stop treating the regressed source-backed tile as one undifferentiated
    "layers branch"
- Experiment configs:
  - `tmp/config-experiments/og_4029_watershed_13.json`
  - `tmp/config-experiments/og_4029_layers_18.json`
- Commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_watershed_13_rerun -DataDir D:\wwow-bot\test-data -ConfigPath "tmp\config-experiments\og_4029_watershed_13.json" -Quiet
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_layers_18_rerun -DataDir D:\wwow-bot\test-data -ConfigPath "tmp\config-experiments\og_4029_layers_18.json" -Quiet
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_source_restore_watershed18 -DataDir D:\wwow-bot\test-data -ConfigPath "tools\MmapGen\config.json" -Quiet

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal"
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=PathfindingService.Tests.LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal"
```

- Results:
  - `watershed + 1.3`
    - hash:
      `932A176CD19C96B38E319ACDFD085A3BD9BC68E00FB6A792AB541F69F7AC713C`
    - focused slice regressed to `4/7`:
      `tmp/test-runtime/results-pathfinding/og_4029_watershed_13_rerun_focused.trx`
    - failure shape:
      - giant bridge/auto-completed polys came back
      - shadowed lower trim ledges came back
      - Frezza shortcut lost its off-mesh leg entirely
        (`offMeshPolyCount=0`, total corridor poly count `299`)
  - `layers + 1.8`
    - hash:
      `814BA912D2089383FEB6AA5836AC4FAC62F16FE21B22E9B2FEE8DD2E2B2DBBE3`
    - focused slice regressed to `5/7`:
      `tmp/test-runtime/results-pathfinding/og_4029_layers_18_rerun_focused.trx`
    - failure shape:
      - one remaining shadowed lower trim ledge
      - deck connector density dropped below the focused threshold
  - restored source-backed `watershed + default simplification`
    - hash:
      `FB2FBAF1848FC2ACFB1F9E093A8EC99284C9C19843CD64E4F15CA4FBBF3315D6`
    - focused slice restored to `7/7`:
      `tmp/test-runtime/results-pathfinding/og_4029_source_restore_watershed18_focused.trx`
    - full raw-Detour sweep restored to the known baseline `17/23`:
      `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_source_restore_watershed18.trx`

- Practical conclusion:
  - `maxSimplificationError=1.3` is the more dangerous of the two knobs on
    this tile; tightening contour simplification is sufficient by itself to
    reintroduce giant bridge polys and break the Frezza off-mesh corridor
  - `layers` is still not promotion-ready even at default simplification; it
    continues to regress the top-ramp/deck slice
  - source config was restored by removing both overrides so tile `40,29`
    returns to the known-best focused state while keeping the broader
    `17/23` raw-Detour baseline intact

## 2026-05-23 anchor stage manifest proof surface on tile `1:40,29`

- Goal:
  - stop treating the remaining Orgrimmar dead-end starts as a query-time
    mystery and prove, per verified anchor, the first bake stage where the
    intended upper support disappears or a lower competing basin becomes the
    dominant survivor
- Implementation landed:
  - `tools/MmapGen/contrib/mmap/src/TileWorker.cpp` now writes a structured
    per-anchor manifest covering:
    - `rasterize`
    - `filterLowHanging`
    - `filterLedge`
    - `removeUseless`
    - `filterLowHeight`
    - `waterInheritance`
    - `buildCHF`
    - `markGameObjects`
    - `erode`
    - `median`
    - `regions`
    - `contours`
    - `polymesh`
    - `finalDetour`
  - `tools/scripts/bake-tile.ps1` now copies the manifest into
    `tmp/bake-sweeps/<variant>/analysis/` and runs
    `tools/NavDataAudit --stage-summary-only` to emit summary JSON + CSV
  - `tools/NavDataAudit/StageManifestAnalyzer.cs` is the machine-readable
    reducer for `first bad stage` / `first bad reason`
- Commands:

```powershell
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1' -Configuration Release

$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1' -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_stage_manifest_clean' -DataDir 'D:\wwow-bot\test-data'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_stage_manifest_clean_focused.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'
```

- Final artifacts:
  - bake dir:
    `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/`
  - raw manifest:
    `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_manifest.json`
  - summary JSON:
    `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_summary.json`
  - summary CSV:
    `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_summary.csv`
  - focused validation:
    `tmp/test-runtime/results-pathfinding/og_4029_anchor_stage_manifest_clean_focused.trx`
- Validation:
  - final tile hash stayed on the restored source-backed snapshot:
    `FB2FBAF1848FC2ACFB1F9E093A8EC99284C9C19843CD64E4F15CA4FBBF3315D6`
  - focused OG slice stayed green `7/7`
  - with `logAnchorStageDiagnostics=false`, the bake log no longer contains the
    replaced source-support probe spam:
    - `SRC-ANCHOR-SUPPORT`
    - `HF-SRC-ANCHOR`
    - `CHF-SRC-ANCHOR`
    - `CHF-SRC-COMP`
- Proven first-bad-stage results from the summary:
  - `1546.600,-4435.900,11.500`
    - first bad stage: `finalDetour`
    - reason: `upper_support_lost`
    - interpretation: support survives through `polymesh`, then the final
      Detour winner drops to lower basin `0x1000000000BE35`
  - `1522.500,-4424.100,17.000`
    - first bad stage: `finalDetour`
    - reason: `upper_support_lost`
    - interpretation: support and lower competitor both survive through
      `polymesh`; final Detour winner still lands on lower basin
      `0x1000000000AD5D`
  - `1523.800,-4425.900,17.100`
    - first bad stage: `median`
    - reason: `lower_competitor_dominant`
    - interpretation: this anchor is already wrong before regions/contours; the
      lower basin becomes dominant at compact-heightfield median time and stays
      dominant into final winner `0x1000000000ADC6`
  - `1521.267,-4425.600,17.609`
    - first bad stage: `contours`
    - reason: `lower_competitor_dominant`
    - interpretation: support survives through `regions`, but contour
      generation is where the lower basin becomes the dominant surviving shape;
      final winner becomes `0x1000000000AD5D`
  - `1521.300,-4422.500,17.100`
    - first bad stage: `sourceSupport`
    - reason: `no_source_support_probe`
    - interpretation: the exact verified upper support was not found in the
      source-backed oracle for this coord, so future fixes must first prove the
      source support itself instead of tuning regions/contours blindly
- Important nuance:
  - some compact-stage records report `anchor_outside_compact_window`; this is
    not the same thing as `support already lost`. The manifest still records
    support/lower counts and component membership inside the analysis window,
    then the later contour/poly/final stages answer whether the support band
    actually survives as a usable winner.
- Practical next rule:
  - use the stage summary as the first gate for any new bake branch
  - if the summary still says `median`, `contours`, or `finalDetour` for the
    same anchor, do not resume generic knob churn; change the stage-local fix
    surface instead

### 2026-05-24 final Detour component follow-up

I extended the anchor manifest again, but only on the final Detour side: each
candidate now records `componentId`, `componentPolyCount`, and
`componentArea2D`, and the stage summary records how many support/lower final
components survive around the anchor.

Validated artifact:

- `tmp/bake-sweeps/og_4029_component_manifest_links-20260524T000728Z/`
- hash stayed
  `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

What this proved on the current hallway chain:

- `1518.200,-4419.800,17.100` still has two final support candidates
  (`0x1000000000AD7E`, `0x1000000000ADA1`) and still wins
  `0x1000000000ADA1`.
- `1520.600,-4426.500,17.900` still has seven final support candidates and
  still wins `0x1000000000AD6E`.
- `1523.800,-4425.900,17.100` is still the clean red:
  `finalDetour -> lower_competitor_dominant`, winner `0x1000000000ADAB`.

Direct runtime probes from those exact coords still fail:

- `1518.2,-4419.8,17.1 -> goal` only walks into the short hallway trap.
- `1520.6,-4426.5,17.9 -> goal` has no real route at all.
- `1523.8,-4425.9,17.1 -> goal` returns only a 2-corner local trap.
- `1491.4,-4417.3,23.3 -> goal` also dead-ends before escaping the hallway.

So the remaining hallway/hall-exit issue is not just one bad endpoint snap. It
is a chained final-Detour trapped-basin problem. The next useful move is a
reachability-aware/component-targeted cull or earlier contour/polymesh
prevention, not more support-band threshold tuning.

### 2026-05-24 routeability-aware finalDetour cull follow-up

I kept the finalDetour component metadata and extended it into a real
routeability proof surface:

- `anchorRouteTargetsWow` lets each anchor resolve a local escape target.
- the manifest summary now records:
  - `FinalWinnerRouteableToAnyTarget`
  - `FinalResolvedRouteTargetCount`
  - `FinalRouteableSupportCandidateCount`
  - `FinalRouteableSupportComponentCount`
- the optional experiment flag
  `postDetourCullAnchorTrappedComponents=true` disables a trapped local winner
  only when the same anchor window still contains another support component
  that can route to the configured target.

Validated experiment branches:

- `tmp/bake-sweeps/og_4029_anchor_routeability_cull-20260524T004027Z/`
  - saved tile hash:
    `B84D1CD2369E03721ECBDC83656EC4E700E546886CFF49C231F52F05CED086AF`
- `tmp/bake-sweeps/og_4029_anchor_routeability_chain_targets-20260524T005038Z/`
  - saved tile hash:
    `039BEDF73A2318B0D6559BDC0FB453D240875EDD08BA2319F56A0EA26D85EA94`

Validation commands:

```powershell
$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1' -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_routeability_chain_targets' -DataDir 'D:\wwow-bot\test-data'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_routeability_chain_targets_focused.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore --settings 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings' -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_anchor_routeability_chain_targets.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'
```

Observed results:

- focused OG slice stayed green `7/7`
- full raw-Detour `CriticalWalkLegs` stayed flat at `17/23`
- the six failing route labels did not change
- the underpass failure changed shape: it no longer dead-ended, but the route
  climbed toward the overhead ramp/ceiling within `1.6y` of the start before
  moving horizontally out

What the routeability proof actually taught us:

- city / hallway / hallway-exit anchors still resolved `0` routeable support
  components even with local chain targets:
  - `1545.000,-4434.500,11.100`
  - `1518.200,-4419.800,17.100`
  - `1491.400,-4417.300,23.300`
- the cull therefore had nothing actionable to remove on those chained trapped
  basins
- `1364.867,-4374.000,26.109` and `1355.600,-4522.300,33.100` did resolve
  routeable support components, which is why the underpass/exterior branch
  changed while the hallway chain did not

Practical conclusion:

- keep the routeability fields in the manifest and summary
- keep `postDetourCullAnchorTrappedComponents` disabled in the default tile
  config for now
- do not spend another loop only rewiring route targets
- the next productive fix surface is still earlier structural loss:
  `polymesh`/`contours` for the hallway-city chain, and lower-vs-overhead
  support disambiguation for the underpass
- checked-in proof-only validation:
  - `tmp/bake-sweeps/og_4029_anchor_routeability_proof_only-20260524T010701Z/`
  - saved tile hash:
    `6FA99D4CA18F7C3E8853712F7931DBD26A03C30C344508E15760E1E8CD459F52`
  - focused slice stayed `7/7`
  - full `CriticalWalkLegs` stayed `17/23`

### Follow-up: combined post-erode restore + source-support window cull

- Why this branch existed:
  - the first manifest proved `1523.800,-4425.900,17.100` already went bad at
    `median`, and the raw compact-stage data showed source-backed support
    collapsing from `80` support spans at `buildCHF` to `8` after `erode`
  - the restore-only probe confirmed support could be preserved better, but it
    did not move the full route score by itself because the lower basin still
    owned nearby no-support cells
- Branch config on tile `4029`:
  - `preRegionRestoreAnchorSourceSupportAfterErode=true`
  - `preRegionCullAnchorSourceSupportCompetingSpans=true`
  - `preRegionCullAnchorSourceSupportFallbackToWindow=true`
- Commands:

```powershell
$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1' -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_source_cull_window' -DataDir 'D:\wwow-bot\test-data'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_restore_source_cull_window_focused.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore --settings 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings' -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_restore_source_cull_window.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding' -- RunConfiguration.TestSessionTimeout=1200000
```

- Artifact bundle:
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/`
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/analysis/map0012940_anchor_stage_manifest.json`
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/analysis/map0012940_anchor_stage_summary.json`
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/analysis/map0012940_anchor_stage_summary.csv`
  - `tmp/test-runtime/results-pathfinding/og_4029_restore_source_cull_window_focused.trx`
  - `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_restore_source_cull_window.trx`
- Validation:
  - saved tile hash changed to:
    `29449D252853BF2E3B9739DC108BA0E4CE1E0F4C1152D7BADAE45032984945C5`
  - saved tile size:
    `8775316`
  - focused OG slice stayed green `7/7`
  - full raw-Detour `CriticalWalkLegs` stayed flat at `17/23`
- What materially improved in the stage summary:
  - `1522.500,-4424.100,17.000`
    - moved from `finalDetour` red to no `firstBadStage`
    - final winner `0x1000000000AD71` is no longer marked as a competing lower
      basin
  - `1521.267,-4425.600,17.609`
    - moved from `contours` red to no `firstBadStage`
    - final winner `0x1000000000AD6D` is no longer marked as a competing lower
      basin
  - `1523.800,-4425.900,17.100`
    - still first goes bad at `median`
    - but the lower competitor is gone by `regions`; the remaining failure is
      now that no final support-band Detour poly covers the exact query
      neighborhood
  - `1546.600,-4435.900,11.500`
    - still first goes bad at `finalDetour`
    - but the final winner `0x1000000000BED3` is no longer marked as a lower
      competitor; the defect has narrowed to `upper_support_lost`
  - `1521.300,-4422.500,17.100`
    - unchanged `sourceSupport`; later-stage tuning is still premature there
- Important operational conclusion:
  - this branch is proof-positive, not route-complete
  - the remaining red routes now die at shifted or later dead-end points, so
    the next step is to add those new dead-end coords to the anchor manifest
    and re-run the same stage-summary loop
  - do not interpret the flat `17/23` as "the source-backed cull did nothing";
    it clearly removed some wrong local basins, but the next blockers are now
    further along the corridor

### Manifest-only shifted dead-end probes

- New config/tooling:
  - `anchorStageManifestCoordsWow` adds analysis-only coords to the manifest
    without changing the actual compact-span / final-Detour cull list
  - this is intentionally separate from
    `postDetourCullAnchorPolyStacksCoordsWow`; the first implementation
    accidentally let analysis-only coords affect the source-support cull, and
    the follow-up fix explicitly separated those probe sets again
- Validation run:
  - bake dir:
    `tmp/bake-sweeps/og_4029_manifest_shifted_deadends_v2-20260523T221238Z/`
  - tile hash stayed on the combined source-cull branch:
    `29449D252853BF2E3B9739DC108BA0E4CE1E0F4C1152D7BADAE45032984945C5`
  - tile size stayed:
    `8775316`
- Added manifest-only probe coords and results:
  - `1537.300,-4437.900,13.000` -> no `firstBadStage`
  - `1520.600,-4426.500,17.900` -> no `firstBadStage`
  - `1355.600,-4522.300,33.100` -> no `firstBadStage`
- Meaning:
  - the current city, hallway, and underpass route stalls are no longer caused
    by those exact endpoint cells resolving onto the wrong local support layer
  - next diagnosis should look further along the corridor/connectivity surface,
    not blindly keep trimming the exact endpoint basin again
