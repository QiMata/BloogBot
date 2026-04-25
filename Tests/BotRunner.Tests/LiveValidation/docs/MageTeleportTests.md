# MageTeleportTests

Tests dispatching `ActionType.CastSpell` for the mage city-teleport spells
(`Teleport: Orgrimmar`, etc.) and asserting on snapshot position arrival.

## Bot Execution Mode

**Dual-Bot Conditional** — FG runs first when actionable, then BG runs against
a fresh stage. The launch roster is `MageTeleport.config.json`: `TRMAF5`
Foreground Troll Mage, `TRMAB5` Background Troll Mage, and `SHODAN`
Background Gnome Mage test director.

This test follows the Shodan migration shape. The test body issues no GM
commands. Per-role loadout setup is encapsulated in
`StageBotRunnerLoadoutAsync`, and the Razor Hill staging teleport is
encapsulated in `StageBotRunnerAtRazorHillAsync`.

## Test Methods (4)

### MageTeleport_Horde_OrgrimmarArrival

**Bots:** BG (`TRMAB5`) + SHODAN (test director). FG (`TRMAF5`) is launched
for topology parity but **does not receive the action dispatch**.
`ActionType.CastSpell` resolves to `_objectManager.CastSpell(int spellId)`,
which is a documented no-op on Foreground (only the `CastSpellByName(string)`
Lua overload casts there). BG sends `CMSG_CAST_SPELL` directly and is
observable via the snapshot.

**Known limitation (pre-existing):** Even after `StageBotRunnerLoadoutAsync`
levels TRMAB5 to 20 and adds Rune of Teleportation, MaNGOS responds with
`SMSG_SPELL_FAILURE` for spell 3567 (initially `NO_POWER`, and a generic
short-payload failure after the level bump). The migration ships the
correct Shodan/FG/BG shape, but resolving the underlying cast rejection is
tracked as a follow-up — likely a mana refresh, GCD, or talent prerequisite
that needs separate inspection.

**Fixture Setup:** `EnsureSettingsAsync(MageTeleport.config.json)`.

**Test Flow (BG only):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Stage loadout | `StageBotRunnerLoadoutAsync(bgAccount, "BG", spells=[3567], items=[(17031,5)])` — clean slate, clear bag 0, learn Teleport: Orgrimmar, add Rune of Teleportation x5. |
| 1 | Wait for spell | Poll 8s for spell `3567` in `SpellList`. |
| 2 | Wait for reagent | Poll 8s for item `17031` in bags. |
| 3 | Stage at Razor Hill | `StageBotRunnerAtRazorHillAsync(bgAccount, "BG")` teleports BG to Razor Hill (Durotar) so the Org arrival delta is unambiguous. |
| 4 | Cast teleport | Dispatch `ActionType.CastSpell` with `IntParam = 3567`. Assert Success. |
| 5 | Verify arrival | Poll 20s (10s cast + 10s buffer) for player position within 50yd of `(1676, -4315)` on map 1 (Orgrimmar landing in `MageTeleportData[3567]`). |

### MageTeleport_Alliance_StormwindArrival

**Bots:** BG (`TRMAB5`) + SHODAN (test director)

Skips when the configured roster is Horde-only (the default for this fixture).
When run against an Alliance roster, the BG target receives Teleport:
Stormwind (3561) plus the Rune of Teleportation reagent through
`StageBotRunnerLoadoutAsync`, then dispatches `ActionType.CastSpell` and
asserts a position change within 15s.

### MagePortal_PartyTeleported

**Bots:** BG (`TRMAB5`) + SHODAN (test director)

Placeholder migration. Stages Portal: Orgrimmar (11417) on the BG target
through `StageBotRunnerLoadoutAsync` and asserts the spell appears in the
BG snapshot's `SpellList`. A future migration can extend this into a
multi-bot party portal flow.

### MageAllCityTeleports

**Bots:** BG (`TRMAB5`) + SHODAN (test director)

Stages all 6 city teleport spells (3567, 3563, 3566, 3561, 3562, 3565) on
the BG target through `StageBotRunnerLoadoutAsync` and asserts every spell
appears in the BG snapshot's `SpellList`.

## Key IDs

| ID | Meaning |
|----|---------|
| 3567 | Teleport: Orgrimmar |
| 3563 | Teleport: Undercity |
| 3566 | Teleport: Thunder Bluff |
| 3561 | Teleport: Stormwind |
| 3562 | Teleport: Ironforge |
| 3565 | Teleport: Darnassus |
| 11417 | Portal: Orgrimmar |
| 17031 | Rune of Teleportation reagent |

## StateManager/BotRunner Action Flow

`ActionType.CastSpell` dispatches the spell id to the BotRunner. FG path:
Lua `CastSpellByName(spellName)`. BG path: `CMSG_CAST_SPELL` with the spell
id. Self-teleport spells consume one Rune of Teleportation per cast and
move the player to the landing coordinates baked into
`MageTeleportData.cs`.

## GM Commands in test body

None. `StageBotRunnerLoadoutAsync` still routes the target-specific
`.learn` / `.additem` commands through the target bot's chat layer because
MaNGOS resolves those commands against the sender's own character.
`StageBotRunnerAtRazorHillAsync` keeps the `.go xyz` self-teleport inside
the fixture for the same reason. Both constraints are tracked by the
Shodan cross-targeting follow-up in
[SHODAN_MIGRATION_INVENTORY.md](SHODAN_MIGRATION_INVENTORY.md).
