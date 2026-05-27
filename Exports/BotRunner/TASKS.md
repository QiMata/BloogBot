# BotRunner Tasks

## Scope
- Project: `Exports/BotRunner`
- Owns task orchestration for corpse-run, combat, gathering, questing, and shared navigation execution loops.
- Master tracker: `docs/TASKS.md`

## Execution Rules
1. Work the highest-signal unchecked task unless a blocker is recorded.
2. Keep live validation bounded and repo-scoped; never blanket-kill `dotnet` or `WoW.exe`.
3. Every navigation delta must land with focused deterministic tests before the next slice.
4. Update this file plus `docs/TASKS.md` in the same session as any shipped BotRunner delta.
5. `Session Handoff` must record `Pass result`, exact validation commands, files changed, and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/BotRunner/BotRunner.csproj` builds in `Release`.
- [x] `Tests/BotRunner.Tests` targeted filters run without restore.
- [x] Repo-scoped cleanup commands are available.

## Active Tasks

### `LPATH-CROSSROADS-UC` - staged long-path execution
- [x] `CrossMapRouter` now compares cross-map candidates by total staged
  route cost and can insert a same-map flight path to the selected transition.
  Deterministic coverage proves Crossroads -> Undercity plans as Crossroads
  taxi `25` -> Orgrimmar `23`, walk to the Orgrimmar zeppelin tower,
  Orgrimmar/Undercity zeppelin, then final Undercity walk without choosing
  Ratchet/Booty Bay or dungeon shortcuts.
- [x] `CharacterAction.TravelTo` now upserts a persistent `TravelTask` for
  cross-map targets instead of returning `TravelTo cross-map not yet
  implemented`; deterministic dispatch coverage proves the task is queued.
- [x] Same-map `CharacterAction.TravelTo` now also upserts a persistent
  `TravelTask` for non-arrived long-pathing targets, so direct proof routes
  like Grunt-base -> literal Frezza emit the normal `TRAVEL_*` planning and
  waypoint diagnostics instead of falling back to `GoToTask route=none`.
- [x] `TransportData` now maps the live Orgrimmar/Undercity zeppelin to
  entry `164871` and keeps the Grom'gol zeppelin entries separate.
- [x] Long-travel navigation uses vertical-aware waypoint arrival and now
  refuses to promote past unsatisfied uphill ramp/corner waypoints during
  stuck recovery.
- [x] Long-travel navigation keeps compact/tight descending support waypoints
  through the Orgrimmar zeppelin tower rope/support chain, and scheduled
  transport boarding waits longer for boats/zeppelins than for elevators.
- [x] Transport identity now decodes static/moving transport GUIDs, uses the
  GUID-derived gameobject entry before reported entry, and emits exact
  expected entry/model/name in transport diagnostics.
- [x] Focused live validation now fast-fails known Orgrimmar
  object/terrain blockers before treating the zeppelin tower walk as complete.
  - [x] Deterministic PathfindingService generated-route validation now catches
    the Orgrimmar flight-master -> zeppelin object/corner blockers before live
    movement starts.
  - [x] The focused offline route gate now passes against `D:\MaNGOS\data`
    using GO-axis route tiles plus generic affordance repair, without
    route-specific runtime clearance or live-position workarounds.
  - [x] Maps `0` and `1` were freshly regenerated with the current GO-aware
    generator/data and Tauren Male Detour headers.
  - [x] Focused live validation was rerun against the regenerated data and
    rebuilt `pathfinding-service`.
  - [x] Fix the current live Orgrimmar runtime stall. The latest live run
    reaches the Orgrimmar zeppelin deck approach near
    `(1330.7,-4653.0,53.5)` without recurring stalls near `(1546,-4430)` or
    `(1508,-4420)`.
  - [ ] Investigate the remaining Orgrimmar -> Undercity zeppelin
    boarding/transfer evidence gap. Screenshot evidence has moved the
    Orgrimmar/Undercity dock anchors to the gangplank/deck positions, but the
    latest live evidence still ends on map `1` with `transport=0x0`.
  - [x] Consume generated static route-pack responses through the existing
    `PathfindingClient.GetPathResult(...)`/`NavigationPath` contract once
    PathfindingService owns route-pack lookup. BotRunner must not ship
    hand-authored Orgrimmar waypoint scripts.
  - [ ] Rerun focused live validation after PathfindingService can answer the
    lower-incline Orgrimmar recovery request without falling back to slow
    native generation. Latest route-pack run failed before boarding near
    `(1363.9,-4378.2,26.1)`.
- [ ] Add and pass focused live validation for Crossroads -> Undercity.

### BR-NAV-006 Prove path ownership through combat and movement-controller handoff
Known remaining work in this owner: `0` items.
- [x] BG corpse-run live recording now persists the active `RetrieveCorpseTask` corridor snapshot to `navtrace_<account>.json`, and `DeathCorpseRunTests` asserts that the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
- [x] Session 188 redirect parity test proved FG/BG matched pause/resume packet timing with `Parity_Durotar_RoadPath_Redirect`. BG `SET_FACING` fix shipped so both clients emit `MSG_MOVE_SET_FACING` on mid-route direction changes.
- [x] Final live proof bundle (session 188): forced-turn Durotar, redirect, combat auto-attack, and corpse-run reclaim all pass on the same DLL baseline.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
### 2026-05-26 (same-map `TravelTo` now enters `TravelTask`; live red moved from spawn-startup to the later tower-approach stall)
- Pass result: built on top of commit `b3c107ba` (`Block false same-map TravelTo arrival below Frezza`). Same-map `TravelTo` no longer routes the literal Frezza proof through `GoToTask`; the live run now enters `TravelTask`, emits `TRAVEL_*` diagnostics immediately, reaches many waypoints from the Grunt-base spawn, and fails later at the tower-approach wall/cliff stall.
- Last delta:
  - Same-map `CharacterAction.TravelTo` now keeps the existing `15y` / `4y` arrival gate but otherwise upserts `TravelTask` instead of `GoToTask`.
  - Added immediate `[TRAVEL_DISPATCH]` diagnostics for both same-map and cross-map `TravelTo` staging.
  - Updated deterministic dispatch coverage so same-map `TravelTo` expects `TravelTask` ownership, repeated dispatch does not grow the task stack, and the literal-Frezza slice emits `[TRAVEL_PLAN]`, `[TRAVEL_LEG]`, `[TRAVEL_EXEC] walk-nav`, and `[NAV_EXEC] try-enter route=LongTravel`.
- Validation/tests run:
  - `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_|FullyQualifiedName~Update_SameMapLiteralFrezzaSlice_EmitsTravelPlanAndWalkNavDiagnostics|FullyQualifiedName~Update_GruntBaseDeckLipSlice_EmitsImmediatePlanAndWalkNavBoundaries" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_same_map_travelto_traveltask_dispatch_20260526_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner` -> `passed (7/7)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_literal_frezza_tauren_fg_20260526_traveltask_dispatch_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)` after `1 m 41 s`.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner\botrunner_same_map_travelto_traveltask_dispatch_20260526_fix1.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner\botrunner_same_map_travelto_traveltask_dispatch_20260526_fix1.log`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526_traveltask_dispatch_fix1.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526_traveltask_dispatch_fix1.log`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Expected-bot-to-walk-from-the-OG-tower-base-Grunt-spawn-to-literal-Frezza-1331.1-LPATHFG1-client-36448-win0-20260526_211122.png`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\03-final-LPATHFG1-20260527T011119Z.json`
  - `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
- Practical read:
  - The startup gap is closed: `botrunner_LPATHFG1.diag.log` now shows `[TRAVEL_DISPATCH]`, `[TRAVEL_PLAN] legs=1 Walk`, `[TRAVEL_LEG] start index=0 type=Walk`, and many `[TRAVEL_WAYPOINT_REACHED]` events from the Grunt-base spawn.
  - The failure moved back to the later tower approach. The live assertion ends at `Final position: (1353.1,-4525.3,34.6) map=1 dist2D=126.1y`, and the failure screenshot shows the bot pressed into a wall/cliff face instead of stalling at spawn.
  - The next credible gap is route/contract behavior, not startup: the same run logs smoothed `raw_detour` requests with `blockedReason=interior_projection:98`, but later alternate unsmoothed responses still report `blockedReason=none` while producing a short route with a huge jump toward `(1320.1,-4653.2,53.7)`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
- Next command: `rg -n "blockedReason|raw_detour|smoothPath|GetPathResult|IsPathUsable|AdvanceReachableWaypoints" E:\repos\Westworld of Warcraft\Services\PathfindingService E:\repos\Westworld of Warcraft\Exports\BotRunner\Movement E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests`

### 2026-05-26 (same-map `TravelTo` no longer false-completes on Frezza's lower lane; live red moved to spawn-geometry creep)
- Pass result: built on top of commit `870a78a0` (`Document literal Frezza long-pathing proof`). Same-map `TravelTo` no longer accepts the literal Frezza objective as "arrived" from the lower Grunt-base lane just because XY fell inside the legacy 15y radius. The focused live proof now stays in `GoToTask`, moves a few yards for real, and fails on a new wall-collision creep against nearby spawn geometry instead of popping `GoToTask arrived`.
- Last delta:
  - Added an opt-in vertical-arrival gate to `GoToTask` and `UpsertGoToTask(...)`.
  - Same-map `CharacterAction.TravelTo` now keeps the legacy 15y XY arrival radius but requires `|dz| <= 4y` before the action auto-completes or its persistent `GoToTask` can pop `arrived`.
  - Added deterministic coverage for the exact literal-Frezza false-arrival shape.
- Validation/tests run:
  - `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_SameMap_UpsertsPersistentGoToTask|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_AlreadyWithinLegacyArrivalTolerance_StopsWithoutTask|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_WithinHorizontalToleranceButWrongVerticalLayer_UpsertsPersistentGoToTask|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_CrossMap_UpsertsPersistentTravelTask|FullyQualifiedName~BotRunnerServiceGoToDispatchTests|FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~Update_RequireVerticalArrival_DoesNotPopTaskWhenOnlyWithinHorizontalTolerance" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_same_map_travelto_vertical_arrival_20260526_fix2.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner` -> `passed (12/12)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_literal_frezza_tauren_fg_20260526_vertical_arrival_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)` after ~`28s`.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner\botrunner_same_map_travelto_vertical_arrival_20260526_fix2.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526_vertical_arrival_fix1.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-wall-collision-creep-before-OG-zeppelin-tower-ramp-climb-from-base-t-LPATHFG1-client-20676-win0-20260526_204920.png`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\`
