# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for all local `TASKS.md` files.
- Local files hold implementation details; this file sets priority and execution order.
- When priorities conflict, this file wins.

## Rules
1. Execute one local `TASKS.md` at a time in queue order.
2. Keep handoff pointers (`current file`, `next file`) updated before switching.
3. Prefer concrete file/symbol tasks over broad behavior buckets.
4. Never blanket-kill `dotnet` or `Game.exe` — cleanup must be PID-scoped.
5. Move completed items to `docs/ARCHIVE.md`.
6. Before session handoff, update `Session Handoff` in both this file and the active local file.
7. If two consecutive passes produce no delta, record the blocker and advance to the next queued file.

## P0 — Active Priorities

| # | ID | Task | Status |
|---|-----|------|--------|
| 1 | `PATH-SMOOTH-001` | Path smoothing Parts 1-4 (adaptive radius, StringPull, LOS skip, smoothPath swap) — all features gated on `enableProbeHeuristics` | Done |
| 2 | `PATH-SMOOTH-002` | Part 5: Cliff/edge detection with GetGroundZ IPC | Done |
| 3 | `PATH-SMOOTH-003` | Part 6: Fall distance tracking in C++ PhysicsEngine | Done |
| 4 | `PATH-SMOOTH-004` | Part 7: Gap jump detection | Done |
| 5 | `DYNOBJ-001` | nearbyObjects IPC pipeline (proto + service + marshal). Caller integration pending. | Done |
| 6 | `FISH-001` | Fix FishingProfessionTests: BG bobber not tracked. Diagnostic logging added to `WoWSharpObjectManager` (GO CREATE) and `SpellHandler.HandleGameObjectCustomAnim`. Needs live test run to capture output. | In Progress |
| 7 | `FG-STUCK-001` | FG ghost stuck on terrain — WoW.exe native limitation, not a code bug. Recovery logic exists (`RecoverRunbackStall()`). Soft FG assertions are correct. | Won't Fix |
| 8 | — | Complete missing-implementation backlog (section below) | Open |
| 9 | `DB-CLEAN-001` | Clean up game object spawns with 0% spawn chance from MaNGOS DB (read-only audit + SOAP cleanup) | Low Priority |

## P1 — Missing Implementation Inventory

### Exports/BotRunner
- [ ] `BR-MISS-001` `ScanForQuestUnitsTask` TODO in `QuestingTask.cs:52` — needs quest system design

### Exports/WoWSharpClient
- [x] `WSC-MISS-001` Missing `WoWPlayer` fields — 11 properties added (ChosenTitle, KnownTitles, etc.) + CopyFrom + switch wiring
- [x] `WSC-MISS-002` `CMSG_CANCEL_AURA` send path — `CancelAura()` on ObjectManager + `DismissBuff()` on WoWUnit
- [x] `WSC-MISS-003` Custom navigation strategy — downgraded to Debug log (valid no-op for callers handling navigation externally)
- [ ] `WSC-MISS-004` Placeholder quest reward selection strategy — needs design

### Exports/Navigation
- [ ] `NAV-MISS-001` `OverlapCapsule` stub in `PhysicsTestExports.cpp` — needs C++ MSBuild + scene query integration
- [ ] `NAV-MISS-002` `returnPhysMat`/`backfaceCulling` in `SceneQuery.h` — design stubs for future physics material queries
- [x] `NAV-MISS-003` PathFinder debug path — replaced hardcoded `C:\Users\Drew\...` with printf

### Exports/WinImports
- [x] `WINIMP-MISS-001` Empty `SafeInjection.cs` deleted (implementation is nested in WinProcessImports.cs)
- [x] `WINIMP-MISS-002` Duplicate P/Invoke declarations normalized — removed raw-uint set, kept typed-enum set, updated SafeInjection call site
- [x] `WINIMP-MISS-003` `CancellationToken` added to `WoWProcessDetector.WaitForProcessReadyAsync` — plumbed through to Task.Delay and monitor methods
- [x] `WINIMP-MISS-004` VK_A constant fixed (`0x53` → `0x41`, was duplicate of VK_S)

### Services/ForegroundBotRunner
- [x] `FG-MISS-001` `NotImplementedException` in `WoWObject.cs` → safe defaults (0, null, empty)
- [x] `FG-MISS-002` `NotImplementedException` in `WoWUnit.cs` → safe defaults (~50 properties)
- [x] `FG-MISS-003` `NotImplementedException` in `WoWPlayer.cs` → safe defaults (~35 properties)
- [ ] `FG-MISS-004` Regression checks for FG snapshot paths — needs test design

