using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Static database of all vanilla 1.12.1 raid instance entrances.
/// Used by RaidTestFixture and TravelTask for raid navigation.
///
/// Data source: MaNGOS areatrigger_teleport table.
/// </summary>
public static class RaidEntryData
{
    /// <summary>
    /// A raid instance definition.
    /// </summary>
    public record RaidDefinition(
        string Name,
        string Abbreviation,
        uint InstanceMapId,
        uint EntranceMapId,
        Position EntrancePosition,
        Position? MeetingStonePosition,
        uint? MeetingStoneMapId,
        int MinLevel,
        int MaxPlayers,
        DungeonEntryData.DungeonFaction Faction,
        bool AttunementRequired,
        uint? AttunementQuestId = null,
        string? Notes = null);

    // =========================================================================
    // 20-MAN RAIDS
    // =========================================================================

    public static readonly RaidDefinition ZulGurub = new(
        Name: "Zul'Gurub",
        Abbreviation: "ZG",
        InstanceMapId: 309,
        EntranceMapId: 0,
        EntrancePosition: new Position(-11916f, -1243f, 65f),
        MeetingStonePosition: null,
        MeetingStoneMapId: null,
        MinLevel: 60,
        MaxPlayers: 20,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: false,
        Notes: "Stranglethorn Vale, no attunement. 3-day reset.");

    public static readonly RaidDefinition RuinsOfAhnQiraj = new(
        Name: "Ruins of Ahn'Qiraj",
        Abbreviation: "AQ20",
        InstanceMapId: 509,
        EntranceMapId: 1,
        EntrancePosition: new Position(-8409f, 1498f, 30f),
        MeetingStonePosition: null,
        MeetingStoneMapId: null,
        MinLevel: 60,
        MaxPlayers: 20,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: false,
        Notes: "Silithus. Requires AQ gate event completed server-side. 3-day reset.");

    // =========================================================================
    // 40-MAN RAIDS
    // =========================================================================

    public static readonly RaidDefinition MoltenCore = new(
        Name: "Molten Core",
        Abbreviation: "MC",
        InstanceMapId: 409,
        EntranceMapId: 0,
        EntrancePosition: new Position(-7513f, -1037f, 181f),
        MeetingStonePosition: new Position(-7522f, -1233f, 286f),  // BRM stone
        MeetingStoneMapId: 0,
        MinLevel: 60,
        MaxPlayers: 40,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: true,
        AttunementQuestId: 7848,
        Notes: "Blackrock Mountain. Attunement via Lothos Riftwalker. Entrance inside BRD.");

    public static readonly RaidDefinition OnyxiasLair = new(
        Name: "Onyxia's Lair",
        Abbreviation: "ONY",
        InstanceMapId: 249,
        EntranceMapId: 1,
        EntrancePosition: new Position(-4708f, -3727f, -62f),
        MeetingStonePosition: null,
        MeetingStoneMapId: null,
        MinLevel: 60,
        MaxPlayers: 40,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: true,
        AttunementQuestId: 6602,  // Drakefire Amulet (Horde)
        Notes: "Dustwallow Marsh. Drakefire Amulet quest chain. 5-day reset.");

    public static readonly RaidDefinition BlackwingLair = new(
        Name: "Blackwing Lair",
        Abbreviation: "BWL",
        InstanceMapId: 469,
        EntranceMapId: 0,
        EntrancePosition: new Position(-7666f, -1101f, 400f),
        MeetingStonePosition: new Position(-7522f, -1233f, 286f),  // BRM stone
        MeetingStoneMapId: 0,
        MinLevel: 60,
        MaxPlayers: 40,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: true,
        Notes: "Blackrock Mountain. Attunement: touch orb behind Drakkisath in UBRS. 7-day reset.");

    public static readonly RaidDefinition TempleOfAhnQiraj = new(
        Name: "Temple of Ahn'Qiraj",
        Abbreviation: "AQ40",
        InstanceMapId: 531,
        EntranceMapId: 1,
        EntrancePosition: new Position(-8233f, 2017f, 129f),
        MeetingStonePosition: null,
        MeetingStoneMapId: null,
        MinLevel: 60,
        MaxPlayers: 40,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: false,
        Notes: "Silithus. Requires AQ gate event. 7-day reset.");

    public static readonly RaidDefinition Naxxramas = new(
        Name: "Naxxramas",
        Abbreviation: "NAXX",
        InstanceMapId: 533,
        EntranceMapId: 0,
        EntrancePosition: new Position(3125f, -3748f, 136f),
        MeetingStonePosition: null,
        MeetingStoneMapId: null,
        MinLevel: 60,
        MaxPlayers: 40,
        Faction: DungeonEntryData.DungeonFaction.Both,
        AttunementRequired: true,
        Notes: "Eastern Plaguelands. Argent Dawn rep + quest attunement. Floating necropolis. 7-day reset.");

    // =========================================================================
    // COLLECTIONS
    // =========================================================================

    /// <summary>All vanilla raids.</summary>
    public static readonly IReadOnlyList<RaidDefinition> AllRaids =
    [
        ZulGurub, RuinsOfAhnQiraj,
        MoltenCore, OnyxiasLair, BlackwingLair, TempleOfAhnQiraj, Naxxramas,
    ];

    /// <summary>20-man raids.</summary>
    public static readonly IReadOnlyList<RaidDefinition> TwentyManRaids =
        AllRaids.Where(r => r.MaxPlayers == 20).ToList();

    /// <summary>40-man raids.</summary>
    public static readonly IReadOnlyList<RaidDefinition> FortyManRaids =
        AllRaids.Where(r => r.MaxPlayers == 40).ToList();

    /// <summary>Look up a raid by abbreviation (case-insensitive).</summary>
    public static RaidDefinition? FindByAbbreviation(string abbreviation) =>
        AllRaids.FirstOrDefault(r => string.Equals(r.Abbreviation, abbreviation, System.StringComparison.OrdinalIgnoreCase));
}
