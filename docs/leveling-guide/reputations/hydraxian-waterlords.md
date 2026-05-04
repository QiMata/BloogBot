# Hydraxian Waterlords — Reputation Guide

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/guide/hydraxian-waterlords-reputation-wow-classic
> - https://www.icy-veins.com/wow-classic/hydraxian-waterlords-reputation-farming-guide
> - https://www.warcrafttavern.com/wow-classic/guides/hydraxian-waterlords-reputation-guide/
> - https://wowwiki-archive.fandom.com/wiki/Hydraxian_Waterlords (referenced via search)
> - https://wowpedia.fandom.com/wiki/Hydraxian_Waterlords (referenced via search)
>
> **Pass 2.** Some details (full Hydraxis quest chain prerequisite list, Honored→Revered intermediate quest names) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline.

## Identity

The **Hydraxian Waterlords** are a Water Elemental faction led by **Duke Hydraxis** on a remote island off the southeast coast of **Azshara**. They are the **only direct counter** to Ragnaros's Fire Lord influence, and Hydraxian rep is **required to summon Majordomo Executus and Ragnaros** in Molten Core.

**MC raid implication**: Without ≥7 Hydraxian-rep'd raid members carrying **Aqual Quintessence** (Honored) or **Eternal Quintessence** (Revered), a raid **cannot complete the Majordomo summon ritual** in MC. Without Majordomo's death, Ragnaros cannot spawn.

| Tier | CP needed (cumulative) | Reward unlock |
|---|---|---|
| Neutral | 0-2999 | Starting tier |
| Friendly | 3000-8999 | Hydraxis quest chain unlocks more steps |
| **Honored** | 9000-20999 | **Aqual Quintessence** (consumable, single-use; ~12-15s vendor cost from Duke Hydraxis after questline) |
| **Revered** | 21000-41999 | **Eternal Quintessence** (permanent, infinite uses; reusable across all MC clears) |
| Exalted | 42000+ | Cosmetic title + access to Hydraxian-themed flavor items |

Total rep needed for **Eternal Quintessence (Revered)**: **21,000 reputation**. Most active MC raiders reach Revered within 4-6 weekly raid cycles via boss-kill passive rep.

## Why Quintessence matters

In Molten Core, **Majordomo Executus is gated behind a 7-rune ritual** in Ragnaros's chamber (the lava pit at the end of the instance):

- 7 runes are placed around the pit, each surrounded by a fire elemental boss-band
- Each rune must be **doused** by a player using Aqual or Eternal Quintessence
- After all 7 runes douse, Majordomo Executus spawns and is fightable
- After Majordomo dies, players get to interact with him (he becomes friendly post-fight) → using a Sulfuron Ingot from Sulfuron Harbinger triggers Ragnaros's spawn

**The 7-Quintessence requirement** is why **at least 7 different raid members must have Hydraxian Honored+** before MC can full-clear. Engine should ensure this minimum is met across the raid roster.

## Reputation gain methods

### Method 1 — MC boss kills (primary endgame source, all the way to Exalted)

Each MC boss grants Hydraxian rep on kill, scaling with boss difficulty. **All MC boss-kill rep continues to Exalted** (no diminishing return at Honored/Revered):

| Boss | Rep on kill |
|---|---|
| **Lucifron** (1st boss) | 100 rep |
| **Magmadar** (2nd) | 100 rep |
| **Gehennas** | 100 rep |
| **Garr** | 100 rep |
| **Baron Geddon** | 100 rep |
| **Shazzrah** | 100 rep |
| **Sulfuron Harbinger** | 100 rep |
| **Golemagg the Incinerator** | 150 rep |
| **Ragnaros** | 200 rep |

**Per full-clear MC**: 100×7 + 150 + 200 = **1050 rep per kill of all 9 bosses**. To reach Honored from Neutral takes ~9 full clears (~6-9 weekly resets); Revered ~ 20 full clears (~5-6 months).

**Engine planning**: Hydraxian rep accrues passively from MC participation. Engine doesn't need to explicitly farm rep — it's a side-effect of normal raid attendance.

### Method 2 — Hydraxis questline

**Duke Hydraxis** in Azshara (~58, 80 — remote island in southeast coast of Azshara — accessed by swimming or via Hydraxian-summoned water dragon) gives a **multi-step questline** that:

1. Introduces the player to Hydraxian Waterlords (~500 rep first quest)
2. Sends the player to kill specific Elemental Lord mobs in Burning Steppes / Silithus / Un'Goro (each step = ~250-1000 rep)
3. Culminates in the **Aqual Quintessence reward quest** (Honored gate)

**Total questline rep**: ~2000-3000 rep, gets a fresh-60 from Neutral to Friendly with ease, and provides the Aqual Quintessence quest item upon Honored.

**Engine planning**: Run the Hydraxis questline once at lvl 60 if planning MC participation. ~2-3 hours /played one-time investment.

### Method 3 — Item turn-ins (limited)

