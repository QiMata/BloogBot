# WoWSharpClient.Tests Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\Tests\WoWSharpClient.Tests

Projects:
- WoWSharpClient.Tests.csproj

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
4. Keep protocol payload builders aligned with authoritative MaNGOS 1.12.1 packet layouts.

## Session Handoff
- Last task completed:
  - Updated party packet payload builder assumptions for `SMSG_GROUP_LIST` to MaNGOS 1.12.1 format (`groupType + ownFlags + memberCount`).
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartyNetworkClientComponentTests" --logger "console;verbosity=minimal"`
  - Result: Passed (`73/73`).
- Files changed:
  - `Tests/WoWSharpClient.Tests/Agent/PartyNetworkAgentTests.cs`
- Next task:
  - Add targeted assertions for live-observed `SMSG_GROUP_LIST` edge payloads when raid/assistant flags are present.

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
1. WoWSharpClientMovementDeathStateParitySuite
- [ ] Behavior: WoWSharpClient unit tests enforce movement, death-state, and reclaim-timer behavior needed for corpse-run parity.
- [ ] FG Baseline: FG client-side state transitions remain deterministic and match expected lifecycle semantics.
- [ ] BG Target: BG client-side state transitions mirror FG semantics, including teleport recovery and fall-state updates.
- [ ] Implementation Targets: `Tests/WoWSharpClient.Tests/**/*.cs`, `Exports/WoWSharpClient/**/*.cs`, `Services/WoWStateManager/**/*.cs`.
- [ ] Simple Command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
- [ ] Acceptance: unit suite passes with explicit coverage for death lifecycle, movement flags, and reclaim delay gating behavior.
- [ ] If Fails: add `Research:WoWSharpClientStateParityGap::<component>` and `Implement:WoWSharpClientStateParityFix::<component>` tasks with failing assertions.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
