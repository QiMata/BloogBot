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
| WoWSharpClient.Tests | 1445 | 0 | 1 | +8 tile SceneData tests |
| Navigation.Physics.Tests | 679 | 2 | 1 | +6 tile merge tests, 2 pre-existing elevator |
| BotRunner.Tests (unit) | 430 | 0 | 3 | Confirmed |
| LiveValidation (BasicLoop) | 2 | 0 | 0 | Login + physics stability |
| LiveValidation (DualClientParity) | 5 | 0 | 0 | FG/BG parity |
| LiveValidation (TileBoundary) | 2 | 0 | 0 | ADT tile crossing |
| LiveValidation (CornerNav) | 3 | 1 | 0 | OrgBankToAH timeout (pathfinding) |

---

## R1-R9 — Archived (see docs/ARCHIVE.md)

All tile-based scene architecture work is complete. SceneDataClient uses tile-based requests, Docker scene-data-service runs in tile mode, LiveValidation tests pass with tiles.

---

## Deferred Issues

| # | Issue | Details |
|---|-------|---------|
| D1 | **Alliance faction bots** | StateManager doesn't launch AVBOTA* accounts — needs settings support |
| D2 | **BG queue pop** | AB/AV queued but never popped — server-side BG matching config |
| D3 | **WSG transfer stalls** | 8/20 bots didn't complete map transfer |
| D4 | **Elevator physics** | 2 pre-existing Navigation.Physics.Tests failures |
| D5 | **OrgBankToAH navigation** | CornerNavigationTests timeout — pathfinding stall in tight Org geometry |

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

# Docker rebuild + deploy
docker compose -f docker-compose.vmangos-linux.yml build scene-data-service
docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service
```
