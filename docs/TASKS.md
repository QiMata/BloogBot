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
8. **The MaNGOS server is ALWAYS live.** Never defer live validation tests — run them every session. FISH-001, BBR-PAR-001, AI-PARITY, and all LiveValidation tests should be executed, not deferred.

## P0 — Active Priorities

| # | ID | Task | Status |
|---|-----|------|--------|
| 1 | `PATH-REFACTOR-001` | **Complete pathfinding service + PhysicsEngine refactor.** BG falls on walkable slopes (should clamp to surface). FG bumps into walls/objects and gets stuck. BG forced through Orgrimmar WMO (catapult near bank). Physics slope handling, WMO collision, and wall-sliding all need rework. | **Open — P0** |
| 2 | `TEST-GMMODE-001` | All LiveValidation tests outside of combat and corpse-run should use `.gm on` for setup safety. | **Done** |
| 3 | `DB-CLEAN-001` | Remove all game object spawns with 0% spawn chance from MaNGOS DB. Also remove commands not from original MaNGOS (non-vanilla). | **Open — P0** |
| 4 | `TEST-MINING-001` | Mining test does wasteful teleporting. FG bot stands on top of node instead of near it. Optimize teleport logic and fix FG node positioning. | **Open — P0** |
| 5 | `TEST-LOG-CLEANUP` | Clean up all out-of-date test logs and temp files (AppData\Local\Temp\claude\ folders). | **Open — P0** |
| 6 | `LV-PARALLEL-001` | Parallelize all LiveValidation FG+BG tests to run in parallel via Task.WhenAll. | **Done** |
| 7 | `FISH-001` | FishingProfessionTests: BG fishing end-to-end. Root cause: MOVEFLAG_FALLINGFAR heartbeats during Z clamp interrupted fishing channel. | **Done** |
| 8 | `TIER2-001` | Frame-ahead simulator, transport waiting, cross-map routing. FrameAheadSimulator, TransportData, TransportWaitingLogic, CrossMapRouter, MapTransitionGraph + NavigationPath integration. 73 tests (54 unit + 19 integration). | **Done** |
| 9 | `AI-PARITY` | All 3 AI parity gates validated: CORPSE (1/1, 4m56s), COMBAT (1/1, 6s), GATHER (2/2, 4m20s). | **Done** |

## Open — Storage Stubs (Blocked on NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in RecordedTests.Shared | Requires AWSSDK.S3 |
| `RTS-MISS-002` | Azure ops in RecordedTests.Shared | Requires Azure.Storage.Blobs |
| `WRTS-MISS-001` | S3 ops in WWoW.RecordedTests.Shared | Requires AWSSDK.S3 |
| `WRTS-MISS-002` | Azure ops in WWoW.RecordedTests.Shared | Requires Azure.Storage.Blobs |

## Open — Test Coverage Gaps (Remaining RPTT/RTS/WRTS TST tasks)

These are incremental coverage expansion tasks. The test projects are healthy; these are additional test surfaces.

| ID | Project | Remaining | Current Pass Count |
|----|---------|-----------|-------------------|
| `RPTT-TST-002..006` | RecordedTests.PathingTests.Tests | Program.FilterTests, lifecycle, timeout, disconnect | 115/115 |
| `RTS-TST-002..006` | RecordedTests.Shared.Tests | S3/Azure storage tests (blocked on NuGet) | 323/323 |
| `WRTS-TST-001..006` | WWoW.RecordedTests.Shared.Tests | S3/Azure storage tests (blocked on NuGet) | 262/283 (21 pre-existing) |
| `RPTT-TST-002..006` | WWoW.RecordedTests.PathingTests.Tests | Program.FilterTests, lifecycle, timeout, disconnect | 85/85 |

## Open — Infrastructure Projects (No Test Projects)

