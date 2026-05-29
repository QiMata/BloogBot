# WWoW `ObjectiveType` — closed Activity-scoped enum + legacy reclassification

> **Status:** SPEC / TARGET (2026-05-29). Authored under the monorepo AOTA decomposition effort
> ([../../../../docs/AOTA_AUTHORING_STANDARD.md](../../../../docs/AOTA_AUTHORING_STANDARD.md) §6 / §6.1).
> **This is a doc deliverable — it does not change code.** The code migration is sequenced in the sibling
> [OBJECTIVE_TYPE_MIGRATION_PLAN.md](OBJECTIVE_TYPE_MIGRATION_PLAN.md) and is owner-gated against the active loop.
> **Reads first:** [../../Spec/18_TERMINOLOGY.md](../../Spec/18_TERMINOLOGY.md) (the A/O/T/A glossary).

## 0. Why this exists

Today WWoW's wire `ObjectiveType` (proto, `Exports/BotCommLayer/Models/ProtoDef/communication.proto`, 84
values `WAIT=0`…`STOP_MOVEMENT=83`) is **a roster of operations, not Objectives**. `BotRunnerService`'s
`MapProtoObjectiveType` maps each wire value **1:1 to a `CharacterAction`** and executes it directly
(`BotRunnerService.ActionMapping.cs`). That bypasses the Objective→Task behavior-tree decomposition entirely:
StateManager is remote-controlling the bot at the operation level, which violates the AOTA layer model (R17)
and R2 (the op is executed, not decomposed + state-verified). This is the anti-pattern called out in
AOTA_AUTHORING_STANDARD §6.1.

The fix: the **wire `ObjectiveType` becomes a closed set of Activity-scoped Objectives** (high-level state
changes); the 84 legacy op-values survive as the **BotRunner-internal execution vocabulary** (`CharacterAction`
+ Task contracts + Action taxonomy), **no longer 1:1 with the wire**. BotRunner decomposes each incoming
Objective into Tasks via its behavior tree; each Task invokes atomic Actions.

```
BEFORE:  StateManager → ObjectiveMessage{GOTO,(x,y,z)} → MapProtoObjectiveType → CharacterAction.GoTo (executed)
AFTER:   StateManager → ObjectiveMessage{TravelTo,(x,y,z)} → BotRunner behavior tree → GoToTask → Actions
                                                                                    ↘ BoardTransportTask (if needed)
```

## 1. The closed Activity-scoped `ObjectiveType` enum (proposed target)

A high-level state change that StateManager requests; BotRunner decides *how*. Closed set (R10); next free = 26.

