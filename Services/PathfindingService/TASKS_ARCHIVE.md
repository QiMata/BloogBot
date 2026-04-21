# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-14) - `PFS-LIVE-001` full split-service live matrix capture

- [x] Captured the first uninterrupted `LiveValidation` pass/fail matrix against the current split-service Linux stack.
- [x] Completion notes:
  - `tmp/test-runtime/results-live/livevalidation_full_matrix_post_gathering_route_hardening.trx` now records the baseline matrix for this topology: `105 total / 103 executed / 89 passed / 14 failed / 2 not executed`.
  - That closes the remaining "capture the matrix" blocker for this owner. Later focused slices reduced false negatives in adjacent owners (`fg_mail_open_mailbox_post_inbox_wait_fix.trx`, `bg_only_orgrimmar_ah_bank_after_flag_fix.trx`), so the remaining red tests are now genuine BotRunner/Foreground/navigation follow-ups instead of a missing service-level evidence gap.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_full_matrix_post_gathering_route_hardening.trx"` -> `captured uninterrupted matrix`
  - `rg -n "ResultSummary|Counters" "tmp/test-runtime/results-live/livevalidation_full_matrix_post_gathering_route_hardening.trx"` -> `confirmed total=105 executed=103 passed=89 failed=14 notExecuted=2`

## Archived Snapshot (2026-04-14) - `PFS-OBJ-001` caller adoption closeout

- [x] Closed the remaining object-aware routing contract follow-through in the first higher-level BotRunner caller.
- [x] Completion notes:
  - `NavigationPath` now requests `PathfindingClient.GetPathResult(...)` instead of the corners-only `GetPath(...)` seam, so the service-side blocked-reason contract is visible to the movement planner without another transport change.
  - Service `blocked_by_dynamic_overlay` / `dynamic_overlay` responses now drive the existing dynamic-blocker replan path even when the service returns zero corners.
  - Deterministic tests now prove nearby-object route requests still flow through the richer route-result seam and that a service-originated overlay rejection records `dynamic_blocker_observed` as the replan reason.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (70/70)`

## Archived Snapshot (2026-04-13) - `PFS-FISH-001` Ratchet shoreline attribution closeout

- [x] Closed the remaining Ratchet shoreline attribution follow-up without another `PathfindingService` code delta.
- [x] Completion notes:
  - Earlier work already kept the staged pool-visibility preflight explicit (`no child`, `spawned but invisible`, `direct-probe only`).
  - The last open shoreline/runtime question is no longer red on the current binaries: the dual live packet-sequence compare now passes after the BotRunner search-walk reference-layer fix and the WoWSharpClient fishing cast-packet fix.
  - Practical implication: `PathfindingService` no longer owns an open Ratchet shoreline blocker; the next active work in this owner is the object-aware routing contract.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - `PFS-NAV-002` corridor-preserving waypoint promotion

- [x] Kept bot-side smoothing/follower precision constrained to the walkable corridor even after the raw path has already been planned.
- [x] Captured the reproduced managed-follower drift in BotRunner and fixed it in `NavigationPath` rather than in `MovementController` or native path generation:
  - Adaptive-radius waypoint promotion now requires the live-position shortcut to preserve the sampled walkable corridor.
  - Short probe-waypoint skipping now uses the same corridor-preserving shortcut rule.
  - Overshoot look-ahead skipping now rejects later waypoints when the direct live-position shortcut leaves the corridor.
- [x] Added deterministic regressions:
  - `NavigationPathTests.GetNextWaypoint_DoesNotAdvanceEarly_WhenAdaptiveRadiusShortcutLeavesWalkableCorridor`
  - `NavigationPathTests.GetNextWaypoint_DoesNotLookAheadSkip_WhenOvershootShortcutLeavesWalkableCorridor`
- Completion notes:
  - Removed the unrelated short-LOS direct-path priming experiment from `NavigationPath`; it changed direct-fallback, gap-detection, and short-route replanning semantics without closing any tracked item and made the stable deterministic slice red.
  - The remaining live proof moved back to BotRunner ownership under `Exports/BotRunner/TASKS.md` `BR-NAV-005`: rerun the reproduced mining route and compare planned versus executed drift after the new clamp.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (66/66)`

## Archived Snapshot (2026-04-03) - `PFS-DOCKER-001` deployment complete

- [x] Split Pathfinding/SceneData service topology and package both services for Docker.
- [x] Deploy the split services on the Linux compose stack with mounted `WWOW_DATA_DIR` volumes.
- [x] Capture runtime evidence that both services are reachable and preloading/serving from `/wwow-data`.
- Completion notes:
  - `pathfinding-service` and `scene-data-service` are now running as separate Linux containers from `docker-compose.vmangos-linux.yml`.
  - Both services publish host ports (`5001`, `5003`) and mount `${WWOW_VMANGOS_DATA_DIR}` read-only at `/wwow-data`.
  - Runtime logs confirm active map preload on Pathfinding and ready scene-slice service startup on SceneData.
