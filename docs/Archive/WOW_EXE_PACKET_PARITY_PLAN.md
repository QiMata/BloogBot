# WoW.exe Packet Handling & ACK Parity — Extensive Plan

**Status:** Planning (drafted 2026-04-16)
**Scope:** Bring `WoWSharpClient` packet dispatch, ObjectManager state mutation, and ACK generation to byte-for-byte parity with WoW.exe 1.12.1.
**Authority:** The WoW.exe binary is the sole authority. Every behavior decision in this plan must reference a disassembled VA or an existing decompilation doc under `docs/physics/` or a binary offset in `memory/wow_exe_physics_decompilation.md`. No heuristics without binary evidence. See `memory/feedback_binary_parity_rule.md`.

---

## 0. Current State Snapshot (as of 2026-04-16)

### What we have already (do not redo)

**Physics decompilation:** 56 disasm captures + 10 analytical markdown docs in `docs/physics/` covering the full grounded/airborne/collision pipeline. Constants, function VAs, CMovement struct layout, and MovementInfo packet format are all pinned — see `memory/wow_exe_physics_decompilation.md`.

**Packet handler coverage:** 14 handler classes in `Exports/WoWSharpClient/Handlers/` covering movement, login, object update, chat, character select, spells, quests, pet, death, standstate, world state, and client control opcodes.

**ACK generation paths already wired:**

| Opcode                                  | Sent by our code | Parsed by our code | Deferred?         |
| --------------------------------------- | :--------------: | :----------------: | ----------------- |
| `MSG_MOVE_TELEPORT_ACK`                 |         ✓        |          ✓         | Yes, gated        |
| `MSG_MOVE_WORLDPORT_ACK`                |         ✓        |          ✗         | No                |
| `CMSG_FORCE_RUN_SPEED_CHANGE_ACK`       |         ✓        |          ✗         | No                |
| `CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK`  |         ✓        |          ✗         | No                |
| `CMSG_FORCE_SWIM_SPEED_CHANGE_ACK`      |         ✓        |          ✗         | No                |
| `CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK` |         ✓        |          ✗         | No                |
| `CMSG_FORCE_WALK_SPEED_CHANGE_ACK`      |         ✓        |          ✗         | No                |
| `CMSG_FORCE_TURN_RATE_CHANGE_ACK`       |         ✓        |          ✗         | No                |
| `CMSG_FORCE_MOVE_ROOT_ACK`              |         ✓        |          ✗         | No                |
| `CMSG_FORCE_MOVE_UNROOT_ACK`            |         ✓        |          ✗         | No                |
| `CMSG_MOVE_WATER_WALK_ACK`              |         ✓        |          ✗         | No                |
| `CMSG_MOVE_HOVER_ACK`                   |         ✓        |          ✗         | No                |
| `CMSG_MOVE_FEATHER_FALL_ACK`            |         ✓        |          ✗         | No                |
| `CMSG_MOVE_KNOCK_BACK_ACK`              |         ✓        |          ✗         | No                |
| `MSG_MOVE_SET_RAW_POSITION_ACK`         |         ✗        |          ✗         | Not observed in WoW.exe 1.12.1 |
| `CMSG_MOVE_FLIGHT_ACK`                  |         ✗        |          ✗         | Not observed in WoW.exe 1.12.1 |

**Live-validated parity bundles (2026-04-15):**

* `Tests/WoWSharpClient.Tests` `Category=MovementParity` → 30/30 green
* `Tests/Navigation.Physics.Tests` `Category=MovementParity` → 8/8 green
* `Tests/BotRunner.Tests` `Category=MovementParity` → 12/12 green on Docker

### Gaps vs WoW.exe identified during scouting

The following gaps have been identified and need binary-backed evidence before fixing:

