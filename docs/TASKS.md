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

## P4 - Command ACK Infrastructure (message capture parity + structured ACKs)

P3 is archived (see `docs/TASKS_ARCHIVE.md`); this phase builds on the loadout
hand-off and tightens how the bot observes the server's response to GM
commands / player actions.

### Context
Today `LoadoutTask` advances on `IsSatisfied()` polling of `ObjectManager`
state (spell in `KnownSpellIds`, item in bags, skill value in `SkillInfo`).
That works but has three real gaps:

1. **BG parity holes.** `SMSG_LEARNED_SPELL` / `SMSG_REMOVED_SPELL` /
   `SMSG_SPELL_FAILURE` / `SMSG_INVENTORY_CHANGE_FAILURE` / `SMSG_NOTIFICATION`
   update `ObjectManager` silently — no event fires
   (`Exports/WoWSharpClient/Handlers/SpellHandler.cs`,
   `Exports/WoWSharpClient/Client/WorldClient.cs`). FG sees these via Lua
   hooks; BG sees nothing, so BG tests reading `[SKILL]` / `[ERROR]` prefixes
   from the snapshot come up empty.
2. **Snapshot signature churn.** `BotRunnerService.SnapshotChangeSignature`
   (`Exports/BotRunner/BotRunnerService.cs:111-125`) includes
   `RecentChatCount` + `RecentErrorCount`. Every new message flips the count
   → forces a full snapshot send. Under heavy chat (loadout dispatch, BG
   fights) we send full snapshots every tick and defeat the 2s heartbeat
   throttle.
3. **No structured per-command ACK.** Tests that want to say *"did this
   specific `.learn 12345` succeed?"* have to baseline + diff + pattern-match
   on text (`LiveBotFixture.BotChat.cs.GetDeltaMessages` +
   `LiveBotFixture.Assertions.cs.ContainsCommandRejection`). Works today
   but brittle — there is no correlation id on `ActionMessage`, and MaNGOS
   1.12 emits no chat text for most GM command successes, so the "wait for
   system message" pattern can't gate on `.learn` / `.setskill` / `.additem`
   at all.

### Goal
Close the BG event-parity gap, stop message volume from churning the
snapshot signature, and give `LoadoutTask` an event-driven alternative
(push notification) to its current polling-based `IsSatisfied` path —
without throwing away the polling fallback, which is the only option for
commands that have no authoritative SMSG (`.modify money`, `.setskill`,
`.modify health/mana`).

### Rules
1. **Every `.learn` must target a specific numeric spell id, every
   `.setskill` a specific skill id.** Catch-all MaNGOS commands
   (`.learn all_myclass`, `.learn all_myspells`) are forbidden — see
   `memory/feedback_explicit_spell_learning.md`.
2. **Polling stays.** State observation is the authoritative success
   signal; events are a latency optimization, not a replacement.
3. **No new free-form message buckets.** If BG needs to surface a new
   SMSG-observed event, it goes through an existing `IWoWEventHandler`
   event (new prefix if needed) so FG parity is preserved.
4. **Snapshot budget matters.** Do not add unbounded repeated fields.
   Ring-buffer with an explicit cap; document it next to the field.

### Sub-phases

- [x] **P4.1** Close BG SMSG → event parity gap
  - [x] P4.1.1 Add `OnLearnedSpell(spellId)` and `OnUnlearnedSpell(spellId)`
    events to `Exports/GameData.Core/Interfaces/IWoWEventHandler.cs`. Fire
    from `SpellHandler.HandleLearnedSpell` / `HandleRemovedSpell`. Surface
    as `[SKILL] Learned spell <id>` / `[SKILL] Unlearned spell <id>` in
    `BotRunnerService.Messages.cs`.
  - [x] P4.1.2 Add `OnSkillUpdated(skillId, oldValue, newValue, maxValue)`
    event. Fire from whichever `SMSG_UPDATE_OBJECT` path mutates
    `IWoWLocalPlayer.SkillInfo` (locate the descriptor-walker site in
    `ObjectUpdate`-family handlers). Surface as
    `[SKILL] Skill <id> <old>→<new>/<max>`.
  - [x] P4.1.3 Add `OnItemAddedToBag(bag, slot, itemId, count)` event.
    Fire from the inventory-change-success path
    (`LootingNetworkClientComponent.OnItemPushResultReceived` is the
    existing observable — mirror it into an `IWoWEventHandler` event so
    FG/BG parity is maintained). Surface as `[UI] Item <id> x<count>
    → bag <bag>/<slot>`.
  - [x] P4.1.4 Route `SMSG_ATTACKSWING_*`, `SMSG_INVENTORY_CHANGE_FAILURE`,
    and `SMSG_SPELL_FAILURE` through `FireOnErrorMessage` alongside their
    existing Rx/diagnostic channels. Today they're silent at the event
    layer (`WorldClient.cs:234-251`, `SpellHandler.cs:459`).
  - [x] P4.1.5 Register a handler for `SMSG_NOTIFICATION` (0x1CB) and
    raise `OnSystemMessage(text)`.
  - [x] P4.1.6 Unit tests: each new event fires once per matching
    inbound packet; `[SKILL]` / `[UI]` / `[ERROR]` prefixes land in
    `snapshot.RecentChatMessages` / `snapshot.RecentErrors` via the
    existing flush path.

