# BotCommLayer Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Protobuf contracts and communication model compatibility across FG/BG paths.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep schema changes backward-safe for all active callers.

## Active Priorities
1. Snapshot schema parity
- [ ] Ensure parity-critical fields are present and consistently populated.
- [ ] Add/update compatibility checks when fields are added or changed.

2. Action contract clarity
- [ ] Keep action enum and parameter mapping synchronized across producers/consumers.

## Session Handoff
- Last schema/action change:
- Regeneration/build verification:
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
Move completed items to `Exports/BotCommLayer/TASKS_ARCHIVE.md`.



## Behavior Cards
1. SnapshotCorpseMovementContractParity
- [ ] Behavior: serialized FG/BG activity snapshots expose matching corpse lifecycle and movement fields used by behavior decisions and test assertions.
- [ ] FG Baseline: FG snapshot payload carries dead/ghost/reclaim and movement values that align with live state transitions.
- [ ] BG Target: BG snapshot payload matches FG field presence, value semantics, and update timing for the same transitions.
- [ ] Implementation Targets: `Exports/BotCommLayer/Models/ProtoDef/game.proto`, `Exports/BotCommLayer/Models/ProtoDef/communication.proto`, `Exports/BotCommLayer/Models/WoWActivitySnapshotExtensions.cs`, `Exports/BotCommLayer/ProtobufSocketServer.cs`, `Exports/BotCommLayer/ProtobufSocketClient.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: snapshot tests pass with no missing parity-critical fields and no schema/extension conversion regressions; timeout/failure path includes repo-scoped teardown evidence.
- [ ] If Fails: add `Research:SnapshotContractMismatch::<field-or-packet>` and `Implement:SnapshotContractFix::<component>` tasks with proto + codeowner references.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
