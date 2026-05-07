# MmapGen — Provenance and License

## Origin

The C++ sources under [contrib/](contrib/), [dep/](dep/), and [src/](src/) of this
project were imported on **2026-05-06** from the
[vmangos/core](https://github.com/vmangos/core) repository (`development` branch
at the time of import). vmangos itself is a derivative of the CMaNGOS / MaNGOS
WoW-emulation lineage.

The import is intentionally **unlinked** from the upstream Github — the import
process used a `--depth 1 --filter=blob:none --sparse` clone, copied only the
working-tree files, dropped the `.git/` directory, and committed the sources
into this repo. There is no submodule, no remote tracking, and no expectation
that further upstream syncs will happen automatically.

After this point, the code under `tools/MmapGen/` is **owned in-tree** as part
of the Westworld of Warcraft monorepo. Modify it, delete unused subtrees,
re-organize, add transports — agents do not need permission from upstream to
edit anything here.

## License

vmangos and its dependencies are licensed under **GPL v2** (see
[UPSTREAM_LICENSE_VMANGOS](UPSTREAM_LICENSE_VMANGOS) for the verbatim upstream
LICENSE file). Per-file copyright headers from CMaNGOS / MaNGOS / vmangos
contributors are retained and must remain. Our derivative work (this
in-tree copy plus any modifications) is therefore also GPL v2.

That license obligation applies **only to MmapGen itself**, which is a
build-time tool that produces data files. The pathfinding runtime
(`Exports/Navigation`, `Services/PathfindingService`, the rest of the WWoW
solution) is not linked against MmapGen and is unaffected by its license.

## Why vmangos and not TrinityCore

We considered TrinityCore's `src/tools/mmaps_generator/` (3.3.5 branch) as an
alternative starting point. We picked vmangos because:

1. **Tile format compatibility.** `Exports/Navigation`'s strict mmap loader
   accepts only the MaNGOS-family wrapper (`MMAP_MAGIC=0x4D4D4150`,
   `MMAP_VERSION=6`, 20-byte `MmapTileHeader`, 64-bit `dtPolyRef`). TrinityCore
   uses a different wrapper layout and would require either rewriting the
   generator output stage or rewriting our loader. Neither is worth doing for
   marginal generator-quality gains.
2. **Existing data parity.** The current `D:\MaNGOS\data` content was produced
   by an externally-patched vmangos `MoveMapGenerator`. Bringing the same
   lineage in-tree means our output is a drop-in replacement for what
   `Navigation.dll` already loads.
3. **GameObject baking.** vmangos's `MapBuilder::buildGameObject(...)` /
   `buildTransports()` already exists and produces the GO-aware tiles the
   `tools/NavDataAudit` proof gate looks for.
4. **Off-mesh connection support.** vmangos's [contrib/mmap/offmesh.txt](contrib/mmap/offmesh.txt)
   format is line-based, simple, and already consumed by the generator.
   Authoring zeppelin/elevator/boat off-mesh links is a one-line change per
   transport.

If a specific TrinityCore improvement is worth porting (better WMO chunk
filtering, threaded queueing, etc.), port it as a focused patch into our copy.
Do not re-import a different upstream wholesale.