- Practical read:
  - The prior false completion is gone: the new live snapshot no longer contains `[TASK] GoToTask pop reason=arrived`.
  - The live proof now stays active long enough to move from `(1332.1,-4634.5,23.9)` to `(1329.7,-4635.0,23.8)` before stalling with forward intent held (`flags=0x1`, `currentSpeed=0.00`).
  - BotRunner still emits only `[GOTO_ROUTE] plan=1 route=none drops=0 cliffs=0 vertical=0`; no `TRAVEL_*` diagnostics appear because same-map `TravelTo` still routes through `GoToTask`, not `TravelTask`.
  - The local service continues to prove the literal Frezza query itself is real:
    - `start=(1332.8,-4633.4,24.0) end=(1331.1,-4649.5,53.6)`
    - `pathLen=144 blockedReason=interior_projection:98`
  - The failure screenshot shows the FG target jammed against nearby Grunt-base tent/prop geometry at spawn, not climbing the ramp.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Tests/BotRunner.Tests/GoToTaskFallbackTests.cs`
- Next command: `rg -n "CharacterAction.TravelTo|UpsertTravelTask|GoToTask|NavigationRoutePolicy.Standard|NavigationRoutePolicy.LongTravel" E:\repos\Westworld of Warcraft\Exports\BotRunner E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests`

### 2026-05-04 (stopped after screenshot-anchor cleanup)
- Pass result: stopped before another live rerun; screenshot anchors are
  applied and BotRunner deterministic transport gates are green.
- Last delta:
  - Applied repo-root screenshot evidence to the Orgrimmar/Undercity transport
    data and cross-map graph:
    Orgrimmar dock `(1320.142944,-4653.158691,53.891945)`,
    Undercity dock `(2066.911377,290.113708,97.031593)`, and deck local offset
    `(-12.580913,-7.983256,-16.398277)`.
  - Updated long-pathing live constants, staging helper coordinates, and
    transport diagnostics tests to match the captured dock points.
  - Updated the configured-boarding-position deterministic test so it asserts
    the useful invariant after the wait and boarding anchors became the same
    screenshot-derived point: hold the configured boarding point while waiting.
  - No live validation was launched after these changes.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CrossMapRouterTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_screenshot_anchors.trx" --results-directory tmp/test-runtime/results-botrunner` -> first run failed the obsolete assertion, rerun passed `(94/94)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_after_nav_fixture_discovery.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (8/8)`.
- Evidence:
  - `tmp/test-runtime/results-botrunner/botrunner_zeppelin_screenshot_anchors.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_after_nav_fixture_discovery.trx`
  - Repo-root screenshots: `org-uc-boarding.jpg`, `uc-org-boarding.jpg`,
    `zepplin-riding.jpg`.
- Files changed in this slice:
  - `Exports/BotRunner/Movement/TransportData.cs`
  - `Exports/BotRunner/Movement/MapTransitionGraph.cs`
  - `Tests/BotRunner.Tests/Movement/CrossMapRouterTests.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TaxiTransportParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuard.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuardTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

### 2026-05-04 (scheduled zeppelin deck-offset boarding gate)
- Pass result: deterministic transport boarding/deck-offset gates are green;
  live manual zeppelin coordinate capture is running under the long-pathing
  config-first fixture.
- Last delta:
  - Scheduled transports now require a stationary dock window before boarding;
    the Orgrimmar/Undercity zeppelin is not treated as boardable while still
    creeping into/out of the stop.
  - Scheduled zeppelin riding now waits until the local transport offset is
    near the configured deck offset before transitioning to `Riding`.
  - WoWSharp scheduled-transport passive attach rejects the earlier below-deck
    false positive by tightening the vertical attach range.
  - The live fixture startup order fix is test-owned, but it is part of this
    BotRunner transport validation flow: long-pathing tests now launch
    StateManager directly with `LongPathing.config.json`.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_transport_after_deck_offset_riding_gate.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (84/84)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=wowsharp_movement_scheduled_transport_attach_deck_gate.trx" --results-directory tmp/test-runtime/results-wowsharp` -> `passed (76/76)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BgOnlyBotFixtureConfigurationTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=live_fixture_config_first_startup_manual_infinite_compile_check.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (9/9)`.
- Evidence:
  - `tmp/test-runtime/results-botrunner/botrunner_transport_after_deck_offset_riding_gate.trx`
  - `tmp/test-runtime/results-wowsharp/wowsharp_movement_scheduled_transport_attach_deck_gate.trx`
  - `tmp/test-runtime/results-botrunner/live_fixture_config_first_startup_manual_infinite_compile_check.trx`
- Files changed in this slice:
  - `Exports/BotRunner/Movement/TransportWaitingLogic.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

### 2026-05-04 (PathfindingService route-pack prototype consumed through normal contract)
- Pass result: PathfindingService route-pack socket contract is green and no
  BotRunner waypoint scripting was added; focused live Crossroads ->
  Undercity remains red on a lower-incline PathfindingService recovery gap.
- Last delta:
  - No BotRunner production code changed in this slice. The existing
    `GetPathResult(...)`/`NavigationPath` flow can consume route-pack results
    because PathfindingService returns them through the normal path contract.
  - `PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract`
    proves the route-pack full path is returned as `route_pack_main_path`.
  - The latest live rerun against the rebuilt Docker service failed before
    zeppelin boarding at `map=1 pos=(1363.9,-4378.2,26.1)`. The service
    correctly bypassed the unsafe cached suffix and the native fallback
    request from `(1363.9,-4377.8,26.1)` to `(1341.0,-4638.6,53.5)` was
    still-running at `25s`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_socket_routepack_contract_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routepack_cache_prep_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)`.
  - `docker compose -f docker-compose.vmangos-linux.yml up -d --build pathfinding-service` -> succeeded; route-pack warmup logged `packs=2`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `(1363.9,-4378.2,26.1)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_socket_routepack_contract_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/long_pathing_routepack_cache_prep_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx`
- Files changed in this slice:
  - `Exports/BotRunner/TASKS.md`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `docs/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "StaticRoutePackCache|CreateDefaultSeeds|IsSegmentWalkableForAgent|1363\\.9|-4377\\.8|CrossroadsToUndercity_UsesFlightAndZeppelin" Services/PathfindingService Tests/PathfindingService.Tests Tests/BotRunner.Tests`

### 2026-05-03 (route-pack architecture direction)
- Pass result: BotRunner deterministic boarding-position handoff passed;
  focused live rerun failed earlier on the static Orgrimmar walk while waiting
  for a slow service recovery. Route-pack architecture was documented for a
  PathfindingService-owned generated-cache solution.
