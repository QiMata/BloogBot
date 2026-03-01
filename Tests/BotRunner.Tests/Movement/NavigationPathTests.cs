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
        // L-shaped path with 90° corner. LOS blocked through wall at corner.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0), new Position(10, 10, 0)],
            isInLineOfSight: (_, from, to) => !(from.Y < 1f && to.Y >= 9f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Bot at (0,0) is 10y from corner → beyond 2y acceptance radius → stays at corner
        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipCornerWaypoint_BasedOnLineOfSightAlone()
    {
        // L-shaped path with 90° corner. LOS blocked through wall shortcut.
        // Even if the bot is close to the corner, it must not skip past it.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0), new Position(10, 10, 0)],
            isInLineOfSight: (_, from, to) => !(from.Y < 1f && to.Y > 1f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(7, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Corner has 90° turn → 2y acceptance radius. Bot is 3y away → stays at corner.
        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipCornerWaypoint_WhenLosCheckThrows()
    {
        // L-shaped path. LOS unavailable (throws). StringPull preserves all waypoints
        // because LOS failure means we can't skip.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0), new Position(10, 10, 0)],
            isInLineOfSight: (_, _, _) => throw new InvalidOperationException("LOS unavailable"));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // LOS throws → StringPull preserves corner. Bot 10y away from corner → stays.
        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_StrictMode_RejectsPathWhenFirstStepIsNotInLineOfSight()
    {
        // In strict mode, blocked LOS between consecutive waypoints rejects the path.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3f, 0f, 0f), new Position(8f, 0f, 0f)],
            isInLineOfSight: (_, from, _) => from.X >= 3f);

        var navPath = new NavigationPath(pathfinding, () => 0, strictPathValidation: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(8f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_StrictMode_RejectsPathWhenLaterSegmentIsNotInLineOfSight()
    {
        // In strict mode, a blocked LOS between later consecutive waypoints rejects the path.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3f, 0f, 0f), new Position(10f, 0f, 0f)],
            isInLineOfSight: (_, from, to) => MathF.Abs(to.X - from.X) <= 5f);

        var navPath = new NavigationPath(pathfinding, () => 0, strictPathValidation: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(10f, 0f, 0f),
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
    public void GetNextWaypoint_CollinearPath_StopsAtFirstReachableWaypoint_WhenProbeHeuristicsDisabled()
    {
        // Without probe heuristics, collinear waypoints are NOT pruned, string-pulled,
        // or subject to adaptive radii. The bot uses the fixed WAYPOINT_REACH_DISTANCE (3y)
        // and CORNER_COMMIT_DISTANCE (1.25y) — matching corpse-run baseline behavior.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(2.8f, 0f, 0f), new Position(4.2f, 0f, 0f)],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        // No adaptive radii: effectiveRadius=max(3,0)=3. (0,0,0) dedup consumed.
        // (2.8,0,0) at 2.8y < 3y → enters advance loop.
        // commitDistance=1.25, 2.8>1.25 → can't advance past it.
        // Bot stays at first non-dedup waypoint.
        Assert.NotNull(waypoint);
        Assert.Equal(2.8f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipShortCornerWaypoint_WhenSegmentTurns()
    {
        // 90° turn: (0,0,0) → (3.5,0,0) → (3.5,1.5,0). Block LOS through the corner
        // so StringPull preserves the corner waypoint.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(3.5f, 0f, 0f), new Position(3.5f, 1.5f, 0f)],
            isInLineOfSight: (_, from, to) => !(from.Y < 0.1f && to.Y > 0.1f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(3.5f, 10f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 4f);

        // Corner at (3.5,0,0) preserved by StringPull. 90° turn → 3y acceptance.
        // Bot at 3.5y, effectiveRadius=max(3,4)=4 → enters loop.
        // But commitDistance=3, dist=3.5 > 3 → doesn't advance past corner.
        Assert.NotNull(waypoint);
        Assert.Equal(3.5f, waypoint!.X);
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
    public void GetNextWaypoint_ProbeChainWithCorners_PreservesAllCorners_WhenProbeHeuristicsDisabled()
    {
        // Without probe heuristics, no pruning or string-pulling. L-shaped path with
        // blocked LOS at corners ensures adaptive radii keep corners tight.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(5, 0, 0),
                new Position(5, 5, 0),
                new Position(10, 5, 0)
            ],
            isInLineOfSight: (_, from, to) =>
            {
                // Block diagonal shortcuts through corners
                if (MathF.Abs(from.X - to.X) > 0.5f && MathF.Abs(from.Y - to.Y) > 0.5f)
                    return false;
                return true;
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(15, 5, 0),
            mapId: 1,
            allowDirectFallback: false);

        // First waypoint (5,0,0) has 90° turn → 2y radius. Bot is 5y away → stays.
        Assert.NotNull(waypoint);
        Assert.Equal(5f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_StrictMode_RejectsPath_WhenProbePruningExposesBlockedSegment()
    {
        // After probe pruning removes collinear intermediates, the resulting segment
        // from start to the anchor waypoint may have blocked LOS.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(1.5f, 0f, 0f),
                new Position(3.0f, 0f, 0f),
                new Position(10f, 0f, 0f)
            ],
            isInLineOfSight: (_, from, to) => !(from.X <= 0.1f && to.X >= 3.0f));

        var navPath = new NavigationPath(pathfinding, () => 0, strictPathValidation: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(10f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
    }

    [Fact]
    public void GetNextWaypoint_StallRecovery_RecalculatesInsteadOfBlindlySkippingBlockedCorner()
    {
        // Bot stuck near a 90° corner where it can't commit (dist > commitDistance).
        // After STALLED_SAMPLE_THRESHOLD iterations, stall recovery triggers recalculation.
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            {
                pathfindingCalls++;
                return [new Position(3, 0, 0), new Position(3, 8, 0)];
            },
            isInLineOfSight: (_, from, to) => !(from.Y < 0.5f && to.Y >= 7f));

        var navPath = new NavigationPath(pathfinding, () => 10_000);
        Position? waypoint = null;
        for (var i = 0; i < 30; i++)
        {
            waypoint = navPath.GetNextWaypoint(
                new Position(-0.5f, 0, 0),
                new Position(3, 12, 0),
                mapId: 1,
                allowDirectFallback: false,
                minWaypointDistance: 4f);
        }

        // Corner (3,0,0) has 90° turn → radius=3, commitDistance=3.
        // Bot at 3.5y: effectiveRadius=max(3,4)=4, 3.5<4 → enters loop,
        // but commitDistance=3, 3.5>3 → can't advance → stalled.
        // After STALLED_SAMPLE_THRESHOLD (6) iterations, recalculation triggers.
        Assert.NotNull(waypoint);
        Assert.Equal(3f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
        Assert.True(pathfindingCalls >= 2);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipFirstWaypoint_WhenPathDoesNotIncludeCurrentPosition()
    {
        // Path starts at a waypoint that's not the current position.
        // LOS blocked through corner to prevent StringPull from collapsing the path.
        var pathfinding = new DelegatePathfindingClient(
            (_, _, _, _) => [new Position(4, 0, 0), new Position(4, 10, 0)],
            isInLineOfSight: (_, from, to) => !(from.Y < 0.5f && to.Y > 0.5f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(4, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Corner at (4,0,0) with 90° turn → 2y radius. Bot 4y away → stays at first waypoint.
        Assert.NotNull(waypoint);
        Assert.Equal(4f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void CalculatePath_PrefersSmoothPath_WhenProbeHeuristicsEnabled()
    {
        var smoothCalls = new List<bool>();
        var pathfinding = new DelegatePathfindingClient((_, _, _, smoothPath) =>
        {
            smoothCalls.Add(smoothPath);
            // Smooth path returns empty (rejected), non-smooth returns valid
            return smoothPath ? [] : [new Position(7, 0, 0)];
        });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(7f, waypoint!.X);
        // Smooth is tried first, then non-smooth as fallback
        Assert.Equal([true, false], smoothCalls);
    }

    [Fact]
    public void CalculatePath_PrefersNonSmoothPath_WhenProbeHeuristicsDisabled()
    {
        var smoothCalls = new List<bool>();
        var pathfinding = new DelegatePathfindingClient((_, _, _, smoothPath) =>
        {
            smoothCalls.Add(smoothPath);
            // Non-smooth returns empty (rejected), smooth returns valid
            return smoothPath ? [new Position(7, 0, 0)] : [];
        });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(7f, waypoint!.X);
        // Non-smooth is tried first (corpse-run mode), then smooth as fallback
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

    // --- New tests for adaptive acceptance radius and string-pulling ---

    [Fact]
    public void ComputeTurnAngle2D_StraightLine_ReturnsZero()
    {
        var angle = NavigationPath.ComputeTurnAngle2D(
            new Position(0, 0, 0),
            new Position(5, 0, 0),
            new Position(10, 0, 0));

        Assert.Equal(0f, angle, precision: 1);
    }

    [Fact]
    public void ComputeTurnAngle2D_RightAngle_Returns90()
    {
        var angle = NavigationPath.ComputeTurnAngle2D(
            new Position(0, 0, 0),
            new Position(5, 0, 0),
            new Position(5, 5, 0));

        Assert.Equal(90f, angle, precision: 1);
    }

    [Fact]
    public void ComputeTurnAngle2D_Reversal_Returns180()
    {
        var angle = NavigationPath.ComputeTurnAngle2D(
            new Position(0, 0, 0),
            new Position(5, 0, 0),
            new Position(0, 0, 0));

        Assert.Equal(180f, angle, precision: 1);
    }

    [Fact]
    public void AdaptiveRadius_StraightPathGetsLargeRadius_CornerGetsSmallRadius()
    {
        // Path: straight segment → 90° corner → straight segment.
        // LOS blocks both diagonal shortcuts and long horizontal jumps
        // so StringPull preserves the intermediate waypoints.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(10, 0, 0),
                new Position(20, 0, 0),
                new Position(20, 10, 0)
            ],
            isInLineOfSight: (_, from, to) =>
            {
                // Block diagonal through corner
                if (from.Y < 1f && to.Y > 1f) return false;
                // Block long horizontal jumps (> 12y) to prevent StringPull from collapsing
                if (MathF.Abs(to.X - from.X) > 12f && MathF.Abs(to.Y - from.Y) < 1f) return false;
                return true;
            });

        var navPath = new NavigationPath(pathfinding, () => 0);

        // Bot at origin — 10y from first waypoint, won't advance through any
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);

        // Move bot within 6y of first waypoint (straight line → large radius)
        waypoint = navPath.GetNextWaypoint(
            new Position(5, 0, 0),
            new Position(20, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Bot at 5y from (10,0,0). Straight-line radius is ~6y. Should advance to corner (20,0,0).
        Assert.NotNull(waypoint);
        Assert.Equal(20f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void StringPull_PreservesCorner_WhenLosBlockedThroughWall()
    {
        // L-shaped path: (0,0) → (10,0) → (10,10). Wall blocks diagonal LOS.
        // StringPull should preserve the corner because LOS from start to (10,10) is blocked.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(10, 0, 0),
                new Position(10, 10, 0)
            ],
            isInLineOfSight: (_, from, to) => !(from.Y < 1f && to.Y > 1f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Corner (10,0,0) preserved by StringPull. Bot is 10y away → stays at corner.
        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void StringPull_RemovesIntermediate_WhenLosIsClear()
    {
        // Three waypoints in a straight line with clear LOS. StringPull should
        // collapse to just the final waypoint.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(5, 0, 0),
                new Position(10, 0, 0),
                new Position(15, 0, 0)
            ],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 0, 0),
            mapId: 1,
            allowDirectFallback: false);

        // StringPull sees clear LOS from (0,0,0) to (15,0,0), collapses to [(15,0,0)].
        // Destination radius = 2y. Bot 15y away → stays at (15,0,0).
        Assert.NotNull(waypoint);
        Assert.Equal(15f, waypoint!.X);
    }

    // ===================== Cliff/edge detection =====================

    [Fact]
    public void ProbeEdgeAhead_ReturnsZero_WhenGroundIsLevel()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)],
            getGroundZ: (_, _, _) => (10f, true));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var drop = navPath.ProbeEdgeAhead(new Position(0, 0, 10), new Position(10, 0, 10), mapId: 1);
        Assert.Equal(0f, drop);
    }

    [Fact]
    public void ProbeEdgeAhead_ReturnsDropDistance_WhenCliffDetected()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)],
            getGroundZ: (_, _, _) => (-10f, true));  // ground 20y below

        var navPath = new NavigationPath(pathfinding, () => 0);
        var drop = navPath.ProbeEdgeAhead(new Position(0, 0, 10), new Position(10, 0, 10), mapId: 1);
        Assert.Equal(20f, drop);
    }

    [Fact]
    public void ProbeEdgeAhead_ReturnsMaxValue_WhenNoGroundFound()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)],
            getGroundZ: (_, _, _) => (float.NaN, false));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var drop = navPath.ProbeEdgeAhead(new Position(0, 0, 10), new Position(10, 0, 10), mapId: 1);
        Assert.Equal(float.MaxValue, drop);
    }

    [Fact]
    public void IsCliffAhead_ReturnsTrue_WhenDropExceedsThreshold()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)],
            getGroundZ: (_, _, _) => (0f, true));  // ground 10y below current Z=10

        var navPath = new NavigationPath(pathfinding, () => 0);
        Assert.True(navPath.IsCliffAhead(new Position(0, 0, 10), new Position(10, 0, 10), mapId: 1));
    }

    [Fact]
    public void IsCliffAhead_ReturnsFalse_WhenDropIsSafe()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)],
            getGroundZ: (_, _, _) => (8f, true));  // ground 2y below current Z=10

        var navPath = new NavigationPath(pathfinding, () => 0);
        Assert.False(navPath.IsCliffAhead(new Position(0, 0, 10), new Position(10, 0, 10), mapId: 1));
    }

    [Fact]
    public void IsLethalCliffAhead_ReturnsTrue_WhenDropIs50Plus()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)],
            getGroundZ: (_, _, _) => (-50f, true));

        var navPath = new NavigationPath(pathfinding, () => 0);
        Assert.True(navPath.IsLethalCliffAhead(new Position(0, 0, 10), new Position(10, 0, 10), mapId: 1));
    }

    // ===================== Fall damage estimation =====================

    [Fact]
    public void EstimateFallDamage_ReturnsZero_BelowThreshold()
    {
        Assert.Equal(0f, NavigationPath.EstimateFallDamage(14f, 1000f));
    }

    [Fact]
    public void EstimateFallDamage_ReturnsPositive_AboveThreshold()
    {
        // 24.57y fall → (24.57 - 14.57) / 100 = 10% = 100 damage on 1000 HP
        var damage = NavigationPath.EstimateFallDamage(24.57f, 1000f);
        Assert.True(damage > 0f);
        Assert.InRange(damage, 90f, 110f);
    }

    [Fact]
    public void EstimateFallDamage_SafeFall_ReducesDistance()
    {
        // 30y fall without safe fall → (30 - 14.57) / 100 = 15.43% = ~154 damage
        // 30y fall with safe fall → effective 15y, (15 - 14.57) / 100 = 0.43% = ~4 damage
        var withoutSafeFall = NavigationPath.EstimateFallDamage(30f, 1000f, hasSafeFall: false);
        var withSafeFall = NavigationPath.EstimateFallDamage(30f, 1000f, hasSafeFall: true);
        Assert.True(withSafeFall < withoutSafeFall);
    }

    [Fact]
    public void AssessJumpDamage_ReturnsZero_WhenJumpingUp()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0)]);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var damage = navPath.AssessJumpDamage(new Position(0, 0, 5), new Position(0, 0, 10), 1000f, false);
        Assert.Equal(0f, damage);
    }

    // ===================== Gap detection =====================

    [Fact]
    public void DetectGaps_FindsGap_WhenMidpointGroundDrops()
    {
        // Two waypoints on platforms with a deep gap between them
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(0, 0, 20),   // platform 1
                new Position(6, 0, 20)    // platform 2
            ],
            getGroundZ: (_, pos, _) =>
            {
                // Midpoint probe at X=3: ground at Z=5 (15y below platforms)
                return pos.X > 1 && pos.X < 5 ? (5f, true) : (20f, true);
            });

        // Disable probe heuristics so StringPull doesn't collapse waypoints
        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        navPath.GetNextWaypoint(new Position(-5, 0, 20), new Position(10, 0, 20), mapId: 1);

        var gaps = navPath.DetectGaps(mapId: 1);
        Assert.Single(gaps);
        Assert.True(gaps[0].IsJumpable);  // 6yd gap < 8yd max, level landing
        Assert.Equal(6f, gaps[0].GapWidth2D, 0.1f);
    }

    [Fact]
    public void DetectGaps_MarksUnjumpable_WhenTooWide()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(0, 0, 20),
                new Position(12, 0, 20)   // 12yd apart — too far to jump
            ],
            getGroundZ: (_, _, _) => (5f, true));

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        navPath.GetNextWaypoint(new Position(-5, 0, 20), new Position(15, 0, 20), mapId: 1);

        var gaps = navPath.DetectGaps(mapId: 1);
        Assert.Single(gaps);
        Assert.False(gaps[0].IsJumpable);
    }

    [Fact]
    public void DetectGaps_ReturnsEmpty_WhenNoGap()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(0, 0, 10),
                new Position(5, 0, 10)
            ],
            getGroundZ: (_, _, _) => (10f, true));  // flat ground, no gap

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        navPath.GetNextWaypoint(new Position(-5, 0, 10), new Position(10, 0, 10), mapId: 1);

        var gaps = navPath.DetectGaps(mapId: 1);
        Assert.Empty(gaps);
    }

    private sealed class DelegatePathfindingClient(
        Func<uint, Position, Position, bool, Position[]> getPath,
        Func<uint, Position, Position, bool>? isInLineOfSight = null,
        Func<uint, Position, float, (float, bool)>? getGroundZ = null) : PathfindingClient
    {
        private readonly Func<uint, Position, Position, bool, Position[]> _getPath = getPath;
        private readonly Func<uint, Position, Position, bool> _isInLineOfSight =
            isInLineOfSight ?? ((_, _, _) => true);
        private readonly Func<uint, Position, float, (float, bool)>? _getGroundZ = getGroundZ;

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
            => _getPath(mapId, start, end, smoothPath);

        public override bool IsInLineOfSight(uint mapId, Position from, Position to)
            => _isInLineOfSight(mapId, from, to);

        public override (float groundZ, bool found) GetGroundZ(uint mapId, Position position, float maxSearchDist = 10.0f)
            => _getGroundZ?.Invoke(mapId, position, maxSearchDist) ?? (position.Z, true);
    }
}
