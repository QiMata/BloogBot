# QuestInteractionTests

Tests quest log tracking: add quest via GM, complete it, remove it — verify each state change in snapshots.

## Test Methods (1)

### Quest_AddCompleteAndRemove_AreReflectedInSnapshots

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Remove stale quest | `EnsureQuestAbsentAsync(account, 786)` — `.quest remove 786` to guarantee clean state |
| 1 | Self-target | `BotSelectSelfAsync()` — `.targetself` (required for quest commands) |
| 2 | Add quest | `.quest add 786` via `SendGmChatCommandTrackedAsync()` with 1500ms delay |
| 3 | Verify add | Assert no "FAULT:" or "no such command". Poll 12s for quest in `QuestLogEntries` with `QuestLog1 == 786`. |
| 4 | Complete quest | `.quest complete 786` via GM chat |
| 5 | Verify complete | `WaitForQuestCompletedChangedOrRemovedAsync()` — 12s poll. Returns true if quest removed from log OR QuestLog2/QuestLog3 changed (objective state). |
| 6 | Remove quest | `.quest remove 786` via GM chat |
| 7 | Verify remove | Poll 12s for quest absence from `QuestLogEntries` |

**Cleanup:** Finally block removes quest via `.quest remove 786` on any failure.

**StateManager/BotRunner Role:**

**No ActionType dispatches.** All quest operations use GM chat commands. BotRunnerService processes `SendChat` actions. The test validates that snapshot quest log tracking correctly reflects server-side quest state changes:
- `snapshot.Player.QuestLogEntries` — array of quest slots
- `QuestLog1` = quest ID
- `QuestLog2`, `QuestLog3` = objective counters/state

Quest log updates arrive via SMSG_QUEST_QUERY_RESPONSE and SMSG_UPDATE_OBJECT packets → ObjectManager parses quest fields → snapshot captures them.

**Key IDs:** Quest 786 (test quest).

**GM Commands:** `.quest add 786`, `.quest complete 786`, `.quest remove 786`, `.targetself`.

**Assertions:** Quest appears in log after add. Quest state changes after complete. Quest absent after remove.
