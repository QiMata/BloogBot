# MSG_MOVE_WORLDPORT_ACK

## Primary Evidence
- `docs/physics/0x401B00_disasm.txt`
- `docs/physics/0x401BC0_disasm.txt`
- `docs/physics/0x401DE0_disasm.txt`
- `docs/physics/0x616800_disasm.txt`

## SMSG_NEW_WORLD (`0x401B00`)
`0x401B00` is the first-stage new-world handler:

1. reads map id plus XYZ/facing into globals at `0x88262C`, `0x88268C`, `0x882690`, `0x882694`, and `0x882680`
2. validates the map id against `0xC0DAA8`
3. stores the resolved world pointer at `0x8826E4`
4. schedules callback `0x401BC0` through `0x4200A0`

There is no `0xDC` packet send in `0x401B00` itself.

## Deferred callback (`0x401BC0`)
`0x401BC0` performs the heavy world-entry work:

- resets scene / world state
- calls `0x616910`
- schedules `0x616800` through `0x41FD80`, which seeds the movement-counter state for the local mover
- finishes the world transition and camera setup

The actual `MSG_MOVE_WORLDPORT_ACK` send is the block at `0x401CA5..0x401CF4`:

1. build serializer with opcode `0x0DC`
2. call `0x5AB630`

That send block is guarded by `cmp edi, esi` at `0x401CA5`. If `edi == 0`, the ACK block is skipped.

## SMSG_LOGIN_VERIFY_WORLD (`0x401DE0`)
`0x401DE0` reads the same map / XYZ / facing payload and stores the same globals, but its last step is different:

- `0x401EA4` calls `0x401BC0` directly with `edx = 0`

Because `edi` is loaded from `edx` at `0x401BCB`, this direct login-verify call skips the `0xDC` send block.

## Binary-Backed Conclusions
- `MSG_MOVE_WORLDPORT_ACK` is emitted from the deferred `0x401BC0` callback, not from `0x401B00`.
- The login-verify path (`0x401DE0`) reuses the same callback body but passes `edx = 0`, so it does not send `0xDC`.
- The worldport ACK is therefore tied to the scheduled new-world transition path, not to every world-coordinate update packet.
