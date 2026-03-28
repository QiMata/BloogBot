#include "SelectorObjectTraversal.h"

namespace
{
    struct SelectorBvhRecursiveTraversalContext
    {
        const WoWCollision::SelectorBvhNodeRecord* nodes = nullptr;
        uint32_t nodeCount = 0u;
        uint32_t stateMaskByte = 0u;
        const uint8_t* leafCullStates = nullptr;
        uint32_t leafCullStateCount = 0u;
        const uint16_t* leafTriangleIds = nullptr;
        uint32_t leafTriangleIdCount = 0u;
        const uint8_t* predicateRejectedStates = nullptr;
        uint32_t predicateRejectedStateCount = 0u;
        uint32_t* ioOverflowFlags = nullptr;
        uint16_t* pendingIds = nullptr;
        uint32_t pendingIdCapacity = 0u;
        uint32_t* ioPendingCount = nullptr;
        uint16_t* acceptedIds = nullptr;
        uint32_t acceptedIdCapacity = 0u;
        uint32_t* ioAcceptedCount = nullptr;
        uint8_t* stateBytes = nullptr;
        uint32_t stateByteCount = 0u;
    };

    constexpr uint32_t kSelectorBvhVisitedNodeTraceCapacity = 16u;
    constexpr uint32_t kSelectorBvhVisitedLeafTraceCapacity = 32u;

    void RecordVisitedNode(WoWCollision::SelectorBvhRecursiveTraversalTrace& trace, uint32_t nodeIndex)
    {
        if (trace.visitedNodeCount < kSelectorBvhVisitedNodeTraceCapacity) {
            trace.visitedNodeIndices[trace.visitedNodeCount] = nodeIndex;
        }

        ++trace.visitedNodeCount;
    }

    void RecordVisitedLeafTriangle(WoWCollision::SelectorBvhRecursiveTraversalTrace& trace, uint16_t triangleIndex)
    {
        if (trace.leafInvocationCount < kSelectorBvhVisitedLeafTraceCapacity) {
            trace.visitedLeafTriangleIds[trace.leafInvocationCount] = triangleIndex;
        }
    }

    bool IsLeafCullRejected(const SelectorBvhRecursiveTraversalContext& context, uint32_t nodeIndex)
    {
        return context.leafCullStates != nullptr &&
               nodeIndex < context.leafCullStateCount &&
               context.leafCullStates[nodeIndex] != 0u;
    }

    bool IsPredicateRejected(const SelectorBvhRecursiveTraversalContext& context, uint32_t triangleIndex)
    {
        return context.predicateRejectedStates != nullptr &&
               triangleIndex < context.predicateRejectedStateCount &&
               context.predicateRejectedStates[triangleIndex] != 0u;
    }

    uint32_t EvaluateSelectorBvhRecursiveTraversalInternal(
        const SelectorBvhRecursiveTraversalContext& context,
        uint32_t nodeIndex,
        const G3D::Vector3& boundsMin,
        const G3D::Vector3& boundsMax,
        WoWCollision::SelectorBvhRecursiveTraversalTrace& trace)
    {
        if (context.nodes == nullptr || nodeIndex >= context.nodeCount) {
            return 0u;
        }

        RecordVisitedNode(trace, nodeIndex);

        const WoWCollision::SelectorBvhNodeRecord& node = context.nodes[nodeIndex];
        if ((node.controlWord & 0x4u) != 0u) {
            ++trace.leafNodeCount;
            if (IsLeafCullRejected(context, nodeIndex)) {
                ++trace.leafCullRejectedCount;
                return 0u;
            }

            uint32_t result = 0u;
            for (uint32_t leafOffset = 0u; leafOffset < node.leafTriangleCount; ++leafOffset) {
                const uint32_t leafTriangleIndex = node.leafTriangleStartIndex + leafOffset;
                if (leafTriangleIndex >= context.leafTriangleIdCount) {
                    break;
                }

                const uint16_t triangleId = context.leafTriangleIds[leafTriangleIndex];
                RecordVisitedLeafTriangle(trace, triangleId);
                ++trace.leafInvocationCount;

                result |= WoWCollision::EvaluateSelectorLeafQueueMutation(
                    triangleId,
                    context.stateMaskByte,
                    IsPredicateRejected(context, triangleId),
                    *context.ioOverflowFlags,
                    context.pendingIds,
                    context.pendingIdCapacity,
                    *context.ioPendingCount,
                    context.acceptedIds,
                    context.acceptedIdCapacity,
                    *context.ioAcceptedCount,
                    context.stateBytes,
                    context.stateByteCount,
                    nullptr);
            }

            return result;
        }

        WoWCollision::SelectorBvhChildTraversal traversal{};
        if (!WoWCollision::BuildSelectorBvhChildTraversal(node, boundsMin, boundsMax, traversal)) {
            return 0u;
        }

        uint32_t result = 0u;
        if (traversal.visitLow != 0u && traversal.lowChildIndex != 0xFFFFFFFFu) {
            ++trace.lowRecursionCount;
            result |= EvaluateSelectorBvhRecursiveTraversalInternal(
                context,
                traversal.lowChildIndex,
                traversal.lowBoundsMin,
                traversal.lowBoundsMax,
                trace);
        }

        if (traversal.visitHigh != 0u && traversal.highChildIndex != 0xFFFFFFFFu) {
            ++trace.highRecursionCount;
            result |= EvaluateSelectorBvhRecursiveTraversalInternal(
                context,
                traversal.highChildIndex,
                traversal.highBoundsMin,
                traversal.highBoundsMax,
                trace);
        }

        return result;
    }
}

