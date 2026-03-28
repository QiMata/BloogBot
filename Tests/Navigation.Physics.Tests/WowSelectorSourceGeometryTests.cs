using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceGeometryTests
{
    [Fact]
    public void TranslateWoWSelectorSourceGeometry_TranslatesPointsAndReanchorsPlaneDistances()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = -10f,
            },
            new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, -1f),
                PlaneDistance = 5f,
            },
        ];
        Vector3[] points =
        [
            new Vector3(1f, 2f, 3f),
            new Vector3(4f, 5f, 6f),
        ];
        Vector3 anchor0 = new(7f, 8f, 9f);
        Vector3 anchor1 = new(-1f, -2f, -3f);
        Vector3 translation = new(2f, -3f, 4f);

        bool translated = TranslateWoWSelectorSourceGeometry(
            translation,
            planes,
            planes.Length,
            points,
            points.Length,
            ref anchor0,
            ref anchor1);

        Assert.True(translated);
        AssertVector(new Vector3(3f, -1f, 7f), points[0]);
        AssertVector(new Vector3(6f, 2f, 10f), points[1]);
        AssertVector(new Vector3(9f, 5f, 13f), anchor0);
        AssertVector(new Vector3(1f, -5f, 1f), anchor1);
        Assert.Equal(-12f, planes[0].PlaneDistance, 6);
        Assert.Equal(9f, planes[1].PlaneDistance, 6);
    }

    [Fact]
    public void BuildWoWSelectorSourcePlaneOutcode_SetsBitsForPlanesBelowBinaryThreshold()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = 0f,
            },
            new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 1f, 0f),
                PlaneDistance = 0f,
            },
            new SelectorSupportPlane
            {
                Normal = new Vector3(0f, 0f, 1f),
                PlaneDistance = -0.03f,
            },
        ];

        uint outcode = BuildWoWSelectorSourcePlaneOutcode(
            planes,
            planes.Length,
            new Vector3(-0.02f, 0.01f, 0f));

        Assert.Equal(0x5u, outcode);
    }

    [Fact]
    public void BuildWoWSelectorSourcePlaneOutcode_ThresholdIsStrictlyLessThanBinaryCutoff()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1f, 0f, 0f),
                PlaneDistance = 0f,
            },
        ];

        uint below = BuildWoWSelectorSourcePlaneOutcode(planes, planes.Length, new Vector3(-0.0195f, 0f, 0f));
        uint exactish = BuildWoWSelectorSourcePlaneOutcode(planes, planes.Length, new Vector3(-0.019444443f, 0f, 0f));

        Assert.Equal(0x1u, below);
        Assert.Equal(0u, exactish);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }
}
