# Activities — Battlegrounds

3 catalog rows: Warsong Gulch (10v10), Arathi Basin (15v15),
Alterac Valley (40v40). Coordinator exists; objective tasks need
per-BG implementation.

## Required task families

| Task | Status |
|---|---|
| `BattlegroundQueueTask` | done |
| `BgFlagCapTask` (WSG) | not-started |
| `BgNodeCapTask` (AB) | not-started |
| `BgGyTowerTask` (AV) | not-started |
| `BgBossEngagementTask` (AV — Drek/Vann) | not-started |
| `BgPostTeleportStabilizationTask` | partial (existing test class implies stabilization) |

## Task specifications

> Phase 0 / S0.8.6 precision blocks. Per-task entries cover the BG
> family head in `Spec/03_BOTRUNNER.md#catalog-of-task-families`:
> `BattlegroundQueueTask` and `BgObjectiveTask`. The catalog lists a
> single `BgObjectiveTask` entry but the shipped code splits it into
> three per-BG implementations (`WsgObjectiveTask`, `AbObjectiveTask`,
> `AvObjectiveTask`). Each gets its own block so a Phase 1 worker has
> enough to land the Phase 1 contract migration (S1.0 →
> family slots under S1.4..S1.13) without a separate investigation.
>
> **Interface drift note.** Per R19 in `Plan/QUESTIONS.md`, the spec
> documents `IBotTask` as a four-method async contract (`TickAsync`,
> `OnPushedAsync`, `OnPoppedAsync`, `OnChildFailedAsync` with
> `BotTaskContext`, `Name`, `BotTaskStatus`). The shipped interface
> at `Exports/BotRunner/Interfaces/IBotTask.cs` is now the Phase 1
> target contract (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync` + `Name` + `Status`, with a
> `BotTaskContext` carrying `IObjectManager`, `PathfindingClient`,
> `ChatSink`, `IMetricsSink`, and bridge `IBotContext`). The
> `BotTask` base class (`Exports/BotRunner/Tasks/BotTask.cs`)
> implements the shim per R25: `TickAsync` -> `OnTick` -> legacy
> `Update()` body. Concrete tasks keep their `void Update()` body
> unchanged; per-family async refactor lands under each S1.4..S1.13
> family slot. Each block below reports the **current shipped
> surface** (post-S1.0 shim) AND the **target surface (Phase 1)** per
> Spec/03's target contract.
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by
> `Exports/BotRunner/SnapshotBuilder.cs` from `IObjectManager` state +
> the top of the task stack. *Reads* lists the snapshot fields the
> task is expected to consume (via the equivalent `IObjectManager`
> property today). *Writes* lists the snapshot fields whose value
> changes as a *side effect* of the task running (so tests poll the
> right field).

### BattlegroundQueueTask

- **Class declaration:** `BotRunner.Tasks.Battlegrounds.BattlegroundQueueTask`
  at `Exports/BotRunner/Tasks/Battlegrounds/BattlegroundQueueTask.cs`.
  Inherits `BotTask` and implements `IBotTask`. **Status:** done.
- **Public surface — current shipped:**
  - `public BattlegroundQueueTask(IBotContext botContext, BattlemasterData.BattlegroundType bgType, uint expectedBgMapId, BattlegroundNetworkClientComponent? bgClient = null)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - Private state machine `BgState` (`FindBattlemaster` →
    `MoveToBattlemaster` → `InteractAndQueue` → `WaitForInvite` →
    `AcceptInvite` → `WaitForEntry` → `Done`).
  - Private helpers: `HandleFindBattlemaster`, `HandleMoveToBattlemaster`,
    `HandleInteractAndQueue`, `HandleWaitForInvite`,
    `HandleAcceptInvite`, `HandleWaitForEntry`, `SetState`,
    `RestartQueueCycle`, `ShouldQueueAsGroup`,
    `ShouldWaitForLeaderGroupQueue`, `RequiresIndividualQueue`,
    `BuildQueueDiagnostics`, `TryRecoverFromVerticalQueueOffset`,
    `ResolveExpectedBattlemaster`.
- **Public surface — target (Phase 1, after S1.0):** per
  `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`:
  - `string Name { get; }` returns
    `"BattlegroundQueueTask:" + bgType` (e.g.
    `"BattlegroundQueueTask:WarsongGulch"`).
  - `BotTaskStatus Status { get; }` (`Running` | `Complete` | `Failed`).
  - `Task TickAsync(BotTaskContext context, CancellationToken ct)` —
    state-machine body migrates here; existing `Thread.Sleep` + `.GetAwaiter().GetResult()`
    calls become `await`s on `InteractWithNpcAsync` /
    `JoinQueueAsync` / `AcceptInviteAsync`.
  - `Task OnPushedAsync(BotTaskContext context, CancellationToken ct)` —
    captures `_bgType`, `_expectedBgMapId`, resolves the
    `BattlegroundNetworkClientComponent` from `context`, logs
    `[BG-QUEUE] Task started`.
  - `Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)` —
    emits queue-cycle diagnostics on `Failed` (currently the
    `[BG-QUEUE] timed out … Diagnostics` log line).
  - `Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)` —
    treats nav-leg failures (`no_path_timeout` etc.) as recoverable;
    re-enters `BgState.FindBattlemaster` on child fail.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `player.Position`, `player.MapId`,
    `player.Race`, `player.Guid`, `player.Name`,
    `IObjectManager.Units` (battlemaster scan by `Entry` /
    `NpcFlags & UNIT_NPC_FLAG_BATTLEMASTER`),
    `IObjectManager.GossipFrame.IsOpen`,
    `IObjectManager.PartyLeaderGuid`,
    `IObjectManager.Party{1..4}Guid`,
    `IObjectManager.PartyMembers`,
    `BattlegroundNetworkClientComponent.CurrentState`
    (`BattlegroundState.Invited` / `InBattleground`).
    These project into `WoWActivitySnapshot.player` plus group fields.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.MapId` flips to `_expectedBgMapId`
    on BG entry; `currentTaskName` (top of task stack) clears on
    `PopTask("bg_entered" | "already_in_bg" | "timeout" |
    "queue_failed" | "no_bg_agent" | "no_player")`.
    Movement-side effects from `NavigateToward` /
    `ObjectManager.StopAllMovement()` mutate
    `WoWActivitySnapshot.movementData`.
- **BG protocol footprint:** opcodes are declared in
  `Exports/GameData.Core/Enums/Opcode.cs` and wired through
  `Exports/WoWSharpClient/Networking/ClientComponents/BattlegroundNetworkClientComponent.cs`.
  - `CMSG_BATTLEMASTER_JOIN = 0x2EE` — sent by
    `BattlegroundNetworkClientComponent.JoinQueueAsync` (payload:
    `uint64 battleMasterGuid + uint32 mapId + uint32 instanceId +
    uint8 joinAsGroup`). Called from `HandleInteractAndQueue`.
  - `CMSG_BATTLEFIELD_PORT = 0x2D5` — sent by
    `BattlegroundNetworkClientComponent.AcceptInviteAsync` (payload:
    `uint32 mapId + uint8 action(1=accept)`). Called from
    `HandleAcceptInvite`.
  - `CMSG_BATTLEMASTER_HELLO = 0x2D7` — required by some servers to
    open the battlemaster gossip; emitted indirectly by
    `ObjectManager.InteractWithNpcAsync(_bmGuid, …)` via
    `Exports/WoWSharpClient/Networking/ClientComponents/GameObjectNetworkClientComponent.cs`
    `CMSG_GAMEOBJ_USE = 0x?` / NPC interact opcode.
  - `CMSG_BATTLEFIELD_STATUS = 0x2D3` — polled by
    `BattlegroundNetworkClientComponent.RefreshQueueStatusAsync` for
    `CurrentState` updates that drive `BgState.WaitForInvite`.
  - `CMSG_BATTLEFIELD_LIST = 0x23C` — discovery; informational.
  - `CMSG_LEAVE_BATTLEFIELD = 0x2E1` — not sent by this task
    (handled by `BgRewardCollectionTask` post-match).
- **FG memory footprint:**
  - `IObjectManager.Player` (position, race, guid, mapid).
  - `IObjectManager.Units` enumeration filtered by
    `Entry == BattlemasterData.FindBattlemaster(...).NpcEntry` and
    by `NpcFlags & UNIT_NPC_FLAG_BATTLEMASTER`.
  - `IObjectManager.SetTarget(ulong guid)`,
    `IObjectManager.InteractWithNpcAsync(ulong guid, CancellationToken)`,
    `IObjectManager.StopAllMovement()`.
  - `IObjectManager.GossipFrame` →
    `GossipFrame.SelectFirstGossipOfType(DialogType.battlemaster)`.
  - `IObjectManager.JoinBattleGroundQueue()` — FG-only Lua fallback
    when `_bgClient == null`. Implementation at
    `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs:158`:
    `MainThreadLuaCall("BattlefieldFrameGroupJoinButton:Click()")` /
    `MainThreadLuaCall("BattlefieldFrameJoinButton:Click()")`,
    gated by `MainThreadLuaCallWithResult` on
    `BattlefieldFrame:IsVisible()`.
  - `IObjectManager.AcceptBattlegroundInvite()` — FG-only Lua at
    `ObjectManager.Interaction.cs:175`: scans
    `GetBattlefieldStatus(1..3)` for `status == 'confirm'` and calls
    `AcceptBattlefieldPort(i, 1)` via `MainThreadLuaCall`.
  - `IObjectManager.PartyLeaderGuid`, `Party{1..4}Guid`,
    `PartyMembers` (for `ShouldQueueAsGroup`).
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.BattlegroundQueueTests.BG_QueueForWSG_ReceivesQueuedStatus`
  at `Tests/BotRunner.Tests/LiveValidation/BattlegroundQueueTests.cs:36`;
  additional coverage in
  `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  (`AB_QueueAndEnterBattleground` at line 45,
  `AV_FullMatch_EnterPrepQueueMountAndReachObjective` at line 85);
  unit coverage at `Tests/BotRunner.Tests/Combat/BattlegroundQueueTaskTests.cs`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~BattlegroundQueueTests.BG_QueueForWSG_ReceivesQueuedStatus"`
- **Catalog `TaskFamily` claim:** `Bg`. Underlies every catalog row in
  `Plan/Activities/00_INDEX.md#battlegrounds-—-see-battlegroundsmd`:
  `bg.wsg`, `bg.ab`, `bg.av` — each row dispatches a
  `BattlegroundQueueTask` to bring its bots from the staging
  battlemaster into the instance map before the BG-specific objective
  task takes over.

### WsgObjectiveTask

- **Class declaration:** `BotRunner.Tasks.Battlegrounds.WsgObjectiveTask`
  at `Exports/BotRunner/Tasks/Battlegrounds/WsgObjectiveTask.cs`.
  Inherits `BotTask` and implements `IBotTask`. **Status:** partial
  (objective state machine drafted; uses raw `MoveToward` not
  pathfinding; no SMSG_UPDATE_WORLD_STATE flag-state subscription;
  no group coordination).
- **Public surface — current shipped:**
  - `public WsgObjectiveTask(IBotContext context, bool isHorde)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - Private state machine `WsgState` (`FindObjective` →
    `MoveToFlag` → `PickupFlag` → `CarryFlagToBase` →
    `CaptureFlag` → `DefendBase` → `Complete`).
  - Compile-time constants for flag entries
    (`AllianceFlagEntry = 179830`, `HordeFlagEntry = 179831`,
    `AllianceFlagDroppedEntry = 179785`,
    `HordeFlagDroppedEntry = 179786`) and base positions
    (`HordeBase`, `AllianceBase`).
- **Public surface — target (Phase 1, after S1.0):**
  - `string Name { get; }` returns `"WsgObjectiveTask:" + (isHorde ? "Horde" : "Alliance")`.
  - `BotTaskStatus Status { get; }`.
  - `Task TickAsync(BotTaskContext context, CancellationToken ct)` —
    body of `Update()` migrates, with `banner.Interact()` replaced by
    an `await context.GameObjectClient.InteractAsync(go.Guid, ct)`.
  - `Task OnPushedAsync(BotTaskContext context, CancellationToken ct)` —
    subscribes to `SMSG_UPDATE_WORLD_STATE` via
    `BattlegroundNetworkClientComponent` so the task observes flag
    pickup/drop/return without polling `GameObjects`.
  - `Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)` —
    unsubscribes from world-state events, calls
    `context.ObjectManager.StopAllMovement()`.
  - `Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)` —
    a failed `GoToTask` re-enters `WsgState.FindObjective`; repeated
    failures escalate via `BotTaskStatus.Failed`.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `player.Position`, `player.MapId`,
    `IObjectManager.GameObjects` (filtered by flag entry IDs),
    `IObjectManager.Player` (race-derived faction via the
    `isHorde` ctor param).
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.Position` (via
    `ObjectManager.MoveToward(targetFlag.Position | homeBase)`),
    movement-flag deltas from `ForceStopImmediate()`, and
    `currentTaskName` clears on `BotContext.BotTasks.Pop()` when
    `WsgState.Complete`.
