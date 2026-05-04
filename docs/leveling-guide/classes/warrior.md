# Warrior — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Warrior (canonical, modern; vanilla details thin)
> - https://www.icy-veins.com/wow-classic/warrior-quests-in-wow-classic
> - https://www.icy-veins.com/wow-classic/classic-warrior-leveling-guide
> - https://www.wowhead.com/classic/quest=1792/whirlwind-weapon
> - https://www.wowhead.com/classic/guide/warrior-class-quests-classic-wow
> - https://vanilla-wow-archive.fandom.com/wiki/Warrior_builds/Level_60 (referenced via search)
> - https://www.vanillawar.com/talents.html (referenced via search)
>
> **Pass 2.** Some endgame chain details (Quel'Serrar tribute event, exact tier piece-by-boss sources) marked `[verify pass 3]` and require a follow-up crawl pass.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Warriors are the unrivalled raid main-tank class in 1.12.1 (Druids and Paladins exist as off-tanks but lack proper threat tools without Salvation/Blessing-Of-Sanctuary stack-ups, and Shamans don't tank meaningfully). In raid DPS, Fury Warriors are the highest-melee-DPS spec when properly geared with mid-MC gear and consumables. PvP Warriors using Mortal Strike are top-tier in lvl-60 ladder with Hand of Rag / Lionheart sword.

| Role | Spec | Strength |
|---|---|---|
| Main Tank | Protection (8/5/38) | Sunder spam threat, Shield Slam burst, Last Stand panic |
| Raid DPS | Fury Bloodthirst (17/31/3) | Highest sustained melee DPS in MC/BWL/AQ40 |
| PvP / Open World | Arms Mortal Strike (31/20/0) | Burst + 50% healing reduction debuff = duel king |
| Leveling | Fury Cruelty (early) → Arms Sweeping Strikes (mid+) | Crit-driven solo grind |

## Race availability + racial trait synergy

All 8 races in 1.12.1 can roll Warrior. (Faction-locked classes are Paladin/Alliance and Shaman/Horde; Warrior is open.)

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Human | Alliance | **Sword Specialization +5**, **Mace Specialization +5**, Diplomacy (rep gain) | Best tank race — Lionheart Helm + sword + 5 weapon skill is canonical Human Warrior. Diplomacy speeds AD / TB / city rep grinds. |
| Dwarf | Alliance | **Mace Specialization +5**, **Gun Specialization +5**, Stoneform (8s bleed/poison/disease immune + 10% armor), Find Treasure | Stoneform PvP utility. Mace spec for Hand of Rag / Hand of Justice itemization. |
| Night Elf | Alliance | **Quickness** (1% dodge), Shadowmeld, Wisp Spirit | Dodge is substantial for tanks; Shadowmeld used for vanish-at-low-HP PvP cheese. |
| Gnome | Alliance | **Expansive Mind** (5% int — irrelevant), **Engineering Specialist** (+15 engineering), **Escape Artist** (cooldown root-break) | Smallest hitbox in the game — caster-LoS PvP advantage. Escape Artist breaks rogue/druid roots. |
| Orc | Horde | **Axe Specialization +5**, **Blood Fury** (25% AP buff 15s, -healing taken), Hardiness (25% reduced stun duration), Command (pet damage — irrelevant for Warrior) | Best raw DPS race. Blood Fury+Death Wish+Recklessness stack for burst. Hardiness is the highest-impact PvP racial. |
| Tauren | Horde | **Endurance** (+5% base health), **War Stomp** (2s AoE stun, 5y), Cultivation (+15 herbalism), Nature Resistance +10 | Largest hitbox — disadvantage in PvP. War Stomp is a strong panic-button. Endurance gives ~250 HP at 60. |
| Undead | Horde | **Will of the Forsaken** (immune fear/sleep/charm 5s, 2-min CD), Cannibalize (corpse heal), Underwater Breathing (15min) | WotF is the highest-impact PvP racial in the game vs Warlocks/Priests. Used to be 5-min CD; in 1.12.1 it's 2-min. |
| Troll | Horde | **Berserking** (10-30% haste based on missing HP, 10s CD ~3min), Bow/Throwing Specialization +5, Beast Slaying +5%, Regeneration | Berserking syncs with execute phase for raw DPS bursts. Best DPS racial *in execute range*. |