| # | ObjectiveType | High-level state change (what becomes true) | Example BT leaves (Tasks) |
|--:|---|---|---|
| 0 | `Unspecified` | (invalid / default) | — |
| 1 | `TravelTo` | player is at a world location | GoToTask, BoardTransportTask, SelectFlightDestinationTask, VisitFlightMasterTask |
| 2 | `InteractNpc` | an NPC interaction reached its terminal state (gossip path resolved) | InteractWithUnitTask, SelectGossipTask |
| 3 | `AcceptQuest` | quest is in the quest log | InteractWithUnitTask, SelectGossipTask, AcceptQuestTask |
| 4 | `TurnInQuest` | quest is complete + reward chosen | CompleteQuestTask, SelectQuestRewardTask |
| 5 | `KillTarget` | a specific unit/template is dead | StartAttackTask, CastSpellTask, FollowTask |
| 6 | `ClearArea` | hostiles in an area at/below threshold | StartAttackTask, CastSpellTask, GoToTask, LootCorpseTask |
| 7 | `AcquireItem` | item(s) present in bags (loot/buy/gather/craft) | LootCorpseTask, BuyItemTask, GatherNodeTask, CraftTask |
| 8 | `UseItemOnTarget` | item/consumable used toward a goal | UseItemTask, CastSpellTask |
| 9 | `FishAtSpot` | fishing goal met at a pool/spot | GoToTask, CastSpellTask(fishing), LootCorpseTask |
| 10 | `GatherRoute` | a gathering route was run to its goal | GoToTask, GatherNodeTask, SkinCorpseTask |
| 11 | `CraftItems` | item(s) crafted | VisitVendorTask(reagents), CraftTask |
| 12 | `TrainSkills` | available skills/talents trained | VisitTrainerTask, TrainSpellTask, TrainTalentTask |
| 13 | `RepairAndRestock` | gear repaired + consumables bought | VisitVendorTask, BuyItemTask, RepairAllItems(Action) |
| 14 | `ManageInventory` | bags reconciled (sell junk / destroy / bank / reorganize) | SellItemTask, CheckMailTask, MoveItem(Action), DestroyItem(Action) |
| 15 | `FormParty` | target players are grouped | InviteToPartyTask, AcceptGroupInvite(Action) |
| 16 | `ManageRaid` | raid composition/roles set | ConvertToRaid(Action), ChangeRaidSubgroup(Action), Promote*(Action) |
| 17 | `TradeWithPlayer` | a player trade completed | InitiateTradeTask, EnchantTradeItemTask, OfferGold/OfferItem/AcceptTrade(Actions) |
| 18 | `DistributeLoot` | group loot resolved (rolls / master-loot) | LootRoll*(Action), AssignLoot(Action) |
| 19 | `PvpJoinQueue` | queued for a battleground + pop accepted | JoinBattlegroundTask, AcceptBattleground(Action) |
| 20 | `RunDungeon` | inside the instance / dungeon objective progressed | GoToTask, InteractWithUnitTask(stone), KillTarget sub-objectives |
| 21 | `RecoverFromDeath` | alive with corpse recovered | ReleaseCorpseTask, RetrieveCorpseTask, AcceptResurrectTask, GoToTask |
| 22 | `ApplyLoadout` | full loadout (level/spells/skills/items/rep/quests/rank) applied (P3) | LoadoutTask + EquipItemTask/TrainSpellTask children |
| 23 | `BringCharacterOnline` | character is in-world from char-select/create | CreateCharacterTask, LoginTask, EnterWorldTask |
| 24 | `FollowEscort` | following/escorting a target | FollowTask, GoToTask |
| 25 | `IdleAtAnchor` | holding position passively (passive-wait) | IdlePostureTask, GoToTask(anchor) |

> `Logout` / `DeleteCharacter` are session-lifecycle Tasks, not Objectives — they are invoked by the
> StateManager process-control path, not dispatched as an `ObjectiveMessage`. `StartPhysicsRecording` /
> `StopPhysicsRecording` are **diagnostic Control commands** and must move to a separate control message, NOT
> the Objective enum (see §2, Control rows).

## 2. Legacy → new reclassification (all 84 values)

Layer key: **O** = becomes/maps to an Objective (wire). **T** = Task (`TASK_CONTRACTS.md`, BotRunner-internal).
**A** = atomic Action (`ACTION_TAXONOMY.md`, local). **C** = diagnostic Control (separate channel, off the
Objective enum). "Serves" = the new Objective(s) whose behavior tree invokes it.

