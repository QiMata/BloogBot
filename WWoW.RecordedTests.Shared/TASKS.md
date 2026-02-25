# WWoW.RecordedTests.Shared Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track parity and migration tasks for WWoW recorded-test shared components.

## Rules
- Work continuously until all tasks in this file are complete.
- Execute without approval prompts.
- Keep this project aligned with primary recorded-test shared implementation.

## Active Priorities
1. Identify overlap/divergence vs `RecordedTests.Shared`.
2. Consolidate or clearly separate responsibilities.

## Handoff Fields
- Last parity check:
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
Move completed items to `WWoW.RecordedTests.Shared/TASKS_ARCHIVE.md`.





## Behavior Cards
1. WWoWRecordedSharedFixtureParity
- [ ] Behavior: WWoW shared recorded-test assets remain deterministic and compatible across replay suites.
- [ ] FG Baseline: FG shared asset generation and lookup produce stable metadata and artifact ordering.
- [ ] BG Target: BG shared asset usage reads equivalent metadata and avoids drift that would skew parity replay checks.
- [ ] Implementation Targets: `WWoW.RecordedTests.Shared/**/*.cs`, `WWoW.RecordedTests.Shared/*.csproj`, `Tests/WWoW.RecordedTests.Shared.Tests/**/*.cs`.
- [ ] Simple Command: `dotnet test Tests/WWoW.RecordedTests.Shared.Tests/WWoW.RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
- [ ] Acceptance: shared asset workflows pass determinism tests and provide stable inputs for WWoW replay parity scenarios.
- [ ] If Fails: add `Research:WWoWSharedFixtureGap::<component>` and `Implement:WWoWSharedFixtureFix::<component>` tasks with artifact evidence.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