| #  | Gap                                                                       | Observed behavior                                                                                       | Impact                                                                     |
| -- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| G1 | Knockback ACK sent **synchronously** in handler, physics consumes later   | ACK contains pre-impulse MovementInfo; server may see inconsistent state                                | Anti-cheat suspicion, possible rubber-banding                              |
| G2 | `MSG_MOVE_TIME_SKIPPED` parsed but `FireOnMoveTimeSkipped` has no listener | Time skip events silently dropped                                                                       | Physics drift after anti-cheat time-sync events                            |
| G3 | `MSG_MOVE_JUMP` / `MSG_MOVE_FALL_LAND` fire events but no ObjectManager hook | `fallTime` not reset on landing; jump state may persist                                                 | Air-time accumulation; incorrect MovementFlags on next jump                |
| G4 | Teleport flag-clear only masks `MOVEFLAG_MASK_MOVING_OR_TURN` (8 bits)    | `MOVEFLAG_JUMPING` / `MOVEFLAG_FALLING*` / `MOVEFLAG_SWIMMING` persist across teleport                  | Post-teleport stale aerial state, mid-air landing animation                |
| G5 | `SMSG_MONSTER_MOVE` and `SMSG_SPLINE_MOVE_*` mutate state with no ACK     | Server-driven spline flag changes silently applied                                                      | Unclear — VMaNGOS may not require these ACKs but needs confirmation        |
| G6 | `MSG_MOVE_SET_RAW_POSITION_ACK` initially assumed missing                 | Static table has no `0x00E0/0x00E1` registration; live 2026-04-17 probe logged inbound `0x00E0` with no outbound ACK | Close as not-applicable in WoW.exe 1.12.1 |
| G7 | `CMSG_MOVE_FLIGHT_ACK` initially assumed missing                          | Static table has no `0x033E/0x033F/0x0340` registration; live 2026-04-17 probes logged inbound `0x033E/0x033F` with no outbound `0x0340` | Close as not-applicable in WoW.exe 1.12.1 |
| G8 | `IsSceneDataReady()` guard on teleport ACK may deadlock                   | If tile data missing, teleport ACK blocks indefinitely                                                  | Cross-zone teleports with missing tiles stall the state machine            |
| G9 | Speed-change ACK format unverified vs WoW.exe byte layout                  | Our ACK includes full MovementInfo + speed; WoW.exe format unconfirmed                                 | Anti-cheat may reject malformed ACKs                                       |
| G10 | Root/unroot ACK counter handling unverified                                | WoW.exe uses a shared movement counter; our code uses a local counter                                   | Counter mismatch → server drops ACKs                                       |

---

## 1. Phase Overview

| Phase | Name                                                 | Deliverable                                                             | Blocks       |
| ----- | ---------------------------------------------------- | ----------------------------------------------------------------------- | ------------ |
| **P2.1** | Decompilation research: packet dispatch & ACK generation | 14 new `0x*_disasm.txt` files + 4 analytical markdown docs              | P2.2 onwards |
| **P2.2** | ACK format parity (byte-level)                       | Per-ACK binary parity tests + SendMovement fixes                        | P2.3         |
| **P2.3** | ACK timing & ordering parity                         | Sync-vs-deferred audit; teleport/knockback timing fixes                 | P2.4         |
| **P2.4** | ObjectManager state mutation parity                  | CGPlayer_C / CGUnit_C / CGObject_C field map; mutation-order parity     | P2.5         |
| **P2.5** | Packet-flow end-to-end parity                        | Capture → dispatch → state → ACK trace tests; full flow verified        | P2.6         |
| **P2.6** | State-machine parity                                 | Client-control / teleport / worldport / login state diagrams; guard fixes | P2.7         |
| **P2.7** | Gap closure (G1-G10)                                 | Each gap closed with binary evidence + test                             | —            |

Each phase has **explicit entry criteria** (what must be proven first), a **work breakdown** (numbered sub-tasks), **test strategy**, and **exit criteria** (what green proves parity).

---

## 2. Phase P2.1 — Decompilation Research: Packet Dispatch & ACK Generation

### 2.1 Entry criteria
- Existing physics decompilation under `docs/physics/` is read and understood.
- `memory/wow_exe_physics_decompilation.md` is the baseline reference sheet.

### 2.2 Known addresses (do not re-find)

