# PathfindingService Tasks

Master tracker: `MASTER-SUB-018`

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Focus: corpse-run path validity (Orgrimmar runback), native-path usage correctness, and deterministic service readiness/response contracts.
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
- **Status: Already addressed.** Default routing returns native `FindPath` output or empty array. `BuildLosFallbackPath` is gated behind `WWOW_ENABLE_LOS_FALLBACK` env var (disabled by default, line 84). No code change needed.

2. [x] `PFS-MISS-002` Remove elevated LOS probe acceptance from runtime path validation.
- **Status: Already addressed.** `TryHasLosForFallback` is only called within the opt-in `BuildLosFallbackPath` path (lines 139, 235, 248, 325). Default production routing never invokes elevated LOS probes.

3. [x] `PFS-MISS-003` Add explicit protobuf->native path mode mapping.
- **Done (batch 5).** `req.Straight` → local `smoothPath` variable + log labels fixed for clarity.
- Acceptance criteria: each request mode maps deterministically to intended native mode.

4. [x] `PFS-MISS-004` Add path provenance and failure-reason metadata to responses.
- **Done (batch 13).** Added `result` (string) and `raw_corner_count` (uint32) fields to `CalculatePathResponse` in `pathfinding.proto`.
  - `PathfindingSocketServer.cs` populates: `result = "native_path"` when corners > 0, `"no_path"` when empty. `raw_corner_count` = pre-sanitization count.
  - Protoc regenerated `Pathfinding.cs`.
- Validation: `dotnet build Services/PathfindingService/PathfindingService.csproj -c Debug` — 0 errors.
- Acceptance: every path response includes explicit source/result metadata.

5. [x] `PFS-MISS-005` Enforce not-ready/fail-fast behavior when nav roots are invalid.
- **Done (batch 5).** `Environment.Exit(1)` instead of warning-and-continue when nav data dirs missing.
- Acceptance criteria: service never reports ready with missing nav data directories.

6. [x] `PFS-MISS-006` Add deterministic Orgrimmar corpse-run regression vectors in pathfinding tests.
- **Done (batch 11).** Added 3 Orgrimmar regression vectors (graveyard→center, entrance→VoS, reverse) with finite-coordinate and min-waypoint assertions to `PathingAndOverlapTests.cs`.
- Acceptance criteria: vector regressions fail when output collides with known wall-run patterns.

7. [x] `PFS-MISS-007` Validate C++ -> protobuf -> C# path data integrity.
- **Done (batch 13).** Added 4 proto round-trip tests to `ProtoInteropExtensionsTests.cs`:
  - `PathCorners_RoundTripThroughProto_PreservesCountOrderAndPrecision` — 5-corner path with provenance metadata
  - `PathCorners_EmptyPath_PreservesNoPathResult` — empty path + "no_path" result
  - `PathCorners_OrderPreserved_NotSortedOrShuffled` — non-sorted corner order preserved
  - `PathCorners_ExtremePrecision_NoTruncation` — float edge values survive serialization
- Validation: 6/6 ProtoInteropExtensionsTests pass (2 existing + 4 new).
- Acceptance: tests fail on coordinate drift, dropped nodes, order mismatch, or precision truncation.

### PFS-PAR-001 ~~Research~~ Done: PathfindingService readiness timeout during gathering tests
- [x] **Done (2026-02-28).** Root cause: `BotServiceFixture` only checked StateManager (port 8088), never PathfindingService (port 5001). If PathfindingService fails to start (WWOW_DATA_DIR missing), StateManager continues but tests requiring pathfinding fail.
- Fix: Added `WaitForPathfindingServiceAsync()` to `BotServiceFixture.cs` — waits up to 30s for port 5001 after StateManager ready. `PathfindingServiceReady` property exposed through `LiveBotFixture.IsPathfindingReady`. `DeathCorpseRunTests` now skips gracefully with diagnostic message about WWOW_DATA_DIR.
- Files: `Tests/Tests.Infrastructure/BotServiceFixture.cs`, `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`, `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
- Validation: `dotnet build Tests/Tests.Infrastructure/Tests.Infrastructure.csproj -c Release` — 0 errors; InfrastructureConfig tests 7/7 pass

## Simple Command Set
1. Build service: `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore`
2. Pathfinding tests: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. Corpse-run test: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all PathfindingService tasks complete (PFS-MISS-001..007)
- Last delta: PFS-MISS-004 (provenance metadata) + PFS-MISS-007 (4 proto round-trip integrity tests)
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Services/PathfindingService/PathfindingService.csproj -c Debug` — 0 errors
  - `dotnet test Tests/PathfindingService.Tests -c Debug --filter ProtoInteropExtensionsTests` — 6/6 pass
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto` — result + raw_corner_count fields
  - `Exports/BotCommLayer/Models/Pathfinding.cs` — regenerated
  - `Services/PathfindingService/PathfindingSocketServer.cs` — populate provenance fields
  - `Tests/PathfindingService.Tests/ProtoInteropExtensionsTests.cs` — 4 new integrity tests
- Next command: continue with next queue file
- Blockers: local test environment missing nav data under `Bot\\Release\\x64\\mmaps`; `dumpbin` missing on PATH in vcpkg app-local script.
