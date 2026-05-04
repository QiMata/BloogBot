---
title: "Raid — Temple of Ahn'Qiraj (AQ40)"
patch: "1.12.1 (Drums of War, Sept 2006); raid added in 1.9 'Gates of Ahn'Qiraj' (Jan 2006); C'Thun retuned in 1.10 'Storms of Azeroth' (Mar 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Temple_of_Ahn%27Qiraj
  - https://warcraft.wiki.gg/wiki/Scepter_of_the_Shifting_Sands
crawl_date: 2026-05-01
---

# Temple of Ahn'Qiraj (AQ40, 40-man) — The Old God Vault

The 9-boss endgame raid in Silithus. Released in 1.9 (Jan 2006) with the **Gates of Ahn'Qiraj** server-wide war effort event. C'Thun was originally **untunable at launch** — Blizzard hotfixed in 1.10 after 8 weeks of no kills. By 1.12.1 the encounter is "killable C'Thun" but still the hardest fight in vanilla. Drops **Tier 2.5** ("Conqueror's/Striker's/Stormcaller's/etc."), the **Scepter of the Shifting Sands** legendary chain (server-first = Scarab Lord title + Black Qiraji Resonating Crystal mount), and the **Atiesh Base** step (C'Thun drop).

---

## Quick Facts

| Field | Value |
|-------|-------|
| Raid size | 40-man |
| Bosses | 9 (Skeram, Bug Trio, Sartura, Fankriss, Viscidus, Princess Huhuran, Twin Emperors, Ouro, C'Thun) |
| Lockout | 7 days (weekly) |
| Location | Silithus, southern entrance through Scepter Gong gate |
| Attune | None — but **Brood of Nozdormu** Neutral required for AQ-locked rewards (rings, Atiesh chain) |
| Tier reward | **Tier 2.5** ("Conqueror's Battlegear" / "Avenger's" / "Striker's" / "Deathdealer's" / "Garments of the Oracle" / "Stormcaller's" / "Enigma" / "Doomcaller's" / "Genesis") |
| Legendary | **Scepter of the Shifting Sands** (server-first = Scarab Lord title + Black Qiraji Resonating Crystal flying mount); **Atiesh Base** step (C'Thun drop, requires Anachronos quest active) |
| Patch added | 1.9 ("Gates of Ahn'Qiraj") — January 2006 |
| 1.12.1 status | Live; C'Thun retuned 1.10 (3-Eye phase + Stomach phase tunings) |

---

## Boss Order

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **The Prophet Skeram** | Easy-Medium | Temple Gates (entry) |
| 2 | **Bug Trio** (Yauj/Vem/Kri) — optional but typical run order | Medium-Hard | Hive Undergrounds |
| 3 | **Battleguard Sartura** | Medium | Hive Undergrounds |
| 4 | **Fankriss the Unyielding** | Medium | Hive Undergrounds |
| 5 | **Viscidus** (optional, hidden behind side path) | Hard | Hive Undergrounds |
| 6 | **Princess Huhuran** | Medium-Hard | Hive Undergrounds |
| 7 | **Twin Emperors** (Vek'lor + Vek'nilash) | Hard | Hive Undergrounds |
| 8 | **Ouro** (optional, behind sand mound) | Hard | Hive Undergrounds |
| 9 | **C'Thun** | **Hardest in vanilla** | Vault of C'Thun (final) |

**Decision-engine rule:** Bug Trio + Viscidus + Ouro are technically optional (skippable if guild prefers C'Thun-only progression), but each drops T2.5 + chain reagents — engine should always include them.

---

## Boss 1: The Prophet Skeram

| Field | Value |
|-------|-------|
| HP | ~430k (40-man scaled) |
| Phases | None (single phase with two split-self mechanics) |
| Mechanic | **Mind Blast** (~10s CD); **Earth Shock** (interruptible — DPS interrupt rotation); at 75/55/35/15% HP, **Skeram splits into 3 copies** — 2 illusions + the real one; raid must DPS all 3 simultaneously OR identify the real one (highest HP) |
| Notable drops | T2.5 piece for **healer** classes (Priest/Druid/Shaman); **Hammer of Ji'zhi** healer mace; **Gauntlets of New Life** Resto Druid `[verify pass 3]` |

**Raid strategy:** dedicated interrupt rotation on Earth Shock; melee dispel Mind Blast cripple debuff; at split, splash AoE on all 3 copies until one shows higher HP.

---

## Boss 2: Bug Trio (Yauj / Vem / Kri)

| Field | Value |
|-------|-------|
| HP per bug | ~200k each (Yauj/Vem/Kri share aggro) |
| Phases | None — all 3 bugs active simultaneously |
| Mechanic | **MUST kill all 3 within 10 seconds of each other** — else surviving bug enrages; **Vem** charges raid (high knockback); **Kri** poison cleave (melee tanks); **Yauj** heals other bugs (~15k heal); **Kill order: Vem first → Kri → Yauj** (so Yauj's heal is wasted on dead Kri) |
| Notable drops | T2.5 piece for **Hunter/Rogue** (Striker's/Deathdealer's); **Necklace of Purity**; **Vest of Swift Execution** |

**Raid strategy:** balance all 3 bugs to 30% HP, then synchronize execute. **Yauj must be killed last** (her resurrect-heal mechanic is unique).

**Decision-engine rule:** if `Snapshot.RaidGroup.HealUptime < 95%`, defer Bug Trio until raid dispel-and-heal coordination matured. Mistuning = enrage = wipe.

---

## Boss 3: Battleguard Sartura

| Field | Value |
|-------|-------|
| HP | ~500k |
| Phases | None |
| Mechanic | **Whirlwind** (Sartura whirlwinds entire raid for 10s, melee continue) — every ~30s; **Adds spawn** (Sartura's Defenders + Sartura's Royal Guards) — must be tank-controlled or kited; **Enrage at 5%** |
| Notable drops | T2.5 piece for **Warrior/Paladin** plate (Conqueror's/Avenger's); **Belt of Never-Ending Agony** (Warrior tank trinket); **Slime-Encrusted Pads** Druid Feral cloak |

**Raid strategy:** off-tanks pick up adds quickly; Whirlwind is unavoidable, raid healers AoE-heal through it.

---

## Boss 4: Fankriss the Unyielding

| Field | Value |
|-------|-------|
| HP | ~400k |
| Phases | None |
| Mechanic | **Spawned worms** (random raid teleported behind boss + worm spawns ~every 15s); **Mortal Wound stack** on tank (50% healing reduction per stack); raid must control 5+ worm adds simultaneously; **enrage at 10% HP** |
| Notable drops | T2.5 piece for **Mage/Warlock** caster (Enigma/Doomcaller's); **Sharpened Silithid Femur** caster wand; **Shackles of Ignorance** Mage |

**Raid strategy:** 4 off-tanks rotate through worm spawns; main tank rotates with Mortal Wound stack reset.

---

## Boss 5: Viscidus (optional, side path through poison cloud)

| Field | Value |
|-------|-------|
| HP | ~400k |
| Phases | 2 alternating: **Liquid** (slow degenerate) + **Frozen** (DPS race) |
| Mechanic | **Frost-resistance gear required** (~150-200 FR per raid member); Viscidus stays in melee circle, all melee + caster front-and-back; **Frost damage** ramps as raid Frost spell-power totals; **Frost magic spam** freezes him into Frozen state, then **physical damage shatters** for 50% HP loss → repeat; if shatter timer expires (frozen for too long), Viscidus regens fully |
| Notable drops | **Vanquished Tentacle of C'Thun** (free pet); **Belt of the Old Gods** Mage; **The Burrower's Shell** trinket |

**Raid strategy:** Frost mages spam Frostbolt into Viscidus's body to charge frozen state; physical DPS rotates Cleave/Whirlwind for shatter; repeat 5-6 cycles.

**Decision-engine rule:** Viscidus shatter is **timing-critical** — engine should treat as a **Frost Mage rotation gate** (`if RaidGroup.FrostMages.Count < 5`, skip Viscidus).

---

## Boss 6: Princess Huhuran

| Field | Value |
|-------|-------|
| HP | ~280k |
| Phases | None — single phase 15-min enrage |
| Mechanic | **Poison-bolt volley** (random raid 8-target); **Wyvern Sting** (random raid sleep, dispel via Cleanse Disease/Cure Poison); **15-min enrage timer**; **Nature Resistance gear required** (~150 NR per raid member) |
| Notable drops | T2.5 piece for **Druid/Shaman** (Genesis/Stormcaller's); **The Dragon's Eye** trinket; **Boots of Epiphany** Resto Shaman |

**Raid strategy:** 15-min DPS race; cleanse Wyvern Sting immediately; healers chained dispel.

**Decision-engine rule:** Huhuran requires `RaidGroup.NatureResistance >= 150 && RaidGroup.AvgItemLevel >= 70`. Engine should fail-fast pre-pull if either gate unmet.

---

## Boss 7: Twin Emperors (Vek'lor + Vek'nilash)

| Field | Value |
|-------|-------|
| HP per twin | ~300k each (separate HP pools, must die within 15 seconds of each other) |
| Phases | None — both twins active throughout |
| Mechanic | **Vek'lor** (caster, ranged) — **Shadow Bolt** (interruptible) + **Blizzard** AoE; **Vek'nilash** (melee) — physical strikes + **Mortal Strike** debuff; **Twins teleport** every 30 seconds — both swap places; **Healing absorbed when twins are within 50 yards of each other** ("Heal Brothers") — keep them >50 yards apart at all times |
| Notable drops | T2.5 piece for **Priest** (Garments of the Oracle); **Vek'nilash's Circlet** Priest helm; **Vek'lor's Diadem** Mage helm; class-specific rings |

**Raid strategy:** room divided into Vek'lor (caster) zone and Vek'nilash (melee) zone, ~50+ yards apart; tanks pickup at teleport; DPS-balance both twins to 0% within 15s of each other.

**Decision-engine rule:** Twin Emperors is **the position-coordination encounter** — engine must encode 50-yard separation as an invariant. Failure = "Heal Brothers" ramp = unkillable.

---

## Boss 8: Ouro (optional, behind sand-mound trigger)

| Field | Value |
|-------|-------|
| HP | ~430k |
| Phases | **2 alternating**: **Surface** (Ouro out of sand, melee DPS) + **Submerged** (under sand, untargetable, **Ouro mounds** spawn around raid) |
| Mechanic | **Sweep** during Surface (Ouro lunges at random raid member); **Submerge** (Ouro disappears, scarab adds + Dirt Mounds spawn); **Mounds require off-tank pickup** — let them stack and they spawn additional minions |
| Notable drops | **Ring of the Qiraji Fury**; **Ouro's Intact Hide** (LW reagent for crafted gear); **Wand of Qiraji Nobility** caster; **Don Rodrigo's Heart** trinket (Druid Feral) |

**Raid strategy:** during Submerge, raid spreads out + tanks-pickup-mounds; during Surface, melee burst boss before next Submerge cycle.

---

## Boss 9: C'Thun (the Final Old God)

| Field | Value |
|-------|-------|
| HP | ~1M (Phase 1) + ~400k (Phase 2) |
| Phases | **Phase 1**: Eye of C'Thun — **Eye Beam** (chain-jumping random raid, 1k damage start, exponential), **Dark Glare** (rotating laser sweep across room, 35k damage); **Phase 2** (after Eye dies, ~50% HP): C'Thun body emerges with Tentacles + Stomach + AoE |
| Mechanic | **Phase 1**: 5-target Eye Beam chain (raid spreads 10+ yards apart to break chain), Dark Glare sweep (~40s rotation, raid runs OUT of cone); **Phase 2**: random raid members **teleported into C'Thun's stomach** (3-tentacle DPS race inside; if not killed in 30s, raid dies); Tentacles spawn outside stomach (Eye Tentacles + Claw Tentacles + Giant Tentacles); raid balances tank-control + tentacle clear + stomach-runs |
| Notable drops | T2.5 helm + chest tokens; **Eye of C'Thun** trinket; **The Eye of Sulfuras** prep (Eye drops only here in 1.12.1 — wait, MC drop. Skip); **Death's Sting** Rogue dagger; **Carapace of the Old God** trinket; **Base of Atiesh** (Atiesh chain step) |

**Raid strategy:** Phase 1 = Eye-Beam-chain spread 10+ yards (NEVER stack), Dark-Glare rotate-out 90 degrees; Phase 2 = stomach-rotation (random teleport, tentacle DPS down inside, exit stomach back to outside, repeat); Giant Claw Tentacle is the 4-tank fight; raid blasts boss while alternating stomach + tentacle waves.

**Decision-engine rule:** C'Thun is the **40-man positional + reaction encounter**. Engine should encode:
1. Eye Beam: spread 10-yard waypoints
2. Dark Glare: 40-second rotational warning, raid pivots out of cone
3. Stomach: random-target detection, internal tentacle DPS (1 tentacle) → emerge → re-engage outside
4. Giant Claw Tentacle: 4-tank rotation

Failure modes are bracket-defining: chain-Eye-Beam = wipe; Dark-Glare-stand-in = ~10 raid deaths; Stomach-DPS-too-slow = stomach-victim death + chain-repeat.

---

## Tier 2.5 Set Map (per class)

| Class | Set name | Helm | Chest | Legs | Shoulders | Bracers | Hands | Belt | Boots |
|-------|----------|------|-------|------|-----------|---------|-------|------|-------|
| Warrior | **Conqueror's Battlegear** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Paladin | **Avenger's Battlegear** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Hunter | **Striker's Garb** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Rogue | **Deathdealer's Embrace** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Priest | **Garments of the Oracle** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Shaman | **Stormcaller's Garb** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Mage | **Enigma Vestments** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Warlock | **Doomcaller's Vestments** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |
| Druid | **Genesis Raiment** | C'Thun | C'Thun | Twins | Skeram | Sartura | Fankriss | Princess | Trio |

`[verify pass 3]` — slot-to-boss map approximate; specific encoded slot per class set varies by tokens.

**Decision-engine rule:** T2.5 acquisition flows from C'Thun (helm/chest, 5% per kill) → Twins (legs) → Sartura (bracers) → Skeram (shoulders) → Trio (boots) → others (Princess belt, Fankriss hands).

---

## Brood of Nozdormu Reputation

AQ40 boss kills + AQ20 (Ruins) clears + Silithus quests give Brood of Nozdormu rep. Reaches **Exalted** via long grind (~50-100 hours with full raid clears).

| Tier | Reward (per class, choose one stat path: Resilience/Tank vs DPS/Caster) |
|------|----------------------------------------------------------------------------|
| Friendly | Common rings (varies by class — class-locked stats) |
| Honored | Better rings; Atiesh quest gating (must be at least Neutral) |
| Revered | Premium rings (Don Julio's Band, Songstone of Ironforge, etc.) |
| Exalted | Pre-raid BiS rings (e.g., **Band of Earthen Wrath** Druid; **Signet of Unyielding Strength**) |

**Decision-engine rule:** Brood of Nozdormu rep is **Atiesh chain step 3** prerequisite. Engine should accumulate via Silithus questing and AQ20/AQ40 boss kills in parallel with C'Thun progression.

See [../reputations/](../reputations/) (Brood of Nozdormu file pending).

---

## Scepter of the Shifting Sands (Legendary)

The **Scepter of the Shifting Sands** is a 7-step legendary chain culminating in the Scarab Gong activation, which **opens the Gates of Ahn'Qiraj** for the entire server.

### Chain summary (1.12 Patch)

| Step | Action | Source |
|------|--------|--------|
| 1 | "Bang a Gong!" — start chain at Cenarion Hold (Anachronos) | Anachronos quest |
| 2 | Defeat the **Black Qiraji Resonating Crystal** chain bosses (Magmadar in MC, Onyxia, Princess Huhuran in AQ40, AD-Honored ritual) | Multi-raid |
| 3 | Collect **War-Effort Resources** from server pool (Linen, Iron Bar, Stranglekelp, etc.) — opens 10-hour event | Server-wide |
| 4 | Server quota fulfilled → 10-hour war event triggers (50,000 NPC waves vs Twilight's Hammer in Silithus) | Single-server |
| 5 | First player to ring the Scarab Gong = **Scarab Lord title** + **Black Qiraji Resonating Crystal** mount (flying-style ground mount) | Server-first only |
| 6 | All other completers get the title and the mount; Scepter persists as item | Per-server-completer |
| 7 | Gates of Ahn'Qiraj open — server-wide AQ20/AQ40 access | Permanent |

**1.12.1 status:** the Scepter chain is **server-launch only** event in retail. On private servers, this is replayed when the server elects to do so (typically each "season" or "phase"). On VMaNGOS, the event is triggered by GM admin per-server.

**Decision-engine rule:** Scepter chain is **single-character per server** for the title; bots typically do not pursue this. Engine should mark `ScepterChain.Pursued == false` by default unless user explicitly opts in.

See [../reputations/](../reputations/) for Brood of Nozdormu detail (Scepter chain step 1 prerequisite).

---

## Atiesh Base Step (C'Thun drop)

For Druids/Mages/Priests/Warlocks pursuing Atiesh:

1. Have **Frame of Atiesh** (40 Splinters from Naxx) — see [naxxramas.md](naxxramas.md#atiesh-greatstaff-of-the-guardian-legendary)
2. Have **Anachronos quest active** (Brood of Nozdormu Neutral required)
3. Kill **Kel'Thuzad** in Naxxramas → receive **Staff Head of Atiesh**
4. Kill **C'Thun** in AQ40 → receive **Base of Atiesh** (100% drop while quest active)
5. Travel to Stratholme Festival Lane → kill demon Atiesh → final purification

**Decision-engine rule:** Atiesh chain spans 2 raids (Naxx + AQ40); engine should track per-character `Atiesh.SplinterCount`, `Atiesh.HasFrame`, `Atiesh.HasStaffHead`, `Atiesh.HasBase`, `Atiesh.AnachronosQuestActive`.

---

## VMaNGOS / Server Reality Check

AQ40 boss-script completeness in VMaNGOS as of 2026 timeframe:

| Boss | Common scripting issue |
|------|------------------------|
| Skeram | Split-clone HP/aggro logic |
| Bug Trio | 10-second-window enrage trigger; Yauj heal aggro |
| Sartura | Whirlwind animation desync; add spawn timing |
| Fankriss | Worm spawn position; Mortal Wound stack reset |
| Viscidus | Frost damage scaling; shatter timing edge case |
| Huhuran | Wyvern Sting dispel mechanic; 15-min enrage |
| Twin Emperors | Teleport position swap; "Heal Brothers" 50-yard radius |
| Ouro | Submerge/Surface phase transition; Mound aggro |
| **C'Thun** | **Stomach phase teleport mechanic; Dark Glare angle; Eye Beam chain target selection** — most-fragile script |

**Decision-engine rule:** if `ServerCapabilities.AQ40Boss[<bossName>].Scripted == false`, refuse-pull. C'Thun in particular has been the most-rebroken-and-refixed script in private server history. Engine should consult server flag pre-pull.

---

## Decision-Engine Rules

1. **Attune precondition**: none for AQ40 entry; Brood of Nozdormu Neutral required for ring/Atiesh chain rewards.
2. **Wing-clear order**: Skeram → Bug Trio → Sartura → Fankriss → Viscidus (optional) → Huhuran → Twin Emperors → Ouro (optional) → C'Thun. Standard "T-pose" linear progression.
3. **Resistance gear gates**: Viscidus 150+ FR; Huhuran 150+ NR. Engine should `Snapshot.Equipment.{Frost,Nature}Resistance` per char.
4. **Twin Emperors 50-yard separation**: encoded as raid invariant. Tanks must position twins on opposite sides of room before pull.
5. **C'Thun positioning**: Eye-Beam-spread = 10-yard distancing rule; Dark-Glare = 40-second rotational rule; Stomach = stomach-rotation rule; Giant-Claw-Tentacle = 4-tank rotation.
6. **Twin Emperors and C'Thun together**: requires guild-tier coordination (~6+ months of MC/BWL pre-clear). Engine should not attempt before BWL Nefarian killed.
7. **Atiesh chain bookkeeping**: per-character flag tracking for Splinter count, Anachronos quest, Staff Head + Base.
8. **Brood of Nozdormu rep grind**: parallelizable with Naxx + AQ20 + AQ40 weekly clears + Silithus daily questing.
9. **VMaNGOS-AQ40-incomplete flag**: server capability check pre-pull; defer to GM if encountering broken script.
10. **Scepter chain default**: bots do NOT pursue unless user-opt-in. Server-wide event; one-character-per-server completion.

---

## Snapshot Fields Needed

```text
Snapshot.Level == 60                            // hard gate
Snapshot.Class                                  // T2.5 set + Atiesh class gate
Snapshot.Faction                                // raid composition mix
Snapshot.Reputation.BroodOfNozdormu             // ring rewards + Atiesh chain
Snapshot.Equipment.FrostResistance              // Viscidus gate >= 150
Snapshot.Equipment.NatureResistance             // Huhuran gate >= 150
Snapshot.RaidGroup.Composition.Tanks            // Twin Emperors 2-tank, C'Thun 4-tank-claw
Snapshot.RaidGroup.Composition.FrostMages       // Viscidus shatter gate >= 5
Snapshot.RaidGroup.Composition.AvgItemLevel     // C'Thun gear gate >= 75
Snapshot.Inventory.SplinterOfAtieshCount        // Atiesh chain progress
Snapshot.Inventory.FrameOfAtiesh                // 40-Splinter combination
Snapshot.Inventory.StaffHeadOfAtiesh            // Naxx KT step
Snapshot.Inventory.BaseOfAtiesh                 // C'Thun step
Snapshot.QuestLog.Active.AtieshChain            // legendary chain progress
Snapshot.QuestLog.Active.ScepterChain           // optional Scepter chain (user opt-in)
Snapshot.WorldBuffs.Active                      // pre-raid stack
Snapshot.ServerCapabilities.AQ40Boss[<name>]    // VMaNGOS scripting flag
Snapshot.RaidGroup.PullState                    // Twin/C'Thun positioning sync
Snapshot.WallClock.WeeklyResetTimer             // lockout alignment
Snapshot.Equipment.T25Pieces                    // per-character set tracker
```

---

## Cross-References

- Naxxramas raid (Atiesh chain co-step): [naxxramas.md](naxxramas.md)
- Other raids: [molten-core.md](molten-core.md), [onyxias-lair.md](onyxias-lair.md), [blackwing-lair.md](blackwing-lair.md), [zul-gurub.md](zul-gurub.md)
- AQ20 (Ruins of Ahn'Qiraj — Brood of Nozdormu rep accelerator, pending): [ruins-of-ahn-qiraj.md](ruins-of-ahn-qiraj.md)
- Cenarion Circle rep (AQ40 friendly gate): [../reputations/](../reputations/)
- Brood of Nozdormu rep: [../reputations/](../reputations/) (file pending)
- World buffs (pre-raid): [../systems/world-buffs.md](../systems/world-buffs.md)
- Consumables (resistance flasks): [../systems/consumables.md](../systems/consumables.md)
- Class-specific raid prep: [../classes/](../classes/) (per-class file)
- Final bracket leveling: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
