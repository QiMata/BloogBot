# Spec 19 — AOTA runtime contract (`IActivity` / `IObjective`)

> **What this spec is.** The runtime contract for the top two layers of
> the [`Spec/18_TERMINOLOGY.md`](18_TERMINOLOGY.md) four-layer
> hierarchy. `Activity` and `Objective` exist today only as a data-only
> catalog row (`ActivityDefinition`) and as free-form snapshot strings.
> This spec is the **target runtime surface** that Phase 2 slot **S2.0**
> ships — modelled on D2Bot's `IActivity` / `BotObjectiveContract`.
> Architecture deep-dive in [`architecture/aota/`](../architecture/aota/);
> dynamic-composition algorithm in
> [`architecture/aota/03_DYNAMIC_COMPOSITION.md`](../architecture/aota/03_DYNAMIC_COMPOSITION.md).

## 1. Layer recap

| Layer | Runtime today | Phase-2 target |
|---|---|---|
| Activity | `AssignedActivity` string parsed into a head `IBotTask` by `ActivityResolver` | `IActivity` interface; instantiated *from* `ActivityDefinition` + `IActivityParameters` |
| Objective | snapshot strings `travel_objective` + `progression_status.current_objective` | `IObjective` interface; sequence inside the active `IActivity` |
| Task | shipped: `IBotTask` (S1.0 async contract) | (no change) |
| Action | shipped: `ActionType` enum + `ActionMessage` proto | (no change) |

## 2. `IActivity` contract

```csharp
public interface IActivity
{
    string Id { get; }                        // catalog id, e.g. "dungeon.ubrs"
    ActivityFamily Family { get; }
    IActivityParameters Parameters { get; }

    IReadOnlyList<IObjective> Objectives { get; }   // composed at construction
    IObjective? CurrentObjective { get; }            // null when complete

    // Pump - called by the BotRunner loop when this activity is assigned.
    Task<ActivityTickResult> TickAsync(BotTaskContext context,
                                        WoWActivitySnapshot snapshot,
                                        CancellationToken ct);

    // Completion predicate, evaluated after every snapshot.
    ActivityCompletion CheckCompletion(WoWActivitySnapshot snapshot);

    // Failure path - parent (StateManager mode handler) decides next.
    void OnObjectiveFailed(IObjective objective, FailureReason reason);
}

public sealed record ActivityTickResult(
    ActivityTickStatus Status,                 // InProgress | Complete | Failed | Suspended
    ActionMessage? NextAction,                  // null when no action this tick
    string? RationaleTag);                      // for snapshot projection + tracing

public sealed record ActivityCompletion(
    bool IsComplete,
    string? FinalObjectiveId,
    FailureReason? Reason);
```

`TickAsync` is **idempotent within a tick** — multiple snapshot deliveries
in the same tick window should not produce multiple `NextAction`s. Used
by tests to drive Activity × Objective permutations against a recorded
snapshot stream.

## 3. `IObjective` contract

```csharp
public interface IObjective
{
    string Id { get; }                          // "ubrs.reach-flame-crest"
    ObjectiveType Type { get; }
    IObjectiveEndState EndState { get; }        // predicate over snapshot
    IReadOnlyList<ObjectiveGate> Gates { get; } // start-time preconditions

    IBotTask BuildHeadTask(BotTaskContext ctx); // first Task this Objective pushes
    bool CheckCompletion(WoWActivitySnapshot snapshot);

    // Optional - emits when the Task stack pops the head Task with a
    // non-Complete terminal status.
    void OnHeadTaskTerminal(BotTaskStatus terminal, string? reason);
}

public sealed record ObjectiveGate(
    string Description,
    Func<WoWActivitySnapshot, bool> Predicate);

public interface IObjectiveEndState
{
    bool IsSatisfied(WoWActivitySnapshot snapshot);
    string DiagnosticLabel { get; }   // "QuestLog[slotForQ132].Counter >= 8"
}
```

`BuildHeadTask` is the **only** place an Objective produces a Task. The
Task may itself push child tasks (R-T1 from
[`architecture/aota/01_LAYERS.md`](../architecture/aota/01_LAYERS.md));
the Objective does not push children directly.

## 4. `ObjectiveType` enum (closed set; proto-mirrored)

