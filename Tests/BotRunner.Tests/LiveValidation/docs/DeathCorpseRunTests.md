# DeathCorpseRunTests

Shodan-directed corpse recovery baseline for `RetrieveCorpseTask`.

## Bot Execution Mode

**Shodan BG-action / FG opt-in** - `Loot.config.json` launches `LOOTBG1`,
`LOOTFG1`, and SHODAN. SHODAN owns setup/cleanup, `LOOTBG1` receives the
committed corpse-run actions, and `LOOTFG1` is available only for the guarded
`WWOW_RETRY_FG_CRASH001=1` crash-regression lane.

## Shodan Shape

- `EnsureSettingsAsync(...)` loads
  `Services/WoWStateManager/Settings/Configs/Loot.config.json`.
- `ResolveBotRunnerActionTargets(...)` resolves only FG/BG BotRunner accounts;
  SHODAN is never an action target.
- `StageBotRunnerCorpseAtNavigationPointAsync(...)` encapsulates clean slate,
  Razor Hill coordinate staging, and death induction.
- `RestoreBotRunnerAliveAtNavigationPointAsync(...)` encapsulates revive and
  cleanup staging.
- The test body dispatches only `ActionType.ReleaseCorpse`,
  `StartPhysicsRecording`, `RetrieveCorpse`, and `StopPhysicsRecording`.

## Current Evidence

`death_corpse_run_shodan.trx` passed overall on 2026-04-25: `1` BG pass and
`1` foreground opt-in skip. The BG run restored strict-alive state and asserted
the navtrace sidecar captured `RetrieveCorpseTask` ownership with a non-null
trace snapshot.

The foreground path remains guarded by `WWOW_RETRY_FG_CRASH001=1` because it is
the historical injected-client crash-regression proof. It now uses the same
Loot/SHODAN launch and fixture-contained staging when intentionally enabled.