- **BG protocol footprint:** opcodes via
  `Exports/WoWSharpClient/Networking/ClientComponents/GameObjectNetworkClientComponent.cs`.
  - `CMSG_GAMEOBJ_USE` — emitted indirectly by
    `IWoWGameObject.Interact()` on flag pickup
    (`WsgState.PickupFlag`) and on own-flag-stand capture
    (`WsgState.CaptureFlag`). The send path is
    `GameObjectNetworkClientComponent.SendOpcodeAsync(Opcode.CMSG_GAMEOBJ_USE, payload, …)`
    with `payload = uint64 gameObjectGuid`.
  - Standard BG movement opcode set
    (`MSG_MOVE_HEARTBEAT`, `MSG_MOVE_START_FORWARD`,
    `MSG_MOVE_STOP`, `MSG_MOVE_SET_FACING`) — fan-out of
    `MoveToward` / `ForceStopImmediate`.
  - *Inbound* (read-only, not sent): `SMSG_UPDATE_WORLD_STATE`,
    `SMSG_BATTLEGROUND_PLAYER_POSITIONS`,
    `SMSG_PLAY_SOUND` (flag-pickup audio cue) — Phase 1 target wires
    these via the `BattlegroundNetworkClientComponent` so the task
    does not have to poll `GameObjects`.
