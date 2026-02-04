# Navigation.Physics.Tests

Physics unit tests for the WWoW character controller emulation system.

## Purpose

These tests validate the physics system that emulates WoW's character controller behavior, ensuring accurate collision detection, movement, and surface interaction.

## Test Categories

### 1. Capsule Sweep Primitive Tests (`CapsuleSweepPrimitiveTests.cs`)
- **Flat ground contact detection** - Validates downward sweep onto horizontal surfaces
- **Slope walkability** - Tests the 60° threshold for walkable surfaces
- **Step height validation** - Verifies the 2.125 yard step limit
- **Wall collision** - Tests blocking and sliding behavior
- **Ceiling detection** - Validates upward movement constraints

### 2. Three-Pass Movement Tests (`ThreePassMovementTests.cs`)
- **UP pass** - Step-up lift and jump handling
- **SIDE pass** - Horizontal collide-and-slide
- **DOWN pass** - Ground snap and step-down
- **Step cancellation** - Verifies step offset cancels during jumps

### 3. Collide-and-Slide Tests (`CollideAndSlideTests.cs`)
- **Single surface sliding** - Tests projection onto wall surfaces
- **Corner (crease) behavior** - Two-wall constraint handling
- **Iteration limits** - Ensures algorithm terminates
- **Speed preservation** - Validates correct slide ratios

### 4. Geometry Extraction Tests (`GeometryExtractionTests.cs`)
- **Terrain triangle extraction** - Validates ADT geometry reading
- **Slope distribution analysis** - Calibrates walkability threshold
- **Real geometry sweeps** - Tests against actual game data

## Physics Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `GRAVITY` | 19.2911 | WoW gravity (yards/s²) |
| `JUMP_VELOCITY` | 7.95577 | Initial jump velocity (yards/s) |
| `STEP_HEIGHT` | 2.125 | Max auto-step height (yards) |
| `STEP_DOWN_HEIGHT` | 4.0 | Max ground snap distance (yards) |
| `WALKABLE_MIN_NORMAL_Z` | 0.5 | cos(60°) - walkability threshold |

## Running Tests

### Without Game Data
Most primitive tests run without game data:
```bash
dotnet test --filter "Category!=RequiresGameData"
```

### With Game Data
For tests requiring WMO/ADT data:
1. Place `maps/` and `vmaps/` folders in the test output directory
2. Run all tests:
```bash
dotnet test
```

## Test Design Principles

### Atomic Tests
Each test validates a single, specific behavior:
- One assertion per physical property
- Known input geometry with calculable expected output
- No dependency on random values

### Calibration Tests
Some tests are designed to help calibrate physics constants:
- Slope distribution analysis
- Step height measurement from real stairs
- Ground height validation against known positions

### Boundary Tests
Critical thresholds are tested at exact boundaries:
- 59°, 60°, 61° slopes for walkability
- 2.0, 2.125, 2.2 yard steps
- Epsilon-close positions for penetration

## Adding New Tests

1. **For primitive operations**: Add to `CapsuleSweepPrimitiveTests.cs`
2. **For movement sequences**: Add to `ThreePassMovementTests.cs`
3. **For wall/corner behavior**: Add to `CollideAndSlideTests.cs`
4. **For real geometry validation**: Add to `GeometryExtractionTests.cs`

Use the helper classes:
- `GeometryTestHelpers` - Creates synthetic test geometry
- `PhysicsTestConstants` - Access physics constants
- `NavigationInterop` - P/Invoke to native physics

## Related Documentation

- `Exports/Navigation/PhysicsEngine.h` - Physics constants and algorithms
- `Exports/Navigation/CapsuleCollision.h` - Collision primitives
- `Exports/Navigation/PhysicsThreePass.cpp` - Three-pass implementation
- `ARCHITECTURE.md` - Overall system architecture

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
