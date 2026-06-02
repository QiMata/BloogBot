# Spec 06 — Pathfinding and Scene Data

## Two services, one data source

```
+-------------------+   GetPath / RoutePack   +-----------------------+
|     BotRunner     | <---------------------> |  PathfindingService   |
|                   |    PathBatchEstimate    |  (Detour / MmapGen)    |
+-------------------+                         +---+-------------------+
                                                   | scene queries (cold cache)
                                                   v
                                              +---------------------+
                                              |  SceneDataService    |
                                              |   (collision tiles)  |
                                              +---------------------+
```

Both services are stateless across requests but warm cache on startup.

## PathfindingService

### Contract

```protobuf
message PathRequest {
  uint32 map_id = 1;
  Position start = 2;
  Position end = 3;
  CapsuleSpec capsule = 4;    // agent dims
  RoutePolicy policy = 5;     // Smooth | Strict | RouteByRopack
  uint64 request_id = 6;
  string requester = 7;       // account name
  bool allow_route_pack = 8;
  uint32 deadline_ms = 9;
}

message PathResponse {
  uint64 request_id = 1;
  PathStatus status = 2;     // Ok | NoPath | Timeout | RoutePackBypass
  repeated Position waypoints = 3;
  RoutePackInfo route_pack = 4;       // hit info, signature
  Affordances affordances = 5;        // climb/drop/jump per segment
  string reason = 6;                   // from ErrorTaxonomy on failure
}
```

Existing surface lives at `Services/PathfindingService/` (port 9002).
The Detour native layer ships as `Navigation.dll`.

### Required capabilities

- **Generated route-pack cache** for static high-traffic legs
  (Crossroads→Undercity, Org flightmaster→zeppelin, etc.). Route packs
  are produced from the same Detour/MmapGen pipeline as live requests,
  validated offline, and stored with a nav-data signature.
- **Route-pack prewarm** from the catalog hot paths at startup.
- **Request de-duplication** for identical in-flight requests
  (same map/capsule/start/end → coalesce).
- **Cancellation and deadline propagation** — bots that time out
  cancel; the native layer aborts.
- **Per-map nav-data signatures** (`map_id × tile_set_hash`).
- **Dynamic-overlay compatibility checks** — route packs invalidate
  when a relevant gameobject overlay changes.
- **Path result cache** keyed by `(map, capsule, policy, start_proj,
  end_proj, overlay_sig)`.
- **Batch estimates** for ActivityScheduler candidate scoring
  (`PathBatchEstimate(requests[])` returns ETAs without full paths).

### Route-pack manifest

```text
RoutePack {
  ManifestKey {
    MapId,
    StartAnchor,        // canonical position (binned)
    EndAnchor,
    Race, Gender,
    Capsule { Radius, Height, Climb },
    RoutePolicy,        // Smooth | Strict
    SchemaVersion,
  },
  NavDataSignature {
    MmapDataRoot,
    TileHashes[],
    GoBakeVersion,
    NavigationDllVersion,
    BuildTime,
  },
  Payload {
    DetourCorners[],
    SanitizedCorners[],
    AffordanceSummary,
    PathSupportedFlag,
    FinalZEvidence,
    BlockerClearanceDiagnostics,
  },
  RuntimeGuard {
    AnchorRadii,
    AllowedCorridorProjectionDistance,
    MaxSegmentLength,
    MaxZDrift,
    AllowDynamicOverlays,
  }
}
```

Route packs are not hand-authored waypoint scripts. They are cached
Detour outputs with provenance.

### Pathfinding freeze (still active)

The pathfinding-overhaul freeze remains in effect:

- **No new repair phases** in `Services/PathfindingService/Repository/Navigation.cs`.
- **No new route-specific scripts** in BotRunner.
- **No per-spot LongPathingRouteTests.** Tests assert against generated
  route packs and live navmesh data.
- **No bot-side jump-up for regular pathing.** Mesh fidelity is the fix.
- **Fix at the mesh** via `tools/MmapGen/` per-tile config.

See [`Plan/09_PARALLEL_BRM_BAKE.md`](../Plan/09_PARALLEL_BRM_BAKE.md) for
the remaining bake-fidelity work.

### Scale targets

| Concurrent requests | Latency P99 | Native CPU |
|---|---|---|
| 50 | < 500 ms | 1 core |
| 200 | < 1 s | 2 cores |
| 500 | < 2 s | 4 cores |
| 1500+ | < 3 s | 4 cores × replicas |

Sharding: `hash(accountName) % replicaCount`. Clients see one logical
endpoint via a thin coordinator.

## SceneDataService

### Contract

```protobuf
message SceneSliceRequest {
  uint32 map_id = 1;
  Position center = 2;
  uint32 radius_tiles = 3;
  uint64 request_id = 4;
}

message SceneSliceResponse {
  uint64 request_id = 1;
  repeated SceneTile tiles = 2;
  string scene_signature = 3;
}

message GroundZRequest {
  uint32 map_id = 1;
  Position xy = 2;
  bool walkable_only = 3;       // SceneCache::GetWalkableGroundZ
  float max_search_height = 4;
  uint64 request_id = 5;
}
```

Existing surface at `Services/SceneDataService/` (port 9003).
`GetWalkableGroundZ` was added 2026-05-10 in commit `1c530288` to fix
the OG cliff-fall parity break.

### Required capabilities

- **Local 3×3 grid slice** queries (collision data for BG physics).
- **Map transform helpers** (world ↔ tile, tile ↔ world).
- **BG physics data** (StepPhysicsV2 inputs).
- **Cache policy** — bounded memory, per-map LRU eviction.
- **Protobuf API** for nearby world data only — bots never load mmap/
  vmap/scene tiles directly.

## R13 — validation order

For any game in this monorepo, the validation order is:

1. **Scene data flow** — SceneDataService delivers triangles at the
   queried coord.
2. **FG/BG physics parity** — both runtimes report the same ground Z and
   walkable state.
3. **Pathfinding** — Detour produces a route that both runtimes can
   execute.

The validation harness's `FG_BG_PARITY_BREAK` Kind is the canary. If it
fires on a checkpoint where SceneData delivers triangles, the bug is in
**physics**, not pathfinding. Do not iterate on pathfinding before
parity is green at representative checkpoints. See
[`Spec/07_PHYSICS.md`](07_PHYSICS.md).

## Existing code anchors

| Concept | File |
|---|---|
| PathfindingService | `Services/PathfindingService/` |
| SceneDataService | `Services/SceneDataService/` |
| Navigation native | `Exports/Navigation/` |
| MmapGen tool | `tools/MmapGen/` |
| Physics validator | `tools/NavMeshPhysicsValidator/` |
| Tile editor | `tools/NavMeshTileEditor/` |
| Path physics probe | `tools/PathPhysicsProbe/` |
| Scene cache builder | `tools/SceneCacheBuilder/` |
| MMAP visualizer | `tools/MmapVisualize/` |
| Route-pack contract design | [`docs/Reference/TRAVEL_PLANNING.md`](../TRAVEL_PLANNING.md) |
