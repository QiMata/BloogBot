# WWoW (Westworld of Warcraft) — Copilot Instructions

## Quick Reference

- **Solution:** `WestworldOfWarcraft.sln` (.NET 8.0 + C++20)
- **Modes:** ForegroundBotRunner (DLL injection into WoW.exe) and BackgroundBotRunner (headless protocol emulation)
- **WoW versions:** 1.12.1, 2.4.3, 3.3.5a

## Build Commands

```bash
# Kill WoW.exe FIRST (locks DLLs) — kill specific PIDs only, NEVER blanket-kill dotnet
tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST
taskkill //F //PID <pid>

# .NET build
dotnet build WestworldOfWarcraft.sln

# .NET tests (11 test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release

# C++ native (VS 2025, PlatformToolset v145)
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal
"$MSBUILD" Exports/Loader/Loader.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
"$MSBUILD" Exports/FastCall/FastCall.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
```

## Fast Test Commands

```bash
# Physics calibration (fastest focused suite)
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# LiveValidation (requires live MaNGOS server)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation"

# Single test project
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release
```

## Code Search Guide

| Area | Location |
|------|----------|
| Game interfaces | `Exports/GameData.Core/` |
| Bot orchestration | `Exports/BotRunner/` |
| WoW protocol | `Exports/WoWSharpClient/` |
| Protobuf IPC | `Exports/BotCommLayer/` |
| Pathfinding & physics (C++) | `Exports/Navigation/` |
| DLL injection (C++) | `Exports/Loader/` |
| Headless bot | `Services/BackgroundBotRunner/` |
| In-process bot | `Services/ForegroundBotRunner/` |
| Pathfinding service | `Services/PathfindingService/` |
| State machine | `Services/WoWStateManager/` |
| Combat profiles | `BotProfiles/` |
| Task list | `docs/TASKS.md` |

## Dependency Flow (top-to-bottom, strict)

```
GameData.Core → BotCommLayer → BotRunner → WoWSharpClient → Services → UI
```

## Constraints

- **NEVER blanket-kill** `dotnet` or `Game.exe` processes (other repos may be running)
- **NEVER write to MaNGOS MySQL** — use SOAP API (`http://127.0.0.1:7878/`) for all mutations
- **NEVER use** `DirectLearnSpellAsync` — it bypasses server validation
- **Protobuf:** When changing `.proto` files in `Exports/BotCommLayer/Models/ProtoDef/`, regenerate with `protocsharp.bat`. Never edit generated `Communication.cs`/`Game.cs`/`Pathfinding.cs` directly.
- **Test cleanup:** Every test must clean up inventory (`".reset items"` via SOAP). Never commit test state.

## MaNGOS Server

The MaNGOS server is **always running**. SOAP at `http://127.0.0.1:7878/` with `ADMINISTRATOR:PASSWORD`.

## Key Documents

| Document | Purpose |
|----------|---------|
| `CLAUDE.md` | Full AI agent instructions (authoritative) |
| `docs/TASKS.md` | Active task list |
| `docs/ARCHIVE.md` | Completed tasks |
| `docs/ARCHITECTURE.md` | System architecture |
| `docs/TECHNICAL_NOTES.md` | Constants, env, known issues |
