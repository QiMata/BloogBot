# GameData.Core Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Shared interfaces/models used by both FG and BG object managers and bot logic.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep interface changes tightly coupled to actual parity requirements.

## Active Priorities
1. Life-state and corpse model parity
- [ ] Keep dead/ghost/reclaim fields aligned with server semantics and snapshot usage.

2. Object/unit/player model consistency
- [ ] Ensure model fields required by tests and BotRunner tasks are explicit and stable.

## Session Handoff
- Last interface/model update:
- Downstream impact validated in:
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
Move completed items to `Exports/GameData.Core/TASKS_ARCHIVE.md`.



## Behavior Cards
1. DeathStateAndReclaimModelParity
- [ ] Behavior: core interfaces expose consistent dead/ghost/reclaim/movement flags so FG/BG decision logic reads the same lifecycle state.
- [ ] FG Baseline: FG object model reflects server-authoritative death and reclaim values at each corpse lifecycle phase.
- [ ] BG Target: BG model fields mirror FG semantics and naming so shared task logic behaves identically.
- [ ] Implementation Targets: `Exports/GameData.Core/Interfaces/IWoWLocalPlayer.cs`, `Exports/GameData.Core/Interfaces/IWoWCorpse.cs`, `Exports/GameData.Core/Interfaces/IObjectManager.cs`, `Exports/GameData.Core/Enums/DeathState.cs`, `Exports/GameData.Core/Enums/MovementFlags.cs`, `Exports/GameData.Core/Enums/CorpseFlags.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ReleaseCorpseTaskTests|FullyQualifiedName~RetrieveCorpseTaskTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: corpse task tests pass with no interface drift regressions and lifecycle state transitions remain deterministic between FG/BG snapshots.
- [ ] If Fails: add `Research:GameDataLifecycleMismatch::<field>` and `Implement:GameDataLifecycleParityFix::<interface-or-enum>` tasks with downstream consumer list.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
