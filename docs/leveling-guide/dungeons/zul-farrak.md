---
title: "Dungeon — Zul'Farrak (ZF)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Zul%27Farrak
crawl_date: 2026-05-01
---

# Zul'Farrak (ZF, 5-man) — Mid-Game Sandfury Troll Pyramid

5-man dungeon in northwest Tanaris (visible pyramid from 1km away). **8+ bosses** including the iconic **Pyramid Event** (4 prisoners freed → NPC allies fight alongside, then Hydromancer Velratha appears, then Chief Ukorz Sandscalp). Sweet spot **lvl 44-50**. Notable drops: **Carrot on a Stick** trinket (3% mount speed), **Sandfury Cleaver** (1H axe), **Sang'thraze the Deflector** (rare Warrior tank shield), **Mosh'aru Tablets** (ZG Hakkar attune chain — 2 tablets needed). 90-min full clear with experienced group.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 44-54 (optimal 44-50) |
| Lockout | None (no instance reset; full clear ~90 min) |
| Continent / Zone | Kalimdor — Tanaris (northwest pyramid) |
| Faction | Both (cross-faction) |
| Theme | Sandfury Troll desert pyramid |
| Pre-quest | Multiple quests start at Gadgetzan + Steamwheedle Port (lvl 44+ Tanaris hubs) |
| Notable drops | **Carrot on a Stick** trinket, **Sandfury Cleaver** axe, **Sang'thraze the Deflector** shield, **Mosh'aru Tablets** (ZG attune), **Divino-matic Rod** caster wand |
| Sub-events | **Pyramid Event** (4 prisoner rescue + NPC ally fight) — bracket-defining encounter |

---

