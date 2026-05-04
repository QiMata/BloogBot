# Warlock — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Warlock (canonical, modern)
> - https://www.wowhead.com/classic/guide/warlock-class-quests-classic-wow
> - https://www.icy-veins.com/wow-classic/warlock-quests-in-wow-classic
> - https://wowpedia.fandom.com/wiki/Dreadsteed_questline (referenced via search)
> - https://www.warcrafttavern.com/wow-classic/guides/warlock-epic-mount-quest/
> - https://vanilla-wow-archive.fandom.com/wiki/Dreadsteed (referenced via search)
>
> **Pass 2.** Some details (Dreadsteed final ritual location: DM West vs DM East confirmation, T0/T0.5 piece-by-boss) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Warlocks are the **highest sustained-DPS caster** in vanilla 1.12.1 and bring **mandatory raid utility**:

- **Curse of Elements** — +10% Shadow / Fire damage taken on target (mandatory raid debuff for all caster DPS)
- **Curse of Recklessness** / **Curse of Tongues** / **Curse of Weakness** — utility per-fight
- **Curse of Shadow** — alternative to CoE for Shadow-only fights
- **Banish** — single-target removal of Demon / Elemental (mandatory for several MC/BWL fights)
- **Soulstone** — pre-combat self-rez stone given to a healer; second of two self-rez resources alongside Shaman Reincarnation
- **Healthstone** — healthstone for every party member; rolling cooldown self-heal item
- **Summoning Ritual** (Ritual of Summoning) — 3-warlock ritual to teleport raid members; **single most-impactful raid logistics tool** in vanilla. Replaces Mage Portal for non-city destinations.
- **Soul Shards** — consumable mechanic; soul shards drop after a soulshard-creating spell on a soon-to-die enemy. Used for Healthstone, Soulstone, Summoning, Soul Fire, Curse of Doom, Inferno (lvl 50 Doomguard summon).

| Role | Spec | Strength |
|---|---|---|
| Raid DPS (Shadow) | SM/Ruin 30/0/21 | Shadow Mastery + Ruin = highest sustained Shadow-spell DPS |
| Raid DPS (Fire-vulnerable bosses) | DS/Ruin 0/30/21 | Imp-sacrifice Fire bonus + Ruin |
| Raid utility / off-tank | Soul Link Demo 0/30/21 → 0/35/16 | Soul Link with Voidwalker for damage absorption + survival |
| PvP / world | Affliction (Drain Tank) 21/0/30 | Siphon Life + Curse of Tongues + DoT spam |
| Leveling | Affliction → respec at 60 | DoT-based leveling; sustained-pull viability |

## Race availability + racial trait synergy

In 1.12.1 **Warlock is restricted to 4 races**: **Human, Gnome, Undead, Orc**. (No Dwarf, Night Elf, Tauren, Troll in 1.12.1; Troll Warlocks added in Cataclysm.)

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Human | Alliance | Sword/Mace Spec +5, **Diplomacy** (rep gain), Perception | Standard Alliance Warlock; Diplomacy speeds AD/Brood/Cenarion grinds |
| Gnome | Alliance | **Expansive Mind** (+5% int — defining caster racial), **Engineering Specialist**, **Escape Artist** (root break), small hitbox | **Best Alliance Warlock race** — +5% int = +5% mana pool. Escape Artist breaks Frost Nova / poly. |
| Undead | Horde | **Will of the Forsaken** (5s fear/sleep/charm immune, 2min CD), Cannibalize, Underwater Breathing, **Shadow Resist +10** | **Best Horde PvP Warlock** — WotF counters Priest fear / vs Warriors fearproof. Shadow Res 10 vs SP/other Warlocks. Plus the racial-flavor "shadow magic affinity". |
| Orc | Horde | **Axe Spec +5** (irrelevant for casters), **Blood Fury** (25% AP buff -healing taken — caster version is +ints? — actually Blood Fury for casters became a racial buff at TBC; in 1.12 Blood Fury is just AP+healing-debuff which is melee-only useful), **Hardiness** (25% reduced stun duration), **Command** (+5% pet damage — **defining Orc Warlock racial**) | **Best Horde DPS Warlock** — Command pet damage syncs with Imp / Voidwalker / Felhunter scaling. Hardiness is anti-Rogue PvP. |

