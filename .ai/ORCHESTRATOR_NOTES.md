# Claude Orchestrator Notes

Session-persistent context for Claude Code to stay consistent across sessions.

## Repo Identity

- **Name:** Westworld of Warcraft (WWoW) / BloogBot
- **Branch:** `cpp_physics_system` (active development)
- **Solution:** `WestworldOfWarcraft.sln`
- **Platform:** Windows 11, .NET 8.0, C++20 (VS 2025 v145)

## Canonical Build Commands

```bash
# .NET
dotnet build WestworldOfWarcraft.sln
dotnet test WestworldOfWarcraft.sln --configuration Release

# C++ (Navigation DLL — most frequently rebuilt)
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal
```

## Canonical Test Commands

```bash
# Physics calibration (fastest, ~50s)
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# Full physics + specific filter
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DriftGate_PerMode"

# LiveValidation (requires live MaNGOS, ~minutes)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation"

# Corpse-run tests
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m
```

## Current Calibration State (2026-03-01)

| Mode | Avg | P99 | Worst |
|------|-----|-----|-------|
| ground | 0.013y | 0.170y | 0.520y |
| air | 0.000y | 0.000y | 0.000y |
| swim | 0.000y | 0.000y | 0.003y |
| transition | 0.013y | 0.200y | 0.497y |
| transport | 0.027y | 0.308y | 0.329y |

97/97 physics tests pass, 1 skipped.

## Gotchas

- **Kill WoW.exe before building** — locked DLLs cause MSB3027 errors
- **NEVER blanket-kill dotnet/Game.exe** — other repos have processes running
- **MaNGOS is always live** — SOAP at `http://127.0.0.1:7878/` (ADMINISTRATOR:PASSWORD)
- **NEVER write to MaNGOS MySQL** — SOAP only for mutations
- **Shell uses bash** (not PowerShell) inside Claude Code despite Windows
- `dumpbin` warning during build is harmless (vcpkg applocal script)
- Physics tests need `test.runsettings` for correct DLL loading

## Key Files

| Purpose | Path |
|---------|------|
| Master instructions | `CLAUDE.md` |
| Task tracking | `docs/TASKS.md` |
| Physics engine (C++) | `Exports/Navigation/PhysicsEngine.cpp` |
| Replay calibration | `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs` |
| LiveValidation fixture | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` |
| Auto-memory | `~/.claude/projects/.../memory/MEMORY.md` |

## Open Work

- `PATH-REFACTOR-001`: Orgrimmar navmesh-vs-collision mismatch
- `LV-QUEST-001`: QuestInteractionTests
- Ground mode worst (0.520y): WMO floor geometry in Orgrimmar
- LiveValidation: 18/20 passing (FishingProfessionTests, ConsumableUsageTests remain)
