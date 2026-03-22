<<<<<<< HEAD
﻿# Loader Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track loader/injection tasks related to FG bot startup stability and diagnostics.

## Rules
- Execute continuously without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep crash diagnostics and guardrails linked to concrete evidence.

## Active Priorities
1. Maintain stable FG injection and startup diagnostics.
2. Prevent regression in startup/attach paths affecting LiveValidation reliability.

## Session Handoff
- Last loader change:
- Validation evidence:
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
Move completed items to `Exports/Loader/TASKS_ARCHIVE.md`.



=======
# Loader Tasks

## Scope
- Project: `Exports/Loader`
- Owns native DLL bootstrap/injection path that hosts managed `ForegroundBotRunner` inside WoW.
- This file tracks direct implementation tasks bound to concrete loader files and teardown behavior.
- Master tracker: `MASTER-SUB-006`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep loader changes focused on startup determinism, diagnostics, and teardown safety.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/Loader/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/Loader/Loader.vcxproj` builds `Release|Win32` — confirmed 2026-02-28 via MSBuild (VS 2025 Community).
- [x] Loader diagnostics are visible in console/log during attach/start failures.
- [x] No machine-specific debug artifact paths remain in active loader workflow files (batch 9).

## Evidence Snapshot (2026-02-25)
- Build tool availability:
  - `msbuild` is not on PATH in this shell (`CommandNotFoundException`).
  - `dotnet msbuild Exports/Loader/Loader.vcxproj ...` fails with `MSB4278` (`Microsoft.Cpp.Default.props` missing), confirming Visual C++ targets are unavailable via current CLI path.
- Teardown behavior evidence:
  - `Exports/Loader/dllmain.cpp` waits a fixed `1000ms` on detach (`WaitForSingleObject(g_hThread, 1000)`) before closing the thread handle.
  - Startup debug wait is fixed `10000ms` (`WaitForSingleObject(hEvent, 10000)` under `_DEBUG`).
- Console/log visibility evidence:
  - `AllocConsole()` and stream redirection are called in `ThreadMain`.
  - `nethost_helpers.h` logs to both console and file (`loader_debug.log`) via `LogMessage`.
  - `Exports/Loader/README.md` documents debug console and diagnostics flow.
- Stale debug artifact evidence:
  - `simple_loader.cpp`, `minimal_loader.cpp`, and `test_minimal.cpp` contain hardcoded machine-specific paths (`C:\\Users\\WowAdmin\\...`, `C:\\Temp\\...`).
  - `stdafx.h` and `stdafx.cpp` still contain placeholder TODO comments.

## P0 Active Tasks (Ordered)

### LDR-MISS-001 Harden bootstrap thread teardown to prevent lingering loader-hosted work
- [x] **Done (batch 13).** Added deterministic teardown in `dllmain.cpp`:
  - `g_shutdownEvent` (manual-reset) created in DLL_PROCESS_ATTACH, signaled in DLL_PROCESS_DETACH.
  - Wait timeout increased from 1000ms to 5000ms with diagnostic logging (clean exit, timeout, unexpected).
  - Thread exit code logged via `GetExitCodeThread`.
  - `FreeConsole()` called on detach if console was allocated.
  - File converted from UTF-16 LE to UTF-8.
- [x] Validation: MSBuild Release|Win32 — 0 errors, `Loader.dll` produced.
- [x] Acceptance: loader unload path is deterministic and emits teardown diagnostics.

### LDR-MISS-002 Add deterministic console/log visibility controls for test runs
- [x] **Done (batch 13).** Console allocation controlled by `WWOW_LOADER_CONSOLE` env var:
  - Default: allocate console (visible diagnostics).
  - `WWOW_LOADER_CONSOLE=0` (or `n`/`N`): suppress console (logs still go to `loader_debug.log`).
  - `g_consoleAllocated` flag tracks ownership for clean FreeConsole on detach.
  - README.md updated with console visibility and teardown diagnostics documentation.
- [x] Validation: MSBuild Release|Win32 — 0 errors.
- [x] Acceptance: operator can suppress console via env var; README documents exact behavior.

### LDR-MISS-003 Remove stale local debug stubs and TODO noise from loader workspace
- [x] **Done (batch 9).** Debug stub files (`simple_loader.cpp`, `minimal_loader.cpp`, `test_minimal.cpp`) were already deleted in a prior session. VS-generated TODO boilerplate removed from `stdafx.h` and `stdafx.cpp`. Verified `rg "TODO|FIXME" Exports/Loader` returns no hits in source files.
- [x] Acceptance: loader task inventory reflects production code only; no ambiguous placeholder comments remain in active path files.

## Simple Command Set
1. `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32`
2. `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Debug /p:Platform=Win32`
3. `rg --line-number "TODO|FIXME" Exports/Loader`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all Loader tasks complete (LDR-MISS-001/002/003)
- Last delta: LDR-MISS-001 (shutdown event + teardown diagnostics) + LDR-MISS-002 (WWOW_LOADER_CONSOLE env var + README)
- Pass result: `delta shipped`
- Validation/tests run:
  - MSBuild Loader.vcxproj Release|Win32 — 0 errors
- Files changed:
  - `Exports/Loader/dllmain.cpp` — shutdown event, console control, teardown diagnostics
  - `Exports/Loader/README.md` — console visibility and teardown documentation
  - `Exports/Loader/TASKS.md`
- Next command: continue with next queue file
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
>>>>>>> cpp_physics_system
