# PathfindingService Tasks

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: deterministic path-response contracts, object-aware diagnostics, and dockerized runtime packaging.

## Execution Rules
1. Keep runtime routing on native path output unless a task explicitly changes the contract.
2. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
3. Every pathing or packaging slice must end with at least one focused build/test command in `Session Handoff`.
4. Archive completed items to `Services/PathfindingService/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `PFS-OBJ-001` Object-aware routing contract
- [x] Close the caller-adoption slice so higher-level BotRunner navigation consumes `GetPathResult(...)` and reacts to service-side blocked reasons instead of relying only on corners-only responses.

2. `PFS-LIVE-001` Live integration sweep
- [x] Run the full `LiveValidation` namespace against the current split-service Linux stack and capture the first complete pass/fail matrix without interruption.

3. `PFS-DOCKER-001` Containerized runtime validation
- [x] Split Docker topology so `PathfindingService` and `SceneDataService` run as separate Windows services with BG endpoint wiring.
- [x] Validate split Linux containers against mounted `WWOW_DATA_DIR` nav/scene data and capture readiness evidence.

4. `PFS-ROUTEPACK-001` Generated static route packs
- [x] Design and prototype PathfindingService-owned route-pack lookup for
  static long legs. Route packs must be generated from Navigation.dll/Detour
  output, validated with race/gender capsule metadata, keyed by nav-data and
  route-algorithm signatures, and bypassed when dynamic overlays or corridor
  validation make the cached route unsafe.

5. `PFS-ROUTEPACK-002` Lower-incline live recovery pack
- [ ] Extend the generated route-pack/warmup strategy so the live lower-layer
  Orgrimmar recovery request from `(1363.9,-4377.8,26.1)` toward the
  Orgrimmar -> Undercity gangplank can be answered without falling back to a
  slow or hanging native service request. The prior target was
  `(1341.0,-4638.6,53.5)`; the current screenshot-derived dock target is
  `(1320.142944,-4653.158691,53.891945)`. Keep this as generated Navigation
  output or generic recurring-recovery warmup, not a production detour script.

## Session Handoff
- Last updated: 2026-05-04
- Active task: `PFS-ROUTEPACK-002` lower-incline live recovery pack
- Last delta:
  - Stopped for repo cleanup before another live rerun. No live
    Crossroads -> Undercity validation was launched after the screenshot
    anchor update.
  - Updated the default Orgrimmar route-pack seed end anchors and socket/route
    tests from the old between-NPC deck point to the screenshot-derived
    Orgrimmar -> Undercity gangplank at
    `(1320.142944,-4653.158691,53.891945)`.
  - `Tests/PathfindingService.Tests.NavigationFixture` now discovers usable
    local nav data roots itself, including ready-drive `MaNGOS\data` roots, so
    focused test commands do not need to set `WWOW_DATA_DIR` explicitly. The
    fixture still sets `WWOW_DATA_DIR` internally for Navigation.dll.
  - The route-pack/static focused screenshot-anchor command timed out after
    `20m`; do not treat that as green. Rerun a narrower deterministic slice
    before live validation.
  - Added `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`.
    The cache warms generated Navigation.dll outputs, keys them by seed/schema,
    map, nav-data signature, route-algorithm signature, race/gender capsule,
    smooth flag, and route policy, and bypasses reuse for incompatible dynamic
    overlays.
  - `PathfindingSocketServer` now warms the Orgrimmar flight-master and
    exterior-incline recovery seeds at startup and returns cache hits as normal
    `NavigationPathResult` instances with `route_pack_main_path` or
    `route_pack_suffix`.
  - Added generic suffix attachment validation through
    `Navigation.IsSegmentWalkableForAgent(...)`, including strict LOS, local
    agent affordance, and resolved endpoint Z checks to avoid vertical-layer
    snaps.
  - The Docker service warmed both route packs (`packs=2`) against the mounted
    `D:/MaNGOS/data` nav data. Warmup took about `184435ms`.
  - Latest live rerun is still red: the lower-layer request from
    `(1363.9,-4377.8,26.1)` to `(1341.0,-4638.6,53.5)` correctly bypassed the
    unsafe cached suffix, then native generation was still-running at `25s`
    and the BotRunner test failed near `(1363.9,-4378.2,26.1)`.
- Pass result: `route-pack prototype shipped; screenshot anchors applied; BotRunner deterministic gates green; Pathfinding screenshot-anchor gate timed out`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CrossMapRouterTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_screenshot_anchors.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (94/94)` after updating an obsolete configured-boarding-position assertion.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_after_nav_fixture_discovery.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (8/8)` without explicitly setting `WWOW_DATA_DIR`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinDeckBoardingPoint_StaysOnUpperDeckLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_routepack_screenshot_boarding_anchor.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> timed out after `20m`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_unit_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (7/7)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_cache_real_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_socket_routepack_contract_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routepack_cache_prep_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)`.
  - `docker compose -f docker-compose.vmangos-linux.yml up -d --build pathfinding-service` -> succeeded.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `(1363.9,-4378.2,26.1)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Tests/PathfindingService.Tests/NavigationFixture.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`
