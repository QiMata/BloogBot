---
name: debugging
description: Debugging workflow for BloogBot/WWoW codebase. Use when investigating bugs, errors, unexpected behavior, or service failures.
trigger: debug, investigate a bug, error, unexpected behavior, service failure, trace a request, bot not moving, crash, IPC issue, find root cause
---

# Debugging in BloogBot

## Goal

Localize and root-cause a bug, error, or service failure by identifying the
execution mode and tracing the request through the layered architecture to the
responsible component.

## Inputs

- The symptom and which execution mode it occurs in:
  - **ForegroundBotRunner (in-process)** — injected into WoW.exe via
    `Exports/Loader/dllmain.cpp`; direct memory r/w via
    `Services/ForegroundBotRunner/Mem/`; native calls via `Exports/FastCall/`
    (x86 fastcall).
  - **BackgroundBotRunner (headless protocol)** — pure C# protocol emulation in
    `Exports/WoWSharpClient/`; no client; packet-level debugging via
    `WoWSharpClient/Handlers/` and `Client/`.
- IPC surface: all services talk Protobuf/TCP via `Exports/BotCommLayer/`
  (PathfindingService 9002; WoWStateManager 9001 char-state, 9000 state-manager
  API — confirm current ports against the service `appsettings`).

## Preconditions

- Identify the execution mode first — the steps differ between FG and BG.
- Reproduce the symptom (or have a log/recording that captures it).

## Procedure

1. **Trace IPC issues**: check `BotCommLayer/ProtobufSocketServer.cs` for
   connection handling → the specific service's socket server/client → port
   availability and startup order.
2. **Trace a request through the layers**:
   1. Entry point: `ForegroundBotRunner` or `BackgroundBotRunner`.
   2. Into `BotRunner/BotRunnerService.cs` (core behavior tree).
   3. Game state: `GameData.Core` interfaces → concrete implementations.
   4. Movement → `PathfindingService` (9002) → `Navigation.dll`.
   5. State change → `WoWStateManager` (9001/9000) → FSM transitions.
   6. Protocol → `WoWSharpClient/OpCodeDispatcher.cs` → specific `Handlers/`.
3. **Debug physics** (complex C++): see `docs/physics/README.md`; key (large) files
   `PhysicsEngine.cpp`, `PhysicsCollideSlide.cpp`, `PhysicsMovement.cpp`,
   `PhysicsGroundSnap.cpp` — read in chunks / via Codex.

## Verification

- Reproduce the symptom, apply the fix, and confirm the symptom is gone.
- Add a regression test or guard where feasible (`.\scripts\test-fast.ps1`).
- Use repo-scoped process cleanup only — never blanket-kill dotnet/WoW.exe/Game.exe.

## Outputs

- A root-cause statement, the fix, and (where feasible) a regression guard.
- A `FailureReason`/crash-cluster entry if the bug warrants one.

## Failure modes and recovery

Common failure patterns and where to look:

| Symptom | Likely cause | Where to look |
|---|---|---|
| Bot not moving | Pathfinding failure / stuck state | `Services/PathfindingService/PathfindingServiceWorker.cs` → `Exports/Navigation/PathFinder.cpp` |
| Physics glitch (falling through world) | Collision response | `Exports/Navigation/PhysicsCollideSlide.cpp`, `PhysicsGroundSnap.cpp` |
| Wrong spell cast | Profile rotation logic | `BotProfiles/<ClassSpec>/` spell priority |
| State machine stuck | Missing FSM transition | `Services/WoWStateManager/StateManagerWorker.cs` |
| Connection timeout | Protocol/network | `Exports/WoWSharpClient/Client/`, `Networking/` |
| DLL injection crash | Loader / CLR bootstrap | `Exports/Loader/dllmain.cpp`, `simple_loader.cpp` |
| Game objects not detected | ObjectManager sync | `Exports/WoWSharpClient/WoWSharpObjectManager.cs` |
| Decision engine wrong choice | ML model / input data | `Services/DecisionEngineService/DecisionEngine.cs`, `MLModel.cs` |

- **Debugging the wrong mode** wastes effort — confirm FG vs BG first.
- **Reading huge physics/packet files inline** fills context — search first, read in
  chunks, or summarize via Codex.

## Related skills

- [[crash-cluster-triage]] — when the symptom is a reproducible WoW.exe crash.
- [[fg-bg-physics-parity]] — when FG and BG physics diverge.
- [[failure-reason-mapping]] — classify the failure you found.
- [[botrunner-task-implementation]] — when the bug is in a Task.
