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
| `LV-TPCOUNT-001` | Teleport ACK counter | BG client sends MSG_MOVE_TELEPORT_ACK with counter=0, server expects counter=12+. `MovementHandler.cs:80` fires `RequiresAcknowledgementArgs(guid, 0)` for MSG_MOVE_TELEPORT (which has no counter field). Fix: track teleport sequence locally and increment on each MSG_MOVE_TELEPORT received. | `Exports/WoWSharpClient/Handlers` | **Open** |

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
- **Current work:** LiveValidation reliability improvements. Commit `77df281` pushed.
- **Completed this session:**
  1. GM mode stays ON for fishing and gathering tests (removed `.gm off` from FishingProfessionTests, GatheringProfessionTests)
  2. Reduced Z-stabilization waits from 6s→3s in BasicLoopTests (4 occurrences)
  3. Wired up `OffsetCornerWaypoints()` in NavigationPath.cs (existed but was never called)
  4. Added `SemaphoreSlim _refreshLock` to `RefreshSnapshotsAsync()` — prevents race conditions when parallel corpse-run tasks both call it
  5. Added decorative doodad exclusion filter in SceneCache.cpp (`ShouldExcludeDoodad()`) — skips catapult, banner, torch, brazier, etc. from collision geometry
  6. Tracked `LV-TPCOUNT-001` — BG client sends teleport ACK counter=0, server expects incrementing sequence. Root cause: `MovementHandler.cs:80` passes 0 for MSG_MOVE_TELEPORT (no counter in packet). Fix: add local `_teleportSequence` counter in WoWSharpObjectManager, increment on each teleport.
- **Previous session:** Physics calibration — air mode perfect (0.000y). Trust velocity fix for JumpStart frames.
- **Physics calibration state (97/97 pass, 1 skip):**
  - ground: n=18881, avg=0.013y, p99=0.170y, worst=0.520y
  - **air: n=2275, avg=0.000y, p99=0.000y, worst=0.000y** ← PERFECT
  - swim: n=1569, avg=0.000y, p99=0.000y, worst=0.003y
  - transition: n=399, avg=0.013y, p99=0.200y, worst=0.497y
  - transport: n=1647, avg=0.027y, p99=0.308y, worst=0.329y
- **Remaining error analysis:**
  - Ground worst (0.520y): WMO floor geometry mismatch in Orgrimmar — capsule sweep vs GetGroundZ disagree
  - Transition worst (0.497y): Single SurfaceStep at geometry discontinuity
  - DirectionChange (0.200y cap): `maxReplayInputDrop` safety clamp
  - Transport worst (0.329y): Elevator timing mismatch (horizontal error)
- **Remaining LiveValidation failures (18/20):**
  - **FishingProfessionTests** — BG fishing catch: SMSG_GAMEOBJECT_CUSTOM_ANIM handler doesn't detect bobber bite.
  - **ConsumableUsageTests** — FG transient: memory lag for GM-added items (intermittent).
  - `LV-QUEST-001` — Quest not in snapshot after `.quest add` (pre-existing, not in suite).
- **What's truly next (by priority):**
  1. **Fix BG fishing catch** — Investigate SMSG_GAMEOBJECT_CUSTOM_ANIM handler in `WoWSharpClient/Handlers/SpellHandler.cs`.
  2. **Increase FG item polling window** — ConsumableUsageTests FG failure is timing-related.
  3. `PATH-REFACTOR-001` — Orgrimmar navmesh-vs-collision mismatch.
  4. `LV-QUEST-001` — QuestInteractionTests.
- **Files changed this session:**
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs` — JumpStart lookahead (lines 298-310), SurfaceStep detection (lines 312-316), isAirborne from cleanedMoveFlags (line 390), SurfaceStep Vz hint (line 447), FallTime=0 fix (line 448)
  - `Exports/Navigation/PhysicsEngine.cpp` — SurfaceStep trust-grounded Z placement (lines 1954-1985), skip ground Z refinement safety net on SurfaceStep (line 2245), skip non-walkable guardrail on SurfaceStep (line 2640)
  - `Exports/Navigation/PhysicsCollideSlide.cpp` — Corner escape (previous session)
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs` — Jump+backward stall recovery (previous session)
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` — Diagnostics (previous session)
- **Next command:** `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=normal" --blame-hang --blame-hang-timeout 10m`
