# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - NAV-OBJ-004 local dynamic-object detour closeout

- [x] `NAV-OBJ-004` Local detour generation around collidable objects:
  - Closed by evidence. Native `PathFinder.cpp` already builds grounded lateral detour candidates around blocked segments, rejects candidates through `ValidateWalkableSegment(...)`, and falls back only after the detour envelope is exhausted.
  - `FindPath_WithActiveDynamicOverlay_ReformsRouteAroundBlockingObject` proves a request-scoped dynamic blocker produces a multi-point route whose segments no longer intersect the registered object.
  - `FindPathCorridor_WithActiveDynamicOverlay_ReturnsRepairedBlockIdentity` proves the corridor API returns that repaired route with overlay blocker identity and an overlay-repaired flag.
  - `NavigationOverlayAwarePathTests.CalculateValidatedPath_RepairsBlockedPath_WithDetourCandidate` proves the service-side bounded repair path composes a local detour and rejects still-blocked repaired legs.
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests|FullyQualifiedName~FindPath_ObstructedDirectSegment_ReformsIntoWalkableDetour" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`

## Archived Snapshot (2026-04-15) - NAV-OBJ-003 surface-transition affordance classification

- [x] `NAV-OBJ-003` Surface-transition affordance classification:
  - Added native `ClassifyPathSegmentAffordance(...)`, returning walk, step-up, steep-climb, jump-gap, safe-drop, unsafe-drop, vertical, or blocked with validation code, resolved end Z, max climb height, gap distance, drop height, and slope angle.
  - Extended `CalculatePathResponse` and generated C# contracts with jump-gap / safe-drop / unsafe-drop / blocked counts plus max climb/gap/drop metrics.
  - Added `PathAffordanceClassifier` and `PathfindingClient` propagation so higher layers receive the expanded route metadata; default response aggregation uses the fast classifier, and explicit native aggregation is gated by `WWOW_ENABLE_NATIVE_AFFORDANCE_SUMMARY=1` because full native classification can exceed normal path response budgets on long live routes.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentAffordanceClassificationTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`

## Archived Snapshot (2026-04-15) - NAV-OBJ-001 native dynamic-overlay identity closeout

- [x] `NAV-OBJ-001` Integrate request-scoped dynamic objects into native path validation:
  - `PathFinder` now records the first request-scoped dynamic-overlay blocker identity while building overlay-aware routes.
  - `Navigation::CalculatePath(...)` exposes that first blocker to `FindPathCorridor(...)`.
  - `FindPathCorridor(...)` now returns the blocking segment index, dynamic object instance ID, GUID, display ID, and an overlay-repaired flag in its packed native result.
  - `PathfindingService.Repository.Navigation` consumes the native metadata as a repaired route result, so higher layers receive `blockedReason` identity without having to rediscover the same native blocker through a managed segment probe.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`

## Archived Snapshot (2026-04-15) - NAV-OBJ-002 capsule/support walkability closeout

- [x] `NAV-OBJ-002` Add capsule-clearance and support-surface validation for candidate segments:
  - Closed by evidence. The existing `ValidateWalkableSegment(...)` export already uses capsule clearance, support-surface probing, step-up / step-down limits, and explicit validation codes (`Clear`, `BlockedGeometry`, `MissingSupport`, `StepUpTooHigh`, `StepDownTooFar`).
  - `PathfindingService.Repository.Navigation` maps those native validation codes into caller-visible blocked reasons.
  - The focused `SegmentWalkabilityTests` sweep proves visible-but-not-walkable cases are distinguished from walkable segments through the production DLL.
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`

## Archived Snapshot (2026-04-13) - NAV-FISH-001 Ratchet shoreline closeout

- [x] `NAV-FISH-001` Fix Ratchet shoreline terrain sticking / no-LOS approach points:
  - Closed the remaining Ratchet live blocker by evidence instead of another native delta.
  - Earlier native/path-owner work already covered grounded detour generation and honest staged-pool classification.
  - The latest dual live compare now passes on the current native build after the managed fixes landed in `FishingTask` and `SpellcastingManager`, proving there is no longer an open native shoreline/collide-slide blocker in this owner.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - NAV-SCENE-001 thin-scene environment closeout

- [x] `NAV-SCENE-001` Preserve thin scene-slice metadata so physics can emit indoors-aware environment flags:
  - Closed the remaining live parity gap after confirming the Docker/service path, not the triangle metadata payload, was the blocker.
  - `SceneQuery::GetAreaInfo(...)` now falls back to scene-cache metadata when VMAP area info is missing or resolves only zero identifiers, so thin injected slices can still surface usable area/environment facts.
  - `PhysicsEngine` now re-queries grounded support environment flags when the initially selected support contact contributes no usable environment bits.
  - `.env` now points `WWOW_VMANGOS_DATA_DIR` at `D:/MaNGOS/data`, matching the actual Docker parity root used by `scene-data-service`.
  - `SceneTileSocketServer` now indexes tile filenames eagerly and loads/parses tile payloads on demand, which keeps `scene-data-service` startup fast enough to bind `0.0.0.0:5003` before Docker health checks fail.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SceneEnvironmentFlagTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataEnvironmentIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataEnvironmentIntegrationTests|FullyQualifiedName~SceneDataPhysicsPipelineTests|FullyQualifiedName~PhysicsEnvironmentFlags_|FullyQualifiedName~RecordResolvedEnvironmentState_" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.Snapshot_IndoorLocation_ReportsIsIndoors|FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.MountSpell_OutdoorLocation_Mounts|FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.MountSpell_IndoorLocation_DoesNotMount" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mount_environment_nav_scene_closeout_20260413_post_lazy_index.trx"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ItemDataTests|FullyQualifiedName~SpellDataTests|FullyQualifiedName~BotRunnerServiceInventoryResolutionTests|FullyQualifiedName~CastSpellTaskTests|FullyQualifiedName~UseItemTaskTests" --logger "console;verbosity=minimal"` -> `passed (126/126)`
- Evidence:
  - `tmp/test-runtime/results-live/mount_environment_nav_scene_closeout_20260413_post_lazy_index.trx`
- Files changed:
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Services/SceneDataService/SceneTileSocketServer.cs`
  - `.env`
