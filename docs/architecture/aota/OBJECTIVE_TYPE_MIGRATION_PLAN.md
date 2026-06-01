# WWoW `ObjectiveType` migration plan — op-level enum → Activity-scoped Objectives

> **Status:** PLAN (2026-05-29). Companion to [OBJECTIVE_TYPES.md](OBJECTIVE_TYPES.md) (the target enum +
> full 84-value reclassification). **Owner-gated:** this is a wire-contract change touching **814 references
> across 201 files** (incl. ~150 LiveValidation tests). It must NOT be interleaved with the active impl loop —
> run it when that loop is idle, or as its own dedicated migration loop. No code is changed by authoring this.

## What's actually changing

Today: `ObjectiveMessage.ObjectiveType` (wire, 84 op-values) → `MapProtoObjectiveType` → `CharacterAction`
(1:1) → executed. The wire carries operations.

Target: `ObjectiveMessage.ObjectiveType` (wire, 26 Activity-scoped Objectives) → BotRunner per-Objective
behavior tree → Tasks → Actions. `CharacterAction` (C# enum) is retained as the **internal** execution
vocabulary, decoupled from the wire. This is the concrete realization of **Phase-2 slot S2.0** (runtime
`IObjective`) and dovetails with **Phase 12** (test isolation — tests stop remote-controlling at the op level).

## Invariants (non-negotiable)

- **R10** — proto change recompiles all clients + tests; a missing contract update must fail the build.
- **R18** — no `[Obsolete]`, no parallel old/new enum behind a flag, no `// removed` stubs. Each slice fully
  removes the legacy values it replaces and migrates every caller in the same change.
- **R2 / R17** — after migration, only Objectives cross the wire; BotRunner decomposes + state-verifies. No
  `MapProtoObjectiveType` 1:1 op execution survives.
- **Test-isolation rule** (WWoW CLAUDE.md): migrated tests declare an Activity/Objective and assert on
  `WoWActivitySnapshot` state — they must not construct raw op-level `ObjectiveMessage`s in the test body.

## Sequenced slices (each slice is self-contained: proto + sender + receiver + tests, recompiles green)

> Family slices remove their legacy enum values and all 814 refs incrementally. Order is dependency-first
> (movement underpins everything; session bootstrap is independent; combat/quest/economy build on movement).

0. **Pre-work (doc, this AOTA effort):** author `TASK_CONTRACTS.md` (the ~40 **T** rows) + `ACTION_TAXONOMY.md`
   (the ~35 **A** rows + the atomic opcodes named in OBJECTIVE_TYPES §2). The migration transcribes against these.
1. **Scaffold the new wire enum + dispatch seam.** Add the 26-value Activity-scoped `ObjectiveType` (§1) to a
   NEW proto message path and an `IObjectiveBehaviorTree` decomposition entry point in BotRunner that replaces
   `MapProtoObjectiveType`. (This is the only step that briefly coexists with the old path; it is removed by the
   end of the final slice — track it so R18 holds at completion, not mid-flight.)
2. **Movement/travel slice** — `TravelTo` Objective; migrate `GOTO/TRAVEL_TO/SET_FACING/JUMP/START_MOVEMENT/
   STOP_MOVEMENT/SELECT_TAXI_NODE/VISIT_FLIGHT_MASTER/BoardTransport` into `GoToTask`/`SelectFlightDestinationTask`/
   `BoardTransportTask` + movement Actions. Remove those 6+ legacy values + refs.
3. **Session slice** — `BringCharacterOnline`; migrate `CREATE_CHARACTER/LOGIN/ENTER_WORLD` (+ `LOGOUT/
   DELETE_CHARACTER` to the StateManager control path). Remove values + refs.
4. **NPC/quest slice** — `InteractNpc`/`AcceptQuest`/`TurnInQuest`; migrate `INTERACT_WITH/SELECT_GOSSIP/
   ACCEPT_QUEST/DECLINE_QUEST/SELECT_REWARD/COMPLETE_QUEST`.
5. **Combat slice** — `KillTarget`/`ClearArea`; migrate `START_*_ATTACK/STOP_ATTACK/CAST_SPELL/STOP_CAST/
   FOLLOW_TARGET/DISMISS_BUFF`. (Combat rotations stay in BotRunner per R17.)
6. **Item/economy slice** — `AcquireItem`/`UseItemOnTarget`/`RepairAndRestock`/`ManageInventory`/`CraftItems`/
   `TrainSkills`; migrate `USE_ITEM/EQUIP_ITEM/UNEQUIP_ITEM/DESTROY_ITEM/MOVE_ITEM/SPLIT_STACK/BUY_ITEM/
   BUYBACK_ITEM/SELL_ITEM/REPAIR_ITEM/REPAIR_ALL_ITEMS/CRAFT/TRAIN_SKILL/TRAIN_TALENT/VISIT_VENDOR/
   VISIT_TRAINER/CHECK_MAIL`.
7. **Loot/corpse/recovery slice** — `LootCorpse`-family into `AcquireItem`/`ClearArea`; `RecoverFromDeath`
   from `RESURRECT/RELEASE_CORPSE/RETRIEVE_CORPSE/SKIN_CORPSE/GATHER_NODE`.
8. **Group/raid/trade/loot-dist slice** — `FormParty`/`ManageRaid`/`TradeWithPlayer`/`DistributeLoot` from the
   `*_GROUP_*/PROMOTE_*/*_TRADE/*_LOOT*/CONVERT_TO_RAID/CHANGE_RAID_SUBGROUP/SEND_CHAT` values.
9. **Activity slice** — `FishAtSpot`/`GatherRoute`/`RunDungeon`/`PvpJoinQueue`/`ApplyLoadout`/`IdleAtAnchor`
   from `START_FISHING/START_GATHERING_ROUTE/START_DUNGEONEERING/JOIN_BATTLEGROUND/ACCEPT_BATTLEGROUND/
   LEAVE_BATTLEGROUND/APPLY_LOADOUT/WAIT`.
10. **Control split** — move `START_PHYSICS_RECORDING/STOP_PHYSICS_RECORDING` to a dedicated diagnostic control
    message (off the Objective enum).
11. **Burn the bridge** — delete the legacy 84-value enum + `MapProtoObjectiveType` + the scaffold seam from
    step 1; confirm zero residual refs; full proto recompile (R10); whole suite green (R18 satisfied).

## Test migration (the ~150 LiveValidation refs)

Per slice, rewrite that family's tests to: declare the Activity/Objective (`AssignedActivity` / `StageBotRunner*`
fixture helpers) and assert on `WoWActivitySnapshot` (current Objective + task-stack progression), NOT construct
raw `ObjectiveMessage{op}` in the test body. This is exactly Phase 12's intent, so the two efforts should be
merged — do not migrate the enum and then re-touch the same tests for Phase 12 separately.

## Acceptance

- [ ] Wire `ObjectiveType` = the 26 Activity-scoped values; no op-level value on the wire.
- [ ] `MapProtoObjectiveType` 1:1 switch deleted; BotRunner decomposes each Objective via behavior tree.
- [ ] All 814 legacy refs migrated/removed (R18 — no `[Obsolete]`, no parallel path at completion).
- [ ] Proto recompiles all clients (R10); full test suite green; LiveValidation asserts Objective+snapshot state.
- [ ] `TASK_CONTRACTS.md` + `ACTION_TAXONOMY.md` exist and match the migrated Task/Action vocabulary.