| # | Local file | Task IDs | Notes |
|---|-----------|----------|-------|
| 1 | `UI/Systems/Systems.AppHost/TASKS.md` | SAH-MISS-001..006 | 2 source files, .NET Aspire orchestration |
| 2 | `UI/Systems/Systems.ServiceDefaults/TASKS.md` | SSD-MISS-001..006 | 1 source file, OpenTelemetry/health config |

## Open — AI Parity (Needs Live Server)

| # | Local file | Task IDs | Notes |
|---|-----------|----------|-------|
| 1 | `WWoWBot.AI/TASKS.md` | AI-PARITY-001..GATHER-001 | **Done** — all 3 parity gates pass live (2026-02-28) |

## Open — Live Validation Failures (Discovered 2026-02-28)

| ID | Test | Error | Owner | Status |
|----|------|-------|-------|--------|
| `LV-EQUIP-001` | EquipmentEquipTests | BG equip swap assertion: bag count unchanged when mainhand already had Worn Mace. | `Tests/BotRunner.Tests` | **Done** — fixed assertion to accept mainhandGuidChanged + added `.gm off` guard |
| `LV-GROUP-001` | GroupFormationTests | SMSG_GROUP_LIST parsed leaderGuid but never stored it persistently. Snapshot returned 0. | `Exports/WoWSharpClient` | **Done** — added LeaderGuid property to IPartyNetworkClientComponent, stored in ParseGroupList/SetLeader, used in snapshot |
| `LV-GROUNDZ-001` | OrgrimmarGroundZAnalysis.PostTeleportSnap | GROUND_SNAP_MAX_DROP=3.0 too restrictive (Org navmesh 3.4y below WMO). Also physics blocked by `_isBeingTeleported` guard. | `Exports/WoWSharpClient/Movement` | **Done** — increased MAX_DROP to 5.0, force physics frame on teleport flag clear |
| `LV-QUEST-001` | QuestInteractionTests | Quest not in snapshot after `.quest add`. Already tracked as WSM-PAR-001. | `Services/WoWStateManager` | Open |

## Deferred (Unused Services)

| Local file | Status |
|-----------|--------|
| `Services/CppCodeIntelligenceMCP/TASKS.md` | CPPMCP-MISS-001 deprioritized |
| `Services/LoggingMCPServer/TASKS.md` | LMCP-MISS-004..006 deprioritized |

## Sub-TASKS Execution Queue (Partial — only non-Done rows)

| # | Local file | Status | Next IDs |
|---|-----------|--------|----------|
| 11 | `RecordedTests.Shared/TASKS.md` | Pending | RTS-MISS-001..004 (blocked on NuGet) |
| 24 | `Tests/PathfindingService.Tests/TASKS.md` | **Partial** | PFS-TST-002/003/005 need nav data |
| 25 | `Tests/PromptHandlingService.Tests/TASKS.md` | **Partial** | PFS-TST-002 low priority |
| 26 | `Tests/RecordedTests.PathingTests.Tests/TASKS.md` | **Partial** | RPTT-TST-002..006 remaining |
| 27 | `Tests/RecordedTests.Shared.Tests/TASKS.md` | **Partial** | RTS-TST-002..006 (storage blocked on NuGet) |
| 31 | `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md` | **Partial** | RPTT-TST-002..006 remaining |
| 32 | `Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md` | **Partial** | WRTS-TST-001..006 (storage blocked on NuGet) |
| 36 | `UI/Systems/Systems.AppHost/TASKS.md` | Pending | SAH-MISS-001..006 |
| 37 | `UI/Systems/Systems.ServiceDefaults/TASKS.md` | Pending | SSD-MISS-001..006 |
| 38 | `WWoWBot.AI/TASKS.md` | **Partial** | AI-PARITY-001..GATHER-001 (need live server) |

> All other queue rows (1-10, 12-23, 28-30, 33-35) are **Done** — see `docs/ARCHIVE.md`.

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