- Last delta:
  - `TravelTask` now treats the next transport stop's configured
    `BoardingPosition` as an alternate walk-leg completion target when the
    current walk leg hands off to a transport. The same tight horizontal and
    vertical checks protect lower tower/pillar false positives.
  - Added deterministic coverage for the latest live deck boarding coordinate
    `(1330.7,-4653.0,53.5)` completing the walk leg and starting the zeppelin
    leg.
  - The latest live rerun did not reach this handoff; it stalled earlier at
    `(1381.6,-4370.6,26.0)`, while the service returned a repaired static
    route suffix after about `23690ms`.
  - Documented generated route packs as cacheable Detour/MMAP outputs, not
    BotRunner waypoint hacks.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests.Update_LiveOrgrimmarZeppelinBoardingPosition|FullyQualifiedName~TravelTaskTests.Update_LiveOrgrimmarZeppelinDeckPosition|FullyQualifiedName~TravelTaskTests.Update_LowerOrgrimmarZeppelinTowerPosition|FullyQualifiedName~TravelTaskTests.Update_OrgrimmarZeppelinPillarPosition" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_boarding_position_handoff.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_boarding_position_handoff.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `(1381.6,-4370.6,26.0)`, before zeppelin boarding handoff.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_exact_incline_deck_static_routepack_prep.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)` in `4m29s`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `docs/TRAVEL_PLANNING.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
- Next command: `rg -n "PathResultCache|RoutePack|GetPathResult|NavigationPath" Exports/BotRunner Services/PathfindingService Tests docs/TRAVEL_PLANNING.md`

### 2026-05-03 (live walk reaches zeppelin deck approach)
- Pass result: deterministic BotRunner movement/travel focus passed, offline
  Orgrimmar static blocker gate stayed green, and focused live validation now
  reaches the Orgrimmar zeppelin deck approach. End-to-end Crossroads ->
  Undercity remains open on zeppelin boarding/transfer evidence.
- Last delta:
  - Added generic long-travel wall-stuck recovery that promotes along the
    existing validated corridor before full PathfindingService replans.
  - Kept long-travel wall recovery in smooth Detour mode; earlier live evidence
    showed slow unsmoothed recovery service requests blocking the runner.
  - Added generic vertical-layer handling that preserves supported uphill
    Detour/MMAP corridor progression instead of forcing a replan.
  - No production Orgrimmar blocker coordinates, clearance cylinders, detours,
    waypoint exceptions, or live-position guards were added.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelWallRecoveryPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_KeepsSmoothDetourModeDuringWallRecovery|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_KeepsSmoothDetourModeDuringRecovery" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_travel_wall_recovery_existing_corridor_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (3/3)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_long_travel_wall_recovery_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (149/149)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelVerticalMismatchPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelReplansWhenNearWaypointIsOverheadLayer|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelWallRecoveryPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_EvaluatesAlternateOnVerticalLayerMismatch" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_travel_vertical_existing_corridor_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelKeepsSupportedUphillLayerProgressionWithoutReplanning|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelReplansWhenNearWaypointIsOverheadLayer|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelVerticalMismatchPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_EvaluatesAlternateOnVerticalLayerMismatch" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_travel_uphill_vertical_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_long_travel_uphill_vertical_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (151/151)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_long_travel_uphill_vertical.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_long_travel_uphill_vertical.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` after `14m27s`; walking reached deck approach, remaining failure `transport=0x0` and no map-transfer evidence.
- Evidence:
  - Latest live TRX:
    `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_long_travel_uphill_vertical.trx`.
  - Latest deterministic BotRunner TRX:
    `tmp/test-runtime/results-botrunner/botrunner_navigation_travel_long_travel_uphill_vertical_focus.trx`.
  - Latest offline static-object TRX:
    `tmp/test-runtime/results-pathfinding/long_pathing_static_blockers_after_long_travel_uphill_vertical.trx`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathFactoryTests.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `Select-String -Path tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_long_travel_uphill_vertical.trx -Pattern "\[TRAVEL_LEG\]|\[TRAVEL_TRANSPORT\]|\[TRANSPORT|Expected the bot to board|failure:"`

### 2026-05-03 (stopping point after generic long-travel recovery)
- Pass result: deterministic BotRunner movement/travel focus and offline
  Orgrimmar static blocker gate passed; focused live validation was not rerun
  after this latest delta.
- Last delta:
  - Added generic long-travel stall recovery behavior. When a walk leg stalls,
    `NavigationPath.RecalculateAfterMovementStall` first tries to promote to a
    forward waypoint on the already validated corridor before requesting a full
    service replan.
  - Tightened `TravelTask` progress observation so waypoint-index churn alone
    no longer resets the no-movement stall timer unless the player has moved.
  - Added deterministic coverage for the existing-corridor promotion path.
  - This does not hardcode Orgrimmar blockers, coordinates, clearance
    cylinders, detours, waypoint exceptions, or live-position guards in
    production code.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests.Update_WalkLegNoProgress|FullyQualifiedName~NavigationPathTests.RecalculateAfterMovementStall_LongTravelPromotesExistingCorridorBeforeReplanning" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_walk_stall_existing_corridor_promotion_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (3/3)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_existing_corridor_promotion_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (145/145)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_existing_corridor_promotion.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_existing_corridor_promotion.trx" --results-directory tmp/test-runtime/results-live`

### 2026-05-02 (live rerun tower approach blocker)
- Pass result: focused live Crossroads -> Undercity rerun failed at the
  Orgrimmar zeppelin tower approach; offline PathfindingService route gates
  remain green.
- Last delta:
  - Rebuilt/relaunched `pathfinding-service` earlier in the slice and
    confirmed this rerun used image
    `sha256:2d7782de11432a274991b49dfd02029e284f606abd3aaad56edcedaa5d4a6ce6`
    with `D:/MaNGOS/data` mounted at `/wwow-data`.
  - Generic PathfindingService and BotRunner affordance behavior was adjusted
    before the rerun: repaired splices are densified, early static repair scan
    reaches the underpass case, and short valid risers are no longer classified
    as severe `SteepClimb`.
  - The latest live route still replans near the zeppelin tower base and then
    fails the blocker guard with `afford=SteepClimb`, player
    `(1342.4,-4652.1,24.6)`, active `(1342.1,-4652.8,24.6)`, target
    `(1341.0,-4638.6,53.5)`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `docker exec pathfinding-service sh -lc "cat /app/pathfinding_status.json"` -> `IsReady=true`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_live_vertical_replan_green.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (15/15)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_live_replan_fix.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_affordance_replan_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (113/113)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_rerun_current.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` after `7m16s`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathRouteAssertions.cs`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "orgrimmar_zeppelin_tower|1342\\.|4652\\.|SteepClimb|native_path_alternate_mode" Tests/PathfindingService.Tests Tests/BotRunner.Tests Services/PathfindingService Exports/BotRunner -g "!**/bin/**" -g "!**/obj/**"`

### 2026-05-01 (GO-axis bake and affordance route gate)
- Pass result: PathfindingService generated-route gate is green; focused live
  Crossroads -> Undercity remains paused until full map regeneration.
- Last delta:
  - Restored the focused GO-axis Orgrimmar route tiles and map `1` Tauren Male
    config after the earlier simplification experiment.
  - Added generic PathfindingService affordance repair for steep/blocked
    Detour legs. The repair is not a BotRunner/live guard and does not encode
    Orgrimmar blocker coordinates or route-specific detours.
  - Hardened the repair scan so it keeps looking for a repairable
    affordance break instead of stopping at the first suspicious local leg.
  - Rebuilt the native Navigation target and reran the offline route gate.
    No live WoW validation was run.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `Get-Process MoveMapGenerator -ErrorAction SilentlyContinue` -> no process found.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data --build-log tmp/test-runtime/results-pathfinding/org_transposed_route_tiles_go_axisfix_regen_20260501_completed.log --tile 39,28 --tile 40,28 --tile 41,28 --tile 39,29 --tile 40,29 --tile 41,29 --tile 39,30 --tile 40,30 --tile 41,30` -> `passed`.
  - `$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"; & $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_affordance_scan_fix.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_final.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
- Files changed:
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Exports/Navigation/PathFinder.cpp`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_full_mmap_regen.trx" --results-directory tmp/test-runtime/results-live`

### 2026-05-01 (route-specific clearance rollback)
- Pass result: PathfindingService generated-route gate is red as intended;
  live Crossroads -> Undercity remains paused until GO-aware mmaps make the
  route pass without runtime blocker hacks.
