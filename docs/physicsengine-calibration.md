# PhysicsEngine Replay Calibration Log

This file is the handoff ledger for `PhysicsEngine` replay calibration.
Only this test is in scope:

- `PathfindingService.Tests.PhysicsEngineTests.StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance`

## Mandatory Preflight (Do This Before Any New Edit)

1. Read this file fully, top to bottom.
2. Check the latest run logs in `logs/` and confirm the best known metrics and last regression.
3. Verify your planned code edit is not already listed under "Do Not Repeat".
4. Make exactly one PhysicsEngine behavior change per run.
5. Append the run result (metrics + frame-pattern notes + log file name) immediately after the run.

If steps 1-3 are skipped, calibration work will likely repeat failed attempts.

## Current Scope Guardrails

- Focus only on `PhysicsEngine` calibration.
- Do not tune `MovementController` in this stream.
- Keep horizontal replay lock behavior intact unless explicitly changing that hypothesis.
- Always keep frame-by-frame trace logging enabled in test output.

## Current Handoff Snapshot (2026-02-25)

- Best observed run in this stream:
  - Log: `logs/physicsengine-variance-20260225-run1.txt`
  - `simulated=248 airborne=144 skippedDt=0 skippedTeleport=0`
  - `avg=0.0054 p95=0.0359 p99=0.0452 max=0.0490 (frame=247)`
- Current dominant error bands in best run:
  - Positive Z band: `f=207..213`, up to `dZ=+0.0407`
  - Negative Z tail: `f=230..247`, down to `dZ=-0.0490`
  - Horizontal error: effectively `0` across worst frames.
- Last attempted adjustment regressed mean error:
  - Log: `logs/physicsengine-variance-20260225-run2.txt`
  - `avg` worsened from `0.0054` to `0.0062`
  - `p95/p99/max` unchanged (`0.0359/0.0452/0.0490`)

## Adjustment History (Chronological)

### A. Baseline before replay-trust calibration in this pass

- Result:
  - `avg=0.0407 p95=0.1411 p99=0.1492 max=0.1707 (frame=45)`
- Main issue:
  - systematic grounded replay mismatch.

### B. Replay-trust overshoot regression

- Result:
  - `avg=0.0596 p95=0.2863 p99=0.2952 max=0.2995 (frame=226)`
- Main issue:
  - non-walkable slope over-lift (`dZ=+0.28..+0.30`) late in run.

### C. Clamp grounded trust-refine `chosenZ` around replay input

- Change:
  - grounded trust-refine clamp around replay `input.z`.
- Result:
  - `avg=0.0188 p95=0.0852 p99=0.1129 max=0.1277 (frame=45)`
- Impact:
  - removed the large late overshoot latch.

### D. Additional replay-trust tuning (current code lineage)

- Result (best):
  - `logs/physicsengine-variance-20260225-run1.txt`
  - `avg=0.0054 p95=0.0359 p99=0.0452 max=0.0490`
- Remaining issue:
  - Z lag on two bands (`f=207..213`, `f=230..247`).

### E. Failed tweak (Do Not Repeat As-Is)

- Change:
  - In `Exports/Navigation/PhysicsEngine.cpp` non-walkable support guardrail block,
    replaced prior trend handling with geometry-sampled support trend:
    `currentSupportZ/nextSupportZ` via `SceneQuery::GetGroundZ`.
- Result:
  - `logs/physicsengine-variance-20260225-run2.txt`
  - `avg` regressed from `0.0054` to `0.0062`, tails unchanged.
- Why it failed:
  - introduced broader small positive bias on grounded moving frames without reducing tail max error.

## Do Not Repeat

- Do not reapply the geometry-derived support-trend replacement from run2 as-is.
- Do not let step-up persistence re-promote a pre-refinement overhead surface after replay-ground refinement has already clamped back to the captured floor.
- Do not run multiple simultaneous hypotheses in one test pass.
- Do not recalibrate by re-scanning unrelated systems (MovementController/task-wide state) for this stream.

