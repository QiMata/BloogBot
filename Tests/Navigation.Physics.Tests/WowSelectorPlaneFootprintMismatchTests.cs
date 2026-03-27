using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorPlaneFootprintMismatchTests
{
    [Fact]
    public void EvaluateWoWSelectorPlaneFootprintMismatch_HorizontalPlaneAtSampleHeight_ReturnsFalse()
    {
        Vector3 position = new(10.0f, 20.0f, 30.0f);
        float radius = 1.0f;
        float sampleHeight = radius * 1.8493989706039429f;
        SelectorSupportPlane plane = new()
        {
            Normal = new Vector3(0.0f, 0.0f, 1.0f),
            PlaneDistance = -(position.Z + sampleHeight),
        };

        Assert.False(EvaluateWoWSelectorPlaneFootprintMismatch(position, radius, plane));
    }

    [Fact]
    public void EvaluateWoWSelectorPlaneFootprintMismatch_OffsetBelowBinaryEpsilon_StaysAccepted()
    {
        Vector3 position = new(10.0f, 20.0f, 30.0f);
        float radius = 1.0f;
        float sampleHeight = radius * 1.8493989706039429f;
        SelectorSupportPlane plane = new()
        {
            Normal = new Vector3(0.0f, 0.0f, 1.0f),
            PlaneDistance = -(position.Z + sampleHeight + (1.0f / 1440.0f)),
        };

        Assert.False(EvaluateWoWSelectorPlaneFootprintMismatch(position, radius, plane));
    }

    [Fact]
    public void EvaluateWoWSelectorPlaneFootprintMismatch_OffsetAboveBinaryEpsilon_ReturnsTrue()
    {
        Vector3 position = new(10.0f, 20.0f, 30.0f);
        float radius = 1.0f;
        float sampleHeight = radius * 1.8493989706039429f;
        SelectorSupportPlane plane = new()
        {
            Normal = new Vector3(0.0f, 0.0f, 1.0f),
            PlaneDistance = -(position.Z + sampleHeight + (1.0f / 360.0f)),
        };

        Assert.True(EvaluateWoWSelectorPlaneFootprintMismatch(position, radius, plane));
    }

    [Fact]
    public void EvaluateWoWSelectorPlaneFootprintMismatch_VerticalPlaneMissesCornerSamples_ReturnsTrue()
    {
        Vector3 position = new(10.0f, 20.0f, 30.0f);
        float radius = 1.0f;
        SelectorSupportPlane plane = new()
        {
            Normal = new Vector3(1.0f, 0.0f, 0.0f),
            PlaneDistance = -(position.X + radius),
        };

        Assert.True(EvaluateWoWSelectorPlaneFootprintMismatch(position, radius, plane));
    }
}