### Services
- [x] `PHS-MISS-001` `NotImplementedException` → `ArgumentException` in `PromptFunctionBase.cs:47`
- [x] `WSM-MISS-001` PathfindingService readiness gate — fail-fast on unavailability instead of proceeding
- [x] `WSM-MISS-003` `StopManagedService` → `StopManagedServiceAsync` with awaited stop + timeout (no more fire-and-forget)
- [x] `DES-MISS-003` FileSystemWatcher lifetime fixed — stored as field, IDisposable implemented
- [x] `DES-MISS-004` Null/empty path validation added to CombatPredictionService + DecisionEngine constructors
- [x] `WSM-MISS-002` Dead pathfinding bootstrap helpers removed from Program.cs (EnsurePathfindingServiceIsAvailable, LaunchPathfindingServiceExecutable, WaitForPathfindingServiceToStart)
- [x] `DES-MISS-001` CombatModelServiceListener pass-through replaced with prediction-backed handler
- [x] `DES-MISS-002` DecisionEngineWorker heartbeat spam replaced with idle-wait + lifecycle logging (full wiring deferred — needs config)
- [x] `BBR-MISS-003` BackgroundBotWorker StopAsync override added — deterministic teardown of bot runner + agent factory on shutdown
- [x] `PFS-MISS-003` Protobuf→native path mode mapping clarified — `req.Straight` → local `smoothPath` variable + log labels fixed
- [x] `PFS-MISS-005` Fail-fast on missing nav data — `Environment.Exit(1)` instead of warning-and-continue

### Services/CppCodeIntelligenceMCP (Deferred — unused service)
- [x] `CPPMCP-BLD-001` System.Text.Json package downgrade fixed (8.0.5 → 9.0.5)
- [x] `CPPMCP-ARCH-002` 10 zero-byte tool placeholder files deleted
- [x] `CPPMCP-MISS-002` Symbol usage analysis `IsUsed` flag — documented as deferred (needs AST-level resolution)
- [ ] ~~`CPPMCP-MISS-001` File analysis response — deprioritized (service unused)~~

### Services/LoggingMCPServer (Deferred — unused service)
- [x] `LMCP-MISS-001` Dead code files deleted
- [x] `LMCP-MISS-002` Duplicate class definitions removed
- [x] `LMCP-MISS-003` GetRecentLogs fixed — non-destructive snapshot
- [ ] ~~`LMCP-MISS-004..006` — deprioritized (service unused)~~

### UI
- [x] `UI-MISS-001` `ConvertBack` → `Binding.DoNothing` in `GreaterThanZeroToBooleanConverter.cs`

### GameData.Core
- [x] `GDC-MISS-001` `DeathState.cs` FIXME → clear XML docs with player vs creature semantics

### BotProfiles
- [x] `BP-MISS-001` 16 miswired PvP factories fixed → `new PvPRotationTask(botContext)`
- [x] `BP-MISS-002` Regression test for profile factory wiring — reflection-based test guards PvP↔PvE cross-wiring

### Storage (S3/Azure stubs — deferred, pending NuGet dependencies)
- [ ] `RTS-MISS-001` S3 ops — requires AWSSDK.S3 NuGet package
- [ ] `RTS-MISS-002` Azure ops — requires Azure.Storage.Blobs NuGet package
- [ ] `WRTS-MISS-001` S3 ops — requires AWSSDK.S3 NuGet package
- [ ] `WRTS-MISS-002` Azure ops — requires Azure.Storage.Blobs NuGet package

### Tests
- [x] `WSC-TST-001` TODO redundancy comments removed from `SMSG_UPDATE_OBJECT_Tests.cs` and `OpcodeHandler_Tests.cs`

## Canonical Commands

```bash
# Corpse-run validation
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Pathfinding service tests
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings

# Combined live validation (crafting + corpse)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CraftingProfessionTests"
```

## Sub-TASKS Execution Queue

