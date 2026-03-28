using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorLeafQueueMutationTests
{
    [Fact]
    public void EvaluateWoWSelectorLeafQueueMutation_SkipsMaskedTriangleBeforeAnyMutation()
    {
        ushort[] pendingIds = new ushort[0x2000];
        ushort[] acceptedIds = new ushort[0x2000];
        byte[] stateBytes = new byte[8];
        stateBytes[2] = 0x80;

        uint result = EvaluateWoWSelectorLeafQueueMutation(
            triangleIndex: 1u,
            stateMaskByte: 0x80u,
            predicateRejected: false,
            inputOverflowFlags: 0u,
            inputPendingIds: pendingIds,
            pendingIdCapacity: pendingIds.Length,
            inputPendingCount: 0u,
            inputAcceptedIds: acceptedIds,
            acceptedIdCapacity: acceptedIds.Length,
            inputAcceptedCount: 0u,
            inputStateBytes: stateBytes,
            stateByteCount: stateBytes.Length,
            out uint overflowFlags,
            outputPendingIds: pendingIds,
            outputPendingIdCapacity: pendingIds.Length,
            out uint pendingCount,
            outputAcceptedIds: acceptedIds,
            outputAcceptedIdCapacity: acceptedIds.Length,
            out uint acceptedCount,
            outputStateBytes: stateBytes,
            outputStateByteCount: stateBytes.Length,
            out SelectorLeafQueueMutationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(0u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(1u, trace.SkippedByMask);
        Assert.Equal(0u, trace.Overflowed);
        Assert.Equal(0u, trace.PendingEnqueued);
        Assert.Equal(0u, trace.AcceptedEnqueued);
        Assert.Equal(0x80u, trace.StateByteBefore);
        Assert.Equal(0x80u, trace.StateByteAfter);
    }

    [Fact]
    public void EvaluateWoWSelectorLeafQueueMutation_OverflowSetsFlagBeforeVisitedBitOrPredicate()
    {
        ushort[] pendingIds = new ushort[0x2000];
        ushort[] acceptedIds = new ushort[0x2000];
        byte[] stateBytes = new byte[4];

        uint result = EvaluateWoWSelectorLeafQueueMutation(
            triangleIndex: 1u,
            stateMaskByte: 0x80u,
            predicateRejected: false,
            inputOverflowFlags: 0u,
            inputPendingIds: pendingIds,
            pendingIdCapacity: pendingIds.Length,
            inputPendingCount: 0x2000u,
            inputAcceptedIds: acceptedIds,
            acceptedIdCapacity: acceptedIds.Length,
            inputAcceptedCount: 0u,
            inputStateBytes: stateBytes,
            stateByteCount: stateBytes.Length,
            out uint overflowFlags,
            outputPendingIds: pendingIds,
            outputPendingIdCapacity: pendingIds.Length,
            out uint pendingCount,
            outputAcceptedIds: acceptedIds,
            outputAcceptedIdCapacity: acceptedIds.Length,
            out uint acceptedCount,
            outputStateBytes: stateBytes,
            outputStateByteCount: stateBytes.Length,
            out SelectorLeafQueueMutationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, overflowFlags);
        Assert.Equal(0x2000u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(1u, trace.Overflowed);
        Assert.Equal(0u, trace.PendingEnqueued);
        Assert.Equal(0u, trace.VisitedBitSet);
        Assert.Equal(0u, trace.AcceptedEnqueued);
        Assert.Equal(0u, stateBytes[2]);
    }

    [Fact]
    public void EvaluateWoWSelectorLeafQueueMutation_AcceptPathEnqueuesPendingThenAcceptedAndSetsVisitedBit()
    {
        ushort[] pendingIds = new ushort[0x2000];
        ushort[] acceptedIds = new ushort[0x2000];
        byte[] stateBytes = new byte[8];

        uint result = EvaluateWoWSelectorLeafQueueMutation(
            triangleIndex: 3u,
            stateMaskByte: 0x80u,
            predicateRejected: false,
            inputOverflowFlags: 0u,
            inputPendingIds: pendingIds,
            pendingIdCapacity: pendingIds.Length,
            inputPendingCount: 0u,
            inputAcceptedIds: acceptedIds,
            acceptedIdCapacity: acceptedIds.Length,
            inputAcceptedCount: 0u,
            inputStateBytes: stateBytes,
            stateByteCount: stateBytes.Length,
            out uint overflowFlags,
            outputPendingIds: pendingIds,
            outputPendingIdCapacity: pendingIds.Length,
            out uint pendingCount,
            outputAcceptedIds: acceptedIds,
            outputAcceptedIdCapacity: acceptedIds.Length,
            out uint acceptedCount,
            outputStateBytes: stateBytes,
            outputStateByteCount: stateBytes.Length,
            out SelectorLeafQueueMutationTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(1u, pendingCount);
        Assert.Equal(1u, acceptedCount);
        Assert.Equal((ushort)3, pendingIds[0]);
        Assert.Equal((ushort)3, acceptedIds[0]);
        Assert.Equal(0x80, stateBytes[6]);
        Assert.Equal(1u, trace.PendingEnqueued);
        Assert.Equal(1u, trace.VisitedBitSet);
        Assert.Equal(0u, trace.PredicateRejected);
        Assert.Equal(1u, trace.AcceptedEnqueued);
    }

    [Fact]
    public void EvaluateWoWSelectorLeafQueueMutation_RejectedPredicateStillEnqueuesPendingAndVisitedBitOnly()
    {
        ushort[] pendingIds = new ushort[0x2000];
        ushort[] acceptedIds = new ushort[0x2000];
        byte[] stateBytes = new byte[8];

        uint result = EvaluateWoWSelectorLeafQueueMutation(
            triangleIndex: 2u,
            stateMaskByte: 0x80u,
            predicateRejected: true,
            inputOverflowFlags: 0u,
            inputPendingIds: pendingIds,
            pendingIdCapacity: pendingIds.Length,
            inputPendingCount: 0u,
            inputAcceptedIds: acceptedIds,
            acceptedIdCapacity: acceptedIds.Length,
            inputAcceptedCount: 0u,
            inputStateBytes: stateBytes,
            stateByteCount: stateBytes.Length,
            out uint overflowFlags,
            outputPendingIds: pendingIds,
            outputPendingIdCapacity: pendingIds.Length,
            out uint pendingCount,
            outputAcceptedIds: acceptedIds,
            outputAcceptedIdCapacity: acceptedIds.Length,
            out uint acceptedCount,
            outputStateBytes: stateBytes,
            outputStateByteCount: stateBytes.Length,
            out SelectorLeafQueueMutationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(1u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal((ushort)2, pendingIds[0]);
        Assert.Equal(0x80, stateBytes[4]);
        Assert.Equal(1u, trace.PendingEnqueued);
        Assert.Equal(1u, trace.VisitedBitSet);
        Assert.Equal(1u, trace.PredicateRejected);
        Assert.Equal(0u, trace.AcceptedEnqueued);
    }
}
