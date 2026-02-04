namespace GameData.Core.Enums;

/// <summary>
/// Types of quests in World of Warcraft.
/// </summary>
public enum QuestType : byte
{
    /// <summary>
    /// Normal quest.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Elite quest requiring a group.
    /// </summary>
    Elite = 1,

    /// <summary>
    /// Life quest.
    /// </summary>
    Life = 21,

    /// <summary>
    /// PvP quest.
    /// </summary>
    PvP = 41,

    /// <summary>
    /// Raid quest.
    /// </summary>
    Raid = 62,

    /// <summary>
    /// Dungeon quest.
    /// </summary>
    Dungeon = 81,

    /// <summary>
    /// World event quest.
    /// </summary>
    WorldEvent = 82,

    /// <summary>
    /// Legendary quest.
    /// </summary>
    Legendary = 83,

    /// <summary>
    /// Escort quest.
    /// </summary>
    Escort = 84,

    /// <summary>
    /// Heroic quest.
    /// </summary>
    Heroic = 85,

    /// <summary>
    /// Raid (10).
    /// </summary>
    Raid10 = 88,

    /// <summary>
    /// Raid (25).
    /// </summary>
    Raid25 = 89
}