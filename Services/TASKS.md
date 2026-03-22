<<<<<<< HEAD
﻿# Services Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Purpose
Track orchestration and runner service work required for full bot implementation.

## Rules
- Execute continuously without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep service behavior deterministic and observable.
- Record every service-side parity blocker in the appropriate subproject task file.

## Subproject Task Files
- `Services/WoWStateManager/TASKS.md`
- `Services/ForegroundBotRunner/TASKS.md`
- `Services/BackgroundBotRunner/TASKS.md`
- `Services/PathfindingService/TASKS.md`
- `Services/DecisionEngineService/TASKS.md`
- `Services/PromptHandlingService/TASKS.md`
- `Services/CppCodeIntelligenceMCP/TASKS.md`
- `Services/LoggingMCPServer/TASKS.md`

## Active Priorities
1. StateManager action forwarding and snapshot consistency.
2. FG/BG runner lifecycle and behavior parity.
3. Pathfinding and movement service reliability under live tests.

## Handoff Fields
- Last changed subproject:
- Service logs/evidence:
- Next subproject task:

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
Move completed items to `Services/TASKS_ARCHIVE.md`.



=======
# Services Tasks

## Scope
- Project umbrella: `Services`
- Master tracker: `MASTER-SUB-012`
- Purpose: route work to service child `TASKS.md` files and enforce cross-service constraints.
- This file should not duplicate deep implementation details that belong in child files.

## Execution Rules
1. Execute one child `TASKS.md` at a time in `Child Queue` order; do not skip ahead.
2. Per pass, read only this file and the active child file first; load only directly referenced files needed to define/execute concrete task IDs.
3. Keep commands simple and one-line where possible.
4. Preserve canonical corpse-run flow in all relevant children: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
5. Enforce `--blame-hang --blame-hang-timeout 10m` for corpse-run-style validations and repo-scoped cleanup evidence.
6. Never blanket-kill `dotnet`; cleanup must be repo-scoped and evidenced.
7. If two consecutive passes produce no file delta, record `blocker` + exact `Next command`, then move to the next child.
8. Archive completed umbrella tasks in `Services/TASKS_ARCHIVE.md` in the same session.
9. Every pass must update `Session Handoff` with `Last delta`, one-line `Pass result` (`delta shipped` or `blocked`), and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Master queue currently routes from this file to service children:
  - `MASTER-SUB-013` and `MASTER-SUB-014` entries exist in [docs/TASKS.md](/E:/repos/Westworld of Warcraft/docs/TASKS.md:133) and [docs/TASKS.md](/E:/repos/Westworld of Warcraft/docs/TASKS.md:134).
  - Current master pointer `Next queue file: MASTER-SUB-013 -> Services/BackgroundBotRunner/TASKS.md` is set in [docs/TASKS.md](/E:/repos/Westworld of Warcraft/docs/TASKS.md:168).
- All queued service child task files exist on disk:
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/CppCodeIntelligenceMCP/TASKS.md`
  - `Services/DecisionEngineService/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Services/LoggingMCPServer/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PromptHandlingService/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
- Corpse-run and timeout guidance is already present in active child files and must remain consistent:
  - Canonical Orgrimmar flow in [BackgroundBotRunner/TASKS.md](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/TASKS.md:7).
  - 10-minute corpse-run timeout command in [BackgroundBotRunner/TASKS.md](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/TASKS.md:51).
  - Orgrimmar pathing focus and timeout command in [PathfindingService/TASKS.md](/E:/repos/Westworld of Warcraft/Services/PathfindingService/TASKS.md:6) and [PathfindingService/TASKS.md](/E:/repos/Westworld of Warcraft/Services/PathfindingService/TASKS.md:21).
  - Timeout plus repo cleanup expectations in [WoWStateManager/TASKS.md](/E:/repos/Westworld of Warcraft/Services/WoWStateManager/TASKS.md:21) and [WoWStateManager/TASKS.md](/E:/repos/Westworld of Warcraft/Services/WoWStateManager/TASKS.md:22).
  - FG/BG parity requirement present in [ForegroundBotRunner/TASKS.md](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/TASKS.md:11).

## Child Queue
1. `Services/BackgroundBotRunner/TASKS.md` (`MASTER-SUB-013`)
2. `Services/CppCodeIntelligenceMCP/TASKS.md` (`MASTER-SUB-014`)
3. `Services/DecisionEngineService/TASKS.md` (`MASTER-SUB-015`)
4. `Services/ForegroundBotRunner/TASKS.md` (`MASTER-SUB-016`)
5. `Services/LoggingMCPServer/TASKS.md` (`MASTER-SUB-017`)
6. `Services/PathfindingService/TASKS.md` (`MASTER-SUB-018`)
7. `Services/PromptHandlingService/TASKS.md` (`MASTER-SUB-019`)
8. `Services/WoWStateManager/TASKS.md` (`MASTER-SUB-020`)

## P0 Active Tasks (Ordered)

### SRV-UMB-001 Keep service child routing aligned with master queue
- [x] **Verified (batch 16).** All 8 child files exist. All statuses in `docs/TASKS.md` are **Done** or **Deferred**.

### SRV-UMB-002 Enforce canonical corpse-run and timeout policy across services
- [x] **Verified (batch 16).** Orgrimmar flow, 10-min timeout, and repo-scoped cleanup consistent across child docs.

### SRV-UMB-003 Enforce FG/BG parity plus physics calibration discipline
- [x] **Verified (batch 16).** Parity requirements and physics calibration gates documented in BBR, FG, and WSM child files.

### SRV-UMB-004 Convert pending service child files to direct task-ID format
- [x] **Done (batches 1-15).** All child files use direct task IDs with acceptance criteria and handoff fields.

## Canonical Commands
1. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all SRV-UMB tasks verified complete
- Last delta: SRV-UMB-001..004 verified — all child files done
- Pass result: `delta shipped`
- Files changed: `Services/TASKS.md`
- Next command: continue with next queue file
- Blockers: none
>>>>>>> cpp_physics_system
