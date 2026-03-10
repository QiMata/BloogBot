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
8. **The MaNGOS server is ALWAYS live.** Never defer live validation tests — run them every session.

---

## P0 — Test Infrastructure Hardening (CURRENT FOCUS)

**Rationale:** All non-bot-behavior items must be resolved before iterating on bot capabilities. Flaky infrastructure, silent failures, and crash-prone tests waste more time than they save. Fix the foundation first.

See `docs/BAD_TEST_BEHAVIORS.md` for full anti-pattern catalog.

### P0.1 — Crash Detection & Resilience (HIGHEST PRIORITY)

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-CRASH-001` | **Crash monitor must resume after recovery.** `StartCrashMonitor()` returns after first crash detection — subsequent crashes go undetected. Fix: continue monitoring loop after recovery instead of returning. | `Tests/Tests.Infrastructure/BotServiceFixture.cs` | Critical | Open |
| `BT-CRASH-002` | **Corpse run test crashes WoW.exe — must fail gracefully.** DeathCorpseRunTests teleports to remote Durotar, FG crashes during ghost pathfinding. Test must detect crash and Assert.Fail immediately instead of timing out for 3+ minutes. | `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` | Critical | Open |
| `BT-DEATH-001` | **Move death test to Orgrimmar.** Current Durotar road location causes 80+y corpse runs, FG crashes. Orgrimmar graveyard = <30y run, simpler geometry. | `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` | High | Open |

### P0.2 — FG Failure Visibility (Stop Hiding Bugs)

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-FEEDBACK-003` | **FG error-out must be hard failure.** FG failures are caught and emitted as warnings — test "passes" while FG is broken. FG crash/error should fail the test or use `Skip.If` with reason. | All LiveValidation tests | High | Open |
| `BT-LOGIC-002` | **Make FG failures hard failures.** Stop silently downgrading FG test failures to warnings. FG should fail same as BG, or use `Skip.If` with documented reason. | All LiveValidation tests | High | Open |
| `BT-VERIFY-006` | **Fix GM mode toggle corruption with try/finally.** CombatLoopTests turns `.gm off` for FG combat but no guarantee of `.gm on` restoration on failure. Use try/finally. | `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs` | High | Open |
| `BT-VERIFY-001` | **Dead-state guard silently blocks commands.** `SendGmChatCommandTrackedAsync` returns Failure when bot is dead but callers ignore the return value. | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs` | High | Open |

### P0.3 — Reduce Test Runtime (Speed Up Iteration)

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-DELAY-001` | **Replace excessive Task.Delay with polling.** 198 hardcoded Task.Delay calls across 30 files. Replace with WaitForSnapshotConditionAsync or WaitForTeleportSettledAsync where applicable. | All LiveValidation tests | High | Open |
| `BT-LOGIC-003` | **Centralize timeouts.** Scattered magic numbers (8s, 12s, 15s, 3min). Create `TestTimeouts` class with configurable defaults. | `Tests/BotRunner.Tests/LiveValidation/` | Medium | Open |
| `BT-LOGIC-004` | **Standardize polling intervals.** Polling ranges from 250ms to 3000ms. Standardize: 500ms for most conditions, 1000ms for slow conditions. | All LiveValidation tests | Medium | Open |
| `BT-FEEDBACK-001` | **Add periodic progress logging.** Long tests (3min corpse run, 1min gather) show no output during polling loops — indistinguishable from hung test. Log every 10s. | All long-running tests | Medium | Open |

### P0.4 — Code Quality & Deduplication

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-LOGIC-001` | **Consolidate distance helpers.** Move `Distance2D`/`Distance3D` to LiveBotFixture shared helpers. Currently duplicated in 4+ test files. | `Tests/BotRunner.Tests/LiveValidation/` | Low | Open |
| `BT-ITEM-001` | **Centralize item/spell setup.** Shared `TestItems`/`TestSpells` constants. `EnsureItemAsync`/`EnsureSpellAsync` helpers that check before adding. | `Tests/Tests.Infrastructure/` | Medium | Open |
| `BT-VERIFY-002` | **Use BotClearInventoryAsync instead of .reset items.** `.reset items` strips equipped gear — causes cross-test contamination. | All LiveValidation tests | High | Open |
| `BT-VERIFY-003` | **Item addition without inventory verification.** `.additem` calls don't verify item appeared in bag snapshot. | Multiple tests | Medium | Open |
| `BT-VERIFY-004` | **Spell learning without verification.** `.learn` calls don't verify spell appears in SpellList snapshot. | Multiple tests | Medium | Open |
| `BT-SETUP-003` | **Missing teardown — tests don't restore position.** Add finally block that teleports back to Orgrimmar safe zone. | Multiple tests | Medium | Open |

### P0.5 — Bot Coordination (Deferred — Requires P0.1-P0.4 First)

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-PARK-001` | **Stop parking bots idle.** Both bots should exercise every test together. Add `PauseCoordinatorAsync()` to suppress AI GOTO actions. | All LiveValidation tests | High | Open |
| `BT-PARK-003` | **Teleport both bots together by default.** Tests should teleport both BG and FG to the test area. | All LiveValidation tests | High | Open |
| `BT-PARK-002` | **Assert on idle bot state at test end.** | All parking tests | Medium | Open |

