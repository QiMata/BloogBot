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
  - `ThreadSynchronizer`'s WndProc block/allow decision is now centralized in a pure `ThreadSynchronizerGateEvaluator`, so the live WM_USER hook path keeps the same behavior while the state-gating rules are deterministic and unit-testable.
  - Added `ThreadSynchronizerGateTests` to pin the critical safety cases: pre-world charselect allowance, valid-world seeding, invalid-map transition blocking, packet-driven `IsLuaSafe` blocking, valid-map auto-pause on map change, and manager-base teardown blocking.
  - Extended the FG binary-backed offset audit past the packet hooks into the movement/snapshot fields used by runtime snapshots: corpse globals, player class/character count, object-manager base, movement-info facing/transport/fall/speed/move-spline offsets, and the distinction between the `0x00672170` `CMap::VectorIntersect` wrapper and `World::Intersect` at `0x006AA160`.
  - Re-ran the full `ForegroundBotRunner.Tests` suite after the audit; `ConnectionStateMachine` and packet-fallback coverage stayed green with no further code changes required.
- Validation/tests run:
  - `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
  - `dotnet build Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-build -v n`
- Files changed:
  - `Services/ForegroundBotRunner/Mem/MemoryAddresses.cs`
  - `Services/ForegroundBotRunner/Mem/Offsets.cs`
  - `Services/ForegroundBotRunner/Mem/ThreadSynchronizer.cs`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/ForegroundBotRunner.Tests/WoWExeImage.cs`
  - `Tests/ForegroundBotRunner.Tests/OffsetsBinaryAuditTests.cs`
  - `Tests/ForegroundBotRunner.Tests/ThreadSynchronizerGateTests.cs`
- Next command:
  - `Get-Content Services/ForegroundBotRunner/Objects/LocalPlayer.cs | Select-Object -First 260`
