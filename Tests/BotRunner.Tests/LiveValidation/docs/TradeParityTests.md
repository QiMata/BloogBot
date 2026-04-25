# TradeParityTests

`TradeParityTests` now uses the Shodan test-director topology and records the
remaining foreground trade runtime gap explicitly. SHODAN launches and stages
the shared Economy roster, resolves foreground/BG participants, and keeps the
test body free of GM setup.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior participant.
   - `ECONBG1` as the Background Orc Warrior participant.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarTradeSpotAsync(...)` and
   `StageBotRunnerLoadoutAsync(...)` are used through `TradeTestSupport`.
4. `ResolveBotRunnerActionTargets(...)` keeps SHODAN director-only and resolves
   which real BotRunner character would own each parity action.

## Test Methods

### Trade_InitiateCancel_FgBgParity

- Intended action owner: foreground initiator.
- Director: `SHODAN`.
- Current status: skipped after Shodan launch/resolve.
- Reason: foreground `DeclineTrade` currently ACKs
  `Failed/behavior_tree_failed`.

### Trade_GoldAndItem_FgBgParity

- Intended action owner: foreground initiator for parity with BG transfer.
- Director: `SHODAN`.
- Current status: skipped after Shodan launch/resolve.
- Reason: foreground `OfferItem` / `AcceptTrade` currently ACK
  `Failed/behavior_tree_failed`.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/trading_shodan_final.trx`.

- `Trade_InitiateCancel_FgBgParity`: skipped with the foreground cancel ACK
  gap.
- `Trade_GoldAndItem_FgBgParity`: skipped with the foreground transfer ACK
  gap.

The earlier diagnostic artifact `trade_parity_fg_transfer_after_ack_wait.trx`
captures foreground `OfferItem` returning `Failed/behavior_tree_failed` once
the helper began waiting for structured command ACKs. Earlier cancel probes
captured foreground `DeclineTrade` with the same failure shape.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_shodan_final.trx"` -> passed `1`, skipped `3`.
- `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`
- Shared support: `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- FG trade frame/runtime follow-up: `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs`
- BG packet trade support: `Exports/WoWSharpClient/InventoryManager.cs`