uint32_t WoWCollision::EvaluateSelectorPlaneLeafQueueMutation(uint32_t triangleIndex,
                                                              uint32_t stateMaskByte,
                                                              uint32_t firstOutcode,
                                                              uint32_t secondOutcode,
                                                              uint32_t thirdOutcode,
                                                              uint32_t& ioOverflowFlags,
                                                              uint16_t* pendingIds,
                                                              uint32_t pendingIdCapacity,
                                                              uint32_t& ioPendingCount,
                                                              uint16_t* acceptedIds,
                                                              uint32_t acceptedIdCapacity,
                                                              uint32_t& ioAcceptedCount,
                                                              uint8_t* stateBytes,
                                                              uint32_t stateByteCount,
                                                              SelectorLeafQueueMutationTrace* outTrace)
{
    return EvaluateSelectorLeafQueueMutation(
        triangleIndex,
        stateMaskByte,
        TriangleSharesSelectorPlaneOutcodeReject(firstOutcode, secondOutcode, thirdOutcode),
        ioOverflowFlags,
        pendingIds,
        pendingIdCapacity,
        ioPendingCount,
        acceptedIds,
        acceptedIdCapacity,
        ioAcceptedCount,
        stateBytes,
        stateByteCount,
        outTrace);
}

uint32_t WoWCollision::EvaluateTriangleLocalBoundsLeafQueueMutation(uint32_t triangleIndex,
                                                                    uint32_t stateMaskByte,
                                                                    const G3D::Vector3& localBoundsMin,
                                                                    const G3D::Vector3& localBoundsMax,
                                                                    const G3D::Vector3& point0,
                                                                    const G3D::Vector3& point1,
                                                                    const G3D::Vector3& point2,
                                                                    uint32_t& ioOverflowFlags,
                                                                    uint16_t* pendingIds,
                                                                    uint32_t pendingIdCapacity,
                                                                    uint32_t& ioPendingCount,
                                                                    uint16_t* acceptedIds,
                                                                    uint32_t acceptedIdCapacity,
                                                                    uint32_t& ioAcceptedCount,
                                                                    uint8_t* stateBytes,
                                                                    uint32_t stateByteCount,
                                                                    SelectorLeafQueueMutationTrace* outTrace)
{
    return EvaluateSelectorLeafQueueMutation(
        triangleIndex,
        stateMaskByte,
        EvaluateTriangleLocalBoundsAabbReject(localBoundsMin, localBoundsMax, point0, point1, point2),
        ioOverflowFlags,
        pendingIds,
        pendingIdCapacity,
        ioPendingCount,
        acceptedIds,
        acceptedIdCapacity,
        ioAcceptedCount,
        stateBytes,
        stateByteCount,
        outTrace);
}

