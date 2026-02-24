# Tests - Test Infrastructure

11 test projects using **xUnit + Moq**. Run all with `dotnet test WestworldOfWarcraft.sln`.

## Test Projects

| Project | Tests |
|---------|-------|
| `BotRunner.Tests/` | Core behavior tree, orchestration, and integration tests |
| `Navigation.Physics.Tests/` | Physics engine replay and validation tests |
| `PathfindingService.Tests/` | Pathfinding algorithm and physics engine tests |
| `PromptHandlingService.Tests/` | Dialog automation tests |
| `WoWSharpClient.Tests/` | Protocol parsing, handler, and agent tests |
| `WowSharpClient.NetworkTests/` | Network/socket, reconnect policy tests |
| `WoWSimulation/` | Full simulation integration tests |
| `RecordedTests.PathingTests.Tests/` | Navigation regression tests |
| `RecordedTests.Shared.Tests/` | Shared orchestration tests |
| `Tests.Infrastructure/` | Shared fixtures, traits, and utilities (not a test project itself) |

## Fixture Architecture

```
Tests.Infrastructure/
├── IntegrationTestConfig        — env-based config (ports, IPs, timeouts)
├── ServiceHealthChecker         — TCP health checks for auth/world/MySQL/services
├── MangosServerFixture          — auth + world + MySQL availability (IAsyncLifetime)
├── BotServiceFixture            — composes MangosServerFixture + StateManager auto-start + FastCall check
├── TestCategories               — trait constants and convenience attributes
├── Skip                         — conditional test skipping (xunit.SkippableFact wrapper)
├── WoWProcessManager            — WoW.exe process launch/injection management
└── InfrastructureTestGuard      — defined in BotRunner.Tests (process cleanup for collection)
```

### Fixture Layers

| Layer | Fixture | Scope | What it checks |
|-------|---------|-------|----------------|
| Config | `IntegrationTestConfig` | Static | Env vars: ports, IPs, timeouts |
| Service avail. | `MangosServerFixture` | Per-class | Auth server, world server, MySQL |
| Full stack | `BotServiceFixture` | Per-class | MaNGOS + StateManager + FastCall.dll |
| Serialization | `InfrastructureTestGuard` | Per-collection | Process cleanup between tests |
| Physics | `PhysicsEngineFixture` | Per-collection | Native DLL + data dir init |

### Test Collections

| Collection | Purpose | Defined in |
|------------|---------|-----------|
| `"Infrastructure"` | Serializes all integration tests touching WoW/StateManager | `BotRunner.Tests/InfrastructureTestCollection.cs` |
| `"Sequential ObjectManager tests"` | Serializes handler tests using shared ObjectManager | `WoWSharpClient.Tests/Handlers/ObjectManagerFixture.cs` |
| `"PhysicsEngine"` | Serializes physics tests sharing native DLL state | `Navigation.Physics.Tests/PhysicsTestFixtures.cs` |

### Trait Attributes

| Attribute | Category Filter | Use |
|-----------|----------------|-----|
| `[UnitTest]` | `Category=Unit` | Fast isolated tests |
| `[IntegrationTest]` | `Category=Integration` | Tests requiring external services |
| `[RequiresInfrastructure]` | `Category=RequiresInfrastructure` | Tests needing WoW/StateManager processes |
| `[RequiresMangosStack]` | `RequiresService=MangosStack` | Tests needing MaNGOS servers |
| `[RequiresWoWServer]` | `RequiresService=WoWServer` | Tests needing running WoW server |

### When to use which fixture

- **Just need MySQL/auth/world?** Use `IClassFixture<MangosServerFixture>`
- **Need StateManager too?** Use `BotServiceFixture` (manually instantiated)
- **Need process serialization?** Add `[Collection(InfrastructureTestCollection.Name)]`
- **Handler tests touching ObjectManager?** Use `[Collection("Sequential ObjectManager tests")]`
- **Physics tests?** Use `[Collection("PhysicsEngine")]`

## Running Tests

```bash
# All tests
dotnet test WestworldOfWarcraft.sln

# By project
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -v n
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj -v n

# By filter
dotnet test --filter "Category=Unit"                              # Unit tests only
dotnet test --filter "Category!=RequiresInfrastructure"           # Skip infrastructure tests
dotnet test --filter "FullyQualifiedName~Navigation.Physics"      # Physics tests
dotnet test --filter "FullyQualifiedName~MovementRecording"       # Specific test
```

## Test Output

- Results directory: `TestResults/`
- Format: `.trx` (Test Results XML)
- RunSettings: Embedded in each test csproj (no `--settings` flag needed)
