using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceRankingTests
{
    [Fact]
    public void EvaluateSelectorTriangleSourceRanking_DotRejectedSourcesLeaveBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateWorkingSelectorPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0f, 0f, 0f);
        for (int i = 5; i < 9; ++i)
        {
            supportPlanes[i].Normal = new Vector3(-1f, 0f, 0f);
        }

        byte[] selectorIndices = CreateTriangleSelectorTable(0, 1, 2);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorTriangleSourceRanking(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            candidateDirection: new Vector3(-1f, 0f, 0.5f),
            points,
            points.Length,
            supportPlanes,
            supportPlanes.Length,
            selectorIndices,
            selectorIndices.Length,
            bestCandidates,
            bestCandidates.Length,
            ref candidateCount,
            ref bestRatio,
            out SelectorSourceRankingTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(0, candidateCount);
        Assert.Equal(4u, trace.DotRejectedCount);
        Assert.Equal(0u, trace.BuilderRejectedCount);
        Assert.Equal(0u, trace.EvaluatorRejectedCount);
        Assert.Equal(0u, trace.AcceptedSourceCount);
        Assert.Equal(0u, trace.FinalCandidateCount);
        Assert.Equal(uint.MaxValue, trace.SelectedSourceIndex);
    }

    [Fact]
    public void EvaluateSelectorTriangleSourceRanking_BuilderRejectLeavesBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateBuilderRejectSelectorPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0f, 0f, 0f);
        for (int i = 6; i < 9; ++i)
        {
            supportPlanes[i].Normal = new Vector3(-1f, 0f, 0f);
        }

        byte[] selectorIndices = CreateBuilderRejectSelectorTable();
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorTriangleSourceRanking(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            candidateDirection: new Vector3(-1f, 0f, 0f),
            points,
            points.Length,
            supportPlanes,
            supportPlanes.Length,
            selectorIndices,
            selectorIndices.Length,
            bestCandidates,
            bestCandidates.Length,
            ref candidateCount,
            ref bestRatio,
            out SelectorSourceRankingTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(0, candidateCount);
        Assert.Equal(3u, trace.DotRejectedCount);
        Assert.Equal(1u, trace.BuilderRejectedCount);
        Assert.Equal(0u, trace.AcceptedSourceCount);
        Assert.Equal(uint.MaxValue, trace.SelectedSourceIndex);
    }

    [Fact]
    public void EvaluateSelectorTriangleSourceRanking_EvaluatorRejectLeavesBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(1f, 0f, 0f))];
        Vector3[] points = CreateWorkingSelectorPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0f, 0f, 0f);
        byte[] selectorIndices = CreateTriangleSelectorTable(0, 1, 2);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorTriangleSourceRanking(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            candidateDirection: new Vector3(-1f, 0f, 0.5f),
            points,
            points.Length,
            supportPlanes,
            supportPlanes.Length,
            selectorIndices,
            selectorIndices.Length,
            bestCandidates,
            bestCandidates.Length,
            ref candidateCount,
            ref bestRatio,
            out SelectorSourceRankingTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(0, candidateCount);
        Assert.Equal(0u, trace.DotRejectedCount);
        Assert.Equal(0u, trace.BuilderRejectedCount);
        Assert.Equal(4u, trace.EvaluatorRejectedCount);
        Assert.Equal(0u, trace.AcceptedSourceCount);
        Assert.Equal(uint.MaxValue, trace.SelectedSourceIndex);
    }

    [Fact]
    public void EvaluateSelectorTriangleSourceRanking_OverwritePathReplacesBestCandidate()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateWorkingSelectorPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0.5f, 0.6f, 0.7f);
        byte[] selectorIndices = CreateTriangleSelectorTable(0, 1, 2);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorTriangleSourceRanking(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            candidateDirection: new Vector3(-1f, 0f, 0.5f),
            points,
            points.Length,
            supportPlanes,
            supportPlanes.Length,
            selectorIndices,
            selectorIndices.Length,
            bestCandidates,
            bestCandidates.Length,
            ref candidateCount,
            ref bestRatio,
            out SelectorSourceRankingTrace trace);

        Assert.True(accepted);
        Assert.Equal(0.2f, bestRatio, 6);
        Assert.Equal(1, candidateCount);
        Assert.Equal(1u, trace.AcceptedSourceCount);
        Assert.Equal(1u, trace.OverwriteCount);
        Assert.Equal(0u, trace.AppendCount);
        Assert.Equal(1u, trace.BestRatioUpdatedCount);
        Assert.Equal(0u, trace.SwappedBestToFront);
        Assert.Equal(1u, trace.FinalCandidateCount);
        Assert.Equal(0u, trace.SelectedSourceIndex);
        Assert.Equal(0f, bestCandidates[0].PlaneDistance, 6);
        AssertVector(bestCandidates[0].Normal, 1f, 0f, 0f);
    }

    [Fact]
    public void EvaluateSelectorTriangleSourceRanking_AppendAndSwapPromotesNewestNearTieBest()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateWorkingSelectorPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, -0.0005f, 0.6f, 0.7f);
        byte[] selectorIndices = CreateTriangleSelectorTable(0, 1, 2);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorTriangleSourceRanking(
            records,
            records.Length,
            testPoint: new Vector3(1f, 0f, 0f),
            candidateDirection: new Vector3(-1f, 0f, 0.5f),
            points,
            points.Length,
            supportPlanes,
            supportPlanes.Length,
            selectorIndices,
            selectorIndices.Length,
            bestCandidates,
            bestCandidates.Length,
            ref candidateCount,
            ref bestRatio,
            out SelectorSourceRankingTrace trace);

        Assert.True(accepted);
        Assert.Equal(0.1995f, bestRatio, 6);
        Assert.Equal(2, candidateCount);
        Assert.Equal(2u, trace.AcceptedSourceCount);
        Assert.Equal(1u, trace.OverwriteCount);
        Assert.Equal(1u, trace.AppendCount);
        Assert.Equal(2u, trace.BestRatioUpdatedCount);
        Assert.Equal(1u, trace.SwappedBestToFront);
        Assert.Equal(2u, trace.FinalCandidateCount);
        Assert.Equal(1u, trace.SelectedSourceIndex);
        Assert.Equal(-0.0005f, bestCandidates[0].PlaneDistance, 6);
        Assert.Equal(0f, bestCandidates[1].PlaneDistance, 6);
    }

    private static SelectorCandidateRecord CreateRecord(Vector3 filterNormal) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = filterNormal,
                PlaneDistance = 0f
            },
            Point0 = new Vector3(0.2f, 0f, 0f),
            Point1 = new Vector3(0.5f, 1f, 0f),
            Point2 = new Vector3(0.9f, -1f, 0f)
        };

    private static Vector3[] CreateWorkingSelectorPoints()
    {
        Vector3[] points = new Vector3[9];
        points[0] = new Vector3(0.2f, 0f, 0f);
        points[1] = new Vector3(0.5f, 1f, 0f);
        points[2] = new Vector3(0.9f, -1f, 0f);
        points[3] = new Vector3(1.5f, 0f, 0f);
        points[4] = new Vector3(1.8f, 1f, 0f);
        points[5] = new Vector3(2.1f, -1f, 0f);
        points[6] = new Vector3(-1f, 0f, 0f);
        points[7] = new Vector3(-1f, 1f, 0f);
        points[8] = new Vector3(-1f, -1f, 0f);
        return points;
    }

    private static Vector3[] CreateBuilderRejectSelectorPoints()
    {
        Vector3[] points = CreateWorkingSelectorPoints();
        points[3] = new Vector3(0f, 0f, 0f);
        points[4] = new Vector3(1f, 0f, 0f);
        points[5] = new Vector3(2f, 0f, 0f);
        return points;
    }

    private static SelectorSupportPlane[] CreateSupportPlanes(float plane5Distance, float plane6Distance, float plane7Distance, float plane8Distance)
    {
        SelectorSupportPlane[] planes = new SelectorSupportPlane[9];
        for (int i = 0; i < planes.Length; ++i)
        {
            planes[i] = new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, 1f),
                PlaneDistance = -1000f
            };
        }

        planes[5] = CreateXPlane(plane5Distance);
        planes[6] = CreateXPlane(plane6Distance);
        planes[7] = CreateXPlane(plane7Distance);
        planes[8] = CreateXPlane(plane8Distance);
        return planes;
    }

    private static SelectorSupportPlane CreateXPlane(float planeDistance) =>
        new()
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = planeDistance
        };

    private static byte[] CreateTriangleSelectorTable(byte a, byte b, byte c)
    {
        byte[] selectorIndices = new byte[32];
        for (int sourceIndex = 0; sourceIndex < 4; ++sourceIndex)
        {
            int offset = 20 + (sourceIndex * 3);
            selectorIndices[offset] = a;
            selectorIndices[offset + 1] = b;
            selectorIndices[offset + 2] = c;
        }

        return selectorIndices;
    }

    private static byte[] CreateBuilderRejectSelectorTable()
    {
        byte[] selectorIndices = CreateTriangleSelectorTable(0, 1, 2);
        selectorIndices[20] = 3;
        selectorIndices[21] = 4;
        selectorIndices[22] = 5;
        return selectorIndices;
    }

    private static void AssertVector(Vector3 actual, float x, float y, float z)
    {
        Assert.True(MathF.Abs(actual.X - x) <= 1e-5f, $"Expected X={x:F6}, got {actual.X:F6}.");
        Assert.True(MathF.Abs(actual.Y - y) <= 1e-5f, $"Expected Y={y:F6}, got {actual.Y:F6}.");
        Assert.True(MathF.Abs(actual.Z - z) <= 1e-5f, $"Expected Z={z:F6}, got {actual.Z:F6}.");
    }
}
