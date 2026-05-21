# Activities — Social Systems

Group/raid invites, trades, whispers, channel chat, guild chat. Most
plumbing exists; this file enumerates the per-activity slots.

## Task families

| Task | Status |
|---|---|
| `GroupInviteTask` | done |
| `GroupAcceptTask` | done |
| `GroupDeclineTask` | done |
| `GroupLeaveTask` | done |
| `GroupKickTask` | done |
| `GroupPromoteLeaderTask` | done |
| `TradeTask` (6 actions, all need BG null guards) | partial — **see S2.12** |
| `WhisperTask` | done — used by Shodan whisper bridge |
| `ChannelJoinTask` | not-started — `ChannelNetworkClientComponent` missing |
| `GuildInviteTask` / `GuildAcceptTask` | partial |
| `MeetingStoneSummonTask` | not-started |

## Slots

### SS.1 — Trade null guards (also S2.12)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** All 6 trade actions on BG handle null TradeFrame without
  NullRef.

### SS.2 — Channel system

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/WoWSharpClient/Components/ChannelNetworkClientComponent.cs`
- **Goal:** Bots can join `Trade`, `LookingForGroup`, `General`,
  `LocalDefense`. Bots in `Trade` parse trade offers from human chat
  and forward to `EconomyCoordinator`.

### SS.3 — Meeting stone summon

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Social/MeetingStoneSummonTask.cs`
- **Goal:** Used by dungeon coordinator to assemble groups at the
  meeting stone outside an instance.

### SS.4 — LFG chat detection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Bot in `LookingForGroup` channel parses `LFG` /
  `LF1M tank` patterns, forwards to `ActivityScheduler` as a
  potential on-demand request.

### SS.5 — Guild lifecycle

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Bots auto-accept guild invites from `Shodan-Wolfheart`
  (production guild master) and can rebuild a guild roster on
  test scenarios.

## Failure recovery

- **Whisper unread** → re-send after `WhisperRetryMs`.
- **Group invite declined** → score-down the (inviter, target) pair
  for cool-down period.

## Task specifications

> Phase 0 / S0.8.10 precision blocks. One entry per task named in
> `Spec/03_BOTRUNNER.md#catalog-of-task-families`:
> `GroupInviteTask`, `GroupAcceptTask`, `GroupLeaveTask`, `TradeTask`,
> `WhisperTask`, `ChannelJoinTask`, `GuildInviteTask`. A Phase 1
> worker reading any block has enough to implement (or finish) the
> task without a separate investigation pass.
>
> **Interface drift note.** Social tasks are unusual in that today
> *most of them are not implemented as standalone `IBotTask` classes*.
> The action dispatch path in `Exports/BotRunner/ActionDispatcher.cs`
> turns each `CharacterAction.{SendGroupInvite, AcceptGroupInvite,
> DeclineGroupInvite, LeaveGroup, DisbandGroup, OfferTrade,
> AcceptTrade, DeclineTrade, SendChat}` into a one-shot
> `IBehaviourTreeNode` sequence built by
> `Exports/BotRunner/SequenceBuilders/InteractionSequenceBuilder.cs`
> (no task stack push). The current shipped `IBotTask` social
> classes are limited to
> `BotRunner.Tasks.Social.ChannelAutoJoinTask`
> (`Exports/BotRunner/Tasks/Social/ChannelAutoJoinTask.cs`) and
> the non-task helper `BotRunner.Tasks.Social.WhisperTracker`
> (`Exports/BotRunner/Tasks/Social/WhisperTracker.cs`). The Phase 1
> target — per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`
> — is for each named task below to become a real `IBotTask` with
> `TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync`. Slot **S1.0** lands the new interface and
> this slot's per-task blocks below report **current code** plus the
> planned anchor file the Phase 1 worker creates.
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by
> `Exports/BotRunner/SnapshotBuilder.cs` from `IObjectManager` state +
> the top of the task stack. "Reads" lists the snapshot fields the
> task is expected to consume (via the equivalent `IObjectManager`
> property today). "Writes" lists the snapshot fields whose value
> changes as a *side effect* of the task running (so tests poll the
> right field). The load-bearing social snapshot fields live at
> `Exports/BotCommLayer/Models/ProtoDef/communication.proto`:
> `WoWActivitySnapshot.partyLeaderGuid` (line 221),
> `WoWActivitySnapshot.recentChatMessages` (line 222),
> `WoWActivitySnapshot.recentErrors` (line 223), and the
> `desired_party_leader_name` / `desired_party_members` planning
> fields (lines 235-236).
>
> **Shodan policy.** Per `Spec/13_TESTING.md#shodan-rules`, social
> tests must never dispatch `ObjectiveType.*` against the Shodan account
> and never assert on Shodan's snapshot for behavior validation.
> `LiveBotFixture.ResolveBotRunnerActionTargets()` resolves the
> non-Shodan test bots; `TradeTestSupport.ResolvePair` does the
> equivalent for the trade scenarios.

### GroupInviteTask

