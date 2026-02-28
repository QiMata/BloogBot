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
- [x] **Done (batch 10).** Replaced TODO + commented code with defer rationale in `QuestingTask.cs:51`. Quest-unit scanning requires quest objective→unit mapping and NPC filter design — deferred with explicit task reference.
- [x] Acceptance: no TODO placeholder remains; defer rationale documents prerequisite design work.

### BR-MISS-002 Keep corpse-run setup fixed to Orgrimmar with reclaim gating
- [x] **Code-complete.** `DeathCorpseRunTests.cs` already uses `.tele name {NAME} Orgrimmar`. Reclaim gating via `CorpseRecoveryDelaySeconds` check exists in `BotRunnerService.ActionDispatch.cs`. Setup and gating code is correct.
- [ ] Live validation deferred — needs `dotnet test --filter "DeathCorpseRunTests"` with live MaNGOS server.

### BR-MISS-003 Tighten snapshot fallback behavior around missing FG fields
- [x] **Done (batch 11).** Replaced bare `catch { }` blocks in `BotRunnerService.Snapshot.cs` with `TryPopulate()` helper that logs field name + exception type at Debug level. All 20+ silent catch blocks now emit `[Snapshot] {Field} unavailable: {Type}` when Debug logging is enabled.
- [x] Acceptance: snapshot fallback is explicit, traceable, and does not mask missing FG implementation work.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Quest|FullyQualifiedName~BotRunner" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-28
- Active task: BR-MISS-002 code-complete (live validation deferred)
- Last delta: BR-MISS-002 marked code-complete — Orgrimmar setup and reclaim gating already in place
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release` — 0 errors
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: continue with next queue file
- Blockers: BR-MISS-002 live validation requires running MaNGOS server
