# Master Tasks - Test & Validate Everything

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/TASKS_ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker).
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. **GM Mode OFF after setup** - `.gm on` corrupts UnitReaction bits. Always `.gm off` before test actions.
7. **Clear repo-scoped WoW/test processes before building.** DLL injection locks output files.
8. **Previous phases archived** - see `docs/ARCHIVE.md` and `docs/TASKS_ARCHIVE.md`.

---

## Test Baseline (2026-04-16)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1494 | 0 | 1 | All movement parity (30/30) green |
| Navigation.Physics.Tests | 137 | 0 | 1 | All movement parity (8/8) green |
| BotRunner.Tests (unit) | 1747 | 0 | 4 | NavigationPathTests (80/80) green |

---

## Completed Phases (archived to docs/TASKS_ARCHIVE.md)

- **P0** - Movement / Physics WoW.exe Parity: All open items closed.
- **P1** - Alterac Valley 40v40 Integration: All open items closed.
- **P2** - WoW.exe Packet Handling & ACK Parity: All sub-phases closed.
- **R1-R10** - Archived (see `docs/ARCHIVE.md`).
- **Deferred D1-D4** - All closed.

---

## P3 - Unified Loadout Hand-off (StateManager → BotRunner)

### Context
Test fixtures currently orchestrate every step of per-bot prep (revive, level,
learn spells/skills, add/equip items, elixirs, honor-rank, rep, talents) by
firing dozens of individual `EquipItem`/`SendChat` actions per account from the
fixture process. This is chatty, slow, and mixes test-harness concerns with
what should be BotRunner's own responsibility — to move itself to a target
configured state.

### Goal
Replace the per-step fixture drip with a **single `ApplyLoadout` action** per
bot. StateManager ships the full target spec once; BotRunner owns the
execution. When every bot reports `LoadoutStatus.Ready`, the coordinator
drives raid formation → all-members-joined → kick-off (e.g. `JoinBattleground`
for WSG). Chatter between StateManager and BotRunner is limited to state
transitions that matter for coordination decisions.

### Rules
1. Loadout spec lives **in the config file** alongside `CharacterSettings`.
   Source of truth; no programmatic generation in fixtures.
2. Spec covers: `TargetLevel`, talents, spells to learn, skills with values,
   armor set id, equip items (per slot), supplemental items (bag fills,
   elixirs), mount spells, honor rank, faction rep rows, completed quest
   ids, riding skill.
3. One `ApplyLoadout` action per bot — BotRunner pushes a `LoadoutTask`,
   executes steps at its own pace, and emits a single `LoadoutStatus`
   transition when done.
4. Snapshot adds one small field: `LoadoutStatus` (`NotStarted`,
   `InProgress`, `Ready`, `Failed`) + optional failure reason. No chatty
   per-step reporting.
5. `BattlegroundCoordinator` waits for all configured bots' snapshots to
   report `Ready` before emitting raid invites. Then it waits for all bots
   to be in the leader's party/raid before emitting the kick-off action.

### Sub-phases

- [ ] **P3.1** Plumbing — proto types
  - [ ] P3.1.1 Add `LoadoutSpec` message to `communication.proto` with
    target level, talents, spells (repeated uint32), skills (repeated
    SkillValue{id,value,max}), armor set id, equip items (repeated
    ItemSlot{itemId,inventorySlot}), supplemental items (repeated uint32),
    elixir item ids, mount spell id, honor rank, faction reputations
    (repeated FactionRep{factionId,standing}), completed quest ids
    (repeated uint32), riding skill target.
  - [ ] P3.1.2 Add `LoadoutStatus` enum to `communication.proto`
    (`NotStarted`, `InProgress`, `Ready`, `Failed`).
  - [ ] P3.1.3 Add `WoWActivitySnapshot.loadoutStatus` and
    `WoWActivitySnapshot.loadoutFailureReason`.
  - [ ] P3.1.4 Add `ActionType.ApplyLoadout` with `LoadoutSpec` as the
    `ActionMessage` payload.
  - [ ] P3.1.5 Include `loadoutStatus` in `SnapshotChangeSignature` so the
    event-driven IPC sends a full snapshot when the status transitions.

