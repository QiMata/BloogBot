# AckCaptureTests

Shodan-directed ACK corpus capture probes for foreground-only packet fixture
generation.

## Bot Execution Mode

**Shodan FG-capture / opt-in command** - `Economy.config.json` launches
`ECONFG1` as the Foreground Orc Warrior corpus source, `ECONBG1` as an idle
Background topology peer, and SHODAN as the Background Gnome Mage director.
SHODAN owns launch and staging; SHODAN is never resolved as an action target.

The foreground target intentionally performs the capture-triggering hop or
configured command because ACK corpus files are emitted by the injected client.

## Test Methods

- `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled`: stages
  `ECONFG1` in Orgrimmar, moves it to Ironforge through fixture-owned capture
  positioning, and asserts `MSG_MOVE_WORLDPORT_ACK` corpus output only when
  `WWOW_CAPTURE_ACK_CORPUS=1`.
- `Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled`: skips unless
  `WWOW_ACK_CAPTURE_GM_COMMAND` is set, then sends prep/trigger/reset commands
  through the Shodan-aware ACK capture helper and checks for new corpus fixtures
  when capture output is enabled.

## Shodan Staging

The test body calls only fixture helpers:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the foreground BotRunner account
  can be used as the ACK corpus target.
- `StageBotRunnerAtNavigationPointAsync(...)` for Orgrimmar and Ironforge
  capture positioning.
- `StageBotRunnerAckCaptureCommandAsync(...)` for configured corpus-trigger
  commands.

## Validation

- Setup grep on `AckCaptureTests.cs` -> no inline FG/BG setup command matches.
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> passed with existing warnings.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `ack_capture_shodan.trx` -> passed overall with `1` passed and `1` skipped.

## Notes

The live run skipped the configured-command probe because
`WWOW_ACK_CAPTURE_GM_COMMAND` was not set. The cross-map probe passed; its
cleanup return hop logged a settle retry failure after the assertion path, and
the required repo-scoped cleanup found no remaining processes.