| Address  | Symbol                        | What it does                                                   | Source            |
| -------- | ----------------------------- | -------------------------------------------------------------- | ----------------- |
| 0x537AA0 | `NetClient::ProcessMessage`   | Inbound packet dispatcher (9-byte prologue confirmed)          | memory/MEMORY.md  |
| 0x005379A0 | `NetClient::Send`           | Outbound packet send (hooked by PacketLogger)                  | memory/MEMORY.md  |
| 0x47ECB0 | `WriteMovementInfo`           | Serializes MovementInfo struct to wire buffer                  | memory/wow_exe_physics_decompilation.md |
| 0x600A30 | `SendMovementPacket`          | Top-level outbound movement packet send                         | memory/wow_exe_physics_decompilation.md |
| 0x618900 | `PrepareMovementInfo`         | Builds MovementInfo with mask 0x75A07DFF                        | memory/wow_exe_physics_decompilation.md |
| 0x5E2110 | `HeartbeatTimer`              | 100ms interval heartbeat check                                   | memory/wow_exe_physics_decompilation.md |
| 0x5E22D0 | `HasPositionChanged`          | Heartbeat gate: compare last-sent vs current position            | memory/wow_exe_physics_decompilation.md |
| 0xB4B424 | `IsIngame` field              | In-memory flag (returns 0 on 1.12.1 — DO NOT USE)                | memory/MEMORY.md  |

### 2.3 Unknown addresses to discover (the real work)

The decompilation targets below are the packet-handling twins of the physics functions we already have. Each one needs:

- A raw disassembly capture → `docs/physics/0x*_disasm.txt`
- A pseudocode translation → `docs/physics/0x*_pseudocode.md`
- Binary-backed evidence for the behavior claim

| Priority | Symbol / target                                | Why we need it                                        | Deliverable filename                       |
| -------- | ---------------------------------------------- | ----------------------------------------------------- | ------------------------------------------ |
| P0       | `NetClient::ProcessMessage` **body**           | Dispatch table; how opcodes route to handlers         | `0x537AA0_disasm.txt` + `0x537AA0_pseudocode.md` |
| P0       | Opcode handler table / vtable                  | The `(opcode → fn)` map WoW.exe consults              | `opcode_dispatch_table.md`                 |
| P0       | `NetClient::Send` **body**                     | Outbound packet framing, encryption, checksum flow     | `0x005379A0_disasm.txt`                    |
| P1       | `CMovement::HandleSpeedChange` (SMSG_FORCE_*_SPEED_CHANGE) | ACK payload byte layout; sequence counter           | `smsg_force_speed_change_handler.md`       |
| P1       | `CMovement::HandleMoveRoot`                    | Root flag mutation + ACK format                        | `smsg_force_move_root_handler.md`          |
| P1       | `CMovement::HandleKnockBack`                   | Impulse storage + ACK payload (position or pre-impulse?) | `smsg_move_knock_back_handler.md`          |
| P1       | `CMovement::HandleWaterWalk` / `HandleHover`   | Flag toggle + ACK format                               | `smsg_move_flag_toggle_handler.md`         |
| P1       | `CMovement::HandleTeleport`                    | Client-side teleport apply; MSG_MOVE_TELEPORT_ACK format | `msg_move_teleport_handler.md`           |
| P1       | `CWorld::HandleNewWorld` / `HandleWorldPortAck`| MSG_MOVE_WORLDPORT_ACK send conditions                  | `msg_move_worldport_ack.md`                |
| P2       | `CGWorldClient::HandleUpdateObject`            | SMSG_UPDATE_OBJECT block walk; Add/Update/OutOfRange   | `smsg_update_object_handler.md`            |
| P2       | `CGObject_C` / `CGUnit_C` / `CGPlayer_C` vtables | Polymorphic dispatch for Update application           | `cgobject_vtables.md`                      |
| P2       | Movement counter / sequence counter location   | What counter goes into each ACK                         | `movement_counter_tracking.md`             |
| P2       | `MSG_MOVE_SET_RAW_POSITION` + ACK               | Confirm whether the assumed raw-position ACK exists at all | `raw_position_and_flight_ack.md`        |
| P3       | `CMSG_MOVE_FLIGHT_ACK` trigger                 | Confirm whether the assumed flight ACK exists at all       | `raw_position_and_flight_ack.md`        |

### 2.4 Tooling: how to capture disassembly

We already have a working PacketLogger hook that successfully injects into `NetClient::ProcessMessage` at 0x537AA0 (see `memory/MEMORY.md` — 500+ SMSG packets captured after fix). The same FASM code-cave pattern used to reach `0x537AA0` can be used to:

