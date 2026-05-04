---
title: "Profession — Enchanting"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Enchanting
  - https://vanilla-wow-archive.fandom.com/wiki/Enchanting (403 — content from training)
crawl_date: 2026-05-01
---

# Enchanting — 1-300 Grind, Raid-Tier Chants, Disenchant Economy

The "no pair-lock" profession. Materials come exclusively from **disenchanting items** (greens, blues, epics) — Enchanting feeds itself by destroying magical equipment for materials. Top-end chants in 1.12 are **bracket-defining for raid DPS/healing**: **Crusader** (BiS holy weapon proc), **Mongoose** (BiS agi weapon proc), **Lifestealing**, **Demonslaying**, **+30 Spirit chest**, **+9 Stamina chest**. Recipes gate behind raid drops, world drops, and reputation rep (Argent Dawn for Crusader; Zandalar Tribe for ZG-themed enchants).

---

## Quick Facts

| Field | Value |
|-------|-------|
| Pair-lock | **None** — material comes from Disenchant action on items |
| Tier caps | Apprentice 75 → Journeyman 150 → Expert 225 → Artisan 300 |
| Specialty | None (Enchanting has no spec) |
| Material flow | Disenchant green/blue/epic items → Dust/Essence/Shard/Crystal materials |
| Top BiS chants | **Crusader** (Argent Dawn rep), **Mongoose** (raid recipe drop), **+30 Spirit** chest, **+9 Stamina** chest |
| Daily-economy | Disenchant drops from dungeon runs → AH Materials | Standard income source |
| Class synergy | Mage/Warlock/Priest/Druid for **Enchant Weapon: Healing Power** caster path |

---

## Skill Progression (1-300)

| Skill | Range | Recipes / mats | Trainer |
|-------|-------|---------------|---------|
| 1-50 | Apprentice | Strange Dust → Lesser Magic Essence; Runed Copper Rod (cheapest first chant: Enchant Bracer Minor Stamina) | Capital city Enchanter trainer |
| 50-100 | Apprentice → Journeyman (75 cap) | Strange Dust + Greater Magic Essence; Runed Silver Rod | Same trainer + Journeyman quest at 50 |
| 100-150 | Journeyman | Soul Dust + Lesser Astral Essence + Small Glimmering Shard; Runed Golden Rod | Same |
| 150-225 | Journeyman → Expert (Expert quest at 125) | Vision Dust + Lesser Mystic Essence + Small Glowing Shard; Runed Truesilver Rod | Same |
| 225-260 | Expert | Dream Dust + Lesser Nether Essence + Small Radiant Shard | Same |
| 260-300 | Expert → Artisan (Artisan quest at 225) | Illusion Dust + Greater Eternal Essence + Large Brilliant Shard + Nexus Crystal | **Master Enchanter** in capital + Artisan-tier recipes from rep/world drops |

**Decision-engine rule:** Enchanting is **the most material-efficient profession** for a non-gathering character. Engine should always Disenchant gray/green drops if Enchanting is on the char.

---

## Material Tier Map

| Tier | Source items (disenchant) | Materials |
|------|---------------------------|-----------|
| Tier 1 (1-25) | Lvl 1-15 greens | Strange Dust |
| Tier 1 (25-50) | Lvl 1-15 greens | Lesser Magic Essence |
| Tier 1 (50+ blues) | Lvl 1-15 blues | Greater Magic Essence + Small Glimmering Shard |
| Tier 2 (75-150) | Lvl 16-25 greens | Soul Dust + Lesser Astral Essence + Greater Astral Essence + Large Glimmering Shard |
| Tier 3 (150-225) | Lvl 26-35 greens | Vision Dust + Lesser Mystic Essence + Greater Mystic Essence + Small/Large Glowing Shard |
| Tier 4 (225-275) | Lvl 36-45 greens | Dream Dust + Lesser Nether Essence + Greater Nether Essence + Small/Large Radiant Shard |
| Tier 5 (275-300) | Lvl 46-60 greens | Illusion Dust + Lesser Eternal Essence + Greater Eternal Essence + Small/Large Brilliant Shard |
| Epic-tier | Lvl 60 epic items | Nexus Crystal (rare) |

**Decision-engine rule:** Disenchant decision tree — engine should Disenchant any green item the bot won't equip (`Equipment.WillEquip == false && Item.Quality == Green`). Vendoring for gold is rarely better.

---

## Trainer Locations

