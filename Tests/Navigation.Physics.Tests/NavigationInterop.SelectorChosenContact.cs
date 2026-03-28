using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

public static partial class NavigationInterop
{
    public enum SelectorPairSource : uint
    {
        None = 0,
        PreservedInput = 1,
        RankingFailure = 2,
        Direct = 3,
        DirectZero = 4,
        AlternateUnitZZero = 5,
        Alternate = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorPairForwardingTrace
    {
        public uint DirectGateAccepted;
        public uint AlternateUnitZFallbackGateAccepted;
        public uint CurrentPositionInsidePrism;
        public uint ProjectedPositionInsidePrism;
        public uint ThresholdSensitive;
        public float NormalZ;
        public SelectorPairSource PairSource;
        public SelectorPairConsumerTrace ConsumerTrace;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairBridgeTrace
    {
        public uint SelectedIndexInRange;
        public uint LoadedSelectedContact;
        public uint LoadedDirectPair;
        public uint NegativeDiagonalCandidateFound;
        public uint UnitZCandidateFound;
        public uint AlternateUnitZFallbackGateAccepted;
        public uint CurrentPositionInsidePrism;
        public uint ProjectedPositionInsidePrism;
        public uint ThresholdSensitive;
        public float SelectedNormalZ;
        public SelectorPairSource PairSource;
        public SelectorPairConsumerTrace ConsumerTrace;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairProducerTransactionTrace
    {
        public SelectorTriangleSourceVariableTransactionTrace VariableTrace;
        public TerrainQuerySelectedContactContainerTrace ContainerTrace;
        public SelectorChosenIndexPairBridgeTrace BridgeTrace;
        public uint UsedAmbientCachedContainerWithoutQuery;
        public uint UsedProducedSelectedContactContainer;
        public uint ContainerInvoked;
        public uint BridgeInvoked;
        public uint ZeroedOutputsOnVariableFailure;
        public uint ZeroedOutputsOnContainerFailure;
        public uint BridgeSelectedContactCount;
        public int ReturnCode;
        public float OutputReportedBestRatio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairDirectionSetupProducerTransactionTrace
    {
        public SelectorTriangleSourceVariableTransactionTrace VariableTrace;
        public SelectorChosenIndexPairDirectionSetupTrace DirectionSetupTrace;
        public TerrainQuerySelectedContactContainerTrace ContainerTrace;
        public SelectorChosenIndexPairBridgeTrace BridgeTrace;
        public uint UsedAmbientCachedContainerWithoutQuery;
        public uint UsedProducedSelectedContactContainer;
        public uint ContainerInvoked;
        public uint BridgeInvoked;
        public uint ZeroedOutputsOnVariableFailure;
        public uint ZeroedOutputsOnDirectionSetupFailure;
        public uint ZeroedOutputsOnContainerFailure;
        public uint BridgeSelectedContactCount;
        public int ReturnCode;
        public float OutputReportedBestRatio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairSelectedContactContainerTransactionTrace
    {
        public TerrainQuerySelectedContactContainerTrace ContainerTrace;
        public uint UsedAmbientCachedContainerWithoutQuery;
        public uint UsedProducedSelectedContactContainer;
        public uint ContainerInvoked;
        public uint ZeroedOutputsOnContainerFailure;
        public uint OutputSelectedContactCount;
        public uint ReturnedSuccess;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairVariableContainerTransactionTrace
    {
        public SelectorTriangleSourceVariableTransactionTrace VariableTrace;
        public SelectorChosenIndexPairSelectedContactContainerTransactionTrace SelectedContactContainerTrace;
        public uint ZeroedOutputsOnVariableFailure;
        public uint ZeroedOutputsOnContainerFailure;
        public uint OutputSelectedContactCount;
        public uint ReturnedSuccess;
        public float OutputReportedBestRatio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairPreBridgeTransactionTrace
    {
        public SelectorTriangleSourceVariableTransactionTrace VariableTrace;
        public SelectorChosenIndexPairDirectionSetupTrace DirectionSetupTrace;
        public SelectorChosenIndexPairSelectedContactContainerTransactionTrace SelectedContactContainerTrace;
        public uint ZeroedOutputsOnVariableFailure;
        public uint ZeroedOutputsOnDirectionSetupFailure;
        public uint ZeroedOutputsOnContainerFailure;
        public uint OutputDirectionRankingAccepted;
        public uint OutputCandidatePlaneCount;
        public int OutputSelectedRecordIndex;
        public uint OutputSelectedContactCount;
        public uint SelectedIndexInRange;
        public uint LoadedChosenContact;
        public uint LoadedChosenPair;
        public float OutputReportedBestRatio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairSelectedRecordLoadTrace
    {
        public int InputSelectedRecordIndex;
        public uint InputSelectedContactCount;
        public uint InputDirectPairCount;
        public uint SelectedIndexUnset;
        public uint SelectedIndexMatchesContactCountSentinel;
        public uint SelectedIndexInRange;
        public uint SelectedIndexPastEndMismatch;
        public uint LoadedChosenContact;
        public uint LoadedChosenPair;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairCallerTransactionTrace
    {
        public SelectorTriangleSourceVariableTransactionTrace VariableTrace;
        public uint SupportPlaneInitCount;
        public uint ValidationPlaneInitCount;
        public uint ScratchPointZeroCount;
        public uint UsedOverridePosition;
        public uint VariableInvoked;
        public uint ZeroedOutputsOnVariableFailure;
        public Vector3 SelectedPosition;
        public Vector3 TestPoint;
        public Vector3 CandidateDirection;
        public float InitialBestRatio;
        public float OutputReportedBestRatio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorChosenIndexPairDirectionSetupTrace
    {
        public SelectorDirectionRankingTrace RankingTrace;
        public uint SupportPlaneInitCount;
        public uint ScratchPointZeroCount;
        public uint DirectionCandidatePlaneInitCount;
        public uint UsedOverridePosition;
        public uint AppliedSwimVerticalOffsetScale;
        public uint RequestedDistanceClamped;
        public uint ZeroDistanceEarlySuccess;
        public uint RankingInvoked;
        public uint RankingAccepted;
        public Vector3 SelectedPosition;
        public Vector3 ScaledCandidateDirection;
        public float InputReportedBestRatioSeed;
        public float InputVerticalOffset;
        public float OutputVerticalOffset;
        public float InputRequestedDistance;
        public float RequestedDistanceClamp;
        public float ClampedRequestedDistance;
        public uint OutputCandidateCount;
        public int OutputSelectedRecordIndex;
        public float OutputReportedBestRatio;
    }

    public enum SelectorPairPostForwardingDispatchKind : uint
    {
        Failure = 0,
        AlternateUnitZ = 1,
        Direct = 2,
        NonStateful = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorPairPostForwardingTrace
    {
        public int PairForwardReturnCode;
        public uint DirectStateBit;
        public uint AlternateUnitZStateBit;
        public uint UsedWindowAdjustment;
        public uint OutputMagnitudeWritten;
        public SelectorPairPostForwardingDispatchKind DispatchKind;
        public float InputWindowSpanScalar;
        public float OutputWindowScalar;
        public float OutputMoveMagnitude;
        public Vector3 OutputMove;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorAlternateUnitZStateTrace
    {
        public uint InputMovementFlags;
        public uint OutputMovementFlags;
        public uint SetFallingFarFlag;
        public uint ClearedFallTime;
        public uint ZeroedVerticalSpeed;
        public uint CopiedPositionZToFallStartZ;
        public float InputPositionZ;
        public uint OutputFallTime;
        public float OutputFallStartZ;
        public float OutputVerticalSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorDirectStateTrace
    {
        public uint InputMovementFlags;
        public uint OutputMovementFlags;
        public uint JumpingBitWasSet;
        public uint ClearedJumpingBit;
        public uint CopiedPosition;
        public uint CopiedFacing;
        public uint CopiedPitch;
        public uint ZeroedMoveTimestamp;
        public uint WroteScalar84;
        public Vector3 InputStartPosition;
        public Vector3 InputCachedPosition;
        public Vector3 OutputCachedPosition;
        public float InputFacing;
        public float InputCachedFacing;
        public float OutputCachedFacing;
        public float InputPitch;
        public float InputCachedPitch;
        public float OutputCachedPitch;
        public uint InputMoveTimestamp;
        public uint OutputMoveTimestamp;
        public float InputScalar84;
        public float RecomputedScalar84;
        public float OutputScalar84;
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenPairForwarding", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorChosenPairForwarding(
        in Triangle triangle,
        in Vector3 contactNormal,
        in Vector3 currentPosition,
        float requestedDistance,
        in Vector3 inputMove,
        [MarshalAs(UnmanagedType.I1)] bool useStandardWalkableThreshold,
        [MarshalAs(UnmanagedType.I1)] bool directionRankingAccepted,
        int selectedIndex,
        int selectedCount,
        [MarshalAs(UnmanagedType.I1)] bool hasNegativeDiagonalCandidate,
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        [MarshalAs(UnmanagedType.I1)] bool hasUnitZCandidate,
        in SelectorPair directPair,
        in SelectorPair alternatePair,
        out SelectorPairForwardingTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairBridge", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWSelectorChosenIndexPairBridge(
        [In] Triangle[] selectedTriangles,
        [In] Vector3[] contactNormals,
        int selectedContactCount,
        [In] SelectorPair[] directPairs,
        int directPairCount,
        [In] SelectorSupportPlane[] candidatePlanes,
        int candidatePlaneCount,
        in Vector3 currentPosition,
        float requestedDistance,
        in Vector3 inputMove,
        [MarshalAs(UnmanagedType.I1)] bool useStandardWalkableThreshold,
        [MarshalAs(UnmanagedType.I1)] bool directionRankingAccepted,
        int selectedIndex,
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        in SelectorPair alternatePair,
        out SelectorPair outputPair,
        out uint directStateDword,
        out uint alternateUnitZStateDword,
        out SelectorChosenIndexPairBridgeTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairProducerTransaction", CallingConvention = CallingConvention.Cdecl)]
    private static extern int EvaluateWoWSelectorChosenIndexPairProducerTransactionNative(
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [In] TerrainAabbContact[] existingContacts,
        [In] SelectorPair[] existingPairs,
        int existingCount,
        [In] TerrainAabbContact[] queryContacts,
        [In] SelectorPair[] queryPairs,
        int queryCount,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        [MarshalAs(UnmanagedType.I1)] bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        [In] SelectorSupportPlane[] candidatePlanes,
        int candidatePlaneCount,
        in Vector3 currentPosition,
        float requestedDistance,
        in Vector3 inputMove,
        [MarshalAs(UnmanagedType.I1)] bool useStandardWalkableThreshold,
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        in SelectorPair alternatePair,
        out SelectorPair outputPair,
        out uint directStateDword,
        out uint alternateUnitZStateDword,
        out float reportedBestRatio,
        out SelectorChosenIndexPairProducerTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransaction", CallingConvention = CallingConvention.Cdecl)]
    private static extern int EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransactionNative(
        [In] SelectorCandidateRecord[] records,
        int recordCount,
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [In] TerrainAabbContact[] existingContacts,
        [In] SelectorPair[] existingPairs,
        int existingCount,
        [In] TerrainAabbContact[] queryContacts,
        [In] SelectorPair[] queryPairs,
        int queryCount,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        float inputVerticalOffset,
        float swimVerticalOffsetScale,
        [MarshalAs(UnmanagedType.I1)] bool selectorBaseMatchesSwimReference,
        float requestedDistanceClamp,
        float horizontalRadius,
        in Vector3 currentPosition,
        float requestedDistance,
        in Vector3 inputMove,
        [MarshalAs(UnmanagedType.I1)] bool useStandardWalkableThreshold,
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        in SelectorPair alternatePair,
        out SelectorPair outputPair,
        out uint directStateDword,
        out uint alternateUnitZStateDword,
        out float reportedBestRatio,
        out SelectorChosenIndexPairDirectionSetupProducerTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction", CallingConvention = CallingConvention.Cdecl)]
    private static extern int EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransactionNative(
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [In] TerrainAabbContact[] existingContacts,
        [In] SelectorPair[] existingPairs,
        int existingCount,
        [In] TerrainAabbContact[] queryContacts,
        [In] SelectorPair[] queryPairs,
        int queryCount,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        [Out] TerrainAabbContact[] outContacts,
        [Out] SelectorPair[] outPairs,
        int maxOutputCount,
        out SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction", CallingConvention = CallingConvention.Cdecl)]
    private static extern int EvaluateWoWSelectorChosenIndexPairVariableContainerTransactionNative(
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [In] TerrainAabbContact[] existingContacts,
        [In] SelectorPair[] existingPairs,
        int existingCount,
        [In] TerrainAabbContact[] queryContacts,
        [In] SelectorPair[] queryPairs,
        int queryCount,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        [MarshalAs(UnmanagedType.I1)] bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        [Out] TerrainAabbContact[] outContacts,
        [Out] SelectorPair[] outPairs,
        int maxOutputCount,
        out float reportedBestRatio,
        out SelectorChosenIndexPairVariableContainerTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairPreBridgeTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool EvaluateWoWSelectorChosenIndexPairPreBridgeTransactionNative(
        [In] SelectorCandidateRecord[] records,
        int recordCount,
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [In] TerrainAabbContact[] existingContacts,
        [In] SelectorPair[] existingPairs,
        int existingCount,
        [In] TerrainAabbContact[] queryContacts,
        [In] SelectorPair[] queryPairs,
        int queryCount,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        float inputVerticalOffset,
        float swimVerticalOffsetScale,
        [MarshalAs(UnmanagedType.I1)] bool selectorBaseMatchesSwimReference,
        float requestedDistanceClamp,
        float requestedDistance,
        float horizontalRadius,
        [In, Out] SelectorSupportPlane[] outCandidatePlanes,
        int maxCandidatePlanes,
        out uint outCandidatePlaneCount,
        out int outSelectedRecordIndex,
        out uint outDirectionRankingAccepted,
        out TerrainAabbContact outChosenContact,
        out SelectorPair outChosenPair,
        [Out] TerrainAabbContact[] outContacts,
        [Out] SelectorPair[] outPairs,
        int maxOutputCount,
        out float outReportedBestRatio,
        out SelectorChosenIndexPairPreBridgeTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransactionNative(
        int selectedRecordIndex,
        [In] TerrainAabbContact[] selectedContacts,
        int selectedContactCount,
        [In] SelectorPair[] directPairs,
        int directPairCount,
        out TerrainAabbContact outChosenContact,
        out SelectorPair outChosenPair,
        out SelectorChosenIndexPairSelectedRecordLoadTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairCallerTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool EvaluateWoWSelectorChosenIndexPairCallerTransactionNative(
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        [MarshalAs(UnmanagedType.I1)] bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        out float reportedBestRatio,
        out SelectorChosenIndexPairCallerTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool EvaluateWoWSelectorChosenIndexPairDirectionSetupTransactionNative(
        [In] SelectorCandidateRecord[] records,
        int recordCount,
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        float inputReportedBestRatioSeed,
        float inputVerticalOffset,
        float swimVerticalOffsetScale,
        [MarshalAs(UnmanagedType.I1)] bool selectorBaseMatchesSwimReference,
        uint movementFlags,
        float requestedDistance,
        float requestedDistanceClamp,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float horizontalRadius,
        [In, Out] SelectorSupportPlane[] outCandidatePlanes,
        int maxCandidatePlanes,
        out uint outCandidateCount,
        out int outSelectedRecordIndex,
        out float outReportedBestRatio,
        out SelectorChosenIndexPairDirectionSetupTrace trace);

    public static int EvaluateWoWSelectorChosenIndexPairProducerTransaction(
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        TerrainAabbContact[] existingContacts,
        SelectorPair[] existingPairs,
        int existingCount,
        TerrainAabbContact[] queryContacts,
        SelectorPair[] queryPairs,
        int queryCount,
        bool queryDispatchSucceeded,
        bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        SelectorSupportPlane[] candidatePlanes,
        int candidatePlaneCount,
        in Vector3 currentPosition,
        float requestedDistance,
        in Vector3 inputMove,
        bool useStandardWalkableThreshold,
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        in SelectorPair alternatePair,
        out SelectorPair outputPair,
        out uint directStateDword,
        out uint alternateUnitZStateDword,
        out float reportedBestRatio,
        out SelectorChosenIndexPairProducerTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairProducerTransactionNative(
                in defaultPosition,
                overridePositionPtr,
                in projectedPosition,
                supportPlaneInitCount,
                validationPlaneInitCount,
                scratchPointZeroCount,
                in testPoint,
                in candidateDirection,
                initialBestRatio,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                existingContacts,
                existingPairs,
                existingCount,
                queryContacts,
                queryPairs,
                queryCount,
                queryDispatchSucceeded,
                rankingAccepted,
                rankingCandidateCount,
                rankingSelectedRecordIndex,
                rankingReportedBestRatio,
                candidatePlanes,
                candidatePlaneCount,
                in currentPosition,
                requestedDistance,
                in inputMove,
                useStandardWalkableThreshold,
                airborneTimeScalar,
                elapsedTimeScalar,
                horizontalSpeedScale,
                in alternatePair,
                out outputPair,
                out directStateDword,
                out alternateUnitZStateDword,
                out reportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static int EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransaction(
        SelectorCandidateRecord[] records,
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        TerrainAabbContact[] existingContacts,
        SelectorPair[] existingPairs,
        int existingCount,
        TerrainAabbContact[] queryContacts,
        SelectorPair[] queryPairs,
        int queryCount,
        bool queryDispatchSucceeded,
        float inputVerticalOffset,
        float swimVerticalOffsetScale,
        bool selectorBaseMatchesSwimReference,
        float requestedDistanceClamp,
        float horizontalRadius,
        in Vector3 currentPosition,
        float requestedDistance,
        in Vector3 inputMove,
        bool useStandardWalkableThreshold,
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        in SelectorPair alternatePair,
        out SelectorPair outputPair,
        out uint directStateDword,
        out uint alternateUnitZStateDword,
        out float reportedBestRatio,
        out SelectorChosenIndexPairDirectionSetupProducerTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransactionNative(
                records,
                records?.Length ?? 0,
                in defaultPosition,
                overridePositionPtr,
                in projectedPosition,
                supportPlaneInitCount,
                validationPlaneInitCount,
                scratchPointZeroCount,
                in testPoint,
                in candidateDirection,
                initialBestRatio,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                existingContacts,
                existingPairs,
                existingCount,
                queryContacts,
                queryPairs,
                queryCount,
                queryDispatchSucceeded,
                inputVerticalOffset,
                swimVerticalOffsetScale,
                selectorBaseMatchesSwimReference,
                requestedDistanceClamp,
                horizontalRadius,
                in currentPosition,
                requestedDistance,
                in inputMove,
                useStandardWalkableThreshold,
                airborneTimeScalar,
                elapsedTimeScalar,
                horizontalSpeedScale,
                in alternatePair,
                out outputPair,
                out directStateDword,
                out alternateUnitZStateDword,
                out reportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static int EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        TerrainAabbContact[] existingContacts,
        SelectorPair[] existingPairs,
        int existingCount,
        TerrainAabbContact[] queryContacts,
        SelectorPair[] queryPairs,
        int queryCount,
        bool queryDispatchSucceeded,
        TerrainAabbContact[] outContacts,
        SelectorPair[] outPairs,
        int maxOutputCount,
        out SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransactionNative(
                overridePositionPtr,
                in projectedPosition,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                existingContacts,
                existingPairs,
                existingCount,
                queryContacts,
                queryPairs,
                queryCount,
                queryDispatchSucceeded,
                outContacts,
                outPairs,
                maxOutputCount,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static int EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction(
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        TerrainAabbContact[] existingContacts,
        SelectorPair[] existingPairs,
        int existingCount,
        TerrainAabbContact[] queryContacts,
        SelectorPair[] queryPairs,
        int queryCount,
        bool queryDispatchSucceeded,
        bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        TerrainAabbContact[] outContacts,
        SelectorPair[] outPairs,
        int maxOutputCount,
        out float reportedBestRatio,
        out SelectorChosenIndexPairVariableContainerTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairVariableContainerTransactionNative(
                in defaultPosition,
                overridePositionPtr,
                in projectedPosition,
                supportPlaneInitCount,
                validationPlaneInitCount,
                scratchPointZeroCount,
                in testPoint,
                in candidateDirection,
                initialBestRatio,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                existingContacts,
                existingPairs,
                existingCount,
                queryContacts,
                queryPairs,
                queryCount,
                queryDispatchSucceeded,
                rankingAccepted,
                rankingCandidateCount,
                rankingSelectedRecordIndex,
                rankingReportedBestRatio,
                outContacts,
                outPairs,
                maxOutputCount,
                out reportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static bool EvaluateWoWSelectorChosenIndexPairPreBridgeTransaction(
        SelectorCandidateRecord[] records,
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        TerrainAabbContact[] existingContacts,
        SelectorPair[] existingPairs,
        int existingCount,
        TerrainAabbContact[] queryContacts,
        SelectorPair[] queryPairs,
        int queryCount,
        bool queryDispatchSucceeded,
        float inputVerticalOffset,
        float swimVerticalOffsetScale,
        bool selectorBaseMatchesSwimReference,
        float requestedDistanceClamp,
        float requestedDistance,
        float horizontalRadius,
        SelectorSupportPlane[] outCandidatePlanes,
        out uint outCandidatePlaneCount,
        out int outSelectedRecordIndex,
        out uint outDirectionRankingAccepted,
        out TerrainAabbContact outChosenContact,
        out SelectorPair outChosenPair,
        TerrainAabbContact[] outContacts,
        SelectorPair[] outPairs,
        int maxOutputCount,
        out float outReportedBestRatio,
        out SelectorChosenIndexPairPreBridgeTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairPreBridgeTransactionNative(
                records,
                records?.Length ?? 0,
                in defaultPosition,
                overridePositionPtr,
                in projectedPosition,
                supportPlaneInitCount,
                validationPlaneInitCount,
                scratchPointZeroCount,
                in testPoint,
                in candidateDirection,
                initialBestRatio,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                existingContacts,
                existingPairs,
                existingCount,
                queryContacts,
                queryPairs,
                queryCount,
                queryDispatchSucceeded,
                inputVerticalOffset,
                swimVerticalOffsetScale,
                selectorBaseMatchesSwimReference,
                requestedDistanceClamp,
                requestedDistance,
                horizontalRadius,
                outCandidatePlanes,
                outCandidatePlanes?.Length ?? 0,
                out outCandidatePlaneCount,
                out outSelectedRecordIndex,
                out outDirectionRankingAccepted,
                out outChosenContact,
                out outChosenPair,
                outContacts,
                outPairs,
                maxOutputCount,
                out outReportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static bool EvaluateWoWSelectorChosenIndexPairCallerTransaction(
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        bool queryDispatchSucceeded,
        bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        out float reportedBestRatio,
        out SelectorChosenIndexPairCallerTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairCallerTransactionNative(
                in defaultPosition,
                overridePositionPtr,
                in projectedPosition,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                queryDispatchSucceeded,
                rankingAccepted,
                rankingCandidateCount,
                rankingSelectedRecordIndex,
                rankingReportedBestRatio,
                out reportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static bool EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
        SelectorCandidateRecord[] records,
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        float inputReportedBestRatioSeed,
        float inputVerticalOffset,
        float swimVerticalOffsetScale,
        bool selectorBaseMatchesSwimReference,
        uint movementFlags,
        float requestedDistance,
        float requestedDistanceClamp,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float horizontalRadius,
        SelectorSupportPlane[] outCandidatePlanes,
        out uint outCandidateCount,
        out int outSelectedRecordIndex,
        out float outReportedBestRatio,
        out SelectorChosenIndexPairDirectionSetupTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorChosenIndexPairDirectionSetupTransactionNative(
                records,
                records?.Length ?? 0,
                in defaultPosition,
                overridePositionPtr,
                inputReportedBestRatioSeed,
                inputVerticalOffset,
                swimVerticalOffsetScale,
                selectorBaseMatchesSwimReference,
                movementFlags,
                requestedDistance,
                requestedDistanceClamp,
                in testPoint,
                in candidateDirection,
                horizontalRadius,
                outCandidatePlanes,
                outCandidatePlanes?.Length ?? 0,
                out outCandidateCount,
                out outSelectedRecordIndex,
                out outReportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    public static bool EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
        int selectedRecordIndex,
        TerrainAabbContact[] selectedContacts,
        int selectedContactCount,
        SelectorPair[] directPairs,
        int directPairCount,
        out TerrainAabbContact outChosenContact,
        out SelectorPair outChosenPair,
        out SelectorChosenIndexPairSelectedRecordLoadTrace trace)
    {
        return EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransactionNative(
            selectedRecordIndex,
            selectedContacts,
            selectedContactCount,
            directPairs,
            directPairCount,
            out outChosenContact,
            out outChosenPair,
            out trace);
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPairPostForwardingDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorPairPostForwardingDispatch(
        int pairForwardReturnCode,
        uint directStateBit,
        uint alternateUnitZStateBit,
        float windowSpanScalar,
        float windowStartScalar,
        in Vector3 moveVector,
        float horizontalReferenceMagnitude,
        uint movementFlags,
        float verticalSpeed,
        float horizontalSpeedScale,
        float referenceZ,
        float positionZ,
        out SelectorPairPostForwardingTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAlternateUnitZStateHandler", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorAlternateUnitZStateHandler(
        uint movementFlags,
        float positionZ,
        out SelectorAlternateUnitZStateTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorDirectStateHandler", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorDirectStateHandler(
        uint movementFlags,
        in Vector3 startPosition,
        float facing,
        float pitch,
        in Vector3 cachedPosition,
        float cachedFacing,
        float cachedPitch,
        uint cachedMoveTimestamp,
        float cachedScalar84,
        float recomputedScalar84,
        out SelectorDirectStateTrace trace);
}
