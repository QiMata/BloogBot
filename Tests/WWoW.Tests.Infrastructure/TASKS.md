# WWoW.Tests.Infrastructure Tasks

## Scope
- Directory: `Tests/WWoW.Tests.Infrastructure`
- Project: `Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj`
- Master tracker: `MASTER-SUB-034`
- Primary implementation surfaces:
  - `Tests/WWoW.Tests.Infrastructure/IntegrationTestConfig.cs`
  - `Tests/WWoW.Tests.Infrastructure/WoWProcessManager.cs`
  - `Tests/WWoW.Tests.Infrastructure/TestCategories.cs`
- Test surfaces (added 2026-02-28):
  - `Tests/WWoW.Tests.Infrastructure/IntegrationTestConfigTests.cs`
  - `Tests/WWoW.Tests.Infrastructure/ServiceHealthCheckerTests.cs`
  - `Tests/WWoW.Tests.Infrastructure/WoWProcessManagerTests.cs`
  - `Tests/WWoW.Tests.Infrastructure/TestCategoriesAttributeTests.cs`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to files listed in `Scope`.
2. Keep commands simple and one-line; run focused `--filter` commands before full-project runs.
3. Never blanket-kill `dotnet`; any cleanup work must be repo-scoped and logged with name/PID/result.
4. For process lifecycle validation, explicitly cover lingering `WoW.exe` and `WoWStateManager` safeguards.
5. Move completed items to `Tests/WWoW.Tests.Infrastructure/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WWoW.Tests.Infrastructure.csproj:7` is `<IsTestProject>true</IsTestProject>`.
- [x] `WWoW.Tests.Infrastructure.csproj` has `<Using Include="Xunit" />` for test discovery.
- [x] `WWoW.Tests.Infrastructure.csproj:34` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `IntegrationTestConfig` uses `int.TryParse()` with default fallback for all port env vars.
- [x] `ServiceHealthChecker.IsServiceAvailableAsync(...)` timeout behavior validated by 14 tests.
- [x] `WoWProcessManager` teardown/lifecycle validated by 31 tests (state transitions, Dispose, TerminateOnDispose).
- [x] Local test coverage: 109 tests across 4 test classes.

## P0 Active Tasks (Ordered)

All P0 tasks completed. See `TASKS_ARCHIVE.md` for details.

## Simple Command Set
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~IntegrationTestConfig" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ServiceHealthChecker" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWProcessManager" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`

## Session Handoff
- Last updated: 2026-02-28
- Active task: None (all P0 tasks complete).
- Last delta: Shipped full test suite (109 tests) covering WWINF-TST-001, 003, 004, 005, 006.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test ... --configuration Release --no-build` -> 109 passed, 0 failed.
  - `--filter "FullyQualifiedName~IntegrationTestConfig"` -> 20 passed.
  - `--filter "FullyQualifiedName~ServiceHealthChecker"` -> 14 passed.
  - `--filter "FullyQualifiedName~WoWProcessManager"` -> 31 passed.
- Files changed:
  - `Tests/WWoW.Tests.Infrastructure/IntegrationTestConfigTests.cs` (new)
  - `Tests/WWoW.Tests.Infrastructure/TestCategoriesAttributeTests.cs` (new)
  - `Tests/WWoW.Tests.Infrastructure/ServiceHealthCheckerTests.cs` (new)
  - `Tests/WWoW.Tests.Infrastructure/WoWProcessManagerTests.cs` (new)
  - `Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj` (added `<Using Include="Xunit" />`)
  - `Tests/WWoW.Tests.Infrastructure/README.md` (simplified)
  - `Tests/WWoW.Tests.Infrastructure/TASKS.md` (updated)
  - `Tests/WWoW.Tests.Infrastructure/TASKS_ARCHIVE.md` (archived completed tasks)
- Blockers: None.
- Next task: None for this sub-project.
- Next command: N/A.