## Boss Order

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Antu'sul** | Easy-Medium | Outer-pyramid lower |
| 2 | **Theka the Martyr** | Medium (scarab phase) | Outer-pyramid mid |
| 3 | **Witch Doctor Zum'rah** | Medium-Hard (skeleton spawns) | Outer-pyramid mid |
| 4 | **Nekrum Gutchewer** | Easy | Outer-pyramid mid (alongside Shadowpriest Sezz'ziz) |
| 5 | **Shadowpriest Sezz'ziz** | Medium | Outer-pyramid mid (sub-boss) |
| 6 | **Sergeant Bly Pyramid Event** | Medium-Hard (multi-wave + NPC allies) | Pyramid stairs (4-prisoner rescue) |
| 7 | **Hydromancer Velratha** | Medium | Atop Pyramid (after Pyramid Event) |
| 8 | **Sandfury Executioner** | Easy | Atop Pyramid (small mob) |
| 9 | **Chief Ukorz Sandscalp** | Hard (final boss) | Atop Pyramid (final encounter) |
| Rare | **Gahz'rilla** | Hard (rare summon) | Pool basin (separate gateway via Sergeant Bly questline) |
| Rare spawn | **Sandarr Dunereaver** | Easy outdoor | Outdoor (spawns near pyramid; not inside) |

**Decision-engine rule:** ZF is the **first 5-man with a multi-NPC escort event**. Engine should encode Pyramid Event as 5-step waypoint sequence with NPC ally death tracking.

---

## Boss 1: Antu'sul (Sandfury Spirit)

| Field | Value |
|-------|-------|
| HP | ~12k |
| Phases | None |
| Mechanic | Sandfury Imps spawn during fight; raid AoE clears them; **Healing Wave** cast on Antu'sul (interruptible — DPS interrupt) |
| Notable drops | Antu'sul's Hand of Justice trinket (rare); Sandcrest of Antu'sul wand |

---

## Boss 2: Theka the Martyr (Scarab Swarm)

| Field | Value |
|-------|-------|
| HP | ~14k |
| Phases | Two — pre-scarab swarm + scarab phase |
| Mechanic | At ~50% HP, Theka **scarab-swarms** (~20 small scarabs spawn around boss); raid splits — tank holds Theka, off-target raid AoEs scarabs; **Plague Cleansing Totem** if Shaman (heal-restore mid-swarm); **scarab DPS race** before they overwhelm |
| Notable drops | **Theka's Tabard** (cosmetic); **Sandfury Sash** (Warrior cloth belt); **Bone Ring** caster ring |

**Decision-engine rule:** scarab swarm = AoE + interrupt rotation. Engine should encode "swarm trigger" at 50% HP and dispatch AoE-spec characters.

---

## Boss 3: Witch Doctor Zum'rah

| Field | Value |
|-------|-------|
| HP | ~16k |
| Phases | None (skeleton spawns continuously) |
| Mechanic | **Skeleton Sandfury** continually spawn from his ritual circle; he heals himself (Voodoo Healing Touch, interruptible); **Mind Control** on tank ~30s duration; raid must control adds + interrupt heal |
| Notable drops | **Zum'rah's Vexing Cane** (caster staff); **Plug of Stinging Plague** (Hunter trinket); **Witch Doctor Mask** (cosmetic) |

---

## Boss 4: Nekrum Gutchewer (sub-boss)

| Field | Value |
|-------|-------|
| HP | ~12k |
| Phases | None |
| Mechanic | Standard tank-and-spank with **Disease Cloud** AoE (1.5k damage random raid every 20s); raid spreads |
| Notable drops | **Hardened Shell** (Warrior tank shield); **Sandfury Drape** cloak |

---

## Boss 5: Shadowpriest Sezz'ziz (sub-boss)

| Field | Value |
|-------|-------|
| HP | ~14k |
| Phases | None |
| Mechanic | **Mind Blast** (~2k damage on random raid), **Shadow Word: Pain** (DoT); raid spreads + casters interrupt |
| Notable drops | **Robe of Shadow** (caster cloth); **Sezz'ziz's Soul-Twister** wand (1.12-niche caster) |

---

## Boss 6: Sergeant Bly Pyramid Event (the Iconic Encounter)

This is **the defining encounter** of Zul'Farrak.

### Setup

After clearing the outer pyramid (bosses 1-5), the raid approaches the pyramid stairs. **4 prisoners** (Raven, Murta Grimgut, Sergeant Bly, Ock'thar) appear at the top of the stairs as captives.

### Phases

| Phase | Description | Duration |
|-------|-------------|----------|
| Phase 1 | Wave 1: 4-6 Sandfury Skeletons spawn from base of stairs | ~30s |
| Phase 2 | Wave 2: Sandfury Witch Doctor + 3 trash spawn | ~45s |
| Phase 3 | Wave 3: Sandfury Slave Master + 4 trash | ~45s |
| Phase 4 | Wave 4: Larger pull (~6 trash + 1 Witch Doctor) | ~60s |
| **Climax** | **Sergeant Bly + 3 NPC allies (Raven/Murta/Ock'thar) descend stairs and fight alongside the raid** as final wave | NPC-driven |
| Post-event | If all 4 NPCs survive, post-event reward unlocks (each NPC gives a quest reward turn-in) | — |

### Notable mechanic

- **Each NPC ally has its own role** — Raven (warrior tank), Murta (mage DPS), Ock'thar (priest healer), Sergeant Bly (warrior leader)
- **Keep all 4 alive** for full reward turn-in
- After all waves cleared, Hydromancer Velratha appears at pyramid top

### Notable drops (event reward, not boss drop)

| Reward | NPC giver | Stat |
|--------|-----------|------|
| **Hex of Jin'do** | Sergeant Bly | Reagent for Mosh'aru Tablets |
| **Mosh'aru Tablets** (2) | Various NPC turn-ins | **ZG Hakkar attune (collect 2 of 5 needed)** |
| **Trinket: Bly's Will to Survive** | Sergeant Bly turn-in | Tank trinket (rare) |

**Decision-engine rule:** Pyramid Event is **the** ZF must-do. Engine should encode 4-NPC keep-alive invariant; failure means partial reward + missed Mosh'aru drop.

---

## Boss 7: Hydromancer Velratha

| Field | Value |
|-------|-------|
| HP | ~18k |
| Phases | None |
| Mechanic | **Water Spout** AoE (random raid 2k damage every 15s); **Frost Resistance gear helps**; **Heal cast** (interruptible); summon **Sandfury Water Elementals** as adds |
| Notable drops | **Sandfury Cleaver** (1H axe, melee BiS mid-bracket); **Velratha's Crested Helm** (mail Hunter helm); **Sandfury Watery Sting** wand |

**Decision-engine rule:** Sandfury Cleaver is **bracket-defining 1H weapon for melee characters**. Engine should prioritize Hydromancer kill loot.

---

## Boss 8: Sandfury Executioner (sub at pyramid top)

| Field | Value |
|-------|-------|
| HP | ~10k |
| Phases | None |
| Mechanic | Tank-and-spank; **Cleave** front-of-room |
| Notable drops | **Headhunter's Mantle** (Hunter mail shoulders); **Executioner's Cleaver** (cross-class 2H axe) |

---

## Boss 9: Chief Ukorz Sandscalp (Final Boss)

| Field | Value |
|-------|-------|
| HP | ~22k |
| Phases | None |
| Mechanic | **Cleave** (front-of-room), **Mortal Strike** stack on tank (50% healing reduction per stack), **Disarm** on tank; tank-rotation when stack ≥ 3 |
| Notable drops | **Sandfury Pulldown** trinket (rare, +Stamina); **Sang'thraze the Deflector** (Warrior tank shield, BiS-mid-bracket); **Carrot on a Stick** (3% mount speed trinket — see below); **Plagueheart Helm** (Mage cloth helm) |

**Decision-engine rule:** Ukorz disarm = tank without weapon for 8 seconds; off-tank rotation invariant. Engine should encode tank-swap on Mortal Wound stack ≥ 3.

---

## Carrot on a Stick (Trinket)

**Carrot on a Stick** is a quest-reward trinket from ZF, NOT a boss drop:

| Step | Action | Source |
|------|--------|--------|
| 1 | Acquire **Stoley's Bottle** (drop from Tanaris hyenas, lvl 40+) | Outdoor farming |
| 2 | Turn in to **Yorus Barleybrew** (Tanaris) for "Stoley's Shipment" quest chain | Quest chain |
| 3 | Travel to ZF + complete a chain involving **Tanaris kobolds** and **Hyena loot** | Multi-step |
| 4 | Receive **Carrot on a Stick** trinket (+3% mount speed when equipped) | Trinket reward |

**Decision-engine rule:** Carrot on a Stick is **bracket-defining** before L60 epic mount. Engine should always pursue chain at L40-50.

---

## ZG Attune (Mosh'aru Tablets)

ZF Pyramid Event drops 2 of the 5 **Mosh'aru Tablets** required for Zul'Gurub Hakkar attune.

| Tablet | Source | Notes |
|--------|--------|-------|
| **Tablet 1** | ZF Pyramid Event (Sergeant Bly turn-in) | One of 2 from ZF |
| **Tablet 2** | ZF Pyramid Event (alternate NPC turn-in) | One of 2 from ZF |
| **Tablets 3-5** | Sunken Temple bosses + ZG raid | Multi-source |

**Decision-engine rule:** ZG attune chain spans ZF + Sunken Temple. Engine should track Tablet count per character; ensure ZF run captures both tablets via NPC keep-alive invariant.

See [../raids/zul-gurub.md](../raids/zul-gurub.md) for full ZG attune chain.

---

## VMaNGOS / Server Reality Check

ZF is **fully scripted** on most VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| Theka the Martyr | Scarab swarm spawn timing + AI |
| Sergeant Bly Pyramid | NPC ally pathing during waves; survival logic |
| Hydromancer Velratha | Water Elemental summon timing |
| Chief Ukorz | Mortal Strike stack reset on tank-swap |

**Decision-engine rule:** ZF script-completeness check pre-pull; almost always-Pass on modern VMaNGOS.

---

## Decision-Engine Rules

1. **Pre-quest priority**: Tanaris (Gadgetzan + Steamwheedle Port) quests pickup before ZF entry. Carrot on a Stick chain + Mosh'aru Tablet quests gate on these.
2. **Boss order**: Antu'sul → Theka → Zum'rah → Nekrum/Sezz'ziz → Pyramid Event → Hydromancer → Executioner → Chief Ukorz Sandscalp.
3. **Pyramid Event invariant**: keep all 4 NPC allies (Raven/Murta/Bly/Ock'thar) alive for full Mosh'aru Tablet rewards.
4. **Carrot on a Stick acquisition**: 3% mount speed at L40-50 = bracket-defining. Engine should always-pursue.
5. **Sandfury Cleaver priority**: melee 1H weapon BiS mid-bracket. Engine should DKP/loot-priority on Hydromancer kill.
6. **Mortal Strike tank rotation (Ukorz)**: tank-swap at stack ≥ 3. Engine should encode threat-management invariant.
7. **ZG attune progression**: parallel with ZF clears (2 tablets per run). Engine should track per-character Mosh'aru count.
8. **Group composition**: 1 tank, 1 healer, 3 DPS — standard 5-man. Pyramid Event has high AoE damage; AoE-DPS specs ideal.
9. **Lockout-free**: ZF resets on log-out. Engine can run multiple runs back-to-back.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 44-54 entry gate
Snapshot.Class                                    // role determination
Snapshot.PartyComposition.Tank                    // 1 tank required
Snapshot.PartyComposition.Healer                  // 1 healer required
Snapshot.PartyComposition.DPS                     // 3 DPS slot
Snapshot.QuestLog.Active.CarrotOnAStick           // Carrot quest chain progress
Snapshot.QuestLog.Active.MoshAruTablets           // ZG attune chain progress
Snapshot.Inventory.MoshAruTabletCount             // 0-5 ZG attune progression
Snapshot.Inventory.Has("CarrotOnAStick")          // 3% mount speed trinket
Snapshot.Inventory.Has("SandfuryCleaver")         // melee 1H BiS mid-bracket
Snapshot.Inventory.Has("SangthrazeDeflector")     // Warrior tank shield BiS-mid
Snapshot.RaidGroup.PyramidEvent.NPCsAlive         // 4-NPC keep-alive invariant
Snapshot.Boss.HydromancerVelratha.Killed          // Sandfury Cleaver loot signal
Snapshot.Boss.ChiefUkorz.Killed                   // final boss completion
Snapshot.ServerCapabilities.ZFBoss[<name>]        // VMaNGOS scripting flag
```

---

## Cross-References

- ZG attune chain (Mosh'aru Tablets): [../raids/zul-gurub.md](../raids/zul-gurub.md)
- Sunken Temple (other Mosh'aru tablet sources): not yet covered (pending)
- Tanaris zone (ZF entry hub): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#tanaris-lvl-40-50)
- Other dungeons: [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
- L40-L50 bracket: [../sections/05-l40-l50.md](../sections/05-l40-l50.md)
