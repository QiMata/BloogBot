# Contributing to WWoW (Westworld of Warcraft)

Thank you for your interest in contributing to the WWoW project! This document provides guidelines and instructions for contributing to the codebase.

## Table of Contents

- [Brand Guidelines](#brand-guidelines)
- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Documentation Requirements](#documentation-requirements)

## Brand Guidelines

### Official Naming

- **Use**: WWoW, Westworld of Warcraft
- **Avoid**: BloogBot (legacy name, being phased out)
- **Context**: Always refer to the project as "WWoW" in new code, comments, and documentation

### Branding in Code

```csharp
// ? Good
/// <summary>
/// WWoW pathfinding service for autonomous bot navigation.
/// </summary>

// ? Avoid
/// <summary>
/// BloogBot pathfinding service...
/// </summary>
```

## Code of Conduct

- Be respectful and inclusive in all interactions
- This project is for **educational and research purposes only**
- Only use on private servers you own or have permission to use
- Do not use on official Blizzard servers or for malicious purposes

## Getting Started

### Prerequisites

- **Visual Studio 2022** (v17.0 or later) with:
  - .NET Desktop Development workload
  - C++ Desktop Development workload (for native components)
- **.NET 8.0 SDK**
- **.NET Aspire workload**: `dotnet workload install aspire`
- **Windows 10/11** (native components require Windows)

### Building the Solution

1. Clone the repository:
   ```bash
   git clone https://github.com/QiMata/BloogBot.git
   cd BloogBot
   ```

2. Open `BloogBot.sln` in Visual Studio 2022

3. Build the solution:
   ```bash
   dotnet build
   ```

4. For native C++ projects, build separately:
   - Right-click `Loader` project ? Build
   - Right-click `FastCall` project ? Build
   - Right-click `Navigation` project ? Build

## Development Setup

### Running Services Locally

Use .NET Aspire for local orchestration:

```bash
dotnet run --project UI/WWoW.Systems/WWoW.Systems.AppHost
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test Tests/PathfindingService.Tests
```

## Project Structure

```
WWoW/
??? .github/                    # GitHub configuration
?   ??? copilot-instructions.md # Copilot context
?   ??? CONTRIBUTING.md         # This file
??? Exports/                    # Core libraries
?   ??? BotRunner/              # Behavior tree framework
?   ??? BotCommLayer/           # Protobuf IPC
?   ??? GameData.Core/          # Shared interfaces
?   ??? WoWSharpClient/         # WoW protocol client
?   ??? WinImports/             # P/Invoke wrappers
?   ??? Loader/                 # C++ CLR bootstrapper
?   ??? FastCall/               # C++ calling convention helper
?   ??? Navigation/             # C++ pathfinding/physics
??? Services/                   # Background worker services
?   ??? PathfindingService/     # A* navigation
?   ??? StateManager/           # FSM coordination
?   ??? DecisionEngineService/  # ML-based decisions
?   ??? PromptHandlingService/  # AI prompt handling
?   ??? BackgroundBotRunner/    # Headless bot execution
?   ??? ForegroundBotRunner/    # In-process bot execution
??? UI/                         # User interfaces
?   ??? StateManagerUI/         # WPF monitoring app
?   ??? WWoW.Systems/           # Aspire orchestration
??? Tests/                      # Unit/integration tests
??? BloogBot.AI/                # Semantic Kernel integration
??? ARCHITECTURE.md             # System architecture docs
```

## Coding Standards

### C# Guidelines

#### Naming

| Element | Convention | Example |
|---------|------------|---------|
| Interfaces | `I` prefix | `IWoWUnit`, `IObjectManager` |
| Classes | PascalCase | `PathfindingService` |
| Methods | PascalCase | `CalculatePathAsync` |
| Properties | PascalCase | `CurrentHealth` |
| Private fields | `_camelCase` | `_connectionString` |
| Parameters | camelCase | `targetPosition` |
| Constants | PascalCase | `MaxPathLength` |

#### Class Suffixes

| Suffix | Usage | Example |
|--------|-------|---------|
| `State` | FSM states | `CombatState`, `RestState` |
| `Service` | Background services | `PathfindingService` |
| `Client` | IPC/network clients | `PathfindingClient` |
| `Handler` | Event/packet handlers | `MovementHandler` |
| `Worker` | BackgroundService implementations | `DecisionEngineWorker` |

#### Code Style

```csharp
// Use file-scoped namespaces
namespace WWoW.Services.PathfindingService;

// Use primary constructors for DI
public class PathfindingWorker(
    ILogger<PathfindingWorker> logger,
    IOptions<PathfindingSettings> settings) : BackgroundService
{
    // Async methods end with "Async"
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessRequestsAsync(stoppingToken);
        }
    }
}

// Use records for DTOs with init-only properties
public record PathRequest
{
    public required uint MapId { get; init; }
    public required Position Start { get; init; }
    public required Position End { get; init; }
}
```

### C++ Guidelines

For native components (`Loader`, `FastCall`, `Navigation`):

- Follow existing code style in each project
- Use C++20 features where appropriate
- Document all exported DLL functions
- Use `extern "C"` for exported functions
- Prefer `__stdcall` for functions called from C#

### Adding a New Service

1. Create a new project using Worker SDK:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk.Worker">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
     </PropertyGroup>
   </Project>
   ```

2. Reference required projects:
   ```xml
   <ProjectReference Include="..\..\Exports\GameData.Core\GameData.Core.csproj" />
   <ProjectReference Include="..\..\Exports\BotCommLayer\BotCommLayer.csproj" />
   ```

3. Implement `BackgroundService`:
   ```csharp
   public class MyServiceWorker : BackgroundService
   {
       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
           // Service logic
       }
   }
   ```

4. Register in Aspire AppHost:
   ```csharp
   var myService = builder.AddProject<Projects.MyService>("myservice");
   ```

5. Add comprehensive `README.md`

## Pull Request Process

### Before Submitting

1. **Run all tests**: `dotnet test`
2. **Build the solution**: `dotnet build`
3. **Update documentation** if adding new features
4. **Follow naming conventions** described above

### PR Requirements

1. **Title**: Use conventional commits format
   - `feat: Add new pathfinding algorithm`
   - `fix: Correct memory leak in ObjectManager`
   - `docs: Update StateManager README`

2. **Description**: Include:
   - Summary of changes
   - Related issues/tickets
   - Testing performed
   - Screenshots (for UI changes)

3. **Checklist**:
   - [ ] Code follows project style guidelines
   - [ ] Added/updated unit tests
   - [ ] Documentation updated
   - [ ] README.md added/updated for new projects
   - [ ] No new warnings introduced

### Review Process

1. PRs require at least one approval
2. All CI checks must pass
3. Address review feedback promptly
4. Squash commits before merging

## Documentation Requirements

### Every Project Must Have

1. **README.md** with:
   - Overview/purpose
   - Architecture diagram (ASCII art preferred)
   - Key components description
   - Dependencies table
   - Usage examples
   - Related documentation links

2. **XML Documentation** for public APIs:
   ```csharp
   /// <summary>
   /// Calculates an A* path between two positions.
   /// </summary>
   /// <param name="start">Starting position in world coordinates.</param>
   /// <param name="end">Target position in world coordinates.</param>
   /// <returns>List of waypoints forming the path.</returns>
   /// <exception cref="PathNotFoundException">Thrown when no valid path exists.</exception>
   public async Task<IReadOnlyList<Position>> CalculatePathAsync(Position start, Position end)
   ```

### Protobuf Changes

When modifying `.proto` files:

1. Edit files in `BotCommLayer/Models/ProtoDef/`
2. Regenerate C# code:
   ```powershell
   .\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat
   ```
3. Update consuming services
4. Document new message types in the BotCommLayer README

## Questions?

- Check existing documentation in `ARCHITECTURE.md`
- Review project-specific READMEs
- Open a GitHub issue for questions not covered here

---

*Thank you for contributing to WWoW!*
