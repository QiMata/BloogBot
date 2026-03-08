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

## P0 — Active Priorities: Solution Cleanup & Refactoring

### Phase 3: Documentation (COMPLETE)
All 9 CLAUDE.md files created. See `docs/ARCHIVE.md`.

### Phase 4: Large File Refactoring (COMPLETE)

| ID | Task | File | Lines | Strategy | Status |
|----|------|------|-------|----------|--------|
| `REFACTOR-001` | Split MangosRepository into partial classes | `Services/DecisionEngineService/Repository/MangosRepository.cs` | 6,952→10 files (max 1535) | `.Items.cs`, `.Spells.cs`, `.Creatures.cs`, `.Quests.cs`, `.World.cs`, `.Characters.cs`, `.Npcs.cs`, `.Locales.cs`, `.Utility.cs` | **Done** `7139f6d` |
| `REFACTOR-002` | Split WoWSharpObjectManager into partial classes | `Exports/WoWSharpClient/Objects/WoWSharpObjectManager.cs` | 3,252→6 files (max 1434) | `.Objects.cs`, `.Movement.cs`, `.Combat.cs`, `.Inventory.cs`, `.Network.cs` | **Done** `492ebd8` |
| `REFACTOR-003` | Split FG ObjectManager into partial classes | `Services/ForegroundBotRunner/Statics/ObjectManager.cs` | 2,907→9 files (max 1012) | `.ScreenDetection.cs`, `.ObjectEnumeration.cs`, `.Spells.cs`, `.Interaction.cs`, `.Inventory.cs`, `.PlayerState.cs`, `.Movement.cs`, `.Combat.cs` | **Done** `0b94c5a` |
| `REFACTOR-004` | Split LiveBotFixture into partial classes | `Tests/Tests.Infrastructure/LiveBotFixture.cs` | 2,306→6 files (max 713) | `.Assertions.cs`, `.BotChat.cs`, `.ServerManagement.cs`, `.GmCommands.cs`, `.Snapshots.cs` | **Done** `88b22bb` |
| `REFACTOR-005` | Split StateManagerWorker into partial classes | `Services/WoWStateManager/StateManagerWorker.cs` | 1,455→3 files (max 1062) | `.BotManagement.cs`, `.SnapshotProcessing.cs` | **Done** `b1d1d27` |

### Phase 5: Command Rate-Limiting & Stability (PARTIAL)

| ID | Task | Owner | Status |
|----|------|-------|--------|
| `RATELIMIT-001` | Audit all Lua/command call sites in FG bot for rate-limiting gaps | `Services/ForegroundBotRunner/` | **Done** — audit complete, null-safety guards added `b93860e` |
| `RATELIMIT-002` | Add throttle/cooldown guards to prevent command spam during state transitions | `Exports/BotRunner/BotRunnerService.cs` | **Done** — LogoutSequence + Party sequences guarded `b93860e` |
| `CRASH-001` | ERROR #132 ACCESS_VIOLATION in-world at 0x170ED07E | `Services/ForegroundBotRunner/` | **Partial** — hook callbacks hardened (`49f8c51`), AV protection via FastCall SafeCallback pending |

## Open — Storage Stubs (Blocked on NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in RecordedTests.Shared | Requires AWSSDK.S3 |
| `RTS-MISS-002` | Azure ops in RecordedTests.Shared | Requires Azure.Storage.Blobs |

## Open — Test Coverage Gaps (Remaining RPTT/RTS/WRTS TST tasks)

These are incremental coverage expansion tasks. The test projects are healthy; these are additional test surfaces.

| ID | Project | Remaining | Current Pass Count |
|----|---------|-----------|-------------------|
| `RPTT-TST-002..006` | RecordedTests.PathingTests.Tests | Program.FilterTests, lifecycle, timeout, disconnect | 115/115 |
| `RTS-TST-002..006` | RecordedTests.Shared.Tests | S3/Azure storage tests (blocked on NuGet) | 323/323 |

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

## Sub-TASKS Execution Queue (Partial — only non-Done rows)

