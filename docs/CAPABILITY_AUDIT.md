# Bot Capability Audit — 2026-03-07

## ActionType Implementation Status

Every ActionType defined in `communication.proto` is mapped in `BotRunnerService.ActionMapping.cs` and dispatched in `BotRunnerService.ActionDispatch.cs`. However, many BG bot (WoWSharpClient) implementations are stubs.

### Fully Implemented + Tested

| ActionType | BG Bot Status | FG Bot Status | Test File |
|------------|---------------|---------------|-----------|
| GOTO | Working (pathfinding + physics) | Working (CTM disabled, pathfinding) | GatheringProfessionTests |
| INTERACT_WITH | Working (CMSG_GAMEOBJ_USE / NPC_TEXT) | Working (native) | EconomyInteractionTests, NpcInteractionTests |
| START_MELEE_ATTACK | Working (CMSG_ATTACKSWING) | Working (native) | CombatLoopTests, CombatRangeTests |
| START_RANGED_ATTACK | Working (CMSG_ATTACKSWING) | Working (native) | CombatRangeTests |
| STOP_ATTACK | Working (CMSG_ATTACKSTOP) | Working (native) | CombatRangeTests |
| CAST_SPELL | Working (CMSG_CAST_SPELL) | Working (Lua CastSpellByName) | CraftingProfessionTests, FishingProfessionTests |
| USE_ITEM | Working (CMSG_USE_ITEM) | Working (native) | ConsumableUsageTests |
| EQUIP_ITEM | Working (CMSG_AUTOEQUIP_ITEM) | Working (native) | EquipmentEquipTests, FishingProfessionTests |
| DESTROY_ITEM | Working (CMSG_DESTROYITEM) | Working (native) | LiveBotFixture helper |
| SET_FACING | Working (MSG_MOVE_SET_FACING) | Working (native) | FishingProfessionTests, CombatLoopTests |
| RELEASE_CORPSE | Working (CMSG_REPOP_REQUEST) | Working (native) | DeathCorpseRunTests |
| RETRIEVE_CORPSE | Working (CMSG_RECLAIM_CORPSE) | Working (native) | DeathCorpseRunTests |
| GATHER_NODE | Working (CMSG_GAMEOBJ_USE + spell) | Working (native) | GatheringProfessionTests |
| SEND_CHAT | Working (CMSG_MESSAGECHAT) | Working (Lua SendChatMessage) | LiveBotFixture (GM commands) |
| SEND_GROUP_INVITE | Working (CMSG_GROUP_INVITE) | Working (native) | GroupFormationTests |
| ACCEPT_GROUP_INVITE | Working (CMSG_GROUP_ACCEPT) | Working (native) | GroupFormationTests |
| LEAVE_GROUP | Working (CMSG_GROUP_DISBAND) | Working (native) | GroupFormationTests |
| DISBAND_GROUP | Working (CMSG_GROUP_DISBAND) | Working (native) | GroupFormationTests |
| UNEQUIP_ITEM | Working (CMSG_AUTOSTORE_BAG_ITEM via EquipmentAgent) | Working (native) | UnequipItemTests |
| DISMISS_BUFF | Working (CMSG_CANCEL_AURA, BG needs .unaura fallback) | Working (native) | BuffDismissTests |

### Implemented but NOT Tested

| ActionType | BG Bot Status | FG Bot Status | Priority | Notes |
|------------|---------------|---------------|----------|-------|
| WAIT | Working (timer) | Working | LOW | Trivial |
| SELECT_GOSSIP | Working (gossip frame) | Working | MEDIUM | Needs gossip NPC interaction |
| LOOT_CORPSE | Working (CMSG_LOOT via LootingAgent) | Working (native) | HIGH | Core farming loop |
| SKIN_CORPSE | Working (CMSG_LOOT) | Working (native) | MEDIUM | Skinning profession |
| DECLINE_GROUP_INVITE | Working (CMSG_GROUP_DECLINE) | Working | LOW | |
| KICK_PLAYER | Working (CMSG_GROUP_UNINVITE_GUID) | Working | LOW | |
| STOP_CAST | Working | Working | LOW | |

### Stubbed / Not Working on BG Bot

