# SpiritHealerTests

Shodan-directed spirit-healer recovery coverage. SHODAN stages the BG action
target at the Valley of Trials spirit healer, induces corpse state through the
fixture, and repairs the character after the run. SHODAN is director-only and
never receives recovery actions.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` as an idle Foreground Orc
Warrior topology participant, and SHODAN as the Background Gnome Mage director.

## Test Method

- `SpiritHealer_Resurrect_PlayerAliveWithSickness`: fixture-stages `ECONBG1`
  into corpse state near the Valley spirit healer, dispatches
  `ActionType.ReleaseCorpse`, moves the ghost into interaction range with
  `ActionType.Goto`, and dispatches `ActionType.InteractWith` against the
  spirit-healer GUID. The final assertion waits for strict-alive snapshot state.

## Shodan Staging

The test body calls fixture helpers and BotRunner actions only. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account receives
  the death/recovery actions.
- `StageBotRunnerCorpseAtValleySpiritHealerAsync(...)` for the graveyard
  teleport and corpse-state setup.
- `RestoreBotRunnerAliveAtValleySpiritHealerAsync(...)` for cleanup.

## Runtime Linkage

- `ActionType.ReleaseCorpse` -> `CharacterAction.ReleaseCorpse`.
- `ActionType.Goto` -> persistent `GoToTask` with 4-yard tolerance.
- `ActionType.InteractWith` -> ghost-aware spirit-healer branch in
  `ActionDispatcher`, which greets the NPC and calls
  `DeadActorAgent.ResurrectWithSpiritHealerAsync(...)`.

## Notes

The spirit healer uses NPC flag `0x20` (`npc_flags=33` on the Durotar entry).
The ghost can spawn roughly 5.7 yards from the healer, so the test moves within
the normal 5-yard interaction range before dispatching `InteractWith`.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceCombatDispatchTests" --logger "console;verbosity=minimal"` -> passed `15/15`.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `spirit_healer_shodan_deadactor_order.trx` -> passed `1/1`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
