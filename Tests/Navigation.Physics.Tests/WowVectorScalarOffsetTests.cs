using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowVectorScalarOffsetTests
{
    [Fact]
    public void AddScalarToVector3_AddsToAllComponents()
    {
        Vector3 value = new(1f, -2f, 3.5f);

        bool updated = AddScalarToWoWVector3(ref value, 0.16666667f);

        Assert.True(updated);
        Assert.Equal(1.1666666f, value.X, 5);
        Assert.Equal(-1.8333334f, value.Y, 5);
        Assert.Equal(3.6666667f, value.Z, 5);
    }

    [Fact]
    public void SubtractScalarFromVector3_SubtractsFromAllComponents()
    {
        Vector3 value = new(1f, -2f, 3.5f);

        bool updated = SubtractScalarFromWoWVector3(ref value, 0.16666667f);

        Assert.True(updated);
        Assert.Equal(0.8333333f, value.X, 5);
        Assert.Equal(-2.1666667f, value.Y, 5);
        Assert.Equal(3.3333333f, value.Z, 5);
    }
}
