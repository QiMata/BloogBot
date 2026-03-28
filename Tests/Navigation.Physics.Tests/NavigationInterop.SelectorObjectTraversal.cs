using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

public static partial class NavigationInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorBvhRecursionChildOutcome
    {
        public uint Result;
        public uint PendingCountDelta;
        public uint AcceptedCountDelta;
        public uint OverflowFlagsDelta;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorBvhRecursionStepTrace
    {
        public uint VisitLow;
        public uint VisitHigh;
        public uint EnteredLowChild;
        public uint EnteredHighChild;
        public uint ResultBefore;
        public uint ResultAfterLow;
        public uint ResultAfterHigh;
        public uint PendingCountBefore;
        public uint PendingCountAfterLow;
        public uint PendingCountAfterHigh;
        public uint AcceptedCountBefore;
        public uint AcceptedCountAfterLow;
        public uint AcceptedCountAfterHigh;
        public uint OverflowFlagsBefore;
        public uint OverflowFlagsAfterLow;
        public uint OverflowFlagsAfterHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorBvhRecursiveTraversalTrace
    {
        public uint VisitedNodeCount;
        public uint LeafNodeCount;
        public uint LeafCullRejectedCount;
        public uint LowRecursionCount;
        public uint HighRecursionCount;
        public uint LeafInvocationCount;
        public uint ResultAfterTraversal;
        public uint PendingCountAfterTraversal;
        public uint AcceptedCountAfterTraversal;
        public uint OverflowFlagsAfterTraversal;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] VisitedNodeIndices;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public ushort[] VisitedLeafTriangleIds;
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPlaneLeafQueueMutation", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorPlaneLeafQueueMutation(
        uint triangleIndex,
        uint stateMaskByte,
        uint firstOutcode,
        uint secondOutcode,
        uint thirdOutcode,
        out uint overflowFlags,
        [In, Out] ushort[] pendingIds,
        int pendingIdCapacity,
        out uint pendingCount,
        [In, Out] ushort[] acceptedIds,
        int acceptedIdCapacity,
        out uint acceptedCount,
        [In, Out] byte[] stateBytes,
        int stateByteCount,
        out SelectorLeafQueueMutationTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorBvhRecursionStep", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorBvhRecursionStep(
        in SelectorBvhChildTraversal traversal,
        in SelectorBvhRecursionChildOutcome lowChildOutcome,
        in SelectorBvhRecursionChildOutcome highChildOutcome,
        uint inputOverflowFlags,
        uint inputPendingCount,
        uint inputAcceptedCount,
        uint inputResult,
        out SelectorBvhRecursionStepTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTriangleLocalBoundsLeafQueueMutation", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWTriangleLocalBoundsLeafQueueMutation(
        uint triangleIndex,
        uint stateMaskByte,
        in Vector3 localBoundsMin,
        in Vector3 localBoundsMax,
        in Vector3 point0,
        in Vector3 point1,
        in Vector3 point2,
        out uint overflowFlags,
        [In, Out] ushort[] pendingIds,
        int pendingIdCapacity,
        out uint pendingCount,
        [In, Out] ushort[] acceptedIds,
        int acceptedIdCapacity,
        out uint acceptedCount,
        [In, Out] byte[] stateBytes,
        int stateByteCount,
        out SelectorLeafQueueMutationTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorBvhRecursiveTraversal", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorBvhRecursiveTraversal(
        [In] SelectorBvhNodeRecord[] nodes,
        int nodeCount,
        uint rootNodeIndex,
        in Vector3 boundsMin,
        in Vector3 boundsMax,
        uint stateMaskByte,
        [In] byte[] leafCullStates,
        int leafCullStateCount,
        [In] ushort[] leafTriangleIds,
        int leafTriangleIdCount,
        [In] byte[] predicateRejectedStates,
        int predicateRejectedStateCount,
        ref uint overflowFlags,
        [In, Out] ushort[] pendingIds,
        int pendingIdCapacity,
        ref uint pendingCount,
        [In, Out] ushort[] acceptedIds,
        int acceptedIdCapacity,
        ref uint acceptedCount,
        [In, Out] byte[] stateBytes,
        int stateByteCount,
        out SelectorBvhRecursiveTraversalTrace trace);
}
