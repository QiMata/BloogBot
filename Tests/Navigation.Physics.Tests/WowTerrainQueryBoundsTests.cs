using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryBoundsTests
{
    [Fact]
    public void TerrainQueryBounds_UsesCollisionRadiusOnHorizontalAxes_AndFeetZAsMin()
    {
        Vector3 projectedPosition = new(10f, 20f, 30f);

        bool built = BuildWoWTerrainQueryBounds(
            projectedPosition,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            out Vector3 boundsMin,
            out Vector3 boundsMax);

        Assert.True(built);
        Assert.Equal(9.666667f, boundsMin.X, 5);
        Assert.Equal(19.666666f, boundsMin.Y, 5);
        Assert.Equal(30f, boundsMin.Z, 5);
        Assert.Equal(10.333333f, boundsMax.X, 5);
        Assert.Equal(20.333334f, boundsMax.Y, 5);
    }

    [Fact]
    public void TerrainQueryBounds_UsesBoundingHeightOnMaxZ()
    {
        Vector3 projectedPosition = new(-4f, 8f, 12f);

        bool built = BuildWoWTerrainQueryBounds(
            projectedPosition,
            collisionRadius: 0.5f,
            boundingHeight: 2.5f,
            out Vector3 boundsMin,
            out Vector3 boundsMax);

        Assert.True(built);
        Assert.Equal(12f, boundsMin.Z, 5);
        Assert.Equal(14.5f, boundsMax.Z, 5);
    }

    [Fact]
    public void TerrainQueryBounds_CacheReuseGateRequiresBothMinAndMaxCornersInside()
    {
        Vector3 projectedPosition = new(3f, 4f, 5f);
        Assert.True(BuildWoWTerrainQueryBounds(projectedPosition, 0.5f, 2f, out Vector3 boundsMin, out Vector3 boundsMax));

        Vector3 cachedBoundsMin = new(2.5f, 3.5f, 5f);
        Vector3 cachedBoundsMax = new(3.5f, 4.5f, 7f);
        Assert.True(EvaluateWoWPointInsideAabbInclusive(cachedBoundsMin, cachedBoundsMax, boundsMin));
        Assert.True(EvaluateWoWPointInsideAabbInclusive(cachedBoundsMin, cachedBoundsMax, boundsMax));

        Vector3 tooShortMaxBounds = new(3.5f, 4.5f, 6.999f);
        Assert.True(EvaluateWoWPointInsideAabbInclusive(cachedBoundsMin, tooShortMaxBounds, boundsMin));
        Assert.False(EvaluateWoWPointInsideAabbInclusive(cachedBoundsMin, tooShortMaxBounds, boundsMax));
    }
}
