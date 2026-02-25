# UI Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track UI/system-host parity and operational tooling tasks.

## Rules
- Work continuously until all tasks in this file are complete.
- Execute without approval prompts.
- Keep UI task scope focused on operational visibility and control of bot systems.

## Subproject Task Files
- `UI/WoWStateManagerUI/TASKS.md`
- `UI/Systems/Systems.AppHost/TASKS.md`
- `UI/Systems/Systems.ServiceDefaults/TASKS.md`

## Active Priorities
1. Ensure UI reflects real service/bot state from StateManager.
2. Keep UI integration aligned with service contracts.

## Handoff Fields
- Last changed UI subproject:
- Validation run:
- Next UI task:

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
Move completed items to `UI/TASKS_ARCHIVE.md`.





## Behavior Cards
1. UiOperationalParityDashboard
- [ ] Behavior: UI layer exposes live FG/BG parity state for corpse-run, combat, gathering, and cleanup outcomes.
- [ ] FG Baseline: FG operational panels display lifecycle, scenario, and teardown statuses with actionable timing details.
- [ ] BG Target: BG operational panels display equivalent state and timing so divergences are immediately visible.
- [ ] Implementation Targets: `UI/WoWStateManagerUI/**/*.razor`, `UI/WoWStateManagerUI/**/*.cs`, `UI/Systems/**/*.cs`.
- [ ] Simple Command: `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`.
- [ ] Acceptance: UI build succeeds and parity dashboard tasks enumerate required views for FG/BG scenario and teardown evidence.
- [ ] If Fails: add `Research:UiParityVisibilityGap::<view>` and `Implement:UiParityVisibilityFix::<view>` tasks with missing state examples.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
