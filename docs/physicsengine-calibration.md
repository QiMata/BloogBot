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
