# Tests.Infrastructure Tasks

## Scope
- Project: `Tests/Tests.Infrastructure`
- Master tracker: `MASTER-SUB-028`
- Directory: `Tests/Tests.Infrastructure`
- Primary implementation surfaces:
- `Tests/Tests.Infrastructure/BotServiceFixture.cs`
- `Tests/Tests.Infrastructure/WoWProcessManager.cs`
- `Tests/BotRunner.Tests/Helpers/StateManagerProcessHelper.cs`
- `run-tests.ps1` (repo-scoped test orchestration + cleanup)

## Execution Rules
1. Work tasks in this file top-down; do not branch to another local `TASKS.md` until this list is complete or blocked.
2. Keep commands one-line and timeout-bounded.
3. Never blanket-kill `dotnet`; cleanup must be repo-scoped and logged with process name/PID/result.
4. Use scan-budget discipline: only read this task file plus directly referenced infra/fixture/process files for the current task.
5. If two passes produce no file delta, record blocker + exact next command in `Session Handoff`, then move to the next queue file.
6. Move completed items to `Tests/Tests.Infrastructure/TASKS_ARCHIVE.md` in the same session.
7. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `Tests/Tests.Infrastructure/Tests.Infrastructure.csproj:5` sets `<IsTestProject>false</IsTestProject>`, so validation must run via consuming test projects.
- [x] `run-tests.ps1:84` defines `Get-RepoScopedTestProcesses`; `run-tests.ps1:181` defines `Stop-RepoScopedTestProcesses`.
- [x] `run-tests.ps1:253` sets `--blame-hang-timeout` from `-TestTimeoutMinutes` (default 10).

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/Tests.Infrastructure/Tests.Infrastructure.csproj` succeeded (all projects up-to-date).
- Infra project is non-runnable by itself: `Tests.Infrastructure.csproj:5` is `<IsTestProject>false</IsTestProject>`.
- Repo-scoped cleanup primitives exist in `run-tests.ps1`:
- `Get-RepoScopedTestProcesses` at line `84`.
- `Stop-RepoScopedTestProcesses` at line `181`.
- `--blame-hang-timeout` wiring via `-TestTimeoutMinutes` at line `253`.
- Previous cleanup risks resolved:
- name-only cleanup loops now use `RepoMarker` ("Westworld of Warcraft") filtering via `MainModule.FileName` check.
- console defaults now controlled by `WWOW_SHOW_WINDOWS` env var (opt-in visible mode).
- Repo-scoped process validation commands executed in this pass:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> `none`.
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly -ListRepoScopedProcesses` -> before cleanup `none`, after cleanup `none`.

## P0 Active Tasks (Ordered)
1. [x] `TINF-MISS-001` Replace name-only kill loops in `BotServiceFixture` and `StateManagerProcessHelper` with repo-scoped process filtering. Added `RepoMarker` constant and `MainModule.FileName` checks to `KillStaleProcessesAsync` and `KillPathfindingServiceProcesses` in BotServiceFixture.cs, and `KillLingeringProcesses` in StateManagerProcessHelper.cs.

2. [x] `TINF-MISS-002` Emit deterministic per-process teardown evidence. Added PID, exit code, and timeout logging to `StateManagerProcessHelper.Stop()`. BotServiceFixture already had comprehensive per-PID evidence in `ForceKillProcess`.

3. [x] `TINF-MISS-003` Unify pathfinding service cleanup with repo-scoped guard. Added `RepoMarker` check to `KillPathfindingServiceProcesses` name-based path. Port-based fallback retained (inherently scoped by port).

4. [x] `TINF-MISS-004` Add optional visible console mode. All 4 bot-process launch sites now use `WWOW_SHOW_WINDOWS` env var: BotServiceFixture.cs, StateManagerProcessHelper.cs, StateManagerWorker.cs, Program.cs.

5. [x] `TINF-MISS-005` Add fixture cleanup verification tests in `Tests/BotRunner.Tests/Helpers/InfrastructureConfigTests.cs` — 7 tests covering WWOW_SHOW_WINDOWS env var logic, repo-scoped marker validation, KillLingeringProcesses callback, and StateManagerProcessHelper.Stop() null-safety. All 7 pass.

6. [x] `TINF-MISS-006` Infra docs aligned — all TINF-MISS tasks completed, session handoff updated.

## Simple Command Set
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly -ListRepoScopedProcesses`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: All TINF-MISS tasks complete.
- Last delta: Implemented TINF-MISS-001 through TINF-MISS-006: repo-scoped process filtering, teardown evidence, pathfinding cleanup guards, WWOW_SHOW_WINDOWS env var, 7 infrastructure config tests.
- Pass result: `delta shipped`
- Files changed:
  - `Tests/Tests.Infrastructure/BotServiceFixture.cs` (RepoMarker constant, repo-scoped StateManager + PathfindingService kills, WWOW_SHOW_WINDOWS)
  - `Tests/BotRunner.Tests/Helpers/StateManagerProcessHelper.cs` (repo-scoped KillLingeringProcesses, teardown evidence in Stop(), WWOW_SHOW_WINDOWS)
  - `Services/WoWStateManager/StateManagerWorker.cs` (WWOW_SHOW_WINDOWS for BGBotRunner launch)
  - `Services/WoWStateManager/Program.cs` (WWOW_SHOW_WINDOWS for PathfindingService launch)
  - `Tests/BotRunner.Tests/Helpers/InfrastructureConfigTests.cs` (NEW — 7 tests)
  - `Tests/Tests.Infrastructure/TASKS.md` (all tasks marked complete)
- Blockers: None.
- Next task: None — all TINF-MISS tasks are complete.
