# Bad Behaviors — Integration Test Findings

Tracks observed bad behavior patterns from live integration test runs. Each entry includes: what was observed, root cause analysis, severity, and fix status.

---

## Categories

1. [Long Idle Times](#1-long-idle-times)
2. [Bot Coordination Issues](#2-bot-coordination-issues)
3. [Failure/Error Responses](#3-failureerror-responses)
4. [GM Command Circumvention](#4-gm-command-circumvention)
5. [Auto-Attack / Combat Logic](#5-auto-attack--combat-logic)
6. [Test Strategy Inconsistencies](#6-test-strategy-inconsistencies)
7. [FG-BG Packet Inconsistencies](#7-fg-bg-packet-inconsistencies)
8. [Map Transition / Crash Issues](#8-map-transition--crash-issues)

---

## 1. Long Idle Times

### BB-IDLE-001: FG Bot Stuck at LoginScreen (60s fixture timeout)
- **Observed**: FG bot (TESTBOT1) spent 60 seconds at `LoginScreen` before fixture gave up and proceeded with BG only.
- **Root Cause**: `GetCurrentScreenState()` treated empty loginState memory (WoW.exe just launched) as "charselect" instead of "login". Bot thought it was past login and never sent credentials. Additionally, `DefaultServerLogin()` Lua function silently fails when called via injected code — UI state prerequisites not met.
- **Fix**: Commit `f5613c0` — empty loginState returns `LoginScreen`; switched to UI-based login (SetText + AccountLogin_Login). LuaInitGracePeriod raised to 8s.
- **Status**: FIXED
- **Severity**: Critical — blocked all FG testing

### BB-IDLE-002: 8-Second Lua Init Grace Period
- **Observed**: After login screen detected, bot waits 8 seconds before first login attempt.
- **Impact**: Adds ~8s to every FG test run. Total FG login time is ~20-30s (8s grace + 15s realm wizard + 5s world entry).
- **Root Cause**: Conservative grace period to ensure WoW's Lua engine is initialized after process launch.
- **Status**: OPEN — Could potentially be reduced by probing Lua readiness instead of fixed delay.
- **Severity**: Low

---

## 2. Bot Coordination Issues

### BB-COORD-001: FG Bot Unavailable — Tests Degrade to BG-Only
- **Observed**: When FG bot fails to enter world, tests proceed with BG only. FG-specific assertions silently skipped via `IsFgActionable` guards.
- **Impact**: Tests pass but FG behavior is never validated. User sees "FG='N/A'" in fixture output.
- **Root Cause**: Historical — FG login was broken (BB-IDLE-001). Now fixed, but guard pattern remains.
- **Status**: MONITORING — Confirm FG consistently enters world after `f5613c0` fix.
- **Severity**: High

### BB-COORD-002: BG Snapshot Oscillation (CharacterSelect Flicker)
- **Observed**: BG bot's `CharacterSelectScreen.IsOpen` always returns true, causing snapshot `ScreenState` to flicker between `InWorld` and `CharacterSelect`.
- **Impact**: Fixture uses "ever seen InWorld" deduplication, but downstream tests may see stale snapshots.
- **Root Cause**: `WoWSharpObjectManager.CharacterSelectScreen.IsOpen` doesn't properly track state.
- **Status**: OPEN
- **Severity**: Medium

### BB-COORD-003: Parking Idle Bot During Single-Bot Tests
- **Observed**: Some tests teleport idle bot to Orgrimmar, others don't. Idle bot can interfere with test scenarios (aggroing mobs, corpse proximity).
- **Status**: OPEN — Need consistent parking pattern.
- **Severity**: Medium

---

## 3. Failure/Error Responses

### BB-ERR-001: Silent SOAP FAULT Responses
- **Observed**: `ExecuteGMCommandAsync` returns `"FAULT: There is no such command."` but many callers discard return value. Test setup state is silently corrupted.
- **Impact**: Tests run against incorrect state (e.g., spell not learned, level not set).
- **Root Cause**: SOAP callers need to check return value or use `AssertGMCommandSucceededAsync`.
- **Status**: OPEN — Audit all SOAP callers.
- **Severity**: High

### BB-ERR-002: UI_ERROR_MESSAGE Not Captured
- **Observed**: Client-side errors (spell not learned, can't use item) show as `UI_ERROR_MESSAGE` in chat but aren't systematically captured/asserted in tests.
- **Status**: OPEN
- **Severity**: Medium

---

## 4. GM Command Circumvention

### BB-GM-001: Combat Tests Use `.die` Instead of Real Damage
- **Observed**: Death tests use `.die` GM command. This bypasses actual combat mechanics — no HP tracking, no death log, no kill credit.
- **Impact**: Doesn't validate real combat damage paths.
- **Status**: OPEN — Should have tests where bots actually kill mobs and receive real damage.
- **Severity**: Medium

### BB-GM-002: `.damage` for HP Reduction Instead of Mob Attacks
- **Observed**: Some tests apply damage via GM instead of actual mob combat.
- **Impact**: Doesn't test threat, aggro radius, evade mechanics.
- **Status**: OPEN
- **Severity**: Medium

### BB-GM-003: Instant Skill/Spell Setup via GM
- **Observed**: Tests use `.learn`/`.setskill` to shortcut profession setup. This is correct for setup but bypasses trainer interaction flow.
- **Impact**: OK for unit tests, but integration tests should validate the full trainer→learn→practice flow.
- **Status**: ACCEPTABLE for setup, but need separate trainer interaction tests.
- **Severity**: Low

---

## 5. Auto-Attack / Combat Logic

### BB-COMBAT-001: BG Auto-Attack Requires Movement Heartbeat
- **Observed**: BG bot's `CMSG_ATTACKSWING` is rejected by MaNGOS if no recent movement packet sent. Bot must send `MSG_MOVE_HEARTBEAT` before attack and keep sending 500ms heartbeats during combat.
- **Impact**: Without heartbeat, bot stands next to mob doing nothing.
- **Root Cause**: MaNGOS validates movement timestamps when processing attack swing.
- **Fix**: `WoWSharpObjectManager.StartMeleeAttack()` sends pre-attack heartbeat and sets `IsAutoAttacking=true`. `MovementController.ShouldSendPacket()` sends heartbeats while `IsAutoAttacking`.
- **Status**: FIXED (commit `46f1be0`)
- **Severity**: Critical

### BB-COMBAT-002: FG Auto-Attack Uses Lua Toggle (Action Slot 72)
- **Observed**: FG bot checks `IsCurrentAction(72)` to detect auto-attack state, then toggles via `CastSpellByName('Attack')`.
- **Risk**: Action slot 72 assumes default action bar layout. Custom UIs or addon modifications could break this.
- **Alternative**: Could use `AttackTarget()` API which is slot-independent.
- **Status**: OPEN — Low risk on private servers with no addons.
- **Severity**: Low

### BB-COMBAT-003: Real Mob Combat — Both BG and FG Pass
- **Observed**: `CombatLoopTests.Combat_AutoAttacksMob_DealsDamageInMeleeRange` validates real auto-attack damage on live mobs for both bots.
- **BG**: Passes reliably with 3-attempt retry loop. Intermittent evade (~20%) handled by re-engaging after mob returns to spawn.
- **FG**: Now also passes — both bots target Scorpid Workers in the same Valley of Trials area. FG was previously targeting Lazy Peons (Friendly NPCs) — see BB-COMBAT-006.
- **Status**: FIXED (both BG and FG, 3/3 runs)
- **Severity**: Low (was Critical)

### BB-COMBAT-005: MSG_MOVE_TELEPORT Processed for Creatures as Player Teleport
- **Observed**: BG bot's `MovementHandler` called `NotifyTeleportIncoming()` for ALL `MSG_MOVE_TELEPORT` packets, including creature position updates. When MaNGOS sends a creature teleport (mob position correction), the BG bot's movement state was reset: `_isBeingTeleported=true` → heartbeat disrupted → mob can't maintain combat → evade.
- **Root Cause**: `MSG_MOVE_TELEPORT` is used by MaNGOS for both player teleports AND creature position updates. The handler didn't check the GUID — it assumed all teleports were for the local player. Creature GUIDs have high nibble `F1xx` (e.g., `F130000C34002FA7`).
- **Fix**: Guard `NotifyTeleportIncoming` to only fire when `teleportGuid == Player.Guid`. Creature teleports are still processed as position updates via `QueueUpdate` with `WoWObjectType.Unit`.
- **Status**: FIXED (MovementHandler.cs)
- **Severity**: Critical — was the root cause of intermittent BG mob evade

### BB-COMBAT-006: UnitReaction Unreliable in Snapshots
- **Observed**: BG bot's `WoWUnit.UnitReaction` is never populated from server packets — always defaults to `Hated(0)`. FG bot returns `Friendly(4)` for hostile mobs after GM mode (`.gm off` doesn't fully clear the reaction override).
- **Impact**: Cannot use UnitReaction to distinguish hostile from friendly creatures. Combat test was targeting Lazy Peons (Friendly) on FG because the UnitReaction filter excluded hostile mobs incorrectly.
- **Workaround**: Filter by creature entry ID (known hostile entries: 3098/3124/3108). Added `unitReaction` field (33) to WoWUnit protobuf for visibility/diagnostics.
- **Fix needed**: BG needs FactionTemplate→UnitReaction mapping using DBC faction data. FG needs to verify `.gm off` clears the reaction override.
- **Status**: OPEN (workaround in place)
- **Severity**: Medium

### BB-COMBAT-004: CastSpell(int) is No-Op on FG
- **Observed**: `ObjectManager.CastSpell(int spellId)` does nothing on FG bot. Only `CastSpell(string spellName)` via Lua works.
- **Impact**: Any code path that calls `CastSpell(int)` silently fails on FG. BG sends CMSG_CAST_SPELL correctly.
- **Status**: KNOWN — Documented in CLAUDE.md. FG uses Lua exclusively.
- **Severity**: Medium — Need to ensure all combat profiles use string overload for FG.

---

## 6. Test Strategy Inconsistencies

### BB-TEST-001: Inconsistent Cleanup Between Tests
- **Observed**: Some tests use `.reset items` in setup, others use selective `DestroyItem`. Some don't clean up at all.
- **Impact**: Test N's leftover state affects Test N+1 results.
- **Status**: OPEN — Need standardized setup/teardown pattern.
- **Severity**: Medium

### BB-TEST-002: Skip Conditions Vary Widely
- **Observed**: Tests skip on different conditions: `IsPathfindingReady`, `IsFgActionable`, `IsReady`, node spawn availability. No central registry of preconditions.
- **Status**: OPEN
- **Severity**: Low

### BB-TEST-003: Shared Fixture Means No Test Isolation
- **Observed**: All 24 test classes share one `LiveBotFixture` instance. Character state persists across test classes. A test that crashes WoW.exe affects all subsequent tests.
- **Impact**: Flaky test results depending on execution order.
- **Status**: KNOWN — Architectural limitation. `xUnit` collection fixtures are shared by design.
- **Severity**: Medium

---

## 7. FG-BG Packet Inconsistencies

### BB-PKT-001: Movement Architecture Mismatch
- **FG**: Uses native ClickToMove/control bit simulation — WoW client generates MSG_MOVE_* packets internally.
- **BG**: Constructs and sends MSG_MOVE_* packets explicitly via MovementController.
- **Impact**: BG movement may differ in timing, frequency, and flags from what WoW client would send. Server anti-cheat could flag BG packets.
- **Status**: KNOWN — Fundamental architectural difference.
- **Severity**: Medium

### BB-PKT-002: Facing Update Mismatch
- **FG**: `SendMovementUpdate()` calls native `CMovement::ExecuteMovement` at 0x00600A30.
- **BG**: Sends explicit `MSG_MOVE_SET_FACING` packet.
- **Impact**: Timing/format differences may cause server-side position desync.
- **Status**: OPEN
- **Severity**: Low

### BB-PKT-003: No FG Receive Packet Hook
- **Observed**: FG PacketLogger only captures outbound CMSG. Inbound SMSG not directly captured — inferred from state changes.
- **Impact**: Can't compare FG inbound packets with BG inbound packets. Can't verify BG correctly handles all SMSG that FG client handles natively.
- **Status**: OPEN (FG-PKT-005 task blocked on ProcessMessage vtable disassembly)
- **Severity**: Medium

### BB-PKT-004: Fishing Spell Targeting Difference
- **FG**: Lua `CastSpellByName('Fishing')` — WoW client handles location targeting natively.
- **BG**: Must explicitly calculate bobber position (18yd in front) and send `TARGET_FLAG_DEST_LOCATION` (0x0040).
- **Fix**: Commit `4cfc4f4` — BG now auto-detects fishing spells and uses location targeting.
- **Status**: FIXED
- **Severity**: Medium

---

## 8. Map Transition / Crash Issues

### BB-CRASH-001: WM_USER During Packet Processing
- **Observed**: FG bot crashed during map transitions. `SendMessage(WM_USER)` dispatched work during WoW's packet processing (mid-teardown).
- **Fix**: Changed to `PostMessage(WM_USER)` + delegate queue with `ManualResetEventSlim`.
- **Status**: FIXED
- **Severity**: Critical

### BB-CRASH-002: Stale Object Pointers During Continent Transition
- **Observed**: During zeppelin/boat crossings, ContinentId changes to 0xFFFFFFFF. Object pointers become invalid. Accessing stale `WoWObject.Pointer` crashes with AccessViolationException (.NET 8 does NOT catch these).
- **Fix**: Polling loop clears ObjectsBuffer immediately on continent transition detection. `IsContinentTransition` property blocks snapshot builders.
- **Status**: PARTIALLY FIXED — Need to verify all code paths check `IsContinentTransition`.
- **Severity**: Critical

### BB-CRASH-003: ThreadSynchronizer Paused During Transitions
- **Observed**: During map transitions, ThreadSynchronizer is paused to prevent Lua calls while WoW state is unstable. If pause doesn't clear correctly, all Lua calls permanently blocked.
- **Monitoring**: CrashTrace log records pause/resume events. Polling loop resumes after `isLoggedIn && !isLoadingWorld`.
- **Status**: MONITORING
- **Severity**: High

### BB-CRASH-004: MapChange Detection Gaps
- **Observed**: Map ID changes (e.g., continent → dungeon) detected via `_prevContinentId != continentId`. But rapid map changes (portal → loading → zone) may race with polling interval (500ms).
- **Status**: OPEN — Need stress testing with rapid zone transitions.
- **Severity**: Medium

---

## Priority Fix Order

1. **BB-COMBAT-003 (FG)** — Investigate FG mob evade after GM teleport position desync
2. **BB-COORD-001** — Confirm FG consistently enters world
3. **BB-ERR-001** — Audit SOAP callers for FAULT handling
4. **BB-TEST-001** — Standardize test cleanup
5. **BB-CRASH-002** — Verify all code paths check IsContinentTransition
6. **BB-COORD-003** — Consistent bot parking pattern
