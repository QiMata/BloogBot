using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowAabbMergeTests
{
    [Fact]
    public void MergeAabbBounds_TakesComponentwiseMinAndMax()
    {
        Vector3 minA = new(1f, 5f, 3f);
        Vector3 maxA = new(4f, 9f, 6f);
        Vector3 minB = new(0.5f, 6f, 2f);
        Vector3 maxB = new(3f, 10f, 8f);

        bool merged = MergeWoWAabbBounds(minA, maxA, minB, maxB, out Vector3 mergedMin, out Vector3 mergedMax);

        Assert.True(merged);
        Assert.Equal(new Vector3(0.5f, 5f, 2f).ToString(), mergedMin.ToString());
        Assert.Equal(new Vector3(4f, 10f, 8f).ToString(), mergedMax.ToString());
    }

    [Fact]
    public void MergeAabbBounds_PreservesSharedFaces()
    {
        Vector3 minA = new(-2f, -2f, -2f);
        Vector3 maxA = new(2f, 2f, 2f);
        Vector3 minB = new(-2f, -1f, -3f);
        Vector3 maxB = new(3f, 2f, 2f);

        bool merged = MergeWoWAabbBounds(minA, maxA, minB, maxB, out Vector3 mergedMin, out Vector3 mergedMax);

        Assert.True(merged);
        Assert.Equal(-2f, mergedMin.X, 5);
        Assert.Equal(-2f, mergedMin.Y, 5);
        Assert.Equal(-3f, mergedMin.Z, 5);
        Assert.Equal(3f, mergedMax.X, 5);
        Assert.Equal(2f, mergedMax.Y, 5);
        Assert.Equal(2f, mergedMax.Z, 5);
    }
}
