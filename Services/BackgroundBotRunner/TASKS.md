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
Known remaining work in this owner: `0` items.
- [x] Session 188 closed the pause/resume gap: `Parity_Durotar_RoadPath_Redirect` proves matched FG/BG packet timing on mid-route redirects. BG `SET_FACING` fix shipped.
- [x] Keep the now-cleared candidate `3/15` mining route as a regression check, then move the active live audit to corpse-run reclaim timing plus paired movement packet traces.
- [x] Session 188 full live proof bundle green: forced-turn Durotar (start + stop edges), redirect parity, combat auto-attack, corpse-run reclaim.
- [x] After the mining stall is closed, re-run at least one corpse-run and one combat-travel segment before broadening back out to the larger BG parity sweep.

2. `BBR-PAR-002` Live gathering/NPC timing
- [ ] Re-run the existing gathering and NPC interaction parity work once the dockerized vmangos stack is online so visibility timing is measured against the new environment.

3. `BBR-DOCKER-001` Containerized worker validation
- [ ] Validate the standalone BG container profile and the `WoWStateManager`-spawned BG worker path against the same endpoint contract.

## Session Handoff
- Last updated: 2026-04-03 (session 296)
- Active task: `BBR-PAR-001`
- Last delta:
  - Session 296 removed the last permanent startup gate on the BG scene-slice path. `BackgroundBotWorker` no longer uses a one-shot `IsTcpEndpointReachable(...)` probe to decide whether local physics gets `SceneDataClient`; if the scene-data endpoint is configured, the worker now keeps the slice client and lets it connect on demand.
  - Supporting transport work landed one layer lower: `ProtobufSocketClient` now supports deferred initial connect, and `SceneDataClient` uses that path with a bounded `1.5s` connect budget plus a short retry backoff so a late `SceneDataService` does not stall startup or hammer a dead socket every frame.
  - Practical implication for this owner: BG runners are no longer permanently downgraded to heavier local-preloaded-map physics just because `SceneDataService` missed an early startup window; they can stay on the intended thin-slice contract once the service comes up.
  - Focused deterministic coverage is green (`BackgroundPhysicsModeResolverTests`, `ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts`), but the shared-tree AV rerun did not reach the old `[AV:EnterWorld]` bottleneck. `logs/av_allbotsenterworld_20260403_deferred_scene_client_rerun.log` shows the test host aborted during `PathfindingService` preload (`Map 229`) before `SceneDataService` or AV bring-up could be measured.
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests|FullyQualifiedName~BotRunner.Tests.IPC.ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts" --logger "console;verbosity=minimal"` -> `passed (14/14)`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath logs/av_allbotsenterworld_20260403_deferred_scene_client_rerun.log` -> `aborted`; test host crashed while `PathfindingService` was still preloading maps
  - Session 294 removed the last startup downgrade that was still pushing AV BG workers onto shared physics. `BackgroundBotWorker` now resolves the scene-data endpoint from config or the `WWOW_SCENE_DATA_*` env vars (default `127.0.0.1:5003`) and constructs `SceneDataClient` directly instead of permanently falling back because a one-shot reachability probe missed the service at bot startup.
  - Practical implication for this owner: the BG worker now stays on the intended scene-backed local path as long as the service is configured, and it no longer loses that path just because `SceneDataService` was a fraction of a second late to listen during AV bring-up.
  - Validation:
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -o E:\tmp\isolated-background-botrunner2\bin --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - Session 293 tightened the scene-backed local physics memory profile instead of changing worker startup again. BG runners that opt into `SceneDataClient` now enable native thin-scene-slice mode, which keeps `Navigation.dll` on explicitly injected nearby geometry and avoids implicit full-map `.scene` / VMAP loads inside each runner.
  - The controller setup now only toggles the native slice mode for native-local controller variants, so shared-physics BG/FG controllers do not gain a hard `Navigation.dll` construction dependency just from the new scene-slice control.
  - Practical implication for this owner: the local in-process physics path should now carry much less native scene memory per runner during AV launch pressure. The next measurement is the focused AV first-objective live slice on the same benchmark host.
  - Validation:
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -o E:\tmp\isolated-background-botrunner\bin --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin3 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - Session 290 switched BG runners to the scene-backed local physics path by default. `BackgroundBotWorker` now resolves `WWOW_BG_PHYSICS_MODE` to local in-process physics unless explicitly forced to `shared`, probes `SceneDataService`, and initializes `WoWSharpObjectManager` with `sceneDataClient` so `MovementController` can step `Navigation.dll` locally.
  - The worker no longer constructs `LocalPhysicsClient`. If `SceneDataService` is unavailable, it logs the condition and falls back to shared `PathfindingService` physics instead of failing startup.
  - Deterministic coverage stayed focused on the config seam (`BackgroundPhysicsModeResolverTests`) while the controller-side native nearby-object marshal is covered in `WoWSharpClient.Tests`.
  - Session 230 put the BG packet sidecar behind the shared `WWOW_ENABLE_RECORDING_ARTIFACTS` flag. `BackgroundPacketTraceRecorder` and `BackgroundBotWorker` now leave `packets_<account>.csv` disabled unless the caller explicitly opts into recording.
  - The same session kept the intentional capture flows working by forwarding the env var from the BotRunner/StateManager test fixtures and the recording-maintenance tool instead of assuming every BG launch should write files.
  - Cleaned the untracked repo artifact trees that had accumulated from repeated live runs: `Bot/*/Recordings`, `Bot/*/WWoWLogs`, `Bot/*/botrunner_diag.log`, and `TestResults/*`.
  - Session 187 closed the forced-turn Durotar stop-tail mismatch that had been blocking the BG live audit. BG now queues a grounded stop while airborne and clears it on the first grounded frame, so the route no longer carries `FORWARD` through the destination after FG has already settled.
  - The same live route now proves both packet edges: FG and BG still match on `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`, neither emits late outbound `SET_FACING` after the opening pair, both end on outbound `MSG_MOVE_STOP`, and the latest stop-edge delta is `50ms`.
  - The remaining BackgroundBotRunner parity work is now the pause/resume and corridor-ownership slice on the already-green corpse/combat routes, plus the broader follow-loop and interaction timing drift outside this route family.
  - Session 186 added `BackgroundPacketTraceRecorder` and wired it through `BackgroundBotWorker`, so BG live parity runs now emit stable `packets_<account>.csv` artifacts alongside `physics_<account>.csv`, `transform_<account>.csv`, and `navtrace_<account>.json`.
  - Session 186 used that new sidecar on a forced-turn Durotar route. The live capture now proves BG matches FG on the opening `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD` edge instead of inferring it from a single packet field in the physics CSV.
  - The remaining BackgroundBotRunner live-parity gap is narrower: the forced-turn route is now closed through the stop edge, so the next BG-owned live slice is pause/resume timing and corridor ownership on the same route family.
  - Session 184 turned the corpse-run proof into real controller-ownership evidence: the BG live test now wraps `RetrieveCorpseTask` in diagnostic recording, writes `navtrace_TESTBOT2.json`, and asserts the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
  - Session 184 also fixed the live recording consumers to use the stable artifact filenames (`physics_<account>.csv`, `transform_<account>.csv`, `navtrace_<account>.json`) instead of the old timestamped wildcard assumption, which keeps repeated live runs from growing the diagnostics directory.
  - The mining stall, corpse-run reclaim, and combat-travel proof slices are still green. After session 187, the remaining BG parity work is the paired FG/BG movement trace capture for pause/resume ordering and corridor ownership on those same route segments.
- Pass result: `BG workers now keep a deferred scene-slice client instead of committing to local-preload fallback at startup`
- Validation/tests run:
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded` (existing `dumpbin` warning from `vcpkg.nuget` still present)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
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
  - `Exports/BotCommLayer/ProtobufSocketClient.cs`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/BackgroundBotRunner/BackgroundPhysicsMode.cs`
  - `Tests/BotRunner.Tests/BackgroundPhysicsModeResolverTests.cs`
  - `Tests/BotRunner.Tests/IPC/ProtobufSocketPipelineTests.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPacketTraceRecorder.cs`
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
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"`
- Blockers: the mining candidate `3/15` stall, the corpse-run harness issue, and the forced-turn stop-tail mismatch are all closed. The next live issue is paired FG/BG pause/resume ownership evidence, not harness cleanup; keep the walkable-triangle-preserving smoothing follow-up deferred until those higher-priority trace gaps are closed.
