# PvP

> **Pass 1 placeholder.** Honor system, ranks 1-14, BG strategies. Pass 8.

## Planned files (pass 8)

| File | Topic |
|---|---|
| `honor-system.md` | 1.12 honor mechanics — HKs, contribution points, weekly decay, standing math |
| `ranks-1-14.md` | Per-rank weekly contribution thresholds, realistic time investment, decay calculations |
| `pvp-rewards-by-rank.md` | Rank rewards table (rank 7-10 epic gear sets, rank 11 mount, rank 13 pieces, rank 14 GM/HW weapons + epic title) |
| `warsong-gulch.md` | 10v10, lvl 10+, lvl 30 / 40 / 50 / 60 brackets; flag carry strategy; mid map |
| `arathi-basin.md` | 15v15, lvl 20+, lvl 30 / 40 / 50 / 60 brackets; node-control strategy |
| `alterac-valley.md` | 40v40, lvl 51+; 1.12 short-AV strategy (~30-40 min); turn-in items; Stormpike/Frostwolf rep grind |
| `world-pvp.md` | World PvP in 1.12.1 (no formal mechanics; honor for /yell-detected kills only — write what's true and call out the lack) |
| `bg-rep-grinds.md` | AB Exalted = epic 100% mount; AV Exalted = ram/wolf mount + Stormpike/Frostwolf trinkets |

## Honor math primer (pass 1 quick-ref — to be expanded in pass 8)

The 1.12 honor system uses **weekly contribution points** computed from honorable kills, dishonorable kills (debuff), bonus honor (BG objectives), and standing on the *server-wide ranked list*. Standing decays each week (~80% of last week's CP carries forward).

Rank 14 (GM / HW) typically required ~300,000+ CP/week sustained for 12-14 weeks on most servers, in practice meaning 60-80 hours/week of constant BGs and world PvP. **The single longest grind in the game.**

| Rank | Title (Alliance) | Title (Horde) |
|---|---|---|
| 1 | Private | Scout |
| 2 | Corporal | Grunt |
| 3 | Sergeant | Sergeant |
| 4 | Master Sergeant | Senior Sergeant |
| 5 | Sergeant Major | First Sergeant |
| 6 | Knight | Stone Guard |
| 7 | Knight-Lieutenant | Blood Guard |
| 8 | Knight-Captain | Legionnaire |
| 9 | Knight-Champion | Centurion |
| 10 | Lieutenant Commander | Champion |
| 11 | Commander | Lieutenant General |
| 12 | Marshal | General |
| 13 | Field Marshal | Warlord |
| 14 | **Grand Marshal** | **High Warlord** |

## Standard sections per BG file (pass 8 contract)

1. **Map** + objectives
2. **Recommended composition** per faction
3. **Standard strategy** (mid-zerg vs base-D, GY camps, flag-carry rotation, etc.)
4. **Rep gain rate** + key rewards per tier
5. **Honor / mark drop rates** for the BG
6. **Decision-Engine Rules**
7. **Snapshot Fields Needed**

## Decision-engine note: PvP role flag

The engine treats PvP as a separate end-state goal track. A character flagged `PvPRole = true` (account-level config) gets:
- Higher priority on opening BG queues at the eligibility threshold (lvl 10 WSG, 20 AB, 51 AV)
- Different gear preferences (Resilience does not exist in 1.12 — instead, **Stamina** + **PvP-specific itemization** like the AV trinkets, Whirlwind Axe / Sword off-hand, Battlemaster's Flask of, etc.)
- Weapon-skill grinds at higher priority (in 1.12 PvP is significantly weapon-skill-gated)
