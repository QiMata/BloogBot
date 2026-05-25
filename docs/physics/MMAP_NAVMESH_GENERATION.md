# MMAP Navmesh Generation Audit

This note is for Crossroads -> Undercity long-pathing and any other route that
depends on large-player clearance through city geometry.

## Required Navmesh Shape

The pathfinding mesh must be generated for Tauren Male clearance, not repaired
after the fact by route-specific runtime overlays.

Post-path generation repair for static-world failures is an explicit
anti-pattern. If a route only works because PathfindingService or native
navigation patched the returned path after query generation, the baked
`.mmap` / `.mmtile` data is still wrong. Fix the generator, source data,
off-mesh authoring, or a serialized final-tile bake pass instead.

- Tauren Male capsule radius: `0.9747`
- Padding: `0.05`
- Required Recast/Detour agent radius: `1.0247`
- Tauren Male capsule height: `2.625`
- Continent Recast cell size: `0.2666666`
- Continent Recast cell height: `0.25`
- Required `walkableRadius`: `ceil(1.0247 / 0.2666666) = 4`
- Required `walkableHeight`: `ceil(2.625 / 0.25) = 11`

For maps `0` and `1`, `config.json` should contain at least:

```json
{
  "0": {
    "agentRadius": 1.0247,
    "agentHeight": 2.625,
    "walkableRadius": 4,
    "walkableHeight": 11
  },
  "1": {
    "agentRadius": 1.0247,
    "agentHeight": 2.625,
    "walkableRadius": 4,
    "walkableHeight": 11
  }
}
```

The stock VMaNGOS `MoveMapGenerator` currently hard-codes continent
`agentRadius = 0.2` and `agentHeight = 1.5` in `TileWorker.cpp`; updating only
`walkableRadius` is not enough because the generated `.mmtile` Detour header
still records the small agent dimensions. Patch the generator to read
`agentRadius` and `agentHeight` from the tile/map/default JSON config and use
those values both for Recast erosion/height derivation and
`dtNavMeshCreateParams.walkableRadius` / `walkableHeight`.

## GameObject Geometry

Generated mmaps must include static server-spawned gameobjects that are relevant
to the Orgrimmar route: city supports, pillars, logs, towers, and ramp/deck
objects. The current data root has the required inputs:

- `vmaps/temp_gameobject_models`
- `gameobject_spawns.json`
- `map1_build.log` with `[GO] map=1 tile=... loaded ... gameobject meshes`

The currently available local generator source under `D:/MaNGOS/source`
contains the required gameobject-spawn bake patch. Before regenerating tiles,
verify `contrib/mmap/src/TileWorker.cpp` still reads
`vmaps/temp_gameobject_models`, reads `gameobject_spawns.json`, applies the
WoW-to-Recast axis conversion for GO bounds, and logs
`[GO] map=... tile=...: marked ... gameobject span boxes`.

Refresh the spawn file with the read-only exporter:

```powershell
dotnet run --project tools/GameObjectExporter/GameObjectExporter.csproj -- "Server=127.0.0.1;Database=mangos;User=root;Password=root;" "D:/MaNGOS/data/gameobject_spawns.json"
```

## No Route-Specific Runtime Geometry Hacks

Static blockers must be represented by GO-aware mmap generation, not by
production code that knows about one route. The following are explicitly
forbidden as fixes for static-world clipping:

- Hardcoded Orgrimmar blocker coordinate lists or clearance cylinders in
  `Services/PathfindingService`, `Exports/BotRunner`, or live-validation code.
- Route-specific detour waypoints or smoothing exceptions that only make the
  flight-master -> zeppelin route pass.
- Live-position guards used as a substitute for fixing generated path geometry.

Deterministic tests may list known blocker positions as evidence and as a red
gate. They must not be mirrored into production path generation as avoidance
logic. If a generated route clips static objects or corners, keep the route gate
red and fix the exporter/generator/data so ordinary Detour pathfinding avoids
those locations naturally.

## Audit Command

Run the repo-owned audit before and after any mmap regeneration:

```powershell
dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:/MaNGOS/data --map 1 --build-log <focused-generation-log> --write-manifest tmp/test-runtime/results-navigation/detour_mmap_map1_org_crossroads_manifest.json
```

If the build log only covers a single focused tile, add `--tile X,Y`. The
default audit tile set is the historical broader Orgrimmar route slice and will
otherwise report missing GO bake lines for tiles that were never regenerated in
that log:

```powershell
dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:/MaNGOS/data --map 1 --tile 40,29 --build-log <focused-generation-log> --write-manifest <focused-manifest>.json
```

Then run the route-level clearance gate before any live Crossroads ->
Undercity validation:

```powershell
$env:WWOW_DATA_DIR='D:\MaNGOS\data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers.trx" --results-directory tmp/test-runtime/results-pathfinding
```

Do not use `--no-build` for this route gate after editing PathfindingService
or test code; a no-build rerun is only appropriate after data-only changes.

Historical note: older iterations temporarily leaned on generic corridor repair
to keep the route usable. That is no longer the accepted direction. Treat any
static-world post-path generation repair as a transitional artifact to delete,
not as a legitimate long-term fix surface.

The audit checks:

- `config.json` contains Tauren Male `agentRadius`, `agentHeight`, and Recast
  cell settings for the audited map.
- Audited `.mmtile` wrappers are exact mmap schema version `6`, Detour
  wrapper version `7`, Detour payload version `7`, and 20-byte wrapper
  headers with uint32 `usesLiquids`.
- Audited `.mmtile` Detour headers were actually generated with Tauren-sized
  `walkableRadius` and `walkableHeight`.
- `temp_gameobject_models` and `gameobject_spawns.json` contain model-backed
  spawns in the audited tile set.
- The selected build log proves the audited tiles loaded or marked baked GO
  geometry. Use `--build-log <path>` when auditing a focused regeneration log
  instead of the default `map<N>_build.log`.
- When `--write-manifest <path>` is supplied, the audit writes a manifest with
  schema version, source data root, generator path, Detour/mmap versions,
  ref width, agent dimensions, per-tile hashes, and a combined nav-data
  signature.

MaNGOS writes tile files as `mapId + tileY + tileX`; for example generator
tile `28,40` is `mmaps/0014028.mmtile`.

Focused Detour/mmap migration result on 2026-05-05:

- Regenerated map `1` tiles `28,39` through `30,41` with
  `D:/MaNGOS/source/bin/MoveMapGenerator.exe`, `--threads 1`, `--silent`, and
  `--configInputPath config.json`.
- Backup of the previous focused tiles:
  `D:/MaNGOS/data/mmaps/detour-migration-backup-20260504-201741`.
- Generation log:
  `tmp/test-runtime/results-navigation/mmap_regen_map1_org_crossroads_20260504-201741.log`.
- Audit/manifest:
  `tmp/test-runtime/results-navigation/detour_mmap_map1_org_crossroads_manifest.json`.
- Manifest nav-data signature:
  `F9CE41288735205E8504D476D38C425C196177512DC18E71C5BFB0E9E2678E69`.
- Every focused tile audited as mmap wrapper version `6`, Detour wrapper
  version `7`, Detour payload version `7`, `usesLiquids=1`,
  `walkableRadius=1.0247`, `walkableHeight=2.6250`, and
  `walkableClimb=1.8000`.
- GO marking counts in the focused log:
  `28,39=56`, `28,40=13`, `28,41=71`, `29,39=22`, `29,40=38`,
  `29,41=16`, `30,39=20`, `30,40=23`, `30,41=12`.

Current audit result after the full fresh map `0` and map `1` regeneration on
2026-05-01:

- `D:/MaNGOS/data/config.json` has map `1` set to `agentRadius=1.0247`,
  `agentHeight=2.625`, `walkableRadius=4`, and `walkableHeight=11`.
- Every fresh root map `0` and map `1` tile has the Tauren-sized Detour
  header values:
  `map=000 tiles=515 radius={1.0247x515} height={2.6250x515} climb={1.8000x515}`;
  `map=001 tiles=785 radius={1.0247x785} height={2.6250x785} climb={1.8000x785}`.
- Audited Orgrimmar route tile files pass with Detour headers
  `walkableRadius=1.0247` and `walkableHeight=2.625`.
- Audited Undercity arrival tiles on map `0`, `27,30` through `30,32`, pass
  with Detour headers `walkableRadius=1.0247` and `walkableHeight=2.625`.
- GO input evidence still passes, with `930` model mappings, `868` modeled
  spawns in the Orgrimmar tile set, and `772` modeled spawns in the Undercity
  arrival tile set.
- The focused route gate passed in
  `tmp/test-runtime/results-pathfinding/long_pathing_route_static_blockers_after_full_fresh_regen.trx`.

Historical audit result before focused regeneration:

- GO evidence passes for the Orgrimmar route tiles.
- Radius/height evidence fails: Orgrimmar tiles still report
  `walkableRadius=0.2` and `walkableHeight=1.5`.

Example focused tile generation command after the generator/config are fixed:

```powershell
Push-Location D:/MaNGOS/data
D:/MaNGOS/source/bin/MoveMapGenerator.exe 1 --tile 28,40 --threads 1 --silent --configInputPath config.json
Pop-Location
```

Focused upstream-backport verification on 2026-05-21 for Orgrimmar tile
`1:40,29` / runtime `0012940.mmtile`:

- Generator build:
  `.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release`
- Backup:
  `D:\MaNGOS\data\mmaps\regen-backup-20260521-180313\0012940.mmtile`
- Focused regen log:
  `tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260521_upstream_backports.log`
- Focused audit/manifest:
  `tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260521_upstream_backports_manifest.json`
- Validation commands:

