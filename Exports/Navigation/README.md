# Navigation Project

A C++ navigation library that provides pathfinding, collision detection, and physics simulation capabilities for World of Warcraft game environments. This project wraps the Detour navigation mesh library and integrates with the game's movement and physics systems.

## Overview

The Navigation project is a C++ dynamic library (.dll) that provides:
- **Pathfinding**: A* pathfinding using navigation meshes
- **Line of Sight**: Raycast-based visibility checks  
- **Collision Detection**: Capsule overlap queries for movement validation
- **Physics Integration**: Ground height detection and terrain queries
- **Multi-Map Support**: Support for multiple WoW continents/maps

## Architecture

### Core Components

- **Navigation.h/cpp**: Main API and singleton manager
- **PathFinder.h/cpp**: A* pathfinding implementation
- **MoveMap.h/cpp**: Navigation mesh management
- **Detour Library**: Third-party navigation mesh library (included)
- **Physics System**: Collision and terrain height detection
- **VMap Integration**: World Model Object (WMO) mesh integration

### Key Classes

#### `Navigation` (Singleton)
The main interface class providing:
```cpp
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

#### `XYZ` Structure
Basic 3D coordinate structure:
```cpp
class XYZ {
public:
    float X, Y, Z;
    XYZ(double X, double Y, double Z);
};
```

#### `NavPoly` Structure
Navigation polygon information:
```cpp
struct NavPoly {
    uint64_t refId;       // Detour poly reference
    uint32_t area;        // Area type (ground, water, lava, etc.)
    uint32_t flags;       // Walk/swim/door flags
    uint32_t vertCount;   // Number of vertices (3-6)
    XYZ verts[6];         // World-space vertices
};
```

## Features

### Pathfinding
- **A* Algorithm**: Efficient pathfinding using navigation meshes
- **Smooth Paths**: Optional path smoothing for natural movement
- **Multi-Terrain**: Support for ground, water, and flying movement
- **Dynamic Loading**: Navigation meshes loaded on-demand per map

### Collision Detection
- **Capsule Queries**: Check for collisions using character capsules
- **Overlap Sweeps**: Moving capsule collision detection
- **Line of Sight**: Raycast-based visibility testing
- **WMO Integration**: World Model Object collision support

### Map Support
Currently supports these WoW maps:
- **Map 0**: Eastern Kingdoms
- **Map 1**: Kalimdor  
- **Map 389**: Ragefire Chasm (dungeon)

Additional maps can be added by placing `.mmtile` files in the `mmaps/` directory.

## C# Integration

The library is consumed by C# applications through P/Invoke wrappers:

```csharp
// From PathfindingService/Repository/Navigation.cs
public class Navigation {
    [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FindPath(uint mapId, XYZ start, XYZ end, bool smoothPath, out int length);
    
    public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath) {
        // Implementation...
    }
}
```

## Usage Examples

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

## Dependencies

### Third-Party Libraries
- **Detour**: Navigation mesh library (included in `Detour/` folder)
- **g3dlite**: Math and utility library (included in `g3dlite/` folder)

### System Requirements
- **Windows**: Windows 10+ (uses Windows APIs for DLL loading)
- **Visual Studio**: 2019+ with C++20 support
- **Platform**: x86/x64 support

## Build Configuration

The project is configured to build as a Dynamic Library (.dll) with these key settings:

### Preprocessor Definitions
```
WIN32;_CONSOLE;_LIB;USE_STANDARD_MALLOC;PREPARED_SLN;_WINDOWS;_WIN32;_CRT_SECURE_NO_WARNINGS;CMAKE_INTDIR="Release";DT_POLYREF64
```

### Include Directories
- `Detour\Include`: Detour navigation library headers
- `g3dlite\Include`: Math library headers  
- `Utilities\`: Project utilities

### Language Standards
- **C++**: C++20 (stdcpp20)
- **C**: C17 (stdc17)

## File Structure

```
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

## Testing

The Navigation library is tested through:
- **PathfindingService.Tests**: End-to-end pathfinding tests
- **Integration Tests**: Real-world WoW coordinate testing
- **Performance Tests**: Path calculation benchmarks

Example test coordinates:
```csharp
// Kalimdor test points
Position start = new(-616.2514f, -4188.0044f, 82.316719f);
Position end = new(1629.36f, -4373.39f, 50.2564f);

// Ragefire Chasm test points  
Position from = new(-247.728561f, -30.644503f, -58.082531f);
Position to = new(-158.395340f, 5.857921f, -42.873611f);
```

## Performance

### Optimizations
- **On-Demand Loading**: Navigation meshes loaded only when needed
- **Spatial Indexing**: BVH trees for fast polygon queries
- **Memory Management**: Custom allocators for Detour
- **Caching**: Mesh data cached between queries

### Benchmarks
- **Path Calculation**: ~1-5ms for typical in-game distances
- **Line of Sight**: ~0.1-1ms per query
- **Collision Queries**: ~0.5-2ms depending on area complexity

## Troubleshooting

### Common Issues

#### Missing Navigation Data
```
Error: Could not find navigation mesh for map X
Solution: Ensure .mmtile files exist in mmaps/ directory
```

#### DLL Loading Failures
```
Error: Unable to load Navigation.dll
Solution: Check that all dependencies are in the same directory
```

#### Invalid Coordinates
```
Error: Path calculation returns empty result
Solution: Verify coordinates are within valid map bounds
```

### Debug Build
For debugging, use the Debug configuration which includes:
- Full debug symbols
- Runtime type information
- Detailed error reporting
- Memory leak detection

## Contributing

When contributing to the Navigation project:

1. **Follow C++20 Standards**: Use modern C++ features appropriately
2. **Maintain API Compatibility**: Changes should not break existing C# integration
3. **Add Tests**: Include tests for new pathfinding features
4. **Document Changes**: Update this README for significant modifications
5. **Performance**: Profile changes that affect pathfinding performance

## License

This project integrates with the Detour navigation library, which is provided under the zlib license. See individual source files for specific license information.

---

*This README covers the core Navigation C++ library. For C# integration details, see the PathfindingService project documentation.*