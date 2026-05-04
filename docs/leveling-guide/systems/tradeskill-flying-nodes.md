---
title: "System — Tradeskill Flying Nodes (Mining/Herbalism Routes)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Tradeskill Flying Nodes — Mining/Herbalism Route Optimization

Mining/Herbalism nodes respawn at **fixed world locations** across zones with **5-15 minute respawn timers** per node. **Find Herbs / Find Minerals** minimap radar (trained at trainer with Apprentice skill, free) is bracket-defining tool. Per-zone density varies dramatically: **Felwood is densest for Herbalism** (Sungrass/Blindweed/Gromsblood/Golden Sansam/Dreamfoil/Mountain Silversage all spawn), **Searing Gorge** is densest for mid-game Mining (Iron/Mithril/Thorium co-exist on same map). **Black Lotus** is the most-contested rare herb (5 zones, ~25-35 server-wide nodes, 4-12 hour respawn). **Druid Travel Form** (40% land speed) doubles farming efficiency vs other classes. Multi-alt coordination enables sustained farming across respawn timers.

---

## Node Respawn Timers

| Node tier | Respawn window |
|-----------|----------------|
| **Common Mining/Herbalism nodes** | ~5-15 minutes per node |
| **Rich Thorium Vein** | ~10-15 minutes |
| **Black Lotus** | **4-12 hours** per node (most-contested) |
| **Devilsaur** (Skinning target) | ~4-6 hours per spawn point |
| **Onyxia Scale skinning** (raid drop only) | Per-raid lockout 7 days |

**Decision-engine cue:** engine should track per-node `LastHarvestTime` + estimated `NextRespawnEstimate` for sustained farming.

---

## Find Herbs / Find Minerals Minimap Radar

Trained at trainer at Apprentice skill level (free):

| Spell | Effect |
|-------|--------|
| **Find Herbs** | Display herb nodes on minimap as small green dots |
| **Find Minerals** | Display mineral veins on minimap as small green dots |
| **Find Treasure** | Treasure chests on minimap (Apprentice trainable) |

**Decision-engine rule:** **Find Herbs/Minerals always-active** for gathering characters. Engine should `Snapshot.Spells.Has("FindHerbs")` and toggle on for Herbalism characters.

---

## Mining Zone Density Map

### Mid-Game Mining (L25-50)

| Zone | Ore types | Density | Notes |
|------|-----------|---------|-------|
| **Searing Gorge** | Iron / Mithril / Thorium | **Highest L40-50** | Multi-tier zone; Thorium Brotherhood rep parallel |
| **Tanaris** (coastal) | Mithril / Truesilver (rare drop) | High | Stonescale Eel Fishing pool overlap |
| **Hinterlands** | Iron / Mithril | High | Wildhammer/Revantusk rep parallel |
| **Stranglethorn Vale** | Mithril | Medium-High | Cross-zone STV questing |
| **Badlands** | Mithril / Iron | Medium | Cross-Uldaman approach |
| **Burning Steppes** | Iron / Mithril | High | Cross-MC raid prep |
| **Un'Goro Crater** | Mithril / Iron | Medium-High | Cross-Devilsaur Leather skinning overlap |

### Endgame Mining (L50-60)

| Zone | Ore types | Density | Notes |
|------|-----------|---------|-------|
| **Winterspring** | Thorium / Rich Thorium | **Highest L55+** | Cross-Wintersaber/Timbermaw rep |
| **Silithus** | Thorium / Rich Thorium | High | Cross-Cenarion Circle + AQ40 attune |
| **Felwood** | Mithril / Thorium | High | Cross-Cenarion + Timbermaw rep |
| **Burning Steppes** | Thorium | Medium | Black Lotus zone |
| **Western/Eastern Plaguelands** | Thorium | Medium | Cross-AD rep parallel |
| **Searing Gorge** | Thorium (continued) | Medium | Cross-Thorium Brotherhood rep |

### Special Mining (Cross-Profession)

| Material | Source | Notes |
|----------|--------|-------|
| **Truesilver Ore** | Rare drop from Mithril nodes | ~5-15% drop rate from Mithril veins |
| **Dark Iron Ore** | BRD only — Dark Iron mobs | Smelt Dark Iron via Spectral Chalice quest at Black Forge |
| **Arcane Crystal** | Random drop from Thorium nodes | ~1% drop rate |
| **Black Diamond** | BRD/Strat/Scholo boss drops | Engineering Mind Control Cap reagent |

---

## Herbalism Zone Density Map

### Mid-Game Herbalism (L25-50)

| Zone | Herb types | Density | Notes |
|------|-----------|---------|-------|
| **Stranglethorn Vale** | Khadgar's Whisker / Sungrass / Blindweed | High | Cross-zone STV questing |
| **Tanaris** | Mageroyal / Khadgar's Whisker / Firebloom | Medium | Cross-Gadgetzan |
| **Searing Gorge** | Firebloom (specialty) | High | Hot zone Firebloom |
| **Hinterlands** | Khadgar's Whisker / Liferoot / Sungrass | High | Wildhammer/Revantusk |

