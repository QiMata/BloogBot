# Spec 03 — BotRunner

## Identity

BotRunner is the shared library that drives a single bot's behavior. The
same `BotRunner` core ships in both FG (injected) and BG (headless)
processes. The execution mode swaps the `IObjectManager` implementation
(`WoWSharpObjectManager` for BG, the FG memory-reader for FG), not the
behavior trees.

## The IBotRunner contract

```csharp
public interface IBotRunner
{
    string AccountName { get; }
    BotExecutionMode Mode { get; }         // Foreground | Background
    IObjectManager ObjectManager { get; }
    WoWActivitySnapshot CurrentSnapshot { get; }

    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);

    Task DispatchAsync(ObjectiveMessage action, CancellationToken ct);
    Task<bool> WaitForLoadoutCompleteAsync(TimeSpan timeout, CancellationToken ct);
    Task<WoWActivitySnapshot> CaptureSnapshotAsync(CancellationToken ct);

    event EventHandler<SnapshotEmittedEvent> SnapshotEmitted;
    event EventHandler<TaskCompletedEvent> TaskCompleted;
    event EventHandler<TaskFailedEvent> TaskFailed;
}
```

`BotRunnerService` (`Exports/BotRunner/BotRunnerService.cs`) is the
production implementation. Tests inject mocks via `IBotRunner`.

## Task stack

BotRunner maintains an LIFO stack of `IBotTask` instances. Each tick:

1. Peek the top task.
2. If `task.IsComplete`, pop it; loop to step 1.
3. If `task.IsFailed`, pop it, emit `TaskFailedEvent`, escalate per
   parent's `OnChildFailed` policy.
4. Otherwise, call `task.Tick(context)`.

A task may push child tasks (e.g. `DungeoneeringTask` pushing a
`TravelTask` which pushes a `BoardTransportTask`). This is the only
way to compose work — no implicit task ordering.

## IBotTask interface

### Target contract (Phase 1 target — to be implemented under slot S1.0)

```csharp
public interface IBotTask
{
    string Name { get; }                     // "TravelTask:Crossroads->UC"
    BotTaskStatus Status { get; }            // Running | Complete | Failed
    bool IsComplete => Status == BotTaskStatus.Complete;
    bool IsFailed => Status == BotTaskStatus.Failed;

    Task TickAsync(BotTaskContext context, CancellationToken ct);
    Task OnPushedAsync(BotTaskContext context, CancellationToken ct);
    Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal);
    Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason);
}
```

`BotTaskContext` exposes the IObjectManager, the pathfinding client,
the chat sink, the metrics sink, and the cancellation token. Tasks
**must not** reach outside the context to global state.

### Current shipped state (2026-05-12)

The interface that exists today at
`Exports/BotRunner/Interfaces/IBotTask.cs` is a single synchronous
method:

```csharp
public interface IBotTask
{
    void Update();
}
```