```powershell
Push-Location D:\MaNGOS\data
& 'E:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 1 --tile 40,29 --threads 1 --silent --debug --offMeshInput 'E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt' --configInputPath 'E:\repos\Westworld of Warcraft\tools\MmapGen\config.json'
Pop-Location

dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\MaNGOS\data --map 1 --tile 40,29 --build-log tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260521_upstream_backports.log --write-manifest tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260521_upstream_backports_manifest.json

dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck" --logger "console;verbosity=minimal"

$env:WWOW_DATA_DIR='D:\MaNGOS\data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_20260521_upstream_backports.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=420000

.\tools\scripts\export-pathfinding-reference.ps1 -Route og-zeppelin -Resume -MmapGenExe .\tools\MmapGen\build\MmapGen.exe
.\tools\scripts\summarize-pathfinding-reference.ps1 -Route og-zeppelin
```

- Validation result:
  - `NavDataAudit` PASS for `--tile 40,29`
  - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck*` PASS `4/4`
  - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers` PASS `1/1`
    with TRX at
    `tmp/test-runtime/results-pathfinding/pathfinding_org_fm_static_blockers_20260521_upstream_backports.trx`
  - refreshed `og-zeppelin/latest` bundle reports `297` top-ramp/deck crop polys,
    worst `zRange=1.000y`, and no mixed-wall or shadowed-lower-ledge regression
    failures in the focused mesh-quality tests

The full map `0` and `1` data set is now compatible with the focused route
gate. Focused live Crossroads -> Undercity evidence is the next proof step.

Focused full vendor-sync + split-root verification on 2026-05-22 for
Orgrimmar tile `1:40,29` / runtime `0012940.mmtile`:

- The vendored Recast bake core under
  `tools/MmapGen/dep/recastnavigation/Recast/{Include,Source}` was synced all
  the way to upstream `main` commit `9f4ce64`.
- Canonical bake path was switched to the split data roots documented in
  `MMAP_DATA_FLOW.md`: bake into `D:/wwow-bot/test-data`, audit there, then
  promote the approved tile into `D:/wwow-bot/prod-data` for Docker.
- `TileWorker.cpp` now falls back to
  `WWOW_VMANGOS_DATA_DIR/gameobject_spawns.json` when the mutable data root has
  no local `gameobject_spawns.json`, so GO-backed city tiles stay auditable in
  the split-root flow.
- `NavDataAudit` accepts `--config-path` and `--spawns-path` for the same
  split-root workflow and records those resolved inputs in the manifest.

Validation commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "recast-full-sync-og-4029-go-fallback" -DataDir "D:\wwow-bot\test-data"

dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\test-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log tmp/bake-sweeps/recast-full-sync-og-4029-go-fallback-20260522T000716Z/bake.log --write-manifest tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260522_full_recast_sync_testdata_manifest.json

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck" --logger "console;verbosity=minimal" --logger "trx;LogFileName=mmap_mesh_quality_org_zeppelin_full_recast_sync_testdata.trx" --results-directory tmp/test-runtime/results-pathfinding
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_full_recast_sync_testdata.trx" --results-directory tmp/test-runtime/results-pathfinding

.\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "40,29"
docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding
docker compose -f docker-compose.vmangos-linux.yml up -d wwow-pathfinding wwow-scene-data

$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_full_recast_sync_proddata.trx" --results-directory tmp/test-runtime/results-pathfinding

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
.\tools\scripts\export-pathfinding-reference.ps1 -Route og-zeppelin -Resume -MmapGenExe .\tools\MmapGen\build\MmapGen.exe
.\tools\scripts\summarize-pathfinding-reference.ps1 -Route og-zeppelin
```

Validation result:

- `NavDataAudit` PASS for `D:\wwow-bot\test-data`, tile `40,29`, with manifest
  `tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260522_full_recast_sync_testdata_manifest.json`
- `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck*` PASS `4/4`
- `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers` PASS `1/1`
  on both test-data and promoted prod-data
- promoted `0012940.mmtile` SHA256 matches exactly between test-data and
  prod-data:
  `40DAF1915B9A9CE4BD3CA9832C4105C38F713A77012C378847C37B1F5EC9C38D`
- Docker `wwow-pathfinding` rebuilt to image manifest
  `sha256:f8225328380120e77625dacec1f4e0e9ed764a5627a31c613fc2ee66fa153ecf`,
  restarted cleanly, and reported `IsReady=true` / `StatusMessage="Ready -
  navigation initialized"` with the promoted data mounted from `prod-data`
- refreshed `tmp/test-runtime/visualization/pathfinding/og-zeppelin/latest/analysis/summary.md`
  now reports:
  - top-ramp/deck crop `268` polygons
  - reachable subset `187`
  - unreachable subset `81`
  - worst `zRange=1.000y`

Focused raw-runtime anchor-trim follow-up on 2026-05-22 for Orgrimmar tiles
`1:40,28` and `1:40,29` / runtime `0012840.mmtile` + `0012940.mmtile`:

- Goal:
  - keep the default runtime on raw Detour
  - push another fix cycle into MmapGen bake output instead of restoring
    managed/native repair
  - specifically target the OG tower-ramp/off-mesh anchor artifact class
- Code surfaces changed:
  - `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`
  - `tools/MmapGen/config.json`
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshBuilder.cpp`
  - `Exports/Navigation/Detour/Source/DetourNavMeshBuilder.cpp`

Validation commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  Exports\Navigation\Navigation.vcxproj `
  -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal

Push-Location D:\wwow-bot\prod-data
& 'E:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 1 --tile 40,28 --silent --threads 1 --offMeshInput 'E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt' --configInputPath 'E:\repos\Westworld of Warcraft\tools\MmapGen\config.json'
& 'E:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 1 --tile 40,29 --silent --threads 1 --offMeshInput 'E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt' --configInputPath 'E:\repos\Westworld of Warcraft\tools\MmapGen\config.json'
Pop-Location

dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\prod-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log "tmp/bake-sweeps/raw-detour-anchor-trim-20260522T032257Z/tile_4029.log" --write-manifest tmp/test-runtime/results-navigation/raw_detour_anchor_trim_tile4029_manifest.json

$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection|FullyQualifiedName~LongPathingRouteTests.OrgrimmarApproachToBoardingPosition_PathExistsAndDescribesOffMeshUsage" --logger "console;verbosity=minimal"

dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_raw_detour_anchor_trim.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000
```

Validation result:

- MmapGen rebuild passed; native `Navigation.dll` rebuild passed.
- Focused regen artifacts live under
  `tmp/bake-sweeps/raw-detour-anchor-trim-20260522T032257Z/`.
- `NavDataAudit` on `40,29` still passes the Detour/header/capsule contract and
  writes
  `tmp/test-runtime/results-navigation/raw_detour_anchor_trim_tile4029_manifest.json`.
  The only red lines are the long-standing GO build-log marker gaps:
  `tile_4029.log does not show gameobject spawn loading` and
  `tile_4029.log has no GO geometry bake line for tile 40,29`.
- Focused OG mesh/runtime checks stayed green:
  - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck*` PASS
  - `LongPathingRouteTests.OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection` PASS
  - `LongPathingRouteTests.OrgrimmarApproachToBoardingPosition_PathExistsAndDescribesOffMeshUsage` PASS
  - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers` PASS
  - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath` PASS
  - `LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments` PASS
- `CriticalWalkLegs` improved from the earlier raw-runtime `16/23` shape to
  `17/23` green: the exact `orgrimmar_zeppelin_tower_ramp` route is no longer
  failing.
- Remaining raw-runtime red cases after this loop:
  - `orgrimmar_city_live_vertical_replan_recovery`
  - `orgrimmar_city_hallway_live_wall_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
  - `orgrimmar_exterior_incline_live_stall_exact_recovery`
  - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`

Focused Detour PR `#725` follow-up on 2026-05-22 for Orgrimmar tile
`1:40,29` / runtime `0012940.mmtile`:

- Goal:
  - test upstream Detour PR `#725` (`fix findNearestPoly result error`) against
    the remaining OG dead-end stack failures
  - keep the `.mmtile` / loader contract intact while adapting the fix to
    WWoW's serialized layout
- Code surfaces changed:
  - `tools/MmapGen/dep/recastnavigation/Detour/Source/DetourNavMeshBuilder.cpp`
  - `Exports/Navigation/Detour/Source/DetourNavMeshBuilder.cpp`
- Important implementation note:
  - the upstream one-line change is not sufficient in WWoW's fork
  - `header->bvNodeCount` participates in section offset calculation during
    load, so the safe local port had to shrink the serialized BV-tree payload
    itself, not just the header field

Validation commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  Exports\Navigation\Navigation.vcxproj `
  -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal

$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,28;40,29" -Variant "og-bvnodecount-pr725-fixed-layout" -DataDir "D:\wwow-bot\test-data"

dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\test-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log tmp/bake-sweeps/og-bvnodecount-pr725-fixed-layout-20260522T233732Z/bake.log --write-manifest tmp/test-runtime/results-navigation/og_bvnodecount_pr725_fixed_layout_tile4029_manifest.json

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_bvnodecount_pr725_fixed_layout.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000

dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_bvnodecount_pr725_fixed_layout_focused.trx" --results-directory tmp/test-runtime/results-pathfinding

powershell -ExecutionPolicy Bypass -File E:\repos\tools\scripts\build-recastnavigation.ps1 -Configuration Debug -RunUpstreamTests
```

Validation result:

- Both WWoW builds passed.
- The safe adapted PR-725 bake changed `0012940.mmtile` hash to
  `43EACD6F5E53818F0478550EC8D4CB407F95C82528C79F7B07D060FCDCACC744`.
- The final tile payload shrank by `16` bytes, which matches the reduced
  actual BV-tree node count.