1. Attach IDA / Ghidra to a running WoW.exe (1.12.1) via `Services/ForegroundBotRunner/Native/DllInjector.cs`.
2. Dump disassembly for each target VA via Ghidra Decompile API or IDA batch mode.
3. Save to `docs/physics/0x*_disasm.txt` (raw) and write a `_pseudocode.md` companion translating to C-like form.
4. Cross-reference against physics disasm where handlers touch CMovement at +0x10/+0x1C/+0x40/etc.

### 2.5 Sub-tasks

- [ ] P2.1.1 Capture `NetClient::ProcessMessage` disassembly (0x537AA0); identify opcode dispatch mechanism (jump table, vtable, or switch).
- [ ] P2.1.2 Dump the opcode → handler mapping. Store as markdown table keyed by opcode hex value.
- [ ] P2.1.3 Capture `NetClient::Send` disassembly (0x005379A0); identify outbound framing, encryption hooks, and size-prefix format.
- [ ] P2.1.4 For each P1-priority handler in §2.3: capture disassembly, translate to pseudocode, identify CMovement mutations and ACK emission.
- [ ] P2.1.5 Decompile CGPlayer_C vtable and list overridden methods vs CGUnit_C / CGObject_C.
- [ ] P2.1.6 Trace the movement counter: find where it lives in CMovement (offset?), when it is incremented, when it is included in outbound packets.

### 2.6 Test strategy

No unit tests in this phase — research only. Exit is proven by a complete decompilation reference set under `docs/physics/`.

### 2.7 Exit criteria
- All P0 and P1 targets have disassembly + pseudocode files committed.
- `docs/physics/README.md` indexes the new files.
- `memory/wow_exe_physics_decompilation.md` gains a **Packet Handling** section with new constants/offsets/VAs.

---

## 3. Phase P2.2 — ACK Format Parity (Byte-Level)

### 3.1 Entry criteria
- P2.1 complete; each ACK's binary format is documented.

### 3.2 Philosophy

Every outbound ACK we send must be **byte-for-byte identical** to what WoW.exe sends for the same trigger, given the same input state. This is verified by:

1. Capturing the real WoW.exe ACK via PacketLogger (already working).
2. Generating our ACK from the same snapshot state.
3. Asserting byte equality with a diff on mismatch.

### 3.3 Sub-tasks

- [ ] P2.2.1 **Capture golden-corpus ACK bytes** from a running FG bot for each ACK opcode. Record in `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/` as raw bytes + state snapshot (pos/facing/speed/flags/counter).
- [ ] P2.2.2 Add `AckBinaryParityTests` class with one test per ACK opcode; loads corpus entry, builds `MovementInfo` from snapshot, calls `SendXxxAck` with a capturing `IWoWClient`, asserts `Assert.Equal(expectedBytes, actualBytes)`.
- [ ] P2.2.3 For each failing test: diff bytes, locate divergence, fix the corresponding `Send*Ack` method in `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`. Every fix must reference a VA from P2.1.
- [ ] P2.2.4 Confirm movement counter semantics (shared vs per-opcode) against `movement_counter_tracking.md`. Fix if ours diverges.
- [ ] P2.2.5 Gate: all ACK opcodes either have a binary-parity test passing OR have a documented `Reason:` entry in `docs/WOW_EXE_PACKET_PARITY_PLAN.md` saying why parity is not achievable (e.g. opcode-not-observed-in-corpus).

### 3.4 Test strategy
- New test class `Tests/WoWSharpClient.Tests/Parity/AckBinaryParityTests.cs` tagged `Category=AckParity`.
- Golden corpus versioned under `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/`.
- Each test: `[Theory]` with `MemberData` enumerating corpus entries; asserts byte equality per opcode.

### 3.5 Exit criteria
- All 14 currently-wired ACKs have byte-parity tests passing.
- Previously-unwired ACKs (`MSG_MOVE_SET_RAW_POSITION_ACK`, `CMSG_MOVE_FLIGHT_ACK`) must either be proven on the wire or explicitly closed as not-applicable in WoW.exe 1.12.1.

---

## 4. Phase P2.3 — ACK Timing & Ordering Parity

### 4.1 Entry criteria
- P2.2 green (all ACK bytes correct in isolation).

