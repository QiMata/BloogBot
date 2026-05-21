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
       └─ Task     ← orchestrates Actions for ONE minute state change + verification:  TravelToTask, LootCorpseTask, InviteToPartyTask, CastSpellTask
            └─ Action  ← ATOMIC code primitive — ONE thing a player can do (LOCAL code):  ReadMemoryDword(addr), WriteMovementBit(forward), SendOpcode(0x117, payload), PressKey('1'), ReadUnitField(guid, off)
```

> **Granularity note.** Things like `MoveToCoord`, `InviteToParty`, `WalkToWorld`, `LootCorpse`, `CastSpell` are **Tasks**, not Actions — each composes many Actions (read transform, flip a movement bit, send opcode, read response, verify state, repeat). An Action is the SINGLE smallest player-capable primitive: one memory read, one bit write, one opcode send, one key press. `IObjectManager` methods that wrap multi-opcode sequences (`MoveToAsync`, `CastSpellAsync`, `LootTargetAsync`) are **Task-level** convenience wrappers; the Actions they invoke internally are the truly-atomic primitives below them.

| Layer | Plural noun | Granularity | Lives where |
|---|---|---|---|
| **Activity** | "the bot's current Activity" | Major, usually-dynamic event supporting any number of characters at once. Catalog-row driven. | [`Exports/GameData.Core/Models/Activities/ActivityDefinition.cs`](../../Exports/GameData.Core/Models/Activities/ActivityDefinition.cs) catalog. **No runtime `IActivity` interface yet** — slated for Phase 2 (see [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)). |
| **Objective** | "the bot's current Objective" | High-level state change composed of multiple Tasks. **What actually travels on the wire as `ObjectiveMessage`** (formerly `ActionMessage` — misnomer cleaned up in the 2026-05-21 rename). | Wire type: `ObjectiveMessage` in [`Exports/BotCommLayer/Models/ProtoDef/communication.proto`](../../Exports/BotCommLayer/Models/ProtoDef/communication.proto). BotRunner decomposes via behavior tree + game DB knowledge lookups into Tasks. Snapshot fields `WoWActivitySnapshot.travel_objective` + `progression_status.current_objective` mirror the in-flight Objective; a general `IObjective` interface is slated for Phase 2. |
| **Task** | "the task stack" (LIFO) | Orchestrates Actions for ONE minute state change with verification + failure handling. Pushes child Tasks (`GoToTask` is the universal child). | [`Exports/BotRunner/Interfaces/IBotTask.cs`](../../Exports/BotRunner/Interfaces/IBotTask.cs) — Phase-1 target contract. Concrete tasks under `Exports/BotRunner/Tasks/` and `BotProfiles/<ClassSpec>/Tasks/`. |
| **Action** | "an Action" | **ATOMIC code primitive — ONE thing the player can do**: one memory read, one bit write, one opcode send, one key press. Examples: `ReadMemoryDword(addr)`, `ReadUnitField(guid, fieldOffset)`, `WriteMovementBit(forward, true)`, `SendOpcode(CMSG_CAST_SPELL, payload)`, `PressKey('1')`. **Pure local code — Actions do NOT cross any wire.** Compound things like `MoveToCoord` / `InviteToParty` / `CastSpell` are Tasks (they compose many Actions over many ticks). | Atomic memory + packet + input primitives. `IObjectManager` *helper methods* like `CastSpellAsync` are Task-level wrappers; the truly-atomic Actions they invoke (memory writes, opcode sends) are what live here. The `ObjectiveType` enum in `communication.proto` is the **roster of Objective kinds StateManager can request via an `ObjectiveMessage`** — not the Action set. |

## Worked example — "Run UBRS"

| Layer | Concrete value |
|---|---|
| Activity | `dungeon.ubrs` (ActivityDefinition row in the hard-coded catalog) |
| Objective DAG (each Objective is one `ObjectiveMessage` on the wire) | `reach-flame-crest` → `enter-instance-portal` → `clear-trash-to-rend` → `kill-rend-blackhand` → `loot-rend` → `exit-instance` |
| Task stack for `reach-flame-crest` (BotRunner decomposes the incoming `ObjectiveMessage` into Tasks via behavior tree + DB lookups) | `TravelToTask(coord)` → pushes `BoardTransportTask(zeppelin_og)` → pushes `WalkToCoordTask(deck_anchor)` → pushes `WaitForLandingTask` |
| Atomic Actions invoked by Tasks (never cross the wire) — examples from `WalkToCoordTask` | `ReadUnitField(player_guid, UNIT_FIELD_POSITION_X/Y/Z)` (read player transform per tick) • `WriteMovementBit(forward, true)` (set forward flag) • `SendOpcode(MSG_MOVE_HEARTBEAT, packet)` (heartbeat to server) • `ReadGameTime()` (compute movement delta) • `PressKey('W')` (FG-side) / `SendOpcode(MSG_MOVE_START_FORWARD)` (BG-side) |

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
