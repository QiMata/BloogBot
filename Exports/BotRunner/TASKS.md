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

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-03-24
- Active task: `BR-NAV-005`
- Last delta:
  - Closed the next execution-side gap after the smoothing guardrails: `NavigationPath.CurrentWaypoints` now returns only the remaining active corridor instead of the full historical path.
  - That keeps `ObjectManager.SetNavigationPath(...)` from handing `MovementController` stale already-cleared corners, which could otherwise reintroduce sideways pull after BotRunner had already advanced the path index.
  - Added `CurrentWaypoints_ReturnsRemainingCorridorAfterWaypointAdvance` in `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs` and kept the deterministic gathering-route slice green.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"` -> `passed (58/58)`
  - `Get-Process PathfindingService,WoWStateManager,BackgroundBotRunner,WoW -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path` -> `no matching repo-scoped runtime processes`
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
- Blockers:
  - The next proof point is still the reproduced mining route; deterministic coverage is green, but live movement execution has not been re-run in this pass.
