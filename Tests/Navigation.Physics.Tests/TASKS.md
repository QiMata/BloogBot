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
  - `CollisionStepWoW` half-step pass now uses `SceneQuery::SweepAABB(...)` over `speed*dt*0.5` instead of a static `TestTerrainAABB(...)` overlap.
  - Fresh `dumpbin /disasm` review of `WoW.exe` `CMovement::CollisionStep (0x633D1C..0x633DEB)` reconfirmed that vanilla does a second swept AABB on this branch.
  - This keeps the grounded runtime closer to the client’s collision flow after the earlier removal of the synthetic static-terrain step-up hold.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded` after stopping idle MSBuild `dotnet.exe` PIDs `16756` and `26576` that were holding `Navigation.dll`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=normal"` -> `32 passed`
- Files changed:
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- Blockers:
  - Static walkable-triangle support still should not be carried as a generic cached token unless new binary evidence says otherwise; the current gap is smooth walkable-surface adherence and live execution parity, not terrain-token persistence.
  - `Exports/Navigation/TASKS.md` has pre-existing merge markers in the current worktree, so I left that tracker untouched instead of risking a bad merge-resolution edit during this physics slice.
  - The next parity audit still needs to replace the remaining runtime-grounded differences branch-by-branch, especially wall/slide handling and any static-overlap shortcuts that still bypass the client’s sweep flow.
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`

## Prior Session
- Last updated: `2026-03-24`
- Pass result: `delta shipped`
- Last delta:
  - Removed the native static `stepUpBaseZ` terrain-hold heuristic from `PhysicsEngine.cpp`; grounded frames no longer keep a synthetic multi-frame stair/ledge Z just to bridge navmesh polygon gaps.
  - `PhysicsBridge.h` now documents `stepUpBaseZ` / `stepUpAge` as reserved compatibility fields that stay inert at runtime instead of implying active terrain support persistence.
  - Re-checked the current WoW.exe parity notes: transport-local continuity is still supported, but there is still no binary evidence for a generic cached static-terrain step-up hold.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" -v n` -> `32 passed`
- Files changed:
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsBridge.h`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- Blockers:
  - Static walkable-triangle support still should not be carried as a generic cached token unless new binary evidence says otherwise; the current gap is smooth walkable-surface adherence and live execution parity, not terrain-token persistence.
  - `Exports/Navigation/TASKS.md` has pre-existing merge markers in the current worktree, so I left that tracker untouched instead of risking a bad merge-resolution edit during this physics slice.
  - The next parity audit still needs to isolate the remaining ad-hoc grounded/clamp branch from `PhysicsEngine.cpp` before changing behavior again; this run removed only the unsupported static step-up hold.
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`

- Last updated: `2026-03-24`
- Pass result: `delta shipped`
- Last delta:
  - `SceneQuery::SweepCapsule` now forwards stable dynamic runtime IDs through all remaining elevator/door overlap and sweep branches instead of synthesizing `0x80000000 | triangleIndex`.
  - Added `ElevatorPhysicsParityTests.UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken`, which proves a real Undercity elevator frame reports the same moving-base support token through both `StepPhysicsV2` and `SweepCapsule`.
  - Re-scanned `WoW.exe` at `0x618C30..0x618D60` and `0x633840..0x6339C0`; the binary still shows transport-local persistence plus world-space collision, with no static terrain-token cache.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken" --logger "console;verbosity=normal"` -> `1 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UndercityElevatorTransportFrame_ReportsDynamicSupportToken|FullyQualifiedName~UndercityElevatorTransportFrame_SweepCapsuleSharesDynamicSupportToken|FullyQualifiedName~UndercityElevatorReplay_TransportAverageStaysWithinParityTarget|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"` -> `5 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n` -> `29 passed`
- Files changed:
  - `Exports/Navigation/SceneQuery.cpp`
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- Blockers:
  - Static walkable-triangle support still should not be carried as a generic cached token unless new binary evidence says otherwise; the current gap is movement-base continuity depth, not terrain-token persistence.
  - `Exports/Navigation/TASKS.md` has pre-existing merge markers in the current worktree, so I left that tracker untouched instead of risking a bad merge-resolution edit during this physics slice.
  - Walkable-triangle-constrained waypoint smoothing remains deferred behind the current bot-behavior priorities.
- Next command:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ElevatorPhysicsParityTests|FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~ServerMovementValidationTests" -v n`

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
