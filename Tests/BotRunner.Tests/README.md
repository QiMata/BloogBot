# BotRunner.Tests

Unit tests for the BotRunner library, validating behavior tree execution, state machine transitions, and client communication.

## Overview

This test project validates:
- **Behavior Trees**: Node execution, selector/sequence logic
- **State Machines**: State transitions and trigger handling
- **Pathfinding Client**: Request/response serialization
- **Name Generator**: Valid name generation

## Test Categories

### Behavior Tree Tests

```csharp
public class BehaviorTreeTests
{
    [Fact]
    public void Selector_FirstChildSucceeds_ReturnsSuccess()
    {
        // Test selector OR logic
    }
    
    [Fact]
    public void Sequence_AllChildrenSucceed_ReturnsSuccess()
    {
        // Test sequence AND logic
    }
    
    [Fact]
    public void Sequence_ChildFails_ReturnsFailure()
    {
        // Test sequence short-circuit
    }
    
    [Fact]
    public void Condition_PredicateTrue_ReturnsSuccess()
    {
        // Test condition nodes
    }
}
```

### State Machine Tests

```csharp
public class StateMachineTests
{
    [Fact]
    public void Fire_ValidTrigger_TransitionsState()
    {
        // Test valid state transition
    }
    
    [Fact]
    public void Fire_InvalidTrigger_ThrowsException()
    {
        // Test invalid transition handling
    }
    
    [Fact]
    public void OnEntry_Called_WhenEnteringState()
    {
        // Test entry actions
    }
}
```

### Name Generator Tests

```csharp
public class NameGeneratorTests
{
    [Fact]
    public void GenerateName_ReturnsValidName()
    {
        // Test name generation
    }
    
    [Fact]
    public void GenerateName_CorrectLength()
    {
        // Test name length constraints
    }
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test Tests/BotRunner.Tests

# Run with verbose output
dotnet test Tests/BotRunner.Tests -v normal

# Run with coverage
dotnet test Tests/BotRunner.Tests --collect:"XPlat Code Coverage"
```

### Visual Studio

1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All" or select specific tests

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.5.3 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | VS Test integration |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK |
| coverlet.collector | 6.0.0 | Code coverage |

## Project References

None currently - add reference to BotRunner for testing:

```xml
<ProjectReference Include="..\..\Exports\BotRunner\BotRunner.csproj" />
```

## Related Documentation

- See `Exports/BotRunner/README.md` for library details
