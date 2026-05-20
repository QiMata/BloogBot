# WoW 1.12.1 World Coordinates — Authoritative Reference

> Source-verified bake-frame and runtime-frame constants for the Westworld of
> Warcraft 1.12.1 monorepo. Numbers below are read directly from
> `tools/MmapGen/` and from the on-disk bake at `D:\MaNGOS\data\mmaps\`.
> When the source code and prior memos disagree, the source code wins.

## How to use this doc

Given a world coordinate `(worldX, worldY, worldZ)` and a `mapId`:

1. **"Is this on map X?"** — Check the map's baked extent table in §4. If the
   target tile `(tileX, tileY)` does not fall inside `[minTileX..maxTileX] ×
   [minTileY..maxTileY]` (in WoW-physics convention), or the tile file is
   missing from `D:\MaNGOS\data\mmaps\`, the bot has no navmesh data there.
2. **"Is this inside the global addressable frame?"** — Continents are clamped
   to `(±17066.666, ±17066.666)` per `MAP_HALFSIZE` (§1). Anything outside is
   unreachable terrain (clipped by the client too).
3. **"Which mmtile file?"** — Use the formula in §3. Note the file-name digit
   ordering is `<map><tileY><tileX>`, with `tileY` listed first — easy to
   misread when grepping bake output.
4. **"Is the Z plausible?"** — See §5. There is no global walkable-Z clamp in
   the bake; the navmesh emits polygons wherever ADT/WMO geometry plus the
   walkable-slope filter agree.

If you find yourself trying to "fix pathfinding" for a coord whose tile is not
even baked, you are off the bake's grid; the failure surface is in the bake or
the map-id lookup, not in the runtime pathfinder.

---

## 1. Constants from MmapGen

All citations are from the in-tree generator under
`E:\repos\Westworld of Warcraft\tools\MmapGen\`.

### MaNGOS / mangos-zero core grid (`src/game/Maps/GridDefines.h`)

```c
#define MAX_NUMBER_OF_GRIDS      64                                    // line 38
#define SIZE_OF_GRIDS            533.33333f                            // line 40
#define CENTER_GRID_ID           (MAX_NUMBER_OF_GRIDS/2)               // line 41   -> 32
#define CENTER_GRID_OFFSET       (SIZE_OF_GRIDS/2)                     // line 43   -> 266.666666
#define MAP_SIZE                 (SIZE_OF_GRIDS*MAX_NUMBER_OF_GRIDS)   // line 58   -> 34133.33
#define MAP_HALFSIZE             (MAP_SIZE/2)                          // line 59   -> 17066.666
#define MAP_RESOLUTION           128                                    // line 56
```

Validation clamp (line 177-180):

```c
if (c > MAP_HALFSIZE - 0.5)       c = MAP_HALFSIZE - 0.5;
else if (c < -(MAP_HALFSIZE - 0.5)) c = -(MAP_HALFSIZE - 0.5);
```

`IsValidMapCoord` (line 188) rejects coords with `fabs(c) > MAP_HALFSIZE - 0.5`.

### Recast/Detour bake side (`contrib/mmap/src/`)

```c
// TerrainBuilder.h:59
static const float GRID_SIZE = 533.33333f;                       // tile width in WoW yards

// MapBuilder.h:41-46
const static float BASE_UNIT_DIM = 0.2666666f;                   // WoW yards per heightfield cell
const static int   VERTEX_PER_MAP  = int(GRID_SIZE/BASE_UNIT_DIM + 0.5f); // = 2000 per axis
const static int   VERTEX_PER_TILE = 80;                         // sub-tile vertex count (default)
const static int   TILES_PER_MAP   = VERTEX_PER_MAP / VERTEX_PER_TILE;    // = 25 sub-tiles / axis

