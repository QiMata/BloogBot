# All 9 Classes — Cross-Class Summary

> **Pass 2 synthesis** of the 9 individual class deep-dives. No new sources; cross-references the per-class files written in iterations 2-10. Synthesis date 2026-05-01.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.
>
> **Purpose.** The end-state goal of the DecisionEngine includes "all 9 classes leveled to 60 across the account." This file gives the engine the cross-class data needed for **account-roster planning** — picking which classes go on which faction-side accounts and which races optimize for the planned role mix.

## Faction + race availability matrix

✅ = available; ❌ = locked.

| Class | Hum | Dwa | NE | Gno | Orc | Tau | Tro | UD | Faction |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| **Warrior** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | Both |
| **Paladin** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | **Alliance only** |
| **Hunter** | ❌ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ | Both |
| **Rogue** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | Both (no Tauren) |
| **Priest** | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ | Both (5 races) |
| **Shaman** | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ | ❌ | **Horde only** |
| **Mage** | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ | Both (4 races) |
| **Warlock** | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ | Both (4 races) |
| **Druid** | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | Both (2 races only) |

**Engine race-class lookup**: This matrix is the source of truth for `engine.IsValidClassForRace(class, race)`. Any character creation request that fails this check is an engine error.

## Faction-locked class summary

| Class | Lock | Why it matters for the engine |
|---|---|---|
| Paladin | Alliance | Account plan needs an Alliance toon to cover Paladin |
| Shaman | Horde | Account plan needs a Horde toon to cover Shaman |

The minimum account allocation to cover all 9 classes is **2 accounts (1 Alliance + 1 Horde)**, since Paladin + Shaman cannot coexist on a single side. With 1 Alliance account = up to 10 chars + 1 Horde account = up to 10 chars, the 9-class roster fits comfortably with room for profession alts.

## Race-class lock summary

The most restrictive races (cannot roll many classes):

- **Tauren**: cannot Rogue, Mage, Warlock, Priest (only Warrior/Hunter/Shaman/Druid)
- **Gnome**: cannot Hunter, Priest, Shaman, Druid (only Warrior/Rogue/Mage/Warlock)
- **Undead**: cannot Hunter, Paladin, Shaman, Druid (only Warrior/Rogue/Priest/Mage/Warlock)
- **Human**: cannot Hunter, Shaman, Druid (only Warrior/Paladin/Rogue/Priest/Mage/Warlock)
- **Dwarf**: cannot Mage, Warlock, Druid (only Warrior/Paladin/Hunter/Rogue/Priest)
- **Night Elf**: cannot Paladin, Shaman, Mage, Warlock (only Warrior/Hunter/Rogue/Priest/Druid)
- **Orc**: cannot Paladin, Mage, Priest, Druid (only Warrior/Hunter/Rogue/Shaman/Warlock)
- **Troll**: cannot Paladin, Warlock, Druid (only Warrior/Hunter/Rogue/Priest/Shaman/Mage)

## Class quest hallmarks (lvl 60 epic class chain summary)

| Class | Lvl-60 epic class chain | Reward | Difficulty |
|---|---|---|---|
| **Warrior** | **Quel'Serrar** (Dire Maul tribute event w/ dragon spawned on top) | Epic 1H sword (tank-flavored) | High — multi-week chain, 5-man+ DM N + dragon kill |
| **Paladin** (Alliance) | **Summoning the Charger** (Stormwind → Ironforge → EPL → Hillsbrad → Scholomance) | Charger epic 100% mount (free riding, paladin-only) | High — 5-phase, ~450g hard cost, 5-man Scholo Death Knight Darkreaver waves |
| **Hunter** | **Stave of the Ancients** (Rhok'delar + Lok'delar) | Epic bow + epic staff | **Highest in vanilla** — MC Eye drop + 4 SOLO demon fights (no pet, no allies) + Onyxia kill |
| **Rogue** | **No epic class chain.** | n/a | n/a — only class without one. Rogues rely on world drops + Pickpocket gold |
| **Priest** | **The Balance of Light and Shadow** (Eye of Divinity from MC Majordomo + EPL peasant escort + Eye of Shadow from Lord Kazzak) | **Benediction** (heal-form) ↔ **Anathema** (shadow-form) staff | Very high — MC + EPL escort event + Lord Kazzak world boss |
| **Shaman** (Horde) | **No epic class chain.** | n/a | n/a — Shaman has Air Totem at 30 as the highest-impact class quest |
| **Mage** | **Arcane Refreshment** (Lorekeeper Lydros DM N + Hydrospawn DM E) | Conjure Water Rank 7 spell — water economy upgrade | Low — short DM N+E run; not a major chain like Hunter/Priest |
| **Warlock** | **Summoning the Dreadsteed** (Burning Steppes → Felwood → Scholomance + Dire Maul West) | Dreadsteed epic 100% mount (free riding, warlock-only) | High — ~250g hard cost + 5-man Scholo + 5-man DM W with wave-defense ritual |
| **Druid** | **No epic class chain** (Pristine Hide chain at 50 rewards gear, not a single epic) | Wildheart pieces | Low — gear-tier upgrade chain |

