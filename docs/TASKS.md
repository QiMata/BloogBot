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

## Test Baseline (2026-04-06 — R1 rerun)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1417 | 0 | 1 | **Confirmed** |
| Navigation.Physics.Tests | 666 | 2 | 1 | **Confirmed** — 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | **Confirmed** |
| BotRunner.Tests (LiveValidation) | 33 | 19 | 21 | Post GetGroundZ→Physics.dll fix. 73/234 ran before timeout. |

---

## R1 — Full Test Suite Rerun (Priority: CRITICAL)

**Goal:** Run every test suite with the latest code (Physics.dll split, group disband, .gm off, NavigationDllResolver). Establish fresh baseline.

| # | Task | Spec |
|---|------|------|
| 1.1 | **Kill all bot processes** — Clean. | **Done** |
| 1.2 | **Rebuild everything** — .NET 0 CS errors. Navigation.dll x64, Physics.dll x64+x86 all built. | **Done** |
| 1.3 | **WoWSharpClient.Tests** — 1417/0/1. **Confirmed.** | **Done** |
| 1.4 | **Navigation.Physics.Tests** — 666/2/1. **Confirmed.** | **Done** |
| 1.5 | **BotRunner.Tests (unit)** — 1626/0/4. **Confirmed.** | **Done** |
| 1.6 | **LiveValidation** — 33 passed, 19 failed, 21 skipped (73/234 ran, 40min timeout). Regression from 57 — needs investigation. | **Done** |
| 1.7 | **Baseline updated** — See table above. LV regression needs R2 investigation. | **Done** |

---

## R2 — Fix Any Regressions Found (Priority: CRITICAL)

| # | Task | Spec |
|---|------|------|
| 2.1 | **Fix any new unit test failures** — If R1.3-R1.5 show new failures, investigate and fix each one. | Open |
| 2.2 | **Fix any new LiveValidation failures** — If R1.6 shows failures beyond the 9 known multi-bot timeouts, investigate. | Open |
| 2.3 | **Verify Physics.dll loads correctly** — BG bots must load Physics.dll (not Navigation.dll) for local physics. Check logs for `[NavigationDllResolver]` messages. No 0x8007000B errors. | Open |
| 2.4 | **Verify .gm off in EnsureCleanSlateAsync** — Run a combat test, verify mobs aggro the bot. GM mode must be off during combat. | Open |
| 2.5 | **Verify group disband in EnsureCleanSlateAsync** — Run 3 sequential tests, verify no "party is full" spam in logs. | Open |

---

## R3 — LiveValidation Deep Dive (Priority: High)

**Goal:** For each LiveValidation test category, verify it exercises real game behavior, not just snapshot != null.

| # | Task | Spec |
|---|------|------|
| 3.1 | **BasicLoopTests** — Bots login, enter world, position stabilizes. Verify position Z is reasonable (not floating). | Open |
| 3.2 | **VendorBuySellTests** — Bot buys/sells at vendor. Verify inventory changes in snapshot. | Open |
| 3.3 | **CombatTests** — Bot engages mob, deals damage. Verify mob HP decreases. GM mode must be OFF. | Open |
| 3.4 | **NavigationTests** — Bot navigates between two points. Verify position changes over time, arrival within timeout. | Open |
| 3.5 | **EconomyTests** — Bank deposit, AH interaction. Verify NPC found and interacted with. | Open |
| 3.6 | **GroupFormationTests** — Party invite + accept. Verify PartyLeaderGuid set in snapshot. | Open |
| 3.7 | **RFC DungeonRun** — Bots form group, enter RFC, combat occurs. Verify mapId=389 and combat flags. | Open |

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
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "FullyQualifiedName~LiveValidation" --no-build --blame-hang --blame-hang-timeout 10m
```
