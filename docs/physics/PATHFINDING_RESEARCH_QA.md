# PATHFINDING_RESEARCH_QA.md — BRM Phase 3 candidate surfaces

Research package answering three open Detour/Recast questions raised by the
Phase 2 closeout for the BRM ascent on tile 3446 (`0004634.mmtile`,
CLI `--tile 34,46`). Every technical claim is cited against the in-tree
Recast/Detour copy at
`tools/MmapGen/dep/recastnavigation/Detour/`.

## Context

Phase 2 of the BRM ascent overhaul shipped Surface H (per-tile
`cs=0.15 + tileSize=142` on tile 3546, commit `bba10488`), which closed the
Ruins-of-Thaurissan stall and walked the BRD/LBRS/BWL FG sub-tests
500-1100y further. One failure cluster remains: **tile 3446 / BRM upper
portal cluster**. UBRS still stalls at `brm_south_lo` (-7949.7, -1162.8,
170.8) and the `ubrs_portal` property test reports
`surfaceZ=null` at (-7524, -1233, 287) — `findNearestPoly` returns a poly
(`0x0001000014F02CDF`), but `getPolyHeight` on the same XY returns failure.
Surfaces F (`maxSimplificationError=1.2` on tile 3446) and G
(`walkableErosionRadius=0.0` on tile 3446) both regressed: the SmoothPath
A* query exploded from <100ms baseline to >9min (no terminating result),
the tile grew 20–23%, and every polyIdx in the upper cluster shifted,
which silently regressed two previously-green corridor-terminus gates.
Phase 2 closes with bake-knob tightening on tile 3446 declared
**ruled out** for the BWL/UBRS/LBRS portal-terminus failures (memo
`project_pfs_overhaul_brm_phase2_surface_g_negative.md`); Phase 3 needs a
structurally different surface.

The three questions below define what's known about the failure
mechanism inside Detour and bound the design space for the next attempt.

---

## Q1. Why `getPolyHeight` returns null when `findNearestPoly` succeeded

### Direct answer

`findNearestPoly` and `getPolyHeight` apply two different containment
tests against two different geometric sets:

1.  **`findNearestPoly`** asks "is there a polygon whose *base footprint
    plus detail edges* lies closest to `center`, within
    `halfExtents`?" When the query XY sits *outside* the polygon's
    convex base footprint but the polygon is the closest one in the BV
    grid, the function falls through to `closestPointOnDetailEdges`
    and reports the nearest XY on a detail-edge segment — `posOverPoly`
    is false and `nearestPt` is *on the polygon boundary*, not at the
    requested XY.

2.  **`getPolyHeight`** is strict: it returns true only if
    `dtPointInPolygon(pos, base_verts, nv)` succeeds, i.e. the requested
    XY lies inside the polygon's **base** convex footprint (the
    `dtPoly::verts` indices that point into `tile->verts`, not the
    detail-mesh sub-triangulation). If `dtPointInPolygon` returns
    false, `getPolyHeight` returns false immediately — no detail-edge
    fallback.

So the bug pattern is: the requested XY is just outside the polygon's
base footprint, `findNearestPoly` snaps to the nearest boundary point
on the polygon and reports success, but the queried XY itself has no
defined surface Z on that polygon. The recon-polyrefs.json data confirms
this exactly for `ubrs_portal`:

- `requested = (-7524, -1233, 287)`
- `nearestPoint = (-7523.715, -1232.9683, 285.838)` — the XY shifted by
  ~0.3y because the requested XY is **outside** poly `0x14F02CDF`'s
  base footprint; the reported point is the closest detail-edge
  vertex.
- `surfaceZ = null` — `getPolyHeight(requested)` failed
  `dtPointInPolygon` on the base verts.

The same shape appears for `fc_stall` and `bwl_portal`.

### Citations

- **`dtNavMesh::getPolyHeight` — strict `dtPointInPolygon` gate.** The
  base footprint test happens before any detail-mesh sub-triangle
  inspection:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:677-693`
    — function signature; builds `verts[]` from `tile->verts[poly->verts[i]*3]`;
    `if (!dtPointInPolygon(pos, verts, nv)) return false;`
- **`dtPointInPolygon` — pure XY pnpoly test on the base verts.** Y is
  ignored:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourCommon.cpp:236-252`
    — "All points are projected onto the xz-plane, so the y-values are
    ignored."
- **`dtClosestHeightPointTriangle` — only invoked AFTER `dtPointInPolygon`
  passes.** This is the actual height interpolation against
  detail-mesh sub-triangles, but it never runs if the base poly
  containment failed:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourCommon.cpp:204-233`
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:699-715`
- **`dtNavMesh::closestPointOnPoly` — the lenient counterpart used by
  findNearestPoly.** When `getPolyHeight` returns false, it sets
  `posOverPoly=false` and falls through to `closestPointOnDetailEdges`:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:728-758`
- **`closestPointOnDetailEdges<true>` — boundary-edge nearest-point
  fallback.** This is what produces the `(-7523.715, -1232.9683)` shifted
  nearestPoint in the recon JSON:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:619-674`
