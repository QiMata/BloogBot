#include "PhysicsEngine.h"
#include "GroundedDriverParity.h"

// Write scope for grounded-driver test exports.
// New DLL exports for this cluster should live here instead of PhysicsTestExports.cpp.

extern "C" {

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverFirstDispatch(uint32_t walkableSelectedContact,
                                                                      uint32_t gateReturnCode,
                                                                      float remainingDistanceBeforeDispatch,
                                                                      float sweepDistanceBeforeVertical,
                                                                      float sweepDistanceAfterVertical,
                                                                      WoWCollision::GroundedDriverFirstDispatchTrace* outTrace)
{
    const WoWCollision::GroundedDriverFirstDispatchTrace trace = WoWCollision::EvaluateGroundedDriverFirstDispatch(
        walkableSelectedContact != 0u,
        gateReturnCode,
        remainingDistanceBeforeDispatch,
        sweepDistanceBeforeVertical,
        sweepDistanceAfterVertical);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedContactDispatch(
    uint32_t checkWalkableAccepted,
    uint32_t consumedSelectedState,
    uint32_t movementFlags,
    float remainingDistanceBeforeDispatch,
    float sweepDistanceBeforeVertical,
    float sweepDistanceAfterVertical,
    uint32_t inputFallTime,
    float inputFallStartZ,
    float inputVerticalSpeed,
    float positionZ,
    WoWCollision::GroundedDriverSelectedContactDispatchTrace* outTrace)
{
    const WoWCollision::GroundedDriverSelectedContactDispatchTrace trace = WoWCollision::EvaluateGroundedDriverSelectedContactDispatch(
        checkWalkableAccepted != 0u,
        consumedSelectedState != 0u,
        movementFlags,
        remainingDistanceBeforeDispatch,
        sweepDistanceBeforeVertical,
        sweepDistanceAfterVertical,
        inputFallTime,
        inputFallStartZ,
        inputVerticalSpeed,
        positionZ);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverResweepBookkeeping(
    const G3D::Vector3* direction,
    float sweepScalar,
    const G3D::Vector3* correction,
    float horizontalBudgetBefore,
    WoWCollision::GroundedDriverResweepBookkeepingTrace* outTrace)
{
    if (!direction || !correction) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverResweepBookkeepingTrace{};
        }
        return 0u;
    }

    const WoWCollision::GroundedDriverResweepBookkeepingTrace trace = WoWCollision::EvaluateGroundedDriverResweepBookkeeping(
        *direction,
        sweepScalar,
        *correction,
        horizontalBudgetBefore);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.FinalizeFlag;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverVerticalCap(
    uint32_t movementFlags,
    float combinedMoveZ,
    float nextSweepDistance,
    float currentZ,
    float boundingRadius,
    float capField80,
    WoWCollision::GroundedDriverVerticalCapTrace* outTrace)
{
    const WoWCollision::GroundedDriverVerticalCapTrace trace = WoWCollision::EvaluateGroundedDriverVerticalCap(
        movementFlags,
        combinedMoveZ,
        nextSweepDistance,
        currentZ,
        boundingRadius,
        capField80);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.AppliedCap;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneCorrection(
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float verticalLimit,
    float remainingDistanceBefore,
    float sweepDistanceBefore,
    float sweepDistanceAfter,
    float inputSweepFraction,
    float inputDistancePointer,
    WoWCollision::GroundedDriverSelectedPlaneCorrectionTrace* outTrace)
{
    if (!requestedMove || !selectedPlaneNormal) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneCorrectionTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneCorrectionKind::VerticalOnly);
    }

    const WoWCollision::GroundedDriverSelectedPlaneCorrectionTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneCorrection(
            *requestedMove,
            *selectedPlaneNormal,
            verticalLimit,
            remainingDistanceBefore,
            sweepDistanceBefore,
            sweepDistanceAfter,
            inputSweepFraction,
            inputDistancePointer);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.CorrectionKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneCorrectionTransaction(
    uint32_t useSelectedPlaneOverride,
    float selectedContactNormalZ,
    const G3D::Vector3* selectedPlaneNormal,
    const G3D::Vector3* inputWorkingVector,
    const G3D::Vector3* inputMoveDirection,
    float inputDistancePointer,
    uint32_t movementFlags,
    float boundingRadius,
    float remainingDistanceBefore,
    float inputSweepFraction,
    WoWCollision::GroundedDriverSelectedPlaneCorrectionTransactionTrace* outTrace)
{
    if (!selectedPlaneNormal || !inputWorkingVector || !inputMoveDirection) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneCorrectionTransactionTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneCorrectionTransactionKind::DirectScalar);
    }

    const WoWCollision::GroundedDriverSelectedPlaneCorrectionTransactionTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneCorrectionTransaction(
            useSelectedPlaneOverride != 0u,
            selectedContactNormalZ,
            *selectedPlaneNormal,
            *inputWorkingVector,
            *inputMoveDirection,
            inputDistancePointer,
            movementFlags,
            boundingRadius,
            remainingDistanceBefore,
            inputSweepFraction);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.CorrectionKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneDistancePointerMutation(
    uint32_t useSelectedPlaneOverride,
    float selectedContactNormalZ,
    const G3D::Vector3* selectedPlaneNormal,
    const G3D::Vector3* inputWorkingVector,
    const G3D::Vector3* inputMoveDirection,
    float inputDistancePointer,
    uint32_t movementFlags,
    float boundingRadius,
    WoWCollision::GroundedDriverSelectedPlaneDistancePointerTrace* outTrace)
{
    if (!selectedPlaneNormal || !inputWorkingVector || !inputMoveDirection) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneDistancePointerTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneDistancePointerKind::DirectScalar);
    }

    const WoWCollision::GroundedDriverSelectedPlaneDistancePointerTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneDistancePointerMutation(
            useSelectedPlaneOverride != 0u,
            selectedContactNormalZ,
            *selectedPlaneNormal,
            *inputWorkingVector,
            *inputMoveDirection,
            inputDistancePointer,
            movementFlags,
            boundingRadius);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.OutputKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverHorizontalCorrection(
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    WoWCollision::GroundedDriverHorizontalCorrectionTrace* outTrace)
{
    if (!requestedMove || !selectedPlaneNormal) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverHorizontalCorrectionTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverHorizontalCorrectionKind::ZeroedOnReject);
    }

    const WoWCollision::GroundedDriverHorizontalCorrectionTrace trace =
        WoWCollision::EvaluateGroundedDriverHorizontalCorrection(
            *requestedMove,
            *selectedPlaneNormal);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.CorrectionKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneRetryTransaction(
    uint32_t walkableSelectedContact,
    uint32_t gateReturnCode,
    uint32_t useSelectedPlaneOverride,
    float selectedContactNormalZ,
    const G3D::Vector3* selectedPlaneNormal,
    const G3D::Vector3* inputWorkingVector,
    const G3D::Vector3* inputMoveDirection,
    float inputDistancePointer,
    uint32_t movementFlags,
    float boundingRadius,
    float remainingDistanceBefore,
    float inputSweepFraction,
    WoWCollision::GroundedDriverSelectedPlaneRetryTrace* outTrace)
{
    if (!selectedPlaneNormal || !inputWorkingVector || !inputMoveDirection) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneRetryTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneBranchGateKind::ExitWithoutMutation);
    }

    const WoWCollision::GroundedDriverSelectedPlaneRetryTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneRetryTransaction(
            walkableSelectedContact != 0u,
            gateReturnCode,
            useSelectedPlaneOverride != 0u,
            selectedContactNormalZ,
            *selectedPlaneNormal,
            *inputWorkingVector,
            *inputMoveDirection,
            inputDistancePointer,
            movementFlags,
            boundingRadius,
            remainingDistanceBefore,
            inputSweepFraction);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.BranchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneFirstPassSetup(
    const G3D::Vector3* inputPackedPairVector,
    float fieldB0,
    float boundingRadius,
    const G3D::Vector3* inputContactNormal,
    uint32_t firstPassRerankSucceeded,
    WoWCollision::GroundedDriverSelectedPlaneFirstPassSetupTrace* outTrace)
{
    if (!inputPackedPairVector || !inputContactNormal) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneFirstPassSetupTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneFirstPassSetupKind::SkipToFollowupRerank);
    }

    const WoWCollision::GroundedDriverSelectedPlaneFirstPassSetupTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneFirstPassSetup(
            *inputPackedPairVector,
            fieldB0,
            boundingRadius,
            *inputContactNormal,
            firstPassRerankSucceeded != 0u);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneBranchGate(
    uint32_t walkableSelectedContact,
    uint32_t gateReturnCode,
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float verticalLimit,
    float remainingDistanceBefore,
    float sweepDistanceBefore,
    float sweepDistanceAfter,
    float inputSweepFraction,
    float inputDistancePointer,
    WoWCollision::GroundedDriverSelectedPlaneBranchGateTrace* outTrace)
{
    if (!requestedMove || !selectedPlaneNormal) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneBranchGateTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneBranchGateKind::ExitWithoutMutation);
    }

    const WoWCollision::GroundedDriverSelectedPlaneBranchGateTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneBranchGate(
            walkableSelectedContact != 0u,
            gateReturnCode,
            *requestedMove,
            *selectedPlaneNormal,
            verticalLimit,
            remainingDistanceBefore,
            sweepDistanceBefore,
            sweepDistanceAfter,
            inputSweepFraction,
            inputDistancePointer);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.BranchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank(
    uint32_t selectedIndex,
    uint32_t selectedCount,
    const G3D::Vector3* inputContactNormal,
    const G3D::Vector3* selectedRecordNormal,
    const G3D::Vector3* inputPackedPairVector,
    const G3D::Vector3* rerankedPackedPairVector,
    uint32_t secondRerankSucceeded,
    WoWCollision::GroundedDriverSelectedPlaneFollowupRerankTrace* outTrace)
{
    if (!inputContactNormal || !selectedRecordNormal || !inputPackedPairVector || !rerankedPackedPairVector) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneFollowupRerankTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneFollowupRerankKind::ExitWithoutSelection);
    }

    const WoWCollision::GroundedDriverSelectedPlaneFollowupRerankTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneFollowupRerank(
            selectedIndex,
            selectedCount,
            *inputContactNormal,
            *selectedRecordNormal,
            *inputPackedPairVector,
            *rerankedPackedPairVector,
            secondRerankSucceeded != 0u);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailPreThirdPassSetup(
    const G3D::Vector3* inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    float tailTransformScalar,
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float selectedContactNormalZ,
    uint32_t movementFlags,
    float boundingRadius,
    uint32_t invokeVerticalCorrection,
    WoWCollision::GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace* outTrace)
{
    if (!inputPackedPairVector || !requestedMove || !selectedPlaneNormal) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace{};
        }
        return static_cast<uint32_t>(
            WoWCollision::GroundedDriverSelectedPlaneTailPreThirdPassSetupKind::UseHorizontalFallbackInputs);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailPreThirdPassSetupTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailPreThirdPassSetup(
            *inputPackedPairVector,
            inputPositionZ,
            followupScalar,
            scalarFloor,
            tailTransformScalar,
            *requestedMove,
            *selectedPlaneNormal,
            selectedContactNormalZ,
            movementFlags,
            boundingRadius,
            invokeVerticalCorrection != 0u);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
    const G3D::Vector3* inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    float tailTransformScalar,
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float selectedContactNormalZ,
    uint32_t movementFlags,
    float boundingRadius,
    uint32_t invokeVerticalCorrection,
    uint32_t thirdPassRerankSucceeded,
    float verticalLimit,
    WoWCollision::GroundedDriverSelectedPlaneTailProjectedBlendTrace* outTrace)
{
    if (!inputPackedPairVector || !requestedMove || !selectedPlaneNormal) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailProjectedBlendTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailProjectedBlendKind::ExitWithoutSelection);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailProjectedBlendTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailProjectedBlendTransaction(
            *inputPackedPairVector,
            inputPositionZ,
            followupScalar,
            scalarFloor,
            tailTransformScalar,
            *requestedMove,
            *selectedPlaneNormal,
            selectedContactNormalZ,
            movementFlags,
            boundingRadius,
            invokeVerticalCorrection != 0u,
            thirdPassRerankSucceeded != 0u,
            verticalLimit);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailWriteback(
    const G3D::Vector3* inputPosition,
    const G3D::Vector3* inputPackedPairVector,
    float followupScalar,
    float scalarFloor,
    float inputSelectedContactNormalZ,
    uint32_t checkWalkableAccepted,
    uint32_t projectedTailRerankSucceeded,
    const G3D::Vector3* projectedTailMove,
    float projectedTailResolved2D,
    WoWCollision::GroundedDriverSelectedPlaneTailWritebackTrace* outTrace)
{
    if (!inputPosition || !inputPackedPairVector || !projectedTailMove) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailWritebackTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailWritebackKind::ThirdPassOnly);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailWritebackTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailWriteback(
            *inputPosition,
            *inputPackedPairVector,
            followupScalar,
            scalarFloor,
            inputSelectedContactNormalZ,
            checkWalkableAccepted != 0u,
            projectedTailRerankSucceeded != 0u,
            *projectedTailMove,
            projectedTailResolved2D);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailChooserContract(
    const G3D::Vector3* chooserInputPackedPairVector,
    const G3D::Vector3* chooserInputProjectedMove,
    float chooserInputScalar,
    uint32_t finalSelectedIndex,
    uint32_t finalSelectedCount,
    float finalSelectedNormalZ,
    uint32_t chooserAcceptedSelectedPlane,
    uint32_t movementFlags,
    const G3D::Vector3* chooserOutputProjectedMove,
    WoWCollision::GroundedDriverSelectedPlaneTailChooserContractTrace* outTrace)
{
    if (!chooserInputPackedPairVector || !chooserInputProjectedMove || !chooserOutputProjectedMove) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailChooserContractTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailChooserContractKind::Return2SelectedPlane);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailChooserContractTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailChooserContract(
            *chooserInputPackedPairVector,
            *chooserInputProjectedMove,
            chooserInputScalar,
            finalSelectedIndex,
            finalSelectedCount,
            finalSelectedNormalZ,
            chooserAcceptedSelectedPlane != 0u,
            movementFlags,
            *chooserOutputProjectedMove);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) void CaptureWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot(
    const G3D::Vector3* field44Vector,
    const G3D::Vector3* field50Vector,
    uint32_t field78,
    const G3D::Vector3* field5cVector,
    float field68,
    float field6c,
    uint32_t field40Flags,
    float field84,
    WoWCollision::GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace* outTrace)
{
    if (!field44Vector || !field50Vector || !field5cVector || !outTrace) {
        return;
    }

    *outTrace = WoWCollision::CaptureGroundedDriverSelectedPlaneTailProbeStateSnapshot(
        *field44Vector,
        *field50Vector,
        field78,
        *field5cVector,
        field68,
        field6c,
        field40Flags,
        field84);
}

