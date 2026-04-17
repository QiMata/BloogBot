# `SMSG_UPDATE_OBJECT` / `SMSG_COMPRESSED_UPDATE_OBJECT` Handler Notes

## Primary evidence
- `0x4651A0` top-level `CGWorldClient::HandleUpdateObject` dispatcher
- `0x466010` prepass dispatcher
- `0x465330` direct values/partial handler
- `0x465C50` direct create/create2 handler
- `0x465EA0` direct movement-only handler
- `0x465FD0` direct GUID-list handler used by both type `4` and type `5`
- `0x467230` type-`5` prepass helper
- `0x4644F0` stale-object lookup/remove helper used by the type-`5` prepass
- `0x466C70` typed object zero/init helper

## Top-level shape (`0x4651A0`)

`0x4651A0` is not a single linear "read a block and mutate state" routine. The function has three distinct stages:

1. Read `blockCount` with `0x418EB0`.
2. Read the extra byte that sits between `blockCount` and the first `ObjectUpdateType`.
3. Run one or two prepasses through `0x466010`, then rewind the stream and run the real per-block dispatch loop.

The direct dispatch jump table at `0x465314` resolves to:

| Update type | Value | Direct target | Proven behavior |
| --- | --- | --- | --- |
| `PARTIAL` | `0` | `0x465330` | values/descriptor path |
| `MOVEMENT` | `1` | `0x465EA0` | movement-only path |
| `CREATE_OBJECT` | `2` | `0x465C50` | create path |
| `CREATE_OBJECT2` | `3` | `0x465C50` | same create path, `create2` flag set |
| `OUT_OF_RANGE_OBJECTS` | `4` | `0x465FD0` | GUID-list path |
| `NEAR_OBJECTS` | `5` | `0x465FD0` | same direct GUID-list path as type `4` |

The prepass jump table at `0x466084` is different:

| Update type | Prepass target | Finding |
| --- | --- | --- |
| `PARTIAL` | `0x4660A0` | prepass work exists |
| `MOVEMENT` | `0x467050` | prepass work exists |
| `CREATE_OBJECT` | `0x4660A0` | same prepass as partial |
| `CREATE_OBJECT2` | `0x4660A0` | same prepass as partial |
| `OUT_OF_RANGE_OBJECTS` | no-op in `0x466010` | no dedicated prepass action |
| `NEAR_OBJECTS` | `0x467230` | dedicated stale-object cleanup prepass |

## Leading type-`4` special case

If the first real update block is type `4`, `0x4651A0` sets `startIndex = 1`, runs the mode-`1` prepass from block `1`, rewinds, then runs the mode-`0` pass starting at block `1`. If the first block is not type `4`, both passes start at block `0`.

That behavior is visible in:

- `0x4651E1..0x4651F5` (`cmp byte ptr [ebp+0xF], 4` / `mov edi, 1`)
- `0x465201..0x465213` first `0x466010(..., mode=1)`
- `0x465216..0x465227` second `0x466010(..., mode=0)`

## Proven type-`5` (`NEAR_OBJECTS`) behavior

This is the clearest new parity finding from the fresh capture.

- WoW.exe does not treat type `5` as "unhandled" or "metadata only".
- The top-level dispatcher routes type `5` through the same direct GUID-list handler as type `4`: `0x4651A0 -> 0x465FD0`.
- The type-`5` prepass `0x467230` walks the GUID list and conditionally calls `0x4644F0`.
- `0x4644F0` performs a lookup via `0x464530`, runs the object's virtual cleanup (`[vtable + 8]`), then calls `0x464700`.

Working conclusion:

- type `5` is a stale-cache cleanup path before the follow-up create blocks arrive
- BG must not throw on `NEAR_OBJECTS`
- treating the type-`5` GUID list as a removal pass is binary-backed by `0x467230 -> 0x4644F0`

## Create-path findings

The create path is split between prepass and direct handlers:

- `0x465C50` is the direct create/create2 block reader
- `0x4660A0` is the create/partial prepass
- `0x466350` is the cached-object branch inside that prepass
- `0x466E00` constructs or recovers the typed object instance
- `0x466C70` zeroes the typed descriptor storage

The strongest ordering fact currently proven is inside the create prepass:

1. parse movement/update-flag payload into stack scratch
2. call `0x466E00` to obtain/create the typed object
3. call `0x466C70` to zero/init the descriptor storage for that object type
4. call `0x466320`
5. only after that, call `0x466A20`

That sequence is visible in `0x46611B..0x4662A4`.

`0x466320` immediately routes into `0x466590` with `push 0; push 1`, while `0x466A20` dispatches by type and reaches the movement/object-type helpers (`0x5D81F0`, `0x5D7730`, `0x5FAD50`, `0x5DD2E0`, etc.).

Conservative conclusion:

- on the create path, WoW.exe does descriptor/value work before it reaches the later type-specific movement/application helper
- this matches the current BG `Add` order better than a "movement first, fields later" model

## Cached-object create branch (`0x466350`)

`0x4660A0` does not always go down the "construct a new object" path.

- It first looks up the GUID via `0x464530`.
- On a cache hit, it calls `0x466350`.
- That hit path returns without calling `0x466E00`, `0x466C70`, or the later list-link step at `0x4662C0..0x4662E0`.

That means duplicate `CREATE_OBJECT` / `CREATE_OBJECT2` blocks for an already-cached GUID are an **in-place mutate path**, not a remove-and-recreate path.

There is also an ordering detail inside `0x466350`:

- for the player / gameobject-style branch (`0x466383..0x4664BE`), `0x466350` parses the movement/update-flag payload and calls `0x5FF070`
- only after that does it call the descriptor walker `0x466590` at `0x466570..0x466582`

Practical implication:

- on the cached-object create branch, movement prepass work can happen before descriptor fields are applied
- BG must not replace the existing object instance when a duplicate create block arrives for the same typed GUID
- for that branch, treating movement as "always after fields" is not WoW.exe-accurate

## Descriptor storage / class sizing anchor

`0x466C70` is the best current layout anchor for object-class storage. It zeroes typed regions at exact offsets:

| Type id | Class | Start | End | Size |
| --- | --- | --- | --- | --- |
| `0` | `CGObject_C` | `+0x110` | `+0x128` | `0x18` |
| `1` | item | `+0x348` | `+0x408` | `0xC0` |
| `2` | container | `+0x6E0` | `+0x8C8` | `0x1E8` |
| `3` | unit | `+0xE68` | `+0x1158` | `0x2F0` |
| `4` | player | `+0x1D70` | `+0x2578` or `+0x3178` | `0x798` remote, `0x1408` local |
| `5` | game object | `+0x288` | `+0x2F0` | `0x68` |
| `6` | dynamic object | `+0x1A0` | `+0x1E0` | `0x40` |
| `7` | corpse | `+0x2B8` | `+0x350` | `0x98` |

The local-player `0x1408` size matches `PLAYER_END * 4` exactly (`0x502 * 4`).

## What is still unresolved

The fresh `0x4651A0` capture closes the dispatcher shape, the type-`5` gap, and the duplicate-create identity rule, but one P2.4 question is still open:

- the exact final order inside the deep `0x466590` descriptor walker versus the later type-specific helpers still needs a dedicated follow-up capture if we want to prove aura-vs-position ordering at the "last applied field" level

That means this capture is enough to justify the `NEAR_OBJECTS` parser fix and to anchor `cgobject_layout.md`, but it is not yet the final word on every descriptor-subfield mutation.
