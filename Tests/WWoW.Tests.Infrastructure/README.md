# WWoW.Tests.Infrastructure

Shared test infrastructure for WWoW integration and unit tests.

## Overview

This project provides common utilities, fixtures, and categorization for organizing tests across the WWoW solution.

## Test Categories

### Distinguishing Unit vs Integration Tests

| Category | Characteristics | Run Command |
|----------|-----------------|-------------|
| **Unit** | Fast, isolated, no external dependencies | `dotnet test --filter "Category=Unit"` |
| **Integration** | Requires external services (WoW server, PathfindingService) | `dotnet test --filter "Category=Integration"` |
| **EndToEnd** | Full system tests | `dotnet test --filter "Category=EndToEnd"` |
| **Performance** | Benchmarks and performance validation | `dotnet test --filter "Category=Performance"` |

### Running Tests

```bash
# Run all tests
dotnet test

# Run only unit tests (fast, no external dependencies)
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Exclude integration tests (useful for CI quick checks)
dotnet test --filter "Category!=Integration"

# Run tests requiring specific services
dotnet test --filter "RequiresService=WoWServer"
dotnet test --filter "RequiresService=PathfindingService"

# Run tests for specific features
dotnet test --filter "Feature=Pathfinding"
dotnet test --filter "Feature=Movement"
```

## Using Test Attributes

### Simple Categorization

```csharp
using WWoW.Tests.Infrastructure;

// Mark as unit test
[UnitTest]
public class MyClassTests
{
    [Fact]
    public void MyMethod_ShouldDoSomething() { }
}

// Mark as integration test requiring WoW server
[RequiresWoWServer]
public class MyIntegrationTests
{
    [Fact]
    public void Connection_ShouldSucceed() { }
}

// Mark as integration test requiring PathfindingService
[RequiresPathfindingService]
public class PathfindingTests
{
    [Fact]
    public void Path_ShouldBeCalculated() { }
}
```

### Manual Trait Usage

```csharp
// Using xUnit Traits directly
[Trait(TestCategories.Category, TestCategories.Integration)]
[Trait(TestCategories.RequiresService, TestCategories.WoWServer)]
[Trait(TestCategories.Feature, TestCategories.Movement)]
public void Movement_ShouldUpdatePosition() { }
```

## Configuration

Integration tests can be configured via environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `WWOW_TEST_AUTH_IP` | `127.0.0.1` | WoW auth server IP |
| `WWOW_TEST_AUTH_PORT` | `3724` | WoW auth server port |
| `WWOW_TEST_WORLD_PORT` | `8085` | WoW world server port |
| `WWOW_TEST_PATHFINDING_IP` | `127.0.0.1` | PathfindingService IP |
| `WWOW_TEST_PATHFINDING_PORT` | `5001` | PathfindingService port |
| `WWOW_TEST_USERNAME` | `TESTBOT1` | Test account username |
| `WWOW_TEST_PASSWORD` | `PASSWORD` | Test account password |

### CI/CD Example

```yaml
# GitHub Actions example
- name: Run Unit Tests
  run: dotnet test --filter "Category=Unit"

- name: Run Integration Tests
  env:
    WWOW_TEST_AUTH_IP: ${{ secrets.WOW_SERVER_IP }}
    WWOW_TEST_USERNAME: ${{ secrets.TEST_ACCOUNT }}
    WWOW_TEST_PASSWORD: ${{ secrets.TEST_PASSWORD }}
  run: dotnet test --filter "Category=Integration"
```

## Service Health Checking

Integration tests automatically skip when required services are unavailable:

```csharp
public class MyIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task MyTest()
    {
        // Automatically skips if WoW server is not running
        SkipIfServerUnavailable();
        
        // Your test code...
    }
}
```

## Project Structure

```
Tests/
??? WWoW.Tests.Infrastructure/      # This project - shared test utilities
?   ??? IntegrationTestConfig.cs    # Configuration and service health checking
?   ??? TestCategories.cs           # Test category constants and attributes
??? WoWSharpClient.Tests/           # Unit + Integration tests for WoWSharpClient
?   ??? Handlers/                   # Unit tests (packet parsing)
?   ??? Integration/                # Integration tests (live server)
??? PathfindingService.Tests/       # Tests for PathfindingService
??? BotRunner.Tests/                # Tests for BotRunner
??? Navigation.Physics.Tests/       # Tests for physics/navigation
```

## Best Practices

1. **Unit tests should**:
   - Be fast (< 100ms)
   - Not require external services
   - Use mocks for dependencies
   - Be deterministic

2. **Integration tests should**:
   - Use `[IntegrationTest]` or `[RequiresWoWServer]` attributes
   - Call skip methods when services unavailable
   - Clean up after themselves
   - Be idempotent (can run multiple times)

3. **Test organization**:
   - Place unit tests in the root of test projects
   - Place integration tests in `Integration/` folder
   - Use descriptive test names: `MethodName_Scenario_ExpectedResult`