__declspec(dllexport) void RestoreWoWGroundedDriverSelectedPlaneTailProbeStateSnapshot(
    const WoWCollision::GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace* snapshot,
    WoWCollision::GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace* outTrace)
{
    if (!snapshot || !outTrace) {
        return;
    }

    *outTrace = WoWCollision::RestoreGroundedDriverSelectedPlaneTailProbeStateSnapshot(*snapshot);
}

__declspec(dllexport) int32_t EvaluateWoWGroundedDriverSelectedPlaneTailElapsedMilliseconds(
    float elapsedSeconds,
    WoWCollision::GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace* outTrace)
{
    const WoWCollision::GroundedDriverSelectedPlaneTailElapsedMillisecondsTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailElapsedMilliseconds(elapsedSeconds);
    if (outTrace) {
        *outTrace = trace;
    }

    return trace.RoundedMilliseconds;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailEntrySetup(
    uint32_t inputWindowMilliseconds,
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* currentPosition,
    const G3D::Vector3* field44Vector,
    const G3D::Vector3* field50Vector,
    uint32_t field78,
    const G3D::Vector3* field5cVector,
    float field68,
    float field6c,
    uint32_t field40Flags,
    float field84,
    WoWCollision::GroundedDriverSelectedPlaneTailEntrySetupTrace* outTrace)
{
    if (!requestedMove || !currentPosition || !field44Vector || !field50Vector || !field5cVector) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailEntrySetupTrace{};
        }

        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailEntrySetupKind::ExitWithoutProbe);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailEntrySetupTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailEntrySetup(
            inputWindowMilliseconds,
            *requestedMove,
            *currentPosition,
            *field44Vector,
            *field50Vector,
            field78,
            *field5cVector,
            field68,
            field6c,
            field40Flags,
            field84);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailPostForwarding(
    int32_t pairForwardReturnCode,
    uint32_t directStateBit,
    uint32_t alternateUnitZStateBit,
    float windowSpanScalar,
    float windowStartScalar,
    const G3D::Vector3* moveVector,
    float horizontalReferenceMagnitude,
    uint32_t movementFlags,
    float verticalSpeed,
    float horizontalSpeedScale,
    float referenceZ,
    float positionZ,
    const G3D::Vector3* startPosition,
    float elapsedScalar,
    float facing,
    float pitch,
    const G3D::Vector3* cachedPosition,
    float cachedFacing,
    float cachedPitch,
    uint32_t cachedMoveTimestamp,
    float cachedScalar84,
    float recomputedScalar84,
    WoWCollision::GroundedDriverSelectedPlaneTailPostForwardingTrace* outTrace)
{
    if (!moveVector || !startPosition || !cachedPosition) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailPostForwardingTrace{};
        }

        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailPostForwardingKind::Return2LateBranch);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailPostForwardingTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailPostForwarding(
            pairForwardReturnCode,
            directStateBit != 0u,
            alternateUnitZStateBit != 0u,
            windowSpanScalar,
            windowStartScalar,
            *moveVector,
            horizontalReferenceMagnitude,
            movementFlags,
            verticalSpeed,
            horizontalSpeedScale,
            referenceZ,
            positionZ,
            *startPosition,
            elapsedScalar,
            facing,
            pitch,
            *cachedPosition,
            cachedFacing,
            cachedPitch,
            cachedMoveTimestamp,
            cachedScalar84,
            recomputedScalar84);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) int32_t EvaluateWoWGroundedDriverSelectedPlaneTailReturn2LateBranch(
    float consumedWindowSeconds,
    int32_t field58,
    int32_t field78,
    WoWCollision::GroundedDriverSelectedPlaneTailReturn2LateBranchTrace* outTrace)
{
    const WoWCollision::GroundedDriverSelectedPlaneTailReturn2LateBranchTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailReturn2LateBranch(
            consumedWindowSeconds,
            field58,
            field78);
    if (outTrace) {
        *outTrace = trace;
    }

    return trace.ElapsedMillisecondsTrace.RoundedMilliseconds;
}

