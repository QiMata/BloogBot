# TradingTests

`TradingTests` now uses the Shodan test-director topology for baseline trade
validation. The test body issues no GM setup commands; SHODAN stages both
BotRunner participants at the Orgrimmar trade spot and the executable case
dispatches only trade `ActionType` messages.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior topology participant.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerLoadoutAsync(...)` clears and stages the target inventory.
4. `StageBotRunnerAtOrgrimmarTradeSpotAsync(...)` positions the trade
   participants without test-body GM commands.
5. Transfer scenarios also use `StageBotRunnerCoinageAsync(...)` before
   dispatch, but remain explicitly skipped until the foreground accept path is
   fixed.

## Test Methods

### Trade_InitiateAndCancel_BothBotsSeeCancellation

- BotRunner action target: `ECONBG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.OfferTrade`, then
  `ActionType.DeclineTrade`.
- Result: passed. The test asserts both participants still produce snapshots
  and no trade-related runtime errors are reported after cancel.

### Trade_GoldAndItem_TransferSuccessful

- BotRunner action target: staged BG initiator plus foreground responder.
- Director: `SHODAN`.
- Current status: skipped after Shodan launch/resolve.
- Reason: BG transfer setup is staged correctly, but the flow currently
  depends on foreground `AcceptTrade`, which ACKs
  `Failed/behavior_tree_failed`.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/trading_shodan_final.trx`.

- `Trade_InitiateAndCancel_BothBotsSeeCancellation`: passed.
- `Trade_GoldAndItem_TransferSuccessful`: skipped with the foreground
  `AcceptTrade` ACK gap.

Related diagnostic artifacts include `trade_parity_fg_transfer_after_ack_wait.trx`
for the foreground transfer failure and earlier BG transfer probes that proved
the corrected BG item-offer packet coordinate mapping.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_shodan_final.trx"` -> passed `1`, skipped `3`.
- `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/TradingTests.cs`
- Shared support: `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- BG packet trade support: `Exports/WoWSharpClient/InventoryManager.cs`
- FG trade follow-up surface: `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
