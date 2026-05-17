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

`WoWActivitySnapshot` gains three identity fields (proto3 additive,
S2.0 delivers):

```
string  current_activity_id   = N;   // catalog id; empty when no Activity
string  current_objective_id  = N+1; // Objective id; empty when between
ObjectiveType current_objective_type = N+2;
```

Plus the existing top-of-stack `currentTaskName` (Task) and
`RecentCommandAcks[]` (Actions).

This is the **only** way a test or the UI observes Activity / Objective
identity. Per [`CLAUDE.md → Test Isolation Rules`](../../CLAUDE.md#test-isolation-rules--critical),
tests assert on these snapshot fields, never on internal `IActivity` /
`IObjective` instances.

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

## 8. Test contract

Phase 2 slot S2.0 ships three contract tests at
`Tests/BotRunner.Tests/Activities/IActivityContractTests.cs`:

1. **`NextObjective_ReturnsTopologicalNext`** — given a snapshot at the
   start of an Activity, the *first* Objective is the one with no
   unmet predecessors and the highest priority.
2. **`NextObjective_SkipsCompletedObjectives`** — advancing the
   snapshot to reflect Objective[0] completion causes Objective[1] to
   be returned next.
3. **`ComposeObjectives_HonorsEntryRequirements`** — missing required
   item / quest / rep causes the composed list to **prepend** the
   precondition Objectives, not fail outright.

Live-validation tests are named per
[`Spec/18_TERMINOLOGY.md#test-naming-convention`](18_TERMINOLOGY.md#test-naming-convention):
`<Activity>_<Objective>_Tests.cs`. They drive a real bot through the
Activity end-to-end and assert that the snapshot's
`(current_activity_id, current_objective_id)` pairs match the composed
sequence in order.

## 9. Backward-compatibility

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

## 10. Why this is its own spec

Before this spec, the `IActivity` / `IObjective` runtime shape was
documented across three files: a paragraph in `Spec/18_TERMINOLOGY.md`,
a forward reference in `Spec/03_BOTRUNNER.md`, and slot S2.0 in
`Plan/03_PHASE2_ONDEMAND_ENGINE.md`. This split made it hard to find
the canonical surface. Lifting it into `Spec/19` keeps the runtime
contract stable while the slot detail in `Plan/03` covers *how to
land* it.
