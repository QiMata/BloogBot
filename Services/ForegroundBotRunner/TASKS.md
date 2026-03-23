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
- [ ] Audit `Mem/ThreadSynchronizer.cs` against WoW.exe window-proc expectations and add deterministic coverage where feasible.
- [ ] Extend the offset audit beyond packet/network globals to the remaining gameplay-critical struct offsets used by snapshots (`0x9B8/0x9BC/0x9C0/0x9E8`, quest log, corpse position, etc.).
- [ ] Re-check `ConnectionStateMachine` and inferred packet fallbacks after the hook/offset audit is complete.

2. FG snapshot parity
- [ ] Keep FG snapshot data complete and comparable with the BG path.
- [ ] Fix FG `SpellList` parity for learned/already-known talent spells (for example `.learn 16462` acknowledged but missing from FG snapshot spell list).

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
  - `PacketLogger` now validates the configured `NetClient::ProcessMessage` VA against a real handler-table pattern scan and can fall back to the scanned address if the fixed offset ever drifts.
  - The old pattern helper was tightened to match the actual 1.12.1 instruction shape (`mov eax, [esi+edi*4+0x74]`) instead of the stale non-SIB heuristic that no longer found the function.
  - Added binary-backed unit coverage for the send/recv hook prologues, process-message discovery, version-string address, and movement-struct layout assumptions.
  - Cleaned this task file and removed the stale merge-conflict state.
- Validation/tests run:
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PacketLoggerBinaryAuditTests" -v n`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ConnectionStateMachineTests" -v n`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build -v n`
- Files changed:
  - `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`
  - `Services/ForegroundBotRunner/Mem/Offsets.cs`
  - `Services/ForegroundBotRunner/Properties/AssemblyInfo.cs`
  - `Services/ForegroundBotRunner/CLAUDE.md`
  - `Services/ForegroundBotRunner/README.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/PacketLoggerBinaryAuditTests.cs`
  - `Tests/ForegroundBotRunner.Tests/WoWExeImage.cs`
- Next command:
  - `Get-Content Services/ForegroundBotRunner/Mem/ThreadSynchronizer.cs | Select-Object -First 260`
