using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorCandidatePlaneRecordTests
{
    [Fact]
    public void BuildSelectorCandidatePlaneRecord_BuildsThreeOrientedEdgePlanesAndAnchoredSourcePlane()
    {
        Vector3[] points = new Vector3[9];
        points[0] = new Vector3(0f, 0f, 0f);
        points[1] = new Vector3(1f, 0f, 0f);
        points[2] = new Vector3(0f, 1f, 0f);
        byte[] selectorIndices = [0, 1, 2];
        var translation = new Vector3(0f, 0f, 1f);
        var sourcePlane = new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = 99f
        };

        SelectorSupportPlane[] outPlanes = new SelectorSupportPlane[4];
        bool ok = BuildWoWSelectorCandidatePlaneRecord(
            points,
            points.Length,
            selectorIndices,
            selectorIndices.Length,
            translation,
            sourcePlane,
            outPlanes,
            outPlanes.Length);

        Assert.True(ok);

        int[] secondPointOffsets = [1, 2, 0];
        int[] oppositePointOffsets = [2, 0, 1];

        for (int i = 0; i < 3; ++i)
        {
            Vector3 pointA = points[selectorIndices[i]];
            Vector3 pointB = points[selectorIndices[secondPointOffsets[i]]];
            Vector3 pointC = points[selectorIndices[oppositePointOffsets[i]]];
            Vector3 translatedPoint = pointA + translation;

            AssertPlaneTouchesPoint(outPlanes[i], pointA);
            AssertPlaneTouchesPoint(outPlanes[i], pointB);
            AssertPlaneTouchesPoint(outPlanes[i], translatedPoint);

            float oppositePointEval = EvaluatePlane(outPlanes[i], pointC);
            Assert.True(oppositePointEval <= 1e-5f,
                $"Expected opposite triangle point to stay on the non-positive side of plane {i}, got {oppositePointEval:F6}.");
        }

        Assert.Equal(sourcePlane.Normal.X, outPlanes[3].Normal.X, 6);
        Assert.Equal(sourcePlane.Normal.Y, outPlanes[3].Normal.Y, 6);
        Assert.Equal(sourcePlane.Normal.Z, outPlanes[3].Normal.Z, 6);
        Assert.Equal(-1f, outPlanes[3].PlaneDistance, 6);
        AssertPlaneTouchesPoint(outPlanes[3], points[selectorIndices[0]] + translation);
    }

    [Fact]
    public void BuildSelectorCandidatePlaneRecord_FailsWhenTranslatedTriangleBuildsDegenerateEdgePlane()
    {
        Vector3[] points = new Vector3[9];
        points[0] = new Vector3(0f, 0f, 0f);
        points[1] = new Vector3(1f, 0f, 0f);
        points[2] = new Vector3(0f, 1f, 0f);
        byte[] selectorIndices = [0, 1, 2];
        var translation = new Vector3(1f, 0f, 0f);
        var sourcePlane = new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = 0f
        };

        SelectorSupportPlane[] outPlanes = new SelectorSupportPlane[4];
        bool ok = BuildWoWSelectorCandidatePlaneRecord(
            points,
            points.Length,
            selectorIndices,
            selectorIndices.Length,
            translation,
            sourcePlane,
            outPlanes,
            outPlanes.Length);

        Assert.False(ok);
    }

    private static float EvaluatePlane(SelectorSupportPlane plane, Vector3 point) =>
        Vector3.Dot(plane.Normal, point) + plane.PlaneDistance;

    private static void AssertPlaneTouchesPoint(SelectorSupportPlane plane, Vector3 point)
    {
        float planeEval = EvaluatePlane(plane, point);
        Assert.True(MathF.Abs(planeEval) <= 1e-5f, $"Expected point {point} to stay on plane, got eval={planeEval:F6}.");
    }
}