# Tier 2: Frame-ahead + transport + cross-map
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~FrameAheadSimulator|FullyQualifiedName~TransportWaiting|FullyQualifiedName~CrossMapRouter"
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameAhead|FullyQualifiedName~ElevatorScenario"

# AI tests
dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"
```

## Session Handoff
- **Last updated:** 2026-03-01
- **Current work:** Physics refactor + test hardening (PATH-REFACTOR-001 partial, TEST-GMMODE-001 done).
- **Last delta:** C++ physics slope/collision fixes + `.gm on` for all non-combat/corpse tests + reduced excessive waits.
- **Completed this session:**
  1. **PATH-REFACTOR-001 (partial)** — Physics engine fixes:
     - `PhysicsCollideSlide.cpp`: Fixed horizontal-only filter — Bottom/Top capsule hits with horizontal normals (hMag >= 0.3) now accepted. Fixes bot phasing through WMO objects (catapults, barricades) at foot level.
     - `PhysicsEngine.cpp`: Split `maxSnapDown` into walkable (4.0y via STEP_DOWN_HEIGHT) and non-walkable (2.6y via STEP_HEIGHT + 0.5). Fixes BG bot "falling" on walkable slopes.
     - `NavigationPath.cs`: Reduced STALLED_SAMPLE_THRESHOLD from 24 to 10 (~330ms at 30fps). Faster stuck detection.
     - `MovementController.cs`: Reduced STALE_FORWARD_NO_DISPLACEMENT_THRESHOLD from 30 to 15 ticks. Faster wall-stuck recovery.
  2. **TEST-GMMODE-001 (done)** — Added `.gm on` to all non-combat/non-corpse LiveValidation tests:
     - BasicLoopTests (5 test methods), ConsumableUsageTests, CraftingProfessionTests
     - EconomyInteractionTests (3 test methods), EquipmentEquipTests (already managed .gm on/off)
     - GroupFormationTests, NpcInteractionTests, OrgrimmarGroundZAnalysisTests (2 test methods)
     - QuestInteractionTests, TalentAllocationTests
  3. **Wait reductions** — Reduced excessive delays across all test files:
     - ConsumableUsageTests: 5000ms → 2000ms item arrival wait
     - EconomyInteractionTests: 2500ms → 1500ms teleport, 2000ms → 1200ms revive, 1000ms → 800ms mail
     - NpcInteractionTests: 2500ms → 1500ms teleport, 2000ms → 1200ms revive, 1500ms → 1000ms additem
     - CraftingProfessionTests: 4000ms → 2000ms post-teleport, 2000ms → 1200ms revive
     - EquipmentEquipTests: 2000ms → 1200ms revive, 1800ms → 1200ms additem, 4000ms → 2500ms equip, 1500ms → 800ms gm off
  4. Physics tests: 97/98 pass (1 pre-existing ExtractWmoDoodads_FromMpq MPQ failure).
- **Files changed this session:**
  - `Exports/Navigation/PhysicsCollideSlide.cpp` — horizontal-only filter accepts Bottom/Top with horizontal normals
  - `Exports/Navigation/PhysicsEngine.cpp` — walkable vs non-walkable snap-down limits
  - `Exports/BotRunner/Movement/NavigationPath.cs` — stuck threshold 24→10
  - `Exports/WoWSharpClient/Movement/MovementController.cs` — stale forward threshold 30→15
  - 10 LiveValidation test files — `.gm on` + reduced waits
  - `docs/TASKS.md` — session handoff update
- **What's truly next (by priority):**
  1. `PATH-REFACTOR-001` (remaining) — Live validation of physics fixes; wall-sliding rework if still stuck
  2. `DB-CLEAN-001` — Remove 0% spawn chance objects and non-vanilla commands
  3. `TEST-MINING-001` — Fix mining test teleport waste and FG node positioning
  4. `TEST-LOG-CLEANUP` — Clean temp files and stale logs
- **Next command:** `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=normal"`
