# Navigation

A native C++ dynamic-link library that provides pathfinding, collision detection, and physics simulation for World of Warcraft game environments.

## Overview

Navigation is a C++ DLL that implements:

1. **A* Pathfinding** using Detour/Recast navigation meshes
2. **PhysX-style Character Controller** physics with 3-pass movement decomposition
3. **Scene Queries** for collision detection against VMAP geometry and terrain
4. **Liquid Evaluation** for water/lava/slime swimming detection

This component is consumed by the **PathfindingService** via P/Invoke and provides the core movement simulation for both headless (BackgroundBotRunner) and injected (ForegroundBotRunner) bot implementations.

## Architecture

```
+-----------------------------------------------------------------------------+
|                         Navigation.dll Architecture                          |
+-----------------------------------------------------------------------------+
|                                                                              |
|   +--------------------------------------------------------------------+    |
|   |                    DLL Exports (DllMain.cpp)                       |    |
|   |  PreloadMap | FindPath | PhysicsStepV2 | LineOfSight | PathArrFree |    |
|   +--------------------------------------------------------------------+    |
|                    |                              |                          |
|         +----------+----------+        +---------+---------+                 |
|         |                     |        |                   |                 |
|   +-------------+      +--------------------------------------+              |
|   |  Navigation |      |            PhysicsEngine             |              |
|   | (Pathfinding)|     |         (Movement Simulation)        |              |
|   |             |      |                                      |              |
|   | - A* Search |      | - Three-Pass Movement (UP>SIDE>DOWN) |              |
|   | - Path Smooth|     | - Collide-and-Slide                  |              |
|   | - LoS Checks|      | - Ground Snap & Step-Up              |              |
|   +-------------+      | - Swimming & Falling                 |              |
|         |              +--------------------------------------+              |
|         |                              |                                     |
|   +-------------+      +--------------------------------------+              |
|   |  MoveMap    |      |            SceneQuery                |              |
|   |  (MaNGOS)   |      |       (Collision Detection)          |              |
|   |             |      |                                      |              |
|   | - .mmap tiles|     | - Capsule Sweeps                     |              |
|   | - NavMesh I/O|     | - Sphere/Box Overlaps                |              |
|   +-------------+      | - Ray Casts                          |              |
|         |              | - Liquid Queries                     |              |
|   +-------------+      +--------------------------------------+              |
|   |   Detour    |                      |                                     |
|   |  (Recast)   |      +--------------------------------------+              |
|   |             |      |       VMapManager2 / MapLoader       |              |
|   | - dtNavMesh |      |        (Geometry Sources)            |              |
|   | - dtNavQuery|      |                                      |              |
|   +-------------+      | - VMAP models (WMO/M2)               |              |
|                        | - ADT terrain heightmaps             |              |
|                        | - Liquid volumes                     |              |
|                        +--------------------------------------+              |
|                                                                              |
+-----------------------------------------------------------------------------+
```

## Project Structure

```
Exports/Navigation/
+-- Navigation.vcxproj          # Visual Studio project
+-- DllMain.cpp                 # DLL entry point and exports
+-- README.md                   # This documentation
|
+-- Core Pathfinding:
|   +-- Navigation.cpp/.h       # A* pathfinding, LoS
|   +-- MoveMap.cpp/.h          # MaNGOS navmesh loading
|   +-- PathFinder.cpp/.h       # Detour path queries
|   +-- MoveMapSharedDefines.h  # Shared constants
|
+-- Physics Engine:
|   +-- PhysicsEngine.cpp/.h    # Main physics coordinator
|   +-- PhysicsBridge.h         # C++/C# interop structures
|   +-- PhysicsTolerances.h     # Skin width, contact offsets
|   +-- PhysicsCollideSlide.cpp/.h  # Wall sliding
|   +-- PhysicsGroundSnap.cpp/.h    # Ground detection
|   +-- PhysicsMovement.cpp/.h      # Air/swim movement
|   +-- PhysicsHelpers.cpp/.h       # Utility functions
|   +-- PhysicsLiquidHelpers.cpp/.h # Water/lava evaluation
|   +-- PhysicsDiagnosticsHelpers.cpp/.h # Debug logging
|
+-- Scene Queries:
|   +-- SceneQuery.cpp/.h       # Capsule sweeps, overlaps
|   +-- CapsuleCollision.h      # Capsule primitives
|   +-- QueryHit.h              # Hit result structures
|
+-- Geometry:
|   +-- MapLoader.cpp/.h        # VMAP/ADT loading
|   +-- VMapManager2.cpp/.h     # VMAP access
|   +-- StaticMapTree.cpp/.h    # BVH for models
|   +-- WorldModel.cpp/.h       # WMO geometry
|   +-- AABox.cpp/.h            # Axis-aligned boxes
|   +-- BIH.cpp/.h              # Bounding interval hierarchy
|   +-- Ray.cpp/.h              # Ray casting
|   +-- Vector3.cpp/.h          # 3D vector math
|   +-- CoordinateTransforms.h  # WoW<->Detour coords
|
+-- Detour/                     # Recast/Detour library
|   +-- Include/
|   +-- Source/
|
+-- g3dlite/                    # G3D math library
    +-- Include/G3D/
```

