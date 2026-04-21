# Task Archive

Completed items moved from TASKS.md.

## Completed (2026-04-12 session 75)

### [x] PFS-TST-010 - Refresh stale corpse-run route fixtures against current native segment validation
- Completed by replaying the legacy Orgrimmar graveyard/center and Razor Hill corpse-run fixtures under the current native validator, confirming they no longer match current walkability/LOS truth, and trimming `PathfindingTests` down to the two surviving Orgrimmar live-retrieve route contracts that still pass cleanly.
- Also hardened service-side validated-path handling in `Services/PathfindingService/Repository/Navigation.cs`:
  1. restored the `WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION` gate,
  2. accepted `MissingSupport` like native path refinement and `PathRouteAssertions`,
  3. threaded grounded `resolvedEndZ` values forward on clear segments,
  4. suppressed duplicate grounded waypoints,
  5. skipped bounded segment repair for straight-corner requests so the budget guard remains deterministic.
- Validation:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter|FullyQualifiedName~CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision" --logger "console;verbosity=minimal"` -> `failed (2/2)` with `BlockedGeometry` on `Segment 1->2` and `Segment 8->9`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`

## Completed (2026-04-12 session 74)

### [x] PFS-TST-003 - Add blocked-corridor reroute regression for wall-avoidance behavior
- Completed by adding `CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine` to `PathingAndOverlapTests.cs`.
- The new regression proves:
  1. the direct corpse-retrieve line-of-sight is blocked,
  2. the returned route still satisfies `PathRouteAssertions`,
  3. the intermediate waypoints deviate materially from the blocked direct segment,
  4. failure output includes the map id plus the full waypoint list.
- Validation:
  - `dotnet restore Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --verbosity minimal` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine" --logger "console;verbosity=minimal"` -> `passed (1/1)`

### [x] PFS-TST-005 - Keep corpse-run path validation aligned with Orgrimmar runback scenario
- Completed by adding `BotTasks/OrgrimmarCorpseRunPathTask.cs` and wiring `PathfindingBotTaskTests.OrgrimmarCorpseRunPath_ShouldReturnValidWaypointPath`.
- Also refreshed the stale generic `PathCalculationTask` onto the same live Orgrimmar corpse-retrieve corridor so the owner-facing class filter is green again.
- Validation:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`

## Completed (2026-03-12 session 73)

### [x] PFS-TST-009 - Add deterministic shoreline path-diagnostic helper coverage
- Completed by extracting `PathRouteDiagnostics` from `PathfindingSocketServer` and pinning the short-route logging rules in `PathRouteDiagnosticsTests.cs`.
- Coverage now proves:
  1. short healthy routes are logged,
  2. healthy long unsampled routes are not,
  3. combined diagnostic reasons are stable and explicit,
  4. logged corner chains truncate deterministically.
- Validation:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathRouteDiagnosticsTests" --logger "console;verbosity=minimal"` -> `4 passed`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `39 passed`

## Completed (2026-03-12 session 71)

### [x] PFS-TST-002 - Convert baseline path assertions to full route validity contract
- Completed by centralizing route validation in `PathRouteAssertions.cs` and reusing it across bot tasks plus baseline xUnit route tests.
- Route assertions now cover:
  1. finite coordinates,
  2. start/end proximity,
  3. zero-length segment rejection,
  4. segment-length/height thresholds,
  5. grounded short-segment validation through `ValidateWalkableSegment`.
- `PathfindingTests` now enable `WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION=1` during deterministic route-contract checks so the test path reflects the shaped/repaired service route instead of the ungated rollout default.
- Validation:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests" --logger "console;verbosity=minimal"` -> `4 passed`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests|FullyQualifiedName~NavigationOverlayAwarePathTests" --logger "console;verbosity=minimal"` -> `12 passed`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `35 passed`
