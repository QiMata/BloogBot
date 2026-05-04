# Rogue — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Rogue (canonical, modern)
> - https://www.icy-veins.com/wow-classic/rogue-quests-in-wow-classic
> - https://www.wowhead.com/classic/guide/rogue-class-quests-classic-wow
> - https://www.warcrafttavern.com/wow-classic/guides/rogue-poisons-guide/
> - https://www.warcrafttavern.com/wow-classic/guides/lockpicking-1-300/
> - https://vanilla-wow-archive.fandom.com/wiki/Rogue/Quests (referenced via search)
>
> **Pass 2.** Some details (T0/T0.5 piece-by-boss, Lord Jorach Ravenholdt chain exact rewards) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Rogues are the **highest sustained single-target melee DPS** class for stationary fights in 1.12.1. Combat Swords with proper world-buff stack tops most parses on Patchwerk/Loatheb-like fights. Rogues bring **mandatory raid utility** beyond DPS:

- **Distract** + **Sap** + **Pick Lock** — only class that handles BRD vault mechanics, MC trash skipping, Onyxia mass-deaggro, and various BC-era prep tools that originate in 1.12
- **Improved Expose Armor** — 5-stack armor reduction debuff (3000 armor in 1.12, mandatory melee buff)
- **Pickpocket** — passive gold income from humanoids; Junkbox drops fund mount/respec costs
- **Lockpicking** — opens BoE/BoP lockboxes (massive AH gold lever; an alt-rogue-with-300-Lockpicking is a known account-level cash farm)
- **Sap** — single most useful CC for instances (humanoids only, but most 5-man trash is humanoid)

| Role | Spec | Strength |
|---|---|---|
| Raid DPS | Combat Swords 15/31/5 (or Combat 31 + Daggers splash) | Highest single-target sustained DPS in raid |
| World / PvP | Subtlety / Hemo 21/8/22 | Premeditation + Hemorrhage + Preparation cheese |
| Ganking / Burst | Assassination 31/8/12 (Cold Blood Eviscerate / Backstab) | Top opener burst |
| Leveling | Combat 5/30/16 | Easy gold via Pickpocket; simple SS-rotation |

## Race availability + racial trait synergy

In 1.12.1 **Rogue is restricted to 7 races** — all but **Tauren**. (No Draenei or Blood Elves in 1.12.1.)

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Human | Alliance | **Sword Spec +5**, **Mace Spec +5**, Diplomacy, Perception | Best Combat Swords raid race — sword spec syncs with Brutality Blade + Hand of Justice + Hand of Rag itemization |
| Dwarf | Alliance | **Mace Spec +5**, **Gun Spec +5**, **Stoneform** (8s bleed/poison/disease immune + 10% armor, 3min CD), Find Treasure | Mace niche; Stoneform PvP utility (purges Rogue-on-Rogue bleed/poison) |
| Night Elf | Alliance | **Quickness** (1% dodge), **Shadowmeld** (out-of-combat stealth — effectively a 2nd vanish), **Wisp Spirit**, +5 Bow Spec | Shadowmeld is a **defining PvP racial** for the class. Best NE-only combo: Stealth → Shadowmeld → reposition → Stealth again. |
| Gnome | Alliance | **Escape Artist** (root break, 27s CD), **Engineering Specialist** (+15 Engineering), **Expansive Mind** (5% int), small hitbox | Escape Artist breaks Frost Nova/Druid roots; Engineering synergy for Goblin Sapper / Rocket Boots |
| Orc | Horde | **Axe Spec +5**, **Blood Fury** (25% AP buff 15s, -healing taken), **Hardiness** (25% reduced stun duration), Command | **Best Horde DPS race** — Hardiness counters Rogue-on-Rogue stun chain; Blood Fury syncs with Cold Blood opener |
| Troll | Horde | **Berserking** (10-30% haste based on missing HP, 10s, ~3min CD), Bow/Throwing Spec, Beast Slaying, Regeneration | Berserking + Cold Blood = burst window. **Best Horde Combat Daggers race** when low-HP. |
| Undead | Horde | **Will of the Forsaken** (5s fear/sleep/charm immune, 2min CD), Cannibalize, Underwater Breathing, Shadow Resistance +10 | **Canonical PvP Rogue race** — WotF is the single most-impactful PvP racial in vanilla, especially vs Warlocks/Priests. Shadow Resist 10 helps vs Shadow Priests. |

