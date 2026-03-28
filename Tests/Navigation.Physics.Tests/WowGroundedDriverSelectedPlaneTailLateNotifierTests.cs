using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneTailLateNotifierTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier_NonAlternatePathAddsRoundedMillisecondsToField78()
    {
        var snapshot = new GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
        {
            Field84 = 0.75f
        };

        int returnedMilliseconds = EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier(
            roundedMilliseconds: 7,
            alternateUnitZStateBit: 0u,
            field78: 100,
            notifyRequested: 0u,
            alternateWindowCommitBase: 0,
            sidecarStatePresent: 0u,
            bit20InitiallySet: 0u,
            commitGuardPassed: 0u,
            bit20StillSet: 0u,
            fieldA0: 1.0f,
            lowNibbleFlags: 0u,
            snapshot,
            rerouteLoopUsed: 0u,
            out GroundedDriverSelectedPlaneTailLateNotifierTrace trace);

        Assert.Equal(7, returnedMilliseconds);
        Assert.Equal(1u, trace.AddedRoundedMillisecondsToField78);
        Assert.Equal(107, trace.OutputField78);
        Assert.Equal(1u, trace.InvokedCommitGuard);
        Assert.Equal(0u, trace.ReturnedEarlyAfterCommitGuard);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier_AlternateNotifyPathUsesCommitCallsAndCanReturnEarly()
    {
        var snapshot = new GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
        {
            Field84 = 0.25f
        };

        int returnedMilliseconds = EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier(
            roundedMilliseconds: 5,
            alternateUnitZStateBit: 1u,
            field78: 90,
            notifyRequested: 1u,
            alternateWindowCommitBase: 12,
            sidecarStatePresent: 1u,
            bit20InitiallySet: 0u,
            commitGuardPassed: 1u,
            bit20StillSet: 0u,
            fieldA0: 0.0f,
            lowNibbleFlags: 0u,
            snapshot,
            rerouteLoopUsed: 0u,
            out GroundedDriverSelectedPlaneTailLateNotifierTrace trace);

        Assert.Equal(5, returnedMilliseconds);
        Assert.Equal(1u, trace.InvokedAlternateWindowCommit);
        Assert.Equal(17, trace.AlternateWindowCommitArgument);
        Assert.Equal(1u, trace.InvokedSidecarCommit);
        Assert.Equal(1u, trace.InvokedCommitGuard);
        Assert.Equal(1u, trace.CommitGuardPassed);
        Assert.Equal(1u, trace.ReturnedEarlyAfterCommitGuard);
        Assert.Equal(90, trace.OutputField78);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier_Bit20RefreshCanRestoreSnapshotAndRunCleanup()
    {
        var snapshot = new GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
        {
            Field44Vector = new Vector3(1.0f, 2.0f, 3.0f),
            Field84 = 0.5f
        };

        int returnedMilliseconds = EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier(
            roundedMilliseconds: 9,
            alternateUnitZStateBit: 0u,
            field78: 10,
            notifyRequested: 1u,
            alternateWindowCommitBase: 0,
            sidecarStatePresent: 1u,
            bit20InitiallySet: 1u,
            commitGuardPassed: 0u,
            bit20StillSet: 1u,
            fieldA0: -0.25f,
            lowNibbleFlags: 0x3u,
            snapshot,
            rerouteLoopUsed: 1u,
            out GroundedDriverSelectedPlaneTailLateNotifierTrace trace);

        Assert.Equal(9, returnedMilliseconds);
        Assert.Equal(19, trace.OutputField78);
        Assert.Equal(1u, trace.InvokedBit20Refresh);
        Assert.Equal(1u, trace.Bit20StillSet);
        Assert.Equal(1u, trace.NegativeFieldA0);
        Assert.Equal(1u, trace.LowNibbleFlagsPresent);
        Assert.Equal(1u, trace.RestoredSnapshotState);
        Assert.Equal(1u, trace.InvokedRerouteCleanup);
        Assert.Equal(snapshot.Field44Vector.X, trace.SnapshotTrace.Field44Vector.X, 5);
        Assert.Equal(snapshot.Field84, trace.SnapshotTrace.Field84, 5);
    }
}
