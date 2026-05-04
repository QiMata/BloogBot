# Talents — 1.12.1 51-Point Trees + Respec Mechanics

> **Sources** (crawl date 2026-05-01):
> - https://vanilla-wow-archive.fandom.com/wiki/Talent (referenced via search)
> - https://us.forums.blizzard.com/en/wow/t/talent-reset-cost-decay/342966 (referenced via search)
> - https://nostalrius.org/viewtopic.php?f=76&t=30663 (referenced via search)
> - Per-class talent files: [classes/warrior.md](../classes/warrior.md) etc.
>
> **Pass 2.** Some details (exact early-respec progression 1g→2g→5g→10g vs strict +5g per step) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Patch 1.11 introduced respec cost decay — fully active by 1.12.1.

## Core mechanics

| Mechanic | Value |
|---|---|
| Total talent points at lvl 60 | **51 points** |
| Talent points per level | **1 per level from 10-60** |
| Talent trees per class | **3** (e.g. Arms/Fury/Prot for Warrior) |
| Capstone position | **31-pt** (the bottom-most talent in each tree, requires 30 points spent in that tree to access) |
| Max points in single tree at lvl 60 | **51** (full deep spec) |
| Trainer for talent points | Class trainer |
| Talent point cost | Free (just visit trainer + click in talent UI) |

## How talent trees work

- Each class has **3 trees** of ~14-18 talents arranged in a 7-row grid (rows 1-7).
- **Each row requires N points spent in the tree to access**: row 2 = 5pts, row 3 = 10pts, row 4 = 15pts, row 5 = 20pts, row 6 = 25pts, row 7 = 30pts.
- The **31-pt capstone** is the row-7 single talent that requires 30 prior points in that tree.
- Most talents are **5-rank** (each rank cost 1 point — so a 5/5 talent uses 5 points).
- Some talents are **1-rank** signature abilities (Mortal Strike, Bloodthirst, Adrenaline Rush, etc.) — usually the capstones.
- Cross-tree talent points work additively but must satisfy each tree's own row requirements (so spending 30 in Arms unlocks Mortal Strike in Arms but doesn't help Fury or Protection).

## Respec mechanics — cost progression

Reset all talents at the **class trainer** for a gold cost that escalates with each respec:

| Respec # | Cost | Notes |
|---|---|---|
| 1st | **1g** | Cheap first respec |
| 2nd | **5g** `[verify pass 3 — some sources say 2g first then 5g]` | |
| 3rd | **10g** | |
| 4th | **15g** | |
| 5th | **20g** | |
| 6th | **25g** | |
| 7th | **30g** | |
| 8th | **35g** | |
| 9th | **40g** | |
| 10th | **45g** | |
| **11th and beyond** | **50g (cap)** | |

**Cap**: 50g per respec is the maximum.

## Respec cost decay (patch 1.11+ feature)

After **30 days without respeccing**, the cost decreases by **5g per month** down to a **floor of 10g**:

- Player at 50g cap → wait 30 days → cost = 45g
- Wait another 30 days (60 days total) → cost = 40g
- ... continues decaying 5g/30 days ...
- Floor: **10g** (decay won't reduce below 10g)
- A player who respecs frequently keeps their cap; a player who waits 6+ months between respecs returns to ~10g

**Engine planning rule**: Time respecs strategically — for raid-content phase changes (e.g., MC → BWL launch triggers Mage swap from Frost to Fire), the engine should plan a respec **every 2-3 phases** rather than monthly to keep total cost manageable.

## Common respec scenarios

| Class | Phase trigger | Spec change | Reasoning |
|---|---|---|---|
| **Mage** | MC raid launch | Deep Frost (10/0/41) | Most MC bosses are Fire-immune |
| **Mage** | BWL/AQ40 launch | Deep Fire (5/41/5) | BWL Vael / Razorgore + AQ40 Sartura are Fire-vulnerable |
| **Druid** | MC raid healer | Restoration 14/0/37 | Resto is the raid healer spec |
| **Druid** | Solo questing weeks / AQ20 farming | Feral Cat 0/30/21 | Feral solo content + AQ20 melee push |
| **Warrior** | Lvl-60 ding | Fury Impale 17/31/3 (raid DPS) or Prot 8/5/38 (MT) | Leveling spec → end-game spec |
| **Priest** | Raid healing primary | Holy 23/30/0 | After leveling Shadow Spirit Tap |
| **Shaman** | Resto raid healer | 5/3/43 Resto | After leveling Enhancement |
| **Paladin** | Charger chain prep | Holy 31/5/15 | Reset to raid-healer spec for class chain healers |

**Engine respec budget**: Most active raiders pay **150-300g over a 60's lifetime in respec costs**. Budgeting 50g per respec × 4-6 respecs through Phase 1 → AQ40 is a typical account-level expense.

## Talent point planning — class quest connection

Talent points are spent at the trainer using the free talent UI. **Talent quests do NOT exist in vanilla 1.12.1** — talents are purely level-up rewards. (Class quests are separate — they unlock spells/spells like Sap or Charge, not talent points.)

## VMaNGOS / private server notes

- **Talent point distribution** (1 per level 10-60 = 51 points at 60) is correctly implemented.
- **Respec cost progression + 50g cap** is faithful to 1.12.1.
- **5g/30-day decay with 10g floor** is correctly implemented.
- **Tree row gates** (5/10/15/20/25/30) work correctly.
- **Cross-tree spending** is allowed (e.g., 17 Arms / 31 Fury / 3 Prot Fury Impale build).

## Decision-Engine Rules

- **id:** `talents.cap-tree-prereqs` — IF placing a talent point in row N of a tree THEN ensure prior tree investment ≥ (N-1)*5 points. Engine validates spec plans before respec.
- **id:** `talents.role-target` — IF `Level==60 && Role.IsSet && CurrentSpec != Role.PreferredSpec` THEN respec to PreferredSpec. Priority **750**.
- **id:** `talents.respec-budget` — IF `RespecCount.ThisMonth >= 2` THEN warn that 50g cap is approaching. Priority **decision-request**.
- **id:** `talents.phase-aware-respec` — IF `RaidPhaseChange.Detected (MC→BWL or BWL→AQ40)` AND `CurrentSpec.IsSubOptimalForNewPhase` THEN plan respec. Priority **800** (raid-prep critical-path).
- **id:** `talents.decay-strategy` — IF `Player.LastRespecDate < (now - 60 days) AND PlannedRespecCost > 25g` THEN log "respec cost has decayed, consider scheduled respec now". Priority **400**.

## Snapshot Fields Needed

- `Level`, `Class` (existing)
- `TalentTreePoints[treeId]` (planned — points-per-tree integers, sum = 51 at 60)
- `CurrentSpec` (planned, derivable from TalentTreePoints)
- `Role.PreferredSpec` (planned account flag)
- `RespecCount.Lifetime` (planned — derived from telemetry / character-state)
- `RespecCount.ThisMonth` (planned)
- `Player.LastRespecDate` / `RespecCostNow` (planned)

## Cross-references

- All 9 [classes/](../classes/) class files — each lists 51-pt build templates for raid/PvP/leveling/hybrid roles
- [classes/all-9-classes-summary.md](../classes/all-9-classes-summary.md) — cross-class spec table
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity + respec priority bands