| ActionType | BG Bot Issue | FG Bot Status | Priority | Fix Required |
|------------|-------------|---------------|----------|--------------|
| UNEQUIP_ITEM | **FIXED + TESTED** — delegates to `EquipmentAgent.UnequipItemAsync()` | Working (native) | ~~MEDIUM~~ | CMSG_AUTOSTORE_BAG_ITEM |
| BUY_ITEM | **FIXED** — `BuyItemFromVendorAsync` via VendorAgent (with vendorGuid param) | Working (native) | ~~HIGH~~ | Legacy MerchantFrame path still null |
| BUYBACK_ITEM | **MerchantFrame is null** (no async bypass yet) | Working (native) | LOW | Rarely used |
| SELL_ITEM | **FIXED** — `SellItemToVendorAsync` via VendorAgent (with vendorGuid param) | Working (native) | ~~HIGH~~ | Legacy MerchantFrame path still null |
| REPAIR_ITEM | **MerchantFrame is null** (single-slot repair rarely used) | Working (native) | LOW | Use RepairAll instead |
| REPAIR_ALL_ITEMS | **FIXED** — `RepairAllItemsAsync` via VendorAgent (with vendorGuid param) | Working (native) | ~~MEDIUM~~ | Legacy MerchantFrame path still null |
| ACCEPT_QUEST | **FIXED** — `AcceptQuestFromNpcAsync` via QuestAgent (with npcGuid param) | Working (native) | ~~MEDIUM~~ | Legacy QuestFrame path still null |
| DECLINE_QUEST | Depends on QuestFrame | Working (native) | LOW | |
| SELECT_REWARD | Depends on QuestFrame | Working (native) | LOW | Use TurnInQuestAsync rewardIndex |
| COMPLETE_QUEST | **FIXED** — `TurnInQuestAsync` via QuestAgent (with npcGuid param) | Working (native) | ~~MEDIUM~~ | Legacy QuestFrame path still null |
| TRAIN_SKILL | Depends on TrainerFrame | Working (native) | MEDIUM | TrainerFrame needs SMSG_TRAINER_LIST handler |
| TRAIN_TALENT | Depends on TalentFrame | Working (native) | LOW | |
| SELECT_TAXI_NODE | Depends on TaxiFrame | Working (native) | LOW | |
| MOVE_ITEM | Working (CMSG_SWAP_ITEM) | Working | LOW | |
| SPLIT_STACK | Working (CMSG_SPLIT_ITEM) | Working | LOW | |
| OFFER_TRADE..LOCKPICK_TRADE | Unknown | Working | LOW | Player-to-player trading |
| PROMOTE_LEADER..ASSIGN_LOOT | Unknown | Working | LOW | Group management |
| LOOT_ROLL_NEED..LOOT_PASS | Unknown | Working | LOW | Loot rolling |
| RESURRECT | Working (CMSG_RESURRECT_RESPONSE) | Working | LOW | Rarely tested scenario |
| CRAFT | Depends on CraftFrame | Working | LOW | |
| LOGIN/LOGOUT/CREATE/DELETE/ENTER | Working (login sequence) | Working | LOW | Tested implicitly via fixture |

## Critical Gaps (Resolved)

### GAP-1: MerchantFrame (BUY/SELL/REPAIR) — FIXED (session 18)
**Impact:** BG bot can now buy, sell, and repair via packet-based async methods.
**Fix:** Added `BuyItemFromVendorAsync`, `SellItemToVendorAsync`, `RepairAllItemsAsync` to IObjectManager + WoWSharpObjectManager, routing through `VendorAgent` via `_agentFactoryAccessor`. ActionDispatch routes to async path when vendorGuid param is provided. Legacy MerchantFrame property remains null (FG uses Lua).

### GAP-2: UnequipItem — FIXED (session 18)
**Impact:** BG bot can now unequip items.
**Fix:** `WoWSharpObjectManager.UnequipItem()` delegates to `EquipmentAgent.UnequipItemAsync()` (CMSG_AUTOSTORE_BAG_ITEM). Maps `EquipSlot` → `EquipmentSlot` (offset -1).

### GAP-3: TrainerFrame — Low Priority
**Impact:** BG bot cannot train spells via ActionType.TrainSkill.
**Status:** `LearnAllAvailableSpellsAsync` already bypasses TrainerFrame via TrainerAgent. ActionType.TrainSkill legacy path still depends on null TrainerFrame. Tests use GM `.learn` commands.

## Test Coverage Summary

| Category | Tests | Pass Rate | Notes |
|----------|-------|-----------|-------|
| Basic Loop | 6 | 6/6 | Login, snapshot, teleport, level, units |
| Character Lifecycle | 4 | 4/4 | Create, items, death/revive |
| Combat | 9 | 9/9 | Melee, ranged, stop, distance, bounding |
| Consumables | 1 | 1/1 | Use item + buff check |
| Crafting | 1 | 1/1 | First aid learn + craft |
| Death/Corpse | 1 | 0-1/1 | FG intermittently stuck during ghost run |
| Economy | 3 | 3/3 | Bank, AH, mail (interact only, no buy/sell) |
| Equipment | 1 | 1/1 | Add + equip |
| Fishing | 1 | 1/1 | Full fishing loop |
| Gathering | 2 | 0-2/2 | Mining/herbalism (spawn timer dependent) |
| Group | 1 | 0-1/1 | Invite + accept + cleanup |
| NPC Interaction | 6 | 6/6 | Vendor, trainer, flight master, NPC flags |
| Ground Z | 2 | 2/2 | Orgrimmar elevation analysis |
| Quest | 1 | 1/1 | Add, complete, remove |
| Talent | 1 | 1/1 | Learn via GM, spell in snapshot |
| Loot Corpse | 1 | 1/1 | Kill → loot → verify inventory |
| Navigation | 2 | 2/2 | Short + city path with GOTO |
| Starter Quest | 1 | 0-1/1 | Accept + turn-in (intermittent in suite) |
| Vendor Buy/Sell | 2 | 2/2 | Buy Weak Flux + sell Linen Cloth via packets |
| Spell Cast | 1 | 1/1 | Heroic Strike on mob, verify health decrease |
| Unequip Item | 1 | 1/1 | Equip → unequip mainhand → verify slot empty |
| Buff Dismiss | 1 | 1/1 | Apply buff → dismiss → verify aura removed |
| **Total** | **49** | **46-49/49** | CombatLoop, Mining, StarterQuest intermittent |

## Recommended New Tests (Priority Order)

1. ~~**VendorBuySellTests**~~ — **DONE** (session 19): Buy + Sell via CMSG_LIST_INVENTORY + CMSG_BUY_ITEM + CMSG_SELL_ITEM
2. ~~**SpellCastOnTargetTests**~~ — **DONE** (session 20): Heroic Strike on mob, verify damage
3. ~~**UnequipItemTests**~~ — **DONE** (session 20): Equip → unequip → verify slot empty
4. ~~**BuffDismissTests**~~ — **DONE** (session 20): Apply buff → dismiss (ActionType + .unaura fallback) → verify removal