- **FG memory footprint:**
  - `IObjectManager.Player`, `IObjectManager.GameObjects`.
  - `IWoWGameObject.Interact()` (NPC/object click — routes through
    Lua `UseGameObject` on FG and `CMSG_GAMEOBJ_USE` on BG).
  - `IObjectManager.MoveToward(Position)`,
    `IObjectManager.ForceStopImmediate()`.
  - No direct `LuaCall` invocations in `WsgObjectiveTask`; FG world
    state is observed entirely through `ObjectManager` reads.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.Battlegrounds.WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground`
  at `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchTests.cs:26`
  (the fixture in
  `WarsongGulchFixture.cs` / `WarsongGulchObjectiveCollection.cs`
  stages 10v10 then dispatches the objective). **Planned anchor
  test:** `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs::WSG_Horde_PicksUpAllianceFlag_AndCapturesAtFriendlyBase`
  for the flag-cap loop in isolation (currently absent — the WSG file
  only covers the queue/entry leg). Status: `not-started` for the
  dedicated objective test.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground"`
- **Catalog `TaskFamily` claim:** `Bg`. Drives catalog row `bg.wsg`
  in `Plan/Activities/00_INDEX.md` (Warsong Gulch, 10v10, level
  10-60).

### AbObjectiveTask