- [ ] **P3.2** Config — `CharacterSettings.Loadout`
  - [ ] P3.2.1 Add `LoadoutSpec Loadout { get; set; }` to `CharacterSettings`.
  - [ ] P3.2.2 Seed WSG config with per-bot loadouts (port today's
    `AlteracValleyLoadoutPlan.ResolveLoadout` output into the JSON).
  - [ ] P3.2.3 Seed AB and AV configs the same way.
  - [ ] P3.2.4 Validation test: every BG config entry has a non-empty
    loadout spec and the spec is self-consistent (items reference valid
    slots, spells are valid ids).

- [ ] **P3.3** BotRunner — `LoadoutTask`
  - [ ] P3.3.1 Create `Exports/BotRunner/Tasks/LoadoutTask.cs`. On
    construction, read the spec and plan ordered steps.
  - [ ] P3.3.2 Executes: wait for `IsObjectManagerValid` → set level via
    `.levelup` chat → learn riding spell / `.setskill` ride rank → learn
    mount spell → set honor rank via SOAP → apply faction rep → learn
    spells (`.learn` each) → `.setskill` each skill → `.additemset` armor
    set → `.additem` each supplemental → `EquipItem` each item (respect
    slot) → `UseItem` elixirs → mark completed quests.
  - [ ] P3.3.3 Emits snapshot status transitions:
    `NotStarted` → `InProgress` (first tick) → `Ready` (all steps
    succeeded) or `Failed` (irrecoverable error with reason).
  - [ ] P3.3.4 Idempotent: safe to re-apply. Skip "already know that
    spell", already-equipped items, etc. Tolerate `CHAT_MSG_SYSTEM` noise.
  - [ ] P3.3.5 Unit tests: deterministic task plan from a fixed spec;
    status transitions on mocked ObjectManager + chat responses.

- [ ] **P3.4** StateManager — dispatch `ApplyLoadout`
  - [ ] P3.4.1 `BattlegroundCoordinator` state machine gets a new
    `ApplyingLoadouts` state between `WaitingForBots` and
    `StagingAtBattlemaster`. Coordinator enqueues one `ApplyLoadout`
    per bot with the spec from CharacterSettings.
  - [ ] P3.4.2 Transition to next state only when every configured bot's
    snapshot shows `loadoutStatus=Ready`. Bots with `Failed` go on an
    exclusion list; coordinator logs but proceeds without them.
  - [ ] P3.4.3 Unit tests: state stays in `ApplyingLoadouts` until all
    bots are Ready; transitions forward on consensus; records failures.

- [ ] **P3.5** Coordinator — raid formation gate
  - [ ] P3.5.1 After loadouts ready, coordinator emits raid-invite desired
    party state (existing `DesiredParty*` path).
  - [ ] P3.5.2 New state `WaitingForRaidFormation` gated on every member's
    snapshot showing `PartyLeaderGuid == leaderGuid`.
  - [ ] P3.5.3 Transition to `QueueForBattleground` only after the gate
    passes. Existing queue/accept/entry flow unchanged.

- [ ] **P3.6** Fixture cleanup
  - [ ] P3.6.1 Delete `WarsongGulchFixture.EnsureLoadoutPreparedAsync`,
    `PrepareLoadoutsOnceAsync`, `RunLoadoutPrepAsync`, `PrepareLoadoutAsync`.
  - [ ] P3.6.2 Delete `AlteracValleyFixture` loadout orchestration.
  - [ ] P3.6.3 Test body stays: fixture writes config, launches, asserts
    on coordinator state progression.

### Design invariants
- **One `ApplyLoadout` action per bot per fixture run.** No per-step
  chatter from StateManager.
- **Snapshots stay small.** `LoadoutStatus` is the only new field; it is
  included in the event-driven signature so transitions are captured
  but heartbeats are not inflated.
- **BotRunner owns execution timing.** It runs the plan at whatever
  pace is safe for its own behavior tree + connection state.
- **Config is authoritative.** Fixtures read from
  `Services/WoWStateManager/Settings/Configs/*.config.json`; no
  programmatic loadout generation.

---

## P2 - WoW.exe Packet Handling & ACK Parity (COMPLETE)

### Context
Physics parity against WoW.exe is green. Packet dispatch, ObjectManager state mutation, and ACK generation still have unverified corners. This phase closes those gaps with binary-backed evidence.

**Full plan:** `docs/WOW_EXE_PACKET_PARITY_PLAN.md` (10 gaps identified, 7 sub-phases).

