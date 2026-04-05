using BotRunner.Tasks.Travel;
using GameData.Core.Models;

namespace BotRunner.Tests.Travel;

public class TransportScheduleServiceTests
{
    [Fact]
    public void FindRoute_MatchesByMapId()
    {
        // Find a route from Kalimdor (map 1) to Eastern Kingdoms (map 0)
        var route = TransportScheduleService.FindRoute(
            startMapId: 1,
            startPos: new Position(1320f, -4653f, 53f), // Near Orgrimmar zeppelin
            endMapId: 0);

        Assert.NotNull(route);
        Assert.Contains("Orgrimmar", route!.Name);
    }

    [Fact]
    public void GetBoardingDock_ReturnsCorrectSide()
    {
        var route = TransportScheduleService.AllRoutes[0]; // Orgrimmar <-> Undercity

        var orgDock = TransportScheduleService.GetBoardingDock(route, route.StartMapId);
        var ucDock = TransportScheduleService.GetBoardingDock(route, route.EndMapId);

        Assert.Equal(route.StartDockPosition, orgDock);
        Assert.Equal(route.EndDockPosition, ucDock);
    }

    [Fact]
    public void GetRoutesFromMap_ListsDepartures()
    {
        // Kalimdor (map 1) should have multiple departures
        var routes = TransportScheduleService.GetRoutesFromMap(1);

        Assert.True(routes.Count >= 3,
            $"Expected at least 3 routes from Kalimdor, got {routes.Count}");
    }

    [Fact]
    public void AllRoutes_Has7Entries()
    {
        Assert.Equal(7, TransportScheduleService.AllRoutes.Count);
    }

    [Fact]
    public void FindRoute_ReturnsNull_WhenNoMatch()
    {
        // Map 999 doesn't exist
        var route = TransportScheduleService.FindRoute(999, new Position(0, 0, 0), 998);

        Assert.Null(route);
    }

    [Fact]
    public void AllRoutes_HavePositiveTimings()
    {
        foreach (var route in TransportScheduleService.AllRoutes)
        {
            Assert.True(route.ApproximateTripTimeSec > 0,
                $"Route {route.Name} has invalid trip time");
            Assert.True(route.ApproximateWaitTimeSec > 0,
                $"Route {route.Name} has invalid wait time");
        }
    }
}
