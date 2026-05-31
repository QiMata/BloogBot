# MMAP / MMTILE Format Spec

The contract `Exports/Navigation`'s strict loader (`MapLoader.cpp`,
`MoveMap.cpp`) accepts. `tools/MmapGen` must produce exactly this layout.
Any divergence is a generator bug.

> Single source of truth for "what's a valid tile?" Update this spec, this
> file, and the strict loader together. If they ever drift, the loader is
> right by default and the doc is wrong.

---

## 1. File naming

MaNGOS writes one mmap params file per map and one mmtile per tile:

| File | Path | Contents |
|---|---|---|
| Map params | `mmaps/<mapId:03d>.mmap` | Single `dtNavMeshParams` struct |
| Tile | `mmaps/<mapId:03d><tileY:02d><tileX:02d>.mmtile` | 20-byte header + Detour tile payload |

Examples:

- `mmaps/001.mmap` — Kalimdor (map 1) navmesh params.
- `mmaps/0012840.mmtile` — Kalimdor tile (Y=28, X=40) — Orgrimmar dock area.
- `mmaps/0002730.mmtile` — Eastern Kingdoms tile (Y=27, X=30) — Undercity arrival.

Note the tile-name encoding: `mapId * 1e5 + tileY * 1e3 + tileX`, zero-padded.
Tile X/Y are MaNGOS coordinates (see §3).

---

## 2. `.mmtile` payload layout

```
+---------------------------+
| MmapTileHeader (20 bytes) |
+---------------------------+
| Detour tile payload       |
| (size bytes; opaque to    |
|  the loader, parsed by    |
|  Detour itself)           |
+---------------------------+
```

### MmapTileHeader (20 bytes, all little-endian uint32)

| Offset | Field | Required value | Notes |
|---|---|---|---|
| 0 | `mmapMagic` | `0x4D4D4150` (`'MMAP'`) | Wrapper magic; rejects foreign formats. |
| 4 | `dtVersion` | `7` | Must match `DT_NAVMESH_VERSION` compiled into `Navigation.dll`. |
| 8 | `mmapVersion` | `6` | MaNGOS wrapper version. |
| 12 | `size` | (payload length) | Length of the Detour payload that follows the header. |
| 16 | `usesLiquids` | `0` or `1` | `1` if liquid surfaces (water swimming volumes) are baked into the mesh. Continents use `1`. |

The strict loader (`MoveMap.cpp`) rejects a tile if any of these are wrong:

- `mmapMagic != 0x4D4D4150`
- `dtVersion != DT_NAVMESH_VERSION` (currently 7)
- `mmapVersion != 6`
- `size <= 0`
- The Detour payload's own header magic does not match Detour's expectation.
- The Detour payload's `version` does not match `DT_NAVMESH_VERSION`.

Rejected tiles do not call `dtNavMesh::addTile(...)`. The map silently has a
hole at that tile location. Audit logs will include a `[MMAP][REJECT]` line
naming the offending field.

### Detour payload

The bytes after the 20-byte wrapper are exactly what `dtCreateNavMeshData(...)`
produces in the generator. The loader passes this region to
`dtNavMesh::addTile(payload, size, DT_TILE_FREE_DATA, 0, ...)` and Detour owns
the lifecycle from there.

---

## 3. Tile coordinate system

MaNGOS uses a 64x64 tile grid per continent, aligned to WoW's continental
grid. The vendored vmangos/CMaNGOS generator uses the historical ADT tile
frame, which is swapped relative to normal WoW `(X, Y, Z)` coordinates:
`tileX` is derived from world **Y**, and `tileY` is derived from world **X**.
This matches `src/game/Commands/DebugCommands.cpp`, which prints rebuild
commands as:

```c++
tileY = 32 - player.X / GRID_SIZE;
tileX = 32 - player.Y / GRID_SIZE;
```

- Continent extent: `(-17066.6664f, +17066.6664f)` on both X and Y axes (WoW continental units).
- `GRID_SIZE = 533.33333f` (WoW units per tile, both axes).
- Tile origin convention: tile `(0, 0)` is at the north-west corner of the
  continent.
  - `tileX = floor((maxX - worldY) / GRID_SIZE)`.
  - `tileY = floor((maxX - worldX) / GRID_SIZE)`.
