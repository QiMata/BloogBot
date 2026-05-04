---
title: "Dungeon — Sunken Temple (Temple of Atal'Hakkar)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Sunken_Temple
crawl_date: 2026-05-01
---

# Sunken Temple (5-man) — Atal'Hakkar Trolls + 6-Statue Puzzle + Eranikus

5-man dungeon at the bottom of Swamp of Sorrows. **Crescent Key required** (Pusillin chase in DM East entrance area — covered in [05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key)). 7+ bosses including the iconic **6-statue activation puzzle** that summons Avatar of Hakkar. Sweet spot **lvl 50-56**. Notable drops: **Eye of the Dead** (Priest class trinket), **Stave of Equinex** (Mage class staff), **Mosh'aru Tablets** (ZG Hakkar attune chain — 2-3 tablets), **Drakeclaw Band** ring, **Atal'ai Insignia** (faction reagent). 2-3 hour full clear.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Group size | 5-man |
| Level range | 50-60 (optimal 52-58) |
| Lockout | None (no instance reset) |
| Continent / Zone | Eastern Kingdoms — Swamp of Sorrows (south of Stonard) |
| Faction | Both (cross-faction) |
| Theme | Atal'ai sect troll temple of Hakkar the Soulflayer |
| **Attune required** | **Crescent Key** from Pusillin (DM East entrance area) |
| Notable drops | **Eye of the Dead** (Priest trinket), **Stave of Equinex** (Mage staff), **Mosh'aru Tablets** (ZG attune), **Drakeclaw Band** Hunter/Ret ring, **Robes of the Lich** Mage cloth |
| Boss count | 7+ bosses including 6-statue puzzle event |

---

## Crescent Key Requirement

Sunken Temple's outer gate requires the **Crescent Key**:

| Step | Action | Source |
|------|--------|--------|
| 1 | Travel to **Dire Maul East entrance area** (lvl 44+) | DM East exterior |
| 2 | Find **Pusillin** (lvl 44 satyr, runs in fixed loop) | DM East entrance |
| 3 | Catch Pusillin (he stops at 5 designated spots; hand him items he asks for) OR **DPS-rush** to 30% HP | One-time chain |
| 4 | Receive **Crescent Key** (BoP, 1-time pickup; one per character) | Reward |
| 5 | Use Crescent Key on Sunken Temple outer gate | Entry unlock |

**Decision-engine rule:** if `Snapshot.Inventory.Has("CrescentKey") == false && Level >= 48`, route to DM East entrance for Pusillin chase. See [05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key).

---

## Pre-Quest Chains (Cenarion Hold + Tanaris)

Before entering ST, multiple long quest chains terminate inside:

| Chain | Source NPC | Reward |
|-------|------------|--------|
| **Forging the Mighty Mallet** | Yeh'kinya (Tanaris) | Mallet of Zul'Farrak component for Atal'ai summoning |
| **The Atal'ai Exile** chain | Stonard (Swamp of Sorrows) | Pre-cleanse setup; multi-step long chain |
| **Eranikus, Tyrant of the Dream** chain | Shen'dralar (DM West) — Druid-class chain | Trinket reward + opens Eranikus boss |
| **Hakkari Loa Pendant** | ZG-related; multiple NPCs | Mosh'aru Tablet integration |

**Decision-engine rule:** ST pre-quest chains are bracket-defining. Engine should always pickup Yeh'kinya + Stonard quest hubs before entering.

---

## Boss Order (Standard Run)