- **Class declaration:** No dedicated `IBotTask` class today.
  Dispatched as a behaviour-tree sequence:
  `InteractionSequenceBuilder.BuildSendGroupInviteSequence(ulong)` and
  `InteractionSequenceBuilder.BuildSendGroupInviteByNameSequence(string)`
  at
  `Exports/BotRunner/SequenceBuilders/InteractionSequenceBuilder.cs:688`
  and `:699`, routed from
  `ActionDispatcher.cs:506` (`CharacterAction.SendGroupInvite`).
  **Planned anchor:**
  `Exports/BotRunner/Tasks/Social/GroupInviteTask.cs`.
  **Status:** done (sequence form).
- **Public surface — current shipped:**
  - `IBehaviourTreeNode InteractionSequenceBuilder.BuildSendGroupInviteSequence(ulong playerGuid)`
  - `IBehaviourTreeNode InteractionSequenceBuilder.BuildSendGroupInviteByNameSequence(string playerName)`
  - Underlying `IObjectManager` entry points:
    - `void IObjectManager.InviteToGroup(ulong guid)`
    - `void IObjectManager.InviteByName(string characterName)`
  - Underlying BG network agent:
    `IPartyNetworkClientComponent.InvitePlayerAsync(string playerName)`
    (`Exports/WoWSharpClient/Networking/ClientComponents/PartyNetworkClientComponent.cs:183`).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`:
  `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
  `OnChildFailedAsync`. Constructor takes
  `(BotTaskContext, string targetName)` or
  `(BotTaskContext, ulong targetGuid)`.
- **Snapshot contract:**
  - *Reads:*
    `WoWActivitySnapshot.partyLeaderGuid` (via
    `IObjectManager.PartyLeaderGuid` /
    `IObjectManager.PartyMembers`) to gate "already grouped";
    `WoWActivitySnapshot.player.Unit.GameObject.Base.Guid` for
    self-identity (avoid inviting self).
  - *Writes/mutates (observable via snapshot):*
    after the invitee accepts,
    `WoWActivitySnapshot.partyLeaderGuid` becomes non-zero and
    matches the leader; transient `recentChatMessages` entries from
    SMSG_PARTY_COMMAND_RESULT (e.g. "X is already in a group").
- **BG protocol footprint:**
  `Opcode.CMSG_GROUP_INVITE` (sent by
  `PartyNetworkClientComponent.InvitePlayerAsync` at
  `PartyNetworkClientComponent.cs:183`).
- **FG calls:**
  - `IObjectManager.PartyMembers` (read).
  - `IObjectManager.Players` (lookup by name/guid for the FG GUID
    path).
  - `IObjectManager.InviteByName(string)` → Lua
    `InviteByName('<name>')` via
    `ObjectManager.MainThreadLuaCall` at
    `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs:551`.
  - `IObjectManager.InviteToGroup(ulong)` → Lua
    `InviteByName('<player.Name>')` after GUID→name resolution
    (`ObjectManager.Interaction.cs:540`).
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.GroupFormationTests.GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp`
  at
  `Tests/BotRunner.Tests/LiveValidation/GroupFormationTests.cs:38`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~GroupFormationTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Underlies every
  multi-bot row in `Plan/Activities/00_INDEX.md` — every dungeon
  (`dungeon.*`), raid (`raid.*`), and battleground (`bg.*`) row in
  the catalog requires `GroupInviteTask` to form the party/raid
  before the rest of the activity stack runs.

### GroupAcceptTask

- **Class declaration:** No dedicated `IBotTask` class today.
  Dispatched as a behaviour-tree sequence:
  `InteractionSequenceBuilder.AcceptGroupInviteSequence` at
  `Exports/BotRunner/SequenceBuilders/InteractionSequenceBuilder.cs:719`,
  routed from
  `ActionDispatcher.cs:512` (`CharacterAction.AcceptGroupInvite`).
  Auto-accept polling for FG bots also lives in
  `Services/ForegroundBotRunner/Grouping/GroupManager.cs:216`
  (`CheckAndAcceptInvite`).
  **Planned anchor:**
  `Exports/BotRunner/Tasks/Social/GroupAcceptTask.cs`.
  **Status:** done (sequence form).
- **Public surface — current shipped:**
  - `IBehaviourTreeNode InteractionSequenceBuilder.AcceptGroupInviteSequence` — polls
    `factory.PartyAgent.HasPendingInvite`/`AcceptInviteAsync()` on
    BG, falls back to `IObjectManager.HasPendingGroupInvite()` +
    `IObjectManager.AcceptGroupInvite()` on FG, ~10s/100-poll
    timeout.
  - `IBehaviourTreeNode InteractionSequenceBuilder.DeclineGroupInviteSequence`
    (`:761`) for the decline variant routed from
    `ActionDispatcher.cs:515`.
  - Underlying `IObjectManager` entry points:
    - `bool IObjectManager.HasPendingGroupInvite()`
    - `void IObjectManager.AcceptGroupInvite()`
    - `void IObjectManager.DeclineGroupInvite()`
  - Underlying BG network agent:
    `IPartyNetworkClientComponent.AcceptInviteAsync()` and
    `DeclineInviteAsync()`
    (`PartyNetworkClientComponent.cs:204` / `:222`).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`:
  `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
  `OnChildFailedAsync`. Constructor takes `(BotTaskContext)` with
  an optional `TimeSpan acceptTimeout` (default 10s mirrors current
  poll budget).
- **Snapshot contract:**
  - *Reads:*
    `WoWActivitySnapshot.partyLeaderGuid` (must be `0` before
    accepting; see
    `GroupFormationTests.EnsureNotGroupedAsync` at
    `GroupFormationTests.cs:122`). `IPartyAgent.HasPendingInvite` is
    the BG-side gate that has no current snapshot projection.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.partyLeaderGuid` flips to the inviter's
    GUID on success (asserted in `GroupFormationTests.cs:161`);
    `recentChatMessages` may surface SMSG_PARTY_COMMAND_RESULT
    text on failure modes (decline, already grouped).
