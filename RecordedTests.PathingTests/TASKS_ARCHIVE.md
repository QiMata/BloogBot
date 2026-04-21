# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - FG corpse-run live validation

- [x] `RPT-MISS-003` Keep corpse-run scenario definition fixed to Orgrimmar.
  - Corpse-run path definitions remain Orgrimmar-based for recorded pathing replay ownership.
  - BG live validation was already green on the Docker-backed MaNGOS stack.
  - FG opt-in validation no longer reproduces the historical `FG-CRASH-001` / `CRASH-001` access violation.
  - The current FG runback/reclaim stall was resolved by letting `NavigationRoutePolicy.CorpseRun` advance close waypoints without the standard probe-corridor shortcut veto.
- Validation:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`

## Archived Snapshot (2026-04-15) - Docker corpse-run route validation

- [x] `RPT-MISS-004` Validate path output consumption in runback replay.
  - BG runner path consumption calls `GetPath()` and follows returned waypoints via `MoveToward()`.
  - Corpse runback disables probe/fallback heuristics.
  - Live BG validation completed with `DeathCorpseRunTests`.
  - Orgrimmar path-output consumption validation completed with `PathfindingService.Tests` route and bot-task contracts.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=death_corpse_run_recorded_pathing_live_validation.trx"` -> `passed (1/1), previous guarded run omitted FG; superseded by 2026-04-15 opt-in revalidation`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`

## Archived Snapshot (pre-2026-04-15) - earlier recorded pathing closeouts

- [x] `RPT-MISS-001` Remove non-cancellable orchestration paths.
- [x] `RPT-MISS-002` Enforce deterministic lingering-process teardown.
- [x] `RPT-MISS-005` Keep test commands simple and consistent.
