---
title: "Dungeon — Dire Maul: West (Capital Gardens)"
patch: "1.12.1 (Drums of War, Sept 2006); Dire Maul released in 1.3 'Ruins of the Dire Maul' (Mar 2005)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Dire_Maul
crawl_date: 2026-05-01
---

# Dire Maul: West (5-man) — Tendris + Immol'thar + Prince Tortheldrin

5-man dungeon in Feralas (Dire Maul ruins, west wing). 6 bosses culminating at **Prince Tortheldrin**. Sweet spot **lvl 58-60**. The iconic mechanic is the **Immol'thar Pylon System** — Prince Tortheldrin maintains a magical shield around Immol'thar (a captured demon) via 4-5 generator pylons. Raid chooses: **smash pylons → fight Immol'thar first → weakened Tortheldrin** (easier), OR **rush Tortheldrin → fight Immol'thar at full HP** (harder). Notable drops: **Foror's Eyepatch** (alt source, Tortheldrin), **Wushoolay's Charm of Spirits** (Druid Resto trinket), **Robe of Volatile Power** (Warlock), **Highborne** caster gear, Druid epic chain Books.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 56-60 (optimal 58-60) |
| Lockout | None per-instance reset |
| Continent / Zone | Kalimdor — Feralas (Dire Maul ruins, west wing) |
| Faction | Both (cross-faction) |
| Theme | Highborne Quel'dorei elven ruins + corrupted ents |
| Notable drops | **Foror's Eyepatch** Hunter trinket (Tortheldrin RNG), **Wushoolay's Charm of Spirits** Druid Resto trinket, **Robe of Volatile Power** Warlock chest, **Highborne** caster gear, **Druid epic chain Books**, **Treant's Bane** 1H mace |
| Boss count | 6 (Tendris → Illyanna → Kalendris → Tsu'zee → Immol'thar → Tortheldrin) |
| Special mechanic | **Immol'thar Pylon System** — choose pylon-smash vs Tortheldrin-rush |

---

