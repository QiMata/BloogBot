# Spec 24 — Behavioral variation (personality knobs)

> **What this spec is.** The contract for per-bot variation that makes
> the WWoW population *protocol-indistinguishable* from a thin live
> human population, **without** introducing fake mistakes, fake AFK,
> or fake idle wandering (per the Vision non-goals).
>
> Indistinguishability here is **timing- and routing-shaped**, not
> chat- or skill-shaped. A bot's combat rotation is still optimal; its
> reaction latency is variable. A bot's pathfinding is still correct;
> its route choice has tunable randomness.

## 1. Why variation matters

Without variation, every bot of a given class/spec produces *identical*
behavior — same exact rotation cadence, same exact reaction time, same
exact zone-quest hub order. A passive observer recording two bots side
by side sees a perfectly correlated signal that no human population
produces.

Variation is **not** anti-detection (Vision non-goal: no anti-detection
beyond Warden). It is **acceptance-criterion 4** from Vision: "a new
human player logging in cannot tell, from gameplay observation alone,
that the population is machine-controlled."

## 2. Personality knobs

Each bot carries a `PersonalityProfile` derived deterministically from
its `AccountName` (so the same bot has the same personality across
restarts):

```csharp
public sealed record PersonalityProfile
{
    public required string AccountName { get; init; }

    // Timing variance ---------------------------------
    public required float  ReactionTimeJitterMs { get; init; }       // 50 .. 250
    public required float  IntraRotationJitterMs { get; init; }      // 20 .. 80
    public required float  WaypointPatienceMs { get; init; }         // 1500 .. 4000
    public required float  PostKillLootDelayMs { get; init; }        // 300 .. 1200
    public required float  NpcInteractApproachMs { get; init; }      // 500 .. 1500

    // Route variance ---------------------------------
    public required float  RouteWiggleRadiusYd { get; init; }        // 0.5 .. 3.0
    public required float  HotspotShuffleStrength { get; init; }     // 0 .. 1
    public required float  GroupQuestPreferenceWeight { get; init; } // 0 .. 1.5

    // Economy variance -------------------------------
    public required float  AhPostingUnderscutPercent { get; init; }  // -5 .. +10
    public required int    AhPostingCadenceMin { get; init; }        // 30 .. 120
    public required bool   VendorJunkAggressively { get; init; }

    // Social variance --------------------------------
    public required float  WhisperReplyDelayMs { get; init; }        // 5000 .. 30000 friendly; 10000 .. 60000 stranger
    public required ChattyLevel ChattyLevel { get; init; }           // Quiet | Normal | Talkative
    public required bool   JoinWorldChatChannels { get; init; }
    public required bool   AcceptRandomGuildInvites { get; init; }

    // Reward / loot variance -------------------------
    public required RewardPriority RewardPriority { get; init; }     // Bis | Highest-vendor | Stat-spread

    // Sleep / login variance -------------------------
    public required TimeSpan PreferredOnlineWindow { get; init; }    // start
    public required TimeSpan PreferredOnlineDuration { get; init; }  // hours
    public required float    WeekendOnlineMultiplier { get; init; }  // 1.0 .. 1.6
}
```

All field ranges are sampled at profile generation time using
`HashStableRandom(accountName)` as the PRNG seed. The result is
deterministic — re-generating from the same account name produces an
identical profile.

## 3. Generation policy

```csharp
public interface IPersonalityFactory
{
    PersonalityProfile Create(string accountName);
}
```

Default `PersonalityFactory` distributes personality types per a
configurable mix:

```json
{
  "PersonalityMix": {
    "Quiet":     0.45,
    "Normal":    0.45,
    "Talkative": 0.10
  },
  "AhPostingAggressiveSharePercent": 0.20,
  "GroupQuestPreferenceMean": 0.55
}
```

Mix configuration lives in `Config/personalities.json` and is
hot-reloadable.

## 4. Where each knob lands

