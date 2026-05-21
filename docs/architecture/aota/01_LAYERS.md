# 01 — Layer definitions and recursive composition

> Reading prerequisite: [`Spec/18_TERMINOLOGY.md`](../../Spec/18_TERMINOLOGY.md).
> This file goes deeper on each layer and on the **recursive composition rules** that the canonical glossary leaves implicit.

## 1. Action — the reusable player-capability primitive

### What it is

An **Action** is a reusable code primitive (a method or variable accessor)
inside the BotRunner that mirrors a single thing a human player can do:
read the loot window, check a bag slot, press a hotkey, click a world
coordinate, read a unit field, cast a spell by id, accept a loot item,
turn in a quest reward.

**Actions are pure local code.** No Action is ever a wire message. The
protobuf `ObjectiveMessage` type (formerly named `ActionMessage` — a
misnomer cleaned up in the 2026-05-21 rename) carries **Objective-level**
state-change requests, not Actions. The BotRunner receives an
`ObjectiveMessage`, decomposes it into Tasks via a behavior tree + game
database knowledge lookups, and the Tasks then invoke Action methods to
read and alter game state.

### Granularity — Action vs Task

> **Critical distinction.** Compound things like `MoveToCoord`,
> `InviteToParty`, `WalkToWorld`, `CastSpell`, `LootCorpse` are **Tasks**
> — each composes many Actions over many ticks with verification. An
> **Action** is the SMALLEST possible primitive: one memory read, one
> bit flip, one packet opcode send, one key press. The `IObjectManager`
> methods that wrap multi-opcode sequences (`MoveToAsync`,
> `CastSpellAsync`, `LootTargetAsync`) are **Task-level convenience
> wrappers**, not Actions; the truly-atomic Actions are the primitives
> they invoke internally.

### Where atomic Actions live

- **Read primitives (single memory or packet read):**
  `ReadMemoryDword(addr)`, `ReadUnitField(guid, fieldOffset)`,
  `ReadOwnPosition()`, `ReadGameTime()`, `ReadTargetGuid()`,
  `ReadInventorySlot(bag, slot)`, `ReadHotbarSpellId(slot)`,
  `ReadQuestLogSlotState(idx)`.
- **Write primitives (single memory bit or single opcode):**
  `WriteMovementBit(forward, true)`, `WriteUnitField(guid, off, val)`,
  `SendOpcode(CMSG_CAST_SPELL, payload)`,
  `SendOpcode(MSG_MOVE_HEARTBEAT, payload)`,
  `SendOpcode(CMSG_LOOT, target_guid)`,
  `PressKey('1')` / `ReleaseKey('1')`,
  `WriteMouseClick(screenX, screenY)`.
- **FG vs BG split:** FG Actions write to WoW.exe process memory + send
  packets via inline hooks; BG Actions emit equivalent opcodes through
  WoWSharpClient and write to an in-process ObjectManager mirror. The
  Action signatures are identical, the implementations differ — that's
  the FG/BG parity contract.
- **What is NOT an Action.** `MoveToCoord(coord)` is a Task: it loops
  `ReadOwnPosition` + `WriteMovementBit` + `SendOpcode(MSG_MOVE_*)` +
  stuck-detection over many ticks until the bot arrives. `CastSpell(id,
  target)` is a Task: it checks the GCD via `ReadGameTime`, sets the
  target via `WriteUnitField`, sends `CMSG_CAST_SPELL`, then verifies
  via `ReadSpellCastResultPacket`. `InviteToParty(player)` is a Task:
  it sends `CMSG_GROUP_INVITE`, then polls `ReadGroupMembership` until
  the invite is accepted or times out.

### The closed wire-level Action-kind roster

The proto's `ObjectiveType` enum (~85 values: `WAIT`, `GOTO`,
`INTERACT_WITH`, `CAST_SPELL`, `LOOT_CORPSE`, `ACCEPT_QUEST`, etc.) is
the **roster of Action kinds the StateManager can request via an
`ObjectiveMessage`** — not the Action itself. The enum is closed-set
because adding one is protobuf-cost:

1. Adds the enum value in `communication.proto`.
2. Adds the mirrored value in `CharacterAction.cs`.
3. Adds mapping in `BotRunnerService.ActionMapping.cs`.
4. Adds sequence builder in `BotRunnerService.ActionDispatch.cs` (FG + BG paths).
5. Regenerates protobuf for every client (rule R10).

So adding wire-dispatchable Action kinds is **deliberately expensive.**
The closed-set discipline forces composition at higher layers instead —
prefer to add a new Task that composes existing Actions over adding a
new `ObjectiveType`.

### Action composition vs Task wrappers

> The original framing of "Actions can use other Actions via
> ObjectManager helpers" was imprecise. After the atomicity clarification
> (2026-05-21+), `IObjectManager` multi-opcode helpers are recognized as
> **Task-level wrappers**, not Actions.

`IObjectManager.TurnInQuestAsync(npc, questId, rewardIndex)` is a
**Task-level convenience method** that internally invokes a sequence
of atomic Actions and verifies their effects before returning:

```
TurnInQuestTask (or IObjectManager.TurnInQuestAsync wrapper)
        │
        ▼ executes atomic Actions:
        ├─ SendOpcode(CMSG_QUESTGIVER_COMPLETE_QUEST, npcGuid|questId)
        ├─ ReadQuestRewardSlots()                  (memory read)
        ├─ SendOpcode(CMSG_QUESTGIVER_CHOOSE_REWARD, rewardIndex)
        ├─ ReadQuestLogSlot(questId, state)        (verify)
        └─ if state == COMPLETE: return success
```

This is the **"ObjectManager wrapper composes atomic Actions"** pattern:
`IObjectManager` methods like `TurnInQuestAsync`, `LootTargetAsync`,
`BuyItemAsync`, `MoveToAsync`, `CastSpellAsync` are convergence points
for multi-Action sequences — they live in the Task layer (or just below
it as Task-helper utilities), NOT in the Action layer.

There are two ways the wrapper gets invoked:

1. **External `ObjectiveMessage` dispatch.** StateManager sends
   `ObjectiveMessage{objective_type=TurnInQuest, …}`; BotRunner's
   dispatcher resolves it to a `TurnInQuestTask` (which may internally
   call `IObjectManager.TurnInQuestAsync` as its execution body). Used
   when StateManager wants a single observable wire-level Objective.
2. **Internal Task call from a parent Task.** A higher-level Task like
   `KillObjectiveTask` directly calls `IObjectManager.TurnInQuestAsync`
   when it needs to turn in a kill quest — no wire round-trip. Used when
   a Task is orchestrating a multi-step state change locally.

**Rule.** Tasks should reach for `IObjectManager` helpers (or smaller
Tasks) directly. Add new `ObjectiveType` enum values only when
StateManager needs to dispatch a brand-new wire-level intent that no
current Task already covers — and the new Action-level primitives those
Tasks invoke should be additions to the BotRunner's atomic-Action set
(memory reads, opcode sends, bit writes), not new wire enum values.

## 2. Task — the behavior-tree node

### What it is

A `Task` is an `IBotTask` instance on the bot's LIFO **task stack**.
The stack is the only place behavior-tree state lives between ticks.

### Where it lives

- **Contract:** `Exports/BotRunner/Interfaces/IBotTask.cs`. Target Phase 1 surface is the async four-method contract: `TickAsync` / `OnPushedAsync` / `OnPoppedAsync` / `OnChildFailedAsync` + `Name` + `Status`. See [`Spec/03_BOTRUNNER.md#target-contract`](../../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10).
- **Base class:** `Exports/BotRunner/Tasks/BotTask.cs` — shim that bridges legacy `void Update()` bodies to the async surface.
- **Concrete tasks:** `Exports/BotRunner/Tasks/` (cross-family) and `BotProfiles/<ClassSpec>/Tasks/` (per-class combat tasks).
- **Family catalog:** [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families).

### Execution model

