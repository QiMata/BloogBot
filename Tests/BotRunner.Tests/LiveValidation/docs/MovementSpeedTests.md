# MovementSpeedTests

Shodan-directed movement-speed coverage. SHODAN stages the BG action target on
a proven Durotar winding-road route; the BotRunner target receives only
`ActionType.Goto`.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` as an idle Foreground Orc
Warrior topology participant, and SHODAN as the Background Gnome Mage director.

## Test Method

- `BG_Durotar_WindingPathSpeed`: stages `ECONBG1` at the Durotar road start,
  dispatches BG `Goto` to the 141-yard winding path target, and asserts enough
  movement samples, average speed above half run speed, Z stability, and
  arrival.

## Shodan Staging

The test body does not issue GM commands. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account
  receives movement actions.
- `StageBotRunnerAtNavigationPointAsync(...)` for the Durotar start point.
- `QuiesceAccountsAsync(...)` immediately after staging so setup actions do not
  block the `Goto` action under test.

## Runtime Linkage

- `ActionType.Goto` carries the Durotar target coordinates and 5-yard arrival
  tolerance to `ECONBG1`.
- The old observational FG shadow teleports were removed; FG stays idle for
  topology parity.
- SHODAN is director-only and never receives movement dispatches.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Direct GM grep on `MovementSpeedTests.cs` -> no matches.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `movement_speed_shodan.trx` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
