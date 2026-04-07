# Master Tasks — Test & Validate Everything

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker).
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. **GM Mode OFF after setup** — `.gm on` corrupts UnitReaction bits. Always `.gm off` before test actions.
7. **Kill WoW.exe before building.** DLL injection locks output files.
8. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-07)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1437 | 0 | 1 | +20 SceneData pipeline tests |
| Navigation.Physics.Tests | 667 | 2 | 1 | +1 extractor test, 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | Confirmed |
| BotRunner.Tests (LiveValidation) | 7+ | 0 | 0 | BasicLoop + DualClientParity pass |
| WSG (20 bots) | 12/20 entered | — | — | First successful BG entry |
| Tile coordinate tests | 5 | 0 | 0 | WorldToTile mapping verified |

---

## R1-R7 + R8 partial — Archived (see docs/ARCHIVE.md)

---

## R8 — Tile-Based Scene Architecture (Priority: CRITICAL — IN PROGRESS)

**Goal:** Replace AABB-based scene data (50K triangle cap, cache replacement on boundary crossing) with tile-based loading (533y ADT tiles, 3×3 neighborhood, additive merge). Scales to 3000+ bots at ~2.3MB/bot.

### Completed
- Proto: `SceneTileRequest/SceneTileResponse` defined + generated
- Splitter: 142 `.scenetile` files extracted from 5 maps (35s)
- Server: `SceneTileSocketServer` pre-loads all tiles, serves by key
- `GetGroundZ` fixed: downward ray (no more roof landing)
- Tile coordinate tests: 5 tests pass
- Docker containers redeployed fresh

### Outstanding

| # | Task | Details |
|---|------|---------|
| 1 | **SceneDataClient tile requests** | Change from AABB `SceneGridRequest` to tile-based `SceneTileRequest`. Compute 3×3 tile neighborhood from bot position. Request only tiles not already loaded. |
| 2 | **C++ InjectSceneTriangles: ADD mode** | Currently `SetSceneCache` REPLACES the entire cache. Need additive merge — inject new tile triangles INTO the existing SceneCache without destroying previous tiles. Or: manage per-tile SceneCaches in C++ and query all of them. |
| 3 | **Bot tile tracking** | Track which tiles are loaded (HashSet of tile keys). On position update, compute needed 3×3, load missing tiles, unload tiles outside 5×5 eviction radius. |
| 4 | **Remove SetSceneSliceMode** | Delete `SetSceneSliceMode` from NativeLocalPhysics, NativePhysicsInterop, MovementController, SceneQuery. Bot loads tiles directly — no slice mode toggle needed. |
| 5 | **Docker: tile deployment** | Copy `Data/scenes/tiles/` into SceneDataService container. Rebuild + deploy. Verify tile server starts in tile mode. |
| 6 | **Tile boundary crossing test** | Bot navigates across a tile boundary (e.g., Orgrimmar → Valley of Trials). Assert: no position teleports, no collision geometry loss, smooth movement. |
| 7 | **Tile merge correctness test** | Load 3×3 tiles, verify GetGroundZ returns valid Z at all 9 tile centers. Verify no gaps at tile boundaries. |
| 8 | **Physics regression test** | Run Navigation.Physics.Tests with tile-based loading. All 667 must pass. |
| 9 | **LiveValidation with tiles** | BasicLoopTests + CornerNav + DualClientParity must pass with tile-based scene data. |

---

## Deferred Issues

| # | Issue | Details |
|---|-------|---------|
| D1 | **Alliance faction bots** | StateManager doesn't launch AVBOTA* accounts — needs settings support |
| D2 | **BG queue pop** | AB/AV queued but never popped — server-side BG matching config |
| D3 | **WSG transfer stalls** | 8/20 bots didn't complete map transfer |
| D4 | **Elevator physics** | 2 pre-existing Navigation.Physics.Tests failures |

---

## Canonical Commands

```bash
# Kill everything before building
taskkill //F //IM WoW.exe 2>/dev/null
taskkill //F //IM BackgroundBotRunner.exe 2>/dev/null
taskkill //F //IM WoWStateManager.exe 2>/dev/null
taskkill //F //IM testhost.x86.exe 2>/dev/null

# Build .NET + C++ (both architectures)
dotnet build WestworldOfWarcraft.sln --configuration Release
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "FullyQualifiedName~LiveValidation" --no-build --blame-hang --blame-hang-timeout 10m

# Scene tile splitting
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --filter "FullyQualifiedName~SplitSceneFilesIntoTiles" --no-build -v n

# Docker rebuild + deploy
docker compose -f docker-compose.vmangos-linux.yml build scene-data-service
docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service
```
