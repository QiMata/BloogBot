# Task Archive

Completed items moved from TASKS.md.

## Completed 2026-04-15

### [x] Master D4 elevator deferral revalidation
- **Problem:** `docs/TASKS.md` still listed stale deferred issue `D4` for two pre-existing Navigation.Physics elevator failures even though this owner already showed no remaining physics parity backlog.
- **Solution:**
  - Read `docs/physicsengine-calibration.md` before touching the physics deferral, per the calibration anti-loop rule.
  - Rebuilt the current Release x64 `Navigation.dll`.
  - Re-ran the Docker scene-data movement parity bundle.
- **Result:**
  - `Category=MovementParity` passed `8/8`, including the compact packet-backed Undercity elevator replay coverage.
  - The master `D4` deferred row is closed by current evidence.
- **Validation:**
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`

## Completed 2026-04-12

### [x] NPT-MISS-004 - Add deterministic steep-incline rejection proof before pathfinding follow-up
- **Problem:** The managed `MovementController` no longer owns local slope guards, so the repo lacked a direct proof that steep uphill faces are still rejected before spending more time on pathfinding.
- **Solution:**
  - Added `SegmentWalkabilityTests.ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment`.
  - The new regression scans the steep-slope sweep corpus (`Un'Goro`, `Desolace`, `Thousand Needles`) and requires at least one real uphill segment to fail native validation as `StepUpTooHigh` or `BlockedGeometry`.
  - Revalidated both positive and negative slope expectations with the existing uphill controller parity test and the server-side climb-angle gate.
- **Result:**
  - The steep-incline rejection proof is now deterministic and green.
  - Walkable uphill travel still passes, so the new regression is not hiding normal slope traversal.
- **Files:**
  - `Tests/Navigation.Physics.Tests/SegmentWalkabilityTests.cs`

## Completed 2026-04-12

### [x] Compact packet-backed Undercity elevator replay parity closeout
- **Problem:** Deterministic Docker-backed movement parity was blocked only by the compact packet-backed Undercity elevator replay after the long V2 replay was already green.
- **Solution:**
  - Fixed `NavigationInterop.cs` to prefer the freshly built root `Navigation.dll` over the stale `x64\Navigation.dll` fallback.
  - Added frame-window diagnostics in `PacketBackedUndercityElevatorSupportTests.cs` that proved the corrected runtime keeps `groundedWallState=1` and a support token through frames `10..19`, while the remaining worst replay frame was a nonzero-to-nonzero `TransportGuid` swap on frame `20`.
  - Updated `ReplayEngine.cs` so replay treats nonzero-to-nonzero `TransportGuid` changes as transport transitions instead of steady-state on-transport frames.
- **Result:**
  - Focused compact transport slice passed (`3/3`).
  - Deterministic Docker-backed `Category=MovementParity` bundle passed (`8/8`).
- **Files:**
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`

## Completed 2026-02-26

### [x] NPT-MISS-001 - Replace placeholder simulation loop with real native stepping
- **Problem:** `SimulatePhysics` built synthetic frames and never called the C++ physics step.
- **Solution:** Rewrote all 10 test methods in `FrameByFramePhysicsTests.cs`:
  - Replaced `[Fact(Skip = ...)]` with runtime `Skip.If(!_fixture.IsInitialized, ...)` + `[Collection("PhysicsEngine")]`
  - Implemented real physics test bodies: FlatGround, RoadTraversal, WalkDownSlope, StandingJump, RunningJump, FreeFall, WallCollision, WaterTransition, IndoorCeiling, Idle
  - Fixed FallTime double-conversion bug (output already in ms, was multiplied by 1000 again)
  - Fixed velocity accumulation (zeroed Vx/Vy in SimulatePhysics loop)
  - Fixed C# MoveFlags naming mismatch: `MoveFlags.Falling` (0x4000) = C++ `MOVEFLAG_FALLINGFAR`
- **Result:** 10/10 tests pass
- **Files:** `FrameByFramePhysicsTests.cs`

### [x] NPT-MISS-002 - Add teleport airborne descent assertions to catch hover regression
- **Problem:** Existing teleport recovery test only checked final Z safety window, not per-frame descent.
- **Solution:** Added `TeleportAirborne_DescentTrend_ZDecreasesPerFrame` test with 4 assertions:
  1. Per-frame descent trend: >=5/10 early frames must show Z decrease
  2. Total descent > 1y in first 10 frames
  3. Ground contact (gap <= 2.5y) within 60 frames
  4. No through-world failure (Z > -50)
- **Result:** 7/7 MovementControllerPhysicsTests pass
- **Files:** `MovementControllerPhysicsTests.cs`

### [x] NPT-MISS-003 - Add hard drift gate for replay/controller parity
- **Problem:** Diagnostics reported drift but no strict gate blocked regressions.
- **Solution:** Added 2 new gating tests + 3 new tolerance constants:
  1. `AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds` in PhysicsReplayTests:
     - Clean avg < 0.15y, P99 < 2.0y, worst < 5.0y
     - Reports top 10 offenders with recording name, frame index, XYZ error vector
  2. `DriftGate_PerMode_CleanFramesWithinThresholds` in ErrorPatternDiagnosticTests:
     - Per-mode thresholds (ground, air, swim, transition, transport)
     - Reports top 3 offenders per mode
  3. Constants in `Tolerances`: `AggregateCleanAvg=0.15`, `AggregateCleanP99=2.0`, `WorstCleanFrame=5.0`
- **Result:** Both tests pass. Clean artifacts/SPLINE_ELEVATION excluded explicitly.
- **Files:** `PhysicsReplayTests.cs`, `ErrorPatternDiagnosticTests.cs`, `Helpers/TestConstants.cs`