Per [`Spec/03_BOTRUNNER.md#task-stack`](../../Spec/03_BOTRUNNER.md#task-stack):

```
each tick:
    1. peek top of stack
    2. if Complete  → pop, loop
    3. if Failed    → pop, emit TaskFailedEvent, escalate via parent.OnChildFailed
    4. otherwise    → call task.TickAsync(context)
```

A Task may **push child tasks** during a tick. That child becomes the
new top of stack and runs to completion before the parent's next tick
sees the result.

### Recursive composition — Tasks pushing Tasks

> "Tasks use other Tasks (most tasks need to get within interact distance,
> so a lot use GoToTask. LongTravelTask will be a series of GoToTasks,
> InteractWithTasks, etc.)" — user's framing.

The push-child mechanic is the **only** way Tasks compose. There are
no implicit task-ordering primitives ("sequence", "parallel") — every
ordering is realised by the parent task pushing the next child when
the current one completes. This makes the behavior tree fully visible
on the stack and fully replayable from snapshots.

#### Canonical compositions

| Parent Task | Pushes (in order) | Why |
|---|---|---|
| `TravelToTask(coord)` | `GoToTask(walk-leg)` → `BoardTransportTask(zeppelin)` → `WaitForLandingTask` → `GoToTask(deck-anchor)` | Travel within a map decomposes into walk-and-board legs. |
| `LongTravelTask(continent-target)` | `UseHearthstoneTask` *or* `TakeFlightPathTask(closest-fp → dest-fp)` *or* `MageTeleportTask(city)` *or* `BoardTransportTask(zeppelin)`; then `TravelToTask(local-coord)` | Cross-map routing is "pick the cheapest gateway, then short-range travel from gateway to target." `CrossMapRouter.cs` chooses; this Task pushes the chosen leg(s). |
| `AcceptQuestTask(questId)` | `GoToTask(quest-giver-coord)` → `InteractWithNpcTask(questGiverGuid)` → emits `ObjectiveType.AcceptQuest` | Every quest pickup is "get within interact range, open quest frame, click accept." |
| `KillObjectiveTask(questId, creatureEntry, requiredCount)` | for each hotspot: `GoToTask(hotspot)` → `PullStrategyTask(matching-unit)` → `PvERotationTask` → `LootCorpseTask`; loops until counter reaches required | Every kill objective is "walk hotspot, pull, kill, loot, repeat." |
| `CollectObjectiveTask(questId, itemId, requiredCount, sourceCreatureId?, sourceGameObjectId?)` | for each hotspot: `GoToTask(hotspot)` → either `PullStrategyTask` + `LootCorpseTask` (creature source) or `UseGameObjectTask` (container source) | Mirror of KillObjective but the gate is item count in inventory, not unit death count. |
| `DungeoneeringTask` | `TravelToTask(instance-portal)` → `InteractWithGameObjectTask(portal)` → per-room: `PullStrategyTask` → `LootCorpseTask`; per-named-boss: `BossEncounterTask(plan)`; on completion: `TravelToTask(exit)` | A dungeon clear is a chained sequence of pulls, encounters, and loots. |
| `BattlegroundQueueTask(bgType)` | `GoToTask(battlemaster)` → `InteractWithNpcTask` → emit `ObjectiveType.JoinBattlegroundQueue` → wait → on invite emit `ObjectiveType.AcceptBattlegroundInvite` → wait → on entry pop | Queueing for any BG is the same 5-step dance; only the battlemaster NPC differs. |
| `GatheringRouteTask(nodeType, waypoints)` | for each waypoint: `GoToTask(waypoint)` → on node detection: `GatherNodeTask(nodeGuid)`; loops the route while skill < cap | Mining/Herb/Skinning routes are identical at this layer; the differentiator is which entry-set the scanner watches for. |
| `CraftRecipeTask(recipeId, targetCount)` | `MaterialSourcingTask(missing-reagents)` (if needed) → `LearnRecipeTask(recipeId)` (if not yet known) → `TrainerVisitTask(profession)` (if skill capped) → `CastCraftSpellTask(spellId)` × N | Crafting is "ensure mats, ensure recipe, ensure skill headroom, then cast N times." |

