# PathfindingService Tasks

Master tracker: `MASTER-SUB-018`

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Focus: corpse-run path validity (Orgrimmar runback), short-horizon shoreline route diagnostics, native-path usage correctness, and deterministic service readiness/response contracts.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Execute tasks in order unless blocked by a recorded dependency.
2. Keep runtime routing on native path output; fallback pathing remains diagnostics-only.
3. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
4. Every pathing fix must be validated with a simple command and recorded in `Session Handoff`.
5. Archive completed items to `Services/PathfindingService/TASKS_ARCHIVE.md` in the same session.
6. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Build check passes: `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore` -> `0 Error(s)`, `0 Warning(s)`.
- Runtime fallback remains wired:
  - `WWOW_ENABLE_LOS_FALLBACK` gate and fallback return path in `Repository/Navigation.cs:63`, `:85`, `:133`.
  - Elevated LOS probe logic in `TryHasLosForFallback` (`Repository/Navigation.cs:351`).
- Path request mode is forwarded directly from protobuf:
  - `PathfindingSocketServer` calls `_navigation.CalculatePath(..., req.Straight)` (`PathfindingSocketServer.cs:181`) with no explicit semantic mapping layer.
- Path response is corners-only and does not include source/reason metadata:
  - `resp.Corners.AddRange(...)` in `PathfindingSocketServer.cs:196`.
- Startup can continue with unresolved nav roots:
  - warning-only behavior: `Program.cs:52` (`FindPath may fail`).
- Current test baseline (`dotnet test Tests/PathfindingService.Tests/...`):
  - `4` failed, `8` passed.
  - 3 failures due missing nav data root (`Bot\\Release\\x64\\mmaps`) from `NavigationFixture.cs:71`.
  - 1 failure in LOS regression (`PhysicsEngineTests.cs:107`).
  - test output also reports missing `dumpbin` in vcpkg app-local script.
- Interop chain source files:
  - proto contract: `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`
  - C# request handling: `Services/PathfindingService/PathfindingSocketServer.cs`
  - native call boundary: `Services/PathfindingService/Repository/Navigation.cs`.

## P0 Active Tasks (Ordered)
1. [x] `PFS-MISS-001` Remove LOS-grid fallback from default production runback routing.
- Status: already addressed. Default routing returns native `FindPath` output or empty array. `BuildLosFallbackPath` is gated behind `WWOW_ENABLE_LOS_FALLBACK` (disabled by default). No code change needed.

2. [x] `PFS-MISS-002` Remove elevated LOS probe acceptance from runtime path validation.
- Status: already addressed. `TryHasLosForFallback` is only called within the opt-in `BuildLosFallbackPath` path. Default production routing never invokes elevated LOS probes.

3. [x] `PFS-MISS-003` Add explicit protobuf->native path mode mapping.
- Done (batch 5). `req.Straight` -> local `smoothPath` variable + log labels fixed for clarity.
- Acceptance criteria: each request mode maps deterministically to intended native mode.

4. [x] `PFS-MISS-004` Add path provenance and failure-reason metadata to responses.
- Done (batch 13). Added `result` (string) and `raw_corner_count` (uint32) fields to `CalculatePathResponse` in `pathfinding.proto`.
  - `PathfindingSocketServer.cs` populates `result = "native_path"` when corners > 0, `"no_path"` when empty.
  - `raw_corner_count` records the pre-sanitization corner count.
  - Protoc regenerated `Pathfinding.cs`.
- Validation: `dotnet build Services/PathfindingService/PathfindingService.csproj -c Debug` -> 0 errors.
- Acceptance: every path response includes explicit source/result metadata.

5. [x] `PFS-MISS-005` Enforce not-ready/fail-fast behavior when nav roots are invalid.
- Done (batch 5). `Environment.Exit(1)` replaced warning-and-continue when nav data dirs are missing.
- Acceptance criteria: service never reports ready with missing nav data directories.

