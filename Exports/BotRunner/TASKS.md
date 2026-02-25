# BotRunner Tasks

## Scope
- Project: `Exports/BotRunner`
- Owns task orchestration for corpse-run, combat, gathering, and questing execution loops.
- This file tracks direct implementation tasks bound to concrete files/tests.
- Master tracker: `MASTER-SUB-004`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep corpse-run flow canonical: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
3. Keep all live validation runs bounded with a 10-minute hang timeout and repo-scoped cleanup evidence.
4. Record `Last delta` and `Next command` in `Session Handoff` each pass.
5. Move completed tasks to `Exports/BotRunner/TASKS_ARCHIVE.md` in the same session.
6. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
7. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.

## Environment Checklist
- [x] `Exports/BotRunner/BotRunner.csproj` builds in `Release`.
- [x] `Tests/BotRunner.Tests` targeted filters run without restore.
- [x] Repo-scoped cleanup command is available.

## Evidence Snapshot (2026-02-25)
- `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore` passes.
- `Exports/BotRunner/Tasks/Questing/QuestingTask.cs` still contains:
  - `// TODO: Implement ScanForQuestUnitsTask`
  - commented push path for `ScanForQuestUnitsTask`.
- `rg -n "class\\s+ScanForQuestUnitsTask|ScanForQuestUnitsTask" Exports/BotRunner` shows no concrete `ScanForQuestUnitsTask` class implementation.
- `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` includes Orgrimmar setup (`TeleportToNamedAsync(..., "Orgrimmar", ...)`).
- Corpse-run live validation reproduces current blocker:
  - `dotnet test ... --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m ...`
  - failure: `[BG] scenario failed: corpse run stalled with minimal movement (travel=0.0y, moveFlags=0x0)`.
- Repo-scoped cleanup evidence:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` exits `0`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` returns `none`.
- Snapshot fallback risk still visible in `Exports/BotRunner/BotRunnerService.Snapshot.cs` via broad `try/catch` guards, including guarded FG `NotImplementedException` handling.

## P0 Active Tasks (Ordered)

### BR-MISS-001 Implement quest unit scanning in questing pipeline
- [ ] Problem: `QuestingTask` still has a placeholder (`TODO`) for quest unit scan wiring and no `ScanForQuestUnitsTask` implementation is present.
- [ ] Target file: `Exports/BotRunner/Tasks/Questing/QuestingTask.cs`.
- [ ] Required change: replace the placeholder path in `ScanForQuestUnitsTask` handling with concrete task creation/insertion in the questing sequence.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Quest|FullyQualifiedName~BotRunner" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: no TODO placeholder remains for quest-unit scan path; questing tests cover expected scan behavior.

### BR-MISS-002 Keep corpse-run setup fixed to Orgrimmar with reclaim gating
- [ ] Problem: corpse-run correctness depends on deterministic setup and reclaim timing.
- [ ] Target files:
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Exports/BotRunner` task orchestration paths used by that test
- [ ] Required change: verify `.tele name {NAME} Orgrimmar` remains canonical and reclaim is accepted only when delay is ready.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: runback uses pathfinding without wall-run stalls and reclaim wait behavior is deterministic.

### BR-MISS-003 Tighten snapshot fallback behavior around missing FG fields
- [ ] Problem: fallback behavior can hide real FG field implementation gaps.
- [ ] Target file: `Exports/BotRunner/BotRunnerService.Snapshot.cs`.
- [ ] Required change: keep null-safe behavior but log/route missing-field paths explicitly so gaps cannot silently pass.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: snapshot fallback is explicit, traceable, and does not mask missing FG implementation work.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Quest|FullyQualifiedName~BotRunner" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Current focus: `BR-MISS-001`
- Last delta: executed prior handoff command (`Get-Content Exports/GameData.Core/TASKS.md`) and added resume-first/next-file continuity guards for one-by-one queue traversal.
- Pass result: `delta shipped`
- Validation/tests run:
  - `Get-Content -Path 'Exports/GameData.Core/TASKS.md' -TotalCount 360`
- Blockers:
  - none for documentation continuity pass (corpse-run stall remains tracked under `BR-MISS-002` evidence).
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next queue file: `Exports/GameData.Core/TASKS.md`
- Next command: `Get-Content -Path 'Exports/GameData.Core/TASKS.md' -TotalCount 360`
- Loop Break: if two passes produce no delta, record exact blocker and switch to next queued file.