#### The `GoToTask` is the universal child

Across every family above, **the most-pushed child task is `GoToTask`.**
Every interaction in MMO play is gated on physical proximity: quest
NPCs, vendors, mailboxes, mailbox-adjacent banks, gathering nodes,
mob hotspots, boss positions, BG capture points. `GoToTask` itself
decomposes — internally it pushes `PathfindingClient.RequestRoute`
followed by a stream of `ObjectiveType.StartMovement` /
`ObjectiveType.StopMovement` actions, driven by `MovementController`
parity guarantees.

### Failure escalation

`OnChildFailedAsync` returns `bool`:

- `true` → parent absorbs the failure and keeps running. Typical when
  the parent has retries left or an alternative branch.
- `false` → parent escalates by failing itself; its parent's
  `OnChildFailedAsync` then fires. The default `BotTask` base returns
  `false`.

Examples:

- `KillObjectiveTask` whose `PullStrategyTask` fails *absorbs* (push next hotspot).
- `KillObjectiveTask` whose 3rd consecutive `PullStrategyTask` fails *escalates* (drop kill objective, escalate to Objective layer).
- `DungeoneeringTask` whose `BossEncounterTask` fails three times *escalates* — the Objective ("clear boss N") fails and the orchestrating Activity decides whether to wipe-retry or abandon.

## 3. Objective — the next-blocker slice

### What it is

> "An Objective is the slice of state change that can be done before
> other state changes can be implemented (done any number of tasks, but
> usually short)." — user's framing.

An `Objective` is the **smallest unit of forward progress that, once
complete, unblocks a meaningfully different next state.** In code shape
it is a sequence of Tasks; in semantic shape it is one named arrow in
the Activity's state machine.

### What "unblocks a meaningfully different next state" means in practice

- A *Quest* Objective ends when the bot's snapshot reflects the quest's
  `QuestState` flipping to `Complete` (counter met, item collected,
  escort delivered), *or* a turn-in writing the next quest's slot into
  the quest log.
- A *Dungeon* Objective ends when a specific boss is dead or a specific
  door is opened or a specific quest item is in inventory.
- A *Profession* Objective ends when the skill ticks up to the next
  rank-band threshold (75 → 150 → 225 → 300) or a specialty quest is
  turn-in-complete.
- A *Travel* Objective ends when the bot's `Position` is within a
  named-location radius.
- A *BG* Objective ends when the team's flag-cap count increments, a
  node is held for N seconds, or the BG instance map transitions.

### Where it lives today

Per [`Spec/18_TERMINOLOGY.md`](../../Spec/18_TERMINOLOGY.md): the
runtime `IObjective` interface is **Phase-2 work**. Today's snapshot
only carries `WoWActivitySnapshot.travel_objective` and
`progression_status.current_objective` (free-form string). Phase-2
slot **S2.0** adds the `IObjective` runtime contract, an
`ObjectiveType` enum on the proto, and a `current_objective_id` /
`current_objective_type` projection on the snapshot.

The placeholder runtime shape (modelled on D2Bot's
`BotObjectiveContract`):

```csharp
public interface IObjective
{
    string Id { get; }                          // "ubrs.reach-flame-crest"
    ObjectiveType Type { get; }                 // Travel | Kill | Collect | Interact | Encounter | Turnin | Queue | Cap | Craft | …
    ObjectiveEndState EndState { get; }         // predicate over snapshot
    IReadOnlyList<ObjectiveGate> Gates { get; } // predicates on snapshot that must be true to *start*
    IBotTask BuildHeadTask(BotTaskContext ctx); // first task to push
    bool CheckCompletion(WoWActivitySnapshot s); // are we done?
}
```

### Recursive composition — Objectives chaining Objectives

Objectives compose two ways:

**(a) Sequence (Activity-level).** An Activity's Objectives form an
ordered sequence (with optional parallel branches). The DecisionEngine
walks them in dependency order: when Objective N's `CheckCompletion`
returns `true`, the engine reads Objective N+1's `BuildHeadTask` and
pushes the resulting Task onto the stack.

