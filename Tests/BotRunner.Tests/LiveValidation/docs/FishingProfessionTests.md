# FishingProfessionTests

Dual-bot live validation for the task-owned Ratchet fishing path.

## Purpose

This suite proves that both BG and FG run the same high-level fishing contract:

1. teleport to Ratchet and resolve a stable dock stage with a visible pool
2. dispatch `ActionType.StartFishing`
3. let `FishingTask` equip the pole, move into range, cast, wait for the bite, open the loot window, and finish after the catch reaches bags

## Production Links

- `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/Tasks/BotTask.cs`
- `Exports/BotRunner/Tasks/FishingTask.cs`
- `Exports/BotRunner/Combat/FishingData.cs`
- `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
- `Exports/WoWSharpClient/Frames/NetworkLootFrame.cs`
- `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
- `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
- `Services/ForegroundBotRunner/Frames/FgLootFrame.cs`

## Test Flow

1. `EnsureCleanSlateAsync()` runs for both bots.
2. Both bots learn fishing, set fishing skill, clear items, receive a fishing pole, and teleport to Ratchet.
3. The test probes dock-stage candidates and keeps only stable landings that expose a visible pool inside `FishingTask` detect range but outside immediate cast range.
4. The test dispatches `ActionType.StartFishing` for BG and FG.
5. `FishingTask` owns:
   - pole equip
   - pool acquisition
   - pool approach
   - cast start
   - channel / bobber confirmation
   - loot-window handling
   - completion after a newly looted catch appears in bags

## Assertions

Both bots must show all of the following:

- pole started in bags and was removed from bags by `FishingTask`
- initial visible pool distance was outside cast range but inside detect range
- `FishingTask pool_acquired`
- `FishingTask in_cast_range`
- fishing channel observed
- fishing bobber observed
- `FishingTask loot_window_open` or a terminal `FishingTask fishing_loot_success` marker
- `FishingTask fishing_loot_success`
- a non-pole bag delta after the loot window closes

## FG/BG Runtime Notes

- BG bite handling comes from `SMSG_GAMEOBJECT_CUSTOM_ANIM -> SpellHandler.HandleGameObjectCustomAnim(...) -> InteractWithGameObject(...)`.
- FG now mirrors that behavior through the injected client's recv hook:
  `PacketLogger.OnPacketCaptured -> ForegroundBotWorker.HandleCapturedPacket(...) -> ObjectManager.TryAutoInteractFishingBobberFromPacket()`.
- The live pass condition is no longer “skill-up or any loot-window signal.” It is an actual catch item appearing in bags after the bobber interaction path.

## Validation

```powershell
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~FishingDataTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip" --logger "console;verbosity=minimal"
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"
```

Latest focused results:
- `FishingTaskTests|FishingDataTests|ActionMessage_AllTypes_RoundTrip` -> `44 passed`
- `FishingProfessionTests` -> `1 passed`

## Current Focus

- keep the focused fishing slice green with both bots asserted
- keep the success contract tied to actual loot reaching bags
- use FG as the live packet/timing reference for the future BG botrunner parity work
- fix shoreline movement parity next: FG can still run off the Ratchet pier if movement stop arrives after the overrun has already started, and BG `MovementController` needs the same stop/fall-state rigor
