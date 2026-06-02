# Native PathfindingService Rewrite + dtCrowd Evaluation

> **Status:** Plan only. Phase 6 of the [pathfinding overhaul](../Archive/PATHFINDING_OVERHAUL.md). Not started; do not implement until Phases 2–5 land. This document captures the design so the team knows where we're going and why.
>
> **Target scale:** ~3000 concurrent bots.

---

## Why a native rewrite

`Services/PathfindingService` (.NET 8) is currently a thin shell around a 5,600-line managed repair pipeline (`Repository/Navigation.cs`) that P/Invokes into `Navigation.dll`. After Phases 2–5, the managed repair pipeline is gone: the service becomes ~500 lines of "marshal protobuf request → call native → marshal protobuf response."

At that point the .NET layer is overhead:

- **Per-call P/Invoke + marshaling.** Every path query crosses a managed↔native boundary twice for input and twice for output, allocates managed arrays for waypoints, and runs through GC.
- **`g_navigationMutex` chokepoint.** `Exports/Navigation/DllMain.cpp:1958` serializes Navigation API calls because shared `dtNavMeshQuery` instances aren't thread-safe. A native service can hold per-thread `dtNavMeshQuery` instances and skip the mutex.
- **dtCrowd availability.** `dtCrowd` is a C++ API; using it from .NET means another P/Invoke surface to maintain. Native service uses it directly.
- **Two deployment artifacts.** Today: `Navigation.dll` + `PathfindingService.exe` (.NET). Native: a single `wwow-pathfinding` daemon. Simpler ops.
- **Container size.** A native binary + Detour data is ~10 MB; a .NET self-contained publish is ~100 MB. At 3000-bot scale we may run multiple shards.

The wire protocol stays the same (length-prefixed protobuf over TCP, port 9002). Existing clients (`BotRunner`, tests, `WoWStateManagerUI`) connect with no code change.

---

## Target architecture

```
+-----------------------------------------------------------+
| wwow-pathfinding (native C++ daemon, port 9002)           |
| +-------------------------------------------------------+ |
| | Connection loop (asio or hand-rolled IOCP)            | |
| | per-conn: read uint32 length → read N bytes proto     | |
| +-------------------------------------------------------+ |
| | Worker pool (one std::thread per logical core)        | |
| | per-worker: dtNavMeshQuery instance (~64 KB)          | |
| |             dtPathCorridor instance for sliced/long   | |
| |             optional: dtCrowdAgent ref for steering   | |
| +-------------------------------------------------------+ |
| | Shared dtNavMesh (loaded once at startup)             | |
| | RouteResultCache (LRU, fuzzy quantized keys)          | |
| | DynamicObjectRegistry (overlay; future: dtTileCache)  | |
| | Optional: dtCrowd (single instance, locked)           | |
| +-------------------------------------------------------+ |
| Protobuf types: existing `BotCommLayer/Models/ProtoDef`,  |
| compiled with protoc-c++ into the daemon.                 |
+-----------------------------------------------------------+
```

Key design decisions:

- **Per-thread `dtNavMeshQuery`.** Each worker thread owns a query. No global mutex; horizontal scale = thread count. Detour's docs explicitly support this pattern.
- **Single `dtNavMesh` shared read-only.** Loaded once at startup from `<data>/mmaps/`. Tile add/remove for dynamic obstacles goes through `DetourTileCache` (Phase 6.2; until then, dynamic obstacles stay on the managed-overlay model migrated to native).
- **Protobuf framing in C++.** Use `google::protobuf::C++` runtime + a tiny wire-framing helper. The existing `.proto` files in `Exports/BotCommLayer/Models/ProtoDef/` are already C++-compatible (we already build managed bindings from them).
- **No managed dependency at runtime.** The daemon links libdetour, librecast (for tile-cache obstacle build), libprotobuf, asio, zlib (for `.mmap` decompression if used). Static link or vendored.

---

## Wire protocol

