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
- Problem: umbrella routing can drift from the master queue and silently break one-by-one execution.
- Target files:
  - `Services/TASKS.md`
  - `docs/TASKS.md`
- Required change:
  - Keep `Child Queue` order aligned with `MASTER-SUB-013..020`.
  - Keep master queue pointers (`Current queue file`, `Next queue file`) synchronized whenever switching child files.
  - Verify each queued child path exists before advancing.
- Validation command:
  - `Get-ChildItem -Path Services -Directory | ForEach-Object { $task = Join-Path $_.FullName 'TASKS.md'; \"{0}: {1}\" -f $_.Name, (Test-Path $task) }`
- Acceptance criteria:
  - No queued service child is missing from either this file or master queue routing.

### SRV-UMB-002 Enforce canonical corpse-run and timeout policy across services
- Problem: inconsistent corpse-run flow/timeout text causes drift in test behavior and teardown expectations.
- Target files:
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Services/TASKS.md`
- Required change:
  - Preserve canonical flow `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
  - Ensure relevant commands include 10-minute hang timeout and repo-scoped cleanup.
  - Remove conflicting location/timeouts if found.
- Validation command:
  - `rg -n \"Orgrimmar|blame-hang-timeout 10m|CleanupRepoScopedOnly\" Services/BackgroundBotRunner/TASKS.md Services/PathfindingService/TASKS.md Services/WoWStateManager/TASKS.md`
- Acceptance criteria:
  - No conflicting corpse-run location or timeout guidance remains across service child task files.

### SRV-UMB-003 Enforce FG/BG parity plus physics calibration discipline
- Problem: parity claims are not reliable unless FG/BG checks and physics calibration are enforced in the same cycle.
- Target files:
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/PathfindingService/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
- Required change:
  - Ensure parity requirements explicitly call out FG and BG comparison in the same scenario run.
  - Ensure movement-drift/hover issues point to PhysicsEngine calibration gates before signoff.
  - Ensure diagnostics request actionable parity output (movement cadence/spells/packets).
- Validation command:
  - `rg -n \"FG|BG|parity|Physics|calibration\" Services/BackgroundBotRunner/TASKS.md Services/PathfindingService/TASKS.md Services/WoWStateManager/TASKS.md Services/ForegroundBotRunner/TASKS.md`
- Acceptance criteria:
  - Parity and physics-calibration requirements are explicit and testable in the referenced child files.

### SRV-UMB-004 Convert pending service child files to direct task-ID format
- Problem: broad behavior buckets create ambiguity and repeated rediscovery across sessions.
- Target files:
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Services/DecisionEngineService/TASKS.md`
  - `Services/LoggingMCPServer/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
- Required change:
  - Convert each child backlog to ordered task IDs with clear scope and acceptance.
  - Keep commands short and executable.
  - Require `Session Handoff` fields: `Last delta`, `Pass result`, and exactly one `Next command`.
- Validation command:
  - `rg -n \"Pass result|Next command|### .*MISS|Acceptance\" Services/BackgroundBotRunner/TASKS.md Services/DecisionEngineService/TASKS.md Services/LoggingMCPServer/TASKS.md Services/WoWStateManager/TASKS.md`
- Acceptance criteria:
  - Each listed child contains direct task IDs, explicit acceptance criteria, and loop-proof handoff fields.

## Canonical Commands
1. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Current child: `Services/BackgroundBotRunner/TASKS.md`
- Next child: `Services/CppCodeIntelligenceMCP/TASKS.md`
- Last delta: added `MASTER-SUB-012` tracker, service-evidence snapshot, and explicit umbrella acceptance gates tied to queue/alignment/timeout/parity checks.
- Pass result: `delta shipped`
- Next command: `Get-Content -Path 'Services/BackgroundBotRunner/TASKS.md' -TotalCount 320`
