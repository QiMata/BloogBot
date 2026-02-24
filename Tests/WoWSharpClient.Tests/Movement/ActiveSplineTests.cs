using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Movement;

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
