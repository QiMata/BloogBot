# Activities — Economy

The living economy is what makes the server feel alive. Bots post to
AH, bid, buy, deposit at bank, send mail, repair, restock vendors via
purchases. Steady-state reached without seeding.

## Catalog rows

- `econ.ah-restock` — post inventory excess to AH, bid on missing
  recipe mats, manage cancellations.
- `econ.vendor-loop` — full city loop: vendor sell trash, repair
  gear, deposit at bank, retrieve/send mail.

## Task families

| Task | Status |
|---|---|
| `AuctionHousePostTask` | partial |
| `AuctionHouseBuyTask` | partial |
| `AuctionHouseBidTask` | not-started |
| `AuctionHouseCancelTask` | not-started |
| `BankDepositTask` | partial |
| `BankWithdrawTask` | partial |
| `MailSendTask` | partial |
| `MailRetrieveTask` | partial |
| `VendorSellTask` | done (BG via VendorAgent) |
| `VendorBuyTask` | done (BG via VendorAgent) |
| `RepairAllTask` | done (BG via VendorAgent) |
| `GearAcquireFromAhTask` | not-started — find BiS slot upgrade on AH and buy |
| `EquipItemTask` | done (FG+BG via `IObjectManager.UseContainerItem`) |
| `UnequipItemTask` | partial (covered by `EquipmentAgent.UnequipItemAsync` / `CMSG_AUTOSTORE_BAG_ITEM`; no standalone task class) |
| `LoadoutTask` | done (orchestrator) |
| `GearGapTask` | not-started |

## Task specifications

