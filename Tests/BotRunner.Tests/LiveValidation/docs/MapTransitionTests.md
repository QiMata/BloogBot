# MapTransitionTests

Shodan-directed Deeprun Tram bounce coverage. SHODAN stages the BG action
target at the Ironforge tram entrance, triggers the server-rejected Deeprun
Tram map transition through the fixture, and returns the target to the
Orgrimmar safe zone after the run.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` as an idle Foreground Orc
Warrior topology participant, and SHODAN as the Background Gnome Mage director.

## Test Method

- `MapTransition_DeeprunTramBounce_ClientSurvives`: fixture-stages `ECONBG1`
  near the Ironforge tram entrance, fixture-triggers the rejected Deeprun Tram
  transition, asserts the target settles back to `InWorld` with
  `IsMapTransition=false`, and dispatches a correlated `ActionType.Goto` at the
  current snapshot position to prove BotRunner remains action-responsive.

## Shodan Staging

The test body does not issue GM commands. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account receives
  the liveness action.
- `StageBotRunnerAtIronforgeTramEntranceAsync(...)` for the Eastern Kingdoms
  staging teleport.
- `TriggerBotRunnerRejectedDeeprunTramTransitionAsync(...)` for the map 369
  rejected-transition command and snapshot settle wait.
- `ReturnBotRunnerToOrgrimmarSafeZoneAsync(...)` for cleanup.

## Runtime Linkage

- Fixture staging uses the existing bot-chat `.go xyz` helper because MaNGOS
  exposes this rejected transition only as a GM world-position command.
- The BotRunner action under test is `ActionType.Goto`, dispatched after the
  bounce against the current snapshot position with an 8-yard tolerance.

## Notes

The rejected Deeprun Tram transition is intentionally BG-action-only. There is
no production `ActionType` for forcing a server-rejected instance teleport, so
the transition itself stays fixture-owned and the BotRunner action proves
post-transition responsiveness.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `map_transition_shodan.trx` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
