# `MSG_MOVE_TIME_SKIPPED`, `MSG_MOVE_JUMP`, and `MSG_MOVE_FALL_LAND`

## Dispatch anchors

- `MSG_MOVE_JUMP (0x0BB)` enters the shared movement wrapper `0x603BB0`, lands in `0x601580`, and dispatches to `0x602B00 -> 0x617970`.
- `MSG_MOVE_FALL_LAND (0x0C9)` enters the same `0x603BB0 -> 0x601580` path and dispatches to `0x602C20 -> 0x61A750`.
- `MSG_MOVE_TIME_SKIPPED (0x319)` uses the dedicated wrapper `0x603B40`, which calls `0x601560 -> 0x61AB90`.

The `0x601580` jump-table mapping is visible in the raw `0x601844` / `0x6018AC` tables:

- case id `0x06` (`MSG_MOVE_JUMP`) -> `0x601676 -> 0x602B00`
- case id `0x10` (`MSG_MOVE_FALL_LAND`) -> `0x6017A5 -> 0x602C20`

## `MSG_MOVE_JUMP`

`0x617970` parses the packet through `0x618F20(..., 0x07, 0)` and, on success, immediately calls:

- `0x7C6230` `CMovement::BeginJump(1)`
- `0x619CA0`

The important state mutation happens in `0x7C6230`:

- rejects the jump when blocking transport / movement-state bits are present
- selects the initial vertical speed (`-9.096748f` for the swim path, `-7.955547f` otherwise)
- delegates to `0x7C61F0`

`0x7C61F0` is the shared fall-entry helper:

- sets `MOVEFLAG_JUMPING`
- zeroes `fallTime` (`+0x78`)
- copies current `Z` into `fallStartZ` (`+0x7C`)
- stores the initial vertical speed into `+0xA0`

For BG parity, the local-player jump hook must at minimum enter airborne state and zero the local fall timer even when normal server movement overwrites are suppressed.

## `MSG_MOVE_FALL_LAND`

`0x61A750` is a thin wrapper over `0x618F20(..., 0x25, 0)`. There is no extra post-parse helper call in the visible wrapper, so the landing packet's authoritative state comes from the parsed movement payload itself.

For BG parity, this matters because local-player server movement overwrites are intentionally suppressed while the client is in control. Without an explicit landing hook, `MOVEFLAG_JUMPING` / `MOVEFLAG_FALLINGFAR` and `fallTime` can remain stale on the BG local-player model even though WoW.exe has already consumed the landing packet.

## `MSG_MOVE_TIME_SKIPPED`

`0x603B40 -> 0x601560 -> 0x61AB90` is a short leaf path:

```cpp
// 0x61AB90
this->field_AC += packetDeltaMs;
```

The visible behavior is "add the packet's 32-bit delta into a movement-local time accumulator." The exact member name at `+0xAC` is still unlabeled in the broader movement-structure notes, but the packet purpose and the leaf body together support the parity rule:

- treat the packet payload as a movement-time delta in milliseconds
- advance the outbound movement timestamp base by that delta

That is the BG-side fix used for G2.