## Key Components

| Component | Description |
|-----------|-------------|
| `Navigation` | Main singleton interface for pathfinding and LoS |
| `PhysicsEngine` | Three-pass movement simulation with collide-and-slide |
| `SceneQuery` | Capsule sweeps, overlaps, and raycasts |
| `MoveMap` | MaNGOS-style navmesh loading and management |
| `PathFinder` | Detour path query wrapper |
| `VMapManager2` | VMAP geometry access |

## Dependencies

| Component | Purpose |
|-----------|---------|
| Detour/Recast | Navigation mesh library (included in `Detour/` subdirectory) |
| G3D Lite | Math library for Vector3, Ray, AABox (included in `g3dlite/` subdirectory) |
| Windows SDK 10.0 | Core Windows APIs |

## DLL Exports

### PreloadMap
Preloads navigation mesh and VMAP geometry for a map.
```cpp
extern "C" __declspec(dllexport) void PreloadMap(uint32_t mapId);
```

### FindPath
Calculates an A* path between two points.
```cpp
extern "C" __declspec(dllexport) XYZ* FindPath(
    uint32_t mapId,    // Map ID (0=Eastern Kingdoms, 1=Kalimdor, etc.)
    XYZ start,         // Start position (WoW coordinates)
    XYZ end,           // End position (WoW coordinates)
    bool smoothPath,   // Apply path smoothing
    int* length        // Output: number of waypoints
);
// Returns: Array of XYZ waypoints (caller must free with PathArrFree)
```

### PathArrFree
Frees a path array returned by `FindPath`.
```cpp
extern "C" __declspec(dllexport) void PathArrFree(XYZ* pathArr);
```

### PhysicsStepV2
Performs one physics simulation step.
```cpp
extern "C" __declspec(dllexport) PhysicsOutput PhysicsStepV2(
    const PhysicsInput& input  // Current state and movement intent
);
// Returns: New state after simulation
```

### LineOfSight
Tests line-of-sight between two points.
```cpp
extern "C" __declspec(dllexport) bool LineOfSight(
    uint32_t mapId,
    XYZ from,
    XYZ to
);
// Returns: true if clear LOS, false if blocked
```

## Physics System

The physics engine implements a **PhysX Character Controller Toolkit (CCT)** style movement system with authentic WoW behavior.

### Three-Pass Movement Decomposition

```
Frame Movement: (moveDir, distance)
         |
         v
+---------------------+
|     UP PASS         |  Step-up lift (STEP_HEIGHT) + any upward intent
|  ^                  |  - Sweep upward for ceiling check
|  | stepOffset       |  - Clamp if hit obstacle
+---------------------+
         |
         v
+---------------------+
|    SIDE PASS        |  Horizontal collide-and-slide
|  --------------->   |  - Iterative wall collision (MAX_SLIDE_ITERATIONS)
|                     |  - Project movement along surfaces
+---------------------+
         |
         v
+---------------------+
|    DOWN PASS        |  Undo step offset + gravity + ground snap
|  |                  |  - Sweep downward (STEP_DOWN_HEIGHT)
|  v gravity + snap   |  - Validate walkable slope
+---------------------+
         |
         v
    Final Position
```

### Physics Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `GRAVITY` | 19.29 | WoW gravity (yards/s^2) |
| `JUMP_VELOCITY` | 7.96 | Initial jump velocity (yards/s) |
| `STEP_HEIGHT` | 2.125 | Maximum auto-step height (yards) |
| `STEP_DOWN_HEIGHT` | 4.0 | Maximum ground snap distance (yards) |
| `GROUND_HEIGHT_TOLERANCE` | 0.04 | Ground detection tolerance |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.5 | Walkable slope threshold (cos 60 deg) |
| `WATER_LEVEL_DELTA` | 2.0 | Swimming transition depth |

