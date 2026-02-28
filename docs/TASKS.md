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
| 9 | `SOAP-CMD-001` | Harden SOAP `ExecuteGMCommandAsync` — throw on "no such command" FAULT, fix `.teleport` → `.tele`/`.go xyz` in all PathingTestDefinitions, consolidate duplicated `ContainsCommandRejection` | Done (partial — throw + command fixes done; ContainsCommandRejection consolidation deferred) |
| 10 | `DB-CLEAN-001` | Clean up game object spawns with 0% spawn chance from MaNGOS DB (read-only audit + SOAP cleanup) | Low Priority |

## P1 — Missing Implementation Inventory

### Exports/BotRunner
- [x] `BR-MISS-001` `ScanForQuestUnitsTask` TODO → defer rationale in `QuestingTask.cs:51` (needs quest objective→unit mapping + NPC filter design)
- [x] `BR-MISS-002` Corpse-run setup fixed to Orgrimmar with reclaim gating — code-complete (live validation deferred)
- [x] `BR-MISS-003` Snapshot fallback logging — bare `catch { }` blocks replaced with `TryPopulate()` helper emitting `[Snapshot] {Field} unavailable: {Type}` at Debug level

### Exports/WoWSharpClient
- [x] `WSC-MISS-001` Missing `WoWPlayer` fields — 11 properties added (ChosenTitle, KnownTitles, etc.) + CopyFrom + switch wiring
- [x] `WSC-MISS-002` `CMSG_CANCEL_AURA` send path — `CancelAura()` on ObjectManager + `DismissBuff()` on WoWUnit
- [x] `WSC-MISS-003` Custom navigation strategy — downgraded to Debug log (valid no-op for callers handling navigation externally)
- [x] `WSC-MISS-004` Placeholder quest reward selection — strategy-aware SelectRewardIndex with HighestValue/BestForClass/BestStatUpgrade/MostNeeded

### Exports/Navigation
- [x] `NAV-MISS-001` `OverlapCapsule` export in `PhysicsTestExports.cpp` — implemented: routes to `SceneQuery::OverlapCapsule` via VMapManager2/StaticMapTree lookup
- [x] `NAV-MISS-002` `returnPhysMat`/`backfaceCulling` in `SceneQuery.h` — resolved: comments updated to "Reserved" with explicit behavior documentation (not evaluated by current paths)
- [x] `NAV-MISS-003` PathFinder debug path — replaced hardcoded `C:\Users\Drew\...` with printf
- [x] `NAV-MISS-004` Corpse runback path consumption — code-complete (live validation deferred)

### Exports/WinImports
- [x] `WINIMP-MISS-001` Empty `SafeInjection.cs` deleted (implementation is nested in WinProcessImports.cs)
- [x] `WINIMP-MISS-002` Duplicate P/Invoke declarations normalized — removed raw-uint set, kept typed-enum set, updated SafeInjection call site
- [x] `WINIMP-MISS-003` `CancellationToken` added to `WoWProcessDetector.WaitForProcessReadyAsync` — plumbed through to Task.Delay and monitor methods
- [x] `WINIMP-MISS-004` VK_A constant fixed (`0x53` → `0x41`, was duplicate of VK_S)

### Services/ForegroundBotRunner
- [x] `FG-MISS-001` `NotImplementedException` in `WoWObject.cs` → safe defaults (0, null, empty)
- [x] `FG-MISS-002` `NotImplementedException` in `WoWUnit.cs` → safe defaults (~50 properties)
- [x] `FG-MISS-003` `NotImplementedException` in `WoWPlayer.cs` → safe defaults (~35 properties)
- [x] `FG-MISS-004` Regression gate for FG materialization throws — 4 source-scanning tests in ForegroundObjectRegressionTests.cs
- [x] `FG-MISS-005` Triage FG memory/warden TODOs — all triaged with explicit IDs (FG-WARDEN-001/002) or defer rationale

