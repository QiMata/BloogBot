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

| Variable | Description |
|----------|-------------|
| `BLOOGBOT_WAIT_DEBUG` | Set to `1` to enable debugger attachment |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Disable .NET telemetry |

## Troubleshooting

### Common Issues

1. **CMake not found**: Install CMake or add to PATH
2. **MSBuild errors**: Ensure Visual Studio 2022 is installed
3. **Missing dependencies**: Run `dotnet restore`
4. **Platform mismatches**: Check target platform settings

### Debug Builds

For injection debugging:
1. Build in Debug configuration
2. Set `BLOOGBOT_WAIT_DEBUG=1`
3. Attach debugger to WoW.exe process
4. PDB files are automatically copied to solution root

### Build Logs

Check these locations for detailed logs:
- `build/CMakeOutput.log` - CMake configuration log
- `Build/*/Debug/` - Build output directory
- GitHub Actions logs for CI builds

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