- **BG protocol footprint:**
  `Opcode.CMSG_GROUP_ACCEPT` (sent by
  `PartyNetworkClientComponent.AcceptInviteAsync` at
  `PartyNetworkClientComponent.cs:204`) and
  `Opcode.CMSG_GROUP_DECLINE` (`:222`) for the decline variant.
- **FG calls:**
  - `IObjectManager.HasPendingGroupInvite()` (Lua
    `StaticPopup1:IsVisible() and StaticPopup1.which == 'PARTY_INVITE'`
    via `MainThreadLuaCallWithResult`,
    `ObjectManager.Interaction.cs:583`).
  - `IObjectManager.AcceptGroupInvite()` (Lua
    `StaticPopup1Button1:Click()` + `AcceptGroup()`,
    `ObjectManager.Interaction.cs:137-138`).
  - `IObjectManager.DeclineGroupInvite()` (Lua `DeclineGroup()` +
    `StaticPopup1Button2:Click()`,
    `ObjectManager.Interaction.cs:567-568`).
  - `Functions.LuaCallWithResult` polling inside
    `GroupManager.CheckAndAcceptInvite()`
    (`GroupManager.cs:221`) for FG auto-accept.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.GroupFormationTests.GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp`
  at
  `Tests/BotRunner.Tests/LiveValidation/GroupFormationTests.cs:38`
  (covers invite + accept + cleanup in a single test).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~GroupFormationTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Required by every
  multi-bot catalog row (every `dungeon.*`, `raid.*`, `bg.*`) —
  the invitee bot runs this task to enter the party.

### GroupLeaveTask

- **Class declaration:** No dedicated `IBotTask` class today.
  Dispatched as two sequences:
  `InteractionSequenceBuilder.LeaveGroupSequence` at
  `Exports/BotRunner/SequenceBuilders/InteractionSequenceBuilder.cs:783`
  (member leave) and
  `InteractionSequenceBuilder.DisbandGroupSequence` at `:809`
  (leader disband). Routed from
  `ActionDispatcher.cs:521` (`CharacterAction.LeaveGroup`) and
  `:524` (`CharacterAction.DisbandGroup`).
  **Planned anchor:**
  `Exports/BotRunner/Tasks/Social/GroupLeaveTask.cs`.
  **Status:** done (sequence form).
- **Public surface — current shipped:**
  - `IBehaviourTreeNode InteractionSequenceBuilder.LeaveGroupSequence`
    (`:783`) — calls
    `factory.PartyAgent.LeaveGroupAsync()` on BG, falls back to
    `IObjectManager.LeaveGroup()` on FG, no-op when
    `PartyLeaderGuid == 0`.
  - `IBehaviourTreeNode InteractionSequenceBuilder.DisbandGroupSequence`
    (`:809`) — gated on
    `factory.PartyAgent.IsGroupLeader` /
    `Player.Guid == PartyLeaderGuid`, then
    `factory.PartyAgent.DisbandGroupAsync()` on BG or
    `IObjectManager.DisbandGroup()` on FG.
  - Underlying `IObjectManager` entry points:
    - `void IObjectManager.LeaveGroup()`
      (`IObjectManager.cs:163`)
    - `void IObjectManager.DisbandGroup()`
      (`IObjectManager.cs:164`)
  - Underlying BG network agent:
    `IPartyNetworkClientComponent.LeaveGroupAsync()` and
    `DisbandGroupAsync()`
    (`PartyNetworkClientComponent.cs:303` / `:325`).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`.
  Constructor takes `(BotTaskContext)` with a
  `GroupExitMode mode = Auto` selecting `LeaveGroup` vs
  `DisbandGroup` based on `PartyLeaderGuid == Player.Guid`.
