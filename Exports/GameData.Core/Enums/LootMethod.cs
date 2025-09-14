namespace GameData.Core.Enums;

/// <summary>
/// Loot methods used in group settings.
/// </summary>
public enum LootMethod : byte
{
    /// <summary>
    /// Free for all looting.
    /// </summary>
    FreeForAll = 0,

    /// <summary>
    /// Round robin looting.
    /// </summary>
    RoundRobin = 1,

    /// <summary>
    /// Master looter decides distribution.
    /// </summary>
    MasterLooter = 2,

    /// <summary>
    /// Group loot with rolling.
    /// </summary>
    GroupLoot = 3,

    /// <summary>
    /// Need before greed system.
    /// </summary>
    NeedBeforeGreed = 4
}