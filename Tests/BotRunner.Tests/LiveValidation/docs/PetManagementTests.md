# PetManagementTests

`PetManagementTests` validates hunter pet-management spell dispatch through
the Shodan test-director pattern. The test body issues no GM commands; setup is
encapsulated in `StageBotRunnerLoadoutAsync`.

## Shodan Shape

1. `PetManagement.config.json` launches:
   - `PETFG1` as an idle Foreground Orc Rogue topology participant.
   - `PETBG1` as the Background Orc Hunter action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerLoadoutAsync(...)` stages `PETBG1` to hunter level `10` and
   learns:
   - Call Pet `883`.
   - Dismiss Pet `2641`.
   - Tame Animal `1515`.
4. The test dispatches `ActionType.CastSpell` only to BG for Call Pet and
   Dismiss Pet.
5. Assertions come from action responses plus a refreshed snapshot after Call
   Pet.

FG remains idle in this slice. The foreground account is class-matched to its
existing live character, while the hunter-specific action requirement is
fulfilled by `PETBG1`. This keeps the shared FG+BG+SHODAN topology without
using the foreground spell-id cast path for pet management.

## Test Methods

### Pet_SummonAndManage_StanceFeedAbility

- BotRunner action target: `PETBG1`.
- Director: `SHODAN`.
- Idle topology participant: `PETFG1`.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/pet_management_shodan.trx`.

- `Pet_SummonAndManage_StanceFeedAbility`: passed in `2m07s`.
- The TRX shows `PETBG1` loadout staging through
  `StageBotRunnerLoadoutAsync` and the only under-test action dispatches as
  BG `ActionType.CastSpell` for spell ids `883` and `2641`.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PetManagementTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=pet_management_shodan.trx"` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/PetManagementTests.cs`
- Shodan loadout helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- BG spell cast implementation: `Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs`