> Phase 0 / S0.8.9 precision blocks. One entry per task in the family
> table above plus the Equipment-family tasks Spec/03 routes here
> (`EquipItemTask`, `UnequipItemTask`, `LoadoutTask`, `GearGapTask`).
> A Phase 1 worker reading any block has enough to implement (or
> finish) the task without a separate investigation pass.
>
> **Interface drift note (R19).** `Spec/03_BOTRUNNER.md#ibottask-interface`
> documents `IBotTask` as a four-method async contract (`TickAsync`,
> `OnPushedAsync`, `OnPoppedAsync`, `OnChildFailedAsync` with
> `BotTaskContext` + `Name` + `BotTaskStatus`). The shipped interface
> at `Exports/BotRunner/Interfaces/IBotTask.cs` is now the Phase 1
> target contract (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync` + `Name` + `Status`). The `BotTask` base
> class (`Exports/BotRunner/Tasks/BotTask.cs`) ships the S1.0 shim
> (per R25): `TickAsync` → `OnTick` → legacy `Update()` body. Existing
> economy tasks keep their `Update()` body unchanged; per-family
> async refactor lands under S1.11 Economy. The base class still
> exposes `BotContext.BotTasks.Pop()`, `Wait.For(...)`, and `Logger`. Each "Public surface" bullet below
> reports **current shipped surface** and (where the task is
> `not-started` or only partial) the **target surface (Phase 1)**.
> All economy tasks are claimed under Phase 1 slot **S1.11 — Economy
> family** (`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`). The vendor
> `Buyback` + single-slot repair packet-fallback path that some of the
> blocks below reference is the deliverable of Phase 1 slot **S1.17 —
> Vendor merchant null handling**.
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by
> `Exports/BotRunner/SnapshotBuilder.cs` from `IObjectManager` +
> the task stack. "Reads" lists the fields the task consumes via
> the equivalent `IObjectManager` property today. "Writes" lists
> the fields whose value changes as a *side effect* of the task
> running (inventory deltas, gold deltas, mail queue) so the test
> assert layer knows what to poll.
>
> **Catalog references.** Every block ties back to the catalog
> rows declared in [`00_INDEX.md`](00_INDEX.md):
> `econ.ah-restock` and `econ.vendor-loop`. Most equipment-family
> blocks additionally underwrite the `LoadoutTask` step list and so
> appear under every catalog row that triggers
> `ObjectiveType.ApplyLoadout`.

### AuctionHousePostTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Economy/AuctionHousePostTask.cs`. No
  `BotTask`-derived class exists today; posting strategy lives in
  the pure-domain service
  `BotRunner.Tasks.Economy.AuctionPostingService` at
  `Exports/BotRunner/Tasks/Economy/AuctionPostingService.cs`
  (record types `MarketPrice` and `PostingDecision`,
  `EvaluatePosting(...)`, `RecordPrice(...)`, `PurgeStale(...)`).
  **Status:** partial — strategy lands; the `IBotTask` that drives
  the walk-to-auctioneer → interact → post loop is open.
- **Public surface:**
  - *Current shipped surface (service only):*
    `AuctionPostingService.EvaluatePosting(uint itemId, ulong itemGuid, uint vendorSellPrice) : PostingDecision?`,
    `RecordPrice(uint itemId, uint buyoutCopper)`,
    `GetMarketPrice(uint itemId) : uint?`,
    `GetAllPrices() : IReadOnlyList<MarketPrice>`,
    `PurgeStale(TimeSpan maxAge)`. Constants:
    `UndercutPercent = 0.05f`, `DefaultAuctionHours = 24`.
  - *Target surface (Phase 1, per R19):*
    `public AuctionHousePostTask(IBotContext context, AuctionPostingService strategy, IReadOnlyList<PostingDecision> queue)`
    plus the four async lifecycle methods (`TickAsync`,
    `OnPushedAsync`, `OnPoppedAsync`, `OnChildFailedAsync`) and a
    private `enum PostState { FindAuctioneer, MoveToAuctioneer,
    InteractWithAuctioneer, PostNextItem, WaitForAck, Complete }`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` / `.MapId`,
    `IObjectManager.Units` filtered by `NpcFlags &
    UNIT_NPC_FLAG_AUCTIONEER (0x40000)`,
    `IObjectManager.Items` (bag scan for postable inventory),
    `IObjectManager.Player.CoinageCopper` (deposit cost check).
    These project into `WoWActivitySnapshot.player`,
    `.nearbyUnits`, and `.inventory`.
  - *Writes/mutates:* `WoWActivitySnapshot.inventory` entries
    disappear as items move into auction listings;
    `WoWActivitySnapshot.player.CoinageCopper` drops by the AH
    deposit cost; auction-listing telemetry surfaces through
    `EconomyCoordinator.AuctionListings` (`Services/WoWStateManager/Economy/`).
- **BG protocol footprint:**
  - `Opcode.MSG_AUCTION_HELLO = 0x255` (`AuctionHouseNetworkClientComponent.cs:134`).
  - `Opcode.CMSG_AUCTION_SELL_ITEM = 0x256`
    (`AuctionHouseNetworkClientComponent.cs:334`) — the post packet.
  - `Opcode.CMSG_AUCTION_LIST_OWNER_ITEMS = 0x259`
    (`AuctionHouseNetworkClientComponent.cs:245`) for the post-ack
    verification scan.
  - `Opcode.CMSG_AUCTION_LIST_ITEMS = 0x258`
    (`AuctionHouseNetworkClientComponent.cs:219`) for the price-scan
    that drives `AuctionPostingService.RecordPrice`.
- **FG memory footprint:**
  - `IObjectManager.Units` (LINQ filter by `UNIT_NPC_FLAG_AUCTIONEER`).
  - `IObjectManager.GetClosestGameObject` is *not* used — auctioneer
    is a unit, not a gameobject (mailbox/banker have GameObject
    rows; auction NPCs do not).
  - `IObjectManager.Items` (bag iteration with `WoWItemBindType` /
    `WoWItemQuality` filters).
  - `IObjectManager.AuctionFrame` → `FgAuctionFrame` at
    `Services/ForegroundBotRunner/Frames/FgAuctionFrame.cs`
    (Lua: `BrowseName:SetText(...)`,
    `AuctionFrameBrowse_Search()`, `StartAuction(...)`,
    `IsVisible` predicate).
  - `IObjectManager.Interact(IWoWUnit)` to open the auctioneer
    gossip (which sends `MSG_AUCTION_HELLO` server-side).
  - No standalone Lua `Functions.LuaCall` outside `FgAuctionFrame`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.AuctionHouseParityTests.AH_PostAndBuy_FgBgParity`
  at `Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs:50`.
  Sibling coverage: `AH_Search_FgBgParity` (line 35),
  `AH_Cancel_FgBgParity` (line 78),
  `AuctionHouseTests.AH_NavigateToAuctioneer_SnapshotShowsNearbyNpc`
  (line 34), `AH_InteractWithAuctioneer_OpensAhFrame` (line 49).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~AuctionHouseParityTests.AH_PostAndBuy_FgBgParity"`
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.ah-restock` (the post leg of the restock loop); referenced
  by the AH strategy callouts in the `EconomyCoordinator`
  responsibilities below.

### AuctionHouseBuyTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Economy/AuctionHouseBuyTask.cs`. No
  class exists today; the buyout packet path is shipped at
  `AuctionHouseNetworkClientComponent.PlaceBidAsync(...)` /
  `BuyoutAsync(...)` (line 290+ of
  `Exports/WoWSharpClient/Networking/ClientComponents/AuctionHouseNetworkClientComponent.cs`).
  **Status:** partial — packet path and FG `AuctionFrame` exist;
  the IBotTask that walks to the auctioneer, searches, and buys is
  open work.
- **Public surface:**
  - *Current shipped surface:* packet helpers
    `PlaceBidAsync(uint auctionId, uint bidCopper)` and
    `ListItemsAsync(...)` on `AuctionHouseNetworkClientComponent`.
  - *Target surface (Phase 1):*
    `public AuctionHouseBuyTask(IBotContext context, uint targetItemId, uint maxCopperToPay, int desiredQuantity)`
    plus async lifecycle methods and a private
    `enum BuyState { FindAuctioneer, MoveToAuctioneer,
    InteractWithAuctioneer, SearchListings, EvaluateMatches,
    BuyoutMatch, WaitForAck, Complete }`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Units`
    (`UNIT_NPC_FLAG_AUCTIONEER`),
    `IObjectManager.Player.Position` / `.CoinageCopper`,
    `IObjectManager.AuctionFrame.GetAuctionListings()` (FG),
    `AuctionPostingService.GetAllPrices()` (BG-side price model).
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.CoinageCopper` drops by the
    buyout total; `WoWActivitySnapshot.inventory` gains the bought
    item once mail collection (`MailRetrieveTask`) runs — AH
    delivers via mail, not directly to bags.
- **BG protocol footprint:**
  - `Opcode.MSG_AUCTION_HELLO = 0x255`.
  - `Opcode.CMSG_AUCTION_LIST_ITEMS = 0x258` (search).
  - `Opcode.CMSG_AUCTION_PLACE_BID = 0x25A`
    (`AuctionHouseNetworkClientComponent.cs:298`) — used for both
    bid and full-buyout (`bid == buyout` is the buyout signal in
    1.12.1).
  - `Opcode.CMSG_AUCTION_LIST_BIDDER_ITEMS = 0x264`
    (`AuctionHouseNetworkClientComponent.cs:271`) for confirming
    the win/loss.
- **FG memory footprint:**
  - `IObjectManager.Units` (LINQ filter by `UNIT_NPC_FLAG_AUCTIONEER`).
  - `IObjectManager.Interact(IWoWUnit)`.
  - `IObjectManager.AuctionFrame` → `FgAuctionFrame.SearchByName`,
    `PlaceBid`, `Buyout`, `IsVisible`.
  - `IObjectManager.Player.CoinageCopper`.
  - No direct `Functions.LuaCall` outside `FgAuctionFrame`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.AuctionHouseParityTests.AH_PostAndBuy_FgBgParity`
  at `Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs:50`
  (round-trips post + buyout cross-faction).
  Standalone-buy planned anchor test:
  `Tests/BotRunner.Tests/LiveValidation/AuctionHouseBuyTests.cs::Buy_BuyoutListing_ItemArrivesViaMail`
  (currently `not-started`).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~AuctionHouseParityTests.AH_PostAndBuy_FgBgParity"`
- **Catalog `TaskFamily` claim:** `Economy`. Activated by
  `econ.ah-restock` (recipe-mat bidding) and by
  `GearAcquireFromAhTask` (Phase-4 slot SE.6) for BiS gear
  acquisition off the AH.

### BankDepositTask

- **Class declaration:**
  `BotRunner.Tasks.Economy.BankDepositTask` at
  `Exports/BotRunner/Tasks/Economy/BankDepositTask.cs`. Inherits
  `BotTask` and implements `IBotTask`. **Status:** partial — the
  walk-to-banker state machine lands, but the inner
  `BankState.DepositItems` body is a logger stub
  (`#pragma warning disable CS0649` on `_depositedCount`).
- **Public surface:**
  - *Current shipped surface:*
    `public BankDepositTask(IBotContext context, IReadOnlySet<uint>? keepItemIds = null)`,
    inherits `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()` body, S1.0/R25 shim-only),
    `public bool ShouldDeposit(uint itemId, int itemClass, int itemSubClass)`.
    `private enum BankState { FindBank, MoveToBank,
    InteractWithBanker, DepositItems, Complete }`.
    `public static readonly Dictionary<string, Position>
    BankPositions` (Orgrimmar / Undercity / Thunder Bluff /
    Stormwind / Ironforge / Darnassus).
    Constants: `BankerInteractRange = 5f`.
  - *Target surface (Phase 1):* same constructor signature plus
    async lifecycle methods; the `DepositItems` body resolved to
    actually emit `CMSG_AUTOSTORE_BANK_ITEM` per item that
    `ShouldDeposit(...)` returns true for, using the bagged-item
    map exposed by `IObjectManager.Items`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` / `.MapId`,
    `IObjectManager.Units` filtered by `NpcFlags &
    UNIT_NPC_FLAG_BANKER (0x20000)`,
    `IObjectManager.Items` (bag iteration with the
    `ShouldDeposit` predicate).
  - *Writes/mutates:*
    `WoWActivitySnapshot.inventory` rows clear (items move to
    bank slots);
    `WoWActivitySnapshot.player.CoinageCopper` does *not* change
    on deposit (coin deposit is a separate `CMSG_MOVE_MONEY` flow).
- **BG protocol footprint:**
  - `Opcode.CMSG_BANKER_ACTIVATE = 0x1B7`
    (`BankNetworkClientComponent.cs:155`).
  - `Opcode.CMSG_AUTOSTORE_BANK_ITEM = 0x282`
    (`BankNetworkClientComponent.cs:290`) — one per item moved.
  - `Opcode.CMSG_BUY_BANK_SLOT = 0x1B9`
    (`BankNetworkClientComponent.cs:418`) when bank tabs are full
    and the strategy elects to purchase another slot.
- **FG memory footprint:**
  - `IObjectManager.Units` filtered by `UNIT_NPC_FLAG_BANKER`.
  - `IObjectManager.Interact(IWoWUnit)` to open the bank window.
  - `IObjectManager.BankFrame` → `FgBankFrame` at
    `Services/ForegroundBotRunner/Frames/FgBankFrame.cs`
    (Lua: `BankFrame:IsVisible()` predicate, `CloseBankFrame()`).
  - `IObjectManager.PickupContainerItem(bag, slot)` +
    `IObjectManager.UseContainerItem(bag, slot)` (or the explicit
    `PutItemInBackpack` / drop-on-bank-slot Lua) to commit the
    deposit on FG.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.BankInteractionTests.Bank_DepositAndWithdraw_ItemPreserved`
  at `Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs:51`.
  Sibling coverage:
  `Bank_NavigateToBanker_FindsBankerNpc` (line 36),
  `BankParityTests.Bank_DepositWithdraw_FgBgParity` (line 36),
  `BankParityTests.Bank_PurchaseSlot_FgBgParity` (line 73),
  `EconomyInteractionTests.Bank_OpenAndDeposit` (line 34).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~BankParityTests.Bank_DepositWithdraw_FgBgParity"`
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.vendor-loop` (the deposit leg of the city loop) and the
  `EconomyCoordinator` "Bank deposit policy" responsibility below.

### BankWithdrawTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Economy/BankWithdrawTask.cs`. No
  class exists today; bank-frame interaction is exercised via the
  `BankNetworkClientComponent.AutostoreBankItemAsync(...)` packet
  helper and the FG `FgBankFrame`. **Status:** partial — packet
  + frame surfaces are in place; the dedicated task is open.
