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

```bash
# .NET build (primary)
dotnet build WestworldOfWarcraft.sln

# .NET tests (MSTest + Moq, 11 test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release

# C++ native components (Navigation, Loader, FastCall)
cmake -B build && cmake --build build

# Or via CMake dotnet targets
cmake --build build --target dotnet_build
cmake --build build --target dotnet_test
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

Before completing ANY session, Claude MUST create `docs/next-session-prompt.md`.

### Requirements

The file must contain a single fenced code block tagged as `prompt` with a complete, self-contained prompt for the next session:

````markdown
```prompt
<Your complete handoff prompt here>
```
````

### The prompt MUST include:

1. **What was accomplished** - Summary of completed work this session
2. **What to work on next** - Specific files, functions, and approach
3. **Blockers or failed attempts** - What didn't work and why
4. **Decisions made** - Design choices, trade-offs, rationale
5. **Current state of in-progress work** - Partial implementations, uncommitted changes, test status

### Completion

If all tasks in TASKS.md are complete, write `ALL_TASKS_COMPLETE` inside the code block instead:

````markdown
```prompt
ALL_TASKS_COMPLETE
```
````

### Important

- The handoff prompt must be **self-contained** - the next session starts fresh with no prior context
- Include exact file paths and line numbers for any in-progress work
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
