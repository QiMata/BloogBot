---
title: "Dungeon — Stratholme: Undead Side (Baron Rivendare)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Stratholme
crawl_date: 2026-05-01
---

# Stratholme: Undead (5-man) — Baron Rivendare + 45-Min Run + Deathcharger's Reins

5-man dungeon in Eastern Plaguelands, eastern half of Stratholme city. **Two-side instance**: Undead (this file, Baron) + Live (Scarlet, separate file). 7 bosses culminating at **Lord Aurius Rivendare**. Sweet spot **lvl 58-60**. Notable drops: **Deathcharger's Reins** (1% drop, epic class horse), **Eye of Naxxramas** (trinket, raid utility), T0.5 piece drops, **The Postmaster's Trousers** (legs). The **45-Minute Baron Run** is the iconic timed event — quest from Ysida Harmon (rescued NPC) requires Baron kill within 45 minutes for full rewards.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 58-63 (optimal 60) |
| Lockout | None per-instance reset; **45-min Baron timer** is per-attempt |
| Continent / Zone | Eastern Kingdoms — Eastern Plaguelands (Stratholme east gate) |
| Faction | Both (cross-faction) |
| Theme | Cult of the Damned + Scourge undead city |
| Entry | **East gate** (UD side) — separate from Live entrance west gate |
| Notable drops | **Deathcharger's Reins** (1% Baron drop, epic mount), **Eye of Naxxramas** trinket, **Crown of Tyranny** Warrior tank helm, **Skullforge Reaver** sword, T0.5 piece drops, **The Postmaster's Trousers** (legs) |
| Boss count | 7 bosses (Barthilas → Maleki → Nerub'enkan → Baroness Anastari → Ramstein → Baron Rivendare) |

---

## Boss Order

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Magistrate Barthilas** | Medium (special opener) | East gate vestibule |
| 2 | **Stonespine** *(rare elite, optional)* | Easy | Mid-level |
| 3 | **Maleki the Pallid** | Medium-Hard (frost mage) | Inner courtyard |
| 4 | **Nerub'enkan** | Medium (spider) | Inner courtyard |
| 5 | **Baroness Anastari** | Hard (banshee Mind Control) | Inner courtyard |
| 6 | **Ramstein the Gorger** | Hard (post-Baron's-front, multi-wave) | Front courtyard |
| 7 | **Lord Aurius Rivendare (Baron)** | Hard (final) | Slaughter House |

**Decision-engine rule:** Strat-UD is the **endgame 5-man for solo Deathcharger farming**. Engine should plan post-L60 multiple weekly attempts.

---

## Boss 1: Magistrate Barthilas (Special Opener)

| Field | Value |
|-------|-------|
| HP | ~24k |
| Phases | None |
| Mechanic | **Tank-and-spank** but **runs to summon** Crimson Hammersmith (Live-side boss) if not engaged within ~5s — keeps fight tight; Drinks Mind Blast on tank; **Disarm** on tank; raid stays in melee |
| Notable drops | **Skullforge Reaver** (1H sword); **Magisterial Cuffs** (caster bracers); **Aurastone Hammer** (mid-tier 1H mace) |

**Decision-engine rule:** Barthilas opener requires **tight pull timing** — engage within 5s of door open or he runs to call adds. Engine should encode pre-pull positioning invariant.

---

## Boss 2: Stonespine (rare optional)

| Field | Value |
|-------|-------|
| HP | ~20k |
| Phases | None |
| Mechanic | Standard tank-and-spank with **Curse of Tongues** debuff (caster damage reduction) |
| Notable drops | **Stonespine's Vest** (Plate-tank); **Eye Bone of Stonespine** (rare drop) |

---

## Boss 3: Maleki the Pallid (Frost Mage)

| Field | Value |
|-------|-------|
| HP | ~25k |
| Phases | None |
| Mechanic | **Frostbolt Volley** (5-yard AoE on closest 5 raiders); **Frost Nova** + **Blink** (kites raid for 5s); raid spreads + ranged DPS focus; **Frost Resistance gear helps** (~30 FR) |
| Notable drops | **Robes of the Lich** (Mage cloth chest); **Maleki's Ring** caster ring; **Wand of Eternal Light** caster wand |

**Decision-engine rule:** Maleki kites raid via Blink — encode 5s respawn point + AoE damage trigger.

---

## Boss 4: Nerub'enkan (Spider)

| Field | Value |
|-------|-------|
| HP | ~24k |
| Phases | None (spawn cycle) |
| Mechanic | **Spider Web** immobilize random raid; **Sand-Striker Curse** (slow + DoT, dispel-needed); spawns **Crypt Beasts** (4 elites); raid AoE adds while tank holds Nerub |
| Notable drops | **Nerub'enkan's Stinger** (caster wand); **The Ravager** (rare 2H polearm); **Webbing Bracers** |

---

## Boss 5: Baroness Anastari (Banshee Mind Control)

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Possess** (Mind Control random raid for 30s; possessed raider attacks party — must be CC'd or DPS-down to break MC); **Banshee Wail** AoE silence (10-yard silence on cast); **Banshee Curse** (50% damage taken increase, dispel-needed) |
| Notable drops | **Anastari's Cloak** (caster); **Banshee Maul** (Druid Feral 2H); **The Postmaster's Trousers** *(T0.5 leg piece, RNG)* |

**Decision-engine rule:** Possess mechanic requires DPS to **break MC on possessed teammate** (DPS-rush to 25% HP forces release). Engine should encode "possessed" detection + MC-break rule.

---

## Boss 6: Ramstein the Gorger (Multi-Wave)

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | **Post-Baron's-front-courtyard event** — 4 waves of trash spawn after Ramstein dies, then Baron emerges |
| Mechanic | **Bash** stun on tank (3s); **Knockback** (5-yard) every 30s; **Trample** AoE (3-yard, 2k damage); off-tanks pick up adds during 4 waves of 4-6 mobs |
| Notable drops | **Crown of Tyranny** (Warrior tank helm — pre-raid BiS); **Heart of Ramstein** trinket; **Maul of the Ancients** 2H |

**Decision-engine rule:** post-Ramstein 4-wave event = **multi-tank fight**. Off-tanks should pre-position for add control. Engine should encode wave-spawn timestamps.

---

## Boss 7: Lord Aurius Rivendare (Baron, Final)

| Field | Value |
|-------|-------|
| HP | ~32k |
| Phases | None |
| Mechanic | **Mortal Strike** (50% healing reduction on tank); **Dead Hand** (random raid 3k damage); **Tank-rotation** when stack ≥ 3; **Skeleton Adds** spawn periodically |
| Notable drops | **Deathcharger's Reins** (epic class horse, **1% drop chance**); **Eye of Naxxramas** trinket (+1% spell crit + spell damage proc); **Slayer's Crest** (Plate trinket); **Plagueborne Slayer's Cloak** |

**Decision-engine rule:** Baron Rivendare is **the** Strat-UD endpoint. Mortal Strike stack-tracking is the standard mechanic. Engine should always-loot Deathcharger's Reins on drop (BoP, BiS class horse for non-Pal/Wlk).

---

## The 45-Minute Baron Run

The iconic Strat-UD timed event.

### How to Trigger

| Step | Action | Source |
|------|--------|--------|
| 1 | Locate **Ysida Harmon** (caged Argent Dawn paladin NPC) inside Strat-UD courtyard area | Pre-Baron raid path |
| 2 | Free Ysida Harmon by clicking the Eye of Sulfuras-style chain (~3-4 second cast) | Cage opening |
| 3 | Receive quest **"Dead Man's Plea"** (Argent Dawn) | Quest acceptance starts 45-min timer |
| 4 | Kill Lord Aurius Rivendare within 45 minutes of quest acceptance | Boss completion |
| 5 | Talk to Ysida → turn-in quest | Reward |

### Rewards

| Reward | Notes |
|--------|-------|
| **Eye of Naxxramas** trinket | +1% spell crit + spell damage proc; raid-tier |
| **Argent Dawn rep** (Honored+) | Reputation accelerator |
| **Argent Avenger** (rare alternate) | Argent Dawn-rep weapon (2H mace) |
| **Increased Deathcharger's Reins drop chance**? | **`[verify pass 3]`** — some sources say timed run boosts drop rate; others say it's flat 1% always |

**Decision-engine rule:** 45-min Baron Run is **the only Strat-UD timed event**. Engine should encode 45-min countdown timer from Ysida turn-in; failure = Eye of Naxxramas not awarded but boss still kills.

---

## Deathcharger's Reins (Epic Class Mount)

The **most-coveted vanilla 1.12 mount drop**.

| Field | Value |
|-------|-------|
| Source | Lord Aurius Rivendare (final boss) |
| Drop rate | **1%** (very low) |
| BoP | Yes — character-locked |
| Stats | 100% land mount speed (epic) — **functions as epic ground mount for non-Paladin / non-Warlock** classes |
| Class restriction | None — usable by all classes (with Apprentice Riding 75 skill) |
| Practical | The **only** epic ground mount drop in vanilla; otherwise epic mount = 1000g + 100g purchase OR Pal Charger / Warlock Dreadsteed free |

**Decision-engine rule:** for any class except Paladin/Warlock at L60 without 1100g for epic mount, Deathcharger's Reins is the **lottery alternative**. Engine should plan 30-50+ Strat-UD runs at 1% drop rate.

---

## T0.5 Piece Sources (Sub-Set Tracking)

Strat-UD drops:

| T0.5 Piece | Boss | Class set tied |
|-----------|------|-----------------|
| **The Postmaster's Trousers** | Baroness Anastari (RNG) | Postmaster set (mage) |
| **Postmaster's Tunic** | Postmaster Malown (Live side, **NOT UD**) | Postmaster set |
| **Slayer's Crest** | Baron Rivendare | Plate-DPS trinket |
| **Plagueborne Cloak** | Baron Rivendare | All-class cloak |

**Decision-engine rule:** T0.5 acquisition is a multi-month rotation across BRD/UBRS/Strat-Live/Strat-UD/Scholo. Engine should track per-character T0.5 slot status.

---

## Atiesh Chain (Strat Festival Lane Purification)

After collecting Atiesh Frame + Staff Head + Base from Naxx + AQ40, the legendary chain final step happens at **Stratholme Festival Lane** (a separate building accessible via UD entry). The demon **Atiesh** spawns there for the final purification fight.

See [../raids/naxxramas.md](../raids/naxxramas.md#atiesh-greatstaff-of-the-guardian-legendary) for full Atiesh chain.

---

## VMaNGOS / Server Reality Check

Strat-UD is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Magistrate Barthilas | "Run to call" timer; cross-instance summon |
| Maleki the Pallid | Frost Volley AoE target selection; Blink animation desync |
| Baroness Anastari | Possess (Mind Control) AI; possessed raider behavior |
| Ramstein the Gorger | 4-wave trash spawn AI |
| Baron Rivendare | Mortal Strike stack reset; Skeleton add spawn timing |

**Decision-engine rule:** Strat-UD script-completeness is high. Engine should standard-pull without fail-fast checks on most modern VMaNGOS.

---

## Decision-Engine Rules

1. **Entry side**: Strat has 2 entrances (Live/west and UD/east). Engine should pre-determine target boss list:
   - Deathcharger's Reins / Eye of Naxx / Postmaster's Trousers → UD side
   - Slayer's set / Postmaster set / T0 → Live side
2. **Magistrate Barthilas opener**: tight pull timing required (engage within 5s). Engine should encode pre-pull positioning + door-open timer.
3. **Possess mechanic (Anastari)**: DPS-rush possessed teammate to 25% HP to break MC. Engine should encode possession detection + DPS-priority shift.
4. **45-min Baron Run**: optional but always-take. Engine should:
   - Auto-rescue Ysida Harmon on first encounter
   - Track 45-min timer from quest acceptance
   - Plan run pace: ~30 min for first 5 bosses, ~10 min for Baron, 5 min buffer
5. **Deathcharger farming priority**: 1% drop = 30-50 average runs. Engine should track per-account RNG-attempt count.
6. **T0.5 piece tracking**: per-character slot map; Strat-UD provides specific slots (Postmaster Trousers, Plate-DPS trinket, etc.).
7. **Argent Dawn rep parallel**: Strat-UD clears + Ysida turn-ins give AD rep. Engine should grind toward Honored (Naxx attune cost reduction).
8. **Atiesh Festival Lane**: post-Atiesh-collection, engine should plan final demon kill at Festival Lane.
9. **Lockout-free**: Strat-UD has no instance reset; engine can run multiple back-to-back for Deathcharger farming.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 58-63 entry gate
Snapshot.Class                                    // T0.5 piece + class trinket targeting
Snapshot.Inventory.Has("DeathchargersReins")      // 1% lottery mount tracker
Snapshot.Inventory.Has("EyeOfNaxxramas")          // Baron run reward
Snapshot.Inventory.Has("PostmasterTrousers")      // T0.5 leg slot (RNG)
Snapshot.QuestLog.Active.DeadMansPlea             // 45-min Baron Run timer signal
Snapshot.WallClock.BaronRunCountdown              // active timer
Snapshot.Boss.MagistrateBarthilas.Killed          // opener timing-success signal
Snapshot.Boss.LordAuriusRivendare.Killed          // final boss + Baron Run completion
Snapshot.Reputation.ArgentDawn                    // Honored gate
Snapshot.RaidGroup.Composition.Tanks              // 1-2 tank for Ramstein 4-wave + Baron
Snapshot.RaidGroup.PossessedRaiders               // Anastari Mind Control state tracking
Snapshot.RaidGroup.PullState.Barthilas5sWindow    // opener timing invariant
Snapshot.QuestLog.Active.AtieshFestivalLane       // legendary chain final step
Snapshot.ServerCapabilities.StratUDBoss[<name>]   // VMaNGOS scripting flag
```

---

## Cross-References

- Strat-Live (Scarlet side, separate file): not yet covered (pending)
- Naxx attune chain (AD Honored gate): [../attunements/naxxramas.md](../attunements/naxxramas.md)
- Argent Dawn rep: [../reputations/argent-dawn.md](../reputations/argent-dawn.md)
- Atiesh chain (Festival Lane purification): [../raids/naxxramas.md](../raids/naxxramas.md), [../raids/ahn-qiraj-temple.md](../raids/ahn-qiraj-temple.md)
- L50-L60 bracket: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
- T0.5 progression: [../classes/](../classes/) (per-class file)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [sunken-temple.md](sunken-temple.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
