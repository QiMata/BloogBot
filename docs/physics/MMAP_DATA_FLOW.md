# MMAP Data Flow — test vs prod isolation

> **PFS-OVERHAUL-006 (2026-05-07).** Establishes a strict separation
> between the **MaNGOS server's** data directory and the **WWoW bot's**
> nav-data directories so test runs can iterate freely without poisoning
> production state.

## TL;DR

Three directories. Each has a clear owner.

| Path | Owner | Mutability | Purpose |
|---|---|---|---|
| `D:/MaNGOS/data/` | the MaNGOS server (`wow-mangosd` container, the WoW client extraction) | **read-only for the bot** | Authoritative `maps/` and `vmaps/` (extracted from the WoW client) plus the server's own `mmaps/` if it has any. The bot pipeline never writes here. |
| `D:/wwow-bot/test-data/` | this repo, exercised by `PathfindingTestFixture` | freely mutable by tests | Per-machine test scratch. `mmaps/` is what `MmapGen.exe` writes; `maps/` and `vmaps/` are NTFS junctions to `D:/MaNGOS/data/`. |
| `D:/wwow-bot/prod-data/` | the Docker `wwow-pathfinding` + `wwow-scene-data` services | only mutated by the **promote** script | Production input mounted into the bot containers. `mmaps/` is COPIED from `test-data/mmaps/` via `tools/MmapGen/promote-mmaps.ps1` once a bake is signed off. `maps/` and `vmaps/` are junctions, same as test-data. |

The MaNGOS server still mounts its own `D:/MaNGOS/data/` directly (line 61
of `docker-compose.vmangos-linux.yml`); that's correct — the server has
its own consumption pattern and writes things like generated content to
its data dir.

## Pipeline

```
┌─────────────────────────┐
│ MaNGOS / WoW client     │
│ extraction              │
│   D:/MaNGOS/data/maps   │
│   D:/MaNGOS/data/vmaps  │ ← read-only client geometry, source of truth
└────────────┬────────────┘
             │ NTFS junction
             ↓
┌─────────────────────────┐
│ test-data               │   PathfindingTestFixture     ┌──────────────────────┐
│   D:/wwow-bot/test-data │ ─ spawns ─→ PathfindingService.exe │ live tests pass green │
│     mmaps/  ← MmapGen   │                              │ → human signs off    │
│     maps/   ← junction  │                              └──────────┬───────────┘
│     vmaps/  ← junction  │                                         │
└────────────┬────────────┘                                         │
             │ promote-mmaps.ps1                                    │
             ↓                                                      ↓
┌─────────────────────────┐                              ┌──────────────────────┐
│ prod-data               │ ──→ docker compose ──→       │ docker restart       │
│   D:/wwow-bot/prod-data │     wwow-pathfinding         │ wwow-pathfinding     │
│     mmaps/  ← copied    │     wwow-scene-data          │ wwow-scene-data      │
│     maps/   ← junction  │     (mounts /wwow-data:ro)   │                      │
│     vmaps/  ← junction  │                              └──────────────────────┘
└─────────────────────────┘
```

## One-time setup (per machine)

Junction the read-only client data into both bot dirs:

```powershell
mkdir D:\wwow-bot\test-data\mmaps
mklink /J D:\wwow-bot\test-data\maps  D:\MaNGOS\data\maps
mklink /J D:\wwow-bot\test-data\vmaps D:\MaNGOS\data\vmaps

mkdir D:\wwow-bot\prod-data\mmaps
mklink /J D:\wwow-bot\prod-data\maps  D:\MaNGOS\data\maps
mklink /J D:\wwow-bot\prod-data\vmaps D:\MaNGOS\data\vmaps
```

If `D:/MaNGOS/data/mmaps/` has a usable starting set, copy it as the
initial seed for both `test-data/mmaps/` and `prod-data/mmaps/`.

## Workflow

### Iterating on a bake

1. Run `MmapGen.exe` from `D:/wwow-bot/test-data/` (or pass `--data-dir`
   if MmapGen ever supports it). Output lands in `test-data/mmaps/`.
2. Re-run the focused live test (`ClimbOrgrimmarZeppelinTowerRampToFrezza`,
   etc.). `PathfindingTestFixture` spawns `PathfindingService.exe` against
   `WWOW_TEST_DATA_DIR=D:/wwow-bot/test-data` (default). If you tweaked
   bake params, the test sees the new tile.
3. Iterate.

### Visual diagnostics before code changes

