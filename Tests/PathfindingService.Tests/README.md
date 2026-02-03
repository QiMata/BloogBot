# PathfindingService.Tests

Unit and integration tests for the PathfindingService, verifying pathfinding algorithms, line-of-sight checks, and physics simulation accuracy.

## Overview

This test project validates:
- **Path Calculation**: A* pathfinding produces valid, efficient paths
- **Line of Sight**: LoS checks correctly detect obstacles
- **Physics Simulation**: Movement physics match expected game behavior
- **Service Integration**: End-to-end service communication

## Test Categories

### Path Calculation Tests

```csharp
public class PathCalculationTests
{
    [Fact]
    public void CalculatePath_StraightLine_ReturnsDirectPath()
    {
        // Test simple A to B with no obstacles
    }
    
    [Fact]
    public void CalculatePath_WithObstacles_FindsValidPath()
    {
        // Test pathfinding around terrain
    }
    
    [Fact]
    public void CalculatePath_UnreachableDestination_ReturnsEmpty()
    {
        // Test handling of impossible paths
    }
}
```

### Line of Sight Tests

```csharp
public class LineOfSightTests
{
    [Fact]
    public void CheckLoS_ClearPath_ReturnsTrue()
    {
        // Test unobstructed view
    }
    
    [Fact]
    public void CheckLoS_BlockedByTerrain_ReturnsFalse()
    {
        // Test terrain occlusion
    }
}
```

### Physics Tests

```csharp
public class PhysicsTests
{
    [Fact]
    public void SimulateMovement_OnGround_StaysGrounded()
    {
        // Test gravity and ground collision
    }
    
    [Fact]
    public void SimulateMovement_OverEdge_Falls()
    {
        // Test falling physics
    }
    
    [Fact]
    public void SimulateMovement_InWater_Swims()
    {
        // Test liquid physics
    }
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test Tests/PathfindingService.Tests

# Run with coverage
dotnet test Tests/PathfindingService.Tests --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test Tests/PathfindingService.Tests --filter "FullyQualifiedName~PathCalculationTests"
```

### Visual Studio

1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All" or right-click specific tests

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.5.3 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | VS Test integration |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK |
| coverlet.collector | 6.0.0 | Code coverage |

## Project References

- **PathfindingService**: System under test
- **GameData.Core**: Game data types
- **WoWSharpClient**: Client models for test data

## Test Data

Tests may require:
- Map data files in the expected location
- Test fixtures with known terrain configurations

## Platform

Tests are configured for x64 platform to match the native Navigation.dll dependency.

## Related Documentation

- See `Services/PathfindingService/README.md` for service details
- See `Exports/Navigation/` for native pathfinding library

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