### Services
- [x] `PHS-MISS-001` `NotImplementedException` → `ArgumentException` in `PromptFunctionBase.cs:47`
- [x] `WSM-MISS-001` PathfindingService readiness gate — fail-fast on unavailability instead of proceeding
- [x] `WSM-MISS-003` `StopManagedService` → `StopManagedServiceAsync` with awaited stop + timeout (no more fire-and-forget)
- [x] `DES-MISS-003` FileSystemWatcher lifetime fixed — stored as field, IDisposable implemented
- [x] `DES-MISS-004` Null/empty path validation added to CombatPredictionService + DecisionEngine constructors
- [x] `WSM-MISS-002` Dead pathfinding bootstrap helpers removed from Program.cs (EnsurePathfindingServiceIsAvailable, LaunchPathfindingServiceExecutable, WaitForPathfindingServiceToStart)
- [x] `DES-MISS-001` CombatModelServiceListener pass-through replaced with prediction-backed handler
- [x] `DES-MISS-002` DecisionEngineWorker heartbeat spam replaced with idle-wait + lifecycle logging (full wiring deferred — needs config)
- [x] `BBR-MISS-001` Action dispatch correlation token — `[act-N]` through receive/dispatch/completion logs in BotRunnerService.cs
- [x] `BBR-MISS-002` Stuck-forward zero-displacement loops — code-complete (stall detection in RetrieveCorpseTask.cs, live validation deferred)
- [x] `BBR-MISS-003` BackgroundBotWorker StopAsync override added — deterministic teardown of bot runner + agent factory on shutdown
- [x] `BBR-MISS-004` Path consumption for corpse runback — code-complete (probe/fallback disabled, live validation deferred)
- [x] `BBR-MISS-005` Parity regression checks — code-complete (infrastructure exists, live validation deferred)
- [x] `PFS-MISS-003` Protobuf→native path mode mapping clarified — `req.Straight` → local `smoothPath` variable + log labels fixed
- [x] `PFS-MISS-005` Fail-fast on missing nav data — `Environment.Exit(1)` instead of warning-and-continue
- [x] `PFS-MISS-001` LOS fallback already gated — `WWOW_ENABLE_LOS_FALLBACK` env var disabled by default, no change needed
- [x] `PFS-MISS-002` Elevated LOS probes already diagnostics-only — `TryHasLosForFallback` only called within opt-in fallback path
- [x] `PFS-MISS-006` Orgrimmar corpse-run regression vectors — 3 tests (graveyard→center, entrance→VoS, reverse) with finite-coordinate + min-waypoint assertions

### Exports/BotCommLayer
- [x] `BCL-MISS-001` Snapshot contract parity audit — corpse lifecycle field map documented with proto field numbers, population sites, consumer patterns
- [x] `BCL-MISS-002` Death/runback serialization tests — 3 round-trip tests for ghost form, resurrection, and corpse-run movement via StateChangeResponse
- [x] `BCL-MISS-003` Socket teardown hardened — `IDisposable` on server/client types, `while(true)` → `while(_isRunning)`, client cleanup on disconnect
- [x] `BCL-MISS-004` Proto regen workflow docs — canonical command, repo-local protoc default, C++ external target documented
- [x] `WSM-MISS-004` Action queue cap/expiry — `TimestampedAction` wrapper, 50-item depth cap, 5-min TTL, explicit drop logging
- [x] `WSM-MISS-005` Action-forwarding contract tests — 24 tests (proto round-trip, dead/ghost detection, EnqueueAction, ActionType coverage)
- [x] `PHS-MISS-004` Test discovery already addressed — all methods have `[Fact(Skip)]` attributes
- [x] `PHS-MISS-002` Transfer-contract tests for PromptFunctionBase — 14 tests (TransferHistory, TransferChatHistory, TransferPromptRunner, ResetChat)
- [x] `PHS-MISS-003` System prompt preservation and InitializeChat semantics — covered in PromptFunctionBaseTransferTests
- [x] `DES-MISS-005` Decision service contract tests — 16 tests (MLModel predict/learn, GetNextActions routing, DecisionEngine lifecycle)

