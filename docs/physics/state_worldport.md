# Worldport State (`SMSG_NEW_WORLD` -> `MSG_MOVE_WORLDPORT_ACK`)

## Primary Evidence
- `docs/physics/msg_move_worldport_ack.md`
- `docs/physics/0x401B00_disasm.txt`
- `docs/physics/0x401BC0_disasm.txt`
- Managed implementation: `Exports/WoWSharpClient/Handlers/LoginHandler.cs`, `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`

## WoW.exe State Machine
1. `SMSG_NEW_WORLD` enters `0x401B00`, which stores map / XYZ / facing into globals and schedules callback `0x401BC0`.
2. `0x401BC0` performs the heavy world-entry work and seeds local movement-counter state via `0x616910 -> 0x616800`.
3. The actual `MSG_MOVE_WORLDPORT_ACK` send lives at `0x401CA5..0x401CF4` inside the deferred callback, not in `0x401B00`.

## Managed State Machine
1. `LoginHandler.HandleNewWorld(...)` parses map / XYZ / facing.
2. BG immediately forwards that payload through `FireOnLoginVerifyWorld(...)` so the local player map and position update before the ACK.
3. BG then sends `MSG_MOVE_WORLDPORT_ACK` through `SendWorldportAck()`.
4. `EventEmitter_OnLoginVerifyWorld(...)` clears pending world-entry state, updates player map/position/facing, resets the movement controller, and restarts the game loop.

## Audit Result
- The managed worldport state correctly ties `MSG_MOVE_WORLDPORT_ACK` to the `SMSG_NEW_WORLD` path, not to every world-coordinate update.
- The current tests already pin that the ACK is emitted only after the world-info mutation and only once per `SMSG_NEW_WORLD`.