- Tile filenames are still written as `<map><tileY:02d><tileX:02d>.mmtile`.
  This filename order is easy to misread during single-tile regen work.

Example: WoW point `(1320.14, -4653.16, 53.89)` (OG/UC zeppelin deck) lives in:

```
tileX = floor((17066.6664 - -4653.16) / 533.3333) = floor(40.726) = 40
tileY = floor((17066.6664 -  1320.14) / 533.3333) = floor(29.527) = 29
```

So the correct MmapGen CLI tile is `--tile 40,29`, the matching per-tile config
key is `"4029"`, and the generated runtime file is `mmaps/0012940.mmtile`.

Important: Detour vertices in these tiles are also in the generator frame:
`(Recast X, Recast Y, Recast Z) = (WoW Y, WoW Z, WoW X)`. Visualization tools
must swap axes back before overlaying WoW path coordinates.

Per-tile vertex grid:

- `BASE_UNIT_DIM = 0.2666666f` (WoW units / heightfield cell). Must divide
  `GRID_SIZE` evenly.
- `VERTEX_PER_MAP = round(GRID_SIZE / BASE_UNIT_DIM) = 2000` (per axis).
- `VERTEX_PER_TILE = 80` (must divide `VERTEX_PER_MAP`).
- `TILES_PER_MAP = VERTEX_PER_MAP / VERTEX_PER_TILE = 25` per axis.

So a continent has 25×25 = 625 tiles addressable via the navmesh, with the
remaining 64×64 - 625 = 3471 entries unused. (vmangos uses the 64×64
addressing because the parameter is a `dtNavMeshParams.maxTiles`, sized
generously.)

---

## 4. `.mmap` map params layout

Single `dtNavMeshParams` (Detour C struct) written raw:

```c
struct dtNavMeshParams {
    float orig[3];      // World origin of tile (0,0). For continents: (-17066.66, -17066.66, 0)
    float tileWidth;    // GRID_SIZE = 533.33333
    float tileHeight;   // GRID_SIZE = 533.33333
    int   maxTiles;     // 64*64 = 4096
    int   maxPolys;     // upper bound of polys per tile
};
```

Exact bytes are platform-endian-dependent for the int fields, but on Windows
x64 (our only build target) this is fine.

Loader behavior: `MoveMap.cpp` reads `<mapId>.mmap`, calls
`dtNavMesh::init(&params)`, then iterates `<mapId>*.mmtile` files in the
directory and calls `addTile(...)` for each.

---

## 5. Polyref bit allocation

`Navigation.dll` is built with `DT_POLYREF64` (64-bit `dtPolyRef`). The bit
split is fixed at:

| Field | Bits | Max values |
|---|---|---|
| `salt` | 16 | 65 535 (per-tile generation counter) |
| `tile` | 28 | ~268 M tiles per navmesh |
| `poly` | 20 | ~1 M polys per tile |

The generator does not need to be aware of the bit split — Detour computes it
from `dtNavMeshParams::maxTiles` and `maxPolys`. Generator's responsibility is
to keep `maxPolys` ≤ `1 << 20`. If a tile has more polys than that fits, the
generator must split into smaller tiles or simplify the mesh; do not silently
truncate.

---

## 6. Off-mesh connections (`dtOffMeshConnection`)

Off-mesh connections are baked into the Detour tile payload at generation
time. They are **not** stored separately. The loader doesn't see them; it
just passes them through Detour.

vmangos's `MapBuilder::buildAllMaps()` reads `offmesh.txt` and feeds the
connection list into `dtNavMeshCreateParams` for each tile that contains an
endpoint. The generator emits standard Detour tile data with the connections
embedded.

Authoring rules (see also [tools/MmapGen/offmesh.txt](../../tools/MmapGen/offmesh.txt)):

- Endpoints must be **inside the navmesh**. If the start point is over a
  non-walkable polygon, Detour silently drops the connection. Use
  `tools/NavDataAudit` to verify `offmesh.txt` entries actually land in
  walkable poly bounds.
