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
- Current cleanup risks are still present and task-backed:
- name-only cleanup loops and direct kills in `BotServiceFixture.cs` around `296`, `345-346`, `362-363`, `611-616`, and port-5001 fallback at `633`.
- hidden console defaults remain at `BotServiceFixture.cs:411` and `StateManagerProcessHelper.cs:255`.
- Repo-scoped process validation commands executed in this pass:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> `none`.
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly -ListRepoScopedProcesses` -> before cleanup `none`, after cleanup `none`.

## P0 Active Tasks (Ordered)
1. [ ] `TINF-MISS-001` Replace name-only kill loops in `BotServiceFixture` with repo-scoped process filtering.
- Evidence: current cleanup loops kill by process name only at `Tests/Tests.Infrastructure/BotServiceFixture.cs:292`, `:341`, `:358`, and `:611`.
- Gap: these loops can terminate unrelated machine-wide `WoWStateManager`, `WoW`, or `PathfindingService` instances.
- Files: `Tests/Tests.Infrastructure/BotServiceFixture.cs`, `run-tests.ps1`.
- Validation: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`.

2. [ ] `TINF-MISS-002` Emit deterministic per-process teardown evidence (name, PID, scope source, outcome) for all cleanup paths.
- Evidence: `DisposeAsync` reports count-only summary at `Tests/Tests.Infrastructure/BotServiceFixture.cs:309` and stale cleanup summary at `:375`.
- Gap: failures are hard to audit without per-process outcome records.
- Files: `Tests/Tests.Infrastructure/BotServiceFixture.cs`.
- Validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.

3. [ ] `TINF-MISS-003` Unify pathfinding service cleanup between process-name and port-based discovery with repo-scoped guard checks.
- Evidence: pathfinding cleanup currently combines name and port 5001 kill paths at `Tests/Tests.Infrastructure/BotServiceFixture.cs:606` through `:645`, but without repo-root command-line scope checks.
- Gap: port-based fallback can still hit unrelated processes if port usage collides.
- Files: `Tests/Tests.Infrastructure/BotServiceFixture.cs`, `run-tests.ps1`.
- Validation: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly -ListRepoScopedProcesses`.

4. [ ] `TINF-MISS-004` Add optional visible console mode for local debugging while keeping default headless behavior.
- Evidence: StateManager launch is forced hidden via `CreateNoWindow = true` at `Tests/Tests.Infrastructure/BotServiceFixture.cs:411` and `Tests/BotRunner.Tests/Helpers/StateManagerProcessHelper.cs:255`.
- Gap: no consistent opt-in path exists to view live StateManager console output in a window when diagnosing hangs.
- Files: `Tests/Tests.Infrastructure/BotServiceFixture.cs`, `Tests/BotRunner.Tests/Helpers/StateManagerProcessHelper.cs`.
- Validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.

5. [ ] `TINF-MISS-005` Add fixture cleanup verification tests in a consuming test project (not in `Tests.Infrastructure` itself).
- Evidence: `Tests/Tests.Infrastructure/Tests.Infrastructure.csproj:5` is not a runnable test project.
- Gap: teardown behavior regressions can land without direct automated assertions.
- Files: `Tests/BotRunner.Tests/**/*.cs`, `Tests/Navigation.Physics.Tests/**/*.cs`, `Tests/Tests.Infrastructure/BotServiceFixture.cs`.
- Validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Fixture|FullyQualifiedName~Cleanup|FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.

6. [ ] `TINF-MISS-006` Keep infra command surface simple and aligned to repo-scoped cleanup primitives.
- Evidence: `run-tests.ps1` already provides `-ListRepoScopedProcesses` and `-CleanupRepoScopedOnly`, but local infra docs still mixed broad guidance.
- Files: `Tests/Tests.Infrastructure/TASKS.md`, `docs/TASKS.md`.
- Validation: run the `Simple Command Set` below unchanged.

## Simple Command Set
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly -ListRepoScopedProcesses`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `TINF-MISS-001` (repo-scoped process filtering in fixture cleanup).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` and `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly -ListRepoScopedProcesses` both returned no repo-scoped lingering processes.
- Files changed: `Tests/Tests.Infrastructure/TASKS.md`.
- Blockers: None.
- Next task: `TINF-MISS-001`.
- Next command: `Get-Content -Path 'Tests/WowSharpClient.NetworkTests/TASKS.md' -TotalCount 360`.
