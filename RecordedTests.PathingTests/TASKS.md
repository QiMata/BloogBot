# RecordedTests.PathingTests Tasks

## Scope
- Directory: `RecordedTests.PathingTests`
- Project: `RecordedTests.PathingTests.csproj`
- Focus: deterministic FG/BG recorded pathing validation with explicit corpse-run flow and teardown safety.
- Canonical corpse-run flow: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
- Master tracker: `MASTER-SUB-010`.
- Keep only unresolved work here; move completed items to `RecordedTests.PathingTests/TASKS_ARCHIVE.md` in the same session.

## Execution Rules
1. Work only the top unchecked task ID unless blocked.
2. Keep scans source-scoped to `RecordedTests.PathingTests` and `Tests/RecordedTests.PathingTests.Tests` while this file is active.
3. Every validation cycle must run both FG and BG and compare movement/result parity.
4. Enforce 10-minute max run time for corpse-run scenarios and keep teardown evidence for timeout/failure.
5. Never blanket-kill `dotnet`; cleanup must be repo-scoped and include only owned lingering clients/services.
6. Move completed items to `RecordedTests.PathingTests/TASKS_ARCHIVE.md` in the same session.
7. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
8. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [ ] Pathfinding service is running against valid `mmaps/maps/vmaps` data roots.
- [ ] No repo-owned stale `WoW.exe`, `WoWStateManager`, `testhost*`, or scoped `dotnet` processes remain from prior runs.
- [ ] Test account/character names used by replay definitions are available and stable.

## Evidence Snapshot (2026-02-25)
- Non-cancellable orchestration paths remain:
  - `RecordedTests.PathingTests/Program.cs:267` calls `RunAsync(testDescription, CancellationToken.None)`.
  - `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs:204` calls `DisconnectAsync(CancellationToken.None)`.
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs:489` calls `DisconnectAsync(CancellationToken.None)`.
- BG runner already has partial linked token plumbing that should be extended through orchestration and teardown:
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs:142` creates linked CTS.
- Path generation/consumption exists and should be validated as authoritative runback movement:
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs:170`, `:219`, `:240` call `GetPath(...)`.
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs:337` uses `_objectManager.MoveToward(waypoint, facing)`.
- No repo-scoped lingering-process cleanup implementation is currently present in this project source:
  - `rg -n "GetProcessesByName|Process\\.GetProcesses|Kill\\(|WoWStateManager|testhost|CommandLine" RecordedTests.PathingTests -g "*.cs"` returned no cleanup hits.
- Corpse/release/resurrect scenario semantics are not represented in current `*.cs` test definitions:
  - `rg -n "corpse|release|resurrect|ValleyOfTrials|Orgrimmar" RecordedTests.PathingTests -g "*.cs"` only shows generic Orgrimmar pathing definitions in `Models/PathingTestDefinitions.cs`.
- README still uses legacy project naming and commands:
  - `RecordedTests.PathingTests/README.md:1` (`WWoW.RecordedTests.PathingTests` title), `:33`, `:36`, `:285` (`dotnet run --project WWoW.RecordedTests.PathingTests`).
- Test discovery baseline in this shell shows no explicit corpse-run/path-replay scenario tests:
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --list-tests`.

## P0 Active Tasks (Ordered)

### RPT-MISS-001 Remove non-cancellable orchestration paths
- [ ] Problem: orchestration and dispose flows still use `CancellationToken.None`, preventing deterministic timeout/cancel teardown.
- [ ] Target files:
  - `RecordedTests.PathingTests/Program.cs`
  - `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`
- [ ] Required change: thread linked cancellation tokens from top-level orchestration into runner connect/execute/dispose paths; remove `CancellationToken.None` usage for owned async operations.
- [ ] Validation command: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~RecordedTestRunner" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: timeout/cancel deterministically exits orchestration loops and returns control without manual process cleanup.

### RPT-MISS-002 Enforce deterministic lingering-process teardown
- [ ] Problem: project source has no explicit repo-scoped lingering-process teardown/reporting for timeout/failure paths.
- [ ] Target files:
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`
  - `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`
  - `RecordedTests.PathingTests/Program.cs` (if needed for final cleanup hook)