- **`dtFindNearestPolyQuery::process` — chooses winner by squared
  distance from queried `center` to `closestPtPoly`, NOT by polygon
  containment.** The `posOverPoly` flag controls the cost function
  (climb-tolerant for `posOverPoly=true`, full 3D distance otherwise),
  but both cases return a valid `nearestRef`:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:643-677`
- **`dtNavMeshQuery::getPolyHeight` — propagates the failure as
  `DT_FAILURE | DT_INVALID_PARAM`.** This is the actual API caller-visible
  result:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:594-624`
  — "Will return DT_FAILURE | DT_INVALID_PARAM if the provided position
  is outside the xz-bounds of the polygon."
- **`dtNavMeshQuery::findNearestPoly` — returns DT_SUCCESS even when
  every candidate has `posOverPoly=false`.** The only way to get
  `nearestRef=0` is for `queryPolygons` to return zero polygons in the
  search box at all:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:680-710`
- **`dtNavMeshQuery::queryPolygons` — pure AABB overlap test against
  the tile BV-tree, no point-in-polygon refinement.** That is, the
  candidate set is "every poly whose BV-AABB overlaps the query
  AABB":
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:896-933`

### What to try

- **Compare nearestPoint.xy vs requested.xy when interpreting
  `surfaceZ=null`.** When the deltas are sub-meter (0.3y for
  `ubrs_portal`, 0.7y for `fc_stall`), the issue is "polygon footprint
  doesn't extend the last fraction of a yard to cover the requested
  XY" — fixable by extending each polygon's footprint without
  fragmenting the graph. When the deltas are multi-yard, the issue is
  "no polygon at this XY at all" — needs a different surface (add a
  poly, off-mesh-link past it, or move the query coord).
- **Add an explicit `posOverPoly` field to the existing property
  test.** Surface this delta in the recon output so future surfaces
  can distinguish "polygon-edge clip" from "polygon-absent-entirely"
  without re-reading the recon-polyrefs JSON each time.
- **Snap consumer queries to `nearestPoint` instead of using the
  requested XY.** When `findNearestPoly` returns a polyRef but
  `getPolyHeight` fails on the requested XY, the consumer can re-call
  `getPolyHeight(polyRef, nearestPoint)`. By construction
  `nearestPoint` is *on* the polygon boundary so `dtPointInPolygon`
  succeeds (modulo float-precision edge cases handled by the
  detail-edge fallback at `DetourNavMesh.cpp:718-725`). This is a
  consumer-side mitigation for cases where the polygon-XY gap is
  unavoidable.

### What NOT to try

- **Don't widen `searchExtents`.** It won't help — `findNearestPoly`
  *already* found the right polygon. The problem is in the height
  computation on that polygon, not in the polygon search.
- **Don't lean on `closestPointOnPoly` for height.** Its height return
  is the *boundary* projection's Y (line 752 lerps between two boundary
  verts), not a surface height that respects the polygon's interior
  detail mesh. For a tilted polygon you can be metres off.
- **Don't increase `DT_VERTS_PER_POLYGON`.** The base footprint
  containment test runs on the base verts (capped at 6 in default Recast
  builds) regardless of the detail mesh density. Densifying the detail
  mesh (e.g. via `detailSampleDist`) helps height interpolation
  precision once `dtPointInPolygon` passes, but it doesn't make
  `dtPointInPolygon` pass.

---

## Q2. Why a small bake-knob change blows up A*

### Direct answer

A* runtime in `dtNavMeshQuery::findPath` is dominated by two things:

1.  The **g-cost** is computed along *edge midpoints* between
    consecutive polygons via `getEdgeMidPoint`, not along the
    straight-line geodesic. When tile 3446 was re-baked with
    `mse=1.2`, the polygon count along the FC→UBRS corridor grew
    287→355 (memo `surface_f_negative.md`). Every additional polygon
    along the corridor adds one *real* g-cost segment whose length is
    the inter-edge-midpoint hop, **not** the straight-line distance. The
    accumulated g-cost on the true corridor exceeds the heuristic's
    straight-line lower bound by an ever-growing margin as fragments
    multiply.

2.  Detour A* **DOES re-open closed nodes** when a better total is
    found (lines 1098-1099 evaluate `(neighbourNode->flags &
    DT_NODE_CLOSED) && total >= neighbourNode->total` — i.e. it skips
    only if the new total is *not* better; otherwise it falls through
    to update + re-push). With many small polygons forming a dense
    adjacency graph, the same closed node gets re-opened many times
    via alternate edge-midpoint approach paths whose first-discovery
    g-cost was sub-optimal. Each re-open invalidates downstream subtrees
    and triggers cascading re-expansions.

