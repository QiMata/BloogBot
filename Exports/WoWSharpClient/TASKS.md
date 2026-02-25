# WoWSharpClient Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Headless WoW 1.12.1 protocol emulation, movement handling, object updates, and state parity.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Prioritize server-authoritative state handling over local assumptions.

## Active Priorities
1. Movement and physics parity
- [ ] Validate teleport/fall/landing movement updates mirror FG behavior.
- [ ] Ensure movement flags and fall-time transitions are applied and cleared correctly.
- [ ] Validate live BG snapshots confirm movement flags are cleared immediately after teleport (no stuck-forward state).
- [ ] Investigate BG `MOVEFLAG_FORWARD` persistence with zero movement after follow `Goto` actions (`flags=0x1`, `Physics returned same position`) to distinguish teleport residue vs path/physics no-op.

2. Death/corpse packet handling
- [ ] Keep reclaim delay packet handling accurate and synchronized with snapshot countdown.

3. Object update parity
- [ ] Audit aura/buff/spell and unit-state field clearing on server updates.
- [ ] Ensure NearbyObjects/NearbyUnits expose enough data for deterministic test assertions.

4. Group/party packet parity
- [ ] Add coverage for edge-case group-list payload variants (raid flags/assistant bits/empty-group transitions across live snapshots).

## Session Handoff
- Last bug/task closed:
  - Fixed `PartyNetworkClientComponent.ParseGroupList` header parsing from incorrect 3-byte header assumption to MaNGOS 1.12.1 2-byte header (`groupType + ownFlags`) before `memberCount`.
  - BG `GroupUpdates` now reports sane member counts (`Members: 1` instead of `1140850688`) and snapshot party leader parity aligns with FG in live tests.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartyNetworkClientComponentTests" --logger "console;verbosity=minimal"`
    - Passed (`73/73`).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~GroupFormationTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/groupformation_run_post_parser_fix.log`
    - Passed (`GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp`).
    - Live log evidence includes `Group list updated - Type: Party, Members: 1` and `COMBAT_COORD ... FG PartyLeader=5, BG PartyLeader=5`.
- Files changed:
  - `Exports/WoWSharpClient/Networking/ClientComponents/PartyNetworkClientComponent.cs`
- Next task:
  - Continue live BG movement triage on persistent `flags=0x1` with zero displacement after follow `Goto` actions; confirm whether this is pathfinding-route absence, physics no-op, or stale action-control bits.

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
Move completed items to `Exports/WoWSharpClient/TASKS_ARCHIVE.md`.



## Behavior Cards
1. TeleportFlagResetAndFallStateParity
- [ ] Behavior: teleport and death/corpse transitions clear stale movement flags and apply correct fall/ground states so BG motion matches FG immediately after relocation.
- [ ] FG Baseline: FG movement/state snapshots show clean flag transitions (no stuck-forward residue) and correct fall/landing behavior after teleport-related transitions.
- [ ] BG Target: BG packet parsing and movement controller updates produce the same post-teleport state transitions, including corpse/ghost phases.
- [ ] Implementation Targets: `Exports/WoWSharpClient/Movement/MovementController.cs`, `Exports/WoWSharpClient/Parsers/MovementPacketHandler.cs`, `Exports/WoWSharpClient/Models/WoWLocalPlayer.cs`, `Exports/WoWSharpClient/Models/WoWCorpse.cs`, `Exports/WoWSharpClient/Networking/ClientComponents/PartyNetworkClientComponent.cs`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: no persistent forward flag with zero displacement after teleport/follow, corpse lifecycle remains synchronized, and logs show deterministic teardown on timeout/failure.
- [ ] If Fails: add `Research:TeleportStateMismatch::<packet-or-model-field>` and `Implement:TeleportStateParityFix::<component>` tasks with log references.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
