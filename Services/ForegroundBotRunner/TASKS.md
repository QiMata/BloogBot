# ForegroundBotRunner Tasks

## Scope
- Project: `Services/ForegroundBotRunner`
- Owns injected-client memory access, hook safety, FG object-model parity, and crash resistance for WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Execution Rules
1. Keep this file focused on injected/runtime parity work only.
2. Never blanket-kill `dotnet` or `WoW.exe`; use repo-scoped cleanup or explicit PIDs only.
3. Prefer deterministic unit coverage for hook/state-machine work before any live validation.
4. Move completed items to `Services/ForegroundBotRunner/TASKS_ARCHIVE.md` in the same session when they no longer need follow-up.

## Active Priorities
1. FG binary hardening
- [x] Audit `Mem/ThreadSynchronizer.cs` against WoW.exe window-proc expectations and add deterministic coverage where feasible.
- [x] Extend the offset audit beyond packet/network globals to the remaining gameplay-critical struct offsets used by snapshots (`0x9B8/0x9BC/0x9C0/0x9E8`, quest log, corpse position, etc.).
- [x] Re-check `ConnectionStateMachine` and inferred packet fallbacks after the hook/offset audit is complete.

2. FG snapshot/runtime parity
- [ ] Keep FG snapshot data complete and comparable with the BG path.
- [x] Make `MovementRecorder` transport captures self-contained by resolving the active transport outside visible-object enumeration and serializing transport-local offset/orientation from the real mover pose.
- [x] Restore descriptor-backed `Coinage`/`Copper` plus local state helpers (`InBattleground`, `HasQuestTargets`) so FG snapshots stop hardcoding those fields.
- [x] Fix FG `SpellList` parity for learned/already-known talent spells (for example `.learn 16462` acknowledged but missing from FG snapshot spell list).
- [x] Restore descriptor-backed FG `Race/Class/Gender`, `FactionTemplate`, and power reads so the injected object model matches the BG snapshot surface for combat/movement consumers.
- [x] Restore foreground vendor interaction methods (`InteractWithNpcAsync`, buy/sell/repair) so FG runtime behavior no longer falls back to interface default no-ops for merchant flows.
- [x] Restore non-null FG `GossipFrame` / `QuestFrame` / `MerchantFrame` surfaces plus task-owned quest/vendor async helpers so BotRunner no longer hits null/default-interface paths on the injected client.
- [x] Restore FG flight-master discovery/activation and a non-null `TaxiFrame` surface so task-driven taxi discovery no longer falls back to interface defaults.
- [x] Restore non-null FG `CraftFrame` / `TrainerFrame` / `TalentFrame` surfaces so the legacy craft/train/talent BotRunner paths no longer hit null/default-interface fallbacks on the injected client.
- [x] Finish the remaining FG runtime parity surfaces that still inherited defaults: `QuestGreetingFrame`, `TradeFrame`, and the task-owned bank/AH/craft helper methods.

3. Packet capture/runtime safety
- [x] `FG-PKT-001` Send hook for `NetClient::Send`.
- [x] `FG-PKT-002` Packet-driven `ConnectionStateMachine`.
- [x] `FG-PKT-003` Fallback inbound packet inference from runtime state transitions.
- [x] `FG-PKT-004` `ThreadSynchronizer` wired to `ConnectionStateMachine`.
- [x] `FG-PKT-005` Direct SMSG receive hook for `NetClient::ProcessMessage`, with binary-backed address/prologue audit and working handler-table pattern fallback.

