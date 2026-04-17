# SMSG_FORCE_*_SPEED_CHANGE Handlers

## Scope
This note covers the speed-change family wired through `0x603F90 -> 0x602780`:

- `SMSG_FORCE_RUN_SPEED_CHANGE` (`0x0E2`)
- `SMSG_FORCE_RUN_BACK_SPEED_CHANGE` (`0x0E4`)
- `SMSG_FORCE_SWIM_SPEED_CHANGE` (`0x0E6`)
- `SMSG_FORCE_WALK_SPEED_CHANGE` (`0x2DA`)
- `SMSG_FORCE_SWIM_BACK_SPEED_CHANGE` (`0x2DC`)
- `SMSG_FORCE_TURN_RATE_CHANGE` (`0x2DE`)

## Primary Evidence
- `docs/physics/0x603F90_disasm.txt`
- `docs/physics/0x602780_disasm.txt`
- `docs/physics/0x619500_disasm.txt`

## Dispatch Map
- `0x0E2 -> 0x6027D7 -> 0x619500` (queue helper, slot `0x14`)
- `0x0E4 -> 0x602804 -> 0x619590` (queue helper, slot `0x15`)
- `0x0E6 -> 0x602831 -> 0x6196B0` (queue helper, slot `0x17`)
- `0x2DA -> 0x6029FD -> 0x619620` (queue helper, slot `0x16`)
- `0x2DC -> 0x6029D0 -> 0x619740` (queue helper, slot `0x18`)
- `0x2DE -> 0x6029A3 -> 0x6197D0` (queue helper, slot `0x19`)

Each `0x6195xx/0x6196xx/0x6197xx` queue helper is paired with an apply helper in the same file:

- run speed: `0x619550 -> 0x7C7030`
- run back speed: `0x6195E0 -> 0x7C7080`
- walk speed: `0x619670 -> 0x7C70D0`
- swim speed: `0x619700 -> 0x7C7120`
- swim back speed: `0x619790 -> 0x7C7170`
- turn rate: `0x619820 -> 0x7C6FF0`

## What WoW.exe Actually Does
1. `0x603F90` resolves the handler object and forwards to `0x602780`.
2. `0x602780` reads the speed payload out of the packet stream via `0x418EB0`.
3. The per-opcode leaf does not emit the ACK inline. The direct leaf that `0x602780` calls is always the queue helper (`0x619500`, `0x619590`, `0x619620`, `0x6196B0`, `0x619740`, or `0x6197D0`).
4. Each queue helper immediately packages a pending movement event through `0x6176A0` with the slot id above.
5. The nearby apply helpers (`0x619550`, `0x6195E0`, `0x619670`, `0x619700`, `0x619790`, `0x619820`) gate the actual mutation through `0x618F20`, then call the concrete speed setter `0x7C70xx`.

## Binary-Backed Conclusions
- The inbound speed-change leafs do not call `0x600A30`, `0x60E0A0`, or `0x5AB630`. There is no immediate network ACK send in the first-stage handler.
- The first-stage speed handler is queue-first. That is why the speed family matters for P2.3 timing parity: the bytes are not emitted from `0x602780` itself.
- The slot ids `0x14` through `0x19` are the stable discriminator values used by WoW.exe to stage the six speed-change subtypes before the later consumer decides when to apply or ACK them.
