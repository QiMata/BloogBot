using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Static database of all vanilla 1.12.1 dungeon instance entrances, meeting stone positions,
/// and metadata. Used by DungeonTestFixture and TravelTask for dungeon-related navigation.
///
/// Entrance coordinates are world-side portal positions (the position you walk to on the
/// overworld map to zone into the instance). Meeting stone positions are for the GameObjectType 23
/// objects near each dungeon entrance.
///
/// Data source: MaNGOS areatrigger_teleport table + gameobject table (type=23).
/// </summary>
public static class DungeonEntryData
{
    public enum DungeonFaction { Both, Horde, Alliance }

    /// <summary>
    /// A dungeon instance definition with entrance and meeting stone positions.
    /// </summary>
    public record DungeonDefinition(
        string Name,
        string Abbreviation,
        uint InstanceMapId,
        uint EntranceMapId,
        Position EntrancePosition,
        Position InstanceEntryPosition,
        Position? MeetingStonePosition,    // null if no meeting stone (city dungeons)
        uint? MeetingStoneMapId,           // null if no meeting stone
        int MinLevel,
        int MaxLevel,
        int MaxPlayers,
        DungeonFaction Faction,
        string? Wing = null);              // For multi-wing dungeons (SM, DM, Strat, BRS)

    // =========================================================================
    // CLASSIC DUNGEONS (Levels 13-30)
    // =========================================================================

    public static readonly DungeonDefinition RagefireChasm = new(
        Name: "Ragefire Chasm",
        Abbreviation: "RFC",
        InstanceMapId: 389,
        EntranceMapId: 1,
        EntrancePosition: new Position(1811f, -4410f, -18f),
        InstanceEntryPosition: new Position(0.797643f, -8.23429f, -15.5288f),
        MeetingStonePosition: null,  // City dungeon, no meeting stone
        MeetingStoneMapId: null,
        MinLevel: 13,
        MaxLevel: 18,
        MaxPlayers: 10,
        Faction: DungeonFaction.Horde);