## Recommended Next Single Hypothesis

Target only non-walkable replay guardrail behavior in `PhysicsEngine.cpp`:

- Keep existing replay-horizontal lock behavior.
- Revert the run2 trend-resolution replacement.
- Apply one narrow slope-conditioned clamp tweak for late under-lift band (`f=230..247`) only.
- Re-run the single test and compare against run1/run2 metrics and frame bands.

## Commands

Run the single calibration test:

```powershell
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName=PathfindingService.Tests.PhysicsEngineTests.StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance" --logger "console;verbosity=detailed"
```

Suggested log capture:

```powershell
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName=PathfindingService.Tests.PhysicsEngineTests.StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance" --logger "console;verbosity=detailed" | Tee-Object logs/physicsengine-variance-YYYYMMDD-runN.txt
```

## 2026-03-23 Transport Parity Addendum

- Scope note:
  - This pass targeted transport/elevator replay parity rather than the original fall-from-height replay.
  - The same calibration hygiene rules still applied because the failure was in `PhysicsEngine` replay behavior.
- Behavioral change shipped:
  - `PhysicsEngine.cpp`: replay step-up persistence now refuses to persist `preSafetyNetZ` when replay-ground refinement disagrees by more than `0.5y`, which prevents transport-exit overhead surfaces from being held for several frames.
  - `ReplayEngine.cs`: all board/leave/teleport skips now reset `FallStartZ` and `StepUpBaseZ` to `INVALID_HEIGHT` instead of zero.
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ElevatorRideV2_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=detailed"`
    - `ElevatorRideV2_FrameByFrame_PositionMatchesRecording`: `avg=0.0142y`, `steady-state p99=0.1190y`, `max=0.3619y`
    - `UndercityElevatorReplay_TransportAverageStaysWithinParityTarget`: `transport avg=0.0303y`, `p99=0.2169y`, `max=0.3619y`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=detailed"`
    - aggregate clean metrics: `avg=0.0124y`, `p99=0.1279y`, `worst=2.2577y`
- Frame-pattern note:
  - The large post-disembark Undercity spike (`sim Z≈61.656` vs `rec Z≈55.242`) was not missing geometry.
  - `GetGroundZ` already returned the correct floor when queried near foot level; the regression came from a higher pre-refinement surface being persisted after the floor had already been corrected.

## 2026-03-23 Swim Parity Addendum

- Scope note:
  - This pass targeted the remaining WoW.exe swim-path parity gap at `CMovement::CollisionStep` swim branch `0x633B5E`.
  - The old native swim path still integrated pitch-based velocity directly and had no submerged terrain/WMO collision.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsMovement.cpp`
    - swim movement now resolves against geometry using submerged collide-and-slide instead of raw position integration
    - the WoW.exe swim-branch half-displacement constant (`0.5f`, `VA 0x007FFA24`) is now applied as two half-step swim collision substeps
    - start-of-frame submerged overlaps now receive a bounded depenetration pass before swim motion
  - `Exports/Navigation/PhysicsEngine.cpp`
    - water-entry damping now survives into the frame output velocity on the entry frame instead of mutating only the carried state
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release`
    - succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.DurotarRecording_WaterEntry_DampsHorizontalVelocity|FullyQualifiedName~FrameByFramePhysicsTests.DurotarSwimDescent_SeabedCollisionPreventsTerrainPenetration|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" -v n`
    - passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n`
    - passed (`29/29`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.WestfallCoast_EnterWater_TransitionsToSwimming" -v n`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" -v n`
    - passed (aggregate clean-frame thresholds held)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" --logger "console;verbosity=detailed"`
    - `avg=0.0029y`, `p99=0.0409y`, `max=0.0764y`
- Frame-pattern note:
  - The Durotar descent segment near `(-1016.77, -4970.62)` now bottoms out on the seabed instead of integrating straight through the underwater floor.
  - The recorded water-entry transition now produces the expected `0.5x` horizontal velocity on the entry frame when seeded with the pre-entry airborne velocity state.
