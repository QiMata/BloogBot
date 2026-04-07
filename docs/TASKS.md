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

## Test Baseline (2026-04-06 — R4 SceneData fix)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1437 | 0 | 1 | +20 SceneData pipeline tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | Confirmed |
| BotRunner.Tests (LiveValidation) | 7+ | 0 | 0 | BasicLoop + DualClientParity pass |

---

## R1/R2/R3/R4 — Archived (see docs/ARCHIVE.md)

**R4 Summary:** SceneDataService pipeline fully operational. 42 maps preloaded, 50K triangles/region served. Bots walk on ground, no floating. TravelTo fixed (arrival check + no oscillation). DualClientParity tests fixed (collection). 24 new pipeline tests added.

---

## R5 — Navigation & AV (Priority: High)

| # | Task | Spec |
|---|------|------|
| 5.1 | **TravelTo pathfinding** — Use GoTo's `NavigationPath.GetNextWaypoint()` for TravelTo so bots navigate around obstacles in cities. Currently stuck on buildings at 60y in Orgrimmar. | Open |
| 5.2 | **AV test infrastructure** — Set up AlteracValleyFixture with multi-bot accounts. Requires 80-bot configuration (40 Horde + 40 Alliance). | Open |
| 5.3 | **AV entry test** — Bots form raid, queue at BG master, enter AV (MapId=30). Verify all bots on AV map. | Open — depends on 5.2 |

---

## R6 — Placeholder Test Flesh-out (Priority: Medium)

| # | Task | Spec |
|---|------|------|
| 6.1 | **BankInteractionTests** (2 tests) — Add deposit/withdraw assertions instead of `Assert.NotNull`. | Open |
| 6.2 | **AuctionHouseTests** (2 tests) — Add AH frame state assertions. | Open |
| 6.3 | **AuctionHouseParityTests** (3 tests) — Add search/post/cancel parity assertions. | Open |
| 6.4 | **BankParityTests** (2 tests) — Add deposit/withdraw parity assertions. | Open |
| 6.5 | **RaidCoordinationTests.MarkTargeting** (1 test) — Add mark assertion. | Open |

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
