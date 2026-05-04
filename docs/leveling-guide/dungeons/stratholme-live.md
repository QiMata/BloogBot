---
title: "Dungeon — Stratholme: Live Side (Scarlet Crusade)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Stratholme_(Classic)
crawl_date: 2026-05-01
---

# Stratholme: Live (5-man) — Scarlet Crusade + Postmaster Mailbox Event + Balnazzar

5-man dungeon, **west side** of Stratholme city in Eastern Plaguelands. Counter-half to Strat-UD; runs from west gate. **Scarlet Crusade themed** with **Postmaster Malown letter event** as iconic mechanic. ~7 bosses culminating at **Balnazzar** (dreadlord). Sweet spot **lvl 58-60**. Notable drops: **T0 dungeon set pieces** (Devout/Shadowcraft/Wildheart/Lightforge — Priest/Rogue/Druid/Paladin), **Postmaster's Tunic + Robe** (T0.5 chest pieces), **Slayer's Helm** Hunter, **Cannon Master's Hatchet** 1H axe.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 58-63 (optimal 60) |
| Lockout | None per-instance reset |
| Continent / Zone | Eastern Kingdoms — Eastern Plaguelands (Stratholme west gate) |
| Faction | Both (cross-faction) |
| Theme | Scarlet Crusade (former Lordaeron military) + Cult of the Damned |
| Entry | **West gate** (Live side) — separate from UD entrance east gate |
| Notable drops | T0 dungeon set pieces (Devout/Shadowcraft/Wildheart/Lightforge), **Postmaster's Tunic + Robe** T0.5, **Slayer's Helm** Hunter, **Cannon Master's Hatchet** 1H axe, **Hearthsinger's Lyric** caster wand |
| Boss count | 7 bosses (Magistrate Barthilas → Hearthsinger → Cannon Master → Timmy/Malor → Postmaster → Balnazzar) |
| Side events | **Postmaster Malown letter-mailbox event** (4 letters scattered) |

---

## Boss Order

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Magistrate Barthilas** *(shared with UD side)* | Medium (special opener — runs to wall) | Western entrance |
| 2 | **Skul** *(rare elite, optional)* | Easy | King's Square |
| 3 | **Hearthsinger Forresten** *(rare elite, optional)* | Easy-Medium | Market Row |
| 4 | **Timmy the Cruel** | Medium-Hard (cleave) | Market Row |
| 5 | **Cannon Master Willey** | Hard (cannon kite mechanic) | Crusader Square / Bastion |
| 6 | **Malor the Zealous** *(rare in some patches)* | Medium | Mid-instance |
| 7 | **Crimson Hammersmith** *(rare elite, optional)* | Easy-Medium | Side path |
| 8 | **Archivist Galford** | Medium | Library section |
| 9 | **Postmaster Malown** *(triggered via 4-letter event)* | Hard (multi-wave) | Postmaster's Office |
| 10 | **Balnazzar** | Hard (final dreadlord) | Crimson Throne (final) |

**Decision-engine rule:** Strat-Live is the **post-raid T0 farming target** for healer/caster/Druid Resto/Pal Holy classes. Hunter T0 ("Slayer's") drops here too. Engine should plan multiple weekly runs for set-piece farming.

---

## Boss 1: Magistrate Barthilas (Shared — Both Sides)

