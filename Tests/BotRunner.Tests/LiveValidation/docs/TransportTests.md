# TransportTests

Shodan-directed transport-location smoke coverage. These tests validate staged
transport visibility/snapshot behavior while keeping Horde-side setup behind
fixture helpers.

## Bot Execution Mode

**Shodan BG-action / tracked skip** - `Economy.config.json` launches `ECONBG1`
as the Background Orc Warrior target, `ECONFG1` idle for topology parity, and
SHODAN as the director.

## Test Methods

- `Zeppelin_OrgToUndercity`: stages the BG target at the Orgrimmar zeppelin
  tower and checks nearby transport snapshots.
- `Boat_RatchetToBootyBay`: stages the BG target at the Ratchet dock and checks
  nearby boat/transport snapshots.
- `Elevator_Undercity`: stages the BG target at the Undercity elevator upper
  stop and checks nearby transport snapshots.
- `Elevator_ThunderBluff`: stages the BG target at Thunder Bluff elevator
  coordinates and checks nearby transport snapshots.
- `Boat_MenethilToTheramore`: Shodan-shaped tracked skip until an
  Alliance/dock-specific action-target config exists.
- `DeeprunTram_IFToSW`: Shodan-shaped tracked skip until an Alliance/tram
  instance action-target config exists.

## Shodan Staging

The fixture owns all transport coordinate setup:

- `StageBotRunnerAtOrgrimmarZeppelinTowerAsync(...)`
- `StageBotRunnerAtRatchetDockAsync(...)`
- `StageBotRunnerAtUndercityElevatorUpperAsync(...)`
- `StageBotRunnerAtThunderBluffElevatorAsync(...)`

The test body resolves only the BG BotRunner target and never sends setup GM
commands directly. SHODAN remains director-only.

The Orgrimmar zeppelin helper now stages at the local MaNGOS
`DurotarZeppelin` point (`map=1`, `x=1340.98`, `y=-4638.58`, `z=53.5445`) and
uses transport entry `164871` for the Orgrimmar/Undercity route. Older notes
that point at `176495` are for the Grom'Gol/Undercity route, not this tower.

## Runtime Linkage

The migrated executable coverage is snapshot-based. It proves that Shodan
stages the correct target account at gameobject transport locations and that
the BotRunner snapshot sees the expected transport context. Action-driven
gameobject transport riding is covered by `MovementParityTests`; taxi rides are
spline movement and are not transport evidence.

## Validation

- Build -> passed with existing warnings.
- Setup grep across the taxi/transport group -> no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `transport_taxi_shodan_final.trx` -> `TransportTests` passed the four
  Horde-side snapshot checks and skipped the two Alliance/tram placeholders.
- Repo-scoped cleanup before and after live validation reported
  `No repo-scoped processes to stop.`
- 2026-04-28 transport trigger probe: FG/BG staging at the corrected tower
  point first showed that the route does not emit
  `SMSG_MONSTER_MOVE_TRANSPORT`. The follow-up route-specific trigger captured
  FG/BG `transport_packet_window` fixtures from `SMSG_UPDATE_OBJECT` evidence
  for entry `164871`; the promoted parity baselines are
  `foreground_orgrimmar_zeppelin_transport_update_baseline.json` and
  `background_orgrimmar_zeppelin_transport_update_baseline.json`.
