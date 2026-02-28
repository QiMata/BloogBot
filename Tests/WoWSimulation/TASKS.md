# WoWSimulation Tasks

## Scope
- Directory: `Tests/WoWSimulation`
- Project: `Tests/WoWSimulation/WoWSimulation.Tests.csproj`
- Master tracker: `MASTER-SUB-031`
- Primary implementation surfaces:
- `Tests/WoWSimulation/MockMangosServer.cs`
- `Tests/WoWSimulation/MockMangosServerTests.cs`

## Execution Rules
1. Work tasks in this file top-down; do not switch to another local `TASKS.md` until this list is complete or blocked.
2. Keep commands simple and one-line; run smallest test filter first.
3. Use scan-budget discipline: read this file plus only directly referenced simulation/test files for the active task.
4. If two passes produce no file delta, record blocker plus exact next command in `Session Handoff`, then move to the next queue file.
5. Move completed items to `Tests/WoWSimulation/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WoWSimulation.Tests.csproj:24` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `MockMangosServer.SendCommand` dispatches commands and throws for unknown commands at `MockMangosServer.cs:52-64`.
- [x] `GetPlayerHealth` path exists at `MockMangosServer.cs:62` and `:135-138`, but no dedicated test currently covers it.
- [x] No-player guard paths exist in `MoveToPosition` (`MockMangosServer.cs:93-94`) and `CastSpell` (`:143-144`), but no tests assert these failure branches.
- [x] `MoveToPosition` event payload includes `From`, `To`, and `Duration` at `MockMangosServer.cs:106-114`; current tests only assert event type.
- [x] `EventType` defines `Death` and `Resurrection` at `MockMangosServer.cs:235-236`, but server/test logic does not exercise corpse lifecycle behavior.
- [x] Fixed simulated latency uses `Task.Delay(10)` at `MockMangosServer.cs:54`, which is not configurable for fast deterministic tests.

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/WoWSimulation/WoWSimulation.Tests.csproj` succeeded (`Restored ...WoWSimulation.Tests.csproj`).
- `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MockMangosServerTests" --logger "console;verbosity=minimal"` passed (`Passed: 7`, `Failed: 0`, `Skipped: 0`, duration `235 ms`).
- Analyzer warnings during test build: `xUnit1031` blocking task operations at `MockMangosServerTests.cs:128`, `:129`, and `:147`.
- Timeout/runsettings wiring confirmed: `WoWSimulation.Tests.csproj:24` and `Tests/test.runsettings:5`.
- Gap anchors are still present in source:
- command dispatch + unknown command throw (`MockMangosServer.cs:52-64`).
- hardcoded latency (`MockMangosServer.cs:54`).
- uncovered paths (`GetPlayerHealth` at `62/135`, no-player guards at `93-94` and `143-144`).
- current tests show movement/spell/interaction happy paths but no direct assertions for the negative branches above (`MockMangosServerTests.cs` symbol hits around `54`, `100`, `128`, `147`).

## P0 Active Tasks (Ordered)
1. [ ] `WSIM-TST-001` Add negative-path tests for unsupported command dispatch and no-player guard branches.
- Evidence: unsupported command throws at `MockMangosServer.cs:64`; no-player false returns at `:93-94` and `:143-144` are untested.
- Files: `Tests/WoWSimulation/MockMangosServerTests.cs`, `Tests/WoWSimulation/MockMangosServer.cs`.
- Required breakdown: add tests for unknown command, `MoveToPosition` without player, and `CastSpell` without player.
- Validation: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MockMangosServerTests" --logger "console;verbosity=minimal"`.

2. [ ] `WSIM-TST-002` Add direct health-path coverage for `GetPlayerHealth`.
- Evidence: command is implemented but not asserted by current tests.
- Files: `Tests/WoWSimulation/MockMangosServerTests.cs`, `Tests/WoWSimulation/MockMangosServer.cs`.
- Required breakdown: assert health with active player and health `0` when no player exists.
- Validation: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MockMangosServerTests" --logger "console;verbosity=minimal"`.

3. [ ] `WSIM-TST-003` Strengthen movement simulation assertions with payload verification.
- Evidence: movement event data includes `From`, `To`, and `Duration` (`MockMangosServer.cs:106-114`) but tests only verify event type (`MockMangosServerTests.cs:73-75`).
- Files: `Tests/WoWSimulation/MockMangosServerTests.cs`, `Tests/WoWSimulation/MockMangosServer.cs`.
- Required breakdown: assert event payload positions and positive duration for movement steps.
- Validation: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SendCommand_MoveToPosition_TriggersMovementEvent" --logger "console;verbosity=minimal"`.

4. [ ] `WSIM-TST-004` Add interaction failure tests for invalid/non-interactable targets.
- Evidence: `InteractWithObject` returns false when object is missing or not interactable (`MockMangosServer.cs:118-120`), but tests cover success only.
- Files: `Tests/WoWSimulation/MockMangosServerTests.cs`, `Tests/WoWSimulation/MockMangosServer.cs`.
- Required breakdown: add tests for invalid object IDs and non-interactable object cases.
- Validation: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~InteractWithObject" --logger "console;verbosity=minimal"`.

5. [ ] `WSIM-TST-005` Implement corpse lifecycle simulation hooks and tests (death, release, resurrection-ready).
- Evidence: `EventType.Death` and `EventType.Resurrection` exist but are unused (`MockMangosServer.cs:235-236`).
- Files: `Tests/WoWSimulation/MockMangosServer.cs`, `Tests/WoWSimulation/MockMangosServerTests.cs`.
- Required breakdown: add explicit commands/state transitions for death and resurrection timing so simulation can validate corpse-run flow before live integration tests.
- Validation: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MockMangosServerTests" --logger "console;verbosity=minimal"`.

6. [ ] `WSIM-TST-006` Make command latency test-configurable to keep simulation loops fast and deterministic.
- Evidence: hardcoded `Task.Delay(10)` at `MockMangosServer.cs:54` introduces avoidable runtime overhead in repeated scenario tests.
- Files: `Tests/WoWSimulation/MockMangosServer.cs`, `Tests/WoWSimulation/MockMangosServerTests.cs`.
- Required breakdown: inject configurable latency (default preserved), set low/zero latency for unit tests, and assert behavior parity.
- Validation: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.

7. [x] `WSIM-TST-007` Remove blocking task operations in tests to satisfy `xUnit1031` and avoid deadlock-prone waits.
- **Done (2026-02-28).** Converted `EventHistory_TracksAllEvents` and `ClearEventHistory_RemovesAllEvents` from `void` with `.Result` to `async Task` with `await`.

## Simple Command Set
- `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MockMangosServerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WSIM-TST-001` (negative-path tests for command dispatch/no-player guards).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MockMangosServerTests" --logger "console;verbosity=minimal"` -> Passed `7`, Failed `0`, Skipped `0`, with `xUnit1031` warnings at lines `128/129/147`.
- Files changed: `Tests/WoWSimulation/TASKS.md`.
- Blockers: None.
- Next task: `WSIM-TST-001`.
- Next command: `Get-Content -Path 'Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md' -TotalCount 360`.
