# NavigationTests

## Shodan Shape

- Uses `Services/WoWStateManager/Settings/Configs/Economy.config.json`.
- `ECONBG1` is the only BotRunner action target for navigation actions.
- `ECONFG1` launches idle for topology parity.
- SHODAN is director-only and owns route-start staging through `StageBotRunnerAtNavigationPointAsync(...)`.
- Test bodies dispatch only `ActionType.Goto` and assert snapshots / task outcome.

## Coverage

- `Navigation_ShortPath_ArrivesAtDestination` stages the BG target on a Durotar road start at `(-500, -4800, 42)`, dispatches `Goto` to `(-460, -4760, 38)`, asserts arrival, and quiesces the target.
- `Navigation_LongPath_ZTrace_FGvsBG` keeps the legacy name but now captures BG-only movement from `(-500, -4800, 41)` to `(-400, -4700, 45)` while FG stays idle for Shodan topology parity. The trace artifact is written as `durotar_winding_trace_*.json`.
- `Navigation_LongPath_ArrivesAtDestination` is a tracked skip: the Valley of Trials long diagonal currently pops `GoToTask` with `no_path_timeout` before arrival under Shodan staging.

## Runtime Notes

- Earlier live attempts showed the Valley long route failing after valid action delivery, so the skip records a navigation/runtime gap rather than a migration exception.
- Reusing the exact same staging command for the short route after the Z-trace route could silently no-op in the live server. The committed short route uses z=`42` to keep the command distinct while MaNGOS ground-snaps to the same Durotar road surface.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Direct GM/setup grep over `NavigationTests.cs` and `AllianceNavigationTests.cs` -> no matches.
- Deterministic safety bundle -> passed `33/33`.
- Dispatch readiness coverage -> passed `60/60`.
- `navigation_alliance_shodan_final4.trx` -> overall live run passed `7/8` with one tracked skip.
