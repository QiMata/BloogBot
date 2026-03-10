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

*All items completed — see P0 Completed table.*

### P0.2 — FG Failure Visibility (Stop Hiding Bugs)

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-VERIFY-006` | **Fix GM mode toggle corruption with try/finally.** CombatLoopTests uses try/finally for .gm on restoration. | `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs` | High | **Fixed** (already implemented) |
| `BT-VERIFY-001` | **Dead-state guard now surfaces [DEAD-GUARD] warning in test output.** `SendGmChatCommandAsync` logs visible warning when commands blocked. | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs` | High | **Fixed** |

### P0.3 — Reduce Test Runtime (Speed Up Iteration)

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-DELAY-001` | **Replace excessive Task.Delay with polling.** Replaced 41 hardcoded delays (2s-8s) with WaitForTeleportSettledAsync/WaitForSnapshotConditionAsync/WaitForPositionChangeAsync. Long delays reduced from 46 to 5 (all justified). | All LiveValidation tests | High | **Fixed** |
| `BT-LOGIC-003` | **Centralize timeouts.** Scattered magic numbers (8s, 12s, 15s, 3min). Create `TestTimeouts` class with configurable defaults. | `Tests/BotRunner.Tests/LiveValidation/` | Medium | Open |
| `BT-LOGIC-004` | **Standardize polling intervals.** After BT-DELAY-001, remaining delays are 200-1500ms (context-appropriate). No further action needed. | All LiveValidation tests | Medium | **Mitigated** |
| `BT-FEEDBACK-001` | **Add periodic progress logging.** Long tests (3min corpse run, 1min gather) show no output during polling loops — indistinguishable from hung test. Log every 10s. | All long-running tests | Medium | Open |

### P0.4 — Code Quality & Deduplication

| ID | Task | Owner | Severity | Status |
|----|------|-------|----------|--------|
| `BT-LOGIC-001` | **Consolidate distance helpers.** Move `Distance2D`/`Distance3D` to LiveBotFixture shared helpers. | `Tests/BotRunner.Tests/LiveValidation/` | Low | **Fixed** `18cb049` |
| `BT-ITEM-001` | **Centralize item/spell setup.** Shared `TestItems`/`TestSpells` constants added to LiveBotFixture. Local duplicates replaced in 6 files. | `LiveBotFixture.Assertions.cs` | Medium | **Fixed** |
| `BT-VERIFY-002` | **Use BotClearInventoryAsync instead of .reset items.** Replaced in VendorBuySellTests and LootCorpseTests (bag-only cleanup). EquipmentEquipTests/FishingProfessionTests keep .reset items (need gear stripped). | All LiveValidation tests | High | **Fixed** |
| `BT-VERIFY-003` | **BotAddItemAsync now polls for item in BagContents after .additem.** Warns if item not confirmed within 3s. | `LiveBotFixture.BotChat.cs` | Medium | **Fixed** |
| `BT-VERIFY-004` | **BotLearnSpellAsync now polls for spell in SpellList after .learn.** Warns if spell not confirmed within 3s. | `LiveBotFixture.BotChat.cs` | Medium | **Fixed** |
| `BT-SETUP-003` | **Missing teardown mitigated by EnsureCleanSlateAsync.** 16/24 test files use EnsureCleanSlateAsync which revives+teleports at start. Remaining 8 have equivalent setup patterns. | Multiple tests | Medium | **Mitigated** |

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
| `BT-CRASH-001` | Crash monitor continues loop after WoW.exe crash (no longer returns). | **Fixed** `18cb049` |
| `BT-CRASH-002` | Corpse run test detects crash and fails gracefully. | **Fixed** `18cb049` |
| `BT-DEATH-001` | Death test uses `.tele name <char> Orgrimmar` — simple 8-step flow. | **Fixed** `18cb049` |
| `BT-FEEDBACK-003` | FG failures are hard assertions (MapTransition, CharLifecycle, Economy). | **Fixed** `2891847` |
| `BT-LOGIC-002` | FG failures propagated — no more silent warning downgrade. | **Fixed** `2891847` |
| `BT-LOGIC-001` | Distance2D/Distance3D centralized in LiveBotFixture, removed from 9 files. | **Fixed** `18cb049` |

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
- **Last updated:** 2026-03-10 (session 50)
- **Current work:** P0 infrastructure hardening — massive test framework improvements.
- **Completed this session:**
  1. **BT-DELAY-001** (`05539f0`): Replaced 41 excessive Task.Delay calls (2s-8s) with polling helpers. Long delays 46→5.
  2. **BT-VERIFY-001**: Dead-state guard surfaces [DEAD-GUARD] warnings in test output.
  3. **BT-VERIFY-002**: VendorBuySellTests + LootCorpseTests use BotClearInventoryAsync instead of .reset items.
  4. **BT-ITEM-001**: Centralized TestItems/TestSpells constants, removed duplicates from 6 files.
  5. **BT-VERIFY-006**: Already implemented (try/finally in CombatLoopTests).
  6. **BT-SETUP-003**: Mitigated — 16/24 test files use EnsureCleanSlateAsync.
  7. **BT-LOGIC-004**: Mitigated — remaining delays are 200-1500ms (context-appropriate).
  8. TASKS.md updated to reflect all completed items from sessions 49-50.
- **P0 Status:** P0.1 (crash detection) COMPLETE. P0.2 (FG visibility) mostly complete (BT-VERIFY-006 done). P0.3 (runtime) COMPLETE. P0.4 (code quality) mostly complete — BT-VERIFY-003/004 remaining.
- **Remaining P0 items:**
  - BT-LOGIC-003: Centralize timeouts (medium priority, many are context-specific)
  - BT-FEEDBACK-001: Add periodic progress logging to long tests
  - BT-VERIFY-003/004: Verify item add / spell learn in snapshot after setup
- **Next:** Run LiveValidation suite to verify no regressions, then review tests for bad behavior patterns.
- **Sessions 1-49:** See `docs/ARCHIVE.md` for full history.