__declspec(dllexport) int32_t EvaluateWoWGroundedDriverSelectedPlaneTailLateNotifier(
    int32_t roundedMilliseconds,
    uint32_t alternateUnitZStateBit,
    int32_t field78,
    uint32_t notifyRequested,
    int32_t alternateWindowCommitBase,
    uint32_t sidecarStatePresent,
    uint32_t bit20InitiallySet,
    uint32_t commitGuardPassed,
    uint32_t bit20StillSet,
    float fieldA0,
    uint32_t lowNibbleFlags,
    const WoWCollision::GroundedDriverSelectedPlaneTailProbeStateSnapshotTrace* snapshot,
    uint32_t rerouteLoopUsed,
    WoWCollision::GroundedDriverSelectedPlaneTailLateNotifierTrace* outTrace)
{
    if (!snapshot) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailLateNotifierTrace{};
        }

        return roundedMilliseconds;
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailLateNotifierTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailLateNotifier(
            roundedMilliseconds,
            alternateUnitZStateBit != 0u,
            field78,
            notifyRequested != 0u,
            alternateWindowCommitBase,
            sidecarStatePresent != 0u,
            bit20InitiallySet != 0u,
            commitGuardPassed != 0u,
            bit20StillSet != 0u,
            fieldA0,
            lowNibbleFlags,
            *snapshot,
            rerouteLoopUsed != 0u);
    if (outTrace) {
        *outTrace = trace;
    }

    return trace.RoundedMilliseconds;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
    uint32_t attemptIndex,
    const G3D::Vector3* normalizedInputDirection,
    float remainingMagnitude,
    const G3D::Vector3* lateralOffset,
    const G3D::Vector3* originalPosition,
    const G3D::Vector3* currentPosition,
    float originalHorizontalMagnitude,
    float originalVerticalMagnitude,
    float previousField68,
    float previousField6c,
    float previousField84,
    WoWCollision::GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace* outTrace)
{
    if (!normalizedInputDirection || !lateralOffset || !originalPosition || !currentPosition) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailProbeRerouteCandidateKind::AcceptCandidate);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailProbeRerouteCandidateTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailProbeRerouteCandidate(
            attemptIndex,
            *normalizedInputDirection,
            remainingMagnitude,
            *lateralOffset,
            *originalPosition,
            *currentPosition,
            originalHorizontalMagnitude,
            originalVerticalMagnitude,
            previousField68,
            previousField6c,
            previousField84);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailProbeVerticalFallback(
    const G3D::Vector3* normalizedInputDirection,
    float currentHorizontalMagnitude,
    float remainingMagnitude,
    uint32_t verticalFallbackAlreadyUsed,
    WoWCollision::GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace* outTrace)
{
    if (!normalizedInputDirection) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailProbeVerticalFallbackKind::RejectNoFallback);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailProbeVerticalFallbackTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailProbeVerticalFallback(
            *normalizedInputDirection,
            currentHorizontalMagnitude,
            remainingMagnitude,
            verticalFallbackAlreadyUsed != 0u);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailRerouteLoopController(
    uint32_t attemptIndex,
    uint32_t incrementAttemptBeforeProbe,
    const G3D::Vector3* normalizedInputDirection,
    float remainingMagnitude,
    float currentHorizontalMagnitude,
    const G3D::Vector3* lateralOffset,
    const G3D::Vector3* originalPosition,
    const G3D::Vector3* currentPosition,
    float originalHorizontalMagnitude,
    float originalVerticalMagnitude,
    float previousField68,
    float previousField6c,
    float previousField84,
    uint32_t verticalFallbackAlreadyUsed,
    WoWCollision::GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace* outTrace)
{
    if (!normalizedInputDirection || !lateralOffset || !originalPosition || !currentPosition) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace{};
        }

        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailRerouteLoopControllerKind::ResetState);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailRerouteLoopControllerTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailRerouteLoopController(
            attemptIndex,
            incrementAttemptBeforeProbe != 0u,
            *normalizedInputDirection,
            remainingMagnitude,
            currentHorizontalMagnitude,
            *lateralOffset,
            *originalPosition,
            *currentPosition,
            originalHorizontalMagnitude,
            originalVerticalMagnitude,
            previousField68,
            previousField6c,
            previousField84,
            verticalFallbackAlreadyUsed != 0u);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailChooserProbe(
    const G3D::Vector3* inputPackedPairVector,
    const G3D::Vector3* inputProjectedMove,
    float chooserInputScalar,
    const G3D::Vector3* probePosition,
    float collisionRadius,
    WoWCollision::GroundedDriverSelectedPlaneTailChooserProbeTrace* outTrace)
{
    if (!inputPackedPairVector || !inputProjectedMove || !probePosition) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailChooserProbeTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailChooserProbeKind::RejectHorizontal);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailChooserProbeTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailChooserProbe(
            *inputPackedPairVector,
            *inputProjectedMove,
            chooserInputScalar,
            *probePosition,
            collisionRadius);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneThirdPassSetup(
    const G3D::Vector3* inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    uint32_t thirdPassRerankSucceeded,
    WoWCollision::GroundedDriverSelectedPlaneThirdPassSetupTrace* outTrace)
{
    if (!inputPackedPairVector) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneThirdPassSetupTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneThirdPassSetupKind::ExitWithoutSelection);
    }

    const WoWCollision::GroundedDriverSelectedPlaneThirdPassSetupTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneThirdPassSetup(
            *inputPackedPairVector,
            inputPositionZ,
            followupScalar,
            scalarFloor,
            thirdPassRerankSucceeded != 0u);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneBlendCorrection(
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float verticalLimit,
    const G3D::Vector3* horizontalProjectedMove,
    float horizontalResolved2D,
    WoWCollision::GroundedDriverSelectedPlaneBlendCorrectionTrace* outTrace)
{
    if (!requestedMove || !selectedPlaneNormal || !horizontalProjectedMove) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneBlendCorrectionTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneBlendCorrectionKind::UseHorizontalFallback);
    }

    const WoWCollision::GroundedDriverSelectedPlaneBlendCorrectionTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneBlendCorrection(
            *requestedMove,
            *selectedPlaneNormal,
            verticalLimit,
            *horizontalProjectedMove,
            horizontalResolved2D);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlanePostFastReturnTailTransaction(
    const G3D::Vector3* inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    uint32_t thirdPassRerankSucceeded,
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float verticalLimit,
    const G3D::Vector3* horizontalProjectedMove,
    float horizontalResolved2D,
    WoWCollision::GroundedDriverSelectedPlanePostFastReturnTailTrace* outTrace)
{
    if (!inputPackedPairVector || !requestedMove || !selectedPlaneNormal || !horizontalProjectedMove) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlanePostFastReturnTailTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlanePostFastReturnTailKind::ExitWithoutSelection);
    }

    const WoWCollision::GroundedDriverSelectedPlanePostFastReturnTailTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlanePostFastReturnTailTransaction(
            *inputPackedPairVector,
            inputPositionZ,
            followupScalar,
            scalarFloor,
            thirdPassRerankSucceeded != 0u,
            *requestedMove,
            *selectedPlaneNormal,
            verticalLimit,
            *horizontalProjectedMove,
            horizontalResolved2D);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPlaneTailReturnDispatch(
    const G3D::Vector3* inputPackedPairVector,
    float inputPositionZ,
    float followupScalar,
    float scalarFloor,
    uint32_t thirdPassRerankSucceeded,
    const G3D::Vector3* requestedMove,
    const G3D::Vector3* selectedPlaneNormal,
    float verticalLimit,
    const G3D::Vector3* horizontalProjectedMove,
    float horizontalResolved2D,
    uint32_t tailRerankSucceeded,
    uint32_t finalSelectedIndex,
    uint32_t finalSelectedCount,
    float finalSelectedNormalZ,
    uint32_t chooserAcceptedSelectedPlane,
    uint32_t movementFlags,
    WoWCollision::GroundedDriverSelectedPlaneTailReturnDispatchTrace* outTrace)
{
    if (!inputPackedPairVector || !requestedMove || !selectedPlaneNormal || !horizontalProjectedMove) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPlaneTailReturnDispatchTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverSelectedPlaneTailReturnDispatchKind::Return0Exit);
    }

    const WoWCollision::GroundedDriverSelectedPlaneTailReturnDispatchTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPlaneTailReturnDispatch(
            *inputPackedPairVector,
            inputPositionZ,
            followupScalar,
            scalarFloor,
            thirdPassRerankSucceeded != 0u,
            *requestedMove,
            *selectedPlaneNormal,
            verticalLimit,
            *horizontalProjectedMove,
            horizontalResolved2D,
            tailRerankSucceeded != 0u,
            finalSelectedIndex,
            finalSelectedCount,
            finalSelectedNormalZ,
            chooserAcceptedSelectedPlane != 0u,
            movementFlags);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverSelectedPairCommitTail(
    uint32_t selectedIndex,
    uint32_t selectedCount,
    uint32_t consumedSelectedState,
    uint32_t snapshotBeforeCommitState,
    uint32_t movementFlags,
    const WoWCollision::SelectorPair* cachedPair,
    const G3D::Vector3* currentPosition,
    float currentFacing,
    float currentPitch,
    const G3D::Vector3* cachedPosition,
    float cachedFacing,
    float cachedPitch,
    uint32_t cachedMoveTimestamp,
    uint32_t inputFallTime,
    float inputFallStartZ,
    float inputVerticalSpeed,
    WoWCollision::GroundedDriverSelectedPairCommitTailTrace* outTrace)
{
    if (!cachedPair || !currentPosition || !cachedPosition) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPairCommitTailTrace{};
        }
        return 0u;
    }

    const WoWCollision::GroundedDriverSelectedPairCommitTailTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPairCommitTail(
            selectedIndex,
            selectedCount,
            consumedSelectedState != 0u,
            snapshotBeforeCommitState != 0u,
            movementFlags,
            *cachedPair,
            *currentPosition,
            currentFacing,
            currentPitch,
            *cachedPosition,
            cachedFacing,
            cachedPitch,
            cachedMoveTimestamp,
            inputFallTime,
            inputFallStartZ,
            inputVerticalSpeed);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

