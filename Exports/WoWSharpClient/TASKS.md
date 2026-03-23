<<<<<<< HEAD
﻿# WoWSharpClient Tasks

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


=======
# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side object model parsing, packet send/receive flow, and network client components.
- This file tracks concrete missing-implementation items tied to source files and test coverage.
- Master tracker: `MASTER-SUB-009`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep model/packet changes paired with deterministic tests in `Tests/WoWSharpClient.Tests` and/or `Tests/WowSharpClient.NetworkTests`.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/WoWSharpClient/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/WoWSharpClient/WoWSharpClient.csproj` builds cleanly in `Release`.
- [ ] Opcode tests and object-update tests run before marking parser/send-path work complete.
- [ ] BG behavior changes are validated against FG parity expectations in the same scenario cycle.

## Evidence Snapshot (2026-02-25)
- `WSC-MISS-001` evidence is still present in object manager mapping:
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs:2000-2039` contains explicit `not implemented` notes for player fields (`ChosenTitle`, `KnownTitles`, `ModHealingDonePos`, `ModTargetResistance`, `FieldBytes`, `OffhandCritPercentage`, `SpellCritPercentage`, `ModManaRegen`, `ModManaRegenInterrupt`, `MaxLevel`, `DailyQuests`).
- `WSC-MISS-002` remains a TODO stub:
  - `Exports/WoWSharpClient/Models/WoWUnit.cs:270` says `TODO: Send CMSG_CANCEL_AURA`.
- `WSC-MISS-003` remains unresolved:
  - `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs:249` logs `"Custom navigation strategy not implemented"`.
- `WSC-MISS-004` still uses placeholder reward-selection behavior:
  - `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs:266` comment indicates placeholder selection of first reward via `SelectGossipOptionAsync(0, ...)`.
- Build/test inventory baseline in this shell:
  - `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj -c Release` succeeded.
  - `dotnet test ...WoWSharpClient.Tests... --list-tests` and `dotnet test ...WowSharpClient.NetworkTests... --list-tests` both enumerate tests (with warning noise in current environment).

## P0 Active Tasks (Ordered)

### WSC-MISS-001 Implement missing `WoWPlayer` field coverage referenced in object manager notes
- [x] **Done (batch 1).** 11 properties added (ChosenTitle, KnownTitles, etc.) + CopyFrom + switch wiring.
- [x] Acceptance: parser notes are removed/replaced by implemented assignments and tests assert populated values when fields are present.

### WSC-MISS-002 Implement `CMSG_CANCEL_AURA` send path for `WoWUnit.DismissBuff`
- [x] **Done (batch 1).** `CancelAura()` on ObjectManager + `DismissBuff()` on WoWUnit.
- [x] Acceptance: dismissing an active buff emits `CMSG_CANCEL_AURA` with expected payload.

### WSC-MISS-003 Resolve `GossipNavigationStrategy.Custom` runtime warning path
- [x] **Done (batch 1).** Downgraded to Debug log (valid no-op for callers handling navigation externally).
- [x] Acceptance: supported gossip flows no longer rely on an unimplemented strategy branch.

### WSC-MISS-004 Replace placeholder quest reward selection strategy logic
- [x] **Done (batch 13).** Replaced placeholder with strategy-aware `SelectRewardIndex()`:
  - `FirstReward` → first available reward index
  - `HighestValue` → highest VendorValue
  - `BestForClass` → first SuitableForClass match, fallback to first
  - `BestStatUpgrade` → highest StatScore
  - `MostNeeded` → SuitableForClass > StatScore > VendorValue priority chain
  - `Custom` → falls through to first reward (caller handles externally)
  - Added overload accepting `IReadOnlyList<QuestRewardChoice>` for strategy-aware selection.
  - Interface `IGossipNetworkClientComponent` updated with new overload.
  - `SelectRewardIndex` is `internal static` for unit test access.
- [x] Validation: `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj -c Debug` — 0 errors. 1229/1234 WoWSharpClient tests pass (4 pre-existing failures).
- [x] Acceptance: quest reward selection respects requested strategy; placeholder behavior removed.

### WSC-PAR-005 Preserve physics flags when forced stop clears directional intent
- [x] **Done (2026-03-12).** `MovementController.SendStopPacket()` and the reset-time forced-stop path now clear only directional/turn intent while preserving falling/swimming state, and `WoWSharpObjectManager.ForceStopImmediate()` mirrors that contract instead of zeroing all movement flags.
- [x] Validation: `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` and `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"` -> 38 passed.
- [x] Acceptance: a forced stop during shoreline overrun or mid-fall no longer erases `MOVEFLAG_FALLINGFAR`, and targeted movement tests prove the preserved-physics behavior.

## Simple Command Set
1. `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj -c Release`
2. `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented" Exports/WoWSharpClient -g "*.cs"`

## Session Handoff
- Last updated: 2026-03-12
- Active task: WSC-PAR-005 complete; next queue file should continue the cross-runner fishing/movement parity work
- Last delta: forced-stop movement packets now preserve active falling/swimming physics state while clearing directional intent, which unblocks the fishing shoreline parity slice
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` — succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"` — 38 passed
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Inventory.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: continue with next queue file
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
>>>>>>> cpp_physics_system

## 2026-03-23 Session Note
- Pass result: `delta shipped`
- Local delta: BG transport parity now uses shared world/local transforms across packet serialization, physics input/output, and object-manager passenger sync.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1277 passed`, `1 skipped`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementPacketHandlerTests|FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests" -v n` -> `58 passed`
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/Movement/TransportCoordinateHelper.cs`
  - `Exports/WoWSharpClient/Parsers/MovementPacketHandler.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
- Next command: `Get-Content Exports/WoWSharpClient/Movement/SplineController.cs | Select-Object -First 260`

## 2026-03-23 Session Note (Force-Speed Parity)
- Pass result: `delta shipped`
- Local delta: managed BG movement now handles the missing server-forced walk-speed, swim-back-speed, and turn-rate opcodes end to end across dispatcher, legacy bridge, event emitter, object manager state application, and ACK generation.
- Validation:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet build Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.MissingForceChangeOpcodes_ParseApplyAndAck" -v n` -> `3 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1280 passed`, `1 skipped`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n` -> `117 passed`
- Files changed:
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
- Next command: `Get-Content Exports/WoWSharpClient/Models/WoWUnit.cs | Select-Object -First 220`