## Usage

### Basic Pathfinding
```cpp
Navigation* nav = Navigation::GetInstance();
nav->Initialize();

XYZ start(1629.36f, -4373.39f, 50.2564f);
XYZ end(-616.2514f, -4188.0044f, 82.316719f);
int pathLength;

XYZ* path = nav->CalculatePath(1, start, end, true, &pathLength);
// Use path...
nav->FreePathArr(path);
```

### Line of Sight Check
```cpp
XYZ from(1629.0f, -4373.0f, 53.0f);
XYZ to(1630.0f, -4372.0f, 53.0f);

bool canSee = nav->IsLineOfSight(1, from, to);
```

### Collision Detection
```cpp
XYZ position(100.0f, 200.0f, 50.0f);
float radius = 0.5f;
float height = 2.0f;

auto polygons = nav->CapsuleOverlap(1, position, radius, height);
bool hasCollision = !polygons.empty();
```

## C# Integration

P/Invoke declarations in PathfindingService:

```csharp
[DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern void PreloadMap(uint mapId);

[DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr FindPath(uint mapId, XYZ start, XYZ end, 
                                     bool smoothPath, out int length);

[DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern void PathArrFree(IntPtr pathArr);

[DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern PhysicsOutput PhysicsStepV2(ref PhysicsInput input);

[DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool LineOfSight(uint mapId, XYZ from, XYZ to);
```

## Build Configuration

| Property | Value |
|----------|-------|
| Project Type | Dynamic Library (DLL) |
| Platform Toolset | v143 (Visual Studio 2022) |
| Character Set | MultiByte |
| C++ Standard | C++20 |
| Target Platforms | Win32, x64 |

### Preprocessor Definitions
```
WIN32;_CONSOLE;_LIB;USE_STANDARD_MALLOC;PREPARED_SLN;
_WINDOWS;_WIN32;_CRT_SECURE_NO_WARNINGS;DT_POLYREF64
```

### Data Files

The Navigation DLL requires external data files:
- `mmaps/` - Navigation mesh tiles (generate with mmap-extractor)
- `vmaps/` - Visual map data (generate with vmap-extractor)
- `maps/` - ADT terrain data (generate with map-extractor)

## Map Support

Currently supports these WoW maps:
- **Map 0**: Eastern Kingdoms
- **Map 1**: Kalimdor  
- **Map 389**: Ragefire Chasm (dungeon)

Additional maps can be added by placing `.mmtile` files in the `mmaps/` directory.

## Performance Considerations

### Pathfinding
- First path request per map triggers navmesh loading (~100-500ms)
- Subsequent paths on loaded maps are fast (~1-10ms)
- Use `PreloadMap` during startup to avoid first-call latency

### Physics
- `PhysicsStepV2` is designed to be called every frame (~60Hz)
- Capsule sweeps against VMAP are the most expensive operation
- BVH acceleration structure keeps queries O(log n)

## Troubleshooting

### Navigation mesh not found
- Verify `.mmap` files exist in `mmaps/` directory
- Check map ID matches the files (e.g., `000.mmap` for Eastern Kingdoms)

### Character falls through world
- Ensure VMAP data is present for the area
- Check if `STEP_DOWN_HEIGHT` is sufficient for terrain
- Verify capsule dimensions match character size

### Physics jitter/oscillation
- Check `GROUND_HEIGHT_TOLERANCE` isn't too tight
- Ensure frame rate is stable (physics assumes consistent dt)
- Verify ground normal calculations aren't fighting

## Related Components

| Component | Relationship |
|-----------|--------------|
| **PathfindingService** | Wraps Navigation.dll via P/Invoke |
| **BotRunner** | Uses PathfindingClient to request paths |
| **WoWSharpClient** | Uses physics output for movement packets |
| **ForegroundBotRunner** | Can use direct memory for position |

## Related Documentation

- [Recast/Detour](https://github.com/recastnavigation/recastnavigation) - Navigation library
- [PhysX CCT](https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/CharacterControllers.html) - Character controller design
- [MaNGOS](https://www.getmangos.eu/) - MMap format reference
- [WoWDev Wiki](https://wowdev.wiki/) - WoW file format documentation

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
