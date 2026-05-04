---
title: "System — Flight Paths (FP Network, Boats, Zeppelins)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Flight Paths — FP Network, Cross-Continent Routes, Faction-Specific Hubs

Each faction has its own **Flight Path** (FP) network across both continents. **Cross-faction neutral FPs** at goblin ports (Booty Bay, Ratchet, Gadgetzan, Everlook) + several other neutral hubs (Thorium Point, Light's Hope Chapel, Cenarion Hold, Marshal's Refuge). FP cost scales with distance: ~1-50s short hops, ~1-5g cross-continent. **Boats + zeppelins** bridge cross-continent travel: Auberdine↔Menethil (cross-faction), Org↔UC zeppelin, Org↔Grom'gol zeppelin (after L35 chain), Stormwind boats. **Alternatives**: Druid Travel Form (40% land speed free), Druid Teleport: Moonglade (free 1h-CD), Hunter Aspect of the Cheetah (30%, daze risk). Engine should plan FP discovery + cost-aware routing.

---

## Faction FP Networks (Activation Required)

### Alliance Major FPs

| Continent | Eastern Kingdoms | Kalimdor |
|-----------|------------------|----------|
| **Capitals** | Stormwind, Ironforge | Darnassus (no native FP — via Rut'theran portal) |
| **Mid-game** | Sentinel Hill (Westfall), Lakeshire (Redridge), Darkshire (Duskwood), Refuge Pointe (Arathi), Aerie Peak (Hinterlands), Chillwind Camp (WPL), Morgan's Vigil (Burning Steppes), Theramore (Dustwallow) | Auberdine (Darkshore), Astranaar (Ashenvale), Forest Song (Ashenvale), Feathermoon (Feralas via Sardor Isle hippogryph from Thalanaar), Nijel's Point (Desolace), Talonbranch Glade (Felwood), Talrendis Point (Azshara), Cenarion Hold (Silithus — neutral) |
| **Endgame** | Light's Hope Chapel (EPL — neutral), Bulwark (WPL — Horde-leaning) | Marshal's Refuge (Un'Goro — neutral), Cenarion Hold (Silithus — neutral) |

### Horde Major FPs

| Continent | Eastern Kingdoms | Kalimdor |
|-----------|------------------|----------|
| **Capitals** | Undercity | Orgrimmar, Thunder Bluff |
| **Mid-game** | Sepulcher (Silverpine), Tarren Mill (Hillsbrad), Hammerfall (Arathi), Forsaken High Command + Rear Guard (Silverpine), Stonard (Swamp of Sorrows), Flame Crest (Burning Steppes), Revantusk Village (Hinterlands), The Bulwark (WPL — Horde primary north access) | Razor Hill (Durotar), Crossroads (Barrens), Camp Taurajo (Barrens), Sun Rock Retreat (Stonetalon), Splintertree Post (Ashenvale), Hellscream's Watch (Ashenvale), Bloodvenom Post (Felwood), Shadowprey Village (Desolace), Valormok (Azshara), Camp Mojache (Feralas), Brackenwall Village (Dustwallow), Freewind Post (Thousand Needles), Marshal's Refuge (Un'Goro — neutral), Cenarion Hold (Silithus — neutral) |

---

## Neutral Cross-Faction FPs

| FP Hub | Zone | Both factions accessible |
|--------|------|-------------------------|
| **Booty Bay** | Stranglethorn Vale | Yes (goblin port) |
| **Ratchet** | The Barrens (eastern coast) | Yes (goblin port) |
| **Gadgetzan** | Tanaris | Yes (goblin port) |
| **Everlook** | Winterspring | Yes (goblin port) |
| **Thorium Point** | Searing Gorge | Yes (Thorium Brotherhood neutral) |
| **Light's Hope Chapel** | Eastern Plaguelands | Yes (Argent Dawn neutral) |
| **Cenarion Hold** | Silithus | Yes (Cenarion Circle neutral) |
| **Marshal's Refuge** | Un'Goro Crater | Yes (adventurer expedition) |
| **Nighthaven** | Moonglade | Yes (Druid sanctuary, Druid-only access) |

**Decision-engine cue:** Neutral FPs are essential for cross-faction characters (rare). For standard faction-aligned characters, neutral FPs provide **fastest cross-continent shortcut routes** (e.g., Booty Bay ↔ Ratchet).

---

## Boats + Zeppelins (Cross-Continent Bridges)

### Boats (Both Factions Can Use)

| Route | Frequency | Notes |
|-------|-----------|-------|
| **Auberdine ↔ Menethil Harbor** | ~5 min | Darkshore (KAL) ↔ Wetlands (EK) — both factions |
| **Booty Bay ↔ Ratchet** | ~5 min | Stranglethorn (EK) ↔ Barrens (KAL) — neutral goblin |
| **Theramore ↔ Menethil Harbor** | ~5 min | Dustwallow (KAL) ↔ Wetlands (EK) — Alliance-only landing |
| **Stormwind ↔ Booty Bay** | ~5 min | Stormwind harbor ↔ Booty Bay — Alliance dock |
| **Rut'theran Village ↔ Auberdine** | ~5 min | Teldrassil (KAL) ↔ Darkshore (KAL) — Night Elf access |

### Zeppelins (Horde-Specific)

| Route | Frequency | Notes |
|-------|-----------|-------|
| **Orgrimmar ↔ Undercity** | ~3 min | Direct cross-continent for Horde |
| **Orgrimmar ↔ Grom'gol Base Camp** | ~3 min | After L35 quest chain — Horde access to Stranglethorn |
| **Undercity ↔ Grom'gol Base Camp** | ~3 min | After L35 quest chain (alt route) |

### Druid Teleport: Moonglade (Druid-Only)

| Route | Cost | CD |
|-------|------|----|
| Druid trainer → Moonglade | 1 Stranglekelp herb | 1 hour |

---

## FP Cost Scaling

| Distance | Approximate cost |
|----------|------------------|
| **Short hop** (zone-local, ~5 min flight) | 1-5s |
| **Mid-range** (cross-zone, ~10-15 min flight) | 5-30s |
| **Long-range** (cross-continent, ~20-30 min flight) | 1-5g |
| **Cross-continent multi-leg** | 2-5g cumulative |

**Decision-engine rule:** FP cost is generally trivial (~1-5s per hop). Engine should always prefer FP over land travel for >2-zone cross-trips.

---

## Discovery Mechanic

Flight Paths are **discovered** by walking to the FP master and clicking the dialogue. Once activated, they appear on the map and become available for travel.

| Discovery cost | None (free) |
|----------------|--------------|
| Coverage radius | All known FPs from current activated FP |
| Auto-flight | Once route selected, flight is auto-pilot until destination |

**Decision-engine rule:** during early-bracket questing, engine should always-walk to FP masters in new zones to discover them. Build full FP network for L40+ raid prep.

---

## Class-Specific Travel Alternatives

### Druid (Travel Form + Teleport: Moonglade)

| Method | Speed | Cost | Restrictions |
|--------|-------|------|--------------|
| **Travel Form** | 40% land speed | Free | Outdoor only; cannot use in instances |
| **Teleport: Moonglade** | Direct (cast time) | 1 Stranglekelp + 1h CD | Cross-faction Druid sanctuary |

### Hunter (Aspect of the Cheetah)

| Method | Speed | Cost | Restrictions |
|--------|-------|------|--------------|
| **Aspect of the Cheetah** | 30% land speed | Free | **Daze on hit cancels** (no PvP/PvE-combat use) |

### Shaman (Ghost Wolf)

| Method | Speed | Cost | Restrictions |
|--------|-------|------|--------------|
| **Ghost Wolf** | 40% land speed | Free | Indoor-OK; usable while moving |

### All Classes (L40+ Mount + L60+ Epic Mount)

| Method | Speed | Cost |
|--------|-------|------|
| **Standard mount** L40 | 60% land speed | 80g + 20g (or free for Pal Warhorse / Wlk Felsteed) |
| **Epic mount** L60 | 100% land speed | 1000g + 100g (or free for Pal Charger / Wlk Dreadsteed) |

See [mounts.md](mounts.md) for full mount system detail.

---

## Cross-Continent Route Planning

### Alliance Routes

| Route | Method |
|-------|--------|
| Stormwind → Ironforge | Deeprun Tram (~3 min) |
| Stormwind → Darnassus | Auberdine boat (Menethil → Auberdine → portal Darnassus) OR Mage portal L20 |
| Stormwind → Booty Bay | Direct boat from Stormwind harbor (~5 min) |
| Stormwind → Theramore | Multi-step (Menethil boat → Theramore boat) |

### Horde Routes

| Route | Method |
|-------|--------|
| Orgrimmar → Undercity | Zeppelin (~3 min) |
| Orgrimmar → Thunder Bluff | Mage portal L20 OR Crossroads FP route |
| Orgrimmar → Grom'gol Base Camp | Zeppelin after L35 quest chain |
| Orgrimmar → Booty Bay | Ratchet boat OR Grom'gol zeppelin (after L35) |

### Cross-Continent (Both Factions)

| Route | Method |
|-------|--------|
| Booty Bay ↔ Ratchet | Goblin boat (~5 min) — neutral cross-continent shortcut |
| Auberdine ↔ Menethil Harbor | Cross-faction boat (~5 min) |
| Cross-EK to Kalimdor (Alliance preference) | Stormwind boat to Booty Bay → Ratchet boat OR Auberdine boat from Menethil |
| Cross-EK to Kalimdor (Horde preference) | Org → Grom'gol zeppelin OR Org → UC zeppelin → Tirisfal land route |

---

## Decision-Engine Rules

1. **FP discovery priority**: at every new zone, walk to FP master to activate. Engine should cache `Snapshot.FlightPaths.Discovered` per character.
2. **FP cost trivial**: 1-5s per hop. Engine should always prefer FP over land travel for 2+ zone trips.
3. **Cross-continent route caching**: per-faction routes (Alliance: Stormwind → Booty Bay → Ratchet vs Horde: Org → Grom'gol → Booty Bay). Engine should pre-compute optimal routes.
4. **Druid Travel Form alt**: 40% land speed free, indoor-restricted. Engine should pre-flag Druid alts for outdoor land travel preference.
5. **Hunter Aspect of the Cheetah caveat**: daze on hit cancels — only safe in non-combat zones.
6. **Shaman Ghost Wolf alt**: 40% land speed free, indoor-OK (better than Druid Travel Form for instance prep).
7. **L40 mount + L60 epic mount**: standard travel. Engine should plan mount fund accumulation pre-L40.
8. **Druid Teleport: Moonglade**: 1h-CD Druid free travel. Engine should track CD for Druid form quest chains.
9. **Boat/zeppelin scheduling**: ~3-5 min ride per route. Engine should plan boat-arrival timing for raid coordination.
10. **PvP-flagged FPs**: contested zones (Hillsbrad Tarren Mill ↔ Southshore corridor) are PvP-vulnerable on PvP servers. Engine should plan FP-hop alternatives.

---

## Snapshot Fields Needed

```text
Snapshot.FlightPaths.Discovered                   // per-character FP activation list
Snapshot.Position.Zone                            // current zone
Snapshot.Position.SubZone                         // FP master discovery proximity
Snapshot.Faction                                  // FP network selection
Snapshot.Class == "Druid"                         // Travel Form / Teleport: Moonglade alt
Snapshot.Class == "Hunter"                        // Aspect of the Cheetah alt
Snapshot.Class == "Shaman"                        // Ghost Wolf alt
Snapshot.Mounted                                  // mount status
Snapshot.Spells.Has("ApprenticeRiding")           // 60% mount unlock signal
Snapshot.Spells.Has("JourneymanRiding")           // 100% epic mount unlock signal
Snapshot.Spells.Has("TeleportMoonglade")          // Druid free-CD signal
Snapshot.Inventory.Stranglekelp                   // Druid teleport reagent
Snapshot.Gold                                     // FP cost reserve
Snapshot.WallClock.LastFPTraveled                 // FP CD tracking
Snapshot.WallClock.BoatScheduler                  // boat/zeppelin arrival prediction
```

---

## Cross-References

- All zones: [../zones/](../zones/) per-zone files
- Mounts (L40 + L60 epic): [mounts.md](mounts.md)
- Druid class (Travel Form + Teleport: Moonglade): [../classes/druid.md](../classes/druid.md)
- Hunter class (Aspect of the Cheetah): [../classes/hunter.md](../classes/hunter.md)
- Shaman class (Ghost Wolf): [../classes/shaman.md](../classes/shaman.md)
- Faction cities (capital FP hubs): [../zones/cities/](../zones/cities/)
- Sections per bracket (FP discovery routing): [../sections/](../sections/)
