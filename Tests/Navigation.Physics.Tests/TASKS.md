# Navigation.Physics.Tests Tasks

## Scope
- Directory: `Tests/Navigation.Physics.Tests`
- Project: `Navigation.Physics.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Local goal: keep native movement/physics parity regressions deterministic, actionable, and fast to validate before any live runs.

## Execution Rules
1. Use targeted test slices before full project sweeps when the failing area is already known.
2. Build `Navigation.dll` before blaming native/runtime mismatches on source changes.
3. Keep the flat-ground fixtures on known open terrain; do not weaken assertions to hide bad coordinates.
4. Use `Tests/Navigation.Physics.Tests/test.runsettings` for wider sweeps.
5. Update this file in the same session as any shipped parity-test delta.

## Simple Command Set
1. Native build: `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
2. Focused frame/controller slice: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests|FullyQualifiedName~MovementControllerPhysics" -v n`
3. Replay drift gate: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" -v n`
4. Full local sweep: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: `2026-03-24`
- Pass result: `delta shipped`
- Last delta:
  - `CollisionStepWoW` now resolves grounded support normals from the closest walkable AABB terrain contact to the chosen `groundZ` instead of leaving the default flat normal whenever `GetGroundZ` succeeds.
  - Added `ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport` to pin the exact steep-descent support-normal regression.
  - The existing steep-descent diagnostic now reports `No-ground frames: 0` instead of `528`, while keeping the same `0.20y` max hover gap over true ground.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundDetectionDiagnostic|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ValleyOfTrialsSlopeTests.SlopeRoute_StepPhysics_ZDoesNotOscillate"` -> `3 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground"` -> `1 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName=Navigation.Physics.Tests.ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundDetectionDiagnostic" --logger "console;verbosity=detailed"` -> passed; `No-ground frames 528 -> 0`, `groundNz` now varies across the slope
- Files changed:
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/ValleyOfTrialsSlopeTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- Blockers:
  - `standingOnInstanceId` / local support-point state is still just pass-through; this pass fixed support normal resolution for the grounded AABB path, not full touched-surface persistence.
  - `Exports/Navigation/TASKS.md` has pre-existing merge markers in the current worktree, so I left that tracker untouched instead of risking a bad merge-resolution edit during this physics slice.
  - Walkable-triangle-constrained waypoint smoothing remains deferred behind the current bot-behavior priorities.
- Next command:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~ValleyOfTrialsSlopeTests" -v n`

## Prior Session
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Fixed `NavigationInterop.MoveFlags` to match `Exports/Navigation/PhysicsBridge.h`; the test enum had `FallingFar` and `Flying` swapped and `OnTransport` on the wrong bit.
  - Added `FrameByFramePhysicsTests.KnockbackImpulse_AirborneTrajectoryMatchesWoWGravity` so the native airborne path is now pinned against WoW gravity/velocity math for knockback-style `FALLINGFAR` motion.
  - Moved `FlatGround_WalkForward_MaintainsGroundContact` onto the Crossroads flat-plains fixture because the old Valley of Strength line is no longer an unobstructed 1-second walk corridor in current map data.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.KnockbackImpulse_AirborneTrajectoryMatchesWoWGravity|FullyQualifiedName~FrameByFramePhysicsTests.FlatGround_WalkForward_MaintainsGroundContact" -v n` -> `2 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests|FullyQualifiedName~MovementControllerPhysics" -v n` -> `42 passed`
- Files changed:
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
