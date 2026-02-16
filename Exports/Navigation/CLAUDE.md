# Navigation - C++ Pathfinding & Physics Engine

Native C++ library using Detour/Recast for pathfinding and a custom physics engine. Accessed from C# via P/Invoke.

## Build

```bash
# Built via CMake (not MSBuild)
cmake -B build && cmake --build build
```

## Key Files

| File | Purpose |
|------|---------|
| `Navigation.cpp/.h` | Main P/Invoke entry points exported to C# |
| `PathFinder.cpp/.h` | A* pathfinding on navmesh |
| `PhysicsEngine.cpp/.h` | Core physics simulation (LARGE — read in chunks) |
| `PhysicsCollideSlide.cpp/.h` | Collision response and sliding |
| `PhysicsMovement.cpp/.h` | Character movement physics |
| `PhysicsGroundSnap.cpp/.h` | Ground detection and snapping |
| `MoveMap.cpp/.h` | Navmesh map loading |
| `MapLoader.cpp/.h` | Map data loading from files |
| `VMapManager2.cpp/.h` | Visual map management for LOS checks |
| `StaticMapTree.cpp/.h` | BIH tree for ray intersection queries |

## Architecture

- **P/Invoke boundary**: C# calls into `Navigation.dll` via exported functions in `Navigation.h`
- **Physics docs**: `docs/physics/README.md` (comprehensive physics engine documentation)
- Companion C++ libs: `Exports/Loader/` (CLR bootstrapper), `Exports/FastCall/` (x86 calling convention bridge)

## Warning

Many files are large (1000+ lines). Always use offset/limit when reading.
