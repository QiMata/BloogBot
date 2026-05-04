---
title: "PvP — Arathi Basin (15v15 Resource Race)"
patch: "1.12.1 (Drums of War, Sept 2006); AB added in 1.7 'Rise of the Blood God' (Sept 2005)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Arathi_Basin
crawl_date: 2026-05-01
---

# Arathi Basin (AB) — 15v15 5-Node Resource Race, League of Arathor vs Defilers

15-player **Resource Race** battleground in Arathi Highlands. Control **5 nodes** (Stables/Lumber Mill/Blacksmith/Mine/Farm) for resource ticks; **first team to 2000 resources wins** (~25-30 min typical). Faction rep: **League of Arathor** (Alliance) / **Defilers** (Horde) at Hammerfall + Refuge Pointe entry hubs. Mark of Honor turn-ins for class-specific PvP gear at Honor vendors. Strategy: **3-node sustained control** wins consistently; "5-cap" all-nodes wipe is rare (60-90 sec) but high-reward.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Format | **15v15 Resource Race** |
| Win condition | First team to **2000 resources** wins (resource tick rate determined by node count) |
| Map size | Mid-large — 5 capturable nodes spread across 4 quadrants + center |
| Continent / Zone | Eastern Kingdoms — Arathi Highlands (entry NPCs at Refuge Pointe A / Hammerfall H) |
| Brackets | 20-29, 30-39, 40-49, 50-59, 60 |
| Faction rep | **League of Arathor** (Alliance) / **Defilers** (Horde) |
| Mark drop | 2 Marks per win, 1 per loss |
| Notable rewards | Class-specific PvP gear at rank tiers; AB Tabard (cosmetic, Exalted) |
| Patch added | 1.7 ("Rise of the Blood God") — September 2005 |

---

## Map Layout

| Node | Position | Strategic Value |
|------|----------|-----------------|
| **Stables** | Northeast | Closest to Alliance spawn |
| **Lumber Mill** | Northwest | High-ground; defensive advantage |
| **Blacksmith** | Center | Most contested; central buff +HP regen |
| **Gold Mine** | Southwest | Closest to Horde spawn |
| **Farm** | Southeast | Lowest-ground; flat terrain |

### Capture Mechanic

| Step | Action |
|------|--------|
| 1 | Click flag at unowned/enemy-controlled node | Begin capture cast (15s) |
| 2 | Cast must be **uninterrupted** (any damage cancels) | Position-defended |
| 3 | After 15s, flag color changes to your faction's color | Node "captured" |
| 4 | Resources tick begins for your team | Per-node tick rate |

### Resource Tick Rate

| Nodes controlled | Resources per minute |
|------------------|----------------------|
| 1 | 12 (slow) |
| 2 | 18 |
| 3 | 36 (3-node tier — sustained win) |
| 4 | 60 |
| 5 | 90+ (5-cap = rapid win) |

**Decision-engine rule:** **3 sustained nodes** is the bracket-winning strategy. Engine should plan team comp to consistently hold 3+ nodes.

---

## Game Format

### Match Setup

- 15 players per team
- Map starts neutral (no nodes captured)
- Both teams race to capture initial nodes (typically each team captures 2 closest)
- Mid-map (Blacksmith) is the contested 3rd node — typical first major fight

### Strategy Patterns

| Strategy | Description | Result |
|----------|-------------|--------|
| **3-Node Hold** | Capture 2 closest + Blacksmith + defend | Sustained victory ~25-30 min |
| **5-Cap Wipe** | Coordinate full-team push for all 5 nodes | High-difficulty; rapid win 60-90 sec |
| **2-Node Defense** | Stable 2 nodes + defend; concede other 3 | Lose by attrition (~2000 vs them at ~600) |
| **AFK Cap** | Capture 1 node and AFK | Always loses |

**Decision-engine rule:** 3-Node Hold is **the standard pattern** — engine should encode role assignments for 3-node defense rotation.

---

## Class Roles

### Node Defenders (3-5 players per node)

**Best classes for defending nodes**:

| Class | Strategy |
|-------|----------|
| **Hunter** | Frost Trap + Concussive Shot kite — best solo node-defender |
| **Druid** (Bear) | Shapeshift bash + Frenzied Regeneration — high HP defender |
| **Mage** | Polymorph + Frostbolt — hold position via CC |
| **Priest** | Power Word: Shield + heal allies; CC via Mind Soothe |
| **Warlock** | Curse of Tongues + Fear; Felhunter for caster control |

### Mid-Map / Offense

**Roles**: capture enemy nodes, kill enemy defenders, support node captures.

| Class | Strategy |
|-------|----------|
| **Rogue** | Stealth + Sap mid-map; vanish escape after capture |
| **Druid** (Cat) | Stealth + Cat Form burst; Travel Form for inter-node movement |
| **Pally / Warrior** | Frontline melee push; high HP |
| **Shaman** (Resto) | Heal team + Earth Shock interrupts on enemy defenders |

### Group Roamer

**Roles**: respond to enemy captures, rotate between contested nodes.

**Best classes**: any with high mobility (Druid Travel Form, Hunter Cheetah, Shaman Ghost Wolf).

**Decision-engine rule:** team composition matters — engine should encode **3-defender + 5-roamer + 7-flexible** distribution.

---

## Reputation Rewards

### League of Arathor (Alliance) / Defilers (Horde)

**Mutually exclusive** — Alliance grinds League, Horde grinds Defilers. Both factions have similar reward tracks.

