---
title: "Profession — Blacksmithing"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://vanilla-wow-archive.fandom.com/wiki/Blacksmithing
  - https://wowpedia.fandom.com/wiki/Blacksmithing_specialization
  - https://www.warcrafttavern.com/wow-classic/guides/blacksmith-specialization-guide/
  - https://www.wowhead.com/classic/guide/classic-wow-blacksmithing-armorsmith-vs-weaponsmith
crawl_date: 2026-05-01
---

# Blacksmithing — 1-300 Grind, Specialty Paths, Pre-Raid BiS Recipes

The plate-armor + 2H weapon profession. Pair-locks with **Mining**. Specialization at lvl 40 + skill 200 (**Armorsmith vs Weaponsmith**); sub-specialty at lvl 50 + skill 250 (**Sword/Axe/Hammer** for Weaponsmith). Specialty choice is **permanent** in 1.12.1 — the only way to change is to fully un-learn Blacksmithing and re-grind from 1. Top-end items: **Lionheart Helm** (Armorsmith plate-DPS BiS), **Stormherald** (Hammer 2H BiS), **Sulfuron Hammer** (legendary Sulfuras prep), **Arcanite Reaper** (BoE pre-raid 2H, no specialty required).

---

## Quick Facts

| Field | Value |
|-------|-------|
| Pair-lock | **Mining** (Iron/Mithril/Thorium nodes feed Blacksmithing) |
| Tier caps | Apprentice 75 → Journeyman 150 → Expert 225 → Artisan 300 |
| Specialty 1 (lvl 40, skill 200) | **Armorsmith** (Plate-tank + Plate-DPS) **vs** **Weaponsmith** (1H/2H weapons) |
| Specialty 2 (lvl 50, skill 250, Weaponsmith only) | **Sword** vs **Axe** vs **Hammer** sub-specialty |
| Specialty change | **Permanent** in 1.12 — un-learn + re-train Blacksmithing 1-300 to swap |
| Top BoE recipe | **Arcanite Reaper** (no specialty; world-drop recipe) |
| Top Armorsmith | **Lionheart Helm** (BiS plate-DPS pre-raid) |
| Top Weaponsmith (Sword) | **Persuader** + **Black Amnesty** + **Truesilver Champion** |
| Top Weaponsmith (Axe) | **Heartseeker** + **Annihilator** + **The Lobotomizer** |
| Top Weaponsmith (Hammer) | **Stormherald** + **Hammer of the Titans** + **Masterwork Stormhammer** |
| Legendary prep | **Sulfuron Hammer** (recipe = Thorium Brotherhood Honored, base for Sulfuras Hand of Ragnaros) |

---

## Skill Progression (1-300)

| Skill | Range | Recipes / mats | Trainer |
|-------|-------|---------------|---------|
| 1-40 | Apprentice | Rough Sharpening Stone (Rough Stone) → Rough Grinding Stone | Capital city BS trainer |
| 40-100 | Apprentice → Journeyman (75 cap) | Copper Bar Recipes (Copper Mace, Copper Belt, Copper Axe) | Same trainer; Journeyman quest at 50 |
| 100-150 | Journeyman | Bronze Bar (smelt 1 Tin + 1 Copper); Bronze items (Bronze Mace, Bronze Greatsword, Bronze Sharpening Stone) | Capital BS trainer |
| 150-200 | Journeyman → Expert (Expert quest at 125) | Iron Bar (smelt Iron Ore at Mining 100); Iron items (Iron Buckle, Iron Counterweight); Steel Bar (Iron + Coal) | Capital trainer |
| 200-225 | Expert | Mithril Bar (smelt Mithril Ore at Mining 175); Mithril items (Mithril Coif, Mithril Spurs, Mithril Headed Trout) | Capital trainer |
| 225-260 | Expert → Artisan (Artisan quest at 225) | Thorium Bar (smelt Thorium Ore at Mining 245); Thorium items (Thorium Belt, Thorium Boots) | **Trenton Lighthammer** (Tanaris) — Artisan-level trainer |
| 260-300 | Artisan | Arcanite Reaper, Arcanite Champion, Lionheart Helm, Stormherald, etc.; Master-tier recipes from world drops or rep | Various rep + recipe drops |

