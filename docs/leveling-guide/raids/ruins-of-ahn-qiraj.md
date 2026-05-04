---
title: "Raid — Ruins of Ahn'Qiraj (AQ20)"
patch: "1.12.1 (Drums of War, Sept 2006); raid added in 1.9 'Gates of Ahn'Qiraj' (Jan 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Ruins_of_Ahn%27Qiraj
crawl_date: 2026-05-01
---

# Ruins of Ahn'Qiraj (AQ20, 20-man) — The Outer Vault

The 6-boss 20-man raid in Silithus, sharing the same outer-Gate entrance as AQ40 but a separate instance portal. **No attune required** (Gates of Ahn'Qiraj must be open server-wide). 3-day lockout. Drops a mix of **class-specific quest reward sets** (turn in **Idols + Scarabs** to Cenarion Hold NPCs) and bracket-equivalent epic gear. Major **Brood of Nozdormu rep accelerator** for AQ40-locked rewards.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Raid size | 20-man |
| Bosses | 6 (Kurinnaxx, Rajaxx, Moam, Buru, Ayamiss, Ossirian) |
| Lockout | **3 days** (Tuesday/Friday US, Wednesday/Saturday EU) — same as ZG |
| Location | Silithus south, after Gates of Ahn'Qiraj opens (Scepter chain) |
| Attune | None — open to all post-Gates |
| Tier reward | **Class-specific quest sets** (3-5 pieces blue/epic via Idol + Scarab turn-in at Cenarion Hold) — *NOT* a numbered Tier set |
| Legendary | None native; Brood of Nozdormu rep accelerator for Atiesh chain (AQ40 C'Thun step) |
| Patch added | 1.9 ("Gates of Ahn'Qiraj") — January 2006 |
| 1.12.1 status | Live; mature scripting (simpler than AQ40); standard pre-Naxx-tier farming raid |

---

## Boss Order

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Kurinnaxx** | Easy | Scarab Terrace (entry) |
| 2 | **General Rajaxx** | Medium (waves event) | General's Terrace |
| 3 | **Moam** | Medium (mana mechanic) | The Reservoir |
| 4 | **Buru the Gorger** | Medium (egg chase) | The Hatchery |
| 5 | **Ayamiss the Hunter** | Medium (flying-then-melee) | The Comb |
| 6 | **Ossirian the Unscarred** | Medium-Hard (crystal rotation) | Watchers' Terrace (final) |

**Decision-engine rule:** AQ20 is the standard "farm raid" — 6 bosses in ~60-90 minutes for a geared 20-man. All bosses scripted by 1.12; no major coordination challenges beyond Ossirian's crystals.

---

## Boss 1: Kurinnaxx (Sand Reaver)

| Field | Value |
|-------|-------|
| HP | ~210k (20-man scaled) |
| Phases | None (single phase) |
| Mechanic | **Mortal Wound** debuff stack on tank (50% healing reduction per stack); **Sand Trap** AoE under random raid; tank rotation when stack >= 4 |
| Notable drops | **Heavy Obsidian Belt** Warrior tank; **Wand of the Whispering Dead** caster wand; **Mantle of Wicked Revenge** Warlock; **Idol of Death** quest item (Druid set turn-in) |

**Raid strategy:** 2 tanks rotate Mortal Wound stacks; raid spreads to avoid Sand Traps; 4-5 minute kill.

---

## Boss 2: General Rajaxx (Qiraji Officer)

| Field | Value |
|-------|-------|
| HP | ~80k (Rajaxx) + waves of mobs |
| Phases | **7 waves** of Captains + General Andorov + finally Rajaxx himself |
| Mechanic | **Wave-event**: Rajaxx commands 6 captains to attack (Captain Drenn, Xurrem, Tuubid, Qeez, Lazuri, Ankha — order varies); each wave ~30-45 seconds; **Captain Andorov** (NPC ally — 4 of his Kaldorei Elites help raid); after waves, **Rajaxx engages directly** with **Thunderclap** + **Disarm** + **Sweeping Strikes** + **Trash Cleave** |
| Notable drops | **Bracelets of Wrath** Warrior tank; **Cloak of Concentrated Hatred** caster; **Ring of the Qiraji Fury** rare drop; **Idol of Strife** quest item (Warrior set turn-in) |

**Raid strategy:** off-tanks pick up captains as waves spawn; Andorov-NPC tanks Rajaxx during his pre-engage chat; raid handles waves in parallel. **Andorov can die — bring Soulstone/Reincarnation rotation to keep him alive** (he gives bonus loot if alive at Rajaxx kill).

**Decision-engine cue:** Rajaxx wave timer is fixed at ~3 minutes total. Engine should encode wave-spawn timestamps and pre-position add-tanks at each spawn point.

---

## Boss 3: Moam (Obsidian Destroyer)

| Field | Value |
|-------|-------|
| HP | ~250k |
| Phases | **2 alternating**: **Energized** (Moam glows + casts Drain Mana on raid mana pool) + **Stone Form** (Moam petrifies, physical-immune, raid waits) |
| Mechanic | **Drain Mana** chain on entire raid (~500 mana per cast on each member); raid healers/casters lose mana fast; **Stone Form** triggers when Moam's mana is full (~3 mins of draining) — physical DPS continues + healers regen mana; **Mana Fiends** spawn from Stone Form to harass raid |
| Notable drops | **Imperial Qiraji Armaments** (T2.5 reagent for AQ40 craft); **Wand of Eternal Light**; **Robe of Apotheosis** Mage; **Idol of Vehemence** quest item (Hunter set) |

**Raid strategy:** healers + caster DPS at <50% mana to keep Moam from full-charging too fast; Stone Form is forced respite phase; melee + Hunters maintain DPS during Stone Form; **prevent Drain Mana from chain-restoring Moam to full mana** by draining-first via Mana Burn / Fel Domination if available.

**Decision-engine cue:** Moam mana-pool tracking is critical. Engine should monitor `Snapshot.Boss.Moam.ManaCurrent / ManaMax` and trigger raid-burn-to-prevent-energize at 80%+.

---

## Boss 4: Buru the Gorger (Silithid Colossus)

| Field | Value |
|-------|-------|
| HP | ~150k |
| Phases | None (chase mechanic throughout) |
| Mechanic | **Egg chase**: Buru fixates on a random raid member and chases them ("Gorger" = chaser); raid kites Buru between **Buru Eggs** (lvl 60 elite, scattered around room) — **eggs explode for 1500 dmg + slow Buru** when Buru reaches them; raid throws egg-explosions to slow chaser; if Buru reaches kited target, ~2k melee + dispel-ignore poison |
| Notable drops | **Hammer of Ji'zhi** healer mace; **Mantle of Maz'Nadir** Hunter; **Carapace of the Old God** trinket (uncommon RNG); **Idol of the Sun** quest item (Paladin set) |

**Raid strategy:** raid spreads + agres Eggs early; designated kiter (high Stamina + speed-bonus, e.g., Druid Travel Form or Hunter Aspect of Cheetah) draws Buru toward the next live Egg; Egg explodes when Buru is near → 4-5 second slow → kiter swap or boss-DPS interim.

**Decision-engine cue:** Buru kiter rotation requires 4-5 designated raid members with `Spells.Has("AspectOfCheetah") OR Class IN [Druid, Shaman]`. Engine should pre-flag kite-eligible members.

---

## Boss 5: Ayamiss the Hunter (Silithid Wasp Queen)

| Field | Value |
|-------|-------|
| HP | ~180k |
| Phases | **2 sequential**: **Phase 1** (~70%): Ayamiss flies above raid, untargetable melee + ranged-only DPS; **Phase 2** (≤70%): Ayamiss lands, melee-able |
| Mechanic | **Phase 1 (flying)**: spawn **Larva** adds (lvl 60 silithid pups) → larvae crawl toward an altar to sacrifice random raid members; **Stinger** ranged attack on tank-position; ranged DPS focus boss while melee tank-pickup larvae; **Phase 2 (landed)**: Ayamiss melee-attacks tank + cleaves; Stinger ramps in damage |
| Notable drops | **Mantle of Phrenic Power** Mage; **Boots of Epiphany** healer; **Don Rodrigo's Heart** trinket (Druid Feral); **Idol of the Sage** quest item (Druid set) |

**Raid strategy:** Phase 1 = ranged-DPS-only on Ayamiss; melee-tank larvae with off-tanks; Phase 2 = standard melee fight; ~5-min engagement.

---

## Boss 6: Ossirian the Unscarred (Horusath, final boss)

| Field | Value |
|-------|-------|
| HP | ~250k |
| Phases | None (continuous crystal rotation) |
| Mechanic | **Crystal rotation**: 8 crystals around the room (Lightning, Frost, Fire, Sand, Holy, Shadow, Nature, Arcane) — each weakens Ossirian to a specific elemental damage type for 30 seconds; raid must **rotate Ossirian to a new crystal as the prior weakness expires**; **Sand Storm** AoE if no crystal active |
| Notable drops | **Cloak of the Hakkari Worshippers** caster; **Crossbow of Imminent Doom** Hunter; **Eskhandar's Pelt** Druid Feral; **Eskhandar's Right Claw** weapon trinket; **Idol of Brutality** quest item (Shaman set) |

**Raid strategy:** designated **crystal-tracker** announces crystal expiry (~30s warning); raid pulls Ossirian to next-active crystal; tank pivots boss for melee-side; **Sand Storm avoidance** = NEVER LET ALL CRYSTALS EXPIRE simultaneously.

**Decision-engine cue:** Ossirian crystal rotation is **the AQ20 timing-coordination encounter**. Engine should encode crystal positions as 8 fixed waypoints + rotation-cycle timer (30s per crystal).

---

## Class-Specific Quest Reward Sets

AQ20 is the only raid in 1.12 that drops **class-specific quest items** turned in at Cenarion Hold for blue/epic gear. The "set" structure varies per class (3-5 pieces typically).

### Quest item drops

| Boss | Idol drops | Scarab drops |
|------|-----------|--------------|
| Kurinnaxx | Idol of Death (Druid) | Scarab of various types (RNG) |
| Rajaxx | Idol of Strife (Warrior) | Scarab of various types |
| Moam | Idol of Vehemence (Hunter) | — |
| Buru | Idol of the Sun (Paladin) | — |
| Ayamiss | Idol of the Sage (Druid alt) | — |
| Ossirian | Idol of Brutality (Shaman); Idol of Rebirth (Priest) | Final-boss Scarabs |

`[verify pass 3]` — exact Idol-to-class mapping. Most servers track these via a **Cenarion Circle** quest hub at Cenarion Hold; turn-in NPC is **Bor Wildmane** (lvl 60 NPC, near zone master).

### Class set names (approximate)

| Class | AQ20 set name |
|-------|---------------|
| Warrior | **Battlegear of Eternal Justice** (Vest + Boots + Belt) `[verify pass 3]` |
| Paladin | **Symbols of Unending Life** `[verify pass 3]` |
| Hunter | **Striker's Field Armor** `[verify pass 3]` |
| Rogue | **Ostracized Berserker's Battlegear** `[verify pass 3]` |
| Priest | **Vestments of the Atal'ai Prophet** `[verify pass 3]` (cross-checked with ZG drops) |
| Shaman | **The Stormcaller's Field Armor** `[verify pass 3]` |
| Mage | **Enigma Field Armor** `[verify pass 3]` |
| Warlock | **The Doomcaller's Field Armor** `[verify pass 3]` |
| Druid | **Ascendance Bands** `[verify pass 3]` |

**Decision-engine rule:** AQ20 drops are **the cheapest class-set acquisition** in vanilla — most pieces require <5 raid clears. Engine should always run AQ20 weekly even if AQ40 + Naxx are in progress.

### Cenarion Circle reagent crafting

After AQ20, players can use AQ20 reagents at Cenarion Hold for **Cenarion Circle-locked crafted gear**:
- Friendly: Basic crafted blue gear
- Honored: 1 BoP recipe per profession (Tailoring, Leatherworking, Blacksmithing, Engineering)
- Revered: Advanced crafted blue/epic gear
- Exalted: BiS-tier rep-only items (rings, cloaks)

---

## Brood of Nozdormu Reputation

AQ20 + AQ40 boss kills give Brood of Nozdormu rep. AQ20 weekly = ~500-1000 rep per clear; AQ40 weekly = ~3000-5000 rep per clear.

| Tier | AQ20 rewards |
|------|--------------|
| Friendly | Basic Anachronos turn-in completion (Atiesh chain step 3 prerequisite) |
| Honored | Common rings (Don Julio's, Don Mauricio's, etc.) |
| Revered | Advanced rings; class-specific Brood-stat rings |
| Exalted | BiS class rings (Band of Earthen Wrath, Band of Servitude, etc.) |

**Decision-engine rule:** AQ20 is the **fastest Brood of Nozdormu rep** path because of 3-day lockout + 6-boss weekly farming. Engine should prioritize AQ20 weekly even on guilds focused on Naxx clearance.

See [../reputations/](../reputations/) for Brood detail (file pending).

---

## Cenarion Circle Reputation

AQ20 + Silithus questing + AQ40 boss kills give Cenarion Circle rep.

| Tier | Reward |
|------|--------|
| Neutral | (default) |
| Friendly | AQ40 entry chain unlock |
| Honored | Cenarion Reagent recipes (Tailoring, LW, Engineering); 1 BoP class recipe |
| Revered | Advanced Cenarion crafted gear; Cenarion Hold quartermaster discount |
| Exalted | Cenarion Vestments + class-specific Cenarion gear (BiS-tier rep-only) |

**Decision-engine rule:** Cenarion Circle Friendly is the AQ40 attune unlock; bots should hit Friendly via Silithus quests + first AQ20 clear in parallel.

---

## VMaNGOS / Server Reality Check

AQ20 boss-script completeness in VMaNGOS as of 2026 timeframe:

| Boss | Common scripting issue |
|------|------------------------|
| Kurinnaxx | Mortal Wound stack reset on tank-swap |
| Rajaxx | Wave-spawn timer drift; Andorov NPC pathing |
| Moam | Drain Mana animation desync; Stone Form shatter timing |
| Buru | Egg explosion damage scaling; chase-target reassignment |
| Ayamiss | Phase 1→2 transition; larva pathing to altar |
| Ossirian | Crystal rotation timing; Sand Storm trigger |

**Decision-engine rule:** AQ20 is **fully scripted** on most VMaNGOS-tier servers. Only Ossirian crystal-timing has historically had edge-case bugs. Engine should `ServerCapabilities.AQ20Boss[<name>]` check pre-pull.

---

## Decision-Engine Rules

1. **Attune precondition**: Gates of Ahn'Qiraj must be open server-wide (Scepter chain or GM-bypass on private servers).
2. **Boss order**: Kurinnaxx → Rajaxx → Moam → Buru → Ayamiss → Ossirian. Linear progression with no skips.
3. **Andorov rotation (Rajaxx)**: keep Andorov NPC alive for bonus loot. Engine should rotate Soulstone/Reincarnation/Resurrect on Andorov.
4. **Moam mana management**: prevent boss from full-charging via raid-burn at 80%. Engine should monitor `Snapshot.Boss.ManaCurrent` and scale DPS accordingly.
5. **Buru kiter assignment**: 4-5 designated kiters with speed buffs/Travel Form/Cheetah. Engine should pre-assign role tags.
6. **Ossirian crystal rotation**: 8-crystal cycle, 30s per crystal, raid pivots boss. Engine should encode crystal waypoints + rotation-cycle countdown.
7. **Idol/Scarab quest item priority**: AQ20 is bracket-defining for class-specific reward sets. Engine should always loot Idols/Scarabs and turn in at Cenarion Hold.
8. **Brood of Nozdormu accelerator**: AQ20 weekly clear gives faster rep than AQ40 progress. Bots farming Nozdormu rep should clear AQ20 ahead of AQ40.
9. **Cenarion Circle parallel rep**: Cenarion Hold Silithus questing + AQ20 = Friendly within 5-10 hours; Honored within 30-50 hours.
10. **Lockout-aligned weekly clear**: 3-day lockout = 2 clears per week. Engine should align AQ20 farm with Tuesday/Friday or Wednesday/Saturday windows.
11. **VMaNGOS-AQ20-script check**: pre-pull confirmation; almost always-Pass on modern VMaNGOS.

---

## Snapshot Fields Needed

```text
Snapshot.Level == 60                            // hard gate
Snapshot.Class                                  // class quest set tracking
Snapshot.Faction                                // raid composition mix
Snapshot.GatesOfAhnQiraj.Open                   // server-wide event check
Snapshot.Reputation.BroodOfNozdormu             // ring rewards + Atiesh chain
Snapshot.Reputation.CenarionCircle              // AQ40 attune + crafted gear
Snapshot.Inventory.IdolsCount                   // class quest set tracking
Snapshot.Inventory.ScarabsCount                 // class quest set tracking
Snapshot.QuestLog.Active.AQ20ClassSet           // class set quest progress
Snapshot.RaidGroup.Composition.Tanks            // 2-tank Kurinnaxx + Rajaxx; designated tank vs add tanks
Snapshot.RaidGroup.Composition.Kiters           // Buru kiter pre-assignment
Snapshot.WorldBuffs.Active                      // pre-raid stack
Snapshot.ServerCapabilities.AQ20Boss[<name>]    // VMaNGOS scripting flag
Snapshot.Boss.Moam.ManaCurrent                  // mana-management trigger
Snapshot.Boss.Ossirian.CurrentCrystal           // rotation tracking
Snapshot.WallClock.LockoutResetTimer            // 3-day lockout alignment
```

---

## Cross-References

- AQ40 raid (sister instance, T2.5 + Atiesh Base): [ahn-qiraj-temple.md](ahn-qiraj-temple.md)
- Other raids: [molten-core.md](molten-core.md), [onyxias-lair.md](onyxias-lair.md), [blackwing-lair.md](blackwing-lair.md), [zul-gurub.md](zul-gurub.md), [naxxramas.md](naxxramas.md)
- Brood of Nozdormu rep: [../reputations/](../reputations/) (file pending)
- Cenarion Circle rep: [../reputations/](../reputations/) (file pending)
- Silithus zone (Cenarion Hold + Twilight Hammer rep): [../sections/06-l50-l60.md](../sections/06-l50-l60.md#silithus-lvl-55-60)
- World buffs (pre-raid): [../systems/world-buffs.md](../systems/world-buffs.md)
- Consumables: [../systems/consumables.md](../systems/consumables.md)
- Class-specific raid prep: [../classes/](../classes/) (per-class file)
