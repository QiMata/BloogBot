# AuctionHouseTests

`AuctionHouseTests` validates Orgrimmar auctioneer detection and
`ActionType.InteractWith` dispatch through the Shodan test-director pattern.
The test body issues no GM setup commands.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior action target.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarAuctionHouseAsync(...)` stages each BotRunner
   target at the Orgrimmar auction house.
4. The interaction test dispatches `ActionType.InteractWith` only to FG/BG,
   using the detected auctioneer GUID.
5. Assertions come from snapshots and nearby-unit markers.

## Test Methods

### AH_NavigateToAuctioneer_SnapshotShowsNearbyNpc

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: none; this is a staged snapshot/NPC-detection
  baseline.

### AH_InteractWithAuctioneer_OpensAhFrame

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.InteractWith` with the detected
  auctioneer GUID.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/auction_house_shodan.trx`.

- `AH_NavigateToAuctioneer_SnapshotShowsNearbyNpc`: passed.
- `AH_InteractWithAuctioneer_OpensAhFrame`: passed.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AuctionHouseTests|FullyQualifiedName~AuctionHouseParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=auction_house_shodan.trx"` -> passed `3`, skipped `2`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/AuctionHouseTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Interact implementation: `Exports/BotRunner/ActionDispatcher.cs`
