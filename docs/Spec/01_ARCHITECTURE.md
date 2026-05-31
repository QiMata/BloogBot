# Spec 01 — Architecture

## Component graph

```
+--------------------+        Activity request        +----------------------+
|   Human Operator   |  ----------------------------> |                      |
|  (WPF UI / chat)   |                                |                      |
+--------------------+                                |                      |
                                                      |                      |
+--------------------+        Loadout + assigned       |                      |
| Catalog (compiled) |  ----------------------------> |    StateManager      |
+--------------------+                                |  (port 8088 / 5002)  |
                                                      |                      |
+--------------------+        Snapshots                |                      |
|   FG/BG Bot N      |  ----------------------------> |  - mode handler      |
+--------------------+                                 |  - lease ledger     |
                                                      |  - activity sched.   |
+--------------------+        Path/scene queries       |  - coordinators     |
| PathfindingService |  <---------------------------- |  - metrics/log out   |
|   (port 5001)      |                                +-----+----------------+
+--------------------+                                      |
+--------------------+                                      | ObjectiveMessages
|  SceneDataService  |                                      v
|   (port 5003)      |   <--- bot local queries --- +----------------------+
+--------------------+                              |  BotRunner (FG/BG)    |
                                                    |   - behavior trees    |
+--------------------+                              |   - IBotTask stack    |
| DecisionEngineSvc  |  <-- snapshots, advice --->  |   - 30 NCC components |
+--------------------+                              +-----+----------------+
                                                          |
+--------------------+                                    | WoW protocol
| PromptHandlingSvc  |  <-- optional intent parse -->     v
+--------------------+                              +----------------------+
                                                    |   VMaNGOS server     |
                                                    |  (realmd + mangosd)  |
                                                    |   SOAP port 7878     |
                                                    +----------------------+
```

## Ownership boundaries

These are hard. Crossing them is an architecture bug.

| Owner | Owns | Does not own |
|---|---|---|
| **StateManager** | Bot lifecycle, mode dispatch, activity registry, lease ledger, coordinators, snapshot cache, metrics rollup, config persistence | Game memory, packets, game-world geometry, behavior tree execution |
| **BotRunner** | Behavior tree execution, IBotTask stack, IObjectManager calls, action dispatch, FG/BG-shared activity logic | Activity selection (StateManager decides), world geometry (PathfindingService), config (StateManager pushes) |
| **PathfindingService** | A* paths, Detour navmesh, route packs, path caches | Per-bot state, activity decisions, scene tiles (delegates to SceneDataService) |
| **SceneDataService** | Collision tiles, 3×3 grid slices, ground-Z queries, scene cache | Pathfinding decisions |
| **ForegroundBotRunner** | Memory reads, native function calls, UI frame handlers, packet capture, anti-Warden, FG-side IObjectManager | Anything that can be done from BG; focus/cursor capture; launcher dependency |
| **BackgroundBotRunner** | Protocol emulation, BG IObjectManager, PhysicsEngine, EventEmitter | Game client launches, native memory, FG-only features |
| **DecisionEngineService** | ML-augmented decision suggestions (rotation choice, threat ranking) | Authority over BotRunner actions (advisory only) |
| **PromptHandlingService** | Optional LLM-augmented intent parsing, GM-command construction | Game logic, authority over actions |
| **WoWStateManagerUI** | Operator-facing presentation, config editing, request submission | Game state (renders snapshots), scheduling (sends to StateManager) |
| **Aspire AppHost** | Docker/service orchestration during development | Production deployment (Docker Compose owns prod) |

## Data flow — the 5 canonical paths

### 1. Snapshot tick (every bot, 10 Hz)

```
Bot --(WoWActivitySnapshot proto)--> StateManager
  → snapshot cache update (ConcurrentDictionary keyed by account)
  → mode handler `OnSnapshotAsync(character, snapshot)`
  → ActivityScheduler progress eval
  → Dashboard summary delta emit
```

### 2. On-demand request

```
Human (UI or in-game whisper) --> StateManager.RequestActivity
  → ActivityRegistry.Resolve(activity, location, levelRange, params)
  → LegalityValidator.Check(request, candidates)
  → BotSelector.Score(candidates) → top-N picks
  → LeaseLedger.Reserve(picks, ActivityInstance)
  → Coordinator (Dungeon/BG/Raid/...).Launch(picks, instance)
  → Coordinator dispatches ObjectiveMessages → bots execute
  → On completion → LeaseLedger.Release → bots resume progression
```

