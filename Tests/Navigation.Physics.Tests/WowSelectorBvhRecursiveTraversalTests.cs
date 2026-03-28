using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowSelectorBvhRecursiveTraversalTests
{
    [Fact]
    public void EvaluateWoWSelectorBvhRecursiveTraversal_StraddlingNodeVisitsLowLeafBeforeHighLeaf()
    {
        SelectorBvhNodeRecord[] nodes =
        {
            new()
            {
                ControlWord = 0x0,
                LowChildIndex = 1,
                HighChildIndex = 2,
                SplitCoordinate = 0.5f,
            },
            new()
            {
                ControlWord = 0x4,
                LeafTriangleCount = 2,
                LeafTriangleStartIndex = 0,
            },
            new()
            {
                ControlWord = 0x4,
                LeafTriangleCount = 1,
                LeafTriangleStartIndex = 2,
            },
        };

        byte[] leafCullStates = new byte[nodes.Length];
        ushort[] leafTriangleIds = { 4, 2, 7 };
        byte[] predicateRejectedStates = new byte[8];
        ushort[] pendingIds = new ushort[8];
        ushort[] acceptedIds = new ushort[8];
        byte[] stateBytes = new byte[32];
        uint overflowFlags = 0;
        uint pendingCount = 0;
        uint acceptedCount = 0;

        uint result = EvaluateWoWSelectorBvhRecursiveTraversal(
            nodes,
            nodes.Length,
            0,
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            stateMaskByte: 0x01,
            leafCullStates,
            leafCullStates.Length,
            leafTriangleIds,
            leafTriangleIds.Length,
            predicateRejectedStates,
            predicateRejectedStates.Length,
            ref overflowFlags,
            pendingIds,
            pendingIds.Length,
            ref pendingCount,
            acceptedIds,
            acceptedIds.Length,
            ref acceptedCount,
            stateBytes,
            stateBytes.Length,
            out SelectorBvhRecursiveTraversalTrace trace);

        Assert.Equal(1u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(3u, pendingCount);
        Assert.Equal(3u, acceptedCount);
        Assert.Equal(3u, trace.VisitedNodeCount);
        Assert.Equal(2u, trace.LeafNodeCount);
        Assert.Equal(0u, trace.LeafCullRejectedCount);
        Assert.Equal(1u, trace.LowRecursionCount);
        Assert.Equal(1u, trace.HighRecursionCount);
        Assert.Equal(3u, trace.LeafInvocationCount);
        Assert.Equal(0u, trace.VisitedNodeIndices[0]);
        Assert.Equal(1u, trace.VisitedNodeIndices[1]);
        Assert.Equal(2u, trace.VisitedNodeIndices[2]);
        Assert.Equal((ushort)4, trace.VisitedLeafTriangleIds[0]);
        Assert.Equal((ushort)2, trace.VisitedLeafTriangleIds[1]);
        Assert.Equal((ushort)7, trace.VisitedLeafTriangleIds[2]);
        Assert.Equal((ushort)4, pendingIds[0]);
        Assert.Equal((ushort)2, pendingIds[1]);
        Assert.Equal((ushort)7, pendingIds[2]);
        Assert.Equal((ushort)4, acceptedIds[0]);
        Assert.Equal((ushort)2, acceptedIds[1]);
        Assert.Equal((ushort)7, acceptedIds[2]);
    }

    [Fact]
    public void EvaluateWoWSelectorBvhRecursiveTraversal_CulledLeafSkipsLeafTriangleIteration()
    {
        SelectorBvhNodeRecord[] nodes =
        {
            new()
            {
                ControlWord = 0x4,
                LeafTriangleCount = 2,
                LeafTriangleStartIndex = 0,
            },
        };

        byte[] leafCullStates = { 1 };
        ushort[] leafTriangleIds = { 3, 5 };
        byte[] predicateRejectedStates = new byte[6];
        ushort[] pendingIds = new ushort[4];
        ushort[] acceptedIds = new ushort[4];
        byte[] stateBytes = new byte[16];
        uint overflowFlags = 0;
        uint pendingCount = 0;
        uint acceptedCount = 0;

        uint result = EvaluateWoWSelectorBvhRecursiveTraversal(
            nodes,
            nodes.Length,
            0,
            new Vector3(-2.0f, -2.0f, -2.0f),
            new Vector3(2.0f, 2.0f, 2.0f),
            stateMaskByte: 0x01,
            leafCullStates,
            leafCullStates.Length,
            leafTriangleIds,
            leafTriangleIds.Length,
            predicateRejectedStates,
            predicateRejectedStates.Length,
            ref overflowFlags,
            pendingIds,
            pendingIds.Length,
            ref pendingCount,
            acceptedIds,
            acceptedIds.Length,
            ref acceptedCount,
            stateBytes,
            stateBytes.Length,
            out SelectorBvhRecursiveTraversalTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(0u, overflowFlags);
        Assert.Equal(0u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(1u, trace.VisitedNodeCount);
        Assert.Equal(1u, trace.LeafNodeCount);
        Assert.Equal(1u, trace.LeafCullRejectedCount);
        Assert.Equal(0u, trace.LeafInvocationCount);
        Assert.Equal(0u, trace.VisitedNodeIndices[0]);
    }

    [Fact]
    public void EvaluateWoWSelectorBvhRecursiveTraversal_OverflowOnLowLeafStillWalksHighLeafAndPreservesQueueCounts()
    {
        SelectorBvhNodeRecord[] nodes =
        {
            new()
            {
                ControlWord = 0x0,
                LowChildIndex = 1,
                HighChildIndex = 2,
                SplitCoordinate = 0.0f,
            },
            new()
            {
                ControlWord = 0x4,
                LeafTriangleCount = 1,
                LeafTriangleStartIndex = 0,
            },
            new()
            {
                ControlWord = 0x4,
                LeafTriangleCount = 1,
                LeafTriangleStartIndex = 1,
            },
        };

        byte[] leafCullStates = new byte[nodes.Length];
        ushort[] leafTriangleIds = { 0, 1 };
        byte[] predicateRejectedStates = new byte[2];
        ushort[] pendingIds = new ushort[1] { 99 };
        ushort[] acceptedIds = new ushort[1];
        byte[] stateBytes = new byte[8];
        uint overflowFlags = 0;
        uint pendingCount = 1;
        uint acceptedCount = 0;

        uint result = EvaluateWoWSelectorBvhRecursiveTraversal(
            nodes,
            nodes.Length,
            0,
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            stateMaskByte: 0x01,
            leafCullStates,
            leafCullStates.Length,
            leafTriangleIds,
            leafTriangleIds.Length,
            predicateRejectedStates,
            predicateRejectedStates.Length,
            ref overflowFlags,
            pendingIds,
            pendingIds.Length,
            ref pendingCount,
            acceptedIds,
            acceptedIds.Length,
            ref acceptedCount,
            stateBytes,
            stateBytes.Length,
            out SelectorBvhRecursiveTraversalTrace trace);

        Assert.Equal(0u, result);
        Assert.Equal(1u, overflowFlags);
        Assert.Equal(1u, pendingCount);
        Assert.Equal(0u, acceptedCount);
        Assert.Equal(2u, trace.LeafInvocationCount);
        Assert.Equal(1u, trace.LowRecursionCount);
        Assert.Equal(1u, trace.HighRecursionCount);
        Assert.Equal((ushort)0, trace.VisitedLeafTriangleIds[0]);
        Assert.Equal((ushort)1, trace.VisitedLeafTriangleIds[1]);
        Assert.Equal((ushort)99, pendingIds[0]);
        Assert.Equal(0u, stateBytes[0]);
        Assert.Equal(0u, stateBytes[2]);
    }
}