- Last delta:
  - Removed the invalid PathfindingService static-clearance workaround that
    hardcoded Orgrimmar blocker zones and inserted route-specific detours.
  - Added/updated repo policy docs: BotRunner and live-validation guards may
    diagnose or fail fast, but they must not substitute for generated mmap
    collision for static gameobjects.
  - No live WoW validation was run in this pass.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -v:minimal` -> `succeeded` with existing PathfindingSocketServer nullable warnings and the existing `dumpbin` applocal warning.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_mmap_required.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `failed as intended (1/1)` with seven blocker clearances.
- Files changed:
  - `AGENTS.md`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `docs/DEVELOPMENT_GUIDE.md`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
- Next command: `Push-Location D:\MaNGOS\data; D:/MaNGOS/source/bin/MoveMapGenerator.exe 0 --threads 1 --configInputPath config.json; D:/MaNGOS/source/bin/MoveMapGenerator.exe 1 --threads 1 --configInputPath config.json; Pop-Location; dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_mmap_regen.trx" --results-directory tmp/test-runtime/results-pathfinding`

### 2026-05-01 (generated-route blocker gate)
- Pass result: PathfindingService generated-route gate failed as intended;
  live Crossroads -> Undercity should remain paused until it passes.
- Last delta:
  - Added an offline route-generation fast-fail in
    `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`.
  - The test calculates the Orgrimmar flight-master -> zeppelin walking path
    with Tauren Male dimensions and rejects the current generated route before
    a WoW client can run into the lower bonfire, bank-front model/palm,
    Z-hallway corners, steep incline, or rope-line support.
  - Read-only MaNGOS lookup identified the route bonfires as
    `guid=10975 entry=177026 display=4572` at
    `(1665.50,-4360.83,26.66)` and `guid=10090 entry=177019 display=4572`
    at `(1592.37,-4427.32,8.05)`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `failed as intended (1/1 failed) with seven blocker clearances`.
- Files changed:
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LongPathingTests.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers.trx" --results-directory tmp/test-runtime/results-pathfinding`

### 2026-05-01 (Orgrimmar blocker fast-fail)
- Pass result: deterministic blocker guard passed `8/8`; broader
  path/travel/transport focus passed `165/165`; live validation now fails
  fast at the first known Orgrimmar route blocker.
- Last delta:
  - The requested GUID-identity live run failed before transport diagnostics:
    no `[TRAVEL_TRANSPORT]` lines were emitted because the Orgrimmar ->
    Undercity zeppelin leg never started.
  - The tower-base failure was `map=1 pos=(1342.7,-4641.4,24.6)
    transport=0x0 current=null`, while the deck target is
    `(1341.0,-4638.6,53.5)`.
  - Added live-validation blocker detection for the bonfire/object choke,
    palm-tree descent, steep incline diagnostics, tower support/flagpole, and
    tower base/deck mismatch.
  - The zeppelin tower approach now requires deck-ish Z before the test moves
    on to transport waiting.
  - Rerun with the guard failed in `5m23s` at
    `Orgrimmar tower support/flagpole object collision` near
    `(1371.2,-4439.4,30.9)`.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_blocker_guard_tests.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (8/8)` after fixing a compile typo from the first attempt.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_path_travel_transport_blocker_guard_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (165/165)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_guid_identity.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 8m50s; no [TRAVEL_TRANSPORT], stopped at tower base`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_blocker_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 5m23s with named tower support/flagpole blocker`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuard.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuardTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LongPathingTests.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "1371\\.|4439\\.|SteepClimb|OrgrimmarZeppelin|CrossroadsToUndercity" Tests/PathfindingService.Tests Exports/BotRunner Services/PathfindingService docs -g "!**/bin/**" -g "!**/obj/**"`

### 2026-05-01 (transport GUID identity)
- Pass result: BotRunner transport GUID focus passed `52/52`; broader
  path/travel/transport focus passed `157/157`.
