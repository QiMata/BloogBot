# WandAttackTests

Tests equipping a wand and dispatching `ActionType.StartWandAttack` against a
natural Durotar creature target.

## Bot Execution Mode

**Dual-Bot Conditional** - FG runs first when actionable, then the test
restages and runs BG. The launch roster is `Wand.config.json`: `TRMAF5`
Foreground Troll Mage, `TRMAB5` Background Troll Mage, and `SHODAN`
Background Gnome Mage test director.

This test follows the Shodan migration shape. The test body issues no GM
commands. Per-role loadout setup is encapsulated in
`StageBotRunnerLoadoutAsync`, and the arbitrary-coordinate mob-area teleport is
encapsulated in `StageBotRunnerAtDurotarMobAreaAsync`.

## Test Methods (1)

### Wand_ShootTarget_DealsDamage

**Bots:** FG (`TRMAF5`, when actionable) + BG (`TRMAB5`) + SHODAN (test director)

**Fixture Setup:** `EnsureSettingsAsync(Wand.config.json)`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Stage loadout | `StageBotRunnerLoadoutAsync(account, label, spells=[5009,5019], skills=[(228,1,300)], items=[(5240,1)])` - clean slate, clear bag 0, learn Wands + Shoot, set Wands skill, add Torchlight Wand. |
| 1 | Equip wand | Dispatch `ActionType.EquipItem` with `IntParam = 5240`; poll ranged slot `17`. |
| 2 | Stage mob area | `StageBotRunnerAtDurotarMobAreaAsync(account, label)` teleports the target to the Valley of Trials creature cluster and waits for nearby units. |
| 3 | Select target | Pick the nearest living low-level creature within wand range from `NearbyUnits`. |
| 4 | Start wand attack | Dispatch `ActionType.StartWandAttack` with the target GUID as `LongParam`. |
| 5 | Verify engagement | Poll for player combat state or target health drop/gone. |
| 6 | Cleanup action | Dispatch `ActionType.StopAttack`. |

**StateManager/BotRunner Action Flow:**

`ActionType.StartWandAttack` -> `BuildStartWandAttackSequence(targetGuid)` ->
set target -> stop movement -> face target -> `_objectManager.StartWandAttack()`.

FG path: Lua `CastSpellByName('Shoot')`.

BG path: `CMSG_CAST_SPELL` for Shoot spell `5019`.

**Key IDs:**

| ID | Meaning |
|----|---------|
| 5240 | Torchlight Wand (`required_level=0` in local MaNGOS item data) |
| 5009 | Wands proficiency |
| 5019 | Shoot |
| 228 | Wands skill |
| 17 | Ranged weapon snapshot inventory slot |

**GM Commands in test body:** None. `StageBotRunnerLoadoutAsync` still routes
the target-specific `.learn` / `.setskill` / `.additem` commands through the
target bot's chat layer. `StageBotRunnerAtDurotarMobAreaAsync` also keeps the
`.go xyz` self-teleport inside the fixture because this MaNGOS server does not
provide a SOAP coordinate teleport command for arbitrary online characters.
Both constraints are tracked by the Shodan cross-targeting follow-up.