**Engine planning note**: Hunter Demon Stalker chain is the longest /played time investment of any class (4 solo demon fights tuned for unique class kit + Onyxia + MC). Plan for **2-4 weeks of dedicated /played**. Warrior Quel'Serrar and Warlock Dreadsteed are similar in /played but easier to "outsource" to a guild run. Rogue and Druid have no major chain so they reach raid-ready faster.

## Lvl-40 mount situation

Three classes get a **free mount + free apprentice riding** at lvl 40 — saving ~80g vs other classes:

| Class | Mount | Cost vs other classes |
|---|---|---|
| Paladin | **Summon Warhorse** | Saves ~170s (apprentice riding training + base mount) |
| Warlock | **Summon Felsteed** | Saves ~170s |
| Druid | **Travel Form** (lvl 30, even earlier than 40 mount!) | Saves ~170s + earlier — Druid has free outdoor 40% speed since lvl 30 |
| Shaman | Ghost Wolf at lvl 20 (40% outdoor speed, but **must dismount on combat**) | Partial — still needs to buy real mount at 40 |
| All other classes | Buy mount + train apprentice riding at vendor | ~170s+ at lvl 40 |

**Engine rule**: For Pal / Warlock at lvl 40, **NEVER auto-purchase a non-class mount** — the class chain is a class-identity priority **990** action. Druid bypass at 30 is even more compelling (Travel Form is ~10 levels earlier than apprentice riding for non-Druids).

## Lvl-60 epic mount summary

| Class | Mount | Cost | Source |
|---|---|---|---|
| Most classes | **Generic faction epic mount** (Stormwind Charger / Frostwolf, etc.) | ~1000g (90g epic riding training + 900g for mount) | Riding trainer + mount vendor |
| Paladin | **Charger** (class chain) | ~450g hard + chain | Class quest (free riding) |
| Warlock | **Dreadsteed** (class chain) | ~250g hard + chain | Class quest (free riding) |
| Druid | **No epic class mount in 1.12.1.** Druids buy a generic epic mount or use Travel Form (40% outdoor only). Flight Form is TBC. | ~1000g for generic | Standard |
| Hunter | No class mount; uses generic. **Spirit Bond pet kept alive via talents = essentially "always-mounted equivalent" via Aspect of the Cheetah** at lvl 14 (but breaks on hit) | ~1000g | Standard |

**Note**: A Tauren Warrior or Hunter who reaches lvl 60 with 1000g banked can buy a **Kodo** racial mount for cheaper after rep discount; same for Orc/Troll/Undead Wolf etc. Detailed in [systems/](../systems/) (pass 10 mounts file).

## Raid role coverage by class

For a 40-man MC raid (typical 1.12 composition), the raid leader staffs:

- 1-2 main tanks (Warrior preferred, occasional Druid Bear off-tank)
- 1-3 off-tanks (Druid Bear / Paladin Protection)
- 6-8 healers (Priest 4-6, Paladin 2-4 Alliance / Shaman 2-4 Horde + Druid 1-2)
- 3-5 caster DPS (Mage 2-3, Warlock 2-3, Shadow Priest 1, Boomkin Druid 0-1)
- 5-7 melee DPS (Rogue 3-5, Fury Warrior 1-3, Enhancement Shaman 0-2)
- 2-4 ranged DPS (Hunter 2-4)

| Class | Tank | Healer | Melee DPS | Ranged DPS | Caster DPS | Raid Utility (key buff) |
|---|:-:|:-:|:-:|:-:|:-:|---|
| Warrior | ✅✅✅ | ❌ | ✅✅ | ❌ | ❌ | Battle Shout AP buff (party) |
| Paladin (A) | ⚪ off-tank | ✅✅ | ⚪ niche | ❌ | ❌ | Blessings + Auras + Bubble |
| Hunter | ❌ | ❌ | ❌ | ✅✅✅ | ❌ | Trueshot Aura + Tranq Shot |
| Rogue | ❌ | ❌ | ✅✅✅ | ❌ | ❌ | Distract + Improved Expose Armor + Sap |
| Priest | ❌ | ✅✅✅ | ❌ | ❌ | ⚪ Shadow | Power Infusion + (Dwarf) Fear Ward |
| Shaman (H) | ⚪ niche | ✅✅ | ✅ | ❌ | ⚪ Elemental | **Windfury Totem** + Mana Tide |
| Mage | ❌ | ❌ | ❌ | ❌ | ✅✅✅ | Conjure Water + Decurse + Counterspell + Polymorph |
| Warlock | ❌ | ❌ | ❌ | ❌ | ✅✅✅ | Curse of Elements + Soulstone + Healthstone + Banish + Summons |
| Druid | ⚪ off-tank | ✅✅ | ⚪ niche | ❌ | ⚪ Boomkin | Innervate + Tranquility + **Rebirth (only combat-rez)** + Faerie Fire (-armor debuff) |

