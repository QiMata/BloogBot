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
  ?           ?? DOWN PASS: Undo step offset + ground snap
  ?
  ?? Output: new position, velocity, moveFlags, groundZ, liquidZ
```

## Key Constants

From `Exports/Navigation/PhysicsTolerances.h`:

| Constant | Value | Description |
|----------|-------|-------------|
| `STEP_HEIGHT` | 0.6f | Max height for auto-stepping (stairs) |
| `STEP_DOWN_HEIGHT` | 0.5f | Max drop for ground snap |
| `GRAVITY` | 19.29f | WoW gravity (yards/s²) |
| `JUMP_VELOCITY` | 7.96f | Initial jump velocity |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.5f | ~60° max walkable slope |
| `WATER_LEVEL_DELTA` | 1.0f | Swim threshold below water surface |

## Source Code Locations

The physics system is implemented in:

```
Exports/Navigation/
??? PhysicsEngine.cpp/.h         # Main entry point (StepV2)
??? PhysicsTolerances.h          # Constants
??? PhysicsBridge.h              # C++ ? C# interop structures
??? PhysicsThreePass.cpp/.h      # UP/SIDE/DOWN decomposition
??? PhysicsCollideSlide.cpp/.h   # Collision response
??? PhysicsGroundSnap.cpp/.h     # Ground detection
??? PhysicsMovement.cpp/.h       # Air/swim movement
??? PhysicsHelpers.cpp/.h        # Utility functions
??? SceneQuery.cpp/.h            # Capsule sweeps against geometry
```

## Related Documentation

- [Navigation.dll README](../../Exports/Navigation/README.md) - Native library overview
- [PathfindingService README](../../Services/PathfindingService/README.md) - Service integration
- [ARCHITECTURE.md](../../ARCHITECTURE.md) - System architecture overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
