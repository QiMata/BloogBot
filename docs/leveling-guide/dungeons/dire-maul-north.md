---
title: "Dungeon — Dire Maul: North (Gordok Commons)"
patch: "1.12.1 (Drums of War, Sept 2006); Dire Maul released in 1.3 'Ruins of the Dire Maul' (Mar 2005)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Dire_Maul
crawl_date: 2026-05-01
---

# Dire Maul: North (5-man) — Tribute Run + King Gordok + Quel'Serrar

5-man dungeon in Feralas (Dire Maul ruins). 7 bosses culminating at **King Gordok**. Sweet spot **lvl 56-60**. **The Tribute Run mechanic** is the iconic feature: leave lieutenants alive when entering King Gordok area for stat buffs + access to BoP **Tribute Chest** rare drops. Other key features: **Quel'Serrar Warrior class chain** (Onyxia-type dragon turn-in), **Foror's Eyepatch** Hunter trinket, Eldreth ring sets, **Druid epic chain books**.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 56-60 (optimal 58-60) |
| Lockout | None per-instance reset |
| Continent / Zone | Kalimdor — Feralas (Dire Maul ruins, north section) |
| Faction | Both (cross-faction) |
| Theme | Gordok Ogre tribe in ancient elven ruins |
| Notable drops | **Tribute Chest** rare BoP loot (lieutenant-conditional), **Foror's Eyepatch** Hunter trinket, Eldreth ring sets, Druid epic chain books, **Quel'Serrar** Warrior 1H sword (chain reward) |
| Boss count | 7 (5 lieutenants + Cho'Rush + King Gordok) |
| Special mechanic | **Tribute Run** (leave lieutenants alive for buffs + chest) |

---

## Boss Order (Standard "Kill-Everything" Run)

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Guard Mol'dar** (lieutenant) | Medium | Entry hall |
| 2 | **Stomper Kreeg** (lieutenant) | Easy | Stomper's Bar |
| 3 | **Guard Fengus** (lieutenant) | Hard (Ogre Magi-tier) | Foyer |
| 4 | **Guard Slip'kik** (lieutenant) | Medium | Mid-instance |
| 5 | **Captain Kromcrush** (lieutenant) | Hard (Ogre Lord-tier) | Throne Room |
| 6 | **Cho'Rush the Observer** | Hard (paired with King) | Throne Room |
| 7 | **King Gordok** (final) | Hard | Throne Room |

**Decision-engine rule:** standard kill-everything run = 7 bosses across ~90 minutes. Tribute Run skips lieutenants for chest-specific rewards.

---

## The Tribute Run (Iconic Mechanic)

The DM-N Tribute Run is the **defining mechanic** of vanilla 1.12 5-mans.

### How It Works

1. Raid enters DM-N
2. Raid **stealths past 5 lieutenants** (or uses Invisibility Potions / Druid Stealth + Cat) — does NOT engage them
3. Raid pulls King Gordok directly (with Cho'Rush)
4. Defeat King Gordok + Cho'Rush
5. Each lieutenant left alive grants a **2-hour stat buff** (collected by clicking the chest):

| Lieutenant | Buff if alive |
|-----------|---------------|
| **Guard Mol'dar** | **Mol'dar's Moxie** — +30 Stamina (2h) |
| **Stomper Kreeg** | **Kreeg's Soothing Charm** — Heal cost reduction (50% reduction in mana cost on heals, 2h) |
| **Guard Fengus** | **Fengus's Ferocity** — +200 Attack Power (2h) |
| **Guard Slip'kik** | **Slip'kik's Savvy** — +5% Spell Crit (2h) |
| **Captain Kromcrush** | (no buff but King's HP reduced significantly) |

6. **Tribute Chest** spawns next to King Gordok's body
7. Chest opens with BoP rare drops based on lieutenants left alive (more lieutenants = better chest):

| Chest tier | Lieutenants alive | Reward range |
|-----------|-------------------|--------------|
| **0/5** | All killed | Standard rare drops only |
| **3/5** | Mid-tier | Common BoP rare drops |
| **5/5** | All alive (max tribute) | **Rare BoP epics**: Tribute Helm, Tribute Ring, Tribute Trinket, **Foror's Eyepatch** (Hunter), Druid books, **Quel'Serrar** chain item |

**Decision-engine rule:** Tribute Run is **the** DM-N mandatory pattern at L60. Engine should encode lieutenant-skip path (stealth + Inv-pot + Druid Cat Form) + King-Cho'Rush-only kill sequence.

---

## "Bow to the King" Ritual

After killing King Gordok with full Tribute Run:

| Step | Action |
|------|--------|
| 1 | Raid clicks King Gordok's corpse | Triggers "King" status quest acceptance |
| 2 | Raid is proclaimed "Kings of Gordok" by NPC ogres | 2-hour buff + access to Stomper Kreeg's Bar |
| 3 | All raiders gain 2-hour **"Mol'dar's Moxie + ... + Slip'kik's Savvy"** buff stack | Combined raid buff |
| 4 | Raiders can now access **Stomper Kreeg's Bar** (alcohol vendor for free Goblin alcoholic brews — 2h drunk debuff trade-off) | Cosmetic |
| 5 | Raiders can pickup **"The Affray" Quel'Serrar quest** if Warrior in raid | Class chain unlock |

**Decision-engine rule:** "Bow to the King" requires Tribute Run completion. Engine should encode this as a Tribute Run end-state ritual.

---

## Boss 1: Guard Mol'dar (lieutenant 1)

| Field | Value |
|-------|-------|
| HP | ~22k |
| Phases | None |
| Mechanic | Standard tank-and-spank with **Cleave** + **Mortal Strike** stack on tank; off-tank rotation |
| Notable drops | **Mol'dar's Moxie** buff (if alive at King); **Heart of the Pridelord** (cosmetic); **Vigorsteel Vambraces** Warrior |

---

## Boss 2: Stomper Kreeg (lieutenant 2)

| Field | Value |
|-------|-------|
| HP | ~18k |
| Phases | None |
| Mechanic | Drinks ale during fight (heal-buff Kreeg); raid interrupt + DPS-rush before drink completes |
| Notable drops | **Kreeg's Soothing Charm** buff (if alive); **Stomper Kreeg's Mossy Tunic** (cloth cosmetic); **Pungent Mug** (cosmetic) |

---

## Boss 3: Guard Fengus (lieutenant 3)

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Spellsteal** debuff on caster raiders (DoT, dispel-needed); **Multi-Shot** AoE physical; **Hard-hitting cleave** — off-tank rotation |
| Notable drops | **Fengus's Ferocity** buff (if alive); **Vigor Pads** (Warrior); **Ironweave Robe** (cloth, mid-tier) |

---

## Boss 4: Guard Slip'kik (lieutenant 4)

| Field | Value |
|-------|-------|
| HP | ~22k |
| Phases | None |
| Mechanic | **Cleave** + **Mortal Strike** + **Knockback** AoE; standard tank-and-spank |
| Notable drops | **Slip'kik's Savvy** buff (if alive); **Slip'kik's Tunic** (cloth); **Eldreth Vambraces** (mid-tier caster) |

---

## Boss 5: Captain Kromcrush (lieutenant 5)

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Berserker Charge** (random raid 5-yard knockback + 3k damage); **Cleave**; **Mortal Strike** stack on tank |
| Notable drops | **Captain Kromcrush's Chestplate** (Plate-DPS); **Bone Sphere** (Warrior trinket); **Ironweave Pants** (cloth) |

---

## Boss 6: Cho'Rush the Observer

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None (paired with King in throne room) |
| Mechanic | Caster — **Mind Control** (random raid 8s); **Frost Bolt** burst; **Heal cast** on King (interruptible — DPS interrupt rotation); **Polymorph** random raid CC |
| Notable drops | **Cho'Rush's Tunic** (cloth caster); **Observer's Hat** (cosmetic); **Cho'Rush's Stinger** (caster wand) |

**Decision-engine rule:** Cho'Rush + King paired pull = 2-tank fight with interrupt rotation. Engine should encode interrupt-on-Heal-cast invariant.

---

## Boss 7: King Gordok (Final)

| Field | Value |
|-------|-------|
| HP | ~36k (reduced significantly if Captain Kromcrush left alive — adds tribute mechanic) |
| Phases | None |
| Mechanic | **Mortal Strike** stack on tank; **Cleave** front-of-room; **Knockback** AoE; **Berserker Rage** at 25% HP (+30% attack speed); off-tank rotation |
| Notable drops | **Tribute Chest** rare loot (varies by Tribute Run completeness); **King Gordok's Crown** (Plate helm, mid-tier); **Hunter polearm** (mid); **Royal Sash** Warrior tank |

**Decision-engine rule:** King Gordok HP reduction depends on Tribute Run lieutenants left. Engine should encode HP-scale-by-tribute-state.

---

## Quel'Serrar Warrior Class Chain

**Quel'Serrar** is a Warrior epic 1H sword with a long quest chain culminating in DM-N.

### Chain Summary

| Step | Action | Source |
|------|--------|--------|
| 1 | Pickup **"The Test of Skulls"** at Warrior trainer (capital city) | Lvl 60 prerequisite |
| 2 | Acquire **Foror's Compendium of Dragon Slaying** (drop from various dragonkin in Burning Steppes / Hinterlands) | Outdoor farming |
| 3 | Travel to **DM-North** + complete **Tribute Run** (Mol'dar/Kreeg/Fengus/Slip'kik/Kromcrush all alive) | DM-N |
| 4 | After King Gordok kill + Tribute Chest spawn, **Quel'Serrar quest item** drops in chest | Tribute reward |
| 5 | Tablet must be turned in to a special NPC at top of DM | Specific waypoint |
| 6 | Summon **Onyxia-type dragon** at the top of DM (rocky outcrop) | Specific event |
| 7 | Defeat the dragon (lvl 60 quest event boss) | Boss kill |
| 8 | Receive **Quel'Serrar** (epic 1H sword) | Final reward |

**Decision-engine rule:** Quel'Serrar chain is **the** Warrior class epic at L60. Engine should pre-flag Warrior alts for Tribute Run path + dragon farming for Compendium.

---

## Foror's Eyepatch (Hunter Quest Trinket)

| Field | Value |
|-------|-------|
| Source | DM-N Tribute Chest (rare drop from full Tribute Run) |
| Use | **Foror's Eyepatch** is Hunter-only equip; +20 Hit Rating + +5% crit; BiS Hunter trinket pre-raid |
| Drop rate | ~5% from Tribute Chest |

**Decision-engine rule:** Foror's Eyepatch is **the** Hunter pre-raid trinket. Engine should plan multiple Tribute Runs for Hunter alt drops.

---

## Eldreth Ring Sets

The DM exterior + DM-N drops several Eldreth ring sets:

| Set | Pieces | Class |
|-----|--------|-------|
| **Eldreth Caster Ring** | Single ring | Caster (Mage/Wlk/Pri) |
| **Eldreth Sorcerer Ring** | Single ring | Caster (Mage) |
| **Eldreth Defender Ring** | Single ring | Plate-DPS (Warrior/Pal) |
| **Eldreth Berserker Ring** | Single ring | Plate-DPS / Hunter |

**Decision-engine rule:** Eldreth rings are mid-tier alternatives to T0.5 ring options. Engine should track per-character ring slot.

---

## Druid Epic Chain Books

DM-N drops Druid quest items used in the **Druid Pristine Hide / Pristine Hide of the Beast** chain (alternate path):

| Book | Drop | Use |
|------|------|-----|
| **Books of the Beasts** | DM-N Tribute Chest (rare) | Druid form-helm Tribal LW component |
| **Books of the Soul** | Cross-DM | Druid epic chain piece |
| **Books of the Tomes** | Cross-DM (DM-W primary) | Druid epic chain piece |

**Decision-engine rule:** Druid books are rare drops; Druid alts should track all 3 books across DM-N/W/E.

---

## VMaNGOS / Server Reality Check

DM-N is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Tribute Run path | Stealth-detection by lieutenants (skip-pull mechanic) |
| Stomper Kreeg | Drink-interrupt mechanic |
| Cho'Rush | Heal-cast interrupt detection |
| King Gordok | HP-scale-by-tribute-state |
| Tribute Chest | Lieutenant-state-tracking + chest reward variant |

**Decision-engine rule:** DM-N script-completeness is high. Engine should standard-pull. Tribute mechanic specifically may have edge cases on stealth-detection.

---

## Decision-Engine Rules

1. **Tribute Run vs Kill-Everything**: pre-determine based on goals:
   - L60 Warrior with Quel'Serrar chain → Tribute Run mandatory
   - L60 Hunter farming Foror's Eyepatch → Tribute Run preferred
   - General loot farming → Kill-Everything maximum drops
2. **Stealth/Inv-Pot path planning**: Tribute Run requires sneaking past lieutenants. Engine should encode Stealth waypoints + Invisibility Potion usage.
3. **2-hour buff stack**: Tribute Run completion grants 4 buffs (Mol'dar, Kreeg, Fengus, Slip'kik). Engine should track buff expiration for raid timing.
4. **Quel'Serrar chain priority**: Warrior class epic; engine should pre-flag chain quest active.
5. **Foror's Eyepatch rate**: ~5% drop. Engine should plan 20+ Tribute Runs for Hunter alts.
6. **Eldreth rings**: mid-tier alternatives across DM-N/W/E. Engine should round-robin DM wings for ring set completion.
7. **Druid books cross-tracking**: 3 books across DM-N/W/E for Druid epic chain. Engine should track per-character book status.
8. **Cho'Rush + King paired pull**: 2-tank fight + interrupt rotation. Engine should encode tank assignments.
9. **Bow ritual**: post-Tribute, raid clicks corpse for "King of Gordok" buff stack. Engine should auto-loot + auto-bow.
10. **Lockout-free**: DM-N has no instance reset; engine can run multiple Tribute Runs back-to-back.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 56-63 entry gate
Snapshot.Class                                    // Warrior Quel'Serrar / Hunter Foror's bias
Snapshot.PartyComposition.{Tank, Healer, DPS}     // 5-man standard
Snapshot.QuestLog.Active.QuelSerrarChain          // Warrior class epic
Snapshot.QuestLog.Active.DruidEpicBooks           // Druid chain progress
Snapshot.Inventory.Has("ForrorsEyepatch")         // Hunter BiS trinket signal
Snapshot.Inventory.Has("QuelSerrar")              // Warrior epic 1H signal
Snapshot.Inventory.{BookOfBeasts, BookOfSoul, BookOfTomes}  // Druid chain books
Snapshot.RaidGroup.TributeRunState                // 0-5 lieutenants alive at King engagement
Snapshot.Buffs.MoldarsMoxie                       // +30 Sta tribute buff
Snapshot.Buffs.KreegsSoothingCharm                // -50% heal mana cost
Snapshot.Buffs.FengussFerocity                    // +200 AP
Snapshot.Buffs.SlipkiksSavvy                      // +5% spell crit
Snapshot.Inventory.InvisibilityPotion             // stealth-skip mat
Snapshot.Equipment.Ring1.Has("Eldreth*")          // ring set tracking
Snapshot.ServerCapabilities.DMNorthBoss[<name>]   // VMaNGOS scripting flag
```

---

## Cross-References

- DM-W (sister, Tendris/Ravenoak/Tortheldrin): not yet covered (pending)
- DM-E (sister, Pusillin/Lethtendris/Alzzin/Crescent Key): not yet covered (pending)
- Crescent Key chain (DM East Pusillin): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key)
- Warrior class (Quel'Serrar BiS 1H): [../classes/warrior.md](../classes/warrior.md)
- Hunter class (Foror's Eyepatch BiS trinket): [../classes/hunter.md](../classes/hunter.md)
- Druid class (epic chain books): [../classes/druid.md](../classes/druid.md)
- Feralas zone (DM ruins): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#feralas-lvl-40-50)
- L50-L60 bracket: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [sunken-temple.md](sunken-temple.md), [stratholme-undead.md](stratholme-undead.md), [stratholme-live.md](stratholme-live.md), [scholomance.md](scholomance.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
