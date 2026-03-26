using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorCandidateQuadPlaneRecordTests
{
    [Fact]
    public void BuildSelectorCandidateQuadPlaneRecord_BuildsFourOrientedEdgePlanesAndAnchoredSourcePlane()
    {
        Vector3[] points = new Vector3[9];
        points[0] = new Vector3(0f, 0f, 0f);
        points[1] = new Vector3(1f, 0f, 0f);
        points[2] = new Vector3(1f, 1f, 0f);
        points[3] = new Vector3(0f, 1f, 0f);
        byte[] selectorIndices = [0, 1, 2, 3];
        var translation = new Vector3(0f, 0f, 1f);
        var sourcePlane = new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = 42f
        };

        SelectorSupportPlane[] outPlanes = new SelectorSupportPlane[5];
        bool ok = BuildWoWSelectorCandidateQuadPlaneRecord(
            points,
            points.Length,
            selectorIndices,
            selectorIndices.Length,
            translation,
            sourcePlane,
            outPlanes,
            outPlanes.Length);

        Assert.True(ok);

        for (int i = 0; i < 4; ++i)
        {
            Vector3 pointA = points[selectorIndices[i]];
            Vector3 pointB = points[selectorIndices[(i + 1) & 3]];
            Vector3 previousPoint = points[selectorIndices[(i + 3) & 3]];
            Vector3 translatedPoint = pointA + translation;

            AssertPlaneTouchesPoint(outPlanes[i], pointA);
            AssertPlaneTouchesPoint(outPlanes[i], pointB);
            AssertPlaneTouchesPoint(outPlanes[i], translatedPoint);

            float previousPointEval = EvaluatePlane(outPlanes[i], previousPoint);
            Assert.True(previousPointEval <= 1e-5f,
                $"Expected previous selector point to stay on the non-positive side of plane {i}, got {previousPointEval:F6}.");
        }

        Assert.Equal(sourcePlane.Normal.X, outPlanes[4].Normal.X, 6);
        Assert.Equal(sourcePlane.Normal.Y, outPlanes[4].Normal.Y, 6);
        Assert.Equal(sourcePlane.Normal.Z, outPlanes[4].Normal.Z, 6);
        Assert.Equal(-1f, outPlanes[4].PlaneDistance, 6);
        AssertPlaneTouchesPoint(outPlanes[4], points[selectorIndices[0]] + translation);
    }

    [Fact]
    public void BuildSelectorCandidateQuadPlaneRecord_FailsWhenTranslatedEdgePlaneDegenerates()
    {
        Vector3[] points = new Vector3[9];
        points[0] = new Vector3(0f, 0f, 0f);
        points[1] = new Vector3(1f, 0f, 0f);
        points[2] = new Vector3(1f, 1f, 0f);
        points[3] = new Vector3(0f, 1f, 0f);
        byte[] selectorIndices = [0, 1, 2, 3];
        var translation = new Vector3(1f, 0f, 0f);
        var sourcePlane = new SelectorSupportPlane
        {
            Normal = new Vector3(0f, 0f, 1f),
            PlaneDistance = 0f
        };

        SelectorSupportPlane[] outPlanes = new SelectorSupportPlane[5];
        bool ok = BuildWoWSelectorCandidateQuadPlaneRecord(
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
