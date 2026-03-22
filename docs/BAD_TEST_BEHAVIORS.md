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
- **Observed**: Tests teleport bots to various locations but don't teleport back afterward.
- **Fix**: Mitigated by `EnsureCleanSlateAsync` — 16/24 test files call it at start, which revives+teleports to Orgrimmar safe zone. Remaining 8 have equivalent setup.
- **Status**: **MITIGATED**
- **Severity**: Medium

---

## 2. Combat & Auto-Attack Issues

### BT-COMBAT-001: Auto-Attack Not Using Toggle Pattern (BloogBot Style)
- **Observed**: `CombatLoopTests` and `CombatRangeTests` send `StartMeleeAttack` action which calls `WoWSharpObjectManager.StartMeleeAttack()`. This sends a single `CMSG_ATTACKSWING` + pre-attack heartbeat. The original BloogBot (DrewKestrell) uses an auto-attack **toggle** via action bar slot — `IsCurrentAction(72)` checks if auto-attack is active, `CastSpellByName('Attack')` toggles it.
- **Impact**: BG bot's single CMSG_ATTACKSWING may not establish the full auto-attack swing timer loop. If the server rejects the first swing (timing, position), there's no retry. The FG bot uses Lua `AttackTarget()` or `CastSpellByName('Attack')` which properly toggles the client's auto-attack state machine.
- **Root Cause**: BG bot sends CMSG_ATTACKSWING but doesn't verify SMSG_ATTACKSTART response. If the server rejects (out of range, facing wrong), the bot doesn't know.
- **Fix**: FG bot now uses `AttackTarget()` Lua API. BG bot still needs SMSG_ATTACKSTART verification.
- **Status**: **PARTIALLY FIXED** (`5a9f882` — FG uses AttackTarget(). BG SMSG_ATTACKSTART verification still open)
- **Severity**: High

### BT-COMBAT-002: CombatRangeTests.MeleeAttack Fails — Creature Teleport ACK Bug
- **Observed**: `MeleeAttack_WithinRange_TargetIsSelected` fails with `TargetGuid=0x0` after `StartMeleeAttack`. Creature teleport ACKs disrupted heartbeat sending.
- **Root Cause**: MovementHandler sent teleport ACK packets for creature teleports, setting `_isBeingTeleported=true` which blocked heartbeats.
- **Fix**: ACK now guarded by player GUID check — creature MSG_MOVE_TELEPORT processed as position updates only.
- **Status**: **FIXED** (`37a2c25`)
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
- **Observed**: `DeathCorpseRunTests` originally teleported to southern Durotar, far from graveyards.
- **Fix**: Moved death test to Orgrimmar with simple 8-step flow using `.tele name <char> Orgrimmar`.
- **Status**: **FIXED** (`18cb049`)
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
- **Observed**: Multiple tests independently declared the same item/spell constants.
- **Fix**: Centralized in `LiveBotFixture.TestItems` and `LiveBotFixture.TestSpells`. Local duplicates replaced in 6 files.
- **Status**: **FIXED**
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
- **Observed**: FG client crashes when teleporting to many coordinate locations.
- **Fix**: Limited FG to 3 nearest gathering spawns. Safe teleport helper added.
- **Status**: **FIXED** (`b1444da`)
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
- **Observed**: `Distance2D`, `Distance3D` copy-pasted across 9 files.
- **Fix**: Consolidated in `LiveBotFixture`. Local copies removed from all test files.
- **Status**: **FIXED** (`18cb049`)
- **Severity**: Low

### BT-LOGIC-002: FG Failures Silently Downgraded to Warnings
- **Observed**: Tests caught FG failures and emitted warnings instead of failing.
- **Fix**: FG failures are now hard assertions in MapTransition, CharLifecycle, Economy, and other tests.
- **Status**: **FIXED** (`2891847`)
- **Severity**: High