### 4.2 Philosophy

WoW.exe sends some ACKs immediately from the opcode handler, others deferred to the next movement tick, others from the game loop. Our current code defaults to "synchronous in handler" for most ACKs. This may race against ObjectManager's pending-update queue and MovementController's physics tick.

### 4.3 Questions to answer with binary evidence (fed by P2.1)

| #   | Question                                                                  | Decompilation target     |
| --- | ------------------------------------------------------------------------- | ------------------------ |
| Q1  | Does WoW.exe send knockback ACK before or after applying impulse to CMovement? | `smsg_move_knock_back_handler.md` |
| Q2  | Does WoW.exe send speed-change ACKs synchronously in handler or on next movement tick? | `smsg_force_speed_change_handler.md` |
| Q3  | Does WoW.exe defer teleport ACK until next physics tick? What are its gate conditions? | `msg_move_teleport_handler.md` |
| Q4  | When is `MSG_MOVE_WORLDPORT_ACK` sent — on NEW_WORLD receipt or on first ground-validated physics tick? | `msg_move_worldport_ack.md` |
| Q5  | Does WoW.exe ACK SMSG_MONSTER_MOVE / spline opcodes? If so, which ones and when? | `0x537AA0_pseudocode.md` dispatch table |

### 4.4 Sub-tasks

- [ ] P2.3.1 For each of Q1-Q5: write the answer as a one-paragraph finding in `docs/physics/packet_ack_timing.md` citing the VA that proves it.
- [ ] P2.3.2 Reconcile each answer with our current implementation. For divergences, write a failing test that captures the incorrect timing (e.g. `KnockbackAckSentAfterImpulseAppliedTests`).
- [ ] P2.3.3 Fix the timing. Options:
  - **Defer-to-controller pattern**: ACK handler sets a "pending ACK" flag in ObjectManager; `MovementController.Update()` consumes and sends on the next tick after state is consistent. Already the pattern for teleport.
  - **Immediate-after-mutation pattern**: Keep synchronous but ensure mutation happens before `Send*Ack` call. Already mostly done.
- [ ] P2.3.4 Add tests that prove the timing: e.g. `MovementController.Update()` consuming the pending knockback impulse before `CMSG_MOVE_KNOCK_BACK_ACK` is emitted.

### 4.5 Test strategy
- New test class `Tests/WoWSharpClient.Tests/Parity/AckTimingParityTests.cs`.
- Tests observe the **order** of state mutations and packet sends by hooking the mock `IWoWClient.SendMovementOpcodeAsync` into a timestamped log and asserting order.

### 4.6 Exit criteria
- All five timing questions answered with binary evidence.
- At least one test per answer proves our timing matches.
- Gap G1 (knockback ACK race) closed.

---

## 5. Phase P2.4 — ObjectManager State Mutation Parity

### 5.1 Entry criteria
- P2.1 §2.3 P2-priority targets complete (CGObject_C / CGUnit_C / CGPlayer_C vtables, SMSG_UPDATE_OBJECT handler).

### 5.2 What we need to build: `CGObject_C_STRUCT.md`

A canonical field-offset map for WoW.exe 1.12.1's object structures, covering:

| Field area              | CGObject_C     | CGUnit_C       | CGPlayer_C     |
| ----------------------- | -------------- | -------------- | -------------- |
| Object type             | +0x00 (type id) | inherits       | inherits       |
| GUID                    | +? (u64)       | inherits       | inherits       |
| Position                | via ptr        | via CMovement  | via CMovement  |
| Update field mask       | +?             | +?             | +?             |
| UpdateFields values     | +?             | +?             | +?             |
| MovementFlags           | —              | CMovement +0x40 | CMovement +0x40 |
| Speed (run/walk/swim/turn) | —          | CMovement +0x88-0x9C | inherits    |
| Aura list pointer       | —              | +?             | inherits       |
| Target GUID             | —              | +?             | inherits       |
| Inventory               | —              | —              | +?             |
| Bag slots               | —              | —              | +?             |
| Combo points            | —              | —              | +?             |
| Spell cast state        | —              | +?             | inherits       |

This will serve as the reference sheet against which our C# models are compared.

### 5.3 Our current model coverage

