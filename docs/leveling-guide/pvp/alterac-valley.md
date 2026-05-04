---
title: "PvP — Alterac Valley (40v40 BG)"
patch: "1.12.1 (Drums of War, Sept 2006); AV added in 1.5 'Battlegrounds' (May 2005); 1.12 patch significantly reduced match length"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Alterac_Valley
crawl_date: 2026-05-01
---

# Alterac Valley (AV) — 40v40 BG, Drek'Thar vs Vanndar, Tower-Capture Resource Race

The largest battleground — **40v40 players** in a multi-objective tower-capture format. Originally **multi-day marathon battles** (5-7 days each) before patch 1.12 nerfs. **In 1.12.1 matches typically last 30-60 minutes**, ending when one team kills the enemy General (**Drek'Thar** Horde / **Vanndar Stormpike** Alliance) OR reaches 0 reinforcements. Faction rep: **Stormpike Guard** (Alliance) / **Frostwolf Clan** (Horde) at hubs in Alterac Valley itself. **Highest-Honor-per-match BG** — pre-1.12 rank-14 grinders ran AV exclusively. Required for AV-themed PvP gear: Stormpike/Frostwolf class sets, summoned bosses (Lokholar/Ivus).

---

## Quick Facts

| Field | Value |
|-------|-------|
| Format | **40v40** large-scale BG |
| Win condition | (1) **Defeat enemy General** (Drek'Thar / Vanndar Stormpike) OR (2) reduce enemy reinforcements to 0; whichever first |
| Match length | ~30-60 minutes (post-1.12 nerf; previously 5-7 days) |
| Continent / Zone | Eastern Kingdoms — Alterac Mountains (separate instance) |
| Brackets | 51-60 (60 dominant) |
| Faction rep | **Stormpike Guard** (Alliance) / **Frostwolf Clan** (Horde) |
| Mark drop | 2 Marks per win, 1 per loss + bonus on tower captures |
| Notable rewards | **Lokholar / Ivus summon** (Horde / Alliance), AV-themed gear, Captain Galvangar / Stonehearth turn-in trinkets |
| 1.12 changes | Match length significantly reduced; some NPCs removed; reinforcement system tightened |
| Patch added | 1.5 ("Battlegrounds") — May 2005 |

---

## Map Layout

### Alliance Side (Stormpike Aid Station)

- **Stormpike Aid Station** (Alliance base): contains General Vanndar Stormpike (final boss)
- **Aid Station Quartermaster**: Captain Balinda Stonehearth
- **Towers**: Stonehearth Outpost, Stonehearth Bunker, Iceblood Tower (`[verify pass 3]` — exact tower-name allocation)

### Horde Side (Frostwolf Keep)

- **Frostwolf Keep** (Horde base): contains General Drek'Thar (final boss)
- **Frostwolf Quartermaster**: Captain Galvangar (mid-boss in some configurations)
- **Towers**: Tower Point, North & South Tower Point, Frostwolf Tower

### Mid-Map

- **Stoutmantle Tower** + **Galvangar Camp** (varies by side)
- **Multiple graveyards** (Spirit Guide respawn points; controlled by tower flips)
- **Quest hubs** for additional rep + objective items

---

## Game Format

### Reinforcement System

| Side | Starting reinforcements | Decrement |
|------|------------------------|-----------|
| Alliance | ~600 reinforcements | -1 per ally death |
| Horde | ~600 reinforcements | -1 per ally death |
| Match end | When one side hits 0 reinforcements OR enemy General killed | First-to-trigger wins |

### Tower Capture Mechanic

| Step | Action | Effect |
|------|--------|--------|
| 1 | Click flag at tower | Begin 4-minute capture cast |
| 2 | Cast must be **uninterrupted** | Stay near flag |
| 3 | After 4 minutes, tower changes to your faction | Captured |
| 4 | Captured tower grants **bonus reinforcements** + **Spirit Guide spawn** | Resource boost |
| 5 | Tower can be destroyed (siege fire) | Permanent loss |

### Boss Kills (Optional Path to Victory)

- **General Vanndar Stormpike** (Alliance final): high HP, requires 10+ raid burst
- **General Drek'Thar** (Horde final): high HP, requires 10+ raid burst
- **Captain Balinda Stonehearth** (Alliance mid): kill grants +Reinforcements
- **Captain Galvangar** (Horde mid): kill grants +Reinforcements
- **Korrak the Bloodrager** (rare elite event boss): high-honor kill

---

## Strategy Patterns

### Pre-1.12 vs Post-1.12 Strategy

| Era | Strategy | Match length |
|-----|----------|---------------|
| Pre-1.12 | Multi-day defense + reinforcement marathon | 5-7 days |
| Post-1.12 (1.12.1) | **Rush to enemy General** for fast win OR sustained tower captures | 30-60 min |

### Standard Post-1.12 Strategy

| Phase | Description |
|-------|-------------|
| Phase 1 | Both teams race to enemy General; first to kill General wins (typically ~25-40 min) |
| Phase 2 | Tower captures along route to General; bonus Reinforcements |
| Phase 3 | Final General fight with raid burst |

**Decision-engine rule:** post-1.12 strategy = **rush to General**. Engine should plan team comp to kill General fast (10+ raid burst) rather than sustain tower captures.

---

## Class Roles

### Tank Brothers (Front-Line)

| Class | Strategy |
|-------|----------|
| **Warrior** (Prot) | Tank General + add control; high HP for sustained engagements |
| **Paladin** (Prot Alliance) | Aura buffs + high HP; counter Drek'Thar tank-fight |
| **Druid** (Bear, all factions) | Off-tank; Bear shapeshift for HP burst |

### Burst DPS (General Kill)

| Class | Strategy |
|-------|----------|
| **Mage** (Frost) | Frost Bolt + Frost Nova for General CC + burst |
| **Warlock** (Affliction) | Rain of Fire + Hellfire AoE |
| **Hunter** | Aimed Shot + ranged control during General fight |
| **Rogue** | Stealth approach + Ambush opener; vanish escape |

### Healers

| Class | Strategy |
|-------|----------|
| **Priest** | Sustained heal + dispel; Power Word: Shield burst |
| **Druid** (Resto) | Lifebloom HoTs + Tranquility 30s emergency |
| **Paladin** (Holy Alliance) | Holy Light spam + Beacon of Light |

**Decision-engine rule:** AV team comp = ~10 burst-DPS + ~5 tanks + ~10 healers + ~15 mid-map roamers. Engine should encode role distribution.

---

## Reputation Rewards

### Stormpike Guard (Alliance) / Frostwolf Clan (Horde)

**Mutually exclusive** — Alliance grinds Stormpike, Horde grinds Frostwolf. Both factions have similar reward tracks.

| Tier | Reward |
|------|--------|
| Friendly | Default; basic vendor access in Alterac Valley NPCs |
| Honored | **AV-themed cloak** + mid-tier class items |
| Revered | Higher-tier class gear; Ivus / Lokholar summoning recipe |
| Exalted | **AV Tabard** (cosmetic) + BiS rep-locked PvP gear; Crystal Cluster / Sapling Branch turn-ins |

---

## Lokholar / Ivus Summoning (Special Mechanic)

In AV, special raid-tier "lieutenants" can be summoned by collecting reagents:

### Alliance: Ivus the Forest Lord

| Step | Action |
|------|--------|
| 1 | Collect **5 Sapling Branch** drops (in AV instance) |
| 2 | Turn in to Stormpike Quartermaster |
| 3 | **Ivus the Forest Lord** spawns (lvl 60 elite NPC ally) |
| 4 | Ivus assists in attacking Frostwolf Keep |

### Horde: Lokholar the Ice Lord

| Step | Action |
|------|--------|
| 1 | Collect **5 Crystal Cluster** drops (in AV instance) |
| 2 | Turn in to Frostwolf Quartermaster |
| 3 | **Lokholar the Ice Lord** spawns (lvl 60 elite NPC ally) |
| 4 | Lokholar assists in attacking Stormpike Aid Station |

**Decision-engine rule:** Lokholar / Ivus accelerate General kills significantly. Engine should plan reagent farming alongside tower capture strategy.

---

## Mark of Honor Turn-Ins

**Alterac Valley Mark of Honor** drops from matches:

| Action | Marks |
|--------|-------|
| Match win | 2 Marks + bonus per tower captured |
| Match loss | 1 Mark |
| Bonus tower capture | +1 Mark per tower destroyed during match |

### Rewards from AV Marks

| Item | Class | Cost (Marks) | Notes |
|------|-------|-------------|-------|
| **AV Tabard** | Cosmetic | ~5 marks (Exalted) | Faction tabard |
| **Captain's Charge** (caster trinket) | Caster | ~30 marks + Honor | High-rank PvP |
| **Class-specific PvP set pieces** | Various | Honor + AV marks | Each class has unique set |
| **AV-themed weapons** | Various | High-rank Honor | Bracket-defining PvP weapons |

**Decision-engine rule:** AV marks accumulate quickly via long matches. Engine should batch-spend with WSG + AB marks at Honor vendor.

---

## PvP Rank Progression (Cross-AB/WSG/AV)

AV matches contribute the **highest Honor per match** of all BGs (longest duration → more HKs). Pre-1.12, rank-14 grinders ran AV exclusively. Post-1.12, AB + WSG + AV mix is competitive.

**Honor estimate per AV match (1.12.1):**
- Win: ~1500-3000 Honor (high HK count + reinforcement bonus)
- Loss: ~500-1000 Honor
- Average per hour: ~3000-5000 Honor

See [honor-system.md](honor-system.md) for full PvP rank ladder.

**Decision-engine rule:** AV is **the highest-Honor BG**. Engine should plan AV queue priority for rank-14 grinders.

---

## 1.12 Patch Changes

Patch 1.12 made significant AV changes:

| Change | Impact |
|--------|--------|
| Match length reduced from multi-day to 30-60 min | Faster turnover |
| Some NPCs removed (Captain Stenarus, etc.) | Simplified gameplay |
| Reinforcement system tightened | Death penalty more impactful |
| Tower capture timing standardized | 4-min capture cast |
| General fight mechanics adjusted | Easier to rush vs original |

**Decision-engine rule:** post-1.12.1 strategy is **rush-General-focused**. Engine should encode time-budget per phase.

---

## Decision-Engine Rules

1. **Match length expectation**: 30-60 min in 1.12.1 (down from multi-day pre-1.12). Engine should plan AV queue accordingly.
2. **Strategy: Rush General**: standard post-1.12 winning pattern. Engine should encode team comp for General burst.
3. **Tower capture priority**: 4-minute capture cast; bonus Reinforcements + Spirit Guide spawn. Engine should distribute team between General-rush vs tower defense.
4. **Lokholar / Ivus summoning**: 5-reagent turn-in for raid-tier ally NPC. Engine should plan reagent farming.
5. **Mark of Honor batching**: AV marks accumulate quickly via long matches. Engine should track per-character mark count + plan turn-in cadence.
6. **Highest Honor BG**: ~1500-3000 Honor per win. Engine should prioritize AV queue for rank-14 grinders.
7. **Pre-match world buffs**: Songflower + Rallying Cry significantly affect match performance at L60. Engine should pre-buff.
8. **Class composition**: 10 burst-DPS + 5 tanks + 10 healers + 15 roamers. Engine should encode role distribution.
9. **Cross-BG queue strategy**: AV + AB + WSG mix; AV gives most Honor per match but takes longer. Engine should optimize for rank goals.

---

## Snapshot Fields Needed

```text
Snapshot.PvPMatch.Type == "AlteracValley"          // BG identifier
Snapshot.Reputation.StormpikeGuard                 // Alliance faction track
Snapshot.Reputation.FrostwolfClan                  // Horde faction track
Snapshot.Inventory.AlteracMarkOfHonor              // mark stockpile
Snapshot.PvPRank                                   // current rank tier
Snapshot.PvPHonor.Weekly                           // weekly Honor cap progress
Snapshot.Class                                     // role assignment
Snapshot.WorldBuffs.Active                         // pre-match buff stack
Snapshot.PvPMatch.ReinforcementsAlliance           // 0-600
Snapshot.PvPMatch.ReinforcementsHorde              // 0-600
Snapshot.PvPMatch.TowersControlled                 // tower count by side
Snapshot.PvPMatch.GeneralAlive                     // General Vanndar / Drek'Thar status
Snapshot.PvPMatch.LokholarSummoned                 // Horde reagent state
Snapshot.PvPMatch.IvusSummoned                     // Alliance reagent state
Snapshot.Inventory.{SaplingBranch, CrystalCluster}  // summon reagent stockpile
Snapshot.PvPMatch.RoleAssigned                     // Tank/DPS/Healer/Roamer
Snapshot.WallClock.QueueWaitTime                   // queue length
```

---

## Cross-References

- Honor system + PvP ranks 1-14: [honor-system.md](honor-system.md)
- Warsong Gulch (sister BG): [warsong-gulch.md](warsong-gulch.md)
- Arathi Basin (sister BG): [arathi-basin.md](arathi-basin.md)
- Alterac Mountains zone (AV entry — open-world only, BG instance separate): [../sections/04-l30-l40.md](../sections/04-l30-l40.md#alterac-mountains-lvl-30-38)
- World buffs (pre-match prep): [../systems/world-buffs.md](../systems/world-buffs.md)
- Other PvP: [honor-system.md](honor-system.md), [warsong-gulch.md](warsong-gulch.md), [arathi-basin.md](arathi-basin.md)
