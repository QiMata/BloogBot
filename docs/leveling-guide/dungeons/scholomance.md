---
title: "Dungeon — Scholomance"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Scholomance
crawl_date: 2026-05-01
---

# Scholomance (5-man) — Necromancer School + Skeleton Key + Headmaster Gandling

5-man dungeon in **Caer Darrow island** (Western Plaguelands; boat from Chillwind Camp/The Bulwark side). **Skeleton Key required** (long Argent Dawn chain). 11+ bosses culminating at **Darkmaster Gandling**. Sweet spot **lvl 58-60**. Notable drops: **Headmaster's Charge** (Mage caster staff, BiS pre-raid), **Skull of Burning Shadows** (Warlock trinket), **Ras Frostwhisper's Glove** (caster), **Slayer's Gauntlets** Hunter T0, **Postmaster's Cap** *(T0.5 helm, RNG)*. Dense set-piece farm.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 58-63 (optimal 60) |
| Lockout | None per-instance reset |
| Continent / Zone | Eastern Kingdoms — Western Plaguelands (Caer Darrow island, boat from Bulwark or Chillwind Camp area) |
| Faction | Both (cross-faction) |
| Theme | Cult of the Damned necromancer school |
| **Attune required** | **Skeleton Key** from Argent Dawn chain (Sacred Hammer of Light + Torch + Bonemeal + Necromancer Skull turn-ins) |
| Notable drops | **Headmaster's Charge** (Mage staff BiS pre-raid), **Skull of Burning Shadows** (Warlock trinket), **Ras Frostwhisper's Glove** (caster), **Slayer's Gauntlets** Hunter T0, **The Postmaster's Cap** T0.5 helm |
| Boss count | 11+ bosses across multiple wings |
| Special events | **Kirtonos summoning** (brazier event); **Ras Frostwhisper rescue** (Argent Dawn chain) |

---

## Skeleton Key Acquisition Chain

The outer gate of Scholomance is locked. Acquisition path:

| Step | Action | Source |
|------|--------|--------|
| 1 | Reach **Argent Dawn** Friendly+ at Light's Hope Chapel | EPL/WPL questing |
| 2 | Receive **"The Key to Scholomance"** quest from Magistrate Marduk Blackpool / Argent Dawn quartermaster | One-time chain |
| 3 | Collect **Sacred Hammer of Light** (Postmaster Malown drop, Strat-Live) | Strat-Live raid |
| 4 | Collect **Torch of Holy Flame** (Cathedral of Light, Stormwind / Org Spire equivalent) | Quest item |
| 5 | Collect **Premium Quality Bonemeal** (Cooking item from Scholo trash) | Pre-Scholo run |
| 6 | Collect **Necromancer's Skull** (Caer Darrow elite) | Outdoor zone |
| 7 | Combine into **Skeleton Key** (BoP) | Final assembly |
| 8 | Use Skeleton Key on Scholomance outer gate | Entry unlock |

**Decision-engine rule:** Skeleton Key chain spans Strat-Live + outdoor Caer Darrow + AD rep grind. Engine should plan ~3-5 hour acquisition window before first Scholo entry.

---

## Boss Order

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Kirtonos the Herald** *(summoned event, optional)* | Hard | Entry hall (post-summon) |
| 2 | **Jandice Barov** | Medium-Hard (clone mechanic) | Mage's Library |
| 3 | **Rattlegore** | Hard (skeletal warlord) | Storage |
| 4 | **Death Knight Darkreaver** *(rare)* | Medium | Side path |
| 5 | **Marduk Blackpool** + **Vectus** (paired) | Hard | Hall of Secrets |
| 6 | **Lord Alexei Barov** | Medium | Hall of Secrets / Storage |
| 7 | **Lady Illucia Barov** | Medium-Hard (caster MC) | Hall of Secrets / Library |
| 8 | **The Ravenian** | Medium | Catacombs |
| 9 | **Dr. Theolen Krastinov (the Butcher)** | Hard (bleed) | Catacombs / Lab |
| 10 | **Lorekeeper Polkelt** *(rare)* | Easy | Library |
| 11 | **Instructor Malicia** | Medium | Storage / Academy |
| 12 | **Ras Frostwhisper** | Hard (Argent Dawn rescue chain) | Catacombs (deeper) |
| 13 | **Darkmaster Gandling** *(final)* | Hard (multi-phase teleport) | Headmaster's Office |

**Decision-engine rule:** Scholo is **the densest 5-man** in vanilla 1.12 (11+ bosses). Engine should plan 2-3 hour full-clear runs.

---

## Boss 1: Kirtonos the Herald (Summoned Event)

**Optional opt-in mid-instance**. Located in entry hall.

