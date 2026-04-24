# FishingProfessionTests

Dual-bot live validation for the single-launch, action-driven Ratchet fishing path.

## Bot Execution Mode

**Dual-Bot Conditional** — Both bots run the Ratchet fishing task path. FG gated on IsFgActionable. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Purpose

This suite proves that both BG and FG run the same high-level fishing contract from one shared roster launch:

1. launch `Fishing.config.json` once with FG, BG, and Shodan all online
2. have Shodan stage a close Ratchet pool
3. dispatch `ActionType.StartFishing` to FG with parameters:
   `["Ratchet", 1, 2628]`
4. let `FishingTask` own gear/spell setup, `.tele name <char> Ratchet`, `.pool update 2628`, pool acquisition, cast positioning, cast/loot, and completion
5. re-stage with Shodan, then dispatch the same `StartFishing` action to BG

## Production Links

- `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/Tasks/BotTask.cs`
- `Exports/BotRunner/Tasks/FishingTask.cs`
- `Exports/BotRunner/Combat/FishingData.cs`
- `Exports/BotRunner/Movement/NavigationPath.cs`
- `Exports/WoWSharpClient/Movement/MovementController.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
- `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
- `Exports/WoWSharpClient/Frames/NetworkLootFrame.cs`
- `Services/PathfindingService/Repository/Navigation.cs`
- `Exports/Navigation/PhysicsCollideSlide.cpp`
- `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
- `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
- `Services/ForegroundBotRunner/Frames/FgLootFrame.cs`

## Test Flow

1. `EnsureSettingsAsync(Fishing.config.json)` launches FG + BG + Shodan once. TESTBOT1 and TESTBOT2 stay idle because `AssignedActivity` is absent from the roster.
2. Shodan equips the dedicated admin mage loadout and stages a close Ratchet pool via `EnsureCloseFishingPoolActiveNearAsync(...)`.
3. The test dispatches `ActionType.StartFishing` to FG with:
   - `location = "Ratchet"`
   - `useGmCommands = 1`
   - `masterPoolId = 2628`
4. `FishingTask` owns:
   - pole equip
   - bait application to the equipped pole
   - GM-driven prep (`.learn`, `.setskill`, `.pool update`, named teleport) because `useGmCommands = 1`
   - pool acquisition
   - pool approach and LOS-aware cast positioning
   - cast start
   - channel / bobber confirmation
   - loot-window handling
   - completion after a newly looted catch appears in bags
5. After FG reports `FishingTask fishing_loot_success`, Shodan re-stages a close pool and the test dispatches the same `StartFishing` action to BG.

## Assertions

Both bots must show all of the following:

- pole started in bags and was removed from bags by `FishingTask`
- bait started in bags and was consumed by `FishingTask`
- `FishingTask pool_acquired`
- `FishingTask in_cast_range`
- best visible pool distance entered the task cast window
- fishing channel observed
- fishing bobber observed
- `FishingTask loot_window_open`
- `FishingTask fishing_loot_success`
- a non-pole bag delta after the loot window closes
- no `FishingTask los_blocked`
- no `Your cast didn't land in fishable water`

## FG/BG Runtime Notes

- BG bite handling comes from `SMSG_GAMEOBJECT_CUSTOM_ANIM -> SpellHandler.HandleGameObjectCustomAnim(...) -> InteractWithGameObject(...)`.
- FG now mirrors that behavior through the injected client's recv hook:
  `PacketLogger.OnPacketCaptured -> ForegroundBotWorker.HandleCapturedPacket(...) -> ObjectManager.TryAutoInteractFishingBobberFromPacket()`.
- The live pass condition is no longer "skill-up or any loot-window signal." It is `bobber observed -> loot_window_open -> fishing_loot_success -> catch item appears in bags`.
- The current Ratchet blocker can happen even before shoreline/pathfinding begins, but it is no longer a single failure mode. The staged harness now distinguishes:
  - no local child spawned at either Ratchet dock stage
  - a local child pool spawned but still never became visible from the dock stage
  - local Ratchet child pools only becoming spawnable on direct child-pool probes after the staged refresh path stayed empty