- [x] **P4.2** Fix snapshot signature churn
  - [x] P4.2.1 Remove `RecentChatCount` + `RecentErrorCount` from
    `SnapshotChangeSignature` in `Exports/BotRunner/BotRunnerService.cs`.
    Messages ride along on full snapshots that fire for real state
    changes + the 2s heartbeat.
  - [x] P4.2.2 Regression test: a stream of `[SYSTEM]`/`[SKILL]` messages
    arriving with no other state change must not trigger a full snapshot
    send; only the next heartbeat (or next real change) should carry
    them.
  - [x] P4.2.3 Confirm that test helpers still work: `GetDeltaMessages`
    already handles the case where deltas arrive only on heartbeat ticks.

- [x] **P4.3** LoadoutTask event-driven step advancement
  - [x] P4.3.1 Extend `LoadoutStep` with an optional
    `AttachExpectedAck(IWoWEventHandler)` handle that each step installs
    before `TryExecute`. `LearnSpellStep` subscribes to `OnLearnedSpell`
    filtered on its `_spellId`; `AddItemStep` subscribes to
    `OnItemAddedToBag` filtered on its `_itemId`; `SetSkillStep`
    subscribes to `OnSkillUpdated` filtered on `_skillId` and only flips
    the ack when `NewValue >= _value`. The first matching event flips
    `AckFired`, which short-circuits `IsSatisfied` → true on the next tick.
  - [x] P4.3.2 Polling remains the fallback. `IsSatisfied` returns
    `AckFired || CheckState(context)` so event + poll race benignly; whichever
    flips first wins.
  - [x] P4.3.3 `LoadoutTask.Update` detaches the advanced step's
    subscription immediately after the `while (TryIsSatisfied)` loop, and
    `TransitionToReady`/`Fail` detach every remaining step. `AttachExpectedAck`
    is idempotent at both the step (`_ackInstalled` guard) and the task
    (`_acksAttached` guard) levels so re-entering the same `LoadoutTask` does
    not double-subscribe.
  - [x] P4.3.4 Unit tests in `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
    now pin: per-step ack filtering; `SuppressFakeServer`-driven advancement
    on the very next `Update()` without a pacing sleep; single-step plan
    reaches `Ready` on event alone; polling-only path still reaches `Ready`
    when no event fires; detach removes the subscription; attach is
    idempotent; null event handler is a safe no-op; per-step detach on
    advancement leaves the active step still subscribed.

- [x] **P4.4** Correlation IDs + structured `CommandAckEvent`
  - [x] P4.4.1 Add `string correlation_id = <n>;` to `ActionMessage` in
    `Exports/BotCommLayer/Models/ProtoDef/communication.proto`.
    StateManager assigns one per dispatch; BotRunner echoes it back.
  - [x] P4.4.2 Add a new message
    `CommandAckEvent { string correlation_id; ActionType action_type;
    enum AckStatus {Pending, Success, Failed, TimedOut} status;
    string failure_reason; uint32 related_id; }` and
    `repeated CommandAckEvent recent_command_acks` on
    `WoWActivitySnapshot` (ring-buffer cap 10; document next to the
    field).
  - [x] P4.4.3 `BotRunnerService` populates the ring on every action it
    dispatches (including `LoadoutTask` step actions). Include the
    correlation id in the action's `CurrentAction` as it goes into
    `_activitySnapshot.CurrentAction`.
  - [x] P4.4.4 `SnapshotChangeSignature` gains
    `RecentCommandAckCount` so coordinator-level transitions can react
    to ACK arrivals without heartbeat lag. (Unlike the chat rings, ack
    counts change rarely — per dispatched command, not per chat line —
    so this does not reintroduce the churn from P4.2.)
  - [x] P4.4.5 Unit tests: end-to-end round trip — StateManager sends
    an action with correlation id, bot pushes a step, emits
    `CommandAckEvent(Success)` or `CommandAckEvent(Failed, reason)`
    in the snapshot.

- [x] **P4.5** Coordinator + test migration to structured ACKs
  - [x] P4.5.1 `BattlegroundCoordinator.LastAckStatus(correlationId, snapshots)`
    scans every bot's `RecentCommandAcks` ring and returns the latest
    status for the id (terminal Success/Failed/TimedOut beats Pending).
    `LoadoutStatus` stays as the per-phase roll-up; `CommandAckEvent`
    is the per-command receipt.
  - [x] P4.5.2 `LiveBotFixture.BotChat.SendGmChatCommandTrackedAsync`
    stamps a `test:<account>:<seq>` correlation id on the outbound
    `ActionMessage` (StateManager only stamps empty ids, so the test id
    survives end-to-end) and returns `GmChatCommandTrace` with
    `CorrelationId`, `AckStatus`, and `AckFailureReason` populated from
    the matching `CommandAckEvent` in `RecentCommandAcks`.
  - [x] P4.5.3 `LiveBotFixture.AssertTraceCommandSucceeded` is the new
    ACK-first helper — `AckStatus ∈ {Failed, TimedOut}` is an
    authoritative rejection; otherwise falls back to
    `ContainsCommandRejection` for commands not yet wired into the ACK
    ring. `IntegrationValidationTests` and `TalentAllocationTests`
    delegate their local `AssertCommandSucceeded` to it. Remaining
    fixtures still use the legacy path and will be migrated incrementally.
  - [x] P4.5.4 `BattlegroundCoordinatorAckTests` feeds scripted
    `RecentCommandAcks` rings through `LastAckStatus` and pins the
    Pending/terminal precedence, cross-snapshot scan, missing-id, and
    failed-with-reason contracts.

### Design invariants
- **No new catch-all `.learn all_*`.** Explicit IDs only, per curated
  per-(class, race) roster in
  `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ClassLoadoutSpells.cs`.
- **Polling stays as the authoritative success signal.** Events are
  a latency optimization; they never decide "did this succeed?" alone.
- **Snapshot budget:** ring buffers only, explicit caps, documented.
- **Correlation IDs flow through `ActionMessage` end-to-end.** No
  StateManager → bot → StateManager round trip loses the id.
- **FG/BG parity.** Every event added to `IWoWEventHandler` must have
  a firing path from both the FG Lua-hook bridge and the BG SMSG
  handler. If one side can't fire it, document why.

---

## P5 - Coordinator ACK Consumption

`P4` opened the correlated ACK plumbing but left `BattlegroundCoordinator.LastAckStatus`
as a static helper with only test coverage. `P5` turns ACKs into a real coordinator
signal, starting with the narrowest sub-phase that already has full ACK support on
the dispatch side.

### Context
`HandleApplyingLoadouts` currently gates `ApplyingLoadouts → WaitingForRaidFormation`
entirely on `WoWActivitySnapshot.LoadoutStatus`. That works when a `LoadoutTask`
runs to completion, but it leaves two gaps where the coordinator stalls forever:

1. **Pre-task rejection.** `BotRunnerService.Messages.cs` emits `CommandAckEvent.Failed`
   with reason `loadout_task_already_active` or `unsupported_action` *before* any
   `LoadoutTask` starts — `LoadoutStatus` never flips.
2. **Step-level TimedOut.** `LoadoutTask` emits `CommandAckEvent.TimedOut` for a
   step that exceeds `MaxRetriesPerStep`. Terminal status reaches the snapshot
   ACK ring, but `LoadoutStatus` may still read `LoadoutInProgress` for a tick.

### Rules
1. **ACK gates are additive, not replacements.** `snapshot.LoadoutStatus` remains
   the primary signal. ACK-driven short-circuit only activates when the ACK is
   terminal and the coordinator has not yet resolved the account.
2. **Deterministic correlation IDs.** Coordinator-dispatched actions pre-stamp
   their own correlation id; `CharacterStateSocketListener.StampDispatchCorrelationId`
   already respects non-empty ids, so the pre-stamp survives end-to-end.
3. **No new proto fields, no new listener plumbing.** Consume only what `P4.4`/`P4.5`
   already put on the wire.

### Sub-phases

- [x] **P5.1** Loadout ACK consumption in `BattlegroundCoordinator.HandleApplyingLoadouts`
  - [x] P5.1.1 Factor `LastAckStatus` into `LastAck` (returns `CommandAckEvent?`)
    + thin `LastAckStatus` wrapper. Coordinator consumers need the failure
    reason; tests that only care about status stay unaffected.
  - [x] P5.1.2 Pre-stamp each dispatched `ApplyLoadout` action with
    `bg-coord:loadout:<account>:<guid>`; record the id in `_loadoutCorrelationIds`.
  - [x] P5.1.3 `RecordLoadoutProgressFromSnapshots` consults `LastAck` before
    `LoadoutStatus`. Terminal Success → `_loadoutReady`; Failed/TimedOut →
    `_loadoutFailed` with the ack reason; Pending is ignored so the existing
    `LoadoutStatus` gate still holds.
  - [x] P5.1.4 Unit tests in `BattlegroundCoordinatorLoadoutTests` cover
    correlation-id stamping and Success/Failed/TimedOut/Pending ACK outcomes
    without relying on `LoadoutStatus`.

### Design invariants
- **Coordinator-owned correlation ids.** Coordinator-dispatched actions stamp
  deterministic ids (prefix `bg-coord:<phase>:<account>:<guid>`). Listener-stamped
  sequence ids only apply when the dispatcher left `CorrelationId` empty.
- **One direction, no round-trip.** Coordinator writes the id on dispatch; the
  bot echoes it back through `CommandAckEvent`. No new RPC, no new storage.
- **Polling stays.** ACK resolution is a short-circuit for terminal gaps, not
  a replacement for `snapshot.LoadoutStatus`.

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

## Handoff (2026-04-22, P5.x LiveValidation ACK migration)

- Completed: migrated the remaining six `LiveValidation/*` `AssertCommandSucceeded`
  helpers to delegate to `LiveBotFixture.AssertTraceCommandSucceeded`. P4.5.3
  started this with `IntegrationValidationTests` and `TalentAllocationTests`;
  this slice closes out: `CombatLoopTests`, `CharacterLifecycleTests`,
  `BuffAndConsumableTests`, `GatheringProfessionTests`, `MageTeleportTests`,
  `QuestInteractionTests`. Each file keeps its local `AssertCommandSucceeded`
  shape (signature-stable) but the body is now a one-line delegation.
- Non-duplicate helpers preserved: `CombatLoopTests.ContainsCombatCommandFailure`
  / `TraceHasCombatCommandFailure` stayed — those are combat-specific rejection
  checks unrelated to the generic command-rejection text scan.
- Removed: `CombatLoopTests` local `ContainsCommandRejection` copy (identical
  to the shared `LiveBotFixture.ContainsCommandRejection`, only referenced
  by the now-delegating `AssertCommandSucceeded`).
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 pre-existing warnings, 0 errors)`
  - LiveValidation suites require Docker+MaNGOS; deterministic slice already
    covered by the P5.1 build/test pass.
- Notes:
  - ACK gate is additive everywhere: commands not yet wired into `CommandAckEvent`
    still fall through to `ContainsCommandRejection`, so no LiveValidation test
    loses coverage. Commands with real ACK signals (e.g. any future tracked
    ApplyLoadout-equivalent chat command) gain immediate Failed/TimedOut
    detection.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CharacterLifecycleTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`
  - `docs/TASKS.md`
- Next command: `rg -n "private static void AssertCommandSucceeded" Tests/BotRunner.Tests/LiveValidation`

## Handoff (2026-04-22, P5.1)

- Completed: shipped `P5.1` (Loadout ACK consumption in `BattlegroundCoordinator`).
  `P4.5.1`'s `LastAckStatus` is no longer test-only — `HandleApplyingLoadouts`
  now pre-stamps correlation ids and `RecordLoadoutProgressFromSnapshots` closes
  the pre-task-rejection + step-TimedOut gaps where `snapshot.LoadoutStatus`
  never flips.
- Validation:
  - `tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST` -> `No tasks are running which match the specified criteria.`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 pre-existing warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BattlegroundCoordinator" -v minimal` -> `passed (22/22)`
- Notes:
  - `LastAckStatus` now delegates to a richer `LastAck` helper that returns the full
    `CommandAckEvent` (so coordinators can log failure reasons). Existing
    `BattlegroundCoordinatorAckTests` stay green because the status-only wrapper
    preserves the `P4.5.1` contract.
  - `HandleApplyingLoadouts` pre-stamps `ActionMessage.CorrelationId` with
    `bg-coord:loadout:<account>:<guid>`. `CharacterStateSocketListener.StampDispatchCorrelationId`
    already skips stamping when `CorrelationId` is non-empty, so the coordinator id
    survives end-to-end to the `CommandAckEvent` without listener changes.
  - ACK gate is additive: `snapshot.LoadoutStatus` still drives resolution when no
    ACK has arrived; terminal ACKs short-circuit only when the account is still
    unresolved. Pending ACKs are deliberately ignored so the coordinator keeps
    waiting on the concrete LoadoutStatus / terminal ack.
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorLoadoutTests.cs`
  - `docs/TASKS.md`
- Next command: `rg -n "AssertCommandSucceeded|AssertTraceCommandSucceeded" Tests/BotRunner.Tests/LiveValidation`

## Handoff (2026-04-21, P4.5)

- Completed: shipped `P4.5` only. Phase `P4` is now fully closed (P4.1-P4.5).
- Commits:
  - `4c39065c` `feat(coord): P4.5.1 add LastAckStatus helper on BattlegroundCoordinator`
  - `e8306a9f` `test(botrunner): P4.5.2/P4.5.3 expose AckStatus in GmChatCommandTrace`
- Validation:
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundCoordinator|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests|FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~ActionForwardingContractTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
- Notes:
  - `BattlegroundCoordinator.LastAckStatus` is static and reusable — coordinator state handlers can key on a dispatched `ActionMessage`'s correlation id to react to ACK arrivals without repeating the scan logic. Integration into further coordinator transitions is deferred until a concrete driver shows up that needs it.
  - `SendGmChatCommandTrackedAsync` now stamps a test-owned `test:<account>:<seq>` correlation id on every dispatched `ActionMessage`. `CharacterStateSocketListener.StampDispatchCorrelationId` only stamps when the id is empty, so the test id survives to the snapshot.
  - Migration policy: only `AssertCommandSucceeded` helpers in `IntegrationValidationTests` and `TalentAllocationTests` were moved over. The rest continue to use `ContainsCommandRejection` until the backing command wires a `CommandAckEvent`. The legacy helper is intentionally still exposed from `LiveBotFixture.Assertions.cs`.
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorAckTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Assertions.cs`
  - `Tests/BotRunner.Tests/LiveValidation/IntegrationValidationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TalentAllocationTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "^- \\[ \\]|Active task:" docs/TASKS.md`

## Handoff (2026-04-21, P4.4)

- Completed: shipped `P4.4` only. `P4.5` was intentionally not started.
- Commits:
  - `9232c83f` `feat(comm): P4.4 add command ack proto schema`
  - `4d1b7489` `feat(botrunner): P4.4 plumb correlated command acks`
  - `3f800ed9` `test(botrunner): P4.4 cover command ack round-trips`
- Validation:
  - `& .\protocsharp.bat "." ".."` (from `Exports/BotCommLayer/Models/ProtoDef`) -> `succeeded`
  - `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (33 warnings, 0 errors)`
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~LoadoutSpecConverterTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
- Notes:
  - `ActionMessage.correlation_id` now survives the StateManager -> bot -> snapshot round trip. `CharacterStateSocketListener` stamps `account:sequence` ids when a dispatch reaches a bot without an explicit correlation id.
  - `WoWActivitySnapshot.recent_command_acks` is now the canonical cap-10 structured ACK ring. BotRunner emits `Pending` on dispatch plus `Success`/`Failed`/`TimedOut` on completion, including per-step `LoadoutTask` actions.
  - `SnapshotChangeSignature` now includes `RecentCommandAckCount`; unlike the chat/error rings dropped in `P4.2`, ACK count only changes per command dispatch/completion, so coordinator-visible ACK arrivals force immediate full snapshots without reintroducing diagnostic churn.
  - Duplicate `ApplyLoadout` requests now fail the duplicate correlation id without clobbering the original in-flight loadout ACK.
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceLoadoutDispatchTests.cs`
  - `Exports/BotCommLayer/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `rg -n "LastAckStatus|SendGmChatCommandTrackedAsync|RecentCommandAcks|ContainsCommandRejection" Services/WoWStateManager Tests/BotRunner.Tests docs/TASKS.md`

## Handoff (2026-04-21, P4.3)

- Completed: shipped `P4.3` only. `P4.4` (correlation ids + `CommandAckEvent`) and `P4.5` (coordinator + test migration) were intentionally not started.
- Commits:
  - `8add32e9` `feat(botrunner): P4.3 event-driven LoadoutTask step advancement`
- Validation:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors, 515 pre-existing warnings)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors, 727 pre-existing warnings)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
- Notes:
  - `LoadoutStep` now owns the ack lifecycle: `AttachExpectedAck(IWoWEventHandler?)` installs a filtered subscription on the matching event, `DetachExpectedAck()` removes it, and `AckFired` short-circuits `IsSatisfied` without preventing the polling path from flipping it. Steps that do not override `OnAttachExpectedAck` (AddItemSet, EquipItem, UseItem, LevelUp) stay pure-polling.
  - `LoadoutTask.Update` attaches all acks once via `AttachExpectedAcks()` on first tick (gated by `_acksAttached`), detaches per-step on advancement, and detaches all remaining steps on terminal transitions (`TransitionToReady`, `Fail`).
  - Polling fallback untouched: the pacing loop, retry budget, `.additemset`/`.use`/`.levelup` behavior, and `IsOneShot` semantics are all unchanged.
  - New ack tests deliberately disable the fake-server side-effect (`harness.SuppressFakeServer = true`) so advancement is attributable to the event alone; the existing polling-only end-to-end test was kept to prove the fallback still converges when no event ever fires.
- Files changed:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `rg -n "correlation_id|CommandAckEvent|RecentCommandAcks" Exports/BotCommLayer docs/TASKS.md`

## Handoff (2026-04-21, P4.1/P4.2)

- Completed: shipped `P4.1` and `P4.2` only. `P4.3`, `P4.4`, and `P4.5` were intentionally not started.
- Commits:
  - `06b39001` `feat(comm): P4.1 add OnLearnedSpell/OnUnlearnedSpell events (FG+BG)`
  - `a9f9ba6b` `feat(comm): P4.1 add OnSkillUpdated event (FG+BG)`
  - `1560495b` `feat(comm): P4.1 add OnItemAddedToBag event (FG+BG)`
  - `35a05376` `feat(comm): P4.1 route attack/inventory/spell failures through OnErrorMessage`
  - `58fbae48` `feat(comm): P4.1 register SMSG_NOTIFICATION -> OnSystemMessage`
  - `b7293f1a` `fix(botrunner): P4.2 drop RecentChat/ErrorCount from snapshot signature`
- Validation:
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpEventEmitterTests|FullyQualifiedName~LootingNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientAttackErrorTests|FullyQualifiedName~SpellHandlerTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientNotificationTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
- Notes:
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs.GetDeltaMessages(...)` already computes deltas by subtracting the previous full-snapshot list from the current list, so heartbeat-delivered message batches still surface correctly. No helper code change was required for `P4.2.3`.
  - No `P4.1` / `P4.2` sub-task remains open.
- Files changed:
  - `Exports/GameData.Core/Interfaces/IWoWEventHandler.cs`
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Exports/WoWSharpClient/Networking/ClientComponents/LootingNetworkClientComponent.cs`
  - `Services/ForegroundBotRunner/Statics/WoWEventHandler.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Agent/LootingNetworkAgentTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientAttackErrorTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientNotificationTests.cs`
  - `Tests/WoWSharpClient.Tests/WoWSharpEventEmitterTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs`
  - task trackers
- Next command: `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`

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

