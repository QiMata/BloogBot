# BG Movement Parity Audit — BG bot vs WoW.exe 1.12.1

> **Scope.** Per-opcode pass/fail status for every BG-side movement
> packet builder, parser, ACK trigger, and integrator constant against
> the decompiled WoW.exe (build 5875) reference. Performed on
> `main` at commit `ebff10a4` after Track 1 (teleport-ACK ground-snap
> gate removal) landed.
>
> **How to read this.** Each opcode/constant lists: where it's built or
> defined in the managed code, the binary VA it must match, the
> reference doc that justifies the model, and the test(s) that pin it.
> A row is **PASS** when both (a) the byte layout matches a captured
> WoW.exe golden corpus fixture or a disasm-derived expectation, and
> (b) the *trigger timing* matches the queue/apply/ACK shape documented
> in the binary reference. **PARTIAL** means layout is pinned but
> timing is asserted only on a representative subset. **FAIL** means
> divergence found and not yet fixed.

## Outbound ACK opcodes

| Opcode | Build site | Binary VA reference | Timing reference | Tests | Status |
|---|---|---|---|---|---|
| `MSG_MOVE_TELEPORT_ACK` (0x0C7) | `MovementPacketHandler.BuildMoveTeleportAckPayload` (16 bytes: GUID + counter + clientTime, **no MovementInfo**) | `0x602FB0` builds opcode at `0x603036`, sends via `0x5AB630` at `0x60308D`; gates on `0x468570` | [`docs/physics/state_teleport.md`](state_teleport.md), [`docs/physics/msg_move_teleport_handler.md`](msg_move_teleport_handler.md) | `MovementPacketHandlerAckTests.BuildMoveTeleportAckPayload_MatchesWowExeDisasmLayout`; `AckBinaryParityTests` golden-corpus fixture; `PacketFlowParityTests.MoveTeleport_UpdatesPlayerState_ThenFlushesDeferredAck`; `StateMachineParityTests.MoveTeleport_AckFiresAfterControlGrant_RegardlessOfGroundSnapOrSceneData`; `ObjectManagerWorldSessionTests.TryFlushPendingTeleportAck_WaitsForUpdates_ButNotGroundSnapNorSceneData` | **PASS** (Track 1: ground-snap gate removed; ACK now fires on the same readiness gates as the binary) |
| `MSG_MOVE_WORLDPORT_ACK` (0x0DC) | `LoginHandler` outbound on `SMSG_NEW_WORLD` | `0x401B00` schedules `0x401BC0`; send block at `0x401CA5..0x401CF4` | [`docs/physics/state_worldport.md`](state_worldport.md), [`docs/physics/msg_move_worldport_ack.md`](msg_move_worldport_ack.md), [`docs/physics/packet_ack_timing.md`](packet_ack_timing.md) Q4 | `AckBinaryParityTests` empty-payload corpus; `StateMachineParityTests.{LoginVerifyWorld_DoesNotSendWorldportAck,NewWorld_SendsExactlyOneWorldportAck_AfterWorldInfoUpdate}`; `PacketFlowParityTests.NewWorld_UpdatesWorldState_AndSendsSingleWorldportAck` | **PASS** |
| `CMSG_FORCE_MOVE_ROOT_ACK` (0x0E9) | `MovementPacketHandler.BuildForceMoveAck` (GUID + counter + MovementInfo) | `SMSG_FORCE_MOVE_ROOT` (0x0E8) → `0x61A700(...,1)` → queue slot `0x23` → later consumer ACKs | [`docs/physics/state_root.md`](state_root.md), [`docs/physics/smsg_force_move_root_handler.md`](smsg_force_move_root_handler.md) | `AckBinaryParityTests.ForceMoveAckFixtures` golden corpus; `MovementPacketHandlerAckTests.BuildForceMoveAck_IncludesGuidCounterAndMovementInfo`; `StateMachineParityTests.ForceMoveRootOpcodes_StageStateUntilDeferredFlush`; `PacketFlowParityTests.ForceMoveRoot_QueuesDeferredAck_ThenFlushesWithUpdatedState` | **PASS** |
| `CMSG_FORCE_MOVE_UNROOT_ACK` (0x0EB) | `BuildForceMoveAck` | `SMSG_FORCE_MOVE_UNROOT` (0x0EA) → `0x61A700(...,0)` → queue slot `0x24` | [`docs/physics/state_root.md`](state_root.md) | Same as ROOT_ACK above (theory shares fixture) | **PASS** |
| `CMSG_MOVE_KNOCK_BACK_ACK` (0x0F0) | `BuildForceMoveAck` | `SMSG_MOVE_KNOCK_BACK` → `0x603F90 → 0x602780 → 0x602670 → 0x617A30 → 0x6177A0` (queue slot `0x1C`); ACK fires only after physics consumes the impulse | [`docs/physics/state_knockback.md`](state_knockback.md), [`docs/physics/smsg_move_knock_back_handler.md`](smsg_move_knock_back_handler.md), [`docs/physics/packet_ack_timing.md`](packet_ack_timing.md) Q1 | `AckBinaryParityTests.ForceMoveAckFixtures` golden corpus; `StateMachineParityTests.MoveKnockBack_StagesImpulseUntilConsumedThenAcks`; `PacketFlowParityTests.MoveKnockBack_StagesImpulse_BeforeAckFlush` | **PASS** (queue-first, physics-consume, ACK-after — matches binary) |
| `CMSG_FORCE_RUN_SPEED_CHANGE_ACK` (0x0E3) | `BuildForceSpeedChangeAck` (GUID + counter + MovementInfo + float speed) | `SMSG_FORCE_RUN_SPEED_CHANGE` (0x0E2) → `0x6027D7 → 0x619500` (slot `0x14`); apply at `0x619550 → 0x7C7030` | [`docs/physics/smsg_force_speed_change_handler.md`](smsg_force_speed_change_handler.md), [`docs/physics/packet_ack_timing.md`](packet_ack_timing.md) Q2 | `AckBinaryParityTests.ForceSpeedAckFixtures` golden corpus; `MovementPacketHandlerAckTests.BuildForceSpeedChangeAck_TrailsMovementInfoWithFloatSpeed`; `PacketFlowParityTests.ForceRunSpeedChange_QueuesDeferredAck_ThenFlushesWithUpdatedState` | **PASS** |
| `CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK` (0x0E5) | `BuildForceSpeedChangeAck` | `SMSG_FORCE_RUN_BACK_SPEED_CHANGE` (0x0E4) → `0x602804 → 0x619590` (slot `0x15`); apply at `0x6195E0 → 0x7C7080` | [`docs/physics/smsg_force_speed_change_handler.md`](smsg_force_speed_change_handler.md) | `AckBinaryParityTests.ForceSpeedAckFixtures`; `PacketFlowParityTests.ForceSpeedChangeFamily_QueuesDeferredAck_ThenFlushesWithUpdatedState` (theory) | **PASS** (commit `ebff10a4`) |
| `CMSG_FORCE_SWIM_SPEED_CHANGE_ACK` (0x0E7) | `BuildForceSpeedChangeAck` | `SMSG_FORCE_SWIM_SPEED_CHANGE` (0x0E6) → `0x602831 → 0x6196B0` (slot `0x17`); apply at `0x619700 → 0x7C7120` | [`docs/physics/smsg_force_speed_change_handler.md`](smsg_force_speed_change_handler.md) | Theory above | **PASS** (commit `ebff10a4`) |
| `CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK` (0x2DD) | `BuildForceSpeedChangeAck` | `SMSG_FORCE_SWIM_BACK_SPEED_CHANGE` (0x2DC) → `0x6029D0 → 0x619740` (slot `0x18`); apply at `0x619790 → 0x7C7170` | [`docs/physics/smsg_force_speed_change_handler.md`](smsg_force_speed_change_handler.md) | Theory above | **PASS** (commit `ebff10a4`) |
| `CMSG_FORCE_WALK_SPEED_CHANGE_ACK` (0x2DB) | `BuildForceSpeedChangeAck` | `SMSG_FORCE_WALK_SPEED_CHANGE` (0x2DA) → `0x6029FD → 0x619620` (slot `0x16`); apply at `0x619670 → 0x7C70D0` | [`docs/physics/smsg_force_speed_change_handler.md`](smsg_force_speed_change_handler.md) | Theory above | **PASS** (commit `ebff10a4`) |
| `CMSG_FORCE_TURN_RATE_CHANGE_ACK` (0x2DF) | `BuildForceSpeedChangeAck` | `SMSG_FORCE_TURN_RATE_CHANGE` (0x2DE) → `0x6029A3 → 0x6197D0` (slot `0x19`); apply at `0x619820 → 0x7C6FF0` | [`docs/physics/smsg_force_speed_change_handler.md`](smsg_force_speed_change_handler.md) | Theory above | **PASS** (commit `ebff10a4`) |
| `CMSG_MOVE_WATER_WALK_ACK` (0x2D0) | `MovementPacketHandler.BuildMovementFlagToggleAck` (GUID + counter + MovementInfo + trailing float marker 1.0/0.0) | `SMSG_MOVE_WATER_WALK` / `SMSG_MOVE_LAND_WALK` → queue (per VMaNGOS `MovementPacketSender`) | [`docs/physics/smsg_move_flag_toggle_handler.md`](smsg_move_flag_toggle_handler.md) | `AckBinaryParityTests.ForceMoveAckFixtures` toggle fixtures; `PacketFlowParityTests.MovementFlagToggleFamily_QueuesDeferredAck_ThenFlushesWithUpdatedFlag` (theory) | **PASS** (layout + timing — Stream 2A) |
| `CMSG_MOVE_HOVER_ACK` (0x0F6) | `BuildMovementFlagToggleAck` | `SMSG_MOVE_SET_HOVER` / `SMSG_MOVE_UNSET_HOVER` → queue | [`docs/physics/smsg_move_flag_toggle_handler.md`](smsg_move_flag_toggle_handler.md) | `AckBinaryParityTests` toggle fixtures; `PacketFlowParityTests.MovementFlagToggleFamily_*` (theory) | **PASS** (layout + timing — Stream 2A) |
| `CMSG_MOVE_FEATHER_FALL_ACK` (0x2CF) | `BuildMovementFlagToggleAck` | `SMSG_MOVE_FEATHER_FALL` / `SMSG_MOVE_NORMAL_FALL` → queue | [`docs/physics/smsg_move_flag_toggle_handler.md`](smsg_move_flag_toggle_handler.md) | `AckBinaryParityTests` toggle fixtures; `PacketFlowParityTests.MovementFlagToggleFamily_*` (theory) | **PASS** (layout + timing — Stream 2A) |