**Same boss as Strat-UD side** — see [stratholme-undead.md#boss-1-magistrate-barthilas-special-opener](stratholme-undead.md#boss-1-magistrate-barthilas-special-opener). Only kills once per instance reset.

**Decision-engine rule:** Barthilas is a one-time kill regardless of which side entered first. Engine should track `Snapshot.Boss.Barthilas.AlreadyKilled` per instance.

---

## Boss 2: Skul (rare optional)

| Field | Value |
|-------|-------|
| HP | ~22k |
| Phases | None |
| Mechanic | Skeletal mage; **Frost Nova** + **Frostbolt** combo; raid spreads + ranged DPS |
| Notable drops | **Skul's Ghastly Touch** (1H sword); **Skull Splitter** (rare 2H mace) |

---

## Boss 3: Hearthsinger Forresten (rare optional)

| Field | Value |
|-------|-------|
| HP | ~24k |
| Phases | None |
| Mechanic | Bard ghost; **Sound Wave** AoE silence; **Imminent Doom** debuff (DoT, dispel-needed); raid stays in melee + dispel |
| Notable drops | **Hearthsinger's Lyric** (caster wand); **Songsteel Bracers**; **Forresten's Hat** (cosmetic) |

---

## Boss 4: Timmy the Cruel

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Trample** (front-of-room knockback); **Mortal Strike** stack on tank (50% healing reduction); **Whirlwind** every ~30s (raid stays out); off-tank rotation |
| Notable drops | **Timmy's Galoshes** (Plate boots); **Cruel Barb** (1H sword); **The Postmaster's Tunic** *(T0.5 chest, RNG)* |

---

## Boss 5: Cannon Master Willey (Cannon Kite)

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Runs to cannon** at start of fight; if cannon reached and channeled, raid takes massive AoE damage; tank must **kite** Willey away from cannon range; Willey has **Bombard** ranged AoE; **Tank-and-spank** at distance from cannon |
| Notable drops | **Cannon Master's Hatchet** (1H axe — Hunter/Sham Enh/Pal Ret); **Slayer's Crest** (Plate trinket); **Plate of the Eternal Council** (Plate chest); **Slayer's Helm** *(T0 Hunter helm, RNG)* |

**Decision-engine rule:** Willey kite mechanic = position him 30+ yards from cannon. Engine should encode pre-pull positioning + tank-pull-direction invariant.

---

## Boss 6: Malor the Zealous (rare in some patches)

| Field | Value |
|-------|-------|
| HP | ~28k |
| Phases | None |
| Mechanic | **Holy Fire** (interruptible heal cast); **Holy Smite** burst; tank-and-spank with interrupt rotation |
| Notable drops | **Zealot's Mantle** (caster shoulders); **Malor's Strikepiece** (1H mace); **Zealot's Cloak** |

---

## Boss 7: Crimson Hammersmith (rare optional)

| Field | Value |
|-------|-------|
| HP | ~22k |
| Phases | None |
| Mechanic | Standard tank-and-spank; **Hammer Slam** AoE knockback; raid stays out |
| Notable drops | **Crimson Felt Hat** (caster cloth); **Crimson Hammersmith's Apron** (cosmetic blacksmithing apron); **Slayer's Helm** *(T0 Hunter alt drop, RNG)* |

---

## Boss 8: Archivist Galford

| Field | Value |
|-------|-------|
| HP | ~26k |
| Phases | None |
| Mechanic | **Massive HP**; **Burning Books** AoE DoT (raid spreads); **Pyroblast** burst; tank holds + raid spreads at 5+ yards |
| Notable drops | **Archivist's Bracers** (caster cloth); **The Postmaster's Robe** *(T0.5 chest, RNG)*; **Galford's Quaff** (consumable proc trinket) |

---

## Boss 9: Postmaster Malown (Letter-Mailbox Event)

This is **THE** Strat-Live signature mechanic.

### Setup

The instance contains **4 mailboxes** scattered across King's Square, Market Row, Crusader Square, and Postmaster's Office. Each mailbox contains a specific **Postmaster's Letter** that the raid must collect.

### Trigger Sequence

| Step | Action | Source |
|------|--------|--------|
| 1 | Find **Mailbox 1** at King's Square (with Skul nearby) | Letter A |
| 2 | Find **Mailbox 2** at Market Row (Hearthsinger Forresten area) | Letter B |
| 3 | Find **Mailbox 3** at Crusader Square (Cannon Master Willey area) | Letter C |
| 4 | Find **Mailbox 4** at Postmaster's Office (final room) | Letter D |
| 5 | Read all 4 letters in any order | Triggers Postmaster Malown spawn |

### Postmaster Malown Encounter

| Field | Value |
|-------|-------|
| HP | ~30k |
| Phases | None (multi-wave from start) |
| Mechanic | **Mind Numbing Poison** (caster damage reduction); **Soul Drain** wand drop; periodically summons **Wretched Mailmen** (4 lvl 60 elites); raid AoE adds while tank holds Postmaster |
| Notable drops | **The Postmaster's Tunic** (T0.5 chest); **The Postmaster's Robe** (T0.5 caster chest); **Soul Drain** caster wand; **Iceblade Hacker** (1H sword, rare) |

**Decision-engine rule:** Postmaster Malown event is **the** Strat-Live must-do for T0.5 chest pieces. Engine should encode 4-mailbox collection sequence + summon trigger.

---

## Boss 10: Balnazzar (Final Dreadlord)

| Field | Value |
|-------|-------|
| HP | ~32k |
| Phases | None |
| Mechanic | **Mind Control** (random raid 8s, possessed raider attacks party); **Inferno** AoE (random raid 3k damage, 5-yard radius); **Sleep** (random raid 6s); raid spreads + Tremor/Fear-Ward to break sleep |
| Notable drops | **Balnazzar's Robe** (caster cloth chest); **The Hand of Edward the Odd** (Mage 1H sword/dagger); **Soul Harvester** (caster staff); **Slayer's Helm** *(T0 Hunter alt drop)* |

**Decision-engine rule:** Balnazzar Mind Control = same as Strat-UD Anastari pattern. Engine should encode possess-detection + DPS-rush invariant.

---

## T0 Dungeon Set Pieces (Per Class)

Strat-Live drops T0 set pieces tied to specific classes:

| Class | T0 set name | Dropped from |
|-------|-------------|--------------|
| Hunter | **Slayer's Garb** | Cannon Master Willey, Crimson Hammersmith, Balnazzar (Slayer's Helm RNG) |
| Priest (Holy) | **Devout's** | Various Strat-Live bosses (RNG) |
| Rogue | **Shadowcraft** | Various Strat-Live + Strat-UD + DM (RNG) |
| Druid | **Wildheart** | Various Strat-Live + Strat-UD + Scholo + DM (RNG) |
| Paladin (Holy/Ret/Prot) | **Lightforge** | Various Strat-Live + BRD + UBRS (RNG) |

