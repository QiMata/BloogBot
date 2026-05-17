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

## 9. Snapshot projection

World-cycle state surfaces on `WoWActivitySnapshot` via three additive
proto fields (continue numbering after Spec/19/21 land):

```protobuf
message WorldBuffWindowProj {
    string buff_name              = 1;   // "OnyxiaBuff" | "Songflower" | "DireMaulTribute" | "ZGHeart" | "BlackLotusPickup"
    uint64 opens_at_ms            = 2;
    uint64 expected_next_raid_at_ms = 3; // 0 when no raid scheduled
    uint32 decay_budget_seconds   = 4;
    bool   currently_active       = 5;   // true when bot already has the buff aura
}

message LockoutStateProj {
    uint32 raid_map_id            = 1;
    uint32 instance_id            = 2;   // MaNGOS character_instance.instance
    bool   permanent              = 3;
    uint64 reset_at_ms            = 4;   // weekly reset timestamp
}

message ActiveGameEventProj {
    uint32 event_id               = 1;
    string name                   = 2;
    uint64 started_at_ms          = 3;
    uint64 ends_at_ms             = 4;
    repeated string affected_activity_ids = 5;
}

// New fields on WoWActivitySnapshot:
repeated WorldBuffWindowProj world_buff_windows = 40;
repeated LockoutStateProj    current_lockouts   = 41;
repeated ActiveGameEventProj active_game_events = 42;
```

Tests assert via these snapshot fields per CLAUDE.md Test Isolation
Rules; direct reads against `character_instance` MySQL or
`game_event` rows are reserved for the StateManager service-side code
that **produces** these projections.

## 10. Failure-reason mapping

World-cycle failures map onto Spec/12's `FailureReason` enum:

| Failure | Spec/12 reason | Notes |
|---|---|---|
| Raid lockout active when Activity assigned | `lockout_active` | exists in Spec/12 today |
| World boss already dead, respawn window closed | `task_precondition_failed` | log only; bot picks alternative Activity |
| Buff source on cooldown when bot arrives | `task_timeout` | with detail string `world_buff_window_missed` |
| `game_event` row absent when event Activity assigned | `task_precondition_failed` | composer must filter event Activities by `IGameEventProvider.GetActiveEvents()` first |
| Weekly-reset event delivery missed (subscriber disconnected) | `server_unavailable` | StateManager re-broadcasts on reconnect |

One new value **may be needed** depending on metric granularity:

- `world_buff_window_missed` *(new)* — distinguished from
  `task_timeout` because it is a routing decision (we chose to chase
  the buff and lost) not a stall. Defer to row-15 Spec/12 pass.

## 11. ML integration — Buff-window pivot

**Surface.** When a raid candidate is forming and the buff-decay
window is the tipping decision (Spec/22 §6 step 2b), the
`RaidCompositionService` consults
`IDecisionEngineClient.GetObjectiveAdviceAsync(ObjectiveContext, ct)`
with the tied Objectives populated as:

- `continue-current-objective`
- `pivot-to-dm-tribute`
- `pivot-to-songflower`
- `pivot-to-onyxia-rend-pickup`
- `wait-for-raid-quorum`

The advisor returns the recommended pivot id; the deterministic
fallback (Phase 1) is "pivot to the lowest travel-cost buff source
with the highest decay budget remaining."

