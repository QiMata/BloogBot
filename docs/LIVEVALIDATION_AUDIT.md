# LiveValidation Test Audit — 2026-03-06

## Test Results Summary

| Run | Date | Passed | Failed | Skipped | Blocker |
|-----|------|--------|--------|---------|---------|
| Pre-realmd restart | 2026-03-06 | 4/17 | 13 | 11 | TESTBOT2 auth failure (StrictVersionCheck) |
| Post-realmd restart | 2026-03-06 | 39/40 | 1 | 0 | BG bot target selection in combat |
| Post-audit fixes | 2026-03-06 | 40/40 | 0 | 0 | None |

### Audit Fix Summary

Fixes applied to 6 HIGH and 3 MEDIUM severity items. All 40 tests now pass. Changes add assertion coverage for dispatch results, teleport verification, pathfinding failure detection, and GM command validation.

---

## Audit Findings (35 total)

### Severity Distribution

| Category | HIGH | MEDIUM | LOW | Total |
|----------|------|--------|-----|-------|
| GM Command Abuse | 1 | 0 | 0 | 1 |
| Timing/Parallelism | 1 | 6 | 6 | 13 |
| Missing Assertions | 5 | 10 | 6 | 21 |
| **Totals** | **7** | **16** | **12** | **35** |

---

## HIGH Severity (must fix)

### GM-1: Silent pathfinding failure in gathering tests — FIXED
- **File:** `GatheringProfessionTests.cs:539-541`
- **Test:** `TryGatherAtSpawns` (Mining + Herbalism)
- **Issue:** When pathfinding times out, the test `continue`s to the next spawn instead of failing. A bot with broken pathfinding can still pass if any spawn is within walking distance.
- **Fix:** `Assert.Fail()` when pathfinding times out. No silent continue.

### AST-4: UseItem dispatch result unchecked — FIXED
- **File:** `ConsumableUsageTests.cs:125-129`
- **Test:** `UseConsumable_ElixirOfLionsStrength_BuffApplied`
- **Issue:** `UseItem` action dispatch result not checked — could be silently rejected.
- **Fix:** `Assert.Equal(ResponseResult.Success, useResult)` on dispatch.

### AST-6: Target selection failure doesn't halt combat test
- **File:** `CombatLoopTests.cs:175-179`
- **Test:** `Combat_AutoAttacksMob_DealsDamageInMeleeRange`
- **Issue:** Target selection failure returns `false` but the outer logic already handles this. However, the actual root cause is the BG bot not updating `TargetGuid` in snapshots after `StartMeleeAttack`.
- **Fix:** Fix BG bot's target state tracking in `WoWSharpObjectManager` or `BotRunnerService`.

### AST-7: Conditional pass in ranged attack test — FIXED
- **File:** `CombatRangeTests.cs:349-363`
- **Test:** `RangedAttack_WithinRange_TargetIsSelected`
- **Issue:** Logs "maybe melee" without asserting; test exits as passed regardless.
- **Fix:** `Skip.If(!selected)` — skips instead of false-passing when no ranged weapon equipped.

### AST-15: Teleport position not verified after pathfinding setup — FIXED
- **File:** `GatheringProfessionTests.cs:434`
- **Test:** Mining/Herbalism
- **Issue:** After `BotTeleportAsync`, position is not verified. If teleport failed, test blames "node not found" instead.
- **Fix:** `Assert.True(teleportDist <= 50f)` after teleport — catches silent teleport failures.

### AST-18: Group invite/accept dispatch unchecked — FIXED
- **File:** `GroupFormationTests.cs:65,73`
- **Test:** `GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp`
- **Issue:** Invite and accept action dispatch results not captured or checked.
- **Fix:** `Assert.Equal(ResponseResult.Success)` for both invite and accept dispatches.

### TIM-6: 180s corpse run timeout with no intermediate logging — ALREADY FIXED
- **File:** `DeathCorpseRunTests.cs:659-668`
- **Test:** `Death_ReleaseAndRetrieve_ResurrectsPlayer`
- **Issue:** Waits up to 180s with no intermediate state logging. If stuck, gives no diagnostic info.
- **Status:** Already has per-tick logging at line 546 and reclaim delay logging at line 668. No change needed.

---

## MEDIUM Severity (should fix)

### TIM-1: Fixed 1200ms delay for movement stop — FIXED
- **File:** `GatheringProfessionTests.cs:545`
- **Fix:** Documented rationale — 1.2s empirically needed for WoW 1.12.1 to process movement stop before gather.

### TIM-2: Node detection polls every 1500ms — FIXED
- **File:** `GatheringProfessionTests.cs:484`
- **Fix:** Reduced to 500ms (6 polls in 3s window instead of 2).

### TIM-4: Fixed delays in out-of-range combat tests — N/A
- **File:** `CombatRangeTests.cs:202,213`
- **Fix:** N/A — delays are appropriate (teleport settle + server rejection, no observable event to poll).

