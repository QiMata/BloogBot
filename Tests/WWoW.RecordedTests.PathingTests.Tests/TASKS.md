# WWoW.RecordedTests.PathingTests.Tests Tasks

## Scope
- Directory: `Tests/WWoW.RecordedTests.PathingTests.Tests`
- Project: `Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj`
- Master tracker: `MASTER-SUB-032`
- Primary implementation surfaces:
- `Tests/WWoW.RecordedTests.PathingTests.Tests/ProgramTests.cs`
- `Tests/WWoW.RecordedTests.PathingTests.Tests/ConsoleTestLoggerTests.cs`
- `WWoW.RecordedTests.PathingTests/Configuration/ConfigurationParser.cs`
- `WWoW.RecordedTests.PathingTests/Program.cs`
- `WWoW.RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`
- `WWoW.RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`

## Execution Rules
1. Execute tasks in this file top-down and keep all scans limited to the files listed in `Scope`.
2. Keep commands simple and one-line; prefer narrow `--filter` runs first.
3. Do not switch to another local `TASKS.md` until this file has concrete IDs, acceptance, and handoff metadata.
4. If two iterations produce no file delta, record blocker + exact next command in `Session Handoff` and then move queue.
5. Move completed items to `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WWoW.RecordedTests.PathingTests.Tests.csproj:25` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] Current test project mainly covers config parsing + console logger (`ProgramTests.cs`, `ConsoleTestLoggerTests.cs`).
- [x] `ConfigurationParser` edge/precedence paths exist for `SERVER_DEFINITIONS` and pathfinding flags (`ConfigurationParser.cs:50-51`, `:129-138`).
- [x] Runtime filter/run loop seams are private and untested (`Program.cs:154-304`).
- [x] Background path execution has timeout/repath/transport branches with no direct unit coverage (`BackgroundRecordedTestRunner.cs:146-149`, `:177`, `:226`, `:243-247`, `:419`, `:470-471`).

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj` succeeded (test + runtime projects restored; remaining up-to-date).
- `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` passed (`Passed: 22`, `Failed: 0`, `Skipped: 0`, duration `288 ms`).
- Local tooling note: build output logs `dumpbin` missing from `vcpkg` applocal script, but filtered test run completed and passed.
- Timeout/runsettings wiring confirmed: `WWoW.RecordedTests.PathingTests.Tests.csproj:25` and `Tests/test.runsettings:5`.
- Current coverage boundaries are explicit:
- `ProgramTests.cs` contains parse-focused tests only (series of `ParseConfiguration_*` tests).
- `Program.cs` filter/run loop entry points are private (`FilterTests` line `154`, `RunTestsAsync` line `193`).
- `BackgroundRecordedTestRunner.cs` contains the untested timeout/repath/transport/disconnect branches this file tracks (`148`, `177`, `226`, `243-247`, `419`, `470-471`).

## P0 Active Tasks (Ordered)
1. [ ] `RPTT-TST-001` Expand parser coverage for configuration precedence and edge parsing.
- Evidence: existing tests assert only a subset of flags; no checks for `SERVER_DEFINITIONS` override and pathfinding in-process precedence.
- Files: `Tests/WWoW.RecordedTests.PathingTests.Tests/ProgramTests.cs`, `WWoW.RecordedTests.PathingTests/Configuration/ConfigurationParser.cs`.
- Required breakdown: add tests for `SERVER_DEFINITIONS` host/port/realm parsing, `--no-pathfinding-inprocess` overriding enable flags, decimal vs hex window handle parsing, and boolean parsing permutations.
- Validation: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ParseConfiguration" --logger "console;verbosity=minimal"`.

2. [ ] `RPTT-TST-002` Add deterministic coverage for filter and fail-fast selection semantics.
- Evidence: `FilterTests` throws when filters match zero tests (`Program.cs:154-190`) but no direct test coverage exists.
- Files: `Tests/WWoW.RecordedTests.PathingTests.Tests/ProgramTests.cs`, `WWoW.RecordedTests.PathingTests/Program.cs`.
- Required breakdown: introduce test seam (internal wrapper or extracted service) and assert exact-match behavior for `TestFilter`, category filter, and zero-match exception message.
- Validation: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Filter" --logger "console;verbosity=minimal"`.

3. [ ] `RPTT-TST-003` Cover program run-loop result handling, including stop-on-first-failure and exception mapping.
- Evidence: orchestration loop logic and catch path in `RunTestsAsync` are untested (`Program.cs:193-304`).
- Files: `Tests/WWoW.RecordedTests.PathingTests.Tests/ProgramTests.cs`, `WWoW.RecordedTests.PathingTests/Program.cs`.
- Required breakdown: add seam for deterministic test descriptions/orchestrator results; assert that failed result short-circuits when `StopOnFirstFailure=true` and exceptions become failed `OrchestrationResult`.
- Validation: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RunTestsAsync|FullyQualifiedName~StopOnFirstFailure" --logger "console;verbosity=minimal"`.

4. [ ] `RPTT-TST-004` Add foreground runner tests for recording target precedence and command preconditions.
- Evidence: `GetRecordingTargetAsync` has config/env precedence and hard failure path (`ForegroundRecordedTestRunner.cs:111-161`) with no test coverage.
- Files: `Tests/WWoW.RecordedTests.PathingTests.Tests/ForegroundRecordedTestRunnerTests.cs` (new), `WWoW.RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`.
- Required breakdown: assert config-over-env precedence, each target type selection path, missing-target exception, and `ExecuteCommandAsync` precondition when not connected.
- Validation: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

5. [ ] `RPTT-TST-005` Add background runner path-consumption tests (empty path, stuck-repath, periodic repath).
- Evidence: navigation loop relies on `GetPath`, stuck checks, and periodic shorter-path replacement (`BackgroundRecordedTestRunner.cs:172-263`) with no isolated tests.
- Files: `Tests/WWoW.RecordedTests.PathingTests.Tests/BackgroundRecordedTestRunnerTests.cs` (new), `WWoW.RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`.
- Required breakdown: create test seams for path provider/object manager state, assert empty-path fast failure, stuck-triggered repath request, and shorter-path swap behavior.
- Validation: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

6. [ ] `RPTT-TST-006` Add timeout/transport/teardown safety tests to prevent lingering runner clients.
- Evidence: timeout conversion, transport wait timeout, and disconnect/dispose cleanup branches are only integration-observed today (`BackgroundRecordedTestRunner.cs:160-165`, `:400-431`, `:468-497`).
- Files: `Tests/WWoW.RecordedTests.PathingTests.Tests/BackgroundRecordedTestRunnerTests.cs` (new), `WWoW.RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`.
- Required breakdown: assert timeout exception text, transport timeout behavior, and disconnect idempotency with disposal/nulling of orchestrator client fields.
- Validation: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"`.

## Simple Command Set
- `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"`
- `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `RPTT-TST-001` (configuration precedence and edge parsing coverage).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WWoW.RecordedTests.PathingTests.Tests/WWoW.RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` -> Passed `22`, Failed `0`, Skipped `0`.
- Files changed: `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md`.
- Blockers: None.
- Next task: `RPTT-TST-001`.
- Next command: `Get-Content -Path 'Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md' -TotalCount 360`.