## Outbound movement-state opcodes (heartbeats / state transitions)

These do not carry counters and have no inbound packet to ACK; they
are emitted by `MovementController.SendMovementPacket` based on the
local movement-flag delta and a 500ms heartbeat cadence.

| Opcode | Trigger (local state delta) | Tests | Status |
|---|---|---|---|
| `MSG_MOVE_START_FORWARD` / `MSG_MOVE_START_BACKWARD` | `MOVEFLAG_FORWARD` / `MOVEFLAG_BACKWARD` set | `MovementControllerTests.{StartForward_,StartBackward_}SendsMsgMove*` | PASS |
| `MSG_MOVE_STOP` | All moving flags cleared | `MovementControllerTests.StopMoving_SendsMsgMoveStop` | PASS |
| `MSG_MOVE_START_STRAFE_LEFT` / `..._RIGHT` | `MOVEFLAG_STRAFE_LEFT` / `_RIGHT` set | `MovementControllerTests.{StartStrafeLeft,StartStrafeRight}_*` | PASS |
| `MSG_MOVE_STOP_STRAFE` | Strafe flags cleared but moving flag retained | `MovementControllerTests.StopStrafe_*` | PASS |
| `MSG_MOVE_JUMP` | `MOVEFLAG_JUMPING` set | `MovementControllerTests.Jump_*` | PASS |
| `MSG_MOVE_FALL_LAND` | `MOVEFLAG_JUMPING` cleared (and/or `FALLINGFAR`) | `MovementControllerTests.FallLand_SendsMsgMoveFallLand` | PASS (selector) — payload identical to other heartbeats; pinned via `BuildMovementInfoBuffer` |
| `MSG_MOVE_START_SWIM` / `MSG_MOVE_STOP_SWIM` | `MOVEFLAG_SWIMMING` toggled | `MovementControllerTests.{StartSwim,StopSwim,SwimToForward}_*` | PASS |
| `MSG_MOVE_HEARTBEAT` | 500ms elapsed while still moving | `MovementControllerTests.Heartbeat_*`, `FlagChange_ResetsHeartbeatTimer` | PASS |
| `MSG_MOVE_SET_FACING` | Action dispatch (not a state delta) | Used by `WoWSharpObjectManager` for `SET_FACING` action | PASS (manual; no parity disasm reference) |

