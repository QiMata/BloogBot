---
title: "System — Spell Batching (1.12 Server Tick Mechanic)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Spell Batching — 1.12.1 Server Tick (~400ms Window) Exploitable Mechanic

1.12.1 has **~400ms server-side spell batching window** (compared to modern WoW's ~50ms tick rate). Spells cast within the same batch window are **processed together**, creating exploitable PvP mechanics absent in modern WoW. Critical for: **Polymorph break-on-damage** (poly + DoT can both land same batch), **Vanish escape** (Vanish + incoming damage same batch = Vanish succeeds), **Blink double-tap** (Blink can clear roots if same batch as root application), **Sap stun chaining** (Sap + damage same batch = Sap lands first), **Cyclone immunity** (Cyclone applies during incoming damage same batch). For PvE, batching is less critical but matters for raid healing dispel coordination + simultaneous AoE casts.

---

## What Is Spell Batching?

In 1.12.1, the server processes spell casts in **discrete batch ticks** (~400ms intervals). All spells cast within the same batch window are evaluated simultaneously when the batch fires.

**Modern WoW** (post-Cata 4.0+) processes spells in real-time (~50ms ticks), making per-spell ordering deterministic.

**1.12.1 batching consequence:** two spells cast within ~200ms of each other are likely processed together — neither is "first" in resolution order, so both effects apply.

### Batching Window Math

| Tick rate | Window | Effect |
|-----------|--------|--------|
| **1.12.1 servers** | ~400ms tick | Two spells within ~200ms = same batch |
| **Modern WoW** | ~50ms tick | Strict ordering of spell resolution |

**Decision-engine cue:** for PvP combat simulation, engine should model spell casts within ~200ms windows as **same-batch resolution**.

---

## Iconic PvP Implications

### 1. Polymorph Break-on-Damage Same-Batch

**Scenario:** Mage casts Polymorph on Hunter, while Hunter is mid-Aimed-Shot at Mage.

**Without batching (modern):** Aimed Shot lands first → Polymorph never applies (Mage is interrupted/CC'd first).

**With 1.12 batching:** if Polymorph cast and Aimed Shot land within ~200ms → both apply same batch:
- Polymorph applies (Hunter sheeped)
- Aimed Shot damage applies + breaks Polymorph
- Net effect: **Polymorph applied + immediately broken + DoT applied**
- Counterintuitive but valid

**Decision-engine rule:** Polymorph + simultaneous damage = both apply, then Polymorph breaks immediately.

### 2. Vanish Escape Same-Batch

**Scenario:** Rogue casts Vanish while Hunter is firing simultaneous Aimed Shot.

**Without batching (modern):** if Aimed Shot lands first, Vanish doesn't activate (interrupted/CC'd). If Vanish first, Rogue invisible immediately.

**With 1.12 batching:** Vanish + Aimed Shot same batch:
- Vanish applies (Rogue invisible)
- Aimed Shot damage applies but **target is now invisible** — damage may register on combat log but target is in stealth state
- Net effect: **Vanish succeeds despite incoming damage**

**Decision-engine rule:** Rogue can Vanish through incoming damage if cast in same batch. Critical PvP escape mechanic.

### 3. Blink Double-Tap (Roots Clear)

**Scenario:** Mage cast in roots; Mage Blinks to escape.

**Without batching (modern):** roots persist; Blink may or may not break roots based on stationary state.

**With 1.12 batching:** Blink + root application same batch:
- Roots apply (Mage rooted)
- Blink teleports — clears roots (Blink moves character)
- Net effect: **Roots cleared by Blink**

**Decision-engine rule:** Blink in same batch as root = roots cleared. Mage escape mechanic.

### 4. Sap Stun Chaining

**Scenario:** Rogue Saps (8s sleep) target who's taking damage.

**Without batching (modern):** Sap breaks on damage immediately.

**With 1.12 batching:** Sap + incoming damage same batch:
- Sap applies (target sleeping)
- Damage may apply but target is now in CC state — damage breaks Sap immediately
- Net effect: **Sap breaks immediately, but Sap technically applied**

**Caveat:** This is essentially the same as modern; Sap break-on-damage is consistent. **Batching effect is more pronounced for Polymorph/Vanish/Blink than Sap.**

### 5. Cyclone Immunity Application

**Scenario:** Druid casts Cyclone (immunity 6s) while taking incoming damage.

**Without batching (modern):** Cyclone may or may not apply if damage is interrupting.

**With 1.12 batching:** Cyclone + incoming damage same batch:
- Cyclone applies (target in immunity state)
- Damage processed but **target now immune** — damage may register on combat log but target unaffected
- Net effect: **Cyclone wins same-batch; damage absorbed by immunity**

**Decision-engine rule:** Cyclone in same batch as damage = Cyclone wins, target enters immunity state.

### 6. Counterspell Coordination

**Scenario:** Two Mages both Polymorphing each other simultaneously.

**Without batching:** First Polymorph lands; second is interrupted.

**With 1.12 batching:** Both Polymorphs same batch:
- Both Polymorphs apply (both Mages sheeped)
- Net effect: **Mutual Polymorph** (both characters CC'd)

**Decision-engine rule:** simultaneous CC casts in same batch = mutual CC, both casters affected.

### 7. Death + Resurrection Batch

**Scenario:** Player dying simultaneously with self-Resurrect (e.g., Soulstone, Ankh).

**Without batching:** Death first, then Soulstone resurrect.

**With 1.12 batching:** death + soulstone same batch:
- Death applies (player dies)
- Soulstone resurrect applies (player rezzes immediately)
- Net effect: **No corpse run** — player is resurrected immediately at death location

**Decision-engine rule:** Soulstone + simultaneous death = instant rez at death location.

---

## PvE Implications

### Raid Healing Dispel Coordination

In raid encounters where multiple raiders need dispels simultaneously:

| Scenario | Without batching | With 1.12 batching |
|----------|------------------|---------------------|
| Two healers dispel different raid members | Strict ordering — only one dispel per server tick | **Both dispels apply same batch** — both raid members cleansed simultaneously |

**Decision-engine cue:** raid dispel timing in 1.12.1 is more forgiving — multiple healers can dispel within same batch and both apply.

### Simultaneous AoE Cast

In raid encounters with multiple AoE casts (e.g., Heigan dance, Razorgore Mind Control):

| Scenario | Without batching | With 1.12 batching |
|----------|------------------|---------------------|
| Multiple Mages cast Frost Nova simultaneously | Strict ordering — first Frost Nova roots, others stack damage | **All Frost Novas apply same batch** — all targets rooted by all Mages simultaneously |

### Heigan Dance (Naxx Plague Wing)

**The Heigan dance is a positional batching test** — eruption sequences are server-tick-aligned with ~400ms windows. Players who move in sync within ~200ms of eruption fire are safe; players outside the window die.

**Decision-engine rule:** Heigan dance encoding requires ~400ms tick simulation. Engine should align dance waypoint with batching tick.

---

## Decision-Engine Implications

### PvP Bot Behavior

| Mechanic | Engine modeling required |
|----------|--------------------------|
| **Polymorph break-on-damage** | Model same-batch DoT application + break |
| **Vanish escape** | Model Vanish + incoming damage same-batch → Vanish wins |
| **Blink double-tap** | Model Blink + root application same-batch → roots clear |
| **Sap chaining** | Model Sap + incoming damage same-batch → Sap break |
| **Cyclone immunity** | Model Cyclone + damage same-batch → Cyclone wins |
| **Counterspell coordination** | Model mutual CC scenarios |

### PvE Bot Behavior

| Mechanic | Engine modeling required |
|----------|--------------------------|
| **Heigan dance** | ~400ms eruption tick — encode dance waypoints with batch alignment |
| **Raid dispel coordination** | Model simultaneous dispel application |
| **Simultaneous AoE** | Model multiple AoE casts same-batch |
| **Soulstone same-batch** | Model death + resurrect same batch → instant rez |

---

## Decision-Engine Rules

1. **Server tick simulation**: engine should model ~400ms server-side batching window.
2. **Same-batch resolution**: spells cast within ~200ms = same batch processing.
3. **PvP CC mechanics**: Polymorph/Vanish/Blink/Cyclone all benefit from same-batch resolution. Engine should model exploitable PvP mechanics.
4. **Heigan dance encoding**: ~400ms tick alignment for raid mechanic.
5. **Raid dispel timing**: simultaneous dispels apply same-batch (more forgiving than modern).
6. **Soulstone same-batch**: death + Soulstone same batch = instant rez (no corpse run).
7. **Modern WoW emulation warning**: bot must NOT use modern ~50ms tick model. Engine should configurably set tick rate to 400ms for 1.12 emulation.
8. **Combat log analysis**: 1.12 combat logs may show out-of-order events due to batching — engine should not assume strict ordering.
9. **Cross-faction PvP coordination**: same-batch resolution affects cross-faction PvP encounters identically.
10. **VMaNGOS server-tick alignment**: VMaNGOS server should be configured for 1.12 batching (~400ms). Engine should `ServerCapabilities.SpellBatching == true` check.

---

## Snapshot Fields Needed

```text
Snapshot.Server.SpellBatchingEnabled              // 1.12 server-tick simulation
Snapshot.Server.TickRate                          // 400ms vs modern 50ms
Snapshot.Combat.CastQueue                         // pending spell cast queue (for batch resolution)
Snapshot.Combat.LastBatchFireTime                 // batch alignment tracking
Snapshot.PvPMatch.LastSpellEvents                 // batch-resolution event log
Snapshot.PvPMatch.OpponentCastIntent              // detect simultaneous CC casts
Snapshot.Class                                    // class-specific batching mechanics
Snapshot.Spells.LastBlinkTime                     // Mage Blink double-tap tracking
Snapshot.Spells.LastVanishTime                    // Rogue Vanish escape tracking
Snapshot.Spells.LastSapTime                       // Rogue Sap chaining
Snapshot.Spells.LastSoulstoneActive               // Warlock self-resurrect signal
Snapshot.RaidGroup.PullState.HeiganDanceTick      // Naxx encoding
```

---

## Cross-References

- VMaNGOS server-tick configuration: see project root [CLAUDE.md](../../CLAUDE.md)
- Naxxramas Heigan dance: [../raids/naxxramas.md](../raids/naxxramas.md)
- Mage class (Polymorph, Blink, Counterspell): [../classes/mage.md](../classes/mage.md)
- Rogue class (Vanish, Sap): [../classes/rogue.md](../classes/rogue.md)
- Druid class (Cyclone): [../classes/druid.md](../classes/druid.md)
- Warlock class (Soulstone): [../classes/warlock.md](../classes/warlock.md)
- PvP system (cross-batching considerations): [../pvp/honor-system.md](../pvp/honor-system.md)
- World buffs (pre-raid stack): [world-buffs.md](world-buffs.md)
