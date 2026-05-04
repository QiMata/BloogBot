# Druid — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Druid (canonical, modern)
> - https://www.wowhead.com/classic/guide/druid-class-quests-classic-wow
> - https://www.icy-veins.com/wow-classic/druid-quests-in-wow-classic
> - https://vanilla-wow-archive.fandom.com/wiki/Travel_Form (referenced via search)
> - https://vanilla-wow-archive.fandom.com/wiki/Druid/Quests (referenced via search)
>
> **Pass 2.** Some details (lvl-50/60 Pristine Hide of the Beast → Wildheart chain exact steps, T0/T0.5 piece-by-boss) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Druids are the **only true hybrid class** in vanilla — viable in **all four roles** (Tank / Healer / Melee DPS / Caster DPS) by switching forms and respeccing. **Restoration Druids** are the third major raid healer alongside Priests and Paladins (Alliance) / Shamans (Horde), bringing **Tranquility** (5-min CD AoE channelled heal) and **Innervate** (mana cooldown for healers — given to a healer pre-pull on long fights). **Feral Bear Druids** are the only viable raid off-tank besides Warrior in 1.12 (vs Princess Theradras / AQ20 / Twin Emp shadow phase / etc.). **Moonkin Form** (Balance 31-pt) was added in patch 1.10 and provides a **+3% spell crit aura** to party — niche but raid-buff-relevant. **Feral Cat DPS** is the lowest-DPS melee in vanilla and is rarely raid-included.

| Role | Spec | Strength |
|---|---|---|
| Raid healer | Restoration 14/0/37 | Healing Touch + Rejuvenation + Innervate + Tranquility (AoE heal) |
| Off-tank | Feral 0/30/21 (or deep Feral 0/40/11) | Bear Form + Frenzied Regeneration + Bash |
| Caster Moonkin | Balance 31/0/20 | Moonkin Form aura (+3% spell crit party-wide) |
| World / Cat DPS | Feral Cat 0/30/21 | Stealth + Pounce + Shred |
| PvP / world | Feral hybrid (Bear ↔ Cat ↔ Caster) | Form-shift to break roots/snares |
| Leveling | Feral Cat (or Balance Wrath) | Cat Form Prowl + Pounce + DoT-then-shift |

## Race availability + racial trait synergy

In 1.12.1 **Druid is restricted to 2 races**: **Night Elf** (Alliance) and **Tauren** (Horde). The most race-restricted class in vanilla. (Worgen + Troll + Highmountain Tauren added in Cataclysm and later.)

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Night Elf | Alliance | **Quickness** (1% dodge), **Shadowmeld** (out-of-combat stealth), **Wisp Spirit**, +5 Bow Spec (irrelevant for Druid) | **Best Druid PvP race** — Shadowmeld + Cat Form Prowl = double-stealth (drop combat → Shadowmeld → reset). 1% dodge stacks with Quickness/Bear Form for tank. |
| Tauren | Horde | **Endurance** (+5% base health), **War Stomp** (2s AoE stun, 5y), **Cultivation** (+15 herbalism), **Nature Resistance +10** | **Best Druid Tank race** — Endurance + 5% HP base; War Stomp is a melee panic-button. Larger hitbox — disadvantage in PvP / Cat Form (slightly worse stealth detection). Cultivation pairs with Druid + Herbalism profession. |

**Engine race-pick rule** (Druids):
- **Tank Bear** Druid → **Tauren** (Endurance + War Stomp survival)
- **Cat DPS** → **Night Elf** (Shadowmeld stealth combo on Alliance)
- **Resto Healer** → either; **Tauren** marginally better (5% HP for survival on tight fights)
- **Moonkin** → either; mostly tied

## Class quests in level order — the Form Quest Chain

Druids are unique: **Bear Form (lvl 10)** and **Aquatic Form (lvl 16)** are quest-rewarded; **Cat Form (lvl 20)** and **Travel Form (lvl 30)** are trainer-only in 1.12.1 (no quest). All form-related questing happens in **Moonglade** (the cross-faction safe haven) via teleport.

