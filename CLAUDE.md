# BloogBot (Westworld of Warcraft) - Claude Code Instructions

## Architecture Overview

Layered microservices with a shared core library layer. Two execution modes: **ForegroundBotRunner** (DLL injection into WoW.exe, direct memory access) and **BackgroundBotRunner** (headless pure-C# protocol emulation, no game client needed).

**Dependency flow (strict top-to-bottom):**
```
GameData.Core (interfaces, zero dependencies)
  → BotCommLayer (Protobuf IPC)
    → BotRunner (core orchestration)
      → WoWSharpClient (WoW protocol)
        → Services (worker services)
          → UI (WPF, .NET Aspire)
```

**Supported WoW versions:** 1.12.1 (Vanilla), 2.4.3 (TBC), 3.3.5a (WotLK)

## Build & Test Commands

**Always kill WoW.exe before building.** The ForegroundBotRunner injects DLLs into WoW.exe from the build output directory. A running WoW.exe locks those DLLs and causes MSB3027 copy errors. Find and kill WoW.exe PIDs first:
```bash
tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST
taskkill //F //PID <pid>
```

```bash
# .NET build (primary)
dotnet build WestworldOfWarcraft.sln

# .NET tests (MSTest + Moq, 11 test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release

# C++ native components via MSBuild (VS 2025 Community)
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal
"$MSBUILD" Exports/Loader/Loader.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
"$MSBUILD" Exports/FastCall/FastCall.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
```

## Code Search Guide

| Looking for... | Start here |
|---|---|
| Game interfaces (IObjectManager, IWoWUnit, etc.) | `Exports/GameData.Core/` |
| Core bot orchestration & behavior trees | `Exports/BotRunner/` |
| WoW protocol (packets, opcodes, auth) | `Exports/WoWSharpClient/` |
| IPC / Protobuf socket communication | `Exports/BotCommLayer/` |
| Pathfinding & physics (C++) | `Exports/Navigation/` |
| DLL injection / CLR bootstrapping (C++) | `Exports/Loader/` |
| Windows API / process management | `Exports/WinImports/` |
| In-process bot (memory read/write, Lua) | `Services/ForegroundBotRunner/` |
| Headless bot (protocol emulation) | `Services/BackgroundBotRunner/` |
| A* pathfinding service (port 5001) | `Services/PathfindingService/` |
| Bot state machine / FSM (ports 5002, 8088) | `Services/WoWStateManager/` |
| ML-based decision engine | `Services/DecisionEngineService/` |
| Dialog/prompt automation | `Services/PromptHandlingService/` |
| Class/spec combat profiles (18+ classes) | `BotProfiles/` |
| WPF desktop UI | `UI/WoWStateManagerUI/` |
| .NET Aspire orchestration | `UI/Systems/Systems.AppHost/` |
| Tests (11 projects) | `Tests/` |

## Finding Things by Symptom

| Symptom | Where to look |
|---|---|
| Bot not moving / pathing failure | `Services/PathfindingService/` → `Exports/Navigation/PathFinder.cpp` |
| Physics glitch (falling, clipping) | `Exports/Navigation/PhysicsEngine.cpp`, `PhysicsCollideSlide.cpp` |
| Wrong spell / combat rotation | `BotProfiles/<ClassSpec>/` combat rotation files |
| Connection / login failure | `Exports/WoWSharpClient/Client/`, `Networking/` |
| State machine stuck | `Services/WoWStateManager/StateManagerWorker.cs` |
| IPC / service communication issue | `Exports/BotCommLayer/ProtobufSocketServer.cs` |
| DLL injection failure | `Exports/Loader/dllmain.cpp`, `simple_loader.cpp` |
| Bot not detecting game objects | `Exports/GameData.Core/IObjectManager.cs` → implementation in `Services/ForegroundBotRunner/Objects/` |
| Decision engine wrong choice | `Services/DecisionEngineService/DecisionEngine.cs`, `MLModel.cs` |

## Subagent Workflow Guidance

For complex investigations spanning multiple services:
1. **First phase**: Use a subagent to explore and map the relevant code paths across the service layers
2. **Second phase**: Main agent implements changes based on the map
3. **Third phase**: Use a subagent to review changes for unintended side effects across the dependency chain

This is important because the layered architecture means changes in Exports/ can affect multiple Services/.

## Process Safety — CRITICAL

**NEVER blanket-kill dotnet or Game processes.** Multiple Claude Code instances may be running concurrently across repos on this machine.

**Rules:**
1. **NEVER** run `taskkill /F /IM dotnet.exe`, `Stop-Process -Name dotnet`, `pkill dotnet`, or any variant that kills ALL dotnet processes
2. **NEVER** run `taskkill /F /IM Game.exe` — D2Bot may have 7+ Game.exe instances running for integration tests
3. Only kill specific PIDs that YOUR session launched
4. Use `TaskStop` tool for background tasks you started
5. If cleanup is needed, identify the specific PID from your output first

**Why:** The D2Bot repo runs 7-bot integration tests (7 Game.exe + dotnet test host). Blanket process killing destroys those runs and leaves orphan processes.

## MaNGOS Data Access — SOAP over MySQL

**NEVER edit the MaNGOS MySQL database directly.** All character/server operations MUST use the SOAP API (port 7878).

- **SOAP endpoint:** `http://127.0.0.1:7878/` with `ADMINISTRATOR:PASSWORD`
- **Test helpers:** `LiveBotFixture.ExecuteGMCommandAsync()` (SOAP) or `SendGmChatCommandAsync()` (bot chat)
- **Read-only MySQL is acceptable** for connectivity checks (`MangosServerFixture`) and non-mutating queries (e.g., starter item lists from `mangos.playercreateinfo_item`)
- **Never use** `DirectLearnSpellAsync` or any method that INSERTs/UPDATEs MaNGOS tables directly
- **`.reset` subcommands (ALL work):** `.reset honor`, `.reset level`, `.reset spells`, `.reset stats`, `.reset talents`, `.reset items`, `.reset all`. Use `.reset items` to strip all gear/inventory in test setup.
- **Exception:** `EnsureGmCommandsEnabledAsync()` bootstraps ADMINISTRATOR GM level via MySQL because SOAP requires GM access to function. This is the only acceptable MySQL write.

### GM Command Behavior (Online vs Offline)

- **Offline characters:** SOAP GM commands write directly to the DB. Changes take effect on next login.
- **Online characters:** GM commands affect in-memory server state immediately (client sees it), but the DB is NOT updated until the server does a periodic save or the character logs out.
- **Stale DB reads:** If a character has a running client, MySQL reads return stale data. Never trust DB reads for online character state — use snapshots via StateManager instead.

### Teleport Commands

- `.tele name <charName> <locationName>` — teleport to a named location (use `.lookup tele <keyword>` to find names)
- `.teleport name <charName> <mapId> <x> <y> <z>` — coordinate-based teleport (requires command table entry)
- `.go xyz <x> <y> <z> <mapId>` — self-teleport (bot chat only, via `SendGmChatCommandAsync`)

## File Reading Guidelines

When working with data files (JSON, CSV, logs, etc.):
- Check file sizes first using `wc -l` or `ls -lh` before reading
- For files over 200 lines, read in chunks, process/summarize, then continue
- Use `head`, `tail`, or `sed -n 'start,end p'` to read specific sections
- Never read large files in their entirety to avoid filling context

**Known large files:**
- `Exports/Navigation/PhysicsEngine.cpp` — C++ physics, read in chunks
- `Exports/WoWSharpClient/` packet handlers — many files, search with grep first
- `BotProfiles/` — 30 profile directories, grep to find the right one

---

## Task Management Protocol

### TASKS.md Maintenance (MANDATORY)

When working on any phase or task from `docs/TASKS.md`:

1. **Before starting work**: Read `docs/TASKS.md` to understand current priorities and dependencies
2. **Break down tasks**: If a task has sub-steps that span multiple files or require investigation, create numbered sub-tasks under it in TASKS.md
3. **Update on completion**: When a task/sub-task is finished, move it to `docs/ARCHIVE.md` and renumber remaining tasks so there are no gaps
4. **Keep TASKS.md lean**: Only open/in-progress items belong in TASKS.md. Completed items go to ARCHIVE.md
5. **Renumber after archiving**: After moving completed items to ARCHIVE.md, renumber remaining items within each phase so priorities are sequential (1.1, 1.2, 1.3... not 1.1, 1.4, 1.7)

### Task Creation Rules

- Any new work discovered during implementation MUST be added to TASKS.md under the appropriate phase
- If a failure reveals a new issue, create a task for it before moving on
- Flaky test setup should be evaluated before creating a code-fix task — distinguish infrastructure issues from real bugs

## Session Handoff Protocol

Before completing ANY session, update the active task files directly:

1. `docs/TASKS.md` (master handoff section)
2. Each impacted directory-local `TASKS.md`
3. Relevant `TASKS_ARCHIVE.md` files for completed items

### Required Handoff Fields

Every session handoff update must include:
1. What was completed
2. Exact commands run and outcomes
3. Snapshot/log/command-response evidence when applicable
4. Files changed
5. The very next command/task to run

### Completion

When all tracked work is complete, mark completion in `docs/TASKS.md` handoff section rather than creating separate prompt files.

### Important

- Do not create or rely on a separate next-session prompt file.
- Keep handoff entries self-contained and precise so any model can continue immediately.
- If a task was partially completed, describe what's done and what remains
- Reference `docs/TASKS.md` for the overall task list

## AI Documentation Rules

- Before asking for clarification, search existing docs
- Update docs if: a new command is added, a new behavior class is introduced, a dependency is added, or schema migrations are created
- Never overwrite existing docs without reading them first
- Use the toolbox/codebase search before guessing at schemas or patterns

## Key References

| Document | Purpose |
|----------|---------|
| `docs/TASKS.md` | Active/open task list |
| `docs/ARCHIVE.md` | Completed task history |
| `docs/ARCHITECTURE.md` | System architecture |
| `docs/TECHNICAL_NOTES.md` | Constants, env paths, known issues |
| `docs/DEVELOPMENT_GUIDE.md` | Developer onboarding |
| `docs/IPC_COMMUNICATION.md` | Socket/Protobuf IPC details |
| `docs/physics/README.md` | Physics engine documentation index |
| `docs/server-protocol/` | WoW 1.12.1 protocol reference (7 docs) |
