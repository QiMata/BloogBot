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
5. Recording corpus compact: `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release -- compact`

## Remaining Physics Parity Backlog
Known remaining work in this owner: `0` items.
1. [x] Session 192: added `UndercityUpperDoorContactTests` plus the `QueryTerrainAABBContacts(...)` export seam so the failing packet-backed Undercity frame can now be inspected deterministically through the production `Navigation.dll`. The new coverage proves the merged query already contains the signed downward elevator support face, and it also proves `0x6334A0` only promotes that face on its stateful path.
2. [x] Session 191: added `TerrainAabbContactOrientationTests` and a pure orientation export so the signed `TestTerrainAABB` contact feed is now pinned deterministically. The floor-below, shelf-above, and wall-facing cases all stay green alongside the `0x6334A0` helper seam and the live Durotar parity routes.
3. [x] Session 190: added `WowCheckWalkableTests` around the pure exported `0x6334A0` helper so the signed-normal thresholds, top-corner touch path, and `0x04000000` consume/preserve behavior are now deterministic. The first direct runtime hookup regressed live Durotar parity and was reverted, so this owner now pins the helper semantics without changing the green runtime baseline.
4. [x] Session 189: added `FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround` so the top-level `0x633840` airborne-before-swim branch order is now pinned in deterministic coverage.
5. [x] Session 188: Disassembled `0x6367B0` from WoW.exe binary and implemented the retry loop in `CollisionStepWoW`. `0x636100` return codes (0/1/2) and `0x636610` integer merge logic documented.
6. [x] Remaining heuristic thresholds audited against binary. Integer jump-table logic in `0x636610` matched by our float approximations. No regressions.
7. [x] All 30 proof gates green after retry loop: `MovementControllerPhysics`, `AggregateDriftGate`, wall replay fixtures (Durotar/BRS/Undercity), multi-level terrain disambiguation.

