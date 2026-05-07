# Detour Upgrade Baseline

This note records the local Detour/Navigation compatibility surface before any
controlled vendor replacement. It is intentionally a baseline, not a path
quality fix.

## Compatibility Contract

The controlled migration target as of 2026-05-05 is:

- `.mmtile` Detour tile payloads must use `DT_NAVMESH_VERSION = 7`.
- MaNGOS mmap wrappers must use `MMAP_MAGIC = 0x4D4D4150` and
  `MMAP_VERSION = 6`.
- `MmapTileHeader` is exactly 20 bytes:
  `mmapMagic:uint32`, `dtVersion:uint32`, `mmapVersion:uint32`,
  `size:uint32`, `usesLiquids:uint32`.
- `Navigation.dll` pathfinding remains x64-first and built with
  `DT_POLYREF64`, so `dtPolyRef` and `dtTileRef` are 64-bit. The current bit
  split is `salt=16`, `tile=28`, `poly=20`.
- `Navigation.dll` no longer eagerly loads maps `0`, `1`, and `389` during
  process initialization. It creates the mmap manager at init and lazily loads
  the requested map through the existing `InitializeMapsForContinent(...)`
  path. This keeps service/test startup cheaper while preserving current
  per-map query behavior.
- The native mmap loader now rejects stale or foreign data before calling
  `dtNavMesh::addTile(...)`: wrapper magic, wrapper version, wrapper Detour
  version, positive payload size, Detour payload magic, and Detour payload
  version must all match.
- A local 32-bit-ref trial on 2026-05-04 made the Orgrimmar flight-master
  route return `no_path`, so a 32-bit ref switch remains a future data
  migration, not a safe mechanical flag cleanup.
- The compatibility probe is exported as
  `GetDetourCompatibilityInfo(...)` and
  `ProbeMMapTileCompatibility(...)`. Managed coverage lives in
  `Tests/Navigation.Physics.Tests/DetourCompatibilityTests.cs` and asserts
  the exact wrapper/schema/ref-width contract.

The focused regenerated proof set is map `1` tiles `28,39` through `30,41`.
Evidence:

- Regeneration log:
  `tmp/test-runtime/results-navigation/mmap_regen_map1_org_crossroads_20260504-201741.log`
- Manifest:
  `tmp/test-runtime/results-navigation/detour_mmap_map1_org_crossroads_manifest.json`
- Manifest nav-data signature:
  `F9CE41288735205E8504D476D38C425C196177512DC18E71C5BFB0E9E2678E69`

## Local Detour Surface To Preserve

- `DetourNavMeshQuery` includes sliced pathfinding:
  `initSlicedFindPath`, `updateSlicedFindPath`,
  `finalizeSlicedFindPath`, and `finalizeSlicedFindPathPartial`.
- The local query API includes any-angle and raycast-cost support:
  `DT_FINDPATH_ANY_ANGLE`, `DT_RAYCAST_USE_COSTS`, `dtRaycastHit.pathCost`,
  and `DT_NODE_PARENT_DETACHED`.
- `DetourPathCorridor` is vendored and compiled into `Navigation.dll`.
  `DllMain.cpp` exposes `FindPathCorridor`, `CorridorUpdate`,
  `CorridorMoveTarget`, `CorridorIsValid`, and `CorridorDestroy`.
- `dtNavMeshQuery::getAttachedNavMesh()` is used by native exports that need
  polygon metadata from a query.
- `dtNavMeshQuery::findDistanceToWall()` is used by `PathFinder` for
  capsule-aware smooth-path wall clearance.
- `dtNavMeshQuery::findLocalNeighbourhood()` exists in the vendor surface and
  is a candidate for future generic recovery work.
- `dtQueryFilter::getCost(...)` carries previous, current, and next polygon
  context. Preserve that signature when comparing against upstream Detour.

## Native Integration Surface To Preserve

- `MoveMap.cpp` owns mmap wrapper loading: `.mmap` stores
  `dtNavMeshParams`; `.mmtile` stores `MmapTileHeader` followed by Detour tile
  data and is loaded with `dtNavMesh::addTile(..., DT_TILE_FREE_DATA, ...)`.
- `PathFinder` keeps WoW axis conversion at the Detour boundary:
  WoW `(X,Y,Z)` becomes Detour `(Y,Z,X)`.
- `PathFinder` carries request-scoped capsule radius/height through Detour
  smooth paths, wall-clearance sampling, dynamic-overlay repair, and
  walkability validation.
- `DllMain.cpp` serializes Navigation and corridor operations behind the
  recursive `g_navigationMutex` because shared `dtNavMeshQuery` instances are
  not thread-safe.

## Validation

Run after any Detour build-flag or vendor-surface edit:

```powershell
$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
& $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DetourCompatibilityTests" --logger "console;verbosity=minimal"
```

Run after any focused mmap regeneration:

```powershell
dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:/MaNGOS/data --map 1 --build-log tmp/test-runtime/results-navigation/mmap_regen_map1_org_crossroads_20260504-201741.log --write-manifest tmp/test-runtime/results-navigation/detour_mmap_map1_org_crossroads_manifest.json
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal"
```

The real Orgrimmar flight-master route gate is still not green on this slice:
`LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
timed out at the runsettings `20m` limit after the strict loader and focused
tile regeneration. Treat that as the open PathfindingService route-pack/real
route gate, not as mmap schema failure and not as live-validation evidence.