**Engine race-pick rule** (Warlocks, by role):
- **Raid DPS** Alliance → **Gnome** (Expansive Mind +5% int)
- **Raid DPS** Horde → **Orc** (Command +5% pet damage scales raid-wide via Imp Firebolts and Felhunter mana drain)
- **PvP** → **Undead** (WotF) on Horde / **Gnome** (Escape Artist) on Alliance

## Class quests in level order — the Pet Chain

The Warlock pet chain is **the most class-quest-rich progression in vanilla** — 4 sequential pet quests at increasing levels. Each pet has distinct tactical use:

| Lvl | Pet | Quest chain | NPCs / Locations | Tactical role |
|---|---|---|---|---|
| **1** | **Imp** | Trainer-only quest (race-specific; "Your First Lesson" or equivalent) | Class trainer in starting zone | Ranged Firebolt DPS, **Blood Pact** (raid +Stamina buff — the canonical Warlock raid pet for tanks). |
| **10** | **Voidwalker** ("The Voidwalker Master") | Race-specific item collection. **Orc**: **Tablet of Verga**. **Undead**: **Egalin's Grimoire**. **Alliance (Hum/Gno)**: **Surena's Choker**. Item drops in starter-zone area. | Trainer → race-specific quest giver → kill mob for item → return → summon and defeat the Voidwalker | **Tank pet** (Sacrifice + Suffering taunt). Solo Warlock survival pet. |
| **20** | **Succubus** | Trainer → **Master Warlock in Ratchet** (Barrens) → **Ashenvale** to retrieve **Heartwood** (kill cultists) → return to Ratchet → summon and defeat Succubus | Multi-zone (Barrens + Ashenvale) | **DPS pet with Seduction CC** — group-content CC pet (Seduction works on humanoids — defining trash-CC tool). |
| **30** | **Felhunter** | **Strahad** (somewhere — `[verify pass 3]`) → **Jorah Annison** (Undercity, Horde) / **Krom Stoutarm** (Ironforge, Alliance) → **Moldy Tome** (Wetlands) + **Tattered Manuscript** (Thousand Needles) → return → **Wetlands** Dragonmaw Bonewarders / Shadowwarders for **3× Rod of Channeling** → return → Ratchet → summon and defeat Felhunter | Multi-zone, multi-step: Wetlands + Thousand Needles + Ratchet | **Mana-burn / mana-tap pet** for caster fights. Devour Magic = single-target spell-dispel utility. **Defining PvP pet** — anti-caster tool. |
| **40** | **Felsteed** (free mount + free riding skill) | Trainer | Summon Felsteed spell directly | 60% ground mount, no apprentice riding skill needed. Saves ~80g vs other classes at 40. Same mechanic as Pal Warhorse. |
| **50** | **Curse of Doom** trained | Trainer | Curse of Doom (60s DoT, applies on expire massive Shadow damage; cannot be applied during PvP combat) | Trainer-only |
| **50** | **Inferno** trained (summons a permanent Doomguard pet) | Trainer (requires 3 warlocks to channel summoning ritual to safely summon) | Inferno spell | Endgame solo summoning cooldown — rarely used in raids |
| **60** | **Dreadsteed** epic mount chain | **Mor'zul Bloodbringer** (Burning Steppes — talk to Demon Trainer in capital first) → **Gorzeeki Wildeyes** (goblin warlock in Burning Steppes) → buy reagents (~250g): **J'eevee's Jar (150g)**, **Black Lodestone (50g)**, **Xorothian Glyphs (50g)** → **Lord Banehollow** quest (Felwood Shadow Hold; Ulathek the Traitor) → Scholomance step (Kanrethad summoning ritual) → final ritual in **Dire Maul West** (defend channeling NPC against waves of demons during ~10-min channel) | **Summon Dreadsteed** (epic 100% ground mount, free riding skill, warlock-only) | **Multi-week chain.** Total cost ~250g + dungeon mat reagents + group runs. Priority **980** at lvl 60. |

### Dreadsteed chain details (engine planning notes)

- Phase 1: **Demon Trainer in capital** → directs to Burning Steppes
- Phase 2: **Mor'zul Bloodbringer** (Burning Steppes) → Gorzeeki Wildeyes → buy reagents (250g hard cost)
- Phase 3: **Lord Banehollow's Servant** chain — purchase Shadowy Potion (6g), find Ulathek the Traitor in Felwood Shadow Hold
- Phase 4: **Kanrethad summoning ritual** in Scholomance — requires Skeleton Key + 5-man Scholo group; ritual runs ~3 minutes during which wave-bosses spawn
- Phase 5: **Final ritual in Dire Maul West** — requires 5-man DM W group + Crescent Key. The summoning ritual NPC channels for ~10 minutes; players defend against waves of demons (lvl 60 elites, mix of imps/voidwalkers/succubi/felhunters/ravagers). Failure = restart from this phase.

