# CraftingProfessionTests

`CraftingProfessionTests` validates First Aid linen-bandage production through
an action-dispatched recipe cast. The suite now follows the Shodan
test-director pattern: BG is the BotRunner action target, FG is launched for
topology parity, and SHODAN stages recipe, skill, and reagent setup.

## Shodan Shape

1. `Crafting.config.json` launches:
   - `CRAFTFG1` as a Foreground Orc Warrior target.
   - `CRAFTBG1` as a Background Orc Warrior target.
   - `SHODAN` as a Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerLoadoutAsync(...)` stages:
   - First Aid Apprentice spell `3273`.
   - Linen Bandage recipe spell `3275`.
   - First Aid skill `129` at `1/75`.
   - Exactly one Linen Cloth `2589`.
4. The test dispatches `ActionType.CastSpell` with spell `3275` only to BG.
5. Assertions come from snapshots: linen cloth is consumed, one Linen Bandage
   `1251` appears, and occupied bag-slot count stays stable.

FG remains idle in this slice. The foreground runner's spell-id cast path is
not the validated crafting path; keeping FG online preserves the shared
FG+BG+SHODAN topology without hiding that limitation.

## Test Methods

### FirstAid_LearnAndCraft_ProducesLinenBandage

- BotRunner action target: `CRAFTBG1`.
- Director: `SHODAN`.
- Idle topology participant: `CRAFTFG1`.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/crafting_shodan.trx`.

- `FirstAid_LearnAndCraft_ProducesLinenBandage`: passed in `2m29s`.
- The TRX shows `CRAFTBG1` loadout staging through `StageBotRunnerLoadoutAsync`
  and the only under-test action dispatch as `ActionType.CastSpell`.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CraftingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=crafting_shodan.trx"` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs`
- Shodan loadout helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Cast implementation path: `Exports/BotRunner/ActionDispatcher.cs`
- BG spell cast implementation: `Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs`