| Lvl | Form / Quest | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| **10** | **Bear Form** quest chain | **Mathrengyl Bearwalker** (Darnassus, Cenarion Enclave) for Alliance / **Turak Runetotem** (Thunder Bluff, Elder Rise) for Horde → **Teleport: Moonglade** (auto-trained at lvl 10) → **Dendrite Starblaze** (Moonglade) → **Great Bear Spirit** (west of Moonglade lake) → fly to **Auberdine** (Darkshore) → **Lunaclaw** in cave east of Auberdine → return to Mathrengyl/Turak | **Bear Form** (tank form: +25% armor, +stamina, generates rage) | **Class identity priority 970 at lvl 10.** Cross-zone (Moonglade → Auberdine) — engine plans flight + travel. |
| **14** | **Cure Poison** quest | Mathrengyl/Turak → 5× Earthroot (purchase from AH or farm Elwynn / Mulgore) → Teleport: Moonglade → Dendrite Starblaze | Cure Poison spell | Trainer-prereq for tank/healer utility. Priority **600**. |
| **16** | **Aquatic Form** quest chain | Mathrengyl/Turak → Teleport: Moonglade → Tajarri (or chain step) → multi-step chain `[verify pass 3]` ending in cave/water area → return | **Aquatic Form** (seal form: +50% swim speed, indefinite underwater breathing) | Priority **940** at lvl 16. Underwater questing in Hillsbrad / Hinterlands / The Hinterlands becomes painless. |
| **20** | **Cat Form** trained (no quest) | Trainer in Darnassus (Mathrengyl) or Thunder Bluff (Turak) | **Cat Form** (DPS form: stealth, +15% melee speed, energy resource, Shred + Pounce + Rake + Rip) | **Class identity priority 950.** Cat Form transforms outdoor/dungeon DPS. Engine adds Cat-form actions to action menu at lvl 20. |
| 24 | Hibernate trained | Trainer | Hibernate (40s sleep on Beast/Dragonkin) | CC for 5-mans |
| **30** | **Travel Form** trained (no quest) | Trainer | **Travel Form** (40% outdoor speed, similar to Shaman Ghost Wolf) | **Class identity priority 970** — saves ~80g (no need for apprentice riding) until lvl 40. Free outdoor mount equivalent. |
| 40 | **Dire Bear Form** (Feral talent at 30 in Feral tree, requires lvl 30+ to spec) | Talent (5 points in Feral) | Dire Bear Form (improved Bear: +200% armor, +25% stam) | Talent unlock, not quest. Only Feral spec gets it. |
| 40 | **Moonkin Form** (Balance 31-pt talent, lvl 40+ if Balance) — **added in patch 1.10 (Dec 2005)** | Balance talent capstone | Moonkin Form (+3% spell crit party aura, +30% melee armor in form, no spell-school benefits in form) | Talent, not quest. Engine flags for Balance respec. |
| **50** | **Pristine Hide of the Beast** chain (Sunken Temple drop start) → Druid epic gear chain | Multi-step from various Druid-trainer NPCs | Eventually rewards **Wildheart pieces** / cloak / accessories `[verify pass 3 — exact reward chain]` | Bundle with planned ST run; multi-zone follow-up. Priority **640**. |
| 60 | **Resurrection** rank-ups + various trainer skills | Trainer | — | Trainer |
| 60 | **No epic class weapon quest** — Druid lacks a Quel'Serrar/Benediction/Rhok'delar equivalent. Endgame Druid weapons come from raids (T1 weapons, AQ40 Idol of Wrath, Naxx) and quest rewards (Hammer of Bestial Fury equivalent). | n/a | n/a | Engine notes |

### Idol slot (ranged-equivalent for Druids)

Druids equip an **Idol** in the ranged slot (parallel to Paladin Libram, Priest Wand, Shaman Totem). Pre-raid Idols give +healing or +Bear-Form-armor or +Cat-Form-AP in the ~10-30 range. Notable:

- **Idol of the Avenger** (Strat or BS-crafted) — Bear form +AP
- **Idol of Brutality** (BRD or BS) — Cat AP+stamina
- **Idol of Health** (Honored Cenarion Circle reward) — +Healing Touch +healing

## Talent trees (1.12 51-point trees)

### Balance (Moonkin Form capstone — added in patch 1.10)

Caster Druid; Wrath + Starfire; Insect Swarm + Moonfire DoTs.