__declspec(dllexport) int32_t EvaluateWoWGroundedDriverSelectedPairCommitGuard(
    const WoWCollision::SelectorPair* incomingPair,
    const WoWCollision::SelectorPair* storedPair,
    uint32_t probeRejectOnStoredPairUnload,
    uint32_t contextMatchesGlobal,
    uint32_t hasAttachedPointer,
    uint32_t attachedBit4Set,
    int32_t opaqueConsumerReturnValue,
    WoWCollision::GroundedDriverSelectedPairCommitGuardTrace* outTrace)
{
    if (!incomingPair || !storedPair) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPairCommitGuardTrace{};
        }
        return 0;
    }

    const WoWCollision::GroundedDriverSelectedPairCommitGuardTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPairCommitGuard(
            *incomingPair,
            *storedPair,
            probeRejectOnStoredPairUnload != 0u,
            contextMatchesGlobal != 0u,
            hasAttachedPointer != 0u,
            attachedBit4Set != 0u,
            opaqueConsumerReturnValue);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.ReturnValue;
}

__declspec(dllexport) int32_t EvaluateWoWGroundedDriverSelectedPairCommitBody(
    const WoWCollision::SelectorPair* incomingPair,
    const WoWCollision::SelectorPair* storedPair,
    uint32_t incomingPairValidatorAccepted,
    uint32_t hasTransformConsumer,
    float storedPhaseScalar,
    float incomingPhaseScalar,
    WoWCollision::GroundedDriverSelectedPairCommitBodyTrace* outTrace)
{
    if (!incomingPair || !storedPair) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverSelectedPairCommitBodyTrace{};
        }
        return 0;
    }

    const WoWCollision::GroundedDriverSelectedPairCommitBodyTrace trace =
        WoWCollision::EvaluateGroundedDriverSelectedPairCommitBody(
            *incomingPair,
            *storedPair,
            incomingPairValidatorAccepted != 0u,
            hasTransformConsumer != 0u,
            storedPhaseScalar,
            incomingPhaseScalar);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.ReturnValue;
}

