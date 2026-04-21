# BotProfiles Tasks

## Scope
- Project: `BotProfiles`
- Owns spec profile factories and per-spec task implementations consumed by `Exports/BotRunner`.
- This file tracks active direct file/symbol tasks only.
- Master tracker: `MASTER-SUB-001`.

## Execution Rules
1. Work this file only until the current top unchecked task is completed or explicitly blocked.
2. Use source-only scans scoped to `BotProfiles` (exclude `bin/`, `obj/`, and `tmp/`).
3. Keep commands one-line and deterministic.
4. For behavior validation, run FG and BG in the same scenario cycle when runtime behavior changes.
5. Never blanket-kill `dotnet`; capture repo-scoped teardown evidence on timeout/failure.
6. Move completed items to `BotProfiles/TASKS_ARCHIVE.md` in the same session.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
8. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.

## Environment Checklist
- [x] `BotProfiles/BotProfiles.csproj` builds in `Release` (`0 warnings`, `0 errors`).
- [x] `Tests/BotRunner.Tests/Profiles/BotProfileFactoryBindingsTests.cs` exists and is discovered.
- [x] Targeted `CombatLoopTests` validation completes with `--blame-hang-timeout 10m`.
- [x] Repo-scoped cleanup command is available: `run-tests.ps1 -CleanupRepoScopedOnly`.

## P0 Active Tasks (Ordered)
- None. Completed `BP-MISS-001` through `BP-MISS-004` are archived in `BotProfiles/TASKS_ARCHIVE.md`.

## Simple Command Set
1. `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-04-12
- Active task: none
- Last delta:
  - Verified the `BP-MISS-001` profile-factory fix was already present in the workspace: the 16 listed profiles no longer return `PvERotationTask` from `CreatePvPRotationTask(...)`.
  - Verified `BP-MISS-002` is covered by the existing `BotProfileFactoryBindingsTests.cs` reflection gate.
  - Archived stale completed `BP-MISS-001` through `BP-MISS-004` so this file now only carries active work.
- Pass result: `delta shipped`
- Validation/tests run:
  - `Get-Content -Path 'Exports/TASKS.md' -TotalCount 360`
  - `rg -n -U -P "CreatePvPRotationTask\(IBotContext botContext\)\s*=>\s*\R\s*new\s+PvERotationTask" BotProfiles -g "*.cs"` -> no matches
  - `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (1/1)`
- Files changed:
  - `BotProfiles/TASKS.md`
  - `BotProfiles/TASKS_ARCHIVE.md`
- Blockers:
  - none
- Next command: `Get-Content Tests/PathfindingService.Tests/TASKS.md | Select-Object -First 220`
