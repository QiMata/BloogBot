# Pathfinding iteration loop

> **Companion docs:**
> [MMAP_DATA_FLOW.md](MMAP_DATA_FLOW.md) (test/prod isolation),
> [PATHFINDING_OVERHAUL.md](PATHFINDING_OVERHAUL.md) (freeze contract),
> [MMAP_NAVMESH_GENERATION.md](MMAP_NAVMESH_GENERATION.md) (capsule constants).

A reproducible `bake -> probe -> test -> compare` loop for tuning the
MmapGen bake parameters. Built on top of the existing tools (`MmapGen.exe`,
`PathPhysicsProbe.exe`, `PathfindingTestFixture`, `dotnet test`) plus the
PFS-OVERHAUL-006 strict-data-dir gate. Every iteration produces a
self-contained variant directory under `tmp/bake-sweeps/<variant>-<UTC>/`
containing the snapshot, the effective config, the probe output, and the
test results -- so you can compare two bakes side-by-side and revert
either of them with one command.

## TL;DR

```powershell
# Run the full loop on the current config (rebake (1,29,40), probe canonical
# routes, run unit + physics + climb tests).
.\tools\scripts\iterate-pathfinding.ps1 `
    -Variant baseline `
    -Map 1 -Tiles "29,40" `
    -RouteManifest tools\scripts\routes\og-zeppelin.json `
    -RunTests "unit,physics,climb" `
    -DataDir D:\MaNGOS\data
```

Output: `tmp\bake-sweeps\baseline-<UTC>\iterate-report.md` plus the
machine-readable `iterate-report.json`.

## The scripts

| Script | Purpose |
|---|---|
| `tools\scripts\bake-tile.ps1` | Snapshot affected tile(s), invoke `MmapGen.exe`, write `bake-report.json`. |
| `tools\scripts\restore-mmaps.ps1` | Roll a sweep back from its snapshot. |
| `tools\scripts\probe-routes.ps1` | Run `PathPhysicsProbe.exe` over a JSON route manifest, aggregate per-route results. |
| `tools\scripts\run-pathfinding-tests.ps1` | Uniform `dotnet test` runner (consistent env vars, trx + console capture, pass/fail summary). Test sets: `unit`, `physics`, `pathfinding`, `climb`, `all`. |
| `tools\scripts\iterate-pathfinding.ps1` | Orchestrator. Calls the others, copies their artifacts into one variant dir, writes `iterate-report.{md,json}`. |
| `tools\scripts\verify-strict-gate.ps1` | Negative-test the PFS-OVERHAUL-006 `WWOW_DATA_DIR` strict gate. Exit 0 = gate behaves; exit 1 = a FATAL branch silently passed. |

## Bake parameter knobs

`tools/MmapGen/config.json` is the bake input. Schema:

```json
{
  "default": { ... },          // read by tools/NavDataAudit only
  "<mapId>": {                 // global-for-this-map
    "agentRadius": 1.0247,     // WoW meters; Tauren M baseline
    "agentHeight": 2.625,
    "walkableRadius": 0,       // 0 = auto-derive (agentRadius / cs)
    "walkableHeight": 0,       // 0 = auto-derive (agentHeight / ch)
    "walkableClimb":  0,       // 0 = auto-derive
    "walkableSlopeAngle": 75.0,        // (terrain) -- degrees, default 75
    "walkableSlopeAngleVMaps": 61.0,   // (WMO/model) -- degrees, default 61
    "<tileXtileY>": {                  // per-tile override (e.g. "2940" = X=29 Y=40)
      "cs": 0.1,
      "ch": 0.1,
      "maxSimplificationError": 1.0,
      "detailSampleDist": 0.5,
      "agentRadius": 1.0247,           // can shadow at tile level too
      "walkableSlopeAngleVMaps": 50.0
    }
  }
}
```

The full read sites are in
`tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
- `getDefaultConfig()` (line 953) -- the hardcoded fallback.
- `getMapIdConfig(mapId)` (line 973) -- merges map-level over default.
- `getTileConfig(mapId, tileX, tileY)` (line 984) -- merges per-tile over map.

Each call to `MmapGen.exe --tile X,Y` reads the merged config for that tile.

### Agent constants (from MMAP_NAVMESH_GENERATION.md)

| | radius | height |
|---|---|---|
| Tauren Male (largest) | 1.0247m | 2.625m |
| Smaller races | (auto-shrink) | (auto-shrink) |

Continent maps must be generated for Tauren Male; smaller capsules use the
same mesh.

### Don't tighten without intent

`feedback_pathfinding_anti_patterns.md` (auto-memory) names two specific
moves the user has flagged as anti-patterns:

1. **Lowering `walkableSlopeAngle` / `walkableSlopeAngleVMaps`** below the
   harvested client values to make a single steep route fail. These are
   load-bearing across Stormwind harbor ramps, Ironforge tram, every
   tower with intentionally steep stair sections.
2. **Lowering `walkableClimb`** below the harvested 1.8 to make a single
   step-up edge non-walkable.

The intended fix surface is `cs` / `ch` / `maxSimplificationError` -- bake
fidelity. Use `walkableSlopeAngle` / `walkableClimb` sweeps as an
investigative knob (e.g. "what if we tightened slope to 55deg, what fails?")
**only when the user has explicitly authorized that override** for the
investigation.

## End-to-end loop

