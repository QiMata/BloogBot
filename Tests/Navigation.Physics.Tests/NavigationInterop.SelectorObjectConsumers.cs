using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

public static partial class NavigationInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectConsumerDispatchTrace
    {
        public uint CalledTraversal;
        public uint CalledAcceptedListConsumer;
        public uint CalledRasterConsumer;
        public uint ClearedQueuedVisitedBits;
        public uint QueueMutationObserved;
        public uint InputFlags;
        public uint PendingCountBeforeCleanup;
        public uint AcceptedCountBeforeCleanup;
        public uint PendingCountAfterCleanup;
        public uint AcceptedCountAfterCleanup;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorAcceptedListConsumerTrace
    {
        public uint PreprocessedPendingQueue;
        public uint PreprocessedAcceptedQueue;
        public uint PendingPreprocessIterations;
        public uint AcceptedPreprocessIterations;
        public uint Helper6acdd0CallCount;
        public uint Helper7bca80CallCount;
        public uint Helper6bce50CallCount;
        public uint Helper6a98e0CallCount;
        public uint OutputQueueFlags;
        public uint RecordSlotReserved;
        public uint RecordOverflowFlagSet;
        public uint TriangleWordSpanReserved;
        public uint TriangleWordOverflowFlagSet;
        public uint AcceptedIdSpanReserved;
        public uint AcceptedIdOverflowFlagSet;
        public uint ReservedTriangleWordStart;
        public uint ReservedTriangleWordCount;
        public uint ReservedAcceptedIdStart;
        public uint ReservedAcceptedIdCount;
        public uint CopiedTriangleWordCount;
        public uint CopiedAcceptedIdCount;
        public uint MinTriangleVertexIndex;
        public uint MaxTriangleVertexIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorAcceptedListConsumerRecordSlotTrace
    {
        public uint RecordReserved;
        public uint ZeroInitializedDwordCount;
        public uint RecordIndex;
        public uint OwnerPayloadToken;
        public uint VertexStreamToken;
        public uint MetadataToken;
        public uint TriangleWordBufferToken;
        public uint AcceptedIdBufferToken;
        public uint OwnerContextToken;
        public ushort TriangleWordCountField;
        public ushort AcceptedIdCountField;
        public ushort MinTriangleVertexIndex;
        public ushort MaxTriangleVertexIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorAcceptedListConsumerPreprocessTrace
    {
        public uint Executed;
        public uint SourceKind;
        public uint SourceTriangleIndex;
        public uint SourceTriangleWordBase;
        public uint DebugColorToken;
        public uint OwnerPayloadToken;
        public uint Helper6acdd0CallCount;
        public uint Helper7bca80CallCount;
        public uint Helper6bce50CallCount;
        public uint Helper6a98e0CallCount;
        public uint NormalizeHelperZeroArg;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] SupportVertexTokens;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] SupportVertexIndices;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] LocalSlotOffsets;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorAcceptedListConsumerPreprocessLoopTrace
    {
        public uint PreprocessEnabled;
        public uint SourceKind;
        public uint DebugColorToken;
        public uint OwnerPayloadToken;
        public uint SourceCount;
        public uint ExecutedIterationCount;
        public uint StoredIterationCount;
        public uint Helper6acdd0CallCount;
        public uint Helper7bca80CallCount;
        public uint Helper6bce50CallCount;
        public uint Helper6a98e0CallCount;
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectConsumerDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectConsumerDispatch(
        uint inputFlags,
        uint queueMutationCountBefore,
        uint queueMutationCountAfterConsumers,
        [In, Out] ushort[] pendingIds,
        int pendingIdCapacity,
        ref uint pendingCount,
        ref uint acceptedCount,
        [In, Out] byte[] stateBytes,
        int stateByteCount,
        out SelectorObjectConsumerDispatchTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAcceptedListConsumerVisibleBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
        uint globalFlags,
        uint inputQueueFlags,
        uint inputConsumerFlags,
        ref uint recordReservationCount,
        ref uint triangleWordCount,
        ref uint acceptedIdCount,
        [In] ushort[] pendingIds,
        int pendingCount,
        [In] ushort[] acceptedIds,
        int acceptedCount,
        [In] ushort[] triangleVertexIndices,
        int triangleVertexIndexCount,
        [In, Out] ushort[] outputAcceptedIds,
        int outputAcceptedIdCapacity,
        [In, Out] ushort[] outputTriangleWords,
        int outputTriangleWordCapacity,
        out SelectorAcceptedListConsumerTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAcceptedListConsumerRecordWrite", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorAcceptedListConsumerRecordWrite(
        uint globalFlags,
        uint inputQueueFlags,
        uint inputConsumerFlags,
        uint ownerContextToken,
        uint vertexStreamToken,
        uint metadataToken,
        uint outputTriangleWordBaseToken,
        uint outputAcceptedIdBaseToken,
        ref uint recordReservationCount,
        ref uint triangleWordCount,
        ref uint acceptedIdCount,
        [In] ushort[] pendingIds,
        int pendingCount,
        [In] ushort[] acceptedIds,
        int acceptedCount,
        [In] ushort[] triangleVertexIndices,
        int triangleVertexIndexCount,
        [In, Out] ushort[] outputAcceptedIds,
        int outputAcceptedIdCapacity,
        [In, Out] ushort[] outputTriangleWords,
        int outputTriangleWordCapacity,
        out SelectorAcceptedListConsumerRecordSlotTrace recordTrace,
        out SelectorAcceptedListConsumerTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration(
        uint sourceKind,
        uint ownerContextToken,
        uint vertexStreamToken,
        [In] ushort[] sourceTriangleIds,
        int sourceCount,
        int sourceIndex,
        [In] ushort[] triangleVertexIndices,
        int triangleVertexIndexCount,
        out SelectorAcceptedListConsumerPreprocessTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop(
        uint sourceKind,
        [MarshalAs(UnmanagedType.I1)] bool preprocessEnabled,
        uint ownerContextToken,
        uint vertexStreamToken,
        [In] ushort[] sourceTriangleIds,
        int sourceCount,
        [In] ushort[] triangleVertexIndices,
        int triangleVertexIndexCount,
        [Out] SelectorAcceptedListConsumerPreprocessTrace[] iterationTraces,
        int maxIterationTraces,
        out SelectorAcceptedListConsumerPreprocessLoopTrace trace);
}
