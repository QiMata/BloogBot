#include "PhysicsEngine.h"
#include "GroundedDriverParity.h"

#include <limits>

// Write scope for grounded-driver bookkeeping parity work.
// New implementations for this cluster should live here instead of PhysicsEngine.cpp.

namespace
{
    constexpr float kWoWPlaneBuildEpsilon = 9.54e-7f;
    constexpr float kWoWHorizontalPushout = 0.001f;
    constexpr float kWoWWalkableMinNormalZ = 0.6427876353263855f;
    constexpr float kWoWRelaxedHoverNormalZThreshold = 0.1736481785774231f;
    constexpr float kWoWResolvedMoveCompareEpsilon = 1.0e-4f;
    constexpr float kWoWProbeRerouteDistanceEpsilon = 0.0005f;
    constexpr float kWoWChooserProbeScalarBias = 0.1f;
    constexpr float kWoWChooserProbeMsScale = 1000.0f;
    constexpr float kWoWMillisecondsToSecondsScale = 1.0f / kWoWChooserProbeMsScale;
    constexpr float kWoWProbeVerticalAbortScale = 1.1866661310195923f;
    constexpr float kWoWChooserAlignmentDotMin = 0.9848077297210693f;

    int32_t RoundHalfAwayFromZero(const float value)
    {
        if (value >= 0.0f) {
            return static_cast<int32_t>(std::floor(value + 0.5f));
        }

        return static_cast<int32_t>(std::ceil(value - 0.5f));
    }
}

WoWCollision::GroundedDriverFirstDispatchTrace WoWCollision::EvaluateGroundedDriverFirstDispatch(
    bool walkableSelectedContact,
    uint32_t gateReturnCode,
    float remainingDistanceBeforeDispatch,
    float sweepDistanceBeforeVertical,
    float sweepDistanceAfterVertical)
{
    GroundedDriverFirstDispatchTrace trace{};
    trace.WalkableSelectedContact = walkableSelectedContact ? 1u : 0u;
    trace.GateReturnCode = gateReturnCode;
    trace.RemainingDistanceBeforeDispatch = remainingDistanceBeforeDispatch;
    trace.RemainingDistanceAfterDispatch = remainingDistanceBeforeDispatch;
    trace.SweepDistanceBeforeVertical = sweepDistanceBeforeVertical;
    trace.SweepDistanceAfterVertical = sweepDistanceAfterVertical;

    auto applyVerticalPath = [&](GroundedDriverFirstDispatchKind dispatchKind, bool setGroundedWallFlag) {
        trace.DispatchKind = static_cast<uint32_t>(dispatchKind);
        trace.SetGroundedWall04000000 = setGroundedWallFlag ? 1u : 0u;
        trace.UsesVerticalHelper = 1u;
        trace.RemainingDistanceRescaled = 1u;
        trace.RemainingDistanceAfterDispatch =
            remainingDistanceBeforeDispatch * (sweepDistanceAfterVertical / sweepDistanceBeforeVertical);
    };

    if (walkableSelectedContact) {
        applyVerticalPath(GroundedDriverFirstDispatchKind::WalkableSelectedVertical, false);
        return trace;
    }

    if (gateReturnCode == 0u) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverFirstDispatchKind::Exit);
        return trace;
    }

    if (gateReturnCode == 2u) {
        applyVerticalPath(GroundedDriverFirstDispatchKind::NonWalkableVertical, true);
        return trace;
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverFirstDispatchKind::Horizontal);
    trace.UsesHorizontalHelper = 1u;
    return trace;
}

WoWCollision::GroundedDriverSelectedContactDispatchTrace WoWCollision::EvaluateGroundedDriverSelectedContactDispatch(
    bool checkWalkableAccepted,
    bool consumedSelectedState,
    uint32_t movementFlags,
    float remainingDistanceBeforeDispatch,
    float sweepDistanceBeforeVertical,
    float sweepDistanceAfterVertical,
    uint32_t inputFallTime,
    float inputFallStartZ,
    float inputVerticalSpeed,
    float positionZ)
{
    GroundedDriverSelectedContactDispatchTrace trace{};
    trace.CheckWalkableAccepted = checkWalkableAccepted ? 1u : 0u;
    trace.ConsumedSelectedState = consumedSelectedState ? 1u : 0u;
    trace.InputMovementFlags = movementFlags;
    trace.OutputMovementFlags = movementFlags;
    trace.RemainingDistanceBeforeDispatch = remainingDistanceBeforeDispatch;
    trace.RemainingDistanceAfterDispatch = remainingDistanceBeforeDispatch;
    trace.SweepDistanceBeforeVertical = sweepDistanceBeforeVertical;
    trace.SweepDistanceAfterVertical = sweepDistanceAfterVertical;
    trace.InputFallTime = inputFallTime;
    trace.OutputFallTime = inputFallTime;
    trace.InputFallStartZ = inputFallStartZ;
    trace.OutputFallStartZ = inputFallStartZ;
    trace.InputVerticalSpeed = inputVerticalSpeed;
    trace.OutputVerticalSpeed = inputVerticalSpeed;
    trace.PositionZ = positionZ;

    if (checkWalkableAccepted) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedContactDispatchKind::WalkableSelectedVertical);
        trace.BypassedNonWalkableDispatch = 1u;
        trace.RemainingDistanceAfterDispatch =
            remainingDistanceBeforeDispatch * (sweepDistanceAfterVertical / sweepDistanceBeforeVertical);
        return trace;
    }

    if (!consumedSelectedState) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedContactDispatchKind::DelegateToNonWalkableDispatch);
        trace.DelegatedToNonWalkableDispatch = 1u;
        return trace;
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedContactDispatchKind::StartFallZero);
    trace.StartedFallWithZeroVelocity = 1u;
    trace.ClearedSplineElevation04000000 = 1u;
    trace.ClearedSwimming00200000 = 1u;
    trace.SetJumping = 1u;
    trace.ResetFallTime = 1u;
    trace.ResetFallStartZ = 1u;
    trace.ResetVerticalSpeed = 1u;
    trace.DroppedChosenPair = 1u;
    trace.OutputMovementFlags = (movementFlags & ~(MOVEFLAG_SPLINE_ELEVATION | MOVEFLAG_SWIMMING)) | MOVEFLAG_JUMPING;
    trace.OutputFallTime = 0u;
    trace.OutputFallStartZ = positionZ;
    trace.OutputVerticalSpeed = 0.0f;
    return trace;
}

WoWCollision::GroundedDriverResweepBookkeepingTrace WoWCollision::EvaluateGroundedDriverResweepBookkeeping(
    const G3D::Vector3& direction,
    float sweepScalar,
    const G3D::Vector3& correction,
    float horizontalBudgetBefore)
{
    GroundedDriverResweepBookkeepingTrace trace{};
    trace.InputDirection = direction;
    trace.InputSweepScalar = sweepScalar;
    trace.InputCorrection = correction;
    trace.InputHorizontalBudget = horizontalBudgetBefore;
    trace.OutputHorizontalBudget = horizontalBudgetBefore;

    const G3D::Vector3 combinedMove = correction + (direction * sweepScalar);
    trace.OutputCombinedMove = combinedMove;
    trace.OutputDirection = combinedMove;
    trace.OutputSweepDistance = combinedMove.magnitude();

    if (trace.OutputSweepDistance <= PhysicsConstants::MS_TO_SEC) {
        trace.FinalizeFlag = 1u;
        trace.TinyMagnitudeFinalize = 1u;
        return trace;
    }

    trace.OutputDirection = combinedMove / trace.OutputSweepDistance;
    trace.NormalizedDirection = 1u;

    const float combinedXYMagnitude = std::sqrt((combinedMove.x * combinedMove.x) + (combinedMove.y * combinedMove.y));
    trace.WroteHorizontalPair = 1u;
    trace.OutputHorizontalX = combinedMove.x;
    trace.OutputHorizontalY = combinedMove.y;
    trace.OutputCombinedXYMagnitude = combinedXYMagnitude;

    if (combinedXYMagnitude > PhysicsConstants::SPEED_EPSILON) {
        trace.NormalizedHorizontalPair = 1u;
        trace.OutputHorizontalX /= combinedXYMagnitude;
        trace.OutputHorizontalY /= combinedXYMagnitude;
    }

    trace.OutputHorizontalBudget = horizontalBudgetBefore - combinedXYMagnitude;
    if (trace.OutputHorizontalBudget <= kWoWPlaneBuildEpsilon) {
        trace.FinalizeFlag = 1u;
        trace.HorizontalBudgetFinalize = 1u;
    }

    return trace;
}