- Connections are **per-tile**. The generator places the connection in the
  tile that contains the start endpoint. If start and end are in different
  tiles, both endpoints must be on the same tile boundary or close to it,
  or the connection is rejected.
- `size` is the connection radius in WoW units. Detour uses this to size the
  off-mesh polygon Recast inserts at each endpoint. Bigger = more forgiving
  approach; smaller = tighter "you must be on this exact spot."
- Default direction is bidirectional unless the generator's
  `DT_OFFMESH_CON_BIDIR` flag is cleared. We currently treat all entries as
  bidirectional and, where directionality matters (boarding vs disembarking),
  add explicit reverse lines.

---

## 7. Liquid (water/lava) layer

`usesLiquids = 1` indicates the heightfield rasterizer included liquid
surfaces. WoW's swimming/wading mechanics require this to compute valid paths
across rivers/oceans/etc. Continents always use it.

Set to `0` only for transport maps and instances where liquid is irrelevant.

---

## 8. Validation gates

Every regeneration must pass three gates before live use:

### 8.1. NavDataAudit

```powershell
dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release -- D:/MaNGOS/data --map 1 --build-log <log> --write-manifest <manifest>
```

Checks:

- Tile wrapper magic / version / size match.
- Detour payload version matches.
- Wrapper header is exactly 20 bytes with `usesLiquids` as uint32 (some legacy
  outputs used uint8 padding; those are rejected).
- Tile Detour headers report the configured `walkableRadius` /
  `walkableHeight` / `walkableClimb` for the audited capsule.
- `temp_gameobject_models` and `gameobject_spawns.json` contain model-backed
  spawns in the audited tile set.
- The supplied build log proves the audited tiles were rasterized with GO bake
  evidence (`[GO] map=… tile=…: marked … gameobject span boxes`).

### 8.2. NavigationDetourCompatibilityTests

```powershell
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --filter "FullyQualifiedName~DetourCompatibilityTests" ...
```

Asserts the runtime can load the produced tiles via the strict loader.

### 8.3. LongPathingRouteTests (during/after Phase 4)

The corridor-level proof gate. After Phase 4, this is property-tests only —
no per-spot gates.

---

## 9. Common failure modes

| Symptom | Likely cause | Fix |
|---|---|---|
| Loader logs `[MMAP][REJECT] mmapMagic` | Generator emitted TrinityCore-format header | Use vmangos `MoveMapGenerator` lineage (i.e. MmapGen). |
| Loader logs `[MMAP][REJECT] dtVersion=8` | Generator built against newer Detour | Pin `tools/MmapGen/dep/recastnavigation/Detour/` to the same version `Exports/Navigation` uses. See `DETOUR_UPGRADE_BASELINE.md`. |
| Loader logs `[MMAP][REJECT] mmapVersion=5` | Old/legacy tile from a pre-strict-loader era | Regenerate. |
| `walkableRadius=0.2` in audit | Generator config didn't pick up Tauren capsule | Ensure `tools/MmapGen/config.json` has the correct `agentRadius=1.0247` for the affected map. |
| Off-mesh connection silently missing | Endpoint outside walkable poly | Move the endpoint, or fix the underlying mesh (the gangplank itself may not be a walkable polygon). |
| Path through a bonfire | GO bake didn't include that gameobject ID | Audit `gameobject_spawns.json`; if the spawn is missing, regenerate spawns via `tools/GameObjectExporter`. If the spawn is present, the bake filter in `MapBuilder::buildGameObject(...)` is skipping it — fix the filter. |

---

## 10. Reference

- `Exports/Navigation/MoveMap.cpp` — strict loader, source of truth.
- `Exports/Navigation/MapLoader.cpp` — wrapper coordinator.
- `tools/MmapGen/contrib/mmap/src/MapBuilder.cpp` — generator entry point.
- `tools/MmapGen/contrib/mmap/src/IntermediateValues.cpp` — wrapper write code (this is where the 20-byte header is emitted).
- `docs/physics/DETOUR_UPGRADE_BASELINE.md` — Detour version + ABI baseline.
- `docs/Archive/PATHFINDING_OVERHAUL.md` — master overhaul plan.