**Decision-engine rule:** profession-skill grind should align with mining-tier unlock (`Profession.Mining.Skill >= ProfessionTier.Required`). Engine should pause Blacksmithing leveling if Mining is < 50 below Blacksmithing target.

---

## Trainer Locations

| Tier | Trainer | Location |
|------|---------|----------|
| Apprentice (1-75) | Multiple per faction | Stormwind / Ironforge / Darnassus / Org / Undercity / Thunder Bluff |
| Journeyman (50-150) | Same trainer with quest | Capital cities |
| Expert (125-225) | Same with quest | Capital cities |
| Artisan (200-300) | **Trenton Lighthammer** (Gadgetzan, Tanaris) | Tanaris (Artisan-only specialty trainer) |
| **Armorsmith** (lvl 40 + skill 200) | **Grumnus Steelshaper** (Ironforge) / **Okothos Ironrager** (Orgrimmar) | Capital city Armorsmith specialty trainer |
| **Weaponsmith** (lvl 40 + skill 200) | **Ironus Coldsteel** (Ironforge) / **Borgosh Corebender** (Orgrimmar) `[verify pass 3]` | Capital city Weaponsmith specialty trainer |
| **Sword sub-specialty** (lvl 50 + skill 250) | **Seril Scourgebane** (Everlook, Winterspring) | Sub-spec trainer |
| **Axe sub-specialty** (lvl 50 + skill 250) | **Krathok Moltenfist** (Burning Steppes) `[verify pass 3]` | Sub-spec trainer |
| **Hammer sub-specialty** (lvl 50 + skill 250) | **Lilith the Lithe** (Stranglethorn Vale or Booty Bay) `[verify pass 3]` | Sub-spec trainer |

**Decision-engine rule:** sub-specialty trainer locations require travel to specific zones; engine should plan trainer-visit alongside questing in the relevant zone.

---

## Specialty Decision: Armorsmith vs Weaponsmith

### Armorsmith path (lvl 40 + skill 200)

**Best for:** Warrior tank, Paladin tank, Paladin healer (some pieces), plate-DPS Warrior/Pal.

**Key recipes (Artisan-tier, 260-300+):**

| Recipe | Skill required | Materials | BiS slot |
|--------|---------------|-----------|----------|
| **Imperial Plate Belt** | 245 | Thorium Bar (8) + Iron Bar (4) | Pre-raid plate belt |
| **Imperial Plate Helm** | 250 | Thorium Bar (12) + Heart of Fire (1) | Pre-raid plate helm |
| **Imperial Plate Boots** | 245 | Thorium Bar (8) + Citrine (1) | Pre-raid plate boots |
| **Imperial Plate Bracers** | 245 | Thorium Bar (8) + Aquamarine (1) | Pre-raid plate bracers |
| **Helm of the Stalwart Defender** | 290 | Truesilver Bar (10) + Black Mageweave (4) + Heart of Fire (4) | Tank-spec plate helm |
| **Stronghold Gauntlets** | 290 | Truesilver Bar (4) + Thorium Bar (12) + Massive Iron Stones (2) | Top tank-DPS plate hands |
| **Lionheart Helm** | 300 | 8 Truesilver Bar + 6 Arcanite Bar + 2 Pristine Hide of the Beast | **BiS plate-DPS helm pre-raid** (Strength + Stamina + Hit) |
| **Lionheart Champion** (1H) | 300 | Arcanite Bar (8) + Pristine Hide (2) + Truesilver Bar (10) | Top 1H plate-DPS sword |
| **Bloodsoul Embrace + Helm** | 300 | Black Diamonds + ZG drops + reputation rep | **BiS Argent Dawn-rep gear** (rep-locked) |
| **Plans: Lionheart Helm** | World drop | from Strat/Scholo trash | — |

### Weaponsmith path (lvl 40 + skill 200)

**Best for:** Warrior DPS, Hunter, Rogue, Paladin Ret, Druid (kits), Shaman (Enh).

**Sub-specialty at lvl 50 + skill 250:** Sword / Axe / Hammer.

#### Weaponsmith — Sword sub-specialty

