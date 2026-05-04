using System;
using Communication;
using Game;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

public class LongPathingRouteBlockerGuardTests
{
    [Fact]
    public void TryDescribeImmediateBlocker_OrgrimmarSteepClimbDiagnostic_ReturnsFailure()
    {
        var snapshot = SnapshotAt(1, 1384.5f, -4382.6f, 26.0f);
        snapshot.RecentChatMessages.Add(
            "[TRAVEL_WALK_NAV] leg=1 nav=True stuck=0 plan=7 smooth=True reason=vertical_layer_mismatch " +
            "resolution=waypoint idx=1 afford=SteepClimb agent=Tauren/Male capsule=(0.975,2.625) " +
            "player=(1384.5,-4382.6,26.0) target=(1320.1,-4653.2,53.9)");

        var blocked = LongPathingRouteBlockers.TryDescribeImmediateBlocker(snapshot, out var reason);

        Assert.True(blocked);
        Assert.Contains("steep climb", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDescribeImmediateBlocker_OtherWalkTarget_DoesNotFail()
    {
        var snapshot = SnapshotAt(1, 1677.5f, -4315.8f, 61.2f);
        snapshot.RecentChatMessages.Add(
            "[TRAVEL_WALK_NAV] leg=1 nav=True stuck=0 plan=1 smooth=True reason=initial_path " +
            "resolution=waypoint idx=3 afford=SteepClimb agent=Tauren/Male capsule=(0.975,2.625) " +
            "player=(1677.5,-4315.8,61.2) target=(1320.0,-4649.0,53.0)");

        var blocked = LongPathingRouteBlockers.TryDescribeImmediateBlocker(snapshot, out var reason);

        Assert.False(blocked);
        Assert.Empty(reason);
    }

    [Fact]
    public void TryDescribeImmediateBlocker_TowerBaseNoRouteDiagnostic_ReturnsFailure()
    {
        var snapshot = SnapshotAt(1, 1342.7f, -4641.4f, 24.6f);
        snapshot.RecentChatMessages.Add(
            "[TRAVEL_WALK_NAV] leg=1 nav=False stuck=0 plan=12 smooth=True reason=vertical_layer_mismatch " +
            "resolution=no_route idx=5 afford=StepUp agent=Tauren/Male capsule=(0.975,2.625) " +
            "player=(1342.7,-4641.4,24.6) target=(1320.1,-4653.2,53.9) active=none");

        var blocked = LongPathingRouteBlockers.TryDescribeImmediateBlocker(snapshot, out var reason);

        Assert.True(blocked);
        Assert.Contains("tower", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("base", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1675.9f, -4334.7f, 56.0f, "bonfire")]
    [InlineData(1605.0f, -4425.2f, 10.2f, "palm-tree")]
    [InlineData(1384.5f, -4382.6f, 26.0f, "steep-incline")]
    [InlineData(1342.7f, -4641.4f, 24.6f, "tower base")]
    public void FailIfBlocked_KnownBlockerZoneStationary_FailsAfterDwell(
        float x,
        float y,
        float z,
        string expectedReason)
    {
        var guard = new LongPathingRouteBlockerGuard(TimeSpan.Zero, movementThresholdYards: 1.5f);
        string? failure = null;

        guard.FailIfBlocked(SnapshotAt(1, x, y, z), (message, _) => failure = message);
        guard.FailIfBlocked(SnapshotAt(1, x + 0.1f, y, z), (message, _) => failure = message);

        Assert.NotNull(failure);
        Assert.Contains(expectedReason, failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailIfBlocked_MovingThroughKnownBlockerZone_DoesNotFail()
    {
        var guard = new LongPathingRouteBlockerGuard(TimeSpan.Zero, movementThresholdYards: 1.5f);
        string? failure = null;

        guard.FailIfBlocked(SnapshotAt(1, 1605.0f, -4425.2f, 10.2f), (message, _) => failure = message);
        guard.FailIfBlocked(SnapshotAt(1, 1608.0f, -4425.2f, 10.2f), (message, _) => failure = message);

        Assert.Null(failure);
    }

    private static WoWActivitySnapshot SnapshotAt(uint mapId, float x, float y, float z)
        => new()
        {
            CurrentMapId = mapId,
            MovementData = new MovementData
            {
                Position = new Position { X = x, Y = y, Z = z }
            }
        };
}
