# AuctionHouseParityTests

`AuctionHouseParityTests` now uses the Shodan test-director topology for the
auction-house parity baseline. Implemented coverage verifies FG/BG auctioneer
staging and detection. Post/buy and cancel remain explicit tracked skips until
BotRunner exposes those auction action surfaces.

## Shodan Shape

1. `Economy.config.json` launches `ECONFG1`, `ECONBG1`, and SHODAN.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarAuctionHouseAsync(...)` stages FG/BG at the
   Orgrimmar auction house.
4. `StageBotRunnerLoadoutAsync(...)` stages Linen Cloth `2589` for the
   post/buy placeholder before it skips.
5. SHODAN is never resolved as an action target.

## Test Methods

### AH_Search_FgBgParity

- BotRunner action targets: `ECONBG1` and `ECONFG1`.
- Director: `SHODAN`.
- Result: passed. Both targets stage at the auction house and detect an
  auctioneer.

### AH_PostAndBuy_FgBgParity

- BotRunner action targets: `ECONFG1` as seller, `ECONBG1` as buyer.
- Director: `SHODAN`.
- Result: skipped with reason:
  `Auction post/buy ActionType surface is not implemented yet; Shodan loadout/location staging is migrated.`

### AH_Cancel_FgBgParity

- BotRunner action targets: `ECONBG1` and `ECONFG1`.
- Director: `SHODAN`.
- Result: skipped with reason:
  `Auction cancel ActionType surface is not implemented yet; Shodan location staging is migrated.`

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/auction_house_shodan.trx`.

- `AH_Search_FgBgParity`: passed.
- `AH_PostAndBuy_FgBgParity`: skipped with the tracked missing-action reason.
- `AH_Cancel_FgBgParity`: skipped with the tracked missing-action reason.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AuctionHouseTests|FullyQualifiedName~AuctionHouseParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=auction_house_shodan.trx"` -> passed `3`, skipped `2`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