- **Snapshot contract:**
  - *Reads:*
    `WoWActivitySnapshot.partyLeaderGuid` (must be non-zero to do
    anything; `==` self GUID picks the disband path — see
    `GroupFormationTests.EnsureNotGroupedAsync` at
    `GroupFormationTests.cs:135-137`).
    `WoWActivitySnapshot.player.Unit.GameObject.Base.Guid` for
    self-identity.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.partyLeaderGuid` returns to `0` after
    SMSG_GROUP_LIST/SMSG_PARTY_MEMBER_STATS clears the cached
    roster (asserted in `GroupFormationTests.cs:118-119`).
- **BG protocol footprint:**
  `Opcode.CMSG_GROUP_DISBAND` (sent by both
  `PartyNetworkClientComponent.LeaveGroupAsync` at
  `PartyNetworkClientComponent.cs:303` and `DisbandGroupAsync` at
  `:325` — vanilla treats both as `CMSG_GROUP_DISBAND`).
  `Opcode.CMSG_GROUP_UNINVITE` /
  `Opcode.CMSG_GROUP_UNINVITE_GUID` (`:266` / `:286`) are the
  related kick path that backs `BuildKickPlayerSequence` /
  `CharacterAction.KickPlayer`.
- **FG calls:**
  - `IObjectManager.LeaveGroup()` → Lua `LeaveParty()` via
    `MainThreadLuaCall`
    (`ObjectManager.Interaction.cs:309`).
  - `IObjectManager.DisbandGroup()` → Lua `LeaveParty()` (vanilla
    leader-leaves-disbands semantics,
    `ObjectManager.Interaction.cs:576`).
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.GroupFormationTests.GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp`
  at `Tests/BotRunner.Tests/LiveValidation/GroupFormationTests.cs:38`
  exercises both modes via `EnsureNotGroupedAsync`
  (`GroupFormationTests.cs:122`).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~GroupFormationTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Required by every
  multi-bot catalog row that ever ends — leaving the party after
  every `dungeon.*` / `raid.*` / `bg.*` completion is the cleanup
  step before the next assigned activity.

### TradeTask

- **Class declaration:** No single `IBotTask` class today; the
  family is a 6-action group dispatched directly through
  `Exports/BotRunner/ActionDispatcher.cs:428..477`
  (cases `CharacterAction.OfferTrade`, `OfferGold`, `OfferItem`,
  `AcceptTrade`, `DeclineTrade`, `EnchantTrade`, `LockpickTrade`).
  **Planned anchor:** `Exports/BotRunner/Tasks/Social/TradeTask.cs`.
  **Status:** partial — see slot **SS.1** / **S2.12**.
  **R19 / S2.12 hardening required.** All 6 BG trade actions on the
  shipped path go through `IObjectManager` async wrappers
  (`InitiateTradeAsync`, `SetTradeGoldAsync`, `SetTradeItemAsync`,
  `AcceptTradeAsync`, `CancelTradeAsync`, plus the enchant/lockpick
  sequences) that, on FG, dereference `_fgTradeFrame` via
  `WaitForTradeFrameAsync`/`WaitForTradeUiAsync`
  (`Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs:1374-1480`)
  — but on BG the symmetric wrappers call into
  `TradeNetworkClientComponent` and **none of the six dispatch
  branches null-guard the trade frame/component before
  invocation**. The slot brief flags this as the highest-priority
  trade hardening item; the planned `TradeTask` should bundle the
  six actions plus the null-guards into a single stack-pushable
  task that owns its own retry/timeout policy. Spec listing in
  `Spec/03_BOTRUNNER.md` lines 137-139 confirms the gap.