WoWCollision::GroundedDriverVerticalCapTrace WoWCollision::EvaluateGroundedDriverVerticalCap(
    uint32_t movementFlags,
    float combinedMoveZ,
    float nextSweepDistance,
    float currentZ,
    float boundingRadius,
    float capField80)
{
    GroundedDriverVerticalCapTrace trace{};
    trace.CapBitSet = (movementFlags & MOVEFLAG_SPLINE_ELEVATION) != 0u ? 1u : 0u;
    trace.PositiveCombinedMoveZ = combinedMoveZ > kWoWPlaneBuildEpsilon ? 1u : 0u;
    trace.CombinedMoveZ = combinedMoveZ;
    trace.InputSweepDistance = nextSweepDistance;
    trace.OutputSweepDistance = nextSweepDistance;
    trace.CurrentZ = currentZ;
    trace.BoundingRadius = boundingRadius;
    trace.CapField80 = capField80;
    trace.CapAbsoluteZ = boundingRadius + capField80;
    trace.PredictedZ = currentZ + combinedMoveZ;
    trace.AllowedDeltaZ = trace.CapAbsoluteZ - currentZ;

    if (trace.CapBitSet == 0u || trace.PositiveCombinedMoveZ == 0u || trace.CapAbsoluteZ >= trace.PredictedZ) {
        return trace;
    }

    trace.AppliedCap = 1u;
    trace.OutputSweepDistance = nextSweepDistance * (trace.AllowedDeltaZ / combinedMoveZ);

    if (std::fabs(trace.AllowedDeltaZ - combinedMoveZ) >= kWoWPlaneBuildEpsilon) {
        trace.SetFinalizeFlag20 = 1u;
    }

    if (trace.OutputSweepDistance < PhysicsConstants::MS_TO_SEC) {
        trace.SetTinySweepFlag30 = 1u;
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneCorrectionTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneCorrection(
    const G3D::Vector3& requestedMove,
    const G3D::Vector3& selectedPlaneNormal,
    float verticalLimit,
    float remainingDistanceBefore,
    float sweepDistanceBefore,
    float sweepDistanceAfter,
    float inputSweepFraction,
    float inputDistancePointer)
{
    GroundedDriverSelectedPlaneCorrectionTrace trace{};
    trace.RequestedMove = requestedMove;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.VerticalLimit = std::fabs(verticalLimit);
    trace.InputRemainingDistance = remainingDistanceBefore;
    trace.OutputRemainingDistance = remainingDistanceBefore;
    trace.InputSweepDistance = sweepDistanceBefore;
    trace.OutputSweepDistance = sweepDistanceBefore;
    trace.InputSweepFraction = inputSweepFraction;
    trace.OutputSweepFraction = inputSweepFraction;
    trace.InputDistancePointer = inputDistancePointer;
    trace.OutputDistancePointer = inputDistancePointer;

    const float intoPlane = requestedMove.dot(selectedPlaneNormal);
    trace.IntoPlane = intoPlane;

    G3D::Vector3 projectedMove = requestedMove - (selectedPlaneNormal * intoPlane);
    if (trace.VerticalLimit > PhysicsConstants::VECTOR_EPSILON &&
        std::fabs(projectedMove.z) > trace.VerticalLimit) {
        projectedMove.z = projectedMove.z > 0.0f ? trace.VerticalLimit : -trace.VerticalLimit;
        trace.ClampedVerticalMagnitude = 1u;
    }

    trace.ProjectedCorrection = projectedMove;
    trace.ProjectedVertical = projectedMove.z;
    trace.OutputCorrection = G3D::Vector3(0.0f, 0.0f, projectedMove.z);
    trace.WroteVerticalOnlyCorrection = 1u;

    if (sweepDistanceBefore > PhysicsConstants::VECTOR_EPSILON) {
        const float rescale = sweepDistanceAfter / sweepDistanceBefore;
        trace.OutputRemainingDistance = remainingDistanceBefore * rescale;
        trace.OutputSweepDistance = sweepDistanceAfter;
        trace.OutputSweepFraction = inputSweepFraction * rescale;
        trace.OutputDistancePointer = inputDistancePointer * rescale;
        trace.MutatedDistancePointer = 1u;
        trace.RescaledRemainingDistance = 1u;
        trace.RescaledSweepFraction = 1u;
    }

    trace.CorrectionKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneCorrectionKind::VerticalOnly);
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneCorrectionTransactionTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneCorrectionTransaction(
    bool useSelectedPlaneOverride,
    float selectedContactNormalZ,
    const G3D::Vector3& selectedPlaneNormal,
    const G3D::Vector3& inputWorkingVector,
    const G3D::Vector3& inputMoveDirection,
    float inputDistancePointer,
    uint32_t movementFlags,
    float boundingRadius,
    float remainingDistanceBefore,
    float inputSweepFraction)
{
    GroundedDriverSelectedPlaneCorrectionTransactionTrace trace{};
    trace.InputMovementFlags = movementFlags;
    trace.SelectedContactNormalZ = selectedContactNormalZ;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.InputWorkingVector = inputWorkingVector;
    trace.InputMoveDirection = inputMoveDirection;
    trace.InputDistancePointer = inputDistancePointer;
    trace.OutputDistancePointer = inputDistancePointer;
    trace.BoundingRadius = boundingRadius;
    trace.InputRemainingDistance = remainingDistanceBefore;
    trace.OutputRemainingDistance = remainingDistanceBefore;
    trace.InputSweepFraction = inputSweepFraction;
    trace.OutputSweepFraction = inputSweepFraction;

    trace.DistancePointerTrace = EvaluateGroundedDriverSelectedPlaneDistancePointerMutation(
        useSelectedPlaneOverride,
        selectedContactNormalZ,
        selectedPlaneNormal,
        inputWorkingVector,
        inputMoveDirection,
        inputDistancePointer,
        movementFlags,
        boundingRadius);
    trace.WroteVerticalOnlyCorrection = 1u;
    trace.MutatedDistancePointer = trace.DistancePointerTrace.MutatedDistancePointer;
    trace.CorrectionKind = trace.DistancePointerTrace.OutputKind;
    trace.OutputCorrection = trace.DistancePointerTrace.OutputCorrection;
    trace.OutputDistancePointer = trace.DistancePointerTrace.OutputDistancePointer;

    if (trace.DistancePointerTrace.MutatedDistancePointer != 0u &&
        std::fabs(inputDistancePointer) > PhysicsConstants::SPEED_EPSILON) {
        const float rescale = trace.OutputDistancePointer / inputDistancePointer;
        trace.OutputRemainingDistance = remainingDistanceBefore * rescale;
        trace.OutputSweepFraction = inputSweepFraction * rescale;
        trace.RescaledRemainingDistance = 1u;
        trace.RescaledSweepFraction = 1u;
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneDistancePointerTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneDistancePointerMutation(
    bool useSelectedPlaneOverride,
    float selectedContactNormalZ,
    const G3D::Vector3& selectedPlaneNormal,
    const G3D::Vector3& inputWorkingVector,
    const G3D::Vector3& inputMoveDirection,
    float inputDistancePointer,
    uint32_t movementFlags,
    float boundingRadius)
{
    GroundedDriverSelectedPlaneDistancePointerTrace trace{};
    trace.UseSelectedPlaneOverride = useSelectedPlaneOverride ? 1u : 0u;
    trace.GroundedWall04000000Set = (movementFlags & MOVEFLAG_SPLINE_ELEVATION) != 0u ? 1u : 0u;
    trace.SelectedContactNormalZ = selectedContactNormalZ;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.InputWorkingVector = inputWorkingVector;
    trace.InputMoveDirection = inputMoveDirection;
    trace.InputDistancePointer = inputDistancePointer;
    trace.OutputDistancePointer = inputDistancePointer;
    trace.BoundingRadius = boundingRadius;

    trace.EffectiveWorkingVector = inputWorkingVector;
    trace.SelectedPlaneMagnitudeSquared = selectedPlaneNormal.squaredMagnitude();
    trace.SelectedContactNormalWithinOverrideBand =
        selectedContactNormalZ <= kWoWWalkableMinNormalZ ? 1u : 0u;
    if (useSelectedPlaneOverride &&
        trace.SelectedContactNormalWithinOverrideBand != 0u &&
        std::fabs(trace.SelectedPlaneMagnitudeSquared) > PhysicsConstants::SPEED_EPSILON) {
        trace.EffectiveWorkingVector = -selectedPlaneNormal;
        trace.UsedSelectedPlaneNormalOverride = 1u;
    }

    const float numerator = (-trace.EffectiveWorkingVector).dot(inputMoveDirection) * inputDistancePointer;
    trace.DotScaledDistance = numerator;

    const float denominator = trace.EffectiveWorkingVector.z;
    if (std::fabs(denominator) >= PhysicsConstants::SPEED_EPSILON) {
        trace.RawScalar = numerator / denominator;
    }
    else {
        trace.UsedInfiniteScalar = 1u;
        trace.RawScalar = numerator < 0.0f
            ? -std::numeric_limits<float>::max()
            : std::numeric_limits<float>::max();
    }

    trace.OutputScalar = trace.RawScalar;
    if (trace.GroundedWall04000000Set != 0u && trace.RawScalar < 0.0f) {
        trace.ZeroedDistancePointer = 1u;
        trace.MutatedDistancePointer = 1u;
        trace.OutputDistancePointer = 0.0f;
        trace.OutputScalar = boundingRadius;
        trace.OutputKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneDistancePointerKind::FlaggedNegativeZeroDistance);
    }
    else if (trace.RawScalar > boundingRadius) {
        trace.MutatedDistancePointer = 1u;
        trace.OutputDistancePointer = inputDistancePointer * (boundingRadius / trace.RawScalar);
        trace.OutputScalar = boundingRadius;
        trace.OutputKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneDistancePointerKind::PositiveRadiusClamp);
    }
    else if (trace.RawScalar < -boundingRadius) {
        trace.MutatedDistancePointer = 1u;
        trace.OutputDistancePointer = inputDistancePointer * ((-boundingRadius) / trace.RawScalar);
        trace.OutputScalar = -boundingRadius;
        trace.OutputKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneDistancePointerKind::NegativeRadiusClamp);
    }

    trace.OutputCorrection = G3D::Vector3(0.0f, 0.0f, trace.OutputScalar);
    return trace;
}

WoWCollision::GroundedDriverHorizontalCorrectionTrace WoWCollision::EvaluateGroundedDriverHorizontalCorrection(
    const G3D::Vector3& requestedMove,
    const G3D::Vector3& selectedPlaneNormal)
{
    GroundedDriverHorizontalCorrectionTrace trace{};
    trace.RequestedMove = requestedMove;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.HorizontalMagnitude = std::sqrt(
        (selectedPlaneNormal.x * selectedPlaneNormal.x) +
        (selectedPlaneNormal.y * selectedPlaneNormal.y));

    if (trace.HorizontalMagnitude <= kWoWPlaneBuildEpsilon) {
        trace.ZeroedOutputOnReject = 1u;
        return trace;
    }

    trace.EntryGateAccepted = 1u;
    trace.NormalizedHorizontalNormal = 1u;
    trace.InverseHorizontalMagnitude = 1.0f / trace.HorizontalMagnitude;
    trace.NormalizedHorizontalNormalVector = G3D::Vector3(
        selectedPlaneNormal.x * trace.InverseHorizontalMagnitude,
        selectedPlaneNormal.y * trace.InverseHorizontalMagnitude,
        0.0f);
    trace.IntoPlane = requestedMove.dot(selectedPlaneNormal);
    trace.OutputCorrection = requestedMove - (selectedPlaneNormal * trace.IntoPlane);
    trace.OutputCorrection.z = 0.0f;
    trace.OutputCorrection += trace.NormalizedHorizontalNormalVector * kWoWHorizontalPushout;
    trace.AppliedEpsilonPushout = 1u;
    trace.CorrectionKind = static_cast<uint32_t>(GroundedDriverHorizontalCorrectionKind::HorizontalEpsilonProjection);
    trace.OutputResolved2D = std::sqrt(
        (trace.OutputCorrection.x * trace.OutputCorrection.x) +
        (trace.OutputCorrection.y * trace.OutputCorrection.y));
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneRetryTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneRetryTransaction(
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
    float inputSweepFraction)
{
    GroundedDriverSelectedPlaneRetryTrace trace{};
    trace.WalkableSelectedContact = walkableSelectedContact ? 1u : 0u;
    trace.GateReturnCode = gateReturnCode;
    trace.InputMovementFlags = movementFlags;
    trace.OutputMovementFlags = movementFlags;
    trace.SelectedContactNormalZ = selectedContactNormalZ;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.InputWorkingVector = inputWorkingVector;
    trace.InputMoveDirection = inputMoveDirection;
    trace.BoundingRadius = boundingRadius;
    trace.InputRemainingDistance = remainingDistanceBefore;
    trace.OutputRemainingDistance = remainingDistanceBefore;
    trace.InputSweepFraction = inputSweepFraction;
    trace.OutputSweepFraction = inputSweepFraction;
    trace.InputDistancePointer = inputDistancePointer;
    trace.OutputDistancePointer = inputDistancePointer;

    auto applyVerticalBranch = [&]() {
        trace.UsesVerticalHelper = 1u;
        const GroundedDriverSelectedPlaneCorrectionTransactionTrace correctionTrace =
            EvaluateGroundedDriverSelectedPlaneCorrectionTransaction(
                useSelectedPlaneOverride,
                selectedContactNormalZ,
                selectedPlaneNormal,
                inputWorkingVector,
                inputMoveDirection,
                inputDistancePointer,
                trace.OutputMovementFlags,
                boundingRadius,
                remainingDistanceBefore,
                inputSweepFraction);
        trace.DistancePointerTrace = correctionTrace.DistancePointerTrace;
        trace.MutatedDistancePointer = correctionTrace.MutatedDistancePointer;
        trace.OutputCorrection = correctionTrace.OutputCorrection;
        trace.OutputDistancePointer = correctionTrace.OutputDistancePointer;
        trace.OutputRemainingDistance = correctionTrace.OutputRemainingDistance;
        trace.OutputSweepFraction = correctionTrace.OutputSweepFraction;
        trace.RescaledRemainingDistance = correctionTrace.RescaledRemainingDistance;
        trace.RescaledSweepFraction = correctionTrace.RescaledSweepFraction;
    };

    if (walkableSelectedContact) {
        trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::WalkableSelectedVertical);
        applyVerticalBranch();
        return trace;
    }

    if (gateReturnCode == 0u) {
        trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::ExitWithoutMutation);
        return trace;
    }

    if (gateReturnCode == 2u) {
        trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::VerticalRetry);
        trace.SetGroundedWall04000000 = 1u;
        trace.OutputMovementFlags |= MOVEFLAG_SPLINE_ELEVATION;
        applyVerticalBranch();
        return trace;
    }

    trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::HorizontalRetry);
    trace.UsesHorizontalHelper = 1u;
    trace.HorizontalCorrectionTrace = EvaluateGroundedDriverHorizontalCorrection(
        inputWorkingVector,
        selectedPlaneNormal);
    trace.OutputCorrection = trace.HorizontalCorrectionTrace.OutputCorrection;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneFirstPassSetupTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneFirstPassSetup(
    const G3D::Vector3& inputPackedPairVector,
    float fieldB0,
    float boundingRadius,
    const G3D::Vector3& inputContactNormal,
    bool firstPassRerankSucceeded)
{
    GroundedDriverSelectedPlaneFirstPassSetupTrace trace{};
    trace.SupportPlaneInitCount = 7u;
    trace.LoadedInputPackedPair = 1u;
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.FieldB0 = fieldB0;
    trace.BoundingRadius = boundingRadius;
    trace.InputContactNormal = inputContactNormal;
    trace.SkinAdjustedFieldB0 = fieldB0 + PhysicsConstants::COLLISION_SKIN_EPSILON;
    trace.BoundingRadiusTanFloor = boundingRadius * PhysicsConstants::WALKABLE_TAN_MAX_SLOPE;
    trace.OutputScalarFloor = trace.SkinAdjustedFieldB0;

    if (trace.BoundingRadiusTanFloor > trace.OutputScalarFloor) {
        trace.UsedBoundingRadiusTanFloor = 1u;
        trace.OutputScalarFloor = trace.BoundingRadiusTanFloor;
    }

    const bool inFirstPassBand =
        inputContactNormal.z >= 0.0f &&
        inputContactNormal.z <= kWoWWalkableMinNormalZ;
    trace.EnteredFirstPassNormalBand = inFirstPassBand ? 1u : 0u;
    if (!inFirstPassBand) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneFirstPassSetupKind::SkipToFollowupRerank);
        return trace;
    }

    trace.HorizontalMagnitude = std::sqrt(
        (inputContactNormal.x * inputContactNormal.x) +
        (inputContactNormal.y * inputContactNormal.y));

    if (trace.HorizontalMagnitude > 0.0f) {
        trace.InverseHorizontalMagnitude = 1.0f / trace.HorizontalMagnitude;
        trace.FirstPassWorkingVector = G3D::Vector3(
            -inputContactNormal.x * trace.InverseHorizontalMagnitude,
            -inputContactNormal.y * trace.InverseHorizontalMagnitude,
            0.0f);
    }
    else {
        trace.InverseHorizontalMagnitude = std::numeric_limits<float>::infinity();
    }

    trace.InvokedFirstPassRerank = 1u;
    trace.FirstPassRerankSucceeded = firstPassRerankSucceeded ? 1u : 0u;
    trace.DispatchKind = static_cast<uint32_t>(
        firstPassRerankSucceeded
            ? GroundedDriverSelectedPlaneFirstPassSetupKind::ContinueToFollowupRerank
            : GroundedDriverSelectedPlaneFirstPassSetupKind::FirstPassFailureExit);
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneBranchGateTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneBranchGate(
    bool walkableSelectedContact,
    uint32_t gateReturnCode,
    const G3D::Vector3& requestedMove,
    const G3D::Vector3& selectedPlaneNormal,
    float verticalLimit,
    float remainingDistanceBefore,
    float sweepDistanceBefore,
    float sweepDistanceAfter,
    float inputSweepFraction,
    float inputDistancePointer)
{
    GroundedDriverSelectedPlaneBranchGateTrace trace{};
    trace.WalkableSelectedContact = walkableSelectedContact ? 1u : 0u;
    trace.GateReturnCode = gateReturnCode;
    trace.RequestedMove = requestedMove;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.InputRemainingDistance = remainingDistanceBefore;
    trace.OutputRemainingDistance = remainingDistanceBefore;
    trace.InputSweepDistance = sweepDistanceBefore;
    trace.OutputSweepDistance = sweepDistanceBefore;
    trace.InputSweepFraction = inputSweepFraction;
    trace.OutputSweepFraction = inputSweepFraction;
    trace.InputDistancePointer = inputDistancePointer;
    trace.OutputDistancePointer = inputDistancePointer;

    const auto applyVerticalBranch = [&]() {
        const GroundedDriverSelectedPlaneCorrectionTrace correctionTrace =
            EvaluateGroundedDriverSelectedPlaneCorrection(
                requestedMove,
                selectedPlaneNormal,
                verticalLimit,
                remainingDistanceBefore,
                sweepDistanceBefore,
                sweepDistanceAfter,
                inputSweepFraction,
                inputDistancePointer);

        trace.OutputCorrection = correctionTrace.OutputCorrection;
        trace.RemainingDistanceRescaled = correctionTrace.RescaledRemainingDistance;
        trace.MutatedDistancePointer = correctionTrace.MutatedDistancePointer;
        trace.RescaledSweepFraction = correctionTrace.RescaledSweepFraction;
        trace.OutputRemainingDistance = correctionTrace.OutputRemainingDistance;
        trace.OutputSweepDistance = correctionTrace.OutputSweepDistance;
        trace.OutputSweepFraction = correctionTrace.OutputSweepFraction;
        trace.OutputDistancePointer = correctionTrace.OutputDistancePointer;
    };

    if (walkableSelectedContact) {
        trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::WalkableSelectedVertical);
        trace.UsesVerticalHelper = 1u;
        applyVerticalBranch();
        return trace;
    }

    if (gateReturnCode == 0u) {
        trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::ExitWithoutMutation);
        return trace;
    }

    if (gateReturnCode == 2u) {
        trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::VerticalRetry);
        trace.SetGroundedWall04000000 = 1u;
        trace.UsesVerticalHelper = 1u;
        applyVerticalBranch();
        return trace;
    }

    trace.BranchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBranchGateKind::HorizontalRetry);
    trace.UsesHorizontalHelper = 1u;
    trace.HorizontalCorrectionTrace = EvaluateGroundedDriverHorizontalCorrection(
        requestedMove,
        selectedPlaneNormal);
    trace.OutputCorrection = trace.HorizontalCorrectionTrace.OutputCorrection;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneFollowupRerankTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneFollowupRerank(
    uint32_t selectedIndex,
    uint32_t selectedCount,
    const G3D::Vector3& inputContactNormal,
    const G3D::Vector3& selectedRecordNormal,
    const G3D::Vector3& inputPackedPairVector,
    const G3D::Vector3& rerankedPackedPairVector,
    bool secondRerankSucceeded)
{
    GroundedDriverSelectedPlaneFollowupRerankTrace trace{};
    trace.SelectedIndex = selectedIndex;
    trace.SelectedCount = selectedCount;
    trace.InputContactNormal = inputContactNormal;
    trace.SelectedRecordNormal = selectedRecordNormal;
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.RerankedPackedPairVector = rerankedPackedPairVector;
    trace.EffectivePackedPairVector = rerankedPackedPairVector;
    trace.UsedUnitZWorkingVector = 1u;

    trace.SelectedIndexInRange = selectedIndex < selectedCount ? 1u : 0u;
    if (trace.SelectedIndexInRange != 0u) {
        trace.SelectedRecordMatchesInputNormal =
            (selectedRecordNormal.x == inputContactNormal.x &&
             selectedRecordNormal.y == inputContactNormal.y &&
             selectedRecordNormal.z == inputContactNormal.z)
            ? 1u
            : 0u;
    }

    if (trace.SelectedIndexInRange == 0u || trace.SelectedRecordMatchesInputNormal == 0u) {
        trace.ReloadedInputPackedPair = 1u;
        trace.EffectivePackedPairVector = inputPackedPairVector;
    }
    else {
        trace.RetainedRerankedPackedPair = 1u;
    }

    trace.CalledSecondRerank = 1u;
    trace.SecondRerankSucceeded = secondRerankSucceeded ? 1u : 0u;
    if (!secondRerankSucceeded) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneFollowupRerankKind::ExitWithoutSelection);
        return trace;
    }

    if (std::fabs(inputContactNormal.z) <= PhysicsConstants::SPEED_EPSILON) {
        trace.HorizontalFastReturn = 1u;
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneFollowupRerankKind::HorizontalFastReturn);
        return trace;
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneFollowupRerankKind::ContinueToUncapturedTail);
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneTailPreThirdPassSetup(
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
    bool invokeVerticalCorrection)
{
    GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace trace{};
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.OutputPackedPairVector = inputPackedPairVector;
    trace.InputPositionZ = inputPositionZ;
    trace.OutputPositionZ = inputPositionZ;
    trace.InputFollowupScalar = followupScalar;
    trace.OutputFollowupScalar = followupScalar;
    trace.InputScalarFloor = scalarFloor;
    trace.OutputScalarFloor = scalarFloor;
    trace.InputTailTransformScalar = tailTransformScalar;
    trace.OutputTailTransformScalar = tailTransformScalar;

    trace.HorizontalCorrectionTrace = EvaluateGroundedDriverHorizontalCorrection(
        requestedMove,
        selectedPlaneNormal);
    trace.HorizontalProjectedMove = trace.HorizontalCorrectionTrace.OutputCorrection;
    trace.HorizontalResolved2D = trace.HorizontalCorrectionTrace.OutputResolved2D;

    if (!invokeVerticalCorrection) {
        return trace;
    }

    trace.InvokedVerticalCorrection = 1u;

    const G3D::Vector3 packedPairHorizontal(inputPackedPairVector.x, inputPackedPairVector.y, 0.0f);
    trace.CorrectionTransactionTrace = EvaluateGroundedDriverSelectedPlaneCorrectionTransaction(
        false,
        selectedContactNormalZ,
        selectedPlaneNormal,
        packedPairHorizontal,
        requestedMove,
        tailTransformScalar,
        movementFlags,
        boundingRadius,
        followupScalar,
        1.0f);
    trace.OutputTailTransformScalar = trace.CorrectionTransactionTrace.OutputDistancePointer;

    const G3D::Vector3 combinedProjectedMove =
        trace.CorrectionTransactionTrace.OutputCorrection +
        G3D::Vector3(
            packedPairHorizontal.x * trace.OutputTailTransformScalar,
            packedPairHorizontal.y * trace.OutputTailTransformScalar,
            0.0f);
    trace.ProjectedTailDistance = combinedProjectedMove.magnitude();
    if (!std::isfinite(trace.ProjectedTailDistance) ||
        trace.ProjectedTailDistance <= PhysicsConstants::VECTOR_EPSILON) {
        trace.RejectedOnTransformMagnitude = 1u;
        trace.ProjectedTailDistance = 0.0f;
        return trace;
    }

    trace.PreparedTailRerankInputs = 1u;
    trace.DispatchKind = static_cast<uint32_t>(
        GroundedDriverSelectedPlaneTailPreThirdPassSetupKind::UseProjectedTailRerankInputs);
    trace.HorizontalProjectedMove = combinedProjectedMove;
    trace.HorizontalResolved2D = trace.ProjectedTailDistance;
    trace.ProjectedTailWorkingVector = combinedProjectedMove / trace.ProjectedTailDistance;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailProjectedBlendTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
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
    float verticalLimit)
{
    GroundedDriverSelectedPlaneTailProjectedBlendTrace trace{};
    trace.TailPreThirdPassSetupTrace = EvaluateGroundedDriverSelectedPlaneTailPreThirdPassSetup(
        inputPackedPairVector,
        inputPositionZ,
        followupScalar,
        scalarFloor,
        tailTransformScalar,
        requestedMove,
        selectedPlaneNormal,
        selectedContactNormalZ,
        movementFlags,
        boundingRadius,
        invokeVerticalCorrection);
    trace.UsedProjectedTailRerankInputs =
        trace.TailPreThirdPassSetupTrace.DispatchKind ==
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailPreThirdPassSetupKind::UseProjectedTailRerankInputs)
        ? 1u
        : 0u;

    trace.PostFastReturnTailTrace = EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(
        inputPackedPairVector,
        inputPositionZ,
        followupScalar,
        scalarFloor,
        thirdPassRerankSucceeded,
        requestedMove,
        selectedPlaneNormal,
        verticalLimit,
        trace.TailPreThirdPassSetupTrace.HorizontalProjectedMove,
        trace.TailPreThirdPassSetupTrace.HorizontalResolved2D);
    trace.DispatchKind = trace.PostFastReturnTailTrace.DispatchKind;
    trace.OutputProjectedMove = trace.PostFastReturnTailTrace.OutputProjectedMove;
    trace.OutputResolved2D = trace.PostFastReturnTailTrace.OutputResolved2D;
    trace.OutputPositionZ = trace.PostFastReturnTailTrace.OutputPositionZ;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailWritebackTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneTailWriteback(
    const G3D::Vector3& inputPosition,
    const G3D::Vector3& inputPackedPairVector,
    float followupScalar,
    float scalarFloor,
    float inputSelectedContactNormalZ,
    bool checkWalkableAccepted,
    bool projectedTailRerankSucceeded,
    const G3D::Vector3& projectedTailMove,
    float projectedTailResolved2D)
{
    GroundedDriverSelectedPlaneTailWritebackTrace trace{};
    trace.InputPosition = inputPosition;
    trace.OutputPosition = inputPosition;
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.ProjectedTailMove = projectedTailMove;
    trace.FollowupScalar = followupScalar;
    trace.ScalarFloor = scalarFloor;
    trace.OutputResolved2D = followupScalar;
    trace.ProjectedTailResolved2D = projectedTailResolved2D;
    trace.InputSelectedContactNormalZ = inputSelectedContactNormalZ;
    trace.OutputSelectedContactNormalZ = inputSelectedContactNormalZ;
    trace.CheckWalkableAccepted = checkWalkableAccepted ? 1u : 0u;
    trace.ProjectedTailRerankSucceeded = projectedTailRerankSucceeded ? 1u : 0u;

    trace.OutputPosition.x += inputPackedPairVector.x * followupScalar;
    trace.OutputPosition.y += inputPackedPairVector.y * followupScalar;

    trace.TailScalarDiffExceedsEpsilon =
        std::fabs(followupScalar - scalarFloor) > kWoWResolvedMoveCompareEpsilon ? 1u : 0u;
    if (trace.TailScalarDiffExceedsEpsilon == 0u ||
        trace.CheckWalkableAccepted == 0u ||
        trace.ProjectedTailRerankSucceeded == 0u) {
        return trace;
    }

    trace.AppliedProjectedTailWriteback = 1u;
    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailWritebackKind::ThirdPassPlusProjectedTail);
    trace.OutputPosition += projectedTailMove;
    trace.OutputResolved2D += projectedTailResolved2D;
    trace.OutputSelectedContactNormalZ += projectedTailMove.z;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
