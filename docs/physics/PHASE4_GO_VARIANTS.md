# Phase 4 — Variant-Aware GameObject Bake + Runtime

> **Status:** Design draft, 2026-05-09. Expands `PATHFINDING_OVERHAUL.md` Phase 4
> ("Transport pass + GO bake fidelity sweep") to cover **event-based**
> GameObjects, not just always-on transports.
>
> **Problem in one line:** the bake captures a hardcoded list of 12 transports
> into the navmesh polymesh, but every other static-collision GameObject in
> the world (DB-spawned + event-conditional) is invisible to both bake and
> runtime — so the bot stalls anywhere a navmesh polygon was implicitly
> derived from a GO it can't physically validate.

---

## 1. Concrete failure cases (motivating examples)

| Case | Object | Always-on? | Failure mode |
|---|---|---|---|
| BRM south-face stall | `displayId=4652 @ (-7940.6,-1142.4,172.8)` map 0 | always | Navmesh polygon at z=171.24, vmap=-200000, runtime classifier reports `Blocked/StepUpTooHigh`; bot stuck mid-climb. |
| Onyxia head (Stormwind / Orgrimmar) | dragon head + spike rack on city wall | event (~2h after guild kills Onyxia) | Adds collidable polygons over the city gate; routes that walk under the gate during the event would walk through a head if the bake pretends nothing's there. |
| Darkmoon Faire (Mulgore / Elwynn) | tents, rides, Silas Darkmoon's ramp | event (rotating monthly cadence) | Entire faire camp is collidable for ~1 week per month. Routes through that field need the variant. |
| Hallow's End | candy buckets, scarecrows, decorations | event (~2 weeks/year) | Scattered collidables; mostly harmless individually but collectively block enough doorways and vendor approaches to matter. |
| Children's Week balloons / Lunar Festival lanterns | small props | event | Mostly walkable-under, but some block doorway widths for Tauren capsule. |

The user's ask: **the bake and the runtime must know about ALL of these**, including the event-conditional ones, AND the BotRunner must be able to request "give me a path that respects the world state I'm currently observing."

---

## 2. Current architecture gap (per Round-5 mapping)

```
[BAKE]                                    [RUNTIME]
gameobject_spawns.json   →   (orphaned)   DynamicObjectRegistry
  exporter exists                            has GO model index but
  but no MmapGen consumer                    no spawn population

MapBuilder.buildTransports               SceneQuery.GetGroundZ
  hardcoded 12 transports                  has GO query path but
  no event awareness                       finds nothing because
  no DB integration                        registry isn't populated

mmaps/0004634.mmtile                     vmap/000_34_46.vmtile
  has poly at z=171.24                     no WMO at GO 4652 footprint
  (came from where? bake mystery)          (vmap is WMO/M2 only)
```

The existing `DynamicObjectRegistry` is the right runtime primitive — it already
handles per-instance transforms, AABB-overlap triangle queries, and dual-source
ground-Z fallback. The gap is: **(a) MmapGen doesn't ingest GO spawns**, **(b)
nothing populates the registry from spawn data at runtime**, and **(c) there's
no variant concept anywhere**.

---

## 3. Design principles

1. **Bake everything that's static-collidable; let the runtime gate dynamic state.**
   The bake produces N variants per map. The runtime composes the requested
   variant subset. Don't try to avoid baking — bake more, gate by request param.

2. **Variants are additive, not mutually exclusive.**
   Real world: bot in Org during Ony-head AND Darkmoon-Faire-Mulgore (yes,
   Darkmoon is on map 1 — included only as composability example). A request
   names a SET of active variants, runtime composes the union.

3. **Tile-scoped variants.**
   A variant doesn't replace the whole map's bake — it replaces individual tiles
   where the variant changes geometry. Most tiles are unaffected by Ony-head
   (only the 2-3 tiles around the city gate). Variant deltas keep the bake
   matrix from exploding.

4. **Variants are server-driven, not bot-decided.**
   The bot doesn't infer "I think Ony is up" from heuristics. The StateManager
   asks the server (SOAP / chat / a /worldstate query if MaNGOS supports it),
   gets the active event set, and passes that into path requests. If the server
   says "no Ony", the bot routes through the gate normally; if "Ony up", the
   bot routes around the head.

5. **Runtime physics layer must hold the same variant truth as the bake.**
   It's not enough for the navmesh to know about a variant — the runtime
   classifier (`ClassifyPathSegmentAffordance`) MUST also see the GO collision
   for the active variants, otherwise the BG bot will report `Blocked` over
   GO-derived polygons and stall. Both layers consume the SAME variant
   manifest at request time.

6. **Variant identity is content-addressed.**
   A variant is named by a stable string (`base`, `ony-head-org`,
   `darkmoon-elwynn`, `hallows-end-org`). The set of active variants is the
   request param. Naming is human-readable for debugging; downstream the
   runtime computes a hash of the sorted variant set as a cache key.

