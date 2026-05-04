# Consumables — 1.12.1 Raid + Dungeon Reference

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/guide/classic-wow-consumables-list-for-each-class
> - https://www.icy-veins.com/wow-classic/warrior-dps-pve-enchants-consumables
> - https://www.icy-veins.com/wow-classic/hunter-dps-pve-enchants-consumables
> - https://forum.nostalrius.org/viewtopic.php?f=24&t=23175 (referenced via search)
> - https://www.method.gg/wow-classic/list-of-all-wow-classic-consumables-enchants-and-world-buffs (referenced via search)
>
> **Pass 2.** Some details (exact heal amounts on Major Healing/Mana Pots, juju stack rules) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Overview

Consumables are the **per-fight or per-pull buffs** that stack with world buffs and class buffs. A raid Warrior in 1.12 going into a Patchwerk-style fight runs 8-12 consumables stacked simultaneously. Total consumable spend per Warrior per raid: **30-80g** depending on tier (more for Naxx-era progression than for MC).

**1.12.1 stacking rules**:
- **Flask**: 1 active at a time (any flask replaces another)
- **Battle Elixir**: 1 active at a time (Mongoose, Greater Agility, Brute Force, Sages, Giants, etc. — all share slot)
- **Guardian Elixir**: 1 active at a time (Greater Defense, Fortitude, Superior Defense — all share slot)
- **Juju**: All 6 jujus can stack with each other AND with Battle/Guardian Elixirs (the trick is Juju buffs are short — 30 min)
- **Food buff**: 1 active (Tender Wolf Steak / Smoked Sagefish / etc.)
- **Drink buff**: 1 active (Conjured Water / Sweet Nectar)
- **Sharpening Stones / Weightstones**: 1 active per weapon (so DW = 2)
- **Scrolls of <stat>**: count as Battle Elixir slot in 1.12.1 (so they don't stack with Mongoose) — this is unusual; **Classic 2019 changed this to allow scrolls + elixirs to stack**

Engine must respect these slots when planning consumable stacks.

## Flasks (3-hour duration, persist through death)

Best raid stats; expensive to craft (each requires Black Lotus = ~50-150g per flask):

| Flask | Effect | Best for |
|---|---|---|
| **Flask of the Titans** | +1200 health, 3h | Tanks (especially MTs on hard-hitting bosses) |
| **Flask of Distilled Wisdom** | +65 INT, 3h | Healers (mana pool boost) |
| **Flask of Supreme Power** | +150 spell damage and healing, 3h | Caster DPS (Mage/Warlock/Boomkin/Shadow) |
| **Flask of the Mongoose** | +25 AGI, +2% melee crit, 3h | **Best DPS flask** — Melee DPS (Warrior/Rogue/Enh/Hunter) |

**Cost note**: Flasks require **Black Lotus** as a reagent. Black Lotus spawns are rare-respawn nodes in Burning Steppes / EPL / Silithus / Winterspring (~30-60 min respawn, contested). On VMaNGOS, BL spawn rate is generally faithful to 1.12.1 retail.

## Major Potions (instant use, 2-minute shared CD)

| Potion | Effect | Use case |
|---|---|---|
| **Major Healing Potion** | Heals 1050-1750 HP `[verify pass 3]` | Universal panic-button |
| **Major Mana Potion** | Restores 1350-2250 mana `[verify]` | Healers / casters during long fights |
| **Major Rejuvenation Potion** | Restores 1440-2400 HP + 1080-1800 mana `[verify]` | Hybrid utility for healers in tight spots |
| **Greater Stoneshield Potion** | +4000 armor for 2 min | Tanks (mitigation cooldown) |
| **Limited Invulnerability Potion** | 100% physical damage immunity for 6s | Anti-melee panic button (Hunter Demon Stalker chain, world bosses) |
| **Free Action Potion** | 30s immunity to stuns + movement-impairing effects | Anti-CC; Hunter pet + Mage pre-pull |
| **Swiftness Potion** | +50% movement speed for 15s | OOC travel; out-of-combat-only |
| **Restorative Potion** | Removes 1 effect of Magic/Curse/Disease/Poison | Anti-debuff utility |
| **Mighty Rage Potion** | +20-60 rage immediately + 5 RPS for 20s + 60 strength `[verify]` | Warriors only effective; raid burst |
| **Magic Resistance Potion** | +50 all magic resistance for 1 hour | Resistance gear shortage |

**Potion CD shared rule**: All potions share a **2-minute cooldown** in 1.12.1. Engine plans potion timing to maximize impact (e.g., Major Mana Pot used during 2-min low-mana windows for healers; Major Healing pre-pull off-CD).

**Health/mana pot stacking rule**: Healthstone (Warlock-crafted) is on a **separate cooldown** from potions — so a player can chain Major Healing Potion + Healthstone for double heal in 2 minutes.

## Battle Elixirs (1 hour, persist through death; 1 slot)

| Elixir | Effect | Use case |
|---|---|---|
| **Elixir of the Mongoose** | +25 AGI, +2% melee crit | **Best DPS elixir** for melee (overlaps with Flask of Mongoose) |
| **Elixir of Greater Agility** | +25 AGI | Alternative if Mongoose unavailable |
| **Elixir of Brute Force** | +18 STR, +18 STAM | Warriors (+health + AP) |
| **Elixir of the Sages** | +18 INT, +18 SPI | Mages / Warlocks / casters |
| **Elixir of Giants** | +25 STR | STR-scaling DPS |
| **Elixir of Greater Intellect** | +25 INT | Casters |
| **Greater Arcane Elixir** | +35 spell damage and healing | Caster DPS / healers |

## Guardian Elixirs (1 hour, persist through death; 1 slot)

| Elixir | Effect | Use case |
|---|---|---|
| **Elixir of Greater Defense** | +35 armor | Tank mitigation |
| **Elixir of Superior Defense** | +450 armor | **Best tank elixir** (vs +35 Greater) |
| **Elixir of Fortitude** | +120 max HP | All-class survival |
| **Elixir of the Mongoose** | (already listed under Battle) — wait this is ambiguous. Mongoose is a **Battle elixir** in 1.12.1. | n/a |
| **Elixir of Greater Defense** vs **Elixir of Superior Defense** — both Guardian. Use Superior at 60. | | |

**Note**: A character can run **1 Battle Elixir + 1 Guardian Elixir** simultaneously since they're different slots. So Mongoose (+25 AGI / 2% crit) + Superior Defense (+450 armor) = optimal melee DPS / tank stack respectively.

## Juju Buffs (30 min, ZF turn-ins; stack with elixirs)

Found in Zul'Farrak (drops from Hydromancer Velratha or quest reward `[verify pass 3]`); jujus are the **niche-but-stackable** consumables for max-DPS pushers.

| Juju | Effect |
|---|---|
| **Juju Power** | +30 STR (best DPS juju for melee) |
| **Juju Might** | +40 AP |
| **Juju Flurry** | +3% melee haste, 30 min `[verify pass 3]` |
| **Juju Guile** | +30 INT |
| **Juju Escape** | 30% movement OOC |
| **Juju Chill** | +15 frost resist |

**Stacking advantage**: Juju Power + Elixir of the Mongoose + Mighty Rage Potion (in Warrior case) all stack. Engine should plan juju acquisition during ZF runs at lvl 44-54 and bank for raid pulls.

## Runes (mana restore, on cooldown)

| Rune | Effect | Source |
|---|---|---|
| **Demonic Rune** | Restores 600-1000 mana, deals 600 shadow damage to self, 2-min CD (separate from potion CD) | Crafted by Engineers OR drops from Diremaul / Demon-quest mobs |
| **Dark Rune** | Restores 900-1500 mana, deals 600 shadow damage to self, 2-min CD (separate) | BoP, drops from Scholomance bosses |

**Engine planning**: Healers in 5-min-plus boss fights chain potions + runes for sustainable mana. Each rune adds ~1000 mana per 2-min window — meaningful on long fights.

## Goblin Sapper Charge (Engineering Goblin spec)

**Effect**: 3-yard AoE explosion, deals 450-700 fire damage to enemies in range AND ~750-1000 damage to the caster (no friendly-fire on group). 5-min cooldown shared with other Goblin Engineering items.

**Use case**:
- AoE pulls during dungeon farming (Strat Live undead, ZF pyramid)
- Burst damage on bosses (used by Goblin-spec Rogues for Patchwerk-style burst)
- **Self-damage is significant** — uses on healers / squishy classes risky

**Engine rule**: Schedule Sapper for high-HP targets where 600 damage matters; suspend on tight-survival pulls.

## Class-specific consumables

| Class | Item | Effect |
|---|---|---|
| **Rogue** | **Thistle Tea** (recipe from class quest at lvl 20) | Restores 100 energy (full bar refresh) — **DPS-defining mid-fight refresh** |
| **Warlock** | **Healthstone (Major)** | Heals ~3000 HP; 2-min CD separate from potion CD |
| **Warlock** | **Soulstone** | Pre-applied to healer; on death gives self-rez within 15 min |
| **Mage** | **Conjured Water** + **Conjured Mana Gem** | Mana cost / regen utility for all casters |
| **Hunter** | **Pet food** | Maintains pet happiness for damage uptime |
| **Druid** | **Mana Pot rotation** | Resto Druid relies on Mana Pots + Innervate cycle |

## Food / Drink

Standard raid food provides stat buffs. Cooking-recipe-tied:

| Food | Effect | Cooking lvl |
|---|---|---|
| **Tender Wolf Steak** | +12 STR + STA, well-fed for 30 min | 250 |
| **Smoked Sagefish** | +6 mp5 well-fed | Cooking 175 |
| **Smoked Desert Dumplings** | +20 STR for 15 min | 175 |
| **Spider Sausage** | +30 SPI | 1 |
| **Spiced Chili Crab** | +5 STA + AP for 30 min | 175 |
| **Cooked Glossy Mightfish** | +20 STA | 250 |
| **Filet of Redgill** | +20 SPI | 175 |
| **Songflower Serenade or Heaven Peach** | n/a (these are world buffs / consumables, not food) | n/a |

## Pre-raid consumable stack checklist

Maximum DPS Warrior going into BWL Vael:

| Slot | Item |
|---|---|
| Flask | Flask of the Mongoose (+25 AGI / +2% crit) |
| Battle Elixir | Elixir of Brute Force (+18 STR/STA) |
| Guardian Elixir | Elixir of Superior Defense (+450 armor) for non-Vael, or Greater Fire Protection Potion for Vael fire phase |
| Juju #1 | Juju Power (+30 STR) |
| Juju #2 | Juju Might (+40 AP) |
| Food | Tender Wolf Steak (+12 STR/STA) |
| Drink | n/a (Warriors don't drink) |
| Sharpening Stone | Elemental Sharpening Stone (+28 weapon damage) |
| Pre-pull potion | Mighty Rage Potion at T-1s for instant rage burst |
| Inventory consumables | 2-4× Major Healing Potion, 1-2× Healthstone, 4× Goblin Sappers (if Goblin-spec'd), 4× Demonic/Dark Runes (for non-Warrior classes) |

**Estimated cost per raid (Warrior, full stack)**: 60-150g depending on AH prices. Naxx-era Warrior may go to 200g.

## Decision-Engine Rules

- **id:** `consumable.flask-pre-raid` — IF `Raid.Scheduled && Raid.PullTime > now + 5min && !ActiveBuffs.Contains(<RoleAppropriateFlask>)` AND `Items.Contains(<flask>)` THEN apply flask. Priority **820**.
- **id:** `consumable.battle-elixir` — IF `Raid.Scheduled && !ActiveBuffs.HasBattleElixir && Items.Contains(<role-appropriate-elixir>)` THEN apply. Priority **800**.
- **id:** `consumable.guardian-elixir` — IF `Raid.Scheduled && !ActiveBuffs.HasGuardianElixir && Items.Contains(<role-appropriate-guardian>)` THEN apply. Priority **800**.
- **id:** `consumable.food-pre-raid` — IF `Raid.Scheduled && !ActiveBuffs.WellFed` THEN eat best-available food in inventory. Priority **750**.
- **id:** `consumable.juju-stack` — IF `Raid.Scheduled && Spec==MeleeDPS && Items.Contains(JujuPower)` AND `!ActiveBuffs.Contains(JujuPowerBuff)` THEN use juju. Priority **750** (30-min only — apply ~10 min before raid pull).
- **id:** `consumable.healing-potion-emergency` — IF `InCombat && PlayerHP < 30% && PotionCD.NotActive && Items.Contains(MajorHealingPotion)` THEN drink potion. Priority **900** (combat-time emergency).
- **id:** `consumable.healthstone-emergency` — IF `InCombat && PlayerHP < 30% && Items.Contains(Healthstone) && Healthstone.NotOnCooldown` THEN use Healthstone. Priority **910** (slightly higher than potion since separate CD).
- **id:** `consumable.mana-pot-rotation` — IF `Class IN {Healer, Caster} && InCombat && PlayerMana < 30% && PotionCD.NotActive` THEN drink Mana Pot. Priority **800**.
- **id:** `consumable.rune-rotation` — IF `Class IN {Healer, Caster} && InCombat && PlayerMana < 30% && RuneCD.NotActive` THEN use Rune (separate CD from potion). Priority **800**.
- **id:** `consumable.stockpile-pre-raid` — IF `Raid.Scheduled && Items.MajorHealingPotionCount < 4` AND `in capital` THEN buy potions from AH. Priority **600**.
- **id:** `consumable.sharpening-stone-applied` — IF `Class IN {Warrior, Rogue, Hunter} && Raid.Scheduled && !ActiveBuffs.HasWeaponEnhancement` THEN apply Elemental Sharpening Stone (or class equivalent). Priority **750**.

## Snapshot Fields Needed

- `ActiveBuffs` (existing) — buff list with names + durations
- `ActiveBuffs.HasBattleElixir` / `HasGuardianElixir` / `WellFed` / `HasWeaponEnhancement` (planned helpers)
- `Items.Contains(itemId)` — for each consumable type
- `Items.<consumableType>Count` (planned) — bag scan helpers
- `PotionCD.IsActive` / `RuneCD.IsActive` / `Healthstone.OnCooldown` (planned — derived from spell/item cooldown table)
- `Raid.Scheduled` / `Raid.PullTime` (planned config)
- `Spec` / `Role` (planned account flags)

## Cross-references

- [systems/world-buffs.md](world-buffs.md) — world buffs that consumables stack with
- [decision-engine/state-flags.md](../decision-engine/state-flags.md) — `ActiveBuffs` field
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — pre-raid prep priority band
- [classes/rogue.md](../classes/rogue.md) — Thistle Tea recipe from lvl-20 class quest
- [classes/warlock.md](../classes/warlock.md) — Healthstone + Soulstone mechanics
- [professions/](../professions/) (pass 6) — Alchemy crafting routes for flasks + elixirs + potions
- [professions/](../professions/) (pass 6) — Engineering Goblin spec for Sapper Charges
- [professions/](../professions/) (pass 6) — Cooking 175-300 for raid food