| Order | Boss | Difficulty | Wing |
|-------|------|-----------|------|
| 1 | **Atal'alarion** *(optional, opt-in summon)* | Hard | Outer pyramid bottom (skip-able) |
| 2 | **Avatar of Hakkar** *(via 6-statue puzzle)* | Medium-Hard | Inner sanctum |
| 3 | **Atal'ai Defenders** event | Medium-Hard (multi-wave) | Center sanctum |
| 4 | **Jammal'an the Prophet** + **Ogom the Wretched** *(paired pull)* | Hard | Inner sanctum (paired boss) |
| 5 | **Hakkari Bloodkeeper** | Easy-Medium (sub-boss) | Pre-Eranikus chamber |
| 6 | **Hakkari Minions** (Atal'ai Defenders waves) | Medium | Various trash sections |
| 7 | **Shade of Eranikus** *(optional, requires Eranikus quest)* | Hard | Final chamber (deep) |

**Decision-engine rule:** ST is **non-linear** — multi-entry choices. Engine should pre-determine target loot:
- Mosh'aru Tablets + Class trinkets → 6-statue puzzle + Jammal'an + Ogom (mandatory)
- Eranikus → Druid quest active + Eranikus opt-in
- Atal'alarion → Atal'ai Idol pieces required (2-3 raid runs to assemble)

---

## Boss 1: Atal'alarion (Optional Opt-In)

| Field | Value |
|-------|-------|
| HP | ~50k (highest in instance) |
| Phases | None |
| Mechanic | Massive single-target boss summoned via 4 **Atal'ai Idol** pieces collected from outdoor Atal'ai trolls (Swamp of Sorrows + Tanaris); only summonable once Idols assembled; Tank-and-spank with **Earthquake** AoE knockback every 30s |
| Notable drops | **Atal'ai Spaulders** (caster cloth shoulders); **Hammer of the Northern Wind** (1H mace); **Talisman of Evasion** (Rogue trinket) |

**Decision-engine rule:** Atal'alarion is **opt-in farming**. Most ST runs skip due to Idol collection time. Engine should track per-character Atal'ai Idol piece count.

---

## Boss 2: 6-Statue Puzzle → Avatar of Hakkar

This is **THE** ST signature mechanic.

### Setup

In the inner sanctum, **6 dragon statues** stand in a circle around a central altar. Each statue corresponds to one of the 6 **Atal'ai High Priests** (Hakkar's lieutenants).

### Activation Sequence

| Statue | High Priest target | Activation order |
|--------|-------------------|------------------|
| 1 | Jammal'an | First |
| 2 | Hethiss | Second |
| 3 | Loroxx | Third |
| 4 | Mijan | Fourth |
| 5 | Thekal | Fifth |
| 6 | Zolo | Sixth |

**Note:** the order of activation cycles per server instance reset; raid must determine order via trial-and-error or via wiki-confirmed pattern. Failure to activate in correct order **resets puzzle** (15-minute cooldown).

### Avatar of Hakkar Encounter

Once all 6 statues activated correctly:
- **Avatar of Hakkar** spawns at center altar
- **Massive HP**: ~60k
- **Phase 1**: Tank-and-spank with **Drain Mana** every 20s (caster mana drain); **Drain Health** debuff on tank (50% healing reduction)
- **Phase 2 (50% HP)**: Avatar **summons Spawn of Hakkar** adds (4 lvl 55 elites); raid AoE adds while tank holds Avatar

### Notable drops

| Item | Class | Stat |
|------|-------|------|
| **Stave of Equinex** | Mage class staff | +30 Spell Damage + Int + Stamina (BiS pre-raid Mage staff) |
| **Hakkari Loa Cape** | Cloth caster cloak | +20 Healing Power |
| **Atal'ai Necklace** | Caster trinket | +25 Mana Regen |
| **Mosh'aru Tablets** | Quest reagent | 2 of 5 needed for ZG Hakkar attune |

**Decision-engine rule:** 6-statue puzzle is **the** ST mandatory event. Engine should encode statue activation order as a 6-step sequence; failure = 15-min wait. Stave of Equinex is Mage class quest reward.

---

## Boss 3: Atal'ai Defenders Event

After Avatar of Hakkar:

| Wave | Adds | Notes |
|------|------|-------|
| 1 | 4 Atal'ai Skirmishers | Trash-tier |
| 2 | 4 Atal'ai Slavemasters | More HP |
| 3 | 4 Atal'ai Witch Doctors | Casters with healing |
| 4 | 4 Atal'ai Scrubs + 1 Hakkari Bloodkeeper | Mid-boss spawn |
| Final | **Hakkari Bloodkeeper** | ~30k HP; physical attacker |

**Notable drops:**
- **Robes of the Lich** (Mage cloth chest)
- **Drakeclaw Band** (ring — Hunter/Pal Ret)
- **Caress of the Underground** (Hunter trinket)
- **Atal'ai Insignia** (Cenarion Circle reagent)

**Decision-engine rule:** Atal'ai Defenders event is **non-skippable** — gates Jammal'an/Ogom path. Engine should encode wave-spawn timing.

