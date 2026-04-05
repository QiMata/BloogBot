# Master Tasks

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker). Never defer live validation tests.
5. **WoW.exe binary parity is THE rule** for physics/movement. No heuristics without binary evidence.
6. Every fix must include or update a focused test.
7. After each shipped delta, commit and push before ending the pass.
8. **Navigation.dll x86 for BotRunner.Tests:** Copy from MSBuild x86 output or `Bot/Release/net8.0/x86/`.
9. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-05)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1417 | 0 | 1 | Green — 7 MC integration tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator |
| BotRunner.Tests (unit, non-LV) | 1623 | 0 | 4 | All green |
| BotRunner.Tests (LiveValidation) | 15 | 12 | 3 | 12 failures = infra/timing, 0 code bugs |

---

## Deferred — Physics.dll DLL Separation

These activate when Physics.dll ships as a separate DLL from Navigation.dll. Currently all physics + scene + pathfinding compile into one Navigation.dll. The CMake project for Physics.dll is created at `Exports/Physics/CMakeLists.txt`.

| # | Task | Status |
|---|------|--------|
| 0.7 | **Refactor Navigation.dll to path-only** — Remove physics/scene code, keep Detour navmesh + PathFinder + MoveMap. | Deferred |
| 0.8 | **Update C# P/Invoke DllName** — NativeLocalPhysics: "Navigation" → "Physics". One-line change per file. | Deferred |
| 0.10 | **Build Physics.dll x86+x64** — Run CMake from VS Developer Command Prompt. Verify all exports. | Deferred |

---

## Canonical Commands

```bash
# Kill WoW.exe before building (MANDATORY)
tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST
taskkill //F //PID <pid>

# Build
dotnet build WestworldOfWarcraft.sln --configuration Release

# Build x86+x64 Navigation.dll (from repo root)
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -p:OutDir="$(pwd)/Bot/Release/net8.0/x86/"

# Unit tests (fast, no server)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build

# WoWSharpClient tests
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure" --no-build

# Physics replay tests
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build

# LiveValidation (needs MaNGOS + StateManager)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Docker services
docker compose -f docker-compose.vmangos-linux.yml up -d pathfinding-service scene-data-service

# Check Navigation.dll exports
strings Bot/Release/net8.0/Navigation.dll | grep -E "^[A-Z][a-zA-Z]+$" | sort -u
```

---

## Blocked - Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in `RecordedTests.Shared` | Requires `AWSSDK.S3` |
| `RTS-MISS-002` | Azure ops in `RecordedTests.Shared` | Requires `Azure.Storage.Blobs` |
