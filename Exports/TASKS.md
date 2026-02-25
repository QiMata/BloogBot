# Exports Tasks

## Scope
- Project umbrella: `Exports`
- Purpose: route work to export subprojects; this file should not carry deep implementation detail that belongs in child `TASKS.md` files.
- Owner of cross-export constraints (corpse-run flow, timeout/teardown discipline, FG/BG parity gates).
- Master tracker: `MASTER-SUB-002`.

## Execution Rules
1. Work one child `TASKS.md` at a time in the order listed under `Child Queue`.
2. Before switching child files, update `Session Handoff` with `Current child`, `Next child`, `Last delta`.
3. Do not duplicate child implementation details here; reference child task IDs only.
4. Use source-scoped scans for the active child project only.
5. If two passes produce no file delta, add a `Loop Break` note with blocker + next concrete command.
6. Move completed umbrella items to `Exports/TASKS_ARCHIVE.md` in the same session.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
8. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queued file read command and execute it in the same pass.
10. When master queue is rebased to this file, reset `Current child` to the first unresolved child and resume child traversal in order.

## Environment Checklist
- [x] All child `TASKS.md` files listed in `Child Queue` exist (`BotCommLayer`, `BotRunner`, `GameData.Core`, `Loader`, `Navigation`, `WinImports`, `WoWSharpClient`).
- [x] `docs/TASKS.md` contains `MASTER-SUB-003` through `MASTER-SUB-009`, all marked `Expanded`.
- [x] Canonical corpse-run/timeout cleanup constraints are represented in export child docs (`Orgrimmar`, `10m` hang timeout, `-CleanupRepoScopedOnly` command references found across child `TASKS.md` files).
- [x] Direct missing-implementation IDs map to owning children (`BR-MISS-001`, `NAV-MISS-001/002`, `WSC-MISS-001/002/003` present in their child files).
- [x] Inventory-first direct task IDs already exist for `BotCommLayer`, `GameData.Core`, `Loader`, and `WinImports` (`BCL-MISS-001`, `GDC-MISS-001`, `LDR-MISS-001`, `WINIMP-MISS-001`).

## Evidence Snapshot (2026-02-25)
- Child existence check: `Test-Path` loop confirms all seven child `TASKS.md` files in this queue are present.
- Master sync check: `rg` in `docs/TASKS.md` confirms `MASTER-SUB-003`..`MASTER-SUB-009` entries and queue alignment.
- Canonical constraint propagation:
  - `rg` over export child task files shows `.tele name {NAME} Orgrimmar`, corpse-run `10m` timeout test command, and cleanup command references.
- Direct-ID routing check:
  - `rg` confirms `BR-MISS-001` in `Exports/BotRunner/TASKS.md`.
  - `rg` confirms `NAV-MISS-001` and `NAV-MISS-002` in `Exports/Navigation/TASKS.md`.
  - `rg` confirms `WSC-MISS-001`, `WSC-MISS-002`, and `WSC-MISS-003` in `Exports/WoWSharpClient/TASKS.md`.
- Inventory task coverage:
  - `rg` confirms `BCL-MISS-001`, `GDC-MISS-001`, `LDR-MISS-001`, and `WINIMP-MISS-001` exist in the designated child files.

## Child Queue
1. `Exports/BotCommLayer/TASKS.md` (`MASTER-SUB-003`)
2. `Exports/BotRunner/TASKS.md` (`MASTER-SUB-004`)
3. `Exports/GameData.Core/TASKS.md` (`MASTER-SUB-005`)
4. `Exports/Loader/TASKS.md` (`MASTER-SUB-006`)
5. `Exports/Navigation/TASKS.md` (`MASTER-SUB-007`)
6. `Exports/WinImports/TASKS.md` (`MASTER-SUB-008`)
7. `Exports/WoWSharpClient/TASKS.md` (`MASTER-SUB-009`)

## P0 Active Tasks (Ordered)

### EXP-UMB-001 Keep child task routing synced with master
- [ ] Ensure every export child file listed above exists and has a maintained `TASKS.md`.
- [ ] Ensure child statuses in `docs/TASKS.md` match reality (`Pending`/`Synced`/`Expanded`).
- [ ] Acceptance: no export child is missing from master queue or this file.

### EXP-UMB-002 Enforce cross-export corpse-run constraints
- [ ] Apply and preserve canonical flow: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
- [ ] Keep 10-minute timeout and repo-scoped cleanup evidence on failure/timeout.
- [ ] Acceptance: child test tasks reference the same canonical flow and timeout policy.

### EXP-UMB-003 Route direct missing-implementation IDs to owning child
- [ ] `BR-MISS-001` -> `Exports/BotRunner/TASKS.md`
- [ ] `NAV-MISS-001`, `NAV-MISS-002` -> `Exports/Navigation/TASKS.md`
- [ ] `WSC-MISS-001`, `WSC-MISS-002`, `WSC-MISS-003` -> `Exports/WoWSharpClient/TASKS.md`
- [ ] Acceptance: each ID is present in the owning child file with concrete validation commands.

### EXP-UMB-004 Add missing child inventory tasks where no direct IDs exist yet
- [ ] Add inventory-first direct task IDs in:
  - `Exports/BotCommLayer/TASKS.md`
  - `Exports/GameData.Core/TASKS.md`
  - `Exports/Loader/TASKS.md`
  - `Exports/WinImports/TASKS.md`
- [ ] Required shape in each file: `Research` task to enumerate concrete file/symbol gaps, followed by `Implement` task placeholders.
- [ ] Acceptance: each child above has at least one concrete, actionable P0 task ID.

## Canonical Commands
1. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
3. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `MASTER-SUB-002` (`Exports/TASKS.md`)
- Current child: `Exports/BotCommLayer/TASKS.md`
- Next child: `Exports/BotRunner/TASKS.md`
- Last delta: added resume-first/next-file continuity guards and rebased child traversal to queue head (`BotCommLayer`) after master pointer moved back to `MASTER-SUB-002`.
- Pass result: `delta shipped`
- Validation/tests run:
  - `Get-Content -Path 'Exports/TASKS.md' -TotalCount 360`
- Files changed:
  - `Exports/TASKS.md`
- Loop Break: if blocked in current child, record blocker + exact next command before switching.
- Next command: `Get-Content -Path 'Exports/BotCommLayer/TASKS.md' -TotalCount 360`
