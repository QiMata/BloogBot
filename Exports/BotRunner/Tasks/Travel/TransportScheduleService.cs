using GameData.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Defines departure schedules and dock positions for boats and zeppelins.
/// Bot navigates to dock/platform 30s before departure, boards transport.
/// Uses existing TransportWaitingLogic and TransportData for coordinate transforms.
/// </summary>
public static class TransportScheduleService
{
    public record TransportRoute(
        string Name,
        string StartLocation,
        Position StartDockPosition,
        uint StartMapId,
        string EndLocation,
        Position EndDockPosition,
        uint EndMapId,
        float ApproximateTripTimeSec,
        float ApproximateWaitTimeSec);

    /// <summary>
    /// All vanilla boat and zeppelin routes with dock positions.
    /// Wait times are approximate — transports run on fixed server cycles.
    /// </summary>
    public static readonly List<TransportRoute> AllRoutes =
    [
        // ═══ Zeppelins ═══
        new("Orgrimmar ↔ Undercity",
            "Orgrimmar", new(1320f, -4653f, 53f), 1,
            "Tirisfal Glades", new(2066f, 288f, 97f), 0,
            300f, 120f),

        new("Orgrimmar ↔ Grom'gol",
            "Orgrimmar", new(1178f, -4654f, 23f), 1,
            "Stranglethorn Vale", new(-12413f, 208f, 32f), 0,
            300f, 120f),

        new("Undercity ↔ Grom'gol",
            "Tirisfal Glades", new(2066f, 244f, 97f), 0,
            "Stranglethorn Vale", new(-12413f, 172f, 32f), 0,
            300f, 120f),

        // ═══ Boats ═══
        new("Ratchet ↔ Booty Bay",
            "Ratchet", new(-996f, -3827f, 6f), 1,
            "Booty Bay", new(-14280f, 556f, 9f), 0,
            240f, 120f),

        new("Menethil Harbor ↔ Theramore",
            "Menethil Harbor", new(-3712f, -585f, 5f), 0,
            "Theramore", new(-3811f, -4472f, 10f), 1,
            300f, 120f),

        new("Menethil Harbor ↔ Auberdine",
            "Menethil Harbor", new(-3741f, -595f, 5f), 0,
            "Auberdine", new(6427f, 832f, 6f), 1,
            300f, 120f),

        new("Auberdine ↔ Darnassus (Rut'theran)",
            "Auberdine", new(6418f, 818f, 6f), 1,
            "Rut'theran Village", new(8641f, 1331f, 9f), 1,
            120f, 60f),
    ];

    /// <summary>
    /// Find a transport route between two locations.
    /// Matches by start/end map IDs and proximity.
    /// </summary>
    public static TransportRoute? FindRoute(uint startMapId, Position startPos, uint endMapId)
    {
        return AllRoutes
            .Where(r =>
                (r.StartMapId == startMapId && r.EndMapId == endMapId) ||
                (r.EndMapId == startMapId && r.StartMapId == endMapId))
            .OrderBy(r =>
            {
                var startDist = r.StartMapId == startMapId
                    ? r.StartDockPosition.DistanceTo(startPos)
                    : r.EndDockPosition.DistanceTo(startPos);
                return startDist;
            })
            .FirstOrDefault();
    }

    /// <summary>
    /// Get the dock position to navigate to for boarding.
    /// Returns the dock on the side matching the start map.
    /// </summary>
    public static Position GetBoardingDock(TransportRoute route, uint currentMapId)
    {
        return route.StartMapId == currentMapId
            ? route.StartDockPosition
            : route.EndDockPosition;
    }

    /// <summary>
    /// Get all routes departing from a specific map.
    /// </summary>
    public static IReadOnlyList<TransportRoute> GetRoutesFromMap(uint mapId)
    {
        return AllRoutes
            .Where(r => r.StartMapId == mapId || r.EndMapId == mapId)
            .ToList();
    }
}
