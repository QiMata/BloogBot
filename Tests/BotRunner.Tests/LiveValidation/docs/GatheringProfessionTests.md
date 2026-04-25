# GatheringProfessionTests

`GatheringProfessionTests` validates mining and herbalism through
`ActionType.StartGatheringRoute`. The suite now follows the Shodan
test-director pattern: FG and BG are BotRunner action targets, and SHODAN is
the director for loadout, pool refresh, and location staging.

## Shodan Shape

1. `Gathering.config.json` launches:
   - `GATHFG1` as a Foreground Orc Warrior target.
   - `GATHBG1` as a Background Orc Warrior target.
   - `SHODAN` as a Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster before
   actions run.
3. `StageBotRunnerLoadoutAsync(...)` stages profession spells, skills, mining
   pick, clean inventory, and level 20 where needed.
4. `RefreshAndPrioritizeGatheringPoolsWithShodanAsync(...)` issues Shodan-owned
   `.pool update` / `.pool spawns` commands and returns active route candidates.
5. `StageBotRunnerAtValleyCopperRouteStartAsync(...)` and
   `StageBotRunnerAtDurotarHerbRouteStartAsync(...)` keep movement staging in
   the fixture.
6. Test bodies dispatch only `ActionType.StartGatheringRoute` to the selected
   BotRunner target.

## Active Coverage

### Mining

- Query natural Copper Vein rows around the Valley copper route center.
- Stage Mining, skill 1/300, Mining Pick, clean bags, and level 20.
- Refresh pool 1024 through SHODAN and prefer active `.pool spawns` coordinates.
- Dispatch `StartGatheringRoute` with the mining spell, node entry 1731, and
  route coordinates.
- Assert success through skill/bag deltas or `GatheringRouteTask`
  diagnostics.

The copper route center is now `(-1000, -4500, 28.5)`. The previous
`(-800, -4500, 31)` center is a bad staging point: native `GetGroundZ` reports
about `107` there, so `.go xyz` put the bots into vertical recovery before the
route could run.

### Herbalism

- Query natural Peacebloom, Silverleaf, and Earthroot rows near the Durotar
  herb route.
- Stage Herbalism, skill 15/300, clean bags, and level 20.
- Refresh herb pools through SHODAN and prefer active `.pool spawns`
  coordinates.
- Dispatch `StartGatheringRoute` with herb entries 1617/1618/1619.
- Assert success through skill/bag deltas or `GatheringRouteTask`
  diagnostics.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/gathering_shodan_level20.trx`.

- `Mining_BG_GatherCopperVein`: passed in `2m49s`; mining skill advanced
  `1 -> 2`.
- `Herbalism_BG_GatherHerb`: passed in `1m12s`.
- `Herbalism_FG_GatherHerb`: skipped because FG was no longer actionable after
  the preceding FG mining failure.
- `Mining_FG_GatherCopperVein`: failed after `6m14s`. The Shodan migration
  shape is correct: FG was level 20, Mining/pick were staged, active copper
  coordinates were selected, and `StartGatheringRoute` was delivered. The FG
  runner pathing/interaction still did not produce `gather_success` or a
  bag/skill delta before timeout.

The FG mining failure is a documented pre-existing functional gap, not a
reason to revert the Shodan migration. The failure stays visible so the next
gathering-task pass can diagnose FG interaction/pathing parity against the now
green BG path.

## Validation

- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=gathering_shodan_level20.trx"` -> `2 passed, 1 skipped, 1 failed` as documented above.
- Reference anchor:
  `FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool` with
  `fishing_shodan_anchor_gathering_slice.trx` -> passed `1/1`.

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
- Route selection: `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelection.cs`
- Director helpers: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Config: `Services/WoWStateManager/Settings/Configs/Gathering.config.json`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Route task: `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