| Legacy `ObjectiveType` | # | Layer | Maps to / serves | Atomic Action (if A) or note |
|---|--:|:--:|---|---|
| WAIT | 0 | O | → `IdleAtAnchor` (25) | passive hold |
| GOTO | 1 | T | `GoToTask` — serves ~every Objective | universal child |
| INTERACT_WITH | 2 | T | `InteractWithUnitTask` — InteractNpc, AcceptQuest, AcquireItem | |
| SELECT_GOSSIP | 3 | T | `SelectGossipTask` — InteractNpc, AcceptQuest, TurnInQuest | atomic: `SendOpcode(CMSG_GOSSIP_SELECT_OPTION)` |
| SELECT_TAXI_NODE | 4 | T | `SelectFlightDestinationTask` — TravelTo | atomic: `SendOpcode(CMSG_ACTIVATETAXI)` |
| ACCEPT_QUEST | 5 | T | `AcceptQuestTask` — AcceptQuest | atomic: `SendOpcode(CMSG_QUESTGIVER_ACCEPT_QUEST)` |
| DECLINE_QUEST | 6 | A | InteractNpc / AcceptQuest | `SendOpcode(CMSG_QUESTGIVER_CANCEL)` |
| SELECT_REWARD | 7 | T | `SelectQuestRewardTask` — TurnInQuest | atomic: `SendOpcode(CMSG_QUESTGIVER_CHOOSE_REWARD)` |
| COMPLETE_QUEST | 8 | T | `CompleteQuestTask` — TurnInQuest | |
| TRAIN_SKILL | 9 | T | `TrainSpellTask` — TrainSkills | atomic: `SendOpcode(CMSG_TRAINER_BUY_SPELL)` |
| TRAIN_TALENT | 10 | T | `TrainTalentTask` — TrainSkills, ApplyLoadout | atomic: `SendOpcode(CMSG_LEARN_TALENT)` |
| OFFER_TRADE | 11 | T | `InitiateTradeTask` — TradeWithPlayer | atomic: `SendOpcode(CMSG_INITIATE_TRADE)` |
| OFFER_GOLD | 12 | A | TradeWithPlayer | `SendOpcode(CMSG_SET_TRADE_GOLD)` |
| OFFER_ITEM | 13 | A | TradeWithPlayer | `SendOpcode(CMSG_SET_TRADE_ITEM)` |
| ACCEPT_TRADE | 14 | A | TradeWithPlayer | `SendOpcode(CMSG_ACCEPT_TRADE)` |
| DECLINE_TRADE | 15 | A | TradeWithPlayer | `SendOpcode(CMSG_CANCEL_TRADE)` |
| ENCHANT_TRADE | 16 | T | `EnchantTradeItemTask` — TradeWithPlayer | composes cast-on-trade-slot |
| LOCKPICK_TRADE | 17 | T | `LockpickTradeItemTask` — TradeWithPlayer | |
| PROMOTE_LEADER | 18 | A | ManageRaid, FormParty | `SendOpcode(CMSG_GROUP_SET_LEADER)` |
| PROMOTE_ASSISTANT | 19 | A | ManageRaid | `SendOpcode(CMSG_GROUP_ASSISTANT_LEADER)` |
| PROMOTE_LOOT_MANAGER | 20 | A | ManageRaid, DistributeLoot | `SendOpcode(CMSG_GROUP_SET_LEADER/loot)` |
| SET_GROUP_LOOT | 21 | A | DistributeLoot | `SendOpcode(CMSG_LOOT_METHOD)` |
| ASSIGN_LOOT | 22 | A | DistributeLoot | `SendOpcode(CMSG_LOOT_MASTER_GIVE)` |
| LOOT_ROLL_NEED | 23 | A | DistributeLoot | `SendOpcode(CMSG_LOOT_ROLL=need)` |
| LOOT_ROLL_GREED | 24 | A | DistributeLoot | `SendOpcode(CMSG_LOOT_ROLL=greed)` |
| LOOT_PASS | 25 | A | DistributeLoot | `SendOpcode(CMSG_LOOT_ROLL=pass)` |
| SEND_GROUP_INVITE | 26 | T | `InviteToPartyTask` — FormParty | atomic: `SendOpcode(CMSG_GROUP_INVITE)` |
| ACCEPT_GROUP_INVITE | 27 | A | FormParty | `SendOpcode(CMSG_GROUP_ACCEPT)` |
| DECLINE_GROUP_INVITE | 28 | A | FormParty | `SendOpcode(CMSG_GROUP_DECLINE)` |
| KICK_PLAYER | 29 | A | ManageRaid, FormParty | `SendOpcode(CMSG_GROUP_UNINVITE)` |
| LEAVE_GROUP | 30 | A | FormParty | `SendOpcode(CMSG_GROUP_DISBAND)` |
| DISBAND_GROUP | 31 | A | ManageRaid, FormParty | `SendOpcode(CMSG_GROUP_DISBAND)` |
| START_MELEE_ATTACK | 32 | T | `StartAttackTask` — KillTarget, ClearArea | atomic: `SendOpcode(CMSG_ATTACKSWING)` |
| START_RANGED_ATTACK | 33 | T | `StartRangedAttackTask` — KillTarget, ClearArea | |
| START_WAND_ATTACK | 34 | T | `StartWandAttackTask` — KillTarget, ClearArea | |
| STOP_ATTACK | 35 | A | KillTarget, ClearArea | `SendOpcode(CMSG_ATTACKSTOP)` |
| CAST_SPELL | 36 | T | `CastSpellTask` — KillTarget, ClearArea, UseItemOnTarget, RecoverFromDeath | atomic: `SendOpcode(CMSG_CAST_SPELL)` |
| STOP_CAST | 37 | A | KillTarget, ClearArea | `SendOpcode(CMSG_CANCEL_CAST)` |
| USE_ITEM | 38 | T | `UseItemTask` — UseItemOnTarget, AcquireItem | atomic: `SendOpcode(CMSG_USE_ITEM)` |
| EQUIP_ITEM | 39 | T | `EquipItemTask` — ApplyLoadout, RepairAndRestock | atomic: `SendOpcode(CMSG_AUTOEQUIP_ITEM)` |
| UNEQUIP_ITEM | 40 | T | `UnequipItemTask` — ApplyLoadout, ManageInventory | |
| DESTROY_ITEM | 41 | A | ManageInventory | `SendOpcode(CMSG_DESTROYITEM)` |
| MOVE_ITEM | 42 | A | ManageInventory | `SendOpcode(CMSG_SWAP_INV_ITEM)` |
| SPLIT_STACK | 43 | A | ManageInventory | `SendOpcode(CMSG_SPLIT_ITEM)` |
| BUY_ITEM | 44 | T | `BuyItemTask` — RepairAndRestock, AcquireItem | atomic: `SendOpcode(CMSG_BUY_ITEM)` |
| BUYBACK_ITEM | 45 | A | ManageInventory | `SendOpcode(CMSG_BUYBACK_ITEM)` |
| SELL_ITEM | 46 | T | `SellItemTask` — ManageInventory, RepairAndRestock | atomic: `SendOpcode(CMSG_SELL_ITEM)` |
| REPAIR_ITEM | 47 | A | RepairAndRestock | `SendOpcode(CMSG_REPAIR_ITEM, single)` |
| REPAIR_ALL_ITEMS | 48 | A | RepairAndRestock | `SendOpcode(CMSG_REPAIR_ITEM, all)` |
| DISMISS_BUFF | 49 | A | (many — buff hygiene) | `SendOpcode(CMSG_CANCEL_AURA)` |
| RESURRECT | 50 | T | `AcceptResurrectTask` — RecoverFromDeath | atomic: `SendOpcode(CMSG_RESURRECT_RESPONSE)` |
| CRAFT | 51 | T | `CraftTask` — CraftItems | atomic: `SendOpcode(CMSG_CAST_SPELL, tradeskill)` |
| LOGIN | 52 | T | `LoginTask` — BringCharacterOnline | |
| LOGOUT | 53 | T | `LogoutTask` — (StateManager control path, not wire Objective) | |
| CREATE_CHARACTER | 54 | T | `CreateCharacterTask` — BringCharacterOnline | |
| DELETE_CHARACTER | 55 | T | `DeleteCharacterTask` — (control path) | |
| ENTER_WORLD | 56 | T | `EnterWorldTask` — BringCharacterOnline | |
| LOOT_CORPSE | 57 | T | `LootCorpseTask` — AcquireItem, ClearArea, KillTarget | |
| RELEASE_CORPSE | 58 | T | `ReleaseCorpseTask` — RecoverFromDeath | atomic: `SendOpcode(CMSG_REPOP_REQUEST)` |
| RETRIEVE_CORPSE | 59 | T | `RetrieveCorpseTask` — RecoverFromDeath | atomic: `SendOpcode(CMSG_RECLAIM_CORPSE)` |
| SKIN_CORPSE | 60 | T | `SkinCorpseTask` — AcquireItem, GatherRoute | |
| GATHER_NODE | 61 | T | `GatherNodeTask` — GatherRoute, AcquireItem | |
| SEND_CHAT | 62 | A | FormParty, social coordination | `SendOpcode(CMSG_MESSAGECHAT)` |
| SET_FACING | 63 | A | (movement — many) | `SendOpcode(MSG_MOVE_SET_FACING)` / `WriteCameraYaw` |
| VISIT_VENDOR | 64 | T | `VisitVendorTask` (GoTo+Interact) — RepairAndRestock, ManageInventory | |
| VISIT_TRAINER | 65 | T | `VisitTrainerTask` — TrainSkills | |
| VISIT_FLIGHT_MASTER | 66 | T | `VisitFlightMasterTask` — TravelTo | |
| START_FISHING | 67 | O | → `FishAtSpot` (9) | |
| START_GATHERING_ROUTE | 68 | O | → `GatherRoute` (10) | |
| CHECK_MAIL | 69 | T | `CheckMailTask` — ManageInventory | |
| START_PHYSICS_RECORDING | 70 | C | diagnostic control (separate message) | NOT an Objective |
| STOP_PHYSICS_RECORDING | 71 | C | diagnostic control (separate message) | NOT an Objective |
| START_DUNGEONEERING | 72 | O | → `RunDungeon` (20) | |
| CONVERT_TO_RAID | 73 | A | ManageRaid | `SendOpcode(CMSG_GROUP_RAID_CONVERT)` |
| CHANGE_RAID_SUBGROUP | 74 | A | ManageRaid | `SendOpcode(CMSG_GROUP_CHANGE_SUB_GROUP)` |
| FOLLOW_TARGET | 75 | T | `FollowTask` — FollowEscort, KillTarget | |
| JOIN_BATTLEGROUND | 76 | O | → `PvpJoinQueue` (19) | |
| ACCEPT_BATTLEGROUND | 77 | A | PvpJoinQueue | `SendOpcode(CMSG_BATTLEFIELD_PORT, accept)` |
| LEAVE_BATTLEGROUND | 78 | A | PvpJoinQueue | `SendOpcode(CMSG_LEAVE_BATTLEFIELD)` |
| TRAVEL_TO | 79 | O | → `TravelTo` (1) | the one already-correct Objective |
| APPLY_LOADOUT | 80 | O | → `ApplyLoadout` (22) | P3 loadout hand-off |
| JUMP | 81 | A | (movement) | `SendOpcode(MSG_MOVE_JUMP)` / `PressKey(Space)` |
| START_MOVEMENT | 82 | A | (movement) | `WriteMovementBit(controlBits, true)` |
| STOP_MOVEMENT | 83 | A | (movement) | `WriteMovementBit(controlBits, false)` |