WoWCollision::CaptureGroundedDriverSelectedPlaneTailProbeStateSnapshot(
    const G3D::Vector3& field44Vector,
    const G3D::Vector3& field50Vector,
    uint32_t field78,
    const G3D::Vector3& field5cVector,
    float field68,
    float field6c,
    uint32_t field40Flags,
    float field84)
{
    GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace trace{};
    trace.Field44Vector = field44Vector;
    trace.Field50Vector = field50Vector;
    trace.Field78 = field78;
    trace.Field5cVector = field5cVector;
    trace.Field68 = field68;
    trace.Field6c = field6c;
    trace.Field40Flags = field40Flags;
    trace.Field84 = field84;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace
WoWCollision::RestoreGroundedDriverSelectedPlaneTailProbeStateSnapshot(
    const GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace& snapshot)
{
    return snapshot;
}

WoWCollision::GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailElapsedMilliseconds(float elapsedSeconds)
{
    GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace trace{};
    trace.InputElapsedSeconds = elapsedSeconds;
    trace.ScaledMilliseconds = elapsedSeconds * kWoWChooserProbeMsScale;
    trace.AdjustedMilliseconds = trace.ScaledMilliseconds;

    if (trace.ScaledMilliseconds >= 0.0f) {
        trace.AdjustedMilliseconds += 0.5f;
        trace.AddedPositiveHalfBias = 1u;
    } else {
        trace.AdjustedMilliseconds -= 0.5f;
        trace.AddedNegativeHalfBias = 1u;
    }

    trace.RoundedMilliseconds = static_cast<int32_t>(trace.AdjustedMilliseconds);
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailEntrySetupTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailEntrySetup(
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
    float field84)
{
    GroundedDriverSelectedPlaneTailEntrySetupTrace trace{};
    trace.ZeroedPairForwardState = 1u;
    trace.ZeroedDirectStateBit = 1u;
    trace.ZeroedSidecarState = 1u;
    trace.ZeroedLateralOffset = 1u;
    trace.InputWindowMilliseconds = inputWindowMilliseconds;
    trace.InputField78 = field78;
    trace.InputRequestedMove = requestedMove;
    trace.CurrentPosition = currentPosition;
    trace.SnapshotTrace = CaptureGroundedDriverSelectedPlaneTailProbeStateSnapshot(
        field44Vector,
        field50Vector,
        field78,
        field5cVector,
        field68,
        field6c,
        field40Flags,
        field84);

    trace.InputWindowSeconds = static_cast<float>(inputWindowMilliseconds) * kWoWMillisecondsToSecondsScale;
    trace.Field78Seconds = static_cast<float>(field78) * kWoWMillisecondsToSecondsScale;
    trace.ElapsedScalar = 0.0f;
    trace.CurrentWindowScalar = trace.Field78Seconds;
    trace.CurrentMagnitude = std::sqrt(
        (requestedMove.x * requestedMove.x) +
        (requestedMove.y * requestedMove.y) +
        (requestedMove.z * requestedMove.z));
    trace.CurrentHorizontalMagnitude = std::sqrt(
        (requestedMove.x * requestedMove.x) +
        (requestedMove.y * requestedMove.y));
    trace.AbsoluteVerticalMagnitude = std::fabs(requestedMove.z);

    if (!(std::fabs(trace.CurrentMagnitude) > PhysicsConstants::VECTOR_EPSILON)) {
        return trace;
    }

    trace.BuiltNormalizedInputDirection = 1u;
    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailEntrySetupKind::ContinueToProbe);
    trace.OutputNormalizedInputDirection = requestedMove / trace.CurrentMagnitude;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailPostForwardingTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailPostForwarding(
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
    float recomputedScalar84)
{
    GroundedDriverSelectedPlaneTailPostForwardingTrace trace{};
    trace.InputPosition = startPosition;
    trace.OutputPosition = startPosition;
    trace.InputElapsedScalar = elapsedScalar;
    trace.OutputElapsedScalar = elapsedScalar;

    trace.PostForwardingTrace = EvaluateSelectorPairPostForwardingDispatch(
        pairForwardReturnCode,
        directStateBit,
        alternateUnitZStateBit,
        windowSpanScalar,
        windowStartScalar,
        moveVector,
        horizontalReferenceMagnitude,
        movementFlags,
        verticalSpeed,
        horizontalSpeedScale,
        referenceZ,
        positionZ);

    if (pairForwardReturnCode == 2) {
        return trace;
    }

    trace.AppliedMoveToPosition = 1u;
    trace.AdvancedElapsedScalar = 1u;
    trace.OutputPosition += trace.PostForwardingTrace.outputMove;
    trace.OutputElapsedScalar += trace.PostForwardingTrace.outputWindowScalar;

    switch (trace.PostForwardingTrace.dispatchKind) {
    case SELECTOR_PAIR_POST_FORWARDING_ALTERNATE_UNIT_Z:
        trace.InvokedAlternateUnitZStateHandler = 1u;
        trace.AlternateUnitZStateTrace = EvaluateSelectorAlternateUnitZStateHandler(
            movementFlags,
            trace.OutputPosition.z);
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailPostForwardingKind::AlternateUnitZ);
        break;
    case SELECTOR_PAIR_POST_FORWARDING_DIRECT:
        trace.InvokedDirectStateHandler = 1u;
        trace.DirectStateTrace = EvaluateSelectorDirectStateHandler(
            movementFlags,
            trace.OutputPosition,
            facing,
            pitch,
            cachedPosition,
            cachedFacing,
            cachedPitch,
            cachedMoveTimestamp,
            cachedScalar84,
            recomputedScalar84);
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailPostForwardingKind::DirectState);
        break;
    default:
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailPostForwardingKind::NonStatefulContinue);
        break;
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailReturn2LateBranchTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailReturn2LateBranch(
    float consumedWindowSeconds,
    int32_t field58,
    int32_t field78)
{
    GroundedDriverSelectedPlaneTailReturn2LateBranchTrace trace{};
    trace.InputField58 = field58;
    trace.OutputField58 = field58;
    trace.InputField78 = field78;
    trace.OutputField78 = field78;
    trace.ElapsedMillisecondsTrace = EvaluateGroundedDriverSelectedPlaneTailElapsedMilliseconds(consumedWindowSeconds);
    trace.InvokedConsumedWindowCommit = 1u;
    trace.OutputField58 -= trace.ElapsedMillisecondsTrace.RoundedMilliseconds;
    trace.OutputField78 -= trace.ElapsedMillisecondsTrace.RoundedMilliseconds;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailLateNotifierTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailLateNotifier(
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
    bool rerouteLoopUsed)
{
    GroundedDriverSelectedPlaneTailLateNotifierTrace trace{};
    trace.SnapshotTrace = snapshot;
    trace.AlternateUnitZStateBit = alternateUnitZStateBit ? 1u : 0u;
    trace.NotifyRequested = notifyRequested ? 1u : 0u;
    trace.SidecarStatePresent = sidecarStatePresent ? 1u : 0u;
    trace.Bit20InitiallySet = bit20InitiallySet ? 1u : 0u;
    trace.InputField78 = field78;
    trace.OutputField78 = field78;
    trace.RoundedMilliseconds = roundedMilliseconds;
    trace.FieldA0 = fieldA0;
    trace.RerouteLoopUsed = rerouteLoopUsed ? 1u : 0u;

    if (!alternateUnitZStateBit) {
        trace.AddedRoundedMillisecondsToField78 = 1u;
        trace.OutputField78 += roundedMilliseconds;
    }

    if (notifyRequested) {
        if (alternateUnitZStateBit) {
            trace.InvokedAlternateWindowCommit = 1u;
            trace.AlternateWindowCommitArgument = alternateWindowCommitBase + roundedMilliseconds;
        }

        if (sidecarStatePresent) {
            trace.InvokedSidecarCommit = 1u;
        }
    }

    if (!bit20InitiallySet) {
        trace.InvokedCommitGuard = 1u;
        trace.CommitGuardPassed = commitGuardPassed ? 1u : 0u;
        if (commitGuardPassed) {
            trace.ReturnedEarlyAfterCommitGuard = 1u;
            return trace;
        }
    } else {
        trace.InvokedBit20Refresh = 1u;
        trace.Bit20StillSet = bit20StillSet ? 1u : 0u;
        trace.NegativeFieldA0 = fieldA0 < 0.0f ? 1u : 0u;
        trace.LowNibbleFlagsPresent = (lowNibbleFlags & 0xFu) != 0u ? 1u : 0u;

        if (trace.Bit20StillSet != 0u &&
            trace.NegativeFieldA0 != 0u &&
            trace.LowNibbleFlagsPresent != 0u) {
            trace.RestoredSnapshotState = 1u;
        }
    }

    if (rerouteLoopUsed) {
        trace.InvokedRerouteCleanup = 1u;
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
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
    float previousField84)
{
    GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace trace{};
    trace.AttemptIndex = attemptIndex;
    trace.NormalizedInputDirection = normalizedInputDirection;
    trace.RemainingMagnitude = remainingMagnitude;
    trace.LateralOffset = lateralOffset;
    trace.OriginalPosition = originalPosition;
    trace.CurrentPosition = currentPosition;
    trace.OriginalHorizontalMagnitude = originalHorizontalMagnitude;
    trace.OriginalVerticalMagnitude = originalVerticalMagnitude;
    trace.PreviousField68 = previousField68;
    trace.PreviousField6c = previousField6c;
    trace.PreviousField84 = previousField84;

    trace.CandidateVector = normalizedInputDirection * remainingMagnitude;
    trace.CandidateVector.x += lateralOffset.x;
    trace.CandidateVector.y += lateralOffset.y;
    trace.OutputNextInputVector = trace.CandidateVector;

    if (attemptIndex > 1u) {
        trace.CheckedDriftThresholds = 1u;

        const float totalCandidateX = (currentPosition.x - originalPosition.x) + trace.CandidateVector.x;
        const float totalCandidateY = (currentPosition.y - originalPosition.y) + trace.CandidateVector.y;
        trace.CandidateDriftDistance2D = std::sqrt((totalCandidateX * totalCandidateX) + (totalCandidateY * totalCandidateY));
        trace.ExceededHorizontalDrift =
            trace.CandidateDriftDistance2D > (originalHorizontalMagnitude + kWoWProbeRerouteDistanceEpsilon) ? 1u : 0u;

        if (trace.ExceededHorizontalDrift != 0u &&
            trace.CandidateDriftDistance2D > (originalVerticalMagnitude * kWoWProbeVerticalAbortScale)) {
            trace.ExceededVerticalAbortThreshold = 1u;
            trace.DispatchKind =
                static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind::AbortReset);
            return trace;
        }
    }

    trace.CandidateLength2D = std::sqrt(
        (trace.CandidateVector.x * trace.CandidateVector.x) + (trace.CandidateVector.y * trace.CandidateVector.y));
    trace.OutputMagnitude = std::sqrt(
        (trace.CandidateLength2D * trace.CandidateLength2D) + (trace.CandidateVector.z * trace.CandidateVector.z));

    if (trace.CandidateLength2D > PhysicsConstants::VECTOR_EPSILON) {
        const float inverseCandidateLength2D = 1.0f / trace.CandidateLength2D;
        trace.OutputField68 = trace.CandidateVector.x * inverseCandidateLength2D;
        trace.OutputField6c = trace.CandidateVector.y * inverseCandidateLength2D;
        trace.OutputField84 =
            ((trace.OutputField68 * previousField68) + (trace.OutputField6c * previousField6c)) * previousField84;
        trace.NormalizedCandidate2D = 1u;

        if (!(trace.OutputField84 > 0.0f)) {
            trace.OutputField84 = 0.0f;
            trace.ZeroedField84 = 1u;
        }
    } else {
        trace.OutputField68 = trace.CandidateVector.x;
        trace.OutputField6c = trace.CandidateVector.y;
        trace.OutputField84 = 0.0f;
        trace.ZeroedField84 = 1u;
    }

    trace.OutputDirectionVector = G3D::Vector3(trace.OutputField68, trace.OutputField6c, 0.0f);
    trace.UpdatedDirectionFields = 1u;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailProbeVerticalFallback(
    const G3D::Vector3& normalizedInputDirection,
    float currentHorizontalMagnitude,
    float remainingMagnitude,
    bool verticalFallbackAlreadyUsed)
{
    GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace trace{};
    trace.NormalizedInputDirection = normalizedInputDirection;
    trace.CurrentHorizontalMagnitude = currentHorizontalMagnitude;
    trace.RemainingMagnitude = remainingMagnitude;
    trace.VerticalFallbackAlreadyUsed = verticalFallbackAlreadyUsed ? 1u : 0u;

    if (verticalFallbackAlreadyUsed) {
        return trace;
    }

    if (!(std::fabs(currentHorizontalMagnitude) > PhysicsConstants::VECTOR_EPSILON)) {
        return trace;
    }

    trace.HorizontalMagnitudeExceedsEpsilon = 1u;
    trace.ClearedField84 = 1u;
    trace.DispatchKind =
        static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind::UseVerticalFallback);
    trace.OutputNextInputVector = G3D::Vector3(0.0f, 0.0f, normalizedInputDirection.z * remainingMagnitude);
    trace.OutputMagnitude = std::fabs(trace.OutputNextInputVector.z);
    trace.OutputField84 = 0.0f;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace
WoWCollision::EvaluateGroundedDriverSelectedPlaneTailRerouteLoopController(
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
    bool verticalFallbackAlreadyUsed)
{
    GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace{};
    trace.InputAttemptIndex = attemptIndex;
    trace.OutputAttemptIndex = attemptIndex;
    trace.InputCurrentHorizontalMagnitude = currentHorizontalMagnitude;
    trace.OutputField68 = previousField68;
    trace.OutputField6c = previousField6c;
    trace.OutputField84 = previousField84;

    if (incrementAttemptBeforeProbe) {
        trace.IncrementedAttemptBeforeProbe = 1u;
        ++trace.OutputAttemptIndex;
    }

    if (trace.OutputAttemptIndex > 5u) {
        trace.AttemptLimitExceeded = 1u;
        trace.InvokedVerticalFallback = 1u;
        trace.VerticalFallbackTrace = EvaluateGroundedDriverSelectedPlaneTailProbeVerticalFallback(
            normalizedInputDirection,
            currentHorizontalMagnitude,
            remainingMagnitude,
            verticalFallbackAlreadyUsed);

        if (trace.VerticalFallbackTrace.DispatchKind ==
            static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind::UseVerticalFallback)) {
            trace.DispatchKind =
                static_cast<uint32_t>(GroundedDriverSelectedPlaneTailRerouteLoopControllerKind::UseVerticalFallback);
            trace.OutputVerticalFallbackUsed = 1u;
            trace.OutputAttemptIndex = 0u;
            trace.OutputNextInputVector = trace.VerticalFallbackTrace.OutputNextInputVector;
            trace.OutputMagnitude = trace.VerticalFallbackTrace.OutputMagnitude;
            trace.OutputDirectionVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
            trace.OutputField68 = 0.0f;
            trace.OutputField6c = 0.0f;
            trace.OutputField84 = trace.VerticalFallbackTrace.OutputField84;
            return trace;
        }

        trace.InvokedResetStateHandler = 1u;
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailRerouteLoopControllerKind::ResetState);
        return trace;
    }

    trace.InvokedCandidateProbe = 1u;
    trace.CandidateTrace = EvaluateGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
        trace.OutputAttemptIndex,
        normalizedInputDirection,
        remainingMagnitude,
        lateralOffset,
        originalPosition,
        currentPosition,
        originalHorizontalMagnitude,
        originalVerticalMagnitude,
        previousField68,
        previousField6c,
        previousField84);

    if (trace.CandidateTrace.DispatchKind ==
        static_cast<uint32_t>(GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind::AbortReset)) {
        trace.InvokedResetStateHandler = 1u;
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailRerouteLoopControllerKind::ResetState);
        return trace;
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailRerouteLoopControllerKind::AcceptCandidate);
    trace.OutputNextInputVector = trace.CandidateTrace.OutputNextInputVector;
    trace.OutputDirectionVector = trace.CandidateTrace.OutputDirectionVector;
    trace.OutputMagnitude = trace.CandidateTrace.OutputMagnitude;
    trace.OutputField68 = trace.CandidateTrace.OutputField68;
    trace.OutputField6c = trace.CandidateTrace.OutputField6c;
    trace.OutputField84 = trace.CandidateTrace.OutputField84;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailChooserProbeTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneTailChooserProbe(
    const G3D::Vector3& inputPackedPairVector,
    const G3D::Vector3& inputProjectedMove,
    float chooserInputScalar,
    const G3D::Vector3& probePosition,
    float collisionRadius)
{
    GroundedDriverSelectedPlaneTailChooserProbeTrace trace{};
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.InputProjectedMove = inputProjectedMove;
    trace.ChooserInputScalar = chooserInputScalar;
    trace.ProbePosition = probePosition;
    trace.CollisionRadius = collisionRadius;

    const float probeBudgetFloat = std::sqrt(
        ((chooserInputScalar + kWoWChooserProbeScalarBias) * 2.0f) * PhysicsConstants::INV_GRAVITY) *
        kWoWChooserProbeMsScale;
    trace.ProbeBudgetMilliseconds = RoundHalfAwayFromZero(probeBudgetFloat);

    trace.ProbeDelta.x = probePosition.x - inputProjectedMove.x;
    trace.ProbeDelta.y = probePosition.y - inputProjectedMove.y;
    trace.ProbeDelta.z = 0.0f;
    trace.ProbeDistance2D = std::sqrt(
        (trace.ProbeDelta.x * trace.ProbeDelta.x) + (trace.ProbeDelta.y * trace.ProbeDelta.y));
    if (!(trace.ProbeDistance2D > collisionRadius)) {
        return trace;
    }

    trace.OutsideCollisionRadius = 1u;
    if (trace.ProbeDistance2D > PhysicsConstants::VECTOR_EPSILON) {
        const float inverseProbeDistance = 1.0f / trace.ProbeDistance2D;
        trace.AlignmentDot =
            (inputPackedPairVector.x * (trace.ProbeDelta.x * inverseProbeDistance)) +
            (inputPackedPairVector.y * (trace.ProbeDelta.y * inverseProbeDistance));
    }

    if (trace.AlignmentDot > kWoWChooserAlignmentDotMin) {
        trace.AlignmentAccepted = 1u;
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailChooserProbeKind::AcceptSelectedPlane);
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailChooserContractTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneTailChooserContract(
    const G3D::Vector3& chooserInputPackedPairVector,
    const G3D::Vector3& chooserInputProjectedMove,
    float chooserInputScalar,
    uint32_t finalSelectedIndex,
    uint32_t finalSelectedCount,
    float finalSelectedNormalZ,
    bool chooserAcceptedSelectedPlane,
    uint32_t movementFlags,
    const G3D::Vector3& chooserOutputProjectedMove)
{
    GroundedDriverSelectedPlaneTailChooserContractTrace trace{};
    trace.FinalSelectedIndex = finalSelectedIndex;
    trace.FinalSelectedCount = finalSelectedCount;
    trace.FinalSelectedNormalZ = finalSelectedNormalZ;
    trace.ChooserInputPackedPairVector = chooserInputPackedPairVector;
    trace.ChooserInputProjectedMove = chooserInputProjectedMove;
    trace.ChooserOutputProjectedMove = chooserOutputProjectedMove;
    trace.ChooserInputScalar = chooserInputScalar;
    trace.GroundedWall04000000Set = (movementFlags & MOVEFLAG_SPLINE_ELEVATION) != 0u ? 1u : 0u;
    trace.ProjectedMoveMutatedByChooser =
        chooserInputProjectedMove != chooserOutputProjectedMove ? 1u : 0u;

    trace.FinalSelectedIndexInRange = finalSelectedIndex < finalSelectedCount ? 1u : 0u;
    trace.FinalSelectedPlaneWalkable =
        (trace.FinalSelectedIndexInRange != 0u && finalSelectedNormalZ > kWoWWalkableMinNormalZ) ? 1u : 0u;

    if (trace.FinalSelectedPlaneWalkable != 0u) {
        trace.Called635F80 = 1u;
        trace.ChooserAcceptedSelectedPlane = chooserAcceptedSelectedPlane ? 1u : 0u;
        if (!chooserAcceptedSelectedPlane) {
            trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailChooserContractKind::Return1Horizontal);
            return trace;
        }
    }

    if (trace.GroundedWall04000000Set == 0u) {
        trace.WroteField80FromSelectedZ = 1u;
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneThirdPassSetupTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneThirdPassSetup(
    const G3D::Vector3& inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    bool thirdPassRerankSucceeded)
{
    GroundedDriverSelectedPlaneThirdPassSetupTrace trace{};
    trace.LoadedPackedPairXY = 1u;
    trace.ZeroedWorkingVectorZ = 1u;
    trace.AdvancedPositionZ = 1u;
    trace.InvokedThirdPassRerank = 1u;
    trace.ThirdPassRerankSucceeded = thirdPassRerankSucceeded ? 1u : 0u;
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.ThirdPassWorkingVector = G3D::Vector3(inputPackedPairVector.x, inputPackedPairVector.y, 0.0f);
    trace.InputPositionZ = inputPositionZ;
    trace.OutputPositionZ = inputPositionZ + followupScalar;
    trace.FollowupScalar = followupScalar;
    trace.ScalarFloor = scalarFloor;
    trace.DispatchKind = static_cast<uint32_t>(
        thirdPassRerankSucceeded
            ? GroundedDriverSelectedPlaneThirdPassSetupKind::ContinueToBlendCorrection
            : GroundedDriverSelectedPlaneThirdPassSetupKind::ExitWithoutSelection);
    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneBlendCorrectionTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneBlendCorrection(
    const G3D::Vector3& requestedMove,
    const G3D::Vector3& selectedPlaneNormal,
    float verticalLimit,
    const G3D::Vector3& horizontalProjectedMove,
    float horizontalResolved2D)
{
    GroundedDriverSelectedPlaneBlendCorrectionTrace trace{};
    trace.RequestedMove = requestedMove;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.HorizontalProjectedMove = horizontalProjectedMove;
    trace.OutputProjectedMove = horizontalProjectedMove;
    trace.VerticalLimit = std::fabs(verticalLimit);
    trace.HorizontalResolved2D = horizontalResolved2D;
    trace.OutputResolved2D = horizontalResolved2D;

    trace.IntoPlane = requestedMove.dot(selectedPlaneNormal);
    trace.SelectedPlaneProjectedMove = requestedMove - (selectedPlaneNormal * trace.IntoPlane);

    if (trace.VerticalLimit > PhysicsConstants::VECTOR_EPSILON &&
        std::fabs(trace.SelectedPlaneProjectedMove.z) > trace.VerticalLimit) {
        const float scale = trace.VerticalLimit / std::fabs(trace.SelectedPlaneProjectedMove.z);
        trace.SelectedPlaneProjectedMove.x *= scale;
        trace.SelectedPlaneProjectedMove.y *= scale;
        trace.SelectedPlaneProjectedMove.z *= scale;
        trace.ClampedVerticalMagnitude = 1u;
    }

    trace.SlopedResolved2D = std::sqrt(
        (trace.SelectedPlaneProjectedMove.x * trace.SelectedPlaneProjectedMove.x) +
        (trace.SelectedPlaneProjectedMove.y * trace.SelectedPlaneProjectedMove.y));

    if (trace.SelectedPlaneProjectedMove.z > 0.0f &&
        horizontalResolved2D > trace.SlopedResolved2D + kWoWResolvedMoveCompareEpsilon) {
        trace.DiscardedUphillBlend = 1u;
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBlendCorrectionKind::UseHorizontalFallback);
        return trace;
    }

    trace.AcceptedSelectedPlaneBlend = 1u;
    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneBlendCorrectionKind::UseSelectedPlaneBlend);
    trace.OutputProjectedMove = trace.SelectedPlaneProjectedMove;
    trace.OutputResolved2D = trace.SlopedResolved2D;
    return trace;
}

WoWCollision::GroundedDriverSelectedPlanePostFastReturnTailTrace WoWCollision::EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(
    const G3D::Vector3& inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    bool thirdPassRerankSucceeded,
    const G3D::Vector3& requestedMove,
    const G3D::Vector3& selectedPlaneNormal,
    float verticalLimit,
    const G3D::Vector3& horizontalProjectedMove,
    float horizontalResolved2D)
{
    GroundedDriverSelectedPlanePostFastReturnTailTrace trace{};
    trace.InputPackedPairVector = inputPackedPairVector;
    trace.InputPositionZ = inputPositionZ;
    trace.OutputPositionZ = inputPositionZ;
    trace.FollowupScalar = followupScalar;
    trace.ScalarFloor = scalarFloor;
    trace.RequestedMove = requestedMove;
    trace.SelectedPlaneNormal = selectedPlaneNormal;
    trace.VerticalLimit = verticalLimit;
    trace.HorizontalProjectedMove = horizontalProjectedMove;
    trace.OutputProjectedMove = horizontalProjectedMove;
    trace.HorizontalResolved2D = horizontalResolved2D;
    trace.OutputResolved2D = horizontalResolved2D;

    trace.ThirdPassSetupTrace = EvaluateGroundedDriverSelectedPlaneThirdPassSetup(
        inputPackedPairVector,
        inputPositionZ,
        followupScalar,
        scalarFloor,
        thirdPassRerankSucceeded);
    trace.OutputPositionZ = trace.ThirdPassSetupTrace.OutputPositionZ;
    if (trace.ThirdPassSetupTrace.ThirdPassRerankSucceeded == 0u) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlanePostFastReturnTailKind::ExitWithoutSelection);
        return trace;
    }

    trace.InvokedBlendCorrection = 1u;
    trace.BlendCorrectionTrace = EvaluateGroundedDriverSelectedPlaneBlendCorrection(
        requestedMove,
        selectedPlaneNormal,
        verticalLimit,
        horizontalProjectedMove,
        horizontalResolved2D);
    trace.OutputProjectedMove = trace.BlendCorrectionTrace.OutputProjectedMove;
    trace.OutputResolved2D = trace.BlendCorrectionTrace.OutputResolved2D;

    if (trace.BlendCorrectionTrace.DispatchKind ==
        static_cast<uint32_t>(GroundedDriverSelectedPlaneBlendCorrectionKind::UseSelectedPlaneBlend)) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlanePostFastReturnTailKind::UseSelectedPlaneBlend);
    } else {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlanePostFastReturnTailKind::UseHorizontalFallback);
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPlaneTailReturnDispatchTrace WoWCollision::EvaluateGroundedDriverSelectedPlaneTailReturnDispatch(
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
    uint32_t movementFlags)
{
    GroundedDriverSelectedPlaneTailReturnDispatchTrace trace{};
    trace.FinalSelectedIndex = finalSelectedIndex;
    trace.FinalSelectedCount = finalSelectedCount;
    trace.FinalSelectedNormalZ = finalSelectedNormalZ;
    trace.GroundedWall04000000Set = (movementFlags & MOVEFLAG_SPLINE_ELEVATION) != 0u ? 1u : 0u;

    trace.PostFastReturnTailTrace = EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(
        inputPackedPairVector,
        inputPositionZ,
        followupScalar,
        scalarFloor,
        thirdPassRerankSucceeded,
        requestedMove,
        selectedPlaneNormal,
        verticalLimit,
        horizontalProjectedMove,
        horizontalResolved2D);
    trace.OutputProjectedMove = trace.PostFastReturnTailTrace.OutputProjectedMove;
    trace.OutputResolved2D = trace.PostFastReturnTailTrace.OutputResolved2D;
    trace.OutputPositionZ = trace.PostFastReturnTailTrace.OutputPositionZ;

    if (trace.PostFastReturnTailTrace.DispatchKind ==
        static_cast<uint32_t>(GroundedDriverSelectedPlanePostFastReturnTailKind::ExitWithoutSelection)) {
        return trace;
    }

    trace.CalledTailRerank = 1u;
    trace.TailRerankSucceeded = tailRerankSucceeded ? 1u : 0u;
    if (!tailRerankSucceeded) {
        return trace;
    }

    trace.FinalSelectedIndexInRange = finalSelectedIndex < finalSelectedCount ? 1u : 0u;
    trace.FinalSelectedPlaneWalkable =
        (trace.FinalSelectedIndexInRange != 0u && finalSelectedNormalZ > kWoWWalkableMinNormalZ) ? 1u : 0u;

    if (trace.FinalSelectedPlaneWalkable != 0u) {
        trace.Called635F80 = 1u;
        trace.ChooserAcceptedSelectedPlane = chooserAcceptedSelectedPlane ? 1u : 0u;
        if (!chooserAcceptedSelectedPlane) {
            trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailReturnDispatchKind::Return1Horizontal);
            return trace;
        }
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPlaneTailReturnDispatchKind::Return2SelectedPlane);
    if (trace.GroundedWall04000000Set == 0u) {
        trace.WroteField80FromSelectedZ = 1u;
    }

    return trace;
}

