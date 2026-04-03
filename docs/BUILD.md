# BloogBot Build System

This document describes the new unified build system for BloogBot that provides AI-compatible build automation and CI/CD capabilities.

## Overview

The build system includes:
- **CMake** for unified C++/.NET builds
- **GitHub Actions** for CI/CD pipeline
- **PowerShell scripts** for local development
- **Docker support** for containerized builds
- **Automated testing** and artifact generation

## Quick Start

### Prerequisites

1. **Visual Studio 2022** (Community or higher) with:
   - C++ development workload
   - Windows SDK 10.0.19041 or later
   - CMake tools
2. **.NET 8 SDK**
3. **Git** for version control
4. **Protocol Buffers Compiler (protoc)** - for regenerating IPC message classes (optional, only needed when modifying `.proto` files)

### Local Development Build

```powershell
# Build Debug configuration (default)
.\scripts\build.ps1

# Build Release configuration
.\scripts\build.ps1 -Configuration Release

# Build with tests
.\scripts\build.ps1 -Test

# Build native projects only
.\scripts\build.ps1 -NativeOnly

# Clean and rebuild
.\scripts\build.ps1 -Clean -Configuration Release
```

### Using CMake Directly

```bash
# Configure
cmake -S . -B build -A Win32

# Build
cmake --build build --config Debug

# Test
ctest --build-config Debug --output-on-failure
```

### Using CMake Presets (VS Code/Visual Studio)

```bash
# List available presets
cmake --list-presets

# Configure with preset
cmake --preset windows-x86-debug

# Build with preset
cmake --build --preset windows-x86-debug-build
```

## CI/CD Pipeline

The GitHub Actions workflow automatically:

1. **Builds** multiple configurations (Debug/Release, Win32/x64)
2. **Tests** all components with coverage reporting
3. **Analyzes** code quality with CodeQL
4. **Scans** for security vulnerabilities
5. **Packages** releases automatically
6. **Deploys** to staging environments

### Workflow Triggers

- **Push** to `main`, `develop`, or `copilotmaxxing` branches
- **Pull Requests** to `main` or `develop`
- **Release** creation

### Artifacts

Each build produces:
- Compiled binaries
- Debug symbols (PDB files)
- Test results
- Code coverage reports
- Security scan results

## Directory Structure

```
BloogBot/
├── CMakeLists.txt              # Root CMake configuration
├── CMakePresets.json           # CMake presets for IDEs
├── GitVersion.yml              # Semantic versioning config
├── Dockerfile                  # Build container definition
├── scripts/
│   ├── build.ps1              # Local build script
│   └── ci-build.ps1           # CI-optimized build script
├── .github/workflows/
│   └── ci-cd.yml              # GitHub Actions pipeline
├── Exports/                   # C++ native projects
│   ├── Loader/
│   │   └── CMakeLists.txt
│   ├── FastCall/
│   │   └── CMakeLists.txt
│   └── Navigation/
│       └── CMakeLists.txt
└── Build/                     # Build output directory
    ├── Win32/
    │   ├── Debug/
    │   └── Release/
    └── x64/
        ├── Debug/
        └── Release/
```

## AI Tooling Support

### Code Intelligence

- **`compile_commands.json`** generated for C++ intellisense
- **Symbol indexing** for cross-references
- **AST generation** for code analysis

### Debug Information

- **PDB files** automatically generated and copied
- **Source maps** for debugging injected code
- **Crash dumps** supported for post-mortem analysis

### Documentation Generation

- **API documentation** auto-generated from source
- **Build logs** with structured output
- **Test reports** in multiple formats

## Configuration Options

### CMake Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BUILD_TESTS` | `ON` | Build test projects |
| `BUILD_NATIVE_ONLY` | `OFF` | Skip .NET builds |
| `CMAKE_BUILD_TYPE` | `Debug` | Build configuration |

### Environment Variables

#### Core / Injection