- **Class declaration:** `BotRunner.Tasks.Battlegrounds.AbObjectiveTask`
  at `Exports/BotRunner/Tasks/Battlegrounds/AbObjectiveTask.cs`.
  Inherits `BotTask` and implements `IBotTask`. **Status:** partial
  (node selection + assault drafted; no node-control state tracking
  via `SMSG_UPDATE_WORLD_STATE`; no defender split between bots).
- **Public surface — current shipped:**
  - `public AbObjectiveTask(IBotContext context, bool isHorde)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `public static readonly Dictionary<string, Position> NodePositions`
    (Stables, Farm, Blacksmith, Lumber Mill, Gold Mine).
  - Private state machine `AbState` (`SelectNode` →
    `MoveToNode` → `AssaultNode` → `DefendNode` → `Complete`).
- **Public surface — target (Phase 1, after S1.0):**
  - `string Name { get; }` returns
    `"AbObjectiveTask:" + (isHorde ? "Horde" : "Alliance") + ":" + _targetNodeName`.
  - `BotTaskStatus Status { get; }`.
  - `Task TickAsync(BotTaskContext context, CancellationToken ct)` —
    `banner.Interact()` becomes
    `await context.GameObjectClient.InteractAsync(banner.Guid, ct)`;
    move legs push `GoToTask` children instead of raw `MoveToward`.
  - `Task OnPushedAsync(BotTaskContext context, CancellationToken ct)` —
    subscribes to AB `SMSG_UPDATE_WORLD_STATE` for the five node
    states (`STATE_NODE_*`) so node selection is driven by which
    nodes are uncontrolled / contested by the enemy.
  - `Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)` —
    unsubscribes from world-state events.
  - `Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)` —
    a failed `GoToTask` toward a node demotes that node's priority
    and reselects via `AbState.SelectNode`.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `player.Position`, `player.MapId`,
    `IObjectManager.GameObjects` (banner GO within 15y of the
    target node), `IObjectManager.Player`. Phase 1 also reads
    AB-specific world-state fields from
    `BattlegroundNetworkClientComponent`.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.Position` (via `MoveToward`),
    movement-flag deltas from `ForceStopImmediate()`, and the
    top-of-task-stack `currentTaskName` clears on
    `BotContext.BotTasks.Pop()` when `AbState.Complete`.
