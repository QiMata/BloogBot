# BotProfiles Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track profile and rotation behavior tasks for all classes/specs.

## Rules
- Work continuously until all tasks in this file are complete.
- Implement continuously without approval prompts.
- Prioritize correctness and parity with shared BotRunner task semantics.

## Active Priorities
1. Audit profile task usage against updated BotRunner task contracts.
2. Remove duplicate/legacy behavior paths as shared tasks stabilize.

## Handoff Fields
- Last profile/spec touched: `ProfilePullRotationRestParity` (shared combat loop harness).
- Validation tests run: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` (Passed, 2026-02-24, evidence: `tmp/row1_combatloop_20260224_031204.txt`, `tmp/row1_pretest_processes_20260224_031204.txt`, `tmp/row1_postcleanup_processes_20260224_031204.txt`).
- Next profile task: run a combined FG/BG live validation cycle (`DeathCorpseRunTests|CombatLoopTests|GatheringProfessionTests`) and add profile-specific mismatch tasks if parity drifts.

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
Move completed items to `BotProfiles/TASKS_ARCHIVE.md`.





## Behavior Cards
1. ProfilePullRotationRestParity
- [ ] Behavior: FG and BG complete the same pull -> rotation -> loot -> rest loop without unnecessary pauses or redundant actions.
- [ ] FG Baseline: capture a successful FG combat loop cycle that uses profile `PullTargetTask`, `PvERotationTask`, and `RestTask` in expected order.
- [ ] BG Target: BG mirrors FG ordering, movement cadence, spell cadence, and downtime so behavior is indistinguishable over repeated pulls.
- [ ] Implementation Targets: `BotProfiles/*/Tasks/PullTargetTask.cs`, `BotProfiles/*/Tasks/PvERotationTask.cs`, `BotProfiles/*/Tasks/RestTask.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: FG and BG both finish combat loops with no stall/retry spirals; action ordering and idle windows are comparable; timeout path includes repo-scoped PID teardown evidence.
- [ ] If Fails: add `Research:ProfileLoopMismatch::<spec>` and `Implement:ProfileLoopParityFix::<spec>` tasks with evidence links in this file.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
