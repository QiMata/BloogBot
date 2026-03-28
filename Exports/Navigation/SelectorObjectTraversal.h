#pragma once

#include "PhysicsEngine.h"

namespace WoWCollision
{
    struct SelectorBvhRecursionChildOutcome
    {
        uint32_t result;
        uint32_t pendingCountDelta;
        uint32_t acceptedCountDelta;
        uint32_t overflowFlagsDelta;
    };

    struct SelectorBvhRecursionStepTrace
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

    struct SelectorBvhRecursiveTraversalTrace
    {
        uint32_t visitedNodeCount = 0u;
        uint32_t leafNodeCount = 0u;
        uint32_t leafCullRejectedCount = 0u;
        uint32_t lowRecursionCount = 0u;
        uint32_t highRecursionCount = 0u;
        uint32_t leafInvocationCount = 0u;
        uint32_t resultAfterTraversal = 0u;
        uint32_t pendingCountAfterTraversal = 0u;
        uint32_t acceptedCountAfterTraversal = 0u;
        uint32_t overflowFlagsAfterTraversal = 0u;
        std::array<uint32_t, 16> visitedNodeIndices{};
        std::array<uint16_t, 32> visitedLeafTriangleIds{};
    };

    uint32_t EvaluateSelectorPlaneLeafQueueMutation(uint32_t triangleIndex,
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
                                                    SelectorLeafQueueMutationTrace* outTrace);

    uint32_t EvaluateTriangleLocalBoundsLeafQueueMutation(uint32_t triangleIndex,
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
                                                          SelectorLeafQueueMutationTrace* outTrace);

    uint32_t EvaluateSelectorBvhRecursionStep(const SelectorBvhChildTraversal& traversal,
                                              const SelectorBvhRecursionChildOutcome& lowChildOutcome,
                                              const SelectorBvhRecursionChildOutcome& highChildOutcome,
                                              uint32_t inputOverflowFlags,
                                              uint32_t inputPendingCount,
                                              uint32_t inputAcceptedCount,
                                              uint32_t inputResult,
                                              SelectorBvhRecursionStepTrace* outTrace);

    uint32_t EvaluateSelectorBvhRecursiveTraversal(const SelectorBvhNodeRecord* nodes,
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
                                                   SelectorBvhRecursiveTraversalTrace* outTrace);
}
