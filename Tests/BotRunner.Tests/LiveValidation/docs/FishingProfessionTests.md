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
- `Exports/WoWSharpClient/Movement/MovementController.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
- `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
- `Exports/WoWSharpClient/Frames/NetworkLootFrame.cs`
- `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
- `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
- `Services/ForegroundBotRunner/Frames/FgLootFrame.cs`

## Test Flow

1. `EnsureCleanSlateAsync()` runs for both bots.
2. Both bots learn fishing, set fishing skill to `75`, clear items, receive a fishing pole plus `Nightcrawler Bait`, and teleport to Ratchet.
3. The test probes dock-stage candidates and keeps only stable landings that expose a live visible fishing-hole object inside `FishingTask` detect range but outside immediate cast range. If Ratchet has no live visible pool at probe time, the test skips instead of using DB-only spawn coordinates.
4. The test dispatches `ActionType.StartFishing` for BG and FG.
5. `FishingTask` owns:
   - pole equip
   - bait application to the equipped pole
   - pool acquisition
   - pool approach
   - cast start
   - channel / bobber confirmation
   - loot-window handling
   - completion after a newly looted catch appears in bags

## Assertions

Both bots must show all of the following:

- pole started in bags and was removed from bags by `FishingTask`
- bait started in bags and was consumed by `FishingTask`
- initial visible pool distance was outside cast range but inside detect range
- `FishingTask pool_acquired`
- `FishingTask in_cast_range`
- fishing channel observed
- fishing bobber observed
- `FishingTask loot_window_open`
- `FishingTask fishing_loot_success`
- a non-pole bag delta after the loot window closes

## FG/BG Runtime Notes

- BG bite handling comes from `SMSG_GAMEOBJECT_CUSTOM_ANIM -> SpellHandler.HandleGameObjectCustomAnim(...) -> InteractWithGameObject(...)`.
- FG now mirrors that behavior through the injected client's recv hook:
  `PacketLogger.OnPacketCaptured -> ForegroundBotWorker.HandleCapturedPacket(...) -> ObjectManager.TryAutoInteractFishingBobberFromPacket()`.
- The live pass condition is no longer “skill-up or any loot-window signal.” It is `bobber observed -> loot_window_open -> fishing_loot_success -> catch item appears in bags`.

## Validation

```powershell
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingTaskTests|FullyQualifiedName~FishingDataTests|FullyQualifiedName~ActionMessage_AllTypes_RoundTrip|FullyQualifiedName~UseItemTaskTests" --logger "console;verbosity=minimal"
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 15m --logger "console;verbosity=minimal"
```

Latest focused results:
- `FishingTaskTests|FishingDataTests|ActionMessage_AllTypes_RoundTrip|UseItemTaskTests` -> `48 passed`
- `MovementControllerTests` -> `38 passed`
- `FishingProfessionTests` -> `1 skipped` when no live Ratchet fishing-hole object was visible from any stable dock stage; the suite now skips instead of running on DB-only spawn assumptions

## Current Focus

- keep the focused fishing slice green with both bots asserted
- keep the success contract tied to the bobber-interact -> loot-window -> bag-delta sequence
- use FG as the live packet/timing reference for the future BG botrunner parity work
- keep the live precondition meaningful: only run the fishing task when a real visible pool exists
- BG `MovementController` forced-stop handling now clears forward/strafe intent while preserving falling/swimming physics flags; FG Ratchet pier overrun recovery still needs follow-up
