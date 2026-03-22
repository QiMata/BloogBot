<<<<<<< HEAD
﻿# BotRunner Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Local scope: bot task execution behavior for corpse/combat/gathering scenarios.
- Priority is deterministic behavior with no stall loops and FG/BG parity support.

## Active Blockers
1. Fresh live corpse-run evidence is still needed after Orgrimmar setup switch.
2. Retrieve behavior can still regress into low-progress loops under movement drift.
3. Combat and gathering parity loops need structured evidence + task expansion.

## Active Work
- [x] `RetrieveCorpseTask`: route/probe resolution runs before stall recovery.
- [x] `RetrieveCorpseTask`: stall detection uses horizontal movement intent.
- [x] `RetrieveCorpseTask`: nested recovery is suppressed during unstick maneuvers.
- [x] `RetrieveCorpseTask`: no-path fallback drives toward corpse before timeout abort.
- [ ] Re-run live corpse retrieval using Orgrimmar setup + 10-minute timeout + teardown guard.

## Iterative Parity Backlog
1. Corpse-run
- [ ] Validate BG and FG both complete full corpse lifecycle without teleport-like shortcuts.
- [ ] Ensure retrieve action is delayed until reclaim timer reaches zero.

2. Combat
- [ ] Compare FG/BG combat action sequencing and movement approach behavior.
- [ ] Create paired `research + implementation` tasks for each mismatch.

3. Gathering/mining
- [ ] Compare FG/BG node approach, interaction timing, and interruption recovery.
- [ ] Create paired `research + implementation` tasks for each mismatch.

4. Physics-gated movement parity
- [ ] Run physics calibration checks when BotRunner movement stalls or diverges.
- [ ] Convert calibration findings into `BotRunner` task fixes plus linked `Navigation` tasks.

## Canonical Commands
1. Atomic/unit focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RetrieveCorpseTaskTests|FullyQualifiedName~TeleportTaskTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Live corpse-run focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Combat focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

4. Gathering focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Evidence Logs
- `tmp/deathcorpse_run_current.log`
- `tmp/botrunnertasks_atomic_current.log`

## Session Handoff
- Last updated: 2026-02-24
- Highest-priority unresolved issue: capture fresh live corpse-run evidence under Orgrimmar setup and guarded runtime.

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.

## Archive
Move completed items to `Exports/BotRunner/TASKS_ARCHIVE.md`.
=======
# BotRunner Tasks

## Scope
- Project: `Exports/BotRunner`
- Owns task orchestration for corpse-run, combat, gathering, and questing execution loops.
- This file tracks direct implementation tasks bound to concrete files/tests.
- Master tracker: `MASTER-SUB-004`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep corpse-run flow canonical: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
3. Keep all live validation runs bounded with a 10-minute hang timeout and repo-scoped cleanup evidence.
4. Record `Last delta` and `Next command` in `Session Handoff` each pass.
5. Move completed tasks to `Exports/BotRunner/TASKS_ARCHIVE.md` in the same session.
6. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
7. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.
9. Every implementation slice must add or update focused unit tests and finish with those tests passing before the next slice unless a blocker is recorded.
10. After each shipped delta, update this file and `docs/TASKS.md`, commit, push, and hand off the next open item for the next session.

## Environment Checklist
- [x] `Exports/BotRunner/BotRunner.csproj` builds in `Release`.
- [x] `Tests/BotRunner.Tests` targeted filters run without restore.
- [x] Repo-scoped cleanup command is available.

## Evidence Snapshot (2026-02-25)
- `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore` passes.
- `Exports/BotRunner/Tasks/Questing/QuestingTask.cs` still contains:
  - `// TODO: Implement ScanForQuestUnitsTask`
  - commented push path for `ScanForQuestUnitsTask`.
- `rg -n "class\\s+ScanForQuestUnitsTask|ScanForQuestUnitsTask" Exports/BotRunner` shows no concrete `ScanForQuestUnitsTask` class implementation.
- `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` includes Orgrimmar setup (`TeleportToNamedAsync(..., "Orgrimmar", ...)`).
- Corpse-run live validation reproduces the current blocker:
  - `dotnet test ... --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m ...`
  - failure: `[BG] scenario failed: corpse run stalled with minimal movement (travel=0.0y, moveFlags=0x0)`.
- Repo-scoped cleanup evidence:
  - `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly` exits `0`.
  - `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -ListRepoScopedProcesses` returns `none`.