WoWCollision::GroundedDriverSelectedPairCommitTailTrace WoWCollision::EvaluateGroundedDriverSelectedPairCommitTail(
    uint32_t selectedIndex,
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
    float inputVerticalSpeed)
{
    GroundedDriverSelectedPairCommitTailTrace trace{};
    trace.SelectedIndex = selectedIndex;
    trace.SelectedCount = selectedCount;
    trace.ConsumedSelectedState = consumedSelectedState ? 1u : 0u;
    trace.SnapshotBeforeCommitState = snapshotBeforeCommitState ? 1u : 0u;
    trace.InputMovementFlags = movementFlags;
    trace.OutputMovementFlags = movementFlags;
    trace.CachedPair = cachedPair;
    trace.CurrentPosition = currentPosition;
    trace.InputCachedPosition = cachedPosition;
    trace.OutputCachedPosition = cachedPosition;
    trace.CurrentFacing = currentFacing;
    trace.InputCachedFacing = cachedFacing;
    trace.OutputCachedFacing = cachedFacing;
    trace.CurrentPitch = currentPitch;
    trace.InputCachedPitch = cachedPitch;
    trace.OutputCachedPitch = cachedPitch;
    trace.InputMoveTimestamp = cachedMoveTimestamp;
    trace.OutputMoveTimestamp = cachedMoveTimestamp;
    trace.InputFallTime = inputFallTime;
    trace.OutputFallTime = inputFallTime;
    trace.InputFallStartZ = inputFallStartZ;
    trace.OutputFallStartZ = inputFallStartZ;
    trace.InputVerticalSpeed = inputVerticalSpeed;
    trace.OutputVerticalSpeed = inputVerticalSpeed;

    const bool useStartFallZero = selectedIndex >= selectedCount || consumedSelectedState;
    if (useStartFallZero) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitTailKind::StartFallZero);
        trace.UsedStartFallZero = 1u;
        trace.UsedCacheSnapshot = 1u;
        trace.OutputCachedPosition = currentPosition;
        trace.OutputCachedFacing = currentFacing;
        trace.OutputCachedPitch = currentPitch;
        trace.OutputMoveTimestamp = 0u;
        trace.OutputMovementFlags = (movementFlags & ~(MOVEFLAG_SPLINE_ELEVATION | MOVEFLAG_SWIMMING)) | MOVEFLAG_JUMPING;
        trace.OutputFallTime = 0u;
        trace.OutputFallStartZ = currentPosition.z;
        trace.OutputVerticalSpeed = 0.0f;
        return trace;
    }

    if (snapshotBeforeCommitState) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitTailKind::ForwardPair);
        trace.UsedCacheSnapshot = 1u;
        trace.ForwardedPair = 1u;
        trace.OutputPair = cachedPair;
        trace.OutputCachedPosition = currentPosition;
        trace.OutputCachedFacing = currentFacing;
        trace.OutputCachedPitch = currentPitch;
        trace.OutputMoveTimestamp = 0u;
        return trace;
    }

    if ((movementFlags & MOVEFLAG_HOVER) != 0u) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitTailKind::DeferredHoverRerank);
        trace.DeferredHoverRerank = 1u;
        return trace;
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitTailKind::ForwardPair);
    trace.ForwardedPair = 1u;
    trace.OutputPair = cachedPair;
    return trace;
}

