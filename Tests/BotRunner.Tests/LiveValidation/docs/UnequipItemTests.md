# UnequipItemTests

Tests unequipping a weapon from mainhand slot — verifies it moves back to bags.

## Bot Execution Mode

**Dual-Bot Conditional** — Both bots run unequip scenarios in parallel. FG gated on IsFgActionable. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This is the first migrated slice of the Shodan test-director overhaul (see
[SHODAN_MIGRATION_INVENTORY.md](SHODAN_MIGRATION_INVENTORY.md)). The test
launches `Equipment.config.json` (TESTBOT1 + TESTBOT2 + SHODAN), stages
the BotRunner under test through `StageBotRunnerLoadoutAsync`, and then
dispatches only `ActionType.EquipItem` and `ActionType.UnequipItem`.
The test body issues no GM commands.

## Test Methods (1)

### UnequipItem_MainhandWeapon_MovesToBags

**Bots:** BG (TESTBOT2) + FG (TESTBOT1) + SHODAN (test director)

**Fixture Setup:** `EnsureSettingsAsync(Equipment.config.json)`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Stage loadout | `StageBotRunnerLoadoutAsync(account, label, spells=[198], skills=[(54,1,300)], items=[(36,1)])` — clean slate, clear bag 0, learn mace proficiency, set Maces skill, add Worn Mace. |
| 1 | Wait for bag | Poll 5s for Worn Mace appearance in bags. |
| 2 | Equip weapon | **Dispatch `ActionType.EquipItem`** with `IntParam = 36`. Assert Success. Poll 5s for mainhand equipped. |
| 3 | Record state | Snapshot: `mainhandGuidBefore` (slot 15), `maceCountBefore` (item 36 in bags) |
| 4 | Unequip | **Dispatch `ActionType.UnequipItem`** with `IntParam = 16` (MainhandEquipSlot enum) |
| 5 | Verify | Poll 5s (200ms interval): check mainhand GUID changed or empty, mace count increased in bags |

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

**GM Commands in test body:** None. All GM staging is encapsulated by
`StageBotRunnerLoadoutAsync` (Shodan test-director helper). The helper
internally still routes `.learn` / `.setskill` / `.additem` through the
target bot's chat layer because MaNGOS resolves those commands against
the sender's own character; a follow-up pass can switch to Shodan
cross-targeting or SOAP name-targeted variants.

**Assertions:** Mainhand filled after equip. Mainhand empty after unequip. Mace appears in bags after unequip.