| Recipe | Skill | Materials | Notes |
|--------|-------|-----------|-------|
| **Truesilver Champion** | 290 | Truesilver Bar (4) + Iron Bar (8) | Pre-raid 1H sword |
| **Persuader** (1H) | 300 | Arcanite Bar (10) + Truesilver Bar (10) + Felcloth (4) | Top Sword-spec 1H |
| **Black Amnesty** (1H) | 300 | Black Iron Bar (12) + Massive Iron Stones (4) + Demonic Rune (2) | Top Sword-spec 1H (BWL/MC tier) |

#### Weaponsmith — Axe sub-specialty

| Recipe | Skill | Materials | Notes |
|--------|-------|-----------|-------|
| **Heartseeker** (1H) | 300 | Arcanite Bar (4) + Truesilver Bar (10) + Heart of Fire (4) | Top Axe-spec 1H |
| **Annihilator** (1H) | 300 | Arcanite Bar (8) + Black Diamond (1) + Demon Stalker drops | Top Axe-spec 1H (Strat/Scholo) |
| **The Lobotomizer** (2H) | 300 | Arcanite Bar (10) + Truesilver Bar (8) + Massive Iron Stones (4) | Top Axe-spec 2H (Strat) |

#### Weaponsmith — Hammer sub-specialty

| Recipe | Skill | Materials | Notes |
|--------|-------|-----------|-------|
| **Hammer of the Titans** (2H) | 300 | Truesilver Bar (4) + Arcanite Bar (4) + Heart of Fire (4) + Massive Iron Stones (4) | Pre-raid 2H |
| **Masterwork Stormhammer** (2H) | 300 | Arcanite Bar (8) + Black Diamond (1) + Aquamarine (4) + Pristine Hide (2) | Top Hammer-spec 2H |
| **Stormherald** (2H) | 300 | Sulfuron Ingot (2) + Arcanite Bar (8) + Black Diamond (4) | **Top Hammer-spec 2H** (MC drops + Sulfuron crafting); Stun proc |

**Decision-engine rule:** Hammer specialty path is bracket-defining for Warrior tank/DPS at L60. Stormherald is the top-pre-raid 2H weapon for tanks (vs. Arcanite Reaper for DPS Warriors).

---

## Sulfuron Hammer (Pre-Raid Legendary Step)

**Sulfuron Hammer** is a 1H mace recipe; it is the **base item** for the **Sulfuras, Hand of Ragnaros** legendary mace.

| Step | Action | Source |
|------|--------|--------|
| 1 | Reach **Thorium Brotherhood Honored** rep (Searing Gorge + BRD turn-ins) | Long grind: ~10-25 hours |
| 2 | Buy **Plans: Sulfuron Hammer** from Lokhtos Darkbargainer (BRD; Thorium Brotherhood quartermaster) | 10g (Honored discount) |
| 3 | Craft **Sulfuron Hammer**: 8 Sulfuron Ingot + 20 Arcanite Bar + 50 Dark Iron Bar + 20 Blood of the Mountain | Most expensive recipe in 1.12 |
| 4 | Defeat **Ragnaros** (MC) → drops **Eye of Sulfuras** (1% rate) | MC raid |
| 5 | Combine Sulfuron Hammer + Eye of Sulfuras → **Sulfuras, Hand of Ragnaros** (legendary) | Final combination |

**Decision-engine rule:** Sulfuras chain is **multi-month**. Engine should accumulate Sulfuron Ingots from MC trash drops + Bloods of the Mountain from MC bosses + Arcanite Bar from Mining/Alchemy parallel.

See [../raids/molten-core.md](../raids/molten-core.md) for Eye of Sulfuras drop detail.

---

## Material Map (1-300 Mining + alt-economy)

