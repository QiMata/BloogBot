using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorLeafQueueMutationWrapperTests
{
    [Fact]
    public void EvaluateWoWSelectorPlaneLeafQueueMutation_BindsTheSelectorPlanePredicate()
    {
        ushort[] pendingIds = new ushort[0x2000];
        ushort[] acceptedIds = new ushort[0x2000];
        byte[] stateBytes = new byte[8];

        uint result = EvaluateWoWSelectorPlaneLeafQueueMutation(
            triangleIndex: 1u,
            stateMaskByte: 0x80u,
            firstOutcode: 0x01u,
            secondOutcode: 0x11u,
            thirdOutcode: 0x21u,
            overflowFlags: out uint overflowFlags,
            pendingIds: pendingIds,
            pendingIdCapacity: pendingIds.Length,
            pendingCount: out uint pendingCount,
            acceptedIds: acceptedIds,
            acceptedIdCapacity: acceptedIds.Length,
            acceptedCount: out uint acceptedCount,
            stateBytes: stateBytes,
            stateByteCount: stateBytes.Length,
            trace: out SelectorLeafQueueMutationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(1u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(1u, trace.PredicateRejected);
        Assert.Equal(1u, trace.PendingEnqueued);
        Assert.Equal(1u, trace.VisitedBitSet);
        Assert.Equal(0x80u, stateBytes[2]);
    }

    [Fact]
    public void EvaluateWoWTriangleLocalBoundsLeafQueueMutation_BindsTheLocalBoundsPredicate()
    {
        ushort[] pendingIds = new ushort[0x2000];
        ushort[] acceptedIds = new ushort[0x2000];
        byte[] stateBytes = new byte[8];

        uint result = EvaluateWoWTriangleLocalBoundsLeafQueueMutation(
            triangleIndex: 2u,
            stateMaskByte: 0x80u,
            localBoundsMin: new Vector3(-1.0f, -1.0f, -1.0f),
            localBoundsMax: new Vector3(1.0f, 1.0f, 1.0f),
            point0: new Vector3(-2.0f, 0.0f, 0.0f),
            point1: new Vector3(-3.0f, 0.25f, 0.0f),
            point2: new Vector3(-4.0f, -0.25f, 0.5f),
            overflowFlags: out uint overflowFlags,
            pendingIds: pendingIds,
            pendingIdCapacity: pendingIds.Length,
            pendingCount: out uint pendingCount,
            acceptedIds: acceptedIds,
            acceptedIdCapacity: acceptedIds.Length,
            acceptedCount: out uint acceptedCount,
            stateBytes: stateBytes,
            stateByteCount: stateBytes.Length,
            trace: out SelectorLeafQueueMutationTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(1u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(1u, trace.PredicateRejected);
        Assert.Equal(1u, trace.PendingEnqueued);
        Assert.Equal(1u, trace.VisitedBitSet);
        Assert.Equal(0x80u, stateBytes[4]);
    }
}
