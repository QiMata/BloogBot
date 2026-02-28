# WWoW.RecordedTests.PathingTests Tasks

## Scope
- Local ownership: `WWoW.RecordedTests.PathingTests/*`.
- Goal: keep the WWoW pathing runner reliable, low-noise, and parity-safe for FG/BG execution.
- Master reference: `docs/TASKS.md` (`MASTER-SUB-039`).
- Master tracker: `MASTER-SUB-039`.

## Execution Rules
1. Execute task IDs in order; do not jump ahead.
2. Keep runs simple: one-line commands with explicit filters.
3. Never blanket-kill `dotnet`; only repo-scoped process cleanup with PID evidence.
4. Every runback/pathing validation must run FG and BG for the same scenario and compare outcomes.
5. If two passes produce no code/document delta, log blocker + exact next command, then hand off.
6. Archive completed items to `WWoW.RecordedTests.PathingTests/TASKS_ARCHIVE.md` in the same session.
7. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `BackgroundRecordedTestRunner.DisconnectAsync` notes missing loop stop even though `WoWSharpObjectManager` has `StopGameLoop` (`Runners/BackgroundRecordedTestRunner.cs:468-471`, `Exports/WoWSharpClient/WoWSharpObjectManager.cs:196`).
- [x] Configuration precedence docs/comments and provider registration are inconsistent (`Configuration/ConfigurationParser.cs:8`, `:20-26`, `README.md:72-75`).
- [x] Path validation currently only rejects null/empty path; no finite/degenerate waypoint validation exists before movement (`Runners/BackgroundRecordedTestRunner.cs:177-184`, `:213`).
- [x] `README.md` and `IMPLEMENTATION_STATUS.md` still contain drift-prone, legacy guidance (`README.md:72-75`, `:133`; `IMPLEMENTATION_STATUS.md:245-263`).

## Evidence Snapshot (2026-02-25)
- `dotnet restore WWoW.RecordedTests.PathingTests/WWoW.RecordedTests.PathingTests.csproj` succeeded (`All projects are up-to-date for restore`).
- `dotnet build WWoW.RecordedTests.PathingTests/WWoW.RecordedTests.PathingTests.csproj --configuration Release --no-restore` succeeded (warnings only; no errors).
- `dotnet build WWoW.RecordedTests.PathingTests/WWoW.RecordedTests.PathingTests.csproj --configuration Release --no-restore` succeeded with `0 Warning(s)` and `0 Error(s)`; non-blocking `dumpbin` tooling message still appears from vcpkg `applocal.ps1`.
- Loop-stop seam confirmed:
- runner starts game loop at `BackgroundRecordedTestRunner.cs:99`;
- disconnect path explicitly comments that stop is unavailable (`:470-471`) and only disposes orchestrator/client (`:473-482`);
- object manager `StopGameLoop()` exists at `WoWSharpObjectManager.cs:196`.
- Configuration precedence drift confirmed:
- class summary says `CLI > env > appsettings > defaults` (`ConfigurationParser.cs:8`);
- provider order comment says `env -> appsettings -> CLI` with environment added before json (`:20-26`);
- README states `CLI > env > config` (`README.md:72-75`, `:133`).
- Path validity gate gap confirmed:
- initial gate only checks `path == null || path.Length == 0` (`BackgroundRecordedTestRunner.cs:183-184`);
- movement executes immediately against waypoint data with no finite/progression validation (`:193-213`).
- Docs drift confirmed:
- `IMPLEMENTATION_STATUS.md` still carries broad TODO/limitations and stale completion statements (`:245-263`), duplicating/contradicting live behavior expectations.

## P0 Active Tasks (Ordered)
1. [x] `WWRPT-RUN-001` Stop background game loop during disconnect/teardown.
- **Done (2026-02-28).** Added `_objectManager?.StopGameLoop()` call before orchestrator disposal in both WWoW and non-WWoW copies.

2. [x] `WWRPT-CFG-001` Align configuration precedence with documented behavior.
- **Done (2026-02-28).** Reordered config providers: JSON → env → CLI. Fixed in both WWoW and non-WWoW copies.

3. [x] `WWRPT-PATH-001` Add path output validity gates before movement execution.
- **Done (2026-02-28).** Added finite coordinate validation loop after path retrieval. Non-finite waypoints now throw with waypoint index and coordinates. Fixed in both WWoW and non-WWoW copies.

4. [ ] `WWRPT-DOC-001` Reduce stale or contradictory local docs.
- Evidence: local docs still include legacy TODO status and mismatched command/config behavior.
- Files: `WWoW.RecordedTests.PathingTests/README.md`, `WWoW.RecordedTests.PathingTests/IMPLEMENTATION_STATUS.md`.
- Implementation: keep only current run commands, current configuration semantics, and active limitations.
- Acceptance: docs match live code paths and can be used as low-context handoff references without re-discovery.

## Simple Command Set
1. Build:
- `dotnet build WWoW.RecordedTests.PathingTests/WWoW.RecordedTests.PathingTests.csproj --configuration Release --no-restore`
2. Run one deterministic scenario (no recording):
- `dotnet run --project WWoW.RecordedTests.PathingTests -- --test-filter Northshire_ElwynnForest_ShortDistance --disable-recording --no-pathfinding-inprocess`
3. Run one category:
- `dotnet run --project WWoW.RecordedTests.PathingTests -- --category Basic --disable-recording --no-pathfinding-inprocess`
4. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `WWRPT-RUN-001` (stop background loop during disconnect/teardown).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet build WWoW.RecordedTests.PathingTests/WWoW.RecordedTests.PathingTests.csproj --configuration Release --no-restore` -> succeeded (`0 warnings`, `0 errors`).
- Files changed: `WWoW.RecordedTests.PathingTests/TASKS.md`.
- Blockers: None.
- Next task: `WWRPT-RUN-001`.
- Next command: `Get-Content -Path 'WWoW.RecordedTests.Shared/TASKS.md' -TotalCount 360`.
