using System;
using System.Collections.Generic;
using BotRunner.Tests.LiveValidation.Harness;
using Communication;
using Game;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for the Layer-1 quick-fail guards used by the bake-validation
/// harness. Each test fabricates a <see cref="WoWActivitySnapshot"/>
/// stream and asserts the guard fires (or doesn't) with the expected
/// failure message class. No live infrastructure required.
/// </summary>
public class BakeValidationGuardsTests
{
    private static readonly DateTime BaseTime = new(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PathGeometry_NearestPointDistance2D_EmptyPathReturnsInfinity()
    {
        var dist = PathGeometry.NearestPointDistance2D(0f, 0f, Array.Empty<PathGeometry.Point2D>());
        Assert.True(float.IsPositiveInfinity(dist));
    }

    [Fact]
    public void PathGeometry_NearestPointDistance2D_SinglePointReturnsEuclidean()
    {
        var path = new[] { new PathGeometry.Point2D(3f, 4f) };
        var dist = PathGeometry.NearestPointDistance2D(0f, 0f, path);
        Assert.Equal(5f, dist, 4);
    }

    [Fact]
    public void PathGeometry_NearestPointDistance2D_PointBetweenSegmentsProjects()
    {
        // Path along the x-axis from (0,0) → (10,0). Bot at (5, 3) should be 3y off.
        var path = new[] { new PathGeometry.Point2D(0f, 0f), new PathGeometry.Point2D(10f, 0f) };
        var dist = PathGeometry.NearestPointDistance2D(5f, 3f, path);
        Assert.Equal(3f, dist, 4);
    }

    [Fact]
    public void PathGeometry_NearestPointDistance2D_PointBeyondEndpointClampsToEnd()
    {
        var path = new[] { new PathGeometry.Point2D(0f, 0f), new PathGeometry.Point2D(10f, 0f) };
        // (15, 0) is 5y past end of segment, projection clamps to (10, 0).
        var dist = PathGeometry.NearestPointDistance2D(15f, 0f, path);
        Assert.Equal(5f, dist, 4);
    }

    [Fact]
    public void PathGeometry_NearestPointDistance2D_MultiSegmentPicksBestSegment()
    {
        // L-shaped path: (0,0) → (10,0) → (10,10). Bot at (10.5, 5) → 0.5y off the second leg.
        var path = new[]
        {
            new PathGeometry.Point2D(0f, 0f),
            new PathGeometry.Point2D(10f, 0f),
            new PathGeometry.Point2D(10f, 10f),
        };
        var dist = PathGeometry.NearestPointDistance2D(10.5f, 5f, path);
        Assert.Equal(0.5f, dist, 4);
    }

    [Fact]
    public void LedgeFallGuard_SingleLargeDrop_FailsImmediately()
    {
        var clock = new ManualClock(BaseTime);
        var guard = new LedgeFallGuard("BRM south face", ledgeFallThresholdY: 5f, clock: clock.Now);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 100f, flags: 0u), (m, _) => failure = m);
        Assert.Null(failure);

        // Drop 12y between consecutive samples without JUMPING flag → fall.
        guard.FailIfStalled(SnapshotAt(0, 0.5f, 0f, 88f, flags: 0u), (m, _) => failure = m);