| Tier | Reward |
|------|--------|
| Friendly | Default; basic vendor access |
| Honored | **AB-themed cloak** + mid-tier class items |
| Revered | Higher-tier class gear |
| Exalted | **AB Tabard** (cosmetic) + BiS rep-locked PvP gear |

---

## Mark of Honor Turn-Ins

**Arathi Basin Mark of Honor** drops from matches:

| Action | Marks |
|--------|-------|
| Match win | 2 Marks |
| Match loss | 1 Mark |
| Per-week Honor cap | Cross-BG total Honor |

### Rewards from AB Marks

| Item | Class | Cost (Marks) | Notes |
|------|-------|-------------|-------|
| **AB Tabard** | Cosmetic | ~5 marks (Exalted) | Faction tabard |
| **Cloak of the Five Thunders** (caster cloak) | Caster | ~10 marks + Honor | Honor vendor + AB quartermaster |
| **Mark of Resolution** (Plate-tank trinket) | Plate | ~20 marks + Honor | High-rank PvP |
| **Class-specific PvP set pieces** | Various | Honor + AB marks | Each class has unique set |

**Decision-engine rule:** AB marks accumulate alongside WSG marks. Engine should batch-spend at Honor vendor based on class gear priority.

---

## PvP Rank Progression (Cross-AB/WSG/AV)

AB matches contribute to **PvP Rank** progression alongside WSG + AV. See [honor-system.md](honor-system.md) for full ladder.

**Decision-engine rule:** AB win-rate (3-node strategy) is bracket-defining for Honor accumulation. Engine should optimize team comp.

---

## Class Roles Per Map Position

### Defending the Stables (NE Node, Alliance side)

- 2-3 defenders preferred
- Hunters with Frost Trap excel
- Mage Polymorph for CC
- Priest for sustained heal

### Defending the Lumber Mill (NW, High-Ground)

- 1-2 defenders (high-ground advantage)
- Caster-friendly (Mage / Warlock)
- Druids can Travel Form to ramp escape

### Defending the Blacksmith (Center, Most Contested)

- 4-5 defenders (most fights happen here)
- Mixed comp — tank + healer + caster + range
- Frequent flips between teams

### Defending the Mine (SW, Horde side)

- Mirror of Stables; 2-3 defenders
- Easier defensive position than Stables for Alliance

### Defending the Farm (SE)

- 2-3 defenders
- Lowest-ground = harder to hold

**Decision-engine rule:** node priority varies by team composition. Engine should rotate defenders based on map control state.

---

## Pre-Match Preparation

| Activity | Effect |
|----------|--------|
| **World buffs (Songflower, Rallying Cry)** | +Stats for entire match |
| **Consumables (Mighty Rage Potion, Major Healing)** | Burst HP/Mana availability |
| **Class-specific buffs** (Druid Mark of the Wild, Pally Blessing of Kings) | Buff entire team |
| **Pre-match positioning** | Speed buff pickup |

**Decision-engine rule:** pre-match world buff stack significantly affects match performance. Engine should run pre-buff routine at L60.

---

## Decision-Engine Rules

1. **3-Node Hold strategy**: standard bracket-winning pattern. Engine should encode 3-defender role assignments.
2. **Resource tick math**: 3 nodes = 36/min sustained; 5-cap = 90/min rapid. Engine should plan based on team strength.
3. **Mark of Honor batching**: AB marks accumulate alongside WSG. Engine should track per-character mark count + plan turn-in cadence.
4. **Class-role per node**: Hunter for outer nodes (Stables/Mine), Mage for Lumber Mill, mixed comp for Blacksmith. Engine should encode role assignments.
5. **Bracket alignment**: AB bracket per character level. Engine should queue at appropriate bracket.
6. **World buffs pre-match**: Songflower + Rallying Cry significantly boost match performance. Engine should plan pre-buff.
7. **Cross-BG honor**: AB + WSG + AV combined Honor → PvP rank progression. Engine should optimize match volume across BGs.

---

## Snapshot Fields Needed

```text
Snapshot.PvPMatch.Type == "ArathiBasin"           // BG identifier
Snapshot.Reputation.LeagueOfArathor                // Alliance faction track
Snapshot.Reputation.Defilers                       // Horde faction track
Snapshot.Inventory.ArathiMarkOfHonor               // mark stockpile
Snapshot.PvPRank                                   // current rank tier
Snapshot.PvPHonor.Weekly                           // weekly Honor cap progress
Snapshot.Class                                     // role assignment
Snapshot.WorldBuffs.Active                         // pre-match buff stack
Snapshot.PvPMatch.NodesControlled                  // 0-5 node count
Snapshot.PvPMatch.Score                            // 0-2000 resource progress
Snapshot.PvPMatch.RoleAssigned                     // Defender/Roamer/Offense
Snapshot.PvPMatch.NodeCurrent                      // current defended node
Snapshot.Position.{Map="AB", Region}               // map awareness
```

---

## Cross-References

- Honor system + PvP ranks 1-14: [honor-system.md](honor-system.md)
- Warsong Gulch (sister BG): [warsong-gulch.md](warsong-gulch.md)
- Alterac Valley: not yet covered (pending)
- Arathi Highlands zone (AB entry): [../sections/03-l20-l30.md](../sections/03-l20-l30.md#arathi-highlands-lvl-30-40)
- World buffs (pre-match prep): [../systems/world-buffs.md](../systems/world-buffs.md)
- Other PvP: [honor-system.md](honor-system.md), [warsong-gulch.md](warsong-gulch.md)
