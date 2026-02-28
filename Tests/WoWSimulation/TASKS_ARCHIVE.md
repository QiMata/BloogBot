# Task Archive

Completed items moved from TASKS.md.

## Completed 2026-02-28

1. [x] `WSIM-TST-001` Add negative-path tests for unsupported command dispatch and no-player guard branches.
   - Added 3 tests: `SendCommand_UnsupportedCommand_ThrowsNotSupportedException`, `SendCommand_MoveToPosition_WithoutPlayer_ReturnsFalse`, `SendCommand_CastSpell_WithoutPlayer_ReturnsFalse`.
   - Covers unknown command throw at `MockMangosServer.cs:69`, no-player guards at `:98-99` and `:148-149`.

2. [x] `WSIM-TST-002` Add direct health-path coverage for `GetPlayerHealth`.
   - Added 2 tests: `SendCommand_GetPlayerHealth_WithPlayer_ReturnsPlayerHealth`, `SendCommand_GetPlayerHealth_WithoutPlayer_ReturnsZero`.
   - Covers `GetPlayerHealth` at `MockMangosServer.cs:140-143`.

3. [x] `WSIM-TST-003` Strengthen movement simulation assertions with payload verification.
   - Added 2 tests: `SendCommand_MoveToPosition_EventPayloadContainsFromToDuration`, `SendCommand_MoveToPosition_UpdatesPlayerPosition`.
   - Asserts `From`, `To`, `Duration` in event payload and verifies player position update.

4. [x] `WSIM-TST-004` Add interaction failure tests for invalid/non-interactable targets.
   - Added 2 tests: `SendCommand_InteractWithObject_InvalidId_ReturnsFalse`, `SendCommand_InteractWithObject_NonInteractableObject_ReturnsFalse`.
   - Covers `InteractWithObject` false returns at `MockMangosServer.cs:125-126`.

5. [x] `WSIM-TST-005` Implement corpse lifecycle simulation hooks and tests (death, release, resurrection-ready).
   - Added `KillPlayer` and `ResurrectPlayer` commands to `MockMangosServer.SendCommand`.
   - Added 7 tests: `KillPlayer_SetsHealthToZeroAndFiresDeathEvent`, `KillPlayer_AlreadyDead_ReturnsFalse`, `KillPlayer_WithoutPlayer_ReturnsFalse`, `ResurrectPlayer_RestoresHealthAndFiresResurrectionEvent`, `ResurrectPlayer_AlreadyAlive_ReturnsFalse`, `ResurrectPlayer_WithoutPlayer_ReturnsFalse`, `CorpseLifecycle_FullDeathAndResurrectionCycle`.
   - Exercises `EventType.Death` and `EventType.Resurrection` which were previously unused.

6. [x] `WSIM-TST-006` Make command latency test-configurable to keep simulation loops fast and deterministic.
   - Added `commandLatencyMs` constructor parameter (default 10, preserving original behavior).
   - Changed `Task.Delay(10)` to conditional `Task.Delay(_commandLatencyMs)` when > 0.
   - Added 3 tests: `Constructor_DefaultLatency_PreservesOriginalBehavior`, `Constructor_ZeroLatency_RunsFasterThanDefault`, `Constructor_ZeroLatency_BehaviorParityWithDefault`.
   - All new tests in this session use `commandLatencyMs: 0` for fast deterministic execution.

7. [x] `WSIM-TST-007` Remove blocking task operations in tests to satisfy `xUnit1031` and avoid deadlock-prone waits.
   - **Done (2026-02-28).** Converted `EventHistory_TracksAllEvents` and `ClearEventHistory_RemovesAllEvents` from `void` with `.Result` to `async Task` with `await`.
