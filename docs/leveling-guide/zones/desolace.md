---
title: "Zone — Desolace (Contested, L28-38)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Desolace
crawl_date: 2026-05-01
---

# Desolace — Contested L28-38 Zone (Maraudon Dungeon, Centaur Reputation)

Kalimdor barren plains zone — **Contested** with multiple hubs across factions. Sub-zones: **Shadowprey Village** (Horde Darkspear troll hub), **Nijel's Point** (Alliance hub), **Ghost Walker Post** (Tauren hub), **Cenarion Outpost** (neutral, near Maraudon), **Maraudon** dungeon (lvl 40-50 central), **Kodo Graveyard** (central landmark), **Mannoroc Coven** (demon area, NE), **Sar'theris Strand** (south coast, naga). **4 Centaur clan reputations** (Magram/Gelkis/Maraudine/Kolkar — **mutually exclusive**). Cenarion Circle rep accelerator at Cenarion Outpost. **Maraudon dungeon** is the bracket-defining 5-man (covered in [maraudon.md dungeon file](../dungeons/maraudon.md)).

---

## Quick Facts

| Field | Value |
|-------|-------|
| Continent | Kalimdor |
| Capital nearby | **Theramore** (Alliance, via boat from Menethil), **Thunder Bluff** (Horde, FP) |
| Faction | **Contested** (Alliance Nijel's Point + Horde Shadowprey Village + neutral Tauren Ghost Walker + neutral Cenarion Outpost) |
| Level range | **28-38** |
| Dungeon | **Maraudon** (lvl 40-50) at central Earth Song Falls / Wicked Grotto / Foulspore Cavern |
| Cross-zone exits | **North** (Stonetalon Mountains L16-27), **South** (Feralas L40-50), **West** (Theramore Isle boat — Alliance), **East** (Mulgore no direct exit; via Stonetalon→Mulgore) |
| Sub-zones | Shadowprey Village (H), Nijel's Point (A), Ghost Walker Post (Tauren), Cenarion Outpost (neutral), Maraudon entrance, Kodo Graveyard, Mannoroc Coven, Sar'theris Strand, 4 Centaur camps |
| Hearthstone hub | **Shadowprey Village Inn** (H) / **Nijel's Point Inn** (A) (L28-38 default bind) |
| Profession trainers | Mining/Skinning (both hubs), Cooking (Shadowprey/Nijel's), Cenarion-aligned LW patterns at Cenarion Outpost |
| Notable mobs | 4 Centaur clans (Galak, Magram, Gelkis, Maraudine, Kolkar), Kodo, Demon (Mannoroc Coven), Naga (Sar'theris Strand), Sandfury Outrunner |
| Notable elites | Centaur clan champions (rare elites); Various Mannoroc Coven elite demons; Maraudon outdoor elites (cross-dungeon) |

---

## Sub-Zone Breakdown

### Shadowprey Village (Horde Hub)

**The Lvl 28-38 Horde quest hub** — Darkspear troll coastal village.

| NPC | Role |
|-----|------|
| **Taiga Wisemane** | Quest hub leader |
| **Various Darkspear troll NPCs** | Quest givers |
| **Innkeeper (Shadowprey)** | Hearthstone bind |
| **Wind Rider Master** | Flight master (FP to Ratchet/Camp Taurajo/Thunder Bluff) |

**Quests (L28-38):**
- **Centaur clan quests** — kill various centaur (cross-faction Magram/Gelkis decision)
- **Kodo round-up** — Kodo Graveyard chain
- **Mannoroc demon hunt** — NE demon zone
- **Sar'theris Strand naga** — south coast naga kills

### Nijel's Point (Alliance Hub)

**The Lvl 28-38 Alliance quest hub** — northern mountain outpost.

| NPC | Role |
|-----|------|
| **Kreldig Ungor** | Quest hub leader |
| **Various Alliance NPCs** | Quest givers |
| **Innkeeper (Nijel's Point)** | Hearthstone bind |
| **Hippogryph Master** | Flight master (FP to Theramore/Feathermoon/Stonetalon) |

**Quests (L28-38):**
- Same theme as Shadowprey (centaur, kodo, demon, naga) but Alliance-aligned

### Ghost Walker Post (Tauren Hub)

**Tauren-aligned hub above Kodo Graveyard**.

| NPC | Role |
|-----|------|
| **Maurin Bonesplitter** | Quest hub leader |
| **Takata Steelblade** | Tauren-aligned quests |

**Quests:** Tauren-themed kodo + ancestor spirit chains.

### Cenarion Outpost (Neutral Hub — Maraudon Adjacent)

**Cenarion-aligned hub** — Maraudon dungeon support + Cenarion Circle rep.

| NPC | Role |
|-----|------|
| **Various Cenarion NPCs** | Quest hub for Maraudon-themed chains |

### Maraudon Dungeon Entrance Area (Central)

**Central canyon entrances**:
- **Wicked Grotto** (Purple wing entrance)
- **Foulspore Cavern** (Orange wing entrance)
- **Earth Song Falls** (Princess Theradras endpoint)

See [../dungeons/maraudon.md](../dungeons/maraudon.md) for full Maraudon dungeon detail.

### Kodo Graveyard (Central)

**Central landmark** — Kodo + Tauren ancestor-themed quests.

### Mannoroc Coven (Demon Area, NE)

**NE demon-corrupted area**.

| Mobs | Level | Notes |
|------|-------|-------|
| Mannoroc Lasher | 32-35 | Quest target |
| Mannoroc Felguard | 34-37 | Higher-tier |
| Burning Blade Demon | 35-38 | Cross-Burning Blade |

### Sar'theris Strand (South Coast Naga)

**South coastline** — Daggerspine variant naga + Sandfury intrusion.

### 4 Centaur Clan Camps

| Clan | Position | Faction (mutually exclusive) |
|------|----------|------------------------------|
| **Galak** | NE | Hostile to all (war target) |
| **Magram** | Center-east | Pursue (mutually exclusive with Gelkis) |
| **Gelkis** | Center-west | Pursue (mutually exclusive with Magram) |
| **Maraudine** | South + Maraudon | Hostile (cross-Maraudon) |
| **Kolkar** | South | Hostile to all |

**Decision-engine cue:** Centaur clans are **mutually exclusive**. Engine should pre-flag faction-side selection (Magram vs Gelkis) before triggering rep grind.

---

## Centaur Reputation (4 Clans, Mutually Exclusive)

The 4 Centaur clans have separate reputation tracks; **friend with one = enemy with the other**:

| Clan | Pursue path | Avoid path |
|------|-------------|------------|
| **Magram** (center-east) | Kill Gelkis to gain Magram rep | Magram-friendly path |
| **Gelkis** (center-west) | Kill Magram to gain Gelkis rep | Gelkis-friendly path |
| **Galak** | Always-hostile | None |
| **Maraudine** | Always-hostile (Maraudon-aligned) | None |
| **Kolkar** | Always-hostile | None |

**Decision-engine rule:** Centaur faction grind is **purely flavor** in 1.12.1 — no mount/recipe gates exist. Engine should default to **Magram for Horde** / **Gelkis for Alliance** unless user-config opts out.

---

## Quest Lines (L28-38)

### Centaur Clan Reputation Chain

| Step | Action |
|------|--------|
| 1 | Pickup at Shadowprey Village (H) or Nijel's Point (A) |
| 2 | Choose faction-side: Magram or Gelkis |
| 3 | Kill opposite-clan centaur for rep gains |
| 4 | Final faction-specific rewards (cosmetic only) |

### Kodo Round-up Chain

| Step | Action |
|------|--------|
| 1 | Various Tauren-aligned quests |
| 2 | Kodo Graveyard ancestor-themed kills |
| 3 | Cross-zone Maraudon prep |

### Mannoroc Demon Hunt

| Step | Action |
|------|--------|
| 1 | Northeast demon kills |
| 2 | Burning Blade demon counter-quests |

### Maraudon Dungeon Pre-Quests

| Step | Action |
|------|--------|
| 1 | Cenarion Outpost quests (Theradric Crystal Carving turn-ins) |
| 2 | Cenarion Circle rep accelerator |
| 3 | **Continues into Maraudon dungeon** (Princess Theradras endpoint) |

---

## Cross-Zone Transition Routes

### To Stonetalon Mountains (North)

| Path | Difficulty |
|------|-----------|
| North via Cenarion Outpost / Sun Rock | Standard |

### To Feralas (South — L40-50)

| Path | Difficulty |
|------|-----------|
| South road via Sar'theris Strand | Through L35-40 mobs; standard |

### To Theramore Isle (West — Alliance Only)

| Path | Difficulty |
|------|-----------|
| Boat from Theramore (cross-Wetlands → Menethil → Theramore) | Multi-step Alliance route |

### To Maraudon Dungeon (Central)

| Path | Difficulty |
|------|-----------|
| Central canyon entrances | Through L40+ Maraudine centaur; gear-restricted at L40- |

---

## Decision-Engine Rules

1. **Bracket entry**: Desolace is the **L28-38 contested zone**. Engine should plan ~10-12 hour completion through L35.
2. **Hearthstone bind**: Shadowprey Village (H) / Nijel's Point (A) Inn is the bracket-4 default.
3. **Centaur faction choice**: pre-flag Magram (H bias) or Gelkis (A bias). Engine should not trigger both simultaneously (mutually exclusive).
4. **Maraudon dungeon prep**: at L42+, plan group queue for Maraudon. Cenarion Outpost pre-quests for full reward.
5. **Cenarion Circle parallel**: Cenarion Outpost quests give CC rep. Engine should always-pickup.
6. **Mannoroc demon area**: lvl 32-38 demons; engine should party-pull for elite demon kills.
7. **Sar'theris Strand naga**: south coast naga; standard quest area at L33-37.
8. **Cross-zone to Feralas**: at L35-38, head south to Camp Mojache (H) or Feathermoon Stronghold (A) for L40-50 bracket.
9. **Profession early grind**: Shadowprey/Nijel's sufficient for Apprentice + Journeyman. Cenarion Outpost for Cenarion-aligned LW patterns.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // bracket gate (28-38)
Snapshot.Faction                                  // determines Shadowprey (H) vs Nijel's Point (A) hub
Snapshot.Position.Zone == "Desolace"              // current zone signal
Snapshot.Position.SubZone                         // Shadowprey/Nijel's/Ghost Walker/Cenarion Outpost/Maraudon entrance/Kodo Graveyard/Mannoroc/Sar'theris
Snapshot.QuestLog.Active.CentaurFactionChoice     // Magram/Gelkis exclusive decision
Snapshot.QuestLog.Active.KodoRoundUp              // Tauren ancestor chain
Snapshot.QuestLog.Active.MannorocDemonHunt        // NE demon area
Snapshot.QuestLog.Active.MaraudonPreQuests        // Cenarion Outpost pre-dungeon chain
Snapshot.Reputation.{Magram, Gelkis, Maraudine, Kolkar}  // 4-clan rep tracking (mutually exclusive)
Snapshot.Reputation.CenarionCircle                // cross-rep accelerator
Snapshot.Hearthstone.BindLocation                 // bracket-4 default bind (faction-dependent)
Snapshot.Dungeons.MaraudonPlanned                 // L42+ group prep signal
```

---

## Cross-References

- L20-L30 bracket: [../sections/03-l20-l30.md](../sections/03-l20-l30.md)
- L30-L40 bracket: [../sections/04-l30-l40.md](../sections/04-l30-l40.md)
- Stonetalon Mountains (cross-zone north): [stonetalon-mountains.md](stonetalon-mountains.md)
- Feralas (cross-zone south, pending): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#feralas-lvl-40-50)
- Mulgore (east border): [mulgore.md](mulgore.md)
- Maraudon dungeon: [../dungeons/maraudon.md](../dungeons/maraudon.md)
- Cenarion Circle rep (Cenarion Outpost): [../reputations/cenarion-circle.md](../reputations/cenarion-circle.md)
- Other zones: [elwynn-forest.md](elwynn-forest.md), [dun-morogh.md](dun-morogh.md), [teldrassil.md](teldrassil.md), [durotar.md](durotar.md), [tirisfal-glades.md](tirisfal-glades.md), [mulgore.md](mulgore.md), [westfall.md](westfall.md), [loch-modan.md](loch-modan.md), [darkshore.md](darkshore.md), [the-barrens.md](the-barrens.md), [silverpine-forest.md](silverpine-forest.md), [redridge-mountains.md](redridge-mountains.md), [wetlands.md](wetlands.md), [duskwood.md](duskwood.md), [hillsbrad-foothills.md](hillsbrad-foothills.md), [ashenvale.md](ashenvale.md), [stonetalon-mountains.md](stonetalon-mountains.md), [arathi-highlands.md](arathi-highlands.md)