### Exports/Loader
- [x] `LDR-MISS-001` Loader teardown hardened — shutdown event, 5s wait with diagnostics, thread exit code logging, FreeConsole on detach
- [x] `LDR-MISS-002` Console visibility controlled by WWOW_LOADER_CONSOLE env var — suppress with =0, README updated
- [x] `LDR-MISS-003` VS-generated TODO boilerplate removed from stdafx.h/stdafx.cpp; debug stub files already deleted

### Exports/WinImports (continued)
- [x] `WINIMP-MISS-005` Cleanup evidence hooks — structured summary table in run-tests.ps1, PID/name in WoWProcessDetector detection logs

### Services/PathfindingService (continued)
- [x] `PFS-MISS-004` Path provenance metadata — `result` + `raw_corner_count` fields in CalculatePathResponse proto
- [x] `PFS-MISS-007` Path data integrity tests — 4 proto round-trip tests (count/order/precision/empty)

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
- [x] `GDC-MISS-002` Corpse lifecycle interface contract documented — XML docs on IWoWLocalPlayer + IWoWCorpse reclaim-critical fields
- [x] `GDC-MISS-003` Snapshot interface drift fixed — `IWoWActivitySnapshot : IActivitySnapshot`, removed duplicated fields, documented hierarchy

### BotProfiles
- [x] `BP-MISS-001` 16 miswired PvP factories fixed → `new PvPRotationTask(botContext)`
- [x] `BP-MISS-002` Regression test for profile factory wiring — reflection-based test guards PvP↔PvE cross-wiring
- [x] `BP-MISS-003` Druid feral identity aligned — folder `DruidFeralCombat` → `DruidFeral` to match class/namespace/FileName
- [x] `BP-MISS-004` Profile capability map — `PROFILE_TASK_MAP.md` with 27-spec factory/task file map

### Storage (S3/Azure stubs — deferred, pending NuGet dependencies)
- [ ] `RTS-MISS-001` S3 ops — requires AWSSDK.S3 NuGet package
- [ ] `RTS-MISS-002` Azure ops — requires Azure.Storage.Blobs NuGet package
- [ ] `WRTS-MISS-001` S3 ops — requires AWSSDK.S3 NuGet package
- [ ] `WRTS-MISS-002` Azure ops — requires Azure.Storage.Blobs NuGet package

