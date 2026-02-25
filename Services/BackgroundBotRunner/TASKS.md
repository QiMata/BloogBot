# BackgroundBotRunner Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Headless runner integration and behavior alignment with shared BotRunner tasks.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep command/action handling deterministic and observable.

## Active Priorities
1. Runner action execution parity
- [ ] Ensure forwarded actions are executed in order and reflected in snapshots quickly.
- [ ] Keep long-running actions from being interrupted by unrelated movement/coordinator behavior.
- [ ] Investigate follow-loop `Goto` behavior where BG can remain `MOVEFLAG_FORWARD` (`flags=0x1`) with zero displacement after teleport/release transitions.

2. Command-response observability
- [ ] Keep logs sufficient to map dispatched commands to server responses during tests.

## Session Handoff
- Last parity issue closed:
  - None in `Services/BackgroundBotRunner` code this session; parity evidence gathered through live `DeathCorpseRunTests` logs.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"`
  - Result set this session: one skipped rerun, one pass (~2m10s), one fail (FG corpse-run intermittent stall), then one pass (~2m10s).
  - BG evidence now includes explicit `Goto` no-route warnings (`[GOTO] No route ...`) after pathfinding-driven `Goto` rollout; this reduces hidden stuck-forward loops but needs retry/log tuning.
- Files changed:
  - none in `Services/BackgroundBotRunner/*` (work landed in `Exports/WoWSharpClient/*` and shared BotRunner flow).
- Next task:
  - Add targeted BG follow-loop diagnostics linking dispatched `Goto` actions to path query results and movement-controller displacement (`step > 0`) expectations.

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
Move completed items to `Services/BackgroundBotRunner/TASKS_ARCHIVE.md`.



## Behavior Cards
1. BackgroundRunnerCommandExecutionParity
- [ ] Behavior: BG runner executes queued movement/combat actions through teleport and corpse-release transitions without stuck-forward loops.
- [ ] FG Baseline: FG runner clears or adjusts movement flags as soon as displacement drops to zero and resumes with valid route commands.
- [ ] BG Target: BG runner mirrors FG command cadence, route requests, and movement-flag resets for the same scenario timeline.
- [ ] Implementation Targets: `Services/BackgroundBotRunner/**/*.cs`, `Exports/WoWSharpClient/Movement/**/*.cs`, `Exports/BotRunner/Tasks/**/*.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: corpse-run completes with no prolonged `MOVEFLAG_FORWARD` plus zero-displacement loops, and teardown evidence records only repo-scoped process shutdown.
- [ ] If Fails: add `Research:BackgroundRunnerStuckForward::<scenario>` and `Implement:BackgroundRunnerCommandRecovery::<scenario>` tasks with route/log evidence references.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
