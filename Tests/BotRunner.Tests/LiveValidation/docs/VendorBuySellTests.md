# VendorBuySellTests

`VendorBuySellTests` now uses the Shodan test-director topology for the BG
vendor packet baseline. SHODAN stages the BG action target at Grimtak in Razor
Hill, supplies money and sell-item setup through fixture helpers, and never
receives the vendor actions itself.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior topology participant.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerLoadoutAsync(...)` clears BG bags and stages Linen Cloth
   `2589` for the sell path.
4. `StageBotRunnerAtRazorHillVendorAsync(...)` stages BG beside Grimtak.
5. `StageBotRunnerCoinageAsync(...)` ensures BG has enough copper for the buy
   path.
6. The test body dispatches only `ActionType.BuyItem`, `ActionType.SellItem`,
   and post-buy `ActionType.DestroyItem` cleanup.

## Test Methods

### Vendor_BuyItem_AppearsInInventory

- BotRunner action target: `ECONBG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.BuyItem` with the detected vendor
  GUID, item `159`, and quantity `1`.
- Result: passed.

### Vendor_SellItem_RemovedFromInventory

- BotRunner action target: `ECONBG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.SellItem` with the detected vendor
  GUID, bag/slot from snapshot inventory, and quantity `1`.
- Result: passed.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/vendor_buy_sell_shodan.trx`.

- `Vendor_BuyItem_AppearsInInventory`: passed.
- `Vendor_SellItem_RemovedFromInventory`: passed.

This remains a BG packet baseline by design. FG is launched for the shared
Shodan topology but stays idle in this slice; foreground vendor buy/sell parity
can be added as a separate behavior slice.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~VendorBuySellTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=vendor_buy_sell_shodan.trx"` -> passed `2/2`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Vendor packet dispatch: `Exports/BotRunner/ActionDispatcher.cs`
- BG vendor component: `Exports/WoWSharpClient/Networking/ClientComponents/VendorNetworkClientComponent.cs`