### BT-LOGIC-003: Magic Numbers for Timeouts
- **Observed**: Tests use hardcoded timeouts scattered throughout. No central timeout configuration.
- **Fix**: Deferred — many timeouts are context-specific (combat=8s, corpse-run=3min, teleport=3s).
- **Status**: **DEFERRED**
- **Severity**: Low

### BT-LOGIC-004: WaitForCondition Polling Intervals Vary
- **Observed**: Polling intervals ranged from 250ms to 3000ms.
- **Fix**: After BT-DELAY-001, remaining delays are 200-1500ms (all context-appropriate). No further action needed.
- **Status**: **MITIGATED**
- **Severity**: Low

---

## 9. Command & Verification Gaps

### BT-VERIFY-001: Dead-State Guard Silently Blocks Commands
- **Observed**: Dead-state guard blocked commands silently when bot was dead/ghost.
- **Fix**: `SendGmChatCommandAsync` now logs visible `[DEAD-GUARD]` warning in test output when commands are blocked.
- **Status**: **FIXED**
- **Severity**: High

### BT-VERIFY-002: .reset items Too Broad — Strips Equipment
- **Observed**: Tests used `.reset items` which strips ALL equipped gear, not just bags.
- **Fix**: Replaced with `BotClearInventoryAsync()` (bags only) in VendorBuySellTests and LootCorpseTests. Tests that need full gear strip (EquipmentEquipTests, FishingProfessionTests) keep `.reset items`.
- **Status**: **FIXED**
- **Severity**: High

### BT-VERIFY-003: Item Addition Without Inventory Verification
- **Observed**: `.additem` calls didn't verify items appeared in bag snapshot.
- **Fix**: `BotAddItemAsync` now polls for item in `BagContents` after `.additem`. Warns if not confirmed within 3s.
- **Status**: **FIXED** (`4aa177f`)
- **Severity**: Medium

### BT-VERIFY-004: Spell Learning Without Verification
- **Observed**: `.learn` calls didn't verify spell in `SpellList` snapshot.
- **Fix**: `BotLearnSpellAsync` now polls for spell in `SpellList` after `.learn`. Warns if not confirmed within 3s.
- **Status**: **FIXED** (`4aa177f`)
- **Severity**: Medium

### BT-VERIFY-005: Cleanup Revive Without Teleport
- **Observed**: Revive without teleport left bot at remote location.
- **Fix**: `EnsureCleanSlateAsync` always pairs revive with teleport to Orgrimmar safe zone.
- **Status**: **FIXED** (part of BT-SETUP-001)
- **Severity**: High

### BT-VERIFY-006: GM Mode Toggle Corruption on FG Failure
- **Observed**: CombatLoopTests `.gm off` not restored on failure.
- **Fix**: CombatLoopTests now uses try/finally for `.gm on` restoration.
- **Status**: **FIXED**
- **Severity**: High

---

## 10. Bot Coordination & Idle Parking

### BT-PARK-001: CombatCoordinator Interferes With Tests
- **Observed**: CombatCoordinator sent competing GOTO/combat actions during tests, interfering with test-directed bot behavior.
- **Fix**: Added `WWOW_TEST_DISABLE_COORDINATOR=1` env var that fully disables CombatCoordinator during test runs. Set in `LiveBotFixture.InitializeAsync`.
- **Status**: **FIXED** (`f1a3a97`)
- **Severity**: High

### BT-PARK-002: No Assertion on Idle Bot Health/State
- **Observed**: Parked bots have no end-of-test health/state verification.
- **Fix**: Deferred — low value since tests are serialized by xUnit collection and `EnsureCleanSlateAsync` resets state at start of each test.
- **Status**: **DEFERRED**
- **Severity**: Medium