- `NavDataAudit` still passes and wrote
  `tmp/test-runtime/results-navigation/og_bvnodecount_pr725_fixed_layout_tile4029_manifest.json`.
- Focused OG route/mesh checks stayed green (`6/6`).
- Full `CriticalWalkLegs` remained `17/23` green with the same six reds as the
  prior anchor-stack baseline:
  - `orgrimmar_city_live_vertical_replan_recovery`
  - `orgrimmar_city_hallway_live_wall_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
  - `orgrimmar_exterior_incline_live_stall_exact_recovery`
  - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
- Standalone upstream-style Recast tests still passed `32`, with `1` expected
  degenerate-triangle skip.

Takeaway:

- PR `#725` is a valid Detour upgrade to keep, but it does not cure the
  remaining OG failures.
- The remaining problem set is still upstream of route generation: local baked
  topology around the city/hallway/exterior dead-end anchors and the underpass
  climb shape.

2026-05-23 anchor-stack / lower-fringe follow-up on tile `1:40,29`:

- Goal:
  - keep the raw/native Detour runtime path
  - continue tuning the final-tile bake only
  - understand why the remaining city/hallway/hall-exit starts still snap into
    dead-end local basins
- Code/config surfaces exercised:
  - `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`
  - `tools/MmapGen/config.json`
- Experiments run:
  - conservative support-surface fallback from `closestPointOnPoly`
  - stricter anchor support floor (`surface >= anchorZ`)
  - extra anchor-cull probe coords from the actual failed route collapse points
  - anchor-local lower-fringe cull when a higher overlapping layer exists in
    the same XY window

Validation commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_anchor_stack_citystarts_closest" -DataDir "D:\wwow-bot\test-data"
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_anchor_stack_citystarts_upsupport" -DataDir "D:\wwow-bot\test-data"
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_anchor_probe_candidates" -DataDir "D:\wwow-bot\test-data"
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_anchor_lower_fringe" -DataDir "D:\wwow-bot\test-data"

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_anchor_lower_fringe_focused.trx" --results-directory tmp/test-runtime/results-pathfinding

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_anchor_lower_fringe.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000
```

Validation result:

- Every branch kept the focused OG checks green (`6/6`).
- None of the four branches beat the current `18/23` ceiling on
  `CriticalWalkLegs`.
- The strongest new signal came from the probe files, not the scoreboard:
  the failing starts still resolve to slightly-below-anchor nearest-poly
  winners:
  - vertical start -> `0x100001520BEEE`, `surfaceZ=11.009`
  - hallway start -> `0x100001520ADA2`, `surfaceZ=16.885`
  - hall-exit start -> `0x1000015209D5A`, `surfaceZ=23.209`
- The lower-fringe branch changed the final tile hash to
  `01629C2251081B8C00E1F546F1690053B70BD8C9491641696603F926D373F9F3`
  and did real lower-fringe culls, but it did **not** improve the total and
  shortened the hallway dead-end path, so it is not a promotion candidate.

Relevant artifacts:

- `tmp/bake-sweeps/og_anchor_stack_citystarts_closest-20260523T031828Z/`
- `tmp/bake-sweeps/og_anchor_stack_citystarts_upsupport-20260523T033128Z/`
- `tmp/bake-sweeps/og_anchor_probe_candidates-20260523T034013Z/`
- `tmp/bake-sweeps/og_anchor_lower_fringe-20260523T034944Z/`
- `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_anchor_lower_fringe.trx`
- `tmp/test-runtime/results-pathfinding/og_vertical_anchor_polyrefs_closest_20260523.tsv`
- `tmp/test-runtime/results-pathfinding/og_hallway_anchor_polyrefs_closest_20260523.tsv`
- `tmp/test-runtime/results-pathfinding/og_hallexit_anchor_polyrefs_closest_20260523.tsv`

Takeaway:

- The next session should not keep retuning generic support-band thresholds.
- The remaining work is now specific: either cull the actual nearest-poly
  winner component at the verified bad anchors, or move earlier in the bake
  and prevent these local start-cell basin layers from ever surviving into the
  final Detour tile.

2026-05-23 preferred-support / `minRegionArea` follow-up on tile `1:40,29`:

- Research-backed knobs/mechanics tried:
  - final-tile preferred-support scoring inside `CullAnchorPolyStacks(...)`
  - tile-local `minRegionArea=60` override from the official `rcConfig`
    guidance on removing isolated watershed regions
- Commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release

.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_4029_anchor_preferred_support" -DataDir "D:\wwow-bot\test-data"
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_preferred_support_focused.trx" --results-directory tmp/test-runtime/results-pathfinding
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_anchor_preferred_support.trx" --results-directory tmp/test-runtime/results-pathfinding

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
& .\Bot\Release\net8.0\PathPhysicsProbe.exe --map 1 --path tmp/test-runtime/results-pathfinding/og_anchor_deadend_exact_preferred_support_20260523.txt --dump-polyrefs --polyref-xy-extent 2.0 --polyref-z-extent 10.0 *> tmp/test-runtime/results-pathfinding/og_anchor_deadend_exact_preferred_support_polyrefs_20260523.tsv
& .\Bot\Release\net8.0\PathPhysicsProbe.exe --map 1 --path tmp/test-runtime/results-pathfinding/og_anchor_deadend_exact_preferred_support_20260523.txt --dump-poly-stack --polyref-xy-extent 2.0 --polyref-z-extent 10.0 *> tmp/test-runtime/results-pathfinding/og_anchor_deadend_exact_preferred_support_stack_20260523.txt

.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_4029_minRegionArea60" -DataDir "D:\wwow-bot\test-data" -ConfigPath "E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_minRegionArea60.json"
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_minRegionArea60_focused.trx" --results-directory tmp/test-runtime/results-pathfinding
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_minRegionArea60.trx" --results-directory tmp/test-runtime/results-pathfinding

.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og_4029_source_restore_after_negative_experiments" -DataDir "D:\wwow-bot\test-data"
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_source_restore_after_negative_experiments_focused.trx" --results-directory tmp/test-runtime/results-pathfinding
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_source_restore_after_negative_experiments.trx" --results-directory tmp/test-runtime/results-pathfinding
```

- Result:
  - Preferred-support branch changed the tile hash to
    `345FA5BBFF7BDFDCFE58B3B061C9E25D162B17723A536C8FDE85E2383FBBA671`, but
    exact dead-end winners did not move and the full sweep stayed `17/23`.
  - Exact stack probes still showed the same-height competitor set around the
    surviving dead-end winners (`BE35`, `AD5D`, `8D00`, `4ECD`, `47F5`), so the
    problem is not just support tie-breaking anymore.
  - `minRegionArea=60` kept the focused slice green and the full sweep stayed
    `17/23`; the route traces were effectively unchanged.
  - I reverted the preferred-support code and restored `test-data` to the
    source-backed tile hash
    `E299BDC34EEFD82F2B0466B66BE09E7BDCDC3A683C59106778D96A55C01824B4`.
  - The restored source-backed state is verified at `6/6` focused green and
    `17/23` on `CriticalWalkLegs`.

- Next-step conclusion:
  - stop churning support scoring and generic `minRegionArea`/`mergeRegionArea`
    tuning
  - target the actual dead-end basin/component, or move earlier in the
    region/contour pipeline so that basin never survives into the final tile

2026-05-23 original-worker comparison bake on tile `1:40,29`:

- Goal:
  - answer "what does the older/original worker actually produce here?"
  - compare a real baseline tile against the current source-backed worker
  - verify whether the GO-aware feed is materially helping this route slice
- Baseline used:
  - earliest in-repo `TileWorker.cpp` state: local commit `4e3716ae`
    (`2026-05-07`)
  - scratch-only compatibility edit in the copied source:
    `SortAndRasterizeTriangles(...) -> rcRasterizeTriangles(...)`
  - scratch build dir:
    `tmp/mmapgen-baseline-20260507/`
- Commands:

```powershell
& "E:\repos\Westworld of Warcraft\tmp\mmapgen-baseline-20260507\build-mmapgen.ps1" -Configuration Release -Reconfigure

Push-Location D:\wwow-bot\test-data
& "E:\repos\Westworld of Warcraft\tmp\mmapgen-baseline-20260507\build\MmapGen.exe" 1 --tile 40,29 --threads 1 --silent --offMeshInput "E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt" --configInputPath "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json"
Pop-Location

dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\test-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log "tmp/bake-sweeps/tileworker_20260507_baseline_20260523T123759Z/bake.log" --write-manifest "tmp/test-runtime/results-navigation/tileworker_20260507_baseline_tile4029_manifest.json"

dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck" --logger "console;verbosity=minimal" --logger "trx;LogFileName=tileworker_20260507_baseline_focused_mesh_quality.trx" --results-directory tmp/test-runtime/results-pathfinding

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=tileworker_20260507_baseline_route_gate.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=420000
```

- Result:
  - baseline tile hash:
    `5EC417472F918E93A1255098FFFDD86B1F56CDE91E4BA0ED8235CCD004C49675`
  - bake log:
    `tmp/bake-sweeps/tileworker_20260507_baseline_20260523T123759Z/bake.log`
  - audit manifest:
    `tmp/test-runtime/results-navigation/tileworker_20260507_baseline_tile4029_manifest.json`
  - focused mesh-quality slice still passed `4/4`:
    `tmp/test-runtime/results-pathfinding/tileworker_20260507_baseline_focused_mesh_quality.trx`
  - focused static-blocker route gate failed:
    `tmp/test-runtime/results-pathfinding/tileworker_20260507_baseline_route_gate.trx`
  - `NavDataAudit` still passed the Tauren/Detour header checks, but failed the
    GO-feed evidence:
    - `bake.log does not show gameobject spawn loading`
    - `bake.log has no GO geometry bake line for tile 40,29`

