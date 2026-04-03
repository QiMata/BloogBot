# WoWStateManager Tasks

## Scope
- Directory: `Services/WoWStateManager`
- Project: `WoWStateManager.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: lifecycle orchestration, snapshot/action forwarding, docker-aware service bootstrap, and spawned bot-worker parity.

## Execution Rules
1. Keep changes scoped to `Services/WoWStateManager` plus direct consumers/tests.
2. Never blanket-kill `dotnet` or `WoW.exe`; use repo-scoped cleanup or explicit PIDs only.
3. Every lifecycle/bootstrap change must include a concrete validation command in `Session Handoff`.
4. Archive completed items to `Services/WoWStateManager/TASKS_ARCHIVE.md` in the same session when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `WSM-PAR-001` Quest snapshot sync lag
- [ ] Trace quest-state latency between WoWSharpClient packet handlers, StateManager snapshot publication, and test assertions.

2. `WSM-BOOT-001` Bootstrap cleanup follow-up
- [ ] Re-check any remaining assumptions that local `C:\Mangos\server` processes are always host-launched once the docker path becomes the default path.

## Session Handoff
- Last updated: 2026-04-03 (session 299)
- Active task: `WSM-BOOT-001`
- Last delta:
  - Session 299 validated the current ownership split end-to-end on the live Linux service stack: `pathfinding-service` and `scene-data-service` are running separately, reachable on `5001`/`5003`, and `WoWStateManager` remains host-side and process-scope-limited to WoW client workers.
  - Confirmed there is no remaining StateManager lifecycle ownership over `PathfindingService`/`SceneDataService`; startup and worker loops only probe/report dependency availability while continuing launch orchestration.
  - Current test status on this branch: `run-tests.ps1 -Layer 4 -SkipBuild` passed; a full `BotRunner.Tests` `LiveValidation` sweep was started and then interrupted by user request before completion, so the live matrix remains in-progress.
  - Validation:
    - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service` and `scene-data-service` both running with expected host ports
    - `docker logs --tail 80 pathfinding-service` -> map preload from mounted `/wwow-data`
    - `docker logs --tail 80 scene-data-service` -> ready on `0.0.0.0:5003` with initialized map set
    - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`
  - Session 298 removed the remaining StateManager startup gate that still blocked bot launch on `PathfindingService` readiness. `StateManagerWorker.ApplyDesiredWorkerState(...)` now treats `PathfindingService` and `SceneDataService` as external dependencies: it probes both endpoints for diagnostics, logs readiness/unavailability, and continues launching configured WoW clients either way.
  - This aligns runtime behavior with the ownership split: `Program.Main` and worker startup now both use warn-only external-dependency semantics rather than launch gating for split services that StateManager no longer manages.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - Session 297 completed `WSM-HOST-001`. `WoWStateManager` no longer launches or kills `PathfindingService`/`SceneDataService`; `Program.Main` now treats both as external dependencies, performs bounded readiness checks only, and then proceeds to client orchestration.
  - `docker-compose.windows.yml` now splits the runtime services into separate containers (`pathfinding-service` and `scene-data-service`) and wires `background-bot-runner` to both endpoints.
  - `Services/WoWStateManager/appsettings.Docker.json` now includes `SceneDataService` host/port so spawned BG workers inherit the same split-service defaults.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`
  - Session 296 updated the scene-data bring-up contract to match the BG worker's deferred slice client. `WoWStateManager` still launches `SceneDataService`, but its timeout warning no longer claims BG runners will definitely fall back to local preloaded-map physics; the warning now states that bots will still launch and retry scene-slice acquisition on demand once the service becomes available.
  - Practical implication for this owner: a late `SceneDataService` no longer implies a permanent runtime downgrade. The old session-295 log line (`without scene slices`) is historical, not current behavior.
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests|FullyQualifiedName~BotRunner.Tests.IPC.ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts" --logger "console;verbosity=minimal"` -> `passed (14/14)`
  - Session 296 finished the RFC coordinator cleanup tracked in the master file. `CharacterStateSocketListener` now constructs `DungeoneeringCoordinator` without the old prep-skip toggle, and the RFC test fixture disables the coordinator during prep so StateManager only coordinates group formation/RFC entry after fixture staging is done.
  - The coordinator itself now transitions from `WaitingForBots` straight into `FormGroup_Inviting`, which keeps the RFC path out of the old prep-action flow even if the coordinator comes online early.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CoordinatorStrictCountTests|FullyQualifiedName~CoordinatorFixtureBaseTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - Session 295 changed `SceneDataService` bootstrap from a hard startup gate into a best-effort dependency. `WoWStateManager` still launches the service, but `WaitForSceneDataService()` now gives it only a short `2.5s` window before continuing so BG workers can fall back to pure local preloaded-map physics instead of stalling fixture startup for two minutes.
  - Live AV proof moved accordingly: `WoWStateManager` now reaches `READY` with `SceneDataService` unavailable, and the latest full AV first-objective rerun no longer fails at the old scene-service startup skip. It still stalls earlier at `40/80` during bring-up, so the remaining blocker is launch pressure / alliance-wave startup, not the scene-data gate.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"` -> `failed` at `[AV:EnterWorld] STALE - bot count stopped at 40/80 for 123s`; `WoWStateManager` reached port `8088` and logged `SceneDataService did not become available ... Background bots will use local Navigation.dll physics without scene slices.`
  - Session 294 extended the spawned-BG endpoint contract to include `SceneDataService__IpAddress` / `SceneDataService__Port`. `StateManagerWorker.StartBackgroundBotWorker(...)` now forwards those values from config or the `WWOW_SCENE_DATA_*` env vars, so BG runners can stay on the scene-backed local physics path instead of launching without a scene endpoint.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release -o E:\tmp\isolated-wowstatemanager\bin --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `WoWStateManager` is now treated as host-side by design because it must launch local `WoW.exe` clients; the Windows compose stack should no longer include a `wow-state-manager` container.
  - Kept the idle host-side `WoWStateManager` path in place with `MangosServer__AutoLaunch=false` and `WWOW_SETTINGS_OVERRIDE=StateManagerSettings.Idle.json`.
  - Updated the stack docs so the containerized pieces stay `vmangos-server` / `pathfinding-service`, while `WoWStateManager` remains outside Docker.
- Pass result: `StateManager remains host-side and only manages WoW client workers while split Pathfinding/SceneData services run externally`
- Validation/tests run:
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> confirms split external services are online
  - `docker logs --tail 80 pathfinding-service` -> confirms pathfinding preload activity
  - `docker logs --tail 80 scene-data-service` -> confirms scene-data ready state
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
- Files changed:
  - `Services/WoWStateManager/TASKS.md`
  - `docs/TASKS.md`
  - `docs/DOCKER_STACK.md`
  - `Services/README.md`
  - `Services/WoWStateManager/StateManagerWorker.cs`
  - `Services/WoWStateManager/Program.cs`
  - `Services/WoWStateManager/appsettings.json`
  - `Services/WoWStateManager/appsettings.Docker.json`
  - `Services/WoWStateManager/TASKS.md`
  - `Services/WoWStateManager/TASKS_ARCHIVE.md`
  - `Services/SceneDataService/Dockerfile`
  - `docker-compose.windows.yml`
  - `docs/DOCKER_STACK.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"`
- Blockers: full `LiveValidation` completion now depends on uninterrupted long-running execution time, not split-service bring-up.