__declspec(dllexport) uint32_t EvaluateWoWGroundedDriverHoverRerankDispatch(
    uint32_t firstRerankSucceeded,
    uint32_t selectedIndex,
    uint32_t selectedCount,
    uint32_t useStandardWalkableThreshold,
    float selectedNormalZ,
    const WoWCollision::SelectorPair* selectedPair,
    float inputWindowSpanScalar,
    float followupScalarCandidate,
    uint32_t secondRerankSucceeded,
    uint32_t movementFlags,
    float positionZ,
    uint32_t inputFallTime,
    float inputFallStartZ,
    float inputVerticalSpeed,
    WoWCollision::GroundedDriverHoverRerankTrace* outTrace)
{
    if (!selectedPair) {
        if (outTrace) {
            *outTrace = WoWCollision::GroundedDriverHoverRerankTrace{};
        }
        return static_cast<uint32_t>(WoWCollision::GroundedDriverHoverRerankDispatchKind::ReturnWithoutCommit);
    }

    const WoWCollision::GroundedDriverHoverRerankTrace trace =
        WoWCollision::EvaluateGroundedDriverHoverRerankDispatch(
            firstRerankSucceeded != 0u,
            selectedIndex,
            selectedCount,
            useStandardWalkableThreshold != 0u,
            selectedNormalZ,
            *selectedPair,
            inputWindowSpanScalar,
            followupScalarCandidate,
            secondRerankSucceeded != 0u,
            movementFlags,
            positionZ,
            inputFallTime,
            inputFallStartZ,
            inputVerticalSpeed);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.DispatchKind;
}

}
