# RecordedTests.PathingTests Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track pathing recording/replay test tasks for pathfinding and movement parity.

## Rules
- Work continuously until all tasks in this file are complete.
- Execute without approval prompts.
- Keep fixtures and recordings focused on reproducible movement behavior.

## Active Priorities
1. Verify recordings reflect current physics/pathfinding behavior.
2. Remove obsolete recordings and stale replay assumptions.

## Handoff Fields
- Last recording/test touched:
- Validation result:
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
Move completed items to `RecordedTests.PathingTests/TASKS_ARCHIVE.md`.





## Behavior Cards
1. RecordedPathReplayParityForRunback
- [ ] Behavior: recorded pathing replays remain deterministic and expose movement parity regressions for corpse runback/combat approach routes.
- [ ] FG Baseline: FG replay runner consumes current recordings and produces stable movement traces and completion outcomes.
- [ ] BG Target: BG replay runner consumes the same recordings and mirrors FG movement progression and completion timing envelope.
- [ ] Implementation Targets: `RecordedTests.PathingTests/Program.cs`, `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`, `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`, `RecordedTests.PathingTests/Context/PathingRecordedTestContext.cs`, `RecordedTests.PathingTests/Models/PathingTestDefinitions.cs`.
- [ ] Simple Command: `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
- [ ] Acceptance: replay tests pass with deterministic fixture behavior; when parity drift appears, a linked research task and implementation task are added immediately.
- [ ] If Fails: add `Research:RecordedReplayDrift::<scenario>` and `Implement:RecordedReplayParityFix::<runner-or-definition>` tasks with evidence links.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