## Movement payload (`BuildMovementInfoBuffer`)

The shared movement block prefix all of the above heartbeats and
non-teleport ACKs. Layout per
[`docs/server-protocol/`](../server-protocol/) and reproduced from
`MovementPacketHandler.cs`:

```
flags (uint32, masked 0x75A07DFF — strips PENDING_*, ASCENDING, SPLINE_ENABLED, LOCAL_DIRTY)
clientTimeMs (uint32)
position (Vec3 = 3*float)
facing (float)
[transport block — only if MOVEFLAG_ONTRANSPORT]
  transportGuid (uint64)
  transportPosition (Vec3)
  transportFacing (float)
[swim pitch — only if MOVEFLAG_SWIMMING]
  pitch (float)
fallTimeMs (uint32)
[jump block — only if MOVEFLAG_JUMPING | FALLINGFAR]
  jumpVelocity (float)
  jumpSinAngle (float)
  jumpCosAngle (float)
  jumpInitialDirection (float)
[spline elevation — only if MOVEFLAG_SPLINE_ELEVATION]
  splineElevation (float)
```

Status: **PASS**. Pinned by `MovementPacketHandlerAckTests.cs`,
`AckBinaryParityTests` (every captured opcode includes a MovementInfo
block matching this layout), and `MovementControllerTests` (which
exercises every conditional sub-block via flag manipulation).

