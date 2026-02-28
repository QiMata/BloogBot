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
- [ ] `Exports/Loader/Loader.vcxproj` builds `Release|Win32`.
- [x] Loader diagnostics are visible in console/log during attach/start failures.
- [ ] No machine-specific debug artifact paths remain in active loader workflow files.

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
- [ ] Problem: detach path currently waits a fixed short interval and closes the thread handle without a deterministic managed shutdown handshake.
- [ ] Target files:
  - `Exports/Loader/dllmain.cpp`
  - `Exports/Loader/nethost_helpers.h`
- [ ] Required change: add explicit shutdown signaling/observability so attach failures or unload paths do not leave long-lived loader-hosted threads/process side effects.
- [ ] Validation command: `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32`.
- [ ] Acceptance: loader unload path is deterministic and emits teardown diagnostics that can be correlated with test-time process cleanup.

### LDR-MISS-002 Add deterministic console/log visibility controls for test runs
- [ ] Problem: startup diagnostics exist but console visibility and log flow are not explicitly controlled per run mode.
- [ ] Target files:
  - `Exports/Loader/dllmain.cpp`
  - `Exports/Loader/nethost_helpers.h`
  - `Exports/Loader/README.md`
- [ ] Required change: define a simple run-mode switch (e.g., debug/test flag) for console allocation and document where logs are written for troubleshooting.
- [ ] Validation command: `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Debug /p:Platform=Win32`.
- [ ] Acceptance: operator can force visible loader diagnostics during tests without source edits, and README documents exact behavior.

### LDR-MISS-003 Remove stale local debug stubs and TODO noise from loader workspace
- [x] **Done (batch 9).** Debug stub files (`simple_loader.cpp`, `minimal_loader.cpp`, `test_minimal.cpp`) were already deleted in a prior session. VS-generated TODO boilerplate removed from `stdafx.h` and `stdafx.cpp`. Verified `rg "TODO|FIXME" Exports/Loader` returns no hits in source files.
- [x] Acceptance: loader task inventory reflects production code only; no ambiguous placeholder comments remain in active path files.

## Simple Command Set
1. `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32`
2. `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Debug /p:Platform=Win32`
3. `rg --line-number "TODO|FIXME" Exports/Loader`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Current focus: `LDR-MISS-001`
- Last delta: added evidence-backed checklist and snapshot for loader build prerequisites, fixed-time teardown waits, console/log visibility, and stale machine-specific debug stubs.
- Pass result: `delta shipped`
- Validation/tests run:
  - `msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32` (fails: `msbuild` not found in PATH)
  - `dotnet msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Release /p:Platform=Win32` (fails: `MSB4278`, missing C++ targets)
  - `dotnet msbuild Exports/Loader/Loader.vcxproj /t:Build /p:Configuration=Debug /p:Platform=Win32` (fails: `MSB4278`, missing C++ targets)
  - `rg -n "WaitForSingleObject|CloseHandle|CreateThread|FreeLibraryAndExitThread|shutdown|stop|detach" Exports/Loader/dllmain.cpp Exports/Loader/nethost_helpers.h`
  - `rg -n "AllocConsole|AttachConsole|FreeConsole|printf|std::cout|log|LOG|diagnostic|DEBUG|console" Exports/Loader/dllmain.cpp Exports/Loader/nethost_helpers.h Exports/Loader/README.md`
  - `rg -n "C:\\\\|D:\\\\|E:\\\\|Users\\\\|Desktop|Documents|TEMP|TODO|FIXME" Exports/Loader/simple_loader.cpp Exports/Loader/minimal_loader.cpp Exports/Loader/test_minimal.cpp Exports/Loader/stdafx.h Exports/Loader/stdafx.cpp`
- Blockers:
  - Native loader build verification is blocked in this shell until Visual Studio C++ `msbuild`/targets are available on PATH.
- Files changed:
  - `Exports/Loader/TASKS.md`
- Next queue file: `Exports/Navigation/TASKS.md`
- Next command: `Get-Content -Path 'Exports/Navigation/TASKS.md' -TotalCount 360`
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
