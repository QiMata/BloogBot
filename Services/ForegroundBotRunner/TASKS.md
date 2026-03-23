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

2. FG snapshot parity
- [ ] Keep FG snapshot data complete and comparable with the BG path.
- [x] Make `MovementRecorder` transport captures self-contained by resolving the active transport outside visible-object enumeration and serializing transport-local offset/orientation from the real mover pose.
- [x] Restore descriptor-backed `Coinage`/`Copper` plus local state helpers (`InBattleground`, `HasQuestTargets`) so FG snapshots stop hardcoding those fields.
- [x] Fix FG `SpellList` parity for learned/already-known talent spells (for example `.learn 16462` acknowledged but missing from FG snapshot spell list).
- [x] Restore descriptor-backed FG `Race/Class/Gender`, `FactionTemplate`, and power reads so the injected object model matches the BG snapshot surface for combat/movement consumers.

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
  - `WoWUnit` now reads descriptor-backed `FactionTemplate`, `Powers`, and `MaxPowers` instead of returning hardcoded placeholders, so FG snapshots expose the same unit-power and faction-template fields the BG path already uses.
  - `WoWPlayer` now derives `Race`, `Class`, and `Gender` from `UNIT_FIELD_BYTES_0`, and `LocalPlayer` now uses that same descriptor-backed path instead of mixing in Lua/global-class fallbacks.
  - Added memory-backed FG tests that pin the `IWoWPlayer` interface path for local-player `Race/Class/Gender` plus descriptor-backed faction-template and mana/rage/energy reads.
- Validation/tests run:
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundBotRunner.Tests.ForegroundPlayerSnapshotParityTests" --logger "console;verbosity=minimal"` -> `12 passed`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` -> `68 passed`
- Files changed:
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWUnit.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPlayerSnapshotParityTests.cs`
- Next command:
  - `rg -n "MerchantFrame => null|BuyItemFromVendorAsync|SellItemToVendorAsync|RepairAllItemsAsync" Services/ForegroundBotRunner Exports/BotRunner Tests/BotRunner.Tests -g '!**/bin/**' -g '!**/obj/**'`