✅✅✅ = primary raid role; ✅✅ = secondary; ✅ = niche; ⚪ = off-spec / spec-dependent; ❌ = not viable.

**Tank coverage**: Warrior is the only true MT class. Druid Bear and Paladin Protection are off-tank options. This is why **every guild needs ≥3 Warriors** for MT/OT rotation.

**Healer coverage**: Priest dominates throughput; Paladin/Shaman/Druid fill in. **No class mono-heals** — vanilla raids run multi-healer comps.

## Account-roster planner

The end-state goal: **all 9 classes leveled to 60** across the account. With faction locks (Paladin = Alliance, Shaman = Horde), the engine plans a 2-account roster:

### Suggested Alliance roster (5-6 classes)

| Slot | Class | Race | Spec at 60 | Notes |
|---|---|---|---|---|
| 1 | **Paladin** | Human or Dwarf | Holy 31/5/15 | Alliance-locked; primary raid healer |
| 2 | **Warrior** | Human (sword spec) | Fury Impale 17/31/3 (raid) or Prot 8/5/38 (MT) | Account's main tank |
| 3 | **Hunter** | Dwarf (gun spec) or Night Elf (bow spec) | MM 5/30/16 | Trueshot raid utility |
| 4 | **Rogue** | Human (sword spec) or Night Elf (Shadowmeld PvP) | Combat Swords 15/31/5 | Lockbox economy + raid melee DPS |
| 5 | **Mage** | Gnome (Expansive Mind) | Deep Frost 10/0/41 or Deep Fire 5/41/5 | Water economy + AoE farming |
| 6 (optional) | **Druid** | Night Elf | Restoration 14/0/37 or Feral 0/30/21 | Combat-rez + healer flex |

### Suggested Horde roster (4-5 classes)

| Slot | Class | Race | Spec at 60 | Notes |
|---|---|---|---|---|
| 1 | **Shaman** | Orc (Enh) or Tauren (Resto) | Resto 5/3/43 or Enh 5/30/16 | Horde-locked; **Windfury raid utility** |
| 2 | **Warlock** | Orc (Command) or Undead (WotF) | SM/Ruin 30/0/21 | Curse of Elements + Soulstone + Summons |
| 3 | **Priest** | Undead (Devouring Plague) or Troll (Hex of Weakness) | Holy 23/30/0 | Mana-efficient healer |
| 4 (optional) | **Hunter** (alt) or **Rogue** (alt) | Troll (Berserking) or Orc (Hardiness) | Per role | Class duplicates if leveling alts |
| 5 (optional) | **Druid** | Tauren | Restoration | Combat-rez + flex healer |

### 9-class minimum-coverage plan

**Minimum viable**: 1 Alliance account with Pal/War/Hun/Rog/Mage + 1 Horde account with Sha/Warlock/Priest/Druid (Tauren). All 9 classes covered.

**Faction-flex classes** (can be on either side, with race-pick differences):
- Warrior (best Alliance: Human; best Horde: Orc)
- Hunter (best Alliance: Dwarf; best Horde: Troll)
- Rogue (best Alliance: Human; best Horde: Orc / Undead PvP)
- Priest (best Alliance: Dwarf for Fear Ward!; best Horde: Undead/Troll)
- Mage (best Alliance: Gnome; best Horde: Troll/Undead)
- Warlock (best Alliance: Gnome; best Horde: Orc/Undead)

**Faction-fixed**:
- Paladin → Alliance, Hum/Dwa
- Shaman → Horde, Orc/Tau/Tro

**Race-restricted**:
- Druid → NE (Alliance) or Tauren (Horde)

### Profession allocation across the 9-class roster

(Aligned with [professions/README.md](../professions/README.md) pass-6 contract)

| Class | Suggested primaries | Why |
|---|---|---|
| Warrior | Mining + Blacksmithing | Self-craft Lionheart Helm + repair income |
| Paladin | Mining + Blacksmithing | Tank itemization; alt of Warrior pairing |
| Hunter | Skinning + Leatherworking | Self-craft Devilsaur + Stormshroud + Wolfshead Helm |
| Rogue | Engineering + Mining (Goblin) — Sapper Charge utility | Or Herbalism + Alchemy for poisons synergy |
| Priest | Tailoring + Enchanting | Truefaith Vestments self-craft + DE economy |
| Shaman | Mining + Engineering (Goblin) | Sapper utility + repair |
| Mage | Tailoring + Enchanting | Robe of the Archmage + DE economy |
| Warlock | Tailoring + Enchanting | Felcloth Bag (soul shards) + spellcloth |
| Druid | Skinning + Leatherworking | Hide of the Wild + Druid leather sets |

