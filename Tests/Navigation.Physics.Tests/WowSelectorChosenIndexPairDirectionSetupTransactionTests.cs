using System;
using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorChosenIndexPairDirectionSetupTransactionTests
{
    private const uint MoveFlagSwimming = 0x00200000u;

    [Fact]
    public void DirectionSetup_NearZeroRequestedDistanceReturnsEarlyWithoutRanking()
    {
        SelectorSupportPlane[] candidatePlanes = new SelectorSupportPlane[5];

        bool result = EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
            records: Array.Empty<SelectorCandidateRecord>(),
            defaultPosition: new Vector3(1.0f, 2.0f, 3.0f),
            overridePosition: null,
            inputReportedBestRatioSeed: 0.75f,
            inputVerticalOffset: 1.25f,
            swimVerticalOffsetScale: 0.5f,
            selectorBaseMatchesSwimReference: false,
            movementFlags: 0u,
            requestedDistance: 0.001f,
            requestedDistanceClamp: 1.0f,
            testPoint: new Vector3(1.0f, 0.0f, 0.0f),
            candidateDirection: new Vector3(-1.0f, 0.0f, 0.0f),
            horizontalRadius: 0.5f,
            outCandidatePlanes: candidatePlanes,
            outCandidateCount: out uint candidateCount,
            outSelectedRecordIndex: out int selectedRecordIndex,
            outReportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.ZeroDistanceEarlySuccess);
        Assert.Equal(0u, trace.RankingInvoked);
        Assert.Equal(0u, candidateCount);
        Assert.Equal(-1, selectedRecordIndex);
        Assert.Equal(0.75f, reportedBestRatio, 6);
        Assert.Equal(0.75f, trace.OutputReportedBestRatio, 6);
    }

    [Fact]
    public void DirectionSetup_OverrideSwimScaleAndClampAreReflectedInTrace()
    {
        SelectorSupportPlane[] candidatePlanes = new SelectorSupportPlane[5];
        Vector3 overridePosition = new(-4.0f, 5.5f, 6.5f);

        bool result = EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
            records: Array.Empty<SelectorCandidateRecord>(),
            defaultPosition: new Vector3(1.0f, 2.0f, 3.0f),
            overridePosition: overridePosition,
            inputReportedBestRatioSeed: 0.5f,
            inputVerticalOffset: 2.0f,
            swimVerticalOffsetScale: 0.25f,
            selectorBaseMatchesSwimReference: true,
            movementFlags: MoveFlagSwimming,
            requestedDistance: 2.5f,
            requestedDistanceClamp: 1.0f,
            testPoint: new Vector3(1.0f, 0.0f, 0.0f),
            candidateDirection: new Vector3(-2.0f, 0.5f, 0.0f),
            horizontalRadius: 0.5f,
            outCandidatePlanes: candidatePlanes,
            outCandidateCount: out uint candidateCount,
            outSelectedRecordIndex: out int selectedRecordIndex,
            outReportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.UsedOverridePosition);
        Assert.Equal(1u, trace.AppliedSwimVerticalOffsetScale);
        Assert.Equal(1u, trace.RequestedDistanceClamped);
        Assert.Equal(overridePosition.X, trace.SelectedPosition.X, 6);
        Assert.Equal(overridePosition.Y, trace.SelectedPosition.Y, 6);
        Assert.Equal(overridePosition.Z, trace.SelectedPosition.Z, 6);
        Assert.Equal(0.5f, trace.OutputVerticalOffset, 6);
        Assert.Equal(1.0f, trace.ClampedRequestedDistance, 6);
        Assert.Equal(-2.0f, trace.ScaledCandidateDirection.X, 6);
        Assert.Equal(0.5f, trace.ScaledCandidateDirection.Y, 6);
        Assert.Equal(0.0f, trace.ScaledCandidateDirection.Z, 6);
        Assert.Equal(1u, trace.RankingInvoked);
        Assert.Equal(0u, trace.RankingAccepted);
        Assert.Equal(0u, candidateCount);
        Assert.Equal(-1, selectedRecordIndex);
        Assert.Equal(0.5f, reportedBestRatio, 6);
    }

    [Fact]
    public void DirectionSetup_RankingRejectStillReturnsSuccess()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(new Vector3(1.0f, 0.0f, 0.0f))
        ];
        SelectorSupportPlane[] candidatePlanes = new SelectorSupportPlane[5];

        bool result = EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
            records,
            defaultPosition: new Vector3(0.0f, 0.0f, 0.0f),
            overridePosition: null,
            inputReportedBestRatioSeed: 1.0f,
            inputVerticalOffset: 0.75f,
            swimVerticalOffsetScale: 1.0f,
            selectorBaseMatchesSwimReference: false,
            movementFlags: 0u,
            requestedDistance: 0.5f,
            requestedDistanceClamp: 1.0f,
            testPoint: new Vector3(1.0f, 0.0f, 0.0f),
            candidateDirection: new Vector3(-1.0f, 0.0f, 0.5f),
            horizontalRadius: 0.5f,
            outCandidatePlanes: candidatePlanes,
            outCandidateCount: out uint candidateCount,
            outSelectedRecordIndex: out int selectedRecordIndex,
            outReportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.RankingInvoked);
        Assert.Equal(0u, trace.RankingAccepted);
        Assert.Equal(0u, candidateCount);
        Assert.Equal(-1, selectedRecordIndex);
        Assert.Equal(1.0f, reportedBestRatio, 6);
        Assert.Equal(0.0f, candidatePlanes[0].Normal.X, 6);
        Assert.Equal(0.0f, candidatePlanes[0].Normal.Y, 6);
        Assert.Equal(1.0f, candidatePlanes[0].Normal.Z, 6);
    }

    [Fact]
    public void DirectionSetup_RankingAcceptancePropagatesCandidateBuffer()
    {
        SelectorCandidateRecord[] records =
        [
            CreateRecord(new Vector3(-1.0f, 0.0f, 0.0f))
        ];
        SelectorSupportPlane[] candidatePlanes = new SelectorSupportPlane[5];

        bool result = EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
            records,
            defaultPosition: new Vector3(0.0f, 0.0f, 0.0f),
            overridePosition: null,
            inputReportedBestRatioSeed: 1.0f,
            inputVerticalOffset: 0.75f,
            swimVerticalOffsetScale: 1.0f,
            selectorBaseMatchesSwimReference: false,
            movementFlags: 0u,
            requestedDistance: 0.5f,
            requestedDistanceClamp: 1.0f,
            testPoint: new Vector3(1.0f, 0.0f, 0.0f),
            candidateDirection: new Vector3(-1.0f, 0.0f, 0.5f),
            horizontalRadius: 0.5f,
            outCandidatePlanes: candidatePlanes,
            outCandidateCount: out uint candidateCount,
            outSelectedRecordIndex: out int selectedRecordIndex,
            outReportedBestRatio: out float reportedBestRatio,
            trace: out SelectorChosenIndexPairDirectionSetupTrace trace);

        Assert.True(result);
        Assert.Equal(1u, trace.RankingInvoked);
        Assert.Equal(1u, trace.RankingAccepted);
        Assert.True(candidateCount > 0);
        Assert.True(selectedRecordIndex >= 0);
        Assert.Equal(candidateCount, trace.OutputCandidateCount);
        Assert.Equal(selectedRecordIndex, trace.OutputSelectedRecordIndex);
        Assert.Equal(reportedBestRatio, trace.OutputReportedBestRatio, 6);
        Assert.NotEqual(1.0f, candidatePlanes[0].Normal.Z);
    }

    private static SelectorCandidateRecord CreateRecord(Vector3 filterNormal) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = filterNormal,
                PlaneDistance = 0f
            },
            Point0 = new Vector3(0.2f, 0f, 0f),
            Point1 = new Vector3(0.6f, 0.2f, 0f),
            Point2 = new Vector3(0.5f, -0.2f, 0f)
        };
}
