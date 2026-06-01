# WWoW Autonomous — `B.composer` / S2.0 Plan (the activity-runtime unblocker)

> **Read this before implementing `B.composer`.** It is the single most
> load-bearing row: the runtime that turns an `ActivityDefinition`
> catalog row into a live Objective graph and decomposes Objectives into
> Tasks. **Today nothing does this at runtime** — tests drive Activities
> via an `AssignedActivity` string, and the catalog's `spec`/`coordinator`
> columns describe *data*, not a running composer. Until this lands, no
> Activity reaches R-Live through the real decision path, and
> `C.goalplanner` has nothing to dispatch into.
>
> This row **is** the existing phase slot
> [`Plan/03 S2.0`](../03_PHASE2_ONDEMAND_ENGINE.md) ("`IActivity` /
> `IObjective` runtime contracts"). The authoritative contract spec is
> [`../../Spec/19_AOTA_RUNTIME.md`](../../Spec/19_AOTA_RUNTIME.md) +
> [`../../architecture/aota/`](../../architecture/aota/) (especially
> `03_DYNAMIC_COMPOSITION.md`). **Read those first — this doc is the
> churn framing, not a re-derivation of the API.**
>
> Pairs with [`CHURN_TRACKER.md`](CHURN_TRACKER.md) `B.composer`.
> **Last updated:** 2026-05-28.

---

## 1. The four-layer model (the contract you are wiring)

`Activity → Objective → Task → Action` (`Spec/18_TERMINOLOGY.md`,
root + repo `CLAUDE.md`):

- **Activity** = a catalog row (`ActivityDefinition`) — a major event
  (a dungeon run, a zone quest chain, a farm).
- **Objective** = a high-level state change; **what crosses the wire**
  as `ObjectiveMessage` (StateManager → BotRunner). The composer's job is
  to produce the Objective sequence for an Activity.
- **Task** = an `IBotTask` behavior-tree node on the BotRunner's LIFO
  stack that orchestrates Actions with verification; `GoToTask` is the
  universal child. BotRunner decomposes an incoming `ObjectiveMessage`
  into Tasks via behavior tree + MaNGOS-DB knowledge lookups.
- **Action** = an atomic, **local** primitive (one memory read / bit
  write / opcode send / key press). Actions never cross the wire.

The composer lives on the **planning/Objective** boundary: it walks an
Activity into Objectives. BotRunner owns Objective→Task→Action (R17).

## 2. What exists vs. what's missing

| Asset | Where | State |
|---|---|---|
| `ActivityDefinition` (the row type) | `Exports/GameData.Core/Models/Activities/ActivityDefinition.cs` | exists — `Id`, `Family`, `LevelRange`, `EntryRequirements`, `TravelTarget`, `Rewards`, `TaskFamily`, etc. |
| `IActivityCatalog` (read-only lookup) | `Services/WoWStateManager/Activities/ActivityCatalog.cs` + 5 `ActivityCatalogRows.Shard*.cs` | exists — 86 rows; `TryGetById`. Drift test `Tests/BotRunner.Tests/Activities/CatalogMarkdownDriftTests.cs`. |
| `IActivityComposer.Compose(...)` (the RUNTIME) | spec'd in `Spec/19_AOTA_RUNTIME.md §9`; `Plan/03 S2.0` slot | **absent** — this row builds it. |
| `IActivity` / `IObjective` runtime contracts | `Spec/19` | **absent** — build per the spec. |
| Test entry today | `AssignedActivity` string + `StageBotRunner*Async` helpers (`LiveBotFixture`) | exists — the *interim* path until `IActivity.Start(...)` lands (see the `CLAUDE.md` test-isolation rule). |
| Dynamic-composition rules (recursive prereqs, quest-chain DAG, item-requirement DAG) | `docs/architecture/aota/03_DYNAMIC_COMPOSITION.md`, `05_ITEM_REQUIREMENTS.md` | spec — the algorithm the composer implements. |

## 3. The deliverable

Per `Spec/19` (authoritative), implement:

1. **`IActivity` / `IObjective` runtime contracts** — the runtime
   counterparts of the catalog `ActivityDefinition`. An `IActivity`
   exposes its ordered/eligible `IObjective`s and prerequisites; an
   `IObjective` maps to an `ObjectiveMessage` on the wire.
2. **`IActivityComposer.Compose(ActivityDefinition, CharacterProgression/snapshot)`**
   — walks a catalog row into the Objective graph, resolving:
   - entry requirements + travel (a `TravelTo` Objective to the
     `TravelTarget` first when out of range);
   - the family-specific Objective sequence (e.g. a dungeon → reach
     portal → per-boss pull Objectives; a zone quest → accept/turn-in
     chain) by consulting the MaNGOS DB (read-only) + the behavior tree;
   - **recursive prerequisite composition** per `aota/03` (an Activity
     whose prereq is unmet composes the prereq Activity first).
3. **`IActivity.Start(...)`** as the legal test/runtime entry point that
   **replaces** the `AssignedActivity` string path (R18 — once landed,
   migrate the interim string path; the grandfathered Category-A tests
   are rewritten in `Plan/12`, not here).
4. Wire the composer so `C.goalplanner`'s selected Objective and the
   `OnDemand` path (`Plan/03`) both flow through it.

Honor:
- **R17** — `IBotTask` impls stay in `Exports/BotRunner/`; the composer
  produces Objectives, BotRunner decomposes them. The composer is the
  Objective-planning boundary (StateManager-side composition is allowed;
  Task construction is not).
- **R18** — no parallel old/new; migrate the `AssignedActivity` entry as
  the new path lands.
- **Test-isolation rule** — new `LiveValidation` tests drive the Activity
  (`IActivity.Start`) and assert the snapshot; never construct a raw
  `ObjectiveMessage`.

## 4. Suggested sub-steps (5 iters — split into tracker sub-rows if it helps)

1. **Contracts + unit composer for ONE family.** Land `IActivity` /
   `IObjective` + `IActivityComposer` and compose ONE simple Activity
   (recommend a gathering or single-zone-quest row — the simplest
   Objective sequence) to its expected Objective list. Accept:
   `UNIT:ActivityComposerTests` (row → expected Objective sequence).
2. **Travel + entry-requirement resolution.** Compose the `TravelTo`
   Objective + entry gating (level/faction/item) from the row's
   `EntryRequirements` + the snapshot.
3. **Recursive prerequisite composition** per `aota/03` (prereq Activity
   composed first when unmet). Accept: unit fixture with a 2-deep prereq
   chain.
4. **`IActivity.Start(...)` runtime entry + migrate the `AssignedActivity`
   string path** (R18). Wire into the `OnDemand` path + the
   `CharacterStateSocketListener` Objective dispatch.
5. **Live proof.** One Activity driven end-to-end via `IActivity.Start`
   reaches completion against the live stack, trace satisfies the
   dynamic-progressive invariant. Accept:
   `LIVE:` the chosen Activity completes; `roster_distance_delta ≤ 0`.

## 5. Why this is the unblocker (and `C.goalplanner` is the other half)

- **Composer (this row):** turns a *chosen* Activity into something the
  bot can *run*. Every Phase B family Activity needs it to reach R-Live
  through the real path (not the interim string).
- **GoalPlanner (`C.goalplanner`):** turns *live progression state* into
  the *choice* of which Activity to run, by consuming
  `decision-engine/`. See
  [`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md).

Together: GoalPlanner picks the Activity → Composer walks it into
Objectives → BotRunner decomposes Objectives into Tasks/Actions →
the bot does it. That closed loop is "autonomous." Build the composer
first (Phase B), the planner second (Phase C).
