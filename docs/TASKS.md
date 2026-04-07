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

## Test Baseline (2026-04-07 — R7 BG tests complete)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1437 | 0 | 1 | +20 SceneData pipeline tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | Confirmed |
| BotRunner.Tests (LiveValidation) | 7+ | 0 | 0 | BasicLoop + DualClientParity pass |
| WSG (20 bots) | 12/20 entered | — | — | First successful BG entry |
| AV (80 bots) | 37/40 Horde | — | — | Alliance not launched |

---

## R1-R7 — All Archived (see docs/ARCHIVE.md)

**Session Summary:**
- **R1-R3:** Test baselines confirmed, LiveValidation deep dive (37 real behavior tests)
- **R4:** SceneDataService pipeline operational (42 maps, 50K triangles/region, bots walk on ground)
- **R5:** TravelTo pathfinding via PathfindingClient.GetPath
- **R6:** 10 placeholder tests fleshed out with real assertions
- **R7:** First successful BG entry (12/20 WSG), `.levelup` fix, 37/40 AV Horde bots

---

## Known Issues (for future sessions)

| Issue | Details |
|-------|---------|
| **AB/AV queue doesn't pop** | Server-side BG matching — may need minimum player count config or BG auto-start |
| **Alliance bots not launched** | StateManager doesn't launch AVBOTA* accounts — faction support gap in settings |
| **TravelTo stuck on buildings** | Straight-line MoveToward can't route around Orgrimmar buildings; GoTo pathfinding has Z alignment issue |
| **8 WSG bots stalled** | Map transfer stale timeout — bots didn't finish transfer in time |
| **Server position resets** | Bot occasionally snaps back to teleport origin during navigation |

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
