---
title: "Dungeon — Maraudon"
patch: "1.12.1 (Drums of War, Sept 2006); raid added in 1.5 'Battlegrounds' (May 2005)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Maraudon
crawl_date: 2026-05-01
---

# Maraudon (5-man) — Three-Wing Centaur-Crystal Earth Cavern

5-man dungeon in central Desolace. **3-area structure** (Wicked Grotto / Purple wing + Foulspore Cavern / Orange wing + Earth Song Falls / final), 7+ bosses culminating at **Princess Theradras**. Sweet spot **lvl 44-50**. Notable drops: **Bonecrusher** (Warrior 1H mace), **Mark of Tyranny** (tank trinket), **Theradric Crystal Carving**, **Charm of the Earth and Sky** (caster trinket), **Helm of Endless Rage** (rare). Multi-entrance, multi-sub-instance design — typically 2-3 hour full clear with experienced group.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 40-50 (optimal 44-50) |
| Lockout | None (no instance reset) |
| Continent / Zone | Kalimdor — central Desolace |
| Faction | Both (cross-faction) |
| Theme | Centaur + Earth elemental + crystal cavern |
| Entrance(s) | 2 entrances merge at Earth Song Falls (Wicked Grotto / Foulspore Cavern); separate portal from Cenarion Hold |
| Notable drops | **Bonecrusher** mace, **Mark of Tyranny** tank trinket, **Theradric Crystal Carving** caster trinket, **Charm of the Earth and Sky** caster, **Pristine Hide of the Beast** (drop chance), **Caverndeep Ambusher** Rogue trinket |
| Boss count | 7+ across 3 areas |

---

## Three-Area Structure

| Area | Purpose | Sequence |
|------|---------|----------|
| **Wicked Grotto (Purple wing)** | Outer entrance from Desolace surface; first 3 bosses | Tinkerer Gizlock → Lord Vyletongue → Celebras the Cursed |
| **Foulspore Cavern (Orange wing)** | Second entrance from Desolace surface; 2 bosses | Razorlash → Noxxion |
| **Earth Song Falls (Final)** | Merged endpoint; 3 bosses | Landslide → **Princess Theradras** → Rotgrip |

**Note:** Both Wicked Grotto and Foulspore Cavern entrances lead to the same Earth Song Falls inner area. A typical full-clear chains both wings before final Princess Theradras kill.

**Decision-engine rule:** Maraudon has **2 entry options**. Engine should pre-determine entry based on group composition and target loot. Wicked Grotto is more melee-friendly; Foulspore Cavern is caster-AoE-friendly.

---

## Boss Order — Wicked Grotto (Purple Wing)

### Boss 1: Tinkerer Gizlock

| Field | Value |
|-------|-------|
| HP | ~14k |
| Phases | None |
| Mechanic | Goblin engineer with **Goblin Bombs** + **Goblin Mortar** AoE attacks; tank stays in melee while raid spreads to dodge bombs; spawns **Goblin Mechs** as adds |
| Notable drops | **Heavy Plated Battleboots** (plate boots); **Tinkerer's Phase-Modulator** (caster trinket); **Goblin Construction Helmet** (cosmetic) |

### Boss 2: Lord Vyletongue

| Field | Value |
|-------|-------|
| HP | ~16k |
| Phases | None |
| Mechanic | **Stealth-then-strike** opener; **Curse of the Cataclysm** debuff (DoT, dispel-needed) on tank; melee + Cleave |
| Notable drops | **Spinesnap Polearm** (Hunter polearm); **Cataclysm Cloak**; **Vyletongue's Lash** (Rogue dagger) |

### Boss 3: Celebras the Cursed (intermediate)

| Field | Value |
|-------|-------|
| HP | ~16k |
| Phases | **2-phase**: Cursed (red) → **Redeemed (transformed)** at 0 HP |
| Mechanic | Phase 1: Tank-and-spank with **Curse of Frailty** (50% healing reduction, dispel); **Roots** (5-yard immobilize); on-death, **transforms into Celebras the Redeemed** (NPC ally) |
| Outcome | Celebras Redeemed grants Earth Song Falls portal access (skip-route to Princess) |
| Notable drops | **Celebras's Robe of Wisdom** (caster cloth); **Earthen Sigil**; **Celebrian Diamond** rare drop |

