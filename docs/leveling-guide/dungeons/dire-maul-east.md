---
title: "Dungeon — Dire Maul: East (Warpwood Quarter)"
patch: "1.12.1 (Drums of War, Sept 2006); Dire Maul released in 1.3 'Ruins of the Dire Maul' (Mar 2005)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Dire_Maul
crawl_date: 2026-05-01
---

# Dire Maul: East (5-man) — Pusillin + Lethtendris + Alzzin + Felvine Shard

5-man dungeon in Feralas (Dire Maul ruins, east wing). 5 bosses culminating at **Alzzin the Wildshaper** (corrupted satyr lord). Sweet spot **lvl 56-60**. **The entrance area features Pusillin** — the satyr who drops the **Crescent Key** (Sunken Temple gate). Notable drops: **Felvine Shard** (Warlock Dreadsteed class chain reagent — must enter DM-E to acquire), **Demonshear** Warrior tank shield, Druid epic chain Books, Hunter/Rogue mid-tier gear. Iconic for being the **DM-E entry-area Pusillin chase + Crescent Key**.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 56-60 (optimal 58-60) |
| Lockout | None per-instance reset |
| Continent / Zone | Kalimdor — Feralas (Dire Maul ruins, east wing) |
| Faction | Both (cross-faction) |
| Theme | Corrupted satyr + warpwood ents |
| Notable drops | **Felvine Shard** (Warlock Dreadsteed reagent), **Demonshear** Warrior tank shield, **Wildshaper's Wraps** Druid Feral leather, **Robe of Crystal** caster cloth, **Cape of Impulsiveness** Hunter cape, Druid epic chain Books |
| Boss count | 5 (Pusillin entrance + Lethtendris + Hydrospawn + Zevrim Thornhoof + Alzzin) |
| Special features | **Pusillin chase** at entrance area (Crescent Key origin), **Felvine Shard** drop for Warlock chain |

---

## Pre-Entry: Pusillin Chase (Crescent Key Origin)

**Pusillin** is a lvl 44 elite satyr **outside** DM-E entrance. He runs a fixed loop and is the gateway to the **Crescent Key** (Sunken Temple attune).

### Chase Mechanic (covered iter 38)

| Step | Action | Source |
|------|--------|--------|
| 1 | Find Pusillin in DM East entrance area (outdoor) | Lvl 44+ approach |
| 2 | Pusillin runs in 5-stop loop | Per-spawn |
| 3 | At each stop, Pusillin asks for items he wants — hand them or DPS-rush him to 30% HP | Trial-and-error |
| 4 | At final stop, Pusillin gives **Crescent Key** + Pusillin's Letter | Reward |
| 5 | Use Crescent Key on Sunken Temple outer gate | ST entry unlock |

**Decision-engine rule:** Pusillin chase is **Sunken Temple attune chain step 1**. Engine should always-pickup at L48+ before ST queue.

See [05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key) for full chain.

---

## Boss Order (Inside DM-East)

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Lethtendris + Pimgib** (paired Succubus + Imp) | Medium | Entry chamber |
| 2 | **Hydrospawn** | Medium-Hard (water AoE) | Pool chamber |
| 3 | **Zevrim Thornhoof** | Hard (satyr mid-boss) | Throne path |
| 4 | **Alzzin the Wildshaper** (final) | Hard (form-shifter) | Throne Room |

**Decision-engine rule:** DM-E is the **shortest** of the 3 DM wings. ~45-60 min full clear.

---

## Boss 1: Lethtendris + Pimgib (Paired Pull)

**Lethtendris is a Succubus boss** with her **Imp pet Pimgib**. Both pull together.

| Boss | HP | Mechanic |
|------|-----|----------|
| **Lethtendris** | ~24k | Caster — **Shadow Bolt Volley** (5-yard AoE on closest 5 raiders); **Mind Numbing Poison** (caster damage reduction); **Whirlwind** every 30s (raid stays out); **Possess** random raid (Mind Control 8s — DPS-rush possessed teammate) |
| **Pimgib** (Imp pet) | ~10k | Trash-tier; Imp throws **Fireballs**; raid AoE clears |

