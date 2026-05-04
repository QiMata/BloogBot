---
title: "System — Played-Time Economy (Gold/Hour, Mount Fund, Raid Costs)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Played-Time Economy — Gold/Hour Targets, Mount Fund, Raid Consumable Budget

Gold accumulation scales with character level + profession choice + AH activity. **Bracket targets**: L1-10 ~1g/hr → L40-50 ~10-20g/hr → L60 raid prep ~20-50g/hr. **Mount fund accumulation**: L40 standard mount = ~100g, **L60 epic mount = ~1100g** (1000g Journeyman Riding + 100g mount). **Raid consumable budget**: weekly Flask+Potion+Food+Buffs = **~30-50g per raid week**. **AH flipping margins**: 10-30% on profession reagents at L40+. **Profession daily CDs**: Arcanite Transmute (Alchemy) ~24h CD, ~5-10g per transmute. Engine should plan per-bracket gold reserves + sustained income strategy.

---

## Gold-Per-Hour Bracket Targets

| Bracket | Active gold/hour | Source mix |
|---------|------------------|------------|
| L1-10 | **~50s - 2g/hr** | Quest rewards + vendor sales |
| L10-20 | **~2-5g/hr** | Quest rewards + early profession crafting |
| L20-30 | **~5-10g/hr** | Quest rewards + Mining/Herbalism gathering |
| L30-40 | **~5-15g/hr** | Mount-fund stage; sustained gathering + dungeon |
| L40-50 | **~10-20g/hr** | Active gathering (Mithril/Truesilver veins, Sungrass herbs) |
| L50-60 | **~15-30g/hr** | High-tier herbs (Felwood/Silithus), Devilsaur skinning |
| L60 raid prep | **~20-50g/hr** | Black Lotus, BWL trash, AH-master active |

**Decision-engine cue:** at bracket transitions, ensure gold reserves match bracket-end goals (e.g., L40 = 100g mount fund).

---

## Mount Fund Accumulation Plan

### L40 Standard Mount (~100g target)

| Source | Approximate gain |
|--------|------------------|
| L1-30 quest rewards (cumulative) | ~30-50g |
| L30-40 Mining/Herbalism gathering | ~30-50g |
| Class quest free mount (Pal Warhorse / Wlk Felsteed) | -100g (free) |
| Faction discount (Honored = 10% off) | -5-10g |

**Decision-engine rule:** at L38, ensure `Snapshot.Gold >= 100g`. Engine should accelerate gathering at L35-39 if behind schedule.

### L60 Epic Mount (~1100g target)

| Source | Approximate gain |
|--------|------------------|
| Sustained L50-58 questing + AH | ~200-300g |
| Profession daily CDs (Arcanite Transmute) | ~50-100g per month |
| Black Lotus farming (rare drop) | ~30-100g per Black Lotus |
| Class epic free mount (Pal Charger / Wlk Dreadsteed) | -1100g (free) |
| AD rep + class quest items (no direct gold) | 0 |

**Decision-engine rule:** at L60, ensure `Snapshot.Gold >= 1100g`. Engine should plan **3-6 month accumulation** for non-Pal/Wlk classes via:
1. Sustained gathering (L55+ Felwood Black Lotus / Silithus / Winterspring)
2. Profession daily CDs + AH flipping
3. Reduce raid consumable spending (alts can supply)

---

## Income Source Detail

### Quest Rewards (Bracket-Linear)

| Bracket | Quest reward gold (cumulative) |
|---------|-------------------------------|
| L1-10 | ~5-15g |
| L10-20 | ~20-40g |
| L20-30 | ~40-80g |
| L30-40 | ~80-150g (mount fund stage) |
| L40-50 | ~150-250g |
| L50-60 | ~250-500g |

### Vendor Sales (Gray Items + Reagent Excess)

| Source | Approximate gain |
|--------|------------------|
| Per kill drop ~50c-2s gray sell | ~30s - 5s per kill (cumulative) |
| Reagent vendor sales (excess herbs, ores, leathers) | Profession-bias (10-30% AH-vendor delta) |

### Profession Crafting (BS/Tailoring/LW Crafted Gear)

| Profession | Income source |
|-----------|---------------|
| **Alchemy** | Daily Arcanite Transmute (24h CD) ~5-10g; Major Healing Pots batch crafting |
| **Blacksmithing** | Imperial Plate set (~2-5g per piece sold to AH); Lionheart Helm (~50-100g endgame) |
| **Engineering** | Goblin Sapper Charge (~5-15g per stack); Mithril Mechanical Dragonling (~5g) |
| **Tailoring** | Mooncloth Bag (~10-30g); Bloodvine set (long-tail); 16-slot bags (~5-15g) |
| **Leatherworking** | Devilsaur set (Hunter pre-raid BiS, ~30-100g per piece); Volcanic set (FR for MC) |

