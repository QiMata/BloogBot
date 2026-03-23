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
- [x] Restore descriptor-backed `Coinage`/`Copper` plus local state helpers (`InBattleground`, `HasQuestTargets`) so FG snapshots stop hardcoding those fields.
- [x] Fix FG `SpellList` parity for learned/already-known talent spells (for example `.learn 16462` acknowledged but missing from FG snapshot spell list).

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
  - FG `WoWPlayer.Coinage` now reads `PLAYER_FIELD_COINAGE` directly from descriptor memory instead of returning a hardcoded `0`, so the injected snapshot path can finally report real money totals like the BG model.
  - `LocalPlayer.Copper`, `InBattleground`, and `HasQuestTargets` now use the same semantics as `WoWSharpClient`: copper is descriptor-backed, battleground detection uses the Vanilla map set, and quest-target state comes from the quest-log slots.
  - Added deterministic `ForegroundPlayerSnapshotParityTests` that allocate a fake object/descriptor pair in-process and verify the FG object model reads coinage and quest-log state through the same memory path it uses when injected.
- Validation/tests run:
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ForegroundPlayerSnapshotParityTests" --logger "console;verbosity=minimal"` -> `10 passed`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"` -> `64 passed`
- Files changed:
  - `Services/ForegroundBotRunner/Objects/LocalPlayer.cs`
  - `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/ForegroundPlayerSnapshotParityTests.cs`
- Next command:
  - `rg -n "WoWPlayer\\.Coinage is a stub|skip coinage assertion" Tests/BotRunner.Tests/LiveValidation`
