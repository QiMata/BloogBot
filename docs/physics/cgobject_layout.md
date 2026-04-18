# `CGObject_C` / `CGUnit_C` / `CGPlayer_C` Layout Anchors

## Primary evidence
- `0x466C70` typed zero/init helper reached from the `SMSG_UPDATE_OBJECT` create path
- `docs/physics/0x466C70_disasm.txt` raw typed zero/init capture
- `0x466E00` typed object construction / recovery helper
- `0x46878A` base `CGObject_C` vfptr write (`0x803640`)
- `0x613B49` base `CGObject_C` vfptr reset during cleanup
- `0x464530`, `0x4644F0`, `0x464920` object-cache lookup / removal helpers
- `memory/wow_exe_physics_decompilation.md` `CMovement` offsets

## Confirmed base-object anchor

Current hard evidence still proves one concrete vtable write with confidence:

- `CGObject_C` vfptr = `0x803640`
- write site: `0x46878A`
- cleanup/reset site: `0x613B49`

That base evidence is documented in [cgobject_vtables.md](./cgobject_vtables.md) and remains the safe anchor for packet-parity work.

## Confirmed typed storage regions from `0x466C70`

The raw `0x466C70` capture now proves the switch shape directly:

- `0x466C73` compares `typeId` against `7`
- `0x466C81` jumps through the case table at `0x466DB8`
- only type ids `0..7` reach a typed zero/init branch here

That makes the packet-instantiated layout surface concrete: object, item,
container, unit, player, game object, dynamic object, and corpse are the only
typed storage cases in this create-path helper.

`0x466C70` is the best current layout anchor for typed object memory. It zeroes type-specific regions at exact offsets from the object base:

| Type id | Working class name | Region start | Region end | Size | Notes |
| --- | --- | --- | --- | --- | --- |
| `0` | `CGObject_C` | `+0x110` | `+0x128` | `0x18` | matches `OBJECT_END * 4` |
| `1` | `CGItem_C` | `+0x348` | `+0x408` | `0xC0` | matches `ITEM_END * 4` |
| `2` | `CGContainer_C` | `+0x6E0` | `+0x8C8` | `0x1E8` | larger than the current managed `CONTAINER_END` math; keep this binary value authoritative |
| `3` | `CGUnit_C` | `+0xE68` | `+0x1158` | `0x2F0` | matches `UNIT_END * 4` |
| `4` | `CGPlayer_C` remote | `+0x1D70` | `+0x2578` | `0x798` | remote-player visible subset |
| `4` | `CGPlayer_C` local | `+0x1D70` | `+0x3178` | `0x1408` | matches full `PLAYER_END * 4` |
| `5` | `CGGameObject_C` | `+0x288` | `+0x2F0` | `0x68` | matches `GAMEOBJECT_END * 4` |
| `6` | `CGDynamicObject_C` | `+0x1A0` | `+0x1E0` | `0x40` | matches `DYNAMICOBJECT_END * 4` |
| `7` | `CGCorpse_C` | `+0x2B8` | `+0x350` | `0x98` | matches `CORPSE_END * 4` |

## Player-specific finding

The local player gets a larger descriptor region than remote players:

- remote `CGPlayer_C` zero size = `0x798`
- local `CGPlayer_C` zero size = `0x1408`

`0x1408` is exactly `0x502 * 4`, which matches the full 1.12.1 `PLAYER_END` descriptor count. The smaller remote-player region is still binary-backed, but the exact semantic cutoff of that subset needs a dedicated follow-up capture.

The local/remote split is now tied to concrete branch sites instead of only the
derived sizes:

- `0x466D04..0x466D23` checks the object GUID against `0x468550` and selects the
  local-player `0x1408` branch
- `0x466D25..0x466D45` selects the remote-player `0x798` branch and zeroes the
  extra pointer at `+0x1C68`
- `0x466DE0` materializes the local-player tail pointer rooted at `+0x3178`

## Pet-type conclusion

`0x466C70` does **not** have a separate type case for `CGPet_C`.

- the helper rejects any type id above `7` at `0x466C73..0x466C7B`
- the switch table at `0x466DB8` contains only the eight packet-instantiated
  object families listed above

Safe conclusion for packet parity:

- pets arriving through `SMSG_UPDATE_OBJECT` reuse the unit/object storage paths
  already covered here
- `WoWLocalPet` remains a managed/runtime promotion over unit state, not a
  separately-instantiated update-object layout class in this path

## `CMovement` anchors

The object-to-`CMovement` pointer offset is not yet proven by this pass, but the movement struct itself is already pinned:

| `CMovement` offset | Meaning |
| --- | --- |
| `+0x10` | position.xyz |
| `+0x1C` | facing |
| `+0x20` | pitch |
| `+0x38` | guid |
| `+0x40` | movement flags |
| `+0x78` | fall time |
| `+0x84` | jump XY speed |
| `+0x88..+0x9C` | walk/run/swim/turn speeds |
| `+0xA0` | fall start velocity |
| `+0xB0` | collision skin |
| `+0xB4` | step height |

Those `CMovement` offsets remain authoritative for movement parity work; the missing piece is only the exact parent-object pointer offset.

## Cache / identity helper anchors

The object-cache helpers compare several stable identity fields:

- `0x464530` and `0x464890` walk the object cache and compare cached values at `+0x18`, `+0x30`, and `+0x34`
- `0x4644F0` removes an existing cached object after lookup and virtual cleanup
- `0x464920` is the shared GUID-list helper used by the direct type-`4` / type-`5` path

The precise semantic names for `+0x18`, `+0x30`, and `+0x34` still need a follow-up naming pass, so they are intentionally left unnamed here.

## Current safe conclusion

The binary now proves:

- exact typed storage regions for object, item, container, unit, player, game object, dynamic object, and corpse instances
- a distinct full-size local-player descriptor region
- the existing `CMovement` inner-field offsets

It still does **not** prove:

- the exact parent-object offset of the `CMovement` pointer/reference
- final names for every object-base member used by the cache helpers
- distinct `CGUnit_C` / `CGPlayer_C` vfptr writes beyond the confirmed `CGObject_C` base vfptr

For packet-parity work, use the offsets in this file as hard anchors and treat everything else as unresolved until a concrete write site is captured.