- **Public surface (planned):**
  - `public BankWithdrawTask(IBotContext context, IReadOnlyList<(int bankSlot, int destinationSlot)> moves)`
    plus async lifecycle methods.
  - Private `enum WithdrawState { FindBank, MoveToBank,
    InteractWithBanker, WithdrawItems, Complete }` mirroring
    `BankDepositTask`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` / `.MapId`,
    `IObjectManager.Units` (`UNIT_NPC_FLAG_BANKER` filter),
    `IObjectManager.BankFrame.GetBankItems()` (FG-only),
    `BankNetworkClientComponent` cached bank inventory (BG).
  - *Writes/mutates:* `WoWActivitySnapshot.inventory` gains rows
    as items move bank → bags;
    `WoWActivitySnapshot.player.FreeBagSlots` drops accordingly.
- **BG protocol footprint:**
  - `Opcode.CMSG_BANKER_ACTIVATE = 0x1B7`.
  - `Opcode.CMSG_AUTOSTORE_BAG_ITEM = 0x10B` — same opcode used
    for bank→bag transfer (the source slot identifier is what
    distinguishes deposit vs withdraw in 1.12.1; see
    `InventoryNetworkClientComponent.cs:218` +
    `EquipmentNetworkClientComponent.cs:149` callers).
- **FG memory footprint:**
  - `IObjectManager.Units` filtered by `UNIT_NPC_FLAG_BANKER`,
    `IObjectManager.Interact(IWoWUnit)`.
  - `IObjectManager.BankFrame` → `FgBankFrame.IsVisible`,
    `FgBankFrame.GetBankItems()`, `Close()`.
  - `IObjectManager.PickupContainerItem(bankBag, bankSlot)` +
    `IObjectManager.UseContainerItem(destBag, destSlot)` to commit
    the withdraw drop.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.BankInteractionTests.Bank_DepositAndWithdraw_ItemPreserved`
  at `Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs:51`
  exercises the round trip;
  `BankParityTests.Bank_DepositWithdraw_FgBgParity` (line 36)
  cross-checks FG/BG. Standalone-withdraw planned anchor test:
  `Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs::Bank_Withdraw_OnlyItemAppearsInBags`
  (currently `not-started`).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~BankInteractionTests.Bank_DepositAndWithdraw_ItemPreserved"`
- **Catalog `TaskFamily` claim:** `Economy`. Used by
  `econ.vendor-loop` whenever a bot's `CharacterBuildConfig`
  flags a stored consumable that needs to be brought back into
  bags for the active day's activity.

### MailSendTask

- **Class declaration:** `BotRunner.Tasks.Economy.MailTransferTask`
  at `Exports/BotRunner/Tasks/Economy/MailTransferTask.cs` is the
  current shipped surface for the send half; it ships the walk-to-
  mailbox state machine but the `MailState.SendMail` body is a
  logger stub (the `_sentCount` field carries
  `#pragma warning disable CS0649`). **Status:** partial.
  **Planned anchor (rename + extraction):**
  `Exports/BotRunner/Tasks/Economy/MailSendTask.cs`.
