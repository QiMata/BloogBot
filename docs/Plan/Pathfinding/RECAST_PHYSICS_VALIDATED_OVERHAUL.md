# Recast Physics-Validated Navmesh Overhaul — Design + Plan

> **Status:** Plan-of-record proposal, awaiting user sign-off.
> **Author:** synthesized 2026-05-30 from BloogBot pathfinding loops 17-26 + upstream Recast/WoW-emulator research.
> **Supersedes (when adopted):** the off-mesh-whack-a-mole tactic of loops 24-26; the `Services/PathfindingService/Repository/Navigation.cs` 5,600-LOC managed repair pipeline (deletion target).
> **Companion doc:** [PATHFINDING_OVERHAUL.md](../../physics/PATHFINDING_OVERHAUL.md) (the 2026-05-06 ADR/freeze contract — this doc is its Phase-2-through-5 concrete implementation).

---

## 0. The user's constraint in one sentence

Take upstream Recast, tune + extend it inside `tools/MmapGen` so that the `.mmtile` files it produces contain **only** polygons and edges that the WoW PhysicsEngine accepts — and ship a runtime that does a **thin Detour query, nothing else**.

No path repair. No corridor fallback. No `ShouldPreferAlternatePath`. No `dtNavMeshQuery::raycast` between waypoints. The mesh is authoritative; A* over it is the answer.

Baking can take hours. Runtime path generation has to be sub-millisecond and correct first try.

---

## 1. What the research established

### 1.1 The gap is real and structural — three causes stacked

The "bake-vs-physics gap" we have been patching for 10+ loops is caused by **three independent failure modes that compound**:

