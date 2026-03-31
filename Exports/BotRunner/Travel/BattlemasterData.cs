using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Static database of battlemaster NPC positions by faction and BG type.
/// Used by BattlegroundCoordinator to navigate bots to the correct NPC for queuing.
///
/// Data source: MaNGOS creature table, filtered by NPC flags (UNIT_NPC_FLAG_BATTLEMASTER = 0x100000).
/// </summary>
public static class BattlemasterData
{
    public enum BattlegroundType
    {
        WarsongGulch = 2,    // mapId 489, 10v10
        ArathiBasin = 3,     // mapId 529, 15v15
        AlteracValley = 1,   // mapId 30,  40v40
    }

    /// <summary>
    /// A battlemaster NPC location.
    /// </summary>
    public record BattlemasterLocation(
        string NpcName,
        uint NpcEntry,
        BattlegroundType BgType,
        DungeonEntryData.DungeonFaction Faction,
        uint MapId,
        Position Position,
        string City);

    // =========================================================================
    // HORDE BATTLEMASTERS — Orgrimmar
    // =========================================================================

    public static readonly BattlemasterLocation OrgrimmarWsg = new(
        NpcName: "Warsong Emissary",
        NpcEntry: 15105,
        BgType: BattlegroundType.WarsongGulch,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1665.8f, -4344.9f, 61.3f),
        City: "Orgrimmar");

    public static readonly BattlemasterLocation OrgrimmarAb = new(
        NpcName: "Defilers Quartermaster",  // Arathi Basin battlemasters are nearby
        NpcEntry: 15106,
        BgType: BattlegroundType.ArathiBasin,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1706f, -4418f, 22f),
        City: "Orgrimmar");

    public static readonly BattlemasterLocation OrgrimmarAv = new(
        NpcName: "Frostwolf Ambassador Rokhstrom",
        NpcEntry: 15103,
        BgType: BattlegroundType.AlteracValley,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1710f, -4414f, 22f),
        City: "Orgrimmar");

    // =========================================================================
    // ALLIANCE BATTLEMASTERS — Stormwind
    // =========================================================================

    public static readonly BattlemasterLocation StormwindWsg = new(
        NpcName: "Elfarran",
        NpcEntry: 14981,
        BgType: BattlegroundType.WarsongGulch,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8454.6f, 318.9f, 121.0f),
        City: "Stormwind");

    public static readonly BattlemasterLocation StormwindAb = new(
        NpcName: "Lady Hotshot",
        NpcEntry: 15007,
        BgType: BattlegroundType.ArathiBasin,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8753f, 404f, 102f),
        City: "Stormwind");

    public static readonly BattlemasterLocation StormwindAv = new(
        NpcName: "Thelman Slatefist",
        NpcEntry: 15008,
        BgType: BattlegroundType.AlteracValley,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8749f, 408f, 102f),
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

    /// <summary>Find the nearest battlemaster for a given BG type and faction.</summary>
    public static BattlemasterLocation? FindBattlemaster(BattlegroundType bgType, DungeonEntryData.DungeonFaction faction) =>
        All.FirstOrDefault(b => b.BgType == bgType && b.Faction == faction);
}
