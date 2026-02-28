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

3. [ ] `PFS-MISS-003` Add explicit protobuf->native path mode mapping.
- Problem: `req.Straight` is passed directly to `smoothPath` without a mapping contract.
- Target files: `Services/PathfindingService/PathfindingSocketServer.cs`, `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`.
- Required change: implement explicit mapping function for request mode semantics and document expected behavior.
- Validation command: `rg -n "req\\.Straight|CalculatePath\\(" Services/PathfindingService/PathfindingSocketServer.cs`
- Acceptance criteria: each request mode maps deterministically to intended native mode and is covered by tests.

4. [ ] `PFS-MISS-004` Add path provenance and failure-reason metadata to responses.
- Problem: callers cannot distinguish native-success, no-path, or diagnostic-fallback outcomes.
- Target files: `Services/PathfindingService/PathfindingSocketServer.cs`, proto definitions in `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`.
- Required change: add source/reason fields (for example: `native_path`, `no_path_native`, `diagnostic_fallback`) and emit consistently.
- Validation command: `rg -n "new PathResponse|Corners|ErrorResponse" Services/PathfindingService/PathfindingSocketServer.cs`
- Acceptance criteria: every response includes explicit path-source/result metadata.

5. [ ] `PFS-MISS-005` Enforce not-ready/fail-fast behavior when nav roots are invalid.
- Problem: startup logs warning for missing nav data but can continue serving requests.
- Target files: `Services/PathfindingService/Program.cs`, `Services/PathfindingService/PathfindingServiceWorker.cs`.
- Required change: service fails fast or serves explicit not-ready state until `maps/mmaps/vmaps` roots are valid.
- Validation command: `rg -n "FindPath may fail|WWOW_DATA_DIR|mmaps|vmaps|ready" Services/PathfindingService/Program.cs Services/PathfindingService/PathfindingServiceWorker.cs`
- Acceptance criteria: service never reports ready with missing nav data directories.

6. [ ] `PFS-MISS-006` Add deterministic Orgrimmar corpse-run regression vectors in pathfinding tests.
- Problem: no fixed corpse-run vectors assert wall-avoidance behavior at service level.
- Target files: `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs`, fixtures.
- Required change: add fixed start/end vectors that previously produced wall collisions; assert non-empty, finite, walkable routes.
- Validation command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests" --logger "console;verbosity=minimal"`
- Acceptance criteria: vector regressions fail when output collides with known wall-run patterns.

7. [ ] `PFS-MISS-007` Validate C++ -> protobuf -> C# path data integrity.
- Problem: no hard gate proves corner count/order/coordinates survive interop unchanged.
- Target files: `Services/PathfindingService/Repository/Navigation.cs`, `Services/PathfindingService/PathfindingSocketServer.cs`, `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`, related tests.
- Required change: add interop assertions for coordinate precision, order preservation, and truncation resistance.
- Validation command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
- Acceptance criteria: tests fail on coordinate drift, dropped nodes, or order mismatch.

## Simple Command Set
1. Build service: `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore`
2. Pathfinding tests: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. Corpse-run test: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Pass result: `delta shipped`
- Last delta: converted to execution-card format with refreshed build/test baselines and explicit interop/task validation gates.
- Next task: `PFS-MISS-001`
- Next command: `Get-Content -Path 'Services/PromptHandlingService/TASKS.md' -TotalCount 320`
- Blockers: local test environment missing nav data under `Bot\\Release\\x64\\mmaps`; `dumpbin` missing on PATH in vcpkg app-local script.
