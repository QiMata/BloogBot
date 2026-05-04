---
title: "Reputation — Steamwheedle Cartel (4 Ports)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Steamwheedle_Cartel
crawl_date: 2026-05-01
---

# Steamwheedle Cartel — 4 Goblin Ports, Engineering Recipes, Bloodsail Mutual Exclusion

Goblin faction managing 4 neutral coastal ports. **Each port has independent reputation tracking** in 1.12.1 (Booty Bay, Ratchet, Gadgetzan, Everlook). Cross-faction (both Alliance and Horde grind same way). Required for: Engineering recipes (Mithril Mechanical Dragonling at Honored, Arcanite Dragonling at Revered, Master Engineer's Goggles base at Exalted); AH discount tiers; goblin auctioneer access. **Mutually exclusive with Bloodsail Buccaneers** — killing Bloodsail mobs in STV gives Steamwheedle rep, but pursuing Bloodsail Honored (cosmetic Pirate set) makes Booty Bay Hostile.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Faction type | Neutral (both factions grind same way); **4 sub-factions per port** |
| Sub-factions | **Booty Bay** (Stranglethorn), **Ratchet** (Barrens), **Gadgetzan** (Tanaris), **Everlook** (Winterspring) |
| Quartermaster | Various per port (no single quartermaster) |
| Friendly gate | Default; basic goblin services |
| Honored gate | **5% AH discount**; Engineering recipes (Mithril Mechanical Dragonling, etc.); ZG/Booty Bay vendor unlocks |
| Revered gate | Advanced Engineering recipes (Arcanite Dragonling); auctioneer-tier items |
| Exalted gate | **Master Engineer's Goggles base recipe**; Crystal Ball of Reflection trinket; cosmetic items |
| Top accelerator | **Bloodsail Buccaneer kills** in STV (mutually exclusive with Bloodsail rep); Booty Bay quests; goblin port quests |
| Time to Honored (per port) | ~5-15 hours |
| Time to Exalted (per port) | ~25-60 hours |

---

## 4-Port Independent Reputation Map

In 1.12.1, **each goblin port has its own reputation track**. A character can be:
- Honored with Booty Bay (from STV pirate kills)
- Friendly with Gadgetzan (from default + a few Tanaris quests)
- Neutral with Everlook (from no Winterspring engagement)
- Hostile with Ratchet (from accidental goblin kill there)

**Decision-engine rule:** track per-port rep separately. Engine should map per-character rep state per port.

| Port | Continent | Best rep activities |
|------|-----------|---------------------|
| **Booty Bay** | Eastern Kingdoms (STV south) | Bloodsail Buccaneer kills, STV pirate quests, fishing tournament |
| **Ratchet** | Kalimdor (Barrens east) | Goblin trade quests, ZG path |
| **Gadgetzan** | Kalimdor (Tanaris) | Marin Noggenfogger chain, Sandfury kills, ZF entry path |
| **Everlook** | Kalimdor (Winterspring) | Witch Doctor Mau'ari Frostsaber pelt skinner, mid-game goblin quests |

---

## Reputation Tier Rewards

### Friendly (Default)

- Basic goblin services available at each port
- Auctioneer NPCs accessible (no discount yet)
- Flight masters available

### Honored (5% AH Discount + Engineering Tier 1)

| Profession | Honored recipe |
|------------|----------------|
| **Engineering** | **Mithril Mechanical Dragonling** (engineering trinket — summons lvl 50 mechanical dragon) |
| **Engineering** | Various Goblin/Gnomish recipe unlocks (cross-spec) |
| **Cooking** | Goblin Deviled Clams (already trainer-taught, but discounted) |
| **General** | **5% AH commission discount** at the port |

### Revered (Advanced Engineering)

| Profession | Revered recipe |
|------------|----------------|
| **Engineering** | **Arcanite Dragonling** (engineering trinket — summons lvl 60 mechanical dragon, raid utility) |
| **Engineering** | Mithril Spurs (Mining + Engineering crafted boots) |
| **General** | Better AH discount |

### Exalted (BiS Engineering)

| Profession | Exalted recipe |
|------------|----------------|
| **Engineering** | **Master Engineer's Goggles** base recipe (class-specific helms) |
| **Engineering** | **Crystal Ball of Reflection** trinket (caster reflection proc) |
| **Engineering** | Top-tier mechanical pet recipes |
| **General** | Cosmetic items + flavor |

**Decision-engine rule:** Honored unlocks Mithril Mechanical Dragonling (mid-tier raid pet). Revered unlocks Arcanite Dragonling (raid-tier). Exalted unlocks Master Engineer's Goggles base. Engine should plan multi-port grinds for Engineer alts.

---

## Reputation Grinding Sources

### Booty Bay (Primary — Bloodsail Buccaneer Kills)

**The Booty Bay rep grind is contentious because of mutual exclusion with Bloodsail Buccaneers.**

**Path A: Steamwheedle Path (Recommended for Engineers)**
- Kill **Bloodsail Buccaneers** in STV (lvl 35-45 elite zone)
- Each kill = ~5-25 rep with all 4 Steamwheedle ports
- **Bloodsail Cape** drops give bigger rep boost
- Reach Honored at all 4 ports simultaneously

**Path B: Bloodsail Path (Mutually Exclusive — Cosmetic Pirate Set)**
- Kill Booty Bay goblins + guards
- **Booty Bay becomes Hostile** to character
- Bloodsail Buccaneer rep climbs
- At Honored Bloodsail: **Pirate Costume Set** (cosmetic 4-piece) + ZG cosmetic gear
- **Cannot turn in Engineering recipes at Booty Bay or use AH at any Steamwheedle port**

**Decision-engine rule:** Path A (Steamwheedle) is **standard for raiders + engineers**. Path B (Bloodsail) is **completionist cosmetic** — engine should default to Path A unless user-config opts into Bloodsail.

### Ratchet (Quest-Based)

| Source | Rep gain |
|--------|----------|
| Wizzlecrank's Shredder escort + chain | ~250 rep one-time |
| Goblin trade route quests | ~50-150 rep per |
| ZG attune-related quests (Zul'Gurub access from Stranglethorn) | minor rep |

### Gadgetzan (Tanaris Quest Hub)

| Source | Rep gain |
|--------|----------|
| Marin Noggenfogger chain | ~250 rep + Noggenfogger Elixir |
| Narain Soothfancy chain | ~150 rep per quest |
| ZF (Zul'Farrak) attune chain quests | ~150 rep per |
| Sandfury Cleaver hunt | minor rep |

### Everlook (Winterspring Quest Hub)

| Source | Rep gain |
|--------|----------|
| Witch Doctor Mau'ari Frostsaber pelt skinner chain | ~150 rep |
| Goblin engineering chains | ~100 rep |
| Lorax (Highborne) chain | minor rep |
| **Wintersaber Trainers chain** quests (cross-NE-only) | minor rep |

### Cross-Port Rep (ZG Raid)

ZG (Zul'Gurub) raid clears give Steamwheedle Cartel rep across all 4 ports:

- ~500-1000 rep per full ZG clear (5-day lockout)
- Across 4 ports, this accumulates well at Honored→Revered

---

## Bloodsail Buccaneer Kill Rate

Bloodsail Buccaneer kills give **the most efficient Steamwheedle rep** for Booty Bay specifically:

| Mob | Rep gain (Steamwheedle Cartel) | Rep gain (Bloodsail) |
|-----|-------------------------------|---------------------|
| Bloodsail Swabbie / Mariner (lvl 35-40) | +5 rep | -5 rep (Bloodsail loss for those tracking it) |
| Bloodsail Sea Dog / Captain (lvl 40-45) | +10 rep | -10 rep |
| Bloodsail Magus (lvl 40-45) | +10 rep | -10 rep |
| Bloodsail Captain Iggy "Reaper" (rare) | +50 rep | -50 rep |

**Decision-engine rule:** ~250-500 Bloodsail kills = Honored. ~1000-2000 = Exalted at Booty Bay.

---

## Mithril Mechanical Dragonling (Honored Unlock)

The most-iconic Honored Engineering recipe:

| Field | Value |
|-------|-------|
| Source | Steamwheedle Cartel Honored recipe (any port) |
| Material | Mithril Bar (10) + Mithril Casing (5) + Heart of Iron + Mithril Spurs |
| Effect | Trinket; summons lvl 50 Mechanical Dragonling for 5 minutes; ~30-min CD |
| Use | PvE raid utility; 5-min duration trinket |

**Decision-engine rule:** Mithril Mechanical Dragonling is mid-tier raid utility. Engineer alts at Honored should auto-craft.

---

## Arcanite Dragonling (Revered Unlock)

| Field | Value |
|-------|-------|
| Source | Steamwheedle Cartel Revered recipe (any port) |
| Material | Arcanite Bar (1) + Mithril Mechanical Dragonling + Heart of Fire + Truesilver Bar |
| Effect | Trinket; summons lvl 60 Mechanical Dragonling for 1 minute; 30-min CD |
| Use | Raid-tier DPS pet trinket |

---

## Master Engineer's Goggles (Exalted Unlock)

| Field | Value |
|-------|-------|
| Source | Steamwheedle Cartel Exalted recipe (any port) |
| Material | Class-specific gear materials |
| Effect | **Engineering-only class-specific helm** (Plate-DPS / Mail-DPS / Cloth-Caster variants) |
| Use | BiS Engineer helm slot pre-raid |

**Decision-engine rule:** Master Engineer's Goggles base recipe is at Exalted. Engineer alts pursue ports independently or single-port at Exalted via heavy grind.

---

## Standing Threshold Map (Per Port)

| Tier | Rep needed (cumulative) | Time estimate (per port) |
|------|--------------------------|------------------------|
| Neutral → Friendly | 0 → 3000 | ~1-2 hours (default + a few quests) |
| Friendly → Honored | 3000 → 9000 | ~5-15 hours (Bloodsail kills + port quests) |
| Honored → Revered | 9000 → 21000 | ~15-30 hours per port (sustained) |
| Revered → Exalted | 21000 → 42000 | ~30-60 hours per port (long grind) |

**Decision-engine rule:** Cross-port grinding is slow (4 separate trackers). Engine should plan single-port focus (typically Booty Bay via Bloodsail) for Engineer alts pursuing Exalted goal.

---

## Bloodsail Buccaneer Mutual Exclusion

**Choosing the Bloodsail path makes Booty Bay Hostile**:

| Decision | Pros | Cons |
|----------|------|------|
| **Steamwheedle path** | Engineer recipes; AH access; raiding utility | No Pirate cosmetic set |
| **Bloodsail path** | Pirate Costume cosmetic set; ZG cosmetic | Booty Bay Hostile = no AH/recipes/quests at any Steamwheedle port |

**Decision-engine rule:** at L60 with Engineering profession, **always default to Steamwheedle path**. Bloodsail is opt-in cosmetic only.

---

## Decision-Engine Rules

1. **4-port independent tracking**: track per-port rep state separately. Engine should never assume "Steamwheedle Honored" — must check Booty Bay/Ratchet/Gadgetzan/Everlook independently.
2. **Steamwheedle vs Bloodsail decision**: default Steamwheedle for Engineer/raid alts. Bloodsail opt-in for cosmetic.
3. **Bloodsail kill grind**: Bloodsail Buccaneers in STV give all 4 ports rep simultaneously. Engine should plan 4-6 hour grind for Honored across all ports.
4. **Mithril Mechanical Dragonling priority**: Engineer at Honored should auto-craft.
5. **Arcanite Dragonling priority**: Engineer at Revered should auto-craft (raid pet trinket).
6. **Master Engineer's Goggles**: Exalted; engine should plan multi-month single-port grind.
7. **ZG raid synergy**: ZG clears give all 4 ports rep. Engine should batch ZG attendance with rep accumulation.
8. **AH discount tracking**: 5% at Honored is meaningful for high-volume traders. Engine should plan rep grind for AH-focused alts.
9. **Crystal Ball of Reflection**: Exalted caster trinket; engine should plan for Mage/Priest/Warlock/Druid alts.

---

## Snapshot Fields Needed

```text
Snapshot.Reputation.SteamwheedleCartel.BootyBay   // per-port tracking
Snapshot.Reputation.SteamwheedleCartel.Ratchet
Snapshot.Reputation.SteamwheedleCartel.Gadgetzan
Snapshot.Reputation.SteamwheedleCartel.Everlook
Snapshot.Reputation.BloodsailBuccaneers           // mutually exclusive
Snapshot.Inventory.BloodsailBuccaneer Cape Drops  // turn-in items
Snapshot.QuestLog.Active.STVPirateGrind           // primary chain
Snapshot.QuestLog.Active.GadgetzanQuests          // Tanaris hub quests
Snapshot.QuestLog.Active.EverlookQuests           // Winterspring hub
Snapshot.QuestLog.Active.RatchetQuests            // Barrens hub
Snapshot.Profession.Engineering.Skill             // recipe-eligibility
Snapshot.Profession.Engineering.Specialty         // Goblin/Gnomish path
Snapshot.Inventory.Has("MithrilMechanicalDragonling")  // Honored signal
Snapshot.Inventory.Has("ArcaniteDragonling")      // Revered signal
Snapshot.Inventory.Has("MasterEngineersGoggles")  // Exalted signal
Snapshot.Inventory.Has("CrystalBallOfReflection") // Exalted caster trinket signal
Snapshot.RaidGroup.ZG.WeeklyClearStatus           // ZG cross-rep accelerator
Snapshot.AH.Discount                              // Honored 5% / Revered higher
```

---

## Cross-References

- Engineering (Mithril/Arcanite Dragonling + Goggles): [../professions/engineering.md](../professions/engineering.md)
- Bloodsail Buccaneers (mutually exclusive): not yet covered (pending) — see also [../sections/03-l20-l30.md](../sections/03-l20-l30.md#stranglethorn-vale-lvl-30-45--top-of-bracket)
- Stranglethorn Vale (Booty Bay + Bloodsail kill zone): [../sections/03-l20-l30.md](../sections/03-l20-l30.md#stranglethorn-vale-lvl-30-45--top-of-bracket)
- Tanaris (Gadgetzan + Marin Noggenfogger): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#tanaris-lvl-40-50)
- Winterspring (Everlook): [../sections/06-l50-l60.md](../sections/06-l50-l60.md#winterspring-lvl-53-60)
- Barrens (Ratchet): [../sections/02-l10-l20.md](../sections/02-l10-l20.md#the-barrens-lvl-10-25)
- ZG raid (cross-rep): [../raids/zul-gurub.md](../raids/zul-gurub.md)
- Other reputations: [argent-dawn.md](argent-dawn.md), [hydraxian-waterlords.md](hydraxian-waterlords.md), [cenarion-circle.md](cenarion-circle.md), [timbermaw-hold.md](timbermaw-hold.md), [thorium-brotherhood.md](thorium-brotherhood.md), [brood-of-nozdormu.md](brood-of-nozdormu.md), [wintersaber-trainers.md](wintersaber-trainers.md)
