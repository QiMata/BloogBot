# WWoW Physics System Documentation

This directory contains detailed documentation for the PhysX CCT-style character controller physics system used in WWoW's `Navigation.dll`.

## Overview

The physics system implements a **PhysX Character Controller Toolkit (CCT)** style movement system with three-pass decomposition (UP ? SIDE ? DOWN) for accurate WoW-like movement simulation.

## Document Index

### Core Call Flow (Numbered Sequence)

Read these documents in order for a complete understanding of the physics pipeline:

| Document | Description |
|----------|-------------|
| [01_CALL_GRAPH.md](./01_CALL_GRAPH.md) | Character Controller move call graph overview |
| [02_PARAMS_AND_STATE.md](./02_PARAMS_AND_STATE.md) | Parameters and state management |
| [03_PERFRAME_MOVE_PIPELINE.md](./03_PERFRAME_MOVE_PIPELINE.md) | Per-frame movement pipeline |
| [04_SWEEP_TEST_MOVE_CHARACTER.md](./04_SWEEP_TEST_MOVE_CHARACTER.md) | `moveCharacter` sweep test implementation |
| [05_DO_SWEEP_TEST.md](./05_DO_SWEEP_TEST.md) | `doSweepTest` function details |
| [06_COLLISION_RESPONSE.md](./06_COLLISION_RESPONSE.md) | Collision response and sliding |
| [07_OVERLAP_RECOVERY_COMPUTE_MTD.md](./07_OVERLAP_RECOVERY_COMPUTE_MTD.md) | Overlap recovery and MTD computation |
| [08_SLOPE_STEP_CEILING_RULES.md](./08_SLOPE_STEP_CEILING_RULES.md) | Slope, step, and ceiling handling rules |
| [09_RIDE_ON_TOUCHED_OBJECT.md](./09_RIDE_ON_TOUCHED_OBJECT.md) | Moving platform support |
| [10_PARITY_TEST_HARNESS.md](./10_PARITY_TEST_HARNESS.md) | Parity testing with reference implementation |

### Reference Documentation

| Document | Description |
|----------|-------------|
| [PHYSX_CCT_RULES.md](./PHYSX_CCT_RULES.md) | PhysX CCT behavioral rules reference |
| [PHYSX_CAPSULE_SWEEP_RULES.md](./PHYSX_CAPSULE_SWEEP_RULES.md) | Capsule sweep test rules |
| [SWEEP_TEST_MOVE_CHARACTER_REFERENCE.md](./SWEEP_TEST_MOVE_CHARACTER_REFERENCE.md) | Additional `moveCharacter` reference |
| [VANILLA_WOW_PHYSICS_INTENTION_RESEARCH.md](./VANILLA_WOW_PHYSICS_INTENTION_RESEARCH.md) | Research on WoW physics intentions |
| [WOW_PHYSICS_SERVICE_GUIDE.md](./WOW_PHYSICS_SERVICE_GUIDE.md) | Integration guide for the physics service |

### Packet Handling