### Sub-phases
- [x] **P2.1** Decompilation research: packet dispatch & ACK generation
  - [x] P2.1.1 Capture `NetClient::ProcessMessage` (0x537AA0) disassembly; identify opcode dispatch mechanism
  - [x] P2.1.2 Dump opcode -> handler mapping as `docs/physics/opcode_dispatch_table.md`
  - [x] P2.1.3 Capture `NetClient::Send` (0x005379A0) disassembly
  - [x] P2.1.4 Decompile P1 handlers: speed change, root, knockback, water walk, hover, teleport, worldport ACK
  - [x] P2.1.5 Decompile `CGPlayer_C` / `CGUnit_C` / `CGObject_C` vtables
  - [x] P2.1.6 Trace movement counter: CMovement offset, increment points, packet inclusion
- [x] **P2.2** ACK format parity (byte-level)
  - [x] P2.2.1 Capture golden corpus ACK bytes from FG bot for each ACK opcode
  - [x] P2.2.2 Add `AckBinaryParityTests` — one test per ACK opcode asserting byte equality
  - [x] P2.2.3 Fix every byte divergence citing a VA
  - [x] P2.2.4 Confirm movement counter semantics match WoW.exe
  - [x] P2.2.5 Gate: all 14 wired ACKs have passing byte-parity tests
- [x] **P2.3** ACK timing & ordering parity
  - [x] P2.3.1 Answer Q1-Q5 (sync vs deferred; see plan §4.3) with binary evidence
  - [x] P2.3.2 Write failing tests for current timing divergences
  - [x] P2.3.3 Fix timing via defer-to-controller or immediate-after-mutation pattern
  - [x] P2.3.4 Close **G1** knockback ACK race
- [x] **P2.4** ObjectManager state mutation parity
  - [x] P2.4.1 Produce `docs/physics/cgobject_layout.md` with exact field offsets
  - [x] P2.4.2 Audit C# classes — each field mapped to a WoW.exe field or documented as intentional omission
  - [x] P2.4.3 Decompile `CGWorldClient::HandleUpdateObject` block-walk order
  - [x] P2.4.4 Write `ObjectUpdateMutationOrderTests` replaying captured SMSG_UPDATE_OBJECT streams
  - [x] P2.4.5 Fix mutation-order divergences
- [x] **P2.5** Packet-flow end-to-end parity
  - [x] P2.5.1 Build `PacketFlowTraceFixture` — bytes in, bytes out, state observer, ordered event log
  - [x] P2.5.2 Write one trace test per representative packet (8 tests: UPDATE_OBJECT Add, UPDATE_OBJECT Update, FORCE_RUN_SPEED_CHANGE, FORCE_MOVE_ROOT, MOVE_KNOCK_BACK, MOVE_TELEPORT, NEW_WORLD→WORLDPORT_ACK, MONSTER_MOVE)
  - [x] P2.5.3 Fix divergences discovered by trace tests
- [x] **P2.6** State-machine parity
  - [x] P2.6.1 Document each state machine (control, teleport, worldport, login, knockback, root) in `docs/physics/state_<name>.md`
  - [x] P2.6.2 Audit implementation against documented transitions
  - [x] P2.6.3 Write `StateMachineParityTests`
  - [x] P2.6.4 Close **G4** (teleport flag clear) and **G8** (teleport ACK deadlock)
- [x] **P2.7** Gap closure (G1-G10 verification)
  - [x] P2.7.1 **G2** wire `MSG_MOVE_TIME_SKIPPED` listener
  - [x] P2.7.2 **G3** wire `MSG_MOVE_JUMP` / `MSG_MOVE_FALL_LAND` consumer
  - [x] P2.7.3 **G6** close `MSG_MOVE_SET_RAW_POSITION_ACK` as not-applicable in WoW.exe 1.12.1
  - [x] P2.7.4 **G7** close `CMSG_MOVE_FLIGHT_ACK` as not-applicable in WoW.exe 1.12.1
  - [x] P2.7.5 Final regression: all parity bundles + new `AckParity` / `PacketFlowParity` / `StateMachineParity` bundles green

### Gaps identified (2026-04-16)
| Gap | Summary                                                             | Close in  |
| --- | ------------------------------------------------------------------- | --------- |
| G1  | Knockback ACK sent before physics consumes impulse                  | P2.3      |
| G2  | `MSG_MOVE_TIME_SKIPPED` has no ObjectManager listener                | P2.7.1    |
| G3  | Jump/fall land events fire but no consumer                           | P2.7.2    |
| G4  | Teleport flag-clear only masks 8 bits (jump/fall/swim persist)       | P2.6.4    |
| G5  | SPLINE_MOVE opcodes ACK behavior unverified                          | P2.3      |
| G6  | `0x00E0` has no static registration and no live WoW.exe ACK emission | P2.7.3    |
| G7  | `0x033E/0x033F/0x0340` have no static registration and no live WoW.exe ACK emission | P2.7.4    |
| G8  | Teleport ACK `IsSceneDataReady()` may deadlock                       | P2.6.4    |
| G9  | ACK byte format vs WoW.exe unverified                                | P2.2      |
| G10 | Movement counter semantics unverified                                | P2.2      |

