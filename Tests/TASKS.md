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
1. [ ] `TST-UMB-001` Enforce child-file queue pointer discipline.
- Problem: queue drift causes repeated rediscovery and context churn.
- Target files: `docs/TASKS.md`, `Tests/TASKS.md`.
- Required change: maintain explicit `current file`/`next file` pointer + exact handoff command before switching child files.
- Validation command: `rg -n "Current queue file|Next queue file|Next command" docs/TASKS.md Tests/TASKS.md`
- Acceptance criteria: no child-file execution starts without pointer + handoff updates.

2. [ ] `TST-UMB-002` Keep corpse-run lifecycle and timeout/cleanup rules aligned across child test docs.
- Problem: lifecycle and timeout drift across suites can hide lingering-process regressions.
- Target files: `Tests/TASKS.md`, child test `TASKS.md` files with corpse/process coverage.
- Required change: preserve shared lifecycle contract (`alive -> dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive`) with 10-minute hang-timeout and repo-scoped cleanup evidence.
- Validation command: `rg -n "DeathCorpseRunTests|blame-hang-timeout 10m|CleanupRepoScopedOnly" Tests -g "TASKS.md"`
- Acceptance criteria: no conflicting timeout/cleanup guidance in child docs.

3. [ ] `TST-UMB-003` Enforce FG/BG parity gate for corpse/combat/gathering runs.
- Problem: parity mismatches are easy to miss when scenarios run in isolation.
- Target files: `Tests/BotRunner.Tests/TASKS.md` and related child test docs.
- Required change: each scenario cycle requires FG + BG pass comparison and follow-up paired tasks when behavior diverges.
- Validation command: `rg -n "FG|BG|parity|corpse|combat|gather" Tests -g "TASKS.md"`
- Acceptance criteria: parity regressions are captured as concrete child IDs, not broad notes.

4. [ ] `TST-UMB-004` Keep canonical test command surface minimal.
- Problem: duplicate command variants increase operator error and slow handoffs.
- Target files: `Tests/TASKS.md` and child test `TASKS.md` command sections.
- Required change: preserve one canonical command per scenario (`corpse`, `combat`, `gathering`, `physics`, `pathfinding`) plus one cleanup command.
- Validation command: `rg -n "^## Simple Command Set|dotnet test|run-tests\\.ps1 -CleanupRepoScopedOnly" Tests -g "TASKS.md"`
- Acceptance criteria: no duplicate multi-step variants unless attached to explicit blocker notes.

5. [ ] `TST-UMB-005` Expand pending child test task files with direct IDs and file/symbol evidence.
- Problem: remaining queue files still need local execution in order to convert broad items into direct implementation cards.
- Target files (queue order): `MASTER-SUB-022` through `MASTER-SUB-034` child test `TASKS.md` files.
- Required change: each child file must include ordered direct task IDs, acceptance criteria, simple command set, and session handoff fields.
- Validation command: `rg -n "Master tracker|P0 Active Tasks|Simple Command Set|Session Handoff" Tests -g "TASKS.md"`
- Acceptance criteria: every queued child file is execution-card formatted and handoff-ready.

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
- Last updated: 2026-02-25
- Pass result: `delta shipped`
- Last delta: converted to execution-card format with command-backed timeout/handoff evidence and verified child queue file existence (`13/13`).
- Next task: `TST-UMB-005` (start `MASTER-SUB-022`)
- Next command: `Get-Content -Path 'Tests/BotRunner.Tests/TASKS.md' -TotalCount 320`
- Blockers: none