| Tier | Bar | Source | Skill (Mining) |
|------|-----|--------|----------------|
| Tier 1 | Copper Bar | Copper Ore (smelt at trainer) | Mining 1 |
| Tier 2 | Bronze Bar | Tin Bar (50) + Copper Bar (50) | Mining 65 |
| Tier 3 | Iron Bar | Iron Ore (smelt) | Mining 100 |
| Tier 3+ | Steel Bar | Iron Bar (1) + Coal (1) | Smelt at any forge |
| Tier 4 | Mithril Bar | Mithril Ore (smelt) | Mining 175 |
| Tier 5 | Truesilver Bar | Truesilver Ore (smelt) | Mining 230 |
| Tier 6 | Thorium Bar | Thorium Ore (smelt) | Mining 245 |
| Tier 7 | Arcanite Bar | Thorium Bar (1) + Arcane Crystal (1) — **Alchemy Transmute** (24h CD) | Alchemy 275 |
| Tier 8 | Elementium Bar | Elementium Ore (BWL) + Pyrium Bar + Fiery Core (1) — Alchemy/Smelting at MC | Skill 300 + special quest |
| Tier 8 | Sulfuron Ingot | MC trash drops only | None — drop only |
| Tier 8 | Black Iron Bar | Smelt Dark Iron Ore at **Black Forge in BRD** (special location) | Mining 230 + BRD access |

**Decision-engine rule:** material acquisition flows from Mining tier to Blacksmithing recipe-tier. Engine should always pair-lock Mining-skill-required >= Blacksmithing-skill-target.

---

## Reputation Recipes (Honored / Revered / Exalted Locks)

| Faction | Honored recipe(s) | Revered recipe(s) | Exalted recipe(s) |
|---------|-------------------|-------------------|-------------------|
| **Thorium Brotherhood** | Sulfuron Hammer plans; Imperial Plate Belt patterns | Wildthorn Mail; Heavy Mithril Helm | Bloodsoul Embrace; Stronghold Gauntlets variants |
| **Argent Dawn** | Pre-raid plate gear blueprints | Argent Avenger crafting (rep-locked weapon) | Bracers + Boots BoP epic upgrades |
| **Cenarion Circle** | Cenarion Reservist gear (LW + BS partial) | Stronghold Gauntlets cenarion variant | Cenarion Vestments equivalent (BS variant) |
| **Timbermaw Hold** | Bracers + Boots crafted enchant component | — | — |
| **Hydraxian Waterlords** | — (mostly BS-irrelevant) | — | — |
| **Steamwheedle Cartel** | Mithril Mechanical Spider (Engineering, not BS) | — | — |

**Decision-engine rule:** Thorium Brotherhood is the primary BS rep grind for L60 endgame recipes. Argent Dawn is secondary for Bloodsoul Embrace and Argent Avenger. Cenarion Circle is tertiary for AQ40-rep crafted upgrades.

---

## Pre-Raid BiS Path (Plate-DPS Warrior)

For a Plate-DPS Warrior at L60, the Blacksmith-craftable BiS chain:

| Slot | Pre-raid BiS | Recipe / source |
|------|--------------|-----------------|
| Helm | **Lionheart Helm** | Armorsmith 300 + Pristine Hide quest reagent |
| Shoulders | **Truesilver Shoulders** or **Heavy Spiked Plate** | Armorsmith 280 / dungeon drop |
| Chest | **Imperial Plate Chest** or world drop | Armorsmith 250 |
| Bracers | **Imperial Plate Bracers** | Armorsmith 245 |
| Hands | **Stronghold Gauntlets** | Armorsmith 290 |
| Belt | **Imperial Plate Belt** | Armorsmith 245 |
| Legs | **Imperial Plate Leggings** | Armorsmith 245 |
| Boots | **Imperial Plate Boots** | Armorsmith 245 |
| Main-hand | **Persuader** (Sword) / **Heartseeker** (Axe) / **Stormherald** (Hammer) | Weaponsmith path-dependent |
| Off-hand | (varies — shield BS-craftable: **Heavy Mithril Shield** mid; **Force of Will** lvl 60 epic dungeon drop) | — |

**Decision-engine rule:** Plate-DPS Warrior BS path is **Armorsmith-locked** for full BiS slot coverage. Engine should pre-flag Armorsmith spec for plate-DPS bots.

For Warrior tank specifically: Armorsmith for **Helm of the Stalwart Defender** + **Stronghold Gauntlets**, plus Hammer-spec 2H for **Stormherald** (procs Stun for clutch interrupts). This requires sacrificing Stronghold Gauntlets (Armorsmith-locked) — engine must choose 1 specialty.

---

## Decision-Engine Rules

