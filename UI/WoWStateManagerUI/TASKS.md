<<<<<<< HEAD
﻿# WoWStateManagerUI Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Directory: .\UI\WoWStateManagerUI

Projects:
- WoWStateManagerUI.csproj

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
# WoWStateManagerUI Tasks

## Scope
- Directory: `UI/WoWStateManagerUI`
- Project: `UI/WoWStateManagerUI/WoWStateManagerUI.csproj`
- Test project: `Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj`
- Master tracker: `MASTER-SUB-038`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to files listed in `Scope`.
2. Keep commands simple and one-line.
3. Prioritize deterministic, explicit binding behavior over implicit WPF defaults.
4. Do not leave converter behavior ambiguous; every converter must define clear one-way or two-way contract.
5. Move completed items to `UI/WoWStateManagerUI/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Simple Command Set
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `dotnet run --project UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release`

## P0 Active Tasks (Ordered)

All tasks complete. No open items.

## Session Handoff
- Last updated: 2026-02-28
- Active task: None (all UI-MISS tasks complete).
- Last delta: Completed `UI-MISS-003` (added `Tests/WoWStateManagerUI.Tests` with 25 converter regression tests covering all 3 converters) and `UI-MISS-004` (simplified README from 289 lines to ~60 lines with command-first flow and converter binding contract).
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release` -> `0 Warning(s)`, `0 Error(s)`.
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release` -> 25 passed, 0 failed.
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` -> `0 Warning(s)`, `0 Error(s)`.
- Files changed:
  - `Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj` (new test project)
  - `Tests/WoWStateManagerUI.Tests/Converters/GreaterThanZeroToBooleanConverterTests.cs` (new, 8 tests)
  - `Tests/WoWStateManagerUI.Tests/Converters/InverseBooleanConverterTests.cs` (new, 4 tests)
  - `Tests/WoWStateManagerUI.Tests/Converters/EnumDescriptionConverterTests.cs` (new, 5 tests)
  - `UI/WoWStateManagerUI/README.md` (rewritten, command-first)
  - `UI/WoWStateManagerUI/TASKS.md` (updated)
  - `UI/WoWStateManagerUI/TASKS_ARCHIVE.md` (updated)
  - `WestworldOfWarcraft.sln` (added test project)
- Blockers: None.
- Next task: None remaining in this TASKS.md.
- Next command: `Get-Content -Path 'docs/TASKS.md' -TotalCount 420`.
>>>>>>> cpp_physics_system
