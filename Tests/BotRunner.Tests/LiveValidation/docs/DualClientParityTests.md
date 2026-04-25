# DualClientParityTests

Shodan-directed dual-client snapshot parity coverage. SHODAN stages the shared
Orgrimmar comparison point while the foreground and background BotRunner
accounts remain the only parity targets.

## Bot Execution Mode

**Shodan FG+BG-action / tracked skip** - `Economy.config.json` launches
`ECONBG1` as the Background Orc Warrior target, `ECONFG1` as the Foreground Orc
Warrior target, and SHODAN as the Background Gnome Mage director.

## Test Methods

- `NearbyUnits_BothBotsDetectSameUnits`: stages both parity targets at the same
  Orgrimmar point and compares visible nearby unit names between snapshots.
- `Position_BothBotsAgreeOnMapAndLocation`: stages both targets and compares
  map and XY distance.
- `SpellList_BothBotsHaveSpells`: checks that both resolved targets expose
  spell data after the Shodan topology is confirmed.
- `Health_BothBotsReportValidHealth`: checks current/max health snapshots on
  both resolved targets.
- `GmCommand_BothBotsCanExecuteCommands`: tracked skip because GM-command
  parity is not a production BotRunner action-dispatch behavior.

## Shodan Staging

The test body does not issue GM setup commands. The fixture owns:

- `EnsureSettingsAsync(Economy.config.json)` and
  `AssertConfiguredCharactersMatchAsync(...)`.
- `ResolveBotRunnerActionTargets(includeForegroundIfActionable: true,
  foregroundFirst: false)` so SHODAN never resolves as an action target.
- `StageBotRunnerAtNavigationPointAsync(...)` for both BG and FG at the shared
  Orgrimmar comparison point.
- `QuiesceAccountsAsync(...)` after staging so snapshot assertions read the
  settled target state.

## Runtime Linkage

- SHODAN is director-only and receives no dual-client parity dispatches.
- Snapshot assertions are scoped to the resolved BG/FG target accounts.
- The only skipped subcase is the legacy GM-command parity probe, documented as
  a non-production action surface.

## Validation

- Build -> passed with existing warnings.
- Setup grep across `DualClientParityTests.cs` and `MovementParityTests.cs` ->
  no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `dual_movement_parity_shodan_final2.trx` -> dual-client executable checks
  passed; GM-command parity skipped with the tracked reason.
- Repo-scoped cleanup before and after live validation reported
  `No repo-scoped processes to stop.`
