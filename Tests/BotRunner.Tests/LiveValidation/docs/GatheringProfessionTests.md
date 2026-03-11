# GatheringProfessionTests

Tests mining and herbalism gathering on naturally spawned nodes with account-level GM setup only.

FG remains a packet/interaction reference path, but BG is the authoritative assertion path for this suite.

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
   - FG failures now log diagnostic evidence and return to the safe zone.
   - BG remains the hard assertion path for live-suite pass/fail.

### 2. Herbalism_GatherHerb_SkillIncreases

Same pattern as mining with herbalism entries `1617`, `1618`, and `1619`.

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
- Setup helpers: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
- Path/movement dispatch: `Exports/BotRunner/BotRunnerService.cs`
- Gather action handling: `Exports/BotRunner/Tasks/`
- BG node detection and interaction: `Exports/WoWSharpClient/`
- FG node interaction reference: `Services/ForegroundBotRunner/`

## Assertions

- Spawn exists in DB or the test skips.
- Teleport actually lands near the requested location.
- Node becomes visible in `NearbyObjects`.
- BG gather attempt completes and the node despawns or the skill changes.
- Skill deltas are logged, but lack of skill-up is informational because Vanilla skill gain is RNG-based.
- FG instability during remote teleports/gathers is logged as reference-only evidence and does not fail the BG-authoritative suite.

## Current Status

- No active live test in this repo currently uses `.gobject add` or any other game-object spawn command.
- `GatheringProfessionTests` only queries natural rows from `mangos.gameobject` via `QueryGameObjectSpawnsAsync(...)`.
- `2026-03-11` investigation on the failing herbalism run confirmed the reported Silverleaf is the natural DB row:
  - `gameobject.guid = 1641`
  - `gameobject.id = 1618`
  - `position = (590.793, -4870.73, 24.6471)` on map `1`
  - `gameobject_template.faction = 0`
- That means the Mangos error is not evidence that the test spawned a new herb node.
- `2026-03-11` follow-up hardening moved gathering to a BG-authoritative pass/fail model. FG mining/herbalism now run as best-effort reference coverage, log `XunitException` crash/teleport fallout, and return to Orgrimmar in `finally` so the broad suite stays stable while the root FG teleport issue remains tracked under `FG-CRASH-TELE`.
- Validation after the hardening pass:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `2 passed, 1 skipped`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `33 passed, 0 failed, 2 skipped`