- **Public surface — current shipped:**
  - `Task IObjectManager.InitiateTradeAsync(ulong playerGuid, CancellationToken ct = default)`
    (`IObjectManager.cs:347`; FG impl at
    `ObjectManager.Interaction.cs:1374`).
  - `Task IObjectManager.SetTradeGoldAsync(uint copper, CancellationToken ct = default)`
    (`:348`; `:1394`).
  - `Task IObjectManager.SetTradeItemAsync(byte tradeSlot, byte bagId, byte slotId, CancellationToken ct = default)`
    (`:349`; `:1414`).
  - `Task IObjectManager.AcceptTradeAsync(CancellationToken ct = default)`
    (`:350`; `:1442`).
  - `Task IObjectManager.CancelTradeAsync(CancellationToken ct = default)`
    (`:351`; `:1454`).
  - `IBehaviourTreeNode InteractionSequenceBuilder.BuildOfferEnchantSequence(int)`
    and `OfferLockpickSequence` for the two non-async trade
    actions (`ActionDispatcher.cs:474` / `:477`).
  - Underlying BG network component:
    `Exports/WoWSharpClient/Networking/ClientComponents/TradeNetworkClientComponent.cs`
    (CMSG opcodes at lines 288/297, 314-334, 369-375).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`.
  Constructor takes
  `(BotTaskContext, ulong partnerGuid, TradeOffer offer)` where
  `TradeOffer` bundles gold + per-slot item bag/slot identifiers.
  Task owns the lifecycle (initiate → wait open → offer →
  accept/decline) so a single push covers the 6-action sequence
  without exposing the intermediate `CharacterAction.*` to the
  caller.
- **Snapshot contract:**
  - *Reads:*
    `IObjectManager.TradeFrame` (no current snapshot projection;
    Phase 1 work should surface a `WoWActivitySnapshot.tradeStatus`
    field — see `Plan/QUESTIONS.md` for the open follow-up).
    `WoWActivitySnapshot.player.Unit.GameObject.Base.Guid` for
    self-identity. Inventory bag/slot identifiers via the
    snapshot's inventory sub-message (existing).
  - *Writes/mutates (observable via snapshot):*
    coinage and inventory deltas on the receiver bot after a
    successful trade (asserted in
    `TradeTestSupport.RunGoldAndItemTransferScenarioAsync` via
    `metrics.ReceiverCoinageAfter` and
    `metrics.ReceiverItemCountAfter` — see
    `TradingTests.cs:62-64`).
    `WoWActivitySnapshot.recentChatMessages` may surface
    SMSG_TRADE_STATUS error text on cancel/timeout.
- **BG protocol footprint:**
  - `Opcode.CMSG_INITIATE_TRADE`
    (`TradeNetworkClientComponent.cs:288/297`) — sent by
    `InitiateTradeAsync(ulong)`.
  - `Opcode.CMSG_BEGIN_TRADE`
    (`TradeNetworkClientComponent.cs:325`) — receiver-side accept
    of the pending invitation, sent by `AcceptTradeAsync` when
    trade window is not yet open.
  - `Opcode.CMSG_ACCEPT_TRADE`
    (`TradeNetworkClientComponent.cs:334`) — final accept once
    both sides have offered, sent by `AcceptTradeAsync`.
  - `Opcode.CMSG_CANCEL_TRADE`
    (`TradeNetworkClientComponent.cs:369/375`) — sent by
    `CancelTradeAsync`.
- **FG calls:**
  - `IObjectManager.SetTarget(ulong)` then Lua
    `if UnitExists('target') and UnitIsPlayer('target') then InitiateTrade('target') end`
    via `MainThreadLuaCall`
    (`ObjectManager.Interaction.cs:1389-1390`).
  - `IObjectManager.TradeFrame` /
    `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs` —
    `OfferMoney(int)`, `OfferItem(byte bagId, byte slotId, int quantity, int tradeWindowSlot)`,
    `AcceptTrade()`, `DeclineTrade()`.
  - Confirmation pollers `WaitForOwnTradeMoneyAsync` /
    `WaitForOwnTradeItemAsync` /
    `WaitForTradeFrameAsync` /
    `WaitForTradeUiAsync`
    (`ObjectManager.Interaction.cs:1402-1480`).
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.TradingTests.Trade_InitiateAndCancel_BothBotsSeeCancellation`
  at `Tests/BotRunner.Tests/LiveValidation/TradingTests.cs:27`;
  full transfer coverage in
  `TradingTests.Trade_GoldAndItem_TransferSuccessful` (`:42`,
  currently skipped on a tracked server-side gap) and the
  FG/BG-parity-flipped pair in
  `TradeParityTests.Trade_InitiateCancel_FgBgParity`
  (`Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs:27`)
  and `Trade_GoldAndItem_FgBgParity` (`:42`). Shared scenario
  driver: `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Underlies cross-bot
  material handoffs that feed every multi-bot row in
  `Plan/Activities/00_INDEX.md` — most directly the economy
  rows (`econ.ah-restock`, `econ.vendor-loop`) when bots exchange
  enchants/mats outside the AH, and the dungeon/raid rows
  (`dungeon.*`, `raid.*`) when group members trade BoP loot
  inside the 2-hour bind window. Also load-bearing for
  `BotRunner.Tasks.Crafting.BatchCraftTask` enchant handoffs (FG
  enchanter applies, then trade-window-pass-back).

### WhisperTask

- **Class declaration:** No dedicated `IBotTask` class today.
  Whisper *send* is routed through `CharacterAction.SendChat`
  (`Exports/BotRunner/ActionDispatcher.cs:826`) which calls
  `BotRunnerService.SendChatThroughBestAvailablePath` (`:20-31`)
  and ultimately
  `IObjectManager.SendChatMessage(string)` — the BG path goes
  through
  `Exports/WoWSharpClient/Networking/ClientComponents/ChatNetworkClientComponent.cs:270`
  (`WhisperAsync(playerName, message, language, ct)`).
  Whisper *receive* + history is owned by the standalone helper
  `BotRunner.Tasks.Social.WhisperTracker` at
  `Exports/BotRunner/Tasks/Social/WhisperTracker.cs` (class declaration line 13;
  this is a pure data structure, not an `IBotTask`).
  **Planned anchor:**
  `Exports/BotRunner/Tasks/Social/WhisperTask.cs`.
  **Status:** done — used by the Shodan whisper bridge.
- **Public surface — current shipped:**
  - `void IObjectManager.SendChatMessage(string message)` (general
    chat dispatch, picks whisper based on `/w <name>` prefix
    parsing inside `ChatNetworkClientComponent`).
  - `Task IChatNetworkClientComponent.WhisperAsync(string playerName, string message, Language language = Language.Common, CancellationToken ct = default)`
    (`ChatNetworkClientComponent.cs:270`).
  - `WhisperTracker` instance API
    (`Exports/BotRunner/Tasks/Social/WhisperTracker.cs`):
    - `WhisperTracker(int maxMessagesPerPlayer = 10)` (ctor, line 20).
    - `void RecordIncoming(string senderName, string text)` (line 26).
    - `void RecordOutgoing(string recipientName, string text)` (line 32).
    - `IReadOnlyList<WhisperMessage> GetHistory(string playerName)` (line 52).
    - `IReadOnlyList<string> GetActiveConversations()` (line 58).
    - `WhisperMessage? GetOldestUnrespondedWhisper()` (line 64).
    - `bool HasUnreadWhispers()` (line 76).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`.
  Constructor takes
  `(BotTaskContext, string recipient, string message)`; task
  pushes a single `WhisperAsync` send, records into the
  `WhisperTracker` on success, and pops. The auto-reply / Shodan
  bridge wraps `WhisperTask` instances inside a long-running
  `BotRunner.Tasks.Social.WhisperResponderTask` (also planned —
  out of scope for S0.8.10).
