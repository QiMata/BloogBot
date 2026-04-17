# Teleport State (`MSG_MOVE_TELEPORT` / `MSG_MOVE_TELEPORT_ACK`)

## Primary Evidence
- `docs/physics/msg_move_teleport_handler.md`
- `docs/physics/packet_ack_timing.md`
- `docs/physics/0x602FB0_disasm.txt`
- Managed implementation: `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`

## WoW.exe State Machine
1. `MSG_MOVE_TELEPORT` (`0x0C5`) reaches `0x602F90 -> 0x6186B0` and applies the local teleport state.
2. The inbound teleport leaf does not emit the outbound ACK.
3. A later internal path, `0x602FB0`, evaluates the `0x468570` readiness gate.
4. Only when that gate passes does WoW.exe build and send `MSG_MOVE_TELEPORT_ACK` (`0x0C7`) through `0x5AB630` at `0x60308D`.

## Managed State Machine
1. `MovementHandler.HandleUpdateMovement(...)` sees `MSG_MOVE_TELEPORT`, calls `NotifyTeleportIncoming(...)`, and queues the position update.
2. `NotifyTeleportIncoming(...)` sets `_isBeingTeleported = true` and clears local movement state through `ResetMovementStateForTeleport(...)`.
3. `EventEmitter_OnTeleport(...)` records `_pendingTeleportAck = (guid, counter, targetPosition)`.
4. `TryFlushPendingTeleportAck()` waits for:
   - local player GUID match
   - `HasEnteredWorld == true`
   - `HasPendingWorldEntry == false`
   - `_isInControl == true`
   - pending object updates drained
   - `_movementController.NeedsGroundSnap == false`
   - local position resolved to the pending target
5. Once ready, BG sends `MSG_MOVE_TELEPORT_ACK`, clears `_pendingTeleportAck`, and clears `_isBeingTeleported`.

## Audit Result
- The managed side now matches the binary split between "teleport applied" and "teleport ACK emitted".
- The unsupported scene-data gate was removed earlier because the only binary-backed readiness fact is the internal `0x468570` gate, not a tile probe.
- The remaining open question is the exact meaning of `0x468570`; current parity only uses proven readiness facts, not guessed internals.
