# BankParityTests

`BankParityTests` now uses the Shodan test-director topology for the bank
parity baseline. Implemented coverage verifies FG/BG bank staging, item
staging, banker detection, and banker `InteractWith` dispatch. Deposit,
withdraw, and bank-slot purchase remain explicit tracked skips until BotRunner
exposes those action surfaces.

## Shodan Shape

1. `Economy.config.json` launches `ECONFG1`, `ECONBG1`, and SHODAN.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarBankAsync(...)` stages FG/BG at the Orgrimmar
   bank.
4. `StageBotRunnerLoadoutAsync(...)` stages Linen Cloth `2589` for
   deposit/withdraw parity setup.
5. SHODAN is never resolved as an action target.

## Test Methods

### Bank_DepositWithdraw_FgBgParity

- BotRunner action targets: `ECONBG1` and `ECONFG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.InteractWith` with the detected
  banker GUID.
- Result: skipped after successful staging and banker interaction because bank
  deposit/withdraw has no BotRunner `ActionType` surface yet.

### Bank_PurchaseSlot_FgBgParity

- BotRunner action targets: `ECONBG1` and `ECONFG1`.
- Director: `SHODAN`.
- Under-test action dispatch: none yet; staging and banker detection are
  migrated.
- Result: skipped because bank-slot purchase has no BotRunner `ActionType`
  surface yet.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/bank_shodan.trx`.

- `Bank_DepositWithdraw_FgBgParity`: skipped with the tracked missing-action
  reason.
- `Bank_PurchaseSlot_FgBgParity`: skipped with the tracked missing-action
  reason.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BankInteractionTests|FullyQualifiedName~BankParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bank_shodan.trx"` -> passed `1`, skipped `3`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/BankParityTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Interact implementation: `Exports/BotRunner/ActionDispatcher.cs`