**Why use the existing Objective advisor, not a new one.** This
decision *is* an Objective-tiebreaker per
[`Spec/19 §9`](19_AOTA_RUNTIME.md#9-ml-integration--composer-tiebreaker) —
the candidates are real Objective ids and the rationale is
`RosterPlanner.Distance` reduction. Adding a `BuffWindow` advisor
would duplicate Spec/20's surface.

**Input feature additions.** `ObjectiveContext.roster_goal_distance[]`
(the 8-element vector from Spec/20 §2.1) gains a normalized
"raid-readiness deficit" axis: how far the bot is from a buffed,
attuned, gear-ready raid slot. This is computed locally by the
RaidCompositionService before the advisor call; no new proto field
needed.

**Three maturity phases** per [`Spec/20 §5`](20_DECISION_ENGINE.md):

| Phase | Source | Owned by |
|---|---|---|
| 1 — Heuristic | Lowest travel cost × highest decay budget | RaidCompositionService default |
| 2 — Rules + lookup | `Config/decision-engine/buff-pivot-rules.json` per `(class, expected_raid_at, current_zone)` | Plan/14 slot S10.6 |
| 3 — ONNX | `Services/DecisionEngineService/Models/objective/v1.onnx` (the same model as the Objective advisor — buff-window axis is a feature, not a separate model) | Plan/14 slot S10.6 (Mode=Ml) |

**Fail-soft fallback.** When advice is `NoAdvice`, low confidence, or
recommends an id outside the tied set, fall back to Phase 1.

**Live-validation guard.** Replaying any raid-formation trace with
buff-pivot advisor forced to `NoAdvice` MUST still produce a
`roster_distance_delta ≤ 0` outcome line (the deterministic pivot
still closes goal distance, just less optimally).

## 12. Dynamic-progressive invariant

A bot's response to a world-cycle event MUST satisfy both properties:

1. **Dynamic.** A weekly-reset event for two bots with different
   `(class, attunement, raid lockouts, current_zone, current_buffs)`
   MUST produce different Activity selections on the next composer
   call. Identical inputs produce identical selections.
2. **Progressive.** Every Activity assignment triggered by a
   world-cycle event (raid lockout cleared → raid attempt; world buff
   open → buff pickup; game event start → event Activity) MUST close
   `RosterPlanner.Distance` *to a measurable extent*:
   - raid lockout-cleared → raid kill = gear-tier-filled axis
   - buff pickup → raid clear time decreases (measurable in trace
     `outcome.wall_clock_ms`)
   - event Activity → event-reward unique-item axis closes
   Failed Activities do NOT advance distance, but the *selection* MUST
   be progressive in expectation.

Asserted via `WorldCycle_DynamicProgressive_WeeklyResetTriggersRaidPickupTest`
in §13.

## 13. Plan-slot cross-reference

| Slot | Owns | Section |
|---|---|---|
| [`Plan/03/S2.4`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s24--legalityvalidator-fixup-mode) | `LockoutVerifier.cs` | §2, §10 `lockout_active` |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | Snapshot projection broadcaster (extend for §9 fields) | §9 fields 40-42 |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `buff-pivot-rules.json` | §11 Phase 2 |
| **(no slot yet — follow-up)** | `Services/WoWStateManager/WorldCycles/GameEventProvider.cs` | §5 |
| **(no slot yet — follow-up)** | `Services/WoWStateManager/WorldCycles/WorldBossWatcher.cs` | §4 |
| **(no slot yet — follow-up)** | `Services/WoWStateManager/WorldCycles/WorldBuffWindowTracker.cs` | §3 |
| **(no slot yet — follow-up)** | `Services/WoWStateManager/WorldCycles/CalendarResetService.cs` | §7, §8 |

The four "no slot yet" rows are tracked as a follow-up in
[`Plan/SPEC_FILL_LOOP.md`](../Plan/SPEC_FILL_LOOP.md). They probably
fit best as a new Plan/17 phase or as Plan/13 (Catalog completeness)
sub-slots since the game-event provider feeds catalog eligibility.

## 14. Test surface

Contract tests at
`Tests/BotRunner.Tests/WorldCycles/WorldCycleContractTests.cs`. All
`Skip("contract pending S<phase>.<n>")` until the matching slot lands.

- **`WeeklyReset_ClearsRaidLockouts`** — at simulated weekly reset,
  `snapshot.current_lockouts[]` is empty for the test bot and the
  bot becomes eligible for the raid Activity again (asserted via
  `snapshot.current_activity_id` transitioning from
  `"econ.vendor-loop"` to a raid id). Slot S2.4 + (no slot)
  CalendarResetService.
- **`WorldBuffWindow_HoldsRaidQuorum`** — quorum formed inside the
  buff-decay window proceeds; quorum formed outside the window
  pivots bots to buff Activities first. Asserted via
  `snapshot.current_objective_id` matching one of
  `{"pivot-to-dm-tribute", "pivot-to-songflower",
  "pivot-to-onyxia-rend-pickup"}` in the buff-pivot case.
- **`GameEvent_EnablesEventActivity`** — STV Fishing Extravaganza
  becomes a selectable Activity only when
  `snapshot.active_game_events[]` contains `event_id=26`.
- **`WorldBossWatcher_SpawnedEventTriggersCoordinatorQuorum`** — DB
  transition `dead → alive` fires the world-boss Coordinator quorum
  within 30 s; asserted via 20 bot snapshots transitioning to
  `boss.azuregos` (or other world-boss id) `current_activity_id`.
- **`BuffPivot_AdvisorRespectsCandidateSet`** — when ObjectiveAdvice
  recommends a `RecommendedObjectiveId` outside the buff-pivot tied
  set, the RaidCompositionService falls back to the Phase-1 heuristic
  (lowest travel cost × highest decay budget).
- **`WorldCycle_DynamicProgressive_WeeklyResetTriggersRaidPickupTest`** —
  the dynamic-progressive invariant from §12. For ≥2 distinct synthetic
  snapshots that differ in `(class, attunement, raid lockout, current
  buffs)`, the post-weekly-reset Activity selection differs in either
  the chosen Activity id or its parameters AND each selection closes
  `RosterPlanner.Distance(snapshot, goal)` in expectation. Slot
  (no slot) CalendarResetService.

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
