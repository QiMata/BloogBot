# AOTA — Activity / Objective / Task / Action

> **What this tree is.** A working-architecture deep-dive on the canonical
> four-layer behavior hierarchy used across the MMO bot monorepo:
> `Activity → Objective → Task → Action`. Adopted from D2Bot; canonical glossary
> at [`docs/Spec/18_TERMINOLOGY.md`](../../Spec/18_TERMINOLOGY.md).
>
> **What this tree is NOT.** It is not the contract. Contracts live in
> [`docs/Spec/`](../../Spec/) (especially `04_ACTIVITIES.md`, `03_BOTRUNNER.md`,
> `05_PROGRESSION.md`). It is not the implementation plan; that lives in
> [`docs/Plan/`](../../Plan/). This tree explains **how the four layers
> compose recursively, how the DecisionEngine builds them dynamically from
> the MaNGOS database, and how the same model ports to other games in the
> monorepo.**

## The model in one paragraph

The bot does one thing at a time, but "one thing" is structured as four
nested layers. An **Activity** is a multi-minute, end-state-shaped goal
("Run UBRS", "Fish at Ratchet", "Get Seal of Ascension"). It decomposes
into **Objectives** — the shortest slices of state change you can finish
before another Objective becomes the next bottleneck ("reach the
instance portal", "kill the next named", "open the door"). An Objective
unfolds into **Tasks** — behavior-tree nodes that other Tasks reuse
(`GoToTask`, `InteractWithTask`, `LootCorpseTask`, `CastSpellTask`).
A Task ultimately emits **Actions** — single protobuf wire messages
(`ActionMessage{ActionType=TravelTo, …}`).

Each layer composes the layer below it **and itself**. Actions call
Actions (object-manager helpers that internally fire multiple wire
messages and observe their effects). Tasks push Tasks (a `LongTravelTask`
is a sequence of `GoToTask` + `BoardTransportTask` + `TakeFlightPathTask`).
Objectives chain Objectives via prerequisite edges. Activities reference
other Activities as preconditions ("BWL Attune requires UBRS cleared
plus Onyxia attunement chain"). This recursive shape is what lets the
DecisionEngine compose arbitrarily long-horizon plans from a small set
of leaf primitives.

## The four layers at a glance

```
                       (one assigned at a time per bot)
Activity     ──►  "Run UBRS"                      ←─ catalog row
  └─ Objective  ──►  "reach Flame Crest"          ←─ next-blocker slice
       └─ Task     ──►  TravelToTask(coord)       ←─ behavior-tree node
            └─ Action  ──►  ActionMessage{TravelTo,x,y,z}  ←─ wire message
```

| Layer | Granularity | Lifetime on stack | Wire-visible? |
|---|---|---|---|
| **Activity** | minutes to hours | duration of the run | indirect (catalog id projected onto snapshot in Phase 2) |
| **Objective** | seconds to single minute | duration of one state-change slice | indirect (objective id projected onto snapshot in Phase 2) |
| **Task** | tick to tens of seconds | duration on LIFO stack | indirect (top-of-stack name on snapshot) |
| **Action** | single tick / packet | fire-and-forget | **yes** — the only thing that crosses StateManager ↔ BotRunner |

The **only** thing that crosses the StateManager↔BotRunner TCP boundary
is an `ActionMessage`. Activities, Objectives, and Tasks are runtime
state derived from Activity assignment + task-stack inspection.

## Why this matters for a "dynamic" DecisionEngine

The 86-row `ActivityCatalog.cs` is **not enumerated end-to-end** by hand.
For each row, an Objective list is *generated at runtime* by walking the
MaNGOS database (`quest_template`, `creature_template`, `gameobject_template`,
`item_template`, `areatrigger_template`) and synthesizing the
"next-blocker" sequence the bot will follow.

```
  Activity row  +  bot snapshot  +  unlock graph
        │                 │                │
        ▼                 ▼                ▼
   ┌─────────────────────────────────────────────────┐
   │   DecisionEngine.ComposeObjectives(activity)    │
   │   ──── queries mangos.* templates ──────────    │
   │   ──── walks quest-chain DAG ───────────────    │
   │   ──── walks item-requirement DAG ──────────    │
   │   ──── filters by snapshot + level/faction ─    │
   └─────────────────────────────────────────────────┘
                          │
                          ▼
              [ Objective₁ ─► Objective₂ ─► … ]
                          │
                          ▼
        (each Objective unfolds into Tasks at execution time)
```

That is the **dynamic** in "dynamic Objective composition": no
hard-coded objective tables. The same algorithm runs against an
unmodified MaNGOS DB on any 1.12.1 server, against a custom-content
realm with extra quest rows, and against an empty test realm where
only the activity row's minimum prerequisites resolve. Detail in
[`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md).

## File tree

| File | Role |
|---|---|
| [`01_LAYERS.md`](01_LAYERS.md) | Layer definitions in depth + the recursive-composition rules (Actions call Actions, Tasks push Tasks, Objectives chain Objectives, Activities reference Activities). |
| [`02_GAME_LOOPS.md`](02_GAME_LOOPS.md) | WoW's 9 game loops (questing, dungeons, raids, battlegrounds, profession-gathering, profession-crafting, economy, reputation/attunement, recovery + world events) decomposed into A/O/T/A. |
| [`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md) | The DecisionEngine algorithm. Maps each layer to its DB and snapshot inputs; pseudocode for `ComposeObjectives`. |
| [`04_QUEST_CHAINS.md`](04_QUEST_CHAINS.md) | Quest-chain DAG schema. How `mangos.quest_template` columns become DAG edges. Faction / race / class / level gates. |
| [`05_ITEM_REQUIREMENTS.md`](05_ITEM_REQUIREMENTS.md) | Item-requirement DAG. Seal of Ascension, Hand of Ragnaros, Onyxia chain crystals, raid-keys, BG marks. Drives both Quest and Dungeon Activities. |
| [`06_WORKED_EXAMPLES.md`](06_WORKED_EXAMPLES.md) | End-to-end A→O→T→A traces for a quest, a dungeon, a profession loop, and a battleground. |
| [`07_PORTABILITY.md`](07_PORTABILITY.md) | Cross-game template. How the same model applies to FFXI / WAR / UO / EQ / EQ2 / PSO / Rag / SWG / D2. |

## Anchor docs (do not re-explain)

- [`docs/Spec/18_TERMINOLOGY.md`](../../Spec/18_TERMINOLOGY.md) — canonical glossary; one-paragraph definition of each layer + the worked UBRS example.
- [`docs/Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md) — `ActivityDefinition` record, OnDemand vs Autonomous flows, legality validation, the 7-step gating.
- [`docs/Spec/03_BOTRUNNER.md`](../../Spec/03_BOTRUNNER.md) — `IBotTask` contract (target Phase-1 async surface), task-family catalog, `ActionMessage` dispatch.
- [`docs/Spec/05_PROGRESSION.md`](../../Spec/05_PROGRESSION.md) — `RosterPlanner` / `ProgressionPlanner` / `ActivityScheduler`; the planner *that* uses AOTA.
- [`docs/leveling-guide/`](../../leveling-guide/) — reference data on WoW game mechanics; the input the DecisionEngine reads alongside the MaNGOS DB.
- [`docs/Plan/Activities/`](../../Plan/Activities/) — per-activity-family implementation slot tables; `00_INDEX.md` is the 86-row catalog board.

## Reading order

If you are landing on this tree fresh:

1. Read [`Spec/18_TERMINOLOGY.md`](../../Spec/18_TERMINOLOGY.md) (3 min) — the canonical glossary.
2. Read [`01_LAYERS.md`](01_LAYERS.md) — what each layer is and how it composes recursively.
3. Read [`02_GAME_LOOPS.md`](02_GAME_LOOPS.md) — how WoW's loops decompose.
4. Read [`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md) — the algorithm that drives the engine.
5. Skim [`06_WORKED_EXAMPLES.md`](06_WORKED_EXAMPLES.md) until one of the examples matches your task.
6. Reach for [`04_QUEST_CHAINS.md`](04_QUEST_CHAINS.md) / [`05_ITEM_REQUIREMENTS.md`](05_ITEM_REQUIREMENTS.md) when authoring per-zone quest catalogs (slot SQ.4) or attunement chains.
7. Read [`07_PORTABILITY.md`](07_PORTABILITY.md) when porting the model to a new game directory.
