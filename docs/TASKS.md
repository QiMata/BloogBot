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

## Test Baseline (2026-04-07 — All phases complete)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1437 | 0 | 1 | +20 SceneData pipeline tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | Confirmed |
| BotRunner.Tests (LiveValidation) | 7+ | 0 | 0 | BasicLoop + DualClientParity pass |
| WSG (20 bots) | 12/20 entered | — | — | First successful BG entry |
| AV (80 bots) | 37/40 Horde | — | — | Alliance not launched |

---

## R1-R7 — All Complete (see docs/ARCHIVE.md)

---

## R8 — Movement & Communication Fixes (Priority: CRITICAL)

### P1: Tile-Based Scene Architecture (IN PROGRESS)
Bot at Org bank (1627,-4376,Z=37) ends up at Z=57 — **on a building roof**. The 50K triangle cap means SceneDataService returns mixed ground+roof+wall triangles for the 600x600y region. Physics capsule sweep finds the roof as "ground" because it's the first surface above teleport Z. `[PHYS][ERR][MOVE] wallHit=1` — bot stuck on roof surrounded by walls.

**Root cause:** SceneDataService sends ALL triangles in AABB (Z=-500 to 2000), capped at 50K. Dense cities like Orgrimmar have 499K+ triangles per region. The 50K sample may include roofs but miss ground.

**Architecture decided:** 533y tiles matching WoW ADT grid. SceneDataService pre-loads .scenetile files, serves by (mapId, tileX, tileY). Bot loads 3×3 neighborhood (~2.3MB/bot). Scales to 3000+ bots.

**Done:**
- Proto: SceneTileRequest/SceneTileResponse defined
- Splitter: SceneTileSplitterTests extracts 142 tiles from 5 maps (35s)
- Server: SceneTileSocketServer pre-loads all tiles, serves by key
- Coordinate tests: 5 tests for tile mapping

**Remaining:**
- [ ] SceneDataClient: request tiles by (mapId, tileX, tileY) instead of AABB
- [ ] C++ InjectSceneTriangles: ADD tiles to cache (not replace). Or per-tile SceneCache management.
- [ ] Bot: track loaded tiles, load new 3×3 on movement, unload distant tiles
- [ ] Remove SetSceneSliceMode entirely
- [ ] Docker: copy tiles into container, rebuild + deploy
- [ ] Tests: tile boundary crossing, tile merge, no regression on physics tests

---

## Deferred Issues

| # | Issue | Details |
|---|-------|---------|
| D1 | **Alliance faction bots** | StateManager doesn't launch AVBOTA* accounts — needs settings support |
| D2 | **BG queue pop** | AB/AV queued but never popped — server-side BG matching config |
| D3 | **WSG transfer stalls** | 8/20 bots didn't complete map transfer — timeout or protocol issue |
| D4 | **Elevator physics** | 2 pre-existing Navigation.Physics.Tests failures — low priority |

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
```