**Key constraint**: Final phase is **lvl-60 5-man content** with mob-defense complexity comparable to Strat Baron Run. Engine should plan with 1+ healer + 2+ DPS minimum; full guild-supported run preferred.

## Other notable Warlock spells

| Lvl | Spell | Notes |
|---|---|---|
| 4 | Curse of Weakness | -damage on target |
| 8 | Healthstone | Health restoration consumable; Warlock crafts and distributes to party |
| 10 | Soulstone | Self-rez stone applied pre-combat to player; **mandatory raid healer/tank reagent** |
| 14 | Curse of Agony | DoT |
| 20 | Banish | 30s removal of Demon / Elemental |
| 24 | Drain Soul | Captures soul shard on kill |
| 30 | Drain Mana, Soul Link (talent) | |
| 40 | Howl of Terror (AoE fear), Death Coil (heal-self + fear), **Felsteed** | |
| 48 | Soul Fire | High-damage cast-time direct damage spell, uses 1 soul shard |
| 50 | Curse of Doom, Inferno | Endgame spells |
| 56 | Curse of the Elements | **Mandatory raid debuff** (+10% Shadow/Fire damage taken) |
| 60 | Shadowburn (talent), Conflagrate (talent) | |

## Talent trees (1.12 51-point trees)

### Affliction (Shadow Mastery / Dark Pact-era capstone)

DoT damage + curses + drains. The 31-pt capstone in 1.12 is **Dark Pact** (transfer pet mana to caster; instant cast).

Key talents: **Suppression** (5/5 — +10% spell hit on Affliction spells — **mandatory for raid hit cap**), **Improved Corruption** (5/5 — -2s Corruption cast → 0s instant), **Improved Curse of Weakness** (2/2), **Improved Drain Soul** (2/2 — +30% mana on Drain Soul), **Improved Life Tap** (2/2), **Improved Drain Life** (5/5 — +15% Drain Life damage), **Improved Drain Mana** (3/3), **Amplify Curse** (1/1 — next Curse of Weakness/Recklessness amplified, 3-min CD), **Curse of Exhaustion** (1/1 — slows target on hit), **Improved Curse of Agony** (5/5 — +10% damage), **Fel Concentration** (5/5 — pushback resistance on drains), **Grim Reach** (2/2 — +20% Affliction range), **Nightfall** (2/2 — chance to proc instant Shadow Bolt during DoT), **Shadow Mastery** (5/5 — +10% Shadow damage — **defining Affliction talent**), **Dark Pact** (1/1 — 31-pt capstone).

### Demonology (Soul Link capstone)

Pet survivability + utility. The 31-pt capstone **Soul Link** = active toggle: 30% of damage taken transferred to pet, 100% of damage absorbed by pet's mitigation.

Key talents: **Improved Healthstone** (2/2 — +20% Healthstone heal), **Improved Imp** (3/3 — +30% Firebolt / Blood Pact / Fire Shield), **Demonic Embrace** (5/5 — +15% Stamina — **defining survivability talent**), **Improved Voidwalker** (3/3 — +30% pet damage and stamina), **Fel Intellect** (3/3 — +15% pet Int), **Improved Succubus** (3/3 — +30% Lash of Pain / Soothing Kiss), **Fel Domination** (1/1 — instant pet summon, 15-min CD), **Fel Stamina** (3/3 — +15% pet Stam), **Master Summoner** (2/2 — -2s pet summon cast), **Master Demonologist** (5/5 — passive pet-aura buff that scales with active pet), **Soul Link** (1/1 — 31-pt capstone).

### Destruction (Ruin / Pyroclasm capstone)

Direct damage + AoE. The 31-pt capstone in 1.12 is **Ruin** (passive: 100% crit damage with Destruction spells — defining DPS talent).