- **Public surface:**
  - *Current shipped surface:*
    `public MailTransferTask(IBotContext context, string recipientName, IReadOnlyList<uint>? itemIds = null, uint goldCopper = 0)`,
    inherits `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()` body, S1.0/R25 shim-only),
    `private enum MailState { FindMailbox, MoveToMailbox,
    SendMail, Complete }`,
    `public static readonly Dictionary<string, Position>
    MailboxPositions` (Orgrimmar AH, Orgrimmar Bank, Undercity AH,
    Stormwind AH, Ironforge AH).
  - *Target surface (Phase 1, per R19):*
    `public MailSendTask(IBotContext context, string recipientName,
    IReadOnlyList<MailAttachment> attachments, uint goldCopper,
    string subject, string body)` plus async lifecycle methods;
    `SendMail` body wired to
    `MailNetworkClientComponent.SendMailAsync(...)`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` / `.MapId`,
    `IObjectManager.GameObjects` (mailbox; see FG footprint),
    `IObjectManager.Items` (attachments).
  - *Writes/mutates:* `WoWActivitySnapshot.inventory` rows clear
    (items move to outgoing mail);
    `WoWActivitySnapshot.player.CoinageCopper` drops by attached
    gold + the per-attachment send fee (30c base in 1.12.1).
- **BG protocol footprint:**
  - `Opcode.CMSG_SEND_MAIL = 0x238`
    (`MailNetworkClientComponent.cs:648`).
  - The mailbox-open gossip uses `Opcode.CMSG_GAMEOBJ_USE = 0x0B1`
    (sent by `IObjectManager.Interact(IGameObject)` for the
    mailbox GameObject; not a mail-specific opcode).
- **FG memory footprint:**
  - `IObjectManager.GetClosestGameObject` filtered by mailbox
    DisplayId (`mangos.gameobject_template` `type = 19`) — or
    fall back to the hard-coded `MailboxPositions` table when no
    mailbox GO is in client range. **R7 reminder:** the FG path
    must assert non-null on `GetClosestGameObject` before
    `Interact`; opening mail with a null target hangs the task
    until the timeout.
  - `IObjectManager.MailFrame` — **Planned anchor:**
    `Services/ForegroundBotRunner/Frames/FgMailFrame.cs`. No
    `FgMailFrame` exists today; the FG mail send path currently
    routes through the same packet helper as BG.
  - `IObjectManager.Items` for attachment selection.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.MailSystemTests.Mail_SendItem_RecipientReceivesItem`
  at `Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs:77`.
  Sibling coverage:
  `MailSystemTests.Mail_SendGold_RecipientReceives` (line 35),
  `MailParityTests.Mail_SendGold_FgBgParity` (line 36),
  `MailParityTests.Mail_SendItem_FgBgParity` (line 76),
  `EconomyInteractionTests.Mail_OpenMailbox` (line 76).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~MailParityTests.Mail_SendItem_FgBgParity"`
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.vendor-loop` (city-loop mail send leg) and the
  `EconomyCoordinator` "Mail to alt accounts" responsibility
  below.

### MailRetrieveTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Economy/MailRetrieveTask.cs`. No
  class exists today; the per-mail-row helpers are shipped at
  `MailNetworkClientComponent` (lines 255–403). **Status:**
  partial — packet surface and `MailNetworkClientComponent` exist;
  the dedicated task is open.
- **Public surface (planned):**
  - `public MailRetrieveTask(IBotContext context, bool takeGold = true, bool takeAttachments = true, bool markRead = true)`
    plus async lifecycle methods.
  - Private `enum RetrieveState { FindMailbox, MoveToMailbox,
    InteractWithMailbox, ListMail, TakeRow, MarkRead, Complete }`.
- **Snapshot contract:**
  - *Reads:* same as `MailSendTask` plus
    `MailNetworkClientComponent.GetIncomingMail()` cache (BG) /
    `IObjectManager.MailFrame.GetIncomingMail()` (FG).
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.CoinageCopper` rises by the sum
    of `MAIL_TAKE_MONEY` rows;
    `WoWActivitySnapshot.inventory` gains the attached items;
    `WoWActivitySnapshot.unreadMailCount` (planned snapshot field)
    drops.
- **BG protocol footprint:**
  - `Opcode.CMSG_MAIL_TAKE_MONEY = 0x245`
    (`MailNetworkClientComponent.cs:255`).
  - `Opcode.CMSG_MAIL_TAKE_ITEM = 0x246`
    (`MailNetworkClientComponent.cs:287`).
  - `Opcode.CMSG_MAIL_MARK_AS_READ = 0x247`
    (`MailNetworkClientComponent.cs:316`).
  - `Opcode.CMSG_MAIL_DELETE = 0x249`
    (`MailNetworkClientComponent.cs:345`) when the strategy
    deletes empties.
  - `Opcode.CMSG_MAIL_RETURN_TO_SENDER = 0x248`
    (`MailNetworkClientComponent.cs:374`) for the
    "mailbox-full → return excess" recovery path.
- **FG memory footprint:**
  - `IObjectManager.GetClosestGameObject` (mailbox, type=19),
    `IObjectManager.Interact(IGameObject)`.
  - `IObjectManager.MailFrame` — **Planned anchor:**
    `Services/ForegroundBotRunner/Frames/FgMailFrame.cs`. Lua
    surface (planned): `MailFrame:IsVisible()`,
    `GetInboxNumItems()`, `GetInboxText(i)`, `TakeInboxItem(i)`,
    `TakeInboxMoney(i)`, `DeleteInboxItem(i)`.
  - `IObjectManager.Items` after retrieval for inventory update.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.MailSystemTests.Mail_SendGold_RecipientReceives`
  at `Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs:35`
  (the recipient-side assert exercises the receive path);
  `MailSystemTests.Mail_SendItem_RecipientReceivesItem` (line 77).
  Cross-mode coverage:
  `MailParityTests.Mail_SendGold_FgBgParity` (line 36),
  `MailParityTests.Mail_SendItem_FgBgParity` (line 76). Standalone-
  retrieve planned anchor test:
  `Tests/BotRunner.Tests/LiveValidation/MailRetrieveTests.cs::Retrieve_GoldAndItems_ClearsInbox`
  (currently `not-started`).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~MailSystemTests.Mail_SendGold_RecipientReceives"`
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.ah-restock` (AH delivers via mail) and `econ.vendor-loop`
  (mail retrieve leg + the "Mailbox full → retrieve first, then
  send" failure-recovery rule below).

### VendorSellTask

- **Class declaration:** No standalone `VendorSellTask` class.
  Selling is bundled inside `BotRunner.Tasks.VendorVisitTask`
  at `Exports/BotRunner/Tasks/VendorVisitTask.cs` (the
  `DoVendorActions` step calls
  `IObjectManager.QuickVendorVisitAsync(_vendorGuid, itemsToBuy,
  CancellationToken.None)`, which fans out to sell-trash + repair
  + buy-consumables). **Status:** done (BG via VendorAgent).
  **Planned anchor (extraction):**
  `Exports/BotRunner/Tasks/Economy/VendorSellTask.cs`.
- **Public surface:**
  - *Current shipped surface (`VendorVisitTask`):*
    `public VendorVisitTask(IBotContext context)`,
    inherits `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()` body, S1.0/R25 shim-only),
    `private enum VendorState { FindVendor, MoveToVendor,
    InteractVendor, VendorActions, ReturnToGrind, Done }`.
    Action body delegates to
    `IObjectManager.QuickVendorVisitAsync(ulong vendorGuid,
    Dictionary<uint, uint>? itemsToBuy, CancellationToken ct)`.
  - *Target surface (Phase 1, per R19):*
    `public VendorSellTask(IBotContext context, IWoWUnit vendor,
    Func<IWoWItem, bool> sellPredicate)` plus async lifecycle
    methods; `VendorBuyTask` and `RepairAllTask` become siblings
    rather than co-bundled steps.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Units` filtered by `NpcFlags &
    UNIT_NPC_FLAG_VENDOR (0x4)`,
    `IObjectManager.Items` (bag iteration with the
    `sellPredicate` — quality-grey, no-quest, no-soulbound-with-
    durability, etc.),
    `IObjectManager.Aggressors` (combat abort gate, see
    `VendorVisitTask.Update` line 47).
  - *Writes/mutates:* `WoWActivitySnapshot.inventory` rows clear;
    `WoWActivitySnapshot.player.CoinageCopper` rises by the sum
    of vendor sell prices (`mangos.item_template.SellPrice`).
