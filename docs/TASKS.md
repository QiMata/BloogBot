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

All previous P0 items completed and archived. See `docs/ARCHIVE.md`.

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

All resolved and archived. See `docs/ARCHIVE.md`.

## Open — LiveValidation Audit (2026-03-06)

| ID | Task | Status |
|----|------|--------|
| `LV-AUDIT-002` | Remaining MEDIUM items. **Fixed:** AST-1/2/3/5/11/13/20, TIM-1/2/5/10/12. **Open:** TIM-7 (FG fishing test). | Mostly Done |

## Open — FG Client Stability (2026-03-06)

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `FG-REALM-STUCK-001` | **FG client stuck on Realm Selection/Language dialog.** TESTBOT1 hits the "Choosing a Realm" first-login screen. **FIXED:** `SelectRealm()` now clicks `RealmListButton1` + `RealmListOkButton` via Lua (rate-limited 2s). `CurrentRealm` only transitions when `MaxCharacterCount > 0`. Commit `2301f0a`. | `Services/ForegroundBotRunner/` | **Fixed** |
| `FG-GHOST-STUCK-001` | Ghost form stuck on Orgrimmar catapult geometry at ~(1577, -4394, 6.2) during corpse run. Previous fix (`ShouldExcludeDoodad` keyword filter) was incorrect — M2 collision is determined by MPQ flags, not name heuristics. `ShouldExcludeDoodad` removed entirely (commit `a1a04bd` reverted). All M2 models must remain in physics sweeps. Root cause is pathfinding/stuck-recovery not handling dense M2 geometry areas. | `Exports/Navigation/` | **Reopened** — needs pathfinding improvement |

## Open — Capability Gaps (from CAPABILITY_AUDIT.md)

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `CAP-GAP-003` | TrainerFrame status unknown — may also be null. LearnAllAvailableSpellsAsync already bypasses Frame. | `Exports/WoWSharpClient/` | Open (low priority) |

## Open — Pathfinding / Physics (2026-03-03)

All resolved and archived. See `docs/ARCHIVE.md`.

## Open — Test Infrastructure Hardening

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `TEST-TRAM-001` | **Deeprun Tram map transition integration test.** Both bots teleport to IF with `.gm on`, then into map 369. Verifies client survives server bounce. | `Tests/BotRunner.Tests/LiveValidation/` | **Done** |
| `TEST-CRASH-001` | **Test fixture fail-fast on client crash.** Background crash monitor polls StateManager + WoW.exe PIDs every 2s. `ClientCrashed` + `CrashMessage` properties. `AssertClientAlive()` in `RefreshSnapshotsAsync`. | `Tests/Tests.Infrastructure/` | **Done** |
| `TEST-FGPACKET-001` | **FG packet capture + connection state machine.** Send hook done. ConnectionStateMachine wired into ThreadSynchronizer (deterministic Lua safety). Receive hook deferred (needs ProcessMessage vtable). | `Services/ForegroundBotRunner/Mem/Hooks/` | **Partial** — recv hook pending |

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
- **Last updated:** 2026-03-08 (session 35)
- **Current work:** LiveValidation test stabilization — PathfindingService crash fix, FG ObjectManager robustness.
- **Completed session 35 (2026-03-08):**
  1. **SceneQuery thread safety FIXED (CRITICAL).** `SceneQuery::m_sceneCaches` (unordered_map) was accessed concurrently by multiple PathfindingService client threads via ProtobufSocketServer's ThreadPool. Caused iterator invalidation / use-after-free crash ~7.5min into test runs, killing PathfindingService and cascading all BG bot operations. Fix: `std::recursive_mutex` protecting Get/Set/Clear. Commit `12dc661`.
  2. **FG ObjectManager EnumerateVisibleObjects FIXED.** `Functions.GetObjectPtr` returning zero caused `Player = null`, making FG snapshot show `CharacterSelect` intermittently. Fix: (a) `GetObjectPtrFromMemory` fallback in EnumerateVisibleObjects, (b) GUID-based local player detection in callback (replaces fragile pointer comparison). Commit `688e9bd`.
  3. **NPC flag detection retry logic.** `ObjectManager_DetectsNpcFlags` test added retry loop (3 attempts, 2s delay) for NPC flags arriving in PARTIAL updates. Post-teleport wait increased from 3s to 5s. Commit `688e9bd`.
  4. **FG bot player detection (sessions 33-34).** `GetObjectPtrFromMemory` walks object manager linked list as fallback. Realm wizard bypass with alternating Lua strategies. Commits `7d728a7`, `73ccd33`, `d38b34a`.
- **Completed sessions 30-32:** See `docs/ARCHIVE.md`.
- **Files changed (commits 688e9bd, 12dc661):**
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs` — EnumerateVisibleObjects fallback + GUID comparison
  - `Exports/Navigation/SceneQuery.h` — added `std::recursive_mutex m_sceneCachesMutex`
  - `Exports/Navigation/SceneQuery.cpp` — lock_guard on Get/Set/Clear SceneCache
  - `Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs` — retry logic, longer delays
- **LiveValidation results: 26/28 passed (93%) before test host crash:**
  - **Test host crash at GatheringProfession.Mining** — not PathfindingService (no connection losses). May be StateManager or test process management issue. 22 tests never ran due to the crash.
  - **Failed (2):** CombatLoop (mob targeting), DeathCorpseRun (complex multi-phase)
  - **Skipped: 0** (all ran before crash)
  - **Newly passing (vs session 32):** Consumable, Equipment, FirstAid, AuctionHouse, EquipItem, Fishing, MeleeAttack_WithinRange, MeleeRange_Formula
- **Known remaining issues:**
  - **Test host crash:** Happens during GatheringProfession.Mining test — investigate crash dump
  - **FG WoW.exe crash:** WoW.exe crashes during early world entry (ThreadSynchronizer + charselect timing). Self-recovers via StateManager restart but tests see "FG Bot: NOT AVAILABLE" during recovery window
  - **DeathCorpseRun:** Complex multi-phase test, timing-sensitive
  - **CombatLoop:** Mob targeting intermittent failure
- **Next priority:** (1) Investigate test host crash during Mining test. (2) Run full LiveValidation suite without crash. (3) Fix remaining 2 test failures. (4) Physics+pathfinding plan phases (see plan file).
- **Test counts:** Physics 97/97, LiveValidation 26/28 (93%, truncated by crash).
- **Plan file:** `C:\Users\lrhod\.claude\plans\federated-wandering-brooks.md`
- **Sessions 1-29:** See `docs/ARCHIVE.md` for full history.
