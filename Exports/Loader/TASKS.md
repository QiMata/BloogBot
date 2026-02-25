# Loader Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track loader/injection tasks related to FG bot startup stability and diagnostics.

## Rules
- Execute continuously without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep crash diagnostics and guardrails linked to concrete evidence.

## Active Priorities
1. Maintain stable FG injection and startup diagnostics.
2. Prevent regression in startup/attach paths affecting LiveValidation reliability.

## Session Handoff
- Last loader change:
- Validation evidence:
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
Move completed items to `Exports/Loader/TASKS_ARCHIVE.md`.




## Behavior Cards
1. ForegroundAttachStartupDiagnosticsParity
- [ ] Behavior: FG client injection/startup is deterministic, surfaces startup diagnostics clearly, and does not leave orphaned loader-hosted processes after failures.
- [ ] FG Baseline: FG startup path consistently injects and reports attach/host initialization milestones before scenario execution.
- [ ] BG Target: BG startup dependencies from loader paths do not regress FG/BG comparative scenario runs or teardown safety.
- [ ] Implementation Targets: `Exports/Loader/dllmain.cpp`, `Exports/Loader/BotHostControl.h`, `Exports/Loader/nethost_helpers.h`, `Exports/Loader/Loader.vcxproj`.
- [ ] Simple Command: `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32`.
- [ ] Acceptance: loader build succeeds, startup diagnostics remain available for troubleshooting, and failed startup paths can still be cleaned with repo-scoped PID evidence.
- [ ] If Fails: add `Research:LoaderStartupFailure::<phase>` and `Implement:LoaderStartupParityFix::<module>` tasks with crash/log references.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
