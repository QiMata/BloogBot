# Task Archive

Completed items moved from TASKS.md.

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