### Summoning Mechanic

| Step | Action | Source |
|------|--------|--------|
| 1 | Locate **8 Brown Sons of Arugal** (entry-hall trash) and kill them | Pre-summon |
| 2 | Click **the Brazier of the Cult** (in entry hall, near gate) | Triggers summon |
| 3 | **Kirtonos the Herald** descends from balcony | Boss spawn |

### Encounter

| Field | Value |
|-------|-------|
| HP | ~32k |
| Phases | None |
| Mechanic | **Sleep Burst** AoE silence (3-yard radius); **Mind Numbing Poison** (caster damage reduction); summons **Skeleton Adds** periodically; tank-and-spank with off-tank for adds |
| Notable drops | **Kirtonos's Cloak** (caster); **Briarwood Reed** (rare caster trinket); **Skull of Burning Shadows** (Warlock trinket) |

**Decision-engine rule:** Kirtonos summon is **opt-in** but routine — 8-trash event takes ~5 minutes. Engine should always include in standard Scholo run.

---

## Boss 2: Jandice Barov (Clone Mechanic)

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Curse of Blood** (DoT, dispel-needed); **Banshee Strike** AoE; **clones herself** when below 50% HP — 4 illusion-Jandice copies; raid must identify the real one (highest HP) |
| Notable drops | **Jandice's Robe** (caster cloth chest); **Black Steel Bindings** (Warrior bracers); **Skull of Burning Shadows** *(Warlock trinket alt drop)* |

**Decision-engine rule:** clone mechanic = identify highest HP = real Jandice. Engine should encode HP-identification-on-clone-spawn.

---

## Boss 3: Rattlegore (Massive Skeletal Lord)

| Field | Value |
|-------|-------|
| HP | ~32k |
| Phases | **3 phases** by HP threshold |
| Mechanic | **Phase 1**: Tank-and-spank with **Cleave** + **Mortal Strike**; **Phase 2 (50% HP)**: spawns **Bone Spike Adds** (3 lvl 60 elites); **Phase 3 (20% HP)**: enrages with +30% attack speed; raid burst-DPS |
| Notable drops | **Rattlegore's Quill** (caster); **Battlechaser's Bracers** (Warrior tank); **Crown of Wrath** rare drop |

---

## Boss 4: Marduk Blackpool + Vectus (Paired Pull)

**Both bosses pull together** when raid enters Hall of Secrets.

| Boss | HP | Mechanic |
|------|-----|----------|
| **Marduk Blackpool** | ~26k | Caster — **Frostbolt Volley** (5-yard AoE on closest 5); **Polymorph** random raid (CC) |
| **Vectus** | ~28k | Physical — **Plague Cloud** AoE (3-yard, 1k damage); **Tank-cleave** with off-tank |

**Decision-engine rule:** Marduk + Vectus paired = 2-tank fight; encode tank assignments + Polymorph dispel.

---

## Boss 5: Lord Alexei Barov

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Death Coil** (random raid 2k damage + 6s self-fear); **Mortal Strike** stack on tank (50% healing reduction); off-tank rotation |
| Notable drops | **Alexei's Spectral Sword** (1H sword); **Black Iron Fishing Pole** (cosmetic? Or rare drop variant); **Death Knight Sabatons** (Plate boots) |

---

## Boss 6: Lady Illucia Barov (Caster MC)

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Mind Control** (random raid 8s); **Banshee Wail** AoE silence; **Curse of Tongues** (caster damage reduction, dispel-needed); raid spreads + DPS-rush possessed teammate |
| Notable drops | **Illucia's Robe** (caster cloth chest); **Banshee's Touch** (caster wand); **Necropile Robe** (Warlock cloth chest, rare) |

---

## Boss 7: The Ravenian (Storage)

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Sweeping Strike** (front-of-room cleave); **Mortal Strike** stack on tank; standard tank-and-spank |
| Notable drops | **Ravenian's Robe** (caster); **Heavy Spiked Plate** (Warrior tank chest); **Spinesnap Polearm** alt drop |

---

## Boss 8: Dr. Theolen Krastinov (the Butcher)

| Field | Value |
|-------|-------|
| HP | ~30k |
| Phases | None |
| Mechanic | **Bleed** stack on tank (DoT, escalates per stack — 5 stacks = lethal); **Cleave** (front-of-room); **Bandage required** if tank can't dispel; off-tank rotation |
| Notable drops | **Krastinov's Bag of Horrors** (Warrior trinket); **Butcher's Cleaver** (1H axe); **Doctor's Robe** (caster) |

**Decision-engine rule:** Krastinov bleed-stack = **tank-rotation** + **Anti-Venom usage** if available. Engine should encode Bleed stack-counter invariant.

