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
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
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
