# Completed Tasks Archive

> Consolidated from SESSION_HISTORY.md, TASKS.md, and ARCHIVE.md on 2026-02-12.

## Summary Table

| Tasks | Area | Status |
|-------|------|--------|
| 0-3, 5 | Foundation (offsets, movement, snapshots, object enum) | Done |
| 7-20 | GrindBot, combat rotations, quest system | Done |
| 21-23 | Protocol docs, snapshot population, descriptor audit | Done |
| 24 | Swimming recording (32/32 scenarios, Southfury River) | Done |
| 29-33 | Object updates, auth, world opcodes, movement validation | Done |
| 35-42 | Headless client assembly + feature parity | Done |
| 43-58 | Full protocol audit (937/937 tests) | Done |
| 59 | Build/test infrastructure refactor | Done |
| 62 | WoW process hardening (window poll, phone-home, orphan reaper, dup guard) | Done |
| 63 | Named pipe log sink (bot logs visible in StateManager console) | Done |
| 64a | DelegateServerDesiredState consolidation | Done |
| 64b | TestConstants swimming + 8/8 SwimmingValidationTests | Done |

## Completed Task Details

### Foundation (Tasks 0-5)
- **Task 0**: Finalize TASKS.md
- **Tasks 1-3**: CMovementInfo offsets, movement input controls, activity snapshot tests
- **Task 5**: Object enumeration

### GrindBot & Combat (Tasks 7-20)
- **Tasks 7-10**: GrindBot 7-phase state machine, combat rotations (9 classes), rest/food/drink, navigation integration
- **Tasks 11-14**: Stuck detection, pull spells, target blacklisting, hotspot patrol
- **Tasks 15-20**: Quest system (IQuestRepository, QuestCoordinator, NPC interaction, objective scanning, group management, dungeon navigation)

### Protocol & Descriptors (Tasks 21-23)
- **Task 21**: MaNGOS server source -> 7 protocol reference docs in `docs/server-protocol/`
- **Tasks 22-23**: ActivitySnapshot population, WoWUnit/WoWPlayer descriptor audit

### Swimming Recording (Task 24)
- Recording captured 2026-02-09: `Dralrahgra_Durotar_2026-02-09_19-16-08.json`
- All swim sub-categories (forward, ascend, descend, water entry/exit) in one recording
- TestConstants.cs updated, 8/8 SwimmingValidationTests passing

### Object Updates & Headless Client (Tasks 29-42)
- **Tasks 29-33**: Object updates, WoWLocalPlayer, SRP auth, world server opcodes, MovementController validation
- **Tasks 35-36**: Game loop/tick system, headless client login+move (17/17 passing)
- **Tasks 37-42**: Combat, NPC interaction, dual-client orchestration, loot, inventory, login init opcodes

### Protocol Audit (Tasks 43-58)
- All CMSG/SMSG formats fixed
- 937/937 protocol tests passing

### Infrastructure (Tasks 59, 62-64)
- **Task 59**: Build/test infrastructure refactor (naming, unified output, DLL tests, service tests, MangosStackFixture, test runner)
- **Task 62**: WoW process hardening (window poll, phone-home wait, orphan reaper, duplicate guard)
- **Task 63**: Named pipe log sink
- **Task 64a**: Consolidated duplicate DelegateServerDesiredState
- **Task 64b**: TestConstants swimming update + SwimmingValidationTests

### Miscellaneous Fixes (not numbered)
- Login disconnect fix (WM_USER during handshake)
- Realm list spam fix (login loop)
- Speed offsets fix
- FastCall.dll stale copy root cause + BotServiceFixture auto-fix
- StateManager startup race condition fix
- Compilation fixes (missing usings in ServiceStartupIntegrationTest.cs)

---

## Session History

