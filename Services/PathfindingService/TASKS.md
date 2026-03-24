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
- [ ] Extend the current short-route diagnostics into bot-side planned-vs-executed tracing so shoreline failures can be attributed to route shape versus runtime drift.

3. `PFS-OBJ-001` Object-aware routing contract
- [ ] Continue the overlay-aware route contract so callers can request richer blocked-reason and affordance metadata without re-implementing path safety in BotRunner.

4. `PFS-DOCKER-001` Containerized runtime validation
- [ ] Validate the refreshed Windows Docker image against mounted `WWOW_DATA_DIR` nav data and capture first-run readiness evidence.

## Session Handoff
- Last updated: 2026-03-24
- Active task: `PFS-NAV-002`
- Last delta:
  - Shipped the next BotRunner-side execution follow-up for `PFS-NAV-002`: `NavigationPath.CurrentWaypoints` now exports only the remaining active corridor after waypoint advancement.
  - That prevents `MovementController` from being reset onto stale already-cleared corners after the managed path logic has already advanced the active index.
  - Added a deterministic regression in `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs` and kept the `NavigationPathTests|GatheringRouteTaskTests` slice green.
  - `PathfindingService` itself was not changed or redeployed in this pass.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"` -> `passed` (`58/58`)
  - `Get-Process PathfindingService,WoWStateManager,BackgroundBotRunner,WoW -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,Path` -> `no matching repo-scoped runtime processes`
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Services/PathfindingService/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
- Blockers: actual `PathfindingService` container runtime validation is still blocked because the current Docker engine is Linux-only and the service/native `Navigation.dll` path is Windows-specific.
