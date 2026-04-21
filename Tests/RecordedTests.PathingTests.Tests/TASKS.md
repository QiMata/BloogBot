# RecordedTests.PathingTests.Tests Tasks

## Scope
- Directory: `Tests/RecordedTests.PathingTests.Tests`
- Project: `RecordedTests.PathingTests.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-026`)
- Runtime surfaces under test:
- `RecordedTests.PathingTests/Program.cs`
- `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`
- `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`
- Corpse-run directive for related suites remains `.tele name {NAME} Orgrimmar` before kill, with bounded runtime and deterministic teardown evidence.

## Execution Rules
1. Work tasks in this file top-down; do not branch to another project until this list is complete or blocked.
2. Keep commands one-line and timeout-bounded.
3. Never blanket-kill `dotnet`; cleanup must be repo-scoped and logged with process name/PID/result.
4. When a missing behavior is found, add a paired research + implementation task ID immediately.
5. Move completed items to `TASKS_ARCHIVE.md` in the same session.
6. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj:23` uses `..\test.runsettings`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj` completed successfully.
- Runsettings and timeout wiring confirmed:
  - `RecordedTests.PathingTests.Tests.csproj:23` -> `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`
  - `Tests/test.runsettings:5` -> `<TestSessionTimeout>600000</TestSessionTimeout>`
- Runtime symbol references for current task IDs confirmed:
  - `Program.cs:80`, `:160` (`FilterTests` flow)
  - `Program.cs:44`, `:93`, `:105`, `:114`, `:141` (PathfindingService lifecycle)
  - `BackgroundRecordedTestRunner.cs:153`, `:158`, `:343` (timeout + `StopMovement`)
  - `ForegroundRecordedTestRunner.cs:202` (`DisposeAsync`) and `:189` (`Dispose` path).
- Baseline command validation:
  - `dotnet test ... --filter "FullyQualifiedName~ProgramTests"` -> `Passed: 22, Failed: 0, Skipped: 0`.

## P0 Active Tasks (Ordered)
None.

Known remaining work in this owner: `0` items.

## Simple Command Set
- `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests|FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-04-15
- Active task: `none`
- Last delta: added `ForegroundRecordedTestRunner` recording-target precedence coverage and disconnect lifecycle coverage; validated the local simple command surface.
- Pass result: delta shipped
- Validation/tests run:
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"` -> `passed (34/34)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests|FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (135/135)`
- Files changed:
  - `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`
  - `Tests/RecordedTests.PathingTests.Tests/ForegroundRecordedTestRunnerTests.cs`
  - `Tests/RecordedTests.PathingTests.Tests/TASKS.md`
- Blockers: none
- Next task: none in this owner
- Next command: `Get-Content -Path 'RecordedTests.Shared/TASKS.md' -TotalCount 220`