```
Activity dungeon.ubrs:
    Objective[0] reach-flame-crest        (Travel)        ──►  TravelToTask(FlameCrest)
    Objective[1] enter-instance-portal    (Interact)      ──►  InteractWithGameObjectTask(portal)
    Objective[2] clear-trash-to-rend      (Encounter+Loot)──►  DungeoneeringTask(route-to-rend)
    Objective[3] kill-rend-blackhand      (Encounter)     ──►  BossEncounterTask(rend-plan)
    Objective[4] loot-rend                (Collect)       ──►  LootCorpseTask(rend)
    Objective[5] exit-instance            (Travel)        ──►  TravelToTask(instance-exit)
```

**(b) Reference (DAG-level).** An Objective in *another* Activity may
be a precondition of a downstream Objective in *this* Activity. The
**unlock graph** ([docs/leveling-guide/decision-engine/unlock-graph.md](../../leveling-guide/decision-engine/unlock-graph.md))
encodes these cross-Activity edges: `ubrs.boss.drakkisath` → `attune.molten-core`
means the MC-attune Activity's first Objective (the Lothos-Riftwaker
turn-in) has a precondition Objective that lives inside the UBRS
Activity. The Engine resolves this by either *requiring* the parent
Activity to have completed (snapshot `QuestsCompleted` test) or
*synthesizing* an upstream Activity assignment when the bot's roster
plan calls for it.

### Why this layer exists

Without an explicit Objective layer, the Activity catalog would either
(a) explode into hundreds of micro-Activities (one per quest step), or
(b) collapse into a few mega-Activities that no test can localize
("UBRS run failed somewhere"). The Objective layer is the natural
**unit of test assertion** and **unit of failure reporting**:

```
[ERROR] dungeon.ubrs / objective[3] kill-rend-blackhand failed after 3 wipes
```

is debuggable; `[ERROR] UBRS failed` is not.

## 4. Activity — the assigned goal

### What it is

An `Activity` is a **multi-minute end-state goal that the bot has been
assigned, one at a time, by either the operator (OnDemand) or the
ProgressionPlanner (autonomous).** It is the top-level unit of
scheduling, lockout, group-formation, gear-loadout, and reward-policy.

### Where it lives

- **Catalog row (data):** `ActivityDefinition` records compiled into `Services/WoWStateManager/Activities/ActivityCatalog.cs` (86 rows). See [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md).
- **Runtime instance (Phase 2):** `IActivity` interface — Phase-2 slot S2.0 per [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../../Plan/03_PHASE2_ONDEMAND_ENGINE.md).
- **Today's runtime:** `AssignedActivity` string (e.g. `"Fishing[Ratchet]"`) parsed by `ActivityResolver` into a top-level `IBotTask`. The Activity layer collapses into the Task layer until S2.0 lands.

### The data-only contract today

```csharp
public sealed record ActivityDefinition
{
    public string Id { get; }                // "dungeon.ubrs"
    public ActivityFamily Family { get; }    // Dungeon | Raid | Bg | Quest | …
    public LevelRange LevelRange { get; }    // 58-60
    public FactionPolicy FactionPolicy { get; }
    public RoleTemplate RoleTemplate { get; }
    public EntryRequirements EntryRequirements { get; }  // items, quests, reps, attunes, server-caps
    public TravelTarget TravelTarget { get; }
    public TimeSpan ExpectedDuration { get; }
    public HumanJoinPolicy HumanJoinPolicy { get; }
    public BotSelectionPolicy BotSelectionPolicy { get; }
    public IReadOnlyList<RewardDefinition> Rewards { get; }
    public string TaskFamily { get; }        // BotRunner task-family head
}
```

### Recursive composition — Activities referencing Activities

Activities relate to other Activities via:

**(a) Entry preconditions.** `EntryRequirements.RequiredQuests` lists
completed quest IDs needed. Those quests live in other Activities
(typically attunement Activities). Example: `raid.mc` lists
`RequiredQuests=[attune-mc-final-turnin]`; that quest is the final step
of Activity `attune.mc`.

