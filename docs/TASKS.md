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
- **Last updated:** 2026-03-07 (session 29)
- **Current work:** LiveValidation test stabilization + bot behavior implementation.
- **Completed session 29 (2026-03-07):**
  1. **VendorBuySellTests root cause found and fixed** — Test was finding ANY vendor near Razor Hill but buying Weak Flux (2880), only sold by Wuark (blacksmith). If Grimtak (general goods) was found first, buy silently failed. Fix: teleport to Grimtak's exact DB position (305.7, -4665.9, 16.5) and buy Refreshing Spring Water (159) from his inventory. Both buy and sell tests now pass.
  2. **VMaNGOS `.go xyz` investigation** — `HandleGoHelper` has NO combat check; `Player::TeleportTo` also has no combat check. Teleport failures are timing/state issues, not combat-blocked. Heavy teleport verification (`.combatstop` + position polling + retry) was tested but REVERTED — added 3s+ per teleport, disrupted test timing, caused 14/50 run.
  3. **VMaNGOS `.quest add` investigation** — Command exists at `SEC_BASIC_ADMIN` (4), bots have level 6. However, the `quest` parent command has `console=false` flag — `.quest add` only works via in-game chat, NOT via SOAP. Tests correctly use bot chat, so the failure is in `ExtractPlayerTarget` not finding the selected player (intermittent). `GetSelectedPlayer()` returns self when guid=0, so it should work — failure is timing-dependent.
  4. **LiveValidation runs: 29/50, 30/50** (clean runs). WoW.exe crash mid-run caused 14/50 and 18/50 in earlier attempts.
- **Completed session 28:** See `docs/ARCHIVE.md`.
- **LiveValidation remaining failures (20 fail in best run):**
  - **Consistent failures:**
    - `GatheringProfession.Mining/Herbalism` — teleport to spawn fails (closest nodes ~300y, `.go xyz` intermittently rejected)
    - `QuestInteraction` — `.quest add` via chat intermittently fails (ExtractPlayerTarget timing)
    - `StarterQuest` — Gornek NPC not visible after teleport
    - `OrgrimmarGroundZ.PostTeleport` — FG not available
  - **Intermittent (pass on some runs, fail on others):**
    - `VendorBuySell.BuyItem` (now passes 3/4 runs — was 0/4 before fix)
    - `ConsumableUsage`, `BuffDismiss`, `UnequipItem`, `CombatLoop`, `SpellCast`, `Navigation`, `Fishing`, `CraftingProfession`, `CharacterLifecycle.Death`, `EquipmentEquip`, `TalentAllocation`, `CombatRange.RangedOutside`
- **Key findings:**
  - **CMSG_USE_ITEM targetMask is uint16** — documented in VMaNGOS source `Spell.h:159`. Do NOT change to uint32.
  - **VMaNGOS INTERACTION_DISTANCE = 5.0y** — vendor/NPC interaction requires player within 5y. Teleporting to NPC position + 1z is fine.
  - **`.quest` commands console=false** — only work via in-game chat, not SOAP.
- **TESTBOT1 (FG) stuck at CharacterSelect** — persists across sessions. All FG-dependent scenarios use BG-only fallback.
- **Next priority:** (1) Fix teleport reliability (investigate why `.go xyz` silently fails for ~20% of teleports), (2) Fix TESTBOT1 CharacterSelect stuck, (3) FG-GHOST-STUCK-001
- **Test counts:** Physics 97/97, Pathfinding 25/25, AI 121/121, Tier2 52/52, WoWSharpClient 1251/1251. LiveValidation 29-30/50.
- **Plan file:** `C:\Users\lrhod\.claude\plans\federated-wandering-brooks.md`
- **Sessions 1-25:** See `docs/ARCHIVE.md` for full history.
