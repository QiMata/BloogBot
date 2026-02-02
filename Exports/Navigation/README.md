# Navigation - Pathfinding & Physics Simulation DLL
# Navigation Project

A C++ navigation library that provides pathfinding, collision detection, and physics simulation capabilities for World of Warcraft game environments. This project wraps the Detour navigation mesh library and integrates with the game's movement and physics systems.

## Overview

**Navigation** is a native C++ dynamic-link library that provides pathfinding and physics simulation for character movement in World of Warcraft-style game environments. It implements:
The Navigation project is a C++ dynamic library (.dll) that provides:
- **Pathfinding**: A* pathfinding using navigation meshes
- **Line of Sight**: Raycast-based visibility checks  
- **Collision Detection**: Capsule overlap queries for movement validation
- **Physics Integration**: Ground height detection and terrain queries
- **Multi-Map Support**: Support for multiple WoW continents/maps

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
### Core Components

## Technical Details
- **Navigation.h/cpp**: Main API and singleton manager
- **PathFinder.h/cpp**: A* pathfinding implementation
- **MoveMap.h/cpp**: Navigation mesh management
- **Detour Library**: Third-party navigation mesh library (included)
- **Physics System**: Collision and terrain height detection
- **VMap Integration**: World Model Object (WMO) mesh integration

### Build Configuration
### Key Classes

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

#### `Navigation` (Singleton)
The main interface class providing:
```cpp
extern "C" __declspec(dllexport) void PreloadMap(uint32_t mapId);
class Navigation {
public:
    static Navigation* GetInstance();
    void Initialize();
    XYZ* CalculatePath(unsigned int mapId, XYZ start, XYZ end, bool straightPath, int* length);
    bool IsLineOfSight(uint32_t mapId, const XYZ& a, const XYZ& b);
    std::vector<NavPoly> CapsuleOverlap(uint32_t mapId, const XYZ& pos, float radius, float height);
    bool RaycastToWmoMesh(unsigned int mapId, float startX, float startY, float startZ, 
                          float endX, float endY, float endZ, float* hitX, float* hitY, float* hitZ);
    // ... other methods
};
```

**Usage**: Call before pathfinding/physics to ensure data is loaded. Reduces first-call latency.

### `FindPath`
Calculates an A* path between two points.

#### `XYZ` Structure
Basic 3D coordinate structure:
```cpp
extern "C" __declspec(dllexport) XYZ* FindPath(
    uint32_t mapId,    // Map ID (0=Eastern Kingdoms, 1=Kalimdor, etc.)
    XYZ start,         // Start position (WoW coordinates)
    XYZ end,           // End position (WoW coordinates)
    bool smoothPath,   // Apply path smoothing
    int* length        // Output: number of waypoints
);
// Returns: Array of XYZ waypoints (caller must free with PathArrFree)
class XYZ {
public:
    float X, Y, Z;
    XYZ(double X, double Y, double Z);
};
```

### `PathArrFree`
Frees a path array returned by `FindPath`.

#### `NavPoly` Structure
Navigation polygon information:
```cpp
extern "C" __declspec(dllexport) void PathArrFree(XYZ* pathArr);
struct NavPoly {
    uint64_t refId;       // Detour poly reference
    uint32_t area;        // Area type (ground, water, lava, etc.)
    uint32_t flags;       // Walk/swim/door flags
    uint32_t vertCount;   // Number of vertices (3-6)
    XYZ verts[6];         // World-space vertices
};
```

### `PhysicsStepV2`
Performs one physics simulation step.
## Features

```cpp
extern "C" __declspec(dllexport) PhysicsOutput PhysicsStepV2(
    const PhysicsInput& input  // Current state and movement intent
);
// Returns: New state after simulation
```
### Pathfinding
- **A* Algorithm**: Efficient pathfinding using navigation meshes
- **Smooth Paths**: Optional path smoothing for natural movement
- **Multi-Terrain**: Support for ground, water, and flying movement
- **Dynamic Loading**: Navigation meshes loaded on-demand per map

**Usage**: Called every frame to update character position based on input and collisions.
### Collision Detection
- **Capsule Queries**: Check for collisions using character capsules
- **Overlap Sweeps**: Moving capsule collision detection
- **Line of Sight**: Raycast-based visibility testing
- **WMO Integration**: World Model Object collision support

### `LineOfSight`
Tests line-of-sight between two points.
### Map Support
Currently supports these WoW maps:
- **Map 0**: Eastern Kingdoms
- **Map 1**: Kalimdor  
- **Map 389**: Ragefire Chasm (dungeon)

