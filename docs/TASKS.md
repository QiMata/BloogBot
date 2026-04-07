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

## Test Baseline (2026-04-06 — R5/R6 complete)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1437 | 0 | 1 | +20 SceneData pipeline tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | Confirmed |
| BotRunner.Tests (LiveValidation) | 7+ | 0 | 0 | BasicLoop + DualClientParity pass |

---

## R1-R6 — Archived (see docs/ARCHIVE.md)

**Summary:**
- **R4:** SceneDataService pipeline operational (42 maps, 50K triangles/region)
- **R5.1:** TravelTo pathfinding via PathfindingClient.GetPath
- **R6:** All 10 placeholder tests fleshed out with real assertions

---

## R7 — Battleground Tests (Priority: High — Infrastructure-gated)

**Prerequisite:** Machine with 32+ GB RAM to run multiple bot processes (~1.8GB each).

| # | Task | Spec |
|---|------|------|
| 7.1 | **WSG test (20 bots)** — **12/20 bots entered WSG (MapId 489)!** Fixed: `.levelup` via bot chat instead of SOAP `.character level` (SOAP only updates DB, not in-memory level). BG queue popped, 12 bots transferred. 8 stalled during map transfer (stale timeout). First successful BG entry. | **Done** (cfc30c5c) |
| 7.2 | **AB test (30 bots)** — 15 Horde + 15 Alliance. Run: `dotnet test --filter "Collection~ArathiBasinValidation"`. | Open |
| 7.3 | **AV test (80 bots)** — 40 Horde + 40 Alliance. Run: `dotnet test --filter "Collection~AlteracValleyValidation"`. | Open |

**Notes:**
- All fixtures auto-generate settings files and create accounts via SOAP
- Each BackgroundBotRunner uses ~1.8GB RAM (scene data loaded per-process)
- Fixtures use `CoordinatorFixtureBase` with `WaitForExactBotCountAsync`
- Server position resets may need investigation (seen in R5.1 navigation tests)

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
