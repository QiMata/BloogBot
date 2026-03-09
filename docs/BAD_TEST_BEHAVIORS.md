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
- **Fix**: Create `EnsureCleanSlateAsync(account)` helper: `.reset items` + revive + teleport to safe zone. Call at start of every test.
- **Status**: OPEN
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

## Priority Fix Order

1. **BT-COMBAT-002** — Fix creature teleport ACK bug (Critical — causes combat test failures)
2. **BT-COMBAT-001** — Implement proper auto-attack toggle pattern (High — fundamental combat reliability)
3. **BT-SETUP-001** — Standardized test cleanup pattern (High — reduces all fixture contamination)
4. **BT-DEATH-001** — Move death test to Orgrimmar (High — reduces crash risk + test time)
5. **BT-LOGIC-002** — Make FG failures hard failures (High — stops hiding FG bugs)
6. **BT-TELE-001** — Safe teleport helper for FG (Critical — prevents client crashes)
7. **BT-ITEM-001** — Centralize item setup (Medium — reduces duplication)
8. **BT-LOGIC-001** — Consolidate distance helpers (Low — code quality)
