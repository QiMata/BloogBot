# PathfindingService Tasks

Master tracker: `MASTER-SUB-018`

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Focus: corpse-run path validity (Orgrimmar runback), short-horizon shoreline route diagnostics, object-aware path requests, affordance-rich path responses, and deterministic service readiness/response contracts.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Execute tasks in order unless blocked by a recorded dependency.
2. Keep runtime routing on native path output; fallback pathing remains diagnostics-only.
3. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
4. Every pathing fix must be validated with a simple command and recorded in `Session Handoff`.
5. Archive completed items to `Services/PathfindingService/TASKS_ARCHIVE.md` in the same session.
6. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
7. Every implementation slice must add or update focused unit tests and finish with those tests passing before the next slice unless a blocker is recorded.
8. After each shipped delta, update this file and `docs/TASKS.md`, commit, push, and hand off the next open item for the next session.
9. For the object-aware pathfinding phase (`PFS-OBJ-*`), every checkpoint must explicitly record: passing unit tests, plan/doc updates, full branch commit+push, and the exact next command the next session must run first.

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

### PFS-OBJ-001 Contract: object-aware path request/response
- [ ] Problem: `CalculatePathRequest` currently only carries `map_id`, `start`, `end`, and `straight`. Dynamic objects already flow through `PhysicsInput.nearby_objects`, but BotRunner cannot ask PathfindingService for a path that is aware of live world obstacles.
- [ ] Target files: `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`, generated `Exports/BotCommLayer/Models/Pathfinding.cs`, `Services/PathfindingService/PathfindingSocketServer.cs`, `Exports/BotRunner/Clients/PathfindingClient.cs`.
- [x] Progress (2026-03-12 session 64): first checkpoint landed. `CalculatePathRequest.nearby_objects` now exists, protobuf code was regenerated, `PathfindingClient` has a compatibility overload that can send request-scoped dynamic objects, `HandlePath(...)` logs overlay counts for diagnostics, and focused proto/client unit tests pass.
- [x] Progress (2026-03-12 session 65): BotRunner now populates `nearby_objects` from live `NavigationPath` calls via `PathfindingOverlayBuilder`. The caller-side filter is conservative (`40y` from start/end, collidable types only, nearest `64` max), and focused BotRunner unit tests prove overlay filtering plus request forwarding.
- [ ] Required change:
  1. First checkpoint: extend `CalculatePathRequest` with a request-scoped world overlay payload: `repeated DynamicObjectProto nearby_objects`, regenerate protobuf code, and prove the contract with passing unit tests.
  2. Second checkpoint: add caller capability/options fields for path shaping and decision use, such as `allow_step_up`, `allow_jump_gap`, `allow_safe_drop`, `allow_swim`, `request_affordance_metadata`, and `agent_radius/height` when race defaults are not enough.
  3. Third checkpoint: extend `CalculatePathResponse` so the caller gets more than corners: path result, blocked-reason metadata, and segment affordance data.
- [ ] Acceptance criteria: BotRunner can send a live game-object list with each path request, and the response schema can explain why a path is valid, blocked, or requires a movement affordance.

### PFS-OBJ-002 Service overlay lifecycle: request-scoped dynamic obstacle context
- [ ] Problem: the existing `DynamicObjectRegistry` is fed by physics ticks, but path requests do not yet mount a request-scoped overlay for live obstacle-aware route validation.
- [ ] Target files: `Services/PathfindingService/PathfindingSocketServer.cs`, `Services/PathfindingService/Repository/Navigation.cs`, `Services/PathfindingService/Repository/Physics.cs`.
- [x] Ready signal (2026-03-12 session 65): caller-side overlay plumbing is now live, so this task is the next implementation slice. The service no longer needs more BotRunner contract work before it can start registering and clearing request-scoped overlays.
- [x] Progress (2026-03-12 session 66): `CalculatePathRequest.nearby_objects` is now mounted via `RequestScopedDynamicObjectOverlay`, which registers synthetic GUIDs, forwards `goState`, logs overlay counts/display IDs, and unregisters those synthetic objects in `finally`. Registry-sensitive native calls (`HandlePhysics`, `HandleLineOfSight`, `HandleGroundZ`, `HandleBatchGroundZ`, `HandleSegmentDynCheck`) now run behind the same gate so the singleton registry cannot leak mounted caller overlays across requests.
- [ ] Required change:
  1. Convert path/LOS/path-validation requests to register and clear a request-scoped overlay from `nearby_objects`.
  2. Keep transport and long-lived world geometry support intact while avoiding registry leakage between requests.
  3. Add diagnostics that report object counts, filtered object counts, and the collidable display IDs used during a path request.
- [ ] Remaining gap:
  1. `CalculatePathRequest` is currently the only request type that carries `nearby_objects`, so standalone LOS / GroundZ / SegmentDynCheck requests are serialized but not yet overlay-aware themselves.
  2. Raw native routes are still returned without overlay-aware segment validation or repair.
- [ ] Acceptance criteria: each path request sees exactly the live obstacles provided by the caller, non-path registry-sensitive requests cannot observe leaked overlay state, and path results are reproducible per request without cross-bot contamination.

