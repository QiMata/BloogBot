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
Known remaining work in this owner: `0` items.
- [x] BG corpse-run live recording now persists the active `RetrieveCorpseTask` corridor snapshot to `navtrace_<account>.json`, and `DeathCorpseRunTests` asserts that the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
- [x] Session 188 redirect parity test proved FG/BG matched pause/resume packet timing with `Parity_Durotar_RoadPath_Redirect`. BG `SET_FACING` fix shipped so both clients emit `MSG_MOVE_SET_FACING` on mid-route direction changes.
- [x] Final live proof bundle (session 188): forced-turn Durotar, redirect, combat auto-attack, and corpse-run reclaim all pass on the same DLL baseline.

### BR-FISH-001 Keep Ratchet fishing search-walk failures bounded and attributable
- [x] `FishingTask` now caps each probe waypoint at `20s`, so one unreachable search leg no longer burns the full `180s` `search_walk` budget before the task can try the next probe.
- [x] `FishingTask` search-walk travel targeting now falls back through shorter local steps (`8y -> 4y -> 2y`) and only keeps a step when the pathfinder can actually route to it from the current pier position.
- [x] `FishingTask` now consumes `MovementStuckRecoveryGeneration` during `search_walk` probe windows and abandons a blocked local pier leg after about `1.5s` with `reason=movement_stuck` instead of regrinding the same corner for the full `20s` stall timeout.
- [x] `FishingTask` now rejects non-progressing shoreline approach targets after `12s` and reacquires the same pool with a different candidate instead of looping on one dock-lip approach until the overall fishing timeout.
- [ ] Keep staged Ratchet visibility explicit so fishing runtime failures only count once a local pool is actually active/visible from the staged dock position.
- [ ] If the dual slice falls back into runtime search again, keep tightening any remaining short local pier legs beyond the new stuck-recovery skip until parity stays green on staged-search reruns too.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-04-02 (search-walk now obeys movement-controller stuck recovery; focused dual path green)
- Active task: `BR-FISH-001`
- Last delta:
  - `FishingTask.SearchForPool(...)` now snapshots the active `MovementStuckRecoveryGeneration` when each probe window opens and treats a newer generation after a short `1.5s` grace as authoritative blocked-probe evidence. The task now emits `search_walk_stalled ... reason=movement_stuck` and advances instead of burning the full `20s` stall timer on the same pier corner.
  - Added deterministic coverage in `AtomicBotTaskTests` for the new behavior.
  - The focused dual live Ratchet path test is green on the current BotRunner binaries: FG completed `fishing_loot_success` with loot item `6303`, and BG completed `fishing_loot_success` with loot item `6358`.
  - Important scope note: the latest green dual rerun did not re-enter `search_walk`, so the blocked-corner fix is currently backed by deterministic coverage plus the earlier live stuck-recovery diagnostics rather than by a fresh staged-search live rerun.
- Pass result: `delta shipped; focused dual Ratchet fishing path green`
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (30/30)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath" --logger "console;verbosity=normal"` -> `passed`
- Files changed:
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/FishingProfessionTests.md`
  - `docs/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=normal"`
- Current blockers:
  - Focused dual path is green on the current binaries, but staged dock visibility/streaming attribution is still nondeterministic across reruns and the actual FG/BG packet-sequence comparison work is still open.
- Blockers (historical note below):
  - The current red fishing slice is no longer blocked on “how to respawn Ratchet pools.” It is now blocked by staged dock visibility/streaming plus the remaining last-two-leg local pier stalls after a local child pool has already been activated.
