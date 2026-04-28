# TaxiTransportParityTests

Shodan-directed FG/BG taxi and transport parity coverage.

## Bot Execution Mode

**Shodan FG+BG-action / tracked skip** - `Economy.config.json` launches
`ECONBG1` and `ECONFG1` as real BotRunner participants plus SHODAN as the
director. Taxi parity dispatches actions to both FG and BG; transport boarding
dispatches real `Goto` actions but keeps unstable runtime observations as
tracked skips.

## Test Methods

- `Taxi_Ride_FgBgParity`: stages both FG and BG for Orgrimmar taxi readiness,
  starts packet/physics/transform recording, dispatches `VisitFlightMaster` and
  `SelectTaxiNode`, and compares departure behavior.
- `Transport_Board_FgBgParity`: stages both clients at the Undercity elevator
  upper stop, records both clients, dispatches `Goto` toward the lower stop, and
  tracks the current elevator `TransportGuid` acquisition gap.
- `Transport_CrossContinent_FgBgParity`: stages the BG target at the Orgrimmar
  zeppelin tower, then skips with the documented missing action-driven
  boarding/disembark assertion.

## Shodan Staging

The test body no longer issues direct setup GM commands. The fixture owns:

- `EnsureSettingsAsync(Economy.config.json)` and character guard checks.
- `ResolveBotRunnerActionTargets(includeForegroundIfActionable: true)` so
  SHODAN cannot become an action target.
- `StageBotRunnerTaxiReadinessAsync(...)` for flight-master, coinage, and taxi
  node setup.
- `StageBotRunnerAtUndercityElevatorUpperAsync(...)` and
  `StageBotRunnerAtOrgrimmarZeppelinTowerAsync(...)` for transport locations.

`StageBotRunnerAtOrgrimmarZeppelinTowerAsync(...)` uses the MaNGOS
`DurotarZeppelin` point (`map=1`, `x=1340.98`, `y=-4638.58`, `z=53.5445`) and
the Orgrimmar/Undercity transport entry `164871`. Entry `176495` is
Grom'Gol/Undercity and should not be used for this cross-continent probe.

## Runtime Linkage

- Taxi parity dispatches `ActionType.VisitFlightMaster` and
  `ActionType.SelectTaxiNode` to both FG and BG.
- Elevator boarding dispatches `ActionType.Goto` to both FG and BG after
  Shodan staging.
- Recording actions remain BotRunner dispatches; setup remains fixture-owned.

## Current Gaps

- Undercity elevator boarding is Shodan-staged and action-dispatched, but live
  clients do not reliably report non-zero `MovementData.TransportGuid` on the
  elevator.
- Cross-continent transport parity still needs a stable production action path
  for boarding, riding, and disembarking a zeppelin route.
- The 2026-04-28 packet-window probe confirmed that the normal
  Orgrimmar/Undercity zeppelin route did not emit
  `SMSG_MONSTER_MOVE_TRANSPORT` within one route cycle, so the next baseline
  attempt needs fresh trigger research from object-update or ordinary
  `SMSG_MONSTER_MOVE` evidence.

## Validation

- Build -> passed with existing warnings.
- Setup grep across the taxi/transport group -> no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `transport_taxi_shodan_final.trx` -> passed overall; taxi parity passed,
  elevator boarding and cross-continent parity skipped with tracked reasons.
- Repo-scoped cleanup before and after live validation reported
  `No repo-scoped processes to stop.`
