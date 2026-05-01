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

The currently available local generator source under `D:/MaNGOS/source` does
not contain the historical gameobject-spawn bake patch, even though
`D:/MaNGOS/data/map1_build.log` proves a patched generator was previously used.
Restore that patch before regenerating tiles; otherwise the next generation
will drop the object geometry.

Refresh the spawn file with the read-only exporter:

```powershell
dotnet run --project tools/GameObjectExporter/GameObjectExporter.csproj -- "Server=127.0.0.1;Database=mangos;User=root;Password=root;" "D:/MaNGOS/data/gameobject_spawns.json"
```

## Audit Command

Run the repo-owned audit before and after any mmap regeneration:

```powershell
dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data
```

The audit checks:

- `config.json` contains Tauren Male `agentRadius`, `agentHeight`, and Recast
  cell settings for the audited map.
- Audited `.mmtile` Detour headers were actually generated with Tauren-sized
  `walkableRadius` and `walkableHeight`.
- `temp_gameobject_models` and `gameobject_spawns.json` contain model-backed
  spawns in the audited tile set.
- The selected build log proves the audited tiles loaded or marked baked GO
  geometry. Use `--build-log <path>` when auditing a focused regeneration log
  instead of the default `map<N>_build.log`.

MaNGOS writes tile files as `mapId + tileY + tileX`; for example generator
tile `28,40` is `mmaps/0014028.mmtile`.

Current focused audit result after rebuilding route tiles on
2026-05-01:

- `D:/MaNGOS/data/config.json` has map `1` set to `agentRadius=1.0247`,
  `agentHeight=2.625`, `walkableRadius=4`, and `walkableHeight=11`.
- Audited Orgrimmar route tiles `28,39` through `30,41` pass with Detour
  headers `walkableRadius=1.0247` and `walkableHeight=2.625`.
- Audited Undercity arrival tiles on map `0`, `27,30` through `30,32`, pass
  with Detour headers `walkableRadius=1.0247` and `walkableHeight=2.625`.
- GO input evidence still passes, with `930` model mappings, `868` modeled
  spawns in the Orgrimmar tile set, and `772` modeled spawns in the Undercity
  arrival tile set.

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

Regenerate the full maps `0` and `1` before treating live Crossroads ->
Undercity as final evidence. Focused Orgrimmar tiles are useful for deterministic
route iteration, but live evidence should come from a complete compatible data
set.
