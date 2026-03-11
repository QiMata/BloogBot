# UnequipItemTests

Tests unequipping a weapon from mainhand slot — verifies it moves back to bags.

## Test Methods (1)

### UnequipItem_MainhandWeapon_MovesToBags

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Clear inventory | `BotClearInventoryAsync(includeExtraBags=false)` — preserve equipped gear. Wait 1s. |
| 1 | Learn mace skill | `BotLearnSpellAsync(198)` + `BotSetSkillAsync(54, 1, 300)`. Wait 500ms. |
| 2 | Add weapon | `BotAddItemAsync(36)` — Worn Mace. Poll 5s for bag appearance. |
| 3 | Equip weapon | **Dispatch `ActionType.EquipItem`** with `IntParam = 36`. Assert Success. Poll 5s for mainhand equipped. |
| 4 | Record state | Snapshot: `mainhandGuidBefore` (slot 15), `maceCountBefore` (item 36 in bags) |
| 5 | Unequip | **Dispatch `ActionType.UnequipItem`** with `IntParam = 16` (MainhandEquipSlot enum) |
| 6 | Verify | Poll 5s (200ms interval): check mainhand GUID changed or empty, mace count increased in bags |

**StateManager/BotRunner Action Flow:**

**EquipItem:** `BuildEquipItemByIdSequence(36)` → find item 36 in bags → `_objectManager.EquipItem(bag, slot)`:
- FG: Lua `PickupContainerItem(bag, slot)` + `AutoEquipCursorItem()`
- BG: CMSG_AUTOEQUIP_ITEM packet

**UnequipItem:** `BuildUnequipItemSequence(16)` → `_objectManager.UnequipItem(equipSlot)`:
- FG: Lua `PickupInventoryItem(16)` + `PutItemInBackpack()`
- BG: CMSG_AUTOSTORE_EQUIP_ITEM packet (moves equipped item to first free bag slot)

**Slot Mapping:**
| Context | Value | Meaning |
|---------|-------|---------|
| BagContents key | 15 | Mainhand equipment slot (snapshot) |
| UnequipItem IntParam | 16 | MainhandEquipSlot enum value |
| Backpack slots | 23-38 | INVENTORY_SLOT_ITEM_START = 23 |

**Key IDs:** Item 36 = Worn Mace. Spell 198 = One-Hand Maces. Skill 54 = Maces.

**GM Commands:** None (all via ActionType dispatch).

**Assertions:** Mainhand filled after equip. Mainhand empty after unequip. Mace appears in bags after unequip.
