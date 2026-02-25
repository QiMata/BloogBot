# Services Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track orchestration and runner service work required for full bot implementation.

## Rules
- Execute continuously without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep service behavior deterministic and observable.
- Record every service-side parity blocker in the appropriate subproject task file.

## Subproject Task Files
- `Services/WoWStateManager/TASKS.md`
- `Services/ForegroundBotRunner/TASKS.md`
- `Services/BackgroundBotRunner/TASKS.md`
- `Services/PathfindingService/TASKS.md`
- `Services/DecisionEngineService/TASKS.md`
- `Services/PromptHandlingService/TASKS.md`
- `Services/CppCodeIntelligenceMCP/TASKS.md`
- `Services/LoggingMCPServer/TASKS.md`

## Active Priorities
1. StateManager action forwarding and snapshot consistency.
2. FG/BG runner lifecycle and behavior parity.
3. Pathfinding and movement service reliability under live tests.

## Handoff Fields
- Last changed subproject:
- Service logs/evidence:
- Next subproject task:

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
Move completed items to `Services/TASKS_ARCHIVE.md`.




## Behavior Cards
1. ServiceOrchestrationParityLoop
- [ ] Behavior: service orchestration keeps FG/BG lifecycle, action routing, and scenario coordination aligned through corpse/combat/gathering cycles.
- [ ] FG Baseline: FG service pipeline completes the full validation cycle without dropped actions, stale state, or unmanaged lingering processes.
- [ ] BG Target: BG service pipeline mirrors FG orchestration timing and action completion semantics in the same run cycle.
- [ ] Implementation Targets: `Services/WoWStateManager/TASKS.md`, `Services/ForegroundBotRunner/TASKS.md`, `Services/BackgroundBotRunner/TASKS.md`, `Services/PathfindingService/TASKS.md`, `Services/DecisionEngineService/TASKS.md`, `Services/PromptHandlingService/TASKS.md`, `Services/CppCodeIntelligenceMCP/TASKS.md`, `Services/LoggingMCPServer/TASKS.md`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: services support all three scenario families in one cycle with deterministic teardown evidence and no cross-service starvation.
- [ ] If Fails: add `Research:ServiceOrchestrationGap::<service>` and `Implement:ServiceOrchestrationFix::<service>` tasks and link to owning subproject TASKS file.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