### Daily CDs (24h Cooldown Income)

| CD | Profession | Approximate gain |
|----|-----------|------------------|
| **Arcanite Transmute** (Thorium Bar + Arcane Crystal → Arcanite Bar) | Alchemy | 5-10g per transmute (~150-300g/month sustained) |
| **Mooncloth crafting** | Tailoring | 1 Mooncloth/day = ~5-10g per cloth (~150-300g/month) |
| **Cured Rugged Hide** | LW | Variable; cross-prof trade |

**Decision-engine rule:** daily CDs are bracket-defining for sustained income. Engine should `Snapshot.Profession.{type}.LastDailyCDExpiry` per profession.

### Open-World Farming

| Activity | Bracket | Gold/hour |
|----------|---------|-----------|
| **Mithril vein farming** (Searing Gorge/Tanaris) | L40-50 | ~10-15g/hr |
| **Truesilver vein farming** (rare Mithril drop) | L40-50 | +5-10g/hr |
| **Thorium vein farming** (Burning Steppes/Felwood/Silithus/Winterspring) | L50-60 | ~15-25g/hr |
| **Sungrass / Mountain Silversage / Dreamfoil herbing** (Felwood/Silithus) | L50-60 | ~10-20g/hr |
| **Black Lotus farming** (5-zone contested) | L60 | ~30-100g per Black Lotus (rare spawn) |
| **Devilsaur Leather skinning** (Un'Goro) | L55-60 | ~15-30g/hr |

### AH Flipping (Profession Arbitrage)

| Strategy | Margin |
|----------|--------|
| **Reagent buy-low/sell-high** (Mooncloth, Black Lotus) | 10-30% margin |
| **Crafted gear pricing** (Imperial Plate set vs Truesilver Champion) | 20-50% margin |
| **Cross-faction goblin AH arbitrage** (Booty Bay/Ratchet/Gadgetzan/Everlook) | Variable; commission 15% offset |

**Decision-engine rule:** AH flipping is **active income stream** — engine should monitor AH listings + price arbitrage at multi-alt level.

### Dungeon Runs

| Tier | Gold per clear |
|------|----------------|
| L20-30 dungeons (BFD, Stockades) | ~1-3g per clear |
| L30-40 dungeons (SM, Mara, RFD) | ~3-8g per clear |
| L40-50 dungeons (ZF, Sunken Temple) | ~5-10g per clear |
| L50-60 dungeons (BRD, Strat, Scholo, DM) | ~10-20g per clear (drops + reagents) |

### Raid Drops (Sold to AH or Guild Distributed)

| Tier | Gold per drop sold |
|------|---------------------|
| MC trash (Sulfuron Ingot, Lava Core) | ~5-50g per piece |
| BWL trash (Black Dragonscale, Elementium Ore) | ~10-100g per piece |
| AQ40 trash (Twilight Trappings) | ~5-30g per piece |
| Naxx trash (Frozen Rune, Splinter of Atiesh) | ~5-30g per piece |

---

## Money Sinks

| Sink | Cost |
|------|------|
| **Mount training L40** | ~80g (or free for Pal/Wlk) |
| **Mount training L60 epic** | ~1000g (or free for Pal Charger / Wlk Dreadsteed) |
| **Talent respec** | 1g-50g per (with decay) |
| **Spell rank training** | 1s-50g per rank |
| **Bag slot expansion** | 10s+50s+1g+25g = 26.5g cumulative |
| **Repair costs** | 5-50g per heavy raid week |
| **Mage portal reagents** | 1-2s per portal (Stranglekelp herb) |
| **Raid consumables** (Flask + Potions + Food + Buffs) | **30-50g per raid week** |
| **Resistance gear** | Variable; 10-100g per piece |
| **Profession material purchases** | Variable |

---

## Raid Consumable Budget (L60)

### Weekly Raid Stack Cost (Per Raid Week)

| Consumable | Source | Cost per raid |
|-----------|--------|---------------|
| **Flask of the Titans** (Stamina) | Alchemy 300 + Black Lotus | ~30-50g per flask (1 per raid) |
| **Mongoose Elixir** (Agility) | Alchemy + Sungrass | ~5-10g per |
| **Major Mana Potion** | Alchemy | ~1-2g per (5-10 per raid) |
| **Greater Stoneshield** (tank) | Alchemy + Stonescale Eel | ~5-10g per |
| **Smoked Desert Dumplings** (food) | Cooking 300 + Sand Worm Meat | ~3-5g per |
| **Dirge's Kickin' Chimaerok Chops** (tank food) | Cooking + Chimaerok Tenderloin | ~5-10g per |
| **Runn Tum Tuber Surprise** (caster food) | Cooking + Sungrass | ~3-5g per |
| **World buffs** (Songflower/DM Tribute/Zanza) | Time-investment + reagent | ~5-15g cumulative reagent |
| **Total weekly raid stack** | — | **~30-50g per raid week** |

**Decision-engine rule:** sustainable raid attendance requires **~150-200g/month** in consumables. Engine should track per-character consumable budget + supply chain (Alchemy alt for self-sustained flasks).

---

## Decision-Engine Rules

1. **Bracket-end gold reserve checks**: ensure `Snapshot.Gold >= bracket_end_target` (L20=10g, L30=30g, L40=100g, L50=300g, L60=1100g).
2. **Mount fund priority**: at L35-39, accelerate gathering for L40 mount. At L55-59, plan 3-6 month L60 epic mount fund.
3. **Daily CD income**: Arcanite Transmute + Mooncloth daily = ~150-300g/month sustained. Engine should track per-profession CD expiry.
4. **AH flipping monitoring**: at L40+, engine should monitor AH listings for arbitrage opportunities.
5. **Raid consumable budget**: ~150-200g/month for L60 raiders. Engine should plan supply chain via Alchemy alt.
6. **Vendor-AH delta**: gray items always to vendor; greens/blues to AH (or disenchant if Enchanting). Decision tree per item-quality.
7. **Open-world farming routes**: per-bracket optimal zones (Searing Gorge L40-50, Felwood/Silithus/Winterspring L55-60).
8. **Class mount priority**: Paladin/Warlock should pursue free class mount instead of paying 100g/1100g.
9. **Profession daily CDs**: Arcanite + Mooncloth = bracket-defining sustained income. Engine should auto-trigger at expiry.
10. **Played-time decay**: respec cost decays over played time. Engine should leverage decay between major content shifts.

---

## Snapshot Fields Needed

```text
Snapshot.Gold                                     // current gold
Snapshot.Bracket.Current                          // bracket signal
Snapshot.Mounted                                  // mount status
Snapshot.Spells.Has("ApprenticeRiding")           // L40 mount unlock signal
Snapshot.Spells.Has("JourneymanRiding")           // L60 epic mount unlock signal
Snapshot.Spells.Has("SummonCharger" OR "SummonDreadsteed")  // class mount free unlock
Snapshot.Profession.Alchemy.LastDailyTransmute    // 24h CD tracking
Snapshot.Profession.Tailoring.LastMooncothCD      // 24h CD tracking
Snapshot.Inventory.{ArcaniteBar, Mooncloth}        // daily CD output
Snapshot.AH.ActiveListings                        // AH flipping state
Snapshot.AH.GoldEarnedThisWeek                    // weekly AH revenue
Snapshot.RaidGroup.Active                         // raid attendance signal
Snapshot.Inventory.{FlaskOfTheTitans, MajorManaPotion, etc.}  // consumable stockpile
Snapshot.WallClock.PlayedTime                     // for sustained-income tracking
Snapshot.Profession.Mining.Skill                  // gathering income capacity
Snapshot.Profession.Herbalism.Skill               // gathering income capacity
Snapshot.Profession.Skinning.Skill                // gathering income capacity
```

---

## Cross-References

- Mount system (cost detail): [mounts.md](mounts.md)
- Talent respec (cost progression): [dual-talent-and-respec.md](dual-talent-and-respec.md)
- Mail/AH/Bank (AH listing strategy): [mail-auction-bank.md](mail-auction-bank.md)
- Faction city services (bag slot expansion): [faction-city-services.md](faction-city-services.md)
- Resistance gear (raid prep gear costs): [resistance-gear.md](resistance-gear.md)
- World buffs + Consumables (raid stack): [world-buffs.md](world-buffs.md), [consumables.md](consumables.md)
- All professions (income sources): [../professions/](../professions/)
- Steamwheedle Cartel rep (5% AH discount): [../reputations/steamwheedle-cartel.md](../reputations/steamwheedle-cartel.md)