// TerrainBuilder.h:63-64
static const float INVALID_MAP_LIQ_HEIGHT     = -500.f;
static const float INVALID_MAP_LIQ_HEIGHT_MAX = 5000.0f;
```

Continent topology summary (derived from the above):

| Quantity | Value |
|---|---|
| Tile side (WoW yards) | `533.33333` |
| Tiles per continent (max addressable) | `64 × 64 = 4096` |
| Continent half-extent (yards) | `17066.666` (`32 × 533.33333`) |
| Continent full extent (yards) | `34133.332` (`64 × 533.33333`) |
| Heightfield cell (WoW yards) | `0.2666666` |
| Heightfield cells per tile | `2000 × 2000` |
| Sub-tile-per-tile default | `25 × 25` (i.e. `TILES_PER_MAP²`) |
| Invalid-height sentinel (loader) | `-100000.0f` (`Exports/Navigation/MapLoader.h:34`) |
| Invalid-height sentinel (physics) | `-200000.0f` (`Exports/Navigation/PhysicsEngine.h:112`) |

### Runtime client (cross-check)

The decompiled 1.12.1 client carries the same constants
(`docs/disasm/wow_exe_decompilation.md:499-502`):

| Constant | Address | Value |
|---|---|---|
| Grid center | `0x7FFAB4` | `17066.666` |
| Grid extent | `0x7FFAB0` | `34133.332` |
| Grid scale | `0x810AE4` | `0.24` (= `1/4.16666...`) |
| Walkable Z (cos 50°) | `0x80DFFC` | `0.642788` |

The exe and the server agree to 5+ decimal places. There is no separate "TBC"
or "WotLK" extent in this bake — see §4.

---

## 2. Tile bounds expressed in world coords

Direct from `MapBuilder.cpp:412-428` (`MapBuilder::getTileBounds`):

```c++
bmax[0] = (32 - int(tileX)) * GRID_SIZE;   // world X max for this tile
bmax[2] = (32 - int(tileY)) * GRID_SIZE;   // world Y max for this tile
bmin[0] = bmax[0] - GRID_SIZE;             // world X min
bmin[2] = bmax[2] - GRID_SIZE;             // world Y min
```

Recast stores horizontal axes in `[0]` (X) and `[2]` (Z), with `[1]` reserved
for vertical. In this generator `Recast.X = WoW.X` and `Recast.Z = WoW.Y`,
which is why `bmin[2]/bmax[2]` corresponds to WoW Y.

Equivalent in plain text:

| Quantity | Formula |
|---|---|
| `worldX_max_of_tile` | `(32 − tileX) × 533.33333` |
| `worldX_min_of_tile` | `worldX_max − 533.33333` |
| `worldY_max_of_tile` | `(32 − tileY) × 533.33333` |
| `worldY_min_of_tile` | `worldY_max − 533.33333` |

`TileWorker.cpp:246-249` repeats the same math when computing per-tile sub-tile
extents, confirming this is the only convention in use inside the bake.

---

## 3. Tile ↔ world-coord math (the two conventions)

There are **two equally valid coordinate naming conventions** that produce the
same filenames and the same physics. They differ only in whether `tileX` is
"the index along WoW's X axis" or "the index that appears first in the
filename".

### Convention A — MmapGen C++ source convention

Used by `tools/MmapGen/contrib/mmap/src/MapBuilder.cpp` and `TileWorker.cpp`.
`tileX` is derived from `worldX` (with axis inversion), `tileY` from `worldY`.

```
tileX = floor(32 − worldX / 533.33333)        // index along WoW X
tileY = floor(32 − worldY / 533.33333)        // index along WoW Y
```

Verified against `MapBuilder.cpp:236-239` (`getGridBounds`) and the bounds
formulas in §2.

### Convention B — vmangos / mangos-zero / `.go` / DebugCommands convention

Used by `src/game/Commands/DebugCommands.cpp` (cited in
`docs/physics/MMAP_FORMAT.md:88-89`) and by the strict loader at
`Exports/Navigation/MapLoader.cpp:1114-1118`:

```c++
gridX = (CENTER_GRID_ID − worldY / GRID_SIZE);   // first filename index
gridY = (CENTER_GRID_ID − worldX / GRID_SIZE);   // second filename index
```

In this naming `tileX` (= `gridX`) is derived from `worldY`, and vice versa.
This matches the WoW client's ADT tile ordering and is what the player-typed
`.go` commands assume.

### Both conventions produce the same filename

Filenames are written as (`MapBuilder.cpp:290`,
`TileWorker.cpp:1014`):

```c++
sprintf(fileName, "mmaps/%03u%02u%02u.mmtile", mapID, tileY, tileX);
```

where the `tileX, tileY` symbols are MmapGen's (Convention A). The first
two-digit field after the map id is **always** the index derived from
**worldX**, the second is derived from **worldY**.

> ⚠ Memo trap: `MEMORY.md` and `docs/physics/MMAP_FORMAT.md` use Convention B,
> so their phrase "`tileX = floor((WORLD_MAX − worldY) / tileSize)`" looks like
> a typo but is actually correct in Convention B's naming. Always check which
> naming convention a doc is using before propagating its labels.

### Worked example — OG zeppelin (Frezza, Undercity → Grom'gol)

Frezza is at approximately `(worldX, worldY) = (-4290.0, 1318.0)`.

Convention A:

```
tileX_A = floor(32 − (-4290.0) / 533.33333) = floor(40.04) = 40   (worldX-axis)
tileY_A = floor(32 −  1318.0  / 533.33333) = floor(29.53) = 29   (worldY-axis)
```

Convention B (vmangos):

```
gridX_B = floor(32 −  1318.0  / 533.33333) = floor(29.53) = 29   (first filename slot)
gridY_B = floor(32 − (-4290.0) / 533.33333) = floor(40.04) = 40   (second filename slot)
```

Both produce `0012940.mmtile`:

```
sprintf("%03u%02u%02u", 1, 29, 40)  // = "0012940"
```

This file exists on disk under `D:\MaNGOS\data\mmaps\` and contains the OG /
UC zeppelin deck navmesh. The CLI to regenerate it uses Convention A:
`MmapGen.exe --tile 40,29` (worldX-axis first).

### `dtNavMeshParams.orig` (continent navmesh origin)

From `MapBuilder.cpp:370-381`, the per-map `dtNavMeshParams` is initialized
with `orig` set to the `bmin` of the highest-indexed populated tile, i.e. the
south-west corner of the densest tile. For both continents this is effectively
the negative half-extent corner:

```
orig ≈ (−17066.666, terrainZMin, −17066.666)
tileWidth = tileHeight = 533.33333
```

This is why Detour queries inside the continent reduce to:

```
tileXAtCoord = floor((worldX − orig[0]) / tileWidth)   // 0..63 increasing east
tileZAtCoord = floor((worldY − orig[2]) / tileWidth)   // 0..63 increasing north
```

The Detour tile-bits in a 64-bit polyref are a **slot index** into Detour's
per-map tile array, not directly `(tileX, tileY)`. Decode XY via world-coord
math (see `MEMORY.md` → "Phase 2 Surface F NEGATIVE").

---

## 4. Per-map baked extents (1.12.1 bake at `D:\MaNGOS\data\mmaps\`)

Source: directory listing of `D:\MaNGOS\data\mmaps\*.mmtile`. Filename digits
parsed as `<mapId:03d><tileY:02d><tileX:02d>` (Convention A). The
**WoW-physics tile space** columns use Convention A (`tileX` ← worldX).

| Map id | Name | Baked tiles | minTileX (worldX-axis) | maxTileX | minTileY (worldY-axis) | maxTileY | worldX range (yards) | worldY range (yards) |
|---|---|---:|---:|---:|---:|---:|---|---|
| 0 | Eastern Kingdoms | 515 | 25 | 44 | 22 | 61 | `−6933.33 .. +3733.33` | `−16000.00 .. +5333.33` |
| 1 | Kalimdor | 785 | 0 | 48 | 0 | 55 | `−9066.66 .. +17066.66` | `−12266.66 .. +17066.66` |

Per-map worked extent (Convention A):

- `worldX_min` = `(32 − maxTileX − 1) × 533.33333` = `(31 − maxTileX) × 533.33333`
- `worldX_max` = `(32 − minTileX) × 533.33333`
- `worldY_min` = `(31 − maxTileY) × 533.33333`
- `worldY_max` = `(32 − minTileY) × 533.33333`

### TBC / WotLK maps (530, 571) — not present

This is a 1.12.1 (Vanilla) bake. There are **no** Outland (530) or Northrend
(571) tiles in `D:\MaNGOS\data\mmaps\`. They were never baked because the
mangos-zero source tree under `tools/MmapGen/src/` does not generate them and
the 1.12.1 client does not load them.

For TBC/WotLK reference (from authoritative sources, not from this bake):

| Map id | Name | Tile-frame | Notes |
|---|---|---|---|
| 530 | Outland (Expansion01) | Same 64×64, GRID_SIZE 533.33, half-extent ±17066.666 | Out of scope for 1.12.1; would require a TBC-aware extractor. |
| 571 | Northrend | Same 64×64 grid | Out of scope for 1.12.1. |

The MaNGOS coordinate frame is **identical** across expansions: every WoW map
uses the same 64×64 ADT grid and the same `MAP_HALFSIZE = 17066.666`. The
expansion difference is which `.adt` cells are populated, not the addressing.

### Other 1.12.1 maps with baked tiles

| Map id | Tiles | minTileX | maxTileX | minTileY | maxTileY | Notes |
|---|---:|---:|---:|---:|---:|---|
| 13 | 36 | 29 | 34 | 29 | 34 | Testing Map (debug/development) |
| 29 | 36 | 29 | 34 | 29 | 34 | Scarlet Monastery — Library / Armory / Cathedral / Graveyard share |
| 30 | 27 | 30 | 34 | 29 | 35 | Alterac Valley |
| 33 | 24 | 25 | 29 | 30 | 34 | Shadowfang Keep |
| 34 | 4 | 31 | 32 | 31 | 32 | Stockade |
| 35 | 4 | 31 | 32 | 31 | 32 | (unused / placeholder) |
| 36 | 28 | 30 | 35 | 30 | 35 | Deadmines |
| 37 | 30 | 29 | 34 | 29 | 33 | Azshara Crater (unfinished) |
| 43 | 5 | 30 | 32 | 31 | 32 | Wailing Caverns |
| 44 | 4 | 31 | 32 | 31 | 32 | (unused) |
| 47 | 6 | 27 | 29 | 27 | 28 | Razorfen Kraul |
| 48 | 4 | 31 | 32 | 32 | 33 | Blackfathom Deeps |
| 70 | 3 | 31 | 32 | 31 | 32 | Uldaman |
| 90 | 6 | 30 | 32 | 32 | 33 | Gnomeregan |
| 109 | 4 | 31 | 32 | 32 | 33 | Sunken Temple |
| 129 | 4 | 29 | 30 | 26 | 27 | Razorfen Downs |
| 189 | 7 | 29 | 32 | 28 | 31 | Scarlet Monastery |
| 209 | 21 | 29 | 31 | 27 | 33 | Zul'Farrak |
| 229 | 6 | 31 | 33 | 31 | 32 | Blackrock Spire (LBRS/UBRS) |
| 230 | 7 | 31 | 33 | 29 | 32 | Blackrock Depths |
| 249 | 4 | 31 | 32 | 31 | 32 | Onyxia's Lair |
| 269 | 39 | 17 | 32 | 25 | 36 | Caverns of Time (Old Hillsbrad placeholder) |
| 289 | 16 | 30 | 33 | 29 | 32 | Scholomance |
| 309 | 25 | 33 | 37 | 52 | 56 | Zul'Gurub |
| 329 | 20 | 36 | 40 | 24 | 27 | Stratholme |
| 349 | 8 | 31 | 33 | 29 | 32 | Maraudon |
| 369 | 12 | 27 | 32 | 31 | 32 | Deeprun Tram |
| 389 | 4 | 31 | 32 | 31 | 32 | Ragefire Chasm |
| 409 | 7 | 32 | 34 | 29 | 31 | Molten Core |
| 429 | 5 | 30 | 31 | 30 | 32 | Dire Maul |
| 449 | 4 | 31 | 32 | 31 | 32 | Alliance PvP (Hall of Champions) |

### Maps with `.mmap` headers but zero baked tiles

These maps were *enumerated* by the bake (so a header file `<mapId>.mmap`
exists) but produced **no** mmtiles. They are typically battlegrounds or PvP
arenas where the source ADTs were empty/skipped by the build:

```
450 (Horde Hall of Champions)   451   469 (Blackwing Lair — see below)
489 (Warsong Gulch)             509 (AQ Ruins)   529 (Arathi Basin)
531 (Temple of Ahn'Qiraj)       533 (Naxxramas)
```

> Note: Map 469 (BWL) and 531 (AQ40) historically *do* have ADTs in the
> Vanilla client. Their absence from this bake is a known data gap; see
> `MEMORY.md` → "Phase 2 Surface H SHIPPED" and "BRM Phase 2 retry-prep" for
> ongoing work on the BWL portal / corridor + this missing-bake interaction.

---

## 5. Continent walkable Z ranges

The bake does **not** carry a fixed walkable-Z clamp. There is no
`WALKABLE_Z_MIN/MAX` constant in `tools/MmapGen/contrib/mmap/src/`. Recast's
own bounds (`config.bmin[1]`, `config.bmax[1]`) come from `rcCalcBounds` over
the actual ADT + WMO vertex set per tile, with `MapBuilder::getTileBounds`
seeding `bmin[1] = FLT_MIN` and `bmax[1] = FLT_MAX` when there are no source
verts (`MapBuilder.cpp:419-420`).

In practice the **walkable floor** stays inside a much narrower band than the
extracted geometry. Empirical / cross-reference numbers:

| Quantity | Value (WoW yards) | Source |
|---|---|---|
| Continent terrain seafloor (deepest natural ADT) | ≈ `-300` to `-500` | Visible in heightfield CSVs around continental shelf tiles (e.g. wetlands → sea). |
| Liquid invalid sentinel (min) | `-500.0` | `TerrainBuilder.h:63` `INVALID_MAP_LIQ_HEIGHT` |
| Liquid invalid sentinel (max) | `+5000.0` | `TerrainBuilder.h:64` `INVALID_MAP_LIQ_HEIGHT_MAX` |
| Highest typical walkable peak (1.12.1 continents) | ≈ `+750` (BWL spawn-side hall, AQ40 chamber roof) | Spot checks; not a hard constant. |
| Typical "ground" range across 1.12.1 outdoor zones | ≈ `−30 .. +250` | Spot checks; the bot's recovery code uses this implicitly when fall-checking. |
| Bot loader invalid-height sentinel | `-100000.0` | `Exports/Navigation/MapLoader.h:34` `INVALID_HEIGHT` |
| Bot physics invalid-height sentinel | `-200000.0` | `Exports/Navigation/PhysicsEngine.h:112` `INVALID_HEIGHT` |
| Walkable slope cos cutoff (client + bot) | `0.642788` (`cos 50°`) | `0x80DFFC` in client; mirrored in `MoveMapSharedDefines.h`. |

The two invalid-height sentinels are distinct on purpose — `MapLoader` uses
`-100000` for "no ADT cell at this XY", while `PhysicsEngine` uses `-200000`
for "no previous ground recorded". Code that filters one MUST not collapse
them to a single magic; see `MEMORY.md` → "round-4 iter-3" for the gate work
that depends on this distinction.

### Best-effort outdoor-zone Z bands (Eastern Kingdoms / Kalimdor)

Spot-checked against `D:\MaNGOS\data\mmaps` heightfield dumps and live
fixture traces. **These are not hard contract values — they are useful sanity
ranges only.**

| Region | Typical walkable Z |
|---|---|
| Stormwind / Elwynn / Westfall | `+0 .. +120` |
| Dun Morogh / Loch Modan / Wetlands | `+5 .. +220` |
| Burning Steppes / Searing Gorge | `+100 .. +250` |
| Blackrock Mountain spire (outdoor) | `+130 .. +220` |
| Eastern Plaguelands | `+50 .. +200` |
| Durotar / Barrens | `+0 .. +120` |
| Stonetalon / Mulgore | `+50 .. +400` |
| Winterspring | `+200 .. +500` |
| Felwood / Moonglade | `+100 .. +250` |

Anything outside `[-500, +800]` in the open world is almost certainly either
underwater (use liquid sentinel checks), underground (instance entrance
geometry), or a teleport / runtime drift bug.

---

## 6. Transports / zeppelins

`MmapGen` bakes a small set of transport vehicles as **standalone GameObject
mmtiles**, not as map ids. From `MapBuilder.cpp:751-769`:

```c++
buildGameObject("Transportship.wmo.vmo",      3015);  // sea ship
buildGameObject("Transport_Zeppelin.wmo.vmo", 3031);  // Horde zeppelins
buildGameObject("Elevatorcar.m2.vmo",          360);
buildGameObject("Undeadelevator.m2.vmo",       455);
buildGameObject("Ironforgeelevator.m2.vmo",    561);
buildGameObject("Gnomeelevatorcar01.m2.vmo",   807);
buildGameObject("Gnomeelevatorcar02.m2.vmo",   808);
buildGameObject("Gnomeelevatorcar03.m2.vmo",   827);  // missing vmap — reusing 03
buildGameObject("Gnomeelevatorcar03.m2.vmo",   852);
buildGameObject("Gnomehutelevator.m2.vmo",    1587);
buildGameObject("Burningsteppselevator.m2.vmo", 2454);
buildGameObject("Subwaycar.m2.vmo",           3831);
```

These are written to `mmaps/go<displayId:04u>.mmtile`
(`MapBuilder.cpp:712`), e.g.:

```
mmaps/go3031.mmtile   // zeppelin
mmaps/go3015.mmtile   // ship
mmaps/go0360.mmtile   // generic elevator car
```

`isTransportMap(mapID)` (`MapBuilder.cpp:469-480`) currently always returns
`false` — there are no top-level "transport map" ids in this 1.12.1 bake. The
older WotLK-style transports-as-maps (id ≥ 5000) do not apply here.

Practical implication: when the bot is on a zeppelin, its **continent-world
coords** still come from the OG / UC mainland map's ADT addressing. The
zeppelin deck navmesh is overlaid via the GO mmtile and the runtime adds it
through `dtNavMesh::addTile` against a *local* coordinate frame anchored to
the GameObject's current world position. Off-mesh / boarding hand-off between
the continent navmesh and the GO mmtile is the surface where the OG zeppelin
work in `MEMORY.md` lives.

> The decompiled client's terrain query also uses `17066.666` as the world
> origin (`docs/disasm/wow_exe_decompilation.md:499`). Transports do not
> shift this constant; they apply their own model-space transform to the
> player while leaving the world-coord query unchanged.

---

## 7. Quick-reference table

| Quantity | Value | Citation |
|---|---|---|
| Continent tile size (yards) | `533.33333` | `TerrainBuilder.h:59`, `GridDefines.h:40` |
| Continent tile count (max) | `64 × 64` | `GridDefines.h:38` |
| Continent half-extent (yards) | `17066.666` | `GridDefines.h:59` |
| Continent full extent (yards) | `34133.332` | `GridDefines.h:58`; client `0x7FFAB0` |
| Map id 0 (Eastern Kingdoms) baked tiles | `515` | disk |
| Map id 1 (Kalimdor) baked tiles | `785` | disk |
| Map id 530 (Outland) | **not baked** | absent from disk; not in 1.12.1 source |
| Map id 571 (Northrend) | **not baked** | absent from disk; not in 1.12.1 source |
| Mmtile filename pattern | `%03u%02u%02u.mmtile` (`map`,`tileY`,`tileX`) | `MapBuilder.cpp:290` |
| Continent navmesh origin (`orig`) | `(−17066.666, ?, −17066.666)` | `MapBuilder.cpp:370-381` + bounds math |
| Walkable slope cutoff (cos) | `0.642788` (50°) | `0x80DFFC` (client) |
| Loader INVALID_HEIGHT | `-100000.0` | `MapLoader.h:34` |
| Physics INVALID_HEIGHT | `-200000.0` | `PhysicsEngine.h:112` |

---

## 8. Gaps / open items

- **No hard walkable-Z range** is recorded in source. §5's outdoor-zone bands
  are spot checks, not contracts. If the bot needs a generic Z-sanity gate,
  use the sentinels in the quick-reference table, not the zone bands.
- **TBC/WotLK extents.** This monorepo bakes Vanilla only. The
  `MAX_NUMBER_OF_GRIDS = 64` constant is shared with later expansions, so the
  addressing math in §3 generalises; what changes between expansions is which
  ADTs are populated, not the frame.
- **Convention drift.** Two different naming conventions exist (§3) and
  several memos in `MEMORY.md` use Convention B labels while the source uses
  Convention A. When propagating a tile coord through code/docs, always
  re-verify against the filename (`%03u<tileY><tileX>`) instead of the label.
- **Maps with empty bakes** (450, 451, 469, 489, 509, 529, 531, 533) need a
  rebuild before they can be navigated. The bake currently emits an `.mmap`
  header but no tiles for them.
- **Transport hand-off frames.** §6 documents that GO mmtiles use a
  GameObject-local frame, but the contract for how `Detour` glues a
  GO mmtile into the continent navmesh at runtime is not exhaustively
  described here — see the OG zeppelin work in `MEMORY.md` and
  `Westworld of Warcraft/docs/physics/MMAP_FORMAT.md` for the surfaces.

---

*Generated 2026-05-14 from source under `E:\repos\Westworld of Warcraft\tools\MmapGen\`
and the on-disk bake at `D:\MaNGOS\data\mmaps\`. Update this doc whenever
`GridDefines.h`, `TerrainBuilder.h`, `MapBuilder.cpp`, or
`Exports/Navigation/MapLoader.cpp` change those constants.*