```cpp
extern "C" __declspec(dllexport) bool LineOfSight(
    uint32_t mapId,
    XYZ from,
    XYZ to
);
// Returns: true if clear LOS, false if blocked
```
Additional maps can be added by placing `.mmtile` files in the `mmaps/` directory.

## Physics System
## C# Integration

The physics engine implements a **PhysX Character Controller Toolkit (CCT)** style movement system with authentic WoW behavior.
The library is consumed by C# applications through P/Invoke wrappers:

### Three-Pass Movement Decomposition
```csharp
// From PathfindingService/Repository/Navigation.cs
public class Navigation {
    [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FindPath(uint mapId, XYZ start, XYZ end, bool smoothPath, out int length);
    
Following the PhysX CCT pattern, each movement frame is decomposed into three passes:

    public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath) {
        // Implementation...
    }
}
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
## Usage Examples

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

### Basic Pathfinding
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
Navigation* nav = Navigation::GetInstance();
nav->Initialize();

### Collide-and-Slide
XYZ start(1629.36f, -4373.39f, 50.2564f);
XYZ end(-616.2514f, -4188.0044f, 82.316719f);
int pathLength;

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
XYZ* path = nav->CalculatePath(1, start, end, true, &pathLength);
// Use path...
nav->FreePathArr(path);
```

Returns all intersections with:
- **VMAP geometry** (WMO buildings, M2 objects)
- **ADT terrain** (heightmap triangles)

### Overlap Tests

### Line of Sight Check
```cpp
// Test capsule overlap at a position
static int OverlapCapsule(const VMAP::StaticMapTree& map,
                          const CapsuleCollision::Capsule& capsule,
                          std::vector<SceneHit>& outOverlaps,
                          uint32_t includeMask = 0xFFFFFFFFu);
XYZ from(1629.0f, -4373.0f, 53.0f);
XYZ to(1630.0f, -4372.0f, 53.0f);

// Test sphere overlap
static int OverlapSphere(const VMAP::StaticMapTree& map,
                         const G3D::Vector3& center,
                         float radius,
                         std::vector<SceneHit>& outOverlaps,
                         uint32_t includeMask = 0xFFFFFFFFu);
bool canSee = nav->IsLineOfSight(1, from, to);
```

### SceneHit Structure

### Collision Detection
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
XYZ position(100.0f, 200.0f, 50.0f);
float radius = 0.5f;
float height = 2.0f;

## Navigation (Pathfinding)
auto polygons = nav->CapsuleOverlap(1, position, radius, height);
bool hasCollision = !polygons.empty();
```

### MMap Integration
## Dependencies

Uses MaNGOS-style `.mmap` navmesh files:
### Third-Party Libraries
- **Detour**: Navigation mesh library (included in `Detour/` folder)
- **g3dlite**: Math and utility library (included in `g3dlite/` folder)

```cpp
class Navigation
{
public:
    static Navigation* GetInstance();
    void Initialize();
### System Requirements
- **Windows**: Windows 10+ (uses Windows APIs for DLL loading)
- **Visual Studio**: 2019+ with C++20 support
- **Platform**: x86/x64 support

    // Calculate A* path
    XYZ* CalculatePath(unsigned int mapId, XYZ start, XYZ end, 
                       bool straightPath, int* length);
## Build Configuration

    // Line of sight check via navmesh
    bool IsLineOfSight(uint32_t mapId, const XYZ& a, const XYZ& b);
The project is configured to build as a Dynamic Library (.dll) with these key settings:

    // Get navmesh query for direct Detour access
    const dtNavMeshQuery* GetQueryForMap(uint32_t mapId);
};
### Preprocessor Definitions
```
WIN32;_CONSOLE;_LIB;USE_STANDARD_MALLOC;PREPARED_SLN;_WINDOWS;_WIN32;_CRT_SECURE_NO_WARNINGS;CMAKE_INTDIR="Release";DT_POLYREF64
```

### Navmesh Data Files
### Include Directories
- `Detour\Include`: Detour navigation library headers
- `g3dlite\Include`: Math library headers  
- `Utilities\`: Project utilities

Place `.mmap` files in the `mmaps/` directory:
- `000.mmap`, `001.mmap`, etc. (map headers)
- `000_XX_YY.mmtile` (individual tiles)
### Language Standards
- **C++**: C++20 (stdcpp20)
- **C**: C17 (stdc17)

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
Navigation/
??? Navigation.h/cpp          # Main API
??? PathFinder.h/cpp          # A* pathfinding
??? MoveMap.h/cpp            # Navigation mesh management
??? Detour/                  # Navigation mesh library
?   ??? Include/             # Detour headers
?   ??? Source/              # Detour implementation
??? g3dlite/                 # Math library
??? Utilities/               # Project utilities
??? VMap integration files   # World model collision
??? README.md               # This file
```

