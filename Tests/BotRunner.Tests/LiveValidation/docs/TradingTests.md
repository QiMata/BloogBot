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
   dispatch. The BG-initiated transfer remains an explicit tracked skip because
   the server leaves item/copper with the initiator even after all trade actions
   ACK `Success`.

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
- Reason: Shodan attempts `5` through `7` proved `OfferTrade`,
  receiver `AcceptTrade` for the pending invitation, `OfferItem`, `OfferGold`,
  and both final `AcceptTrade` actions ACK `Success`; however, the server still
  leaves the staged Linen Cloth and copper with the BG initiator. Foreground
  initiated transfer is covered by `TradeParityTests`.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/trading_shodan_final.trx`.

- `Trade_InitiateAndCancel_BothBotsSeeCancellation`: passed.
- `Trade_GoldAndItem_TransferSuccessful`: skipped with the tracked BG-to-FG
  server transfer completion gap.

Related diagnostic artifacts:

- `trading_fg_shodan_attempt5.trx`: foreground-initiated transfer passed; BG
  initiated transfer left item/copper with the initiator.
- `trading_fg_shodan_attempt6.trx`: same BG transfer gap after repeat run.
- `trading_fg_shodan_attempt7.trx`: BG `OfferTrade`, receiver-open
  `AcceptTrade`, `OfferItem`, `OfferGold`, receiver final `AcceptTrade`, and
  initiator final `AcceptTrade` all returned `Success`, but the receiver still
  observed no item/copper delta.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_fg_shodan_final.trx"` -> passed `3`, skipped `1`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> failed with the known Ratchet anchor instability: foreground repeated `loot_window_timeout` and popped on `max_casts_reached`.
- `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/TradingTests.cs`
- Shared support: `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- FG trade follow-up surface: `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
