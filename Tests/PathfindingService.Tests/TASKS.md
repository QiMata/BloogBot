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

- [x] `PFS-ROUTEPACK-002` - Add deterministic coverage for the latest live
  lower-incline recovery gap near `(1363.9,-4377.8,26.1)`. The route target
  has moved from the old between-NPC deck point to the screenshot-derived
  Orgrimmar -> Undercity gangplank at
  `(1320.142944,-4653.158691,53.891945)`. Any new seed or recurring-recovery
  warmup must still be generated from current Navigation output and must
  reject unsafe vertical-layer suffix attachment.

- [x] `PFS-CACHE-001` - Add deterministic coverage for PathfindingService-owned
  route result caching. Coverage must prove static-overlay quantized hits,
  dynamic-overlay bypass, in-flight request coalescing, and short-TTL negative
  cache expiry.

- [x] `PFS-METRICS-001` - Add deterministic coverage proving
  `NavigationPerformanceMetrics` increments for resolver attempts and managed
  validation without requiring native Navigation calls.

- [x] `PFS-SOCKET-LOG-001` - Add deterministic socket coverage proving clean
  post-response EOF no longer logs `Unexpected EOF`, while truncated payload
  EOF remains a warning.

- [x] `PFS-ROUTEPACK-003` - Add deterministic coverage for bounded route-pack
  generation timeout so slow native pack generation is a fast unavailable pack
  rather than a session hang.

## Simple Command Set
1. Full project sweep: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
2. Reroute + corpse-run focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
3. Route validity focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`

## Session Handoff
### 2026-05-26 (Grunt #1 -> Frezza socket contract proof)
- Active task: answer the "wrong coordinates or wrong service port?" question
  against the exact lower-deck Grunt NPC spawn
  `(1332.76,-4633.40,24.0783)` and literal Frezza spawn
  `(1331.11,-4649.45,53.6269)` on the promoted
  `D:\wwow-bot\test-data\mmaps\0012940.mmtile` baseline.
- Pass result: `delta shipped; direct Navigation and isolated-port socket
  contract now agree on the same exact Grunt #1 -> Frezza route signature`.
- Last delta:
  - Added
    `PathfindingSocketServerIntegrationTests.HandlePath_DeckLipGruntNpcToLiteralFrezza_ReturnsCurrentServicePathThroughIsolatedPort`
    to prove the normal protobuf/TCP service contract returns the same
    `raw_detour` corridor the direct Navigation contract already reported.
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeckLipRawPathContractTests.CalculateRawPath_DeckLipGruntBaseToLiteralFrezza_EndsNearRequestedTargetDespiteInteriorProjectionGap|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_DeckLipGruntNpcToLiteralFrezza_ReturnsCurrentServicePathThroughIsolatedPort" --logger "console;verbosity=normal" --logger "trx;LogFileName=decklip_grunt1_to_frezza_socket_contract_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `passed (2/2)`.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\decklip_grunt1_to_frezza_socket_contract_20260526.trx`
- Practical read:
  - Direct contract: `Literal Frezza path: result=raw_detour len=144 blockedSeg=97 blockedReason=interior_projection:98 final=(1328.32,-4649.35,53.84) dist2D=2.79 dz=0.21`
  - Socket contract: `Socket literal Frezza path: result=raw_detour len=144 blockedSeg=97 blockedReason=interior_projection:98 firstDist2D=0.00 final=(1328.32,-4649.35,53.84) dist2D=2.79 dz=0.21`
  - The promoted data and the normal service contract agree on the same route,
    so the leading suspect remains the later live execution / waypoint
    promotion surface, not bad Grunt/Frezza coordinates and not the isolated
    local service port.
- Commit: `fc01c417` (`Add socket proof for Grunt to Frezza path`)
- Next command: `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelKeepsTightDescendingRopeStepBeforeStallPromotion|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelMovementStuckPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelWallRecoveryPromotesExistingCorridorBeforeReplanning" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner`

### 2026-05-13 (Focused mmap regen visibility pass)
- Active task: refresh visual mmap artifacts after focused OG/BRD/BRM runtime
  tile regeneration.
- Pass result: `delta shipped; OG guard green, BRD/BRM still red as bake
  fidelity evidence`.
