---
title: "Zone — Moonglade (Druid Sanctuary, All Levels)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Moonglade
crawl_date: 2026-05-01
---

# Moonglade — Druid Sacred Sanctuary (All Levels, Class-Locked)

Kalimdor north-central sacred zone — **Druid-only access** (cross-faction; Druids of both Alliance and Horde share). **No hostile mobs** — sacred ground, no combat zone. Sub-zones: **Nighthaven** (Druid hub), **Shrine of Remulos** (Druid Aquatic Form quest area), **Mystral Lake**, **Stormrage Barrow Dens** (Druid quest area). No native dungeon. **Cenarion Circle origin** (Cenarion Hold in Silithus is the extension). All Druid form quests (Bear at L10, Aquatic at L16, Cat at L20-trained, Travel at L30) originate here via Druid trainer **Teleport: Moonglade** spell.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Continent | Kalimdor (north-central) |
| Capital nearby | **Darnassus** (cross-faction Druid teleport) |
| Faction | **Druid-only access** (cross-faction; both Alliance + Horde Druids share) |
| Level range | **All levels** (Druid form quests L10-30) |
| Dungeon | None native |
| Cross-zone exits | **South** (Felwood), **West** (Winterspring FP), **Druid teleport** from any Druid trainer at any level |
| Sub-zones | Nighthaven (Druid hub), Shrine of Remulos (Aquatic Form quest area), Mystral Lake, Stormrage Barrow Dens (Druid quests) |
| Hearthstone hub | **Nighthaven Inn** (Druid-friendly; non-Druids can technically bind but rarely useful) |
| Profession trainers | None native (cross-Druid services only) |
| Notable mobs | **NO HOSTILE MOBS** — sacred ground, no combat zone |
| Notable NPCs | **Keeper Remulos** (Druid quest hub leader); **Hippogryph Master Bunthen Plainswind** (FP); **Druid trainers** for class form quests; **Eranikus pre-quest NPCs** (cross-Sunken Temple) |

---

## Sub-Zone Breakdown

### Nighthaven (Druid Hub)

**The Druid sacred hub** — central village.

| NPC | Role |
|-----|------|
| **Keeper Remulos** | Druid quest hub leader (form quest origin) |
| **Innkeeper (Nighthaven)** | Hearthstone bind |
| **Hippogryph Master / Wind Rider Master** | Flight master (FP to Everlook/Bloodvenom Post/Talonbranch Glade) |
| **Druid trainer NPCs** | Class form quest trainers |

### Shrine of Remulos (Aquatic Form Quest Area)

**Eastern shrine** — Druid Aquatic Form quest endpoint.

### Mystral Lake (Central Lake)

**Atmospheric central lake** — Druid quest setting.

### Stormrage Barrow Dens (Druid Quest Area)

**Northern dens** — Druid-themed quests + Eranikus pre-quest cross-Sunken Temple setup.

---

## Druid Form Quest Origins (All Druid Class Quests)

### Bear Form Chain (Druid L10)

| Step | Action |
|------|--------|
| 1 | Druid trainer in any city sends to Moonglade via **Teleport: Moonglade** |
| 2 | Travel to Stormrage Barrow Dens area |
| 3 | Defeat **Lunaclaw** (lvl 11 elite quest boss) |
| 4 | Return to trainer for **Bear Form** spell |

See [../classes/druid.md](../classes/druid.md) and [../sections/02-l10-l20.md](../sections/02-l10-l20.md) for full Bear Form detail.

### Aquatic Form Chain (Druid L16)

| Step | Action |
|------|--------|
| 1 | Druid trainer sends to Moonglade |
| 2 | Travel to **Shrine of Remulos** |
| 3 | Multi-step: retrieve amulet halves from underwater locations |
| 4 | Combine + return for **Aquatic Form** spell |

### Cat Form (Druid L20 — Trainer-Taught, No Quest)

In 1.12, Cat Form is **trainer-taught only** (no quest required) at any Druid trainer.

### Travel Form Chain (Druid L30)

| Step | Action |
|------|--------|
| 1 | Druid trainer sends to Moonglade |
| 2 | Multi-step Stormrage Barrow Dens chain |
| 3 | Final return for **Travel Form** spell (40% movement = pre-mount QoL) |

### Eranikus Pre-Quest (Druid L60 — Cross-Sunken Temple)

| Step | Action |
|------|--------|
| 1 | Pickup at Stormrage Barrow Dens (Druid-only) |
| 2 | Multi-zone Eranikus quest line |
| 3 | **Continues into Sunken Temple Shade of Eranikus boss fight** |

See [../dungeons/sunken-temple.md](../dungeons/sunken-temple.md) for Eranikus dungeon detail.

---

## Druid Teleport: Moonglade Spell

**The Druid-only zone access mechanism**.

| Field | Value |
|-------|-------|
| Spell | **Teleport: Moonglade** |
| Caster | Druid only (cross-faction) |
| Cost | 1 reagent (Stranglekelp herb) |
| CD | 1-hour cooldown |
| Use case | Free access to Moonglade for form quests + Druid sanctuary refuge |
| Trained at | Any Druid trainer |

