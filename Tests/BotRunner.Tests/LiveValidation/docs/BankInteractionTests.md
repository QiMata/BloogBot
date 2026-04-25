# BankInteractionTests

`BankInteractionTests` now uses the Shodan test-director topology for
Orgrimmar bank staging. The test body issues no GM setup commands; SHODAN
handles location and item setup through fixture helpers, while FG/BG receive
only BotRunner action dispatches.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior action target.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarBankAsync(...)` stages each BotRunner target at
   the Orgrimmar bank.
4. `StageBotRunnerLoadoutAsync(...)` stages Linen Cloth `2589` for the
   deposit/withdraw placeholder.
5. SHODAN is never resolved as an action target.

## Test Methods

### Bank_NavigateToBanker_FindsBankerNpc

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: none; this is a staged snapshot/NPC-detection
  baseline.
- Result: passed.

### Bank_DepositAndWithdraw_ItemPreserved

- BotRunner action targets: `ECONBG1`, then `ECONFG1` when actionable.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.InteractWith` with the detected
  banker GUID.
- Result: skipped after successful Shodan item/location staging and banker
  interaction because bank deposit/withdraw has no BotRunner `ActionType`
  surface yet.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/bank_shodan.trx`.

- `Bank_NavigateToBanker_FindsBankerNpc`: passed.
- `Bank_DepositAndWithdraw_ItemPreserved`: skipped with the tracked
  missing-action reason.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BankInteractionTests|FullyQualifiedName~BankParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bank_shodan.trx"` -> passed `1`, skipped `3`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Interact implementation: `Exports/BotRunner/ActionDispatcher.cs`
