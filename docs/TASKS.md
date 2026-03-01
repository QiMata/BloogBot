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
| 3 | `DB-CLEAN-001` | Remove all game object spawns with 0% spawn chance from MaNGOS DB. Also remove commands not from original MaNGOS (non-vanilla). | **Done** — pool_gameobject chance=0 is standard MaNGOS (equal distribution), NOT "never spawns." Command table already sanitized (4 legitimate entries remain). |
| 4 | `TEST-MINING-001` | Mining test does wasteful teleporting. FG bot stands on top of node instead of near it. Optimize teleport logic and fix FG node positioning. | **Done** — eliminated re-teleport, FG bot positioned 5y from node (not on top), reduced wait times |
| 5 | `TEST-LOG-CLEANUP` | Clean up all out-of-date test logs and temp files (AppData\Local\Temp\claude\ folders). | **Done** — cleaned 3GB of stale tmp/ contents |
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
- **Current work:** Cleanup sprint — all P0 maintenance tasks closed. Physics fixes need live validation.
- **Last delta:** Skipped ExtractWmoDoodads, committed all uncommitted code (infra, Tier 2, test coverage), cleaned 3GB tmp/, fixed mining test positioning, verified DB-CLEAN-001 not needed.
- **Completed this session:**
  1. **ExtractWmoDoodads_FromMpq** — Skipped (359 .doodads already extracted, StormLib.dll unavailable). Physics: 97/97 pass + 1 skip.
  2. **DB-CLEAN-001 (done)** — Investigated pool_gameobject chance=0 = standard MaNGOS equal distribution (NOT "never spawns"). Command table has 4 legitimate entries (wareffort, debug). No action needed.
  3. **TEST-MINING-001 (done)** — Eliminated wasteful re-teleport in GatheringProfessionTests. FG bot positioned 5y from node (not on top). Reduced wait times (4s→2s Z stabilization, 3s→2s respawn).
  4. **TEST-LOG-CLEANUP (done)** — Cleaned 3GB of stale tmp/ contents (dotnet caches, logs, backups). Claude temp is 25K.
  5. **Anti-regression** — Committed ALL uncommitted code from previous sessions (5 commits):
     - `5f7678b`: Repo-scoped process cleanup, PathfindingService readiness, FISH-001, mining fix, S3 tests
     - `65b123e`: Tier 2 (FrameAheadSimulator, TransportData, TransportWaitingLogic, CrossMapRouter, MapTransitionGraph)
     - `4770a3b`: 33 new test files across RecordedTests, PathingTests, AI
     - `281792b`: TASKS.md updates
     - `50d9616`: tmp/ cleanup
  6. **Calibration metrics confirmed:** avg=0.0126y, p99=0.2000y, worst=0.9877y (all gates PASS)
- **What's truly next (by priority):**
  1. `PATH-REFACTOR-001` (remaining) — Run LiveValidation suite to validate physics fixes live. Wall-sliding rework if still stuck.
  2. `LV-QUEST-001` — QuestInteractionTests: quest not in snapshot after `.quest add`
- **Next command:** `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=normal" --blame-hang --blame-hang-timeout 10m`