```
                                                  +----------------+
                                                  |  config.json   |
                                                  |  offmesh.txt   |
                                                  +-------+--------+
                                                          |
                                                          v
+---------------+       +---------------+       +-------------------+
| iterate-      |  -->  | bake-tile.ps1 |  -->  | MmapGen.exe       |
| pathfinding   |       | (snapshot)    |       | (per-tile rebake) |
+---------------+       +---------------+       +-------------------+
        |                                                |
        |                                                v
        |                                       +-------------------+
        |                                       | mmaps/*.mmtile    |
        |                                       | (test-data dir)   |
        |                                       +-------------------+
        |
        |                +---------------+      +-------------------+
        +--------------> | probe-routes  | ---> | PathPhysicsProbe  |
        |                | (manifest)    |      | (segment classify)|
        |                +---------------+      +-------------------+
        |                                                |
        |                                                v
        |                                       +-------------------+
        |                                       | probe-results.json|
        |                                       +-------------------+
        |
        |                +---------------+      +-------------------+
        +--------------> | run-pf-tests  | ---> | dotnet test       |
                         | (unit/phys/.. |      | (filtered)        |
                         |  /climb)      |      +-------------------+
                         +---------------+               |
                                                         v
                                                +--------------------+
                                                | tests\<set>.trx    |
                                                | tests\<set>.console|
                                                +--------------------+

After all three steps:
  tmp\bake-sweeps\<variant>-<UTC>\iterate-report.{md,json}
```

## Recipes

### Validate the strict gate

```powershell
.\tools\scripts\verify-strict-gate.ps1
# Exit 0 = all FATAL branches fired correctly.
```

Run this any time a Navigation.dll caller is added/changed.

### Confirm current bake is "still good" (smoke loop)

```powershell
.\tools\scripts\iterate-pathfinding.ps1 -Variant smoke -SkipBake `
    -RouteManifest tools\scripts\routes\og-zeppelin.json `
    -RunTests "unit,physics" `
    -DataDir D:\MaNGOS\data
```

No bake, just probe + unit/physics. Quick (~60 seconds).

### Sweep `cs` for tile (1,29,40)

Edit `tools/MmapGen/config.json` -- e.g. set `"1": { "2940": { "cs": 0.05 } }`,
then:

```powershell
.\tools\scripts\iterate-pathfinding.ps1 -Variant cs-005 -Map 1 -Tiles "29,40" `
    -RouteManifest tools\scripts\routes\og-zeppelin.json `
    -RunTests "unit,physics,climb" `
    -DataDir D:\MaNGOS\data
```

Compare against `tmp\bake-sweeps\baseline-<UTC>\iterate-report.json`.

### Sweep `walkableSlopeAngle` (anti-pattern -- investigative only)

```powershell
# 1. Edit tools/MmapGen/config.json: set "1": { "walkableSlopeAngle": 55.0 }
# 2. Run iterate-pathfinding with a clearly-named variant
.\tools\scripts\iterate-pathfinding.ps1 -Variant slope-55-INVESTIGATIVE `
    -Map 1 -Tiles "29,40" `
    -RouteManifest tools\scripts\routes\og-zeppelin.json `
    -RunTests "unit,physics" `
    -DataDir D:\MaNGOS\data
# 3. Compare probe-results.json against baseline
# 4. RESTORE before any commit
.\tools\scripts\restore-mmaps.ps1 -Variant slope-55-INVESTIGATIVE-<UTC>
# 5. git checkout tools/MmapGen/config.json
```

### Roll back a sweep

```powershell
.\tools\scripts\restore-mmaps.ps1 -Variant <variant>-<UTC>
docker restart wwow-pathfinding wwow-scene-data    # if testing against prod
```

The `restore-mmaps.ps1` script reads `bake-report.json` from the sweep dir
to find the right tile paths and source `dataDir` -- you do not have to
re-specify them.

## Test data dir choices

Per [MMAP_DATA_FLOW.md](MMAP_DATA_FLOW.md):

- `D:/wwow-bot/test-data/` -- the bake/iteration sandbox. Mutable. Use this
  for any `MmapGen.exe` write. **Note**: first path query against test-data
  is currently 30s+ slower than against MaNGOS data due to NTFS junction
  reparse overhead + cold OS file cache. PFS-OVERHAUL-006 added a
  `PRELOAD_COMPLETE` log marker that `PathfindingTestFixture` polls for, so
  the first request no longer blocks behind cold preload -- but the
  iteration loop still runs faster against `D:/MaNGOS/data` for the climb
  test until the bake parameters stabilize.
- `D:/MaNGOS/data/` -- the server's data dir. Read-only for the bot. Use as
  the climb-iteration "working" config until the test-data first-path
  latency is rooted out.
- `D:/wwow-bot/prod-data/` -- the Docker `wwow-pathfinding` mount. Promote
  with `tools/MmapGen/promote-mmaps.ps1` once a sweep is signed off.

## Done criteria for a sweep variant

A sweep is "shippable" (promotable to prod-data) when:

1. `bake-report.json` has `bakeExitCode=0` and every affected tile has a
   non-null `afterLen`.
2. `probe-results.json` shows the canonical climb routes
   (`ClimbOrgrimmarTowerToFrezza`, `FlightMasterDescentControl`,
   `OgFrezzaToBoardingPosition`) with the expected affordance / segment
   count, and **no regression** vs the baseline variant.
3. `tests\test-summary.json` reports `unit` + `physics` green (213+7 unit,
   19/19 physics).
4. `iterate-report.md` shows `overall exit: 0` (or, for the climb sub-test,
   the canonical `(1338,-4646,51.6) flags=0x1` Phase 5.3.6 stall -- not a
   regression to a worse stall).
5. `tools/NavDataAudit` (when wired up) signs off on the new manifest.

Promote with `tools/MmapGen/promote-mmaps.ps1 -Map 1 -Tiles "29,40"` and
restart Docker.
