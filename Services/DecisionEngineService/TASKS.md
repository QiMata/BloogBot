# DecisionEngineService Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\Services\DecisionEngineService

Projects:
- DecisionEngineService.csproj

## Instructions
- Execute tasks directly without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep this file focused on active, unresolved work only.
- Add new tasks immediately when new gaps are discovered.
- Archive completed tasks to TASKS_ARCHIVE.md.

## Active Priorities
1. Validate this project behavior against current FG/BG parity goals.
2. Remove stale assumptions and redundant code paths.
3. Add or adjust tests as needed to keep behavior deterministic.

## Session Handoff
- Last task completed:
- Validation/tests run:
- Files changed:
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
Move completed items to TASKS_ARCHIVE.md and keep this file short.




## Behavior Cards
1. DecisionEngineActionSelectionParity
- [ ] Behavior: decision engine selects the same next-action sequence for corpse-run, combat, and gathering scenarios under equivalent inputs.
- [ ] FG Baseline: FG decision traces produce stable player-like action ordering and pacing for scenario goals.
- [ ] BG Target: BG decision traces mirror FG priorities, cooldown handling, and movement/action transitions for the same state snapshots.
- [ ] Implementation Targets: `Services/DecisionEngineService/**/*.cs`, `BotProfiles/**/*.cs`, `Exports/BotRunner/Tasks/**/*.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: FG/BG action sequencing deltas stay within expected tolerance and no divergent decision branch blocks scenario completion.
- [ ] If Fails: add `Research:DecisionEngineParityGap::<scenario>` and `Implement:DecisionEngineSelectionFix::<scenario>` tasks tied to trace evidence.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
