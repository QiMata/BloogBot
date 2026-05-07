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

### Releasing a bake to production (Docker)

1. From repo root:
   ```powershell
   .\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "29,40"
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
| `WWOW_DATA_DIR` | (must be set explicitly) | `PathfindingService.exe` runtime |
| `WWOW_BOT_PROD_DATA_DIR` | `D:/wwow-bot/prod-data` | `docker-compose.vmangos-linux.yml` for `wwow-pathfinding` + `wwow-scene-data` mounts |
| `WWOW_VMANGOS_DATA_DIR` | `D:/MaNGOS/data` | the MaNGOS `wow-mangosd` container only — **don't reuse for bot services** |

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
