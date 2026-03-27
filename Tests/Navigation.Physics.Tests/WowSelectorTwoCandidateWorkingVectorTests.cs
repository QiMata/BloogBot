using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorTwoCandidateWorkingVectorTests
{
    [Fact]
    public void BuildWoWSelectorTwoCandidateWorkingVector_LineZGateReturnsSelectedNormal()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));
        SelectorSupportPlane firstCandidatePlane = CreatePlane(new Vector3(0.0f, 0.0f, 1.0f), 0.0f);
        SelectorSupportPlane secondCandidatePlane = CreatePlane(new Vector3(0.0f, 1.0f, 0.0f), 0.0f);

        Assert.True(BuildWoWSelectorTwoCandidateWorkingVector(
            position: new Vector3(0.0f, 0.0f, 0.0f),
            collisionRadius: 1.0f,
            selectedRecord,
            firstCandidatePlane,
            secondCandidatePlane,
            out Vector3 outVector,
            out SelectorTwoCandidateWorkingVectorTrace trace));

        Assert.Equal(1u, trace.ReturnedSelectedNormal);
        Assert.Equal(1u, trace.RejectedByLineZGate);
        Assert.Equal(0u, trace.RejectedBySelectedPlaneDotGate);
        Assert.Equal(0u, trace.RejectedByFootprintMismatch);
        Assert.Equal(0.0f, outVector.X, 6);
        Assert.Equal(0.0f, outVector.Y, 6);
        Assert.Equal(1.0f, outVector.Z, 6);
        Assert.Equal(-1.0f, trace.LineDirection.X, 6);
        Assert.Equal(0.0f, trace.LineDirection.Y, 6);
        Assert.Equal(0.0f, trace.LineDirection.Z, 6);
    }

    [Fact]
    public void BuildWoWSelectorTwoCandidateWorkingVector_FootprintMismatchReturnsSelectedNormal()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f));
        SelectorSupportPlane firstCandidatePlane = CreatePlane(new Vector3(1.0f, 0.0f, 0.0f), 0.0f);
        SelectorSupportPlane secondCandidatePlane = CreatePlane(new Vector3(0.0f, 0.7071068f, 0.7071068f), 0.0f);

        Assert.True(BuildWoWSelectorTwoCandidateWorkingVector(
            position: new Vector3(0.0f, 0.0f, 0.0f),
            collisionRadius: 1.0f,
            selectedRecord,
            firstCandidatePlane,
            secondCandidatePlane,
            out Vector3 outVector,
            out SelectorTwoCandidateWorkingVectorTrace trace));

        Assert.Equal(1u, trace.ReturnedSelectedNormal);
        Assert.Equal(0u, trace.RejectedByLineZGate);
        Assert.Equal(0u, trace.RejectedBySelectedPlaneDotGate);
        Assert.Equal(1u, trace.RejectedByFootprintMismatch);
        Assert.Equal(0.0f, outVector.X, 6);
        Assert.Equal(0.0f, outVector.Y, 6);
        Assert.Equal(1.0f, outVector.Z, 6);
        Assert.Equal(0.0f, trace.LineDirection.X, 6);
        Assert.Equal(-0.7071068f, trace.LineDirection.Y, 6);
        Assert.Equal(0.7071068f, trace.LineDirection.Z, 6);
    }

    [Fact]
    public void BuildWoWSelectorTwoCandidateWorkingVector_ConstructedVectorNegatesAgainstFirstCandidateNormal()
    {
        SelectorCandidateRecord selectedRecord = CreateSelectedRecord(
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(-1.0f, 0.0f, 0.0f));
        SelectorSupportPlane firstCandidatePlane = CreatePlane(new Vector3(0.0f, 1.0f, 0.0f), 0.0f);
        SelectorSupportPlane secondCandidatePlane = CreatePlane(new Vector3(-1.0f, 0.0f, 0.0f), 0.0f);

        Assert.True(BuildWoWSelectorTwoCandidateWorkingVector(
            position: new Vector3(0.0f, 0.0f, 0.0f),
            collisionRadius: 1.0f,
            selectedRecord,
            firstCandidatePlane,
            secondCandidatePlane,
            out Vector3 outVector,
            out SelectorTwoCandidateWorkingVectorTrace trace));

        Assert.Equal(0u, trace.ReturnedSelectedNormal);
        Assert.Equal(0u, trace.ReturnedNegatedFirstCandidate);
        Assert.Equal(1u, trace.ReturnedConstructedVector);
        Assert.Equal(1u, trace.OrientationNegated);
        Assert.Equal(2u, trace.SelectedEdgeIndex);
        Assert.Equal(0.0f, outVector.X, 6);
        Assert.Equal(-1.0f, outVector.Y, 6);
        Assert.Equal(0.0f, outVector.Z, 6);
        Assert.Equal(0.0f, trace.LineDirection.X, 6);
        Assert.Equal(0.0f, trace.LineDirection.Y, 6);
        Assert.Equal(1.0f, trace.LineDirection.Z, 6);
        Assert.Equal(-1.0f, trace.EdgeDirection.X, 6);
        Assert.Equal(0.0f, trace.EdgeDirection.Y, 6);
        Assert.Equal(0.0f, trace.EdgeDirection.Z, 6);
        Assert.Equal(0.0f, trace.WorkingVector.X, 6);
        Assert.Equal(-1.0f, trace.WorkingVector.Y, 6);
        Assert.Equal(0.0f, trace.WorkingVector.Z, 6);
    }

    private static SelectorCandidateRecord CreateSelectedRecord(Vector3 point0, Vector3 point1, Vector3 point2) =>
        new()
        {
            FilterPlane = new SelectorSupportPlane
            {
                Normal = new Vector3(0.0f, 0.0f, 1.0f),
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
