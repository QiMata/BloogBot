# BackgroundBotRunner Tasks

## Scope
- Directory: `Services/BackgroundBotRunner`
- Project: `BackgroundBotRunner.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: headless runner lifecycle, docker packaging, and FG/BG behavior parity through the shared BotRunner stack.

## Execution Rules
1. Keep changes scoped to the worker plus directly related startup/config call sites.
2. Every parity or lifecycle slice must leave a concrete validation command in `Session Handoff`.
3. Never blanket-kill repo processes; use repo-scoped cleanup or explicit PIDs only.
4. Archive completed items to `Services/BackgroundBotRunner/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `BBR-PAR-001` FG/BG action and movement parity
Known remaining work in this owner: `2` items.
- [ ] Continue tracing the remaining follow-loop and interaction timing divergences against the now-complete FG interaction surface.
- [x] Keep the now-cleared candidate `3/15` mining route as a regression check, then move the active live audit to corpse-run reclaim timing plus paired movement packet traces.
- [ ] Capture a matching FG trace or replay for the same now-green corpse/combat route segment and compare combat pause/resume timing plus corridor ownership, now that the start/stop packet edges are closed.
- [x] After the mining stall is closed, re-run at least one corpse-run and one combat-travel segment before broadening back out to the larger BG parity sweep.

2. `BBR-PAR-002` Live gathering/NPC timing
- [ ] Re-run the existing gathering and NPC interaction parity work once the dockerized vmangos stack is online so visibility timing is measured against the new environment.

3. `BBR-DOCKER-001` Containerized worker validation
- [ ] Validate the standalone BG container profile and the `WoWStateManager`-spawned BG worker path against the same endpoint contract.

## Session Handoff
- Last updated: 2026-03-25 (session 187)
- Active task: `BBR-PAR-001`
- Last delta:
  - Session 187 closed the forced-turn Durotar stop-tail mismatch that had been blocking the BG live audit. BG now queues a grounded stop while airborne and clears it on the first grounded frame, so the route no longer carries `FORWARD` through the destination after FG has already settled.
  - The same live route now proves both packet edges: FG and BG still match on `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`, neither emits late outbound `SET_FACING` after the opening pair, both end on outbound `MSG_MOVE_STOP`, and the latest stop-edge delta is `50ms`.
  - The remaining BackgroundBotRunner parity work is now the pause/resume and corridor-ownership slice on the already-green corpse/combat routes, plus the broader follow-loop and interaction timing drift outside this route family.
  - Session 186 added `BackgroundPacketTraceRecorder` and wired it through `BackgroundBotWorker`, so BG live parity runs now emit stable `packets_<account>.csv` artifacts alongside `physics_<account>.csv`, `transform_<account>.csv`, and `navtrace_<account>.json`.
  - Session 186 used that new sidecar on a forced-turn Durotar route. The live capture now proves BG matches FG on the opening `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD` edge instead of inferring it from a single packet field in the physics CSV.
  - The remaining BackgroundBotRunner live-parity gap is narrower: the forced-turn route is now closed through the stop edge, so the next BG-owned live slice is pause/resume timing and corridor ownership on the same route family.
  - Session 184 turned the corpse-run proof into real controller-ownership evidence: the BG live test now wraps `RetrieveCorpseTask` in diagnostic recording, writes `navtrace_TESTBOT2.json`, and asserts the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
  - Session 184 also fixed the live recording consumers to use the stable artifact filenames (`physics_<account>.csv`, `transform_<account>.csv`, `navtrace_<account>.json`) instead of the old timestamped wildcard assumption, which keeps repeated live runs from growing the diagnostics directory.
  - The mining stall, corpse-run reclaim, and combat-travel proof slices are still green. After session 187, the remaining BG parity work is the paired FG/BG movement trace capture for pause/resume ordering and corridor ownership on those same route segments.
- Pass result: `BG stop-edge parity shipped; 2 BackgroundBotRunner-owned items remain`
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`; BG now ends on outbound `MSG_MOVE_STOP` with no late outbound `SET_FACING`
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath" --logger "console;verbosity=normal"` -> `passed (1/1)`; stable BG packet sidecar confirmed
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)` twice; start-edge parity proven, stop-tail mismatch captured
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=normal"` -> `passed (1/1)`
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPacketTraceRecorder.cs`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Exports/BotRunner/BotRunnerService.Diagnostics.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelper.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelperTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `Get-Content Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs | Select-Object -Skip 320 -First 220`
- Blockers: the mining candidate `3/15` stall, the corpse-run harness issue, and the forced-turn stop-tail mismatch are all closed. The next live issue is paired FG/BG pause/resume ownership evidence, not harness cleanup; keep the walkable-triangle-preserving smoothing follow-up deferred until those higher-priority trace gaps are closed.