3.  The heuristic scale `H_SCALE = 0.999` makes `h(n) = 0.999 *
    dtVdist(n, endPos)` — an *underestimate* of straight-line
    distance, which is admissible iff every edge weight is at least
    the straight-line distance between its endpoints. The default cost
    function `dtVdist(pa, pb) * areaCost` with `areaCost=1.0`
    satisfies this on a *single* edge. But across a fragmented corridor
    where the bot's true polyline path is much longer than the
    straight-line distance to the goal, the heuristic is admissible but
    *very* loose — A* must explore the entire open list of dead-end
    fragments before pruning them, because each fragment still looks
    promising under `h = 0.999 * straight_line`.

The combined effect: a 25% polygon-count increase along the corridor
becomes a several-thousand-fold runtime increase because (a) more
fragments → more open-list candidates per expansion, (b) every fragment
near the geodesic looks equally promising under the loose admissible
heuristic, (c) closed nodes get re-opened by cheaper alternate
approaches, and (d) `m_nodePool->getNode` is hash-linked-list and gets
called once per `getNode` call per neighbour per expansion.

The node pool is sized at `init(maxNodes)` time via
`new dtNodeQueue(maxNodes)` and `new dtNodePool(maxNodes,
dtNextPow2(maxNodes/4))`. If the consumer (`PathfindingService`)
passed e.g. `maxNodes=2048`, then once the pool fills, `getNode` returns
NULL and `outOfNodes` is set — but the algorithm doesn't terminate; it
just stops adding *new* nodes, and the open list drains slowly through
a smaller working set than the corridor needs. With 2048 nodes and a
corridor that wants 4000+ unique polys (fragmented bake), this can look
like a hang because the algorithm is grinding through inadmissible
re-expansions of the existing 2048 nodes.

### Citations

- **A* heuristic scale and admissibility.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:103`
  defines `static const float H_SCALE = 0.999f;`. The heuristic is
  applied at `:1089` for non-goal neighbours: `heuristic =
  dtVdist(neighbourNode->pos, endPos) * H_SCALE;`. Start-node total at
  `:983`. Sliced variant at `:1415`. `H_SCALE < 1.0` means the
  heuristic is strictly less than the Euclidean lower bound — admissible
  iff every edge cost ≥ its Euclidean distance.
- **g-cost computed at edge midpoints, not straight line.** The
  neighbour's spatial position used by the cost function is set on
  first visit by `getEdgeMidPoint` at `:1056-1058` (`if
  (neighbourNode->flags == 0)` — first-visit branch). The cost is
  `filter->getCost(bestNode->pos, neighbourNode->pos, ...)` at
  `:1069-1072` — i.e. from one edge midpoint to the next.
- **Default cost function: Euclidean distance × per-area cost.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:79-85`
  / `:94-100` — `return dtVdist(pa, pb) * m_areaCost[curPoly->getArea()];`.
  Default `m_areaCost[*] = 1.0f` from `dtQueryFilter::dtQueryFilter()`
  at `:63-69`.
- **Closed-node re-expansion is permitted.** Lines `1097-1099`:
  ```
  // The node is already visited and process, and the new result is worse, skip.
  if ((neighbourNode->flags & DT_NODE_CLOSED) && total >= neighbourNode->total)
      continue;
  ```
  i.e. iff `total < neighbourNode->total`, the algorithm proceeds to
  re-open the node: `:1104` clears `DT_NODE_CLOSED`, `:1116` sets
  `DT_NODE_OPEN`, `:1117` pushes back onto the heap.
- **Node-pool and open-list sizing fixed at `init()` time.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp:166-218`
  — `init(maxNodes)`. `m_nodePool` allocated with `maxNodes` slots
  (`:181`); `m_openList` allocated at line `:209` with capacity
  `maxNodes`. If the consumer never re-calls `init` with a larger
  `maxNodes`, the pool is the hard cap.
- **`getNode` returns null when pool is full; algorithm sets
  `outOfNodes` and continues without terminating.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNode.cpp:121-152`
  — pool-full at `:133-134` (`if (m_nodeCount >= m_maxNodes) return 0;`).
  The main loop handles null at `DetourNavMeshQuery.cpp:1046-1051`
  (`if (!neighbourNode) { outOfNodes = true; continue; }`).
- **Node hash bucket count is `dtNextPow2(maxNodes/4)`.** Bucket count
  smaller than active node count means hash bucket chains lengthen and
  `findNode`/`getNode` becomes O(chain length) per call.
  `DetourNavMeshQuery.cpp:181` — `new dtNodePool(maxNodes,
  dtNextPow2(maxNodes/4))`. Hash function at
  `DetourNode.cpp:38-47` for 32-bit polyref, `:25-36` for 64-bit. Bucket
  walk at `:121-131`.
