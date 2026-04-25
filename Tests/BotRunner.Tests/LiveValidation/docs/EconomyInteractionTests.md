# EconomyInteractionTests

`EconomyInteractionTests` now uses the Shodan test-director topology for
banker, auctioneer, and mailbox interaction baselines. The test body issues no
GM setup commands; SHODAN owns world and mail staging through fixture helpers,
while FG/BG receive only `ActionType` dispatches.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior action target.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarBankAsync(...)` stages bank interaction.
4. `StageBotRunnerAtOrgrimmarAuctionHouseAsync(...)` stages auction-house
   interaction.
5. `StageBotRunnerAtOrgrimmarMailboxAsync(...)` stages mailbox interaction.
6. `StageBotRunnerMailboxMoneyAsync(...)` sends the mail-money payload via
   SOAP from the fixture helper.
7. SHODAN is never resolved as an action target.

## Test Methods

### Bank_OpenAndDeposit

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.InteractWith` with the detected
  banker GUID.
- Result: passed.

### AuctionHouse_OpenAndList

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.InteractWith` with the detected
  auctioneer GUID.
- Result: passed.

### Mail_OpenMailbox

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail` with the detected mailbox
  GUID.
- Result: passed. The test asserts coinage increases after the fixture stages a
  money mail for each target.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/economy_interaction_shodan.trx`.

- `Bank_OpenAndDeposit`: passed.
- `AuctionHouse_OpenAndList`: passed.
- `Mail_OpenMailbox`: passed.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EconomyInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=economy_interaction_shodan.trx"` -> passed `3/3`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Interact/check-mail implementation: `Exports/BotRunner/ActionDispatcher.cs`
- BG mail component: `Exports/WoWSharpClient/Networking/ClientComponents/MailNetworkClientComponent.cs`