- **Snapshot contract:**
  - *Reads:*
    `WoWActivitySnapshot.recentChatMessages` (incoming whisper
    body, filtered to `CHAT_MSG_WHISPER` upstream of the tracker —
    see `WhisperTracker.cs:13` doc comment).
    `WoWActivitySnapshot.player.Unit.GameObject.Base.Guid` for
    self-identity (drop self-whispers).
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.recentChatMessages` gains the outgoing
    whisper text on the sender bot (the
    `BotRunnerService.SnapshotBuilder` enqueues outgoing chat
    through `EnqueueDiagnosticMessage` /
    `[ACTION-PLAN]`-style entries — see
    `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs:233`
    for the empty-baseline assertion and `:245` for the
    "[SYSTEM] …" round-trip pattern).
- **BG protocol footprint:**
  `Opcode.CMSG_MESSAGECHAT` with
  `ChatMsg.CHAT_MSG_WHISPER` payload (built inside
  `ChatNetworkClientComponent.WhisperAsync` →
  `WorldClient.SendChatMessageAsync(ChatMsg, Language, destination, message, ct)`
  at `ChatNetworkClientComponent.cs:236`, dispatched as
  `CMSG_MESSAGECHAT` per
  `Exports/WoWSharpClient/Networking/ClientComponents/README.md:255`).
- **FG calls:**
  - `IObjectManager.SendChatMessage(string)` —
    `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
    routes whispers as `/w <name> <text>` strings to Lua
    `SendChatMessage(text, "WHISPER", language, target)` (the
    GM-chat helper path inside
    `SendChatThroughBestAvailablePath` keeps `.gm`/`.tele` etc.
    out of the wire).
  - Whisper *receive* on FG arrives via the same
    `WoWEventHandler` `CHAT_MSG_WHISPER` filter that fans into the
    snapshot's `recentChatMessages` field.
- **Test anchor:**
  - Unit: `BotRunner.Tests.Social.WhisperTrackerTests` at
    `Tests/BotRunner.Tests/Social/WhisperTrackerTests.cs`
    (9 `[Fact]` methods, `RecordIncoming_Stores` through
    `GetHistory_ReturnsEmpty_ForUnknownPlayer`).
  - Live: closest dual-bot live coverage is
    `BotRunner.Tests.LiveValidation.ChannelTests.Channel_SendMessage_OtherBotReceives`
    at
    `Tests/BotRunner.Tests/LiveValidation/ChannelTests.cs:32`
    (asserts cross-bot chat propagation through
    `RecentChatMessages`); a dedicated whisper test is not yet in
    the LiveValidation suite. **Planned anchor:**
    `Tests/BotRunner.Tests/LiveValidation/WhisperTests.cs::WhisperRoundTrip_RecipientSeesMessage`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~WhisperTrackerTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Required by every
  multi-bot row in `Plan/Activities/00_INDEX.md` indirectly
  (coordinator-to-bot status pings, Shodan whisper bridge for GM
  liaison), and load-bearing for the SS.4 LFG chat detection
  pipeline that feeds `ActivityScheduler`. Per
  `Spec/13_TESTING.md#shodan-rules`, Shodan-directed whisper
  scenarios stage real BotRunner targets via
  `LiveBotFixture.ResolveBotRunnerActionTargets()` and never
  assert on Shodan's snapshot.

### ChannelJoinTask

- **Class declaration:**
  `BotRunner.Tasks.Social.ChannelAutoJoinTask` at
  `Exports/BotRunner/Tasks/Social/ChannelAutoJoinTask.cs` (class
  declaration line 13). Inherits `BotTask` and implements
  `IBotTask` (S1.0 shim: `TickAsync` → `OnTick` → legacy `Update()`).
  **Status:** not-started for the full target —
  `ChannelNetworkClientComponent.JoinChannel()` is referenced in
  the comment at line 55 but the actual call site is a `//
  Actual join via ChannelNetworkClientComponent.JoinChannel()`
  placeholder. Per slot **SS.2**, the BG component itself is the
  missing piece called out as
  `Exports/WoWSharpClient/Components/ChannelNetworkClientComponent.cs`.
  The component **does** in fact exist at
  `Exports/WoWSharpClient/Networking/ClientComponents/ChannelNetworkClientComponent.cs`
  and exposes `JoinChannelAsync`, `LeaveChannelAsync`, and
  `SendChannelMessageAsync` (see lines 102, 153, 191 there); the
  Phase 1 fix is to wire the placeholder line in
  `ChannelAutoJoinTask.Update()` to that component.