| Variable | Default | Description |
|----------|---------|-------------|
| `FOREGROUNDBOT_DLL_PATH` | *(set by StateManager)* | Absolute path to ForegroundBotRunner.dll — set automatically during injection |
| `WWOW_ACCOUNT_NAME` | *(set by StateManager)* | Account name for the bot to log in with |
| `WWOW_ACCOUNT_PASSWORD` | *(set by StateManager)* | Account password for the bot |
| `WWOW_CHARACTER_CLASS` | *(optional)* | Override character class for auto-creation (e.g. `Warrior`) |
| `WWOW_CHARACTER_RACE` | *(optional)* | Override character race (e.g. `Orc`) |
| `WWOW_CHARACTER_GENDER` | *(optional)* | Override character gender (e.g. `Male`) |
| `WWOW_WAIT_DEBUG` | *(unset)* | Set to `1` to pause on startup and wait for debugger attachment |
| `WWOW_INJECT_LOG_DIR` | `<BaseDirectory>/WWoWLogs` | Override directory for injection diagnostic logs |
| `WWOW_ENABLE_RECORDING_ARTIFACTS` | *(unset)* | Set to `1` to allow packet/snapshot recording artifacts and FG file-backed recording diagnostics; default keeps those files disabled |
| `WWOW_DISABLE_PACKET_HOOKS` | *(unset)* | Set to `1` to skip SignalEventManager + PacketLogger hook installation. **Required** to prevent FG crash during cross-map transfers (dungeon teleports). Also configurable via `appsettings.json` `Injection:DisablePacketHooks` |
| `WWOW_LOGIN_STATE_MONITOR` | *(unset)* | Set to `1` to enable login state monitor polling |
| `LOADER_ALLOC_CONSOLE` | *(unset)* | Set to `1` to allocate a console window for Loader.dll diagnostics. Also configurable via `appsettings.json` `Injection:AllocateConsole` |
| `LOADER_PAUSE_ON_EXCEPTION` | *(unset)* | Set to `1` to pause on exception during loader bootstrap (for debugging) |

#### Services

| Variable | Default | Description |
|----------|---------|-------------|
| `WWOW_DATA_DIR` | *(auto-detected)* | Root directory containing `maps/`, `vmaps/`, `mmaps/`, `dbc/` data files for pathfinding |
| `WWOW_RECORDINGS_DIR` | `<Documents>/BloogBot/MovementRecordings` | Override directory for movement recordings |
| `WWOW_LOADER_DLL_PATH` | *(auto-detected)* | Override path to Loader.dll for FG injection |
| `WWOW_SETTINGS_OVERRIDE` | *(unset)* | Path to override StateManagerSettings.json |
| `WWOW_SHOW_WINDOWS` | *(unset)* | Set to `1` to show child process windows (StateManager, PathfindingService, etc.) |
| `WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION` | *(unset)* | Set to `1` to enable native path segment validation in PathfindingService |

#### Testing

| Variable | Default | Description |
|----------|---------|-------------|
| `WWOW_TEST_DISABLE_COORDINATOR` | *(unset)* | Set to `1` to disable CombatCoordinator during tests |
| `WWOW_TEST_COORD_SUPPRESS_SECONDS` | *(unset)* | Seconds to suppress coordinator after enabling |
| `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK` | *(unset)* | Set to `1` to disable auto-release corpse task during tests |
| `WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK` | *(unset)* | Set to `1` to disable auto-retrieve corpse task during tests |
| `WWOW_TEST_WOW_PATH` | `E:\Elysium Project Game Client\WoW.exe` | Override WoW.exe path for tests |
| `WWOW_TEST_LOADER_PATH` | *(auto-detected)* | Override Loader.dll path for tests |
| `WWOW_TEST_AUTH_IP` | `127.0.0.1` | Auth server IP for tests |
| `WWOW_TEST_AUTH_PORT` | `3724` | Auth server port for tests |
| `WWOW_TEST_WORLD_PORT` | `8085` | World server port for tests |
| `WWOW_TEST_PATHFINDING_IP` | `127.0.0.1` | Pathfinding service IP for tests |
| `WWOW_TEST_PATHFINDING_PORT` | `5001` | Pathfinding service port for tests |
| `WWOW_TEST_MYSQL_PORT` | `3306` | MySQL port for tests |
| `WWOW_TEST_MYSQL_USER` | `root` | MySQL user for tests |
| `WWOW_TEST_MYSQL_PASSWORD` | `root` | MySQL password for tests |
| `WWOW_TEST_SOAP_PORT` | `7878` | SOAP API port for tests |
| `WWOW_TEST_USERNAME` | `TESTBOT1` | Test account username |
| `WWOW_TEST_PASSWORD` | `PASSWORD` | Test account password |
| `WWOW_TEST_RESTORE_COMMAND_TABLE` | *(unset)* | Restore command table after test modifications |
| `WWOW_BOT_OUTPUT_DIR` | *(auto-detected)* | Override bot output directory for test fixtures |
| `WWOW_MAP_PATH` | *(unset)* | Override map data path for physics tests |
| `WWOW_RECORDED_TEST_HOST` | *(unset)* | Host for recorded scenario tests |
| `WWOW_RECORDED_TEST_PORT` | *(unset)* | Port for recorded scenario tests |
| `WWOW_RECORDED_TEST_REALM` | *(unset)* | Realm for recorded scenario tests |
| `WWOW_WOW_WINDOW_TITLE` | *(unset)* | WoW window title for FG recorded tests |
| `WWOW_WOW_PROCESS_ID` | *(unset)* | WoW process ID for FG recorded tests |
| `WWOW_WOW_WINDOW_HANDLE` | *(unset)* | WoW window handle for FG recorded tests |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | *(unset)* | Disable .NET CLI telemetry |

