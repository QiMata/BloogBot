# TaxiTests

Shodan-directed Horde taxi smoke coverage. SHODAN owns taxi setup while the BG
BotRunner target receives only flight-master and taxi-node actions.

## Bot Execution Mode

**Shodan BG-action / tracked skip** - `Economy.config.json` launches
`ECONBG1` as the Background Orc Warrior action target, `ECONFG1` idle for
topology parity, and SHODAN as the Background Gnome Mage director.

## Test Methods

- `Taxi_HordeDiscovery`: stages the BG target at the Orgrimmar flight master
  without enabling all taxi nodes, then dispatches `ActionType.VisitFlightMaster`.
- `Taxi_HordeRide_OrgToXroads`: stages taxi readiness, dispatches
  `VisitFlightMaster`, dispatches `SelectTaxiNode` for Crossroads, and waits for
  a departure position delta.
- `Taxi_MultiHop_OrgToGadgetzan`: same action path as the Crossroads ride with
  the Gadgetzan node target.
- `Taxi_AllianceRide`: Shodan-shaped tracked skip because the shared economy
  roster is Horde-only.

## Shodan Staging

The test body does not issue GM setup commands. The fixture owns:

- `EnsureSettingsAsync(Economy.config.json)` and
  `AssertConfiguredCharactersMatchAsync(...)`.
- `ResolveBotRunnerActionTargets(includeForegroundIfActionable: false)` so only
  `ECONBG1` receives taxi actions.
- `StageBotRunnerTaxiReadinessAsync(...)`, which wraps clean-slate staging,
  coinage, optional taxi-node enabling, Orgrimmar flight-master positioning, and
  flight-master GUID lookup.

## Runtime Linkage

- SHODAN is director-only and never receives an action dispatch.
- The BG action target receives only `ActionType.VisitFlightMaster` and
  `ActionType.SelectTaxiNode`.
- No `.tele`, `.modify money`, or taxi-node setup calls remain in the test body.

## Validation

- Build -> passed with existing warnings.
- Setup grep across the taxi/transport group -> no inline GM setup calls.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `transport_taxi_shodan_final.trx` -> passed all executable `TaxiTests`
  methods; Alliance ride skipped with the Horde-roster reason.
- Repo-scoped cleanup before and after live validation reported
  `No repo-scoped processes to stop.`
