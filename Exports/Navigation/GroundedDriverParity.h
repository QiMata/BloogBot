#pragma once

#include "PhysicsEngine.h"
#include "SelectorChosenContact.h"

namespace WoWCollision
{
    enum class GroundedDriverFirstDispatchKind : uint32_t
    {
        Exit = 0u,
        Horizontal = 1u,
        WalkableSelectedVertical = 2u,
        NonWalkableVertical = 3u,
    };

    struct GroundedDriverFirstDispatchTrace
    {
        uint32_t WalkableSelectedContact = 0u;
        uint32_t GateReturnCode = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverFirstDispatchKind::Exit);
        uint32_t SetGroundedWall04000000 = 0u;
        uint32_t UsesVerticalHelper = 0u;
        uint32_t UsesHorizontalHelper = 0u;
        uint32_t RemainingDistanceRescaled = 0u;
        float RemainingDistanceBeforeDispatch = 0.0f;
        float RemainingDistanceAfterDispatch = 0.0f;
        float SweepDistanceBeforeVertical = 0.0f;
        float SweepDistanceAfterVertical = 0.0f;
    };

    enum class GroundedDriverSelectedContactDispatchKind : uint32_t
    {
        StartFallZero = 0u,
        DelegateToNonWalkableDispatch = 1u,
        WalkableSelectedVertical = 2u,
    };

    struct GroundedDriverSelectedContactDispatchTrace
    {
        uint32_t CheckWalkableAccepted = 0u;
        uint32_t ConsumedSelectedState = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedContactDispatchKind::StartFallZero);
        uint32_t BypassedNonWalkableDispatch = 0u;
        uint32_t DelegatedToNonWalkableDispatch = 0u;
        uint32_t StartedFallWithZeroVelocity = 0u;
        uint32_t ClearedSplineElevation04000000 = 0u;
        uint32_t ClearedSwimming00200000 = 0u;
        uint32_t SetJumping = 0u;
        uint32_t ResetFallTime = 0u;
        uint32_t ResetFallStartZ = 0u;
        uint32_t ResetVerticalSpeed = 0u;
        uint32_t DroppedChosenPair = 0u;
        uint32_t InputMovementFlags = 0u;
        uint32_t OutputMovementFlags = 0u;
        float RemainingDistanceBeforeDispatch = 0.0f;
        float RemainingDistanceAfterDispatch = 0.0f;
        float SweepDistanceBeforeVertical = 0.0f;
        float SweepDistanceAfterVertical = 0.0f;
        uint32_t InputFallTime = 0u;
        uint32_t OutputFallTime = 0u;
        float InputFallStartZ = 0.0f;
        float OutputFallStartZ = 0.0f;
        float InputVerticalSpeed = 0.0f;
        float OutputVerticalSpeed = 0.0f;
        float PositionZ = 0.0f;
    };

    struct GroundedDriverResweepBookkeepingTrace
    {
        uint32_t NormalizedDirection = 0u;
        uint32_t WroteHorizontalPair = 0u;
        uint32_t NormalizedHorizontalPair = 0u;
        uint32_t FinalizeFlag = 0u;
        uint32_t TinyMagnitudeFinalize = 0u;
        uint32_t HorizontalBudgetFinalize = 0u;
        G3D::Vector3 InputDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputSweepScalar = 0.0f;
        G3D::Vector3 InputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputHorizontalBudget = 0.0f;
        G3D::Vector3 OutputCombinedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float OutputSweepDistance = 0.0f;
        G3D::Vector3 OutputDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float OutputHorizontalX = 0.0f;
        float OutputHorizontalY = 0.0f;
        float OutputCombinedXYMagnitude = 0.0f;
        float OutputHorizontalBudget = 0.0f;
    };

    struct GroundedDriverVerticalCapTrace
    {
        uint32_t CapBitSet = 0u;
        uint32_t PositiveCombinedMoveZ = 0u;
        uint32_t AppliedCap = 0u;
        uint32_t SetFinalizeFlag20 = 0u;
        uint32_t SetTinySweepFlag30 = 0u;
        float CombinedMoveZ = 0.0f;
        float InputSweepDistance = 0.0f;
        float OutputSweepDistance = 0.0f;
        float CurrentZ = 0.0f;
        float BoundingRadius = 0.0f;
        float CapField80 = 0.0f;
        float CapAbsoluteZ = 0.0f;
        float PredictedZ = 0.0f;
        float AllowedDeltaZ = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneCorrectionKind : uint32_t
    {
        VerticalOnly = 0u,
    };

    struct GroundedDriverSelectedPlaneCorrectionTrace
    {
        uint32_t WroteVerticalOnlyCorrection = 0u;
        uint32_t ClampedVerticalMagnitude = 0u;
        uint32_t MutatedDistancePointer = 0u;
        uint32_t RescaledRemainingDistance = 0u;
        uint32_t RescaledSweepFraction = 0u;
        uint32_t CorrectionKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneCorrectionKind::VerticalOnly);
        G3D::Vector3 RequestedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ProjectedCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float IntoPlane = 0.0f;
        float ProjectedVertical = 0.0f;
        float VerticalLimit = 0.0f;
        float InputRemainingDistance = 0.0f;
        float OutputRemainingDistance = 0.0f;
        float InputSweepDistance = 0.0f;
        float OutputSweepDistance = 0.0f;
        float InputSweepFraction = 0.0f;
        float OutputSweepFraction = 0.0f;
        float InputDistancePointer = 0.0f;
        float OutputDistancePointer = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneCorrectionTransactionKind : uint32_t
    {
        DirectScalar = 0u,
        PositiveRadiusClamp = 1u,
        NegativeRadiusClamp = 2u,
        FlaggedNegativeZeroDistance = 3u,
    };

    enum class GroundedDriverHorizontalCorrectionKind : uint32_t
    {
        ZeroedOnReject = 0u,
        HorizontalEpsilonProjection = 1u,
    };

    enum class GroundedDriverSelectedPlaneDistancePointerKind : uint32_t
    {
        DirectScalar = 0u,
        PositiveRadiusClamp = 1u,
        NegativeRadiusClamp = 2u,
        FlaggedNegativeZeroDistance = 3u,
    };

    struct GroundedDriverSelectedPlaneDistancePointerTrace
    {
        uint32_t UseSelectedPlaneOverride = 0u;
        uint32_t SelectedContactNormalWithinOverrideBand = 0u;
        uint32_t UsedSelectedPlaneNormalOverride = 0u;
        uint32_t UsedInfiniteScalar = 0u;
        uint32_t GroundedWall04000000Set = 0u;
        uint32_t ZeroedDistancePointer = 0u;
        uint32_t MutatedDistancePointer = 0u;
        uint32_t OutputKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneDistancePointerKind::DirectScalar);
        G3D::Vector3 InputMoveDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 EffectiveWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float SelectedContactNormalZ = 0.0f;
        float SelectedPlaneMagnitudeSquared = 0.0f;
        float InputDistancePointer = 0.0f;
        float OutputDistancePointer = 0.0f;
        float BoundingRadius = 0.0f;
        float DotScaledDistance = 0.0f;
        float RawScalar = 0.0f;
        float OutputScalar = 0.0f;
    };

    struct GroundedDriverSelectedPlaneCorrectionTransactionTrace
    {
        GroundedDriverSelectedPlaneDistancePointerTrace DistancePointerTrace{};
        uint32_t WroteVerticalOnlyCorrection = 0u;
        uint32_t MutatedDistancePointer = 0u;
        uint32_t RescaledRemainingDistance = 0u;
        uint32_t RescaledSweepFraction = 0u;
        uint32_t CorrectionKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneCorrectionTransactionKind::DirectScalar);
        uint32_t InputMovementFlags = 0u;
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputMoveDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float SelectedContactNormalZ = 0.0f;
        float BoundingRadius = 0.0f;
        float InputRemainingDistance = 0.0f;
        float OutputRemainingDistance = 0.0f;
        float InputSweepFraction = 0.0f;
        float OutputSweepFraction = 0.0f;
        float InputDistancePointer = 0.0f;
        float OutputDistancePointer = 0.0f;
    };

    struct GroundedDriverHorizontalCorrectionTrace
    {
        uint32_t EntryGateAccepted = 0u;
        uint32_t NormalizedHorizontalNormal = 0u;
        uint32_t AppliedEpsilonPushout = 0u;
        uint32_t ZeroedOutputOnReject = 0u;
        uint32_t CorrectionKind = static_cast<uint32_t>(GroundedDriverHorizontalCorrectionKind::ZeroedOnReject);
        G3D::Vector3 RequestedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 NormalizedHorizontalNormalVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float IntoPlane = 0.0f;
        float HorizontalMagnitude = 0.0f;
        float InverseHorizontalMagnitude = 0.0f;
        float OutputResolved2D = 0.0f;
    };

    struct GroundedDriverSelectedPlaneRetryTrace
    {
        GroundedDriverSelectedPlaneDistancePointerTrace DistancePointerTrace{};
        GroundedDriverHorizontalCorrectionTrace HorizontalCorrectionTrace{};
        uint32_t WalkableSelectedContact = 0u;
        uint32_t GateReturnCode = 0u;
        uint32_t SetGroundedWall04000000 = 0u;
        uint32_t UsesVerticalHelper = 0u;
        uint32_t UsesHorizontalHelper = 0u;
        uint32_t MutatedDistancePointer = 0u;
        uint32_t RescaledRemainingDistance = 0u;
        uint32_t RescaledSweepFraction = 0u;
        uint32_t InputMovementFlags = 0u;
        uint32_t OutputMovementFlags = 0u;
        uint32_t BranchKind = 0u;
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputMoveDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float SelectedContactNormalZ = 0.0f;
        float BoundingRadius = 0.0f;
        float InputRemainingDistance = 0.0f;
        float OutputRemainingDistance = 0.0f;
        float InputSweepFraction = 0.0f;
        float OutputSweepFraction = 0.0f;
        float InputDistancePointer = 0.0f;
        float OutputDistancePointer = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneFirstPassSetupKind : uint32_t
    {
        SkipToFollowupRerank = 0u,
        FirstPassFailureExit = 1u,
        ContinueToFollowupRerank = 2u,
    };

    struct GroundedDriverSelectedPlaneFirstPassSetupTrace
    {
        uint32_t SupportPlaneInitCount = 0u;
        uint32_t LoadedInputPackedPair = 0u;
        uint32_t UsedBoundingRadiusTanFloor = 0u;
        uint32_t EnteredFirstPassNormalBand = 0u;
        uint32_t InvokedFirstPassRerank = 0u;
        uint32_t FirstPassRerankSucceeded = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneFirstPassSetupKind::SkipToFollowupRerank);
        G3D::Vector3 InputContactNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 FirstPassWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float FieldB0 = 0.0f;
        float BoundingRadius = 0.0f;
        float SkinAdjustedFieldB0 = 0.0f;
        float BoundingRadiusTanFloor = 0.0f;
        float OutputScalarFloor = 0.0f;
        float HorizontalMagnitude = 0.0f;
        float InverseHorizontalMagnitude = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneBranchGateKind : uint32_t
    {
        ExitWithoutMutation = 0u,
        HorizontalRetry = 1u,
        VerticalRetry = 2u,
        WalkableSelectedVertical = 3u,
    };

    struct GroundedDriverSelectedPlaneBranchGateTrace
    {
        GroundedDriverHorizontalCorrectionTrace HorizontalCorrectionTrace{};
        uint32_t WalkableSelectedContact = 0u;
        uint32_t GateReturnCode = 0u;
        uint32_t SetGroundedWall04000000 = 0u;
        uint32_t UsesVerticalHelper = 0u;
        uint32_t UsesHorizontalHelper = 0u;
        uint32_t RemainingDistanceRescaled = 0u;
        uint32_t MutatedDistancePointer = 0u;
        uint32_t RescaledSweepFraction = 0u;
        uint32_t BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::ExitWithoutMutation);
        G3D::Vector3 RequestedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCorrection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputRemainingDistance = 0.0f;
        float OutputRemainingDistance = 0.0f;
        float InputSweepDistance = 0.0f;
        float OutputSweepDistance = 0.0f;
        float InputSweepFraction = 0.0f;
        float OutputSweepFraction = 0.0f;
        float InputDistancePointer = 0.0f;
        float OutputDistancePointer = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneFollowupRerankKind : uint32_t
    {
        ExitWithoutSelection = 0u,
        HorizontalFastReturn = 1u,
        ContinueToUncapturedTail = 2u,
    };

    struct GroundedDriverSelectedPlaneFollowupRerankTrace
    {
        uint32_t SelectedIndex = 0u;
        uint32_t SelectedCount = 0u;
        uint32_t SelectedIndexInRange = 0u;
        uint32_t SelectedRecordMatchesInputNormal = 0u;
        uint32_t ReloadedInputPackedPair = 0u;
        uint32_t RetainedRerankedPackedPair = 0u;
        uint32_t UsedUnitZWorkingVector = 0u;
        uint32_t CalledSecondRerank = 0u;
        uint32_t SecondRerankSucceeded = 0u;
        uint32_t HorizontalFastReturn = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneFollowupRerankKind::ExitWithoutSelection);
        G3D::Vector3 InputContactNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedRecordNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 RerankedPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 EffectivePackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SecondPassWorkingVector = G3D::Vector3(0.0f, 0.0f, 1.0f);
    };

    enum class GroundedDriverSelectedPlaneTailPreThirdPassSetupKind : uint32_t
    {
        UseHorizontalFallbackInputs = 0u,
        UseProjectedTailRerankInputs = 1u,
    };

    struct GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace
    {
        GroundedDriverSelectedPlaneCorrectionTransactionTrace CorrectionTransactionTrace{};
        GroundedDriverHorizontalCorrectionTrace HorizontalCorrectionTrace{};
        uint32_t InvokedVerticalCorrection = 0u;
        uint32_t RejectedOnTransformMagnitude = 0u;
        uint32_t PreparedTailRerankInputs = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailPreThirdPassSetupKind::UseHorizontalFallbackInputs);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputPositionZ = 0.0f;
        float OutputPositionZ = 0.0f;
        float InputFollowupScalar = 0.0f;
        float OutputFollowupScalar = 0.0f;
        float InputScalarFloor = 0.0f;
        float OutputScalarFloor = 0.0f;
        float InputTailTransformScalar = 0.0f;
        float OutputTailTransformScalar = 0.0f;
        G3D::Vector3 HorizontalProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float HorizontalResolved2D = 0.0f;
        G3D::Vector3 ProjectedTailWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float ProjectedTailDistance = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailProjectedBlendKind : uint32_t
    {
        ExitWithoutSelection = 0u,
        UseHorizontalFallback = 1u,
        UseSelectedPlaneBlend = 2u,
    };

    struct GroundedDriverSelectedPlaneTailProjectedBlendTrace;

    enum class GroundedDriverSelectedPlaneThirdPassSetupKind : uint32_t
    {
        ExitWithoutSelection = 0u,
        ContinueToBlendCorrection = 1u,
    };

    struct GroundedDriverSelectedPlaneThirdPassSetupTrace
    {
        uint32_t LoadedPackedPairXY = 0u;
        uint32_t ZeroedWorkingVectorZ = 0u;
        uint32_t AdvancedPositionZ = 0u;
        uint32_t InvokedThirdPassRerank = 0u;
        uint32_t ThirdPassRerankSucceeded = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneThirdPassSetupKind::ExitWithoutSelection);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ThirdPassWorkingVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputPositionZ = 0.0f;
        float OutputPositionZ = 0.0f;
        float FollowupScalar = 0.0f;
        float ScalarFloor = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneBlendCorrectionKind : uint32_t
    {
        UseHorizontalFallback = 0u,
        UseSelectedPlaneBlend = 1u,
    };

    struct GroundedDriverSelectedPlaneBlendCorrectionTrace
    {
        uint32_t ClampedVerticalMagnitude = 0u;
        uint32_t DiscardedUphillBlend = 0u;
        uint32_t AcceptedSelectedPlaneBlend = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBlendCorrectionKind::UseHorizontalFallback);
        G3D::Vector3 RequestedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 HorizontalProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float IntoPlane = 0.0f;
        float VerticalLimit = 0.0f;
        float HorizontalResolved2D = 0.0f;
        float SlopedResolved2D = 0.0f;
        float OutputResolved2D = 0.0f;
    };

    enum class GroundedDriverSelectedPlanePostFastReturnTailKind : uint32_t
    {
        ExitWithoutSelection = 0u,
        UseHorizontalFallback = 1u,
        UseSelectedPlaneBlend = 2u,
    };

    struct GroundedDriverSelectedPlanePostFastReturnTailTrace
    {
        GroundedDriverSelectedPlaneThirdPassSetupTrace ThirdPassSetupTrace{};
        GroundedDriverSelectedPlaneBlendCorrectionTrace BlendCorrectionTrace{};
        uint32_t InvokedBlendCorrection = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlanePostFastReturnTailKind::ExitWithoutSelection);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 RequestedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 SelectedPlaneNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 HorizontalProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputPositionZ = 0.0f;
        float OutputPositionZ = 0.0f;
        float FollowupScalar = 0.0f;
        float ScalarFloor = 0.0f;
        float VerticalLimit = 0.0f;
        float HorizontalResolved2D = 0.0f;
        float OutputResolved2D = 0.0f;
    };

    struct GroundedDriverSelectedPlaneTailProjectedBlendTrace
    {
        GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace TailPreThirdPassSetupTrace{};
        GroundedDriverSelectedPlanePostFastReturnTailTrace PostFastReturnTailTrace{};
        uint32_t UsedProjectedTailRerankInputs = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProjectedBlendKind::ExitWithoutSelection);
        G3D::Vector3 OutputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float OutputResolved2D = 0.0f;
        float OutputPositionZ = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailWritebackKind : uint32_t
    {
        ThirdPassOnly = 0u,
        ThirdPassPlusProjectedTail = 1u,
    };

    struct GroundedDriverSelectedPlaneTailWritebackTrace
    {
        uint32_t TailScalarDiffExceedsEpsilon = 0u;
        uint32_t CheckWalkableAccepted = 0u;
        uint32_t ProjectedTailRerankSucceeded = 0u;
        uint32_t AppliedProjectedTailWriteback = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailWritebackKind::ThirdPassOnly);
        G3D::Vector3 InputPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ProjectedTailMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float FollowupScalar = 0.0f;
        float ScalarFloor = 0.0f;
        float OutputResolved2D = 0.0f;
        float ProjectedTailResolved2D = 0.0f;
        float InputSelectedContactNormalZ = 0.0f;
        float OutputSelectedContactNormalZ = 0.0f;
    };

    struct GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
    {
        G3D::Vector3 Field44Vector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 Field50Vector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        uint32_t Field78 = 0u;
        G3D::Vector3 Field5cVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float Field68 = 0.0f;
        float Field6c = 0.0f;
        uint32_t Field40Flags = 0u;
        float Field84 = 0.0f;
    };

    struct GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace
    {
        uint32_t AddedPositiveHalfBias = 0u;
        uint32_t AddedNegativeHalfBias = 0u;
        float InputElapsedSeconds = 0.0f;
        float ScaledMilliseconds = 0.0f;
        float AdjustedMilliseconds = 0.0f;
        int32_t RoundedMilliseconds = 0;
    };

    enum class GroundedDriverSelectedPlaneTailEntrySetupKind : uint32_t
    {
        ExitWithoutProbe = 0u,
        ContinueToProbe = 1u,
    };

    struct GroundedDriverSelectedPlaneTailEntrySetupTrace
    {
        GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace SnapshotTrace{};
        uint32_t ZeroedPairForwardState = 0u;
        uint32_t ZeroedDirectStateBit = 0u;
        uint32_t ZeroedSidecarState = 0u;
        uint32_t ZeroedLateralOffset = 0u;
        uint32_t BuiltNormalizedInputDirection = 0u;
        uint32_t InputWindowMilliseconds = 0u;
        uint32_t InputField78 = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailEntrySetupKind::ExitWithoutProbe);
        G3D::Vector3 InputRequestedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 CurrentPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputNormalizedInputDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputWindowSeconds = 0.0f;
        float Field78Seconds = 0.0f;
        float ElapsedScalar = 0.0f;
        float CurrentWindowScalar = 0.0f;
        float CurrentMagnitude = 0.0f;
        float CurrentHorizontalMagnitude = 0.0f;
        float AbsoluteVerticalMagnitude = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailPostForwardingKind : uint32_t
    {
        Return2LateBranch = 0u,
        AlternateUnitZ = 1u,
        DirectState = 2u,
        NonStatefulContinue = 3u,
    };

    struct GroundedDriverSelectedPlaneTailPostForwardingTrace
    {
        SelectorPairPostForwardingTrace PostForwardingTrace{};
        SelectorAlternateUnitZStateTrace AlternateUnitZStateTrace{};
        SelectorDirectStateTrace DirectStateTrace{};
        uint32_t InvokedAlternateUnitZStateHandler = 0u;
        uint32_t InvokedDirectStateHandler = 0u;
        uint32_t AppliedMoveToPosition = 0u;
        uint32_t AdvancedElapsedScalar = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailPostForwardingKind::Return2LateBranch);
        G3D::Vector3 InputPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputElapsedScalar = 0.0f;
        float OutputElapsedScalar = 0.0f;
    };

    struct GroundedDriverSelectedPlaneTailReturn2LateBranchTrace
    {
        GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace ElapsedMillisecondsTrace{};
        uint32_t InvokedConsumedWindowCommit = 0u;
        int32_t InputField58 = 0;
        int32_t OutputField58 = 0;
        int32_t InputField78 = 0;
        int32_t OutputField78 = 0;
    };

    struct GroundedDriverSelectedPlaneTailLateNotifierTrace
    {
        GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace SnapshotTrace{};
        uint32_t AlternateUnitZStateBit = 0u;
        uint32_t NotifyRequested = 0u;
        uint32_t SidecarStatePresent = 0u;
        uint32_t AddedRoundedMillisecondsToField78 = 0u;
        uint32_t InvokedAlternateWindowCommit = 0u;
        uint32_t InvokedSidecarCommit = 0u;
        uint32_t Bit20InitiallySet = 0u;
        uint32_t InvokedCommitGuard = 0u;
        uint32_t CommitGuardPassed = 0u;
        uint32_t ReturnedEarlyAfterCommitGuard = 0u;
        uint32_t InvokedBit20Refresh = 0u;
        uint32_t Bit20StillSet = 0u;
        uint32_t NegativeFieldA0 = 0u;
        uint32_t LowNibbleFlagsPresent = 0u;
        uint32_t RestoredSnapshotState = 0u;
        uint32_t RerouteLoopUsed = 0u;
        uint32_t InvokedRerouteCleanup = 0u;
        int32_t RoundedMilliseconds = 0;
        int32_t AlternateWindowCommitArgument = 0;
        int32_t InputField78 = 0;
        int32_t OutputField78 = 0;
        float FieldA0 = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind : uint32_t
    {
        AcceptCandidate = 0u,
        AbortReset = 1u,
    };

    struct GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace
    {
        uint32_t AttemptIndex = 0u;
        uint32_t CheckedDriftThresholds = 0u;
        uint32_t ExceededHorizontalDrift = 0u;
        uint32_t ExceededVerticalAbortThreshold = 0u;
        uint32_t NormalizedCandidate2D = 0u;
        uint32_t ZeroedField84 = 0u;
        uint32_t UpdatedDirectionFields = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind::AcceptCandidate);
        G3D::Vector3 NormalizedInputDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 LateralOffset = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OriginalPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 CurrentPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 CandidateVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputDirectionVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputNextInputVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float RemainingMagnitude = 0.0f;
        float OriginalHorizontalMagnitude = 0.0f;
        float OriginalVerticalMagnitude = 0.0f;
        float CandidateDriftDistance2D = 0.0f;
        float CandidateLength2D = 0.0f;
        float OutputMagnitude = 0.0f;
        float PreviousField68 = 0.0f;
        float PreviousField6c = 0.0f;
        float PreviousField84 = 0.0f;
        float OutputField68 = 0.0f;
        float OutputField6c = 0.0f;
        float OutputField84 = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind : uint32_t
    {
        RejectNoFallback = 0u,
        UseVerticalFallback = 1u,
    };

    struct GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace
    {
        uint32_t VerticalFallbackAlreadyUsed = 0u;
        uint32_t HorizontalMagnitudeExceedsEpsilon = 0u;
        uint32_t ClearedField84 = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind::RejectNoFallback);
        G3D::Vector3 NormalizedInputDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputNextInputVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float CurrentHorizontalMagnitude = 0.0f;
        float RemainingMagnitude = 0.0f;
        float OutputMagnitude = 0.0f;
        float OutputField84 = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailRerouteLoopControllerKind : uint32_t
    {
        AcceptCandidate = 0u,
        UseVerticalFallback = 1u,
        ResetState = 2u,
    };

    struct GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace
    {
        GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace CandidateTrace{};
        GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace VerticalFallbackTrace{};
        uint32_t InputAttemptIndex = 0u;
        uint32_t OutputAttemptIndex = 0u;
        uint32_t IncrementedAttemptBeforeProbe = 0u;
        uint32_t AttemptLimitExceeded = 0u;
        uint32_t InvokedCandidateProbe = 0u;
        uint32_t InvokedVerticalFallback = 0u;
        uint32_t InvokedResetStateHandler = 0u;
        uint32_t RerouteLoopUsed = 1u;
        uint32_t OutputVerticalFallbackUsed = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailRerouteLoopControllerKind::AcceptCandidate);
        G3D::Vector3 OutputNextInputVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputDirectionVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float InputCurrentHorizontalMagnitude = 0.0f;
        float OutputMagnitude = 0.0f;
        float OutputField68 = 0.0f;
        float OutputField6c = 0.0f;
        float OutputField84 = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailChooserProbeKind : uint32_t
    {
        RejectHorizontal = 0u,
        AcceptSelectedPlane = 1u,
    };

    struct GroundedDriverSelectedPlaneTailChooserProbeTrace
    {
        uint32_t OutsideCollisionRadius = 0u;
        uint32_t AlignmentAccepted = 0u;
        int32_t ProbeBudgetMilliseconds = 0;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailChooserProbeKind::RejectHorizontal);
        G3D::Vector3 InputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ProbePosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ProbeDelta = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float ChooserInputScalar = 0.0f;
        float CollisionRadius = 0.0f;
        float ProbeDistance2D = 0.0f;
        float AlignmentDot = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailChooserContractKind : uint32_t
    {
        Return1Horizontal = 1u,
        Return2SelectedPlane = 2u,
    };

    struct GroundedDriverSelectedPlaneTailChooserContractTrace
    {
        uint32_t FinalSelectedIndex = 0u;
        uint32_t FinalSelectedCount = 0u;
        uint32_t FinalSelectedIndexInRange = 0u;
        uint32_t FinalSelectedPlaneWalkable = 0u;
        uint32_t Called635F80 = 0u;
        uint32_t ChooserAcceptedSelectedPlane = 0u;
        uint32_t GroundedWall04000000Set = 0u;
        uint32_t WroteField80FromSelectedZ = 0u;
        uint32_t ProjectedMoveMutatedByChooser = 0u;
        uint32_t DispatchKind =
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailChooserContractKind::Return2SelectedPlane);
        G3D::Vector3 ChooserInputPackedPairVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ChooserInputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 ChooserOutputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float ChooserInputScalar = 0.0f;
        float FinalSelectedNormalZ = 0.0f;
    };

    enum class GroundedDriverSelectedPlaneTailReturnDispatchKind : uint32_t
    {
        Return0Exit = 0u,
        Return1Horizontal = 1u,
        Return2SelectedPlane = 2u,
    };

    struct GroundedDriverSelectedPlaneTailReturnDispatchTrace
    {
        GroundedDriverSelectedPlanePostFastReturnTailTrace PostFastReturnTailTrace{};
        uint32_t CalledTailRerank = 0u;
        uint32_t TailRerankSucceeded = 0u;
        uint32_t FinalSelectedIndex = 0u;
        uint32_t FinalSelectedCount = 0u;
        uint32_t FinalSelectedIndexInRange = 0u;
        uint32_t FinalSelectedPlaneWalkable = 0u;
        uint32_t Called635F80 = 0u;
        uint32_t ChooserAcceptedSelectedPlane = 0u;
        uint32_t GroundedWall04000000Set = 0u;
        uint32_t WroteField80FromSelectedZ = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailReturnDispatchKind::Return0Exit);
        G3D::Vector3 OutputProjectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float OutputResolved2D = 0.0f;
        float OutputPositionZ = 0.0f;
        float FinalSelectedNormalZ = 0.0f;
    };

    enum class GroundedDriverSelectedPairCommitTailKind : uint32_t
    {
        StartFallZero = 0u,
        ForwardPair = 1u,
        DeferredHoverRerank = 2u,
    };

    struct GroundedDriverSelectedPairCommitTailTrace
    {
        uint32_t SelectedIndex = 0u;
        uint32_t SelectedCount = 0u;
        uint32_t ConsumedSelectedState = 0u;
        uint32_t SnapshotBeforeCommitState = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitTailKind::StartFallZero);
        uint32_t UsedStartFallZero = 0u;
        uint32_t UsedCacheSnapshot = 0u;
        uint32_t ForwardedPair = 0u;
        uint32_t DeferredHoverRerank = 0u;
        uint32_t InputMovementFlags = 0u;
        uint32_t OutputMovementFlags = 0u;
        SelectorPair CachedPair{};
        SelectorPair OutputPair{};
        G3D::Vector3 CurrentPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 InputCachedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 OutputCachedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float CurrentFacing = 0.0f;
        float InputCachedFacing = 0.0f;
        float OutputCachedFacing = 0.0f;
        float CurrentPitch = 0.0f;
        float InputCachedPitch = 0.0f;
        float OutputCachedPitch = 0.0f;
        uint32_t InputMoveTimestamp = 0u;
        uint32_t OutputMoveTimestamp = 0u;
        uint32_t InputFallTime = 0u;
        uint32_t OutputFallTime = 0u;
        float InputFallStartZ = 0.0f;
        float OutputFallStartZ = 0.0f;
        float InputVerticalSpeed = 0.0f;
        float OutputVerticalSpeed = 0.0f;
    };

    enum class GroundedDriverSelectedPairCommitGuardKind : uint32_t
    {
        RejectProbeHit = 0u,
        RejectContextMismatch = 1u,
        RejectAttachedBit = 2u,
        CallOpaqueConsumer = 3u,
    };

    struct GroundedDriverSelectedPairCommitGuardTrace
    {
        uint32_t ZeroIncomingPair = 0u;
        uint32_t StoredPairNonZero = 0u;
        uint32_t ProbeRejectChecked = 0u;
        uint32_t ProbeRejected = 0u;
        uint32_t ContextMatchesGlobal = 0u;
        uint32_t HasAttachedPointer = 0u;
        uint32_t AttachedBit4Set = 0u;
        uint32_t CalledOpaqueConsumer = 0u;
        uint32_t GuardKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitGuardKind::RejectProbeHit);
        int32_t ReturnValue = 0;
        SelectorPair IncomingPair{};
        SelectorPair StoredPair{};
    };

    enum class GroundedDriverSelectedPairCommitBodyKind : uint32_t
    {
        RejectUnchangedPair = 0u,
        RejectIncomingPairValidator = 1u,
        CommitPair = 2u,
    };

    struct GroundedDriverSelectedPairCommitBodyTrace
    {
        uint32_t IncomingPairNonZero = 0u;
        uint32_t StoredPairNonZero = 0u;
        uint32_t IncomingPairMatchesStoredPair = 0u;
        uint32_t CalledIncomingPairValidator = 0u;
        uint32_t IncomingPairValidatorAccepted = 0u;
        uint32_t InitializedStoredTransformIdentity = 0u;
        uint32_t InitializedIncomingTransformIdentity = 0u;
        uint32_t ProcessedStoredPair = 0u;
        uint32_t ProcessedIncomingPair = 0u;
        uint32_t CalledStoredAttachmentBridge = 0u;
        uint32_t CalledIncomingAttachmentBridge = 0u;
        uint32_t AppliedStoredTransformScalar = 0u;
        uint32_t AppliedStoredTransformMatrix = 0u;
        uint32_t AppliedStoredTransformFinalize = 0u;
        uint32_t AppliedIncomingTransformScalar = 0u;
        uint32_t AppliedIncomingTransformMatrix = 0u;
        uint32_t AppliedIncomingTransformFinalize = 0u;
        uint32_t WroteCommittedPair = 0u;
        uint32_t CalledCommitNotification = 0u;
        uint32_t CommitKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitBodyKind::RejectUnchangedPair);
        int32_t ReturnValue = 0;
        float StoredPhaseScalar = 0.0f;
        float IncomingPhaseScalar = 0.0f;
        SelectorPair IncomingPair{};
        SelectorPair StoredPair{};
        SelectorPair OutputCommittedPair{};
    };

    enum class GroundedDriverHoverRerankDispatchKind : uint32_t
    {
        ReturnWithoutCommit = 0u,
        StartFallZero = 1u,
        ForwardPair = 2u,
    };

    struct GroundedDriverHoverRerankTrace
    {
        uint32_t FirstRerankSucceeded = 0u;
        uint32_t SelectedIndex = 0u;
        uint32_t SelectedCount = 0u;
        uint32_t SelectedIndexInRange = 0u;
        uint32_t UsedStandardWalkableThreshold = 0u;
        float SelectedNormalZ = 0.0f;
        float ThresholdNormalZ = 0.0f;
        uint32_t SelectedNormalAccepted = 0u;
        uint32_t LoadedSelectedPair = 0u;
        float InputWindowSpanScalar = 0.0f;
        float FollowupScalarCandidate = 0.0f;
        uint32_t UsedDirectForwardAboveOne = 0u;
        uint32_t ZeroedFollowupScalar = 0u;
        uint32_t ClampedFollowupScalar = 0u;
        float OutputFollowupScalar = 0.0f;
        uint32_t CalledSecondRerank = 0u;
        uint32_t SecondRerankSucceeded = 0u;
        uint32_t ForwardedPair = 0u;
        uint32_t StartedFallWithZeroVelocity = 0u;
        uint32_t AdvancedPositionZ = 0u;
        uint32_t DispatchKind = static_cast<uint32_t>(GroundedDriverHoverRerankDispatchKind::ReturnWithoutCommit);
        uint32_t InputMovementFlags = 0u;
        uint32_t OutputMovementFlags = 0u;
        SelectorPair OutputPair{};
        float InputPositionZ = 0.0f;
        float OutputPositionZ = 0.0f;
        uint32_t InputFallTime = 0u;
        uint32_t OutputFallTime = 0u;
        float InputFallStartZ = 0.0f;
        float OutputFallStartZ = 0.0f;
        float InputVerticalSpeed = 0.0f;
        float OutputVerticalSpeed = 0.0f;
    };

    GroundedDriverFirstDispatchTrace EvaluateGroundedDriverFirstDispatch(bool walkableSelectedContact,
                                                                         uint32_t gateReturnCode,
                                                                         float remainingDistanceBeforeDispatch,
                                                                         float sweepDistanceBeforeVertical,
                                                                         float sweepDistanceAfterVertical);

    GroundedDriverSelectedContactDispatchTrace EvaluateGroundedDriverSelectedContactDispatch(bool checkWalkableAccepted,
                                                                                            bool consumedSelectedState,
                                                                                            uint32_t movementFlags,
                                                                                            float remainingDistanceBeforeDispatch,
                                                                                            float sweepDistanceBeforeVertical,
                                                                                            float sweepDistanceAfterVertical,
                                                                                            uint32_t inputFallTime,
                                                                                            float inputFallStartZ,
                                                                                            float inputVerticalSpeed,
                                                                                            float positionZ);

    GroundedDriverResweepBookkeepingTrace EvaluateGroundedDriverResweepBookkeeping(const G3D::Vector3& direction,
                                                                                   float sweepScalar,
                                                                                   const G3D::Vector3& correction,
                                                                                   float horizontalBudgetBefore);

    GroundedDriverVerticalCapTrace EvaluateGroundedDriverVerticalCap(uint32_t movementFlags,
                                                                     float combinedMoveZ,
                                                                     float nextSweepDistance,
                                                                     float currentZ,
                                                                     float boundingRadius,
                                                                     float capField80);

    GroundedDriverSelectedPlaneCorrectionTrace EvaluateGroundedDriverSelectedPlaneCorrection(const G3D::Vector3& requestedMove,
                                                                                             const G3D::Vector3& selectedPlaneNormal,
                                                                                             float verticalLimit,
                                                                                             float remainingDistanceBefore,
                                                                                             float sweepDistanceBefore,
                                                                                             float sweepDistanceAfter,
                                                                                             float inputSweepFraction,
                                                                                             float inputDistancePointer);
    GroundedDriverSelectedPlaneCorrectionTransactionTrace EvaluateGroundedDriverSelectedPlaneCorrectionTransaction(
        bool useSelectedPlaneOverride,
        float selectedContactNormalZ,
        const G3D::Vector3& selectedPlaneNormal,
        const G3D::Vector3& inputWorkingVector,
        const G3D::Vector3& inputMoveDirection,
        float inputDistancePointer,
        uint32_t movementFlags,
        float boundingRadius,
        float remainingDistanceBefore,
        float inputSweepFraction);

    GroundedDriverSelectedPlaneDistancePointerTrace EvaluateGroundedDriverSelectedPlaneDistancePointerMutation(
        bool useSelectedPlaneOverride,
        float selectedContactNormalZ,
        const G3D::Vector3& selectedPlaneNormal,
        const G3D::Vector3& inputWorkingVector,
        const G3D::Vector3& inputMoveDirection,
        float inputDistancePointer,
        uint32_t movementFlags,
        float boundingRadius);

    GroundedDriverHorizontalCorrectionTrace EvaluateGroundedDriverHorizontalCorrection(
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& selectedPlaneNormal);

    GroundedDriverSelectedPlaneRetryTrace EvaluateGroundedDriverSelectedPlaneRetryTransaction(
        bool walkableSelectedContact,
        uint32_t gateReturnCode,
        bool useSelectedPlaneOverride,
        float selectedContactNormalZ,
        const G3D::Vector3& selectedPlaneNormal,
        const G3D::Vector3& inputWorkingVector,
        const G3D::Vector3& inputMoveDirection,
        float inputDistancePointer,
        uint32_t movementFlags,
        float boundingRadius,
        float remainingDistanceBefore,
        float inputSweepFraction);

    GroundedDriverSelectedPlaneFirstPassSetupTrace EvaluateGroundedDriverSelectedPlaneFirstPassSetup(
        const G3D::Vector3& inputPackedPairVector,
        float fieldB0,
        float boundingRadius,
        const G3D::Vector3& inputContactNormal,
        bool firstPassRerankSucceeded);

    GroundedDriverSelectedPlaneBranchGateTrace EvaluateGroundedDriverSelectedPlaneBranchGate(bool walkableSelectedContact,
                                                                                              uint32_t gateReturnCode,
                                                                                              const G3D::Vector3& requestedMove,
                                                                                              const G3D::Vector3& selectedPlaneNormal,
                                                                                              float verticalLimit,
                                                                                              float remainingDistanceBefore,
                                                                                              float sweepDistanceBefore,
                                                                                              float sweepDistanceAfter,
                                                                                              float inputSweepFraction,
                                                                                              float inputDistancePointer);

    GroundedDriverSelectedPlaneFollowupRerankTrace EvaluateGroundedDriverSelectedPlaneFollowupRerank(
        uint32_t selectedIndex,
        uint32_t selectedCount,
        const G3D::Vector3& inputContactNormal,
        const G3D::Vector3& selectedRecordNormal,
        const G3D::Vector3& inputPackedPairVector,
        const G3D::Vector3& rerankedPackedPairVector,
        bool secondRerankSucceeded);

    GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace EvaluateGroundedDriverSelectedPlaneTailPreThirdPassSetup(
        const G3D::Vector3& inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        float tailTransformScalar,
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& selectedPlaneNormal,
        float selectedContactNormalZ,
        uint32_t movementFlags,
        float boundingRadius,
        bool invokeVerticalCorrection);

    GroundedDriverSelectedPlaneTailProjectedBlendTrace EvaluateGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
        const G3D::Vector3& inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        float tailTransformScalar,
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& selectedPlaneNormal,
        float selectedContactNormalZ,
        uint32_t movementFlags,
        float boundingRadius,
        bool invokeVerticalCorrection,
        bool thirdPassRerankSucceeded,
        float verticalLimit);

    GroundedDriverSelectedPlaneThirdPassSetupTrace EvaluateGroundedDriverSelectedPlaneThirdPassSetup(
        const G3D::Vector3& inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        bool thirdPassRerankSucceeded);

    GroundedDriverSelectedPlaneBlendCorrectionTrace EvaluateGroundedDriverSelectedPlaneBlendCorrection(
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& selectedPlaneNormal,
        float verticalLimit,
        const G3D::Vector3& horizontalProjectedMove,
        float horizontalResolved2D);

    GroundedDriverSelectedPlanePostFastReturnTailTrace EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(
        const G3D::Vector3& inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        bool thirdPassRerankSucceeded,
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& selectedPlaneNormal,
        float verticalLimit,
        const G3D::Vector3& horizontalProjectedMove,
        float horizontalResolved2D);

    GroundedDriverSelectedPlaneTailWritebackTrace EvaluateGroundedDriverSelectedPlaneTailWriteback(
        const G3D::Vector3& inputPosition,
        const G3D::Vector3& inputPackedPairVector,
        float followupScalar,
        float scalarFloor,
        float inputSelectedContactNormalZ,
        bool checkWalkableAccepted,
        bool projectedTailRerankSucceeded,
        const G3D::Vector3& projectedTailMove,
        float projectedTailResolved2D);

    GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace CaptureGroundedDriverSelectedPlaneTailProbeStateSnapshot(
        const G3D::Vector3& field44Vector,
        const G3D::Vector3& field50Vector,
        uint32_t field78,
        const G3D::Vector3& field5cVector,
        float field68,
        float field6c,
        uint32_t field40Flags,
        float field84);

    GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace RestoreGroundedDriverSelectedPlaneTailProbeStateSnapshot(
        const GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace& snapshot);

    GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace EvaluateGroundedDriverSelectedPlaneTailElapsedMilliseconds(
        float elapsedSeconds);

    GroundedDriverSelectedPlaneTailEntrySetupTrace EvaluateGroundedDriverSelectedPlaneTailEntrySetup(
        uint32_t inputWindowMilliseconds,
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& currentPosition,
        const G3D::Vector3& field44Vector,
        const G3D::Vector3& field50Vector,
        uint32_t field78,
        const G3D::Vector3& field5cVector,
        float field68,
        float field6c,
        uint32_t field40Flags,
        float field84);

    GroundedDriverSelectedPlaneTailPostForwardingTrace EvaluateGroundedDriverSelectedPlaneTailPostForwarding(
        int32_t pairForwardReturnCode,
        bool directStateBit,
        bool alternateUnitZStateBit,
        float windowSpanScalar,
        float windowStartScalar,
        const G3D::Vector3& moveVector,
        float horizontalReferenceMagnitude,
        uint32_t movementFlags,
        float verticalSpeed,
        float horizontalSpeedScale,
        float referenceZ,
        float positionZ,
        const G3D::Vector3& startPosition,
        float elapsedScalar,
        float facing,
        float pitch,
        const G3D::Vector3& cachedPosition,
        float cachedFacing,
        float cachedPitch,
        uint32_t cachedMoveTimestamp,
        float cachedScalar84,
        float recomputedScalar84);

    GroundedDriverSelectedPlaneTailReturn2LateBranchTrace EvaluateGroundedDriverSelectedPlaneTailReturn2LateBranch(
        float consumedWindowSeconds,
        int32_t field58,
        int32_t field78);

    GroundedDriverSelectedPlaneTailLateNotifierTrace EvaluateGroundedDriverSelectedPlaneTailLateNotifier(
        int32_t roundedMilliseconds,
        bool alternateUnitZStateBit,
        int32_t field78,
        bool notifyRequested,
        int32_t alternateWindowCommitBase,
        bool sidecarStatePresent,
        bool bit20InitiallySet,
        bool commitGuardPassed,
        bool bit20StillSet,
        float fieldA0,
        uint32_t lowNibbleFlags,
        const GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace& snapshot,
        bool rerouteLoopUsed);

    GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace EvaluateGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
        uint32_t attemptIndex,
        const G3D::Vector3& normalizedInputDirection,
        float remainingMagnitude,
        const G3D::Vector3& lateralOffset,
        const G3D::Vector3& originalPosition,
        const G3D::Vector3& currentPosition,
        float originalHorizontalMagnitude,
        float originalVerticalMagnitude,
        float previousField68,
        float previousField6c,
        float previousField84);

    GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace EvaluateGroundedDriverSelectedPlaneTailProbeVerticalFallback(
        const G3D::Vector3& normalizedInputDirection,
        float currentHorizontalMagnitude,
        float remainingMagnitude,
        bool verticalFallbackAlreadyUsed);

    GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace EvaluateGroundedDriverSelectedPlaneTailRerouteLoopController(
        uint32_t attemptIndex,
        bool incrementAttemptBeforeProbe,
        const G3D::Vector3& normalizedInputDirection,
        float remainingMagnitude,
        float currentHorizontalMagnitude,
        const G3D::Vector3& lateralOffset,
        const G3D::Vector3& originalPosition,
        const G3D::Vector3& currentPosition,
        float originalHorizontalMagnitude,
        float originalVerticalMagnitude,
        float previousField68,
        float previousField6c,
        float previousField84,
        bool verticalFallbackAlreadyUsed);

    GroundedDriverSelectedPlaneTailChooserProbeTrace EvaluateGroundedDriverSelectedPlaneTailChooserProbe(
        const G3D::Vector3& inputPackedPairVector,
        const G3D::Vector3& inputProjectedMove,
        float chooserInputScalar,
        const G3D::Vector3& probePosition,
        float collisionRadius);

    GroundedDriverSelectedPlaneTailChooserContractTrace EvaluateGroundedDriverSelectedPlaneTailChooserContract(
        const G3D::Vector3& chooserInputPackedPairVector,
        const G3D::Vector3& chooserInputProjectedMove,
        float chooserInputScalar,
        uint32_t finalSelectedIndex,
        uint32_t finalSelectedCount,
        float finalSelectedNormalZ,
        bool chooserAcceptedSelectedPlane,
        uint32_t movementFlags,
        const G3D::Vector3& chooserOutputProjectedMove);

    GroundedDriverSelectedPlaneTailReturnDispatchTrace EvaluateGroundedDriverSelectedPlaneTailReturnDispatch(
        const G3D::Vector3& inputPackedPairVector,
        float inputPositionZ,
        float followupScalar,
        float scalarFloor,
        bool thirdPassRerankSucceeded,
        const G3D::Vector3& requestedMove,
        const G3D::Vector3& selectedPlaneNormal,
        float verticalLimit,
        const G3D::Vector3& horizontalProjectedMove,
        float horizontalResolved2D,
        bool tailRerankSucceeded,
        uint32_t finalSelectedIndex,
        uint32_t finalSelectedCount,
        float finalSelectedNormalZ,
        bool chooserAcceptedSelectedPlane,
        uint32_t movementFlags);

    GroundedDriverSelectedPairCommitTailTrace EvaluateGroundedDriverSelectedPairCommitTail(uint32_t selectedIndex,
                                                                                            uint32_t selectedCount,
                                                                                            bool consumedSelectedState,
                                                                                            bool snapshotBeforeCommitState,
                                                                                           uint32_t movementFlags,
                                                                                           const SelectorPair& cachedPair,
                                                                                           const G3D::Vector3& currentPosition,
                                                                                           float currentFacing,
                                                                                           float currentPitch,
                                                                                           const G3D::Vector3& cachedPosition,
                                                                                           float cachedFacing,
                                                                                           float cachedPitch,
                                                                                           uint32_t cachedMoveTimestamp,
                                                                                           uint32_t inputFallTime,
                                                                                           float inputFallStartZ,
                                                                                           float inputVerticalSpeed);

    GroundedDriverSelectedPairCommitGuardTrace EvaluateGroundedDriverSelectedPairCommitGuard(const SelectorPair& incomingPair,
                                                                                            const SelectorPair& storedPair,
                                                                                            bool probeRejectOnStoredPairUnload,
                                                                                            bool contextMatchesGlobal,
                                                                                            bool hasAttachedPointer,
                                                                                            bool attachedBit4Set,
                                                                                            int32_t opaqueConsumerReturnValue);

    GroundedDriverSelectedPairCommitBodyTrace EvaluateGroundedDriverSelectedPairCommitBody(const SelectorPair& incomingPair,
                                                                                           const SelectorPair& storedPair,
                                                                                           bool incomingPairValidatorAccepted,
                                                                                           bool hasTransformConsumer,
                                                                                           float storedPhaseScalar,
                                                                                           float incomingPhaseScalar);

    GroundedDriverHoverRerankTrace EvaluateGroundedDriverHoverRerankDispatch(bool firstRerankSucceeded,
                                                                             uint32_t selectedIndex,
                                                                             uint32_t selectedCount,
                                                                             bool useStandardWalkableThreshold,
                                                                             float selectedNormalZ,
                                                                             const SelectorPair& selectedPair,
                                                                             float inputWindowSpanScalar,
                                                                             float followupScalarCandidate,
                                                                             bool secondRerankSucceeded,
                                                                             uint32_t movementFlags,
                                                                             float positionZ,
                                                                             uint32_t inputFallTime,
                                                                             float inputFallStartZ,
                                                                             float inputVerticalSpeed);
}
