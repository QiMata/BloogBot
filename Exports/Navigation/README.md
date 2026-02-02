# Navigation - Pathfinding & Physics Simulation DLL

## Overview

**Navigation** is a native C++ dynamic-link library that provides pathfinding and physics simulation for character movement in World of Warcraft-style game environments. It implements:

1. **A\* Pathfinding** using Detour/Recast navigation meshes
2. **PhysX-style Character Controller** physics with 3-pass movement decomposition
3. **Scene Queries** for collision detection against VMAP geometry and terrain
4. **Liquid Evaluation** for water/lava/slime swimming detection

This component is consumed by the **PathfindingService** via P/Invoke and provides the core movement simulation for both headless (BackgroundBotRunner) and injected (ForegroundBotRunner) bot implementations.

## Architecture

```
???????????????????????????????????????????????????????????????????????????????
?                         Navigation.dll Architecture                          ?
???????????????????????????????????????????????????????????????????????????????
?                                                                             ?
?   ???????????????????????????????????????????????????????????????????????   ?
?   ?                        DLL Exports (DllMain.cpp)                    ?   ?
?   ?  PreloadMap | FindPath | PhysicsStepV2 | LineOfSight | PathArrFree  ?   ?
?   ???????????????????????????????????????????????????????????????????????   ?
?                    ?                              ?                         ?
?         ??????????????????????        ??????????????????????               ?
?         ?                    ?        ?                    ?               ?
?   ?????????????????   ?????????????????????????????????????????????       ?
?   ?  Navigation   ?   ?            PhysicsEngine                   ?       ?
?   ?  (Pathfinding)?   ?         (Movement Simulation)              ?       ?
?   ?               ?   ?                                            ?       ?
?   ? • A* Search   ?   ? • Three-Pass Movement (UP?SIDE?DOWN)       ?       ?
?   ? • Path Smooth ?   ? • Collide-and-Slide                        ?       ?
?   ? • LoS Checks  ?   ? • Ground Snap & Step-Up                    ?       ?
?   ?????????????????   ? • Swimming & Falling                       ?       ?
?         ?             ??????????????????????????????????????????????       ?
?         ?                              ?                                    ?
?         ?                              ?                                    ?
?   ?????????????????   ?????????????????????????????????????????????       ?
?   ?  MoveMap      ?   ?            SceneQuery                      ?       ?
?   ?  (MaNGOS)     ?   ?       (Collision Detection)                ?       ?
?   ?               ?   ?                                            ?       ?
?   ? • .mmap tiles ?   ? • Capsule Sweeps                           ?       ?
?   ? • NavMesh I/O ?   ? • Sphere/Box Overlaps                      ?       ?
?   ?????????????????   ? • Ray Casts                                ?       ?
?         ?             ? • Liquid Queries                           ?       ?
?         ?             ??????????????????????????????????????????????       ?
?   ?????????????????                    ?                                    ?
?   ?   Detour      ?                    ?                                    ?
?   ?  (Recast)     ?   ?????????????????????????????????????????????       ?
?   ?               ?   ?       VMapManager2 / MapLoader             ?       ?
?   ? • dtNavMesh   ?   ?        (Geometry Sources)                  ?       ?
?   ? • dtNavQuery  ?   ?                                            ?       ?
?   ?????????????????   ? • VMAP models (WMO/M2)                     ?       ?
?                       ? • ADT terrain heightmaps                   ?       ?
?                       ? • Liquid volumes                           ?       ?
?                       ?????????????????????????????????????????????       ?
?                                                                             ?
???????????????????????????????????????????????????????????????????????????????
```

## Technical Details

### Build Configuration

| Property | Value |
|----------|-------|
| Project Type | Dynamic Library (DLL) |
| Platform Toolset | v143 (Visual Studio 2022) |
| Character Set | MultiByte |
| C++ Standard | C++20 (Debug), C++ Latest (Release) |
| Target Platforms | Win32, x64 |

### Output Locations

| Configuration | Output Path |
|---------------|-------------|
| Debug (Win32) | `..\..\Bot\Debug\net8.0\Navigation.dll` |
| Release (Win32) | `..\Bot\Release\net8.0\Navigation.dll` |
| Debug (x64) | `..\..\Bot\Debug\net8.0\Navigation.dll` |
| Release (x64) | `..\Bot\Release\net8.0\Navigation.dll` |

### Dependencies

- **Detour/Recast** - Navigation mesh library (included in `Detour/` subdirectory)
- **G3D Lite** - Math library for Vector3, Ray, AABox (included in `g3dlite/` subdirectory)
- **Windows SDK 10.0** - Core Windows APIs

### Preprocessor Definitions

```
WIN32;_CONSOLE;_LIB;USE_STANDARD_MALLOC;PREPARED_SLN;
_WINDOWS;_WIN32;_CRT_SECURE_NO_WARNINGS;DT_POLYREF64
```

## DLL Exports

### `PreloadMap`
Preloads navigation mesh and VMAP geometry for a map.

```cpp
extern "C" __declspec(dllexport) void PreloadMap(uint32_t mapId);
```

**Usage**: Call before pathfinding/physics to ensure data is loaded. Reduces first-call latency.