1. **Pair-lock with Mining**: `Profession.Mining.Skill >= Profession.Blacksmithing.Skill - 25`. Engine should pause BS leveling if Mining is too far behind.
2. **Specialty selection at lvl 40 + skill 200**: prompt user-config (`Snapshot.Profession.PreferredCraftingPath`) — Plate-DPS/Tank ⇒ Armorsmith; DPS Warrior/Hunter/Rogue/Pal Ret ⇒ Weaponsmith.
3. **Sub-specialty at lvl 50 + skill 250 (Weaponsmith only)**: Sword vs Axe vs Hammer based on character race + weapon-skill bonus (Human = Sword, Orc = Axe, Dwarf = Mace, etc.).
4. **Sulfuron Hammer chain**: gate on Thorium Brotherhood Honored rep first; THEN MC raid clear for Eye of Sulfuras.
5. **Lionheart Helm priority**: pre-raid plate BiS — engine should farm Pristine Hide of the Beast (Druid intro quest, Sunken Temple drop) + Truesilver/Arcanite Bars in parallel with leveling.
6. **Imperial Plate set baseline**: at skill 250+, engine should auto-craft full Imperial Plate set as pre-raid baseline (cheap, 100% craft, no rep gates).
7. **Weapon-specialty sub-trainer travel**: Sword sub at Everlook (Winterspring), Axe sub at Burning Steppes, Hammer sub at Booty Bay/STV. Engine should plan trip during questing in those zones.
8. **Mining vs Blacksmithing skill ratio**: Mining typically ahead by 5-15 skill. Engine should track delta and add brief Mining-route blocks if delta inverts.
9. **Black Iron Bar smelting**: special location at BRD's Black Forge (in the dungeon). Requires entering BRD — engine should batch-smelt during BRD farm runs.
10. **Specialty change protocol (rare)**: requires un-learning + re-leveling 1-300 (~30+ hours of /played + ~500g+ of materials). Engine should refuse-by-default; require explicit user-confirm flag.

---

## Snapshot Fields Needed

```text
Snapshot.Profession.Blacksmithing.Skill         // 1-300 grind progression
Snapshot.Profession.Blacksmithing.Specialty     // None/Armorsmith/Weaponsmith
Snapshot.Profession.Blacksmithing.SubSpecialty  // None/Sword/Axe/Hammer
Snapshot.Profession.Mining.Skill                // pair-lock dependency
Snapshot.Inventory.{IronBar, MithrilBar, ThoriumBar, ArcaniteBar}  // material reserves
Snapshot.Inventory.{SulfuronIngot, BlackIronBar, ElementiumBar}    // legendary/raid reagents
Snapshot.Inventory.PristineHideOfTheBeast       // Lionheart reagent
Snapshot.Inventory.HeartOfFire                  // multiple recipes
Snapshot.Inventory.MassiveIronStones            // Stronghold Gauntlets reagent
Snapshot.Reputation.ThoriumBrotherhood          // recipe lock
Snapshot.Reputation.ArgentDawn                  // recipe lock
Snapshot.Reputation.CenarionCircle              // recipe lock
Snapshot.Inventory.PlansLearned                 // recipe-track per plan
Snapshot.QuestLog.Active.SulfurasChain          // Sulfuras chain progress
Snapshot.Class                                  // determine plate-tank vs DPS path
Snapshot.Race                                   // racial weapon spec (Human=Sword, Orc=Axe, Dwarf=Mace)
```

---

## Cross-References

- Mining (pair-lock dependency): [../professions/](../professions/) — Mining file pending
- Alchemy (Arcanite Transmute): [alchemy.md](alchemy.md)
- Sulfuras chain: [../raids/molten-core.md](../raids/molten-core.md)
- Pristine Hide reagent (Druid class quest origin): [../classes/druid.md](../classes/druid.md), [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
- Thorium Brotherhood rep: [../reputations/](../reputations/) (file pending)
- Argent Dawn rep: [../reputations/argent-dawn.md](../reputations/argent-dawn.md)
- Cenarion Circle rep: [../reputations/](../reputations/) (file pending)
- Pre-raid BiS gear references: [../classes/warrior.md](../classes/warrior.md), [../classes/paladin.md](../classes/paladin.md)