- Direct VMaNGOS server-code review corrected the `.pool update` interpretation: the command prints the pool's current spawned count before it calls `sPoolMgr.UpdatePool(...)`. A response like `Pool #2620: 0 objects spawned [limit = 1]` is pre-update state only, not proof the refresh failed, and `Pool #2620: 1 objects spawned [limit = 1]` is not proof the just-issued update activated a local node.
- The harness now follows child-pool rerolls with `.pool spawns <child>` probes, and the newer direct GM/system-message capture path improved attribution, but the transport is still not fully authoritative. Some `.pool spawns <child>` replies still arrive outside the tracked command window, so the local child pools can remain `Unknown` even though later asynchronous system lines show up in the log. The next instrumentation slice is therefore tighter response capture/timing, not more `.pool update` text parsing.
- When staged visibility fails, the live harness prints the full Barrens master-pool child-site map (`2607..2627`) plus the activation summary, so the failure evidence is self-contained.
- Once staging or direct-probe fallback does proceed, shoreline/pathfinding failures are still real. The latest fishing deltas keep the runtime search local: the harness now caps far probe travel to `20y` from the dock stage, and `FishingTask` refines each probe onto a better walkable edge before moving. The newest clean FG rerun reduced the search to `3` local waypoints and then stalled on the last two short pier legs (`search_walk_stalled waypoint=2/3 distance=16.4`, `waypoint=3/3 distance=12.4`) before `search_walk_exhausted`.
- `FishingTask` now also treats a newer `MovementStuckRecoveryGeneration` during an active `search_walk` probe window as authoritative blocked-leg evidence. After a short `1.5s` grace, it emits `search_walk_stalled ... reason=movement_stuck` and advances instead of waiting out the full `20s` stall timer on the same local pier corner.
- `FishingTask` now also treats a non-progressing shoreline target as a rejected approach, not just a bad cast spot. After `12s` without reducing `distToApproach`, it records the stalled target itself and reacquires the same pool against a different shoreline candidate. This closes the old dock-lip loop where FG could retry the same local approach until the overall fishing timeout.
- The live failure path now appends recent snapshot errors plus recent BotRunner diagnostic lines, so shoreline/pathfinding failures are distinguishable from fishing-task regressions without opening process logs manually.

## Validation

```powershell
dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly
$env:WWOW_DATA_DIR='D:/MaNGOS/data'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live"
```

Latest focused results:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> succeeded
- Targeted deterministic slice (`FishingPoolActivationAnalyzerTests|LiveBotFixtureBotChatTests|GatheringRouteSelectionTests|BotRunnerServiceFishingDispatchTests`) -> `33 passed`
- `2026-04-24` focused live single-launch rerun `fishing_action_driven_single_launch_pathfinding_first_1.trx` is green:
  - FG: `pool_acquired ... castSource=pathfinding` -> `cast_position_arrived distance=15.8 edgeDist=18.0 los=True` -> `fishing_loot_success`
  - BG: `pool_acquired ... castSource=pathfinding` -> `cast_position_arrived distance=16.0 edgeDist=18.0 los=True` -> `fishing_loot_success`
  - Console counts: one TESTBOT1 `WoW.exe` launch, one fixture-ready line, one initial `Restarting with custom settings: ...Fishing.config.json`, and no mid-test roster restarts
- `2026-04-24` focused live single-launch rerun `fishing_action_driven_single_launch_pathfinding_first_2.trx` is also green with the same `castSource=pathfinding` outcome for both bots.
- `2026-04-24` focused live single-launch rerun `fishing_action_driven_single_launch_pathfinding_first_3.console.txt` is inconclusive. The fixture stalled during Shodan `FISHING-WAKE-*` pool staging before either `StartFishing` dispatch, so it does not count as a fishing-placement regression.
- The current open question is staging reliability, not the BG dock LOS issue from the latest screenshot. After restoring the pathfinding-first cast resolver, both bots are again fishing from the same pier-edge standoff instead of BG's old native dock-interior stand point.

## Current Focus

- keep the focused fishing slice meaningful with one shared FG+BG+Shodan roster launch
- keep the success contract tied to the bobber-interact -> loot-window -> bag-delta sequence
- keep `StartFishing` action dispatch authoritative for both FG and BG instead of relying on roster restarts or `AssignedActivity`
- treat staged Ratchet visibility and authoritative `.pool spawns` capture as the first blocker on reruns that do not surface an immediate local pool, then treat any post-acquisition failure as shoreline/pathfinding/LOS work
- use FG as the live packet/timing reference; the focused FG packet-capture slice is green again on the current binaries
- keep the `MovementStuckRecoveryGeneration` search-walk guard covered whenever the dual slice re-enters local pier search
