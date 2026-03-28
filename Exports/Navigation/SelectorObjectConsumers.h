#pragma once

#include "PhysicsEngine.h"
#include "SelectorObjectTraversal.h"

// Write scope for selector object post-traversal consumer parity work.
// New implementations for the accepted-list consumer path should live here instead of
// PhysicsEngine.cpp or SelectorObjectTraversal.cpp.

namespace WoWCollision
{
    struct SelectorObjectConsumerDispatchTrace
    {
        uint32_t calledTraversal;
        uint32_t calledAcceptedListConsumer;
        uint32_t calledRasterConsumer;
        uint32_t clearedQueuedVisitedBits;
        uint32_t queueMutationObserved;
        uint32_t inputFlags;
        uint32_t pendingCountBeforeCleanup;
        uint32_t acceptedCountBeforeCleanup;
        uint32_t pendingCountAfterCleanup;
        uint32_t acceptedCountAfterCleanup;
    };

    struct SelectorAcceptedListConsumerTrace
    {
        uint32_t preprocessedPendingQueue;
        uint32_t preprocessedAcceptedQueue;
        uint32_t pendingPreprocessIterations;
        uint32_t acceptedPreprocessIterations;
        uint32_t helper6acdd0CallCount;
        uint32_t helper7bca80CallCount;
        uint32_t helper6bce50CallCount;
        uint32_t helper6a98e0CallCount;
        uint32_t outputQueueFlags;
        uint32_t recordSlotReserved;
        uint32_t recordOverflowFlagSet;
        uint32_t triangleWordSpanReserved;
        uint32_t triangleWordOverflowFlagSet;
        uint32_t acceptedIdSpanReserved;
        uint32_t acceptedIdOverflowFlagSet;
        uint32_t reservedTriangleWordStart;
        uint32_t reservedTriangleWordCount;
        uint32_t reservedAcceptedIdStart;
        uint32_t reservedAcceptedIdCount;
        uint32_t copiedTriangleWordCount;
        uint32_t copiedAcceptedIdCount;
        uint32_t minTriangleVertexIndex;
        uint32_t maxTriangleVertexIndex;
    };

    struct SelectorAcceptedListConsumerRecordSlotTrace
    {
        uint32_t recordReserved;
        uint32_t zeroInitializedDwordCount;
        uint32_t recordIndex;
        uint32_t ownerPayloadToken;
        uint32_t vertexStreamToken;
        uint32_t metadataToken;
        uint32_t triangleWordBufferToken;
        uint32_t acceptedIdBufferToken;
        uint32_t ownerContextToken;
        uint16_t triangleWordCountField;
        uint16_t acceptedIdCountField;
        uint16_t minTriangleVertexIndex;
        uint16_t maxTriangleVertexIndex;
    };

    enum class SelectorAcceptedListConsumerPreprocessSource : uint32_t
    {
        Pending = 0u,
        Accepted = 1u,
    };

    struct SelectorAcceptedListConsumerPreprocessTrace
    {
        uint32_t executed;
        uint32_t sourceKind;
        uint32_t sourceTriangleIndex;
        uint32_t sourceTriangleWordBase;
        uint32_t debugColorToken;
        uint32_t ownerPayloadToken;
        uint32_t helper6acdd0CallCount;
        uint32_t helper7bca80CallCount;
        uint32_t helper6bce50CallCount;
        uint32_t helper6a98e0CallCount;
        uint32_t normalizeHelperZeroArg;
        uint32_t supportVertexTokens[3];
        uint16_t supportVertexIndices[3];
        uint32_t localSlotOffsets[3];
    };

    struct SelectorAcceptedListConsumerPreprocessLoopTrace
    {
        uint32_t preprocessEnabled;
        uint32_t sourceKind;
        uint32_t debugColorToken;
        uint32_t ownerPayloadToken;
        uint32_t sourceCount;
        uint32_t executedIterationCount;
        uint32_t storedIterationCount;
        uint32_t helper6acdd0CallCount;
        uint32_t helper7bca80CallCount;
        uint32_t helper6bce50CallCount;
        uint32_t helper6a98e0CallCount;
    };