        Assert.NotNull(failure);
        Assert.Contains("LedgeFall", failure);
        Assert.Contains("dz=-12", failure);
    }

    [Fact]
    public void LedgeFallGuard_DropWhileJumping_DoesNotFail()
    {
        var guard = new LedgeFallGuard("OG zeppelin board", ledgeFallThresholdY: 5f);
        string? failure = null;

        // 0x2000 is the JUMPING flag.
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, flags: 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 88f, flags: 0x2000u), (m, _) => failure = m);

        Assert.Null(failure);
    }

    [Fact]
    public void LedgeFallGuard_DropOnTransport_DoesNotFail()
    {
        var guard = new LedgeFallGuard("OG zeppelin transit", ledgeFallThresholdY: 5f);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, flags: 0u), (m, _) => failure = m);
        guard.FailIfStalled(
            SnapshotAt(1, 0f, 0f, 70f, flags: 0u, transportGuid: 0xDEADBEEFul),
            (m, _) => failure = m);

        Assert.Null(failure);
    }

    [Fact]
    public void LedgeFallGuard_SmallStepDoesNotFail()
    {
        var guard = new LedgeFallGuard("nominal stair", ledgeFallThresholdY: 5f);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 100f, flags: 0u), (m, _) => failure = m);
        // 1.5y stair drop is normal in WoW geometry — must not trip the guard.
        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 98.5f, flags: 0u), (m, _) => failure = m);

        Assert.Null(failure);
    }

    [Fact]
    public void LedgeFallGuard_MapChangeResetsBaseline()
    {
        var guard = new LedgeFallGuard("cross-map", ledgeFallThresholdY: 5f);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, flags: 0u), (m, _) => failure = m);
        // Map switches to 0; large dz is across instances, must not be treated as a fall.
        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 30f, flags: 0u), (m, _) => failure = m);

        Assert.Null(failure);
    }

    [Fact]
    public void LedgeFallGuard_ConfirmationWindow_DelaysFire()
    {
        var clock = new ManualClock(BaseTime);
        var guard = new LedgeFallGuard(
            "lake plunge",
            ledgeFallThresholdY: 5f,
            confirmationWindow: TimeSpan.FromSeconds(2),
            clock: clock.Now);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 100f, flags: 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 88f, flags: 0u), (m, _) => failure = m);
        Assert.Null(failure);

        clock.Advance(TimeSpan.FromSeconds(2.1));
        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 75f, flags: 0u), (m, _) => failure = m);
        Assert.NotNull(failure);
    }

    [Fact]
    public void OffPathGuard_OnPathDoesNotFail()
    {
        var clock = new ManualClock(BaseTime);
        var path = new[] { new PathGeometry.Point2D(0f, 0f), new PathGeometry.Point2D(100f, 0f) };
        var guard = new OffPathGuard(
            "FlameCrest -> UBRS",
            path,
            offPathThresholdY: 10f,
            dwellTimeout: TimeSpan.FromSeconds(2),
            clock: clock.Now);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(0, 50f, 5f, 100f, 0u), (m, _) => failure = m);
        clock.Advance(TimeSpan.FromSeconds(3));
        guard.FailIfStalled(SnapshotAt(0, 50f, 5f, 100f, 0u), (m, _) => failure = m);

        Assert.Null(failure);
    }

    [Fact]
    public void OffPathGuard_DriftBriefly_DoesNotFail()
    {
        var clock = new ManualClock(BaseTime);
        var path = new[] { new PathGeometry.Point2D(0f, 0f), new PathGeometry.Point2D(100f, 0f) };
        var guard = new OffPathGuard("transient", path, 10f, TimeSpan.FromSeconds(2), clock.Now);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(0, 50f, 25f, 100f, 0u), (m, _) => failure = m);
        clock.Advance(TimeSpan.FromSeconds(1.5));
        // Bot returns to corridor before timeout.
        guard.FailIfStalled(SnapshotAt(0, 50f, 5f, 100f, 0u), (m, _) => failure = m);

        Assert.Null(failure);
    }

    [Fact]
    public void OffPathGuard_DriftSustainedBeyondTimeout_Fails()
    {
        var clock = new ManualClock(BaseTime);
        var path = new[] { new PathGeometry.Point2D(0f, 0f), new PathGeometry.Point2D(100f, 0f) };
        var guard = new OffPathGuard("FlameCrest -> UBRS", path, 10f, TimeSpan.FromSeconds(2), clock.Now);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(0, 50f, 25f, 100f, 0u), (m, _) => failure = m);
        clock.Advance(TimeSpan.FromSeconds(2.5));
        guard.FailIfStalled(SnapshotAt(0, 50f, 28f, 100f, 0u), (m, _) => failure = m);

        Assert.NotNull(failure);
        Assert.Contains("OffPath", failure);
        Assert.Contains("FlameCrest -> UBRS", failure);
    }

    [Fact]
    public void OffPathGuard_EmptyPathSilent()
    {
        var guard = new OffPathGuard("empty", Array.Empty<PathGeometry.Point2D>());
        string? failure = null;
        guard.FailIfStalled(SnapshotAt(0, 1000f, 1000f, 0f, 0u), (m, _) => failure = m);
        Assert.Null(failure);
    }

    [Fact]
    public void ZRunawayGuard_AccumulatesAbsoluteDelta()
    {
        var guard = new ZRunawayGuard("OG climb", zBudgetYards: 30f);

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, 0u), (_, _) => Assert.Fail("seed sample must not fail"));
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 110f, 0u), (_, _) => Assert.Fail("10y bump under budget"));
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 95f, 0u), (_, _) => Assert.Fail("|−15|+10=25 still under"));

        Assert.Equal(25f, guard.Accumulated, 4);
    }

    [Fact]
    public void ZRunawayGuard_BudgetExceededFires()
    {
        var guard = new ZRunawayGuard("OG climb", zBudgetYards: 20f);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 115f, 0u), (m, _) => failure = m); // +15
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 105f, 0u), (m, _) => failure = m); // +10 → 25 > 20

        Assert.NotNull(failure);
        Assert.Contains("ZRunaway", failure);
        Assert.Contains("OG climb", failure);
    }

    [Fact]
    public void ZRunawayGuard_MarkLegitimateRefundsBudget()
    {
        var guard = new ZRunawayGuard("offmesh OG", zBudgetYards: 20f);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 115f, 0u), (m, _) => failure = m); // +15

        // Caller observed an off-mesh-link traversal that legitimately drops 12y.
        guard.MarkLegitimateZTransition(12f);

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 105f, 0u), (m, _) => failure = m); // +10 → was 25, now 13

        Assert.Null(failure);
    }

    [Fact]
    public void ZRunawayGuard_MapChangeResetsAccumulator()
    {
        var guard = new ZRunawayGuard("cross-map", zBudgetYards: 5f);
        string? failure = null;

        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 100f, 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 102f, 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(1, 0f, 0f, 105f, 0u), (m, _) => failure = m); // +5 at edge

        Assert.Null(failure);

        // Map change → reset.
        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 50f, 0u), (m, _) => failure = m);
        guard.FailIfStalled(SnapshotAt(0, 0f, 0f, 52f, 0u), (m, _) => failure = m);

        Assert.Null(failure);
        Assert.Equal(2f, guard.Accumulated, 4);
    }

    private static WoWActivitySnapshot SnapshotAt(
        uint mapId,
        float x,
        float y,
        float z,
        uint flags,
        ulong transportGuid = 0ul)
        => new()
        {
            CurrentMapId = mapId,
            MovementData = new MovementData
            {
                Position = new Position { X = x, Y = y, Z = z },
                MovementFlags = flags,
                TransportGuid = transportGuid,
            }
        };

    /// <summary>
    /// Deterministic clock for guard tests — call <see cref="Advance"/> to
    /// step time without sleeping.
    /// </summary>
    private sealed class ManualClock
    {
        private DateTime _now;
        public ManualClock(DateTime start) { _now = start; }
        public DateTime Now() => _now;
        public void Advance(TimeSpan delta) { _now = _now.Add(delta); }
    }
}
