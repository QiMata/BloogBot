# Spec 22 — World cycles (calendar, lockouts, respawns, world buffs)

> **What this spec is.** The contract for time-of-day, weekly-reset,
> holiday-event, world-buff-window, raid-lockout, and world-boss
> respawn semantics that the DecisionEngine and the Activity composers
> must respect.
>
> The 1.12.1 server has no calendar UI, but the underlying mangosd
> cron jobs (`game_event`, world-boss respawn timers, lockout reset
> cron) exist and drive everything. This spec describes what the bot
> stack observes and how.

## 1. Time scales

| Scale | Anchor | Bot consumption |
|---|---|---|
| Tick | every 100 ms snapshot | Task / Action layer |
| Minute | per-bot pacing variance ([`Spec/24`](24_BEHAVIORAL_VARIATION.md)) | per-Objective patience |
| Hour | trade-chat budget, mailbox cadence | Social / Economy layer |
| Day | daily reset (server config), world-boss respawn windows | Activity selection |
| Week | weekly reset (Tuesday US / Wednesday EU), raid lockout reset | Raid availability |
| Holiday | `game_event` table cron-driven | Activity availability |

## 2. Server-realm calendar

`Westworld-Test` runs accelerated timers per
[`Spec/16_REALMS_AND_ACCOUNTS.md`](16_REALMS_AND_ACCOUNTS.md). `Westworld`
runs default 1.12.1 cadence. The catalog's `LockoutPolicy` is a *hint*
— the authoritative state is the MaNGOS `character_instance` table.

```sql
-- raid lockout check
SELECT instance, permanent, extendTime
FROM   character_instance
WHERE  guid = :charGuid
  AND  instance IN (SELECT id FROM instance WHERE map = :raidMapId);
```

The `LockoutVerifier` (Phase 2 slot S2.4) queries this before any
autonomous raid Activity assignment.

## 3. World-buff windows

Five world-buff sources, all with bot-relevant respawn timers:

| Buff | Source | Default respawn | Decay |
|---|---|---|---|
| Heart of Hakkar (ZG) | Hakkar at ZG | 3-day raid lockout | 60 min on logout |
| Onyxia / Nefarian / Rend slaughter cry | Onyxia + Nef + Rend kills, broadcast in capital | tied to raid kill cadence | 2 hr off / 60 min logout |
| Songflower Serenade | 7 flowers in Felwood | 30 min after pick | 60 min off / 60 min logout |
| Dire Maul tribute | DM-N tribute run | per-bot lockout | 2 hr off / 60 min logout |
| Black Lotus | spawn timer in 5 zones | 60 min spawn cadence | n/a — used in flasks |

The DecisionEngine respects buff-window state via the snapshot field
`WorldBuffWindowOpen` (planned per
[`leveling-guide/decision-engine/state-flags.md`](../leveling-guide/decision-engine/state-flags.md)):

```csharp
public sealed record WorldBuffWindow(
    string BuffName,
    DateTime OpensAt,
    DateTime ExpectedNextRaidStart,
    TimeSpan DecayBudget);
```

A bot in band "Critical-path progression" with a planned raid in the
60 min decay window pauses its current Objective to drive to the
buff source.

## 4. World-boss respawn

Three world bosses in 1.12.1: Azuregos (Azshara), Lord Kazzak (Blasted
Lands), Emerald Dragons (rotating Lethon / Emeriss / Taerar / Ysondre).

| Boss | Spawn map | Respawn after kill | Catalog row |
|---|---|---|---|
| Azuregos | Azshara | 3-7 days | `boss.azuregos` |
| Lord Kazzak | Blasted Lands | 3-7 days | `boss.kazzak` |
| Emerald Dragons | Ashenvale / Hinterlands / Duskwood / Feralas | 1-3 days per dragon | `boss.emerald-dragons` |

Respawn windows are wide; the `WorldBossWatcher` service polls
`creature` table rows with `id IN (boss-creature-ids)` and emits a
`WorldBossSpawnedEvent` when a row transitions `dead → alive`. The
event triggers the relevant Activity's Coordinator to quorum a 20-bot
raid within 30 minutes.

## 5. Game events (`game_event`)

The MaNGOS `game_event` table drives time-windowed content:

| EventId | Name | Cadence (default 1.12.1) | Bot impact |
|---|---|---|---|
| 1 | Midsummer Fire Festival | Summer | quest cycle |
| 2 | Winter Veil | December | quest cycle |
| 5 | Lunar Festival | Jan/Feb | quest + coin turn-ins |
| 7 | Hallow's End | October | quest cycle |
| 8 | Children's Week | May | escort + photo quests |
| 26 | STV Fishing Extravaganza | Sun 14:00 server time | `event.stv-fishing-extravaganza` |
| 50+ | Server-custom holidays | per-server | declared in `ServerCapabilities` |

