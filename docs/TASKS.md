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
- **Last updated:** 2026-03-08 (session 38)
- **Current work:** Solution cleanup & refactoring plan execution.
- **Completed session 38 (2026-03-08):**
  1. **FG ERROR #134 login crash FIXED.** DefaultServerLogin double-fire during auth handshake. Fix: 15s login attempt cooldown, Connecting state exclusion from IsLoggedIn, smart GlueDialog dismissal (only error dialogs, not "Success!"). Commit `85a679c`.
  2. **Phase 0: Stale artifact cleanup.** Removed tracked CMake build artifacts (FastCall + Loader cmake_build_* dirs, 40 files), tmp/soap.xml, untracked Loader.dll + mystery unicode file. Updated .gitignore with consolidated cmake_build_*, Loader.dll, tmp/ patterns. Commit `21b926a`.
  3. **Phase 1: Build stabilization.** Added BotProfiles.csproj to solution. Unified C++ PlatformToolset from v143 to v145 in FastCall + Navigation vcxproj. All C# projects build (dotnet), all 3 C++ projects build (MSBuild v145). Commit `21b926a`.
  4. **Phase 2: Dead code & orphaned projects.** Removed dead `StartEnumeration()` legacy method from FG ObjectManager. Removed 3 diagnostic `File.AppendAllText` calls from `BuildUseItemByIdSequence`. Replaced hardcoded `D:\World of Warcraft\WWoWLogs` paths with dynamic `Process.MainModule`-based paths in 4 FG crash trace methods. Removed 7 orphaned projects (5 WWoW.* duplicates, 2 unused MCP services). Commit `40971d4`.
- **Completed sessions 35-37:** See `docs/ARCHIVE.md`.
- **Files changed (session 38):**
  - `.gitignore` — consolidated cmake patterns, added Loader.dll + tmp/
  - `WestworldOfWarcraft.sln` — added BotProfiles.csproj
  - `Exports/FastCall/FastCall.vcxproj` — PlatformToolset v143→v145
  - `Exports/Navigation/Navigation.vcxproj` — PlatformToolset v143→v145
  - `Services/ForegroundBotRunner/Frames/FgLoginScreen.cs` — login cooldown + Connecting guard
  - `Services/ForegroundBotRunner/Frames/FgRealmSelectScreen.cs` — Connecting guard
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs` — removed StartEnumeration, dynamic crash log paths, DismissGlueDialog safety
  - `Services/ForegroundBotRunner/Mem/ThreadSynchronizer.cs` — dynamic crash log path
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs` — dynamic crash log path
  - `Services/ForegroundBotRunner/MovementRecorder.cs` — dynamic crash log path
  - `Exports/BotRunner/BotRunnerService.Sequences.Inventory.cs` — removed diag File.AppendAllText
  - Removed: 7 orphaned project directories (177 files, ~25K lines deleted)
- **Known remaining issues:**
  - **ERROR #132 ACCESS_VIOLATION:** In-world crash at 0x170ED07E, happens after stable login. Deferred to Phase 5 of cleanup plan.
  - **Test host crash:** During GatheringProfession.Mining test
  - **DeathCorpseRun / CombatLoop:** Timing-sensitive test failures
- **Next priority:** Phase 3 (documentation — CLAUDE.md for 6+ services) and Phase 4 (refactoring large files starting with MangosRepository.cs 6952 lines). See plan file.
- **Test counts:** Physics 97/97, LiveValidation 26/28 (93%).
- **Plan file:** `C:\Users\lrhod\.claude\plans\federated-wandering-brooks.md`
- **Sessions 1-29:** See `docs/ARCHIVE.md` for full history.