- **Public surface — current shipped:**
  - `ChannelAutoJoinTask(IBotContext context, IReadOnlyList<string>? channels = null)`
    (ctor, line 34).
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); `Update()` body (line 39) is a `JoinChannels` → `Complete` state machine that increments `_joinIndex` and currently only logs. Per-family async refactor in S1.12 Social (S1.0/R25, shim-only).
  - `static readonly List<string> DefaultChannels`
    (line 21 — `General`, `Trade`, `LocalDefense`,
    `LookingForGroup`).
  - `static readonly HashSet<string> CityOnlyChannels` (line 30 —
    just `Trade`).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`.
  Constructor remains
  `(BotTaskContext, IReadOnlyList<string>? channels = null)`;
  the `TickAsync` body finally invokes
  `IChannelNetworkClientComponent.JoinChannelAsync(name, password: "", ct)`
  for each channel in `_channels`, awaits a
  `ChannelJoined` notification from the component's
  `ChannelNotified` observable (`ChannelNetworkClientComponent.cs:91`)
  before moving to the next index, and pops on completion.
- **Snapshot contract:**
  - *Reads:*
    `WoWActivitySnapshot.player` map/zone identifiers (gate
    `Trade` on `CityOnlyChannels` — only join in major cities;
    today `Update()` checks `ObjectManager.Player == null` but
    not zone, so the city-only filter is not wired).
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.recentChatMessages` gains the
    SMSG_CHANNEL_NOTIFY join confirmations
    (`ChannelNotification` observable surfaces these). The
    snapshot does not currently project the joined-channels list
    — Phase 1 work should add a `WoWActivitySnapshot.joinedChannels`
    repeated field (open follow-up; flag in `Plan/QUESTIONS.md`
    when S1.0 starts).
- **BG protocol footprint:**
  - `Opcode.CMSG_JOIN_CHANNEL`
    (`ChannelNetworkClientComponent.cs:134`).
  - `Opcode.CMSG_LEAVE_CHANNEL`
    (`ChannelNetworkClientComponent.cs:170`).
  - `Opcode.CMSG_MESSAGECHAT` with
    `ChatMsg.CHAT_MSG_CHANNEL` for the send path
    (`ChannelNetworkClientComponent.cs:218`).
  - The same opcodes also flow through
    `ChatNetworkClientComponent.cs:333` /
    `:369` for the lower-level chat surface.
- **FG calls:**
  - On FG today there is no `IObjectManager` method for channel
    join; the Phase 1 implementation reuses the BG component
    (FG bots already construct an `AgentFactory` —
    `Exports/WoWSharpClient/Networking/ClientComponents/AgentFactory.cs`).
    No `MainThreadLuaCall` is needed because join/leave do not
    require a UI frame.
  - Receive-side notifications fan in through
    `WoWEventHandler` `CHAT_MSG_CHANNEL` /
    `CHAT_MSG_CHANNEL_NOTICE` events into
    `recentChatMessages`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.ChannelTests.Channel_SendMessage_OtherBotReceives`
  at
  `Tests/BotRunner.Tests/LiveValidation/ChannelTests.cs:32` —
  current test exercises chat propagation via
  `ObjectiveType.SendChat` and asserts on
  `WoWActivitySnapshot.RecentChatMessages`. A dedicated
  channel-join assertion is not yet present. **Planned anchor:**
  `Tests/BotRunner.Tests/LiveValidation/ChannelTests.cs::Channel_AutoJoin_DefaultChannelsJoined`
  (covers SS.2 acceptance).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~ChannelTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Required by every
  multi-bot row indirectly — `LookingForGroup` channel join is
  the precondition for slot SS.4's LFG chat detection that feeds
  the dungeon/raid rows in `Plan/Activities/00_INDEX.md`, and
  `Trade` channel join feeds the `EconomyCoordinator` market
  signal per slot SS.2's goal.

### GuildInviteTask

- **Class declaration:** No dedicated `IBotTask` class today.
  Guild invite/accept is *not* currently wired through the
  `ActionDispatcher` or `InteractionSequenceBuilder`; live tests
  drive it through GM commands (`.guild create`, `.guild invite`,
  `.guild delete` via
  `LiveBotFixture.SendGmChatCommandAsync` — see
  `GuildOperationTests.cs:96` and `:120`). The BG component does
  expose the real CMSG opcodes:
  `IGuildNetworkClientComponent.InvitePlayerToGuildAsync` and
  `.AcceptGuildInviteAsync` /
  `.DeclineGuildInviteAsync`
  (`Exports/WoWSharpClient/Networking/ClientComponents/GuildNetworkClientComponent.cs:154-156`).
  **Planned anchor:**
  `Exports/BotRunner/Tasks/Social/GuildInviteTask.cs` (sends the
  invite) and `Exports/BotRunner/Tasks/Social/GuildAcceptTask.cs`
  (recipient accept). Slot **SS.5** owns wiring the auto-accept
  policy.
  **Status:** partial — BG opcodes ready, no behaviour-tree
  dispatch yet.
- **Public surface — current shipped:**
  - `Task IGuildNetworkClientComponent.InvitePlayerToGuildAsync(string playerName, CancellationToken ct = default)`
    (`GuildNetworkClientComponent.cs:156`).
  - `Task IGuildNetworkClientComponent.AcceptGuildInviteAsync(CancellationToken ct = default)`
    (`:154`).
  - `Task IGuildNetworkClientComponent.DeclineGuildInviteAsync(CancellationToken ct = default)`
    (`:155`).
  - `Task IGuildNetworkClientComponent.LeaveGuildAsync` /
    `DisbandGuildAsync` / `CreateGuildAsync` / `RemovePlayerFromGuildAsync` /
    `PromoteGuildMemberAsync` / `DemoteGuildMemberAsync` /
    `SetGuildMOTDAsync` / `SetGuildInfoAsync` (`:157..164`) for
    the rest of the lifecycle.
  - State properties:
    `IsInGuild`, `CurrentGuildId`, `CurrentGuildName`
    (`:146-148`).
  - Observable: `IObservable<IReadOnlyList<GuildMember>> GuildRosters`
    (`:140`).
  - `ActionDispatcher` integration: **none today** — no
    `CharacterAction.SendGuildInvite` /
    `CharacterAction.AcceptGuildInvite`. Phase 1 work must add
    these enum values, the proto `Communication.ObjectiveType.*`
    values, the `BotRunnerService.ActionMapping.cs` mapping rows,
    and the dispatch branches per
    `Spec/03_BOTRUNNER.md#actionmessage-dispatch` (5-step
    "Adding a new action type" checklist).
