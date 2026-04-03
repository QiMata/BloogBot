# PathfindingService Tasks

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: deterministic path-response contracts, shoreline/object-aware diagnostics, and dockerized runtime packaging.

## Execution Rules
1. Keep runtime routing on native path output unless a task explicitly changes the contract.
2. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
3. Every pathing or packaging slice must end with at least one focused build/test command in `Session Handoff`.
4. Archive completed items to `Services/PathfindingService/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `PFS-NAV-002` Walkable-tile-preserving smoothing
- [ ] Keep bot-side smoothing/follower precision constrained to walkable triangles/poly corridors so waypoint following never curves across unwalkable terrain.
- [ ] If the managed follower still leaves the corridor after the current BotRunner guardrails, capture the exact segment and decide whether the next fix belongs in `NavigationPath`, `MovementController`, or the native corridor output.

2. `PFS-FISH-001` Ratchet shoreline attribution
- [ ] Keep the Ratchet pool-visibility preflight explicit. The April 2 reruns now show three honest staged outcomes: no local child spawned, a local child spawned but stayed invisible from the dock stage, or local Ratchet children only became spawnable on direct child-pool probes after the staged refresh path stayed empty.
- [ ] Once the dual slice has a visible staged pool again, extend the current short-route diagnostics into bot-side planned-vs-executed tracing so shoreline failures can be attributed to route shape versus runtime drift. Focused FG is green again on the current BotRunner binaries; the remaining shoreline/runtime question is the dual BG leg after staged visibility succeeds.

3. `PFS-OBJ-001` Object-aware routing contract
- [ ] Continue the overlay-aware route contract so callers can request richer blocked-reason and affordance metadata without re-implementing path safety in BotRunner.

4. `PFS-LIVE-001` Live integration sweep
- [ ] Run the full `LiveValidation` namespace against the current split-service Linux stack and capture the first complete pass/fail matrix without interruption.

5. `PFS-DOCKER-001` Containerized runtime validation
- [x] Split Docker topology so `PathfindingService` and `SceneDataService` run as separate Windows services with BG endpoint wiring.
- [x] Validate split Linux containers against mounted `WWOW_DATA_DIR` nav/scene data and capture readiness evidence.

## Session Handoff
- Last updated: 2026-04-03 (session 299)
- Active task: `PFS-LIVE-001`
- Last delta:
  - Session 299 closed the Linux deployment gap for the split services. `pathfinding-service` and `scene-data-service` are both running from `docker-compose.vmangos-linux.yml`, both publish host ports (`5001`, `5003`), and both mount `WWOW_DATA_DIR` as `/wwow-data`.
  - Runtime evidence on the live containers confirms the expected preload behavior: `PathfindingService` map preloads are active, and `SceneDataService` reports ready with initialized map coverage while serving scene slices from the mounted data path.
  - `run-tests.ps1 -Layer 4 -SkipBuild` is currently green; a full `LiveValidation` namespace run was started but intentionally interrupted by user request before completion, so the complete live matrix is still pending.
  - Validation:
    - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service` and `scene-data-service` both `Up`, publishing `5001` and `5003`
    - `docker compose -f .\docker-compose.vmangos-linux.yml ps` -> split services listed as running in the compose stack
    - `docker logs --tail 80 pathfinding-service` -> confirms `/wwow-data` preload activity across maps
    - `docker logs --tail 80 scene-data-service` -> confirms `/wwow-data` load + `Ready and listening on 0.0.0.0:5003`
    - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed (2/2 layers)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`; long-running matrix needs continuation
  - Session 297 finished the split-service Docker wiring requested by the BG local-collision design. `docker-compose.windows.yml` now defines `pathfinding-service` and `scene-data-service` as separate services, and `background-bot-runner` inherits both endpoints (`PathfindingService__*`, `SceneDataService__*`) by default.
  - Added `Services/SceneDataService/Dockerfile` so scene slices can be deployed independently from pathfinding.
  - Validation confirms compose structure is correct, but runtime deployment on this host is still blocked by Docker engine mode: `docker info` reports `OSType=linux`, so Windows images cannot start (`no matching manifest for linux/amd64`).
  - Validation:
    - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml --profile bgbot config` -> `succeeded`
    - `docker run --rm mcr.microsoft.com/windows/servercore:ltsc2022 cmd /c echo windows` -> `failed` (`no matching manifest for linux/amd64`)
  - Session 290 updated the native preload path to discover map ids from `Data/scenes`, `Data/mmaps`, and `Data/maps` instead of hard-coding `0`, `1`, and `389`. `PathfindingSocketServer` now reports the discovered preload set in its ready status so the shared fallback physics/pathing service and the scene-slice service stay aligned on available maps.
  - Practical implication: when BG runners fall back to shared `PathfindingService` physics, the service now has the same broad map preload coverage as the scene-backed local path.
  - No `PathfindingService` binary changes shipped in this pass. The delta is a BotRunner-side shoreline-stall escape plus fresh live evidence.
  - `FishingTask` now rejects non-progressing shoreline approach targets after `12s`, and the focused FG packet-capture rerun passed end-to-end on the current binaries (`loot_bag_delta -> fishing_loot_success`).
  - Practical implication for `PathfindingService`: the focused FG shoreline route is no longer the blocker. The remaining path-attribution problem is back where it belongs: staged visibility in the dual fixture first, then planned-vs-executed evidence for the BG/local-pier runtime leg once stage readiness is re-established.
- Pass result: `split Pathfinding/SceneData services are dockerized, deployed, and reachable on Linux with mounted data volumes`
- Validation/tests run:
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service` + `scene-data-service` running and publishing host ports
  - `docker compose -f .\docker-compose.vmangos-linux.yml ps` -> both split services present and running
  - `docker logs --tail 80 pathfinding-service` -> map preload logs from mounted `/wwow-data`
  - `docker logs --tail 80 scene-data-service` -> ready state and map initialization from mounted `/wwow-data`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`
  - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`
  - `docker compose -f .\docker-compose.windows.yml --profile bgbot config` -> `succeeded`
  - `docker run --rm mcr.microsoft.com/windows/servercore:ltsc2022 cmd /c echo windows` -> `failed` (`no matching manifest for linux/amd64`)
- Files changed:
  - `docker-compose.vmangos-linux.yml`
  - `Services/PathfindingService/Dockerfile`
  - `Services/SceneDataService/Dockerfile`
  - `Services/PathfindingService/TASKS_ARCHIVE.md`
  - `Services/SceneDataService/Dockerfile`
  - `docker-compose.windows.yml`
  - `docs/DOCKER_STACK.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"`
- Blockers: no deployment blocker; remaining blocker is the duration/instability of the full live matrix run, which requires uninterrupted execution time.