6. [x] `PFS-MISS-006` Add deterministic Orgrimmar corpse-run regression vectors in pathfinding tests.
- Done (batch 11). Added 3 Orgrimmar regression vectors (graveyard->center, entrance->VoS, reverse) with finite-coordinate and min-waypoint assertions to `PathingAndOverlapTests.cs`.
- Acceptance criteria: vector regressions fail when output collides with known wall-run patterns.

7. [x] `PFS-MISS-007` Validate C++ -> protobuf -> C# path data integrity.
- Done (batch 13). Added 4 proto round-trip tests to `ProtoInteropExtensionsTests.cs`:
  - `PathCorners_RoundTripThroughProto_PreservesCountOrderAndPrecision`
  - `PathCorners_EmptyPath_PreservesNoPathResult`
  - `PathCorners_OrderPreserved_NotSortedOrShuffled`
  - `PathCorners_ExtremePrecision_NoTruncation`
- Validation: 6/6 `ProtoInteropExtensionsTests` pass.
- Acceptance: tests fail on coordinate drift, dropped nodes, order mismatch, or precision truncation.

### PFS-PAR-001 Research Done: PathfindingService readiness timeout during gathering tests
- [x] Done (2026-02-28). Root cause: `BotServiceFixture` only checked StateManager (port 8088), never PathfindingService (port 5001). If PathfindingService fails to start (`WWOW_DATA_DIR` missing), StateManager continues but tests requiring pathfinding fail.
- Fix: added `WaitForPathfindingServiceAsync()` to `BotServiceFixture.cs` so the fixture waits up to 30s for port 5001 after StateManager is ready. `PathfindingServiceReady` is exposed through `LiveBotFixture.IsPathfindingReady`. `DeathCorpseRunTests` now skips gracefully with diagnostic output.
- Files: `Tests/Tests.Infrastructure/BotServiceFixture.cs`, `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`, `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
- Validation: `dotnet build Tests/Tests.Infrastructure/Tests.Infrastructure.csproj -c Release` -> 0 errors; InfrastructureConfig tests 7/7 pass.

### PFS-FISH-001 Research: Ratchet shoreline fishing-route diagnostics
- [ ] Problem: `FishingTask` can now acquire the correct pool and the live contract can succeed, but the short route from the Ratchet named-teleport landing to a castable pool position can still strand FG/BG on terrain or at a no-LOS endpoint before `FishingTask in_cast_range`.
- [ ] Target files: `Services/PathfindingService/PathfindingSocketServer.cs`, `Services/PathfindingService/Repository/Navigation.cs`, `Tests/PathfindingService.Tests/`, live evidence from `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`.
- [ ] Required change:
  1. Log the requested start/end points and returned corners for short shoreline routes used by fishing-hole approaches.
  2. Record enough metadata to distinguish "bad route returned" from "route returned but runtime execution drifted off it".
  3. Tie the diagnostics back to the Ratchet fishing evidence (`FishingTask los_blocked phase=move`, `Your cast didn't land in fishable water`).
- [ ] Acceptance criteria: a focused pathfinding pass can name whether the Ratchet shoreline issue is a service route-shape problem, a native collision/path problem, or a runtime execution drift problem.

## Simple Command Set
1. Build service: `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore`
2. Pathfinding tests: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
3. Corpse-run test: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-03-12
- Active task: `PFS-FISH-001` Ratchet shoreline fishing-route diagnostics
- Last delta: added explicit fishing-route ownership for the short Ratchet shoreline path and refreshed the project/test baseline before code changes
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `25 passed`
- Files changed:
  - `Services/PathfindingService/TASKS.md`
- Next command: `rg --line-number "CalculatePath|IsInLineOfSight|TryHasLos|HandlePath" Services/PathfindingService/PathfindingSocketServer.cs Services/PathfindingService/Repository/Navigation.cs Exports/BotRunner/Tasks/FishingTask.cs Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
- Blockers: the live Ratchet shoreline failure is still only documented by downstream fishing evidence (`FishingTask los_blocked phase=move`, `Your cast didn't land in fishable water`) until the new route diagnostics are added.
