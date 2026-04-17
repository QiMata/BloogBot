# Root / Unroot State (`SMSG_FORCE_MOVE_ROOT` / `SMSG_FORCE_MOVE_UNROOT`)

## Primary Evidence
- `docs/physics/smsg_force_move_root_handler.md`
- `docs/physics/packet_ack_timing.md`
- Managed implementation: `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`

## WoW.exe State Machine
1. `SMSG_FORCE_MOVE_ROOT` reaches `0x61A700(..., 1)`; `SMSG_FORCE_MOVE_UNROOT` reaches `0x61A700(..., 0)`.
2. `0x61A700` does not mutate movement flags inline.
3. Instead it selects queue slot `0x23` (root) or `0x24` (unroot) and stages the event through `0x617570`.
4. A later consumer performs the actual state change and ACK work.

## Managed State Machine
1. `EventEmitter_OnForceMoveRoot(...)` and `EventEmitter_OnForceMoveUnroot(...)` call `QueueDeferredRootChange(...)`.
2. The deferred queue stores `(guid, counter, ack opcode, applyRoot)`.
3. `FlushPendingDeferredMovementChanges(gameTimeMs)` applies the queued root or unroot transition to the local player:
   - root sets `MOVEFLAG_ROOT` and clears moving flags
   - unroot clears `MOVEFLAG_ROOT`
4. BG emits the corresponding ACK only during that deferred flush, not inline from the packet event.

## Audit Result
- Managed root/unroot handling matches the binary staging model instead of mutating flags and ACKing directly in the packet leaf.
- Current packet-flow tests already pin the deferred apply-then-ACK order for the representative root case.