Before changing bake precision, Detour query behavior, or BotRunner movement
execution, generate the stable visual bundle:

```powershell
$env:WWOW_DATA_DIR = 'D:\MaNGOS\data'
.\tools\scripts\export-pathfinding-reference.ps1 -Route all -Resume
.\tools\scripts\summarize-pathfinding-reference.ps1 -Route all
```

Artifacts are overwritten under
`tmp/test-runtime/visualization/pathfinding/<route>/latest/` and categorized as
`source/`, `mmap/`, `overlays/`, `analysis/`, and `logs/`. See
[`PATHFINDING_VISUAL_DIAGNOSTICS.md`](PATHFINDING_VISUAL_DIAGNOSTICS.md) for
the inspection order and current OG/BRD findings.

Use `-RefreshRaw` only when the raw MmapGen debug geometry must be regenerated.
Normal `-Resume` avoids expensive rebuilds and keeps the latest reference set
small.

### Releasing a bake to production (Docker)

1. From repo root:
   ```powershell
   .\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "40,29"
   ```
   (Or `promote-mmaps.ps1` alone to promote everything in test-data.)
2. The script copies `test-data/mmaps/{tile}.mmtile` → `prod-data/mmaps/`.
3. Restart Docker so the live services reload:
   ```powershell
   docker restart wwow-pathfinding wwow-scene-data
   ```

### Confirming the right data is loaded

`PathfindingService.exe` logs `[PathfindingService] WWOW_DATA_DIR set
to: <path>` on startup. Tests load from `D:/wwow-bot/test-data/`;
production loads from `/wwow-data` which is `D:/wwow-bot/prod-data/`.
If you ever see a different dir, somebody set `WWOW_DATA_DIR`
explicitly — track it down before drawing conclusions about test
results.

## Env vars

| Env var | Default | Used by |
|---|---|---|
| `WWOW_TEST_DATA_DIR` | `D:/wwow-bot/test-data` | `PathfindingTestFixture` |
| `WWOW_VALIDATION_DATA_DIR` | `D:/wwow-bot/test-data` | `PathfindingValidationFixture` (waypoint-correctness suite) |
| `WWOW_DATA_DIR` | (must be set explicitly) | `PathfindingService.exe` runtime |
| `WWOW_BOT_PROD_DATA_DIR` | `D:/wwow-bot/prod-data` | `docker-compose.vmangos-linux.yml` for `wwow-pathfinding` + `wwow-scene-data` mounts |
| `WWOW_VMANGOS_DATA_DIR` | `D:/MaNGOS/data` | the MaNGOS `wow-mangosd` container only — **don't reuse for bot services** |

## Pathfinding service port allocation (PFS-OVERHAUL-006 / Phase 6)

| Port | Purpose | Owner |
|---|---|---|
| `9002` | Production Docker (`wwow-pathfinding` container) | docker compose |
| `5101` | Live-bot test fixture (`PathfindingTestFixture`) | `BotRunner.Tests` LiveValidation |
| `5111` | Waypoint-correctness fixture (`PathfindingValidationFixture`) | `PathfindingService.Tests/WaypointGeneration/*` |

The validation fixture exists to keep the bake-fidelity gate (`WaypointGenerationTests`)
reading from `D:/wwow-bot/test-data` regardless of what the live live-bot fixture is
doing. Both can run in parallel without contention. Override the validation port via
`WWOW_VALIDATION_PATHFINDING_PORT`. The fixture only spawns `PathfindingService.exe`
when `WWOW_USE_VALIDATION_PATHFINDING_SERVICE=1` is set; the default code path is
direct P/Invoke into `Navigation.dll` loaded into the test process, with the fixture
ensuring `WWOW_DATA_DIR` is set to test-data before any P/Invoke fires.

## Tile-coordinate convention source of truth (corrected 2026-05-12)

`MmapGen.exe --tile X,Y` interprets its first argument as `tileX` and the
second as `tileY`. In the vendored vmangos/CMaNGOS generator, `tileX` indexes
the world Y axis and `tileY` indexes the world X axis. The generated filename is
`<map><tileY:02d><tileX:02d>.mmtile`.

Concretely:

- OG zeppelin coords around `(1338, -4646, 51.6)` live in MmapGen tile
  **(tileX=40, tileY=29)**.
- `MmapGen.exe 1 --tile 40,29` writes `mmaps/0012940.mmtile`, which is the
  runtime tile loaded for the Orgrimmar tower.
