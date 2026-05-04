# Weapon Skill — 1.12.1 Mechanics + Racial BiS

> **Sources** (crawl date 2026-05-01):
> - https://wowwiki-archive.fandom.com/wiki/Glancing_blow (referenced via search)
> - https://wowpedia.fandom.com/wiki/Glancing_blow
> - https://warcraft.wiki.gg/wiki/Glancing_blow
> - https://vanilla-wow-archive.fandom.com/wiki/Glancing_blow
> - https://forum.nostalrius.org/viewtopic.php?f=6&t=559 (referenced via search)
> - https://bookdown.org/marrowwar/marrow_compendium/mechanics.html
>
> **Pass 2.** Some details (exact training cost per weapon type, weapon-master coordinate locations) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Weapon skill is **the most under-appreciated stat in vanilla 1.12.1 raid mechanics**. Against a **level 63 raid boss** (all MC/BWL/AQ40/Naxx bosses are effective lvl 63), a melee character with **300 weapon skill** suffers a **35% damage reduction on every glancing blow** — and **40% of all white-swing attacks are glances**. The math:

- **300 weapon skill** (base lvl-60 cap): glances do **65% damage** (35% penalty)
- **305 weapon skill** (with +5 racial spec): glances do **85% damage** (15% penalty) — **+~7% raw DPS**
- **308 weapon skill** (cap; +5 racial + +3 gear): glances do **95% damage** (5% penalty) — **+~10% raw DPS** vs 300

**308 is the soft cap** — gains beyond 308 produce zero glance-damage improvement. **This is why Human Warriors using swords (and Orc Warriors using axes) parse ~5-7% higher** than equivalent-gear non-spec races.

## The cap math

| Player weapon skill | Glance chance vs lvl 63 boss | Glance damage % | Net damage vs ideal |
|---|---|---|---|
| 295 (lvl 59 character) | ~45% glance | ~55% damage on glance | -22% vs cap |
| 300 (lvl 60 base, no spec) | 40% glance | 65% damage on glance | -14% vs cap |
| 305 (+5 racial spec) | ~37% glance | 85% damage on glance | **-3% vs cap** |
| **308 (+5 racial + +3 gear)** | ~36% glance | 95% damage on glance | **0% (cap)** |
| 309+ | same as 308 | same as 308 | 0 (no further gain) |

