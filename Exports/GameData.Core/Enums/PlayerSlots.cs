namespace GameData.Core.Enums;

public enum PlayerSlots
{
    // first slot for item stored (in any way in player m_items data)
    PLAYER_SLOT_START = 0,
    // last+1 slot for item stored (in any way in player m_items data)
    PLAYER_SLOT_END = 118,
    PLAYER_SLOTS_COUNT = PLAYER_SLOT_END - PLAYER_SLOT_START
}