**Notable drops:**
- **Pimgib's Collar** (caster trinket — chance to summon Imp pet on use)
- **Lethtendris's Wand** (caster wand)
- **Felvine Shard** *(rare drop — Warlock Dreadsteed reagent)*
- **Robe of Volatile Power** (Warlock chest, alt source)

**Decision-engine rule:** Lethtendris + Pimgib paired = 2-target raid; off-tank picks up Pimgib while tank holds Lethtendris.

---

## Boss 2: Hydrospawn (Water Elemental in Pool)

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None (continuous AoE) |
| Mechanic | Lives in a pool of water in mid-room; **Water Bubbling** AoE (3-yard radius around Hydrospawn — 1.5k frost damage per pulse); **Frost Bolt** burst; **Frost Resistance gear helps** (~30 FR per raider); raid stays out of pool perimeter |
| Notable drops | **Robe of Crystal** (caster cloth chest); **Hydrospawn's Embrace** (Warrior tank ring); **Water Spire** (caster trinket — rare) |

**Decision-engine rule:** Hydrospawn pool perimeter = encode 3-yard exclusion zone. Raid melees on far side, casters on safe distance.

---

## Boss 3: Zevrim Thornhoof (Satyr Mid-Boss)

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Sacrifice** — random raid teleported to altar where Zevrim continues damaging them (3k damage every 5s) until released; raid must DPS Zevrim down to release sacrifice or rescue with magic dispel; **Demonic Frenzy** (+30% attack speed at 30% HP) |
| Notable drops | **Wildshaper's Wraps** (Druid Feral leather bracers); **Mantle of the Wildshaper** (Druid Feral leather shoulders); **Zevrim's Hoof** (cosmetic); **Cape of Impulsiveness** (Hunter mid-tier cape) |

**Decision-engine rule:** Sacrifice mechanic = **DPS-Zevrim-priority** to release teleported raider. Engine should encode "sacrificed state" detection + DPS-Zevrim invariant.

---

## Boss 4: Alzzin the Wildshaper (Final, Form-Shifter)

| Field | Value |
|-------|-------|
| HP | ~32k |
| Phases | **3 phases** (Demon → Wolf → Boar form transitions) |
| Mechanic | **Phase 1 (100-66% HP)**: Demon form — caster + **Shadow Bolt Volley** + **Mind Control**; **Phase 2 (66-33% HP)**: Wolf form — physical melee + **Cleave** + **Mortal Strike**; **Phase 3 (33% HP-0%)**: Boar form — **Charge** AoE + **Trample** + **Berserker Rage**; raid adapts to phase mechanics |
| Notable drops | **Felvine Shard** *(higher drop rate, Warlock Dreadsteed reagent)*; **Demonshear** (Warrior tank shield); **Wildshaper's Helm** (Druid form-helm); **Hunter's Pursuit** (Hunter mail belt); **Druid epic chain Book** (rare drop) |

**Decision-engine rule:** Alzzin 3-phase = encode form-transition timing for DPS allocation. Wolf-Phase melee + Boar-Phase ranged spreads.

---

## Felvine Shard (Warlock Dreadsteed Reagent)

| Field | Value |
|-------|-------|
| Source | Lethtendris (rare drop, ~3%) + Alzzin (higher rate, ~10%) |
| Use | **Warlock Dreadsteed quest chain reagent** — combine with other components for Dreadsteed summon |
| BoP | Yes — Warlock-only equip |
| Multi-stage | DM-E Felvine Shard + Scholomance Krastinov Bag of Horrors + Felwood demonologist NPC = full Dreadsteed chain |

**Decision-engine rule:** Felvine Shard is **the** Warlock class quest reagent at L60. Engine should plan multiple DM-E runs for Warlock alts pursuing Dreadsteed.

See [../classes/warlock.md](../classes/warlock.md) for full Dreadsteed chain.

---

## Druid Epic Chain Books (DM-E variant)

DM-E drops Druid quest items used in the Druid Pristine Hide chain extension:

