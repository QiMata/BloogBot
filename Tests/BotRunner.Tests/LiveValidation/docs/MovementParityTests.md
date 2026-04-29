# MovementParityTests

Direct foreground/background movement activity parity coverage. The foreground
and background BotRunner accounts stage themselves with their own GM chat
teleports, then perform the same visible live actions.

## Bot Execution Mode

**Direct FG+BG parity** - the fixture launches the configured foreground and
background movement parity accounts. SHODAN is not part of this suite; these
characters have account-level GM access and can self-stage with `.go xyz`.

## Test Methods

- `Pathfinding_PointAToPointB_FgBgParity`: stages both participants in
  Durotar, dispatches matching `ActionType.Goto`, and asserts point A to point
  B pathfinding, travel, arrival, and FG/BG distance parity.
- `RunningJump_FgBgParity`: starts both participants on the same Durotar path,
  dispatches `ActionType.Jump` while they are moving, and asserts matching jump
  evidence.
- `Knockback_FgBgParity`: targets each participant with its own GM
  `.targetself` command, applies `.knockback 5 5`, and asserts movement or jump
  displacement from the command baseline.
- `TransportRide_FgBgParity`: synchronizes on the Undercity west elevator
  gameobject at the lower stop, dispatches matching `Goto` movement onto the
  lower car center, and observes ride evidence. This is not taxi coverage;
  taxis are spline-based movement and belong to taxi/spline tests.

## Staging

The test body uses only the movement-parity FG/BG accounts:

- `EnsureCleanSlateAsync(...)` clears each participant before a probe.
- `BotTeleportAsync(...)` sends each participant's own `.go xyz` GM command.
- `WaitForTeleportSettledAsync(...)` must confirm starts for ground probes;
  elevator staging also accepts settled transport state because transport-local
  and world-position snapshots differ between FG and BG.

## Runtime Linkage

- BG and FG receive only recording actions plus the action under test:
  `Goto`, `Jump`, or bot-chat GM self-knockback commands.
- The Undercity elevator probe observes gameobject transport evidence through
  sustained transport samples or the elevator's large vertical travel. It does
  not classify taxi spline movement as transport behavior.
- Route movement must produce meaningful FG and BG travel. Insufficient live
  travel after a delivered `Goto` fails the route instead of being hidden by a
  Shodan staging skip.

## Validation

- Build -> passed with existing warnings.
- Setup grep across `DualClientParityTests.cs` and `MovementParityTests.cs` ->
  no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `dual_movement_parity_shodan_final2.trx` -> passed overall with `10` passed
  and `7` tracked skips across the combined dual-client/movement slice.
- 2026-04-28 direct-restoration note: `MovementParityTests` was moved back off
  Shodan-directed staging after `movement_parity_category_latest.trx` exposed
  janky tracked skips from Shodan-era quiesce/start-settle gates.
- 2026-04-29 earlier health-check note: point-to-point pathfinding, running jump, and
  self-knockback are live-green after revalidation. The Undercity elevator
  probe was synchronized on the real lower-stop elevator object. At that point,
  `movement_parity_current_polling_helper.trx` passed overall with `4` passed
  and `1` tracked skip for the intermittent FG elevator evidence gap.
- 2026-04-29 `MVT-TRANSPORT-FG` closeout: the Undercity elevator probe now
  boards via action-driven `Goto` from the lower wait point to the lower car
  center, without synthetic lower-car teleport placement or tracked skip. The
  final movement bundle,
  `movement_parity_transport_fg_goto_board_full_04.trx`, passed with `5`
  passed and `0` skipped.
- Repo-scoped cleanup before and after live validation reported
  `No repo-scoped processes to stop.`