### Endgame Herbalism (L50-60)

| Zone | Herb types | Density | Notes |
|------|-----------|---------|-------|
| **Felwood** | Sungrass + Blindweed + Gromsblood + Golden Sansam + Dreamfoil + Mountain Silversage | **Densest L60** — multi-herb spawn | Cross-Cenarion + Timbermaw rep |
| **Silithus** | Mountain Silversage + Dreamfoil | High | Cross-Cenarion Circle + AQ |
| **Winterspring** | Icecap (specialty) + Mountain Silversage | High | Cross-Wintersaber rep |
| **Western/Eastern Plaguelands** | Plaguebloom + Arthas' Tears + Mountain Silversage | High | Cross-AD rep |
| **Burning Steppes** | Firebloom (continued) | Medium | Cross-MC prep |

### Black Lotus (Contested Rare)

**The most-contested herb in 1.12** — required for raid-tier flasks (Mongoose, Titans, Wisdom, Supreme).

| Zone | Approximate spawn nodes | Notes |
|------|-------------------------|-------|
| **Burning Steppes** | ~6 nodes | Heavy contested (raid attune zone) |
| **Eastern Plaguelands** | ~5 nodes | Heavy contested |
| **Western Plaguelands** | ~5 nodes | Medium contested |
| **Silithus** | ~7 nodes | Medium contested |
| **Winterspring** | ~5 nodes | Light contested (high-level mobs) |

**Total server-wide:** ~25-35 active Black Lotus nodes at any time.

**Respawn:** 4-12 hour per node (varies by server).

**Decision-engine cue:** Black Lotus is **server-economy-defining**. Engine should:
1. Map per-server Black Lotus spawn nodes
2. Track respawn timers per node
3. Prioritize Black Lotus over other herbs when in range
4. Coordinate with raid leaders for guild-locked flasks

---

## Skinning Zone Density Map (Mid-Late Game)

| Zone | Beast types | Density | Notes |
|------|------------|---------|-------|
| **Stranglethorn Vale** | Tigers / Panthers / Raptors | **Densest mid-game** (Hemet Nesingwary) | L35-45 prime |
| **Tanaris** | Hyenas / Scorpids / Basilisks | High | L40-50 |
| **Hinterlands** | Raptors / Wolves | High | L40-45 |
| **Plaguelands (W+E)** | Plagued bears + ghouls | High | L51-58 |
| **Felwood** | Corrupted bears + deer + treants | High | L48-55 |
| **Winterspring** | Frostsabers + Yetis | **Densest L55+** | Cross-Wintersaber rep |
| **Un'Goro Crater** | **Devilsaurs** (lvl 55 elite) — **Heavy Devilsaur Leather farm** | Specialty | Multi-alt rotation |

### Heavy Devilsaur Leather (Un'Goro)

**Iconic L55+ Skinning farm** — Devilsaurs spawn at ~6-8 fixed points + roaming patrols, ~4-6 hour respawn.

| Field | Value |
|-------|-------|
| Mob type | Devilsaur (lvl 55 elite) |
| Drop | Heavy Devilsaur Leather (~80-100% per skin) + Devilsaur Eye (~5-10%) |
| AH price | 50-100g per Heavy Devilsaur Leather (server-dependent) |
| Solo capability | L60 plate/feral classes; pair-pull L58+; Hunter solo with kiting |
| Multi-alt rotation | 4-alt rotation = ~6 hours sustained farming |

**Decision-engine rule:** Devilsaur farming is **the** L55+ Skinner alt income stream. Engine should plan multi-alt rotation across 4-6 hour respawn timers.

---

## Druid Travel Form Farming Bias

Druid Travel Form (40% land speed, free, indoor-restricted) doubles farming efficiency:

| Metric | Druid (Travel Form) | Other class (no mount) |
|--------|---------------------|------------------------|
| **Inter-node travel speed** | 40% boost | 0% (walking) |
| **Inter-zone transit** | Travel Form + flight path | Flight path only |
| **Multi-zone gathering** | Faster cross-zone | Slower |
| **Total nodes/hour** | ~2x advantage at L60 | Baseline |

**Decision-engine cue:** Druid alts with Travel Form are **2x more efficient** at gathering. Engine should pre-flag Druid+Herbalism / Druid+Mining alts for gathering missions.

---

## Multi-Alt Coordination

### Sustained Farming via Multi-Alt Rotation

**4-alt rotation example** for sustained Black Lotus farming:

| Alt # | Active time | Respawn window |
|-------|-------------|----------------|
| Alt 1 | Hour 0-2 | (alt resting) |
| Alt 2 | Hour 2-4 | (alt resting) |
| Alt 3 | Hour 4-6 | (alt resting) |
| Alt 4 | Hour 6-8 | (alt resting) |
| Alt 1 | Hour 8-10 (return) | (cycle) |

**Decision-engine rule:** multi-alt coordination requires per-alt:
- `Snapshot.Position.Zone` tracking
- `Snapshot.Profession.{type}.Skill`
- `Snapshot.WallClock.LastSession`
- Cross-alt mail-bridging for inventory transfer

### Daily CD Cross-Alt Coordination

| CD | Alt # | Daily output |
|----|-------|--------------|
| Arcanite Transmute (Alchemy) | Alt 1 | 1 Arcanite Bar/day |
| Mooncloth crafting (Tailoring) | Alt 2 | 1 Mooncloth/day |
| Cured Rugged Hide (LW) | Alt 3 | 1 hide/day |
| AH master | Alt 4 | Sustained AH-flipping |

---

## Decision-Engine Rules

1. **Find Herbs/Minerals always-active**: minimap radar enabled for gathering characters.
2. **Per-zone density map**: engine should plan farming routes by bracket (Searing Gorge mid-game Mining, Felwood endgame Herbalism, Un'Goro Devilsaur skinning).
3. **Black Lotus tracking**: per-server node mapping + respawn timer tracking. Multi-alt rotation for sustained farming.
4. **Devilsaur farming**: 4-6 hour respawn per spawn point; multi-alt rotation for sustained 50-100g/hour income.
5. **Druid Travel Form bias**: 2x farming efficiency vs other classes. Engine should bias Druid alts for gathering.
6. **Multi-alt coordination**: 4-alt rotation enables sustained farming + daily CDs combined.
7. **Truesilver/Arcane Crystal RNG**: rare drops from Mithril/Thorium nodes. Engine should track per-character `Inventory.{TruesilverBar, ArcaniteCrystal}` for crafting recipe gates.
8. **Cross-rep parallel grind**: Searing Gorge (Thorium Brotherhood) + Felwood (Cenarion Circle + Timbermaw Hold) + Silithus (Cenarion Circle + Brood Nozdormu) gather rep parallel with material.
9. **Spectral Chalice quest**: Mining-Smelt Dark Iron unlock. Engine should pickup at first BRD entry.

---

## Snapshot Fields Needed

```text
Snapshot.Spells.Has("FindHerbs")                  // minimap radar
Snapshot.Spells.Has("FindMinerals")               // minimap radar
Snapshot.Profession.Mining.Skill                  // Mining tier eligibility
Snapshot.Profession.Herbalism.Skill               // Herbalism tier eligibility
Snapshot.Profession.Skinning.Skill                // Skinning tier eligibility
Snapshot.NearbyNodes.{Type, Coords}               // gathering proximity detection
Snapshot.WallClock.LastNodeHarvest                // node respawn estimation per character
Snapshot.WallClock.{BlackLotusRespawn, DevilsaurRespawn}  // per-node respawn tracking
Snapshot.Class == "Druid"                         // Travel Form farming bias
Snapshot.Spells.Has("TravelForm")                 // movement bias signal
Snapshot.Equipment.MainHand.Has("MasterMiningPick" OR "ZulianSlicer")  // skill bonus tools
Snapshot.Position.{Zone, X, Y}                    // route-planning state
Snapshot.MultiAlt.AltID                           // multi-alt coordination
Snapshot.Reputation.{ThoriumBrotherhood, CenarionCircle, TimbermawHold, BroodOfNozdormu}  // cross-rep parallel grind tracking
```

---

## Cross-References

- Mining profession (Apprentice through Master): [../professions/mining.md](../professions/mining.md)
- Herbalism profession (Apprentice through Master): [../professions/herbalism.md](../professions/herbalism.md)
- Skinning profession (Light through Devilsaur Leather): [../professions/skinning.md](../professions/skinning.md)
- Searing Gorge zone (mid-game Mining): [../zones/searing-gorge.md](../zones/searing-gorge.md)
- Felwood zone (densest L60 Herbalism): [../zones/felwood.md](../zones/felwood.md)
- Un'Goro Crater zone (Devilsaur Skinning): [../zones/un-goro-crater.md](../zones/un-goro-crater.md)
- Black Lotus zones: [../zones/burning-steppes.md](../zones/burning-steppes.md), [../zones/eastern-plaguelands.md](../zones/eastern-plaguelands.md), [../zones/western-plaguelands.md](../zones/western-plaguelands.md), [../zones/silithus.md](../zones/silithus.md), [../zones/winterspring.md](../zones/winterspring.md)
- Druid class (Travel Form farming bias): [../classes/druid.md](../classes/druid.md)
- Flight Paths (cross-zone transit): [flight-paths.md](flight-paths.md)
- Played-time economy (gathering income tier): [played-time-economy.md](played-time-economy.md)