---

## Boss 4: Jammal'an the Prophet + Ogom the Wretched (Paired Pull)

**Both bosses pull together** when raid enters mid-sanctum.

| Boss | HP | Mechanic |
|------|-----|----------|
| **Jammal'an the Prophet** | ~30k | **Mind Control** (random raid 8s); **Hex of Jammal'an** (curse, dispel); caster — interrupt Heal cast |
| **Ogom the Wretched** | ~28k | Physical melee + **Cleave** front-of-room; **Whirlwind** every 30s (raid stays out of melee); off-tank pickup |

### Strategy

- Tank holds Ogom, off-tank holds Jammal'an (or main tank rotates)
- Designated DPS focuses Jammal'an for Mind Control + interrupt
- **Eye of the Dead** (Priest class trinket) is highest-priority loot — drops from this combined fight

### Notable drops

| Item | Class | Stat |
|------|-------|------|
| **Eye of the Dead** | Priest class trinket | +30 Healing on heal cast (proc); BiS pre-raid Priest healing |
| **Headmaster's Charge** | Mage staff (rare) | +30 Int + Spell Crit (drops from Jammal'an) |
| **Drakeclaw Band** (alt drop) | Ring | +Hit/+Crit (Hunter/Pal Ret) |
| **Yenniku** dagger (Rogue) | 1H dagger | Mid-tier Rogue Combat dagger |

**Decision-engine rule:** Jammal'an + Ogom paired pull = 2-tank fight if HP-tight; alternatively single-tank rotation. Engine should encode tank-swap on Mortal Wound stack ≥ 3.

---

## Boss 5: Shade of Eranikus (Optional Final Boss)

**Eranikus** is the dungeon's final boss but **only appears if Eranikus quest is active** (Druid class chain from Shen'dralar in DM West).

| Field | Value |
|-------|-------|
| HP | ~40k |
| Phases | None |
| Mechanic | **Sleep Burst** AoE (random raid sleep, 6s); **Frost Resistance helps** (~50 FR per raider); **Aspect of the Tyrant** debuff (50% damage taken increase, dispel-needed); **Whirlwind** AoE every 45s |
| Notable drops | **Cloak of the Hakkari Worshippers** (caster cloak); **Crystal-Forged Sword** (1H sword); **Druid Lifegiving Seed** trinket (Druid quest reward — tied to Eranikus chain) |

**Decision-engine rule:** Eranikus only spawns if Druid in raid has Eranikus quest active. Engine should pre-flag Druid alts pursuing this chain.

---

## Mosh'aru Tablets (ZG Attune Chain)

ST drops **2-3 of the 5 Mosh'aru Tablets** required for Zul'Gurub Hakkar attune. The remaining 2-3 tablets come from ZF Pyramid Event.

| Tablet | Source within ST |
|--------|------------------|
| Tablet 1 | 6-statue puzzle (Avatar of Hakkar fight) |
| Tablet 2 | Jammal'an / Ogom paired pull |
| Tablet 3 (optional) | Atal'ai Defenders event final |

**Decision-engine rule:** ST + ZF together complete the ZG attune chain. Engine should track per-character Mosh'aru count.

See [zul-farrak.md](zul-farrak.md) for ZF tablets and [../raids/zul-gurub.md](../raids/zul-gurub.md) for ZG attune.

---

## Class Quest Targets

| Class | Reward in ST | Quest source |
|-------|--------------|---------------|
| **Priest** | **Eye of the Dead** trinket (BiS pre-raid healer trinket) | "The Eye of the Dead" chain (multi-zone, ends in ST) |
| **Mage** | **Stave of Equinex** caster staff | "The Stave of Equinex" chain (Sunken Temple item turn-in) |
| **Druid** | **Lifegiving Seed** trinket (sometimes) | Eranikus chain via Shen'dralar (DM West origin) |
| **Hunter** | Drakeclaw Band (ring) | Standard loot |
| **Warrior** | Various 1H + cloth caster trinkets (drops) | Standard loot |

**Decision-engine rule:** ST is **the** L50 class-quest target. Engine should always plan ST run for Priest + Mage class chains pre-raid.

---

## VMaNGOS / Server Reality Check

