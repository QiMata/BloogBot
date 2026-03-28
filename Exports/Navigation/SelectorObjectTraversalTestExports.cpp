#include "SelectorObjectTraversal.h"

struct ExportSelectorBvhRecursionChildOutcome
{
    uint32_t result;
    uint32_t pendingCountDelta;
    uint32_t acceptedCountDelta;
    uint32_t overflowFlagsDelta;
};

struct ExportSelectorBvhRecursionStepTrace
{
    uint32_t visitLow;
    uint32_t visitHigh;
    uint32_t enteredLowChild;
    uint32_t enteredHighChild;
    uint32_t resultBefore;
    uint32_t resultAfterLow;
    uint32_t resultAfterHigh;
    uint32_t pendingCountBefore;
    uint32_t pendingCountAfterLow;
    uint32_t pendingCountAfterHigh;
    uint32_t acceptedCountBefore;
    uint32_t acceptedCountAfterLow;
    uint32_t acceptedCountAfterHigh;
    uint32_t overflowFlagsBefore;
    uint32_t overflowFlagsAfterLow;
    uint32_t overflowFlagsAfterHigh;
};

struct ExportSelectorBvhRecursiveTraversalTrace
{
    uint32_t visitedNodeCount;
    uint32_t leafNodeCount;
    uint32_t leafCullRejectedCount;
    uint32_t lowRecursionCount;
    uint32_t highRecursionCount;
    uint32_t leafInvocationCount;
    uint32_t resultAfterTraversal;
    uint32_t pendingCountAfterTraversal;
    uint32_t acceptedCountAfterTraversal;
    uint32_t overflowFlagsAfterTraversal;
    uint32_t visitedNodeIndices[16];
    uint16_t visitedLeafTriangleIds[32];
};

