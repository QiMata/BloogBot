# Spec 18 — Terminology: the Activity / Objective / Task / Action stack

> Canonical four-layer hierarchy adopted from D2Bot (`E:\repos\D2Bot\`).
> This page is the single source of truth for what these four words mean
> across WWoW. Read this before adding new "behavior tree", "task family",
> or "action mapping" terms — those terms map onto these four layers and
> using them as synonyms drifts the spec.

## The four layers

```
Activity     ← major dynamic event:        "Run UBRS", "Molten Core raid", "Warsong Gulch", "Fish at Ratchet"
  └─ Objective  ← high-level state change (WIRE-LEVEL):  "Reach the instance portal", "Kill the next boss"
       └─ Task     ← orchestrates Actions for ONE minute state change + verification:  "Travel to (x,y,z)", "Loot corpse + verify bag"
            └─ Action  ← reusable code primitive matching a player capability (LOCAL code):  ReadLootWindow(), PressHotkeyAsync("1"), IObjectManager.CastSpellAsync(id, target)
```

| Layer | Plural noun | Granularity | Lives where |
|---|---|---|---|
| **Activity** | "the bot's current Activity" | Major, usually-dynamic event supporting any number of characters at once. Catalog-row driven. | [`Exports/GameData.Core/Models/Activities/ActivityDefinition.cs`](../../Exports/GameData.Core/Models/Activities/ActivityDefinition.cs) catalog. **No runtime `IActivity` interface yet** — slated for Phase 2 (see [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)). |
| **Objective** | "the bot's current Objective" | High-level state change composed of multiple Tasks. **What actually travels on the wire as `ObjectiveMessage`** (formerly `ActionMessage` — misnomer cleaned up in the 2026-05-21 rename). | Wire type: `ObjectiveMessage` in [`Exports/BotCommLayer/Models/ProtoDef/communication.proto`](../../Exports/BotCommLayer/Models/ProtoDef/communication.proto). BotRunner decomposes via behavior tree + game DB knowledge lookups into Tasks. Snapshot fields `WoWActivitySnapshot.travel_objective` + `progression_status.current_objective` mirror the in-flight Objective; a general `IObjective` interface is slated for Phase 2. |
| **Task** | "the task stack" (LIFO) | Orchestrates Actions for ONE minute state change with verification + failure handling. Pushes child Tasks (`GoToTask` is the universal child). | [`Exports/BotRunner/Interfaces/IBotTask.cs`](../../Exports/BotRunner/Interfaces/IBotTask.cs) — Phase-1 target contract. Concrete tasks under `Exports/BotRunner/Tasks/` and `BotProfiles/<ClassSpec>/Tasks/`. |
| **Action** | "an Action" | **Reusable code primitive** (methods + variables) mirroring a single thing a human player can do: read the loot window, check a bag slot, press a hotkey, click a world coord, read a unit field, cast a spell. **Pure local code — Actions do NOT cross any wire.** | `IObjectManager` accessors + state objects + write methods. The `ObjectiveType` enum (~85 values) in `communication.proto` is the **roster of Action kinds the StateManager can request via an `ObjectiveMessage`** — not the Action itself. |

## Worked example — "Run UBRS"

| Layer | Concrete value |
|---|---|
| Activity | `dungeon.ubrs` (ActivityDefinition row in the hard-coded catalog) |
| Objective DAG (each Objective is one `ObjectiveMessage` on the wire) | `reach-flame-crest` → `enter-instance-portal` → `clear-trash-to-rend` → `kill-rend-blackhand` → `loot-rend` → `exit-instance` |
| Task stack for `reach-flame-crest` (BotRunner decomposes the incoming `ObjectiveMessage` into Tasks via behavior tree + DB lookups) | `TravelToTask(coord)` → pushes `BoardTransportTask(zeppelin_og)` → pushes `WalkToCoordTask(deck_anchor)` → pushes `WaitForLandingTask` |
| Actions invoked by Tasks (reusable code primitives — never cross the wire) | `ReadPlayerPosition()` (read), `IObjectManager.MoveToAsync(coord)` (write), `ReadCurrentZone()` (read), `IsWithinInteractRange(target)` (predicate), `IObjectManager.InteractAsync(zeppelin)` (write + verify) |

## Layer ownership rules

- An **Activity** decomposes into Objectives gated by snapshot state
  changes. The DecisionEngine composes the Objective list dynamically
  from the MaNGOS DB — see
  [`docs/architecture/aota/03_DYNAMIC_COMPOSITION.md`](../architecture/aota/03_DYNAMIC_COMPOSITION.md).
- An **Objective** is the wire-level unit of dispatch. StateManager
  sends an `ObjectiveMessage` (formerly `ActionMessage`) to a BotRunner;
  the BotRunner uses a behavior tree + game database knowledge lookups
  to decompose it into Tasks. Adding a new Objective kind is an
  `ObjectiveType` enum extension (proto change, rule R10).
- A **Task** orchestrates Actions to drive one minute state change.
  Tasks compose other Tasks via push (the LIFO task stack); `GoToTask`
  is the universal child. **Every Task must include verification** —
  read state via Actions after every dispatch, fail properly if the
  expected state change didn't happen.
- An **Action** is reusable code that mirrors what a human player can
  do. Multiple Tasks call the same Action methods. Actions invoke
  `IObjectManager` helpers (which may emit packets or read memory).
  **Actions are local code; no Action is ever a wire message.**
- **Only `ObjectiveMessage` crosses the StateManager↔BotRunner TCP
  boundary.** Activity assignment, Objective dispatch, snapshot
  reporting, and command acks are all wire-visible. Task identity is
  projected onto the snapshot (top-of-stack `current_task_name`).
  Action invocation is purely local — never visible to StateManager.

## What today's WWoW uses for each layer

| Layer | Today's surface | Phase-1 target | Phase-2 target |
|---|---|---|---|
| Activity | `ActivityDefinition` catalog row + `AssignedActivity` string (`"Fishing[Ratchet]"`) parsed by [`ActivityResolver`](../../Exports/BotRunner/Activities/ActivityResolver.cs). Returns an `IBotTask` directly — no `IActivity` object. | (no change) | New `IActivity` runtime contract modeled on D2's [`IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs). Instantiated *from* an `ActivityDefinition`. |
| Objective | Snapshot-only: `WoWActivitySnapshot.travel_objective` (travel-specific) + `progression_status.current_objective` (free-form string). No `IObjective` interface. | (no change) | New `IObjective` runtime contract modeled on D2's [`BotObjectiveContract`](../../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveRuntimeContracts.cs). New `ObjectiveType` enum on the proto. |
| Task | [`IBotTask`](../../Exports/BotRunner/Interfaces/IBotTask.cs) + `BotTaskStatus` enum. Stack-based. | Phase-1 closes the IBotTask substrate per [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md). | (no change) |
| Action | `ObjectiveType` enum (~85 values) + `ObjectiveMessage` over protobuf. | (no change) | (no change — closed set) |

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
observed in `WoWActivitySnapshot`. Tests must not drive `ObjectiveMessage`
dispatch directly — see [WWoW CLAUDE.md → Test Isolation
Rules](../../CLAUDE.md#test-isolation-rules) for the enforcement contract.

## Related files

- [`architecture/aota/`](../architecture/aota/) — **Architecture deep-dive on the four layers, recursive composition rules, dynamic Objective composition from the MaNGOS DB, and cross-game portability template.** Extends this canonical glossary.
- [`Spec/03_BOTRUNNER.md`](03_BOTRUNNER.md) — IBotRunner / IBotTask contract
- [`Spec/04_ACTIVITIES.md`](04_ACTIVITIES.md) — ActivityDefinition catalog + OnDemand legality
- [`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md) — Phase-1 IBotTask closure
- [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md) — Phase-2 IActivity / IObjective runtime contracts (slot S2.0)
- D2Bot reference: [`D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs), [`D2Bot/D2Orchestrator/Orchestration/ObjectiveContracts.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveContracts.cs), [`D2Bot/D2Orchestrator/Core/IBotTask.cs`](../../../../D2Bot/D2Orchestrator/Core/IBotTask.cs)
