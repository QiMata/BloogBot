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
dotnet run --project tools/NavDataAudit/NavDataAudit.csproj -- D:/MaNGOS/data
```

The audit checks:

- `config.json` contains Tauren Male `agentRadius`, `agentHeight`, and Recast
  cell settings for the audited map.
- Orgrimmar `.mmtile` Detour headers were actually generated with Tauren-sized
  `walkableRadius` and `walkableHeight`.
- `temp_gameobject_models` and `gameobject_spawns.json` contain model-backed
  Orgrimmar spawns.
- `map1_build.log` proves the audited Orgrimmar tiles loaded baked GO meshes.

Current audit result before regeneration:

- GO evidence passes for the Orgrimmar route tiles.
- Radius/height evidence fails: Orgrimmar tiles still report
  `walkableRadius=0.2` and `walkableHeight=1.5`.

Example focused tile generation command after the generator/config are fixed:

```powershell
Push-Location D:/MaNGOS/data
D:/MaNGOS/source/build/contrib/mmap/MoveMapGenerator.exe 1 --tile 28,40 --threads 1 --silent --configInputPath config.json
Pop-Location
```

Regenerate all audited Orgrimmar tiles (`28,39` through `30,41`) before
re-running deterministic long-route tests. Regenerate the full maps `0` and `1`
before treating live Crossroads -> Undercity as final evidence.