---

## Boss 9: Lorekeeper Polkelt (rare)

| Field | Value |
|-------|-------|
| HP | ~24k |
| Phases | None |
| Mechanic | **Curse of Weakness** (damage reduction, dispel); **Polymorph** (random raid CC); standard tank-and-spank |
| Notable drops | **Polkelt's Quill** (caster wand); **Lorekeeper's Tunic** (caster cloth) |

---

## Boss 10: Instructor Malicia

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Holy Wrath** AoE (5-yard radius); **Holy Smite** burst; tank-and-spank |
| Notable drops | **Instructor's Robe** (caster); **Holy Mallet** (1H mace); **Malicia's Wand** caster wand |

---

## Boss 11: Ras Frostwhisper (Argent Dawn Rescue)

**Argent Dawn quest required** to escort the rescue chain. Located deep in catacombs.

### Pre-Quest

| Step | Action | Source |
|------|--------|--------|
| 1 | Acquire **The Soulshatter** quest from Argent Dawn Quartermaster (Light's Hope) | One-time chain |
| 2 | Locate Ras Frostwhisper's **chamber** in deep catacombs | Inside Scholo |
| 3 | Use **Soulshatter** consumable on Ras Frostwhisper to "save" his soul | Pre-engagement |
| 4 | Engage Ras Frostwhisper boss fight | Standard mechanic |

### Encounter

| Field | Value |
|-------|-------|
| HP | ~32k |
| Phases | None |
| Mechanic | **Frostbolt Volley** (5-yard AoE on closest 5 raiders); **Frost Nova** (5-yard immobilize); **Frost Resistance gear helps** (~50 FR per raider); spawns **Frost Adds** periodically |
| Notable drops | **Ras Frostwhisper's Glove** (caster); **Frost Tiger Blade** (1H sword); **Ras's Heart** rare trinket |

**Decision-engine rule:** Ras Frostwhisper Soulshatter mechanic = **AD chain quest item required** for full reward. Engine should track per-character Soulshatter usage.

---

## Boss 12: Darkmaster Gandling (Final)

| Field | Value |
|-------|-------|
| HP | ~36k |
| Phases | None (continuous teleport mechanic) |
| Mechanic | **Curse of Tongues** (caster damage reduction, dispel); **Shadow Shield** (200 damage absorbed, recasted every 30s); **teleports random raider** to mini-room (3 mini-rooms behind Headmaster's Office; teleported raider must defeat 2 lvl 60 ghosts to return); **caster boss** — interrupt rotation |
| Notable drops | **Headmaster's Charge** (Mage caster staff, **BiS pre-raid Mage staff**); **Necropile Mantle** (Warlock cloth shoulders); **Necropile Robe** (Warlock cloth chest); **Skull of Burning Shadows** (Warlock trinket alt drop); **Slayer's Gauntlets** (T0 Hunter hands, RNG) |

**Decision-engine rule:** Gandling teleport mechanic = **single-target raider sent to mini-room**. Engine should encode "teleported state" detection + 2-ghost-clear invariant before re-engagement.

---

## T0 / T0.5 Set Pieces (Per Class)

Scholo drops:

| Set piece | Class | Boss source |
|-----------|-------|-------------|
| **Slayer's Gauntlets** | Hunter (T0) | Darkmaster Gandling (RNG) |
| **Devout's Gloves** | Priest (T0) | Various Scholo bosses (RNG) |
| **Shadowcraft Tunic** | Rogue (T0) | Various |
| **Wildheart Vest** | Druid (T0) | Various |
| **Lightforge Helmet** | Paladin (T0) | Various |
| **The Postmaster's Cap** | T0.5 helm | Lady Illucia Barov (RNG) |
| **Heavy Necrotic Spaulders** | Plate-DPS | Marduk Blackpool / Vectus |

**Decision-engine rule:** Scholo is part of the **5-dungeon T0 rotation** (BRD/UBRS/Strat-Live/Strat-UD/Scholo + DM). Engine should track per-character T0 slot map across all 5 sources.

---

## Argent Dawn Reputation Sources

Scholo provides AD rep via:

| Source | Rep gain |
|--------|----------|
| Boss kills (per kill) | ~75-150 rep |
| Scourgestone drops (turn in at Light's Hope) | ~25 rep per turn-in |
| Necromancer Skull drops (turn in at Light's Hope) | ~50 rep per turn-in |
| Soulshatter Ras Frostwhisper completion | ~250 rep one-time |

**Decision-engine rule:** Scholo + Strat parallel = **fastest AD-Honored grind** in vanilla 1.12. Engine should always-loot Scourgestones + Necromancer Skulls.

---

## VMaNGOS / Server Reality Check

Scholo is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Kirtonos summon | 8-trash kill detection + Brazier interaction |
| Jandice Barov | Clone mechanic HP-identification AI |
| Rattlegore | 3-phase HP threshold transition |
| Lady Illucia Barov | Mind Control AI on possessed raider |
| Ras Frostwhisper | Soulshatter consumable interaction; Frost Add spawn timing |
| Darkmaster Gandling | Teleport-to-mini-room AI; teleported raider state tracking |

**Decision-engine rule:** Scholo script-completeness is high. Engine should standard-pull. Gandling teleport edge cases occur on some servers.

---

## Decision-Engine Rules

1. **Skeleton Key precondition**: required for entry. Engine should ensure `Snapshot.Inventory.Has("SkeletonKey") == true` pre-pull.
2. **Kirtonos summon**: opt-in but always-do. 8-trash + Brazier event takes ~5 min. Engine should encode summon sequence.
3. **Jandice clone identification**: 4-illusion mechanic. Engine should track real-Jandice via highest-HP-on-spawn.
4. **Krastinov bleed-stack**: tank-rotation when stack ≥ 5; bandage if dispel unavailable.
5. **Lady Illucia Mind Control**: same as Strat-Live Balnazzar / Strat-UD Anastari pattern. Engine should DPS-rush possessed teammate.
6. **Ras Frostwhisper Soulshatter**: AD chain quest required. Engine should track Soulshatter usage signal.
7. **Gandling teleport**: random raider sent to mini-room → 2 ghosts → return. Engine should encode "teleported state" + clear-and-return invariant.
8. **T0 acquisition**: per-character slot map across BRD/UBRS/Strat-Live/Strat-UD/Scholo/DM. Engine should track per-character set status.
9. **AD rep parallel**: Scholo + Strat is the AD-Honored grind path. Engine should always-loot Scourgestones + Necromancer Skulls.
10. **Headmaster's Charge priority**: Mage class drops; BoP. Engine should DKP/loot-priority for Mage class members.
11. **Lockout-free**: Scholo has no instance reset; engine can run multiple back-to-back for set-piece + rep farming.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 58-63 entry gate
Snapshot.Class                                    // T0 set + class trinket targeting
Snapshot.Inventory.Has("SkeletonKey")             // entry gate
Snapshot.Inventory.Has("HeadmastersCharge")       // Mage staff signal
Snapshot.Inventory.Has("SkullOfBurningShadows")   // Warlock trinket signal
Snapshot.Inventory.Has("RasFrostwhispersGlove")   // caster glove signal
Snapshot.Inventory.Has("PostmasterCap")           // T0.5 helm signal
Snapshot.Boss.KirtonosTheHerald.Killed            // summoned event completion
Snapshot.Boss.JandiceBarov.Killed                 // clone mechanic
Snapshot.Boss.RasFrostwhisper.Killed              // AD rescue
Snapshot.Boss.DarkmasterGandling.Killed           // final
Snapshot.QuestLog.Active.SoulshatterRas           // AD chain quest signal
Snapshot.Inventory.Has("Soulshatter")             // Ras consumable signal
Snapshot.Reputation.ArgentDawn                    // Honored gate
Snapshot.Inventory.Scourgestones                  // AD rep turn-in stockpile
Snapshot.Inventory.NecromancerSkull               // Skeleton Key reagent + AD rep
Snapshot.RaidGroup.Composition.Tanks              // 1-2 tank for Marduk+Vectus / Krastinov bleed
Snapshot.RaidGroup.GandlingTeleportedState        // mini-room raider tracking
Snapshot.ServerCapabilities.ScholoBoss[<name>]    // VMaNGOS scripting flag
```

---

## Cross-References

- Strat-Live (sister T0 source, Sacred Hammer of Light reagent): [stratholme-live.md](stratholme-live.md)
- Strat-UD (sister T0 source): [stratholme-undead.md](stratholme-undead.md)
- Argent Dawn rep (AD Honored grind): [../reputations/argent-dawn.md](../reputations/argent-dawn.md)
- Naxx attune chain (AD Honored gate): [../attunements/naxxramas.md](../attunements/naxxramas.md)
- Mage class (Headmaster's Charge BiS): [../classes/mage.md](../classes/mage.md)
- T0 progression: [../classes/](../classes/) (per-class file)
- Western Plaguelands zone (Caer Darrow boat): [../sections/06-l50-l60.md](../sections/06-l50-l60.md#western-plaguelands-lvl-51-58)
- L50-L60 bracket: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [sunken-temple.md](sunken-temple.md), [stratholme-undead.md](stratholme-undead.md), [stratholme-live.md](stratholme-live.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