### 2026-02-09: FastCall.dll Root Cause + Test Infrastructure
- **Root Cause Found**: FastCall.dll in `Bot\Debug\net8.0\` was stale 12KB version missing `LuaCall` export
- **Race Condition Fixed**: StateManager and test fighting over DLL locks during build
- **BotServiceFixture**: Now verifies FastCall.dll size and auto-copies correct version
- **InfrastructureTestGuard refactored**: Kills lingering processes, provides `EnsureCleanState()`
- **StateManagerProcessHelper created**: Centralized StateManager process lifecycle
- **5 test classes updated** to accept InfrastructureTestGuard via constructor injection
- Test results: 21 unit tests passing, infrastructure guard injection verified

### 2025-06-20: Tasks 62, 63, 64a
- Task 62: WoW process hardening
- Task 63: Named pipe log sink
- Task 64a: Consolidated duplicate DelegateServerDesiredState
- Created `docs/recording-swimming-guide.md` and `run-swimming-recording-test.ps1`

### Sessions 1-3: Physics Test Suite & Movement Recording (2026-02-08)
- Physics test suite: 42/43 passing
- RecordingCalibrationTests: 11 tests mapped to real recordings
- Movement recording system: NaN/Infinity sanitization, player spline data
- 33 recordings captured across Orgrimmar, Durotar, Undercity, Barrens, Blackrock Spire
- Only `Dralrahgra_Durotar_2026-02-08_12-28-15.json` has player spline data

---

## Completed Documentation Consolidation (2026-02-12)

The following files were consolidated/archived during the Phase 0 cleanup:
- `docs/SESSION_HISTORY.md` -> merged into this file
- `docs/next-session-prompt.md` -> archived (stale session handoff)
- `docs/SWIMMING-RECORDING-QUICKSTART.md` -> archived (recording complete)
- `docs/recording-swimming-guide.md` -> archived (recording complete)
- `docs/SNAPSHOT_COMMUNICATION_HANDOFF.md` -> archived (fixes applied)
- `docs/FILE_INVENTORY.md` -> archived (redundant with PROJECT_STRUCTURE.md)
- `docs/GUIDELINES.md` -> merged into CLAUDE.md

---

## Phase 1 — Cleanup Obsolete Code & Refactor Tests (completed 2026-02-15)

### 1.1 Remove Obsolete WoWClient Methods — DONE

Removed all 14 `[Obsolete]` sync-over-async wrappers from `WoWClient`. All consumers migrated:
- **WoWSharpObjectManager**: 20 direct `_woWClient.SyncMethod()` calls → fire-and-forget async
- **NetworkGameClientAdapter**: 8 delegating calls → fire-and-forget async
- **MovementController**: 3 `SendMovementOpcode` calls → fire-and-forget async
- **LoginScreen/RealmSelectScreen/CharacterSelectScreen**: 5 calls → fire-and-forget async
- **LiveServerFixture**: 3 calls → `await` async
- **WoWSharpClientIntegrationTests**: 1 call → `await` async

### 1.2 Harden ForegroundBotRunner WoW Process Launching — DONE (already implemented)

All items already exist in `Services/WoWStateManager/StateManagerWorker.cs`:
1. Window polling with `EnumWindows` (15s timeout, 250ms intervals)
2. Bot phone-home polling via `CurrentActivityMemberList` (30s timeout, 500ms intervals)
3. Orphan process reaper (kills after 60s without snapshot)
4. CharacterSettings deduplication by `AccountName`

### 1.3 Unified Integration Test Fixture — EVALUATED (deferred)

Existing infrastructure sufficient: `InfrastructureTestCollection` + `InfrastructureTestGuard` serializes tests, `StateManagerProcessHelper` handles lifecycle. Single ordered fixture impractical (xUnit no ordering, manual tests can't chain, stages 6-8 not yet implemented).

### 1.4 Remove/Consolidate Redundant Test Files — EVALUATED (deferred)

Duplication is cosmetic (helper methods in 6+ files), not structural. Each test file serves distinct purpose. `StateManagerProcessHelper.FindSolutionRoot()` and `ServiceHealthChecker` already consolidate common logic.

### 1.5 Fix Test Infrastructure Issues — DONE (already implemented)

`MangosStackFixture.VerifyMySqlCredentialsAsync()` verifies MySQL, trait attributes use modern xUnit v2 API, `StateManagerSettings.ResetInstance()` exists.

---

## Phase 2 — PhysicsEngine Alignment with PhysX CCT (completed 2026-02-15)

**Final Baseline: 58 tests passing, 1 skipped**

| Recording | Avg Err | SS P99 | Tolerance |
|-----------|---------|--------|-----------|
| FlatRunForward | 0.054y | 0.239y | avg<0.055, p99<0.25 |
| StandingJump | 0.045y | 0.201y | avg<0.055, p99<0.25 |
| RunningJumps | 0.049y | 0.233y | avg<0.055, p99<0.25 |
| LongFlatRun | 0.013y | 0.119y | avg<0.055, p99<0.25 |
| FallFromHeight | 0.012y | 0.115y | avg<0.055, p99<0.25 |
| ComplexMixed | 0.029y | 0.125y | avg<0.055, p99<0.25 |
| Swimming | 0.019y | 0.120y | avg<0.055, p99<0.25 |
| Elevator | 0.071y | 0.463y | avg<0.13, p99<1.0 |

**Cross-recording mode aggregates (30,763 frames):**
| Mode | Frames | Avg Err | P99 |
|------|--------|---------|-----|
| Ground | 23,591 | 0.039y | 0.202y |
| Air | 2,658 | 0.004y | 0.160y |
| Swim | 2,296 | 0.019y | 0.080y |
| Transport | 1,647 | 0.110y | 0.862y |

**Remaining error:** Horizontal frame-timing alignment (~0.119y systematic = 1 frame at 7 y/s). Transport error inherent to open-loop replay.

### Tasks completed:
- **2.1** Consolidate Dual Three-Pass — PhysicsThreePass.cpp deleted, DecomposeMovement inlined
- **2.2** Ground Z Precision — GetGroundZ refinement in DOWN pass + all snap functions
- **2.3** Slope Angle — 0.5f confirmed correct via recordings (no change needed)
- **2.4** Walk Experiment — full PhysX retry + recovery sweep + ground snap
- **2.5** Climbing Sensor — SKIPPED (all tests within tolerance)
- **2.6** STEP_HEIGHT Reduction — SKIPPED (all tests within tolerance)
- **2.7** Update Docs — README.md updated with walk experiment + GetGroundZ docs
- **2.8** Swimming — backward swim pitch fix, avg=0.019y P99=0.080y
- **2.9** Transport — inherent to open-loop replay, no fix needed
- **2.10** Airborne — avg=0.004y, P99=0.160y, excellent accuracy

---

## Phase 4.3 — Bidirectional Group Formation (completed 2026-02-15)

### Result: SUCCESS — both phases passed

**Test setup:** ORWR1 (Warrior "Dralrahgra", Foreground) + ORSH1 (Shaman "Lokgarn", Background), coordinated by `CharacterStateSocketListener` group test state machine.

**Phase 1 — Background invites Foreground:**
```
GROUP_TEST P1: Injecting SendGroupInvite for 'ORSH1' → invite 'Dralrahgra'
GROUP_TEST P1: Injecting AcceptGroupInvite for 'ORWR1'
GROUP_TEST P1: Verify — BG PartyLeader=8, FG PartyLeader=5
GROUP_TEST P1: SUCCESS — Group formed! Now leaving group.
GROUP_TEST P1: Both bots confirmed out of group. Starting Phase 2.
```

**Phase 2 — Foreground invites Background:**
```
GROUP_TEST P2: Injecting SendGroupInvite for 'ORWR1' → invite 'Lokgarn'
GROUP_TEST P2: Injecting AcceptGroupInvite for 'ORSH1'
GROUP_TEST P2: Verify — BG PartyLeader=0, FG PartyLeader=5
GROUP_TEST P2: SUCCESS — Group formed! Bidirectional group test COMPLETE.
```

### Bugs Fixed During This Work

1. **CurrentAction echo-back (CRITICAL)**: `PopulateSnapshotFromObjectManager()` never cleared `CurrentAction`, causing infinite re-execution. Fixed by clearing in both `PopulateSnapshotFromObjectManager()` and `HandleRequest()`.

2. **`_behaviorTree ??=` never-replace**: Completed behavior trees lingered because `??=` never assigned over non-null. Changed to `_behaviorTree = null`.

3. **No Serilog in BackgroundBotRunner**: `BotRunnerService` uses `Serilog.Log` (static), but BackgroundBotRunner never configured Serilog. Added packages and configuration.

4. **FluentBehaviourTree Splice error**: `BuildBehaviorTreeFromActions` called `Splice()` without parent Sequence context. Wrapped in `.Sequence("StateManager Action Sequence")`.

### Files Modified
- `Exports/BotRunner/BotRunnerService.cs` — echo-back fix, tree cleanup, Splice fix
- `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs` — response action clearing, delivery logging
- `Services/BackgroundBotRunner/Program.cs` — Serilog configuration
- `Services/BackgroundBotRunner/BackgroundBotRunner.csproj` — Serilog packages