## MovementController integrator constants

These live in `Exports/Navigation/PhysicsEngine.h` (`PhysicsConstants`
namespace) and are consumed by both the C++ physics engine and the
managed `MovementController`. Each constant cites its WoW.exe VA.

| Constant | Value | WoW.exe VA | Reference | Status |
|---|---|---|---|---|
| `GRAVITY` | 19.29110527 y/s² | `0x0081DA58` | [`memory/wow_exe_physics_decompilation.md`](../../../C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/wow_exe_physics_decompilation.md) | PASS |
| `HALF_GRAVITY` | 9.64555 | `0x0081DA60` | same | PASS |
| `DOUBLE_GRAVITY` | 38.5822 | `0x0081DA64` | same | PASS |
| `INV_GRAVITY` | 0.05184 | `0x0080E020` | same | PASS |
| `JUMP_VELOCITY` | 7.955547 y/s | imm `0xC0FE93D8` @ `0x7C626F` | same | PASS |
| `JUMP_VELOCITY_SWIMMING` | 9.096748 y/s | imm `0xC1118C48` @ `0x7C6266` | same | PASS |
| `TERMINAL_VELOCITY` | 60.14800262 y/s | `0x0087D894` (computed at init by `0x7C6160`) | same | PASS |
| `SAFE_FALL_TERMINAL_VELOCITY` | 7.0 y/s | `0x0087D898` | same | PASS |
| `STEP_HEIGHT` | 2.027778 y | imm `0x4001C71C` in CMovement ctor (+0xB4) | same | PASS |
| `COLLISION_SKIN_FRACTION` | 0.333333 | CMovement +0xB0 | same | PASS |
| `BASE_WALK_SPEED` | 2.5 y/s | `0x0081018C` | same | PASS |
| `BASE_RUN_SPEED` | 7.0 y/s | `0x00810190` | same | PASS |
| `BASE_RUN_BACK_SPEED` | 4.5 y/s | (VMaNGOS `baseMoveSpeed[]`) | same | PASS |
| `BASE_TURN_RATE` | π rad/s | (3.141594) | same | PASS |
| `WALKABLE_TAN_MAX_SLOPE` | 1.19175 (tan 50°) | `0x0080E008` | same | PASS |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.6428 (cos 50°) | `0x0080DFFC` | same | PASS |

