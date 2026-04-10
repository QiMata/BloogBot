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

### BR-NAV-005 Preserve walkable-triangle corridor during bot-side smoothing
- [x] Progress (2026-03-24 session 141): `NavigationPath` now gates bot-side smoothing through corridor-preservation checks. String-pull shortcuts, runtime LOS skip-ahead, corner offsets, and cliff-reroute offsets all require multi-sample navmesh proximity plus lateral support before they can bypass the raw path.
- [x] Progress (2026-03-24 session 141): deterministic regressions now pin clear-LOS-but-off-corridor shortcuts, rejected corner offsets, rejected cliff reroutes, and the gathering-route slice still passes.
- [x] Progress (2026-03-24 session 164): `NavigationPath.CurrentWaypoints` now exports only the remaining active corridor, so `MovementController` cannot be reset onto stale already-cleared corners after BotRunner has advanced past them.
- [ ] Run the reproduced mining route again and compare planned waypoints against execution to confirm the remaining drift, if any, is now in BotRunner waypoint promotion/resequence policy rather than movement-controller routing.
- [ ] If execution still curves off-corridor, clamp BotRunner waypoint-promotion and candidate-selection policy to the same corridor-preserving rule before revisiting native/service smoothing.

### BR-NAV-006 Prove path ownership through combat and movement-controller handoff
Known remaining work in this owner: `0` items.
- [x] BG corpse-run live recording now persists the active `RetrieveCorpseTask` corridor snapshot to `navtrace_<account>.json`, and `DeathCorpseRunTests` asserts that the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
- [x] Session 188 redirect parity test proved FG/BG matched pause/resume packet timing with `Parity_Durotar_RoadPath_Redirect`. BG `SET_FACING` fix shipped so both clients emit `MSG_MOVE_SET_FACING` on mid-route direction changes.
- [x] Final live proof bundle (session 188): forced-turn Durotar, redirect, combat auto-attack, and corpse-run reclaim all pass on the same DLL baseline.

### BR-FISH-001 Keep Ratchet fishing search-walk failures bounded and attributable
- [x] `FishingTask` now caps each probe waypoint at `20s`, so one unreachable search leg no longer burns the full `180s` `search_walk` budget before the task can try the next probe.
- [x] `FishingTask` search-walk travel targeting now falls back through shorter local steps (`8y -> 4y -> 2y`) and only keeps a step when the pathfinder can actually route to it from the current pier position.
- [x] Ownership alignment (2026-04-09 session 309): removed task-level `MovementStuckRecoveryGeneration` consumption from `FishingTask` search-walk so stuck detection/recovery ownership stays in `IObjectManager` movement implementations.
- [x] `FishingTask` now rejects non-progressing shoreline approach targets after `12s` and reacquires the same pool with a different candidate instead of looping on one dock-lip approach until the overall fishing timeout.
- [ ] Keep staged Ratchet visibility explicit so fishing runtime failures only count once a local pool is actually active/visible from the staged dock position.
- [ ] If the dual slice falls back into runtime search again, keep tightening remaining short local pier legs (timeout/path quality only, no task-level unstuck budget) until parity stays green on staged-search reruns too.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-04-09 (BR-NAV-005 universal stuck-ownership alignment)
- Active task: `BR-NAV-005`
- Last delta:
  - Session 309 aligned stuck ownership to movement-layer only:
    - Removed task-level stuck-budget logic from `GatheringRouteTask`.
    - Removed `FishingTask` search-walk `MovementStuckRecoveryGeneration` skip path.
    - Removed active `RetrieveCorpseTask` task-owned stall recovery maneuvers (jump/strafe/turn recovery paths); corpse run now remains path/no-path timeout driven while movement-layer recovery stays centralized in `IObjectManager` implementations.
  - Removed implicit global stuck-generation wiring from shared path factory defaults:
    - `NavigationPathFactory` no longer auto-binds `MovementStuckRecoveryGeneration`.
    - BotRunner callers now build `NavigationPath` without passing movement-stuck generation providers.
  - Updated deterministic task tests to lock the new ownership contract (`no synthetic task recovery`).
  - Session 308 tightened the MovementController boundary again for parity:
    - Stale-forward handling no longer mutates movement flags/steering target or arms forced strafe.
    - Caller-facing stuck escalation signals remain, so BotRunner keeps all route-recovery policy ownership.
  - Revalidated BotRunner deterministic slices after the parity-only stale-signal change:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (74/74)`.
  - Removed route-execution surface from shared movement APIs:
    - `IObjectManager.SetNavigationPath(...)` removed.
    - BotRunner deterministic tests no longer assert against the removed API.
  - Confirmed parity-only movement-controller contract in deterministic suites:
    - `MovementController` now exposes only `SetTargetWaypoint(...)` for steering hints.
    - Legacy `SetPath(...)` route-style API was removed from `MovementController`.
  - Locked contract with WoWSharpClient parity pass:
    - `MovementController` now stores only a single steering target and does not perform corridor/waypoint selection.
    - `ObserveStaleForwardAndRecover(...)` L2 remains callback-only so BotRunner keeps route policy ownership.
  - Updated deterministic WoWSharpClient tests to reflect the new ownership boundary.
  - Revalidated BotRunner deterministic slices after the contract change:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"` -> `passed (72/72)`.
  - Aligned with parity guidance: `MovementController` no longer performs stuck-time waypoint reselection/escape-index routing; it now signals recovery generation and leaves corridor ownership to BotRunner.
  - Removed BotRunner pushes of full route execution into `MovementController`:
    - `BotTask.TryNavigateToward(...)` now issues only `MoveToward(waypoint)` and does not call `SetNavigationPath(...)`.
    - `FishingTask.TryFollowSearchWaypointPath(...)` now issues only `MoveToward(nextWaypoint)` and does not call `SetNavigationPath(...)`.
  - Updated deterministic coverage to match the ownership model (no movement-controller aggressive fallback selection; no fishing search-walk expectation for `SetNavigationPath`).
  - Replayed the live Valley mining route multiple times with repo-local runtime/temp dirs; failure persists with repeated stuck-recovery oscillation and `candidate_timeout` advancement.
- Pass result: `BR-NAV-005 still open; task-level unstuck ownership is now removed and deterministic slices pass, but live Valley mining still fails with repeated candidate timeouts`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_universal_stuck_ownership.trx"` -> `failed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~MovementControllerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (164/164)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (73/73)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (64/64)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (65/65)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_local_delta_cap.trx"` -> `failed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_mc_route_ownership_shift.trx"` -> `failed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_botrunner_route_ownership.trx"` -> `failed (1/1)`
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/TargetPositioningService.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/GameData.Core/Interfaces/IObjectManager.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerIntegrationTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerIpcParityTests.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "candidate_timeout|STUCK-L2|MoveToward preserving airborne steering only" "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\mining_bg_gather_route_post_universal_stuck_ownership.trx"`
- Current blockers:
  - Live route remains trapped in repeated local stuck-recovery loops (same coordinate clusters), with `NavigationPath` promotion oscillation still visible in the latest artifact.