## Session Handoff
- Last updated: `2026-04-01 (session 230)`
- Pass result: `recording artifacts and FG file-backed diagnostics are now explicit opt-in`
- Last delta:
  - Session 230 stopped the foreground runner from creating packet/snapshot sidecars and `WWoWLogs`/`Documents/BloogBot` diagnostics unless `WWOW_ENABLE_RECORDING_ARTIFACTS=1`. That gate now covers `MovementRecorder`, `ForegroundPacketTraceRecorder`, `ForegroundBotWorker` startup logs, loader/startup logs, `ThreadSynchronizer` crash traces, `SignalEventManager`, `PacketLogger`, `ConnectionStateMachine`, `NativeLibraryHelper`, `WoWEventHandler`, `LoginStateMonitor`, and the anti-AFK debug log.
  - The automated/live recording paths still work because the test/tool entry points now enable the env var intentionally before launching FG capture flows.
  - Removed the untracked repo output trees that had been inflating from repeated captures: `Bot/*/Recordings`, `Bot/*/WWoWLogs`, `Bot/*/botrunner_diag.log`, and `TestResults/*`. The canonical replay corpus stayed under `Tests/Navigation.Physics.Tests/Recordings`.
  - Session 181 fixed the automated recording movement path instead of letting scenario captures fall back to Lua. `ObjectManager.StartMovement(...)` / `StopMovement(...)` now dispatch `Functions.SetControlBit(...)` through `ThreadSynchronizer.RunOnMainThread(...)`, which cleared the repeated `SetControlBitSafeFunction(...)` `NullReferenceException` seen in `injection_firstchance.log` during the Undercity capture scenarios.
  - `Memory.cs` now logs memory-read failures without dereferencing a null `InnerException`, which restored correct FG metadata reads during capture (`Race=Orc`, `Gender=Female`) instead of the earlier `Race=None` / `Gender=None` noise caused by the logging path itself.
  - Fresh packet-backed FG captures now exist for the native lower-route and west-elevator-up Undercity scenarios: `Urgzuga_Undercity_2026-03-25_10-00-52` and `Urgzuga_Undercity_2026-03-25_10-01-09`. `RecordingMaintenance capture` also now auto-cleans duplicate `Bot/*/Recordings` trees after each run so repeated FG capture passes stop inflating disk usage.
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `13 passed`
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios 13_undercity_lower_route,14_undercity_elevator_west_up --timeout-minutes 8 --configuration Release` -> succeeded; produced `Urgzuga_Undercity_2026-03-25_10-00-52` and `Urgzuga_Undercity_2026-03-25_10-01-09`
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPacketTraceRecorderTests" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - Files changed:
  - `Services/ForegroundBotRunner/Diagnostics/RecordingFileArtifactGate.cs`
  - `Services/ForegroundBotRunner/Diagnostics/ForegroundPacketTraceRecorder.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
  - `Services/ForegroundBotRunner/Loader.cs`
  - `Services/ForegroundBotRunner/MovementRecorder.cs`
  - `Services/ForegroundBotRunner/MovementScenarioRunner.cs`
  - `Services/ForegroundBotRunner/Mem/Hooks/ConnectionStateMachine.cs`
  - `Services/ForegroundBotRunner/Mem/Hooks/NativeLibraryHelper.cs`
  - `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`
  - `Services/ForegroundBotRunner/Mem/Hooks/SignalEventManager.cs`
  - `Services/ForegroundBotRunner/Mem/ThreadSynchronizer.cs`
  - `Services/ForegroundBotRunner/Program.cs`
  - `Services/ForegroundBotRunner/Statics/LoginStateMonitor.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.PlayerState.cs`
  - `Services/ForegroundBotRunner/Statics/WoWEventHandler.cs`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPacketTraceRecorderTests.cs`
  - `Services/ForegroundBotRunner/Mem/Memory.cs`
  - `Services/ForegroundBotRunner/Mem/Functions.cs`
  - `Services/ForegroundBotRunner/MovementScenarioRunner.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tools/RecordingMaintenance/Program.cs`
  - Next command:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPacketTraceRecorderTests" --logger "console;verbosity=minimal"`
  - `ObjectManager` now exposes live foreground `QuestGreetingFrame` and `TradeFrame` wrappers instead of returning interface defaults, which closes the last remaining FG interaction-frame gaps tracked in this file.
  - Added foreground implementations for `DepositExcessItemsAsync`, `PostAuctionItemsAsync`, and `CraftAvailableRecipesAsync`; those flows now drive the injected client through coarse Lua/UI automation instead of inherited no-op defaults.
  - Added deterministic FG interaction-frame coverage for quest-greeting enumeration/selection and trade-window offer/accept flows, and fixed the quest-greeting Lua probe so the count/read paths stay distinct.
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~ForegroundInteractionFrameTests" --logger "console;verbosity=minimal"` -> `10 passed`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` -> `90 passed`
  - Files changed:
  - `Services/ForegroundBotRunner/Frames/FgQuestGreetingFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgTradeFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - Next command:
  - `rg -n "=> 0;|=> false;|return null;|NotImplementedException" Services/ForegroundBotRunner Exports/BotRunner -g '!**/bin/**' -g '!**/obj/**'`
- Previous delta:
  - `ObjectManager` now exposes live foreground `CraftFrame`, `TrainerFrame`, and `TalentFrame` wrappers instead of returning `null`, which restores the remaining legacy craft/train/talent BotRunner frame surfaces on FG.
  - Added Lua-backed `FgCraftFrame`, `FgTrainerFrame`, and `FgTalentFrame` implementations; the trainer path now enumerates service metadata with zero-based BotRunner indexing, the talent path reconstructs tab/row/column state plus next-rank spell IDs, and the craft path checks reagent counts before `DoCraft(...)`.
  - Added deterministic FG frame tests covering trainer service enumeration/train indexing, talent-point accounting and spell-ID-driven learning, and craft-slot material checks/zero-based craft indexing.
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundInteractionFrameTests" --logger "console;verbosity=minimal"` -> `8 passed`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` -> `88 passed`
  - Files changed:
  - `Services/ForegroundBotRunner/Frames/FgCraftFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgTalentFrame.cs`
  - `Services/ForegroundBotRunner/Frames/FgTrainerFrame.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Inventory.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Spells.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundInteractionFrameTests.cs`
  - Next command:
  - `rg -n "QuestGreetingFrame => null|TradeFrame => null|DepositExcessItemsAsync|PostAuctionItemsAsync|CraftAvailableRecipesAsync" Services/ForegroundBotRunner Exports/BotRunner Tests -g '!**/bin/**' -g '!**/obj/**'`
