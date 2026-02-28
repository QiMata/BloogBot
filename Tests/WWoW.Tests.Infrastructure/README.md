# WWoW.Tests.Infrastructure

Shared test infrastructure for WWoW integration and unit tests. Provides configuration, service health checking, process management, and test categorization.

## Quick Commands

```bash
# Run all tests in this project
dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"

# Run by component
dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~IntegrationTestConfig" --logger "console;verbosity=minimal"
dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ServiceHealthChecker" --logger "console;verbosity=minimal"
dotnet test Tests/WWoW.Tests.Infrastructure/WWoW.Tests.Infrastructure.csproj --configuration Release --no-restore --filter "FullyQualifiedName~WoWProcessManager" --logger "console;verbosity=minimal"

# Run by category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category!=Integration"
```

## Components

| File | Purpose |
|------|---------|
| `IntegrationTestConfig.cs` | Config from env vars, `ServiceHealthChecker`, `RequiredServices` flags |
| `TestCategories.cs` | Trait constants and convenience attributes (`[UnitTest]`, `[RequiresWoWServer]`, etc.) |
| `WoWProcessManager.cs` | WoW process launch, DLL injection, and lifecycle management |

## Configuration (Environment Variables)

| Variable | Default | Description |
|----------|---------|-------------|
| `WWOW_TEST_AUTH_IP` | `127.0.0.1` | WoW auth server IP |
| `WWOW_TEST_AUTH_PORT` | `3724` | WoW auth server port |
| `WWOW_TEST_WORLD_PORT` | `8085` | WoW world server port |
| `WWOW_TEST_PATHFINDING_IP` | `127.0.0.1` | PathfindingService IP |
| `WWOW_TEST_PATHFINDING_PORT` | `5001` | PathfindingService port |
| `WWOW_TEST_USERNAME` | `TESTBOT1` | Test account username |
| `WWOW_TEST_PASSWORD` | `PASSWORD` | Test account password |

## Task Tracking

See `Tests/WWoW.Tests.Infrastructure/TASKS.md` for active tasks and session handoff.