- **BG protocol footprint:**
  - `CMSG_GAMEOBJ_USE` — emitted indirectly by
    `IWoWGameObject.Interact()` on banner assault
    (`AbState.AssaultNode`), wired via
    `GameObjectNetworkClientComponent.SendOpcodeAsync(Opcode.CMSG_GAMEOBJ_USE, …)`.
  - Standard BG movement opcode set as in `WsgObjectiveTask`.
  - *Inbound* (read-only): `SMSG_UPDATE_WORLD_STATE` for node
    control (`WORLD_STATE_AB_NODE_STATE_*`) and resource
    accumulation (`WORLD_STATE_AB_RESOURCES_*`); Phase 1 wires
    these through `BattlegroundNetworkClientComponent`.
- **FG memory footprint:**
  - `IObjectManager.Player`, `IObjectManager.GameObjects`.
  - `IWoWGameObject.Interact()`.
  - `IObjectManager.MoveToward(Position)`,
    `IObjectManager.ForceStopImmediate()`.
  - No `LuaCall` invocations.
- **Test anchor:** **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AbObjectiveTests.cs::AB_Horde_AssaultsFarmNode_AndHoldsForResources`.
  Status: `not-started`. Today coverage stops at
  `BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AB_QueueAndEnterBattleground`
  at `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs:45`
  (queue/entry leg only — no objective dispatch).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~BattlegroundEntryTests.AB_QueueAndEnterBattleground"`
- **Catalog `TaskFamily` claim:** `Bg`. Drives catalog row `bg.ab`
  in `Plan/Activities/00_INDEX.md` (Arathi Basin, 15v15, level
  20-60).

