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
- [x] Implement immediate teleport movement reset to clear stale `MOVEFLAG_FORWARD`/movement flags on teleport events.
- [ ] Validate live BG snapshots confirm movement flags are cleared immediately after teleport (no stuck-forward state).
- [ ] Investigate BG `MOVEFLAG_FORWARD` persistence with zero movement after follow `Goto` actions (`flags=0x1`, `Physics returned same position`) to distinguish teleport residue vs path/physics no-op.

2. Death/corpse packet handling
- [ ] Keep reclaim delay packet handling accurate and synchronized with snapshot countdown.
- [x] Ensure ghost/dead state transitions are reflected immediately in object/player models (descriptor-first `InGhostForm` in `WoWLocalPlayer`).

3. Object update parity
- [ ] Audit aura/buff/spell and unit-state field clearing on server updates.
- [ ] Ensure NearbyObjects/NearbyUnits expose enough data for deterministic test assertions.
- [x] Harden GameObject field diff numeric conversion to avoid `InvalidCastException` (`Single` -> `UInt32`) during live update processing.

4. Group/party packet parity
- [x] Fix `SMSG_GROUP_LIST` parsing to MaNGOS 1.12.1 wire format (`groupType(1) + ownFlags(1) + memberCount(4)`).
- [x] Validate BG party leader snapshot parity in live group formation (`FG PartyLeaderGuid == BG PartyLeaderGuid`).
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


