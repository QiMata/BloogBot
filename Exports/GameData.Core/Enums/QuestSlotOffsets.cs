namespace GameData.Core.Enums;

public enum QuestSlotOffsets
{
    QUEST_ID_OFFSET = 0,
    QUEST_COUNT_STATE_OFFSET = 1,                        // including counters 6bits+6bits+6bits+6bits + state 8bits
    QUEST_TIME_OFFSET = 2
}