- **BG protocol footprint:**
  - `Opcode.CMSG_SELL_ITEM = 0x1A0`
    (`VendorNetworkClientComponent.cs:380`).
- **FG memory footprint:**
  - `IObjectManager.Units` filtered by `UNIT_NPC_FLAG_VENDOR`
    (LINQ chain at `VendorVisitTask.FindVendor`, lines 87–103).
  - `IObjectManager.SetTarget(ulong guid)` +
    `IObjectManager.Interact(IWoWUnit)`.
  - `IObjectManager.MerchantFrame` → `FgMerchantFrame` at
    `Services/ForegroundBotRunner/Frames/FgMerchantFrame.cs`
    (Lua: `MerchantFrame:IsVisible()`,
    `GetMerchantNumItems()`,
    `BuyMerchantItem(slot, count)`,
    `BuybackItem(slot)`,
    `GetRepairAllCost()`, `RepairAllItems()`).
    **R5 note:** Spec/03's FG-only gap list flags `BuybackItem` as
    requiring the FG `MerchantFrame`. The BG packet fallback for
    null-`MerchantFrame` cases is owned by Phase 1 slot **S1.17 —
    Vendor merchant null handling**.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.VendorBuySellTests.Vendor_SellItem_RemovedFromInventory`
  at `Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs:55`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~VendorBuySellTests.Vendor_SellItem_RemovedFromInventory"`
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.vendor-loop` (the sell leg) plus the
  `GoldThresholdManager.GoldAction.SellVendorTrash` branch of
  `Exports/BotRunner/Tasks/Economy/GoldThresholdManager.cs` when
  the bot is below `MinReserveCopper`.

### VendorBuyTask

- **Class declaration:** No standalone class. Buying is bundled
  inside `BotRunner.Tasks.VendorVisitTask` via the `itemsToBuy`
  dictionary that `ConsumableData.GetConsumablesToBuy(...)`
  produces (`VendorVisitTask.DoVendorActions`, line 175). The
  packet sender lives at
  `Exports/WoWSharpClient/Networking/ClientComponents/VendorNetworkClientComponent.cs`.
  **Status:** done (BG via VendorAgent).
  **Planned anchor (extraction):**
  `Exports/BotRunner/Tasks/Economy/VendorBuyTask.cs`.
- **Public surface:**
  - *Current shipped surface:* implicitly through
    `VendorVisitTask` + `IObjectManager.QuickVendorVisitAsync(...)`.
    Public sender helpers on `VendorNetworkClientComponent`:
    `BuyItemAsync(ulong vendorGuid, uint itemId, uint count, ...)`,
    `BuyItemInSlotAsync(...)`.
  - *Target surface (Phase 1, per R19):*
    `public VendorBuyTask(IBotContext context, IWoWUnit vendor,
    IReadOnlyDictionary<uint, uint> itemQtyByItemId)` plus async
    lifecycle methods.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Units` (`UNIT_NPC_FLAG_VENDOR`),
    `IObjectManager.Player.CoinageCopper`,
    `IObjectManager.MerchantFrame.GetItems()` (FG) /
    `VendorNetworkClientComponent` merchant-list cache (BG) — both
    can be null pre-interact, hence S1.17.
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.CoinageCopper` drops by the
    purchase total; `WoWActivitySnapshot.inventory` gains the
    bought rows.
- **BG protocol footprint:**
  - `Opcode.CMSG_BUY_ITEM = 0x1A2`
    (`VendorNetworkClientComponent.cs:155, 183`).
  - `Opcode.CMSG_BUY_ITEM_IN_SLOT = 0x1A3`
    (`VendorNetworkClientComponent.cs:317`).
- **FG memory footprint:**
  - `IObjectManager.MerchantFrame` (`FgMerchantFrame.BuyItem` Lua
    `BuyMerchantItem(slot, count)` at line 71 of
    `Services/ForegroundBotRunner/Frames/FgMerchantFrame.cs`).
  - `IObjectManager.SetTarget` + `IObjectManager.Interact`.
  - `IObjectManager.Player.CoinageCopper` reactivity check.
  - `BotRunner.Combat.ConsumableData.GetConsumablesToBuy(...)` to
    populate the buy queue.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.VendorBuySellTests.Vendor_BuyItem_AppearsInInventory`
  at `Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs:39`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~VendorBuySellTests.Vendor_BuyItem_AppearsInInventory"`
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.vendor-loop` (the buy-consumables leg). Implicitly
  referenced by every combat-class catalog row via
  `ConsumableData` (food/water/potion restock for the active
  spec).

### RepairAllTask

- **Class declaration:** No standalone class. Repair is bundled
  inside the same `QuickVendorVisitAsync` call as sell/buy.
  Packet helpers at
  `Exports/WoWSharpClient/Networking/ClientComponents/VendorNetworkClientComponent.cs`
  (`RepairAllItemsAsync(...)` line 500,
  `RepairItemAsync(ulong itemGuid, ...)` line 533).
  **Status:** done (BG via VendorAgent).
  **Planned anchor (extraction):**
  `Exports/BotRunner/Tasks/Economy/RepairAllTask.cs`.
- **Public surface:**
  - *Current shipped surface:* implicit through
    `VendorVisitTask.DoVendorActions` → `QuickVendorVisitAsync`.
    Network helpers: `VendorNetworkClientComponent.RepairAllItemsAsync(ulong vendorGuid, ...)`,
    `RepairItemAsync(ulong vendorGuid, ulong itemGuid, ...)`.
  - *Target surface (Phase 1, per R19):*
    `public RepairAllTask(IBotContext context, IWoWUnit repairVendor, bool useGuildFundsIfAvailable = false)`
    plus async lifecycle methods.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Units` filtered by `NpcFlags &
    UNIT_NPC_FLAG_REPAIR (0x1000)` (see
    `VendorVisitTask.FindVendor` lines 89–95 for the preferred-
    repair-vendor sort),
    `IObjectManager.Player.CoinageCopper`,
    `IObjectManager.Items` durability values.
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.CoinageCopper` drops by the
    repair total; per-item durability returns to max — surfaces
    on the snapshot via `WoWActivitySnapshot.equipment.durability`.
- **BG protocol footprint:**
  - `Opcode.CMSG_REPAIR_ITEM = 0x2A8`
    (`VendorNetworkClientComponent.cs:500` for repair-all path
    and `:533` for single-slot path).
- **FG memory footprint:**
  - `IObjectManager.MerchantFrame` → `FgMerchantFrame.RepairAll()`
    (Lua: `RepairAllItems()` at line 91 of
    `Services/ForegroundBotRunner/Frames/FgMerchantFrame.cs`;
    cost gated by `GetRepairAllCost()` at line 27).
  - `IObjectManager.SetTarget` + `IObjectManager.Interact` on the
    repair-flagged unit.
  - `IObjectManager.Player.CoinageCopper` for affordability check.
  - **R5 note:** the single-slot `RepairItem(slot)` Lua path
    requires a non-null `MerchantFrame`; the BG packet fallback
    that bypasses `MerchantFrame` is owned by Phase 1 slot **S1.17
    — Vendor merchant null handling**.
- **Test anchor:** No dedicated `RepairAllTests` file exists
  today; repair is exercised end-to-end inside the
  `VendorBuySellTests` (round trip) and indirectly by
  `BankParityTests` / `BankInteractionTests` (gold-delta checks
  capture the repair charge). Standalone planned anchor test:
  `Tests/BotRunner.Tests/LiveValidation/RepairAllTests.cs::RepairAll_RestoresDurability_AndConsumesGold`
  (currently `not-started`).
- **Catalog `TaskFamily` claim:** `Economy`. Underwrites
  `econ.vendor-loop` (the repair leg) plus every dungeon/raid
  catalog row's pre-pull readiness gate (the
  `EncounterReadiness` slot of `RaidPositioningTask` checks
  durability and pushes a `RepairAllTask` when below 25% on any
  slot).

### EquipItemTask

- **Class declaration:**
  `BotRunner.Tasks.EquipItemTask` at
  `Exports/BotRunner/Tasks/EquipItemTask.cs`. Inherits `BotTask`
  and implements `IBotTask`. **Status:** done — atomic
  primary-construct-and-fire task (`Update` calls
  `ObjectManager.UseContainerItem(bagId, slotId)` and pops).
  Maps to `ObjectiveType.EquipItem` from StateManager.
- **Public surface:**
  - *Current shipped surface:*
    `public EquipItemTask(IBotContext botContext, int bagId, int slotId)`;
    inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`).
    The task pops itself via `BotTasks.Pop()` on the same tick it invokes
    `UseContainerItem` (S1.0/R25, shim-only — per-family async refactor in S1.11 Economy).
  - *Target surface (Phase 1, per R19):* same constructor
    signature, async lifecycle methods. The "fire-and-forget"
    model becomes an `OnPushedAsync` body; `TickAsync` polls
    for the `EquipmentChanged` event before reporting
    `BotTaskStatus.Complete`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Items` (`bagId`/`slotId` resolution)
    and the source item's `EquipSlot` metadata.
  - *Writes/mutates:* `WoWActivitySnapshot.equipment.<slot>`
    becomes the source item; `WoWActivitySnapshot.inventory`
    drops the source row;
    `WoWActivitySnapshot.player.EquippedAverageItemLevel` ticks
    up if the new item beats the displaced item's iLvl.
- **BG protocol footprint:**
  - `Opcode.CMSG_AUTOSTORE_BAG_ITEM = 0x10B`
    (`EquipmentNetworkClientComponent.cs:149`,
    `InventoryNetworkClientComponent.cs:218`) — the same opcode
    drives both equip (bag→equipment) and reverse-equip
    (equipment→bag). The slot pair on the wire identifies the
    direction.
  - In FG mode `UseContainerItem` may trigger
    `Opcode.CMSG_USE_ITEM = 0x0AB` if the source bag slot holds a
    consumable rather than an equippable; the task's `EquipSlot`
    metadata short-circuits this.
- **FG memory footprint:**
  - `IObjectManager.UseContainerItem(int bagId, int slotId)`.
  - No `IObjectManager.MerchantFrame` / `LuaCall` — the auto-equip
    helper handles the slot routing.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.EquipmentEquipTests.EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot`
  at `Tests/BotRunner.Tests/LiveValidation/EquipmentEquipTests.cs:45`.
  Sibling coverage:
  `EquipItem_AutomatedMode_LoadoutAppliesAndEquips` (line 92) for
  the LoadoutTask-driven path.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~EquipmentEquipTests.EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot"`