| # | Local file | Status | Next IDs |
|---|-----------|--------|----------|
| 11 | `RecordedTests.Shared/TASKS.md` | Pending | RTS-MISS-001..004 (blocked on NuGet) |
| 24 | `Tests/PathfindingService.Tests/TASKS.md` | **Partial** | PFS-TST-002/003/005 need nav data |
| 25 | `Tests/PromptHandlingService.Tests/TASKS.md` | **Partial** | PFS-TST-002 low priority |
| 26 | `Tests/RecordedTests.PathingTests.Tests/TASKS.md` | **Partial** | RPTT-TST-002..006 remaining |
| 27 | `Tests/RecordedTests.Shared.Tests/TASKS.md` | **Partial** | RTS-TST-002..006 (storage blocked on NuGet) |
| 36 | `UI/Systems/Systems.AppHost/TASKS.md` | Pending | SAH-MISS-001..006 |
| 37 | `UI/Systems/Systems.ServiceDefaults/TASKS.md` | Pending | SSD-MISS-001..006 |
| 38 | `WWoWBot.AI/TASKS.md` | **Partial** | AI-PARITY-001..GATHER-001 (need live server) |

> All other queue rows (1-10, 12-23, 28-30, 33-35) are **Done** — see `docs/ARCHIVE.md`.
> Rows 31, 32 (WWoW.RecordedTests.*) and deferred MCP services removed in session 38 cleanup.

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
- **Last updated:** 2026-03-08 (session 41)
- **Current work:** CRASH-001 FG bot ERROR #132 crash investigation and fix.
- **Completed this session:**
  1. **CRASH-001 root cause identified and fixed (`49f8c51`):**
     - **Root cause 1:** SignalEventManager hook callbacks (`SignalEventHook`, `SignalEventNoArgsHook`) had ZERO try/catch. Any managed exception (NullRef from null typesArg, subscriber exceptions) propagated into WoW's native call stack → ERROR #132 at random addresses. .NET 8 ignores `[HandleProcessCorruptedStateExceptions]`, so AV catching in ThreadSynchronizer WndProc was dead code.
     - **Root cause 2:** TOCTOU race in `FgCharacterSelectScreen` — `HasReceivedCharacterList` and `CharacterSelects` each read `MaxCharacterCount` from live memory independently. Between checks, the value could drop to 0 during zone transitions, causing BotRunnerService to queue `CreateCharacter` Lua while in-world → `attempt to call global 'CreateCharacter' (a nil value)`.
     - **Fix:** Wrapped both SignalEventManager callbacks with try/catch + null/pointer guards. Snapshot MaxCharacterCount in FgCharacterSelectScreen to prevent TOCTOU. Added screen state guard to `CreateCharacter()`. Added `SafeCallback1`/`SafeCallback3` SEH wrappers to FastCall.dll for future use by hook assembly code caves.
  2. **Unit tests:** WoWSharpClient 1254/1254, Physics 97/97, AI 119/121 (2 pre-existing NuGet failures).
  3. **FastCall.dll rebuilt** with new SafeCallback SEH exports.
- **Known remaining issues:**
  - **CRASH-001 partial:** The managed try/catch in hook callbacks catches NullRef/ArrayBounds/subscriber exceptions. However, AccessViolationException from stale WoW memory pointers is still UNCATCHABLE on .NET 8. The SafeCallback wrappers in FastCall.dll are ready but the assembly code caves in SignalEventManager/PacketLogger need to be updated to route through them (requires modifying the FASM injection code). This is the next step for full AV protection.
  - **BG pet support:** Pet returns null — Hunter/Warlock won't work.
  - **BG combat auto-attack:** CombatLoopTests fails — auto-attack mechanics need investigation.
  - **SMSG_UPDATE_AURA_DURATION:** "No handler registered" — duration data not parsed yet (cosmetic).
- **Test counts:** WoWSharpClient 1254, Physics 97, AI 119 (+2 pre-existing failures).
- **Plan file:** `C:\Users\lrhod\.claude\plans\federated-wandering-brooks.md`
- **Sessions 1-39:** See `docs/ARCHIVE.md` for full history.