### `FindPath`
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

### `PathArrFree`
Frees a path array returned by `FindPath`.

```cpp
extern "C" __declspec(dllexport) void PathArrFree(XYZ* pathArr);
```

### `PhysicsStepV2`
Performs one physics simulation step.

```cpp
extern "C" __declspec(dllexport) PhysicsOutput PhysicsStepV2(
    const PhysicsInput& input  // Current state and movement intent
);
// Returns: New state after simulation
```

**Usage**: Called every frame to update character position based on input and collisions.

### `LineOfSight`
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

Following the PhysX CCT pattern, each movement frame is decomposed into three passes:

```
Frame Movement: (moveDir, distance)
         ?
         ?
???????????????????????
?     UP PASS         ?  Step-up lift (STEP_HEIGHT) + any upward intent
?  ?                  ?  • Sweep upward for ceiling check
?  ? stepOffset       ?  • Clamp if hit obstacle
???????????????????????
         ?
         ?
???????????????????????
?    SIDE PASS        ?  Horizontal collide-and-slide
?  ????????????????   ?  • Iterative wall collision (MAX_SLIDE_ITERATIONS)
?                     ?  • Project movement along surfaces
???????????????????????
         ?
         ?
???????????????????????
?    DOWN PASS        ?  Undo step offset + gravity + ground snap
?  ?                  ?  • Sweep downward (STEP_DOWN_HEIGHT)
?  ? gravity + snap   ?  • Validate walkable slope
???????????????????????
         ?
         ?
    Final Position
```

### Physics Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `GRAVITY` | 19.29 | WoW gravity (yards/s²) |
| `JUMP_VELOCITY` | 7.96 | Initial jump velocity (yards/s) |
| `STEP_HEIGHT` | 2.125 | Maximum auto-step height (yards) |
| `STEP_DOWN_HEIGHT` | 4.0 | Maximum ground snap distance (yards) |
| `GROUND_HEIGHT_TOLERANCE` | 0.04 | Ground detection tolerance |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.5 | Walkable slope threshold (cos 60°) |
| `WATER_LEVEL_DELTA` | 2.0 | Swimming transition depth |

### Movement Flags

The physics system uses WoW movement flags for state tracking:

```cpp
MOVEFLAG_FORWARD        = 0x00000001  // Moving forward
MOVEFLAG_BACKWARD       = 0x00000002  // Moving backward
MOVEFLAG_STRAFE_LEFT    = 0x00000004  // Strafing left
MOVEFLAG_STRAFE_RIGHT   = 0x00000008  // Strafing right
MOVEFLAG_JUMPING        = 0x00002000  // In jump
MOVEFLAG_FALLINGFAR     = 0x00004000  // Falling (airborne)
MOVEFLAG_SWIMMING       = 0x00200000  // In water
MOVEFLAG_FLYING         = 0x01000000  // Flying mode
// ... (see PhysicsBridge.h for complete list)
```

### Collide-and-Slide

When the character hits a surface during SIDE pass:

1. **Compute slide tangent** - Project movement along the surface
2. **Handle corners** - If two surfaces met, slide along their intersection (crease)
3. **Iterate** - Repeat up to `MAX_SLIDE_ITERATIONS` (4) to resolve complex geometry

## Scene Query System

`SceneQuery` provides collision detection against world geometry:

### Capsule Sweeps

```cpp
// Sweep a capsule through the world
static int SweepCapsule(
    uint32_t mapId,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    std::vector<SceneHit>& outHits,
    const G3D::Vector3& playerForward
);
```

Returns all intersections with:
- **VMAP geometry** (WMO buildings, M2 objects)
- **ADT terrain** (heightmap triangles)

### Overlap Tests

```cpp
// Test capsule overlap at a position
static int OverlapCapsule(const VMAP::StaticMapTree& map,
                          const CapsuleCollision::Capsule& capsule,
                          std::vector<SceneHit>& outOverlaps,
                          uint32_t includeMask = 0xFFFFFFFFu);

// Test sphere overlap
static int OverlapSphere(const VMAP::StaticMapTree& map,
                         const G3D::Vector3& center,
                         float radius,
                         std::vector<SceneHit>& outOverlaps,
                         uint32_t includeMask = 0xFFFFFFFFu);
```

### SceneHit Structure

```cpp
struct SceneHit
{
    bool hit;                    // Intersection occurred
    float distance;              // Travel distance (TOI)
    float time;                  // Normalized [0,1] fraction
    float penetrationDepth;      // Overlap depth
    G3D::Vector3 normal;         // Contact normal
    G3D::Vector3 point;          // Contact point
    int triIndex;                // Triangle index
    uint32_t instanceId;         // Model instance ID
    bool startPenetrating;       // Started overlapping
    HitFeatureType featureType;  // Face/Edge/Vertex
    CapsuleRegion region;        // Cap0/Side/Cap1
};
```

## Navigation (Pathfinding)

### MMap Integration

Uses MaNGOS-style `.mmap` navmesh files:

```cpp
class Navigation
{
public:
    static Navigation* GetInstance();
    void Initialize();
    
    // Calculate A* path
    XYZ* CalculatePath(unsigned int mapId, XYZ start, XYZ end, 
                       bool straightPath, int* length);
    
    // Line of sight check via navmesh
    bool IsLineOfSight(uint32_t mapId, const XYZ& a, const XYZ& b);
    
    // Get navmesh query for direct Detour access
    const dtNavMeshQuery* GetQueryForMap(uint32_t mapId);
};
```

### Navmesh Data Files

Place `.mmap` files in the `mmaps/` directory:
- `000.mmap`, `001.mmap`, etc. (map headers)
- `000_XX_YY.mmtile` (individual tiles)

## File Structure

```
Exports/Navigation/
??? Navigation.vcxproj          # Visual Studio project
??? DllMain.cpp                 # DLL entry point and exports
??? README.md                   # This documentation
?
??? Core Pathfinding:
?   ??? Navigation.cpp/.h       # A* pathfinding, LoS
?   ??? MoveMap.cpp/.h          # MaNGOS navmesh loading
?   ??? PathFinder.cpp/.h       # Detour path queries
?   ??? MoveMapSharedDefines.h  # Shared constants
?
??? Physics Engine:
?   ??? PhysicsEngine.cpp/.h    # Main physics coordinator
?   ??? PhysicsBridge.h         # C++/C# interop structures
?   ??? PhysicsTolerances.h     # Skin width, contact offsets
?   ??? PhysicsThreePass.cpp/.h # UP/SIDE/DOWN decomposition
?   ??? PhysicsCollideSlide.cpp/.h  # Wall sliding
?   ??? PhysicsGroundSnap.cpp/.h    # Ground detection
?   ??? PhysicsMovement.cpp/.h      # Air/swim movement
?   ??? PhysicsHelpers.cpp/.h       # Utility functions
?   ??? PhysicsLiquidHelpers.cpp/.h # Water/lava evaluation
?   ??? PhysicsDiagnosticsHelpers.cpp/.h # Debug logging
?   ??? PhysicsSelectHelpers.h      # Hit selection
?   ??? PhysicsShapeHelpers.h       # Capsule builders
?   ??? PhysicsMath.h               # Math utilities
?
??? Scene Queries:
?   ??? SceneQuery.cpp/.h       # Capsule sweeps, overlaps
?   ??? CapsuleCollision.h      # Capsule primitives
?   ??? QueryHit.h              # Hit result structures
?
??? Geometry:
?   ??? MapLoader.cpp/.h        # VMAP/ADT loading
?   ??? VMapManager2.cpp/.h     # VMAP access
?   ??? VMapFactory.cpp/.h      # VMAP factory
?   ??? StaticMapTree.cpp/.h    # BVH for models
?   ??? WorldModel.cpp/.h       # WMO geometry
?   ??? ModelInstance.cpp/.h    # Model transforms
?   ??? AABox.cpp/.h            # Axis-aligned boxes
?   ??? BIH.cpp/.h/.inl         # Bounding interval hierarchy
?   ??? Ray.cpp/.h              # Ray casting
?   ??? Vector3.cpp/.h          # 3D vector math
?   ??? Matrix3.cpp/.h          # 3x3 matrix
?   ??? CoordinateTransforms.h  # WoW?Detour coords
?
??? Detour/                     # Recast/Detour library
?   ??? Include/
?   ?   ??? DetourNavMesh.h
?   ?   ??? DetourNavMeshQuery.h
?   ?   ??? ...
?   ??? Source/
?       ??? DetourNavMesh.cpp
?       ??? ...
?
??? g3dlite/                    # G3D math library
    ??? Include/G3D/
        ??? Vector3.h
        ??? Ray.h
        ??? ...
```

## C# Integration

### P/Invoke Declarations (in PathfindingService)

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

### Coordinate System

WoW uses a right-handed coordinate system:
- **X**: East-West (positive = East)
- **Y**: North-South (positive = North)  
- **Z**: Up-Down (positive = Up)

Detour uses a different convention, so `CoordinateTransforms.h` provides conversions.

## Building

### Prerequisites

- Visual Studio 2022 with C++ Desktop Development workload
- Windows 10/11 SDK

### Build Steps

1. Open `BloogBot.sln` in Visual Studio 2022
2. Select desired configuration (Debug/Release) and platform (Win32/x64)
3. Build the Navigation project: **Build ? Build Navigation**
4. Output DLL will be in the configured output directory

### Data Files

The Navigation DLL requires external data files:
- `mmaps/` - Navigation mesh tiles (generate with mmap-extractor)
- `vmaps/` - Visual map data (generate with vmap-extractor)
- `maps/` - ADT terrain data (generate with map-extractor)

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

### "Navigation mesh not found"
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

## References

- [Recast/Detour](https://github.com/recastnavigation/recastnavigation) - Navigation library
- [PhysX CCT](https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/CharacterControllers.html) - Character controller design
- [MaNGOS](https://www.getmangos.eu/) - MMap format reference
- [WoWDev Wiki](https://wowdev.wiki/) - WoW file format documentation

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform. See [ARCHITECTURE.md](../../ARCHITECTURE.md) for system-wide documentation.*