- Last delta:
  - The latest live run showed the bot was in place at the Orgrimmar ->
    Undercity wait target (`map=1`, `pos=(1341.0,-4638.5,53.5)`), but not
    boarded (`transport=0x0`).
  - The visible same-model zeppelin was entry `175080`, display `3031`
    (Orgrimmar/Grom'gol); the route needs entry `164871`, display `3031`
    (Orgrimmar/Undercity).
  - Added `TransportObjectIdentity` to decode `0xF120` static and `0x1FC0`
    moving transport GUIDs, canonicalize nearby object entries from GUID, and
    prevent scheduled transports from matching by model alone when no route
    identity is available.
  - `[TRAVEL_TRANSPORT]` now reports
    `expected=<entry>:<display>:<name>` and GUID-formatted nearest objects.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests" --logger "trx;LogFileName=botrunner_transport_guid_identity_focus.trx"` -> `passed (52/52)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests" --logger "trx;LogFileName=botrunner_path_travel_transport_guid_identity_focus.trx"` -> `passed (157/157)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs`
  - `Exports/BotRunner/Movement/TransportData.cs`
  - `Exports/BotRunner/Movement/TransportObjectIdentity.cs`
  - `Exports/BotRunner/Movement/TransportWaitingLogic.cs`
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  - `Tests/BotRunner.Tests/Movement/PathfindingOverlayBuilderTests.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_guid_identity.trx" --results-directory tmp/test-runtime/results-live`

### 2026-05-01 (compact tower support and scheduled boarding)
- Pass result: BotRunner path/travel/transport focus passed `151/151`; live
  Crossroads -> Undercity now reaches the Orgrimmar zeppelin route target but
  still lacks actual transport/map-transfer evidence.
- Last delta:
  - Added compact/tight descending waypoint guards in `NavigationPath` for the
    Orgrimmar tower rope/support chain and made compact vertical holds
    uphill-only after live evidence showed a downhill support step could pin
    the route to the upper layer.
  - Changed `TransportWaitingLogic` so scheduled transports do not abort
    boarding after the 10-second elevator timeout while the expected
    boat/zeppelin remains at the stop.
  - The latest live run reached map `1` pos `(1341.0,-4638.5,53.5)`, started
    the zeppelin leg, and waited without `TransportGuid`; transport diagnostics
    showed `near=0` for entry `164871` and `nearestDisplay=175080:3031`.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests" --logger "trx;LogFileName=botrunner_path_travel_transport_compact_uphill_only_focus.trx"` -> `passed (151/151)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_compact_uphill_only_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 12m09s; reached Orgrimmar zeppelin target but no transport/map-transfer evidence`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests" --logger "trx;LogFileName=botrunner_path_travel_transport_longpath_timeout_compile_focus.trx"` -> `passed (151/151)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Movement/TransportWaitingLogic.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_transfer_window_8min.trx" --results-directory tmp/test-runtime/results-live`

### 2026-05-01 (long-travel ramp-corner promotion guard)
- Pass result: BotRunner focused long-pathing suite passed `116/116`.
- Last delta:
  - Added a vertical-aware guard around stuck-recovery and destination-progress
    waypoint promotion so long-travel routes do not cut across important uphill
    ramp/corner waypoints.
  - The regression mirrors the Orgrimmar zeppelin deck failure where the
    character stayed near `(1339.4,-4645.4,51.9)` while the active waypoint was
    promoted up to the deck layer.
  - The preceding focused live run still failed before the zeppelin transfer;
    the user-provided screenshot identifies this ramp-corner shortcut as the
    blocker.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_StalledLongTravelPromotesToDestinationProgressWaypoint|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_StalledVerticalAwareLongTravel_DoesNotPromoteToStackedLowerLayerWaypoint|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_StalledVerticalAwareLongTravel_DoesNotPromotePastUnsatisfiedUphillRampCorner|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelReplansWhenNearWaypointIsOverheadLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=navpath_long_travel_ramp_corner_promotion.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~RaceDimensionsConcurrencyTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_pathing_focus_after_ramp_corner_guard.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (116/116)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_ramp_corner_guard.trx" --results-directory tmp/test-runtime/results-live`

### 2026-05-01 (Orgrimmar/Undercity zeppelin route identity)
- Pass result: `TransportWaitingLogicTests` passed `28/28`.
- Last delta:
  - Corrected BotRunner's static zeppelin transport definitions so
    Orgrimmar/Undercity uses the live entry `164871`.
  - Reassigned Grom'gol/Undercity to `176495` and Orgrimmar/Grom'gol to
    `175080`, matching the live transport docs and packet-window captures.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests" --logger "console;verbosity=minimal"` -> `passed (28/28)`.
- Files changed:
  - `Exports/BotRunner/Movement/TransportData.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `git diff --check -- Exports/BotRunner/Movement/TransportData.cs Exports/BotRunner/TASKS.md`

### 2026-04-28 (direct jump action dispatch for movement parity)
- Pass result: `Direct Jump dispatch supports the live FG/BG movement activity parity bundle`
- Last delta:
  - `ActionDispatcher` now dispatches `CharacterAction.Jump`.
  - `BotRunnerService.ActionMapping` maps protobuf `ObjectiveType.JUMP` to the
    shared action enum.
  - This supports `MovementParityTests.RunningJump_FgBgParity` without using
    Shodan or a behavior-script workaround.
- Validation/tests run:
  - `.\protocsharp.bat "." ".."` from `Exports/BotCommLayer/Models/ProtoDef` -> `succeeded`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors after regeneration)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_direct_actions_full_04.trx"` -> `passed (5/5; duration 2m41s)`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `git status --short --branch`

### 2026-04-28 (packet-window trigger classifier)
- Pass result: `Shared packet-window trigger classifier supports route-specific transport triggers`
- Last delta:
  - Added `PostTeleportWindowTriggerClassifier` to keep FG/BG recorder trigger
    rules in one place.
  - The classifier preserves existing teleport, worldport ACK, knockback, and
    `SMSG_MONSTER_MOVE_TRANSPORT` scenarios, and adds route-specific
    transport-entry classification for ordinary `SMSG_MONSTER_MOVE` and
    object-update payloads.
  - `WWOW_TRANSPORT_PACKET_WINDOW_ENTRIES` / singular
    `WWOW_TRANSPORT_PACKET_WINDOW_ENTRY` can override the default local
    Orgrimmar/Undercity zeppelin entry `164871`.
- Validation/tests run:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPostTeleportWindowRecorderTests" --logger "console;verbosity=minimal"` -> `passed (9/9; existing warnings; nonfatal dumpbin warning)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
- Files changed:
  - `Exports/BotRunner/PostTeleportWindowTriggerClassifier.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md`

### 2026-04-26 (Trade action dispatch follow-up)
- Pass result: `Trade action dispatch routes foreground trade operations through object-manager helpers; deterministic dispatch bundles and Shodan trade live validation passed in the expected shape`
- Last delta:
  - `ActionDispatcher` now invokes `SetTradeGoldAsync`, `SetTradeItemAsync`, `AcceptTradeAsync`, and `CancelTradeAsync` for both FG and BG trade action dispatch.
  - This keeps BotRunner action dispatch uniform and removes the foreground-only behavior-tree snippets that previously returned `Failed/behavior_tree_failed`.
  - The BG-to-FG item/gold transfer gap is documented in test docs/inventory, not hidden in dispatch: all action ACKs succeed, but the server leaves the trade payload with the initiator.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_fg_shodan_final.trx"` -> `passed (3), skipped (1)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> `failed with known Ratchet anchor instability: FG loot_window_timeout / max_casts_reached`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Services/WoWStateManager/TASKS.md Exports/BotRunner/TASKS.md Services/ForegroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md`

### 2026-04-25 (Mail collection diagnostic dispatch)
- Pass result: `CheckMail action diagnostics now report structured mail collection results; deterministic bundles and FG/BG live mail validation passed`
- Last delta:
  - `ActionDispatcher` now uses `CollectAllMailWithResultAsync(...)` for `CharacterAction.CheckMail` and emits a `[MAIL-COLLECT]` diagnostic marker with mailbox, inbox, collected, money, deletion, coinage, and subject fields.
  - This keeps Shodan-directed mail tests action-only while allowing foreground assertions to observe action completion when snapshot coinage or bag propagation lags.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> `passed (4/4)`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Services/WoWStateManager/TASKS.md Exports/BotRunner/TASKS.md`

### 2026-04-25 (Shodan loadout target-selection support)
- Pass result: `BotRunner .targetguid dispatch coverage passed and the Shodan-directed loadout smoke test passed 1/1`
- Last delta:
  - Added BotRunner's internal `.targetguid <guid>` SendChat handling in `ActionDispatcher`; it selects a target GUID through `IObjectManager.SetTarget(...)` and does not emit server chat.
  - This is fixture support for SHODAN selected-target MaNGOS setup commands (`.learn`, `.setskill`, `.additem`) and does not reintroduce AssignedActivity or change production action dispatch semantics.
  - Invalid `.targetguid` forms fail the behavior node instead of falling through to server chat.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceCombatDispatchTests.BuildBehaviorTreeFromActions_SendChatTargetGuid_SelectsGuidWithoutServerChat" --logger "console;verbosity=minimal"` -> `passed (2/2)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UnequipItemTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=loadout_shodan_director_smoke_retry.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Services/WoWStateManager/TASKS.md Exports/BotRunner/TASKS.md`

### 2026-04-25 (ACK capture Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated ACK capture live validation passed overall with 1 pass and 1 env-gated skip`
- Last delta:
  - `AckCaptureTests` now uses Shodan-owned foreground capture positioning before the ACK corpus worldport probe.
  - The foreground client remains the capture source because the injected client emits corpus fixtures; configured command capture is env-gated and dispatched through a fixture helper.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckCaptureTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=ack_capture_shodan.trx"` -> `passed overall (1 passed, 1 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|\\.learn|\\.additem|\\.setskill" Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`

### 2026-04-25 (Transport/taxi Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated taxi/transport live validation passed overall with 8 passes and 5 tracked skips`
- Last delta:
  - `TaxiTests`, `TaxiTransportParityTests`, and `TransportTests` now dispatch only taxi, recording, and transport `Goto` actions after Shodan-directed taxi readiness and coordinate staging.
  - BG taxi ride and FG/BG taxi parity passed. Elevator `TransportGuid`, cross-continent boarding, and Alliance transport placeholders remain explicit tracked skips for later runtime/config work.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TaxiTests|FullyQualifiedName~TaxiTransportParityTests|FullyQualifiedName~TransportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=transport_taxi_shodan_final.trx"` -> `passed overall (8 passed, 5 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|\\.modify|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/DualClientParityTests.cs Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`

### 2026-04-25 (SpellCastOnTarget Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated Battle Shout live validation passed 1/1`
- Last delta:
  - `SpellCastOnTargetTests` now dispatches only BG `ObjectiveType.CastSpell` after Shodan-directed Battle Shout spell, rage, and aura staging.
  - FG launches idle for topology parity; the slice adds a fixture staging helper rather than changing BotRunner cast dispatch behavior.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellCastOnTargetTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=spell_cast_on_target_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|\\.modify|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/TaxiTests.cs Tests/BotRunner.Tests/LiveValidation/TaxiTransportParityTests.cs Tests/BotRunner.Tests/LiveValidation/TransportTests.cs`

### 2026-04-25 (BattlegroundQueue Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated WSG queue live validation passed 1/1`
- Last delta:
  - `BattlegroundQueueTests` now dispatches only BG `ObjectiveType.JoinBattleground` after Shodan-directed minimum-level and Orgrimmar WSG battlemaster staging.
  - The test also dispatches `ObjectiveType.LeaveBattleground` as cleanup after queue action evidence is captured.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundQueueTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=battleground_queue_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/SpellCastOnTargetTests.cs`

### 2026-04-25 (BgInteraction Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated BgInteraction live validation passed overall with 3 BG passes and 2 tracked skips`
- Last delta:
  - `BgInteractionTests` now dispatches only BG `ObjectiveType.InteractWith`, `CheckMail`, and `VisitFlightMaster` after Shodan-directed bank, auction-house, mailbox, mail-money, coinage, and flight-master staging.
  - Auction-house interaction, mail collection, and flight-master visit passed. Bank deposit stays a tracked skip until a bank deposit `ObjectiveType` exists; Deeprun Tram stays in the dedicated transport slice.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BgInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bg_interaction_shodan.trx"` -> `passed overall (3 passed, 2 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BattlegroundQueueTests.cs`

### 2026-04-25 (Buff/Consumable Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated buff/consumable live validation passed overall with 1 BG pass and 2 tracked skips`
- Last delta:
  - `BuffAndConsumableTests` and `ConsumableUsageTests` now dispatch only BG `ObjectiveType.UseItem` / `DismissBuff` after Shodan-directed elixir and aura staging.
  - `ConsumableUsageTests` passed the legacy BG `UseItem` baseline. The stricter buff/slot and dismiss assertions stay tracked skips until BG consumable aura observation and `WoWUnit.Buffs` metadata are stable.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~ConsumableUsageTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=buff_consumable_shodan.trx"` -> `passed overall (1 passed, 2 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BgInteractionTests.cs`

### 2026-04-25 (DeathCorpseRun Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated corpse-run live validation passed overall with 1 BG pass and 1 FG opt-in skip`
- Last delta:
  - `DeathCorpseRunTests` now dispatches only BG `ObjectiveType.ReleaseCorpse`, `StartPhysicsRecording`, `RetrieveCorpse`, and `StopPhysicsRecording` after Shodan-directed Razor Hill corpse staging.
  - The BG run still asserts `RetrieveCorpseTask` navtrace ownership and strict-alive recovery; the foreground crash-regression lane remains guarded by `WWOW_RETRY_FG_CRASH001=1`.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=death_corpse_run_shodan.trx"` -> `passed overall (1 passed, 1 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs Tests/BotRunner.Tests/LiveValidation/ConsumableUsageTests.cs`

### 2026-04-25 (LootCorpse Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated loot live validation passed 1/1`
- Last delta:
  - `LootCorpseTests` now dispatches only BG `ObjectiveType.StartMeleeAttack`, `StopAttack`, and `LootCorpse` after Shodan-directed clean-bag and Durotar mob-area staging.
  - The old dedicated combat fixture path was removed from this test; FG launches only for Shodan topology parity while the BG snapshot supplies kill, loot-dispatch, and inventory evidence.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LootCorpseTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=loot_corpse_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|EnsureCleanSlateAsync|WaitForTeleportSettledAsync|damage" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`

### 2026-04-25 (Navigation/Alliance Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated navigation/alliance live validation passed 7/8 with one tracked skip`
- Last delta:
  - `NavigationTests` now dispatches only BG `ObjectiveType.Goto` after Shodan-directed Durotar road and winding-road staging.
  - `AllianceNavigationTests` uses Shodan-directed Human Alliance coordinate staging and snapshot assertions; no production BotRunner action is dispatched in that class.
  - The Valley of Trials long diagonal remains a tracked skip because delivered `Goto` currently pops `GoToTask` with `no_path_timeout` before arrival under Shodan staging.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation.NavigationTests|FullyQualifiedName~LiveValidation.AllianceNavigationTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=navigation_alliance_shodan_final4.trx"` -> `passed overall (7 passed, 1 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`

### 2026-04-25 (MovementSpeed Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated movement-speed live validation passed 1/1`
- Last delta:
  - `MovementSpeedTests` now dispatches only BG `ObjectiveType.Goto` after Shodan-directed Durotar road staging.
  - Foreground shadow teleports were removed; FG launches only for Shodan topology parity while the BG snapshot supplies speed, Z-stability, and arrival evidence.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementSpeedTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_speed_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs Tests/BotRunner.Tests/LiveValidation/AllianceNavigationTests.cs`

### 2026-04-25 (Corner/Tile navigation Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated corner/tile navigation live validation passed 6/6`
- Last delta:
  - `CornerNavigationTests` and `TileBoundaryCrossingTests` now dispatch only BG `ObjectiveType.TravelTo` after Shodan-directed navigation point staging.
  - Route checks cover Orgrimmar bank-to-AH, RFC corridor, Orgrimmar tile-boundary, and Durotar open-terrain tile-boundary movement.
  - Snapshot-only probes for Orgrimmar obstacles and Undercity tunnel staging remain fixture-owned setup with no BotRunner production action.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests|FullyQualifiedName~TileBoundaryCrossingTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=corner_tile_navigation_shodan.trx"` -> `passed (6/6)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MovementSpeedTests.cs`

### 2026-04-25 (TravelPlanner Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated TravelPlanner live validation passed overall with 1 executable pass and 3 tracked skips`
- Last delta:
  - `TravelPlannerTests` now dispatches only BG `ObjectiveType.TravelTo` after Shodan-directed street-level Orgrimmar staging.
  - The short Orgrimmar route passes and proves action delivery plus movement start from the staged position.
  - The long Crossroads probes stay tracked skips because delivered `TravelTo` starts `GoToTask` but produces no position delta after 20s and leaves BG `CurrentAction=TravelTo`.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelPlannerTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=travel_planner_shodan.trx"` -> `passed overall (1 passed, 3 skipped)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/CornerNavigationTests.cs Tests/BotRunner.Tests/LiveValidation/TileBoundaryCrossingTests.cs`

### 2026-04-25 (MountEnvironment Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated MountEnvironment live validation passed 4/4`
- Last delta:
  - `MountEnvironmentTests` now dispatches only BG `ObjectiveType.CastSpell` after Shodan-directed mount loadout and scene-position staging.
  - Fixture-owned `.learn`, `.setskill`, `.dismount`, `.unaura`, and `.go xyz` commands remain outside the test body because they are setup, not production BotRunner behavior.
  - Outdoor success is verified by snapshot `MountDisplayId`; indoor block is verified by snapshot chat/error evidence.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MountEnvironmentTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mount_environment_shodan.trx"` -> `passed (4/4)`.
  - Session Ratchet anchor `fishing_shodan_anchor.trx` -> `failed in known FG fishing cast/loot instability (loot_window_timeout -> max_casts_reached); not a MountEnvironment regression`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/TravelPlannerTests.cs`

### 2026-04-25 (MapTransition Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated MapTransition live validation passed 1/1`
- Last delta:
  - `MapTransitionTests` now dispatches only a post-bounce `ObjectiveType.Goto` after Shodan-directed Ironforge and rejected Deeprun Tram staging.
  - The fixture-owned map 369 `.go xyz` remains outside the test body because BotRunner has no production ObjectiveType for forcing a server-rejected instance teleport.
  - BG liveness after the bounce is verified through a correlated command ACK; FG stays idle for topology parity.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MapTransitionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=map_transition_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MountEnvironmentTests.cs`

### 2026-04-25 (SpiritHealer Shodan migration dispatch fix)
- Pass result: `BotRunner spirit-healer InteractWith dispatch now uses the BG dead-actor packet path; migrated SpiritHealer live validation passed 1/1`
- Last delta:
  - `ActionDispatcher` now routes dead/ghost `InteractWith` to the spirit-healer activation branch before the generic gameobject fallback, because runtime object collections can expose the healer GUID outside the typed `Units` view.
  - The branch greets the target NPC and calls `DeadActorAgent.ResurrectWithSpiritHealerAsync(...)`, matching the MaNGOS `CMSG_SPIRIT_HEALER_ACTIVATE` path.
  - `BotRunnerServiceCombatDispatchTests` covers spirit-healer routing when the unit is present, when the unit is missing, and when the GUID also appears in `GameObjects`.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceCombatDispatchTests" --logger "console;verbosity=minimal"` -> `passed (15/15)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpiritHealerTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=spirit_healer_shodan_deadactor_order.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs`

### 2026-04-25 (NPC Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated NPC live validation passed 3 with 1 tracked trainer funding/mailbox skip`
- Last delta:
  - `NpcInteractionTests` now dispatches only NPC interaction `ObjectiveType` messages or asserts snapshots after Shodan-directed staging.
  - Vendor, flight-master, and object-manager coverage runs on FG/BG action targets; `Trainer_LearnAvailableSpells` is skipped after Shodan launch because live funding for trainer purchases is blocked by the documented money/mailbox staging gap.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NpcInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=npc_interaction_shodan.trx"` -> `passed 3, skipped 1`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs`

### 2026-04-25 (Quest Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated quest-group live validation passed 6/6`
- Last delta:
  - `GossipQuestTests`, `QuestObjectiveTests`, `QuestInteractionTests`, and `StarterQuestTests` now dispatch only quest/gossip/combat `ObjectiveType` messages or assert fixture-staged quest snapshot state after Shodan-directed setup.
  - The suite reuses `Economy.config.json`; `ECONBG1` receives actions, `ECONFG1` stays idle for topology parity, and SHODAN remains director-only.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GossipQuestTests|FullyQualifiedName~QuestObjectiveTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=quest_group_shodan_rerun.trx"` -> `passed (6/6)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor_quest_slice.trx"` -> `failed (known Ratchet anchor instability: FG loot_window_timeout/max_casts_reached)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs`

### 2026-04-25 (Trading Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated Trading/TradeParity live validation passed 1 with 3 tracked foreground trade action skips`
- Last delta:
  - `TradingTests` now dispatches only trade `ObjectiveType` messages after Shodan-directed trade staging; the BG offer/decline cancel proof passed.
  - `TradeParityTests` and the item/gold transfer path remain explicit skips after Shodan launch/resolve because the foreground trade runtime currently ACKs `DeclineTrade`, `OfferItem`, and `AcceptTrade` as `Failed/behavior_tree_failed`.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_shodan_final.trx"` -> `1 passed, 3 skipped`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/GossipQuestTests.cs Tests/BotRunner.Tests/LiveValidation/QuestObjectiveTests.cs Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`

### 2026-04-25 (Mail Shodan migration observation)
- Pass result: `BotRunner now emits structured mail collection diagnostics; deterministic dispatch coverage stayed green and migrated MailSystem/MailParity live validation passed 4/4 with FG and BG actions`
- Last delta:
  - `MailSystemTests` and `MailParityTests` now dispatch only `ObjectiveType.CheckMail` after Shodan-directed mailbox and SOAP mail staging.
  - `ActionDispatcher` records `[MAIL-COLLECT]` markers from `CollectAllMailWithResultAsync(...)`, giving FG and BG mail tests structured completion evidence when snapshot deltas lag.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> `passed (4/4)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/TradingTests.cs Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`

### 2026-04-25 (EconomyInteraction Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated EconomyInteraction live validation passed 3/3`
- Last delta:
  - `EconomyInteractionTests` now dispatches only `ObjectiveType.InteractWith` for banker/auctioneer and `ObjectiveType.CheckMail` for mailbox collection after Shodan-directed staging.
  - FG and BG both passed the bank, AH, and mail interaction baselines.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EconomyInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=economy_interaction_shodan.trx"` -> `passed (3/3)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`

### 2026-04-25 (VendorBuySell Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated VendorBuySell live validation passed 2/2`
- Last delta:
  - `VendorBuySellTests` now dispatches only `ObjectiveType.BuyItem`, `ObjectiveType.SellItem`, and post-buy `DestroyItem` cleanup after Shodan-directed vendor/item/money staging.
  - The suite remains a BG vendor packet baseline; FG is launched for Shodan topology parity but does not receive vendor buy/sell actions in this slice.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~VendorBuySellTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=vendor_buy_sell_shodan.trx"` -> `passed (2/2)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele|modify money" Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`

### 2026-04-25 (Bank Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated bank live validation passed 1 with 3 tracked skips`
- Last delta:
  - `BankInteractionTests` now dispatches only `ObjectiveType.InteractWith` after Shodan-directed bank staging. Banker detection passes on FG/BG and the implemented banker interaction returns success.
  - `BankParityTests` now stages Linen Cloth through `StageBotRunnerLoadoutAsync`; deposit/withdraw and bank-slot purchase remain explicit missing-action skips because BotRunner has no bank deposit/withdraw/slot-purchase action surface yet.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BankInteractionTests|FullyQualifiedName~BankParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bank_shodan.trx"` -> `1 passed, 3 skipped`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs`

### 2026-04-25 (AuctionHouse Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated AuctionHouse live validation passed 3 with 2 tracked skips`
- Last delta:
  - `AuctionHouseTests` now dispatches only `ObjectiveType.InteractWith` after Shodan-directed AH staging. FG and BG auctioneer interactions return success.
  - `AuctionHouseParityTests` now stages Linen Cloth through `StageBotRunnerLoadoutAsync`; post/buy and cancel remain explicit missing-action skips because BotRunner has no auction post/buy/cancel action surface yet.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AuctionHouseTests|FullyQualifiedName~AuctionHouseParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=auction_house_shodan.trx"` -> `3 passed, 2 skipped`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs Tests/BotRunner.Tests/LiveValidation/BankParityTests.cs`

### 2026-04-25 (PetManagement Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and the migrated PetManagement live slice passed`
- Last delta:
  - `PetManagementTests` now dispatches `ObjectiveType.CastSpell` only after Shodan-directed hunter pet setup. BG Call Pet and Dismiss Pet both return success.
  - FG remains launched but idle in this slice because foreground spell-id casting is not the validated pet-management path.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PetManagementTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=pet_management_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/AuctionHouseTests.cs Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs`

### 2026-04-25 (Crafting Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and the migrated Crafting live slice passed`
- Last delta:
  - `CraftingProfessionTests` now dispatches `ObjectiveType.CastSpell` only after Shodan-directed First Aid staging. BG crafting produces one Linen Bandage from one Linen Cloth.
  - FG remains launched but idle in this slice because foreground spell-id casting is not the validated crafting path.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CraftingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=crafting_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/PetManagementTests.cs`

### 2026-04-25 (Gathering Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and the migrated Gathering live slice documents a foreground mining gap`
- Last delta:
  - `GatheringProfessionTests` now dispatches `ObjectiveType.StartGatheringRoute` only after Shodan-directed staging. BG mining and herbalism pass on the corrected route center.
  - FG mining receives the action and moves around active copper candidates, but never reports gather success, bag delta, or skill delta before timeout. This is documented in the slice doc and inventory as a foreground gathering functional gap, not a BotRunner code delta in this slice.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=gathering_shodan_level20.trx"` -> `2 passed, 1 skipped, 1 failed`; FG mining failure documented.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs`

### 2026-04-24 (Wand action dispatch support for Shodan migration)
- Pass result: `BotRunner wand dispatch coverage green; Equipment/Wand migrated live slice passed (2/2)`
- Last delta:
  - `BuildStartWandAttackSequence(targetGuid)` now primes the target action by selecting the target, stopping movement, and facing the target on the first tick, then faces again and starts Shoot on the next tick. This prevents foreground "target not in front" failures while preserving action-dispatched behavior.
  - Added `BotRunnerServiceCombatDispatchTests.BuildBehaviorTreeFromActions_StartWandAttack_FacesTargetBeforeShoot` to pin the two-tick face-before-shoot sequence.
  - The live `WandAttackTests` now proves FG and BG both receive wand actions on mage accounts while SHODAN stays director-only.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellDataTests|FullyQualifiedName~BotRunnerServiceCombatDispatchTests" --logger "console;verbosity=minimal"` -> `passed (118/118)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~WandAttackTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=equipment_wand_action_plan_fresh8.trx" *> "tmp/test-runtime/results-live/equipment_wand_action_plan_fresh8.console.txt"` -> `passed (2/2)`.
- Files changed:
  - `Exports/BotRunner/SequenceBuilders/CombatSequenceBuilder.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`

### 2026-04-24 (Heartbeat readiness for action dispatch)
- Pass result: `FG/BG action-dispatched Ratchet fishing is green in one shared Fishing.config.json launch; deterministic BotRunner dispatch/snapshot coverage stayed green`
- Last delta:
  - `BotRunnerService` now includes lightweight readiness fields on heartbeat-only snapshots (`ScreenState`, `ConnectionState`, `IsObjectManagerValid`, `IsMapTransition`). This gives StateManager current transition/readiness state before it consumes a queued one-shot action.
  - The fix preserves the simplified action-driven fishing flow and leaves `FishingTask.TryResolveCastPosition(...)` pathfinding-first. No `AssignedActivity: "Fishing[Ratchet]"` workaround was reintroduced.
  - Root cause from FG diag: `StartFishing` could be returned while FG was in a transition-skip loop; `UpdateBehaviorTree(...)` was skipped and the next object-manager snapshot cleared `CurrentAction`, so no `[ACTION-RECV]` appeared.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt"` -> `passed (1/1)`; FG diag shows `[ACTION-RECV] type=StartFishing params=3 ready=True` followed by `tasks=2(FishingTask)`, and the TRX shows FG/BG `fishing_loot_success`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `git status --short`

### 2026-04-24 (Fishing single-launch follow-up)
- Pass result: `StartFishing metadata forwarding shipped; pathfinding-first fishing standoff restored; deterministic coverage green and the focused live Ratchet slice is green twice after the BG LOS regression fix`
- Last delta:
  - `ActionDispatcher.StartFishing` now accepts the metadata shape `[location, useGmCommands, masterPoolId, waypoint floats...]` and forwards those values into `FishingTask`. Legacy float-only waypoint payloads still dispatch unchanged.
  - `FishingTask.TryResolveCastPosition(...)` is pathfinding-first again. This directly fixes the current BG Ratchet regression where `castSource=native` was selecting a too-far-inboard dock candidate that passed coarse LOS but cast into the pier.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_1.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.console.txt"` -> `passed (1/1)`; TRX shows FG/BG `castSource=pathfinding` and both `fishing_loot_success`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_2.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.console.txt"` -> `passed (1/1)`; TRX again shows FG/BG `castSource=pathfinding`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/BotRunnerServiceFishingDispatchTests.cs`
  - `docs/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_4.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_4.console.txt"`

### 2026-04-21 (P4.5)
- Pass result: `P4.5 coordinator + test migration to structured ACKs shipped; Phase P4 closed`
- Last delta:
  - `BattlegroundCoordinator.LastAckStatus(correlationId, snapshots)` scans every bot's `RecentCommandAcks` ring and returns the most recent status (terminal beats Pending).
  - `LiveBotFixture.BotChat.SendGmChatCommandTrackedAsync` stamps a `test:<account>:<seq>` correlation id on every tracked dispatch; `GmChatCommandTrace` exposes `CorrelationId` / `AckStatus` / `AckFailureReason`.
  - `LiveBotFixture.AssertTraceCommandSucceeded` prefers the ACK status when present and falls back to `ContainsCommandRejection`. `IntegrationValidationTests` and `TalentAllocationTests` now delegate their `AssertCommandSucceeded` helpers to it.
  - `BattlegroundCoordinatorAckTests` pins the `LastAckStatus` contract (null / Pending / terminal-over-Pending / cross-snapshot scan).
- Validation/tests run:
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundCoordinator|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests|FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~ActionForwardingContractTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
- Commits:
  - `4c39065c` `feat(coord): P4.5.1 add LastAckStatus helper on BattlegroundCoordinator`
  - `e8306a9f` `test(botrunner): P4.5.2/P4.5.3 expose AckStatus in GmChatCommandTrace`
- Next command: `rg -n "^- \\[ \\]|Active task:" docs/TASKS.md`

### 2026-04-21 (P4.4)
- Pass result: `P4.4 structured per-command ACKs shipped`
- Last delta:
  - `BotRunnerService` now tracks action correlation ids end-to-end, buffers a cap-10 `RecentCommandAcks` ring, stamps correlated `CurrentAction` clones into `_activitySnapshot`, and includes `RecentCommandAckCount` in `SnapshotChangeSignature` so ACK arrivals force immediate full snapshots without reintroducing the `P4.2` chat churn.
  - `HandleApplyLoadoutAction` seeds correlated step ids for `LoadoutTask`, and `LoadoutTask` now emits per-step `Pending`/`Success`/`TimedOut` `CommandAckEvent`s. Duplicate `ApplyLoadout` requests fail the duplicate correlation id without clobbering the original in-flight loadout ACK.
  - `CharacterStateSocketListener` now stamps `account:sequence` correlation ids on outbound actions when StateManager hands BotRunner an unstamped command.
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Exports/BotRunner/TASKS.md`
- Commits:
  - `9232c83f` `feat(comm): P4.4 add command ack proto schema`
  - `4d1b7489` `feat(botrunner): P4.4 plumb correlated command acks`
  - `3f800ed9` `test(botrunner): P4.4 cover command ack round-trips`
- Next command: `rg -n "LastAckStatus|SendGmChatCommandTrackedAsync|RecentCommandAcks|ContainsCommandRejection" Services/WoWStateManager Tests/BotRunner.Tests docs/TASKS.md`

### 2026-04-21 (P4.3)
- Pass result: `P4.3 LoadoutTask event-driven step advancement shipped`
- Last delta:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`: `LoadoutStep` gained `AttachExpectedAck`/`DetachExpectedAck` plus the `AckFired`/`MarkAckFired` plumbing. `LearnSpellStep`, `SetSkillStep`, and `AddItemStep` override `OnAttachExpectedAck` to install filtered subscriptions on `IWoWEventHandler.OnLearnedSpell` / `OnSkillUpdated` / `OnItemAddedToBag`. `LoadoutTask.Update` attaches all acks once on first tick, detaches per-step when `IsSatisfied` flips, and detaches everything on terminal (Ready/Failed). `_acksAttached` guards against double-subscribing on re-entry.
  - Polling still runs every tick; the event handle is an optional latency optimization that flips `IsSatisfied` on the very next `Update()` without waiting for the 100ms pacing tick. No SMSG-less command (`.levelup`, `.additemset`, `.use`) changed behavior.
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
- Files changed:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Commits: `8add32e9 feat(botrunner): P4.3 event-driven LoadoutTask step advancement`
- Scope note: `P4.4` (correlation ids + `CommandAckEvent`) and `P4.5` (coordinator + test migration) are still open in `docs/TASKS.md` and were intentionally not started.
- Next command: `rg -n "correlation_id|CommandAckEvent|RecentCommandAcks" Exports/BotCommLayer docs/TASKS.md`

### 2026-04-21 (P4.1/P4.2)
- Pass result: `P4.1/P4.2 BotRunner plumbing shipped`
- Last delta:
  - `BotRunnerService.Messages` now buffers learned/unlearned spell, skill-update, item-added, error, and system-message events through the shared FG/BG event surface.
  - `SnapshotChangeSignature` no longer counts recent chat/error buffer lengths, and `BotRunnerServiceSnapshotTests.Start_WhenOnlyDiagnosticMessagesChange_KeepsHeartbeatOnlyUntilHeartbeatInterval` pins the no-churn heartbeat behavior.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`
- Previous handoff preserved below.

- Last updated: 2026-04-20
- Active task: carry the now-green WSG desired-party/live-objective path forward into the next battleground objective slice.
- Last delta:
  - `BotRunnerService.DesiredParty.GetCurrentGroupSize(...)` now treats the local player as the fifth member when `PartyAgent.GroupSize == 4` / `GetGroupMembers().Count == 4`, matching the live `SMSG_GROUP_LIST` contract that excludes self. That fixes the Horde 5-player-party ceiling that previously prevented WSG leaders from converting to raid and inviting the last five queue members.
  - `BotRunnerServiceDesiredPartyTests` now pins that exact `PartyAgent` contract and verifies it still drives the current `IObjectManager.ConvertToRaid()` execution path.
  - `BgTestHelper.WaitForBotsAsync(...)` now prints the specific raw snapshot(s) missing from `AllBots` whenever live hydration stalls, which turns the old `19/20` aggregate into actionable account-level diagnostics.
  - The WSG objective scenarios now run as `WsgFlagCaptureObjectiveTests` and `WsgFullGameObjectiveTests` on separate fixture collections, so each destructive live scenario gets a fresh 20-bot roster instead of inheriting the previous match's transfer residue.
- Pass result: `BotRunner desired-party reconciliation is proven by green live WSG objective coverage`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FullGame_CompletesToVictoryOrDefeat" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_fullgame_after_group_size_fix_20260421_0210.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "(FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFlagCaptureObjectiveTests|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFullGameObjectiveTests)" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_objective_split_fixtures_20260421_0337.trx"` -> `passed (2/2)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.DesiredParty.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchObjectiveCollection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`
- Previous handoff notes:
  - `ActionDispatcher.JoinBattleground` now upserts `BattlegroundQueueTask` instead of unconditionally pushing, so repeated queue dispatch cannot stack duplicate battleground queue tasks on the BotRunner stack.
  - Added deterministic regression coverage in `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs` for both the first queue push and the duplicate-dispatch no-growth case.
  - The same battleground slice also depended on the AB fixture changes that moved the live queue/entry rerun fully onto background runners; the fresh live AB rerun passed after the earlier foreground-transfer crash.
  - `PathfindingClient` exposes a local short-horizon segment simulation hook backed by `NativeLocalPhysics.Step`.
  - `NavigationPath` now rejects service route segments that local physics proves climb onto the wrong route layer and repairs them through nearby same-layer detour candidates.
  - The repair path keeps strict local-physics/support/width checks for the short detour leg and avoids using the noisy downstream lateral-width probe as a veto on the longer ramp stitch-back leg.
  - Long service segments are no longer rejected solely because the short-horizon local simulation reports `hit_wall` when route-layer metrics remain consistent.
  - Corpse-run routes now advance close waypoints without the standard probe-corridor shortcut veto because `NavigationRoutePolicy.CorpseRun` deliberately disables probe heuristics.
  - The live Orgrimmar bank-to-auction-house route now arrives instead of looping back over the corner waypoint.
  - Foreground ghost forward input mitigation remains guarded and unit-covered. The opt-in FG corpse-run rerun now passes and restores strict-alive state.
- Pass result: `JoinBattleground queue-task upsert is pinned and the AB queue-entry rerun is green`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_AcceptsLongLocalPhysicsHorizonHit_WhenRouteLayerRemainsConsistent|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RejectsShortLocalPhysicsHitWall|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_after_corpse_probe_policy.trx"` -> `passed (1/1)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`
- Files changed:
  - `Exports/BotRunner/Clients/PathfindingClient.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tests/ForegroundBotRunner.Tests/ObjectManagerMovementTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