## Boss Order (Standard Pylon-Smash Run)

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Tendris Warpwood** | Hard (massive ent) | Entry forest area |
| 2 | **Illyanna Ravenoak + Pet** | Medium (paired) | Mid-instance forest |
| 3 | **Magister Kalendris** | Medium (caster) | Mid-instance |
| 4 | **Tsu'zee** *(rare owl, optional)* | Easy | Side path (highly rare) |
| 5 | *(Pylons smashed: 5 generators in side rooms)* | — | Pre-Immol'thar |
| 6 | **Immol'thar** *(after pylons smashed)* | Hard (high HP demon) | Throne Room |
| 7 | **Prince Tortheldrin** *(final)* | Hard (after pylons + Immol'thar OR after rush) | Throne Room |

**Decision-engine rule:** DM-W is the **6-boss densest mid-bracket dungeon**. Engine should plan 75-90 min full clear with experienced group.

---

## Boss 1: Tendris Warpwood (Massive Ent)

| Field | Value |
|-------|-------|
| HP | ~32k (high HP for entry boss) |
| Phases | None |
| Mechanic | **Branch Slam** AoE knockback (5-yard radius); **Roots** random raid (immobilize, dispel-needed); **Spell-immune** to magic damage above 50% HP — **physical DPS only Phase 1**; **Burning Sap** debuff on tank (DoT, 1.5k per pulse) |
| Notable drops | **Bracers of Heroism** (Plate); **Treant's Bane** (1H mace); **Sapwood Stamper** (Druid Feral leather mid-tier); **Branch of Cenarius** (rare caster staff) |

**Decision-engine rule:** Tendris spell-immune Phase 1 = physical DPS focus. Engine should encode "Phase 1 magic-resist" + "Phase 2 magic-allowed" transition.

---

## Boss 2: Illyanna Ravenoak + Companion Pet

| Field | Value |
|-------|-------|
| HP | ~26k (Illyanna) + ~12k (Ghost Sabertooth pet) |
| Phases | None (paired) |
| Mechanic | Hunter-themed boss with **Multi-Shot** (3 random raid 2k damage each); **Aimed Shot** burst (interruptible — DPS interrupt rotation); **Pet pairs with her** — kill pet first or simultaneously (pet's death triggers Illyanna's Frenzy) |
| Notable drops | **Wisp of the Outer Forest** (Mage caster wand); **Illyanna's Sash** (Hunter mail belt); **Boots of Avoidance** (Rogue) |

---

## Boss 3: Magister Kalendris

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Spellsteal-tier** caster; **Frost Bolt** burst; **Mass Polymorph** (random raid CC); **Magister's Curse** (caster damage reduction, dispel-needed); **Mind Control** random raider — interrupt cast |
| Notable drops | **Magister's Bracers** (caster cloth); **Robe of Volatile Power** *(Warlock cloth chest, RNG)*; **Mass Polymorph Wand** (rare caster wand) |

**Decision-engine rule:** Kalendris Mind Control = same as Strat-UD Anastari. Engine should DPS-rush possessed teammate.

---

## Boss 4: Tsu'zee (rare owl, optional)

| Field | Value |
|-------|-------|
| HP | ~16k |
| Phases | None |
| Mechanic | **Aerial Dive** (random raid 2k damage); **Wing Fluff** AoE (5-yard slow); **Whirlwind** every ~30s |
| Notable drops | **Tsu'zee's Owlfeather Cape** (rare cosmetic); **Eyepatch alt drop** *(Foror's? `[verify pass 3]`)* |

**Decision-engine rule:** Tsu'zee is highly RNG. Engine should not fail on "no Tsu'zee spawn" — proceed standard.

---

## Immol'thar Pylon System (Iconic Mechanic)

The defining DM-W feature: **Prince Tortheldrin maintains a magical shield around Immol'thar** (a captured shadow demon) via **5 generator pylons** scattered in side rooms.

### Pylon Configuration

| Pylon | Location | Notes |
|-------|----------|-------|
| **Generator 1** | Side room A (north) | Cleric guards |
| **Generator 2** | Side room B (south) | Highborne guards |
| **Generator 3** | Side room C (east) | Trash mob barriers |
| **Generator 4** | Side room D (west) | Trash mob barriers |
| **Generator 5** | Throne room | Final pylon (must be done with active fight or before) |

### Strategy A: Pylon-Smash (Easier — Most Groups)

| Step | Action | Effect |
|------|--------|--------|
| 1 | Raid clears Tendris/Illyanna/Kalendris | Standard progression |
| 2 | Raid splits to 5 pylon-rooms; **smash all 5 pylons** (~30s each) | Trash kills + 5 pylon destruction events |
| 3 | Immol'thar's shield drops; Immol'thar engages | Boss spawn |
| 4 | Defeat Immol'thar (high HP demon, physical attacks) | Boss kill |
| 5 | Engage Prince Tortheldrin; **Tortheldrin is weakened** (pylons fed his power) | Final boss |

### Strategy B: Tortheldrin-Rush (Harder — Less Common)

| Step | Action | Effect |
|------|--------|--------|
| 1 | Raid skips pylons | — |
| 2 | Raid engages Tortheldrin directly | Tortheldrin at full HP + Immol'thar still in shield |
| 3 | Defeat Tortheldrin | Shield drops on death; Immol'thar engages immediately |
| 4 | Defeat Immol'thar | Boss kill |
| **Net difficulty** | Harder Tortheldrin (full HP); easier Immol'thar (no shield power-up) |

**Decision-engine rule:** **Pylon-smash is preferred** for 95% of groups. Engine should default to pylon-clear sequence.

---

## Boss 5: Immol'thar (Captured Demon, post-pylon)

| Field | Value |
|-------|-------|
| HP | ~38k |
| Phases | None |
| Mechanic | **Doom Bolt** (random raid 4k damage, instant cast); **Heart of Night** debuff (DoT, dispel-needed); **physical melee** + **Aura of Suppression** (5-yard caster damage reduction); standard tank-and-spank with off-tank for adds |
| Notable drops | **Immol'thar's Lifeless Skull** (Warlock trinket); **Demonic Nightblade** (Rogue dagger); **Robe of Volatile Power** *(alt drop)* |

---

## Boss 6: Prince Tortheldrin (Final, Highborne Mage)

| Field | Value |
|-------|-------|
| HP | ~32k (full) / ~16k (after pylons smashed) |
| Phases | None |
| Mechanic | Caster boss — **Frostbolt Volley** (5-yard AoE on closest 5); **Polymorph** random raid CC; **Frost Nova** (5-yard immobilize); **Mana Shield** (50% damage absorbed); **Mind Control** (random raid possess) |
| Notable drops | **Foror's Eyepatch** *(Hunter trinket BiS, RNG drop)*; **Wushoolay's Charm of Spirits** *(Druid Resto trinket BiS-pre-raid)*; **Highborne Sash** (caster cloth belt); **Cloak of the Magic Lord** (caster cloak); **Druid epic chain Books** (rare drop) |

**Decision-engine rule:** Tortheldrin is the **final-boss with multiple BiS drop options**. Engine should plan multiple runs for class-specific BiS.

---

## Wushoolay's Charm of Spirits (Druid Resto BiS Trinket)

| Field | Value |
|-------|-------|
| Source | Prince Tortheldrin (rare drop, ~5%) |
| Stat | +24 Healing Power + Spirit + Spell Crit Chance proc |
| Use | **BiS Druid Resto trinket pre-raid** |
| BoP | Yes |

**Decision-engine rule:** Wushoolay's Charm is **the** Druid Resto trinket pre-raid. Engine should plan multiple DM-W runs for Druid Resto alts.

---

## Foror's Eyepatch (Alt Source)

| Field | Value |
|-------|-------|
| Source | Prince Tortheldrin (alt RNG, ~3%) — primary source is DM-N Tribute Chest |
| Stat | +20 Hit Rating + 5% crit; Hunter-only equip |
| Use | BiS Hunter trinket pre-raid |

**Decision-engine rule:** Foror's Eyepatch has **2 sources** (DM-N Tribute Chest primary + DM-W Tortheldrin alt). Engine should plan whichever is faster for Hunter alt acquisition.

---

## Druid Epic Chain Books

DM-W drops Druid epic chain books used in the Druid Pristine Hide chain extension:

| Book | Drop source |
|------|-------------|
| **Book of the Tomes** | Prince Tortheldrin (rare RNG, ~5%) |
| **Book of the Soul** | Cross-DM (DM-W primary, DM-N alt source) |

**Decision-engine rule:** Druid epic chain books are RNG drops. Engine should track per-character book status across DM-N/W/E.

---

## Shen'dralar Reputation

DM-W is the home of the **Shen'dralar** (Highborne elven scholars). Boss kills + outdoor mob kills give Shen'dralar rep.

| Tier | Reward |
|------|--------|
| Honored | Druid Eranikus chain quests; Highborne caster gear recipes |
| Revered | Top-tier rep recipes; Shen'dralar caster gear |
| Exalted | BiS rep-only Tailoring + LW recipes; Shen'dralar Provisioner Tablets |

**Decision-engine rule:** Shen'dralar rep = Druid Eranikus chain gateway. Engine should track for Druid alts pursuing Sunken Temple final boss.

---

## VMaNGOS / Server Reality Check

DM-W is **mostly scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Tendris Warpwood | Phase-1 magic-immune transition |
| Illyanna Ravenoak | Pet AI death-triggers-Frenzy state |
| Pylon-smash mechanic | Generator destruction sequence + Immol'thar shield drop |
| Prince Tortheldrin | Mana Shield absorption tracking; Polymorph AI |

**Decision-engine rule:** DM-W script-completeness is high. Engine should standard-pull. Pylon system specifically may have edge cases on shield-drop trigger.

---

## Decision-Engine Rules

1. **Pylon-smash strategy**: 95% of groups smash pylons before engaging Immol'thar. Engine should default to pylon-clear sequence.
2. **Tendris Phase-1 physical DPS**: spell-immune above 50% HP. Engine should encode physical-DPS-only invariant.
3. **Illyanna pet death**: kill pet first or simultaneously. Pet's death triggers Illyanna's Frenzy — encode coordinated kill.
4. **Kalendris Mind Control**: same as other DM/Strat MC bosses; DPS-rush possessed teammate.
5. **Wushoolay's Charm priority**: Druid Resto BiS trinket; engine should plan multiple runs for Druid alt drops.
6. **Foror's Eyepatch alt source**: ~3% drop from Tortheldrin; primary is DM-N Tribute Chest. Engine should prioritize whichever path faster for Hunter alts.
7. **Druid epic chain books**: RNG drops; engine should track per-character book status across DM wings.
8. **Shen'dralar rep**: Druid Eranikus chain gateway. Engine should always-pickup outdoor Shen'dralar quests on first DM-W visit.
9. **Tortheldrin Mana Shield**: 50% damage absorbed, recasted every 30s. Engine should track shield-up/shield-down state for DPS planning.
10. **Lockout-free**: DM-W has no instance reset; engine can run multiple back-to-back for trinket/book farming.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 56-60 entry gate
Snapshot.Class                                    // Druid Resto / Hunter / Warlock targeting
Snapshot.Inventory.Has("ForrorsEyepatch")         // Hunter trinket signal
Snapshot.Inventory.Has("WushoolaysCharm")         // Druid Resto trinket signal
Snapshot.Inventory.Has("RobeOfVolatilePower")     // Warlock chest signal
Snapshot.Inventory.{BookOfTomes, BookOfSoul}      // Druid chain book progress
Snapshot.RaidGroup.PylonSmashState                // 0-5 pylons smashed
Snapshot.Boss.Tendris.Phase                       // Phase 1 (50% HP) magic-immune signal
Snapshot.Boss.IllyannaPet.Killed                  // pet-coordination signal
Snapshot.Boss.Immolthar.Killed                    // post-pylon completion
Snapshot.Boss.PrinceTortheldrin.Killed            // final boss
Snapshot.Boss.Tortheldrin.ManaShieldActive        // shield-up state
Snapshot.RaidGroup.PossessedRaiders               // Kalendris/Tortheldrin MC tracking
Snapshot.Reputation.Shendralar                    // Druid Eranikus gateway
Snapshot.QuestLog.Active.EranikusChain            // Druid class chain
Snapshot.ServerCapabilities.DMWestBoss[<name>]    // VMaNGOS scripting flag
```

---

## Cross-References

- DM-N (sister, Tribute Run + Quel'Serrar): [dire-maul-north.md](dire-maul-north.md)
- DM-E (sister, Pusillin/Crescent Key): not yet covered (pending)
- Crescent Key chain (DM-East entry area, Pusillin): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key)
- Druid class (Wushoolay's Charm BiS Resto trinket + Eranikus chain): [../classes/druid.md](../classes/druid.md)
- Hunter class (Foror's Eyepatch alt source): [../classes/hunter.md](../classes/hunter.md)
- Warlock class (Robe of Volatile Power BiS): [../classes/warlock.md](../classes/warlock.md)
- Sunken Temple (Eranikus quest endpoint): [sunken-temple.md](sunken-temple.md)
- Feralas zone: [../sections/05-l40-l50.md](../sections/05-l40-l50.md#feralas-lvl-40-50)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [sunken-temple.md](sunken-temple.md), [stratholme-undead.md](stratholme-undead.md), [stratholme-live.md](stratholme-live.md), [scholomance.md](scholomance.md), [dire-maul-north.md](dire-maul-north.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
