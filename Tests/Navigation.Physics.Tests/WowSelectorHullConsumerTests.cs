using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorHullConsumerTests
{
    [Fact]
    public void EvaluateWoWSelectorHullTransformedBoundsCull_IdentityBasisMatchesAxisAlignedCull()
    {
        SelectorSupportPlane[] planes = BuildUnitBoxHullPlanes();
        Vector3 boundsMin = new(0.0f, 0.0f, 0.0f);
        Vector3 boundsMax = new(1.0f, 1.0f, 1.0f);
        Vector3 identityX = new(1.0f, 0.0f, 0.0f);
        Vector3 identityY = new(0.0f, 1.0f, 0.0f);
        Vector3 identityZ = new(0.0f, 0.0f, 1.0f);
        Vector3 translation = new(0.0f, 0.0f, 0.0f);

        uint transformedResult = EvaluateWoWSelectorHullTransformedBoundsCull(
            planes,
            planes.Length,
            boundsMin,
            boundsMax,
            identityX,
            identityY,
            identityZ,
            translation);

        Assert.Equal(
            EvaluateWoWSelectorSourceAabbCull(planes, planes.Length, boundsMin, boundsMax),
            transformedResult);
    }

    [Fact]
    public void EvaluateWoWSelectorHullTransformedBoundsCull_UsesRotatedLocalSignBitsToChooseTheSupportCorner()
    {
        SelectorSupportPlane[] inclusivePlane =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1.0f, 0.0f, 0.0f),
                PlaneDistance = -5.0194f,
            },
        ];
        SelectorSupportPlane[] rejectingPlane =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1.0f, 0.0f, 0.0f),
                PlaneDistance = -5.0195f,
            },
        ];

        Vector3 basisRow0 = new(0.0f, 1.0f, 0.0f);
        Vector3 basisRow1 = new(-1.0f, 0.0f, 0.0f);
        Vector3 basisRow2 = new(0.0f, 0.0f, 1.0f);
        Vector3 translation = new(10.0f, 20.0f, 30.0f);

        Assert.Equal(3u, EvaluateWoWSelectorHullTransformedBoundsCull(
            inclusivePlane,
            inclusivePlane.Length,
            localBoundsMin: new Vector3(-1.0f, -2.0f, -3.0f),
            localBoundsMax: new Vector3(4.0f, 5.0f, 6.0f),
            basisRow0,
            basisRow1,
            basisRow2,
            translation));

        Assert.Equal(0u, EvaluateWoWSelectorHullTransformedBoundsCull(
            rejectingPlane,
            rejectingPlane.Length,
            localBoundsMin: new Vector3(-1.0f, -2.0f, -3.0f),
            localBoundsMax: new Vector3(4.0f, 5.0f, 6.0f),
            basisRow0,
            basisRow1,
            basisRow2,
            translation));
    }

    [Fact]
    public void EvaluateWoWSelectorHullTransformedBoundsCull_UsesInclusiveBinaryThreshold()
    {
        SelectorSupportPlane[] inclusivePlane =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1.0f, 0.0f, 0.0f),
                PlaneDistance = -12.0194f,
            },
        ];
        SelectorSupportPlane[] rejectingPlane =
        [
            new SelectorSupportPlane
            {
                Normal = new Vector3(1.0f, 0.0f, 0.0f),
                PlaneDistance = -12.0195f,
            },
        ];

        Vector3 identityX = new(1.0f, 0.0f, 0.0f);
        Vector3 identityY = new(0.0f, 1.0f, 0.0f);
        Vector3 identityZ = new(0.0f, 0.0f, 1.0f);
        Vector3 translation = new(11.0f, 0.0f, 0.0f);
        Vector3 worldBoundsMin = new(11.0f, 0.0f, 0.0f);
        Vector3 worldBoundsMax = new(12.0f, 1.0f, 1.0f);

        Assert.Equal(0u, EvaluateWoWSelectorSourceAabbCull(
            rejectingPlane,
            rejectingPlane.Length,
            worldBoundsMin,
            worldBoundsMax));

        Assert.Equal(0u, EvaluateWoWSelectorHullTransformedBoundsCull(
            rejectingPlane,
            rejectingPlane.Length,
            localBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            localBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            identityX,
            identityY,
            identityZ,
            translation));

        Assert.Equal(3u, EvaluateWoWSelectorSourceAabbCull(
            inclusivePlane,
            inclusivePlane.Length,
            worldBoundsMin,
            worldBoundsMax));

        Assert.Equal(3u, EvaluateWoWSelectorHullTransformedBoundsCull(
            inclusivePlane,
            inclusivePlane.Length,
            localBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            localBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            identityX,
            identityY,
            identityZ,
            translation));
    }

    [Fact]
    public void EvaluateWoWSelectorHullPointWithMargin_UsesNegatedMarginThreshold()
    {
        SelectorSupportPlane[] planes = BuildUnitBoxHullPlanes();

        Assert.Equal(3u, EvaluateWoWSelectorHullPointWithMargin(
            planes,
            planes.Length,
            point: new Vector3(-0.015f, 0.5f, 0.5f),
            margin: 0.02f));

        Assert.Equal(0u, EvaluateWoWSelectorHullPointWithMargin(
            planes,
            planes.Length,
            point: new Vector3(-0.021f, 0.5f, 0.5f),
            margin: 0.02f));
    }

    [Fact]
    public void EvaluateWoWSelectorHullPointEpsilon_UsesFixedBinaryThreshold()
    {
        SelectorSupportPlane[] planes = BuildUnitBoxHullPlanes();

        Assert.Equal(3u, EvaluateWoWSelectorHullPointEpsilon(
            planes,
            planes.Length,
            point: new Vector3(-0.019f, 0.5f, 0.5f)));

        Assert.Equal(0u, EvaluateWoWSelectorHullPointEpsilon(
            planes,
            planes.Length,
            point: new Vector3(-0.02f, 0.5f, 0.5f)));
    }

    private static SelectorSupportPlane[] BuildUnitBoxHullPlanes() =>
    [
        new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = 0.0f },
        new SelectorSupportPlane { Normal = new Vector3(-1.0f, 0.0f, 0.0f), PlaneDistance = 1.0f },
        new SelectorSupportPlane { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = 0.0f },
        new SelectorSupportPlane { Normal = new Vector3(0.0f, -1.0f, 0.0f), PlaneDistance = 1.0f },
        new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, 1.0f), PlaneDistance = 0.0f },
        new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, -1.0f), PlaneDistance = 1.0f },
    ];
}
