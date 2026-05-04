---
title: "System — Server Events Calendar (Recurring + Seasonal)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Server Events Calendar — Recurring + Seasonal Events Schedule

1.12.1 has multiple recurring events that affect raid prep, profession economy, and PvP queueing. **Weekly**: Stranglethorn Fishing Extravaganza (Sundays 14:00-16:00 server time), weekly raid reset (Tue US / Wed EU for 7-day raids; Tue/Fri for 3-day ZG/AQ20). **Daily**: Profession CDs (Arcanite Transmute 24h, Mooncloth 24h, Cured Rugged Hide 24h). **World bosses**: Azuregos + Lord Kazzak (16h respawn), 4 Emerald Dragons (random rotation). **Seasonal**: Hallow's End (Oct), Winter Veil (Dec-Jan), Lunar Festival (Chinese NY), Children's Week (May 1-7), Midsummer Fire Festival (June 21-July 5). **PvP holidays**: BG-specific weekends with bonus rewards. Engine should encode wall-clock scheduler.

---

## Weekly Recurring Events

### Stranglethorn Fishing Extravaganza (Sundays 14:00-16:00 Server Time)

**The defining 1.12.1 fishing event** — see [../professions/fishing.md](../professions/fishing.md#stranglethorn-fishing-extravaganza-sundays).

| Field | Value |
|-------|-------|
| Schedule | **Sunday 14:00-16:00 server time** weekly |
| Location | Booty Bay, Stranglethorn Vale |
| Goal | Catch 40 Speckled Tastyfish from STV pools, deliver to Riggle Bassbait |
| 1st place reward | **1000g** + "Master Angler of Stranglethorn" title + Arcanite Fishin' Pole (+35 fishing skill, BiS) |
| 2nd-4th rewards | Hook of the Master Angler, Nat Pagle's Extreme Angler FC-5000, Lucky Fishing Hat |

**Decision-engine cue:** STV Extravaganza is **the** Fishing event. Engine should:
- Track Sunday 14:00-16:00 server time as recurring event
- Plan STV pool route during 2-hour window
- Coordinate with raid week-end fishing
- Maintain Speckled Tastyfish stockpile

### Weekly Raid Reset (Per-Raid Lockout)

| Raid | Reset cycle |
|------|-------------|
| **Molten Core** | 7-day (Tue US / Wed EU reset) |
| **Onyxia's Lair** | 5-day cycle |
| **Blackwing Lair** | 7-day (Tue US / Wed EU) |
| **Zul'Gurub** | 3-day (Tue/Fri US, Wed/Sat EU) |
| **Ahn'Qiraj 20** | 3-day |
| **Ahn'Qiraj 40** | 7-day |
| **Naxxramas** | 7-day |

**Decision-engine rule:** engine should track per-raid `WeeklyResetTimer.HoursUntil` for raid attendance planning. If `HoursUntil < 12`, defer non-essential raid pulls.

### Weekly BG Honor Cap

| Day | Action |
|-----|--------|
| Tuesday (US) / Wednesday (EU) | Weekly honor reset; rank-progression evaluation |

PvP rank progression evaluated weekly — see [../pvp/honor-system.md](../pvp/honor-system.md).

---

## Daily Recurring Events

### Profession Daily CDs (24-Hour Cooldown)

| CD | Profession | Output | Approximate value |
|----|-----------|--------|-------------------|
| **Arcanite Transmute** | Alchemy | 1 Arcanite Bar (Thorium Bar + Arcane Crystal) | 5-10g per |
| **Mooncloth crafting** | Tailoring | 1 Mooncloth (2 Felcloth) | 5-10g per |
| **Cured Rugged Hide** | Leatherworking | 1 Cured Rugged Hide | Variable; cross-prof reagent |

**Decision-engine rule:** engine should track per-character `Profession.{type}.LastDailyCDExpiry` and auto-trigger at expiry.

### Daily Quest Reset (Standard Cooldown)

Some quests reset daily (rare in 1.12 — most quests are one-time):
- Tabard quests (rare repeatables)
- Various daily-reset content

---

## World Boss Respawn Calendar

### Azuregos (Azshara — Forlorn Ridge)

| Field | Value |
|-------|-------|
| Spawn location | Forlorn Ridge, eastern Azshara |
| Respawn timer | **16 hours** after kill |
| Difficulty | Lvl 60 raid (~30+ raid group required) |
| Drops | Twilight Trappings (Brood of Nozdormu rep), Tier-set items, weapon drops |

### Lord Kazzak (Blasted Lands — Tainted Plains)

| Field | Value |
|-------|-------|
| Spawn location | South plains, Blasted Lands |
| Respawn timer | **16 hours** after kill |
| Difficulty | Lvl 60 raid with **Aura of Madness** AoE (30+ raid group required) |
| Drops | Cross-tier-set items, weapon drops |

### Emerald Dragons (4 Locations Rotate)

The 4 Emerald Dragons (**Ysondre, Lethon, Emeriss, Taerar**) spawn at 4 grove locations:

| Dragon | Location |
|--------|----------|
| **Ysondre** | Twilight Grove (Duskwood, EK) |
| **Lethon** | Seradane (Hinterlands, EK) |
| **Emeriss** | Dream Bough (Feralas, KAL) |
| **Taerar** | Bough Shadow (Ashenvale, KAL) |

| Field | Value |
|-------|-------|
| Rotation | 4 dragons rotate, 1 active at a time |
| Respawn timer | **2-7 days** between rotations (varies by server) |
| Difficulty | Lvl 60 raid (40+ raid group required) |
| Drops | Cross-tier-set items, weapon drops, Trinket of the Defender |

**Decision-engine rule:** track per-server `WorldBoss.{Azuregos, Kazzak, EmeraldDragons}.LastSpawnTime` + estimated respawn. Coordinate raid week with boss schedule.

---

## Seasonal Events (Annual Recurring)

### Hallow's End (October)

| Field | Value |
|-------|-------|
| Schedule | October 18-31 (annual) |
| Iconic content | Headless Horseman (Scarlet Monastery summon), Candy Buckets, Wickerman, Hallowed Helm |
| 1.12.1 status | Limited vs TBC version — fewer events available |
| Notable rewards | Hallow's End Treats, candy-themed cosmetic rewards |

### Winter Veil (December-January)

| Field | Value |
|-------|-------|
| Schedule | December 16 - January 2 (annual) |
| Iconic content | Greatfather Winter (capital cities), Mistletoe, Smokywood Pastures, Christmas-themed quests |
| 1.12.1 status | Vanilla version — limited vs Cataclysm/MoP versions |
| Notable rewards | Christmas-themed cosmetic gear, Smokywood treats |

### Lunar Festival (Chinese New Year — January-February)

| Field | Value |
|-------|-------|
| Schedule | Annual (varies by Chinese lunar calendar, typically Jan-Feb) |
| Iconic content | Coin of the Hidden Dragons turn-ins (~20 elder NPCs across world for cosmetic Coin of the Hidden Dragons) |
| Notable rewards | Lunar Festival fireworks, Coin of the Hidden Dragons cosmetics, Elune's Lantern |

### Children's Week (May 1-7)

| Field | Value |
|-------|-------|
| Schedule | May 1-7 (annual) |
| Iconic content | Orphan adoption quest hub (Dornaa for Alliance, Salandria for Horde — children's escort quests across Azeroth) |
| Notable rewards | Cosmetic mini-pet (Hyacinth Macaw, Dornaa's Doll) — varies by year |

### Midsummer Fire Festival (June 21 - July 5)

| Field | Value |
|-------|-------|
| Schedule | June 21 - July 5 (annual) |
| Iconic content | Bonfire-honoring (capital city + zone bonfires), Stealing Flames (cross-faction PvP), Fire Festival items |
| Notable rewards | Crown of the Fire Festival, Brazier of Madness, fire-themed cosmetic items |

### Brewfest (October — TBC ONLY, NOT IN 1.12.1)

`[verify pass 3]` Brewfest in TBC, not 1.12.1. Engine should NOT plan Brewfest content for 1.12.1 emulation.

---

## PvP Holiday Events (Per-BG Bonus Weekends)

### BG Holiday Weekend Bonuses

| BG | Holiday weekend bonus |
|----|----------------------|
| **Warsong Gulch** holiday | +20% honor for WSG matches |
| **Arathi Basin** holiday | +20% honor for AB matches |
| **Alterac Valley** holiday | +30% honor for AV matches (typically) |
| **All-BG holiday** | +50% honor across all BGs (rare) |

**Decision-engine rule:** track per-BG holiday weekends; engine should auto-queue holiday-bonus BG for honor max.

---

## Wall-Clock Scheduler (Decision-Engine)

### Calendar Awareness

Engine should track:

```text
Snapshot.WallClock.CurrentDateTime               // current real-time
Snapshot.WallClock.ServerDateTime                // server-time (for STV Sunday)
Snapshot.WallClock.DayOfWeek                     // Sunday detection for STV
Snapshot.WallClock.HoursToWeeklyReset             // raid week countdown
Snapshot.WallClock.HoursToBGHoliday               // PvP weekend bonus
Snapshot.WallClock.WorldBoss.{Azuregos, Kazzak, EmeraldDragons}.LastSpawnTime  // per-server respawn
Snapshot.WallClock.SeasonalEvent.Active           // current seasonal event detection
Snapshot.Profession.{type}.LastDailyCDExpiry     // 24h CD tracking
```

### Decision Triggers

| Event | Action trigger |
|-------|----------------|
| Sunday 14:00-16:00 server time | STV Fishing Extravaganza queue |
| Tuesday US / Wednesday EU before reset | Final raid pull window |
| Daily 24h+ since last Arcanite Transmute | Auto-craft Arcanite Bar |
| Daily 24h+ since last Mooncloth | Auto-craft Mooncloth at Moonwell |
| World boss respawn within ~1 hour | Raid coordination notice |
| Seasonal event active | Auto-queue seasonal quests |
| BG holiday weekend | Auto-queue PvP for max honor |

---

## Decision-Engine Rules

1. **STV Fishing Extravaganza priority**: Sunday 14:00-16:00 server time. Engine should pre-stage Speckled Tastyfish + plan 2-hour STV route.
2. **Weekly raid reset alignment**: Tue (US) / Wed (EU) for 7-day raids. Engine should defer non-essential raid pulls if `HoursUntilReset < 12`.
3. **Daily CD auto-trigger**: Arcanite Transmute + Mooncloth + Cured Rugged Hide at 24h+ expiry. Engine should auto-craft for sustained income.
4. **World boss tracking**: per-server respawn timers (Azuregos/Kazzak 16h, Emerald Dragons 2-7 days rotation). Engine should coordinate with raid leaders.
5. **Seasonal event detection**: Hallow's End (Oct 18-31), Winter Veil (Dec 16-Jan 2), Lunar Festival (Chinese NY), Children's Week (May 1-7), Midsummer Fire (Jun 21-Jul 5). Engine should auto-pickup seasonal quests.
6. **PvP holiday weekends**: track BG-specific holiday bonuses; auto-queue holiday BG for max honor.
7. **Brewfest 1.12 caveat**: Brewfest is TBC content, NOT 1.12.1. Engine should not plan Brewfest content for 1.12 emulation.
8. **VMaNGOS server time configuration**: server should be configured for proper time-zone handling. Engine should `ServerCapabilities.SeasonalEvents` check.

---

## Snapshot Fields Needed

```text
Snapshot.WallClock.CurrentDateTime                // current real-time
Snapshot.WallClock.ServerDateTime                 // server-time for STV
Snapshot.WallClock.DayOfWeek                      // Sunday detection
Snapshot.WallClock.HourOfDay                      // STV 14-16 detection
Snapshot.WallClock.HoursToWeeklyReset             // raid week countdown
Snapshot.WallClock.HoursToBGHoliday               // PvP holiday signal
Snapshot.WallClock.WorldBoss.{Azuregos, Kazzak, EmeraldDragons}.LastSpawnTime
Snapshot.WallClock.WorldBoss.{*}.RespawnEstimate  // per-boss respawn estimation
Snapshot.WallClock.SeasonalEvent.Active           // {HallowsEnd, WinterVeil, LunarFestival, ChildrensWeek, Midsummer}
Snapshot.Profession.{type}.LastDailyCDExpiry     // 24h CD tracking
Snapshot.RaidGroup.WeeklyResetCountdown           // raid week countdown
Snapshot.PvPMatch.HolidayBonus                    // BG holiday detection
Snapshot.QuestLog.Active.SeasonalQuests          // seasonal event quest pickup
Snapshot.Inventory.SpeckledTastyfish              // STV Extravaganza stockpile
```

---

## Cross-References

- Stranglethorn Fishing Extravaganza: [../professions/fishing.md](../professions/fishing.md)
- Daily profession CDs: [played-time-economy.md](played-time-economy.md)
- Weekly raid reset (per-raid lockouts): [../raids/](../raids/)
- World bosses (Azuregos/Kazzak/Emerald Dragons): [../zones/azshara.md](../zones/azshara.md), [../zones/blasted-lands.md](../zones/blasted-lands.md), [../zones/duskwood.md](../zones/duskwood.md), [../zones/the-hinterlands.md](../zones/the-hinterlands.md), [../zones/feralas.md](../zones/feralas.md), [../zones/ashenvale.md](../zones/ashenvale.md)
- PvP rank/honor system: [../pvp/honor-system.md](../pvp/honor-system.md)
- BG holiday bonuses: [../pvp/warsong-gulch.md](../pvp/warsong-gulch.md), [../pvp/arathi-basin.md](../pvp/arathi-basin.md), [../pvp/alterac-valley.md](../pvp/alterac-valley.md)
- Wall-clock scheduler integration: see project root [CLAUDE.md](../../CLAUDE.md)