### PFS-OBJ-003 Overlay-aware path validation and repair
- [ ] Problem: native `FindPath` returns mmap routes, but there is no service-level repair loop that rejects corners/segments crossing collidable live objects and reforms the path around them.
- [ ] Target files: `Services/PathfindingService/Repository/Navigation.cs`, `Services/PathfindingService/PathfindingSocketServer.cs`, tests under `Tests/PathfindingService.Tests/`.
- [ ] Required change:
  1. Validate each returned path segment against dynamic objects, capsule clearance, LOS, and support surface checks.
  2. When a segment is blocked by live objects, build a local detour/reform pass around the obstruction instead of returning the raw native path.
  3. Re-optimize the repaired route so the caller gets a usable waypoint chain instead of a one-off avoidance spike.
- [ ] Acceptance criteria: a path that would clip through a live collidable object is either reformed into a valid route or returned with an explicit blocked reason; raw invalid corners are no longer silently trusted.

### PFS-OBJ-004 Affordance metadata: step, jump, drop, swim, blocked
- [ ] Problem: the service knows about ground Z, LOS, falling, and gap detection, but a caller cannot currently ask "is this route walkable vs requiring a jump/drop/step-up?"
- [ ] Target files: `Services/PathfindingService/Repository/Navigation.cs`, `Services/PathfindingService/Repository/Physics.cs`, protobuf contract, `Tests/PathfindingService.Tests/`.
- [ ] Required change:
  1. Classify each segment or transition as `walk`, `step_up`, `jump_gap`, `safe_drop`, `unsafe_drop`, `swim`, or `blocked`.
  2. Surface quantitative metadata such as climb height, gap length, drop height, clearance, slope, and support confidence.
  3. Keep the rules compatible with existing native constants (`STEP_HEIGHT`, jump velocity, fall-distance tracking, gap detection) instead of inventing a second physics model in C#.
- [ ] Acceptance criteria: BotRunner can reject or prefer routes based on movement affordances instead of only corner count and LOS.

### PFS-OBJ-005 Decision-grade spatial queries
- [ ] Problem: the advanced decision system needs more than start->end pathing; it needs queries like "nearest reachable LOS position" and "best nearby valid surface for interaction."
- [ ] Target files: `pathfinding.proto`, `PathfindingSocketServer.cs`, `Repository/Navigation.cs`, future BotRunner consumers.
- [ ] Required change:
  1. Add follow-on request types after the base path contract is stable, such as `FindReachablePositionInRadius`, `FindLosPositionForTarget`, and `EvaluateRoute`.
  2. Reuse the same object-aware overlay and affordance metadata from `PFS-OBJ-001..004`.
  3. Keep the first slice narrow: build these only after object-aware path requests are stable.
- [ ] Acceptance criteria: decision systems can query reachability/LOS/surface options without re-implementing path safety logic inside BotRunner.

### PFS-OBJ-006 Validation matrix: deterministic + live replay
- [ ] Problem: object-aware routing will regress quietly unless it is covered by deterministic route fixtures and live collision scenarios.
- [ ] Target files: `Tests/PathfindingService.Tests/`, `Tests/BotRunner.Tests/LiveValidation/`, route replay fixtures/logs.
- [ ] Required change:
  1. Add deterministic service tests for blocked-by-object, repaired-detour, step-up, jump-gap, and safe-drop classification.
  2. Add live validation scenarios that consume the new route metadata, starting with Ratchet shoreline and any existing temporary-object blockers.
  3. Capture planned-vs-executed route evidence so failures can be attributed to service route shape vs runtime execution drift.
- [ ] Acceptance criteria: each new object-aware routing capability has both a deterministic test and at least one live integration contract.

## Simple Command Set
1. Build service: `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore`
2. Pathfinding tests: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
3. Corpse-run test: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-03-12 (session 66)
- Active task: `PFS-OBJ-003` overlay-aware path validation and repair
- Last delta: service-side request-scoped overlay lifecycle is now in place for `CalculatePathRequest.nearby_objects`, with synthetic-guid registration, `finally` unregister cleanup, and a shared gate that keeps registry-sensitive native calls from observing leaked overlay state
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Services/PathfindingService/PathfindingService.csproj --configuration Release --no-restore` -> succeeded
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> succeeded
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~RequestScopedDynamicObjectOverlayTests|FullyQualifiedName~ProtoInteropExtensionsTests" --logger "console;verbosity=minimal"` -> `15 passed`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests" --logger "console;verbosity=minimal"` -> `4 passed`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `29 passed`
- Files changed:
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Services/PathfindingService/Repository/RequestScopedDynamicObjectOverlay.cs`
  - `Services/PathfindingService/README.md`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/PathfindingService.Tests/RequestScopedDynamicObjectOverlayTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
- Next command: `Get-Content Services/PathfindingService/Repository/Navigation.cs`
- Blockers: the service now mounts path overlays, but raw native routes are still returned without overlay-aware segment validation or detour repair; standalone non-path requests also still lack their own `nearby_objects` payload.