**Engine race-pick rule**: For PvP-flagged Warrior plans → Orc (DPS) or Undead (utility). For raid-tank plans → Human (Sword/Mace + Diplomacy) or Dwarf (Mace). For raid-DPS plans → Orc.

## Class quests in level order

| Lvl | Quest / Chain | Source / NPC | Reward | Engine action |
|---|---|---|---|---|
| 1 | Race-specific starter Warrior quest | Class trainer in starting zone | First weapon (often a low-DPS dagger or 2H sword) | Auto-accept on first trainer visit |
| 4 | **Charge** trained at trainer (no quest) | Trainer | — | Read trainer skills at lvl 4 |
| 10 | **Defensive Stance** + Sunder Armor + Taunt unlock chain | Trainer in capital → travel to a class-specific NPC by race (e.g., Sergeant De Vries / Thal'trak Proudtusk) | Defensive Stance, Taunt, Sunder Armor (rank 1) | Race-specific 2-NPC chain; bundle with bracket-2 hub bind |
| 20 | Trainer rank-ups for Battle Shout, Heroic Strike, Demoralizing Shout | Trainer | — | Pure trainer visit, no quest |
| 30 | **The Affray** → Berserker Stance | **Klannoc Macleod**, Fray Island, Barrens (68, 84) | Berserker Stance + Intercept | Wave-fight on Fray Island; final boss **Big Will (lvl 33 elite)**; soloable for Fury Warriors with low-mid greens, easier in 2-man |
| 30 | **The Windwatcher → Cyclonian → Whirlwind Weapon** | **Bath'rah the Windwatcher**, Alterac Mountains (78, 14) | Choice of **Whirlwind Axe** / **Whirlwind Sword** / **Whirlwind Warhammer** (2H) | Requires The Affray completed first. Materials: 8 Liferoot, 30 Witherbark / Bloodscalp Tusks, 1 Essence of the Exile (drops in Arathi/STV). Final boss **Cyclonian (lvl 40 elite air elemental)** — 2-3 player kill at lvl 30, soloable at 40+. Weapon is BiS 2H from 30 to ~45. |
| 30 | Whirlwind Axe DPS / speed: 79.0 dps (51-78 dmg, 2.80 speed) `[verify pass 3]` | n/a | n/a | n/a |
| 30 | Whirlwind Sword DPS / speed: 79.0 dps `[verify pass 3]` | n/a | n/a | n/a |
| 30 | Whirlwind Warhammer DPS / speed: 79.0 dps (slower, higher base damage) `[verify pass 3]` | n/a | n/a | n/a |
| 60 | **A Troubled Spirit** → Sunken Temple chain → **Diamond Flask** | Trainer in capital → Blasted Lands → Swamp of Sorrows → Sunken Temple | Diamond Flask trinket (+75 healing on use, on a 6-min CD) | Bundle with planned ST run; chain is short |
| 60 | **Quel'Serrar** epic 1H sword chain | Foror Phineas (Dire Maul West, after killing Pusillin in DM East to retrieve Foror's Compendium) → Pristinely Preserved Dragon Egg → DM N tribute event with dragon ground-lit on top | Quel'Serrar (epic 1H tank/DPS sword) | **`[verify pass 3]`** — exact item chain. Requires DM N tribute setup + dragon kill on top of DM. 5-man minimum, often 10-man. Multi-week if guild scheduling. |
| 60 | Class-set quest chain (Battlegear of Valor → Heroism aka Tier 0.5) — **patch 1.10 addition** | Multiple dungeon turn-ins | Tier 0.5 upgrade for each piece | Optional but a meaningful gear bridge between dungeon-set and MC. |

**Note on the lvl-30 Affray + Whirlwind chain:** A Warrior who skips this is leaving the highest-DPS 2H weapon for the entire 30-45 bracket on the table. Engine priority: **950** (class identity) at lvl 30. Soloable for Fury at lvl 30-31 with 2-3 attempts; trivial in a 2-man.

## Talent trees (1.12 51-point trees)

### Arms (Mortal Strike capstone, 31-pt at top of tree)

Key talents: **Improved Heroic Strike** (3/3), **Deflection** (5/5), **Improved Rend** (3/3), **Improved Charge** (2/2), **Tactical Mastery** (5/5 — keeps rage on stance change, defining Arms talent), **Anger Management** (1/1 — passive +1 rage every 3s), **Deep Wounds** (3/3 — bleed on crit), **Two-Handed Weapon Specialization** (5/5), **Impale** (2/2 — +20% crit damage on Arms abilities), **Sweeping Strikes** (1/1 — 5-swing cleave, 10s, 30s CD; defining AoE-leveling talent), **Mortal Strike** (1/1 — 31-pt capstone, 200% weapon damage + 50% healing-reduction debuff 10s).