## C# Integration
## Testing

### P/Invoke Declarations (in PathfindingService)
The Navigation library is tested through:
- **PathfindingService.Tests**: End-to-end pathfinding tests
- **Integration Tests**: Real-world WoW coordinate testing
- **Performance Tests**: Path calculation benchmarks

Example test coordinates:
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
// Kalimdor test points
Position start = new(-616.2514f, -4188.0044f, 82.316719f);
Position end = new(1629.36f, -4373.39f, 50.2564f);

[DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool LineOfSight(uint mapId, XYZ from, XYZ to);
// Ragefire Chasm test points  
Position from = new(-247.728561f, -30.644503f, -58.082531f);
Position to = new(-158.395340f, 5.857921f, -42.873611f);
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
## Performance

### Data Files
### Optimizations
- **On-Demand Loading**: Navigation meshes loaded only when needed
- **Spatial Indexing**: BVH trees for fast polygon queries
- **Memory Management**: Custom allocators for Detour
- **Caching**: Mesh data cached between queries

The Navigation DLL requires external data files:
- `mmaps/` - Navigation mesh tiles (generate with mmap-extractor)
- `vmaps/` - Visual map data (generate with vmap-extractor)
- `maps/` - ADT terrain data (generate with map-extractor)
### Benchmarks
- **Path Calculation**: ~1-5ms for typical in-game distances
- **Line of Sight**: ~0.1-1ms per query
- **Collision Queries**: ~0.5-2ms depending on area complexity

## Performance Considerations
## Troubleshooting

### Pathfinding
- First path request per map triggers navmesh loading (~100-500ms)
- Subsequent paths on loaded maps are fast (~1-10ms)
- Use `PreloadMap` during startup to avoid first-call latency
### Common Issues

### Physics
- `PhysicsStepV2` is designed to be called every frame (~60Hz)
- Capsule sweeps against VMAP are the most expensive operation
- BVH acceleration structure keeps queries O(log n)
#### Missing Navigation Data
```
Error: Could not find navigation mesh for map X
Solution: Ensure .mmtile files exist in mmaps/ directory
```

## Troubleshooting
#### DLL Loading Failures
```
Error: Unable to load Navigation.dll
Solution: Check that all dependencies are in the same directory
```

### "Navigation mesh not found"
- Verify `.mmap` files exist in `mmaps/` directory
- Check map ID matches the files (e.g., `000.mmap` for Eastern Kingdoms)
#### Invalid Coordinates
```
Error: Path calculation returns empty result
Solution: Verify coordinates are within valid map bounds
```

### Character falls through world
- Ensure VMAP data is present for the area
- Check if `STEP_DOWN_HEIGHT` is sufficient for terrain
- Verify capsule dimensions match character size
### Debug Build
For debugging, use the Debug configuration which includes:
- Full debug symbols
- Runtime type information
- Detailed error reporting
- Memory leak detection

### Physics jitter/oscillation
- Check `GROUND_HEIGHT_TOLERANCE` isn't too tight
- Ensure frame rate is stable (physics assumes consistent dt)
- Verify ground normal calculations aren't fighting
## Contributing

## Related Components
When contributing to the Navigation project:

| Component | Relationship |
|-----------|--------------|
| **PathfindingService** | Wraps Navigation.dll via P/Invoke |
| **BotRunner** | Uses PathfindingClient to request paths |
| **WoWSharpClient** | Uses physics output for movement packets |
| **ForegroundBotRunner** | Can use direct memory for position |
1. **Follow C++20 Standards**: Use modern C++ features appropriately
2. **Maintain API Compatibility**: Changes should not break existing C# integration
3. **Add Tests**: Include tests for new pathfinding features
4. **Document Changes**: Update this README for significant modifications
5. **Performance**: Profile changes that affect pathfinding performance

## References
## License

- [Recast/Detour](https://github.com/recastnavigation/recastnavigation) - Navigation library
- [PhysX CCT](https://nvidia-omniverse.github.io/PhysX/physx/5.3.1/docs/CharacterControllers.html) - Character controller design
- [MaNGOS](https://www.getmangos.eu/) - MMap format reference
- [WoWDev Wiki](https://wowdev.wiki/) - WoW file format documentation
This project integrates with the Detour navigation library, which is provided under the zlib license. See individual source files for specific license information.

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform. See [ARCHITECTURE.md](../../ARCHITECTURE.md) for system-wide documentation.*
*This README covers the core Navigation C++ library. For C# integration details, see the PathfindingService project documentation.*