#### appsettings.json Configuration (StateManager)

These `appsettings.json` keys in `Services/WoWStateManager/appsettings.json` map to environment variable behavior:

| Key | Default | Description |
|-----|---------|-------------|
| `Injection:DisablePacketHooks` | `"true"` | Sets `WWOW_DISABLE_PACKET_HOOKS=1` for FG bot process |
| `Injection:AllocateConsole` | `"true"` | Sets `LOADER_ALLOC_CONSOLE=1` for FG bot process |
| `GameClient:ExecutablePath` | `D:\World of Warcraft\WoW.exe` | Path to WoW.exe for FG injection |
| `LoaderDllPath` | `Loader.dll` | Path to native Loader.dll |

## Troubleshooting

### Common Issues

1. **CMake not found**: Install CMake or add to PATH
2. **MSBuild errors**: Ensure Visual Studio 2022 is installed
3. **Missing dependencies**: Run `dotnet restore`
4. **Platform mismatches**: Check target platform settings

### Debug Builds

For injection debugging:
1. Build in Debug configuration
2. Set `WWOW_WAIT_DEBUG=1`
3. Attach debugger to WoW.exe process
4. PDB files are automatically copied to solution root

### Build Logs

Check these locations for detailed logs:
- `build/CMakeOutput.log` - CMake configuration log
- `Build/*/Debug/` - Build output directory
- GitHub Actions logs for CI builds

## Protocol Buffers

The project uses Protocol Buffers for inter-process communication. The `.proto` files are located in `Exports/BotCommLayer/Models/ProtoDef/`.

### Installing protoc

If you need to modify `.proto` files, install the Protocol Buffers compiler:

```powershell
# Option 1: Run the installation script (downloads to C:\protoc)
.\install-protoc.ps1

# Option 2: Manual download
# Download from: https://github.com/protocolbuffers/protobuf/releases
# Extract to C:\protoc (or any location)
# The bin\protoc.exe should be accessible
```

### Regenerating C# Classes

After modifying any `.proto` file:

```powershell
# Using the batch script (recommended)
.\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. "C:\protoc\bin\protoc.exe"

# Or manually from the ProtoDef directory
cd Exports\BotCommLayer\Models\ProtoDef
C:\protoc\bin\protoc.exe --csharp_out=".." --proto_path="." communication.proto database.proto game.proto pathfinding.proto
```

### Proto Files

| File | Purpose |
|------|---------|
| `communication.proto` | ActivitySnapshot, state changes, action messages |
| `game.proto` | WoW game objects (WoWPlayer, WoWUnit, WoWItem, etc.) |
| `pathfinding.proto` | Navigation requests and responses |
| `database.proto` | Database schema definitions |

## Contributing

When adding new components:

1. **Update CMakeLists.txt** for new C++ projects
2. **Add to .csproj** for new .NET projects  
3. **Update build scripts** if new dependencies added
4. **Test both local and CI builds** before submitting PR

## Versioning

Uses GitVersion for semantic versioning:
- **Main branch**: Production releases
- **Develop branch**: Alpha pre-releases  
- **Feature branches**: Feature pre-releases
- **Release branches**: Beta pre-releases

## Container Builds

Build in Docker for reproducible environments:

```bash
# Build container
docker build -t bloogbot-build .

# Run build
docker run --rm -v "${PWD}:C:/src" bloogbot-build
```

This ensures consistent builds across different development machines and CI environments.
