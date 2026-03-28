using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Progression;

/// <summary>
/// Represents a profession trainer's location and skill range in the game world.
/// </summary>
public record ProfessionTrainerLocation(
    string ProfessionName,
    string TrainerName,
    int TrainerEntry,       // creature_template entry
    uint MapId,
    float X, float Y, float Z,
    string City,
    string Faction,         // "Horde", "Alliance", "Neutral"
    int MinSkill,           // 0=Apprentice, 75=Journeyman, 150=Expert, 225=Artisan
    int MaxSkill);          // Skill cap this trainer teaches to

/// <summary>
/// Static curated data for profession trainers across major cities.
/// Positions are approximate and can be refined from the MaNGOS DB.
/// Covers all 13 professions (10 primary + 3 secondary) at Apprentice level
/// for Horde (Orgrimmar) and Alliance (Ironforge/Stormwind).
/// </summary>
public static class ProfessionTrainerData
{
    private static readonly ProfessionTrainerLocation[] AllTrainers =
    [
        // ===== HORDE — ORGRIMMAR =====
        // Primary professions
        new("Mining", "Makaru", 3357, 1, 1696f, -4468f, 28f, "Orgrimmar", "Horde", 0, 75),
        new("Herbalism", "Jandi", 3404, 1, 1637f, -4440f, 17f, "Orgrimmar", "Horde", 0, 75),
        new("Blacksmithing", "Snarl", 3355, 1, 1847f, -4361f, 27f, "Orgrimmar", "Horde", 0, 75),
        new("Alchemy", "Yelmak", 3347, 1, 1612f, -4387f, 13f, "Orgrimmar", "Horde", 0, 75),
        new("Engineering", "Roxxik", 11017, 1, 1685f, -4544f, 27f, "Orgrimmar", "Horde", 0, 75),
        new("Leatherworking", "Karolek", 3365, 1, 1852f, -4354f, 27f, "Orgrimmar", "Horde", 0, 75),
        new("Tailoring", "Magar", 3363, 1, 1802f, -4561f, 27f, "Orgrimmar", "Horde", 0, 75),
        new("Enchanting", "Godan", 3345, 1, 1917f, -4434f, 27f, "Orgrimmar", "Horde", 0, 75),
        new("Skinning", "Thuul", 3367, 1, 1852f, -4354f, 27f, "Orgrimmar", "Horde", 0, 75),
        // Secondary professions
        new("First Aid", "Arnok", 3373, 1, 1487f, -4156f, 27f, "Orgrimmar", "Horde", 0, 75),
        new("Cooking", "Zamja", 3399, 1, 1768f, -4274f, 7f, "Orgrimmar", "Horde", 0, 75),
        new("Fishing", "Lumak", 3332, 1, 1679f, -4371f, 24f, "Orgrimmar", "Horde", 0, 75),

        // ===== HORDE — UNDERCITY =====
        new("Mining", "Brom Killian", 3555, 0, 1610f, 164f, -44f, "Undercity", "Horde", 0, 150),
        new("Herbalism", "Martha Alliestarr", 3556, 0, 1610f, 164f, -44f, "Undercity", "Horde", 0, 150),
        new("Alchemy", "Doctor Herbert Halsey", 3964, 0, 1607f, 270f, -44f, "Undercity", "Horde", 0, 150),
        new("Tailoring", "Josef Gregorian", 3557, 0, 1540f, 236f, -44f, "Undercity", "Horde", 0, 150),
        new("Enchanting", "Lavinia Crowe", 3345, 0, 1560f, 270f, -44f, "Undercity", "Horde", 0, 150),
        new("Leatherworking", "Arthur Moore", 3549, 0, 1498f, 196f, -44f, "Undercity", "Horde", 0, 150),

        // ===== HORDE — THUNDER BLUFF =====
        new("Mining", "Brek Stonehoof", 3557, 1, -2663f, -585f, -9f, "Thunder Bluff", "Horde", 0, 75),
        new("Herbalism", "Korambi", 3406, 1, -2667f, -609f, -9f, "Thunder Bluff", "Horde", 0, 75),
        new("Skinning", "Mooranta", 3549, 1, -2648f, -616f, -9f, "Thunder Bluff", "Horde", 0, 75),
        new("Leatherworking", "Una", 3549, 1, -2648f, -616f, -9f, "Thunder Bluff", "Horde", 0, 75),

        // ===== ALLIANCE — IRONFORGE =====
        // Primary professions
        new("Mining", "Geofram Bouldertoe", 5392, 0, -4852f, -1059f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Herbalism", "Reyna Stonebranch", 5503, 0, -4876f, -1245f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Blacksmithing", "Bengus Deepforge", 5511, 0, -4797f, -1114f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Alchemy", "Tally Berryfizz", 5499, 0, -4858f, -1250f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Engineering", "Springspindle Fizzlegear", 5518, 0, -4740f, -1107f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Leatherworking", "Fimble Finespindle", 5564, 0, -4821f, -1063f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Tailoring", "Jormund Stonebrow", 5546, 0, -4807f, -1064f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Enchanting", "Gimble Thistlefuzz", 5157, 0, -4920f, -1254f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Skinning", "Balthus Stoneflayer", 5564, 0, -4823f, -1060f, 502f, "Ironforge", "Alliance", 0, 75),
        // Secondary professions
        new("First Aid", "Nissa Firestone", 5150, 0, -4818f, -1230f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Cooking", "Daryl Riknussun", 5159, 0, -4845f, -1254f, 502f, "Ironforge", "Alliance", 0, 75),
        new("Fishing", "Grimnur Stonebrand", 5161, 0, -4667f, -1235f, 502f, "Ironforge", "Alliance", 0, 75),

        // ===== ALLIANCE — STORMWIND =====
        // Primary professions
        new("Mining", "Gelman Stonehand", 5513, 0, -8622f, 702f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Herbalism", "Shaina Fuller", 5566, 0, -8751f, 689f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Blacksmithing", "Therum Deepforge", 5511, 0, -8425f, 691f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Alchemy", "Lilyssia Nightbreeze", 5499, 0, -8572f, 835f, 107f, "Stormwind", "Alliance", 0, 75),
        new("Engineering", "Lilliam Sparkspindle", 5518, 0, -8379f, 687f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Leatherworking", "Simon Tanner", 5564, 0, -8459f, 726f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Tailoring", "Georgio Bolero", 5546, 0, -8842f, 685f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Enchanting", "Lucan Cordell", 5157, 0, -8854f, 803f, 99f, "Stormwind", "Alliance", 0, 75),
        new("Skinning", "Maris Granger", 5564, 0, -8461f, 728f, 97f, "Stormwind", "Alliance", 0, 75),
        // Secondary professions
        new("First Aid", "Michelle Belle", 5150, 0, -8764f, 691f, 100f, "Stormwind", "Alliance", 0, 75),
        new("Cooking", "Stephen Ryback", 5159, 0, -8552f, 1002f, 97f, "Stormwind", "Alliance", 0, 75),
        new("Fishing", "Arnold Leland", 5161, 0, -8593f, 770f, 97f, "Stormwind", "Alliance", 0, 75),

        // ===== NEUTRAL — EXPERT+ TRAINERS =====
        // Gadgetzan (Tanaris)
        new("Alchemy", "Alchemist Pestlezugg", 7948, 1, -7019f, -3743f, 9f, "Gadgetzan", "Neutral", 150, 225),
        new("Engineering", "Buzzek Bracketswing", 7944, 1, -7030f, -3780f, 9f, "Gadgetzan", "Neutral", 150, 225),
        new("First Aid", "Doctor Gregory Victor", 12920, 1, -7033f, -3785f, 9f, "Gadgetzan", "Neutral", 150, 225),

        // Booty Bay (Stranglethorn)
        new("Fishing", "Old Man Heming", 2626, 0, -14353f, 466f, 15f, "Booty Bay", "Neutral", 150, 225),
        new("Cooking", "Dirge Quikcleave", 2818, 1, -7050f, -3743f, 9f, "Gadgetzan", "Neutral", 150, 225),
    ];

    /// <summary>
    /// Gets all trainers for the specified profession name (case-insensitive).
    /// </summary>
    public static List<ProfessionTrainerLocation> GetTrainersForProfession(string professionName) =>
        AllTrainers
            .Where(t => t.ProfessionName.Equals(professionName, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Gets the nearest trainer for a profession on the given map, prioritizing the specified faction.
    /// Returns null if no trainer is available.
    /// </summary>
    public static ProfessionTrainerLocation? GetNearestTrainer(
        string professionName, uint mapId, float x, float y, float z, string faction)
    {
        var candidates = AllTrainers
            .Where(t => t.ProfessionName.Equals(professionName, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.MapId == mapId)
            .Where(t => t.Faction.Equals(faction, StringComparison.OrdinalIgnoreCase)
                     || t.Faction.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            return null;

        return candidates
            .OrderBy(t => DistanceSquared(x, y, z, t.X, t.Y, t.Z))
            .First();
    }

    /// <summary>
    /// Gets a trainer that can teach the profession at the given skill level for the specified faction.
    /// Finds the trainer whose skill range brackets the current skill (MinSkill &lt;= currentSkill &lt; MaxSkill).
    /// </summary>
    public static ProfessionTrainerLocation? GetTrainerForSkillRange(
        string professionName, int currentSkill, string faction)
    {
        return AllTrainers
            .Where(t => t.ProfessionName.Equals(professionName, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Faction.Equals(faction, StringComparison.OrdinalIgnoreCase)
                     || t.Faction.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
            .Where(t => currentSkill >= t.MinSkill && currentSkill < t.MaxSkill)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets all known profession names.
    /// </summary>
    public static IReadOnlyList<string> AllProfessionNames =>
        AllTrainers.Select(t => t.ProfessionName).Distinct().OrderBy(n => n).ToList();

    private static float DistanceSquared(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz;
    }
}