- Practical takeaway:
  - the older worker can still build a tile that "looks" plausible enough for
    the focused mesh-quality test
  - but without the current GO-aware feed it regresses the real route and
    brings back the classic static-blocker failures
  - in other words, feeding server-spawned WoW GO geometry into the bake is
    not optional for this tile class

- Restore step after the comparison:
  - restored `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  - restored hash:
    `E299BDC34EEFD82F2B0466B66BE09E7BDCDC3A683C59106778D96A55C01824B4`

2026-05-23 source-support stage-probe loop on tile `1:40,29`:

- I instrumented `TileWorker.cpp` with stage summaries keyed off the verified
  OG anchor list so normal bakes can report source-support presence through:
  - rasterize / filterLowHanging / filterLedge / removeUseless /
    filterLowHeight / waterInheritance
  - buildCHF / markGameObjects / erode / median
- Commands:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_stage_support_probe_v2 -DataDir D:\wwow-bot\test-data -Quiet
```

- Artifact dir:
  - `tmp/bake-sweeps/og_4029_stage_support_probe_v2-20260523T133825Z/`
- Result:
  - the source-support pre-region branch still did not earn promotion as a real
    bake fix
  - the new `HF-SRC-ANCHOR` / `CHF-SRC-ANCHOR` plumbing is worth keeping as
    instrumentation, but the current per-subtile log stream is still noisy and
    should not be treated as proof that a route-fixing cull exists
  - because the branch remained unproven, I disabled
    `preRegionCullAnchorSourceSupportCompetingSpans` again in
    `tools/MmapGen/config.json`
- Restore note:
  - the latest bake-sweep snapshot restored `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
    to pre-loop hash:
    `FE0C8973C5D6344B9121F896F2255670C27781A14A5D47254BE3D33D458E0F25`

## 2026-05-23 partition/simplification rollback note

- I isolated the active `4029` config regressions and restored the source tree
  to the conservative `watershed + default maxSimplificationError` state.
- Negative results:
  - `watershed + maxSimplificationError=1.3`
    - hash:
      `932A176CD19C96B38E319ACDFD085A3BD9BC68E00FB6A792AB541F69F7AC713C`
    - focused slice fell to `4/7`
    - giant bridge polys and `offMeshPolyCount=0` proved `1.3` is not a safe
      "better contour fidelity" knob for tile `40,29`
  - `layers + 1.8`
    - hash:
      `814BA912D2089383FEB6AA5836AC4FAC62F16FE21B22E9B2FEE8DD2E2B2DBBE3`
    - focused slice fell to `5/7`
    - one shadowed lower trim ledge and reduced deck connector density
      remained
- Restored source-backed state:
  - rebake command:

```powershell
.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release
.\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og_4029_source_restore_watershed18 -DataDir D:\wwow-bot\test-data -ConfigPath "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" -Quiet
```

  - restored hash:
    `FB2FBAF1848FC2ACFB1F9E093A8EC99284C9C19843CD64E4F15CA4FBBF3315D6`
  - focused validation:
    `tmp/test-runtime/results-pathfinding/og_4029_source_restore_watershed18_focused.trx`
    (`7/7` pass)
  - full raw-Detour sweep:
    `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_source_restore_watershed18.trx`
    (`17/23` pass)
- Operational rule for future loops:
  - do not tighten `maxSimplificationError` on `40,29` as a generic
    navmesh-quality experiment
  - keep `partitionType=layers` experimental-only until a bake-side trim
    closes its remaining deck regressions without sacrificing connector
    coverage

## 2026-05-23 anchor stage manifest workflow for tile `1:40,29`

- New workflow:
  - enable `writeAnchorStageManifest=true` on the target tile
  - leave `logAnchorStageDiagnostics=false` for the normal loop
  - run one focused bake with `tools/scripts/bake-tile.ps1`
  - inspect the generated summary JSON/CSV under
    `tmp/bake-sweeps/<variant>/analysis/`
  - only if the structured summary is insufficient, temporarily flip
    `logAnchorStageDiagnostics=true` to restore the old
    `SRC-ANCHOR-SUPPORT` / `HF-SRC-ANCHOR` / `CHF-SRC-ANCHOR` /
    `CHF-SRC-COMP` print stream
- What `bake-tile.ps1` now does automatically:
  - copies `meshes/map0012940_anchor_stage_manifest.json` into the variant's
    `analysis/` folder
  - runs `tools/NavDataAudit --stage-summary-only --stage-manifest ...`
  - writes:
    - `map0012940_anchor_stage_summary.json`
    - `map0012940_anchor_stage_summary.csv`
- Final validated run:

```powershell
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1' -Configuration Release

$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1' -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_stage_manifest_clean' -DataDir 'D:\wwow-bot\test-data'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_stage_manifest_clean_focused.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'
```

- Final artifact bundle:
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/`
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_manifest.json`
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_summary.json`
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_summary.csv`
- Validation for that run:
  - tile hash remained the approved source-backed hash:
    `FB2FBAF1848FC2ACFB1F9E093A8EC99284C9C19843CD64E4F15CA4FBBF3315D6`
  - focused OG slice stayed green `7/7`
  - full `CriticalWalkLegs` sweep was intentionally skipped because the tile
    hash did not change
- First-bad-stage answers now proven by the summary:
  - `1546.600,-4435.900,11.500` -> `finalDetour`
  - `1522.500,-4424.100,17.000` -> `finalDetour`
  - `1523.800,-4425.900,17.100` -> `median`
  - `1521.267,-4425.600,17.609` -> `contours`
  - `1521.300,-4422.500,17.100` -> `sourceSupport`
- Meaning:
  - if a future change claims to fix the hallway/city set, the first check is
    whether those five answers move in the right direction
  - if they do not, do not spend another loop on `partitionType` or
    `maxSimplificationError`

### Follow-up workflow: restore + source-support window cull branch

- Best current bake-side branch after the manifest landed:
  - `preRegionRestoreAnchorSourceSupportAfterErode=true`
  - `preRegionCullAnchorSourceSupportCompetingSpans=true`
  - `preRegionCullAnchorSourceSupportFallbackToWindow=true`
- Why this branch matters:
  - it is the first branch that improved the structured anchor answers without
    regressing the focused OG `7/7` slice
  - it moved two hallway/city anchors out of the red set entirely:
    - `1522.500,-4424.100,17.000`
    - `1521.267,-4425.600,17.609`
- Exact run:

```powershell
$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1' -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_source_cull_window' -DataDir 'D:\wwow-bot\test-data'
```

- Resulting artifact bundle:
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/`
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/analysis/map0012940_anchor_stage_summary.json`
  - `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/analysis/map0012940_anchor_stage_summary.csv`
- Validation:
  - tile hash changed to
    `29449D252853BF2E3B9739DC108BA0E4CE1E0F4C1152D7BADAE45032984945C5`
  - focused slice stayed `7/7`
  - full `CriticalWalkLegs` stayed `17/23`
- What that means for future loops:
  - keep this branch in mind as a better stage baseline than the earlier clean
    manifest run, because it removed proven wrong basins at multiple anchors
  - do not claim route success from it yet; the route failures simply moved
    further along
  - next iteration should extend the anchor list with the shifted dead-end
    coords from the current failing routes rather than resuming generic knob
    changes

### Analysis-only shifted dead-end coords

- `anchorStageManifestCoordsWow` is now the safe way to add extra manifest
  probes without changing the actual cull list.
- Validated run:
  - `tmp/bake-sweeps/og_4029_manifest_shifted_deadends_v2-20260523T221238Z/`
  - hash remained `29449D252853BF2E3B9739DC108BA0E4CE1E0F4C1152D7BADAE45032984945C5`
- Current shifted dead-end probe answers:
  - `1537.300,-4437.900,13.000` -> green
  - `1520.600,-4426.500,17.900` -> green
  - `1355.600,-4522.300,33.100` -> green
- Practical implication:
  - the route failures now sit beyond those exact endpoint cells, so treat the
    next loop as a corridor/connectivity hunt, not another local support-cell
    cleanup pass at those same coords

### 2026-05-24 finalDetour component manifest follow-up

- The stage manifest now records final Detour component metadata per candidate:
  - `componentId`
  - `componentPolyCount`
  - `componentArea2D`
  - stage-level `supportComponentCount` / `lowerComponentCount`
- Validated bake:
  - `tmp/bake-sweeps/og_4029_component_manifest_links-20260524T000728Z/`
  - hash stayed
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - because this was analysis-only, focused/full route expectations did not
    need a second sweep beyond the existing `7/7` focused and `17/23` full
    results already validated on the same hash
- What the new manifest proved in the hallway chain:
  - `1518.200,-4419.800,17.100` still has two final support candidates
    (`0x1000000000AD7E`, `0x1000000000ADA1`)
  - the winner is still `0x1000000000ADA1`
  - `1520.600,-4426.500,17.900` still has seven final support candidates and
    still wins `0x1000000000AD6E`
  - `1523.800,-4425.900,17.100` is still
    `finalDetour -> lower_competitor_dominant` with winner
    `0x1000000000ADAB`
- Runtime probes against the same hash:
  - `1518.2,-4419.8,17.1 -> goal` still resolves only the short trapped
    hallway route ending near `1520.567,-4426.500,17.909`
  - `1520.6,-4426.5,17.9 -> goal` returns no real route (`FindPath <2 corners`)
  - `1523.8,-4425.9,17.1 -> goal` returns only a 2-corner local trap
  - `1491.4,-4417.3,23.3 -> goal` also dead-ends locally near
    `1479.77,-4426.00,25.31`
- Practical meaning:
  - the hallway/hall-exit failure is no longer “one bad endpoint snap”
  - it is a chain of trapped final Detour basins across the hallway anchors
  - next useful tooling/fix surface is pair-specific final reachability or a
    component-targeted cull of the current trapped winner basin, not more
    source-support threshold churn

### 2026-05-24 routeability-aware trapped-component cull follow-up

- New proof surface:
  - `anchorRouteTargetsWow` adds local escape targets to the finalDetour
    manifest
  - the summary now carries:
    - `FinalWinnerRouteableToAnyTarget`
    - `FinalResolvedRouteTargetCount`
    - `FinalRouteableSupportCandidateCount`
    - `FinalRouteableSupportComponentCount`
  - optional experiment flag:
    `postDetourCullAnchorTrappedComponents`
- Validated branches:
  - `tmp/bake-sweeps/og_4029_anchor_routeability_cull-20260524T004027Z/`
    - hash:
      `B84D1CD2369E03721ECBDC83656EC4E700E546886CFF49C231F52F05CED086AF`
  - `tmp/bake-sweeps/og_4029_anchor_routeability_chain_targets-20260524T005038Z/`
    - hash:
      `039BEDF73A2318B0D6559BDC0FB453D240875EDD08BA2319F56A0EA26D85EA94`
- Commands:

```powershell
$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'
powershell -ExecutionPolicy Bypass -File 'E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1' -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_routeability_chain_targets' -DataDir 'D:\wwow-bot\test-data'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_routeability_chain_targets_focused.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'