Key talents: **Improved Shadow Bolt** (5/5 — +20% Shadow Bolt crit chance), **Cataclysm** (5/5 — -5% mana on Destruction), **Bane** (5/5 — -1.5s Shadow Bolt / Soul Fire cast), **Aftermath** (5/5 — chance to daze on Destruction crit), **Shadowburn** (1/1 — instant Shadow damage + soul shard chance, 15s CD), **Improved Firebolt** (2/2 — -0.5s Firebolt cast), **Devastation** (5/5 — +5% Destruction crit), **Improved Searing Pain** (3/3 — +6% crit), **Pyroclasm** (3/3 — chance to stun on Conflagrate / Rain of Fire / Soul Fire crit), **Improved Rain of Fire** (3/3 — -mana cost), **Conflagrate** (1/1 — consumes Immolate for big damage burst), **Ruin** (1/1 — 31-pt capstone, 100% crit damage with Destruction).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **SM/Ruin (raid Shadow DPS)** | **30/0/21** — deep Affliction (Suppression 5 / Imp Corruption 5 / Imp Drain Life 5 / Improved Curse of Agony 5 / Nightfall 2 / Shadow Mastery 5 + filler) → 30 Aff; Imp Shadow Bolt 5 / Bane 5 / Devastation 5 / Improved Searing Pain 1 / Ruin 1 → 21 Destro | Raid DPS, dominant spec | Curse of Elements + Corruption + Curse of Agony + Shadow Bolt rotation |
| **DS/Ruin (Fire-vulnerable)** | **0/30/21** — Imp Healthstone 2 / Demonic Embrace 5 / Imp Imp 3 / Imp VW 3 / Fel Intellect 3 / Imp Succubus 3 / Fel Stamina 3 / Master Demonologist 5 / **Demonic Sacrifice** (10-pt talent at top of 30-pt section) — wait that's TBC... | Fire-vulnerable boss DPS | Variant for raid DPS that uses pet sacrifice |
| **Soul Link Demo / off-tank** | **0/35/16** — deep Demonology: Soul Link → Pet absorbs 30% of damage; functional warlock-as-tank for soloing elite content | World, AQ20, Naxx 4HM | Stack of Voidwalker + Soul Link = 15% damage transferred + Voidwalker mitigation |
| **Affliction PvP (drain tank)** | **21/0/30** — moderate Aff to Nightfall + Suppression; moderate Destro to Devastation; emphasis on DoT + Drain Life | BG / arena | Sustained drain-tank PvP |
| **Leveling Affliction** | 0/0/15 → 30/0/21 by 60 (early Aff) | Solo questing | DoT spam + Drain Life self-sustain; can pull 2-3 mobs at once with VW tank |

## Recommended weapons by bracket

| Bracket | Weapon | Notes |
|---|---|---|
| 1-15 | Vendor staff or 1H+OH | Stat sticks |
| 15-30 | Quest staff | Stat sticks for INT/Spi |
| 30-45 | 1H + OH or staff | Stat sticks |
| 45-55 | Staff or 1H+OH | Truesilver Champion 2H staff (BS-crafted), Hand of Edward the Odd offhand 1H |
| 55-58 | Staff | Skullshatter Battlestaff (Strat) |
| 58-60 | Staff | **Headmaster's Charge** (Scholo) — pre-raid BiS spell-power staff |
| 60 (post-MC) | Staff | T1 set bonuses + MC drops |
| 60 (post-AQ40) | Staff | **Staff of the Qiraji Prophets** (AQ40), **Anubisath Warhammer** | |

## Pre-raid BiS gear

`[verify pass 3 for exact items]`

| Slot | Item | Source |
|---|---|---|
| Head | **Dreadmist Mask** (T0) → upgrade to Necrology Helm (T0.5) | Strat UD Baron / quest |
| Neck | **Mark of Fordring** | EPL |
| Shoulders | **Dreadmist Mantle** (T0) | LBRS Mor Grayhoof |
| Cloak | **Sage's Mantle** (Mage class) — Warlock equivalent: **Drape of Vaulted Secrets** (Strat) | |
| Chest | **Dreadmist Robe** (T0) — upgrade to Robe of the Archmage (Tailoring, Mage-only — n/a) — pre-raid Warlock chest is **Bloodvine Vest** (Tailoring BoP, world-recipe) | Tailoring 300 |
| Bracers | **Dreadmist Bindings** (T0) | |
| Hands | **Hands of Power** (BoE) | |
| Belt | **Dreadmist Belt** (T0) | |
| Legs | **Dreadmist Leggings** (T0) | |
| Feet | **Dreadmist Boots** (T0) | |
| Ring 1 | **Magni's Will** equivalent / Don Mauricio's Band | |
| Ring 2 | **Tarnished Elven Ring** (BoE) | |
| Trinket 1 | **Briarwood Reed** (DM N) | DM N |
| Trinket 2 | **Eye of the Beast** (LBRS Beasts) | |
| Weapon | **Headmaster's Charge** staff | Scholo Darkmaster Gandling |
| Wand | **Wand of the Whispering Dead** (Strat) / **Bonecreeper Stylus** | |

