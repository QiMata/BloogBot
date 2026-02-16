# Tests - Test Infrastructure

11 test projects using **MSTest + Moq**. Run all with `dotnet test WestworldOfWarcraft.sln`.

## Test Projects

| Project | Tests |
|---------|-------|
| `BotRunner.Tests/` | Core behavior tree and orchestration tests |
| `Navigation.Physics.Tests/` | Physics engine unit tests |
| `PathfindingService.Tests/` | Pathfinding algorithm tests |
| `PromptHandlingService.Tests/` | Dialog automation tests |
| `WoWSharpClient.Tests/` | Protocol parsing tests |
| `WowSharpClient.NetworkTests/` | Network/socket tests |
| `WoWSimulation/` | Full simulation integration tests |
| `RecordedTests.PathingTests.Tests/` | Navigation regression tests |
| `RecordedTests.Shared.Tests/` | Shared orchestration tests |
| `Tests.Infrastructure/` | Shared test utilities (not a test project itself) |
| `Bot/` | End-to-end bot tests |

## Test Output

- Results directory: `TestResults/`
- Format: `.trx` (Test Results XML)

## Running Specific Tests

```bash
dotnet test WestworldOfWarcraft.sln --filter "FullyQualifiedName~Navigation.Physics"
dotnet test WestworldOfWarcraft.sln --filter "FullyQualifiedName~WoWSharpClient"
```