| Document | Description |
|----------|-------------|
| [0x537AA0_disasm.txt](./0x537AA0_disasm.txt) | Raw `NetClient::ProcessMessage` dispatcher disassembly |
| [0x537AA0_pseudocode.md](./0x537AA0_pseudocode.md) | C-like translation of `NetClient::ProcessMessage` |
| [0x005379A0_disasm.txt](./0x005379A0_disasm.txt) | Raw `NetClient::Send` disassembly |
| [opcode_dispatch_table.md](./opcode_dispatch_table.md) | Static opcode -> handler registration map recovered from WoW.exe |
| [0x401B00_disasm.txt](./0x401B00_disasm.txt) | Raw `SMSG_NEW_WORLD` handler disassembly |
| [0x401BC0_disasm.txt](./0x401BC0_disasm.txt) | Raw deferred world-entry callback disassembly |
| [0x401DE0_disasm.txt](./0x401DE0_disasm.txt) | Raw `SMSG_LOGIN_VERIFY_WORLD` handler disassembly |
| [msg_move_worldport_ack.md](./msg_move_worldport_ack.md) | World-entry and `MSG_MOVE_WORLDPORT_ACK` send conditions |
| [0x4651A0_disasm.txt](./0x4651A0_disasm.txt) | Raw `SMSG_UPDATE_OBJECT` top-level dispatcher disassembly |
| [0x601580_disasm.txt](./0x601580_disasm.txt) | Raw movement-dispatch wrapper below `0x603BB0` |
| [0x602780_disasm.txt](./0x602780_disasm.txt) | Raw force-speed/root/flag dispatch wrapper below `0x603F90` |
| [0x603F90_disasm.txt](./0x603F90_disasm.txt) | Raw top-level wrapper for force-speed/root/flag opcodes |
| [0x602670_disasm.txt](./0x602670_disasm.txt) | Raw inbound `SMSG_MOVE_KNOCK_BACK` leaf |
| [0x6026F0_disasm.txt](./0x6026F0_disasm.txt) | Raw `MSG_MOVE_KNOCK_BACK` local path |
| [0x602FB0_disasm.txt](./0x602FB0_disasm.txt) | Raw teleport-ACK handler / sender path |
| [0x61A380_disasm.txt](./0x61A380_disasm.txt) | Raw water-walk / land-walk inbound toggle leaf |
| [0x61A430_disasm.txt](./0x61A430_disasm.txt) | Raw local water-walk apply helper |
| [0x61A490_disasm.txt](./0x61A490_disasm.txt) | Raw feather-fall / normal-fall inbound toggle leaf |
| [0x61A550_disasm.txt](./0x61A550_disasm.txt) | Raw local feather-fall apply helper |
| [0x61A5D0_disasm.txt](./0x61A5D0_disasm.txt) | Raw hover / unhover inbound toggle leaf |
| [0x61A700_disasm.txt](./0x61A700_disasm.txt) | Raw root / unroot inbound leaf |
| [0x616800_disasm.txt](./0x616800_disasm.txt) | Raw movement-counter initialization / refresh path |
| [0x617570_disasm.txt](./0x617570_disasm.txt) | Raw pending movement queue helpers |
| [0x619500_disasm.txt](./0x619500_disasm.txt) | Raw speed-change queue / apply helper family |
| [0x619DE0_disasm.txt](./0x619DE0_disasm.txt) | Raw companion-counter consumer and dequeue path |
| [smsg_force_speed_change_handler.md](./smsg_force_speed_change_handler.md) | Speed-change queue / apply notes |
| [smsg_force_move_root_handler.md](./smsg_force_move_root_handler.md) | Root / unroot queue behavior |
| [smsg_move_knock_back_handler.md](./smsg_move_knock_back_handler.md) | Inbound knockback staging notes |
| [smsg_move_flag_toggle_handler.md](./smsg_move_flag_toggle_handler.md) | Water-walk, feather-fall, and hover toggle notes |
| [msg_move_teleport_handler.md](./msg_move_teleport_handler.md) | Teleport apply vs teleport ACK send path |
| [msg_move_time_skipped_jump_land.md](./msg_move_time_skipped_jump_land.md) | `TIME_SKIPPED`, `JUMP`, and `FALL_LAND` dispatch/state notes |
| [raw_position_and_flight_ack.md](./raw_position_and_flight_ack.md) | Evidence that `0x00E0` / `0x0340` are not active WoW.exe ACK surfaces |
| [packet_ack_timing.md](./packet_ack_timing.md) | Q1-Q5 timing answers for knockback, speed, teleport, worldport, and spline/monster movement |
| [smsg_update_object_handler.md](./smsg_update_object_handler.md) | `SMSG_UPDATE_OBJECT` multi-pass dispatch and type-`5` findings |
| [cgobject_vtables.md](./cgobject_vtables.md) | Confirmed `CGObject_C` vfptr evidence and current limits |
| [cgobject_layout.md](./cgobject_layout.md) | Confirmed typed storage offsets from `0x466C70` and related helpers |
| [csharp_object_field_audit.md](./csharp_object_field_audit.md) | Managed `WoWObject` / `WoWUnit` / `WoWPlayer` / `WoWGameObject` field map against 1.12.1 descriptors |
| [movement_counter_tracking.md](./movement_counter_tracking.md) | `local + 0x128` / `local + 0x12C` counter trace |