---

## Handoff (2026-04-20)

- Completed: closed the WSG desired-party/objective slice end to end. `BotRunnerService.DesiredParty.GetCurrentGroupSize(...)` now counts the local player when `PartyAgent` reports only the other four members of a full 5-player party, so Horde leaders actually convert to raid before inviting the remaining queue roster. The WSG objective scenarios also now run on separate fresh fixture collections, which removes the shared-fixture contamination where a completed full-game run left the next destructive scenario at `hydrated=19/20`.
- Deterministic coverage:
  - `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs` now pins the `PartyAgent.GroupSize == 4` / `GetGroupMembers().Count == 4` case and verifies it still drives the existing `IObjectManager.ConvertToRaid()` behavior path.
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs` now logs the exact raw snapshot(s) missing from `AllBots` when live hydration stalls, instead of only emitting the aggregate `19/20` count.
- Live-validation coverage:
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs` now uses a shared abstract base plus two separate collection fixtures so the single-capture and full-game objective scenarios each start from a fresh 20-bot WSG roster.
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs` exposes an explicit battleground-reset helper used by the objective prep path.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FullGame_CompletesToVictoryOrDefeat" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_fullgame_after_group_size_fix_20260421_0210.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "(FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFlagCaptureObjectiveTests|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFullGameObjectiveTests)" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_objective_split_fixtures_20260421_0337.trx"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found.`
- Evidence:
  - `tmp/test-runtime/results-live/wsg_fullgame_after_group_size_fix_20260421_0210.trx`
  - `tmp/test-runtime/results-live/wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx`
  - `tmp/test-runtime/results-live/wsg_objective_split_fixtures_20260421_0337.trx`
- Files changed: `Exports/BotRunner/BotRunnerService.DesiredParty.cs`, `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchObjectiveCollection.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs`, and task trackers.
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`

## Handoff (2026-04-19)

- Completed: closed the battleground queue-entry stabilization slice that followed `P2`. Early battleground/friend/ignore handlers are now registered before fresh world-client login traffic arrives, duplicate `JoinBattleground` dispatch no longer stacks queue tasks, and the Arathi Basin queue/entry fixture is stable on the background-only runner path.
- Live-validation note:
  - `tmp/test-runtime/results-live/ab_queue_entry_alliance_groundlevel_recheck.trx` captured a failed rerun where `ABBOT1` (PID `33636`) crashed during the foreground battleground transfer edge.
  - `tmp/test-runtime/results-live/ab_queue_entry_background_only_recheck.trx` then passed after the fixture moved both AB leaders onto background runners, matching the existing `CoordinatorFixtureBase` warning that foreground battleground transfers are unstable in this harness.
- Validation:
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were up before the reruns.
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the deterministic/live reruns.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AgentFactoryTests" --logger "console;verbosity=minimal"` -> `passed (101/101)`
  - `powershell -ExecutionPolicy Bypass -File ./run-tests.ps1 -CleanupRepoScopedOnly` -> repo-scoped cleanup completed before each live run.
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_alliance_groundlevel_recheck.trx"` -> `failed` with `[AB:BG] CRASHED`
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
- Files changed: `Exports/WoWSharpClient/Networking/ClientComponents/NetworkClientComponentFactory.cs`, `Services/BackgroundBotRunner/BackgroundBotWorker.cs`, `Exports/BotRunner/ActionDispatcher.cs`, `Tests/WoWSharpClient.Tests/Agent/AgentFactoryTests.cs`, `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs`, `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`, and task trackers.
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
- Binary-backed note:
  - `docs/physics/0x466590_disasm.txt` now pins the deep `SMSG_UPDATE_OBJECT` descriptor walker: WoW.exe copies the update mask into stack scratch, walks fields in ascending descriptor-index order, and forwards each present field through `0x466A00 -> 0x6142E0`.
  - `docs/physics/0x466C70_disasm.txt` now pins the typed create-path switch directly: `0x466C73` rejects type ids above `7`, the jump table at `0x466DB8` only covers the eight packet-instantiated object families, and there is no separate packet-instantiated `CGPet_C` branch in this path.
  - `docs/physics/state_root.md` already pins the WoW.exe root/unroot queue-first path (`0x61A700` staging through `0x617570`), and the parity harness now proves both `SMSG_FORCE_MOVE_ROOT` and `SMSG_FORCE_MOVE_UNROOT` defer mutation/ACK until the later flush.
  - `docs/physics/state_knockback.md` already pins the WoW.exe knockback queue path (`0x603F90 -> 0x602780 -> 0x602670 -> 0x617A30 -> 0x6177A0`), and the parity harness now proves BG stages the impulse first, consumes it later, and ACKs only after that consume step.
  - `opcode_dispatch_table.md` already pinned `SMSG_CLIENT_CONTROL_UPDATE` to `0x603EA0`; the new disasm now proves that WoW.exe reads a packed GUID, reads a one-byte `canControl` flag, looks up the target object, and forwards the normalized bool into `0x5FA600`.
  - `0x5FA600` toggles bit `0x400` in `[object + 0xC58]` and only runs the follow-up global update when the object's GUID matches the active mover. That means the packet's GUID and byte both matter, so BG now ignores non-local GUIDs and preserves an explicit local lockout until `canControl=true` arrives.
  - `docs/physics/msg_move_teleport_handler.md` / `docs/physics/packet_ack_timing.md` still show the only confirmed WoW.exe teleport-ACK gate at this stage: `MSG_MOVE_TELEPORT` applies through `0x602F90 -> 0x6186B0`, while outbound `0x0C7` is emitted later from `0x602FB0` after the internal `0x468570` gate.
  - There is still no binary evidence that `0x468570` depends on local tile/scene loading. That made our `SceneDataClient.EnsureSceneDataAround(...)` requirement an unsupported BG-only deadlock source, so the gate was removed and the tests now pin “updates drained + ground snap resolved” as the managed readiness condition.
