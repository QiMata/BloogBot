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
- Do not reuse the first RFC / Un'Goro "wall" regression coordinates as-is; current map data reports open space there, so they do not exercise grounded wall-slide behavior.
- Do not wire the new `0x6334A0` helper directly into live grounded resolution until `TestTerrainAABB` contact orientation and the post-query `0x637330` normal-flip path match the client; the first direct hookup already regressed both live Durotar parity routes.

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

## 2026-03-24 Ground Support Addendum

- Scope note:
  - This pass targeted grounded support-surface identification on steep terrain rather than replay-drift tails.
  - The immediate symptom was a deterministic slope diagnostic reporting `groundNz=1.0` for hundreds of grounded frames even though `groundZ` already matched the surface.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - `CollisionStepWoW` now resolves its grounded support normal from the closest walkable AABB terrain contact to the chosen `groundZ` instead of leaving the default flat `(0,0,1)` normal whenever `GetGroundZ` succeeds.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release`
    - succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundDetectionDiagnostic|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ValleyOfTrialsSlopeTests.SlopeRoute_StepPhysics_ZDoesNotOscillate"`
    - passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground"`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName=Navigation.Physics.Tests.ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundDetectionDiagnostic" --logger "console;verbosity=detailed"`
    - passed; steep-descent `No-ground frames` dropped from `528` to `0`, `groundNz` now varied across the route (`0.745..0.999`), and `Max Z above true ground` remained `0.20y`
- Frame-pattern note:
  - The steep Valley of Trials descent was not primarily losing floor Z clamp; it was losing support identity.
  - `groundZ` already tracked the correct surface, but the grounded path kept emitting a synthetic flat normal, so downstream logic had no reliable indication of which walkable triangle the character was actually standing on.

## 2026-03-24 Moving-Base Support Addendum

- Scope note:
  - This pass targeted parity between the grounded AABB path and the original client’s moving-base handling after reviewing fresh `WoW.exe` disassembly around `CMovement::Update (0x618C30)` and `CMovement::CollisionStep (0x633840)`.
  - The binary evidence reinforced that vanilla persists transport-local state (`transportGuid` + local offset/orientation), while static terrain support is recomputed from collision each frame rather than carried as a generic triangle token.
- Behavioral change shipped:
  - `Exports/Navigation/DynamicObjectRegistry.h/.cpp`
    - Dynamic objects now get stable runtime instance IDs and can resolve world support points back into object-local coordinates.
  - `Exports/Navigation/SceneQuery.h/.cpp`
    - `AABBContact` now carries instance identity, and `TestTerrainAABB` / `SweepAABB` now include dynamic-object triangles instead of treating the AABB ground path as static-only.
  - `Exports/Navigation/PhysicsEngine.cpp`
    - `CollisionStepWoW` now captures moving-base support identity/local point from the chosen grounded AABB support contact and emits it through `standingOnInstanceId` / `standingOnLocal*` only when the support is truly dynamic.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release`
    - succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Navigation.Physics.Tests.ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_ReportsDynamicSupportToken" --logger "console;verbosity=normal"`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" -v minimal`
    - passed (`3/3`)
## 2026-03-24 Moving-Base Query Identity Addendum

- Scope note:
  - This follow-up targeted the remaining mismatch between grounded AABB support tokens and capsule overlap/sweep query identities on moving bases.
  - A fresh `WoW.exe` spot-check over `CMovement::Update (0x618C30..0x618D60)` and `CMovement::CollisionStep (0x633840..0x6339C0)` still showed transport-local persistence plus world-space collision, but no static terrain token cache.
- Behavioral change shipped:
  - `Exports/Navigation/SceneQuery.cpp`
    - all remaining dynamic-object branches in `SweepCapsule` now forward stable runtime instance IDs from `DynamicObjectRegistry::QueryTriangles(..., outInstanceIds)` instead of synthesizing `0x80000000 | triangleIndex`.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
    - succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore`
    - succeeded (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken" --logger "console;verbosity=normal"`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UndercityElevatorTransportFrame_ReportsDynamicSupportToken|FullyQualifiedName~UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken|FullyQualifiedName~UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - passed (`5/5`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n`
    - passed (`29/29`)
- Frame-pattern note:
  - On Undercity frame `912`, `StepPhysicsV2` reported moving-base support `2147483650`.
  - The matching zero-distance penetrated capsule query on the same world support point now reports the same dynamic runtime ID instead of a per-triangle synthetic value.
  - Remaining non-zero non-dynamic hits in that sweep are nearby static WMO instance IDs and are expected.
- Parity note:
  - The open work is no longer “persist a static triangle token like the client.” The correct remaining target is moving-base continuity where we still need it, plus continued precision on walkable-triangle-constrained smoothing.
## 2026-03-24 Static Step-Up Hold Removal Addendum

- Scope note:
  - This pass targeted one explicit runtime heuristic in `PhysicsEngine.cpp`: the multi-frame `stepUpBaseZ` terrain hold used to bridge polygon gaps after a stair / ledge rise.
  - Fresh `WoW.exe` notes still support persisted moving-base continuity only; there is still no binary evidence for a synthetic static-terrain Z latch carried across frames.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - removed the runtime step-up height persistence block
    - `stepUpBaseZ` / `stepUpAge` are now emitted as inert compatibility fields instead of overriding grounded Z for several frames after a rise
  - `Exports/Navigation/PhysicsBridge.h`
    - bridge comments now describe those fields as reserved compatibility data rather than active terrain-hold state
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" -v n`
    - passed (`32/32`)
- Frame-pattern note:
  - Removing the synthetic hold did not reintroduce underground drift or the exact stuck-step regression.
  - The remaining movement gap is still corridor-following precision and later live execution ownership, not missing static-floor persistence in native physics.

## 2026-03-24 Ground Half-Step Sweep Addendum

- Scope note:
  - This pass targeted the next grounded runtime mismatch after the static step-up hold removal.
  - A fresh `dumpbin /disasm` spot-check over `CMovement::CollisionStep` (`0x633D1C..0x633DEB`) reconfirmed that vanilla runs a second swept AABB on the half-step branch; our code was still using a static `TestTerrainAABB` overlap there.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - `CollisionStepWoW` half-step pass now uses `SceneQuery::SweepAABB(...)` from the start box over `speed*dt*0.5` instead of a static half-step `TestTerrainAABB(...)`
    - this keeps the second grounded pass aligned with the client’s `0x633DEB` `Collide` call instead of treating the half-step box as a stationary overlap
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit `LNK1104` on `Navigation.dll`; stopped idle MSBuild `dotnet.exe` PIDs `16756` and `26576`; reran and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=normal"`
    - passed (`32/32`)
- Frame-pattern note:
  - The grounded movement slice stayed green after replacing the half-step overlap with the swept branch the binary actually uses.
  - The persistent live issue is still not broad floor-loss in native physics; it remains higher-level route/follow precision plus the later BG execution stall.

## 2026-03-24 Ground Wall Response Addendum

- Scope note:
  - This pass targeted the next grounded runtime heuristic after the half-step sweep correction: the manual wall pushback branch inside `CollisionStepWoW`.
  - Fresh decomp notes still point at the original client's `SlideAlongNormal` contact-plane projection path instead of the old `endX/endY += normal * skin` shove we still had in native physics.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `CollisionStepWoW` now resets per-frame wall outputs before collision resolution instead of carrying stale values across calls
    - non-walkable grounded contacts are now ordered and deduplicated, then the requested XY move is projected across those blocking planes to resolve wall response
    - `wallBlockedFraction` is now emitted from resolved-vs-requested XY distance, and grounded support is re-queried at the post-slide XY instead of after an ad-hoc normal push
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
    - added `ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits` to pin that a known walkable slope route keeps full progress and does not emit bogus wall hits after the wall-response rewrite
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore`
    - succeeded (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits" --logger "console;verbosity=normal"`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=normal"`
    - passed (`33/33`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=normal"`
    - passed
- Frame-pattern note:
  - The Valley of Trials slope route continued moving with `wallFrames=0`, `minBlockedFraction>0.99`, and `totalTravel>10y`, so the new grounded wall branch did not introduce false-positive blocking on known walkable terrain.
  - Two first-pass wall fixtures that looked promising in notes (`Ragefire Chasm` corridor and an `Un'Goro` crater wall) did not produce any wall hits in current map data and should not be reused until their coordinates are refreshed against a live trace.
- Recommended next single hypothesis:
  - Keep this contact-plane projection branch intact and replace only the remaining grounded wall/corner ordering heuristics with the exact multi-plane `SlideAlongNormal` sequence from the client, using a verified real wall trace rather than the stale RFC / Un'Goro coordinates.

## 2026-03-24 Ground Sweep Clamp Removal Addendum

- Scope note:
  - This pass targeted the last leftover grounded wall-response owner inside `CollisionStepWoW` after the contact-plane projection rewrite.
  - Runtime review showed the function was still pre-clamping `endX/endY` from full-sweep contacts before the later slide path ran, which left two competing grounded wall-resolution branches in the same frame.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - removed the old full-sweep `wallDist = max(0, distance - skin)` XY clamp branch from grounded `CollisionStepWoW`
    - the initial sweep now gathers contacts only; all grounded wall response is resolved by the later contact-plane slide path instead of a separate pre-clamp heuristic
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit `LNK1104` on `Navigation.dll`; a repo-scoped process scan showed only the active MSBuild process; reran once the lock cleared and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`33/33`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The Valley of Trials slope route remained clear of false wall hits after removing the redundant sweep clamp, so the remaining gap is still exact `SlideAlongNormal` ordering rather than a need for a second grounded shove/clamp branch.
  - A Stormwind candidate that looked like a wall-block repro in first-pass notes is currently a ledge/fall route in map data and should not be promoted into a wall-parity fixture.
- Recommended next single hypothesis:
  - Keep the single-owner grounded wall path and replace the remaining contact ordering / multi-plane corner behavior with the verified client `SlideAlongNormal` sequence, backed by a refreshed real wall trace on terrain, WMO, or a dynamic object.

## 2026-03-24 Ground Slide Contact-Dedupe Removal Addendum

- Scope note:
  - This pass targeted the next clearly non-verbatim grounded wall/corner heuristic inside `CollisionStepWoW`: the near-parallel normal dedupe inside the ordered contact-plane slide branch.
  - The binary-backed goal remains the same single-owner `SlideAlongNormal` flow, but the current code was still discarding later contact planes whenever `existing.dot(normal) > 0.999f`.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` no longer removes later non-walkable contacts just because their normals are almost aligned with an earlier plane
    - ordered non-walkable contacts now all participate in sequential slide projection, which keeps more corner constraints alive while the remaining ordering heuristics are still being reduced
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit `LNK1104` on `Navigation.dll`; `Get-Process` module scan showed repo `PathfindingService.exe` PID `16488` had the DLL loaded; stopped only that PID; reran and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`33/33`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Fixture-search note:
  - Broad current-data `SweepCapsule` probes around Goldshire Inn/Town, Northshire Abbey, and Stormwind Stockade did not produce real non-walkable hits.
  - Do not promote those coordinates into wall regressions unless a refreshed live trace proves they now exercise terrain/WMO/dynamic-object blocking.
- Recommended next single hypothesis:
  - Keep removing one remaining grounded ordering heuristic at a time, but do not guess at wall coordinates; refresh a real terrain/WMO/dynamic-object wall trace first, then continue replacing the contact-ordering shortcuts toward the client’s full `SlideAlongNormal` sequence.

## 2026-03-24 Ground Slide Contact-Sort Removal + Replay Fixture Addendum

- Scope note:
  - This pass targeted the next explicit grounded wall/corner heuristic after the near-parallel dedupe removal: the custom non-walkable contact sort inside `CollisionStepWoW`.
  - A local `WoW.exe` spot-check also closed a stale decomp note: `0x637330` is the vec3-negation helper used after `TestTerrain`, not the unresolved grounded slide helper.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` no longer re-ranks non-walkable contacts by custom distance / depth / horizontal-normal heuristics before sequential plane projection
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
    - added `DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`, which pins a real recorded Durotar wall-slide window and asserts the replay keeps sustained 60°+ deflection with tight spatial error
  - `docs/physics/wow_exe_decompilation.md`
    - corrected the `0x637330` note to `Vec3Negate`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - succeeded
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore`
    - succeeded (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.ComplexMixed_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The Durotar replay wall-slide window stayed spatially tight after the sort removal, so that custom re-ranking heuristic was not required to preserve the recorded deflection behavior.
  - The open gap remained the provenance of the grounded query volume and the unresolved post-`TestTerrain` wall helper, not the old per-contact ranking.
- Recommended next single hypothesis:
  - Recheck the local binary around the full-step / half-step branch directly before changing slide behavior again; confirm whether the client is actually accumulating sweep contacts there or only widening the eventual `TestTerrain` volume.

## 2026-03-24 Ground Query-Volume Merge Addendum

- Scope note:
  - This pass targeted the next grounded mismatch after the contact-sort removal by re-reading `CMovement::CollisionStep (0x633C7B..0x633E76)` in the local vanilla client.
  - The new binary read closed another stale assumption: `0x6373B0` is an AABB merge helper, not `CWorldCollision::Collide`.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `CollisionStepWoW` no longer accumulates full-step and half-step `SweepAABB` contacts into the wall/support query path
    - it now unions the start box, full-step box, and contracted half-step box, then runs `TestTerrainAABB(...)` on that merged volume before custom slide projection
    - post-slide support now comes only from the final resolved AABB query instead of re-appending earlier half-step contacts
  - `docs/physics/wow_exe_decompilation.md`
    - corrected the grounded/falling/swimming notes so `0x6373B0` is tracked as `AABB::Merge`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit `LNK1104` on `Navigation.dll`; reran once the transient lock cleared and succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The Durotar replay wall-slide window and the Stormwind blocked-wall fixture both stayed green after replacing synthetic sweep-contact accumulation with the merged query volume, so the old sweep-contact provenance was not required to preserve those known behaviors.
  - The remaining native gap is now narrower and better defined: the exact grounded post-`TestTerrain` wall/corner resolution sequence is still unresolved, but the query-volume builder preceding it now matches the local disassembly more closely.
- Recommended next single hypothesis:
  - Keep `0x6373B0` closed as `AABB::Merge`, then isolate the real post-`TestTerrain` grounded wall helper from the binary before replacing any more of the custom contact-plane projection logic.

## 2026-03-24 Ground Blocker-Axis Merge Addendum

- Scope note:
  - This pass targeted the next grounded mismatch after the merged query-volume rewrite: `CollisionStepWoW` was still resolving wall response by projecting directly across raw non-walkable triangle normals from `TestTerrainAABB(...)`.
  - Fresh local disassembly of the grounded helper chain (`0x6367B0` calling `0x636610`) points at a small merged blocker-axis set after `TestTerrain`, not an ordered loop over every raw triangle plane.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` now derives dominant opposing cardinal blocker axes from the merged `TestTerrainAABB(...)` contact set instead of projecting directly across raw triangle normals
    - blocker axes are merged with the same `1 / 2 / 3+` rules visible in the local `0x636610` helper before the slide tangent is computed
    - when the merged blocker collapses travel into a near-zero tangent, the stateless fallback now uses the strongest single blocker axis instead of stopping dead on a synthetic diagonal wedge
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Do Not Repeat:
  - Do not orient blocker axes purely from current movement direction whenever a non-walkable contact has any horizontal component. That version created widespread false wall hits and collapsed `MovementControllerPhysics` speeds to `0.77 y/s` on flat terrain and `3.19 y/s` on the live-speed route.
  - Do not let one diagonal non-walkable contact emit both cardinal blocker axes by default. That over-constrained oblique walls into synthetic corner wedges and kept `Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` below gate until the dominant-axis filter and strongest-axis fallback were restored.
- Frame-pattern note:
  - The replay wall-slide, blocked-wall, slope, and movement-controller slices stayed green after replacing raw triangle-plane projection with merged blocker axes, so the current stateless path is closer to the binary’s post-`TestTerrain` shape without reopening the old false-wall regressions.
  - The grounded branch is still not verbatim `WoW.exe`: the exact remaining-distance loop in `0x6367B0` plus the `0x635C00` / `0x635D80` helper effects are still unresolved.
- Recommended next single hypothesis:
  - Keep the blocker-axis merge, but replace the remaining one-shot wall-resolution bookkeeping with the grounded helper’s actual loop/remaining-distance sequence from `0x6367B0` instead of adding more fallback heuristics.
## 2026-03-25 Grounded Remaining-Move Retry Attempt

- Scope note:
  - This pass targeted the open `0x6367B0` bookkeeping gap by retrying grounded wall resolution once with the remaining move vector instead of resolving the frame in a single blocker-axis projection.
- Behavioral change attempted:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` was changed to run a two-iteration remaining-move loop over the merged blocker-axis resolver
    - the first blocker normal was kept for reporting, while the second pass attempted to project the already-slid move again to approximate the unresolved post-`TestTerrain` retry sequence
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_ReportsDynamicSupportToken|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.UndercityGroundProbe_WMOFloorDetected|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - failed: `MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` dropped to `3.26 y/s` (gate `>= 3.5 y/s`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Do Not Repeat:
  - Do not add a blind second grounded blocker-axis projection pass over the already-slid move while reusing the same merged `TestTerrainAABB(...)` contact set. That over-constrained the live-speed route and dropped `Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` to `3.26 y/s`, which is materially worse than the pre-change baseline.
- Recommended next single hypothesis:
  - Revert this retry loop. If `0x6367B0` bookkeeping is revisited again, it needs new binary evidence for how remaining distance is updated, not another stateless reprojection pass over the same blocker set.

## 2026-03-25 Distinct Secondary Blocker Axes + Verified Wall Fixtures

- Scope note:
  - This pass reopened grounded blocker-axis heuristics only after replacing the stale wall-coordinate assumptions with replay-backed fixtures on terrain, WMO, and dynamic-object geometry.
  - The old Stormwind Stockade and RFC coordinate probes remain diagnostics only; they are no longer treated as wall-parity evidence.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `buildMergedBlockerNormal(...)` still keeps the best opposing blocker axis as primary, but it no longer discards later distinct blocker axes with the `candidate.score + 0.1 < bestScore` heuristic
    - the primary axis is inserted first, then the remaining unique axes stay available to the existing `1 / 2 / 3+` merge rules and strongest-axis fallback
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
    - added `BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls`, which proves repeated zero-step stalls against static Blackrock Spire interior geometry with no nearby blocking GO within parity range
    - added `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock`, which proves the packet-backed Undercity elevator ride still clamps forward travel against the upper door while on the moving transport
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
    - added `Recordings.BlackrockSpire` so the WMO blocker fixture is tracked in the canonical recording set instead of by ad-hoc filename string
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded once the lock cleared
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - Verified replay-backed wall fixtures now exist for all three blocker classes needed by the parity backlog: terrain (`DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`), WMO (`BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls`), and dynamic-object (`PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock`).
  - The `score + 0.1` secondary-axis filter was not required to keep those fixtures, the slope false-wall guard, `MovementControllerPhysics`, or the aggregate replay gate green.
  - The grounded branch is still not verbatim `WoW.exe`; the remaining unresolved native work is the full post-`TestTerrain` `0x6367B0` loop plus the `0x635C00` / `0x635D80` helper bookkeeping.
- Recommended next single hypothesis:
  - Keep the new replay-backed wall fixtures in the focused slice and reduce the remaining one-shot `resolveWallSlide(...)` bookkeeping only when the next change is backed by fresh binary evidence from `0x6367B0`, not by another synthetic remaining-move retry.

## 2026-03-25 Three-Axis Blocker Merge Zeroes Output

- Scope note:
  - This pass targeted one specific unresolved `0x636610` merge rule inside the grounded `0x6367B0` helper chain after the verified terrain/WMO/dynamic wall fixtures were in place.
  - Fresh local `WoW.exe` disassembly showed that one jump-table case in `0x636610` explicitly zeroes the merged blocker vector instead of selecting a surviving axis.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `buildMergedBlockerNormal(...)` now returns `(0, 0, 0)` for the three-axis blocker case instead of falling through to the first surviving axis
    - the existing strongest-axis fallback in `resolveWallSlide(...)` remains responsible for keeping travel from collapsing when the merged blocker vector is zero
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded once the lock cleared
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The packet-backed Undercity door clamp, the Blackrock Spire WMO stalls, the Durotar terrain deflection replay, `MovementControllerPhysics`, and the aggregate drift gate all stayed green with the binary-backed three-axis zero rule in place.
  - This closes one real `0x636610` mapping detail, but the grounded path is still not verbatim `WoW.exe`; the remaining unresolved native work is the exact `0x6367B0` loop plus the `0x635C00` / `0x635D80` bookkeeping around the merged blocker result.
- Recommended next single hypothesis:
  - Isolate the still-open `0x635C00` / `0x635D80` effects from the grounded helper chain before changing any more of `resolveWallSlide(...)`; the next reduction should come from those helpers, not another guessed retry rule.

## 2026-03-25 Three-Axis Minority-Axis Selection + Four-Axis Zero

- Scope note:
  - This pass corrected the remaining `0x636610` jump-table mapping after re-reading the helper more carefully in the local `WoW.exe`.
  - The earlier same-day three-axis zero change held the current replay slice, but the disassembly showed it was still incomplete: the zero-output case belongs to four axes, while the three-axis case chooses the lone axis from the minority orientation group.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `buildMergedBlockerNormal(...)` now matches the local `0x636610` jump table more closely:
      - `1` blocker axis: copy it through
      - `2` blocker axes: keep the existing merged two-axis path
      - `3` blocker axes: choose the lone X-axis against two Y-axes, or the lone Y-axis against two X-axes
      - `4` blocker axes: zero the merged blocker vector
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded once the lock cleared
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The replay-backed Durotar terrain deflection, Blackrock Spire WMO stalls, packet-backed Undercity elevator door block, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate all stayed green with the corrected three-axis / four-axis mapping.
  - The open native gap is narrower again: the merged blocker selector is closer to the real helper, but the actual post-merge bookkeeping in `0x635C00`, `0x635D80`, and the surrounding `0x6367B0` loop is still not reduced to the binary.
- Recommended next single hypothesis:
  - Use the now-correct `0x636610` mapping as fixed input and isolate one concrete `0x635C00` or `0x635D80` effect next, rather than editing blocker-axis selection again.

## 2026-03-25 Horizontal Epsilon Pushout From 0x635D80

- Scope note:
  - This pass targeted the smallest remaining `0x635D80` effect that could be mapped cleanly into the current stateless slide path after the `0x636610` selector was corrected.
  - Fresh local disassembly shows `0x635D80` adding the `0.001f` constant at `0x801360` after the horizontal blocker correction is computed, so the resolved move is nudged slightly off the blocker plane.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` now adds a `0.001f` pushout along the horizontal blocker normal immediately after the client-style normal projection, instead of leaving the resolved move exactly on the blocker plane
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded once the lock cleared
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green with the binary-backed `0.001f` horizontal pushout in place.
  - The remaining native gap is now centered on the vertical/post-merge bookkeeping that `0x635C00` and `0x636100` still apply around the merged blocker result, not on the blocker selector or horizontal epsilon.
- Recommended next single hypothesis:
  - Keep the `0x635D80` epsilon pushout and isolate the vertical correction coming out of `0x635C00` next; that is the most obvious remaining client effect the stateless wall slide still lacks.

## 2026-03-25 Selected-Plane Z Correction With Radius Clamp

- Scope note:
  - This pass targeted the next explicit client effect still missing from the stateless grounded slide path: `0x635C00` emits a Z-only correction from the selected contact plane and clamps that correction against the unit bounding radius by scaling the in-flight distance.
  - The earlier grounded rewrite was still treating wall resolution as XY-only after the merged blocker selector, which meant the client’s per-plane vertical correction was being thrown away completely.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `buildMergedBlockerNormal(...)` now keeps the actual source normal for the primary opposing blocker contact alongside the merged blocker axis
    - grounded `resolveWallSlide(...)` now derives a Z-only correction from that selected contact plane, clamps its magnitude to `radius`, and scales the XY move by the same ratio when the correction would exceed the radius cap
    - the final grounded `GetGroundZ(...)` query now uses the clamped predicted support Z from that correction instead of always querying from the original `startZ`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - first attempt hit transient `LNK1104` on `Navigation.dll`; immediate retry succeeded once the lock cleared
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green with the selected-plane vertical correction and radius clamp in place.
  - This removes one more clear stateless shortcut, but the grounded path is still not verbatim `WoW.exe`: the unresolved work is now concentrated in the `0x636100` branch/retry path and the special `0x635C00` merged-vector substitution when the selected plane is effectively flat.
- Recommended next single hypothesis:
  - Keep the selected-plane Z correction and isolate the `0x636100` return-code `2` retry path next; that is the remaining place where the client still mutates distance/flags around `0x635C00` in a way the stateless path does not.

## 2026-03-25 Mutually Exclusive 0x636100 Helper Branch Selection

- Scope note:
  - This pass targeted the remaining ambiguity around `0x636100`: the local helper chain shows it gating either the `0x635D80` horizontal correction branch or the alternate `0x635C00` selected-plane branch with extra bookkeeping, not stacking both outputs unconditionally.
  - The stateless grounded slide was still blending those effects by always doing the horizontal blocker projection and then layering a selected-plane Z correction on top whenever the primary contact carried slope.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` now treats the `0x635D80`-style horizontal correction and the `0x635C00` selected-plane projection as mutually exclusive branches
    - near-vertical selected planes still use the horizontal blocker projection plus the `0.001f` pushout
    - sloped selected planes now project the requested move directly onto that selected contact plane and keep the existing radius clamp on the resulting Z correction, without also applying the horizontal epsilon branch
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
    - retargeted `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` to the promoted packet-backed elevator fixture’s real stalled window (`frames 11..19`) instead of the earlier debugging capture’s frame range
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - succeeded on the clean rerun after ignoring one earlier build/test overlap that raced `Navigation.dll`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`35/35`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate drift gate all stayed green with the helper choice split in place.
  - This removes another stacked stateless shortcut, but the grounded path is still not verbatim `WoW.exe`: the remaining native gap is now concentrated in the `0x636100` return-code / distance-pointer bookkeeping and the surrounding `0x6367B0` branch sequencing rather than the blocker merge or the plain helper outputs themselves.
- Recommended next single hypothesis:
  - Keep the mutually exclusive helper split and isolate the `0x636100` return-code / movement-fraction mutation next; do not revisit blocker-axis merge rules or the reverted two-pass retry loop without new binary evidence.

## 2026-03-25 Horizontal Branch Gate For Synthetic Uphill Plane Corrections

- Scope note:
  - This pass targeted the still-open `0x636100` gate by using the Durotar live-speed route as a concrete grounded symptom instead of treating the remaining native mismatch as wall-only.
  - The current stateless selected-plane branch could still manufacture uphill corrections on a walkable route, which then fed repeated `FALLINGFAR -> FALL_LAND` churn in the live forced-turn Durotar capture.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `resolveWallSlide(...)` now always computes the pure horizontal `0x635D80`-style correction first
    - when the alternate selected-plane branch would create a positive `Z` correction while also reducing horizontal travel versus that horizontal branch, the resolver now keeps the horizontal result instead of accepting the synthetic uphill plane correction
  - `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs`
    - strengthened `Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` so the exact Durotar route must now stay fully grounded (`FALLINGFAR == 0`) in addition to meeting the speed gate
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed" --logger \"console;verbosity=minimal\"`
    - passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger \"console;verbosity=minimal\"`
    - passed (`1/1`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger \"console;verbosity=minimal\"`
    - passed with a restarted `PathfindingService` so BG loaded the rebuilt `Navigation.dll`
- Frame-pattern note:
  - The deterministic Durotar live-speed route now stays grounded through all `400` frames while still running at `6.49 y/s`; the earlier `FALLINGFAR` churn on that exact route is gone in the fast harness.
  - Fresh live FG/BG evidence improved materially once the rebuilt DLL was actually loaded: BG outbound `MSG_MOVE_FALL_LAND` on the forced-turn Durotar route dropped from `11` to `4`, and BG again emitted a true `MSG_MOVE_STOP` near the route tail.
  - The remaining native mismatch is now narrower and no longer centered on `HitWall`: the live BG `physics_TESTBOT2.csv` trace still shows upper-surface `GetGroundZ(...)` picks with `HitWall=0` (for example `38.49 -> 40.676` near the route start), which then feed the remaining false landing cycles.
- Recommended next single hypothesis:
  - Keep the new `0x636100` gate and target the final grounded support probe next: reduce the `GetGroundZ(...)` start-height/search window in `CollisionStepWoW` so the route stops selecting upper terrain shelves when `HitWall=0`.

## 2026-03-25 Final-Box Support Contact Seed For Ground Query

- Scope note:
  - This pass targeted the remaining `HitWall=0` shelf snap on the live forced-turn Durotar route by changing only the final grounded `GetGroundZ(...)` seed in `CollisionStepWoW`.
  - Instead of always querying from `predictedSupportZ + stepH + stepUp`, the grounded path now seeded `GetGroundZ(...)` from the walkable final-box AABB support contact nearest `predictedSupportZ`, plus a tiny epsilon.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `CollisionStepWoW` now seeds the final `GetGroundZ(...)` call from the final-box walkable support contact closest to `predictedSupportZ` instead of the old fixed high-only start height
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed" --logger "console;verbosity=minimal"`
    - passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - passed with a restarted `PathfindingService`
- Frame-pattern note:
  - The deterministic Durotar live-speed route stayed green (`Speed: 6.49 y/s`, `FALLINGFAR: 0/400`, `minZ: 32.4`), so the lower seed did not reopen the native replay/controller slice.
  - The live forced-turn Durotar route still snapped to the same upper shelf on the opening segment: BG `physics_TESTBOT2.csv` moved from `38.490` to `40.626/40.677` at frames `652..653` with `HitWall=0`, then resumed the false `FALLINGFAR -> FALL_LAND` cycle. BG outbound packets remained `MSG_MOVE_SET_FACING`, `MSG_MOVE_START_FORWARD`, `16x MSG_MOVE_HEARTBEAT`, `3x MSG_MOVE_FALL_LAND`, and no `MSG_MOVE_STOP`.
  - Practical takeaway: the final-box walkable support contact can already be on the wrong shelf, so seeding the final `GetGroundZ(...)` call from that contact does not disambiguate multi-level terrain.
- Do-not-repeat note:
  - Do not assume the final grounded AABB support contact is trustworthy enough to seed `GetGroundZ(...)` on multi-level terrain; the live Durotar shelf snap still reproduced with `HitWall=0`.
- Recommended next single hypothesis:
  - Keep the live Durotar trace as the proof fixture, but query `GetGroundZ(...)` directly from the predicted support height itself (`predictedSupportZ + epsilon`) instead of any high-only or contact-derived seed.

## 2026-03-25 Direct Predicted-Support Ground Query

- Scope note:
  - This follow-up targeted the same live Durotar shelf snap, but removed the final-box support-contact seed entirely.
  - The final grounded `GetGroundZ(...)` call was queried directly from `predictedSupportZ + epsilon` so the support pick stayed anchored to the predicted support height instead of any elevated seed.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `CollisionStepWoW` now seeds the final `GetGroundZ(...)` call from `predictedSupportZ + COLLISION_SKIN_EPSILON`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed" --logger "console;verbosity=minimal"`
    - passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - failed; BG regressed to immediate false descent and packet-order mismatch on a restarted `PathfindingService`
- Frame-pattern note:
  - The native replay/controller slice stayed green, so this lower query seed does not immediately trip the deterministic native proof gates.
  - The live Durotar turn-start route regressed hard: BG dropped from `38.49` to `36.18` on the first moving frame, then continued into repeated false descents (`32.77 -> 21.42 -> 24.46 -> 35.05 ...`) with `HitWall=0`.
  - Packet evidence regressed at the start edge too: the parity assertion saw `MSG_MOVE_FALL_LAND` where the expected opening packet at that comparison point was `MSG_MOVE_SET_FACING`.
  - Practical takeaway: a pure `predictedSupportZ + epsilon` seed is too low for this multi-level terrain. The correct support surface sits above the low selected surface but below the old upper shelf.
- Do-not-repeat note:
  - Do not seed the final grounded `GetGroundZ(...)` query directly from `predictedSupportZ + epsilon` on this path; it promotes the lower `36.18` surface and drives immediate false descent.
- Recommended next single hypothesis:
  - Keep the lower/higher failure pair as the new constraint and seed the final grounded `GetGroundZ(...)` call from the mid-height window: `predictedSupportZ + stepUp + epsilon`, not `+epsilon` and not `+stepH + stepUp`.

## 2026-03-25 Mid-Height Ground Query (`predictedSupportZ + stepUp + epsilon`)

- Scope note:
  - This pass targeted the same multi-level Durotar shelf snap using the constraint learned from the failed low-query run: the support seed must stay above the low `36.18` surface but below the old high-only shelf promotion path.
  - The final grounded `GetGroundZ(...)` call was seeded from `predictedSupportZ + stepUp + epsilon`.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `CollisionStepWoW` now seeds the final `GetGroundZ(...)` call from `predictedSupportZ + stepUp + COLLISION_SKIN_EPSILON`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed" --logger "console;verbosity=minimal"`
    - passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - passed with a restarted `PathfindingService`
- Frame-pattern note:
  - The live route returned to the pre-regression packet pattern: BG outbound packets were `MSG_MOVE_SET_FACING`, `MSG_MOVE_START_FORWARD`, `15x MSG_MOVE_HEARTBEAT`, `3x MSG_MOVE_FALL_LAND`, and `1x MSG_MOVE_STOP`.
  - The opening multi-level support error still remained. BG `physics_TESTBOT2.csv` climbed from `38.490` to `40.626/40.677` at frames `644..645`, stayed on the upper shelf for `15` frames, then settled back onto the road at frame `659`.
  - Practical takeaway: tuning only the center-point `GetGroundZ(...)` seed is not enough. The grounded final snap still picks the wrong support surface at this XY even when the query height is constrained into the middle window.
- Do-not-repeat note:
  - Do not spend more time on center-point `GetGroundZ(...)` seed-height tuning alone for this Durotar fixture. High-only, contact-seeded, low-only, and mid-height seeds have now all been exercised without removing the opening false shelf.
- Recommended next single hypothesis:
  - Replace the final grounded center-point support lookup with `SceneQuery::GetCapsuleSupportZ(...)` so the final grounded snap is chosen from the capsule footprint rather than a single center sample on multi-level terrain.

## 2026-03-25 Capsule-Footprint Final Support Probe

- Scope note:
  - This pass replaced the final grounded center-point support lookup with the existing capsule-footprint helper in an attempt to eliminate the opening false upper-shelf pick on the live Durotar route.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - grounded `CollisionStepWoW` now called `SceneQuery::GetCapsuleSupportZ(...)` for the final support pick, with center-point `GetGroundZ(...)` as fallback
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed" --logger "console;verbosity=minimal"`
    - failed `ServerMovementValidationTests.GroundMovement_Position_NotUnderground`
- Frame-pattern note:
  - The footprint-based final support probe reopened a native regression immediately: `GroundMovement_Position_NotUnderground` reported `124/24333` grounded frames more than `0.5y` below engine ground Z (`0.51%` underground rate), with the worst new failures on the Durotar recordings.
  - Because the native underground gate failed, this pass was reverted before any new live BG evidence was accepted.
- Do-not-repeat note:
  - Do not replace the final grounded support lookup wholesale with `GetCapsuleSupportZ(...)`; the helper is too aggressive as a global primary support source and reopens the underground regression gate.
- Recommended next single hypothesis:
  - Keep the reverted mid-height center query as the stable baseline. The next support fix must be narrower than a full footprint-probe swap, likely a conditional multi-level disambiguation path that only activates when the center query promotes an upper shelf.

## 2026-03-25 Top-Level CollisionStep Branch Precedence (`0x633840`)

- Scope note:
  - This pass did not touch the still-unresolved grounded helper. It only aligned the top-level `CollisionStep` branch order after a fresh binary capture showed BG still treated overlapping airborne/swim frames in the wrong order.
  - Captured evidence lives in `docs/physics/0x633840_disasm.txt`.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - `StepV2` now prefers the airborne path whenever airborne flags are present, even if `MOVEFLAG_SWIMMING` overlaps on the same frame
    - pure swim frames still route through `ProcessSwimMovement`
  - `Tests/Navigation.Physics.Tests/FrameAheadIntegrationTests.cs`
    - added `AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~FrameAheadIntegrationTests.JumpArc_FlatGround_PeakHeightMatchesPhysics|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" --logger "console;verbosity=minimal"`
    - passed (`3/3`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The new dry-ground overlap regression now matches the binary intent: `FALLINGFAR | SWIMMING` produces the same descending frame shape as pure airborne motion and does not preserve `MOVEFLAG_SWIMMING` on dry land.
  - The live Durotar redirect slice remained green, so this cleanup did not disturb the existing dry-ground runtime path.
- Recommended next single hypothesis:
  - Keep the top-level `0x633840` precedence fixed and move to the next grounded blocker with direct binary evidence: disassemble `0x6334A0` `CheckWalkable`, then replace the current fixed walkability simplification before touching `0x636100` again.

## 2026-03-25 `0x6334A0` CheckWalkable Helper Capture

- Scope note:
  - This pass targeted the next grounded blocker with direct binary evidence: `0x6334A0` `CheckWalkable`.
  - The shipped delta was intentionally limited to binary capture, raw contact-data plumbing, a pure helper, and deterministic tests. A first runtime hookup was tried, regressed live parity immediately, and was reverted before handoff.
- Behavioral change shipped:
  - `Exports/Navigation/SceneQuery.h/.cpp`
    - `AABBContact` now preserves raw plane normals, raw triangle vertices, and plane distance so native code can evaluate the same contact geometry that `0x6334A0` reasons about
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `WoWCollision::CheckWalkable(...)` plus the local helper logic mirroring `0x6333D0` and `0x6335D0`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - exported `EvaluateWoWCheckWalkable(...)` for deterministic test coverage
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the P/Invoke for the pure helper export
  - `Tests/Navigation.Physics.Tests/WowCheckWalkableTests.cs`
    - added deterministic coverage for steep positive, shallow positive, steep negative-touch, and steep negative-no-touch cases
- Binary evidence captured:
  - `docs/physics/0x6334A0_disasm.txt`
    - `0x6334A0` uses `0.6427876f` when the `this+0x15C` path says the normal threshold is steep-only, and `0.17364818f` otherwise
    - positive normals above threshold call `0x6335D0` and may clear `0x04000000`
    - negative normals call `0x6333D0`, may consume `0x04000000`, and only succeed when `-normal.z > 0.6427876f`
    - `0x6333D0` uses the top-footprint corner test with `1/720`
    - `0x6335D0` uses the three edge planes with `1/12`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"`
    - passed (`5/5`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The pure helper/test seam is solid, but the first runtime hookup into grounded wall resolution regressed the live Durotar turn-start and redirect routes immediately.
  - Reverting that hookup returned both live routes to green, which narrows the remaining blocker to contact orientation / normal-flip parity rather than the helper logic itself.
- Do Not Repeat:
  - Do not route `0x6334A0` straight from the current `TestTerrainAABB` contact stream into live grounded resolution. Fix the contact-orientation / `0x637330` feed first, then retry.
- Recommended next single hypothesis:
  - Keep the new helper/tests frozen and align the `TestTerrain` contact-orientation / `Vec3Negate` path before any new grounded runtime usage of `0x6334A0`.

## 2026-03-26 Signed `TestTerrain` Contact Orientation (`0x6721B0` + `0x637330`)

- Scope note:
  - This pass targeted the exact blocker left by the first `0x6334A0` helper capture: the static `TestTerrainAABB` path was still upward-flattening contacts instead of preserving the signed, post-negation contact normal the client uses after `TestTerrain`.
  - Fresh binary evidence was captured in `docs/physics/0x6721B0_disasm.txt`.
- Behavioral change shipped:
  - `Exports/Navigation/SceneQuery.h/.cpp`
    - added `BuildTerrainAABBContact(...)`
    - static `TestTerrainAABB` contacts now face the query box center instead of always being flipped upward
    - `planeDistance` now matches that signed contact normal
    - `walkable` now uses signed `normal.z >= cos(50)` instead of `abs(normal.z)`
  - `Exports/Navigation/PhysicsEngine.cpp`
    - the pure `0x6334A0` helper path now consumes the signed contact normal first, with raw winding only as fallback
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateTerrainAABBContactOrientation(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the new pure orientation export
  - `Tests/Navigation.Physics.Tests/TerrainAabbContactOrientationTests.cs`
    - added deterministic coverage for floor-below, shelf-above, and wall-facing cases
- Binary evidence captured:
  - `0x6721B0`
    - copies `0x34` contact structs directly with `rep movsd`; it does not rebuild generic upward face normals
  - `0x637330`
    - is a pure three-component negate helper
  - Practical implication:
    - the client preserves a signed contact normal through `TestTerrain` and flips it once, so the BG static AABB path must not flatten everything upward
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"`
    - passed (`8/8`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`38/38`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The signed orientation feed held both live Durotar parity fixtures, which is the first clean confirmation that the `TestTerrain` contact-orientation blocker itself can move without reopening the old shelf/landing regressions.
  - This does not yet mean runtime `0x6334A0` parity is finished; it only means the signed contact feed it depends on is now parity-safe enough to retry.
- Recommended next single hypothesis:
  - Reintroduce runtime `0x6334A0` walkability usage on top of the signed `TestTerrainAABB` contact feed, then rerun the same focused deterministic/live Durotar gates before touching `0x636100` again.

## 2026-03-26 Reopened packet-backed Undercity upper-door blocker

- Scope note:
  - The current native baseline was rechecked before any further `0x6334A0` runtime hookup work. The live Durotar turn-start route still passed, but the compact packet-backed Undercity replay slice reopened `PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock`.
  - A short-lived standard-threshold `0x6334A0` runtime hookup was tried and reverted immediately after the focused replay gate failed harder; the reopened Undercity failure remained after the revert, so the active blocker is on the current baseline rather than in that abandoned hook.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed after a transient `LNK1104 Navigation.dll` retry
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`38/38`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"`
    - failed on the current baseline: `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock`
- Frame-pattern note:
  - Fresh replay dumping around frames `11..19` shows frames `11..14` still preserve the blocked step (`<= 0.018y` error) while frames `15..19` inject free forward motion on transport.
  - The engine is still transforming the transport-local position to the moving elevator correctly (`simZ` matches the next-frame elevator world `Z`), but `groundZ` remains stuck at `-28.868` after frame `14` and the grounded mover starts emitting world-space forward velocity (`~7 y/s`) instead of preserving the block.
  - That failure shape points away from generic transport coordinate math and toward the unresolved grounded blocker-axis / support-contact feed. The current prime suspects are the heuristic `0xC4E544` blocker-axis reconstruction in `CollisionStepWoW` and the dynamic-object support query path that feeds it.
- Do Not Repeat:
  - Do not retry the direct runtime `0x6334A0` hookup until the reopened packet-backed Undercity upper-door blocker is green again on the baseline.
  - Do not treat the current `38/38` widened deterministic slice as sufficient proof for this area; it does not include the packet-backed Undercity upper-door replay.
- Recommended next single hypothesis:
  - Finish tracing the binary producer for the `0xC4E544` blocker-axis buffer (`0x635240` / `0x633720` / `0x635410` chain), replace the remaining axis-merge heuristics with the binary-backed feed, then rerun the packet-backed Undercity upper-door slice before any further live/runtime walkability work.

### Follow-up: primary-axis override removal was reverted

- Hypothesis tried:
  - Remove the extra `primaryAxis` override in `CollisionStepWoW` because it is not backed by the `0x636610` / `0x6367B0` binary merge path and looked like a plausible source of the sideways escape into freefall on the packet-backed elevator blocker.
- Outcome:
  - `PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` stayed failed at the exact same frame and distance (`frame 15`, simulated step `3.5793y`).
  - The widened deterministic native slice still passed (`38/38`), but a live turn-start parity spot check regressed badly (`Stop edge diverged by 1594ms`, repeated false-airborne `0x4001` movement and large Z bobbing), so the code change was reverted immediately.
- Additional diagnostic evidence:
  - Dynamic-object ground support is present at the blocked doorway point on frame `15`:
    - start/world position `(-14.668z)` -> `GetGroundZ=-14.668`
    - recorded blocked next point `(-14.668z)` -> `GetGroundZ=-14.668`
    - simulated escaped point `(1551.917,245.923,-14.668)` -> `GetGroundZ=-49.522`
  - This confirms the dynamic support query is working at the blocked position; the mover is escaping sideways first, then falling to the shaft floor.
- Do Not Repeat:
  - Do not spend another run on removing the `primaryAxis` override by itself; it does not fix the packet-backed blocker and it destabilizes broader movement.
  - Do not burn more live parity time until the replay/native blocker and false-airborne bobbing are fixed in the deterministic native gates first.

## 2026-03-26 Packet-backed Undercity frame-15 contact probe

- Scope note:
  - This pass did not change runtime behavior. It turned the ad-hoc native frame dump into deterministic test coverage so the failing packet-backed Undercity upper-door frame can be inspected through the production `Navigation.dll` without a separate tester binary.
  - The target was the merged `TestTerrainAABB` query for `PacketBackedUndercityElevatorUp` frame `15`, where the replay currently escapes sideways and then falls.
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - exported `QueryTerrainAABBContacts(...)` so tests can record the exact merged-query contacts that feed grounded wall resolution
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added `TerrainAabbContact` plus the P/Invoke for `QueryTerrainAABBContacts(...)`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - added deterministic coverage for the real packet-backed frame-15 contact set
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`9/9`)
- Frame-pattern note:
  - The merged frame-15 query already contains the elevator support face at deck height with a signed downward normal (`normal.z ~= -1`) and raw `walkable=0`.
  - `EvaluateWoWCheckWalkable(...)` promotes that exact support face only on the helper's stateful path (`groundedWallFlagBefore=true`).
  - The same frame-15 contact dump also shows many wall contacts that would be promoted by that same stateful path if it were broadcast contact-by-contact across the whole merged query.
  - Practical implication:
    - the blocker is no longer "raw `walkable` vs `CheckWalkable`".
    - the blocker is reproducing the binary's selected-contact plus grounded-wall-state path before `0x6334A0` is applied.
- Do Not Repeat:
  - Do not replace grounded merged-query `contact.walkable` checks with unconditional `CheckWalkable(..., groundedWallFlagBefore=true)` or any equivalent broadcast stateful call; the real frame-15 contact dump proves that would also bless unrelated walls.
  - Do not treat the helper alone as the missing fix for the packet-backed Undercity blocker; the unresolved piece is the binary-selected contact / `0xC4E544` state path that feeds it.
- Recommended next single hypothesis:
  - Continue tracing the `0xC4E544` producer chain (`0x632BA0` / `0x633720` / `0x6351A0` / `0x635410` / `0x6353D0`) and map how the selected contact index plus grounded-wall state are chosen before `0x6334A0` runs.

## 2026-03-26 Grounded blocker-threshold retry (reverted)

- Scope note:
  - This pass targeted one explicit non-binary-backed grounded heuristic in `CollisionStepWoW`: the blocker-candidate filters `opposeScore <= 0.15f` and dominant-axis `> 0.25f`.
  - Fresh local disassembly around `0x632700` shows the client's candidate filter is effectively a near-zero opposing-dot test (`dot < -1e-5f`), so those custom thresholds are unsupported.
- Behavioral change tried:
  - `Exports/Navigation/PhysicsEngine.cpp`
    - removed the grounded blocker-candidate score and dominant-axis thresholds so any materially opposing contact could contribute to the existing axis merge path
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"`
    - failed unchanged: `frame 15`, simulated step `3.5793y`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - the only failure remained the same packet-backed Undercity transport replay; no new deterministic wall regression was uncovered in that slice
- Frame-pattern note:
  - Removing those thresholds did not move the failing transport stall at all. The same blocked window still holds through frame `14` and still breaks at frame `15`.
  - Practical implication:
    - the unsupported `0.15f` / `0.25f` filters are cleanup work, but they are not the missing selected-contact behavior that causes the packet-backed elevator escape.
- Do Not Repeat:
  - Do not spend another run on removing the blocker-candidate thresholds by themselves; the frame-15 transport miss is unchanged.
  - Do not treat `0x632700`'s near-zero opposing-dot filter as sufficient to reconstruct the selected-contact path; the unresolved piece is still the binary candidate/selector chain feeding `0xC4E544`.

## 2026-03-26 Grounded-wall state plumbing plus selected-contact walkability

- Scope note:
  - This pass targeted the reopened packet-backed Undercity upper-door blocker on the current baseline instead of packet cadence or live integration.
  - The goal was to move one level closer to the binary-selected contact path feeding `0x6334A0` without broadcasting stateful walkability across the whole merged query.
- Binary/evidence note:
  - The fresh note in `docs/physics/wow_exe_decompilation.md` records the selected-contact container rooted at `0xC4E52C` with `0xC4E534` (`0x34` contacts) and `0xC4E544` (`0x08` paired selector payload).
  - That evidence keeps the constraint explicit: `0x6367B0` consumes one selected entry plus one paired payload, so runtime `CheckWalkable` must stay on a chosen contact path rather than becoming a merged-query broadcast.
- Behavioral change shipped:
  - `Exports/Navigation/PhysicsBridge.h`, `Exports/Navigation/PhysicsEngine.h`, `Services/PathfindingService/Repository/Physics.cs`, `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`, and `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - now carry `groundedWallState` through native step/replay boundaries
  - `Exports/Navigation/PhysicsEngine.cpp`
    - now feeds `WoWCollision::CheckWalkable(...)` only from the selected primary contact in grounded wall resolution
    - uses a `0x635C00`-shaped Z-only correction on the stateful selected-contact walkable path instead of turning that support face into a horizontal plane-slide vector
    - sets `groundedWallState` after the non-walkable vertical branch and reuses that state in the later support-surface selection path
    - reorients blocker normals against the current collision position before the opposing-dot test
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - replaced the ad hoc frame dumps with a deterministic frame-16 assertion that the merged query contains a statefully walkable horizontal contact which is non-opposing until oriented against the current collision position
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests" --logger "console;verbosity=minimal"`
    - passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=minimal"`
    - passed (`4/4`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"`
    - passed (`29/29`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The selected-contact walkable branch must remain Z-only. A plane-slide vector on that path reopens the frame-15/16 forward escape on the elevator doorway.
  - The frame-16 merged query still contains raw `+X` side contacts that score as non-opposing until their normals are oriented against the current collision position. After that reorientation, the statefully walkable side face becomes a full opposing blocker and the blocked replay window stays green.
- Do Not Repeat:
  - Do not turn the stateful selected-contact walkable path back into a plane-slide vector; keep the local `0x635C00`-style Z-only shape unless new binary evidence disproves it.
  - Do not broadcast stateful `CheckWalkable` over every merged-query contact.
  - Do not describe the current-position reorientation as a named binary helper. It is an inference from the selected-contact semantics plus deterministic packet-backed evidence and should stay constrained by those fixtures.
- Recommended next single hypothesis:
  - Add a native transaction/export seam for the selected-contact producer path so deterministic tests can record the chosen index, paired selector payload, and post-selection branch result while tracing `0x6351A0` / `0x633720` / `0x635410` / `0x6353D0`.

## 2026-03-26 Selected-contact native trace seam

- Scope note:
  - This pass did not change runtime movement behavior. It converted the remaining frame-16 blocker-selection reconstruction from C# into a production-DLL native trace export so grounded selector work can be pinned without a separate tester project.
- Binary/evidence note:
  - Full-window disassembly of `0x6351A0` now confirms the local branch shape behind the selected-contact producer chain: `0x632BA0` -> `0x633720` -> direct `0xC4E544[index]` return on `0x635410` success, zero-pair success on `0x635410` failure, and `0x7C5DA0` / `0x6353D0` -> `0x635090` on the alternate path.
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateGroundedWallSelection(...)`, a production-DLL trace export that mirrors the current grounded blocker-selection path and returns the chosen contact, raw/oriented oppose scores, reorientation bit, and stateful `CheckWalkable` result
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added `GroundedWallSelectionTrace` plus the matching P/Invoke
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - now uses the native trace export for the frame-16 blocker-selection regression instead of reconstructing the selection path in C#
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=Navigation.Physics.Tests.PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The native trace now shows the selected frame-16 blocker as a real horizontal side face from the production DLL path (`normal ~= +X`, `oriented ~= -X`, `rawOppose ~= 0`, `orientedOppose ~= 1`, `walk0=0`, `walk1=1`), which is the exact transaction shape we need for the remaining producer-chain audit.
- Recommended next single hypothesis:
  - Extend the native trace seam one level deeper so deterministic tests can also record the chosen `0xC4E544` paired selector payload and the post-`0x635410` / post-`0x6353D0` branch source, then compare that against the `0x6351A0` disassembly before changing runtime behavior again.

## 2026-03-26 Shared grounded-wall transaction trace seam

- Scope note:
  - This pass stayed in native physics only. It did not try packet cadence or live parity.
  - The goal was to make the production grounded wall resolver itself traceable, rather than keeping a second selector implementation in the export layer.
- Diagnostic/runtime change shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added shared `WoWCollision::ResolveGroundedWallContacts(...)` plus `GroundedWallResolutionTrace`
    - routed the grounded runtime wall lambda through that shared helper so the export and runtime now execute the same selection/branch codepath
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - extended `EvaluateGroundedWallSelection(...)` to return the full branch transaction: state before/after, selected vs merged wall normals, branch kind, horizontal/branch/final projected moves, and blocked fraction
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching interop fields and branch enum
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - updated the frame-16 regression to pin the production-helper result rather than the earlier managed reconstruction
    - added a second frame-16 assertion for the resolver branch transaction
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - passed (`4/4`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`7/7`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~PhysicsReplayTests" --logger "console;verbosity=minimal"`
    - passed (`55/56`, one existing skipped MPQ extraction test)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The production frame-16 trace no longer supports the earlier managed assumption that the resolver selects a statefully walkable contact on that frame.
  - The shared helper now shows the chosen blocker is WMO instance `0x00003B34` at `point=(1553.8352, 242.3765, -9.1597)` with `normal ~= +X`, `oriented ~= -X`, `walk0=0`, `walk1=0`.
  - The corresponding branch is the plain horizontal path (`branch=1`), with `mergedWallNormal=(-1,0,0)`, `horizontal/branch move ~= (-0.001, 0.0315, 0)`, and final move clamped to zero by the post-branch direction test.
- Do Not Repeat:
  - Do not rely on the earlier managed frame-16 selector reconstruction that reported `walk1=1`; the production helper disproves it.
  - Do not add a separate native tester binary for this path. The useful harness is the production-linked export layer around the actual grounded resolver.
- Recommended next single hypothesis:
  - Trace the selected-contact producer chain one level deeper (`0x633720` / `0x635090`) to explain why the runtime-selected frame-16 blocker is WMO wall index `3` instead of the stateful elevator support contact present elsewhere in the merged query.

## 2026-03-26 Selected-contact metadata collapse evidence

- Scope note:
  - This pass did not change runtime physics behavior.
  - It extended the production-DLL grounded-wall trace seam so deterministic tests could resolve the selected contact back to static VMAP/WMO metadata.
- Diagnostic/test delta shipped:
  - `Exports/Navigation/WorldModel.h`
    - added read-only getters needed by the test export to inspect root/group metadata
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - `EvaluateGroundedWallSelection(...)` now resolves the selected contact back to static instance flags, model flags, root WMO id, and best-effort WMO group match
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching metadata fields on `GroundedWallSelectionTrace`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - added a packet-backed regression proving the frame-16 selected blocker currently collapses to parent WMO metadata only
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - passed (`5/5`)
- Frame-pattern note:
  - The frame-16 selected blocker still resolves to instance `0x00003B34`, but the new trace shows that metadata lookup only reaches the parent static WMO instance: `instanceFlags = modelFlags = 0x00000004`, `rootWmoId = 1150`, `groupId = -1`, `groupMatchFound = 0`.
  - That means the current `SceneCache` / `TestTerrainAABB` path is not missing the blocker triangle; it is missing the deeper child WMO/M2 identity that the client's `0x5FA550` model-property walk appears to use.
- Do Not Repeat:
  - Do not treat the current scene-cache `instanceId` on packet-backed Undercity frame 16 as proof that we already have the same model-property identity the client does.
  - Do not spend another pass extracting more raw geometry before preserving child WMO/M2 metadata through the selected-contact path.
- Recommended next single hypothesis:
  - Preserve child doodad/WMO metadata through the `SceneCache` -> `TestTerrainAABB` contact path, then rerun the frame-16 selected-contact trace to see whether the `0x633760` threshold gate can distinguish parent WMO vs child model identity.

## 2026-03-26 Selected-contact metadata source trace

- Scope note:
  - This follow-up still did not change runtime physics behavior.
  - It extended the static metadata trace with a best-effort child doodad match so the selected contact can report whether it resolves as parent WMO, WMO group, or child doodad M2.
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `selectedResolvedModelFlags` and `selectedMetadataSource` to `EvaluateGroundedWallSelection(...)`
    - after parent/group lookup, the trace now tries to match the selected contact triangle against the parent WMO's default doodad set using the same `.doodads` + `.vmo` transform path the extractor uses
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching interop fields
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - logs and asserts the resolved metadata source on the packet-backed frame-16 blocker
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"`
    - passed (`5/5`)
- Frame-pattern note:
  - The new metadata source field still reports `1` (`parent instance`) on the frame-16 selected blocker, and `selectedResolvedModelFlags` stays `0x00000004`.
  - That means the current best-effort static lookup still cannot recover a deeper child doodad/group identity from the selected triangle; the next real fix has to preserve that metadata earlier in the `SceneCache` / `TestTerrainAABB` pipeline.

## 2026-03-26 Legacy scene-cache auto-upgrade to metadata-bearing format

- Scope note:
  - This pass changed the production scene-loader path, not the grounded resolver body.
  - The goal was to remove the last non-binary metadata collapse in the normal `EnsureMapLoaded(...)` path before tracing deeper into the selected-contact producer chain.
- Binary/evidence note:
  - The already-captured packet-backed frame-16 blocker remains the same binary-selected triangle throughout: static WMO instance `0x00003B34`, root WMO `1150`, WMO group `3228`, `groupFlags = 0x0000AA05` once per-triangle metadata is preserved. That is the `0x5FA550`-relevant identity the client-side walkability threshold split needs.
- Diagnostic/runtime change shipped:
  - `Exports/Navigation/SceneCache.h`
    - added `GetExtractBounds()` so legacy caches can be rebuilt with the same bounded coverage they were loaded with
  - `Exports/Navigation/SceneQuery.cpp`
    - `EnsureMapLoaded(...)` now detects metadata-less legacy `.scene` files, rebuilds them through `SceneCache::Extract(...)`, saves back a v2 cache, and loads the metadata-bearing result instead of staying on the flattened parent-only path
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - added a direct legacy-v1 load regression proving collapse to parent WMO metadata still happens if the runtime is forced onto a v1 file
    - added an `EnsureMapLoaded(...)` upgrade regression proving the normal autoload path upgrades that same legacy file to v2 and returns the correct WMO-group metadata
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PacketBackedUndercityElevatorUp_Frame16_SelectedContactCurrentlyCollapsesToParentWmoMetadata|FullyQualifiedName~PacketBackedUndercityElevatorUp_Frame16_EnsureMapLoaded_UpgradesLegacySceneCacheToMetadataBearingFormat|FullyQualifiedName~PacketBackedUndercityElevatorUp_Frame16_FreshSceneExtract_ReportsSelectedContactMetadata" --logger "console;verbosity=detailed"`
    - passed (`3/3`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`14/14`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"`
    - passed (`32/32`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"`
    - passed (`1/1`)
- Frame-pattern note:
  - The scene-loader fix changes metadata source only. The selected frame-16 blocker stays the same triangle/instance, but the normal autoload path now resolves it as WMO group `3228` (`src=2`) instead of collapsing to parent-only metadata (`src=1`).
- Do Not Repeat:
  - Do not spend another pass on raw MPQ/WMO extraction for this blocker.
  - Do not add a separate native tester binary for scene-cache upgrade coverage; the production DLL + deterministic tests already prove the runtime loader behavior.
- Recommended next single hypothesis:
  - Extend the native transaction seam into the selected-contact producer chain so deterministic tests can capture the paired `0xC4E544` payload and whether the `0x633720` / `0x635090` path chose the frame-16 blocker for the same reason the binary does.

## 2026-03-26 Selected-contact threshold/prism trace addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - It extended the production-DLL trace around the already-selected contact so the deterministic harness can mirror the binary `0x633760 -> 0x6335D0` gate before changing the runtime branch again.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x6351A0_disasm.txt` and `docs/physics/0x632BA0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the newly confirmed `0x632BA0` five-slot candidate loop and the `0x633760` projected-prism interpretation
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - `GroundedWallResolutionTrace` now records the selected-contact threshold point, selected `normal.z`, current/projected `0x6335D0` prism inclusion, and whether the chosen contact would stay on the direct paired path under the relaxed or standard `0x633760` thresholds
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - `EvaluateGroundedWallSelection(...)` now exports those new threshold/prism fields from the production resolver trace
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching interop fields
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - added a packet-backed frame-16 regression pinning the projected-prism result
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
    - passed (`8/8`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`7/7`)
- Frame-pattern note:
  - The packet-backed frame-16 selected wall has `normal.z ~= -0.000224`, so it is threshold-sensitive under both the relaxed and standard `0x633760` modes.
  - The projected `position + requestedMove` point is outside the `0x6335D0` expanded prism (`insideProjected = 0`), so once that wall is selected it would stay on the alternate `0x635090` path under both thresholds (`directStd = 0`, `directRelaxed = 0`).
  - Practical implication: the remaining blocker is even earlier than the threshold split. The next parity pass needs to explain why `0x632BA0` is selecting the WMO wall entry in the first place instead of the stateful elevator-support candidate present elsewhere in the merged query.
- Do Not Repeat:
  - Do not assume the frame-16 selected wall is failing because we picked the wrong relaxed-vs-standard threshold inside `0x633760`; the projected-prism trace disproves that shortcut.
  - Do not spend another pass wiring a threshold-mode guess into runtime grounded resolution before the `0x632BA0` / `0x632280` selection chain is mapped more fully.
- Recommended next single hypothesis:
  - Trace the `0x632BA0` write path one level deeper (`0x632280` and its interaction with `0x632700`) so deterministic tests can record why the frame-16 WMO wall survives selection while the stateful elevator-support contact does not.

## 2026-03-26 Merged-query direct-pair absence addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to disprove one remaining shortcut hypothesis before touching the selector-builder runtime path again: whether the packet-backed frame-16 merged query already contained a direct-pair-ready contact that the current selection code was simply missing later in `0x633760`.
- Binary/evidence delta shipped:
  - added a raw capture in `docs/physics/0x632280_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the newly confirmed `0x632280` four-entry source loop plus the `0x632830` / `0x6329E0` helper shape
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - promoted the selected-contact threshold/prism math into a pure `EvaluateSelectedContactThresholdGate(...)` helper so tests can run the exact same `0x633760 -> 0x6335D0` gate logic over arbitrary merged-query contacts
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWSelectedContactThresholdGate(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching interop seam
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
    - turned the frame-16 direct-pair scan into a pinned regression and now asserts the merged query contains zero direct-pair candidates under both threshold modes
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"`
    - passed (`9/9`)
- Frame-pattern note:
  - The packet-backed frame-16 merged query is not hiding a better late direct-pair candidate. Under both the relaxed and standard `0x633760` thresholds, the direct-pair candidate count is `0`.
  - Practical implication: the next parity unit has to stay in the earlier selector-builder path (`0x632280` / `0x632830` / `0x6318C0`), not in another threshold/prism tweak.
- Do Not Repeat:
  - Do not spend another pass searching the raw frame-16 merged query for a missing direct-pair-ready contact under the current `0x633760 -> 0x6335D0` rules; the deterministic scan now proves there are none.
- Recommended next single hypothesis:
  - Trace the production resolver one step earlier so deterministic tests can compare its selected blocker against the binary `0x632280` / `0x632830` candidate-buffer rules.

## 2026-03-26 Selector support-plane builder addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to expose the next pure binary building block behind the selector chain: the fixed 9-plane support strip that `0x631BE0` prepares before `0x632830` starts validating candidate directions.
- Binary/evidence delta shipped:
  - added a raw capture in `docs/physics/0x631440_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the `0x631440` support-plane strip and its diagonal constants `0x80DFE4` / `0x80DFE0`
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildSelectorSupportPlanes(...)`, mirroring the binary `0x631440` plane-strip builder
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWSelectorSupportPlanes(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching `SelectorSupportPlane` interop
  - `Tests/Navigation.Physics.Tests/WowSelectorSupportPlaneTests.cs`
    - added deterministic coverage for both the axis planes and the diagonal planes/constants
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests" --logger "console;verbosity=minimal"`
    - passed (`2/2`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector-builder prework is now constrained by an exact 9-plane strip instead of an inferred shape. That removes one more place where the BG could be carrying a geometry approximation that never existed in the client.
- Do Not Repeat:
  - Do not replace the selector-builder support strip with ad-hoc axis or corner planes. The binary now gives us the exact 9-plane layout and constants.
- Recommended next single hypothesis:
  - Mirror the next pure builder in the same chain (`0x631BE0`) so deterministic tests can combine the exact 9-point neighborhood with the now-pinned 9-plane support strip before tackling `0x632830`.

## 2026-03-26 Selector neighborhood builder addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to expose the binary `0x631BE0` neighborhood/selector-table builder so the next `0x632830` work can start from exact data rather than inferred corner layouts.
- Binary/evidence delta shipped:
  - added a raw capture in `docs/physics/0x631BE0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the exact 9-point neighborhood and 32-byte selector table emitted by `0x631BE0`
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildSelectorNeighborhood(...)`, mirroring the binary `0x631BE0` point/table builder
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWSelectorNeighborhood(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added the matching interop seam
  - `Tests/Navigation.Physics.Tests/WowSelectorNeighborhoodTests.cs`
    - added deterministic coverage for the 9-point layout and the exact 32-byte selector table
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests" --logger "console;verbosity=minimal"`
    - passed (`4/4`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector-builder prework is now constrained by both exact upstream builders: the 9-plane support strip (`0x631440`) and the 9-point neighborhood / selector table (`0x631BE0`). That leaves `0x632830` / `0x6318C0` as the next real unknown, not the data they consume.
- Do Not Repeat:
  - Do not substitute an inferred corner order or inferred selector-byte pattern for this stage. The binary now gives the exact 9-point layout and exact 32-byte table.
- Recommended next single hypothesis:
  - Mirror the first candidate-validation helper (`0x6329E0` / `0x632830`) on top of the now-pinned support planes and selector neighborhood so deterministic tests can explain why a candidate survives or fails before `0x632700` returns.

## 2026-03-26 Selector candidate-validation addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the next selector-chain body itself: the ratio helper (`0x6329E0`) plus the in-place strip validation / rebuild path (`0x632830` / `0x632980` / `0x6318C0`).
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x6329E0_disasm.txt`, `docs/physics/0x632830_disasm.txt`, and `docs/physics/0x6318C0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the exact strip-buffer shape, ratio thresholds, and clip/rebuild rules
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `EvaluateSelectorPlaneRatio(...)`
    - added pure `ClipSelectorPointStripAgainstPlane(...)`
    - added pure `ClipSelectorPointStripExcludingPlane(...)`
    - added pure `ValidateSelectorPointStripCandidate(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWSelectorPlaneRatio(...)`
    - added `ClipWoWSelectorPointStripAgainstPlane(...)`
    - added `EvaluateWoWSelectorCandidateValidation(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector validator seams
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidateValidationTests.cs`
    - added deterministic coverage for ratio math, strip clipping, first-pass best-ratio updates, and strict second-pass rejection
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests" --logger "console;verbosity=minimal"`
    - passed (`9/9`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector-builder path is now pinned through the first candidate-validation gate as pure, deterministic code. The remaining unknown is no longer the strip math itself; it is how `0x632280` / `0x632700` feed concrete runtime candidate records into that validator and how the chosen record reaches `0x633720` / `0x635090`.
- Do Not Repeat:
  - Do not re-spend a pass guessing at the `0x632830` thresholds or rebuilding the strip from inferred generic clipping rules. The binary-backed ratio/clip/validation chain is now pinned in the production DLL with deterministic tests.
- Recommended next single hypothesis:
  - Trace the caller-side candidate record layout in `0x632700` / `0x632280` so deterministic tests can map one real selected record all the way into the now-pinned `0x632830` validator.

## 2026-03-26 Selector candidate-plane record addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the next pure caller-side helper feeding the already-mirrored strip validator: the four-plane candidate record builder at `0x632460`.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x632460_disasm.txt` and `docs/physics/0x637480_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the now-confirmed `0x632460` record layout and the `0x637480` normalized plane builder it uses
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildSelectorCandidatePlaneRecord(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWSelectorCandidatePlaneRecord(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector record seam
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidatePlaneRecordTests.cs`
    - added deterministic coverage for the three oriented side planes, the translated source-plane anchor, and the degenerate-edge early-fail path
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests" --logger "console;verbosity=minimal"`
    - passed (`11/11`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector caller chain is now pinned one step deeper: exact support planes, exact neighborhood/table, exact strip validator, and now the exact four-plane candidate record that feeds the first clip/validation pass. The next unknown is no longer the record geometry itself; it is how `0x632700` evaluates one record and how `0x632280` ranks the returned scalar/index set.
- Do Not Repeat:
  - Do not infer the `0x632460` record layout from generic extrusion logic or from reordered selector corners. The binary now fixes the cyclic selector order, the opposite-point flip test, and the translated source-plane anchor.
- Recommended next single hypothesis:
  - Mirror the `0x632700` single-record evaluator on top of the now-pinned `0x632460` output so deterministic tests can explain one candidate's filter, strip clipping, and selected-ratio result before `0x632280` performs tie ranking.

## 2026-03-26 Selector record-evaluator addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the next caller-side selector body itself: the `0x631870` plane-prefix clip helper plus the `0x632700` record-set evaluator that sits between the record builders and the already-mirrored `0x632830` validator.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x631870_disasm.txt` and `docs/physics/0x632700_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the exact `0x34` record layout, the local strip seeding path, the prefix clip loop, and the final best-ratio/index update rule
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `ClipSelectorPointStripAgainstPlanePrefix(...)`
    - added pure `EvaluateSelectorCandidateRecordSet(...)`
    - added `SelectorCandidateRecord` and `SelectorRecordEvaluationTrace`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `ClipWoWSelectorPointStripAgainstPlanePrefix(...)`
    - added `EvaluateWoWSelectorCandidateRecordSet(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector evaluator seams
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidateRecordSetTests.cs`
    - added deterministic coverage for the plane-prefix early-fail path, the dot-reject path, the clip-reject path, and the lowest-ratio record selection path
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests" --logger "console;verbosity=minimal"`
    - passed (`15/15`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector chain is now pinned through the first caller-side evaluator: exact support planes, exact neighborhood/table, exact plane record, exact prefix clip, exact validator, and now the exact record-set walk that updates the chosen ratio/index. The remaining unknown is no longer how one record is evaluated; it is how `0x632F80` / `0x632280` build and rank the multi-record buffers that feed this helper.
- Do Not Repeat:
  - Do not reintroduce inferred record scoring or guessed strip seeding here. The binary now fixes the dot filter threshold, the `-1` source-id seed, the prefix clip order, and the final caller-best update rule.
- Recommended next single hypothesis:
  - Mirror the `0x632F80` five-record builder on top of the now-pinned selector neighborhood and candidate directions so deterministic tests can feed real binary-shaped record arrays into the now-pinned `0x632700` evaluator.

## 2026-03-26 Selector quad-record builder addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the next pure builder feeding the now-mirrored `0x632700` evaluator: the 4-selector / 5-plane candidate record builder at `0x632F80`.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x632F80_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` with the now-confirmed 4-selector ring walk, previous-point flip rule, and slot-4 source-plane anchor
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildSelectorCandidateQuadPlaneRecord(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWSelectorCandidateQuadPlaneRecord(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new quad-record seam
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidateQuadPlaneRecordTests.cs`
    - added deterministic coverage for the four oriented side planes, the translated source-plane anchor, and the degenerate-edge early-fail path
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests" --logger "console;verbosity=minimal"`
    - passed (`17/17`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector builder chain is now pinned through both plane-record shapes consumed by the caller-side evaluator: the 3-selector / 4-plane record from `0x632460` and the 4-selector / 5-plane record from `0x632F80`. The next unknown is no longer how those records are built; it is how `0x632280` / `0x632BA0` rank, append, and swap the candidate buffers that feed `0x632700`.
- Do Not Repeat:
  - Do not infer the `0x632F80` side-plane orientation from generic quad extrusion. The binary fixes the ring order, the previous-point flip test, and the slot-4 source-plane anchor.
- Recommended next single hypothesis:
  - Mirror the `0x632280` overwrite/append/swap ranking path on top of the now-pinned `0x632460` / `0x632F80` record builders and the now-pinned `0x632700` evaluator.

## 2026-03-26 Selector source-ranking addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the next caller-side selector body itself: the `0x632280` four-source overwrite/append/swap ranking loop that sits between the `0x632460` translated-triplet builder and the already-mirrored `0x632700` evaluator.
- Binary/evidence delta shipped:
  - tightened `docs/physics/wow_exe_decompilation.md` so the `0x632280` section now explicitly records the production-DLL mirror: source-plane dot reject, `0x632460` clip-plane build, `0x632700` evaluator handoff, and the `0x80DFEC` overwrite/append/swap window on the 5-slot best-candidate buffer
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `EvaluateSelectorTriangleSourceRanking(...)`
    - added `SelectorSourceRankingTrace`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWSelectorTriangleSourceRanking(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector-ranking seam
  - `Tests/Navigation.Physics.Tests/WowSelectorSourceRankingTests.cs`
    - added deterministic coverage for the dot-reject path, builder-reject path, evaluator-reject path, overwrite path, and append-and-swap near-tie path
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests" --logger "console;verbosity=minimal"`
    - passed (`22/22`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector caller chain is now pinned through the first caller-side multi-source ranking body: exact support planes, exact neighborhood/table, exact translated-triplet clip planes, exact record evaluator, and now the exact caller-best overwrite/append/swap behavior that keeps the newest near-tie best in slot 0.
  - The remaining unknown is no longer how `0x632280` ranks four source planes; it is how `0x632BA0` assembles and ranks the 5 candidate directions before `0x6351A0` gates the chosen result.
- Do Not Repeat:
  - Do not treat `0x632280` as a record builder. The binary scratch block is a translated-triplet clip-plane buffer feeding `0x632700`, while the candidate records themselves stay in the already-pinned `0x34` record array.
- Recommended next single hypothesis:
  - Mirror the `0x632BA0` five-direction chooser on top of the now-pinned `0x632280` source-ranking helper and the already-pinned `0x632F80` quad-record builder before touching `0x6351A0` / `0x635410`.

## 2026-03-26 Selector direction-ranking addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the next caller-side selector body itself: the second-half `0x632BA0` five-direction chooser core that sits between the already-mirrored `0x632F80` quad-record builder and the later `0x6351A0` selected-contact gate.
- Binary/evidence delta shipped:
  - tightened `docs/physics/wow_exe_decompilation.md` so the `0x632BA0` section now explicitly records the production-DLL mirror for the second-half chooser core and also keeps the unresolved `0x632A30` / `0x631E70` setup gates explicit
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `EvaluateSelectorDirectionRanking(...)`
    - added `SelectorDirectionRankingTrace`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWSelectorDirectionRanking(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector-ranking seam
  - `Tests/Navigation.Physics.Tests/WowSelectorDirectionRankingTests.cs`
    - added deterministic coverage for the direction-plane dot-reject path, builder-reject path, evaluator-reject path, append-and-swap near-tie promotion path, and the final `0x80DFEC` zero-clamp gate
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - passed (`27/27`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selector caller chain is now pinned through both caller-side ranking bodies: exact support planes, exact neighborhood/table, exact translated-triplet ranking in `0x632280`, exact five-direction quad-record ranking in the second half of `0x632BA0`, and the same binary `0x80DFEC` overwrite/append/swap window that keeps the newest near-tie best in slot `0`.
  - The remaining unknown is no longer how the second-half chooser ranks five directions; it is how the earlier `0x632A30` / `0x631E70` setup gates seed or bypass that loop and how `0x6351A0` / `0x635410` consume the selected record afterward.
- Do Not Repeat:
  - Do not treat the new seam as the full `0x632BA0` mirror. The early zero-distance success path and the `0x632A30` / `0x631E70` gating logic are still unresolved and must stay explicit until separately pinned from the binary.
- Recommended next single hypothesis:
  - Mirror the tiny `0x635410` height-match helper next, then return to the unresolved `0x632A30` / `0x631E70` setup gates so the full `0x632BA0 -> 0x6351A0` producer chain can be assembled without guesswork.

## 2026-03-26 Selector post-gate z-match addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the tiny post-selector gates that `0x6351A0` uses after `0x633720`: `0x635410` on the direct-return path and `0x6353D0` on the alternate path.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x635410_disasm.txt` and `docs/physics/0x6353D0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the selected-contact note now records that both helpers scan the same local `0x10`-stride candidate buffer at `buffer + 8`, which means they compare `normal.z`, not world height
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `HasSelectorCandidateWithNegativeDiagonalZ(...)`
    - added pure `HasSelectorCandidateWithUnitZ(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `HasWoWSelectorCandidateWithNegativeDiagonalZ(...)`
    - added `HasWoWSelectorCandidateWithUnitZ(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new post-selector z-match seams
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidateZMatchTests.cs`
    - added deterministic coverage for the `0x635410` negative-diagonal match, the `0x6353D0` unit-Z match, the binary epsilon window, and the bounded-candidate-count path
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"`
    - passed (`31/31`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The selected-contact producer chain is now pinned one step deeper on the `0x6351A0` side: after the already-mirrored selector builders/evaluators/rankers and the already-documented `0x633720` gate, both tiny local candidate-buffer tests are now explicit binary seams.
  - The remaining unknown is no longer those post-selector z-match scans; it is the unresolved `0x632A30` / `0x631E70` setup side of `0x632BA0` and the broader `0x6351A0` transaction around the selected index and paired `0xC4E544` payload.
- Do Not Repeat:
  - Do not refer to `0x635410` as a height-match helper again. The binary is explicit that it reads the third float from the local candidate plane record, so this is a `normal.z` match gate, not a world-height check.
- Recommended next single hypothesis:
  - Disassemble and mirror the unresolved `0x631E70` and `0x632A30` setup gates next so the full `0x632BA0` producer path can be assembled before wiring a wider `0x6351A0` transaction seam.

## 2026-03-26 Cached query-bounds gate addendum

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the first explicit cache-hit gate inside the newly reviewed `0x631E70` path: the inclusive point-vs-AABB test at `0x637350`.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x637350_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the unresolved `0x631E70` note now records that it uses `0x637350` against the cached bounds at `0xC4E5A0` before rebuilding the merged query
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `IsPointInsideAabbInclusive(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWPointInsideAabbInclusive(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new bounds-containment seam
  - `Tests/Navigation.Physics.Tests/WowAabbContainmentTests.cs`
    - added deterministic coverage for inclusive min/max acceptance plus below-min / above-max rejection
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"`
    - passed (`34/34`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is now slightly narrower: before touching the expensive merged-query rebuild, the binary first checks whether both current and projected points are already inside the cached query AABB.
  - The remaining unknown is no longer that inclusive bounds check; it is the rest of the `0x631E70` query-builder body and the `0x632A30` wrapper that decides when to invoke it.
- Do Not Repeat:
  - Do not collapse this helper into a strict `< max` test or an epsilon-grown bounds check. The binary compares `point >= min` and `point <= max` directly on all three axes.
- Recommended next single hypothesis:
  - Capture and mirror the smallest self-contained branch of `0x632A30` next, using the now-pinned `0x637350` cache-hit gate as part of the setup path instead of re-inferring it.

## 2026-03-26 Terrain-query mask addendum (`0x6315F0`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the exact query-mask builder that `0x631E70` feeds into `0x6721B0`, so the next merged-query work stops relying on inferred mask constants.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x6315F0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the unresolved `0x631E70` note now records the exact `0x6315F0` base-mask split and both augmentation gates
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildTerrainQueryMask(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWTerrainQueryMask(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new query-mask seam
  - `Tests/Navigation.Physics.Tests/WowTerrainQueryMaskTests.cs`
    - added deterministic coverage for the `0x5FA550` base-mask split, the strict `this+0x20 > 0x80DFE8` `0x30000` gate, the swim exclusion, and the two-bit `0x8000` augment
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - passed (`17/17`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is now narrower in one more place: the mask it passes into `0x6721B0` is no longer guesswork. The remaining unknown is the rest of the query-builder transaction around cached bounds reuse, merged AABB expansion, optional swim-side query, and post-query transform handling.
- Do Not Repeat:
  - Do not rename the `this+0x20` gate as “pitch” or any other semantic field in code yet. The binary proves the raw offset and threshold comparison, but the field meaning is still an inference.
- Recommended next single hypothesis:
  - Mirror the smallest remaining `0x631E70` branch around the `0x6315F0 -> 0x6721B0` call site next, now that the exact query-mask math is pinned.

## 2026-03-26 Projected query-bounds addendum (`0x631E70`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to pin the exact projected AABB shape that `0x631E70` builds before the double `0x637350` cache-fit test.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x631E70_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the unresolved `0x631E70` note now records the exact projected bounds layout and the two-corner cache-fit gate
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildTerrainQueryBounds(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWTerrainQueryBounds(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new projected-bounds seam
  - `Tests/Navigation.Physics.Tests/WowTerrainQueryBoundsTests.cs`
    - added deterministic coverage for the exact `XY` radius expansion, `Z` feet/min vs `feet + height` max, and the paired-corner cache-fit shape when composed with `0x637350`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"`
    - passed (`11/11`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is now narrower again: before the cache-hit gate, the exact projected query AABB is no longer inferred. The remaining unknown is the post-cache-miss expand/merge/query transaction plus the optional swim-side query and transform rewrite of `0xC4E534`.
- Do Not Repeat:
  - Do not treat the projected bounds as symmetric on Z. The binary is explicit that `min.z = projected.z` while only `max.z` gets the `this+0xB4` expansion.
- Recommended next single hypothesis:
  - Mirror the remaining `0x631E70` cache-miss path next: `0x637300`, `0x6372D0`, `0x6373B0`, the `0x61E9C0` pre-query call, and the optional swim-side `0x30000` query.

## 2026-03-26 AABB-merge addendum (`0x6373B0`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the already-suspected `0x6373B0` helper from fresh raw binary evidence and pin it through the production DLL so the merged-query volume stops relying on anonymous local logic.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x6373B0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the unresolved `0x631E70` note now records that `0x6373B0` is a pure componentwise AABB union helper, not a query/collision routine
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `MergeAabbBounds(...)`
    - replaced the local merged-query lambda in `CollisionStepWoW` with that binary-backed helper
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `MergeWoWAabbBounds(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new AABB-merge seam
  - `Tests/Navigation.Physics.Tests/WowAabbMergeTests.cs`
    - added deterministic coverage for the exact componentwise min/max union and shared-face preservation path
- Validation:
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowAabbMergeTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"`
    - passed (`13/13`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again, but the closure here is structural rather than behavioral: `0x6373B0` contributes only the AABB union. The remaining unknowns are still the expand/copy/query transaction around it and the `0x632A30` wrapper.
- Do Not Repeat:
  - Do not spend more time treating `0x6373B0` as a hidden collision or slide helper. The raw binary closes it as a pure AABB merge.
- Recommended next single hypothesis:
  - Mirror the remaining `0x631E70` / `0x632A30` transaction next, starting with the full `0x632A30` wrapper and the exact `0x631E70 -> 0x632280` call/early-return shape.

## 2026-03-26 Wrapper-gates addendum (`0x632A30`, `0x6376A0`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the wrapper-visible gates on `0x632A30` and the shared selector-plane initializer `0x6376A0`, so the remaining unknown is the data transaction feeding `0x632280`, not the wrapper edges around it.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x632A30_disasm.txt` and `docs/physics/0x6376A0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the unresolved `0x632A30` / `0x631E70` note now records the explicit no-override `0x631E70` call, the early-fail `*outScalar = 0` path, the final `0x80DFEC` zero clamp, and the `(0,0,1,0)` selector-plane init from `0x6376A0`
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `InitializeSelectorSupportPlane(...)`
    - added pure `ClampSelectorReportedBestRatio(...)`
    - added pure `FinalizeSelectorTriangleSourceWrapper(...)`
    - refactored `EvaluateSelectorDirectionRanking(...)` to use the same binary-backed clamp helper
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `InitializeWoWSelectorSupportPlane(...)`
    - added `EvaluateWoWSelectorReportedBestRatioClamp(...)`
    - added `EvaluateWoWSelectorTriangleSourceWrapperGates(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new wrapper/init seams
  - `Tests/Navigation.Physics.Tests/WowSelectorSourceWrapperTests.cs`
    - added deterministic coverage for the init, clamp, early-fail, override bypass, and success-path zero clamp
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSourceWrapperTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowAabbMergeTests" --logger "console;verbosity=minimal"`
    - passed (`17/17`)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"`
    - passed (`16/16`)
- Frame-pattern note:
  - The unresolved `0x632A30` path is narrower again, but still not behavior-complete: the wrapper edges are now closed, while the remaining unknown is the full data transaction through `0x631BE0`, optional `0x631E70`, and `0x632280`.
- Do Not Repeat:
  - Do not keep treating the remaining `0x632A30` gap as a generic wrapper mystery. The visible early-return and zero-clamp behavior are closed; only the interior data flow remains.
- Recommended next single hypothesis:
  - Mirror the next internal `0x632A30` data seam instead of another wrapper edge, starting with the exact argument flow into `0x632280` and the `0x631BE0` outputs it consumes.

## 2026-03-26 Wrapper-seeds addendum (`0x632A30`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the fixed seed payload that `0x632A30` hands to `0x632280`, so the remaining unknowns are the variable fields rather than the wrapper defaults.
- Binary/evidence delta shipped:
  - reused the fresh raw capture in `docs/physics/0x632A30_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the `0x632A30` note now records both fixed `(0,0,-1)` vectors and the initial `1.0f` best ratio before the `0x632280` call
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `InitializeSelectorTriangleSourceWrapperSeeds(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `InitializeWoWSelectorTriangleSourceWrapperSeeds(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new seed seam
  - `Tests/Navigation.Physics.Tests/WowSelectorSourceWrapperSeedTests.cs`
    - added deterministic coverage for the exact `testPoint`, `candidateDirection`, and initial `bestRatio` values
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSourceWrapperSeedTests|FullyQualifiedName~WowSelectorSourceWrapperTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - passed (`15/15`)
- Frame-pattern note:
  - The unresolved `0x632A30` path is narrower again: the fixed seed state is now closed, and the next unknown is the variable payload that changes with the caller transaction.
- Do Not Repeat:
  - Do not spend more time re-proving the fixed `(0,0,-1)` seed vectors or the `1.0f` initial ratio. Those wrapper defaults are now closed.
- Recommended next single hypothesis:
  - Mirror the next variable `0x632A30` payload field, starting with the selected-index seed and the exact `0x631BE0` outputs that are passed forward into `0x632280`.

## 2026-03-26 Scalar-offset addendum (`0x6372D0`, `0x637300`, `0x61E9C0`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the tiny scalar-offset helpers on the `0x631E70` cache-miss path so the remaining unknown is the higher-level merged transaction rather than the per-vector arithmetic.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x6372D0_disasm.txt`, `docs/physics/0x637300_disasm.txt`, and `docs/physics/0x61E9C0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the old `ExpandAndSweep` label is replaced with the true helper semantics: subtract scalar from the min vector, add scalar to the max vector, and then merge; `0x61E9C0` is a no-op in this build
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `AddScalarToVector3(...)`
    - added pure `SubtractScalarFromVector3(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `AddScalarToWoWVector3(...)`
    - added `SubtractScalarFromWoWVector3(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new scalar-offset seams
  - `Tests/Navigation.Physics.Tests/WowVectorScalarOffsetTests.cs`
    - added deterministic coverage for the exact add/subtract-all-components behavior
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowAabbMergeTests|FullyQualifiedName~WowSelectorSourceWrapperSeedTests" --logger "console;verbosity=minimal"`
    - passed (`6/6`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again, but this closure is still structural: the scalar offsets are now closed, while the remaining unknown is how the full cache-miss transaction combines them with the merged bounds, query mask, and optional swim-side work.
- Do Not Repeat:
  - Do not keep treating `0x637300` as a sweep/collision routine. In this build it is only a three-component scalar subtract helper.
- Recommended next single hypothesis:
  - Mirror the next higher-level `0x631E70` cache-miss transaction step, starting with the exact merged-bounds handoff after `0x637300` / `0x6372D0` and the still-variable query call state.

## 2026-03-26 Cache-miss bounds addendum (`0x631E70`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the higher-level merged-bounds handoff on the `0x631E70` cache-miss path so the remaining unknown is the optional swim-side query / contact-flip work rather than the AABB transaction itself.
- Binary/evidence delta shipped:
  - tightened `docs/physics/wow_exe_decompilation.md` so the `0x631E70` note now records the exact cache-miss sequence: projected query bounds, binary `1/6` expansion, then merge against cached `0xC4E5A0` through `0x6373B0`
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `BuildTerrainQueryCacheMissBounds(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWTerrainQueryCacheMissBounds(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new cache-miss seam
  - `Tests/Navigation.Physics.Tests/WowTerrainQueryCacheMissBoundsTests.cs`
    - added deterministic coverage for the exact cache-miss AABB transaction
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowAabbMergeTests" --logger "console;verbosity=minimal"`
    - passed (`9/9`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again. The merged cache-miss bounds handoff is now closed, and the next unknown is the optional swim-side `0x30000` query / `0x637330` contact flip before this data feeds the selector path.
- Do Not Repeat:
  - Do not keep re-deriving the cache-miss bounds by composing smaller helpers in tests only. That higher-level transaction is now pinned explicitly.
- Recommended next single hypothesis:
  - Mirror the `0x6320C5..0x63213A` swim-side query path inside `0x631E70`, including the `0x30000` mask and the contact normal/plane flip via `0x637330`.

## 2026-03-26 Swim-side plane-flip addendum (`0x637330`, `0x597AD0`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the per-contact plane rewrite on the `0x631E70` swim-side query path so the remaining unknown is the surrounding transform loop rather than the flip itself.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x597AD0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the swim-path note now records both halves of the rewrite: `0x637330` negates the normal and `0x597AD0` writes the negated `{normal, planeD}` record back
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `NegatePlane(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `BuildWoWNegatedPlane(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new plane-flip seam
  - `Tests/Navigation.Physics.Tests/WowSwimQueryPlaneFlipTests.cs`
    - added deterministic coverage for the exact flipped-plane output
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests" --logger "console;verbosity=minimal"`
    - passed (`9/9`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again. The swim-side per-contact rewrite is now closed, and the next unknown is the transport-local contact transform loop starting at `0x63214C`.
- Do Not Repeat:
  - Do not keep treating the swim-side branch as “normal-only” negation. The plane distance flips with it.
- Recommended next single hypothesis:
  - Mirror the `0x63214C..0x632270` transport-local contact transform loop inside `0x631E70`.

## 2026-03-26 Transport-local transform addendum (`0x7BD700`, `0x7BCC60`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the raw point/vector/plane math behind the `0x63214C..0x632270` transport-local contact rewrite, so the remaining unknown is the per-contact loop shape rather than the transform formulas.
- Binary/evidence delta shipped:
  - added raw captures in `docs/physics/0x7BD700_disasm.txt` and `docs/physics/0x7BCC60_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the transport note now records the inverse RT-frame build (`0x7BD700`) and the frame-applied point transform (`0x7BCC60`) that the `0x631E70` rewrite loop uses
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `TransformWorldPointToTransportLocal(...)`
    - added pure `TransformWorldVectorToTransportLocal(...)`
    - added pure `BuildTransportLocalPlane(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `TransformWoWWorldPointToTransportLocal(...)`
    - added `BuildWoWTransportLocalPlane(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new transport-local seams
  - `Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs`
    - added deterministic coverage for the inverse-yaw point transform and local-plane rebuild
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTransportLocalTransformTests|FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests" --logger "console;verbosity=minimal"`
    - passed (`8/8`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again. The inverse transport-local transform math is now closed, and the next unknown is the exact contact-buffer loop that applies it per cached contact before selector consumption.
- Do Not Repeat:
  - Do not re-open the point/vector transform formulas as a heuristic question. The binary now closes the inverse-frame build and point-transform pair the loop depends on.
- Recommended next single hypothesis:
  - Mirror the `0x63214C..0x632270` per-contact rewrite loop itself, including the exact source/destination fields copied back into the cached contact buffer.

## 2026-03-26 Transport-local record rewrite addendum (`0x63214C`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the actual cached-contact record body that `0x631E70` rewrites after the inverse transport transform is built, so the remaining unknown is the outer loop/gating rather than the `0x34`-byte record contents.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x63214C_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the transport note now records the exact per-record layout: plane at `+0x00..+0x0C`, points at `+0x10/+0x1C/+0x28`
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `TransformSelectorCandidateRecordToTransportLocal(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `TransformWoWSelectorCandidateRecordToTransportLocal(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the record-transform seam
  - `Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs`
    - added deterministic coverage for the full record rewrite
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTransportLocalTransformTests|FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests" --logger "console;verbosity=minimal"`
    - passed (`9/9`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again. The record body is now closed, and the next unknown is the count/guid gate plus array walk around `0xC4E530` / `0xC4E534`.
- Do Not Repeat:
  - Do not treat the `0x34`-byte record shape as speculative anymore. The binary loop now closes the plane-plus-three-points layout directly.
- Recommended next single hypothesis:
  - Mirror the outer `0x63214C..0x632270` batch loop and gate conditions next, including the `transportGuid == 0` and `count == 0` fast exits.

## 2026-03-26 Transport-local record-buffer loop addendum (`0x63214C`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the visible fast exits and in-place array walk around the already-pinned `0x34`-byte record transform.
- Binary/evidence delta shipped:
  - reused the fresh raw capture in `docs/physics/0x63214C_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the same note now records the `transportGuid == 0` and `count == 0` fast exits explicitly
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `TransformSelectorCandidateRecordBufferToTransportLocal(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `TransformWoWSelectorCandidateRecordBufferToTransportLocal(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the record-buffer seam
  - `Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs`
    - added deterministic coverage for the zero-guid fast exit and nonzero-guid full-buffer rewrite
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTransportLocalTransformTests|FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests" --logger "console;verbosity=minimal"`
    - passed (`11/11`)
- Frame-pattern note:
  - The unresolved `0x631E70` path is narrower again. The outer transport-local loop is now closed as well, and the next unknown is the later selector path that consumes the rewritten cache.
- Do Not Repeat:
  - Do not keep treating the `0x63214C` branch as a transform-math blocker. Both the record body and the fast-exit loop/gates are now pinned.
- Recommended next single hypothesis:
  - Return to the later selector handoff that consumes the transformed records, starting at `0x632280`.

## 2026-03-26 Selector-consumer tail addendum (`0x6351A0`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the visible `0x6351A0` consumer tail so the next runtime change can use the real direct-pair / zero-pair / alternate-pair branch contract instead of the current inferred wall fallback logic.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x635734_callsite_disasm.txt`
  - added raw capture in `docs/physics/0x7C5DA0_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the `0x6351A0` note now records the full visible outcome contract:
    - zero-distance early return with pair-only zero
    - `0x632BA0` failure returning `2` and zeroing the move vector
    - selected-index sentinel return
    - two distinct out-state dwords on the post-`0x633720` tail
    - alternate `0x7C5DA0` / `this+0x84` unit-Z gate before `0x635090`
  - `0x7C5DA0` is now closed more precisely as a tiny airborne time-scalar helper (`this->+0xA0 * -1/gravity` when airborne, else `0`), not a radius helper
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `EvaluateSelectorAlternateUnitZFallbackGate(...)`
    - added pure `EvaluateSelectorPairConsumer(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWSelectorAlternateUnitZFallbackGate(...)`
    - added `EvaluateWoWSelectorPairConsumer(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector-consumer seams
  - `Tests/Navigation.Physics.Tests/WowSelectorPairConsumerTests.cs`
    - added deterministic coverage for the alternate unit-Z gate, zero-distance return, ranking failure, selected-index sentinel return, direct-pair return, zero-pair direct tail, unit-Z zero-pair tail, and alternate-pair return
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - passed (`19/19`)
- Frame-pattern note:
  - The open selector gap is narrower again. The later consumer tail is now pinned, including the two caller-visible out-state dwords. The next missing runtime piece is feeding the real selected index plus paired `0xC4E544` payload into grounded resolution instead of reconstructing a blocker from merged contacts.
- Do Not Repeat:
  - Do not collapse the `0x6351A0` tail into a single generic “state” bit. The caller at `0x635734` reads two distinct out-state dwords after the call.
  - Do not reintroduce selector fallback heuristics on the alternate path without matching the binary `0x7C5DA0` / `this+0x84` unit-Z gate first.
- Recommended next single hypothesis:
  - Expose the selected index plus paired `0xC4E544[index]` payload from the production grounded path, then wire that exact transaction into `ResolveGroundedWallContacts(...)` before revisiting live bobbing behavior.

## 2026-03-26 Selector follow-up gate addendum (`0x635550`)

- Scope note:
  - This pass still did not change runtime grounded behavior.
  - The goal was to close the visible pure gate that `0x635450` calls immediately after `0x6351A0`, so the next runtime change can mirror the caller-side selector transaction instead of inferring the post-selection airborne/window checks.
- Binary/evidence delta shipped:
  - added raw capture in `docs/physics/0x635550_disasm.txt`
  - tightened `docs/physics/wow_exe_decompilation.md` so the `0x6351A0` note now also records the visible `0x635550` contract:
    - immediate success when the second `0x6351A0` out-state dword is nonzero
    - otherwise require `this->+0xA0 < 0`
    - compute the binary `0x7C5DA0` jump-time scalar
    - compare that scalar against the window start/end and finally against horizontal move length squared using `this->+0x84`
- Diagnostic/test delta shipped:
  - `Exports/Navigation/PhysicsEngine.h/.cpp`
    - added pure `ComputeJumpTimeScalar(...)`
    - added pure `EvaluateSelectorPairFollowupGate(...)`
  - `Exports/Navigation/PhysicsTestExports.cpp`
    - added `EvaluateWoWJumpTimeScalar(...)`
    - added `EvaluateWoWSelectorPairFollowupGate(...)`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
    - added matching interop for the new selector follow-up seams
  - `Tests/Navigation.Physics.Tests/WowSelectorPairFollowupGateTests.cs`
    - added deterministic coverage for the jump-time helper, alternate-state short-circuit, negative-vertical gate, window-before/window-after outcomes, and the final strict horizontal-length-squared comparison
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal`
    - passed
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
    - passed
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorPairFollowupGateTests|FullyQualifiedName~WowSelectorPairConsumerTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"`
    - passed (`27/27`)
- Frame-pattern note:
  - The open selector gap is narrower again. The visible follow-up gate after `0x6351A0` is now closed, so the next unknown is the surrounding `0x635450` transaction that combines the two out-state dwords, the `0x635550` result, and the `0x7C5F50` scalar before grounded resolution consumes the selected payload.
- Do Not Repeat:
  - Do not treat `0x7C5DA0` as a generic radius or distance helper. The fresh binary capture closes it as a jump-time scalar gated solely by `MOVEFLAG_JUMPING`.
  - Do not weaken the final `0x635550` comparison to `>=`. The binary uses a strict greater-than check on horizontal allowance squared versus move length squared.
- Recommended next single hypothesis:
  - Mirror the visible `0x635450` caller transaction next, including the exact use of the two `0x6351A0` out-state dwords, the `0x635550` result, and the `0x7C5F50` scalar before touching runtime grounded resolution again.
