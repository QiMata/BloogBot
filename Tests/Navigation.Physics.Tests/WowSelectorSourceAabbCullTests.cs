using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceAabbCullTests
{
    [Fact]
    public void EvaluateWoWSelectorSourceAabbCull_ReturnsThreeWhenAllPlanesKeepTheSupportPoint()
    {
        SelectorSupportPlane[] planes =
        [
            new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = -4.0f },
            new SelectorSupportPlane { Normal = new Vector3(0.0f, 1.0f, 0.0f), PlaneDistance = -3.0f },
            new SelectorSupportPlane { Normal = new Vector3(0.0f, 0.0f, 1.0f), PlaneDistance = -2.0f },
        ];

        uint result = EvaluateWoWSelectorSourceAabbCull(
            planes,
            planes.Length,
            boundsMin: new Vector3(-1.0f, -1.0f, -1.0f),
            boundsMax: new Vector3(5.0f, 4.0f, 3.0f));

        Assert.Equal(3u, result);
    }

    [Fact]
    public void EvaluateWoWSelectorSourceAabbCull_UsesSignedSupportCornersAndBinaryThreshold()
    {
        SelectorSupportPlane[] positivePlane =
        [
            new SelectorSupportPlane { Normal = new Vector3(1.0f, 0.0f, 0.0f), PlaneDistance = -5.0f },
        ];
        SelectorSupportPlane[] negativePlane =
        [
            new SelectorSupportPlane { Normal = new Vector3(-1.0f, 0.0f, 0.0f), PlaneDistance = 1.0f },
        ];

        Assert.Equal(0u, EvaluateWoWSelectorSourceAabbCull(
            positivePlane,
            positivePlane.Length,
            boundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            boundsMax: new Vector3(4.97f, 1.0f, 1.0f)));

        Assert.Equal(0u, EvaluateWoWSelectorSourceAabbCull(
            negativePlane,
            negativePlane.Length,
            boundsMin: new Vector3(1.03f, 0.0f, 0.0f),
            boundsMax: new Vector3(2.0f, 1.0f, 1.0f)));
    }
}
