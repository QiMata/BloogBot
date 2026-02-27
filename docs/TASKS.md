# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for all local `TASKS.md` files.
- Local files hold implementation details; this file sets priority and execution order.
- When priorities conflict, this file wins.

## Rules
1. Execute one local `TASKS.md` at a time in queue order.
2. Keep handoff pointers (`current file`, `next file`) updated before switching.
3. Prefer concrete file/symbol tasks over broad behavior buckets.
4. Never blanket-kill `dotnet` or `Game.exe` — cleanup must be PID-scoped.
5. Move completed items to `docs/ARCHIVE.md`.
6. Before session handoff, update `Session Handoff` in both this file and the active local file.
7. If two consecutive passes produce no delta, record the blocker and advance to the next queued file.

## P0 — Active Priorities

| # | ID | Task | Status |
|---|-----|------|--------|
| 1 | `CORPSE-3D-001` | Dynamic Z-aware corpse reclaim approach range (works in Orgrimmar, multi-level terrain) | In progress |
| 2 | `FISH-001` | Fix FishingProfessionTests: BG SMSG_GAMEOBJECT_CUSTOM_ANIM never fires for bobber — game object not tracked. See `SpellHandler.cs:500-538`, `FishingData.cs` | Open |
| 3 | `FG-STUCK-001` | Investigate FG ghost stuck on terrain (stale MOVEFLAG_FORWARD 0x10000001). Tolerated via soft FG assertions | Open |
| 4 | — | Complete missing-implementation backlog (section below) | Open |

## P1 — Missing Implementation Inventory

### Exports/BotRunner
- [ ] `BR-MISS-001` `ScanForQuestUnitsTask` TODO in `QuestingTask.cs:52`

### Exports/WoWSharpClient
- [ ] `WSC-MISS-001` Missing `WoWPlayer` fields in `WoWSharpObjectManager.cs:2000`
- [ ] `WSC-MISS-002` `CMSG_CANCEL_AURA` send path in `WoWUnit.cs:270`
- [ ] `WSC-MISS-003` Custom navigation strategy in `GossipNetworkClientComponent.cs:249`

### Exports/Navigation
- [ ] `NAV-MISS-001` TODO stub in `PhysicsTestExports.cpp:231`
- [ ] `NAV-MISS-002` `returnPhysMat`/scene-query support in `SceneQuery.h:26`

### Services/ForegroundBotRunner
- [ ] `FG-MISS-001` `NotImplementedException` in `WoWObject.cs`
- [ ] `FG-MISS-002` `NotImplementedException` in `WoWUnit.cs`
- [ ] `FG-MISS-003` `NotImplementedException` in `WoWPlayer.cs`
- [ ] `FG-MISS-004` Regression checks for FG snapshot paths (corpse/combat/gathering)

### Services
- [ ] `CPPMCP-MISS-001` File analysis response in `mcp_server.cpp:94`
- [ ] `CPPMCP-MISS-002` Symbol usage analysis in `CppAnalysisService.cs:165`
- [ ] `PHS-MISS-001` `NotImplementedException` guard in `PromptFunctionBase.cs:47`

### UI
- [ ] `UI-MISS-001` `ConvertBack` throw in `GreaterThanZeroToBooleanConverter.cs:18`

### Storage (S3/Azure stubs)
- [ ] `RTS-MISS-001` S3 ops in `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:49`
- [ ] `RTS-MISS-002` Azure ops in `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:47`
- [ ] `WRTS-MISS-001` S3 ops in `WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:60`
- [ ] `WRTS-MISS-002` Azure ops in `WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:55`

### Tests
- [ ] `WSC-TST-001` TODO placeholders in `SMSG_UPDATE_OBJECT_Tests.cs:16` and `OpcodeHandler_Tests.cs:44`

## Canonical Commands

```bash
# Corpse-run validation
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Pathfinding service tests
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings

# Combined live validation (crafting + corpse)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CraftingProfessionTests"
```

## Sub-TASKS Execution Queue

| # | Local file | Status | Next IDs |
|---|-----------|--------|----------|
| 1 | `BotProfiles/TASKS.md` | Pending | BP-MISS-001..004 |
| 2 | `Exports/TASKS.md` | Pending | EXP-UMB-001..004 |
| 3 | `Exports/BotCommLayer/TASKS.md` | Pending | BCL-MISS-001..004 |
| 4 | `Exports/BotRunner/TASKS.md` | Pending | BR-MISS-001..003 |
| 5 | `Exports/GameData.Core/TASKS.md` | Pending | GDC-MISS-001..003 |
| 6 | `Exports/Loader/TASKS.md` | Pending | LDR-MISS-001..003 |
| 7 | `Exports/Navigation/TASKS.md` | Pending | NAV-MISS-001..004 |
| 8 | `Exports/WinImports/TASKS.md` | Pending | WINIMP-MISS-001..005 |
| 9 | `Exports/WoWSharpClient/TASKS.md` | Pending | WSC-MISS-001..004 |
| 10 | `RecordedTests.PathingTests/TASKS.md` | Pending | RPT-MISS-001..005 |
| 11 | `RecordedTests.Shared/TASKS.md` | Pending | RTS-MISS-001..004 |
| 12 | `Services/TASKS.md` | Pending | SRV-UMB-001..004 |
| 13 | `Services/BackgroundBotRunner/TASKS.md` | Pending | BBR-MISS-001..005 |
| 14 | `Services/CppCodeIntelligenceMCP/TASKS.md` | Pending | CPPMCP-BLD-001..DOC-001 |
| 15 | `Services/DecisionEngineService/TASKS.md` | Pending | DES-MISS-001..005 |
| 16 | `Services/ForegroundBotRunner/TASKS.md` | Pending | FG-MISS-001..005 |
| 17 | `Services/LoggingMCPServer/TASKS.md` | Pending | LMCP-MISS-001..006 |
| 18 | `Services/PathfindingService/TASKS.md` | Pending | PFS-MISS-001..007 |
| 19 | `Services/PromptHandlingService/TASKS.md` | Pending | PHS-MISS-001..003 |
| 20 | `Services/WoWStateManager/TASKS.md` | Pending | WSM-MISS-001..005 |
| 21 | `Tests/TASKS.md` | Pending | TST-UMB-001..005 |
| 22 | `Tests/BotRunner.Tests/TASKS.md` | Pending | BRT-CR-001..PAR-002 |
| 23 | `Tests/Navigation.Physics.Tests/TASKS.md` | **Done** | NPT-MISS-001..003 shipped |
| 24 | `Tests/PathfindingService.Tests/TASKS.md` | Pending | PFS-TST-001..006 |
| 25 | `Tests/PromptHandlingService.Tests/TASKS.md` | Pending | PHS-TST-001..005 |
| 26-41 | Remaining test/UI/AI projects | Pending | See local files |

## Session Handoff
- **Last updated:** 2026-02-27h
- **Current work:** `CORPSE-3D-001` — Dynamic Z-aware approach range for corpse reclaim. Test changed back to Orgrimmar. Awaiting test results.
- **Last delta:** RetrieveCorpseTask computes `retrieveRange = sqrt(34^2 - zDelta^2)` instead of fixed 25y. WorldClient.cs bridges SMSG_CORPSE_RECLAIM_DELAY. ForceStopImmediate sends MSG_MOVE_STOP.
- **Next command:** Verify DeathCorpseRunTests passes in Orgrimmar, then commit and push.