## Physics Pipeline Summary

```
StepV2(PhysicsInput, dt) ? PhysicsOutput
  ?
  ?? Overlap Recovery (depenetration from previous tick)
  ?
  ?? Movement Mode Selection:
  ?     ?? Flying ? direct velocity integration
  ?     ?? Swimming ? ProcessSwimMovement()
  ?     ?? Airborne ? ProcessAirMovement() + gravity
  ?     ?? Grounded ? Three-Pass Move:
  ?           ?
  ?           ?? UP PASS: Step-up lift + ceiling check
  ?           ?? SIDE PASS: CollideAndSlide() horizontal
  ?           ?? DOWN PASS: Undo step offset + ground snap + GetGroundZ refinement
  ?           ?
  ?           ?? Walk Experiment (if landed on non-walkable):
  ?                 ?? Retry with stepOffset=0
  ?                 ?? Recovery sweep downward
  ?                 ?? Ground snap to walkable
  ?
  ?? Post-frame GetGroundZ safety net
  ?? Output: new position, velocity, moveFlags, groundZ, liquidZ
```

## Key Constants

From `Exports/Navigation/PhysicsEngine.h` (`PhysicsConstants` namespace):

| Constant | Value | Description |
|----------|-------|-------------|
| `GRAVITY` | 19.2911f | WoW gravity (yards/s²), client @ 0x0081DA58 |
| `JUMP_VELOCITY` | 7.9535f | sqrt(2*g*maxJumpHeight), computed inline in client |
| `TERMINAL_VELOCITY` | 60.148f | Max fall speed, client @ 0x0087D894 |
| `SAFE_FALL_TERMINAL_VELOCITY` | 7.0f | Terminal velocity with Safe Fall, client @ 0x0087D898 |
| `FALL_START_VELOCITY` | -0.1f | Initial downward nudge entering freefall |
| `STEP_HEIGHT` | 2.125f | Index 8 in 15-entry step table, client @ 0x008060CE |
| `STEP_DOWN_HEIGHT` | 4.0f | Max downward snap while grounded |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.6428f | cos(50°), client @ 0x0080DFFC |
| `WALKABLE_TAN_MAX_SLOPE` | 1.1918f | tan(50°), client @ 0x0080E008 |
| `NORMAL_PROBE_OFFSET` | 0.3f | Finite-difference terrain normal offset |
| `WATER_LEVEL_DELTA` | 2.0f | Swim threshold below water surface |
| `BASE_WALK_SPEED` | 2.5f | VMaNGOS baseMoveSpeed[] |
| `BASE_RUN_SPEED` | 7.0f | VMaNGOS baseMoveSpeed[] |
| `BASE_RUN_BACK_SPEED` | 4.5f | VMaNGOS baseMoveSpeed[] |
| `BASE_SWIM_SPEED` | 4.722222f | Server-side only (not in client binary) |
| `BASE_TURN_RATE` | π (3.141594f) | VMaNGOS baseMoveSpeed[] |
| `FALL_SAFE_DISTANCE` | 14.57f | Min fall distance before damage |
| `FALL_DAMAGE_COEFF` | 0.018f | VMaNGOS Player::HandleFall() |
| `FALL_DAMAGE_OFFSET` | 0.2426f | VMaNGOS Player::HandleFall() |
| `VECTOR_EPSILON` | 1e-6f | General vector magnitude / sweep distance epsilon |
| `GROUND_SNAP_EPSILON` | 1e-4f | Ground snap / candidate sorting epsilon |
| `OVERLAP_NORMAL_Z_FILTER` | 0.7f | Ignore overlaps with normal Z above this |
| `MAX_DEFERRED_DEPEN_PER_TICK` | 0.05f | Max depenetration per physics tick |
| `MAX_OVERLAP_RECOVER_ITERATIONS` | 4 | Max overlap recovery iterations per tick |
| `WATER_ENTRY_VELOCITY_DAMP` | 0.5f | Velocity damping on water entry |

