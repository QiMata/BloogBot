using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairCallerTransactionTests
{
    [Fact]
    public void CallerTransaction_NoOverride_QueryFailureReturnsFalseAndZeroesReportedRatio()
    {
        Vector3 defaultPosition = new(10.0f, 20.0f, 30.0f);
        Vector3 projectedPosition = new(3.0f, 4.0f, 5.0f);
        Vector3 cachedBoundsMin = new(2.0f, 3.4f, 4.5f);
        Vector3 cachedBoundsMax = new(3.5f, 4.5f, 6.999f);

        bool result = EvaluateWoWSelectorChosenIndexPairCallerTransaction(
            defaultPosition,
            overridePosition: null,
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
            rankingAccepted: true,
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 4,
            rankingReportedBestRatio: 0.75f,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairCallerTransactionTrace trace);

        Assert.False(result);
        Assert.Equal(7u, trace.SupportPlaneInitCount);
        Assert.Equal(9u, trace.ValidationPlaneInitCount);
        Assert.Equal(9u, trace.ScratchPointZeroCount);
        Assert.Equal(0u, trace.UsedOverridePosition);
        Assert.Equal(1u, trace.VariableInvoked);
        Assert.Equal(1u, trace.ZeroedOutputsOnVariableFailure);
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
        Assert.Equal(1u, trace.VariableTrace.TerrainQueryInvoked);
        Assert.Equal(0u, trace.VariableTrace.TerrainQuerySucceeded);
        Assert.Equal(1u, trace.VariableTrace.QueryFailureZeroedOutput);
        Assert.Equal(0f, reportedBestRatio, 6);
        Assert.Equal(0f, trace.OutputReportedBestRatio, 6);
    }

    [Fact]
    public void CallerTransaction_OverrideBypassesQueryAndUsesFixedSeeds()
    {
        Vector3 defaultPosition = new(1.0f, 2.0f, 3.0f);
        Vector3 overridePosition = new(-4.0f, 5.5f, 6.5f);

        bool result = EvaluateWoWSelectorChosenIndexPairCallerTransaction(
            defaultPosition,
            overridePosition,
            projectedPosition: new Vector3(7.0f, 8.0f, 9.0f),
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin: new Vector3(0.0f, 0.0f, 0.0f),
            cachedBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            modelPropertyFlagSet: false,
            movementFlags: 0u,
            field20Value: -1.0f,
            rootTreeFlagSet: false,
            childTreeFlagSet: false,
            queryDispatchSucceeded: false,
            rankingAccepted: false,
            rankingCandidateCount: 3u,
            rankingSelectedRecordIndex: -1,
            rankingReportedBestRatio: 0.25f,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairCallerTransactionTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.UsedOverridePosition);
        Assert.Equal(1u, trace.VariableInvoked);
        Assert.Equal(0u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(overridePosition.X, trace.SelectedPosition.X, 6);
        Assert.Equal(overridePosition.Y, trace.SelectedPosition.Y, 6);
        Assert.Equal(overridePosition.Z, trace.SelectedPosition.Z, 6);
        Assert.Equal(0u, trace.VariableTrace.TerrainQueryInvoked);
        Assert.Equal(0u, trace.VariableTrace.TerrainQuerySucceeded);
        Assert.Equal(0.25f, reportedBestRatio, 6);
        Assert.Equal(0.25f, trace.OutputReportedBestRatio, 6);
    }

    [Fact]
    public void CallerTransaction_QuerySuccessZeroClampsReportedRatio()
    {
        bool result = EvaluateWoWSelectorChosenIndexPairCallerTransaction(
            defaultPosition: new Vector3(11.5f, -3.25f, 7.75f),
            overridePosition: null,
            projectedPosition: new Vector3(3.0f, 4.0f, 5.0f),
            collisionRadius: 0.5f,
            boundingHeight: 2.0f,
            cachedBoundsMin: new Vector3(2.5f, 3.5f, 5.0f),
            cachedBoundsMax: new Vector3(3.5f, 4.5f, 7.0f),
            modelPropertyFlagSet: false,
            movementFlags: 0x10000000u,
            field20Value: -0.5f,
            rootTreeFlagSet: true,
            childTreeFlagSet: true,
            queryDispatchSucceeded: false,
            rankingAccepted: true,
            rankingCandidateCount: 2u,
            rankingSelectedRecordIndex: 0,
            rankingReportedBestRatio: 0.001f,
            reportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairCallerTransactionTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.VariableTrace.TerrainQueryInvoked);
        Assert.Equal(1u, trace.VariableTrace.TerrainQuerySucceeded);
        Assert.Equal(1u, trace.VariableTrace.RankingAccepted);
        Assert.Equal(1u, trace.VariableTrace.ZeroClampedOutput);
        Assert.Equal(0u, trace.ZeroedOutputsOnVariableFailure);
        Assert.Equal(0f, reportedBestRatio, 6);
        Assert.Equal(0f, trace.OutputReportedBestRatio, 6);
    }
}