| Our class                | Maps to       | Known gaps                                                                 |
| ------------------------ | ------------- | -------------------------------------------------------------------------- |
| `WoWObject`              | CGObject_C    | Possibly missing fields                                                    |
| `WoWUnit`                | CGUnit_C      | Aura list, threat table, spell cast state may be incomplete                |
| `WoWLocalPlayer`         | CGPlayer_C    | Inventory item GUIDs not sourced from snapshot (see MEMORY.md)             |
| `WoWGameObject`          | CGGameObject_C | Door/transport/quest-item subtypes need confirmation                       |
| `WoWLocalPet`            | CGPet_C       | Promotion-on-Add and Promotion-on-Update both exist — need binary evidence |

### 5.4 Sub-tasks

- [ ] P2.4.1 Produce `docs/physics/cgobject_layout.md` with exact field offsets for CGObject_C, CGUnit_C, CGPlayer_C, CGGameObject_C, CGPet_C from P2.1 decompilation.
- [ ] P2.4.2 Audit our C# classes. Each field we track must map to a documented WoW.exe field; each WoW.exe field we DON'T track must have a documented reason (e.g. "Graphics-only, not relevant to bot logic").
- [ ] P2.4.3 Decompile `CGWorldClient::HandleUpdateObject`. Document the SMSG_UPDATE_OBJECT block-walk order: which fields are applied before/after position, before/after movement flags, before/after aura list.
- [ ] P2.4.4 Match our `WoWSharpObjectManager.Network.cs` `ProcessUpdatesAsync` against §P2.4.3's order. Write a deterministic test that replays a captured SMSG_UPDATE_OBJECT byte stream and verifies our state mutation order matches.
- [ ] P2.4.5 Identify mutation-order divergences. Fix. Reference VA for each change.

### 5.5 Test strategy
- New test class `Tests/WoWSharpClient.Tests/Parity/ObjectUpdateMutationOrderTests.cs`.
- Golden corpus: captured SMSG_UPDATE_OBJECT byte streams from real sessions.
- Each test replays one stream, captures mutation events via an observer hook on `WoWSharpObjectManager`, asserts event order matches WoW.exe.

### 5.6 Exit criteria
- `docs/physics/cgobject_layout.md` exists and is complete for CGObject_C / CGUnit_C / CGPlayer_C.
- Every field in our C# models has a documented mapping.
- At least three mutation-order tests pass, covering: local player Add, remote unit Add, local player field Update with movement.

---

## 6. Phase P2.5 — Packet-Flow End-to-End Parity

### 6.1 Entry criteria
- P2.2 (byte parity), P2.3 (timing), P2.4 (state mutation) all green.

### 6.2 Goal

A single **end-to-end trace test** per representative packet proves:
1. Bytes arrive on the socket.
2. Opcode dispatch selects the right handler.
3. Handler parses payload exactly as WoW.exe does (byte parity per P2.2).
4. State mutation matches WoW.exe order (per P2.4).
5. If an ACK is required, it is sent with the right timing (per P2.3) and byte-for-byte identical payload (per P2.2).

### 6.3 Representative packets (one trace test each)

| Packet                    | Why it matters                                                       |
| ------------------------- | -------------------------------------------------------------------- |
| `SMSG_UPDATE_OBJECT` (Add local player) | Login path; sets up ObjectManager from scratch              |
| `SMSG_UPDATE_OBJECT` (Update with movement) | Most common packet; drives ObjectManager every tick   |
| `SMSG_FORCE_RUN_SPEED_CHANGE` | Speed ACK flow + Player.RunSpeed mutation + MovementController observes new speed |
| `SMSG_FORCE_MOVE_ROOT`    | Root flag + ACK + MovementController respects root                   |
| `SMSG_MOVE_KNOCK_BACK`    | Impulse storage + ACK + MovementController consumes impulse          |
| `MSG_MOVE_TELEPORT`       | Deferred ACK + gated by ground snap + MovementController reset       |
| `SMSG_NEW_WORLD` → `MSG_MOVE_WORLDPORT_ACK` | Map transition + worldport ACK timing                |
| `SMSG_MONSTER_MOVE`       | SplineController drives remote unit without ACK                      |

### 6.4 Sub-tasks