- **dtNode internal layout — 16-bit dtNodeIndex caps maxNodes at
  65535.** `tools/MmapGen/dep/recastnavigation/Detour/Include/DetourNode.h:31-34`
  — `typedef unsigned short dtNodeIndex;` and `DT_NODE_PARENT_BITS=24`.
- **Heuristic-best `lastBestNode` tie-breaking does NOT terminate
  search early.** Lines `1121-1125` — `lastBestNode` only updated for
  bookkeeping; the main `while (!m_openList->empty())` loop continues
  until either `bestNode->id == endRef` at `:1001` or open list drains.
  If the goal is unreachable through fragmented graph, the algorithm
  drains the entire connected component.

### What to try

- **Measure the failure mode, not just the symptom.** Before any
  Phase 3 surface targets tile 3446 again, instrument the
  PathfindingService A* path to log
  `m_openList->getCapacity()`, peak `m_nodePool->getNodeCount()`,
  `outOfNodes` flag, and total `bestPoly` iteration count. The >9min
  hang on F/G probably hit the pool cap and started thrashing
  re-opens. Distinguishing "infinite re-opens" from "pool exhausted"
  is necessary to size any countermeasure.
- **Raise `maxNodes` for known-fragmented queries** as an interim
  band-aid if pool exhaustion is confirmed — but only as a diagnostic
  step, not a fix. The fragmented graph is still wrong; this just
  shows whether more headroom would let the query complete (e.g. in
  60s instead of >9min).
- **Cap iterations explicitly.** The current API has no `maxIterations`
  parameter — but the PathfindingService wrapper can timestamp the
  query and abort externally. If F/G's >9min hangs are
  closed-node re-expansion thrashing, an external 30s cap turns a
  fatal hang into a benign `DT_PARTIAL_RESULT`.
- **Use the sliced-find-path variant** (`initSlicedFindPath` +
  `updateSlicedFindPath` at `:1240+`) with a per-update iteration
  budget. Same A* algorithm; lets the consumer abort cleanly between
  expansion batches.
- **Test whether `H_SCALE = 1.0` (or even slightly > 1.0, breaking
  admissibility deliberately) cuts runtime on the fragmented tile.**
  Weighted-A* with a non-admissible heuristic often runs many times
  faster at the cost of optimality bounds — and for a bot, a
  sub-optimal path that completes in 2s beats an optimal one that
  hangs forever. This is a *codebase change*, not a bake change. Test
  on a non-shipping branch first.

### What NOT to try

- **Don't densify tile 3446's polygon graph.** F and G both did, both
  blew up A*. Any further bake knob on tile 3446 that increases
  polygon count (mse, cs decrease, erosion change, walkable-slope
  change) will hit the same A* cliff.
- **Don't lean on `DT_FINDPATH_ANY_ANGLE`** as a fast-path. It's a
  *sliced*-find-path option (`DetourNavMesh.h:134-137`) and isn't
  honoured by the regular `findPath`. Even where supported it still
  walks the polygon graph; raycast shortcuts don't help when the
  *first* expansion across the fragmented region times out.
- **Don't expect H_SCALE to make A* admissible-and-fast on a
  fragmented graph.** The default 0.999 is admissible *and* loose;
  the looseness is exactly what makes fragmented-graph queries
  expensive. Tightening (e.g. 1.0) doesn't fix the underlying
  edge-midpoint g-cost vs straight-line h-cost gap.

---

## Q3. Off-mesh connections for BRM upper portals

### Direct answer

Off-mesh connections are the correct Detour primitive for "the polygon
graph has a real connectivity gap that the bot should be allowed to
cross via a non-walkable jump/portal", **if** the BRM portal cluster
satisfies all of the following:

1.  Both endpoints lie within a tile that has been baked (vertex A is
    required to be inside the navmesh; vertex B is not).
2.  The endpoints are within `con->rad` of a real ground polygon in
    their respective tiles. `connectExtOffMeshLinks` does a strict
    radius check (`dtSqr(nearestPt[0]-p[0]) + dtSqr(nearestPt[2]-p[2])
    > dtSqr(targetCon->rad)` → `continue`) and silently drops the
    connection if no qualifying polygon is found.
3.  The user accepts the bidirectional flag semantics: `DT_OFFMESH_CON_BIDIR
    = 1` makes the connection traversable both ways; absent that flag
    the connection is one-directional from A to B.

The BRM upper portal cluster (UBRS/LBRS/BWL/BRD) fits this pattern: the
recon-polyrefs.json data shows all four targets resolve to real ground
polygons (`0x14F02CDF`, `0x14F029D1`, `0x14F01B1B`, `0x14001E56`), and
the failure is that **corridor pathing** doesn't reach them — the
intra-tile-3446 graph has a connectivity gap the F/G negatives proved
isn't a bake fidelity problem.

