# PathfindingService Tasks

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: deterministic path-response contracts, object-aware diagnostics, and dockerized runtime packaging.

## Execution Rules
1. Keep runtime routing on native path output unless a task explicitly changes the contract.
2. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
3. Every pathing or packaging slice must end with at least one focused build/test command in `Session Handoff`.
4. Archive completed items to `Services/PathfindingService/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `PFS-OBJ-001` Object-aware routing contract
- [x] Close the caller-adoption slice so higher-level BotRunner navigation consumes `GetPathResult(...)` and reacts to service-side blocked reasons instead of relying only on corners-only responses.

2. `PFS-LIVE-001` Live integration sweep
- [x] Run the full `LiveValidation` namespace against the current split-service Linux stack and capture the first complete pass/fail matrix without interruption.

3. `PFS-DOCKER-001` Containerized runtime validation
- [x] Split Docker topology so `PathfindingService` and `SceneDataService` run as separate Windows services with BG endpoint wiring.
- [x] Validate split Linux containers against mounted `WWOW_DATA_DIR` nav/scene data and capture readiness evidence.

## Session Handoff
- Last updated: 2026-04-30
- Active task: `LPATH-CROSSROADS-UC` deterministic Tauren-sized route clearance slice
- Last delta:
  - `Repository.Navigation` now calls native `FindPathForAgent(...)` with the requested capsule radius/height instead of using a fixed pathfinding capsule.
  - Long smooth routes are densified before deterministic validation, early duplicate support anchors are collapsed to the real support layer, and the early static/capsule repair pass remains bounded to the first 24 segments.
  - Added generic local escape candidates for early static/capsule breaks so the Orgrimmar support/tree stall can route out through PathfindingService without hard-coded micro-waypoints.
- Pass result: `delta shipped; deterministic Tauren Male long-pathing route suite passed, live validation remains in BotRunner owner`
- Validation/tests run:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_tauren_agent_collapsed_support.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`
- Files changed:
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathRouteAssertions.cs`
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/Navigation.h`
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/PathFinder.h`
  - `docs/TASKS.md`
- Next command: `git status --short --branch`
