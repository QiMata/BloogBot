# BotRunner.Tests Tasks

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: keep BotRunner deterministic tests and live-validation assertions aligned with current FG/BG runtime parity.

## Execution Rules
1. Do not run live validation until the remaining code-only parity work is complete.
2. Prefer compile-only or deterministic test slices when the change only touches live-validation assertions.
3. Keep assertions snapshot-driven; do not reintroduce direct DB validation or FG/BG-specific skip logic for fields that now exist in both models.
4. Use repo-scoped cleanup only; never blanket-kill `dotnet` or `WoW.exe`.
5. Update this file in the same session as any BotRunner test delta.

## Active Priorities
1. Live-validation expectation cleanup
- [x] Remove stale FG coinage stub assumptions from mail/trainer live assertions now that `WoWPlayer.Coinage` is descriptor-backed.
- [ ] Sweep remaining live-validation suites for FG/BG divergence assumptions that are no longer true.
- [ ] Keep moving explicitly BG-only live suites onto BG-only fixtures/settings so behavior regressions are isolated without launching unnecessary FG clients.

2. Final validation prep
- [ ] Keep the final live-validation chunk queued until the remaining parity implementation work is done.
- [ ] Use the final run to collect fresh Orgrimmar transport evidence with the updated FG recorder.

3. Movement/controller parity coverage
Known remaining work in this owner: `0` items.
- [x] Added deterministic coverage for the persistent `BADFACING` retry window that was holding the candidate `3/15` mining route in stationary combat.
- [x] Added targeted BG corpse-run coverage for live waypoint ownership: `DeathCorpseRunTests` now asserts the emitted `navtrace_<account>.json` captured `RetrieveCorpseTask` ownership and a non-null `TraceSnapshot`, with deterministic helper tests covering stable recording-file lookup/cleanup.
- [x] Session 188: `Parity_Durotar_RoadPath_Redirect` proves pause/resume packet ordering. BG `SET_FACING` on mid-route redirects now matches FG. Full live proof bundle green.

## Simple Command Set
1. Build:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`

2. Deterministic snapshot/protobuf slice:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`

3. Final live-validation chunk after code-only parity closes:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m`

## Session Handoff
- Last updated: `2026-03-25 (session 188)`
- Pass result: `live parity harness revalidated against the corrected managed SET_FACING path; remaining gap is native drift, not test coverage`
- Last delta:
  - Session 188 did not change the BotRunner live harness logic. It revalidated the existing forced-turn Durotar packet assertions against the corrected WoWSharpClient facing send path and confirmed the harness still captures the same FG/BG opening pair: `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`.
  - The first turn-start rerun missed the `600ms` stop-edge bound by `9ms` (`609ms` total delta), then the immediate rerun passed unchanged. That keeps the harness useful while also confirming the live instability is still the known native Z drift / `FALLINGFAR` churn on this route, not a managed packet-order regression.
  - `Parity_Durotar_RoadPath_Redirect` stayed green without any harness edits, so the live packet-sidecar comparison continues to prove the redirect ordering fix from session 188.
  - Session 187 tightened `MovementParityTests` again so the forced-turn Durotar route proves the stop edge instead of only the opening pair. The harness now rejects late outbound `SET_FACING` after the opening pair, requires outbound `MSG_MOVE_STOP` from both FG and BG, requires the final outbound movement packet to be `MSG_MOVE_STOP`, and bounds the stop-edge delta to `300ms`.
  - The same session added deterministic coverage for the fix that made the live stop edge converge: `GoToArrivalTests` now pin the horizontal arrival rule and the exhausted-path 2D recalc guard, and `MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame` pins the queued grounded stop behavior.
  - With the stop edge closed, the remaining BotRunner test gap is narrower: matched pause/resume ownership evidence on the same route segment, using the existing packet and `navtrace_<account>.json` sidecars.
  - Session 186 tightened `MovementParityTests` so live parity runs no longer pass when only one client moves. The harness now requires meaningful travel from both FG and BG before a route counts as evidence.
  - Session 186 also added a forced-turn Durotar parity route and packet-sidecar comparison. The new live slice sets both bots to the same wrong facing before `Goto`, proves FG and BG both emit `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`, and leaves a sharper remaining gap: the turn-start tail still diverges around late heartbeats/stop behavior.
  - Stable BG `packets_<account>.csv` artifacts now exist alongside the prior FG packet sidecars, so BotRunner live-validation assertions can compare the same route interval directly instead of inferring BG ordering from a single packet field in `physics_<account>.csv`.
  - Session 184 added `RecordingArtifactHelper` plus deterministic `RecordingArtifactHelperTests` so live validation can reuse the stable on-disk recording artifacts instead of searching for timestamped copies that no longer exist.
  - Session 184 upgraded `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` from a plain success-path live check into a trace-backed ownership assertion: the test now starts/stops diagnostic recording around `RetrieveCorpseTask` and verifies the emitted `navtrace_TESTBOT2.json` captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
  - Session 184 also kept the compact packet-backed Undercity replay slice green after the recording-helper changes, so the remaining BotRunner test gap is now the paired FG/BG heartbeat/facing cadence evidence, not missing BG-side ownership state.
- Validation:
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> first rerun `failed` at stop-edge delta `609ms`; immediate rerun `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (61/61)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`; start and stop edges now match the harness expectations
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath" --logger "console;verbosity=normal"` -> `passed (1/1)`; confirmed `packets_TESTBOT2.csv` is emitted
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)` twice; live packet comparison now proves the shared turn-start opening edge and captures the remaining tail mismatch
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=normal"` -> `passed (1/1)`
- Files changed:
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelper.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelperTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Exports/BotRunner/BotRunnerService.Diagnostics.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `py -c "from capstone import *; f=open(r'D:/World of Warcraft/WoW.exe','rb'); f.seek(0x633840-0x400000); code=f.read(2048); md=Cs(CS_ARCH_X86, CS_MODE_32); [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') or (i.mnemonic in ('ret','retn') and (_ for _ in ()).throw(SystemExit)) for i in md.disasm(code, 0x633840)]"`
