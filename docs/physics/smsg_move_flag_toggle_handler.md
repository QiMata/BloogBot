# SMSG_MOVE_* Flag Toggle Handlers

## Scope
This note covers the inbound flag-toggle family handled by `0x603F90 -> 0x602780`:

- `SMSG_MOVE_WATER_WALK` (`0x0DE`)
- `SMSG_MOVE_LAND_WALK` (`0x0DF`)
- `SMSG_MOVE_FEATHER_FALL` (`0x0F2`)
- `SMSG_MOVE_NORMAL_FALL` (`0x0F3`)
- `SMSG_MOVE_SET_HOVER` (`0x0F4`)
- `SMSG_MOVE_UNSET_HOVER` (`0x0F5`)

## Primary Evidence
- `docs/physics/0x602780_disasm.txt`
- `docs/physics/0x61A380_disasm.txt`
- `docs/physics/0x61A490_disasm.txt`
- `docs/physics/0x61A5D0_disasm.txt`
- `docs/physics/0x61A430_disasm.txt`
- `docs/physics/0x61A550_disasm.txt`
- `docs/physics/0x617570_disasm.txt`

## Dispatch
- `0x0DE -> 0x61A380(..., 1)` and `0x0DF -> 0x61A380(..., 0)`
- `0x0F2 -> 0x61A490(..., 1)` and `0x0F3 -> 0x61A490(..., 0)`
- `0x0F4 -> 0x61A5D0(..., 1)` and `0x0F5 -> 0x61A5D0(..., 0)`

The queue slots derived in the leaves are:

- water walk / land walk: `0x21` / `0x22`
- feather fall / normal fall: `0x1D` / `0x1E`
- hover / unhover: `0x1F` / `0x20`

## What the Inbound Leaves Prove
`0x61A380`, `0x61A490`, and `0x61A5D0` all have the same shape:

1. zero a small temporary payload block
2. derive the slot id from the set/clear boolean
3. call `0x617570`

There is no direct call to `0x600A30`, `0x60E0A0`, or `0x5AB630` in these first-stage inbound leaves.

## Related Local-Apply Paths
- `0x61A430` is the local water-walk apply helper reached from the `MSG_MOVE_WATER_WALK` side.
- `0x61A550` is the local feather-fall apply helper reached from the `MSG_MOVE_FEATHER_FALL` side.

Those local helpers compare the incoming flag bit against the current movement flags and then call `0x618F20`, which is the same gate the speed-change family uses before local mutation.

## Binary-Backed Conclusions
- The inbound flag toggles are staged first and applied later.
- WoW.exe distinguishes set vs clear with stable queue slot ids rather than separate inline send routines.
- The negative evidence matters here: because the first-stage inbound leaves never emit bytes directly, the timing and byte-parity work for these ACKs belongs to the later queue consumer.
