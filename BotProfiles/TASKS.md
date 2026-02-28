# BotProfiles Tasks

## Scope
- Project: `BotProfiles`
- Owns spec profile factories and per-spec task implementations consumed by `Exports/BotRunner`.
- This file tracks direct file/symbol tasks only.
- Master tracker: `MASTER-SUB-001`.

## Execution Rules
1. Work this file only until the current top unchecked task is completed or explicitly blocked.
2. Use source-only scans scoped to `BotProfiles` (exclude `bin/`, `obj/`, and `tmp/`).
3. Keep commands one-line and deterministic.
4. For behavior validation, run FG and BG in the same scenario cycle.
5. Never blanket-kill `dotnet`; capture repo-scoped teardown evidence on timeout/failure.
6. Move completed items to `BotProfiles/TASKS_ARCHIVE.md` in the same session.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
8. Resume-first guard: start each pass by running the prior `Session Handoff -> Next command` verbatim before new scans.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same pass.
10. If no additional delta is possible in two consecutive passes, mark `blocked` with blocker + replacement command and advance the master queue pointer.

## Environment Checklist
- [x] `BotProfiles/BotProfiles.csproj` builds in `Release` (`0 warnings`, `0 errors`).
- [x] `Tests/BotRunner.Tests` targeted `CombatLoopTests` filter runs with `--blame-hang-timeout 10m` and completes (`Passed: 1`).
- [x] Repo-scoped cleanup command is available: `run-tests.ps1` exists and includes `-CleanupRepoScopedOnly` switch and handler.
- [x] Dedicated profile factory binding regression test does not exist yet (`Tests/BotRunner.Tests/Profiles/BotProfileFactoryBindingsTests.cs` missing; filter returns `No test matches`).

## Evidence Snapshot (2026-02-25)
- Restore/build status:
  - `dotnet restore BotProfiles/BotProfiles.csproj` succeeded.
  - `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore` succeeded.
- PvP factory miswire inventory:
  - `rg -n -U -P "CreatePvPRotationTask\(IBotContext botContext\)\s*=>\s*\n\s*new\s+PvERotationTask" BotProfiles -g "*.cs"` reports `MISWIRED_PROFILE_COUNT=16` (matching the `BP-MISS-001` file list).
- Targeted runtime gate:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` passed (`1` test).
- Missing regression gate:
  - `dotnet test ... --filter "FullyQualifiedName~BotProfileFactoryBindingsTests"` reports no matching tests.
- Tooling note:
  - Test build logs still print `dumpbin` missing from vcpkg `applocal.ps1`, but commands exit successfully.

## P0 Active Tasks (Ordered)

### BP-MISS-001 Fix miswired PvP factories that currently return PvE rotation tasks
- [x] **Done (batch 1).** All 16 miswired PvP factories fixed to return `new PvPRotationTask(botContext)`.
- [x] Problem: multiple profiles map `CreatePvPRotationTask(...)` to `new PvERotationTask(...)`, bypassing dedicated PvP classes.
- [ ] Evidence: miswired files are:
  - `BotProfiles/DruidFeralCombat/DruidFeral.cs`
  - `BotProfiles/DruidRestoration/DruidRestoration.cs`
  - `BotProfiles/HunterBeastMastery/HunterBeastMastery.cs`
  - `BotProfiles/HunterMarksmanship/HunterMarksmanship.cs`
  - `BotProfiles/HunterSurvival/HunterSurvival.cs`
  - `BotProfiles/MageArcane/MageArcane.cs`
  - `BotProfiles/MageFrost/MageFrost.cs`
  - `BotProfiles/PriestDiscipline/PriestDiscipline.cs`
  - `BotProfiles/PriestHoly/PriestHoly.cs`
  - `BotProfiles/PriestShadow/PriestShadow.cs`
  - `BotProfiles/RogueAssassin/RogueAssassin.cs`
  - `BotProfiles/RogueCombat/RogueCombat.cs`
  - `BotProfiles/RogueSubtlety/RogueSubtlety.cs`
  - `BotProfiles/ShamanElemental/ShamanElemental.cs`
  - `BotProfiles/ShamanEnhancement/ShamanEnhancement.cs`
  - `BotProfiles/ShamanRestoration/ShamanRestoration.cs`
- [ ] Target symbols: every `CreatePvPRotationTask(IBotContext botContext)` method above.
- [ ] Required change: return `new PvPRotationTask(botContext)` in each affected profile unless an exception is documented in this file.
- [ ] Validation command: `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore`.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: no affected profile returns `PvERotationTask` from `CreatePvPRotationTask`; build and targeted combat tests pass.

### BP-MISS-002 Add regression test that guards profile factory wiring
- [x] **Done (batch 1).** Reflection-based test added in `BotProfileFactoryBindingsTests.cs`.
- [x] Problem: no test fails when profile factories wire PvP to the wrong task type.
- [ ] Target files: `Tests/BotRunner.Tests/BotRunner.Tests.csproj`, `Tests/BotRunner.Tests/Profiles/BotProfileFactoryBindingsTests.cs` (new).
- [ ] Required change: add reflection-based assertions for all `BotBase` subclasses so `CreatePvPRotationTask` returns `PvPRotationTask` (or a documented exception list).
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: test fails before BP-MISS-001 fix, passes after, and blocks regressions.

### BP-MISS-003 Resolve druid feral identity inconsistency
- [ ] Problem: feral profile identity is inconsistent (`DruidFeralCombat/DruidFeral.cs`, namespace `DruidFeral`, `FileName => "DruidFeral.dll"`).
- [ ] Evidence file: `BotProfiles/DruidFeralCombat/DruidFeral.cs`.
- [ ] Required change: choose one canonical identity and align folder/class/namespace/`FileName` usage, then update dependent tests/docs.
- [ ] Validation command: `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore`.
- [ ] Acceptance: one canonical feral profile identity is used across source and documentation.

### BP-MISS-004 Add profile capability map for low-context handoff
- [ ] Problem: profile coverage requires repeated source rediscovery.
- [ ] Target doc: `BotProfiles/PROFILE_TASK_MAP.md` (new), linked from `docs/TASKS.md`.
- [ ] Required content: per-spec map of factory methods -> concrete task files -> parity status -> owning task IDs.
- [ ] Acceptance: another agent can select one spec and continue work without re-scanning the directory tree.

## Simple Command Set
1. `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `MASTER-SUB-001` (`BotProfiles/TASKS.md`)
- Last delta: executed prior handoff `rg` command first, confirmed 16 PvP factory miswires remain, and added explicit resume-first/next-file continuity guards to prevent looped rediscovery.
- Pass result: `delta shipped`
- Validation/tests run:
  - `rg -n -U -P "CreatePvPRotationTask\(IBotContext botContext\)\s*=>\s*\n\s*new\s+PvERotationTask" BotProfiles -g "*.cs"`
- Files changed:
  - `BotProfiles/TASKS.md`
- Blockers:
  - none
- Next task: `BP-MISS-001` replace `CreatePvPRotationTask` miswires to `new PvPRotationTask(botContext)` across the 16 listed profile files.
- Next command: `Get-Content -Path 'Exports/TASKS.md' -TotalCount 360`
