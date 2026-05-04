# Hunter — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Hunter (canonical, modern)
> - https://www.wowhead.com/classic/guide/hunter-class-quests-classic-wow
> - https://www.icy-veins.com/wow-classic/rhok-delar-quest-guide-to-stave-of-the-ancients
> - https://vanilla-wow-archive.fandom.com/wiki/Rhok'delar,_Longbow_of_the_Ancient_Keepers (referenced via search)
> - https://www.warcrafttavern.com/wow-classic/guides/hunter-pets/
> - https://www.icy-veins.com/wow-classic/hunter-dps-pve-spec-builds-talents
> - https://vanilla-wow-archive.fandom.com/wiki/Trueshot_Aura
>
> **Pass 2.** Some details (T0/T0.5 piece-by-boss assignments, Lok'delar exact stats) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Hunters are the **vanilla raid utility / steady-DPS** class. The Marksmanship-spec Hunter brings **Trueshot Aura** (raid-wide +50 ranged AP for the party — but party only, not raid; up to 5-player party scope), **Tranquilizing Shot** (mandatory for Magmadar / Ragnaros / Razorgore — clears enrage), **Hunter's Mark** (debuff slot — +110 ranged AP to all attackers), and **Misdirection** (n/a — TBC). Hunter DPS is sustained-shot-rotation rather than burst, and the class is **tied for highest damage-per-mana in execute phase** thanks to Auto Shot only. Hunter is the **most mobile** raid DPS — kiting is a defining mechanic on Magmadar (fear kite), Princess Huhuran, and several BWL fights.

| Role | Spec | Strength |
|---|---|---|
| Raid DPS | Marksmanship 7/31/13 (or pure 5/30/16, "Aimed Shot Trueshot") | Trueshot Aura + Aimed Shot rotation |
| World / Solo | Beast Mastery 31/0/20 | Strong pet sustain, leveling speed |
| PvP / BG | Survival 0/x/30+ or hybrid Surv 31 | Counterattack stun, Wyvern Sting, Deterrence |
| Leveling | BM 31/0/20 → respec to MM at 60 | Pet tankiness lets hunter quest at 1-2 levels above local mob band |

## Race availability + racial trait synergy

In 1.12.1, **Hunter is restricted to 5 races**: Dwarf, Night Elf, Orc, Tauren, Troll. **No Human, Gnome, or Undead Hunters** in vanilla (Undead Hunters are Cataclysm; Human Hunters are also Cata).

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Dwarf | Alliance | **Gun Specialization +5**, Mace Specialization +5, **Stoneform** (8s bleed/poison/disease immune + 10% armor, 3min CD), Find Treasure | Best Alliance Hunter race for raid DPS — gun spec syncs with Ashjre'thul (BWL Crossbow) — wait that's a crossbow. Gun-specific endgame: **Larvae of the Great Worm** (AQ40 gun) and various MC guns. Stoneform PvP utility. |
| Night Elf | Alliance | **Quickness** (1% dodge), **Shadowmeld** (out-of-combat stealth), **Wisp Spirit**, +5 Bow Specialization (no — wait, NE actually have **+5 Bow Specialization** racial in vanilla? Yes, NEs get bow + bow specialization racial, granting +5 weapon skill with bows.) | Bow synergy. Shadowmeld useful for vanish-pull resets in PvP. Smaller hitbox than Dwarf. |
| Orc | Horde | **Axe Specialization +5**, **Blood Fury** (25% AP buff 15s, -healing taken), Hardiness (25% reduced stun duration), **Command** (+5% pet damage) | **Best Horde DPS race** — Command + Blood Fury stack for solo content; Hardiness mitigates Rogue/Warrior PvP stuns. |
| Tauren | Horde | **Endurance** (+5% base health), **War Stomp** (2s AoE stun, 5y), Cultivation (+15 herbalism), Nature Resistance +10 | War Stomp is a strong raid-wipe-recovery / solo panic-button. Cultivation helps Hunter's preferred Skinning + Herbalism profession pairing. |
| Troll | Horde | **Berserking** (10-30% haste based on missing HP, 10s, ~3min CD), **Bow Specialization +5**, **Throwing Specialization +5**, Beast Slaying (+5% damage to beasts), Regeneration (+10% health regen) | Berserking + Aimed Shot rotation = burst execute window. Bow Specialization is the canonical Horde Hunter racial. **Best Horde Hunter race in raid** when bow itemization (which dominates 1.12 ranged BiS). |

**Engine race-pick rule**: Alliance Hunter → **Dwarf** (gun spec + Stoneform). Horde Hunter → **Troll** (Bow Spec + Berserking) for PvE; Orc for solo/PvP. Tauren is fine but loses ranged-weapon specialization.

## Class quests in level order

| Lvl | Quest / Chain | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| 1-9 | Race-specific starter | Class trainer in starting zone | Starter weapon + initial spells | Auto-accept |
| **10** | **Taming the Beast** chain (5 quests, race-specific) | Class trainer → 3 sequential **Tame Beast** quests using a temporary taming rod on race-specific creatures → final turn-in unlocks **Tame Beast** spell + first permanent pet | Tame Beast spell, first permanent pet | **Highest-priority class identity action in 1-10 bracket.** Without pet, Hunter solo questing is dramatically slower. Priority **990** — suspends all questing. |
| 10 | Auto Shot, Aspect of the Monkey, Concussive Shot trained | Trainer | — | Auto-trained |
| 14 | Aspect of the Cheetah trained | Trainer | Aspect of the Cheetah (movement-speed buff, breaks on hit) | Free outdoor mount until Apprentice Riding at 40 |
| 16 | Hunter's Mark, Multi-Shot, Mongoose Bite | Trainer | — | Auto-trained |
| 20 | Tame Beast / Beast Lore / Eyes of the Beast | Trainer | — | Auto-trained |
| 30 | Aspect of the Pack (party movement-speed) | Trainer | — | Auto-trained |
| 36 | Aspect of the Wild (party Nature Resistance) | Trainer | — | Auto-trained |
| 40 | **Bow / Gun specialization quest** at trainer (Hunters get a weapon-skill quest at 40 — `[verify pass 3]`) | Trainer | Free weapon skill in chosen ranged weapon | Bundle with capital trip |
| 50 | Tranquilizing Shot trained | Trainer | **Tranquilizing Shot** (dispels Frenzy / Enrage on target) | **Mandatory** for Magmadar / Razorgore / many MC bosses. Engine MUST verify learned before scheduling raid. |
| **50** | **The Hunt for Echeyakee / Lupos / etc.** — race-specific rare-pet quests | Various trainers | Specific named pet (Echeyakee = white lion, Lupos = wolf) | Optional flavor; engine ignores unless `AccountFlag.HunterRarePetCollector` |
| **50** | **Quivers / Ammo Pouches** trained | Vendor | Bigger quiver = more ammo + +haste% on ranged | Quiver = +14% ranged haste, ammo pouch = +12-14% — **mandatory** for raid Hunters |
| **60** | **Stave of the Ancients → Rhok'delar / Lok'delar** chain | **Ancient Petrified Leaf** (50% drop from **Majordomo Executus** in Molten Core) → **Vartrus the Ancient** (Felwood) → 4 solo demon fights → **Onyxia** kill (30% drop **Mature Black Dragon Sinew**) → final Lok'delar craft | **Rhok'delar** (epic bow, 144 dps, +14 Agi, +24 ranged AP) AND **Lok'delar** (epic staff, +50 Agi/Spi, hunter-only) `[verify pass 3 exact stats]` | **Most demanding lvl-60 class chain in vanilla.** Requires MC + Onyxia raid kills + 4 brutal solo demon fights with **no pet, no allies** (insta-despawn). Priority **970** at lvl 60. |
| 60 | **Ancient Sinew Wrapped Lamina** quiver upgrade | Optional follow-up after Stave of the Ancients | Epic 18-slot quiver (+15% ranged haste) `[verify pass 3]` | Bundle with Onyxia kills; requires Mature Blue Dragon Sinew (Cobalt Dragonkin in Azshara, or 300g+ AH) |

### The Demon Stalker chain — 4 solo demon fights (lvl 60)

Each demon is a **lvl 62 disguised elite Doomguard**, masquerading as a friendly NPC of varying race. Hunter MUST fight solo — no pet, no allies. Despawn on violation. The 4 demons:

| Demon | Location | Disguise | Mechanic | Hunter Tactic |
|---|---|---|---|---|
| **Artorius the Doombringer** | NE Winterspring (~57, 17) | Tauren named **Artorius the Amiable** | Heavy DoT within 30 yards | Kite at max range with Aspect of the Cheetah; Serpent Sting reapply on tick (Stinging Trauma debuff = massive damage) |
| **Klinfran the Crazed** | W Burning Steppes (~33, 71) | Human named **Franklin the Friendly** | Periodic enrage; melee 1-shots when enraged | Maintain Scorpid Sting; alternate ranged + brief melee to bait reset |
| **Solenor the Slayer** | SW Silithus (~58, 63) | Gnome named **Nelson the Nice** | 15-min hard enrage timer; spawns slow beetles | Reset enrage via **Frost Trap** / Scatter Shot kiting; use terrain LoS |
| **Simone the Seductress** | W Un'Goro Crater (~38, 72) | Troll named **Simone the Seductress** (still named Simone) | Starts with a pet (Cobra-like); lightning + curse spam | **Freezing Trap** the pet on engage; **Viper Sting** silence + Mind-Numbing Poison via Serpent Sting on Simone |

**Engine planning rule**: Schedule the 4 demons in order of difficulty (Artorius easiest → Solenor hardest typically); allow 1-2 attempts per demon for gear / consumable adjustment; bring full consumables (Major Mana Pots, Free Action Potions, Greater Stoneshield Potions, Limited Invulnerability Potion). Cost: ~30-40g per attempt in consumables.

## Pet system (1.12)

**Three pet families** in 1.12.1: Cunning, Tenacity, Ferocity (these are the original 1.x family groupings; some sources use different names — TBC introduced the modern 3-family + spec pet system).

Vanilla pet mechanics:
- **Loyalty**: 1-6 (Best Friend = 6). Pets gain loyalty by happy state + feeding. Loyalty determines training-point cap.
- **Training Points (TP)**: Earned by killing mobs while pet is active. Spent on pet abilities at trainer (Growl, Charge, Bite, Claw, Cower, etc.).
- **Pet stable**: Stable Master in capital + each major city; **2 stable slots** in 1.12.1 (TBC expanded to more). Plus 1 active pet = max 3 pets total.
- **Happiness**: Pet must be fed (correct food type per beast family). Hungry pet → unhappy → low loyalty → flees.
- **Feeding**: Use Feed Pet on a food item the pet's family eats (Carnivores eat meat, Herbivores eat fruit/bread, etc.).

**Notable pets** (lvl 60 raid use):
- **Cat** family (Ferocity-equivalent) — **Broken Tooth** (lvl 37 named cat in Badlands) has a 1.0s base attack speed = highest DPS pet at 60 by ~15-20%. **Vanilla-defining pet pickup.**
- **Boar / Bear** (Tenacity) — high health, used for solo/leveling tank role
- **Wolf** — 3% damage party buff (Furious Howl, similar to Battle Shout) — meta-relevant for raid DPS
- **Cunning** family (Owl, Bat, Carrion Bird) — utility (dive, screech)

**Engine pet-acquisition rule**: At lvl 37, a Hunter's PvE-DPS plan requires **Broken Tooth** (Badlands ~33,52, lvl 37 elite cat — actually not elite, just rare spawn ~1-3 hour respawn). Engine should add Broken Tooth tame to the action menu with priority **750** for raid-DPS Hunters.

## Talent trees (1.12 51-point trees)

### Beast Mastery (Bestial Wrath capstone)

Pet-focused. The 31-pt capstone **Bestial Wrath** = pet immune to fear/incapacitate/stun, +50% damage for 18s, 2-min CD.

Key talents: **Improved Aspect of the Hawk** (5/5 — chance to gain ranged haste on shot), **Endurance Training** (5/5 — pet HP), **Improved Eyes of the Beast** (3/3), **Improved Aspect of the Monkey** (3/3 — dodge), **Thick Hide** (3/3 — pet armor), **Pathfinding** (3/3 — Aspect of the Cheetah/Pack +15% speed), **Bestial Swiftness** (1/1 — pet +30% speed), **Unleashed Fury** (5/5 — pet damage), **Improved Bestial Wrath / Frenzy** (5/5), **Ferocity** (5/5 — pet crit), **Spirit Bond** (2/2 — pet+master +1% HP regen/3s), **Intimidation** (1/1 — pet stuns target), **Bestial Discipline** (2/2 — pet focus), **Bestial Wrath** (1/1 — 31-pt capstone).

### Marksmanship (Trueshot Aura capstone)

Ranged-shot focused. The 31-pt capstone **Trueshot Aura** = +50 ranged AP to party (5-player party scope, NOT 40-man raid).

Key talents: **Improved Concussive Shot** (3/3 — daze proc chance), **Efficiency** (5/5 — -5% mana cost on shots), **Improved Hunter's Mark** (5/5 — +30 ranged AP to debuff), **Lethal Shots** (5/5 — +5% ranged crit), **Aimed Shot** (1/1 — instant casted shot, 3s cast, high damage; **rotation defining**), **Improved Arcane Shot** (5/5 — +30% damage), **Hawk Eye** (3/3 — +6 yd range), **Mortal Shots** (5/5 — +30% ranged crit damage), **Scatter Shot** (1/1 — 4s incapacitate, breaks on dmg), **Barrage** (3/3 — Multi-Shot/Volley damage), **Ranged Weapon Specialization** (5/5 — +5% ranged damage), **Trueshot Aura** (1/1 — 31-pt capstone).

### Survival (Wyvern Sting capstone)

Trap-focused + melee crit. The 31-pt capstone **Wyvern Sting** = 12s sleep on humanoid; on dispel/expire → 12s damage DoT.

Key talents: **Monster Slaying** (3/3 — +3% damage to beasts/giants/dragonkin), **Humanoid Slaying** (3/3 — +3% damage to humanoids), **Deflection** (5/5 — parry), **Entrapment** (5/5 — trap roots target), **Savage Strikes** (2/2 — +20% melee crit), **Counterattack** (1/1 — riposte 5s root + damage), **Improved Wing Clip** (3/3 — proc 25% root), **Lightning Reflexes** (5/5 — agility), **Survivalist** (5/5 — stamina), **Killer Instinct** (3/3 — crit), **Deterrence** (1/1 — 10s parry/dodge cooldown), **Wyvern Sting** (1/1 — 31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **MM raid DPS (Trueshot)** | **5/30/16** or **7/31/13** — Imp Aspect of the Hawk 5 + (BM) → Imp Hunter's Mark 5 + Lethal Shots 5 + Aimed Shot 1 + Imp Arcane Shot 5 + Mortal Shots 5 + Scatter Shot 1 + Barrage 3 + Ranged Weapon Spec 5 + Trueshot Aura 1 (MM 31 → Trueshot) → 5/13 split for sub-talents | MC/BWL/AQ40/Naxx primary spec | Trueshot Aura is **mandatory** even with multiple Hunters in raid; Aimed Shot ↔ Multi-Shot rotation. |
| **BM solo / leveling / world** | **31/0/20** | Solo questing, world farming, AQ20 trash farming | Pet sustain + Bestial Wrath burst; pets stay alive against +2 / +3 elites |
| **Survival PvP / BG** | **0/14/37** | BG ladder / duels | Wyvern Sting + Counterattack + Scatter Shot = double-CC, deterrence reset; melee viable |
| **Leveling: BM splash** | 5/15/0 → 31/0/20 by 50 → respec MM at 60 | Solo → raid pivot | Standard leveler path; Engine should plan ~50g respec at 60 |

## Recommended weapons by bracket

| Bracket | Ranged | Melee 1H/2H | Notes |
|---|---|---|---|
| 10-25 | Vendor bow/gun (whatever race specializes in) | 2H stick (Staff of Westfall A / Crescent Staff H) | Stat sticks |
| 25-40 | Quest bow rewards from Crossroads / Westfall / Loch Modan / Darkshore | 1H/2H per quest reward | Don't over-invest |
| 40-50 | **Heavy Crossbow of the Black Howl** (BS-crafted, lvl 40, BoE) | Stat 1H sticks | The Crossbow is a long-runtime ranged weapon |
| 50-58 | **Hunter's Bow of the Wolf / of the Eagle** (BoE, AH) — stat-stick bows | Stat-stick weapons | |
| 58-60 | **Bonechewer** (UBRS lvl 58 elite Death Knight Drak, drop) / **Hurricane** (Strat Live quest reward / drop) `[verify pass 3]` | Stat-stick | Pre-raid BiS bow |
| 60 (post-MC) | **Striker's Mark** (Onyxia quest reward — Alliance Marshal Windsor / Horde equivalent) `[verify]` | Stat sticks | |
| 60 (post-Demon Stalker) | **Rhok'delar, Longbow of the Ancient Keepers** (epic, 144 dps) | Lok'delar staff for Spirit/Agi stat-stick alt | **Class-defining ranged weapon** |
| 60 (post-AQ40) | **Larvae of the Great Worm** (gun) / Ashjre'thul (BWL crossbow) | | |
| 60 (post-Naxx) | **Cryptstalker** set-supporting ranged | | |

**Quiver / Ammo Pouch slot** (vanilla-only, removed in TBC): A quiver gives +12-15% ranged haste; an ammo pouch gives +12-14% (used by gun-Hunters). **MUST equip best available** — engine rule: prefer 18-slot Quiver of the Night Watch (MC drop) > 16-slot Heavy Quiver > 14-slot. Ammo carried in this slot doesn't take regular bag space.

**Ammo**: Hunters consume ammo per shot. Vendor ammo (Solid Arrow/Solid Shot) at lvl 50; Engineering Goblin spec crafts higher-tier ammo (Thorium Headed Arrow). Engine must keep ≥1000 ammo in quiver/pouch for any raid action.

## Pre-raid BiS gear (MM raid Hunter, MC entry)

`[verify pass 3 for exact item IDs and drop bosses]`

| Slot | Item | Source |
|---|---|---|
| Head | **Stalker's Helm** (T0.5) / Hyperion Helm (PvP rank 8) | Quest upgrade or PvP |
| Neck | **Mark of Fordring** (EPL) | Quest |
| Shoulders | **Truestrike Shoulders** | LBRS Mor Grayhoof |
| Cloak | **Cape of the Black Baron** (BS-crafted) | BoE |
| Chest | **Beaststalker Tunic** (T0) → upgrade to T0.5 | Strat UD Baron |
| Bracers | **Wristbands of True Flight** | LBRS Halycon |
| Hands | **Devilsaur Gauntlets** (LW BoE) | |
| Belt | **Wolfshear Leggings** — n/a, that's Druid; for Hunter: **Belt of Preserved Heads** | Strat Baron |
| Legs | **Devilsaur Leggings** | LW BoE |
| Feet | **Battlechaser's Greaves** | DM E |
| Ring 1 | **Magni's Will** / Don Julio's Band | Quest / AV exalted |
| Ring 2 | **Painweaver Band** | Strat UD Maleki |
| Trinket 1 | **Hand of Justice** | BRD Lord Roccor / chest |
| Trinket 2 | **Blackhand's Breadth** / Rune of the Guard Captain | UBRS Drakkisath / Strat Live Magistrate Barthilas |
| Ranged | **Bonechewer** / **Hurricane** | UBRS / Strat Live |
| MH | **Sang'thraze the Deflector** (1H stat stick) | ZF |
| OH | **Hand of Antu'sul** | ZF |
| Quiver | **Heavy Quiver** (14-slot) → upgrade to 18-slot quest reward | |

## Tier set progression

| Tier | Set name (Hunter) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Beaststalker Armor** | 8-piece, 60 5-mans | Drop-only, BoP |
| **T0.5 (Dungeon Set 2)** | **Beastmaster Armor** | Quest upgrade chain (patch 1.10) | Stat bridge to T1 |
| **T1** | **Giantstalker Armor** | Molten Core (8-piece — helm Onyxia, chest Sulfuron Harbinger, etc.) `[verify pass 3]` | Set bonus: +10 stam 4-piece, +1% ranged hit 6-piece, etc. |
| **T2** | **Dragonstalker Armor** | BWL + Onyxia (8-piece — helm Nefarian, chest Onyxia, etc.) `[verify pass 3]` | Iconic helm with raptor-head; set bonus: ranged hit + AP |
| **T2.5** | **Striker's Garb** (Glory of the Defender quest) / **Stalker's Battlegear** | AQ40 — token-based (Qiraji Bindings) | |
| **T3** | **Cryptstalker Armor** | Naxx40 — 9-piece token-based | Set bonuses include massive ranged AP + threat reduction |

## Class trainer locations

| City | Faction | Hunter trainer NPCs | Notes |
|---|---|---|---|
| **Ironforge** | Alliance | **Hall of Arms** — Ulbrek Firehand, Thorgas Grimson, Grif Wildheart | Dwarves train here |
| **Darnassus** | Alliance | **Cenarion Enclave** — Jeen'ra Nightrunner, Jocaste, Aayndia Floralwind | NEs train here |
| Stormwind | n/a | n/a — no Human/Gnome Hunters in 1.12.1 | |
| **Orgrimmar** | Horde | **Valley of Honor** — Sian'tsu, Hawkeye Frizcrank, Ormak Grimshot | Orcs/Trolls train here |
| **Thunder Bluff** | Horde | **Hunter Rise** — Ahanu, Holt Thunderhorn, Urek Thunderhorn | Tauren train here |
| Undercity | n/a | n/a — no Undead Hunters in 1.12.1 | |

**Stable Masters** (handle pet stable slots, 2 in vanilla): Goldshire (Lindea Rabonne), Razor Hill, Stranglethorn (Booty Bay), Tarren Mill, Camp Mojache, Hillsbrad, Stonebull Lake (Mulgore), Ironforge (Ulbrek), Orgrimmar.

## VMaNGOS / private server notes

- **Taming the Beast** chain at lvl 10 is fully scripted and works on VMaNGOS.
- **Tame Beast** spell, pet loyalty/happiness mechanics work correctly.
- **Trueshot Aura** = +50 ranged AP, party-only (5-player); confirm core matches retail 1.12.1 not Classic 2019 (which extended to raid scope at some point — `[verify pass 3]`).
- **Demon Stalker chain** — the 4 demon disguised-NPC mechanics (talk to friendly NPC → reveals as Doomguard → 1v1 fight) works on VMaNGOS with **occasional bugs** when players try to interact with the disguised NPC while in combat. Solo-only enforcement (despawn on group nearby) is reliably enforced.
- **Broken Tooth** in Badlands has a long-and-variable spawn timer on VMaNGOS (1-3+ hours); rare spawn behavior is correct vs retail.
- **Quiver / Ammo Pouch +haste** is correctly applied. Quivers do NOT exist in Classic 2019 re-release (changed to bag-equivalents) — VMaNGOS retains 1.12.1 behavior.

## Decision-Engine Rules

- **id:** `class.hunter.race-lock` — IF `Class==Hunter && Race NOT IN {Dwarf, NightElf, Orc, Tauren, Troll}` THEN engine error.
- **id:** `class.hunter.taming-the-beast` — IF `Class==Hunter && Level>=10 && !Spells.Contains(TameBeast)` THEN run Taming the Beast 5-quest chain. Priority **990** (highest class identity in 1-10/10-20 brackets). Suspends questing. Pet acquisition at the end.
- **id:** `class.hunter.broken-tooth` — IF `Class==Hunter && Level>=37 && Spec IN {MM, RaidDPS} && !PetTamed.Contains(BrokenTooth)` AND in Badlands THEN attempt Broken Tooth tame. Priority **750**. Variable wait (1-3 hours) — engine plans alongside other Badlands/Searing Gorge questing.
- **id:** `class.hunter.tranq-shot` — IF `Class==Hunter && Level>=50 && !Spells.Contains(TranquilizingShot)` THEN visit trainer immediately. Priority **900** at lvl 50 (mandatory raid skill).
- **id:** `class.hunter.demon-stalker` — IF `Class==Hunter && Level==60 && Items.Contains(AncientPetrifiedLeaf) && !Spells.Contains(LokDelar)` THEN start the chain. Priority **970**. **Multi-week effort**: 4 solo demons + Onyxia 30%-drop sinew. Engine plans solo demon attempts during off-raid hours.
- **id:** `class.hunter.demon-stalker.demon-i` — sub-rule per demon, with consumable cost prep (bring Free Action Potions, Greater Stoneshield, Limited Invul, Major Mana). Priority **930** when engaging.
- **id:** `class.hunter.quiver-or-pouch` — IF `Class==Hunter && Level>=20 && !RangedAmmoSlot.IsQuiverOrPouch` THEN equip best available. Priority **800** (raid-mandatory hidden ranged-haste boost). Distinct from other ammo-related rules.
- **id:** `class.hunter.ammo-stock` — IF `Class==Hunter && AmmoCount < 1000 && in capital` THEN buy ammo to top off (Solid Arrow / Solid Shot from vendor; or self-craft Thorium Headed Arrow if Engineering 250+ Goblin spec). Priority **600**.
- **id:** `class.hunter.feed-pet` — IF `Class==Hunter && Pet.Happiness < Happy && in safe area` THEN Feed Pet. Priority **800** (pet damage drops dramatically when unhappy).
- **id:** `class.hunter.pet-train` — IF `Class==Hunter && Pet.UnspentTrainingPoints > 0 && in capital with pet trainer` THEN spend TP at pet trainer. Priority **500**.
- **id:** `class.hunter.respec-mm-at-60` — IF `Class==Hunter && Level==60 && Role==RaidDPS && CurrentSpec != MM` THEN respec to 5/30/16 (or 7/31/13) MM. Priority **750**.

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `QuestsCompleted` (existing) — Taming the Beast, Demon Stalker chain quest IDs `[verify pass 3]`
- `Spells` (planned) — TameBeast, TranquilizingShot, Aimed Shot, Trueshot Aura, Lok'delar (the staff is summoned — actually it's an item not a spell), Bestial Wrath, etc.
- `PetTamed` (planned) — list of pet identifiers (with their families and stats including Broken Tooth tag)
- `Pet.Active.Happiness` / `Pet.Active.Loyalty` / `Pet.Active.UnspentTP` (planned)
- `Items.Contains(itemId)` (planned) — Ancient Petrified Leaf, Mature Black Dragon Sinew, Mature Blue Dragon Sinew, Rhok'delar, Lok'delar
- `RangedAmmoSlot.IsQuiverOrPouch` (planned) — derived from equipped item
- `AmmoCount` (planned) — sum of ammo stack in equipped quiver/pouch
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)

## Cross-references

- [decision-engine/per-bracket-actions/01-l1-l10.md](../decision-engine/per-bracket-actions/01-l1-l10.md) — Taming the Beast lands here at lvl 10
- [decision-engine/per-bracket-actions/04-l30-l40.md](../decision-engine/per-bracket-actions/04-l30-l40.md) — Broken Tooth tame at 37
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Demon Stalker chain at 60
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/warrior.md](warrior.md) — shared mail/leather pre-raid items overlap
- [raids/](../raids/) (pass 5) — Onyxia + MC for the Demon Stalker prerequisites
- [professions/](../professions/) (pass 6) — Engineering Goblin spec for Thorium Headed Arrow ammo
