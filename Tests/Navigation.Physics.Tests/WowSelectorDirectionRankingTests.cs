using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorDirectionRankingTests
{
    [Fact]
    public void EvaluateSelectorDirectionRanking_DotRejectedDirectionsLeaveBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateWorkingQuadPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0f, 0f, 0f, 0f);
        for (int i = 0; i < 5; ++i)
        {
            supportPlanes[i].Normal = new Vector3(-1f, 0f, 0f);
        }

        byte[] selectorIndices = CreateDirectionSelectorTable(0, 1, 2, 3);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        int bestRecordIndex = -1;
        float bestRatio = 1f;
        float reportedBestRatio = 1f;

        bool accepted = EvaluateWoWSelectorDirectionRanking(
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
            ref reportedBestRatio,
            ref bestRecordIndex,
            out SelectorDirectionRankingTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(1f, reportedBestRatio, 6);
        Assert.Equal(0, candidateCount);
        Assert.Equal(-1, bestRecordIndex);
        Assert.Equal(5u, trace.DotRejectedCount);
        Assert.Equal(0u, trace.BuilderRejectedCount);
        Assert.Equal(0u, trace.EvaluatorRejectedCount);
        Assert.Equal(0u, trace.AcceptedDirectionCount);
        Assert.Equal(uint.MaxValue, trace.SelectedDirectionIndex);
    }

    [Fact]
    public void EvaluateSelectorDirectionRanking_BuilderRejectLeavesBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateBuilderRejectQuadPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0f, 0f, 0f, 0f);
        for (int i = 1; i < 5; ++i)
        {
            supportPlanes[i].Normal = new Vector3(-1f, 0f, 0f);
        }

        byte[] selectorIndices = CreateBuilderRejectDirectionSelectorTable();
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        int bestRecordIndex = -1;
        float bestRatio = 1f;
        float reportedBestRatio = 1f;

        bool accepted = EvaluateWoWSelectorDirectionRanking(
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
            ref reportedBestRatio,
            ref bestRecordIndex,
            out SelectorDirectionRankingTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(1f, reportedBestRatio, 6);
        Assert.Equal(0, candidateCount);
        Assert.Equal(-1, bestRecordIndex);
        Assert.Equal(4u, trace.DotRejectedCount);
        Assert.Equal(1u, trace.BuilderRejectedCount);
        Assert.Equal(0u, trace.AcceptedDirectionCount);
    }

    [Fact]
    public void EvaluateSelectorDirectionRanking_EvaluatorRejectLeavesBestRatioUnchanged()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(1f, 0f, 0f))];
        Vector3[] points = CreateWorkingQuadPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, 0f, 0f, 0f, 0f);
        byte[] selectorIndices = CreateDirectionSelectorTable(0, 1, 2, 3);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        int bestRecordIndex = -1;
        float bestRatio = 1f;
        float reportedBestRatio = 1f;

        bool accepted = EvaluateWoWSelectorDirectionRanking(
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
            ref reportedBestRatio,
            ref bestRecordIndex,
            out SelectorDirectionRankingTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(1f, reportedBestRatio, 6);
        Assert.Equal(0, candidateCount);
        Assert.Equal(-1, bestRecordIndex);
        Assert.Equal(0u, trace.DotRejectedCount);
        Assert.Equal(0u, trace.BuilderRejectedCount);
        Assert.Equal(5u, trace.EvaluatorRejectedCount);
        Assert.Equal(0u, trace.AcceptedDirectionCount);
    }

    [Fact]
    public void EvaluateSelectorDirectionRanking_AppendAndSwapPromotesNewestNearTieBest()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateWorkingQuadPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(0f, -0.0005f, 0.6f, 0.7f, 0.8f);
        byte[] selectorIndices = CreateDirectionSelectorTable(0, 1, 2, 3);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        int bestRecordIndex = -1;
        float bestRatio = 1f;
        float reportedBestRatio = 1f;

        bool accepted = EvaluateWoWSelectorDirectionRanking(
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
            ref reportedBestRatio,
            ref bestRecordIndex,
            out SelectorDirectionRankingTrace trace);

        Assert.True(accepted);
        Assert.Equal(0.1995f, bestRatio, 6);
        Assert.Equal(0.1995f, reportedBestRatio, 6);
        Assert.Equal(2, candidateCount);
        Assert.Equal(0, bestRecordIndex);
        Assert.Equal(2u, trace.AcceptedDirectionCount);
        Assert.Equal(1u, trace.OverwriteCount);
        Assert.Equal(1u, trace.AppendCount);
        Assert.Equal(2u, trace.BestRatioUpdatedCount);
        Assert.Equal(1u, trace.SwappedBestToFront);
        Assert.Equal(2u, trace.FinalCandidateCount);
        Assert.Equal(1u, trace.SelectedDirectionIndex);
        Assert.Equal(0u, trace.SelectedRecordIndex);
        Assert.Equal(-0.0005f, bestCandidates[0].PlaneDistance, 6);
        Assert.Equal(0f, bestCandidates[1].PlaneDistance, 6);
    }

    [Fact]
    public void EvaluateSelectorDirectionRanking_FinalZeroClampMatchesBinaryEpsilonGate()
    {
        SelectorCandidateRecord[] records = [CreateRecord(new Vector3(-1f, 0f, 0f))];
        Vector3[] points = CreateWorkingQuadPoints();
        SelectorSupportPlane[] supportPlanes = CreateSupportPlanes(-0.199f, 0.6f, 0.7f, 0.8f, 0.9f);
        byte[] selectorIndices = CreateDirectionSelectorTable(0, 1, 2, 3);
        SelectorSupportPlane[] bestCandidates = new SelectorSupportPlane[5];
        int candidateCount = 0;
        int bestRecordIndex = -1;
        float bestRatio = 1f;
        float reportedBestRatio = 1f;

        bool accepted = EvaluateWoWSelectorDirectionRanking(
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
            ref reportedBestRatio,
            ref bestRecordIndex,
            out SelectorDirectionRankingTrace trace);

        Assert.True(accepted);
        Assert.Equal(0.001f, bestRatio, 6);
        Assert.Equal(0f, reportedBestRatio, 6);
        Assert.Equal(1, candidateCount);
        Assert.Equal(0, bestRecordIndex);
        Assert.Equal(1u, trace.ZeroClampedOutput);
        Assert.Equal(0f, trace.ReportedBestRatio, 6);
        Assert.Equal(0u, trace.SelectedDirectionIndex);
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
            Point1 = new Vector3(0.6f, 0.2f, 0f),
            Point2 = new Vector3(0.5f, -0.2f, 0f)
        };

    private static Vector3[] CreateWorkingQuadPoints()
    {
        Vector3[] points = new Vector3[9];
        points[0] = new Vector3(0.1f, -0.5f, 0f);
        points[1] = new Vector3(0.1f, 0.5f, 0f);
        points[2] = new Vector3(1.0f, 0.5f, 0f);
        points[3] = new Vector3(1.0f, -0.5f, 0f);
        points[4] = new Vector3(0f, 0f, 0f);
        points[5] = new Vector3(0f, 0f, 0f);
        points[6] = new Vector3(0f, 0f, 0f);
        points[7] = new Vector3(0f, 0f, 0f);
        points[8] = new Vector3(0f, 0f, 0f);
        return points;
    }

    private static Vector3[] CreateBuilderRejectQuadPoints()
    {
        Vector3[] points = CreateWorkingQuadPoints();
        points[4] = new Vector3(0f, 0f, 0f);
        points[5] = new Vector3(1f, 0f, 0f);
        points[6] = new Vector3(2f, 0f, 0f);
        points[7] = new Vector3(3f, 0f, 0f);
        return points;
    }

    private static SelectorSupportPlane[] CreateSupportPlanes(float plane0Distance, float plane1Distance, float plane2Distance, float plane3Distance, float plane4Distance)
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

        planes[0] = CreateXPlane(plane0Distance);
        planes[1] = CreateXPlane(plane1Distance);
        planes[2] = CreateXPlane(plane2Distance);
        planes[3] = CreateXPlane(plane3Distance);
        planes[4] = CreateXPlane(plane4Distance);
        return planes;
    }

    private static SelectorSupportPlane CreateXPlane(float planeDistance) =>
        new()
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = planeDistance
        };

    private static byte[] CreateDirectionSelectorTable(byte a, byte b, byte c, byte d)
    {
        byte[] selectorIndices = new byte[32];
        for (int directionIndex = 0; directionIndex < 5; ++directionIndex)
        {
            int offset = directionIndex * 4;
            selectorIndices[offset] = a;
            selectorIndices[offset + 1] = b;
            selectorIndices[offset + 2] = c;
            selectorIndices[offset + 3] = d;
        }

        return selectorIndices;
    }

    private static byte[] CreateBuilderRejectDirectionSelectorTable()
    {
        byte[] selectorIndices = CreateDirectionSelectorTable(0, 1, 2, 3);
        selectorIndices[0] = 4;
        selectorIndices[1] = 5;
        selectorIndices[2] = 6;
        selectorIndices[3] = 7;
        return selectorIndices;
    }
}
