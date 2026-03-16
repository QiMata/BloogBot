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

1. `EnsureCleanSlateAsync(..., teleportToSafeZone: false)` runs for both bots, and FG is checked with `CheckFgActionableAsync(requireTeleportProbe: false)`.
2. Both bots learn fishing, set fishing skill to `75`, clear items, receive a fishing pole plus `Nightcrawler Bait`, and teleport to Ratchet with the named GM teleport.
3. The test records the arrival position plus any immediately visible pool distance, but it no longer pre-selects dock stages or queries DB-only pool spawns.
4. The test dispatches `ActionType.StartFishing` for BG and FG.
5. `FishingTask` owns:
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
- The remaining intermittent failure mode is shoreline/pathfinding-bound. When approach movement stalls on terrain or never gets LOS to the water, the live evidence is `FishingTask los_blocked phase=move` and the WoW error `Your cast didn't land in fishable water`.
- The live failure path now appends recent snapshot errors plus recent BotRunner diagnostic lines, so shoreline/pathfinding failures are distinguishable from fishing-task regressions without opening process logs manually.

## Validation

```powershell
dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~FishingDataTests" --logger "console;verbosity=minimal"
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=minimal"
```

Latest focused results:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded
- `FishingTaskTests|FishingDataTests` -> `40 passed`
- Focused live fishing already proved the task-owned path can succeed end-to-end: BG completed a live catch with `skill 75 -> 76`, `bestPool=17.3y`, `lootSuccess=True`, and `catchDelta=[6358]`
- The dual-bot live test is still intermittent because shoreline/pathfinding can strand a bot before `FishingTask in_cast_range`; the latest FG failure ended with `FishingTask los_blocked phase=move castTarget=(-956.2,-3775.0,0.0)`
- The newest focused rerun on 2026-03-12 skipped before Ratchet staging because BG entered setup at `health=0/0` and did not reach strict-alive after `.revive`, so the new shoreline/pathfinding assertions were not exercised in that pass.

## Current Focus

- keep the focused fishing slice meaningful with both bots asserted from the Ratchet named teleport
- keep the success contract tied to the bobber-interact -> loot-window -> bag-delta sequence
- treat intermittent fishing failures as shoreline/pathfinding/LOS work unless the task contract itself regresses
- use FG as the live packet/timing reference after the shoreline/pathfinding approach is stable
- BG `MovementController` forced-stop handling now clears forward/strafe intent while preserving falling/swimming physics flags, but terrain sticking and no-LOS approach failures still need pathfinding follow-up
