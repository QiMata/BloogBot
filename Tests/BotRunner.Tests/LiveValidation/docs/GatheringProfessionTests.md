# GatheringProfessionTests

Tests mining and herbalism gathering on naturally spawned nodes with account-level GM setup only.

## Active Coverage

### 1. Mining_GatherCopperVein_SkillIncreases

**Flow summary:**
1. Query Copper Vein spawns from the world DB.
2. Prepare FG first, then BG:
   - revive / return to setup location
   - clear bags when needed
   - self-target
   - learn mining spells
   - set skill
   - add Mining Pick
3. Teleport near candidate spawns.
4. Detect the node from `NearbyObjects`.
5. Use `Goto` and `GatherNode`.
6. Assert gather success and report skill deltas.

### 2. Herbalism_GatherHerb_SkillIncreases

Same pattern as mining with herbalism entries `1617`, `1618`, and `1619`.

## Code paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
- Setup helpers: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
- Path/movement dispatch: `Exports/BotRunner/BotRunnerService.cs`
- Gather action handling: `Exports/BotRunner/Tasks/`
- BG node detection and interaction: `Exports/WoWSharpClient/`
- FG node interaction reference: `Services/ForegroundBotRunner/`

## Assertions

- Spawn exists in DB or test skips
- Teleport actually lands near the requested location
- Node becomes visible in `NearbyObjects`
- Gather attempt completes and the node despawns or the skill changes
- Skill deltas are logged, but lack of skill-up is informational because Vanilla skill gain is RNG-based

## Current Status

- No active live test in this repo currently uses `.gobject add` or any other game-object spawn command.
- `GatheringProfessionTests` only queries natural rows from `mangos.gameobject` via `QueryGameObjectSpawnsAsync(...)`.
- `2026-03-11` investigation on the failing herbalism run confirmed the reported Silverleaf is the natural DB row:
  - `gameobject.guid = 1641`
  - `gameobject.id = 1618`
  - `position = (590.793, -4870.73, 24.6471)` on map `1`
  - `gameobject_template.faction = 0`
- That means the Mangos error is not evidence that the test spawned a new herb node. The current live regression is FG crash/recovery behavior around herbalism teleports plus the downstream `GroupFormationTests` fallout after the FG crash.