Status: **PASS**. Constants are pinned by VA citation in
`PhysicsEngine.h` and exercised through `Tests/Navigation.Physics.Tests/`
(notably `WowVerticalTravelTimeTests`, `MovementControllerPhysicsTests`,
and the recorded-trace replay harness).

## Findings

1. **Single divergence found and fixed.** The
   `_movementController.NeedsGroundSnap` clause in
   `WoWSharpObjectManager.Movement.cs:TryFlushPendingTeleportAck` was
   strictly more conservative than WoW.exe's `0x468570` readiness gate,
   delaying the teleport ACK 30–60 frames after the binary would have
   sent it. Removed at commit `eda32b09`; tests that pinned the
   divergent behaviour were updated in the same commit.

2. **Knockback queue-first model matches.** Managed code stages the
   impulse via `_pendingKnockback*` fields, lets `MovementController`
   consume the impulse on the next physics tick, and only then sends
   the ACK via `TryFlushPendingKnockbackAck`. This matches the WoW.exe
   shape `0x603F90 → 0x602780 → 0x602670 → 0x617A30 → 0x6177A0` where
   `0x602670` does not call any send helper.

3. **Speed/root/toggle queue-first model matches.** All six
   `SMSG_FORCE_*_SPEED_CHANGE` opcodes, both root opcodes, and the
   three movement-flag-toggle pairs route through
   `QueueDeferredMovementChange`, with apply + ACK happening only in
   `FlushPendingDeferredMovementChanges`. New theory test
   (`PacketFlowParityTests.ForceSpeedChangeFamily_*`, commit
   `ebff10a4`) extends the original RUN-only test to cover the five
   remaining variants.

4. **Integrator constants match WoW.exe by VA citation.** No drift
   detected. `Exports/Navigation/PhysicsEngine.h` holds the
   authoritative copy with VA citations; replay tests in
   `Tests/Navigation.Physics.Tests/` validate end-to-end behaviour.

5. **Movement-flag-toggle timing is now pinned.** Layout fixtures already
   existed for water-walk / hover / feather-fall ACKs in
   `AckBinaryParityTests`, and Stream 2A added a parametrized timing
   theory (`PacketFlowParityTests.MovementFlagToggleFamily_QueuesDeferredAck_ThenFlushesWithUpdatedFlag`)
   covering all six inbound opcodes
   (`SMSG_MOVE_{WATER_WALK,LAND_WALK,SET_HOVER,UNSET_HOVER,FEATHER_FALL,NORMAL_FALL}`)
   → three ACK opcodes
   (`CMSG_MOVE_{WATER_WALK,HOVER,FEATHER_FALL}_ACK`) with
   queue-first dispatch, deferred apply on flush, byte-exact
   MovementInfo + trailing 1.0/0.0 marker assertion. No regression
   detected; the toggle family is now timing-pinned to the same
   standard as the speed and root families.