WoWCollision::GroundedDriverSelectedPairCommitGuardTrace WoWCollision::EvaluateGroundedDriverSelectedPairCommitGuard(
    const SelectorPair& incomingPair,
    const SelectorPair& storedPair,
    bool probeRejectOnStoredPairUnload,
    bool contextMatchesGlobal,
    bool hasAttachedPointer,
    bool attachedBit4Set,
    int32_t opaqueConsumerReturnValue)
{
    GroundedDriverSelectedPairCommitGuardTrace trace{};
    trace.IncomingPair = incomingPair;
    trace.StoredPair = storedPair;
    trace.ZeroIncomingPair = (incomingPair.first == 0.0f && incomingPair.second == 0.0f) ? 1u : 0u;
    trace.StoredPairNonZero = (storedPair.first != 0.0f || storedPair.second != 0.0f) ? 1u : 0u;
    trace.ContextMatchesGlobal = contextMatchesGlobal ? 1u : 0u;
    trace.HasAttachedPointer = hasAttachedPointer ? 1u : 0u;
    trace.AttachedBit4Set = attachedBit4Set ? 1u : 0u;

    if (trace.ZeroIncomingPair != 0u && trace.StoredPairNonZero != 0u) {
        trace.ProbeRejectChecked = 1u;
        if (probeRejectOnStoredPairUnload) {
            trace.ProbeRejected = 1u;
            trace.GuardKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitGuardKind::RejectProbeHit);
            trace.ReturnValue = 0;
            return trace;
        }
    }

    if (!contextMatchesGlobal) {
        trace.GuardKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitGuardKind::RejectContextMismatch);
        trace.ReturnValue = 0;
        return trace;
    }

    if (hasAttachedPointer && !attachedBit4Set) {
        trace.GuardKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitGuardKind::RejectAttachedBit);
        trace.ReturnValue = 0;
        return trace;
    }

    trace.CalledOpaqueConsumer = 1u;
    trace.GuardKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitGuardKind::CallOpaqueConsumer);
    trace.ReturnValue = opaqueConsumerReturnValue;
    return trace;
}