**Practical conclusion**: A racial-spec'd Warrior using Edgemaster's Handguards (+5 dagger/sword skill — wait, Edgemaster's gives +5 dagger/sword) effectively reaches 308 weapon skill at lvl 60 → caps glance penalty.

`[verify pass 3 — Edgemaster's Handguards exact stats: +5 to all 1H weapon skills, BoP from BS-crafted or BoE drop]`

## Weapon types and skill categories

Each weapon type has a separate skill bar. A character with sword skill 300 has zero skill in axes — they would need to grind axes from 1 to 300 separately to use them effectively.

**1H weapon types** (relevant for melee):
- **Sword** (1H): straightswords, scimitars, etc.
- **Mace** (1H): maces, flails
- **Axe** (1H): hatchets, hand-axes
- **Dagger**: daggers, knives
- **Fist Weapon**: knuckles, claws (Orc and other Fist-spec races)
- **Polearm**: pikes (some 2H but classed as polearm; Druid feral, Paladin, Warrior, Hunter)

**2H weapon types**:
- **Two-Handed Sword**
- **Two-Handed Mace**
- **Two-Handed Axe**
- **Staff** (used by casters as stat-stick + Druid feral; Mages, Druids, Priests, Warlocks, Hunters can use)

**Ranged weapon types**:
- **Bow** (Hunter, NE racial)
- **Crossbow** (Hunter)
- **Gun** (Dwarf racial, Hunter, Warrior)
- **Throwing**: thrown weapons + Throwing axes/spears (Troll racial, Rogue, Hunter, Warrior)
- **Wand** (Mage, Priest, Warlock — caster autoshot equivalent)

## Racial weapon specializations (+5 starting skill)

The racials that grant **+5 weapon skill in specific categories** are the meta-defining mechanic for melee race choice in raids:

| Race | Faction | Spec'd weapons | Notes |
|---|---|---|---|
| **Human** | Alliance | **Sword** (+5), **Mace** (+5) | **Best Alliance Warrior/Rogue** for sword/mace itemization (Brutality Blade, Hand of Justice mace, Hand of Rag — wait Hand of Rag is mace — actually 2H mace with sword skill use? clarify pass 3) |
| **Dwarf** | Alliance | **Mace** (+5), **Gun** (+5) | Mace for 1H tank (Quel'Serrar — wait that's sword), Gun for Hunter |
| **Night Elf** | Alliance | **Bow** (+5) | Best Alliance Hunter race for bow itemization |
| **Gnome** | Alliance | none | No weapon spec — but +5% int, Escape Artist, smaller hitbox |
| **Orc** | Horde | **Axe** (+5), **Fist Weapon** (+5) | **Best Horde Warrior/Rogue** for axe itemization (Sandfury Cleaver ZF, etc.); Fist for Fist-spec niche |
| **Troll** | Horde | **Bow** (+5), **Throwing** (+5) | Best Horde Hunter for bow itemization |
| **Tauren** | Horde | none (Endurance + War Stomp instead) | Larger hitbox = PvP disadvantage; +5% HP from Endurance |
| **Undead** | Horde | none (WotF + shadow res instead) | PvP utility focus (Will of the Forsaken) |

**Engine race-pick rule** (melee DPS roles): The +5 racial weapon spec is the **single highest-impact race choice** in the game for melee classes. A Human Combat Swords Rogue parses ~5-7% above an Undead/Gnome equivalent. An Orc Fury Warrior using axes parses similarly higher than non-spec races. This is canonical 1.12.1 meta.

## Skill progression — how to gain weapon skill

### Per-swing skill-up chance

When a player swings a weapon and **lands the hit** (not a miss / dodge / parry), there's a small chance the weapon skill increases by 1. Probability depends on:
- Current skill: higher skill = lower up-rate
- Target level: equal or higher level = better up-rate
- **Diminishing odds** in higher tiers (1-150: ~80% chance per swing; 200-300: ~5-15% per swing)

**Time to grind weapon from 1 → 300**:
- Lvl 60 character grinding lvl 50+ humanoids: **2-4 hours /played** for a single weapon type
- This is why **switching weapon types is costly** — engine should commit to a class's racial-spec weapon and stick with it.

### Weapon master training (capital cities)

Each capital has a **Weapon Master** NPC who teaches new weapon-class skills (cost: ~1g per weapon class):

| City | Weapon Master | Notes |
|---|---|---|
| Stormwind | **Woo Ping** (Old Town) | Trains all weapons available to Alliance |
| Ironforge | **Bixi Wobblebonk** (Hall of Arms) | Trains all weapons available to Alliance |
| Darnassus | **Ilyenia Moonfire** (Warrior's Terrace) | NE-side trainers |
| Orgrimmar | **Sayoc** (Valley of Honor — axes/2H) + **Hanashi** (Valley of Honor — swords/maces) — split between two NPCs | |
| Thunder Bluff | **Ansekhwa** (limited weapon training; usually fly to Org) | |
| Undercity | **Archibald** (War Quarter) | UD-side trainers |

**Engine training rule**: When a character first encounters a new weapon type from quest reward / drop, before equipping it, train at the relevant Weapon Master (cost ~1g). Without training, **the weapon cannot be equipped at all**.

## Weapon skill quests (per-class via class trainer)

Some classes have specific weapon-skill quests at lvl 30 / 40 / 60:

- **Warrior**: Whirlwind Weapon at lvl 30 (chooses Axe/Sword/Warhammer reward; the chosen 2H weapon comes already at decent skill — see [warrior.md](../classes/warrior.md))
- **Hunter**: Bow/Gun specialization quest at lvl 40 — `[verify pass 3]`
- **Paladin**: Verigan's Fist at lvl 20 (2H mace already with skill)
- **Rogue**: Lockpicking is a separate "skill" but works similarly (1-300 grind, lvl 16 unlock)

## Engine implications for raid prep

**Critical for raid melee**: A Warrior or Rogue planning to enter MC/BWL **MUST verify weapon skill is at or near 308** for primary weapon. Engine rules:

1. At ding 60, schedule a 2-4 hour weapon-skill grind on the racial-spec weapon (vs same-level mobs in EPL/WPL/Tyr's Hand for sword/axe scaling).
2. Prefer racial-spec weapons over equal-stat alternatives (Brutality Blade for Human Warrior > Maladath for Human Warrior; Sandfury Cleaver for Orc > equivalent sword).
3. Check Edgemaster's Handguards (+5 to all 1H weapon skills) availability for Honored AD or BoP-crafted source — engine notes if usable.
4. Suspend weapon-skill grinds during world-buff windows (don't waste world buffs grinding dummies).

## VMaNGOS / private server notes

- **Glance damage formula** matches retail 1.12.1 on most VMaNGOS forks.
- **Weapon skill 308 cap** is correctly implemented.
- **Racial +5 weapon spec** is correctly applied as a passive at character creation.
- **Per-swing skill-up rate** matches retail 1.12.1 distributions.
- **Weapon master training cost** is correctly ~1g per weapon class.

## Decision-Engine Rules

- **id:** `weaponskill.use-racial-spec-weapon` — IF `Class IN {Warrior, Rogue, Hunter} && Race.HasWeaponSpec` AND comparing two weapons of equivalent stats THEN prefer the weapon matching racial spec. Priority weight modifier **+50** to gear-pick rules.
- **id:** `weaponskill.cap-308` — IF `Level==60 && Class.IsMeleeDPS && WeaponSkill[primary] < 308` AND `(Race.HasWeaponSpec for primary OR Items.EdgemasterHandgloves equipped)` THEN grind weapon skill to 308. Priority **600** (raid prep critical).
- **id:** `weaponskill.train-new-weapon` — IF `Items.NewWeapon.RequiresUntrainedSkill && CopperOnHand >= 100` AND in capital THEN visit Weapon Master before equipping. Priority **800** (gates equipping).
- **id:** `weaponskill.no-weapon-switch-mid-raid` — IF `InRaid && CurrentWeaponSkill > 280 && Items.NewerWeapon.SkillType != CurrentSkillType` THEN suppress weapon switch. Priority **999** (don't reset weapon skill mid-raid).
- **id:** `weaponskill.suspend-during-buff-window` — IF `WorldBuffWindowOpen` THEN suspend weapon-skill grind action. Priority **999**.
- **id:** `weaponskill.grind-target-selection` — IF `WeaponSkillGrindActive` THEN target lvl-58-60 mobs (not elites; no XP-loss-on-death from over-leveling). Priority **400**.

## Snapshot Fields Needed

- `WeaponSkill[weaponType]` (existing) — current weapon skill per type (Sword, Mace, Axe, Dagger, Fist, 2H Sword, 2H Mace, 2H Axe, Polearm, Staff, Bow, Gun, Crossbow, Thrown, Wand)
- `Race.HasWeaponSpec(weaponType)` (planned helper — derived from Race table)
- `EquippedWeapon.SkillType` (planned — derived from item table)
- `Items.EdgemasterHandgloves` (planned — bag/equipment scan)
- `Class.IsMeleeDPS` (planned helper)
- `WorldBuffWindowOpen` (existing — from world-buffs.md)
- `WeaponSkillGrindActive` (planned — engine-internal action flag)

## Cross-references

- [classes/warrior.md](../classes/warrior.md) — Whirlwind Weapon class quest at lvl 30; weapon-skill rule for raid Warriors
- [classes/rogue.md](../classes/rogue.md) — Combat Swords meta favored by Human race
- [classes/hunter.md](../classes/hunter.md) — Bow specialization for NE/Troll Hunters
- [classes/paladin.md](../classes/paladin.md) — Verigan's Fist 2H mace
- [classes/all-9-classes-summary.md](../classes/all-9-classes-summary.md) — race-pick table per class with racial weapon specs
- [systems/world-buffs.md](world-buffs.md) — `WorldBuffWindowOpen` rule that suspends grinding
- [decision-engine/state-flags.md](../decision-engine/state-flags.md) — `WeaponSkill[type]` field
- [reputations/argent-dawn.md](../reputations/argent-dawn.md) — AD insignia gear (no direct weapon-skill items but contributes to overall raid prep)
