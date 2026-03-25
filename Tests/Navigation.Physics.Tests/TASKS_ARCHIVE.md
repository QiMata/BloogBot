# Task Archive

Completed items moved from TASKS.md.

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