| # | Local file | Status | Next IDs |
|---|-----------|--------|----------|
| 1 | `BotProfiles/TASKS.md` | **Partial** | BP-MISS-001/002 done, BP-MISS-003/004 pending |
| 2 | `Exports/TASKS.md` | Pending | EXP-UMB-001..004 (documentation) |
| 3 | `Exports/BotCommLayer/TASKS.md` | Pending | BCL-MISS-001..004 |
| 4 | `Exports/BotRunner/TASKS.md` | Pending | BR-MISS-001..003 |
| 5 | `Exports/GameData.Core/TASKS.md` | **Partial** | GDC-MISS-001 done, GDC-MISS-002..003 pending |
| 6 | `Exports/Loader/TASKS.md` | Pending | LDR-MISS-001..003 |
| 7 | `Exports/Navigation/TASKS.md` | **Partial** | NAV-MISS-003 done, NAV-MISS-001/002/004 pending |
| 8 | `Exports/WinImports/TASKS.md` | **Partial** | WINIMP-MISS-001/002/003/004 done, 005 pending |
| 9 | `Exports/WoWSharpClient/TASKS.md` | **Partial** | WSC-MISS-001/002/003 done, WSC-MISS-004 pending |
| 10 | `RecordedTests.PathingTests/TASKS.md` | Pending | RPT-MISS-001..005 |
| 11 | `RecordedTests.Shared/TASKS.md` | Pending | RTS-MISS-001..004 |
| 12 | `Services/TASKS.md` | Pending | SRV-UMB-001..004 |
| 13 | `Services/BackgroundBotRunner/TASKS.md` | **Partial** | BBR-MISS-003 done, BBR-MISS-001/002/004/005 pending |
| 14 | `Services/CppCodeIntelligenceMCP/TASKS.md` | **Deferred** | Unused service — deprioritized per user |
| 15 | `Services/DecisionEngineService/TASKS.md` | **Partial** | DES-MISS-001/002/003/004 done, DES-MISS-005 pending |
| 16 | `Services/ForegroundBotRunner/TASKS.md` | **Partial** | FG-MISS-001/002/003 done, FG-MISS-004/005 pending |
| 17 | `Services/LoggingMCPServer/TASKS.md` | **Deferred** | Unused service — deprioritized per user |
| 18 | `Services/PathfindingService/TASKS.md` | **Partial** | PFS-MISS-003/005 done, PFS-MISS-001/002/004/006/007 pending |
| 19 | `Services/PromptHandlingService/TASKS.md` | **Partial** | PHS-MISS-001 done, PHS-MISS-002..003 pending |
| 20 | `Services/WoWStateManager/TASKS.md` | **Partial** | WSM-MISS-001/002/003 done, WSM-MISS-004/005 pending |
| 21 | `Tests/TASKS.md` | Pending | TST-UMB-001..005 |
| 22 | `Tests/BotRunner.Tests/TASKS.md` | Pending | BRT-CR-001..PAR-002 |
| 23 | `Tests/Navigation.Physics.Tests/TASKS.md` | **Done** | NPT-MISS-001..003 shipped |
| 24 | `Tests/PathfindingService.Tests/TASKS.md` | Pending | PFS-TST-001..006 |
| 25 | `Tests/PromptHandlingService.Tests/TASKS.md` | Pending | PHS-TST-001..005 |
| 26-41 | Remaining test/UI/AI projects | Pending | See local files |

## Session Handoff
- **Last updated:** 2026-02-27
- **Current work:** Quick-fix sweep batch 2 — 5 additional items completed. LoggingMCPServer + CppCodeIntelligenceMCP deprioritized per user.
- **Last delta (this session):**
  - `BP-MISS-002`: Reflection-based regression test for profile factory wiring (4 tests, all pass)
  - `BBR-MISS-003`: BackgroundBotWorker StopAsync override — deterministic teardown of bot runner + agent factory
  - `DES-MISS-001`: CombatModelServiceListener now calls DecisionEngine.GetNextActions instead of base pass-through
  - `DES-MISS-002`: DecisionEngineWorker heartbeat spam removed, replaced with idle-wait + lifecycle logging
  - `WSM-MISS-002`: 3 dead pathfinding bootstrap helpers removed (~95 LOC: EnsurePathfindingServiceIsAvailable, LaunchPathfindingServiceExecutable, WaitForPathfindingServiceToStart)
  - LoggingMCPServer and CppCodeIntelligenceMCP tasks marked as deferred (unused services)
- **Remaining open items:**
  - Design stubs: BR-MISS-001, WSC-MISS-004, NAV-MISS-001/002, FG-MISS-004/005
  - Service hardening: BBR-MISS-001/002/004/005, WSM-MISS-004/005, DES-MISS-005
  - Deferred (NuGet): RTS-MISS-001/002, WRTS-MISS-001/002
  - Deferred (unused): CPPMCP-MISS-001, LMCP-MISS-004..006
  - Sub-TASKS queue: ~75 remaining items across local TASKS.md files
- **Next task:** Continue quick-fix sweep. Next targets: PFS-MISS-001/002, BotCommLayer (BCL-MISS-002/003), Loader (LDR-MISS-002/003).