- **Catalog `TaskFamily` claim:** `Equipment` (per
  Spec/03_BOTRUNNER.md "gear chase loops live here" routing of
  the Equipment family into this file). Underwrites every
  `LoadoutTask` step list whose `LoadoutSpec` declares an equipped
  item, and the gear-acquisition tail of
  `econ.ah-restock` / `GearAcquireFromAhTask` (Phase 4 slot SE.6).

### UnequipItemTask

- **Class declaration:** No standalone `UnequipItemTask` class.
  The unequip path is dispatched from `ObjectiveType.UnequipItem`
  through `EquipmentAgent.UnequipItemAsync(...)` and lands at
  `Exports/WoWSharpClient/Networking/ClientComponents/EquipmentNetworkClientComponent.cs:149`.
  **Status:** partial — the action is end-to-end (test green) but
  there is no IBotTask class; the action handler does the work
  inline.
  **Planned anchor (task extraction):**
  `Exports/BotRunner/Tasks/UnequipItemTask.cs` (sibling to
  `EquipItemTask.cs`, *not* under `Tasks/Economy/` — the Spec/03
  table buckets unequip under Equipment which co-lives with
  the gear chase loops in this file).
- **Public surface:**
  - *Current shipped surface:* `ObjectiveType.UnequipItem` handler
    inside `BotRunnerService.ActionDispatch.cs` calling
    `IObjectManager.EquipmentAgent.UnequipItemAsync(int equipSlot,
    CancellationToken ct)`. No `IBotTask` wrapper.
  - *Target surface (Phase 1, per R19):*
    `public UnequipItemTask(IBotContext botContext, int equipSlot, int? destinationBagId = null, int? destinationSlotId = null)`
    + async lifecycle methods, mirroring `EquipItemTask`'s
    one-shot shape.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Equipment[equipSlot]`,
    `IObjectManager.FreeBagSlotsCount` (must be > 0 to receive
    the unequipped item).
  - *Writes/mutates:*
    `WoWActivitySnapshot.equipment.<slot>` clears;
    `WoWActivitySnapshot.inventory` gains the row (or the test
    helper polls `_bot.WaitForInventoryItemAsync(...)`).
- **BG protocol footprint:**
  - `Opcode.CMSG_AUTOSTORE_BAG_ITEM = 0x10B`
    (`EquipmentNetworkClientComponent.cs:149`). The
    "EquipSlot 16 = MainHand" mapping note in
    `UnequipItemTests.cs:42` documents the slot-key convention
    BotRunner uses on the wire.
- **FG memory footprint:**
  - `IObjectManager.PickupContainerItem` for the equipment slot,
    `IObjectManager.UseContainerItem` for the destination bag
    slot (the FG Lua equivalent of the BG packet pair). No
    standalone Lua call.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.UnequipItemTests.UnequipItem_MainhandWeapon_MovesToBags`
  at `Tests/BotRunner.Tests/LiveValidation/UnequipItemTests.cs:54`.
  Sibling coverage:
  `UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips` (line 102)
  for the LoadoutTask-driven path.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~UnequipItemTests.UnequipItem_MainhandWeapon_MovesToBags"`
