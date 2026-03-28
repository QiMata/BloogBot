using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowTerrainQueryMergedTransactionTests
{
    [Fact]
    public void TerrainQueryMergedTransaction_CacheHitReusesCachedQueryAndBypassesMissPath()
    {
        Vector3 projectedPosition = new(3.0f, 4.0f, 5.0f);
        Vector3 cachedBoundsMin = new(2.5f, 3.5f, 5.0f);
        Vector3 cachedBoundsMax = new(3.5f, 4.5f, 7.0f);

        bool result = EvaluateWoWTerrainQueryMergedQueryTransaction(
            projectedPosition,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: false,
            movementFlags: 0x10000000u,
            field20Value: -0.5f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            queryDispatchSucceeded: false,
            out TerrainQueryMergedQueryTrace trace);

        Assert.True(result);
        Assert.Equal(2.5f, trace.QueryBoundsMin.X, 5);
        Assert.Equal(3.5f, trace.QueryBoundsMin.Y, 5);
        Assert.Equal(5.0f, trace.QueryBoundsMin.Z, 5);
        Assert.Equal(3.5f, trace.QueryBoundsMax.X, 5);
        Assert.Equal(4.5f, trace.QueryBoundsMax.Y, 5);
        Assert.Equal(7.0f, trace.QueryBoundsMax.Z, 5);
        Assert.Equal(1u, trace.CacheContainsBoundsMin);
        Assert.Equal(1u, trace.CacheContainsBoundsMax);
        Assert.Equal(1u, trace.ReusedCachedQuery);
        Assert.Equal(0u, trace.BuiltMergedBounds);
        Assert.Equal(0u, trace.BuiltQueryMask);
        Assert.Equal(0u, trace.QueryInvoked);
        Assert.Equal(0u, trace.QueryDispatchSucceeded);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(0u, trace.QueryMask);
    }

    [Fact]
    public void TerrainQueryMergedTransaction_CacheMissBuildsMergedBoundsAndMaskBeforeFailedDispatch()
    {
        Vector3 projectedPosition = new(3.0f, 4.0f, 5.0f);
        Vector3 cachedBoundsMin = new(2.0f, 3.4f, 4.5f);
        Vector3 cachedBoundsMax = new(3.5f, 4.5f, 6.999f);

        bool result = EvaluateWoWTerrainQueryMergedQueryTransaction(
            projectedPosition,
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: false,
            movementFlags: 0x10000000u,
            field20Value: -0.5f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            queryDispatchSucceeded: false,
            out TerrainQueryMergedQueryTrace trace);

        Assert.False(result);
        Assert.Equal(1u, trace.CacheContainsBoundsMin);
        Assert.Equal(0u, trace.CacheContainsBoundsMax);
        Assert.Equal(0u, trace.ReusedCachedQuery);
        Assert.Equal(1u, trace.BuiltMergedBounds);
        Assert.Equal(1u, trace.BuiltQueryMask);
        Assert.Equal(1u, trace.QueryInvoked);
        Assert.Equal(0u, trace.QueryDispatchSucceeded);
        Assert.Equal(0u, trace.ReturnedSuccess);
        Assert.Equal(0x13A111u, trace.QueryMask);
        Assert.Equal(2.0f, trace.MergedBoundsMin.X, 5);
        Assert.Equal(3.3333333f, trace.MergedBoundsMin.Y, 5);
        Assert.Equal(4.5f, trace.MergedBoundsMin.Z, 5);
        Assert.Equal(3.6666667f, trace.MergedBoundsMax.X, 5);
        Assert.Equal(4.6666665f, trace.MergedBoundsMax.Y, 5);
        Assert.Equal(7.1666665f, trace.MergedBoundsMax.Z, 5);
    }

    [Fact]
    public void TerrainQueryMergedTransaction_CacheMissReturnsDispatchSuccessAndUsesModelPropertyBaseMask()
    {
        Vector3 projectedPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 cachedBoundsMin = new(9.666667f, 19.666666f, 30.0f);
        Vector3 cachedBoundsMax = new(10.333333f, 20.333334f, 32.0f);

        bool result = EvaluateWoWTerrainQueryMergedQueryTransaction(
            projectedPosition,
            collisionRadius: 0.33333334f,
            boundingHeight: 2.027778f,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet: true,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            queryDispatchSucceeded: true,
            out TerrainQueryMergedQueryTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.BuiltMergedBounds);
        Assert.Equal(1u, trace.BuiltQueryMask);
        Assert.Equal(1u, trace.QueryInvoked);
        Assert.Equal(1u, trace.QueryDispatchSucceeded);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(0x108111u, trace.QueryMask);
        Assert.Equal(9.5f, trace.MergedBoundsMin.X, 5);
        Assert.Equal(19.5f, trace.MergedBoundsMin.Y, 5);
        Assert.Equal(29.833334f, trace.MergedBoundsMin.Z, 5);
        Assert.Equal(10.5f, trace.MergedBoundsMax.X, 5);
        Assert.Equal(20.5f, trace.MergedBoundsMax.Y, 5);
        Assert.Equal(32.194447f, trace.MergedBoundsMax.Z, 5);
    }
}
