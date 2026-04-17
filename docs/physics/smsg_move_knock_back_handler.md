# SMSG_MOVE_KNOCK_BACK

## Primary Evidence
- `docs/physics/0x603F90_disasm.txt`
- `docs/physics/0x602780_disasm.txt`
- `docs/physics/0x602670_disasm.txt`
- `docs/physics/0x617570_disasm.txt`

## Dispatch
- `SMSG_MOVE_KNOCK_BACK` (`0x0EF`) is wired to `0x603F90`.
- `0x603F90` forwards to `0x602780`.
- `0x602780` hits the `0x6028F9` branch for opcode `0x0EF`, then calls `0x602670`.

## What 0x602670 Does
`0x602670` is the inbound knockback leaf. It:

1. Calls `0x60E990(1, 0)` to prepare the movement path.
2. Reads four values from the packet stream with `0x419130`.
3. Forwards those values into `0x617A30` on `CMovement + 0x9A8`.

`0x617A30` is a thin wrapper that hardcodes slot `0x1C` and forwards into `0x6177A0`.

## Queue Semantics
`0x6177A0` and `0x617730` show the actual staging behavior:

- the parsed movement payload is copied into the pending movement queue at `self + 0x150`
- if the queued record is new, `0x630A40()->0x128` is captured and later compared through `0x619030` / `0x619090`

## Binary-Backed Conclusions
- The inbound knockback leaf does not emit bytes directly. There is no `0x600A30`, `0x60E0A0`, or `0x5AB630` call inside `0x602670`.
- WoW.exe stages the knockback through queue slot `0x1C` first, together with the current movement counter from `local + 0x128`.
- That queue-first shape is the binary proof that knockback ACK ordering must be solved against the later queue consumer, not by patching `0x602670`-equivalent logic inline.