Unchanged from today. See `Exports/BotCommLayer/Models/ProtoDef/`. Frame format:

```
+-----------+----------------------------+
| length    | protobuf message           |
| (uint32   |                            |
|  little-  | NavigationPathRequest /    |
|  endian)  | NavigationPathResult /     |
|           | ServiceStatusRequest / ... |
+-----------+----------------------------+
```

The protobuf compiler step that generates C# bindings for `BotCommLayer` will also need a C++ binding step for the daemon. Add a CMake invocation of `protoc --cpp_out=...` driven from the same `.proto` files.

---

## dtCrowd evaluation

`dtCrowd` is Detour's local steering / avoidance system. Each agent has:

- A target position.
- A radius / height (so other agents can avoid it).
- A path corridor (`dtPathCorridor`).
- A velocity that's blended each tick toward the target while avoiding nearby agents and walls.

### Does dtCrowd help us?

Depends on what "3000 bots" means concretely:

| Scenario | Use dtCrowd? | Why |
|---|---|---|
| 3000 bots scattered across many maps, rarely intersecting | **No** | Per-agent steering cost is wasted; bots aren't avoiding each other. Stick with one-shot `findPath` + `dtPathCorridor::moveOverPolyRef` for waypoint advancement. |
| 3000 bots clustered on a few maps (faction capitals, BG queues, common quest hubs) | **Yes** | Agent-vs-agent avoidance matters; dtCrowd handles it without the bot logic re-implementing it. Also gives smooth steering in narrow corridors. |
| Mixed | **Hybrid** | Run dtCrowd only when a bot's local radius contains other bots; fall back to pure `dtPathCorridor` otherwise. dtCrowd has a per-agent activation cost so this is actually how it's designed to be used at scale. |

### Practical numbers for ~3000 bots

dtCrowd's documented practical ceiling per `dtCrowd` instance is **256** active agents, hardcoded by `MAX_AGENTS` in `DetourCrowd.h`. To run 3000 we either:

1. **Patch `MAX_AGENTS` higher.** Doable but costs O(N²) for nearest-agent queries (current implementation is brute-force-against-all-agents per tick). At 3000 agents that's ~9M comparisons per tick. Not workable.
2. **Shard `dtCrowd` instances.** Run multiple instances, partitioning agents by spatial cell (one instance per ~256-agent cell). Cross-cell avoidance is ignored; usually fine because cells are large enough that two bots near a cell boundary aren't really at risk of bumping.
3. **Use dtCrowd selectively.** For bots in densely-populated areas; bots elsewhere use pure `dtPathCorridor`. This is the pattern most large-scale users land on.

**Recommendation:** Option 3 (selective). Default = pure `dtPathCorridor` per bot, no avoidance. Promote to a `dtCrowd` agent when the bot enters a "crowded zone" (capital cities, BG holding pens, raid corridors). Demote back to plain corridor when leaving.

Crowded-zone detection is cheap: each tick, iterate the bot's local hash-grid cell; if more than N bots are in the same cell, route them through dtCrowd.

### What we need to validate (Phase 6.1 dtCrowd evaluation)

Before committing to integrating dtCrowd:

1. **Throughput at scale.** Can a single shard of the native daemon handle 3000 path queries/sec on cold cache? With cache hits? Profile.
2. **Memory.** `dtNavMeshQuery` ≈ 64 KB. 256-agent dtCrowd ≈ 16 MB. 3000 bots if all in dtCrowd = ~190 MB just for crowd state. Manageable.
3. **dtCrowd agent count breakdown.** What % of bots are in crowded zones at peak? Measure during a representative workload.
4. **Off-mesh connection traversal in dtCrowd.** Verify dtCrowd handles `dtOffMeshConnection` traversal correctly for boarding zeppelins/elevators — there's a known historical bug where agents stutter at off-mesh entry. Smoke-test with the OG↔UC zeppelin.

### What we DON'T need from dtCrowd

