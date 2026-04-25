# MountEnvironmentTests

Shodan-directed mount environment coverage. SHODAN stages the BG action target
with riding skill, the Frostwolf Howler spell, and indoor/outdoor test
positions; SHODAN remains director-only and never receives mount actions.

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` as an idle Foreground Orc
Warrior topology participant, and SHODAN as the Background Gnome Mage director.

## Test Methods

- `Snapshot_OutdoorLocation_ReportsNotIndoors`: fixture-stages `ECONBG1` at
  the Valley of Trials, clears mount state, and asserts `IsIndoors=false`.
- `Snapshot_IndoorLocation_ReportsIsIndoors`: fixture-stages `ECONBG1` inside
  Ragefire Chasm, clears mount state, and asserts `IsIndoors=true`.
- `MountSpell_OutdoorLocation_Mounts`: fixture-stages riding skill and mount
  spell `23509`, stages the outdoor location, dispatches `ActionType.CastSpell`,
  and waits for non-zero `MountDisplayId`.
- `MountSpell_IndoorLocation_DoesNotMount`: fixture-stages the same loadout and
  indoor location, dispatches `ActionType.CastSpell`, and asserts recent
  `[MOUNT-BLOCK]` / `ONLY_OUTDOORS` evidence plus zero `MountDisplayId`.

## Shodan Staging

The test body does not issue GM commands. The fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `Economy.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only the BG BotRunner account receives
  mount actions.
- `StageBotRunnerMountLoadoutAsync(...)` for riding skill and mount spell setup.
- `StageBotRunnerUnmountedAsync(...)` for `.dismount` / `.unaura` cleanup.
- `StageBotRunnerAtMountEnvironmentLocationAsync(...)` for Valley of Trials and
  Ragefire Chasm coordinate staging.

## Runtime Linkage

- `ActionType.CastSpell` carries spell id `23509` to the BG BotRunner action
  target.
- Outdoor success is observed via snapshot `MountDisplayId`.
- Indoor rejection is observed via snapshot chat/error rings after the cast.

## Notes

This slice is intentionally BG-action-only because foreground spell-id casting
is still documented as a runtime gap for `ActionType.CastSpell`; FG launches for
Shodan topology parity and stays idle.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `mount_environment_shodan.trx` -> passed `4/4`.
- Session Ratchet anchor `fishing_shodan_anchor.trx` -> failed in the known
  anchor-instability lane: FG never reached `fishing_loot_success` within 3m
  after repeated `loot_window_timeout`, `max_casts_reached`, and "cast didn't
  land in fishable water" evidence. Not treated as a MountEnvironment
  regression.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`
