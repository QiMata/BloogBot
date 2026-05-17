# Spec 05 — Progression

## Three planning layers

```
RosterPlanner          // long-horizon (account-level)
   ↓
ProgressionPlanner     // per-character next-objective
   ↓
ActivityScheduler      // multi-character coordination
```

Each layer is a service under `Services/WoWStateManager/Progression/`.

## RosterPlanner

**Owns:** account-level decisions about which characters exist on the
roster and which long-horizon goals they carry.

```csharp
public sealed record CharacterRosterGoal
{
    public required string AccountName { get; init; }
    public required Race Race { get; init; }
    public required Class Class { get; init; }
    public required string SpecName { get; init; }            // catalog reference
    public required IReadOnlyList<Profession> Professions { get; init; }
    public required GearTier TargetGearTier { get; init; }    // PreRaid | T1 | T2 | T2.5 | T3
    public required IReadOnlyList<ReputationGoal> Reputations { get; init; }
    public required IReadOnlyList<AttunementGoal> Attunements { get; init; }
    public required MountTier MountTier { get; init; }
    public required long GoldTargetCopper { get; init; }
    public required PvPRank? PvPRankTarget { get; init; }
    public required IReadOnlyList<int> RareItemTargets { get; init; }
}
```

### `RosterPlanner.Distance` — the canonical progression metric

