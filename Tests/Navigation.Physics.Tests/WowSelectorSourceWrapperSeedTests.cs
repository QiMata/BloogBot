using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceWrapperSeedTests
{
    [Fact]
    public void InitializeSelectorTriangleSourceWrapperSeeds_UsesBinaryDefaultVectorsAndRatio()
    {
        bool initialized = InitializeWoWSelectorTriangleSourceWrapperSeeds(
            out Vector3 testPoint,
            out Vector3 candidateDirection,
            out float bestRatio);

        Assert.True(initialized);
        Assert.Equal(0f, testPoint.X, 6);
        Assert.Equal(0f, testPoint.Y, 6);
        Assert.Equal(-1f, testPoint.Z, 6);
        Assert.Equal(0f, candidateDirection.X, 6);
        Assert.Equal(0f, candidateDirection.Y, 6);
        Assert.Equal(-1f, candidateDirection.Z, 6);
        Assert.Equal(1f, bestRatio, 6);
    }
}