WoWCollision::GroundedDriverSelectedPairCommitBodyTrace WoWCollision::EvaluateGroundedDriverSelectedPairCommitBody(
    const SelectorPair& incomingPair,
    const SelectorPair& storedPair,
    bool incomingPairValidatorAccepted,
    bool hasTransformConsumer,
    float storedPhaseScalar,
    float incomingPhaseScalar)
{
    GroundedDriverSelectedPairCommitBodyTrace trace{};
    trace.IncomingPair = incomingPair;
    trace.StoredPair = storedPair;
    trace.IncomingPairNonZero = (incomingPair.first != 0.0f || incomingPair.second != 0.0f) ? 1u : 0u;
    trace.StoredPairNonZero = (storedPair.first != 0.0f || storedPair.second != 0.0f) ? 1u : 0u;
    trace.IncomingPairMatchesStoredPair =
        (incomingPair.first == storedPair.first && incomingPair.second == storedPair.second) ? 1u : 0u;

    if (trace.IncomingPairMatchesStoredPair != 0u) {
        trace.CommitKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitBodyKind::RejectUnchangedPair);
        trace.ReturnValue = 0;
        return trace;
    }

    if (trace.IncomingPairNonZero != 0u) {
        trace.CalledIncomingPairValidator = 1u;
        if (!incomingPairValidatorAccepted) {
            trace.CommitKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitBodyKind::RejectIncomingPairValidator);
            trace.ReturnValue = 0;
            return trace;
        }

        trace.IncomingPairValidatorAccepted = 1u;
    }

    trace.InitializedStoredTransformIdentity = 1u;
    trace.InitializedIncomingTransformIdentity = 1u;

    if (trace.StoredPairNonZero != 0u) {
        trace.ProcessedStoredPair = 1u;
        trace.StoredPhaseScalar = storedPhaseScalar;
        trace.AppliedStoredTransformScalar = 1u;
        trace.AppliedStoredTransformMatrix = 1u;
        trace.AppliedStoredTransformFinalize = 1u;
        if (hasTransformConsumer) {
            trace.CalledStoredAttachmentBridge = 1u;
        }
    }

    if (trace.IncomingPairNonZero != 0u) {
        trace.ProcessedIncomingPair = 1u;
        trace.IncomingPhaseScalar = incomingPhaseScalar;
        trace.AppliedIncomingTransformScalar = 1u;
        trace.AppliedIncomingTransformMatrix = 1u;
        trace.AppliedIncomingTransformFinalize = 1u;
        if (hasTransformConsumer) {
            trace.CalledIncomingAttachmentBridge = 1u;
        }
    }

    trace.WroteCommittedPair = 1u;
    trace.CalledCommitNotification = 1u;
    trace.OutputCommittedPair = incomingPair;
    trace.CommitKind = static_cast<uint32_t>(GroundedDriverSelectedPairCommitBodyKind::CommitPair);
    trace.ReturnValue = 1;
    return trace;
}

