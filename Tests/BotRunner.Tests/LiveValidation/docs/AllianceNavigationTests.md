# AllianceNavigationTests

## Shodan Shape

- Uses `Services/WoWStateManager/Settings/Configs/Navigation.config.json`.
- `NAVBG1` is the Human Warrior background target for Alliance-side staging assertions.
- `ECONFG1` launches idle as the stable foreground topology participant. An initial all-Human foreground roster was not kept because the foreground runner crashed during the first live attempt.
- SHODAN is director-only and owns Alliance coordinate staging through `StageBotRunnerAtNavigationPointAsync(...)`.
- Test bodies issue no GM setup and dispatch no BotRunner behavior action; they assert staged snapshots after fixture-owned location setup.

## Coverage

- `Alliance_GoldshireToStormwind` stages Goldshire.
- `Alliance_VendorBuySell` stages Stormwind Trade District.
- `Alliance_Deadmines_Entry` stages the Westfall Deadmines approach.
- `Alliance_Stockade_Entry` stages the Stormwind Stockade entrance.
- `Alliance_Gnomeregan_Entry` stages the Dun Morogh Gnomeregan approach.

## Validation

- Direct GM/setup grep over `NavigationTests.cs` and `AllianceNavigationTests.cs` -> no matches.
- `navigation_alliance_shodan_final4.trx` -> all five Alliance staging checks passed as part of the combined live run.
- Combined live result for `NavigationTests` and `AllianceNavigationTests` -> `7` passed, `1` skipped for the tracked Valley long-route `no_path_timeout` gap.
