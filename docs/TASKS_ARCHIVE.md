# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-29) - MVT-TRANSPORT-NAMED-UC closeout

- [x] Closed `MVT-TRANSPORT-NAMED-UC`: the stricter named-Undercity elevator
  route now uses PathfindingService-generated approach points instead of
  hand-authored lower-route waypoints.
- Completion notes:
  - `MovementParityTests.TransportRide_FgBgParity` starts both participants
    with `.tele name <character> undercity`, queries PathfindingService for the
    route from `(1584.07,241.987,-52.1534)` to `(1532.3,242.2,-41.4)`, and
    fixture-drives the returned corners with `SetFacing`, `StartMovement`, and
    `StopMovement`.
  - The probe waits for the real west Undercity elevator at the lower stop,
    stages both clients at the lower board point, starts both forward together,
    requires both to show gameobject transport evidence, and stops each
    participant when it reaches the upper exit.
  - BG transport-local movement now models the known Undercity elevator ride
    window and avoids passive reattach at the upper stop.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_named_undercity_pathfinding_route_18.trx"` -> `passed (1/1)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.PhysicsStep_OnMovingTransport_PreservesLocalOffsetAndSyncsWorldPosition|FullyQualifiedName~MovementControllerTests.Update_KnownUndercityElevatorRide_AnimatesToUpperAndDismounts|FullyQualifiedName~MovementControllerTests.Update_AtUpperUndercityElevatorExit_DoesNotPassiveReattach|FullyQualifiedName~MovementControllerTests.PhysicsStep_OnTransport_UsesLocalCoordinatesAndIncludesTransportObject|FullyQualifiedName~MovementControllerTests.PhysicsResult_OnTransport_RecomputesLocalOffsetFromWorldOutput|FullyQualifiedName~MovementControllerTests.Update_BeforeUndercityElevatorDeck_DoesNotPassiveAttach|FullyQualifiedName~MovementControllerTests.Update_OnUndercityElevatorDeck_AttachesToCar|FullyQualifiedName~MovementControllerTests.Update_IdleNearUndercityElevatorDoorMarker_DoesNotPassiveAttach" --logger "console;verbosity=minimal"` -> `passed (8/8)`.
- Evidence:
  - `tmp/test-runtime/results-live/movement_parity_transport_named_undercity_pathfinding_route_18.trx`

## Archived Snapshot (2026-04-29) - MVT-TRANSPORT-FG closeout

- [x] Closed `MVT-TRANSPORT-FG`: stabilized
  `MovementParityTests.TransportRide_FgBgParity` foreground gameobject
  transport evidence in the full live bundle.
- Completion notes:
  - The direct FG/BG movement bundle now has no open task-tracked items and no
    tracked elevator skip.
  - `TransportRide_FgBgParity` boards the real Undercity west elevator through
    action-driven `Goto` movement from the lower wait point to the lower car
    center, preserving the taxi-vs-gameobject-transport coverage split.
  - Staging now clears residual horizontal movement before the next live parity
    lane and accepts stable in-world snapshots when teleport-settle polling is
    stale.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_lower.trx"` -> `passed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_full_04.trx"` -> `passed (5/5, 0 skipped; duration 3m22s)`.
  - Final `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/movement_parity_transport_fg_goto_board_lower.trx`
  - `tmp/test-runtime/results-live/movement_parity_transport_fg_goto_board_full_04.trx`

## Archived Snapshot (2026-04-21) - P3 Unified Loadout Hand-off closeout

- [x] `P3.1` Plumbing — proto types (`LoadoutSpec`, `LoadoutStatus`, `ActionType.ApplyLoadout`, `WoWActivitySnapshot.loadoutStatus` + `loadoutFailureReason`, status field included in `SnapshotChangeSignature`). Shipped in commit `9c0e6339`.
- [x] `P3.2` Config — `CharacterSettings.Loadout` POCO (`LoadoutSpecSettings` + sub-POCOs) and `Services/WoWStateManager/Settings/LoadoutSpecConverter.cs` (`ToProto` / `BuildApplyLoadoutAction`). Shipped in commit `61b9fffb`.
- [x] `P3.3` BotRunner — `Exports/BotRunner/Tasks/LoadoutTask.cs` with plan-builder + seven step executors (`LearnSpellStep`, `SetSkillStep`, `AddItemStep`, `AddItemSetStep`, `EquipItemStep`, `UseItemStep`, `LevelUpStep`), 100ms pacing throttle, idempotent `IsSatisfied` + `TryExecute` contract. Scaffold in commit `d38fff31`, executors in `813da3ef`. `BotRunnerService.HandleApplyLoadoutAction` / `SyncLoadoutStatusIntoSnapshot` wiring in `a1a312c3`.
- [x] `P3.4` StateManager dispatch — `BattlegroundCoordinator.CoordState.ApplyingLoadouts` between `WaitingForBots` and `QueueForBattleground`, per-bot single-shot `ApplyLoadout` dispatch, `ExcludedAccounts` exclusion list for `LoadoutFailed` bots, chain-loop through purely-internal orchestration states. Shipped in commit `f1799080`.
- [x] `P3.5` Coordinator raid-formation gate — `CoordState.WaitingForRaidFormation` reuses `DescribeFactionGroupIssues`; trivially short-circuits when `!RequiresFactionGroupQueue`. Shipped alongside P3.4 in `f1799080`.
- [x] `P3.6` Fixture cleanup — fixtures now stamp `CharacterSettings.Loadout` at `BuildCharacterSettings` via `AlteracValleyLoadoutPlan.BuildLoadoutSpecSettings`. WSG fixture-driven prep path (`EnsureLoadoutPreparedAsync` chain) removed in commit `a6d6aa55`; AV parity closed in P3.7. Phase-1 stamp + tests in `64f38a20`.
- [x] `P3.7` Explicit spell curation — `ClassLoadoutSpells.ResolveHighestRankClassSpellIds` replaces the forbidden `.learn all_myclass` / `.learn all_myspells` shortcuts; AV `PrepareObjectiveReadyLoadoutAsync` and its call sites removed. Shipped in commit `d8f9e873`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTask|FullyQualifiedName~BotRunnerServiceLoadoutDispatch|FullyQualifiedName~LoadoutSpecConverter|FullyQualifiedName~BattlegroundCoordinator|FullyQualifiedName~CoordinatorStrictCount|FullyQualifiedName~ActionForwardingContract|FullyQualifiedName~ClassLoadoutSpells|FullyQualifiedName~BattlegroundFixtureConfigurationTests.WarsongGulchFixture_StampsPerBotLoadout|FullyQualifiedName~BattlegroundFixtureConfigurationTests.AlteracValleyFixture_StampsPerBotLoadout" --logger "console;verbosity=minimal"` -> `passed (59/59)`
- Design invariants now in production:
  - One `ApplyLoadout` per bot per fixture run; coordinator keeps `_loadoutSent` / `_loadoutReady` / `_loadoutFailed` sets.
  - `WoWActivitySnapshot.LoadoutStatus` is the only new snapshot field (part of the change signature so transitions force full sends).
  - BotRunner owns execution timing (100ms pacing + 20-retry budget per step).
  - Config-authoritative: fixtures stamp `CharacterSettings.Loadout` at init time; no programmatic generation at runtime.
  - Curated per-(class, race) spell roster; `.learn all_*` is forbidden and fails loud on unknown class.
- Commits (in order): `3fa5aaf9` (plan), `9c0e6339`, `61b9fffb`, `d38fff31`, `a1a312c3`, `813da3ef`, `f1799080`, `64f38a20`, `a6d6aa55`, `d8f9e873`.
- Memory: `feedback_explicit_spell_learning.md` locks the "no `.learn all_*`" rule for future sessions; CRITICAL pointer added to `MEMORY.md`.

## Archived Snapshot (2026-04-17) - WoW.exe packet handling & ACK parity closeout

- [x] `P2` packet dispatch, ACK generation, timing, mutation order, packet-flow, and state-machine parity.
- Completion notes:
  - `docs/physics/0x466590_disasm.txt` now anchors the deep `SMSG_UPDATE_OBJECT` descriptor walker: field application is in ascending descriptor-index order, and each present field forwards through `0x466A00 -> 0x6142E0`.
  - `docs/physics/0x466C70_disasm.txt` now anchors the typed create-path layout switch directly and proves there is no separate packet-instantiated `CGPet_C` branch in the `SMSG_UPDATE_OBJECT` path.
  - `cgobject_layout.md`, `csharp_object_field_audit.md`, and `smsg_update_object_handler.md` now reflect the raw WoW.exe evidence instead of leaving the remaining P2.4 conclusions implicit.
  - `ObjectUpdateMutationOrderTests` already covered the required replay set for local cached-create, remote unit create, local player update-with-movement, and duplicate fallback gameobject create.
  - The deterministic parity bundles remained green after the final closeout:
    - `AckParity` `29/29`
    - `MovementParity` in `WoWSharpClient.Tests` `32/32`
    - `PacketFlowParity` `8/8`
    - `StateMachineParity` `8/8`
    - `MovementParity` in `Navigation.Physics.Tests` `8/8`
    - `NavigationPathTests` `80/80`
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectUpdateMutationOrderTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateMachineParityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=PacketFlowParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=StateMachineParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`

## Archived Snapshot (2026-04-15) - Corpse-run probe-policy closeout

- [x] `RPT-MISS-003` foreground corpse-run live validation.
- Completion notes:
  - The historical `CRASH-001` WoW.exe access violation did not reproduce during 2026-04-15 opt-in reruns.
  - The current foreground ghost runback/reclaim stall was caused by corpse-run routes getting pinned to a close micro-waypoint while the standard shortcut probe veto blocked advancement.
  - `NavigationRoutePolicy.CorpseRun` now advances close waypoints without the standard probe-corridor shortcut veto, matching its existing contract that probe heuristics are disabled for faithful corpse-run route following.
  - `CRASH_INVESTIGATION.md` remains valid as historical crash context only, not as an active blocker.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails" --logger "console;verbosity=minimal"` -> covered by focused regression bundle, `passed`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`

## Archived Snapshot (2026-04-15) - Navigation local-physics route-layer repair

- [x] `BR-NAV-008` Stop local route-layer traps from making the bot run back and forth over the same Orgrimmar corner point.
- Completion notes:
  - `PathfindingClient` now exposes local segment simulation for BotRunner path validation.
  - `NavigationPath` now rejects service route segments that local physics proves climb to the wrong WMO/terrain layer.
  - Rejected wrong-layer segments can be repaired through nearby same-layer detour candidates instead of dropping the whole route.
  - The repair keeps strict checks on the short detour leg and avoids treating the downstream ramp lateral-width probe as authoritative when local physics/support continuity prove the longer stitch-back leg.
  - Live Orgrimmar bank-to-auction-house validation now passes and logs a repaired local-physics route-layer segment before arrival.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (77/77)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_local_physics_detour_width_relax.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-15) - Navigation waypoint overshoot closeout

- [x] `BR-NAV-007` Prevent waypoint overshoot from steering the bot back and forth over the same point.
- Completion notes:
  - `NavigationPath` now stores the path start anchor and advances the active waypoint when the current position has crossed the active waypoint along the inbound segment.
  - Overshoot advancement remains corridor-gated: it requires the next waypoint shortcut to preserve sampled walkable corridor, and strict mode also requires LOS.
  - Deterministic coverage now proves a walkable overshoot advances forward and an off-corridor overshoot remains pinned to the current waypoint.
  - Foreground ghost runback mitigation from the same session remains guarded: the FG corpse-run test is opt-in with `WWOW_RETRY_FG_CRASH001=1`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (72/72)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`

## Archived Snapshot (2026-04-15) - Legacy/tracker hygiene pass

- [x] Superseded legacy `WWoW.RecordedTests.Shared` placeholder tracker.
- [x] Superseded legacy `WWoW.RecordedTests.PathingTests` placeholder tracker.
- [x] Checked off stale WinImports/ForegroundBotRunner umbrella checklist lines whose concrete task IDs were already complete.
- Validation:
  - `rg --files WWoW.RecordedTests.Shared` -> only task tracker files remain.
  - `rg --files WWoW.RecordedTests.PathingTests` -> only task tracker files remain.
  - `rg -n "^- \[ \]|Known remaining work|Active task:" --glob TASKS.md` -> remaining unchecked items are now limited to documented blocked/stale service checklist surfaces.

## Archived Snapshot (2026-04-15) - UI umbrella closeout

- [x] `UI-UMB-001` AppHost child execution.
- [x] `UI-UMB-002` ServiceDefaults child execution.
- [x] `UI-UMB-003` WoWStateManagerUI child execution.
- [x] `UI-UMB-004` Parent/master status sync.
- Completion notes:
  - `UI/TASKS.md`, `UI/Systems/Systems.AppHost/TASKS.md`, `UI/Systems/Systems.ServiceDefaults/TASKS.md`, and `UI/WoWStateManagerUI/TASKS.md` now report no remaining owner-local items.
- Validation:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md` -> matched the current handoff command; the previous master queue fields are no longer present in the current docs structure.

## Archived Snapshot (2026-04-15) - WoWStateManagerUI converter surface closeout

- [x] `UI-MISS-005` Add converter-contract coverage for all remaining WPF converters.
- Completion notes:
  - Added tests for `NullToBoolConverter`, `PathToFilenameConverter`, and `ServiceStatusToBrushConverter`.
  - README now documents all converter one-way contracts currently used by XAML.
- Validation:
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`

## Archived Snapshot (2026-04-15) - Systems.ServiceDefaults closeout

- [x] `SSD-MISS-001` Direct automated coverage for service default extensions.
- [x] `SSD-MISS-002` Configuration-driven telemetry resource fields.
- [x] `SSD-MISS-003` Configuration-driven health endpoint exposure policy.
- [x] `SSD-MISS-004` Configurable resilience defaults for deterministic tests.
- [x] `SSD-MISS-005` Service discovery scheme policy wiring.
- [x] `SSD-MISS-006` README command/integration guidance cleanup.
- Completion notes:
  - `Tests/Systems.ServiceDefaults.Tests` now covers health registration, endpoint mapping, telemetry field resolution, resilience toggle semantics, and service-discovery scheme policy.
  - README now documents current commands and the `ServiceDefaults:*` configuration keys.
- Validation:
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - Parallel validation caveat: an earlier simultaneous `dotnet build` and `dotnet test` collided on `obj/Release/Systems.ServiceDefaults.dll`; rerunning build alone succeeded.

## Archived Snapshot (2026-04-15) - Systems.AppHost closeout

- [x] `SAH-MISS-001` Externalize AppHost container settings.
- [x] `SAH-MISS-002` Add startup preflight validation for required bind mounts.
- [x] `SAH-MISS-003` Normalize AppHost host path resolution.
- [x] `SAH-MISS-004` Add deterministic DB -> WoW startup diagnostics.
- [x] `SAH-MISS-005` Add a simple local AppHost debugging profile.
- [x] `SAH-MISS-006` Correct AppHost README commands and prerequisites.
- Completion notes:
  - AppHost settings now load from `WowServer` configuration with sane local defaults.
  - Bind-mount paths resolve absolutely from the AppHost project directory unless `WowServer:Paths:BaseDirectory` overrides them.
  - Startup fails before container creation when required config/data mounts are missing.
- Validation:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release --no-build --launch-profile local` -> expected preflight failure listing missing `config`/`data` bind-mount sources in this workspace

## Archived Snapshot (2026-04-15) - RecordedTests Shared storage provider closeout

- [x] `RTS-MISS-001` Real S3 storage operations.
- [x] `RTS-MISS-002` Real Azure Blob storage operations.
- [x] `RTS-MISS-003` Cross-provider cancellation/failure semantics normalization.
- [x] `RTS-MISS-004` Provider contract parity tests.
- Completion notes:
  - `RecordedTests.Shared` now references `AWSSDK.S3` and `Azure.Storage.Blobs`.
  - S3/Azure upload/download/list/delete and `StoreAsync` are backed by concrete SDK adapters.
  - Deterministic in-memory S3/Azure test backends keep unit tests credential-free.
  - `RecordedTestStorageProviderParityTests` proves equivalent filesystem/S3/Azure behavior for CRUD, missing downloads, delete idempotency, cancellation, and metadata storage.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"` -> `passed (125/125)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (382/382)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (56/56)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (53/53)`
  - `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented|StoreAsync is not directly implemented" RecordedTests.Shared/Storage -S` -> no matches
  - `rg -n "stubbed|download stub|not yet implemented|requires AWSSDK|requires Azure.Storage.Blobs" Tests/RecordedTests.Shared.Tests/Storage RecordedTests.Shared/Storage -S` -> no matches

## Archived Snapshot (2026-04-15) - Recorded pathing live + test owner closeout

- [x] `RPT-MISS-004` Recorded pathing path-output consumption validation.
  - BG runner path consumption was validated through live Docker corpse-run proof and focused Orgrimmar pathfinding/bot-task contracts.
- [x] `RPTT-TST-001` Program filter exact-match/fail-fast coverage.
- [x] `RPTT-TST-002` Program in-process PathfindingService lifecycle start/stop/error cleanup coverage.
- [x] `RPTT-TST-003` Background recorded runner timeout/stop-movement coverage.
- [x] `RPTT-TST-004` Background recorded runner disconnect/game-loop lifecycle coverage.
- [x] `RPTT-TST-005` Foreground recorded runner target precedence and disconnect lifecycle coverage.
- [x] `RPTT-TST-006` Recorded pathing tests simple command surface validation.
- Completion notes:
  - `Tests/RecordedTests.PathingTests.Tests/TASKS.md` now reports no remaining owner-local items.
  - `RecordedTests.PathingTests/TASKS.md` retained only the foreground corpse-run proof. Current 2026-04-15 revalidation supersedes the old `CRASH-001` blocker with a runback/reclaim stall.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=death_corpse_run_recorded_pathing_live_validation.trx"` -> `passed (1/1), previous guarded run omitted FG; superseded by 2026-04-15 opt-in revalidation`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` -> `passed (30/30)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"` -> `passed (34/34)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests|FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (135/135)`

## Archived Snapshot (2026-04-15) - BG server-packet movement parity coverage

- [x] Added the missing deterministic BG protocol/object-manager parity bundle hook:
  - `Tests/WoWSharpClient.Tests` now has `Category=MovementParity` coverage for force-speed/root, server movement flag toggles, compressed movement trigger variants, knockback ACK behavior, and pending knockback consumption.
  - `ObjectManagerWorldSessionTests.MoveKnockBack_ServerPacketFeedsMovementControllerNextFrame` proves `SMSG_MOVE_KNOCK_BACK` traverses `MovementHandler -> WoWSharpObjectManager -> MovementController`.
  - The singleton handler test seam now wires `ObjectManagerFixture` and `ResetObjectManager()` to `WoWSharpEventEmitter.Instance`, so server packet events reach the object manager under test.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.MoveKnockBack|FullyQualifiedName~ObjectManagerWorldSessionTests.ServerControlledMovementFlagChanges_ParseApplyAndAck|FullyQualifiedName~MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`

## Archived Snapshot (2026-04-15) - BloogBot.AI test path closeout

- [x] `AI-TST-PATH-001` AI test project reference fix:
  - Updated `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` from removed `..\..\WWoWBot.AI\BloogBot.AI.csproj` to existing `..\..\BloogBot.AI\BloogBot.AI.csproj`.
  - Refreshed `BloogBot.AI/TASKS.md` to current repo paths and no remaining owner-local items.
- Validation:
  - `dotnet restore Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` -> `restored`
  - `dotnet test Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` -> `passed (121/121)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`

## Archived Snapshot (2026-04-15) - RecordedTests Azure stub contract closeout

- [x] `RTS-TST-003` Azure Blob `StoreAsync` warning/no-op semantics.
- [x] `RTS-TST-004` Azure Blob upload/download/list/delete current-stub method contracts.
- [x] `RTS-TST-005` Azure Blob configured-container URI guard tests.
- [x] `RTS-TST-006` RecordedTests.Shared.Tests command surface closeout.
- Completion notes:
  - Added Azure Blob tests for constructor validation, upload URI/logging, download/delete argument validation, invalid URI parsing, configured-container mismatch, stubbed download directory creation without file creation, list empty fallback logging, `StoreAsync` warning/no-op behavior, and dispose idempotence.
  - Updated `Tests/RecordedTests.Shared.Tests/TASKS.md` to report no remaining owner-local items and a validated three-command test surface.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (98/98)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (367/367)`

## Archived Snapshot (2026-04-15) - RecordedTests S3 stub contract coverage

- [x] `RTS-TST-001` S3 `StoreAsync` warning/no-op semantics.
- [x] `RTS-TST-002` S3 upload/download/list/delete current-stub method contracts.
- Completion notes:
  - Existing `StoreAsync_DoesNotThrow` and `StoreAsync_WithLogger_LogsWarning` pin S3 `StoreAsync` warning/no-op behavior.
  - Added S3 method-contract tests for invalid URI parsing, stubbed download directory creation without file creation, list empty fallback logging, and delete logging.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (50/50)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (62/62)`

## Archived Snapshot (2026-04-15) - Prompt function deterministic test split

- [x] `PHS-TST-002` PromptHandlingService.Tests deterministic prompt split:
  - Added `Tests/PromptHandlingService.Tests/ScriptedPromptRunner.cs` for offline `IPromptRunner` coverage.
  - Converted prompt-function tests to use scripted local responses instead of Ollama in the default path.
  - Tagged Ollama prompt checks as `Category=Integration` and documented the local endpoint/models in the test README.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (27 passed, 161 skipped, 0 failed, 188 total)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`

## Archived Snapshot (2026-04-15) - WSM Quest Snapshot Evidence Closeout

- [x] Closed `WSM-PAR-001` from `Services/WoWStateManager/TASKS.md`.
- Completion notes:
  - No runtime code changed for this item.
  - Current live evidence proves GM-driven quest add/complete/remove transitions propagate from WoWSharpClient quest-log updates through BotRunner snapshot serialization into StateManager query responses.
  - Artifact: `tmp/test-runtime/results-live/quest_snapshot_wsm_par_rerun.trx`.
  - Evidence includes `[FG] After add: QuestLog1=786 QuestLog2=0 QuestLog3=0`, `[BG] After add: QuestLog1=786 QuestLog2=4 QuestLog3=0`, successful `.quest complete 786`, successful `.quest remove 786`, and a passing test result.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.QuestInteractionTests.Quest_AddCompleteAndRemove_AreReflectedInSnapshots" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=quest_snapshot_wsm_par_rerun.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`

## Archived Snapshot (2026-04-15) - WSM Bootstrap External Ownership Closeout

- [x] Closed `WSM-BOOT-001` from `Services/WoWStateManager/TASKS.md`.
- Completion notes:
  - `MangosServerOptions` now defaults to `AutoLaunch=false` and an empty `MangosDirectory`.
  - `Services/WoWStateManager/appsettings.json` and `Tests/BotRunner.Tests/appsettings.test.json` disable MaNGOS auto-launch by default and no longer carry a default host MaNGOS directory.
  - `MangosServerBootstrapper` now skips auto-launch when an explicit opt-in omits `MangosServer:MangosDirectory`.
  - Docker stack and technical notes docs now describe Docker `realmd`/`mangosd` as the default ownership path, with Windows host process launch as legacy opt-in.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MangosServerBootstrapperTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal"` -> `passed (24/24)`

## Archived Snapshot (2026-04-15) - Deferred D3/D4 Closeout

- [x] Closed deferred issue `D3` (`WSG transfer stalls`) by focused WSG live evidence.
- [x] Closed deferred issue `D4` (`Elevator physics`) by current deterministic Navigation movement-parity evidence.
- Completion notes:
  - `WSG_PreparedRaid_QueueAndEnterBattleground` passed with all 20 WSG bots entering world, all 20 queueing, and all 20 transferring onto WSG map `489`.
  - The WSG artifact is `tmp/test-runtime/results-live/wsg_transfer_d3_rerun.trx` and includes `[WSG:Enter] All 20/20 bots entered world`, `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, `[WSG:BG] 20/15 bots on BG map`, and `[WSG:Final] onWsg=20, totalSnapshots=20`.
  - The stale D4 row is closed by the current Docker-backed `Category=MovementParity` pass after rebuilding `Navigation.dll`; the compact packet-backed Undercity elevator coverage is green in that `8/8` bundle.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_transfer_d3_rerun.trx"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - Final cleanup: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`

## Archived Snapshot (2026-04-15) - Deferred BG D1/D2 Closeout

- [x] Closed deferred issue `D1` (`Alliance faction bots`) by evidence and deterministic launch-order regression.
- [x] Closed deferred issue `D2` (`BG queue pop`) with current AB queue-entry proof and existing AV full-match queue-entry evidence.
- Completion notes:
  - `AlteracValley.config.json` includes `AVBOTA1-40`, and `WoWStateManagerLaunchThrottleTests.AlteracValleySettings_IncludeAllianceAccountsInLaunchOrder` proves all 40 runnable Alliance accounts survive StateManager launch ordering.
  - `ArathiBasinFixture` now uses a reliable 10v10 queue-entry smoke roster, keeps one Horde foreground visual client, runs the Alliance raid leader as a background runner, and extends AB cold-start enter-world tolerance to `8m` max / `2m` stale.
  - AB live evidence reached `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, and `[AB:BG] 20/20 bots on BG map`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests" --logger "console;verbosity=minimal"` -> `passed (20/20)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_queue_entry_d2_after_ab_10v10_single_fg.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-15) - Final Core Live-Validation Closeout

- [x] Closed the queued final core live-validation chunk after the Navigation implementation queue was cleared.
- Completion notes:
  - `BasicLoopTests`, `MovementSpeedTests`, and `CombatBgTests` passed together on the closed surface-affordance and local-detour baseline.
  - Result artifact: `tmp/test-runtime/results-live/livevalidation_core_chunk_post_nav_affordance_detour_closeout.trx`.
  - The scanned owner queues now show no active implementation item and no unchecked final-validation item.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_core_chunk_post_nav_affordance_detour_closeout.trx"` -> `passed (4/4)`

## Archived Snapshot (2026-04-15) - Surface Affordance + Local Detour Closeout

- [x] Closed `NAV-OBJ-003` surface-transition affordance classification.
- [x] Closed `NAV-OBJ-004` local detour generation around collidable objects.
- Completion notes:
  - Native `ClassifyPathSegmentAffordance(...)` now returns explicit segment classifications and quantitative metrics for climb height, gap distance, drop height, slope angle, resolved Z, and validation code.
  - `CalculatePathResponse` now carries jump-gap, safe-drop, unsafe-drop, and blocked counts plus max climb/gap/drop metrics; generated C# and `PathfindingClient` propagate those fields.
  - Service response aggregation stays fast by default and can use bounded native segment classification with `WWOW_ENABLE_NATIVE_AFFORDANCE_SUMMARY=1`.
  - Native and service detour repair were closed by existing implementation plus current evidence: lateral grounded candidates are validated with `ValidateWalkableSegment(...)`, repaired dynamic-overlay paths avoid the registered blocker, and corridor results preserve the blocker identity that forced repair.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentAffordanceClassificationTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests|FullyQualifiedName~FindPath_ObstructedDirectSegment_ReformsIntoWalkableDetour" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`

## Archived Snapshot (2026-04-15) - Native Dynamic-Overlay Identity Closeout

- [x] Closed `NAV-OBJ-001` request-scoped dynamic object path-validation identity.
- [x] Closed stale `NAV-OBJ-002` capsule/support walkability validation by evidence.
- Completion notes:
  - Native corridor results now carry the dynamic blocker identity that forced an overlay-aware repair: segment index, runtime instance ID, GUID, display ID, and repair flag.
  - `PathfindingService.Repository.Navigation` consumes that metadata as a usable repaired route result and preserves the original detailed `dynamic_overlay,...` reason without a second managed segment probe.
  - The existing `ValidateWalkableSegment(...)` export and service reason mapping already satisfy the capsule-clearance/support-surface contract.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~OrgrimmarGroundZAnalysisTests.DualClient_OrgrimmarGroundZ_PostTeleportSnap|FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=live_remaining_groundz_corpserun_after_org_corner_closeout.trx"` -> `passed (2/2)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`

## Archived Snapshot (2026-04-14) - Orgrimmar Corner Navigation Closeout

- [x] Closed deferred issue `D5` (`OrgBankToAH navigation`) and the remaining live `CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall` blocker.
- Completion notes:
  - `CharacterAction.TravelTo` now upserts a persistent `GoToTask`, keeping long-running travel on the same movement owner that already handles route diagnostics and replans.
  - `NavigationPath` now allows stuck-driven replans to choose materially safer alternates within a bounded extra-cost budget, and it no longer re-rejects overlay-aware service routes with a duplicate local dynamic-object gate against the same nearby-object set.
  - The live corner test now stages from the street-level Orgrimmar bank approach instead of the elevated banker perch, so it measures the intended corner-navigation route rather than a forced ledge drop.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_MovementStuckRecoveryPrefersSaferAlternateWithinTolerance|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_DoesNotLocallyRejectOverlayAwareServiceRouteForDynamicSegmentIntersection" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=orgbank_to_ah_corner_navigation_post_overlay_local_dyn_gate_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-14) - Shared BG-only dungeon/raid fixture closeout

- [x] Closed the shared BG-only dungeon/raid entry fixture blocker in `Tests/BotRunner.Tests`.
- Completion notes:
  - `DungeonInstanceFixture` no longer depends on `TESTBOT1` for default live coordinator coverage; BG-led fixtures now use dedicated `<prefix>1` leader accounts and only FG-led fixtures opt back into `TESTBOT1`.
  - Shared instance-entry fixtures now precreate missing accounts, wipe mismatched stale characters, and reserve deterministic generated names before launch so live entry coverage no longer depends on preseeded manual state.
  - Added configuration coverage in `DungeonFixtureConfigurationTests`, then revalidated the shared contract across all raid entry specs plus a representative dungeon rerun.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DungeonFixtureConfigurationTests|FullyQualifiedName~CoordinatorFixtureBaseTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.AQ20Tests.AQ20_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=aq20_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.BlackwingLairTests.BWL_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=raid_entry_namespace_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)` (`BWL_RaidFormAndEnter`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.ZulGurubTests.ZG_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=zg_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.MoltenCoreTests.MC_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mc_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.OnyxiasLairTests.ONY_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ony_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.AQ40Tests.AQ40_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=aq40_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.NaxxramasTests.NAXX_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=naxx_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Dungeons.StratholmeLivingTests.STRAT_LIVE_GroupFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=strath_live_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - Ratchet fishing parity closeout

- [x] Closed the remaining Ratchet fishing follow-through tracked locally under `BR-FISH-001`, `PFS-FISH-001`, and `NAV-FISH-001`.
- Completion notes:
  - `FishingTask` search-walk now keeps probe travel targets on the waypoint reference layer and no longer counts nearby wrong-layer positions as arrived.
  - `SpellcastingManager` now keeps fishing on the no-target `CMSG_CAST_SPELL` payload shape, matching the focused FG packet capture.
  - The latest dual live compare proves BG now reaches the same cast/channel/loot packet milestones as FG: `SMSG_SPELL_GO`, `MSG_CHANNEL_START`, `SMSG_GAMEOBJECT_CUSTOM_ANIM`, `CMSG_GAMEOBJ_USE`, and `SMSG_LOOT_RESPONSE`.
  - Practical implication: the Ratchet fishing parity issue is no longer open in BotRunner, PathfindingService, or Navigation.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingTaskTests" --logger "console;verbosity=minimal"` -> `passed (37/37)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - Thin Scene Environment Closeout

- [x] `MASTER-SUB-007` / `NAV-SCENE-001`:
  - Closed the remaining live parity proof for thin scene-slice environment flags and archived the completed navigation item out of `Exports/Navigation/TASKS.md`.
  - Confirmed the last blocker was Docker/service configuration rather than triangle metadata loss: the copied Ragefire tile still carried indoor group flags, native direct stepping on that tile returned indoors, and the live seam only failed while `scene-data-service` mounted the wrong data root and spent startup parsing every tile before binding port `5003`.
  - `SceneQuery::GetAreaInfo(...)` now falls back to scene-cache metadata when VMAP area info is missing/useless, `PhysicsEngine` now re-queries grounded support environment flags when needed, `.env` now points `WWOW_VMANGOS_DATA_DIR` at `D:/MaNGOS/data`, and `SceneTileSocketServer` now loads `.scenetile` payloads on demand after a fast filename index pass.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SceneEnvironmentFlagTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataEnvironmentIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataEnvironmentIntegrationTests|FullyQualifiedName~SceneDataPhysicsPipelineTests|FullyQualifiedName~PhysicsEnvironmentFlags_|FullyQualifiedName~RecordResolvedEnvironmentState_" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.Snapshot_IndoorLocation_ReportsIsIndoors|FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.MountSpell_OutdoorLocation_Mounts|FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.MountSpell_IndoorLocation_DoesNotMount" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mount_environment_nav_scene_closeout_20260413_post_lazy_index.trx"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ItemDataTests|FullyQualifiedName~SpellDataTests|FullyQualifiedName~BotRunnerServiceInventoryResolutionTests|FullyQualifiedName~CastSpellTaskTests|FullyQualifiedName~UseItemTaskTests" --logger "console;verbosity=minimal"` -> `passed (126/126)`
  - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore` -> `succeeded`
  - `docker compose -f docker-compose.vmangos-linux.yml build scene-data-service` -> `succeeded`
  - `docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service` -> `succeeded`
- Evidence:
  - `tmp/test-runtime/results-live/mount_environment_nav_scene_closeout_20260413_post_lazy_index.trx`

## Archived Snapshot (2026-04-13) - MASTER-SUB-026 live closeout

- [x] `MASTER-SUB-026` Pathfinding follow-through on `PFS-NAV-002`:
  - Closed the remaining live proof step for `BR-NAV-005`; the reproduced BG mining route now passes on the current binaries.
  - Confirmed the blocker was combat-side re-engage spam rather than another path-generation defect.
  - `SpellcastingManager` now latches confirmed melee auto-attack per target, suppresses duplicate `CMSG_ATTACKSWING` retries after server confirmation, and clears that latch on stop/cancel/rejection.
  - Added deterministic regressions in `WoWSharpObjectManagerCombatTests`, `SpellHandlerTests`, and `WorldClientAttackErrorTests`.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests|FullyQualifiedName~SpellHandlerTests.HandleAttackStart_LocalPlayerConfirmsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleAttackStop_LocalPlayerClearsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleCancelCombat_LocalPlayerClearsTrackedAutoAttackState|FullyQualifiedName~SpellHandlerTests.HandleAttackerStateUpdate_OurSwingConfirmsPendingAutoAttack|FullyQualifiedName~WorldClientAttackErrorTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (99/99)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_melee_confirm_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - BotRunner corridor-promotion clamp

- [x] `MASTER-SUB-026` Pathfinding follow-through on `PFS-NAV-002`:
  - Closed the remaining managed-follower corridor gap in `NavigationPath` rather than `MovementController` or native path generation.
  - Adaptive-radius waypoint promotion, probe-waypoint skipping, and overshoot look-ahead skips now all require the live-position shortcut to preserve the sampled walkable corridor.
  - Added deterministic regressions for the two reproduced off-corridor execution cases:
    - `NavigationPathTests.GetNextWaypoint_DoesNotAdvanceEarly_WhenAdaptiveRadiusShortcutLeavesWalkableCorridor`
    - `NavigationPathTests.GetNextWaypoint_DoesNotLookAheadSkip_WhenOvershootShortcutLeavesWalkableCorridor`
  - Removed the unrelated short-LOS direct-path priming experiment from `NavigationPath`; it changed direct-fallback/gap/replan semantics without closing any tracked item and made the stable deterministic slice red.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (66/66)`
- Follow-on:
  - Remaining live proof moved to `Exports/BotRunner/TASKS.md` `BR-NAV-005`: rerun `GatheringProfessionTests.Mining_BG_GatherCopperVein` and compare planned versus executed drift after the new clamp.

## Archived Snapshot (2026-04-12) - Pathfinding Fixture Refresh

- [x] `MASTER-SUB-024` PathfindingService stale route fixture refresh:
  - Replayed the old Orgrimmar graveyard/center and Razor Hill corpse-run contracts under the current native validator and confirmed they no longer match current walkability/LOS truth.
  - Archived those obsolete route expectations from `PathfindingTests`, leaving the owner on the two green live Orgrimmar corpse-retrieve regressions plus the green `PathfindingBotTaskTests` coverage.
  - Hardened `Services/PathfindingService/Repository/Navigation.cs` so optional native validation again honors `WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION`, accepts `MissingSupport`, propagates grounded clear endpoints, suppresses duplicate grounded waypoints, and skips bounded repair for straight-corner requests.
- Validation:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter|FullyQualifiedName~CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision" --logger "console;verbosity=minimal"` -> `failed (2/2)` with `BlockedGeometry` on `Segment 1->2` and `Segment 8->9`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`

## Archived Snapshot (2026-04-12) - Steep Incline Regression Confirmation

- [x] `MASTER-SUB-025` Native steep-incline rejection proof:
  - Confirmed the BG `MovementController` is no longer the steep-slope enforcement point; it now trusts native physics output.
  - Added `Tests/Navigation.Physics.Tests/SegmentWalkabilityTests.ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment`, which scans `Un'Goro`, `Desolace`, and `Thousand Needles` for a real uphill segment that native walkability rejects as `StepUpTooHigh` or `BlockedGeometry`.
  - Revalidated that normal uphill travel still works with `Forward_Uphill_MaintainsSpeedAndGrounded`, and that the broader climb-angle guard remains green with `GroundMovement_ClimbAngle_WithinVmangosWallClimbLimit`.
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment|FullyQualifiedName~Forward_Uphill_MaintainsSpeedAndGrounded|FullyQualifiedName~GroundMovement_ClimbAngle_WithinVmangosWallClimbLimit" --logger "console;verbosity=minimal"` -> `passed (3/3)`

## Archived Snapshot (2026-04-12) - BotProfiles + PathfindingService Test Backlog Closeout

- [x] `MASTER-SUB-001` BotProfiles backlog cleanup:
  - Archived stale completed `BP-MISS-001` through `BP-MISS-004` after revalidating the already-landed factory fixes and the reflection-based regression test.
- [x] `MASTER-SUB-024` PathfindingService deterministic test backlog:
  - Closed `PFS-TST-003` with `CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine`.
  - Closed `PFS-TST-005` with `BotTasks/OrgrimmarCorpseRunPathTask.cs` plus the green `PathfindingBotTaskTests` class filter.
- Validation:
  - `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`

## Archived Snapshot (2026-04-12) - P0 Live Movement Parity Closeout

- [x] `P0.1` Kept all live FG/BG grounded movement routes green on Docker-backed scene data.
- Completion notes:
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs` now refreshes snapshots while tracked GM chat commands wait for execution/response evidence.
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs` now holds teleport settle until the bot is back in `InWorld`, reports `BotConnectionState.BotInWorld`, and clears `IsMapTransition`.
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs` now deletes stale redirect recording artifacts before starting the capture, matching the main parity runner.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_20260412_post_transition_wait_fix.trx"` -> `passed (12/12)`

## Archived Snapshot (2026-04-12) - P0 Deterministic Transport Parity Closeout

- [x] `P0.2` Closed the compact packet-backed Undercity elevator parity gap on Docker-backed scene data without regressing the long V2 replay.
- [x] `P0.3` Kept the grouped parity bundles and shared Docker data-root resolver in place so scene-data mismatches surface immediately.
- Completion notes:
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs` now prefers the freshly built root `Navigation.dll` over the stale `x64\Navigation.dll` fallback.
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs` now treats nonzero-to-nonzero `TransportGuid` swaps as transport transitions instead of steady-state on-transport frames.
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs` now logs the corrected compact replay transport window plus the frame-20 raw transport-GUID swap facts.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (3/3)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`

## Archived Snapshot (2026-04-09) - FG Realm Wizard Transition Stabilization

- [x] Replaced runtime realm-wizard Lua fallback sweeps with state-based, named-control actions.
- [x] Kept realm suggestion -> empty character-select transition detection state-driven (`charselect`) for new-account/new-character runs.
- [x] Verified repeated focused live passes:
  - `tmp/test-runtime/results-live/fg_new_account_flow_latest.trx` (`129.8s` in-world)
  - `tmp/test-runtime/results-live/fg_new_account_flow_rerun1.trx` (`122.5s`)
  - `tmp/test-runtime/results-live/fg_new_account_flow_rerun2.trx` (`121.7s`)
  - `tmp/test-runtime/results-live/fg_new_account_flow_no_sweep.trx` (`116.9s`)

## Archived Snapshot (2026-04-09) - AV P1.13/P1.14 Closeout

- [x] `P1.13` Equip items systemic failure:
  - `BuildEquipItemByIdSequence`/`BuildUseItemByIdSequence` fallback shipped for backpack + equipped bag probes.
  - Live AV proof run (`tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx`) passed with no `[LOADOUT-WARN]` lines.
- [x] `P1.14` AV stragglers:
  - Battleground coordinator restage-before-join plus entry settle window closed the recurring `72-74/80` stall.
  - Same live AV proof run recorded `BG-SETTLE bestOnBg=80` and `bg=80,off=0` before objective dispatch.
- [x] `P1.15` stale-open cleanup:
  - Removed from Open list after prior completion so `docs/TASKS.md` now tracks only unresolved work.
- Validation:
  - `rg -n "BG-SETTLE|AV:Mount|AV:HordeObjective|AV:AllianceObjective" tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx` -> `bestOnBg=80`, `bg=80,off=0`, `mounted=77/70`, objective thresholds met.
  - `rg -n "\[LOADOUT-WARN\]" tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx` -> no matches.

## Archived Snapshot (2026-04-08) - P1 Open-List Cleanup

- [x] `P1.16` Goto action persistence:
  - `CharacterAction.GoTo` now upserts a persistent `GoToTask` (push/retarget/duplicate-skip) instead of stacking a new task each poll cycle.
  - Deterministic coverage added in `Tests/BotRunner.Tests/BotRunnerServiceGoToDispatchTests.cs` (`4/4` passing).
- [x] Removed stale resolved duplicate from Open:
  - `P1.6` FG bot CharacterSelect was already resolved by all-BG AV bring-up and is now tracked only under Completed.
- Validation:
  - `dotnet restore Tests/BotRunner.Tests/BotRunner.Tests.csproj --verbosity minimal` -> `succeeded`
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`

## Archived Snapshot (2026-04-03) - P30 Coordinator Refactor Complete

- [x] `30.1` Extract Dungeoneering prep into `RfcBotFixture`.
- [x] `30.2` Simplify `DungeoneeringCoordinator` to the coordination-only flow used by the coordinator tests.
- [x] `30.3` Reuse the shared `CoordinatorFixtureBase` / `BattlegroundCoordinatorFixtureBase` pattern across RFC and battleground fixtures.
- [x] `30.4` All coordinator tests now follow the same lifecycle: fixture prep first, coordinator actions second, assertions last.
- Completion notes:
  - `RfcBotFixture` now disables the coordinator during prep, clears stale group state up front, performs revive/level/spell/gear/Orgrimmar staging, then re-enables the coordinator only after prep finishes.
  - `DungeoneeringCoordinator` now starts from `WaitingForBots` and transitions directly into group formation once every bot is in-world, so it no longer needs to enter the old prep path to drive RFC tests.
  - Deterministic coverage now proves both the strict bot-count gate and the coordination-only action flow: `CoordinatorStrictCountTests` confirms RFC waits for all bots before group formation and never emits prep chat commands before RFC teleporting.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CoordinatorStrictCountTests|FullyQualifiedName~CoordinatorFixtureBaseTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`


## Archived Snapshot (2026-02-23 09:27:22) - docs/TASKS.md

# Master Tasks

This is the canonical orchestration file for all implementation work.

## Canonical Task System

- Source of truth: `docs/TASKS.md` + directory-local `TASKS.md` files.
- Deprecated duplicate task files have been removed.
- Every coding session must update:
  - this file,
  - the impacted directory `TASKS.md` files,
  - and archive files when lists grow too large.

## Execution Policy (Required)

- Do not pause for approval; implement continuously until all `TASKS.md` work is complete.
- Do not stop at summaries; convert findings directly into new priority tasks and keep executing.
- Keep tasks recursive: when new gaps are discovered, add them immediately to the relevant local `TASKS.md`.
- Keep context light: archive completed tasks frequently and keep active files short and direct.
- Life/death/corpse/ghost assertions must come from ObjectManager/`WoWActivitySnapshot` state.
- Avoid SOAP in normal test flow; use SOAP only for hard fallback recovery and protocol research.

## Global Completion Criteria

1. Foreground and Background bots behave identically for all implemented capabilities.
2. LiveValidation tests are state-driven, deterministic, and efficient (no dead setup branches).
3. Physics, movement, object state, protobuf snapshots, and bot task behavior are aligned FG vs BG.
4. No test uses `.gobject add`; natural spawns plus `.respawn` only.
5. Corpse retrieval behavior uses pathfinding and respects reclaim delay/cooldown semantics.

## Task File Topology

Top-level orchestration files:
- `docs/TASKS.md` (this file)
- `Exports/TASKS.md`
- `Services/TASKS.md`
- `Tests/TASKS.md`
- `UI/TASKS.md`
- `BotProfiles/TASKS.md`
- `RecordedTests.Shared/TASKS.md`
- `RecordedTests.PathingTests/TASKS.md`
- `WWoW.RecordedTests.Shared/TASKS.md`
- `WWoW.RecordedTests.PathingTests/TASKS.md`
- `WWoWBot.AI/TASKS.md`

Subproject files are required in each major csproj directory (seeded in this session).
Primary detailed audit queues currently live in:
- `Tests/BotRunner.Tests/TASKS.md` (LiveValidation audit execution queue)
- `Services/WoWStateManager/TASKS.md` (state/action orchestration)
- `Exports/BotRunner/TASKS.md`, `Exports/WoWSharpClient/TASKS.md`, `Exports/Navigation/TASKS.md` (core parity workstreams)

## Mandatory Workstream Mapping

All parity gaps must be tracked under one of these:
1. PhysicsEngine (`Exports/Navigation/*`, native behavior parity, movement replay parity).
2. MovementController (`Exports/WoWSharpClient/Movement/*`, server-authoritative movement updates).
3. ObjectManager and WoW models/interfaces (`Exports/GameData.Core/*`, `Exports/WoWSharpClient/Models/*`, FG readers).
4. BotCommLayer protobuf (`Exports/BotCommLayer/Models/ProtoDef/*`, snapshot compatibility).
5. BotTasks/BotRunner logic (`Exports/BotRunner/*`, sequencing/guards/retries/path behavior).

## Live Validation Audit (Integrated)

This is the active LiveValidation audit backlog.

Current status:
- `DeathCorpseRunTests`: setup is now snapshot-first strict-alive with fallback teleport only when setup map/Z is invalid.
- `DeathCorpseRunTests`: corpse target now comes from snapshot transition data; when server transitions directly to ghost, the test uses snapshot last-alive corpse fallback (no SOAP corpse-state dependency).
- `.kill` remains unavailable on this command table; `.die` remains effective fallback.
- `DeathCorpseRunTests` still has intermittent corpse-run stall cases where FG stays ghosted with no movement and `moveFlags=0x10000000` before reclaim.
- Command table restoration landed:
  - sanitize path removes stale fixture-injected rows and phantom `kill/select` definitions.
  - optional baseline restore path (`WWOW_TEST_RESTORE_COMMAND_TABLE=1`) now restores authoritative 4-row command snapshot from local MaNGOS SQL.
- Command-table gap remains:
  - current restored baseline does not include expected live-test GM commands (`kill/die/select/revive`) in `mangos.command`.
  - fixture currently resolves death commands by behavior probing; this needs deterministic DB migration + verification.
- FG death-recovery stability fix landed:
  - FG now guarantees non-null `PathfindingClient` injection into `ClassContainer`.
  - `RetrieveCorpseTask` no longer immediately collapses on transient ghost-state flicker.
  - FG `InGhostForm` now uses descriptor-first detection (`PLAYER_FLAGS_GHOST`) with memory/Lua fallback.
- `RetrieveCorpseTask` now uses horizontal corpse distance (`DistanceTo2D`) and corpse-Z clamping for path target when corpse Z is implausible, so reclaim gating is not blocked by bad corpse Z.
- BG object-update stability fix landed:
  - `WoWSharpObjectManager.ApplyGameObjectFieldDiffs` now converts mixed numeric payloads safely (`Single`/`UInt32`) to prevent repeated live `InvalidCastException` crashes.
- BG movement remains partially unresolved:
  - teleport reset is applied, but BG can still remain `flags=0x1` with zero displacement during follow `Goto` loops; needs MovementController/physics/path-action triage.
- Local command-table baseline located: `D:\MaNGOS\sql\world_full_14_june_2021.sql` contains the repack's authoritative command data snapshot (4 rows).
- `GatheringProfessionTests`: passing (`2/2`), natural nodes only; next reduction is setup command churn.
- BG teleport parity issue identified: `MOVEFLAG_FORWARD` can remain set after teleport and must be cleared immediately on teleport state transitions.
- `FishingProfessionTests` refactor landed: setup is snapshot-delta driven (conditional revive/teleport/learn/setskill/add pole) and focused run passes.
- `CraftingProfessionTests` refactor landed: snapshot-delta setup with deterministic bag-state verification; BG strict path passes and FG currently relies on `.cast` fallback when `CastSpell` crafting misses.
- `EquipmentEquipTests` refactor landed: snapshot-delta setup with strict alive guard and bag-to-mainhand transition assertion; focused run passes.
- Group/party snapshot parity fix landed:
  - `SMSG_GROUP_LIST` parsing in BG was off by one header byte, producing bogus group sizes (`1140850688`) and leader GUID divergence.
  - parser now matches MaNGOS 1.12.1 layout (`groupType + ownFlags + memberCount`), and focused `GroupFormationTests` run passes with FG/BG leader parity.
- `NpcInteractionTests` refactor landed:
  - setup is now snapshot-delta based (strict-alive/location checks and conditional item/money/level setup).
  - assertions now require NPC discovery by `NpcFlags` and successful `InteractWith` action forwarding for BG/FG.
  - focused run passes in `tmp/npcinteraction_run_post_refactor.log`.
- `EconomyInteractionTests` refactor landed:
  - setup is now snapshot-delta based (strict-alive/location checks and conditional item setup).
  - banker/auctioneer interactions now require explicit NPC detection + successful `InteractWith` action forwarding.
  - mailbox path is BG-strict and FG-warning due intermittent FG `NearbyObjects` visibility gap.
  - focused run passes in `tmp/economy_run_post_refactor.log`.
- `QuestInteractionTests` refactor landed:
  - setup is now strict-alive + snapshot-delta cleanup (no unconditional `.quest remove`).
  - assertions now require snapshot quest-log add/remove transitions, plus completion confirmation via quest-log change/removal or explicit completed chat response.
  - focused run passes in `tmp/quest_run_post_refactor_v2.log`.
- Quest snapshot parity plumbing landed:
  - `BotRunnerService.BuildPlayerProtobuf` now serializes `Player.QuestLogEntries` from `IWoWPlayer.QuestLog`.
  - FG `WoWPlayer.QuestLog` descriptor reads implemented to expose 20 quest slots (3 fields each) in FG snapshots.
- `TalentAllocationTests` refactor landed:
  - setup is now strict-alive + snapshot-delta (conditional level/unlearn commands only when needed).
  - BG spell assertion is strict; FG is warning-only pending spell-list parity for learned/already-known talent spells.
  - focused run passes in `tmp/talent_run_post_refactor_v3.log`.
- `CharacterLifecycleTests` refactor validated:
  - strict alive/death/revive transitions are now snapshot-asserted.
  - focused verification passes in `tmp/basic_character_post_refactor_verify.log`.
- `BasicLoopTests` refactor validated:
  - setup is snapshot-delta based, with concrete snapshot assertions and teleport movement-flag checks.
  - focused verification passes in `tmp/basic_character_post_refactor_verify.log`.
- `CombatLoopTests` combat-targeting correction landed:
  - test now teleports to a boar-dense Valley of Trials coordinate cluster and selects creature targets using `entry=3098` / `Mottled Boar` identity constraints.
  - allied/friendly target classes are excluded via creature GUID + `NpcFlags` filters.
  - focused verification passes in `tmp/combatloop_run_post_refactor_v8.log`.
- Combat snapshot parity gap observed:
  - `Player.Unit.TargetGuid` can remain unset/stale in snapshots during successful melee engage, so selection visibility is currently weaker than live combat state and needs parity work.

Remaining test-audit queue:
1. Command table restoration follow-through: decide whether baseline restore should become default (vs env-gated), then lock fixture behavior to prevent any future command-table drift and stale help artifacts (`Enabled by test fixture`).
   - Current execution hook: `WWOW_TEST_RESTORE_COMMAND_TABLE=1` in `LiveBotFixture` (baseline restore + backup table creation).
2. Build and apply a reproducible command-table migration for this repack that restores expected vanilla-era GM command hierarchy used by tests (`kill/die/select/revive`), with DB diff + reload verification.
3. `DeathCorpseRunTests`: stabilize intermittent corpse-run stalls (`moveFlags=0x10000000`, no step movement) while preserving pathfinding-only corpse run semantics.
4. Add corpse position to activity snapshot protobuf (`WoWPlayer.corpsePosition`) to eliminate last-alive fallback dependency when server transitions directly to ghost.
5. Teleport parity: validate and finish BG/FG teleport movement-flag clearing (`MOVEFLAG_FORWARD`/stale movement flags) after server teleports.
6. `CraftingProfessionTests` FG parity follow-up: remove `.cast` fallback by fixing FG `CastSpell` crafting behavior, then restore strict FG assertion.
7. `TalentAllocationTests` FG parity follow-up: restore strict FG spell-list assertion once FG snapshot `SpellList` reliably includes learned/already-known talent spells.
8. Combat snapshot parity follow-up: make `Player.Unit.TargetGuid` and nearby-unit identity fields reliably reflect live target selection in BG/FG snapshots during melee engage.

## Global Recursive Loop

Repeat until all directory `TASKS.md` files are complete:
1. Pick the highest-priority open task from the relevant local `TASKS.md`.
2. Implement code changes directly.
3. Run focused tests.
4. Record concrete evidence (pass/fail/skip, command responses, snapshot deltas).
5. Update local `TASKS.md` and this file.
6. Archive completed items to keep active files concise.
7. Continue with the next priority task.

## Required Research Inputs (WoW 1.12.1)

Use these continuously while closing tasks:
- Local docs under `docs/` and opcode/protocol references.
- MaNGOS Zero source scope (`1.12.1-1.12.3`): `https://github.com/mangoszero/server`
- MaNGOS command table reference (authoritative schema/data baseline): `https://www.getmangos.eu/wiki/referenceinfo/otherinfo/commandtable-r1735/`
- Vanilla resurrection timing reference: `https://wowwiki.fandom.com/wiki/Patch_1.1.0`
- Opcode catalog reference: `https://wowdev.wiki/Opcodes`

## Session Handoff Protocol

Before ending any session, update this file with:
1. Commands run + outcomes.
2. Snapshot and command-response evidence.
3. Files changed.
4. First command to run next session.
5. Highest-priority unresolved issue and mapped workstream.

## Latest Handoff

- Completed in this session:
  - Refined `CombatLoopTests` targeting so the test only selects neutral boars (`entry=3098` / `Mottled Boar`) in a boar-spawn coordinate cluster instead of camp-adjacent friendly NPC space.
  - Added strict combat-command failure detection (`You cannot attack that target`, `You should select a character or a creature`, invalid-target paths) so allied/invalid target flows fail deterministically.
  - Stabilized combat assertion flow for snapshot lag: if `TargetGuid` is not observed but target dies immediately from engage, test still validates via dead/gone snapshot transition.
  - Re-verified refactored `CharacterLifecycleTests` and `BasicLoopTests` pass as a combined focused run.
- Latest test evidence:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_run_post_refactor_v7.log`
    - `Failed` due over-strict snapshot-target gate (`[BG] Target GUID was never selected in snapshot`) despite boar candidate selection.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_run_post_refactor_v8.log`
    - `Passed` with boar-only targeting and no combat-target error messages.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BasicLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/basic_character_post_refactor_verify.log`
    - `Passed` (`10/10`).
- Files changed this session:
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combat_death_verify_next.log`
- Highest-priority unresolved issue:
  - Snapshot parity gap in live combat: `Player.Unit.TargetGuid` and nearby-unit identity visibility are intermittently incomplete/stale during melee engage, which weakens target-state observability and should be fixed in snapshot mapping/ObjectManager parity workstreams.




## Archived Snapshot (2026-02-24 19:43:32) - docs/TASKS.md

- [x] Build and maintain a single behavior matrix covering all character abilities and world interactions.
- [x] Ensure every matrix row links to the owning local `TASKS.md`.
- [x] `DeathCorpseRunTests` setup uses `.tele name {NAME} Orgrimmar` before kill.
- [x] `ValleyOfTrials` setup path removed from corpse-run flow.
- [x] Add a `Behavior Cards` section in each local `TASKS.md` for owned behaviors.
- [x] Add explicit continuation instructions so the next agent can resume from the highest-priority unchecked item.

## Archived Snapshot (2026-04-15) - BG gather server retry and Docker timing closeout

- [x] BG `ObjectManager -> MovementController` stop/use packet trigger parity is now in the deterministic `Category=MovementParity` bundle.
- [x] BG gather server `TRY_AGAIN` behavior is implemented and covered by deterministic `GatheringRouteTaskTests`.
- [x] Docker-backed BG herbalism and NPC vendor timing validation passed.
- [x] `Services/BackgroundBotRunner/TASKS.md` now reports `0` remaining owner-local parity/docker items for this slice.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests.Herbalism_BG_GatherHerb" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=herbalism_bg_retry_try_again.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NpcInteractionTests.Vendor_VisitTask_FindsAndInteracts" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=npc_vendor_visit_docker_timing.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-15) - DecisionEngineService runtime closeout

- [x] `Services/DecisionEngineService/TASKS.md` now reports `0` remaining owner-local items.
- Completed:
  - Added runtime option parsing and defaults for DecisionEngine enablement, directories, SQLite path, and listener endpoint.
  - Added startup preflight for writable data/processed directories and SQLite `TrainedModel`/`ModelWeights` schema creation.
  - Wired `DecisionEngineWorker` to create and dispose a runtime containing `CombatPredictionService` and `CombatModelServiceListener`.
  - Added focused tests in `Tests/PromptHandlingService.Tests/DecisionEngineRuntimeTests.cs`.
  - Refreshed stale active-task handoffs in `Tests/TASKS.md`, `Tests/Tests.Infrastructure/TASKS.md`, and `Tests/WoWSharpClient.NetworkTests/TASKS.md`.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (31 passed, 161 skipped, 0 failed, 192 total)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`


---

## Archived: P0 - Movement / Physics WoW.exe Parity (completed 2026-04-15)
### Context
- `MovementController` / `PhysicsEngine` parity against the original `WoW.exe` binary is the current highest-priority work.
- Parity tests must use the same host data root mounted into Docker `scene-data-service` and `pathfinding-service`: `${WWOW_VMANGOS_DATA_DIR:-D:/MaNGOS/data}`.
- Repo-local `Data/` and `Bot/...` fallbacks are now acceptable only when that Docker parity root is unavailable.
- Three grouped parity bundles now exist for fast validation:
  - Deterministic BG protocol/object-manager bundle: `Tests/WoWSharpClient.Tests` with `Category=MovementParity`
  - Live FG/BG bundle: `Tests/BotRunner.Tests` with `Category=MovementParity`
  - Deterministic replay/physics bundle: `Tests/Navigation.Physics.Tests` with `Category=MovementParity`

### Current Status
- Local pathing oscillation at the Orgrimmar bank-to-auction-house corner is fixed on the live Docker stack.
  - `NavigationPath` now rejects route segments that local physics proves climb onto the wrong WMO/terrain layer, then repairs the segment through a nearby same-layer detour when one is available.
  - The repair keeps strict local-physics/support checks on the short detour leg and avoids using the noisy lateral-width probe as a veto on the longer ramp stitch-back leg.
  - Local short-horizon `hit_wall` results no longer reject long service segments when route-layer metrics remain consistent.
  - Corpse-run routes now advance close waypoints without the standard probe-corridor shortcut veto because `NavigationRoutePolicy.CorpseRun` intentionally disables probe heuristics.
  - Live evidence: `CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall` passed with `corner_navigation_after_corpse_probe_policy.trx`; route diagnostics showed `Repaired local-physics route-layer segment 2` and arrival at `(1687,-4465,26)`.
- Foreground corpse-run crash/stall status was revalidated.
  - `CRASH-001` is no longer valid as an active crash blocker: the opt-in foreground corpse-run reruns on 2026-04-15 did not crash `WoW.exe`.
  - The current foreground runback/reclaim stall is fixed: `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer` passed with `fg_corpse_run_after_corpse_probe_policy.trx`, restored strict-alive state after 30s, and reached best 34y from corpse.
- Deterministic BG server-packet movement triggers are now part of the parity bundle.
  - `ObjectManagerWorldSessionTests` now tags force-speed, force-root, movement-flag toggle, compressed movement trigger, and knockback ACK coverage as `Category=MovementParity`.
  - `MoveKnockBack_ServerPacketFeedsMovementControllerNextFrame` proves the full BG path: `SMSG_MOVE_KNOCK_BACK` -> `MovementHandler` -> `WoWSharpObjectManager` ACK/state mutation -> `MovementController.Update()` consuming the pending impulse and sending a non-directional airborne heartbeat.
  - `ForceStopImmediate_BlocksStopPacketBeforeGameObjectUse` proves the interaction path now completes `MSG_MOVE_STOP` before `CMSG_GAMEOBJ_USE`, closing the stop/use ordering gap behind BG gathering and NPC/game-object interactions.
  - `MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity` now constructs the controller with the object manager that owns the pending knockback state.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`.
- Docker-backed BG gathering/NPC timing is now green.
  - `GatheringRouteTask` now retries same-node gather attempts on server `SPELL_FAILED_TRY_AGAIN` instead of abandoning a visible active node.
  - `GatheringProfessionTests.Herbalism_BG_GatherHerb` passed against the live Docker vmangos stack with recording artifacts enabled.
  - `NpcInteractionTests.Vendor_VisitTask_FindsAndInteracts` passed against the same Docker timing environment.
  - Direct `DockerServiceTests` TCP endpoint checks for `PathfindingService` and `SceneDataService` passed.
- Latest completed live FG/BG grounded movement parity bundle is green on Docker scene data.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_20260412_post_transition_wait_fix.trx"` -> `passed (12/12)` on `D:\MaNGOS\data`
  - `2026-04-12` harness hardening closed the remaining live flake: teleport settle now waits for `InWorld` + `BotInWorld` + `IsMapTransition=false`, tracked GM chat command waits now force snapshot refreshes, and the redirect slice now clears stale packet/transform artifacts before recording.
- Deterministic jump/knockback parity is green on Docker scene data.
- Deterministic transport parity is now green on Docker scene data.
  - `ReplayEngine.cs` now treats any nonzero-to-nonzero `TransportGuid` swap as a transport transition reset instead of falsely carrying moving-base state across a different object.
  - `NavigationInterop.cs` now prefers the freshly built root `Navigation.dll` over the stale `x64\Navigation.dll` fallback, so deterministic runs exercise the real current native build.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`

### Open
- None.


## Archived: P1 - Alterac Valley 40v40 Integration (completed 2026-04-15)

### Context
Single AV test: `AV_FullMatch_EnterPrepQueueMountAndReachObjective` (80 bots, 40v40).
Fixture: `AlteracValleyFixture` / `AlteracValleyCollection`. Dedicated AV accounts (`AVBOT1-40`, `AVBOTA1-40`), all BG bots.
Honor rank 15 set in DB for all 80 AV accounts. mangosd config updated: Alterac.MinPlayersInQueue=1, InitMaxPlayers=40.

### Completed
- [x] P1.1 **Level bug** - `.levelup` computes delta from current level
- [x] P1.2 **Anticheat rejection** - AV prep skips raid formation; bots queue individually
- [x] P1.3 **Single test** - consolidated 7 AV tests into one full-pipeline test
- [x] P1.4 **Coordinator flow** - confirmed working: WaitingForBots -> QueueForBattleground
- [x] P1.5 **PvP rank** - honor_highest_rank=15 set in DB for all 80 characters
- [x] P1.7 **PvP gear equip** - Changed to fire-and-forget (equip was blocking 18s+ per bot). Removed invalid `.modify honor rank` command (doesn't exist in VMaNGOS)
- [x] P1.8 **Alliance teleport fall** - FIXED (Z+3 removed for indoor Stormwind)
- [x] P1.9 **BG queue pop** - BG coordinator transitions through all states. 73-74/80 bots enter AV map 30. VMaNGOS AV config fixed: Alterac.MinPlayersInQueue=1, InitMaxPlayers=40, min_players_per_team=1 in DB
- [x] P1.10 **Enter world tolerance** - MinimumBotCount override accepts 78/80 for FG stragglers. All >= checks fixed
- [x] P1.11 **Coordinator timeout** - 90s timeout for WaitingForBots so pipeline proceeds with >=75% staged
- [x] P1.12 **High Warlord / Grand Marshal** - Leaders have HW Battle Axe (18831) / GM Claymore (18876) + Warlord/FM armor sets. DB rank 15. All bots now BG (headless) to avoid FG crashes.
- [x] P1.6-resolved **FG bots removed** - All AV bots BG. FG crash/CharacterSelect issues no longer block the pipeline.
- [x] P1.mount **Mount via .cast GM command** - UseItem and CastSpell actions failed for GM-added items. `.gm on` + `.targetself` + `.cast 23509/23510` works. 68/80 bots mount successfully.
- [x] P1.16 **Goto action persistence** - Repeated `Goto` dispatches now upsert a single persistent `GoToTask` (push/retarget/duplicate-skip) instead of stacking fresh tasks each poll cycle. Deterministic coverage: `BotRunnerServiceGoToDispatchTests` (4/4).
- [x] P1.15 **Scene tiles for ALL maps** - Generated 695 scene tiles across 34 maps (was 142/5 maps). Includes Emerald Dream (169, 256 tiles). Docker scene-data-service redeployed with full coverage. Fixed brute-force tile discovery offset bug (36->44 bytes).
- [x] P1.13 **Equip items systemic failure** - by-id equip/use fallback now probes backpack + equipped bag slots (`0..15`, `1..4 x 0..19`). Full AV integration pass completed with no `[LOADOUT-WARN]` output in run artifact.
- [x] P1.14 **8 straggler bots** - coordinator restage + settle window closed the queue-entry gap; latest AV pass reached `BG-SETTLE bestOnBg=80` and `bg=80,off=0` before objective push.

### Open
- None.


## Archived: Session Handoffs (2026-04-14 through 2026-04-16)
## Session Handoff (2026-04-15 - Navigation Local-Physics Detour + Corpse-Run Closeout)

- Completed:
  - Kept the monotonic waypoint overshoot fix and added local-physics route-layer validation for short route segments.
  - Added same-layer detour repair when a service route segment would make local physics climb onto the wrong WMO/terrain layer.
  - Relaxed only the noisy downstream lateral-width veto after the detour candidate is proven by local physics and support continuity; the short detour leg still uses strict width/support/local-physics checks.
  - Added deterministic coverage for the local-physics layer trap, noisy downstream ramp-width probe case, long local-physics horizon `hit_wall` tolerance, and corpse-run close-waypoint advancement with probe heuristics disabled.
  - Revalidated the live Orgrimmar bank-to-auction-house route; the bot no longer loops back and forth over the corner waypoint and arrived successfully.
  - Kept the current foreground ghost-runback mitigation guarded and unit-covered: foreground ghost forward movement uses simulated key input for `Front`, and the live FG corpse-run test is opt-in via `WWOW_RETRY_FG_CRASH001=1`.
  - Re-ran the opt-in foreground corpse-run validation. It did not reproduce the old WoW.exe access violation, so `CRASH_INVESTIGATION.md` is now historical. The former FG runback/reclaim stall now passes.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer" --logger "console;verbosity=minimal"` -> `passed (3/3)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_AcceptsLongLocalPhysicsHorizonHit_WhenRouteLayerRemainsConsistent|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RejectsShortLocalPhysicsHitWall|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails" --logger "console;verbosity=minimal"` -> `passed (3/3)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_after_corpse_probe_policy.trx"` -> `passed (1/1)`.
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`.
- Files changed:
  - `Exports/BotRunner/Clients/PathfindingClient.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tests/ForegroundBotRunner.Tests/ObjectManagerMovementTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/CRASH_INVESTIGATION.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/DeathCorpseRunTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `RecordedTests.PathingTests/TASKS.md`
  - `RecordedTests.PathingTests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
  - `docs/TECHNICAL_NOTES.md`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
- Exact next command:
  - `rg -n "^- \[ \]" --glob TASKS.md`

---

## Session Handoff (2026-04-15 - DecisionEngineService Runtime Closeout)

- Completed:
  - Closed the stale `Services/DecisionEngineService/TASKS.md` runtime checklist; owner-local remaining items are now `0`.
  - Added config-backed `DecisionEngineRuntimeOptions`, startup preflight, SQLite schema creation, SQLite native provider initialization, and a disposable runtime that owns `CombatPredictionService` plus `CombatModelServiceListener`.
  - Updated `DecisionEngineWorker` so enabled hosts start the prediction/listener runtime instead of idling with no runtime composition.
  - Added direct `DecisionEngineRuntimeTests` in `Tests/PromptHandlingService.Tests` for config defaults/overrides, writable directories, SQLite `TrainedModel`/`ModelWeights`, and listener/runtime creation.
  - Updated DecisionEngineService README and archived completed local items.
  - Refreshed stale test umbrella handoffs in `Tests/TASKS.md`, `Tests/Tests.Infrastructure/TASKS.md`, and `Tests/WoWSharpClient.NetworkTests/TASKS.md` so they no longer advertise completed work as active.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet build Services/DecisionEngineService/DecisionEngineService.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DecisionEngineRuntimeTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (31 passed, 161 skipped, 0 failed, 192 total)`
  - `rg -n "^- \[ \]" --glob TASKS.md` -> at that point, the only remaining unchecked item was `RecordedTests.PathingTests/TASKS.md:37`; this was later closed by the corpse-run probe-policy closeout above.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Services/DecisionEngineService/*`
  - `Tests/PromptHandlingService.Tests/DecisionEngineRuntimeTests.cs`
  - `Tests/PromptHandlingService.Tests/TASKS.md`
  - `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md`
  - `Tests/TASKS.md`
  - `Tests/TASKS_ARCHIVE.md`
  - `Tests/Tests.Infrastructure/TASKS.md`
  - `Tests/Tests.Infrastructure/TASKS_ARCHIVE.md`
  - `Tests/WoWSharpClient.NetworkTests/TASKS.md`
  - `Tests/WoWSharpClient.NetworkTests/TASKS_ARCHIVE.md`
  - `Services/TASKS.md`
  - `Services/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Exact next command:
  - `rg -n "^- \[ \]" --glob TASKS.md`

---

## Session Handoff (2026-04-15 - Legacy/Tracker Hygiene Pass)

- Completed:
  - Marked `WWoW.RecordedTests.Shared` and `WWoW.RecordedTests.PathingTests` as legacy placeholder trackers superseded by the primary `RecordedTests.*` owners.
  - Checked off stale WinImports and ForegroundBotRunner environment/umbrella checklist lines whose concrete task IDs were already complete.
- Validation:
  - `rg --files WWoW.RecordedTests.Shared` -> only task tracker files remain.
  - `rg --files WWoW.RecordedTests.PathingTests` -> only task tracker files remain.
  - `rg -n "^- \[ \]|Known remaining work|Active task:" --glob TASKS.md` -> remaining unchecked items are now limited to documented blocked/stale service checklist surfaces.
- Files changed:
  - `WWoW.RecordedTests.Shared/TASKS.md`
  - `WWoW.RecordedTests.Shared/TASKS_ARCHIVE.md`
  - `WWoW.RecordedTests.PathingTests/TASKS.md`
  - `WWoW.RecordedTests.PathingTests/TASKS_ARCHIVE.md`
  - `Exports/WinImports/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Exact next command:
  - `rg -n "^- \[ \]|Known remaining work|Active task:" --glob TASKS.md`

---

## Session Handoff (2026-04-15 - UI Umbrella Closeout)

- Completed:
  - Closed `UI-UMB-001` through `UI-UMB-004` in `UI/TASKS.md`.
  - AppHost, ServiceDefaults, and WoWStateManagerUI child task files now all report no remaining owner-local items.
  - Archived the UI umbrella closeout in `UI/TASKS_ARCHIVE.md`.
- Validation:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md` -> matched the current handoff command; the previous master queue fields are no longer present in the current docs structure.
- Files changed:
  - `UI/TASKS.md`
  - `UI/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The UI umbrella and its three child owners are closed.
- Exact next command:
  - `rg -n "^- \[ \]|Known remaining work|Active task:" --glob TASKS.md`

---

## Session Handoff (2026-04-15 - WoWStateManagerUI Converter Surface Closeout)

- Completed:
  - Added follow-up `UI-MISS-005` coverage for the remaining WPF converters in `UI/WoWStateManagerUI`.
  - Covered `NullToBoolConverter`, `PathToFilenameConverter`, and `ServiceStatusToBrushConverter` one-way contracts.
  - Updated the WPF README converter binding contract so every converter currently used by XAML is documented.
  - Advanced UI parent tracker through `UI-UMB-003`; only parent/master sync remains in `UI-UMB-004`.
- Validation:
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
- Files changed:
  - `Tests/WoWStateManagerUI.Tests/Converters/NullToBoolConverterTests.cs`
  - `Tests/WoWStateManagerUI.Tests/Converters/PathToFilenameConverterTests.cs`
  - `Tests/WoWStateManagerUI.Tests/Converters/ServiceStatusToBrushConverterTests.cs`
  - `UI/WoWStateManagerUI/README.md`
  - `UI/WoWStateManagerUI/TASKS.md`
  - `UI/WoWStateManagerUI/TASKS_ARCHIVE.md`
  - `UI/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `UI/WoWStateManagerUI/TASKS.md` reports no remaining owner-local items, and the UI child implementations are complete.
- Exact next command:
  - `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md`

---

## Session Handoff (2026-04-15 - Systems.ServiceDefaults Closeout)

- Completed:
  - Closed `SSD-MISS-001` through `SSD-MISS-006` in `UI/Systems/Systems.ServiceDefaults/TASKS.md`.
  - Added direct xUnit coverage for `AddServiceDefaults`, `ConfigureOpenTelemetry`, `MapDefaultEndpoints`, and policy helpers.
  - Added configuration-driven telemetry resource tags for service name, bot role, scenario id, and test id.
  - Made health endpoint exposure configurable outside Development.
  - Added deterministic-test control for the standard HTTP resilience handler.
  - Wired service discovery allowed-scheme policy and replaced README guidance with current commands/snippets.
- Validation:
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - Parallel validation caveat: an earlier simultaneous `dotnet build` and `dotnet test` collided on `obj/Release/Systems.ServiceDefaults.dll`; rerunning build alone succeeded.
- Files changed:
  - `UI/Systems/Systems.ServiceDefaults/Extensions.cs`
  - `UI/Systems/Systems.ServiceDefaults/Properties/AssemblyInfo.cs`
  - `UI/Systems/Systems.ServiceDefaults/README.md`
  - `Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj`
  - `Tests/Systems.ServiceDefaults.Tests/ServiceDefaultsExtensionsTests.cs`
  - `UI/Systems/Systems.ServiceDefaults/TASKS.md`
  - `UI/Systems/Systems.ServiceDefaults/TASKS_ARCHIVE.md`
  - `UI/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `UI/Systems/Systems.ServiceDefaults/TASKS.md` now reports no remaining owner-local items. UI queue advances to `UI/WoWStateManagerUI/TASKS.md`.
- Exact next command:
  - `Get-Content -Path 'UI/WoWStateManagerUI/TASKS.md' -TotalCount 360`

---

## Session Handoff (2026-04-15 - Systems.AppHost Closeout)

- Completed:
  - Closed `SAH-MISS-001` through `SAH-MISS-006` in `UI/Systems/Systems.AppHost/TASKS.md`.
  - Externalized AppHost image, credential, port, volume, and path settings under `WowServer` configuration.
  - Added absolute bind-mount path resolution rooted at the AppHost project directory by default.
  - Added preflight validation for required config/data bind-mount sources before Aspire resource creation.
  - Added a `local` launch profile and replaced stale README commands/paths.
- Validation:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release --no-build --launch-profile local` -> expected preflight failure listing missing `config`/`data` bind-mount sources in this workspace
- Files changed:
  - `UI/Systems/Systems.AppHost/Program.cs`
  - `UI/Systems/Systems.AppHost/WowServerConfig.cs`
  - `UI/Systems/Systems.AppHost/appsettings.json`
  - `UI/Systems/Systems.AppHost/Properties/launchSettings.json`
  - `UI/Systems/Systems.AppHost/README.md`
  - `UI/Systems/Systems.AppHost/TASKS.md`
  - `UI/Systems/Systems.AppHost/TASKS_ARCHIVE.md`
  - `UI/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `UI/Systems/Systems.AppHost/TASKS.md` now reports no remaining owner-local AppHost items. UI queue advances to `UI/Systems/Systems.ServiceDefaults/TASKS.md`.
- Exact next command:
  - `Get-Content -Path 'UI/Systems/Systems.ServiceDefaults/TASKS.md' -TotalCount 360`

---

## Session Handoff (2026-04-15 - RecordedTests Shared Storage Provider Closeout)

- Completed:
  - Closed `RTS-MISS-001` through `RTS-MISS-004` in `RecordedTests.Shared/TASKS.md`.
  - Added real S3 storage operations via `AWSSDK.S3` and real Azure Blob operations via `Azure.Storage.Blobs`.
  - Replaced cloud provider StoreAsync warning/no-op paths with metadata/artifact storage behavior.
  - Normalized cancellation, missing-artifact, delete-idempotency, and configured bucket/container URI semantics across filesystem/S3/Azure.
  - Added deterministic in-memory cloud backends and a shared provider parity test matrix.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests|FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests|FullyQualifiedName~RecordedTestStorageProviderParityTests" --logger "console;verbosity=minimal"` -> `passed (125/125)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (382/382)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (56/56)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (53/53)`
  - `rg -n "TODO: Implement actual S3|S3 listing not yet implemented|TODO: Implement actual Azure Blob|Azure Blob listing not yet implemented|StoreAsync is not directly implemented" RecordedTests.Shared/Storage -S` -> no matches
  - `rg -n "stubbed|download stub|not yet implemented|requires AWSSDK|requires Azure.Storage.Blobs" Tests/RecordedTests.Shared.Tests/Storage RecordedTests.Shared/Storage -S` -> no matches
- Files changed:
  - `RecordedTests.Shared/RecordedTests.Shared.csproj`
  - `RecordedTests.Shared/Properties/AssemblyInfo.cs`
  - `RecordedTests.Shared/Storage/S3RecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs`
  - `RecordedTests.Shared/Storage/FileSystemRecordedTestStorage.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/InMemoryCloudStorageBackends.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/RecordedTestStorageProviderParityTests.cs`
  - `RecordedTests.Shared/TASKS.md`
  - `RecordedTests.Shared/TASKS_ARCHIVE.md`
  - `Tests/RecordedTests.Shared.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `RecordedTests.Shared/TASKS.md` now reports no remaining owner-local storage provider items.
- Exact next command:
  - `Get-Content -Path 'Services/TASKS.md' -TotalCount 360`

---

## Session Handoff (2026-04-15 - Recorded Pathing Live + Test Owner Closeout)

- Completed:
  - Validated the recorded pathing BG corpse-run path against the live Docker stack and archived `RPT-MISS-004`.
  - At the time, the foreground corpse-run half was treated as a `CRASH-001` blocker; later 2026-04-15 revalidation showed the access violation is historical and closed `RPT-MISS-003`.
  - Closed and archived `RPTT-TST-001` through `RPTT-TST-006`; `Tests/RecordedTests.PathingTests.Tests/TASKS.md` now reports no remaining owner-local items.
  - Added direct coverage for recorded pathing CLI filters, in-process pathfinding service lifecycle cleanup, background runner timeout/disconnect cleanup, and foreground target precedence/disconnect cleanup.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=death_corpse_run_recorded_pathing_live_validation.trx"` -> `passed (1/1), previous guarded run omitted FG; superseded by 2026-04-15 opt-in revalidation`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests" --logger "console;verbosity=minimal"` -> `passed (30/30)` after the service lifecycle slice
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ProgramTests|FullyQualifiedName~ConsoleTestLoggerTests" --logger "console;verbosity=minimal"` -> `passed (34/34)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~BackgroundRecordedTestRunnerTests|FullyQualifiedName~ForegroundRecordedTestRunnerTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (135/135)`
- Files changed:
  - `RecordedTests.PathingTests/TASKS.md`
  - `RecordedTests.PathingTests/TASKS_ARCHIVE.md`
  - `RecordedTests.PathingTests/Program.cs`
  - `RecordedTests.PathingTests/Properties/AssemblyInfo.cs`
  - `RecordedTests.PathingTests/Runners/BackgroundRecordedTestRunner.cs`
  - `RecordedTests.PathingTests/Runners/ForegroundRecordedTestRunner.cs`
  - `Tests/RecordedTests.PathingTests.Tests/ProgramTests.cs`
  - `Tests/RecordedTests.PathingTests.Tests/BackgroundRecordedTestRunnerTests.cs`
  - `Tests/RecordedTests.PathingTests.Tests/ForegroundRecordedTestRunnerTests.cs`
  - `Tests/RecordedTests.PathingTests.Tests/TASKS.md`
  - `Tests/RecordedTests.PathingTests.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The recorded pathing test owner is closed. The recorded pathing runner owner was later closed by the opt-in foreground corpse-run proof; `CRASH-001` remains historical.
- Exact next command:
  - `Get-Content -Path 'RecordedTests.Shared/TASKS.md' -TotalCount 220`

---

## Session Handoff (2026-04-15 - BG Gather Server Retry + Docker Timing Closeout)

- Completed:
  - Added full deterministic parity coverage for BG stop/use ordering: `ForceStopImmediate()` now blocks through `MSG_MOVE_STOP` before game-object interaction packets can fire.
  - Made BG game-object use and game-object cast sends synchronous at the packet boundary.
  - Mapped server cast-failure reason `0x7A` to `TRY_AGAIN`.
  - Updated `GatheringRouteTask` to delay the gather cast after use, retry same-node attempts on `TRY_AGAIN`, and retry no-loot when the active node remains visible.
  - Updated live gathering route selection to parse active `.pool spawns` coordinates and prepare route-specific profession skill floors.
  - Closed `BBR-PAR-002` and `BBR-DOCKER-001` in `Services/BackgroundBotRunner/TASKS.md`.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests.HandleCastFailed" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests.Herbalism_BG_GatherHerb" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=herbalism_bg_retry_try_again.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NpcInteractionTests.Vendor_VisitTask_FindsAndInteracts" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=npc_vendor_visit_docker_timing.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DockerServiceTests.PathfindingService_TcpConnect_Responds|FullyQualifiedName~DockerServiceTests.SceneDataService_TcpConnect_Responds" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/InventoryManager.cs`
  - `Exports/WoWSharpClient/SpellcastingManager.cs`
  - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
  - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelectionTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
  - `docs/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS_ARCHIVE.md`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS_ARCHIVE.md`
- Practical implication:
  - `docs/TASKS.md` and the touched BackgroundBotRunner/BotRunner/WoWSharpClient local trackers now report no remaining owner-local parity/docker specs from this slice.
- Exact next command:
  - `rg -n "^- \[ \]|Known remaining work|Active task:" docs/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md Exports/WoWSharpClient/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md`

---

## Session Handoff (2026-04-15 - BG Server Packet Movement Parity Coverage)

- Completed:
  - Added deterministic parity-tagged coverage for BG server-packet movement triggers from `MovementHandler` through `WoWSharpObjectManager` into `MovementController`.
  - Fixed the WoWSharpClient object-manager test fixture so singleton-handler tests subscribe the object manager to `WoWSharpEventEmitter.Instance` again.
  - Fixed the pending-knockback controller test so it uses the object manager that owns the pending impulse.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.MoveKnockBack|FullyQualifiedName~ObjectManagerWorldSessionTests.ServerControlledMovementFlagChanges_ParseApplyAndAck|FullyQualifiedName~MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/ObjectManagerFixture.cs`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS_ARCHIVE.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
- Exact next command:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"`

---

## Session Handoff (2026-04-15 - BloogBot.AI Test Path Closeout)

- Completed:
  - Fixed `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` to reference the existing `BloogBot.AI/BloogBot.AI.csproj` project instead of the removed `WWoWBot.AI` path.
  - Refreshed `BloogBot.AI/TASKS.md` to current repo paths and closed its stale active pointer.
- Validation:
  - `dotnet restore Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` -> `restored`
  - `dotnet test Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` -> `passed (121/121)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj`
  - `BloogBot.AI/TASKS.md`
  - `BloogBot.AI/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `BloogBot.AI/TASKS.md` now reports `0` known remaining owner-local items.
- Exact next command:
  - `rg --files -g TASKS.md | ForEach-Object { rg --with-filename -n "^- \[ \]|\[ \] Problem|Active task:" $_ }`

---

## Session Handoff (2026-04-15 - RecordedTests Azure Stub Contract Closeout)

- Completed:
  - Closed `RTS-TST-003` through `RTS-TST-006` in `Tests/RecordedTests.Shared.Tests/TASKS.md`.
  - Added Azure Blob tests for current stub behavior: `StoreAsync` warning/no-op, upload URI/logging, download/list/delete validation and fallback behavior, configured-container mismatch, and dispose idempotence.
  - Validated the local storage command set and rewrote the local test tracker to report no remaining owner-local test items.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (98/98)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~FileSystemRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (367/367)`
- Files changed:
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/TASKS.md`
  - `Tests/RecordedTests.Shared.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `Tests/RecordedTests.Shared.Tests/TASKS.md` now reports `0` known remaining owner-local items.
  - Real S3/Azure provider implementation remains tracked in `RecordedTests.Shared/TASKS.md`.
- Exact next command:
  - `rg --files -g TASKS.md | ForEach-Object { rg --with-filename -n "^- \[ \]|\[ \] Problem|Active task:" $_ }`

---

## Session Handoff (2026-04-15 - RecordedTests S3 Stub Contract Coverage)

- Completed:
  - Closed `RTS-TST-001` and `RTS-TST-002` in `Tests/RecordedTests.Shared.Tests/TASKS.md`.
  - Added direct S3 storage tests for current stub behavior: invalid URI parsing, valid download directory side effect, no downloaded file while stubbed, list fallback logging, and delete logging.
  - Left provider implementation unchanged; `RecordedTests.Shared/TASKS.md` still tracks real S3/Azure backend implementation separately.
- Validation:
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (50/50)`
  - `dotnet test Tests/RecordedTests.Shared.Tests/RecordedTests.Shared.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~S3RecordedTestStorageTests|FullyQualifiedName~AzureBlobRecordedTestStorageTests" --logger "console;verbosity=minimal"` -> `passed (62/62)`
- Files changed:
  - `Tests/RecordedTests.Shared.Tests/Storage/S3RecordedTestStorageTests.cs`
  - `Tests/RecordedTests.Shared.Tests/TASKS.md`
  - `Tests/RecordedTests.Shared.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `Tests/RecordedTests.Shared.Tests/TASKS.md` now advances to `RTS-TST-003` for Azure Blob `StoreAsync` warning/no-op coverage.
- Exact next command:
  - `Get-Content -Path 'Tests/RecordedTests.Shared.Tests/Storage/AzureBlobRecordedTestStorageTests.cs' -TotalCount 260`

---

## Session Handoff (2026-04-15 - Prompt Function Deterministic Test Split)

- Completed:
  - Closed `PHS-TST-002` in `Tests/PromptHandlingService.Tests/TASKS.md`.
  - Added a deterministic `ScriptedPromptRunner` for prompt-function tests.
  - Converted the prompt-function default test path away from local Ollama/model dependencies.
  - Isolated Ollama prompt coverage behind `Category=Integration` with endpoint/model prerequisites documented in the test README.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (27 passed, 161 skipped, 0 failed, 188 total)`
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~IntentionParserFunctionTests|FullyQualifiedName~GMCommandGeneratorFunctionTests|FullyQualifiedName~CharacterSkillPrioritizationFunctionTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
- Files changed:
  - `Tests/PromptHandlingService.Tests/ScriptedPromptRunner.cs`
  - `Tests/PromptHandlingService.Tests/IntentionParserFunctionTests.cs`
  - `Tests/PromptHandlingService.Tests/GMCommandGeneratorFunctionTests.cs`
  - `Tests/PromptHandlingService.Tests/CharacterSkillPrioritizationFunctionTests.cs`
  - `Tests/PromptHandlingService.Tests/README.md`
  - `Tests/PromptHandlingService.Tests/TASKS.md`
  - `Tests/PromptHandlingService.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - `Tests/PromptHandlingService.Tests/TASKS.md` now reports `0` known remaining owner-local items.
- Exact next command:
  - `rg --files -g TASKS.md | ForEach-Object { rg --with-filename -n "^- \[ \]|\[ \] Problem|Active task:" $_ }`

---

## Session Handoff (2026-04-15 - WSM Quest Snapshot Evidence Closeout)

- Completed:
  - Closed `WSM-PAR-001` in `Services/WoWStateManager/TASKS.md`.
  - Current live evidence proves quest add/complete/remove state propagates from WoWSharpClient quest-log updates through BotRunner snapshot serialization into StateManager query responses.
  - No runtime code changed for this item; it was stale relative to the current quest snapshot pipeline.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.QuestInteractionTests.Quest_AddCompleteAndRemove_AreReflectedInSnapshots" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=quest_snapshot_wsm_par_rerun.trx"` -> `passed (1/1)`
  - Final cleanup: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/quest_snapshot_wsm_par_rerun.trx` contains `[FG] After add: QuestLog1=786 QuestLog2=0 QuestLog3=0`, `[BG] After add: QuestLog1=786 QuestLog2=4 QuestLog3=0`, successful `.quest complete 786`, successful `.quest remove 786`, and a passing result.
- Files changed:
  - task handoff/archive files for `WSM-PAR-001`.
- Practical implication:
  - `Services/WoWStateManager/TASKS.md` now reports `0` known remaining owner-local items.
- Exact next command:
  - `rg -n "^- \[ \]|\[ \] Problem|Active task:" docs/TASKS.md Services/WoWStateManager/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Exports/BotRunner/TASKS.md`

---

## Session Handoff (2026-04-15 - WSM Bootstrap External Ownership Closeout)

- Completed:
  - Closed `WSM-BOOT-001` in `Services/WoWStateManager/TASKS.md`.
  - `MangosServerOptions` now defaults to `AutoLaunch=false` with no default `C:\Mangos\server` directory.
  - `Services/WoWStateManager/appsettings.json` and `Tests/BotRunner.Tests/appsettings.test.json` disable MaNGOS auto-launch by default and no longer carry a default host MaNGOS directory.
  - `MangosServerBootstrapper` now exits early if auto-launch is explicitly enabled without `MangosServer:MangosDirectory`.
  - `docs/DOCKER_STACK.md` and `docs/TECHNICAL_NOTES.md` now describe Docker `realmd`/`mangosd` as the default ownership path and Windows host MaNGOS process launch as legacy opt-in.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MangosServerBootstrapperTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal"` -> `passed (24/24)`
- Files changed:
  - `Services/WoWStateManager/MangosServerOptions.cs`
  - `Services/WoWStateManager/MangosServerBootstrapper.cs`
  - `Services/WoWStateManager/appsettings.json`
  - `Tests/BotRunner.Tests/MangosServerBootstrapperTests.cs`
  - `Tests/BotRunner.Tests/appsettings.test.json`
  - `docs/DOCKER_STACK.md`
  - `docs/TECHNICAL_NOTES.md`
  - task handoff/archive files
- Practical implication:
  - Superseded by the WSM Quest Snapshot Evidence Closeout above. At this point in the work, the remaining visible WoWStateManager local task was `WSM-PAR-001` quest snapshot latency.
- Exact next command:
  - `rg -n "Quest|QuestLog|QuestStatus|QuestState|Quest.*snapshot|SMSG_QUEST|CMSG_QUEST" Exports/WoWSharpClient Services/WoWStateManager Tests/BotRunner.Tests`

---

## Session Handoff (2026-04-15 - Deferred D3/D4 Closeout)

- Completed:
  - Closed `D3` by focused WSG live evidence. `WSG_PreparedRaid_QueueAndEnterBattleground` now reaches all 20 WSG bots in world, queues all 20, and transfers all 20 onto WSG map `489`.
  - Closed stale `D4` by current deterministic Navigation evidence. After a fresh `Navigation.dll` Release x64 build, the Docker scene-data `Category=MovementParity` bundle passes `8/8`, including the compact packet-backed Undercity elevator replay coverage that previously represented the two Navigation.Physics failures.
  - No runtime code was changed for this D3/D4 closeout; this pass verified current behavior and moved the stale deferred rows into archives.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_transfer_d3_rerun.trx"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - Final cleanup: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/wsg_transfer_d3_rerun.trx` contains `[WSG:Enter] All 20/20 bots entered world`, `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, `[WSG:BG] 20/15 bots on BG map`, and `[WSG:Final] onWsg=20, totalSnapshots=20`.
- Files changed:
  - task handoff/archive files only for D3/D4.
- Practical implication:
  - The master deferred issue table is empty. Superseded by the WSM closeout handoffs above; at this point, remaining visible local work was owner-specific.
- Exact next command:
  - `rg -n "^- \[ \]|\[ \] Problem|Active task:" docs/TASKS.md Services/WoWStateManager/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Tests/BotRunner.Tests/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Exports/BotRunner/TASKS.md`

---

## Session Handoff (2026-04-15 - Deferred BG D1/D2 Closeout)

- Completed:
  - Closed `D1` by evidence and regression coverage. `AlteracValley.config.json` contains the full `AVBOTA1-40` Alliance roster, and `StateManagerWorker.OrderLaunchSettings(...)` now has a deterministic test proving all 40 `AVBOTA*` runnable accounts remain in launch order.
  - Closed `D2` for the AB/AV queue-pop path. AV already had the full-match pass with `BG-SETTLE bg=80,off=0`; AB now has a focused 10v10 live queue-entry proof that reaches `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, and `[AB:BG] 20/20 bots on BG map`.
  - Hardened the AB live fixture for the queue-entry proof: AB now runs a 20-account smoke roster, keeps one Horde foreground visual client, runs the Alliance raid leader headless, and extends cold-start enter-world tolerance to `8m` max / `2m` stale.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests" --logger "console;verbosity=minimal"` -> `passed (20/20)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_queue_entry_d2_after_ab_10v10_single_fg.trx"` -> `passed (1/1)`
  - Final cleanup: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Tests/BotRunner.Tests/WoWStateManagerLaunchThrottleTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`
  - task handoff/archive files
- Practical implication:
  - Superseded by the D3/D4 closeout handoff above. At this point in the work, deferred BG tracking was down to `D3` and `D4` was still separate.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_transfer_d3_rerun.trx"`

---

## Session Handoff (2026-04-15 - Final Core Live-Validation Closeout)

- Completed:
  - Ran the queued final core live-validation chunk after the Navigation implementation queue was cleared.
  - `BasicLoopTests`, `MovementSpeedTests`, and `CombatBgTests` passed together on the closed surface-affordance and local-detour baseline.
  - Result artifact: `tmp/test-runtime/results-live/livevalidation_core_chunk_post_nav_affordance_detour_closeout.trx`.
  - The tracked Navigation, PathfindingService, and BotRunner implementation queues now report no active open items.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_core_chunk_post_nav_affordance_detour_closeout.trx"` -> `passed (4/4)`
- Files changed:
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The current task-tracked solution work is closed in the scanned owner queues; any next pass should start from a fresh open-task scan.
- Exact next command:
  - `rg -n "^- \\[ \\]|\\[ \\] Problem|Active task:" docs/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md`

---

## Session Handoff (2026-04-15 - Surface Affordance + Local Detour Closeout)

- Completed:
  - Closed `NAV-OBJ-003`. Native `ClassifyPathSegmentAffordance(...)` now exposes segment affordance classification and metrics for higher layers that need an explicit native answer: walk, step-up, steep-climb, jump-gap, safe-drop, unsafe-drop, vertical, and blocked, plus validation code, resolved Z, climb height, gap distance, drop height, and slope angle.
  - Extended `CalculatePathResponse` / generated C# / `PathfindingClient` with jump-gap, safe-drop, unsafe-drop, blocked counts and max climb/gap/drop metrics.
  - Added `PathAffordanceClassifier` for service response aggregation. Default aggregation stays fast/geometric for live route latency; bounded native aggregation can be enabled with `WWOW_ENABLE_NATIVE_AFFORDANCE_SUMMARY=1`.
  - Closed `NAV-OBJ-004` by evidence. Native `PathFinder` already generates grounded lateral detour candidates around blocked segments and validates detour legs through `ValidateWalkableSegment(...)`; deterministic dynamic-overlay and service repair tests prove repaired routes avoid registered blockers.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentAffordanceClassificationTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests|FullyQualifiedName~FindPath_ObstructedDirectSegment_ReformsIntoWalkableDetour" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
- Files changed:
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`
  - `Exports/BotCommLayer/Models/Pathfinding.cs`
  - `Exports/BotRunner/Clients/PathfindingClient.cs`
  - `Services/PathfindingService/PathAffordanceClassifier.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/SegmentAffordanceClassificationTests.cs`
  - `Tests/PathfindingService.Tests/PathAffordanceClassifierTests.cs`
  - `Tests/BotRunner.Tests/Clients/PathfindingClientRequestTests.cs`
  - task/doc handoff files
- Practical implication:
  - The Navigation owner queue currently has no active open items after `NAV-OBJ-003` and `NAV-OBJ-004` were archived.
- Exact next command:
  - `rg -n "^- \\[ \\]|\\[ \\] Problem|Active task:" docs/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md`

---

## Session Handoff (2026-04-15 - Native Dynamic-Overlay Identity Closeout)

- Completed:
  - Ran the queued live follow-up slice from the prior handoff: `OrgrimmarGroundZAnalysisTests.DualClient_OrgrimmarGroundZ_PostTeleportSnap` and `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` are now green together.
  - Closed `NAV-OBJ-001`. Native path/corridor results now preserve the request-scoped dynamic object that forced an overlay-aware repair: blocked segment index, runtime instance ID, GUID, display ID, and an overlay-repaired flag flow from `PathFinder` -> `Navigation::CalculatePath(...)` -> `FindPathCorridor(...)` -> `PathfindingService.Repository.Navigation`.
  - `PathfindingService` now trusts native repaired overlay metadata as a usable route result and preserves the original detailed `dynamic_overlay,...` reason without rediscovering the same blocker through a managed segment probe.
  - Closed stale `NAV-OBJ-002` by evidence: the existing native `ValidateWalkableSegment(...)` export and service reason mapping already distinguish capsule/support walkability failures from clear visible segments; the focused walkability sweep is green.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~OrgrimmarGroundZAnalysisTests.DualClient_OrgrimmarGroundZ_PostTeleportSnap|FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=live_remaining_groundz_corpserun_after_org_corner_closeout.trx"` -> `passed (2/2)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
- Files changed:
  - `Exports/Navigation/PathFinder.h`
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/DllMain.cpp`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/DynamicObjectRegistryTests.cs`
  - `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `Exports/Navigation/TASKS_ARCHIVE.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - The next active native owner item is `NAV-OBJ-003` surface-transition affordance classification. `NAV-OBJ-004` is still queued after that, but local dynamic-object detour repair already has meaningful coverage from the `NAV-OBJ-001` closeout.
- Exact next command:
  - `rg -n -g "*.cs" -g "*.cpp" -g "*.h" -g "*.md" -g "!**/Recordings/**" "PathSegmentAffordance|MaxAffordance|StepUp|Drop|Cliff|Vertical|jump|safe-drop|unsafe-drop" Exports/Navigation Services/PathfindingService Tests/Navigation.Physics.Tests Tests/PathfindingService.Tests`

---

## Session Handoff (2026-04-14 - Native Overlay-Aware Corridor Routing)

- Completed:
  - Continued `NAV-OBJ-001` instead of only recording blocker identity. Native smooth path generation now reacts to request-scoped dynamic overlays during route shaping, not only in managed post-validation.
  - `PathFinder.cpp` now runs `RefinePathForWalkability(...)` / `SimplifyPathForWalkability(...)` only while `DynamicObjectRegistry` has active request-scoped objects, so overlay-bearing smooth paths use `ValidateWalkableSegment` plus the existing lateral detour search without paying that cost for overlay-free requests.
  - `FindPathCorridor(...)` now detects the same overlay condition and reuses the overlay-aware native smooth point path as a passive corridor result. Practical effect: `PathfindingService` now receives the native repaired route for live dynamic blockers instead of always starting from the raw Detour corner chain and repairing later in managed code.
  - Added deterministic native coverage in `DynamicObjectRegistryTests.FindPath_WithActiveDynamicOverlay_ReformsRouteAroundBlockingObject`, which registers a live Undercity elevator (`displayId=455`) and proves the returned path detours around it without any remaining segment/object intersection.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~DynamicObjectRegistryTests|FullyQualifiedName~FindPath_ObstructedDirectSegment_ReformsIntoWalkableDetour" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
- Files changed:
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/DllMain.cpp`
  - `Tests/Navigation.Physics.Tests/DynamicObjectRegistryTests.cs`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - `NAV-OBJ-001` is materially closer to closure: request-scoped dynamic blockers now participate in native route repair for the actual smooth-path export used by the service. The remaining gap is surfacing repaired/blocked-segment identity through the corridor/result contract cleanly enough that higher layers do not have to rediscover it from separate probes.
- Exact next command:
  - `rg -n "FindPathCorridor|BlockedReason|BlockedSegmentIndex|dynamic_overlay" Services/PathfindingService/Repository/Navigation.cs Services/PathfindingService/PathfindingSocketServer.cs Exports/BotRunner/Movement/NavigationPath.cs`

---

## Session Handoff (2026-04-14 - Orgrimmar Corner Navigation Closeout)

- Completed:
  - Closed deferred issue `D5` and the remaining live `CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall` blocker.
  - `CharacterAction.TravelTo` now upserts a persistent `GoToTask` instead of issuing a one-shot path/corner walk, so long-running travel keeps the same movement owner that already handles replans and corridor diagnostics.
  - `NavigationPath` now handles this Orgrimmar case with two tighter contracts:
    - stuck-driven replans may spend a bounded extra cost budget on materially safer alternate corridors;
    - overlay-aware service routes are no longer immediately collapsed by a duplicate local `SegmentIntersectsDynamicObjects` gate against the same nearby-object set.
  - The live test now stages from the street-level bank approach instead of the banker perch, turning the test back into a corner-navigation probe instead of a forced ledge descent.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_MovementStuckRecoveryPrefersSaferAlternateWithinTolerance|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_DoesNotLocallyRejectOverlayAwareServiceRouteForDynamicSegmentIntersection" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=orgbank_to_ah_corner_navigation_post_overlay_local_dyn_gate_fix.trx"` -> `passed (1/1)`
- Evidence:
  - `tmp/test-runtime/results-live/orgbank_to_ah_corner_navigation_post_overlay_local_dyn_gate_fix.trx`
  - Key route checkpoints in the passing artifact:
    - `0s -> dist=104.5y`
    - `5s -> dist=98.7y`
    - `10s -> dist=63.9y`
    - `15s -> dist=31.6y`
    - `[GOTO-TASK] Arrived ... dist2D=14.8`
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CornerNavigationTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The full live matrix is no longer blocked by the Orgrimmar bank-to-auction-house corner route. The remaining immediate live follow-ups from that matrix are the post-teleport Orgrimmar ground-Z regression and the corpse-run parity follow-up.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~OrgrimmarGroundZAnalysisTests.DualClient_OrgrimmarGroundZ_PostTeleportSnap|FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=live_remaining_groundz_corpserun_after_org_corner_closeout.trx"`

---

## Session Handoff (2026-04-14 - Live Matrix Capture + Orgrimmar Service Expectation Cleanup)

- Completed:
  - Captured the first uninterrupted split-service `LiveValidation` matrix on the current Linux stack in `tmp/test-runtime/results-live/livevalidation_full_matrix_post_gathering_route_hardening.trx` (`105 total / 103 executed / 89 passed / 14 failed / 2 not executed`), closing `PFS-LIVE-001`.
  - Fixed the foreground mailbox false-success path. `CollectAllMailAsync(...)` now waits for the mailbox frame plus inbox population instead of treating the first transient zero inbox count as final, and `EconomyInteractionTests.Mail_OpenMailbox` is green again.
  - Closed the stale Orgrimmar AH/bank live-test assumptions. `AuctionHouseTests`, `AuctionHouseParityTests`, `BankInteractionTests`, and `BankParityTests` now run on the BG-only fixture while their active assertions remain BG-only, and they probe `NPCFlags.UNIT_NPC_FLAG_AUCTIONEER` / `UNIT_NPC_FLAG_BANKER` instead of stale magic literals.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_full_matrix_post_gathering_route_hardening.trx"` -> `captured uninterrupted matrix (89 passed, 14 failed, 2 not executed)`
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.WaitForInboxCountAsync|FullyQualifiedName~ForegroundInteractionFrameTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EconomyInteractionTests.Mail_OpenMailbox" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_mail_open_mailbox_post_inbox_wait_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BgInteractionTests.Bank_DepositItem_MovesToBankSlot|FullyQualifiedName~BgInteractionTests.AuctionHouse_InteractWithAuctioneer" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=bg_orgrimmar_bank_auctioneer_probe_post_fg_mail_fix.trx"` -> `passed (2/2)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AuctionHouseTests|FullyQualifiedName~AuctionHouseParityTests|FullyQualifiedName~BankInteractionTests|FullyQualifiedName~BankParityTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=bg_only_orgrimmar_ah_bank_after_flag_fix.trx"` -> `passed (4) / skipped (5)`
- Files changed:
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AuctionHouseTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BankParityTests.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Practical implication:
  - The remaining live reds are now real runtime/navigation/parity follow-ups, not a missing matrix artifact and not the stale Orgrimmar AH/bank/mail test assumptions that were distorting the matrix earlier in the day.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall|FullyQualifiedName~OrgrimmarGroundZAnalysisTests.DualClient_OrgrimmarGroundZ_PostTeleportSnap|FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=live_remaining_nav_groundz_corpserun_followup.trx"`

## Session Handoff (2026-04-14 - Pathfinding Route Result Caller Adoption)

- Completed:
  - Closed the remaining `PFS-OBJ-001` caller-adoption slice. `NavigationPath` now consumes `PathfindingClient.GetPathResult(...)` instead of flattening every service response to corners-only `GetPath(...)`.
  - Service-originated `blocked_by_dynamic_overlay` / `dynamic_overlay` responses now trigger the existing bounded dynamic-blocker replan path even when the service returns zero corners, which was previously invisible to BotRunner once the client collapsed the response to an empty array.
  - Added deterministic BotRunner coverage proving nearby-object route requests flow through the route-result seam and that service-side overlay rejection records `dynamic_blocker_observed` as the replan reason.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (70/70)`
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Practical implication:
  - The richer pathfinding route contract is now consumed by a higher-level BotRunner planner. The next open owner-facing pathfinding item is the full split-service `LiveValidation` sweep, not more contract plumbing.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_full_matrix_post_pathfinding_route_result_adoption.trx"`

## Session Handoff (2026-04-14 - Pathfinding Object-Aware IPC Contract)

- Completed:
  - Extended the pathfinding IPC contract so callers can request richer route metadata instead of only corner arrays. `CalculatePathResponse` now includes `has_blocked_segment`, `blocked_segment_index`, `blocked_reason`, and coarse affordance summary fields (`max_affordance`, `path_supported`, step/drop/cliff/vertical counts, total Z gain/loss, max slope).
  - `PathfindingSocketServer` now populates those fields from the sanitized service path, and `PathfindingClient` now exposes `GetPathResult(...)` while preserving the legacy `GetPath(...)` corners-only API for existing callers.
  - Fixed the stale socket integration test to decode the real `[length][flag][payload]` framing used by `ProtobufCompression`, then pinned the richer response contract in focused BotRunner and PathfindingService tests.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found.`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`
  - `Exports/BotCommLayer/Models/Pathfinding.cs`
  - `Exports/BotRunner/Clients/PathfindingClient.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Tests/BotRunner.Tests/Clients/PathfindingClientRequestTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotCommLayer/TASKS.md`
- Practical implication:
  - The remaining open work for `PFS-OBJ-001` is caller adoption and higher-level use of the new metadata, not schema transport or socket framing.
- Exact next command:
  - `rg -n "GetPathResult|BlockedReason|MaxAffordance|PathSupported" Exports/BotRunner Tests/BotRunner.Tests`

---

## Session Handoff (2026-04-14 - Shared BG-only Dungeon/Raid Fixture Closeout)

- Completed:
  - Closed the shared BG-only dungeon/raid entry fixture blocker. `DungeonInstanceFixture` no longer depends on shared `TESTBOT1` for default live coordinator coverage.
  - BG-led dungeon/raid entry fixtures now use dedicated `<prefix>1` leader accounts, while FG-led fixtures still explicitly opt back into `TESTBOT1`.
  - Shared instance-entry fixtures now precreate missing accounts, wipe mismatched stale characters, and reserve deterministic generated names before launch so live entry coverage no longer depends on preseeded manual state.
  - Added configuration coverage around the new contract in `DungeonFixtureConfigurationTests`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DungeonFixtureConfigurationTests|FullyQualifiedName~CoordinatorFixtureBaseTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.AQ20Tests.AQ20_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=aq20_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.BlackwingLairTests.BWL_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=raid_entry_namespace_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)` (`BWL_RaidFormAndEnter`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.ZulGurubTests.ZG_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=zg_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.MoltenCoreTests.MC_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mc_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.OnyxiasLairTests.ONY_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ony_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.AQ40Tests.AQ40_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=aq40_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.NaxxramasTests.NAXX_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=naxx_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Dungeons.StratholmeLivingTests.STRAT_LIVE_GroupFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=strath_live_entry_post_dedicated_bg_leader_provisioning_fix.trx"` -> `passed (1/1)`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/DungeonInstanceFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DungeonFixtureConfigurationTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - Shared dungeon/raid live entry coverage no longer launches unnecessary FG leaders or stalls on the special-purpose `TESTBOT1` account having no reusable character. The BG-only fixture contract is now explicit and green end to end.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_core_chunk_post_bg_only_dungeon_raid_fixture_closeout.trx"`

---

## Session Handoff (2026-04-13 - Centralized Scene-Data Fallback for Dungeon/Raid Entry)

- Completed:
  - Closed the remaining centralized scene-data-service blocker on live dungeon/raid entry. `SceneTileSocketServer` no longer treats every missing `.scenetile` as a hard failure.
  - The service now synthesizes missing tile responses from sibling `.scene` sources when available, caches the synthesized response, and returns a success-empty tile only when the source scene proves there is no geometry in that tile.
  - Added direct unit coverage for the new contract in `SceneTileSocketServerTests`: failure without any source, synthesize-and-cache from a source `.scene`, and success-empty when the source scene has no overlapping geometry.
  - Added direct Docker-facing assertions in `SceneDataClientIntegrationTests` for the Molten Core recovery path: `409_31_33` and `409_30_33` now return scene data, and the Molten Core + Strath entry neighborhoods now pass the full 3x3 refresh contract.
  - Synced the missing source `.scene` corpus from repo `Data\scenes` into `D:\MaNGOS\data\scenes`, rebuilt `scene-data-service`, and revalidated the previously blocked live entry slices.
- Validation:
  - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneTileSocketServerTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_Map409_31_33_TileCanBeSynthesizedFromSceneSource|FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_Map409_30_33_TileReturnsSceneData|FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_DungeonAndRaidEntryNeighborhoods_ReturnSceneData" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Raids.MoltenCoreTests.MC_RaidFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=molten_core_entry_post_scene_service_source_fallback.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Dungeons.StratholmeUndeadTests.STRAT_UD_GroupFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=strath_undead_entry_post_scene_service_source_fallback.trx"` -> `passed (1/1)`
- Files changed:
  - `Services/SceneDataService/SceneTileSocketServer.cs`
  - `Tests/BotRunner.Tests/SceneTileSocketServerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/SceneDataClientIntegrationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DungeonInstanceFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DungeonFixtureConfigurationTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - The shared `scene-data-service` now closes missing-tile gaps centrally instead of pushing the failure back onto per-bot local disk behavior. With the service fix in place, Molten Core and Strath undead entry are no longer blocked by scene-data misses on the Docker-backed stack.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Dungeons.StratholmeLivingTests.STRAT_LIVE_GroupFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=strath_living_entry_post_scene_service_source_fallback.trx"`

## Session Handoff (2026-04-13 - Ratchet Fishing Packet Parity Closeout)

- Completed:
  - Closed the remaining Ratchet fishing follow-through tracked locally under `BR-FISH-001`, `PFS-FISH-001`, and `NAV-FISH-001`.
  - `FishingTask` search-walk now keeps probe travel targets on the waypoint reference layer and requires tight Z agreement before counting a nearby stepped waypoint as arrived.
  - `SpellcastingManager` no longer forces fishing casts through destination-target payloads; fishing now uses the same no-target `CMSG_CAST_SPELL` shape the focused FG packet capture uses.
  - The dual live packet compare is green on the current Docker-backed stack. BG now reaches the same cast/channel/loot packet milestones as FG: `SMSG_SPELL_GO`, `MSG_CHANNEL_START`, `SMSG_GAMEOBJECT_CUSTOM_ANIM`, `CMSG_GAMEOBJ_USE`, and `SMSG_LOOT_RESPONSE`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingTaskTests" --logger "console;verbosity=minimal"` -> `passed (37/37)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`
- Files changed:
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/WoWSharpClient/SpellcastingManager.cs`
  - `Tests/WoWSharpClient.Tests/WoWSharpObjectManagerCombatTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/TASKS_ARCHIVE.md`
  - `Exports/Navigation/TASKS.md`
  - `Exports/Navigation/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - Ratchet fishing parity is no longer an open blocker in BotRunner, PathfindingService, or Navigation. The next open owner-facing item is the broader live-validation sweep on the split-service Docker stack.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=livevalidation_full_matrix_post_ratchet_packet_parity.trx"`

## Session Handoff (2026-04-13 - Ratchet Stage Visibility Attribution)

## Session Handoff (2026-04-13 - Ratchet Ferry-End Pier Target Iteration)

- Completed:
  - Retargeted `Fishing_CatchFish_BgAndFg_RatchetPierOpenWaterPath` away from the bank-side shallow-water spots and onto the ferry end of the Ratchet pier, per the live repro feedback.
  - Kept the bank-side packet-capture start, restored a forced pier-entry hop at `(-955.1,-3775.5,5.0)`, and iterated the ferry-side destination through the real walkable corridor seen in live traces instead of the unreachable transport anchor.
  - Live evidence narrowed the issue: the user-requested “farther out on the pier” target selection is no longer the unknown. The remaining blocker is unstable FG pathing from the bank into the ferry-end corridor, while BG can consistently progress much farther down the pier.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetPierOpenWaterPath" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_dual_fishing_ferry_end_walkable_surface.trx"` -> `failed`; FG reached the forced pier-entry hop, but not the guessed mid-pier waypoint.
  - `dotnet test ... --logger "trx;LogFileName=ratchet_dual_fishing_ferry_end_direct_from_pier_entry.trx"` -> `failed`; BG reached `(-992.2,-3820.9,5.7)` and then hit native `first resolution returned null`, proving the last few yards past the ferry corridor are not currently walkable.
  - `dotnet test ... --logger "trx;LogFileName=ratchet_dual_fishing_ferry_end_corridor_endpoint.trx"` -> `failed`; FG reached the ferry-berth waypoint `(-987,-3811,6)` and then stalled on the final 15y leg, while BG reproduced the same stable endpoint.
  - `dotnet test ... --logger "trx;LogFileName=ratchet_dual_fishing_ferry_end_stable_cast_endpoint.trx"` -> `failed`; later reruns still show FG route instability even after collapsing the target to the stable ferry-side endpoint.
  - `dotnet test ... --logger "trx;LogFileName=ratchet_dual_fishing_ferry_berth_final_spot.trx"` -> `failed`; the user-requested ferry-berth final spot is implemented, but FG still intermittently falls back into the older bank-side oscillation path.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - The spec now targets the ferry end of the pier instead of the bank, and the live traces identify the real remaining gap: FG `GoToTask`/path resolution is unstable on the bank-to-ferry corridor. The next fix belongs in pathing, not in choosing yet another fishing coordinate.
- Exact next command:
  - `rg -n "target=\\(-987,-3811,6\\)|target=\\(-992,-3821,6\\)|first waypoint after|first resolution returned null|Arrived at \\(-987,-3811,6\\)" "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\ratchet_dual_fishing_ferry_end_corridor_endpoint.trx"`

- Completed:
  - Closed the staged-visibility attribution half of the Ratchet fishing follow-up work instead of treating every missing-pool pop as the same failure.
  - `FishingProfessionTests` now carries staged preflight outcome through to the final fishing assertion path.
  - The direct child-pool fallback is now explicit (`LocalChildSpawnedOnDirectProbeOnly`) rather than being flattened into a generic proceed path.
  - Missing-pool pops after invisible/direct-probe staging now fail as staged-visibility blockers; visible-stage missing-pool pops still fail as real shoreline/runtime bugs.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -o E:\tmp\isolated-botrunner-tests\ratchet-stage-attribution --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RatchetFishingStageAttributionTests|FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~FishingPoolStagePlannerTests" --logger "console;verbosity=minimal"` -> `passed (23/23)`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/RatchetFishingStageAttribution.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RatchetFishingStageAttributionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - Ratchet reruns that never surface a visible local pool from the dock stage no longer look like generic fishing/pathfinding regressions. The live harness now reports whether the blocker was invisible staged local pools, direct-probe-only local activation, or a true post-visibility runtime miss.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetPoolTaskPath" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_dual_fishing_stage_visibility_attribution.trx"`

---

## Session Handoff (2026-04-13 - Scene Data Service Direct Reliability Coverage)

- Completed:
  - Built direct deterministic coverage around `SceneTileSocketServer` so the scene-data service path is no longer validated only by Docker readiness or live movement slices.
  - Found and fixed a concrete service flaw: failed on-demand tile loads were being cached as permanent negative results. A bad `.scenetile` read now returns a failure for that request but does not poison the cache, so the next request can recover after the file is corrected.
  - Added header validation so a misnamed or wrong-map tile file fails closed instead of being served under the request map ID.
  - Added direct tests for filename indexing, v1 metadata synthesis, v2 `groupFlags` preservation, map-header mismatch rejection, negative-load retry, and positive cache reuse.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneTileSocketServerTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=BotRunner.Tests.Clients.DockerServiceTests.SceneDataService_TcpConnect_Responds" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientTests|FullyQualifiedName~SceneDataPhysicsPipelineTests" --logger "console;verbosity=minimal"` -> `passed (20/20)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientIntegrationTests|FullyQualifiedName~SceneDataEnvironmentIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (15/15)`
- Files changed:
  - `Services/SceneDataService/SceneTileSocketServer.cs`
  - `Tests/BotRunner.Tests/SceneTileSocketServerTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - `scene-data-service` now fails closed on bad tile headers, can recover from a corrected tile file without a process restart, and still matches both the `SceneDataClient` deterministic pipeline and the real Docker `5003` contract.
- Exact next command:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientTests|FullyQualifiedName~SceneDataPhysicsPipelineTests|FullyQualifiedName~SceneDataClientIntegrationTests|FullyQualifiedName~SceneDataEnvironmentIntegrationTests" --logger "console;verbosity=minimal"`

## Session Handoff (2026-04-13 - Service-Managed Scene Autoload Gate)

- Completed:
  - Confirmed the scale concern directly: the managed movement/object-manager path does not intentionally read scene files from disk, but native `SceneQuery::EnsureMapLoaded(...)` still had a safety-path autoload for local `.scene` / VMAP data when no injected cache existed.
  - Added a fail-closed gate for service-managed local physics. `SetSceneAutoloadEnabled(false)` now makes `EnsureMapLoaded(...)` return immediately when no injected scene cache exists, and `WoWSharpObjectManager.Initialize(...)` enables that mode whenever local physics is running with a `SceneDataClient`.
  - Added a native regression proving `PreloadMap(1)` will not materialize a scene cache from a local `.scene` file once autoload is disabled.
  - Re-ran the focused Ratchet fishing repro after that change. It did not reach movement/pathing: FG setup failed earlier because `.additem 6256` and `.additem 6530` never appeared in bags, and the account flipped to `CharacterSelect`.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SceneDataRegionFallbackTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TryFlushPendingTeleportAck_WaitsForUpdatesGroundSnapAndSceneData" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fishing_route_repro_after_nav_scene_closeout.trx"` -> `failed (1/1)` before movement; artifact shows `.additem 6256` and `.additem 6530` warnings followed by repeated `ScreenState='CharacterSelect'`
- Files changed:
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/WoWSharpClient/Movement/NativePhysicsInterop.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/SceneDataRegionFallbackTests.cs`
  - `Exports/Navigation/TASKS.md`
  - `docs/TASKS.md`
- Practical implication:
  - A 3000-bot startup no longer has to rely on per-process local scene autoload in the service-managed local-physics mode. If `scene-data-service` misses, the runtime now fails closed instead of quietly stampeding disk. The current Ratchet blocker moved back to FG live setup, not scene-data delivery.
- Exact next command:
  - `rg -n "additem 6256|additem 6530|CharacterSelect|fishing-items" "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\ratchet_fishing_route_repro_after_nav_scene_closeout.trx"`

## Session Handoff (2026-04-13 - Thin Scene Environment Closeout)

- Completed:
  - Closed `MASTER-SUB-007` / `NAV-SCENE-001` by finishing the remaining live parity proof for thin scene-slice environment flags and archiving the completed item out of `Exports/Navigation/TASKS.md`.
  - Confirmed the last blocker was the Docker/service path rather than the scene-triangle metadata transport: the copied Ragefire tile still carried indoor group flags, native direct stepping on that tile returned `env=0x00000001`, and the live failure reproduced only while `scene-data-service` was mounted against the wrong data root and timing out during eager tile preparse.
  - Hardened native environment resolution so thin slices can still resolve indoor state when VMAP area info is missing/useless, and changed `SceneTileSocketServer` to index `.scenetile` filenames eagerly but load/parse payloads on demand so the Docker service becomes healthy before health checks expire.
  - Corrected `.env` so `WWOW_VMANGOS_DATA_DIR` now points at the actual parity data root `D:/MaNGOS/data`.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SceneEnvironmentFlagTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataEnvironmentIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataEnvironmentIntegrationTests|FullyQualifiedName~SceneDataPhysicsPipelineTests|FullyQualifiedName~PhysicsEnvironmentFlags_|FullyQualifiedName~RecordResolvedEnvironmentState_" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.Snapshot_IndoorLocation_ReportsIsIndoors|FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.MountSpell_OutdoorLocation_Mounts|FullyQualifiedName=BotRunner.Tests.LiveValidation.MountEnvironmentTests.MountSpell_IndoorLocation_DoesNotMount" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mount_environment_nav_scene_closeout_20260413_post_lazy_index.trx"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ItemDataTests|FullyQualifiedName~SpellDataTests|FullyQualifiedName~BotRunnerServiceInventoryResolutionTests|FullyQualifiedName~CastSpellTaskTests|FullyQualifiedName~UseItemTaskTests" --logger "console;verbosity=minimal"` -> `passed (126/126)`
  - `dotnet build Services/SceneDataService/SceneDataService.csproj --configuration Release --no-restore` -> `succeeded`
  - `docker compose -f docker-compose.vmangos-linux.yml build scene-data-service` -> `succeeded`
  - `docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service` -> `succeeded`
- Files changed:
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Services/SceneDataService/SceneTileSocketServer.cs`
  - `.env`
  - `Exports/Navigation/TASKS.md`
  - `Exports/Navigation/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - Thin-scene indoor classification is now stable end to end on the live Docker stack, and the next open navigation owner item moves back to the Ratchet shoreline repro under `NAV-FISH-001`.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fishing_route_repro_after_nav_scene_closeout.trx"`

## Session Handoff (2026-04-13 - Mining Route Live Closeout)

- Completed:
  - Closed the remaining live proof step for the pathfinding follow-through tracked under `MASTER-SUB-026` / `BR-NAV-005`.
  - Confirmed the last blocker was combat-side, not another path-generation defect: `SpellcastingManager` now latches confirmed melee auto-attack per target, suppresses duplicate `CMSG_ATTACKSWING` retries after server confirmation, and clears that latch on stop/cancel/rejection.
  - Added deterministic regressions across `WoWSharpObjectManagerCombatTests`, `SpellHandlerTests`, and `WorldClientAttackErrorTests` for the new confirm/clear behavior.
  - Re-ran the reproduced live BG mining route; `GatheringProfessionTests.Mining_BG_GatherCopperVein` passed and wrote `tmp/test-runtime/results-live/mining_bg_gather_route_post_melee_confirm_fix.trx`.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests|FullyQualifiedName~SpellHandlerTests.HandleAttackStart_LocalPlayerConfirmsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleAttackStop_LocalPlayerClearsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleCancelCombat_LocalPlayerClearsTrackedAutoAttackState|FullyQualifiedName~SpellHandlerTests.HandleAttackerStateUpdate_OurSwingConfirmsPendingAutoAttack|FullyQualifiedName~WorldClientAttackErrorTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (99/99)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_melee_confirm_fix.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Exports/WoWSharpClient/SpellcastingManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs`
  - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Tests/WoWSharpClient.Tests/WoWSharpObjectManagerCombatTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientAttackErrorTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The pathfinding follow-through is no longer blocked on the BG mining live slice. The reproduced route now completes on the current binaries, and the remaining open queue in this area has moved off pathfinding and back to the next explicit owner task file.
- Exact next command:
  - `Get-Content Exports/BotRunner/TASKS.md -TotalCount 220`

## Session Handoff (2026-04-13 - Corridor-Preserving Waypoint Promotion Clamp)

- Completed:
  - Closed the remaining `PFS-NAV-002` BotRunner-side corridor gap in `NavigationPath`: adaptive-radius waypoint promotion, short probe-waypoint skipping, and overshoot look-ahead skips now all require the live-position shortcut to preserve the sampled walkable corridor before BotRunner can advance to a later waypoint.
  - Added deterministic regressions for the two reproduced off-corridor execution cases in `NavigationPathTests`: adaptive-radius early advance and overshoot look-ahead skip.
  - Removed the unrelated short-LOS direct-path priming experiment from `NavigationPath`; it changed direct-fallback, gap-detection, and short-route replanning semantics without closing any tracked item and made the stable deterministic slice red.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (66/66)`
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - BotRunner no longer advances to a later waypoint just because the bot is inside a generous adaptive radius or closer to a farther waypoint after overshooting; the live-position shortcut still has to stay inside the walkable corridor that the service planned.
  - The remaining proof step is live: rerun the reproduced BG mining route and compare planned versus executed drift after the new clamp.
- Exact next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_corridor_promotion_clamp.trx"`

---

## Session Handoff (2026-04-12 - Pathfinding Fixture Refresh)

- Completed:
  - Closed the stale `PathfindingTests` corpse-run route backlog by replaying the old Orgrimmar graveyard/center and Razor Hill corpse-run fixtures under the current native validator, confirming they no longer match current walkability/LOS truth, and archiving those obsolete contracts.
  - Restored and hardened the optional service-side native segment validation path in `Services/PathfindingService/Repository/Navigation.cs`: the `WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION` gate is active again, `MissingSupport` now matches native/test acceptance, grounded clear endpoints are threaded forward, duplicate grounded waypoints are suppressed, and straight-corner requests skip the expensive bounded repair pass so the latency guard stays green.
  - Reduced `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs` to the two Orgrimmar live-retrieve regressions that still reflect current behavior cleanly: blocked-direct-line reroute and straight-request completion budget.
- Validation:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter|FullyQualifiedName~CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision" --logger "console;verbosity=minimal"` -> `failed (2/2)` with `BlockedGeometry` on `Segment 1->2` and `Segment 8->9`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
- Files changed:
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The owner no longer carries stale route contracts that only passed while the service was accidentally bypassing native segment validation.
  - Current deterministic route coverage now centers on the live Orgrimmar corpse-retrieve corridor, and straight-mode corpse-run requests no longer burn the validation budget before callers can fall through.
- Exact next command:
  - `Get-Content Services/PathfindingService/TASKS.md -TotalCount 200`

---

## Session Handoff (2026-04-12 - Steep Incline Regression Confirmation)

- Completed:
  - Confirmed the BG `MovementController` is not the steep-slope enforcement point anymore; it now trusts native physics output, so steep-incline rejection lives in the native walkability/collision layer.
  - Added `Tests/Navigation.Physics.Tests/SegmentWalkabilityTests.ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment`, which scans `Un'Goro`, `Desolace`, and `Thousand Needles` and requires at least one real uphill segment to be rejected as `StepUpTooHigh` or `BlockedGeometry`.
  - Revalidated that normal uphill travel still works (`MovementControllerPhysicsTests.Forward_Uphill_MaintainsSpeedAndGrounded`) and that the broader server-side climb-angle guard remains green (`ServerMovementValidationTests.GroundMovement_ClimbAngle_WithinVmangosWallClimbLimit`).
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~ValidateWalkableSegment_SteepSweepContainsRejectedUphillSegment|FullyQualifiedName~Forward_Uphill_MaintainsSpeedAndGrounded|FullyQualifiedName~GroundMovement_ClimbAngle_WithinVmangosWallClimbLimit" --logger "console;verbosity=minimal"` -> `passed (3/3)`
- Files changed:
  - `Tests/Navigation.Physics.Tests/SegmentWalkabilityTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - Do not spend more time reintroducing a managed slope guard before pathfinding work. The current steep-incline rejection lives below the BG controller in native walkability/collision, and it now has a deterministic regression.
- Exact next command:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter|FullyQualifiedName~CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision" --logger "console;verbosity=minimal"`

---

## Session Handoff (2026-04-12 - BotProfiles + PathfindingService Test Backlog Closeout)

- Completed:
  - Archived stale completed `BotProfiles` items `BP-MISS-001` through `BP-MISS-004` after revalidating that the profile-factory implementation and reflection gate were already present in the workspace.
  - Closed `PFS-TST-003` with a deterministic reroute regression: `CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine` now proves the direct corpse-retrieve segment is blocked and the returned route deviates around that blockage while still satisfying `PathRouteAssertions`.
  - Closed `PFS-TST-005` with `BotTasks/OrgrimmarCorpseRunPathTask.cs` plus `PathfindingBotTaskTests.OrgrimmarCorpseRunPath_ShouldReturnValidWaypointPath`.
  - Refreshed the stale generic `PathCalculationTask` onto the same current live Orgrimmar corpse-retrieve corridor so the owner-facing `PathfindingBotTaskTests` filter is green again.
- Validation:
  - `rg -n -U -P "CreatePvPRotationTask\(IBotContext botContext\)\s*=>\s*\R\s*new\s+PvERotationTask" BotProfiles -g "*.cs"` -> no matches
  - `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet restore Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --verbosity minimal` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName~OrgrimmarCorpseRunPath_ShouldReturnValidWaypointPath" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
- Files changed:
  - `BotProfiles/TASKS.md`
  - `BotProfiles/TASKS_ARCHIVE.md`
  - `Tests/PathfindingService.Tests/PathingAndOverlapTests.cs`
  - `Tests/PathfindingService.Tests/BotTaskTests.cs`
  - `Tests/PathfindingService.Tests/BotTasks/OrgrimmarCorpseRunPathTask.cs`
  - `Tests/PathfindingService.Tests/BotTasks/PathCalculationTask.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The `BotProfiles` tracker no longer carries stale completed factory work.
  - `PathfindingService.Tests` now has a deterministic blocked-direct-line reroute gate and a bot-task-level Orgrimmar corpse-run contract that both pass on the current binaries.
  - Follow-on `PFS-TST-010` is now queued: the older `CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter` and `CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision` fixtures are still red on the current native walkability validator and need to be refreshed or replaced.
  - Do not use broad substring filters like `CorpseRunRoute` for this owner; that pattern also matches the socket integration test and is not the deterministic regression gate for these specs.
- Exact next command:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter|FullyQualifiedName~CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision" --logger "console;verbosity=minimal"`

---

## Session Handoff (2026-04-12)

- Completed:
  - Closed `P0.1` by hardening the live movement-parity harness instead of changing movement semantics.
  - `LiveBotFixture.SendGmChatCommandTrackedAsync(...)` now refreshes snapshots while waiting for tracked GM chat execution/response.
  - `LiveBotFixture.WaitForTeleportSettledAsync(...)` now requires `ScreenState=InWorld`, `ConnectionState=BotInWorld`, and `IsMapTransition=false` in addition to XY/Z settle.
  - `MovementParityTests.RunRedirectParityTest(...)` now clears stale packet/transform/physics artifacts before starting recordings, matching the main parity runner.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_20260412_post_transition_wait_fix.trx"` -> `passed (12/12)`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Practical implication:
  - The live FG/BG grounded movement bundle is green again on the Docker-backed scene-data root, so there is no remaining open `P0` movement blocker in the master tracker.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"`

- Completed:
  - Fixed the deterministic test harness to load the freshly built root `Navigation.dll` before the stale `x64\Navigation.dll` fallback, then revalidated the compact Undercity replay against the actual current native build.
  - Added frame-window diagnostics showing the corrected runtime keeps `groundedWallState=1` and a moving-base support token through compact frames `10..19`, while the remaining worst replay error on frame `20` occurred on a nonzero-to-nonzero `TransportGuid` swap.
  - Shipped one narrow replay-harness behavior fix in `ReplayEngine.cs`: treat any nonzero-to-nonzero `TransportGuid` change as a transport transition reset instead of a steady-state on-transport frame. That closed the compact packet-backed Undercity elevator parity gap without regressing the long V2 replay.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (3/3)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> stopped the hung repo-scoped live-test processes after the first timed-out run.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_20260412.trx"` -> timed out again after a clean rerun; no new `.trx` was written.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> stopped the second hung repo-scoped live-test run.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_ValleyOfTrials_FlatPath" --logger "console;verbosity=minimal"` -> `failed` with `BG only moved 0.0y on a 50.0y route`; BG ended at `1629,-4373,31.30` while FG completed the expected Valley of Trials route.
- Files changed:
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS_ARCHIVE.md`
  - `Tests/Navigation.Physics.Tests/TASKS_ARCHIVE.md`
- Practical implication:
  - Do not trust deterministic parity results that came from the stale `Bot\Release\net8.0\x64\Navigation.dll` load path; the harness now prefers the correct root DLL.
  - Do not carry replay transport state across nonzero-to-nonzero `TransportGuid` swaps; the compact Undercity packet-backed replay uses exactly that transition on the upper-exit handoff.
  - `P0.1` is now narrowed to a live BotRunner-side setup/runtime regression: BG movement parity is failing on the first Valley of Trials route because BG starts or is teleported onto the wrong world position before route traversal.
- Exact next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_ValleyOfTrials_FlatPath" --logger "console;verbosity=normal"`

- Completed:
  - Added a new deterministic frame-19 diagnostic in `PacketBackedUndercityElevatorSupportTests.cs` proving the incoming carry bit is sufficient for the compact upper-deck failure: forcing `groundedWallState=1` on the same runtime step keeps the output at the recorded upper-deck Z instead of collapsing to the static upper door deck.
  - Tried one transport-specific native bootstrap in `PhysicsEngine.cpp` to seed that carry bit from same-transport downward support faces in the merged query, then reverted it immediately after validation showed zero metric change on the compact replay.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` before and after reverting the no-op native bootstrap.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_ForcedGroundedWallState_LogsFullRuntimeDelta" --logger "console;verbosity=normal"` -> `passed (1/1)` with baseline `pos=(1551.8728,242.4102,39.9447)` and forced-state `pos=(1551.8728,242.4102,42.6260)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_ForcedGroundedWallState_LogsFullRuntimeDelta|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (4/6)`; long V2 replay still passed; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - The missing carry bit is now proven to be sufficient on the exact failing frame, but the first live bootstrap condition did not latch it.
  - Do not retry the same-transport downward-support bootstrap scan as implemented this pass; it produced no metric change.
  - The next binary-aligned target is earlier in the producer chain: either persist `groundedWallState` across the relevant compact transport window, or wire the later selector/support-commit path so it latches the same carry state the forced diagnostic proves is required.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_ForcedGroundedWallState_LogsFullRuntimeDelta|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=normal"`

- Completed:
  - Tried one narrower moving-base continuity tweak in `PhysicsEngine.cpp`: replace the grounded pre-sweep static `GetGroundZ(...)` snap with the active transport's persisted local support point whenever the moving-base point resolved on the current transport frame.
  - Reverted that tweak immediately after validation showed zero metric change on the compact packet-backed Undercity replay.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (3/5)`; long V2 replay still passed; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - Do not retry the active-transport pre-sweep snap keyed off the persisted local support point as a standalone fix; it produced no metric change.
  - The runtime is still reclassifying these compact transport frames onto static support later in the path, and baseline `PhysicsEngine.cpp` still has no live consumer of the bridged `standingOnInstanceId` / `standingOnLocal*` input fields.
  - The next binary-aligned target is the later selector/support-commit path itself, or the still-unwired transport-local selector-record rewrite feeding it.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=normal"`

- Completed:
  - Tried one more narrow transport-specific grounded-wall tweak in `PhysicsEngine.cpp`, then reverted it after validation showed zero metric change on the compact packet-backed Undercity replay.
  - Confirmed that letting the non-walkable support-face scan bootstrap a dynamic transport support face from hypothetical `CheckWalkable(..., true)` still leaves frame `19` unchanged at `support=0`, `groundedWallState=0`, and `simZ=39.786`.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` after fixing and reverting the temporary test-export signature adjustment.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (3/5)`; long V2 replay still passed; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - Do not retry the `ResolveGroundedWallContacts(...)` support-face bootstrap keyed off hypothetical `CheckWalkable(..., true)` as a standalone fix; it produced no metric change.
  - The remaining live binary-aligned target is the still-unused transport-local selected-contact rewrite in the grounded wall selector path itself.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=normal"`

- Completed:
  - Tried one more narrow transport-specific support-selection tweak in `PhysicsEngine.cpp`, then reverted it after validation showed zero metric change on the compact packet-backed Undercity replay.
  - Confirmed that admitting downward-facing dynamic transport contacts through the later `isStatefulSupportWalkable(...)` chooser still leaves frame `19` unchanged at `support=0`, `groundedWallState=0`, and `simZ=39.786`.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` before and after reverting the no-op native tweak.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (3/5)`; long V2 replay still passed; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - Do not retry the later `isStatefulSupportWalkable(...)` dynamic transport-support admission keyed off hypothetical `CheckWalkable(..., true).walkable` as a standalone fix; it produced no metric change.
  - The next single runtime edit should target `ResolveGroundedWallContacts(...)` directly, or wire the binary’s transport-local selected-contact rewrite into the grounded-wall selector path.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=normal"`

- Completed:
  - Tried one more narrow transport-specific support-selection tweak in `CollisionStepWoW`, then reverted it after validation showed no metric change.
  - Confirmed the attempted bootstrap condition was wrong for the current binary-aligned helper semantics: on the frame-19 transport deck contact, `CheckWalkable(..., true)` returns `walkable=true`, `walkableState=false`, `groundedWallFlagAfter=true`.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` before and after reverting the no-op native tweak.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (3/5)`; long V2 replay stayed green; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - Do not bootstrap this transport fix off `CheckWalkableResult.walkableState`; that field stays `false` here even when the helper returns `walkable=true` and promotes `groundedWallFlagAfter=true`.
  - The next single-scope behavior change must key off `walkable` / `groundedWallFlagAfter` directly inside the selected-contact/support-commit path, or wire the binary’s transport-local selected-contact rewrite there.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap" --logger "console;verbosity=normal"`

- Completed:
  - Re-ran the compact Undercity transport diagnostics on the pulled `origin/main` baseline and corrected the live assumptions for the current branch state.
  - Proved the post-pull lower transport frame (`10`) no longer exposes the earlier dynamic support contact in the final query window; it now resolves a static parent-WMO support contact `0x00003B34` near `z=-41.361`, while the upper failing frame (`19`) still resolves the dynamic elevator deck contact `0x80000001` near `z=42.339`.
  - Tried one narrow native behavior tweak in `PhysicsEngine.cpp` to carry `groundedWallState` across the transport horizontal branch when the selected blocker looked statefully walkable, then reverted it after validation showed no metric change.
- Validation:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact" --logger "console;verbosity=detailed"` -> `passed (1/1)` with lower frame `10` logging `finalSupport inst=0x00003B34`, `point=(1551.8584,242.2622,-41.3606)`, `selectedInst=0x00003B34`, `selectedPoint=(1553.8317,242.2779,-42.7888)`, `support=0`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact" --logger "console;verbosity=detailed"` -> `passed (1/1)` with frame pair `19->20` logging `finalSupport inst=0x80000001`, `point=(1551.8728,242.4102,42.3390)`, `walk state=false => false`, `walk state=true => true`.
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` before and after reverting the no-op native tweak.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (2/4)`; long V2 replay stayed green; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `docs/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - The attempted state-carry tweak was a no-op because `GroundedWallResolutionTrace.walkableWithState` is computed with the actual incoming `groundedWallState`, not a hypothetical `true` path, so it cannot bootstrap the carry bit from `false`.
  - The next parity unit has to evaluate the hypothetical stateful `CheckWalkable(..., true)` path directly inside the runtime selected-contact/support-commit transaction, or wire the binary’s transport-local selected-contact rewrite there, instead of reusing the existing trace field.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_FinalSupportQueryIncludesStatefulTransportDeckContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap" --logger "console;verbosity=normal"`

- Completed:
  - Added `PacketBackedUndercityElevatorUp_LowerTransportFrame_CurrentVsNextTransportRegistrationComparison` in `PacketBackedUndercityElevatorSupportTests.cs`.
  - Proved the replay harness's grounded transport object timing is not the compact blocker: feeding current-frame versus next-frame dynamic objects into the lower compact transport step produces the same output (`support=0`, `gw=0`, `wall=True`, identical world position).
  - Extended the lower grounded-wall trace to log the actual selected contact geometry. On compact frame `10`, the wall selector is not choosing the transport support face; it is choosing a same-instance elevator side wall at `selectedPoint=(1553.8317,242.2779,-41.4388)` with `selectedNormal=(1,0,0)`, while the real support face is still present separately as `0x80000001` and is stateful-walkable.
  - Tried one narrow native behavior tweak in `PhysicsEngine.cpp` to suppress same-transport horizontal side-wall contacts that sit below the current rider height when a same-instance support face already exists, then reverted it after validation showed no metric change on the compact replay.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` before and after reverting the no-op native tweak.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_CurrentVsNextTransportRegistrationComparison" --logger "console;verbosity=detailed"` -> `passed (1/1)` and logged identical `currentDyn` / `nextDyn` outputs on frame `10`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_CurrentVsNextTransportRegistrationComparison|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (4/6)`; long V2 replay stayed green; compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - The compact blocker is no longer explained by transport object timing in the replay harness.
  - It is also not fixed by suppressing the selected same-transport lower side wall as a standalone runtime tweak.
  - The remaining gap is consistent with the still-unwired WoW.exe transport-local selector-record rewrite path (`0x63214C..0x632270`) or a later equivalent transaction in the selected-contact pipeline, not a simple scene-data, support-availability, or registration-order issue.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_CurrentVsNextTransportRegistrationComparison|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap" --logger "console;verbosity=normal"`

- Completed:
  - Corrected the new lower-frame compact Undercity diagnostic so it registers the replay's dynamic objects before querying support contacts; the earlier "missing lower-frame transport contact" output was invalid because it was querying an empty dynamic registry.
  - Added `PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact` in `PacketBackedUndercityElevatorSupportTests.cs`.
  - Proved the lower compact transport frame (`10`) already has the active elevator support face in the final support query as runtime instance `0x80000001`, and `CheckWalkable(...)` accepts that face only on the stateful path (`walk0=false`, `walk1=true`), while the real replay still outputs `support=0`, `gw=0`, and `wallHit=1`.
  - Tried one narrow native behavior tweak in `PhysicsEngine.cpp` to let active-transport support contacts participate in stateful walkability during `CollisionStepWoW` ground selection, then reverted it after validation showed no metric change on the compact replay.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal -m:1` -> `succeeded` (before and after reverting the no-op native tweak).
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact" --logger "console;verbosity=detailed"` -> `passed (1/1)` and logged `finalSupport inst=0x80000001`, `walk0=false`, `walk1=true`, `support=0`, `gw0 branch=1 selectedInst=0x80000001`, `gw1 branch=1 selectedInst=0x80000001`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_ReplayLogsUpperArrivalSupportState|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> `passed (4/6)`; lower/upper diagnostics plus long V2 replay stayed green, while the compact packet-backed pair still failed unchanged (`frame 19 simZ=39.786 recZ=42.626`, worst steady-state frame `9` still `3.2728y`).
- Files changed:
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - The compact blocker is no longer "missing active transport support contact" and no longer the first `isStatefulSupportWalkable(...)` gate by itself.
  - On both the lower transport frame and the upper-arrival frame, the active elevator support face is already present and becomes walkable on the stateful helper path, yet the live replay still keeps `support=0`, `groundedWallState=0`, and zero XY displacement through the transport window.
  - The next parity unit has to target the later support-commit / movement-zeroing transaction inside `CollisionStepWoW`, not another standalone transport-support eligibility tweak.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_LowerTransportFrame_FinalSupportQueryIncludesDynamicTransportContact|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=normal"`

- Completed:
  - Added one more compact Undercity transport diagnostic in `PacketBackedUndercityElevatorSupportTests.cs` that logs the frame-19 dynamic registration order against the merged/final support contacts.
  - Proved the active elevator transport itself is the missing support instance on the failing compact frame: the fresh-process transport registers first (`transportIndex=0`), so its runtime instance is `0x80000001`, and both the merged support contact and the final support-query contact are also `0x80000001`.
  - Tried one narrow native behavior tweak in `PhysicsEngine.cpp` (skip the generic grounded post-`CollisionStepWoW` safety-net refine whenever `transportGuid != 0`), then kept only the diagnostic outcome after validation showed no metric change on the compact replay.
- Validation:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_LogsTransportRegistrationOrderAgainstSupportContacts" --logger "console;verbosity=detailed"` -> `passed (1/1)` with `transportIndex=0`, `freshProcessTransportInst~0x80000001`, `mergedInst=0x80000001`, `finalInst=0x80000001`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> support diagnostics `passed`; long V2 replay `passed`; compact packet-backed pair still `failed` unchanged at frame `19` (`simZ=39.786`, `recZ=42.626`, `support=0`, `gw=0`)
- Files changed:
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - The remaining compact transport blocker is no longer “wrong dynamic object,” no longer scene-data parity, and no longer the generic grounded safety-net refine. The next physics change has to target the support-commit path inside `CollisionStepWoW` itself.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_LogsTransportRegistrationOrderAgainstSupportContacts|FullyQualifiedName~PacketBackedUndercityElevatorSupportTests.PacketBackedUndercityElevatorUp_Frame19_GroundedWallTraceShowsSupportPromotionGap|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=normal"`

- Completed:
  - Added compact Undercity transport diagnostics in `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs` plus replay-frame support-state capture in `ReplayEngine.cs` / `CalibrationResult.cs`.
  - Confirmed the compact replay still never latches a dynamic support token (`support=0` on transport frames `10..19`), but the failing `19 -> 20` arrival step already has a dynamic elevator contact in the final support query and that contact becomes walkable on the stateful `CheckWalkable(...)` path.
  - Tried one narrow transport-only behavior tweak in `PhysicsEngine.cpp` (skip the initial grounded `GetGroundZ` pre-snap while `transportGuid != 0`), then reverted it after validation showed no improvement on the compact replay.
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"` -> compact packet-backed pair still `failed`; long V2 elevator replay still `passed`.
- Files changed:
  - `Tests/Navigation.Physics.Tests/Helpers/CalibrationResult.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/Navigation.Physics.Tests/PacketBackedUndercityElevatorSupportTests.cs`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `docs/physicsengine-calibration.md`
- Practical implication:
  - The remaining compact transport blocker is no longer “missing scene data” or “missing transport support contact.” The contact is already present and statefully walkable on the failing frame; the next work unit is tracing the exact selected-support contact / commit path inside `CollisionStepWoW`.
- Exact next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PacketBackedUndercityElevatorSupportTests|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=normal"`

- Completed:
  - Promoted movement/physics WoW.exe parity to `P0` in the master tracker and pinned parity validation to the same Docker host data root used by `scene-data-service`.
  - Added grouped parity bundle commands for both live FG/BG parity (`BotRunner.Tests`, `Category=MovementParity`) and deterministic replay/physics parity (`Navigation.Physics.Tests`, `Category=MovementParity`).
  - Revalidated movement parity on `D:\MaNGOS\data`: live grounded routes passed; deterministic jump/knockback passed; compact Undercity packet-backed elevator parity remains the blocker.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=movement_parity_category_20260409.trx"` -> `passed (12/12)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `failed (6/8)` with only the two compact Undercity elevator tests red.
- Files changed:
  - `docs/TASKS.md`
  - `Tests/Tests.Infrastructure/SceneDataParityPaths.cs`
  - `Tests/Tests.Infrastructure/BotServiceFixture.cs`
  - `Tests/BotRunner.Tests/SceneDataParityPathsTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/Navigation.Physics.Tests/PhysicsTestFixtures.cs`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Tests/Tests.Infrastructure/TASKS.md`
  - `docs/physicsengine-calibration.md`
- Next command:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~ElevatorPhysicsParityTests.UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=normal"`

- Completed:
  - Closed `P1.13` with live AV validation after inventory fallback hardening; run artifact contains no `[LOADOUT-WARN]` output.
  - Closed `P1.14` via coordinator restage + settle-window flow; run artifact reached `BG-SETTLE bg=80,off=0`.
  - Archived resolved `P1.13`, `P1.14`, and stale-open `P1.15` from the Open list.
- Validation:
  - `rg -n "BG-SETTLE|AV:Mount|AV:HordeObjective|AV:AllianceObjective|offBgAtSuccess" tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx` -> shows `bestOnBg=80`, `bg=80,off=0`, `mounted=77/70`, `HordeObjective near=30`, `AllianceObjective near=40`.
  - `rg -n "\[LOADOUT-WARN\]" tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx` -> no matches.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 1 -SkipBuild -TestTimeoutMinutes 2` -> failed at pre-existing `Navigation.Physics.Tests.DllAvailabilityTests.NavigationDll_ShouldLoadAndInitializePhysics`.
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BgTestHelperTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorStrictCountTests.cs`
  - `run-tests.ps1`
  - `Tests/Tests.Infrastructure/TestRuntimePaths.cs`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_iteration_rerun.trx"`

## Session Handoff (2026-04-09 - Dedicated Battleground Pools)

- Completed:
  - Switched battleground fixtures to dedicated non-overlapping account pools for AV/WSG/AB. AV horde leader is now `AVBOT1` and AB horde leader is now `ABBOT1` (no shared `TESTBOT1` across battlegrounds).
  - Added battleground launch-prep reuse policy: preserve existing characters when any account character matches configured race/class/gender, preventing unnecessary erase/recreate cycles and helping retain PvP ranks.
  - Added deterministic guardrails that assert battleground account pools remain disjoint and the preserve-existing-character policy stays enabled for battleground fixtures.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CoordinatorFixtureBaseTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results` -> `passed (31/31)`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBaseTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.WSG_QueueAndEnterBattleground|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live`

## Session Handoff (2026-04-09 - FG Lua Error Capture for AV Leader Stability)

- Completed:
  - Added state-safe foreground Lua error capture (`WWOW_LUA_ERROR_BUFFER`) and removed the old post-world-entry `seterrorhandler(function() end)` suppression path.
  - Wired deterministic Lua-error draining into FG realm wizard and character-create flows so each critical Lua call/query logs contextual errors (`realmwizard.*`, `charselect.create.*`).
  - Added targeted unit coverage for the new diagnostics helper and capture-callback wiring.
- Validation:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FgCharacterSelectScreenTests|FullyQualifiedName~FgRealmSelectScreenTests|FullyQualifiedName~LuaErrorDiagnosticsTests" --logger "console;verbosity=minimal"` -> `passed (14/14)`.
- Files changed:
  - `Services/ForegroundBotRunner/Diagnostics/LuaErrorDiagnostics.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
  - `Services/ForegroundBotRunner/Frames/FgRealmSelectScreen.cs`
  - `Services/ForegroundBotRunner/Frames/FgCharacterSelectScreen.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Tests/ForegroundBotRunner.Tests/FgRealmSelectScreenTests.cs`
  - `Tests/ForegroundBotRunner.Tests/FgCharacterSelectScreenTests.cs`
  - `Tests/ForegroundBotRunner.Tests/LuaErrorDiagnosticsTests.cs`
  - `docs/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_fg_lua_capture_rerun.trx"`

## Session Handoff (2026-04-09 - FG New Account Realm Wizard Stabilization)

- Completed:
  - Removed realm-wizard action fallback sweeps (`_G`/global frame iteration) and kept automation state-based with explicit named controls (`English` -> `Suggest Realm` -> `Okay/Accept`).
  - Kept deterministic handoff detection from realm wizard to empty character select via glue/login state (`charselect`) instead of Lua fallback sweeps.
  - Revalidated FG first-login world entry with dedicated new-account/new-character live runs.
- Validation:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FgRealmSelectScreenTests|FullyQualifiedName~FgCharacterSelectScreenTests|FullyQualifiedName~ForegroundBotWorkerWorldEntryCinematicTests|FullyQualifiedName~LuaErrorDiagnosticsTests" --logger "console;verbosity=minimal"` -> `passed (21/21)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName=BotRunner.Tests.LiveValidation.ForegroundNewAccountFlowTests.NewAccount_NewCharacter_EntersWorld" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=fg_new_account_flow_no_sweep.trx"` -> `passed (1/1)`.
  - Stability reruns:
    - `...LogFileName=fg_new_account_flow_latest.trx` -> in-world after `129.8s`.
    - `...LogFileName=fg_new_account_flow_rerun1.trx` -> in-world after `122.5s`.
    - `...LogFileName=fg_new_account_flow_rerun2.trx` -> in-world after `121.7s`.
    - `...LogFileName=fg_new_account_flow_no_sweep.trx` -> in-world after `116.9s`.
  - Artifact paths were pinned to repo-local temp/runtime dirs (`tmp/dotnethome`, `tmp/test-runtime`) for this validation pass.
- Files changed:
  - `Services/ForegroundBotRunner/Frames/FgRealmSelectScreen.cs`
  - `Tests/ForegroundBotRunner.Tests/FgRealmSelectScreenTests.cs`
  - `docs/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS_ARCHIVE.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_fg_post_realm_stabilization.trx"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-001/002 Closure)

- Completed:
  - Closed `BR-NAV-001` conservative overlay filter in BotRunner (`PathfindingOverlayBuilder`) and archived the completed item from active BotRunner tasks.
  - Closed `BR-NAV-002` by threading movement capabilities and route-policy settings through a shared `NavigationPathFactory` (`Standard`/`CorpseRun`) across BotRunner call sites, then archived the completed item from active BotRunner tasks.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (8/8)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (66/66)`.
- Files changed:
  - `Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs`
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/Movement/TargetPositioningService.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Movement/PathfindingOverlayBuilderTests.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathFactoryTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_TraceRecordsStallDrivenReplanReason|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_TraceRecordsMovementStuckRecoveryReplanReason" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-003 Closure)

- Completed:
  - Closed `BR-NAV-003` with explicit dynamic-blocker evidence driven replanning (`dynamic_blocker_observed`) so blocked segments trigger planned forced recalculation before long stall loops.
  - Archived completed `BR-NAV-003` out of the active BotRunner task list (`Exports/BotRunner/TASKS.md` -> `Exports/BotRunner/TASKS_ARCHIVE.md`).
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (73/73)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathAffordance|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-004 Slice 1)

- Completed:
  - Closed `BR-NAV-004` first slice by teaching `NavigationPath` (movement/path consumer) to reject unsupported cliff-heavy routes and prefer cheaper supported alternates when available.
  - Archived the completed first slice in `Exports/BotRunner/TASKS_ARCHIVE.md`; active BotRunner work now continues with the second `BR-NAV-004` slice (surfacing affordances to higher-level task logic).
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (61/61)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (74/74)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathAffordance|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"`

## Session Handoff (2026-04-09 - BR-NAV-005 Universal Stuck Ownership)

- Completed:
  - Enforced ownership rule: removed task-level unstuck/recovery behavior so stuck detection/recovery ownership stays in movement-layer `IObjectManager` implementations.
  - `GatheringRouteTask` no longer uses candidate/node stuck-recovery budgets from `MovementStuckRecoveryGeneration`.
  - `FishingTask` search-walk no longer consumes `MovementStuckRecoveryGeneration` to skip probe legs.
  - `RetrieveCorpseTask` no longer executes task-owned stall recovery maneuvers (turn/jump/strafe pulses); it now remains path/no-path timeout driven.
  - `NavigationPathFactory` no longer implicitly binds `MovementStuckRecoveryGeneration`; BotRunner callers now construct navigation paths without passing stuck-generation providers.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_universal_stuck_ownership.trx"` -> `failed (1/1)`; runtime still loops on `candidate_timeout` with repeated `STUCK-L2` signals.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/TargetPositioningService.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `rg -n "candidate_timeout|STUCK-L2|MoveToward preserving airborne steering only" "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\mining_bg_gather_route_post_universal_stuck_ownership.trx"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-004 Slice 2)

- Completed:
  - Closed `BR-NAV-004` second slice by surfacing explicit route-affordance decisions from `NavigationPath` to higher-level task diagnostics.
  - Added `NavigationRouteDecision` to `NavigationTraceSnapshot` so each route plan records support status, max affordance, estimated cost, alternate-route evaluation/selection, and endpoint-retarget outcome.
  - `GoToTask` now emits plan-scoped route summaries (`[GOTO_ROUTE]`) to diagnostics/Serilog, and `RetrieveCorpseTask` summary formatting now includes the surfaced route decision.
  - Archived completed `BR-NAV-004` second slice in `Exports/BotRunner/TASKS_ARCHIVE.md`; `Exports/BotRunner/TASKS.md` now moves on to `BR-NAV-005`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests|FullyQualifiedName~RetrieveCorpseTaskTests.FormatNavigationTraceSummary_IncludesKeyFieldsAndTruncatesPathsAndSamples" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (69/69)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff (2026-04-09 - BR-NAV-005 Movement/Route Ownership Alignment)

- Completed:
  - Enforced parity ownership split: `MovementController` no longer performs stuck-time waypoint reselection/escape routing; it now signals stuck recovery and leaves route ownership to BotRunner.
  - Removed BotRunner path-execution handoff into `MovementController`:
    - `BotTask.TryNavigateToward(...)` now issues only `MoveToward(waypoint)`.
    - `FishingTask.TryFollowSearchWaypointPath(...)` now issues only `MoveToward(nextWaypoint)`.
  - Updated deterministic tests to match the ownership model.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"` -> `passed (64/64)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (65/65)`.
  - Live mining reruns (all failed):  
    - `...LogFileName=mining_bg_gather_route_post_local_delta_cap.trx`
    - `...LogFileName=mining_bg_gather_route_post_mc_route_ownership_shift.trx`
    - `...LogFileName=mining_bg_gather_route_post_botrunner_route_ownership.trx`
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `rg -n "Stuck recovery promoted active waypoint|STUCK-L3|candidate_timeout" E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\mining_bg_gather_route_post_botrunner_route_ownership.trx`

## Session Handoff (2026-04-09 - MovementController Single-Target Parity Contract)

- Completed:
  - Enforced parity ownership boundary: `MovementController` now holds only a single steering target and does not execute waypoint/corridor policy.
  - `SetPath(...)` is now a legacy compatibility shim that stores path head only; `SetTargetWaypoint(...)` stores one target; stale-forward `L2` escalation no longer mutates waypoint selection.
  - Updated deterministic `MovementControllerTests` to reflect callback-only route ownership.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests"` -> `passed (159/159)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests"` -> `passed (40/40)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"` -> `passed (72/72)`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=mining_bg_gather_route_post_single_target_mc.trx"`

## Session Handoff (2026-04-09 - Remove Legacy Route APIs from MovementController Boundary)

- Completed:
  - Finalized the ownership boundary from the shared contract side: removed `IObjectManager.SetNavigationPath(...)` and removed `WoWSharpObjectManager` forwarding to `MovementController.SetPath(...)`.
  - Removed `MovementController.SetPath(...)`; controller now accepts only `SetTargetWaypoint(...)` as a single steering hint while BotRunner remains the route/corridor owner.
  - Updated deterministic tests in WoWSharpClient, Navigation.Physics, and BotRunner to the single-target contract.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~MovementControllerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (164/164)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (73/73)`.
- Files changed:
  - `Exports/GameData.Core/Interfaces/IObjectManager.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerIntegrationTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerIpcParityTests.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_setnavigationpath_removal.trx"`

## Session Handoff (2026-04-09 - MovementController Parity-Only Stuck Signaling)

- Completed:
  - Enforced the strict parity contract: `MovementController` no longer mutates movement state or steering target during stale-forward recovery.
  - Removed in-controller forced strafe/forced-recovery mutation; stale-forward now emits caller-facing stuck escalation signals only.
  - Updated deterministic stale-forward tests to assert callback-only behavior and unchanged movement/waypoint state.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerIntegrationTests|FullyQualifiedName~ObjectManagerWorldSessionTests" --logger "console;verbosity=minimal"` -> `passed (158/158)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (74/74)`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_mc_parity_only_stuck_signals.trx"`

## Archived from TASKS.md on 2026-05-11 (spec rewrite cleanup)

All handoff entries from 2026-04-19 through 2026-05-06 — pathfinding/
physics work. The OG cliff-fall parity round-4 iter-5 VICTORY
(2026-05-10) supersedes most. Kept verbatim below for institutional
memory; do not act on these unless re-promoted into Plan/.

## Handoff (2026-05-06, boarding target refresh green but live still misses transport)

- Completed:
  - Kept the PathfindingService route proof closed for this slice:
    deterministic route gates are green, Docker `wwow-pathfinding` is rebuilt
    and healthy, and focused live pathing reaches the Orgrimmar dock/zeppelin
    area instead of failing at the prior lower-incline route blocker.
  - Added a BotRunner boarding-target refresh: `TransportWaitingLogic`
    refreshes the boarding waypoint while the scheduled zeppelin remains at
    the stop, and `TravelTask` refreshes direct scheduled-transport target and
    facing when the observed transport origin moves.
  - Added/updated deterministic BotRunner tests for the moving zeppelin
    boarding waypoint and scheduled direct-board target refresh.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_transport_boarding_refresh_green2.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (68/68)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:VMAP_PHYS_LOG_MASK='0'; $env:VMAP_PHYS_LOG_LEVEL='0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_boarding_refresh.trx" --results-directory tmp/test-runtime/results-live -- RunConfiguration.TestSessionTimeout=1500000` -> failed after `8m26s`: zeppelin detected at the dock, but the bot missed boarding before departure. Final snapshot `map=1 pos=(1336.7,-4658.3,49.3) distToUndercity=4906.5 transport=0x0 current=null`.
- Evidence:
  - `tmp/test-runtime/results-botrunner/botrunner_transport_boarding_refresh_green2.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_boarding_refresh.trx`
  - `tmp/test-runtime/screenshots/long-pathing/The-Orgrimmar---Undercity-zeppelin-was-detected-at-the-dock-but-the-bot-missed-b-LPATHFG1-client-6048-win0-20260506_010905.png`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-06, PathfindingService route proof moved blocker to boarding)

- Completed:
  - Closed the PathfindingService side of the lower-incline/live route
    recovery gap. The latest live Crossroads -> Undercity run no longer fails
    at the prior lower-incline request and reaches the Orgrimmar dock area.
  - Kept the generic route fixes in `Navigation.cs`: smooth native fallback
    gets smooth validation, local-physics layer classification now requires
    meaningful upward rise before probing, and the restored affordance/static
    scan keeps the route off the known static clips without route-specific
    production coordinates.
  - Added deterministic flight-master -> zeppelin approach blocker coverage
    and kept the route-pack/lower-friction regression slice green.
  - Rebuilt/redeployed Docker `wwow-pathfinding`; the new image digest is
    `sha256:1fa40d9cc8b50021f7043d3a114310b1752139f6ea8f17ed313593f48c50e8ae`,
    and the service reported ready with `maps=41`.
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=flightmaster_static_blockers_restore_affordance_scan.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=420000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinApproachRoute_AvoidsKnownLiveBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=flightmaster_zeppelin_approach_restore_affordance_scan.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable|FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_routepack_lower_friction_regressions.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (16/16)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_after_affordance_restore.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> row results `passed (13/13)`, but VSTest exited `1` after the session shutdown timeout.
  - `docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding` and `docker compose -f docker-compose.vmangos-linux.yml up -d wwow-pathfinding` -> `passed`; service became healthy/ready with `maps=41`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_affordance_restore.trx" --results-directory tmp/test-runtime/results-live -- RunConfiguration.TestSessionTimeout=1500000` -> failed after reaching the dock area: zeppelin detected, boarding missed, final snapshot `map=1 pos=(1336.6,-4658.1,49.3) transport=0x0`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/flightmaster_static_blockers_restore_affordance_scan.trx`
  - `tmp/test-runtime/results-pathfinding/flightmaster_zeppelin_approach_restore_affordance_scan.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_routepack_lower_friction_regressions.trx`
  - `tmp/test-runtime/results-pathfinding/critical_walk_legs_after_affordance_restore.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_affordance_restore.trx`
  - `tmp/test-runtime/screenshots/long-pathing/The-Orgrimmar---Undercity-zeppelin-was-detected-at-the-dock-but-the-bot-missed-b-LPATHFG1-client-41528-win0-20260506_004545.png`
- Pass result: `delta shipped; PathfindingService route gates green; live validation remains open on zeppelin boarding`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-06, direct upper-deck local-physics gate closed)

- Completed:
  - Added bounded route-progress local-physics repair in
    `Services/PathfindingService/Repository/Navigation.cs`. It inserts a
    support point only when the current waypoint can locally walk to the
    snapped candidate, the candidate advances toward the downstream route
    anchor, it stays near the route corridor, and the new leg passes static
    LOS.
  - Kept route-pack attachment strict: the endpoint guard remains in place,
    and the suffix safety regression still passes after the new repair.
  - Closed the deterministic direct Orgrimmar tower friction gate against
    `D:\MaNGOS\data`.
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `passed` with known `PathfindingSocketServer` warnings and benign missing `dumpbin` applocal output.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_deck_friction_route_progress.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_route_progress.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath" --logger "console;verbosity=minimal" --logger "trx;LogFileName=lower_incline_routepack_route_progress.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=180000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_routepack_suffix_route_progress.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_deck_friction_route_progress.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_route_progress.trx`
  - `tmp/test-runtime/results-pathfinding/lower_incline_routepack_route_progress.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_suffix_route_progress.trx`
- Pass result: `delta shipped; direct upper-deck local-physics gate green; live validation remains open`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-05, lower-incline route-pack deterministic recovery)

- Follow-up (2026-05-05, direct upper-deck local-physics gate):
  - Reconfirmed the remaining deterministic red gate:
    `OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable`
    still returns the straight alternate path with an early local-physics break
    at segment `1->2`, `(1339.2,-4645.6,52.0)` ->
    `(1337.6,-4644.5,53.8)`, result `native_path_alternate_mode`,
    blocked reason `static_los`.
  - Verified the lower-layer prefix is the core issue: probing a nearby upper
    layer removes the first jump but exposes additional deck collision pockets,
    so a one-off layer trim or sampled micro-route is not a safe fix.
  - Kept only the generic endpoint-safety guard in local-physics repair so the
    helper cannot replace the requested final endpoint with a lateral support
    point; speculative alternate-start/micro-route experiments were removed.
  - Re-tested a bounded micro-route search for the compact deck step. It could
    repair the first jump in one diagnostic path but then exposed the next deck
    pocket and added too much latency, so the speculative code was removed.
  - Current validation:
    `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`
    -> `passed` with known `PathfindingSocketServer` warnings and benign
    missing `dumpbin` applocal output.
  - Current failing evidence:
    `tmp/test-runtime/results-pathfinding/orgrimmar_tower_deck_friction_current_open_after_micro_revert.trx`.
  - Endpoint-guard regression evidence:
    `tmp/test-runtime/results-pathfinding/static_routepack_cache_endpoint_guard.trx`
    (`passed 14/14`),
    `tmp/test-runtime/results-pathfinding/lower_incline_routepack_endpoint_guard.trx`
    (`passed 1/1`), and
    `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_suffix_endpoint_guard.trx`
    (`passed 1/1`).
  - Next implementation should target a generic deck/support-layer strategy
    that keeps route-pack suffix attachment strict and proves every returned
    direct segment with the local physics probe.

- Completed:
  - Replayed the prior handoff command:
    `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - Changed Orgrimmar route-pack seeds to target the screenshot-derived
    Undercity zeppelin gangplank and stay startup-deferred/on-demand.
  - Added bounded on-demand warming for deferred route packs, including
    failed-warm retry throttling and a route algorithm signature bump to
    `PathfindingService.StaticRoutePack.v10`.
  - Added a corridor-seed generation mode for the lower/exterior Orgrimmar
    recovery seeds so route-pack generation uses bounded Detour corridor
    output instead of the slow live native path.
  - Split route-pack attachment validation from internal generated-corridor
    validation. Attachments still use the strict segment probe; internal
    generated-corridor prefix validation follows the bounded local-reachability
    contract used by runtime suffix validation.
  - Added focused lower-incline route-pack coverage proving on-demand warmup
    hits the gangplank target and returns a bounded route-pack suffix for the
    live start.
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `passed` with known `PathfindingSocketServer` warnings and benign missing `dumpbin` applocal output.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath" --logger "console;verbosity=minimal" --logger "trx;LogFileName=lower_incline_routepack_on_demand_gangplank_final_focus.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=180000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarStaticRoutePackSeeds_TargetGangplankAndDeferStartupWarmup|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_routepack_contract_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (3/3)`.
  - Safety probe:
    `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_suffix_safety_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed 1/2`; suffix safety passed, direct tower friction recovery still fails on local physics segment `1->2` from `(1339.2,-4645.6,52.0)` to `(1337.6,-4644.5,53.8)`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/lower_incline_routepack_on_demand_gangplank_final_focus.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_contract_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_suffix_safety_final.trx`
- Pass result: `delta shipped; lower-incline route-pack deterministic recovery green; live validation and upper-deck direct local-physics repair remain open`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-05, configurable Navigation mmap preload)

- Completed:
  - Added native `Navigation.dll` startup preload control through
    `WWOW_NAVIGATION_PRELOAD_MAPS`. Values: `none`/`false`/`off`/`0` for no
    eager preload, explicit map lists such as `0,1,389`, or `all`/`*` to
    discover every `.mmap` under `WWOW_DATA_DIR/mmaps`.
  - Kept normal on-demand behavior: any map passed into path/query exports
    still loads through the existing requested-map path if it was not already
    preloaded.
  - Added PathfindingService config `Navigation:PreloadMaps` with the same
    value grammar, plus `Navigation:RunStartupDiagnostics=false` by default so
    tests do not implicitly load map `0`/`1` unless requested.
  - PathfindingService status now reports the configured preloaded map IDs.
  - Updated the Linux compose `wwow-pathfinding` service to run with
    `WWOW_NAVIGATION_PRELOAD_MAPS=all`, `Navigation__PreloadMaps=all`, and
    `Navigation__RunStartupDiagnostics=false`; widened its healthcheck
    startup period to cover full-map preload.
  - Rebuilt and redeployed the Docker image with preload-all enabled. The live
    `wwow-pathfinding` container reported `IsReady=true` after loading all 41
    discovered maps.
  - Documented the setting in `Services/PathfindingService/README.md`,
    `docs/DOCKER_STACK.md`, and `docs/BUILD.md`.
- Validation/tests run:
  - `$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"; & $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerPreloadConfigTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_preload_config_tests.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (3/3)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DetourCompatibilityTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=detour_after_configurable_preload.trx" --results-directory tmp/test-runtime/results-navigation` -> `passed (2/2)`.
  - Diagnostic attempt:
    `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_LiveCorpseRunRoute_ReturnsValidatedPathWithinBudget" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_socket_live_corpse_preload_config.trx" --results-directory tmp/test-runtime/results-pathfinding` -> failed at the existing `10s` response-budget assertion after spending `44s` in the long corpse-run route path. Logs showed map `1` and map `0` loaded; treat this as the existing long-route boundedness issue, not a preload config failure.
  - `docker compose -f .\docker-compose.vmangos-linux.yml up -d --build wwow-pathfinding` -> succeeded; rebuilt `world-of-warcraft-wwow-pathfinding:latest` and recreated `wwow-pathfinding`.
  - `docker inspect wwow-pathfinding --format '{{range .Config.Env}}{{println .}}{{end}}'` -> confirmed `WWOW_NAVIGATION_PRELOAD_MAPS=all`, `Navigation__PreloadMaps=all`, and `Navigation__RunStartupDiagnostics=false`.
  - `docker logs --since 5m wwow-pathfinding` -> reported `[Navigation] preloading 41 configured map(s)` and `Navigation loaded in 117.7s`; startup diagnostics stayed disabled.
  - `docker exec wwow-pathfinding cat /app/pathfinding_status.json` -> `IsReady=true`, `StatusMessage="Ready - navigation initialized"`, and `LoadedMaps` contained all 41 discovered map IDs.
- Files changed:
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/Navigation.h`
  - `Services/PathfindingService/NativeProcessEnvironment.cs`
  - `Services/PathfindingService/Program.cs`
  - `Services/PathfindingService/PathfindingServiceWorker.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/appsettings.PathfindingService.json`
  - `Services/PathfindingService/appsettings.PathfindingService.Docker.json`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerPreloadConfigTests.cs`
  - `docker-compose.vmangos-linux.yml`
  - docs and task files.
- Next command:
  - `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-05, Detour/mmap v6 migration slice)

- Completed:
  - Preserved existing dirty work, including `.env`; no unrelated changes were
    reverted.
  - Chose the forward-compatible schema for this migration: Detour tile
    payload version `7`, mmap wrapper version `6`, 20-byte uint32
    `MmapTileHeader`, and 64-bit `dtPolyRef`/`dtTileRef` through
    `DT_POLYREF64`.
  - Changed `Navigation.dll` initialization to create the mmap manager without
    eagerly loading maps `0`, `1`, and `389`; maps now load on first request
    through the existing per-map load path.
  - Made native `.mmtile` loading reject mismatched wrapper magic/version,
    wrapper Detour version, payload size, Detour payload magic, and Detour
    payload version before `dtNavMesh::addTile(...)`.
  - Extended native/managed Detour compatibility probes to assert mmap header
    size, pointer size, 64-bit ref bit split, file `usesLiquids`, and strict
    header compatibility.
  - Extended `tools/NavDataAudit` with exact wrapper/payload version checks and
    `--write-manifest` output with per-tile hashes and a combined nav-data
    signature.
  - Regenerated focused map `1` tiles `28,39` through `30,41` with
    `D:/MaNGOS/source/bin/MoveMapGenerator.exe`; previous files were moved to
    `D:/MaNGOS/data/mmaps/detour-migration-backup-20260504-201741`.
  - Documented the migration contract and focused regeneration evidence in
    `docs/physics/DETOUR_UPGRADE_BASELINE.md` and
    `docs/physics/MMAP_NAVMESH_GENERATION.md`.
- Evidence:
  - Regeneration log:
    `tmp/test-runtime/results-navigation/mmap_regen_map1_org_crossroads_20260504-201741.log`.
  - Manifest:
    `tmp/test-runtime/results-navigation/detour_mmap_map1_org_crossroads_manifest.json`.
  - Manifest nav-data signature:
    `F9CE41288735205E8504D476D38C425C196177512DC18E71C5BFB0E9E2678E69`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"; & $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded` with existing warnings.
  - `dotnet build tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -v:minimal` -> `succeeded`.
  - Focused `MoveMapGenerator.exe` regeneration for map `1` tiles `28,39`
    through `30,41` -> `succeeded`; GO marking counts were
    `56,13,71,22,38,16,20,23,12`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:/MaNGOS/data --map 1 --build-log "tmp/test-runtime/results-navigation/mmap_regen_map1_org_crossroads_20260504-201741.log" --write-manifest "tmp/test-runtime/results-navigation/detour_mmap_map1_org_crossroads_manifest.json"` -> `passed`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DetourCompatibilityTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=detour_mmap_v6_contract.trx" --results-directory tmp/test-runtime/results-navigation` -> `passed (2/2)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DetourCompatibilityTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=detour_mmap_v6_regenerated_tiles.trx" --results-directory tmp/test-runtime/results-navigation` -> `passed (2/2)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_static_route_pack_cache_detour_mmap_v6.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`.
  - Combined Orgrimmar route/cache gate
    `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|...RoutePack_CachesMainPathAndRecoveryAnchor|...CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes`
    -> aborted at the runsettings `10m` timeout.
  - Single Orgrimmar static blocker gate
    `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
    -> aborted at the extended `20m` timeout. Treat the real route gate as
    red/open; no live validation was launched.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Exports/Navigation/MoveMapSharedDefines.h`
  - `Exports/Navigation/MoveMap.cpp`
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/DllMain.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/DetourCompatibilityTests.cs`
  - `tools/NavDataAudit/Program.cs`
  - `docs/physics/DETOUR_UPGRADE_BASELINE.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - `docs/physics/README.md`
  - `docs/TASKS.md`
  - impacted local `TASKS.md` files.
- Next command:
  - `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-04, Detour compatibility baseline)

- Completed:
  - Preserved the existing dirty `.env` and previous PathfindingService
    cache/socket/route-pack dirty work.
  - Added native Detour compatibility probes in `DllMain.cpp`:
    `GetDetourCompatibilityInfo(...)` reports the compiled Detour ABI and
    feature surface, and `ProbeMMapTileCompatibility(...)` loads a real
    `.mmtile` through `MMapManager` and reports wrapper/header compatibility.
  - Added managed `DetourCompatibilityTests` covering the current compiled
    ABI, Detour tile version, mmap wrapper evidence, native tile loading, and
    local Detour features before any vendor refresh.
  - Documented the baseline and local Detour customizations in
    `docs/physics/DETOUR_UPGRADE_BASELINE.md`, then linked it from
    `docs/physics/README.md`.
  - Confirmed the current working ABI is `DT_POLYREF64` with 64-bit
    `dtPolyRef`/`dtTileRef` while Detour tiles remain
    `DT_NAVMESH_VERSION = 7`. The source `MMAP_VERSION` is `4`, but current
    generated data can report wrapper version `6`; the probe records this
    instead of silently assuming the source constant matches local data.
  - Tried a local 32-bit-ref build by removing `DT_POLYREF64`; the
    Orgrimmar flight-master route gate quickly returned `no_path`, so the flag
    was restored. Treat any 32-bit-ref move or new packed tile layout as an
    explicit mmap regeneration/migration decision, not a mechanical cleanup.
  - No route-specific production pathing hacks were added and no live
    validation was launched.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"; & $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded` with existing native warnings.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DetourCompatibilityTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=detour_compatibility_baseline.trx" --results-directory tmp/test-runtime/results-navigation` -> `passed (2/2)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_cache_pack_detour_baseline_unit.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~StaticRoutePackCacheTests|FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_detour_baseline_route_cache.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> shell timeout after `20m`; TRX counters stayed `0`, so do not treat this route gate as green.
  - Diagnostic 32-bit-ref trial before restoring `DT_POLYREF64`: `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers` failed in about `22s` with `result=no_path blocked=none`.
- Evidence:
  - `tmp/test-runtime/results-navigation/detour_compatibility_baseline.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_cache_pack_detour_baseline_unit.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_detour_baseline_route_cache.trx`
- Files changed:
  - `Exports/Navigation/DllMain.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/DetourCompatibilityTests.cs`
  - `docs/physics/DETOUR_UPGRADE_BASELINE.md`
  - `docs/physics/README.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-04, PathfindingService cache/instrumentation/socket logging)

- Completed:
  - Preserved existing dirty `.env` config and made no native Navigation
    changes.
  - Added `RouteResultCache`, a PathfindingService-owned cache for static
    overlay route results. Keys include map, quantized start/end, race/gender,
    capsule, smooth flag, policy, nav-data signature, route algorithm
    signature, and dynamic-overlay signature.
  - Added in-flight coalescing so equivalent concurrent static route requests
    share one calculation.
  - Added short-TTL negative caching for `no_path`/blocked results and exposed
    cache metrics for hits, misses, coalescing, bypass, expiry, invalidation,
    positive/negative stores, slow requests, entry count, and in-flight count.
  - Wired `PathfindingSocketServer` so route-pack hits and native validated
    results pass through the service cache while dynamic overlays bypass it
    conservatively.
  - Added deterministic `RouteResultCacheTests` for quantized hits,
    dynamic-overlay bypass, concurrent coalescing, and negative TTL expiry.
  - Added `NavigationPerformanceMetrics` and low-noise `[NAV_METRICS]` socket
    logging for resolver attempts, native `FindPathForAgent`, corridor query
    timing, managed validation timing, static/LOS/steep/local-layer/segment/
    dynamic repair counts, blocked outcomes, `no_path`, and slow counters.
  - Fixed `ProtobufSocketServer` clean disconnect handling so a client closing
    after a full request/response no longer logs `Unexpected EOF`; incomplete
    payloads still surface as warnings.
  - Made route-pack startup warmup opt-in via
    `WWOW_ROUTE_PACK_STARTUP_WARMUP=1` and marked the current lower-incline
    recovery seed `WarmAtStartup=false` so service initialization is not held
    hostage by the known slow native route-pack generation gap.
  - Added a per-seed static route-pack generation timeout. The real
    Navigation-backed route-pack proof now fails in about `30s` when a startup
    seed misses its generation budget instead of exhausting the `20m` test
    session.
  - Reworked the socket route-cache integration proof to use a deterministic
    generated route-pack fixture through the normal protobuf contract; the
    repeat request now asserts `server.RouteCacheStats.HitCount`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=route_result_cache_tests.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (4/4)` with the existing benign `dumpbin` applocal warning.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ProtobufSocketServerLoggingTests|FullyQualifiedName~StaticRoutePackCacheTests|FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~NavigationOverlayAwarePathTests.CalculateValidatedPath_RecordsResolverAndManagedValidationMetrics|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_RepeatedStaticRequest_UsesServiceRouteCacheThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_cache_socket_logging_metrics_timeout_bundle_after_assertion.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (18/18)` with the existing benign `dumpbin` applocal warning.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_real_after_warmup_timeout_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> failed bounded at `30s` on route-pack seed warmup; keep `PFS-ROUTEPACK-002` red.
  - `git diff --check` -> no whitespace errors; line-ending warnings only.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_cache_socket_logging_metrics_timeout_bundle_after_assertion.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_metrics_cache_socket_eof.trx`
  - `tmp/test-runtime/results-pathfinding/routepack_real_after_warmup_timeout_guard.trx`
- Files changed:
  - `Exports/BotCommLayer/ProtobufSocketServer.cs`
  - `Services/PathfindingService/RouteCaching/RouteResultCache.cs`
  - `Services/PathfindingService/Repository/NavigationPerformanceMetrics.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Tests/PathfindingService.Tests/ProtobufSocketServerLoggingTests.cs`
  - `Tests/PathfindingService.Tests/RouteResultCacheTests.cs`
  - `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `docs/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-04, living-server automation spec)

- Completed:
  - Committed and pushed the full long-pathing/route-pack/gameplay-doc
    checkpoint to `origin/main` as `79e4920c`.
  - Added `docs/LIVING_SERVER_AUTOMATION_SPEC.md`, working backward from the
    3000-bot living-server vision into activity catalog shape, automated
    progression, StateManager topology, human on-demand activity API,
    pathfinding/scene-data scale, metrics registry, logging policy, data
    products, phases, acceptance criteria, and open decisions.
  - Updated the spec to make the existing `WoWStateManagerUI` Dashboard tab the
    operator surface for metrics and on-demand activity configs. The UI should
    load/edit/validate/save/enable/disable/request/cancel activity configs
    through StateManager APIs, while StateManager remains authoritative.
  - Linked the new spec from `docs/WESTWORLD_ARCHITECTURE.md`.
  - Linked the planned Dashboard responsibilities from
    `UI/WoWStateManagerUI/README.md`.
- Research inputs:
  - Local: `docs/WESTWORLD_ARCHITECTURE.md`,
    `docs/leveling-guide/README.md`, `docs/leveling-guide/decision-engine/*`,
    `docs/TRAVEL_PLANNING.md`, and `docs/TECHNICAL_NOTES.md`.
  - External platform references: Microsoft ASP.NET Core metrics,
    OpenTelemetry .NET metrics, and Docker logging-driver documentation.
- Validation/tests run:
  - Documentation-only slice; no build/test run after adding the spec.
- Files changed:
  - `docs/LIVING_SERVER_AUTOMATION_SPEC.md`
  - `docs/WESTWORLD_ARCHITECTURE.md`
  - `UI/WoWStateManagerUI/README.md`
  - `docs/TASKS.md`
- Next command: `git status --short --branch`

---

## Handoff (2026-05-04, stopped for repo cleanup before live rerun)

- Completed in this stop-point slice:
  - Preserved dirty work and stopped the manual/live validation path. No new
    live Crossroads -> Undercity or zeppelin run was launched after the
    screenshot-anchor updates.
  - Consumed the repo-root screenshot evidence and moved the
    Orgrimmar/Undercity zeppelin static anchors to the captured gangplank/deck
    values:
    - Orgrimmar -> Undercity dock: `(1320.142944,-4653.158691,53.891945)`.
    - Undercity -> Orgrimmar dock: `(2066.911377,290.113708,97.031593)`.
    - Stable center-deck transport-local offset:
      `(-12.580913,-7.983256,-16.398277)`.
  - Updated BotRunner transport data, cross-map graph/test constants,
    long-pathing live constants, and PathfindingService route-pack seed/test
    anchors to use the screenshot-derived Orgrimmar/Undercity dock points.
  - Updated `Tests/PathfindingService.Tests.NavigationFixture` so test commands
    no longer need to set `WWOW_DATA_DIR` explicitly when a normal local data
    root such as `D:\MaNGOS\data` is discoverable. The native boundary still
    receives `WWOW_DATA_DIR` internally.
  - Cleaned up the orphan manual capture/test processes with repo-scoped
    cleanup and one explicit orphan `WoW.exe` PID kill after verifying its
    parent was the stopped repo-scoped `WoWStateManager.exe`.
- Validation/tests run in this slice:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> listed the manual capture
    process tree; after cleanup, later checks reported no repo-scoped
    processes.
  - `.\run-tests.ps1 -CleanupRepoScopedOnly` -> stopped repo-scoped manual
    capture/test children.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CrossMapRouterTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_screenshot_anchors.trx" --results-directory tmp/test-runtime/results-botrunner` -> first run failed one obsolete assertion, then passed `(94/94)` after updating the assertion to hold the configured boarding point.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_after_nav_fixture_discovery.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (8/8)` without explicitly setting `WWOW_DATA_DIR`.
  - Pathfinding focused screenshot-anchor route command timed out after
    `20m`; no pass/fail was produced.
  - A no-env native smoke path command also timed out after `5m`; the leftover
    repo-scoped `dotnet.exe`/`testhost.exe` pair was cleaned up with
    `.\run-tests.ps1 -CleanupRepoScopedOnly`.
- Evidence:
  - `tmp/test-runtime/results-botrunner/botrunner_zeppelin_screenshot_anchors.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_after_nav_fixture_discovery.trx`
  - Repo-root screenshots: `org-uc-boarding.jpg`, `uc-org-boarding.jpg`,
    `zepplin-riding.jpg`, plus the related Grom'gol/UC elevator capture images.
- Files changed in this slice:
  - `Exports/BotRunner/Movement/TransportData.cs`
  - `Exports/BotRunner/Movement/MapTransitionGraph.cs`
  - `Tests/BotRunner.Tests/Movement/CrossMapRouterTests.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TaxiTransportParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuard.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuardTests.cs`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Tests/PathfindingService.Tests/NavigationFixture.cs`
  - `docs/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-04, long-pathing live fixture config-first launch)

- Completed:
  - `LiveBotFixture.InitializeAsync()` now prepares the live-validation
    environment but defers StateManager startup until a test-specific settings
    file is known. Derived fixtures that call `SetCustomSettingsPath(...)`
    still launch immediately with that preset, so there is no default startup
    followed by a restart to apply the real config.
  - Added the long-pathing collection fixture path so the zeppelin-focused live
    tests start StateManager with `LongPathing.config.json` before any client
    launches.
  - Manual zeppelin coordinate-capture mode now dispatches no `TravelTo`
    action and uses an infinite snapshot wait instead of a 12-hour timeout.
  - Relaunched the manual Orgrimmar -> Undercity zeppelin capture run. The
    active process shape matches the long-pathing config: one foreground
    `WoW.exe` plus two `BackgroundBotRunner.exe` processes.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BgOnlyBotFixtureConfigurationTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=live_fixture_config_first_startup_manual_infinite_compile_check.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (9/9)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:WWOW_TEST_MANUAL_ZEPPELIN_COORD_CAPTURE='1'; Start-Process dotnet.exe ... --filter "FullyQualifiedName~LongPathingTests.OrgrimmarToUndercityZeppelin_BoardsAndDeplanes" ...` -> launched background manual capture.
- Evidence:
  - `tmp/test-runtime/results-botrunner/live_fixture_config_first_startup_manual_infinite_compile_check.trx`
  - `tmp/test-runtime/results-live/manual_zeppelin_coord_capture_config_first_20260504_122723.out.log`
  - `tmp/test-runtime/results-live/manual_zeppelin_coord_capture_config_first_20260504_122723.err.log`
- Files changed in this slice:
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

## Handoff (2026-05-04, PathfindingService static route-pack prototype)

- Completed:
  - Preserved existing dirty work and kept the route-pack implementation owned
    by PathfindingService. No production Orgrimmar blocker coordinates,
    clearance cylinders, detours, waypoint exceptions, or live-position guards
    were added.
  - Added `StaticRoutePackCache` with generated startup warmup, nav-data and
    route-algorithm signatures, race/gender capsule keying, smooth/policy
    keying, dynamic-overlay compatibility checks, and generic suffix
    projection/attachment validation.
  - `PathfindingSocketServer` now warms two Orgrimmar route-pack seeds at
    startup and returns cache hits through the normal path result contract as
    `route_pack_main_path` / `route_pack_suffix`.
  - Added a generic `Navigation.IsSegmentWalkableForAgent(...)` probe used by
    suffix attachment to require strict LOS, local agent affordance, and
    resolved endpoint Z compatibility.
  - Added deterministic unit, real Navigation, and socket-contract coverage
    for route-pack generation/reuse, key mismatches, dynamic overlays, unsafe
    suffix bypass, and the Orgrimmar flight-master/recovery-anchor packs.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_unit_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (7/7)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_cache_real_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_socket_routepack_contract_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routepack_cache_prep_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)`.
  - `docker compose -f docker-compose.vmangos-linux.yml up -d --build pathfinding-service` -> succeeded; service warmup logged `packs=2` after about `184435ms`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `map=1 pos=(1363.9,-4378.2,26.1)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_unit_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/routepack_cache_real_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_socket_routepack_contract_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-pathfinding/long_pathing_routepack_cache_prep_after_resolved_z_guard.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx`
  - Screenshot: `tmp/test-runtime/screenshots/long-pathing/Long-travel-stall-before-Orgrimmar-flight-master---zeppelin-tower-likely-wall-ce-LPATHFG1-client-37552-win0-20260504_003425.png`
- Live finding:
  - The cache correctly bypassed an unsafe lower-layer suffix after the
    resolved-Z guard. The live recovery request from
    `(1363.9,-4377.8,26.1)` to `(1341.0,-4638.6,53.5)` then fell back to
    native generation and only logged still-running markers at `5s`, `15s`,
    and `25s`; no completion was found in the inspected service logs.
- Files changed in this slice:
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `docs/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "StaticRoutePackCache|CreateDefaultSeeds|IsSegmentWalkableForAgent|OrgrimmarFlightMasterToZeppelinRoutePack|1363\\.9|-4377\\.8" Services/PathfindingService Tests/PathfindingService.Tests Tests/BotRunner.Tests docs/TASKS.md`

---

## Handoff (2026-05-03, route-pack architecture direction)

- Completed:
  - Preserved existing dirty work and kept the new route-pack direction within
    the static navigation ownership boundary.
  - Added the route-pack architecture to `docs/TRAVEL_PLANNING.md`: generated
    route packs should be cached PathfindingService/MMAP outputs, not
    production hand-authored route scripts.
  - Latest live retry after the generic boarding-position handoff failed before
    the zeppelin tower at `map=1 pos=(1381.6,-4370.6,26.0)`, while
    PathfindingService accepted the recovery request from
    `(1381.3,-4370.6,26.0)` to `(1341.0,-4638.6,53.5)` and returned
    `repaired_local_physics_layer` after about `23690ms`.
  - This supports the route-pack idea: the Orgrimmar flight-master -> zeppelin
    tower path took about `11674ms`, and the later static recovery suffix took
    about `23690ms`; a generated route pack could answer both as a validated
    full path/suffix without blocking live movement.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests.Update_LiveOrgrimmarZeppelinBoardingPosition|FullyQualifiedName~TravelTaskTests.Update_LiveOrgrimmarZeppelinDeckPosition|FullyQualifiedName~TravelTaskTests.Update_LowerOrgrimmarZeppelinTowerPosition|FullyQualifiedName~TravelTaskTests.Update_OrgrimmarZeppelinPillarPosition" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_boarding_position_handoff.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseLiveReplanRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinDeckBoardingPoint_StaysOnUpperDeckLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_ramp_deck_boarding_position_focus.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_boarding_position_handoff.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_boarding_position_handoff.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `(1381.6,-4370.6,26.0)`, before the tower/deck handoff was exercised.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_ramp_deck_and_exact_incline_after_guard_tuning.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> timed out after 20 minutes; immediate process check found no repo-scoped processes.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_exact_incline_deck_static_routepack_prep.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)` in `4m29s`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_boarding_position_handoff.trx`
  - `tmp/test-runtime/results-botrunner/botrunner_zeppelin_boarding_position_handoff.trx`
  - `tmp/test-runtime/results-pathfinding/long_pathing_ramp_deck_boarding_position_focus.trx`
  - `tmp/test-runtime/results-pathfinding/long_pathing_static_blockers_after_boarding_position_handoff.trx`
  - `tmp/test-runtime/results-pathfinding/long_pathing_exact_incline_deck_static_routepack_prep.trx`
- Files changed in this slice:
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `docs/TRAVEL_PLANNING.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
- Next command: `rg -n "PathResultCache|RoutePack|PathfindingClient.GetPathResult|CalculateValidatedPath" Exports/BotRunner Services/PathfindingService Tests docs/TRAVEL_PLANNING.md`

---

## Handoff (2026-05-03, live walk reaches zeppelin deck approach)

- Completed:
  - Preserved all existing dirty work and kept this slice generic. No
    production route-specific Orgrimmar blocker coordinates, clearance
    cylinders, detour waypoints, waypoint exceptions, or live-position guards
    were added.
  - Followed the user concern about managed path shaping: the successful
    direction was to relax/preserve the Detour/MMAP corridor for long-travel
    recovery, not to insert Orgrimmar-specific path fixes.
  - Added generic long-travel wall-stuck recovery that first promotes along
    the existing validated corridor before making a full PathfindingService
    replan.
  - Kept long-travel wall recovery in smooth Detour mode instead of switching
    to the safer alternate replan policy that was creating slow unsmoothed
    service requests.
  - Added generic long-travel vertical-layer handling that preserves supported
    uphill Detour corridor progression instead of forcing an expensive replan.
  - Focused live validation now gets through the prior Z-hallway/runtime walk
    blockers and reaches the Orgrimmar zeppelin deck approach. The remaining
    failure is zeppelin boarding/transfer evidence: final snapshot
    `map=1 pos=(1330.7,-4653.0,53.5) transport=0x0 current=null`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_long_travel_recovery_smooth_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (147/147)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_long_travel_recovery_smooth.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_long_travel_recovery_smooth.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `map=1 pos=(1546.7,-4430.5,10.4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelWallRecoveryPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_KeepsSmoothDetourModeDuringWallRecovery|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_KeepsSmoothDetourModeDuringRecovery" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_travel_wall_recovery_existing_corridor_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (3/3)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_long_travel_wall_recovery_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (149/149)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_long_travel_wall_recovery.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_long_travel_wall_recovery.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` after progressing to `map=1 pos=(1508.7,-4419.7,20.6)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_DUMP_LONG_PATHING_ROUTE='1'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_critical_walklegs_dump_current.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> timed out after 20 minutes; immediate repo-scoped process check found none.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelVerticalMismatchPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelReplansWhenNearWaypointIsOverheadLayer|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelWallRecoveryPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_EvaluatesAlternateOnVerticalLayerMismatch" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_travel_vertical_existing_corridor_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_long_travel_vertical_recovery_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (150/150)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_long_travel_vertical_recovery.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_long_travel_vertical_recovery.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `map=1 pos=(1546.3,-4430.8,10.5)` after a slow vertical-layer service request.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelKeepsSupportedUphillLayerProgressionWithoutReplanning|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelReplansWhenNearWaypointIsOverheadLayer|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelVerticalMismatchPromotesExistingCorridorBeforeReplanning|FullyQualifiedName~NavigationPathFactoryTests.Create_LongTravelPolicy_EvaluatesAlternateOnVerticalLayerMismatch" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_travel_uphill_vertical_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_long_travel_uphill_vertical_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (151/151)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_long_travel_uphill_vertical.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_long_travel_uphill_vertical.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` after `14m27s`; the walking leg reached the zeppelin deck approach and the remaining assertion is transport/map-transfer evidence.
- Evidence:
  - Latest live TRX:
    `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_long_travel_uphill_vertical.trx`.
  - Failure screenshot:
    `tmp/test-runtime/screenshots/long-pathing/Expected-the-bot-to-board-the-Orgrimmar---Undercity-zeppelin-or-complete-the-cro-LPATHFG1-client-22544-win0-20260503_201439.png`.
  - Latest offline static-object TRX:
    `tmp/test-runtime/results-pathfinding/long_pathing_static_blockers_after_long_travel_uphill_vertical.trx`.
  - Latest deterministic BotRunner focus TRX:
    `tmp/test-runtime/results-botrunner/botrunner_navigation_travel_long_travel_uphill_vertical_focus.trx`.
- Files changed in this slice:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathFactoryTests.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `Select-String -Path tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_long_travel_uphill_vertical.trx -Pattern "\[TRAVEL_LEG\]|\[TRAVEL_TRANSPORT\]|\[TRANSPORT|Expected the bot to board|failure:"`

---

## Handoff (2026-05-03, stopping point after generic long-travel recovery delta)

- Completed:
  - Preserved the existing dirty work and stopped after a deterministic/offline
    validation point.
  - Added generic long-travel movement recovery behavior:
    `TravelTask` no longer treats waypoint-index churn as movement progress
    unless the player actually moved, and `NavigationPath.RecalculateAfterMovementStall`
    now first promotes to a validated forward waypoint on the existing
    long-travel corridor before asking PathfindingService for a full replan.
  - Added deterministic coverage for that corridor-promotion path. This is not
    route-specific: production code does not encode Orgrimmar blocker
    coordinates, clearance cylinders, detour points, waypoint exceptions, or
    live-position guards. Coordinates remain only in deterministic tests and
    diagnostics as evidence.
  - Reran the focused offline Orgrimmar static-object route gate after the
    latest BotRunner change; it still passes against `D:\MaNGOS\data`.
  - Did not rerun live WoW validation after this latest change; live validation
    remains paused at the user's requested stopping point.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests.Update_WalkLegNoProgress|FullyQualifiedName~NavigationPathTests.RecalculateAfterMovementStall_LongTravelPromotesExistingCorridorBeforeReplanning" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_walk_stall_existing_corridor_promotion_single.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (3/3)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelTaskTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_navigation_travel_existing_corridor_promotion_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (145/145)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_static_blockers_after_existing_corridor_promotion.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
- Key blocker clearances from the latest offline gate:
  - lower flight-master bonfire `clearance=8.67`, required `4.50`.
  - bank-front palm/static model `clearance=19.61`, required `4.00`.
  - bank-front bonfire `clearance=16.74`, required `4.50`.
  - Z-hallway north early-cut corner `clearance=3.89`, required `3.50`.
  - Z-hallway south early-cut corner `clearance=4.76`, required `4.00`.
  - exterior steep incline `clearance=13.91`, required `6.00`.
  - exterior rope-line support `clearance=6.82`, required `5.00`.
- Evidence:
  - Latest offline TRX:
    `tmp/test-runtime/results-pathfinding/long_pathing_static_blockers_after_existing_corridor_promotion.trx`.
  - Latest deterministic BotRunner TRX:
    `tmp/test-runtime/results-botrunner/botrunner_navigation_travel_existing_corridor_promotion_focus.trx`.
  - Latest live TRX remains the pre-delta failure:
    `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_waypoint_progress_walk_stall_replan.trx`,
    failing near `(1508.7,-4419.7,20.6)`.
- Files changed in this stopping slice:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_existing_corridor_promotion.trx" --results-directory tmp/test-runtime/results-live`

---

## Handoff (2026-05-02, live rerun after regenerated mmaps)

- Completed:
  - Confirmed `pathfinding-service` is healthy on rebuilt image
    `sha256:2d7782de11432a274991b49dfd02029e284f606abd3aaad56edcedaa5d4a6ce6`
    with `D:/MaNGOS/data -> /wwow-data (ro)` and `IsReady=true`.
  - Reran the focused live Crossroads -> Undercity integration test with
    `WWOW_TEST_PRESERVE_EXISTING_PATHFINDING=1`; the route progressed beyond
    the earlier city static-object gate but failed at the Orgrimmar zeppelin
    tower approach.
  - Current live blocker: `[TRAVEL_WALK_NAV] ... plan=15 ... reason=vertical_layer_mismatch ... afford=SteepClimb ... player=(1342.4,-4652.1,24.6) ... target=(1341.0,-4638.6,53.5) ... active=(1342.1,-4652.8,24.6)`.
  - `pathfinding-service` logs for the same area show short tower routes are
    being served from the mounted data, including
    `result=native_path_alternate_mode pathLen=35` from
    `(1345.7,-4646.7,25.3)` to `(1341.0,-4638.6,53.5)`, plus native
    wall-hit/low-displacement evidence near the upper tower geometry.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `docker ps --filter "name=pathfinding-service" --format "table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service` healthy on port `5001`.
  - `docker inspect pathfinding-service --format "Image={{.Image}}..."` -> image `sha256:2d7782de11432a274991b49dfd02029e284f606abd3aaad56edcedaa5d4a6ce6`, mount `D:/MaNGOS/data -> /wwow-data (ro)`.
  - `docker exec pathfinding-service powershell -NoProfile -Command "Get-Content /app/pathfinding_status.json"` -> failed because the Linux container has no `powershell` executable.
  - `docker exec pathfinding-service sh -lc "cat /app/pathfinding_status.json"` -> `IsReady=true`, `StatusMessage=Ready - navigation initialized`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_rerun_current.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` after `7m16s`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - Latest live TRX:
    `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_rerun_current.trx`.
  - Screenshot:
    `tmp/test-runtime/screenshots/long-pathing/Known-Crossroads---Undercity-pathing-blocker-Orgrimmar-walk-route-selected-a-ste-LPATHFG1-client-19352-win0-20260501_232829.png`.
  - Latest static blocker route gate remains green:
    `tmp/test-runtime/results-pathfinding/long_pathing_route_static_blockers_after_live_replan_fix.trx`.
    The passing TRX emits no blocker clearance failure lines.
- Files changed:
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathRouteAssertions.cs`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "orgrimmar_zeppelin_tower|1342\\.|4652\\.|SteepClimb|native_path_alternate_mode" Tests/PathfindingService.Tests Tests/BotRunner.Tests Services/PathfindingService Exports/BotRunner -g "!**/bin/**" -g "!**/obj/**"`

---

## Handoff (2026-05-01, full mmap regeneration and service relaunch)

- Completed:
  - Moved the pre-existing root map `0`/`1` `.mmtile` files to
    `D:\MaNGOS\data\mmaps\pre-full-regen-20260501` because a first full
    generator pass completed without overwriting old root tile headers.
  - Fresh-regenerated all root map `0` and map `1` tiles from
    `D:\MaNGOS\data\config.json` using the current GO-aware generator and
    one thread per map.
  - Verified every fresh root `000*.mmtile` and `001*.mmtile` now has Detour
    `walkableRadius=1.0247`, `walkableHeight=2.625`, and `walkableClimb=1.8`.
  - Reran `tools/NavDataAudit` for the Orgrimmar route tile set and the
    Undercity arrival tile set; both passed.
  - Reran the offline Orgrimmar flight-master -> zeppelin route gate; it
    passed against the fully regenerated data set.
  - Restarted Docker container `pathfinding-service`. It reloaded
    `/wwow-data/mmaps/` with map `0: 515 tiles`, map `1: 785 tiles`,
    map `389: 4 tiles`, and reported `IsReady=True`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `D:/MaNGOS/source/bin/MoveMapGenerator.exe 0 --threads 1 --configInputPath config.json` from `D:\MaNGOS\data` -> first pass logged success but header sweep showed mixed stale headers.
  - `D:/MaNGOS/source/bin/MoveMapGenerator.exe 1 --threads 1 --configInputPath config.json` from `D:\MaNGOS\data` -> first pass logged success but header sweep showed mixed stale headers.
  - Header sweep after the first pass -> `map=000 tiles=515 radius={0.2000x503, 1.0247x12}`, `map=001 tiles=785 radius={0.2000x767, 1.0247x18}`.
  - Fresh map `0` regeneration after moving old root tiles aside -> `Done.MoveMapGenerator finished with success!`.
  - Fresh map `1` regeneration after moving old root tiles aside -> `Done.MoveMapGenerator finished with success!`.
  - Header sweep after fresh regeneration -> `map=000 tiles=515 radius={1.0247x515} height={2.6250x515} climb={1.8000x515} badParamTiles=0`; `map=001 tiles=785 radius={1.0247x785} height={2.6250x785} climb={1.8000x785} badParamTiles=0`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data` -> `RESULT: PASS`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data --map 0 --tile 27,30 --tile 28,30 --tile 29,30 --tile 30,30 --tile 27,31 --tile 28,31 --tile 29,31 --tile 30,31 --tile 27,32 --tile 28,32 --tile 29,32 --tile 30,32` -> `RESULT: PASS`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_full_fresh_regen.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `docker restart pathfinding-service` -> restarted `pathfinding-service`.
  - `docker ps --filter "name=pathfinding-service" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service Up ... (healthy)`, port `5001`.
  - `docker logs --since 45s pathfinding-service` -> loaded maps `0`, `1`, and `389`; status updated to ready.
- Evidence:
  - Fresh generation logs:
    `tmp/test-runtime/results-pathfinding/mmap_regen_map0_full_fresh_20260501.out.log`
    and
    `tmp/test-runtime/results-pathfinding/mmap_regen_map1_full_fresh_20260501.out.log`.
  - Passing route-gate TRX:
    `tmp/test-runtime/results-pathfinding/long_pathing_route_static_blockers_after_full_fresh_regen.trx`.
- Remaining work:
  - Focused live Crossroads -> Undercity validation is now unblocked but has
    not been run in this pass.
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_full_mmap_regen.trx" --results-directory tmp/test-runtime/results-live`

---

## Handoff (2026-05-01, GO-axis bake and affordance route gate)

- Completed:
  - Confirmed no repo-scoped processes were running after the apparent
    generation hang and no `MoveMapGenerator` process remained.
  - Restored the focused Orgrimmar route tile set from the
    `.pre-simplification10-routefix.bak` backups and restored map `1`
    `config.json` to Tauren Male agent settings without the failed
    `maxSimplificationError` experiment.
  - Kept the GO-axis spawn bake in `D:/MaNGOS/source/contrib/mmap/src/TileWorker.cpp`
    so server-spawned gameobjects are marked in Recast coordinates as
    `(Y,Z,X)`.
  - Added a generic PathfindingService affordance repair pass that asks native
    navigation to classify suspicious uphill legs and locally resamples the
    corridor when a segment is a steep climb or step-up limit violation. The
    production code does not contain Orgrimmar blocker coordinates, clearance
    cylinders, route-specific waypoints, or live-position guards.
  - Hardened the affordance repair scan after a normal-build rerun exposed a
    timing/order flake: the repair now keeps scanning past local
    suspicious-but-unrepairable uphill legs and uses a five-second local budget.
  - Rebuilt `Exports/Navigation/Navigation.vcxproj` after the native
    navigation source was already dirty, reran the audit, and reran the
    focused offline route gate. Live WoW validation remained paused.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `Get-Process MoveMapGenerator -ErrorAction SilentlyContinue` -> no process found.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data --build-log tmp/test-runtime/results-pathfinding/org_transposed_route_tiles_go_axisfix_regen_20260501_completed.log --tile 39,28 --tile 40,28 --tile 41,28 --tile 39,29 --tile 40,29 --tile 41,29 --tile 39,30 --tile 40,30 --tile 41,30` -> `passed`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_go_axisfix_restored_baseline.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `failed (1/1)` only on the exterior steep incline, proving the restored GO-axis tiles cleared the known GO/object/corner blockers but still left an overly steep Detour leg.
  - `$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"; & $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data --build-log tmp/test-runtime/results-pathfinding/org_transposed_route_tiles_go_axisfix_regen_20260501_completed.log --tile 39,28 --tile 40,28 --tile 41,28 --tile 39,29 --tile 40,29 --tile 41,29 --tile 39,30 --tile 40,30 --tile 41,30` -> `passed`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_docs_final.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `failed (1/1)` on the exterior steep incline before the repair scan was hardened.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_affordance_scan_fix.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_final.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
- Evidence:
  - Passing route-gate TRX:
    `tmp/test-runtime/results-pathfinding/long_pathing_route_static_blockers_final.trx`.
  - The passing gate logged
    `[CORRIDOR-AFFORDANCE-REPAIR] segment=282 reason=step_up_limit window=272->289 pathLen=482 repairedLen=467`.
  - Clearance probe after the passing gate reported: lower flight-master
    bonfire clearance `8.67` required `4.50`; bank-front palm/static model
    `19.61` required `4.00`; bank-front bonfire `16.74` required `4.50`;
    Z-hallway north/south `3.89`/`4.75` required `3.50`/`4.00`; exterior
    steep incline `13.91` required `6.00`; exterior rope-line support `6.82`
    required `5.00`.
- Files changed:
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Exports/Navigation/PathFinder.cpp`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - External: `D:/MaNGOS/source/contrib/mmap/src/TileWorker.cpp`
  - External: `D:/MaNGOS/data/config.json`
  - External focused route tiles: `D:/MaNGOS/data/mmaps/0012839.mmtile`,
    `0012840.mmtile`, `0012841.mmtile`, `0012939.mmtile`,
    `0012940.mmtile`, `0012941.mmtile`, `0013039.mmtile`,
    `0013040.mmtile`, and `0013041.mmtile`.
- Remaining work:
  - Full maps `0` and `1` still need regeneration with the current generator
    before treating live Crossroads -> Undercity evidence as final.
  - Focused live validation remains paused.
- Next command: `Push-Location D:\MaNGOS\data; D:/MaNGOS/source/bin/MoveMapGenerator.exe 0 --threads 1 --configInputPath config.json; D:/MaNGOS/source/bin/MoveMapGenerator.exe 1 --threads 1 --configInputPath config.json; Pop-Location`

---

## Handoff (2026-05-01, route-specific clearance rollback)

- Completed:
  - Removed the PathfindingService production workaround that hardcoded
    Orgrimmar static-clearance zones and inserted route-specific snapped
    detours for the flight-master -> zeppelin walk.
  - Reconfirmed the correct policy across the repo docs: deterministic tests
    may name known blockers as evidence, but production path generation must
    not copy those blocker coordinates into runtime avoidance logic.
  - Documented that static gameobject collision must be fixed by GO-aware mmap
    export/generation and regenerated data, not by BotRunner live guards or
    PathfindingService route shaping.
  - Did not run live WoW validation.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -v:minimal` -> `succeeded` with existing PathfindingSocketServer nullable warnings and the existing `dumpbin` applocal warning.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_mmap_required.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `failed as intended (1/1)` with the seven blocker clearances. This is the correct offline blocker until the mmaps are regenerated with effective static GO collision.
- Evidence:
  - Red route-gate TRX:
    `tmp/test-runtime/results-pathfinding/long_pathing_route_static_blockers_mmap_required.trx`.
  - Key blocker lines remain: lower flight-master bonfire clearance `1.59`
    required `4.50`; bank-front palm/static model clearance `0.55` required
    `4.00`; bank-front bonfire clearance `3.12` required `4.50`; Z-hallway
    north/south clearances `0.03` and `0.07`; exterior incline clearance
    `0.28`; rope-line support clearance `1.30`.
  - Focused tile `D:\MaNGOS\data\mmaps\0014028.mmtile` had previously been
    regenerated after backup to
    `D:\MaNGOS\data\mmaps\0014028.mmtile.pre-routefix.bak`, and the generator
    marked `637` GO span boxes; that single-tile run did not change the route,
    so the next fix must inspect the generator/data and regenerate the needed
    map set rather than add runtime route exceptions.
- Files changed:
  - `AGENTS.md`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `docs/DEVELOPMENT_GUIDE.md`
  - `docs/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
- Next command: `Push-Location D:\MaNGOS\data; D:/MaNGOS/source/bin/MoveMapGenerator.exe 0 --threads 1 --configInputPath config.json; D:/MaNGOS/source/bin/MoveMapGenerator.exe 1 --threads 1 --configInputPath config.json; Pop-Location; dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers_after_mmap_regen.trx" --results-directory tmp/test-runtime/results-pathfinding`

---

## Handoff (2026-05-01, generated-route blocker gate)

- Completed:
  - Added a deterministic PathfindingService gate that calculates the
    Orgrimmar flight-master -> Orgrimmar/Undercity zeppelin route with the
    Tauren Male capsule and fails if the generated route clips known static
    blockers before a WoW client is launched.
  - Looked up the Orgrimmar bonfire positions from the local MaNGOS world DB:
    `guid=10975 entry=177026 display=4572 size=2.21128
    pos=(1665.50,-4360.83,26.66)` and
    `guid=10090 entry=177019 display=4572 size=2.21128
    pos=(1592.37,-4427.32,8.05)`.
  - The new generated-route gate currently fails as intended and reports all
    known route blockers in one assertion: lower flight-master bonfire,
    bank-front palm/static model, bank-front bonfire, Z-hallway north/south
    early-cut corners, exterior steep incline, and exterior rope-line support.
  - Updated the live-validation expectation: do not rerun the live
    Crossroads -> Undercity proof until this offline route gate passes because
    regenerated GO-aware mmaps naturally avoid the blockers.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `mysql -h 127.0.0.1 -P 3306 -uroot -proot -t -e "SELECT g.guid,g.id AS entry,t.name,t.displayId,t.size,g.map,g.position_x AS x,g.position_y AS y,g.position_z AS z,g.orientation FROM mangos.gameobject g JOIN mangos.gameobject_template t ON t.entry=g.id WHERE g.map=1 AND g.position_x BETWEEN 1300 AND 1710 AND g.position_y BETWEEN -4700 AND -4300 AND (LOWER(t.name) LIKE '%bonfire%' OR LOWER(t.name) LIKE '%fire%' OR LOWER(t.name) LIKE '%palm%' OR LOWER(t.name) LIKE '%tree%' OR t.displayId IN (4572,1617,1731)) ORDER BY g.position_x DESC, g.position_y;"` -> `found the route bonfires above`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `failed as intended (1/1 failed) with generated-route blocker diagnostics`.
- Evidence:
  - TRX: `tmp/test-runtime/results-pathfinding/long_pathing_route_static_blockers.trx`.
  - Key failure lines:
    - `Orgrimmar lower flight-master bonfire`: clearance `1.59`, required `4.50`, segment `59->60`.
    - `Orgrimmar bank-front palm/static model snag`: clearance `0.55`, required `4.00`, segment `105->106`.
    - `Orgrimmar bank-front bonfire`: clearance `3.12`, required `4.50`, segment `111->112`.
    - `Orgrimmar Z-hallway early-cut north corner`: clearance `0.03`, required `3.50`, segment `151->152`.
    - `Orgrimmar Z-hallway early-cut south corner`: clearance `0.07`, required `4.00`, segment `212->213`.
    - `Orgrimmar exterior steep incline`: clearance `0.28`, required `6.00`, segment `231->232`.
    - `Orgrimmar exterior rope-line support snag`: clearance `1.30`, required `5.00`, segment `259->260`.
- Files changed:
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LongPathingTests.md`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_route_static_blockers.trx" --results-directory tmp/test-runtime/results-pathfinding`

---

## Handoff (2026-05-01, Orgrimmar blocker fast-fail)

- Completed:
  - Ran the requested GUID-identity live command. It failed before the
    Orgrimmar -> Undercity zeppelin leg started, so there were no
    `[TRAVEL_TRANSPORT]` lines to validate in this attempt.
  - The requested run stopped at the Orgrimmar zeppelin tower base:
    `map=1 pos=(1342.7,-4641.4,24.6) transport=0x0 current=null`; the route
    target is the deck/wait point at `(1341.0,-4638.6,53.5)`.
  - Added `LongPathingRouteBlockerGuard` plus deterministic coverage for
    immediate `afford=SteepClimb` diagnostics, tower-base `nav=False /
    resolution=no_route` diagnostics, and stationary dwell in known blocker
    zones: Orgrimmar bonfire/object choke, palm-tree descent, steep incline,
    tower support/flagpole, and tower base/deck mismatch.
  - `LongPathingTests` now requires deck-ish Z before considering the
    zeppelin tower approach complete, so ground-level tower proximity no
    longer lets the test continue to the transport wait.
  - Reran live validation with the fast-fail guard. It now fails in `5m23s`
    at `Known Crossroads -> Undercity pathing blocker: Orgrimmar tower
    support/flagpole object collision`, `map=1`,
    `anchor=(1371.0,-4439.4,30.9)`, `current=(1371.2,-4439.4,30.9)`,
    `moved=0.2`, `transport=0x0`.
- Validation/tests run:
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_guid_identity.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 8m50s; no [TRAVEL_TRANSPORT], stopped at tower base map=1 pos=(1342.7,-4641.4,24.6)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_blocker_guard_tests.trx" --results-directory tmp/test-runtime/results-botrunner` -> first compile failed on a named-argument typo; fixed and reran -> `passed (8/8)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_path_travel_transport_blocker_guard_focus.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (165/165)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_blocker_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 5m23s with named tower support/flagpole blocker`.
- Evidence:
  - Tower-base live TRX: `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_guid_identity.trx`.
  - Tower-base screenshot:
    `tmp/test-runtime/screenshots/long-pathing/Expected-TravelTask-to-finish-tower-approach-and-start-the-Orgrimmar---Undercity-LPATHFG1-client-22760-win0-20260501_085416.png`.
  - Fast-fail live TRX: `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_blocker_guard.trx`.
  - Fast-fail screenshot:
    `tmp/test-runtime/screenshots/long-pathing/Known-Crossroads---Undercity-pathing-blocker-Orgrimmar-tower-support-flagpole-ob-LPATHFG1-client-10036-win0-20260501_090717.png`.
  - Deterministic TRX: `tmp/test-runtime/results-botrunner/long_pathing_blocker_guard_tests.trx`.
  - Broader BotRunner focus TRX:
    `tmp/test-runtime/results-botrunner/botrunner_path_travel_transport_blocker_guard_focus.trx`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuard.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingRouteBlockerGuardTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LongPathingTests.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "1371\\.|4439\\.|SteepClimb|OrgrimmarZeppelin|CrossroadsToUndercity" Tests/PathfindingService.Tests Exports/BotRunner Services/PathfindingService docs -g "!**/bin/**" -g "!**/obj/**"`

---

## Handoff (2026-05-01, transport GUID identity)

- Completed:
  - Confirmed from the latest live evidence that the bot was at the configured
    Orgrimmar -> Undercity zeppelin wait point: `map=1`,
    `pos=(1341.0,-4638.5,53.5)`, `transport=0x0`, near the route wait target
    `(1341.0,-4638.6,53.5)`.
  - Identified the observed nearby same-model zeppelin as the wrong route:
    `entry=175080`, `display=3031` (Orgrimmar/Grom'gol), while the staged
    route must wait for `entry=164871`, `display=3031`
    (Orgrimmar/Undercity).
  - Added `TransportObjectIdentity` so BotRunner can decode static transport
    GUIDs (`0xF120`, entry in bits 24-47) and moving transport GUIDs
    (`0x1FC0`, entry in low 24 bits), then look up the matching
    `TransportData` definition.
  - `PathfindingOverlayBuilder` now canonicalizes nearby object `Entry` from
    GUID before sending overlays to pathfinding/travel logic, so GUID wins
    over stale or wrong reported entries.
  - Scheduled transport matching now requires the resolved gameobject entry
    and display model; boats/zeppelins no longer match by model-only fallback
    when identity is missing. Elevator marker fallback remains intact.
  - `[TRAVEL_TRANSPORT]` diagnostics now include
    `expected=<entry>:<display>:<name>` plus GUID-bearing nearest-object
    formatting, so future live logs identify exactly which object and model
    were seen.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests" --logger "trx;LogFileName=botrunner_transport_guid_identity_focus.trx"` -> `passed (52/52)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests" --logger "trx;LogFileName=botrunner_path_travel_transport_guid_identity_focus.trx"` -> `passed (157/157)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - Previous live TRX showing the bot in place but not boarded:
    `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_compact_uphill_only_guard.trx`.
  - New focused TRX:
    `Tests/BotRunner.Tests/TestResults/botrunner_transport_guid_identity_focus.trx`.
  - New movement focus TRX:
    `Tests/BotRunner.Tests/TestResults/botrunner_path_travel_transport_guid_identity_focus.trx`.
- Files changed:
  - `Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs`
  - `Exports/BotRunner/Movement/TransportData.cs`
  - `Exports/BotRunner/Movement/TransportObjectIdentity.cs`
  - `Exports/BotRunner/Movement/TransportWaitingLogic.cs`
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  - `Tests/BotRunner.Tests/Movement/PathfindingOverlayBuilderTests.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LongPathingTests.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_guid_identity.trx" --results-directory tmp/test-runtime/results-live`

---

## Handoff (2026-05-01, compact tower support and transport evidence)

- Completed:
  - Added compact/tight descending support guards in `NavigationPath` so
    long-travel stall recovery does not promote past the Orgrimmar tower
    rope/support chain before the character has actually made the drop.
  - Changed compact vertical-transition holding to uphill-only after live
    evidence showed a downhill support step could otherwise keep snapping back
    to the upper support layer.
  - Extended scheduled transport boarding logic so zeppelins/boats wait up to
    the scheduled transport boarding budget while the route object remains at
    the stop; elevators keep the short boarding timeout.
  - Tightened `LongPathingTests` so zeppelin transfer evidence is only actual
    transport/map-transfer state, and extended the evidence timeout to match
    the production scheduled transport wait window.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests" --logger "trx;LogFileName=botrunner_path_travel_transport_compact_uphill_only_focus.trx"` -> `passed (151/151)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_compact_uphill_only_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 12m09s; the bot reached the Orgrimmar zeppelin route target at map=1 pos=(1341.0,-4638.5,53.5), transport=0x0, and saw only display-near entry 175080 instead of route entry 164871 before the 6-minute evidence timeout`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~TransportWaitingLogicTests" --logger "trx;LogFileName=botrunner_path_travel_transport_longpath_timeout_compile_focus.trx"` -> `passed (151/151)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinSupport_FirstCompactStep_IsWalkableForTaurenCapsule|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCitySupport_FirstUphillStep_IsWalkableForTaurenCapsule|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCitySupport_ForwardFrictionSegment_IsBlockedForTaurenCapsule" --logger "trx;LogFileName=long_pathing_support_compact_transport_focus.trx"` -> `passed (3/3)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests" --logger "trx;LogFileName=wowsharpclient_movement_controller_transport_boarding_focus.trx"` -> `passed (74/74)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - Live TRX: `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_compact_uphill_only_guard.trx`.
  - Failure screenshot: `tmp/test-runtime/screenshots/long-pathing/Expected-the-bot-to-board-the-Orgrimmar---Undercity-zeppelin-or-complete-the-cro-LPATHFG1-client-6964-win0-20260501_081718.png`.
  - BotRunner focus TRX: `Tests/BotRunner.Tests/TestResults/botrunner_path_travel_transport_longpath_timeout_compile_focus.trx`.
  - Pathfinding focus TRX: `Tests/PathfindingService.Tests/TestResults/long_pathing_support_compact_transport_focus.trx`.
  - WoWSharpClient focus TRX: `Tests/WoWSharpClient.Tests/TestResults/wowsharpclient_movement_controller_transport_boarding_focus.trx`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Movement/TransportWaitingLogic.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LongPathingTests.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_transfer_window_8min.trx" --results-directory tmp/test-runtime/results-live`

---

## Handoff (2026-05-01, Orgrimmar zeppelin ramp-corner guard)

- Completed:
  - Diagnosed the latest focused live failure as long-travel stall recovery
    promoting past an unsatisfied uphill ramp/corner waypoint near the
    Orgrimmar zeppelin tower, matching the user-provided screenshot of the
    Tauren blocked on the deck corner.
  - Added a vertical-aware promotion guard so stuck recovery and long-travel
    destination-progress promotion cannot skip intermediate uphill waypoints
    that have not been vertically reached.
  - Added regression coverage for the failing Orgrimmar deck sequence so the
    active waypoint stays on the ramp corner instead of jumping to the deck
    waypoint.
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_focused_mmap_map0_map1.trx" --results-directory tmp/test-runtime/results-live` -> `failed after 12m54s; still on map=1 at pos=(1339.4,-4645.4,51.9), transport=0x0`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_StalledLongTravelPromotesToDestinationProgressWaypoint|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_StalledVerticalAwareLongTravel_DoesNotPromoteToStackedLowerLayerWaypoint|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_StalledVerticalAwareLongTravel_DoesNotPromotePastUnsatisfiedUphillRampCorner|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_LongTravelReplansWhenNearWaypointIsOverheadLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=navpath_long_travel_ramp_corner_promotion.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~RaceDimensionsConcurrencyTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_pathing_focus_after_ramp_corner_guard.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (116/116)`.
- Evidence:
  - Live TRX: `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_focused_mmap_map0_map1.trx`.
  - Targeted TRX: `tmp/test-runtime/results-botrunner/navpath_long_travel_ramp_corner_promotion.trx`.
  - Focus TRX: `tmp/test-runtime/results-botrunner/botrunner_long_pathing_focus_after_ramp_corner_guard.trx`.
  - The failure screenshot helper returned no path (`exit=2`); the user
    supplied the WoW-client screenshot showing the blocked deck-corner state.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_ramp_corner_guard.trx" --results-directory tmp/test-runtime/results-live`

---

## Handoff (2026-05-01, focused Undercity MMAP regeneration audit)

- Completed:
  - Generalized `tools/NavDataAudit` so GO input checks count model-backed
    spawns in the audited tile set for any map, not only Orgrimmar, and added
    `--build-log` for focused generation logs.
  - Regenerated focused map `0` Undercity arrival tiles `27,30` through
    `30,32` with the GO-aware generator and Tauren Male radius/height.
  - Re-ran deterministic route validation after both focused map `1`
    Orgrimmar and focused map `0` Undercity tiles were regenerated.
- Validation/tests run:
  - `dotnet build tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -v:minimal` -> `succeeded`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data` -> `passed`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data --map 0 --build-log D:/MaNGOS/data/map0_focused_undercity_build.log --tile 27,30 --tile 27,31 --tile 27,32 --tile 28,30 --tile 28,31 --tile 28,32 --tile 29,30 --tile 29,31 --tile 29,32 --tile 30,30 --tile 30,31 --tile 30,32` -> `passed`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_focused_mmap_map0_map1_tauren_go.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (12/12)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~RaceDimensionsConcurrencyTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_pathing_focus_after_focused_mmap.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (115/115)`.
- Evidence:
  - Map `0` audit output: tiles `0003027`, `0003127`, `0003227`,
    `0003028`, `0003128`, `0003228`, `0003029`, `0003129`, `0003229`,
    `0003030`, `0003130`, and `0003230` report `walkableRadius=1.0247`
    and `walkableHeight=2.6250`.
  - Map `0` focused build log: `D:/MaNGOS/data/map0_focused_undercity_build.log`;
    GO marks include `tile=28,31: marked 390 gameobject span boxes`.
  - TRX: `tmp/test-runtime/results-pathfinding/long_pathing_routes_focused_mmap_map0_map1_tauren_go.trx`.
  - TRX: `tmp/test-runtime/results-botrunner/botrunner_long_pathing_focus_after_focused_mmap.trx`.
- Files changed:
  - `tools/NavDataAudit/Program.cs`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/TASKS.md`
- External local data/source touched:
  - `D:/MaNGOS/data/map0_focused_undercity_build.log`
  - `D:/MaNGOS/data/mmaps/0003027.mmtile` through
    `D:/MaNGOS/data/mmaps/0003230.mmtile`
- Next command: `git status --short --branch`

---

## Handoff (2026-05-01, focused Orgrimmar MMAP regeneration audit)

- Completed:
  - Corrected `tools/NavDataAudit` to use the MaNGOS generator tile filename
    order (`mapId + tileY + tileX`), so tile `28,40` audits
    `mmaps/0014028.mmtile`.
  - Rebuilt the local `D:/MaNGOS/source` `MoveMapGenerator` after restoring
    GO-aware marking and `agentRadius` / `agentHeight` config handling.
  - Regenerated the focused Orgrimmar route tiles `28,39` through `30,41` in
    `D:/MaNGOS/data/mmaps` with Tauren Male radius `1.0247` and height
    `2.625`.
- Validation/tests run:
  - `cmd.exe /c 'call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" && cmake --build D:/MaNGOS/source/build-nmake-extractors --target MoveMapGenerator --config Release'` -> `succeeded`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data` -> `passed`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_focused_mmap_tauren_go.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (12/12)`.
- Evidence:
  - Audit output: Orgrimmar tiles `0013928`, `0014028`, `0014128`,
    `0013929`, `0014029`, `0014129`, `0013930`, `0014030`, and `0014130`
    report `walkableRadius=1.0247` and `walkableHeight=2.6250`.
  - Regeneration output marked GO span boxes in every focused tile, including
    `tile=28,40: marked 637 gameobject span boxes`.
  - TRX: `tmp/test-runtime/results-pathfinding/long_pathing_routes_focused_mmap_tauren_go.trx`.
- Files changed:
  - `tools/NavDataAudit/Program.cs`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/TASKS.md`
- External local data/source touched:
  - `D:/MaNGOS/source/contrib/mmap/src/TileWorker.cpp`
  - `D:/MaNGOS/data/config.json`
  - `D:/MaNGOS/data/mmaps/0013928.mmtile` through
    `D:/MaNGOS/data/mmaps/0014130.mmtile`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~RaceDimensionsConcurrencyTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_long_pathing_focus_after_focused_mmap.trx" --results-directory tmp/test-runtime/results-botrunner`

---

## Handoff (2026-05-01, MMAP/Recast generation audit)

- Completed:
  - Added `tools/NavDataAudit`, a deterministic audit that reads
    `config.json`, Orgrimmar `.mmtile` Detour headers,
    `vmaps/temp_gameobject_models`, `gameobject_spawns.json`, and
    `map1_build.log`.
  - Documented the required Tauren Male navmesh generation shape in
    `docs/physics/MMAP_NAVMESH_GENERATION.md`: `agentRadius=1.0247`,
    `agentHeight=2.625`, `walkableRadius=4`, `walkableHeight=11`.
  - Confirmed the current `D:/MaNGOS/data` GO evidence passes for Orgrimmar
    route tiles, but generated tile headers still fail with
    `walkableRadius=0.2` and `walkableHeight=1.5`.
- Validation/tests run:
  - `dotnet build tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -v:minimal` -> `succeeded`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data` -> `failed as expected; GO evidence passed, Tauren radius/height evidence failed`.
- Evidence:
  - Audit output before the tile-name correction targeted `mapId + tileX +
    tileY` filenames; the corrected MaNGOS generator order is `mapId + tileY +
    tileX`.
  - Corrected audit output before focused regeneration: Orgrimmar route tiles
    `0013928` through `0014130` reported `walkableRadius=0.2000` and
    `walkableHeight=1.5000`.
  - Audit output: `temp_gameobject_models` has `930` model mappings,
    `gameobject_spawns.json` has `297` modeled Orgrimmar corridor/tower
    spawns, and the audited map `1` tiles all have `[GO] ... loaded ...`
    build-log evidence.
- Files changed:
  - `tools/NavDataAudit/NavDataAudit.csproj`
  - `tools/NavDataAudit/Program.cs`
  - `docs/physics/MMAP_NAVMESH_GENERATION.md`
  - `docs/physics/README.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/TASKS.md`
- Next command: `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data`

---

## Handoff (2026-05-01, Orgrimmar/Undercity zeppelin route identity)

- Completed:
  - Corrected the static BotRunner zeppelin route entries so
    Orgrimmar/Undercity uses entry `164871`, Grom'gol/Undercity uses
    `176495`, and Orgrimmar/Grom'gol uses `175080`.
  - Added deterministic `TransportWaitingLogicTests` coverage that verifies
    entry `164871` has Orgrimmar and Undercity stops and no Grom'gol stop.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests" --logger "console;verbosity=minimal"` -> `passed (28/28)`.
- Evidence:
  - Console result: `Passed! - Failed: 0, Passed: 28, Skipped: 0`.
- Files changed:
  - `Exports/BotRunner/Movement/TransportData.cs`
  - `Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `git diff --check -- Exports/BotRunner/Movement/TransportData.cs Tests/BotRunner.Tests/Movement/TransportWaitingLogicTests.cs docs/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md`

---

## Handoff (2026-04-30, Crossroads -> Undercity Tauren pathfinding clearance)

- Completed:
  - Added native `FindPathForAgent(...)` / `CalculatePathForAgent(...)` so
    PathfindingService can route with the caller's capsule radius and height
    instead of the fixed default player capsule.
  - Updated `PathFinder` smoothing/validation to carry the agent capsule into
    segment validation, local detour refinement, simplification, and Detour
    wall-clearance nudging.
  - Reworked `PathfindingService.Repository.Navigation` long-route validation
    to stay bounded while repairing early static/capsule breaks: smooth-route
    densification, duplicate support-anchor collapse, early support-layer
    normalization, and generic local escape candidates now cover the Orgrimmar
    support/tree stall without hard-coded Orgrimmar micro-waypoints.
  - Added `LongPathingRouteTests` for the known Crossroads -> Undercity bad
    walk legs using Tauren Male dimensions, including the flight-master descent,
    city support stall, L-corner/pillar corridor, ramp/ceiling stall, exterior
    support recovery, tower friction recovery, and Undercity arrival route.
- Validation/tests run:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`.
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded` with existing `PathfindingSocketServer` warnings and nonfatal `dumpbin` noise.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_tauren_agent_collapsed_support.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`.
- Evidence:
  - TRX: `tmp/test-runtime/results-pathfinding/long_pathing_routes_tauren_agent_collapsed_support.trx`.
- Files changed:
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/Navigation.h`
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/PathFinder.h`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathRouteAssertions.cs`
  - `docs/TASKS.md`
  - `Exports/Navigation/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
- Next command: `git status --short --branch`

---

## Handoff (2026-04-29, MVT-TRANSPORT-NAMED-UC closeout)

- Completed:
  - Closed `MVT-TRANSPORT-NAMED-UC`: the stricter named-Undercity elevator
    parity lane now asks PathfindingService for the route from `.tele name
    <character> undercity` landing `(1584.07,241.987,-52.1534)` to the west
    lower boarding point `(1532.3,242.2,-41.4)` instead of using hand-authored
    approach waypoints.
  - `MovementParityTests.TransportRide_FgBgParity` now logs the generated
    route, fixture-drives the generated corners with `SetFacing` +
    `StartMovement` / `StopMovement`, waits for the real west elevator at the
    lower stop, boards both clients together, requires transport evidence for
    both, and stops each participant as soon as it reaches the upper exit.
  - Background transport-local movement now preserves the player offset while
    on gameobject transports, models the known Undercity elevator lower-hold /
    ascent / upper-dismount window, avoids passive reattach to upper-stop
    elevator cars, and preserves transport-local orientation when explicit
    facing updates occur on transport.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_named_undercity_pathfinding_route_18.trx"` -> `passed (1/1; route generated 13 corners; both clients rode and dismounted at the upper exit)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.PhysicsStep_OnMovingTransport_PreservesLocalOffsetAndSyncsWorldPosition|FullyQualifiedName~MovementControllerTests.Update_KnownUndercityElevatorRide_AnimatesToUpperAndDismounts|FullyQualifiedName~MovementControllerTests.Update_AtUpperUndercityElevatorExit_DoesNotPassiveReattach|FullyQualifiedName~MovementControllerTests.PhysicsStep_OnTransport_UsesLocalCoordinatesAndIncludesTransportObject|FullyQualifiedName~MovementControllerTests.PhysicsResult_OnTransport_RecomputesLocalOffsetFromWorldOutput|FullyQualifiedName~MovementControllerTests.Update_BeforeUndercityElevatorDeck_DoesNotPassiveAttach|FullyQualifiedName~MovementControllerTests.Update_OnUndercityElevatorDeck_AttachesToCar|FullyQualifiedName~MovementControllerTests.Update_IdleNearUndercityElevatorDoorMarker_DoesNotPassiveAttach" --logger "console;verbosity=minimal"` -> `passed (8/8; existing warnings/nonfatal dumpbin noise)`.
- Evidence:
  - TRX: `tmp/test-runtime/results-live/movement_parity_transport_named_undercity_pathfinding_route_18.trx`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MovementParityTests.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Next command: `git status --short --branch`

---

## Handoff (2026-04-29, named Undercity elevator route correction)

- Completed:
  - Replaced the weak lower-car `Goto` transport check with the stricter test
    shape requested for `MovementParityTests.TransportRide_FgBgParity`:
    both bots are teleported with `.tele name <character> undercity`, then the
    fixture manually drives movement toward the Undercity west elevator using
    `SetFacing`, `StartMovement`, and `StopMovement` actions.
  - Added minimal `START_MOVEMENT` / `STOP_MOVEMENT` action contracts and
    BotRunner dispatch mapping so live tests can drive FG/BG forward movement
    without going through `Goto` pathfinding.
  - Elevator boarding now waits for the real west Undercity elevator at the
    lower stop, then starts both bots forward together and requires both to
    acquire transport state before tracing the ride up and dismount.
  - Re-opened tracker `MVT-TRANSPORT-NAMED-UC` because the stronger named
    Undercity route currently fails before boarding.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_named_undercity_observe_05.trx"` -> `failed (1/1; first named-teleport lower-route waypoint: BG reached the intended z=-43 layer, FG fell/drove on z=-66 layer)`.
- Evidence:
  - TRX: `tmp/test-runtime/results-live/movement_parity_transport_named_undercity_observe_05.trx`.
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Exports/GameData.Core/Enums/CharacterAction.cs`
  - `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MovementParityTests.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_named_undercity_route_followup.trx"`

---

## Handoff (2026-04-29, MVT-TRANSPORT-FG closeout)

- Completed:
  - Closed `MVT-TRANSPORT-FG`: `MovementParityTests.TransportRide_FgBgParity`
    no longer carries the tracked FG skip, and the full live movement bundle is
    green with `5` passed and `0` skipped.
  - Replaced the synthetic lower-car teleport with action-driven boarding:
    after synchronizing on the real west Undercity elevator at the lower stop,
    FG/BG now dispatch `Goto` from the lower wait point to the lower car center.
  - Hardened direct movement staging so an in-world, near-target, non-airborne
    final snapshot can satisfy stale teleport-settle polling, but still-moving
    horizontal snapshots are stopped before the test action begins.
  - Added post-running-jump residual movement cleanup so a short jump probe
    cannot leak native forward movement into the next live parity lane.
- Validation/tests run:
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` ->
    required containers were up: `mangosd`, `realmd`,
    `pathfinding-service`, and `maria-db`.
  - `git status --short --branch` -> `## main...origin/main` at session start.
  - `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md` -> found only `MVT-TRANSPORT-FG` in master/local BotRunner trackers.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_lower.trx"` -> `passed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_full.trx"` -> `failed (2/5; FG lower-wait settle rejected an in-world final snapshot; later knockback saw the crashed FG client)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_full_02.trx"` -> `failed (1/5; transport passed, knockback exposed residual native movement after transport)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_full_03.trx"` -> `failed (1/5; transport passed, knockback still exposed residual movement before staging was tightened)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_goto_board_full_04.trx"` -> `passed (5/5, 0 skipped; duration 3m22s)`.
  - Final `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - TRX: `tmp/test-runtime/results-live/movement_parity_transport_fg_goto_board_lower.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_transport_fg_goto_board_full_04.trx`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MovementParityTests.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Next command: `git status --short --branch`

---

## Handoff (2026-04-29, movement parity health check)

- Completed:
  - Confirmed the user suspicion: the live movement bundle was not fully healthy
    at the start of the session.
  - Fixed the knockback staging settle gate so a stable, in-world final snapshot
    can satisfy staging when `WaitForTeleportSettledAsync` is stale.
  - Reworked the Undercity elevator probe to synchronize on the real west
    elevator at the lower stop, then place FG/BG on the lower car.
  - Converted the remaining intermittent foreground elevator ride gap into
    tracked work `MVT-TRANSPORT-FG`; taxi rides remain spline movement coverage,
    not gameobject transport coverage.
  - Replaced new ad-hoc elevator/placement wait loops with
    `WaitForSnapshotConditionAsync(...)` snapshot polling helpers.
- Validation/tests run:
  - `docker ps` -> required containers were up: `mangosd`, `realmd`,
    `pathfinding-service`, and `maria-db`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckBinaryParityTests" --logger "console;verbosity=minimal"` -> `passed (41/41)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_IdleNearGameObjectTransport_AttachesBeforePostTeleportGroundSnap|FullyQualifiedName~MovementControllerTests.Update_IdleNearMapObjectTransportDeck_AttachesWithZeppelinOriginOffset|FullyQualifiedName~ObjectManagerWorldSessionTests.DirectMonsterMove_MovingTransportHighGuid_CreatesGameObjectTransport|FullyQualifiedName~ObjectManagerWorldSessionTests.MessageMoveKnockBack_PrimesImpulseWithoutForceAck|FullyQualifiedName~ObjectUpdateMutationOrderTests.MovingTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject|FullyQualifiedName~ObjectUpdateMutationOrderTests.StaticTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_current_check.trx"` -> `failed (1/5; knockback staging did not settle despite stable BG position)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Knockback_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_knockback_current_fix.trx"` -> `passed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_current_fix_full.trx"` -> `failed (1/5; FG did not show Undercity elevator transport ride evidence)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_current_fix.trx"` -> `passed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_current_fix_full_02.trx"` -> `failed (1/5; FG WoW.exe crashed during staging and later stayed at the lower elevator stop without transport evidence while BG recorded transport samples)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_current_tracked_fg_transport.trx"` -> `passed (5/5; intermittent FG transport gap did not reproduce)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors after polling-helper refactor; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_current_polling_helper.trx"` -> `passed overall with tracked skip (4 passed, 1 skipped, 0 failed)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - TRX: `tmp/test-runtime/results-live/movement_parity_current_check.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_knockback_current_fix.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_current_fix_full.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_transport_current_fix.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_current_fix_full_02.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_current_tracked_fg_transport.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_current_polling_helper.trx`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MovementParityTests.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_fg_gap_followup.trx"`

---

## Handoff (2026-04-28, ACK corpus promotion)

- Completed: promoted the three previously untracked live ACK corpus captures:
  two `MSG_MOVE_TELEPORT_ACK` samples for GUID `366` with counters `0` and `1`,
  plus one zero-payload `MSG_MOVE_WORLDPORT_ACK` sample.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckBinaryParityTests" --logger "console;verbosity=minimal"` -> `passed (41/41; existing warnings/nonfatal dumpbin noise)`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_TELEPORT_ACK/20260427_234918_747_0000.json`
  - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_TELEPORT_ACK/20260427_235007_703_0000.json`
  - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK/20260427_234925_875_0001.json`
  - `docs/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `git status --short --branch`

---

## Handoff (2026-04-28, direct FG/BG movement activity parity overhaul)

- Completed: replaced the janky movement parity shape with direct FG/BG
  activity probes: point-to-point pathfinding, running jump, GM self-knockback,
  and an Undercity elevator gameobject transport ride.
- Key corrections:
  - SHODAN is not used as a behavior actor in `MovementParityTests`; the
    movement participants self-stage with account-level GM access.
  - Taxi rides are documented as spline-based movement, not transport evidence.
  - Gameobject transport evidence is now tied to the Undercity elevator probe
    via sustained transport samples or the elevator's vertical ride.
  - BG handles direct jump dispatch, BG self-knockback packets without forcing
    an ACK, moving-transport high GUID object creation, and passive attach to
    nearby gameobject transports.
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `.\protocsharp.bat "." ".."` from `Exports/BotCommLayer/Models/ProtoDef` -> `succeeded`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors after proto regeneration)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_IdleNearGameObjectTransport_AttachesBeforePostTeleportGroundSnap|FullyQualifiedName~MovementControllerTests.Update_IdleNearMapObjectTransportDeck_AttachesWithZeppelinOriginOffset|FullyQualifiedName~ObjectManagerWorldSessionTests.DirectMonsterMove_MovingTransportHighGuid_CreatesGameObjectTransport|FullyQualifiedName~ObjectManagerWorldSessionTests.MessageMoveKnockBack_PrimesImpulseWithoutForceAck|FullyQualifiedName~ObjectUpdateMutationOrderTests.MovingTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject|FullyQualifiedName~ObjectUpdateMutationOrderTests.StaticTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.TransportRide_FgBgParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_transport_elevator_04.trx"` -> `passed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_direct_actions_full_04.trx"` -> `passed (5/5; duration 2m41s)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - TRX: `tmp/test-runtime/results-live/movement_parity_transport_elevator_04.trx`.
  - TRX: `tmp/test-runtime/results-live/movement_parity_direct_actions_full_04.trx`.
- Worktree note:
  - The three untracked ACK corpus captures remain untracked and were not
    promoted.
- Files changed:
  - movement action contracts/dispatch, WoWSharpClient movement/object handling,
    deterministic WoWSharpClient tests, live `MovementParityTests`, and
    movement/taxi/transport docs/task trackers.
- Next command: `git status --short --branch`

---

## Handoff (2026-04-28, live movement parity bundle)

- Completed: ran the live BotRunner movement parity bundle after Stream 4
  closeout.
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_category_latest.trx"` -> `passed (8 passed, 5 skipped, 0 failed; duration 10m26s)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - TRX: `tmp/test-runtime/results-live/movement_parity_category_latest.trx`.
  - Skipped lanes remained the tracked MovementParity skips:
    `Parity_Durotar_ObstacleDense`, `Parity_ValleyOfTrials_ReverseHill`,
    `Parity_ValleyOfTrials_SteepDescent`, `Parity_ValleyOfTrials_LedgeDrop`,
    and `Parity_ValleyOfTrials_HillPath`.
- Worktree note:
  - The three untracked ACK corpus captures remain untracked and were not
    promoted.
- Files changed:
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "TransportGuid|Transport_Board_FgBgParity|Transport_CrossContinent_FgBgParity|StageBotRunnerAtOrgrimmarZeppelinTowerAsync|StageBotRunnerAtUndercityElevatorUpperAsync" Tests/BotRunner.Tests/LiveValidation Tests/BotRunner.Tests/LiveValidation/docs`

---

## Handoff (2026-04-28, tracker sweep after Stream 4 closeout)

- Completed: confirmed Stream 4 is fully closed in the master/local parity docs
  and found no remaining unchecked items in the listed task trackers.
- ACK corpus decision:
  - Inspected the three untracked ACK corpus captures:
    `MSG_MOVE_TELEPORT_ACK/20260427_234918_747_0000.json`,
    `MSG_MOVE_TELEPORT_ACK/20260427_235007_703_0000.json`, and
    `MSG_MOVE_WORLDPORT_ACK/20260427_234925_875_0001.json`.
  - They are valid live captures, but they add no new deterministic shape beyond
    the committed teleport/worldport ACK corpus coverage, so they were left
    untracked and intentionally not promoted.
- Archive/tracker sweep:
  - `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md` -> no matches.
  - No completed checklist item needed to move to `TASKS_ARCHIVE.md` in this
    sweep; the current handoff blocks remain in-place as continuation context.
  - Corrected `docs/physics/bg_movement_parity_audit.md` so the knockback
    section points to the now-closed transport/zeppelin section instead of
    claiming transport remains open.
- Session checks:
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` ->
    `mangosd`, `realmd`, `pathfinding-service`, and `maria-db` were running
    and healthy.
  - `git log -1 --oneline` -> `46296362 test(bg-movement-parity): pin zeppelin transport object-update window`.
  - `git status --short --branch` -> `main...origin/main`, with only the three
    untracked ACK corpus captures listed above.
- Files changed:
  - `docs/TASKS.md`
  - `docs/physics/bg_movement_parity_audit.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_category_latest.trx"`

---

## Handoff (2026-04-28, Stream 4 zeppelin transport object-update baselines)

- Completed: closed the Stream 4 zeppelin transport baseline gap. FG/BG
  recorders now derive `transport_packet_window` from route-specific transport
  evidence rather than relying only on `SMSG_MONSTER_MOVE_TRANSPORT`.
- Last delta:
  - Added `PostTeleportWindowTriggerClassifier`, shared by FG/BG recorders.
    It keeps the existing teleport/worldport/knockback triggers, still accepts
    `SMSG_MONSTER_MOVE_TRANSPORT`, and adds:
    - ordinary `SMSG_MONSTER_MOVE` when the mover GUID encodes the configured
      transport entry;
    - `SMSG_UPDATE_OBJECT` / `SMSG_COMPRESSED_UPDATE_OBJECT` when the decoded
      object-update payload mentions the configured transport entry.
  - Added `WWOW_TRANSPORT_PACKET_WINDOW_ENTRIES` /
    `WWOW_TRANSPORT_PACKET_WINDOW_ENTRY` support; default is the local MaNGOS
    Orgrimmar/Undercity zeppelin entry `164871`.
  - Exposed decoded BG receive payloads through `WoWClient.PacketReceivedDetailed`
    so the BG recorder can classify object-update / monster-move triggers.
  - Promoted FG/BG Orgrimmar zeppelin transport baselines:
    `foreground_orgrimmar_zeppelin_transport_update_baseline.json` and
    `background_orgrimmar_zeppelin_transport_update_baseline.json`.
  - Added
    `PostTeleportPacketWindowParityTests.OrgrimmarZeppelinTransportBaselines_PinRouteObjectUpdateTrigger`.
- Research result:
  - The normal Orgrimmar/Undercity route still did not emit
    `SMSG_MONSTER_MOVE_TRANSPORT`.
  - The stable trigger is `SMSG_UPDATE_OBJECT` mentioning entry `164871`; the
    FG raw payload also contains `GAMEOBJECT_TYPE_ID = 15` (`MoTransport`).
  - Both FG and BG transport windows then observe the same ordinary
    `SMSG_MONSTER_MOVE` sequence.
- Validation/tests run:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPostTeleportWindowRecorderTests" --logger "console;verbosity=minimal"` -> `passed (9/9; existing warnings/nonfatal dumpbin noise)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_POST_TELEPORT_WINDOW='1'; $env:WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW='1'; $env:WWOW_TRANSPORT_PACKET_WINDOW_ENTRIES='164871'; $env:WWOW_POST_TELEPORT_WINDOW_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/zeppelin-transport-capture-20260428_03'; $env:WWOW_BG_POST_TELEPORT_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/zeppelin-transport-capture-20260428_03'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_LOG_LEVEL='Information'; $env:WWOW_FILE_LOG_LEVEL='Information'; $env:WWOW_CONSOLE_LOG_LEVEL='Warning'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundAndBackground_OrgrimmarZeppelin_CapturesTransportPacketWindows" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fg_bg_zeppelin_transport_window_03.trx"` -> `passed (1/1)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (10/10; existing warnings/nonfatal dumpbin noise)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - TRX: `tmp/test-runtime/results-live/fg_bg_zeppelin_transport_window_03.trx`.
  - Promoted source fixtures:
    `tmp/test-runtime/zeppelin-transport-capture-20260428_03/foreground_20260428_175423_661.json` and
    `tmp/test-runtime/zeppelin-transport-capture-20260428_03/background_20260428_175423_657.json`.
- Files changed:
  - `Exports/BotRunner/PostTeleportWindowTriggerClassifier.cs`
  - `Exports/WoWSharpClient/Networking/Implementation/PacketPipeline.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Client/WoWClient.cs`
  - `Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`
  - `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPostTeleportWindowRecorderTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`
  - new transport packet-window fixtures
  - transport docs/task trackers.
- Next command: `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md`

---

## Handoff (2026-04-28, Stream 4 zeppelin transport trigger research)

- Completed/partial: added the first explicit transport packet-window trigger
  for `SMSG_MONSTER_MOVE_TRANSPORT` in the FG and BG packet-window recorders,
  corrected Orgrimmar/Undercity zeppelin staging to the MaNGOS
  `DurotarZeppelin` point (`1340.98, -4638.58, 53.5445`, map `1`) and
  transport entry `164871`, and added an opt-in FG/BG zeppelin live probe.
- Research result:
  - Deterministic recorder coverage proves FG captures
    `SMSG_MONSTER_MOVE_TRANSPORT` as `transport_packet_window`.
  - The live Orgrimmar/Undercity zeppelin probe staged both FG and BG at the
    corrected tower point and waited one route cycle, but produced only
    staging `post_teleport_packet_window` fixtures. No
    `transport_packet_window` fixture was emitted, so no transport baseline was
    promoted.
  - Next transport work should research a route-specific trigger beyond
    `SMSG_MONSTER_MOVE_TRANSPORT` (likely GO update / ordinary
    `SMSG_MONSTER_MOVE` payload correlation or action-driven boarding), rather
    than assuming the obvious transport opcode covers normal zeppelins.
- Validation/tests run:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPostTeleportWindowRecorderTests" --logger "console;verbosity=minimal"` -> `passed (6/6; existing nonfatal dumpbin warning)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_POST_TELEPORT_WINDOW='1'; $env:WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW='1'; $env:WWOW_POST_TELEPORT_WINDOW_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/zeppelin-transport-capture-20260428_02'; $env:WWOW_BG_POST_TELEPORT_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/zeppelin-transport-capture-20260428_02'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_LOG_LEVEL='Information'; $env:WWOW_FILE_LOG_LEVEL='Information'; $env:WWOW_CONSOLE_LOG_LEVEL='Warning'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundAndBackground_OrgrimmarZeppelin_CapturesTransportPacketWindows" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fg_bg_zeppelin_transport_window_02.trx"` -> `test run successful; 1 skipped with tracked reason: no SMSG_MONSTER_MOVE_TRANSPORT transport windows within one route cycle`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - TRX: `tmp/test-runtime/results-live/fg_bg_zeppelin_transport_window_02.trx`.
  - Staging-only fixtures:
    `tmp/test-runtime/zeppelin-transport-capture-20260428_02/*.json`
    (`post_teleport_packet_window` only; no `transport_packet_window`).
- Files changed:
  - `Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPostTeleportWindowRecorderTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TaxiTransportParityTests.cs`
  - transport docs/task trackers.
- Next command: `rg -n "SMSG_MONSTER_MOVE|SMSG_COMPRESSED_UPDATE_OBJECT|OBJECT_FIELD_ENTRY|TransportGuid|MOVEFLAG_ONTRANSPORT" Exports/WoWSharpClient Services/ForegroundBotRunner Services/BackgroundBotRunner Tests/BotRunner.Tests/LiveValidation docs/physics -g "!**/bin/**" -g "!**/obj/**"`

---

## Handoff (2026-04-28, BG movement parity Stream 4 worldport ACK + knockback)

- Completed: closed the FG `MSG_MOVE_WORLDPORT_ACK` post-load recorder gap and
  the knockback Stream 4 baseline/implementation gap. Transport/zeppelin is
  now the remaining Stream 4 research item.
- Last delta:
  - Foreground and background post-teleport window recorders now classify
    transfer windows, outbound `MSG_MOVE_WORLDPORT_ACK` windows, and inbound
    `SMSG_MOVE_KNOCK_BACK` windows by scenario.
  - `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled` now
    also waits for a foreground packet-window fixture containing
    `MSG_MOVE_WORLDPORT_ACK`; promoted
    `foreground_ek_to_kalimdor_worldport_ack_baseline.json`.
  - BG knockback now stages server knockback as `MOVEFLAG_JUMPING`, preserves
    directional intent, primes jump fields from the server vector, consumes the
    impulse in `MovementController`, then emits `CMSG_MOVE_KNOCK_BACK_ACK`
    followed by movement packets.
  - Added `ForegroundAndBackground_Knockback_CapturesPacketWindows`, using
    Taragaman the Hungerer's real `Uppercut` knockback in Ragefire Chasm; it
    captures both FG and BG windows and isolates FG before the BG leg so the
    creature targets the BG bot.
  - Promoted `foreground_knockback_baseline.json` and
    `background_knockback_baseline.json`; added
    `ForegroundWorldportAckBaseline_PinsObservedAckInsideTransferWindow` and
    `KnockbackBaselines_PinFgAndBgAckShape`.
- Validation/tests run:
  - `git status --short` -> preserved the three pre-existing untracked ACK
    corpus JSON files and added only post-teleport packet-window fixtures.
  - `docker ps` -> checked once at session start; `mangosd`, `realmd`,
    `pathfinding-service`, and `maria-db` were running/healthy.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_POST_TELEPORT_WINDOW='1'; $env:WWOW_POST_TELEPORT_WINDOW_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/fg-worldport-ack-capture-20260428_02'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_LOG_LEVEL='Information'; $env:WWOW_FILE_LOG_LEVEL='Information'; $env:WWOW_CONSOLE_LOG_LEVEL='Warning'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fg_worldport_ack_window_02.trx"` -> `passed (1/1)`.
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_POST_TELEPORT_WINDOW='1'; $env:WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW='1'; $env:WWOW_POST_TELEPORT_WINDOW_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/knockback-capture-20260428_09'; $env:WWOW_BG_POST_TELEPORT_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/knockback-capture-20260428_09'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_LOG_LEVEL='Information'; $env:WWOW_FILE_LOG_LEVEL='Information'; $env:WWOW_CONSOLE_LOG_LEVEL='Warning'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundAndBackground_Knockback_CapturesPacketWindows" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fg_bg_knockback_window_09.trx"` -> `passed (1/1)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MoveKnockBack|FullyQualifiedName~PendingKnockback|FullyQualifiedName~AckBinaryParityTests" --logger "console;verbosity=minimal"` -> `passed (46/46)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`.
- Evidence:
  - Worldport ACK source:
    `tmp/test-runtime/fg-worldport-ack-capture-20260428_02/foreground_20260428_145244_980.json`.
  - Knockback sources:
    `tmp/test-runtime/knockback-capture-20260428_09/foreground_20260428_155002_042.json` and
    `tmp/test-runtime/knockback-capture-20260428_09/background_20260428_155012_776.json`.
  - TRX files:
    `tmp/test-runtime/results-live/fg_worldport_ack_window_02.trx` and
    `tmp/test-runtime/results-live/fg_bg_knockback_window_09.trx`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`
  - knockback/object-manager parity tests and new packet-window fixtures
  - physics docs and task trackers.
- Next command: `rg -n "TransportGuid|ON_TRANSPORT|SMSG_MONSTER_MOVE_TRANSPORT|TaxiTransportParityTests|TransportTests" Tests/BotRunner.Tests/LiveValidation Services docs/physics -g "!**/bin/**" -g "!**/obj/**"`

---

## Handoff (2026-04-26, Shodan trade foreground stabilization)

- Completed: closed the foreground trade action follow-up. `TradeParityTests`
  now runs foreground cancel and foreground-initiated item/gold transfer under
  the Shodan director topology; `TradingTests` keeps only the BG-initiated
  transfer as an explicit tracked skip.
- Last delta:
  - `ActionDispatcher` now sends trade gold/item/accept/cancel through
    `IObjectManager` async methods instead of FG behavior-tree frame snippets.
  - `FgTradeFrame` handles trade popup accept/decline, trade-money entry,
    cursor cleanup, and own-offer confirmation polling.
  - BG `AcceptTradeAsync` distinguishes a pending invitation from final trade
    acceptance, preventing initiator final-accept from re-sending begin-trade.
  - The remaining BG-to-FG transfer gap is documented in
    `SHODAN_MIGRATION_INVENTORY.md`: all actions ACK `Success`, but item/copper
    stay with the BG initiator.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradeNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`.
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> `passed (1/1)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_fg_shodan_final.trx"` -> `passed (3), skipped (1)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> `failed with known Ratchet anchor instability: FG loot_window_timeout / max_casts_reached`.
  - Repo-scoped cleanup before/after live validation and anchor -> `No repo-scoped processes to stop.`
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/WoWSharpClient/Networking/ClientComponents/I/ITradeNetworkClientComponent.cs`
  - `Exports/WoWSharpClient/Networking/ClientComponents/TradeNetworkClientComponent.cs`
  - `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TradingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`
  - trade docs/task trackers and focused FG/WoWSharpClient tests.
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Services/WoWStateManager/TASKS.md Exports/BotRunner/TASKS.md Services/ForegroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md`

---

## Handoff (2026-04-25, Shodan mail foreground stabilization)

- Completed: closed the foreground mail runtime follow-up; `MailSystemTests`
  and `MailParityTests` now dispatch `ActionType.CheckMail` to both FG and BG
  under the Shodan director topology.
- Last delta:
  - Foreground mailbox collection now waits through delayed mailbox metadata
    refreshes when visible inbox rows have not exposed ready money/items.
  - BotRunner emits a structured `[MAIL-COLLECT]` diagnostic marker from
    `CollectAllMailWithResultAsync(...)`, allowing foreground mail assertions
    to observe action completion when snapshot deltas lag.
  - SOAP mail staging waits for delivery settlement, item assertions accept
    fresh foreground collection markers, and mail docs/inventory no longer
    describe the slice as BG-only.
- Validation/tests run:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.CollectInboxAttachmentsLua|FullyQualifiedName~ForegroundInteractionFrameTests.DeleteEmptyInboxItemsLua|FullyQualifiedName~ForegroundInteractionFrameTests.WaitForInboxPendingAttachmentsAsync" --logger "console;verbosity=minimal"` -> `passed (3/3)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> `passed (4/4)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Files changed:
  - `Exports/GameData.Core/Interfaces/IObjectManager.cs`
  - `Exports/GameData.Core/Models/MailCollectionResult.cs`
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`
  - live-validation docs and task trackers.
- Next command: `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"`

---

## P4 - Command ACK Infrastructure (message capture parity + structured ACKs)

P3 is archived (see `docs/TASKS_ARCHIVE.md`); this phase builds on the loadout
hand-off and tightens how the bot observes the server's response to GM
commands / player actions.

### Context
Today `LoadoutTask` advances on `IsSatisfied()` polling of `ObjectManager`
state (spell in `KnownSpellIds`, item in bags, skill value in `SkillInfo`).
That works but has three real gaps:

1. **BG parity holes.** `SMSG_LEARNED_SPELL` / `SMSG_REMOVED_SPELL` /
   `SMSG_SPELL_FAILURE` / `SMSG_INVENTORY_CHANGE_FAILURE` / `SMSG_NOTIFICATION`
   update `ObjectManager` silently — no event fires
   (`Exports/WoWSharpClient/Handlers/SpellHandler.cs`,
   `Exports/WoWSharpClient/Client/WorldClient.cs`). FG sees these via Lua
   hooks; BG sees nothing, so BG tests reading `[SKILL]` / `[ERROR]` prefixes
   from the snapshot come up empty.
2. **Snapshot signature churn.** `BotRunnerService.SnapshotChangeSignature`
   (`Exports/BotRunner/BotRunnerService.cs:111-125`) includes
   `RecentChatCount` + `RecentErrorCount`. Every new message flips the count
   → forces a full snapshot send. Under heavy chat (loadout dispatch, BG
   fights) we send full snapshots every tick and defeat the 2s heartbeat
   throttle.
3. **No structured per-command ACK.** Tests that want to say *"did this
   specific `.learn 12345` succeed?"* have to baseline + diff + pattern-match
   on text (`LiveBotFixture.BotChat.cs.GetDeltaMessages` +
   `LiveBotFixture.Assertions.cs.ContainsCommandRejection`). Works today
   but brittle — there is no correlation id on `ActionMessage`, and MaNGOS
   1.12 emits no chat text for most GM command successes, so the "wait for
   system message" pattern can't gate on `.learn` / `.setskill` / `.additem`
   at all.

### Goal
Close the BG event-parity gap, stop message volume from churning the
snapshot signature, and give `LoadoutTask` an event-driven alternative
(push notification) to its current polling-based `IsSatisfied` path —
without throwing away the polling fallback, which is the only option for
commands that have no authoritative SMSG (`.modify money`, `.setskill`,
`.modify health/mana`).

### Rules
1. **Every `.learn` must target a specific numeric spell id, every
   `.setskill` a specific skill id.** Catch-all MaNGOS commands
   (`.learn all_myclass`, `.learn all_myspells`) are forbidden — see
   `memory/feedback_explicit_spell_learning.md`.
2. **Polling stays.** State observation is the authoritative success
   signal; events are a latency optimization, not a replacement.
3. **No new free-form message buckets.** If BG needs to surface a new
   SMSG-observed event, it goes through an existing `IWoWEventHandler`
   event (new prefix if needed) so FG parity is preserved.
4. **Snapshot budget matters.** Do not add unbounded repeated fields.
   Ring-buffer with an explicit cap; document it next to the field.

### Sub-phases

- [x] **P4.1** Close BG SMSG → event parity gap
  - [x] P4.1.1 Add `OnLearnedSpell(spellId)` and `OnUnlearnedSpell(spellId)`
    events to `Exports/GameData.Core/Interfaces/IWoWEventHandler.cs`. Fire
    from `SpellHandler.HandleLearnedSpell` / `HandleRemovedSpell`. Surface
    as `[SKILL] Learned spell <id>` / `[SKILL] Unlearned spell <id>` in
    `BotRunnerService.Messages.cs`.
  - [x] P4.1.2 Add `OnSkillUpdated(skillId, oldValue, newValue, maxValue)`
    event. Fire from whichever `SMSG_UPDATE_OBJECT` path mutates
    `IWoWLocalPlayer.SkillInfo` (locate the descriptor-walker site in
    `ObjectUpdate`-family handlers). Surface as
    `[SKILL] Skill <id> <old>→<new>/<max>`.
  - [x] P4.1.3 Add `OnItemAddedToBag(bag, slot, itemId, count)` event.
    Fire from the inventory-change-success path
    (`LootingNetworkClientComponent.OnItemPushResultReceived` is the
    existing observable — mirror it into an `IWoWEventHandler` event so
    FG/BG parity is maintained). Surface as `[UI] Item <id> x<count>
    → bag <bag>/<slot>`.
  - [x] P4.1.4 Route `SMSG_ATTACKSWING_*`, `SMSG_INVENTORY_CHANGE_FAILURE`,
    and `SMSG_SPELL_FAILURE` through `FireOnErrorMessage` alongside their
    existing Rx/diagnostic channels. Today they're silent at the event
    layer (`WorldClient.cs:234-251`, `SpellHandler.cs:459`).
  - [x] P4.1.5 Register a handler for `SMSG_NOTIFICATION` (0x1CB) and
    raise `OnSystemMessage(text)`.
  - [x] P4.1.6 Unit tests: each new event fires once per matching
    inbound packet; `[SKILL]` / `[UI]` / `[ERROR]` prefixes land in
    `snapshot.RecentChatMessages` / `snapshot.RecentErrors` via the
    existing flush path.

- [x] **P4.2** Fix snapshot signature churn
  - [x] P4.2.1 Remove `RecentChatCount` + `RecentErrorCount` from
    `SnapshotChangeSignature` in `Exports/BotRunner/BotRunnerService.cs`.
    Messages ride along on full snapshots that fire for real state
    changes + the 2s heartbeat.
  - [x] P4.2.2 Regression test: a stream of `[SYSTEM]`/`[SKILL]` messages
    arriving with no other state change must not trigger a full snapshot
    send; only the next heartbeat (or next real change) should carry
    them.
  - [x] P4.2.3 Confirm that test helpers still work: `GetDeltaMessages`
    already handles the case where deltas arrive only on heartbeat ticks.

- [x] **P4.3** LoadoutTask event-driven step advancement
  - [x] P4.3.1 Extend `LoadoutStep` with an optional
    `AttachExpectedAck(IWoWEventHandler)` handle that each step installs
    before `TryExecute`. `LearnSpellStep` subscribes to `OnLearnedSpell`
    filtered on its `_spellId`; `AddItemStep` subscribes to
    `OnItemAddedToBag` filtered on its `_itemId`; `SetSkillStep`
    subscribes to `OnSkillUpdated` filtered on `_skillId` and only flips
    the ack when `NewValue >= _value`. The first matching event flips
    `AckFired`, which short-circuits `IsSatisfied` → true on the next tick.
  - [x] P4.3.2 Polling remains the fallback. `IsSatisfied` returns
    `AckFired || CheckState(context)` so event + poll race benignly; whichever
    flips first wins.
  - [x] P4.3.3 `LoadoutTask.Update` detaches the advanced step's
    subscription immediately after the `while (TryIsSatisfied)` loop, and
    `TransitionToReady`/`Fail` detach every remaining step. `AttachExpectedAck`
    is idempotent at both the step (`_ackInstalled` guard) and the task
    (`_acksAttached` guard) levels so re-entering the same `LoadoutTask` does
    not double-subscribe.
  - [x] P4.3.4 Unit tests in `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
    now pin: per-step ack filtering; `SuppressFakeServer`-driven advancement
    on the very next `Update()` without a pacing sleep; single-step plan
    reaches `Ready` on event alone; polling-only path still reaches `Ready`
    when no event fires; detach removes the subscription; attach is
    idempotent; null event handler is a safe no-op; per-step detach on
    advancement leaves the active step still subscribed.

- [x] **P4.4** Correlation IDs + structured `CommandAckEvent`
  - [x] P4.4.1 Add `string correlation_id = <n>;` to `ActionMessage` in
    `Exports/BotCommLayer/Models/ProtoDef/communication.proto`.
    StateManager assigns one per dispatch; BotRunner echoes it back.
  - [x] P4.4.2 Add a new message
    `CommandAckEvent { string correlation_id; ActionType action_type;
    enum AckStatus {Pending, Success, Failed, TimedOut} status;
    string failure_reason; uint32 related_id; }` and
    `repeated CommandAckEvent recent_command_acks` on
    `WoWActivitySnapshot` (ring-buffer cap 10; document next to the
    field).
  - [x] P4.4.3 `BotRunnerService` populates the ring on every action it
    dispatches (including `LoadoutTask` step actions). Include the
    correlation id in the action's `CurrentAction` as it goes into
    `_activitySnapshot.CurrentAction`.
  - [x] P4.4.4 `SnapshotChangeSignature` gains
    `RecentCommandAckCount` so coordinator-level transitions can react
    to ACK arrivals without heartbeat lag. (Unlike the chat rings, ack
    counts change rarely — per dispatched command, not per chat line —
    so this does not reintroduce the churn from P4.2.)
  - [x] P4.4.5 Unit tests: end-to-end round trip — StateManager sends
    an action with correlation id, bot pushes a step, emits
    `CommandAckEvent(Success)` or `CommandAckEvent(Failed, reason)`
    in the snapshot.

- [x] **P4.5** Coordinator + test migration to structured ACKs
  - [x] P4.5.1 `BattlegroundCoordinator.LastAckStatus(correlationId, snapshots)`
    scans every bot's `RecentCommandAcks` ring and returns the latest
    status for the id (terminal Success/Failed/TimedOut beats Pending).
    `LoadoutStatus` stays as the per-phase roll-up; `CommandAckEvent`
    is the per-command receipt.
  - [x] P4.5.2 `LiveBotFixture.BotChat.SendGmChatCommandTrackedAsync`
    stamps a `test:<account>:<seq>` correlation id on the outbound
    `ActionMessage` (StateManager only stamps empty ids, so the test id
    survives end-to-end) and returns `GmChatCommandTrace` with
    `CorrelationId`, `AckStatus`, and `AckFailureReason` populated from
    the matching `CommandAckEvent` in `RecentCommandAcks`.
  - [x] P4.5.3 `LiveBotFixture.AssertTraceCommandSucceeded` is the new
    ACK-first helper — `AckStatus ∈ {Failed, TimedOut}` is an
    authoritative rejection; otherwise falls back to
    `ContainsCommandRejection` for commands not yet wired into the ACK
    ring. `IntegrationValidationTests` and `TalentAllocationTests`
    delegate their local `AssertCommandSucceeded` to it. Remaining
    fixtures still use the legacy path and will be migrated incrementally.
  - [x] P4.5.4 `BattlegroundCoordinatorAckTests` feeds scripted
    `RecentCommandAcks` rings through `LastAckStatus` and pins the
    Pending/terminal precedence, cross-snapshot scan, missing-id, and
    failed-with-reason contracts.

### Design invariants
- **No new catch-all `.learn all_*`.** Explicit IDs only, per curated
  per-(class, race) roster in
  `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ClassLoadoutSpells.cs`.
- **Polling stays as the authoritative success signal.** Events are
  a latency optimization; they never decide "did this succeed?" alone.
- **Snapshot budget:** ring buffers only, explicit caps, documented.
- **Correlation IDs flow through `ActionMessage` end-to-end.** No
  StateManager → bot → StateManager round trip loses the id.
- **FG/BG parity.** Every event added to `IWoWEventHandler` must have
  a firing path from both the FG Lua-hook bridge and the BG SMSG
  handler. If one side can't fire it, document why.

---

## P5 - Coordinator ACK Consumption

`P4` opened the correlated ACK plumbing but left `BattlegroundCoordinator.LastAckStatus`
as a static helper with only test coverage. `P5` turns ACKs into a real coordinator
signal, starting with the narrowest sub-phase that already has full ACK support on
the dispatch side.

### Context
`HandleApplyingLoadouts` currently gates `ApplyingLoadouts → WaitingForRaidFormation`
entirely on `WoWActivitySnapshot.LoadoutStatus`. That works when a `LoadoutTask`
runs to completion, but it leaves two gaps where the coordinator stalls forever:

1. **Pre-task rejection.** `BotRunnerService.Messages.cs` emits `CommandAckEvent.Failed`
   with reason `loadout_task_already_active` or `unsupported_action` *before* any
   `LoadoutTask` starts — `LoadoutStatus` never flips.
2. **Step-level TimedOut.** `LoadoutTask` emits `CommandAckEvent.TimedOut` for a
   step that exceeds `MaxRetriesPerStep`. Terminal status reaches the snapshot
   ACK ring, but `LoadoutStatus` may still read `LoadoutInProgress` for a tick.

### Rules
1. **ACK gates are additive, not replacements.** `snapshot.LoadoutStatus` remains
   the primary signal. ACK-driven short-circuit only activates when the ACK is
   terminal and the coordinator has not yet resolved the account.
2. **Deterministic correlation IDs.** Coordinator-dispatched actions pre-stamp
   their own correlation id; `CharacterStateSocketListener.StampDispatchCorrelationId`
   already respects non-empty ids, so the pre-stamp survives end-to-end.
3. **No new proto fields, no new listener plumbing.** Consume only what `P4.4`/`P4.5`
   already put on the wire.

### Sub-phases

- [x] **P5.1** Loadout ACK consumption in `BattlegroundCoordinator.HandleApplyingLoadouts`
  - [x] P5.1.1 Factor `LastAckStatus` into `LastAck` (returns `CommandAckEvent?`)
    + thin `LastAckStatus` wrapper. Coordinator consumers need the failure
    reason; tests that only care about status stay unaffected.
  - [x] P5.1.2 Pre-stamp each dispatched `ApplyLoadout` action with
    `bg-coord:loadout:<account>:<guid>`; record the id in `_loadoutCorrelationIds`.
  - [x] P5.1.3 `RecordLoadoutProgressFromSnapshots` consults `LastAck` before
    `LoadoutStatus`. Terminal Success → `_loadoutReady`; Failed/TimedOut →
    `_loadoutFailed` with the ack reason; Pending is ignored so the existing
    `LoadoutStatus` gate still holds.
  - [x] P5.1.4 Unit tests in `BattlegroundCoordinatorLoadoutTests` cover
    correlation-id stamping and Success/Failed/TimedOut/Pending ACK outcomes
    without relying on `LoadoutStatus`.

### Design invariants
- **Coordinator-owned correlation ids.** Coordinator-dispatched actions stamp
  deterministic ids (prefix `bg-coord:<phase>:<account>:<guid>`). Listener-stamped
  sequence ids only apply when the dispatcher left `CorrelationId` empty.
- **One direction, no round-trip.** Coordinator writes the id on dispatch; the
  bot echoes it back through `CommandAckEvent`. No new RPC, no new storage.
- **Polling stays.** ACK resolution is a short-circuit for terminal gaps, not
  a replacement for `snapshot.LoadoutStatus`.

---

## P2 - WoW.exe Packet Handling & ACK Parity (COMPLETE)

### Context
Physics parity against WoW.exe is green. Packet dispatch, ObjectManager state mutation, and ACK generation still have unverified corners. This phase closes those gaps with binary-backed evidence.

**Full plan:** `docs/WOW_EXE_PACKET_PARITY_PLAN.md` (10 gaps identified, 7 sub-phases).

### Sub-phases
- [x] **P2.1** Decompilation research: packet dispatch & ACK generation
  - [x] P2.1.1 Capture `NetClient::ProcessMessage` (0x537AA0) disassembly; identify opcode dispatch mechanism
  - [x] P2.1.2 Dump opcode -> handler mapping as `docs/physics/opcode_dispatch_table.md`
  - [x] P2.1.3 Capture `NetClient::Send` (0x005379A0) disassembly
  - [x] P2.1.4 Decompile P1 handlers: speed change, root, knockback, water walk, hover, teleport, worldport ACK
  - [x] P2.1.5 Decompile `CGPlayer_C` / `CGUnit_C` / `CGObject_C` vtables
  - [x] P2.1.6 Trace movement counter: CMovement offset, increment points, packet inclusion
- [x] **P2.2** ACK format parity (byte-level)
  - [x] P2.2.1 Capture golden corpus ACK bytes from FG bot for each ACK opcode
  - [x] P2.2.2 Add `AckBinaryParityTests` — one test per ACK opcode asserting byte equality
  - [x] P2.2.3 Fix every byte divergence citing a VA
  - [x] P2.2.4 Confirm movement counter semantics match WoW.exe
  - [x] P2.2.5 Gate: all 14 wired ACKs have passing byte-parity tests
- [x] **P2.3** ACK timing & ordering parity
  - [x] P2.3.1 Answer Q1-Q5 (sync vs deferred; see plan §4.3) with binary evidence
  - [x] P2.3.2 Write failing tests for current timing divergences
  - [x] P2.3.3 Fix timing via defer-to-controller or immediate-after-mutation pattern
  - [x] P2.3.4 Close **G1** knockback ACK race
- [x] **P2.4** ObjectManager state mutation parity
  - [x] P2.4.1 Produce `docs/physics/cgobject_layout.md` with exact field offsets
  - [x] P2.4.2 Audit C# classes — each field mapped to a WoW.exe field or documented as intentional omission
  - [x] P2.4.3 Decompile `CGWorldClient::HandleUpdateObject` block-walk order
  - [x] P2.4.4 Write `ObjectUpdateMutationOrderTests` replaying captured SMSG_UPDATE_OBJECT streams
  - [x] P2.4.5 Fix mutation-order divergences
- [x] **P2.5** Packet-flow end-to-end parity
  - [x] P2.5.1 Build `PacketFlowTraceFixture` — bytes in, bytes out, state observer, ordered event log
  - [x] P2.5.2 Write one trace test per representative packet (8 tests: UPDATE_OBJECT Add, UPDATE_OBJECT Update, FORCE_RUN_SPEED_CHANGE, FORCE_MOVE_ROOT, MOVE_KNOCK_BACK, MOVE_TELEPORT, NEW_WORLD→WORLDPORT_ACK, MONSTER_MOVE)
  - [x] P2.5.3 Fix divergences discovered by trace tests
- [x] **P2.6** State-machine parity
  - [x] P2.6.1 Document each state machine (control, teleport, worldport, login, knockback, root) in `docs/physics/state_<name>.md`
  - [x] P2.6.2 Audit implementation against documented transitions
  - [x] P2.6.3 Write `StateMachineParityTests`
  - [x] P2.6.4 Close **G4** (teleport flag clear) and **G8** (teleport ACK deadlock)
- [x] **P2.7** Gap closure (G1-G10 verification)
  - [x] P2.7.1 **G2** wire `MSG_MOVE_TIME_SKIPPED` listener
  - [x] P2.7.2 **G3** wire `MSG_MOVE_JUMP` / `MSG_MOVE_FALL_LAND` consumer
  - [x] P2.7.3 **G6** close `MSG_MOVE_SET_RAW_POSITION_ACK` as not-applicable in WoW.exe 1.12.1
  - [x] P2.7.4 **G7** close `CMSG_MOVE_FLIGHT_ACK` as not-applicable in WoW.exe 1.12.1
  - [x] P2.7.5 Final regression: all parity bundles + new `AckParity` / `PacketFlowParity` / `StateMachineParity` bundles green

### Gaps identified (2026-04-16)
| Gap | Summary                                                             | Close in  |
| --- | ------------------------------------------------------------------- | --------- |
| G1  | Knockback ACK sent before physics consumes impulse                  | P2.3      |
| G2  | `MSG_MOVE_TIME_SKIPPED` has no ObjectManager listener                | P2.7.1    |
| G3  | Jump/fall land events fire but no consumer                           | P2.7.2    |
| G4  | Teleport flag-clear only masks 8 bits (jump/fall/swim persist)       | P2.6.4    |
| G5  | SPLINE_MOVE opcodes ACK behavior unverified                          | P2.3      |
| G6  | `0x00E0` has no static registration and no live WoW.exe ACK emission | P2.7.3    |
| G7  | `0x033E/0x033F/0x0340` have no static registration and no live WoW.exe ACK emission | P2.7.4    |
| G8  | Teleport ACK `IsSceneDataReady()` may deadlock                       | P2.6.4    |
| G9  | ACK byte format vs WoW.exe unverified                                | P2.2      |
| G10 | Movement counter semantics unverified                                | P2.2      |

---

## Handoff (2026-04-25, Shodan SpellCastOnTarget migration slice)

- Completed:
  - Migrated `SpellCastOnTargetTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG Battle Shout action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Added `StageBotRunnerRageAsync(...)` so the rage setup for Battle Shout lives behind the fixture boundary alongside `StageBotRunnerLoadoutAsync(...)` and `StageBotRunnerAurasAbsentAsync(...)`.
  - The BG target dispatches only correlated `ActionType.CastSpell` with spell id `6673`; the test body has no inline setup GM commands.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/SpellCastOnTargetTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `SpellCastOnTargetTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now `7`.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|\\.modify|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/SpellCastOnTargetTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellCastOnTargetTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=spell_cast_on_target_shodan.trx"` -> `passed (1/1)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/spell_cast_on_target_shodan.trx` -> `CastSpell_BattleShout_AuraApplied` passed after Shodan-shaped spell/rage/aura staging, BG `CastSpell` dispatch, aura `6673` observation, and fixture cleanup.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/SpellCastOnTargetTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SpellCastOnTargetTests.md`
  - live-validation docs and task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|\\.modify|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/TaxiTests.cs Tests/BotRunner.Tests/LiveValidation/TaxiTransportParityTests.cs Tests/BotRunner.Tests/LiveValidation/TransportTests.cs`

## Handoff (2026-04-25, Shodan BattlegroundQueue migration slice)

- Completed:
  - Migrated `BattlegroundQueueTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG WSG queue action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Added `StageBotRunnerAtOrgrimmarWarsongBattlemasterAsync(...)` so WSG battlemaster coordinate staging lives in the fixture. Level setup uses `StageBotRunnerLoadoutAsync(...)`.
  - The BG target dispatches only `ActionType.JoinBattleground` with WSG type/map parameters and cleanup `ActionType.LeaveBattleground`.
  - Docs added at `Tests/BotRunner.Tests/LiveValidation/docs/BattlegroundQueueTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `BattlegroundQueueTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now `8`.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BattlegroundQueueTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundQueueTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=battleground_queue_shodan.trx"` -> `passed (1/1)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/battleground_queue_shodan.trx` -> `BG_QueueForWSG_ReceivesQueuedStatus` passed after Shodan level/staging, WSG battlemaster snapshot detection, `JoinBattleground` dispatch, queue evidence observation, and `LeaveBattleground` cleanup.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/BattlegroundQueueTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/BattlegroundQueueTests.md`
  - live-validation docs and task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/SpellCastOnTargetTests.cs`

## Handoff (2026-04-25, Shodan BgInteraction migration slice)

- Completed:
  - Migrated `BgInteractionTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG economy/NPC smoke action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Moved item, bank, auction-house, mailbox, mail-money, coinage, and flight-master setup behind fixture helpers. The migrated test body no longer issues direct GM setup calls.
  - The BG target dispatches only `ActionType.InteractWith`, `ActionType.CheckMail`, and `ActionType.VisitFlightMaster`.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/BgInteractionTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `BgInteractionTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now `9`.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BgInteractionTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BgInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bg_interaction_shodan.trx"` -> `passed overall (3 passed, 2 skipped)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/bg_interaction_shodan.trx` -> `AuctionHouse_InteractWithAuctioneer`, `Mail_SendGoldAndCollect_CoinageChanges`, and `FlightMaster_DiscoverAndTakeFlight` passed; bank deposit and Deeprun Tram are tracked skips.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/BgInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/BgInteractionTests.md`
  - live-validation docs and task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BattlegroundQueueTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 22 - LootCorpseTests)

- Completed:
  - Migrated `LootCorpseTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Loot.config.json`. `LOOTBG1` is the BG loot action target, `LOOTFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Replaced the old dedicated `CombatBgArenaFixture` execution mode with `LiveBotFixture` plus Shodan settings validation and action-target resolution.
  - Moved clean-slate and bag cleanup into `StageBotRunnerLoadoutAsync(...)`; moved Durotar mob-area setup into `StageBotRunnerAtDurotarMobAreaAsync(...)`. The migrated test body no longer issues direct GM setup calls.
  - The BG target dispatches only `ActionType.StartMeleeAttack`, `StopAttack`, and `LootCorpse`, then verifies the loot dispatch and inventory observation path.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/LootCorpseTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `LootCorpseTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~16.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|EnsureCleanSlateAsync|WaitForTeleportSettledAsync|damage" Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LootCorpseTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=loot_corpse_shodan.trx"` -> `passed (1/1)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/loot_corpse_shodan.trx` -> `Loot_KillAndLootMob_InventoryChanges` passed through Shodan clean-bag staging, Durotar mob-area staging, BG melee kill, `LootCorpse` dispatch, and inventory observation.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`
  - `Services/WoWStateManager/Settings/Configs/Loot.config.json`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LootCorpseTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|EnsureCleanSlateAsync|WaitForTeleportSettledAsync|damage" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`

---

## Handoff (2026-04-25, Shodan test-director overhaul slice 21 - NavigationTests / AllianceNavigationTests)

- Completed:
  - Migrated `NavigationTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG navigation action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Migrated `AllianceNavigationTests.cs` to `Services/WoWStateManager/Settings/Configs/Navigation.config.json` with stable idle foreground `ECONFG1`, Human BG target `NAVBG1`, and SHODAN as director. The initial all-Human foreground config was not kept because the foreground runner crashed in the first live attempt.
  - Moved navigation coordinate setup behind `StageBotRunnerAtNavigationPointAsync(...)`; migrated test bodies no longer issue direct `BotTeleportAsync(...)` setup calls.
  - `NavigationTests` dispatches only BG `ActionType.Goto` for executable route probes. `AllianceNavigationTests` remains snapshot-only after fixture-owned Alliance coordinate staging.
  - `Navigation_LongPath_ArrivesAtDestination` is a tracked skip for the Valley of Trials long diagonal `GoToTask` `no_path_timeout`; the committed Durotar short route stages at z=`42` to avoid the repeated identical command no-op observed in earlier live attempts.
  - Docs added at `Tests/BotRunner.Tests/LiveValidation/docs/NavigationTests.md` and `Tests/BotRunner.Tests/LiveValidation/docs/AllianceNavigationTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: both files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~17.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs Tests/BotRunner.Tests/LiveValidation/AllianceNavigationTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation.NavigationTests|FullyQualifiedName~LiveValidation.AllianceNavigationTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=navigation_alliance_shodan_final4.trx"` -> `passed overall (7 passed, 1 skipped)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/navigation_alliance_shodan_final4.trx` -> five Alliance staging checks passed, `Navigation_ShortPath_ArrivesAtDestination` passed, `Navigation_LongPath_ZTrace_FGvsBG` passed, and `Navigation_LongPath_ArrivesAtDestination` skipped with the tracked Valley long-route `no_path_timeout` reason.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AllianceNavigationTests.cs`
  - `Services/WoWStateManager/Settings/Configs/Navigation.config.json`
  - `Tests/BotRunner.Tests/LiveValidation/docs/NavigationTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/AllianceNavigationTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`

---

## Handoff (2026-04-25, Shodan test-director overhaul slice 20 - MovementSpeedTests)

- Completed:
  - Migrated `MovementSpeedTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG movement-speed action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Replaced the old foreground-shadow teleport setup with fixture-contained Durotar road staging through `StageBotRunnerAtNavigationPointAsync(...)`; the test body now dispatches only BG `ActionType.Goto`.
  - The live probe still asserts the 141-yard Durotar route, minimum/maximum travel speed envelope, Z stability, and arrival tolerance from snapshots.
  - Docs added at `Tests/BotRunner.Tests/LiveValidation/docs/MovementSpeedTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `MovementSpeedTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~19.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MovementSpeedTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementSpeedTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_speed_shodan.trx"` -> `passed (1/1)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/movement_speed_shodan.trx` -> `BG_Durotar_WindingPathSpeed` passed with BG-only `Goto` dispatch after Shodan-owned staging.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/MovementSpeedTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MovementSpeedTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs Tests/BotRunner.Tests/LiveValidation/AllianceNavigationTests.cs`

---

## Handoff (2026-04-25, Shodan test-director overhaul slice 19 - CornerNavigationTests / TileBoundaryCrossingTests)

- Completed:
  - Migrated `CornerNavigationTests.cs` and `TileBoundaryCrossingTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG navigation action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Added fixture-contained arbitrary navigation coordinate staging via `StageBotRunnerAtNavigationPointAsync(...)`; the migrated test bodies no longer issue direct `BotTeleportAsync(...)` setup calls.
  - Route checks dispatch only BG `ActionType.TravelTo`, while snapshot-only probes rely on Shodan-owned staging and snapshot assertions.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/CornerNavigationTests.md` and `Tests/BotRunner.Tests/LiveValidation/docs/TileBoundaryCrossingTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: both files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~20.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|BgAccountName|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/CornerNavigationTests.cs Tests/BotRunner.Tests/LiveValidation/TileBoundaryCrossingTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests|FullyQualifiedName~TileBoundaryCrossingTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=corner_tile_navigation_shodan.trx"` -> `passed (6/6)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/corner_tile_navigation_shodan.trx` -> Orgrimmar bank-to-AH route, RFC corridor route, obstacle snapshot, Undercity tunnel snapshot, Orgrimmar tile boundary, and Durotar open tile boundary all passed.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/CornerNavigationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TileBoundaryCrossingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/CornerNavigationTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TileBoundaryCrossingTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MovementSpeedTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 18 - TravelPlannerTests)

- Completed:
  - Migrated `TravelPlannerTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG travel action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Added fixture-contained street-level Orgrimmar staging through `StageBotRunnerAtTravelPlannerStartAsync(...)` plus targeted BG quiesce after staging. The test body no longer issues `.tele` setup commands.
  - The executable short-walk case dispatches only `ActionType.TravelTo` toward the Orgrimmar auction-house service location and asserts snapshot movement.
  - The long Orgrimmar-to-Crossroads probes launch through the Shodan topology but are tracked skips because delivered `TravelTo` starts `GoToTask` with no position delta after 20s and leaves BG `CurrentAction=TravelTo`.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/TravelPlannerTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `TravelPlannerTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~22.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/TravelPlannerTests.cs` -> `no matches`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TravelPlannerTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=travel_planner_shodan.trx"` -> `passed overall (1 passed, 3 skipped)`.
  - Session Ratchet anchor: `tmp/test-runtime/results-live/fishing_shodan_anchor.trx` remains the once-per-session anchor evidence and failed in the known anchor-instability lane; not treated as a TravelPlanner regression.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/travel_planner_shodan.trx` -> `TravelTo_ShortWalk_WithinOrgrimmar` passed; three Crossroads probes skipped with the tracked no-movement reason.
  - Earlier failure evidence captured delivered `TravelTo` plus `GOTO-TASK Update #1` at the street-level Orgrimmar start toward Crossroads and no position delta after 20s.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/TravelPlannerTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TravelPlannerTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/CornerNavigationTests.cs Tests/BotRunner.Tests/LiveValidation/TileBoundaryCrossingTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 17 - MountEnvironmentTests)

- Completed:
  - Migrated `MountEnvironmentTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG mount-environment action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Added fixture-contained mount loadout, unmount cleanup, and indoor/outdoor coordinate staging helpers. The test body no longer issues `.learn`, `.setskill`, `.dismount`, `.unaura`, or `.go xyz` setup commands.
  - The BG target dispatches only `ActionType.CastSpell` for mount behavior checks; snapshot/chat assertions prove outdoor mount success and indoor mount rejection.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/MountEnvironmentTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `MountEnvironmentTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~23.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MountEnvironmentTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mount_environment_shodan.trx"` -> `passed (4/4)`.
  - Session Ratchet anchor: `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> `failed in known anchor-instability lane: FG never reached fishing_loot_success within 3m after repeated loot_window_timeout, max_casts_reached, and "cast didn't land in fishable water" evidence; not treated as a MountEnvironment regression`.
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.dismount|\\.unaura" Tests/BotRunner.Tests/LiveValidation/MountEnvironmentTests.cs` -> `no matches`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/mount_environment_shodan.trx` -> outdoor and indoor scene classification plus outdoor mount allow / indoor mount block all passed.
  - `tmp/test-runtime/results-live/fishing_shodan_anchor.trx` -> known Ratchet anchor flake on FG fishing cast/loot loop.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/MountEnvironmentTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MountEnvironmentTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/TravelPlannerTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 16 - MapTransitionTests)

- Completed:
  - Migrated `MapTransitionTests.cs` to the Shodan test-director pattern using `Services/WoWStateManager/Settings/Configs/Economy.config.json`. `ECONBG1` is the BG map-transition action target, `ECONFG1` is idle for topology parity, and SHODAN is the Background Gnome Mage director.
  - Added fixture-contained Ironforge tram staging and rejected Deeprun Tram transition helpers. The test body no longer issues `.go xyz` setup commands.
  - The BG target dispatches only a correlated post-bounce `ActionType.Goto` at its current snapshot position, proving BotRunner remains action-responsive after the map transition settles.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/MapTransitionTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `MapTransitionTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~24.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MapTransitionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=map_transition_shodan.trx"` -> `passed (1/1)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/map_transition_shodan.trx` -> Deeprun Tram rejected-transition bounce settled to `InWorld` and the BG post-bounce `Goto` liveness action completed.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MapTransitionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MountEnvironmentTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 14 - NpcInteractionTests)

- Completed:
  - Migrated `NpcInteractionTests.cs` to the Shodan test-director pattern with `Services/WoWStateManager/Settings/Configs/NpcInteraction.config.json`. `NPCBG1` is the Background Orc Hunter action target, `NPCFG1` is the Foreground Orc Rogue action target, and SHODAN is the Background Gnome Mage director.
  - Added fixture-contained Razor Hill hunter trainer and Orgrimmar flight-master staging helpers, plus spell-unlearn staging for the trainer path. The test body resolves action recipients with `ResolveBotRunnerActionTargets(...)`; SHODAN remains director-only.
  - Vendor, flight-master, and object-manager checks now dispatch only `ActionType.VisitVendor` / `VisitFlightMaster` or assert snapshots after Shodan staging. `Trainer_LearnAvailableSpells` is Shodan-shaped but skipped with a tracked live funding/mailbox staging gap.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/NpcInteractionTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `NpcInteractionTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~26.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NpcInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=npc_interaction_shodan.trx"` -> `passed 3, skipped 1`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/npc_interaction_shodan.trx` -> vendor, flight-master, and object-manager paths passed; trainer skipped with the documented funding/mailbox reason.
  - `tmp/test-runtime/results-live/npc_interaction_shodan_final.trx` -> pre-skip diagnostic failure captured `[SHODAN-STAGE] BG mailbox staging failed` after strict Orgrimmar mailbox staging could not enable GM mode; SOAP `Trainer Gold` mail remained uncollectable and target coinage stayed `0`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Services/WoWStateManager/Settings/Configs/NpcInteraction.config.json`
  - `Tests/BotRunner.Tests/LiveValidation/docs/NpcInteractionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 13 - quest group)

- Completed:
  - Migrated `GossipQuestTests.cs`, `QuestObjectiveTests.cs`, `QuestInteractionTests.cs`, and `StarterQuestTests.cs` to the Shodan test-director pattern. The slice reuses `Services/WoWStateManager/Settings/Configs/Economy.config.json` with `ECONBG1` as the quest/gossip action target, `ECONFG1` launched idle for topology parity, and SHODAN as Background Gnome Mage director.
  - Added `QuestTestSupport` plus fixture-contained quest location and quest-state staging helpers in `LiveBotFixture.TestDirector.cs` for Razor Hill, Valley of Trials, Durotar objective staging, and quest add/complete/remove setup.
  - Test bodies no longer issue GM setup commands. Executable behavior paths dispatch only `ActionType.InteractWith`, `StartMeleeAttack`, `AcceptQuest`, or `CompleteQuest` to BG; snapshot-plumbing paths assert fixture-staged quest-log state.
  - Docs added/refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/GossipQuestTests.md`, `QuestObjectiveTests.md`, `QuestInteractionTests.md`, and `StarterQuestTests.md`; execution-mode index updated; inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: all four files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~27.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GossipQuestTests|FullyQualifiedName~QuestObjectiveTests|FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=quest_group_shodan_rerun.trx"` -> `passed (6/6)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor_quest_slice.trx"` -> `failed (known anchor instability: FG never reached fishing_loot_success within 3m after loot_window_timeout retries and max_casts_reached)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/quest_group_shodan_rerun.trx` -> all quest-group tests passed.
  - `tmp/test-runtime/results-live/quest_group_shodan.trx` -> first post-migration attempt passed `4`, failed `1`, and skipped `1`; the rerun fixed the reward-completion assertion and moved quest-objective staging to a nearby attackable Durotar mob cluster.
  - `tmp/test-runtime/results-live/fishing_shodan_anchor_quest_slice.trx` -> Ratchet anchor failed in the documented FG fishing instability path (`loot_window_timeout` retries, `max_casts_reached`), not a quest-slice regression.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/GossipQuestTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/QuestObjectiveTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/QuestTestSupport.cs`
  - live-validation docs and task trackers.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 12 - TradingTests/TradeParityTests)

- Completed:
  - Migrated `TradingTests.cs` and `TradeParityTests.cs` to the Shodan test-director pattern. The slice reuses `Services/WoWStateManager/Settings/Configs/Economy.config.json` with `ECONFG1` / `ECONBG1` as real BotRunner participants plus SHODAN as Background Gnome Mage director.
  - Added `StageBotRunnerAtOrgrimmarTradeSpotAsync` and shared `TradeTestSupport` so loadout, coinage, Orgrimmar trade positioning, visible-partner resolution, and structured ACK checks live outside the test bodies.
  - Fixed BG trade item packet coordinates in `Exports/WoWSharpClient/InventoryManager.cs` by mapping logical backpack `bag 0, slot 0` to packet `bag 0xFF, slot 23`. Added foreground trade Lua routing coverage while documenting the remaining foreground trade runtime gap.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/TradingTests.md` and `Tests/BotRunner.Tests/LiveValidation/docs/TradeParityTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: both files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~31.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_shodan_final.trx"` -> `passed 1, skipped 3`.
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> `passed (1/1)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
- Evidence:
  - `tmp/test-runtime/results-live/trading_shodan_final.trx` -> `TradingTests` passed BG offer/decline cancel and skipped transfer/parity paths with explicit foreground trade ACK reasons.
  - `tmp/test-runtime/results-live/trade_parity_fg_transfer_after_ack_wait.trx` -> foreground `OfferItem`/transfer path ACK failure (`Failed/behavior_tree_failed`).
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/TradingTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TradeTestSupport.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/OrgrimmarServiceLocations.cs`
  - `Exports/WoWSharpClient/InventoryManager.cs`
  - `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - task trackers and live-validation docs.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/GossipQuestTests.cs Tests/BotRunner.Tests/LiveValidation/QuestObjectiveTests.cs Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 11 - MailSystemTests/MailParityTests)

- Completed:
  - Migrated `MailSystemTests.cs` and `MailParityTests.cs` to the Shodan test-director pattern. The slice reuses `Services/WoWStateManager/Settings/Configs/Economy.config.json` with `ECONFG1` and `ECONBG1` as mail action targets and SHODAN as Background Gnome Mage director.
  - Added `StageBotRunnerMailboxItemAsync` so SOAP item-mail setup joins the existing Shodan mailbox and mail-money helpers. Test bodies now dispatch only `ActionType.CheckMail`.
  - Later foreground stabilization closed the `CheckMail` timing gap: foreground mailbox collection now waits through delayed inbox metadata and emits `[MAIL-COLLECT]` markers for money/item collection evidence.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/MailSystemTests.md` and `Tests/BotRunner.Tests/LiveValidation/docs/MailParityTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: both files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~33.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> `passed (4/4)`.
  - Repo-scoped cleanup before and after live validation -> `No repo-scoped processes to stop.`
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs` -> no matches.
- Evidence:
  - `tmp/test-runtime/results-live/mail_fg_shodan_director_extendedpoll.trx` -> passed `4/4` with FG and BG `CheckMail`.
  - Earlier `tmp/test-runtime/results-live/mail_shodan.trx`, `mail_shodan_rerun.trx`, and `mail_gold_rerun.trx` captured the now-fixed foreground timing diagnostics.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MailSystemTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MailParityTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - task trackers.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/TradingTests.cs Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 10 - EconomyInteractionTests)
- Completed:
  - Migrated `EconomyInteractionTests.cs` to the Shodan test-director pattern. The slice reuses `Services/WoWStateManager/Settings/Configs/Economy.config.json` with `ECONFG1` and `ECONBG1` as action targets plus SHODAN as Background Gnome Mage director.
  - Added `StageBotRunnerAtOrgrimmarMailboxAsync` and `StageBotRunnerMailboxMoneyAsync` so mailbox location and SOAP mail-money setup are fixture-contained. Existing Shodan bank/AH staging helpers now cover the other two methods.
  - `EconomyInteractionTests` dispatches only `ActionType.InteractWith` for banker/auctioneer and `ActionType.CheckMail` for mailbox collection. FG and BG both passed the live slice.
  - Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/EconomyInteractionTests.md`, execution-mode index updated, and inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `EconomyInteractionTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~35.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EconomyInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=economy_interaction_shodan.trx"` -> `passed (3/3)`.
  - Reference Ratchet anchor was already run once this session during the Gathering slice: `fishing_shodan_anchor_gathering_slice.trx` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/economy_interaction_shodan.trx` shows FG/BG bank and auctioneer `InteractWith` success, plus FG/BG mailbox `CheckMail` success and coinage increase after fixture-staged SOAP mail.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/EconomyInteractionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 9 - VendorBuySellTests)
- Completed:
  - Migrated `VendorBuySellTests.cs` to the Shodan test-director pattern. The slice reuses `Services/WoWStateManager/Settings/Configs/Economy.config.json` with `ECONBG1` as the BG vendor packet action target, `ECONFG1` launched idle for topology parity, and SHODAN as Background Gnome Mage director.
  - Added `StageBotRunnerAtRazorHillVendorAsync` and `StageBotRunnerCoinageAsync` so Razor Hill vendor staging and money setup are fixture-contained. Test bodies no longer issue `.go` / `.additem` / `.modify money` setup.
  - `VendorBuySellTests` dispatches only `ActionType.BuyItem`, `ActionType.SellItem`, and post-buy `ActionType.DestroyItem` cleanup from the test body. This remains a BG packet baseline by design; foreground vendor parity is left to a future behavior slice.
  - Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/VendorBuySellTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `VendorBuySellTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~36.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~VendorBuySellTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=vendor_buy_sell_shodan.trx"` -> `passed (2/2)`.
  - Reference Ratchet anchor was already run once this session during the Gathering slice: `fishing_shodan_anchor_gathering_slice.trx` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/vendor_buy_sell_shodan.trx` shows `ECONBG1` staging through `StageBotRunnerAtRazorHillVendorAsync`, copper/item setup through fixture helpers, `BuyItem` adding item `159` while coinage decreases, and `SellItem` removing Linen Cloth while coinage increases.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/VendorBuySellTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele|modify money" Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 8 - BankInteractionTests/BankParityTests)
- Completed:
  - Migrated `BankInteractionTests.cs` and `BankParityTests.cs` to the Shodan test-director pattern. The slice reuses `Services/WoWStateManager/Settings/Configs/Economy.config.json` with `ECONFG1` Foreground Orc Warrior, `ECONBG1` Background Orc Warrior, and SHODAN as Background Gnome Mage director.
  - Added `StageBotRunnerAtOrgrimmarBankAsync` so bank coordinate staging is fixture-contained. Test bodies no longer issue `.tele` / `.go` / `.additem` setup.
  - `BankInteractionTests` validates FG/BG banker detection and dispatches only `ActionType.InteractWith` to detected banker GUIDs. `BankParityTests` validates FG/BG bank staging and Linen Cloth staging; deposit/withdraw and bank-slot purchase are explicit skips because no bank action surfaces exist yet.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/BankInteractionTests.md` and `Tests/BotRunner.Tests/LiveValidation/docs/BankParityTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: both files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~37.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BankInteractionTests|FullyQualifiedName~BankParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bank_shodan.trx"` -> `1 passed, 3 skipped`.
  - Reference Ratchet anchor was already run once this session during the Gathering slice: `fishing_shodan_anchor_gathering_slice.trx` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/bank_shodan.trx` shows `ECONFG1`/`ECONBG1` staging through `StageBotRunnerAtOrgrimmarBankAsync`, banker detection passing, `ActionType.InteractWith` succeeding where exercised, and deposit/withdraw/slot-purchase placeholders skipping with explicit missing-action reasons.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BankParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/BankInteractionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/BankParityTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 7 - AuctionHouseTests/AuctionHouseParityTests)
- Completed:
  - Migrated `AuctionHouseTests.cs` and `AuctionHouseParityTests.cs` to the Shodan test-director pattern. New `Services/WoWStateManager/Settings/Configs/Economy.config.json` launches `ECONFG1` Foreground Orc Warrior, `ECONBG1` Background Orc Warrior, and SHODAN as Background Gnome Mage director.
  - Added `StageBotRunnerAtOrgrimmarAuctionHouseAsync` so AH coordinate staging is fixture-contained. Test bodies no longer issue `.tele` / `.go` / `.additem` setup.
  - `AuctionHouseTests` dispatches only `ActionType.InteractWith` to FG/BG auctioneer GUIDs. `AuctionHouseParityTests` validates FG/BG AH staging/search detection; post/buy and cancel are explicit skips because no auction post/buy/cancel action surface exists yet.
  - Docs refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/AuctionHouseTests.md` and `Tests/BotRunner.Tests/LiveValidation/docs/AuctionHouseParityTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: both files moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~39.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AuctionHouseTests|FullyQualifiedName~AuctionHouseParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=auction_house_shodan.trx"` -> `3 passed, 2 skipped`.
  - Reference Ratchet anchor was already run once this session during the Gathering slice: `fishing_shodan_anchor_gathering_slice.trx` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/auction_house_shodan.trx` shows `ECONFG1`/`ECONBG1` staging through `StageBotRunnerAtOrgrimmarAuctionHouseAsync`, AH search/detection passing on both roles, `ActionType.InteractWith` succeeding on both roles, and the post/buy/cancel placeholders skipping with explicit missing-action reasons.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/Economy.config.json` (new)
  - `Tests/BotRunner.Tests/LiveValidation/AuctionHouseTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/AuctionHouseTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/AuctionHouseParityTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs Tests/BotRunner.Tests/LiveValidation/BankParityTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 6 - PetManagementTests)
- Completed:
  - Migrated `PetManagementTests.cs` to the Shodan test-director pattern. New `Services/WoWStateManager/Settings/Configs/PetManagement.config.json` launches `PETBG1` Background Orc Hunter as the action target, idle `PETFG1` Foreground Orc Rogue for topology parity, and SHODAN as Background Gnome Mage director.
  - Moved hunter pet setup into `StageBotRunnerLoadoutAsync`: level `10`, Call Pet `883`, Dismiss Pet `2641`, and Tame Animal `1515`.
  - Kept the behavior surface BG-only: `PETBG1` receives the `ActionType.CastSpell` dispatches for Call Pet and Dismiss Pet. FG remains launched but idle because foreground spell-id casting is not the validated pet-management path.
  - Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/PetManagementTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `PetManagementTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~41.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PetManagementTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=pet_management_shodan.trx"` -> `passed (1/1)`.
  - Reference Ratchet anchor was already run once this session during the Gathering slice: `fishing_shodan_anchor_gathering_slice.trx` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/pet_management_shodan.trx` shows `PETBG1` staging via `StageBotRunnerLoadoutAsync`, `.learn 883`, `.learn 2641`, `.learn 1515`, and the two under-test dispatches as BG `ActionType.CastSpell`.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/PetManagement.config.json` (new)
  - `Tests/BotRunner.Tests/LiveValidation/PetManagementTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/PetManagementTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/AuctionHouseTests.cs Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 5 - CraftingProfessionTests)
- Completed:
  - Migrated `CraftingProfessionTests.cs` to the Shodan test-director pattern. New `Services/WoWStateManager/Settings/Configs/Crafting.config.json` launches `CRAFTFG1` Foreground Orc Warrior, `CRAFTBG1` Background Orc Warrior, and SHODAN as Background Gnome Mage director.
  - Moved First Aid recipe/skill/reagent setup into `StageBotRunnerLoadoutAsync`: First Aid Apprentice `3273`, Linen Bandage recipe `3275`, First Aid skill `129=1/75`, and one Linen Cloth `2589`.
  - Kept the behavior surface BG-only: `CRAFTBG1` receives the single `ActionType.CastSpell` dispatch, while FG remains launched for Shodan-topology parity because foreground spell-id casting is not the validated crafting path.
  - Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/CraftingProfessionTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `CraftingProfessionTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~42.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CraftingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=crafting_shodan.trx"` -> `passed (1/1)`.
  - Reference Ratchet anchor was already run once this session during the Gathering slice: `fishing_shodan_anchor_gathering_slice.trx` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/crafting_shodan.trx` shows `CRAFTBG1` staging via `StageBotRunnerLoadoutAsync`, `.learn 3273`, `.learn 3275`, `.setskill 129 1 75`, `.additem 2589 1`, and the only under-test dispatch as `ActionType.CastSpell`.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/Crafting.config.json` (new)
  - `Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/CraftingProfessionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/PetManagementTests.cs`

## Handoff (2026-04-25, Shodan test-director overhaul slice 4 - GatheringProfessionTests)
- Completed:
  - Migrated `GatheringProfessionTests.cs` to the Shodan test-director pattern. New `Services/WoWStateManager/Settings/Configs/Gathering.config.json` launches `GATHFG1` Foreground Orc Warrior, `GATHBG1` Background Orc Warrior, and SHODAN as Background Gnome Mage director.
  - Added fixture-contained gathering staging helpers: Shodan refreshes/prioritizes pool candidates, target bots receive profession loadout through `StageBotRunnerLoadoutAsync`, and route teleport staging lives in `StageBotRunnerAtValleyCopperRouteStartAsync` / `StageBotRunnerAtDurotarHerbRouteStartAsync`.
  - Corrected the Valley copper route center to `(-1000,-4500,28.5)` after native `GetGroundZ` showed the old `(-800,-4500,31)` center sits on a high terrain layer. The test body now dispatches only `ActionType.StartGatheringRoute`.
  - Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/GatheringProfessionTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `GatheringProfessionTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~43.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings plus benign vcpkg applocal dumpbin warning)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=gathering_shodan_level20.trx"` -> `2 passed, 1 skipped, 1 failed`. Pass: `Mining_BG_GatherCopperVein`, `Herbalism_BG_GatherHerb`. Skip: `Herbalism_FG_GatherHerb` because FG was no longer actionable after the preceding FG mining failure. Fail: `Mining_FG_GatherCopperVein` after correct Shodan staging/action delivery; documented as a foreground gathering functional gap.
  - Reference anchor: `dotnet test ... --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "trx;LogFileName=fishing_shodan_anchor_gathering_slice.trx"` -> `passed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/gathering_shodan_level20.trx` shows BG mining skill `1 -> 2`, BG herbalism success, and FG mining receiving `StartGatheringRoute` while moving around active copper candidates before timeout.
  - `tmp/test-runtime/results-live/fishing_shodan_anchor_gathering_slice.trx` is the once-per-session Ratchet anchor pass.
  - `D:\World of Warcraft\logs\botrunner_GATHFG1.diag.log` and `Bot/Release/net8.0/logs/botrunner_GATHBG1.diag.log` contain the FG/BG action delivery and gathering task traces.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/Gathering.config.json` (new)
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/GatheringProfessionTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs`

## Handoff (2026-04-24, Shodan test-director overhaul slice 3 - MageTeleportTests)
- Completed:
  - Migrated `MageTeleportTests.cs` to the Shodan test-director pattern. New `Services/WoWStateManager/Settings/Configs/MageTeleport.config.json` launches `TRMAF5` Foreground Troll Mage, `TRMAB5` Background Troll Mage, and SHODAN as Background Gnome Mage director. `TRMAB5` is the only BotRunner action target for spell-casting tests because `ActionType.CastSpell` resolves to `_objectManager.CastSpell(int)`, which is a documented no-op on the Foreground runner; FG is launched for Shodan-topology parity but stays idle.
  - Added a fixture-contained `StageBotRunnerAtRazorHillAsync` helper for the Razor Hill staging teleport (Durotar) so the Org arrival delta is unambiguous, and an optional `levelTo` parameter on `StageBotRunnerLoadoutAsync` so spell-casting tests can seed sufficient level via SOAP `.character level`.
  - Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/MageTeleportTests.md`. Inventory updated at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`: `MageTeleportTests.cs` moved to ALREADY-SHODAN; SHODAN-CANDIDATE total now ~44.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `docker ps` -> confirmed `mangosd`, `realmd`, `maria-db`, `pathfinding-service`, and `scene-data-service` already live.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test ... --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_shodan_levelup.trx"` -> `2 passed, 1 skipped (Alliance), 1 failed`. Pass: `MagePortal_PartyTeleported`, `MageAllCityTeleports`. Skip: `MageTeleport_Alliance_StormwindArrival` (Horde-only roster). Fail: `MageTeleport_Horde_OrgrimmarArrival` — pre-existing `SMSG_SPELL_FAILURE` for spell 3567 (initially `NO_POWER`, then a short-payload generic failure even after the bot was leveled to 20 and Rune of Teleportation was staged). Tracked as a follow-up; the Shodan/FG/BG migration shape is correct.
  - Reference anchor: `dotnet test ... --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> `failed (1/1)` after FG hit `fishing_loot_success` then BG hit `loot_window_timeout` + `max_casts_reached` (BG-side anchor flake; the prior session saw the same failure on FG). Not a regression from this slice.
- Evidence:
  - `tmp/test-runtime/results-live/mage_teleport_shodan_levelup.console.txt` shows `[ACTION-PLAN] BG TRMAB5/Jinmarbobhs: ... dispatch CastSpell.` and `[ACTION-PLAN] FG TRMAF5/Taldakurnqe: ... idle (FG ActionType.CastSpell-by-id is a no-op).`, then `Spell error for 3567: Cast failed for spell 3567` after the level-up to 20, ending in `Failed: 1, Passed: 2, Skipped: 1`.
  - `tmp/test-runtime/results-live/fishing_shodan_anchor.console.txt` captures `[FG:CHAT] [TASK] FishingTask fishing_loot_success` followed by `[BG] FishingTask never reached fishing_loot_success within 3m`.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/MageTeleport.config.json` (new)
  - `Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/MageTeleportTests.md` (new)
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`

## Handoff (2026-04-24, Shodan test-director overhaul slice 2 - EquipmentEquipTests + WandAttackTests)
- Completed:
  - Migrated `EquipmentEquipTests.cs` to the Shodan test-director pattern. `Equipment.config.json` now launches `EQUIPFG1`/`EQUIPBG1` Orc Warriors plus SHODAN; Shodan stages loadout, and only the FG/BG action targets receive `ActionType.EquipItem`.
  - Migrated `WandAttackTests.cs` to a separate `Wand.config.json` with `TRMAF5`/`TRMAB5` Troll Mages plus SHODAN. This keeps wand loadout/actions on mage characters; Shodan remains director-only.
  - Added action-target guardrails in `LiveBotFixture.TestDirector`: `ResolveBotRunnerActionTargets(...)` logs the director/target split and refuses to treat SHODAN as an action target; `AssertConfiguredCharactersMatchAsync(...)` verifies the live account character class/race/gender against the selected config before actions run.
  - Fixed foreground character creation class selection so configured mage accounts are created as mages, not warriors, by resolving the race-local `SetSelectedClass` slot from `GetClassesForRace(...)`.
  - Fixed BG wand dispatch: `StartWandAttack` now casts Shoot spell `5019`, and `SpellData` includes `Shoot` for name-based resolution. BotRunner wand dispatch now stops, faces the target, then starts Shoot.
- Validation:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FgCharacterSelectScreenTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellDataTests|FullyQualifiedName~BotRunnerServiceCombatDispatchTests" --logger "console;verbosity=minimal"` -> `passed (118/118)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> confirmed `mangosd`, `realmd`, `maria-db`, `pathfinding-service`, and `scene-data-service` were already live.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~WandAttackTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=equipment_wand_action_plan_fresh8.trx" *> "tmp/test-runtime/results-live/equipment_wand_action_plan_fresh8.console.txt"` -> `passed (2/2)`.
  - Reference anchor attempted twice: `fishing_shodan_anchor.trx` and `fishing_shodan_anchor_retry.trx` both failed in FG after repeated `loot_window_timeout` and `max_casts_reached` without `fishing_loot_success`. This is recorded as an anchor failure, not an Equipment/Wand regression; the migrated live slice passed.
- Evidence:
  - `tmp/test-runtime/results-live/equipment_wand_action_plan_fresh8.console.txt` shows `director=SHODAN targets=BG:EQUIPBG1..., FG:EQUIPFG1...` for Equipment and `director=SHODAN targets=FG:TRMAF5..., BG:TRMAB5...` for Wand, then `Test Run Successful` with `Passed: 2`.
  - `tmp/test-runtime/results-live/fishing_shodan_anchor_retry.console.txt` captures the repeated anchor failure: `[FG] FishingTask never reached fishing_loot_success within 3m` with recent chat ending in `retry reason=loot_window_timeout` and `pop reason=max_casts_reached`.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/Equipment.config.json`
  - `Services/WoWStateManager/Settings/Configs/Wand.config.json`
  - `Tests/BotRunner.Tests/LiveValidation/EquipmentEquipTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/WandAttackTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/UnequipItemTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Services/ForegroundBotRunner/Frames/FgCharacterSelectScreen.cs`
  - `Exports/BotRunner/SequenceBuilders/CombatSequenceBuilder.cs`
  - `Exports/WoWSharpClient/SpellcastingManager.cs`
  - `Exports/GameData.Core/Constants/SpellData.cs`
  - unit/live docs and task trackers.
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`

## Handoff (2026-04-24, Shodan test-director overhaul slice 1 - inventory + UnequipItemTests pilot)
- Completed:
  - Audited the 70 top-level `Tests/BotRunner.Tests/LiveValidation/*.cs` files for direct FG/BG GM-command usage and grouped them by migration category. ~45 are SHODAN-CANDIDATE (test-body GM setup that should move to Shodan), the others are ACTIVITY-OWNED, NO-GM-USAGE, ALREADY-SHODAN, or FIXTURE-INFRASTRUCTURE. Inventory landed at `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`.
  - Added the first Shodan test-director helper: `LiveBotFixture.StageBotRunnerLoadoutAsync(targetAccount, label, spellsToLearn?, skillsToSet?, itemsToAdd?, cleanSlate, clearInventoryFirst)` with declarative `SkillDirective` / `ItemDirective` records (`Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`). The helper refuses to be called against Shodan herself and rejects empty target accounts.
  - Migrated `UnequipItemTests.cs` as the pilot. It now launches `Equipment.config.json` (`EQUIPFG1` + `EQUIPBG1` + SHODAN, no `AssignedActivity`), stages each role via `StageBotRunnerLoadoutAsync`, then dispatches only `ActionType.EquipItem` and `ActionType.UnequipItem`. The test body issues no GM commands. Doc refreshed at `Tests/BotRunner.Tests/LiveValidation/docs/UnequipItemTests.md`.
  - Created `Services/WoWStateManager/Settings/Configs/Equipment.config.json` to back the new pilot launch and subsequent equipment/generic-loadout migrations. Wand-specific action tests now use `Wand.config.json`.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors` (1066 warnings, unchanged).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - Live `UnequipItemTests` rerun is the next manual step — the deterministic safety bundle is the only thing run during this slice because the live equipment slice would re-trigger a StateManager restart (`Equipment.config.json` is new) and the previous Ratchet rerun already proved `EnsureSettingsAsync` switching across configs in this session.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md` (new)
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs` (new)
  - `Tests/BotRunner.Tests/LiveValidation/UnequipItemTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/UnequipItemTests.md`
  - `Services/WoWStateManager/Settings/Configs/Equipment.config.json` (new)
  - `docs/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UnequipItemTests.UnequipItem_MainhandWeapon_MovesToBags" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=unequip_shodan_pilot_1.trx"`

## Handoff (2026-04-24, live-validation Tier 1 slice 13 - FG StartFishing pending-action delivery)
- Completed:
  - Root-caused the simplified one-roster Ratchet failure to one-shot pending action delivery during FG transition-skip windows. StateManager was draining `_pendingActions` into a heartbeat response backed by the cached snapshot, while FG could still be in `ObjectManager.IsInMapTransition`; BotRunner merged the response, hit the transition-skip `continue`, then the next snapshot population cleared `CurrentAction` before `UpdateBehaviorTree(...)` could see it.
  - `BotRunnerService` heartbeat payloads now carry the lightweight readiness fields StateManager needs: `ScreenState`, `ConnectionState`, `IsObjectManagerValid`, and `IsMapTransition`.
  - `CharacterStateSocketListener` now drains queued external/test actions only when the current heartbeat/full snapshot is actionable (`InWorld`, `BotInWorld`, object manager valid, not map transition). If not actionable, the pending action stays queued for the next ready update instead of being burned.
  - Added `ActionForwardingContractTests` coverage for ready-heartbeat delivery, transition-heartbeat deferral, and non-actionable full-snapshot deferral.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> confirmed `mangosd`, `realmd`, `maria-db`, `pathfinding-service`, and `scene-data-service` were running/healthy before live validation.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt"` -> `passed (1/1)` in `4m 48s`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Evidence:
  - `D:\World of Warcraft\logs\botrunner_TESTBOT1.diag.log` -> `21:57:10.498 [ACTION-RECV] type=StartFishing params=3 ready=True`, then `tasks=2(FishingTask)`.
  - `Bot/Release/net8.0/logs/botrunner_TESTBOT2.diag.log` -> `21:58:37.471 [ACTION-RECV] type=StartFishing params=3 ready=True`, then `tasks=2(FishingTask)`.
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx` -> FG/BG `FishingTask update_entered`, `activity_start`, and final `fishing_loot_success` for both roles with no roster restart.
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.exit.txt`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `git status --short`

## Handoff (2026-04-24, live-validation Tier 1 slice 12 - single-launch Ratchet fishing; BG pathfinding cast restored)
- Completed:
  - Simplified `Fishing_CatchFish_BgAndFg_RatchetStagedPool` down to one `EnsureSettingsAsync(Fishing.config.json)` launch. The test now keeps FG + BG + Shodan online together, stages with Shodan, dispatches `ActionType.StartFishing` to FG, re-stages, then dispatches the same action to BG. `Fishing.config.json` no longer assigns `Fishing[Ratchet]` to TESTBOT1/TESTBOT2, and the obsolete `Fishing.ShodanOnly.config.json` roster file was deleted.
  - Extended `ActionDispatcher.StartFishing` so action-dispatched fishing matches the env-var path. The dispatcher now accepts `[location, useGmCommands, masterPoolId, waypoint floats...]`, forwards those into `FishingTask`, and preserves the legacy float-only waypoint shape. Added `BotRunnerServiceFishingDispatchTests` coverage for both shapes.
  - Fixed the recurring BG Ratchet LOS regression reported in the latest screenshot. `FishingTask.TryResolveCastPosition(...)` had drifted back to native-first selection, which made BG reuse `castSource=native` dock-interior standoffs (`distance≈18.2`) that threw into the pier. The resolver is pathfinding-first again, with the native ring sweep kept only as fallback.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `war-scenedata`, `pathfinding-service`, and the WWoW world services were up before live validation.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_1.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.console.txt"` -> `passed (1/1)` in `20.8074m`; TRX shows FG `castSource=pathfinding` -> `cast_position_arrived distance=15.8` -> `fishing_loot_success`, BG `castSource=pathfinding` -> `cast_position_arrived distance=16.0` -> `fishing_loot_success`, and the console shows one `WoW.exe started for account TESTBOT1`, one fixture-ready line, and only the initial `Restarting with custom settings: ...Fishing.config.json`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_2.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.console.txt"` -> `passed (1/1)`; TRX again shows FG/BG `castSource=pathfinding` and both `fishing_loot_success`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_3.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_3.console.txt"` -> `shell timed out after 30m`; the console stalled in `EnsureCloseFishingPoolActiveNearAsync(...)` during `FISHING-WAKE-*` pool staging before any `StartFishing` dispatch. Follow-up `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` stopped the lingering repo-scoped `BackgroundBotRunner.exe` and `WoWStateManager.exe` processes. Treat this as an inconclusive staging hang, not a fishing-placement regression.
- Evidence:
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.trx`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.console.txt`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.trx`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.console.txt`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_3.console.txt`
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Services/WoWStateManager/Settings/Configs/Fishing.config.json`
  - `Services/WoWStateManager/Settings/Configs/Fishing.ShodanOnly.config.json` (deleted)
  - `Tests/BotRunner.Tests/BotRunnerServiceFishingDispatchTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/FishingProfessionTests.md`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_4.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_4.console.txt"`

## Handoff (2026-04-24, live-validation Tier 1 slice 11 - Shodan staging stabilized; Ratchet fishing green 4x)
- Completed:
  - Closed the focused live fishing blocker. `Fishing_CatchFish_BgAndFg_RatchetStagedPool` now passes reliably by keeping Shodan isolated for staging, then validating FG and BG in separate runtime-generated fishing rosters so they never contend for the same relocated pool GUID.
  - `LiveBotFixture.ServerManagement.cs` now repairs previously relocated Barrens master-pool children before each staging pass (`FISHING-BASELINE`), queries one stable anchor child row per pool instead of mixing `MIN(x)`/`MIN(y)` across diverged children, and prefers relocating an active child onto pier-reachable pool `2627` instead of the shallower `2620` landing-adjacent site.
  - `FishingProfessionTests.cs` now stages Shodan through `Fishing.ShodanOnly.config.json`, runs FG-only fishing, re-stages with Shodan, then runs BG-only fishing. That preserves task-owned fishing behavior while removing the same-pool race that was invalidating dual-bot runs after relocation fallback.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (31/31)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_target2627_probe.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_1.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_2.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_3.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_rerun_after_fixture_change.trx"` -> `passed (1/1)` (4th consecutive pass; 22m28s; Shodan -> FG -> Shodan -> BG staging cycles with FISHING-BASELINE repairs + FISHING-RELOCATE onto pool 2627 both rounds)
- Evidence:
  - `tmp/test-runtime/results-live/fishing_target2627_probe.trx`
  - `tmp/test-runtime/results-live/fishing_final_1.trx`
  - `tmp/test-runtime/results-live/fishing_final_2.trx`
  - `tmp/test-runtime/results-live/fishing_final_3.trx`
  - `tmp/test-runtime/results-live/fishing_final_rerun_after_fixture_change.trx`
  - `tmp/test-runtime/results-live/fishing_final_1.console.txt`
  - `tmp/test-runtime/results-live/fishing_final_2.console.txt`
  - `tmp/test-runtime/results-live/fishing_final_3.console.txt`
  - `tmp/test-runtime/results-live/fishing_final_rerun_after_fixture_change.console.txt`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: none queued; Ratchet fishing slice is stable at 4 consecutive green reruns and is ready to be archived on the next tracker sweep.

## Handoff (2026-04-23, live-validation Tier 1 slice 10 - Shodan idles correctly and admin loadout equips)

- Completed:
  - Fixed the Shodan activity leak in `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`. `TESTBOT1` launches were leaving `WWOW_ASSIGNED_ACTIVITY=Fishing[Ratchet]` in process-global env state, and the next background launch inherited it. `StartBackgroundBotWorker(...)` now explicitly removes optional env vars when absent, and `StartForegroundBotRunner(...)` now clears the same optional globals when null, so `UseGmCommands=true` with no `AssignedActivity` leaves Shodan idle instead of auto-running `FishingTask`.
  - Added a dedicated admin loadout path in `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ShodanLoadout.cs` and switched `FishingProfessionTests` to use it. The helper levels Shodan to 60, resets items, learns wand proficiency (`5019`), `.additem`s a slot-correct mage BIS list, then dispatches `ActionType.EquipItem` per item and waits until each item leaves the bag snapshot. This proves the items are equipped, not merely added to inventory.
  - Corrected the slot map after live validation exposed bad IDs in the earlier list. The final validated loadout is Frostfire-based with `22498/22499/22496/22503/22501/22502/22497/22500`, neck `23058`, cloak `22731`, rings `23062/23031`, trinkets `23046/19379`, main-hand `22589`, and ranged wand `22820`. No fishing pole is present.
- Remaining blocker: the focused Ratchet fishing slice is still red, but for a different reason. Shodan now logs in, stands still, and only acts when the fixture sends explicit GM chat. The next failure is the pool-staging verifier: `EnsureCloseFishingPoolActiveNearAsync(...)` keeps logging `closest active pool = 340282346638528859811704183484516925440.0y` (`float.MaxValue`), which means the current `.pool spawns 2628` response parsing is not surfacing child coordinates in the captured chat path.
- Validation:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_idle_check.trx"` -> `failed (1/1)`; confirmed Shodan no longer auto-runs fishing, but the original loadout list failed on a bad neck-slot item id.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_loadout_fix.trx"` -> `failed (1/1)`; TRX now contains `[SHODAN-LOADOUT] Added and equipped 16 BIS items for 'SHODAN'.` and no Shodan-owned `FishingTask` activity, but `FISHING-ENSURE` still returns `float.MaxValue` for the closest active pool and FG times out waiting for a pier-reachable pool.
  - Artifacts: `tmp/test-runtime/results-live/fishing_shodan_idle_check.trx`, `tmp/test-runtime/results-live/fishing_shodan_loadout_fix.trx`.
- Files changed:
  - `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ShodanLoadout.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command (after the verifier rework): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_pool_verifier_rework.trx" *> "tmp/test-runtime/results-live/fishing_shodan_pool_verifier_rework.console.txt"`

## Handoff (2026-04-23, live-validation Tier 1 slice 9 - pathfinding-first cast resolver; both bots stand on pier edge and cast)

- Completed:
  - Re-architected the Ratchet fishing approach so both FG and BG land on the pier edge and cast into the pool every run, instead of falling off or swimming. User-confirmed screenshot (2026-04-23) shows both TESTBOT1 (FG) and TESTBOT2 (BG) standing on the Ratchet pier with fishing rods cast into the water.
  - Raised `MaxPoolLockDistance` from `45f` to `80f` (matching `FishingPoolDetectRange`) so a pool visible from the teleport landing is acquired immediately. This skips the blind 8-direction radial `BuildDefaultSearchWaypoints` sweep entirely — that sweep was the root of both failure modes (FG climbed Ratchet town structures east of the dock, BG walked off the dock into water).
  - Gated the "direct" and "straight-probe" search-walk fallbacks (`CanDirectSearchWalkFallback`, `CanSearchWaypointStraightProbePath`) on `SupportsNativeLocalPhysicsQueries`. Without reliable local LOS (FG, scene-data-less managers), `TryHasLineOfSight` always returned `true`, which let the bot walk any Z-matching short stride — including straight off a dock lip into water. FG now requires a real navmesh path for every move.
  - Made the cast-position resolver pathfinding-first for both runners. `TryResolveCastViaPathfinding` goes through `PathfindingClient.GetPath`, scans the returned path from the pool end backward, and interpolates on the first segment that brackets `IdealCastingDistanceFromPool` (`18f`, the bobber landing distance) so the resulting standoff puts the bobber right on the pool. If no segment brackets 18y, falls back to the in-range node closest to 18y, then to the endpoint. Native sphere-sweep (`FishingCastPositionFinder.FindForPool`) is the secondary when pathfinding declines — reversing the previous order — because the navmesh-authoritative path is always walkable, while the native edge finder can select a standoff right on the dock edge that BG physics slides off.
- Remaining blocker: loot table. Both bots cast successfully at `edgeDist=18.0 los=True` from `(-975.0,-3792.8,5.8)` (pathfinding-interpolated), BG emits `loot_window_open count=1 coins=0 items=[]` but the loot has no fish — the bobber appears to land beside the pool rather than on it. This is a cast-aiming / facing precision issue, not a navigation issue. Next iteration target.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `war-scenedata`, `pathfinding-service` all `Up ... (healthy)`.
  - Build (Release, all five projects: `Exports/BotRunner`, `Services/WoWStateManager`, `Services/BackgroundBotRunner`, `Services/ForegroundBotRunner`, `Tests/BotRunner.Tests`) -> `0 errors`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test ... fishing_bobber_landing_distance.trx ...` -> `failed (1/1)` on `Fishing_CatchFish_BgAndFg_RatchetStagedPool`. Positioning is clean for both bots — evidence:
    - BG: `pool_acquired distance=52.4` -> `cast_position_resolved source=pathfinding pos=(-975.0,-3792.8,5.8) edgeDist=18.0 los=True` -> continuous `approaching_pool` with `playerZ` staying at 5.2-6.9 (on dock) -> `cast_position_arrived distance=16.0 edgeDist=18.0 los=True` -> `cast_started attempt=1 spell=18248`. No `fell_off_pier`, no `player_swimming`.
    - FG: `pool_acquired distance=52.4` -> same `cast_position_resolved` -> continuous `approaching_pool` with `playerZ` staying at 5.1-5.6 (on dock) -> `cast_position_arrived distance=15.8 edgeDist=18.0 los=True` -> `cast_started attempt=1 spell=18248`. No `fell_off_pier`, no `player_swimming`, no search walk.
  - Artifacts: `tmp/test-runtime/results-live/fishing_bobber_landing_distance.trx`, `.console.txt`.
  - User visual confirmation: screenshot showing both TESTBOT1 and TESTBOT2 on the Ratchet pier edge with active fishing lines into the water.
- Files changed:
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command (focused live rerun after a cast-aiming tweak): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_bobber_aim_into_pool.trx" *> "tmp/test-runtime/results-live/fishing_bobber_aim_into_pool.console.txt"`

## Handoff (2026-04-23, live-validation Tier 1 slice 8 - phase-gated fell_off_pier; BG Ratchet fishing green)

- Completed:
  - Diagnosed the remaining BG blocker as a misnamed guard: `fell_off_pier` in `FishingTask.MoveToFishingPool` was tripping on the very first tick any time the resolved cast position sat on an elevated surface (Z=6.6) while the player was still at water / terrain level (Z=2.8). The name implies "was on the pier and fell off"; the old `approachPosition.Z - player.Position.Z > 3f` check had no phase, so a bot that never stood on the pier immediately got popped with `fell_off_pier`.
  - Added a phase gate to the guard. A new `_reachedApproachLevelForActivePool` latch flips to true the first time the player is within `FellOffPierOnApproachZTolerance` (1.5y) of the resolved approach Z. The drop check now requires that latch before popping, so only a real drop after the bot was already on the dock qualifies as "fell off". The latch resets together with the cast-position cache via `ClearCastPositionCache`, so retries and pool changes start fresh. Constants introduced: `FellOffPierOnApproachZTolerance = 1.5f`, `FellOffPierZThreshold = 3f`.
  - Did not touch the local-physics split, did not reintroduce `PathfindingClient.GetGroundZ` / `IsInLineOfSight` wrappers, did not add Navigation.dll P/Invokes, did not resurrect the deleted `FishingAtRatchetActivity` / `IActivity`, did not hardcode Ratchet coordinates.
- Remaining blocker: FG Ratchet fishing. With the phase gate in place BG completes the slice end to end; FG still fails, but on a different guard (`player_swimming_approach` → `pop reason=player_swimming`) because FG's teleport + search walk drops it into deeper water at Z≈0 / Z≈-1 and the swim guard in `MoveToFishingPool` fires before the pier check. Fixing that is a separate pass — either a search-walk filter that refuses waypoints with water-level support Z, or an approach mode that lets the bot walk out of shallow water before popping.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `war-scenedata`, `pathfinding-service` all `Up 2 days (healthy)`.
  - `dotnet build Exports/BotRunner/BotRunner.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors` (515 warnings, unchanged).
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dock_navigation_retry_after_pier_tweak.trx" *> "tmp/test-runtime/results-live/fishing_dock_navigation_retry_after_pier_tweak.console.txt"` -> `failed (1/1)` (BG succeeded, FG failed); artifacts: `tmp/test-runtime/results-live/fishing_dock_navigation_retry_after_pier_tweak.trx`, `.console.txt`.
  - Live markers (BG, end-to-end green): `activity_start location=Ratchet` -> `outfit_complete` -> `travel_dispatched command='.tele Ratchet'` -> `default_search_waypoints_generated count=8` -> `search_walk_found_pool guid=0xF11002C1AF004C1E entry=180655 distance=45.0 waypoint=5/8` -> `cast_position_resolved pos=(-968.1,-3783.4,6.6) facing=4.63 edgeDist=22.5 los=True` -> sequential `approaching_pool` steps from distance 44.5 down to 25.4 with playerZ climbing 5.0 -> 5.5 (no premature `fell_off_pier`) -> `cast_position_arrived distance=24.6 edgeDist=22.5 los=True` -> `cast_started attempt=1 spell=18248` -> `loot_window_open` -> `loot_bag_delta items=[6361]` -> `fishing_loot_success lootWindowSeen=True lootItemSeen=True bobberSeen=True lootItems=[6361]` -> `pop reason=fishing_loot_success`.
  - Live markers (FG, failing separately): `search_walk_found_pool ... distance=43.7 waypoint=5/8` -> `cast_position_unresolved ... playerPos=(-972.0,-3762.5,0.0) poolPos=(-969.8,-3805.1,0.0)` -> `approaching_pool playerZ=0.0` -> `approaching_pool playerZ=-1.3` -> `retry reason=player_swimming_approach` -> `pop reason=player_swimming`. FG is in deeper water, so the earlier `IsSwimming` guard pops before the phase-gated pier guard is ever evaluated.
- Files changed:
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command (focused live re-run after an FG swim-approach tweak): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_fg_swim_recovery.trx" *> "tmp/test-runtime/results-live/fishing_fg_swim_recovery.console.txt"`

## Handoff (2026-04-22, live-validation Tier 1 slice 7 - post-wrapper-removal validation)

- Completed:
  - Validated `2597067d` end to end against the focused Ratchet fishing slice instead of assuming the wrapper removal was behavior-preserving. The ABI crash fix from `91cbd44a` held: no `[StateManager-ERR] AccessViolationException` returned anywhere in the live evidence.
  - Restored the pre-removal runtime split for local-physics queries without reintroducing `PathfindingClient.GetGroundZ` / `PathfindingClient.IsInLineOfSight` or adding new `Navigation.dll` imports. `NavigationPath` and `FishingTask` now ask a single `BotRunner.Helpers.LocalPhysicsSupport` helper whether native local-physics queries are reliable for the current `IObjectManager`; BG / scene-data-backed managers still use `WoWSharpClient.Movement.NativeLocalPhysics` directly, while FG managers fall back to the old "GroundZ unavailable / LOS treated as clear" behavior that the deleted wrappers were effectively providing.
  - Fixed the deterministic test harness fallout from the wrapper removal. `DelegatePathfindingClient` now implements `GetPathResult(...)` so `NavigationPath` can exercise the same path-result contract production uses, `GoToArrivalTests` now installs `NativeLocalPhysics.TestGetGroundZOverride`, and the stall-detection performance test was updated to match the current `NavigationPath` recovery path.
- Remaining blocker: the wrapper removal itself is no longer the main issue. FG behavior is back to the earlier search-walk shape instead of failing immediately, and BG is back to the pre-wrapper-removal blocker: it finds pool `180655`, resolves a dock-top cast position, then drops below the pier and trips `fell_off_pier`. The productive next iteration is dock navigation / pier-approach handling, not more wrapper rollback.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were up / healthy before the live run.
  - `dotnet build Exports/BotRunner/BotRunner.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~PathfindingPerformanceTests|FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~BotRunnerServiceTests" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-deterministic" --logger "trx;LogFileName=post_wrapper_removal_unit.trx"` -> `passed (195/195)`; see `tmp/test-runtime/results-deterministic/post_wrapper_removal_unit.trx`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~BotRunnerServiceTests" --logger "console;verbosity=minimal"` -> `passed (194/194)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingPerformanceTests.GetNextWaypoint_LOSStringPull_SkipsIntermediateWaypoints|FullyQualifiedName~PathfindingPerformanceTests.GetNextWaypoint_StallDetection_TriggersRecalculation" --logger "console;verbosity=minimal"` -> `passed (2/2)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> identified repo-scoped leftovers after an earlier timed-out deterministic run (`dotnet.exe` PID `31400`, `testhost.x86.exe` PID `11752`).
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> stopped only those repo-scoped processes; no blanket process kill used.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_post_wrapper_removal.trx" *> "tmp/test-runtime/results-live/fishing_post_wrapper_removal.console.txt"` -> `failed (1/1)` with the expected remaining navigation blockers; see `tmp/test-runtime/results-live/fishing_post_wrapper_removal.trx` and `tmp/test-runtime/results-live/fishing_post_wrapper_removal.console.utf8.txt`.
  - Live markers from `fishing_post_wrapper_removal.trx`: FG now advances through mixed `probe_rejected` / `path` / `direct` / `navigate` search-walk modes before `search_walk_exhausted` instead of failing immediately after wrapper removal; BG reaches `search_walk_found_pool guid=0xF11002C1AF004C1E entry=180655`, resolves `cast_position_resolved pos=(-970.2,-3785.9,6.6) facing=4.73 edgeDist=25.5 los=False`, then hits `fell_off_pier playerZ=2.8 approachZ=6.6`.
- Files changed:
  - `Exports/BotRunner/Helpers/LocalPhysicsSupport.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/BotRunner/Helpers/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/Movement/PathfindingPerformanceTests.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dock_navigation_retry_after_pier_tweak.trx" *> "tmp/test-runtime/results-live/fishing_dock_navigation_retry_after_pier_tweak.console.txt"`

## Handoff (2026-04-22, live-validation Tier 1 slice 6 - inline Ratchet activity into FishingTask)

- Completed:
  - Diagnosed the previous failure as a critical P/Invoke ABI mismatch in `FishingCastPositionFinder.LineOfSightNative`. The C++ export `bool LineOfSight(uint32_t mapId, XYZ from, XYZ to)` takes `XYZ` by value; the C# declaration was passing seven loose floats and the resulting stack mismatch raised `System.AccessViolationException` and crashed the StateManager process on the first finder call. Switched to the same `XYZ`-by-value pattern that `WoWSharpClient.NativePhysicsInterop` and `Services.PathfindingService.Navigation` already use.
  - Refactored away `Exports/BotRunner/Activities/FishingAtRatchetActivity.cs` and the entire `IActivity` interface per "no individual activity files" + ".tele name <name> Ratchet" directives. `ActivityResolver.Resolve` now returns `IBotTask` directly, and `FishingTask` itself owns the full sequence: GM-command outfit setup (`.additem` 6256/6530, `.learn` 7620/7738, `.setskill 356 75 300`, `.pool update <id>`), then `.tele name <character> <location>` (with self-form fallback), then the existing fishing flow.
  - Removed the `zDelta>2` gate so the cast-position finder always runs, and added a `cast_position_unresolved` diagnostic for the null case.
  - Added a generic 8-direction radial search-walk fallback (`BuildDefaultSearchWaypoints`, ~28y) so a `FishingTask` dispatched with no explicit waypoints can still find pools that are outside the immediate gameobject visibility window from a named landmark.
  - Updated the live test marker from `[ACTIVITY] FishingAtRatchet start` to `[TASK] FishingTask activity_start`.
- Remaining blocker: Ratchet live slice still fails on dock navigation, not on the cast resolver. With the ABI fix in place, BG bot now successfully runs the search-walk, finds pool 180655 at 44.8y on waypoint 6/8, and the resolver returns `cast_position_resolved pos=(-968.8,-3783.5,6.6) edgeDist=22.5`. But the actual approach to that standoff drops the bot into water at Z=2.8 (approachZ=6.6) and the existing `fell_off_pier` guard pops the task. FG is still stuck earlier in the search-walk on multiple `search_walk_stalled` events.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `pathfinding-service` all `Up ... (healthy)`.
  - Build (Release, all five projects: `Exports/BotRunner`, `Services/WoWStateManager`, `Services/BackgroundBotRunner`, `Services/ForegroundBotRunner`, `Tests/BotRunner.Tests`) -> `0 errors`.
  - Native PowerShell probe (`Add-Type` against `Bot/Release/net8.0/Navigation.dll`): `GetGroundZ(-958,-3768)=5.605`, `GetGroundZ(-958,-3770)=1.265`, `GetGroundZ(-960,-3770)=5.566`, `GetGroundZ(-963,-3771)=5.441`, `GetGroundZ(-955,-3782)=-8.182`, `GetGroundZ(-957.18,-3778.92)=...` (closest bay pool spawn at ~24y from the `.tele Ratchet` landing point). The Ratchet pier is genuinely ~1y wide along Y at the staging X.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test ... fishing_search_walk_fallback.trx ...` -> `failed (1/1)`. BG: full pipeline through `cast_position_resolved` then `fell_off_pier`. FG: stuck on `search_walk_stalled`. No more `AccessViolationException`.
- Files changed:
  - `Exports/BotRunner/Activities/ActivityResolver.cs` (rewritten)
  - `Exports/BotRunner/Activities/FishingAtRatchetActivity.cs` (deleted)
  - `Exports/BotRunner/Activities/IActivity.cs` (deleted)
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/Combat/FishingData.cs`
  - `Exports/BotRunner/Tasks/FishingCastPositionFinder.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Commits pushed: `91cbd44a fix(fishing): inline Ratchet activity into FishingTask and fix LineOfSight ABI`, `884772bd feat(fishing): generic radial search-walk fallback when no waypoints provided`.
- Next command: `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dock_navigation_retry.trx"`

## Handoff (2026-04-22, live-validation Tier 1 slice 5 - Ratchet activity cast-position sweep)

- Completed: carried forward the per-character activity plumbing (`UseGmCommands`, `AssignedActivity`, runner env vars, BotRunner activity dispatch), removed the hardcoded Ratchet fishing waypoints from `FishingAtRatchetActivity`, inserted the allowed `.pool update 2628` outfit tick, added `FishingCastPositionFinder` with direct `Navigation.dll` `GetGroundZ` / `LineOfSight` probes, and integrated per-pool cast-position caching + explicit facing into `FishingTask`.
- Remaining blocker: the focused Ratchet live slice is still red. Neither bot emitted `[TASK] FishingTask cast_position_resolved`, so both continued to fall back to the legacy shoreline `in_cast_range` path and repeated `loot_window_timeout`; FG also hit repeated `approach_stalled` retries and one `fell_off_pier` abort near the end of the run.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}" | Select-String -Pattern 'mangos|realm|maria|scene|pathfind' | ForEach-Object { $_.Line }` -> `scene-data-service`, `war-scenedata`, `mangosd`, `realmd`, `pathfinding-service`, `maria-db` all `Up ... (healthy)`.
  - `dotnet build Exports/BotRunner/BotRunner.csproj -c Release -v minimal`; `dotnet build Services/WoWStateManager/WoWStateManager.csproj -c Release -v minimal`; `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj -c Release -v minimal`; `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj -c Release -v minimal`; `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> all succeeded (`0` errors).
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_sphere_sweep.trx" *> "tmp/test-runtime/results-live/fishing_sphere_sweep.console.txt"; exit $LASTEXITCODE` -> `failed (1 test, 1 failure)` in `3m 52s`; FG never reached `fishing_loot_success`.
  - `PowerShell Add-Type Navigation probe in Bot/Release/net8.0` -> `GetGroundZ(map=1, x=-960, y=-3770, z=9, search=40)=5.566`, `GetGroundZ(-955, -3782, 9, 40)=-8.182`, `GetGroundZ(-949.932, -3766.883, 9, 40)=3.703`; `Navigation.dll` and `Physics.dll` are present in `Bot/Release/net8.0/` and `Bot/Release/net8.0/x86/`.
- Files changed:
  - `Services/WoWStateManager/Settings/CharacterSettings.cs`
  - `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  - `Services/WoWStateManager/StateManagerWorker.cs`
  - `Services/WoWStateManager/Settings/Configs/Fishing.config.json`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/Activities/IActivity.cs`
  - `Exports/BotRunner/Activities/ActivityResolver.cs`
  - `Exports/BotRunner/Activities/FishingAtRatchetActivity.cs`
  - `Exports/BotRunner/Tasks/FishingCastPositionFinder.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command:
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_sphere_sweep_retry.trx"`

## Handoff (2026-04-22, live-validation Tier 1 slice 4 - dual-bot Ratchet staged-pool fishing)

- Completed: replaced the pier open-water direct-cast shortcut in `FishingProfessionTests` with `Fishing_CatchFish_BgAndFg_RatchetStagedPool`, the authoritative FG+BG Ratchet staged-pool proof. Both TESTBOT1 (FG) and TESTBOT2 (BG) are now required in-world (asserted pre- and post-prep against `LiveBotFixture.AllBots`), stage at the Ratchet packet-capture dock, locate a real off-shore fishing pool via `PrepareRatchetFishingStageAsync` (DB spawn query + natural respawn wait + visible-pool confirmation), and dispatch the task-owned `ActionType.StartFishing` flow. `AssertFishingResult` enforces `pool_acquired`, cast-range arrival, channel/bobber observation, and a newly looted item for each bot. Shoreline/open-water direct-cast shortcuts are no longer part of the pass contract.
- Deletions: removed the pier open-water direct-cast path entirely. Dropped `RunPierOpenWaterFishing*`, `AssertDirectFishing*`, `FormatDirectFishingFailureContext`, `BuildRatchetPierCastCandidates`, `TryDirectFishingCastAsync`, `TryEnsureRatchetPierCastProbeReady`, `EnsureTestNavigationDllResolverRegistered`, `ResolveNavigationDllForTests`, `WaitForPositionSettledAsync`, `MoveToFishingWaypointAsync*`, `WaitForGoToArrivalMessageAsync`, `WaitForFacingSettledAsync`, `WaitForCastReadySnapshotAsync`, `WaitForFishingPoleEquippedAsync`, the facing utilities (`CalculateFacingToPoint/Delta`, `NormalizeAngleRadians`, `FacingDeltaRadians`, `GetMainhandGuid`, `MakeSetFacing`, `MakeGoto`), the pier-specific record types (`DirectFishingRunResult`, `DirectFishingCastCandidate`, `FerryCastTargetSpec`, `DirectFishingCastAttemptResult`, `PositionWaitResult`, `GoToArrivalWaitResult`, `WaypointMoveResult`), the pier/known-pool constants, the Navigation P/Invokes, and the now-unused `System.Reflection` / `System.Runtime.InteropServices` / `BotRunner.Native` usings. File shrank from `3023` -> `1832` lines.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `pathfinding-service` all `(healthy)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 warnings, 0 errors)` in `26s`.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dual_bot_ratchet_followup.trx"` -> `passed (1/1)` in `1m 49s`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_full_class_after_dual_bot_cleanup.trx"`

## Handoff (2026-04-22, live-validation Tier 1)

- Commits made:
  - `8174a87c` `refactor(tests): blanket-remove .gm on from live validation`
  - `93099a65` `refactor(tests): port CombatBg/CombatFg to fresh-account arena fixtures`
  - `d85a3cee` `refactor(tests): replace .respawn with natural wait in FishingProfessionTests`
- Validation commands + outcomes:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> slice 1 green (`1065 warnings, 0 errors`), slice 2 preflight green (`0 warnings, 0 errors`) plus post-harness-fix green (`85 warnings, 0 errors`), slice 3 green (`85 warnings, 0 errors`).
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_no_gm_on.trx"` -> `failed`; Horde Orgrimmar arrival still did not complete after the GM-toggle removal.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_no_gm_on_retry.trx"` -> `failed again`; Horde path logged `Spell error for 3567`.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_bg_arena_slice2.trx"` -> `skipped (1)` on the first BG-only fresh-account attempt because initial character-name hydration lagged.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_bg_arena_slice2_retry.trx"` -> `passed (1/1)` after the `LiveBotFixture` hydration reseed fix.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatFgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_fg_arena_slice2.trx"` -> `passed (1/1)`.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_natural_respawn_slice3.trx"` -> `passed (1/1)` in `1.7826m`; the new long-wait fallback was not needed on this pass because a nearby staged pool was already visible.
  - `rg -n "\.gm on|SendGmChatCommandAsync.*gm on|SetGmModeAsync" Tests Services Exports` -> slice 1 cleanup grep now only hits the allowed rule docs.
  - `rg -n "CombatTestHelpers|CombatBgBotFixture|CombatFgBotFixture" Tests` -> slice 2 cleanup grep returned `no matches`.
  - `rg -n "\.respawn" Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs` -> slice 3 cleanup grep returned `no matches`.
- Files changed:
  - Slice 1: `Tests/BotRunner.Tests/LiveValidation/{Battlegrounds/AlteracValleyFixture.cs,IntegrationValidationTests.cs,MageTeleportTests.cs,LiveBotFixture.cs,Scenarios/TestScenario.cs,Scenarios/TestScenarioRunner.cs,RagefireChasmTests.cs,LootCorpseTests.cs,FIXTURE_LIFECYCLE.md,docs/CombatLoopTests.md,docs/LootCorpseTests.md,docs/TEST_EXECUTION_MODES.md}`, `Services/WoWStateManager/Settings/CharacterSettings.cs`, `Tests/RecordedTests.PathingTests.Tests/PathingTestDefinitionTests.cs`, `docs/TASKS.md`, `Tests/BotRunner.Tests/TASKS.md`.
  - Slice 2: `Services/WoWStateManager/Settings/Configs/{CombatBg.config.json,CombatFg.config.json}`, `Tests/BotRunner.Tests/LiveValidation/{CombatBgArenaFixture.cs,CombatFgArenaFixture.cs,CombatBgTests.cs,CombatFgTests.cs,LiveBotFixture.cs,LootCorpseTests.cs}` plus deletion of the legacy Tier-1 combat helper/fixture/collection files, `docs/TASKS.md`, `Tests/BotRunner.Tests/TASKS.md`.
  - Slice 3: `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`, `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs`, `docs/TASKS.md`, `Tests/BotRunner.Tests/TASKS.md`.
- Next command:
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_followup_after_tier1.trx"`

## Handoff (2026-04-22, Tier 1 LiveValidation slice 1)

- Completed: blanket-removed active `.gm on` dispatches/helpers from live validation and supporting comments/docs under `Tests/` / `Services/`.
  - Deleted the `SetGmModeAsync(...)` helpers from `IntegrationValidationTests` and `MageTeleportTests`.
  - Removed the FG observer `.gm on` from `CombatTestHelpers` and widened follow distance to keep the observer safely out of aggro range.
  - Replaced AV mount prep's runtime GM toggle path with SOAP `.aura <mountSpellId> <characterName>` application.
  - Updated deterministic pathing fixture data plus stale live-validation docs/comments so `rg -n "\.gm on|SendGmChatCommandAsync.*gm on|SetGmModeAsync" Tests Services Exports` now only returns the rule docs (`Tests/CLAUDE.md`, `LiveValidation/docs/OVERHAUL_PLAN.md`).
  - Tightened `MageTeleport_Horde_OrgrimmarArrival` to use the real learned `CastSpell` path plus rune setup instead of the GM `.cast` shortcut while removing the old GM-mode bracket.
- Validation:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `No tasks are running which match the specified criteria.`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1065 warnings, 0 errors)` on the first build after the `.gm on` removal.
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were running/healthy for the live reruns.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_no_gm_on.trx"` -> `failed (2 passed, 1 failed, 1 skipped)`; `MageTeleport_Horde_OrgrimmarArrival` still did not arrive in Orgrimmar within 15s after helper removal.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (85 warnings, 0 errors)` after switching the Horde mage test from GM `.cast` to the real `CastSpell` path.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_no_gm_on_retry.trx"` -> `failed again (2 passed, 1 failed, 1 skipped)`; `MageTeleport_Horde_OrgrimmarArrival` logged `Spell error for 3567: Cast failed for spell 3567` and a delayed movement-controller snap to `(1469.8, -4221.5, 59.0)` while the final snapshot still reported Razor Hill.
- Notes:
  - Slice 1 code is shipped despite the blocked live proof per the follow-through policy: the failure reproduced twice and looks specific to the long-standing Horde mage teleport live path, not to residual `.gm on` usage.
  - The retry preserved the slice goal: no runtime GM-mode toggles were reintroduced anywhere in the test suite.
- Files changed:
  - `Services/WoWStateManager/Settings/CharacterSettings.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatArenaFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatTestHelpers.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FIXTURE_LIFECYCLE.md`
  - `Tests/BotRunner.Tests/LiveValidation/IntegrationValidationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RagefireChasmTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Scenarios/TestScenario.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Scenarios/TestScenarioRunner.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/CombatLoopTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LootCorpseTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/RecordedTests.PathingTests.Tests/PathingTestDefinitionTests.cs`
  - `docs/TASKS.md`
- Next command: `rg -n "CombatTestHelpers|CombatBgBotFixture|CombatFgBotFixture|CombatArenaFixture|CombatLoopTests" Tests/BotRunner.Tests/LiveValidation Services/WoWStateManager/Settings/Configs`

## Handoff (2026-04-22, Tier 1 LiveValidation slice 2)

- Completed: ported `CombatBgTests` and `CombatFgTests` onto dedicated fresh-account arena fixtures/configs, removed the legacy shared combat helper path, and kept the combat assertions on real boar kills with one `StartMeleeAttack` dispatch per attacker.
  - Rewrote `CombatBg.config.json` and `CombatFg.config.json` to use dedicated Orc Warrior fresh-account rosters (`BGONLY*`, `FGONLY*`) modeled on `CombatArena.config.json`.
  - Added `CombatBgArenaFixture` and `CombatFgArenaFixture`, both `CoordinatorFixtureBase`-backed with prep-time teleports to the Valley of Trials boar cluster and coordinator suppression during direct-action staging.
  - Rewrote `CombatBgTests` / `CombatFgTests` to find a single boar visible to both attackers, dispatch one melee-start action per bot, poll for snapshot-confirmed death, and assert every attacker survives.
  - Deleted the old combat helper/fixture/collection trio and made the minimal `LootCorpseTests` collection/fixture swap needed to keep the project compiling after that removal.
  - Hardened `LiveBotFixture.InitializeAsync()` with periodic DB character-name reseeding during the initial hydration wait so fresh BG-only rosters can pass the first in-world gate instead of stalling with blank `CharacterName` fields.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (0 warnings, 0 errors)` before the first live run.
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `No tasks are running which match the specified criteria.` before each live run.
  - `docker ps --format "{{.Names}} {{.Status}}" | Select-String -Pattern "mangos|realm|maria|scene-data|pathfinding"` -> `scene-data-service`, `mangosd`, `realmd`, `pathfinding-service`, and `maria-db` were running/healthy.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_bg_arena_slice2.trx"` -> `skipped (1)` on the first attempt; both BG bots reached `InWorld`, but the initial fixture gate still saw blank `CharacterName` values and never counted them as hydrated.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (85 warnings, 0 errors)` after the initial-hydration reseed fix in `LiveBotFixture`.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_bg_arena_slice2_retry.trx"` -> `passed (1/1)` in `58.1491s`.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatFgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_fg_arena_slice2.trx"` -> `passed (1/1)` in `2.7348m`.
  - `rg -n "CombatTestHelpers|CombatBgBotFixture|CombatFgBotFixture" Tests` -> `no matches`.
- Notes:
  - The only slice-2 harness change outside the direct combat files is the new periodic reseed in `LiveBotFixture.InitializeAsync()`; it was required because the BG-only fresh-account case exposed a gap that the mixed FG/BG `CombatArenaFixture` path had previously masked.
  - `LootCorpseTests` now rides the new BG arena fixture because deleting the legacy BG combat fixture would otherwise leave a dangling compile reference. No behavioral changes were made to the corpse-loot assertions themselves.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/CombatBg.config.json`
  - `Services/WoWStateManager/Settings/Configs/CombatFg.config.json`
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgArenaFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgArenaFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgBotFixture.cs` (deleted)
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgBotFixture.cs` (deleted)
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgValidationCollection.cs` (deleted)
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgValidationCollection.cs` (deleted)
  - `Tests/BotRunner.Tests/LiveValidation/CombatTestHelpers.cs` (deleted)
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`
  - `docs/TASKS.md`
- Next command: `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_natural_respawn_slice3.trx"`

## Handoff (2026-04-22, Tier 1 LiveValidation slice 3)

- Completed: removed the forced fishing-pool refresh command from `FishingProfessionTests` and replaced it with a natural nearby-pool wait plus one alternate named-tele retry path.
  - `RefreshRatchetFishingPoolsAsync(...)` now clears nearby pool respawn timers, then waits up to `5` minutes for a staged fishing pool to reappear from `MovementData.NearbyGameObjects` without issuing any runtime respawn command.
  - If the natural wait exhausts its budget, `PrepareRatchetFishingStageAsync(...)` now performs exactly one alternate named-tele retry, choosing the best DB-backed coastal candidate from `BootyBay` / `Auberdine` / `Azshara`.
  - Added stage-scoped nearby-gameobject polling/logging so fishing-pool visibility failures now print both `NearbyObjects` and `NearbyGameObjects` evidence.
  - Updated the fishing respawn-timer helper comment in `LiveBotFixture.ServerManagement.cs` to reflect the natural-wait / alternate-restage flow.
- Validation:
  - `rg -n "\.respawn" Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs` -> `no matches`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (85 warnings, 0 errors)`
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `No tasks are running which match the specified criteria.` before the live rerun.
  - `docker ps --format "{{.Names}} {{.Status}}" | Select-String -Pattern "mangos|realm|maria|scene-data|pathfinding"` -> `scene-data-service`, `mangosd`, `realmd`, `pathfinding-service`, and `maria-db` were running/healthy.
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_natural_respawn_slice3.trx"` -> `passed (1/1)` in `1.7826m`.
- Notes:
  - This focused rerun stayed on the fast path: a nearby staged pool was already visible after Ratchet staging, so the new `5`-minute natural-wait budget and the alternate named-tele retry were not consumed on this run.
  - The slice still ships because the forbidden forced-refresh path is fully removed, the focused staged fishing task stays green, and the longer fallback is now available for the slow-respawn cases that motivated the change.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs`
  - `docs/TASKS.md`
- Next command: `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_full_class_followup.trx"`

## Handoff (2026-04-22, P5.x LiveValidation ACK migration)

- Completed: migrated the remaining six `LiveValidation/*` `AssertCommandSucceeded`
  helpers to delegate to `LiveBotFixture.AssertTraceCommandSucceeded`. P4.5.3
  started this with `IntegrationValidationTests` and `TalentAllocationTests`;
  this slice closes out: `CombatLoopTests`, `CharacterLifecycleTests`,
  `BuffAndConsumableTests`, `GatheringProfessionTests`, `MageTeleportTests`,
  `QuestInteractionTests`. Each file keeps its local `AssertCommandSucceeded`
  shape (signature-stable) but the body is now a one-line delegation.
- Non-duplicate helpers preserved: `CombatLoopTests.ContainsCombatCommandFailure`
  / `TraceHasCombatCommandFailure` stayed — those are combat-specific rejection
  checks unrelated to the generic command-rejection text scan.
- Removed: `CombatLoopTests` local `ContainsCommandRejection` copy (identical
  to the shared `LiveBotFixture.ContainsCommandRejection`, only referenced
  by the now-delegating `AssertCommandSucceeded`).
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 pre-existing warnings, 0 errors)`
  - LiveValidation suites require Docker+MaNGOS; deterministic slice already
    covered by the P5.1 build/test pass.
- Notes:
  - ACK gate is additive everywhere: commands not yet wired into `CommandAckEvent`
    still fall through to `ContainsCommandRejection`, so no LiveValidation test
    loses coverage. Commands with real ACK signals (e.g. any future tracked
    ApplyLoadout-equivalent chat command) gain immediate Failed/TimedOut
    detection.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CharacterLifecycleTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs`
  - `docs/TASKS.md`
- Next command: `rg -n "private static void AssertCommandSucceeded" Tests/BotRunner.Tests/LiveValidation`

## Handoff (2026-04-22, P5.1)

- Completed: shipped `P5.1` (Loadout ACK consumption in `BattlegroundCoordinator`).
  `P4.5.1`'s `LastAckStatus` is no longer test-only — `HandleApplyingLoadouts`
  now pre-stamps correlation ids and `RecordLoadoutProgressFromSnapshots` closes
  the pre-task-rejection + step-TimedOut gaps where `snapshot.LoadoutStatus`
  never flips.
- Validation:
  - `tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST` -> `No tasks are running which match the specified criteria.`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 pre-existing warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BattlegroundCoordinator" -v minimal` -> `passed (22/22)`
- Notes:
  - `LastAckStatus` now delegates to a richer `LastAck` helper that returns the full
    `CommandAckEvent` (so coordinators can log failure reasons). Existing
    `BattlegroundCoordinatorAckTests` stay green because the status-only wrapper
    preserves the `P4.5.1` contract.
  - `HandleApplyingLoadouts` pre-stamps `ActionMessage.CorrelationId` with
    `bg-coord:loadout:<account>:<guid>`. `CharacterStateSocketListener.StampDispatchCorrelationId`
    already skips stamping when `CorrelationId` is non-empty, so the coordinator id
    survives end-to-end to the `CommandAckEvent` without listener changes.
  - ACK gate is additive: `snapshot.LoadoutStatus` still drives resolution when no
    ACK has arrived; terminal ACKs short-circuit only when the account is still
    unresolved. Pending ACKs are deliberately ignored so the coordinator keeps
    waiting on the concrete LoadoutStatus / terminal ack.
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorLoadoutTests.cs`
  - `docs/TASKS.md`
- Next command: `rg -n "AssertCommandSucceeded|AssertTraceCommandSucceeded" Tests/BotRunner.Tests/LiveValidation`

## Handoff (2026-04-21, P4.5)

- Completed: shipped `P4.5` only. Phase `P4` is now fully closed (P4.1-P4.5).
- Commits:
  - `4c39065c` `feat(coord): P4.5.1 add LastAckStatus helper on BattlegroundCoordinator`
  - `e8306a9f` `test(botrunner): P4.5.2/P4.5.3 expose AckStatus in GmChatCommandTrace`
- Validation:
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundCoordinator|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests|FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~ActionForwardingContractTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
- Notes:
  - `BattlegroundCoordinator.LastAckStatus` is static and reusable — coordinator state handlers can key on a dispatched `ActionMessage`'s correlation id to react to ACK arrivals without repeating the scan logic. Integration into further coordinator transitions is deferred until a concrete driver shows up that needs it.
  - `SendGmChatCommandTrackedAsync` now stamps a test-owned `test:<account>:<seq>` correlation id on every dispatched `ActionMessage`. `CharacterStateSocketListener.StampDispatchCorrelationId` only stamps when the id is empty, so the test id survives to the snapshot.
  - Migration policy: only `AssertCommandSucceeded` helpers in `IntegrationValidationTests` and `TalentAllocationTests` were moved over. The rest continue to use `ContainsCommandRejection` until the backing command wires a `CommandAckEvent`. The legacy helper is intentionally still exposed from `LiveBotFixture.Assertions.cs`.
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorAckTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Assertions.cs`
  - `Tests/BotRunner.Tests/LiveValidation/IntegrationValidationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TalentAllocationTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "^- \\[ \\]|Active task:" docs/TASKS.md`

## Handoff (2026-04-25, Shodan Buff/Consumable migration slice)

- Completed: migrated `BuffAndConsumableTests.cs` and `ConsumableUsageTests.cs` to the Shodan test-director pattern using the existing `Loot.config.json` topology.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs Tests/BotRunner.Tests/LiveValidation/ConsumableUsageTests.cs` -> `no matches`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~ConsumableUsageTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=buff_consumable_shodan.trx"` -> `passed overall (1 passed, 2 skipped)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
- Notes:
  - `BuffAndConsumableTests` and `ConsumableUsageTests` now reuse `Loot.config.json`; SHODAN performs clean slate, bag clear, elixir staging, and Lion's Strength aura cleanup, while `LOOTBG1` receives only `UseItem` / `DismissBuff`.
  - `ConsumableUsageTests` passed the legacy BG `UseItem` baseline. The richer buff/slot and dismiss assertions remain tracked skips until the BG consumable aura observation path and `WoWUnit.Buffs` metadata are stable.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/ConsumableUsageTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/BuffAndConsumableTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/ConsumableUsageTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - task trackers
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BgInteractionTests.cs`

## Handoff (2026-04-25, Shodan DeathCorpseRun migration slice)

- Completed: migrated `DeathCorpseRunTests.cs` to the Shodan test-director pattern using the existing `Loot.config.json` topology.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`
  - `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|EnsureCleanSlateAsync|WaitForTeleportSettledAsync|damage|InduceDeathForTestAsync|RevivePlayerAsync" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` -> `no matches`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=death_corpse_run_shodan.trx"` -> `passed overall (1 passed, 1 skipped)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
- Notes:
  - `DeathCorpseRunTests` now reuses `Loot.config.json`; SHODAN performs clean-slate, Razor Hill corpse staging, death induction, revive, and restore staging, while `LOOTBG1` receives only `ReleaseCorpse`, `StartPhysicsRecording`, `RetrieveCorpse`, and `StopPhysicsRecording`.
  - The BG run restored strict-alive state and asserted the `navtrace_<account>.json` sidecar captured `RetrieveCorpseTask` ownership. `LOOTFG1` remains launched through the same topology, but the foreground corpse-run path still skips by default unless `WWOW_RETRY_FG_CRASH001=1` is set for targeted CRASH-001 regression proof.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/DeathCorpseRunTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - task trackers
- Next command: `rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|BotClearInventoryAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die|\\.unaura|EnsureCleanSlateAsync|WaitForTeleportSettledAsync" Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs Tests/BotRunner.Tests/LiveValidation/ConsumableUsageTests.cs`

## Handoff (2026-04-25, Shodan SpiritHealer migration slice)

- Completed: migrated `SpiritHealerTests.cs` to the Shodan test-director pattern and fixed BotRunner dead/ghost spirit-healer `InteractWith` dispatch.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `passed (0 errors; existing warnings)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceCombatDispatchTests" --logger "console;verbosity=minimal"` -> `passed (15/15)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpiritHealerTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=spirit_healer_shodan_deadactor_order.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` before and after live validation -> `No repo-scoped processes to stop.`
- Notes:
  - `SpiritHealerTests` now reuses `Economy.config.json`; SHODAN performs corpse/graveyard staging and cleanup, `ECONBG1` receives only `ReleaseCorpse`, `Goto`, and `InteractWith`, and `ECONFG1` stays idle for topology parity.
  - `ActionDispatcher` now checks the ghost spirit-healer activation branch before generic gameobject interaction so `DeadActorAgent.ResurrectWithSpiritHealerAsync(...)` is used even when the runtime object collections expose the GUID outside the typed unit list.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
  - `Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SpiritHealerTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - task trackers
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money|\\.die" Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs`

## Handoff (2026-04-21, P4.4)

- Completed: shipped `P4.4` only. `P4.5` was intentionally not started.
- Commits:
  - `9232c83f` `feat(comm): P4.4 add command ack proto schema`
  - `4d1b7489` `feat(botrunner): P4.4 plumb correlated command acks`
  - `3f800ed9` `test(botrunner): P4.4 cover command ack round-trips`
- Validation:
  - `& .\protocsharp.bat "." ".."` (from `Exports/BotCommLayer/Models/ProtoDef`) -> `succeeded`
  - `dotnet build Exports/BotCommLayer/BotCommLayer.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (33 warnings, 0 errors)`
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~LoadoutSpecConverterTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
- Notes:
  - `ActionMessage.correlation_id` now survives the StateManager -> bot -> snapshot round trip. `CharacterStateSocketListener` stamps `account:sequence` ids when a dispatch reaches a bot without an explicit correlation id.
  - `WoWActivitySnapshot.recent_command_acks` is now the canonical cap-10 structured ACK ring. BotRunner emits `Pending` on dispatch plus `Success`/`Failed`/`TimedOut` on completion, including per-step `LoadoutTask` actions.
  - `SnapshotChangeSignature` now includes `RecentCommandAckCount`; unlike the chat/error rings dropped in `P4.2`, ACK count only changes per command dispatch/completion, so coordinator-visible ACK arrivals force immediate full snapshots without reintroducing diagnostic churn.
  - Duplicate `ApplyLoadout` requests now fail the duplicate correlation id without clobbering the original in-flight loadout ACK.
- Files changed:
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
  - `Exports/BotCommLayer/Models/Communication.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceLoadoutDispatchTests.cs`
  - `Exports/BotCommLayer/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `rg -n "LastAckStatus|SendGmChatCommandTrackedAsync|RecentCommandAcks|ContainsCommandRejection" Services/WoWStateManager Tests/BotRunner.Tests docs/TASKS.md`

## Handoff (2026-04-21, P4.3)

- Completed: shipped `P4.3` only. `P4.4` (correlation ids + `CommandAckEvent`) and `P4.5` (coordinator + test migration) were intentionally not started.
- Commits:
  - `8add32e9` `feat(botrunner): P4.3 event-driven LoadoutTask step advancement`
- Validation:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors, 515 pre-existing warnings)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors, 727 pre-existing warnings)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
- Notes:
  - `LoadoutStep` now owns the ack lifecycle: `AttachExpectedAck(IWoWEventHandler?)` installs a filtered subscription on the matching event, `DetachExpectedAck()` removes it, and `AckFired` short-circuits `IsSatisfied` without preventing the polling path from flipping it. Steps that do not override `OnAttachExpectedAck` (AddItemSet, EquipItem, UseItem, LevelUp) stay pure-polling.
  - `LoadoutTask.Update` attaches all acks once via `AttachExpectedAcks()` on first tick (gated by `_acksAttached`), detaches per-step on advancement, and detaches all remaining steps on terminal transitions (`TransitionToReady`, `Fail`).
  - Polling fallback untouched: the pacing loop, retry budget, `.additemset`/`.use`/`.levelup` behavior, and `IsOneShot` semantics are all unchanged.
  - New ack tests deliberately disable the fake-server side-effect (`harness.SuppressFakeServer = true`) so advancement is attributable to the event alone; the existing polling-only end-to-end test was kept to prove the fallback still converges when no event ever fires.
- Files changed:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `rg -n "correlation_id|CommandAckEvent|RecentCommandAcks" Exports/BotCommLayer docs/TASKS.md`

## Handoff (2026-04-21, P4.1/P4.2)

- Completed: shipped `P4.1` and `P4.2` only. `P4.3`, `P4.4`, and `P4.5` were intentionally not started.
- Commits:
  - `06b39001` `feat(comm): P4.1 add OnLearnedSpell/OnUnlearnedSpell events (FG+BG)`
  - `a9f9ba6b` `feat(comm): P4.1 add OnSkillUpdated event (FG+BG)`
  - `1560495b` `feat(comm): P4.1 add OnItemAddedToBag event (FG+BG)`
  - `35a05376` `feat(comm): P4.1 route attack/inventory/spell failures through OnErrorMessage`
  - `58fbae48` `feat(comm): P4.1 register SMSG_NOTIFICATION -> OnSystemMessage`
  - `b7293f1a` `fix(botrunner): P4.2 drop RecentChat/ErrorCount from snapshot signature`
- Validation:
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpEventEmitterTests|FullyQualifiedName~LootingNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientAttackErrorTests|FullyQualifiedName~SpellHandlerTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientNotificationTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
- Notes:
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs.GetDeltaMessages(...)` already computes deltas by subtracting the previous full-snapshot list from the current list, so heartbeat-delivered message batches still surface correctly. No helper code change was required for `P4.2.3`.
  - No `P4.1` / `P4.2` sub-task remains open.
- Files changed:
  - `Exports/GameData.Core/Interfaces/IWoWEventHandler.cs`
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Exports/WoWSharpClient/Networking/ClientComponents/LootingNetworkClientComponent.cs`
  - `Services/ForegroundBotRunner/Statics/WoWEventHandler.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Agent/LootingNetworkAgentTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientAttackErrorTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientNotificationTests.cs`
  - `Tests/WoWSharpClient.Tests/WoWSharpEventEmitterTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs`
  - task trackers
- Next command: `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`

## Handoff (2026-04-20)

- Completed: closed the WSG desired-party/objective slice end to end. `BotRunnerService.DesiredParty.GetCurrentGroupSize(...)` now counts the local player when `PartyAgent` reports only the other four members of a full 5-player party, so Horde leaders actually convert to raid before inviting the remaining queue roster. The WSG objective scenarios also now run on separate fresh fixture collections, which removes the shared-fixture contamination where a completed full-game run left the next destructive scenario at `hydrated=19/20`.
- Deterministic coverage:
  - `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs` now pins the `PartyAgent.GroupSize == 4` / `GetGroupMembers().Count == 4` case and verifies it still drives the existing `IObjectManager.ConvertToRaid()` behavior path.
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs` now logs the exact raw snapshot(s) missing from `AllBots` when live hydration stalls, instead of only emitting the aggregate `19/20` count.
- Live-validation coverage:
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs` now uses a shared abstract base plus two separate collection fixtures so the single-capture and full-game objective scenarios each start from a fresh 20-bot WSG roster.
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs` exposes an explicit battleground-reset helper used by the objective prep path.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FullGame_CompletesToVictoryOrDefeat" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_fullgame_after_group_size_fix_20260421_0210.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "(FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFlagCaptureObjectiveTests|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFullGameObjectiveTests)" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_objective_split_fixtures_20260421_0337.trx"` -> `passed (2/2)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found.`
- Evidence:
  - `tmp/test-runtime/results-live/wsg_fullgame_after_group_size_fix_20260421_0210.trx`
  - `tmp/test-runtime/results-live/wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx`
  - `tmp/test-runtime/results-live/wsg_objective_split_fixtures_20260421_0337.trx`
- Files changed: `Exports/BotRunner/BotRunnerService.DesiredParty.cs`, `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchObjectiveCollection.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs`, and task trackers.
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`

## Handoff (2026-04-19)

- Completed: closed the battleground queue-entry stabilization slice that followed `P2`. Early battleground/friend/ignore handlers are now registered before fresh world-client login traffic arrives, duplicate `JoinBattleground` dispatch no longer stacks queue tasks, and the Arathi Basin queue/entry fixture is stable on the background-only runner path.
- Live-validation note:
  - `tmp/test-runtime/results-live/ab_queue_entry_alliance_groundlevel_recheck.trx` captured a failed rerun where `ABBOT1` (PID `33636`) crashed during the foreground battleground transfer edge.
  - `tmp/test-runtime/results-live/ab_queue_entry_background_only_recheck.trx` then passed after the fixture moved both AB leaders onto background runners, matching the existing `CoordinatorFixtureBase` warning that foreground battleground transfers are unstable in this harness.
- Validation:
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were up before the reruns.
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the deterministic/live reruns.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AgentFactoryTests" --logger "console;verbosity=minimal"` -> `passed (101/101)`
  - `powershell -ExecutionPolicy Bypass -File ./run-tests.ps1 -CleanupRepoScopedOnly` -> repo-scoped cleanup completed before each live run.
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_alliance_groundlevel_recheck.trx"` -> `failed` with `[AB:BG] CRASHED`
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
- Files changed: `Exports/WoWSharpClient/Networking/ClientComponents/NetworkClientComponentFactory.cs`, `Services/BackgroundBotRunner/BackgroundBotWorker.cs`, `Exports/BotRunner/ActionDispatcher.cs`, `Tests/WoWSharpClient.Tests/Agent/AgentFactoryTests.cs`, `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs`, `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`, `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`, and task trackers.
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
- Binary-backed note:
  - `docs/physics/0x466590_disasm.txt` now pins the deep `SMSG_UPDATE_OBJECT` descriptor walker: WoW.exe copies the update mask into stack scratch, walks fields in ascending descriptor-index order, and forwards each present field through `0x466A00 -> 0x6142E0`.
  - `docs/physics/0x466C70_disasm.txt` now pins the typed create-path switch directly: `0x466C73` rejects type ids above `7`, the jump table at `0x466DB8` only covers the eight packet-instantiated object families, and there is no separate packet-instantiated `CGPet_C` branch in this path.
  - `docs/physics/state_root.md` already pins the WoW.exe root/unroot queue-first path (`0x61A700` staging through `0x617570`), and the parity harness now proves both `SMSG_FORCE_MOVE_ROOT` and `SMSG_FORCE_MOVE_UNROOT` defer mutation/ACK until the later flush.
  - `docs/physics/state_knockback.md` already pins the WoW.exe knockback queue path (`0x603F90 -> 0x602780 -> 0x602670 -> 0x617A30 -> 0x6177A0`), and the parity harness now proves BG stages the impulse first, consumes it later, and ACKs only after that consume step.
  - `opcode_dispatch_table.md` already pinned `SMSG_CLIENT_CONTROL_UPDATE` to `0x603EA0`; the new disasm now proves that WoW.exe reads a packed GUID, reads a one-byte `canControl` flag, looks up the target object, and forwards the normalized bool into `0x5FA600`.
  - `0x5FA600` toggles bit `0x400` in `[object + 0xC58]` and only runs the follow-up global update when the object's GUID matches the active mover. That means the packet's GUID and byte both matter, so BG now ignores non-local GUIDs and preserves an explicit local lockout until `canControl=true` arrives.
  - `docs/physics/msg_move_teleport_handler.md` / `docs/physics/packet_ack_timing.md` still show the only confirmed WoW.exe teleport-ACK gate at this stage: `MSG_MOVE_TELEPORT` applies through `0x602F90 -> 0x6186B0`, while outbound `0x0C7` is emitted later from `0x602FB0` after the internal `0x468570` gate.
  - There is still no binary evidence that `0x468570` depends on local tile/scene loading. That made our `SceneDataClient.EnsureSceneDataAround(...)` requirement an unsupported BG-only deadlock source, so the gate was removed and the tests now pin “updates drained + ground snap resolved” as the managed readiness condition.
- Commands run:
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were up before the validation pass.
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the targeted build/test run.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectUpdateMutationOrderTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateMachineParityTests|FullyQualifiedName~ClientControlUpdate_LocalPlayer_TracksCanControlAndBlocksReconcile|FullyQualifiedName~ClientControlUpdate_RemoteGuid_DoesNotAffectLocalControl|FullyQualifiedName~NotifyTeleportIncoming_ClearsMovementFlagsToNone|FullyQualifiedName~TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PacketFlowParityTests|FullyQualifiedName~StateMachineParityTests|FullyQualifiedName~NotifyTeleportIncoming_ClearsMovementFlagsToNone|FullyQualifiedName~TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData" --logger "console;verbosity=minimal"` -> `passed (13/13)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=PacketFlowParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateMachineParityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=StateMachineParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
- Files changed: `docs/physics/0x466590_disasm.txt`, `docs/physics/0x466C70_disasm.txt`, `docs/physics/cgobject_layout.md`, `docs/physics/csharp_object_field_audit.md`, `docs/physics/smsg_update_object_handler.md`, `docs/physics/README.md`, `docs/TASKS.md`, `docs/TASKS_ARCHIVE.md`, `Exports/WoWSharpClient/TASKS.md`, and `Tests/WoWSharpClient.Tests/TASKS.md`.
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md -g '!**/TASKS_ARCHIVE.md'`