**Cross-account profession coverage**: This pairing covers all 9 primary professions across the 9-class roster — Mining (×3), Skinning (×2), Tailoring (×3), Enchanting (×3), Engineering (×2), Blacksmithing (×2), Leatherworking (×2), Herbalism (×0 — gap; pair with Alchemy on Rogue alt OR add a dedicated farm alt). Alchemy is also missing — engine should slot Herbalism+Alchemy on a Rogue or as a dedicated 1-300 alt.

## Cross-class shared resources

Multiple classes compete on the same MC drops:

- **Eye of Divinity (Priest) / Ancient Petrified Leaf (Hunter)** — share 50/50 from Majordomo Executus's Cache. Engine plans MC roster with priest+hunter rotation across lockouts.
- **Eye of Shadow (Priest)** — drops from Lord Kazzak (world boss). Single drop per kill; contested with Naxx attune crafted Eye of Shadow Tailoring item.
- **Quel'Serrar (Warrior) chain trigger** — Foror's Compendium + Dire Maul N tribute event. Tank chain.
- **MC Bindings (Eye of Sulfuras)** — used by Warriors crafting Sulfuras (and Shaman+Pal alts).

## Decision-Engine Rules (cross-class meta-rules)

- **id:** `account.class-coverage` — Account end-state requires all 9 classes leveled. Engine tracks `Account.ClassesLeveled[Class] : Level` and prefers leveling missing classes over alt-of-already-leveled classes.
- **id:** `account.faction-balance` — IF `Account.AllianceCharCount == 0 && PlannedClassRoster.Contains(Paladin)` THEN engine schedules creation of an Alliance toon (typically Hum/Dwarf for Paladin start).
- **id:** `account.faction-balance.horde` — IF `Account.HordeCharCount == 0 && PlannedClassRoster.Contains(Shaman)` THEN engine schedules creation of a Horde toon (Orc/Tauren/Troll).
- **id:** `account.druid-race-pick` — IF `PlannedClassRoster.Contains(Druid) && Account.AllianceHasNightElf == false && Account.HordeHasTauren == false` THEN engine schedules NE Druid on Alliance (or Tauren on Horde, whichever side has fewer chars).
- **id:** `account.priest-fear-ward-flex` — IF `Account.AlliancePriest != null && AlliancePriest.Race != Dwarf && Account.PrimaryRaidIsAlliance` THEN flag for **Dwarf Priest reroll** at next major raid milestone (Fear Ward is mandatory raid utility on Alliance). Priority **300** (long-term reroll plan).
- **id:** `account.respec-cost-budget` — Engine reserves 50g per character for **respec costs** (max-cap respec is 50g, decays 5g/30 days). Mages, Druids, and Warriors respec most frequently between phases.

## Snapshot Fields Needed (account-level)

This file references **account-level state** that doesn't live on a single character snapshot:

- `Account.Characters[]` (StateManager-resident) — list of characters with class/race/level/faction
- `Account.ClassesLeveled[Class] : MaxLevelAcrossAccount` (planned aggregator)
- `Account.AllianceCharCount` / `Account.HordeCharCount` (planned, derived)
- `Account.PrimaryRaidFaction` (config flag — engine prefers raid roster on this faction)
- `PlannedClassRoster` (config — the engine's plan for which 9-of-9 classes to level)

These are **not on `WoWActivitySnapshot`** — they're consulted by the StateManager's cross-character planner at action-selection time (see [decision-engine/leveling-priority.md#faction-side-priority](../decision-engine/leveling-priority.md#faction-side-priority)).

## Cross-references

- All 9 individual class files: [warrior.md](warrior.md), [paladin.md](paladin.md), [hunter.md](hunter.md), [rogue.md](rogue.md), [priest.md](priest.md), [shaman.md](shaman.md), [mage.md](mage.md), [warlock.md](warlock.md), [druid.md](druid.md)
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band + faction-side priority
- [sections/00-questions-and-answers.md](../sections/00-questions-and-answers.md) Q3 — multi-character account planning open question (resolves here as account-roster planner table)
- [professions/README.md](../professions/README.md) — profession allocation pass 6
- [reputations/README.md](../reputations/README.md) — Wintersaber Trainers (Alliance only) for cat mount
- [pvp/README.md](../pvp/README.md) — class PvP viability and rank-pick guide
