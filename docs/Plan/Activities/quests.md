# Activities — Questing

Quest pickup, objective progress, escort, turn-in, and abandon. The
shipped state is asymmetric: the FG path is driven through
`IObjectManager.QuestFrame` (Lua-button clicks), the BG path is driven
through `WoWSharpObjectManager.TurnInQuestAsync` /
`QuestNetworkClientComponent` (CMSG packets). Both share the same
`ActionType` dispatch through
`Exports/BotRunner/ActionDispatcher.cs`.

## Required task families

| Task | Status | Anchor |
|---|---|---|
| `AcceptQuestTask` | partial | `Exports/BotRunner/Tasks/AcceptQuestTask.cs` (FG QuestFrame click only; no BG path; no quest-id parameter) |
| `KillObjectiveTask` | not-started | embedded in `Exports/BotRunner/Tasks/Questing/QuestingTask.cs` objective scanner; never extracted as a standalone task |
| `CollectObjectiveTask` | not-started | embedded in `Exports/BotRunner/Tasks/Questing/QuestingTask.cs` objective scanner; never extracted as a standalone task |
| `EscortObjectiveTask` | partial | shipped as `Exports/BotRunner/Tasks/Questing/EscortQuestTask.cs` (class is `EscortQuestTask`, not `EscortObjectiveTask`) |
| `TurnInQuestTask` | partial | shipped as `Exports/BotRunner/Tasks/CompleteQuestTask.cs` (class is `CompleteQuestTask`); ActionDispatcher's BG branch dispatches `TurnInQuestAsync` directly without a task wrapper |
| `AbandonQuestTask` | not-started | no class; ActionDispatcher's `CharacterAction.AbandonQuest` case dispatches `QuestAgent.RemoveQuestFromLogAsync(slot)` inline (`Exports/BotRunner/ActionDispatcher.cs:292`) |
| `QuestChainTask` (orchestrator) | not-started | reads `CharacterBuildConfig.QuestChains`; router scaffold at `Exports/BotRunner/Tasks/Questing/QuestChainRouter.cs` |

## Coordinator: `QuestCoordinator`