Key talents: **Improved Wrath** (5/5 — -0.5s Wrath cast), **Nature's Grasp** (1/1 — passive root proc on Nature spell hit), **Improved Nature's Grasp** (4/4 — +20% root proc), **Improved Entangling Roots** (3/3), **Improved Moonfire** (5/5 — +6% Moonfire damage / +6% crit), **Natural Weapons** (5/5 — +10% physical damage in animal forms), **Natural Shapeshifter** (3/3 — -mana cost on shifts), **Improved Thorns** (3/3), **Omen of Clarity** (1/1 — passive Clearcasting proc — **leveling staple**), **Nature's Reach** (2/2 — +20% range on Nature spells), **Vengeance** (5/5 — +100% crit damage on Nature spells), **Improved Starfire** (5/5 — -0.5s Starfire cast), **Nature's Grace** (1/1 — next spell -0.5s cast after a crit), **Moonglow** (3/3 — -10% mana cost on Moonfire/Starfire/Wrath), **Moonfury** (5/5 — +10% Moonfire/Starfire/Wrath damage), **Moonkin Form** (1/1 — 31-pt capstone, **patch 1.10**).

### Feral Combat (Heart of the Wild capstone)

Bear/Cat form; tank or melee DPS.

Key talents: **Ferocity** (5/5 — -5 energy on Cat skills), **Feral Aggression** (5/5 — +20% Demoralizing Roar / Ferocious Bite damage), **Improved Shred** (2/2 — -3 energy on Shred), **Brutal Impact** (2/2 — +1s stun on Bash + Pounce), **Thick Hide** (5/5 — +10% armor), **Feline Swiftness** (3/3 — +30% Cat-form speed outdoor), **Sharpened Claws** (3/3 — +6% Bear/Cat crit), **Improved Bash** (3/3 — +30% Bash stun chance), **Faerie Fire (Feral)** (1/1 — Feral version of Faerie Fire usable in form), **Blood Frenzy** (2/2 — combo points on Cat crits), **Heart of the Wild** (5/5 — passive +Stam in Bear / +Str in Cat / +Int caster), **Leader of the Pack** (1/1 — passive +3% melee crit aura — defining raid Feral talent), **Improved Leader of the Pack** (2/2 — heal proc on crits), **Predatory Strikes** (3/3 — +AP from level), **Primal Fury** (2/2 — extra rage/energy on crits), **Furor** (5/5 — +rage/energy gained on shapeshift), **Savage Fury** (2/2 — +20% Maul/Rake/Mangle damage)... actually the 31-pt capstone is variable by build path. Most agree on **Heart of the Wild** as the deep talent (mid-tree), and **Leader of the Pack** as the deep-Feral 31-pt-zone capstone.

### Restoration (Swiftmend capstone — added in patch 1.x)

Healing-focused. The 31-pt capstone **Swiftmend** = consumes Rejuvenation or Regrowth on target for instant heal, 15-sec CD.