**Decision-engine cue:** Celebras transformation = checkpoint — if killed, raid can skip back to Earth Song Falls via portal. Engine should encode "Celebras Redeemed" as portal-unlock signal.

---

## Boss Order — Foulspore Cavern (Orange Wing)

### Boss 4: Razorlash

| Field | Value |
|-------|-------|
| HP | ~16k |
| Phases | None |
| Mechanic | **Spore Cloud** AoE poison (3-yard radius around Razorlash; raid stays out + DPS from range); **physical Cleave** front-of-room |
| Notable drops | **Carapace of Razorlash** (caster cloak); **Razorlash's Tail Lash** (rare leather); **Spore-Encrusted Pads** (Druid Feral) |

### Boss 5: Noxxion

| Field | Value |
|-------|-------|
| HP | ~18k |
| Phases | None |
| Mechanic | **Noxxious Vapors** AoE (~1k nature damage per pulse, every 5s on entire raid); **Nature Resistance gear helps** (~50 NR per raider); **Toxic Sludge adds** spawn (4-6 lvl 50 elites); raid AoE adds while tank holds Noxxion |
| Notable drops | **Noxxious Stalker Boots** (Hunter mail); **Slime Stream Bands** (caster cloth); **Sludge Belt** (Warrior tank); **Toxic Bracers** |

**Decision-engine rule:** Noxxion is the **Nature Resistance-tier boss**. If raid average NR < 50, defer Noxxion to gear-up week. Engine should track per-character NR.

---

## Boss Order — Earth Song Falls (Final Area)

### Boss 6: Landslide (mid-boss)

| Field | Value |
|-------|-------|
| HP | ~12k |
| Phases | None |
| Mechanic | **Earth Slam** (knockback all melee 5-yards + 1k damage); positional — keep on flat ground; **Boulder roll** (avoid line). |
| Notable drops | **Helm of the Mountain** (mid-tier mail); **Stoneslayer** (Warrior 2H mace); **Landslide** (cosmetic mace) |

### Boss 7: Princess Theradras (Final Boss)

| Field | Value |
|-------|-------|
| HP | ~22k |
| Phases | None (continuous spawn cycle) |
| Mechanic | **Earth Charge** (random raid 2k damage); **Repulsive Gaze** (random raid charm/fear, 8s); **Dust Field** AoE (3-yard area persistent damage); **Bound Earth Elementals** spawn periodically; tank rotation when Mortal Strike-stack ≥ 3 |
| Notable drops | **Mark of Tyranny** trinket (tank — +Stamina + block); **Bonecrusher** (Warrior 1H mace, BiS-mid-bracket DPS); **Charm of the Earth and Sky** (caster trinket — +25 Spell Damage proc); **Princess's Theradric Crystal Carving** (caster trinket — +30 Spell Crit proc); **Helm of Endless Rage** (Plate-DPS helm rare) |

**Decision-engine rule:** Princess Theradras is **the** Maraudon endpoint. Engine should track loot priority for Bonecrusher + Mark of Tyranny + Charm of the Earth and Sky based on raid composition.

### Boss 8: Rotgrip (post-Princess, optional)

| Field | Value |
|-------|-------|
| HP | ~18k |
| Phases | None |
| Mechanic | **Crocolisk Rage** (frenzy at low HP, +30% attack speed); **tail Sweep** (knockback); positional — keep on land |
| Notable drops | **Rotgrip Tail Cudgel** (1H mace); **Crocolisk Boot** (Hunter mail); **Hide-Encrusted Fang** (Rogue dagger) |

---

## Pristine Hide of the Beast (Druid Class Quest Source)

Princess Theradras has a chance to drop **Pristine Hide of the Beast** as a **rare loot** (1-2% rate). This is the same item as the Druid class quest reward (sister-source via Sunken Temple drop).

**Decision-engine rule:** If Druid alt has Pristine Hide quest active, Maraudon is a bonus farming target alongside Sunken Temple.

---

## Cenarion Circle Quests in Maraudon

Multiple quests start at **Cenarion Hold** (Silithus) or **Cenarion Outpost** (Desolace, near Maraudon entrance) and complete inside Maraudon. These provide:
- **Cenarion Circle reputation** rep accelerator
- Crystal-collector quests (Theradric Crystal turn-ins)
- Earth Song Falls quest chain (Cenarion-themed)