- [ ] P2.5.1 Build `Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture` with: captured bytes in, captured bytes out, state observer hook, ordered event log.
- [ ] P2.5.2 Write one trace test per representative packet. Each test imports corpus bytes, feeds them through `OpCodeDispatcher`, observes the full flow, asserts each step.
- [ ] P2.5.3 Fix any divergences discovered. Reference P2.1 disassembly for every fix.

### 6.5 Test strategy
- Tests tagged `Category=PacketFlowParity`.
- Each test runs in isolation via `[Collection("Sequential ObjectManager tests")]` since `WoWSharpObjectManager.Instance` is a singleton.
- Uses existing `UpdateProcessingHelper.DrainPendingUpdates()` to deterministically drain the pending-update queue.

### 6.6 Exit criteria
- All eight representative-packet trace tests pass.
- Each test's flow is annotated with the VA from P2.1 that proves parity for that step.

---

## 7. Phase P2.6 — State-Machine Parity

### 7.1 Entry criteria
- P2.5 green.

### 7.2 State machines to document & verify

| State machine          | Our implementation                                                 | WoW.exe reference          |
| ---------------------- | ------------------------------------------------------------------- | -------------------------- |
| Client-control state    | `WoWSharpObjectManager._isInControl`                               | TBD from P2.1              |
| Teleport state          | `_isBeingTeleported`, `_pendingTeleportAck`                        | TBD from P2.1              |
| Worldport / map transition | `_pendingWorldEntryGuid`, `HasEnteredWorld`, `IsInMapTransition` | TBD from P2.1              |
| Login / world-entry     | `EnterWorld()` → `SchedulePendingWorldEntryRetry()`                | TBD from P2.1              |
| Knockback               | `_pendingKnockbackVelX/Y/Z`, `_hasPendingKnockback`                | TBD from P2.1              |
| Root                    | `MOVEFLAG_ROOT` flag + root-ack sequence counter                   | TBD from P2.1              |

### 7.3 Sub-tasks

- [ ] P2.6.1 For each state machine, produce a `docs/physics/state_<name>.md` with:
  - State diagram (entry, exit, transitions).
  - Transition triggers (which packet / ACK / tick causes which transition).
  - Invariants (what must be true in each state).
  - Binary evidence (VA from P2.1).
- [ ] P2.6.2 Audit our implementation. Each documented transition must have a code path; each code path must match a documented transition. Add missing transitions; remove spurious ones.
- [ ] P2.6.3 Write state-machine parity tests. E.g. "After receiving SMSG_NEW_WORLD, we remain in `IsInMapTransition=true` until `MSG_MOVE_WORLDPORT_ACK` is sent AND ground snap resolves AND first position update is received."

### 7.4 Test strategy
- `Tests/WoWSharpClient.Tests/Parity/StateMachineParityTests.cs`.
- Each state machine gets its own test class.
- Uses a deterministic time source (fake clock) to avoid flakes.

### 7.5 Exit criteria
- All six state machines documented and tested.
- Gap G8 (teleport ACK deadlock) closed.
- Gap G4 (teleport flag clear incomplete) closed.

---

## 8. Phase P2.7 — Gap Closure (G1-G10)

### 8.1 Entry criteria
- P2.1 through P2.6 green.

### 8.2 The gap table revisited (see §0)

Each gap G1-G10 should have been closed by an earlier phase. This phase is a **verification pass**:

| Gap | Closed by phase | Evidence                             |
| --- | --------------- | ------------------------------------ |
| G1  | P2.3            | Knockback ACK timing test            |
| G2  | P2.7            | New listener + test                  |
| G3  | P2.7            | Jump/fall land hook + fallTime test  |
| G4  | P2.6            | Teleport state machine fix           |
| G5  | P2.3 / P2.1     | Documented as "no ACK needed" OR closed |
| G6  | P2.7            | Not applicable: no static registration + no live ACK emission |
| G7  | P2.7            | Not applicable: no static registration + no live ACK emission |
| G8  | P2.6            | Teleport ACK guard fix               |
| G9  | P2.2            | Byte parity test                     |
| G10 | P2.2            | Byte parity test + counter doc       |

### 8.3 Residual sub-tasks (those not closed by prior phases)

