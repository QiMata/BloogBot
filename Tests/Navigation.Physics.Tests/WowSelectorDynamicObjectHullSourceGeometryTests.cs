using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorDynamicObjectHullSourceGeometryTests
{
    [Fact]
    public void BuildWoWSelectorDynamicObjectHullSourceGeometry_ReturnsFalseWhenSourcePlanesCullTheObjectBounds()
    {
        SelectorSupportPlane[] sourcePlanes =
        [
            new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = -4.0f },
        ];

        Assert.False(BuildWoWSelectorDynamicObjectHullSourceGeometry(
            sourcePlanes,
            sourcePlanes.Length,
            objectBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            objectBoundsMax: new Vector3(3.0f, 1.0f, 1.0f),
            localSupportPoints: BuildUnitCubeSupportPoints(),
            supportPointCount: 8,
            basisRow0: new Vector3(1.0f, 0.0f, 0.0f),
            basisRow1: new Vector3(0.0f, 1.0f, 0.0f),
            basisRow2: new Vector3(0.0f, 0.0f, 1.0f),
            translation: new Vector3(0.0f, 0.0f, 0.0f),
            outPlanes: new SelectorSupportPlane[6],
            outPlaneCount: 6,
            outPoints: new Vector3[8],
            outPointCount: 8,
            out _,
            out _));
    }

    [Fact]
    public void BuildWoWSelectorDynamicObjectHullSourceGeometry_MatchesTheSequentialCullTransformAndHullBuildPath()
    {
        SelectorSupportPlane[] sourcePlanes =
        [
            new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = 0.0f },
            new SelectorSupportPlane { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 0.0f },
        ];
        Vector3[] localSupportPoints = BuildUnitCubeSupportPoints();
        Vector3 basisRow0 = new(0.0f, 1.0f, 0.0f);
        Vector3 basisRow1 = new(-1.0f, 0.0f, 0.0f);
        Vector3 basisRow2 = new(0.0f, 0.0f, 1.0f);
        Vector3 translation = new(10.0f, 20.0f, 30.0f);

        Vector3[] expectedTransformedPoints = new Vector3[8];
        Assert.True(TransformWoWSelectorSupportPointBuffer(
            localSupportPoints,
            localSupportPoints.Length,
            basisRow0,
            basisRow1,
            basisRow2,
            translation,
            expectedTransformedPoints,
            expectedTransformedPoints.Length));

        SelectorSupportPlane[] expectedPlanes = new SelectorSupportPlane[6];
        Vector3[] expectedPoints = new Vector3[8];
        Assert.True(BuildWoWSelectorHullSourceGeometry(
            expectedTransformedPoints,
            expectedTransformedPoints.Length,
            expectedPlanes,
            expectedPlanes.Length,
            expectedPoints,
            expectedPoints.Length,
            out Vector3 expectedAnchorPoint0,
            out Vector3 expectedAnchorPoint1));

        SelectorSupportPlane[] actualPlanes = new SelectorSupportPlane[6];
        Vector3[] actualPoints = new Vector3[8];
        Assert.True(BuildWoWSelectorDynamicObjectHullSourceGeometry(
            sourcePlanes,
            sourcePlanes.Length,
            objectBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            objectBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            localSupportPoints,
            localSupportPoints.Length,
            basisRow0,
            basisRow1,
            basisRow2,
            translation,
            actualPlanes,
            actualPlanes.Length,
            actualPoints,
            actualPoints.Length,
            out Vector3 actualAnchorPoint0,
            out Vector3 actualAnchorPoint1));

        Assert.Equal(expectedPoints.Length, actualPoints.Length);
        for (int i = 0; i < expectedPoints.Length; ++i) {
            AssertVectorEqual(expectedPoints[i], actualPoints[i]);
        }

        Assert.Equal(expectedPlanes.Length, actualPlanes.Length);
        for (int i = 0; i < expectedPlanes.Length; ++i) {
            AssertVectorEqual(expectedPlanes[i].Normal, actualPlanes[i].Normal);
            Assert.Equal(expectedPlanes[i].PlaneDistance, actualPlanes[i].PlaneDistance, 6);
        }
        AssertVectorEqual(expectedAnchorPoint0, actualAnchorPoint0);
        AssertVectorEqual(expectedAnchorPoint1, actualAnchorPoint1);
    }

    private static Vector3[] BuildUnitCubeSupportPoints() =>
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

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