**Decision-engine rule:** T0 acquisition is **multi-month rotation** across BRD/UBRS/Strat-Live/Strat-UD/Scholo/DM. Engine should track per-character T0 slot set across all 5 dungeons.

---

## Service Entrance / Crusader Square (Outdoor Farming)

Outside the Strat-Live instance, the **Crusader Square** courtyard contains:

| Resource | Source | Notes |
|----------|--------|-------|
| **Argent Dawn rep** (Honored grind) | Crusader General + Crusader Lord Valdelmar (lvl 60 elite) kills | Repeatable, no instance lockout |
| **Scourgestones** (rep accelerator) | Mob kills (Plagued Wretched, Crusader Captain) | 5-10 Scourgestones per pull, AD turn-in |
| **Plagued Lung** (Scholo attune chain reagent) | Crusader Generals (rare drop) | Cross-dungeon |
| **Lordaeron Citizen Necklace** (cosmetic loot) | Crusader Captain | Random vendor sell |
| **Tirion Fordring chain** (long multi-zone Cenarion-tier rep chain) | Tirion Fordring NPC nearby | Multi-step, ends in EPL |

**Decision-engine rule:** Crusader Square outdoor farming is **the** AD-rep grind path for casual Honored push. Engine should plan 1-2 hour weekly run for AD rep accelerator parallel with Naxx attune.

---

## VMaNGOS / Server Reality Check

