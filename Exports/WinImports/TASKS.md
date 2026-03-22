<<<<<<< HEAD
﻿# WinImports Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\Exports\WinImports

Projects:
- WinProcessImports.csproj

## Instructions
- Execute tasks directly without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep this file focused on active, unresolved work only.
- Add new tasks immediately when new gaps are discovered.
- Archive completed tasks to TASKS_ARCHIVE.md.

## Active Priorities
1. Validate this project behavior against current FG/BG parity goals.
2. Remove stale assumptions and redundant code paths.
3. Add or adjust tests as needed to keep behavior deterministic.

## Session Handoff
- Last task completed:
- Validation/tests run:
- Files changed:
- Next task:

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.
## Archive
Move completed items to TASKS_ARCHIVE.md and keep this file short.



=======
# WinImports Tasks

## Scope
- Project: `Exports/WinImports`
- Owns Windows interop for process open/injection, WoW process readiness detection, and UI automation helpers.
- This file tracks direct implementation tasks tied to concrete interop/monitoring files.
- Master tracker: `MASTER-SUB-008`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep Win32 interop changes explicit and verify signature correctness before behavior changes.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/WinImports/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/WinImports/WinProcessImports.csproj` builds for `net8.0` with `x86` target.
- [ ] WoW/client process detection can run without admin-only assumptions.
- [ ] Repo-scoped teardown path can identify lingering WoW/StateManager test processes without blanket `dotnet` kills.

## Evidence Snapshot (2026-02-25)
- `SafeInjection.cs` is still an empty shell:
  - file size is `0` bytes, while active injection logic exists under `WinProcessImports.SafeInjection`.
- Duplicate P/Invoke signatures are present in `WinProcessImports.cs`:
  - `VirtualAllocEx` at lines `27` and `75`.
  - `WriteProcessMemory` at lines `31` and `83`.
  - `CreateRemoteThread` at lines `35` and `91`.
- Readiness detector still has non-cancelable startup delay:
  - `WoWProcessDetector.WaitForProcessReadyAsync` has no `CancellationToken` argument.
  - `Exports/WinImports/WoWProcessDetector.cs:36` calls `await Task.Delay(initialWait);` without cancellation.
- UI automation key mapping bug remains:
  - `Exports/WinImports/WoWUIAutomation.cs:60` maps `VK_A` to `0x53` (same value as `VK_S`).
- Build baseline in current shell:
  - `dotnet build Exports/WinImports/WinProcessImports.csproj -c Release` succeeded.
- Repo-scoped cleanup hooks are present in script and process-name set:
  - `run-tests.ps1` contains `-CleanupRepoScopedOnly` and `-ListRepoScopedProcesses`.
  - process list includes `StateManager.exe`, `WoWStateManager.exe`, and `WoW.exe`.

## P0 Active Tasks (Ordered)

### WINIMP-MISS-001 Consolidate injection entrypoint and remove split-brain `SafeInjection` definitions
- [x] **Done (batch 2).** Empty `SafeInjection.cs` deleted; implementation is nested in `WinProcessImports.cs`.
- [x] Problem: `SafeInjection.cs` is empty while injection implementation lives as nested `WinProcessImports.SafeInjection`, creating ambiguous ownership.
- [x] Acceptance: injection helper location is unambiguous; no empty implementation shell remains in active project files.

### WINIMP-MISS-002 Normalize duplicate P/Invoke declarations and marshalling contracts
- [x] **Done (batch 2).** Removed raw-uint set, kept typed-enum set, updated SafeInjection call site.
- [x] Problem: `WinProcessImports.cs` declares `VirtualAllocEx`, `WriteProcessMemory`, and `CreateRemoteThread` multiple times with differing signatures, increasing marshaling/maintenance risk.
- [x] Acceptance: interop surface is deterministic, and call-site binding cannot silently drift across duplicate signatures.

### WINIMP-MISS-003 Add cancellation-aware readiness flow for startup monitor paths
- [x] **Done (batch 2).** `CancellationToken` added to `WoWProcessDetector.WaitForProcessReadyAsync` — plumbed through to Task.Delay and monitor methods.
- [x] Problem: `WoWProcessDetector.WaitForProcessReadyAsync` applies an unconditional startup delay and does not accept a `CancellationToken`, weakening teardown/timeout responsiveness.
- [x] Acceptance: canceled or timed-out readiness waits terminate deterministically without leaving monitor loops running.

### WINIMP-MISS-004 Correct UI input constants and deterministic action semantics
- [x] **Done (batch 2).** VK_A constant fixed (`0x53` → `0x41`, was duplicate of VK_S).
- [x] Problem: `WoWUIAutomation` currently maps `VK_A` to `0x53` (same as `VK_S`), causing incorrect directional input behavior.
- [x] Acceptance: left/back input semantics are not conflated and UI automation movement commands match intended keys.

### WINIMP-MISS-005 Add repo-scoped lingering process evidence hooks for WoW/StateManager cleanup
- [x] **Done (batch 13).** Added process evidence to cleanup and detection paths:
  - `run-tests.ps1`: `Stop-RepoScopedTestProcesses` now emits structured evidence summary table (pass/pid/name/outcome) with color-coded output after each cleanup.
  - `WoWProcessDetector.cs`: readiness detection start/result logs now include process name and PID for traceability.
- [x] Validation: `dotnet build Exports/WinImports/WinProcessImports.csproj -c Release` — 0 errors.
- [x] Acceptance: cleanup logs include per-process evidence and preserve unrelated machine processes (repo-scoped only).

## Simple Command Set
1. `dotnet build Exports/WinImports/WinProcessImports.csproj -c Release`
2. `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj -c Release`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`
5. `rg --line-number "TODO|FIXME|NotImplemented|throw new" Exports/WinImports`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all WinImports tasks complete (WINIMP-MISS-001..005)
- Last delta: WINIMP-MISS-005 (cleanup evidence hooks in run-tests.ps1 + detection PID logging in WoWProcessDetector.cs)
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Exports/WinImports/WinProcessImports.csproj -c Release` — 0 errors
- Files changed:
  - `Exports/WinImports/WoWProcessDetector.cs` — process name/PID in detection logs
  - `run-tests.ps1` — cleanup evidence summary table
  - `Exports/WinImports/TASKS.md`
- Next command: continue with next queue file
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
>>>>>>> cpp_physics_system