## Tier set progression

| Tier | Set name (Warlock) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Dreadmist Raiment** | 8-piece, 60 5-mans | Drop-only, BoP |
| **T0.5 (Dungeon Set 2)** | **Necrology Raiment** | Quest upgrade chain (patch 1.10) | Bridge to T1 |
| **T1** | **Felheart Raiment** | Molten Core (8-piece) | Set bonuses: -mana cost on summons + chance to gain mana on shadow crit |
| **T2** | **Nemesis Raiment** | BWL + Onyxia (8-piece) | Iconic horns helm; set bonuses: +25 spell damage + Soul Shard on critical |
| **T2.5** | **Doomcaller's Vestments** | AQ40 — token-based | |
| **T3** | **Plagueheart Raiment** | Naxx40 — 9-piece | Set bonuses: massive Shadow damage + threat reduction |

## Class trainer locations

| City | Faction | Warlock trainer NPCs | Notes |
|---|---|---|---|
| **Stormwind** | Alliance | **Slaughtered Lamb** (Mage Quarter, basement; warlock-specific building) — Demisette Cloyce, Sandahl, Maximillian Crowe | **Demon Trainer here as well** (Dreadsteed chain start NPC) |
| **Ironforge** | Alliance | **Forlorn Cavern** (next to Rogue trainers) — Briarthorn, Rardel Nodlinger | Gnome Warlocks train here too |
| **Goldshire** | Alliance | sub-trainer Thomas (lvl 1-10) | |
| **Orgrimmar** | Horde | **Cleft of Shadow** — Punra, Ophek, Talvash del Kissel, Strahad Farsan | Primary Horde Warlock hub |
| **Razor Hill** | Horde | sub-trainer Dhugru Gorelust | |
| **Undercity** | Horde | **Magic Quarter** (south wing) — Lazlo, Elysa, Daephesh | UD-side trainers |
| Darnassus / Thunder Bluff | n/a | n/a — no NE/Tauren Warlocks in 1.12.1 | |

## VMaNGOS / private server notes

- **Imp / Voidwalker / Succubus / Felhunter** quest chains are fully scripted on VMaNGOS.
- **Felsteed** at lvl 40 is a direct trainer-skill (no quest chain in VMaNGOS) — works correctly.
- **Dreadsteed final ritual** in Dire Maul West has had occasional script issues with wave-spawning timing; engine should plan 1-2 attempts. `[verify VMaNGOS scripting status pass 3]`
- **Ritual of Summoning** (3-warlock raid summon) works correctly; mandatory raid logistics tool.
- **Soul Shard mechanic** with Drain Soul on dying mobs works correctly. Soul Shards take bag space (1 per shard) — Warlocks use **Soul Shard Bag** (2-slot soul-shard-only) or **Felcloth Bag** (8-slot) to manage.
- **Curse of Doom** (lvl 50) is a 1.12-canonical spell; **does NOT work in PvP** in 1.12 (despawns on PvP target). Engine should not schedule it for PvP encounters.

## Decision-Engine Rules

