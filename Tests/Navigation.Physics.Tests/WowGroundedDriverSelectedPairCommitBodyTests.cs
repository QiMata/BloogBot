using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPairCommitBodyTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitBody_UnchangedPairRejectsBeforeAnyPipelineWork()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitBody(
            incomingPair: new SelectorPair { First = 3.0f, Second = 4.0f },
            storedPair: new SelectorPair { First = 3.0f, Second = 4.0f },
            incomingPairValidatorAccepted: 1u,
            hasTransformConsumer: 1u,
            storedPhaseScalar: 1.25f,
            incomingPhaseScalar: 2.5f,
            out GroundedDriverSelectedPairCommitBodyTrace trace);

        Assert.Equal(0, result);
        Assert.Equal(GroundedDriverSelectedPairCommitBodyKind.RejectUnchangedPair, trace.CommitKind);
        Assert.Equal(1u, trace.IncomingPairMatchesStoredPair);
        Assert.Equal(0u, trace.CalledIncomingPairValidator);
        Assert.Equal(0u, trace.InitializedStoredTransformIdentity);
        Assert.Equal(0u, trace.WroteCommittedPair);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitBody_NonZeroIncomingPairValidatorRejectsBeforeIdentitySetup()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitBody(
            incomingPair: new SelectorPair { First = 5.0f, Second = 6.0f },
            storedPair: new SelectorPair(),
            incomingPairValidatorAccepted: 0u,
            hasTransformConsumer: 1u,
            storedPhaseScalar: 1.0f,
            incomingPhaseScalar: 2.0f,
            out GroundedDriverSelectedPairCommitBodyTrace trace);

        Assert.Equal(0, result);
        Assert.Equal(GroundedDriverSelectedPairCommitBodyKind.RejectIncomingPairValidator, trace.CommitKind);
        Assert.Equal(1u, trace.IncomingPairNonZero);
        Assert.Equal(1u, trace.CalledIncomingPairValidator);
        Assert.Equal(0u, trace.IncomingPairValidatorAccepted);
        Assert.Equal(0u, trace.InitializedStoredTransformIdentity);
        Assert.Equal(0u, trace.WroteCommittedPair);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitBody_StoredPairOnlyProcessesStoredPhaseAndCommitsZeroPair()
    {
        int result = EvaluateWoWGroundedDriverSelectedPairCommitBody(
            incomingPair: new SelectorPair(),
            storedPair: new SelectorPair { First = 7.0f, Second = 8.0f },
            incomingPairValidatorAccepted: 0u,
            hasTransformConsumer: 1u,
            storedPhaseScalar: 1.75f,
            incomingPhaseScalar: 2.25f,
            out GroundedDriverSelectedPairCommitBodyTrace trace);

        Assert.Equal(1, result);
        Assert.Equal(GroundedDriverSelectedPairCommitBodyKind.CommitPair, trace.CommitKind);
        Assert.Equal(0u, trace.CalledIncomingPairValidator);
        Assert.Equal(1u, trace.InitializedStoredTransformIdentity);
        Assert.Equal(1u, trace.InitializedIncomingTransformIdentity);
        Assert.Equal(1u, trace.ProcessedStoredPair);
        Assert.Equal(0u, trace.ProcessedIncomingPair);
        Assert.Equal(1u, trace.CalledStoredAttachmentBridge);
        Assert.Equal(0u, trace.CalledIncomingAttachmentBridge);
        Assert.Equal(1.75f, trace.StoredPhaseScalar, 6);
        Assert.Equal(1u, trace.WroteCommittedPair);
        Assert.Equal(1u, trace.CalledCommitNotification);
        Assert.Equal(0.0f, trace.OutputCommittedPair.First, 6);
        Assert.Equal(0.0f, trace.OutputCommittedPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitBody_IncomingPairOnlyProcessesIncomingPhaseAndCommitsInputPair()
    {
        SelectorPair incomingPair = new() { First = 9.0f, Second = 10.0f };

        int result = EvaluateWoWGroundedDriverSelectedPairCommitBody(
            incomingPair: incomingPair,
            storedPair: new SelectorPair(),
            incomingPairValidatorAccepted: 1u,
            hasTransformConsumer: 1u,
            storedPhaseScalar: 1.5f,
            incomingPhaseScalar: 2.75f,
            out GroundedDriverSelectedPairCommitBodyTrace trace);

        Assert.Equal(1, result);
        Assert.Equal(GroundedDriverSelectedPairCommitBodyKind.CommitPair, trace.CommitKind);
        Assert.Equal(1u, trace.CalledIncomingPairValidator);
        Assert.Equal(1u, trace.IncomingPairValidatorAccepted);
        Assert.Equal(1u, trace.InitializedStoredTransformIdentity);
        Assert.Equal(1u, trace.InitializedIncomingTransformIdentity);
        Assert.Equal(0u, trace.ProcessedStoredPair);
        Assert.Equal(1u, trace.ProcessedIncomingPair);
        Assert.Equal(0u, trace.CalledStoredAttachmentBridge);
        Assert.Equal(1u, trace.CalledIncomingAttachmentBridge);
        Assert.Equal(2.75f, trace.IncomingPhaseScalar, 6);
        Assert.Equal(incomingPair.First, trace.OutputCommittedPair.First, 6);
        Assert.Equal(incomingPair.Second, trace.OutputCommittedPair.Second, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPairCommitBody_BothStoredAndIncomingPairsProcessBothVisiblePipelines()
    {
        SelectorPair incomingPair = new() { First = 11.0f, Second = 12.0f };
        SelectorPair storedPair = new() { First = 1.0f, Second = 2.0f };

        int result = EvaluateWoWGroundedDriverSelectedPairCommitBody(
            incomingPair: incomingPair,
            storedPair: storedPair,
            incomingPairValidatorAccepted: 1u,
            hasTransformConsumer: 1u,
            storedPhaseScalar: 3.5f,
            incomingPhaseScalar: 4.5f,
            out GroundedDriverSelectedPairCommitBodyTrace trace);

        Assert.Equal(1, result);
        Assert.Equal(GroundedDriverSelectedPairCommitBodyKind.CommitPair, trace.CommitKind);
        Assert.Equal(1u, trace.ProcessedStoredPair);
        Assert.Equal(1u, trace.ProcessedIncomingPair);
        Assert.Equal(1u, trace.CalledStoredAttachmentBridge);
        Assert.Equal(1u, trace.CalledIncomingAttachmentBridge);
        Assert.Equal(1u, trace.AppliedStoredTransformScalar);
        Assert.Equal(1u, trace.AppliedStoredTransformMatrix);
        Assert.Equal(1u, trace.AppliedStoredTransformFinalize);
        Assert.Equal(1u, trace.AppliedIncomingTransformScalar);
        Assert.Equal(1u, trace.AppliedIncomingTransformMatrix);
        Assert.Equal(1u, trace.AppliedIncomingTransformFinalize);
        Assert.Equal(3.5f, trace.StoredPhaseScalar, 6);
        Assert.Equal(4.5f, trace.IncomingPhaseScalar, 6);
        Assert.Equal(incomingPair.First, trace.OutputCommittedPair.First, 6);
        Assert.Equal(incomingPair.Second, trace.OutputCommittedPair.Second, 6);
    }
}
