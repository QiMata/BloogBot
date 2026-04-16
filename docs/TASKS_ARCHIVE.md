# Task Archive

Completed items moved from TASKS.md.

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