    public static readonly DungeonDefinition WailingCaverns = new(
        Name: "Wailing Caverns",
        Abbreviation: "WC",
        InstanceMapId: 43,
        EntranceMapId: 1,
        EntrancePosition: new Position(-740f, -2214f, 16f),
        InstanceEntryPosition: new Position(-158.441f, 131.601f, -74.2552f),
        MeetingStonePosition: new Position(-723f, -2226f, 17f),
        MeetingStoneMapId: 1,
        MinLevel: 15,
        MaxLevel: 25,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition TheDeadmines = new(
        Name: "The Deadmines",
        Abbreviation: "DM",   // Note: not to be confused with Dire Maul (DireMaul)
        InstanceMapId: 36,
        EntranceMapId: 0,
        EntrancePosition: new Position(-11208f, 1672f, 24f),
        InstanceEntryPosition: new Position(-14.5732f, -385.475f, 62.4561f),
        MeetingStonePosition: new Position(-11209f, 1658f, 25f),
        MeetingStoneMapId: 0,
        MinLevel: 17,
        MaxLevel: 26,
        MaxPlayers: 10,
        Faction: DungeonFaction.Alliance);

    public static readonly DungeonDefinition ShadowfangKeep = new(
        Name: "Shadowfang Keep",
        Abbreviation: "SFK",
        InstanceMapId: 33,
        EntranceMapId: 0,
        EntrancePosition: new Position(-229f, 1571f, 76f),
        InstanceEntryPosition: new Position(-228.191f, 2111.41f, 76.8904f),
        MeetingStonePosition: new Position(-232f, 1566f, 77f),
        MeetingStoneMapId: 0,
        MinLevel: 22,
        MaxLevel: 30,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition BlackfathomDeeps = new(
        Name: "Blackfathom Deeps",
        Abbreviation: "BFD",
        InstanceMapId: 48,
        EntranceMapId: 1,
        EntrancePosition: new Position(4254f, 740f, -25f),
        InstanceEntryPosition: new Position(-150.234f, 106.594f, -39.779f),
        MeetingStonePosition: new Position(4247f, 745f, -23f),
        MeetingStoneMapId: 1,
        MinLevel: 24,
        MaxLevel: 32,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition TheStockade = new(
        Name: "The Stockade",
        Abbreviation: "STOCK",
        InstanceMapId: 34,
        EntranceMapId: 0,
        EntrancePosition: new Position(-8764f, 846f, 88f),
        InstanceEntryPosition: new Position(48.9849f, 0.483882f, -16.3942f),
        MeetingStonePosition: null,  // City dungeon
        MeetingStoneMapId: null,
        MinLevel: 24,
        MaxLevel: 32,
        MaxPlayers: 10,
        Faction: DungeonFaction.Alliance);

    public static readonly DungeonDefinition Gnomeregan = new(
        Name: "Gnomeregan",
        Abbreviation: "GNOMER",
        InstanceMapId: 90,
        EntranceMapId: 0,
        EntrancePosition: new Position(-5163f, 927f, 257f),
        InstanceEntryPosition: new Position(-329.098f, -3.20722f, -152.851f),
        MeetingStonePosition: new Position(-5160f, 921f, 258f),
        MeetingStoneMapId: 0,
        MinLevel: 29,
        MaxLevel: 38,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    // =========================================================================
    // MID-LEVEL DUNGEONS (Levels 30-50)
    // =========================================================================

    public static readonly DungeonDefinition RazorfenKraul = new(
        Name: "Razorfen Kraul",
        Abbreviation: "RFK",
        InstanceMapId: 47,
        EntranceMapId: 1,
        EntrancePosition: new Position(-4464f, -1666f, 82f),
        InstanceEntryPosition: new Position(1942.27f, 1544.23f, 83.3055f),
        MeetingStonePosition: new Position(-4460f, -1660f, 83f),
        MeetingStoneMapId: 1,
        MinLevel: 29,
        MaxLevel: 38,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition ScarletMonasteryGraveyard = new(
        Name: "Scarlet Monastery",
        Abbreviation: "SM-GY",
        InstanceMapId: 189,
        EntranceMapId: 0,
        EntrancePosition: new Position(2892f, -764f, 161f),
        InstanceEntryPosition: new Position(1687.27f, 1050.09f, 18.6773f),
        MeetingStonePosition: new Position(2880f, -770f, 162f),
        MeetingStoneMapId: 0,
        MinLevel: 28,
        MaxLevel: 38,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both,
        Wing: "Graveyard");

    public static readonly DungeonDefinition ScarletMonasteryLibrary = new(
        Name: "Scarlet Monastery",
        Abbreviation: "SM-LIB",
        InstanceMapId: 189,
        EntranceMapId: 0,
        EntrancePosition: new Position(2892f, -764f, 161f),
        InstanceEntryPosition: new Position(253.672f, -206.624f, 18.6773f),
        MeetingStonePosition: new Position(2880f, -770f, 162f),
        MeetingStoneMapId: 0,
        MinLevel: 33,
        MaxLevel: 40,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both,
        Wing: "Library");

    public static readonly DungeonDefinition ScarletMonasteryArmory = new(
        Name: "Scarlet Monastery",
        Abbreviation: "SM-ARM",
        InstanceMapId: 189,
        EntranceMapId: 0,
        EntrancePosition: new Position(2892f, -764f, 161f),
        InstanceEntryPosition: new Position(1608.1f, -318.919f, 18.6714f),
        MeetingStonePosition: new Position(2880f, -770f, 162f),
        MeetingStoneMapId: 0,
        MinLevel: 36,
        MaxLevel: 42,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both,
        Wing: "Armory");

    public static readonly DungeonDefinition ScarletMonasteryCathedral = new(
        Name: "Scarlet Monastery",
        Abbreviation: "SM-CATH",
        InstanceMapId: 189,
        EntranceMapId: 0,
        EntrancePosition: new Position(2892f, -764f, 161f),
        InstanceEntryPosition: new Position(853.179f, 1319.18f, 18.6714f),
        MeetingStonePosition: new Position(2880f, -770f, 162f),
        MeetingStoneMapId: 0,
        MinLevel: 38,
        MaxLevel: 44,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both,
        Wing: "Cathedral");

    public static readonly DungeonDefinition RazorfenDowns = new(
        Name: "Razorfen Downs",
        Abbreviation: "RFD",
        InstanceMapId: 129,
        EntranceMapId: 1,
        EntrancePosition: new Position(-4658f, -2524f, 85f),
        InstanceEntryPosition: new Position(2593.68f, 1111.23f, 50.9518f),
        MeetingStonePosition: new Position(-4653f, -2519f, 86f),
        MeetingStoneMapId: 1,
        MinLevel: 37,
        MaxLevel: 46,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition Uldaman = new(
        Name: "Uldaman",
        Abbreviation: "ULDA",
        InstanceMapId: 70,
        EntranceMapId: 0,
        EntrancePosition: new Position(-6072f, -2955f, 209f),
        InstanceEntryPosition: new Position(-228.859f, 46.1018f, -46.0186f),
        MeetingStonePosition: new Position(-6066f, -2951f, 210f),
        MeetingStoneMapId: 0,
        MinLevel: 41,
        MaxLevel: 51,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition ZulFarrak = new(
        Name: "Zul'Farrak",
        Abbreviation: "ZF",
        InstanceMapId: 209,
        EntranceMapId: 1,
        EntrancePosition: new Position(-6802f, -2890f, 9f),
        InstanceEntryPosition: new Position(1212.67f, 842.04f, 8.93346f),
        MeetingStonePosition: new Position(-6797f, -2886f, 10f),
        MeetingStoneMapId: 1,
        MinLevel: 44,
        MaxLevel: 54,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition Maraudon = new(
        Name: "Maraudon",
        Abbreviation: "MARA",
        InstanceMapId: 349,
        EntranceMapId: 1,
        EntrancePosition: new Position(-1433f, 2928f, 52f),
        InstanceEntryPosition: new Position(1016.83f, -458.52f, -43.4737f),
        MeetingStonePosition: new Position(-1428f, 2932f, 53f),
        MeetingStoneMapId: 1,
        MinLevel: 46,
        MaxLevel: 55,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    // =========================================================================
    // HIGH-LEVEL DUNGEONS (Levels 50-60)
    // =========================================================================

    public static readonly DungeonDefinition SunkenTemple = new(
        Name: "Sunken Temple",
        Abbreviation: "ST",
        InstanceMapId: 109,
        EntranceMapId: 0,
        EntrancePosition: new Position(-10456f, -3829f, -43f),
        InstanceEntryPosition: new Position(-315.903f, 100.197f, -131.849f),
        MeetingStonePosition: new Position(-10450f, -3824f, -42f),
        MeetingStoneMapId: 0,
        MinLevel: 50,
        MaxLevel: 56,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition BlackrockDepths = new(
        Name: "Blackrock Depths",
        Abbreviation: "BRD",
        InstanceMapId: 230,
        EntranceMapId: 0,
        EntrancePosition: new Position(-7178f, -920f, 166f),
        InstanceEntryPosition: new Position(456.929f, 34.0923f, -68.0896f),
        MeetingStonePosition: new Position(-7522f, -1233f, 286f),
        MeetingStoneMapId: 0,
        MinLevel: 52,
        MaxLevel: 60,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition LowerBlackrockSpire = new(
        Name: "Lower Blackrock Spire",
        Abbreviation: "LBRS",
        InstanceMapId: 229,
        EntranceMapId: 0,
        EntrancePosition: new Position(-7535f, -1002f, 275f),
        InstanceEntryPosition: new Position(78.3534f, -226.841f, 49.7662f),
        MeetingStonePosition: new Position(-7522f, -1233f, 286f),  // Shared with BRD
        MeetingStoneMapId: 0,
        MinLevel: 55,
        MaxLevel: 60,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both);

    public static readonly DungeonDefinition UpperBlackrockSpire = new(
        Name: "Upper Blackrock Spire",
        Abbreviation: "UBRS",
        InstanceMapId: 229,
        EntranceMapId: 0,
        EntrancePosition: new Position(-7535f, -1002f, 275f),
        InstanceEntryPosition: new Position(78.3534f, -226.841f, 49.7662f),
        MeetingStonePosition: new Position(-7522f, -1233f, 286f),  // Shared with BRD/LBRS
        MeetingStoneMapId: 0,
        MinLevel: 58,
        MaxLevel: 60,
        MaxPlayers: 10,
        Faction: DungeonFaction.Both,
        Wing: "Upper");

    public static readonly DungeonDefinition DireMaulEast = new(
        Name: "Dire Maul",
        Abbreviation: "DM-E",
        InstanceMapId: 429,
        EntranceMapId: 1,
        EntrancePosition: new Position(-3979f, 1127f, 161f),
        InstanceEntryPosition: new Position(47.4501f, -153.665f, -2.71439f),
        MeetingStonePosition: new Position(-3973f, 1131f, 162f),
        MeetingStoneMapId: 1,
        MinLevel: 56,
        MaxLevel: 60,
        MaxPlayers: 5,
        Faction: DungeonFaction.Both,
        Wing: "East");

    public static readonly DungeonDefinition DireMaulWest = new(
        Name: "Dire Maul",
        Abbreviation: "DM-W",
        InstanceMapId: 429,
        EntranceMapId: 1,
        EntrancePosition: new Position(-3979f, 1127f, 161f),
        InstanceEntryPosition: new Position(-203.166f, -322.997f, -2.72467f),
        MeetingStonePosition: new Position(-3973f, 1131f, 162f),
        MeetingStoneMapId: 1,
        MinLevel: 56,
        MaxLevel: 60,
        MaxPlayers: 5,
        Faction: DungeonFaction.Both,
        Wing: "West");

    public static readonly DungeonDefinition DireMaulNorth = new(
        Name: "Dire Maul",
        Abbreviation: "DM-N",
        InstanceMapId: 429,
        EntranceMapId: 1,
        EntrancePosition: new Position(-3979f, 1127f, 161f),
        InstanceEntryPosition: new Position(10.5786f, -836.991f, -32.3988f),
        MeetingStonePosition: new Position(-3973f, 1131f, 162f),
        MeetingStoneMapId: 1,
        MinLevel: 56,
        MaxLevel: 60,
        MaxPlayers: 5,
        Faction: DungeonFaction.Both,
        Wing: "North");

    public static readonly DungeonDefinition StratholmeLiving = new(
        Name: "Stratholme",
        Abbreviation: "STRAT-LIVE",
        InstanceMapId: 329,
        EntranceMapId: 0,
        EntrancePosition: new Position(3352f, -3379f, 145f),
        InstanceEntryPosition: new Position(3392.92f, -3395.03f, 143.135f),
        MeetingStonePosition: new Position(3346f, -3375f, 146f),
        MeetingStoneMapId: 0,
        MinLevel: 58,
        MaxLevel: 60,
        MaxPlayers: 5,
        Faction: DungeonFaction.Both,
        Wing: "Living");

    public static readonly DungeonDefinition StratholmeUndead = new(
        Name: "Stratholme",
        Abbreviation: "STRAT-UD",
        InstanceMapId: 329,
        EntranceMapId: 0,
        EntrancePosition: new Position(3392f, -3363f, 142f),
        InstanceEntryPosition: new Position(3392.84f, -3364.44f, 142.965f),
        MeetingStonePosition: new Position(3346f, -3375f, 146f),  // Shared with Living side
        MeetingStoneMapId: 0,
        MinLevel: 58,
        MaxLevel: 60,
        MaxPlayers: 5,
        Faction: DungeonFaction.Both,
        Wing: "Undead");

    public static readonly DungeonDefinition Scholomance = new(
        Name: "Scholomance",
        Abbreviation: "SCHOLO",
        InstanceMapId: 289,
        EntranceMapId: 0,
        EntrancePosition: new Position(1267f, -2556f, 95f),
        InstanceEntryPosition: new Position(190.819f, 126.329f, 137.227f),
        MeetingStonePosition: new Position(1262f, -2551f, 96f),
        MeetingStoneMapId: 0,
        MinLevel: 58,
        MaxLevel: 60,
        MaxPlayers: 5,
        Faction: DungeonFaction.Both);

    // =========================================================================
    // COLLECTIONS
    // =========================================================================

    /// <summary>All vanilla dungeons.</summary>
    public static readonly IReadOnlyList<DungeonDefinition> AllDungeons =
    [
        RagefireChasm, WailingCaverns, TheDeadmines, ShadowfangKeep,
        BlackfathomDeeps, TheStockade, Gnomeregan,
        RazorfenKraul,
        ScarletMonasteryGraveyard, ScarletMonasteryLibrary, ScarletMonasteryArmory, ScarletMonasteryCathedral,
        RazorfenDowns, Uldaman, ZulFarrak, Maraudon,
        SunkenTemple, BlackrockDepths, LowerBlackrockSpire, UpperBlackrockSpire,
        DireMaulEast, DireMaulWest, DireMaulNorth,
        StratholmeLiving, StratholmeUndead, Scholomance,
    ];

    /// <summary>Dungeons that have meeting stones (for summoning tests).</summary>
    public static readonly IReadOnlyList<DungeonDefinition> DungeonsWithMeetingStones =
        AllDungeons.Where(d => d.MeetingStonePosition != null).ToList();

    /// <summary>Dungeons accessible to Horde bots.</summary>
    public static readonly IReadOnlyList<DungeonDefinition> HordeDungeons =
        AllDungeons.Where(d => d.Faction != DungeonFaction.Alliance).ToList();

    /// <summary>Dungeons accessible to Alliance bots.</summary>
    public static readonly IReadOnlyList<DungeonDefinition> AllianceDungeons =
        AllDungeons.Where(d => d.Faction != DungeonFaction.Horde).ToList();

    /// <summary>Look up a dungeon by abbreviation (case-insensitive).</summary>
    public static DungeonDefinition? FindByAbbreviation(string abbreviation) =>
        AllDungeons.FirstOrDefault(d => string.Equals(d.Abbreviation, abbreviation, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>Look up dungeons by instance map ID.</summary>
    public static IReadOnlyList<DungeonDefinition> FindByMapId(uint mapId) =>
        AllDungeons.Where(d => d.InstanceMapId == mapId).ToList();
}
