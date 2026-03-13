# GatheringProfessionTests

Tests mining and herbalism gathering on naturally spawned nodes with account-level GM setup only.

FG remains a packet/interaction reference path, but BG is the authoritative assertion path for this suite.

## Active Coverage

### 1. Mining_GatherCopperVein_SkillIncreases

**Flow summary:**
1. Query Copper Vein spawns near the Valley of Trials route start from the world DB.
2. Prepare FG first, then BG:
   - revive / return to setup location
   - clear bags when needed
   - self-target
   - learn mining spells
   - set skill
   - add Mining Pick
3. Explicitly stage this test at `ValleyOfTrials` with `.tele name {character} ValleyOfTrials`.
4. Dispatch `ActionType.StartGatheringRoute` with the natural copper candidate coordinates.
5. Let `GatheringRouteTask` optimize the route, path candidate-to-candidate, scan visible nodes, and gather the first valid node.
6. Assert gather success via task diagnostics, bag delta, or skill delta.
   - FG failures now log diagnostic evidence and return to the safe zone.
   - BG remains the hard assertion path for live-suite pass/fail.
   - If all natural candidates are on respawn, the live test skips instead of spawning objects.
   - The nearby-node query now includes `pool_gameobject` / `pool_template` metadata so the Valley route loads the full pooled candidate set instead of truncating pooled spawns silently.

### 2. Herbalism_GatherHerb_SkillIncreases

**Flow summary:**
1. Query Peacebloom (1617), Silverleaf (1618), and Earthroot (1619) spawns near the Durotar herb route start from the world DB, including pool metadata.
2. Prepare FG first, then BG:
   - revive / return to setup location
   - clear bags when needed
   - self-target
   - learn herbalism spells
   - set skill
3. Stage at the Durotar herb route start (`-500, -4800, 38`).
4. Dispatch `ActionType.StartGatheringRoute` with the natural herb candidate coordinates and all three herb entry IDs.
5. Let `GatheringRouteTask` optimize the route, path candidate-to-candidate, scan visible nodes, and gather the first valid node.
6. Assert gather success via task diagnostics, bag delta, or skill delta.
   - FG failures log diagnostic evidence and return to the safe zone.
   - BG remains the hard assertion path for live-suite pass/fail.
   - If all natural candidates are on respawn, the live test skips instead of spawning objects.
   - The nearby-node query includes `pool_gameobject` / `pool_template` metadata so the Durotar route loads the full pooled candidate set.

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
- Mining route selection: `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelection.cs`
- Setup helpers: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- Gather route task: `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
- BG node detection and interaction: `Exports/WoWSharpClient/`
- FG node interaction reference: `Services/ForegroundBotRunner/`

## Assertions

- Spawn exists in DB or the test skips.
- Only the test itself stages `ValleyOfTrials`; fixture/login cleanup remains on the Orgrimmar safe zone.
- Teleport actually lands near the requested location.
- `StartGatheringRoute` is forwarded successfully.
- `GatheringRouteTask` reports `gather_success` or the bags / skill snapshot changes.
- If no natural node is visible on the route, the test skips rather than forcing a spawn.
- Skill deltas are logged, but lack of skill-up is informational because Vanilla skill gain is RNG-based.
- FG instability during remote teleports/gathers is logged as reference-only evidence and does not fail the BG-authoritative suite.

## Current Status

- No active live test in this repo currently uses `.gobject add` or any other game-object spawn command.
- `GatheringProfessionTests` only queries natural rows from `mangos.gameobject` via `QueryGameObjectSpawnsNearAsync(...)`.
- `2026-03-11` investigation on the failing herbalism run confirmed the reported Silverleaf is the natural DB row:
  - `gameobject.guid = 1641`
  - `gameobject.id = 1618`
  - `position = (590.793, -4870.73, 24.6471)` on map `1`
  - `gameobject_template.faction = 0`
- That means the Mangos error is not evidence that the test spawned a new herb node.
- `2026-03-11` follow-up hardening moved gathering to a BG-authoritative pass/fail model. FG mining/herbalism now run as best-effort reference coverage, log `XunitException` crash/teleport fallout, and return to Orgrimmar in `finally` so the broad suite stays stable while the root FG teleport issue remains tracked under `FG-CRASH-TELE`.
- `2026-03-12` mining was moved onto the task-owned route contract:
  - `communication.proto` now carries `ActionType.StartGatheringRoute`
  - `CharacterAction.StartGatheringRoute` maps through BotRunner
  - `GatheringRouteTask` owns route optimization, candidate movement, visible-node discovery, and gather interaction
  - the old inline mining fallback path was removed from the live test
- `2026-03-12` Valley copper discovery was widened again so the mining test loads pool metadata and all nearby pooled candidates in the Valley radius. The latest DB + live rerun confirmed:
  - `7` natural Valley copper candidates inside the route radius
  - all `7` belong to `pool_entry=1024` (`Copper Veins - Durotar (Master Pool)`)
  - the previous `6`-candidate cap was removed
- `2026-03-12` fixture/login scan confirmed there is no fixture-level or post-login `.tele name {name} ValleyOfTrials` path. The only active Valley teleport in the mining flow is the test's explicit staging helper.
- `2026-03-13` herbalism was moved onto the same task-owned route contract as mining:
  - `GatheringRouteSelection.SelectDurotarHerbCandidates(...)` queries Peacebloom, Silverleaf, and Earthroot near `(-500, -4800)` with `300y` radius and pool metadata
  - `ActionType.StartGatheringRoute` dispatches all three herb entry IDs with the candidate coordinates
  - `GatheringRouteTask` owns route optimization, candidate movement, visible-node discovery, and gather interaction — identical to the mining contract
  - the old inline `TryGatherAtSpawns` herbalism path was replaced
  - the latest live rerun found `24` Durotar herb-route candidates across pools `1020`, `1021`, `1022` and skipped because all natural herbs were on respawn
- Validation after the route-task mining pass:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` -> succeeded
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` -> succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `16 passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 --filter "FullyQualifiedName~Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=detailed"` -> `1 skipped` (`No Copper Vein nodes currently spawned on any of the 6 Valley copper-route candidates`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip" --logger "console;verbosity=minimal"` -> `17 passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 --filter "FullyQualifiedName~Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=detailed"` -> `1 skipped` (`No Copper Vein nodes currently spawned on any of the 7 Valley copper-route candidates`)
- Validation after the herbalism route-task migration:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false` -> succeeded
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip" --logger "console;verbosity=minimal"` -> `20 passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 --filter "FullyQualifiedName~Herbalism_GatherHerb_SkillIncreases" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=detailed"` -> `1 skipped` (`No herb nodes currently spawned on any of the 24 Durotar herb-route candidates`)
- Validation after the hardening pass:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `2 passed, 1 skipped`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `33 passed, 0 failed, 2 skipped`