`[verify pass 3 — vanilla 1.12.1 had limited item turn-ins for Hydraxian rep, mostly elemental cores from Burning Steppes / Silithus mobs]`

## Aqual Quintessence vs Eternal Quintessence

| Feature | Aqual Quintessence | Eternal Quintessence |
|---|---|---|
| Tier required | Honored | Revered |
| Vendor cost | ~12-15s from Duke Hydraxis `[verify pass 3]` | Free / quest reward |
| Stack size | 1 (consumable) | 1 (no stack — single-charge per use) |
| Charges | 1 | **infinite** (1 use per cooldown, ~3-4 day cooldown? `[verify]`) |
| Use case | Single-MC-clear; must repurchase weekly | Permanent — reusable across all MC clears for the lifetime of the character |

**Engine optimization**: Aqual Quintessence is **always purchased fresh before each MC raid** (Honored characters) — they decay/expire. Eternal Quintessence is a **one-time item** that persists indefinitely.

**Scaling strategy**: Engine plans 7 Aqual Quintessence purchases pre-raid for Honored chars. Once each char reaches Revered, swap to Eternal Quintessence (permanent).

## Engine planning rule — Hydraxian as MC raid prerequisite

The DecisionEngine should treat Hydraxian rep as **part of the MC attunement chain in practice**:

1. Lvl 60 + planning MC participation: **run Hydraxis questline → Friendly minimum**
2. Pre-MC raid: ensure **≥7 raid members have Aqual or Eternal Quintessence** in inventory
3. Post-MC raid (after Honored): **purchase Aqual Quintessence at Hydraxis** for next week's raid (~12-15s)
4. Long-term: push to **Revered for Eternal Quintessence** (permanent — saves weekly Aqual purchases and ensures non-decay)

## VMaNGOS / private server notes

- **Hydraxis questline** is fully scripted on VMaNGOS; the remote-island Azshara location is correctly placed.
- **Boss-kill rep gains** in MC are correctly itemized (100/150/200 per-boss tier).
- **Aqual Quintessence** vendor stocking + Eternal Quintessence quest reward work correctly.
- **7-rune douse mechanic** at Ragnaros's pit is correctly scripted; Majordomo summons after all 7 runes doused.

## Decision-Engine Rules

- **id:** `rep.hydraxian.questline-start` — IF `Level==60 && Reputation[HydraxianWaterlords] < Friendly && Account.MCRaiderPlanned` THEN run Duke Hydraxis Azshara questline. Priority **600**.
- **id:** `rep.hydraxian.aqual-pre-mc` — IF `Reputation[HydraxianWaterlords] >= Honored && Raid.MCScheduled && !Items.Contains(AqualQuintessence) && !Items.Contains(EternalQuintessence)` THEN purchase Aqual Quintessence from Duke Hydraxis pre-raid. Priority **800** (raid-prep critical-path).
- **id:** `rep.hydraxian.eternal-target` — IF `Reputation[HydraxianWaterlords] >= Honored && Reputation[HydraxianWaterlords] < Revered && Account.MCRaiderActive` THEN continue MC raid attendance for boss-kill rep accumulation. Priority **400** (background; bundles with raid).
- **id:** `rep.hydraxian.eternal-acquire` — IF `Reputation[HydraxianWaterlords] >= Revered && !Items.Contains(EternalQuintessence)` THEN visit Duke Hydraxis to claim Eternal Quintessence. Priority **750** (one-time; replaces weekly Aqual purchase).
- **id:** `rep.hydraxian.raid-prereq-7-members` — IF `Raid.MCScheduled && Sum(RaidMembers.HasQuintessence) < 7` THEN raid leader / engine warns about insufficient douse coverage. Priority **999** (critical raid blocker — ritual cannot complete).

## Snapshot Fields Needed

- `Level`, `Class` (existing)
- `Reputation[HydraxianWaterlords]` (existing — generic Reputation table)
- `Items.Contains(AqualQuintessence)` / `Items.Contains(EternalQuintessence)` (planned bag scan)
- `Raid.MCScheduled` (planned config)
- `Account.MCRaiderPlanned` / `Account.MCRaiderActive` (planned account flags)
- `Sum(RaidMembers.HasQuintessence)` (planned — derived from raid roster scan)

## Cross-references

- [attunements/molten-core.md](../attunements/molten-core.md) — MC attunement (separate from Hydraxian rep; both required)
- [reputations/argent-dawn.md](argent-dawn.md) — pattern reference (similar high-importance rep grind)
- [decision-engine/state-flags.md](../decision-engine/state-flags.md) — `Reputation[HydraxianWaterlords]` field
- [systems/world-buffs.md](../systems/world-buffs.md) — Songflower / Onyxia head etc. world buffs stack with MC raid; Hydraxian is the rep gate, not a buff
- [raids/molten-core.md](#) (pass 5) — full MC raid encounter list with boss mechanics + Quintessence usage
- [zones/azshara.md](#) (pass 3) — Duke Hydraxis location + zone overview