uint32_t WoWCollision::EvaluateSelectorBvhRecursionStep(const SelectorBvhChildTraversal& traversal,
                                                        const SelectorBvhRecursionChildOutcome& lowChildOutcome,
                                                        const SelectorBvhRecursionChildOutcome& highChildOutcome,
                                                        uint32_t inputOverflowFlags,
                                                        uint32_t inputPendingCount,
                                                        uint32_t inputAcceptedCount,
                                                        uint32_t inputResult,
                                                        SelectorBvhRecursionStepTrace* outTrace)
{
    SelectorBvhRecursionStepTrace trace{};
    trace.visitLow = traversal.visitLow;
    trace.visitHigh = traversal.visitHigh;
    trace.resultBefore = inputResult;
    trace.pendingCountBefore = inputPendingCount;
    trace.acceptedCountBefore = inputAcceptedCount;
    trace.overflowFlagsBefore = inputOverflowFlags;

    uint32_t result = inputResult;
    uint32_t pendingCount = inputPendingCount;
    uint32_t acceptedCount = inputAcceptedCount;
    uint32_t overflowFlags = inputOverflowFlags;

    trace.resultAfterLow = result;
    trace.pendingCountAfterLow = pendingCount;
    trace.acceptedCountAfterLow = acceptedCount;
    trace.overflowFlagsAfterLow = overflowFlags;

    if (traversal.visitLow != 0u) {
        trace.enteredLowChild = 1u;
        result |= lowChildOutcome.result;
        pendingCount += lowChildOutcome.pendingCountDelta;
        acceptedCount += lowChildOutcome.acceptedCountDelta;
        overflowFlags |= lowChildOutcome.overflowFlagsDelta;

        trace.resultAfterLow = result;
        trace.pendingCountAfterLow = pendingCount;
        trace.acceptedCountAfterLow = acceptedCount;
        trace.overflowFlagsAfterLow = overflowFlags;
    }

    trace.resultAfterHigh = result;
    trace.pendingCountAfterHigh = pendingCount;
    trace.acceptedCountAfterHigh = acceptedCount;
    trace.overflowFlagsAfterHigh = overflowFlags;

    if (traversal.visitHigh != 0u) {
        trace.enteredHighChild = 1u;
        result |= highChildOutcome.result;
        pendingCount += highChildOutcome.pendingCountDelta;
        acceptedCount += highChildOutcome.acceptedCountDelta;
        overflowFlags |= highChildOutcome.overflowFlagsDelta;

        trace.resultAfterHigh = result;
        trace.pendingCountAfterHigh = pendingCount;
        trace.acceptedCountAfterHigh = acceptedCount;
        trace.overflowFlagsAfterHigh = overflowFlags;
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return result;
}

uint32_t WoWCollision::EvaluateSelectorBvhRecursiveTraversal(const SelectorBvhNodeRecord* nodes,
                                                             uint32_t nodeCount,
                                                             uint32_t rootNodeIndex,
                                                             const G3D::Vector3& boundsMin,
                                                             const G3D::Vector3& boundsMax,
                                                             uint32_t stateMaskByte,
                                                             const uint8_t* leafCullStates,
                                                             uint32_t leafCullStateCount,
                                                             const uint16_t* leafTriangleIds,
                                                             uint32_t leafTriangleIdCount,
                                                             const uint8_t* predicateRejectedStates,
                                                             uint32_t predicateRejectedStateCount,
                                                             uint32_t& ioOverflowFlags,
                                                             uint16_t* pendingIds,
                                                             uint32_t pendingIdCapacity,
                                                             uint32_t& ioPendingCount,
                                                             uint16_t* acceptedIds,
                                                             uint32_t acceptedIdCapacity,
                                                             uint32_t& ioAcceptedCount,
                                                             uint8_t* stateBytes,
                                                             uint32_t stateByteCount,
                                                             SelectorBvhRecursiveTraversalTrace* outTrace)
{
    SelectorBvhRecursiveTraversalTrace trace{};
    if (nodes == nullptr ||
        rootNodeIndex >= nodeCount ||
        (leafTriangleIdCount != 0u && leafTriangleIds == nullptr) ||
        (pendingIdCapacity != 0u && pendingIds == nullptr) ||
        (acceptedIdCapacity != 0u && acceptedIds == nullptr) ||
        stateBytes == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    SelectorBvhRecursiveTraversalContext context{};
    context.nodes = nodes;
    context.nodeCount = nodeCount;
    context.stateMaskByte = stateMaskByte;
    context.leafCullStates = leafCullStates;
    context.leafCullStateCount = leafCullStateCount;
    context.leafTriangleIds = leafTriangleIds;
    context.leafTriangleIdCount = leafTriangleIdCount;
    context.predicateRejectedStates = predicateRejectedStates;
    context.predicateRejectedStateCount = predicateRejectedStateCount;
    context.ioOverflowFlags = &ioOverflowFlags;
    context.pendingIds = pendingIds;
    context.pendingIdCapacity = pendingIdCapacity;
    context.ioPendingCount = &ioPendingCount;
    context.acceptedIds = acceptedIds;
    context.acceptedIdCapacity = acceptedIdCapacity;
    context.ioAcceptedCount = &ioAcceptedCount;
    context.stateBytes = stateBytes;
    context.stateByteCount = stateByteCount;

    const uint32_t result = EvaluateSelectorBvhRecursiveTraversalInternal(
        context,
        rootNodeIndex,
        boundsMin,
        boundsMax,
        trace);

    trace.resultAfterTraversal = result;
    trace.pendingCountAfterTraversal = ioPendingCount;
    trace.acceptedCountAfterTraversal = ioAcceptedCount;
    trace.overflowFlagsAfterTraversal = ioOverflowFlags;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return result;
}