- **Public surface — target (Phase 1):** the four `IBotTask`
  overrides per `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`.
  Constructors:
  - `GuildInviteTask(BotTaskContext, string targetCharacterName)`
  - `GuildAcceptTask(BotTaskContext)` — polls for pending invite
    notification then sends `CMSG_GUILD_ACCEPT`.
- **Snapshot contract:**
  - *Reads:*
    `IGuildNetworkClientComponent.IsInGuild` /
    `CurrentGuildName` (no current snapshot projection —
    Phase 1 work should surface
    `WoWActivitySnapshot.guildName` / `guildMembers` to make the
    "auto-accept from Shodan-Wolfheart" policy at slot SS.5
    testable. Today `GuildOperationTests.cs:137-138` only
    asserts on `IsObjectManagerValid` as a soft survives-the-RTT
    proof). `WoWActivitySnapshot.recentChatMessages` for
    SMSG_GUILD_EVENT text capture.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.recentChatMessages` gains the guild
    invite/accept system text (asserted indirectly in
    `GuildOperationTests.cs:109-113` /
    `:143-147` via case-insensitive "guild" substring scan).
- **BG protocol footprint:**
  - `Opcode.CMSG_GUILD_INVITE`
    (`GuildNetworkClientComponent.cs:156`).
  - `Opcode.CMSG_GUILD_ACCEPT`
    (`GuildNetworkClientComponent.cs:154`).
  - `Opcode.CMSG_GUILD_DECLINE`
    (`:155`).
  - `Opcode.CMSG_GUILD_LEAVE` / `CMSG_GUILD_DISBAND` /
    `CMSG_GUILD_CREATE` / `CMSG_GUILD_REMOVE` /
    `CMSG_GUILD_PROMOTE` / `CMSG_GUILD_DEMOTE` /
    `CMSG_GUILD_MOTD` / `CMSG_GUILD_INFO_TEXT` /
    `CMSG_GUILD_ROSTER` / `CMSG_GUILD_INFO` (`:157..167`) for
    the wider guild lifecycle.
- **FG calls:**
  - No `IObjectManager` member exists for guild invite/accept
    today; FG bots fall through to the BG component the same
    way `AcceptGroupInviteSequence` already does (via
    `_agentFactoryAccessor` in
    `InteractionSequenceBuilder.cs:729`). No Lua call required —
    `AcceptGuild`/`DeclineGuild` Lua APIs exist in vanilla but
    the BG component is the canonical path because guild invite
    arrives as `SMSG_GUILD_INVITE` and the response is a single
    empty-payload opcode.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.GuildOperationTests.Guild_CreateAndInvite_RosterShowsBothMembers`
  at
  `Tests/BotRunner.Tests/LiveValidation/GuildOperationTests.cs:35`.
  Today the test stages everything via GM commands and asserts
  on `IsObjectManagerValid` plus chat-log substring matches;
  once SS.5 + Phase 1 land the BotRunner-dispatched path, the
  same test method should switch to
  `ObjectiveType.SendGuildInvite` /
  `ObjectiveType.AcceptGuildInvite` and assert on
  `WoWActivitySnapshot.guildName`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~GuildOperationTests" --configuration Release`
- **Catalog `TaskFamily` claim:** `Social`. Underlies every
  multi-bot social-cohesion scenario in
  `Plan/Activities/00_INDEX.md`: the production guild
  `Shodan-Wolfheart` is the parent group for all production
  bots (per slot SS.5), and the dungeon (`dungeon.*`) / raid
  (`raid.*`) catalog rows lean on guild rosters for repeat-fill
  group formation when the LFG channel (slot SS.4) misses.
  Per `Spec/13_TESTING.md#shodan-rules` guild tests must dispatch
  against the resolved BotRunner targets, never Shodan.