**Engine race-pick rule**: Alliance Rogue raid → **Human** (Sword Spec). Horde Rogue raid → **Orc** (Hardiness). PvP rogue (any side) → **Undead** (WotF) or NE (Shadowmeld). World/ganking → **Troll** (Berserking burst).

## Class quests in level order

| Lvl | Quest / Chain | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| 1-9 | Race-specific starter | Class trainer in starting zone | Starter weapon | Auto-accept |
| 10 | Stealth, Sap, Pick Pocket trained | Trainer | Stealth Rank 1, Pick Pocket, Sap | Auto-trained |
| **16** | **Pick Lock** trained | Trainer | Pick Lock skill (Lockpicking 1) | Trainer-only (no quest). **Major class identity unlock** — gates Heavy Junkbox AH economy. |
| **20** | **Mathias and the Defias** chain (Alliance) / **The Shattered Salute → Deep Cover** chain (Horde) | Alliance: **Master Mathias Shaw** (SI:7, Stormwind Old Town) → **Agent Kearnen** (Westfall) → **Klaven's Tower** (Westfall) → return. Horde: **Shenthul** (Orgrimmar Cleft of Shadow) → Flare Gun event in **Barrens** (Crossroads area) | **Recipe: Thistle Tea** (rogue-only consumable, restores 100 energy on use) + **ability to use Poisons** | **Class-defining unlock.** Without Poisons, Rogue raid DPS is ~25% below par. Priority **950**. **Prerequisite: Lockpicking 75** (so the engine must Pick Lock-grind earlier). |
| 22 | Vanish, Cheap Shot, Kick rank-ups | Trainer | — | Auto-trained |
| 24 | Blind trained | Trainer | **Blind** (10s incapacitate, breaks on damage; uses Blinding Powder reagent) | Auto-trained, but engine must keep Blinding Powder in bags |
| 26 | Distract trained | Trainer | Distract | Auto-trained |
| 30 | Sprint, Cold Blood (talent gate at 30 if specced) | Trainer | Sprint Rank 1 | Auto-trained |
| 40 | Ambush, Hemorrhage (talent gate), Preparation (talent gate) | Trainer | — | Auto-trained |
| 50 | **A Simple Request → Ravenholdt chain** | Any Rogue trainer → **Lord Jorach Ravenholdt** (Hillsbrad Foothills, Ravenholdt Manor) → Pickpocket items in Azshara → quest tasks in Sunken Temple | **Whisperwalk Boots** (high-end leather feet) + **Duskbat Drape** (cloak) `[verify pass 3]` | Bundle with planned Sunken Temple run. |
| 50 | Wound Poison rank-ups, Mind-Numbing Poison, Crippling Poison rank-ups | Trainer | Higher poison ranks | Auto-trained |
| 60 | Trainer rank-ups for Sinister Strike / Eviscerate / Backstab / Slice and Dice / Rupture Rank 4 / etc. | Trainer | Rank 4-6 spells | Auto-trained |
| 60 | **No epic class weapon quest** — Rogue is the *only* class without a lvl-60 epic class quest reward like Quel'Serrar / Charger / Rhok'delar / Benediction. | n/a | n/a | Engine notes this as a class-identity gap for raid prep — Rogues rely on world drops + dungeon items, not class chains. |

### Lockpicking 1-300 (Rogue-only profession)

Lockpicking is a Rogue-class skill, not a profession in the trade-skill sense. Skill scales 1-300 mirror weapon skill (5 × level cap).

| Skill | Source | Notes |
|---|---|---|
| 1 | Trained at lvl 16 | Trainer-only; cost ~10s |
| 25-50 | Locked Footlockers, Practice Lock targets | Practice locks in trainer rooms |
| 75 | **Required for lvl-20 Poisons chain** | Engine must verify before scheduling Poisons |
| 100-150 | World junkboxes (lvl 20-35 humanoids) | Sturdy Junkboxes |
| 150-200 | Worn Junkboxes (lvl 35-45 humanoids) | |
| 200-250 | Heavy Junkboxes (lvl 45-55 humanoids — particularly Stranglethorn pirates) | High AH value for the contents (cloth, gold, BoE blue chance) |
| 250-300 | Reinforced Junkboxes / Sturdy Junkboxes / Practice Locks in BRD | Final 25 points often via BRD Relic Coffer doors (12 doors = 12 free skill-up attempts per BRD run) |
| **300** | Cap | Required for some BWL chests (Tightly Sealed Trunk) and select Naxx-tier locks `[verify pass 3]` |