## Session Handoff
- Last updated: `2026-03-26 (session 194)`
- Pass result: `delta shipped`
- Last delta:
  - Session 194 replaced the frame-16 blocker-selection reconstruction with a native trace export from the production `Navigation.dll`. `NavigationInterop.cs` now exposes `GroundedWallSelectionTrace`, and `UndercityUpperDoorContactTests.cs` asks the DLL directly which contact it chose, what the raw/oriented oppose scores were, and how stateful `CheckWalkable` evaluated that contact.
  - This owner can now pin the grounded blocker transaction itself without a separate C++ test project: chosen contact, reorientation bit, raw/non-raw oppose scores, and walkability before/after state all come back from the same DLL the runtime uses.
  - Session 193 carried the binary-selected grounded-wall state through the deterministic interop path. `NavigationInterop.cs` still talks to the production `Navigation.dll`, but replay input/output now preserve `GroundedWallState` so packet-backed tests can exercise the same selected-contact state path across grounded frames that the runtime uses.
  - `UndercityUpperDoorContactTests.cs` replaced the temporary frame dumps with a real frame-16 regression: the merged query contains a statefully walkable horizontal contact that is non-opposing until the contact normal is oriented against the current collision position.
  - This owner now pins the two real packet-backed invariants behind the current runtime fix: frame 15 proves the elevator support face requires the stateful `0x6334A0` path, and frame 16 proves the chosen blocker can still be missed unless the contact is oriented the way the runtime currently reconstructs it.
  - Important constraint: that current-position reorientation is still an inference from selected-contact semantics plus the packet-backed frame evidence, not a named binary helper. Keep it pinned, but keep tracing the producer chain before broadening the claim.
  - Session 192 added a deterministic recorder for the real failing packet-backed Undercity frame instead of relying on one-off temp harness output. `NavigationInterop.cs` now exposes `QueryTerrainAABBContacts(...)` plus `TerrainAabbContact`, and `UndercityUpperDoorContactTests.cs` reconstructs the exact merged frame-15 query against the production `Navigation.dll`.
  - The new tests prove the merged query already contains the elevator deck support face at deck height with a signed downward normal and raw `walkable=0`.
  - The same tests also prove `EvaluateWoWCheckWalkable(...)` only promotes that support face on the helper's stateful path and that the same state would also promote many wall contacts in the same query if applied indiscriminately.
  - This changes the native target: the missing parity piece is now the binary selected-contact / `0xC4E544` state path feeding `0x6334A0`, not the helper body in isolation.
  - Session 191 added deterministic coverage for the signed `TestTerrainAABB` contact feed that the runtime still lacked after the first `0x6334A0` helper capture. `TerrainAabbContactOrientationTests.cs` now proves the pure orientation export keeps support below the query box upward and walkable, geometry above the query box downward and non-walkable, and wall contacts facing the box center.
  - `NavigationInterop.cs` now exposes `EvaluateTerrainAABBContactOrientation(...)`, and the native export routes through the same `BuildTerrainAABBContact(...)` helper that `SceneQuery.cpp` now uses for static `TestTerrainAABB` contacts.
  - The widened focused slice stayed green after the signed-orientation change: the new orientation tests, the `WowCheckWalkable` helper tests, the airborne-before-swim guard, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and both live Durotar parity routes all passed.
  - Session 190 added deterministic coverage for the binary `0x6334A0` walkability helper. The new `WowCheckWalkableTests.cs` fixture drives the exported pure helper through steep positive, shallow positive, steep negative-touch, and steep negative-no-touch cases, which now pins the real signed-normal thresholds plus the `0x04000000` consume/preserve behavior without depending on a full live movement scene.
  - `NavigationInterop.cs` now exposes the helper through `EvaluateWoWCheckWalkable(...)`, and `SceneQuery.cpp` / `PhysicsTestExports.cpp` preserve enough raw contact geometry for the tests to reason about the same triangle/plane data the client helper uses.
  - A first direct runtime hookup of the helper regressed both live Durotar parity routes and was reverted before handoff. This owner therefore keeps the deterministic helper coverage but does not yet claim runtime parity for `0x6334A0`; the next native delta has to fix `TestTerrain` contact orientation / `Vec3Negate` parity first.
  - Session 189 added a deterministic guard for the top-level `0x633840` branch order before touching the still-partial grounded helper. Fresh binary disassembly now lives in `docs/physics/0x633840_disasm.txt` and shows airborne is checked before swim (`0x633A29`/`0x633A4C` before `0x633B5E`).
  - `FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround` proves the native engine now treats `FALLINGFAR | SWIMMING` on dry ground as airborne motion: the frame descends like pure freefall and the final output clears `MOVEFLAG_SWIMMING`.
  - The focused native slice stayed green after the rebuild: the new precedence test, an existing jump-arc sanity check, the packet-backed swim replay, and the live Durotar redirect parity route all passed unchanged.
  - Session 185 converted the native parity backlog into an exact counted checklist. This owner now records `4` known remaining native-proof items, aligned to the master `11`-item parity checklist in `docs/TASKS.md`.
  - No code or tests changed in session 185; this was a docs-only planning update.
  - Session 184 re-ran `RecordingMaintenance compact` after the new BotRunner live-recording work to keep the parity corpus lean: the canonical recordings directory still contains `26` logical recordings totaling `411.67 MiB`, all `.bin` sidecars were already current (`written=0, skipped=26`), and the duplicate `Bot/Debug|Release/net8.0/Recordings` output trees were already missing/clean.
  - Session 184 re-ran the compact packet-backed Undercity replay slice after the recording-helper changes in `BotRunner.Tests`; `PacketBackedUndercityLowerRoute_ReplayRemainsUnderground`, `PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck`, and `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` all stayed green.
  - Session 182 split the grounded `0x636100` helper choice in `PhysicsEngine.cpp`: `resolveWallSlide(...)` now treats the `0x635D80` horizontal correction and the `0x635C00` selected-plane projection as mutually exclusive branches instead of stacking both on sloped selected planes.
  - Session 182 retargeted `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` to the promoted packet-backed elevator fixture’s real stalled window (`frames 11..19`) so the compact March 25 recording remains the canonical upper-door block regression.
  - The rebuilt native DLL still held `MovementControllerPhysics`, `GroundMovement_Position_NotUnderground`, the Durotar wall-slide replay, the Blackrock Spire WMO stalls, the packet-backed Undercity upper-door block, and the aggregate drift gate with that helper split in place.
  - Session 181 promoted the final packet-backed March 25 Undercity recordings after the foreground native movement fix: `Recordings.PacketBackedUndercityLowerRoute` now points at `Urgzuga_Undercity_2026-03-25_10-00-52` and `Recordings.PacketBackedUndercityElevatorUp` now points at `Urgzuga_Undercity_2026-03-25_10-01-09`.
  - The stale intermediate `Urgzuga_Undercity` capture attempts from the repeated debugging passes were pruned from the canonical corpus, and `RecordingMaintenance capture` now auto-cleans duplicate `Bot/*/Recordings` output trees after each capture run so replay calibration stops reintroducing the large debug-output copy.
  - The promoted lower-route/elevator fixtures still preserve the intended parity evidence shape: compact packet-backed underground seating, elevator boarding while underground, upper-deck disembark, and upper-door block coverage, while reducing noise from the earlier failed/native-fallback attempts.
  - Session 180 kept the replay-backed terrain/WMO/dynamic wall fixtures green while shipping the binary-backed selected-plane Z correction and radius clamp from the local `0x635C00` helper into grounded `resolveWallSlide(...)`.
  - The rebuilt native DLL still held `MovementControllerPhysics`, `GroundMovement_Position_NotUnderground`, the Durotar wall-slide replay, the Blackrock Spire WMO stalls, the packet-backed Undercity upper-door block, and the aggregate drift gate after one transient rebuild lock retry.
  - Session 179 kept the replay-backed terrain/WMO/dynamic wall fixtures green while shipping the binary-backed `0.001f` horizontal pushout from the local `0x635D80` helper into grounded `resolveWallSlide(...)`.
  - The rebuilt native DLL still held `MovementControllerPhysics`, `GroundMovement_Position_NotUnderground`, the Durotar wall-slide replay, the Blackrock Spire WMO stalls, the packet-backed Undercity upper-door block, and the aggregate drift gate after one transient rebuild lock retry.
  - Session 178 corrected the grounded `0x636610` mapping in `PhysicsEngine.cpp`: the three-axis case now selects the lone axis from the minority orientation group, and the four-axis case now zeroes the merged blocker vector.
  - The rebuilt native DLL still held `MovementControllerPhysics`, `GroundMovement_Position_NotUnderground`, the Durotar wall-slide replay, the Blackrock Spire WMO stalls, the packet-backed Undercity upper-door block, and the aggregate drift gate after one transient rebuild lock retry.
  - Session 177 kept the new replay-backed terrain/WMO/dynamic wall fixtures green while shipping one more binary-backed grounded blocker merge rule: the three-axis `0x636610` case in `PhysicsEngine.cpp` now zeroes the merged blocker vector instead of selecting the first axis.
  - The rebuilt native DLL still held `MovementControllerPhysics`, `GroundMovement_Position_NotUnderground`, the Durotar wall-slide replay, the Blackrock Spire WMO stalls, the packet-backed Undercity upper-door block, and the aggregate drift gate.
  - Session 176 added the missing replay-backed wall fixtures the native parity loop needed: `PhysicsReplayTests` now verifies terrain wall-slide deflection in Durotar, repeated WMO contact stalls in Blackrock Spire, and the upper-door clamp during the packet-backed Undercity elevator ride.
  - Session 176 also shipped one native blocker-axis reduction in `PhysicsEngine.cpp`: grounded `buildMergedBlockerNormal(...)` no longer drops later distinct blocker axes with the `score + 0.1` filter once the best opposing axis has been chosen as primary.
  - The updated focused slice stayed green after the native rebuild, so the new terrain/WMO/dynamic-object wall regressions, the slope false-wall guard, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay gate all held under the slimmer blocker-axis heuristic.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (3/3)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=Navigation.Physics.Tests.PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (3/3)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `passed (29/29)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `passed (38/38)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~FrameAheadIntegrationTests.JumpArc_FlatGround_PeakHeightMatchesPhysics|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `No code/tests run in session 185; docs-only planning update.`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"` -> `3 passed`
  - `dotnet run --project tools/RecordingMaintenance/RecordingMaintenance.csproj --configuration Release -- compact` -> `26 logical recordings, 411.67 MiB canonical corpus, 0 sidecars refreshed, duplicate Bot/*/Recordings copies missing/clean`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `35 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"` -> `1 passed`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `13 passed`
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios 13_undercity_lower_route,14_undercity_elevator_west_up --timeout-minutes 8 --configuration Release` -> succeeded; produced `Urgzuga_Undercity_2026-03-25_10-00-52` and `Urgzuga_Undercity_2026-03-25_10-01-09`
- Next command: `py -c "from capstone import *; import pathlib; code=pathlib.Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x635090; data=code[start-0x400000:start-0x400000+1024]; [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') for i in md.disasm(data, start)]"`
- Files changed:
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsBridge.h`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Services/PathfindingService/Repository/Physics.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/FrameAheadIntegrationTests.cs`
  - `docs/physics/0x633840_disasm.txt`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `Tools/RecordingMaintenance/Program.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Blockers:
  - The current deterministic harness now exposes blocker selection directly; the next missing visibility is the paired `0xC4E544` selector payload and which `0x6351A0` branch produced it.
  - The best next tester is still the production-DLL deterministic harness, not a separate native test binary. The missing visibility is inside the selected-contact producer chain, so the next high-leverage delta is a transaction/export seam that records the chosen index and paired selector payload.
  - The new frame-15 contact probe proves the helper body is not the immediate blocker. Without the binary-selected contact / grounded-wall-state path, a blanket stateful `0x6334A0` call would also promote many walls in the same merged query.
  - The exact grounded post-`TestTerrain` wall/corner resolution helper is still unresolved in the binary; the current native baseline now has the correct top-level branch precedence, but it still lacks the real `0x6334A0` walkability logic and the remaining `0x636100` return-code / movement-fraction bookkeeping around `0x635C00` / `0x635D80`.
  - Do not route the new `0x6334A0` helper into live grounded resolution again until `TestTerrainAABB` contact orientation and the post-query `0x637330` normal-flip path are parity-safe; the first direct hookup already regressed both live Durotar routes and was reverted.
  - Do not replace merged-query `contact.walkable` with unconditional `CheckWalkable(..., groundedWallFlagBefore=true)` or any equivalent per-contact broadcast; the new Undercity frame-15 coverage proves that would bless unrelated walls.
  - The remaining-move retry attempt has already been disproved locally and must not be retried without new binary evidence.
  - Verified replay-backed wall fixtures now exist; do not reuse the stale Stormwind / RFC / Un'Goro coordinate probes as parity evidence.
  - Managed movement no longer lacks packet-backed recordings, but exact live FG/BG ordering around heartbeat-before-stop edges, facing corrections, and corpse-run/combat pause-resume timing still needs matched live traces.
