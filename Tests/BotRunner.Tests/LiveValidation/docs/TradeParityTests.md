# TradeParityTests

`TradeParityTests` uses the Shodan test-director topology for foreground-owned
trade actions. SHODAN launches and stages the shared Economy roster, resolves
foreground/BG participants, and keeps the test body free of GM setup.

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
- Current status: active and passing.
- Result: foreground `OfferTrade` and `DeclineTrade` both ACK `Success`, both
  participants keep producing snapshots, and no trade runtime errors are
  reported.

### Trade_GoldAndItem_FgBgParity

- Intended action owner: foreground initiator for parity with BG transfer.
- Director: `SHODAN`.
- Current status: active and passing.
- Result: foreground initiator stages Linen Cloth and copper, BG accepts the
  pending invitation, both sides final-accept, and BG observes the item/copper
  delta.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/trading_shodan_final.trx`.

- `Trade_InitiateCancel_FgBgParity`: passed.
- `Trade_GoldAndItem_FgBgParity`: passed.

Diagnostic artifacts:

- `trading_fg_shodan_attempt5.trx`: foreground transfer passed with
  `item 1->0 / 0->1`, `coinage 50335->50325 / 68568->68578`.
- `trading_fg_shodan_attempt6.trx`: foreground transfer passed again with
  `item 1->0 / 0->1`, `coinage 50325->50315 / 68578->68588`.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_fg_shodan_final.trx"` -> passed `3`, skipped `1`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> failed with the known Ratchet anchor instability: foreground repeated `loot_window_timeout` and popped on `max_casts_reached`.
- `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`
- Shared support: `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- FG trade frame/runtime follow-up: `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs`
- BG packet trade support: `Exports/WoWSharpClient/InventoryManager.cs`