| Book | Drop source |
|------|-------------|
| **Book of the Beasts** | Alzzin (rare RNG, ~5%) |
| **Demon Stalker Cape** | Cross-faction loot (rare) |

**Decision-engine rule:** Book of the Beasts is the DM-E specific Druid book; engine should track per-character book status.

---

## VMaNGOS / Server Reality Check

DM-E is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Pusillin chase | Outdoor 5-stop loop AI; trade-detection at each stop |
| Lethtendris | Pimgib pet AI; Possess MC AI |
| Hydrospawn | Pool AoE radius scaling; Frost damage tick rate |
| Zevrim Thornhoof | Sacrifice teleport mechanic + altar damage tick |
| Alzzin the Wildshaper | 3-phase form-shift transition AI |

**Decision-engine rule:** DM-E script-completeness is high. Engine should standard-pull. Pusillin chase is the most-fragile script area historically.

---

## Decision-Engine Rules

1. **Pusillin chase priority**: at L48+, always pickup Crescent Key on first DM-E entrance approach.
2. **Lethtendris + Pimgib paired pull**: 2-target raid; off-tank picks up Pimgib.
3. **Hydrospawn pool perimeter**: 3-yard exclusion zone. Raid stays out.
4. **Zevrim Sacrifice mechanic**: DPS-Zevrim-priority to release teleported raider.
5. **Alzzin 3-phase form-shift**: encode phase transitions for DPS allocation (Demon → Wolf → Boar).
6. **Felvine Shard farming**: ~3% Lethtendris / ~10% Alzzin. Engine should plan 5-10 runs for Warlock Dreadsteed acquisition.
7. **Druid Book of the Beasts**: rare drop; engine should track for Druid alts pursuing epic chain.
8. **Cross-DM coordination**: DM-N (Tribute Run) + DM-W (Pylon mechanic) + DM-E (this) form 3-wing trio. Engine should plan rotation across all 3 for class quest farming.
9. **Lockout-free**: DM-E has no instance reset; engine can run multiple back-to-back.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 56-60 entry gate
Snapshot.Class                                    // Warlock / Druid / Warrior targeting
Snapshot.Inventory.Has("CrescentKey")             // Pusillin chase outcome (ST attune)
Snapshot.Inventory.Has("FelvineShard")            // Warlock Dreadsteed reagent signal
Snapshot.Inventory.Has("Demonshear")              // Warrior tank shield signal
Snapshot.Inventory.Has("BookOfBeasts")            // Druid epic chain book signal
Snapshot.QuestLog.Active.DreadsteedChain          // Warlock class chain progress
Snapshot.QuestLog.Active.PusillinChase            // Crescent Key pre-quest signal
Snapshot.Boss.Lethtendris.Killed                  // paired pull completion
Snapshot.Boss.Hydrospawn.Killed                   // pool boss completion
Snapshot.Boss.AlzzinTheWildshaper.Killed          // final boss + 3-phase completion
Snapshot.RaidGroup.SacrificedRaiders              // Zevrim sacrifice tracking
Snapshot.ServerCapabilities.DMEastBoss[<name>]    // VMaNGOS scripting flag
```

---

## Cross-References

- DM-N (sister, Tribute Run + Quel'Serrar): [dire-maul-north.md](dire-maul-north.md)
- DM-W (sister, Immol'thar pylons + Wushoolay's): [dire-maul-west.md](dire-maul-west.md)
- Crescent Key chain (Pusillin pre-quest): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key)
- Sunken Temple (Crescent Key destination): [sunken-temple.md](sunken-temple.md)
- Warlock class (Felvine Shard for Dreadsteed): [../classes/warlock.md](../classes/warlock.md)
- Druid class (epic chain books): [../classes/druid.md](../classes/druid.md)
- Feralas zone (DM ruins): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#feralas-lvl-40-50)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [sunken-temple.md](sunken-temple.md), [stratholme-undead.md](stratholme-undead.md), [stratholme-live.md](stratholme-live.md), [scholomance.md](scholomance.md), [dire-maul-north.md](dire-maul-north.md), [dire-maul-west.md](dire-maul-west.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