## Prior Session
- Last updated: `2026-03-24 (session 171)`
- Pass result: `delta shipped`
- Last delta:
  - Removed the remaining custom grounded wall-contact sort from `CollisionStepWoW`, so the contact-plane slide path no longer re-ranks blocking planes by distance / depth / horizontal-normal magnitude.
  - Added `PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`, which pins a real recorded Durotar wall-slide window and verifies the replay keeps the same sustained deflection signature.
  - Corrected `docs/physics/wow_exe_decompilation.md`: local `WoW.exe` disassembly shows `0x637330` is the vec3-negation helper used after `TestTerrain`, not the unresolved grounded slide helper.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore` -> `succeeded` (existing warnings only)
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.ComplexMixed_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ValleyOfTrialsSlopeTests.StuckPosition_ExactServiceValues_ShouldMoveForward|FullyQualifiedName~ValleyOfTrialsSlopeTests.SteepDescent_50msTicks_GroundNormalTracksSlopeSupport|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `35 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"` -> `1 passed`
- Files changed:
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- Blockers:
  - `0x637330` is now closed as the vec3-negation helper, but the exact grounded post-query wall/corner helper is still unresolved.
  - The first two candidate wall fixtures (`Ragefire Chasm` corridor and `Un'Goro` crater wall) do not report real wall hits in current data, so the next parity pass needs a verified wall trace instead of reusing those stale coordinates.
  - The next parity audit still needs to replace the remaining grounded wall/corner heuristics branch-by-branch after the merged query volume is built.
- Next command:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~FrameByFramePhysicsTests.StormwindCity_WalkIntoWall_Blocked" --logger "console;verbosity=detailed"`
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
  - The next parity audit still needs to replace the remaining runtime-grounded differences branch-by-branch, especially wall/slide handling and any static-overlap shortcuts that still bypass the client’s sweep flow.
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`

## Earlier Sessions
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
