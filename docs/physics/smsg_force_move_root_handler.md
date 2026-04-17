# SMSG_FORCE_MOVE_ROOT / SMSG_FORCE_MOVE_UNROOT

## Primary Evidence
- `docs/physics/0x603F90_disasm.txt`
- `docs/physics/0x602780_disasm.txt`
- `docs/physics/0x61A700_disasm.txt`
- `docs/physics/0x617570_disasm.txt`

## Dispatch
- `SMSG_FORCE_MOVE_ROOT` (`0x0E8`) reaches `0x61A700(..., 1)` from `0x60285E`.
- `SMSG_FORCE_MOVE_UNROOT` (`0x0EA`) reaches `0x61A700(..., 0)` from `0x60287E`.

## What 0x61A700 Proves
`0x61A700` does not mutate `[CMovement + 0x40]` inline and does not call an outbound send helper. Instead it:

1. Zeroes a small temporary payload block on the stack.
2. Converts the boolean root/unroot selector into a queue slot:
   - nonzero selector -> slot `0x23`
   - zero selector -> slot `0x24`
3. Calls `0x617570` with that slot id and `queueInline=1`.

## Why This Matters
- The first-stage root handler is a staging function, not the final mutator.
- Because `0x61A700` never calls `0x600A30`, `0x60E0A0`, or `0x5AB630`, the root ACK is not emitted inline from the packet leaf itself.
- The binary evidence here supports a deferred root/unroot flow: `ProcessMessage` dispatches to `0x61A700`, `0x61A700` queues a typed movement event, and a later consumer performs the actual state change / ACK work.