### 3. Automated progression tick

```
StateManager mode handler `OnSnapshotAsync` (Automated mode)
  → ProgressionPlanner.NextObjective(character, snapshot)
  → ActivityScheduler.TryAcquireLease(objective)
    → if lease granted: coordinator launches
    → if no group needed: BotRunner gets direct action
  → snapshot delta proves progress; loop continues
```

### 4. Path query

```
Bot --(PathRequest proto)--> PathfindingService (port 5001)
  → route-pack cache lookup (signature match)
    → hit: return packed path
    → miss: native Detour query
  → SceneDataService consulted for tile data only if not preloaded
  → response: NavigationPath
```

### 5. Config hot reload

```
UI edit --> StateManager.SaveConfig(scope, payload)
  → schema validate
  → atomic file write to Config/<scope>.json
  → ConfigChangedEvent broadcast over IPC
  → subscribers (BotRunner, services) reload affected sections
  → ack response within 2 s or rollback
```

## Process map

| Process | Role | Port(s) |
|---|---|---|
| `mangosd` | World server (Docker) | 8085 (world), 7878 (SOAP) |
| `realmd` | Auth/realm server (Docker) | 3724 |
| `WoWStateManager.exe` | Orchestrator, lease ledger, mode dispatch | 8088 (UI), 5002 (bot snapshot ingest) |
| `WoWStateManagerUI.exe` | WPF operator console | — |
| `PathfindingService.exe` (Docker `wwow-pathfinding`) | Detour routes, route packs | 5001 |
| `SceneDataService.exe` (Docker `wwow-scene-data`) | Collision/ground-Z slices | 5003 |
| `DecisionEngineService.exe` | ML rotation/threat advice | 5004 |
| `PromptHandlingService.exe` | Optional LLM intent | 5005 |
| `BackgroundBotRunner.exe` × N | Headless bots (50–100 per process target) | — |
| `WoW.exe` × M (FG bots) | Injected game clients | — |

## Versioning rules

- **Protobuf contracts** are forward-compatible: new fields are optional;
  removed fields stay reserved. See [`Spec/08_PROTOCOLS.md`](08_PROTOCOLS.md).
- **Hard-coded catalog** versions with `CatalogVersion` int — bumped on any
  catalog row change. The Dashboard shows the running catalog version and
  flags drift between StateManager and a connected bot.
- **Navmesh signatures** version the route-pack cache. See
  [`Spec/06_PATHFINDING.md`](06_PATHFINDING.md).
- **Behavior tree shapes** are not versioned; tests cover regressions.

## Build/deploy summary

See [`BUILD.md`](../BUILD.md) and [`DOCKER_STACK.md`](../DOCKER_STACK.md) for
the full procedure. The agent-relevant bits:

- `dotnet build WestworldOfWarcraft.sln` builds everything managed.
- Native components (`Navigation.dll`, `Loader.dll`, `FastCall.dll`) build
  via MSBuild with toolset `v145`.
- Docker stack: `docker compose -f docker-compose.vmangos-linux.yml up -d`.
- Tests: `.\run-tests.ps1` for the layered suite; `dotnet test
  <project>.csproj` for targeted runs.

## Pointer index

| Topic | Authoritative doc |
|---|---|
| StateManager modes + scale | [`Spec/02_STATEMANAGER.md`](02_STATEMANAGER.md) |
| BotRunner contract | [`Spec/03_BOTRUNNER.md`](03_BOTRUNNER.md) |
| Activities | [`Spec/04_ACTIVITIES.md`](04_ACTIVITIES.md) |
| Pathfinding | [`Spec/06_PATHFINDING.md`](06_PATHFINDING.md) |
| Physics parity | [`Spec/07_PHYSICS.md`](07_PHYSICS.md) |
| Protocols (WoW + IPC) | [`Spec/08_PROTOCOLS.md`](08_PROTOCOLS.md) |
| UI | [`Spec/09_UI.md`](09_UI.md) |
| Metrics | [`Spec/10_METRICS.md`](10_METRICS.md) |
| Logging | [`Spec/11_LOGGING.md`](11_LOGGING.md) |
| Error taxonomy | [`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md) |
| Testing | [`Spec/13_TESTING.md`](13_TESTING.md) |
| Config | [`Spec/14_CONFIG.md`](14_CONFIG.md) |
| Skills | [`Spec/15_SKILLS.md`](15_SKILLS.md) |
