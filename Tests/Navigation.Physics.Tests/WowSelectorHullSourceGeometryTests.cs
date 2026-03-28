using Xunit;

using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorHullSourceGeometryTests
{
    [Fact]
    public void BuildWoWSelectorHullSourceGeometry_BuildsCanonicalCubeFacePlanesAndZeroAnchors()
    {
        Vector3[] supportPoints =
        [
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(1.0f, 0.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(0.0f, 1.0f, 1.0f),
        ];
        SelectorSupportPlane[] planes = new SelectorSupportPlane[6];
        Vector3[] points = new Vector3[8];

        Assert.True(BuildWoWSelectorHullSourceGeometry(
            supportPoints,
            supportPoints.Length,
            planes,
            planes.Length,
            points,
            points.Length,
            out Vector3 anchorPoint0,
            out Vector3 anchorPoint1));

        Assert.Equal(supportPoints, points);
        AssertVectorEqual(new Vector3(0.0f, 0.0f, 0.0f), anchorPoint0);
        AssertVectorEqual(new Vector3(0.0f, 0.0f, 0.0f), anchorPoint1);

        AssertPlaneEqual(new Vector3(1.0f, 0.0f, 0.0f), -1.0f, planes[0]);
        AssertPlaneEqual(new Vector3(1.0f, 0.0f, 0.0f), 0.0f, planes[1]);
        AssertPlaneEqual(new Vector3(0.0f, 1.0f, 0.0f), 0.0f, planes[2]);
        AssertPlaneEqual(new Vector3(0.0f, -1.0f, 0.0f), 1.0f, planes[3]);
        AssertPlaneEqual(new Vector3(0.0f, 0.0f, -1.0f), 1.0f, planes[4]);
        AssertPlaneEqual(new Vector3(0.0f, 0.0f, 1.0f), 0.0f, planes[5]);
    }

    private static void AssertPlaneEqual(Vector3 expectedNormal, float expectedDistance, SelectorSupportPlane actual)
    {
        AssertVectorEqual(expectedNormal, actual.Normal);
        Assert.Equal(expectedDistance, actual.PlaneDistance, 6);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