**(b) Item gates.** `EntryRequirements.RequiredItems` lists items
that themselves originate from other Activities (Seal of Ascension
from a chain of UBRS / LBRS / Vael's room).

**(c) Reputation gates.** `EntryRequirements.RequiredReputations`
references factions whose only practical farm path is a specific
catalog Activity (Naxx attune → `rep.argent-dawn` reputation Activity).

**(d) Soft sequencing (Phase 4 ProgressionPlanner).** The planner
treats certain Activity outcomes as *prerequisites for picking* a
later Activity, even when no hard `EntryRequirements` row exists.
Example: "complete `dungeon.uldaman` before assigning
`quest.zone.tanaris`" is a planner heuristic, not a hard
`RequiredQuests`.

### One Activity at a time per bot

Per [`Spec/04_ACTIVITIES.md`](../../Spec/04_ACTIVITIES.md): a bot has
exactly one `AssignedActivity` at any moment. Group formation is
"five level/role-compatible bots independently assigned to the same
group Activity converge through the matching Coordinator." There is
no scheduler holding leases (per the 2026-05-12 design refinement) —
bots are always on and the Coordinator just reacts to snapshot
quorums.

## 5. The full picture — composition rules summarized

| Rule | Statement |
|---|---|
| **R-A1** | An Action is local code; it may internally invoke other Actions via `IObjectManager` helpers. Actions never cross the wire — what crosses is an `ObjectiveMessage` that the BotRunner decomposes into Tasks → Actions. |
| **R-A2** | New `ObjectiveType` enum values (the wire-level roster of Action *kinds* StateManager can request) are protobuf-cost; prefer to express new behavior as a new *Task* that composes existing Actions. |
| **R-T1** | A Task composes other Tasks via the **push** primitive; there is no implicit sequence or parallel. |
| **R-T2** | A Task may not push a Task whose family is not in the family catalog (see `Spec/03_BOTRUNNER.md#catalog-of-task-families`). |
| **R-T3** | Every Task ends as `Complete` or `Failed`; `OnChildFailedAsync` returns `true` to absorb or `false` to escalate. |
| **R-O1** | An Objective is the smallest unit of forward state-change. It maps 1:1 to a snapshot predicate that is `false` when started and `true` when finished. |
| **R-O2** | Objectives form an ordered (DAG-ordered) sequence inside an Activity. Cross-Activity edges live in the **unlock graph**. |
| **R-O3** | An Objective may only push Tasks from families the activity catalog already declares for the Activity. Adding a new family requires a spec PR. |
| **R-AC1** | An Activity is one assigned-at-a-time goal. Catalog row drives legality + selection + reward policy. Runtime `IActivity` lands in Phase 2 (slot S2.0). |
| **R-AC2** | Activities reference other Activities through `EntryRequirements` + the unlock graph. The DecisionEngine resolves these into a planned sequence. |
| **R-W1** | Only **`ObjectiveMessage`** (an Objective-level state-change request) crosses the StateManager↔BotRunner wire. BotRunner decomposes that locally into Tasks → Actions. Activity / Task identity is *projected* onto the snapshot for observability (`current_activity_id`, top-of-stack task name); Action invocation is never wire-visible. |

## 6. Snapshot projection (Phase 2)

`WoWActivitySnapshot` carries one identity field per layer for tests
and the UI:

| Layer | Snapshot field | Today | Phase 2 |
|---|---|---|---|
| Activity | `current_activity_id` | (assigned-activity string only) | catalog id string |
| Objective | `current_objective_id`, `current_objective_type` | (free-form `progression_status.current_objective` string) | id + `ObjectiveType` enum |
| Task | top-of-stack `currentTaskName` | exists | exists; renamed `current_task_name` |
| Action | most-recent `RecentCommandAcks[]` | exists | exists |

Tests assert on the snapshot at any layer — never on the bot's
internal `IBotTask` instances directly. This is the
"Tests drive Activities, not Actions" rule from [`CLAUDE.md`](../../../CLAUDE.md#test-isolation-rules--critical).
