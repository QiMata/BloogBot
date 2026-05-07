# MMAP Navmesh Generation Audit

This note is for Crossroads -> Undercity long-pathing and any other route that
depends on large-player clearance through city geometry.

## Required Navmesh Shape

The pathfinding mesh must be generated for Tauren Male clearance, not repaired
after the fact by route-specific runtime overlays.

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

Then run the route-level clearance gate before any live Crossroads ->
Undercity validation:

```powershell
$env:WWOW_DATA_DIR='D:\MaNGOS\data'
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers.trx" --results-directory tmp/test-runtime/results-pathfinding
```

Do not use `--no-build` for this route gate after editing PathfindingService
or test code; a no-build rerun is only appropriate after data-only changes.

As of 2026-05-01 this route gate is green on `D:\MaNGOS\data` after restoring
the focused GO-axis route tiles and adding generic affordance-based corridor
repair for steep or blocked Detour legs. The production repair is allowed only
because it is route-agnostic: it asks native navigation to classify suspicious
uphill segments and resamples a local corridor window without Orgrimmar blocker
coordinates, clearance cylinders, route-specific waypoints, or live-position
guards. The earlier PathfindingService static-clearance pass that hardcoded
Orgrimmar clearance zones remains invalid and must not be restored.

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

The full map `0` and `1` data set is now compatible with the focused route
gate. Focused live Crossroads -> Undercity evidence is the next proof step.
