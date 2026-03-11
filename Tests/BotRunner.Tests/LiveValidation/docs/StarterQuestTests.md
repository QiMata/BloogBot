# StarterQuestTests

Tests the player-action quest flow: accept quest from NPC, turn in to different NPC — using ActionType dispatches (not GM commands).

## Test Methods (1)

### Quest_AcceptAndTurnIn_StarterQuest

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Stabilize zone | Teleport to Orgrimmar (1629, -4373, 12). Wait for settlement. |
| 1 | Remove stale quest | `EnsureQuestAbsentAsync(4641)` — `.quest remove 4641`. Wait 8s for absence. Wait 1s for server propagation. |
| 2 | Teleport to quest giver | Kaltunk at (-607.43, -4251.33, 39.04+3), Map 1. Wait for settlement. |
| 3 | Find Kaltunk | `FindNpcByEntryAsync(10176)` — retry 3x, 1s delays. Assert GUID != 0. |
| 4 | Accept quest | **Dispatch `ActionType.AcceptQuest`** with `LongParam = kaltunkGuid`, `IntParam = 4641`. Assert Success. |
| 5 | Verify accepted | Poll 10s for quest 4641 in `QuestLogEntries`. On failure: `.quest remove`, retry once. |
| 6 | Teleport to turn-in | Gornek at (-600.13, -4186.19, 41.27+3), Map 1. Wait for settlement. |
| 7 | Find Gornek | `FindNpcByEntryAsync(3143)`. If not found: `.respawn`, retry. Assert GUID != 0. |
| 8 | Complete quest | **Dispatch `ActionType.CompleteQuest`** with `LongParam = gornekGuid`, `IntParam = 4641`. Assert Success. |
| 9 | Verify completed | Poll 10s for quest absence from `QuestLogEntries` (turn-in removes quest). |

**Cleanup:** Finally block removes quest if still present. Teleport back to Orgrimmar.

**StateManager/BotRunner Action Flow:**

**AcceptQuest dispatch chain:**
1. ActionMessage with `ActionType.AcceptQuest`, `LongParam=npcGuid`, `IntParam=4641`
2. `AcceptQuestSequence` OR `_objectManager.AcceptQuestFromNpcAsync(npcGuid, questId)` (packet-based path when params provided)
3. Packet sequence:
   a. CMSG_QUESTGIVER_HELLO (open quest dialog with Kaltunk)
   b. Server responds SMSG_QUESTGIVER_QUEST_LIST (available quests)
   c. CMSG_QUESTGIVER_QUERY_QUEST (request quest 4641 details)
   d. Server responds SMSG_QUESTGIVER_QUEST_DETAILS
   e. CMSG_QUESTGIVER_ACCEPT_QUEST (accept quest 4641)
   f. Server adds quest → SMSG_QUEST_QUERY_RESPONSE

**CompleteQuest dispatch chain:**
1. `_objectManager.TurnInQuestAsync(gornekGuid, questId)` (packet-based)
2. CMSG_QUESTGIVER_HELLO → SMSG_QUESTGIVER_STATUS
3. CMSG_QUESTGIVER_REQUEST_REWARD → SMSG_QUESTGIVER_OFFER_REWARD
4. CMSG_QUESTGIVER_CHOOSE_REWARD → server completes quest, grants XP/rewards
5. Quest removed from log

**Key IDs:**
- Quest 4641 (Valley of Trials starter quest)
- NPC 10176 = Kaltunk (quest giver)
- NPC 3143 = Gornek (turn-in NPC)

**Key Coordinates:**
| NPC | X | Y | Z |
|-----|---|---|---|
| Kaltunk | -607.43 | -4251.33 | 39.04 |
| Gornek | -600.13 | -4186.19 | 41.27 |

**GM Commands:** `.quest remove 4641` (cleanup only), `.respawn` (if Gornek not found).

**Assertions:** Quest accepted via NPC interaction. Quest appears in log. Quest completed via NPC turn-in. Quest removed from log after turn-in.

**This is a true player-action test** — AcceptQuest and CompleteQuest use the same packet flow a real player would use, not GM shortcuts.
