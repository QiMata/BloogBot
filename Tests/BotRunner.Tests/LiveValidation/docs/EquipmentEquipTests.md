# EquipmentEquipTests

Tests equipping a staged weapon from bags to the mainhand equipment slot.

## Bot Execution Mode

**Dual-Bot Conditional** - Both bots run equip scenarios in parallel. FG is
gated on `IsFgActionable`. The launch roster is `Equipment.config.json`:
`EQUIPFG1` Foreground Orc Warrior, `EQUIPBG1` Background Orc Warrior, and
`SHODAN` Background Gnome Mage test director.

This test follows the Shodan migration shape. The test body issues no GM
commands. Per-role loadout setup is encapsulated in
`StageBotRunnerLoadoutAsync`.

## Test Methods (1)

### EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot

**Bots:** BG (`EQUIPBG1`) + FG (`EQUIPFG1`, when actionable) + SHODAN (test director)

**Fixture Setup:** `EnsureSettingsAsync(Equipment.config.json)`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Stage loadout | `StageBotRunnerLoadoutAsync(account, label, spells=[198], skills=[(54,1,300)], items=[(36,1)])` - clean slate, clear bag 0, learn mace proficiency, set Maces skill, add Worn Mace. |
| 1 | Wait for bag | Poll for Worn Mace (`36`) in bag snapshot contents. |
| 2 | Record baseline | Snapshot mainhand slot (`15`) and Worn Mace count in bags. |
| 3 | Equip weapon | Dispatch `ActionType.EquipItem` with `IntParam = 36`. |
| 4 | Verify equip | Poll until mainhand is filled and either the mainhand GUID changed or the Worn Mace bag count dropped. |

**StateManager/BotRunner Action Flow:**

`ActionType.EquipItem` -> `BuildEquipItemByIdSequence(36)` -> find item 36 in
bags -> `_objectManager.EquipItem(bag, slot)`.

FG path: Lua `PickupContainerItem(bag, slot)` + `AutoEquipCursorItem()`.

BG path: `CMSG_AUTOEQUIP_ITEM` packet.

**Key IDs:**

| ID | Meaning |
|----|---------|
| 36 | Worn Mace |
| 198 | One-Handed Maces spell |
| 54 | Maces skill |
| 15 | Mainhand snapshot inventory slot |

**GM Commands in test body:** None. `StageBotRunnerLoadoutAsync` sends
`.learn` / `.setskill` / `.additem` from SHODAN after the director selects the
FG/BG player with BotRunner's internal `.targetguid <guid>` helper. The
selected-target command sequence is fixture-owned and serialized for parallel
FG/BG staging.
