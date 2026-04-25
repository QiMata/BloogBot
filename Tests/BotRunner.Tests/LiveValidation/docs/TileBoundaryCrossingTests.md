# TileBoundaryCrossingTests

Shodan-directed ADT tile-boundary movement coverage. SHODAN stages the BG
action target near each boundary; the BotRunner target receives only
`ActionType.TravelTo` for movement probes.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` as an idle Foreground Orc
Warrior topology participant, and SHODAN as the Background Gnome Mage director.

## Test Methods

- `Navigate_CrossesTileBoundary_ArrivesSmooth`: stages west of the Orgrimmar
  tile boundary, dispatches BG `TravelTo` east across the boundary, and asserts
  boundary crossing plus arrival.
- `Navigate_OpenTerrain_CrossesBoundarySmooth`: stages south of Orgrimmar,
  dispatches BG `TravelTo` across an open-terrain tile boundary, and asserts
  snapshots remain valid without falling through world geometry.

## Shodan Staging

The test body does not issue GM commands. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account
  receives tile-boundary actions.
- `StageBotRunnerAtNavigationPointAsync(...)` for the boundary start points.
- `QuiesceAccountsAsync(...)` immediately after staging so setup actions do not
  block the movement action under test.

## Runtime Linkage

- `ActionType.TravelTo` carries map id `1` and destination coordinates to
  `ECONBG1`.
- The Orgrimmar route proves a concrete boundary crossing and arrival.
- The open-terrain route keeps the historic lower bar: tile snapshots must be
  observed and Z must not fall below the world.
- FG stays idle for topology parity. SHODAN is director-only and never receives
  navigation dispatches.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Direct GM grep on `TileBoundaryCrossingTests.cs` -> no matches.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `corner_tile_navigation_shodan.trx` -> passed `6/6` for the combined corner/tile slice.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