- **id:** `class.warlock.race-lock` — IF `Class==Warlock && Race NOT IN {Human, Gnome, Undead, Orc}` THEN engine error.
- **id:** `class.warlock.summon-imp` — IF `Class==Warlock && Level>=1 && !Spells.Contains(SummonImp)` THEN run starter Imp quest. Priority **990** (immediate at lvl 1, no delay).
- **id:** `class.warlock.summon-voidwalker` — IF `Class==Warlock && Level>=10 && !Spells.Contains(SummonVoidwalker)` THEN run race-specific Voidwalker chain. Priority **940**. Voidwalker is the canonical solo-leveling tank pet.
- **id:** `class.warlock.summon-succubus` — IF `Class==Warlock && Level>=20 && !Spells.Contains(SummonSuccubus)` THEN run Ratchet → Ashenvale chain. Priority **920**.
- **id:** `class.warlock.summon-felhunter` — IF `Class==Warlock && Level>=30 && !Spells.Contains(SummonFelhunter)` THEN run multi-zone (Wetlands + Thousand Needles + Ratchet) chain. Priority **930** — Felhunter is the canonical PvP/CC pet and gates Devour Magic.
- **id:** `class.warlock.felsteed` — IF `Class==Warlock && Level>=40 && !Spells.Contains(SummonFelsteed)` THEN visit class trainer. Priority **990** — free 60% mount + free riding = the largest gold-saving in vanilla. **Always do before any non-class mount purchase.**
- **id:** `class.warlock.dreadsteed` — IF `Class==Warlock && Level==60 && CopperOnHand >= 25000 && !Spells.Contains(SummonDreadsteed)` THEN start Mor'zul Bloodbringer chain. Priority **980** at lvl 60. **Multi-week**: 250g hard reagent cost + Felwood/Burning Steppes/Scholo/DM W groups required.
- **id:** `class.warlock.curse-of-elements-stack` — IF `Class==Warlock && InRaid && Boss.IsShadowOrFireVulnerable && !TargetHasCurseOfElements` THEN apply Curse of Elements. Priority **820** (combat-time raid debuff).
- **id:** `class.warlock.healthstone-distribution` — IF `Class==Warlock && PartyMember.LacksHealthstone && OutOfCombat` THEN craft + give Healthstone to party. Priority **600** (raid prep).
- **id:** `class.warlock.soulstone-on-healer` — IF `Class==Warlock && InRaid && PrimaryHealer.LacksSoulstone && OutOfCombat` THEN apply Soulstone to primary healer (or designated battle-rez target). Priority **750** (pre-pull raid utility).
- **id:** `class.warlock.summon-ritual-on-request` — IF `Class==Warlock && Level>=40 && CountOtherWarlocksInRaid >= 2 && SummonRequest.Pending` THEN coordinate Ritual of Summoning. Priority **600**.
- **id:** `class.warlock.banish-target` — IF `Class==Warlock && Boss.SpawnsDemon && !DemonHasBanish` THEN Banish demon. Priority **820** (combat-time, mandatory for several MC bosses).
- **id:** `class.warlock.respec-at-60` — IF `Class==Warlock && Level==60 && CurrentSpec != PlannedRoleSpec` THEN respec to SM/Ruin (raid Shadow) / DS/Ruin (Fire-vulnerable) / Demo (off-tank) per role. Priority **750**.
- **id:** `class.warlock.soul-shard-stockpile` — IF `Class==Warlock && SoulShardCount < 8 && OutOfCombat && in solo questing` THEN cast Drain Soul on dying mobs. Priority **400** (background economy). Engine should not skimp on shard supply going into raid.

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `Spells` (planned) — SummonImp, SummonVoidwalker, SummonSuccubus, SummonFelhunter, SummonFelsteed, SummonDreadsteed, CurseOfElements, Banish, SoulFire, Inferno, CurseOfDoom, RitualOfSummoning
- `SoulShardCount` (planned — bag scan for soul shard items)
- `Items.HealthstoneCharges` (planned — Healthstone has 1 charge per cast)
- `PartyMember.LacksHealthstone` / `PrimaryHealer.LacksSoulstone` (planned — derived from party scan)
- `TargetHasCurseOfElements` (planned — boss debuff scan)
- `Boss.SpawnsDemon` / `DemonHasBanish` (planned — encounter-specific flags)
- `CountOtherWarlocksInRaid` (planned)
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)
- `CopperOnHand` (existing) — gates Dreadsteed reagent cost

## Cross-references

- [decision-engine/per-bracket-actions/01-l1-l10.md](../decision-engine/per-bracket-actions/01-l1-l10.md) — Imp at 1, Voidwalker at 10
- [decision-engine/per-bracket-actions/03-l20-l30.md](../decision-engine/per-bracket-actions/03-l20-l30.md) — Succubus at 20, Felhunter at 30
- [decision-engine/per-bracket-actions/04-l30-l40.md](../decision-engine/per-bracket-actions/04-l30-l40.md) — Felsteed at 40 (gold-saving rule)
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Dreadsteed at 60
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/paladin.md](paladin.md) — parallel Charger chain (same lvl-60 class-mount pattern)
- [classes/mage.md](mage.md) — Curse of Elements + Mage Improved Scorch debuff stacking
- [professions/](../professions/) (pass 6) — Tailoring Felcloth Bag (Warlock soul shard storage)
- [systems/](../systems/) (pass 10) — Soul Shard mechanics
