# StarterQuestTests

BG-first live baseline for accepting and turning in quest `4641` through real NPC interaction.

## Bot Execution Mode

**BG-Only** — BG-only quest accept/turn-in baseline. No FG observation. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite currently exercises:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/BotRunnerService.Sequences.NPC.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/QuestNetworkClientComponent.cs`
- `Exports/BotRunner/BotRunnerService.Snapshot.cs`

## Test Method

### Quest_AcceptAndTurnIn_StarterQuest

**Bots:** BG only. FG is a packet/timing reference for future follow-up, not an asserted path in this suite.

## Test Flow

1. `EnsureCleanSlateAsync()`.
2. Pre-flight teleport to Orgrimmar safe zone to stabilize the next teleport.
3. Force-remove stale quest `4641`.
4. Teleport near Kaltunk and wait until the quest giver is visible in `NearbyUnits`.
5. Dispatch `ActionType.AcceptQuest` with Kaltunk's GUID and quest `4641`.
6. Poll `QuestLogEntries` until quest `4641` appears.
7. Teleport near Gornek and wait until the turn-in NPC is visible.
8. Dispatch `ActionType.CompleteQuest` with Gornek's GUID and quest `4641`.
9. Poll `QuestLogEntries` until quest `4641` is gone.

## Runtime Linkage

- Accept path: `BotRunnerService.ActionDispatch` -> `AcceptQuestSequence` in `BotRunnerService.Sequences.NPC.cs` -> `WoWSharpObjectManager.AcceptQuestFromNpcAsync(...)`.
- Complete path: `BotRunnerService.ActionDispatch` -> `CompleteQuestSequence` in `BotRunnerService.Sequences.NPC.cs` -> `WoWSharpObjectManager.CompleteQuestAsync(...)`.
- Snapshot verification path: `BotRunnerService.Snapshot` writes `Player.QuestLogEntries`.
- The suite is closer to the final behavior target than `QuestInteractionTests`, but it is still action-driven inside the test. The planned overhaul endpoint is task ownership via `AcceptQuestTask` / `CompleteQuestTask`.

## Metrics

The live assertions require:
- Kaltunk and Gornek visible through normal snapshot updates
- `AcceptQuest` dispatch succeeds and quest `4641` appears in the snapshot
- `CompleteQuest` dispatch succeeds and quest `4641` disappears from the snapshot

## Current Status

`2026-03-11`: the focused quest/NPC validation slice passed `8/8`. `StarterQuestTests` is green as a BG-first baseline, but it is still part of the remaining BRT-OVR-002 action-to-task migration work.