| Knob | Consumed by |
|---|---|
| `ReactionTimeJitterMs` | `IBotTask.OnPushedAsync` initial wait |
| `IntraRotationJitterMs` | `PvERotationTask` between cast windows |
| `WaypointPatienceMs` | `GoToTask` stuck-detection threshold |
| `PostKillLootDelayMs` | `LootCorpseTask` initial wait |
| `NpcInteractApproachMs` | `InteractWithNpcTask` post-arrival wait |
| `RouteWiggleRadiusYd` | `PathfindingClient` route post-processor adds small lateral noise |
| `HotspotShuffleStrength` | `KillObjectiveTask.PickHotspot` random shuffle ranking |
| `GroupQuestPreferenceWeight` | composer's Objective scoring for `Group=1` quests |
| `AhPostingUnderscutPercent` | `AuctionHousePostTask.PriceCalculator` |
| `AhPostingCadenceMin` | `econ.ah-restock` between cycles |
| `WhisperReplyDelayMs` | whisper handler reply queue |
| `ChattyLevel` | `IChatGenerator` template selection rate |
| `RewardPriority` | `IRewardSelector.SelectQuestReward` |
| `PreferredOnlineWindow` | autonomous login schedule |

## 5. Non-personality determinism

These do **not** vary by personality (deterministic across bots):

- **Combat rotation correctness.** A Fury Warrior always uses
  Heroic Strike on rage cap; Hot Streak Pyro fires on proc; etc.
- **Pathfinding correctness.** Routes still resolve through the
  same `PathfindingService`; the variance is post-process lateral
  wiggle, not algorithmic choice.
- **Activity legality.** Every bot enforces `EntryRequirements`
  identically; personality does not bypass gates.
- **Snapshot emission.** 100 ms tick interval; personality does not
  alter snapshot cadence.
- **Quest-progress tracking.** `QuestLogEntries` counter logic is
  identical for every bot.

Variation is **layered over** correctness, not substituted for it.

## 6. Variance application via task base class

`BotTask.OnPushedAsync` adds the post-push delay from
`PersonalityProfile.ReactionTimeJitterMs`. Each Task family that
needs additional variance reads its specific knob via:

```csharp
protected TimeSpan Jitter(string knob)
    => TimeSpan.FromMilliseconds(_context.Personality.GetMs(knob));
```

This keeps the variance plumbing local and skippable for unit tests
(unit tests get `PersonalityProfile.Deterministic` which sets every
jitter to 0).

## 7. The "Deterministic" personality

For tests:

```csharp
public static class PersonalityProfile
{
    public static PersonalityProfile Deterministic { get; } = new()
    {
        AccountName = "TEST",
        ReactionTimeJitterMs = 0,
        IntraRotationJitterMs = 0,
        WaypointPatienceMs = 1500,
        // ... all jitters zeroed, all flags false
    };
}
```

Live-validation tests inject `Deterministic` so timings are
predictable.

## 8. Operator overrides

The UI exposes a per-bot personality editor. Operator may pin any knob
to a specific value, bypassing the AccountName-derived default. The
override is persisted in `Config/character-overrides.json` and survives
restart.

This is the escape hatch when a bot's deterministic personality is
producing pathological behavior (e.g. very-low-AH-cadence bot is
hoarding inventory).

## 9. Test surface

- **`PersonalityFactoryTests.Deterministic_FromAccountName`** — same account name produces same profile across PRNG implementations.
- **`PersonalityFactoryTests.MixConfig_RespectsConfiguredDistribution`** — over a 1000-bot sample, the mix matches `Config/personalities.json` ±5%.
- **`AhPostingTests.UnderscutPercentRespected`** — bots with `AhPostingUnderscutPercent=10` post at 10% above min; bots with -5 post 5% below.
- **`RouteWiggleTests.RouteVariesByPersonality`** — same A→B route resolves identically (Detour determinism) but post-processor wiggles by `RouteWiggleRadiusYd`.
- **`PersonalityIntegrationTests.RotationCorrectness_UnaffectedByJitter`** — high-jitter bot still completes the rotation correctly within the rotation's max-duration budget.

## 10. Why this is its own spec

Behavioral variation overlaps every other spec (combat in Spec/03,
chat in Spec/21, AH in Plan/Activities/economy, scheduling in
Spec/22). Without a single canonical surface, the knobs would be
either re-invented per family or, worse, hard-coded into individual
tasks. This spec gives Phase 12 a stable contract to bind to and
gives every other family a single place to look for "where does my
timing variance come from".
