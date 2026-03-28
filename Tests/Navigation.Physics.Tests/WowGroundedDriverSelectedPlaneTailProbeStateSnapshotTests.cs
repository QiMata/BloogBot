using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailProbeStateSnapshotTests
{
    [Fact]
    public void CaptureWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot_CopiesVisibleDriverFieldsIntoSnapshotLayout()
    {
        var field44 = new Vector3(1.5f, -2.25f, 3.75f);
        var field50 = new Vector3(-4.5f, 5.25f, -6.75f);
        var field5c = new Vector3(7.125f, -8.5f, 9.875f);

        CaptureWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot(
            field44,
            field50,
            0x12345678u,
            field5c,
            0.625f,
            -0.875f,
            0x04000020u,
            0.9375f,
            out GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace trace);

        Assert.Equal(field44.X, trace.Field44Vector.X, 5);
        Assert.Equal(field44.Y, trace.Field44Vector.Y, 5);
        Assert.Equal(field44.Z, trace.Field44Vector.Z, 5);
        Assert.Equal(field50.X, trace.Field50Vector.X, 5);
        Assert.Equal(field50.Y, trace.Field50Vector.Y, 5);
        Assert.Equal(field50.Z, trace.Field50Vector.Z, 5);
        Assert.Equal(0x12345678u, trace.Field78);
        Assert.Equal(field5c.X, trace.Field5cVector.X, 5);
        Assert.Equal(field5c.Y, trace.Field5cVector.Y, 5);
        Assert.Equal(field5c.Z, trace.Field5cVector.Z, 5);
        Assert.Equal(0.625f, trace.Field68, 5);
        Assert.Equal(-0.875f, trace.Field6c, 5);
        Assert.Equal(0x04000020u, trace.Field40Flags);
        Assert.Equal(0.9375f, trace.Field84, 5);
    }

    [Fact]
    public void RestoreWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot_RoundTripsCapturedSnapshotWithoutMutation()
    {
        var snapshot = new GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
        {
            Field44Vector = new Vector3(-1.0f, 2.0f, -3.0f),
            Field50Vector = new Vector3(4.0f, -5.0f, 6.0f),
            Field78 = 0x00ABCDEFu,
            Field5cVector = new Vector3(-7.5f, 8.5f, -9.5f),
            Field68 = -0.25f,
            Field6c = 0.5f,
            Field40Flags = 0x20000020u,
            Field84 = 0.75f
        };

        RestoreWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot(
            snapshot,
            out GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace trace);

        Assert.Equal(snapshot.Field44Vector.X, trace.Field44Vector.X, 5);
        Assert.Equal(snapshot.Field44Vector.Y, trace.Field44Vector.Y, 5);
        Assert.Equal(snapshot.Field44Vector.Z, trace.Field44Vector.Z, 5);
        Assert.Equal(snapshot.Field50Vector.X, trace.Field50Vector.X, 5);
        Assert.Equal(snapshot.Field50Vector.Y, trace.Field50Vector.Y, 5);
        Assert.Equal(snapshot.Field50Vector.Z, trace.Field50Vector.Z, 5);
        Assert.Equal(snapshot.Field78, trace.Field78);
        Assert.Equal(snapshot.Field5cVector.X, trace.Field5cVector.X, 5);
        Assert.Equal(snapshot.Field5cVector.Y, trace.Field5cVector.Y, 5);
        Assert.Equal(snapshot.Field5cVector.Z, trace.Field5cVector.Z, 5);
        Assert.Equal(snapshot.Field68, trace.Field68, 5);
        Assert.Equal(snapshot.Field6c, trace.Field6c, 5);
        Assert.Equal(snapshot.Field40Flags, trace.Field40Flags);
        Assert.Equal(snapshot.Field84, trace.Field84, 5);
    }
}
