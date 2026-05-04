---
title: "PvP — Warsong Gulch (10v10 CTF)"
patch: "1.12.1 (Drums of War, Sept 2006); WSG added in 1.5 'Battlegrounds' (May 2005)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Warsong_Gulch
crawl_date: 2026-05-01
---

# Warsong Gulch (WSG) — 10v10 CTF, Silverwing Sentinels vs Warsong Outriders

10-player Capture the Flag battleground in Ashenvale. **First team to 3 captures wins** (typically 25-30 minutes). Faction reputation: **Silverwing Sentinels** (Alliance) / **Warsong Outriders** (Horde) at Yojamba-style hubs in Ashenvale + Stranglethorn. Mark of Honor turn-ins for class-specific PvP gear at Honor vendors. **Druid Flag Carrier** (Travel Form + Innervate + Bear shapeshift) is the meta FC for both factions; alternative carriers: Warrior with Cyclone item, Hunter with Aspect of the Cheetah, Warlock with Healthstone + Soul Link.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Format | **10v10 Capture the Flag** |
| Win condition | First team to **3 flag captures** (no time limit, but typically 25-30 min matches) `[verify pass 3]` |
| Map size | Mid-sized — 2 base zones + central tunnel/graveyard area |
| Continent / Zone | Ashenvale (instance entry NPCs at Silverwing Grove A / Warsong Lumber Camp H) |
| Brackets | 10-19, 20-29, 30-39, 40-49, 50-59, 60 |
| Faction rep | **Silverwing Sentinels** (Alliance) / **Warsong Outriders** (Horde) |
| Mark drop | 2 Marks per win, 1 Mark per loss |
| Notable rewards | Class-specific PvP gear at rank tiers; **Sergeant's Cloak / Knight's** etc.; **Mark of Sanctification** caster trinket |
| Patch added | 1.5 ("Battlegrounds") — May 2005 |

---

## Map Layout

### Alliance Side (North — Silverwing Hold)

- **Silverwing Hold flag room** (north): contains Alliance flag
- **Sentinel Hill access ramp** (entrance from Warsong Lumber Camp side, contested)

### Horde Side (South — Warsong Lumber Mill)

- **Warsong Lumber Mill flag room** (south): contains Horde flag
- **Lumber Camp access ramp** (entrance from Silverwing side, contested)

### Mid-Map

- **Tunnel** (central — runs N/S beneath the map): primary path; +50% speed buff inside
- **Graveyard** (central — surface above Tunnel): contested zone; respawn point for both teams when defeated mid-map
- **Speed buff** (+50% movement, 10s duration): spawns on either side of mid-map ramps
- **Restoration buff** (+10% health regen, 10s duration): spawns near base

**Decision-engine rule:** map awareness invariant — engine should encode tunnel vs ramp routing per character class (Druid Travel Form prefers tunnel; Warrior prefers ramps).

---

## Game Format

### Match Setup

- 10 players per team (mixed level brackets within bracket)
- Both teams spawn at base flag room
- 1 Alliance flag in Silverwing Hold; 1 Horde flag in Warsong Lumber Mill
- Win = capture 3 enemy flags (return to own flag room with enemy flag while own flag is at home)

### Rules

- **Pickup**: Tap enemy flag = pick it up (carrier becomes "flag carrier")
- **Capture**: Carrier returns to own flag room (own flag must be home for capture to count)
- **Drop on death**: Flag carrier death = flag drops; can be picked up again or returned to base
- **Aura on carrier**: Flag carrier gets a debuff (visible to all) marking their location

### Match Flow Example

| Phase | Description | Duration |
|-------|-------------|----------|
| Phase 1 | Both teams scout + secure mid-map; first FC pickup | ~5 min |
| Phase 2 | Each team's FC defends + attempts capture; 1-2 captures occur | ~10-15 min |
| Phase 3 | Final push to 3 captures; defense vs offense rotation | ~5-10 min |
| Win | First team to 3 captures wins | Match end |

