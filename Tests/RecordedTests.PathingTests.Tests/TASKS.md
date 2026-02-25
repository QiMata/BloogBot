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
1. [ ] `RPTT-TST-001` Add direct tests for `Program.FilterTests` fail-fast behavior and exact-match filtering.
- Evidence: `RecordedTests.PathingTests/Program.cs:160` (filter logic) and `RecordedTests.PathingTests/Program.cs:182` (throws when filter result is empty).
- Gap: current coverage is config parsing only in `Tests/RecordedTests.PathingTests.Tests/ProgramTests.cs:10` through `:379`; no assertions on filter execution behavior.
- Files: `Tests/RecordedTests.PathingTests.Tests/ProgramTests.cs`, `RecordedTests.PathingTests/Program.cs`.
- Validation: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"`.

2. [ ] `RPTT-TST-002` Add tests for in-process PathfindingService lifecycle start/stop and error cleanup path.
- Evidence: `RecordedTests.PathingTests/Program.cs:44` (conditional start), `:93` (normal stop), `:105` (stop on fatal error), `:114` (start method), `:141` (stop method).
- Gap: no tests currently verify host lifecycle semantics or stop-on-error guarantee.
- Files: `Tests/RecordedTests.PathingTests.Tests/ProgramTests.cs`, `RecordedTests.PathingTests/Program.cs`.
- Validation: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"`.

3. [ ] `RPTT-TST-003` Add timeout/teardown tests for `BackgroundRecordedTestRunner.RunTestAsync`.
- Evidence: timeout conversion in `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs:139` and `:153`; `StopMovement()` in `finally` at `:158`.
- Gap: no tests currently verify timeout turns into `TimeoutException` and movement stop still runs on cancellation/failure paths.
- Files: `Tests/RecordedTests.PathingTests.Tests/BackgroundRecordedTestRunnerTests.cs` (new), `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`.
- Validation: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

4. [ ] `RPTT-TST-004` Add disconnect/game-loop lifecycle tests for `BackgroundRecordedTestRunner`.
- Evidence: starts loop at `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs:92`; disconnect note at `:463` indicates no stop loop equivalent.
- Gap: no tests currently enforce idempotent disconnect or guard against lingering runner resources after timeout/failure.
- Files: `Tests/RecordedTests.PathingTests.Tests/BackgroundRecordedTestRunnerTests.cs` (new), `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`.
- Validation: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

5. [ ] `RPTT-TST-005` Add recording-target precedence and disconnect idempotency tests for `ForegroundRecordedTestRunner`.
- Evidence: config/env target resolution in `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs:105`; disconnect in `:182` and dispose path in `:204`.
- Gap: no tests currently verify priority order (config over env) and safe repeated disconnect/dispose behavior.
- Files: `Tests/RecordedTests.PathingTests.Tests/ForegroundRecordedTestRunnerTests.cs` (new), `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`.
- Validation: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

6. [ ] `RPTT-TST-006` Simplify project command surface to fast local checks and one bounded full run command.
- Evidence: this project has no local `README.md` command guide; command usage currently only implicit in task notes.
- Files: `Tests/RecordedTests.PathingTests.Tests/TASKS.md`.
- Validation: run the `Simple Command Set` below unchanged.

## Simple Command Set
- `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests|FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `RPTT-TST-001`
- Last delta: added explicit one-by-one continuation rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: delta shipped
- Validation/tests run: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` -> `Passed: 22, Failed: 0, Skipped: 0`.
- Files changed: `Tests/RecordedTests.PathingTests.Tests/TASKS.md`
- Blockers: none
- Next task: `RPTT-TST-001`
- Next command: `Get-Content -Path 'Tests/RecordedTests.Shared.Tests/TASKS.md' -TotalCount 360`