- [ ] Required change: add explicit cleanup for repo-owned `WoW.exe`, `WoWStateManager`, `testhost*`, and scoped `dotnet` child processes with per-process PID/action logs; do not touch non-repo workloads.
- [ ] Validation command: `Get-CimInstance Win32_Process | Where-Object { ($_.Name -in @('WoW.exe','WoWStateManager.exe','dotnet.exe','testhost.exe','testhost.net9.0.exe')) -and $_.CommandLine -like '*Westworld of Warcraft*' } | Select-Object Name,ProcessId,CommandLine`.
- [ ] Acceptance: timeout/failure emits process name + PID + action result and no owned lingering process survives run completion.

### RPT-MISS-003 Keep corpse-run scenario definition fixed to Orgrimmar
- [ ] Problem: current project scenario definitions are generic pathing tests and do not explicitly encode corpse-run release/reclaim behavior.
- [ ] Target files:
  - `RecordedTests.PathingTests/Models/PathingTestDefinitions.cs`
  - `RecordedTests.PathingTests/Program.cs`
  - `Tests/RecordedTests.PathingTests.Tests` (scenario coverage if missing)
- [ ] Required change: add/adjust corpse-run scenario setup to teleport with `.tele name {NAME} Orgrimmar` before kill, then assert release/runback/reclaim-ready/resurrect flow for both FG and BG.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: logs/test definitions show Orgrimmar teleport and successful runback/resurrect behavior with no `ValleyOfTrials` fallback.

### RPT-MISS-004 Validate path output consumption in runback replay
- [ ] Problem: path calls exist in BG runner, but runback correctness needs deterministic proof that returned path corners are consumed without wall-running regressions.
- [ ] Target files:
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`
  - `RecordedTests.PathingTests/Context/PathingRecordedTestContext.cs` (if present/used)
  - `Tests/RecordedTests.PathingTests.Tests` (path-consumption assertions/log validation)
- [ ] Required change: assert that runback execution follows `PathfindingService` waypoints and remove any fallback movement heuristics that diverge from path corner order.
- [ ] Validation command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~Orgrimmar" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: corpse runback follows returned route corners and no repeated wall-running pattern appears in replay traces.

### RPT-MISS-005 Keep test commands simple and consistent
- [ ] Problem: project docs still reference legacy `WWoW.*` project naming and stale run commands.
- [ ] Target files:
  - `RecordedTests.PathingTests/README.md`
  - `RecordedTests.PathingTests/TASKS.md`
- [ ] Required change: align to current project names and one-line command patterns for build/run/test/cleanup.
- [ ] Validation command: `rg -n "WWoW\\.RecordedTests\\.PathingTests|dotnet run --project WWoW\\.RecordedTests\\.PathingTests" RecordedTests.PathingTests/README.md`.
- [ ] Acceptance: one canonical command path exists for build/run/test/cleanup without legacy naming drift.

## Simple Command Set
1. `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~Orgrimmar" --logger "console;verbosity=minimal"`
3. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`
4. `Get-CimInstance Win32_Process | Where-Object { ($_.Name -in @('WoW.exe','WoWStateManager.exe','dotnet.exe','testhost.exe','testhost.net9.0.exe')) -and $_.CommandLine -like '*Westworld of Warcraft*' } | Select-Object Name,ProcessId,CommandLine`
5. `rg -n "CancellationToken\\.None|GetPath\\(|MoveToward\\(" RecordedTests.PathingTests -g "*.cs"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `MASTER-SUB-010` (`RecordedTests.PathingTests/TASKS.md`)
- Current focus: `RPT-MISS-001`
- Last delta: added evidence-backed gaps for cancellation paths, teardown ownership, scenario coverage mismatch, and doc-command drift with direct validation commands.
- Pass result: `delta shipped`
- Validation/tests run:
  - `rg -n "RunAsync\\(testDescription, CancellationToken\\.None\\)|DisconnectAsync\\(CancellationToken\\.None\\)|CreateLinkedTokenSource|GetPath\\(|MoveToward\\(" RecordedTests.PathingTests -S`
  - `rg -n "GetProcessesByName|Process\\.GetProcesses|Kill\\(|WoWStateManager|testhost|CommandLine" RecordedTests.PathingTests -g "*.cs"`
  - `rg -n "WWoW.RecordedTests.PathingTests|dotnet run --project WWoW.RecordedTests.PathingTests" RecordedTests.PathingTests/README.md -S`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --list-tests`
- Files changed:
  - `RecordedTests.PathingTests/TASKS.md`
- Next command: `Get-Content -Path 'RecordedTests.Shared/TASKS.md' -TotalCount 360`
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
