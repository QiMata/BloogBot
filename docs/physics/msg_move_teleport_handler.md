# MSG_MOVE_TELEPORT / MSG_MOVE_TELEPORT_ACK

## Primary Evidence
- `docs/physics/0x601580_disasm.txt`
- `docs/physics/0x602780_disasm.txt`
- `docs/physics/0x602FB0_disasm.txt`

## Two Distinct Paths

### 1. Local teleport apply (`MSG_MOVE_TELEPORT`, opcode `0x0C5`)
- `0x601580` dispatches opcode `0x0C5` through `0x6016E2 -> 0x602F90`.
- `0x602F90` is thin: it forwards to `0x6186B0` on `CMovement + 0x9A8`.
- There is no packet construction or `0x5AB630` send in `0x602F90`.

### 2. Teleport ACK network send (`MSG_MOVE_TELEPORT_ACK`, opcode `0x0C7`)
- `opcode_dispatch_table.md` shows `0x0C7` statically registered to `0x603F90`.
- `0x603F90 -> 0x602780`.
- `0x602780` takes the `0x6028DE` branch for `0x0C7` and calls `0x602FB0`.

## What 0x602FB0 Proves
`0x602FB0` parses a full movement payload through `0x47EBA0`, then splits into two behaviors:

- if `0x468570` returns zero, it calls `0x60E990(1, 0)` and then `0x618720` on `CMovement + 0x9A8`
- otherwise it builds a packet serializer with opcode `0x0C7` at `0x603036`, writes the object guid and two stack arguments into that serializer, and sends through `0x5AB630` at `0x60308D`

## Binary-Backed Conclusions
- WoW.exe does not emit the network teleport ACK from the `0x602F90` local teleport leaf.
- The outbound `0x0C7` packet is emitted later from the internal `0x602FB0` path after the `0x468570` gate.
- That split is the key timing fact for parity work: "teleport applied" and "teleport ACK sent" are not the same branch in WoW.exe.