### AvObjectiveTask

- **Class declaration:** `BotRunner.Tasks.Battlegrounds.AvObjectiveTask`
  at `Exports/BotRunner/Tasks/Battlegrounds/AvObjectiveTask.cs`.
  Inherits `BotTask` and implements `IBotTask`. **Status:** partial
  (objective progression drafted; no role split between
  GY/tower/boss; raw `MoveToward` instead of pathfinding; combat
  rotation handled implicitly by the bot's normal combat task once
  in range). The catalog calls out a separate
  `BgBossEngagementTask` (Drek/Vann) — currently merged into the
  `AvState.PushToGeneral` branch of `AvObjectiveTask`.
- **Public surface — current shipped:**
  - `public AvObjectiveTask(IBotContext context, bool isHorde)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `public static readonly Dictionary<string, Position> HordeTargets`
    (Stonehearth Bunker, Icewing Bunker, Dun Baldar N/S Bunker,
    Stormpike Graveyard, Vanndar Stormpike).
  - `public static readonly Dictionary<string, Position> AllianceTargets`
    (Tower Point, Iceblood Tower, East/West Frostwolf Tower,
    Frostwolf Graveyard, Drek'Thar).
  - Private state machine `AvState` (`SelectObjective` →
    `MoveToObjective` → `AssaultObjective` → `DefendObjective` →
    `PushToGeneral` → `Complete`).
- **Public surface — target (Phase 1, after S1.0):**
  - `string Name { get; }` returns
    `"AvObjectiveTask:" + (isHorde ? "Horde" : "Alliance") + ":" + _targetName`.
  - `BotTaskStatus Status { get; }`.
  - `Task TickAsync(BotTaskContext context, CancellationToken ct)` —
    `banner.Interact()` becomes
    `await context.GameObjectClient.InteractAsync(banner.Guid, ct)`;
    move legs push `GoToTask` / `MountAndGoToTask` children to honor
    AV's outdoor scale (single objective leg is up to 1km).
  - `Task OnPushedAsync(BotTaskContext context, CancellationToken ct)` —
    subscribes to AV `SMSG_UPDATE_WORLD_STATE` for tower/GY
    control flags and general-room-flag triggers (used to gate
    `PushToGeneral`).
  - `Task OnPoppedAsync(BotTaskContext context, BotTaskStatus terminal)` —
    unsubscribes from world-state events.
  - `Task<bool> OnChildFailedAsync(BotTaskContext context, IBotTask child, string reason)` —
    a failed travel leg increments `_objectivesCompleted` to skip
    the unreachable objective; repeated failures escalate to
    `BotTaskStatus.Failed`.
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `player.Position`, `player.MapId`,
    `IObjectManager.GameObjects` (banner GO within 20y of the
    target objective), `IObjectManager.Player`. Phase 1 also reads
    AV-specific world-state fields from
    `BattlegroundNetworkClientComponent`.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.Position` (via `MoveToward`),
    `WoWActivitySnapshot.honorableKills` /
    `WoWActivitySnapshot.honorPoints` accumulate while in the BG
    (driven by server-side honor grants, not by the task), and the
    top-of-task-stack `currentTaskName` clears on
    `BotContext.BotTasks.Pop()` when `AvState.Complete`.
- **BG protocol footprint:**
  - `CMSG_GAMEOBJ_USE` — emitted indirectly by
    `IWoWGameObject.Interact()` on banner assault
    (`AvState.AssaultObjective`).
  - Standard BG movement opcode set as in the WSG/AB blocks.
  - General engagement (`AvState.PushToGeneral`) is handed off to
    the combat rotation, which sends `CMSG_CAST_SPELL` /
    `CMSG_ATTACKSWING` / `CMSG_SET_SELECTION`. Those opcodes are
    not owned by this task.
  - *Inbound* (read-only): `SMSG_UPDATE_WORLD_STATE` for
    tower-burn / GY-capture / boss-room flags;
    `SMSG_PVP_CREDIT` for honor on objective completion.
