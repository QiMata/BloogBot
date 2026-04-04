using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Progression;

/// <summary>
/// Defines leveling zone routes for Horde and Alliance.
/// Each zone has a level range and a rally position.
/// When a bot reaches the max level for a zone, it travels to the next zone.
/// </summary>
public static class ZoneLevelingRoute
{
    public record ZoneDefinition(string Name, int MinLevel, int MaxLevel, uint MapId, Position RallyPosition);

    public static readonly List<ZoneDefinition> HordeRoute =
    [
        new("Durotar", 1, 10, 1, new(340f, -4684f, 17f)),
        new("The Barrens", 10, 20, 1, new(-434f, -2596f, 96f)),
        new("Stonetalon Mountains", 20, 30, 1, new(930f, 920f, 105f)),
        new("Thousand Needles", 25, 35, 1, new(-5371f, -2536f, -40f)),
        new("Desolace", 30, 40, 1, new(-429f, 1556f, 91f)),
        new("Dustwallow Marsh", 35, 45, 1, new(-3354f, -2977f, 34f)),
        new("Tanaris", 40, 50, 1, new(-7171f, -3785f, 8f)),
        new("Felwood", 48, 55, 1, new(3987f, -1292f, 298f)),
        new("Un'Goro Crater", 50, 55, 1, new(-6164f, -1100f, -208f)),
        new("Winterspring", 55, 60, 1, new(6659f, -4553f, 718f)),
        new("Western Plaguelands", 51, 58, 0, new(1756f, -1644f, 59f)),
        new("Eastern Plaguelands", 55, 60, 0, new(2277f, -5320f, 82f)),
    ];

    public static readonly List<ZoneDefinition> AllianceRoute =
    [
        new("Elwynn Forest", 1, 10, 0, new(-9461f, 60f, 56f)),
        new("Westfall", 10, 20, 0, new(-10646f, 1165f, 34f)),
        new("Redridge Mountains", 15, 25, 0, new(-9448f, -2174f, 64f)),
        new("Duskwood", 20, 30, 0, new(-10539f, -1155f, 28f)),
        new("Wetlands", 25, 35, 0, new(-3762f, -759f, 10f)),
        new("Stranglethorn Vale", 30, 45, 0, new(-11340f, -229f, 76f)),
        new("Tanaris", 40, 50, 1, new(-7171f, -3785f, 8f)),
        new("The Hinterlands", 45, 50, 0, new(291f, -2037f, 123f)),
        new("Felwood", 48, 55, 1, new(3987f, -1292f, 298f)),
        new("Un'Goro Crater", 50, 55, 1, new(-6164f, -1100f, -208f)),
        new("Winterspring", 55, 60, 1, new(6659f, -4553f, 718f)),
        new("Western Plaguelands", 51, 58, 0, new(1756f, -1644f, 59f)),
        new("Eastern Plaguelands", 55, 60, 0, new(2277f, -5320f, 82f)),
    ];

    /// <summary>
    /// Get the appropriate zone for a given level and faction.
    /// Returns the first zone where the level is within the min-max range.
    /// </summary>
    public static ZoneDefinition? GetZoneForLevel(int level, bool isHorde)
    {
        var route = isHorde ? HordeRoute : AllianceRoute;
        return route.FirstOrDefault(z => level >= z.MinLevel && level <= z.MaxLevel);
    }

    /// <summary>
    /// Get the next zone in the route after the current zone.
    /// </summary>
    public static ZoneDefinition? GetNextZone(string currentZoneName, bool isHorde)
    {
        var route = isHorde ? HordeRoute : AllianceRoute;
        var currentIndex = route.FindIndex(z => z.Name == currentZoneName);
        if (currentIndex < 0 || currentIndex >= route.Count - 1)
            return null;
        return route[currentIndex + 1];
    }
}
