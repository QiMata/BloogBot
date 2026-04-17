# Movement Counter Tracking

## Primary Evidence
- `docs/physics/0x616800_disasm.txt`
- `docs/physics/0x617570_disasm.txt`
- `docs/physics/0x619DE0_disasm.txt`
- `0x630A40 -> 0x4685E0`
- targeted callsites at `0x616A05`, `0x61714C`, `0x619D45`, `0x619D6B`, `0x619EAA`, `0x61AA94`, `0x61AAC1`, and `0x61AAF0`

## Owner Object
- `0x630A40` is a thin jump to `0x4685E0`.
- `0x4685E0` returns `[0xB41414 + 0xD4]` when that root pointer is non-null.
- The movement-counter fields are read from that returned object:
  - `+0x128`
  - `+0x12C`

## Initialization And Refresh
The counter is not maintained with a plain `inc` instruction. WoW.exe seeds and refreshes it from the current tick source returned by `0x42C010`.

- `0x6168DF -> 0x42C010`
- `0x6168E4`: `mov [local + 0x12C], eax`
- `0x6168EA`: `mov [local + 0x128], eax`

When time advances past the current value:

- `0x616800` fetches the current tick into `esi`
- `0x616840`: `mov [local + 0x12C], esi`
- `0x616846`: `mov [local + 0x128], esi`

That is the strongest binary proof in this phase: the movement counter is a sampled time value, not a standalone incrementing integer owned by `WriteMovementInfo`.

## Read Sites
### `local + 0x128`
- `0x616A05`: exported getter path
- `0x61714C`: passes the value into `0x600A30` for opcode `0x0EE`
- `0x61770A` / `0x61781C`: seeds pending-queue comparisons
- `0x6187F0`: used by the `0x7C6A50` path
- `0x618C74`: used in `0x618C30` gating
- `0x619B02`: passed into `0x61A820`
- `0x61AA94`, `0x61AAC1`, `0x61AAF0`: passed into `0x60E0A0` for opcodes `0x0BE`, `0x0C1`, and `0x0BA`

### `local + 0x12C`
- `0x616A2F`: computes `([local + 0x12C] - [local + 0x128])`
- `0x619D45`, `0x619D6B`, `0x619EAA`: feed the companion value into `0x619DE0`

## Queue Interaction
- `0x6177A0` / `0x617730` copy movement payloads into the pending queue at `self + 0x150`.
- When a new queued record is inserted and `needsCounter == 0`, `0x617817..0x61782D` fetches `local + 0x128` and compares the queued record against it through `0x619030` and `0x619090`.

## Binary-Backed Conclusions
- The ACK / movement counter lives on the object returned by `0x630A40`, not inside the serializer itself.
- WoW.exe refreshes `+0x128` and `+0x12C` from the current tick source in the `0x616800` family.
- Outbound ACK helpers read the counter first and then call the packet builders (`0x600A30` / `0x60E0A0`), which is why byte-parity work in P2.2 has to match the caller-side counter source rather than patching `WriteMovementInfo`.