- **Catalog `TaskFamily` claim:** `Equipment`. Used by
  `LoadoutTask` whenever a re-loadout displaces an existing slot
  occupant (the displaced item must be unequipped before the
  replacement can land). Indirectly underwrites every
  `quest.starter.*` row that hands out a starting weapon when the
  bot already has a starting weapon equipped.

### LoadoutTask

- **Class declaration:** `BotRunner.Tasks.LoadoutTask` at
  `Exports/BotRunner/Tasks/LoadoutTask.cs`. Inherits `BotTask`
  and implements `IBotTask`. **Status:** done (orchestrator).
  Executes a `Communication.LoadoutSpec` step list received via
  `ObjectiveType.ApplyLoadout`; reports through
  `WoWActivitySnapshot.LoadoutStatus`.
- **Public surface:**
  - *Current shipped surface:*
    `public LoadoutTask(IBotContext context, LoadoutSpec spec, Action<CommandAckEvent>? commandAckSink = null, Func<int, string>? stepCorrelationIdFactory = null)`,
    inherits `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()` body, S1.0/R25 shim-only),
    `public LoadoutStatus Status { get; }`,
    `public string FailureReason { get; }`,
    `public LoadoutSpec Spec { get; }`,
    `public int StepIndex { get; }`,
    `internal IReadOnlyList<LoadoutStep> Plan { get; }`.
    Constants: `StepPacingMs = 100`, `MaxRetriesPerStep = 20`,
    `ThrottleKey = "LoadoutTask.Pace"`.
  - *Target surface (Phase 1, per R19):* same constructor signature
    plus async lifecycle methods; `Update` body lifted to
    `TickAsync`. `OnPushedAsync` runs `BuildPlan`; `OnPoppedAsync`
    emits the terminal `CommandAckEvent`.
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Level`,
    `IObjectManager.Spells` (LearnedSpellIds),
    `IObjectManager.Skills`,
    `IObjectManager.Items`,
    `IObjectManager.Equipment`,
    `IObjectManager.Talents` (off-scope; out-of-character SOAP/
    MySQL path per class summary lines 26–31). LoadoutSpec arrives
    via `WoWActivitySnapshot.command.LoadoutSpec` (delta proto).
  - *Writes/mutates:* `WoWActivitySnapshot.LoadoutStatus`
    progresses `LoadoutNotStarted → LoadoutInProgress →
    LoadoutReady` (or `LoadoutFailed`);
    `WoWActivitySnapshot.equipment`, `.spells`, `.skills`,
    `.player.Level` all tick as each step's
    `IsSatisfied(...)` predicate flips.
- **BG protocol footprint:** the orchestrator itself sends no
  opcodes; it composes per-step sub-tasks. Per-step opcode
  inventory:
  - Spell-learn steps → `Opcode.CMSG_TRAIN_SPELL = 0x1B6` (via
    `TrainerNetworkClientComponent`).
  - Equip steps → `Opcode.CMSG_AUTOSTORE_BAG_ITEM = 0x10B` (via
    `EquipItemTask`).
  - Skill-set / level-set / reset talents / honor-rank-set steps
    use GM SOAP commands and emit *no* CMSG opcodes (out-of-
    character; see class summary lines 26–31).
- **FG memory footprint:**
  - `IObjectManager.Player`,
    `IObjectManager.Items`,
    `IObjectManager.Equipment`,
    `IObjectManager.UseContainerItem`,
    `IObjectManager.LearnSpell(uint spellId)` (FG Lua wrapper).
  - `BotContext.CommandAckSink` for SOAP-step ack propagation.
  - No standalone `Functions.LuaCall` outside the per-step
    sub-tasks.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips`
  at `Tests/BotRunner.Tests/LiveValidation/EquipmentEquipTests.cs:92`.
  Sibling coverage:
  `UnequipItemTests.UnequipItem_AutomatedMode_LoadoutAppliesAndUnequips`
  (line 102),
  `OnboardingAutomatedModeTests` (full LoadoutSpec end-to-end on
  fresh accounts).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~EquipmentEquipTests.EquipItem_AutomatedMode_LoadoutAppliesAndEquips"`
