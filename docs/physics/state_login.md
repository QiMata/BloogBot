# Login World-Entry State (`EnterWorld` / `SMSG_LOGIN_VERIFY_WORLD`)

## Primary Evidence
- `docs/physics/msg_move_worldport_ack.md`
- `docs/physics/0x401DE0_disasm.txt`
- Managed implementation: `Exports/WoWSharpClient/WoWSharpObjectManager.cs`, `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`, `Exports/WoWSharpClient/Handlers/LoginHandler.cs`

## WoW.exe State Machine
1. Initial login world-entry reaches `0x401DE0` (`SMSG_LOGIN_VERIFY_WORLD`).
2. `0x401DE0` reads map / XYZ / facing and stores the same globals used by the far-teleport path.
3. Its final step is `call 0x401BC0` with `edx = 0`.
4. Because the deferred callback sees `edi == 0`, the `MSG_MOVE_WORLDPORT_ACK` send block is skipped for the login path.

## Managed State Machine
1. `EnterWorld(characterGuid)` sets `PlayerGuid`, flips `HasEnteredWorld = true`, stores `_pendingWorldEntryGuid`, and sends `CMSG_PLAYER_LOGIN`.
2. `SchedulePendingWorldEntryRetry(...)` retries `CMSG_PLAYER_LOGIN` until the world-entry state clears.
3. `LoginHandler.HandleLoginVerifyWorld(...)` parses map / XYZ / facing and fires `OnLoginVerifyWorld`.
4. `EventEmitter_OnLoginVerifyWorld(...)` clears `_pendingWorldEntryGuid`, updates local world state, resets the movement controller, and starts the game loop.
5. No worldport ACK is sent on this path.

## Audit Result
- Managed login world-entry matches the binary rule that `SMSG_LOGIN_VERIFY_WORLD` updates world state but does not emit `MSG_MOVE_WORLDPORT_ACK`.
- The pending-login retry state is BG-specific scaffolding around `CMSG_PLAYER_LOGIN`; it is not a WoW.exe packet-state divergence.