The failure mode when `findNearestPoly` snaps to the wrong end is
silent: at bake time, `baseOffMeshLinks` (`DetourNavMesh.cpp:561-617`)
uses `findNearestPolyInTile` on `con->pos[0]` (vertex A); if the
returned polyRef is wrong (e.g. snapped onto the wrong ledge above the
intended ground), the off-mesh poly's `firstLink` points into the
wrong polygon. At query time, `findPath` traverses links normally —
the bot will be teleported / off-mesh-stepped from the wrong source
polygon, landing somewhere the path planner didn't intend. There's no
runtime validator for "the off-mesh start polygon is the one the
designer wanted" — only the `con->rad` distance check at bake time.

### Citations

- **`dtOffMeshConnection` struct.** Fields: 6-float endpoint pair, scalar
  radius, poly index (within the tile), link flags, side, and userId.
  - `tools/MmapGen/dep/recastnavigation/Detour/Include/DetourNavMesh.h:234-257`
- **Bidirectional flag.**
  `tools/MmapGen/dep/recastnavigation/Detour/Include/DetourNavMesh.h:103`
  — `static const unsigned int DT_OFFMESH_CON_BIDIR = 1;`
- **Off-mesh poly type marker.**
  `tools/MmapGen/dep/recastnavigation/Detour/Include/DetourNavMesh.h:155-162`
  — `DT_POLYTYPE_OFFMESH_CONNECTION = 1`. `getPolyHeight` short-circuits
  this type at `DetourNavMesh.cpp:681-682` ("Off-mesh connections do
  not have detail polys and getting height over them does not make
  sense.").
- **Vertex A inside the navmesh; vertex B optional.**
  `tools/MmapGen/dep/recastnavigation/Detour/Include/DetourNavMesh.h:790-794`
  doc on `dtOffMeshConnection::pos[6]`: "For a properly built navigation
  mesh, vertex A will always be within the bounds of the mesh. Vertex B
  is not required to be within the bounds of the mesh."
- **`baseOffMeshLinks` — wires vertex A.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:561-617`
  — calls `findNearestPolyInTile(tile, con->pos[0], halfExtents,
  nearestPt)`. The radius check at `:580-582`: `if (dtSqr(nearestPt[0]
  - p[0]) + dtSqr(nearestPt[2] - p[2]) > dtSqr(con->rad)) continue;`
  drops the connection silently. On success, `tile->verts[poly->verts[0]
  * 3]` is rewritten with `nearestPt` (`:584-585`) and a bidirectional
  link pair is established (`:587-615`).
- **`connectExtOffMeshLinks` — wires vertex B to a polygon in the
  target tile.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:454-522`
  — symmetric structure; uses `con->pos[3]` (vertex B); radius check at
  `:481-483`; bidirectional link only created if `targetCon->flags &
  DT_OFFMESH_CON_BIDIR` at `:503-519`.
- **`halfExtents = {rad, walkableClimb, rad}` for the polygon snap.**
  Both `baseOffMeshLinks` (`:573`) and `connectExtOffMeshLinks` (`:473`)
  use the connection's `rad` for horizontal extents and the tile's
  `walkableClimb` for vertical extent. This means a tall vertical gap
  (e.g. the bot is meant to climb up a wall) is *not* searched
  unbounded — if vertex B is more than `walkableClimb` above/below the
  nearest ground polygon's surface, the snap fails and the connection
  is dropped.
- **addTile wires off-mesh connections after building the polygon
  graph.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:1007-1042`
  — `connectIntLinks` first (`:1007`), then `baseOffMeshLinks`
  (`:1010`), then `connectExtOffMeshLinks(tile, tile, -1)` for
  intra-tile (`:1011`), then for each loaded neighbour both directions
  (`:1025-1028` for the SAME tile-coords layer, `:1037-1040` for the
  8 cardinal/diagonal neighbours). **A neighbour tile that isn't loaded
  when the source tile is added gets connected when IT is later added,
  via the reverse-direction calls.**
- **`DT_POLYTYPE_OFFMESH_CONNECTION` skipped in detail-edge fallback.**
  `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMesh.cpp:745-754`
  — `closestPointOnPoly` for an off-mesh poly returns the lerped
  segment point, not a detail edge.

### What to try

- **Place off-mesh connections only for the **inter-tile portal gaps**,
  not intra-tile.** The BRM upper portals span tile 3446 to neighbouring
  tiles. Off-mesh handles the cross-tile teleport cleanly.
- **Set radius generously but cap to walkableClimb's-worth.** A 5y
  radius gives `findNearestPoly` room to snap onto a deck polygon even
  with sub-meter polygon-footprint clip (Q1's issue) — but the
  vertical search is still capped at `walkableClimb`, so vertex A and
  vertex B Z's must be within that tile's `walkableClimb` of the
  intended ground polygon's surface.
- **Use `DT_OFFMESH_CON_BIDIR` for portal pairs that the bot
  legitimately traverses both ways** (e.g. zone-in/zone-out portals,
  which is what the BRM upper cluster is). For one-way drops (cliff
  jumps), omit the flag.
- **Verify the off-mesh wiring at bake time.** After `MmapGen` finishes
  a tile with off-mesh additions, log `con->poly` and its
  `tile->polys[con->poly].firstLink` — if `firstLink == DT_NULL_LINK`,
  the connection silently failed to bind (the radius check
  dropped it). The recon-polyrefs JSON format already prints `polyRef`
  and `nearestPoint` for ground polys; mirror that for off-mesh polys.
- **Test with a single portal first (e.g. UBRS) before deploying the
  full cluster.** Off-mesh-aware managed path consumption isn't part
  of the current `Services/PathfindingService/Repository/Navigation.cs`
  (see memory `project_pfs_overhaul_brm_surface4_offmesh_negative`):
  Surface 4 was bench-green but live FG regressed because the managed
  consumer wasn't off-mesh-aware. Phase 4 of the overhaul is the
  blocker.

### What NOT to try

- **Don't try to connect across more than `walkableClimb` of vertical
  gap.** The halfExtents cap is hard. If the gap is e.g. 12y and
  `walkableClimb=1.8y`, the snap will fail. Workaround: either move
  the off-mesh endpoint Z to within climb of an existing polygon, or
  add an intermediate "platform" polygon to the bake first.
- **Don't put both endpoints in the same connectivity component as a
  shortcut.** Off-mesh changes A*'s topology and can cause the
  pathfinder to take an off-mesh hop when a walkable route was
  preferable, especially if `m_areaCost` for the off-mesh poly is
  the default 1.0 (so off-mesh hops look as cheap as
  unit-length walking). If the off-mesh is meant to be a
  fallback-only path, set its area to a distinct ID and bump its
  `dtQueryFilter::setAreaCost` to a high value.
- **Don't rely on off-mesh to fix the **`surfaceZ=null` problem from
  Q1**.** Off-mesh adds an edge to the polygon graph; it doesn't fix
  the polygon footprint vs queried XY mismatch. A consumer querying
  `getPolyHeight` on the queried XY will still get failure. Off-mesh
  is the fix for "the path planner can't get there"; Q1 is the fix for
  "the path planner CAN get there but the surface-Z probe fails on
  arrival".

---

## Next attack surfaces (ranked)

### Surface I — Off-mesh connection for the BRM upper portal cluster
- **Hypothesis.** The intra-tile-3446 connectivity gap proved
  bake-knob-immune (F/G negative). The polygon graph has a real
  topological disconnect between the FC corridor and the
  UBRS/LBRS/BWL portal polys; the only way to close it without
  fragmenting the graph is to *add* an edge — an off-mesh connection.
- **Proposed change.** Author offmesh.txt entries for UBRS, LBRS,
  BWL, and (deferred) BRD using their `recon-polyrefs.json`
  coordinates as endpoints. Use `rad=5.0y` (enough headroom for
  posOverPoly=false snap), `flags=DT_OFFMESH_CON_BIDIR`, distinct
  `area=DT_AREA_PORTAL_OFFMESH` with `m_areaCost[portal] = 10.0` so
  walkable alternatives are preferred. Re-bake tile 3446 and the
  relevant neighbour tiles.
- **Expected outcome.** Property tests `Corridor_FcToUbrsPortal_*`,
  `Corridor_FcToLbrsPortal_*`, `Corridor_FcToBwlPortal_*` flip GREEN
  via the off-mesh edge. SmoothPath gates flip GREEN once the corridor
  includes the off-mesh poly. Live FG: UBRS/LBRS/BWL sub-tests
  complete or stall at a different (downstream) coord.
- **Risk of regression.** Medium-high. Memory
  `project_pfs_overhaul_brm_surface4_offmesh_negative` documents
  Surface 4 was bench-green but live FG regressed 4/4 because
  `Services/PathfindingService/Repository/Navigation.cs` isn't
  off-mesh-aware. **Dependency.** Phase 4 of the overhaul
  (off-mesh-aware managed path consumption) must land first, or
  Surface I will repeat Surface 4's negative.
- **Dependencies.** Phase 4 managed consumer (off-mesh-aware path
  navigation); offmesh.txt format compatibility with current MmapGen.
- **Status (2026-05-14, post Surface K).** SKIPPED. Dependency check
  on commit `7a2a87a8`: `Services/PathfindingService/Repository/
  Navigation.cs` has zero matches for `DT_POLYTYPE_OFFMESH_CONNECTION`
  / `OffMesh` / `off.?mesh`. The managed repair pipeline still
  capsule-validates every traversed polygon and would burn its budget
  on each off-mesh link, repeating the prior negative (memory
  `pfs-overhaul-brm-surface4-offmesh-negative`). Landing the managed
  consumer's off-mesh awareness is **Phase 5 of the overhaul**
  ("Managed repair retirement", target `Navigation.cs` < 500 LOC) —
  multi-cycle work, exit criteria for this iteration loop. Surface I
  cannot ship until Phase 5 lands. The native side
  (`Exports/Navigation/PathFinder.cpp:1746-1817`) already handles
  off-mesh polys in smooth-path generation, so when Phase 5 retires
  the repair pipeline Surface I should ship cleanly.

### Surface J — Consumer-side `nearestPoint` snap for `getPolyHeight`
- **Hypothesis.** The `ubrs_portal` surfaceZ=null isn't a bake bug; it's a
  consumer bug. The bot's height probe queries `getPolyHeight(polyRef,
  requested_xy)` when it should query `getPolyHeight(polyRef,
  nearestPoint_xy)` — the latter is guaranteed to succeed by
  construction (it's on the polygon boundary).
- **Proposed change.** In PathfindingService's height-probe wrappers,
  when `findNearestPoly` succeeds but `getPolyHeight(requested)`
  fails, retry with `getPolyHeight(polyRef, nearestPoint)` and log
  the XY delta. Promote `posOverPoly` from `findNearestPoly` into the
  diagnostic record.
- **Expected outcome.** `Walkable_AllFourPortals_HaveGroundPoly` flips
  GREEN deterministically (no bake change). No bake regression because
  no bake change. The 0.3y XY snap is below the bot's hit-radius so
  consumer behavior is unchanged for sub-meter snaps.
- **Risk of regression.** Low. Pure consumer-side change; no
  polygon-graph topology shifts. The risk: if some downstream
  consumer assumed `surfaceZ` is at the *requested* XY (e.g. for
  collision checks), they get the boundary-snapped Z instead.
- **Dependencies.** None.

### Surface K — Cap A* iterations + sliced-find-path migration
- **Hypothesis.** Even if Surfaces I/J flip the static property tests
  GREEN, future bake iterations or new portal additions will re-hit
  the F/G A* runtime cliff somewhere. The PathfindingService should
  not be vulnerable to bake mistakes hanging it for >9min.
- **Proposed change.** Migrate the synchronous `findPath` call path
  to `initSlicedFindPath` + `updateSlicedFindPath` with a per-update
  iteration budget (e.g. 2000 expansions) and an external 30s wall
  clock cap. On cap: return `DT_PARTIAL_RESULT` with the
  `lastBestNode` path and a `PathfindingFailureKind::A_STAR_TIMEOUT`
  enum value, surfaced to the bot decision engine as a recoverable
  failure (replan with alternate start/end, or use the partial path
  with a stall recovery).
- **Expected outcome.** F/G-style 9-minute hangs become 30s timeouts.
  Live FG sub-tests get a real failure mode that the
  PathfindingService can log/screenshot/report instead of hanging the
  whole test. Bench property tests run faster (the >9min hangs
  become 30s timeouts, surfacing as test failures rather than CI
  timeouts).
- **Risk of regression.** Medium. The sliced API has different
  semantics around node-pool reuse across slices and around the
  `m_query` state struct (cf. lines `:1240+` for the sliced
  variant's main loop and lines `:1395+` for its inner expansion).
  Tests that today pass within 100ms could theoretically take 200ms
  with sliced overhead. Mitigation: tune the per-slice budget.
- **Dependencies.** PathfindingService refactor.

### Surface L — Targeted off-mesh polygon-footprint extension via NavMeshTileEditor
- **Hypothesis.** Per memory `project_pfs_overhaul_006_brm_phase4_findings`,
  the `NavMeshPhysicsValidator + NavMeshTileEditor` pipeline lets us
  surgically cull individual polygons via `dtPoly::flags = 0` (which
  the default filter rejects). The inverse — surgically *extending*
  a polygon's footprint via `dtPoly::verts` mutation — is also
  possible at the same surgical layer.
- **Proposed change.** Identify the polygon whose footprint clips the
  `ubrs_portal` XY (poly `0x14F02CDF`, recon: requested
  XY=(-7524,-1233), nearestPoint XY=(-7523.715,-1232.9683)). Mutate
  its base verts at the offending edge to extend by 0.5y, validate
  the polygon remains convex, and re-write the tile via NavMeshTileEditor.
  No re-bake, no graph fragmentation.
- **Expected outcome.** `Walkable_AllFourPortals_HaveGroundPoly` flips
  GREEN for `ubrs_portal`. Other gates unchanged because polyIdx is
  preserved. No A* regression.
- **Risk of regression.** High *risk of subtle correctness bugs*
  (vertex mutation must preserve convexity; detail mesh sub-triangulation
  must still tessellate the new convex hull; BV-AABB must be
  recomputed). Lower than F/G *risk of perf regression* because no
  fragmentation. **Dependency.** NavMeshTileEditor needs an
  `ExtendPoly` operation (or per-vertex mutation) — currently only
  `Cull` is implemented per the memory.
- **Dependencies.** NavMeshTileEditor extension; detail-mesh
  re-tessellation logic.
- **Status (2026-05-14, post Surface J + K).** **MULTI-CYCLE BLOCKER.**
  Surface J already closed the property gate for the targeted poly
  (`Walkable_AllFourPortals_HaveGroundPoly` is 14/14 GREEN via
  consumer-side `nearestPoint` retry, commit `ebad865c`), so Surface
  L's expected outcome is already achieved by a much cheaper consumer-
  side change. Surface L remains valuable as a *bake-side* hardening
  defense (eliminates the need for consumer retry on the targeted
  poly) but no longer unblocks a live FG sub-test. The implementation
  cost stays at the original >1000 LOC estimate: ExtendPoly needs
  per-vertex mutation API, convexity validation, **detail-mesh sub-
  triangulation rebuild for the modified poly** (the in-tree
  Recast/Detour bake-time code path the bake doesn't expose at
  runtime), **BV-tree AABB recomputation** for the modified poly's
  enclosing branch, and atomic on-disk MMTILE rewrite with header
  bookkeeping. Per the loop's multi-cycle-blocker exit criterion,
  Surface L is deferred. Existing `Cull` mode in NavMeshTileEditor
  remains the only edit operation; future Surface L work should land
  alongside a regression suite that asserts dtPolyDetail tessellation
  integrity after each mutation. See iteration handoff memo
  `pfs-overhaul-brm-phase3-queue-exhausted`.

### Surface M — H_SCALE > 1.0 (weighted A*) for fragmented-bake tolerance
- **Hypothesis.** F/G hangs were dominated by closed-node re-expansion
  on a fragmented graph. A non-admissible heuristic (e.g.
  `H_SCALE = 1.5`) would prevent re-expansions and prune the open list
  aggressively, at the cost of optimality bounds. For bot pathing,
  optimality bounds are far less important than completion latency.
- **Proposed change.** Patch `DetourNavMeshQuery.cpp` to expose
  `H_SCALE` as a per-query parameter (or per-`dtNavMeshQuery`
  instance member). PathfindingService picks `1.0` for static map
  loadouts and `1.5+` for queries against tiles flagged
  "high-fragmentation" (or "previously timed out").
- **Expected outcome.** F/G-style fragmented baked tiles would
  return a sub-optimal path in <2s instead of hanging >9min. Tile
  3446 doesn't *need* this once Surfaces I/J/K land, but the codebase
  becomes resilient to future bake mistakes.
- **Risk of regression.** Medium-high. Path optimality changes;
  paths get longer/uglier on tiles where the optimal path is
  available. Bot screen-recording may show paths that look "wrong" to
  reviewers. Mitigation: A/B test on the existing 14-gate property
  suite and flag any geometric regression.
- **Dependencies.** Codebase patch to the in-tree Detour copy
  (cleanly upstreamable since Recast/Detour upstream is open to this
  kind of configurability).
- **Status (2026-05-14, post Surface K).** **DEPRIORITIZED — REDUNDANT
  WITH SURFACE K.** Surface K (commit `7a2a87a8`) added a
  `FindPathForAgentSliced` native export with a 30s wall-clock cap
  that converts F/G-class hangs into deterministic timeouts. The
  Surface M problem ("F/G fragmented tiles hang >9min") is already
  bounded to <30s by Surface K, so the incremental Surface M win is
  reduced to "F/G tiles return a sub-optimal completed path in <2s
  instead of timing out at 30s". That is a real but small gain, and
  it costs: changing `H_SCALE > 1.0` in the in-tree Detour copy
  shifts path optimality across the entire monorepo, including the
  WoW-classic and ARPG bot stacks that share this Detour. Without
  Docker live FG verification this iteration, the path-quality
  regression risk is unobserved and cannot be safely landed. Surface
  M should be revisited in a session with Docker live FG access AND
  after a real F/G timeout event in production (currently only an
  expected/hypothesized failure mode). For now, **deferred**.

---

## Ruled out

- **E** (cull `fc_stall` polyref 245 via NavMeshTileEditor on
  prod-data): NO-OP because prod-data's `fc_stall` polyrefs are
  different from the MaNGOS/data baseline used to write the cull
  range, and prod-data's FC→UBRS corridor doesn't include the
  fc_stall poly. Memo:
  `project_pfs_overhaul_brm_phase2_retry_prep`.
- **F** (`maxSimplificationError=1.2` on tile 3446): target gate
  flipped GREEN but corridor terminus regressed (polyIdx shift) and
  A* exploded >9min on SmoothPath. Memo:
  `project_pfs_overhaul_brm_phase2_surface_f_negative`.
- **G** (`walkableErosionRadius=0.0` on tile 3446): falsified the
  "erosion-only changes are polyIdx-stable" hypothesis. Same A* >9min
  cliff as F. Three previously-green gates regressed. Memo:
  `project_pfs_overhaul_brm_phase2_surface_g_negative`.