ST is **mostly scripted** on VMaNGOS-tier servers. Edge-case bugs:

| Boss | Common scripting issue |
|------|------------------------|
| 6-statue puzzle | Statue activation order randomization across server resets |
| Avatar of Hakkar | Drain Mana scaling; Spawn add aggro |
| Jammal'an + Ogom | Mind Control AI; dual-pull aggro |
| Atal'ai Defenders | Wave timing + Hakkari Bloodkeeper trigger |
| Shade of Eranikus | Sleep Burst RNG; dispel logic |

**Decision-engine rule:** ST script-completeness check pre-pull. Eranikus chain in particular has historical bugs (Druid quest state desync).

---

## Decision-Engine Rules

1. **Crescent Key precondition**: required for ST entry. Engine should ensure `Snapshot.Inventory.Has("CrescentKey") == true` pre-pull.
2. **6-statue puzzle**: encode 6-step activation sequence; if pattern wrong, 15-min reset. Engine should query server-known pattern or trial-and-error log.
3. **Pre-quest chains**: pickup at Yeh'kinya (Tanaris) + Stonard (Swamp of Sorrows) + Shen'dralar (DM West) for Eranikus.
4. **Mosh'aru Tablet tracking**: per-character count; ZF + ST together = 5 tablets for ZG attune.
5. **Class quest priority**:
   - Priest: Eye of the Dead trinket (mandatory pre-raid)
   - Mage: Stave of Equinex (BiS staff)
   - Druid: Eranikus trinket (chain-active only)
6. **Atal'alarion farming**: opt-in only, requires Idol assembly (~3-5 ST runs). Engine should mark as low-priority sub-objective.
7. **Eranikus opt-in**: only if Druid in raid has chain active. Engine should pre-flag.
8. **Lockout-free**: ST resets on log-out. Engine can run multiple back-to-back for class quest farming.

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 50-60 entry gate
Snapshot.Class                                    // class trinket targeting
Snapshot.Inventory.Has("CrescentKey")             // entry gate
Snapshot.Inventory.MoshAruTabletCount             // ZG attune progression (target 5)
Snapshot.Inventory.AtalAiIdolCount                // Atal'alarion summoning gate (4 needed)
Snapshot.QuestLog.Active.EyeOfTheDead             // Priest class quest tracking
Snapshot.QuestLog.Active.StaveOfEquinex           // Mage class quest tracking
Snapshot.QuestLog.Active.EranikusChain            // Druid class quest tracking
Snapshot.Inventory.Has("EyeOfTheDead")            // Priest trinket signal
Snapshot.Inventory.Has("StaveOfEquinex")          // Mage staff signal
Snapshot.Boss.AvatarOfHakkar.Killed               // 6-statue puzzle completion
Snapshot.Boss.JammalanAndOgom.Killed              // mid-sanctum completion
Snapshot.Boss.ShadeOfEranikus.Killed              // optional final
Snapshot.RaidGroup.Composition.Tanks              // 1-2 tank for Jammal'an+Ogom
Snapshot.PartyComposition.Priest                  // Eye of the Dead loot priority
Snapshot.PartyComposition.Mage                    // Stave of Equinex loot priority
Snapshot.PartyComposition.Druid                   // Eranikus chain priority
Snapshot.ServerCapabilities.STBoss[<name>]        // VMaNGOS scripting flag
```

---

## Cross-References

- ZF Mosh'aru Tablets (paired source): [zul-farrak.md](zul-farrak.md)
- ZG attune chain: [../raids/zul-gurub.md](../raids/zul-gurub.md)
- Crescent Key chain (DM East Pusillin): [../sections/05-l40-l50.md](../sections/05-l40-l50.md#sunken-temple-attune-crescent-key)
- Priest class quest (Eye of the Dead): [../classes/priest.md](../classes/priest.md)
- Mage class quest (Stave of Equinex): [../classes/mage.md](../classes/mage.md)
- Druid class quest (Eranikus chain via Shen'dralar): [../classes/druid.md](../classes/druid.md)
- L50-L60 bracket: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
- Other dungeons: [zul-farrak.md](zul-farrak.md), [maraudon.md](maraudon.md), [blackrock-depths.md](blackrock-depths.md), [upper-blackrock-spire.md](upper-blackrock-spire.md)
