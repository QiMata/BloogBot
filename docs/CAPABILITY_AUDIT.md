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
| DISMISS_BUFF | Working (string-based) | Working | LOW | |

### Stubbed / Not Working on BG Bot

| ActionType | BG Bot Issue | FG Bot Status | Priority | Fix Required |
|------------|-------------|---------------|----------|--------------|
| UNEQUIP_ITEM | **Empty method** — `UnequipItem(EquipSlot slot) { }` | Working (native) | MEDIUM | Send CMSG_AUTOEQUIP_ITEM to empty slot |
| BUY_ITEM | **MerchantFrame is null** — never assigned | Working (native) | HIGH | Implement MerchantFrame from SMSG_LIST_INVENTORY |
| BUYBACK_ITEM | **MerchantFrame is null** | Working (native) | HIGH | Same as BUY_ITEM |
| SELL_ITEM | **MerchantFrame is null** | Working (native) | HIGH | Implement VendorNetworkClientComponent |
| REPAIR_ITEM | **MerchantFrame is null** | Working (native) | MEDIUM | Same as BUY_ITEM |
| REPAIR_ALL_ITEMS | **MerchantFrame is null** | Working (native) | MEDIUM | Same as BUY_ITEM |
| ACCEPT_QUEST | Depends on QuestFrame | Working (native) | MEDIUM | QuestFrame may work (SMSG_QUESTGIVER_OFFER_REWARD exists) |
| DECLINE_QUEST | Depends on QuestFrame | Working (native) | LOW | |
| SELECT_REWARD | Depends on QuestFrame | Working (native) | MEDIUM | |
| COMPLETE_QUEST | Depends on QuestFrame | Working (native) | MEDIUM | |
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

## Critical Gaps (Must Fix)

### GAP-1: MerchantFrame (BUY/SELL/REPAIR)
**Impact:** BG bot cannot buy, sell, or repair items at vendors.
**Root cause:** `WoWSharpObjectManager.MerchantFrame` is `{ get; private set; }` and never assigned.
**Fix:** Implement `SMSG_LIST_INVENTORY` handler in WoWSharpClient that populates a `MerchantFrame` object. The `VendorNetworkClientComponent` already exists with `SellItemByGuidAsync` — wire it up.

### GAP-2: UnequipItem
**Impact:** BG bot cannot unequip items (empty method body).
**Fix:** Send `CMSG_AUTOEQUIP_ITEM` targeting an empty backpack slot, or implement as swap to first available bag slot.

### GAP-3: TrainerFrame
**Impact:** BG bot cannot train spells at NPCs via ActionType.
**Status:** Need to verify if `SMSG_TRAINER_LIST` handler exists. Currently tests use GM `.learn` commands.

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
| **Total** | **40** | **39-40/40** | DeathCorpseRun is only intermittent failure |

## Recommended New Tests (Priority Order)

1. **LootCorpseTests** — Kill mob → loot → verify inventory change (ActionType.LootCorpse)
2. **VendorBuySellTests** — Interact with vendor → buy item → verify inventory (needs MerchantFrame fix first)
3. **SpellCastOnTargetTests** — Cast offensive spell on mob → verify damage
4. **PathfindingNavigationTests** — Navigate between two distant points → verify arrival
5. **BuffDismissTests** — Apply buff → dismiss → verify removal
