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
10. For object-aware pathfinding coverage, every test slice must land with passing focused tests, updated task docs, a committed+pushed branch checkpoint, and a handoff that names the next implementation command explicitly.

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

### [x] PFS-TST-001 - Strengthen nav data preflight to validate full data root
- **Done (batch 17).** `VerifyNavDataExists()` now validates `maps/`, `vmaps/`, and `mmaps/` in one pass with aggregated missing-dir listing. Single resolved root path shared across env-var and fallback paths.
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

### [x] PFS-TST-004 - Expand proto/native contract tests with round-trip invariants
- **Done (batch 17).** 5 new tests: transport field round-trip, standing-on field round-trip, fall/liquid output preservation, zero-transport edge case, zero-standing-on edge case. Total: 11 proto tests pass.
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

### [x] PFS-TST-006 - Simplify and enforce command surface with timeout-aware defaults
- **Done (batch 17).** README.md updated: 4 canonical commands with `--configuration Release --no-restore --settings --logger`, fixed stale filter (`PathCalculationTests` → `PathfindingTests`), added timeout explanation.
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

### [x] PFS-TST-007 - Add request-scoped overlay lifecycle coverage
- **Done (session 66).** Added `RequestScopedDynamicObjectOverlayTests` to verify valid object register/update/unregister, invalid-object filtering, `goState` forwarding, exception-safe cleanup, and gate serialization between overlay work and other registry-sensitive calls.
- Evidence:
1. `Tests/PathfindingService.Tests/RequestScopedDynamicObjectOverlayTests.cs`
2. `Services/PathfindingService/Repository/RequestScopedDynamicObjectOverlay.cs`
3. `Services/PathfindingService/PathfindingSocketServer.cs`
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~RequestScopedDynamicObjectOverlayTests|FullyQualifiedName~ProtoInteropExtensionsTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Overlay lifecycle regressions fail in deterministic tests before live bot routing is exercised.
2. Test output proves overlay cleanup still happens when the wrapped native call fails.

### [x] PFS-TST-008 - Add deterministic overlay-aware path validation/repair coverage
- **Done (session 67, expanded session 68).** Added `NavigationOverlayAwarePathTests` to verify alternate-mode recovery when the preferred route is blocked, bounded detour repair around a blocked segment, explicit `blocked_by_dynamic_overlay` failure when no repair candidate works, `repaired_segment_validation`, and explicit step-limit result mapping.
- Evidence:
1. `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
2. `Services/PathfindingService/Repository/Navigation.cs`
3. `Services/PathfindingService/PathfindingSocketServer.cs`
- Command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Service-level overlay validation/repair regressions fail deterministically before live collision scenarios are exercised.
2. The deterministic suite now covers `native_path_alternate_mode`, `repaired_dynamic_overlay`, and `blocked_by_dynamic_overlay` results.

## Simple Command Set
1. Full project sweep: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`.
2. Route validity focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`.
3. Physics/LOS focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsEngineTests" --logger "console;verbosity=minimal"`.
4. Proto contract focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~ProtoInteropExtensionsTests" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-03-12 (session 68)
- Active task: PFS-TST-002/003/005 remain open; `PFS-TST-008` now also covers native walkability result mapping
- Last delta: expanded `NavigationOverlayAwarePathTests` with non-dynamic repair/result-mapping cases for the new native segment-validation bridge, then revalidated the full `PathfindingService.Tests` project with default rollout still disabled
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1` -> succeeded
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathfindingTests" --logger "console;verbosity=minimal"` -> `9 passed`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `34 passed`
- Files changed:
  - `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Services/PathfindingService/Repository/Navigation.cs`
- Blockers: PFS-TST-002/003/005 still depend on stronger shared route-validity assertions and live nav-data route evidence; the new native segment-validation bridge is present, but its support-surface probe is still gated off by default for long routes.
- Next command: `Get-Content Exports/Navigation/SceneQuery.cpp | Select-Object -Skip 520 -First 260`
