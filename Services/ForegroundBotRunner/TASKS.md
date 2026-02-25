# ForegroundBotRunner Tasks

Master tracker: `MASTER-SUB-016`

## Scope
- Directory: `Services/ForegroundBotRunner`
- Focus: remove FG object-model throw paths that break corpse/combat/gathering parity and stabilization tests.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Keep this file implementation-focused on FG object/materialization behavior only.
2. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
3. Every validation cycle must compare FG and BG behavior for the same scenario.
4. On completion, move finished items to `Services/ForegroundBotRunner/TASKS_ARCHIVE.md` in the same session.
5. If two runs in a row produce no code delta, record blocker + exact next command in `Session Handoff` and advance to the next queued file in `docs/TASKS.md`.
6. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Build check passes: `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore` -> `0 Error(s)`, `0 Warning(s)`; output still contains a non-blocking `dumpbin` missing message from `vcpkg ... applocal.ps1`.
- `NotImplementedException` baseline:
  - `WoWObject.cs`: `31` throw matches across lines `195-231`.
  - `WoWUnit.cs`: `56` throw matches across lines `244-559`.
  - `WoWPlayer.cs`: `49` throw matches across lines `41-252`.
- TODO carryover requiring explicit keep/implement/defer:
  - `Services/ForegroundBotRunner/Mem/MemoryAddresses.cs:137`
  - `Services/ForegroundBotRunner/Mem/AntiWarden/WardenDisabler.cs:214`
  - `Services/ForegroundBotRunner/Mem/AntiWarden/WardenDisabler.cs:386`

## P0 Active Tasks (Ordered)
1. [ ] `FG-MISS-001` Remove throw paths in `WoWObject.cs`.
- Problem: movement/spline/transport accessors can still throw during snapshot/materialization.
- Target files: `Services/ForegroundBotRunner/Objects/WoWObject.cs`.
- Required change: replace all `NotImplementedException` members with descriptor-backed values or safe defaults that preserve current call contracts.
- Validation command: `rg --line-number "throw new NotImplementedException\\(\\)" Services/ForegroundBotRunner/Objects/WoWObject.cs`
- Acceptance criteria: command returns no matches.

2. [ ] `FG-MISS-002` Remove throw paths in `WoWUnit.cs`.
- Problem: target/power/combat stat and movement relation fields can still throw.
- Target files: `Services/ForegroundBotRunner/Objects/WoWUnit.cs`.
- Required change: implement all currently-thrown members with stable value reads/defaults so corpse/combat/gathering flows do not fail.
- Validation command: `rg --line-number "throw new NotImplementedException\\(\\)" Services/ForegroundBotRunner/Objects/WoWUnit.cs`
- Acceptance criteria: command returns no matches.

3. [ ] `FG-MISS-003` Remove throw paths in `WoWPlayer.cs`.
- Problem: race/class/guild/inventory/xp/combat-rating members can still throw.
- Target files: `Services/ForegroundBotRunner/Objects/WoWPlayer.cs`.
- Required change: implement all currently-thrown members with safe, deterministic value reads/defaults used by active bot logic.
- Validation command: `rg --line-number "throw new NotImplementedException\\(\\)" Services/ForegroundBotRunner/Objects/WoWPlayer.cs`
- Acceptance criteria: command returns no matches.

4. [ ] `FG-MISS-004` Add regression gate for FG materialization throws.
- Problem: throw regressions can re-enter without a dedicated test guard.
- Target files: `Tests/BotRunner.Tests` (corpse/combat/gathering slices) and supporting FG test utilities.
- Required change: add tests asserting that exercised FG object-member reads do not throw `NotImplementedException`.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~Combat|FullyQualifiedName~Gather" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: guard fails when a throw is reintroduced and passes on current implementation.

5. [ ] `FG-MISS-005` Triage remaining FG memory/warden TODOs.
- Problem: non-object TODO markers are untracked implementation risk.
- Target files: `Services/ForegroundBotRunner/Mem/MemoryAddresses.cs`, `Services/ForegroundBotRunner/Mem/AntiWarden/WardenDisabler.cs`.
- Required change: create explicit task IDs or explicit defer rationale for each TODO marker.
- Validation command: `rg -n "TODO" Services/ForegroundBotRunner/Mem/MemoryAddresses.cs Services/ForegroundBotRunner/Mem/AntiWarden/WardenDisabler.cs`
- Acceptance criteria: each TODO has a linked task ID or documented defer decision in this file.

## Simple Command Set
1. Build: `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj --configuration Release --no-restore`
2. Corpse-run smoke: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. FG parity slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~Combat|FullyQualifiedName~Gather" --logger "console;verbosity=minimal"`
4. Repo-scoped cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Pass result: `delta shipped`
- Last delta: converted to execution-card format with refreshed evidence and explicit per-task problem/target/validation/acceptance structure.
- Next task: `FG-MISS-001`
- Next command: `rg --line-number "throw new NotImplementedException\\(\\)" Services/ForegroundBotRunner/Objects/WoWObject.cs`
- Blockers: none