    uint32_t EvaluateSelectorObjectConsumerDispatch(uint32_t inputFlags,
                                                    uint32_t queueMutationCountBefore,
                                                    uint32_t queueMutationCountAfterConsumers,
                                                    uint16_t* pendingIds,
                                                    uint32_t pendingIdCapacity,
                                                    uint32_t& ioPendingCount,
                                                    uint32_t& ioAcceptedCount,
                                                    uint8_t* stateBytes,
                                                    uint32_t stateByteCount,
                                                    SelectorObjectConsumerDispatchTrace* outTrace);

    uint32_t EvaluateSelectorAcceptedListConsumerVisibleBody(uint32_t globalFlags,
                                                             uint32_t inputQueueFlags,
                                                             uint32_t inputConsumerFlags,
                                                             uint32_t& ioRecordReservationCount,
                                                             uint32_t& ioTriangleWordCount,
                                                             uint32_t& ioAcceptedIdCount,
                                                             const uint16_t* pendingIds,
                                                             uint32_t pendingCount,
                                                             const uint16_t* acceptedIds,
                                                             uint32_t acceptedCount,
                                                             const uint16_t* triangleVertexIndices,
                                                             uint32_t triangleVertexIndexCount,
                                                             uint16_t* outputAcceptedIds,
                                                             uint32_t outputAcceptedIdCapacity,
                                                             uint16_t* outputTriangleWords,
                                                             uint32_t outputTriangleWordCapacity,
                                                             SelectorAcceptedListConsumerTrace* outTrace);

    uint32_t EvaluateSelectorAcceptedListConsumerRecordWrite(uint32_t globalFlags,
                                                             uint32_t inputQueueFlags,
                                                             uint32_t inputConsumerFlags,
                                                             uint32_t ownerContextToken,
                                                             uint32_t vertexStreamToken,
                                                             uint32_t metadataToken,
                                                             uint32_t outputTriangleWordBaseToken,
                                                             uint32_t outputAcceptedIdBaseToken,
                                                             uint32_t& ioRecordReservationCount,
                                                             uint32_t& ioTriangleWordCount,
                                                             uint32_t& ioAcceptedIdCount,
                                                             const uint16_t* pendingIds,
                                                             uint32_t pendingCount,
                                                             const uint16_t* acceptedIds,
                                                             uint32_t acceptedCount,
                                                             const uint16_t* triangleVertexIndices,
                                                             uint32_t triangleVertexIndexCount,
                                                             uint16_t* outputAcceptedIds,
                                                             uint32_t outputAcceptedIdCapacity,
                                                             uint16_t* outputTriangleWords,
                                                             uint32_t outputTriangleWordCapacity,
                                                             SelectorAcceptedListConsumerRecordSlotTrace* outRecordTrace,
                                                             SelectorAcceptedListConsumerTrace* outTrace);

    uint32_t EvaluateSelectorAcceptedListConsumerPreprocessIteration(SelectorAcceptedListConsumerPreprocessSource sourceKind,
                                                                     uint32_t ownerContextToken,
                                                                     uint32_t vertexStreamToken,
                                                                     const uint16_t* sourceTriangleIds,
                                                                     uint32_t sourceCount,
                                                                     uint32_t sourceIndex,
                                                                     const uint16_t* triangleVertexIndices,
                                                                     uint32_t triangleVertexIndexCount,
                                                                     SelectorAcceptedListConsumerPreprocessTrace* outTrace);

    uint32_t EvaluateSelectorAcceptedListConsumerPreprocessLoop(SelectorAcceptedListConsumerPreprocessSource sourceKind,
                                                                bool preprocessEnabled,
                                                                uint32_t ownerContextToken,
                                                                uint32_t vertexStreamToken,
                                                                const uint16_t* sourceTriangleIds,
                                                                uint32_t sourceCount,
                                                                const uint16_t* triangleVertexIndices,
                                                                uint32_t triangleVertexIndexCount,
                                                                SelectorAcceptedListConsumerPreprocessTrace* outIterationTraces,
                                                                uint32_t maxIterationTraces,
                                                                SelectorAcceptedListConsumerPreprocessLoopTrace* outTrace);
}
