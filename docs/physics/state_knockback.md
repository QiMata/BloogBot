# Knockback State (`SMSG_MOVE_KNOCK_BACK`)

## Primary Evidence
- `docs/physics/smsg_move_knock_back_handler.md`
- `docs/physics/packet_ack_timing.md`
- Managed implementation: `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`

## WoW.exe State Machine
1. `SMSG_MOVE_KNOCK_BACK` dispatches through `0x603F90 -> 0x602780 -> 0x602670`.
2. `0x602670` parses the payload and forwards it into `0x617A30 -> 0x6177A0`.
3. `0x6177A0` stages the knockback in queue slot `0x1C` and captures the current movement counter from `local + 0x128`.
4. There is no inline ACK send from `0x602670`; the ACK timing belongs to the later queue consumer.

## Managed State Machine
1. `EventEmitter_OnForceMoveKnockBack(...)` converts the packet into pending local impulse state:
   - `_pendingKnockbackVelX/Y/Z`
   - `_hasPendingKnockback = true`
   - `_pendingKnockbackAck = (guid, counter)`
   - local movement flags updated to `MOVEFLAG_JUMPING`, with
     `MOVEFLAG_FALLINGFAR` cleared and directional intent preserved
   - fall time reset and jump fields primed from the server-provided
     horizontal/vertical values
2. `MovementController.Update(...)` consumes the pending impulse on the next physics tick through `TryConsumePendingKnockback(...)`.
3. The movement controller preserves primed jump fields for ACK parity, or derives them from the consumed vector if no server fields were staged.
4. Only after physics consumes the staged impulse does BG send `CMSG_MOVE_KNOCK_BACK_ACK` through `TryFlushPendingKnockbackAck(...)`.
5. BG then emits the normal local movement sequence for the airborne arc, including `MSG_MOVE_JUMP`, heartbeat(s), and `MSG_MOVE_FALL_LAND` when the arc lands.

## Audit Result
- Managed knockback now matches the queue-first WoW.exe shape: stage first, consume in physics, ACK later.
- The earlier inline-ACK race is closed and covered by deterministic parity tests.
- Live Taragaman `Uppercut` fixtures pin the current packet-window shape:
  `foreground_knockback_baseline.json` and
  `background_knockback_baseline.json`.