Key talents: **Improved Mark of the Wild** (5/5 — +35% MotW stats), **Furor** (5/5 — +rage/energy on shift), **Improved Healing Touch** (5/5 — -0.5s HT cast), **Improved Enrage** (2/2), **Reflection** (3/3 — 15% mana regen during cast), **Tranquil Spirit** (5/5 — -10% mana cost on Tranquility/Healing Touch — defining Resto talent), **Improved Rejuvenation** (3/3 — +15% Rejuv heal), **Nature's Focus** (5/5 — pushback resistance), **Insect Swarm** (no — Balance), **Subtlety** (5/5 — -threat on heals), **Tranquil Healing** (no — that's name confusion), **Nature's Swiftness** (1/1 — instant cast next nature spell, 3-min CD — **defining raid utility**), **Gift of Nature** (5/5 — +10% all healing — defining Resto talent), **Improved Tranquility** (2/2 — -threat on Tranquility), **Innervate** (1/1 — target gets 200% mana regen for 20s, 6-min CD — **defining raid utility, target a healer**), **Swiftmend** (1/1 — 31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Restoration raid healer** | **14/0/37** — 14 Balance (Improved Wrath 5 / Natural Weapons 5 / Improved Moonfire 4 — wait actually for Resto the splash is into Furor + Natural Weapons): 14 Balance (Imp Wrath 5 / Improved Nature's Grasp 4 / Nature's Reach 2 / Vengeance 3) → 14 Balance; 37 Resto (Imp MotW 5 / Furor 5 / Imp Healing Touch 5 / Reflection 3 / Tranquil Spirit 5 / Imp Rejuvenation 3 / Nature's Focus 5 / Subtlety 5 / Nature's Swiftness 1 / Gift of Nature 5 / Innervate 1 / Swiftmend 1) | Raid healer, dominant build | Healing Touch + Innervate + NS + Swiftmend rotation |
| **Feral tank (deep Bear)** | **0/30/21** or **0/35/16** — deep Feral: Ferocity 5 / Feral Aggression 5 / Imp Shred — wait Shred is Cat. Actually for Bear-tank: Sharpened Claws 3 / Brutal Impact 2 / Thick Hide 5 / Imp Bash 3 / Heart of the Wild 5 / Leader of the Pack 1 / Imp LotP 2 / Predatory Strikes 3 → 30 Feral; 16 Resto (Furor 5 + Imp MotW 5 + Imp Enrage 2 + Reflection 3 + Imp Rejuvenation 1) | Off-tank | Bear Form + Frenzied Regeneration + Bash + Demoralizing Roar |
| **Moonkin (Balance raid caster)** | **31/0/20** — deep Balance to Moonkin Form (Improved Wrath 5 / Improved Moonfire 5 / Natural Weapons 5 / Vengeance 5 / Improved Starfire 5 / Moonfury 5 / Moonkin Form 1) → 31 Balance; 20 Resto (Furor 5 + Imp MotW 5 + Reflection 3 + Tranquil Spirit 5 + Imp Rejuvenation 2) | Raid caster + +3% spell crit party aura | Moonfire + Wrath/Starfire spam + Insect Swarm in Moonkin form |
| **Cat DPS (rare)** | 0/30/21 with Cat-focus: Ferocity 5 / Imp Shred 2 / Brutal Impact 2 / Sharpened Claws 3 / Feline Swiftness 3 / Blood Frenzy 2 / Heart of the Wild 5 / Leader of the Pack 1 / Predatory Strikes 3 / Primal Fury 2 → 30 Feral; 16 Balance (Imp Wrath 5 + Natural Weapons 5 + Improved Moonfire 4 + Improved Thorns 1 + ...) — wait actually Cat DPS hybrid usually splashes Resto for Furor + Natural Shapeshifter | Solo questing / world / niche raid DPS | Cat Form + Pounce + Shred + Rip rotation |
| **Leveling Cat** | 0/30/0 → 0/30/21 by 60 → respec Resto at 60 | Solo questing | Cat Form Prowl + Pounce open + Shred → Rip; quick-shift to Caster for Heal between fights |

## Recommended weapons by bracket

| Bracket | Weapon | Notes |
|---|---|---|
| 1-15 | Vendor staff or 1H+OH | Stat sticks |
| 15-30 | Quest staff | Stat sticks for INT/Spi; Druids can equip 2H Maces, Daggers, Fist Weapons, Staves |
| 30-45 | 2H mace or staff | Whirling Hammer / Truesilver Champion (BS) |
| 45-55 | Staff or 2H mace | Hand of Edward the Odd offhand 1H sword (chance to cast spell on hit — defining spell-haste-equivalent for caster Druids) |
| 55-58 | Staff | Headmaster's Charge (Scholo) |
| 58-60 | Staff | Headmaster's Charge / **Lei of Lilies** (offhand healing) | |
| 60 (post-MC) | Staff | T1 Wildheart Staff variants | |
| 60 (post-AQ40) | Staff | **Anubisath Warhammer** (2H mace), **Idol of Wrath** (ranged) | |

