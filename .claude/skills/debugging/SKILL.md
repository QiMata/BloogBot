---
name: debugging
description: Debugging workflow for BloogBot/WWoW codebase. Use when investigating bugs, errors, unexpected behavior, or service failures.
---

# Debugging in BloogBot

## Two Execution Modes

Identify which mode the bug occurs in — debugging steps differ:

### ForegroundBotRunner (In-Process)
- Bot is injected into WoW.exe via `Exports/Loader/dllmain.cpp`
- Direct memory read/write via `Services/ForegroundBotRunner/Mem/`
- Native function calls via `Exports/FastCall/` (x86 fastcall convention)
- Debug: Check DLL injection logs, memory offsets, Lua execution errors

### BackgroundBotRunner (Headless Protocol)
- Pure C# protocol emulation in `Exports/WoWSharpClient/`
- No game client needed — packet-level debugging
- Debug: Check packet handlers in `WoWSharpClient/Handlers/`, auth flow in `Client/`

## Service Communication (IPC)

All services communicate via Protobuf TCP sockets (`Exports/BotCommLayer/`):
- **PathfindingService** — port 5001
- **WoWStateManager** — ports 5002 (char state), 8088 (state manager API)

To trace IPC issues:
1. Check `BotCommLayer/ProtobufSocketServer.cs` for connection handling
2. Check the specific service's socket server/client classes
3. Verify port availability and service startup order

## Common Failure Patterns

| Symptom | Likely Cause | Where to Look |
|---------|-------------|---------------|
| Bot not moving | Pathfinding failure or stuck state | `Services/PathfindingService/PathfindingServiceWorker.cs` → `Exports/Navigation/PathFinder.cpp` |
| Physics glitch (falling through world) | Physics engine collision | `Exports/Navigation/PhysicsCollideSlide.cpp`, `PhysicsGroundSnap.cpp` |
| Wrong spell cast | Profile rotation logic | `BotProfiles/<ClassSpec>/` — check spell priority |
| State machine stuck | FSM transition missing | `Services/WoWStateManager/StateManagerWorker.cs` |
| Connection timeout | Protocol or network issue | `Exports/WoWSharpClient/Client/`, `Networking/` |
| DLL injection crash | Loader or CLR bootstrap | `Exports/Loader/dllmain.cpp`, `simple_loader.cpp` |
| Game objects not detected | ObjectManager sync | `Exports/WoWSharpClient/WoWSharpObjectManager.cs` |
| Decision engine wrong choice | ML model or input data | `Services/DecisionEngineService/DecisionEngine.cs`, `MLModel.cs` |

## Tracing a Request Through Service Layers

1. Start at the entry point: `ForegroundBotRunner` or `BackgroundBotRunner`
2. Follow into `BotRunner/BotRunnerService.cs` (core behavior tree)
3. Check game state: `GameData.Core` interfaces → concrete implementations
4. If movement: trace to `PathfindingService` (port 5001) → `Navigation.dll`
5. If state change: trace to `WoWStateManager` (port 5002/8088) → FSM transitions
6. If protocol: trace through `WoWSharpClient/OpCodeDispatcher.cs` → specific `Handlers/`

## Debugging Physics Issues

The physics engine is complex C++ code. See `docs/physics/README.md` for detailed documentation.
Key files (all large — read in chunks):
- `PhysicsEngine.cpp` — Main simulation loop
- `PhysicsCollideSlide.cpp` — Collision response
- `PhysicsMovement.cpp` — Character movement
- `PhysicsGroundSnap.cpp` — Ground detection