$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
dotnet test 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj' --configuration Release --no-build --no-restore --settings 'E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings' -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_anchor_routeability_chain_targets.trx" --results-directory 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding'
```

- Results:
  - focused slice stayed `7/7`
  - full `CriticalWalkLegs` stayed `17/23`
  - the six failing route labels stayed the same
  - the underpass route changed from "dead-end locally" to "routes out through
    the overhead ramp/ceiling first"
- Routeability summary interpretation on this branch:
  - city / hallway / hallway-exit anchors still had
    `FinalRouteableSupportComponentCount=0`
  - the routeability cull therefore could not fix those basins; it had no
    routeable alternate support component to preserve
  - `1364.867,-4374.000,26.109` and `1355.600,-4522.300,33.100` did have
    routeable support components, which is why only the exterior/underpass side
    changed
- Current rule:
  - keep the routeability fields and target config as analysis scaffolding
  - keep `postDetourCullAnchorTrappedComponents=false` in the default tile
    config until a branch improves route outcomes
  - if the routeability summary still says zero routeable support components
    across the hallway chain, stop rewiring targets and go earlier in the bake
    (`polymesh` / `contours` / corridor connectivity)
- Checked-in proof-only validation:
  - `tmp/bake-sweeps/og_4029_anchor_routeability_proof_only_qfix_manifest_only-20260524T012055Z/`
  - saved tile hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - focused slice stayed `7/7`
  - full `CriticalWalkLegs` stayed `17/23`
  - keep `1520.600,-4426.500,17.900` in `anchorStageManifestCoordsWow`, not
    `postDetourCullAnchorPolyStacksCoordsWow`

### 2026-05-24: pre-region coord split and negative neighbor-borrow experiment

- New surfaces:
  - `preRegionAnchorCoordsWow`
    - source-support / compact cleanup coords only
    - does not automatically change the final Detour anchor stack cull list
  - `borrowMissingAnchorSourceSupportFromNeighbors`
    - experiment-only fallback for no-source-support anchors
- Validation branch:
  - `og_4029_pre_region_anchor_split_15206-20260524T023316Z`
  - saved tile hash:
    `B196C738FF6ABA04B35055461112E8722AD0A2209A515100F8A9E53A6DD9AAA5`
  - focused slice stayed `7/7`
  - full raw-Detour sweep stayed `17/23`
- What it proved:
  - the hallway `1520.600,-4426.500,17.900` coord's earlier success came from
    pre-region/source cleanup, not from final Detour stack trimming
  - with the coord moved into `preRegionAnchorCoordsWow` only:
    - `1522.500,-4424.100,17.000` became green
    - `1523.800,-4425.900,17.100` moved from
      `finalDetour / lower_competitor_dominant` to
      `polymesh / upper_support_lost`
    - `1521.300,-4422.500,17.100` stayed blocked at
      `sourceSupport / no_source_support_probe`
- Negative borrow branch:
  - `og_4029_pre_region_anchor_borrow_15213-20260524T024038Z`
  - saved tile hash:
    `98D17DF9AE904BD1DC544729D4B96980361644C950AE9053F9F7D497E81CA3FE`
  - `1521.300,-4422.500,17.100` borrowed source support from
    `1522.500,-4424.100,17.000` and stopped failing at `sourceSupport`
  - this is not promotable:
    - direct `1518.2,-4419.8,17.1 -> full goal` collapsed to an immediate
      two-corner local path
- Checked-in default:
  - restore bake:
    `og_4029_pre_region_split_default_restore-20260524T024742Z`
  - tile hash restored to:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - keep `borrowMissingAnchorSourceSupportFromNeighbors=false`
  - keep shifted hallway coords manifest-only or pre-region-only until a branch
    improves actual route outcomes

### 2026-05-24: pre-poly contour preservation experiments on `1523.8`

- New native experiment surfaces:
  - `prePolyPreserveAnchorSupportCoordsWow`
    - preserves `RC_BORDER_VERTEX` points on source-backed support-band contours
      before `rcBuildPolyMesh()`
  - `prePolyUseRawAnchorSupportContoursWow`
    - swaps a source-backed support contour from simplified contour verts back
      to raw contour verts immediately before `rcBuildPolyMesh()`
- Baseline restore before these experiments:
  - tile hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Combined raw+preserve branch:
  - `og_4029_prepoly_raw_plus_preserve_1523_v1-20260524T143954Z`
  - saved tile hash:
    `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`
  - focused slice stayed `7/7`
  - full raw-Detour sweep stayed `17/23`
  - exact validation commands:
    - bake:
      `powershell -ExecutionPolicy Bypass -File tools/scripts/bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_raw_plus_preserve_1523_v1' -DataDir 'D:\wwow-bot\test-data'`
    - focused:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut"`
    - full:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" -- RunConfiguration.TestSessionTimeout=1200000`
  - What it proved:
    - `1523.800,-4425.900,17.100` moved from
      `polymesh / upper_support_lost` to
      `finalDetour / lower_competitor_dominant`
    - the source-backed support band survived contour/polymesh as a
      `19`-vertex contour, but final Detour still fragmented it into
      `14` non-routeable support-band candidates
    - hallway dead-end shape improved deeper to
      `1514.0,-4426.5,20.2`
  - Why it is not promotable yet:
    - `FinalRouteableSupportComponentCount` for the hallway chain stayed `0`
    - city / exit / exterior / underpass red count stayed unchanged at `17/23`
- Rejected composition:
  - `og_4029_pre_region_shifted_v2_plus_prepoly_raw_preserve_1523_v1-20260524T144503Z`
  - combining the best shifted `preRegionAnchorCoordsWow` list with the
    raw+preserve contour branch pushed `1523.8` back to
    `polymesh / upper_support_lost`
  - conclusion:
    - the earlier shifted pre-region proof window and the later raw+preserve
      contour branch are not composable as-is
- Rejected `maxVertsPerPoly=4` follow-up:
  - `og_4029_prepoly_raw_preserve_1523_maxverts4_v1-20260524T144728Z`
  - saved tile hash:
    `6530FC7C41C030557088AFED612BE667BB279F4BECB667F00C60CAB15E07F9C1`
  - focused slice regressed to `5/7`
  - exact focused failures:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
  - practical read:
    - a stage-backed `maxVertsPerPoly` increase really did make more
      hallway/exit/exterior anchors routeable in the manifest, but it
      reintroduced giant bridge polys and lost the intentional deck connector
      footprint
    - treat `maxVertsPerPoly=4` the same way we now treat `=6`: not
      promotable on tile `40,29`
- Checked-in default after this loop:
  - restore bake:
    `og_4029_restore_after_prepoly_iteration_20260524-20260524T145052Z`
  - tile hash restored to:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - keep both pre-poly contour experiment keys absent from checked-in config
  - next real fix surface:
    - local contour resimplification between the default `8`-vertex support
      contour and the raw-preserved `19`-vertex support contour, not another
      global `maxVertsPerPoly` increase

### 2026-05-24: local contour resimplification follow-up on `1523.8`

- Short research memo for this loop:
  - `docs/physics/RECAST_WOW_SIBLING_COMPARISON_2026_05_24.md`
- New native experiment surface:
  - `prePolyResimplifyAnchorSupportMaxError`
  - `prePolyResimplifyAnchorSupportMaxEdgeLen`
  - `prePolyResimplifyAnchorSupportTessellateWallEdges`
  - `prePolyResimplifyAnchorSupportTessellateAreaEdges`
  - local helper `ResimplifyRawAnchorSupportContours(...)`
- Critical correction:
  - the first same-day `og_4029_prepoly_resimplify_1523_mse13_v1` note was not
    a real contour-resimplify result
  - after raw restore, `contour.nverts = contour.nrverts`; the resimplify
    helper still skipped when `nrverts <= nverts`, so it never ran
- Corrected follow-up commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - real bug-fixed `1.3` branch:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_notess_v3' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_notess.json'`
  - `maxEdgeLen` isolation:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_edge24_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_edge24.json'`
  - tight-end upstream-range `1.1` branch:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse11_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse11.json'`
  - focused/full validation:
    - `og_4029_prepoly_resimplify_1523_mse13_notess_v3`
    - `og_4029_prepoly_resimplify_1523_mse11_v1`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_resimplify_bugfix_iteration_20260524' -DataDir 'D:\wwow-bot\test-data'`
