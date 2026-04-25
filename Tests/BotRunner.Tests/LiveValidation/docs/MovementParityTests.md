# MovementParityTests

Shodan-directed foreground/background movement parity coverage. SHODAN owns
route start staging while the foreground and background BotRunner targets
receive only movement, facing, and recording actions.

## Bot Execution Mode

**Shodan FG+BG-action / tracked skip** - `Economy.config.json` launches
`ECONBG1` as the Background Orc Warrior target, `ECONFG1` as the Foreground Orc
Warrior target, and SHODAN as the Background Gnome Mage director.

## Test Methods

- Valley of Trials routes: flat path, hill path, reverse hill, long diagonal,
  ledge drop, steep climb, and steep descent.
- Durotar routes: road path, turn-start road path, winding path, and the
  redirect/pause-resume road path.
- Each executable route stages BG/FG at the same start point, starts recording,
  dispatches matching `ActionType.Goto` actions, and compares transform,
  movement-flag, packet, and travel evidence.

## Shodan Staging

The test body does not issue GM setup commands. The fixture owns:

- `EnsureSettingsAsync(Economy.config.json)` and
  `AssertConfiguredCharactersMatchAsync(...)`.
- `ResolveBotRunnerActionTargets(includeForegroundIfActionable: true,
  foregroundFirst: false)` so SHODAN never resolves as an action target.
- `StageBotRunnerAtNavigationPointAsync(...)` for BG and FG route starts.
- Quiesce handling before staging, before action dispatch, and after recording
  stops.

## Runtime Linkage

- SHODAN is director-only and receives no movement actions.
- BG and FG receive only `StartPhysicsRecording`, optional `SetFacing`, `Goto`,
  and `StopPhysicsRecording`.
- Route-local skips document live staging/quiesce instability, insufficient
  live travel after a delivered `Goto`, and redirect packet-recording edges
  where the FG trace misses the expected `START_FORWARD` packet.

## Validation

- Build -> passed with existing warnings.
- Setup grep across `DualClientParityTests.cs` and `MovementParityTests.cs` ->
  no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `dual_movement_parity_shodan_final2.trx` -> passed overall with `10` passed
  and `7` tracked skips across the combined dual-client/movement slice.
- Repo-scoped cleanup before and after live validation reported
  `No repo-scoped processes to stop.`
