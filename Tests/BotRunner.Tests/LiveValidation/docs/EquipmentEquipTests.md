# EquipmentEquipTests

Tests equipping a weapon from inventory to the mainhand equipment slot.

## Bot Execution Mode

**Dual-Bot Conditional** — Both bots run equip scenarios in parallel. FG gated on IsFgActionable. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Test Methods (1)

### EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()` (revive + safe zone + GM on).

**Test Flow:**

| Step | Action | Details |
|------|--------|---------|
| 0 | Check mainhand | Read slot 15 from snapshot. If occupied: `.reset items {charName}`, wait 1.5s |
| 1 | Learn mace skill | Check SpellList for 198 (One-Hand Maces). If missing: `BotLearnSpellAsync(198)` + `BotSetSkillAsync(54, 1, 300)`. Poll 5s. |
| 2 | Ensure weapon in bags | Count item 36 in BagContents. If none + bags >= 15: `BotClearInventoryAsync()`. If still none: `BotAddItemAsync(36)`, poll 5s. |
| 3 | Equip weapon | **Dispatch `ActionType.EquipItem`** with `IntParam = 36` |
| 4 | Verify equip | Poll 3s: mainhand slot GUID changed or bag count dropped |

**StateManager/BotRunner Action Flow:**

**EquipItem dispatch chain:**
1. ActionMessage with `ActionType.EquipItem`, `IntParam=36`
2. `BuildEquipItemByIdSequence(36)` in BotRunnerService
3. Sequence: scan BagContents for itemId 36 → resolve (bag, slot) → `_objectManager.EquipItem(bag, slot)`
4. FG: Lua `PickupContainerItem(bag, slot)` then `AutoEquipCursorItem()` via ThreadSynchronizer main thread calls
5. BG: CMSG_AUTOEQUIP_ITEM packet with bag=0xFF, slot=absolute slot index

**Key IDs:**
- Item 36 = Worn Mace
- Spell 198 = One-Hand Maces proficiency
- Skill 54 = Maces
- Mainhand equipment slot = 15 (snapshot BagContents key)

**GM Commands:** `.reset items {charName}` (clear equipment if mainhand occupied).

**Assertions:** Mainhand slot filled after equip. Dispatch returns Success.