- **Path planning** — that's `dtNavMeshQuery::findPath` regardless of crowd usage. dtCrowd just consumes a path.
- **Long-range avoidance** — bots don't avoid each other across continents. Local-cell only.
- **Formation steering** — we don't run synchronized squad movement.

---

## Migration plan (high level)

### Phase 6.0 — Wire protocol parity

- Compile existing `.proto` to C++. Wire a tiny native daemon that just echoes requests as no-op responses. Prove `BotRunner` connects to it on port 9002 and gets a (canned) response.
- Run side-by-side with the .NET service on a different port; toggle via `appsettings.json`.

### Phase 6.1 — Native pathfinding parity

- Move tile loading, `dtNavMesh` setup, `dtNavMeshQuery::findPath`, smooth-path, capsule wall clearance into the native daemon.
- Port `RouteResultCache` to native (LRU + quantized key + nav-data signature).
- A/B test: same protobuf request to .NET vs native produces identical waypoint sequence (modulo float epsilon).
- Cut over `BotRunner` to point at the native daemon. Decommission the .NET service.

### Phase 6.2 — dtTileCache for dynamic obstacles

- Port the managed `DynamicObjectRegistry` to `DetourTileCache` obstacles. Each registered dynamic blocker becomes a tile-cache cylinder/box obstacle.
- Tile cache rebuilds the affected tiles on-demand; queries see the updated mesh natively. No managed overlay reapplication per query.

### Phase 6.3 — dtCrowd integration (selective)

- Implement crowded-zone detection (per-cell agent count threshold).
- Add dtCrowd agent registration / unregistration as bots enter / leave crowded zones.
- Add a `NavigationPathRequest.use_crowd_steering` field (default false) for the protobuf API. Bots in crowded zones set it true.
- The daemon returns either a static path (no crowd) or a corridor reference + steering instructions per tick (crowd).

### Phase 6.4 — Sharding

- If a single daemon can't carry 3000 bots, shard by map ID (one daemon per continent) or by client ID (round-robin).
- Add a thin routing front-end (or use the `BotRunner` config to point at the right shard).

---

## Open questions

- **`dtPathCorridor` reuse across queries.** Each bot maintains a corridor in the daemon (state per connection). Connection drop = corridor lost = bot must re-`findPath` on reconnect. Is that acceptable, or do we need a session token + corridor persistence?
- **Hot reload of mmap data.** Today the .NET service requires restart to pick up regenerated tiles. Can the native daemon support `dtNavMesh::removeTile(...)` + `addTile(...)` for hot patching during dev? Probably yes via a `ReloadTilesRequest`.
- **Determinism for tests.** Some Detour internals (especially `dtCrowd` neighbor iteration) are insertion-order-dependent. Need to make sure tests that assert exact paths still pass on the native side, or relax assertions to "path exists + bounded length."
- **Linux container.** The current `wwow-pathfinding` Docker image is Linux x64. Building libdetour + protobuf C++ on Alpine vs Debian — pick once and document. Vendored, not apt-pulled, to match the in-tree philosophy.

---

## Reference

- [docs/Archive/PATHFINDING_OVERHAUL.md](../Archive/PATHFINDING_OVERHAUL.md) — overhaul master plan; this is Phase 6 of that.
- [tools/MmapGen/](../../tools/MmapGen/) — generator that produces the tiles this service consumes.
- [Exports/Navigation/](../../Exports/Navigation/) — current native pathfinding library that gets folded into the daemon.
- [Exports/BotCommLayer/Models/ProtoDef/](../../Exports/BotCommLayer/Models/ProtoDef/) — `.proto` definitions to compile to C++.
- Detour upstream: https://github.com/recastnavigation/recastnavigation (vendored at `tools/MmapGen/dep/recastnavigation/`).
- dtCrowd reference: `dep/recastnavigation/DetourCrowd/` (NOT YET VENDORED — this is in upstream Recast but not currently in our vmangos pull; add when Phase 6.3 begins).
