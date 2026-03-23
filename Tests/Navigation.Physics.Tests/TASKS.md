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
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added Orgrimmar transport replay coverage using the only in-repo transport capture for that area, `Dralrahgra_Durotar_2026-02-08_11-06-02` (the Orgrimmar-to-Undercity zeppelin).
  - The new tests prove two things deterministically: the replay harness cleanly matches the ground-side boarding/disembark windows that still have world geometry, and it explicitly skips the in-flight frames because the recording drops `NearbyGameObjects` to zero immediately after boarding.
  - `7.9` is therefore still blocked on better recording data rather than a discovered physics regression.
- Validation:
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrgrimmarZeppelinRide_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~OrgrimmarZeppelinReplay_SkipsInFlightFrames_WithoutDynamicObjectData" -v n` -> `2 passed`
- Files changed:
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
- Blockers:
  - The current Orgrimmar zeppelin recording does not retain in-flight dynamic transport snapshots, so full transport-on-transport replay parity for `7.9` still needs a newer capture or a separate reconstruction source.
  - Recorded directional remote-unit extrapolation coverage still needs a better packet fixture outside this project.
  - `docs/TASKS.md` still has the open second Orgrimmar transport replay item (`7.9`).
- Next command:
  - `Get-Content Services/ForegroundBotRunner/Mem/Offsets.cs | Select-Object -First 260`

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