### BT-PARK-003: Bots Not Teleported Together
- **Observed**: Only the active bot was teleported to the test area; the other stayed wherever the last test left it.
- **Fix**: 22/24 test classes already had FG parity via `IsFgActionable`. Added FG parity to VendorBuySellTests and converted `FishingProfessionTests` into a dual-bot task-owned Ratchet flow with hard FG assertions once FG is actionable.
- **Status**: **FIXED** (`f1a3a97`)
- **Severity**: High

---

## 11. Feedback & Observability Gaps

### BT-FEEDBACK-001: Silent Test Timeouts With No Progress Indication
- **Observed**: Long-running tests had no progress output during polling loops.
- **Fix**: All 4 shared polling helpers accept optional `progressLabel` that logs every 5s. Added labels to 6 test files for waits ≥10s. Inline 5s logging in `FindLivingMobAsync` and `FindLivingBoarAsync`.
- **Status**: **FIXED** (`3029d68`)
- **Severity**: Medium

### BT-FEEDBACK-002: No Aggregate Test Summary at End of Suite
- **Observed**: When running the full LiveValidation suite, individual test output is interleaved and hard to parse. There's no summary showing which bots participated in which tests, which bots errored, or cumulative state drift.
- **Impact**: User sees "47 passed, 2 failed" but can't tell which tests exercised FG vs BG, or if FG was idle for 80% of the suite.
- **Fix**: Add a test summary that logs per-test: bot(s) exercised, duration, location, and any warnings.
- **Status**: OPEN
- **Severity**: Low

### BT-FEEDBACK-003: Error-Out Without Assertion — Test "Passes" on Silent Failure
- **Observed**: FG failures were caught and downgraded to warnings.
- **Fix**: FG failures are now hard assertions in MapTransition, CharLifecycle, Economy, and other tests. Same fix as BT-LOGIC-002.
- **Status**: **FIXED** (`2891847`)
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

### BT-MOVE-003: Ratchet Shoreline Pathing Can Leave Fishing Bots Terrain-Stuck, Falling, or LOS-Blocked
- **Observed**: During the live fishing path, the bot can now reach the correct Ratchet fishing hole from the named teleport, but the final shoreline approach can still snag on terrain, run off the pier, or settle at a cast point with no clean LOS to fishable water. The live evidence is repeated `FishingTask los_blocked phase=move` plus the WoW error `Your cast didn't land in fishable water`.
- **Impact**: Fishing can fail intermittently even though the task-owned fishing contract is correct. This is likely the same short-horizon pathing/collision family behind other sporadic live failures where the bot reaches the area but not a usable final position.
- **Fix**: Prioritize pathfinding/native navigation logging for planned vs executed shoreline routes, then fix the corridor/collide-slide behavior so stop/fall handling and final-position selection leave the bot at a castable, LOS-valid point instead of stuck terrain.
- **Status**: OPEN
- **Severity**: High

---

## Status Summary

**Fixed (19/25):** BT-SETUP-001, BT-COMBAT-002, BT-TELE-001, BT-DEATH-001, BT-ITEM-001, BT-LOGIC-001, BT-LOGIC-002, BT-VERIFY-001, BT-VERIFY-002, BT-VERIFY-003, BT-VERIFY-004, BT-VERIFY-005, BT-VERIFY-006, BT-PARK-001, BT-PARK-003, BT-FEEDBACK-001, BT-FEEDBACK-003, BT-COMBAT-001 (partial)

**Mitigated (2):** BT-SETUP-003, BT-LOGIC-004

**Deferred (2):** BT-LOGIC-003 (timeout centralization), BT-PARK-002 (idle bot assertion)

**Still Open (5):**
1. **BT-SETUP-002** — SOAP revive fallback (Medium)
2. **BT-COMBAT-001** — BG bot SMSG_ATTACKSTART verification (High, partially fixed)
3. **BT-COMBAT-003** — `.damage` cleanup leaves stale combat state (Medium)
4. **BT-FEEDBACK-002** — Aggregate test summary (Low)
5. **BT-MOVE-003** - Ratchet shoreline terrain/LOS pathing gap (High)