---

## Class Roles

### Flag Carrier (FC)

**Best classes for FC** (high HP/survivability + mobility):

| Class | Strategy | Notes |
|-------|----------|-------|
| **Druid** (any spec) | **Travel Form (40% speed)** + Bear shapeshift for HP + Innervate + Mark of the Wild | **Meta FC** — bracket-defining |
| **Warrior** (Prot/Arms) | High HP + Berserker Stance + Spell Reflection | Mid-tier; needs party support |
| **Hunter** | Aspect of the Cheetah (30% speed); Wing Clip kite | Pre-mount alt; less ideal post-L40 mount era |
| **Warlock** | Soul Link + Felhunter; Healthstone burst heal | Specialized; high kill-priority |

**Decision-engine rule:** Druid is **meta FC**. Engine should pre-flag Druid alts for WSG queue.

### Defender (Flag Room)

**Best classes for defending flag room**:

| Class | Strategy |
|-------|----------|
| **Hunter** | Frost Trap + Concussive Shot kite |
| **Mage** | Polymorph CC + Frostbolt burst |
| **Priest** | Heal flag carrier + AoE Mind Control |
| **Warlock** | Curse of Tongues + Fear |

### Mid-Map / Offense

**Roles**: pick up enemy flag, kill enemy flag carrier, escort own carrier.

| Class | Strategy |
|-------|----------|
| **Rogue** | Vanish through tunnel; ambush FC with Cheap Shot + Ambush |
| **Mage** | Frost Nova + Polymorph for FC slow + CC |
| **Druid** | Cat Form stealth + Bear-form FC defense alt |
| **Pally / Warrior** | Frontline defense; high HP |

**Decision-engine rule:** team composition matters more than individual class. Engine should encode role assignment based on raid roster.

---

## Reputation Rewards

### Silverwing Sentinels (Alliance) / Warsong Outriders (Horde)

**Mutually exclusive** — Alliance grinds Silverwing, Horde grinds Warsong. Both factions have similar reward tracks.

| Tier | Reward |
|------|--------|
| Friendly | Default; basic vendor access |
| Honored | **WSG-themed cloak** + mid-tier class items |
| Revered | Higher-tier class gear (cloth caster legs, plate-DPS bracers, etc.) |
| Exalted | **WSG Tabard** (cosmetic) + BiS rep-locked PvP gear |

**Decision-engine rule:** WSG rep grind is **bracket-aligned** — engine should push to Honored at L60 for class-specific PvP gear.

---

## Mark of Honor Turn-Ins

**Warsong Gulch Mark of Honor** drops from matches:

| Action | Marks |
|--------|-------|
| Match win | 2 Marks |
| Match loss | 1 Mark |
| Per-week Honor cap | Determined by total Honor across all BGs |

### Rewards from WSG Marks

| Item | Class | Cost (Marks) | Notes |
|------|-------|-------------|-------|
| **Cloak of the Five Thunders** (caster cloak) | Caster | ~10 marks + Honor | Honor vendor + WSG quartermaster |
| **Mark of Sanctification** (caster trinket) | Caster | ~20 marks + Honor | Healer-tier trinket |
| **WSG Tabard** | Cosmetic | ~5 marks (Friendly+) | Faction tabard |
| **Sergeant's Cloak** (mid-rank PvP) | All | Honor + rank | Lieutenant Commander rank |
| **Knight's Robe** (high-rank PvP) | All | Honor + rank | Knight-Captain rank |
| **Class-specific PvP set pieces** | Various | Honor + WSG marks | Each class has unique set |

**Decision-engine rule:** Marks accumulate from BG matches. Engine should batch-spend at Honor vendor based on highest-priority class gear.

---

## PvP Rank Progression (Cross-WSG/AB/AV)

WSG matches contribute to **PvP Rank** progression alongside Arathi Basin (AB) + Alterac Valley (AV). Rank rewards are bracket-defining at L60:

| Rank tier | Honor required (weekly) | WSG match count estimate |
|-----------|-------------------------|--------------------------|
| Sergeant (rank 4) | Mid-tier | ~50 matches per week |
| Lieutenant Commander (rank 7) | High-tier | ~150 matches per week |
| Knight-Captain (rank 9) | Top-tier | ~200+ matches per week |
| Marshal / Field Marshal (rank 11+) | Sustained Top-tier | Multi-week sustained |
| Grand Marshal / High Warlord (rank 14) | Maximum Honor | 12-14 week sustained 60-80h/week |

See [honor-system.md](honor-system.md) for full PvP rank ladder.

---

## Optimal Queue Strategy

| Time of day | Queue length |
|-------------|--------------|
| Off-peak (2-6 AM server) | 2-5 minute queue (faster) |
| Peak (7-11 PM server) | 10-30 minute queue |
| Weekend events (WSG holiday) | 5-15 minute queue (highest reward weeks) |
| Bracket-specific (10-19/20-29/etc) | Generally faster than 60-bracket |

**Decision-engine rule:** WSG queue length varies. Engine should track queue waiting time and balance with other activities (questing, rep grinding).

---

## Decision-Engine Rules

1. **FC class priority**: Druid (Travel Form) is meta. Engine should pre-flag Druid alts for FC role.
2. **Bracket alignment**: WSG bracket is per-level (10-19, 20-29, etc.). Engine should queue at appropriate bracket for active character.
3. **Mark of Honor batching**: WSG marks drop per match; engine should track per-character mark count + plan turn-in cadence.
4. **Honor system integration**: cross-BG honor accumulation affects rank progression. Engine should plan WSG + AB + AV mix for rank goals.
5. **Match outcome strategy**: 2 marks for win vs 1 for loss = win-rate matters. Engine should optimize team comp for wins (high FC class + strong defender).
6. **Map awareness**: tunnel vs ramp routing per class. Engine should encode pathing preferences.
7. **Speed buff priority**: mid-map +50% movement buff is contested. Engine should prioritize for FC role.
8. **Pre-match preparation**: world buffs (Songflower, Rallying Cry) significantly affect WSG performance. Engine should pre-buff at L60.
9. **Class-specific PvP rewards**: each class has WSG set pieces. Engine should plan rep + mark grind per character class.

---

## Snapshot Fields Needed

```text
Snapshot.PvPMatch.Type == "WarsongGulch"          // BG identifier
Snapshot.Reputation.SilverwingSentinels           // Alliance faction track
Snapshot.Reputation.WarsongOutriders              // Horde faction track
Snapshot.Inventory.WarsongMarkOfHonor              // mark stockpile
Snapshot.PvPRank                                  // current rank tier
Snapshot.PvPHonor.Weekly                          // weekly Honor cap progress
Snapshot.Class                                    // FC class priority
Snapshot.Spells.Has("TravelForm")                 // Druid FC bias
Snapshot.WorldBuffs.Active                        // pre-match buff stack
Snapshot.PvPMatch.Score                           // current match capture count
Snapshot.PvPMatch.OwnFlagState                    // home/captured by enemy
Snapshot.PvPMatch.EnemyFlagState                  // home/picked up by us
Snapshot.PvPMatch.RoleAssigned                    // FC/Defender/Offense
Snapshot.Position.{Map="WSG", Region}             // map awareness
Snapshot.WallClock.QueueWaitTime                  // queue length
```

---

## Cross-References

- Honor system + PvP ranks 1-14: [honor-system.md](honor-system.md)
- Arathi Basin: not yet covered (pending)
- Alterac Valley: not yet covered (pending)
- Druid (FC class meta): [../classes/druid.md](../classes/druid.md)
- Ashenvale zone (WSG entry): [../sections/02-l10-l20.md](../sections/02-l10-l20.md#ashenvale-lvl-18-30)
- World buffs (pre-match prep): [../systems/world-buffs.md](../systems/world-buffs.md)
- Other PvP: [honor-system.md](honor-system.md)