- Corrected results:
  - diagnostic proof:
    - `[CONTOUR-ANCHOR-RAW] anchor=(1523.800,-4425.900,17.100) contour=1 region=8 verts=19->448`
  - bug-fixed `1.3` branch:
    - `448 -> 21`
    - hash:
      `F02666AFF5F064FC2999657718DC5B0084613F37C3DE4015DA339A43EC06959D`
    - focused:
      `7/7`
    - full:
      `17/23`
  - `maxEdgeLen=24` isolation:
    - still `448 -> 21`
    - same hash:
      `F02666AFF5F064FC2999657718DC5B0084613F37C3DE4015DA339A43EC06959D`
    - no extra tests run because the tile matched the already-validated
      bug-fixed `1.3` branch exactly
  - `1.1` branch:
    - `448 -> 22`
    - hash:
      `089DBEC002F4D8DF9BDBD091D32F659364F958C40F50E04F9D95357EDDD39FAD`
    - focused:
      `7/7`
    - full:
      `17/23`
    - same six remaining reds
- Manifest authority after the corrected loop:
  - `F02666...` branch:
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1522.500,-4424.100,17.000` ->
      no first-bad stage
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - `089DBE...` branch:
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1522.500,-4424.100,17.000` ->
      no first-bad stage
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - restored baseline `A01DEE...` branch:
    - `1523.800,-4425.900,17.100` still ->
      `finalDetour / lower_competitor_dominant`
- New practical recommendation:
  - upstream-style local resimplify is now bounded enough to stop churning:
    `1.3` and `1.1` both collapse to near-coarse `21/22`-vertex contours and
    keep the route set at `17/23`
  - `maxEdgeLen` is not the missing intermediate-contour lever here
  - next work should pivot to explicit local contour preservation / custom
    simplification or source-support / lower-competitor classification
- Restore after the corrected loop:
  - artifact:
    `tmp/bake-sweeps/og_4029_restore_after_resimplify_bugfix_iteration_20260524-20260524T231759Z`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

### 2026-05-25 UTC: local raw-window contour reinjection follow-up

- New targeted native surface in `TileWorker.cpp`:
  - `prePolyResimplifyAnchorSupportLocalPreserveRadius`
  - helper `InjectAnchorLocalRawVertices(...)`
  - refactor helper `FinalizeAnchorContourFlags(...)`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - radius `3.0` bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_localraw_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_localraw_r3.json'`
  - radius `6.0` bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_localraw_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_localraw_r6.json'`
  - focused/full validation rerun for both changed hashes:
    - `og_4029_prepoly_resimplify_1523_localraw_r3_v1`
    - `og_4029_prepoly_resimplify_1523_localraw_r6_v1`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_localraw_window_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Results:
  - radius `3.0` artifact:
    `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_localraw_r3_v1-20260525T001650Z/`
  - radius `3.0` contour/log facts:
    - `448 -> 46`
    - `[CONTOUR-ANCHOR-LOCAL-RAW] ... injectedRawVerts=25 preserveRadius=3.000`
    - hash:
      `F076A6FA0974755EA1F8384BB3C2154E064804EDD8604001030F6C6D637C2DC5`
    - focused:
      `7/7`
    - full:
      `17/23`
    - manifest:
      - `1523.800,-4425.900,17.100` still ->
        `finalDetour / lower_competitor_dominant`
      - `1522.500,-4424.100,17.000` still ->
        no first-bad stage
    - bake-side proof:
      - `1523.8` still logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=2 lowerFringeCulled=0 supportBandCandidates=2`
  - radius `6.0` artifact:
    `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_localraw_r6_v1-20260525T002119Z/`
  - radius `6.0` contour/log facts:
    - `448 -> 145`
    - `[CONTOUR-ANCHOR-LOCAL-RAW] ... injectedRawVerts=124 preserveRadius=6.000`
    - hash:
      `5997F2588CE58B979CE0CC8C199076F7C5A979284C2AEFFB837E99377A21E459`
    - focused:
      `7/7`
    - full:
      `17/23`
    - manifest regression:
      - `1522.500,-4424.100,17.000` ->
        `finalDetour / support_footprint_missed_anchor`
    - route-shape regression:
      - `orgrimmar_city_hallway_live_wall_stall_recovery` ended deeper at
        `(1514.0,-4426.5,20.2)`
    - bake-side proof:
      - `1523.8` still logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=14 lowerFringeCulled=0 supportBandCandidates=14`
- Practical read:
  - the local raw-window family is not promotable
  - a real intermediate contour by itself does not help if the final support
    footprint still misses the anchor
  - the next fix must target support-footprint / overlap behavior or move
    earlier into source-support classification; do not keep spending loops on
    "more local raw contour detail"
- Restore after this negative loop:
  - artifact:
    `tmp/bake-sweeps/og_4029_restore_after_localraw_window_iteration_20260525-20260525T002411Z/`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

### 2026-05-25 UTC: support-gap finalDetour follow-up

- New targeted native surface:
  - `postDetourCullAnchorPolyStacksSupportGap2D`
  - helper `GetDetourBoundsGap2D(...)`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_support_gap1_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_anchor_support_gap1.json'`
  - focused/full validation:
    - `og_4029_anchor_support_gap1_v1`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_support_gap1_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Results:
  - artifact:
    `tmp/bake-sweeps/og_4029_anchor_support_gap1_v1-20260525T005200Z/`
  - hash:
    `33F6D5DA3189CF1985120B247D23C9EF0C978995B10FF79C90A65DB5ABFE991D`
  - focused:
    `7/7`
  - full:
    `17/23`
  - key proof:
    - `1523.8` logged
      `[DT-ANCHOR-CULL-SKIP] ... lowerFringeCulled=2 ... bestSupportGap2D=0.300`
    - but the anchor still ended at
      `finalDetour / lower_competitor_dominant`
- Practical read:
  - the finalDetour cull can now touch a small lower fringe around `1523.8`
  - that fringe is not the dominant surviving basin, so this branch is another
    bounded negative result rather than a promotable fix
- Restore after this loop:
  - artifact:
    `tmp/bake-sweeps/og_4029_restore_after_support_gap1_iteration_20260525-20260525T005613Z/`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

### 2026-05-25 UTC: support-footprint negatives after support-gap

- New native/config surface:
  - `AnchorSupportBandTuning`
  - tile-local `anchorSourceSupportFloorSlackBelow`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - raw+preserve + gap bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raw_preserve_support_gap1_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raw_preserve_support_gap1.json'`
  - support-floor slack bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_support_floor_slack035_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_support_floor_slack035.json'`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_support_floor_slack_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Observed results:
  - raw+preserve + gap branch:
    - hash:
      `EFD2DCE534EFB2A9039447DFBE84C6F695701C507ED60DC0592C71752EB783FD`
    - focused:
      `7/7`
    - full:
      `17/23`
    - proof:
      - `1523.8` still logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=14 lowerFringeCulled=2 ... supportBandCandidates=14 ... bestSupportGap2D=0.300`
      - `1523.800,-4425.900,17.100` stayed
        `finalDetour / lower_competitor_dominant`
      - `1522.500,-4424.100,17.000` regressed to
        `finalDetour / support_footprint_missed_anchor`
  - `anchorSourceSupportFloorSlackBelow=0.35` branch:
    - hash:
      `CD5F1EB58003C4326D03B8A638EA154AF2855F3547520000AE39E45E59163FE0`
    - focused:
      `7/7`
    - full:
      `17/23`
    - proof:
      - `1523.8` logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=4 lowerFringeCulled=0 ... supportBandCandidates=4 ... bestSupportGap2D=-1.000`
      - `1523.800,-4425.900,17.100` stayed
        `finalDetour / lower_competitor_dominant`
      - `1522.500,-4424.100,17.000` regressed to
        `finalDetour / lower_competitor_dominant`
      - `1521.267,-4425.600,17.609` regressed to
        `finalDetour / lower_competitor_dominant`
- Current best interpretation:
  - the finalDetour support-gap surface is useful proof, but combining it with
    the raw+preserve contour branch still does not make the support shard
    dominant or routeable
  - widening the support floor below the sampled source-support Y is not a safe
    WoW geometry approximation at this anchor; it regresses sibling supports
    and reduces the useful support-band evidence
  - the remaining clean clue is still the exact-neighborhood support-footprint
    hole at `1523.8`: nearby source-backed support survives, but the anchor
    cell itself still falls into the wrong final basin
  - next branches should target exact-neighborhood support-footprint bridging /
    overlap or earlier source-support classification, not more generic
    `supportGap2D` or `supportFloorSlackBelow` widening

### 2026-05-25 UTC: raster support patch contour-loss proof

- New native/config surface:
  - `preRasterizeAnchorSupportPatchCoordsWow`
  - `preRasterizeAnchorSupportPatchHalfExtent`
  - helper `RasterizeAnchorSupportPatches(...)`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - raster patch only:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06.json'`
  - raster patch + raw+preserve:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_raw_preserve_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_raw_preserve.json'`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_raster_patch_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Observed results:
  - raster patch only:
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
      (unchanged from stable baseline)
    - focused:
      `7/7`
    - decisive proof:
      - `1523.8` moved to
        `median: supportCell=true`
        and
        `regions: supportCell=true`
      - but then fell back to
        `contours: supportCell=false`
        and still ended at
        `finalDetour / lower_competitor_dominant`
  - raster patch + raw+preserve:
    - hash:
      `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`
    - focused:
      `7/7`
    - full:
      `17/23`
    - decisive proof:
      - the earlier stage gain survived
        (`median/regions supportCell=true`)
      - `polymesh supportCount` returned to `16`
      - `finalDetour supportComponentCount` still stayed `0`
      - the saved tile snapped exactly back to the old raw+preserve branch
      - `1522.5` regressed again to
        `finalDetour / support_footprint_missed_anchor`