Lifecycle today is handled by a `BotTask` abstract base class with a
`PopTask(string reason)` method and per-task private state machines.
The target async contract above is **Phase 1 work** scheduled under
slot **S1.0 — IBotTask contract migration** in
[`../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md).
S1.0 is the first Phase 1 slot a worker claims; every family slot
(S1.4..S1.13) codes against the new interface.

For S0.8 spec-hardening purposes, family files in
[`../Plan/Activities/`](../Plan/Activities/) document **both** the
current shipped surface and the target surface per task.

## Catalog of task families

Each family is a directory under `Exports/BotRunner/Tasks/`. Family heads
listed below; per-task detail in [`Plan/Activities/`](../Plan/Activities/).

| Family | Representative tasks | Activity spec |
|---|---|---|
| Travel | `TravelTask`, `GoToTask`, `MountAndGoToTask`, `TakeFlightPathTask`, `BoardTransportTask`, `ElevatorRideTask`, `UseHearthstoneTask`, `MageTeleportTask` | [`Plan/Activities/travel.md`](../Plan/Activities/travel.md) |
| Combat | `PullTargetTask`, `PvERotationTask`, `PvPRotationTask`, `RestTask`, `HealTask`, `BuffTask`, `SummonPetTask`, `ConjureItemsTask` | [`Plan/Activities/combat.md`](../Plan/Activities/combat.md) |
| Questing | `AcceptQuestTask`, `KillObjectiveTask`, `CollectObjectiveTask`, `EscortObjectiveTask`, `TurnInQuestTask`, `AbandonQuestTask` | [`Plan/Activities/quests.md`](../Plan/Activities/quests.md) |
| Dungeoneering | `DungeoneeringTask`, `PullStrategyTask`, `BossEncounterTask`, `LootCorpseTask` (group-loot path) | [`Plan/Activities/dungeons.md`](../Plan/Activities/dungeons.md) |
| Raid | `RaidPositioningTask`, `RaidEncounterTask`, `MasterLootTask`, `ReadyCheckTask` | [`Plan/Activities/raids.md`](../Plan/Activities/raids.md) |
| BG | `BattlegroundQueueTask`, `BgObjectiveTask` (flag, node, GY, tower) | [`Plan/Activities/battlegrounds.md`](../Plan/Activities/battlegrounds.md) |
| Gathering | `GatheringRouteTask`, `GatherNodeTask`, `FishingTask`, `SkinningTask` | [`Plan/Activities/professions-gathering.md`](../Plan/Activities/professions-gathering.md) |
| Crafting | `CraftRecipeTask`, `MaterialSourcingTask`, `LearnRecipeTask`, `TrainerVisitTask` | [`Plan/Activities/professions-crafting.md`](../Plan/Activities/professions-crafting.md) |
| Economy | `AuctionHousePostTask`, `AuctionHouseBuyTask`, `BankDepositTask`, `BankWithdrawTask`, `MailSendTask`, `MailRetrieveTask`, `VendorSellTask`, `VendorBuyTask`, `RepairAllTask` | [`Plan/Activities/economy.md`](../Plan/Activities/economy.md) |
| Social | `GroupInviteTask`, `GroupAcceptTask`, `GroupLeaveTask`, `TradeTask`, `WhisperTask`, `ChannelJoinTask`, `GuildInviteTask` | [`Plan/Activities/social.md`](../Plan/Activities/social.md) |
| Recovery | `ReleaseCorpseTask`, `RetrieveCorpseTask`, `StuckRecoveryTask`, `ReconnectTask`, `SpiritHealerTask` | [`Plan/Activities/recovery.md`](../Plan/Activities/recovery.md) |
| Equipment | `EquipItemTask`, `UnequipItemTask`, `LoadoutTask`, `GearGapTask` | [`Plan/Activities/economy.md`](../Plan/Activities/economy.md) (gear chase loops live here) |
| World-event | `StvFishingExtravaganzaTask`, `WorldBossEngagementTask` | [`Plan/Activities/world-events.md`](../Plan/Activities/world-events.md) |
| Loadout | `LoadoutTask` (orchestrator), `LearnSpellsTask`, `LearnTalentsTask`, `SetSkillTask` | covered under Equipment + Combat |

The full enumeration of all currently-implemented tasks plus required
new tasks lives in `Plan/Activities/` per family. Each family doc lists
the task slots that an autonomous agent claims.

## FG/BG parity rule

For every task, both the FG path (memory + Lua) and the BG path
(protocol packets) must be implementable and tested. When the BG path
cannot be built (FG-only frame dependency that has no packet
equivalent), the task is FG-only and the catalog activity must not
schedule a BG-only bot for it.

Current FG-only gaps (must be closed before living-server scale):

- `BuybackItem` (requires MerchantFrame); use `RepairAll` workaround.
- `Craft` (CraftFrame); BG must use `CraftAgent` packet path (open work).
- Trade actions (`OfferTrade`, `OfferGold`, `OfferItem`, `AcceptTrade`,
  `EnchantTrade`, `LockpickTrade`) — all 6 lack null guards on BG; will
  NullRef. **High-priority hardening task.**
- `SelectTaxiNode` (TaxiFrame).
- `TrainSkill` legacy path (TrainerFrame).
- `TrainTalent` (TalentFrame).
- `SelectGossip` legacy (GossipFrame).

All listed gaps have slots in [`Plan/04_PHASE3_BOT_LEASE_SCHEDULER.md`](../Plan/04_PHASE3_BOT_LEASE_SCHEDULER.md)
as pre-requisites for full activity coverage.

## ObjectiveMessage dispatch

`StateManager` ships an `ObjectiveMessage` (proto) to a bot. BotRunner:

1. Maps `ObjectiveMessage.ObjectiveType` → `CharacterAction` enum
   (`BotRunnerService.ActionMapping.cs`).
2. Builds a `Xas.FluentBehaviourTree` sequence for the action
   (`BotRunnerService.ActionDispatch.cs`).
3. Sequence pushes one or more `IBotTask` instances on the stack.
4. Returns ACK to StateManager once the action has been accepted and
   queued; the eventual completion is communicated via the next
   snapshot.

Adding a new action type:

1. Add enum value in `GameData.Core/Enums/CharacterAction.cs`.
2. Add proto value in `communication.proto`; regenerate.
3. Add mapping in `BotRunnerService.ActionMapping.cs`.
4. Add sequence builder in `BotRunnerService.ActionDispatch.cs` (FG +
   BG paths).
5. Implement task if needed.
6. Write a `Tests/BotRunner.Tests/LiveValidation/...Tests.cs` test that
   dispatches the action via StateManager and asserts on the resulting
   snapshot.

## Activity resolver

`Exports/BotRunner/Activities/ActivityResolver.cs` parses
`AssignedActivity` strings (e.g. `"Fishing[Ratchet]"`,
`"Dungeon[WailingCaverns]"`, `"Battleground[WSG]"`) and starts the
corresponding task family. **The string format must match the catalog
identifiers in [`Spec/04_ACTIVITIES.md`](04_ACTIVITIES.md).** Drift
between resolver and catalog is a bug; tests assert both directions.

`ActivityResolver` today returns an `IBotTask` directly — it skips the
Activity AND Objective layers defined in
[`Spec/18_TERMINOLOGY.md`](18_TERMINOLOGY.md). The runtime `IActivity` /
`IObjective` contracts (modeled on D2Bot's
`D2Orchestrator/Orchestration/Activities/IActivity.cs`) are Phase-2
work; see [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)
slot S2.0.

## Reward selection

**Invariant: a bot ALWAYS picks a reward when offered.** Quests, raid
loot, BG honor turn-ins, world events — every reward prompt is resolved
with a non-null selection. Skipping a reward is a bug.

The selector lives at `Exports/BotRunner/Activities/RewardSelector.cs`
and exposes:

```csharp
public interface IRewardSelector
{
    int SelectQuestReward(QuestRewardChoice choice, BotContext ctx);
    int SelectLootRollIntent(LootRollOption option, BotContext ctx); // Need | Greed | Pass-rare
    int SelectVendorPurchase(IReadOnlyList<int> vendorItemIds, BotContext ctx);
}
```

### Three maturity phases (matches [`Spec/20 §5`](20_DECISION_ENGINE.md#5-three-maturity-phases-per-advisor))

The selection algorithm evolves through three phases. Each phase has
a corresponding `AdvisorMode` (Spec/20 §2.1) flip in
`Config/decision-engine.json`'s `advisors.reward.mode` per
[`Spec/20 §4.1`](20_DECISION_ENGINE.md#41-configdecision-enginejson-schema).
Per the Plan/14 exit criteria, Reward is the first advisor to ship all
three phases.

| Phase | `AdvisorMode` | Source | Wire |
|---|---|---|---|
| 1 — Heuristic (trivial) | `ADVISOR_MODE_TRIVIAL` | First valid reward by index. Always non-null. Satisfies the "always picks" invariant. | `RewardSelector.SelectQuestRewardFallback(rewardSet)` in this slot's owner. |
| 2 — Rules + lookup | `ADVISOR_MODE_RULES` | Reads `CharacterBuildConfig.TargetGearSet` + the per-class BiS table at `Config/decision-engine/reward-rules.json`. Falls back to highest vendor value when no gear option matches the BiS slot. Spec/24 `RewardPriority` knob biases the scorer (Plan/16 S12.7). | Plan/14 slot S10.6 ships the rules-file consumer. |
| 3 — ONNX | `ADVISOR_MODE_ML` | `Services/DecisionEngineService/Models/reward/v1.onnx` trained on labeled `outcome` traces (Spec/20 §6.1). Picks the reward that best advances the roster goal under current market prices and lockout state. | Plan/14 slot S10.6 flips mode to `ADVISOR_MODE_ML` once the model file is present. |

### Spec/20 wire (Phase 2 and Phase 3)

For Phase 2 and Phase 3, `RewardSelector` consumes
`IDecisionEngineClient.GetRewardAdviceAsync(RewardContext, ct)` per
[`Spec/20 §2`](20_DECISION_ENGINE.md#2-service-surface). The wire is
owned by Plan/14 slot S10.1 (`Reward selector advisor wire`):

```csharp
public sealed class RewardSelector : IRewardSelector
{
    public async Task<int> SelectQuestRewardAsync(QuestRewardChoice choice, BotContext ctx, CancellationToken ct)
    {
        // Phase 1 fallback always available:
        int trivial = SelectQuestRewardFallback(choice);

        // Phase 2/3 attempt via advisor:
        var rewardCtx = BuildRewardContext(choice, ctx);
        var advice = await _decisionEngine.GetRewardAdviceAsync(rewardCtx, ct);

        if (advice.RecommendedChoiceIndex is int idx
            && advice.Confidence >= 0.5f
            && idx >= 0 && idx < choice.Options.Count)
        {
            EmitAdviceLog(advisor: "reward", advice, used_index: idx);
            return idx;
        }

        EmitAdviceLog(advisor: "reward", advice, used_index: 0xFFFFFFFFu);  // discarded
        return trivial;
    }
    // ... LootRollIntent + VendorPurchase follow the same pattern.
}
```

Fail-soft semantics: `NoAdvice` of any kind, advice with index outside
the option range, or confidence < 0.5 falls through to the Phase-1
trivial selector. Tests pin `Mode=Trivial` for determinism per Plan/14
S10.6.

### `RewardContext` build for Phase 2/3

`BuildRewardContext` populates the proto message per
[`Spec/20 §2.1`](20_DECISION_ENGINE.md#21-proto-wire-shapes):

| Field | Source |
|---|---|
| `bot_level`, `bot_class`, `bot_spec` | `BotContext.Snapshot.Player` |
| `quest_entry` | `QuestRewardChoice.QuestEntry` |
| `reward_item_ids[]` | `QuestRewardChoice.Options[].ItemId` |
| `reward_item_quality[]` | `item_template.Quality` (DB lookup via `IMangosCatalog`) |
| `reward_item_slot[]` | `item_template.InventoryType` |
| `reward_item_sell_price[]` | `item_template.SellPrice` |
| `currently_equipped_in_slot[]` | `BotContext.Snapshot.Player.Equipment[slot]` |
| `active_spec_role` | derived from `BotContext.Snapshot.personality.reward_priority` + `Class.PrimaryRole` |

The context is **pure** over `(choice, snapshot)` plus a single
`IMangosCatalog` DB read per item id (cached per quest entry to avoid
re-reads). No network calls beyond the advisor RPC.

### Tests assert the invariant per selector phase

Naming follows [`Spec/18 #test-naming-convention`](18_TERMINOLOGY.md#test-naming-convention):

- **`RewardSelectorTests.QuestTurnIn_NeverReturnsNull`** (Phase 1, S10.1) —
  no quest turn-in ever results in `selected_reward = null`. `Mode=Trivial`
  pinned for determinism.
- **`RewardSelectorTests.PrefersDecisionEngineAdvice`** (Phase 2/3, S10.1) —
  advisor returns `RewardAdvice { choice_index=2, confidence=0.9 }`;
  the selector picks index 2 and `snapshot.advice_log[0].advisor =
  "reward"` with `used_index = 2`.
- **`RewardSelectorTests.FallsBackOnNoAdvice`** (Phase 2/3, S10.1) —
  advisor returns `NoAdvice`; selector returns the trivial Phase-1
  pick; `snapshot.advice_log[0].used_index = 0xFFFFFFFD`
  (ServiceDown) or similar `AdviceError.*` sentinel per Spec/20 §2.
- **`RewardSelectorTests.RejectsAdviceForUnknownChoiceIndex`** (Phase 2/3,
  S10.1) — advice with `RecommendedChoiceIndex` outside the actual
  choice list is discarded; trivial fallback wins; `advice_log[0].used_index
  = 0xFFFFFFFFu` (discarded sentinel per Spec/19 §5).
- **`RewardSelectorTests.PerSpecBiSSlotPreference`** (Phase 2, S10.6) —
  for each test class/spec, when one option matches the spec's
  `TargetGearSet` slot, the selector picks it.
- **`BotRunner_DynamicProgressive_RewardSelectionClosesBisDistanceTest`** (Phase 2/3, S10.8) —
  the dynamic-progressive invariant. For ≥2 synthetic snapshots
  differing in `(class, spec, currently_equipped_in_slot)`, the selector
  picks DIFFERENT reward indices AND each pick reduces the GearTier
  axis of `RosterPlanner.Distance` (Spec/05). Replay with `Mode=Trivial`
  still completes; ML mode optimizes the BiS axis specifically.

### Snapshot projection — reward selection state

The reward decision projects via the existing `advice_log[]`
(field 36 per Spec/19 §5). No new proto field for reward selection;
the `(advisor="reward", confidence, used_index)` triple from
`AdviceLogEntry` is sufficient for tests.

Additionally, the bot's recent reward picks contribute to
`outcome.roster_distance_delta` in trace JSONL per Spec/20 §6.1 — that's
the live-validation path that ties this slot to the dynamic-progressive
invariant.

### Plan-slot cross-reference

| Slot | Owns | Phase covered |
|---|---|---|
| [`Plan/14/S10.1`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s101--reward-selector-advisor-wire) | `RewardSelector.cs` advisor wire | All phases (consumer side) |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `Config/decision-engine/reward-rules.json` + mode flip | Phase 2 + Phase 3 |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace pipeline that feeds Phase 3 training | Phase 3 input |
| [`Plan/14/S10.8`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s108--livevalidation-for-advisor-wire) | LiveValidation including `DecisionEngine_RewardSelection_GuidedByAdvice` | All phases (correctness guard) |
| [`Plan/16/S12.7`](../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md#s127--reward-knob-consumer) | `RewardPriority` knob biases the Phase-1 fallback path | All phases |
| [`Plan/03/S2.9`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s29--rewardselector-trivial-phase-4-has-the-upgrade) | Trivial Phase-1 selector (initial ship) | Phase 1 |

## Loadout

`LoadoutTask` (existing at `Exports/BotRunner/Tasks/LoadoutTask.cs`)
executes a `LoadoutSpecSettings` plan:

1. Level via GM `.character level <N>` (test-mode) or via real XP
   (automated mode).
2. Learn spells via `LearnAllAvailableSpellsAsync` (TrainerAgent packet
   path).
3. Set skills via `.setskill` SOAP (test-mode) or trainer interaction
   (automated mode).
4. Equip items via `AcquireAndEquipTask` (vendor-buy, mail, or AH).
5. Set talents via `LearnTalentsTask`.
6. Set reputation via `.modify reputation` (test-mode) or via faction
   grind tasks (automated mode).

The loadout reports completion via `WoWActivitySnapshot.LoadoutStatus`.
`AutomatedModeHandler` watches for completion before parsing
`AssignedActivity`.

## Behavior tree library

We use `Xas.FluentBehaviourTree`. Every BotRunner action rebuilds the
tree per invocation — this is by design; trees are throwaway. **Do not
introduce persistent behavior trees**; they hide state from the snapshot
and break test repeatability.

## Snapshot emission

Snapshots emit every `SnapshotIntervalMs` (default 100 ms). The
emission path:

1. Read all required state from `IObjectManager` + task stack.
2. Build `WoWActivitySnapshot` proto.
3. Compute delta from last emitted snapshot.
4. Send full snapshot if delta exceeds threshold or N×100ms since last
   full; otherwise send delta.

Test fixtures wait on snapshot predicates (per the test contract in
[`Spec/13_TESTING.md`](13_TESTING.md)).

## Configuration

Per-character `CharacterSettings` (in `StateManagerSettings.json`)
contains:

```json
{
  "AccountName": "PROD001",
  "ClientPath": "C:\\WoW\\WoW.exe",
  "Mode": "Background",
  "Loadout": { ... },
  "AssignedActivity": "Fishing[Ratchet]",
  "NextActivities": ["Travel[Orgrimmar]", "Vendor[Ragmar]"],
  "FactionSide": "Horde",
  "Role": "DPS",
  "SpecName": "WarriorFury",
  "TalentBuildName": "FuryPvE",
  "CharacterBuildConfig": { ... },
  "ProgressionPriority": "default"
}
```

`CharacterBuildConfig` is the long-horizon plan (BiS gear, rep targets,
mount, gold target, PvP rank). See
[`Spec/05_PROGRESSION.md`](05_PROGRESSION.md).

## Existing code anchors

| Concept | File |
|---|---|
| BotRunner orchestrator | `Exports/BotRunner/BotRunnerService.cs` |
| Action mapping | `Exports/BotRunner/BotRunnerService.ActionMapping.cs` |
| Action dispatch | `Exports/BotRunner/BotRunnerService.ActionDispatch.cs` |
| Task base | `Exports/BotRunner/Tasks/*.cs` |
| Activity resolver | `Exports/BotRunner/Activities/ActivityResolver.cs` |
| Activity parser | `Exports/BotRunner/Activities/ActivityParser.cs` |
| Pathfinding client | `Exports/BotRunner/Clients/PathfindingClient.cs` |
| Snapshot client | `Exports/BotRunner/Clients/CharacterStateUpdateClient.cs` |
| Cross-map router | `Exports/BotRunner/Movement/CrossMapRouter.cs` |
| Transport data | `Exports/BotRunner/Movement/TransportData.cs` |
| Transport state machine | `Exports/BotRunner/Movement/TransportWaitingLogic.cs` |
| Flight path data | `Exports/BotRunner/Combat/FlightPathData.cs` |
