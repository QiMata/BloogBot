# FishingProfessionTests

Dual-bot live validation for the task-owned Ratchet fishing path.

## Bot Execution Mode

**Dual-Bot Conditional** — Both bots run the Ratchet fishing task path. FG gated on IsFgActionable. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Purpose

This suite proves that both BG and FG run the same high-level fishing contract:

1. teleport to Ratchet via `.tele name {charName} Ratchet`
2. dispatch `ActionType.StartFishing`
3. let `FishingTask` equip the pole, acquire the nearest visible pool, move into a castable LOS position, cast, wait for the bite, open the loot window, and finish after the catch reaches bags

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

1. `EnsureCleanSlateAsync(..., teleportToSafeZone: true)` runs for both bots, and FG is checked with `CheckFgActionableAsync(requireTeleportProbe: false)`.
2. Both bots learn fishing, set fishing skill to `75`, clear items, receive a fishing pole plus `Nightcrawler Bait`, and teleport to Ratchet with the named GM teleport.
3. The test queries nearby DB-backed pool rows, builds a local Ratchet child-pool refresh plan, and classifies the staged result instead of flattening everything into "no pool." The normal staged refresh now updates near-stage Ratchet child pools first (`2620`, `2619`, `2627`) and only falls back to master pool `2628` after the local child set stays empty.
4. Both bots now prefer the same fixed local stage order before dispatch: `packet-capture -> parity`. If a stage surfaces a visible natural pool, the test continues immediately. If local Ratchet child pools report spawned objects but still stay invisible, the harness can still proceed with a bounded local search-waypoint set and logs that the blocker is now visibility/streaming or the short pier search route. If both dock stages stay empty, the harness runs direct child-pool probes and can fall back with another bounded local search set when those probes prove nearby Ratchet children are spawnable.
5. The test dispatches `ActionType.StartFishing` for BG and FG.
6. `FishingTask` owns:
   - pole equip
   - bait application to the equipped pole
   - pool acquisition
   - pool approach and LOS-aware cast positioning
   - cast start
   - channel / bobber confirmation
   - loot-window handling
   - completion after a newly looted catch appears in bags

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
dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingPoolStagePlannerTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=minimal"
```

Latest focused results:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded
- Targeted deterministic slice (`FishingTaskTests|AtomicBotTaskTests`) -> `30 passed`
- Focused FG packet capture already proved the task-owned path can succeed end-to-end: `pool_acquired`, `in_cast_range_current`, `cast_started`, and `fishing_loot_success` all fired and `packets_TESTBOT1.csv` was recorded.
- Latest 2026-04-02 focused FG rerun on the current binaries is green and captured the full success chain from the packet-capture dock: `pool_acquired`, `in_cast_range_current`, `cast_started`, `loot_bag_delta items=[6361]`, `fishing_loot_success`, and `pop reason=fishing_loot_success`.
- A separate earlier focused live fishing pass also proved the runtime contract can succeed end-to-end on live data: BG completed a live catch with `skill 75 -> 76`, `bestPool=17.3y`, `lootSuccess=True`, and `catchDelta=[6358]`.
- The newest runtime delta tightens search-walk travel targets again: an unreachable `8y` local step now falls back to a reachable `4y` or `2y` step before the task gives up on that probe.
- Latest 2026-04-02 focused dual rerun on the current binaries is green: FG completed `fishing_loot_success` with loot item `[6303]`, and BG completed `fishing_loot_success` with loot item `[6358]`.
- Important scope note: this latest green dual rerun acquired immediate local pools and never entered `search_walk`, so the blocked-corner guard is currently proven by deterministic coverage plus the earlier live stuck-generation diagnostics rather than by a fresh staged-search live rerun.
- Because the harness now keeps staged visibility explicit, the remaining open work is no longer basic focused dual runtime completion. It is the comparison/instrumentation slice: keep the green FG and dual live baselines, then tighten authoritative staged visibility / `.pool spawns` attribution on nondeterministic reruns and perform the actual FG/BG packet-sequence comparison.

## Current Focus

- keep the focused fishing slice meaningful with both bots asserted from the Ratchet named teleport
- keep the success contract tied to the bobber-interact -> loot-window -> bag-delta sequence
- treat staged Ratchet visibility and authoritative `.pool spawns` capture as the first blocker on reruns that do not surface an immediate local pool, then treat any post-acquisition failure as shoreline/pathfinding/LOS work
- use FG as the live packet/timing reference; the focused FG packet-capture slice is green again on the current binaries
- keep the `MovementStuckRecoveryGeneration` search-walk guard covered whenever the dual slice re-enters local pier search
