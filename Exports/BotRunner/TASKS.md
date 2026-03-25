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

### BR-NAV-001 Build a collidable game-object snapshot for path requests
- [x] `NavigationPath` now forwards conservative nearby collidable object overlays from live BotRunner call sites.
- [ ] Keep the filter conservative first: only pass objects known to have collision or gameplay relevance.

### BR-NAV-002 Extend path requests to send object context and movement capabilities
- [x] Nearby-object overloads are live for BotRunner callers.
- [ ] Thread character movement capabilities and route-policy settings down from BotRunner.

### BR-NAV-003 Replan and re-optimize when dynamic blockers invalidate the current route
- [x] `NavigationPath.TraceSnapshot` records requested start/end, raw service waypoints, runtime waypoints, plan version, explicit replan reason, and bounded per-tick execution samples.
- [x] `RetrieveCorpseTask` and live diagnostics consume the trace formatter.
- [ ] Continue using dynamic blocker evidence to trigger planned replans instead of long stall loops.

### BR-NAV-004 Consume affordance metadata in movement and decision logic
- [ ] Teach movement/path consumers to reject unsupported routes and prefer cheaper valid routes.
- [ ] Surface route affordances to higher-level tasks before extending the contract into combat/interaction code.

### BR-NAV-005 Preserve walkable-triangle corridor during bot-side smoothing
- [x] Progress (2026-03-24 session 141): `NavigationPath` now gates bot-side smoothing through corridor-preservation checks. String-pull shortcuts, runtime LOS skip-ahead, corner offsets, and cliff-reroute offsets all require multi-sample navmesh proximity plus lateral support before they can bypass the raw path.
- [x] Progress (2026-03-24 session 141): deterministic regressions now pin clear-LOS-but-off-corridor shortcuts, rejected corner offsets, rejected cliff reroutes, and the gathering-route slice still passes.
- [x] Progress (2026-03-24 session 164): `NavigationPath.CurrentWaypoints` now exports only the remaining active corridor, so `MovementController` cannot be reset onto stale already-cleared corners after BotRunner has advanced past them.
- [ ] Run the reproduced mining route again and compare planned waypoints against execution to confirm the remaining drift, if any, is now in `MovementController`/`WoWSharpObjectManager` rather than `NavigationPath`.
- [ ] If execution still curves off-corridor, clamp the movement-controller side to the same corridor-preserving rule before revisiting native/service smoothing.

### BR-NAV-006 Prove path ownership through combat and movement-controller handoff
Known remaining work in this owner: `4` items.
- [x] BG corpse-run live recording now persists the active `RetrieveCorpseTask` corridor snapshot to `navtrace_<account>.json`, and `DeathCorpseRunTests` asserts that the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
- [ ] Capture the matching FG/BG frame-level evidence for candidate `3/15` or the same corpse/combat route segment, combining `NavigationPath.CurrentWaypoints`, `BotTask` pause/resume state, and the executed `MovementController` / packet stream on the same interval.
- [ ] If live drift or stalls remain, isolate whether ownership breaks in `BotTask`, `WoWSharpObjectManager`, or `MovementController` before touching `PathfindingService` or route smoothing again.
- [ ] Keep combat pause/chase recovery from discarding the active corridor unless collision/combat evidence explicitly invalidates it.
- [ ] Re-run the mining and corpse/combat repro routes after each ownership fix so BotRunner only hands verified remaining-corridor state into the controller.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-03-25 (session 187)
- Active task: `BR-NAV-006`
- Last delta:
  - Session 187 closed the forced-turn Durotar stop tail that BotRunner and BG had been using as the active managed blocker. `BuildGoToSequence(...)` now treats arrival as horizontal distance only, and `NavigationPath` no longer re-opens an exhausted route because of Z-only destination drift.
  - `MovementParityTests` now proves the same forced-turn route on both edges instead of only the start edge: no late outbound `SET_FACING` is allowed after the opening pair, both clients must emit outbound `MSG_MOVE_STOP`, the final outbound movement packet must be `MSG_MOVE_STOP`, and the FG/BG stop-edge delta must stay within `300ms` (latest run: `50ms`).
  - The remaining BotRunner ownership/controller gap is now the pause/resume and corridor-handoff slice on the same route family, plus the candidate `3/15` mining proof loop. The old stop-tail mismatch is no longer an open reason to touch movement arrival/stop logic.
  - Session 186 tightened the live movement proof instead of letting route passes count as parity by accident. `MovementParityTests` now requires meaningful travel from both FG and BG, and it has a forced-turn Durotar route that uses the stable FG/BG `packets_<account>.csv` sidecars on the same interval.
  - That forced-turn route closes the old “do we have matched facing-correction evidence?” question for BotRunner-owned live proof: both clients now show `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD` on the start edge.
  - That session narrowed the remaining BotRunner ownership/controller gap to the route tail plus pause/resume ownership work. Session 187 subsequently closed the route tail, leaving pause/resume and corridor handoff as the current live gap.
  - Session 184 finished wiring the live corridor-ownership trace path. `BotRunnerService.Diagnostics` now records `navtrace_<account>.json` alongside the stable transform/physics artifacts, and `DeathCorpseRunTests` now starts/stops recording around `RetrieveCorpseTask` and asserts that the emitted sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
  - Added deterministic coverage for the stable-vs-legacy recording lookup/cleanup helper, and updated `MovementParityTests` to consume the stable `physics_<account>.csv` / `transform_<account>.csv` filenames so live diagnostics no longer depend on timestamped file copies.
  - Revalidated the compact packet-backed Undercity replay slice and the BG corpse-run live slice after the recorder changes. The remaining BotRunner ownership/controller backlog is now the missing paired FG/BG heartbeat/facing evidence on the same route segments, not whether BG can emit corridor ownership state at all.
- Pass result: `stop-edge parity shipped; 4 BotRunner-owned items remain`
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (61/61)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`; stop-edge delta `50ms`, no late outbound `SET_FACING`, final outbound packet `MSG_MOVE_STOP` for both clients
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)` twice; start edge proven, stop tail still divergent
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `dotnet run --project tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release -- compact` -> `26 logical recordings, 411.67 MiB canonical corpus, 0 sidecars refreshed, duplicate Bot/*/Recordings copies missing/clean`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/BotRunnerService.Diagnostics.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelper.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelperTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `docs/TASKS.md`
- Next command: `Get-Content Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs | Select-Object -Skip 320 -First 220`
- Blockers:
  - The old mining and corpse-run harness blockers are closed, and the forced-turn stop edge is now closed too. The remaining live parity issue is paired FG/BG controller trace evidence for pause/resume behavior and corridor ownership on the same route segment.
