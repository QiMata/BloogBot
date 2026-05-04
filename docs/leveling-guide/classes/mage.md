# Mage — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Mage (canonical, modern)
> - https://www.wowhead.com/classic/guide/mage-class-quests-classic-wow
> - https://www.icy-veins.com/wow-classic/mage-quests-in-wow-classic
> - https://legacy-wow.com/polymorph-pig-quest-classic/ (referenced via search)
> - https://vanilla-wow-archive.fandom.com/wiki/Mage/Quests (referenced via search)
>
> **Pass 2.** Some details (T0/T0.5 piece-by-boss, full Mage's Wand quest chain step list) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Mages are the **highest single-target burst caster** and **highest AoE damage class** in 1.12.1. The class brings **mandatory raid utility**:

- **Conjure Water / Conjure Mana Gem** — the **water economy** of vanilla raid mana management; healers blow through 30+ water/raid, all of which comes from Mages
- **Polymorph** — humanoid/beast CC; 5-mans rely on Sap (Rogue) + Polymorph (Mage) for trash management
- **Counterspell** — interrupts caster mobs (Razorgore, Vael, AQ40 Twin Emperors)
- **Decurse / Remove Lesser Curse** — Mages are one of two classes that decurse (other: Druid)
- **Arcane Brilliance / Arcane Intellect** — raid-wide INT buff
- **Portal: <city>** — fast travel for the entire raid (replaces hearthstone/flight overhead) — every raid keeps 1+ Mage for Portal duty

Mages are **the AoE-grinding class** at lvl 60 — Frost Mages farm dungeon trash (Strat Live + Mara) and lvl 55-60 outdoor mob packs (Plagued Hatchlings, Dire Mauls, Princess Theradras runs) for ~10-30g/hour.

| Role | Spec | Strength |
|---|---|---|
| Raid DPS (single-target) | Deep Fire 5/41/5 (or Pyromancer hybrid) | Combustion + Pyroblast burst on Fire-vulnerable bosses |
| Raid utility / trash farming | Deep Frost 10/0/41 | Cone of Cold + Frostbolt + Ice Barrier survival |
| AoE / world farming | Frost AoE 10/0/41 | Pull 8-15 mobs, Frost Nova → Blizzard → Cone of Cold cycle |
| PvP / arena | Pom-Pyro 33/0/18 + variants | Presence of Mind + Pyroblast = instant 2k+ crit kill |
| Leveling | Frost (general) → Fire respec at 60 | Frost is the safest leveling spec; Frostbolt slow + Frost Nova kite |

## Race availability + racial trait synergy

In 1.12.1 **Mage is restricted to 4 races**: **Human, Gnome, Undead, Troll**. (No Dwarf, Night Elf, Tauren, Orc Mages in vanilla. Dwarf/NE Mages added in Cataclysm; Tauren in MoP.)

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Human | Alliance | Sword/Mace Spec +5, **Diplomacy** (rep gain), Perception | Diplomacy speeds AD/Brood/Cenarion grinds. Standard Alliance Mage. |
| Gnome | Alliance | **Expansive Mind** (+5% int — **defining Mage racial**), **Engineering Specialist** (+15 Engineering), **Escape Artist** (root break, 27s CD), small hitbox | **Best Alliance Mage race** — +5% int = +5% mana pool + +5% spell crit indirectly. Escape Artist is core PvP. Smaller hitbox = LoS PvP advantage. |
| Undead | Horde | **Will of the Forsaken** (5s fear/sleep/charm immune, 2min CD), Cannibalize, Underwater Breathing, Shadow Resist +10 | Best Horde PvP Mage — WotF counters Warlock Fear / Priest Psychic Scream. Shadow Res 10 helps vs SP. |
| Troll | Horde | **Berserking** (haste based on missing HP, 10s, ~3min CD), Bow/Throwing +5, Beast Slaying, **Regeneration** (+10% health regen) | **Best Horde DPS Mage in execute window** — Berserking syncs with Combustion + Pyroblast burst. Regeneration helps solo questing. |

**Engine race-pick rule** (Mages, by role):
- **Raid DPS** Alliance → **Gnome** (Expansive Mind +5% int = the highest-impact PvE racial for Mages)
- **Raid DPS** Horde → **Troll** (Berserking burst window aligns with Combustion)
- **PvP** → **Undead** (WotF) on Horde / **Gnome** (Escape Artist) on Alliance

## Class quests in level order

| Lvl | Quest / Chain | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| 1-9 | Race-specific starter | Class trainer in starting zone | Starter caster weapon + Conjure Bread / Water rank 1 | Auto-accept |
| 14 | **Polymorph** (Sheep) trained | Trainer | Polymorph (10s incapacitate humanoid/beast, breaks on damage; uses no reagent in 1.12 — consumed Rune of Polymorph in beta only) | Auto-trained |
| 17/20 | **Teleport: <home city>** trained | Trainer | Teleport: Stormwind / Ironforge (Alliance @ 20); Teleport: Orgrimmar / Undercity (Horde @ 20). Darnassus (A) and Thunder Bluff (H) at lvl 30 trainer rank | Trainer |
| 20 | Conjure Food / Water Rank 3-4 ranks | Trainer | — | Auto-trained |
| **28-30** | **Polymorph: Pig** quest | **Archmage Xylem** (Azshara, ~75,77) → **Warlord Krellian** (Ruins of Eldarath, lvl 49 elite naga) → **Fragmented Magic** quest (Polymorph 50 Spitelash Naga in Eldarath using Prismatic Shell) → return | **Polymorph: Pig** spell (visual variant of Polymorph; mechanically identical) | Bundle with Azshara questing at lvl 45+; **NOT solo at lvl 28** — Krellian is a lvl-49 elite. Engine schedules at lvl 45+ when nearby. Priority **400** (cosmetic flex; mechanically equivalent to base Polymorph). |
| **30** | **Mage's Wand → Icefury Wand** quest chain (multi-zone) | Class trainer → "Journey to the Marsh" → "Hidden Secrets" → "Get the Scoop" → "Rituals of Power" → "Items of Power" | **Icefury Wand** (lvl 30 caster wand with Frost damage proc) | Priority **600** (gear gate for caster wand slot through 30-50 bracket). |
| **35** | **Celestial Stave** chain (continuation of Mage's Wand) | "Return to the Marsh" → "The Infernal Orb" → "The Exorcism" → "Power in Uldaman" (requires Uldaman group run) → "Mana Surges" → "Celestial Power" | **Celestial Stave** (lvl 40 staff with Spirit/Int) | Priority **600**. Bundle with planned Uldaman run. |
| 40 | **Portal: <home city>** trained | Trainer | Portal: Stormwind/IF/Darnassus (A) or Org/UC/TB (H) | Trainer |
| **50** | **Sunken Temple** Mage chain (Fire Ruby / Arcane Crystal Pendant) | Class trainer "Magecraft" → "Magic Dust" → "The Siren's Coral" → **"Destroy Morphaz"** (requires ST run) | Choice of **Fire Ruby** (trinket: instant 65-80 Fire damage spell, 30s CD) **OR** **Arcane Crystal Pendant** (neck with Int/Spi/spell damage) | Priority **620** at lvl 50. Bundle with planned ST. Fire Ruby is canonical PvP / utility pick; Pendant for Holy/Disc Priest crossover-stats. |
| 50+ | **Polymorph: Turtle** book | **PvP rank 11+ quartermaster** (in Stormwind / Orgrimmar PvP halls) | Polymorph: Turtle (visual variant) | Cosmetic only. Requires PvP rank 11 (Commander/Lieutenant General) — multi-week PvP grind. Priority **150**. |
| **60** | **Arcane Refreshment** chain | **Lorekeeper Lydros** (Dire Maul N library) → **Hydrospawn** (Dire Maul E elite, drops **Hydrospawn Essence**) → return | **Conjure Water Rank 7** spell (highest-rank water; reduces water consumption and improves restored mana) | **Single most-important Mage lvl-60 quest.** Priority **800** at lvl 60 — gates raid water-economy efficiency. |
| 60 | **No epic class weapon quest** — Mage lacks a class chain like Hunter Rhok'delar / Priest Benediction. | n/a | n/a | Engine notes this. Mage gear progression is from world drops + crafted (Robe of the Archmage tailoring), not class quests. |

### Robe of the Archmage (Tailoring-crafted, not a class quest)

`[verify pass 3 for exact mat list]`

**Robe of the Archmage** is a **Tailoring 300 BoP recipe** — *Mage-only* equipped. Mats include 8× Mooncloth + 4× Pristine Hide of the Beast (Sunken Temple drops) + Arcanite Bars + Enchanted Leather. The robe gives massive Spirit / Int / spell damage. **Pre-raid BiS chest for Mages**, and the second-best Mage chest until BWL T2.

**Recipe source**: Drops from **Trade Goods Vendors** in Naxxramas era, but in 1.12.1 the recipe drops from world bosses + UBRS. `[verify pass 3 — exact recipe drop source]`. Mages need to either farm the recipe themselves or buy from a tailoring alt.

## Talent trees (1.12 51-point trees)

### Arcane (Arcane Power capstone)

Mana efficiency + utility. The 31-pt capstone **Arcane Power** = 15s self-buff: +30% spell damage, +30% mana cost, 3-min CD.

Key talents: **Arcane Subtlety** (3/3 — -threat), **Arcane Focus** (5/5 — +10% spell hit on Arcane school), **Arcane Concentration** (5/5 — Clearcasting proc on hit), **Magic Absorption** (5/5 — +10 all resists), **Improved Arcane Missiles** (5/5 — pushback resistance), **Improved Mana Shield** (2/2), **Improved Counterspell** (2/2 — +4s silence on Counterspell), **Arcane Meditation** (3/3 — +15% mana regen during cast), **Presence of Mind** (1/1 — next spell instant cast, 3-min CD — **defining PvP talent**), **Arcane Mind** (5/5 — +15% Int), **Arcane Instability** (3/3 — +3% spell damage + 1% crit), **Arcane Power** (1/1 — 31-pt capstone).

### Fire (Combustion capstone)

Burst Fire damage. The 31-pt capstone **Combustion** = next 3 Fire spell crits guaranteed; +50 to Fire crit chance during; 3-min CD.

Key talents: **Improved Fireball** (5/5 — -0.5s Fireball cast), **Impact** (5/5 — Stun proc on Fire damage), **Ignite** (5/5 — 40% of Fire crit damage as DoT — **defining Fire talent**), **Flame Throwing** (2/2 — +6 yards Fire range), **Improved Flamestrike** (3/3 — +15% Flamestrike crit), **Incinerate** (2/2 — +4% spell crit on Fire Blast / Scorch), **Improved Fire Blast** (3/3 — -2s Fire Blast CD), **Critical Mass** (3/3 — +6% crit on Fire spells), **Master of Elements** (3/3 — refund 30% mana on crits), **Burning Soul** (2/2 — pushback resistance), **Improved Scorch** (3/3 — chance to apply Fire vulnerability debuff — stack to **+15% Fire damage taken** on target = mandatory raid debuff), **Fire Power** (5/5 — +10% Fire damage), **Combustion** (1/1 — 31-pt capstone).

### Frost (Ice Barrier capstone)

Survivability + sustained Frost damage. The 31-pt capstone **Ice Barrier** = 1-min absorb shield, instant cast, 30s CD — **the defining PvP / leveling Frost talent**.

Key talents: **Frost Warding** (2/2 — +30% Frost Armor effect), **Improved Frostbolt** (5/5 — -0.5s Frostbolt cast), **Elemental Precision** (3/3 — +6% Frost/Fire spell hit), **Ice Shards** (5/5 — +100% Frost crit damage — **defining Frost crit talent**), **Frostbite** (3/3 — chance to root on Frostbolt), **Improved Frost Nova** (2/2 — -2s Frost Nova CD), **Permafrost** (3/3 — +9 yards Frost slow), **Piercing Ice** (3/3 — +6% Frost damage), **Cold Snap** (1/1 — resets Frost spell CDs, 10-min CD), **Improved Blizzard** (3/3 — Blizzard slows targets 25%), **Arctic Reach** (2/2 — +20% range on Frostbolt/Blizzard/Frost Nova), **Frost Channeling** (3/3 — -15% mana cost on Frost spells), **Shatter** (5/5 — +50% crit chance vs frozen targets — **defining Frost burst talent**), **Ice Block** (1/1 — 10-sec invulnerability, 5-min CD — **panic button**), **Ice Barrier** (1/1 — 31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Deep Fire (raid DPS)** | **5/41/5** — 5 Arcane (Arcane Concentration, Arcane Subtlety) → Fire 41 (Imp Fireball 5 / Ignite 5 / Imp Flamestrike 3 / Critical Mass 3 / Incinerate 2 / Imp Fire Blast 3 / Master of Elements 3 / Improved Scorch 3 / Fire Power 5 / Combustion 1 + filler) → 5 Frost (Frost Warding) | Single-target raid DPS | Best on Fire-vulnerable bosses (BWL Vael, Razorgore, Firemaw / Ebonroc / Flamegor *not* Fire-vulnerable; AQ40 Battleguard Sartura, etc.). NOT for MC (most MC bosses are fire-immune). |
| **Deep Frost (MC + trash farming)** | **10/0/41** — 10 Arcane (Imp Counterspell 2 + Arcane Meditation 3 + Arcane Subtlety 3 + Arcane Focus 2) → 0 Fire → Frost 41 (Imp Frostbolt 5 / Ice Shards 5 / Frostbite 3 / Imp Frost Nova 2 / Permafrost 3 / Piercing Ice 3 / Cold Snap 1 / Imp Blizzard 3 / Frost Channeling 3 / Shatter 5 / Ice Block 1 / Ice Barrier 1 + filler) | MC raid DPS, trash farming, AoE | Best for Fire-immune MC trash and bosses |
| **Pom-Pyro PvP** | **33/0/18** or **33/18/0** — Arcane to Presence of Mind + Arcane Power; Fire splash to Pyroblast (lvl 20 talent) | BG / arena / world | Instant Pyroblast → Combustion follow-up — top-tier 1v1 burst |
| **Leveling Frost** | 0/0/31 progressively → 10/0/41 by 60 | Solo questing | Frost = safest leveling spec; Ice Barrier + Frost Nova + Frostbolt slow makes 1v1 trivial. AoE leveling once Improved Blizzard at lvl 35+. |

## Recommended weapons by bracket

| Bracket | Weapon | Notes |
|---|---|---|
| 1-15 | Vendor staff or 1H+OH | Stat sticks |
| 15-30 | Quest staff (Staff of Westfall A / Crescent Staff H) | Stat sticks |
| 30-40 | **Icefury Wand** (class quest) for ranged-slot wand; staff for MH | Class wand at lvl 30 is the best wand for the bracket |
| 40-50 | **Celestial Stave** (class quest) | Mage-only stat stick |
| 50-58 | Stat sticks (Hand of Edward the Odd, Truesilver Champion staff) | |
| 58-60 | **Headmaster's Charge** (Scholo) staff | Pre-raid BiS staff |
| 60 (post-MC) | **Staff of Dominance** (BWL Nefarian) / **Atiesh, Greatstaff of the Guardian** (Naxx legendary) | |
| Wand slot | **Icefury Wand** (lvl 30) → **Wand of the Whispering Dead** (Strat) → **Hand of Edward the Odd** offhand | Mages always equip a wand; wands proc spell damage between casts |

## Pre-raid BiS gear (Frost / Fire raid Mage)

`[verify pass 3 for exact items]`

| Slot | Item | Source |
|---|---|---|
| Head | **Crown of Tyranny** / **Magister's Crown** (T0) → upgrade to **Sorcerer's Crown** (T0.5) | T0 from Strat UD Baron / T0.5 quest |
| Neck | **Mark of the Chosen** (Stratholme) / **Arcane Crystal Pendant** (class quest at 50) | |
| Shoulders | **Magister's Mantle** (T0) | |
| Cloak | **Cape of the Cosmos** (Tailoring) / **Cloak of the Cosmos** | |
| Chest | **Robe of the Archmage** (Tailoring BoP, Mage-only) | Tailoring 300 |
| Bracers | **Magister's Bindings** | |
| Hands | **Hands of Power** (BoE) / **Magister's Gloves** | |
| Belt | **Magister's Belt** | |
| Legs | **Magister's Leggings** | |
| Feet | **Magister's Boots** / **Boots of the Full Moon** | |
| Ring 1 | **Magni's Will** / **Don Mauricio's Band of Magnetism** (Strat UD Maleki) | |
| Ring 2 | **Tarnished Elven Ring** (BoE) | |
| Trinket 1 | **Briarwood Reed** (DM N) — caster trinket | DM N tribute |
| Trinket 2 | **Eye of the Beast** (LBRS) / **Fire Ruby** (class quest at 50) | |
| MH | **Headmaster's Charge** (staff) / **Hand of Edward the Odd** (BoE 1H sword with chance-to-cast-spell-on-hit proc) | |
| OH | n/a if staff; **Tome of Knowledge** if 1H | |
| Wand | **Wand of the Whispering Dead** / **Hand of Edward the Odd** | |

## Tier set progression

| Tier | Set name (Mage) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Magister's Regalia** | 8-piece, 60 5-mans | Drop-only, BoP |
| **T0.5 (Dungeon Set 2)** | **Sorcerer's Regalia** | Quest upgrade chain (patch 1.10) | Stat bridge to T1 |
| **T1** | **Arcanist Regalia** | Molten Core (8-piece) | Set bonuses: -mana on spells + Arcane critical bonus |
| **T2** | **Netherwind Regalia** | BWL + Onyxia (8-piece) | Iconic crown helm; set bonuses: chance to produce mana orbs, +12% Polymorph duration |
| **T2.5** | **Enigma Vestments** | AQ40 — token-based (Qiraji Bindings) | |
| **T3** | **Frostfire Regalia** | Naxx40 — 9-piece token-based | Set bonuses: massive Frost/Fire damage |

## Class trainer locations

| City | Faction | Mage trainer NPCs | Notes |
|---|---|---|---|
| **Stormwind** | Alliance | **Wizard's Sanctum** (Mage Quarter, top of tower) — Larimaine Purdue, Marryk Nurribit, Elsharin | Primary Alliance Mage hub |
| **Ironforge** | Alliance | **Hall of Mysteries / Mystic Ward** — Annora (also Enchanting trainer), Gimrizz Shadowcog (Gnome Mage trainer) | Gnome Mages train here |
| Darnassus | n/a | n/a — no NE Mages in 1.12.1 | |
| **Orgrimmar** | Horde | **Valley of Spirits** — Tai'jin (also class quest start NPC for Trolls) `[verify pass 3]` | Troll Mages train here |
| Thunder Bluff | n/a | n/a — no Tauren Mages in 1.12.1 | |
| **Undercity** | Horde | **Magic Quarter** — Rupert Boch, Lexington Mortaim, Sheri Zipstitch (Forsaken Mage trainers) | UD Mages train here |

## VMaNGOS / private server notes

- **Polymorph: Pig** quest in Azshara (Archmage Xylem, Warlord Krellian) is fully scripted on VMaNGOS.
- **Mage's Wand / Celestial Stave** chain (lvl 30/35) including the Uldaman step is fully scripted.
- **Sunken Temple Magecraft chain** at lvl 50 works correctly; Morphaz/Hazzas dragon spawn mechanic is reliable.
- **Arcane Refreshment** at lvl 60 (Lorekeeper Lydros + Hydrospawn) is fully scripted.
- **Robe of the Archmage** recipe is on the world drop table; rare but obtainable.
- **Decurse** is a 1.12-canonical Mage-only ability; works correctly on VMaNGOS. Note: **TBC made decurse a Druid ability shared**, but in 1.12.1 only Mages decurse.
- **Cone of Cold range** in 1.12.1 is 10 yards; Classic 2019 changed to 12 yards. **Engine should respect 1.12.1 range** (10 yd) when planning AoE pulls.

## Decision-Engine Rules

- **id:** `class.mage.race-lock` — IF `Class==Mage && Race NOT IN {Human, Gnome, Undead, Troll}` THEN engine error.
- **id:** `class.mage.icefury-wand` — IF `Class==Mage && Level>=30 && !Items.Contains(IcefuryWand)` THEN run Mage's Wand chain. Priority **600**.
- **id:** `class.mage.celestial-stave` — IF `Class==Mage && Level>=35 && !Items.Contains(CelestialStave)` THEN continue chain. Priority **600**.
- **id:** `class.mage.polymorph-pig` — IF `Class==Mage && Level>=45 && !Spells.Contains(PolymorphPig)` THEN visit Archmage Xylem in Azshara. Priority **400** (cosmetic flex; deferrable). **Don't attempt at lvl 28-30** — Krellian is lvl-49 elite.
- **id:** `class.mage.sunken-temple-trinket` — IF `Class==Mage && Level>=50 && !Items.Contains(FireRuby) && !Items.Contains(ArcaneCrystalPendant)` AND ST run on action menu THEN run Magecraft chain. Priority **620**. Pick **Fire Ruby** for PvP / Pom-Pyro spec; **Arcane Crystal Pendant** for raid healer hybrid usage.
- **id:** `class.mage.arcane-refreshment` — IF `Class==Mage && Level==60 && !Spells.Contains(ConjureWaterRank7)` AND DM access THEN run Lorekeeper Lydros → Hydrospawn chain. Priority **800** (raid water economy gate).
- **id:** `class.mage.respec-fire-or-frost-at-60` — IF `Class==Mage && Level==60 && Role==RaidDPS && CurrentSpec NOT IN {DeepFire, DeepFrost}` THEN respec to either Fire (BWL+ raids on Fire-vulnerable bosses) or Frost (MC + trash farming). Priority **750**. **Respec frequently** — Mages typically pay 50g+ across phases as raid bosses change.
- **id:** `class.mage.water-stockpile` — IF `Class==Mage && Items.ConjuredWaterCount < 80` AND in safe area THEN cast Conjure Water until full stack. Priority **600** (raid prep + party water ration). 1 stack = 20 charges; raid healer uses ~30/raid.
- **id:** `class.mage.mana-gem-cooldown` — IF `Class==Mage && InCombat && ManaGem.NotOnCooldown && CurrentMana < 40%` THEN use Mana Gem. Priority **750** (combat-time).
- **id:** `class.mage.portal-on-request` — IF `Class==Mage && Level>=40 && PortalRequest.Pending` THEN open Portal: <city>. Priority **500** (group utility).
- **id:** `class.mage.decurse-priority` — IF `Class==Mage && Spec.HasDecurse && PartyMember.HasCurse && AfflictionType.IsCurse` THEN cast Remove Lesser Curse. Priority **800** (combat-time).
- **id:** `class.mage.improved-scorch-stack` — IF `Class==Mage && Spec==Fire && Boss.IsFireVulnerable && ScorchStacks < 5 && BossHasImpScorchStackTracker==true` THEN apply Improved Scorch. Priority **820** (raid utility — stack the Fire vulnerability debuff before going Fireball spam).

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `Spells` (planned) — Polymorph, PolymorphPig, ConjureWaterRank7, Counterspell, Decurse, IceBlock, Combustion, ArcanePower, Portal:<city>, Teleport:<city>
- `Items.Contains(itemId)` (planned) — IcefuryWand, CelestialStave, FireRuby, ArcaneCrystalPendant, ManaGem charges
- `Items.ConjuredWaterCount` (planned — sum of Conjured Water stacks in bags)
- `ManaGem.OnCooldown` (planned — boolean; gem is on cooldown after use, ~5 min CD)
- `PortalRequest.Pending` (planned — engine receives external request from group/raid leader)
- `Boss.IsFireVulnerable` / `BossHasImpScorchStackTracker` (planned — boss-encounter flags)
- `ScorchStacks` (planned — derived from boss debuff scan)
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)

## Cross-references

- [decision-engine/per-bracket-actions/03-l20-l30.md](../decision-engine/per-bracket-actions/03-l20-l30.md) — Mage's Wand chain
- [decision-engine/per-bracket-actions/05-l40-l55.md](../decision-engine/per-bracket-actions/05-l40-l55.md) — Sunken Temple Magecraft chain at 50
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Arcane Refreshment at 60
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/priest.md](priest.md) — Power Infusion target rule (Mage as recipient)
- [professions/](../professions/) (pass 6) — Tailoring 300 + Robe of the Archmage recipe
- [systems/](../systems/) (pass 10) — talent system + respec mechanics (Mages respec frequently)
- [pvp/](../pvp/) (pass 8) — Polymorph: Turtle PvP rank 11 reward
