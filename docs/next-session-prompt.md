```prompt
## Context

This is the BloogBot (Westworld of Warcraft) project — a WoW 1.12.1 bot with dual-bot architecture (ForegroundBotRunner DLL injection + BackgroundBotRunner headless protocol emulation). Read `CLAUDE.md` for full architecture overview and `docs/TASKS.md` for the task list.

## What Was Accomplished This Session

### FishingProfessionTests — FULLY PASSING (dual-client BG + FG)

Fixed three issues preventing the BG (headless) bot from fishing:

1. **SOAP `.teleport name` with coordinates is invalid** — silently fails. Switched to `BotTeleportAsync` (`.go xyz` via chat) for both bots. MaNGOS sends `MSG_MOVE_TELEPORT` back; `NotifyTeleportIncoming()` ensures the position write guard allows the update.

2. **Added `SET_FACING` ActionType (proto 63)** — Characters face toward water before casting. Implemented end-to-end: proto → Communication.cs → CharacterAction → BotRunnerService behavior tree handler.

3. **Corrected dock position** — Mapped dock geometry from MaNGOS creature positions. Dock is ~5yd wide (X: -991 to -986), southern tip at Y≈-3836. Fishing position: (-988.5, -3834.0, 5.7) centered on dock, facing 6.21 rad (east toward fishing node).

### Documentation Updated
- MEMORY.md — Added teleport/facing lessons, updated test safety notes
- ARCHIVE.md — Added teleport fix + SET_FACING entry
- TASKS.md — Updated FishingProfessionTests status

## What to Work On Next

### Priority 1: Bobber activation (catching fish)
FishingProfessionTests currently detects the bobber (Fishing Bobber displayId=668, type=17) but doesn't activate it to actually catch fish. Next step:
- Send CMSG_GAMEOBJ_USE on the bobber GUID to activate it when it splashes
- Verify loot window opens with fish
- This exercises the full fishing → loot pipeline

### Priority 2: Run remaining LiveValidation tests
8 tests pass (BasicLoopTests 6 + FishingProfessionTests 1 + GroupFormationTests 1). See TASKS.md section 1 for the other 11 test classes.

### Priority 3: Implement Phase 2 from plan (Atomic BotTask architecture)
Plan file: `C:\Users\lrhod\.claude\plans\parsed-greeting-bengio.md`

## Key Technical Details

- **SET_FACING**: proto 63, 1 float param (radians). Formula: `atan2(targetY - srcY, targetX - srcX)`, add 2π if negative.
- **Teleport**: Use `.go xyz` via chat (`BotTeleportAsync`), NOT SOAP `.teleport name` with coordinates. SOAP `.tele name <player> <location>` works for named locations only.
- **Position write guard**: `NotifyTeleportIncoming()` must be called BEFORE `QueueUpdate`, not after.
- **Ratchet dock**: 5yd wide (X: -991 to -986), surface Z≈5.7, southern tip Y≈-3836. Safe fishing: (-988.5, -3834.0, 5.7).
- **Key Ports**: 3724 (auth), 8085 (world), 3306 (MySQL root/root), 7878 (SOAP), 5001 (PathfindingService), 5002 (IPC), 8088 (StateManager API)

## Files Modified This Session

- `Exports/WoWSharpClient/Handlers/MovementHandler.cs` — NotifyTeleportIncoming() + ACK handler fix
- `Exports/WoWSharpClient/WoWSharpObjectManager.cs` — Added NotifyTeleportIncoming() method
- `Exports/BotCommLayer/Models/ProtoDef/communication.proto` — SET_FACING = 63
- `Exports/BotCommLayer/Models/Communication.cs` — SetFacing enum value
- `Exports/GameData.Core/Enums/CharacterAction.cs` — SetFacing enum value
- `Exports/BotRunner/BotRunnerService.cs` — SetFacing mapping + behavior tree handler
- `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs` — Dock position, facing, .go xyz teleport
- `docs/TASKS.md`, `docs/ARCHIVE.md`, `docs/next-session-prompt.md` — Updated
```