DecisionEngine consumes the live event set via:

```csharp
public interface IGameEventProvider
{
    IReadOnlyList<ActiveGameEvent> GetActiveEvents();
    event EventHandler<GameEventChanged> EventChanged;
}

public sealed record ActiveGameEvent(
    int EventId,
    string Name,
    DateTime StartedAt,
    DateTime EndsAt,
    IReadOnlyList<string> AffectedActivityIds);
```

When a holiday event starts, the matching Activity rows become
*eligible* (e.g. `event.lunar-festival.coin-cycle` becomes
selectable). Bots in the priority band "Background / opportunistic"
opportunistically pick up event Activities.

## 6. Raid-window scheduling (autonomous)

Per [`Spec/05_PROGRESSION.md`](05_PROGRESSION.md): there is no
scheduler. Bot quorums form organically. But quorum *timing* respects
world-cycle constraints:

```
RaidCompositionService quorum policy:
   1. count attuned 60s by class/spec for the candidate raid
   2. on quorum threshold met:
      a. if all members have valid world buffs active OR
         expected-next-raid-start is outside the buff decay window:
            quorum forms, raid starts
      b. else:
            quorum is held; bots pivot to world-buff Activities
            (DM tribute, Songflower, Onyxia/Nef/Rend slaughter cry pickup)
      c. when buffs land OR window decay forces the issue:
            quorum forms, raid starts
```

Quorum thresholds per raid (default):

| Raid | Quorum size |
|---|---|
| ZG / AQ20 | 18 of 20 |
| MC | 35 of 40 |
| Onyxia | 33 of 40 |
| BWL | 36 of 40 |
| AQ40 | 37 of 40 |
| Naxx | 38 of 40 |

Quorum holds for 30 minutes; if it does not fill in 30 min, the
candidate roster releases and bots resume their own progression
priorities.

## 7. Daily reset window

Configurable per realm; default 03:00 server-local. At reset:

- `wwow_world_cycles_reset_total{event=daily}` counter increments.
- `LockoutVerifier` refreshes the cache.
- `WorldBossWatcher` re-polls.
- DecisionEngine re-runs Activity-eligibility for every active bot.

Bots in mid-Activity are not interrupted; the reset only affects
Activity *selection* on the next composer call.

## 8. Weekly reset

Tuesday 04:00 server time (US) / Wednesday 04:00 (EU); configurable.
At weekly reset:

- All raid lockouts clear.
- BG honor rolls; PvP rank updates land.
- `wwow_world_cycles_reset_total{event=weekly}` counter increments.
- DecisionEngine emits a one-time `WeeklyResetEvent` to subscribers.

Raid-attuned 60s respond to weekly reset by re-evaluating raid-Activity
eligibility (most go from "lockout active" → "lockout clear" → "raid
candidate"). Pre-raid buff Activities re-prioritize accordingly.

## 9. Test surface

- **`WorldCycleTests.WeeklyReset_ClearsRaidLockouts`** — at simulated weekly reset, `character_instance` rows referencing the test bot's raid lockouts are cleared and the bot is eligible for the raid Activity again.
- **`WorldCycleTests.WorldBuffWindow_HoldsRaidQuorum`** — quorum formed inside the buff-decay window proceeds; quorum formed outside the window pivots bots to buff Activities first.
- **`WorldCycleTests.GameEvent_EnablesEventActivity`** — STV Fishing Extravaganza becomes a selectable Activity only when the `game_event` row is active.
- **`WorldBossWatcherTests.SpawnedEvent_TriggersCoordinatorQuorum`** — DB transition `dead → alive` fires the world-boss Coordinator quorum within 30 s.

Live validation: `Westworld-Test` runs accelerated timers, so weekly
reset can be exercised in a 30-min test cycle.

## 10. Existing code anchors (planned files)

| Concept | File |
|---|---|
| Game-event provider | `Services/WoWStateManager/WorldCycles/GameEventProvider.cs` |
| World-boss watcher | `Services/WoWStateManager/WorldCycles/WorldBossWatcher.cs` |
| Lockout verifier | `Services/WoWStateManager/Activities/Legality/LockoutVerifier.cs` |
| World-buff windows | `Services/WoWStateManager/WorldCycles/WorldBuffWindowTracker.cs` |
| Daily/weekly reset emitter | `Services/WoWStateManager/WorldCycles/CalendarResetService.cs` |
