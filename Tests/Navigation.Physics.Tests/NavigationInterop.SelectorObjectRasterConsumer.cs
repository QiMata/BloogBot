using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

public static partial class NavigationInterop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterPayload
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public SelectorSupportPlane[] Planes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public Vector3[] SupportPoints;
        public Vector3 AnchorPoint0;
        public Vector3 AnchorPoint1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterWindow
    {
        public int RowMin;
        public int ColumnMin;
        public int RowMax;
        public int ColumnMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterPrefixTrace
    {
        public uint ModeGateAccepted;
        public uint QuantizedWindowAccepted;
        public uint ScratchAllocationRequired;
        public uint EnteredPrepassPointLoops;
        public uint EnteredRasterCellLoops;
        public float QuantizeScale;
        public Vector3 AppliedTranslation;
        public Vector3 TranslatedSupportPointMin;
        public Vector3 TranslatedSupportPointMax;
        public Vector3 TranslatedAnchorPoint0;
        public Vector3 TranslatedAnchorPoint1;
        public float TranslatedFirstPlaneDistance;
        public SelectorObjectRasterWindow RawWindow;
        public SelectorObjectRasterWindow ClippedWindow;
        public uint ScratchByteCount;
        public uint PrepassPointCountX;
        public uint PrepassPointCountY;
        public uint RasterCellCountX;
        public uint RasterCellCountY;
        public int PointStartIndex;
        public int PointRowAdvance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterQueueEntry
    {
        public uint Allocated;
        public uint CallerContextToken;
        public uint RasterSourceToken;
        public uint ScratchWordStart;
        public uint ScratchWordReserved;
        public ushort AppendedWordCount;
        public ushort AppendedTriangleCount;
        public ushort MinAppendedWord;
        public ushort MaxAppendedWord;
        public uint ScratchBufferPresent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterBodyTrace
    {
        public SelectorObjectRasterPrefixTrace Prefix;
        public uint ReturnedAnyCandidate;
        public uint PrepassPointWrites;
        public uint VisitedRasterCellCount;
        public uint SkippedByCellModeValue;
        public uint SkippedByCellModeMask;
        public uint RejectedTriangleCount;
        public uint AcceptedTriangleCount;
        public uint QueueCountBefore;
        public uint QueueCountAfter;
        public uint ScratchWordsBefore;
        public uint ScratchWordsAfter;
        public uint QueueLimitOverflowed;
        public uint ScratchOverflowed;
        public uint AppendedWordCount;
        public uint AppendedTriangleCount;
        public uint FinalQueueEntryListSpliceLinked;
        public uint NormalCleanupLinked;
        public uint FailureCleanupExecuted;
        public uint FailureCleanupDestroyedPayloadBlocks;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterPrefixFailureCleanupTrace
    {
        public uint PrefixAccepted;
        public uint ModeGateAccepted;
        public uint FailureCleanupExecuted;
        public uint FailureCleanupDestroyedPayloadBlocks;
        public uint ReturnedBeforePrepass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterPrepassTrace
    {
        public uint PointWriteCount;
        public uint OutputWriteCount;
        public uint PointIndexOutOfRangeCount;
        public uint FirstPointIndex;
        public uint LastPointIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterPrepassCompositionTrace
    {
        public SelectorObjectRasterPrepassTrace Prepass;
        public Vector3 TranslatedAnchorPoint0;
        public Vector3 TranslatedAnchorPoint1;
        public float TranslatedFirstPlaneDistance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterCellIterationTrace
    {
        public uint VisitedRasterCell;
        public uint SkippedByCellModeValue;
        public uint SkippedByCellModeMask;
        public uint ReturnedAnyCandidate;
        public uint RejectedTriangleCount;
        public uint AcceptedTriangleCount;
        public uint QueueCountBefore;
        public uint QueueCountAfter;
        public uint ScratchWordsBefore;
        public uint ScratchWordsAfter;
        public uint QueueLimitOverflowed;
        public uint ScratchOverflowed;
        public uint EntryAllocatedBefore;
        public uint EntryAllocatedAfter;
        public uint CellIndex;
        public uint CellModeNibble;
        public uint LocalPointBase;
        public uint WorldPointBase;
        public uint AppendedWordCountBefore;
        public uint AppendedWordCountAfter;
        public uint AppendedTriangleCountBefore;
        public uint AppendedTriangleCountAfter;
        public uint TriangleRejectMask;
        public uint TriangleAcceptMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRasterAggregationTrace
    {
        public SelectorObjectRasterQueueEntry Entry;
        public uint ReturnedAnyCandidate;
        public uint VisitedRasterCellCount;
        public uint SkippedByCellModeValue;
        public uint SkippedByCellModeMask;
        public uint RejectedTriangleCount;
        public uint AcceptedTriangleCount;
        public uint QueueCountBefore;
        public uint QueueCountAfter;
        public uint ScratchWordsBefore;
        public uint ScratchWordsAfter;
        public uint QueueLimitOverflowed;
        public uint ScratchOverflowed;
        public uint AppendedWordCount;
        public uint AppendedTriangleCount;
        public uint FinalQueueEntryListSpliceLinked;
        public uint NormalCleanupLinked;
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterConsumerPrefix", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterConsumerPrefix(
        uint modeWord,
        int rasterRowCount,
        int rasterColumnCount,
        int rasterRowStride,
        float quantizeScale,
        in Vector3 objectTranslation,
        in SelectorObjectRasterPayload sourcePayload,
        out SelectorObjectRasterPrefixTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterConsumerBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterConsumerBody(
        uint modeWord,
        int rasterRowCount,
        int rasterColumnCount,
        int pointGridRowStride,
        int cellModeRowStride,
        float quantizeScale,
        uint cellModeMaskFlags,
        uint callerContextToken,
        uint rasterSourceToken,
        uint inputQueueEntryCount,
        uint inputScratchWordCount,
        uint deferredCleanupListPresent,
        in Vector3 objectTranslation,
        in SelectorObjectRasterPayload sourcePayload,
        [In] Vector3[] pointGrid,
        int pointGridPointCount,
        [In] byte[] cellModes,
        int cellModeCount,
        [In, Out] uint[] pointOutcodes,
        int pointOutcodeCapacity,
        [In, Out] ushort[] scratchWords,
        int outScratchWordCapacity,
        out SelectorObjectRasterQueueEntry entry,
        out SelectorObjectRasterBodyTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit(
        uint prefixAccepted,
        in SelectorObjectRasterPrefixTrace prefix,
        out SelectorObjectRasterPrefixFailureCleanupTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
        in SelectorObjectRasterPrefixTrace prefix,
        int pointGridRowStride,
        in Vector3 objectTranslation,
        in SelectorObjectRasterPayload sourcePayload,
        [In] Vector3[] pointGrid,
        int pointGridPointCount,
        [In, Out] uint[]? pointOutcodes,
        int pointOutcodeCapacity,
        out SelectorObjectRasterPrepassTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterPrepassComposition", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterPrepassComposition(
        in SelectorObjectRasterPrefixTrace prefix,
        in SelectorObjectRasterPayload sourcePayload,
        [In] Vector3[]? pointGrid,
        int pointGridPointCount,
        [In, Out] uint[]? pointOutcodes,
        int pointOutcodeCapacity,
        out SelectorObjectRasterPrepassCompositionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterCellIteration", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterCellIteration(
        in SelectorObjectRasterPrefixTrace prefix,
        uint localRow,
        uint localColumn,
        int pointGridRowStride,
        int cellModeRowStride,
        uint cellModeMaskFlags,
        uint callerContextToken,
        uint rasterSourceToken,
        uint inputQueueEntryCount,
        uint inputScratchWordCount,
        [In] byte[] cellModes,
        int cellModeCount,
        [In] uint[] pointOutcodes,
        int pointOutcodeCapacity,
        [In, Out] ushort[] scratchWords,
        int outScratchWordCapacity,
        ref SelectorObjectRasterQueueEntry entry,
        out SelectorObjectRasterCellIterationTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRasterCellLoopAggregation", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRasterCellLoopAggregation(
        in SelectorObjectRasterPrefixTrace prefix,
        int pointGridRowStride,
        int cellModeRowStride,
        uint cellModeMaskFlags,
        uint callerContextToken,
        uint rasterSourceToken,
        uint inputQueueEntryCount,
        uint inputScratchWordCount,
        uint deferredCleanupListPresent,
        [In] byte[] cellModes,
        int cellModeCount,
        [In] uint[] pointOutcodes,
        int pointOutcodeCapacity,
        [In, Out] ushort[] scratchWords,
        int outScratchWordCapacity,
        out SelectorObjectRasterAggregationTrace trace);
}
