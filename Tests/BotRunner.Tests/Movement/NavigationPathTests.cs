using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Models;

namespace BotRunner.Tests.Movement;

public class NavigationPathTests
{
    [Fact]
    public void GetNextWaypoint_FirstCalculation_IsNotSuppressedWhenTickIsNegative()
    {
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient((_, start, _, _) =>
        {
            pathfindingCalls++;
            return [start, new Position(30, 30, 0)];
        });

        long tick = -1000;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(100, 100, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(1, pathfindingCalls);
        Assert.Equal(30, waypoint!.X);
        Assert.Equal(30, waypoint.Y);
        Assert.Equal(0, waypoint.Z);
    }

    [Fact]
    public void GetNextWaypoint_Recalculation_RespectsCooldownAndRecalculatesAfterCooldown()
    {
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) =>
        {
            pathfindingCalls++;
            return end.X > 50
                ? [start, new Position(0, 20, 0)]
                : [start, new Position(20, 0, 0)];
        });

        long tick = 0;
        var navPath = new NavigationPath(pathfinding, () => tick);
        var currentPosition = new Position(0, 0, 0);

        var firstWaypoint = navPath.GetNextWaypoint(currentPosition, new Position(30, 0, 0), mapId: 1, allowDirectFallback: false);
        tick = 500;
        var secondWaypoint = navPath.GetNextWaypoint(currentPosition, new Position(100, 0, 0), mapId: 1, allowDirectFallback: false);
        tick = 2500;
        var thirdWaypoint = navPath.GetNextWaypoint(currentPosition, new Position(100, 0, 0), mapId: 1, allowDirectFallback: false);

        Assert.NotNull(firstWaypoint);
        Assert.NotNull(secondWaypoint);
        Assert.NotNull(thirdWaypoint);
        Assert.Equal(2, pathfindingCalls);
        Assert.Equal(20, firstWaypoint!.X);
        Assert.Equal(0, firstWaypoint.Y);
        Assert.Equal(20, secondWaypoint!.X);
        Assert.Equal(0, secondWaypoint.Y);
        Assert.Equal(0, thirdWaypoint!.X);
        Assert.Equal(20, thirdWaypoint.Y);
    }

    private sealed class DelegatePathfindingClient(
        Func<uint, Position, Position, bool, Position[]> getPath) : PathfindingClient
    {
        private readonly Func<uint, Position, Position, bool, Position[]> _getPath = getPath;

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
            => _getPath(mapId, start, end, smoothPath);
    }
}
