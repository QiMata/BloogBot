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

See [docs/Archive/PATHFINDING_OVERHAUL.md](../../docs/Archive/PATHFINDING_OVERHAUL.md)
for the master plan, freeze contract, sequencing, and exit criteria.

## Read before editing

1. [NOTICE.md](NOTICE.md) — provenance + license.
2. [docs/Archive/PATHFINDING_OVERHAUL.md](../../docs/Archive/PATHFINDING_OVERHAUL.md)
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
- **Post-path generation repair is an anti-pattern.** If a static-world route
  only goes green because PathfindingService or `Navigation.dll` patched it
  after query generation, the tile is still wrong. Move the fix into the bake
  pipeline, off-mesh authoring, or a serialized final-tile cull and keep the
  route gate red until the baked data itself is correct.
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
- **Verify serialized final-tile edits with hash + probe, not size alone.**
  Post-addTile Detour culls can change which polys survive without changing the
  `.mmtile` byte length. Always confirm with `Get-FileHash`, `NavDataAudit`,
  and a route/poly-stack probe before claiming a final-tile fix landed.

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
- `writeAnchorStageManifest` / `logAnchorStageDiagnostics` - tile-local stacked-
  support debugging controls. Prefer `writeAnchorStageManifest=true`: it writes
  `meshes/map<map><tileY><tileX>_anchor_stage_manifest.json`, and
  `tools/scripts/bake-tile.ps1` copies that into
  `tmp/bake-sweeps/<variant>/analysis/` plus a `NavDataAudit` summary JSON/CSV.
  Use `anchorStageManifestCoordsWow` for analysis-only extra probe coords when
  you need stage answers for shifted dead-end points without changing the
  actual compact-span / final-Detour cull coord list.
  Use `preRegionAnchorCoordsWow` when a shifted hallway/city dead-end needs
  earlier source-support / compact cleanup proof without also promoting that XY
  into the checked-in final Detour stack-cull list.
  Use `anchorRouteTargetsWow` when a stage-clean anchor still dead-ends inside
  final Detour and you need reachability proof for the local winner component.
  Keep `postDetourCullAnchorTrappedComponents` off in the checked-in config
  unless a validated branch improves the real route sweep; on tile `1:40,29`
  the routeability evidence is useful, but the cull is not promotable yet.
  Keep `borrowMissingAnchorSourceSupportFromNeighbors=false` by default; it can
  make a sourceSupport blind spot look green in the manifest while making the
  real hallway route shape worse.
  If `1523.800,-4425.900,17.100` still dies at `polymesh`, the next allowed
  experiment surface is the pre-poly contour pair:
  `prePolyPreserveAnchorSupportCoordsWow` +
  `prePolyUseRawAnchorSupportContoursWow`. On `2026-05-24` that pair moved
  `1523.8` to `finalDetour / lower_competitor_dominant` while keeping the
  focused deck slice `7/7`, which proves the contour can survive later in the
  pipeline. It did NOT improve the full route count yet, so leave both keys
  absent in the checked-in config until a branch improves more than the stage
  answer.
  Do not retry `maxVertsPerPoly=4` or `=6` as a follow-up to that contour
  branch. Both values reintroduced top-deck connector / giant-bridge
  regressions even though they made some hallway/exterior anchors look more
  routeable in the manifest.
  Leave `logAnchorStageDiagnostics=false` unless you explicitly need the older
  per-subtile `HF-SRC-ANCHOR` / `CHF-SRC-ANCHOR` / `CHF-SRC-COMP` print stream.
  If exact dead-end coords are green in the manifest but live/raw-Detour
  routes still stall, extend the final Detour manifest first (component ids,
  candidate counts, direct runtime probes from those exact support coords)
  before retuning support thresholds again.
- [offmesh.txt](offmesh.txt) — off-mesh connections. Format is one connection
  per line; see the file header for the exact grammar. WWoW-specific entries
  are tagged `// WWoW:` and live above the divider; inherited vmangos
  entries live below.

## Don't

- Don't introduce a separate generator (DotRecast, TrinityCore mmaps_generator)
  without an ADR + tile-format compatibility plan. The runtime loader is
  strict. Also verify the geometry-input contract before treating a sibling
  generator as a candidate fix: stock TrinityCore/AzerothCore/vmangos
  generator flow is `terrain + vmap + offmesh`, not WWoW's GO-spawn-aware bake.
  On GO-sensitive city tiles, a straight swap is expected to regress routes.
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
- **Canonical iteration now bakes into `D:/wwow-bot/test-data`.** Use
  `tools/scripts/bake-tile.ps1` (or run `MmapGen.exe` with cwd set to the
  chosen data root) so test-data receives the new `mmaps/` tile first, then
  promote the approved tile into `D:/wwow-bot/prod-data` with
  `tools/MmapGen/promote-mmaps.ps1`. `WWOW_VMANGOS_DATA_DIR` is the supported
  fallback source for shared `gameobject_spawns.json` when the split test/prod
  roots do not carry their own copy. If you still run directly against
  `D:/MaNGOS/data`, treat that as a focused scratch/probe path and back up the
  affected `mmaps/<mapId>*.mmtile` files first. The historical backup pattern
  is `D:/MaNGOS/data/mmaps/regen-backup-<UTC-stamp>/`.

## Done criteria for any MmapGen edit

1. `tools/MmapGen/build-mmapgen.ps1` succeeds.
2. The targeted tile set was regenerated against the intended mutable data root
   (`D:/wwow-bot/test-data` for the normal loop, or an explicitly documented
   scratch root for a focused probe).
3. `tools/NavDataAudit` passes for the regenerated tiles and produces a
   manifest with the new nav-data signature.
4. The matching `LongPathingRouteTests` gate (or new gate, if the slice adds
   one) is green against the regenerated data.
5. The Docker `wwow-pathfinding` service was rebuilt and redeployed against
   the new data.
6. The handoff entry in `Services/PathfindingService/TASKS.md` (or this
   project's `TASKS.md`, when one exists) records the regenerated tile set,
   the manifest path, and the green gate evidence.
