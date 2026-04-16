using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Movement;
using WoWSharpClient.Models;
using WoWSharpClient.Tests.Handlers;
using WoWSharpClient.Tests.Util;

namespace WoWSharpClient.Tests.Movement;

public class ActiveSplineStepTests
{
    private static Spline MakeLinearSpline(float x0, float x1, uint durationMs)
    {
        var points = new List<Position> { new(x0, 0, 0), new(x1, 0, 0) };
        return new Spline(1, 1, 0, SplineFlags.None, points, durationMs);
    }

    private static Spline Make3PointSpline()
    {
        var points = new List<Position>
        {
            new(0, 0, 0),
            new(10, 0, 0),
            new(20, 0, 0)
        };
        return new Spline(1, 1, 0, SplineFlags.None, points, 2000);
    }

    private static Spline MakeCyclicFlyingSpline()
    {
        var points = new List<Position>
        {
            new(0, 0, 0),
            new(10, 0, 0),
            new(10, 10, 0),
            new(0, 0, 0),
        };
        return new Spline(1, 1, 0, SplineFlags.Flying | SplineFlags.Cyclic, points, 3000);
    }

    private static Position CatmullRomExpected(Position p0, Position p1, Position p2, Position p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return new Position(
            0.5f * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
            0.5f * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3),
            0.5f * (2 * p1.Z + (-p0.Z + p2.Z) * t + (2 * p0.Z - 5 * p1.Z + 4 * p2.Z - p3.Z) * t2 + (-p0.Z + 3 * p1.Z - 3 * p2.Z + p3.Z) * t3)
        );
    }

    [Fact]
    public void Step_AtStart_ReturnsFirstPoint()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        var pos = active.Step(0);

        Assert.Equal(0f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_AtHalf_ReturnsMidpoint()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        var pos = active.Step(500);

        Assert.Equal(5f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_AtEnd_ReturnsLastPoint()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        var pos = active.Step(1000);

        Assert.Equal(10f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_PastEnd_ReturnsLastPoint()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        var pos = active.Step(5000);

        Assert.Equal(10f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_MultipleIncrements_Accumulate()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        active.Step(250);
        active.Step(250);
        var pos = active.Step(0); // read at 500ms

        Assert.Equal(5f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_3Points_FirstSegment()
    {
        var active = new ActiveSpline(Make3PointSpline());

        // 2 segments, 2000ms total → 1000ms per segment
        // At 500ms, halfway through first segment: X = 5
        var pos = active.Step(500);

        Assert.Equal(5f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_3Points_SecondSegment()
    {
        var active = new ActiveSpline(Make3PointSpline());

        // 1500ms = 1000ms (segment 0 done) + 500ms into segment 1
        // Segment 1 goes from X=10 to X=20, halfway = X=15
        var pos = active.Step(1500);

        Assert.Equal(15f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_3Points_AtSegmentBoundary()
    {
        var active = new ActiveSpline(Make3PointSpline());

        // At 1000ms, exactly at second point (X=10)
        var pos = active.Step(1000);

        Assert.Equal(10f, pos.X, 0.01f);
    }

    [Fact]
    public void Step_InterpolatesYAndZ()
    {
        var points = new List<Position>
        {
            new(0, 0, 0),
            new(10, 20, 30)
        };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 1000);
        var active = new ActiveSpline(spline);

        var pos = active.Step(500);

        Assert.Equal(5f, pos.X, 0.01f);
        Assert.Equal(10f, pos.Y, 0.01f);
        Assert.Equal(15f, pos.Z, 0.01f);
    }

    [Fact]
    public void Step_NegativeCoordinates()
    {
        var points = new List<Position>
        {
            new(-100, -200, -50),
            new(100, 200, 50)
        };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 2000);
        var active = new ActiveSpline(spline);

        var pos = active.Step(1000); // midpoint

        Assert.Equal(0f, pos.X, 0.01f);
        Assert.Equal(0f, pos.Y, 0.01f);
        Assert.Equal(0f, pos.Z, 0.01f);
    }

    [Fact]
    public void Step_SinglePoint_ReturnsPoint()
    {
        var points = new List<Position> { new(42, 0, 0) };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 1000);
        var active = new ActiveSpline(spline);

        var pos = active.Step(500);

        Assert.Equal(42f, pos.X, 0.01f);
    }

    [Fact]
    public void Finished_FalseAtStart()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        active.Step(0);
        Assert.False(active.Finished);
    }

    [Fact]
    public void Finished_TrueAtEnd()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        active.Step(1000);
        Assert.True(active.Finished);
    }

    [Fact]
    public void Finished_TruePastEnd()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        active.Step(5000);
        Assert.True(active.Finished);
    }

    [Fact]
    public void Finished_FalsePartway()
    {
        var active = new ActiveSpline(MakeLinearSpline(0, 10, 1000));

        active.Step(500);
        Assert.False(active.Finished);
    }

    [Fact]
    public void Finished_SinglePoint_Immediately()
    {
        var points = new List<Position> { new(0, 0, 0) };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 1000);
        var active = new ActiveSpline(spline);

        active.Step(0);
        Assert.True(active.Finished);
    }

    [Fact]
    public void Constructor_ServerStartInPast_BeginsMidSpline()
    {
        var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
        var spline = new Spline(1, 1, 1000, SplineFlags.None, points, 1000);
        var active = new ActiveSpline(spline, currentTimeMs: 1500);

        var pos = active.Step(0);

        Assert.Equal(5f, pos.X, 0.01f);
    }

    [Fact]
    public void Constructor_HugeClockSkew_IgnoresSeedAndStartsAtOrigin()
    {
        var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
        var spline = new Spline(1, 1, 1000, SplineFlags.None, points, 1000);
        var active = new ActiveSpline(spline, currentTimeMs: 1_000_000);

        var initial = active.Step(0);
        var mid = active.Step(500);

        Assert.Equal(0f, initial.X, 0.01f);
        Assert.Equal(5f, mid.X, 0.01f);
        Assert.False(active.Finished);
    }

    [Fact]
    public void Step_CyclicSpline_AtExactDuration_StaysOnLastPointBeforeWrap()
    {
        var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
        var spline = new Spline(1, 1, 0, SplineFlags.Cyclic, points, 1000);
        var active = new ActiveSpline(spline);

        var atBoundary = active.Step(1000);
        var afterWrap = active.Step(1);

        Assert.Equal(10f, atBoundary.X, 0.01f);
        Assert.InRange(afterWrap.X, 0f, 0.02f);
    }

    [Fact]
    public void Step_CyclicFlyingSpline_UsesWrappedNeighborOnFirstSegment()
    {
        var active = new ActiveSpline(MakeCyclicFlyingSpline());

        var pos = active.Step(500);
        var expected = CatmullRomExpected(
            new Position(10, 10, 0),
            new Position(0, 0, 0),
            new Position(10, 0, 0),
            new Position(10, 10, 0),
            0.5f);

        Assert.Equal(expected.X, pos.X, 3);
        Assert.Equal(expected.Y, pos.Y, 3);
        Assert.Equal(expected.Z, pos.Z, 3);
    }

    [Fact]
    public void Step_CyclicFlyingSpline_UsesWrappedNeighborOnClosingSegment()
    {
        var active = new ActiveSpline(MakeCyclicFlyingSpline());

        var pos = active.Step(2500);
        var expected = CatmullRomExpected(
            new Position(10, 0, 0),
            new Position(10, 10, 0),
            new Position(0, 0, 0),
            new Position(10, 0, 0),
            0.5f);

        Assert.Equal(expected.X, pos.X, 3);
        Assert.Equal(expected.Y, pos.Y, 3);
        Assert.Equal(expected.Z, pos.Z, 3);
    }
}

public class SplineSegmentMsTests
{
    [Fact]
    public void SegmentMs_TwoPoints()
    {
        var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 1000);

        Assert.Equal(1000f, spline.SegmentMs);
    }

    [Fact]
    public void SegmentMs_ThreePoints()
    {
        var points = new List<Position> { new(0, 0, 0), new(10, 0, 0), new(20, 0, 0) };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 2000);

        Assert.Equal(1000f, spline.SegmentMs);
    }

    [Fact]
    public void SegmentMs_SinglePoint_Zero()
    {
        var points = new List<Position> { new(0, 0, 0) };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 1000);

        Assert.Equal(0f, spline.SegmentMs);
    }

    [Fact]
    public void SegmentMs_ManyPoints()
    {
        var points = new List<Position>
        {
            new(0, 0, 0), new(10, 0, 0), new(20, 0, 0),
            new(30, 0, 0), new(40, 0, 0), new(50, 0, 0)
        };
        var spline = new Spline(1, 1, 0, SplineFlags.None, points, 5000);

        // 5 segments, 5000ms → 1000ms per segment
        Assert.Equal(1000f, spline.SegmentMs);
    }
}