- Snapshot fallback risk still visible in `Exports/BotRunner/BotRunnerService.Snapshot.cs` via broad `try/catch` guards, including guarded FG `NotImplementedException` handling.

## P0 Active Tasks (Ordered)

### BR-MISS-001 Implement quest unit scanning in questing pipeline
- [x] Done (batch 10). Replaced TODO + commented code with defer rationale in `QuestingTask.cs:51`. Quest-unit scanning requires quest objective->unit mapping and NPC filter design, so the placeholder is now an explicit defer.
- [x] Acceptance: no TODO placeholder remains; defer rationale documents prerequisite design work.

### BR-MISS-002 Keep corpse-run setup fixed to Orgrimmar with reclaim gating
- [x] Code-complete. `DeathCorpseRunTests.cs` already uses `.tele name {NAME} Orgrimmar`. Reclaim gating via `CorpseRecoveryDelaySeconds` already exists in `BotRunnerService.ActionDispatch.cs`.
- [ ] Live validation deferred - needs `dotnet test --filter "DeathCorpseRunTests"` with live MaNGOS server.

### BR-MISS-003 Tighten snapshot fallback behavior around missing FG fields
- [x] Done (batch 11). Replaced bare `catch { }` blocks in `BotRunnerService.Snapshot.cs` with `TryPopulate()` helper that logs the field name + exception type at Debug level. Silent snapshot fallbacks now emit `[Snapshot] {Field} unavailable: {Type}` when Debug logging is enabled.
- [x] Acceptance: snapshot fallback is explicit, traceable, and does not mask missing FG implementation work.

### BR-PAR-004 Harden task-owned fishing around bait usage and real loot completion
- [x] Done (2026-03-12). `FishingTask` now applies bait to the equipped fishing pole before pool approach, confirms lure consumption/enchant state, and keeps the live success contract tied to `loot_window_open` plus a post-loot bag delta instead of setup-only signals.
- [x] Acceptance: dual-bot live fishing can assert `equip -> bait -> approach -> bobber -> loot-window -> bag-delta` directly against `FishingTask`, and unit coverage proves the lure path targets the equipped pole GUID instead of `0x0`.

### BR-NAV-001 Build a collidable game-object snapshot for path requests
- [ ] Problem: BotRunner currently asks for paths with only `mapId/start/end`. It does not package the live game-object list the pathfinding service needs to avoid temporary blockers and route around collision-heavy areas.
- [ ] Target files: `Exports/BotRunner/Clients/PathfindingClient.cs`, `Exports/BotRunner/Movement/NavigationPath.cs`, `Exports/WoWSharpClient/WoWSharpObjectManager*.cs`, FG snapshot equivalents.
- [x] Progress (2026-03-12 session 65): `PathfindingOverlayBuilder` now builds a conservative collidable overlay from `IObjectManager.GameObjects`, filtering to nearby finite collidable objects, capping to the nearest `64`, and mapping them into `DynamicObjectProto` with transform/scale/state. `NavigationPath` now forwards that overlay from all live BotRunner path call sites, and focused unit tests pass.
- [ ] Required change:
  1. Build a filtered list of nearby collidable game objects from the current object manager snapshot.
  2. Map those objects into `DynamicObjectProto` with display ID, transform, scale, and state.
  3. Keep the filter conservative first: only pass objects known to have collision or gameplay relevance.
- [ ] Acceptance criteria: every path request can include a live obstacle list without flooding the service with irrelevant world objects.

### BR-NAV-002 Extend path requests to send object context and movement capabilities
- [ ] Problem: `PathfindingClient.GetPath(...)` cannot send the context needed for object-aware routing or decision-grade route selection.
- [ ] Target files: `Exports/BotRunner/Clients/PathfindingClient.cs`, call sites in `NavigationPath.cs`, `BotRunnerService.Sequences.Movement.cs`, relevant tests.
- [x] Progress (2026-03-12 session 64): first checkpoint landed. `PathfindingClient.GetPath(...)` now has a compatibility overload that can send `nearby_objects`, and deterministic request-shape coverage passes in `PathfindingClientRequestTests`.
- [x] Progress (2026-03-12 session 65): third checkpoint is now in place for live callers. `NavigationPath`, `BotTask`, `RetrieveCorpseTask`, `TargetPositioningService`, and the BotRunner movement/combat sequences now use the overlay-aware overload when nearby collidable objects are present. Remaining BotRunner work in this item is the movement-capability / route-policy fields.
- [ ] Required change:
  1. First checkpoint: extend `GetPath(...)` with a compatibility overload that can send `nearby_objects`, and prove the request shape with passing unit tests.
  2. Second checkpoint: thread character movement capabilities and route-policy settings down from BotRunner.
  3. Third checkpoint: update live call sites to use the new overload without breaking existing behavior during the rollout.
