using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorSourceWrapperTransactionTests
{
    [Fact]
    public void WrapperTransaction_NoOverride_QueryFailureUsesDefaultPositionAndReturnsFalse()
    {
        Vector3 defaultPosition = new(11.5f, -3.25f, 7.75f);

        bool accepted = EvaluateWoWSelectorTriangleSourceWrapperTransaction(
            defaultPosition,
            overridePosition: null,
            terrainQuerySucceeded: false,
            inputBestRatio: 0.75f,
            out SelectorTriangleSourceWrapperTrace trace);

        Assert.False(accepted);
        Assert.Equal(7u, trace.SupportPlaneInitCount);
        Assert.Equal(9u, trace.ValidationPlaneInitCount);
        Assert.Equal(9u, trace.ScratchPointZeroCount);
        Assert.Equal(0u, trace.UsedOverridePosition);
        Assert.Equal(1u, trace.TerrainQueryInvoked);
        Assert.Equal(0u, trace.TerrainQuerySucceeded);
        Assert.Equal(0u, trace.ReturnedSuccess);
        Assert.Equal(1u, trace.QueryFailureZeroedOutput);
        Assert.Equal(defaultPosition.X, trace.SelectedPosition.X, 6);
        Assert.Equal(defaultPosition.Y, trace.SelectedPosition.Y, 6);
        Assert.Equal(defaultPosition.Z, trace.SelectedPosition.Z, 6);
        Assert.Equal(0f, trace.TestPoint.X, 6);
        Assert.Equal(0f, trace.TestPoint.Y, 6);
        Assert.Equal(-1f, trace.TestPoint.Z, 6);
        Assert.Equal(0f, trace.CandidateDirection.X, 6);
        Assert.Equal(0f, trace.CandidateDirection.Y, 6);
        Assert.Equal(-1f, trace.CandidateDirection.Z, 6);
        Assert.Equal(1f, trace.InitialBestRatio, 6);
        Assert.Equal(0.75f, trace.InputBestRatio, 6);
        Assert.Equal(0f, trace.ReportedBestRatio, 6);
    }

    [Fact]
    public void WrapperTransaction_OverrideBypassesTerrainQueryAndUsesOverridePosition()
    {
        Vector3 defaultPosition = new(1f, 2f, 3f);
        Vector3 overridePosition = new(-4.5f, 8.25f, 12.75f);

        bool accepted = EvaluateWoWSelectorTriangleSourceWrapperTransaction(
            defaultPosition,
            overridePosition,
            terrainQuerySucceeded: false,
            inputBestRatio: 0.75f,
            out SelectorTriangleSourceWrapperTrace trace);

        Assert.True(accepted);
        Assert.Equal(1u, trace.UsedOverridePosition);
        Assert.Equal(0u, trace.TerrainQueryInvoked);
        Assert.Equal(0u, trace.TerrainQuerySucceeded);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(0u, trace.QueryFailureZeroedOutput);
        Assert.Equal(overridePosition.X, trace.SelectedPosition.X, 6);
        Assert.Equal(overridePosition.Y, trace.SelectedPosition.Y, 6);
        Assert.Equal(overridePosition.Z, trace.SelectedPosition.Z, 6);
        Assert.Equal(0.75f, trace.ReportedBestRatio, 6);
    }

    [Fact]
    public void WrapperTransaction_SuccessPathStillZeroClampsTinyReportedRatio()
    {
        Vector3 defaultPosition = new(0.5f, 1.5f, 2.5f);

        bool accepted = EvaluateWoWSelectorTriangleSourceWrapperTransaction(
            defaultPosition,
            overridePosition: null,
            terrainQuerySucceeded: true,
            inputBestRatio: 0.001f,
            out SelectorTriangleSourceWrapperTrace trace);

        Assert.True(accepted);
        Assert.Equal(1u, trace.TerrainQueryInvoked);
        Assert.Equal(1u, trace.TerrainQuerySucceeded);
        Assert.Equal(1u, trace.ReturnedSuccess);
        Assert.Equal(0u, trace.QueryFailureZeroedOutput);
        Assert.Equal(0f, trace.ReportedBestRatio, 6);
    }
}
