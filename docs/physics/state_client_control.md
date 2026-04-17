# Client-Control State (`SMSG_CLIENT_CONTROL_UPDATE`)

## Primary Evidence
- `docs/physics/opcode_dispatch_table.md`
- `docs/physics/0x603EA0_disasm.txt`
- WoW.exe runtime disassembly of `0x5FA600`

## Dispatcher Anchor
- `opcode_dispatch_table.md` maps `SMSG_CLIENT_CONTROL_UPDATE` (`0x159`) to `0x603EA0`.
- The static registration site is `0x6038C6..0x6038D0`, which installs `0x603EA0` for opcode `0x159`.

## What `0x603EA0` Does
`0x603EA0` is not a parameterless "teleport complete" event. The handler:

1. reads a packed GUID through `0x642ED0`
2. reads a one-byte control flag through `0x418CB0`
3. looks up the target object via `0x468460`
4. normalizes the byte with `test/setne`
5. calls `0x5FA600(object, canControlBool)`

Relevant lines:
- `0x603EAF`: `call 0x642ED0`
- `0x603EBA`: `call 0x418CB0`
- `0x603ED6`: `call 0x468460`
- `0x603EE5`: `setne cl`
- `0x603EEB`: `call 0x5FA600`

## What `0x5FA600` Proves
The callee toggles a persistent control-state bit on the target object:

- `0x5FA621..0x5FA636` reads `[esi + 0xC58]`
- when `canControl != 0`, it `or`s bit `0x400`
- when `canControl == 0`, it clears bit `0x400`

The same function then compares the object's GUID against the active mover / current-player GUID and only runs the follow-up global update path on a match (`0x5FA641..0x5FA69B`).

## Binary-Backed Conclusions
- The packet's GUID matters. WoW.exe applies the update to the looked-up object, not unconditionally to "the player".
- The packet's byte matters. WoW.exe explicitly forwards `canControl` into `0x5FA600`; dropping it is a parity bug.
- A `canControl == false` packet is a persistent lockout bit flip, not a transient log-only signal.
- A `canControl == true` packet is the matching unlock edge.

## Managed Parity Implication
`WoWSharpObjectManager` should treat `SMSG_CLIENT_CONTROL_UPDATE` as:

- no-op for non-local GUIDs, because `_isInControl` models only the local mover state
- `_isInControl = false` plus a persistent explicit lockout when the local packet says `canControl = false`
- `_isInControl = true` and lockout clear when the local packet says `canControl = true`

That persistent lockout must block the game-loop reconciler; otherwise the next tick immediately undoes the server's `canControl = false` update, which diverges from WoW.exe's stored control bit.
