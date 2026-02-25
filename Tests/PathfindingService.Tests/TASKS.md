# PathfindingService.Tests Tasks

## Scope
- Directory: `Tests/PathfindingService.Tests`
- Project: `PathfindingService.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-024`)
- Local goal: verify pathfinding outputs are valid and consumed deterministically for corpse runback, combat movement, and gathering travel parity.

## Execution Rules
1. Execute tasks in numeric order unless blocked by missing data or fixture prerequisites.
2. Keep scan scope to this project path and directly referenced implementation files only.
3. Use one-line `dotnet test` commands and include `test.runsettings` for timeout enforcement.
4. Never blanket-kill `dotnet`; use repo-scoped process cleanup only and record evidence.
5. Move completed IDs to `Tests/PathfindingService.Tests/TASKS_ARCHIVE.md` in the same session.
6. If two consecutive passes produce no file delta, record blocker and exact next command, then advance to the next queue file in `docs/TASKS.md`.
7. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist (Run Before P0)
- [ ] `Navigation.dll` is present in test output (`Tests/PathfindingService.Tests/bin/Release/net8.0/Navigation.dll` currently missing in this shell session).
- [ ] `WWOW_DATA_DIR` (if set) resolves to nav data root and contains required nav folders (currently unset in this shell session).
- [x] `Tests/PathfindingService.Tests/test.runsettings` is used (10-minute `TestSessionTimeout`).

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/PathfindingService.Tests/PathfindingService.Tests.csproj` -> up-to-date.
- `Tests/PathfindingService.Tests/test.runsettings:6` -> `<TestSessionTimeout>600000</TestSessionTimeout>`.
- `NavigationFixture.cs` preflight evidence confirms current gap tracked by `PFS-TST-001`:
  - current hard checks focus on `mmaps/` and `.mmtile` (`:50-56`, `:67-85`)
  - message mentions `maps/` and `vmaps/` without explicit `Directory.Exists` validation for those folders.
- `README.md` command surface drift confirmed for `PFS-TST-006`:
  - sample commands at `:90`, `:93`, `:96` omit explicit `--settings Tests/PathfindingService.Tests/test.runsettings`.
- Live validation on current handoff command confirms a discovery gap:
  - `dotnet test ... --filter "FullyQualifiedName~NavigationFixture|FullyQualifiedName~Preflight"` -> `No test matches the given testcase filter`.
- `dotnet test ... --list-tests` currently exposes only:
  - `PathfindingBotTaskTests`
  - `PathfindingTests`
  - `PhysicsEngineTests`
  - `ProtoInteropExtensionsTests`

## P0 Active Tasks (Ordered)

### [ ] PFS-TST-001 - Strengthen nav data preflight to validate full data root
- Problem: preflight currently guarantees `mmaps/` and `.mmtile` files but does not explicitly verify `maps/` and `vmaps/`.
- Evidence:
1. `Tests/PathfindingService.Tests/NavigationFixture.cs:43`
2. `Tests/PathfindingService.Tests/NavigationFixture.cs:51`
3. `Tests/PathfindingService.Tests/NavigationFixture.cs:80`
- Implementation targets:
1. `Tests/PathfindingService.Tests/NavigationFixture.cs`
2. `Tests/PathfindingService.Tests` (new preflight-focused test file if needed)
- Required change:
1. Resolve a single data root from `WWOW_DATA_DIR` or assembly output fallback.
2. Validate `maps/`, `vmaps/`, and `mmaps/` directories exist under that root.
3. Keep `.mmtile` presence check and include resolved root path in failure text.
4. Add a deterministic test for preflight error text using temporary directories.
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationFixture|FullyQualifiedName~Preflight" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Missing data roots fail early with explicit directory names and resolved path.
2. Fixture behavior is deterministic across local and CI runs.
3. The preflight-focused filter command executes at least one discovered test (no zero-match result).

### [ ] PFS-TST-002 - Convert baseline path assertions to full route validity contract
- Problem: baseline path test only asserts `NotEmpty`, which is too weak to catch wall-running or malformed route segments.
- Evidence:
1. `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs:11`
2. `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs:20`
3. `Tests/PathfindingService.Tests/BotTasks/PathCalculationTask.cs:49`
4. `Tests/PathfindingService.Tests/BotTasks/PathSegmentValidationTask.cs:48`
- Implementation targets:
1. `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs`
2. `Tests/PathfindingService.Tests/BotTasks/PathCalculationTask.cs`
3. `Tests/PathfindingService.Tests/BotTasks/PathSegmentValidationTask.cs`
4. `Tests/PathfindingService.Tests` (shared path assertion helper file)
- Required change:
1. Assert waypoint count is at least 2.
2. Assert start and end waypoint proximity thresholds.
3. Assert no zero-length segments.
4. Assert segment horizontal distance and height deltas remain within deterministic thresholds.
5. Emit failing segment index and coordinates for triage.
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Invalid route shapes fail with actionable diagnostics.
2. Route validity checks are centralized and reused across tests and bot tasks.

### [ ] PFS-TST-003 - Add blocked-corridor reroute regression for wall-avoidance behavior
- Problem: suite has blocked line-of-sight coverage but lacks a path reroute assertion proving generated paths route around barriers.
- Evidence:
1. `Tests/PathfindingService.Tests/PhysicsEngineTests.cs:98`
2. `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs:11`
- Implementation targets:
1. `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs` (or new `CorpseRunRouteTests.cs`)
- Required change:
1. Add deterministic start/end pair where direct line is blocked.
2. Assert calculated path contains intermediate waypoints that deviate from straight-line direct segment.
3. Assert destination proximity and segment validity after reroute.
4. Record map and waypoint list in assertion message on failure.
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~CorpseRunRoute" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Wall-impact regressions fail before bot-level corpse tests are run.
2. Passing output demonstrates valid detour route generation.

### [ ] PFS-TST-004 - Expand proto/native contract tests with round-trip invariants
- Problem: one-way mappings are covered, but round-trip invariants and edge values are not explicitly gated.
- Evidence:
1. `Tests/PathfindingService.Tests/ProtoInteropExtensionsTests.cs:12`
2. `Tests/PathfindingService.Tests/ProtoInteropExtensionsTests.cs:105`
- Implementation targets:
1. `Tests/PathfindingService.Tests/ProtoInteropExtensionsTests.cs`
- Required change:
1. Add round-trip test (`proto -> native -> proto`) for full physics input/output field set.
2. Add edge-value checks for transport, standing-on fields, and physics flags.
3. Keep float comparisons deterministic with explicit tolerances where conversion precision is expected.
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~ProtoInteropExtensionsTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Any contract drift between C++, protobuf, and C# fails in this project first.
2. Failure output identifies exact field mismatch.

### [ ] PFS-TST-005 - Keep corpse-run path validation aligned with Orgrimmar runback scenario
- Problem: bot tasks validate Durotar sample routes, but no direct route-validation case mirrors the corpse-run runback corridor used by bot-level tests.
- Evidence:
1. `Tests/PathfindingService.Tests/BotTaskTests.cs:16`
2. `Tests/PathfindingService.Tests/BotTasks/PathCalculationTask.cs:24`
3. `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` (Orgrimmar teleport flow)
- Implementation targets:
1. `Tests/PathfindingService.Tests/BotTasks` (new Orgrimmar-focused task)
2. `Tests/PathfindingService.Tests/BotTaskTests.cs`
- Required change:
1. Add deterministic Orgrimmar runback route validation inputs used by corpse-run flow.
2. Reuse shared route-validity assertions from `PFS-TST-002`.
3. Ensure this test runs in under the same 10-minute project timeout budget.
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Pathfinding regressions for corpse runback are caught before end-to-end bot tests.
2. New test is stable across repeated runs.

### [ ] PFS-TST-006 - Simplify and enforce command surface with timeout-aware defaults
- Problem: project README commands do not consistently include runsettings and timeout-safe defaults.
- Evidence:
1. `Tests/PathfindingService.Tests/README.md:90`
2. `Tests/PathfindingService.Tests/test.runsettings:6`
- Implementation targets:
1. `Tests/PathfindingService.Tests/README.md`
2. `Tests/PathfindingService.Tests/TASKS.md`
- Required change:
1. Replace ambiguous commands with canonical one-line commands using `--settings Tests/PathfindingService.Tests/test.runsettings`.
2. Include focused filter commands for route, physics, and proto tests.
3. Add repo-scoped cleanup command reference for stale test processes.
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`.
- Acceptance:
1. Engineers can run all or focused suites with one-line commands.
2. Timeout behavior is consistent and documented.

## Simple Command Set
1. Full project sweep: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`.
2. Route validity focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`.
3. Physics/LOS focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsEngineTests" --logger "console;verbosity=minimal"`.
4. Proto contract focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~ProtoInteropExtensionsTests" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-02-25
- Active task: `PFS-TST-001`
- Last delta: added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction always resumes on the next local `TASKS.md`.
- Pass result: delta shipped
- Last command run: `Get-Content -Path 'Tests/PathfindingService.Tests/TASKS.md' -TotalCount 460`
- Validation result: `PFS-TST-001` remains open; preflight filter currently matches zero discovered tests and fixture still hard-checks `mmaps`/`.mmtile` only.
- Files changed: `Tests/PathfindingService.Tests/TASKS.md`
- Blockers: `WWOW_DATA_DIR` unset and `Navigation.dll` absent from this project's `bin/Release/net8.0` output in current shell session.
- Next task: `PFS-TST-001`
- Next command: `Get-Content -Path 'Tests/PromptHandlingService.Tests/TASKS.md' -TotalCount 360`.
