using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorTrianglePlaneOutcodeRejectTests
{
    [Fact]
    public void EvaluateWoWSelectorTrianglePlaneOutcodeReject_WhenAllVerticesShareSelectorPlaneBit_ReturnsTrue()
    {
        bool rejected = EvaluateWoWSelectorTrianglePlaneOutcodeReject(0x01u, 0x11u, 0x21u);

        Assert.True(rejected);
    }

    [Fact]
    public void EvaluateWoWSelectorTrianglePlaneOutcodeReject_WhenVerticesDoNotShareSelectorPlaneBit_ReturnsFalse()
    {
        bool rejected = EvaluateWoWSelectorTrianglePlaneOutcodeReject(0x01u, 0x03u, 0x02u);

        Assert.False(rejected);
    }

    [Fact]
    public void EvaluateWoWSelectorTrianglePlaneOutcodeReject_WhenOneVertexIsInside_ReturnsFalse()
    {
        bool rejected = EvaluateWoWSelectorTrianglePlaneOutcodeReject(0x00u, 0x20u, 0x20u);

        Assert.False(rejected);
    }
}