| Layer | Failure mode | Concrete example in this repo |
|---|---|---|
| **A. Source geometry extraction** | Vmap extractor misses WMO triangles with `MOPY.material_id = 0xFF` (collision-only invisible walls) and/or misses MOBN/MOBR BSP-only collision. | [TrinityCore #23972](https://github.com/TrinityCore/TrinityCore/issues/23972) cites Orgrimmar warchief building among the worst — and the OG zeppelin tower stalls have lived in this exact area. [AzerothCore PR #20822](https://github.com/azerothcore/azerothcore-wotlk/pull/20822) is the canonical extractor fix. **Likely affects our 4029 / 3928 stalls.** |
| **B. Recast parameter mistuning** | Cell size too coarse for capsule, `walkableClimb`/`walkableSlopeAngle` too lax for runtime physics, `detailSampleMaxError` too high. | Our defaults: `cs=0.2666`, `ch≈0.2666`, `walkableClimb=1.8`, `maxAngle=60`. Mononen's published rule for our `agentRadius=1.0247` (Tauren M) is `cs ≈ r/2 ≈ 0.51` outdoor or `r/3 ≈ 0.34` indoor — **we are using a *too-fine* cs globally and a *too-coarse* cs on tight tiles**. `ch=cs` violates Mononen's "ch=cs/2" rule. Documented in [Mononen 2009](http://digestingduck.blogspot.com/2009/08/recast-settings-uncovered.html). |
| **C. No bake-time physics validation** | Recast has no concept of the runtime capsule's actual collision behavior. It rasterizes geometry, filters by slope and clearance, simplifies contours, and writes the mesh. **It never asks "can the agent actually traverse this polygon's edges?"** That question is asked for the first time at runtime, in our PhysicsEngine, by which time the bot is already stuck on a wall. | Every iter-1, iter-2, loop-25 stall is this. Recast says walkable, PhysicsEngine says blocked, bot stalls 15s, test fails. |

We have been attacking layer (C) at runtime with `Navigation.cs`'s 5,600-line repair pipeline and at bake-time with off-mesh whack-a-mole. **The user's constraint rules both out.** Neither survives.

### 1.2 No off-the-shelf fix exists

External research (TrinityCore, AzerothCore, CMaNGOS, vmangos, namigator, AmeisenNavigation, recast-rs):

- **Upstream Recast 1.6.0** (May 2024) fixed tile-border rasterization, added `findNearestPoly` with `isOverPoly`, fixed off-mesh dedup, added OBB/AABB obstacles — none of which validate against an external physics engine.
- **TrinityCore / AzerothCore / CMaNGOS** all use the same conservative-bake-plus-area-type approach (CMaNGOS's `rcModAlmostUnwalkableTriangles` marking steep polys as `NAV_AREA_GROUND_STEEP` for mob-only traversal). None of them claim physics-valid output.
- **namigator** explicitly disclaims client-side movement-restriction honoring.
- **No published implementation** exists of a "post-bake capsule-sweep validation pass against source geometry." The research agent's verdict: *"it would be novel work."*

This is convenient: it means the user's constraint is achievable, but **only** by writing the validation pass ourselves. There is nothing to copy.

### 1.3 We have the rare prerequisite — a native PhysicsEngine

The asymmetry that makes this novel work tractable for us specifically: `Exports/Navigation/PhysicsEngine.cpp` (~6,762 LOC + extracted `PhysicsCollideSlide`, `PhysicsGroundSnap`, `PhysicsMovement` modules) is **already** a WoW-faithful capsule sweep / slope / step-up / fall implementation, callable from C++. `tools/MmapGen` is also C++. Linking the physics engine **into the bake** is engineering, not invention. None of the public WoW emulators have a comparable physics engine in their bake pipeline because they don't have a bot — they have a server that trusts the client.

---

## 2. The recommended solution — three layers, in dependency order

### Layer 1 — Tune Recast strictly to the PhysicsEngine's parameters

Stop guessing; derive every Recast knob from a single struct that holds the PhysicsEngine's measured behavior.

| PhysicsEngine constant | Source of truth | Drives Recast |
|---|---|---|
| Capsule radius (per race) | `Exports/Navigation/PhysicsTolerances.h` | `agentRadius`, then `walkableRadius_voxels = ceil(r / cs)` |
| Capsule height | same | `agentHeight`, then `walkableHeight_voxels = ceil(h / ch)` |
| Step-up height (per race) | same | `agentMaxClimb`, then `walkableClimb_voxels = floor(maxClimb / ch)` — **floor**, conservative |
| Max walkable slope cone | same | `walkableSlopeAngle` |

Then derive everything else by Mononen's rules:

- `cs = r/2` outdoor, `r/3` indoor (per-tile override on tight tiles)
- `ch = cs/2`
- `maxEdgeLen = walkableRadius_voxels * 8`
- `maxSimplificationError = 1.3` (Mononen-compliant; reject anything above 1.5)
- `detailSampleDist = cs * 6` (NOT `cs * 16`)
- `detailSampleMaxError = 0.5` (NOT `1.25`)
- `borderSize = walkableRadius_voxels + 3`
- `minRegionArea = 20`, `mergeRegionArea = 40` (TrinityCore defaults; sane)
- `partitionType = watershed` (NOT `layers` — repeatedly regressed per `_4029_NEGATIVE_RESULT_partition_layers_simplify13`)
- `vertsPerPoly = 6` (NOT 3 — our current 3 was a bake-quality probe; 6 is the documented sweet spot)

**Single C++ struct, one place** to change. No more per-tile cs/ch/tileSize overrides. The per-tile override mechanism (Cycle-16) becomes deprecated; tile-specific quirks move to Layer 3's validation pass + targeted per-tile geometry repair (Layer 2).

### Layer 2 — Fix vmap extraction at the source

Before tuning the navmesh, make sure the source geometry is correct. **Most "navmesh says walkable, physics says wall" cases are extractor bugs, not Recast bugs** ([TrinityCore #23972](https://github.com/TrinityCore/TrinityCore/issues/23972), [AzerothCore PR #20822](https://github.com/azerothcore/azerothcore-wotlk/pull/20822), [wowdev.wiki WMO](https://wowdev.wiki/WMO)).

Audit + fix our vmap extractor for:

1. **WMO `MOPY.material_id = 0xFF` triangles** — invisible collision-only walls. These create the classic "the navmesh thinks the room is open, but there's an invisible interior wall." Likely cause of the OG warchief / zeppelin tower stalls.
2. **WMO MOBN/MOBR BSP-only collision paths** — collision that lives in the BSP tree and not in the MOPY-flagged geometry. Different extraction code path; can be silently missed.
3. **Known-bad WMO blacklist** — vanilla WoW has documented broken WMOs (the wowdev wiki and TrinityCore #23972 list them). Maintain a per-WMO patch list at extraction time.
4. **M2 collision mesh** — confirm we use the M2 `collision_indices` / `collision_positions` / `collision_face_normals` block, NOT the render mesh. Some extractors use the render mesh, which produces over-conservative collision (lots of false walls).
5. **ADT MCNK holes** — confirm the holes bitmap is honored. Missed holes produce phantom walkable terrain over real falls.

Each fix is a discrete extraction-pipeline patch with a regression test (re-extract one known-bad WMO, diff vmap output before/after, verify a probe point that previously had no collision now does).

### Layer 3 — Bake-time physics validation pass (the novel piece)

After Recast has produced the Detour navmesh for a tile, run a **PhysicsEngine-driven validation pass** before writing the `.mmtile` file. This is the missing layer no public WoW emulator has.

**Algorithm:**

```
for each polygon P in dtTile:
    for each edge E in P:                  # the boundary edges, not internal triangles
        sample N points along E (N ~ edge_length / capsule_radius * 2)
        for each sample point S:
            cap_pos = S + (P.normal * step_up_clearance)
            result = PhysicsEngine.SweepCapsule(
                start = cap_pos,
                direction = E.tangent,
                length = capsule_radius,
                race = agent_race)
            record result.affordance (Walk / StepUp / SteepClimb / Blocked / ...)
        edge_affordance = worst_of(samples)

    poly_affordance = worst_of(edges)
    P.areaType = map_affordance_to_areaType(poly_affordance)

after all polys classified:
    # Connectivity pruning — a polygon only reachable through physics-rejected
    # edges is itself unreachable, even if its own edges validated.
    reachable_set = flood_fill(starting from NAV_AREA_GROUND_CONFIRMED polys,
                                 traversing only Walk/StepUp edges)
    for P not in reachable_set:
        P.areaType = NAV_AREA_PHYSICS_UNREACHABLE

    # Optional: repair pass — for polys with Blocked edges, try shrinking the
    # polygon inward by walkableRadius and re-validating. If the shrunk poly
    # is fully Walk-affordance, replace; else mark Blocked permanently.
    for P with P.areaType in {Blocked, SteepClimb}:
        P_shrunk = inset_polygon(P, walkableRadius)
        if P_shrunk.area > min_poly_area and all_edges_Walk(P_shrunk):
            replace P with P_shrunk
            P.areaType = NAV_AREA_GROUND_REPAIRED
        else:
            P.areaType = NAV_AREA_PHYSICS_BLOCKED   # culled at write time
```

**Output:** a Detour tile where every polygon's `areaType` is one of:

| Area | Meaning | Runtime cost |
|---|---|---|
| `NAV_AREA_GROUND_CONFIRMED` | All edges Walk-affordance; reachable from confirmed ground. | 1.0 |
| `NAV_AREA_GROUND_STEPUP` | At least one edge StepUp-clear; reachable. | 1.5 |
| `NAV_AREA_GROUND_REPAIRED` | Original poly had Blocked edge; inset poly validates clean. | 2.0 |
| `NAV_AREA_PHYSICS_UNREACHABLE` | Validated walkable but disconnected. Cull or keep as island for off-mesh use. | excluded by default `dtQueryFilter` |
| `NAV_AREA_PHYSICS_BLOCKED` | Failed validation, couldn't repair. **Removed from tile entirely.** | does not exist in output |
| `NAV_AREA_OFFMESH_LINK` | Authored off-mesh; bypasses validation. | 1.0 |

The runtime `dtQueryFilter` includes Confirmed/StepUp/Repaired/OffMesh and excludes Unreachable/Blocked. **A* over this filter cannot produce a physics-invalid path because the polygons it traverses have already been physics-validated.**

**Cost:** validation is O(polys × edges_per_poly × samples_per_edge × capsule_sweep_cost). For a typical 1k-poly WoW tile with ~6 edges/poly × ~3 samples/edge × ~10μs/sweep ≈ 180ms per tile. Across ~41 maps × ~785 tiles per map ≈ 1.5 hours total on a single thread, parallelizable to minutes. **Bake-time cost is fine; runtime cost is zero.**

**This eliminates the entire repair pipeline.** `Navigation.cs` can be deleted. `NavigationPath.ShouldPreferAlternatePath` can be deleted. The 88 `[TRAVEL_TRANSPORT]` chat traces from iter 2 become unnecessary — there is no failure to diagnose because the path can't be invalid.

### 2.1 Why this is achievable and the others were not

| Approach | Cost | Closes the gap? | Why we didn't / can't pick it |
|---|---|---|---|
| **Off-mesh whack-a-mole (loops 24-26)** | 1 iter per stall × N stalls × cross-tile regression risk | No — each stall surfaces the next; loop 26 hit regression on iter 2 | Diminishing returns; user constraint rules out |
| **Managed repair pipeline (Navigation.cs)** | 5,600 LOC, 8 repair phases, 20-min A* hangs | Partially — masks symptoms, doesn't fix root cause | Already being deprecated per the PATHFINDING_OVERHAUL ADR; user constraint rules out |
| **`NavigationPath.ShouldPreferAlternatePath` corridor fallback (current runtime)** | Modest, but conflates "physics rejected segment 0" with "smooth path bad" | Sometimes — iter 1's progress came from this | Runtime post-processing; user constraint rules out |
| **Recast 1.6 upgrade alone** | Vendor sync work | No — fixes some classes of bug but not bake-vs-physics by construction | Worth doing as Layer 1 dependency, but insufficient alone |
| **CMaNGOS-style area types (Layer 3 partial)** | Moderate — replicate `rcModAlmostUnwalkableTriangles` | Marginally — uses slope angle as proxy for physics; still doesn't validate capsule sweep | Strict subset of what Layer 3 proposes; we'd need to write Layer 3 anyway |
| **Layer 1 + Layer 2 + Layer 3 (this proposal)** | Sizable bake-pipeline work; runtime becomes thin | **Yes by construction** | Recommended |

---

## 3. Executable phased plan

Each phase ends with a green test suite and a commit/push (per R15). Phases are sized for a single dedicated session unless noted. The "Exit criteria" column is the gate to advance.

### Phase 0 — Establish ground truth (1 session)

Before changing anything, build the validation harness that will measure success.

**Tasks:**

1. Build a **Physics-Validation Probe** — a standalone tool (`tools/PhysicsValidationProbe/`) that takes a `.mmtile` file + map ID and runs the Layer-3 algorithm in measurement mode (does not write a new tile; reports affordance distribution).
2. Run the probe across all current `.mmtile` files; produce a **baseline report** — per-tile counts of `Walk / StepUp / SteepClimb / Blocked` polygons.
3. Identify the **top 20 worst tiles** (highest Blocked-edge ratio). These will be our regression-test bellwethers.
4. Write a **comprehensive baseline manifest** of all currently-failing live tests with their exact failure modes captured (we have this for the OG zep + Crossroads tests; add the others).

**Exit criteria:** baseline probe runs on all tiles; report committed; the iter-2 FINDINGS doc is the regression baseline for the chosen lever set.

### Phase 1 — Layer 1: parameter overhaul (1-2 sessions)

**Tasks:**

1. Create `tools/MmapGen/include/BakeProfile.h` — single struct holding all bake params, derived from a single `AgentProfile` (race-keyed PhysicsEngine constants).
2. Replace all `JsonFloatOrDefault` calls in `TileWorker.cpp` with `bakeProfile.cs`, `.ch`, etc.
3. Set defaults per Mononen rules: `cs=r/2`, `ch=cs/2`, `walkableClimb=floor(physics_step_up/ch)`, `walkableSlopeAngle=physics_max_slope`, `maxSimplificationError=1.3`, `detailSampleDist=cs*6`, `detailSampleMaxError=0.5`, `borderSize=walkableRadius_voxels+3`, `partitionType=watershed`, `vertsPerPoly=6`.
4. Delete all per-tile cs/ch/tileSize/maxSimplificationError overrides from `config.json` (Layer 3 makes them obsolete; keep `_NEGATIVE_RESULT_*` comment blocks as institutional memory).
5. Rebake all tiles. Run Phase 0 probe; expect Blocked-poly count to drop ~30-50% just from parameter tightening.
6. Run the bake-fixture pair (OG zep + BRM). **They will likely fail** because the route packs are calibrated against the current bake. Expect to regenerate route packs in Phase 4. For Phase 1, accept failure here; gate on Phase 0 probe improvement.

**Exit criteria:** Phase 0 baseline-vs-Phase-1 probe diff shows: (a) Blocked-poly count down ≥30% globally; (b) no new "white whale" tiles (a tile that was 90% Walk before but 50% Walk after); (c) total tile size growth ≤2× (cost of finer cs).

### Phase 2 — Recast vendor upgrade (1 session)

**Tasks:**

1. Diff our current `tools/MmapGen/dep/recastnavigation/` against upstream `recastnavigation/recastnavigation` v1.6.0.
2. Replace verbatim with v1.6.0. Preserve our build glue (CMakeLists, generator.cpp). Resolve API breakage (the `findNearestPoly`/`isOverPoly` API changed; we don't use it from MmapGen but `Exports/Navigation` does).
3. Verify `Exports/Navigation` still builds and links against the new Detour ABI. `DT_NAVMESH_VERSION` may change — update consumer.
4. Re-run Phase 0 probe. Expect tile-border ghost-ledge counts to drop (1.6.0 fixed border rasterization).
5. Re-run bake-fixture pair. Tile-size delta should be small; affordance distribution similar.

**Exit criteria:** native + managed both build clean; Phase 0 probe shows tile-border `Blocked` polygons (those within `borderSize` of tile edge) drop ≥50%; no regression in non-border polys; runtime path queries still succeed.

### Phase 3 — Layer 2: vmap extraction fixes (1-2 sessions)

**Tasks:**

1. Locate the vmap extractor in this repo. If we're reusing CMaNGOS's `vmap_extractor`, fork it into `tools/VmapExtract/` for in-tree control.
2. Apply [AzerothCore PR #20822](https://github.com/azerothcore/azerothcore-wotlk/pull/20822)-equivalent fix for WMO `material_id = 0xFF` collision-only triangles.
3. Audit MOBN/MOBR BSP traversal path. Add a unit test: extract `Orczeppelinhouse.wmo` (and one other known-broken WMO), assert collision-triangle count matches manual reference.
4. Build a **WMO blacklist patch system** — JSON-driven per-WMO triangle overrides. Seed with the [TrinityCore #23972](https://github.com/TrinityCore/TrinityCore/issues/23972) list (Orgrimmar warchief building, Krakenstatue, etc.).
5. Re-extract vmaps for affected maps. Re-bake those tiles.
6. Run Phase 0 probe. Expect specific stall locations (1608.1,-4382.3,10.0 — the iter-2 sub-floor pocket; the 1615.3,-4240.85 doodad-wall) to show improved Blocked-edge classification — the wall that was missing in vmap should now be present, so Recast filters that polygon out instead of producing it as walkable.

**Exit criteria:** the three named stall coords (iter-1 east-wall, iter-2 OG-interior, loop-25 doodad-wall) show in the Phase 0 probe as either (a) no polygon present at all (extractor wall now blocks Recast from generating one), or (b) polygon present with `Blocked` edges (caught by Layer 3 in Phase 4).

### Phase 4 — Layer 3: bake-time physics validation pass (2-3 sessions, the headline work)

**Tasks:**

1. **Link PhysicsEngine into MmapGen.** This is the engineering crux. `tools/MmapGen/CMakeLists.txt` adds dependency on `Exports/Navigation`'s physics modules (`PhysicsCollideSlide`, `PhysicsGroundSnap`, `PhysicsMovement`, `SceneQuery`). Resolve any include cycles. The MmapGen build picks up VMap loading from the same source the runtime engine uses.
2. Implement `PhysicsValidationPass` in `tools/MmapGen/contrib/mmap/src/PhysicsValidationPass.{h,cpp}`. API: `void PhysicsValidationPass::Run(dtNavMesh* mesh, const AgentProfile& agent, const PhysicsValidationConfig& cfg)`.
3. Algorithm exactly as in §2 Layer 3 above. Sample edges, sweep capsule, classify polygons.
4. Implement the **connectivity prune** (flood-fill from `NAV_AREA_GROUND_CONFIRMED`; mark unreached as `NAV_AREA_PHYSICS_UNREACHABLE`).
5. Implement the **repair pass** (inset polygon by `walkableRadius`; re-validate; replace if clean).
6. Wire into `TileWorker.cpp` after Detour navmesh build and before `dtCreateNavMeshData` write. Add `--skip-physics-validation` CLI flag for emergency bypass.
7. Define new `NAV_AREA_*` constants in `Exports/Navigation/NavAreaTypes.h` so runtime `dtQueryFilter` can include/exclude.
8. Add per-tile validation report writing (counts of each area type, time spent, top-N worst polygons by sample count). Goes to `tmp/bake-sweeps/<variant>/validation/<tile>.json`.
9. Re-bake all tiles. Validate execution time meets budget (≤30 min on 8 cores for full re-bake).
10. Run Phase 0 probe. Expect Blocked-poly count ≈0 in output `.mmtile` (everything Blocked got pruned or repaired before write).

**Exit criteria:**
- All 41 maps re-bake successfully.
- Total bake time ≤4 hours on a single dev box (acceptable per user's "we can take our time").
- Phase 0 probe on output: `Blocked` poly count = 0. `Walk` ≥ 60% of polys, `StepUp` 20-30%, `Repaired` ≤5%, `Unreachable` ≤5%.
- The OG zep + BRM bake-fixture pair pass.
- The 4 failing live tests (CrossroadsToUndercity, OrgrimmarToUndercity, OgZeppelin BakeVal, BrmDungeon BakeVal) — **manually run** to capture pass/fail. Each may need route-pack regeneration in Phase 5 to fully pass, but the static probe at each test's stall coord should show no `Blocked` polys.

### Phase 5 — Runtime simplification (1-2 sessions)

With bake-time validation in place, the runtime no longer needs a repair pipeline.

**Tasks:**

1. Update `Exports/Navigation/PathFinder.cpp` to use a `dtQueryFilter` that includes `NAV_AREA_GROUND_CONFIRMED`, `NAV_AREA_GROUND_STEPUP`, `NAV_AREA_GROUND_REPAIRED`, `NAV_AREA_OFFMESH_LINK` and excludes `NAV_AREA_PHYSICS_UNREACHABLE`. With proper per-area costs (Confirmed=1.0, StepUp=1.5, Repaired=2.0), A* naturally prefers Confirmed routes.
2. **Delete `Services/PathfindingService/Repository/Navigation.cs`'s 5,600-LOC repair pipeline** (per R18 — full deletion, no `[Obsolete]` markers). `Navigation.FindPath()` becomes a ~50-line thin wrapper over `dtNavMeshQuery::findPath` + `findStraightPath`.
3. Delete `NavigationPath.ShouldPreferAlternatePath`, `IsRouteSupported`, the corridor-fallback machinery in `Exports/BotRunner/Movement/NavigationPath.cs`. Replace with: if `findPath` returns a route, use it; if it doesn't, fail. No second-tries, no smooth-vs-raw choice.
4. Delete the runtime `SnapshotStallGuard` collision-creep detector (no longer needed; physics-valid paths don't creep into walls).
5. Delete the route-pack cache (`StaticRoutePackCache.cs`) — the bake is fast enough at query time that pre-baked routes are unnecessary; if perf measurements disagree, keep, but expect to delete.
6. Delete the dynamic-overlay machinery (`PathfindingOverlayBuilder.cs`) UNLESS it's needed for transports/elevators — research that separately.
7. Run the full integration test suite. The 4 long-pathing tests should pass. The bake-fixture pair should pass. The synthetic Navigation tests should still pass.

**Exit criteria:**
- `git diff --stat HEAD~1` shows net deletion ≥5,000 LOC.
- All 4 long-pathing live tests pass.
- All bake-fixture validation tests pass.
- p50 path query latency ≤5ms; p99 ≤50ms (per PATHFINDING_OVERHAUL Phase 6 target).
- No `Navigation.cs` repair code remains.

### Phase 6 — Validation coverage expansion (1 session)

Now that the bake is the only source of truth, expand the validation harness so regression detection is bulletproof.

**Tasks:**

1. **Grid-sweep validator** — for each map, sample 10,000 random walkable points; for each, sample 10 random walkable destinations within 200y; assert `findPath` succeeds AND the resulting path's `PhysicsValidationPass` re-validates clean. Run nightly.
2. **Per-stall regression tests** — add `LongPathingTests` entries for each historically-known stall coord (iter-1 east-wall, iter-2 OG-interior, loop-25 doodad-wall, OG zep deck-lip Cycle-17e). Each asserts the bot completes a path through the stall coord without collision-creep.
3. **Cross-tile seam validator** — sample 100 random points within `borderSize` of each tile edge; assert `findPath` to a point on the adjacent tile succeeds. Catches seam-ghost-ledge regressions.
4. **Bake-time regression CI** — bake a single tile (40,29) on every PR that touches `tools/MmapGen`. Assert byte-identical output across commits unless `bake-version-bump` label is set.

**Exit criteria:** grid-sweep validator passes on all 41 maps (or documents the small set of expected-unreachable points like Hyjal that we don't care about); per-stall regression tests added; CI bake check wired up.

---

## 4. Risks and how we manage them

| Risk | Likelihood | Mitigation |
|---|---|---|
| **Bake time explodes** to days, not hours | Medium | Profile Phase-4 early. If a tile takes >10 min, sample fewer edge points or parallelize the validation pass within a tile. Acceptable upper bound: 8 hours single-threaded full bake. |
| **Physics-validated mesh is too conservative** — agent can no longer reach legitimate places | Medium | The repair pass (Layer-3 inset) recovers most false-Blocked polys. The fallback is per-area-type cost tuning + a small authored off-mesh whitelist (e.g., for legitimate jumps the physics models don't predict). |
| **PhysicsEngine link-into-MmapGen surfaces include cycles or thread-unsafety** | Medium-Low | The physics modules already separate `PhysicsCollideSlide` etc. from the main monolith; this is the easier link target. Worst case: extract just the capsule-sweep API into a new `PhysicsSweep` static lib that both `Exports/Navigation` and `tools/MmapGen` depend on. |
| **Recast 1.6 upgrade breaks tile ABI** | Low | `DT_NAVMESH_VERSION` is a single header; both consumers rebuild from the same vendor source. The Detour binary format is documented and we control both ends. |
| **Vmap extractor fixes break existing baked tiles** | High | This IS the point — bad bakes get replaced. Phase 0 baseline + tile-by-tile diffs catch regressions; the Phase-6 grid-sweep validator gates promotion to prod. |
| **The 4 failing live tests don't all pass even after Phase 5** | Medium | The OG-UC zep test is already confirmed non-pathfinding (iter-2 finding — vmangos transport schedule). Test infra fix needed independently; not blocked by this overhaul. The other 3 should pass; if one doesn't, Phase 4's per-tile validation report will pinpoint which polygon is the new culprit. |
| **Runtime perf regresses** | Low | The new runtime is strictly less work than the old (no repair pipeline). Validation is at bake time only. If A* over the new mesh is slower because we have more area types, tune the cost function. |
| **Deleting Navigation.cs reveals hidden runtime consumers we didn't audit** | Medium | Phase 5 is intentionally last. Run the full test suite + grep for `Navigation.` method names before deletion. Use `[Obsolete]` markers for one Phase-5 commit, then a follow-up commit removes once green. Per R18 these don't ship — they're scaffolding during the cutover only. |
| **The novel Layer-3 algorithm has correctness bugs** | High initially | Phase 0's probe tool is the test bed; the algorithm runs in "measurement mode" before "write mode." Hand-validate on the 3 known stall coords before any global rebake. |

---

## 5. Acceptance criteria — overall

This overhaul is complete when **all** of the following are true:

1. `tools/MmapGen` produces `.mmtile` files where every polygon has a non-`Blocked` area type, every reachable polygon connects to `NAV_AREA_GROUND_CONFIRMED` through valid edges, and the file format is consumed by `Exports/Navigation` without change.
2. `Services/PathfindingService/Repository/Navigation.cs` contains no repair-pipeline code — only a thin Detour wrapper.
3. `Exports/BotRunner/Movement/NavigationPath.cs` consumes Detour results directly without `ShouldPreferAlternatePath` / corridor-fallback logic.
4. All 4 `LongPathingTests` pass (CrossroadsToUndercity, OrgrimmarToUndercity, OgZeppelin BakeVal, BrmDungeon BakeVal). The OG-UC zep test exemption (non-pathfinding) is acceptable if the iter-2 FINDINGS recommendation (extend dock-wait timeout to 540s) is also shipped.
5. p50 path query latency ≤5ms; p99 ≤50ms on a 16-core dev box.
6. The Phase-6 grid-sweep validator passes on all 41 maps.
7. Net code deletion ≥5,000 LOC (mostly `Navigation.cs`).
8. The 2 active off-mesh entries in `tools/MmapGen/offmesh.txt` can be removed — the bake-time validation eliminates the need for them — and the bake-fixture pair still passes.
9. The `_NEGATIVE_RESULT_*` config blocks in `tools/MmapGen/config.json` can be deleted — no more per-tile heroic tuning.
10. The runtime SnapshotStallGuard collision-creep detector is deleted from `LongPathingTests.cs` — no longer needed because physics-valid paths don't creep.

---

## 6. What this proposal does NOT do

- **Does not solve the OrgrimmarToUndercity transport-detection failure** (iter-2 Failure B). That's a test-infrastructure fix (extend `OrgrimmarUndercityZeppelinDockWaitSeconds` 120→540), independent of pathfinding.
- **Does not add new bot capabilities** — no new task types, no new behavior-tree work. This is strictly a bake/runtime pathfinding rebuild.
- **Does not address PathfindingService scale** to 3000 concurrent bots (PATHFINDING_OVERHAUL Phase 6). That's a separate native-rewrite project. This overhaul makes that project easier (no managed repair to port) but doesn't deliver it.
- **Does not modify the PhysicsEngine.** The physics engine is the ground truth we are validating against; we use it as-is. If it has bugs, they propagate into the bake — but the bake then matches the runtime exactly, which is what the user asked for.

---

## 7. Ordering note for the implementer

Phases 1, 2, 3 are mostly independent and can be done in any order. Phase 4 depends on all three. Phase 5 depends on Phase 4. Phase 6 depends on Phase 5.

If picking only one to do first: **Phase 0**. The baseline measurement is what proves every later phase actually improved things. Without it, we're back to off-mesh whack-a-mole — local wins, no global accounting.

If picking only one architectural change to deliver: **Phase 4**. It's the novel work, it's what fulfills the user's constraint, and Phases 1-3 + 5-6 are all in service of making Phase 4 possible and provable.

---

*End of proposal. The next-session prompt that kicks this off lives in [`NEXT_SESSION_RECAST_PHYSICS_OVERHAUL_KICKOFF.md`](NEXT_SESSION_RECAST_PHYSICS_OVERHAUL_KICKOFF.md).*
