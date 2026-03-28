using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

public static partial class NavigationInterop
{
    public enum GroundedDriverSelectedContactDispatchKind : uint
    {
        StartFallZero = 0,
        DelegateToNonWalkableDispatch = 1,
        WalkableSelectedVertical = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverFirstDispatchTrace
    {
        public uint WalkableSelectedContact;
        public uint GateReturnCode;
        public uint DispatchKind;
        public uint SetGroundedWall04000000;
        public uint UsesVerticalHelper;
        public uint UsesHorizontalHelper;
        public uint RemainingDistanceRescaled;
        public float RemainingDistanceBeforeDispatch;
        public float RemainingDistanceAfterDispatch;
        public float SweepDistanceBeforeVertical;
        public float SweepDistanceAfterVertical;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedContactDispatchTrace
    {
        public uint CheckWalkableAccepted;
        public uint ConsumedSelectedState;
        public GroundedDriverSelectedContactDispatchKind DispatchKind;
        public uint BypassedNonWalkableDispatch;
        public uint DelegatedToNonWalkableDispatch;
        public uint StartedFallWithZeroVelocity;
        public uint ClearedSplineElevation04000000;
        public uint ClearedSwimming00200000;
        public uint SetJumping;
        public uint ResetFallTime;
        public uint ResetFallStartZ;
        public uint ResetVerticalSpeed;
        public uint DroppedChosenPair;
        public uint InputMovementFlags;
        public uint OutputMovementFlags;
        public float RemainingDistanceBeforeDispatch;
        public float RemainingDistanceAfterDispatch;
        public float SweepDistanceBeforeVertical;
        public float SweepDistanceAfterVertical;
        public uint InputFallTime;
        public uint OutputFallTime;
        public float InputFallStartZ;
        public float OutputFallStartZ;
        public float InputVerticalSpeed;
        public float OutputVerticalSpeed;
        public float PositionZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverResweepBookkeepingTrace
    {
        public uint NormalizedDirection;
        public uint WroteHorizontalPair;
        public uint NormalizedHorizontalPair;
        public uint FinalizeFlag;
        public uint TinyMagnitudeFinalize;
        public uint HorizontalBudgetFinalize;
        public Vector3 InputDirection;
        public float InputSweepScalar;
        public Vector3 InputCorrection;
        public float InputHorizontalBudget;
        public Vector3 OutputCombinedMove;
        public float OutputSweepDistance;
        public Vector3 OutputDirection;
        public float OutputHorizontalX;
        public float OutputHorizontalY;
        public float OutputCombinedXYMagnitude;
        public float OutputHorizontalBudget;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverVerticalCapTrace
    {
        public uint CapBitSet;
        public uint PositiveCombinedMoveZ;
        public uint AppliedCap;
        public uint SetFinalizeFlag20;
        public uint SetTinySweepFlag30;
        public float CombinedMoveZ;
        public float InputSweepDistance;
        public float OutputSweepDistance;
        public float CurrentZ;
        public float BoundingRadius;
        public float CapField80;
        public float CapAbsoluteZ;
        public float PredictedZ;
        public float AllowedDeltaZ;
    }

    public enum GroundedDriverSelectedPlaneCorrectionKind : uint
    {
        VerticalOnly = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneCorrectionTrace
    {
        public uint WroteVerticalOnlyCorrection;
        public uint ClampedVerticalMagnitude;
        public uint MutatedDistancePointer;
        public uint RescaledRemainingDistance;
        public uint RescaledSweepFraction;
        public GroundedDriverSelectedPlaneCorrectionKind CorrectionKind;
        public Vector3 RequestedMove;
        public Vector3 SelectedPlaneNormal;
        public Vector3 ProjectedCorrection;
        public Vector3 OutputCorrection;
        public float IntoPlane;
        public float ProjectedVertical;
        public float VerticalLimit;
        public float InputRemainingDistance;
        public float OutputRemainingDistance;
        public float InputSweepDistance;
        public float OutputSweepDistance;
        public float InputSweepFraction;
        public float OutputSweepFraction;
        public float InputDistancePointer;
        public float OutputDistancePointer;
    }

    public enum GroundedDriverSelectedPlaneCorrectionTransactionKind : uint
    {
        DirectScalar = 0,
        PositiveRadiusClamp = 1,
        NegativeRadiusClamp = 2,
        FlaggedNegativeZeroDistance = 3,
    }

    public enum GroundedDriverHorizontalCorrectionKind : uint
    {
        ZeroedOnReject = 0,
        HorizontalEpsilonProjection = 1,
    }

    public enum GroundedDriverSelectedPlaneDistancePointerKind : uint
    {
        DirectScalar = 0,
        PositiveRadiusClamp = 1,
        NegativeRadiusClamp = 2,
        FlaggedNegativeZeroDistance = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneDistancePointerTrace
    {
        public uint UseSelectedPlaneOverride;
        public uint SelectedContactNormalWithinOverrideBand;
        public uint UsedSelectedPlaneNormalOverride;
        public uint UsedInfiniteScalar;
        public uint GroundedWall04000000Set;
        public uint ZeroedDistancePointer;
        public uint MutatedDistancePointer;
        public GroundedDriverSelectedPlaneDistancePointerKind OutputKind;
        public Vector3 InputMoveDirection;
        public Vector3 InputWorkingVector;
        public Vector3 EffectiveWorkingVector;
        public Vector3 SelectedPlaneNormal;
        public Vector3 OutputCorrection;
        public float SelectedContactNormalZ;
        public float SelectedPlaneMagnitudeSquared;
        public float InputDistancePointer;
        public float OutputDistancePointer;
        public float BoundingRadius;
        public float DotScaledDistance;
        public float RawScalar;
        public float OutputScalar;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneCorrectionTransactionTrace
    {
        public GroundedDriverSelectedPlaneDistancePointerTrace DistancePointerTrace;
        public uint WroteVerticalOnlyCorrection;
        public uint MutatedDistancePointer;
        public uint RescaledRemainingDistance;
        public uint RescaledSweepFraction;
        public GroundedDriverSelectedPlaneCorrectionTransactionKind CorrectionKind;
        public uint InputMovementFlags;
        public Vector3 SelectedPlaneNormal;
        public Vector3 InputWorkingVector;
        public Vector3 InputMoveDirection;
        public Vector3 OutputCorrection;
        public float SelectedContactNormalZ;
        public float BoundingRadius;
        public float InputRemainingDistance;
        public float OutputRemainingDistance;
        public float InputSweepFraction;
        public float OutputSweepFraction;
        public float InputDistancePointer;
        public float OutputDistancePointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverHorizontalCorrectionTrace
    {
        public uint EntryGateAccepted;
        public uint NormalizedHorizontalNormal;
        public uint AppliedEpsilonPushout;
        public uint ZeroedOutputOnReject;
        public GroundedDriverHorizontalCorrectionKind CorrectionKind;
        public Vector3 RequestedMove;
        public Vector3 SelectedPlaneNormal;
        public Vector3 NormalizedHorizontalNormalVector;
        public Vector3 OutputCorrection;
        public float IntoPlane;
        public float HorizontalMagnitude;
        public float InverseHorizontalMagnitude;
        public float OutputResolved2D;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneRetryTrace
    {
        public GroundedDriverSelectedPlaneDistancePointerTrace DistancePointerTrace;
        public GroundedDriverHorizontalCorrectionTrace HorizontalCorrectionTrace;
        public uint WalkableSelectedContact;
        public uint GateReturnCode;
        public uint SetGroundedWall04000000;
        public uint UsesVerticalHelper;
        public uint UsesHorizontalHelper;
        public uint MutatedDistancePointer;
        public uint RescaledRemainingDistance;
        public uint RescaledSweepFraction;
        public uint InputMovementFlags;
        public uint OutputMovementFlags;
        public GroundedDriverSelectedPlaneBranchGateKind BranchKind;
        public Vector3 SelectedPlaneNormal;
        public Vector3 InputWorkingVector;
        public Vector3 InputMoveDirection;
        public Vector3 OutputCorrection;
        public float SelectedContactNormalZ;
        public float BoundingRadius;
        public float InputRemainingDistance;
        public float OutputRemainingDistance;
        public float InputSweepFraction;
        public float OutputSweepFraction;
        public float InputDistancePointer;
        public float OutputDistancePointer;
    }

    public enum GroundedDriverSelectedPlaneFirstPassSetupKind : uint
    {
        SkipToFollowupRerank = 0,
        FirstPassFailureExit = 1,
        ContinueToFollowupRerank = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneFirstPassSetupTrace
    {
        public uint SupportPlaneInitCount;
        public uint LoadedInputPackedPair;
        public uint UsedBoundingRadiusTanFloor;
        public uint EnteredFirstPassNormalBand;
        public uint InvokedFirstPassRerank;
        public uint FirstPassRerankSucceeded;
        public GroundedDriverSelectedPlaneFirstPassSetupKind DispatchKind;
        public Vector3 InputContactNormal;
        public Vector3 InputPackedPairVector;
        public Vector3 FirstPassWorkingVector;
        public float FieldB0;
        public float BoundingRadius;
        public float SkinAdjustedFieldB0;
        public float BoundingRadiusTanFloor;
        public float OutputScalarFloor;
        public float HorizontalMagnitude;
        public float InverseHorizontalMagnitude;
    }

    public enum GroundedDriverSelectedPlaneBranchGateKind : uint
    {
        ExitWithoutMutation = 0,
        HorizontalRetry = 1,
        VerticalRetry = 2,
        WalkableSelectedVertical = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneBranchGateTrace
    {
        public GroundedDriverHorizontalCorrectionTrace HorizontalCorrectionTrace;
        public uint WalkableSelectedContact;
        public uint GateReturnCode;
        public uint SetGroundedWall04000000;
        public uint UsesVerticalHelper;
        public uint UsesHorizontalHelper;
        public uint RemainingDistanceRescaled;
        public uint MutatedDistancePointer;
        public uint RescaledSweepFraction;
        public GroundedDriverSelectedPlaneBranchGateKind BranchKind;
        public Vector3 RequestedMove;
        public Vector3 SelectedPlaneNormal;
        public Vector3 OutputCorrection;
        public float InputRemainingDistance;
        public float OutputRemainingDistance;
        public float InputSweepDistance;
        public float OutputSweepDistance;
        public float InputSweepFraction;
        public float OutputSweepFraction;
        public float InputDistancePointer;
        public float OutputDistancePointer;
    }

    public enum GroundedDriverSelectedPlaneFollowupRerankKind : uint
    {
        ExitWithoutSelection = 0,
        HorizontalFastReturn = 1,
        ContinueToUncapturedTail = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneFollowupRerankTrace
    {
        public uint SelectedIndex;
        public uint SelectedCount;
        public uint SelectedIndexInRange;
        public uint SelectedRecordMatchesInputNormal;
        public uint ReloadedInputPackedPair;
        public uint RetainedRerankedPackedPair;
        public uint UsedUnitZWorkingVector;
        public uint CalledSecondRerank;
        public uint SecondRerankSucceeded;
        public uint HorizontalFastReturn;
        public GroundedDriverSelectedPlaneFollowupRerankKind DispatchKind;
        public Vector3 InputContactNormal;
        public Vector3 SelectedRecordNormal;
        public Vector3 InputPackedPairVector;
        public Vector3 RerankedPackedPairVector;
        public Vector3 EffectivePackedPairVector;
        public Vector3 SecondPassWorkingVector;
    }

    public enum GroundedDriverSelectedPlaneTailPreThirdPassSetupKind : uint
    {
        UseHorizontalFallbackInputs = 0,
        UseProjectedTailRerankInputs = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace
    {
        public GroundedDriverSelectedPlaneCorrectionTransactionTrace CorrectionTransactionTrace;
        public GroundedDriverHorizontalCorrectionTrace HorizontalCorrectionTrace;
        public uint InvokedVerticalCorrection;
        public uint RejectedOnTransformMagnitude;
        public uint PreparedTailRerankInputs;
        public GroundedDriverSelectedPlaneTailPreThirdPassSetupKind DispatchKind;
        public Vector3 InputPackedPairVector;
        public Vector3 OutputPackedPairVector;
        public float InputPositionZ;
        public float OutputPositionZ;
        public float InputFollowupScalar;
        public float OutputFollowupScalar;
        public float InputScalarFloor;
        public float OutputScalarFloor;
        public float InputTailTransformScalar;
        public float OutputTailTransformScalar;
        public Vector3 HorizontalProjectedMove;
        public float HorizontalResolved2D;
        public Vector3 ProjectedTailWorkingVector;
        public float ProjectedTailDistance;
    }

    public enum GroundedDriverSelectedPlaneTailProjectedBlendKind : uint
    {
        ExitWithoutSelection = 0,
        UseHorizontalFallback = 1,
        UseSelectedPlaneBlend = 2,
    }

    public enum GroundedDriverSelectedPlaneThirdPassSetupKind : uint
    {
        ExitWithoutSelection = 0,
        ContinueToBlendCorrection = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneThirdPassSetupTrace
    {
        public uint LoadedPackedPairXY;
        public uint ZeroedWorkingVectorZ;
        public uint AdvancedPositionZ;
        public uint InvokedThirdPassRerank;
        public uint ThirdPassRerankSucceeded;
        public GroundedDriverSelectedPlaneThirdPassSetupKind DispatchKind;
        public Vector3 InputPackedPairVector;
        public Vector3 ThirdPassWorkingVector;
        public float InputPositionZ;
        public float OutputPositionZ;
        public float FollowupScalar;
        public float ScalarFloor;
    }

    public enum GroundedDriverSelectedPlaneBlendCorrectionKind : uint
    {
        UseHorizontalFallback = 0,
        UseSelectedPlaneBlend = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneBlendCorrectionTrace
    {
        public uint ClampedVerticalMagnitude;
        public uint DiscardedUphillBlend;
        public uint AcceptedSelectedPlaneBlend;
        public GroundedDriverSelectedPlaneBlendCorrectionKind DispatchKind;
        public Vector3 RequestedMove;
        public Vector3 SelectedPlaneNormal;
        public Vector3 HorizontalProjectedMove;
        public Vector3 SelectedPlaneProjectedMove;
        public Vector3 OutputProjectedMove;
        public float IntoPlane;
        public float VerticalLimit;
        public float HorizontalResolved2D;
        public float SlopedResolved2D;
        public float OutputResolved2D;
    }

    public enum GroundedDriverSelectedPlanePostFastReturnTailKind : uint
    {
        ExitWithoutSelection = 0,
        UseHorizontalFallback = 1,
        UseSelectedPlaneBlend = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlanePostFastReturnTailTrace
    {
        public GroundedDriverSelectedPlaneThirdPassSetupTrace ThirdPassSetupTrace;
        public GroundedDriverSelectedPlaneBlendCorrectionTrace BlendCorrectionTrace;
        public uint InvokedBlendCorrection;
        public GroundedDriverSelectedPlanePostFastReturnTailKind DispatchKind;
        public Vector3 InputPackedPairVector;
        public Vector3 RequestedMove;
        public Vector3 SelectedPlaneNormal;
        public Vector3 HorizontalProjectedMove;
        public Vector3 OutputProjectedMove;
        public float InputPositionZ;
        public float OutputPositionZ;
        public float FollowupScalar;
        public float ScalarFloor;
        public float VerticalLimit;
        public float HorizontalResolved2D;
        public float OutputResolved2D;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailProjectedBlendTrace
    {
        public GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace TailPreThirdPassSetupTrace;
        public GroundedDriverSelectedPlanePostFastReturnTailTrace PostFastReturnTailTrace;
        public uint UsedProjectedTailRerankInputs;
        public GroundedDriverSelectedPlaneTailProjectedBlendKind DispatchKind;
        public Vector3 OutputProjectedMove;
        public float OutputResolved2D;
        public float OutputPositionZ;
    }

    public enum GroundedDriverSelectedPlaneTailWritebackKind : uint
    {
        ThirdPassOnly = 0,
        ThirdPassPlusProjectedTail = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailWritebackTrace
    {
        public uint TailScalarDiffExceedsEpsilon;
        public uint CheckWalkableAccepted;
        public uint ProjectedTailRerankSucceeded;
        public uint AppliedProjectedTailWriteback;
        public GroundedDriverSelectedPlaneTailWritebackKind DispatchKind;
        public Vector3 InputPosition;
        public Vector3 OutputPosition;
        public Vector3 InputPackedPairVector;
        public Vector3 ProjectedTailMove;
        public float FollowupScalar;
        public float ScalarFloor;
        public float OutputResolved2D;
        public float ProjectedTailResolved2D;
        public float InputSelectedContactNormalZ;
        public float OutputSelectedContactNormalZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
    {
        public Vector3 Field44Vector;
        public Vector3 Field50Vector;
        public uint Field78;
        public Vector3 Field5cVector;
        public float Field68;
        public float Field6c;
        public uint Field40Flags;
        public float Field84;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace
    {
        public uint AddedPositiveHalfBias;
        public uint AddedNegativeHalfBias;
        public float InputElapsedSeconds;
        public float ScaledMilliseconds;
        public float AdjustedMilliseconds;
        public int RoundedMilliseconds;
    }

    public enum GroundedDriverSelectedPlaneTailEntrySetupKind : uint
    {
        ExitWithoutProbe = 0,
        ContinueToProbe = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailEntrySetupTrace
    {
        public GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace SnapshotTrace;
        public uint ZeroedPairForwardState;
        public uint ZeroedDirectStateBit;
        public uint ZeroedSidecarState;
        public uint ZeroedLateralOffset;
        public uint BuiltNormalizedInputDirection;
        public uint InputWindowMilliseconds;
        public uint InputField78;
        public GroundedDriverSelectedPlaneTailEntrySetupKind DispatchKind;
        public Vector3 InputRequestedMove;
        public Vector3 CurrentPosition;
        public Vector3 OutputNormalizedInputDirection;
        public float InputWindowSeconds;
        public float Field78Seconds;
        public float ElapsedScalar;
        public float CurrentWindowScalar;
        public float CurrentMagnitude;
        public float CurrentHorizontalMagnitude;
        public float AbsoluteVerticalMagnitude;
    }

    public enum GroundedDriverSelectedPlaneTailPostForwardingKind : uint
    {
        Return2LateBranch = 0,
        AlternateUnitZ = 1,
        DirectState = 2,
        NonStatefulContinue = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailPostForwardingTrace
    {
        public SelectorPairPostForwardingTrace PostForwardingTrace;
        public SelectorAlternateUnitZStateTrace AlternateUnitZStateTrace;
        public SelectorDirectStateTrace DirectStateTrace;
        public uint InvokedAlternateUnitZStateHandler;
        public uint InvokedDirectStateHandler;
        public uint AppliedMoveToPosition;
        public uint AdvancedElapsedScalar;
        public GroundedDriverSelectedPlaneTailPostForwardingKind DispatchKind;
        public Vector3 InputPosition;
        public Vector3 OutputPosition;
        public float InputElapsedScalar;
        public float OutputElapsedScalar;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailReturn2LateBranchTrace
    {
        public GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace ElapsedMillisecondsTrace;
        public uint InvokedConsumedWindowCommit;
        public int InputField58;
        public int OutputField58;
        public int InputField78;
        public int OutputField78;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailLateNotifierTrace
    {
        public GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace SnapshotTrace;
        public uint AlternateUnitZStateBit;
        public uint NotifyRequested;
        public uint SidecarStatePresent;
        public uint AddedRoundedMillisecondsToField78;
        public uint InvokedAlternateWindowCommit;
        public uint InvokedSidecarCommit;
        public uint Bit20InitiallySet;
        public uint InvokedCommitGuard;
        public uint CommitGuardPassed;
        public uint ReturnedEarlyAfterCommitGuard;
        public uint InvokedBit20Refresh;
        public uint Bit20StillSet;
        public uint NegativeFieldA0;
        public uint LowNibbleFlagsPresent;
        public uint RestoredSnapshotState;
        public uint RerouteLoopUsed;
        public uint InvokedRerouteCleanup;
        public int RoundedMilliseconds;
        public int AlternateWindowCommitArgument;
        public int InputField78;
        public int OutputField78;
        public float FieldA0;
    }

    public enum GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind : uint
    {
        AcceptCandidate = 0,
        AbortReset = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace
    {
        public uint AttemptIndex;
        public uint CheckedDriftThresholds;
        public uint ExceededHorizontalDrift;
        public uint ExceededVerticalAbortThreshold;
        public uint NormalizedCandidate2D;
        public uint ZeroedField84;
        public uint UpdatedDirectionFields;
        public GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind DispatchKind;
        public Vector3 NormalizedInputDirection;
        public Vector3 LateralOffset;
        public Vector3 OriginalPosition;
        public Vector3 CurrentPosition;
        public Vector3 CandidateVector;
        public Vector3 OutputDirectionVector;
        public Vector3 OutputNextInputVector;
        public float RemainingMagnitude;
        public float OriginalHorizontalMagnitude;
        public float OriginalVerticalMagnitude;
        public float CandidateDriftDistance2D;
        public float CandidateLength2D;
        public float OutputMagnitude;
        public float PreviousField68;
        public float PreviousField6c;
        public float PreviousField84;
        public float OutputField68;
        public float OutputField6c;
        public float OutputField84;
    }

    public enum GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind : uint
    {
        RejectNoFallback = 0,
        UseVerticalFallback = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace
    {
        public uint VerticalFallbackAlreadyUsed;
        public uint HorizontalMagnitudeExceedsEpsilon;
        public uint ClearedField84;
        public GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind DispatchKind;
        public Vector3 NormalizedInputDirection;
        public Vector3 OutputNextInputVector;
        public float CurrentHorizontalMagnitude;
        public float RemainingMagnitude;
        public float OutputMagnitude;
        public float OutputField84;
    }

    public enum GroundedDriverSelectedPlaneTailRerouteLoopControllerKind : uint
    {
        AcceptCandidate = 0,
        UseVerticalFallback = 1,
        ResetState = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace
    {
        public GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace CandidateTrace;
        public GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace VerticalFallbackTrace;
        public uint InputAttemptIndex;
        public uint OutputAttemptIndex;
        public uint IncrementedAttemptBeforeProbe;
        public uint AttemptLimitExceeded;
        public uint InvokedCandidateProbe;
        public uint InvokedVerticalFallback;
        public uint InvokedResetStateHandler;
        public uint RerouteLoopUsed;
        public uint OutputVerticalFallbackUsed;
        public GroundedDriverSelectedPlaneTailRerouteLoopControllerKind DispatchKind;
        public Vector3 OutputNextInputVector;
        public Vector3 OutputDirectionVector;
        public float InputCurrentHorizontalMagnitude;
        public float OutputMagnitude;
        public float OutputField68;
        public float OutputField6c;
        public float OutputField84;
    }

    public enum GroundedDriverSelectedPlaneTailChooserProbeKind : uint
    {
        RejectHorizontal = 0,
        AcceptSelectedPlane = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailChooserProbeTrace
    {
        public uint OutsideCollisionRadius;
        public uint AlignmentAccepted;
        public int ProbeBudgetMilliseconds;
        public GroundedDriverSelectedPlaneTailChooserProbeKind DispatchKind;
        public Vector3 InputPackedPairVector;
        public Vector3 InputProjectedMove;
        public Vector3 ProbePosition;
        public Vector3 ProbeDelta;
        public float ChooserInputScalar;
        public float CollisionRadius;
        public float ProbeDistance2D;
        public float AlignmentDot;
    }

    public enum GroundedDriverSelectedPlaneTailChooserContractKind : uint
    {
        Return1Horizontal = 1,
        Return2SelectedPlane = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailChooserContractTrace
    {
        public uint FinalSelectedIndex;
        public uint FinalSelectedCount;
        public uint FinalSelectedIndexInRange;
        public uint FinalSelectedPlaneWalkable;
        public uint Called635F80;
        public uint ChooserAcceptedSelectedPlane;
        public uint GroundedWall04000000Set;
        public uint WroteField80FromSelectedZ;
        public uint ProjectedMoveMutatedByChooser;
        public GroundedDriverSelectedPlaneTailChooserContractKind DispatchKind;
        public Vector3 ChooserInputPackedPairVector;
        public Vector3 ChooserInputProjectedMove;
        public Vector3 ChooserOutputProjectedMove;
        public float ChooserInputScalar;
        public float FinalSelectedNormalZ;
    }

    public enum GroundedDriverSelectedPlaneTailReturnDispatchKind : uint
    {
        Return0Exit = 0,
        Return1Horizontal = 1,
        Return2SelectedPlane = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPlaneTailReturnDispatchTrace
    {
        public GroundedDriverSelectedPlanePostFastReturnTailTrace PostFastReturnTailTrace;
        public uint CalledTailRerank;
        public uint TailRerankSucceeded;
        public uint FinalSelectedIndex;
        public uint FinalSelectedCount;
        public uint FinalSelectedIndexInRange;
        public uint FinalSelectedPlaneWalkable;
        public uint Called635F80;
        public uint ChooserAcceptedSelectedPlane;
        public uint GroundedWall04000000Set;
        public uint WroteField80FromSelectedZ;
        public GroundedDriverSelectedPlaneTailReturnDispatchKind DispatchKind;
        public Vector3 OutputProjectedMove;
        public float OutputResolved2D;
        public float OutputPositionZ;
        public float FinalSelectedNormalZ;
    }

    public enum GroundedDriverSelectedPairCommitTailKind : uint
    {
        StartFallZero = 0,
        ForwardPair = 1,
        DeferredHoverRerank = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPairCommitTailTrace
    {
        public uint SelectedIndex;
        public uint SelectedCount;
        public uint ConsumedSelectedState;
        public uint SnapshotBeforeCommitState;
        public GroundedDriverSelectedPairCommitTailKind DispatchKind;
        public uint UsedStartFallZero;
        public uint UsedCacheSnapshot;
        public uint ForwardedPair;
        public uint DeferredHoverRerank;
        public uint InputMovementFlags;
        public uint OutputMovementFlags;
        public SelectorPair CachedPair;
        public SelectorPair OutputPair;
        public Vector3 CurrentPosition;
        public Vector3 InputCachedPosition;
        public Vector3 OutputCachedPosition;
        public float CurrentFacing;
        public float InputCachedFacing;
        public float OutputCachedFacing;
        public float CurrentPitch;
        public float InputCachedPitch;
        public float OutputCachedPitch;
        public uint InputMoveTimestamp;
        public uint OutputMoveTimestamp;
        public uint InputFallTime;
        public uint OutputFallTime;
        public float InputFallStartZ;
        public float OutputFallStartZ;
        public float InputVerticalSpeed;
        public float OutputVerticalSpeed;
    }

    public enum GroundedDriverSelectedPairCommitGuardKind : uint
    {
        RejectProbeHit = 0,
        RejectContextMismatch = 1,
        RejectAttachedBit = 2,
        CallOpaqueConsumer = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPairCommitGuardTrace
    {
        public uint ZeroIncomingPair;
        public uint StoredPairNonZero;
        public uint ProbeRejectChecked;
        public uint ProbeRejected;
        public uint ContextMatchesGlobal;
        public uint HasAttachedPointer;
        public uint AttachedBit4Set;
        public uint CalledOpaqueConsumer;
        public GroundedDriverSelectedPairCommitGuardKind GuardKind;
        public int ReturnValue;
        public SelectorPair IncomingPair;
        public SelectorPair StoredPair;
    }

    public enum GroundedDriverSelectedPairCommitBodyKind : uint
    {
        RejectUnchangedPair = 0,
        RejectIncomingPairValidator = 1,
        CommitPair = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverSelectedPairCommitBodyTrace
    {
        public uint IncomingPairNonZero;
        public uint StoredPairNonZero;
        public uint IncomingPairMatchesStoredPair;
        public uint CalledIncomingPairValidator;
        public uint IncomingPairValidatorAccepted;
        public uint InitializedStoredTransformIdentity;
        public uint InitializedIncomingTransformIdentity;
        public uint ProcessedStoredPair;
        public uint ProcessedIncomingPair;
        public uint CalledStoredAttachmentBridge;
        public uint CalledIncomingAttachmentBridge;
        public uint AppliedStoredTransformScalar;
        public uint AppliedStoredTransformMatrix;
        public uint AppliedStoredTransformFinalize;
        public uint AppliedIncomingTransformScalar;
        public uint AppliedIncomingTransformMatrix;
        public uint AppliedIncomingTransformFinalize;
        public uint WroteCommittedPair;
        public uint CalledCommitNotification;
        public GroundedDriverSelectedPairCommitBodyKind CommitKind;
        public int ReturnValue;
        public float StoredPhaseScalar;
        public float IncomingPhaseScalar;
        public SelectorPair IncomingPair;
        public SelectorPair StoredPair;
        public SelectorPair OutputCommittedPair;
    }

    public enum GroundedDriverHoverRerankDispatchKind : uint
    {
        ReturnWithoutCommit = 0,
        StartFallZero = 1,
        ForwardPair = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedDriverHoverRerankTrace
    {
        public uint FirstRerankSucceeded;
        public uint SelectedIndex;
        public uint SelectedCount;
        public uint SelectedIndexInRange;
        public uint UsedStandardWalkableThreshold;
        public float SelectedNormalZ;
        public float ThresholdNormalZ;
        public uint SelectedNormalAccepted;
        public uint LoadedSelectedPair;
        public float InputWindowSpanScalar;
        public float FollowupScalarCandidate;
        public uint UsedDirectForwardAboveOne;
        public uint ZeroedFollowupScalar;
        public uint ClampedFollowupScalar;
        public float OutputFollowupScalar;
        public uint CalledSecondRerank;
        public uint SecondRerankSucceeded;
        public uint ForwardedPair;
        public uint StartedFallWithZeroVelocity;
        public uint AdvancedPositionZ;
        public GroundedDriverHoverRerankDispatchKind DispatchKind;
        public uint InputMovementFlags;
        public uint OutputMovementFlags;
        public SelectorPair OutputPair;
        public float InputPositionZ;
        public float OutputPositionZ;
        public uint InputFallTime;
        public uint OutputFallTime;
        public float InputFallStartZ;
        public float OutputFallStartZ;
        public float InputVerticalSpeed;
        public float OutputVerticalSpeed;
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverFirstDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverFirstDispatch(
        uint walkableSelectedContact,
        uint gateReturnCode,
        float remainingDistanceBeforeDispatch,
        float sweepDistanceBeforeVertical,
        float sweepDistanceAfterVertical,
        out GroundedDriverFirstDispatchTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedContactDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedContactDispatch(
        uint checkWalkableAccepted,
        uint consumedSelectedState,
        uint movementFlags,
        float remainingDistanceBeforeDispatch,
        float sweepDistanceBeforeVertical,
        float sweepDistanceAfterVertical,
        uint inputFallTime,
        float inputFallStartZ,
        float inputVerticalSpeed,
        float positionZ,
        out GroundedDriverSelectedContactDispatchTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverResweepBookkeeping", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverResweepBookkeeping(
        in Vector3 direction,
        float sweepScalar,
        in Vector3 correction,
        float horizontalBudgetBefore,
        out GroundedDriverResweepBookkeepingTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverVerticalCap", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverVerticalCap(
        uint movementFlags,
        float combinedMoveZ,
        float nextSweepDistance,
        float currentZ,
        float boundingRadius,
        float capField80,
        out GroundedDriverVerticalCapTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneCorrection", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneCorrection(
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float verticalLimit,
        float remainingDistanceBefore,
        float sweepDistanceBefore,
        float sweepDistanceAfter,
        float inputSweepFraction,
        float inputDistancePointer,
        out GroundedDriverSelectedPlaneCorrectionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
        uint useSelectedPlaneOverride,
        float selectedContactNormalZ,
        in Vector3 selectedPlaneNormal,
        in Vector3 inputWorkingVector,
        in Vector3 inputMoveDirection,
        float inputDistancePointer,
        uint movementFlags,
        float boundingRadius,
        float remainingDistanceBefore,
        float inputSweepFraction,
        out GroundedDriverSelectedPlaneCorrectionTransactionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
        uint useSelectedPlaneOverride,
        float selectedContactNormalZ,
        in Vector3 selectedPlaneNormal,
        in Vector3 inputWorkingVector,
        in Vector3 inputMoveDirection,
        float inputDistancePointer,
        uint movementFlags,
        float boundingRadius,
        out GroundedDriverSelectedPlaneDistancePointerTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverHorizontalCorrection", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverHorizontalCorrection(
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        out GroundedDriverHorizontalCorrectionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
        uint walkableSelectedContact,
        uint gateReturnCode,
        uint useSelectedPlaneOverride,
        float selectedContactNormalZ,
        in Vector3 selectedPlaneNormal,
        in Vector3 inputWorkingVector,
        in Vector3 inputMoveDirection,
        float inputDistancePointer,
        uint movementFlags,
        float boundingRadius,
        float remainingDistanceBefore,
        float inputSweepFraction,
        out GroundedDriverSelectedPlaneRetryTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup(
        in Vector3 inputPackedPairVector,
        float fieldB0,
        float boundingRadius,
        in Vector3 inputContactNormal,
        uint firstPassRerankSucceeded,
        out GroundedDriverSelectedPlaneFirstPassSetupTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneBranchGate", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneBranchGate(
        uint walkableSelectedContact,
        uint gateReturnCode,
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float verticalLimit,
        float remainingDistanceBefore,
        float sweepDistanceBefore,
        float sweepDistanceAfter,
        float inputSweepFraction,
        float inputDistancePointer,
        out GroundedDriverSelectedPlaneBranchGateTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank(
        uint selectedIndex,
        uint selectedCount,
        in Vector3 inputContactNormal,
        in Vector3 selectedRecordNormal,
        in Vector3 inputPackedPairVector,
        in Vector3 rerankedPackedPairVector,
        uint secondRerankSucceeded,
        out GroundedDriverSelectedPlaneFollowupRerankTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup(
        in Vector3 inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        float tailTransformScalar,
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float selectedContactNormalZ,
        uint movementFlags,
        float boundingRadius,
        uint invokeVerticalCorrection,
        out GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
        in Vector3 inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        float tailTransformScalar,
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float selectedContactNormalZ,
        uint movementFlags,
        float boundingRadius,
        uint invokeVerticalCorrection,
        uint thirdPassRerankSucceeded,
        float verticalLimit,
        out GroundedDriverSelectedPlaneTailProjectedBlendTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailWriteback", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailWriteback(
        in Vector3 inputPosition,
        in Vector3 inputPackedPairVector,
        float followupScalar,
        float scalarFloor,
        float inputSelectedContactNormalZ,
        uint checkWalkableAccepted,
        uint projectedTailRerankSucceeded,
        in Vector3 projectedTailMove,
        float projectedTailResolved2D,
        out GroundedDriverSelectedPlaneTailWritebackTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract(
        in Vector3 chooserInputPackedPairVector,
        in Vector3 chooserInputProjectedMove,
        float chooserInputScalar,
        uint finalSelectedIndex,
        uint finalSelectedCount,
        float finalSelectedNormalZ,
        uint chooserAcceptedSelectedPlane,
        uint movementFlags,
        in Vector3 chooserOutputProjectedMove,
        out GroundedDriverSelectedPlaneTailChooserContractTrace trace);

    [DllImport(NavigationDll, EntryPoint = "CaptureWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CaptureWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot(
        in Vector3 field44Vector,
        in Vector3 field50Vector,
        uint field78,
        in Vector3 field5cVector,
        float field68,
        float field6c,
        uint field40Flags,
        float field84,
        out GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace trace);

    [DllImport(NavigationDll, EntryPoint = "RestoreWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot", CallingConvention = CallingConvention.Cdecl)]
    public static extern void RestoreWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot(
        in GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace snapshot,
        out GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailElapsedMilliseconds", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWGroundedDriverSelectedPlaneTailElapsedMilliseconds(
        float elapsedSeconds,
        out GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup(
        uint inputWindowMilliseconds,
        in Vector3 requestedMove,
        in Vector3 currentPosition,
        in Vector3 field44Vector,
        in Vector3 field50Vector,
        uint field78,
        in Vector3 field5cVector,
        float field68,
        float field6c,
        uint field40Flags,
        float field84,
        out GroundedDriverSelectedPlaneTailEntrySetupTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding(
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
        in Vector3 startPosition,
        float elapsedScalar,
        float facing,
        float pitch,
        in Vector3 cachedPosition,
        float cachedFacing,
        float cachedPitch,
        uint cachedMoveTimestamp,
        float cachedScalar84,
        float recomputedScalar84,
        out GroundedDriverSelectedPlaneTailPostForwardingTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch(
        float consumedWindowSeconds,
        int field58,
        int field78,
        out GroundedDriverSelectedPlaneTailReturn2LateBranchTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier(
        int roundedMilliseconds,
        uint alternateUnitZStateBit,
        int field78,
        uint notifyRequested,
        int alternateWindowCommitBase,
        uint sidecarStatePresent,
        uint bit20InitiallySet,
        uint commitGuardPassed,
        uint bit20StillSet,
        float fieldA0,
        uint lowNibbleFlags,
        in GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace snapshot,
        uint rerouteLoopUsed,
        out GroundedDriverSelectedPlaneTailLateNotifierTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
        uint attemptIndex,
        in Vector3 normalizedInputDirection,
        float remainingMagnitude,
        in Vector3 lateralOffset,
        in Vector3 originalPosition,
        in Vector3 currentPosition,
        float originalHorizontalMagnitude,
        float originalVerticalMagnitude,
        float previousField68,
        float previousField6c,
        float previousField84,
        out GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback(
        in Vector3 normalizedInputDirection,
        float currentHorizontalMagnitude,
        float remainingMagnitude,
        uint verticalFallbackAlreadyUsed,
        out GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController(
        uint attemptIndex,
        uint incrementAttemptBeforeProbe,
        in Vector3 normalizedInputDirection,
        float remainingMagnitude,
        float currentHorizontalMagnitude,
        in Vector3 lateralOffset,
        in Vector3 originalPosition,
        in Vector3 currentPosition,
        float originalHorizontalMagnitude,
        float originalVerticalMagnitude,
        float previousField68,
        float previousField6c,
        float previousField84,
        uint verticalFallbackAlreadyUsed,
        out GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe(
        in Vector3 inputPackedPairVector,
        in Vector3 inputProjectedMove,
        float chooserInputScalar,
        in Vector3 probePosition,
        float collisionRadius,
        out GroundedDriverSelectedPlaneTailChooserProbeTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
        in Vector3 inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        uint thirdPassRerankSucceeded,
        out GroundedDriverSelectedPlaneThirdPassSetupTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection(
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float verticalLimit,
        in Vector3 horizontalProjectedMove,
        float horizontalResolved2D,
        out GroundedDriverSelectedPlaneBlendCorrectionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
        in Vector3 inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        uint thirdPassRerankSucceeded,
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float verticalLimit,
        in Vector3 horizontalProjectedMove,
        float horizontalResolved2D,
        out GroundedDriverSelectedPlanePostFastReturnTailTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
        in Vector3 inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        uint thirdPassRerankSucceeded,
        in Vector3 requestedMove,
        in Vector3 selectedPlaneNormal,
        float verticalLimit,
        in Vector3 horizontalProjectedMove,
        float horizontalResolved2D,
        uint tailRerankSucceeded,
        uint finalSelectedIndex,
        uint finalSelectedCount,
        float finalSelectedNormalZ,
        uint chooserAcceptedSelectedPlane,
        uint movementFlags,
        out GroundedDriverSelectedPlaneTailReturnDispatchTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPairCommitTail", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverSelectedPairCommitTail(
        uint selectedIndex,
        uint selectedCount,
        uint consumedSelectedState,
        uint snapshotBeforeCommitState,
        uint movementFlags,
        in SelectorPair cachedPair,
        in Vector3 currentPosition,
        float currentFacing,
        float currentPitch,
        in Vector3 cachedPosition,
        float cachedFacing,
        float cachedPitch,
        uint cachedMoveTimestamp,
        uint inputFallTime,
        float inputFallStartZ,
        float inputVerticalSpeed,
        out GroundedDriverSelectedPairCommitTailTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPairCommitGuard", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWGroundedDriverSelectedPairCommitGuard(
        in SelectorPair incomingPair,
        in SelectorPair storedPair,
        uint probeRejectOnStoredPairUnload,
        uint contextMatchesGlobal,
        uint hasAttachedPointer,
        uint attachedBit4Set,
        int opaqueConsumerReturnValue,
        out GroundedDriverSelectedPairCommitGuardTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverSelectedPairCommitBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWGroundedDriverSelectedPairCommitBody(
        in SelectorPair incomingPair,
        in SelectorPair storedPair,
        uint incomingPairValidatorAccepted,
        uint hasTransformConsumer,
        float storedPhaseScalar,
        float incomingPhaseScalar,
        out GroundedDriverSelectedPairCommitBodyTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWGroundedDriverHoverRerankDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWGroundedDriverHoverRerankDispatch(
        uint firstRerankSucceeded,
        uint selectedIndex,
        uint selectedCount,
        uint useStandardWalkableThreshold,
        float selectedNormalZ,
        in SelectorPair selectedPair,
        float inputWindowSpanScalar,
        float followupScalarCandidate,
        uint secondRerankSucceeded,
        uint movementFlags,
        float positionZ,
        uint inputFallTime,
        float inputFallStartZ,
        float inputVerticalSpeed,
        out GroundedDriverHoverRerankTrace trace);
}
