# Navigation.Physics.Tests Tasks

## Scope
- Directory: `Tests/Navigation.Physics.Tests`
- Project: `Navigation.Physics.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-023`)
- Local goal: make physics parity regressions deterministic, actionable, and fast to validate.

## Execution Rules
1. Execute tasks in numeric order unless blocked by missing fixture/data.
2. Keep every validation command one-line and runnable without custom wrappers.
3. Use `test.runsettings` for hard timeout enforcement on every command.
4. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
5. Archive completed IDs to `Tests/Navigation.Physics.Tests/TASKS_ARCHIVE.md` in the same session.
6. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
7. Start each pass by running the previous `Session Handoff -> Next command` verbatim before any broader scan.
8. After shipping one local delta, set `Next command` to the next queue-file read command so one-by-one progression survives compaction.

## Environment Checklist (Run Before P0)
- [x] `Navigation.dll` is present for this test project (`Bot/Release/x64/Navigation.dll`).
- [ ] `WWOW_DATA_DIR` resolves to a root containing `maps/`, `vmaps/`, and `mmaps/`.
- [x] `Tests/Navigation.Physics.Tests/test.runsettings` is used (10-minute `TestSessionTimeout`).

## Evidence Snapshot (2026-02-25)
- `dotnet restore Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj` -> up-to-date.
- `Tests/Navigation.Physics.Tests/test.runsettings:6` -> `<TestSessionTimeout>600000</TestSessionTimeout>`.
- `FrameByFramePhysicsTests.cs` still contains placeholder simulation path:
  - `:380` `// TODO: Call actual physics`
  - `:381` commented `StepPhysicsV2` invocation
  - `:373` `SimulatePhysics(...)` entrypoint.
- `MovementControllerPhysicsTests.cs:123` contains `TeleportRecovery_StopsFreeFall`, confirming `NPT-MISS-002` target location.
- Environment probe: `WWOW_DATA_DIR` is unset in this shell session; data-root checklist item remains open.

## P0 Active Tasks (Ordered)

### [ ] NPT-MISS-001 - Replace placeholder simulation loop with real native stepping
- Problem: `SimulatePhysics` currently builds synthetic frames and never calls the C++ physics step.
- Evidence: `// TODO: Call actual physics` in `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs` around line 380.
- Implementation targets:
1. `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
2. `Tests/Navigation.Physics.Tests/NavigationInterop.cs` (`StepPhysicsV2`, `PhysicsInput`, `PhysicsOutput`)
- Required change:
1. Call `StepPhysicsV2(ref input)` on every frame.
2. Store `PhysicsOutput` per frame in the `PhysicsFrame` record.
3. Feed output state (position and velocity) into the next `PhysicsInput`.
4. Add finite-value checks to fail fast on invalid output.
- Command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. No placeholder-only path remains in `SimulatePhysics`.
2. Frame-by-frame assertions are driven by native physics output.
3. Failures include frame index and expected vs actual physics state.

### [ ] NPT-MISS-002 - Add teleport airborne descent assertions to catch hover regression
- Problem: existing teleport recovery test guards against falling through the world but does not fail on hover.
- Evidence: `TeleportRecovery_StopsFreeFall` only asserts final Z safety window in `MovementControllerPhysicsTests.cs`.
- Implementation targets:
1. `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs`
- Required change:
1. Add a dedicated airborne teleport scenario with start point above terrain.
2. Assert post-teleport per-frame descent trend (Z decreases across initial frames).
3. Assert landing settles within expected ground window after descent.
4. Emit frame-by-frame Z/velocity in assertion messages for triage.
- Command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. BG hover behavior fails deterministically.
2. Corrected fall behavior passes with bounded landing frame window.
3. Assertion output is specific enough to debug one failing frame set.

### [ ] NPT-MISS-003 - Add hard drift gate for replay/controller parity
- Problem: diagnostics report drift but there is no strict gate to block regressions.
- Evidence: replay tests log detailed metrics but not all key drift metrics are merge-blocking assertions.
- Implementation targets:
1. `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
2. `Tests/Navigation.Physics.Tests/Diagnostics/ErrorPatternDiagnosticTests.cs`
- Required change:
1. Define explicit thresholds for overall average, steady-state p99, and worst clean-frame error.
2. Fail tests when any threshold is exceeded.
3. Print recording name, frame index, and XYZ error vector for top offenders.
4. Keep artifact/transport/teleport exclusions explicit in assertions.
- Command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests|FullyQualifiedName~ErrorPatternDiagnosticTests" --logger "console;verbosity=minimal"`.
- Acceptance:
1. Drift threshold breaches fail the build.
2. Failure output identifies exact frames to replay.
3. Clean-frame and artifact-frame handling are clearly separated.

## Simple Command Set
1. Single project sweep: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`.
2. Fast frame-loop verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`.
3. Teleport/fall verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`.
4. Drift diagnostics gate: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests|FullyQualifiedName~ErrorPatternDiagnosticTests" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-02-25
- Active task: `NPT-MISS-001`
- Last delta: added explicit one-by-one continuation rules (`run prior Next command first`, `set next queue-file command after delta`) to prevent rediscovery loops after compaction.
- Pass result: delta shipped
- Last command run: `rg --line-number "TODO: Call actual physics|SimulatePhysics|StepPhysicsV2" Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
- Validation result: `NPT-MISS-001` still open (placeholder path remains at `FrameByFramePhysicsTests.cs:380-381`).
- Files changed: `Tests/Navigation.Physics.Tests/TASKS.md`
- Blockers: `WWOW_DATA_DIR` not set in this shell session (required for full physics-data environment validation).
- Next task: `NPT-MISS-001`
- Next command: `Get-Content -Path 'Tests/PathfindingService.Tests/TASKS.md' -TotalCount 360`.