Strat-Live is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Magistrate Barthilas | Run-to-wall opener timing (shared with UD side) |
| Cannon Master Willey | Cannon kite range; AoE damage scaling |
| Postmaster Malown | 4-mailbox letter-collection trigger AI |
| Balnazzar | Mind Control / Sleep AoE timing |
| Crusader Square outdoor | Mob respawn rate; AD rep tick |

**Decision-engine rule:** Strat-Live script-completeness is high. Engine should standard-pull without fail-fast checks on most modern VMaNGOS.

---

## Decision-Engine Rules

1. **Entry side**: Strat has 2 entrances (Live/west and UD/east). Engine should pre-determine target boss list:
   - T0 set + Postmaster Tunic + Slayer's Helm → Live side
   - Deathcharger / Eye of Naxx / 45-min Baron Run → UD side
2. **Postmaster letter event**: 4-mailbox collection → Postmaster Malown summon. Engine should encode 4-mailbox waypoint sequence.
3. **Cannon Master Willey kite**: position 30+ yards from cannon. Engine should encode pre-pull positioning + tank-pull-direction.
4. **Balnazzar Mind Control**: possess mechanic; DPS-rush possessed teammate. Engine should encode possess-detection + MC-break.
5. **T0 acquisition**: per-character slot map; Strat-Live provides specific slots (Postmaster Tunic, Slayer's Helm, etc.). Engine should track across 5 dungeon sources.
6. **Crusader Square outdoor farming**: AD rep accelerator alongside instance runs. Engine should round-trip Strat outdoor + indoor for combined rep + loot.
7. **Tirion Fordring chain**: multi-zone Cenarion-Hold tier chain at outdoor; engine should pickup on first visit.
8. **Lockout-free**: Strat-Live has no instance reset; engine can run multiple back-to-back for set-piece farming.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 58-63 entry gate
Snapshot.Class                                    // T0 set targeting
Snapshot.Inventory.Has("PostmasterTunic")         // T0.5 chest signal
Snapshot.Inventory.Has("PostmasterRobe")          // T0.5 caster chest signal
Snapshot.Inventory.Has("SlayersHelm")             // T0 Hunter helm signal
Snapshot.Inventory.Has("CannonMastersHatchet")    // 1H axe drop signal
Snapshot.Inventory.PostmasterLetterCount          // 0-4 letter event progress
Snapshot.Boss.Balnazzar.Killed                    // final boss
Snapshot.Boss.PostmasterMalown.Killed             // event boss completion
Snapshot.Reputation.ArgentDawn                    // Honored gate (Crusader Square farm)
Snapshot.Inventory.Scourgestones                  // AD rep turn-in
Snapshot.RaidGroup.PossessedRaiders               // Balnazzar MC tracking
Snapshot.RaidGroup.PullState.WilleyCannonRange    // Cannon kite invariant
Snapshot.QuestLog.Active.TirionFordringChain      // multi-zone chain
Snapshot.ServerCapabilities.StratLiveBoss[<name>] // VMaNGOS scripting flag
```

---

## Cross-References

- Strat-UD (sister side, Baron Rivendare): [stratholme-undead.md](stratholme-undead.md)
- Magistrate Barthilas (shared boss): [stratholme-undead.md#boss-1-magistrate-barthilas-special-opener](stratholme-undead.md#boss-1-magistrate-barthilas-special-opener)
- Argent Dawn rep (AD Honored grind): [../reputations/argent-dawn.md](../reputations/argent-dawn.md)
- Tirion Fordring chain (multi-zone): [../sections/06-l50-l60.md](../sections/06-l50-l60.md#western-plaguelands-lvl-51-58)
- Naxx attune chain (AD Honored gate): [../attunements/naxxramas.md](../attunements/naxxramas.md)
- T0 progression: [../classes/](../classes/) (per-class file)
- Atiesh Festival Lane (legendary chain final step in Strat exterior): [../raids/naxxramas.md](../raids/naxxramas.md)
- L50-L60 bracket: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [sunken-temple.md](sunken-temple.md), [stratholme-undead.md](stratholme-undead.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
