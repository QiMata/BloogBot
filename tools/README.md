# tools/ — Standalone CLIs & navmesh utilities

One-off command-line tools that support the build, the navmesh/physics pipeline,
and test-data maintenance. They are **not** part of the runtime bot stack and are
not referenced by the Services layer.

> **Agent rules:** managed tools follow `.github/instructions/tools.instructions.md`;
> native (C++) tools follow `.github/instructions/native.instructions.md`.
> `MmapGen` has its own `CLAUDE.md` / `AGENTS.md`.

## Index

| Tool | What it does |
|------|--------------|
| `MmapGen/` | In-tree generator for WoW navigation tiles (`.mmap` / `.mmtile`) consumed by `Exports/Navigation` + `Services/PathfindingService`. Native (Recast). Has its own README/CLAUDE.md. |
| `GameObjectExporter/` | Queries the VMaNGOS DB for gameobject spawns and exports JSON for the navmesh bake and the runtime `SceneCacheBuilder`. Supports named world-state variants. |
| `NavDataAudit/` | Parses and audits `.mmap`/`.mmtile` integrity (magic/version/headers, capsule constants) to catch malformed bake output. |
| `NavMeshPhysicsValidator/` | Runs the runtime physics classifier (`ClassifyPathSegmentAffordance`) over sampled paths through a navmesh tile and reports polygon-edges where the bake's walkability disagrees with full physics. JSON report + heat-map. |
| `PathPhysicsProbe/` | Drives `Navigation.dll` / `Physics.dll` to classify the physics affordance of each segment on a path; localizes the first bake-mesh-vs-runtime disagreement. Implements the `mmo-physics-pathing-probe` skill contract. Has its own README. |
| `MmapVisualize/` | Parses a `.mmtile` into a Wavefront `.obj` of walkable detail-mesh triangles (optionally grafting in VMap collision geometry) for visual inspection. |
| `WwowRecastBridge/` | Bridges the managed `PathfindingService.Repository` to the native Recast/Detour stack for tile generation and queries. |
| `RecordingMaintenance/` | Maintenance CLI for recorded physics/path test data (`summary`, `write-sidecars`, `compact`, `capture`, `cleanup-output-copies`). Reuses helpers from `Tests/Navigation.Physics.Tests`. |

## Notes

- Most tools target the pathfinding/physics overhaul. Read
  [docs/physics/README.md](../docs/physics/README.md) before changing how they
  read or write navmesh data — the pathfinding stack is under an architectural
  freeze.
- `RecordingMaintenance` references a **test** project (`Navigation.Physics.Tests`)
  on purpose, to reuse its recording helpers. That is the one place a non-test
  project depends on `Tests/`; see `docs/agent-readability-audit.md`.