WoWCollision::GroundedDriverHoverRerankTrace WoWCollision::EvaluateGroundedDriverHoverRerankDispatch(
    bool firstRerankSucceeded,
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
    float inputVerticalSpeed)
{
    GroundedDriverHoverRerankTrace trace{};
    trace.FirstRerankSucceeded = firstRerankSucceeded ? 1u : 0u;
    trace.SelectedIndex = selectedIndex;
    trace.SelectedCount = selectedCount;
    trace.UsedStandardWalkableThreshold = useStandardWalkableThreshold ? 1u : 0u;
    trace.SelectedNormalZ = selectedNormalZ;
    trace.ThresholdNormalZ = useStandardWalkableThreshold
        ? PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z
        : kWoWRelaxedHoverNormalZThreshold;
    trace.InputWindowSpanScalar = inputWindowSpanScalar;
    trace.FollowupScalarCandidate = followupScalarCandidate;
    trace.InputMovementFlags = movementFlags;
    trace.OutputMovementFlags = movementFlags;
    trace.InputPositionZ = positionZ;
    trace.OutputPositionZ = positionZ;
    trace.InputFallTime = inputFallTime;
    trace.OutputFallTime = inputFallTime;
    trace.InputFallStartZ = inputFallStartZ;
    trace.OutputFallStartZ = inputFallStartZ;
    trace.InputVerticalSpeed = inputVerticalSpeed;
    trace.OutputVerticalSpeed = inputVerticalSpeed;

    if (!firstRerankSucceeded) {
        return trace;
    }

    trace.SelectedIndexInRange = selectedIndex < selectedCount ? 1u : 0u;
    if (trace.SelectedIndexInRange != 0u) {
        if (selectedNormalZ <= trace.ThresholdNormalZ) {
            trace.DispatchKind = static_cast<uint32_t>(GroundedDriverHoverRerankDispatchKind::StartFallZero);
            trace.StartedFallWithZeroVelocity = 1u;
            trace.OutputMovementFlags = (movementFlags & ~(MOVEFLAG_SPLINE_ELEVATION | MOVEFLAG_SWIMMING)) | MOVEFLAG_JUMPING;
            trace.OutputFallTime = 0u;
            trace.OutputFallStartZ = positionZ;
            trace.OutputVerticalSpeed = 0.0f;
            return trace;
        }

        trace.SelectedNormalAccepted = 1u;
        trace.LoadedSelectedPair = 1u;
        trace.OutputPair = selectedPair;
    }

    if (inputWindowSpanScalar > 1.0f) {
        trace.DispatchKind = static_cast<uint32_t>(GroundedDriverHoverRerankDispatchKind::ForwardPair);
        trace.UsedDirectForwardAboveOne = 1u;
        trace.ForwardedPair = 1u;
        return trace;
    }

    trace.OutputFollowupScalar = followupScalarCandidate;
    if (followupScalarCandidate <= 0.0f) {
        trace.ZeroedFollowupScalar = 1u;
        trace.OutputFollowupScalar = 0.0f;
    }
    else {
        const float remainingWindowSpan = 1.0f - inputWindowSpanScalar;
        if (followupScalarCandidate > remainingWindowSpan) {
            trace.ClampedFollowupScalar = 1u;
            trace.OutputFollowupScalar = remainingWindowSpan;
        }
    }

    trace.CalledSecondRerank = 1u;
    trace.SecondRerankSucceeded = secondRerankSucceeded ? 1u : 0u;
    if (!secondRerankSucceeded) {
        return trace;
    }

    trace.DispatchKind = static_cast<uint32_t>(GroundedDriverHoverRerankDispatchKind::ForwardPair);
    trace.ForwardedPair = 1u;
    trace.AdvancedPositionZ = 1u;
    trace.OutputPositionZ = positionZ + trace.OutputFollowupScalar;
    return trace;
}