### RecordedTests.PathingTests
- [x] `RPT-MISS-001` Non-cancellable orchestration paths removed — CancellationToken threaded through Program.cs, RunTestsAsync, DisposeAsync
- [x] `RPT-MISS-002` Deterministic lingering-process teardown — CleanupRepoScopedProcesses in Program.cs (PID-scoped, repo-filtered)
- [x] `RPT-MISS-003` Corpse-run scenario fixed to Orgrimmar — code-complete (live validation deferred)
- [x] `RPT-MISS-004` Path output consumption in runback — code-complete (live validation deferred)
- [x] `RPT-MISS-005` Test commands simplified — legacy `WWoW.` prefixes removed from README.md

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
| 1 | `BotProfiles/TASKS.md` | **Done** | BP-MISS-001/002/003/004 all done |
| 2 | `Exports/TASKS.md` | **Done** | EXP-UMB-001..004 verified complete |
| 3 | `Exports/BotCommLayer/TASKS.md` | **Done** | BCL-MISS-001/002/003/004 all done |
| 4 | `Exports/BotRunner/TASKS.md` | **Done** | BR-MISS-001/002/003 all done |
| 5 | `Exports/GameData.Core/TASKS.md` | **Done** | GDC-MISS-001/002/003 all done |
| 6 | `Exports/Loader/TASKS.md` | **Done** | LDR-MISS-001/002/003 all done |
| 7 | `Exports/Navigation/TASKS.md` | **Done** | NAV-MISS-001/002/003/004 all done |
| 8 | `Exports/WinImports/TASKS.md` | **Done** | WINIMP-MISS-001/002/003/004/005 all done |
| 9 | `Exports/WoWSharpClient/TASKS.md` | **Done** | WSC-MISS-001/002/003/004 all done |
| 10 | `RecordedTests.PathingTests/TASKS.md` | **Done** | RPT-MISS-001/002/003/004/005 all done |
| 11 | `RecordedTests.Shared/TASKS.md` | Pending | RTS-MISS-001..004 |
| 12 | `Services/TASKS.md` | **Done** | SRV-UMB-001..004 verified complete |
| 13 | `Services/BackgroundBotRunner/TASKS.md` | **Done** | BBR-MISS-001/002/003/004/005 all done |
| 14 | `Services/CppCodeIntelligenceMCP/TASKS.md` | **Deferred** | Unused service — deprioritized per user |
| 15 | `Services/DecisionEngineService/TASKS.md` | **Done** | DES-MISS-001/002/003/004/005 all done |
| 16 | `Services/ForegroundBotRunner/TASKS.md` | **Done** | FG-MISS-001/002/003/004/005 all done |
| 17 | `Services/LoggingMCPServer/TASKS.md` | **Deferred** | Unused service — deprioritized per user |
| 18 | `Services/PathfindingService/TASKS.md` | **Done** | PFS-MISS-001/002/003/004/005/006/007 all done |
| 19 | `Services/PromptHandlingService/TASKS.md` | **Done** | PHS-MISS-001/002/003/004 all done |
| 20 | `Services/WoWStateManager/TASKS.md` | **Done** | WSM-MISS-001/002/003/004/005 all done |
| 21 | `Tests/TASKS.md` | **Done** | TST-UMB-001..005 verified complete |
| 22 | `Tests/BotRunner.Tests/TASKS.md` | **Partial** | BRT-CR-001 done; BRT-CR-002/003, BRT-PAR-001/002 need live server |
| 23 | `Tests/Navigation.Physics.Tests/TASKS.md` | **Done** | NPT-MISS-001..003 shipped |
| 24 | `Tests/PathfindingService.Tests/TASKS.md` | **Partial** | PFS-TST-001/004/006 done; PFS-TST-002/003/005 need nav data |
| 25 | `Tests/PromptHandlingService.Tests/TASKS.md` | **Partial** | PHS-TST-001/003/004/005 done; PHS-TST-002 low priority |
| 26-41 | Remaining test/UI/AI projects | Pending | See local files |

## Session Handoff
- **Last updated:** 2026-02-28
- **Current work:** Quick-fix sweep batch 17 — PFS preflight/proto tests, PHS repository discovery, README updates.
- **Last delta (this session):**
  - Batch 16 committed (`16b3994`) — umbrella verification, test task triage.
  - Batch 17: PFS-TST-001 (preflight validates maps/vmaps/mmaps), PFS-TST-004 (5 round-trip proto tests), PFS-TST-006 (README), PHS-TST-004 (161 MangosRepository methods discovered), PHS-TST-005 (README).
- **Build verification:**
  - PathfindingService.Tests: 0 errors, 11/11 proto tests pass
  - PromptHandlingService.Tests: 0 errors, 32 pass / 173 skip / 0 fail (205 total)
- **Remaining open items:**
  - Deferred (NuGet): RTS-MISS-001/002, WRTS-MISS-001/002
  - Deferred (unused): CPPMCP-MISS-001, LMCP-MISS-004..006
  - Pending: PFS-TST-002/003/005 (need nav data), PHS-TST-002 (IPromptRunner stubs, low priority)
  - Partially done: Tests/BotRunner.Tests/TASKS.md (BRT-CR-002/003, BRT-PAR-001/002 need live server)
  - Live validation: BBR-MISS-002/004/005, BR-MISS-002, NAV-MISS-004, RPT-MISS-003/004, BRT-CR-002/003, BRT-PAR-001/002
- **Next task:** Commit batch 17, then assess remaining test project TASKS.md files (rows 26-41).
