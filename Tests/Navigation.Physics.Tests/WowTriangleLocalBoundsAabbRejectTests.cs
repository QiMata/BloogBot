using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTriangleLocalBoundsAabbRejectTests
{
    [Fact]
    public void EvaluateWoWTriangleLocalBoundsAabbReject_WhenAllVerticesShareOutsideMinFace_ReturnsTrue()
    {
        Vector3 boundsMin = new(-1.0f, -1.0f, -1.0f);
        Vector3 boundsMax = new(1.0f, 1.0f, 1.0f);

        bool rejected = EvaluateWoWTriangleLocalBoundsAabbReject(
            boundsMin,
            boundsMax,
            new Vector3(-2.0f, 0.0f, 0.0f),
            new Vector3(-3.0f, 0.25f, 0.0f),
            new Vector3(-4.0f, -0.25f, 0.5f));

        Assert.True(rejected);
    }

    [Fact]
    public void EvaluateWoWTriangleLocalBoundsAabbReject_WhenVerticesAreOutsideDifferentFaces_ReturnsFalse()
    {
        Vector3 boundsMin = new(-1.0f, -1.0f, -1.0f);
        Vector3 boundsMax = new(1.0f, 1.0f, 1.0f);

        bool rejected = EvaluateWoWTriangleLocalBoundsAabbReject(
            boundsMin,
            boundsMax,
            new Vector3(-2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 2.0f, 0.0f),
            new Vector3(0.0f, 0.0f, -2.0f));

        Assert.False(rejected);
    }

    [Fact]
    public void EvaluateWoWTriangleLocalBoundsAabbReject_WhenVerticesStayWithinBinaryEpsilon_ReturnsFalse()
    {
        Vector3 boundsMin = new(-1.0f, -1.0f, -1.0f);
        Vector3 boundsMax = new(1.0f, 1.0f, 1.0f);
        const float epsilon = 0.0194444433f;

        bool rejected = EvaluateWoWTriangleLocalBoundsAabbReject(
            boundsMin,
            boundsMax,
            new Vector3(-1.0f - (epsilon * 0.5f), 0.0f, 0.0f),
            new Vector3(-1.0f - (epsilon * 0.25f), 0.5f, 0.0f),
            new Vector3(-1.0f, -0.5f, 0.5f));

        Assert.False(rejected);
    }
}