7. **Default request is `base` only.**
   Backward-compat: existing path queries work unchanged. Only callers that
   know about events opt into variants.

---

## 4. Bake-time pipeline

### 4a. Variant manifest

```yaml
# tools/MmapGen/variants/manifest.yaml
variants:
  - id: base
    description: "Always-on world. Static DB spawns with no event/condition."
    spawn_filter: "event_id IS NULL AND pool_template IS NULL"
  - id: ony-head-org
    description: "Onyxia head turn-in display in Orgrimmar."
    affected_maps: [1]
    affected_tile_ranges: [{map: 1, x: [40,42], y: [29,31]}]
    spawn_filter: "event_id = <ID> OR script_name LIKE '%ony_head%'"
  - id: darkmoon-elwynn
    affected_maps: [0]
    affected_tile_ranges: [{map: 0, x: [33,35], y: [49,51]}]
    spawn_filter: "event_id = <DARKMOON_ELWYNN_ID>"
  # ... one entry per event ...
```

Source of truth for `spawn_filter` is MaNGOS DB (`game_event`,
`game_event_gameobject`, `pool_template`, `pool_gameobject`). The
GameObjectExporter is extended to dump per-variant filtered JSON files:

```
gameobject_spawns/
  base.json
  ony-head-org.json
  darkmoon-elwynn.json
  ...
```

### 4b. Bake invocation

```powershell
# Bake all variants (CI / nightly)
MmapGen.exe 0 --variants base,ony-head-org,darkmoon-elwynn

# Single variant rebake (focused investigation)
MmapGen.exe 0 --tile 34,46 --variants base
MmapGen.exe 0 --tile 41,30 --variants ony-head-org
```

For each `--variants V1,V2`, MmapGen:
1. Loads `gameobject_spawns/<variant>.json` for each named variant.
2. Filters spawns by map + tile-range.
3. For each affected tile, generates a separate output file:
   - `mmaps/0001403.base.mmtile` — base bake
   - `mmaps/0001403.ony-head-org.mmtile` — variant delta (only the polys that DIFFER from base)
4. Writes a per-tile variant manifest documenting which variants touched
   the tile and what changed: `mmaps/0001403.variants.json`.

The base bake remains the only non-variant bake; deltas are explicitly
suffixed. Existing single-bake behavior is `--variants base` (default).

### 4c. SceneCacheBuilder companion (NEW tool)

The runtime physics needs the **collision triangle data** for each variant's
GameObjects, separate from the navmesh. New tool:

```
tools/SceneCacheBuilder/
  Program.cs               # CLI: --variants, --maps, --tiles
  SceneCacheBuilder.csproj
```

For each variant + tile, `SceneCacheBuilder` reads the spawn JSON, resolves
each `displayId` to a model file via `temp_gameobject_models`, transforms
the model's collision triangles by the spawn's position + orientation +
scale, and writes a packed binary file:

```
scene-cache/
  0001403.base.scenecache
  0001403.ony-head-org.scenecache
  ...
```

Schema: header (variant id, tile coord, triangle count) + flat triangle
array (9 floats per triangle). Same access pattern as vmap query, just
keyed by variant.

---

## 5. Runtime pipeline

### 5a. PathfindingService

`PathfindingService` accepts a new optional protobuf field on path requests:

```protobuf
message PathRequest {
  // ... existing fields ...
  repeated string active_variants = 7;  // ["base", "ony-head-org"]
}
```

At request time, the service:
1. Computes a cache key from `(map, sorted active_variants)`.
2. Loads the base mmtile + applies variant deltas in deterministic order
   (by variant id) to construct a Detour navmesh suitable for this request.
3. Loads the matching scene-cache files into `DynamicObjectRegistry` /
   `SceneQuery::GetGroundZ`'s static-triangle layer.
4. Solves the path against the composed navmesh + scene cache.
5. Returns the path. Cache the composed navmesh keyed by variant set
   (LRU, bounded memory).

If `active_variants` is empty, default to `["base"]`.

### 5b. DynamicObjectRegistry composite load

```cpp
// Exports/Navigation/DynamicObjectRegistry.h
bool LoadVariantSceneCache(uint32_t mapId, const char* sceneCachePath, const char* variantId);
void UnloadVariant(uint32_t mapId, const char* variantId);
```

The registry tracks variant ownership of registered triangles, so unloading
a variant cleanly removes its triangles without touching others.
PathfindingService loads the variants needed for the current request,
unloads any variants no longer in any active request (LRU).

Naming: `LoadVariantSceneCache` (positional spawn data), distinct from
existing `LoadDisplayIdMapping` (model index).

### 5c. SceneDataService

