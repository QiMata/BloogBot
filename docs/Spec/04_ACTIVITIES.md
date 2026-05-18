# Spec 04 — Activities

> **Layer disambiguation:** "Activity" in this doc is the top layer of
> the four-layer hierarchy defined in
> [`Spec/18_TERMINOLOGY.md`](18_TERMINOLOGY.md):
> `Activity → Objective → Task → Action`. The `ActivityDefinition` record
> below is the **catalog row** (data-only). The runtime `IActivity`
> interface is Phase-2 work tracked in
> [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)
> slot S2.0; today the only Activity-shaped fields on the wire are
> `WoWActivitySnapshot.travel_objective` and
> `progression_status.current_objective`.

## ActivityDefinition

Every activity in the system is described by:

```csharp
public sealed record ActivityDefinition
{
    public required string Id { get; init; }                 // "dungeon.wailing-caverns"
    public required ActivityFamily Family { get; init; }     // Dungeon | Raid | Bg | Quest | ...
    public required string Activity { get; init; }           // "Dungeon"
    public required string Location { get; init; }           // "Wailing Caverns"
    public required LevelRange LevelRange { get; init; }     // 17-24
    public required FactionPolicy FactionPolicy { get; init; }
    public required int MinPlayers { get; init; }
    public required int MaxPlayers { get; init; }
    public required RoleTemplate RoleTemplate { get; init; } // tanks=1, healers=1, dps=3
    public required EntryRequirements EntryRequirements { get; init; }
    public required TravelTarget TravelTarget { get; init; }
    public required TimeSpan ExpectedDuration { get; init; }
    public required HumanJoinPolicy HumanJoinPolicy { get; init; }
    public required BotSelectionPolicy BotSelectionPolicy { get; init; }
    public required IReadOnlyList<string> ProgressionTags { get; init; }
    public required IReadOnlyList<RewardDefinition> Rewards { get; init; }
    public required string TaskFamily { get; init; }         // BotRunner task family head
}

public enum ActivityFamily
{
    StarterQuesting,
    ZoneQuesting,
    Dungeon,
    Raid,
    Battleground,
    ProfessionGathering,
    ProfessionCrafting,
    ProfessionLeveling,
    Economy,
    Reputation,
    Attunement,
    WorldEvent,
    WorldBoss,
}

public sealed record LevelRange(int Min, int Max);             // both inclusive; 1..60

public sealed record RoleTemplate(int Tanks, int Healers, int Dps, int Support = 0);

public sealed record TravelTarget(
    uint MapId,
    float X,
    float Y,
    float Z,
    string NamedLocation);                                     // resolved at runtime

public sealed record FactionPolicy(
    FactionRequirement Requirement,                            // Horde | Alliance | Either
    bool AllowCrossFaction);                                   // bots can escort opposing-faction human

public sealed record HumanJoinPolicy
{
    public bool HumanCanInitiate { get; init; }              // always true per decision R6
    public HumanGroupRole HumanRole { get; init; }           // Member | Leader | Observer
    public bool BotRaidLeader { get; init; }                 // overrides HumanRole=Leader for raids
    public bool RequireFactionMatch { get; init; }
    public bool LootPriorityToHuman { get; init; }
    public bool GearHuman { get; init; }                     // when true, OnDemand also gears the human
    public TimeSpan HumanIdleTimeout { get; init; }          // teardown if human disengages
}
// HumanJoinPolicy supports both object-initializer construction (used by raid/dungeon
// catalog rows that set BotRaidLeader / GearHuman) and a 5-arg positional constructor
// preserving the original record signature for compactness in non-raid rows. Both forms
// are present in Exports/GameData.Core/Models/Activities/HumanJoinPolicy.cs.

public sealed record BotSelectionPolicy(
    int RoleFitWeight = 100,
    int LevelFitWeight = 50,
    int InterruptibilityWeight = 40,
    int TravelEtaWeight = 30,
    int GearReadinessWeight = 20,
    int ClassUtilityWeight = 20,
    int ProgressionOpportunityWeight = 15,
    int RecentFailurePenaltyWeight = -25,
    int HumanPreferenceWeight = 50,
    LootPolicy LootPriority = LootPolicy.NeedBeforeGreed);

public sealed record EntryRequirements
{
    public IReadOnlyList<int> RequiredItems { get; init; } = [];      // item IDs (mangos.item_template)
    public IReadOnlyList<int> RequiredQuests { get; init; } = [];      // completed quest IDs
    public IReadOnlyList<FactionStanding> RequiredReputations { get; init; } = [];
    public IReadOnlyList<string> RequiredAttunements { get; init; } = []; // catalog IDs ("attune.mc")
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = []; // server caps
    public LockoutPolicy LockoutPolicy { get; init; } = LockoutPolicy.None();
}

public sealed record FactionStanding(int FactionId, ReputationStanding MinStanding);

public sealed record LockoutPolicy(
    LockoutType Type,                                          // None | PerCharacterDaily | PerCharacterWeekly | PerInstanceId
    string? LockoutKey)                                        // e.g. "mc" | "naxx"; null when Type=None
{
    public static LockoutPolicy None() => new(LockoutType.None, null);
}

public enum LockoutType { None, PerCharacterDaily, PerCharacterWeekly, PerInstanceId }
// LockoutPolicy is a HINT used for scheduling cadence. The authoritative
// "is this character locked from this instance right now?" answer comes
// from the MaNGOS `character_instance` table (read via SOAP or DB) and is
// queried by the Phase 2 `LockoutVerifier`. Phase 0 catalog tests assert
// the hint is well-formed; Phase 2 cross-validates against observed DB
// lockout state for each catalog row that declares non-None.

public sealed record RewardDefinition(
    RewardKind Kind,                                           // XpRange | Gold | ItemId | FactionRep | Honor
    int Min,
    int Max,
    int? ItemId,
    int? FactionId);

public enum RewardKind { XpRange, Gold, ItemId, FactionRep, Honor }
// `RewardDefinition` is the activity SUMMARY shape — enough for the
// ProgressionPlanner's value scoring (XP/hour vs gold/hour vs rep gain).
// Per-quest reward CHOICE (multiple `RewChoiceItemId1..6` on a quest) is
// handled at turn-in time by the `RewardSelector` — see
// [Spec/03_BOTRUNNER.md#reward-selection](03_BOTRUNNER.md#reward-selection).
// Per-encounter drop tables (BoE chase items like Eye of Sulfuras) live
// in a separate `LootTable` model added in a later spec PR; not in the
// initial catalog.

public enum LootPolicy { FreeForAll, GroupLoot, NeedBeforeGreed, MasterLoot }

public enum HumanGroupRole { Member, Leader, Observer }

public enum FactionRequirement { Horde, Alliance, Either }

public enum ReputationStanding { Hated, Hostile, Unfriendly, Neutral, Friendly, Honored, Revered, Exalted }
```