- Last delta:
  - Regenerated runtime tiles `0012940.mmtile`, `0004533.mmtile`, and
    `0004634.mmtile` in `D:\MaNGOS\data\mmaps`.
  - Re-rendered stable latest artifacts under
    `tmp/test-runtime/visualization/pathfinding/og-zeppelin/latest/` and
    `tmp/test-runtime/visualization/pathfinding/brd/latest/`.
  - Updated summaries and suspicious polygon OBJs. OG top-deck crop remains
    below guard thresholds. BRD/BRM still show large vertical-span polygons.
- Validation/tests run:
  - Focused `NavDataAudit` for OG tile `1 40,29` -> passed.
  - Focused `NavDataAudit` for BRD/BRM map-0 tiles -> Detour/header capsule
    checks passed; GO-bake assertions failed because those tiles have no
    modeled GO spawn origins and only fallback GO span boxes.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Debug --no-build -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons" --logger "console;verbosity=minimal"` -> passed `1/1`.
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; .\tools\scripts\summarize-pathfinding-reference.ps1 -Route all`

### 2026-05-13 (NavSummary resolver coverage)
- Active task: deterministic coverage for the optional nav-summary accelerator.
- Pass result: `delta shipped; summary expansion/fallback tests green`.
- Last delta:
  - Added `NavSummaryRouteResolverTests` covering detailed-leg expansion,
    dynamic-overlay bypass, failed detailed-leg fallback, and route-cache
    signature activation.
- Validation/tests run:
  - `dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Debug --no-build -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavSummaryRouteResolverTests" --logger "console;verbosity=minimal"` -> passed `4/4`.
- Next command: `dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Debug --no-build -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavSummaryRouteResolverTests" --logger "console;verbosity=minimal"`

### 2026-05-12 (S1.3 OG tower mmap/physics drill-down)
- Active task: S1.3 remains blocked on the Orgrimmar zeppelin tower
  deterministic route gate.
- Pass result: `blocked; exact tower segment is physically unreachable and
  final route selection still returns it`.
- Last delta:
  - Confirmed the correct tile convention for the OG tower:
    MmapGen tile `(40,29)`, runtime file `0012940.mmtile`, config key
    `"4029"`. Earlier `(29,40)` / `0014029.mmtile` notes were wrong-tile
    artifacts and must not be used.
  - Isolated the current underpass red segment:
    `94->95 from=(1342.667,-4653.067,39.509) to=(1340.800,-4652.000,40.509)`.
  - Direct segment trace shows the Tauren capsule slides along a wall:
    final position stays near X `1342.7`, endpoint miss `1.87`, repeated wall
    normal `(1,0,0)`.
  - In-tree MmapGen regenerated `0012940.mmtile` with GO bake support,
    source/stage diagnostics, and per-tile `maxVertsPerPoly=3`; the top
    ramp/deck mesh-quality guard now passes.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/underpass_sim_anchor_diagnostics.trx`
  - `tmp/test-runtime/results-pathfinding/spiral_reachability_segment94_trace.trx`
  - `tmp/test-runtime/visualization/pathfinding/og-zeppelin/latest/analysis/summary.md`
  - `Tests/PathfindingService.Tests/MmapMeshQualityTests.cs`
- Follow-up result: S1.3 critical walk-leg rerun reported `Passed (1/1),
  Duration: 15s`, then `dotnet test` aborted at the 600s test-session timeout.
  `.\run-tests.ps1 -ListRepoScopedProcesses` reported no repo-scoped processes.
- Next command: isolate PathfindingService testhost/fixture cleanup after the
  functional pass; do not re-open pathfinding or BotRunner movement without an
  actual route assertion failure.

### 2026-05-12 (S1.3 PathfindingService stability baseline)
- Active task: S1.3 no-route-pack stability sweep is red before the catalog
  P99 guard can be trusted.
- Pass result: `blocked; critical walk-leg matrix failed 3/23`.
- Last delta:
  - Re-ran the Crossroads -> Undercity critical walking-leg matrix against
    `D:\MaNGOS\data`.
  - The matrix now reports three Orgrimmar zeppelin tower failures. These are
    substrate failures, not invitation to add managed path repair, route-pack
    seeds, per-spot route gates, or BotRunner boarding constants.
  - Per the PathfindingService freeze, route the next delta through
    MmapGen/physics evidence for the OG zeppelin tower cluster.
- Failing cases:
  - `orgrimmar_city_live_vertical_replan_recovery`: waypoint `63` floats
    `2.9y` from collision support (`supportZ=27.400`) on a
    `repaired_static_los` path.
  - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`: local
    physics movement break at segment `172->173`.
  - `orgrimmar_zeppelin_tower_friction_recovery`: early waypoint projects
    onto upper tower support before the Tauren capsule can reach it.
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=s1_3_critical_walk_legs.trx" --results-directory tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (20 passed, 3 failed)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/s1_3_critical_walk_legs.trx`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