The streaming SceneDataService needs the same composability — when serving
scene triangles to a BackgroundBotRunner that asked for variant set V, the
service includes the union of base + V's triangles.

Extension: `ExtractSceneGrid()` accepts a variants param; iterates registered
variant scene caches in the requested area and includes their triangles in
the response.

---

## 6. BotRunner integration

### 6a. StateManager owns world-state observation

StateManager queries the server periodically for active events:
- SOAP `event` listing (MaNGOS doesn't have this directly; need `.event list` parser)
- Or scan `game_event` MySQL table directly (read-only is acceptable per
  CLAUDE.md "MaNGOS Data Access" rule for non-mutating queries).

StateManager publishes the active set as part of its standard snapshot:

```protobuf
message WorldStateSnapshot {
  repeated string active_variants = 1;
  int64 observed_at_unix_ms = 2;
}
```

### 6b. BotRunner consumes world state

Every path request from `BotRunner` includes the StateManager's last
observed `active_variants`. `NavigationPath`/`PathfindingClient` thread
this through transparently — no per-task code change.

### 6c. Conservative defaults

- If StateManager hasn't published a world-state snapshot, BotRunner sends
  `active_variants=[]` → service defaults to `base`. Safe.
- If a variant is requested but the corresponding mmtile delta is missing,
  service logs a warning and falls back to base. Live test catches the
  warning so missing variants don't silently degrade.

---

## 7. Storage / naming

```
mmaps/
  0001403.mmtile              # Legacy single-bake (kept for back-compat)
  0001403.base.mmtile         # Same content as legacy after variant-aware bake
  0001403.ony-head-org.mmtile # Delta vs base
  0001403.variants.json       # Manifest: which variants touch this tile

scene-cache/
  0001403.base.scenecache
  0001403.ony-head-org.scenecache

gameobject_spawns/
  base.json
  ony-head-org.json
  darkmoon-elwynn.json
  ...

tools/MmapGen/variants/manifest.yaml  # Source of truth for variant defs
```

The legacy `0001403.mmtile` (no `.variant` suffix) is kept as a symlink /
copy of `.base.mmtile` for any consumer that pre-dates variants (e.g.,
the live FG bot's injected x86 Navigation.dll might still use the legacy
path until the variant-aware path is deployed).

---

## 8. Phased implementation

| Phase | Scope | Exit |
|---|---|---|
| **A** | MmapGen reads `gameobject_spawns/base.json`, bakes always-on static GOs into mmtile. SceneCacheBuilder produces `<tile>.base.scenecache`. Runtime loads scene cache via `LoadVariantSceneCache` at startup with variant=`base`. **No protobuf change yet** — request path is still single-variant. | BRM 4652 case: probe + live test pass for FlameCrest→UBRS WITH the GO 4652 baked into vmap-equivalent runtime data. OG zeppelin live test still passes. |
| **B** | Variant manifest + per-variant exporter. SceneCacheBuilder produces all variants. Bake produces `<tile>.<variant>.mmtile` deltas. Runtime LRU caches variant-composed navmeshes. | Probe Ony-head Org tile with `--variants base,ony-head-org` and verify the head GO appears as collidable triangles in the scene cache and as a navmesh-blocker delta. |
| **C** | PathfindingService protobuf field `active_variants`. PathfindingClient passes through. Default `[]→[base]` unchanged behavior. | Existing live tests keep passing with empty variant set. New live test exercises a request with explicit `[base]` and one with `[base, ony-head-org]`. |
| **D** | StateManager world-state observation. BotRunner snapshot field. End-to-end: StateManager publishes active variants, BotRunner reflects them in path requests, PathfindingService serves the composed bake. | Live test: trigger Ony-head event via SOAP, observe StateManager's active_variants update, observe path request reflect the variant, observe path through Org city gate route AROUND the head. |
| **E** | Coverage rollout: extract every event from MaNGOS DB (`game_event`, `pool_template`), generate manifest entries, bake all variants in CI. | All map-0 and map-1 events have variant entries; nightly bake produces all variants; nothing is unbakeable. |

Phases A–C are mostly engineering. D requires SOAP / DB polling design.
E is data-extraction grunt work that scales linearly.

---

## 9. Risks / open questions

1. **MmapGen variant-aware bake performance.** Current full bake of map 0
   already takes ~hours. N variants linearly multiplies that. Mitigation:
   delta-only bake (only re-bake tiles whose spawn footprint changed),
   parallel tile bake (`--threads N` already exists), nightly CI rather
   than on-demand.

2. **Variant explosion.** Theoretical 2^N composition matrix. Mitigation:
   variants are named singletons, runtime composes at load time (not
   bake time). Bake produces one delta per variant. Composition is O(active
   variants) per request, cached LRU.

3. **MaNGOS event source-of-truth.** MaNGOS's event system uses
   `game_event_gameobject` linkage tables. If our exporter mistakes a
   pool-spawn for an event-spawn, the variant filter is wrong. Mitigation:
   variant manifest is human-curated initially, validated against DB at
   bake time.

4. **Runtime SceneQuery cache invalidation.** When a variant LRU-evicts,
   `DynamicObjectRegistry` must atomically remove its triangles without
   ripping out base triangles. Mitigation: `LoadVariantSceneCache` /
   `UnloadVariant` track owner per triangle.

5. **Live FG bot's injected x86 Navigation.dll** doesn't speak the new
   protobuf field. It uses WoW client physics for collision (which DOES
   know about all GOs because the WoW client receives spawns live). FG bot
   doesn't need variant-aware bake at runtime — it already gets the truth
   from the client. **The variant work primarily matters for BG bots and
   for the BotRunner's path-planning layer (which the FG bot also queries).**
   The FG bot's stalls happen when BotRunner gives it a path the WoW
   client physics can't follow — that path planning is what gains the
   variant awareness here.

6. **Existing hardcoded transport list in `MapBuilder.buildTransports()`**.
   Should those 12 entries become a variant (e.g., `transports-default`)?
   Yes, but for back-compat keep them in `base.json` initially; can split
   later if a transport becomes event-conditional (e.g., the Booty Bay
   ship is timed).

---

## 10. What this addresses (cross-reference)

- **BRM south-face stall**: Phase A bakes GO 4652 into base. Probe verifies
  vmap-equivalent ground-Z available at the live stall coord; classifier
  no longer reports `Blocked` artifacts.
- **Onyxia head**: Phase B + D — variant `ony-head-org`, request-time
  composition.
- **Darkmoon Faire**: Phase B + D — variant per host city, request-time
  composition.
- **All other event GOs**: Phase E rollout.
- **PFS-OVERHAUL-006 freeze contract**: bake-side fix, not a managed-repair
  extension. Per `PATHFINDING_OVERHAUL.md` §"What IS allowed during the
  freeze" — anything in `tools/MmapGen/` and analogous bake-side tools is
  in scope.

---

## 11. Decision points needed before code starts

| # | Question | Default |
|---|---|---|
| 1 | Variant identifier format. Strings (`ony-head-org`) or hashed integers? | Strings, human-readable |
| 2 | Variant manifest format. YAML, JSON, INI? | YAML (existing offmesh.txt is custom; YAML is more declarative) |
| 3 | Where does the per-tile variant manifest go? Per-mmtile JSON sibling, or single map-level index? | Per-tile JSON sibling for parallel-bake friendliness |
| 4 | LRU cache size for runtime composed navmeshes? | 16 entries per map; revisit after measuring |
| 5 | When variant data is stale (event ended after request issued), how does the cache invalidate? | TTL = 60s on active_variants snapshot; refresh poll on StateManager |
| 6 | Do we keep the legacy `0001403.mmtile` filename, or migrate everything to `.base.mmtile`? | Keep legacy as a symlink during rollout, drop after Phase D ships |

---

## 12. Phase A first concrete deliverable (proposed)

Smallest viable slice that proves the architecture and unblocks BRM:

1. **MmapGen reads `gameobject_spawns/base.json`** during `buildTransports` (rename to `buildPersistentGameObjects`). For each spawn whose displayId resolves to a vmo model, call existing `buildGameObject(...)` with the spawn's position + orientation + scale.
2. **`tools/SceneCacheBuilder` minimal version** — produces `<tile>.scenecache` from the same `base.json`, packed-triangle format. No variants yet (single file per tile).
3. **`Exports/Navigation/DynamicObjectRegistry::LoadSceneCache(mapId, path)`** — loads packed-triangle data into the registry as if every triangle came from a `RegisterObject` call. Exposed via a new C export `LoadSceneCacheForMap(mapId, path)`.
4. **`PhysicsTestExports::InitializePhysics`** — after `LoadDisplayIdMapping`, scan `scene-cache/` for `<map>*.scenecache` and call `LoadSceneCacheForMap` for each.
5. **PathPhysicsProbe** — with the new init, GroundZ should return `vmap` or `bih` (or a new "scenecache" source) for GO 4652 footprint instead of -200000.
6. **Live test** — re-run FlameCrestToBrmDungeonEntrance UBRS case, expect bot to walk past (-7949.7,-1162.8) without stalling.

This Phase A doesn't touch the protobuf or BotRunner. It's the "always-on
GO collision finally exists" baseline. Variants come in Phase B.

Estimated effort: 1-2 sessions for Phase A (MmapGen hookup + SceneCacheBuilder
scaffold + runtime load). Real BRM unblock validation in the same window.

---

**Awaiting decision on the open questions in §11 and confirmation that
Phase A is the right starting slice before code begins.**
