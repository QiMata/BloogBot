# Tests/

15 projects: 14 test/harness projects (xUnit + Moq) plus the shared
`Tests.Infrastructure` library (fixtures, traits, health checks — **not** a test
project itself). Run everything with `dotnet test WestworldOfWarcraft.sln`.

| Project | Covers |
|---------|--------|
| `BotRunner.Tests/` | Behavior-tree/task lifecycle, orchestration, contract/drift tests, and the LiveValidation suite. Targets **x86** (FG injection) → needs x86 `Navigation.dll`. |
| `Navigation.Physics.Tests/` | Physics-engine replay and validation. Targets **x64** → needs x64 `Navigation.dll`. |
| `PathfindingService.Tests/` | A* pathfinding + physics service. |
| `WoWSharpClient.Tests/` | Protocol parsing, packet handlers, ObjectManager. |
| `WoWSharpClient.NetworkTests/` | Network/socket and reconnect-policy tests. |
| `PromptHandlingService.Tests/` | Dialog automation. |
| `ForegroundBotRunner.Tests/` | FG injection/memory tests. |
| `BloogBot.AI.Tests/` | Decision-engine / state-machine tests. |
| `WoWSimulation/` | Full simulation integration. |
| `RecordedTests.PathingTests.Tests/`, `RecordedTests.Shared.Tests/` | Navigation regression vs recorded runs. |
| `WoWStateManagerUI.Tests/` | WPF dashboard view-model tests. |
| `Systems.ServiceDefaults.Tests/` | Aspire service-default infrastructure. |
| `LoadTests/` (`LoadTestHarness`) | Load/soak harness. |
| `Tests.Infrastructure/` | Shared fixtures, traits, and helpers (utility library). |

> **Native-DLL note:** `dotnet test` for `BotRunner.Tests` needs the x86
> `Navigation.dll`. On a box without the C++ toolchain, file-only contract tests
> (e.g. `Spec/ProjectLayeringTests.cs`, `Spec/SkillsContractTests.cs`) can be
> validated by their PowerShell mirrors under `scripts/` instead.

- **Fixture architecture, collections, traits, and the mandatory LiveValidation
  test pattern:** [CLAUDE.md](CLAUDE.md).
- **Agent rules (test isolation, skip policy, Shodan):**
  `.github/instructions/tests.instructions.md`.
