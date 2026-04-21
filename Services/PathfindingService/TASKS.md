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
- Last updated: 2026-04-15
- Active task: `none (all tracked owner items closed)`
- Last delta:
  - Added `PathAffordanceClassifier` and wired `PathfindingSocketServer` to emit the expanded route affordance response fields: jump-gap, safe-drop, unsafe-drop, blocked counts, max climb height, max gap distance, and max drop height.
  - `Repository.Navigation` now exposes `ClassifySegmentAffordance(...)` for explicit native segment classification calls.
  - Default response aggregation remains fast/geometric; bounded native aggregation is opt-in via `WWOW_ENABLE_NATIVE_AFFORDANCE_SUMMARY=1`.
- Pass result: `surface affordance response metadata shipped; all currently tracked PathfindingService items remain complete`
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
- Files changed:
  - `Services/PathfindingService/PathAffordanceClassifier.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/README.md`
  - `Services/PathfindingService/TASKS.md`
  - `docs/TASKS.md`
- Next command: `rg -n "^- \\[ \\]|\\[ \\] Problem|Active task:" docs/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md`
