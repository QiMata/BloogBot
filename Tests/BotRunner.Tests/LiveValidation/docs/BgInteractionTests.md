# BgInteractionTests

Shodan-directed economy/NPC interaction smoke coverage.

## Bot Execution Mode

**Shodan BG-action / tracked skip** - `Economy.config.json` launches
`ECONBG1` as the BG action target, `ECONFG1` idle for topology parity, and
SHODAN as the director. SHODAN performs setup through fixture helpers; only
`ECONBG1` receives BotRunner actions.

## Active Tests

### AuctionHouse_InteractWithAuctioneer

**Purpose:** Prove auctioneer detection and BG `ActionType.InteractWith`
dispatch after Shodan-owned Orgrimmar auction-house staging.

**Setup path:**
- `StageBotRunnerAtOrgrimmarAuctionHouseAsync(...)` places the BG action
  target near the Orgrimmar auctioneers.

**Action path:**
- `ActionType.InteractWith` using the detected auctioneer GUID.

**Current live result:**
- Passed in `bg_interaction_shodan.trx`.

### Mail_SendGoldAndCollect_CoinageChanges

**Purpose:** Prove BG mailbox collection through BotRunner after Shodan-owned
mailbox and SOAP mail-money staging.

**Setup path:**
- `StageBotRunnerAtOrgrimmarMailboxAsync(...)` stages the mailbox location.
- `StageBotRunnerMailboxMoneyAsync(...)` sends test copper through the fixture
  helper rather than the test body.

**Action path:**
- `ActionType.CheckMail` using the detected mailbox GUID.

**Current live result:**
- Passed in `bg_interaction_shodan.trx`; coinage increased after collection.

### FlightMaster_DiscoverAndTakeFlight

**Purpose:** Prove BG flight-master visit dispatch after Shodan-owned coinage
and Orgrimmar flight-master staging.

**Setup path:**
- `StageBotRunnerCoinageAsync(...)` funds the BG action target.
- `StageBotRunnerAtOrgrimmarFlightMasterAsync(...)` stages the flight-master
  location.

**Action path:**
- `ActionType.VisitFlightMaster` with a correlation id and command ACK check.

**Current live result:**
- Passed in `bg_interaction_shodan.trx`.

### Bank_DepositItem_MovesToBankSlot

**Purpose:** Keep the bank-deposit smoke path Shodan-shaped while documenting
the missing production action surface.

**Setup path:**
- `StageBotRunnerLoadoutAsync(...)` stages a Worn Mace (`36`) and clean bags.
- `StageBotRunnerAtOrgrimmarBankAsync(...)` stages the bank location.

**Action path:**
- `ActionType.InteractWith` using the detected banker GUID.

**Current live result:**
- Tracked skip in `bg_interaction_shodan.trx` after banker interaction because
  bank deposit has no BotRunner `ActionType` surface yet.

### DeeprunTram_RideTransport_ArrivesAtDestination

**Current live result:**
- Tracked skip in `bg_interaction_shodan.trx`; this smoke suite uses the Horde
  economy roster, and dedicated transport tests own Deeprun Tram validation.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed.
- Setup grep -> no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `bg_interaction_shodan.trx` -> passed overall (`3` passed, `2` skipped).
