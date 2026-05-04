---
title: "System — Talent Respec (No Dual-Spec in 1.12)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Talent Respec — Cost Progression, Decay, Tactical Timing

**Dual specialization does NOT exist in 1.12.1** (added in WotLK 3.1). Each character has **single talent tree allocation**. **Respec cost progression**: 1g → 5g → 10g → 15g → 20g → 25g → 30g → 35g → 40g → 45g → **50g cap (11th respec onward)**. **Decay**: -5g per 30 played-time days, **decay floor at 10g** (re-respec available at 10g minimum). **Class trainer respec** available at any capital city. **Reset returns all talent points** for re-allocation. Tactical respec timing: bracket transitions (e.g., Mage Frost solo → Frost+Improved Frostbolt for raid), class quest unlocks (e.g., Druid Resto for healing pre-raid), raid prep (e.g., Priest Holy → Shadow for solo grinding then back). Engine should track respec count + decay timer.

---

## Respec Cost Progression

| Respec # | Cost | Cumulative |
|----------|------|------------|
| 1 | 1g | 1g |
| 2 | 5g | 6g |
| 3 | 10g | 16g |
| 4 | 15g | 31g |
| 5 | 20g | 51g |
| 6 | 25g | 76g |
| 7 | 30g | 106g |
| 8 | 35g | 141g |
| 9 | 40g | 181g |
| 10 | 45g | 226g |
| 11+ | **50g (CAP)** | +50g per additional |

**Decision-engine cue:** at 11+ respecs, cost stops rising at 50g per respec. Annual respec budget = ~250-500g for active min-maxers.

---

## Decay Mechanic

Respec cost decays over **played time**:

| Decay rule | Detail |
|-----------|--------|
| **Decay rate** | -5g per 30 days **played time** (NOT real-time) |
| **Decay floor** | 10g (cannot decay below 10g) |
| **Decay trigger** | Periodic (every 30 played days) |

**Example:** Player at 50g respec cap → 30 played days = 45g → 60 played days = 40g → 7 cycles (210 days played) = 15g → 8 cycles = 10g (floor).

**Decision-engine rule:** for active L60 raiders, plan ~5-10 respecs per year, leveraging decay between bracket transitions. Engine should track per-character `Respec.Count` + `Respec.LastDecayCheck`.

---

## Class Trainer Respec Mechanic

Class trainer respec at any capital city:

| Service | Detail |
|---------|--------|
| **Trainer NPC** | Per-class trainer in respective city district |
| **Action** | "Pay X gold to forget all talents" dialogue |
| **Result** | All talent points refunded; spend from scratch |
| **No reset of class quests** | Class quest items + spells retained |

**Decision-engine cue:** pre-respec, ensure character has the gold reserve. Engine should `Snapshot.Gold >= RespecCost` check before action.

---

## Tactical Respec Timing

### Pre-L60 Bracket Transitions

