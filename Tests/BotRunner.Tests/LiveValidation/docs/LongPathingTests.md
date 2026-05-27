# LongPathingTests

Focused staged long-pathing validation for routes that require more than one
travel mode.

## Bot Execution Mode

**Default roster: Shodan-directed FG action** - `LongPathing.config.json`
launches `LPATHFG1` as a Tauren Male foreground Warrior action target,
`LPATHBG1` idle for topology parity, and SHODAN as director. The test body
dispatches only `ObjectiveType.TravelTo` to the configured foreground target
after fixture-owned Crossroads staging and taxi readiness.

**Alternate roster: Shodan foreground action** - set
`WWOW_LONG_PATHING_SETTINGS_PATH` to
`Services/WoWStateManager/Settings/Configs/LongPathing.ShodanForeground.config.json`
to rerun the same live proof with SHODAN itself as the configured foreground
target and `LPATHBG1` as the background parity bot. This is intended for
capsule/regression confirmation on the same route, not as the default suite
topology.

The test scopes `Injection__DisablePacketHooks=true` and
`WWOW_DISABLE_PACKET_HOOKS=1` for the full run because the Orgrimmar ->
Undercity zeppelin is a foreground cross-map world transfer, matching the
packet-hook crash guard used by dungeon and battleground transfer fixtures.

The fixture resolves its roster from `WWOW_LONG_PATHING_SETTINGS_PATH` when
that env var is set; otherwise it falls back to the checked-in
`LongPathing.config.json`.

## 2026-05-26 Focused Live Status

The current focused `DeckLipClimbFromGruntToFrezza` live proof is no longer
blocked at the Grunt-base spawn by a missing local-dev PathfindingService
handoff. The relevant bounded fix was in the live fixture/orchestration layer:
the fixture spawned `PathfindingService.exe` on `127.0.0.1:9020`, but
StateManager and direct live-test probes were still using the constructor-time
default `127.0.0.1:9002` until `BotServiceFixture` was changed to resolve the
current endpoint from environment at use time.

Focused verification commands and results:

- `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingFixtureConfigurationTests|FullyQualifiedName~BgOnlyBotFixtureConfigurationTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~IBotTaskContractTests|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_CrossMap_UpsertsPersistentTravelTask" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_pathfinding_port_handoff_regression_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner`
  Result: `passed (27/27)`.
- `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_CLIMB_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_tauren_fg_20260526_localpf_portfix_probe_fix3.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000`
  Result: `failed (1/1)` after ~`59s`, but the failure moved deeper into the
  route and now proves the local service is really in play.

Exact artifact paths for this slice:

- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner\botrunner_pathfinding_port_handoff_regression_20260526.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_tauren_fg_20260526_localpf_portfix_probe_fix3.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-Frezza-likely-LPATHFG1-client-19028-win0-20260526_184905.png`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToFrezza\`
- `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
- `D:\World of Warcraft\WWoWLogs\fg_LPATHFG120260526.log`

Important proof points from the rerun:

- `PathfindingTestFixture` launched `PathfindingService.exe` on
  `127.0.0.1:9020` and recorded `ready on port 9020`.
- `BotServiceFixture` logged
  `[StateManager] PathfindingService endpoint=127.0.0.1:9020`.
- The bot processes attempted and established `127.0.0.1:9020` connections
  instead of Docker `9002`.
- The focused live proof moved off the original spawn stall
  `(1332.8,-4633.4,24.0)` and advanced through multiple walk-leg waypoints.
- The current red is later: near `anchor=(1352.0,-4527.1,35.5)` /
  `current=(1351.3,-4526.3,34.5)` the local service returns:
  - smoothed request: `corners=5 result=raw_detour blockedReason=none`
  - unsmoothed request: `corners=2 result=raw_detour blockedReason=none`
- BotRunner then rejects both returned routes locally:
  - `[NavigationPath] Path rejected by IsPathUsable: raw=5 ... start=(1351.3,-4526.3,34.5) end=(1320.1,-4653.2,53.9)`
  - `[NavigationPath] Path rejected by IsPathUsable: raw=2 ...`
  - `[NAV-DIAG] TryNavigateToward: GetNextWaypoint returned null. pos=(1351.3,-4526.3,34.5), dest=(1320.1,-4653.2,53.9), map=1`

Treat the next iteration as a BotRunner local route-validation problem on the
current promoted tile, not as a missing local pathfinding service or a
Tauren-vs-Gnome capsule split.

## 2026-05-26 Raw Path Contract Follow-Up

The next bounded slice built on commit `1238aba6`
(`Fix long-pathing local service port handoff`) without rebaking the promoted
tile. The local service was still returning the same tiny later tower-approach
stub route, but the service contract no longer hides that stub as a clean
successful path.