- Current best interpretation:
  - the `1523.8` support footprint is recoverable before contours
  - the next real loss is now proven to be `rcBuildContours(...)`
  - carrying the raw contour later is still insufficient; it only recreates the
    old non-routeable `52D99...` shard branch
  - next work should focus on local contour-builder preservation /
    simplification for a source-backed recovered footprint, not more
    finalDetour or generic support-band tuning

### 2026-05-25 UTC: raster patch + contour-band boundary carry negative

- Exact upstream motivation before touching WWoW code:
  - official Recast says `rcBuildContours(...)` traces raw region outlines
    exactly, while the simplified contour only keeps mandatory portal/area
    vertices plus error/tessellation-driven inserts:
    https://recastnav.com/group__recast.html
  - upstream `simplifyContour(...)` seeds simplified vertices from region/area
    transitions first, then falls back to a coarse seed before
    Ramer-Douglas-Peucker-style deviation splitting:
    https://raw.githubusercontent.com/recastnavigation/recastnavigation/main/Recast/Source/RecastContour.cpp
- Active task:
  - test whether a contour-local carry surface can preserve only the recovered
    source-support band boundary near `1523.800,-4425.900,17.100`, instead of
    reusing the earlier full raw-contour carry
- Pass result:
  - `delta shipped; the new contour-local surface is a bounded negative. It did
    not move 1523.8 off finalDetour, and because it touched multiple same-band
    contours in the patch window it reintroduced the old deck bridge / trim
    regressions. The next retry in this family must isolate the single
    recovered contour/region touching the patch neighborhood.`
- New native/config surface:
  - `prePolyResimplifyAnchorSupportBandBoundaryRadius`
  - helper `InjectAnchorSupportBandBoundaryVertices(...)`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - focused bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_seed_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_seed_r3.json'`
  - changed-hash proof:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_seed_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - mandatory full-rerun rebake after the changed hash:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_seed_r3_v1_fullrerun' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_seed_r3.json'`
  - full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_seed_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_boundary_seed_iteration_fullrerun_20260525' -DataDir 'D:\wwow-bot\test-data'`
  - restored hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
- Artifacts:
  - focused bake:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_raster_support_patch06_boundary_seed_r3_v1-20260525T024153Z\`
  - mandatory full-rerun bake:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_raster_support_patch06_boundary_seed_r3_v1_fullrerun-20260525T024605Z\`
  - restore:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_restore_after_boundary_seed_iteration_fullrerun_20260525-20260525T024951Z\`
- Observed results:
  - changed hash:
    `E58B0DF11E71196123A377094B4A41710238591B8D454352BDF93B7C825D424F`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - focused:
    `3/7`
  - full:
    `20/23`
  - decisive bake-log proof:
    - `[HF-ANCHOR-SUPPORT-PATCH] map=1 tile=40,29: rasterized 1 support patch(es)`
    - raw restore hit three contours:
      - contour `1` region `8` `13 -> 226`
      - contour `3` region `7` `11 -> 158`
      - contour `4` region `19` `3 -> 10`
    - band-boundary carry then injected boundary verts on two of those
      contours:
      - contour `1` region `8` injected `3`
      - contour `3` region `7` injected `2`
    - final re-simplified contour sizes were:
      - contour `1` region `8` `226 -> 18`
      - contour `3` region `7` `158 -> 13`
      - contour `4` region `19` `10 -> 3`
  - bad-anchor stage summary from the saved manifest:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - focused regressions:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons in the crop
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
      clipped the known exterior steep incline and rope-line support blockers
  - full `CriticalWalkLegs` failures:
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
- Practical read:
  - this branch did not change the anchor-stage answer at `1523.8`; it stayed
    `finalDetour / lower_competitor_dominant`
  - the new surface proved the current `boundaryRadius=3.0` shape is too wide
    semantically, not geometrically: it preserved the wrong same-band contour
    set, not just the intended recovered footprint
  - if this family is retried, the next branch must isolate the single
    recovered contour/region that actually touches the raster-patch
    neighborhood and must not reinject support-band boundary vertices across
    every same-band contour in the local window

### 2026-05-25 UTC: single-contour selector follow-up negative

- Active task:
  - keep the raster patch plus contour-local raw restore / resimplify /
    preserve loop, but isolate exactly one support-band contour at a time
    instead of reopening every same-band contour near `1523.8`
- Pass result:
  - `delta shipped; the selector surface is a useful bounded proof, but both
    isolated contour choices are negative. Selecting only the anchor-containing
    contour or only the nearest non-containing contour still leaves 1523.8 at
    finalDetour and still reproduces the bad deck bridge / trim /
    static-blocker profile.`
- New native/config surface:
  - `AnchorSupportContourSelectionMode`
  - `prePolySupportContourSelectionMode`
  - legacy alias:
    `prePolySelectAnchorContainingSupportContourOnly=true` still maps to
    `AnchorContaining`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - anchor-containing bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_seed_anchoronly_r3.json'`
  - anchor-containing hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - anchor-containing focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - anchor-containing full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - nearest-non-containing bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3.json'`
  - nearest-non-containing hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - nearest-non-containing focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - nearest-non-containing full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
- Artifacts:
  - anchor-containing:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1-20260525T042822Z\`
  - nearest-non-containing:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1-20260525T043821Z\`
- Observed results:
  - anchor-containing hash:
    `5FE8640E4B7D756F74DBCA47952345F8A06507C6C81BA330E400092228399340`
  - nearest-non-containing hash:
    `84C09EFE50E2E04114DCF3A4F218A1DBF29E4E6F8776680CC966B47D2ADFB856`
  - both focused results:
    `3/7`
  - both full results:
    `20/23`
  - shared focused failures:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons in the crop
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - shared full failures:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
- Decisive selector proof:
  - candidate set stayed the same on both branches:
    - `contour 1 / region 8 verts=226 containsAnchor=0 closestDistance2D=0.836`
    - `contour 3 / region 7 verts=158 containsAnchor=1 closestDistance2D=0.200`
    - `contour 4 / region 19 verts=10 containsAnchor=0 closestDistance2D=1.997`
  - anchor-containing branch selected only `contour 3 / region 7`:
    - raw restore:
      `11 -> 158`
    - injected boundary verts:
      `2`
    - re-simplified:
      `158 -> 13`
  - nearest-non-containing branch selected only `contour 1 / region 8`:
    - raw restore:
      `13 -> 226`
    - injected boundary verts:
      `3`
    - re-simplified:
      `226 -> 18`
  - despite isolating those contours cleanly, both branches still kept
    `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
- Stage summary for the important anchors stayed identical on both branches:
  - `1522.500,-4424.100,17.000` -> no `firstBadStage`
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1521.267,-4425.600,17.609` -> no `firstBadStage`
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`
- Practical read:
  - the earlier boundary-carry regression was not just "too many contours"
  - isolating the literal anchor-containing contour does not fix it
  - isolating the nearest non-containing contour does not fix it either
  - the next contour-stage retry should change the local preservation /
    simplification shape itself, not just swap which one of the current raw
    contours gets preserved
- Restore after this selector loop:
  - command:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_single_contour_selector_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

### 2026-05-25 UTC: support-band-local contour preserve follow-up

- Active task:
  - keep the raster patch plus anchor-containing contour fixed, but change the
    preserved shape itself by carrying only the raw contour verts that remain
    inside the recovered support band within a local anchor window around
    `1523.8`
- Pass result:
  - `delta shipped; the support-band-local preserve surface is a bounded
    negative. It created a richer anchor-containing contour than the
    boundary-only branch, but still left 1523.8 at finalDetour and still
    reproduced the same bad deck / static-blocker profile.`
- New native/config surface:
  - `InjectAnchorSupportBandLocalRawVertices(...)`
  - `prePolyResimplifyAnchorSupportBandLocalPreserveRadius`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_band_local_anchoronly_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_band_local_anchoronly_r6.json'`
  - changed hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_band_local_anchoronly_r6_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_band_local_anchoronly_r6_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
- Artifacts:
  - changed tile:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_raster_support_patch06_band_local_anchoronly_r6_v1-20260525T165251Z\`
  - restore:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_restore_after_band_local_iteration_20260525-20260525T165615Z\`
- Observed results:
  - hash:
    `B9E24E82A964DDFD4E7EB10B8401CFB645681DB2EF0ECAF3D784D26B7AA2981A`
  - focused:
    `3/7`
  - full:
    `20/23`
  - decisive contour proof:
    - selector still isolated `contour 3 / region 7`
    - raw restore:
      `11 -> 158`
    - local support-band preserve produced a richer candidate contour:
      `158 -> 34`
    - new log:
      `[CONTOUR-ANCHOR-BAND-LOCAL] ... preservedSupportBandRawVerts=23 preserveRadius=6.000`
    - but `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - shared focused failures stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - shared full failures stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - important anchor stages stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
- Practical read:
  - boundary-only carry was too sparse, full local-raw carry was too broad, and
    this support-band-local midpoint is still not enough
  - the current missing lever is no longer "pick a denser local arc on
    region 7"
  - the next serious retry should move earlier into `rcBuildContours(...)` or
    a different contour-builder shape, not more post-contour reinjection on the
    same selected contour
- Restore after this loop:
  - command:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_band_local_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

### 2026-05-25 UTC: boundary pre-seed contour follow-up

- Active task:
  - keep the raster patch, raw restore, anchor-containing contour selection,
    and local resimplify fixed, but move the recovered support-band boundary
    into the local simplifier's initial seed phase instead of reinserting those
    verts after simplification
