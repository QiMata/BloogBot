# World Buffs — 1.12.1 Raid Prep System

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/spell=22888/rallying-cry-of-the-dragonslayer
> - https://warcraft.wiki.gg/wiki/Rallying_Cry_of_the_Dragonslayer
> - https://wowpedia.fandom.com/wiki/Rallying_Cry_of_the_Dragonslayer (referenced via search)
> - https://wowpedia.fandom.com/wiki/Warchief's_Blessing (referenced via search)
> - https://www.warcrafttavern.com/wow-classic/guides/world-buffs/
> - https://overgear.com/guides/wow-classic/wow-classic-complete-world-buffs-consumables-guide/
>
> **Pass 2.** Some details (Warchief's Blessing exact haste-rating-equivalent stat in 1.12 terms, DM tribute individual buff names) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Overview

World buffs are **persistent character buffs** triggered by world events or location-specific actions, lasting **30-120 minutes** and providing **massive raid DPS multipliers**. A fully world-buffed raid (Onyxia/Nef + Songflower + DM Tribute + Rend if Horde) parses **30-50% above baseline** on the same boss.

**Critical 1.12.1 buff cap**: A character can have **at most 16 buffs active at once** (changed to 32+ in TBC). World buffs and consumables compete for these slots — the engine must plan buff stacking carefully.

| Buff | Source | Effect | Duration | Faction |
|---|---|---|---|---|
| **Rallying Cry of the Dragonslayer** | Ony or Nef head turn-in (capital city event) | +140 AP, +10% spell crit, +5% melee/ranged crit | **2 hours** | Both |
| **Warchief's Blessing** | Rend Blackhand head turn-in to Thrall in Org | +300 HP, +10 mp5, +15% melee haste (`[verify pass 3]`) | **2 hours** | **Horde only** (Patch 2.0.1 removed Alliance access) |
| **Songflower Serenade** | Click Songflower bush in Felwood (after cleansing the corruption) | +15 all stats, +5% spell/melee/ranged crit | **1 hour** | Both |
| **Dire Maul Tribute** (combined Eldreth/Mol'dar/Slip'kik) | Run DM N tribute event (avoid killing tribute mobs to maximize King's Square reward) | +15% max HP, +200 AP, +3% spell crit | **2 hours** | Both |
| **Spirit of Zanza / Sheen of Zanza / Swiftness of Zanza** | ZG quartermaster (Zandalar Tribe rep, after ZG opens) | +50 stam/spi (Spirit), +25 spd power (Sheen), +10% movement (Swiftness) | **2 hours** | Both |

## Rallying Cry of the Dragonslayer (Ony/Nef Head)

**Triggered by**: A player turning in **Head of Onyxia** (drops from Onyxia raid) OR **Head of Nefarian** (drops from BWL Nefarian) to the appropriate faction NPC in Stormwind / Orgrimmar.

**Effect**: All players within the capital (Stormwind for Alliance, Orgrimmar for Horde) when the turn-in happens receive **Rallying Cry of the Dragonslayer**:
- **+140 melee Attack Power**
- **+10% spell critical strike chance**
- **+5% melee and ranged critical strike chance**
- **Duration: 2 hours**

**1.12.1 specifics**: Both Onyxia and Nefarian heads grant the **same buff**. Patch 3.2.2 (Wrath) later removed Onyxia's head from the buff trigger pool — but in 1.12.1, both work.

**Strategic use**: Guilds coordinate post-MC/BWL kill announcements 30-60 minutes before raid invite to ensure raid members are in capital for the buff trigger. Players outside the capital at turn-in time miss the buff.

**VMaNGOS scripting**: This buff triggers correctly on most VMaNGOS forks. The "head turn-in NPC announcement" raid-wide system works.

## Warchief's Blessing (Rend Blackhand head — HORDE ONLY)

**Triggered by**: A Horde player turning in **For The Horde!** (Rend Blackhand head from UBRS) to **Thrall** in Orgrimmar's Grommash Hold.

**Effect** (Horde players in Orgrimmar + Crossroads/Barrens at turn-in time):
- **+300 health**
- **+10 mana regeneration per 5 sec (mp5)**
- **+15% melee haste** `[verify pass 3 — original 1.12 text reads "+150 melee haste rating" but rating wasn't introduced until WotLK; in 1.12 was likely +15% melee attack speed]`
- **Duration: 2 hours**

**1.12.1 specifics**: **Alliance does not have an equivalent buff in 1.12.1** — Patch 2.0.1 (Dec 2006, post-1.12.1) removed Alliance Warchief's Blessing access entirely. In 1.12.1 the buff was theoretically Alliance-accessible if an Alliance player turned in Rend's head (extremely rare since it required killing the Horde-aligned Rend), but in practice always Horde.

**Strategic use**: Same as Rallying Cry — guilds coordinate Rend kill turn-ins with raid invite windows.

## Songflower Serenade (Felwood)

**Triggered by**: Clicking a **Songflower bush** in Felwood after the **Corrupted Songflower** has been cleansed by killing the corrupted plant nearby.

**Effect**:
- **+15 all stats** (Strength, Agility, Stamina, Intellect, Spirit)
- **+5% melee / ranged / spell critical strike chance**
- **Duration: 1 hour**

**Locations**: Several Songflower bushes scattered across Felwood (~6 known spawn points). Each bush:
- Stays cleansed for 2 hours after corruption kill
- Loot-able once per 25 minutes per bush during that 2-hour window

**Strategic use**: Songflower is **easier to obtain than dragon heads** but lasts only 1 hour. Engine should plan Songflower pickup ~30 minutes before raid pull (allows travel time + first 30 min of raid).

**VMaNGOS scripting**: Songflower spawns and loot timer work correctly.

## Dire Maul Tribute (Combined Eldreth + Mol'dar + Slip'kik)

**Triggered by**: Completing a **DM N tribute run** — entering Dire Maul North, killing King Gordok at the end *without killing the tribute-named mobs* (Eldreth, Mol'dar, Slip'kik, etc.). Each mob you leave alive contributes a buff to the post-King's-Reckoning chest.

**Effect (combined max-tribute)**:
- **+15% max HP**
- **+200 melee Attack Power**
- **+3% spell critical strike chance**
- **Duration: 2 hours**

**Tactical complexity**: DM N tribute is a **5-man dungeon run** that takes 30-45 minutes. Each tribute mob skipped reduces the run time and gives more buffs in the post-fight chest. Engine should plan a dedicated DM tribute run ~60-90 minutes before raid invite.

**VMaNGOS scripting**: DM N tribute event has had occasional script issues on private servers (tribute book despawn timing); engine plans 1-2 attempts. `[verify VMaNGOS scripting status pass 3]`

## Spirit / Sheen / Swiftness of Zanza (ZG Reward)

**Triggered by**: Zandalar Tribe rep quartermaster after ZG opens (the 20-man raid in Stranglethorn). Requires ZG turn-in items and a small gold cost.

**Effects** (each is a separate buff, can stack):
- **Spirit of Zanza**: +50 Stamina, +50 Spirit
- **Sheen of Zanza**: +25 spell damage and healing
- **Swiftness of Zanza**: +10% movement speed (out-of-combat only)
- **Duration: 2 hours each**

**Strategic use**: These buffs are **always-available** post-ZG opening (no boss kill required, just rep + gold). They stack with Rallying Cry / Songflower / DM tribute. Engine should plan Zanza buffs as the cheapest mass-stack option.

## Stacking strategy (full pre-raid buff stack)

Maximum world-buff DPS stack going into MC/BWL/AQ40:

| Slot | Buff | Source |
|---|---|---|
| 1 | **Rallying Cry of the Dragonslayer** | Ony/Nef head |
| 2 | **Warchief's Blessing** (Horde only) | Rend head |
| 3 | **Songflower Serenade** | Felwood |
| 4 | **DM Tribute combined** | Dire Maul N |
| 5 | **Spirit of Zanza** | ZG vendor |
| 6 | **Sheen of Zanza** | ZG vendor |
| 7 | **Swiftness of Zanza** | ZG vendor (out-of-combat only — drops on combat) |
| 8-16 | Class buffs (Mark of Wild, PWFort, BoK, BoM/BoW, Battle Shout, etc.) + consumables (flasks, elixirs, food) | Various |

A fully stacked Horde melee DPS at lvl 60 can reach **+340 AP, +13% melee crit, +13% spell crit, +15% max HP, +15% melee haste** before consumables are added. This is the difference between "strong" and "world-record" parses.

## Buff loss / decay rules

- **Death**: All world buffs lost on death (no "Spirit of Redemption" preservation in 1.12)
- **Hearthstone**: World buffs **persist through hearthstone** in 1.12 (changed in Classic 2019 re-release where some forks added the "lose buffs on hearth" mechanic)
- **Logout / login**: World buffs persist (timer continues to count down)
- **Server reset**: World buffs persist if server resets are scheduled — VMaNGOS handles this correctly
- **Dispels**: Some buffs (Songflower) are dispellable; world boss buffs typically aren't

## Engine planning rule

The DecisionEngine should treat world buffs as a **pre-raid prep window** action class:

1. **Identify scheduled raid time** (configured externally).
2. **Backward-plan from raid pull** (T-0):
   - T-90 minutes: DM Tribute run start (45 min) → arrive at raid stack with ~75 min DM buff remaining
   - T-60 minutes: Songflower pickup (~30 min from Org/SW to Felwood bush + 30 min back to capital) — arrives with ~30 min Songflower buff remaining when raid pulls
   - T-30 minutes: Capital city stack (Onyxia/Nef head if guild is announcing; Rend if Horde) → these are 2-hour buffs so timing is loose
   - T-15 minutes: Zanza vendor stack
   - T-5 minutes: consumables (flask, elixirs, food, scrolls)
3. **Suspend non-buff-prep actions during the window** — don't waste world buff time on AH or trade chat.

## Decision-Engine Rules

- **id:** `worldbuff.window-active` — IF `Raid.PullTime is within 0 to 120 minutes from now` THEN `WorldBuffWindowOpen=true`. Engine prioritizes world-buff actions during the window.
- **id:** `worldbuff.dm-tribute-trip` — IF `WorldBuffWindowOpen && Raid.PullTime > now + 75min && !ActiveBuffs.Contains(DMTribute)` THEN start DM Tribute run. Priority **800**.
- **id:** `worldbuff.songflower-trip` — IF `WorldBuffWindowOpen && Raid.PullTime > now + 30min && !ActiveBuffs.Contains(SongflowerSerenade)` THEN travel to Felwood Songflower bush. Priority **750**. Plan 30-min round trip.
- **id:** `worldbuff.head-stack` — IF `Faction-appropriate dragon head buff scheduled` AND `Raid.PullTime > now + 30min` AND `!ActiveBuffs.Contains(RallyingCry)` THEN travel to capital. Priority **820**.
- **id:** `worldbuff.zanza-vendor` — IF `Raid.PullTime > now + 15min && !ActiveBuffs.Contains(SpiritOfZanza)` AND `Reputation[ZandalarTribe] >= Friendly` AND `CopperOnHand >= zanzaCost` AND `in capital` THEN buy Zanza buff items. Priority **600**.
- **id:** `worldbuff.no-pvp-during-window` — IF `WorldBuffWindowOpen && BuffsActiveCount >= 4` THEN suppress PvP-flag actions. Priority **999** (don't waste world buffs on a death).
- **id:** `worldbuff.no-risky-grind-during-window` — IF `WorldBuffWindowOpen && BuffsActiveCount >= 4` THEN suppress risky-grind actions (lvl-60 elite mobs, world bosses if not already raid-coordinated). Priority **900**.

## Snapshot Fields Needed

- `ActiveBuffs` (existing) — buff list with names, durations, stacks; engine queries for specific buff IDs
- `BuffsActiveCount` (planned helper — derived count)
- `Raid.PullTime` (planned — config from raid leader / external scheduler)
- `WorldBuffWindowOpen` (planned — derived: raid scheduled within 0-120 min)
- `Reputation[ZandalarTribe]` (existing under generic Reputation table)
- `CopperOnHand` (existing) — gates Zanza vendor cost
- `ActiveBuffs.RallyingCryRemaining` / `SongflowerRemaining` / `DMTributeRemaining` / etc. (planned per-buff timer fields)

## Cross-references

- [decision-engine/state-flags.md](../decision-engine/state-flags.md) — `WorldBuffWindowOpen` field declaration
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — pre-raid prep priority band
- [systems/consumables.md](#) (next iteration) — non-world-buff consumables (flasks, elixirs, food, runes)
- [reputations/zandalar-tribe.md](#) (pass 7) — Zanza buff source rep
- [dungeons/dire-maul-north.md](#) (pass 4) — DM Tribute event mechanics
- [zones/felwood.md](#) (pass 3) — Songflower bush locations
- [raids/molten-core.md](#) (pass 5), [raids/blackwing-lair.md](#) (pass 5), [raids/onyxias-lair.md](#) (pass 5) — head drops
- [classes/warrior.md](../classes/warrior.md) — Battle Shout (class buff stack with world buffs)
