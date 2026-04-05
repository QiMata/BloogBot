# Navigation.dll Export Audit — DLL Separation Plan

## Source Files → Target DLL Mapping

### Physics.dll (LOCAL — runs every tick in bot process)
**Source files:** PhysicsEngine.cpp, PhysicsCollideSlide.cpp, PhysicsMovement.cpp, PhysicsGroundSnap.cpp, PhysicsTestExports.cpp (test-only subset)

| Export | Source | Purpose |
|--------|--------|---------|
| `PhysicsStepV2` | DllMain.cpp | Main physics tick — position, velocity, flags |
| `StepPhysicsV2` | PhysicsTestExports.cpp | Test variant of above |
| `GetGroundZ` | DllMain.cpp | Ground-height query (used by CollisionStep) |
| `GetTerrainHeight` | PhysicsTestExports.cpp | Raw terrain height |
| `LineOfSight` | DllMain.cpp | Ray-cast LOS check |
| `SweepCapsule` | PhysicsTestExports.cpp | Capsule sweep collision |
| `OverlapCapsule` | PhysicsTestExports.cpp | Capsule overlap test |
| `IntersectCapsuleTriangle` | PhysicsTestExports.cpp | Single triangle test |
| `SweepCapsuleTriangle` | PhysicsTestExports.cpp | Single triangle sweep |
| `GetPhysicsConstants` | PhysicsTestExports.cpp | Binary parity constants |
| `InitializePhysics` | PhysicsTestExports.cpp | Physics engine init |
| `ShutdownPhysics` | PhysicsTestExports.cpp | Physics engine shutdown |
| `PreloadMap` | DllMain.cpp | Load map data for physics |
| `SetDataDirectory` | DllMain.cpp | Set data file path |
| `SetPhysicsLogLevel` | DllMain.cpp | Debug logging |
| `ComputeCapsuleSweepDiagnostics` | PhysicsTestExports.cpp | Debug diagnostics |
| `ValidateWalkableSegment` | DllMain.cpp | Walkability check |
| `SegmentIntersectsDynamicObjects` | DllMain.cpp | Dynamic obstacle check |
| `IsPointOnNavmesh` | DllMain.cpp | Navmesh point query |
| `FindNearestWalkablePoint` | DllMain.cpp | Nearest walkable point |
| `EvaluateWoW*` | PhysicsTestExports.cpp + PhysicsGroundSnap.cpp | ~80 binary parity test exports |
| `LoadDynamicObjectMapping` | PhysicsTestExports.cpp | Dynamic object system |
| `RegisterDynamicObject` | PhysicsTestExports.cpp | Dynamic object register |
| `UnregisterDynamicObject` | PhysicsTestExports.cpp | Dynamic object unregister |
| `UpdateDynamicObjectPosition` | PhysicsTestExports.cpp | Dynamic object move |
| `ClearDynamicObjects` | PhysicsTestExports.cpp | Clear per-map |
| `ClearAllDynamicObjects` | PhysicsTestExports.cpp | Clear all |
| `GetDynamicObjectCount` | PhysicsTestExports.cpp | Count |
| `GetCachedModelCount` | PhysicsTestExports.cpp | Model cache count |

### SceneData.dll (Docker service OR local)
**Source files:** VMapManager2.cpp, StaticMapTree.cpp, ModelInstance.cpp, WorldModel.cpp, MapLoader.cpp, ADT terrain loading

| Export | Source | Purpose |
|--------|--------|---------|
| `QueryTerrainTriangles` | PhysicsTestExports.cpp | Extract terrain triangles for area |
| `QueryTerrainAABBTriangles` | DllMain.cpp | AABB-scoped triangle query |
| `InjectSceneTriangles` | DllMain.cpp | Inject triangles into physics |
| `ClearSceneCache` | DllMain.cpp | Clear cached scene data |
| `SetSceneSliceMode` | DllMain.cpp | Toggle scene slice mode |
| `InitializeMapLoader` | PhysicsTestExports.cpp | Map data loader init |
| `LoadMapTile` | PhysicsTestExports.cpp | Load specific tile |
| `ExtractSceneCache` | PhysicsTestExports.cpp | Extract scene to file |
| `LoadSceneCache` | PhysicsTestExports.cpp | Load scene from file |
| `HasSceneCache` | PhysicsTestExports.cpp | Check cache exists |
| `UnloadSceneCache` | PhysicsTestExports.cpp | Unload cached scene |
| `SetScenesDir` | PhysicsTestExports.cpp | Set scene directory |
| `ExtractWmoDoodads` | PhysicsTestExports.cpp | WMO doodad extraction |
| `GetVmapDiagnostics` | PhysicsTestExports.cpp | VMAP debug info |

### Navigation.dll (Docker PathfindingService — path-only)
**Source files:** PathFinder.cpp, MoveMap.cpp, Navigation.cpp (path entry points only)

| Export | Source | Purpose |
|--------|--------|---------|
| `FindPath` | DllMain.cpp | A* path query |
| `FindSmoothPath` | (via FindPath) | String-pulled smooth path |
| `PathArrFree` | DllMain.cpp | Free path array memory |
| `FindPathCorridor` | DllMain.cpp | Detour corridor init |
| `CorridorUpdate` | DllMain.cpp | Corridor position update |
| `CorridorMoveTarget` | DllMain.cpp | Corridor target move |
| `CorridorIsValid` | DllMain.cpp | Corridor validity check |
| `CorridorDestroy` | DllMain.cpp | Corridor cleanup |

## Shared Dependencies
- Detour library (pathfinding + physics both need navmesh for obstacle data)
- Map data loading (mmaps/, maps/) — needed by all three
- VMAP data (vmaps/) — needed by physics (LOS) and scene data

## Key Insight
GetGroundZ is physics, not pathfinding. WoW.exe's CMovement::CollisionStep (0x633840) calls ground-height internally every tick. No network hop.
