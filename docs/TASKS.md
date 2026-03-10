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

## P0 — Test Fixture & Harness Hardening (FOUNDATION)

**Rationale:** Test infrastructure must be solid before implementing more bot behavior. Flaky tests, fixture contamination, and client crashes waste more time than they save. Fix the foundation first.

See `docs/BAD_TEST_BEHAVIORS.md` for full anti-pattern catalog.

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-PARK-001` | **Stop parking bots idle.** Tests park one bot at Orgrimmar bank while only testing the other. Both bots should exercise every test together. Add `PauseCoordinatorAsync()` to suppress AI GOTO actions instead of parking. | All LiveValidation tests | High | Open |
| `BT-FEEDBACK-003` | **FG error-out must be hard failure.** FG failures are caught and emitted as warnings — test "passes" while FG is broken. FG crash/error should fail the test or use `Skip.If` with reason. | All LiveValidation tests | High | Open |
| `BT-COMBAT-002` | **Fix creature teleport ACK bug.** ~~BG sends teleport ACK for creature MSG_MOVE_TELEPORT.~~ Fixed: ACK now guarded by player GUID check. | `Exports/WoWSharpClient/Handlers/MovementHandler.cs` | Critical | **Fixed** `37a2c25` |
| `BT-TELE-001` | **Safe teleport helper for FG.** Limit FG teleports to pre-validated coordinates. Wait for terrain load. Catch crash and mark as skipped. | `Tests/Tests.Infrastructure/LiveBotFixture.cs` | Critical | Open |
| `BT-PARK-003` | **Teleport both bots together by default.** Tests should teleport both BG and FG to the test area so behavior can be observed and compared. Parking should be the documented exception. | All LiveValidation tests | High | Open |
| `BT-COMBAT-001` | **FG auto-attack uses AttackTarget().** FG switched from `CastSpellByName('Attack')` to `AttackTarget()` Lua API. Evasion is now hard failure. BG heartbeat already works (IsAutoAttacking + MovementController). | `Services/ForegroundBotRunner/Statics/ObjectManager.Combat.cs` | High | **Fixed** `5a9f882` |
| `BT-MOVE-001` | **MovementFlags not resetting after teleport.** BG bot may retain stale movement flags after teleport. | `Exports/WoWSharpClient/Movement/MovementController.cs` | Medium | Open |
| `BT-MOVE-002` | **Falling detection broken when teleported mid-air.** BG bot hovers instead of falling when teleported above ground. | `Exports/WoWSharpClient/Movement/MovementController.cs` | High | Open |
| `BT-SETUP-001` | **Standardized test cleanup pattern.** Create `EnsureCleanSlateAsync(account)`: `.reset items` + revive + teleport to safe zone. Call at start of every test. | `Tests/Tests.Infrastructure/LiveBotFixture.cs` | High | Open |
| `BT-DEATH-001` | **Move death test to Orgrimmar.** Current Durotar road location causes 80+y corpse runs, FG crashes. Orgrimmar graveyard = <30y run. | `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs` | High | Open |
| `BT-LOGIC-002` | **Make FG failures hard failures.** Stop silently downgrading FG test failures to warnings. FG should fail same as BG, or use `Skip.If` with documented reason. | All LiveValidation tests | High | Open |
| `BT-FEEDBACK-001` | **Add periodic progress logging.** Long tests (3min corpse run, 1min gather) show no output during polling loops — indistinguishable from hung test. Log every 10s. | All long-running tests | Medium | Open |
| `BT-ITEM-001` | **Centralize item/spell setup.** Shared `TestItems`/`TestSpells` constants. `EnsureItemAsync`/`EnsureSpellAsync` helpers that check before adding. | `Tests/Tests.Infrastructure/` | Medium | Open |
| `BT-LOGIC-001` | **Consolidate distance helpers.** Move `Distance2D`/`Distance3D` to LiveBotFixture shared helpers. | `Tests/BotRunner.Tests/LiveValidation/` | Low | Open |

## P1 — Open Bug Fixes

| ID | Task | Owner | Status |
|----|------|-------|--------|
| `BB-COMBAT-006` | **UnitReaction unreliable in snapshots.** BG defaults to Hated(0), FG returns Friendly(4) for hostile mobs. Need FactionTemplate→Reaction mapping for BG using DBC data. Workaround: entry-based filter in place. | `Exports/WoWSharpClient/` | Open (workaround in place) |
| `FG-GHOST-STUCK-001` | **Ghost form stuck on Orgrimmar catapult geometry.** Pathfinding/stuck-recovery doesn't handle dense M2 geometry. | `Exports/Navigation/` | Open |
| `LV-AUDIT-002` | **FG fishing test (TIM-7).** BG fishing CMSG_CAST_SPELL channel not starting. | `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs` | Open |

## P2 — FG Client Stability

| ID | Issue | Owner | Status |
|----|-------|-------|--------|
| `FG-REALM-STUCK-001` | FG client stuck on Realm Selection/Language dialog. | `Services/ForegroundBotRunner/` | **Fixed** `2301f0a` |
| `TEST-FGPACKET-001` | FG packet capture — recv hook pending (needs ProcessMessage vtable). | `Services/ForegroundBotRunner/Mem/Hooks/` | **Partial** |
| `FG-CRASH-TELE` | FG ERROR #132 during teleport to mining locations. ACCESS_VIOLATION at 0x006FA780 referencing 0x00000005. Terrain tile not loaded before ObjectManager access. | `Services/ForegroundBotRunner/` | Open |

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
- **Last updated:** 2026-03-09 (session 46)
- **Current work:** P0 test harness hardening — fixing combat, auto-attack, idle bot parking, and test observability.
- **Completed this session:**
  1. **BT-COMBAT-002 FIXED** (`37a2c25`): Creature teleport ACK bug — MovementHandler now only ACKs player teleports. CombatRangeTests.MeleeAttack passes.
  2. **BT-COMBAT-001 FIXED** (`5a9f882`): FG auto-attack switched from `CastSpellByName('Attack')` to `AttackTarget()` Lua API. Idempotent, no action bar dependency. CombatLoopTests passes without mob evade.
  3. **FG evasion = hard failure**: CombatLoopTests no longer downgrades FG evasion to warning. `Assert.True(fgPassed, ...)` with try/finally GM mode restoration (BT-VERIFY-006).
  4. **66 stale files removed** from git: C++ intermediates (31), .dotnet telemetry (15), scripts (13), .ai docs (2), IDE config (1). `.gitignore` updated.
  5. **BAD_TEST_BEHAVIORS.md expanded**: Added §10 (Bot Coordination & Idle Parking), §11 (Feedback & Observability Gaps), §12 (BG Movement State). 12 categories, 27 anti-patterns total.
  6. **TASKS.md updated**: BT-COMBAT-002 and BT-COMBAT-001 marked fixed. BT-MOVE-001/002 added. Priorities updated.
- **Combat test results: CombatLoopTests 1/1 ✓, CombatRangeTests 8/8 ✓**
- **LiveValidation results: 35 passed, 14 failed, 1 skipped (50 total)**
  - **All 14 failures are FG cascade crashes** — WoW.exe crashed 3× (PIDs: 7020→36148→26496). FG-dependent tests fail while client re-injects.
  - **Root cause:** GatheringProfessionTests teleports FG to remote coordinates → ERROR #132 → cascade.
  - **BG tests all pass.** Skipped: LootCorpseTests (precondition).
- **Next priorities:**
  - BT-TELE-001: Limit FG teleports to safe locations (prevents cascade crashes)
  - BT-PARK-001: Both bots exercise every test (stop idle parking)
  - BT-SETUP-001: Standardized cleanup at test start (EnsureCleanSlateAsync)
  - BT-MOVE-002: Falling detection broken when teleported mid-air
  - BT-DEATH-001: Move death test to Orgrimmar
- **Sessions 1-45:** See `docs/ARCHIVE.md` for full history.