- **FG memory footprint:**
  - `IObjectManager.Player`, `IObjectManager.GameObjects`.
  - `IWoWGameObject.Interact()`.
  - `IObjectManager.MoveToward(Position)`,
    `IObjectManager.ForceStopImmediate()`.
  - No `LuaCall` invocations.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.Battlegrounds.AvObjectiveTests.AV_TowerAssault_HordeBurnsStonehearthBunker`
  at `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AvObjectiveTests.cs:61`;
  also `AV_GraveyardCapture_HordeCapturesSnowfallGraveyard`
  (line 72) and `AV_FullGame_HordeBurnsAllianceTowersAndKillsVanndar`
  (line 83) — the full-game test doubles as the
  `BgBossEngagementTask` anchor until that task is split out.
  Entry-leg coverage at
  `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs::AV_FullMatch_EnterPrepQueueMountAndReachObjective`
  (line 85).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~AvObjectiveTests.AV_TowerAssault_HordeBurnsStonehearthBunker"`
- **Catalog `TaskFamily` claim:** `Bg`. Drives catalog row `bg.av`
  in `Plan/Activities/00_INDEX.md` (Alterac Valley, 40v40, level
  51-60). AV is the smallest activity-roster scale gate — see the
  `SBG.av` slot note below.

## Coordinator: `BattlegroundCoordinator`

Existing at `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`.
Needs upgrade per S2.8.

Responsibilities:

- Maintain queue depth per BG per faction.
- Form a group when the queue reaches size threshold.
- Travel bots to battlemaster (uses `travel.md`).
- Dispatch `BattlegroundQueueTask`.
- On queue pop, dispatch BG-specific objective tasks.
- Track win/loss; honor accumulation logged.

## Slots

### SBG.wsg — Warsong Gulch (10v10)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `bg.wsg`
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Battleground/WsgFlagCapTask.cs`
  - `Bot/battlegrounds/wsg.json`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgTests.cs`
- **Objective:** Pick up flag in enemy flagroom, return to friendly
  flagroom, score. Defense: kill enemy flag carrier.

### SBG.ab — Arathi Basin (15v15)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `bg.ab`
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Battleground/AbNodeCapTask.cs`
  - `Bot/battlegrounds/ab.json`
- **Objective:** Capture and hold 3+ of 5 nodes (Farm, LM, BS, GM, Stables).
  Score on resource accumulation.

### SBG.av — Alterac Valley (40v40)

- **Owner:** `monorepo-worker`
- **Status:** not-started
- **Catalog:** `bg.av`
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Battleground/AvGyTowerTask.cs`
  - `Exports/BotRunner/Tasks/Battleground/AvBossEngagementTask.cs`
  - `Bot/battlegrounds/av.json`
- **Objective:** Capture GYs, burn enemy towers, kill enemy commanders,
  rush boss (Drek'Thar or Vanndar Stormpike).
- **Note:** AV is the largest BG and the smallest activity-roster
  scale gate. The scheduler must be able to fill 80 bots
  simultaneously (40 per faction) for at least one AV without
  degrading background progression.

### SBG.common.1 — Queue stabilization

- **Owner:** `monorepo-worker`
- **Status:** partial — `Tests/BotRunner.Tests/LiveValidation/BgPostTeleportStabilizationTests.cs`
- **Goal:** Bot enters BG, stabilizes (movement, target reset),
  proceeds to objective.

### SBG.common.2 — Honor tracking

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Snapshot tracks honor gain per match. `wwow.botrunner.honor_total{bracket}` metric.

### SBG.common.3 — Cross-faction BG fill

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Faction balance — coordinator ensures both factions
  fill within `BgFillSkew` (default 1).

## Failure recovery

- **Queue pop missed** → re-queue.
- **Match end without victory** → still credit honor, snapshot
  result, lease release.
- **Disconnect mid-match** → reconnect; bot rejoins via BG re-entry.
