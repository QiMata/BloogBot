# CornerNavigationTests

Shodan-directed corner and obstacle navigation coverage. SHODAN stages the BG
action target at each probe coordinate; the BotRunner target receives only
`ActionType.TravelTo` for route checks.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` as an idle Foreground Orc
Warrior topology participant, and SHODAN as the Background Gnome Mage director.

## Test Methods

- `Navigate_OrgBankToAH_ArrivesWithoutStall`: stages the street-level
  Orgrimmar bank approach, dispatches BG `TravelTo` to the auction-house
  service location, and asserts arrival within 15 yards.
- `Navigate_RFCCorridors_PassesThroughDoorways`: stages the BG target inside
  Ragefire Chasm and dispatches BG `TravelTo` deeper into the corridor.
- `Navigate_DynamicObjects_PathfindsAround`: stages the BG target near known
  Orgrimmar static obstacles and verifies a snapshot is available.
- `Navigate_UndercityTunnels_FollowsExpectedPath`: stages the BG target in an
  Undercity tunnel probe location and verifies a snapshot is available.

## Shodan Staging

The test body does not issue GM commands. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account
  receives navigation actions.
- `StageBotRunnerAtNavigationPointAsync(...)` for all coordinate staging.
- `QuiesceAccountsAsync(...)` immediately after staging so setup actions do not
  block the route action under test.

## Runtime Linkage

- Route probes dispatch `ActionType.TravelTo` to `ECONBG1`.
- Snapshot-only probes still use Shodan-owned staging and never call
  `BotTeleportAsync(...)` from the test body.
- FG stays idle for topology parity. SHODAN is director-only and never receives
  navigation dispatches.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Direct GM grep on `CornerNavigationTests.cs` -> no matches.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `corner_tile_navigation_shodan.trx` -> passed `6/6` for the combined corner/tile slice.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
