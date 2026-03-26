using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorCandidateValidationTests
{
    [Fact]
    public void EvaluateSelectorPlaneRatio_MatchesBinaryFormula()
    {
        var plane = new SelectorSupportPlane
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = -2f
        };

        float ratio = EvaluateWoWSelectorPlaneRatio(
            new Vector3(5f, 0f, 0f),
            plane,
            new Vector3(2f, 0f, 0f));

        Assert.Equal(1.5f, ratio, 6);
    }

    [Fact]
    public void EvaluateSelectorPlaneRatio_ReturnsZeroWhenDenominatorFallsUnderBinaryEpsilon()
    {
        var plane = new SelectorSupportPlane
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = 0f
        };

        float ratio = EvaluateWoWSelectorPlaneRatio(
            new Vector3(5f, 0f, 0f),
            plane,
            new Vector3(0f, 0f, 0f));

        Assert.Equal(0f, ratio, 6);
    }

    [Fact]
    public void ClipSelectorPointStripAgainstPlane_ClipsPolygonAndTagsIntersectionsWithPlaneIndex()
    {
        var plane = new SelectorSupportPlane
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = 0f
        };

        Vector3[] points = new Vector3[15];
        uint[] sourceIndices = new uint[15];
        points[0] = new Vector3(-1f, -1f, 0f);
        points[1] = new Vector3(1f, -1f, 0f);
        points[2] = new Vector3(1f, 1f, 0f);
        points[3] = new Vector3(-1f, 1f, 0f);
        sourceIndices[0] = 10u;
        sourceIndices[1] = 11u;
        sourceIndices[2] = 12u;
        sourceIndices[3] = 13u;
        int count = 4;

        bool ok = ClipWoWSelectorPointStripAgainstPlane(
            plane,
            clipPlaneIndex: 7u,
            points,
            sourceIndices,
            points.Length,
            ref count);

        Assert.True(ok);
        Assert.Equal(4, count);
        AssertVector(points[0], -1f, -1f, 0f);
        AssertVector(points[1], 0f, -1f, 0f);
        AssertVector(points[2], 0f, 1f, 0f);
        AssertVector(points[3], -1f, 1f, 0f);
        Assert.Equal(10u, sourceIndices[0]);
        Assert.Equal(7u, sourceIndices[1]);
        Assert.Equal(7u, sourceIndices[2]);
        Assert.Equal(13u, sourceIndices[3]);
    }

    [Fact]
    public void ValidateSelectorCandidate_FirstPassFailureUpdatesBestRatioWithoutRebuild()
    {
        SelectorSupportPlane[] planes = BuildPermissivePlaneSet();
        Vector3[] points = new Vector3[15];
        uint[] sourceIndices = new uint[15];
        points[0] = new Vector3(0f, 0f, 0f);
        points[1] = new Vector3(-2f, 1f, 0f);
        points[2] = new Vector3(-2f, -1f, 0f);
        sourceIndices[0] = 1u;
        sourceIndices[1] = 2u;
        sourceIndices[2] = 3u;
        int count = 3;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorCandidateValidation(
            planes,
            planes.Length,
            planeIndex: 0,
            testPoint: new Vector3(1f, 0f, 0f),
            ioPoints: points,
            ioSourceIndices: sourceIndices,
            maxCapacity: points.Length,
            ioCount: ref count,
            inOutBestRatio: ref bestRatio,
            trace: out SelectorCandidateValidationTrace trace);

        Assert.True(accepted);
        Assert.Equal(0f, bestRatio, 6);
        Assert.Equal(0u, trace.FirstPassAllBelowLooseThreshold);
        Assert.Equal(0u, trace.RebuildExecuted);
        Assert.Equal(0u, trace.RebuildSucceeded);
        Assert.Equal(0u, trace.SecondPassAllBelowStrictThreshold);
        Assert.Equal(1u, trace.ImprovedBestRatio);
        Assert.Equal(3u, trace.FinalStripCount);
        Assert.Equal(0f, trace.CandidateBestRatio, 6);
        Assert.Equal(0f, trace.OutputBestRatio, 6);
    }

    [Fact]
    public void ValidateSelectorCandidate_StrictPassKeepsBestRatioUnchangedAndRejectsCandidate()
    {
        SelectorSupportPlane[] planes = BuildPermissivePlaneSet();
        Vector3[] points = new Vector3[15];
        uint[] sourceIndices = new uint[15];
        points[0] = new Vector3(-2f, 0f, 0f);
        points[1] = new Vector3(-3f, 1f, 0f);
        points[2] = new Vector3(-2f, 2f, 0f);
        sourceIndices[0] = 10u;
        sourceIndices[1] = 11u;
        sourceIndices[2] = 12u;
        int count = 3;
        float bestRatio = 1f;

        bool accepted = EvaluateWoWSelectorCandidateValidation(
            planes,
            planes.Length,
            planeIndex: 0,
            testPoint: new Vector3(1f, 0f, 0f),
            ioPoints: points,
            ioSourceIndices: sourceIndices,
            maxCapacity: points.Length,
            ioCount: ref count,
            inOutBestRatio: ref bestRatio,
            trace: out SelectorCandidateValidationTrace trace);

        Assert.False(accepted);
        Assert.Equal(1f, bestRatio, 6);
        Assert.Equal(1u, trace.FirstPassAllBelowLooseThreshold);
        Assert.Equal(1u, trace.RebuildExecuted);
        Assert.Equal(1u, trace.RebuildSucceeded);
        Assert.Equal(1u, trace.SecondPassAllBelowStrictThreshold);
        Assert.Equal(0u, trace.ImprovedBestRatio);
        Assert.True(trace.FinalStripCount >= 3u);
        Assert.Equal(0f, trace.CandidateBestRatio, 6);
        Assert.Equal(1f, trace.OutputBestRatio, 6);
    }

    private static SelectorSupportPlane[] BuildPermissivePlaneSet()
    {
        var planes = new SelectorSupportPlane[9];
        for (int i = 0; i < planes.Length; ++i)
        {
            planes[i] = new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, 1f),
                PlaneDistance = -1000f
            };
        }

        planes[0] = new SelectorSupportPlane
        {
            Normal = new Vector3(1f, 0f, 0f),
            PlaneDistance = 0f
        };
        return planes;
    }

    private static void AssertVector(Vector3 actual, float x, float y, float z)
    {
        Assert.True(MathF.Abs(actual.X - x) <= 1e-5f, $"Expected X={x:F6}, got {actual.X:F6}.");
        Assert.True(MathF.Abs(actual.Y - y) <= 1e-5f, $"Expected Y={y:F6}, got {actual.Y:F6}.");
        Assert.True(MathF.Abs(actual.Z - z) <= 1e-5f, $"Expected Z={z:F6}, got {actual.Z:F6}.");
    }
}