**Decision-engine cue:** Druid alts have **free access to Moonglade** via Teleport: Moonglade. Engine should use this spell for quick Moonglade visits during form quest chains.

---

## Cross-Zone Transition Routes

### To Felwood (South)

| Path | Difficulty |
|------|-----------|
| South via cross-zone path | Through L48+ Felwood mobs; standard |

### To Winterspring (West — FP)

| Path | Difficulty |
|------|-----------|
| FP from Nighthaven to Everlook | Standard |

### To Darnassus / Cross-Faction Druid Teleport

| Path | Difficulty |
|------|-----------|
| Druid Teleport: Moonglade (cross-faction) | Direct (Druid-only) |

### To Bloodvenom Post / Talonbranch Glade (Felwood)

| Path | Difficulty |
|------|-----------|
| FP from Nighthaven | Standard |

---

## Decision-Engine Rules

1. **Druid-only access**: Moonglade is **Druid-class-locked** for practical access. Engine should pre-flag Druid alts for Moonglade visits.
2. **Free access via Teleport: Moonglade**: Druid Teleport: Moonglade spell provides free 1-hour-CD travel. Engine should use for form quest chains.
3. **No hostile mobs**: Moonglade is sacred ground; no combat. Engine should mark as safe-zone.
4. **Form quest origins**: all Druid form quests (Bear/Aquatic/Travel) originate here. Engine should plan visits at L10/L16/L30.
5. **Eranikus pre-quest**: Druid L60 class chain extension to Sunken Temple. Engine should pickup at L55+ if Druid alt pursuing Eranikus.
6. **Cross-faction sanctuary**: Druids of both factions share Moonglade peacefully. Engine should NOT flag PvP risk.
7. **No profession trainers**: gathering or crafting is non-existent in Moonglade. Engine should not plan profession activities.

---

## Snapshot Fields Needed

```text
Snapshot.Class == "Druid"                         // Moonglade access gate
Snapshot.Position.Zone == "Moonglade"             // current zone signal
Snapshot.Position.SubZone                         // Nighthaven/Shrine of Remulos/Mystral Lake/Stormrage Barrow Dens
Snapshot.Spells.Has("TeleportMoonglade")          // Druid free access signal
Snapshot.Spells.Has("BearForm")                   // Druid form quest progress
Snapshot.Spells.Has("AquaticForm")                // Druid form quest progress
Snapshot.Spells.Has("CatForm")                    // L20 trainer-taught
Snapshot.Spells.Has("TravelForm")                 // L30 form quest
Snapshot.QuestLog.Active.EranikusPreQuest          // Druid L60 chain
Snapshot.Inventory.Stranglekelp                   // Teleport reagent
```

---

## Cross-References

- All brackets: [../sections/](../sections/) per-bracket files
- Druid class: [../classes/druid.md](../classes/druid.md)
- Sunken Temple (Eranikus extension): [../dungeons/sunken-temple.md](../dungeons/sunken-temple.md)
- Felwood (cross-zone south): [felwood.md](felwood.md)
- Winterspring (cross-zone west FP): [winterspring.md](winterspring.md)
- Darnassus (capital — Druid teleport target): not yet covered (pending)
- Cenarion Circle rep (origin in Moonglade, extended to Cenarion Hold Silithus): [../reputations/cenarion-circle.md](../reputations/cenarion-circle.md)
- Other zones: [elwynn-forest.md](elwynn-forest.md), [dun-morogh.md](dun-morogh.md), [teldrassil.md](teldrassil.md), [durotar.md](durotar.md), [tirisfal-glades.md](tirisfal-glades.md), [mulgore.md](mulgore.md), [westfall.md](westfall.md), [loch-modan.md](loch-modan.md), [darkshore.md](darkshore.md), [the-barrens.md](the-barrens.md), [silverpine-forest.md](silverpine-forest.md), [redridge-mountains.md](redridge-mountains.md), [wetlands.md](wetlands.md), [duskwood.md](duskwood.md), [hillsbrad-foothills.md](hillsbrad-foothills.md), [ashenvale.md](ashenvale.md), [stonetalon-mountains.md](stonetalon-mountains.md), [arathi-highlands.md](arathi-highlands.md), [desolace.md](desolace.md), [thousand-needles.md](thousand-needles.md), [stranglethorn-vale.md](stranglethorn-vale.md), [dustwallow-marsh.md](dustwallow-marsh.md), [alterac-mountains.md](alterac-mountains.md), [the-hinterlands.md](the-hinterlands.md), [badlands.md](badlands.md), [searing-gorge.md](searing-gorge.md), [tanaris.md](tanaris.md), [feralas.md](feralas.md), [azshara.md](azshara.md), [un-goro-crater.md](un-goro-crater.md), [felwood.md](felwood.md), [western-plaguelands.md](western-plaguelands.md), [eastern-plaguelands.md](eastern-plaguelands.md), [burning-steppes.md](burning-steppes.md), [winterspring.md](winterspring.md), [silithus.md](silithus.md), [blasted-lands.md](blasted-lands.md), [swamp-of-sorrows.md](swamp-of-sorrows.md)
