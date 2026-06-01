# MmapGen Bake Recipe (Phase 0 audit output)

> Output of the Phase 0 audit for the
> [Comprehensive Pathfinding Test Plan](COMPREHENSIVE_TEST_PLAN.md).
> Captures **how instance maps actually bake today** so Phase 2 per-dungeon
> work has a known starting point.

## Headline finding

**Every vanilla dungeon (and several raids) is already baked in
`D:/wwow-bot/prod-data/mmaps/`.** This contradicts the test plan's initial
assumption that 19 of 21 dungeons would need to be baked from scratch.

Confirmed baked map IDs (from `D:/wwow-bot/prod-data/mmaps/*.mmap`):

| Map ID | Name | Tile count | Type |
|---|---|---|---|
| 000 | Eastern Kingdoms | many | continent |
| 001 | Kalimdor | many | continent |
| 013 | Testing | n/a | (dev) |
| 029 | Stockades-internal-test? | n/a | (dev) |
| 030 | Alterac Valley | n/a | BG |
| **033** | **Shadowfang Keep** | **25 tiles** | dungeon |
| **034** | **The Stockades** | **4 tiles** | dungeon |
| 035 | Stormwind Stockade alt | n/a | (dev) |
| **036** | **Deadmines** | **28 tiles** | dungeon |
| **043** | **Wailing Caverns** | **5 tiles** | dungeon |
| **047** | **Razorfen Kraul** | **6 tiles** | dungeon |
| **048** | **Blackfathom Deeps** | **4 tiles** | dungeon |
| **070** | **Uldaman** | **3 tiles** | dungeon |
| **090** | **Gnomeregan** | **6 tiles** | dungeon |
| **109** | **Sunken Temple** | **4 tiles** | dungeon |
| **129** | **Razorfen Downs** | **4 tiles** | dungeon |
| **189** | **Scarlet Monastery** (all 4 wings) | tiles present | dungeon |
| **209** | **Zul'Farrak** | tiles present | dungeon |
| **229** | **Blackrock Spire** (LBRS + UBRS) | tiles present | dungeon |
| **230** | **Blackrock Depths** | tiles present | dungeon |
| 249 | Onyxia's Lair | tiles present | raid |
| 269 | Caverns of Time test | n/a | (dev) |
| **289** | **Scholomance** | tiles present | dungeon |
| 309 | Zul'Gurub | tiles present | raid |
| **329** | **Stratholme** (Live + UD) | tiles present | dungeon |
| **349** | **Maraudon** | tiles present | dungeon |
| 369 | DEEPRUN_TRAM | tiles present | infrastructure |
| **389** | **Ragefire Chasm** | tiles present | dungeon |
| 409 | Molten Core | tiles present | raid |
| **429** | **Dire Maul** (East+West+North) | tiles present | dungeon |
| 449/450/451 | Test maps | n/a | (dev) |
| 469 | Blackwing Lair | tiles present | raid |
| 489 | Warsong Gulch | tiles present | BG |
| 509 | AQ20 | tiles present | raid |
| 529 | Arathi Basin | tiles present | BG |
| 531 | AQ40 | tiles present | raid |
| 533 | Naxxramas | tiles present | raid |

All **21 vanilla 5-mans plus the 5 raids and 3 battlegrounds** have baked
output already.

## How the bake works (driver mechanics)

Source: [`tools/MmapGen/contrib/mmap/src/MapBuilder.cpp:75-140`](../../../tools/MmapGen/contrib/mmap/src/MapBuilder.cpp).

1. `MmapGen.exe` discovers maps from the working directory by scanning
   `maps/*.wdt` (terrain index) and `vmaps/*.vmtree` (collision index).
2. Per-tile, `vmaps/*.vmtile` provides the WMO/M2 collision geometry.
3. **No DBC files are required at bake time** — terrain and collision are
   pre-extracted by an external pipeline upstream of this repo.
4. `shouldSkipMap()` (lines 430-467) has only three exclusion lists:
   - `--skipContinents` (default OFF): skips maps 0, 1
   - `--skipJunkMaps` (default ON): skips 42, 169
   - `--skipBattlegrounds` (default OFF): skips 30, 37, 489, 529
5. **Every other map ID gets baked if its source data is present** — no
   per-map config row is required.

This explains why maps 36 + 43 baked despite being absent from
[`tools/MmapGen/config.json`](../../../tools/MmapGen/config.json) (which
only has top-level entries for maps `"0"` and `"1"`): config provides
*per-tile parameter overrides* (capsule, climb, erosion, etc.), not the
authoritative bake list.

## Concrete bake recipe (single dungeon)

```powershell
# 1. Build MmapGen (one-time per checkout)
.\tools\MmapGen\build-mmapgen.ps1

# 2. Bake one map (example: Stockades = map 34)
Set-Location D:\wwow-bot\test-data
& "$repo\tools\MmapGen\build\MmapGen.exe" 34 `
    --threads 8 --silent `
    --configInputPath "$repo\tools\MmapGen\config.json" `
    --offMeshInput "$repo\tools\MmapGen\offmesh.txt"
# Output: D:\wwow-bot\test-data\mmaps\034.mmap + 0343YXX.mmtile per tile

# 3. (Optional) Validate against runtime physics
dotnet run --project tools\NavDataAudit\NavDataAudit.csproj -c Release -- `
    D:/wwow-bot/test-data --map 34 `
    --write-manifest tmp/test-runtime/results-navigation/map34.json

