# Physics DLL Split — Implementation Plan

## Goal
Move physics/collision from PathfindingService TCP to local in-process P/Invoke.
Each BG bot loads Navigation.dll directly, requests scene data on demand from a new
SceneDataService, and runs physics at full framerate with zero IPC latency.

## Current Architecture (broken for parity)
```
MovementController (C#)
  → PathfindingClient (protobuf + TCP, 5-20ms round-trip)
    → PathfindingSocketServer (deserialize + marshal)
      → Navigation.dll!PhysicsStepV2 (0.5ms)
    → Response (serialize + TCP)
  → ApplyPhysicsResult
Total: ~7-20ms per physics frame
```

## Target Architecture (binary parity)
```
MovementController (C#)
  → LocalPhysicsEngine (direct P/Invoke, <1ms)
    → Navigation.dll!PhysicsStepV2
  → ApplyPhysicsResult
Total: ~1ms per physics frame, same as WoW.exe

SceneDataService (TCP, async, on-demand)
  → Serves .scene grid data to bots
  → Bots request grids around their position
  → Grids cached locally, evicted when far away
```

## Shared Code Analysis
- Pathfinding: PathFinder.cpp + MoveMap.cpp + Detour (navmesh only, NO collision deps)
- Physics: PhysicsEngine.cpp + SceneQuery.cpp + SceneCache.cpp + VMAP (collision only, NO navmesh deps)
- They share ONLY: Navigation.h (export declarations) + the compiled DLL
- They can be split into two DLLs with zero code changes

## Implementation Steps (in execution order)

### Phase 1: Local Physics Client (no DLL split needed)
The current Navigation.dll already exports `PhysicsStepV2`. Each BG bot process can
load it directly via P/Invoke — the `NativePathfindingClient` in tests already does this.

**Step 1.1: Create `LocalPhysicsClient` in WoWSharpClient**
- File: `Exports/WoWSharpClient/Movement/LocalPhysicsClient.cs`
- P/Invoke wrapper: `[DllImport("Navigation.dll")] static extern PhysicsOutput PhysicsStepV2(ref PhysicsInput input)`
- Copy the P/Invoke struct definitions from `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
- Implement `IPhysicsClient` interface with `PhysicsStep(proto) → proto` conversion
- Handle DLL initialization: call `PreloadMap(mapId)` on first use

**Step 1.2: Create `IPhysicsClient` interface**
- File: `Exports/BotRunner/Clients/IPhysicsClient.cs`
- Methods: `PhysicsOutput PhysicsStep(PhysicsInput input)`
- Both `PathfindingClient` (TCP) and `LocalPhysicsClient` (P/Invoke) implement this
- `MovementController` takes `IPhysicsClient` instead of `PathfindingClient`

**Step 1.3: Wire `LocalPhysicsClient` into `BackgroundBotWorker`**
- `BackgroundBotWorker.InitializeInfrastructure()` creates `LocalPhysicsClient` instead of `PathfindingClient` for physics
- Keep `PathfindingClient` for navigation (GetPath, LineOfSight)
- Navigation.dll must be in the bot's working directory (copy from build output)

**Step 1.4: Scene data initialization**
- `LocalPhysicsClient.Initialize(mapId)` calls `PreloadMap(mapId)` via P/Invoke
- PreloadMap loads both navmesh AND scene data — for now, this is fine
- Later (Phase 3), scene data loading will be split out

**Step 1.5: Test — IPC parity tests with LocalPhysicsClient**
- Existing `MovementControllerIpcParityTests` already use `NativePathfindingClient` (direct P/Invoke)
- Verify all 5 tests still pass: 0 airborne frames at full framerate
- Add timing comparison: measure ms per physics frame (should be <1ms)

### Phase 2: Increase Physics Framerate
With local P/Invoke, the physics can run at higher framerate than 20 FPS.

**Step 2.1: Reduce PHYSICS_FIXED_DT from 50ms to 16ms**
- File: `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
- Change `PHYSICS_FIXED_DT = 0.016f` (60 FPS, matching WoW.exe)
- The sub-stepping accumulator handles this automatically
- More physics steps = smoother movement = less Z oscillation

**Step 2.2: Verify heartbeat timing**
- WoW.exe sends heartbeats at ~500ms intervals during movement
- Our `ShouldSendPacket()` already has 500ms heartbeat interval
- More physics steps between heartbeats = more accurate position in each heartbeat
- No change needed to heartbeat logic

### Phase 3: SceneDataService (deferred — only if memory is a concern)
Each BG bot process loading full scene data uses ~300-500MB per map. For 3000 bots,
this is 300GB+ total. If memory is a concern, split scene data into a shared service.

**Step 3.1: Create SceneDataService**
- New project: `Services/SceneDataService/`
- TCP server on port 5003
- API: `RequestGridData(mapId, cellX, cellY)` → returns serialized SceneCache grid
- Loads full .scene files and serves individual grid cells on demand

**Step 3.2: Create `RemoteSceneProvider` in Navigation.dll**
- Instead of SceneCache loading from disk, request from SceneDataService
- Only load grids within ~200y of the bot's position
- Evict grids that are >400y away
- Reduces per-bot memory from ~300MB to ~5-10MB

**Step 3.3: Split Navigation.dll into PhysicsEngine.dll + PathfindingEngine.dll**
- PhysicsEngine.dll: PhysicsEngine + SceneQuery + SceneCache + VMAP (collision)
- PathfindingEngine.dll: PathFinder + MoveMap + Detour (navmesh)
- PathfindingService uses PathfindingEngine.dll only
- BG bots use PhysicsEngine.dll locally + PathfindingClient for navigation

## File Changes Summary

### Phase 1 New Files:
- `Exports/WoWSharpClient/Movement/LocalPhysicsClient.cs` — P/Invoke wrapper
- `Exports/BotRunner/Clients/IPhysicsClient.cs` — interface

### Phase 1 Modified Files:
- `Exports/WoWSharpClient/Movement/MovementController.cs` — accept IPhysicsClient
- `Services/BackgroundBotRunner/BackgroundBotWorker.cs` — create LocalPhysicsClient
- `Exports/BotRunner/Clients/PathfindingClient.cs` — implement IPhysicsClient

### Phase 2 Modified Files:
- `Exports/WoWSharpClient/WoWSharpObjectManager.cs` — PHYSICS_FIXED_DT = 0.016f

### Phase 3 New Files:
- `Services/SceneDataService/` — new service project
- `Exports/Navigation/RemoteSceneProvider.cpp/.h` — grid-on-demand loading

## Risk Assessment
- Phase 1: LOW risk — NativePathfindingClient already proves this works
- Phase 2: LOW risk — just a constant change, sub-stepping handles it
- Phase 3: MEDIUM risk — new service + DLL split, but can be deferred

## Dependencies
- Navigation.dll must be deployed alongside BackgroundBotRunner.exe
- Scene data files (.scene, .vmap, .map) must be accessible from bot working dir
- PathfindingService still needed for navigation (GetPath, LineOfSight)
