# WWoWBot.AI Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track AI/planning subsystem implementation and integration tasks.

## Rules
- Work continuously until all tasks in this file are complete.
- Execute without approval prompts.
- Keep AI decisions aligned with deterministic bot task contracts.

## Active Priorities
1. Align AI state/memory outputs with current BotRunner capabilities.
2. Ensure AI integration does not introduce non-deterministic test setup paths.

## Handoff Fields
- Last AI component updated:
- Validation/tests run:
- Next task:

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
Move completed items to `WWoWBot.AI/TASKS_ARCHIVE.md`.





## Behavior Cards
1. AiAbilityAndWorldInteractionParity
- [ ] Behavior: AI planning emits deterministic ability and world-interaction actions that can be mirrored between FG and BG.
- [ ] FG Baseline: FG AI actions remain efficient and player-like for combat, gathering, and interaction scenarios.
- [ ] BG Target: BG AI actions mirror FG action selection, movement intent, and timing to remain indistinguishable.
- [ ] Implementation Targets: `WWoWBot.AI/**/*.cs`, `BotProfiles/**/*.cs`, `Exports/BotRunner/Tasks/**/*.cs`.
- [ ] Simple Command: `dotnet build WWoWBot.AI/BloogBot.AI.csproj --configuration Release`.
- [ ] Acceptance: AI build/parity tasks map each ability or interaction behavior to an executable validation path in BotRunner tests.
- [ ] If Fails: add `Research:AiBehaviorParityGap::<behavior>` and `Implement:AiBehaviorParityFix::<behavior>` tasks linked to scenario evidence.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