- [ ] Acceptance criteria: BotRunner can ask for "path to X with these live blockers and these allowed movement affordances."

### BR-NAV-003 Replan and re-optimize when dynamic blockers invalidate the current route
- [ ] Problem: once live obstacles move or appear, BotRunner still tends to walk the old path until it stalls.
- [ ] Target files: `Exports/BotRunner/Movement/NavigationPath.cs`, movement task call sites, telemetry/tests.
- [x] Progress (2026-03-12 session 74): `NavigationPath.TraceSnapshot` now records requested start/end, raw service waypoints, runtime waypoints, plan version, explicit replan reason, and bounded per-tick execution samples. Focused `NavigationPathTests` now pin short-route trace capture, stall-driven replans, and direct-fallback attribution.
- [x] Progress (2026-03-12 session 75): `RetrieveCorpseTask` warning paths now consume the trace via `FormatNavigationTraceSummary(...)`, so no-route and stall-recovery logs include the active plan, current index, route snippets, and recent execution samples. `RetrieveCorpseTaskTests` pin the formatter output.
- [x] Progress (2026-03-12 session 76): live corpse/fishing test failures now append snapshot tails plus recent BotRunner diagnostics, and `RetrieveCorpseTask` now mirrors stall/no-path/timeout trace summaries into the BotRunner diag file so those live assertions can pull the same divergence record without reading service logs directly. `LiveBotFixtureDiagnosticsTests` cover the log-tail helper.
- [ ] Required change:
  1. Re-request paths when the current route collides with new object context or repeated wall-hit telemetry.
  2. Prefer repaired/re-optimized paths over direct fallback movement when service data is available.
  3. Finish exercising the new trace/replan evidence in live corpse-run and fishing runs once BG clean-slate revive stops blocking the suites at setup time.
- [ ] Acceptance criteria: dynamic blockers trigger planned replans instead of long stall loops and direct-move thrash.

### BR-NAV-004 Consume affordance metadata in movement and decision logic
- [ ] Problem: even after the service can classify `step_up` / `jump_gap` / `safe_drop`, BotRunner will ignore that metadata unless movement and decision layers consume it.
- [ ] Target files: `NavigationPath.cs`, movement tasks, future decision-system consumers.
- [ ] Required change:
  1. Teach movement/path consumers to reject unsupported routes and prefer cheaper valid routes.
  2. Surface path affordances to higher-level tasks so they can decide whether a goal is reachable, risky, or requires a different approach point.
  3. Keep the first slice small: pathing should consume the metadata before combat/interaction code does.
- [ ] Acceptance criteria: decision logic can reason about route quality, not just route existence.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Quest|FullyQualifiedName~BotRunner" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-03-12
- Active task: `BR-NAV-003` remains open, but the current shipped BotRunner delta is the task-owned gathering route slice used by live mining
- Last delta: added `StartGatheringRoute` wiring and `GatheringRouteTask`, removed the dead inline mining fallback from the live test, and validated that mining now stages `ValleyOfTrials` only inside the test while the route task owns candidate movement, visible-node discovery, and gather interaction
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` -> succeeded
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` -> succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `16 passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 --filter "FullyQualifiedName~Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=detailed"` -> `1 skipped` (`No Copper Vein nodes currently spawned on any of the 6 Valley copper-route candidates.`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldEntryHydrationTests|FullyQualifiedName~PathfindingClientRequestTests|FullyQualifiedName~PathfindingClientTimeoutTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip" --logger "console;verbosity=minimal"` -> `24 passed`
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Exports/GameData.Core/Enums/CharacterAction.cs`
  - `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
  - `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
  - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
  - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelectionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/GatheringProfessionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/OVERHAUL_PLAN.md`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `Get-Content Exports/BotRunner/Tasks/GatheringRouteTask.cs | Select-Object -First 240`
- Blockers: Mining is now task-owned, but the live pass still depends on natural node uptime; when all Valley copper candidates are on respawn the test skips by design. Movement-capability fields for `BR-NAV-002` also remain open, and herbalism still needs to move onto the same route-task contract.
>>>>>>> cpp_physics_system
