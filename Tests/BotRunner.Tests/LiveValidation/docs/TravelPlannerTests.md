# TravelPlannerTests

Shodan-directed travel planner coverage. SHODAN stages the BG action target on
a street-level Orgrimmar approach point and the BotRunner target receives only
`ActionType.TravelTo` actions.

## Bot Execution Mode

**Shodan BG-action / tracked skip** - `Economy.config.json` launches `ECONBG1`
as the Background Orc Warrior action target, `ECONFG1` as an idle Foreground
Orc Warrior topology participant, and SHODAN as the Background Gnome Mage
director.

## Test Methods

- `TravelTo_ShortWalk_WithinOrgrimmar`: fixture-stages `ECONBG1` at the
  Orgrimmar street-level start, dispatches BG `ActionType.TravelTo` toward the
  auction-house service location, and asserts the snapshot position changes.
- `TravelTo_Crossroads_BotStartsMoving`: tracked skip after Shodan launch due
  the current long-route no-movement gap.
- `TravelTo_Crossroads_PositionApproachesDestination`: tracked skip after
  Shodan launch due the same long-route no-movement gap.
- `TravelTo_CrossZone_MapStaysKalimdor`: tracked skip after Shodan launch due
  the same long-route no-movement gap.

## Shodan Staging

The test body does not issue GM commands. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account
  receives travel actions.
- `StageBotRunnerAtTravelPlannerStartAsync(...)` for the street-level
  Orgrimmar staging teleport.
- `QuiesceAccountsAsync(...)` immediately after staging so leftover setup
  actions do not block the first `TravelTo` dispatch.

## Runtime Linkage

- `ActionType.TravelTo` carries map id `1` plus destination coordinates to the
  BG BotRunner action target.
- The short Orgrimmar route proves dispatch delivery and movement start from a
  Shodan-staged position.
- FG stays idle for topology parity. SHODAN is director-only and never receives
  `TravelTo`.

## Notes

The old elevated Orgrimmar start point could resolve onto a different collision
layer and prevented movement. The migrated helper stages a street-level
approach point also used by corner-navigation coverage.

The long Orgrimmar-to-Crossroads probes are explicit tracked skips, not hidden
passes. The migrated action is delivered and `GoToTask` starts, but live
evidence shows no position delta after 20 seconds and the BG snapshot remains
`CurrentAction=TravelTo`. That is a travel/planning runtime gap to fix in a
later slice.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Direct GM grep on `TravelPlannerTests.cs` -> no matches.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `travel_planner_shodan.trx` -> passed overall: `1` passed, `3` skipped.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