**Note**: Druids in Feral Cat/Bear form override their weapon damage with form-based DPS scaling; weapon stats still matter for INT/Stam/AP secondary stats. **Weapon-skill is irrelevant in Bear/Cat Form** (form damage doesn't use weapon-skill).

## Pre-raid BiS gear (Restoration Druid focus)

`[verify pass 3 for exact items]`

| Slot | Item | Source |
|---|---|---|
| Head | **Wildheart Cowl** (T0) → upgrade to T0.5 | Strat UD Baron |
| Neck | **Mark of Fordring** | EPL |
| Shoulders | **Wildheart Spaulders** (T0) | LBRS |
| Cloak | **Cape of the Cosmos** (Tailoring) | |
| Chest | **Hide of the Wild** (LW BoP, **Druid-only or anyone-with-LW**) | Leatherworking 300 |
| Bracers | **Wildheart Bracers** (T0) | |
| Hands | **Wildheart Gloves** (T0) | |
| Belt | **Wildheart Belt** (T0) | |
| Legs | **Wildheart Kilt** (T0) | |
| Feet | **Wildheart Boots** (T0) | |
| Ring 1 | **Magni's Will** equivalent / Don Mauricio's Band | |
| Ring 2 | **Tarnished Elven Ring** | |
| Trinket 1 | **Briarwood Reed** (DM N) | DM N |
| Trinket 2 | **Eye of the Beast** (LBRS Beasts) | |
| Weapon | **Headmaster's Charge** (staff) | Scholo |
| Idol | **Idol of Health** (Cenarion Circle Honored) / Idol of Brutality | rep / BRD |

## Tier set progression

| Tier | Set name (Druid) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Wildheart Raiment** | 8-piece, 60 5-mans | Drop-only, BoP. Set bonus 8-piece: bonus to Healing Touch / Rejuvenation cost reduction |
| **T0.5 (Dungeon Set 2)** | **Feralheart** | Quest upgrade chain (patch 1.10) | Bridge to T1 |
| **T1** | **Cenarion Raiment** | Molten Core (8-piece) | Set bonuses: -mana cost on heals + bonus damage with form-shifts |
| **T2** | **Stormrage Raiment** | BWL + Onyxia (8-piece) | Iconic antlers helm; set bonuses: chance to refresh Rejuv on cast |
| **T2.5** | **Genesis Raiment** | AQ40 — token-based | Sub-spec branches per role |
| **T3** | **Dreamwalker Raiment** | Naxx40 — 9-piece | Set bonuses: massive healing throughput + free Innervate |

## Class trainer locations

| City | Faction | Druid trainer NPCs | Notes |
|---|---|---|---|
| **Darnassus** | Alliance | **Cenarion Enclave** (2nd floor of Druid tree) — **Mathrengyl Bearwalker** (form quest NPC), Aldrae, Tarsis Kir-Moldir | Primary Alliance Druid hub |
| **Moonglade** | both | **Loganaar** (handles cross-faction form chains and trainer lvl-50+ rank-ups) — central Druid sanctuary, Teleport: Moonglade arrives here | Cross-faction safe haven; Druids are unique in this. **All form-quest chains route through Moonglade.** |
| **Thunder Bluff** | Horde | **Elder Rise** (top of Spirit Rise) — **Turak Runetotem** (form quest NPC), Sheal Runetotem | Primary Horde Druid hub |
| Stormwind / Ironforge / Orgrimmar / Undercity | n/a | n/a — no Druid trainers in non-faction-druid cities | |

**Teleport: Moonglade** is auto-trained at lvl 10 (before Bear Form quest start) — engine confirms before scheduling any form quest.

## VMaNGOS / private server notes

- **Bear Form** quest chain (Lunaclaw fight in Auberdine cave) is fully scripted on VMaNGOS — Lunaclaw spawns reliably.
- **Aquatic Form** quest chain works correctly; underwater quest steps are scripted.
- **Cat Form** + **Travel Form** are trainer-only in 1.12.1 (no quest — Classic 2019 added quest chains for both, so engine should not assume Classic 2019 behavior on a 1.12.1 server).
- **Moonkin Form** (Balance 31-pt) was added in **patch 1.10 (Storms of Azeroth, Dec 2005)**. Confirm the VMaNGOS server is at or beyond patch 1.10 talent calculator before allowing Moonkin spec.
- **Innervate** target-on-healer mechanic works correctly; **Tranquility** AoE heal works.
- **Pristine Hide of the Beast** drops from Sunken Temple bosses correctly.
- **Loganaar in Moonglade** handles form chain hand-ins and acts as a universal trainer.

## Decision-Engine Rules

- **id:** `class.druid.race-lock` — IF `Class==Druid && Race NOT IN {NightElf, Tauren}` THEN engine error.
- **id:** `class.druid.bear-form` — IF `Class==Druid && Level>=10 && !Spells.Contains(BearForm)` THEN run Moonglade → Auberdine Lunaclaw chain. Priority **970** (class identity, suspends questing).
- **id:** `class.druid.cure-poison` — IF `Class==Druid && Level>=14 && !Spells.Contains(CurePoison)` THEN visit trainer + Moonglade Dendrite. Priority **600**.
- **id:** `class.druid.aquatic-form` — IF `Class==Druid && Level>=16 && !Spells.Contains(AquaticForm)` THEN run Aquatic Form chain. Priority **940** (class identity).
- **id:** `class.druid.cat-form` — IF `Class==Druid && Level>=20 && !Spells.Contains(CatForm)` THEN visit trainer (no quest in 1.12.1). Priority **950**.
- **id:** `class.druid.travel-form` — IF `Class==Druid && Level>=30 && !Spells.Contains(TravelForm)` THEN visit trainer. Priority **970** — saves apprentice riding cost (~80g) until lvl 40, equivalent to a free 40% outdoor mount.
- **id:** `class.druid.dire-bear-form` — IF `Class==Druid && Level>=30 && Spec==Feral && TalentTreePoints[Feral]>=30 && !Spells.Contains(DireBearForm)` THEN visit trainer. Priority **800** for Feral specs.
- **id:** `class.druid.moonkin-form` — IF `Class==Druid && Level>=40 && Spec==Balance && TalentTreePoints[Balance]>=31 && !Spells.Contains(MoonkinForm)` THEN respec or grab the talent. Priority **750** for Balance.
- **id:** `class.druid.pristine-hide-chain` — IF `Class==Druid && Level>=50 && !Items.Contains(PristineHideOfTheBeast)` AND ST run on action menu THEN bundle the chain. Priority **640**.
- **id:** `class.druid.innervate-target` — IF `Class==Druid && Spec==Resto && PartyHealer.Mana<25% && Innervate.NotOnCooldown` THEN cast Innervate on healer. Priority **850** (combat-time raid utility).
- **id:** `class.druid.tranquility-on-aoe` — IF `Class==Druid && Spec==Resto && Raid.AoEDamageInProgress && PartyHPAvg<60% && Tranquility.NotOnCooldown` THEN channel Tranquility. Priority **820** (combat-time, AoE healing).
- **id:** `class.druid.battle-rez` — IF `Class==Druid && Level>=20 && Spells.Contains(Rebirth) && PartyMember.IsDead && InCombat && Rebirth.NotOnCooldown` THEN cast Rebirth. Priority **900** (combat-rez, **defining Druid raid utility**).
- **id:** `class.druid.idol-equipped` — IF `Class==Druid && Level>=20 && RangedSlot.IsEmpty` THEN equip best Idol. Priority **600**.
- **id:** `class.druid.respec-at-60` — IF `Class==Druid && Level==60 && CurrentSpec != PlannedRoleSpec` THEN respec to 14/0/37 Resto / 0/30/21 Feral / 31/0/20 Balance. Priority **750**. Druids respec frequently between Resto (raid healing) and Feral (AQ20 farming, world PvP).

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `Spells` (planned) — BearForm, CatForm, AquaticForm, TravelForm, DireBearForm, MoonkinForm, CurePoison, Innervate, Tranquility, Rebirth, Swiftmend, Hibernate, FaerieFire (Feral)
- `RangedSlot.IsIdol` / `RangedSlot.SpellPower` (planned) — same as Pal Libram, Shaman Totem, Priest Wand mechanic
- `CurrentForm` (planned: Bear / Cat / Aquatic / Travel / Dire Bear / Moonkin / Caster / Tree-of-Life [TBC]; vanilla = Caster + 4 forms + Dire Bear + Moonkin)
- `Rebirth.OnCooldown` (planned — 30-min CD in 1.12)
- `Innervate.OnCooldown` (planned — 6-min CD)
- `Tranquility.OnCooldown` (planned — 5-min CD)
- `PartyHealer.Mana` (planned — derived from party scan)
- `Raid.AoEDamageInProgress` / `PartyHPAvg` (planned — boss-encounter scripting + party scan)
- `Items.PristineHideOfTheBeast` (planned)
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)

## Cross-references

- [decision-engine/per-bracket-actions/02-l10-l20.md](../decision-engine/per-bracket-actions/02-l10-l20.md) — Bear Form at 10, Aquatic Form at 16
- [decision-engine/per-bracket-actions/03-l20-l30.md](../decision-engine/per-bracket-actions/03-l20-l30.md) — Cat Form at 20, Travel Form at 30
- [decision-engine/per-bracket-actions/05-l40-l55.md](../decision-engine/per-bracket-actions/05-l40-l55.md) — Pristine Hide at 50
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/shaman.md](shaman.md) — parallel Resto/utility role; both use Innervate-equivalent (Druid Innervate vs Shaman Mana Tide)
- [reputations/](../reputations/) (pass 7) — Cenarion Circle (Idol of Health) and Cenarion Hold rep
- [systems/](../systems/) (pass 10) — Form-shift mechanics + Idol ranged-slot
