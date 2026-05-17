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

## 9. Snapshot projection

Personality state surfaces on `WoWActivitySnapshot` via one additive
proto field (continuing after Spec/23 fields 43-44; Plan/16 slot
S12.10 lands this):

```protobuf
message PersonalityProj {
    uint64 personality_hash       = 1;   // stable hash of all knob values
    string cluster_id             = 2;   // ML-cluster id when chat-template
                                         // path used GetPersonalityClusterAdviceAsync; empty otherwise
    uint32 chatty_level           = 3;   // ChattyLevel enum (0/1/2)
    uint32 reward_priority        = 4;   // RewardPriority enum
    bool   join_world_chat        = 5;
    bool   accept_random_guild    = 6;
    bool   vendor_junk_aggressively = 7;
    bool   operator_override_present = 8; // §8 override active
}

// New field on WoWActivitySnapshot:
PersonalityProj personality = 45;
```

Tests assert via this projection per Test Isolation Rules. Direct
reads of `PersonalityProfile` private fields are reserved to the
`BotTask` base-class plumbing that *consumes* them (§6).

## 10. Failure-reason mapping

Personality has no first-class failure modes (its consumers fail with
their own reasons). The two relevant edge cases:

| Failure | Spec/12 reason | Notes |
|---|---|---|
| `Config/personalities.json` invalid JSON | `catalog_invalid` | bot falls back to `PersonalityProfile.Deterministic` per [`Plan/16 Failure recovery`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#failure-recovery); no FailureReason raised at Task layer |
| Operator override out-of-range | (config save-time validation; never reaches runtime) | rejected with a schema validation error in the UI |

No new Spec/12 values needed.

## 11. ML integration — Personality clustering

**Surface.** `IPersonalityFactory.Create(accountName)` calls
`IDecisionEngineClient.GetPersonalityClusterAdviceAsync(PersonalityClusterContext, ct)`
([`Spec/20 §2.4`](20_DECISION_ENGINE.md#24-personalitycluster-advisor))
**exactly once** per profile generation. The advisor returns a
`recommended_cluster_id` chosen from the `available_cluster_ids` set
exported from a Python-side clustering pass over real-population
traces. The factory then biases its knob sampling by the cluster
centroid (e.g. cluster `talkative-altoholic` biases `ChattyLevel`
toward `Talkative` and `RewardPriority` toward `Stat-spread`).

**Why advisory not authoritative.** Profile generation is deterministic
in the absence of advice (Phase 1 = uniform sample over
`Config/personalities.json` `PersonalityMix`). The advisor's job is to
add a "weighted by real-population shape" axis on top of the
deterministic-from-account-name PRNG; the final knob values still come
from `HashStableRandom(accountName)` so re-runs are reproducible
(advisor input is fixed by account name + bot context).

**Input feature vector.** `PersonalityClusterContext` per Spec/20 §2.1.
Service-side tensor shape is `[1, 40]` per
[`Spec/20 §4.2`](20_DECISION_ENGINE.md#42-onnx-feature-tensor-shapes-per-advisor).

**Output shape.** `PersonalityClusterAdvice` per Spec/20 §2.1.

**Three maturity phases** per [`Spec/20 §5`](20_DECISION_ENGINE.md):

| Phase | Source | Owned by |
|---|---|---|
| 1 — Heuristic | Uniform sample over `Config/personalities.json` `PersonalityMix`; cluster_id is empty | Plan/16 slot S12.1 |
| 2 — Rules + lookup | `Config/decision-engine/personality-cluster-rules.json` per `(class, race, faction, target_level)` precedence table | Plan/14 slot S10.6 |
| 3 — ONNX | `Services/DecisionEngineService/Models/personality_cluster/v1.onnx` trained on real-population trace exports (Spec/13 captures) | Plan/14 slot S10.6 (Mode=Ml flip) |

**Fail-soft fallback.** When advice is `NoAdvice`, low confidence, or
recommends a cluster id outside the available set, the factory falls
back to Phase 1 uniform sampling. `PersonalityProj.cluster_id` is
empty in that case, and tests can assert on its emptiness to verify
fallback occurred.

**Live-validation guard.** Replaying any production trace with the
`personality_cluster` advisor forced to `NoAdvice` MUST produce
identical Activity-completion outcomes (same `roster_distance_delta`
sign) for the same `(accountName, class, race)` triple as a run with
advisor enabled. Variation knobs MAY differ; outcomes MUST converge.
This is the §5 "non-personality determinism" property elevated to a
trace invariant.

## 12. Dynamic-progressive invariant

Behavior variation MUST satisfy both properties:

1. **Dynamic.** Two bots with different `AccountName` (and therefore
   different deterministic-PRNG personalities) running the same
   Activity MUST emit different per-tick `ActionMessage` timing
   signatures over a representative window. Specifically: the
   sequence of `(ActionType, dispatchedAtMs)` tuples diverges in
   `dispatchedAtMs` cadence by at least the `ReactionTimeJitterMs`
   range. Asserted via the trace surface — not by reading internal
   timer state.
2. **Progressive.** Variation MUST NOT degrade Activity outcomes
   beyond the configured tolerance: a high-jitter bot completes the
   same Activity within `1.5 × deterministic_baseline_wall_clock`
   (Plan/16 S12.11 budget). `roster_distance_delta` per Activity is
   ≤ 0 regardless of personality, matching the
   `PersonalityIntegrationTests.RotationCorrectness_UnaffectedByJitter`
   test in §13.

Asserted via
`BehavioralVariation_DynamicProgressive_TimingDivergesButOutcomeIsStableTest`
in §13.

## 13. Plan-slot cross-reference

| Slot | Owns | Section |
|---|---|---|
| [`Plan/16/S12.1`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s121--personalityprofile-record--factory) | `PersonalityProfile`, `IPersonalityFactory`, `HashStableRandom` | §2, §3, §11 |
| [`Plan/16/S12.2`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s122--bottaskcontext-carries-personality) | `BotTaskContext.Personality` injection | §6 |
| [`Plan/16/S12.3..S12.7`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s123--timing-knob-consumers) | per-knob consumers (timing, route, economy, social, reward) | §4 |
| [`Plan/16/S12.8`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s128--configpersonalitiesjson-hot-reload) | `Config/personalities.json`, hot-reload subscriber | §3 |
| [`Plan/16/S12.9`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s129--per-bot-operator-override) | `Config/character-overrides.json` | §8 |
| [`Plan/16/S12.10`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s1210--snapshot-projection) | `WoWActivitySnapshot.personality` field 45 | §9 |
| [`Plan/16/S12.11`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s1211--distribution--correctness-livevalidation) | LiveValidation distribution + correctness suites | §13 tests |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `personality-cluster-rules.json`, ONNX | §11 Phase 2 / Phase 3 |

## 14. Test surface

Contract tests live at
`Tests/BotRunner.Tests/Personality/PersonalityContractTests.cs`. All
`Skip("contract pending S12.<n>")` until the matching slot lands.
Assertions go through `WoWActivitySnapshot.personality` (§9) per Test
Isolation Rules — never via `PersonalityProfile` private fields.

- **`PersonalityFactory_DeterministicFromAccountName`** — same
  account name produces same `snapshot.personality.personality_hash`
  across PRNG implementations and process restarts. Slot S12.1.
- **`PersonalityFactory_MixConfigRespectsConfiguredDistribution`** —
  over a 1000-bot sample, the `snapshot.personality.chatty_level`
  histogram matches `Config/personalities.json` `PersonalityMix` ±5%.
  Slot S12.1 + S12.8.
- **`AhPosting_UnderscutPercentRespected`** — bots with
  `AhPostingUnderscutPercent=10` post at 10% above the AH minimum
  observed price; bots at -5 post 5% below. Slot S12.5.
- **`RouteWiggle_RouteVariesByPersonality`** — same A→B route resolves
  identically inside Detour (deterministic), but the
  `PathfindingClient` post-processor lateral wiggle differs by at
  least `RouteWiggleRadiusYd` between bots with different personalities.
  Slot S12.4.
- **`PersonalityIntegration_RotationCorrectnessUnaffectedByJitter`** —
  high-jitter bot still completes the rotation correctly within the
  rotation's max-duration budget (1.5× deterministic baseline). Slot
  S12.11.
- **`PersonalityCluster_AdvisorOutsideAvailableSet_FallsBackToUniform`** —
  when `GetPersonalityClusterAdviceAsync` returns a `recommended_cluster_id`
  not in `available_cluster_ids`, factory falls back to uniform
  sampling and `snapshot.personality.cluster_id` is empty. Slot S12.1
  + S10.6.
- **`OperatorOverride_PinsKnobValue`** — operator pins
  `AhPostingCadenceMin=30` in `Config/character-overrides.json`; the
  bot's snapshot reflects `operator_override_present=true` and the
  AH-restock cadence matches the override regardless of the
  PRNG-derived default. Slot S12.9.
- **`BehavioralVariation_DynamicProgressive_TimingDivergesButOutcomeIsStableTest`** —
  the dynamic-progressive invariant from §12. Two bots with different
  AccountName-derived personalities running the same Activity emit
  diverging `(ActionType, dispatchedAtMs)` cadence in traces AND both
  produce `roster_distance_delta ≤ 0` outcomes within 1.5× the
  deterministic-baseline wall clock. Slot S12.11.

## 10. Why this is its own spec

Behavioral variation overlaps every other spec (combat in Spec/03,
chat in Spec/21, AH in Plan/Activities/economy, scheduling in
Spec/22). Without a single canonical surface, the knobs would be
either re-invented per family or, worse, hard-coded into individual
tasks. This spec gives Phase 12 a stable contract to bind to and
gives every other family a single place to look for "where does my
timing variance come from".