- **Catalog `TaskFamily` claim:** `Equipment`. Underwrites
  every catalog row in `Plan/Activities/00_INDEX.md` indirectly
  — `LoadoutTask` is the first task that runs once
  `AutomatedModeHandler` parses `AssignedActivity`, and the
  catalog row only begins after `LoadoutStatus.LoadoutReady`.
  Directly referenced by the
  `EconomyCoordinator → AccountRoster` cross-character handoff
  for the bank-alt loadout step.

### GearGapTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Equipment/GearGapTask.cs`. No class
  exists today; gear-gap detection lives implicitly inside the
  Phase 4 progression planner (`Spec/05_PROGRESSION.md`) and the
  `SE.6 — Gear acquisition from AH` slot below. **Status:**
  `not-started`.
- **Public surface (planned, Phase 1 per R19):**
  - `public GearGapTask(IBotContext context, TargetGearSet target, IGearMarketModel marketModel)`
    plus async lifecycle methods.
  - `public IReadOnlyList<GearGap> CurrentGaps { get; }`.
  - Private `enum GearGapState { Inspect, Plan, DispatchAcquire,
    WaitForLoadout, Complete }`.
  - Behaviour: compares `IObjectManager.Equipment` against
    `CharacterBuildConfig.TargetGearSet` (per
    `Spec/05_PROGRESSION.md`), produces one or more child tasks
    per gap — `GearAcquireFromAhTask` (slot SE.6),
    `VendorBuyTask` (vendor-purchasable BoEs),
    `MailRetrieveTask` (alt-mailed gear), or a quest pointer.
- **Snapshot contract:**
  - *Reads:*
    `WoWActivitySnapshot.command.CharacterBuildConfig.TargetGearSet`,
    `IObjectManager.Equipment` (per-slot iLvl + item id),
    `IObjectManager.Items` (bagged candidates),
    `AuctionPostingService.GetAllPrices()` for AH-acquisition
    affordability.
  - *Writes/mutates:* emits diagnostic
    `[GEAR_GAP] slot=<n> currentItemId=<x> targetItemId=<y>
    plan=<acquire-source>` rows (mirrored into
    `WoWActivitySnapshot.recentDiagnostics`); does *not* directly
    flip equipment — that's `EquipItemTask`'s job.
- **BG protocol footprint:** the task itself sends no opcodes;
  every gap dispatches one of the leaf tasks above. Indirect
  footprint follows the chosen acquire path:
  - AH acquire → opcode set under `AuctionHouseBuyTask`.
  - Vendor acquire → opcode set under `VendorBuyTask`.
  - Mail acquire → opcode set under `MailRetrieveTask`.
- **FG memory footprint:**
  - `IObjectManager.Equipment`,
    `IObjectManager.Items`.
  - `BotContext.CharacterBuildConfig.TargetGearSet` (per
    `Spec/05_PROGRESSION.md`).
  - No `MerchantFrame` / `BankFrame` / `MailFrame` / `AuctionFrame`
    touch — those open inside the child tasks.
- **Test anchor:** **Planned anchor:**
  `Tests/BotRunner.Tests/LiveValidation/GearGapTests.cs::GearGap_BuyableSlot_DispatchesAcquireAndCloses`
  + `GearGap_EmptySlot_PicksHighestAffordable`. No existing test
  targets `GearGapTask` directly. The closest exercised coverage
  is the LoadoutTask test pair listed under `LoadoutTask` above,
  which validates the static-spec equip path but not the
  market-driven gap planner. **Status:** `not-started`.
- **Catalog `TaskFamily` claim:** `Equipment` / `Economy`
  (the task straddles both — Spec/03 places it in Equipment but
  its primary dispatch surface is the Economy AH-buy + mail-retrieve
  tasks). Underwrites `econ.ah-restock` (the gear-side of the
  restock loop) plus every `dungeon.*` and `raid.*` catalog row
  where `RaidPositioningTask.EncounterReadiness` checks
  per-slot iLvl against the encounter floor.

## Coordinator: `EconomyCoordinator`

Per S2.11.

Responsibilities:

- AH posting strategy:
  - Bot has recipe → undercut competing listing by 5% (cap floor at
    vendor sell price + 10%).
  - Bot has crafted item → post at median price × 1.05.
  - Excess raw materials → post at vendor sell × 3.
- Bid strategy: bid on listed mats below `current_market_avg × 0.8`.
- Cancellation: cancel auctions undercut by > 20%.
- Bank deposit policy: deposit > N stack of any item; deposit BoE
  drops below current level requirement.
- Mail to alt accounts: cross-character mat transfer when one
  character's profession needs a mat another's gathering profession
  has surplus of.

## Slots

### SE.1 — AH price discovery

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Economy/AhPriceTracker.cs`
  - `Exports/BotRunner/Tasks/Economy/AuctionHouseScanTask.cs`
- **Goal:** Bots scan AH on each visit; StateManager aggregates median
  prices per item across the past N hours. Used by post and bid
  strategies.

### SE.2 — Posting strategy

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Economy/AuctionHousePostTask.cs`

### SE.3 — Bidding strategy

- **Owner:** `monorepo-worker`
- **Status:** open

### SE.4 — Cancellation strategy

- **Owner:** `monorepo-worker`
- **Status:** open

### SE.5 — Cross-character mail brokering

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** `EconomyCoordinator` brokers mat transfers between
  characters on the same `AccountRoster` to feed crafting.

### SE.6 — Gear acquisition from AH

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Economy/GearAcquireFromAhTask.cs`
- **Goal:** Bots with a `TargetGearSet` slot still empty buy the BiS
  candidate from AH if affordable.

### SE.7 — LiveValidation (24-hour AH steady-state)

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** SE.1..SE.6
- **Goal:** 50-bot economy-only run. Assert:
  - AH listings count > 100 after 6h.
  - Median bid-on auction count > 10/hour.
  - Bank inventory growth bounded (no infinite hoarding).
  - Trade chat shows at least 1 trade offer per hour.

## Failure recovery

- **AH GMs ban inventory items** → metric, do not retry.
- **Mailbox full** → retrieve first, then send.
- **Vendor merchant null** → use packet path (existing fix).