### Location naming (no `WoWZone` enum required)

`Location` is a **plain string** matching the
[`Plan/Activities/00_INDEX.md`](../Plan/Activities/00_INDEX.md) canonical
name. Resolution to `(MapId, Position)` happens at runtime via the
`NamedLocationResolver` (see [`Plan/Activities/travel.md`](../Plan/Activities/travel.md)
slot ST.6) reading `Bot/named-locations.json`. The catalog test asserts
every catalog `Location` value resolves to a non-empty entry in
`Bot/named-locations.json`.

### TaskFamily existence rule

`TaskFamily` is one of the family heads in
[`Spec/03_BOTRUNNER.md#catalog-of-task-families`](03_BOTRUNNER.md#catalog-of-task-families):
`Travel`, `Combat`, `Questing`, `Dungeoneering`, `Raid`, `Bg`,
`Gathering`, `Crafting`, `Economy`, `Social`, `Recovery`, `Equipment`,
`WorldEvent`, `Loadout`. The Phase 0 catalog test asserts each
`TaskFamily` is in this fixed list. Phase 2 will additionally verify
each family has at least one implemented `IBotTask`.

## Catalog — hard-coded source of truth

The catalog is a **compiled C# static class** in
`Services/WoWStateManager/Activities/ActivityCatalog.cs`. Each row is a
literal `ActivityDefinition`. Tests assert (Phase 0 invariants):

1. Every row has a unique `Id`.
2. Every `Location` resolves to a non-empty entry in
   `Bot/named-locations.json` (resolver loaded via dependency injection
   for the test).
3. Every `LevelRange` is within [1, 60] and `Min ≤ Max`.
4. Every `RoleTemplate` sums to ≥ `MinPlayers` and ≤ `MaxPlayers`.
5. Every `TaskFamily` is one of the fixed family-head strings above.
6. The catalog markdown in
   [`Plan/Activities/00_INDEX.md`](../Plan/Activities/00_INDEX.md) has
   one entry per compiled row (same `Id` set).
7. Every `Family` is a valid `ActivityFamily` enum value.

Phase 2 adds further legality validation that exercises database
fixtures (`mangos.item_template`, `mangos.quest_template`, attunement
chain completion). Those tests are NOT part of Phase 0 because they
require the MaNGOS DB fixture, which Phase 0's catalog work does not.

`leveling-guide/` is the **reference**, not the authority. When the
catalog and the guide disagree, the catalog wins, but a test fails and
a slot is opened to reconcile.

## Initial catalog rows

The catalog has ~90 rows in the initial release. See
[`Plan/Activities/00_INDEX.md`](../Plan/Activities/00_INDEX.md) for the
full table. Family breakdown:

| Family | Row count |
|---|---|
| Starter questing (1-10) | 6 (one per starting zone) |
| Zone questing (10-60) | 26 (Westfall through Silithus) |
| Dungeon | 26 (Ragefire Chasm through Stratholme) |
| Raid | 7 (ZG, AQ20, MC, Ony, BWL, AQ40, Naxx) |
| Battleground | 3 (WSG, AB, AV) |
| Profession farming | 3 (Mining route, Herb route, Skinning route) |
| Profession leveling | 1 (City trainer + recipe loop) |
| Economy | 2 (AH restock, vendor/repair/bank/mail loop) |
| Reputation grind | 5 (Timbermaw, AD, CC, Thorium, ZT) |
| Attunement | 5 (MC, OnyHorde, OnyAlliance, BWL, Naxx) |
| World event | 1 (STV Fishing Extravaganza) |
| World boss | 3 (Azuregos, Kazzak, Emerald Dragons) |
| **Total** | **~88** |

This list is intentionally fixed for the first release. Adding rows
(e.g. ad-hoc holiday events, custom server content) is a spec PR.

## OnDemand vs Autonomous — siloed

This spec covers two separate flows that share the catalog but differ
in everything else:

| | **Autonomous** | **OnDemand** |
|---|---|---|
| Triggered by | Progression decision engine | Operator UI click (or Shodan whisper) |
| Bot pool | Production roster (per [`Spec/16`](16_REALMS_AND_ACCOUNTS.md)) | Reserved 80-bot pool |
| Lockouts | Honored (real lockouts apply) | **Skipped** (DB lockout cleared before launch) |
| Gearing | Real loot from real activities | **GM-applied** loadout per activity config |
| Travel | Real travel (walk, fly, transport) | **Direct teleport** to staging |
| Group form | Real invites + accepts | Same protocol but no human-style delays |
| Loot | Distributed per real loot rules + ProgressionPlanner | **Does not matter** — characters are ephemeral |
| Lease tracking | None (bots always on) | None (pool slot is just held until tear-down) |
| Persists progress? | Yes | **No** — characters often deleted at end |

**Implication:** legality validation runs against catalog requirements
for autonomous progression decisions, but for OnDemand, StateManager
*circumvents* the same requirements when the activity config grants it.
A human OnDemand request never gets rejected for "missing attunement"
or "lockout active" — the StateManager fixes the bot's state first.

**Test fixtures default to OnDemand-equivalent staging.** The vast
majority of LiveValidation tests under `Tests/BotRunner.Tests/LiveValidation/`
need to exercise a *specific* gameplay surface (fishing, AH, raid
formation, BG queue, dungeon clear) without paying the wall-clock
cost of simulated progression. They follow the OnDemand circumvention
pattern: `LiveBotFixture` issues `.character level <N>` /
`.reset items` / `.additem` / `.modify reputation` / `.tele` GM
commands via SOAP (port 7878) per [`Spec/13 §GM command policy`](13_TESTING.md#gm-command-policy)
to drop the bot into the desired pre-condition, then asserts on the
behavior under test. **This is correct** — the staging mode mirrors
how OnDemand activities ship; if the OnDemand launcher is allowed to
GM-command a bot into a state, a test fixture testing that same
gameplay surface is also allowed.

The exception is **autonomous progression tests** under
[`Tests/BotRunner.Tests/LiveValidation/Progression/`](../../Tests/BotRunner.Tests/LiveValidation/Progression/)
(new folder; see [`Spec/13 §Test staging mode`](13_TESTING.md#test-staging-mode--ondemand-equivalent-by-default)),
which by design start from a true L1 baseline and exercise the
ProgressionPlanner + composer end-to-end without any
`.character level` shortcuts. Those are the tests that prove the
autonomous side actually works — the OnDemand-equivalent tests only
prove the gameplay surfaces work *after* state is staged.

## Legality validation

Legality validation has two callers:

1. **Autonomous progression** — `ProgressionPlanner` asks "can bot X
   legally do activity Y right now?" Full validation per the 7 steps
   below. Failure means "pick a different objective."
2. **OnDemand launcher** — same 7 steps, but failure produces a
   *list of fixes* (level-up to N, learn spell X, clear lockout key Y,
   bind hearthstone, etc.) that the launcher *applies* before
   proceeding. Failure to fix one of the items is the only real
   rejection at OnDemand time.

The 7 steps:

```csharp
public sealed record ActivityLegalityResult
{
    public bool IsLegal { get; init; }
    public ActivityDefinition? ResolvedDefinition { get; init; }
    public IReadOnlyList<LegalityFailure> Failures { get; init; } = [];
    public IReadOnlyList<ActivityDefinition> SuggestedAlternatives { get; init; } = [];
}

public sealed record LegalityFailure(
    string Reason,                   // from ErrorTaxonomy
    string Detail,
    string? AffectedField);
```

Validation steps (executed in order; first failure short-circuits):

1. **Activity exists** — `Id` resolves in catalog.
2. **Level fits** — `LevelRange.Contains(request.LevelRange)`.
3. **Faction reach** — the human's faction can physically reach
   `TravelTarget`. Cross-faction activities (e.g. opposing-faction city)
   require explicit `FactionPolicy.AllowCrossFaction = true`.
4. **Entry requirements** — for each bot candidate (and the human if
   present), check items, quests, reputations, attunements.
5. **Server capabilities** — `ServerCapabilities` config must include
   the activity's required capability (e.g. Naxxramas requires
   `Naxx: true`).
6. **Concurrency** — `MaxConcurrentActivities` not exceeded.
7. **Role coverage** — enough bots available to fill the role template.

Failures emit `wwow_activity_rejected_total{reason}` and return a
structured rejection with `SuggestedAlternatives` (catalog rows that
would satisfy the same activity-family at the same level range without
the missing requirement).

## Bot selection scoring

Selection scores candidates for the OnDemand reserved pool by:

| Signal | Weight (default) | Notes |
|---|---|---|
| Role fit | 100 | Hard preference, not gate |
| Faction match | 100 | When `RequireFactionMatch=true` in HumanJoinPolicy |
| Pool availability | 80 | Idle pool slot > recycling > spawning new |
| Class utility for activity | 30 | Per-activity table (e.g. priest for Strat UD) |
| Recent failure count (account, activity pair) | -25 | Cool-down for failure-prone pairings |

For OnDemand, the bot pool is purpose-built — most candidates pass
trivially because the pool is sized to the activity. Weights matter
when the pool is partially occupied by a concurrent activity.

For autonomous progression, `ProgressionPlanner` does not "select bots
for an activity" — each bot picks its own next objective. Selection
scoring is OnDemand-only.

Weights are tunable per activity family via `BotSelectionPolicy` on the
`ActivityDefinition`. The user explicitly directed (2026-05-12)
**ship homogeneous defaults first**; per-family tuning waits for
measured data. Future: LLM-augmented personality affects selection.

### Scoring formula (normative)

`BotSelectionScorer.Score(bot, activity)` returns an `int` computed as:

```csharp
public static int Score(PoolBot bot, ActivityDefinition activity)
{
    int score = 0;
    BotSelectionPolicy w = activity.BotSelectionPolicy;

    score += w.RoleFitWeight              * Bool(bot.MatchesRole(activity.RoleTemplate));
    score += w.LevelFitWeight             * Bool(activity.LevelRange.Contains(bot.Level));
    score += w.InterruptibilityWeight     * Bool(bot.CanInterruptCurrentActivity);
    score += w.TravelEtaWeight            * Normalize(activity.TravelEtaSeconds(bot), max: 600);
    score += w.GearReadinessWeight        * Bool(bot.HasMinimumGearForFamily(activity.Family));
    score += w.ClassUtilityWeight         * activity.ClassUtilityFor(bot.Class);          // 0..2
    score += w.ProgressionOpportunityWeight * Bool(bot.WouldGainProgress(activity));
    score += w.RecentFailurePenaltyWeight * bot.RecentFailureCount(activity);             // already negative
    score += w.HumanPreferenceWeight      * Bool(bot.IsHumanPreferred(activity));

    return score;
}

// Helpers:
//   Bool(b) = b ? 1 : 0
//   Normalize(x, max) = 1 - Math.Clamp(x / max, 0, 1)   // closer travel = higher score
```

Tie-breaks (in order, when two bots tie on `Score`):

1. Lower `bot.RecentFailureCount(activity)` wins.
2. Lower `bot.AccountName` lex order (deterministic; only for stable replay).

The score is **pure** over `(bot, activity)` inputs — `RecentFailureCount`
reads from a per-bot in-memory counter populated by the trace pipeline
(Spec/20 §6.1 `outcome.completion="failed"` lines). Tests pin this
counter to 0 for determinism.

### `ClassUtilityFor` — per-activity class table

`ClassUtilityFor(class)` returns an `int` in `[0, 2]` based on
per-Activity class affinities. Defaults to 0 (neutral) unless declared:

```json
{
  "activityId": "dungeon.stratholme-undead",
  "classUtility": {
    "Priest":  2,   // Holy Nova on plague drops; mass dispel
    "Mage":    2,   // AoE the undead packs
    "Paladin": 1,   // tank or holy still viable
    "Warrior": 1
  }
}
```

Catalog rows that omit `classUtility` get 0 for all classes; the
weight contribution is 0 (homogeneous default per the user's direction).
Per-family tuning happens in `Config/activities/<id>.json` (Plan/03
S2.7) and is hot-reloaded.

### Failure-reason mapping (selection-time only)

Bot-selection failures map onto [`Spec/12`](12_ERROR_TAXONOMY.md):

| Failure | Spec/12 reason | Notes |
|---|---|---|
| No candidate scores above 0 (no role-fit, level mismatch, etc.) | `missing_role` | existing; surfaces as `RejectionCode.POOL_EXHAUSTED` to OnDemand |
| Selected bot disconnects between `LegalityChecked` and `Spawning` | `bot_disconnected` | existing; launcher re-runs selection |
| `BotSelectionPolicy.HumanPreferenceWeight` reference points at a non-existent human GUID | `task_precondition_failed` | defensive; should never happen in OnDemand path |

No new Spec/12 values needed.

## OnDemand activity instance lifecycle

The lifecycle is driven by
[`Spec/02_STATEMANAGER.md`](02_STATEMANAGER.md)'s OnDemand launcher:

```text
Requested → LegalityChecked → Spawning → Outfitting → Partying
         → Travelling → Engaged → TearDown → Done
                                                ↓
                                          Pool slots freed
```

Critical: at the `Outfitting` stage, the launcher **circumvents**
normal gameplay restrictions per the activity's config. It runs:

- `.character level <required>` for each bot below threshold.
- `.reset talents` then apply the build.
- `.learn all-recipes` for the spec's expected spell list.
- `.setskill <skill> <required>` for required professions.
- `.additem <itemId>` for missing gear (or vendor-buy through `BotRunner`).
- `.reset lockouts` for the human as well as the bots when the activity
  needs a clean lockout state (e.g. "I want to do MC again now").
- `.modify rep <factionId> <standing>` for reputation prereqs.
- `.tele name <bot> <stagingLocation>` to teleport.

These actions all go through SOAP via
`LiveBotFixture.ExecuteGMCommandAsync()`-equivalent paths owned by the
OnDemand launcher.

Every stage transition is metrics-instrumented
(`wwow.statemanager.ondemand.<stage>_total{result,activity}`) and
emits a structured log line with `instance_id` as correlation field.

## Autonomous activity lifecycle

For autonomous bots:

```text
ProgressionPlanner.SelectObjective(bot) → bot.AssignedActivity = "..."
   → ActivityResolver parses → BotTask pushed
   → bot executes → snapshot reports progress
   → on completion → ProgressionPlanner picks next objective
```

No instance, no stages, no scheduler. The bot's own behavior tree
drives the loop; StateManager just observes via snapshots.

When a group is needed (dungeon, raid, BG), bots organically form one
when enough bots in the same level band reach the same objective at
the same time. This is the "living server" texture — groups happen
because the population supports them, not because a scheduler
allocated them.

## Human join semantics (OnDemand only)

The `HumanJoinPolicy` shape is defined once at the top of this doc —
see the [`HumanJoinPolicy` record above](#activitydefinition). This
section covers semantics; the record fields are the contract.

The human is *always* legal in OnDemand. The launcher circumvents:

- **Faction mismatch** — picks same-faction pool bots, teleports human
  to a neutral safe staging coord.
- **Missing attunement** — applies attunement state via GM commands
  before teleport.
- **Lockout active** — `.reset` lockouts before teleport.
- **Level gap** — `.character level` the human if `GearHuman = true`;
  otherwise level-scale the bots.

Default `HumanIdleTimeout`: 5 minutes for short activities (dungeons),
15 minutes for long (raids). Per-row tunable. The user's stance
(2026-05-12): loot priority does not matter during OnDemand because
the instance is siloed and characters are ephemeral — the field is
omitted.

## Snapshot projection

Bot-selection state surfaces on `OnDemandInstanceProj` (Spec/23 §10
field 43) — extending that message rather than `WoWActivitySnapshot`
directly because selection is an OnDemand-only concern. Plan/03 S2.5
owns the projection.

```protobuf
// Extension of OnDemandInstanceProj (Spec/23 §10):
message BotSelectionEntry {
    string  account_name        = 1;
    int32   score               = 2;     // BotSelectionScorer.Score output
    bool    selected            = 3;     // true iff this bot ended up in the activity
    repeated string score_components = 4; // human-readable breakdown:
                                          //   "RoleFit:100", "LevelFit:50", "TravelEta:18", ...
}

message OnDemandInstanceProj {
    // ... existing fields 1-9 from Spec/23 §10 ...
    repeated BotSelectionEntry selection_results = 10;  // top 8 candidates by score
}
```

Tests assert against this projection rather than reading
`BotSelectionScorer` internal state.

## ML integration — Selection-weight learning

**Surface.** `BotSelectionScorer.Score(...)` uses the
`BotSelectionPolicy` weight tuple defined on the catalog row. The
**ML hook** is an off-line tool that learns better weight tuples by
analyzing outcome traces (Spec/20 §6.1) for OnDemand activities. The
runtime path stays deterministic — no new advisor RPC.

**Why no new advisor RPC.** Selection runs at most once per OnDemand
launch (≤1 per minute at production scale); per-launch advisor calls
would dominate the budget. The Phase-2 lookup table approach is
sufficient: a learned weight tuple is just a config delta on the
`ActivityDefinition.BotSelectionPolicy` defaults.

**Three maturity phases** (matches the Spec/20 §5 pattern even though
no Spec/20 RPC is consumed):

| Phase | Source | Owned by |
|---|---|---|
| 1 — Heuristic | `BotSelectionPolicy` record defaults (homogeneous across families) | this Spec |
| 2 — Rules + lookup | Per-family overrides in `Config/activities/<id>.json` | Plan/03 slot S2.7 (Per-activity config files) |
| 3 — Learned weights | Off-line tool fits a gradient-boosted regressor over `(bot, activity, outcome)` traces; output is a PR for `Config/activities/<id>.json` BotSelectionPolicy block | (no slot yet — Plan follow-up) |

**Input feature vector** for the off-line learner:

| Feature | Source |
|---|---|
| `bot.level`, `bot.class`, `bot.spec`, `bot.gear_tier` | `outcome` line per Spec/20 §6.1 |
| `activity.family`, `activity.level_range`, `activity.role_template` | catalog snapshot |
| `bot.recent_failure_count` (last 24h) | trace aggregation |
| `bot.travel_eta_seconds` | snapshot at selection time |
| `outcome.completion == "complete"` | label (0/1) |
| `outcome.wall_clock_ms` | continuous regression target |
| `outcome.roster_distance_delta` | progressive label per Spec/05 |

**Output.** Per-Activity weight tuple proposal: 9 ints in
`[-100, +200]` range matching `BotSelectionPolicy` fields. Tool emits
JSON snippet for human review.

**Fail-soft fallback.** No learned weights → defaults apply. The
learner is **always off-line**; runtime never blocks on it.

**Live-validation guard.** Replaying any OnDemand trace with weights
reset to defaults MUST still produce a completed activity — selection
optimality may degrade but never to the point of preventing completion.
Asserted via `BotSelection_DefaultWeights_StillCompleteActivityTest`
in §Test surface.

## Dynamic-progressive invariant

Activity selection MUST satisfy both properties:

1. **Dynamic.** Two OnDemand requests for the same Activity but
   different `(requesting_human, pool_state)` MUST sometimes select
   different bot rosters. Identical inputs produce identical
   selections (deterministic given fixed weights + tie-break order).
2. **Progressive.** The selected bot roster's resulting Activity
   completion MUST close someone's `RosterPlanner.Distance`. Selection
   that pulls bots from progressive Activities into a low-value
   OnDemand request (e.g. an ambient port service) is allowed only
   when the human's distance reduction exceeds the bots' aggregate
   distance reduction loss. Tracked at the trace surface via the
   `outcome.roster_distance_delta_aggregate` field (Spec/23 §13).

Asserted by `Activities_DynamicProgressive_BotSelectionRespectsProgressionCostTest`
in the test surface section.

## Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/03/S2.7`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s27--per-activity-config-files) | `Config/activities/<id>.json` (per-row `BotSelectionPolicy` overrides + `classUtility`) | §ClassUtilityFor, §ML Phase 2 |
| [`Plan/03/S2.5`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s25--ondemandactivitylauncher) | OnDemand launcher invokes `BotSelectionScorer.Score(...)` during Spawning stage; emits `selection_results[]` projection | §Snapshot projection |
| [`Plan/13/S9.1-S9.7`](13_PHASE9_CATALOG_FILL.md) | Catalog row authoring for new Activities; `BotSelectionPolicy` defaults inherited from this Spec | §ActivityDefinition |
| **(no slot yet — Plan follow-up)** | `BotSelectionScorer.cs`, off-line weight-learner tool | §ML Phase 3 |

The "no slot yet" row joins the orphan-services follow-up tracked in
[`Plan/SPEC_FILL_LOOP.md`](../Plan/SPEC_FILL_LOOP.md). Likely home is a
new Plan/18 phase or a sub-slot under Plan/03.

## Test surface

Contract tests live at
`Tests/BotRunner.Tests/Activities/BotSelectionContractTests.cs`. Tests
assert against `OnDemandInstanceProj.selection_results[]` (proto field
43.10) per Test Isolation Rules.

- **`BotSelectionScorer_PureFunctionOfBotAndActivity`** — calling
  `Score(bot, activity)` twice with identical inputs (including
  `RecentFailureCount=0`) returns the same integer.
- **`BotSelectionScorer_RoleFitWeightDominates`** — a candidate with
  role-fit + level-fit beats a candidate with level-fit + class-utility
  unless `ClassUtilityWeight > RoleFitWeight + LevelFitWeight` — i.e.
  the default policy puts role first.
- **`BotSelectionScorer_TieBreakIsLowestRecentFailureThenLexAccountName`** —
  given two candidates with identical scores, the bot with fewer
  recent failures wins; given equal failures, the lex-lowest
  `AccountName` wins. Deterministic.
- **`BotSelectionPolicy_PerFamilyOverrideRespected`** — when
  `Config/activities/dungeon.stratholme-undead.json` declares
  `classUtility.Priest=2`, the selector prefers Priests over other
  classes of equal role+level fit.
- **`BotSelection_DefaultWeights_StillCompleteActivityTest`** — replay
  a representative OnDemand trace with all `BotSelectionPolicy`
  weights reset to defaults; the same activity completes within 1.5×
  its baseline wall clock. Plan/14 S10.8 guard.
- **`Activities_DynamicProgressive_BotSelectionRespectsProgressionCostTest`** —
  the dynamic-progressive invariant. For two OnDemand requests with
  identical `activity_id` but different pool states (one with attuned
  raiders idle, one with attuned raiders mid-quest-chain), the selector
  picks different rosters AND the resulting
  `outcome.roster_distance_delta_aggregate` is ≤ 0 in both cases.

## Existing code anchors

| Concept | File |
|---|---|
| Activity catalog (to be added) | `Services/WoWStateManager/Activities/ActivityCatalog.cs` |
| Activity parser | `Exports/BotRunner/Activities/ActivityParser.cs` |
| Activity resolver | `Exports/BotRunner/Activities/ActivityResolver.cs` |
| Battleground coordinator | `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs` |
| Dungeon coordinator | `Services/WoWStateManager/Coordination/DungeoneeringCoordinator.cs` |
| Raid composition | `Services/WoWStateManager/Progression/RaidCompositionService.cs` |
| Progression planner | `Services/WoWStateManager/Progression/ProgressionPlanner.cs` |
