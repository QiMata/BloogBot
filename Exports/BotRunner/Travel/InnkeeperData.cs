using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

/// <summary>
/// Static database of innkeeper NPC positions for major cities and quest hubs.
/// Used by TravelTask to determine when to set a new hearthstone bind point
/// (e.g., when assigned to a new grind zone near a different innkeeper).
/// </summary>
public static class InnkeeperData
{
    public enum InnFaction { Horde, Alliance, Neutral }

    public record InnkeeperLocation(
        string Name,
        string LocationName,
        uint MapId,
        Position Position,
        InnFaction Faction);

    public static readonly IReadOnlyList<InnkeeperLocation> AllInnkeepers = new List<InnkeeperLocation>
    {
        // Horde capital cities
        new("Innkeeper Gryshka", "Orgrimmar", 1, new Position(1642f, -4440f, 16f), InnFaction.Horde),
        new("Innkeeper Norman", "Undercity", 0, new Position(1639f, 224f, -43f), InnFaction.Horde),
        new("Innkeeper Pala", "Thunder Bluff", 1, new Position(-1167f, -29f, 159f), InnFaction.Horde),

        // Alliance capital cities
        new("Innkeeper Allison", "Stormwind", 0, new Position(-8867f, 675f, 97f), InnFaction.Alliance),
        new("Innkeeper Firebrew", "Ironforge", 0, new Position(-4854f, -1245f, 502f), InnFaction.Alliance),
        new("Innkeeper Saelienne", "Darnassus", 1, new Position(10129f, 2225f, 1325f), InnFaction.Alliance),

        // Horde quest hub inns
        new("Innkeeper Grosk", "Razor Hill", 1, new Position(338f, -4688f, 16f), InnFaction.Horde),
        new("Innkeeper Kauth", "Crossroads", 1, new Position(-440f, -2596f, 96f), InnFaction.Horde),
        new("Innkeeper Karakul", "Camp Taurajo", 1, new Position(-2365f, -1990f, 96f), InnFaction.Horde),
        new("Innkeeper Shul'kar", "Tarren Mill", 0, new Position(-20f, -864f, 55f), InnFaction.Horde),
        new("Innkeeper Adegwa", "Brill", 0, new Position(2268f, 249f, 34f), InnFaction.Horde),
        new("Innkeeper Renee", "Sepulcher", 0, new Position(507f, 1540f, 126f), InnFaction.Horde),
        new("Innkeeper Sikewa", "Freewind Post", 1, new Position(-5408f, -2414f, 90f), InnFaction.Horde),
        new("Innkeeper Byula", "Kargath", 0, new Position(-6659f, -3681f, 265f), InnFaction.Horde),

        // Alliance quest hub inns
        new("Innkeeper Farley", "Goldshire", 0, new Position(-9457f, 45f, 57f), InnFaction.Alliance),
        new("Innkeeper Belm", "Lakeshire", 0, new Position(-9270f, -2216f, 64f), InnFaction.Alliance),
        new("Innkeeper Hearthstove", "Loch Modan", 0, new Position(-5393f, -2953f, 322f), InnFaction.Alliance),
        new("Innkeeper Anderson", "Southshore", 0, new Position(-848f, -564f, 17f), InnFaction.Alliance),
        new("Innkeeper Abeqwa", "Theramore", 1, new Position(-3727f, -4522f, 15f), InnFaction.Alliance),
        new("Innkeeper Shaussiy", "Astranaar", 1, new Position(2825f, -367f, 108f), InnFaction.Alliance),

        // Neutral inns
        new("Innkeeper Wiley", "Ratchet", 1, new Position(-976f, -3789f, 5f), InnFaction.Neutral),
        new("Innkeeper Skindle", "Gadgetzan", 1, new Position(-7160f, -3836f, 9f), InnFaction.Neutral),
        new("Innkeeper Vizzie", "Booty Bay", 0, new Position(-14370f, 430f, 23f), InnFaction.Neutral),
        new("Innkeeper Fizzgrimble", "Everlook", 1, new Position(6723f, -4638f, 721f), InnFaction.Neutral),
        new("Calandrath", "Cenarion Hold", 1, new Position(-6806f, 829f, 50f), InnFaction.Neutral),
        new("Jessica Chambers", "Light's Hope Chapel", 0, new Position(2290f, -5316f, 82f), InnFaction.Neutral),
    };

    /// <summary>
    /// Find the nearest innkeeper on the same map for the given faction.
    /// </summary>
    public static InnkeeperLocation? FindNearest(uint mapId, Position position, InnFaction faction)
        => AllInnkeepers
            .Where(i => i.MapId == mapId && (i.Faction == faction || i.Faction == InnFaction.Neutral))
            .OrderBy(i => i.Position.DistanceTo(position))
            .FirstOrDefault();

    /// <summary>
    /// Get all innkeepers accessible by a faction.
    /// </summary>
    public static IReadOnlyList<InnkeeperLocation> GetByFaction(InnFaction faction)
        => AllInnkeepers.Where(i => i.Faction == faction || i.Faction == InnFaction.Neutral).ToList();
}