- Pass result:
  - `delta shipped; the earlier boundary-preseed surface is a bounded
    negative. It fired on the intended recovered contour, but still collapsed
    back to the same 13-vertex shape and reproduced the same 3/7 focused and
    20/23 full regression profile as the later boundary-carry family.`
- New native/config surface:
  - `SimplifyAnchorContour(..., mandatorySeedMask)`
  - `BuildAnchorSupportBandBoundaryVertexMask(...)`
  - `prePolyResimplifyAnchorSupportBandBoundarySeedRadius`
  - `[CONTOUR-ANCHOR-BAND-SEED]`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3.json'`
  - changed hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_boundary_preseed_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Artifacts:
  - changed tile:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1-20260525T173241Z\`
  - restore:
    `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_restore_after_boundary_preseed_iteration_20260525-20260525T173619Z\`
- Observed results:
  - hash:
    `EB6F72B9E86E550DB277BA767D2BCB07D5C99337E729191B0C52378CF487DADC`
  - focused:
    `3/7`
  - full:
    `20/23`
  - decisive contour proof:
    - selector still isolated `contour 3 / region 7`
    - raw restore:
      `11 -> 158`
    - upstream-style early seeding really fired:
      `[CONTOUR-ANCHOR-BAND-SEED] ... seededBoundaryVerts=4 seedRadius=3.000`
    - but the resimplifier still collapsed back to the same coarse candidate:
      `158 -> 13`
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - shared focused failures stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - shared full failures stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - important anchor stages stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
- Practical read:
  - moving the same recovered support-band boundary endpoints earlier into the
    local contour-builder seed phase was not enough
  - on this family, seed timing alone still snaps back to the same 13-vertex
    region-7 contour and the same bad deck / hallway-exit / underpass route
    profile
  - the next serious retry should not spend another loop only on boundary-seed
    timing; it needs a different contour-builder shape or genuinely earlier
    source/vertical classification work

### 2026-05-25 UTC: existing-simplified local support-band carry follow-up

The next contour-stage retry removed the resimplify step entirely and worked on
the current `rcBuildContours()` output directly.

- Upstream motivation before touching WWoW code:
  - Recast's `simplifyContour(...)` copies raw contour points into the
    simplified contour while carrying raw indices until the final flag rewrite,
    so a same-order XYZ remap on the existing simplified contour is a valid
    bounded way to splice raw verts back in without rerunning simplification:
    https://raw.githubusercontent.com/recastnavigation/recastnavigation/main/Recast/Source/RecastContour.cpp
- New local surface:
  - `BuildAnchorContourRawIndexView(...)`
  - `CarryLocalRawVerticesIntoExistingAnchorSupportContours(...)`
  - config keys:
    `prePolyCarryAnchorSupportCoordsWow`,
    `prePolyCarryAnchorSupportBandLocalRadius`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_carry_local_band_r4_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_carry_local_band_r4.json'`
  - changed hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_carry_local_band_r4_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_carry_local_band_r4_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_carry_local_band_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Artifact + hash:
  - changed tile artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_carry_local_band_r4_v1-20260525T193344Z/`
  - restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_carry_local_band_iteration_20260525-20260525T193752Z/`
  - saved tile hash:
    `3D3BEA0EFB858DBC0B4D72C501CCE50864CE4A7A8F3D2DA8280A2356ECAD97E3`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Decisive proof:
  - the branch did avoid resimplify and still produced a real local carry on
    the existing simplified contours:
    - `contour 1 / region 8 verts=13->42 injectedSupportBandRawVerts=29`
    - `contour 3 / region 7 verts=11->31 injectedSupportBandRawVerts=20`
    - `contour 4 / region 19 verts=3->10 injectedSupportBandRawVerts=7`
  - the tile did not fall back to the old shard hash, but it still regressed
    into the same focused deck family:
    - focused:
      `3/7`
    - full:
      `20/23`
  - shared focused failures stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - shared full failures stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - `1523.800,-4425.900,17.100` still ended at
    `finalDetour / lower_competitor_dominant`
- Stage summary for the important anchors stayed:
  - `1522.500,-4424.100,17.000` -> no `firstBadStage`
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1521.267,-4425.600,17.609` -> no `firstBadStage`
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`
- Practical conclusion:
  - the resimplify step was not the only thing pushing this family into the
    bad deck/bridge profile
  - even a direct local raw carry on top of the existing simplified contours
    still reintroduces the same 3/7 focused and 20/23 full regression family
    while leaving `1523.8` at `finalDetour`
  - if contour work continues, the next retry must be narrower than this
    multi-contour local carry or move earlier than contours entirely; do not
    assume "skip re-simplify" is enough by itself

### 2026-05-25 UTC: anchor-containing no-resimplify carry follow-up

The next retry kept the same no-resimplify carry surface but isolated the
single anchor-containing contour instead of reopening every same-band contour in
the local window.

- Exact commands:
  - bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_carry_local_band_anchoronly_r4.json'`
  - changed hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_carry_local_band_anchoronly_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Artifact + hash:
  - changed tile artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1-20260525T195224Z/`
  - restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_carry_local_band_anchoronly_iteration_20260525-20260525T195513Z/`
  - saved tile hash:
    `1932EC1BC322393040870F3293C9CF9B9EA6CCBB640974A3595B87CC4D5839B8`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Decisive proof:
  - selector diagnostics still isolated the same contour:
    - `contour 1 / region 8 verts=226 containsAnchor=0 closestDistance2D=0.836`
    - `contour 3 / region 7 verts=158 containsAnchor=1 closestDistance2D=0.200`
    - `contour 4 / region 19 verts=10 containsAnchor=0 closestDistance2D=1.997`
  - this branch reopened and preserved only that anchor-containing contour:
    - `contour 3 / region 7 verts=11->31 injectedSupportBandRawVerts=20 preserveRadius=4.000`
    - `preservedBorderVerts=31`
  - despite that narrower scope, route results stayed on the same regression
    family:
    - focused:
      `3/7`
    - full:
      `20/23`
  - `1523.800,-4425.900,17.100` still ended at
    `finalDetour / lower_competitor_dominant`
- Stage summary for the important anchors stayed:
  - `1522.500,-4424.100,17.000` -> no `firstBadStage`
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1521.267,-4425.600,17.609` -> no `firstBadStage`
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`
- Practical conclusion:
  - the broad multi-contour carry was not the real reason this family failed
  - even when only the anchor-containing contour is reopened and preserved, the
    tile still collapses to the same deck/static-blocker/full-route regression
    profile
  - that closes off the remaining "post-contour but narrower" retry; the next
    serious branch needs to move the local support mask into the actual
    `rcBuildContours(...)` simplifier or earlier source/vertical staging

### 2026-05-25 UTC: contour-build simplify-time seed follow-up

WWoW then closed that exact "move it into the real simplifier" retry by
seeding the same local support-band mask inside upstream Recast's
`simplifyContour(...)` during `rcBuildContours()`.

- New local surface:
  - `rcAnchorContourSimplifyOverride`
  - `rcSetContourSimplifyAnchorOverrides(...)`
  - `rcClearContourSimplifyAnchorOverrides()`
  - `BuildContourSimplifyAnchorOverrides(...)`
  - config keys:
    `contourBuildSeedAnchorSupportCoordsWow`,
    `contourBuildSeedAnchorSupportBandLocalRadius`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4.json'`
  - changed hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - full `CriticalWalkLegs`:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_contourbuild_seed_local_anchoronly_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
  - restored hash:
    `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
- Artifact + hash:
  - changed tile artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1-20260525T200739Z/`
  - restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_contourbuild_seed_local_anchoronly_iteration_20260525-20260525T201053Z/`
  - saved tile hash:
    `C0873DE50193A03921A761F75C278B82B001100B2E58BFCF4721DA8D827A5357`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Decisive proof:
  - upstream simplify-time seeding really fired on the intended recovered
    contour:
    `[CONTOUR-BUILD-ANCHOR-SEED] region=7 rawVerts=158 simplifiedVerts=33 seededSupportBandRawVerts=26 matchedOverrides=1`
  - selector diagnostics still isolated the same target:
    - `contour 1 / region 8 verts=226 containsAnchor=0 closestDistance2D=0.836`
    - `contour 3 / region 7 verts=158 containsAnchor=1 closestDistance2D=0.200`
    - `contour 4 / region 19 verts=10 containsAnchor=0 closestDistance2D=1.997`
  - the later border-preserve pass still only touched that same contour:
    `[CONTOUR-ANCHOR-PRESERVE] anchor=(1523.800,-4425.900,17.100) contour=3 region=7 preservedBorderVerts=33`
  - the stage manifest tightened the remaining read on `1523.8`:
    - `contours supportCandidateCount=1`
    - `polymesh supportCandidateCount=2`
    - but `supportContainsAnchorProjection=false` throughout
    - `finalDetour supportCount=0`
    - final answer still:
      `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - focused/full stayed on the same regression family:
    - focused:
      `3/7`
    - full:
      `20/23`
- Stage summary for the important anchors stayed:
  - `1522.500,-4424.100,17.000` -> no `firstBadStage`
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1521.267,-4425.600,17.609` -> no `firstBadStage`
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`
- Practical conclusion:
  - moving the same recovered support-band mask into the actual upstream
    `rcBuildContours(...)` simplifier is still not enough
  - the newest proof is no longer "support vanished too early"; it is that the
    surviving support still misses the exact final footprint overlap at
    `1523.8`
  - this closes the most plausible contour-builder timing retry; the next
    serious fallback should be a research-backed local `ch` override or another
    genuinely earlier source/vertical classification branch, not more
    seed-timing churn on the same support mask