| Bracket transition | Recommended respec scenario |
|-----|---|
| **L1-10 (no respec needed)** | First talent spent at L10; no respec required |
| **L20 (post-Polymorph for Mage)** | Mage Frost → Frost+Improved Frostbolt (5/5 mid-tier) |
| **L30 (post-Travel Form Druid)** | Druid Feral → Resto for group healing prep (or Feral 31-pt Force of Nature if PvP-focused) |
| **L40 (post-Plate Armor Warrior)** | Warrior Fury → Arms (Mortal Strike) for raid DPS, OR Prot for tank role |
| **L50 (post-Sunken Temple Mage staff)** | Mage Frost → Mage Fire (Pyroblast 31-pt) for caster DPS spec |
| **L60 (post-Class Epic)** | Class epic gear may shift talent priorities (e.g., Hunter Beast Mastery 31-pt + Rhok'delar bow) |

### Raid Prep Respecs

| Encounter | Recommended respec |
|-----|---|
| **Naxx Frostwyrm Lair (Sapphiron + KT)** | Frost Resistance gear required → temporarily respec for FR gear-friendly stats |
| **AQ40 Princess Huhuran** | Nature Resistance gear → temp respec for NR-friendly stats |
| **MC Ragnaros tank** | Fire Resistance + tank stats → temp respec for FR-friendly tanking |
| **PvP rank-14 grind** | Aff Warlock / Frost Mage / Disc Priest typical PvP specs — respec for HK efficiency |

### Class Quest Respecs

| Class | Class quest + recommended respec |
|-------|----------------------------------|
| **Druid** | Travel Form L30 → Resto if healing role required, OR Feral if solo questing |
| **Warlock** | Felsteed L40 → Demonology spec for class quest pet survival |
| **Hunter** | Rhok'delar/Lok'delar L60 → Beast Mastery for soloing demonic-grove fights |
| **Priest** | Benediction/Anathema L60 → Holy or Shadow depending on group role |

### Multi-Spec Workflow (Single-Spec Era)

Without dual-spec, players must choose one spec at a time. Common patterns:

| Pattern | Description |
|---------|-------------|
| **Single-spec** | Player commits to one spec (e.g., Holy Priest healer) and never respecs |
| **Annual respec** | Player respects 1-2x per year for major content shifts (e.g., Naxx prep) |
| **Bracket-transition respec** | Player respecs at major level milestones (L40, L60) |
| **Daily respec workflow** | High-cost — player respecs frequently for 5g→50g cap; respec budget 100-300g per cycle |

**Decision-engine rule:** for typical raiders, plan **2-4 respecs per year**. For min-maxers, **5-10 respecs per year**. Cap budget at ~300g respec/year.

---

## Respec Cost Tracking

| Cost tier | Months active raid play |
|-----------|------------------------|
| **1g** | First respec (no prior) |
| **5g** | Within 30-60 played days |
| **10-25g** | Mid-bracket / bracket-transition |
| **30-45g** | Active L60 raider |
| **50g cap** | High-frequency respec |
| **10g floor** | After ~210+ played days of decay |

**Decision-engine cue:** engine should ALWAYS track `Respec.Count` + `Respec.LastDecayCheck` to avoid surprise 50g respec charges.

---

## Respec vs Talent Tree Restraint

### When to Avoid Respec

| Condition | Reason |
|-----------|--------|
| Cost too high (50g cap) | Wait for decay |
| New talent build hasn't been validated | Theorycraft first |
| Bracket-transition near | Wait for major class quest unlock |
| Group composition stable | No need for spec change |

### When to Respec

| Condition | Reason |
|-----------|--------|
| Major raid week (Naxx prep) | Resistance build alignment |
| Switching from leveling spec to endgame spec | Bracket transition complete |
| PvP rank push | Class-spec PvP optimization |
| Class quest reward (e.g., Quel'Serrar Warrior) | Talent build shifts |

---

## Decision-Engine Rules

1. **No dual-spec in 1.12**: engine should not assume dual-spec mechanics. Single spec at a time.
2. **Cost progression cap**: 50g cap at 11+ respecs. Engine should track `Respec.Count` per character.
3. **Decay tracking**: -5g per 30 played days, floor 10g. Engine should `Snapshot.Respec.LastDecayCheck` per character.
4. **Tactical respec timing**: bracket transitions (L20/L30/L40/L60), raid week (Naxx/AQ40), class quest unlock, PvP rank push.
5. **Pre-respec gold check**: ensure `Snapshot.Gold >= RespecCost` before action.
6. **Annual respec budget**: 100-300g for typical raiders, 300-500g for min-maxers.
7. **Played-time decay**: encourage decay between respecs. Engine should NOT spam respec; wait for decay window.
8. **Class trainer respec**: any capital city; no special trainer required.
9. **No talent reset on class quest**: class quest items + spells retained through respec. Engine should not require re-quest.

---

## Snapshot Fields Needed

```text
Snapshot.Talents.PointsSpent                      // current talent allocation
Snapshot.Talents.AllocationByTree                 // per-tree breakdown
Snapshot.Respec.Count                             // total respecs to date
Snapshot.Respec.LastCost                          // most recent respec cost
Snapshot.Respec.LastDecayCheck                    // played-time decay tracking
Snapshot.Respec.NextCost                          // computed cost for next respec
Snapshot.Gold                                     // pre-respec reserve check
Snapshot.WallClock.PlayedTime                     // for decay calculation
Snapshot.Encounter.Active                         // raid prep respec trigger
Snapshot.Class                                    // class-specific respec strategy
Snapshot.Spec.Current                             // single-spec tracking (Holy/Shadow/Disc Priest, etc.)
```

---

## Cross-References

- Spell ranks (cross-talent dependency): [spell-ranks.md](spell-ranks.md)
- Talents (overall talent system): [talents.md](talents.md)
- Class trainers per city: [../zones/cities/](../zones/cities/)
- All classes (per-class respec strategies): [../classes/](../classes/)
- Resistance gear (raid prep respec triggers): [resistance-gear.md](resistance-gear.md)
