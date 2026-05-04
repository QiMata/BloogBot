# PathfindingService.Tests Tasks

## Scope
- Directory: `Tests/PathfindingService.Tests`
- Project: `PathfindingService.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-024`)
- Local goal: verify pathfinding outputs are valid and consumed deterministically for corpse runback, combat movement, and gathering travel parity.

## Execution Rules
1. Execute tasks in numeric order unless blocked by missing data or fixture prerequisites.
2. Keep scan scope to this project path and directly referenced implementation files only.
3. Use one-line `dotnet test` commands and include `test.runsettings` for timeout enforcement.
4. Never blanket-kill `dotnet`; use repo-scoped process cleanup only and record evidence.
5. Move completed IDs to `Tests/PathfindingService.Tests/TASKS_ARCHIVE.md` in the same session.
6. If two consecutive passes produce no file delta, record blocker and exact next command, then advance to the next queue file in `docs/TASKS.md`.
7. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.
10. Do not make deterministic route gates pass by hardcoding blocker
    coordinates, clearance cylinders, route-specific detour waypoints, or
    live-position guards in production path generation. Static object clipping
    is an mmap/gameobject-bake defect until regenerated mmaps avoid it
    naturally.

## Environment Checklist
- [x] `Navigation.dll` is present in test output.
- [x] `NavigationFixture` auto-discovers a working nav data root for the current shell.
- [x] `Tests/PathfindingService.Tests/test.runsettings` is used (`10-minute TestSessionTimeout`).

## P0 Active Tasks (Ordered)

- [x] `LPATH-ORG-GO-ROUTE` - Fix GO-aware mmap generation/data and generic
  path behavior so
  `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  passes against `D:\MaNGOS\data` without PathfindingService route-specific
  static-clearance repairs. The test may keep known blocker positions as
  evidence; production path generation must not copy those coordinates into
  avoidance logic.

- [x] `PFS-ROUTEPACK-001` - Add deterministic tests for generated static route
  packs. The tests should prove route packs are keyed by nav-data signature,
  race/gender capsule, route policy, and dynamic-overlay compatibility, and
  that packed Orgrimmar routes match current Navigation.dll output before live
  validation uses them.

- [ ] `PFS-ROUTEPACK-002` - Add deterministic coverage for the latest live
  lower-incline recovery gap near `(1363.9,-4377.8,26.1)`. The route target
  has moved from the old between-NPC deck point to the screenshot-derived
  Orgrimmar -> Undercity gangplank at
  `(1320.142944,-4653.158691,53.891945)`. Any new seed or recurring-recovery
  warmup must still be generated from current Navigation output and must
  reject unsafe vertical-layer suffix attachment.

## Simple Command Set
1. Full project sweep: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
2. Reroute + corpse-run focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
3. Route validity focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-05-04
- Active task: `PFS-ROUTEPACK-002` lower-incline route-pack recovery validation.
- Last delta:
  - Stopped before another live rerun so the repo can be cleaned up. No
    further live validation was launched after the screenshot anchors were
    applied.
  - Updated deterministic route constants and socket route-pack requests to
    use the repo-root screenshot evidence:
    Orgrimmar dock `(1320.142944,-4653.158691,53.891945)`,
    Undercity dock `(2066.911377,290.113708,97.031593)`, and transport-local
    deck offset `(-12.580913,-7.983256,-16.398277)`.
  - `NavigationFixture` now auto-discovers usable local nav data roots, so
    focused commands do not need `$env:WWOW_DATA_DIR='D:\MaNGOS\data'` when
    the standard data root is present. The fixture still sets the env var
    internally for Navigation.dll.
  - The focused screenshot-anchor pathfinding command timed out after `20m`,
    so the updated anchor is not yet green on the real Navigation slice.
  - Added `StaticRoutePackCacheTests` covering generated warmup, cache-key
    mismatches, dynamic-overlay compatibility, projected suffix reuse, and
    unsafe suffix bypass.
  - Added real Navigation coverage proving the generated Orgrimmar
    flight-master route pack and exterior-incline recovery anchor validate
    against the existing static route assertions.
  - Added a socket integration test proving route-pack hits return through the
    normal `PathfindingSocketServer` path contract with `route_pack_main_path`.
  - Added a regression guard that the latest lower-layer live stall attachment
    cannot be served by a vertical snap. The live rerun now bypasses that
    unsafe suffix but still falls back to slow native generation.
- Pass result: `screenshot anchors applied; BotRunner deterministic gates green; Pathfinding screenshot-anchor route gate timed out`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CrossMapRouterTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_screenshot_anchors.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (94/94)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_after_nav_fixture_discovery.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (8/8)` without explicitly setting `WWOW_DATA_DIR`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinDeckBoardingPoint_StaysOnUpperDeckLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_routepack_screenshot_boarding_anchor.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> timed out after `20m`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_unit_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (7/7)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_cache_real_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_socket_routepack_contract_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routepack_cache_prep_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `(1363.9,-4378.2,26.1)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_unit_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/routepack_cache_real_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_socket_routepack_contract_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/long_pathing_routepack_cache_prep_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx`
- Files changed:
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Tests/PathfindingService.Tests/NavigationFixture.cs`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `docs/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
- Blockers:
  - The current cache intentionally bypasses the unsafe lower-layer suffix near
    `(1363.9,-4377.8,26.1)`; native fallback was still-running past `25s`, so
    live Crossroads -> Undercity remains red before boarding.
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

### Previous Handoff (2026-04-30)
- Last delta:
  - Added `LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes`.
  - The route data pins the known Crossroads -> Undercity bad walk legs: Orgrimmar flight-master descent, city support/tree stall, L-corner/pillar corridor, zeppelin tower ramp/ceiling stall, exterior support recovery, tower friction recovery, and Undercity arrival.
  - `PathRouteAssertions` now supports Tauren-sized capsule validation, bounded native segment checks, static LOS checks, and early support-Z drift checks for long-pathing regressions.
- Pass result: `delta shipped`
- Validation/tests run:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_tauren_agent_collapsed_support.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`
- Files changed:
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathRouteAssertions.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/Navigation.h`
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/PathFinder.h`
  - `docs/TASKS.md`
- Blockers:
  - none
- Next command: `git status --short --branch`