### TIM-5: Pathfinding timeout lacks diagnostic state — FIXED
- **File:** `GatheringProfessionTests.cs:521-541`
- **Fix:** Added moveFlags, health, position dump on pathfinding timeout.

### TIM-7: Fishing test doesn't test FG bot
- **File:** `FishingProfessionTests.cs:107`
- **Fix:** `FishingProfessionTests` now stages both bots at Ratchet, dispatches `ActionType.StartFishing` for both, and hard-asserts the FG task-owned equip -> approach -> cast -> bobber -> loot-window -> bag-delta path once FG is actionable.

### TIM-10: Undocumented 180s ReclaimTimeout — FIXED
- **File:** `DeathCorpseRunTests.cs:50`
- **Fix:** Added XML doc explaining 3min covers server lag, reconnect, and crash recovery.

### TIM-12: Reconnect polling has no iteration logging — FIXED
- **File:** `GatheringProfessionTests.cs:568`
- **Fix:** Added log every 5th poll iteration for reconnect visibility.

### AST-1: Baseline snapshot null-check missing — FIXED
- **File:** `BasicLoopTests.cs:240`
- **Fix:** Added `Assert.NotNull(baseline?.Player)`.

### AST-2: Teleport dispatch result not verified — FIXED
- **File:** `BasicLoopTests.cs:176-186`
- **Fix:** Added `Assert.True(moved, ...)` after `TeleportAndVerifyAsync` before using position data.

### AST-3: No pre-condition assertion for item count — FIXED
- **File:** `CharacterLifecycleTests.cs:197-227`
- **Fix:** Added `Assert.Equal(0, beforeSlotsForItem)` after cleanup.

### AST-5: Snapshot null-check missing before auras — FIXED
- **File:** `ConsumableUsageTests.cs:87-90`
- **Fix:** Added `Assert.NotNull(playerBefore)`.

### AST-8: `.cast` GM command result unchecked — FIXED
- **File:** `CraftingProfessionTests.cs:254-258`
- **Fix:** Added `AssertCommandSucceeded(castTrace, label, ...)` after `.cast` fallback.

### AST-11: No pre-equip empty check — FIXED
- **File:** `EquipmentEquipTests.cs:102`
- **Fix:** Added `Assert.False(mainhandBeforeEquipped, ...)` after logging.

### AST-12: Equip dispatch result unchecked — FIXED
- **File:** `EquipmentEquipTests.cs:179-183`
- **Fix:** `Assert.Equal(ResponseResult.Success, equipResult)` on EquipItem dispatch.

### AST-13: Initial fishing skill not validated — FIXED
- **File:** `FishingProfessionTests.cs:114-118`
- **Fix:** Added `Assert.True(bgSkillBefore > 0, ...)` after initial skill read.

### AST-16: `.setskill` GM command result unchecked — FIXED
- **File:** `GatheringProfessionTests.cs:339,389`
- **Fix:** Added `AssertCommandSucceeded(setSkillTrace, label, ...)` for both mining and herbalism `.setskill`.

### AST-20: Double `.learn` failure not guarded — FIXED
- **File:** `TalentAllocationTests.cs:93`
- **Fix:** Added `Assert.True(learnTrace.DispatchResult == ResponseResult.Success, ...)` after retry.

---

## LOW Severity (nice to have)

| ID | File | Issue |
|----|------|-------|
| TIM-3 | GatheringProfessionTests.cs:560 | Fixed 2000ms post-gather wait |
| TIM-8 | CombatRangeTests.cs:195-230 | BG-only test, no FG variant |
| TIM-9 | CharacterLifecycleTests.cs:205 | Fixed 1000ms delay before snapshot |
| TIM-11 | LiveBotFixture.cs:213,252 | Fixed bootstrap delays |
| TIM-13 | LiveBotFixture.cs:45-48 | Command counters accumulate across tests |
| AST-9 | DeathCorpseRunTests.cs:385 | ReleaseCorpse dispatch assertion style |
| AST-10 | EconomyInteractionTests.cs:158 | Snapshot null not checked |
| AST-17 | GatheringProfessionTests.cs:150 | Skip.If vs Assert for skill cap |
| AST-19 | NpcInteractionTests.cs:115 | BackgroundBot null not asserted |
| AST-21 | CombatRangeTests.cs:163-164 | Skip.If vs Assert for mob detection |

---

## BG Bot Known Issues

1. **Target selection not reflected in snapshots** — `StartMeleeAttack` dispatches `CMSG_ATTACKSWING` but `TargetGuid` in the snapshot stays 0. Root cause: `WoWSharpObjectManager` or `BotRunnerService` doesn't update target state when processing the attack action.

2. **Zone boundary crash (FIXED)** — `EnumerateVisibleObjects` ACCESS_VIOLATION during sub-zone area cache resets. Fixed with SEH protection in FastCall.dll (commit `d4275a6`).

3. **Mining crash at zone boundaries (FIXED)** — `WoWObject.Interact()` null pointer dereference. Fixed with ManagerBase + Pointer guards (commit `adb9d0b`).
