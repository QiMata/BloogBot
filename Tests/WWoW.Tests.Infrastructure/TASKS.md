# WWoW.Tests.Infrastructure Tasks

## Scope
- Directory: `Tests/WWoW.Tests.Infrastructure`
- Project: `Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj`
- Master tracker: `MASTER-SUB-034`
- Primary implementation surfaces:
- `Tests/WWoW.Tests.Infrastructure/IntegrationTestConfig.cs`
- `Tests/WWoW.Tests.Infrastructure/WoWProcessManager.cs`
- `Tests/WWoW.Tests.Infrastructure/TestCategories.cs`
- Planned test surfaces to add in this project:
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
- [x] `WWoW.Tests.Infrastructure.csproj:7` is `<IsTestProject>true</IsTestProject>`, and the project currently has no local `*Tests.cs` files.
- [x] `WWoW.Tests.Infrastructure.csproj:33` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `IntegrationTestConfig` uses `int.Parse(...)` for environment ports and can throw on malformed values (`IntegrationTestConfig.cs:26`, `:33`, `:51`).
- [x] `ServiceHealthChecker.IsServiceAvailableAsync(...)` uses `Task.WhenAny(connectTask, Task.Delay(timeoutMs))` without local regression tests (`IntegrationTestConfig.cs:114`).
- [x] `WoWProcessManager` has critical teardown and lifecycle seams with no local regression tests (`TerminateProcess`/`Dispose`/`TerminateOnDispose` in `WoWProcessManager.cs:210`, `:257`, `:277-279`).
- [x] `LaunchWoWProcess` currently sets `CreateNoWindow = false` (`WoWProcessManager.cs:240`) and should remain explicit in tests/docs.

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj` succeeded (`Restored ... WWoW.Tests.Infrastructure.csproj`).
- Project wiring confirmed:
- `TargetFramework=net8.0`, `IsTestProject=true`, runsettings configured (`WWoW.Tests.Infrastructure.csproj:3`, `:7`, `:33`).
- Timeout cap is active at `Tests/test.runsettings:5` (`TestSessionTimeout=600000`).
- Local unit-test coverage is currently absent in this project (`Get-ChildItem ... *Tests.cs` returned no files).
- `dotnet test ... --filter "FullyQualifiedName~IntegrationTestConfig"` completed with `No test matches the given testcase filter`, confirming zero discovered local tests.
- `dotnet test ... --list-tests` completed with `No test is available in ... WWoW.Tests.Infrastructure.dll`, confirming discovery gap for this project.
- Repo-scoped process guard infrastructure exists in `run-tests.ps1`:
- known process names include `WoWStateManager.exe` and `WoW.exe` (`run-tests.ps1:42-43`);
- cleanup/list functions are present (`Get-RepoScopedTestProcesses`, `Stop-RepoScopedTestProcesses`, `-CleanupRepoScopedOnly`, `-ListRepoScopedProcesses` at `:84`, `:181`, `:15-16`).

## P0 Active Tasks (Ordered)
1. [ ] `WWINF-TST-001` Add the missing local test suite so this test project validates its own infrastructure contracts.
- Evidence: `Tests/WWoW.Tests.Infrastructure` contains infrastructure code but no `*Tests.cs` files.
- Files: `Tests/WWoW.Tests.Infrastructure/*.cs`, `Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj`.
- Required breakdown: add test classes for config parsing, health checks, process lifecycle, and trait attributes.
- Validation: `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.

2. [x] `WWINF-TST-002` Harden `IntegrationTestConfig` environment parsing and add deterministic tests for invalid values.
- **Done (2026-02-28).** All `int.Parse()` calls replaced with `int.TryParse()` + default fallback in both `Tests/WWoW.Tests.Infrastructure/IntegrationTestConfig.cs` and `Tests/Tests.Infrastructure/IntegrationTestConfig.cs`.

3. [ ] `WWINF-TST-003` Add timeout/cancellation coverage for `ServiceHealthChecker` to prevent lingering socket work.
- Evidence: timeout currently depends on `Task.WhenAny(...)` path but has no tests proving bounded return time across unreachable endpoints.
- Files: `Tests/WWoW.Tests.Infrastructure/IntegrationTestConfig.cs`, `Tests/WWoW.Tests.Infrastructure/ServiceHealthCheckerTests.cs`.
- Required breakdown: add tests for reachable endpoint success, unreachable endpoint timeout, and repeated timeout calls without runaway background work.
- Validation: `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ServiceHealthChecker" --logger "console;verbosity=minimal"`.

4. [ ] `WWINF-TST-004` Add `WoWProcessManager` lifecycle tests for state transitions and teardown guarantees.
- Evidence: `LaunchAndInjectAsync`, `TerminateProcess`, and `Dispose` define critical transitions (`NotStarted` -> `Failed/ManagedCodeRunning/ProcessExited`) without direct local assertions.
- Files: `Tests/WWoW.Tests.Infrastructure/WoWProcessManager.cs`, `Tests/WWoW.Tests.Infrastructure/WoWProcessManagerTests.cs`.
- Required breakdown: introduce test seams where needed (for process launch/kill/wait) and assert termination behavior, `TerminateOnDispose`, and handle cleanup paths.
- Validation: `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWProcessManager" --logger "console;verbosity=minimal"`.

5. [ ] `WWINF-TST-005` Add repo-scoped process-guard coverage for lingering `WoW.exe` and `WoWStateManager` from WWoW test runs.
- Evidence: user-reported lingering clients/state managers require explicit guard assertions in WWoW infrastructure flows.
- Files: `Tests/WWoW.Tests.Infrastructure/WoWProcessManager.cs`, `Tests/WWoW.Tests.Infrastructure/WoWProcessManagerTests.cs`, `run-tests.ps1`.
- Required breakdown: assert cleanup logic targets only repo-scoped processes and records per-process teardown evidence (name, PID, outcome).
- Validation: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`.

6. [ ] `WWINF-TST-006` Keep command surface simple and align README usage with this task file.
- Evidence: local README is broad; this task file now defines concrete command flow and should remain the single execution baseline.
- Files: `Tests/WWoW.Tests.Infrastructure/README.md`, `Tests/WWoW.Tests.Infrastructure/TASKS.md`, `docs/TASKS.md`.
- Required breakdown: keep README examples aligned to the simple command set below and remove drift-prone duplication.
- Validation: run the `Simple Command Set` below unchanged.

## Simple Command Set
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~IntegrationTestConfig" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ServiceHealthChecker" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWProcessManager" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WWINF-TST-001` (add missing local infrastructure test suite).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test ... --filter "FullyQualifiedName~IntegrationTestConfig"` -> no matching tests; `dotnet test ... --list-tests` -> no tests available in assembly.
- Files changed: `Tests/WWoW.Tests.Infrastructure/TASKS.md`.
- Blockers: No local `*Tests.cs` coverage exists yet, so filtered validation cannot run until `WWINF-TST-001` adds the first test suite.
- Next task: `WWINF-TST-001`.
- Next command: `Get-Content -Path 'UI/TASKS.md' -TotalCount 360`.