**Tally:** 7 → Objective · ~40 → Task · ~35 → Action · 2 → Control. Only `TRAVEL_TO` was already a real
Objective; the new enum adds ~19 genuinely high-level Objectives (KillTarget, ClearArea, AcquireItem,
TurnInQuest, FormParty, RecoverFromDeath, …) that the legacy op-enum never had.

## 3. Consequences for the other registries

- **`TASK_CONTRACTS.md`** (to be authored): the ~40 **T** rows above become Task contracts. Their `.cs` files
  already exist under `Exports/BotRunner/Tasks/` — the registry transcribes their precondition / ordered-Actions
  / verification / failure-handling from code.
- **`ACTION_TAXONOMY.md`** (to be authored): the ~35 **A** rows (plus the atomic opcodes named in the "Atomic
  Action" column of the **T** rows) become the closed atomic-Action table.
- **`CharacterAction` (C# enum) stays** as the BotRunner-internal execution vocabulary — but it is **no longer
  1:1 with the wire `ObjectiveType`**. `MapProtoObjectiveType` is replaced by per-Objective behavior-tree
  decomposition (see migration plan).
- **`ObjectiveType` namespace collision resolved:** the wire enum is redefined to §1's Activity-scoped set;
  the op-level names move into `CharacterAction` / Tasks / Actions, which already exist.

See [OBJECTIVE_TYPE_MIGRATION_PLAN.md](OBJECTIVE_TYPE_MIGRATION_PLAN.md) for the sequenced 814-reference code change.
