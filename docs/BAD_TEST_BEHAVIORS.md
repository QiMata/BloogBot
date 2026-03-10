# Bad Test Behaviors — LiveValidation Test Anti-Patterns

Tracks observed bad patterns in the LiveValidation integration test suite. Each entry describes the anti-pattern, which tests exhibit it, the impact, and recommended fix.

---

## Categories

1. [Setup/Teardown Issues](#1-setupteardown-issues)
2. [Combat & Auto-Attack Issues](#2-combat--auto-attack-issues)
3. [Death/Corpse Run Issues](#3-deathcorpse-run-issues)
4. [Item & Equipment Duplication](#4-item--equipment-duplication)
5. [Teleport & Location Risks](#5-teleport--location-risks)
6. [Fixture State Contamination](#6-fixture-state-contamination)
7. [GM Command Misuse](#7-gm-command-misuse)
8. [Test Logic Issues](#8-test-logic-issues)

---

## 1. Setup/Teardown Issues

### BT-SETUP-001: No Standardized Test Cleanup Pattern
- **Observed**: Some tests call `.reset items`, others call `.additem` without cleanup, others do nothing. No consistent entry/exit state.
- **Tests**: All 24 test classes share `LiveBotFixture`. State leaks across tests.
- **Impact**: Test N's leftover items/buffs/position affect Test N+1. Results depend on execution order.
- **Fix**: Created `EnsureCleanSlateAsync(account, label)`: logs entry state, revives if dead (with reason), teleports to Orgrimmar safe zone, ensures GM mode on. Deployed to 13 test files.
- **Status**: **FIXED** (`42100fc`)
- **Severity**: High

### BT-SETUP-002: EnsureStrictAlive Uses SOAP Revive Fallback
- **Observed**: `EnsureStrictAliveAsync` calls `RevivePlayerAsync(characterName)` via SOAP as first fallback. This bypasses the client action path and may leave stale ghost/corpse state.
- **Tests**: DeathCorpseRunTests, CombatLoopTests, CombatRangeTests, all tests with strict-alive setup.
- **Impact**: SOAP revive sets HP server-side but client may not process the state change, leaving ghost flags or dead stand-state in snapshots.
- **Fix**: Prefer `ReleaseCorpse` + `RetrieveCorpse` client actions first. SOAP revive only as last resort.
- **Status**: OPEN
- **Severity**: Medium

### BT-SETUP-003: Missing Teardown — Tests Don't Restore Position
- **Observed**: Tests teleport bots to various locations but don't teleport back to a safe zone afterward. Next test inherits the position.
- **Tests**: CombatRangeTests (teleports to FarX/FarY 200y away), GatheringProfessionTests (teleports to 25 mining locations), FishingProfessionTests (Ratchet dock).
- **Impact**: Next test finds bot in unexpected location. Mob area, NPC proximity, and zone-specific behavior vary.
- **Fix**: Add `finally` block or test cleanup that teleports back to Orgrimmar safe zone (1629.4, -4373.4, 34).
- **Status**: OPEN
- **Severity**: Medium

---

## 2. Combat & Auto-Attack Issues

### BT-COMBAT-001: Auto-Attack Not Using Toggle Pattern (BloogBot Style)
- **Observed**: `CombatLoopTests` and `CombatRangeTests` send `StartMeleeAttack` action which calls `WoWSharpObjectManager.StartMeleeAttack()`. This sends a single `CMSG_ATTACKSWING` + pre-attack heartbeat. The original BloogBot (DrewKestrell) uses an auto-attack **toggle** via action bar slot — `IsCurrentAction(72)` checks if auto-attack is active, `CastSpellByName('Attack')` toggles it.
- **Impact**: BG bot's single CMSG_ATTACKSWING may not establish the full auto-attack swing timer loop. If the server rejects the first swing (timing, position), there's no retry. The FG bot uses Lua `AttackTarget()` or `CastSpellByName('Attack')` which properly toggles the client's auto-attack state machine.
- **Expected Flow (from VMaNGOS server)**:
  1. Client sends `CMSG_ATTACKSWING` with target GUID
  2. Server validates range, facing, alive state, combat reach
  3. Server sends `SMSG_ATTACKSTART` (attacker GUID + victim GUID)
  4. Server schedules swing timer (weapon speed)
  5. Client must maintain movement heartbeats (500ms) during combat
  6. On each swing: server sends `SMSG_ATTACKERSTATEUPDATE` with damage
  7. To stop: client sends `CMSG_ATTACKSTOP` → server sends `SMSG_ATTACKSTOP`
- **Root Cause**: BG bot sends CMSG_ATTACKSWING but doesn't verify SMSG_ATTACKSTART response. If the server rejects (out of range, facing wrong), the bot doesn't know.
- **Fix**: BG bot should: (1) verify SMSG_ATTACKSTART received after CMSG_ATTACKSWING, (2) retry if rejected, (3) maintain heartbeat packets during combat (already done via IsAutoAttacking flag). FG bot should use `AttackTarget()` API (slot-independent) instead of `CastSpellByName('Attack')`.
- **Status**: OPEN
- **Severity**: High

### BT-COMBAT-002: CombatRangeTests.MeleeAttack Fails — Creature Teleport ACK Bug
- **Observed**: `MeleeAttack_WithinRange_TargetIsSelected` fails with `TargetGuid=0x0` after `StartMeleeAttack`. Logs show the BG bot sends `ACK TELEPORT` packets in response to creature `MSG_MOVE_TELEPORT` (GUID `F130000C1A002B06`). Two ACKs are sent, then 500ms fallback clears `_isBeingTeleported` twice.
- **Root Cause**: The MovementHandler guards `NotifyTeleportIncoming` for player GUIDs but still sends teleport ACK packets for creature teleports. The ACK sets `_isBeingTeleported=true` which disrupts heartbeat sending → server doesn't receive position updates → rejects combat.
- **Impact**: BG melee combat unreliable when mobs send creature teleport packets near combat start.
- **Fix**: The teleport ACK path must also be guarded to only ACK player teleports. Creature MSG_MOVE_TELEPORT should be processed as position updates only.
- **Status**: OPEN
- **Severity**: Critical

### BT-COMBAT-003: .damage Shortcut for Mob Cleanup Leaves Combat State
- **Observed**: Multiple tests use `.damage 5000` to kill mobs during cleanup. This doesn't clear the bot's combat state (IsAutoAttacking, TargetGuid) properly — the bot may still think it's in combat for the next test.
- **Tests**: CombatRangeTests (4 places), CombatLoopTests cleanup, LootCorpseTests.
- **Impact**: Next test starts with stale combat state.
- **Fix**: After `.damage` cleanup, send `StopAttack` action AND verify combat state cleared in snapshot.
- **Status**: OPEN
- **Severity**: Medium

---

## 3. Death/Corpse Run Issues

### BT-DEATH-001: Death Test Location Too Remote
- **Observed**: `DeathCorpseRunTests` teleports to southern Durotar road between Razor Hill and Sen'jin Village (`-830, -4910, 24`). This is far from graveyards, making the corpse run very long (80+ yards). The test takes 2-3 minutes per bot.
- **User Requirement**: Death run should be done in **Orgrimmar** where graveyards are close and the run is short.
- **Impact**: Long test time, increased chance of stuck/stall during extended corpse run. FG client crashes during long pathfinding runs through complex geometry.
- **Fix**: Move death location to Orgrimmar (e.g., near the Valley of Honor or near a graveyard). This shortens the corpse run to <30 yards and reduces crash risk.
- **Status**: OPEN
- **Severity**: High

### BT-DEATH-002: Kill Method Uses GM Command Instead of Real Damage
- **Observed**: `InduceDeathForTestAsync` uses `.kill` or `.die` GM commands to kill the character. This bypasses real combat death (no damage taken, no death log, no combat state).
- **Impact**: Tests don't validate the real death path. A bug in combat death handling wouldn't be caught.
- **Alternative**: For death test, have a nearby mob attack and kill the bot, or use `.damage` incrementally to simulate HP loss.
- **Status**: OPEN (Acceptable for corpse-run test — testing the run, not the death)
- **Severity**: Low

### BT-DEATH-003: FG Client Crashes During Corpse Run — No Error Window
- **Observed**: FG client (WoW.exe) crashes during corpse run pathfinding through complex geometry (Orgrimmar catapults, Razor Hill buildings). Crash produces ERROR #132 ACCESS_VIOLATION but the error dialog may not appear if the crash is in the ThreadSynchronizer during state transition.
- **Root Cause**: FG bot's ObjectManager polling + Lua calls during map transition/ghost-form loading screen can access stale pointers. The `IsContinentTransition` guard doesn't cover all code paths.
- **Impact**: Test fixture loses FG bot mid-test. Subsequent FG tests fail or are skipped.
- **Fix**: Move death test to simpler geometry. Add crash detection to fixture (TEST-CRASH-001 already done). Ensure `IsContinentTransition` guards all ObjectManager access paths.
- **Status**: OPEN
- **Severity**: High

---

## 4. Item & Equipment Duplication

### BT-ITEM-001: Redundant Item Setup Across Tests
- **Observed**: Multiple tests independently add the same items:
  - **Worn Mace (36)**: CombatLoopTests, CombatRangeTests (implicitly via weapon check)
  - **Mining Pick (2901)**: GatheringProfessionTests
  - **Fishing Pole (6256)**: FishingProfessionTests
  - **Linen Cloth (2589)**: CraftingProfessionTests
  - **Throwing Knife (2947)**: CombatRangeTests
  - **Rough Stone (2835)**: CraftingProfessionTests
  - Various food/consumable items: ConsumableUsageTests, BuffDismissTests
- **Impact**: Items accumulate in inventory across tests. No cleanup between tests. Bag space fills up, causing `.additem` failures in later tests.
- **Fix**: Centralize item constants in a shared `TestItems` class. Add `.reset items` at the START of every test that modifies inventory. Consider a `EnsureItemAsync` helper that checks inventory before adding.
- **Status**: OPEN
- **Severity**: Medium

### BT-ITEM-002: .learn / .setskill Repeated Without Check
- **Observed**: Tests call `.learn <spellId>` and `.setskill <skillId>` without first checking if the spell/skill already exists. Server responds "You already know that spell." — not harmful but wastes time and clutters logs.
- **Tests**: CombatLoopTests (`.learn 198`), GatheringProfessionTests (`.learn 2575`, `.learn 2366`), FishingProfessionTests (`.learn 7620`), CraftingProfessionTests (multiple `.learn` calls).
- **Impact**: Redundant GM commands add 1-2s per call. Log noise obscures real issues.
- **Fix**: Add `EnsureSpellLearnedAsync(account, spellId)` that checks snapshot spell list before calling `.learn`.
- **Status**: OPEN
- **Severity**: Low

---

## 5. Teleport & Location Risks

### BT-TELE-001: FG Teleport to Coordinates Crashes Client
- **Observed**: FG client crashes (ERROR #132 ACCESS_VIOLATION at 0x006FA780) when teleporting to certain coordinates. The mining test teleports to 25 different locations across Durotar/Barrens. Some Z coordinates from the spawn DB don't match FG client's terrain resolution.
- **Tests**: GatheringProfessionTests (25 Copper Vein locations), DeathCorpseRunTests (Durotar road).
- **Root Cause**: `.go xyz` teleports with Z from spawn table. FG client loads terrain tiles async — if the destination tile isn't loaded, the ObjectManager accesses invalid memory. Z+3 offset helps but doesn't prevent all crashes.
- **Fix**: Limit FG teleports to known-safe locations. Add a `SafeTeleportAsync` helper that: (1) only teleports to pre-validated coordinates, (2) waits for terrain load, (3) catches FG crash and marks test as skipped.
- **Status**: OPEN
- **Severity**: Critical

### BT-TELE-002: Cross-Zone Teleport Without Zone Load Wait
- **Observed**: Tests teleport between zones (Durotar → Barrens, Durotar → Orgrimmar) without waiting for zone transition to complete. Fixed delays (`Task.Delay(3000)`) are used instead of polling for zone-loaded state.
- **Tests**: GatheringProfessionTests, MapTransitionTests, DeathCorpseRunTests.
- **Impact**: Snapshot taken during zone load contains stale/invalid data. ObjectManager enumeration during transition can crash FG.
- **Fix**: Poll `IsContinentTransition` or zone-loaded condition instead of fixed delays.
- **Status**: OPEN
- **Severity**: Medium

---

## 6. Fixture State Contamination

### BT-FIXTURE-001: All Tests Share Single LiveBotFixture
- **Observed**: All 24 test classes share one `LiveBotFixture` instance (xUnit collection fixture). Character state (position, inventory, buffs, combat, ghost) persists across ALL test classes. A test that crashes WoW.exe affects all subsequent tests.
- **Impact**: Flaky test results depending on execution order. Running individual tests passes but full suite has cascade failures.
- **Fix**: Architectural limitation of xUnit collection fixtures. Mitigations: (1) standardized cleanup at test start, (2) crash detection with auto-recovery, (3) position/inventory reset between test classes.
- **Status**: KNOWN
- **Severity**: High

### BT-FIXTURE-002: Parallel BG+FG Execution Causes Mob Contention
- **Observed**: When BG and FG run in parallel (e.g., DeathCorpseRunTests), both bots may target the same mob, compete for the same corpse, or trigger mob evade by proximity.
- **Impact**: Random test failures depending on mob spawn timing and bot execution order.
- **Fix**: CombatLoopTests already switched to sequential execution. Other tests should follow.
- **Status**: PARTIALLY FIXED (CombatLoopTests sequential, others still parallel)
- **Severity**: Medium

---

## 7. GM Command Misuse

### BT-GM-001: .respawn Called Without Checking Results
- **Observed**: Tests call `.respawn` to force mob respawn near the bot. The command is fire-and-forget — no check if mobs actually respawned. Some areas have long respawn timers that `.respawn` can't override.
- **Tests**: CombatRangeTests (`EnsureAliveAndNearMobsAsync`), CombatLoopTests, LootCorpseTests.
- **Impact**: Test proceeds with no mobs available, then fails at mob-finding step.
- **Fix**: After `.respawn`, poll for nearby units in snapshot. If none after 3s, skip or retry.
- **Status**: OPEN
- **Severity**: Low

### BT-GM-002: .combatstop Used to Exit Combat Before Teleport
- **Observed**: `CombatRangeTests.MeleeAttack_OutsideRange_DoesNotStartCombat` sends `.combatstop` to force-exit combat before teleporting 200y away. This is a valid test setup pattern but hides real bugs — if the bot can't naturally exit combat, that's a bug.
- **Status**: ACCEPTABLE for negative test cases
- **Severity**: Low

### BT-GM-003: .npc add temp for Missing Mobs
- **Observed**: `CombatLoopTests.FindLivingMobAsync` spawns a temporary Mottled Boar via `.npc add temp 3098` when no natural mobs are found. Temp creatures are removed on server restart but persist within a session.
- **Impact**: Temp creatures don't follow normal patrol/respawn patterns. Combat behavior may differ from natural spawns.
- **Fix**: Prefer `.respawn` + wait. Only spawn temp as last resort with a warning.
- **Status**: ACCEPTABLE
- **Severity**: Low

---

## 8. Test Logic Issues

### BT-LOGIC-001: Distance Helper Functions Duplicated Across Tests
- **Observed**: `Distance2D`, `Distance3D`, `DistanceTo`, `DistanceTo2D` are copy-pasted across CombatLoopTests, CombatRangeTests, DeathCorpseRunTests, and LiveBotFixture.
- **Impact**: Code duplication, inconsistent naming, maintenance burden.
- **Fix**: Move to `LiveBotFixture` or a shared `TestHelpers` class.
- **Status**: OPEN
- **Severity**: Low

### BT-LOGIC-002: FG Failures Silently Downgraded to Warnings
- **Observed**: Multiple tests catch FG failures and emit warnings instead of failing:
  - CombatLoopTests: "FG WARN: FG auto-attack failed (mob evade). Known issue"
  - DeathCorpseRunTests: FG failure with "strict-alive/stuck/stalled" reasons → warning only
- **Impact**: FG bugs are never surfaced as test failures. FG could be completely broken and tests still pass.
- **Fix**: FG should be a hard failure (same as BG) unless explicitly skipped. If FG is expected to fail for a known reason, use `Skip.If` with the specific reason documented.
- **Status**: OPEN
- **Severity**: High

### BT-LOGIC-003: Magic Numbers for Timeouts
- **Observed**: Tests use hardcoded timeouts scattered throughout: `TimeSpan.FromSeconds(8)`, `TimeSpan.FromSeconds(12)`, `TimeSpan.FromSeconds(15)`, `TimeSpan.FromMinutes(3)`, etc. No central timeout configuration.
- **Impact**: Difficult to tune timeouts for different environments (CI vs local, fast vs slow server).
- **Fix**: Centralize timeouts in `TestConstants` or `LiveBotFixture.Timeouts` class.
- **Status**: OPEN
- **Severity**: Low

### BT-LOGIC-004: WaitForCondition Polling Intervals Vary
- **Observed**: Polling intervals range from 250ms to 3000ms across different wait helpers. Some use `Task.Delay(500)`, others `Task.Delay(350)`, others `Task.Delay(3000)`.
- **Impact**: Inconsistent responsiveness and timing behavior across tests.
- **Fix**: Standardize on 500ms for most conditions, 1000ms for slow conditions (zone load, corpse run).
- **Status**: OPEN
- **Severity**: Low

---

## 9. Command & Verification Gaps

### BT-VERIFY-001: Dead-State Guard Silently Blocks Commands
- **Observed**: `LiveBotFixture.BotChat.cs` `SendGmChatCommandTrackedAsync` has a dead-state guard that blocks commands when the bot is dead/ghost. It returns `ResponseResult.Failure` but some callers ignore the return value.
- **Impact**: Commands like `.gm off` in CombatLoopTests are silently skipped if bot is dead, but the test assumes they ran. GM mode state leaks.
- **Fix**: Callers must check return value, or dead-state guard should throw/skip rather than silently fail.
- **Status**: OPEN
- **Severity**: High

### BT-VERIFY-002: .reset items Too Broad — Strips Equipment
- **Observed**: Tests call `.reset items` to clear inventory but this also strips ALL equipped gear. CombatLoopTests equips Worn Mace, then a later test calls `.reset items`, stripping the weapon.
- **Tests**: GatheringProfessionTests, FishingProfessionTests, EquipmentEquipTests.
- **Impact**: Cross-test contamination. Combat tests fail because weapon was stripped by gathering test cleanup.
- **Fix**: Use `BotClearInventoryAsync()` (bags only) instead of `.reset items` when only inventory needs clearing.
- **Status**: OPEN
- **Severity**: High

### BT-VERIFY-003: Item Addition Without Inventory Verification
- **Observed**: `.additem` calls don't verify the item actually appeared in the bag snapshot. If bags are full or server rejects, test proceeds with wrong state.
- **Tests**: VendorBuySellTests, LootCorpseTests, CraftingProfessionTests.
- **Fix**: After `.additem`, poll snapshot until item appears in `BagContents` (like GatheringProfessionTests does for fishing pole).
- **Status**: OPEN
- **Severity**: Medium

### BT-VERIFY-004: Spell Learning Without Verification
- **Observed**: `.learn` calls don't verify spell appears in `SpellList` snapshot. If `.learn` fails silently (permissions, server error), test continues with wrong setup.
- **Tests**: FishingProfessionTests (7 spell learns with no verification), CraftingProfessionTests.
- **Fix**: After `.learn`, verify spell in snapshot. Use `AssertCommandSucceeded` on the trace.
- **Status**: OPEN
- **Severity**: Medium

### BT-VERIFY-005: Cleanup Revive Without Teleport
- **Observed**: `DeathCorpseRunTests` revives bot after failed corpse run but doesn't teleport back to safe zone. Bot left at remote Durotar death location.
- **Impact**: Downstream tests find bot in unexpected position.
- **Fix**: Always pair revive with teleport to Orgrimmar safe zone in cleanup.
- **Status**: OPEN
- **Severity**: High

### BT-VERIFY-006: GM Mode Toggle Corruption on FG Failure
- **Observed**: `CombatLoopTests` turns `.gm off` for FG combat (line 133) and restores `.gm on` at cleanup (line 409). If test fails between those lines, FG remains in `.gm off` state.
- **Impact**: All subsequent FG tests see GM mode off — teleports fail, `.learn`/`.damage` fail.
- **Fix**: Use try/finally to guarantee `.gm on` restoration. Or don't toggle GM mode at all.
- **Status**: OPEN
- **Severity**: High

---

## 10. Bot Coordination & Idle Parking

### BT-PARK-001: Tests Park One Bot Idle While Only Testing the Other
- **Observed**: Multiple tests teleport one bot to Orgrimmar bank and leave it idle for the entire test duration while only the other bot is exercised. This defeats the purpose of having a dual-client test harness.
- **Tests**:
  - **FishingProfessionTests**: Parks FG at Orgrimmar, only tests BG fishing. "Park FG bot at Orgrimmar to reduce coordinator interference."
  - **GatheringProfessionTests**: Parks non-active bot at Orgrimmar 4+ times. Mining test parks BG; Herbalism test parks FG. Each gather scenario parks the idle bot separately.
  - **DeathCorpseRunTests**: Parks FG at Orgrimmar during BG corpse run scenario.
- **Impact**: FG bot sits visibly idle on Orgrimmar bank for minutes. No assertions on the idle bot. No validation that the idle bot is even alive or responsive. If the idle bot errors out, nobody notices. Wastes test time — both bots should be doing meaningful work.
- **Root Cause**: Tests use parking as a workaround for CombatCoordinator sending competing GOTO actions. The real fix is pausing the coordinator during tests, not parking bots.
- **Fix**: (1) Both bots should execute the same test in the same area (fish together, mine different nodes, die together). (2) If truly only one bot can be active, assert on the idle bot's state at test end. (3) Add `PauseCoordinatorAsync()` to suppress AI GOTO actions during focused tests instead of parking.
- **Status**: OPEN
- **Severity**: High

### BT-PARK-002: No Assertion on Idle Bot Health/State
- **Observed**: When a bot is parked idle, there is zero verification at test end that the parked bot is still alive, responsive, and in a valid state. The parked bot could have been killed by mobs, crashed, disconnected, or fallen through the world.
- **Tests**: All tests that park (FishingProfessionTests, GatheringProfessionTests, DeathCorpseRunTests).
- **Impact**: Silent failures accumulate. If the parked FG bot crashes, all subsequent FG tests fail with no clear root cause traced back to the parking test.
- **Fix**: At minimum, assert parked bot is still alive and in the expected position at test end.
- **Status**: OPEN
- **Severity**: Medium

### BT-PARK-003: Bots Not Teleported Together
- **Observed**: When a test requires a specific location, only the active bot is teleported there. The other bot stays wherever the last test left it. This means:
  - The user can't observe both bots reacting in the same environment
  - BG bot behavior near the test area is never validated
  - Snapshot comparisons between FG and BG at the same location are impossible
- **Tests**: Most tests except CombatRangeTests, BasicLoopTests, and OrgrimmarGroundZAnalysisTests (which do teleport both).
- **Fix**: Default pattern should be: teleport BOTH bots to the test area, run test on both, compare results. Parking should be the exception with documented justification.
- **Status**: OPEN
- **Severity**: High

---

## 11. Feedback & Observability Gaps

### BT-FEEDBACK-001: Silent Test Timeouts With No Progress Indication
- **Observed**: Long-running tests (DeathCorpseRunTests 3min, GatheringProfessionTests 1min) have polling loops with no progress output until completion or timeout. From the outside, the test appears frozen.
- **Impact**: User can't distinguish between "test running normally" and "test hung/errored". No way to know if FG is sitting idle because it's parked or because it crashed.
- **Fix**: Add periodic progress logging every 10s in polling loops: "Still waiting for corpse run... BG pos=(x,y,z) FG pos=(x,y,z)".
- **Status**: OPEN
- **Severity**: Medium

### BT-FEEDBACK-002: No Aggregate Test Summary at End of Suite
- **Observed**: When running the full LiveValidation suite, individual test output is interleaved and hard to parse. There's no summary showing which bots participated in which tests, which bots errored, or cumulative state drift.
- **Impact**: User sees "47 passed, 2 failed" but can't tell which tests exercised FG vs BG, or if FG was idle for 80% of the suite.
- **Fix**: Add a test summary that logs per-test: bot(s) exercised, duration, location, and any warnings.
- **Status**: OPEN
- **Severity**: Low

### BT-FEEDBACK-003: Error-Out Without Assertion — Test "Passes" on Silent Failure
- **Observed**: Some tests catch exceptions in FG paths and emit warnings instead of failing. If the FG bot errors out during a test, the test continues with BG only and reports "passed" — the user has no idea FG failed unless they read the verbose output.
- **Tests**: CombatLoopTests ("FG WARN: FG auto-attack failed"), DeathCorpseRunTests (FG failure → warning), GatheringProfessionTests (FG crash → continues with BG).
- **Cross-reference**: BT-LOGIC-002 (FG failures silently downgraded to warnings).
- **Impact**: FG could be completely non-functional and the suite shows green. The user sees FG sitting idle and doesn't know why.
- **Fix**: FG failure should be a test failure with clear output: "FG FAILED: [reason]. BG continued alone." Or use `Skip.If` with explicit message.
- **Status**: OPEN
- **Severity**: High

---

## 12. BG Bot Movement State Issues

### BT-MOVE-001: MovementFlags Not Resetting After Teleport
- **Observed**: After BG bot is teleported (`.go xyz`), MovementFlags may retain stale values from the pre-teleport state. `ResetMovementStateForTeleport` clears flags to `MOVEFLAG_NONE`, but the physics engine may re-apply flags (e.g., `MOVEFLAG_FORWARD`) from the input state before the ground snap completes.
- **Impact**: Stale movement flags cause incorrect heartbeat packets to the server. If MOVEFLAG_FORWARD persists after a teleport-to-stationary, the server sees the player as moving when they're standing still.
- **Fix**: Verify MovementFlags are `MOVEFLAG_NONE` after teleport completion (after ground snap). Add assertion in tests.
- **Status**: OPEN
- **Severity**: Medium

### BT-MOVE-002: Falling Detection Broken When Teleported Mid-Air
- **Observed**: When BG bot is teleported to a position above ground level (e.g., Z+3 offset for undermap safety), the physics engine should detect freefall and set `MOVEFLAG_FALLINGFAR`. The ground snap mechanism (`_needsGroundSnap`) runs physics frames to settle, but if the terrain query returns no ground (navmesh gap or unloaded tile), the bot may stay floating with `MOVEFLAG_NONE` instead of falling.
- **Impact**: Bot hovers in air instead of falling to ground. Server sees stationary player at incorrect Z → position desync → mob evade, gathering fail, etc.
- **Fix**: After ground snap timeout (`GROUND_SNAP_MAX_FRAMES`), if Z hasn't converged to ground, force `MOVEFLAG_FALLINGFAR` and apply gravity until a ground contact is detected.
- **Status**: OPEN
- **Severity**: High

---

## Priority Fix Order

1. **BT-PARK-001** — Stop parking bots idle; both bots exercise every test (High — the #1 visible problem)
2. **BT-FEEDBACK-003** — FG error-out must be a hard failure, not silent warning (High — stops hiding FG bugs)
3. **BT-COMBAT-002** — Fix creature teleport ACK bug (Critical — causes combat test failures)
4. **BT-TELE-001** — Safe teleport helper for FG (Critical — prevents client crashes)
5. **BT-PARK-003** — Teleport both bots together by default (High — enables observation & comparison)
6. **BT-COMBAT-001** — Implement proper auto-attack toggle pattern (High — fundamental combat reliability)
7. **BT-SETUP-001** — Standardized test cleanup pattern (High — reduces all fixture contamination)
8. **BT-VERIFY-006** — Fix GM mode toggle corruption with try/finally (High — prevents state leak cascade)
9. **BT-VERIFY-002** — Use BotClearInventoryAsync instead of .reset items (High — stops cross-test contamination)
10. **BT-VERIFY-005** — Always pair cleanup revive with teleport (High — prevents position contamination)
11. **BT-DEATH-001** — Move death test to Orgrimmar (High — reduces crash risk + test time)
12. **BT-LOGIC-002** — Make FG failures hard failures (High — stops hiding FG bugs)
13. **BT-VERIFY-001** — Fix dead-state guard silent command blocking (High — stops hidden command failures)
14. **BT-FEEDBACK-001** — Add periodic progress logging to long tests (Medium — observability)
15. **BT-PARK-002** — Assert on idle bot state at test end (Medium — catch silent failures)
16. **BT-ITEM-001** — Centralize item setup (Medium — reduces duplication)
17. **BT-VERIFY-003/004** — Verify item add and spell learn in snapshot (Medium — stops silent setup failures)
18. **BT-LOGIC-001** — Consolidate distance helpers (Low — code quality)
19. **BT-FEEDBACK-002** — Add aggregate test summary (Low — nice to have)
