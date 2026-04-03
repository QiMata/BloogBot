using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Static database of battlemaster NPC positions by faction and BG type.
/// Used by BattlegroundCoordinator to navigate bots to the correct NPC for queuing.
/// Titles were verified against the MaNGOS creature templates because nearby city
/// battlemasters are clustered together and title is what identifies the BG owner.
/// </summary>
public static class BattlemasterData
{
    private const int WarsongGulchMinimumLevel = 10;
    private const int ArathiBasinMinimumLevel = 20;
    private const int AlteracValleyMinimumLevel = 51;

    public enum BattlegroundType
    {
        AlteracValley = 1,   // mapId 30,  40v40
        WarsongGulch = 2,    // mapId 489, 10v10
        ArathiBasin = 3,     // mapId 529, 15v15
    }

    /// <summary>
    /// A battlemaster NPC location.
    /// </summary>
    public record BattlemasterLocation(
        string NpcName,
        string NpcTitle,
        uint NpcEntry,
        uint SpawnGuid,
        BattlegroundType BgType,
        DungeonEntryData.DungeonFaction Faction,
        uint MapId,
        Position Position,
        string City)
    {
        /// <summary>
        /// Packed creature GUID for direct NPC interaction.
        /// Format: 0xF13000{entry:4hex}{spawnGuid:6hex}
        /// Used when the NPC is not visible in ObjectManager yet.
        /// </summary>
        public ulong PackedGuid => 0xF130000000000000UL | ((ulong)NpcEntry << 24) | SpawnGuid;
    };

    // =========================================================================
    // HORDE BATTLEMASTERS - Orgrimmar
    // =========================================================================

    public static readonly BattlemasterLocation OrgrimmarWsg = new(
        NpcName: "Brakgul Deathbringer",
        NpcTitle: "Warsong Gulch Battlemaster",
        NpcEntry: 3890,
        SpawnGuid: 4765,
        BgType: BattlegroundType.WarsongGulch,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1990.41f, -4794.15f, 55.90f),
        City: "Orgrimmar");

    public static readonly BattlemasterLocation OrgrimmarAb = new(
        NpcName: "Deze Snowbane",
        NpcTitle: "Arathi Basin Battlemaster",
        NpcEntry: 15006,
        SpawnGuid: 4761,
        BgType: BattlegroundType.ArathiBasin,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(2002.26f, -4796.74f, 56.85f),
        City: "Orgrimmar");

    public static readonly BattlemasterLocation OrgrimmarAv = new(
        NpcName: "Kartra Bloodsnarl",
        NpcTitle: "Alterac Valley Battlemaster",
        NpcEntry: 14942,
        SpawnGuid: 4764,
        BgType: BattlegroundType.AlteracValley,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1980.90f, -4787.78f, 55.88f),
        City: "Orgrimmar");

    // =========================================================================
    // ALLIANCE BATTLEMASTERS - Stormwind
    // =========================================================================

    public static readonly BattlemasterLocation StormwindWsg = new(
        NpcName: "Elfarran",
        NpcTitle: "Warsong Gulch Battlemaster",
        NpcEntry: 14981,
        SpawnGuid: 54614,
        BgType: BattlegroundType.WarsongGulch,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8454.62f, 318.85f, 120.97f),
        City: "Stormwind");

    public static readonly BattlemasterLocation StormwindAb = new(
        NpcName: "Lady Hoteshem",
        NpcTitle: "Arathi Basin Battlemaster",
        NpcEntry: 15008,
        SpawnGuid: 54625,
        BgType: BattlegroundType.ArathiBasin,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8420.48f, 328.71f, 120.89f),
        City: "Stormwind");

    public static readonly BattlemasterLocation StormwindAv = new(
        NpcName: "Thelman Slatefist",
        NpcTitle: "Alterac Valley Battlemaster",
        NpcEntry: 7410,
        SpawnGuid: 42893,
        BgType: BattlegroundType.AlteracValley,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8424.55f, 342.81f, 120.89f),
        City: "Stormwind");

    // =========================================================================
    // COLLECTIONS
    // =========================================================================

    /// <summary>All battlemaster locations.</summary>
    public static readonly IReadOnlyList<BattlemasterLocation> All =
    [
        OrgrimmarWsg, OrgrimmarAb, OrgrimmarAv,
        StormwindWsg, StormwindAb, StormwindAv,
    ];

    /// <summary>Horde battlemasters.</summary>
    public static readonly IReadOnlyList<BattlemasterLocation> Horde =
        All.Where(b => b.Faction == DungeonEntryData.DungeonFaction.Horde).ToList();

    /// <summary>Alliance battlemasters.</summary>
    public static readonly IReadOnlyList<BattlemasterLocation> Alliance =
        All.Where(b => b.Faction == DungeonEntryData.DungeonFaction.Alliance).ToList();

    /// <summary>Find the battlemaster for a given BG type and faction.</summary>
    public static BattlemasterLocation? FindBattlemaster(BattlegroundType bgType, DungeonEntryData.DungeonFaction faction) =>
        All.FirstOrDefault(b => b.BgType == bgType && b.Faction == faction);

    public static int GetMinimumLevel(BattlegroundType bgType) => bgType switch
    {
        BattlegroundType.WarsongGulch => WarsongGulchMinimumLevel,
        BattlegroundType.ArathiBasin => ArathiBasinMinimumLevel,
        BattlegroundType.AlteracValley => AlteracValleyMinimumLevel,
        _ => WarsongGulchMinimumLevel,
    };

    public static int GetMinimumLevel(uint bgTypeId) => GetMinimumLevel((BattlegroundType)bgTypeId);
}
