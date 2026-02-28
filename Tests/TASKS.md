# Tests Tasks

Master tracker: `MASTER-SUB-021`

## Scope
- Directory: `Tests`
- Focus: routing-only umbrella for child test-project `TASKS.md` files with strict one-file queue execution.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Treat this file as routing-only; child test-project `TASKS.md` files hold implementation details.
2. Work one child file at a time in queue order; do not open sibling child files in the same pass.
3. Keep commands simple and one-line; prefer canonical commands over new orchestration scripts.
4. Never blanket-kill `dotnet`; cleanup must stay repo-scoped with PID/process-name evidence.
5. Every timeout/failure/cancel path must record deterministic teardown evidence.
6. Missing behaviors must create paired `research + implementation` IDs in the owning child file.
7. Archive completed items to `Tests/TASKS_ARCHIVE.md` in the same session.
8. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Timeout-command consistency is present in umbrella + child docs:
  - `rg --line-number --fixed-strings -g "TASKS.md" -- "--blame-hang-timeout 10m" Tests`
  - Hits include `Tests/TASKS.md`, `Tests/BotRunner.Tests/TASKS.md`, `Tests/Tests.Infrastructure/TASKS.md`, and recorded-tests suites.
- Handoff-key consistency is present across child task docs:
  - `rg --line-number "Session Handoff|Last delta|Next command|Pass result" Tests -g "TASKS.md"`
  - Active child docs include required handoff keys.
- Child queue file existence check is clean (`13/13`):
  - Verified paths for `Tests/BotRunner.Tests/TASKS.md` through `Tests/WWoW.Tests.Infrastructure/TASKS.md` all return `True`.

## P0 Active Tasks (Ordered)
1. [x] `TST-UMB-001` Enforce child-file queue pointer discipline.
- **Verified (batch 16).** All active child files have Session Handoff with pointers and handoff commands.

2. [x] `TST-UMB-002` Keep corpse-run lifecycle and timeout/cleanup rules aligned across child test docs.
- **Verified (batch 16).** Lifecycle, 10-min timeout, and repo-scoped cleanup consistent.

3. [x] `TST-UMB-003` Enforce FG/BG parity gate for corpse/combat/gathering runs.
- **Verified (batch 16).** Parity requirements captured as BRT-PAR-001/002 in BotRunner.Tests/TASKS.md.

4. [x] `TST-UMB-004` Keep canonical test command surface minimal.
- **Verified (batch 16).** One canonical command per scenario in Simple Command Set sections.

5. [x] `TST-UMB-005` Expand pending child test task files with direct IDs and file/symbol evidence.
- **Done (batches 1-15).** Active child files have direct task IDs, acceptance criteria, and handoff fields.

## Child Queue (Ordered)
1. `MASTER-SUB-022` -> `Tests/BotRunner.Tests/TASKS.md`
2. `MASTER-SUB-023` -> `Tests/Navigation.Physics.Tests/TASKS.md`
3. `MASTER-SUB-024` -> `Tests/PathfindingService.Tests/TASKS.md`
4. `MASTER-SUB-025` -> `Tests/PromptHandlingService.Tests/TASKS.md`
5. `MASTER-SUB-026` -> `Tests/RecordedTests.PathingTests.Tests/TASKS.md`
6. `MASTER-SUB-027` -> `Tests/RecordedTests.Shared.Tests/TASKS.md`
7. `MASTER-SUB-028` -> `Tests/Tests.Infrastructure/TASKS.md`
8. `MASTER-SUB-029` -> `Tests/WowSharpClient.NetworkTests/TASKS.md`
9. `MASTER-SUB-030` -> `Tests/WoWSharpClient.Tests/TASKS.md`
10. `MASTER-SUB-031` -> `Tests/WoWSimulation/TASKS.md`
11. `MASTER-SUB-032` -> `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md`
12. `MASTER-SUB-033` -> `Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md`
13. `MASTER-SUB-034` -> `Tests/WWoW.Tests.Infrastructure/TASKS.md`

## Simple Command Set
1. Corpse-run scenario: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. Combat scenario: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. Gathering scenario: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Pathfinding tests: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
5. Physics tests: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
6. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all TST-UMB tasks verified complete
- Last delta: TST-UMB-001..005 verified — all routing and formatting tasks done
- Pass result: `delta shipped`
- Files changed: `Tests/TASKS.md`
- Next command: continue with next queue file
- Blockers: BRT-CR-002/003, BRT-PAR-001/002 require live server validation
- Blockers: none
