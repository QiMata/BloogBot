using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowAabbOutcodeTests
{
    [Fact]
    public void BuildWoWAabbOutcode_SetsBelowMinBitsPerAxis()
    {
        Vector3 boundsMin = new(10f, 20f, 30f);
        Vector3 boundsMax = new(40f, 50f, 60f);
        Vector3 point = new(9f, 19f, 29f);

        uint outcode = BuildWoWAabbOutcode(point, boundsMin, boundsMax);

        Assert.Equal(0x15u, outcode);
    }

    [Fact]
    public void BuildWoWAabbOutcode_SetsAboveMaxBitsPerAxis()
    {
        Vector3 boundsMin = new(10f, 20f, 30f);
        Vector3 boundsMax = new(40f, 50f, 60f);
        Vector3 point = new(41f, 51f, 61f);

        uint outcode = BuildWoWAabbOutcode(point, boundsMin, boundsMax);

        Assert.Equal(0x2Au, outcode);
    }

    [Fact]
    public void BuildWoWAabbOutcode_TreatsBoundaryFacesAsInside()
    {
        Vector3 boundsMin = new(10f, 20f, 30f);
        Vector3 boundsMax = new(40f, 50f, 60f);

        Assert.Equal(0u, BuildWoWAabbOutcode(new Vector3(10f, 20f, 30f), boundsMin, boundsMax));
        Assert.Equal(0u, BuildWoWAabbOutcode(new Vector3(40f, 50f, 60f), boundsMin, boundsMax));
    }

    [Fact]
    public void EvaluateWoWTriangleAabbOutcodeReject_WhenAllVerticesShareOutsideBit_ReturnsTrue()
    {
        bool rejected = EvaluateWoWTriangleAabbOutcodeReject(0x01u, 0x11u, 0x21u);

        Assert.True(rejected);
    }

    [Fact]
    public void EvaluateWoWTriangleAabbOutcodeReject_WhenVerticesDoNotShareOutsideBit_ReturnsFalse()
    {
        bool rejected = EvaluateWoWTriangleAabbOutcodeReject(0x01u, 0x02u, 0x00u);

        Assert.False(rejected);
    }
}