Per [`Plan/03_PHASE2_ACTIVITY_REGISTRY.md#s210--questcoordinator`](../03_PHASE2_ACTIVITY_REGISTRY.md#s210--questcoordinator).

Responsibilities:

- Assign a quest chain to a bot from
  `docs/leveling-guide/sections/<bracket>.md`.
- For group quests, form a 5-person party (or larger if catalog row
  declares group quest IDs).
- For shared-objective quests (Defias Brotherhood kill cap shared),
  ensure bots in the same area share progress.

## Task specifications

> Phase 0 / S0.8.3 precision blocks. One entry per task name listed in
> [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../Spec/03_BOTRUNNER.md#catalog-of-task-families)
> row Questing. A Phase 1 worker reading any block has enough to
> implement (or finish) the task without a separate investigation pass.
>
> **Interface drift note (R19).** `Spec/03_BOTRUNNER.md` documents
> `IBotTask` as the four-method async contract
> (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` / `OnChildFailedAsync`
> with `BotTaskContext` + `Name` + `BotTaskStatus`). The shipped
> interface at `Exports/BotRunner/Interfaces/IBotTask.cs` is now the
> Phase 1 target contract (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync` + `Name` + `Status`). The `BotTask` base class
> at `Exports/BotRunner/Tasks/BotTask.cs` (constructor
> `BotTask(IBotContext)`, `BotTasks.Pop()` helper, `ObjectManager` /
> `BotContext` / `Container` / `Logger` properties) ships the S1.0
> shim per R25: `TickAsync` → `OnTick` → legacy `Update()` body.
> Existing questing tasks keep their `Update()` body unchanged;
> per-family async refactor lands under S1.6 Questing. See R19/R25
> in [`Plan/QUESTIONS.md`](../QUESTIONS.md#r19).
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by the BotRunner snapshot
> path from `IObjectManager` state + the top of the task stack. "Reads"
> lists the snapshot fields the task is expected to consume (via the
> equivalent `IObjectManager` property today). "Writes" lists the
> snapshot fields whose value changes as a side effect of the task
> running so tests poll the right field. The relevant top-level shapes
> are `WoWActivitySnapshot.Player.QuestLogEntries`
> (`game.proto:159` — `questLog1`/`questLog2`/`questLog3`/`questId` +
> repeated `QuestObjectiveProgress`), `WoWActivitySnapshot.Player.XP`,
> `WoWActivitySnapshot.Player.Coinage`,
> `WoWActivitySnapshot.RecentCommandAcks`, and
> `WoWActivitySnapshot.RecentChatMessages` / `RecentErrors`.
>
> **Dual surface (R19).** BG packet path and FG memory path are
> documented as siblings, not alternatives. The catalog row is
> implementable only when both fire; the activity registry must not
> schedule a BG-only bot for a task whose BG bullet is empty.

### AcceptQuestTask

- **Class declaration:** `BotRunner.Tasks.AcceptQuestTask` at
  `Exports/BotRunner/Tasks/AcceptQuestTask.cs`. Inherits `BotTask` and
  implements `IBotTask`. **Status:** partial — FG-only (`questFrame.AcceptQuest()`); no `(questGiverGuid, questId)` overload, so the BG packet path is reached only via the parameterized branch of `ActionDispatcher` (`CharacterAction.AcceptQuest`), not through this task class.
- **Public surface — current shipped:**
  - `public AcceptQuestTask(IBotContext botContext)` (primary constructor)
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); body reads `ObjectManager.QuestFrame`, calls `AcceptQuest()`, logs `[ACCEPT_QUEST]`, then pops. Per-family async refactor lands under S1.6 Questing (S1.0/R25, shim-only).
  - Base-class members from `BotTask`: `BotContext`, `ObjectManager`, `BotTasks`, `Container`, `Logger`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` / `OnChildFailedAsync` + `Name` + `Status` + `BotTaskContext`). Constructor must accept `(ulong questGiverGuid, uint questId)` so both FG and BG paths can drive a single task instead of branching in `ActionDispatcher`.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `ObjectManager.QuestFrame.IsOpen`, `ObjectManager.QuestFrame.State`, `ObjectManager.QuestFrame.NpcGuid`, `Player.QuestLog[]`.
  - *Writes (as side effects observed in next snapshot):* `Player.QuestLogEntries` gains a new entry with `questId` matching the accepted quest; `RecentChatMessages` may include the "Quest accepted" system line; `RecentCommandAcks` gains the `AckStatus.Success` entry for the dispatched `ActionType.AcceptQuest`.
- **BG protocol footprint:** `CMSG_QUESTGIVER_ACCEPT_QUEST` (0x189). Sent by `WoWSharpClient.Networking.ClientComponents.QuestNetworkClientComponent.AcceptQuestAsync(ulong questGiverGuid, uint questId, ...)`. Server confirmation arrives as `SMSG_QUEST_CONFIRM_ACCEPT` (0x19B) routed through the `QuestNetworkClientComponent.QuestAccepted` observable. Quest-log mutation arrives via `SMSG_QUESTUPDATE_*` and the player object's `QUEST_LOG_*` UpdateFields.
- **FG memory footprint:** `IObjectManager.QuestFrame` (`IQuestFrame` returned by `ForegroundBotRunner.Frames.FgQuestFrame`). `FgQuestFrame.AcceptQuest()` runs the inline Lua `AcceptQuestLua` block (`QuestFrameAcceptButton:Click()` / fallback gossip + quest-title button scan). State checked via `QuestFrameState` (`FrameLuaReader.ReadBool` on `QuestFrameAcceptButton:IsVisible()`). `IWoWPlayer.QuestLog` (`QuestSlot[]` at memory-mapped slots; 4-byte counters + uint state).
- **Test anchor:** `BotRunner.Tests.LiveValidation.StarterQuestTests.Quest_AcceptAndTurnIn_StarterQuest` at `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`. Filter: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~StarterQuestTests.Quest_AcceptAndTurnIn_StarterQuest"`. Snapshot-plumbing companion: `QuestInteractionTests.Quest_AddCompleteAndRemoveAreReflectedInSnapshots` at `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`.
- **Catalog `TaskFamily` claim:** `Questing` (per [Spec/03_BOTRUNNER.md catalog row Questing](../Spec/03_BOTRUNNER.md#catalog-of-task-families) and [R16](../QUESTIONS.md#r16)). Drives every quest row in [`00_INDEX.md`](00_INDEX.md): 6 starter rows (`quest.starter.*`) and 29 zone rows (`quest.zone.*`).

### KillObjectiveTask

- **Planned anchor:** `Exports/BotRunner/Tasks/Questing/KillObjectiveTask.cs`. **Status:** not-started. Today the behavior lives inside `QuestingTask.GetRemainingQuestObjectives()` + the embedded objective scan in `QuestingTask.Update()` (`Exports/BotRunner/Tasks/Questing/QuestingTask.cs:35-82`), which selects the closest objective and pushes `MoveToPositionTask`. The actual kill is delegated to the bot's combat container, not a dedicated task. Slot **SQ.1** wires the kill-count tracker into the snapshot; the standalone task class is the Phase 1 wrapper that pushes the combat task and gates completion on `QuestObjectiveProgress.currentCount >= requiredCount`.
- **Public surface — current shipped:** none (no class). Current state machine: `QuestingTask.Update()` reads `Player.QuestLog`, builds `QuestTaskData` via `Container.QuestRepository.GetQuestTemplateById(...)`, walks `Objectives[].HotSpots`, pushes `MoveToPositionTask(BotContext, position)`. No `void Update()`. No `IBotTask` instance.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10). Constructor `(uint questId, int objectiveIndex, int creatureEntry, int requiredCount, IReadOnlyList<Position> hotSpots)`. `TickAsync` pushes `MoveToPositionTask` for the next hotspot, then a combat-family pull task once a matching `IWoWUnit.Entry == creatureEntry` enters scan radius, and completes when `Player.QuestLog[slotForQuestId].QuestCounters[objectiveIndex] >= requiredCount`.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `Player.QuestLog[]`, `ObjectManager.Units` filtered by `Entry == creatureEntry` and `Health > 0`, `Player.Position`, aggressor / combat state from `ObjectManager.Aggressors`.
  - *Writes (as side effects):* `Player.QuestLogEntries[i].objectives[objectiveIndex].currentCount` advances; on final kill, the same entry's `objectives[objectiveIndex].currentCount == requiredCount` and the quest's `QuestState` flips to 1 (`QUEST_STATE_COMPLETE`) per `QuestHandler.HandleQuestUpdateComplete`. `Player.XP` may tick if any reward was XP-only.
- **BG protocol footprint:** No CMSG. Combat opcodes (`CMSG_ATTACKSWING`, `CMSG_CAST_SPELL`) are owned by the combat family. Inbound progress: `SMSG_QUESTUPDATE_ADD_KILL` (0x199) handled by `WoWSharpClient.Handlers.QuestHandler.HandleQuestUpdateAddKill` → `ctx.ObjectManager.UpdateQuestKillProgress(questId, creatureEntry, killCount, requiredCount)`. Quest completion arrives as `SMSG_QUESTUPDATE_COMPLETE` (0x198).
- **FG memory footprint:** `IWoWPlayer.QuestLog[]` (`QuestSlot.QuestCounters` byte array, 6-bits-per-counter packed format per `QuestSlotOffsets.QUEST_COUNT_STATE_OFFSET`). `ObjectManager.Units` (memory-mapped object list). Aggression/combat state via `IWoWUnit.IsInCombat`. No quest-specific Lua call — kill counts are server-pushed via SMSG and re-read from memory.
- **Test anchor:** `BotRunner.Tests.LiveValidation.QuestObjectiveTests.Quest_KillObjective_CountIncrementsAndCompletes` at `Tests/BotRunner.Tests/LiveValidation/QuestObjectiveTests.cs` (currently uses Sarkoth quest 790; dispatches `StartMeleeAttack` and polls `NearbyUnits` for the mob to disappear, not a `KillObjectiveTask` instance). **Planned anchor test:** `Tests/BotRunner.Tests/LiveValidation/KillObjectiveTaskTests.cs::Kill_QuestObjective_TaskCompletesWhenCounterMet`. Filter: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~QuestObjectiveTests.Quest_KillObjective"`.
- **Catalog `TaskFamily` claim:** `Questing`. Drives every quest row in [`00_INDEX.md`](00_INDEX.md) that has a `ReqCreatureOrGOId*` > 0 (the majority of starter + zone rows).

### CollectObjectiveTask

- **Planned anchor:** `Exports/BotRunner/Tasks/Questing/CollectObjectiveTask.cs`. **Status:** not-started. Today the behavior is intertwined with `KillObjectiveTask`'s embedded scanner in `QuestingTask` — `QuestingTask.BuildObjective` (`Exports/BotRunner/Tasks/Questing/QuestingTask.cs:196`) sets `RequiredItemId` / `RequiredItemCount` and resolves spawn positions for either creatures that drop the item (`repo.GetCreaturesByLootableItemId`) or gameobjects that contain it (`repo.GetGameObjectsByLootableItemId`), but no standalone class implements the "loot until count" loop. Slot **SQ.1** wires the item-collection tracker into the snapshot.
- **Public surface — current shipped:** none (no class). `QuestObjectiveData.RequiredItemId` / `RequiredItemCount` are read by `QuestingTask` and drive hotspot selection; looting is delegated to the existing `LootCorpseTask` / `GatherNodeTask` paths.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10). Constructor `(uint questId, int objectiveIndex, int itemId, int requiredCount, IReadOnlyList<Position> hotSpots, int? sourceCreatureId, int? sourceGameObjectId)`. `TickAsync` pushes `MoveToPositionTask`, then a kill-or-interact child task per source kind, then a `LootCorpseTask` or container-open task, completing when `IWoWPlayer.Inventory` count for `itemId` reaches `requiredCount` (mirrors `SMSG_QUESTUPDATE_ADD_ITEM` increments).
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `Player.Inventory`, `Player.PackSlots` (`IWoWItem[]` enumeration to count `ItemId == itemId`), `Player.QuestLog[]`, `ObjectManager.Objects` filtered by `Entry == sourceCreatureId/sourceGameObjectId`.
  - *Writes (as side effects):* `Player.QuestLogEntries[i].objectives[objectiveIndex].currentCount` advances by `itemCount` per `SMSG_QUESTUPDATE_ADD_ITEM`; `Player.bagContents` (proto `Game.WoWPlayer.bagContents` map) gains the item id; `RecentCommandAcks` gains the dispatch ack.
- **BG protocol footprint:** No CMSG (looting + container open opcodes are owned by `LootCorpseTask` / `UseGameObject` paths: `CMSG_LOOT`, `CMSG_LOOT_RELEASE`, `CMSG_GAMEOBJ_USE`). Inbound progress: `SMSG_QUESTUPDATE_ADD_ITEM` (0x19A) handled by `WoWSharpClient.Handlers.QuestHandler.HandleQuestUpdateAddItem` → `ctx.ObjectManager.UpdateQuestItemProgress(itemId, itemCount)`.
- **FG memory footprint:** `IWoWPlayer.Inventory` / `PackSlots` (uint id arrays read from player memory). `IObjectManager.Objects` for source spawns. No Lua. Lootframe automation (when the source kill drops the item) is handled by `IObjectManager.LootFrame` + the existing `LootCorpseTask`.
- **Test anchor:** **Planned anchor test:** `Tests/BotRunner.Tests/LiveValidation/CollectObjectiveTaskTests.cs::Collect_QuestObjective_ItemCountReachesRequired`. Filter (once added): `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~CollectObjectiveTaskTests"`. No shipped test exists today; `QuestObjectiveTests` covers only the kill branch.
- **Catalog `TaskFamily` claim:** `Questing`. Drives every quest row in [`00_INDEX.md`](00_INDEX.md) whose `mangos.quest_template.ReqItemId*` is non-zero (collection quests in Westfall, Loch Modan, The Barrens, etc.).

### EscortObjectiveTask

- **Class declaration:** `BotRunner.Tasks.Questing.EscortQuestTask` at `Exports/BotRunner/Tasks/Questing/EscortQuestTask.cs`. Inherits `BotTask` and implements `IBotTask`. **Note:** the spec name is `EscortObjectiveTask` (per [Spec/03_BOTRUNNER.md catalog row Questing](../Spec/03_BOTRUNNER.md#catalog-of-task-families)); the shipped class is `EscortQuestTask`. Phase 1 may rename, but the existing class is the implementation anchor. **Status:** partial — local state machine works but is never pushed by `ActionDispatcher` and has no live test.
- **Public surface — current shipped:**
  - `public EscortQuestTask(IBotContext context, uint npcEntry, float followDistance = 5f)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - Private state machine: `enum EscortState { FindNpc, FollowNpc, DefendNpc, Complete }`; constants `MaxFollowDistance = 30f`, `DefendRange = 15f`.
  - Base-class members from `BotTask`: `BotContext`, `ObjectManager`, `Logger`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10). Constructor `(uint questId, uint npcEntry, float followDistance)`. `TickAsync` runs the existing state machine asynchronously; `OnChildFailedAsync` handles `task_unrecoverable` when the escort NPC dies (slot SQ.2 failure-recovery row).
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `ObjectManager.Units` filtered by `Entry == _npcEntry && Health > 0`; once locked on, by `Guid == _npcGuid`. `ObjectManager.Units` again filtered by `TargetGuid == _npcGuid && Health > 0 && IsInCombat` for threat detection. `Player.Position` for follow distance.
  - *Writes (as side effects):* `Player.QuestLogEntries[i].QuestState` flips to 1 (escort complete) when the escort NPC reaches its destination and dispatches `SMSG_QUESTUPDATE_COMPLETE`. `Player.Position` advances (movement-controller side effect). `RecentErrors` may carry "[ESCORT] NPC lost or dead" diagnostic when failure path fires.
- **BG protocol footprint:** No quest-family CMSG; movement is driven through standard movement opcodes (`MSG_MOVE_*` via `WoWSharpClient.Movement`). Combat opcodes when defending threats. Inbound: `SMSG_QUESTUPDATE_COMPLETE` (0x198) handled by `QuestHandler.HandleQuestUpdateComplete` flips `QuestSlot.QuestState` to 1.
- **FG memory footprint:** `IObjectManager.Units` (memory-mapped). `IObjectManager.MoveToward(Position)` (Lua-less; sets `ControlBits.Front` after `SetFacing`). No Lua call. Escort dialogue (if any) reaches the player through `WoWEventHandler.OnChatMessage`.
- **Test anchor:** **Planned anchor test:** `Tests/BotRunner.Tests/LiveValidation/EscortQuestTests.cs::Escort_QuestObjective_NpcCompletesEscortAndQuestFlips`. Filter (once added): `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~EscortQuestTests"`. No shipped LiveValidation test exists today.
- **Catalog `TaskFamily` claim:** `Questing`. Drives every quest row in [`00_INDEX.md`](00_INDEX.md) whose `mangos.quest_template.SpecialFlags` carries the escort bit (e.g. Westfall "The Defias Brotherhood" sub-step, Hillsbrad "The Syndicate" chain).

### TurnInQuestTask

- **Class declaration:** `BotRunner.Tasks.CompleteQuestTask` at `Exports/BotRunner/Tasks/CompleteQuestTask.cs`. Inherits `BotTask` and implements `IBotTask`. **Note:** the spec name is `TurnInQuestTask` (per [Spec/03_BOTRUNNER.md catalog row Questing](../Spec/03_BOTRUNNER.md#catalog-of-task-families)); the shipped class is `CompleteQuestTask`. **Status:** partial — FG QuestFrame click path only; the BG packet path is reached only via the parameterized branch of `ActionDispatcher.CharacterAction.CompleteQuest` (`Exports/BotRunner/ActionDispatcher.cs:312`), which calls `_objectManager.TurnInQuestAsync(npcGuid, questId, rewardIndex, ct)` inline instead of pushing this task.
- **Public surface — current shipped:**
  - `public CompleteQuestTask(IBotContext botContext, int rewardIndex = 0)` (primary constructor)
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); body reads `ObjectManager.QuestFrame`, calls `CompleteQuest(rewardIndex)`, logs `[COMPLETE_QUEST]`, then pops. Per-family async refactor lands under S1.6 Questing (S1.0/R25, shim-only).
  - Base-class members from `BotTask`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10). Constructor `(ulong questGiverGuid, uint questId, int rewardIndex)`. `TickAsync` drives the dual-surface path: FG via `QuestFrame.CompleteQuest(rewardIndex)`, BG via `IObjectManager.TurnInQuestAsync`. Reward selection MUST be non-null per the "always picks" invariant ([R18](../QUESTIONS.md#r18)) — the `IRewardSelector` phase 2 selector picks index 0 when the spec leaves `rewardIndex` unset.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `ObjectManager.QuestFrame.IsOpen`, `ObjectManager.QuestFrame.State` (`QuestFrameState.Complete`), `ObjectManager.QuestFrame.RewardCount`, `Player.QuestLog[]` for the source slot (must have `QuestState == 1`).
  - *Writes (as side effects):* `Player.QuestLogEntries[i]` is removed (turn-in pops the slot). `Player.XP` increments by the quest's reward XP. `Player.Coinage` increments by the gold reward. `Player.Inventory` / `bagContents` gains the chosen reward item (per R18). `RecentChatMessages` may include the quest-complete system line. `RecentCommandAcks` gains the dispatched action's ack.
- **BG protocol footprint:** Dual opcode sequence per `QuestNetworkClientComponent`: `CMSG_QUESTGIVER_COMPLETE_QUEST` (0x18A) → server replies with `SMSG_QUESTGIVER_QUEST_COMPLETE` carrying the reward list → `CMSG_QUESTGIVER_CHOOSE_REWARD` (0x18E) with the chosen reward index. Optional `CMSG_QUESTGIVER_REQUEST_REWARD` (0x18C) when the reward UI must be re-fetched. The `WoWSharpObjectManager.TurnInQuestAsync(npcGuid, questId, rewardIndex, ct)` helper is the single BG-side entry point called from `ActionDispatcher`.
- **FG memory footprint:** `IObjectManager.QuestFrame` (`IQuestFrame.CompleteQuest(int? parReward)`). `ForegroundBotRunner.Frames.FgQuestFrame.CompleteQuest(int?)` runs (when `parReward.HasValue`) the inline Lua `QuestRewardItem{rewardButtonIndex}:Click()` then `CompleteQuestLua` (clicks `QuestFrameCompleteQuestButton` / `QuestFrameCompleteButton` / falls back to first visible reward / gossip / quest-title button). `IWoWPlayer.QuestLog` re-read for the slot's `QuestState` and removal.
- **Test anchor:** `BotRunner.Tests.LiveValidation.StarterQuestTests.Quest_AcceptAndTurnIn_StarterQuest` at `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs` (covers the full accept→complete loop for Valley of Trials quest 4641). Filter: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~StarterQuestTests.Quest_AcceptAndTurnIn"`. Reward-choice coverage: `BotRunner.Tests.LiveValidation.GossipQuestTests` references `RewardQuestId = 2161` for selection assertions.
- **Catalog `TaskFamily` claim:** `Questing`. Drives every quest row in [`00_INDEX.md`](00_INDEX.md): 6 starter rows + 29 zone rows. The "always picks a reward" invariant ([R18](../QUESTIONS.md#r18)) is enforced inside this task via the configured `IRewardSelector`.

### AbandonQuestTask

- **Planned anchor:** `Exports/BotRunner/Tasks/AbandonQuestTask.cs`. **Status:** not-started. Today `CharacterAction.AbandonQuest` is dispatched inline by `Exports/BotRunner/ActionDispatcher.cs:292` — the dispatcher unboxes `params[0]` as a byte quest-log slot and calls `factory.QuestAgent.RemoveQuestFromLogAsync(questSlot)` directly inside a `builder.Do("Abandon Quest", ...)` lambda. There is no `IBotTask` class wrapping it, no FG `IQuestFrame.AbandonQuest()` member, and no proto `ActionType` value (the C# enum has `CharacterAction.AbandonQuest` but the proto enum is missing it per `BotRunnerService.ActionMapping.cs:15`).
- **Public surface — current shipped:** none (no class). The dispatcher lambda IS the implementation.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10). Constructor `(byte questLogSlot)` for the BG packet path or `(uint questId)` with slot-lookup against `IWoWPlayer.QuestLog`. `TickAsync` issues the dual surface: FG via a new `IQuestFrame.AbandonQuest(byte slot)` (Lua `AbandonQuest(questId)`) member, BG via `QuestNetworkClientComponent.RemoveQuestFromLogAsync(byte)`. Phase 1 also adds `ABANDON_QUEST` to the `ActionType` proto so StateManager can dispatch directly.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `Player.QuestLog[]` to resolve `questId` → slot index when a `(uint questId)` overload is used. `Player.QuestLog[slot].QuestId` validated non-zero before issuing the CMSG.
  - *Writes (as side effects):* `Player.QuestLogEntries` loses the entry for the abandoned slot (slot is zeroed server-side, re-read into snapshot). `RecentCommandAcks` gains the ack. No XP, no coinage delta.
- **BG protocol footprint:** `CMSG_QUESTLOG_REMOVE_QUEST` (0x194 — note the shipped enum name is `CMSG_QUESTLOG_REMOVE_QUEST`, not the spec-brief's `CMSG_QUEST_LOG_REMOVE_QUEST`; both refer to the same opcode). Sent by `QuestNetworkClientComponent.RemoveQuestFromLogAsync(byte questLogSlot, ...)`. Server clears the slot; quest-log mutation arrives via `SMSG_QUEST_FORCE_REMOVED` / refreshed player UpdateFields. No reward, no chooser.
- **FG memory footprint:** No shipped FG path. **Planned:** add `IQuestFrame.AbandonQuest(byte questLogSlot)` driving Lua `SelectQuestLogEntry(questLogSlot+1); SetAbandonQuest(); AbandonQuest();` (the 1.12.1 UI 3-call dance). Slot resolved against `IWoWPlayer.QuestLog`.
- **Test anchor:** **Planned anchor test:** `Tests/BotRunner.Tests/LiveValidation/AbandonQuestTests.cs::Quest_Abandon_RemovesFromLogAndSnapshot`. Filter (once added): `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~AbandonQuestTests"`. Today, abandon is exercised only as a cleanup side-step inside `QuestInteractionTests.StageBotRunnerQuestAbsentAsync`-style fixture helpers; no first-class behavior test exists.
- **Catalog `TaskFamily` claim:** `Questing`. Used as a recovery path for every quest row in [`00_INDEX.md`](00_INDEX.md) (failure recovery + abandon-and-retake when a chain step is blocked).

## Slots

### SQ.1 — Quest tracker (kill / collect counters)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/WoWSharpClient/Handlers/QuestObjectiveHandler.cs`
  - `Exports/BotRunner/Tasks/Quest/QuestObjectiveTracker.cs`
- **Goal:** Parse `SMSG_QUESTUPDATE_ADD_KILL` and
  `SMSG_QUESTUPDATE_ADD_ITEM` into the snapshot. Tests assert kill /
  collect counts visible in the snapshot.

### SQ.2 — `EscortObjectiveTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Quest/EscortObjectiveTask.cs`
- **Goal:** Detect NPC moving (escort spawn), follow within leash
  range, kill ambushes, complete on NPC dialogue.

### SQ.3 — `QuestChainTask` orchestrator

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** SQ.1, SQ.2
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Quest/QuestChainTask.cs`
- **Goal:** Drive a multi-quest chain from a `CharacterBuildConfig`
  reference. Knows quest prerequisites and turn-in NPCs.

### SQ.4 — Per-zone quest catalogs

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Bot/quests/zone-<name>.json`
- **Goal:** One JSON per quest zone catalog row. Each lists the
  primary chain quest IDs in turn-order, plus optional side quests.
  Authored from `docs/leveling-guide/zones/<name>.md`.

### SQ.5 — Group-quest detection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Quests with `Group: 5` flag in `mangos.quest_template`
  surface to the scheduler as group-quest catalog rows.

### SQ.6 — Starter-quest LiveValidation per starting zone

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Goal:** 6 tests (one per starting zone). Each levels a fresh bot
  1→5 via real quest XP without GM `.character level`.

### SQ.7 — Zone-quest LiveValidation per zone (~26)

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Goal:** One test per zone catalog row. Drives a level-appropriate
  bot through the primary chain; asserts XP gain matches expected.

## Failure recovery

- **Quest item not in inventory after kill** → KillObjectiveTask
  re-runs until N kills; emit `task_timeout` if still missing after
  3× expected (drop-rate aware).
- **Escort NPC dies** → fail with `task_unrecoverable`; coordinator
  retries (escort respawns on a timer).
- **Quest no longer offered (already turned in)** → mark as done in
  `CharacterRosterGoal.CompletedQuestChains`.
