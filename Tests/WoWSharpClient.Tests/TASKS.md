<<<<<<< HEAD
﻿# WoWSharpClient.Tests Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\Tests\WoWSharpClient.Tests

Projects:
- WoWSharpClient.Tests.csproj

## Instructions
- Execute tasks directly without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep this file focused on active, unresolved work only.
- Add new tasks immediately when new gaps are discovered.
- Archive completed tasks to TASKS_ARCHIVE.md.

## Active Priorities
1. Validate this project behavior against current FG/BG parity goals.
2. Remove stale assumptions and redundant code paths.
3. Add or adjust tests as needed to keep behavior deterministic.
4. Keep protocol payload builders aligned with authoritative MaNGOS 1.12.1 packet layouts.

## Session Handoff
- Last task completed:
  - Updated party packet payload builder assumptions for `SMSG_GROUP_LIST` to MaNGOS 1.12.1 format (`groupType + ownFlags + memberCount`).
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartyNetworkClientComponentTests" --logger "console;verbosity=minimal"`
  - Result: Passed (`73/73`).
- Files changed:
  - `Tests/WoWSharpClient.Tests/Agent/PartyNetworkAgentTests.cs`
- Next task:
  - Add targeted assertions for live-observed `SMSG_GROUP_LIST` edge payloads when raid/assistant flags are present.

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.
## Archive
Move completed items to TASKS_ARCHIVE.md and keep this file short.



=======
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
>>>>>>> cpp_physics_system

## 2026-03-23 Session Note
- Pass result: `delta shipped`
- Local delta: transport packet round-trips, transport-local physics input, and passenger world-sync behavior now have focused deterministic coverage.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementPacketHandlerTests|FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests" -v n` -> `58 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1277 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Handlers/MovementPacketHandlerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command: `Get-Content Tests/WoWSharpClient.Tests/Movement/SplineControllerTests.cs | Select-Object -First 260`

## 2026-03-23 Session Note (Force-Speed Parity)
- Pass result: `delta shipped`
- Local delta: added deterministic coverage for the previously-missing walk-speed, swim-back-speed, and turn-rate force-change path so each opcode now proves parse -> event -> player state mutation -> ACK payload parity.
- Validation:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.MissingForceChangeOpcodes_ParseApplyAndAck" -v n` -> `3 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1280 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command: `Get-Content Tests/WoWSharpClient.Tests/Models/WoWUnitTests.cs | Select-Object -First 260`
