# AGENTS.md — tools/MmapGen

Repo-local guide for agents working in `tools/MmapGen/`. Read this before
editing anything here. Pair with [CLAUDE.md](CLAUDE.md) (same content, same
contract).

## What this project is

The in-tree mmap/navmesh generator. Produces `.mmap` and `.mmtile` files that
`Exports/Navigation` / `Services/PathfindingService` load at runtime.

Sources were imported from [vmangos/core](https://github.com/vmangos/core) on
2026-05-06 and **unlinked** from upstream (no submodule, no remote tracking).
This is now ours. Edit it like any other repo code. See [NOTICE.md](NOTICE.md)
for license (GPL v2) and provenance.

## What this project replaces

`D:/MaNGOS/source/bin/MoveMapGenerator.exe` — an externally-patched
`MoveMapGenerator` that lives outside this repo and that no agent can audit.
Once Phase 2 of the overhaul lands, MmapGen is the only generator we use.

## Why the rest of the repo cares

Five years of `Services/PathfindingService` work has accumulated 5,600+ lines
of managed query-time repair logic to compensate for tiles that don't bake
static GameObjects, transports, or capsule-correct walkable rules. The
overhaul direction is: **fix the mesh, not the query**. MmapGen is the lever.

See [docs/physics/PATHFINDING_OVERHAUL.md](../../docs/physics/PATHFINDING_OVERHAUL.md)
for the master plan, freeze contract, sequencing, and exit criteria.

## Read before editing

1. [NOTICE.md](NOTICE.md) — provenance + license.
2. [docs/physics/PATHFINDING_OVERHAUL.md](../../docs/physics/PATHFINDING_OVERHAUL.md)
   — the why and the sequence.
3. [docs/physics/MMAP_FORMAT.md](../../docs/physics/MMAP_FORMAT.md) — the
   tile/wrapper format the loader expects. Output drift breaks the runtime.
4. [docs/physics/MMAP_NAVMESH_GENERATION.md](../../docs/physics/MMAP_NAVMESH_GENERATION.md)
   — historical generator notes, capsule constants, audit workflow.

## Core rules

- **Output format is a contract.** `Navigation.dll`'s strict loader accepts
  exactly: `MMAP_MAGIC=0x4D4D4150`, `MMAP_VERSION=6`, `DT_NAVMESH_VERSION=7`,
  64-bit `dtPolyRef` (`salt=16, tile=28, poly=20`), 20-byte `MmapTileHeader`.
  If a generator change breaks any of these, fix the generator, not the
  loader. See `docs/physics/MMAP_FORMAT.md` and `docs/physics/DETOUR_UPGRADE_BASELINE.md`.
- **Tauren Male is the largest WoW capsule and is the default.** Continent
  maps must be generated with `agentRadius=1.0247`, `agentHeight=2.625`,
  `walkableRadius=4`, `walkableHeight=11`, `walkableClimb=1.8`. Do not regress
  to vmangos's stock `agentRadius=0.2`.
- **Static GameObjects belong in the mesh, not in runtime overlays.** If a
  bonfire / city support / palm / pillar / banner / lip clips a route, the
  fix is in `contrib/mmap/src/TileWorker.cpp` /
  `MapBuilder::buildGameObject(...)` — never a managed-side repair, never a
  hardcoded blocker coordinate, never a route-pack.
- **Transports belong in `offmesh.txt`.** Zeppelins, gangplanks, elevators,
  boats, teleport pads. One line per direction (or use the bidirectional
  flag). Do not hand-code `ApproachPosition` / `BoardingPosition` constants
  in BotRunner to compensate for missing off-mesh edges.
- **Per-tile regeneration is the unit of change.** A single bad route is a
  small tile set; regenerate just those tiles, run `tools/NavDataAudit`, then
  the matching `LongPathingRouteTests` gate. Full-map regeneration is for
  config / capsule changes.
- **Every regeneration must produce a manifest.** `NavDataAudit
  --write-manifest <path>` records nav-data signature, agent dimensions,
  per-tile hashes. The signature feeds PathfindingService's route cache key.

## Build

See [README.md](README.md) for the full command. Short form:

```powershell
.\tools\MmapGen\build-mmapgen.ps1
```

Currently: scaffold only — succeeds with a no-op target. Phase 2 of the
overhaul wires the real build. The CMakeLists.txt has the target list and
expected order in a comment block.

## Authoring config

- [config.json](config.json) — per-map / per-tile generator config. The top-
  level `default` block is the fallback. Map-level keys override the global
  default. A nested per-tile key (e.g. `"3147"`) overrides the map default
  for that single tile. Used for tuning `maxSimplificationError`,
  `detailSampleDist`, and capsule overrides on city tiles where geometry is
  pathological.
- [offmesh.txt](offmesh.txt) — off-mesh connections. Format is one connection
  per line; see the file header for the exact grammar. WWoW-specific entries
  are tagged `// WWoW:` and live above the divider; inherited vmangos
  entries live below.

## Don't

- Don't introduce a separate generator (DotRecast, TrinityCore mmaps_generator)
  without an ADR + tile-format compatibility plan. The runtime loader is
  strict.
- Don't bypass `NavDataAudit` for "the change is small / focused / obvious."
  The audit is what catches walkable-radius regressions, GO bake misses,
  and Detour version drift.
- Don't `dotnet test` against new tiles without redeploying to the Docker
  pathfinding service. The cached service runs against mounted data; stale
  services produce false greens. Rebuild + redeploy is part of the workflow.
- Don't add per-spot route gates in `Tests/PathfindingService.Tests` to "make
  the test go green." If a route fails, regenerate the tile and assert
  generic walkability for that capsule, not specific coordinates.
- Don't edit upstream-mirrored files (under `contrib/`, `dep/`, `src/`) without
  a comment block explaining what changed and why. We're free to modify, but
  reviewers and future agents need to trace divergence from the import baseline.

## Process safety

- **Never blanket-kill `dotnet.exe` or `Game.exe`.** See the root
  `AGENTS.md`. MmapGen runs do not need any kill operations.
- **MmapGen.exe runs against `D:/MaNGOS/data` in-place.** Always back up the
  affected `mmaps/<mapId>*.mmtile` files before a regen if you might want
  to roll back. The historical pattern is
  `D:/MaNGOS/data/mmaps/regen-backup-<UTC-stamp>/`.

## Done criteria for any MmapGen edit

1. `tools/MmapGen/build-mmapgen.ps1` succeeds.
2. The targeted tile set was regenerated against `D:/MaNGOS/data`.
3. `tools/NavDataAudit` passes for the regenerated tiles and produces a
   manifest with the new nav-data signature.
4. The matching `LongPathingRouteTests` gate (or new gate, if the slice adds
   one) is green against the regenerated data.
5. The Docker `wwow-pathfinding` service was rebuilt and redeployed against
   the new data.
6. The handoff entry in `Services/PathfindingService/TASKS.md` (or this
   project's `TASKS.md`, when one exists) records the regenerated tile set,
   the manifest path, and the green gate evidence.
