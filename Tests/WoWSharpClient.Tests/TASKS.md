# WoWSharpClient.Tests Tasks

## Scope
- Directory: `Tests/WoWSharpClient.Tests`
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Master tracker: `MASTER-SUB-030`
- Primary implementation surfaces:
- `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs`
- `Tests/WoWSharpClient.Tests/Handlers/OpcodeHandler_Tests.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
- `Exports/WoWSharpClient/Models/WoWUnit.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`

## Execution Rules
1. Work tasks in this file top-down; do not switch to another local `TASKS.md` until this list is complete or blocked.
2. Keep commands simple and one-line; use test filters for smallest targeted run first.
3. Use scan-budget discipline: read this file plus only directly referenced handler/model files for each task.
4. If two passes produce no file delta, record blocker plus exact next command in `Session Handoff`, then move to the next queue file.
5. Move completed items to `Tests/WoWSharpClient.Tests/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WoWSharpClient.Tests.csproj:823` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `SMSG_UPDATE_OBJECT_Tests.cs` TODO marker replaced with focused deterministic tests (WSC-TST-001).
- [x] `SMSG_UPDATE_OBJECT_Tests.cs` fixed `Thread.Sleep` replaced with `UpdateProcessingHelper.DrainPendingUpdates()` (WSC-TST-003).
- [x] `OpcodeHandler_Tests.cs` TODO marker replaced with category-specific postcondition assertions (WSC-TST-002).
- [x] `OpcodeHandler_Tests.cs` `handlerType` parameter now drives handler-category-specific assertions (WSC-TST-002).
- [x] Regression tests for DismissBuff/CancelAura, player field mappings, and unit geometry added (WSC-TST-004).

## P0 Active Tasks (Ordered)

All P0 tasks completed. No active items.

## Simple Command Set
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~OpcodeHandler" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: None (all P0 tasks completed).
- Last delta: Completed WSC-TST-001 through WSC-TST-004 in single session.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` -> Passed `1250`, Failed `4` (all pre-existing: WoWPlayer_ArraySizes_MatchProtocol, CanResurrect_RequiresDeadAndCorpsePosition, InGhostForm_TrueWhenHasGhostBuff, Reset_ClearsAllPhysicsState), Skipped `1`.
- Targeted test run: `dotnet test ... --filter "FullyQualifiedName~SMSG_UPDATE_OBJECT|FullyQualifiedName~OpcodeHandler|FullyQualifiedName~PlayerFieldMapping|FullyQualifiedName~UnitGeometry|FullyQualifiedName~DismissBuffCancelAura|FullyQualifiedName~SessionTimelineReplay"` -> Passed `51`, Failed `0`, Skipped `0`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs` (WSC-TST-001, WSC-TST-003)
  - `Tests/WoWSharpClient.Tests/Handlers/OpcodeHandler_Tests.cs` (WSC-TST-002)
  - `Tests/WoWSharpClient.Tests/Util/UpdateProcessingHelper.cs` (WSC-TST-003, new file)
  - `Tests/WoWSharpClient.Tests/Handlers/RegressionTests.cs` (WSC-TST-004, new file)
  - `Tests/WoWSharpClient.Tests/TASKS.md` (session handoff)
  - `Tests/WoWSharpClient.Tests/TASKS_ARCHIVE.md` (completed items)
- Blockers: None.
- Next task: None (local queue complete). Proceed to next queue file per rule 8.
- Next command: `Get-Content -Path 'Tests/WoWSimulation/TASKS.md' -TotalCount 360`.
