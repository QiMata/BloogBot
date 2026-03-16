# QuestInteractionTests

Snapshot-plumbing coverage for quest state changes that are driven through GM chat commands.

## Bot Execution Mode

**Dual-Bot Conditional** — Both bots run quest scenarios in parallel. FG gated on IsFgActionable. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite currently exercises:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- `Exports/BotRunner/BotRunnerService.Snapshot.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
- `Exports/WoWSharpClient/Client/WorldClient.cs`

## Test Methods

### Quest_AddCompleteAndRemove_AreReflectedInSnapshots

**Bots:** BG (`TESTBOT2`) plus FG (`TESTBOT1`) when FG is actionable.

## Test Flow

1. `EnsureCleanSlateAsync()` and remove any stale copy of quest `786`.
2. Self-target so MaNGOS `.quest` commands resolve against the player.
3. Send `.quest add 786` through the normal chat/command path.
4. Poll `ActivitySnapshot.Player.QuestLogEntries` until quest `786` appears.
5. Send `.quest complete 786` through the same chat path.
6. Poll until the quest is removed or `QuestLog2` / `QuestLog3` changes.
7. Send `.quest remove 786` and verify the quest disappears from the snapshot.

## Runtime Linkage

- Test-owned setup/teardown: GM chat only.
- Chat dispatch path: `BotRunnerService.ActionDispatch` -> `CharacterAction.SendChat` -> `WoWSharpObjectManager.SendChatMessage()`.
- Snapshot projection path: `BotRunnerService.Snapshot` populates `Player.QuestLogEntries`.
- This is **not** the final task-driven questing path. It verifies that live quest state reaches snapshots correctly so later `AcceptQuestTask` / `CompleteQuestTask` work has a reliable observation surface.

## Metrics

The live assertions require:
- no command-table rejection for `.quest add`, `.quest complete`, or `.quest remove`
- quest `786` present after add
- quest state changed or removed after complete
- quest absent after remove

## Current Status

`2026-03-11`: the focused quest/NPC validation slice passed `8/8`. `QuestInteractionTests` remains green, but it is still snapshot-plumbing coverage rather than the task-driven questing suite planned in the overhaul.
