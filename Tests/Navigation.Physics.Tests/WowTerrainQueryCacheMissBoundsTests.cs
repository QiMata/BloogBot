using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryCacheMissBoundsTests
{
    [Fact]
    public void TerrainQueryCacheMissBounds_ExpandsProjectedBoundsByBinaryOneSixth()
    {
        Vector3 projectedPosition = new(10f, 20f, 30f);
        Vector3 cachedBoundsMin = new(9.666667f, 19.666666f, 30f);
        Vector3 cachedBoundsMax = new(10.333333f, 20.333334f, 32.02778f);

        bool built = BuildWoWTerrainQueryCacheMissBounds(
            projectedPosition,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin,
            cachedBoundsMax,
            out Vector3 boundsMin,
            out Vector3 boundsMax);

        Assert.True(built);
        Assert.Equal(9.5f, boundsMin.X, 5);
        Assert.Equal(19.5f, boundsMin.Y, 5);
        Assert.Equal(29.833334f, boundsMin.Z, 5);
        Assert.Equal(10.5f, boundsMax.X, 5);
        Assert.Equal(20.5f, boundsMax.Y, 5);
        Assert.Equal(32.194447f, boundsMax.Z, 5);
    }

    [Fact]
    public void TerrainQueryCacheMissBounds_MergesExpandedProjectedBoundsWithCachedAabb()
    {
        Vector3 projectedPosition = new(3f, 4f, 5f);
        Vector3 cachedBoundsMin = new(2.0f, 3.4f, 4.5f);
        Vector3 cachedBoundsMax = new(3.9f, 4.8f, 7.5f);

        bool built = BuildWoWTerrainQueryCacheMissBounds(
            projectedPosition,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            out Vector3 boundsMin,
            out Vector3 boundsMax);

        Assert.True(built);
        Assert.Equal(2.0f, boundsMin.X, 5);
        Assert.Equal(3.3333333f, boundsMin.Y, 5);
        Assert.Equal(4.5f, boundsMin.Z, 5);
        Assert.Equal(3.9f, boundsMax.X, 5);
        Assert.Equal(4.8f, boundsMax.Y, 5);
        Assert.Equal(7.5f, boundsMax.Z, 5);
    }
}