| Tier | Trainer | Location |
|------|---------|----------|
| Apprentice (1-75) | Multiple per faction | Stormwind / Ironforge / Darnassus / Org / Undercity / Thunder Bluff |
| Journeyman (50-150) | Same trainer with quest | Capital cities |
| Expert (150-225) | Same with quest | Capital cities |
| Artisan (200-300) | **Enchantress Eldrinda** (Stormwind, Mage Quarter); **Hgarth** (Undercity) `[verify pass 3]` | Capital city Artisan-only Enchanter trainer |
| Master (300+) | Many recipes are world-drop only; raid-tier from raid drops | — |

---

## Top-End Chant Recipes (L60 / Raid-Tier)

### Weapon enchants (BiS)

| Recipe | Source | Effect |
|--------|--------|--------|
| **Enchant Weapon - Crusader** | **Argent Dawn Honored** rep recipe (Light's Hope quartermaster) | **Best DPS proc enchant**: 50% chance proc on hit; +60 Strength + 100 Healing for 15s; raid-tier procs |
| **Enchant Weapon - Mongoose** | World drop in AQ40 / Naxxramas trash; rare BoE recipe | **Best agi proc enchant**: chance to proc +120 Agility for 15s + 2% haste; PvP & raid BiS for Hunter/Rogue/Warrior DPS |
| **Enchant Weapon - Lifestealing** | World drop / vendor `[verify pass 3]` | Chance to proc 30 dmg + heal back; tank survivability |
| **Enchant Weapon - Demonslaying** | World drop in Felwood/Outland-tier `[verify pass 3]` | +20 damage vs demons (MC/BWL/Naxx-niche; not general use) |
| **Enchant Weapon - Healing Power** | Recipe drop from ZG / mid-tier dungeons | +55 Healing Power on weapon — caster healer BiS |
| **Enchant Weapon - Spell Power** | Recipe drop from raids | +30 Spell Damage — caster DPS |
| **Enchant Weapon - Major Striking** | Trainer + Vision Dust farm | +9 weapon damage; baseline pre-raid |
| **Enchant Weapon - Major Intellect** | Recipe drop | +22 Intellect; Mage caster |

**Decision-engine rule:** for caster characters, Healing Power or Spell Power weapon enchants depend on spec; Crusader is universal-DPS proc; Mongoose is agi-DPS proc. Engine should pre-determine weapon enchant based on `Snapshot.Class.Role`.

### Chest enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Chest - Major Health** | 285 | +100 Health |
| **Enchant Chest - Major Mana** | 285 | +100 Mana (Caster preference) |
| **Enchant Chest - Greater Stats** | 295 | **+4 to all stats** — universal pre-raid BiS |
| **Enchant Chest - Restore Mana Prime** | 290 | +30 Spirit + Mana regen — healer BiS |

**Decision-engine rule:** Greater Stats (+4 all) is the universal default. Mana-regen variants only for healers/casters.

### Bracer enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Bracer - Greater Stats** | 290 | +4 all stats |
| **Enchant Bracer - Strength** | 285 | +9 Strength |
| **Enchant Bracer - Spirit** | 270 | +9 Spirit |
| **Enchant Bracer - Healing Power** | 290 | +24 Healing Power |
| **Enchant Bracer - Spell Power** | 290 | +12 Spell Damage |

### Hand/Glove enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Gloves - Riding Skill** | 290 | **+15% mount speed for 5s** (PvP/raid utility) `[verify pass 3]` |
| **Enchant Gloves - Threat** | 280 | +2% threat (Warrior tank) |
| **Enchant Gloves - Healing Power** | 290 | +30 Healing Power |
| **Enchant Gloves - Greater Strength** | 285 | +7 Strength |
| **Enchant Gloves - Greater Agility** | 285 | +7 Agility |

### Cloak enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Cloak - Stealth** | 280 | +5 Stealth (Rogue/Druid Cat) |
| **Enchant Cloak - Greater Resistance** | 290 | +5 all resistances |
| **Enchant Cloak - Greater Agility** | 290 | +5 Agility |
| **Enchant Cloak - Subtlety** | 290 | -2% threat (raid DPS) |
| **Enchant Cloak - Lesser Agility** | 195 | +3 Agility (mid-game) |

### Boot enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Boots - Greater Stamina** | 285 | +7 Stamina |
| **Enchant Boots - Greater Agility** | 290 | +7 Agility |
| **Enchant Boots - Spirit** | 270 | +5 Spirit |
| **Enchant Boots - Minor Speed** | 250 | **+8% movement speed** — universal QoL (1.12-specific stack with Mongoose haste? `[verify pass 3]`) |

**Decision-engine rule:** Minor Speed boot enchant is bracket-defining (8% speed lifelong). Engine should auto-craft for any character above L40 if Enchanting is available on alt.

### Shield enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Shield - Major Stamina** | 285 | +12 Stamina (tank) |
| **Enchant Shield - Lesser Block** | 200 | +5 Block |
| **Enchant Shield - Resistance** | 270 | +7 all resistance (Sapphiron) |

### Ring enchants

| Recipe | Skill | Effect |
|--------|-------|--------|
| **Enchant Ring - +1 Healing Power** | Various | +1 Healing (low-end) |

**Note:** Rings can only be enchanted by the wearer themselves (enchanter on the wearer = no — only own-ring enchant).

---

## Reputation Recipes

| Faction | Tier | Recipe |
|---------|------|--------|
| **Argent Dawn** | Honored | **Enchant Weapon - Crusader** — buy from Argent Dawn quartermaster (Light's Hope Chapel) |
| **Argent Dawn** | Revered | Enchant Cloak - Greater Resistance recipe; Enchant Bracer - Spirit recipe |
| **Argent Dawn** | Exalted | Argent-Avenger crafted gear support |
| **Cenarion Circle** | Honored | **Enchant Bracer - Healing Power**; **Enchant Bracer - Mana Regeneration** |
| **Cenarion Circle** | Revered | Cenarion Reservist's Boots recipe (LW + Enchant component) |
| **Cenarion Circle** | Exalted | Cenarion Vestments crafted (Tailoring + Enchant component) |
| **Hydraxian Waterlords** | Friendly | **Enchant Weapon - Lifestealing** `[verify pass 3]` |
| **Thorium Brotherhood** | Honored | **Enchant Cloak - Subtlety** + recipes for Mining-related-class items `[verify pass 3]` |
| **Timbermaw Hold** | Exalted | **Enchant Bracer - Mana Regeneration** + Boot variant — top mana-regen for casters |
| **Zandalar Tribe** | Honored | ZG-themed enchant recipes (e.g., **Enchant 2H Weapon - Agility** `[verify pass 3]`) |
| **Zandalar Tribe** | Revered | Mid-tier Tailoring recipes; **Enchant Cloak - Stealth** alternate |

**Decision-engine rule:** Crusader (AD Honored) is the highest-impact enchant rep recipe. Engine should grind AD to Honored as fast as possible if Enchanting is on the bot.

---

## World-Drop Recipes (Top BiS)

| Recipe | Source | Skill required |
|--------|--------|----------------|
| **Enchant Weapon - Mongoose** | AQ40 / Naxxramas trash drops; very rare BoE | 300 |
| **Enchant Weapon - Spell Power** | World drop or raid trash | 300 |
| **Enchant Cloak - Subtlety** | World drop (lvl 60 cloth/leather mob zones) | 300 |
| **Enchant Boots - Minor Speed** | World drop (mid-tier) | 290 |
| **Enchant Chest - Major Stats (+4)** | World drop or rep | 295 |

**Decision-engine rule:** Mongoose is the **lottery enchant** — extremely rare drop. Engine should track AH listings; recipe cost ~5000g+ on most servers.

---

## Disenchant Economy

Enchanting is **the highest-margin profession** for AH-flipping at L60+:

| Material | Source | AH price (server-dependent) |
|----------|--------|-----------------------------|
| Strange Dust | Lvl 1-15 disenchants | 1-3s per Dust |
| Soul Dust | Lvl 16-25 disenchants | 5-10s per Dust |
| Vision Dust | Lvl 26-35 disenchants | 15-30s per Dust |
| Dream Dust | Lvl 36-45 disenchants | 50-80s per Dust |
| Illusion Dust | Lvl 46-60 disenchants | 100-200s per Dust |
| Greater Eternal Essence | Lvl 46-60 blue disenchants | 50-100s per Essence |
| Large Brilliant Shard | Lvl 46-60 blue disenchants | 100-300s per Shard |
| Nexus Crystal | Epic disenchants | 50-100s per Crystal `[verify pass 3]` |

**Decision-engine rule:** at L60, disenchant rate of dungeon greens/blues vs. AH-flip dictates whether bot should:
- (A) Disenchant all and craft chants for personal use
- (B) Disenchant all and AH the materials for gold
- (C) Vendor only gray gear; disenchant only specific items with material targets

Engine should route based on `Snapshot.Profession.Enchanting.MaterialReserve` per material type vs. crafting plan.

---

## Decision-Engine Rules

1. **No pair-lock**: Enchanting is the only standalone profession; can be on any character without gathering compromise.
2. **Disenchant decision**: green/blue items the bot will not equip = always Disenchant. Gray = always vendor.
3. **Crusader chain**: gate on Argent Dawn Honored rep; engine should grind AD parallel with weapon-enchant target.
4. **Mongoose acquisition**: AH-watch + raid trash farming; recipe ~5000g pre-acquisition. Engine should NOT block on this — fall back to Crusader.
5. **+4 Stats chest baseline**: engine should auto-craft and apply Greater Stats (chest +4) for any L55+ character if reagents available.
6. **Boots Minor Speed**: 8% movement speed is universally bracket-defining. Engine should auto-craft for L40+ characters.
7. **Riding Skill glove enchant** `[verify pass 3]`: 15% mount speed for 5s active proc — useful for PvP or raid-emergency relocate. Engine optional.
8. **Class-role enchant routing**:
   - Plate-DPS (Warrior/Pal Ret): Crusader weapon + +9 Strength gloves + Mongoose if available
   - Mail-DPS (Hunter/Sham Enh): Mongoose weapon + Greater Agility gloves + Greater Stamina boots
   - Leather-DPS (Rogue): Mongoose weapon + Greater Agility gloves + Stealth cloak (vs. Subtlety)
   - Cloth-DPS (Mage/Wlk): Spell Power weapon + +22 Intellect bracer + Subtlety cloak
   - Healer (Pri/Pal Holy/Sha Resto/Dru Resto): Healing Power weapon + Healing Power bracer + Restore Mana Prime chest
9. **Raid-tier rep grind sequence**: Argent Dawn (Crusader) → Timbermaw (mana regen) → Cenarion Circle (healer-tier) → Zandalar (ZG enchants) — engine should round-robin across rep grinds during dungeon downtime.
10. **AH disenchant flipping**: at L60, monitor AH for cheap green/blue stacks; bulk-disenchant for material flipping if `MaterialPriceDelta > 30%` margin.

---

## Snapshot Fields Needed

```text
Snapshot.Profession.Enchanting.Skill            // 1-300 grind progression
Snapshot.Profession.Enchanting.RecipesLearned   // recipe-track per chant
Snapshot.Inventory.{StrangeDust, SoulDust, VisionDust, DreamDust, IllusionDust}  // material reserves
Snapshot.Inventory.{LesserMagic, GreaterMagic, LesserAstral, GreaterAstral, LesserMystic, GreaterMystic, LesserNether, GreaterNether, LesserEternal, GreaterEternal}  // essence reserves
Snapshot.Inventory.{SmallGlimmering, LargeGlimmering, SmallGlowing, LargeGlowing, SmallRadiant, LargeRadiant, SmallBrilliant, LargeBrilliant}  // shard reserves
Snapshot.Inventory.NexusCrystal                 // epic-tier reagent
Snapshot.Reputation.ArgentDawn                  // Crusader recipe gate
Snapshot.Reputation.CenarionCircle              // healer chants gate
Snapshot.Reputation.TimbermawHold               // mana regen chant gate
Snapshot.Reputation.ZandalarTribe               // ZG enchants gate
Snapshot.Equipment.{Weapon, Chest, Bracer, Hands, Cloak, Boots, Shield}.Enchanted  // chant-applied per slot
Snapshot.Class                                  // determine enchant routing
Snapshot.RoleSpec                               // DPS/Heal/Tank routing
```

---

## Cross-References

- Argent Dawn rep (Crusader recipe): [../reputations/argent-dawn.md](../reputations/argent-dawn.md)
- Cenarion Circle rep: [../reputations/](../reputations/) (file pending)
- Timbermaw Hold rep: [../reputations/](../reputations/) (file pending)
- Zandalar Tribe rep: [../reputations/](../reputations/) (file pending)
- Mongoose drop in raids: [../raids/ahn-qiraj-temple.md](../raids/ahn-qiraj-temple.md), [../raids/naxxramas.md](../raids/naxxramas.md)
- Class-spec weapon enchant routing: [../classes/](../classes/) per-class file
- Disenchant input from dungeons: [../dungeons/blackrock-depths.md](../dungeons/blackrock-depths.md), [../dungeons/upper-blackrock-spire.md](../dungeons/upper-blackrock-spire.md)
- Other professions: [alchemy.md](alchemy.md), [blacksmithing.md](blacksmithing.md)
