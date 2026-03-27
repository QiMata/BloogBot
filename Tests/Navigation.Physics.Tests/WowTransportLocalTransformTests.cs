using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTransportLocalTransformTests
{
    [Fact]
    public void TransformWorldPointToTransportLocal_InvertsYawAndTranslation()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        Vector3 worldPoint = new(10f, 22f, 6f);

        bool transformed = TransformWoWWorldPointToTransportLocal(
            worldPoint,
            transportPosition,
            transportOrientation,
            out Vector3 localPoint);

        Assert.True(transformed);
        Assert.Equal(2f, localPoint.X, 5);
        Assert.Equal(0f, localPoint.Y, 5);
        Assert.Equal(1f, localPoint.Z, 5);
    }

    [Fact]
    public void BuildTransportLocalPlane_RotatesNormalAndRecomputesPlaneDistance()
    {
        Vector3 transportPosition = new(10f, 20f, 5f);
        float transportOrientation = MathF.PI * 0.5f;
        Vector3 worldNormal = new(0f, 1f, 0f);
        Vector3 worldPoint = new(10f, 22f, 6f);

        bool built = BuildWoWTransportLocalPlane(
            worldNormal,
            worldPoint,
            transportPosition,
            transportOrientation,
            out SelectorSupportPlane plane);

        Assert.True(built);
        Assert.Equal(1f, plane.Normal.X, 5);
        Assert.Equal(0f, plane.Normal.Y, 5);
        Assert.Equal(0f, plane.Normal.Z, 5);
        Assert.Equal(-2f, plane.PlaneDistance, 5);
    }
}
