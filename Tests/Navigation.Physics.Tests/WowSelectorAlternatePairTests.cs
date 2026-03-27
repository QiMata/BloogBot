using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorAlternatePairTests
{
    [Fact]
    public void BuildWoWSelectorAlternatePair_BandFailureUsesNegatedInputMove()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.0f, 0.0f, 0.8f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));
        SelectorSupportPlane[] candidatePlanes =
        [
            CreatePlane(new Vector3(0.0f, 1.0f, 0.0f), 0.0f),
        ];
        Vector3 inputMove = new(1.0f, 2.0f, 3.0f);

        Assert.True(BuildWoWSelectorAlternatePair(
            position: new Vector3(0.0f, 0.0f, 0.0f),
            collisionRadius: 1.0f,
            selectedRecord,
            candidatePlanes,
            candidateCount: 1u,
            inputMove,
            windowStartScalar: 1.0f,
            windowEndScalar: 3.0f,
            out SelectorPair outPair,
            out SelectorAlternatePairTrace trace));

        Assert.Equal(1u, trace.UsedNegatedInputWorkingVector);
        Assert.Equal(0u, trace.UsedNegatedFirstCandidate);
        Assert.Equal(0u, trace.UsedTwoCandidateBuilder);
        Assert.Equal(0u, trace.UsedSelectedContactNormal);
        Assert.Equal(1u, trace.NormalizedHorizontal);
        Assert.Equal(2.236068f, trace.HorizontalMagnitude, 5);
        Assert.Equal(2.236068f, trace.Denominator, 5);
        Assert.Equal(12.52198f, trace.Scale, 5);
        Assert.Equal(-1.0f, trace.WorkingVector.X, 6);
        Assert.Equal(-2.0f, trace.WorkingVector.Y, 6);
        Assert.Equal(-3.0f, trace.WorkingVector.Z, 6);
        Assert.Equal(-5.6f, outPair.First, 5);
        Assert.Equal(-11.2f, outPair.Second, 5);
    }

    [Fact]
    public void BuildWoWSelectorAlternatePair_ThreeCandidateModeUsesSelectedContactNormal()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.6f, 0.8f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));
        SelectorSupportPlane[] candidatePlanes =
        [
            CreatePlane(new Vector3(1.0f, 0.0f, 0.0f), 0.0f),
            CreatePlane(new Vector3(0.0f, 1.0f, 0.0f), 0.0f),
            CreatePlane(new Vector3(0.0f, 0.0f, 1.0f), 0.0f),
        ];
        Vector3 inputMove = new(1.0f, 0.0f, 0.0f);

        Assert.True(BuildWoWSelectorAlternatePair(
            position: new Vector3(0.0f, 0.0f, 0.0f),
            collisionRadius: 1.0f,
            selectedRecord,
            candidatePlanes,
            candidateCount: 3u,
            inputMove,
            windowStartScalar: 0.0f,
            windowEndScalar: 2.0f,
            out SelectorPair outPair,
            out SelectorAlternatePairTrace trace));

        Assert.Equal(0u, trace.UsedNegatedInputWorkingVector);
        Assert.Equal(0u, trace.UsedNegatedFirstCandidate);
        Assert.Equal(0u, trace.UsedTwoCandidateBuilder);
        Assert.Equal(1u, trace.UsedSelectedContactNormal);
        Assert.Equal(1u, trace.NormalizedHorizontal);
        Assert.Equal(1.0f, trace.HorizontalMagnitude, 6);
        Assert.Equal(1.0f, trace.Denominator, 6);
        Assert.Equal(-1.2f, trace.Scale, 6);
        Assert.Equal(0.6f, trace.WorkingVector.X, 6);
        Assert.Equal(0.8f, trace.WorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.WorkingVector.Z, 6);
        Assert.Equal(-0.72f, outPair.First, 6);
        Assert.Equal(-0.96f, outPair.Second, 6);
    }

    [Fact]
    public void BuildWoWSelectorAlternatePair_TwoCandidateModeUsesBinaryWorkingVector()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.0f, 0.0f, 0.5f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(-1.0f, 0.0f, 0.0f));
        SelectorSupportPlane[] candidatePlanes =
        [
            CreatePlane(new Vector3(0.0f, 1.0f, 0.0f), 0.0f),
            CreatePlane(new Vector3(-1.0f, 0.0f, 0.0f), 0.0f),
        ];
        Vector3 inputMove = new(1.0f, 2.0f, 0.0f);

        Assert.True(BuildWoWSelectorAlternatePair(
            position: new Vector3(0.0f, 0.0f, 0.0f),
            collisionRadius: 1.0f,
            selectedRecord,
            candidatePlanes,
            candidateCount: 2u,
            inputMove,
            windowStartScalar: 1.0f,
            windowEndScalar: 2.0f,
            out SelectorPair outPair,
            out SelectorAlternatePairTrace trace));

        Assert.Equal(1u, trace.NormalizedHorizontal);
        Assert.Equal(1.0f, trace.HorizontalMagnitude, 6);
        Assert.Equal(1.0f, trace.Denominator, 6);
        Assert.Equal(2.0f, trace.Scale, 6);
        Assert.Equal(0.0f, trace.WorkingVector.X, 6);
        Assert.Equal(-1.0f, trace.WorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.WorkingVector.Z, 6);
        Assert.Equal(0.0f, outPair.First, 6);
        Assert.Equal(-2.0f, outPair.Second, 6);
    }

    private static SelectorCandidateRecord CreateSelectedRecord(Vector3 normal, Vector3 point0, Vector3 point1, Vector3 point2) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = normal,
                PlaneDistance = 0.0f,
            },
            Point0 = point0,
            Point1 = point1,
            Point2 = point2,
        };

    private static SelectorSupportPlane CreatePlane(Vector3 normal, float planeDistance) =>
        new()
        {
            Normal = normal,
            PlaneDistance = planeDistance,
        };
}