**Engine pickpocket rule**: Pickpocket every humanoid mob the bot kills. Junkboxes are auto-mailed to alts for Lockpicking grinds and AH listing. **Estimated 5-15g/hour passive income** from steady pickpocketing during questing/grinding.

## Talent trees (1.12 51-point trees)

### Assassination (Cold Blood capstone — wait, no — Cold Blood is at 21 Assa; the 31-pt Assa capstone is **Vigor**)

Actually, let me be precise: in 1.12 the Assassination 31-pt capstone was **Vigor** (+10 max energy). Cold Blood is mid-tree. The deep-Assa burst spec uses Cold Blood + Eviscerate / Backstab.

Key talents: **Improved Eviscerate** (3/3), **Remorseless Attacks** (2/2 — +20% crit chance on next ability after killing blow), **Malice** (5/5 — +5% crit), **Ruthlessness** (3/3 — combo points retain after kill — wait, Ruthlessness is +20% chance to keep finishing-move CP after kill — actually that's "20% chance combo point retained after finishing move kill"), **Murder** (2/2 — +2% damage to humanoid/beast/etc.), **Improved Slice and Dice** (3/3), **Lethality** (5/5 — +30% crit damage on combo abilities), **Vile Poisons** (5/5 — +25% poison damage), **Improved Poisons** (5/5 — +20% poison proc chance), **Cold Blood** (1/1 — next attack 100% crit), **Improved Kidney Shot** (3/3), **Vigor** (1/1 — 31-pt capstone).

### Combat (Adrenaline Rush capstone)

The most-played raid spec. The 31-pt capstone **Adrenaline Rush** = +100% energy regen for 15s, 5-min CD.

Key talents: **Improved Gouge** (3/3), **Improved Sinister Strike** (2/2 — -3 energy on SS), **Lightning Reflexes** (5/5 — +5% dodge), **Improved Backstab** (3/3 — +30% BS crit), **Deflection** (5/5 — +5% parry), **Precision** (5/5 — +5% hit chance — **mandatory for raid hit cap**), **Endurance** (2/2 — Sprint/Evasion CD), **Riposte** (1/1 — counter-attack after parry), **Improved Sprint** (2/2 — root immunity on Sprint), **Improved Kick** (2/2 — Kick silences caster), **Dagger Specialization** (5/5 — +5% crit with daggers — **only for Combat Daggers**), **Sword Specialization** (5/5 — chance to gain extra attack on swing — **the defining Combat Swords talent**), **Mace Specialization** (5/5 — chance to stun on swing — niche), **Fist Specialization** (5/5), **Aggression** (3/3 — +6% Sinister Strike + Eviscerate damage), **Weapon Expertise** (2/2 — +5 weapon skill), **Blade Flurry** (1/1 — extra cleave attack on adjacent target for 15s, 2-min CD — strong on multi-target trash), **Adrenaline Rush** (1/1 — 31-pt capstone).

### Subtlety (Premeditation capstone)

PvP-focused. The 31-pt capstone **Premeditation** = adds 2 combo points to target on use, free, 1-min CD.

Key talents: **Master of Deception** (5/5 — +15 stealth level), **Opportunity** (5/5 — +20% backstab/ambush/garrote damage), **Camouflage** (5/5 — +15% stealth movement speed), **Initiative** (3/3 — +75% chance to add combo on stealth opener), **Improved Sap** (3/3 — +90% Sap success in combat — wait Sap is OOC only), **Elusiveness** (5/5 — Vanish/Blind/Cloak CD reduction), **Setup** (3/3 — +60% chance combo on dodge), **Ghostly Strike** (1/1 — +125% weapon damage, 50% dodge for 7s, 20s CD), **Hemorrhage** (1/1 — physical damage debuff, builds CP), **Deadliness** (5/5 — +10% AP from finishing moves), **Heightened Senses** (2/2 — +6% to detect stealth + 6 yards), **Preparation** (1/1 — resets Vanish/Sprint/Cold Blood/Evasion CDs, 10-min CD), **Premeditation** (1/1 — 31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Combat Swords (raid)** | **15/31/5** — Imp Eviscerate 3 / Malice 5 / Murder 2 / Imp SnD 3 / Lethality 2 → 15 Assa; Imp Gouge 3 / Imp SS 2 / Lightning Reflexes 5 / Precision 5 / Sword Specialization 5 / Aggression 3 / Weapon Expertise 2 / Blade Flurry 1 / Adrenaline Rush 1 → 31 Combat; Master of Deception 5 → 5 Sub | Canonical raid DPS spec | Sword Specialization is the single highest-DPS talent in the game for the spec. |
| **Combat Daggers (raid alt)** | **20/31/0** | Higher DPS than Swords if you have BiS daggers (e.g., Perdition's Blade from MC, Distracting Dagger from BWL); requires positional Backstab | More gear-dependent |
| **Hemorrhage / Sub PvP** | **15/3/33** — Imp Eviscerate 3 / Malice 5 / Lethality 5 / Cold Blood 1 → 15 Assa; Lightning Reflexes 3 → 3 Combat; deep Sub: Master of Deception 5 / Opportunity 5 / Camouflage 5 / Initiative 3 / Setup 3 / Hemorrhage 1 / Deadliness 5 / Premeditation 1 → 33 Sub | BG / world PvP | Hemorrhage stacking debuff + Premeditation 2-CP burst openers |
| **Assassination burst** | **31/8/12** — deep Assa to Cold Blood + Vigor; 8 Combat for Precision 5; 12 Sub for Master of Deception 5 + Opportunity | World ganking | One-shot windows: Stealth → Cheap Shot → Cold Blood + Eviscerate / Ambush; +10 max energy from Vigor for follow-up |
| **Leveling Combat** | 0/30/0 → 5/30/16 by 60 | Solo questing | Sinister Strike + Eviscerate; Pickpocket every humanoid for gold |

## Recommended weapons by bracket

| Bracket | MH | OH | Notes |
|---|---|---|---|
| 10-25 | Dagger from Westfall / WC | Off-hand dagger or empty | Stealth opener viability requires dagger MH |
| 25-40 | **Smite's Mighty Hammer** (Deadmines) for Combat Daggers cleave / **The Hand of Antu'sul** (ZF, lvl 47 1H sword) | Off-hand 1H sword/dagger | |
| 40-50 | **Witchblade** (Maraudon, lvl 47, slow 1H sword) for Sword spec / **Distracting Dagger** (n/a, that's BWL) — pre-raid: any agility-1H | | |
| 50-58 | **Sandfury Cleaver** (ZF, axe — for Orc Axe Spec rogues) / **Sang'thraze the Deflector** (ZF, 1H sword) | OH stat-stick | |
| 58-60 | **Brutality Blade** (BRD Lord Roccor, 1% drop — pre-raid BiS MH for Combat Swords) / **Hand of Justice** trinket procs | **Mirah's Song** (BRD Princess, fast 1H sword) / **Dal'Rend's Tribal Guardian** (UBRS Drakkisath) | |
| 60 (raid entry) | Brutality Blade or Dal'Rend's Sacred Charge | Mirah's Song | |
| 60 (post-MC) | **Quel'Serrar** (DM tribute event 1H sword — see [warrior.md](warrior.md)) — viable for tank-rogue niche / **Vis'kag the Bloodletter** (AQ40 1H sword) — endgame | | |
| 60 (post-BWL) | **Ashkandi, Greatsword of the Brotherhood** (no — that's 2H, not for rogue) / **Maladath, Runed Blade of the Black Flight** (BWL, 1H sword) | | |
| 60 (post-AQ40) | **Kingsfall** (AQ40 dagger, BiS dagger spec) / **Death's Sting** (AQ40 1H sword) | | |
| 60 (post-Naxx) | **The Hungering Cold** / **Death's Sting** | | |

**Vanilla rogue weapon-skill rule**: Combat Swords requires sword skill cap (300 weapon skill = 305 effective with Human/Dwarf — wait, sword spec is +5, so Human Combat Swords has 305 effective sword skill, which converts ~5% glancing-blow damage into normal hits and adds ~1.4% hit). **+5 weapon-spec is the single biggest reason Human is the canonical Alliance Rogue.**

## Pre-raid BiS gear (Combat Swords focus, MC entry)

`[verify pass 3 for exact item IDs]`

| Slot | Item | Source |
|---|---|---|
| Head | **Lionheart Helm** (BS-crafted, BoE) — same as Warrior; or **Eye of Rend** (UBRS Warmaster Voone) | BS recipe drop / UBRS |
| Neck | **Mark of Fordring** | EPL quest |
| Shoulders | **Truestrike Shoulders** | LBRS Mor Grayhoof |
| Cloak | **Cape of the Black Baron** (BS-crafted, BoE) | |
| Chest | **Cadaverous Armor** (Strat) | |
| Bracers | **Wristbands of True Flight** | LBRS Halycon |
| Hands | **Devilsaur Gauntlets** (LW BoE) | |
| Belt | **Belt of Preserved Heads** | Strat Baron |
| Legs | **Devilsaur Leggings** (LW BoE) | |
| Feet | **Whisperwalk Boots** (Ravenholdt class quest reward at lvl 50) / **Battlechaser's Greaves** (DM E) | Class quest / DM |
| Ring 1 | **Magni's Will** / Don Julio's Band (AV Exalted) | Quest / AV exalted |
| Ring 2 | **Painweaver Band** | Strat UD Maleki the Pallid |
| Trinket 1 | **Hand of Justice** | BRD Lord Roccor / chest |
| Trinket 2 | **Blackhand's Breadth** | UBRS Drakkisath quest reward |
| MH | **Brutality Blade** (BRD, 1% drop) | BRD Lord Roccor |
| OH | **Mirah's Song** | BRD Princess |
| Ranged | **Heavy Crossbow of the Black Howl** (BS BoE) | |

## Tier set progression

| Tier | Set name (Rogue) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Shadowcraft Armor** | 8-piece, 60 5-mans | Drop-only, BoP. Set bonus 8-piece: +5% chance to gain energy on melee crit `[verify pass 3]` |
| **T0.5 (Dungeon Set 2)** | **Darkmantle Armor** | Quest upgrade chain (patch 1.10) | Stat bridge to T1 |
| **T1** | **Nightslayer Armor** | Molten Core (8-piece — helm Onyxia, chest Sulfuron Harbinger, etc.) `[verify pass 3]` | Set bonus: stam + parry + crit |
| **T2** | **Bloodfang Armor** | BWL + Onyxia (8-piece — helm Nefarian, chest Onyxia, etc.) | **Iconic vanilla set** — 8-piece bonus: chance to apply Bloodfang DoT on attacks. Highest-prestige raid set in 1.12. |
| **T2.5** | **Deathdealer's Embrace** / **Deathdealer's Vest** | AQ40 — token-based (Qiraji Bindings) | Sub-spec for Combat vs Subtlety |
| **T3** | **Bonescythe Armor** | Naxx40 — 9-piece token-based | Set bonuses: massive AP + threat reduction |

## Class trainer locations

| City | Faction | Rogue trainer NPCs | Notes |
|---|---|---|---|
| **Stormwind** | Alliance | **SI:7 (Old Town basement)** — Master Mathias Shaw (chain start NPC), Osborne the Night Man, Mary Edras | Primary chain hub for Alliance |
| **Ironforge** | Alliance | **Forlorn Cavern** (hidden chamber) — Hagrus, Lord Schele, Tyrion `[verify pass 3]` | |
| **Darnassus** | Alliance | **Cenarion Enclave / Howling Oak** — Mardant Strongoak, Lyazjen `[verify pass 3]` | |
| **Orgrimmar** | Horde | **Cleft of Shadow** — Shenthul (chain start NPC for Horde), Got'os, Therzok | Primary chain hub for Horde |
| Thunder Bluff | n/a | n/a — no Tauren Rogues in 1.12.1 | |
| **Undercity** | Horde | **Rogues' Quarter** (south wing) — Carolai Anise, Zane Bradford, Miles Welsh | |
| **Goldshire** | Alliance | Keryn Sylvius (lower-tier trainer for Humans) | |
| **Razor Hill** | Horde | sub-trainer for Orc rogues `[verify pass 3]` | |

## VMaNGOS / private server notes

- **Mathias and the Defias / Klaven's Tower** chain is fully scripted on VMaNGOS.
- **The Shattered Salute / Deep Cover** (Horde) is fully scripted including the Barrens flare event.
- **Ravenholdt chain** at lvl 50 has had occasional script issues with the Sunken Temple step `[verify VMaNGOS scripting status pass 3]`.
- **Pickpocket loot tables** are correctly weighted by level on VMaNGOS — Stranglethorn pirates are the canonical 45-55 Heavy Junkbox farm.
- **Lockpicking practice** in trainer rooms works correctly. BRD Relic Coffer doors give 12 free skill-up attempts per run.
- **Stealth** detection scaling matches retail 1.12.1 (every level above the Rogue's = +1 detection bonus to mobs/players).
- **Vanish** in 1.12.1 has a known bug-state on retail where DoTs would still break stealth despite the spell description; **VMaNGOS replicates this** (faithful to 2006 behavior). **Classic 2019 fixed Vanish**, so a memory-shared bot expecting Classic-2019 Vanish will see different behavior on a 2006-canonical server.

## Decision-Engine Rules

- **id:** `class.rogue.race-lock` — IF `Class==Rogue && Race==Tauren` THEN engine error.
- **id:** `class.rogue.pick-lock` — IF `Class==Rogue && Level>=16 && !Spells.Contains(PickLock)` AND in capital THEN visit trainer. Priority **800** (gates Lockpicking economy + Poisons quest at 20).
- **id:** `class.rogue.lockpicking-grind-to-75` — IF `Class==Rogue && Level>=16 && Lockpicking < 75` THEN grind Lockpicking on Heavy Junkboxes / world locks. Priority **750** (must reach 75 before lvl-20 Poisons chain). Targets: Practice Locks in trainer rooms, Stockades doors, Footlocker drops.
- **id:** `class.rogue.poisons-quest` — IF `Class==Rogue && Level>=20 && Lockpicking>=75 && !Spells.Contains(InstantPoison)` THEN run faction-specific Poisons chain. Priority **950** (class identity, raid DPS gate).
- **id:** `class.rogue.thistle-tea` — IF `Class==Rogue && Spells.Contains(InstantPoison) && Items.ThistleTeaCount < 20` AND in capital THEN craft Thistle Tea (Recipe: Thistle Tea + 1 Swiftthistle + 1 Refreshing Spring Water per tea). Priority **400** (background; raid prep stockpile).
- **id:** `class.rogue.ravenholdt-chain` — IF `Class==Rogue && Level>=50 && !QuestsCompleted.Contains(RavenholdtChain)` THEN run A Simple Request → Lord Jorach Ravenholdt chain. Priority **640** (gear gate — Whisperwalk Boots + Duskbat Drape are pre-raid BiS-adjacent).
- **id:** `class.rogue.lockpicking-grind-to-300` — IF `Class==Rogue && Level==60 && Lockpicking < 300` THEN run BRD Relic Coffer Door grinds. Priority **350** (background, gates BWL Tightly Sealed Trunk).
- **id:** `class.rogue.pickpocket-mob` — IF `snapshot.CurrentTarget.IsHumanoid && !snapshot.CurrentTarget.WasPickpocketed && snapshot.IsStealthed` THEN Pickpocket before opener. Priority **600** (always-on gold/junkbox income).
- **id:** `class.rogue.poison-stockpile` — IF `Class==Rogue && Items.InstantPoisonCharges + Items.DeadlyPoisonCharges < 100` AND in capital with **Reagent Vendor** access THEN buy reagents. Priority **500**.
- **id:** `class.rogue.respec-combat-at-60` — IF `Class==Rogue && Level==60 && Role==RaidDPS && CurrentSpec NOT IN {CombatSwords, CombatDaggers}` THEN respec to 15/31/5 Combat Swords. Priority **750**.
- **id:** `class.rogue.weapon-skill-grind` — IF `WeaponSkill[mainhand] < 5*Level` AND solo THEN attack low-level mobs / dummies to top off. Priority **300** (background, raid-mandatory).

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `Lockpicking` (planned — Rogue-specific class skill, distinct from weapon skill)
- `Spells` (planned) — PickLock, InstantPoison, DeadlyPoison, Vanish, Blind, Distract, etc.
- `Items.ThistleTeaCount` / `Items.InstantPoisonCharges` / `Items.DeadlyPoisonCharges` / `Items.MindNumbingPoisonCharges` (planned — poison reagent and charge tracking)
- `QuestsCompleted` (existing) — Poisons quest, Ravenholdt chain
- `IsStealthed` (planned — derivable from active aura scan)
- `CurrentTarget.IsHumanoid` (planned — derivable from target type)
- `CurrentTarget.WasPickpocketed` (planned — Pickpocket has a per-mob debuff to prevent re-pickpocket; engine reads it)
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)

## Cross-references

- [decision-engine/per-bracket-actions/02-l10-l20.md](../decision-engine/per-bracket-actions/02-l10-l20.md) — Pick Lock + Poisons quest at 16/20
- [decision-engine/per-bracket-actions/05-l40-l55.md](../decision-engine/per-bracket-actions/05-l40-l55.md) — Ravenholdt chain at 50
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/warrior.md](warrior.md) — Lionheart / weapon overlap
- [professions/](../professions/) (pass 6) — Engineering Goblin synergy for Gnome rogues
- [systems/](../systems/) (pass 10) — Lockpicking-as-class-skill mechanics
- [pvp/](../pvp/) (pass 8) — Rogue PvP (Hemorrhage / Vanish exploit) is the dominant 1v1 class in vanilla
