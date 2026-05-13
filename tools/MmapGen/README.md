# MmapGen

In-tree generator for the WoW navigation tiles (`.mmap` / `.mmtile`) consumed
by `Exports/Navigation` and `Services/PathfindingService`.

> **Why this exists:** static collision (city supports, gangplanks, bonfires,
> elevators, zeppelins) and capsule-correct walkable rules belong in the
> navmesh, not in 5,600 lines of managed query-time repair. Owning the
> generator in-tree is the foundation of the pathfinding overhaul described
> in [docs/physics/PATHFINDING_OVERHAUL.md](../../docs/physics/PATHFINDING_OVERHAUL.md).

## Status

| | |
|---|---|
| Imported from | [vmangos/core](https://github.com/vmangos/core) `development` branch, 2026-05-06 — see [NOTICE.md](NOTICE.md) |
| Build status | Scaffold only. CMake target exists; full bring-up is Phase 2. |
| Owns | navmesh tile generation for the runtime to consume |
| Replaces | `D:/MaNGOS/source/bin/MoveMapGenerator.exe` (external) once Phase 2 lands |

## Layout

```
tools/MmapGen/
  CMakeLists.txt           # top-level cmake (this is the entry point)
  build-mmapgen.ps1        # wrapper: configure + build
  config.json              # per-map / per-tile generator config (capsule, walkable*, etc.)
  offmesh.txt              # off-mesh connections (zeppelins, elevators, gangplanks, ...)
  NOTICE.md                # provenance + GPL license obligations
  UPSTREAM_LICENSE_VMANGOS # verbatim vmangos LICENSE for attribution
  README.md, AGENTS.md, CLAUDE.md
  contrib/mmap/            # the generator (vmangos)
  dep/                     # Recast, Detour, g3dlite, zlib, loadlib, headers
  src/                     # vmap reader, shared, framework (subset of vmangos src/)
```

The `contrib/`, `dep/`, `src/` layout mirrors vmangos's repo so the relative
`../../src/...` includes inside `contrib/mmap/CMakeLists.txt` continue to
resolve. We are free to flatten or prune as we make this our own.

## Build (when Phase 2 wires it up)

```powershell
# Configure + build Release x64
.\tools\MmapGen\build-mmapgen.ps1

# Or hand-run cmake
cmake -B tools\MmapGen\build -S tools\MmapGen -G "Visual Studio 17 2022" -A x64
cmake --build tools\MmapGen\build --config Release --target MmapGen
```

The root `CMakeLists.txt` includes `tools/MmapGen` via `add_subdirectory(...)`,
so MmapGen also builds as part of any whole-repo cmake configure.

## Runtime (when Phase 2 wires it up)

```powershell
# Generate full nav data for maps 0 and 1
Push-Location D:\MaNGOS\data
& "$repo\tools\MmapGen\build\Release\MmapGen.exe" 0 --threads 8 --silent --offMeshInput "$repo\tools\MmapGen\offmesh.txt" --configInputPath "$repo\tools\MmapGen\config.json"
& "$repo\tools\MmapGen\build\Release\MmapGen.exe" 1 --threads 8 --silent --offMeshInput "$repo\tools\MmapGen\offmesh.txt" --configInputPath "$repo\tools\MmapGen\config.json"
Pop-Location

# Or a focused single-tile regen for the OG dock
& "$repo\tools\MmapGen\build\Release\MmapGen.exe" 1 --tile 40,29 --threads 1 --silent --offMeshInput "$repo\tools\MmapGen\offmesh.txt" --configInputPath "$repo\tools\MmapGen\config.json"
```

Every regeneration must be followed by:

```powershell
dotnet run --project tools\NavDataAudit\NavDataAudit.csproj --configuration Release -- D:/MaNGOS/data --map 1 --build-log <focused-log> --write-manifest tmp/test-runtime/results-navigation/<manifest>.json
```

## Authoring

Two files in this directory drive what gets baked into tiles:

- [config.json](config.json) — agent capsule (radius, height), Recast cell
  metrics, walkable rules. **Tauren Male is the largest WoW capsule** and is
  the default; do not regress to `agentRadius=0.2` (that was vmangos's stock
  default and produces tiles small races fit through but Tauren do not).
  Focused diagnostics can add `debugStageCropWow` to a per-tile block; with
  `--debug` the generator writes heightfield, compact-heightfield, and contour
  CSVs for that WoW-space crop beside the usual OBJ/debug files. Per-tile
  `maxVertsPerPoly` is honored and clamped to Recast/Detour's valid range.
- [offmesh.txt](offmesh.txt) — explicit nav-graph edges Recast cannot infer:
  zeppelin gangplanks, elevator shafts, teleport pads, fall drops with no
  walkable slope.

Adding a transport is a one-line change. Adding city-collision is a code
change in `contrib/mmap/src/TileWorker.cpp` /
`MapBuilder::buildGameObject(...)` — never a runtime workaround.

## See also

- [docs/physics/PATHFINDING_OVERHAUL.md](../../docs/physics/PATHFINDING_OVERHAUL.md) — master plan, ADR, sequencing, freeze contract
- [docs/physics/MMAP_FORMAT.md](../../docs/physics/MMAP_FORMAT.md) — tile/wrapper format the loader accepts
- [docs/physics/CPP_PATHFINDING_SERVICE_PLAN.md](../../docs/physics/CPP_PATHFINDING_SERVICE_PLAN.md) — native PathfindingService rewrite + dtCrowd evaluation
- [docs/physics/MMAP_NAVMESH_GENERATION.md](../../docs/physics/MMAP_NAVMESH_GENERATION.md) — historical (pre-overhaul) generator notes
- [tools/NavDataAudit/](../NavDataAudit/) — the proof gate every regeneration must pass
