using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using System.Collections.Generic;

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
    public void CurrentWaypoints_ReturnsRemainingCorridorAfterWaypointAdvance()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(2f, 0f, 0f),
                new Position(8f, 0f, 0f),
                new Position(14f, 0f, 0f)
            ],
            isInLineOfSight: (_, from, to) => MathF.Abs(to.X - from.X) <= 6.5f);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(1.9f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(8f, waypoint!.X);

        var remaining = navPath.CurrentWaypoints;
        Assert.Equal(2, remaining.Length);
        Assert.Equal(8f, remaining[0].X);
        Assert.Equal(14f, remaining[1].X);
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
        // Corner waypoint offset by capsule-radius * 3 along bisector (~1.27y per axis for 90° turn)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 8f, 11f);
        Assert.InRange(waypoint.Y, -2f, 1f);
    }

    [Fact]
    public void GetNextWaypoint_AdvancesPastCorner_WhenWithinAcceptanceRadiusAfterOffset()
    {
        // L-shaped path with 90° corner. LOS blocked through wall shortcut.
        // Bot at (5,0) is ~4y from offset corner (~8.73,-1.27) which is within the
        // 90° speed-scaled acceptance radius (~4.2y), so the bot advances to the next waypoint.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 0), new Position(10, 10, 0)],
            isInLineOfSight: (_, from, to) => !(from.Y < 1f && to.Y > 1f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(5, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Bot within acceptance radius of offset corner → advances to second waypoint (10, 10)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 9f, 11f);
        Assert.InRange(waypoint.Y, 9f, 11f);
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
        // Corner waypoint offset by capsule-radius * 3 along bisector (~1.27y per axis for 90° turn)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 8f, 11f);
        Assert.InRange(waypoint.Y, -2f, 1f);
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
    public void GetNextWaypoint_CollinearPath_AdvancesPastCloseWaypoints_WhenProbeHeuristicsDisabled()
    {
        // Without probe heuristics, collinear waypoints are NOT pruned, string-pulled,
        // or subject to adaptive radii. The bot uses the fixed WAYPOINT_REACH_DISTANCE (3.5y).
        // In non-strict mode (default), CanAdvanceToNextWaypoint always returns true,
        // so collinear waypoints within effectiveRadius are all consumed in one pass.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(2.8f, 0f, 0f), new Position(4.2f, 0f, 0f)],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        // No adaptive radii: effectiveRadius=max(3.5,0)=3.5. (0,0,0) dedup consumed.
        // (2.8,0,0) at 2.8y < 3.5y → enters advance loop → CanAdvance=true (non-strict).
        // (4.2,0,0) at 4.2y > 3.5y → exits advance loop.
        // Bot targets (4.2,0,0).
        Assert.NotNull(waypoint);
        Assert.Equal(4.2f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(0.5f, 0f, 0f),
                new Position(4f, 0f, 0f)
            ],
            isPointOnNavmesh: (_, position, _) =>
            {
                if (position.X > 1f && position.X < 3f)
                    return (false, position);

                return (true, position);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(4f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotSkipShortCornerWaypoint_WhenSegmentTurns()
    {
        // 90° turn: (0,0,0) → (8,0,0) → (8,6,0). Block LOS through the corner
        // so StringPull preserves the corner waypoint.
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) => [start, new Position(8f, 0f, 0f), new Position(8f, 6f, 0f)],
            isInLineOfSight: (_, from, to) => !(from.Y < 0.1f && to.Y > 0.1f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(8f, 20f, 0f),
            mapId: 1,
            allowDirectFallback: false,
            minWaypointDistance: 4f);

        // Corner at (8,0,0) preserved by StringPull. 90° turn → speed-scaled acceptance (~4.2y).
        // Bot at 8y, effectiveRadius=max(4.2,4)=4.2 → 8 > 4.2 → doesn't enter advance loop.
        // Corner waypoint offset by capsule-radius * 3 along bisector (~1.27y per axis for 90° turn)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 6f, 9f);
        Assert.InRange(waypoint.Y, -2f, 1f);
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
        // Bot stuck near a 90° corner where it can't advance (dist > effectiveRadius).
        // After STALLED_SAMPLE_THRESHOLD iterations, stall recovery triggers recalculation.
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            {
                pathfindingCalls++;
                return [new Position(10, 0, 0), new Position(10, 10, 0)];
            },
            isInLineOfSight: (_, from, to) => !(from.Y < 0.5f && to.Y >= 7f));

        var navPath = new NavigationPath(pathfinding, () => 10_000);
        Position? waypoint = null;
        for (var i = 0; i < 30; i++)
        {
            waypoint = navPath.GetNextWaypoint(
                new Position(4, 0, 0),
                new Position(10, 20, 0),
                mapId: 1,
                allowDirectFallback: false,
                minWaypointDistance: 4f);
        }

        // Corner (10,0,0) has 90° turn → speed-scaled acceptance (~4.2y).
        // Bot at ~6y from offset corner: outside effectiveRadius → can't advance → stalled.
        // After STALLED_SAMPLE_THRESHOLD (6) iterations, recalculation triggers.
        // Corner waypoint offset by capsule-radius * 3 along bisector (~1.27y per axis for 90° turn)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 8f, 11f);
        Assert.InRange(waypoint.Y, -2f, 1f);
        Assert.True(pathfindingCalls >= 2);
    }

    [Fact]
    public void GetNextWaypoint_AdvancesPastFirstWaypoint_WhenWithinAcceptanceRadiusAfterOffset()
    {
        // Path starts at a waypoint that's not the current position.
        // LOS blocked through corner to prevent StringPull from collapsing the path.
        // Bot at (0,0), corner at (4,0) offset to ~(2.73,-1.27). Distance ~3y < acceptance ~4.2y.
        var pathfinding = new DelegatePathfindingClient(
            (_, _, _, _) => [new Position(4, 0, 0), new Position(4, 10, 0)],
            isInLineOfSight: (_, from, to) => !(from.Y < 0.5f && to.Y > 0.5f));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(4, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Bot within acceptance radius of offset corner → advances to second waypoint (4, 10)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 3f, 5f);
        Assert.InRange(waypoint.Y, 9f, 11f);
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
    public void GetNextWaypoint_AcceptsShortRouteWhenEndpointIsInsideArrivalBubble()
    {
        var pathfinding = new DelegatePathfindingClient((_, start, _, _) =>
            [start, new Position(3.6f, 0f, 2f)]);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(3.9f, 0f, 2f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(3.6f, waypoint!.X);
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
        // Probe heuristics prune collinear intermediate waypoint (10,0) since
        // (0,0)→(10,0)→(20,0) is a straight line with LOS clear up to 12y.
        // First returned waypoint is the 90° corner at (20,0) with capsule offset.
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

        // Bot at origin — collinear (10,0) pruned by probe heuristics, first target is corner (20,0)
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(20, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Corner at (20,0) offset by capsule-radius * 3 along bisector (~1.27y per axis for 90° turn)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 18f, 21f);

        // Move bot within 6y of corner — should advance to final waypoint
        waypoint = navPath.GetNextWaypoint(
            new Position(15, 0, 0),
            new Position(20, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        // Bot at 15y from origin, ~4y from offset corner. Within acceptance radius → advances to (20,10).
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 19f, 21f);
        Assert.InRange(waypoint.Y, 9f, 11f);
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
        // Corner waypoint offset by capsule-radius * 3 along bisector (~1.27y per axis for 90° turn)
        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 8f, 11f);
        Assert.InRange(waypoint.Y, -2f, 1f);
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

    [Fact]
    public void StringPull_PreservesCorner_WhenClearLosShortcutLeavesWalkableCorridor()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(10, 0, 0),
                new Position(10, 10, 0)
            ],
            isInLineOfSight: (_, _, _) => true,
            isPointOnNavmesh: (_, position, _) =>
            {
                if (position.X > 2f && position.X < 8f && MathF.Abs(position.X - position.Y) < 0.25f)
                    return (false, position);

                return (true, position);
            },
            findNearestWalkablePoint: (_, position, _) => (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 8f, 11f);
        Assert.True(waypoint.Y < 2f);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotOffsetCornerOutsideWalkableCorridor()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            [
                start,
                new Position(10, 0, 0),
                new Position(10, 10, 0)
            ],
            isInLineOfSight: (_, from, to) => !(from.Y < 1f && to.Y > 1f),
            findNearestWalkablePoint: (_, position, _) =>
                position.Y < -0.5f ? (0u, position) : (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0, 0, 0),
            new Position(10, 20, 0),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 9.5f, 10.5f);
        Assert.InRange(waypoint.Y, -0.25f, 0.25f);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotLosSkipAcrossOffCorridorShortcut()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(6, 0, 0),
                new Position(20, 0, 0),
                new Position(20, 20, 0)
            ],
            isInLineOfSight: (_, _, _) => true,
            isPointOnNavmesh: (_, position, _) =>
            {
                if (position.X > 10f && position.X < 18f && position.Y > 4f && position.Y < 18f)
                    return (false, position);

                return (true, position);
            },
            findNearestWalkablePoint: (_, position, _) => (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(6.5f, 0f, 0f),
            new Position(20f, 20f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.InRange(waypoint!.X, 18f, 21f);
        Assert.True(waypoint.Y < 2f);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotAdvanceEarly_WhenAdaptiveRadiusShortcutLeavesWalkableCorridor()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(8.5f, 0.9f, 0f),
                new Position(12.5f, 0.9f, 0f)
            ],
            isInLineOfSight: (_, from, to) => !(from.X < 0.5f && to.X > 10f),
            isPointOnNavmesh: (_, position, _) =>
            {
                if (position.X > 8.1f && position.X < 8.3f && position.Y > 0.84f && position.Y < 0.87f)
                    return (false, position);

                return (true, position);
            },
            findNearestWalkablePoint: (_, position, _) => (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);

        var initialWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(initialWaypoint);
        Assert.Equal(8.5f, initialWaypoint!.X, precision: 1);
        Assert.Equal(0.9f, initialWaypoint.Y, precision: 1);

        var shortcutAttempt = navPath.GetNextWaypoint(
            new Position(2.5f, 0.8f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(shortcutAttempt);
        Assert.Equal(8.5f, shortcutAttempt!.X, precision: 1);
        Assert.Equal(0.9f, shortcutAttempt.Y, precision: 1);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotLookAheadSkip_WhenOvershootShortcutLeavesWalkableCorridor()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(4f, 0f, 0f),
                new Position(10f, 0f, 0f),
                new Position(16f, 0f, 0f)
            ],
            isPointOnNavmesh: (_, position, _) =>
            {
                if (position.X > 9.0f && position.X < 9.2f)
                    return (false, position);

                return (true, position);
            },
            findNearestWalkablePoint: (_, position, _) => (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);

        var initialWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(initialWaypoint);
        Assert.Equal(4f, initialWaypoint!.X);
        Assert.Equal(0f, initialWaypoint.Y);

        var overshootWaypoint = navPath.GetNextWaypoint(
            new Position(8.2f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(overshootWaypoint);
        Assert.Equal(4f, overshootWaypoint!.X);
        Assert.Equal(0f, overshootWaypoint.Y);
    }

    [Fact]
    public void GetNextWaypoint_AdvancesPastOvershotWaypoint_WhenNextCorridorIsWalkable()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(4f, 0f, 0f),
                new Position(10f, 0f, 0f),
                new Position(16f, 0f, 0f)
            ],
            isPointOnNavmesh: (_, position, _) => (true, position),
            findNearestWalkablePoint: (_, position, _) => (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);

        var initialWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(initialWaypoint);
        Assert.Equal(4f, initialWaypoint!.X);
        Assert.Equal(0f, initialWaypoint.Y);

        var overshotWaypoint = navPath.GetNextWaypoint(
            new Position(8.2f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(overshotWaypoint);
        Assert.Equal(16f, overshotWaypoint!.X);
        Assert.Equal(0f, overshotWaypoint.Y);
        Assert.Equal(2, navPath.Metrics.WaypointsReached);
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

    [Fact]
    public void RerouteAroundCliff_RejectsOffsetOutsideWalkableCorridor()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [new Position(10, 0, 10)],
            getGroundZ: (_, position, _) => (position.Z, true),
            findNearestWalkablePoint: (_, position, _) =>
                position.Y < -1f ? (0u, position) : (1u, position));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var reroute = navPath.RerouteAroundCliff(
            1,
            new Position(0, 0, 10),
            new Position(10, 0, 10),
            new NavigationPath.CliffProbeResult(
                Forward: 0f,
                ForwardLeft: 0f,
                ForwardRight: 0f,
                Left: float.MaxValue,
                Right: 0f));

        Assert.Null(reroute);
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

    [Fact]
    public void GetNextWaypoint_ForwardsNearbyObjectsFromProviderToRouteRequest()
    {
        var expectedNearbyObjects = new[]
        {
            new DynamicObjectProto
            {
                Guid = 0xBEEF,
                DisplayId = 17,
                X = 4f,
                Y = 5f,
                Z = 6f,
                Orientation = 1.2f,
                Scale = 1.1f,
                GoState = 1,
            }
        };
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, end, _) => [start, end],
            getPathWithNearbyObjects: (_, start, end, nearbyObjects, _) =>
            {
                Assert.NotNull(nearbyObjects);
                Assert.Single(nearbyObjects!);
                return [start, end];
            });

        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            nearbyObjectProvider: (_, _) => expectedNearbyObjects);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(1, pathfinding.RouteResultCalls);
        Assert.Equal(0, pathfinding.LegacyGetPathCalls);
        Assert.NotNull(pathfinding.LastNearbyObjects);
        Assert.Single(pathfinding.LastNearbyObjects!);
        Assert.Equal(expectedNearbyObjects[0].Guid, pathfinding.LastNearbyObjects![0].Guid);
    }

    [Fact]
    public void GetNextWaypoint_DoesNotLocallyRejectOverlayAwareServiceRouteForDynamicSegmentIntersection()
    {
        var nearbyObjects = new[]
        {
            new DynamicObjectProto
            {
                Guid = 7,
                DisplayId = 17,
                X = 5f,
                Y = 0f,
                Z = 0f,
            }
        };

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, end, _) =>
            [
                new Position(10f, 0f, 0f),
                end
            ],
            isInLineOfSight: (_, from, to) => MathF.Abs(to.X - from.X) <= 12f,
            segmentIntersectsDynamicObjects: (_, _, _) => true);

        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            nearbyObjectProvider: (_, _) => nearbyObjects);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);
        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X);
        Assert.True(trace.RouteDecision.HasPath);
        Assert.Equal(1, pathfinding.RouteResultCalls);
        Assert.Equal(0, pathfinding.LegacyGetPathCalls);
        Assert.NotNull(pathfinding.LastNearbyObjects);
    }

    [Fact]
    public void GetNextWaypoint_PassesNullNearbyObjectsWhenOverlayProviderReturnsNoObjects()
    {
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) => [start, end]);
        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            nearbyObjectProvider: (_, _) => []);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(1, pathfinding.RouteResultCalls);
        Assert.Equal(0, pathfinding.LegacyGetPathCalls);
        Assert.Null(pathfinding.LastNearbyObjects);
    }

    [Fact]
    public void GetNextWaypoint_RetargetsStackedLocalVerticalEndpoint_ToNearbyWalkablePoint()
    {
        var requestedEnds = new List<Position>();
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, end, _) =>
            {
                requestedEnds.Add(end);
                if (MathF.Abs(end.Z - 8f) < 0.01f)
                    return [new Position(0f, 0f, 8f), end];

                return [new Position(3f, 0f, 0f), end];
            },
            findNearestWalkablePoint: (_, _, searchRadius) =>
            {
                if (searchRadius < 4f)
                    return (0u, new Position(0f, 0f, 0f));

                return (1u, new Position(6f, 0f, 0f));
            });

        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            enableProbeHeuristics: false);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(2f, 0f, 8f),
            mapId: 1,
            allowDirectFallback: false);
        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(6f, waypoint!.X);
        Assert.Equal(0f, waypoint.Y);
        Assert.Equal(0f, waypoint.Z);
        Assert.True(trace.RouteDecision.HasPath);
        Assert.True(trace.RouteDecision.EndpointRetargeted);
        Assert.Equal(2, requestedEnds.Count);
        Assert.Equal(2f, requestedEnds[0].X);
        Assert.Equal(8f, requestedEnds[0].Z);
        Assert.Equal(6f, requestedEnds[1].X);
        Assert.Equal(0f, requestedEnds[1].Z);
    }

    [Fact]
    public void GetNextWaypoint_RetargetsStackedEndpoint_UsingReferenceHeightForNearestWalkableLookup()
    {
        var nearestWalkableQueries = new List<Position>();
        var target = new Position(10.4f, 10.2f, 39.6f);

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, end, _) =>
            {
                if (MathF.Abs(end.Z - target.Z) < 0.01f)
                    return
                    [
                        new Position(10.0f, 10.0f, 34.5f),
                        new Position(10.0f, 10.0f, 39.8f),
                        end
                    ];

                return [end];
            },
            getGroundZ: (_, position, _) =>
            {
                if (position.Z > 39f)
                    return (35.3f, true);

                return (position.Z, true);
            },
            findNearestWalkablePoint: (_, position, _) =>
            {
                nearestWalkableQueries.Add(position);
                if (MathF.Abs(position.Z - 34.5f) < 0.01f)
                    return (1u, new Position(14f, 10f, 34.7f));

                return (1u, target);
            });

        var navPath = new NavigationPath(pathfinding, () => 0);

        var waypoint = navPath.GetNextWaypoint(
            new Position(10f, 10f, 34.5f),
            target,
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(14f, waypoint!.X);
        Assert.Equal(10f, waypoint.Y);
        Assert.Equal(34.7f, waypoint.Z);
        Assert.Contains(nearestWalkableQueries, query => MathF.Abs(query.Z - 34.5f) < 0.01f);
    }

    [Fact]
    public void GetNextWaypoint_RetargetsStackedEndpoint_UsingGroundSupportWhenStartZIsStale()
    {
        var nearestWalkableQueries = new List<Position>();
        var start = new Position(-477.8f, -4822.2f, 25.6f);
        var target = new Position(-480.8f, -4822.4f, 37.4f);
        var retargeted = new Position(-484.0f, -4822.8f, 34.2f);

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, end, _) =>
            {
                if (MathF.Abs(end.Z - target.Z) < 0.01f)
                    return [start, new Position(start.X, start.Y, 37.6f), end];

                return [retargeted];
            },
            getGroundZ: (_, position, _) =>
            {
                if (MathF.Abs(position.X - start.X) < 0.01f && MathF.Abs(position.Y - start.Y) < 0.01f)
                    return (34.2f, true);

                if (position.Z > 37f)
                    return (34.2f, true);

                return (position.Z, true);
            },
            findNearestWalkablePoint: (_, position, _) =>
            {
                nearestWalkableQueries.Add(position);
                if (MathF.Abs(position.Z - 34.2f) < 0.01f)
                    return (1u, retargeted);

                return (1u, target);
            });

        var navPath = new NavigationPath(pathfinding, () => 0);

        var waypoint = navPath.GetNextWaypoint(
            start,
            target,
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.Equal(retargeted.X, waypoint!.X);
        Assert.Equal(retargeted.Y, waypoint.Y);
        Assert.Equal(retargeted.Z, waypoint.Z);
        Assert.Contains(nearestWalkableQueries, query => MathF.Abs(query.Z - 34.2f) < 0.01f);
    }

    [Fact]
    public void GetNextWaypoint_TraceCapturesShortRoutePlanAndExecution()
    {
        long tick = 250;
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) =>
            [start, new Position(5f, 0f, 0f), end]);

        var navPath = new NavigationPath(
            pathfinding,
            () => tick,
            enableProbeHeuristics: false);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(NavigationTraceReason.InitialPath, trace.LastReplanReason);
        Assert.Equal("waypoint", trace.LastResolution);
        Assert.False(trace.UsedDirectFallback);
        Assert.True(trace.IsShortRoute);
        Assert.False(trace.UsedNearbyObjectOverlay);
        Assert.False(trace.SmoothPath);
        Assert.Equal(1, trace.PlanVersion);
        Assert.Equal(250, trace.LastPlanTick);
        Assert.NotNull(trace.RequestedStart);
        Assert.NotNull(trace.RequestedDestination);
        Assert.Equal(3, trace.ServiceWaypoints.Length);
        Assert.Equal(3, trace.PlannedWaypoints.Length);
        Assert.NotNull(trace.ActiveWaypoint);
        Assert.Equal(5f, trace.ActiveWaypoint!.X);
        Assert.Single(trace.ExecutionSamples);
        Assert.Equal(1, trace.ExecutionSamples[0].PlanVersion);
        Assert.Equal(1, trace.ExecutionSamples[0].WaypointIndex);
        Assert.Equal("waypoint", trace.ExecutionSamples[0].Resolution);
        Assert.False(trace.ExecutionSamples[0].UsedDirectFallback);
        Assert.NotNull(trace.ExecutionSamples[0].ReturnedWaypoint);
        Assert.Equal(5f, trace.ExecutionSamples[0].ReturnedWaypoint!.X);
    }

    [Fact]
    public void GetNextWaypoint_ShortRoutePathExhaustedWithinCooldown_RecalculatesImmediately()
    {
        long tick = 0;
        var requestedEnds = new List<Position>();
        var staleDestination = new Position(8f, 0f, 0f);
        var movingDestination = new Position(8f, 6f, 0f);

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, end, _) =>
            {
                requestedEnds.Add(end);
                if (MathF.Abs(end.Y - staleDestination.Y) < 0.01f)
                    return [new Position(6f, 0f, 0f), staleDestination];

                return [new Position(8f, 3f, 0f), movingDestination];
            },
            isInLineOfSight: (_, _, to) => MathF.Abs(to.Y - movingDestination.Y) > 0.01f);

        var navPath = new NavigationPath(
            pathfinding,
            () => tick,
            enableProbeHeuristics: false);

        var firstWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            staleDestination,
            mapId: 1,
            allowDirectFallback: true);

        Assert.NotNull(firstWaypoint);

        tick = 500;

        var replannedWaypoint = navPath.GetNextWaypoint(
            new Position(7.8f, 0f, 0f),
            movingDestination,
            mapId: 1,
            allowDirectFallback: true);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(replannedWaypoint);
        Assert.Equal(2, requestedEnds.Count);
        Assert.Equal(staleDestination.Y, requestedEnds[0].Y);
        Assert.Equal(movingDestination.Y, requestedEnds[1].Y);
        Assert.Equal(2, trace.PlanVersion);
        Assert.Equal(NavigationTraceReason.PathExhaustedStillFar, trace.LastReplanReason);
        Assert.NotNull(trace.RequestedDestination);
        Assert.Equal(movingDestination.Y, trace.RequestedDestination!.Y);
        Assert.Equal(2, trace.ServiceWaypoints.Length);
        Assert.Equal(2, trace.PlannedWaypoints.Length);
        Assert.NotNull(trace.ActiveWaypoint);
        Assert.Equal(trace.ActiveWaypoint!.Y, replannedWaypoint!.Y);
        Assert.Equal("waypoint", trace.LastResolution);
    }

    [Fact]
    public void GetNextWaypoint_TraceRecordsStallDrivenReplanReason()
    {
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            {
                pathfindingCalls++;
                return [new Position(10, 0, 0), new Position(10, 10, 0)];
            },
            isInLineOfSight: (_, from, to) => !(from.Y < 0.5f && to.Y >= 7f));

        var navPath = new NavigationPath(pathfinding, () => 10_000);
        for (var i = 0; i < 30; i++)
        {
            navPath.GetNextWaypoint(
                new Position(4f, 0f, 0f),
                new Position(10f, 20f, 0f),
                mapId: 1,
                allowDirectFallback: false,
                minWaypointDistance: 4f);
        }

        var trace = navPath.TraceSnapshot;

        Assert.True(pathfindingCalls >= 2);
        Assert.True(trace.PlanVersion >= 2);
        Assert.Equal(NavigationTraceReason.StalledNearWaypoint, trace.LastReplanReason);
        Assert.Equal("waypoint", trace.LastResolution);
        Assert.NotEmpty(trace.ExecutionSamples);
        Assert.Equal(trace.PlanVersion, trace.ExecutionSamples[^1].PlanVersion);
        Assert.Equal("waypoint", trace.ExecutionSamples[^1].Resolution);
    }

    [Fact]
    public void GetNextWaypoint_TraceRecordsMovementStuckRecoveryReplanReason()
    {
        var pathfindingCalls = 0;
        var stuckGeneration = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, _, _) =>
            {
                pathfindingCalls++;
                return pathfindingCalls == 1
                    ? [new Position(start.X + 10f, start.Y, start.Z)]
                    : [new Position(start.X, start.Y + 10f, start.Z)];
            });

        var navPath = new NavigationPath(
            pathfinding,
            () => 10_000,
            enableProbeHeuristics: false,
            stuckRecoveryGenerationProvider: () => stuckGeneration);

        var destination = new Position(20f, 20f, 0f);
        var firstWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            destination,
            mapId: 1,
            allowDirectFallback: false);

        stuckGeneration = 1;

        var replannedWaypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            destination,
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(firstWaypoint);
        Assert.NotNull(replannedWaypoint);
        Assert.Equal(2, pathfindingCalls);
        Assert.Equal(10f, firstWaypoint!.X);
        Assert.Equal(0f, firstWaypoint.Y);
        Assert.Equal(0f, replannedWaypoint!.X);
        Assert.Equal(10f, replannedWaypoint.Y);
        Assert.Equal(2, trace.PlanVersion);
        Assert.Equal(NavigationTraceReason.MovementStuckRecovery, trace.LastReplanReason);
        Assert.Equal("waypoint", trace.LastResolution);
    }

    [Fact]
    public void GetNextWaypoint_MovementStuckRecovery_DoesNotPromoteToFarWaypoint()
    {
        var stuckGeneration = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(25f, 0f, 0f),
                new Position(35f, 0f, 0f),
                new Position(45f, 0f, 0f),
            ],
            isInLineOfSight: (_, from, to) => to.X <= 35f || from.X >= 35f);

        var navPath = new NavigationPath(
            pathfinding,
            () => 10_000,
            enableProbeHeuristics: false,
            stuckRecoveryGenerationProvider: () => stuckGeneration);

        var current = new Position(0f, 0f, 0f);
        var destination = new Position(100f, 0f, 0f);

        var initialWaypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 1,
            allowDirectFallback: false);

        stuckGeneration = 1;

        var promotedWaypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(initialWaypoint);
        Assert.NotNull(promotedWaypoint);
        Assert.Equal(initialWaypoint!.X, promotedWaypoint!.X);
    }

    [Fact]
    public void GetNextWaypoint_MovementStuckRecovery_RepeatedStationaryPromotions_EscalatesToFartherCorridorWaypoint()
    {
        var stuckGeneration = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(4f, 0f, 0f),
                new Position(12f, 0f, 0f),
                new Position(38f, 0f, 0f),
            ],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(
            pathfinding,
            () => 10_000,
            enableProbeHeuristics: false,
            stuckRecoveryGenerationProvider: () => stuckGeneration);

        var current = new Position(0f, 0f, 0f);
        var destination = new Position(100f, 0f, 0f);

        var initialWaypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 1,
            allowDirectFallback: false);

        Position? promotedWaypoint = null;
        for (stuckGeneration = 1; stuckGeneration <= 4; stuckGeneration++)
        {
            promotedWaypoint = navPath.GetNextWaypoint(
                current,
                destination,
                mapId: 1,
                allowDirectFallback: false);
        }

        Assert.NotNull(initialWaypoint);
        Assert.NotNull(promotedWaypoint);
        Assert.Equal(4f, initialWaypoint!.X);
        Assert.Equal(38f, promotedWaypoint!.X);
    }

    [Fact]
    public void GetNextWaypoint_MovementStuckRecovery_RepeatedNearPromotionWithoutEscalatedHop_StopsRepromotingNearWaypoint()
    {
        var stuckGeneration = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(6f, 0f, 0f),
                new Position(10f, 0f, 0f),
                new Position(40f, 0f, 0f),
            ],
            isInLineOfSight: (_, _, _) => true,
            getGroundZ: (_, position, _) => position.X >= 20f
                ? (0f, false)
                : (position.Z, true));

        var navPath = new NavigationPath(
            pathfinding,
            () => 10_000,
            enableProbeHeuristics: false,
            stuckRecoveryGenerationProvider: () => stuckGeneration);

        var current = new Position(0f, 0f, 0f);
        var destination = new Position(100f, 0f, 0f);

        var initialWaypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 1,
            allowDirectFallback: false);

        stuckGeneration = 1;
        var nearPromotedWaypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 1,
            allowDirectFallback: false);

        Position? finalWaypoint = nearPromotedWaypoint;
        for (stuckGeneration = 2; stuckGeneration <= 5; stuckGeneration++)
        {
            finalWaypoint = navPath.GetNextWaypoint(
                current,
                destination,
                mapId: 1,
                allowDirectFallback: false);
        }

        Assert.NotNull(initialWaypoint);
        Assert.NotNull(nearPromotedWaypoint);
        Assert.NotNull(finalWaypoint);
        Assert.Equal(6f, initialWaypoint!.X);
        Assert.Equal(10f, nearPromotedWaypoint!.X);
        Assert.Equal(6f, finalWaypoint!.X);
    }

    [Fact]
    public void GetNextWaypoint_PrefersCheaperSupportedAlternateWhenPrimaryHasCliffSegment()
    {
        var start = new Position(0f, 0f, 10f);
        var destination = new Position(40f, 0f, 0f);
        var smoothPath = new[]
        {
            new Position(20f, 0f, 10f),
            new Position(40f, 0f, 0f), // cliff drop (10y)
        };
        var safeAlternatePath = new[]
        {
            new Position(10f, 0f, 8f),
            new Position(20f, 0f, 6f),
            new Position(30f, 0f, 3f),
            new Position(40f, 0f, 0f),
        };

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, smooth) => smooth ? smoothPath : safeAlternatePath);

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(0, trace.Affordances.CliffCount);
        Assert.True(trace.RouteDecision.HasPath);
        Assert.True(trace.RouteDecision.IsSupported);
        Assert.NotEqual(SegmentAffordance.Cliff, trace.RouteDecision.MaxAffordance);
        Assert.True(trace.RouteDecision.AlternateEvaluated);
        Assert.True(trace.RouteDecision.AlternateSelected);
        Assert.False(trace.RouteDecision.EndpointRetargeted);
        Assert.True(trace.RouteDecision.EstimatedCost > 0f);
        Assert.Contains(pathfinding.SmoothCalls, value => value);
        Assert.Contains(pathfinding.SmoothCalls, value => !value);
    }

    [Fact]
    public void GetNextWaypoint_MovementStuckRecoveryPrefersSaferAlternateWithinTolerance()
    {
        var start = new Position(0f, 0f, 10f);
        var destination = new Position(50f, 0f, 5f);
        var primaryDropPath = new[]
        {
            new Position(25f, 0f, 10f),
            new Position(50f, 0f, 5f),
        };
        var saferAlternatePath = new[]
        {
            new Position(10f, 12f, 9f),
            new Position(20f, 24f, 8f),
            new Position(30f, 24f, 7f),
            new Position(40f, 12f, 6f),
            new Position(50f, 0f, 5f),
        };

        int stuckGeneration = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, smooth) => smooth ? primaryDropPath : saferAlternatePath,
            isInLineOfSight: (_, from, to) => from.DistanceTo2D(to) <= 18f);

        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            enableProbeHeuristics: true,
            stuckRecoveryGenerationProvider: () => stuckGeneration);

        var initialWaypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var initialTrace = navPath.TraceSnapshot;

        stuckGeneration = 1;
        var recoveredWaypoint = navPath.GetNextWaypoint(
            start,
            destination,
            mapId: 1,
            allowDirectFallback: false);
        var recoveryTrace = navPath.TraceSnapshot;

        Assert.NotNull(initialWaypoint);
        Assert.False(initialTrace.RouteDecision.AlternateSelected);
        Assert.True(initialTrace.RouteDecision.AlternateEvaluated);
        Assert.Equal(SegmentAffordance.Drop, initialTrace.RouteDecision.MaxAffordance);

        Assert.NotNull(recoveredWaypoint);
        Assert.Equal(NavigationTraceReason.MovementStuckRecovery, recoveryTrace.LastReplanReason);
        Assert.True(recoveryTrace.RouteDecision.AlternateEvaluated);
        Assert.True(recoveryTrace.RouteDecision.AlternateSelected);
        Assert.Equal(SegmentAffordance.Walk, recoveryTrace.RouteDecision.MaxAffordance);
        Assert.True(recoveryTrace.RouteDecision.EstimatedCost > initialTrace.RouteDecision.EstimatedCost);
        Assert.Contains(pathfinding.SmoothCalls, value => value);
        Assert.Contains(pathfinding.SmoothCalls, value => !value);
    }

    [Fact]
    public void GetNextWaypoint_ReplacesCollisionLayerMismatchedWaypoint_WithNearbySupportedCandidate()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(10f, 0f, 0f),
                new Position(20f, 0f, 0f),
            ],
            getGroundZ: (_, position, _) =>
            {
                var isUpperLayerTrap = MathF.Abs(position.X - 10f) <= 0.6f
                    && MathF.Abs(position.Y) <= 0.6f;
                return isUpperLayerTrap ? (12f, true) : (0f, true);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X, precision: 1);
        Assert.Equal(2f, waypoint.Y, precision: 1);
        Assert.Equal(0f, waypoint.Z, precision: 1);
        Assert.Contains(trace.PlannedWaypoints, p =>
            MathF.Abs(p.X - 10f) < 0.2f
            && MathF.Abs(p.Y - 2f) < 0.2f
            && MathF.Abs(p.Z) < 0.2f);
    }

    [Fact]
    public void GetNextWaypoint_SelectsAlternate_WhenCollisionLayerMismatchCannotBeRepaired()
    {
        var primaryLayerMismatchPath = new[]
        {
            new Position(10f, 0f, 0f),
            new Position(30f, 0f, 0f),
        };
        var alternateSameLayerPath = new[]
        {
            new Position(10f, 20f, 0f),
            new Position(20f, 20f, 0f),
            new Position(30f, 0f, 0f),
        };

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, smooth) => smooth ? primaryLayerMismatchPath : alternateSameLayerPath,
            getGroundZ: (_, position, _) =>
            {
                var isUpperLayerTrap = position.X >= 2f
                    && position.X <= 18f
                    && MathF.Abs(position.Y) <= 9f;
                return isUpperLayerTrap ? (12f, true) : (0f, true);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(30f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(10f, waypoint!.X, precision: 1);
        Assert.Equal(20f, waypoint.Y, precision: 1);
        Assert.True(trace.RouteDecision.AlternateEvaluated);
        Assert.True(trace.RouteDecision.AlternateSelected);
        Assert.Contains(pathfinding.SmoothCalls, value => value);
        Assert.Contains(pathfinding.SmoothCalls, value => !value);
    }

    [Fact]
    public void GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer()
    {
        var primaryTrapPath = new[]
        {
            new Position(4f, 0f, 0f),
            new Position(12f, 0f, 0f),
            new Position(30f, 0f, 0f),
        };
        var alternateSameLayerPath = new[]
        {
            new Position(4f, 6f, 0f),
            new Position(16f, 6f, 0f),
            new Position(30f, 0f, 0f),
        };

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, smooth) => smooth ? primaryTrapPath : alternateSameLayerPath,
            isInLineOfSight: (_, from, to) => from.DistanceTo2D(to) <= 15f,
            findNearestWalkablePoint: (_, position, _) => (position.Y == 0f ? 1u : 0u, position),
            simulateLocalSegment: (_, from, to, _, _, _) =>
            {
                if (MathF.Abs(from.Y) <= 0.5f && MathF.Abs(to.Y) <= 0.5f && to.X >= 4f && to.X <= 12f)
                {
                    return new LocalSegmentSimulationResult(
                        Available: true,
                        Compatible: true,
                        MaxUpwardRouteZDelta: 12f,
                        MaxAbsoluteRouteZDelta: 12f,
                        MaxLateralDistance: 0.2f,
                        FinalPosition: new Position(to.X, to.Y, to.Z + 12f),
                        Reason: "test_layer_climb");
                }

                return LocalSegmentSimulationResult.CompatibleResult(to);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(30f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(4f, waypoint!.X, precision: 1);
        Assert.Equal(6f, waypoint.Y, precision: 1);
        Assert.True(trace.RouteDecision.AlternateEvaluated);
        Assert.True(trace.RouteDecision.AlternateSelected);
        Assert.Contains(pathfinding.SmoothCalls, value => value);
        Assert.Contains(pathfinding.SmoothCalls, value => !value);
    }

    [Fact]
    public void GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour()
    {
        var trapPath = new[]
        {
            new Position(4f, 0f, 0f),
            new Position(12f, 0f, 0f),
            new Position(30f, 0f, 0f),
        };

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => trapPath,
            isInLineOfSight: (_, from, to) => from.DistanceTo2D(to) <= 15f,
            simulateLocalSegment: (_, from, to, _, _, _) =>
            {
                if (MathF.Abs(from.Y) <= 0.5f && MathF.Abs(to.Y) <= 0.5f && to.X >= 4f && to.X <= 12f)
                {
                    return new LocalSegmentSimulationResult(
                        Available: true,
                        Compatible: true,
                        MaxUpwardRouteZDelta: 12f,
                        MaxAbsoluteRouteZDelta: 12f,
                        MaxLateralDistance: 0.2f,
                        FinalPosition: new Position(to.X, to.Y, to.Z + 12f),
                        Reason: "test_layer_climb");
                }

                return LocalSegmentSimulationResult.CompatibleResult(to);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(30f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.NotEqual(0f, waypoint!.Y, precision: 1);
        Assert.True(trace.RouteDecision.HasPath);
        Assert.False(trace.RouteDecision.AlternateSelected);
        Assert.Contains(trace.PlannedWaypoints, p => MathF.Abs(p.Y) > 0.5f);
    }

    [Fact]
    public void GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy()
    {
        var trapPath = new[]
        {
            new Position(4f, 0f, 0f),
            new Position(12f, 0f, 0f),
            new Position(30f, 0f, 8f),
        };

        static float DistanceFromRampCenter(Position position)
        {
            const float x0 = 4f;
            const float y0 = 2f;
            const float x1 = 30f;
            const float y1 = 0f;
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            return MathF.Abs((dy * position.X) - (dx * position.Y) + (x1 * y0) - (y1 * x0)) / len;
        }

        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => trapPath,
            isInLineOfSight: (_, from, to) => from.DistanceTo2D(to) <= 35f,
            getGroundZ: (_, position, _) =>
            {
                var isDownstreamLateralProbe = position.X > 6f
                    && DistanceFromRampCenter(position) > 0.4f;
                return isDownstreamLateralProbe
                    ? (position.Z + 3f, true)
                    : (position.Z, true);
            },
            simulateLocalSegment: (_, from, to, _, _, _) =>
            {
                if (MathF.Abs(from.Y) <= 0.5f && MathF.Abs(to.Y) <= 0.5f && to.X >= 4f && to.X <= 12f)
                {
                    return new LocalSegmentSimulationResult(
                        Available: true,
                        Compatible: false,
                        MaxUpwardRouteZDelta: 12f,
                        MaxAbsoluteRouteZDelta: 12f,
                        MaxLateralDistance: 0.2f,
                        FinalPosition: new Position(to.X, to.Y, to.Z + 12f),
                        Reason: "test_layer_climb");
                }

                return LocalSegmentSimulationResult.CompatibleResult(to);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(30f, 0f, 8f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.NotEqual(0f, waypoint!.Y, precision: 1);
        Assert.True(trace.RouteDecision.HasPath);
        Assert.False(trace.RouteDecision.AlternateSelected);
        Assert.Contains(trace.PlannedWaypoints, p => MathF.Abs(p.Y) > 0.5f);
    }

    [Fact]
    public void GetNextWaypoint_AcceptsLongLocalPhysicsHorizonHit_WhenRouteLayerRemainsConsistent()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(20f, 0f, 0f),
                new Position(40f, 0f, 0f)
            ],
            simulateLocalSegment: (_, from, to, _, _, _) =>
            {
                if (from.DistanceTo2D(to) > 12f)
                {
                    return new LocalSegmentSimulationResult(
                        Available: true,
                        Compatible: false,
                        MaxUpwardRouteZDelta: 0.1f,
                        MaxAbsoluteRouteZDelta: 0.3f,
                        MaxLateralDistance: 0.2f,
                        FinalPosition: new Position(12f, 0f, 0f),
                        Reason: "hit_wall");
                }

                return LocalSegmentSimulationResult.CompatibleResult(to);
            });

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: false);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(40f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.NotNull(waypoint);
        Assert.True(navPath.TraceSnapshot.RouteDecision.HasPath);
    }

    [Fact]
    public void GetNextWaypoint_RejectsShortLocalPhysicsHitWall()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) =>
            [
                new Position(5f, 0f, 0f),
                new Position(12f, 0f, 0f)
            ],
            findNearestWalkablePoint: (_, position, _) => (0u, position),
            simulateLocalSegment: (_, from, to, _, _, _) => new LocalSegmentSimulationResult(
                Available: true,
                Compatible: false,
                MaxUpwardRouteZDelta: 0.1f,
                MaxAbsoluteRouteZDelta: 0.3f,
                MaxLateralDistance: 0.2f,
                FinalPosition: new Position(3f, 0f, 0f),
                Reason: "hit_wall"));

        var navPath = new NavigationPath(pathfinding, () => 0, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(12f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        Assert.Null(waypoint);
        Assert.False(navPath.TraceSnapshot.RouteDecision.HasPath);
    }

    [Fact]
    public void GetNextWaypoint_TraceRecordsDynamicBlockerDrivenReplanReason()
    {
        long tick = 10_000;
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, end, _) =>
            {
                pathfindingCalls++;
                return [start, end];
            },
            segmentIntersectsDynamicObjects: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => tick, enableProbeHeuristics: true);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.Null(waypoint);
        Assert.True(pathfindingCalls >= 2);
        Assert.True(trace.PlanVersion >= 2);
        Assert.Equal(NavigationTraceReason.DynamicBlockerObserved, trace.LastReplanReason);
        Assert.Equal("no_route", trace.LastResolution);
    }

    [Fact]
    public void GetNextWaypoint_TraceRecordsServiceDynamicOverlayReplanReason()
    {
        long tick = 10_000;
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, end, _) => [start, end],
            getPathResult: (_, _, _, nearbyObjects, _, _, _) =>
            {
                Assert.NotNull(nearbyObjects);
                Assert.Single(nearbyObjects!);

                return new PathfindingRouteResult(
                    Corners: [],
                    Result: "blocked_by_dynamic_overlay",
                    RawCornerCount: 2,
                    BlockedSegmentIndex: 0,
                    BlockedReason: "dynamic_overlay",
                    MaxAffordance: PathSegmentAffordance.Walk,
                    PathSupported: false,
                    StepUpCount: 0,
                    DropCount: 0,
                    CliffCount: 0,
                    VerticalCount: 0,
                    TotalZGain: 0f,
                    TotalZLoss: 0f,
                    MaxSlopeAngleDeg: 0f,
                    JumpGapCount: 0,
                    SafeDropCount: 0,
                    UnsafeDropCount: 0,
                    BlockedCount: 0,
                    MaxClimbHeight: 0f,
                    MaxGapDistance: 0f,
                    MaxDropHeight: 0f);
            });

        var navPath = new NavigationPath(
            pathfinding,
            () => tick,
            enableProbeHeuristics: true,
            nearbyObjectProvider: (_, _) =>
            [
                new DynamicObjectProto
                {
                    Guid = 1,
                    DisplayId = 17,
                    X = 4f,
                    Y = 5f,
                    Z = 6f,
                }
            ]);

        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: false);

        var trace = navPath.TraceSnapshot;

        Assert.Null(waypoint);
        Assert.True(pathfinding.RouteResultCalls >= 2);
        Assert.Equal(0, pathfinding.LegacyGetPathCalls);
        Assert.Equal(NavigationTraceReason.DynamicBlockerObserved, trace.LastReplanReason);
        Assert.Equal("no_route", trace.LastResolution);
    }

    [Fact]
    public void GetNextWaypoint_TraceRecordsDirectFallbackWhenNoRouteExists()
    {
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [],
            isInLineOfSight: (_, _, _) => true);

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            new Position(20f, 0f, 0f),
            mapId: 1,
            allowDirectFallback: true);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(20f, waypoint!.X);
        Assert.Equal(NavigationTraceReason.InitialPath, trace.LastReplanReason);
        Assert.Equal("direct_fallback", trace.LastResolution);
        Assert.True(trace.UsedDirectFallback);
        Assert.False(trace.RouteDecision.HasPath);
        Assert.Empty(trace.ServiceWaypoints);
        Assert.Empty(trace.PlannedWaypoints);
        Assert.Null(trace.ActiveWaypoint);
        Assert.Single(trace.ExecutionSamples);
        Assert.True(trace.ExecutionSamples[0].UsedDirectFallback);
        Assert.Equal("direct_fallback", trace.ExecutionSamples[0].Resolution);
        Assert.NotNull(trace.ExecutionSamples[0].ReturnedWaypoint);
        Assert.Equal(20f, trace.ExecutionSamples[0].ReturnedWaypoint!.X);
    }

    [Fact]
    public void GetNextWaypoint_DirectFallbackUsesWalkableCorridorForShortSegmentWhenLosFails()
    {
        var destination = new Position(12.3f, 2.4f, 0.5f);
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [],
            isInLineOfSight: (_, _, _) => false,
            simulateLocalSegment: (_, from, to, _, _, _) => new LocalSegmentSimulationResult(
                Available: true,
                Compatible: true,
                FinalPosition: to,
                MaxLateralDistance: 0.2f,
                MaxAbsoluteRouteZDelta: 0.5f,
                MaxUpwardRouteZDelta: 0.5f,
                Reason: "ok"));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            destination,
            mapId: 1,
            allowDirectFallback: true);

        var trace = navPath.TraceSnapshot;

        Assert.NotNull(waypoint);
        Assert.Equal(destination.X, waypoint!.X, 3);
        Assert.Equal(destination.Y, waypoint.Y, 3);
        Assert.Equal(destination.Z, waypoint.Z, 3);
        Assert.Equal("direct_fallback", trace.LastResolution);
        Assert.True(trace.UsedDirectFallback);
    }

    [Fact]
    public void GetNextWaypoint_DirectFallbackStillRejectsLongSegmentWhenLosFails()
    {
        var destination = new Position(25f, 0f, 0.5f);
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, _, _, _) => [],
            isInLineOfSight: (_, _, _) => false,
            simulateLocalSegment: (_, from, to, _, _, _) => new LocalSegmentSimulationResult(
                Available: true,
                Compatible: true,
                FinalPosition: to,
                MaxLateralDistance: 0.2f,
                MaxAbsoluteRouteZDelta: 0.5f,
                MaxUpwardRouteZDelta: 0.5f,
                Reason: "ok"));

        var navPath = new NavigationPath(pathfinding, () => 0);
        var waypoint = navPath.GetNextWaypoint(
            new Position(0f, 0f, 0f),
            destination,
            mapId: 1,
            allowDirectFallback: true);

        var trace = navPath.TraceSnapshot;

        Assert.Null(waypoint);
        Assert.Equal("no_route", trace.LastResolution);
        Assert.False(trace.UsedDirectFallback);
    }

    [Fact]
    public void GetNextWaypoint_ActivatesElevatorTransport_AndBypassesPathfinding()
    {
        var pathfindingCalls = 0;
        var pathfinding = new DelegatePathfindingClient((_, start, end, _) =>
        {
            pathfindingCalls++;
            return [start, end];
        });

        var current = new Position(1544.24f, 240.77f, 55.40f);
        var destination = new Position(1544.24f, 240.77f, -43.0f);
        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            nearbyObjectProvider: (_, _) => []);

        var waypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 0,
            allowDirectFallback: false,
            currentTransportGuid: 0);

        Assert.NotNull(waypoint);
        Assert.Equal(1544.24f, waypoint!.X, 2);
        Assert.Equal(240.77f, waypoint.Y, 2);
        Assert.Equal(55.40f, waypoint.Z, 2);
        Assert.True(navPath.IsRidingTransport);
        Assert.True(navPath.ShouldHoldPositionForTransport(current, waypoint));
        Assert.Equal(0, pathfindingCalls);
    }

    [Fact]
    public void GetNextWaypoint_ElevatorRide_HoldsPositionWhileBoarded()
    {
        IReadOnlyList<DynamicObjectProto> nearbyObjects = [];
        var pathfinding = new DelegatePathfindingClient(
            getPath: (_, start, end, _) => [start, end],
            getPathWithNearbyObjects: (_, start, end, _, _) => [start, end]);
        var current = new Position(1544.24f, 240.77f, 55.40f);
        var destination = new Position(1544.24f, 240.77f, -43.0f);
        var navPath = new NavigationPath(
            pathfinding,
            () => 0,
            nearbyObjectProvider: (_, _) => nearbyObjects);

        navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 0,
            allowDirectFallback: false,
            currentTransportGuid: 0);

        nearbyObjects =
        [
            new DynamicObjectProto
            {
                Guid = 1,
                DisplayId = 455,
                X = 1544.24f,
                Y = 240.77f,
                Z = 55.40f,
            }
        ];

        navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 0,
            allowDirectFallback: false,
            currentTransportGuid: 0);

        var ridingWaypoint = navPath.GetNextWaypoint(
            current,
            destination,
            mapId: 0,
            allowDirectFallback: false,
            currentTransportGuid: 12345);

        Assert.Null(ridingWaypoint);
        Assert.True(navPath.IsRidingTransport);
        Assert.True(navPath.ShouldHoldPositionForTransport(current, ridingWaypoint));
    }

    private sealed class DelegatePathfindingClient : PathfindingClient
    {
        private readonly Func<uint, Position, Position, bool, Position[]> _getPath;
        private readonly Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Position[]>? _getPathWithNearbyObjects;
        private readonly Func<uint, Position, float, (bool onNavmesh, Position nearestPoint)> _isPointOnNavmesh;
        private readonly Func<uint, Position, float, (uint areaType, Position nearestPoint)> _findNearestWalkablePoint;
        private readonly Func<uint, Position, Position, bool> _segmentIntersectsDynamicObjects;
        private readonly Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Race, Gender, PathfindingRouteResult>? _getPathResult;
        private readonly Func<uint, Position, Position, Race, Gender, float, LocalSegmentSimulationResult>? _simulateLocalSegment;

        public DelegatePathfindingClient(
            Func<uint, Position, Position, bool, Position[]> getPath,
            Func<uint, Position, Position, bool>? isInLineOfSight = null,
            Func<uint, Position, float, (float, bool)>? getGroundZ = null,
            Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Position[]>? getPathWithNearbyObjects = null,
            Func<uint, Position, float, (bool onNavmesh, Position nearestPoint)>? isPointOnNavmesh = null,
            Func<uint, Position, float, (uint areaType, Position nearestPoint)>? findNearestWalkablePoint = null,
            Func<uint, Position, Position, bool>? segmentIntersectsDynamicObjects = null,
            Func<uint, Position, Position, IReadOnlyList<DynamicObjectProto>?, bool, Race, Gender, PathfindingRouteResult>? getPathResult = null,
            Func<uint, Position, Position, Race, Gender, float, LocalSegmentSimulationResult>? simulateLocalSegment = null)
        {
            _getPath = getPath;
            _getPathWithNearbyObjects = getPathWithNearbyObjects;
            _isPointOnNavmesh = isPointOnNavmesh ?? ((_, position, _) => (true, position));
            _findNearestWalkablePoint = findNearestWalkablePoint ?? ((_, position, _) => (1u, position));
            _segmentIntersectsDynamicObjects = segmentIntersectsDynamicObjects ?? ((_, _, _) => false);
            _getPathResult = getPathResult;
            _simulateLocalSegment = simulateLocalSegment;

            // GroundZ + LineOfSight no longer flow through PathfindingClient.
            // Install them on NativeLocalPhysics so the production NavigationPath
            // calls (which now go straight to the static helper) still observe
            // the per-test mock. Tests in this suite are serialized via
            // [Collection], so static-state leakage between cases is bounded.
            var losDelegate = isInLineOfSight ?? ((_, _, _) => true);
            WoWSharpClient.Movement.NativeLocalPhysics.TestLineOfSightOverride =
                (mapId, fx, fy, fz, tx, ty, tz) => losDelegate(mapId, new Position(fx, fy, fz), new Position(tx, ty, tz));

            if (getGroundZ != null)
                WoWSharpClient.Movement.NativeLocalPhysics.TestGetGroundZOverride =
                    (mapId, x, y, z, maxDist) => getGroundZ(mapId, new Position(x, y, z), maxDist);
            else
                WoWSharpClient.Movement.NativeLocalPhysics.TestGetGroundZOverride =
                    (mapId, x, y, z, _) => (z, true);
        }

        public int LegacyGetPathCalls { get; private set; }
        public int OverlayGetPathCalls { get; private set; }
        public int RouteResultCalls { get; private set; }
        public IReadOnlyList<DynamicObjectProto>? LastNearbyObjects { get; private set; }
        public List<bool> SmoothCalls { get; } = [];

        public override Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
        {
            LegacyGetPathCalls++;
            SmoothCalls.Add(smoothPath);
            return _getPath(mapId, start, end, smoothPath);
        }

        public override Position[] GetPath(uint mapId, Position start, Position end, IReadOnlyList<DynamicObjectProto>? nearbyObjects, bool smoothPath = false, Race race = 0, Gender gender = 0)
        {
            OverlayGetPathCalls++;
            SmoothCalls.Add(smoothPath);
            LastNearbyObjects = nearbyObjects?.ToArray();
            return _getPathWithNearbyObjects?.Invoke(mapId, start, end, nearbyObjects, smoothPath)
                ?? _getPath(mapId, start, end, smoothPath);
        }

        public override PathfindingRouteResult GetPathResult(uint mapId, Position start, Position end, IReadOnlyList<DynamicObjectProto>? nearbyObjects, bool smoothPath = false, Race race = 0, Gender gender = 0)
        {
            RouteResultCalls++;
            SmoothCalls.Add(smoothPath);
            LastNearbyObjects = nearbyObjects?.ToArray();

            if (_getPathResult is not null)
                return _getPathResult(mapId, start, end, nearbyObjects, smoothPath, race, gender);

            var corners = _getPathWithNearbyObjects?.Invoke(mapId, start, end, nearbyObjects, smoothPath)
                ?? _getPath(mapId, start, end, smoothPath);
            return new PathfindingRouteResult(
                Corners: corners,
                Result: corners.Length > 0 ? "native_path" : "no_path",
                RawCornerCount: (uint)corners.Length,
                BlockedSegmentIndex: null,
                BlockedReason: "none",
                MaxAffordance: PathSegmentAffordance.Walk,
                PathSupported: corners.Length > 0,
                StepUpCount: 0,
                DropCount: 0,
                CliffCount: 0,
                VerticalCount: 0,
                TotalZGain: 0f,
                TotalZLoss: 0f,
                MaxSlopeAngleDeg: 0f,
                JumpGapCount: 0,
                SafeDropCount: 0,
                UnsafeDropCount: 0,
                BlockedCount: 0,
                MaxClimbHeight: 0f,
                MaxGapDistance: 0f,
                MaxDropHeight: 0f);
        }

        public override (bool onNavmesh, Position nearestPoint) IsPointOnNavmesh(uint mapId, Position position, float searchRadius = 4.0f)
            => _isPointOnNavmesh(mapId, position, searchRadius);

        public override (uint areaType, Position nearestPoint) FindNearestWalkablePoint(uint mapId, Position position, float searchRadius = 8.0f)
            => _findNearestWalkablePoint(mapId, position, searchRadius);

        public override bool SegmentIntersectsDynamicObjects(uint mapId, Position from, Position to)
            => _segmentIntersectsDynamicObjects(mapId, from, to);

        public override LocalSegmentSimulationResult SimulateLocalSegment(
            uint mapId,
            Position from,
            Position to,
            Race race = 0,
            Gender gender = 0,
            float maxDistance = 12.0f,
            float deltaTime = 0.05f)
            => _simulateLocalSegment?.Invoke(mapId, from, to, race, gender, maxDistance)
                ?? LocalSegmentSimulationResult.Unavailable(from);
    }
}
