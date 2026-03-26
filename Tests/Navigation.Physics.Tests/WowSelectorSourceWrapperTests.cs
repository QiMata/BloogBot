using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceWrapperTests
{
    [Fact]
    public void InitializeSelectorSupportPlane_DefaultsToUnitZ()
    {
        bool initialized = InitializeWoWSelectorSupportPlane(out SelectorSupportPlane plane);

        Assert.True(initialized);
        Assert.Equal(0f, plane.Normal.X, 6);
        Assert.Equal(0f, plane.Normal.Y, 6);
        Assert.Equal(1f, plane.Normal.Z, 6);
        Assert.Equal(0f, plane.PlaneDistance, 6);
    }

    [Fact]
    public void ClampSelectorReportedBestRatio_ZeroesAtAndBelowBinaryEpsilon()
    {
        Assert.Equal(0f, EvaluateWoWSelectorReportedBestRatioClamp(0.0013888889f), 6);
        Assert.Equal(0f, EvaluateWoWSelectorReportedBestRatioClamp(0.001f), 6);
        Assert.Equal(0.01f, EvaluateWoWSelectorReportedBestRatioClamp(0.01f), 6);
    }

    [Fact]
    public void FinalizeSelectorTriangleSourceWrapper_NoOverrideAndQueryFailReturnsFalseAndZero()
    {
        bool accepted = EvaluateWoWSelectorTriangleSourceWrapperGates(
            hasOverridePosition: false,
            terrainQuerySucceeded: false,
            inputBestRatio: 0.75f,
            out float reportedBestRatio);

        Assert.False(accepted);
        Assert.Equal(0f, reportedBestRatio, 6);
    }

    [Fact]
    public void FinalizeSelectorTriangleSourceWrapper_OverrideBypassesQueryFailure()
    {
        bool accepted = EvaluateWoWSelectorTriangleSourceWrapperGates(
            hasOverridePosition: true,
            terrainQuerySucceeded: false,
            inputBestRatio: 0.75f,
            out float reportedBestRatio);

        Assert.True(accepted);
        Assert.Equal(0.75f, reportedBestRatio, 6);
    }

    [Fact]
    public void FinalizeSelectorTriangleSourceWrapper_SuccessPathStillZeroClampsTinyRatio()
    {
        bool accepted = EvaluateWoWSelectorTriangleSourceWrapperGates(
            hasOverridePosition: false,
            terrainQuerySucceeded: true,
            inputBestRatio: 0.001f,
            out float reportedBestRatio);

        Assert.True(accepted);
        Assert.Equal(0f, reportedBestRatio, 6);
    }
}
