# Paladin — WoW 1.12.1 Class Deep-Dive (Alliance only)

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Paladin (canonical, modern; vanilla details thin)
> - https://www.icy-veins.com/wow-classic/paladin-quests-in-wow-classic
> - https://www.wowhead.com/classic/guide/classic-wow-paladin-warhorse-charger-mounts
> - https://www.warcrafttavern.com/wow-classic/guides/paladin-epic-mount/
> - https://wowwiki-archive.fandom.com/wiki/Summoning_the_Charger_quest_chain (referenced via search; direct fetch returned 403)
> - https://vanilla-wow-archive.fandom.com/wiki/Paladin_builds (referenced via search)
>
> **Pass 2.** Some chain detail (exact Charger material list, Dark Iron Spy vs Defias Rogue Wizard race-split for Redemption) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.
>
> **Faction lock.** Paladin is **Alliance-only** in 1.12.1. Horde Paladins (Blood Elves) are TBC. Engine MUST NOT schedule Paladin actions for Horde toons.

## Identity

Paladins are the **Alliance-exclusive raid healer + off-tank + buff bot**. The class fills three archetypes simultaneously and excels at none — but the **utility** (Blessings, Auras, Greater Blessings via Tome at lvl 60, Bubble, Lay on Hands, Cleanse, Hand of Freedom — wait, Freedom is TBC) is unmatched. Holy Paladins are the **highest mana-efficiency single-target healers** in MC/BWL once Illumination is fully stacked. Protection Paladins use the **Reckoning bomb** and **Holy Shield** for AoE-tank niche pulls and serve as off-tanks. Retribution Paladins are **considered subpar** for raid DPS (not allowed in most BWL+ raid rosters in 1.12) but viable in PvP and solo.

| Role | Spec | Strength |
|---|---|---|
| Raid healer | Holy 31/5/15 | Highest mana-efficient single-target heal |
| Off-tank / AoE puller | Protection 5/31/15 | Holy Shield AoE block, Reckoning passive damage |
| Reckoning bomb | Holy 21 / Ret 30 splash | Burst-damage gimmick — niche use |
| PvP / world | Retribution 0/14/37 | Repentance + Seal of Command + Hammer of Justice |
| Leveling | Retribution / Holy hybrid | Slowest leveling class in vanilla; mitigated by free lvl-40 mount |

## Race availability + racial trait synergy

In 1.12.1 only **Human** and **Dwarf** can be Paladins. Night Elves, Gnomes, and Draenei are **not** Paladin races (Draenei is TBC).

| Race | Relevant racials | Notes |
|---|---|---|
| Human | **Sword Specialization +5**, **Mace Specialization +5**, Diplomacy (rep gain), Perception (stealth detection) | Best for Ret PvP — sword spec syncs with Quel'Serrar, mace spec with Hand of Rag-equivalent items. Diplomacy speeds Argent Dawn / city rep grinds. The Human Paladin is the canonical raid Paladin. |
| Dwarf | **Mace Specialization +5**, **Gun Specialization +5**, Stoneform (8s bleed/poison/disease immune + 10% armor, 3min CD), Find Treasure | Stoneform is a strong PvP CD — purges Rogue/Warrior bleeds, breaks DoTs. Find Treasure marks chests on the minimap. Mace spec for Hand of Rag itemization. |

**Engine race-pick rule**: For Holy / raid healer roles → Human (Diplomacy speeds AD revered for Naxx attune by ~30% in /played time). For Protection / Ret / PvP → Dwarf (Stoneform is a major survivability cooldown, particularly against Rogues).

## Class quests in level order

