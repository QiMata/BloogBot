using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

public sealed class TerrainAabbContactOrientationTests
{
    [Fact]
    public void TriangleBelowBoxCenter_FacesUp_AndRemainsWalkable()
    {
        Vector3 boxMin = new(-0.25f, -0.25f, -0.25f);
        Vector3 boxMax = new(0.25f, 0.25f, 0.25f);
        Triangle triangle = new(
            new Vector3(-1.0f, -1.0f, -0.5f),
            new Vector3(-1.0f, 1.0f, -0.5f),
            new Vector3(1.0f, -1.0f, -0.5f));

        bool ok = EvaluateTerrainAABBContactOrientation(
            triangle,
            boxMin,
            boxMax,
            out Vector3 normal,
            out float planeDistance,
            out bool walkable);

        Assert.True(ok);
        Assert.True(walkable);
        Assert.True(normal.Z > 0.99f, $"expected upward-facing contact normal, got {normal}");
        Assert.True(planeDistance > 0.0f);
    }

    [Fact]
    public void TriangleAboveBoxCenter_FacesDown_AndStopsBeingWalkable()
    {
        Vector3 boxMin = new(-0.25f, -0.25f, -0.25f);
        Vector3 boxMax = new(0.25f, 0.25f, 0.25f);
        Triangle triangle = new(
            new Vector3(-1.0f, -1.0f, 0.5f),
            new Vector3(1.0f, -1.0f, 0.5f),
            new Vector3(-1.0f, 1.0f, 0.5f));

        bool ok = EvaluateTerrainAABBContactOrientation(
            triangle,
            boxMin,
            boxMax,
            out Vector3 normal,
            out _,
            out bool walkable);

        Assert.True(ok);
        Assert.False(walkable);
        Assert.True(normal.Z < -0.99f, $"expected downward-facing contact normal, got {normal}");
    }

    [Fact]
    public void WallTriangle_FacesTowardBoxCenter()
    {
        Vector3 boxMin = new(-0.25f, -0.25f, -0.25f);
        Vector3 boxMax = new(0.25f, 0.25f, 0.25f);
        Triangle triangle = new(
            new Vector3(0.5f, -1.0f, -1.0f),
            new Vector3(0.5f, 1.0f, -1.0f),
            new Vector3(0.5f, -1.0f, 1.0f));

        bool ok = EvaluateTerrainAABBContactOrientation(
            triangle,
            boxMin,
            boxMax,
            out Vector3 normal,
            out _,
            out bool walkable);

        Assert.True(ok);
        Assert.False(walkable);
        Assert.True(normal.X < -0.99f, $"expected west-facing wall normal, got {normal}");
    }
}