- [x] P2.7.1 **G2** — Wire `MSG_MOVE_TIME_SKIPPED` listener in ObjectManager. Find VA of WoW.exe's time-skip handler. Document what state it mutates. Match.
- [x] P2.7.2 **G3** — Wire `MSG_MOVE_JUMP` / `MSG_MOVE_FALL_LAND` consumer in ObjectManager. Verify fallTime reset semantics against WoW.exe.
- [x] P2.7.3 **G6** — Close `MSG_MOVE_SET_RAW_POSITION_ACK` as not-applicable in WoW.exe 1.12.1. Evidence: no static registration and a 2026-04-17 live probe logged inbound `0x00E0` with no outbound ACK.
- [x] P2.7.4 **G7** — Close `CMSG_MOVE_FLIGHT_ACK` as not-applicable in WoW.exe 1.12.1. Evidence: no static registration and 2026-04-17 live probes logged inbound `0x033E/0x033F` with no outbound `0x0340`.
- [ ] P2.7.5 Final full-stack regression: run all three parity bundles + new AckParity bundle + new PacketFlowParity bundle. All green.

### 8.4 Exit criteria
- All 10 gaps closed with binary evidence OR documented as not-applicable.
- New test bundles (`Category=AckParity`, `Category=PacketFlowParity`, `Category=StateMachineParity`) all green in CI.

---

## 9. Risk / Mitigation

| Risk                                                                 | Mitigation                                                                                                             |
| -------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Decompilation is large undertaking; may produce too much noise       | Prioritize per §2.3; only P0/P1 are blocking; P2/P3 can defer if bot already works                                     |
| Fixing timing in one handler breaks behavior that currently works    | Every change has a failing test first; every change cites a VA; no "seems reasonable" changes                          |
| Byte-parity tests become brittle when opcode formats legitimately vary | Golden corpus captures multiple samples per opcode; tests accept any corpus entry's byte pattern as valid              |
| `WoWSharpObjectManager.Instance` singleton makes parallel tests hard | Tests use `[Collection("Sequential ObjectManager tests")]` + `ResetWorldSessionState` — already established pattern    |
| Anti-cheat may trigger when we match WoW.exe exactly                  | Good — that proves parity. Document any anti-cheat triggers and confirm WoW.exe triggers the same                      |

---

## 10. Success Criteria (Overall P2)

When P2 is complete:

1. `docs/physics/` has a `Packet Handling` subsection with at least 14 new disassembly files and 4 analytical markdown docs.
2. `memory/wow_exe_physics_decompilation.md` has been extended with packet handling offsets, constants, and VAs.
3. Every currently-wired ACK has a binary-parity test (byte identical to WoW.exe).
4. Every ACK has documented timing (sync-in-handler OR deferred-to-controller) matching WoW.exe.
5. ObjectManager's `CGObject_C`-level struct map is documented; every C# field maps to a WoW.exe field.
6. Eight end-to-end packet-flow trace tests pass.
7. Six state machines are documented and tested for parity.
8. All 10 identified gaps are closed with binary evidence.
9. Full solution test matrix stays green throughout.
10. No regressions in the three existing MovementParity bundles or the 80-test NavigationPathTests bundle.

---

## 11. Out of Scope

- Rewriting physics (already done, green).
- Adding new bot features (tasks, professions, AV) — see BloogBot.AI backlog.
- UI changes.
- Protocol versions other than 1.12.1 (TBC / WotLK share some but not all opcodes; out of scope for now).

---

## 12. References

- `memory/MEMORY.md` — rules, known addresses, FG recv hook
- `memory/wow_exe_physics_decompilation.md` — baseline decompilation sheet
- `memory/feedback_binary_parity_rule.md` — binary parity is THE rule
- `memory/physics_rules.md` — physics engine rules
- `docs/physics/README.md` — physics decompilation index (will gain packet-handling section)
- `docs/server-protocol/movement-protocol.md` — current protocol doc
- `docs/server-protocol/opcodes-1.12.1.md` — complete opcode enum
- `Exports/WoWSharpClient/Handlers/MovementHandler.cs` — main parse entrypoint
- `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs` — ACK generation paths
- `Exports/WoWSharpClient/Movement/MovementController.cs` — ACK consumption paths