## Source Code Locations

The physics system is implemented in:

```
Exports/Navigation/
??? PhysicsEngine.cpp/.h         # Main entry point (StepV2) + three-pass UP/SIDE/DOWN
??? PhysicsTolerances.h          # Contact offset, skin width, epsilon values
??? PhysicsBridge.h              # C++ ? C# interop structures (PhysicsInput/Output)
??? PhysicsCollideSlide.cpp/.h   # Iterative collide-and-slide (wall collision)
??? PhysicsGroundSnap.cpp/.h     # Ground detection (step-up, step-down, vertical sweep)
??? PhysicsMovement.cpp/.h       # Air/swim movement processing
??? PhysicsHelpers.cpp/.h        # Utility functions
??? SceneQuery.cpp/.h            # Capsule sweeps against VMAP/ADT geometry
```

## Implementation Notes

### GetGroundZ Refinement (Precision Enhancement)

The capsule sweep contact point lies at a **lateral offset** from the character's actual XY (capsule radius away from center). On slopes, projecting this contact via the plane equation introduces systematic Z error that grows with slope steepness:

```cpp
planeZ = pz - ((nx * (x - px) + ny * (y - py)) / nz);  // biased by lateral offset
```

To correct this, every ground snap function (`TryStepUpSnap`, `TryDownwardStepSnap`, `VerticalSweepSnapDown`, `ExecuteDownPass`) performs a **GetGroundZ refinement** — a direct VMAP/ADT height query at the exact character XY immediately after setting Z from the plane equation:

```cpp
float preciseZ = SceneQuery::GetGroundZ(mapId, x, y, z, searchDist);
if (VMAP::IsValidHeight(preciseZ) &&
    preciseZ <= z + 0.05f &&   // tight upward bound
    preciseZ >= z - 0.5f) {    // reasonable downward bound
    z = preciseZ;
}
```

A post-frame safety net in `StepV2` also queries GetGroundZ as a final check with tighter bounds than the per-snap refinements.

### Walk Experiment (PhysX Pattern)

When the three-pass move lands on a **non-walkable slope** (ground normal Z < `DEFAULT_WALKABLE_MIN_NORMAL_Z`), the engine performs a PhysX-style "walk experiment":

1. **Retry with stepOffset=0** — re-run the entire three-pass move without step-up injection
2. **Recovery sweep** — if the retry still lands on non-walkable and climbed upward, perform a downward `CollideAndSlide` sweep to undo the climb
3. **Ground snap** — after recovery, attempt `TryDownwardStepSnap` to find walkable ground below

This prevents characters from climbing steep slopes by undoing any upward progress that doesn't end on walkable terrain.

### DecomposeMovement (Inlined from PhysX CCT)

The `DecomposeMovement` function splits a movement vector into UP/SIDE/DOWN components with step offset injection:

- **Step offset cancelled** when jumping or when there's no horizontal movement
- **UP vector** = vertical upward component + step offset (if active)
- **SIDE vector** = horizontal component
- **DOWN vector** = vertical downward component (step offset undone in DOWN pass)

## Related Documentation

- [Navigation.dll README](../../Exports/Navigation/README.md) - Native library overview
- [PathfindingService README](../../Services/PathfindingService/README.md) - Service integration
- [ARCHITECTURE.md](../../ARCHITECTURE.md) - System architecture overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