- The per-tile config key is `"4029"` because `TileWorker::getTileConfig`
  concatenates `tileX` then `tileY`.
- `0014029.mmtile` is not the Orgrimmar zeppelin-tower tile. Any visualization
  that shows Feralas/Azshara/Darnassus/Dire Maul style assets for this path is
  using the swapped tile/order.

When in doubt, recompute from world coordinates and then confirm the generator
output filename:

```
tileX = floor((17066.6664 - worldY) / 533.3333)
tileY = floor((17066.6664 - worldX) / 533.3333)
filename = <map><tileY:02d><tileX:02d>.mmtile
```

Detour vertices from these tiles are in `(WoW Y, WoW Z, WoW X)` order. Tools
must convert them back before overlaying path coordinates recorded as normal
WoW `(X, Y, Z)`.

## Temporary fallback for live tests (PFS-OVERHAUL-006 Cycle 14, 2026-05-07)

The structural separation works, but the first path request against
`D:/wwow-bot/test-data` is ~30 seconds slower than against
`D:/MaNGOS/data` despite bit-identical mmtile content (verified via
`fc /b` and `Get-FileHash`). The climb sub-test's 20-second
`SnapshotStallGuard` fires before the path arrives, so the bot can't
move. Likely causes (uninvestigated yet): NTFS junction reparse
overhead on the `vmaps/` and `maps/` symlinks, cold OS file cache on
the test-data dir, or `WWOW_NAVIGATION_PRELOAD_MAPS=all` blocking the
first request behind a cold preload.

Until that is rooted-out, **set `WWOW_DATA_DIR=D:/MaNGOS/data` on
live BotRunner.Tests runs** (and `WWOW_TEST_DATA_DIR` to match). The
canonical Phase 5.3.6 stall reproduces (`flags=0x1=WALK`, ~470 waypoints
walked, ending at `(1338.1,-4646.0,51.6)`). Pathfinding unit/smoke tests
in `Tests/PathfindingService.Tests` are unaffected and can keep using
the test-data dir.

When the latency is fixed, delete this section and switch the climb
runs back to `D:/wwow-bot/test-data`.

## Strict `WWOW_DATA_DIR` gate (PFS-OVERHAUL-006 Cycle 14, 2026-05-07)

`Navigation.dll` now mirrors `PathfindingService.exe`'s `Program.cs` strictness:
- `Exports/Navigation/DllMain.cpp::InitializeAllSystems` `std::exit(1)` if
  `WWOW_DATA_DIR` is unset OR doesn't contain `mmaps/`+`maps/`+`vmaps/`.
- `Exports/Navigation/Navigation.cpp::GetMmapsPath` and
  `Exports/Navigation/VMapFactory.cpp::getVMapsPath` removed their
  cwd / DLL-relative / DLL-parent / last-resort fallbacks; they `std::exit(1)`
  on a missing env var or missing subdir.
- The previous fallbacks silently picked up stale build-output dirs
  (`Bot/Release/net8.0/mmaps/`, `Data/mmaps/`, etc.) that hadn't been
  regenerated since March 2026 — bake-parameter experiments looked like
  no-ops because tests were exercising 2-month-old tile data.

The escape hatch for in-process consumers (e.g. ForegroundBotRunner injected
into WoW.exe) is the existing `extern "C" SetDataDirectory(const char*)`
P/Invoke, which writes the env var natively — `WoWSharpClient`'s
`NativeLocalPhysics.EnsureInitialized` calls it before any other Navigation
export, so FG-injection still works as long as the C# resolver finds a Data
dir at injection time.

## Why this matters

Before this isolation, the bot's pathfinding service mounted the SAME
`D:/MaNGOS/data` directory as the MaNGOS server. Worse, the
`PathfindingService.exe`'s `Program.cs` resolver would walk parent
directories looking for any `mmaps/` it could find, silently falling
back to stale build-output mirrors (`Bot/Release/net8.0/mmaps/`,
`Data/mmaps/`, etc.) that hadn't been regenerated since March 2026.
Bake parameter experiments (`ch`, `cs`, `walkableClimb`, `maxSimplificationError`)
appeared to have NO effect on test results because tests were
exercising 2-month-old tile data. The diagnostic was hiding under a
file-system bug.

The fix is structural: own the directories, fail hard when the env
var isn't set, separate test from prod with a single explicit
"promote" step. With this in place, every test run honestly reflects
the current bake state, and no bot iteration can poison the server's
data dir.
