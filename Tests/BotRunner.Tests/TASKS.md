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
- [ ] Keep the Ratchet fishing slice split into completed FG packet-capture reference work versus the remaining comparison/instrumentation work. Focused FG capture and the focused dual Ratchet path test are green on the current binaries; the remaining open work is authoritative staged local-pool activation/visibility attribution on nondeterministic reruns plus the actual FG/BG packet-sequence comparison.

2. Alterac Valley live-validation expansion
- [ ] Reduce `BackgroundBotRunner` per-instance memory / launch pressure so AV can actually bring all `80` accounts in-world on the `64 GB` benchmark host; the `2026-04-02` AV benchmark stalled at `39/80` with `BackgroundBotRunner` `p95 private=64.8 GB` across `55` instances and launch never progressed past `AVBOTA16`.
- [ ] Get the existing AV first-objective live slice green once launch pressure is low enough for all `80` accounts to enter world; the test already exists, but the `2026-04-02` benchmark still stalled at `39/80` before queue/objective movement began.

3. Final validation prep
- [ ] Keep the final live-validation chunk queued until the remaining parity implementation work is done.
- [ ] Use the final run to collect fresh Orgrimmar transport evidence with the updated FG recorder.

4. Movement/controller parity coverage
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
- Last updated: `2026-04-03 (session 297)`
- Pass result: `StateManager-managed-process constraints now exclude Pathfinding/SceneData external services`
- Last delta:
  - Session 297 removed the remaining fixture/runtime assumptions that `WoWStateManager` owns `PathfindingService`/`SceneDataService`. `BotServiceFixture` cleanup now only tears down `StateManager`, `WoW.exe`, and `BackgroundBotRunner` processes it owns; it no longer kills pathfinding/scene service processes.
  - Updated live-fixture wording so crash-state checks are scoped to StateManager-managed processes only (`StateManager` + `WoW.exe`).
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - Session 296 removed the permanent scene-slice startup downgrade from the AV path. `BackgroundBotWorker` now keeps a deferred `SceneDataClient` whenever the endpoint is configured, `ProtobufSocketClient` can connect lazily, and `SceneDataClient` now retries boundedly instead of forcing an early yes/no decision on slice mode.
  - Added focused deterministic coverage in `BackgroundPhysicsModeResolverTests`, `ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts`, and `SceneDataClientTests`.
  - The new shared-tree AV rerun did not produce fresh `[AV:EnterWorld]` evidence. `logs/av_allbotsenterworld_20260403_deferred_scene_client_rerun.log` shows the test host aborted while `PathfindingService` was still preloading maps (`Map 229`), so this session did not yet confirm whether the deferred scene-slice contract moves the AV ceiling.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientTests|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests|FullyQualifiedName~BotRunner.Tests.IPC.ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts" --logger "console;verbosity=minimal"` -> `passed (14/14)`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath logs/av_allbotsenterworld_20260403_deferred_scene_client_rerun.log` -> `aborted`; test host crashed during `PathfindingService` preload before AV bring-up
  - Session 296 closed the remaining RFC coordinator-refactor item. `RfcBotFixture` now disables the coordinator during fixture prep, clears stale group state before staging, and re-enables the coordinator only after revive/level/spell/gear/Orgrimmar prep completes.
  - `DungeoneeringCoordinator` now transitions from `WaitingForBots` straight into `FormGroup_Inviting`, so the RFC path no longer needs the old prep phases to begin group formation.
  - Added deterministic coverage in `CoordinatorStrictCountTests` for the new contract: RFC waits for every bot before group formation starts, and the coordinator-driven group/teleport flow never emits prep chat commands like `.learn`, `.character level`, `.reset`, or `.additem`.
  - Session 295 closed the last controller/startup seam in the pure-local fallback path. `WoWSharpObjectManager` now creates `MovementController` when BG runners request local physics without any remote/shared client, which is the path they take after `WoWStateManager` continues past an unavailable `SceneDataService`.
  - `WoWStateManager` now waits only `2.5s` for `SceneDataService` before continuing, so the AV fixture no longer spends two minutes blocked on the dead `5003` socket before any BG bring-up can happen.
  - The latest shared-tree AV first-objective rerun confirms that shift in failure mode: `WoWStateManager` reached `READY`, logged the pure-local fallback, and the run stalled later at `[AV:EnterWorld] 40/80` instead of skipping or blocking on scene-service startup. The missing accounts were the full alliance roster, so the remaining live blocker is launch pressure / launch ordering, not the old hover gate.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CoordinatorStrictCountTests|FullyQualifiedName~CoordinatorFixtureBaseTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient|FullyQualifiedName~ObjectManagerWorldSessionTests.EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController" --logger "console;verbosity=minimal"` -> `passed (2/2)`
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"` -> `failed` after `5.2m` at `[AV:EnterWorld] STALE - bot count stopped at 40/80 for 123s`; `WoWStateManager` hit `READY`, scene-data fallback engaged, and the missing half was `AVBOTA1-40`
  - Session 294 fixed the remaining controller/startup seams behind the AV hover regression. `MovementController` now keeps post-teleport bots falling through a real no-ground grace window, rejects overhead support during that settle window, and no longer finalizes the snap mid-air above the battlemaster staging point.
  - `WoWStateManager` now forwards `SceneDataService__*` to spawned BG workers, and `BackgroundBotWorker` no longer permanently downgrades to shared physics because a one-shot scene-service reachability probe missed startup.
  - Deterministic validation is green. The isolated live rerun now gets further than before, but it still does not reach AV queue logic end-to-end: first the fixture had to be pointed at the isolated output tree with `WWOW_BOT_OUTPUT_DIR`, then `Navigation.dll` had to be staged manually, and the remaining isolated-output blockers are missing `Loader.dll` plus an unavailable `SceneDataService`.
  - Session 293 shipped the next AV launch-pressure reduction step instead of adding more live assertions. The scene-backed BG local path now enables thin-scene-slice mode, which keeps `Navigation.dll` on the injected nearby collision slice and prevents implicit full-map `.scene` / VMAP autoload inside each `BackgroundBotRunner`.
  - Added deterministic native proof in `SceneSliceModeTests.GetGroundZ_SceneSliceMode_DoesNotAutoloadFullSceneCache` plus managed controller proof in `MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode`.
  - Practical implication: the next AV rerun should measure a materially lower native memory footprint per BG runner. Until that rerun lands, the active AV work in this owner is still the same two items: reduce launch pressure enough for all `80` bots to enter world, then get the already-written first-objective slice green.
  - Validation:
    - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
    - `$env:WWOW_DATA_DIR='E:\repos\Westworld of Warcraft\Data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release -o E:\tmp\isolated-nav-physics-tests\bin --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneSliceModeTests.GetGroundZ_SceneSliceMode_DoesNotAutoloadFullSceneCache" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin3 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - Revalidated the existing `AlteracValleyFixture` / `AlteracValleyLoadoutPlan` contract with `BattlegroundFixtureConfigurationTests`; the deterministic slice confirms the 80-account level-60 roster, High Warlord / Grand Marshal foreground leaders, faction mounts, baseline elixirs, class/role loadouts, and first-objective assignments.
  - Removed the stale open AV fixture/loadout bullets from this file and archived them in `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`; the remaining active AV work in this owner is launch-pressure reduction and then getting the already-written first-objective live test green.
  - Validation again used an isolated output directory because the shared `Bot\Release\net8.0` output tree is still locked by active AV/background processes.
  - Rebuilt `Tests/BotRunner.Tests` in `Release`, then ran the focused live AV slice `AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective` under external process sampling and wrote the artifact bundle to `TestResults\\AVBenchmark_20260402_191811`.
  - The test still fails during coordinator initialization at `[AV:EnterWorld]`, stalling at `39/80` after `122s` of no progress; it never reached queue, mount, or objective movement.
  - The launch path has no hard concurrency cap in `Services/WoWStateManager/StateManagerWorker.cs`; it iterates the settings list sequentially with small delays, so the plateau is not an explicit max-bot setting.
  - On the `12`-logical-core / `63.93 GB` host, aggregate process telemetry hit `CPU p95=79.6%`, `private MB p95=65735.6`, `private MB max=69322.17`, `working set MB max=46622.07`, `handles p95=30884.4`, and `threads p95=1143.7`.
  - `BackgroundBotRunner` is the blocker: `max instances=55`, `private MB p95=64790.63`, `working set MB p95=40687.3`, which is about `1178 MB` private and `740 MB` working set per runner. Projected to `78` BG runners, that is about `91.9 GB` private and `57.7 GB` working set before foreground bots and fixed services.
  - Launch evidence matches the resource ceiling: `54` unique BG login attempts were observed (`AVBOT2-40` plus `AVBOTA2-16`), while `AVBOTA17-40` never started and the missing-account list at failure still included both FG leaders plus the remainder of the Alliance wave. The run also logged repeated `Enter world timed out ... Retrying CMSG_PLAYER_LOGIN` warnings and at least one transient `AUTH_LOGON_PROOF` failure under load.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin4 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall|FullyQualifiedName~MovementControllerTests.Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -o E:\tmp\isolated-background-botrunner2\bin --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release -o E:\tmp\isolated-wowstatemanager\bin --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -o E:\tmp\isolated-av-live2\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_QueueAndEnterBattleground" --logger "console;verbosity=minimal"` -> initial rerun `skipped` (`Live bot not ready`; fixture still used the wrong bot-output root)
  - `WWOW_BOT_OUTPUT_DIR=E:\tmp\isolated-av-live2\bin; dotnet test ... --logger "console;verbosity=normal"` -> advanced into full fixture startup, then exposed isolated-output blockers: `Navigation.dll` missing for `PathfindingService` on the first pass; after staging that DLL manually, the next rerun still timed out with `SceneDataService` unavailable and foreground launch failing on `Loader.dll` (`E:\Loader.dll`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -o E:\tmp\isolated-botrunner-tests\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal"` -> `passed (11/11)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --blame-hang --blame-hang-timeout 60m --logger "trx;LogFileName=av_benchmark.trx" --logger "console;verbosity=normal"` -> `failed ([AV:EnterWorld] STALE at 39/80; benchmark artifacts captured externally in TestResults\\AVBenchmark_20260402_191811)`
- Files changed:
  - `Exports/BotCommLayer/ProtobufSocketClient.cs`
  - `Exports/WoWSharpClient/Movement/SceneDataClient.cs`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/BackgroundBotRunner/BackgroundPhysicsMode.cs`
  - `Services/WoWStateManager/Program.cs`
  - `Tests/BotRunner.Tests/BackgroundPhysicsModeResolverTests.cs`
  - `Tests/BotRunner.Tests/IPC/ProtobufSocketPipelineTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/SceneDataClientTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorStrictCountTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RagefireChasmTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RfcBotFixture.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `Services/WoWStateManager/Program.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/Movement/NativeLocalPhysics.cs`
  - `Exports/WoWSharpClient/Movement/NativePhysicsInterop.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/SceneSliceModeTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `Get-ChildItem E:\repos\Westworld of Warcraft\Bot\Release\net8.0\WWoWLogs | Sort-Object LastWriteTime -Descending | Select-Object -First 80 Name,LastWriteTime`