# 4. Promote test-data -> prod-data
.\tools\MmapGen\promote-mmaps.ps1 -Map 34

# 5. Restart consuming services so they pick up the new tiles
docker restart wwow-pathfinding wwow-scene-data
```

**Per-tile sweep helper** (the loop-24 pattern):

```powershell
.\tools\scripts\bake-tile.ps1 -Map 34 -Tiles "31,31;31,32;32,31;32,32" `
    -Variant baseline
# Snapshots existing tiles, runs MmapGen with --threads 1, dumps:
#   tmp/bake-sweeps/baseline-<timestamp>/bake-report.json
#   tmp/bake-sweeps/baseline-<timestamp>/bake.log
```

## Expected bake time per dungeon

Calibration from existing tile counts at `--threads 8`:

| Tile count | Dungeon examples | Wall time (rough) |
|---|---|---|
| 1-4 tiles | Stockades, BFD, RFD, Mara | ~20-60s |
| 5-10 tiles | WC, RFK, ST, Uldaman, Gnomer | ~60-180s |
| 11-25 tiles | SFK, DM, SM, BRD | ~3-10 min |
| 25+ tiles | LBRS+UBRS, Strat, DM-Tribute | ~10-20 min |

Per-tile knob tuning (when a dungeon path fails) is the long pole — that's
the 3-15 loops/dungeon estimate from the comprehensive test plan. A clean
"bake once and call it done" run is sub-20-minute work per dungeon.

## What's NOT confirmed (verify before Phase 2 promotion)

1. **Bake correctness** — the `.mmtile` files exist, but their walkability
   correctness inside dungeon geometry is **untested**. The 23/0/0
   prod-data sweep covers map 0, 1, 36 (DM entrance corridor), 43 (WC
   spiral entry). Deep-interior boss-chain coverage is what Phase 2 will
   prove.

2. **Off-mesh coverage** — [`offmesh.txt`](../../../tools/MmapGen/offmesh.txt)
   currently has exactly **one active connection** (OG zeppelin deck
   seam, line 38). Inside dungeons, off-mesh links commonly needed:
   - BRD elevator from Detention Block down to Shadowforge City
   - DM Tribute teleport pads to Cho'Rush
   - Gnomer launch pad (Crowd Pummeler arena → Engineer Thermaplugg)
   - Sunken Temple wing teleports
   - Strat Live/UD teleport pylons
   - Maraudon waterfall jumps

   These will surface in Phase 2 as `BlockedReason: "no route"` failures
   between boss waypoints, then get authored into `offmesh.txt` and
   re-baked for the affected tile.

3. **Source data freshness** — `maps/` + `vmaps/` may have been extracted
   from a different vanilla WoW client build than the one running on
   the LandSandBoat/MaNGOS server, causing geometry drift. Symptom would
   be paths that bake successfully but walk into invisible walls at
   runtime. Not common but possible — the FG screenshot pipeline catches
   this.

4. **Tile count completeness** — small-tile maps (Stockades = 4 tiles)
   may have unbaked tiles at the edges that don't contain walkable
   geometry. Confirming the bake covers the entire walkable interior
   per dungeon is a per-dungeon Phase 2 step.

## Impact on the comprehensive test plan

The plan's `BakeBlocked` status assumption was **wrong for all 21
dungeons**. Updated mapping:

| Original status | Revised status | Reason |
|---|---|---|
| `BakeBlocked` × 19 | `Experimental` × 19 | Mmtiles exist; waypoint authoring + sweep iteration only |
| `Experimental` × 2 (DM, WC) | `Experimental` × 2 | Unchanged — need boss-chain extension beyond existing entrance coverage |

`BakeBlocked` remains in the enum for future use cases (e.g., raid maps
we explicitly opt out of for now, or maps whose source data is missing
on a fresh checkout).

Phase 2 calibration revises **down**:
- "Bake the instance map" step is largely already done (verify-only).
- Per-dungeon iteration becomes "author waypoints + sweep BG-only +
  flip Stable" — closer to the optimistic 2-3 loops/dungeon than the
  pessimistic 8-15.
- Total Phase 2 effort: revised from ~80-150 loops to **~50-80 loops**.

## Files referenced

- [`tools/MmapGen/contrib/mmap/src/MapBuilder.cpp`](../../../tools/MmapGen/contrib/mmap/src/MapBuilder.cpp) — bake driver (discoverTiles + shouldSkipMap)
- [`tools/MmapGen/contrib/mmap/src/generator.cpp`](../../../tools/MmapGen/contrib/mmap/src/generator.cpp) — CLI entry (argv parsing, buildAllMaps)
- [`tools/MmapGen/config.json`](../../../tools/MmapGen/config.json) — per-tile param overrides (NOT a bake whitelist)
- [`tools/MmapGen/offmesh.txt`](../../../tools/MmapGen/offmesh.txt) — explicit off-mesh connections
- [`tools/MmapGen/build-mmapgen.ps1`](../../../tools/MmapGen/build-mmapgen.ps1) — MmapGen.exe builder
- [`tools/MmapGen/promote-mmaps.ps1`](../../../tools/MmapGen/promote-mmaps.ps1) — test-data → prod-data
- [`tools/scripts/bake-tile.ps1`](../../../tools/scripts/bake-tile.ps1) — per-tile sweep helper (loop-24 pattern)
- `D:/wwow-bot/test-data/maps/` + `vmaps/` — source data root
- `D:/wwow-bot/prod-data/mmaps/` — production bake output (consumed by services)