extern "C" {

__declspec(dllexport) uint32_t EvaluateWoWSelectorPlaneLeafQueueMutation(
    uint32_t triangleIndex,
    uint32_t stateMaskByte,
    uint32_t firstOutcode,
    uint32_t secondOutcode,
    uint32_t thirdOutcode,
    uint32_t* ioOverflowFlags,
    uint16_t* pendingIds,
    int pendingIdCapacity,
    uint32_t* ioPendingCount,
    uint16_t* acceptedIds,
    int acceptedIdCapacity,
    uint32_t* ioAcceptedCount,
    uint8_t* stateBytes,
    int stateByteCount,
    WoWCollision::SelectorLeafQueueMutationTrace* outTrace)
{
    if (!ioOverflowFlags || !ioPendingCount || !ioAcceptedCount) {
        if (outTrace) {
            *outTrace = WoWCollision::SelectorLeafQueueMutationTrace{};
        }
        return 0u;
    }

    return WoWCollision::EvaluateSelectorPlaneLeafQueueMutation(
        triangleIndex,
        stateMaskByte,
        firstOutcode,
        secondOutcode,
        thirdOutcode,
        *ioOverflowFlags,
        pendingIds,
        pendingIdCapacity < 0 ? 0u : static_cast<uint32_t>(pendingIdCapacity),
        *ioPendingCount,
        acceptedIds,
        acceptedIdCapacity < 0 ? 0u : static_cast<uint32_t>(acceptedIdCapacity),
        *ioAcceptedCount,
        stateBytes,
        stateByteCount < 0 ? 0u : static_cast<uint32_t>(stateByteCount),
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWTriangleLocalBoundsLeafQueueMutation(
    uint32_t triangleIndex,
    uint32_t stateMaskByte,
    const G3D::Vector3* localBoundsMin,
    const G3D::Vector3* localBoundsMax,
    const G3D::Vector3* point0,
    const G3D::Vector3* point1,
    const G3D::Vector3* point2,
    uint32_t* ioOverflowFlags,
    uint16_t* pendingIds,
    int pendingIdCapacity,
    uint32_t* ioPendingCount,
    uint16_t* acceptedIds,
    int acceptedIdCapacity,
    uint32_t* ioAcceptedCount,
    uint8_t* stateBytes,
    int stateByteCount,
    WoWCollision::SelectorLeafQueueMutationTrace* outTrace)
{
    if (!localBoundsMin || !localBoundsMax || !point0 || !point1 || !point2 ||
        !ioOverflowFlags || !ioPendingCount || !ioAcceptedCount) {
        if (outTrace) {
            *outTrace = WoWCollision::SelectorLeafQueueMutationTrace{};
        }
        return 0u;
    }

    return WoWCollision::EvaluateTriangleLocalBoundsLeafQueueMutation(
        triangleIndex,
        stateMaskByte,
        *localBoundsMin,
        *localBoundsMax,
        *point0,
        *point1,
        *point2,
        *ioOverflowFlags,
        pendingIds,
        pendingIdCapacity < 0 ? 0u : static_cast<uint32_t>(pendingIdCapacity),
        *ioPendingCount,
        acceptedIds,
        acceptedIdCapacity < 0 ? 0u : static_cast<uint32_t>(acceptedIdCapacity),
        *ioAcceptedCount,
        stateBytes,
        stateByteCount < 0 ? 0u : static_cast<uint32_t>(stateByteCount),
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorBvhRecursionStep(
    const WoWCollision::SelectorBvhChildTraversal* traversal,
    const ExportSelectorBvhRecursionChildOutcome* lowChildOutcome,
    const ExportSelectorBvhRecursionChildOutcome* highChildOutcome,
    uint32_t inputOverflowFlags,
    uint32_t inputPendingCount,
    uint32_t inputAcceptedCount,
    uint32_t inputResult,
    ExportSelectorBvhRecursionStepTrace* outTrace)
{
    if (!traversal || !lowChildOutcome || !highChildOutcome) {
        if (outTrace) {
            *outTrace = ExportSelectorBvhRecursionStepTrace{};
        }
        return 0u;
    }

    WoWCollision::SelectorBvhRecursionChildOutcome nativeLowOutcome{};
    nativeLowOutcome.result = lowChildOutcome->result;
    nativeLowOutcome.pendingCountDelta = lowChildOutcome->pendingCountDelta;
    nativeLowOutcome.acceptedCountDelta = lowChildOutcome->acceptedCountDelta;
    nativeLowOutcome.overflowFlagsDelta = lowChildOutcome->overflowFlagsDelta;

    WoWCollision::SelectorBvhRecursionChildOutcome nativeHighOutcome{};
    nativeHighOutcome.result = highChildOutcome->result;
    nativeHighOutcome.pendingCountDelta = highChildOutcome->pendingCountDelta;
    nativeHighOutcome.acceptedCountDelta = highChildOutcome->acceptedCountDelta;
    nativeHighOutcome.overflowFlagsDelta = highChildOutcome->overflowFlagsDelta;

    WoWCollision::SelectorBvhRecursionStepTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorBvhRecursionStep(
        *traversal,
        nativeLowOutcome,
        nativeHighOutcome,
        inputOverflowFlags,
        inputPendingCount,
        inputAcceptedCount,
        inputResult,
        &nativeTrace);

    if (outTrace) {
        outTrace->visitLow = nativeTrace.visitLow;
        outTrace->visitHigh = nativeTrace.visitHigh;
        outTrace->enteredLowChild = nativeTrace.enteredLowChild;
        outTrace->enteredHighChild = nativeTrace.enteredHighChild;
        outTrace->resultBefore = nativeTrace.resultBefore;
        outTrace->resultAfterLow = nativeTrace.resultAfterLow;
        outTrace->resultAfterHigh = nativeTrace.resultAfterHigh;
        outTrace->pendingCountBefore = nativeTrace.pendingCountBefore;
        outTrace->pendingCountAfterLow = nativeTrace.pendingCountAfterLow;
        outTrace->pendingCountAfterHigh = nativeTrace.pendingCountAfterHigh;
        outTrace->acceptedCountBefore = nativeTrace.acceptedCountBefore;
        outTrace->acceptedCountAfterLow = nativeTrace.acceptedCountAfterLow;
        outTrace->acceptedCountAfterHigh = nativeTrace.acceptedCountAfterHigh;
        outTrace->overflowFlagsBefore = nativeTrace.overflowFlagsBefore;
        outTrace->overflowFlagsAfterLow = nativeTrace.overflowFlagsAfterLow;
        outTrace->overflowFlagsAfterHigh = nativeTrace.overflowFlagsAfterHigh;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorBvhRecursiveTraversal(
    const WoWCollision::SelectorBvhNodeRecord* nodes,
    int nodeCount,
    uint32_t rootNodeIndex,
    const G3D::Vector3* boundsMin,
    const G3D::Vector3* boundsMax,
    uint32_t stateMaskByte,
    const uint8_t* leafCullStates,
    int leafCullStateCount,
    const uint16_t* leafTriangleIds,
    int leafTriangleIdCount,
    const uint8_t* predicateRejectedStates,
    int predicateRejectedStateCount,
    uint32_t* ioOverflowFlags,
    uint16_t* pendingIds,
    int pendingIdCapacity,
    uint32_t* ioPendingCount,
    uint16_t* acceptedIds,
    int acceptedIdCapacity,
    uint32_t* ioAcceptedCount,
    uint8_t* stateBytes,
    int stateByteCount,
    ExportSelectorBvhRecursiveTraversalTrace* outTrace)
{
    if (!nodes || !boundsMin || !boundsMax || !ioOverflowFlags || !ioPendingCount || !ioAcceptedCount || !stateBytes) {
        if (outTrace) {
            *outTrace = ExportSelectorBvhRecursiveTraversalTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorBvhRecursiveTraversalTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorBvhRecursiveTraversal(
        nodes,
        nodeCount < 0 ? 0u : static_cast<uint32_t>(nodeCount),
        rootNodeIndex,
        *boundsMin,
        *boundsMax,
        stateMaskByte,
        leafCullStates,
        leafCullStateCount < 0 ? 0u : static_cast<uint32_t>(leafCullStateCount),
        leafTriangleIds,
        leafTriangleIdCount < 0 ? 0u : static_cast<uint32_t>(leafTriangleIdCount),
        predicateRejectedStates,
        predicateRejectedStateCount < 0 ? 0u : static_cast<uint32_t>(predicateRejectedStateCount),
        *ioOverflowFlags,
        pendingIds,
        pendingIdCapacity < 0 ? 0u : static_cast<uint32_t>(pendingIdCapacity),
        *ioPendingCount,
        acceptedIds,
        acceptedIdCapacity < 0 ? 0u : static_cast<uint32_t>(acceptedIdCapacity),
        *ioAcceptedCount,
        stateBytes,
        stateByteCount < 0 ? 0u : static_cast<uint32_t>(stateByteCount),
        &nativeTrace);

    if (outTrace) {
        outTrace->visitedNodeCount = nativeTrace.visitedNodeCount;
        outTrace->leafNodeCount = nativeTrace.leafNodeCount;
        outTrace->leafCullRejectedCount = nativeTrace.leafCullRejectedCount;
        outTrace->lowRecursionCount = nativeTrace.lowRecursionCount;
        outTrace->highRecursionCount = nativeTrace.highRecursionCount;
        outTrace->leafInvocationCount = nativeTrace.leafInvocationCount;
        outTrace->resultAfterTraversal = nativeTrace.resultAfterTraversal;
        outTrace->pendingCountAfterTraversal = nativeTrace.pendingCountAfterTraversal;
        outTrace->acceptedCountAfterTraversal = nativeTrace.acceptedCountAfterTraversal;
        outTrace->overflowFlagsAfterTraversal = nativeTrace.overflowFlagsAfterTraversal;
        for (size_t index = 0; index < nativeTrace.visitedNodeIndices.size(); ++index) {
            outTrace->visitedNodeIndices[index] = nativeTrace.visitedNodeIndices[index];
        }

        for (size_t index = 0; index < nativeTrace.visitedLeafTriangleIds.size(); ++index) {
            outTrace->visitedLeafTriangleIds[index] = nativeTrace.visitedLeafTriangleIds[index];
        }
    }

    return result;
}

}
