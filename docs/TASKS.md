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
| WoWSharpClient.Tests | 1441 | 0 | 1 | -4 removed SetSceneSliceMode tests |
| Navigation.Physics.Tests | 678 | 2 | 1 | -1 removed SceneSliceModeTests, 2 pre-existing elevator |
| BotRunner.Tests (unit) | 308 | 0 | 2 | Confirmed |

---

## P1 — Alterac Valley 40v40 Integration (TOP PRIORITY)

### Context
Single AV test: `AV_FullMatch_EnterPrepQueueMountAndReachObjective` (80 bots, 40v40).
Fixture: `AlteracValleyFixture` / `AlteracValleyCollection`. 2 FG leaders (TESTBOT1 Horde, AVBOTA1 Alliance) + 78 BG bots.
Honor rank 14 set in DB for all 80 AV accounts. mangosd restart required after DB honor changes.

### Completed
- [x] P1.1 **Level bug** — `.levelup` now computes delta from current level (was blindly adding targetLevel-1, causing level 178)
- [x] P1.2 **Anticheat rejection** — AV prep skips raid formation; bots queue individually (VMaNGOS rejects grouped AV queue)
- [x] P1.3 **Single test** — consolidated 7 AV tests into one full-pipeline test
- [x] P1.4 **Coordinator flow** — confirmed working: WaitingForBots → QueueForBattleground. Env vars visible to StateManager.
- [x] P1.5 **PvP rank** — honor_highest_rank=14 set in DB for all 80 AV characters, PvPRankForLoadout constant fixed to 14

### Open
- [ ] P1.6 **FG bot character creation flakiness** — TESTBOT1 and AVBOTA1 (Foreground) get stuck at CharacterSelect when character needs to be created. By the time "Create" is clicked, server has already disconnected. Need hardening: faster create flow or retry with reconnect.
- [ ] P1.7 **PvP gear equip failure** — Item 22852 (Druid PvP armor) failed to equip for AVBOT3 after 2 attempts. `.additemset` adds items to bag but `EquipItem` action times out waiting for bag removal. Possible causes: rank requirement not met in-memory, equip action race, or item class/level mismatch.
- [ ] P1.8 **Alliance teleport fall** — Alliance bots at Stormwind AV battlemaster (-8424.5, 342.8, 120.9) teleport to Z+3 (123.9) but land at Z=127 and pathfinding returns null waypoints trying to descend. Navigation mesh gap or Z+3 offset too high for indoor Stormwind location.
- [ ] P1.9 **BG queue pop** — Coordinator reaches QueueForBattleground and sends JoinBattleground to all 80 bots, but no bot has entered map 30 yet. Need to verify CMSG_BATTLEMASTER_JOIN is actually sent/received, and that VMaNGOS BG matching creates the instance with 40+40 queued.

---

## R1-R10 — Archived (see docs/ARCHIVE.md)

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
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build

# Docker rebuild + deploy
docker compose -f docker-compose.vmangos-linux.yml build scene-data-service
docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service
```
