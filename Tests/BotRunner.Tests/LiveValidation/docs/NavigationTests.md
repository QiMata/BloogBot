# NavigationTests

BG-only navigation baseline for the headless botrunner.

## Bot Execution Mode

**BG-Only** — BG-only pathfinding baseline. Some Z-trace diagnostics probe both bots. No FG parity comparison. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite validates the live `Goto` path through:
- `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
- `Exports/WoWSharpClient/Movement/MovementController.cs`
- `Services/PathfindingService`

## Test Methods

### Navigation_ShortPath_ArrivesAtDestination

**Bot:** BG only (`TESTBOT2`)

**Scenario:** Razor Hill short path (~30y)
- Start: `(340, -4686, 16.5)`
- End: `(310, -4720, 11)`
- Timeout: `20s`
- Arrival radius: `8y`

### Navigation_CityPath_ArrivesAtDestination

**Bot:** BG only (`TESTBOT2`)

**Scenario:** Orgrimmar city path (~50y)
- Start: `(1629, -4373, 34)`
- End: `(1660, -4420, 34)`
- Timeout: `45s`
- Arrival radius: `8y`

## Metrics

Each run asserts or logs:
- `Goto` dispatch succeeds
- best distance to destination decreases over time
- step distance shows real travel instead of packet noise only
- total travel is recorded for timeout diagnosis
- arrival occurs inside the `8y` acceptance radius

FG remains useful for packet capture, but arrival assertions here are BG-only because the botrunner movement logic lives in the headless client.