### Fury (Bloodthirst capstone)

Key talents: **Cruelty** (5/5 — +5% melee crit), **Improved Demoralizing Shout** (5/5 — extra debuff %), **Unbridled Wrath** (5/5 — chance to gain rage on swing), **Improved Cleave** (3/3), **Enrage** (5/5 — +25% damage 12s after taking critical hit), **Improved Battle Shout** (5/5), **Death Wish** (1/1 — 30s self-buff: +20% damage, -20% damage taken? — actually +20% damage taken increase; 3-min CD), **Improved Berserker Rage** (2/2), **Flurry** (5/5 — +30% attack speed for 3 swings after a crit), **Bloodthirst** (1/1 — 31-pt capstone, instant attack dealing 45% AP damage; 8-charge HP-restore for next 5 attacks).

### Protection (Shield Slam capstone)

Key talents: **Improved Bloodrage** (2/2), **Tactical Mastery** (note: Tactical Mastery is in **Arms** tree, not Prot — Prot has Shield Specialization 5/5 + Anticipation 5/5 + Toughness 5/5), **Shield Specialization** (5/5 — block chance), **Anticipation** (5/5 — defense rating), **Improved Shield Block** (1/1), **Last Stand** (1/1 — 30% HP for 20s, 8-min CD), **Improved Sunder Armor** (3/3), **Defiance** (5/5 — +15% threat in Defensive Stance), **Toughness** (5/5 — armor multiplier), **Improved Disarm** (3/3), **Improved Shield Wall** (2/2 — extra 1 charge), **Concussion Blow** (1/1 — 5s stun), **One-Handed Weapon Specialization** (5/5 — flat damage with 1H), **Shield Slam** (1/1 — 31-pt capstone — strong threat + dispel + damage).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Fury Impale (raid DPS)** | **17/31/3** — Imp Heroic Strike 3 / Deflection 5 / Tactical Mastery 5 / Anger Management 1 / Deep Wounds 3 / Impale 2 → 17 Arms; Cruelty 5 / Imp Battle Shout 5 / Unbridled Wrath 5 / Enrage 5 / Death Wish 1 / Imp Berserker Rage 2 / Flurry 5 / Bloodthirst 1 → 31 Fury; Shield Specialization 3 → 3 Prot | MC/BWL/AQ40 main DPS spec | Stance-dance Battle ↔ Berserker; uses Bloodthirst as primary instant. |
| **Fury Cruelty leveling** | 0/31/0 progressively → 5/31/0 | Solo questing | Pure Fury for Cruelty + Flurry + Bloodthirst by lvl 40 |
| **Arms Mortal Strike PvP** | **31/20/0** — Imp Heroic Strike 3 / Deflection 5 / Tactical Mastery 5 / Imp Overpower 2 / Anger Management 1 / Deep Wounds 3 / Two-Handed Spec 5 / Impale 2 / Sweeping Strikes 1 / Mortal Strike 1 / Imp Hamstring 3 / Axe Spec 5 → 31 Arms; Cruelty 5 / Imp Battle Shout 5 / Unbridled Wrath 5 / Enrage 5 → 20 Fury | Open-world / BG ladder / duel king | MS + Hamstring is the canonical Warrior PvP combo |
| **Protection Tank** | **8/5/38** or **5/8/38** — token Arms (Imp HS, Deflection) / token Fury (Cruelty 5 for crit-rage uptime) / deep Prot (Shield Spec, Anticipation, Imp Sunder, Defiance, Toughness, Last Stand, Imp Shield Block, Concussion Blow, Improved Disarm, OH Spec, Shield Slam) | MC/BWL/Naxx main tank | Spam Sunder for threat; Shield Slam on cooldown; SS for top-end threat |