- Last updated: 2026-05-06
- Active task: deterministic Orgrimmar route gates are green; live proof moved
  past PathfindingService and is now blocked in BotRunner zeppelin boarding.
- Last delta:
  - Added/kept the flight-master -> zeppelin approach regression coverage so
    the deterministic route gate checks the current live blockers before a WoW
    client launches.
  - Verified smooth-fallback validation, local-physics rise gating, restored
    affordance/static scan behavior, route-pack suffix safety, and static
    route-pack cache coverage against `D:\MaNGOS\data`.
  - Re-ran the critical Crossroads -> Undercity walking-leg data row; every
    row passed, though VSTest hung during shutdown and returned exit code `1`.
  - Follow-up BotRunner boarding-target refresh coverage passed, but live
    validation still failed on zeppelin client attachment after reaching the
    dock, so no new PathfindingService route gate is open.
- Pass result: `delta shipped; deterministic route gates green`
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=flightmaster_static_blockers_restore_affordance_scan.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=420000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinApproachRoute_AvoidsKnownLiveBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=flightmaster_zeppelin_approach_restore_affordance_scan.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable|FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_routepack_lower_friction_regressions.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (16/16)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_after_affordance_restore.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> row results `passed (13/13)`, but VSTest exited `1` after the session shutdown timeout.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/flightmaster_static_blockers_restore_affordance_scan.trx`
  - `tmp/test-runtime/results-pathfinding/flightmaster_zeppelin_approach_restore_affordance_scan.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_routepack_lower_friction_regressions.trx`
  - `tmp/test-runtime/results-pathfinding/critical_walk_legs_after_affordance_restore.trx`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-05
- Active task: deterministic `PFS-ROUTEPACK-002` coverage shipped; live proof
  remains tracked in the master/PathfindingService task files.
- Last delta:
  - Re-ran
    `LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable`;
    it still fails on local-physics segment `1->2` from
    `(1339.2,-4645.6,52.0)` to `(1337.6,-4644.5,53.8)` with result
    `native_path_alternate_mode` and blocked reason `static_los`.
  - Probed alternate start-layer and sampled micro-route approaches, then
    removed them because they did not produce a clean segment sequence. Keep
    the next test delta generic and prove it with this direct gate plus the
    route-pack suffix safety pair.
  - Re-tested a bounded micro-route search for the compact deck step. It could
    repair the first jump in one diagnostic path but then exposed the next deck
    pocket and added too much latency, so that speculative code was removed.
  - Added `OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath`
    to prove the lower-incline seed warms on demand against the current
    gangplank target and returns a route-pack suffix from the live start.
  - Updated the static seed contract test to assert gangplank end anchors,
    startup-deferred/on-demand behavior, and no dynamic-overlay reuse for the
    Orgrimmar route-pack seeds.
  - Kept diagnostic failure output for route-pack generation paths so future
    blocked results print the generated path and blocked segment.
- Pass result: `delta shipped; deterministic lower-incline route-pack coverage green`
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `passed`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath" --logger "console;verbosity=minimal" --logger "trx;LogFileName=lower_incline_routepack_on_demand_gangplank_final_focus.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=180000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarStaticRoutePackSeeds_TargetGangplankAndDeferStartupWarmup|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_routepack_contract_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (3/3)`.
  - Safety probe:
    `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_suffix_safety_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed 1/2`; suffix safety passed, direct deck friction route still has a local-physics break at segment `1->2`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_deck_friction_current_open.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed`; current direct upper-deck local-physics gate remains open.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_endpoint_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath" --logger "console;verbosity=minimal" --logger "trx;LogFileName=lower_incline_routepack_endpoint_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=180000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_routepack_suffix_endpoint_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_deck_friction_current_open_after_micro_revert.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed`; final retained code still leaves the direct upper-deck gate open at segment `1->2`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/lower_incline_routepack_on_demand_gangplank_final_focus.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_contract_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_suffix_safety_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_deck_friction_current_open.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_endpoint_guard.trx`
  - `tmp/test-runtime/results-pathfinding/lower_incline_routepack_endpoint_guard.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_suffix_endpoint_guard.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_deck_friction_current_open_after_micro_revert.trx`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-05
- Active task: configurable mmap preload tests shipped;
  `PFS-ROUTEPACK-002` real-route validation remains open.
- Last delta:
  - Added `PathfindingSocketServerPreloadConfigTests` covering disabled
    preload values, explicit map ID lists, `all` discovery from `.mmap` files,
    and direct `WWOW_NAVIGATION_PRELOAD_MAPS` override precedence.
  - Confirmed the existing live corpse-run socket integration route still
    exceeds its `10s` response budget; this was diagnostic only and is not a
    green route proof.
- Pass result: `delta shipped; preload config tests green`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerPreloadConfigTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_preload_config_tests.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (3/3)`.
  - Diagnostic attempt `PathfindingSocketServerIntegrationTests.HandlePath_LiveCorpseRunRoute_ReturnsValidatedPathWithinBudget` -> failed the existing `10s` response-budget assertion after `44s`; map preload had occurred in native logs.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_preload_config_tests.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_socket_live_corpse_preload_config.trx`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-05
- Active task: mmap v6 migration validation complete for cache/unit gates;
  `PFS-ROUTEPACK-002` real-route validation remains open.
- Last delta:
  - Re-ran `StaticRoutePackCacheTests` after strict native mmap loading and
    focused map `1` tile regeneration; the deterministic route-pack cache
    contract is still green.
  - Tried both a combined Orgrimmar/Crossroads real-route gate and the single
    Orgrimmar static-blocker route gate. Both timed out, so do not treat
    regenerated v6/v7 focused tiles as a green real-route proof yet.
  - No route-specific production blocker coordinates, clearance cylinders,
    detour waypoints, or live-position guards were added.
- Pass result: `delta shipped; route-pack cache unit gate green, real Orgrimmar route gate red`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_static_route_pack_cache_detour_mmap_v6.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor|FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_uc_detour_mmap_v6_route_gates.trx" --results-directory tmp/test-runtime/results-pathfinding` -> aborted at the `10m` runsettings timeout.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_detour_mmap_v6.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> aborted at the `20m` timeout.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_static_route_pack_cache_detour_mmap_v6.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_org_uc_detour_mmap_v6_route_gates.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_org_fm_static_blockers_detour_mmap_v6.trx`
- Blockers:
  - `PFS-ROUTEPACK-002` remains the next deterministic route-pack/recovery
    gate. The current problem is still a real-route generation/runtime
    boundedness gap, not permission to add route-specific production geometry.
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-04
- Active task: Detour baseline cross-check complete; `PFS-ROUTEPACK-002`
  lower-incline route-pack recovery validation remains open.
- Last delta:
  - Re-ran the deterministic cache/route-pack unit slice after adding native
    Detour compatibility probes and managed ABI tests.
  - The slice stayed green, but the broader real-route/cache command timed
    out at the shell `20m` limit before executing tests and wrote a
    zero-counter TRX. Do not treat the Orgrimmar real-route gate as green from
    this pass.
  - The native Detour baseline now records that current generated data works
    with `DT_POLYREF64`/64-bit refs and `DT_NAVMESH_VERSION = 7`; a local
    32-bit-ref trial made the Orgrimmar static route return `no_path`.
- Pass result: `delta shipped; cache/route-pack unit slice green, real-route gate remains open`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_cache_pack_detour_baseline_unit.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~StaticRoutePackCacheTests|FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_detour_baseline_route_cache.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> shell timeout after `20m`; TRX counters stayed `0`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_cache_pack_detour_baseline_unit.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_detour_baseline_route_cache.trx`
- Files changed:
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/DetourCompatibilityTests.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Exports/Navigation/DllMain.cpp`
  - `docs/physics/DETOUR_UPGRADE_BASELINE.md`
- Blockers:
  - `PFS-ROUTEPACK-002` remains the next deterministic route-pack/recovery
    gate. No route-specific production workaround was added.
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

- Previous handoff:
- Last updated: 2026-05-04
- Active task: cache/metrics/socket logging coverage shipped;
  `PFS-ROUTEPACK-002` lower-incline route-pack recovery validation remains
  open.
- Last delta:
  - Added `RouteResultCacheTests` for static-overlay fuzzy hits,
    dynamic-overlay bypass, in-flight coalescing, and short-TTL negative cache
    expiry.
  - Added `ProtobufSocketServerLoggingTests` proving clean post-response EOF
    no longer logs `Unexpected EOF`, while truncated payload EOF remains a
    warning.
  - Added
    `NavigationOverlayAwarePathTests.CalculateValidatedPath_RecordsResolverAndManagedValidationMetrics`
    for resolver and managed-validation metric increments without native
    Navigation calls.
  - Added `StaticRoutePackCacheTests.WarmUpAll_SkipsSeedsNotMarkedForStartup`
    and `WarmUp_ReturnsFalseWhenGenerationExceedsSeedTimeout`, then reworked
    the socket route-cache contract onto a deterministic generated route-pack
    fixture through the normal protobuf server path.
  - The real Navigation-backed route-pack command now fails bounded at about
    `30s` on route-pack seed warmup instead of timing out after `20m`. Keep
    `PFS-ROUTEPACK-002` red.
- Pass result: `delta shipped; cache/metrics/socket logging bundle green; Navigation-backed route-pack proof fails bounded at seed warmup`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ProtobufSocketServerLoggingTests|FullyQualifiedName~StaticRoutePackCacheTests|FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~NavigationOverlayAwarePathTests.CalculateValidatedPath_RecordsResolverAndManagedValidationMetrics|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_RepeatedStaticRequest_UsesServiceRouteCacheThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_cache_socket_logging_metrics_timeout_bundle_after_assertion.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (18/18)` with the existing benign `dumpbin` applocal warning.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_real_after_warmup_timeout_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> failed bounded at `30s` on route-pack seed warmup.
  - `git diff --check` -> no whitespace errors; line-ending warnings only.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_cache_socket_logging_metrics_timeout_bundle_after_assertion.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_metrics_cache_socket_eof.trx`
  - `tmp/test-runtime/results-pathfinding/routepack_real_after_warmup_timeout_guard.trx`
- Files changed:
  - `Exports/BotCommLayer/ProtobufSocketServer.cs`
  - `Services/PathfindingService/RouteCaching/RouteResultCache.cs`
  - `Services/PathfindingService/Repository/NavigationPerformanceMetrics.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Tests/PathfindingService.Tests/ProtobufSocketServerLoggingTests.cs`
  - `Tests/PathfindingService.Tests/RouteResultCacheTests.cs`
  - `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- Blockers:
  - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor`
    still times out on real Navigation data; lower-incline recurring-recovery
    route-pack generation remains the next deterministic gate.
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

### Previous Handoff (2026-05-04 earlier route-cache slice)
- Last updated: 2026-05-04
- Active task: `PFS-CACHE-001` deterministic cache coverage shipped; `PFS-ROUTEPACK-002` lower-incline route-pack recovery validation remains open.
- Last delta:
  - Added `RouteResultCacheTests` covering static-overlay fuzzy quantized
    route hits, dynamic-overlay bypass, concurrent in-flight coalescing, and
    short-TTL negative cache expiry.
  - Extended `PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract`
    so a repeated route-pack request asserts `server.RouteCacheStats.HitCount`
    once the real Navigation-backed route-pack proof completes again.
  - The combined socket proof command built successfully and the deterministic
    cache tests passed, but the run still aborted at the 20-minute test session
    timeout before the Navigation-backed Orgrimmar route-pack socket proof
    completed. Keep the route-pack/lower-incline gate red.
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
- Pass result: `delta shipped; route result cache tests green; Navigation-backed route-pack socket proof still times out`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=route_result_cache_tests.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (4/4)` with the existing benign `dumpbin` applocal warning.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=route_result_cache_socket_contract.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> built successfully and `RouteResultCacheTests` passed `(4/4)`, then aborted at the 20-minute session timeout before the Navigation-backed socket route-pack proof completed.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=route_result_cache_tests_after_socket_timeout.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (4/4)` with the existing benign `dumpbin` applocal warning.
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
  - `Tests/PathfindingService.Tests/RouteResultCacheTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Services/PathfindingService/RouteCaching/RouteResultCache.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
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