Focused verification commands and results:

- `dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RawPathContractTests|FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~SlicedFindPathTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_raw_contract_projection_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  Result: `aborted` because `WWOW_DATA_DIR` was unset and the strict
  PathfindingService data-root gate exited before test execution.
- `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RawPathContractTests|FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~SlicedFindPathTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_raw_contract_projection_20260526_fix2.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  Result: `passed (10/10)`.
- `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_CLIMB_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_tauren_fg_20260526_endpoint_projection_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000`
  Result: `failed (1/1)` after ~`62s`, still at the later tower wall/dirt
  stall.

Exact artifact paths for this follow-up:

- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\pathfinding_raw_contract_projection_20260526.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\pathfinding_raw_contract_projection_20260526_fix2.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_tauren_fg_20260526_endpoint_projection_fix1.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-Frezza-likely-LPATHFG1-client-30036-win0-20260526_190904.png`
- `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
- `D:\World of Warcraft\WWoWLogs\fg_LPATHFG120260526.log`

Important proof points from the rerun:

- The screenshot still shows the FG target jammed into the tower wall/dirt at
  the later anchor, so the visual state agrees with the logs.
- The raw Detour response is still only a short local stub:
  - smooth: 5 corners ending near `(1352.2,-4527.0,36.2)`
  - straight: 2 corners ending near `(1352.2,-4527.0,35.7)`
- The service now reports the real problem instead of claiming success:
  - `[PATH_DIAG] id=16 result=raw_detour pathLen=5 rawPathLen=5 blockedIdx=3 blockedReason=end_projection:130.2`
  - `[PATH_DIAG] id=17 result=raw_detour pathLen=2 rawPathLen=2 blockedIdx=0 blockedReason=end_projection:130.2`
  - `[NAV_PATH] service-request exit elapsedMs=1 corners=5 result=raw_detour blockedIndex=3 blockedReason=end_projection:130.2`
  - `[NAV_PATH] service-request exit elapsedMs=1 corners=2 result=raw_detour blockedIndex=0 blockedReason=end_projection:130.2`

Treat the next iteration as a true local path/topology investigation from
`(1351.3,-4526.3,34.4)` toward `(1320.1,-4653.2,53.9)` on the current
promoted data, not as a silent contract or fixture bug.

## 2026-05-26 Literal Frezza Direct Proof

Commit `aac53962` (`Add literal Frezza deck-lip proof`) added a second focused
surface for the user’s coordinate challenge: same Grunt-base staging, but
`TravelTo` goes directly to Zeppelin Master Frezza’s literal map-1 spawn
instead of the Undercity objective that later resolves through the boarding
corridor.

Focused verification commands and results:

- `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeckLipRawPathContractTests|FullyQualifiedName~WaypointDumpDiagnostic.Dump_GruntToFrezza_PolygonChain|FullyQualifiedName~WaypointDumpDiagnostic.Compare_GruntToFrezza_vs_GruntToSnurk_SmoothPaths|FullyQualifiedName~RawPathContractTests" --logger "console;verbosity=normal" --logger "trx;LogFileName=pathfinding_grunt_literal_frezza_contract_20260526_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  Result: `passed (7/7)`.
- `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_literal_frezza_tauren_fg_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000`
  Result: `failed (1/1)` after ~`38s`.

Exact artifact paths for this direct-target slice:

- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\pathfinding_grunt_literal_frezza_contract_20260526_fix1.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-literal-Frezz-LPATHFG1-client-40164-win0-20260526_202201.png`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\`
- `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
- `D:\World of Warcraft\WWoWLogs\fg_LPATHFG120260526.log`

Important proof points from the direct-target rerun:

- The deterministic service surface proves the literal Frezza coords are
  valid on the promoted tile:
  - `Navigation.CalculateRawPath(...)` returned
    `len=144 blockedSeg=97 blockedReason=interior_projection:98`
    with final waypoint `(1328.32,-4649.35,53.84)`, only `2.79y` from Frezza.
  - `WaypointDumpDiagnostic.Dump_GruntToFrezza_PolygonChain` logged
    `TotalPolyCount: 68`, `off-mesh count: 1`.
- The live proof does query the exact Frezza coords:
  - `[PATH_DIAG] ... start=(1332.8,-4633.4,24.0) end=(1331.1,-4649.5,53.6) ... pathLen=144 ...`