```csharp
public enum ObjectiveType
{
    Travel = 0,            // arrive at a named-location position
    Interact = 1,          // open conversation / NPC click
    AcceptQuest = 2,
    TurnInQuest = 3,
    Kill = 4,              // kill N of creatureEntry
    Collect = 5,           // gather N of itemId
    UseGameObject = 6,     // chest, door, lever, herb, ore, fishing pool
    CastSpell = 7,         // quest "cast X on Y" or class quest spell
    Escort = 8,            // follow NPC to destination
    EncounterTrash = 9,    // dungeon/raid trash leg
    EncounterBoss = 10,    // single named boss
    Loot = 11,             // claim corpse / loot window
    Queue = 12,            // BG / dungeon-finder queue
    Cap = 13,              // BG node capture / flag cap
    Hold = 14,             // BG node defense hold
    Craft = 15,            // recipe cast loop
    Train = 16,            // trainer interaction
    Bank = 17,             // bank deposit / withdraw
    Mail = 18,             // send / retrieve
    Auction = 19,          // post / cancel / buy
    Vendor = 20,           // sell / buy / repair
    Rebind = 21,           // hearthstone bind
    Equip = 22,            // gear slot change
    Loop = 23,             // gathering route, hotspot loop, any "until X" sweep
    GroupForm = 24,        // social - invite/accept quorum
    WorldEventStage = 25,  // STV Extravaganza tag-in, Darkmoon Faire visit
    Reserved26..63,
}
```

`ObjectiveType` is mirrored as a proto3 enum on the
`WoWActivitySnapshot.current_objective_type` field. New values are
proto-additive (rule R10 in the monorepo CLAUDE.md).

## 5. Snapshot projection

`WoWActivitySnapshot` gains four identity / advice-trace fields (proto3
additive — slot **S2.0** delivers; today's highest assigned field is
`has_pending_group_invite = 32`):

```protobuf
string        current_activity_id     = 33;   // catalog id; empty when no Activity
string        current_objective_id    = 34;   // Objective id; empty when between
ObjectiveType current_objective_type  = 35;   // enum mirror of section 4
repeated AdviceLogEntry advice_log    = 36;   // ring buffer cap 8 — Spec/20 RPCs land here

// New companion message — declared in the same proto file
message AdviceLogEntry {
    string advisor     = 1;   // "objective" | "reward" | "rotation" | "threat"
    string rationale   = 2;   // Spec/20 RotationAdvice.Rationale et al.
    float  confidence  = 3;
    uint32 used_index  = 4;   // composer's actually-picked index (or 0xFFFFFFFF when discarded)
    uint32 timestamp   = 5;   // unix-seconds of the advice request
}
```

Plus the existing top-of-stack `currentTaskName` (Task) and
`recent_command_acks` (Actions). Both retain their existing field
numbers; no renames.