The dynamic-progressive invariant referenced across [`Spec/19 §10`](19_AOTA_RUNTIME.md#10-dynamic-progressive-invariant),
[`Spec/20 §10`](20_DECISION_ENGINE.md#10-dynamic-progressive-invariant),
[`Spec/21 §12`](21_SOCIAL_FABRIC.md#12-dynamic-progressive-invariant),
[`Spec/22 §12`](22_WORLD_CYCLES.md#12-dynamic-progressive-invariant),
[`Spec/23 §13`](23_ONDEMAND_API.md#13-dynamic-progressive-invariant),
and [`Spec/24 §12`](24_BEHAVIORAL_VARIATION.md#12-dynamic-progressive-invariant)
is grounded here. `RosterPlanner.Distance(snapshot, goal)` returns a
scalar that **strictly decreases** every time an Activity completes
that closes part of `CharacterRosterGoal`.

```csharp
public sealed record RosterPlannerDistance
{
    public required float TotalScalar { get; init; }              // sum of weighted axes; tests assert delta <= 0
    public required IReadOnlyDictionary<DistanceAxis, float> PerAxis { get; init; }
}

public enum DistanceAxis
{
    Level             = 0,  // (TargetLevel - Player.Level) / TargetLevel    (0..1)
    GearTier          = 1,  // (TargetGearTier - GearTier(currentEquipped)) / TargetGearTier
    AttunementStep    = 2,  // unmet attunement step count / total attunement steps
    ReputationTier    = 3,  // sum_i max(0, GoalStanding_i - CurrentStanding_i) / sum_i GoalStanding_i
    GoldTargetPct     = 4,  // max(0, GoldTargetCopper - Player.gold) / GoldTargetCopper
    MountTier         = 5,  // (TargetMountTier - CurrentMountTier) / TargetMountTier
    PvPRank           = 6,  // (TargetPvPRank - CurrentPvPRank) / 14
    ProfessionSkill   = 7,  // sum_p max(0, TargetSkill_p - CurrentSkill_p) / (300 * |Professions|)
}

public static class RosterPlanner
{
    /// <summary>
    /// Computes the bot's distance to its CharacterRosterGoal.
    /// Pure function over (snapshot, goal) - no I/O, no DB lookups.
    /// </summary>
    public static RosterPlannerDistance Distance(
        WoWActivitySnapshot snapshot,
        CharacterRosterGoal goal);

    /// <summary>
    /// Default weights summing to 1.0; tests pin via the optional
    /// `weights` argument for determinism.
    /// </summary>
    public static IReadOnlyDictionary<DistanceAxis, float> DefaultWeights { get; }
        // Level=0.18, GearTier=0.18, AttunementStep=0.15, ReputationTier=0.10,
        // GoldTargetPct=0.05, MountTier=0.08, PvPRank=0.08, ProfessionSkill=0.18
}
```

`TotalScalar` is computed as `sum(weights[axis] * perAxis[axis])` so
that a fully-achieved goal returns `TotalScalar = 0` and a fresh
level-1 character with no professions / no gold returns `TotalScalar
≈ 1`.

`roster_distance_delta` per Activity (used in trace
`outcome.roster_distance_delta` per Spec/20 §6.1) is computed as
`Distance(post-Activity snapshot, goal).TotalScalar -
Distance(pre-Activity snapshot, goal).TotalScalar`. **Strictly
non-positive** for any progressive Activity completion. Cosmetic-only
completions return 0; the loop's contract is `≤ 0`, never positive.

`Distance` is intentionally a pure function — no DB hits, no service
calls — because it is computed every snapshot tick by the trace writer
(Spec/20 §6.1 `kind="outcome"` lines emit the delta). The 8-axis
breakdown is what the `ObjectiveContext.roster_goal_distance` 8-float
array (Spec/20 §2.1) carries.

### Coverage rules the RosterPlanner enforces (in order):

1. **Faction-side bootstrap.** If the account plan needs a Shaman and
   the account has 0 Horde characters, the planner schedules a Horde
   character creation first.
2. **Class coverage.** All 9 classes appear at level 60 before any class
   is duplicated at 60.
3. **Profession coverage.** All 9 primary professions distributed across
   the roster; no profession unrepresented at 300 skill.
4. **Spec diversity.** Each class has at least one of each spec at 60
   (tank, healer, DPS where applicable).
5. **PvP rank.** Roster contains at least one character at each PvP
   rank band needed for AV objectives.

## ProgressionPlanner

**Owns:** the next objective for a single character given its current
snapshot.

```csharp
public sealed record ProgressionObjective
{
    public required string Type { get; init; }    // "Quest" | "Dungeon" | "Profession" | ...
    public required string CatalogId { get; init; } // ActivityDefinition.Id
    public required int Priority { get; init; }
    public required string Rationale { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}
```

Priority bands (highest first; matches `docs/leveling-guide/decision-engine/leveling-priority.md`):

1. **Survival** — corpse run, spirit healer, food/water if HP/Mana < threshold.
2. **Training** — class trainer + weapon trainer if available skill points exist.
3. **Gear** — equip available gear; visit vendor for missing slot; chase BiS if eligible.
4. **Attunement** — current bracket's MC/Ony/BWL chain if not complete.
5. **Reputation** — current bracket's required factions (Argent Dawn for EPL, etc.).
6. **Mount** — at 40 (or 60 epic) if gold target met.
7. **Gold** — farm gold if below `GoldTargetCopper * 1.1`.
8. **Profession** — train + level professions to bracket cap.
9. **Default grind** — zone quests for current bracket from
   [`Plan/Activities/quests.md`](../Plan/Activities/quests.md).

The planner respects:

- **Lockouts.** Dungeon/raid lockouts gate Attunement and Gear bands
  (autonomous progression honors real lockouts; only OnDemand
  circumvents them).
- **Server capabilities.** Disabled raids omit from Attunement band.
- **Account-level state.** RosterPlanner overrides individual choices
  when account-level coverage demands it.

## Group formation is organic (no scheduler)

Per the 2026-05-12 design refinement: **there is no `ActivityScheduler`
holding leases and routing bots.** Autonomous bots are always on,
running their own behavior trees toward their own objectives. Groups
form when level/role-compatible bots converge on the same group
activity organically — e.g. five 17-24 level bots independently
decide "next: Wailing Caverns" and the QuestCoordinator detects the
quorum and triggers the group-invite flow.

The "How do bots act when they can't raid?" question (e.g. attuned
60s waiting for raid lockout to reset, or short of a 40-man quorum)
resolves through the ProgressionPlanner's priority bands:

- Gear-chase dungeons (Strat live for Cape, Scholo for trinket, etc.).
- World buff farming during pre-raid windows.
- Profession leveling / AH posting.
- Reputation grinds (Argent Dawn, Cenarion Circle, Thorium Brotherhood).
- PvP queues (BGs to fill toward PvP rank goal).
- Mount/gold farm (low-priority but ever-present fallback).

The QuestCoordinator, DungeoneeringCoordinator, BattlegroundCoordinator,
and RaidCoordinator from
[`Spec/02_STATEMANAGER.md#coordinators`](02_STATEMANAGER.md#coordinators)
detect group quorums and orchestrate the form-invite-travel-engage
flow, but they do NOT preempt or lease bots — they just react to
snapshot conditions.

## Test realm acceleration

For test realm (`Westworld-Test`, see
[`Spec/16_REALMS_AND_ACCOUNTS.md`](16_REALMS_AND_ACCOUNTS.md)),
mangosd config accelerates lockout refresh and respawn timers so the
progression loop can be exercised without waiting real-world days.
Tests assert against accelerated timings; production realm uses
default Vanilla 1.12.1 timings.

## Character build templates

Pre-built templates ship in `Services/WoWStateManager/Progression/Templates/`:

- `FuryWarriorPreRaid.json` — Lionheart Helm, Drake Tooth Necklace, etc.
- `HolyPriestMCReady.json` — Robes of the Exalted, Aurastone Hammer.
- `FrostMageAoEFarmer.json` — Lord Valthalak's Robes, Master's Hat.
- ... (one per representative spec; ~15 total)

A `CharacterRosterGoal` can reference a template by name; the
`CharacterBuildConfig` inherits the template's `TargetGearSet`,
`ReputationGoals`, `MountGoal`, `GoldTargetCopper`, etc.

## Account-level state model

```csharp
public sealed record AccountRoster
{
    public required string AccountName { get; init; }
    public required IReadOnlyList<CharacterRosterGoal> Characters { get; init; }
    public required IReadOnlyDictionary<int, int> FactionStandings { get; init; } // factionId → standing
    public required IReadOnlyList<int> CompletedAttunements { get; init; }
    public required long SharedGoldCopper { get; init; }
}
```

Account state is **not on per-character snapshots.** It lives in
StateManager memory (rebuildable from snapshots) and is persisted to a
small JSON file on shutdown.

## Snapshot projection

Progression state surfaces on `WoWActivitySnapshot` via one additive
proto field (field numbering continues after Spec/24's field 45):

```protobuf
message RosterPlannerDistanceProj {
    float  total_scalar         = 1;            // RosterPlannerDistance.TotalScalar
    float  axis_level           = 2;            // PerAxis[DistanceAxis.Level]
    float  axis_gear_tier       = 3;
    float  axis_attunement_step = 4;
    float  axis_reputation_tier = 5;
    float  axis_gold_target_pct = 6;
    float  axis_mount_tier      = 7;
    float  axis_pvp_rank        = 8;
    float  axis_profession_skill = 9;
    float  last_completion_delta = 10;           // delta produced by the most recent
                                                 //   Activity completion; <= 0 invariant
    string roster_template_name = 11;            // e.g. "FuryWarriorPreRaid"
}

// New field on WoWActivitySnapshot (continues after Spec/24 field 45):
RosterPlannerDistanceProj roster_distance = 46;
```

Per CLAUDE.md Test Isolation Rules, tests assert on this snapshot
projection rather than reading `RosterPlanner.Distance(...)` directly
from in-process state. Live-validation guards on the dynamic-progressive
invariant scan `outcome.roster_distance_delta` in JSONL traces (Spec/20
§6.1).

## Failure-reason mapping

Progression failures map onto [`Spec/12`](12_ERROR_TAXONOMY.md):

| Failure | Spec/12 reason | Notes |
|---|---|---|
| `CharacterRosterGoal` references a missing template | `catalog_invalid` | exists |
| Account-level coverage rule unsatisfiable (e.g. account banned, faction unavailable) | `task_unrecoverable` | RosterPlanner emits a single warning per session |
| Distance computation throws (corrupted snapshot) | `task_unrecoverable` | defensive — wrap in try/catch; default to MaxValue distance |
| Server caps disable a goal axis (e.g. PvPRank impossible on this server) | (no FailureReason; just exclude axis from sum) | `weights[PvPRank] = 0` for that realm |

No new Spec/12 values needed; existing taxonomy covers this surface.

## ML integration — Objective scoring

**Surface.** `ProgressionPlanner.PickNextObjective(snapshot, db)`
ranks candidate Activities by expected `roster_distance_delta`. When
two candidates tie within an epsilon of `1e-3`, the planner consults
`IDecisionEngineClient.GetObjectiveAdviceAsync(ObjectiveContext, ct)`
([`Spec/20 §2.1`](20_DECISION_ENGINE.md#21-proto-wire-shapes)) with the
tied catalog ids and the bot's current 8-axis `roster_goal_distance`.
The advisor returns a tie-breaker; fail-soft to lowest-id lex tie-break.

**Why advisory not authoritative.** The deterministic ranker (expected
delta computed from catalog `EstimatedRewardValue` + travel cost)
suffices; the advisor only kicks in on ties — and only nudges,
never vetoes. A bot with the advisor pinned to `NoAdvice` still
converges on its goals, just less optimally.

**Input feature vector.** `ObjectiveContext.roster_goal_distance[8]`
is the per-axis distance from this Spec. The advisor receives the
candidate Activity ids in `tied_objective_ids` and the per-axis
distance vector — see [`Spec/20 §4.2`](20_DECISION_ENGINE.md#42-onnx-feature-tensor-shapes-per-advisor)
Objective row for the tensor shape.

**Output shape.** `ObjectiveAdvice.RecommendedObjectiveId` — must be
in the tied set per Spec/19 §9.

**Three maturity phases** per [`Spec/20 §5`](20_DECISION_ENGINE.md):

| Phase | Source | Wire |
|---|---|---|
| 1 — Heuristic | Lowest catalog-id lex among ties | `ProgressionPlanner` default |
| 2 — Rules + lookup | `Config/decision-engine/objective-tie-rules.json` per `(ActivityFamily, level_band)` | Plan/14 S10.6 |
| 3 — ONNX | `Models/objective/v1.onnx` trained on labeled traces | Plan/14 S10.6 Mode=Ml |

**Fail-soft fallback.** `NoAdvice` or out-of-set recommendation →
lowest-id lex tie-break. Asserted by Plan/14 slot S10.8
`DecisionEngine_DynamicProgressive_AllAdvisorsForcedToNoAdvice`.

**Live-validation guard.** Replaying any progression trace with the
Objective advisor forced to `NoAdvice` MUST still produce
`outcome.roster_distance_delta ≤ 0`. The deterministic ranker can
make slower choices but never anti-progressive ones.

## Dynamic-progressive invariant

This spec defines the invariant referenced across the loop:

1. **Dynamic.** `ProgressionPlanner.PickNextObjective(snapshot, db)`
   produces different Objective choices when snapshot inputs differ in
   bot-relevant axes (race / class / level / faction / inventory /
   QuestsCompleted / Reputation / Attunements). Identical snapshots
   produce identical choices (deterministic stable ranking).
2. **Progressive.** Every Activity assignment, after CheckCompletion,
   MUST measurably reduce `RosterPlanner.Distance(snapshot, goal).TotalScalar`
   — i.e. `roster_distance_delta ≤ 0`. Cosmetic-only completions
   return 0; the loop's contract is `≤ 0`, never positive.

The 8-axis breakdown is `(level, gear_tier, attunement_step,
reputation_tier, gold_target_pct, mount_tier, pvp_rank,
profession_skill)`. Failed Activities do NOT advance distance; the
*selection* MUST be progressive in expectation (highest expected
positive delta first), not every individual run.

## Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| (no slot yet) | `Services/WoWStateManager/Progression/RosterPlanner.cs` (new) | §RosterPlanner.Distance |
| (no slot yet) | `Services/WoWStateManager/Progression/ProgressionPlanner.cs` (existing — but no slot in the current Plan) | §ProgressionPlanner |
| [`Plan/14/S10.2`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) | Tie-breaker via `GetObjectiveAdviceAsync` | §ML integration |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | `WoWActivitySnapshot.roster_distance` field 46 (extend the projection slot) | §Snapshot projection |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace writes `outcome.roster_distance_delta` | §RosterPlanner.Distance |

Two "no slot yet" rows tracked as a follow-up in
[`Plan/SPEC_FILL_LOOP.md`](../Plan/SPEC_FILL_LOOP.md). RosterPlanner +
ProgressionPlanner are referenced as "existing code" by §Existing code
anchors but have no implementation slot in any current Plan — likely
homes are a new Plan/18 (Progression) phase or sub-slots inside
Plan/03 (Phase 2).

## Test surface

Contract tests live at
`Tests/BotRunner.Tests/Progression/ProgressionContractTests.cs`.
Assertions go through `snapshot.roster_distance` (proto field 46) and
trace JSONL `outcome.roster_distance_delta` lines per Test Isolation
Rules.

- **`RosterPlannerDistance_PureFunctionOfSnapshotAndGoal`** — calling
  `RosterPlanner.Distance(...)` twice with identical inputs returns
  identical scalars (no clock/state dependence).
- **`RosterPlannerDistance_FullyAchievedGoalReturnsZero`** — a snapshot
  matching the `CharacterRosterGoal` exactly returns
  `TotalScalar == 0.0f`.
- **`RosterPlannerDistance_PerAxisSumEqualsTotalScalar`** — sum of
  `weights[axis] * perAxis[axis]` equals `TotalScalar` within `1e-5`.
- **`ProgressionPlanner_ObjectiveAdvisorRespectsTieSet`** — when
  DecisionEngine returns `ObjectiveAdvice` outside the tied set, the
  planner falls back to lowest-id lex tie-break.
- **`Progression_DynamicProgressive_DistanceStrictlyDecreasesPerActivityTest`** —
  the dynamic-progressive invariant. Replays a representative trace
  and asserts every `outcome.roster_distance_delta ≤ 0` line; also
  asserts that two synthetic snapshots that differ in
  `(class, level, attunements)` produce DIFFERENT
  `PickNextObjective(...)` choices.

## Existing code anchors

| Concept | File |
|---|---|
| Progression planner | `Services/WoWStateManager/Progression/ProgressionPlanner.cs` |
| Raid composition | `Services/WoWStateManager/Progression/RaidCompositionService.cs` |
| Character settings | `Services/WoWStateManager/Settings/CharacterSettings.cs` |
| Loadout converter | `Services/WoWStateManager/Settings/LoadoutSpecConverter.cs` |
| Loadout task | `Exports/BotRunner/Tasks/LoadoutTask.cs` |