- But the live same-map objective never turns into the expected `TravelTask`
  climb:
  - latest snapshot chat only shows `[GOTO_ROUTE] plan=1 route=none` and
    `[TASK] GoToTask pop reason=arrived`
  - no `[TRAVEL_PLAN]`, `[TRAVEL_LEG]`, `[TRAVEL_WALK_NAV]`, or
    `[TRAVEL_WAYPOINT_REACHED]` lines are emitted before failure
  - failure anchor/current stays at the Grunt-base spawn lane:
    `anchor=(1332.8,-4633.4,24.0) current=(1332.1,-4634.5,23.9) moved=1.3`

Treat the next iteration as a same-map `TravelTo` decomposition / task-name
selection bug in BotRunner, not as a wrong Frezza coordinate or a missing
direct path on the promoted tile.

## 2026-05-26 Vertical-Arrival Follow-Up

Built on top of commit `870a78a0` (`Document literal Frezza long-pathing proof`)
without rebaking the promoted tile. This bounded slice closed the false
same-map arrival gap that let the literal Frezza proof stop from the lower
Grunt-base lane just because XY fell inside the legacy `15y` radius.

Focused verification commands and results:

- `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_SameMap_UpsertsPersistentGoToTask|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_AlreadyWithinLegacyArrivalTolerance_StopsWithoutTask|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_WithinHorizontalToleranceButWrongVerticalLayer_UpsertsPersistentGoToTask|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_CrossMap_UpsertsPersistentTravelTask|FullyQualifiedName~BotRunnerServiceGoToDispatchTests|FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~Update_RequireVerticalArrival_DoesNotPopTaskWhenOnlyWithinHorizontalTolerance" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_same_map_travelto_vertical_arrival_20260526_fix2.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner`
  Result: `passed (12/12)`.
- `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_literal_frezza_tauren_fg_20260526_vertical_arrival_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000`
  Result: `failed (1/1)` after ~`28s`.

Exact artifact paths for this follow-up:

- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner\botrunner_same_map_travelto_vertical_arrival_20260526_fix2.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526_vertical_arrival_fix1.trx`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-wall-collision-creep-before-OG-zeppelin-tower-ramp-climb-from-base-t-LPATHFG1-client-20676-win0-20260526_204920.png`
- `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\`

Important proof points from the rerun:

- The earlier false completion is gone:
  - old: `[TASK] GoToTask pop reason=arrived`
  - new: no `arrived` pop before failure
- The live proof now stays active long enough to move from
  `(1332.1,-4634.5,23.9)` to `(1329.7,-4635.0,23.8)` before stalling with
  forward intent held and `currentSpeed=0.00`.
- The service still answers the literal Frezza request during the live run:
  - `[PATH_DIAG] ... start=(1332.8,-4633.4,24.0) end=(1331.1,-4649.5,53.6) ... pathLen=144 blockedReason=interior_projection:98`
- The screenshot now shows the FG target jammed against nearby spawn/tent prop
  geometry at the Grunt base instead of falsely "arriving" below the ramp.
- The current live gap is still upstream of the expected staged travel
  executor:
  - latest snapshot chat shows `[GOTO_ROUTE] plan=1 route=none drops=0 cliffs=0 vertical=0`
  - no `[TRAVEL_PLAN]`, `[TRAVEL_LEG]`, `[TRAVEL_WALK_NAV]`, or
    `[TRAVEL_WAYPOINT_REACHED]` lines appear

Treat the next iteration as a same-map `TravelTo` dispatch/executor problem:
the objective no longer false-completes below Frezza, but it still runs as a
`GoToTask`/`route=none` path instead of entering the `TravelTask` staged walk
surface that owns `TRAVEL_*` diagnostics.

## Test Methods

- `CrossroadsToUndercity_UsesFlightAndZeppelin`: stages the Horde target at
  Crossroads, grants taxi access through the existing fixture helper, dispatches
  `TravelTo` to the Undercity target, and requires evidence for the staged
  route: Crossroads taxi `25 -> 23`, Orgrimmar taxi arrival, walking to the
  Orgrimmar zeppelin tower, actual zeppelin transport state or map transfer,
  Eastern Kingdoms arrival, and final proximity to the Undercity destination.
- `DeckLipClimbFromGruntToFrezza`: gated by `WWOW_DECKLIP_CLIMB_TEST=1`.
  Teleports directly to the zeppelin-tower base Grunt and requires
  `[TRAVEL_LEG] complete index=0 reason=walk_arrived` on the same
  Undercity-bound walk leg that previously stalled on the deck lip. This is
  the fastest live proof for the `40,29` lip/ramp lane.
- `DeckLipClimbFromGruntToLiteralFrezza`: gated by
  `WWOW_DECKLIP_DIRECT_FREZZA_TEST=1`. Uses the same Grunt-base staging, but
  dispatches `TravelTo` directly to Frezza's literal map-1 spawn. This is the
  cleanest live proof for "are we using the right coordinates?" on the current
  promoted tile.

## Fast-Fail Blockers

The Orgrimmar flight-master -> zeppelin walk now fails quickly when the live
route enters known bad object/terrain states instead of waiting for the broad
walk timeout:

- Bonfire/object choke after taxi landing near `(1673,-4334,53)`.
- Palm-tree descent collision near `(1605,-4425,10)`.
- Steep-incline route selection, including `[TRAVEL_WALK_NAV]` diagnostics
  with `afford=SteepClimb` for the zeppelin walk target.
- Tower support/flagpole object collision near `(1371,-4439,31)`.
- Tower base/deck mismatch near `(1343,-4641,25)`, including diagnostics with
  `nav=False`, `resolution=no_route`, and `active=none`.

The tower approach success check also requires deck-ish Z before the test moves
on to the zeppelin leg; ground-level proximity to `(1341.0,-4638.6)` is no
longer enough.

## Diagnostic Screenshot Modes

Long-pathing reverse-engineering is screenshot-first. Use these modes before
guessing at bake or runtime fixes:

- `WWOW_LONG_PATHING_TIMELINE=1`
  The test writes paired PNG+JSON timeline checkpoints under
  `tmp/test-runtime/screenshots/long-pathing/timeline/<TestName>/`.
- `WWOW_OG_RAMP_WAYPOINT_INSPECT=1`
  Runs `OgRampWaypointInspect`, which teleports to each suspect zeppelin-ramp
  waypoint, waits for settle, and captures screenshot + snapshot evidence.
- `WWOW_OG_DECK_LIP_VERIFY=1`
  Runs `OgDeckLipAnchorVerification`, which does the same for deck-lip support
  points where the path and the real standable surface may disagree.

For short suspect routes or a narrowed suspect window, prefer one capture per
reached waypoint plus every stall/replan/failure checkpoint. For longer routes,
keep the timeline checkpoints at every poll interval and never skip stall
evidence.

## Offline Route Gate

Live Crossroads -> Undercity validation is gated by
`PathfindingService.Tests.LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`.
That test calculates the Orgrimmar flight-master -> zeppelin walking route
with the Tauren Male capsule and fails before launching WoW if the generated
path clips:

- Lower flight-master bonfire:
  `guid=10975 entry=177026 display=4572 pos=(1665.50,-4360.83,26.66)`.
- Bank-front palm/static model snag near `(1605.00,-4425.20,10.20)`.
- Bank-front bonfire:
  `guid=10090 entry=177019 display=4572 pos=(1592.37,-4427.32,8.05)`.
- Z-hallway early-cut north and south corners near
  `(1513.20,-4415.90,20.00)` and `(1415.30,-4372.90,25.30)`.
- Exterior steep incline near `(1383.00,-4385.00,28.00)`.
- Exterior rope-line support near `(1371.10,-4439.40,30.90)`.

As of May 1, 2026, the offline gate is red against `D:\MaNGOS\data` after
removing an invalid PathfindingService static-clearance workaround. Live
Crossroads -> Undercity validation remains paused until regenerated GO-aware
mmaps make normal pathfinding avoid these blockers.

Do not make this gate pass by adding route-specific production code: no
hardcoded Orgrimmar clearance cylinders, no waypoint exceptions, and no
live-position guard as a substitute for static collision in the generated
navmesh. The live blocker guard is diagnostic only; the fix belongs in
gameobject export, mmap generation, and regenerated map data.

## Runtime Linkage

- `TravelTo` now queues `TravelTask` for cross-map destinations.
- `CrossMapRouter` must plan the route as staged objectives instead of a
  single direct path or a Ratchet/Booty Bay neutral shortcut.
- Walk legs must be resolved through PathfindingService with the configured
  Tauren Male capsule. `[TRAVEL_WALK_NAV]` diagnostics include the resolved
  agent race/gender and capsule so live failures can be tied to the exact
  request metadata.
- Flight-path activation uses the existing object-manager taxi packet path.
- Zeppelin handling uses `TransportWaitingLogic` and snapshot evidence for the
  Orgrimmar/Undercity route. Nearby zeppelin objects are diagnostic only; the
  assertion requires `TransportGuid` / `ONTRANSPORT` or map transfer evidence,
  and the transfer-evidence wait is long enough to cover the scheduled
  transport wait budget. Transport diagnostics include the expected
  gameobject entry/display/name and nearest objects formatted with GUIDs, with
  static (`0xF120`) and moving (`0x1FC0`) transport GUIDs decoded back to
  route entries before matching.
- Failure screenshots are captured from the managed WoW.exe PID for the target
  account; desktop or unrelated game-window fallbacks are intentionally not
  accepted as evidence.
