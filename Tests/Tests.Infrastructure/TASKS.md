<<<<<<< HEAD
﻿# Tests.Infrastructure Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- This file owns fixture and process-lifecycle safeguards used by live tests.
- Current priority is preventing lingering clients/managers during long-running scenarios.

## Scope
Shared test fixture and client infrastructure for live/integration orchestration.

## Active Priorities
1. Hard teardown on abnormal exits
- [ ] Ensure timeout/failure/cancel paths always stop repo-scoped lingering `WoWStateManager`, test-launched `WoW`, `dotnet`, and `testhost*`.
- [ ] Keep deterministic teardown order (`WoWStateManager` -> child clients -> repo-scoped `dotnet/testhost*`).
- [ ] Emit teardown summary with process name, PID, and stop result.

2. Fixture determinism
- [ ] Keep setup snapshot-driven with minimal command count.
- [ ] Ensure cleanup executes even when a test aborts mid-run.

3. Corpse-run support
- [ ] Support 10-minute execution windows for `DeathCorpseRunTests`.
- [ ] Preserve cleanup guarantees even when the full timeout window is consumed.

4. Diagnostic control
- [ ] Add optional visible console/window mode for local debugging.
- [ ] Keep default headless behavior for CI and unattended runs.

## Canonical Verification Commands
1. Focused corpse-run:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Repo-scoped process audit:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`

3. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last infra fix:
- Validation/tests run:
- Files changed:
- Next task:

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
Move completed items to `Tests/Tests.Infrastructure/TASKS_ARCHIVE.md`.
=======
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
>>>>>>> cpp_physics_system