| Lvl | Quest / Chain | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| 1 | Race-specific starter | Class trainer in Northshire (H) / Coldridge Valley (D) | Hammer of Wrath (no — that's lvl 12 spell; starter weapon) | Auto-accept |
| 12 | **Tome of Divinity → Redemption** chain | **Brother Wilhelm** (Goldshire, Human) / **Azar Stronghammer** (Kharanos, Dwarf) → **Duthorian Rall** (Stormwind Cathedral of Light) → 10 Linen Cloth + kill **Defias Rogue Wizard** (Westfall, Human) or **Dark Iron Spy** (Loch Modan / Wetlands, Dwarf) `[verify pass 3]` | **Resurrection** ability | Race-locked starter sets the pattern; bundle with bracket-2 hub bind |
| 14 | Sense Undead trained | Trainer | Sense Undead | Auto-trained on level-up |
| 20 | **Tome of Valor → Verigan's Fist** chain | Duthorian Rall → multi-instance chain spanning **Stockades**, **Shadowfang Keep**, **Blackfathom Deeps** (item drops in each) | **Verigan's Fist** — lvl 22 BoP 2H mace, slow speed, +9 Stamina, +1% spell crit, Holy damage proc | Class-defining 2H weapon for the entire 20-40 bracket |
| 30 | Trainer rank-ups for Auras (Devotion, Concentration, Retribution), Blessings, Hammer of Justice | Trainer | — | Auto-trained |
| 40 | **Tome of Nobility → Summon Warhorse** chain | Trainer → Duthorian Rall → 4 Manuscript Pages (drops in Tirisfal/Westfall/Loch Modan/Hillsbrad — confirm pass 3) → final turn-in | **Summon Warhorse** (60% ground mount, free, no riding skill required) | **Single largest gold saving in the game**: Paladin saves ~80-170s at lvl 40 vs every other class because Warhorse is free + apprentice riding is free. Engine should NOT spend gold on a non-class mount alternative. |
| 50 | Class trainer Greater Blessings rank-up | Trainer | Greater Blessings (Symbol of Kings reagent required) | Bundle with capital trip |
| 50 | **Resistance Aura** trained at trainer | Trainer | Frost / Fire / Shadow Resistance Auras | Trainer-only |
| 60 | **Summoning the Charger** epic mount chain | **Duthorian Rall** (Stormwind Cathedral) → **Lord Grayson Shadowbreaker** (Cathedral) → **High Priest Rohan** (Ironforge Hall of Mysteries, 150g for Exorcism Censer) → **Terrordale** (Eastern Plaguelands, exorcise spirits) → **Grimand Elmore** (Stormwind Dwarven District, 150g + Pure Water + Wildvine + Arthas' Tears + Blood of Heroes for Stratholme Holy Water `[verify pass 3]`) → **Hillsbrad / Southshore** (Ancient Equine Spirit) → **Scholomance** (Rattlegore kill, then 4 waves of mini-bosses summoned by Lord Grayson's Satchel + final boss **Death Knight Darkreaver**) | **Summon Charger** (epic 100% ground mount, free riding skill, paladin-only) | Total cost ~450g hard + mats. Multi-week chain. Class-identity priority **980** at lvl 60. |

### Level-60 Charger chain breakdown (engine-relevant)

The chain is the longest class quest in vanilla. The DecisionEngine should plan it as a **multi-week sub-goal** with these milestones:

1. **Phase 1: Stormwind Cathedral** — Duthorian Rall → Lord Grayson. Cost: 0g. Requires lvl 60.
2. **Phase 2: Ironforge & Terrordale (EPL)** — High Priest Rohan (150g for Censer) → exorcise 4 (5? `[verify pass 3]`) Anguished Highborne spirits in Terrordale (lvl 53-55 ghost mobs).
3. **Phase 3: Materials** — Grimand Elmore in Dwarven District (150g + various crafted/farmed materials). The materials are the longest part of the chain because some are RNG drops or rep-locked:
   - **Pure Water** — 4× / 5× from Hydraxian Waterlords (Honored gives 6 buyable / week ?)`[verify pass 3]` — alternative source: Pure Water bottled by Mages
   - **Wildvine** — 4× / herbalism node drop in STV / Tanaris swamps; auctionable for ~5g each
   - **Arthas' Tears** — 4× / herbalism node in EPL/WPL/Tirisfal (lvl 270 herb)
   - **Blood of Heroes** — 4× / Stratholme bosses random drop (Cannon Master Willey, Postmaster, Maleki, etc.) `[verify pass 3]`
4. **Phase 4: Hillsbrad** — Ancient Equine Spirit (Southshore area, summon-and-talk).
5. **Phase 5: Scholomance** — Skeleton Key required. Kill **Rattlegore** (boss, ~halfway through). Open Lord Grayson's Satchel → spawns 4 waves of mini-bosses (Royal Dreadguard, Spectral Citizen, Risen Aberration, Risen Constructor `[verify pass 3]`) followed by **Death Knight Darkreaver**. Kill Darkreaver → return to Lord Grayson → receive **Summon Charger**.

Engine implementation: **track each phase as a separate snapshot predicate**. Don't bundle the whole chain — Phase 5 requires a Skeleton Key and a 5-man group; engine must coordinate.

## Talent trees (1.12 51-point trees)

### Holy (Holy Shock capstone)

Healing + buffs + utility. The 31-pt capstone **Holy Shock** was added in patch **1.10 (Storms of Azeroth, Dec 2005)**.

Key talents: **Divine Strength** (5/5 — STR scaling), **Divine Intellect** (5/5 — INT scaling), **Spiritual Focus** (5/5 — pushback resistance), **Improved Lay on Hands** (2/2 — armor buff after LoH), **Healing Light** (3/3 — +12% Holy Light healing), **Improved Blessing of Wisdom** (2/2 — +20% mana from BoW), **Illumination** (5/5 — 100% mana refund on crit-heal — the **defining Holy Paladin talent**), **Lasting Judgement / Improved Lay on Hands** (cheap utility), **Holy Power** (5/5 — +5% crit on Holy spells), **Improved Concentration Aura** (3/3 — pushback resistance for raid), **Holy Shock** (1/1 — fast heal/damage, long CD, 31-pt capstone).

### Protection (Holy Shield capstone)

Tank + utility. The 31-pt capstone **Holy Shield** is a charge-based shield-block buff that deals damage on block.

Key talents: **Improved Devotion Aura** (5/5 — +20% to Aura armor), **Improved Righteous Fury** (3/3 — +30% threat in RF), **Anticipation** (5/5 — defense rating), **Improved Hammer of Justice** (3/3 — -10s CD), **Toughness** (5/5 — armor multiplier), **Improved Concentration Aura** (3/3), **Blessing of Sanctuary** (1/1 — block + damage reduction blessing), **Reckoning** (Ret tree splash, 5/5 — passive — 100% chance after dodging/blocking/parrying to gain an extra autoattack swing; **the "Reckoning bomb" mechanic** — store charges, then unleash for burst), **Holy Shield** (1/1 — 31-pt capstone).

### Retribution (Repentance capstone)

DPS + crowd-control. **Repentance** is the 31-pt capstone — 6-second incapacitate, breaks on damage, undead/humanoid only.

Key talents: **Improved Blessing of Might** (5/5 — +25% AP from BoM), **Conviction** (5/5 — +5% melee crit), **Improved Judgement** (2/2 — -2s Judgement CD), **Improved Seal of the Crusader** (3/3), **Sanctity Aura** (1/1 — +10% Holy damage to party), **Improved Retribution Aura** (3/3 — +50% damage), **Two-Handed Weapon Specialization** (3/3 — +6% damage with 2H), **Sword/Mace Specialization** is **NOT** in the talent tree — Paladins rely on racial weapon-spec only, **Vengeance** (5/5 — +15% damage after a crit, 8s), **Seal of Command** (Ret 11-pt — chance to trigger an extra weapon-damage attack on swing, the **iconic Ret PvP / leveling seal**), **Crusade** (3/3 — +3% damage to all targets), **Repentance** (31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Holy raid healer** | **31/5/15** — Divine Intellect 5 / Spiritual Focus 5 / Imp Lay on Hands 2 / Healing Light 3 / Illumination 5 / Imp Blessing of Wisdom 2 / Holy Power 5 / Lasting Judgement 1 / Imp Concentration Aura 3 → 31 Holy + 5 Prot (Improved Devotion Aura) + 15 Ret (Conviction 5 / Benediction 5 / Pursuit of Justice 1 / Improved Blessing of Might 4) | MC/BWL/AQ40/Naxx primary healer | Illumination + Holy Light spam = canonical 1.12 healer rotation |
| **Protection tank** | **5/31/15** — 5 Holy (DI 5) + 31 Prot (Improved Devotion 5 / Toughness 5 / Anticipation 5 / Improved Concentration 3 / Blessing of Sanctuary 1 / etc. → Holy Shield 1) + 15 Ret (Conviction 5 / Improved Blessing of Might 5 / Reckoning 5) | MC/BWL off-tank / 5-man tank | Holy Shield + Sanctuary + Reckoning = AoE-tank niche; primary tank is still Warrior |
| **Reckoning bomb (gimmick)** | **21/0/30** or **5/16/30** — Reckoning maxed, Holy buffs for damage, Vengeance + Crusade + Repentance | World PvP burst | Stack 5 Reckoning charges over time (passive), then release for ~5 instant attacks; one-shots cloth |
| **Retribution PvP / world** | **0/14/37** — 14 Prot for Anticipation 5 + Improved Hammer of Justice 3 + Toughness 5 + Improved Devotion 1 + Reckoning 5 splash; 37 Ret to Vengeance + Crusade + Repentance | BG / duel / world | Hammer of Justice + Repentance = double-CC; sustained mid-DPS |
| **Leveling: Ret/Holy hybrid** | 5/0/15 → 15/0/30 → 31/0/20 by 60 | Solo questing | Ret for offensive, Holy splash for self-heal sustain. Paladins are the **slowest leveling class** in 1.12 — mitigated by Bubble + Bandage + Bandage + Bandage. |

**Engine spec-pick rule**: At lvl 10 — start with Holy splash (Spiritual Focus, Divine Intellect) for self-heal sustain regardless of intended end-state role; respec at 60 to role-spec. Paladin respec rate is high pre-60 because of leveling vs raid spec mismatch.

## Recommended weapons by bracket

| Bracket | Weapon class | Best in bracket | Why |
|---|---|---|---|
| 1-12 | 1H mace + shield | Vendor mace | 1H+shield = least death |
| 12-22 | 1H + shield or 2H | Quest greens; 1H+shield Holy for survival | |
| 22-40 | **Verigan's Fist** (2H mace) | Class quest at lvl 20 | Scales the entire bracket; Holy proc helps undead grinds |
| 40-50 | 2H mace/sword | Whirlwind-comparable 2H from Maraudon (Witchblade) / Searing Gorge / dungeons | |
| 50-58 | 2H | **Hammer of the Northern Wind** (Strat UD) / **Argent Avenger** (BS-crafted, Argent Dawn rep — Honored required) | |
| 58-60 (pre-raid) | 1H + libram + shield (Holy/Prot) OR 2H (Ret) | **Hammer of the Naaru** — that's TBC. Pre-raid: **Hammer of Grace** (Scholo, fast 1H mace) for Holy / **Truesilver Champion** (BS-crafted 2H) / **Quel'Serrar** (DM tribute, 1H sword tank weapon, see [warrior.md](warrior.md)) | |
| 60 (raid) | Per spec | Holy: **Hammer of the Lightbringer** / Hand of Rag (MC); Ret: **Sulfuras, Hand of Ragnaros** (BS recipe from Ragnaros bindings); Prot: Quel'Serrar 1H + Aegis of Stormwind shield | |

**Libram slot**: Paladins use a **Libram** ranged-slot relic (similar to Druid Idol, Shaman Totem, Priest Wand — wait, Priest uses Wand, Druid Idol, Shaman Totem-relic, Paladin Libram). Pre-raid Libram of Hope (Strat UD Postmaster) → MC drops better.

## Pre-raid BiS gear (Holy raid healer focus, MC entry)

`[verify pass 3 for exact item IDs and drop bosses]`

| Slot | Item | Source |
|---|---|---|
| Head | **Magister's Crown** (cloth — wait Paladins wear plate at 40, so plate at 60) → **Lightforge Helm** (T0) or **Soulforge Helm** (T0.5) | Strat UD Baron (T0) / quest (T0.5) |
| Neck | **Mark of Fordring** | EPL quest |
| Shoulders | **Truestrike Shoulders** (DPS) / **Mantle of Lost Hope** (Holy) | LBRS Mor Grayhoof / Strat UD Postmaster |
| Cloak | **Cloak of the Cosmos** / **Sage's Mantle** (cloak — n/a, Pal uses cape) → **Drape of Vaulted Secrets** | various |
| Chest | **Cassandra's Grace** (Scholo) / Lightforge Breastplate (T0) | |
| Bracers | **Bracelets of Wrath** (BoE crafted Holy) / **Wristguards of Stability** (BWL) | |
| Hands | **Lightforge Gauntlets** (T0) / **Hands of Power** (BoE) | |
| Belt | **Lightforge Belt** | |
| Legs | **Lightforge Legplates** | |
| Feet | **Lightforge Boots** | |
| Ring 1 | **Magni's Will** / Don Julio's Band (AV Exalted) | |
| Ring 2 | **Tarnished Elven Ring** (BoE) | |
| Trinket 1 | **Briarwood Reed** (Holy DPS) / **Royal Seal of Eldre'Thalas** (DM E) | |
| Trinket 2 | **Eye of the Beast** (LBRS Beasts) for Holy / **Diamond Flask** (Sunken Temple Warrior class quest — n/a for Pal) | |
| Weapon 1H | **Hammer of Grace** (Scholo) for Holy / **Quel'Serrar** for Prot | |
| Off-hand / Shield | **Aegis of Stormwind** (BS-crafted) for Prot / **Drillborer Disk** (BRD Plugger) | |
| Libram | **Libram of Hope** (Strat UD Postmaster) | |

## Tier set progression

| Tier | Set name (Paladin) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Lightforge Armor** | 8-piece, 60 5-mans | drop-only, BoP |
| **T0.5 (Dungeon Set 2)** | **Soulforge Armor** | Quest upgrade chain (patch 1.10) | Bridge to T1 |
| **T1** | **Lawbringer Armor** | Molten Core (8-piece — helm Onyxia, shoulders Magmadar, chest Sulfuron, hands Garr, legs Geddon, wrist Lucifron, belt Shazzrah, boots Golemagg) `[verify pass 3]` | Set bonus: improved aura range, mana on Judgement, etc. |
| **T2** | **Judgement Armor** | BWL + Onyxia (8-piece — helm Nefarian, chest Onyxia, shoulders Vael, hands Razorgore, legs Vael, wrist Broodlord, belt Firemaw/Ebonroc, boots Flamegor) `[verify pass 3]` | Iconic helm with horns; set bonus emphasizes Judgement mana + +20 spell damage and healing |
| **T2.5** | **Avenger's Battlegear** (Ret) / **Battlegear of Heroism** — wait that's Warrior; Pal AQ40 is **Battlegear of Eternal Justice** for Ret | AQ40 — token-based via Qiraji Bindings of Command/Dominance | Sub-spec branches: Ret vs Holy/Prot variants |
| **T3** | **Redemption Armor** | Naxx40 — 9-piece token-based | Set bonuses: massive healing throughput + threat reduction |

## Class trainer locations

| City | Faction | Paladin trainer NPCs | Notes |
|---|---|---|---|
| **Stormwind** | Alliance | **Cathedral of Light** — Brother Sammuel, Brother Crowley, Lord Grayson Shadowbreaker, Duthorian Rall (chain start) | Primary Pal hub for Alliance |
| **Ironforge** | Alliance | **Hall of Mysteries / Mystic Ward** — Brandur Ironhammer, Maxan Anvol, Tiza Battleforge | Chain-step location for High Priest Rohan |
| **Goldshire** | Alliance | **Brother Wilhelm** (lvl-12 Tome of Divinity starter for Humans) | Sub-trainer / chain start |
| **Kharanos** | Alliance | **Azar Stronghammer** (lvl-12 Tome of Divinity starter for Dwarves) | Sub-trainer / chain start |
| Darnassus | n/a | n/a — no NE Paladins in 1.12.1 | |
| Horde cities | n/a | n/a — no Horde Paladins in 1.12.1 | Blood Elves are TBC |

**Engine trainer-visit rule**: Same as Warrior — every even level threshold, schedule a trainer visit if `snapshot.UnlearnedSpells > 0`. Bundle with capital services.

## VMaNGOS / private server notes

- **Verigan's Fist chain** (lvl 20) is fully scripted — 3-instance chain works correctly.
- **Tome of Nobility / Warhorse** chain at lvl 40 is fully scripted. Free Warhorse + free riding works.
- **Charger chain** at lvl 60 — most steps scripted; **Death Knight Darkreaver** mini-boss waves in Scholomance have had occasional script-issue reports (waves not spawning if Rattlegore is killed too quickly) `[verify VMaNGOS scripting status pass 3]`.
- **Reckoning** in 1.12.1 stacks **infinitely** in the original retail patch (later capped at 4 in 1.13 / Classic 2019). Reckoning bomb is a true 1.12 mechanic — engine should know that on Classic 2019 the bomb is capped, so the spec is weaker on re-release than on 2006-canonical.

## Decision-Engine Rules

- **id:** `class.paladin.faction-lock` — IF `snapshot.Class==Paladin && snapshot.Faction==Horde` THEN **engine error** — character creation should not have allowed this on a 1.12.1 server.
- **id:** `class.paladin.race-lock` — IF `snapshot.Class==Paladin && snapshot.Race NOT IN {Human, Dwarf}` THEN **engine error** for the same reason.
- **id:** `class.paladin.redemption` — IF `Class==Paladin && Level>=12 && !Spells.Contains(Resurrection)` THEN start the **Tome of Divinity** chain at the race-appropriate starter NPC. Priority **920** (class identity).
- **id:** `class.paladin.verigans-fist` — IF `Class==Paladin && Level>=20 && !Items.Contains(VerigansFist)` THEN run the **Tome of Valor** chain. Priority **930** (class identity, weapon gates 20-40 bracket DPS). Bundle with planned Stockades / SFK / BFD runs.
- **id:** `class.paladin.warhorse` — IF `Class==Paladin && Level>=40 && !Spells.Contains(SummonWarhorse)` THEN run the **Tome of Nobility** chain. Priority **990** (class identity, free mount = the largest gold saving in the leveling game). **Always do before any non-class mount purchase.**
- **id:** `class.paladin.charger` — IF `Class==Paladin && Level==60 && !Spells.Contains(SummonCharger) && CopperOnHand >= 30000` (300g working capital — chain costs ~450g hard but engine should hold reserves) THEN start the **Summoning the Charger** chain. Priority **980** (class identity). Suspends raid-prep if `Role IN {Tank, Healer}`. **Multi-week chain** — engine plans phases as separate snapshot predicates.
- **id:** `class.paladin.charger.phase5` — IF `Class==Paladin && Level==60 && Spells.Contains(DivinationScryer placeholder) && !Spells.Contains(SummonCharger) && KeysInBags.Contains(SkeletonKey)` THEN form a 5-man Scholomance group for the Death Knight Darkreaver fight. Priority **970**.
- **id:** `class.paladin.tome-of-greatness` — covers the lvl-50 Greater Blessings unlock; bundle with capital trip.
- **id:** `class.paladin.respec-at-60` — IF `Class==Paladin && Level==60 && CopperOnHand>=respecCost(50g) && CurrentSpec != PlannedRoleSpec` THEN respec to role spec. Priority **700**.
- **id:** `class.paladin.libram-slot` — IF `Class==Paladin && Level>=60 && !Items.EquippedRanged.IsLibram` THEN equip best available Libram. Priority **600**.
- **id:** `class.paladin.greater-blessing-reagent` — Greater Blessings (lvl 50+) require **Symbol of Kings** stack. Engine must keep ≥40 Symbols in bags before raid action.

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `QuestsCompleted` (existing) — Tome of Divinity / Valor / Nobility / Charger chain quest IDs need enumeration `[verify pass 3]`
- `Spells` (planned) — Resurrection, SummonWarhorse, SummonCharger, Aura spells, Blessings, Holy Shock, Holy Shield, Repentance, Seal of Command
- `Items.Contains(itemId)` (planned) — Verigan's Fist, Libram of Hope, Symbol of Kings stack count
- `KeysInBags` (existing) — Skeleton Key for Scholo final phase
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)
- `CopperOnHand` (existing) — gates Charger chain start

## Cross-references

- [decision-engine/per-bracket-actions/02-l10-l20.md](../decision-engine/per-bracket-actions/02-l10-l20.md) — Verigan's Fist chain lands in this bracket
- [decision-engine/per-bracket-actions/04-l30-l40.md](../decision-engine/per-bracket-actions/04-l30-l40.md) — Warhorse chain at 40 (and the **gold-saving rule**: do NOT buy non-class mount before doing this)
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Charger chain at 60
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/warrior.md](warrior.md) — same-armor-tier class for Quel'Serrar reference
- [reputations/](../reputations/) (pass 7) — Hydraxian Waterlords for Pure Water + Argent Dawn for Argent Avenger weapon