**Decision-engine rule:** Cenarion Circle quests in Maraudon are bracket-defining for Druid alts pursuing CC rep. Engine should always pickup CC turn-in quests on first Maraudon entry.

---

## VMaNGOS / Server Reality Check

Maraudon is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Tinkerer Gizlock | Goblin Bomb spawn timing + AI |
| Lord Vyletongue | Stealth-opener invisibility detection |
| Celebras the Cursed | Phase-2 transformation NPC state |
| Noxxion | Noxxious Vapors AoE timing + Toxic Sludge add spawn |
| Princess Theradras | Repulsive Gaze charm AI; Bound Earth Elemental spawn pathing |

**Decision-engine rule:** Maraudon script-completeness check pre-pull; almost always-Pass on modern VMaNGOS.

---

## Decision-Engine Rules

1. **Entry strategy**: 2 entrances on Desolace surface (Wicked Grotto vs Foulspore Cavern). Pre-determine based on target loot:
   - Bonecrusher / Mark of Tyranny → Wicked Grotto + Earth Song Falls (Princess)
   - Caverndeep Ambusher / Carapace of Razorlash → Foulspore Cavern
   - Full clear → Both entrances + Earth Song Falls
2. **Celebras checkpoint**: kill Celebras the Cursed → portal to Earth Song Falls unlocked. Engine should track `Snapshot.Boss.CelebrasRedeemed == true` as Earth Song Falls signal.
3. **Nature Resistance gate (Noxxion)**: raid average NR ≥ 50. Engine should check per-character NR pre-Noxxion-pull.
4. **Princess loot priority**:
   - Tank: Mark of Tyranny (BiS trinket)
   - Plate-DPS: Bonecrusher (1H mace)
   - Caster: Charm of the Earth and Sky / Theradric Crystal Carving
   - Druid: Pristine Hide of the Beast (rare drop)
5. **Earth Song Falls portal usage**: post-Celebras kill, engine should suggest portal-skip vs full back-track based on group fatigue.
6. **Cenarion Circle parallel**: Cenarion-Hold/Outpost questgivers in Desolace + Silithus give CC rep for Maraudon clears. Engine should always pickup CC quests on entry.
7. **Druid Pristine Hide farming**: Maraudon is one of 3 sources (Sunken Temple, Druid class quest reward primary). Engine should track per-alt quest status.
8. **Boss order optimization**: standard run = Wicked Grotto (3 bosses) → Earth Song Falls (4 bosses) → optional Foulspore (2 bosses). Engine should encode dynamic order based on time-budget.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 40-50 entry gate
Snapshot.Class                                    // role + Druid Pristine Hide bias
Snapshot.PartyComposition.{Tank, Healer, DPS}     // 5-man standard
Snapshot.Equipment.NatureResistance               // Noxxion gate >= 50
Snapshot.Boss.CelebrasRedeemed                    // Earth Song Falls portal signal
Snapshot.QuestLog.Active.PristineHideOfTheBeast   // Druid class quest tracking
Snapshot.QuestLog.Active.CenarionCircleMaraudon   // CC rep quest progress
Snapshot.Inventory.Has("Bonecrusher")             // Princess loot tracker
Snapshot.Inventory.Has("MarkOfTyranny")           // Princess loot tracker (tank)
Snapshot.Inventory.Has("PristineHideOfTheBeast")  // Druid quest reagent
Snapshot.Reputation.CenarionCircle                // rep gain tracking
Snapshot.ServerCapabilities.MaraudonBoss[<name>]  // VMaNGOS scripting flag
```

---

## Cross-References

- ZG attune chain (Pristine Hide co-source via Sunken Temple): not yet covered (pending — Sunken Temple)
- Druid Pristine Hide of the Beast (class quest origin): [../classes/druid.md](../classes/druid.md)
- Cenarion Circle rep: [../reputations/](../reputations/) (file pending)
- Robe of the Archmage (uses Pristine Hide as reagent): [../professions/tailoring.md](../professions/tailoring.md)
- Hide of the Wild + Truefaith Vestments (also Pristine Hide reagent): [../professions/leatherworking.md](../professions/leatherworking.md), [../professions/tailoring.md](../professions/tailoring.md)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
- L40-L50 bracket: [../sections/05-l40-l50.md](../sections/05-l40-l50.md)