- Commands run:
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were up before the validation pass.
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the targeted build/test run.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectUpdateMutationOrderTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateMachineParityTests|FullyQualifiedName~ClientControlUpdate_LocalPlayer_TracksCanControlAndBlocksReconcile|FullyQualifiedName~ClientControlUpdate_RemoteGuid_DoesNotAffectLocalControl|FullyQualifiedName~NotifyTeleportIncoming_ClearsMovementFlagsToNone|FullyQualifiedName~TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PacketFlowParityTests|FullyQualifiedName~StateMachineParityTests|FullyQualifiedName~NotifyTeleportIncoming_ClearsMovementFlagsToNone|FullyQualifiedName~TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData" --logger "console;verbosity=minimal"` -> `passed (13/13)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=PacketFlowParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateMachineParityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=StateMachineParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
- Files changed: `docs/physics/0x466590_disasm.txt`, `docs/physics/0x466C70_disasm.txt`, `docs/physics/cgobject_layout.md`, `docs/physics/csharp_object_field_audit.md`, `docs/physics/smsg_update_object_handler.md`, `docs/physics/README.md`, `docs/TASKS.md`, `docs/TASKS_ARCHIVE.md`, `Exports/WoWSharpClient/TASKS.md`, and `Tests/WoWSharpClient.Tests/TASKS.md`.
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md -g '!**/TASKS_ARCHIVE.md'`

## Canonical Commands

```bash
# Repo-scoped process inspection/cleanup only
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly

# Keep dotnet cache/temp + test artifacts on repo drive
$env:DOTNET_CLI_HOME='E:\repos\Westworld of Warcraft\tmp\dotnethome'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:WWOW_REPO_ROOT='E:\repos\Westworld of Warcraft'
$env:WWOW_TEST_RUNTIME_ROOT='E:\repos\Westworld of Warcraft\tmp\test-runtime'
$env:VSTEST_RESULTS_DIRECTORY='E:\repos\Westworld of Warcraft\tmp\test-runtime\results'
$env:TEMP='E:\repos\Westworld of Warcraft\tmp\test-runtime\temp'
$env:TMP='E:\repos\Westworld of Warcraft\tmp\test-runtime\temp'

# Build .NET + C++ (both architectures)
dotnet build WestworldOfWarcraft.sln --configuration Release
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"
$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_latest.trx"

# Docker rebuild + deploy
docker compose -f docker-compose.vmangos-linux.yml build scene-data-service
docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service
```
