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
- [x] Pathfinding service is running against valid `mmaps/maps/vmaps` data roots.
- [x] No repo-owned stale `WoW.exe`, `WoWStateManager`, `testhost*`, or scoped `dotnet` processes remain from prior runs.
- [x] Test account/character names used by replay definitions are available and stable.

## Evidence Snapshot (2026-04-15)
- Docker-backed BG corpse-run validation passes with `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer`.
- FG corpse-run validation is no longer blocked by an actively reproduced WoW.exe access violation. The 2026-04-15 opt-in rerun with `WWOW_RETRY_FG_CRASH001=1` now passes after corpse-run waypoint advancement stopped applying the standard probe-corridor veto to close waypoints.
- Orgrimmar corpse-run route output/consumption is covered by deterministic `PathfindingService.Tests` route and bot-task contracts.
- Non-cancellable orchestration and repo-scoped cleanup gaps are already closed in `Program.cs`, `ForegroundRecordedTestRunner.cs`, and `BackgroundRecordedTestRunner.cs`.

## Active Tasks

None.

Known remaining work in this owner: `0` pending items.

## Simple Command Set
1. `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~Orgrimmar" --logger "console;verbosity=minimal"`
3. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`
4. `Get-CimInstance Win32_Process | Where-Object { ($_.Name -in @('WoW.exe','WoWStateManager.exe','dotnet.exe','testhost.exe','testhost.net9.0.exe')) -and $_.CommandLine -like '*Westworld of Warcraft*' } | Select-Object Name,ProcessId,CommandLine`
5. `rg -n "CancellationToken\.None|GetPath\(|MoveToward\(" RecordedTests.PathingTests -g "*.cs"`

## Session Handoff
- Last updated: 2026-04-15
- Active task: none.
- Last delta: closed `RPT-MISS-003`. Docker-backed BG corpse-run validation remains green, and the opt-in FG corpse-run rerun now restores strict-alive state after the corpse-run route policy stopped using standard probe-corridor shortcut vetoes for close waypoints.
- Pass result: `passed`
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=death_corpse_run_recorded_pathing_live_validation.trx"` -> `passed (1/1), previous guarded run omitted FG; superseded by 2026-04-15 opt-in revalidation`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`
- Files changed:
  - `RecordedTests.PathingTests/TASKS.md`
- Next command: `rg -n "^- \[ \]" --glob TASKS.md`
- Blockers: none in this owner.
