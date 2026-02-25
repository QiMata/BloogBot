# Exports Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track shared engine/library parity work across export projects.

## Rules
- Work continuously until all tasks in this file are complete.
- Execute tasks directly; do not pause for approval.
- Keep FG and BG behavior parity as the primary objective.
- Update this file whenever a subproject task changes priority.

## Subproject Task Files
- `Exports/BotRunner/TASKS.md`
- `Exports/BotCommLayer/TASKS.md`
- `Exports/GameData.Core/TASKS.md`
- `Exports/WoWSharpClient/TASKS.md`
- `Exports/Navigation/TASKS.md`
- `Exports/Loader/TASKS.md`
- `Exports/WinImports/TASKS.md`

## Active Priorities
1. Close FG/BG parity gaps in movement/death/corpse behavior.
2. Keep protobuf snapshots aligned with actual runtime state.
3. Ensure ObjectManager models and update handlers clear/apply server-authoritative fields correctly.
4. Keep BotRunner tasks deterministic and snapshot-driven.

## Handoff Fields
- Last changed subproject:
- Tests/validation run:
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
Move completed items to `Exports/TASKS_ARCHIVE.md`.





## Behavior Cards
1. CrossExportCorpseCombatGatheringParity
- [ ] Behavior: shared export libraries produce consistent FG/BG behavior across corpse-run, combat loop, and gathering loop scenarios.
- [ ] FG Baseline: establish FG evidence for corpse/combat/gathering flows using the canonical BotRunner live validations.
- [ ] BG Target: BG mirrors FG outcome, movement/spell cadence, and packet/state transitions across all three scenarios in the same validation cycle.
- [ ] Implementation Targets: `Exports/BotRunner/TASKS.md`, `Exports/BotCommLayer/TASKS.md`, `Exports/GameData.Core/TASKS.md`, `Exports/WoWSharpClient/TASKS.md`, `Exports/Navigation/TASKS.md`, `Exports/Loader/TASKS.md`, `Exports/WinImports/TASKS.md`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: all scenarios complete for FG/BG with no lingering repo-scoped clients/managers; teardown evidence captured with process name, PID, and stop outcome.
- [ ] If Fails: add `Research:CrossExportParityGap::<scenario>` and `Implement:CrossExportParityFix::<owner-project>` tasks and link owner TASKS paths.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
