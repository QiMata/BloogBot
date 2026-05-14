# Spec 18 — Terminology: the Activity / Objective / Task / Action stack

> Canonical four-layer hierarchy adopted from D2Bot (`E:\repos\D2Bot\`).
> This page is the single source of truth for what these four words mean
> across WWoW. Read this before adding new "behavior tree", "task family",
> or "action mapping" terms — those terms map onto these four layers and
> using them as synonyms drifts the spec.

## The four layers

```
Activity     ← top-level goal:        "Run Wailing Caverns", "Fish at Ratchet"
  └─ Objective  ← sub-goal:           "Reach the instance portal", "Kill the next boss"
       └─ Task     ← behavior tree node: "Travel to (x,y,z)", "Cast Holy Light on tank"
            └─ Action  ← single wire message: ActionMessage{ActionType=TravelTo, ...}
```

| Layer | Plural noun | Granularity | Lives where today |
|---|---|---|---|
| **Activity** | "the bot's current Activity" | Multi-minute. One assigned at a time per bot. Catalog-row driven. | [`Exports/GameData.Core/Models/Activities/ActivityDefinition.cs`](../../Exports/GameData.Core/Models/Activities/ActivityDefinition.cs) catalog. **No runtime `IActivity` interface yet** — slated for Phase 2 (see [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)). |
| **Objective** | "the bot's current Objective" | Tens of seconds to single-minute. Multiple per Activity, sequenced. | **Not yet abstracted.** Today's `WoWActivitySnapshot.travel_objective` and `progression_status.current_objective` are the only Objective-shaped fields on the wire; a general `IObjective` is slated for Phase 2. |
| **Task** | "the task stack" (LIFO) | Single-second to tens-of-seconds. Behavior-tree node. | [`Exports/BotRunner/Interfaces/IBotTask.cs`](../../Exports/BotRunner/Interfaces/IBotTask.cs) — Phase-1 target contract. Concrete tasks live under `Exports/BotRunner/Tasks/` and `BotProfiles/<ClassSpec>/Tasks/`. |
| **Action** | "an action sent over the wire" | Single tick / packet. | `ActionType` enum in [`Exports/BotCommLayer/Models/ProtoDef/communication.proto`](../../Exports/BotCommLayer/Models/ProtoDef/communication.proto) (~85 values). Carried by `ActionMessage`. |

## Worked example — "Run UBRS"

| Layer | Concrete value |
|---|---|
| Activity | `dungeon.ubrs` (ActivityDefinition row in the hard-coded catalog) |
| Objective | `reach-flame-crest` → `enter-instance-portal` → `clear-trash-to-rend` → `kill-rend-blackhand` → `loot-rend` → `exit-instance` |
| Task (one Objective unfolds into many tasks; sample for `reach-flame-crest`) | `TravelToTask(coord)` → pushes `BoardTransportTask(zeppelin_og)` → pushes `WalkToCoordTask(deck_anchor)` → pushes `WaitForLandingTask` |
| Action (one Task unfolds into a stream of actions) | `ActionMessage{ActionType=TravelTo, x,y,z}` then `ActionMessage{ActionType=Interact, target=zeppelin}` then a stream of `ActionMessage{ActionType=StartMovement}` / `StopMovement` over many ticks |

## Layer ownership rules

- An **Activity** can only decompose into Objectives that this codebase
  already knows how to drive. New Objective types require a corresponding
  IBotTask family — adding a new Objective without backing Tasks is a
  spec violation.
- An **Objective** can only push existing **Tasks**. Adding a new
  Objective shape that requires a wholly new behavior-tree node is a
  spec change.
- A **Task** can only emit existing **Actions** (`ActionType` enum
  values). Adding a new ActionType is a protobuf change requiring all
  clients to be regenerated (rule R10).
- **Actions are the only thing that crosses the StateManager↔BotRunner
  TCP boundary.** Activities and Objectives are runtime state derived
  from Activity assignment + Task stack inspection; they are not direct
  wire commands today (Phase 2 adds a `current_activity_id` /
  `current_objective_id` projection to `WoWActivitySnapshot`).

## What today's WWoW uses for each layer

| Layer | Today's surface | Phase-1 target | Phase-2 target |
|---|---|---|---|
| Activity | `ActivityDefinition` catalog row + `AssignedActivity` string (`"Fishing[Ratchet]"`) parsed by [`ActivityResolver`](../../Exports/BotRunner/Activities/ActivityResolver.cs). Returns an `IBotTask` directly — no `IActivity` object. | (no change) | New `IActivity` runtime contract modeled on D2's [`IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs). Instantiated *from* an `ActivityDefinition`. |
| Objective | Snapshot-only: `WoWActivitySnapshot.travel_objective` (travel-specific) + `progression_status.current_objective` (free-form string). No `IObjective` interface. | (no change) | New `IObjective` runtime contract modeled on D2's [`BotObjectiveContract`](../../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveRuntimeContracts.cs). New `ObjectiveType` enum on the proto. |
| Task | [`IBotTask`](../../Exports/BotRunner/Interfaces/IBotTask.cs) + `BotTaskStatus` enum. Stack-based. | Phase-1 closes the IBotTask substrate per [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md). | (no change) |
| Action | `ActionType` enum (~85 values) + `ActionMessage` over protobuf. | (no change) | (no change — closed set) |

## Test-naming convention

Tests should be named after the Activity × Objective pair they exercise:

```
Tests/BotRunner.Tests/LiveValidation/
    Dungeons/
        UbrsAscent_ReachFlameCrest_Tests.cs   // Activity × Objective
        UbrsAscent_KillRend_Tests.cs
    Professions/
        FishingRatchet_CatchStonescale_Tests.cs
```

Tests **assert against the bot's Activity + Objective + Task state** as
observed in `WoWActivitySnapshot`. Tests must not drive `ActionMessage`
dispatch directly — see [WWoW CLAUDE.md → Test Isolation
Rules](../../CLAUDE.md#test-isolation-rules) for the enforcement contract.

## Related files

- [`Spec/03_BOTRUNNER.md`](03_BOTRUNNER.md) — IBotRunner / IBotTask contract
- [`Spec/04_ACTIVITIES.md`](04_ACTIVITIES.md) — ActivityDefinition catalog + OnDemand legality
- [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md) — Phase-1 IBotTask closure
- [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md) — Phase-2 IActivity / IObjective runtime contracts (slot S2.0)
- D2Bot reference: [`D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs), [`D2Bot/D2Orchestrator/Orchestration/ObjectiveContracts.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveContracts.cs), [`D2Bot/D2Orchestrator/Core/IBotTask.cs`](../../../../D2Bot/D2Orchestrator/Core/IBotTask.cs)
