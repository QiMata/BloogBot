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
                ? [start, new Position(80, 0, 0)]
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
        Assert.Equal(80, thirdWaypoint!.X);
        Assert.Equal(0, thirdWaypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipCornerWaypoint_WhenNextSegmentNotInLineOfSight()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3, 0, 0), new Position(6, 0, 0)],
            isInLineOfSight: (_, from, to) => !(from.X < 2f && to.X >= 6f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(1.6f, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(3f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipCornerWaypoint_BasedOnLineOfSightAlone()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(2.6f, 0, 0), new Position(8f, 0, 0)],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(2.6f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipCornerWaypoint_WhenLosCheckThrows()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3, 0, 0), new Position(6, 0, 0)],
            isInLineOfSight: (_, _, _) => throw new InvalidOperationException("LOS unavailable"));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(1.6f, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(3f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPathWhenFirstStepIsNotInLineOfSight()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3f, 0f, 0f), new Position(8f, 0f, 0f)],
            isInLineOfSight: (_, _, to) => to.X > 3.1f);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPathWhenLaterSegmentIsNotInLineOfSight()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3f, 0f, 0f), new Position(6f, 0f, 0f), new Position(9f, 0f, 0f)],
            isInLineOfSight: (_, from, to) => !(from.X >= 5f && to.X >= 8f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_SkipsShortCollinearProbeWaypoint_WhenNextSegmentIsVisible()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(2.8f, 0f, 0f), new Position(4.2f, 0f, 0f)],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(4.2f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipShortCollinearProbeWaypoint_WhenProbeHeuristicsDisabled()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(2.8f, 0f, 0f), new Position(4.2f, 0f, 0f)],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(2.8f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipShortCornerWaypoint_WhenSegmentTurns()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(2.8f, 0f, 0f), new Position(2.8f, 1.5f, 0f)],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(2.8f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_PrunesCollinearProbeChain_ToStableAnchorWaypoint()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(2.8f, 0f, 0f),
                new Position(4.0f, 0f, 0f),
                new Position(5.2f, 0f, 0f)
            ],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(5.2f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotPruneProbeChain_WhenProbeHeuristicsDisabled()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(2.8f, 0f, 0f),
                new Position(4.0f, 0f, 0f),
                new Position(5.2f, 0f, 0f)
            ],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 3f);

        Assert.NotNull(waypoint);
        Assert.Equal(2.8f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPath_WhenProbePruningExposesBlockedAnchorSegment()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(1.5f, 0f, 0f),
                new Position(3.0f, 0f, 0f),
                new Position(10f, 0f, 0f)
            ],
            isInLineOfSight: (_, from, to) => !(from.X <= 0.1f && to.X >= 3.0f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_StallRecovery_RecalculatesInsteadOfBlindlySkippingBlockedCorner()
    {
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            {
                pathfindingCalls++;
                return [start, new Position(2, 0, 0), new Position(6, 0, 0)];
            },
            isInLineOfSight: (_, from, to) => !(from.X < 1f && to.X >= 6f));

        var navPath = new NavigationPath(pathfinding, () => 10_000);
        Position? waypoint = null;
        for (var i = 0; i < 30; i++)
        {
            waypoint = navPath.GetNextWaypoint(
                new Position(0.6f, 0, 0),
                new Position(20, 0, 0),
                mapId: 1,
                allowDirectFallback: false,
                minWaypointDistance: 3f);
        }

        Assert.NotNull(waypoint);
        Assert.Equal(2f, waypoint!.X);
        Assert.True(pathfindingCalls >= 2);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipFirstWaypoint_WhenPathDoesNotIncludeCurrentPosition()
    {
        var pathfinding = new DelegatePathfindingClient((_, _, _, _) =>
            [new Position(4, 0, 0), new Position(10, 0, 0)]);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(4f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void CalculatePath_UsesFallbackSmoothingOnlyWhenPrimaryPathIsEmpty()
    {
        var smoothCalls = new List<bool>();
        var pathfinding = new DelegatePathfindingClient((_, _, _, smoothPath) =>
        {
            smoothCalls.Add(smoothPath);
            return smoothPath ? [new Position(7, 0, 0)] : [];
        });

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(7f, waypoint!.X);
        Assert.Equal([false, true], smoothCalls);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPathWithNonFiniteCoordinates()
    {
        var pathfinding = new DelegatePathfindingClient((_, start, _, _) =>
            [start, new Position(float.NaN, 5f, 0f)]);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPathWhenAnyWaypointContainsNonFiniteCoordinates()
    {
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) =>
            [start, new Position(float.NaN, 5f, 0f), end]);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPathWithoutMeaningfulDestinationProgress()
    {
        var pathfinding = new DelegatePathfindingClient((_, start, _, _) =>
            [start, new Position(0.2f, 0f, 0f)]);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(40, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_RejectsPathWithFirstWaypointTooFarFromStart()
    {
        var pathfinding = new DelegatePathfindingClient((_, _, end, _) =>
            [new Position(600, 600, 0), end]);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(610, 610, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_StrictValidation_RejectsPathWithoutDestinationClosure()
    {
        var pathfinding = new DelegatePathfindingClient((_, start, _, _) =>
            [start, new Position(30f, 0f, 0f)]);

        var navPath = new NavigationPath(pathfinding, () => 0, strictPathValidation: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(100f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_StrictValidation_RejectsPathWhenLosProbeUnavailable()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(6f, 0f, 0f), new Position(20f, 0f, 0f)],
            isInLineOfSight: (_, _, _) => throw new InvalidOperationException("LOS unavailable"));

        var navPath = new NavigationPath(pathfinding, () => 0, strictPathValidation: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_StrictValidation_StallRecovery_RecalculatesInsteadOfSkippingWaypoint()
    {
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            {
                pathfindingCalls++;
                return [start, new Position(2f, 0f, 0f), new Position(6f, 0f, 0f), new Position(20f, 0f, 0f)];
            },
            isInLineOfSight: (_, from, to) => !(from.X < 1f && to.X >= 6f));

        var navPath = new NavigationPath(pathfinding, () => 10_000, strictPathValidation: true);
        Position? waypoint = null;
        for (var i = 0; i < 30; i++)
        {
            waypoint = navPath.GetNextWaypoint(
                new Position(0.9f, 0f, 0f),
                new Position(20f, 0f, 0f),
                mapId: 1,
                allowDirectFallback: false,
                minWaypointDistance: 3f);
        }

        Assert.NotNull(waypoint);
        Assert.Equal(2f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
        Assert.True(pathfindingCalls >= 2);
    }

    private sealed class DelegatePathfindingClient(
        Func<uint, Position, Position, bool, Position[]> getPath,
        Func<uint, Position, Position, bool>? isInLineOfSight = null) : PathfindingClient
    {
        private readonly Func<uint, Position, Position, bool, Position[]> _getPath = getPath;
        private readonly Func<uint, Position, Position, bool> _isInLineOfSight =
            isInLineOfSight ?? ((_, _, _) => true);

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
            => _getPath(mapId, start, end, smoothPath);

        public override bool IsInLineOfSight(uint mapId, Position from, Position to)
            => _isInLineOfSight(mapId, from, to);
    }
}
