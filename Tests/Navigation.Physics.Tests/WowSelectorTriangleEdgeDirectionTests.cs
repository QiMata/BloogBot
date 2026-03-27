using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorTriangleEdgeDirectionTests
{
    [Fact]
    public void BuildWoWSelectorTriangleEdgeDirection_MixedScorersCanFavorCrossRankedEdge()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.0f, 0.01f, 0.0f),
            new Vector3(-1.0f, 0.01f, 0.0f),
            new Vector3(0.0f, 10.0f, 10.0f));
        Vector3 intersectionPoint = new(0.0f, 0.0f, 0.0f);
        Vector3 lineDirection = new(1.0f, 0.0f, 0.0f);

        Assert.True(BuildWoWSelectorTriangleEdgeDirection(
            selectedRecord,
            intersectionPoint,
            lineDirection,
            out Vector3 outDirection,
            out SelectorTriangleEdgeDirectionTrace trace));

        Assert.Equal(1u, trace.SelectedEdgeIndex);
        Assert.Equal(0u, trace.ZeroLengthRejectedCount);
        Assert.Equal(1u, trace.PointToLineScoredCount);
        Assert.Equal(2u, trace.PlaneScoredCount);
        Assert.Equal(4.9800772e-05f, trace.BestScore, 8);
        Assert.Equal(-0.07056966f, outDirection.X, 6);
        Assert.Equal(-0.7049909f, outDirection.Y, 6);
        Assert.Equal(-0.70569664f, outDirection.Z, 6);
    }

    [Fact]
    public void BuildWoWSelectorTriangleEdgeDirection_SkipsZeroLengthEdgeBeforeRankingRemainingEdges()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f));
        Vector3 intersectionPoint = new(-2.0f, -2.0f, -2.0f);
        Vector3 lineDirection = new(1.0f, 0.0f, 0.0f);

        Assert.True(BuildWoWSelectorTriangleEdgeDirection(
            selectedRecord,
            intersectionPoint,
            lineDirection,
            out Vector3 outDirection,
            out SelectorTriangleEdgeDirectionTrace trace));

        Assert.Equal(1u, trace.SelectedEdgeIndex);
        Assert.Equal(1u, trace.ZeroLengthRejectedCount);
        Assert.Equal(1u, trace.PointToLineScoredCount);
        Assert.Equal(1u, trace.PlaneScoredCount);
        Assert.Equal(0.0f, trace.BestScore, 6);
        Assert.Equal(-1.0f, outDirection.X, 6);
        Assert.Equal(0.0f, outDirection.Y, 6);
        Assert.Equal(0.0f, outDirection.Z, 6);
    }

    [Fact]
    public void BuildWoWSelectorTriangleEdgeDirection_AllZeroLengthEdgesLeaveDefaultOutput()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(3.0f, 4.0f, 5.0f),
            new Vector3(3.0f, 4.0f, 5.0f),
            new Vector3(3.0f, 4.0f, 5.0f));
        Vector3 intersectionPoint = new(0.0f, 0.0f, 0.0f);
        Vector3 lineDirection = new(1.0f, 0.0f, 0.0f);

        Assert.True(BuildWoWSelectorTriangleEdgeDirection(
            selectedRecord,
            intersectionPoint,
            lineDirection,
            out Vector3 outDirection,
            out SelectorTriangleEdgeDirectionTrace trace));

        Assert.Equal(uint.MaxValue, trace.SelectedEdgeIndex);
        Assert.Equal(3u, trace.ZeroLengthRejectedCount);
        Assert.Equal(0u, trace.PointToLineScoredCount);
        Assert.Equal(0u, trace.PlaneScoredCount);
        Assert.True(trace.BestScore > 3.0e38f);
        Assert.Equal(0.0f, outDirection.X, 6);
        Assert.Equal(0.0f, outDirection.Y, 6);
        Assert.Equal(0.0f, outDirection.Z, 6);
    }

    private static SelectorCandidateRecord CreateSelectedRecord(Vector3 point0, Vector3 point1, Vector3 point2) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = new Vector3(0.0f, 0.0f, 1.0f),
                PlaneDistance = 0.0f,
            },
            Point0 = point0,
            Point1 = point1,
            Point2 = point2,
        };
}
