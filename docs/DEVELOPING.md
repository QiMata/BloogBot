# Reference for Developing and Debugging

## Quick Start

### Building the Project

The preferred way to build BloogBot is using the provided PowerShell build script:

```powershell
# Debug build (default)
.\scripts\build.ps1

# Release build
.\scripts\build.ps1 -Configuration Release

# Build with tests
.\scripts\build.ps1 -Test

# Clean and rebuild
.\scripts\build.ps1 -Clean
```

### Using CMake Directly

```bash
# Configure
cmake -S . -B build -A Win32

# Build
cmake --build build --config Debug

# Build specific target
cmake --build build --target Loader --config Debug
```

### Using Visual Studio/VS Code

The project includes CMake presets for IDE integration:
- Open the folder in Visual Studio 2022 or VS Code
- Select a CMake preset (e.g., "Windows x86 Debug")
- Build using the IDE's CMake integration

## Build Configurations for Each Project

### Services
| Project | Configuration | Platform | Build Method |
|---------|---------------|----------|--------------|
| ForegroundBotRunner | Debug | x86 | dotnet build |
| WoWStateManager | Debug | x86 | dotnet build |
| PathfindingService | Debug | x86 | dotnet build |
| BackgroundBotRunner | Debug | Any CPU | dotnet build |
| DecisionEngineService | Debug | Any CPU | dotnet build |
| PromptHandlingService | Debug | Any CPU | dotnet build |
| LoggingMCPServer | Debug | Any CPU | dotnet build |
| CppCodeIntelligenceMCP | Debug | Any CPU | dotnet build |

### Libraries (Exports)
| Project | Configuration | Platform | Build Method |
|---------|---------------|----------|--------------|
| BotCommLayer | Debug | Any CPU | dotnet build |
| BotRunner | Debug | Any CPU | dotnet build |
| GameData.Core | Debug | Any CPU | dotnet build |
| WinProcessImports | Debug | Any CPU | dotnet build |
| WoWSharpClient | Debug | Any CPU | dotnet build |
| **FastCall** | **Debug** | **Win32** | **CMake/MSBuild** |
| **Loader** | **Release** | **Win32** | **CMake/MSBuild** |
| **Navigation** | **Release** | **Win32** | **CMake/MSBuild** |

### Tests
| Project | Configuration | Platform | Build Method | Notes |
|---------|---------------|----------|--------------|-------|
| Navigation.Physics.Tests | Debug | x86 | dotnet test | **Use --settings test.runsettings** |
| BotRunner.Tests | Debug | x86 | dotnet test | **Use --settings test.runsettings** |
| PathfindingService.Tests | Debug | Any CPU | dotnet test | |
| WoWSharpClient.Tests | Debug | Any CPU | dotnet test | |
| PromptHandlingService.Tests | Debug | Any CPU | dotnet test | |

### UI
| Project | Configuration | Platform | Build Method |
|---------|---------------|----------|--------------|
| WoWStateManagerUI | Debug | Any CPU | dotnet build |

**Note**: Bold entries are native C++ projects built through CMake. All others are .NET projects.

## Debug Setup

### For Injection Debugging (ForegroundBotRunner)
1. Build in Debug mode with x86 platform
2. Set environment variable: `WWOW_WAIT_DEBUG=1`
3. Copy PDB files are automatically copied to solution root for debugging
4. Attach Visual Studio debugger to WoW.exe process after injection
5. Use Managed (.NET Framework) debugging for net48 shim code

### Key Notes
- Native projects (FastCall, Loader, Navigation) use Win32 platform
- Injection-related projects (ForegroundBotRunner, PathfindingService, WoWStateManager) use x86 platform
- Physics tests require `--settings Tests/BotRunner.Tests/test.runsettings` for x86 platform target
- Loader and Navigation are built in Release configuration
- All other projects use Debug configuration with Any CPU platform