### P0 — Completed

| ID | Task | Status |
|----|------|--------|
| `BT-COMBAT-002` | Fix creature teleport ACK bug. ACK now guarded by player GUID check. | **Fixed** `37a2c25` |
| `BT-TELE-001` | Safe teleport helper for FG. Limited FG to 3 nearest gathering spawns. | **Fixed** `b1444da` |
| `BT-COMBAT-001` | FG auto-attack uses AttackTarget() Lua API. Evasion is now hard failure. | **Fixed** `5a9f882` |
| `BT-SETUP-001` | Standardized test cleanup pattern (EnsureCleanSlateAsync). | **Fixed** `42100fc` |

---

## P1 — Open Bug Fixes (After P0 Complete)

| ID | Task | Owner | Status |
|----|------|-------|--------|
| `BB-COMBAT-006` | **UnitReaction unreliable in snapshots.** BG defaults to Hated(0), FG returns Friendly(4) for hostile mobs. Workaround: entry-based filter in place. | `Exports/WoWSharpClient/` | Open (workaround) |
| `FG-GHOST-STUCK-001` | **Ghost form stuck on Orgrimmar catapult geometry.** | `Exports/Navigation/` | Open |
| `LV-AUDIT-002` | **FG fishing test (TIM-7).** BG fishing CMSG_CAST_SPELL channel not starting. | `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs` | Open |
| `BT-MOVE-001` | **MovementFlags not resetting after teleport.** BG bot retains stale movement flags. | `Exports/WoWSharpClient/Movement/MovementController.cs` | Open |
| `BT-MOVE-002` | **Falling detection broken when teleported mid-air.** BG bot hovers instead of falling. | `Exports/WoWSharpClient/Movement/MovementController.cs` | Open |

## P2 — FG Client Stability

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `FG-REALM-STUCK-001` | FG client stuck on Realm Selection/Language dialog. | `Services/ForegroundBotRunner/` | **Fixed** `2301f0a` |
| `TEST-FGPACKET-001` | FG packet capture — recv hook pending (needs ProcessMessage vtable). | `Services/ForegroundBotRunner/Mem/Hooks/` | **Partial** |
| `FG-CRASH-TELE` | FG ERROR #132 during teleport to mining locations. ACCESS_VIOLATION at 0x006FA780. | `Services/ForegroundBotRunner/` | Open |

## P3 — Capability Gaps (Low Priority)

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `CAP-GAP-003` | TrainerFrame status unknown — may also be null. | `Exports/WoWSharpClient/` | Open (low priority) |
| `BG-PET-001` | BG pet support — Pet returns null. Hunter/Warlock won't work. | `Exports/WoWSharpClient/` | Open |

## Blocked — Storage Stubs (Needs NuGet)

| ID | Task | Blocker |
|----|------|---------|
| `RTS-MISS-001` | S3 ops in RecordedTests.Shared | Requires AWSSDK.S3 |
| `RTS-MISS-002` | Azure ops in RecordedTests.Shared | Requires Azure.Storage.Blobs |

## Completed Phases (See `docs/ARCHIVE.md`)

- Phase 3: Documentation (9 CLAUDE.md files)
- Phase 4: Large File Refactoring (5 monolith files split)
- Phase 5: Command Rate-Limiting & Stability (RATELIMIT-001/002, CRASH-001)
- AI Parity (all 3 gates pass live)
- Live Validation Failures (2026-02-28 batch)
- Pathfinding / Physics (all resolved)
- Test Infrastructure: TEST-TRAM-001, TEST-CRASH-001

## Canonical Commands

```bash
# Full LiveValidation suite
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Corpse-run only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m

# Combat tests only
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~CombatRangeTests"

# Physics calibration
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings

# AI tests
dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"

# Full solution (all test projects)
dotnet test WestworldOfWarcraft.sln --configuration Release
```

## Session Handoff
- **Last updated:** 2026-03-10 (session 49)
- **Current work:** P0 infrastructure hardening — reorganized TASKS.md to prioritize non-bot-behavior items.
- **Completed this session:**
  1. **CombatRangeTests FIXED** (`609191c`): Added Goto walk + lenient targeting for position desync. All 8 tests pass.
  2. **TASKS.md reorganized**: Split P0 into P0.1-P0.5 sub-priorities. Added BT-CRASH-001/002 for crash monitor gaps.
- **LiveValidation baseline:** 46 passed, 2 failed, 2 skipped (50 total)
- **Next priorities (in order):**
  - BT-CRASH-001: Fix crash monitor resume-after-recovery
  - BT-CRASH-002: Corpse run crash detection + graceful failure
  - BT-DEATH-001: Move death test to Orgrimmar
  - BT-FEEDBACK-003/BT-LOGIC-002: FG failures as hard failures
  - BT-LOGIC-001: Consolidate distance helpers
  - BT-LOGIC-003: Centralize timeouts
  - BT-DELAY-001: Replace excessive Task.Delay calls
- **Sessions 1-48:** See `docs/ARCHIVE.md` for full history.
