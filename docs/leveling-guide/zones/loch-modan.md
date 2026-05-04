---
title: "Zone — Loch Modan (Alliance, L10-19)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Loch_Modan
crawl_date: 2026-05-01
---

# Loch Modan — Alliance Mid-Bracket Zone (L10-19)

Eastern Kingdoms valley zone south of Dun Morogh — **Alliance L10-19 zone** designed for Dwarf/Gnome levelers. Sub-zones: **Thelsamar** (main hub), **Stonewrought Dam**, **Mo'grosh Stronghold** (ogre/trogg cave), **Ironband's Excavation Site** (archaeology), **Algaz Station** (south Wetlands border). No native dungeon. Notable mobs: **Troggs** (Mo'grosh primary), **Kobolds**, **Mo'grosh Ogres** (lvl 14-18 elite), **Dark Iron Dwarves** (lvl 18+), **Razormane Quilboar** (south border). Mining-rich (Tin/Silver primary). Cross-zone north (Dun Morogh→Ironforge), south (Wetlands).

---

## Quick Facts

| Field | Value |
|-------|-------|
| Continent | Eastern Kingdoms |
| Capital nearby | **Ironforge** (north exit road via tunnel) |
| Faction | **Alliance only** |
| Level range | **10-19** (extends to 22 for Dark Iron quests) |
| Dungeon | None native; Uldaman entrance via Badlands cross-zone |
| Cross-zone exits | **North** (Dun Morogh → Ironforge), **South** (Wetlands L20-25) |
| Sub-zones | Thelsamar (main hub), Stonewrought Dam, Mo'grosh Stronghold, Ironband's Excavation Site, Algaz Station |
| Hearthstone hub | **Thelsamar Inn** (L10-15 default bind for bracket) |
| Profession trainers | Mining (Apprentice + Journeyman, Thelsamar), Cooking (Thelsamar) |
| Notable mobs | Troggs (Mo'grosh primary), Kobolds, Mo'grosh Ogres, Dark Iron Dwarves, Razormane Quilboar (south) |
| Notable elites | **Mo'grosh Brute** (lvl 14-18 ogre); **Dark Iron Dwarven Spy** (rare) |

---

## Sub-Zone Breakdown

### Thelsamar (Main Hub)

**The Lvl 10-19 quest hub**.

| NPC | Role |
|-----|------|
| **Magistrate Bluntnose** | Town leader (atmospheric) |
| **Captain Rugelfuss** | Trogg eradication chain |
| **Chief Engineer Hinderweir VII** | Stonewrought Dam quest hub |
| **Prospector Ironband** | Excavation chain (cross-Badlands setup) |
| **Innkeeper (Thelsamar)** | Hearthstone bind |

**Quests (L10-15):**
- **Trogg Threat** — Captain Rugelfuss + Mo'grosh trogg kills
- **Stonewrought Dam** — Hinderweir's explosives + dwarven concerns
- **Iron Excavation** — Prospector Ironband chain (~12+ steps)
- **Mo'grosh Brute** — kill ogre elite for quest
- **Dark Iron Dwarven Spy** — rare elite hunt

### Stonewrought Dam

**Northwestern dam area** — Hinderweir's quest hub for explosives + trogg/Dark Iron chains.

### Mo'grosh Stronghold (Ogre + Trogg Cave)

**Northeastern cave** — primary trogg + ogre quest zone.

| Mobs | Level | Notes |
|------|-------|-------|
| Mo'grosh Trogg | 12-15 | Quest target |
| Mo'grosh Brute | 14-18 elite | Quest boss |
| Mo'grosh Mystic | 14-17 | Caster trogg |
| Mo'grosh Ogre | 16-19 | Higher-tier ogre |

**Decision-engine cue:** Mo'grosh Brute requires party-pull. Engine should encode group invariant.

### Ironband's Excavation Site (Archaeology Chain)

**East-central excavation** — Prospector Ironband multi-quest chain.

| Mobs | Level | Notes |
|------|-------|-------|
| Stonevault Trogg | 14-17 | Excavation guards |
| Stonevault Bonesnapper | 16-18 | Higher-tier |
| Various Hammertoe Grez chain mobs | 14-18 | Cross-zone Badlands prep |

### Algaz Station (South — Wetlands Gateway)

**South pass to Wetlands** — Algaz Tower + connecting bridge.

---

## Cross-Zone Transition Routes

### To Dun Morogh / Ironforge (North)

| Path | Difficulty |
|------|-----------|
| North road from Thelsamar through tunnel | Direct; safe; ~10 min walk |

### To Wetlands (L20-25)

| Path | Difficulty |
|------|-----------|
| South pass via Algaz Station | Through lvl 18-20 Dark Iron; gear-restricted at L18- |

### To Stranglethorn Vale (L30+)

| Path | Difficulty |
|------|-----------|
| Stormwind boat (after Stormwind tram) → Booty Bay | Multi-step cross-continent |

---

## Quest Lines (L10-19)

### Captain Rugelfuss Trogg Eradication (Main Chain)

| Step | Action |
|------|--------|
| 1 | Captain Rugelfuss intro at Thelsamar |
| 2 | Mo'grosh trogg kill quests (~20-30 troggs) |
| 3 | Mo'grosh Brute elite kill |
| 4 | Stonevault Trogg cleanup |

### Hinderweir / Stonewrought Dam Chain

| Step | Action |
|------|--------|
| 1 | Hinderweir intro at Stonewrought Dam |
| 2 | Explosives recovery + Dark Iron Dwarven Spy hunt |
| 3 | Cross-zone setup for Wetlands prep |

### Prospector Ironband Excavation Chain

| Step | Action |
|------|--------|
| 1 | Prospector Ironband intro at Excavation Site |
| 2 | 8-10 sub-quests at Stonevault + Hammertoe Grez area |
| 3 | **Cross-zone to Badlands** (Uldaman dungeon entry chain at L40+) — defers to higher bracket |

---

## Decision-Engine Rules

1. **Bracket entry**: Loch Modan is the **L10-19 Alliance Dwarf/Gnome default zone**. Engine should plan ~10-15 hour completion through L18.
2. **Hearthstone bind**: Thelsamar Inn is the bracket-2 default for Dwarf/Gnome alts.
3. **Mo'grosh Brute party-pull**: lvl 14-18 ogre elite. Engine should encode group invariant.
4. **Excavation Chain cross-zone**: Prospector Ironband chain extends to Badlands/Uldaman at L40+. Engine should pickup early but defer completion.
5. **Cross-zone to Wetlands**: at L18-20, head south via Algaz Station to Wetlands. Standard route.
6. **Mining rich**: Tin/Silver/Iron veins prolific in Mo'grosh + Stonewrought Dam areas. Engine should batch Mining + quests.
7. **Profession early grind**: Thelsamar sufficient for Apprentice + Journeyman (1-150). Ironforge for advanced.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // bracket gate (10-19)
Snapshot.Faction == "Alliance"                    // faction gate
Snapshot.Race IN ("Dwarf", "Gnome")               // bracket-2 default for these races
Snapshot.Position.Zone == "LochModan"             // current zone signal
Snapshot.Position.SubZone                         // Thelsamar/Stonewrought/Mo'grosh/Excavation
Snapshot.QuestLog.Active.RugelfussTroggChain      // main quest chain
Snapshot.QuestLog.Active.IronbandExcavation       // long Badlands setup chain
Snapshot.QuestLog.Completed.MoGroshBruteKill      // elite quest tracking
Snapshot.Hearthstone.BindLocation == "Thelsamar"  // bracket-2 default bind
Snapshot.Profession.Mining.Skill                  // Mining-bias grind signal
```

---

## Cross-References

- L10-L20 bracket: [../sections/02-l10-l20.md](../sections/02-l10-l20.md)
- Dun Morogh (cross-zone north): [dun-morogh.md](dun-morogh.md)
- Wetlands (cross-zone south, pending): [../sections/02-l10-l20.md](../sections/02-l10-l20.md#wetlands-lvl-20-25-mostly-post-bracket)
- Badlands (Excavation Chain extension, pending): [../sections/04-l30-l40.md](../sections/04-l30-l40.md#badlands-lvl-35-45)
- Ironforge (capital): not yet covered (pending)
- Other zones: [elwynn-forest.md](elwynn-forest.md), [dun-morogh.md](dun-morogh.md), [teldrassil.md](teldrassil.md), [durotar.md](durotar.md), [tirisfal-glades.md](tirisfal-glades.md), [mulgore.md](mulgore.md), [westfall.md](westfall.md)