**Engine spec-pick rule** (priority in [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md#class-identity)): At lvl 10 (first talent point), pick by intended end-state role:
- AccountFlag.Warrior.Role = `Tank` → start Prot path immediately (Shield Spec → Anticipation → Toughness)
- AccountFlag.Warrior.Role = `RaidDPS` → leveling Fury (Cruelty → Flurry → Bloodthirst at 40 talent), respec at 60 to 17/31/3 Impale build (~50g)
- AccountFlag.Warrior.Role = `PvP` → Arms 31/20 from lvl 30 onward
- Default (no flag) → Fury leveling, respec at 60 to whatever role the account needs

## Recommended weapons by bracket

| Bracket | Weapon class | Best in bracket | Why |
|---|---|---|---|
| 1-15 | Vendor 2H or quest 2H | Anything — early greens are mostly equivalent | Don't farm specifically, take quest rewards |
| 15-25 | 2H sword/axe | Staff of Westfall (A) / Crescent Staff (H) — staff or 2H from WC / Deadmines | Both are quest-chain rewards from instances |
| 25-30 | 2H | Sang'thraze the Deflector (ZF later) / Corpsemaker (RFD) | 2H mace progression |
| 30-45 | **2H from Whirlwind Weapon quest** | Whirlwind Axe / Sword / Warhammer (choose by racial weapon spec — Orc → Axe, Human → Sword, Dwarf → Hammer) | Lvl 30 BiS 2H until ~45 |
| 45-50 | 2H | Ravager (SM Armory drop, lvl 36-42 — better at lower bracket actually) / Witchblade (Maraudon) | |
| 50-58 | 2H | **Arcanite Reaper** (BS-crafted, lvl 50, BoE) | The "Arc Reap" — pre-raid 2H BiS until MC drops a replacement. ~150g cost or self-craft. |
| 58-60 | 2H or 1H+OH | Arcanite Reaper / **Bonecrusher** (Strat Live) / **Dal'Rend's Sacred Charge** (UBRS Drakkisath) | Dal'Rend's main + off-hand is the canonical Fury setup pre-MC |
| 60 (raid entry) | DW 1H | **Dal'Rend's Sacred Charge + Mirah's Song** (BRD Princess) or **Dal'Rend's Tribal Guardian** | Pre-raid DW Fury BiS |
| 60 (post-MC) | 1H + OH | **Brutality Blade** (BRD Lord Roccor 1%) / Hand of Justice trinket procs | |
| 60 (post-BWL) | 1H + OH | **Maladath, Runed Blade of the Black Flight** / Ashkandi 2H / Empyrean Demolisher | |
| 60 (post-AQ40) | DW 1H | Vis'kag the Bloodletter / Death's Sting | |
| 60 (post-Naxx) | 1H/2H | The Hungering Cold / Might of Menethil 2H | |

**Engine weapon-skill rule**: Always prefer a weapon matching the character's **racial weapon-spec bonus**. E.g., a Human Fury Warrior should equip **Brutality Blade** + **Mirah's Song** before equipping a non-sword equivalent — the +5 Sword Specialization is a free crit + glance-conversion bonus that no other weapon class gets.

## Pre-raid BiS gear (Fury DPS focus, MC entry)

`[verify pass 3 for exact item IDs and drop bosses]`

| Slot | Item | Source |
|---|---|---|
| Head | **Lionheart Helm** | BS-crafted (Master Armorsmith), BoE; mats include Arcanite Bars, Pristine Hide of the Beast |
| Neck | Onyxia Tooth Pendant / Mark of Fordring (EPL quest) | quest reward |
| Shoulders | **Truestrike Shoulders** | LBRS Mor Grayhoof |
| Cloak | **Cape of the Black Baron** (BoE blue) / Cloak of Firemaw | UBRS / BWL |
| Chest | **Cadaverous Armor** | Stratholme Cannon Master Willey |
| Bracers | **Wristbands of True Flight** | LBRS Halycon (drop) |
| Hands | **Devilsaur Gauntlets** | LW-crafted (BoE) |
| Belt | **Belt of Preserved Heads** / Mugger's Belt | Stratholme Baron / Stratholme Postmaster |
| Legs | **Devilsaur Leggings** | LW-crafted (BoE) |
| Feet | **Battlechaser's Greaves** / Black Steel Bindings | DM E / BS-crafted |
| Ring 1 | **Magni's Will** / Don Julio's Band (AV Exalted) | Marshal Windsor reward (Alliance) / AV exalted |
| Ring 2 | **Painweaver Band** | Stratholme UD (Maleki the Pallid) |
| Trinket 1 | **Hand of Justice** | BRD trash chest / Lord Roccor |
| Trinket 2 | **Blackhand's Breadth** | UBRS Drakkisath head turn-in (also serves as Onyxia attune turn-in step for Horde) |
| MH | **Brutality Blade** / Untamed Blade (DM N tribute) | BRD / DM N |
| OH | **Mirah's Song** / Hand of Antu'sul | BRD Princess Moira / ZF |
| Ranged | **Heavy Crossbow of the Black Howl** / Bonechewer (UBRS) | BS-crafted / UBRS |

**Tank pre-raid BiS** is a different list dominated by stamina + defense pieces (Aegis of Stormwind shield BS-crafted, Drillborer Disk shield BRD Plugger, Quel'Serrar 1H sword, Truesilver Champion 2H optional).

## Tier set progression

| Tier | Set name (Warrior) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Battlegear of Valor** | 8-piece, all 60 5-mans (BRD/Strat/Scholo/UBRS/LBRS/DM) | Drop-only, BoP. ~10-30 hours of dungeon farming for full set. |
| **T0.5 (Dungeon Set 2)** | **Battlegear of Heroism** | Quest chain (added patch 1.10 Dec 2005) — turn-in T0 piece + raid mats | Each piece = 30-min run + 30-50g in mats; significant stat bump over T0 |
| **T1** | **Battlegear of Might** | Molten Core (8-piece — helm Onyxia, shoulders Magmadar, chest Sulfuron, hands Garr, legs Geddon, wrist Lucifron, belt Shazzrah, boots Golemagg) `[verify pass 3 — Onyxia drops T1 helms not T2 helms? confirm]` | Set bonuses include rage gen on hit, +60 AP, etc. |
| **T2** | **Battlegear of Wrath** | BWL (8-piece — helm Nefarian, chest Onyxia, shoulders Vael, hands Razorgore, legs Vael, wrist Broodlord, belt Firemaw/Ebonroc, boots Flamegor) `[verify pass 3]` | Set bonuses include hit chance + crit + +5 sword/axe/mace skill on 2-piece. |
| **T2.5** | **Conqueror's Battlegear** | AQ40 — 5 piece token-based (Qiraji Bindings of Command/Dominance turned in to questgiver in AQ40 lobby) | Strong sustain stat bump; 3-piece + 5-piece set bonuses |
| **T3** | **Dreadnaught's Battlegear** | Naxx40 — 9-piece (head, shoulder, chest, gloves, legs, belt, wrist, feet, ring) — token-based from various Naxx bosses (Four Horsemen, KT, Sapphiron, Patchwerk, Loatheb, Maexxna, Gluth, Anub'Rekhan, Heigan) | 2/4/6/8/9-piece set bonuses include massive threat reduction + life regen |

## Class trainer locations per faction

| City | Faction | Warrior trainer NPCs | Weapon master |
|---|---|---|---|
| **Stormwind** | Alliance | Old Town Barracks (Lyon Mountainheart, Lehna, Brock Stoneseeker) | **Woo Ping** (Old Town) |
| **Ironforge** | Alliance | Hall of Arms (Bromos Grummner, Granis Swiftaxe, Ulfir Ironbeard) | **Bixi Wobblebonk** (also Ironforge Hall of Arms) |
| **Darnassus** | Alliance | Warrior's Terrace (Sildanair, Strompeak, Aelthalyste) | **Ilyenia Moonfire** |
| **Orgrimmar** | Horde | Hall of the Brave / Valley of Honor (Sergeant Ba'sha, Snang, Thuwd) | **Sayoc** (axes/2H weapons), **Hanashi** (swords/maces) — both Valley of Honor |
| **Thunder Bluff** | Horde | Hunter's Rise / Warrior's Lodge (Krang Stonehoof, Ker Ragetotem, Karn Stonehoof) | **Ansekhwa** (limited weapon training; usually fly to Org) |
| **Undercity** | Horde | War Quarter (Christoph Walker, Dyana Talonshrike, Janet Hommers) | **Archibald** (War Quarter) |

**Engine trainer-visit rule**: Every 2 levels OR after dinging an even level threshold (10/20/30/40/50), schedule a trainer visit if `snapshot.UnlearnedSpells > 0`. Bundle with mailbox / auctioneer / repair visits to amortize the trip.

## VMaNGOS / private server notes

- **The Affray** wave-fight is well-scripted on VMaNGOS; Big Will spawns reliably.
- **Cyclonian** is reliable; the air-elemental fight is straightforward.
- **DM N tribute event for Quel'Serrar** has occasionally had issues on private servers with the dragon despawning if the run takes too long. **`[verify VMaNGOS scripting status pass 3]`**
- **Dungeon Set 2 turn-in chain** is fully scripted on most VMaNGOS forks.
- **Tier 2 itemization** is generally correct; some tier-piece-by-boss assignments may differ from 1.12.1 retail (server tuning). Engine should confirm boss drops via WoWHead Classic before treating any tier piece as guaranteed.

## Decision-Engine Rules

- **id:** `class.warrior.affray` — IF `Class==Warrior && Level>=30 && !QuestsCompleted.Contains(2459)` (The Affray, ID `[verify pass 3]`) THEN travel to Fray Island (Barrens 68,84) and complete The Affray. Priority **950** (class identity). Suspends questing.
- **id:** `class.warrior.whirlwind` — IF `Class==Warrior && QuestsCompleted.Contains(<Affray>) && !QuestsCompleted.Contains(<WhirlwindWeapon>)` THEN run The Windwatcher → Cyclonian → Whirlwind Weapon. Priority **940**. Choose reward by racial weapon-spec: Orc → Axe, Human → Sword, Dwarf/Tauren → Warhammer. Other races default to Sword (most universally itemized late-game).
- **id:** `class.warrior.sunken-temple-trinket` — IF `Class==Warrior && Level>=60 && !Items.Contains(DiamondFlask)` AND a Sunken Temple run is on the action menu THEN bundle the chain. Priority **620** (gear gate, low cost). Diamond Flask is a competitive 60-second-burst trinket pre-raid.
- **id:** `class.warrior.quel-serrar` — IF `Class==Warrior && Level==60 && Role IN {Tank, Hybrid} && !Items.Contains(QuelSerrar)` AND DM access THEN add Quel'Serrar chain to action menu. Priority **800** (critical-path tank weapon). Long chain; engine should plan a multi-week sub-goal.
- **id:** `class.warrior.respec-at-60` — IF `Class==Warrior && Level==60 && CopperOnHand>=respecCost(50g) && CurrentSpec != PlannedRoleSpec` THEN respec to planned role spec (Fury 17/31/3 raid DPS, Arms 31/20 PvP, Prot 8/5/38 tank). Priority **700** (gear/role gate). Cost decays 5g/30 days from peak.
- **id:** `class.warrior.lvl40-dual-wield-trainer` — IF `Class==Warrior && Level>=20 && Spec==Fury && !Spells.Contains(DualWield)` THEN visit class trainer to learn Dual Wield (lvl 20 trainer skill, 1g cost). Priority **800**.
- **id:** `class.warrior.weapon-master` — IF `Class==Warrior && Level>=10 && WeaponSkill[<weaponInUse>] == 0` AND in capital THEN visit weapon master to train weapon skill (1g). Priority **700**.
- **id:** `class.warrior.mainhand-weapon-skill-grind` — IF `WeaponSkill[mainhand] < 5*Level` AND solo-questing THEN prefer attacking dummies / low-level mobs to top off weapon skill. Priority **300** (background, only when nothing higher). Specifically critical entering raids — 300 weapon skill against lvl 63 raid bosses dramatically reduces glancing-blow penalty.

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `QuestsCompleted` (existing) — quest IDs for The Affray, Whirlwind Weapon, Diamond Flask, Quel'Serrar steps need to be enumerated `[verify pass 3]`
- `Spells` (planned) — needed to detect Dual Wield, Defensive Stance, Berserker Stance, Sunder Armor, Bloodthirst, Mortal Strike, Shield Slam
- `Items.Contains(itemId)` (planned helper) — needed for Whirlwind weapon ownership check, Diamond Flask, Quel'Serrar
- `WeaponSkill[type]` (existing) — needed for weapon-skill-grind rule
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)
- `Role` (planned account-level: `Tank | RaidDPS | PvP | Hybrid | Leveling`)
- `CopperOnHand` (existing) — respec cost gate

## Cross-references

- [decision-engine/per-bracket-actions/03-l20-l30.md](../decision-engine/per-bracket-actions/03-l20-l30.md) — Affray + Whirlwind Weapon lands here
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Quel'Serrar + Diamond Flask + respec land here
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class-identity priority band
- [systems/](../systems/) (pass 10) — talents, weapon-skill, respec mechanics will live here once written
- [attunements/](../attunements/) (pass 9) — Warrior-relevant attunes (BWL Vael orb is class-agnostic)