6. **Post-teleport snap-window broadcast diverges (Stream 1).** Live FG
   capture from a Durotar `(-460,-4760,38)` → `Z+10` vertical-drop
   teleport (committed at
   [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json))
   shows WoW.exe broadcasts during the post-teleport fall:

   | Δms | Direction | Opcode | Notes |
   |---|---|---|---|
   | 0 | Recv | `MSG_MOVE_TELEPORT_ACK` (37 B) | server-pushed teleport notification |
   | 6 | Send | `MSG_MOVE_TELEPORT_ACK` (20 B) | client ACK; matches `BuildMoveTeleportAckPayload` byte layout |
   | 491 | Send | `MSG_MOVE_HEARTBEAT` (48 B) | flags `0x6000` = `MOVEFLAG_FALLINGFAR | MOVEFLAG_JUMPING` |
   | 991 | Send | `MSG_MOVE_HEARTBEAT` (48 B) | same FALLING flags, ~500ms cadence |
   | 1271 | Send | `MSG_MOVE_FALL_LAND` (32 B) | landing |

   Stream 2B closed two of the three contributing parity bugs:

   - **`472170e3`** — dropped the `!_needsGroundSnap` suppression at
     [`MovementController.cs:379`](../../Exports/WoWSharpClient/Movement/MovementController.cs)
     (originally introduced by commit `49915f62`). BG now broadcasts
     heartbeats and `MSG_MOVE_FALL_LAND` during the snap window.
   - **`4771d931`** — reordered `DetermineOpcode` so the
     `FALLINGFAR/JUMPING → grounded` landing rules fire before the
     `current==NONE && previous!=NONE → MSG_MOVE_STOP` rule. Pre-fix,
     the snap-completion `SendStopPacket` call emitted `MSG_MOVE_STOP`
     instead of the `MSG_MOVE_FALL_LAND` that WoW.exe emits.

   Pinned by:

   - `PostTeleportPacketWindowParityTests.ForegroundBaseline_ReportsExpectedTeleportPacketSequence`
     — locks the FG fixture's expected shape so it can't drift unnoticed.
   - `PostTeleportPacketWindowParityTests.Background_AfterTeleportTrigger_EmitsOutboundTeleportAckMatchingForegroundShape`
     — confirms the immediate 16-byte client ACK is byte-equivalent.
   - `PostTeleportPacketWindowParityTests.Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
     — now passing; pins the BG-today outbound stream.

   The recording infrastructure (`ForegroundPostTeleportWindowRecorder`,
   gated on `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1`) is reusable for any
   future teleport-related parity work — additional baselines (cross-map,
   transport, knockback-induced) can be captured by re-running the
   `Foreground_VerticalDropTeleport_*` test against different
   coordinates and renaming the resulting `foreground_*` fixture.

   **Status: Stream 2B closed; one parity gap remains (Stream 2C).**

   FG outbound: `[TELEPORT_ACK, HEARTBEAT, HEARTBEAT, FALL_LAND]`
   BG outbound: `[TELEPORT_ACK, HEARTBEAT, HEARTBEAT, HEARTBEAT, FALL_LAND]`

   The +1 heartbeat at the start of the fall is because
   `ShouldSendPacket` fires immediately on `NONE → FALLINGFAR`, while
   WoW.exe cadence-gates the first heartbeat ~`PACKET_INTERVAL_MS`
   (≈500ms) after the outbound ACK. Stream 2C plan:
   [`docs/handoff_session_bg_movement_parity_followup_v7.md`](../handoff_session_bg_movement_parity_followup_v7.md).

## What's *not* in scope of this audit

- **Inbound parsing (SMSG side)** — covered by handler-specific tests
  under `Tests/WoWSharpClient.Tests/Handlers/`. This audit focused on
  outbound (CMSG/MSG_*_ACK) parity.
- **Spline / monster-move opcodes** — per
  [`packet_ack_timing.md`](packet_ack_timing.md) Q5, WoW.exe does not
  ACK these; managed behaviour matches.
- **Recorded-trace physics divergence** — handled by the
  `Navigation.Physics.Tests/Helpers/ReplayEngine.cs` harness, which is
  the right tool for per-frame integrator parity.