This is the **only** way a test or the UI observes Activity / Objective
identity. Per [`CLAUDE.md → Test Isolation Rules`](../../CLAUDE.md#test-isolation-rules--critical),
tests assert on these snapshot fields, never on internal `IActivity` /
`IObjective` instances. The `advice_log` ring buffer is also the only
legal way a test asserts that DecisionEngine advice reached the
composer (Spec/20 §3 forbids tests from poking the
`IDecisionEngineClient` mock directly).

## 6. Composition entry point

```csharp
public interface IActivityComposer
{
    IActivity Compose(ActivityDefinition definition,
                       WoWActivitySnapshot snapshot,
                       IMangosCatalog db,
                       UnlockGraph unlocks,
                       IActivityParameters parameters);
}
```

The composer implements the algorithm in
[`architecture/aota/03_DYNAMIC_COMPOSITION.md`](../architecture/aota/03_DYNAMIC_COMPOSITION.md):
walks `quest_template` + `creature_template` + `gameobject_template` +
`item_template` + `areatrigger_teleport` + `npc_vendor` + `npc_trainer`
+ loot templates, filters by snapshot + faction + race + class,
synthesizes the Objective sequence, prepends precondition Objectives,
attaches priority / cost metadata.

Per-family `Compose<Family>Objectives` sub-algorithms live in
`Services/WoWStateManager/Activities/Composers/`. Phase 2 ships one per
`ActivityFamily` enum value.

## 7. Lifecycle inside the BotRunner

```
StateManager assigns Activity:
   AssignedActivity = "dungeon.ubrs"
   ↓ ActivityResolver.Resolve(assignedString)
   ↓ IActivityComposer.Compose(def, snapshot, db, unlocks, params)
   ↓ IActivity instance owned by BotRunner

each tick:
   IActivity.TickAsync(ctx, snapshot, ct) →
       if CurrentObjective.CheckCompletion(snapshot):
           advance CurrentObjective
       if CurrentObjective.Gates are satisfied:
           if Task stack empty: push CurrentObjective.BuildHeadTask(ctx)
       else:
           suspend (Gates not yet met; surface as Suspended)

       return ActivityTickResult { Status, NextAction, RationaleTag }

   Then: BotRunner runs the Task-stack loop (S1.0 contract):
       top.TickAsync(ctx, ct)
       if Status == Complete: pop
       if Status == Failed: OnChildFailedAsync chain
```

## 8. Failure-reason mapping

`FailureReason` is **owned by [`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md)** —
that file is the single source of truth. The `OnObjectiveFailed(IObjective,
FailureReason)` callback in §2 and the `ActivityCompletion.Reason` field
both take the same enum.

Objective-shaped failures map onto Spec/12 reasons as follows:

| Objective failure surface | Spec/12 `FailureReason` |
|---|---|
| `ObjectiveGate` predicate returns false on entry | `task_precondition_failed` |
| `IObjectiveEndState` unsatisfiable for current snapshot | `task_unrecoverable` |
| Child `IBotTask` returned `BotTaskStatus.Aborted` | `task_cancelled` |
| `CheckCompletion` still false past Activity-level deadline | `task_timeout` |
| `ActivityDefinition.EntryRequirements` unmet after precondition pass | one of `missing_attunement`, `missing_key`, `missing_reputation`, `missing_level`, `missing_flight_path`, or `illegal_activity_request` per Spec/12 |
| Raid/dungeon lockout blocks entry (Spec/22 World Cycles) | `lockout_active` |
| `ObjectiveType` the BotRunner cannot drive | `catalog_invalid` |
| Operator / cancel-token canceled the Activity | `task_cancelled` |

Two new values **may need adding** to Spec/12 once the runtime ships
and reveals failure modes Spec/12 cannot label cleanly. Tracked as
follow-ups in [`Plan/SPEC_FILL_LOOP.md`](../Plan/SPEC_FILL_LOOP.md)
rather than committed here:

- `objective_end_state_unreachable` — distinct from `task_unrecoverable`
  if composer-vs-runtime disagreement is a category we want to alert on.
- `objective_decision_engine_rejected` — only matters if Phase-3 ML
  ever returns a hard veto rather than a confidence value.

Snapshot projection (additive field on `WoWActivitySnapshot`, S2.0):

```protobuf
FailureReason last_objective_failure = 37;   // mirror of Spec/12 enum;
                                               // empty/none when current Objective is healthy
```

## 9. ML integration — Composer tiebreaker

**Surface.** `IActivityComposer.Compose(...)` is deterministic by
default (topological sort + priority + travel-cost). When two or more
Objectives tie on all stable-sort keys (priority, soonest expiring,
travel cost, unlock fan-out), the composer queries
`IDecisionEngineClient.GetObjectiveAdviceAsync(ctx, ct)` (Spec/20 §2)
and uses the returned `ObjectiveAdvice.RecommendedObjectiveId` as the
tiebreaker.

**Why advisory not authoritative.** Per Spec/20 §1, the composer
treats `NoAdvice` (timeout, service down, model failure) as "fall
through to the deterministic id-lexicographic tie-break". The composed
list shape is therefore stable under DecisionEngine outages.

**Input feature vector** (`ObjectiveContext`, fixed shape, declared in
`Services/DecisionEngineService/Contexts/ObjectiveContext.cs`):

| Field | Shape | Source |
|---|---|---|
| `bot_level` | uint8 | snapshot.Player.Level |
| `bot_class` | uint8 | snapshot.Player.Class |
| `bot_race` | uint8 | snapshot.Player.Race |
| `bot_position` | float[3] | snapshot.MovementData.Position |
| `current_zone_id` | uint16 | snapshot.Player.ZoneId |
| `current_map_id` | uint16 | snapshot.CurrentMapId |
| `inventory_value_copper` | uint32 | snapshot-derived (sum of `BankItems` + bag inventory sell prices) |
| `tied_objective_ids` | string[N] | composer's tie set (N ≤ 8 — composer truncates) |
| `tied_objective_types` | ObjectiveType[N] | parallel to tied_objective_ids |
| `tied_objective_costs` | float[N] | composer's travel-cost estimates |
| `tied_unlock_fanout` | uint16[N] | composer's downstream-unlock counts |
| `roster_goal_distance` | float[8] | distance-to-CharacterRosterGoal per axis (Spec/05): level, gear tier, attunement step, rep tier, gold target, mount tier, PvP rank, profession-skill cap |

**Output shape** (`ObjectiveAdvice`, declared in Spec/20 §3):

```csharp
public sealed record ObjectiveAdvice(
    string? RecommendedObjectiveId,   // must equal one of ObjectiveContext.tied_objective_ids
    float   Confidence,                 // 0..1
    string  Rationale);
```

**Maturity phases** (per Spec/20 §5):

| Phase | Source | Live when |
|---|---|---|
| 1 — Heuristic | `Services/DecisionEngineService/Heuristics/ObjectiveTieHeuristic.cs` — picks lowest `tied_objective_costs` then lowest `Id` | Plan/14 slot S10.2 + S10.6 (Mode=Trivial) |
| 2 — Rules + lookup | `Config/decision-engine/objective-tie-rules.json` — per `(ActivityFamily, ObjectiveType)` precedence table | Plan/14 slot S10.6 (Mode=Rules) |
| 3 — ONNX | `Services/DecisionEngineService/Models/objective/v1.onnx` — trained on labeled traces under `tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl` (Plan/14 slot S10.7) | Plan/14 slot S10.6 (Mode=Ml) once trained |

**Fail-soft fallback.** Composer drops back to deterministic id-lex
tie-break when `ObjectiveAdvice.RecommendedObjectiveId` is null, has
`Confidence < 0.5`, or does not appear in
`ObjectiveContext.tied_objective_ids`.

**Live-validation guard.** Plan/14 slot S10.8 ships
`Tests/BotRunner.Tests/Activities/ObjectiveAdviceContractTests.cs`
asserting that for every snapshot in
`tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl`, replacing the
advice with a `NoAdvice` causes the same Activity to **still complete**
within the spec's wall-clock budget. This is the canary that proves ML
cannot break correctness — only nudge.

## 10. Dynamic-progressive invariant

The composed Objective list MUST satisfy two properties on every
`TickAsync`:

1. **Dynamic.** `IActivityComposer.Compose(def, snapshot1, db, unlocks, params)`
   and `Compose(def, snapshot2, db, unlocks, params)` produce
   different orderings when `snapshot1` and `snapshot2` differ in any
   bot-relevant axis: `Player.Race`, `Player.Class`, `Player.Level`,
   `Player.Faction`, `Inventory.Items`, `QuestsCompleted`,
   `Reputation.Standings`, `Attunements`. Identical snapshots produce
   identical orderings (deterministic stable sort).
2. **Progressive.** Every `ActivityCompletion { IsComplete = true }`
   strictly reduces the `RosterPlanner.Distance(snapshot, goal)`
   metric defined in [`Spec/05_PROGRESSION.md`](05_PROGRESSION.md). The
   invariant is checked once per Activity completion; it is **not**
   required per Objective (some Objectives are setup-only and do not
   close goal distance).

Both properties are testable from the snapshot stream alone — no
private `IActivity` state needed. Slot S2.0 ships the contract test
that asserts both (named in section 11 below).

## 11. Test contract

Phase 2 slot S2.0 ships **four** contract tests at
`Tests/BotRunner.Tests/Activities/IActivityContractTests.cs`. The first
three are the deterministic-composer tests; the fourth asserts the
dynamic-progressive invariant.

1. **`NextObjective_ReturnsTopologicalNext`** — given a snapshot at the
   start of an Activity, the *first* Objective is the one with no
   unmet predecessors and the highest priority. Asserts via
   `WoWActivitySnapshot.current_objective_id`.
2. **`NextObjective_SkipsCompletedObjectives`** — advancing the
   snapshot to reflect Objective[0] completion causes Objective[1] to
   be returned next. Asserts via the
   `(current_activity_id, current_objective_id)` snapshot pair.
3. **`ComposeObjectives_HonorsEntryRequirements`** — missing required
   item / quest / rep causes the composed list to **prepend** the
   precondition Objectives, not fail outright. Asserts via the
   prefix sequence emitted in `WoWActivitySnapshot.current_objective_id`
   across N composer ticks.
4. **`AotaRuntime_DynamicProgressive_ComposerProducesDifferentOrderingsPerSnapshotTest`** —
   the dynamic-progressive invariant from §10. Drives
   `IActivityComposer.Compose(...)` against ≥3 distinct synthetic
   snapshots that differ in `(Race, Class, Level, QuestsCompleted)`
   and asserts (a) the composed `Objectives` lists differ in either
   contents or order, and (b) each successful Activity completion
   reduces `RosterPlanner.Distance(snapshot, goal)`.

Two additional ML-surface contract tests ship alongside slot **S10.8**
(Plan/14) — they are listed here so the test surface is colocated:

5. **`ObjectiveComposer_NoAdvice_FallsThroughToDeterministicTieBreak`** —
   `NoAdvice` from DecisionEngineClient produces a stable id-lex
   tie-break.
6. **`ObjectiveComposer_AdviceOutsideTieSet_IsIgnored`** — advice that
   names an `Id` not in `ObjectiveContext.tied_objective_ids` is
   discarded and the deterministic tie-break is used. Asserts via
   `WoWActivitySnapshot.advice_log[].used_index == 0xFFFFFFFF`.

Live-validation tests are named per
[`Spec/18_TERMINOLOGY.md#test-naming-convention`](18_TERMINOLOGY.md#test-naming-convention):
`<Activity>_<Objective>_Tests.cs`. They drive a real bot through the
Activity end-to-end and assert that the snapshot's
`(current_activity_id, current_objective_id)` pairs match the composed
sequence in order, AND that the
[`AotaRuntime_DynamicProgressive_*`](#11-test-contract) invariant holds
across the trajectory.

## 12. Plan-slot cross-reference

| Slot | Files | Ships |
|---|---|---|
| [`Plan/03/S2.0`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s20--iactivity--iobjective-runtime-contracts) | `Exports/BotRunner/Activities/IActivity.cs`, `IObjective.cs`, `ActivityResolver.cs`, `Exports/BotCommLayer/Models/ProtoDef/communication.proto`, `Tests/BotRunner.Tests/Activities/IActivityContractTests.cs` | Sections §2-§7 of this spec, plus the four tests in §11. |
| [`Plan/13/S9.x`](../Plan/13_PHASE9_CATALOG_FILL.md) | per-Activity `Config/activities/<id>.json` rows | Composer's seed-objective synthesis from full catalog (§6). |
| [`Plan/14/S10.2`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) | per-family `*Composer.cs` consults `GetObjectiveAdviceAsync` on tie | ML integration entry point (§9). |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | `WoWActivitySnapshot.advice_log` (field 36 here) | Composer-advice trace projection (§5). |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `Config/decision-engine/objective-tie-rules.json`, `Services/DecisionEngineService/Heuristics/ObjectiveTieHeuristic.cs`, `ModelDescriptor.cs` | Phase-1/2/3 mode selection (§9). |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | `Services/DecisionEngineService/Models/objective/v1.onnx` + trace producer | Phase 3 ONNX inference (§9). |
| [`Plan/14/S10.8`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s108--livevalidation-for-advisor-wire) | `Tests/BotRunner.Tests/Activities/ObjectiveAdviceContractTests.cs` | Tests #5 and #6 in §11; live-validation correctness guard. |

## 13. Backward-compatibility

`ActivityResolver` continues to accept the legacy `AssignedActivity`
string and produce an `IBotTask` directly *until S2.0 lands*. After
S2.0, the resolver wraps the composed `IActivity` in a single
synthetic `IBotTask` adapter so the existing BotRunner loop keeps
working without rewriting every shipped Task.

The Phase-12 test-isolation refactor (Plan/12) rewrites the 117
Category-A LiveValidation tests cataloged in
[`Audits/test-isolation-audit.md`](../Audits/test-isolation-audit.md)
to drive `Activity × Objective` instead of constructing raw
`ActionMessage` objects in the test body.

## 14. Why this is its own spec

Before this spec, the `IActivity` / `IObjective` runtime shape was
documented across three files: a paragraph in `Spec/18_TERMINOLOGY.md`,
a forward reference in `Spec/03_BOTRUNNER.md`, and slot S2.0 in
`Plan/03_PHASE2_ONDEMAND_ENGINE.md`. This split made it hard to find
the canonical surface. Lifting it into `Spec/19` keeps the runtime
contract stable while the slot detail in `Plan/03` covers *how to
land* it.
