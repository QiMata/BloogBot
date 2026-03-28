using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPairCommitGuardTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitGuard_ZeroIncomingPairWithStoredPairAndProbeHitRejects()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitGuard(
            incomingPair: new SelectorPair(),
            storedPair: new SelectorPair { First = 1.0f, Second = 2.0f },
            probeRejectOnStoredPairUnload: 1u,
            contextMatchesGlobal: 1u,
            hasAttachedPointer: 0u,
            attachedBit4Set: 0u,
            opaqueConsumerReturnValue: 17,
            out GroundedDriverSelectedPairCommitGuardTrace trace);

        Assert.Equal(0, result);
        Assert.Equal(GroundedDriverSelectedPairCommitGuardKind.RejectProbeHit, trace.GuardKind);
        Assert.Equal(1u, trace.ZeroIncomingPair);
        Assert.Equal(1u, trace.StoredPairNonZero);
        Assert.Equal(1u, trace.ProbeRejectChecked);
        Assert.Equal(1u, trace.ProbeRejected);
        Assert.Equal(0u, trace.CalledOpaqueConsumer);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitGuard_ContextMismatchRejects()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitGuard(
            incomingPair: new SelectorPair { First = 3.0f, Second = 4.0f },
            storedPair: new SelectorPair(),
            probeRejectOnStoredPairUnload: 0u,
            contextMatchesGlobal: 0u,
            hasAttachedPointer: 0u,
            attachedBit4Set: 0u,
            opaqueConsumerReturnValue: 11,
            out GroundedDriverSelectedPairCommitGuardTrace trace);

        Assert.Equal(0, result);
        Assert.Equal(GroundedDriverSelectedPairCommitGuardKind.RejectContextMismatch, trace.GuardKind);
        Assert.Equal(0u, trace.ProbeRejectChecked);
        Assert.Equal(0u, trace.CalledOpaqueConsumer);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitGuard_AttachedPointerWithoutBit4Rejects()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitGuard(
            incomingPair: new SelectorPair { First = 5.0f, Second = 6.0f },
            storedPair: new SelectorPair(),
            probeRejectOnStoredPairUnload: 0u,
            contextMatchesGlobal: 1u,
            hasAttachedPointer: 1u,
            attachedBit4Set: 0u,
            opaqueConsumerReturnValue: 19,
            out GroundedDriverSelectedPairCommitGuardTrace trace);

        Assert.Equal(0, result);
        Assert.Equal(GroundedDriverSelectedPairCommitGuardKind.RejectAttachedBit, trace.GuardKind);
        Assert.Equal(1u, trace.HasAttachedPointer);
        Assert.Equal(0u, trace.AttachedBit4Set);
        Assert.Equal(0u, trace.CalledOpaqueConsumer);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitGuard_VisibleGuardsPassToOpaqueConsumer()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitGuard(
            incomingPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            storedPair: new SelectorPair(),
            probeRejectOnStoredPairUnload: 0u,
            contextMatchesGlobal: 1u,
            hasAttachedPointer: 1u,
            attachedBit4Set: 1u,
            opaqueConsumerReturnValue: 23,
            out GroundedDriverSelectedPairCommitGuardTrace trace);

        Assert.Equal(23, result);
        Assert.Equal(GroundedDriverSelectedPairCommitGuardKind.CallOpaqueConsumer, trace.GuardKind);
        Assert.Equal(1u, trace.CalledOpaqueConsumer);
        Assert.Equal(23, trace.ReturnValue);
    }
}