[Collection("Sequential ObjectManager tests")]
public class SplineFacingTests(ObjectManagerFixture fixture) : IClassFixture<ObjectManagerFixture>
{
    [Fact]
    public void ResolveFacing_NormalUsesMovementDirection()
    {
        var unit = new WoWUnit(new HighGuid(1))
        {
            SplineType = SplineType.Normal,
            Facing = 0f
        };

        float facing = SplineController.ResolveFacing(unit, new Position(0, 0, 0), new Position(0, 10, 0));

        Assert.Equal(MathF.PI / 2f, facing, 3);
    }

    [Fact]
    public void ResolveFacing_FacingAngleUsesExplicitAngle()
    {
        var unit = new WoWUnit(new HighGuid(1))
        {
            SplineType = SplineType.FacingAngle,
            FacingAngle = -0.75f
        };

        float facing = SplineController.ResolveFacing(unit, new Position(0, 0, 0), new Position(5, 0, 0));

        Assert.Equal(TransportCoordinateHelper.NormalizeFacing(-0.75f), facing, 3);
    }

    [Fact]
    public void ResolveFacing_FacingSpotUsesTargetPoint()
    {
        var unit = new WoWUnit(new HighGuid(1))
        {
            SplineType = SplineType.FacingSpot,
            FacingSpot = new Position(10f, 10f, 0f),
            Facing = 0f
        };

        float facing = SplineController.ResolveFacing(unit, new Position(0f, 0f, 0f), new Position(10f, 0f, 0f));

        Assert.Equal(MathF.PI / 2f, facing, 3);
    }

    [Fact]
    public void ResolveFacing_FacingTargetUsesTrackedTargetPosition()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x11;
        const ulong targetGuid = 0xF130000000000222ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            targetGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            new MovementInfoUpdate
            {
                X = 5f,
                Y = 10f,
                Z = 0f,
                Facing = 0f
            },
            []));
        UpdateProcessingHelper.DrainPendingUpdates();

        var target = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(targetGuid));
        target.Position = new Position(5f, 10f, 0f);

        var unit = new WoWUnit(new HighGuid(2))
        {
            SplineType = SplineType.FacingTarget,
            SplineTargetGuid = targetGuid,
            Facing = 0f,
            ObjectManager = objectManager
        };

        float facing = SplineController.ResolveFacing(unit, new Position(5f, 0f, 0f), new Position(5f, 5f, 0f));

        Assert.Equal(MathF.PI / 2f, facing, 3);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            fixture._woWClient.Object,
            fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance);
    }
}
