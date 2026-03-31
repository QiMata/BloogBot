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
        /// Used when the NPC isn't visible in ObjectManager (NpcFlags not yet sent by server).
        /// </summary>
        public ulong PackedGuid => 0xF130000000000000UL | ((ulong)NpcEntry << 24) | SpawnGuid;
    };

    // =========================================================================
    // HORDE BATTLEMASTERS — Orgrimmar
    // =========================================================================

    /// <summary>
    /// Warsong Emissary in Orgrimmar Valley of Honor.
    /// VMaNGOS creature entry 15105, npc_flags=2049 (GOSSIP|BATTLEMASTER).
    /// Multiple spawn positions — this one is near the Valley of Honor pond.
    /// </summary>
    // Spawn GUIDs from VMaNGOS creature table. Used to calculate packed GUIDs
    // for direct NPC interaction when ObjectManager doesn't have the NPC loaded.
    //
    // NOTE: Entry 15105 "Warsong Emissary" is event-only (game_event 19 "Call to Arms,
    // Warsong Gulch"). Use entry 14942 "Kartra Bloodsnarl" — a permanent WSG
    // battlemaster in Orgrimmar that is always spawned.
    public static readonly BattlemasterLocation OrgrimmarWsg = new(
        NpcName: "Kartra Bloodsnarl",
        NpcEntry: 14942,
        SpawnGuid: 4764,   // creature.guid for spawn at (1980.9,-4787.78,55.88)
        BgType: BattlegroundType.WarsongGulch,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1980.9f, -4787.78f, 55.88f),
        City: "Orgrimmar");

    public static readonly BattlemasterLocation OrgrimmarAb = new(
        NpcName: "Defilers Quartermaster",
        NpcEntry: 15106,
        SpawnGuid: 17153,
        BgType: BattlegroundType.ArathiBasin,
        Faction: DungeonEntryData.DungeonFaction.Horde,
        MapId: 1,
        Position: new Position(1706f, -4418f, 22f),
        City: "Orgrimmar");

    public static readonly BattlemasterLocation OrgrimmarAv = new(
        NpcName: "Frostwolf Ambassador Rokhstrom",
        NpcEntry: 15103,
        SpawnGuid: 28657,
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
        SpawnGuid: 54614,  // creature.guid for spawn at (-8454.6,318.9)
        BgType: BattlegroundType.WarsongGulch,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8454.6f, 318.9f, 121.0f),
        City: "Stormwind");

    public static readonly BattlemasterLocation StormwindAb = new(
        NpcName: "Lady Hotshot",
        NpcEntry: 15007,
        SpawnGuid: 54615,
        BgType: BattlegroundType.ArathiBasin,
        Faction: DungeonEntryData.DungeonFaction.Alliance,
        MapId: 0,
        Position: new Position(-8753f, 404f, 102f),
        City: "Stormwind");

    public static readonly BattlemasterLocation StormwindAv = new(
        NpcName: "Thelman Slatefist",
        NpcEntry: 15008,
        SpawnGuid: 54616,
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
