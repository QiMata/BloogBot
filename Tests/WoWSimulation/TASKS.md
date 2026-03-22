<<<<<<< HEAD
﻿# WoWSimulation Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\Tests\WoWSimulation

Projects:
- WoWSimulation.Tests.csproj

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
# WoWSimulation Tasks

## Scope
- Directory: `Tests/WoWSimulation`
- Project: `Tests/WoWSimulation/WoWSimulation.Tests.csproj`
- Master tracker: `MASTER-SUB-031`
- Primary implementation surfaces:
- `Tests/WoWSimulation/MockMangosServer.cs`
- `Tests/WoWSimulation/MockMangosServerTests.cs`

## Execution Rules
1. Work tasks in this file top-down; do not switch to another local `TASKS.md` until this list is complete or blocked.
2. Keep commands simple and one-line; run smallest test filter first.
3. Use scan-budget discipline: read this file plus only directly referenced simulation/test files for the active task.
4. If two passes produce no file delta, record blocker plus exact next command in `Session Handoff`, then move to the next queue file.
5. Move completed items to `Tests/WoWSimulation/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `WoWSimulation.Tests.csproj:24` sets `<RunSettingsFilePath>$(MSBuildProjectDirectory)\..\test.runsettings</RunSettingsFilePath>`.
- [x] `Tests/test.runsettings:5` enforces `TestSessionTimeout=600000` (10 minutes).
- [x] `MockMangosServer.SendCommand` dispatches commands and throws for unknown commands at `MockMangosServer.cs:69`.
- [x] `GetPlayerHealth` path exists at `MockMangosServer.cs:65` and `:140-143`, covered by WSIM-TST-002 tests.
- [x] No-player guard paths in `MoveToPosition` (`MockMangosServer.cs:98-99`) and `CastSpell` (`:148-149`) covered by WSIM-TST-001 tests.
- [x] `MoveToPosition` event payload includes `From`, `To`, and `Duration` at `MockMangosServer.cs:111-114`; covered by WSIM-TST-003 tests.
- [x] `EventType` defines `Death` and `Resurrection`; exercised by `KillPlayer`/`ResurrectPlayer` commands and WSIM-TST-005 tests.
- [x] Configurable latency via constructor parameter (`commandLatencyMs`, default 10); covered by WSIM-TST-006 tests.

## P0 Active Tasks (Ordered)

All P0 tasks complete. See `Tests/WoWSimulation/TASKS_ARCHIVE.md` for completed items.

## Simple Command Set
- `dotnet build Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore -p:OutputPath=bin/Release/net8.0-windows -v:minimal`
- `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --no-build -p:OutputPath=bin/Release/net8.0-windows --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: None (all P0 tasks complete).
- Last delta: Implemented all 6 pending tasks (WSIM-TST-001 through WSIM-TST-006). Added 19 new tests (total 26), 2 new server commands (KillPlayer, ResurrectPlayer), configurable latency constructor parameter.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet test Tests/WoWSimulation/WoWSimulation.Tests.csproj --configuration Release --no-restore --no-build -p:OutputPath=bin/Release/net8.0-windows --logger "console;verbosity=minimal"` -> Passed `26`, Failed `0`, Skipped `0`, Duration `344 ms`.
- Files changed: `Tests/WoWSimulation/MockMangosServer.cs`, `Tests/WoWSimulation/MockMangosServerTests.cs`, `Tests/WoWSimulation/TASKS.md`, `Tests/WoWSimulation/TASKS_ARCHIVE.md`.
- Blockers: Standard output path (`Bot/Release/net8.0/`) locked by WoWStateManager (PID 148684). Use `-p:OutputPath=bin/Release/net8.0-windows` for isolated builds.
- Next task: Move to next queue file.
- Next command: N/A (local queue complete).
>>>>>>> cpp_physics_system
