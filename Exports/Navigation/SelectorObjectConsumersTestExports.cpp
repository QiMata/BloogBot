#include "SelectorObjectConsumers.h"

extern "C" {

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectConsumerDispatch(
    uint32_t inputFlags,
    uint32_t queueMutationCountBefore,
    uint32_t queueMutationCountAfterConsumers,
    uint16_t* pendingIds,
    int pendingIdCapacity,
    uint32_t* ioPendingCount,
    uint32_t* ioAcceptedCount,
    uint8_t* stateBytes,
    int stateByteCount,
    WoWCollision::SelectorObjectConsumerDispatchTrace* outTrace)
{
    if (!ioPendingCount || !ioAcceptedCount) {
        if (outTrace) {
            *outTrace = WoWCollision::SelectorObjectConsumerDispatchTrace{};
        }
        return 0u;
    }

    return WoWCollision::EvaluateSelectorObjectConsumerDispatch(
        inputFlags,
        queueMutationCountBefore,
        queueMutationCountAfterConsumers,
        pendingIds,
        pendingIdCapacity < 0 ? 0u : static_cast<uint32_t>(pendingIdCapacity),
        *ioPendingCount,
        *ioAcceptedCount,
        stateBytes,
        stateByteCount < 0 ? 0u : static_cast<uint32_t>(stateByteCount),
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorAcceptedListConsumerVisibleBody(
    uint32_t globalFlags,
    uint32_t inputQueueFlags,
    uint32_t inputConsumerFlags,
    uint32_t* ioRecordReservationCount,
    uint32_t* ioTriangleWordCount,
    uint32_t* ioAcceptedIdCount,
    const uint16_t* pendingIds,
    int pendingCount,
    const uint16_t* acceptedIds,
    int acceptedCount,
    const uint16_t* triangleVertexIndices,
    int triangleVertexIndexCount,
    uint16_t* outputAcceptedIds,
    int outputAcceptedIdCapacity,
    uint16_t* outputTriangleWords,
    int outputTriangleWordCapacity,
    WoWCollision::SelectorAcceptedListConsumerTrace* outTrace)
{
    if (!ioRecordReservationCount || !ioTriangleWordCount || !ioAcceptedIdCount) {
        if (outTrace) {
            *outTrace = WoWCollision::SelectorAcceptedListConsumerTrace{};
        }
        return 0u;
    }

    return WoWCollision::EvaluateSelectorAcceptedListConsumerVisibleBody(
        globalFlags,
        inputQueueFlags,
        inputConsumerFlags,
        *ioRecordReservationCount,
        *ioTriangleWordCount,
        *ioAcceptedIdCount,
        pendingIds,
        pendingCount < 0 ? 0u : static_cast<uint32_t>(pendingCount),
        acceptedIds,
        acceptedCount < 0 ? 0u : static_cast<uint32_t>(acceptedCount),
        triangleVertexIndices,
        triangleVertexIndexCount < 0 ? 0u : static_cast<uint32_t>(triangleVertexIndexCount),
        outputAcceptedIds,
        outputAcceptedIdCapacity < 0 ? 0u : static_cast<uint32_t>(outputAcceptedIdCapacity),
        outputTriangleWords,
        outputTriangleWordCapacity < 0 ? 0u : static_cast<uint32_t>(outputTriangleWordCapacity),
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorAcceptedListConsumerRecordWrite(
    uint32_t globalFlags,
    uint32_t inputQueueFlags,
    uint32_t inputConsumerFlags,
    uint32_t ownerContextToken,
    uint32_t vertexStreamToken,
    uint32_t metadataToken,
    uint32_t outputTriangleWordBaseToken,
    uint32_t outputAcceptedIdBaseToken,
    uint32_t* ioRecordReservationCount,
    uint32_t* ioTriangleWordCount,
    uint32_t* ioAcceptedIdCount,
    const uint16_t* pendingIds,
    int pendingCount,
    const uint16_t* acceptedIds,
    int acceptedCount,
    const uint16_t* triangleVertexIndices,
    int triangleVertexIndexCount,
    uint16_t* outputAcceptedIds,
    int outputAcceptedIdCapacity,
    uint16_t* outputTriangleWords,
    int outputTriangleWordCapacity,
    WoWCollision::SelectorAcceptedListConsumerRecordSlotTrace* outRecordTrace,
    WoWCollision::SelectorAcceptedListConsumerTrace* outTrace)
{
    if (!ioRecordReservationCount || !ioTriangleWordCount || !ioAcceptedIdCount) {
        if (outRecordTrace) {
            *outRecordTrace = WoWCollision::SelectorAcceptedListConsumerRecordSlotTrace{};
        }

        if (outTrace) {
            *outTrace = WoWCollision::SelectorAcceptedListConsumerTrace{};
        }
        return 0u;
    }

    return WoWCollision::EvaluateSelectorAcceptedListConsumerRecordWrite(
        globalFlags,
        inputQueueFlags,
        inputConsumerFlags,
        ownerContextToken,
        vertexStreamToken,
        metadataToken,
        outputTriangleWordBaseToken,
        outputAcceptedIdBaseToken,
        *ioRecordReservationCount,
        *ioTriangleWordCount,
        *ioAcceptedIdCount,
        pendingIds,
        pendingCount < 0 ? 0u : static_cast<uint32_t>(pendingCount),
        acceptedIds,
        acceptedCount < 0 ? 0u : static_cast<uint32_t>(acceptedCount),
        triangleVertexIndices,
        triangleVertexIndexCount < 0 ? 0u : static_cast<uint32_t>(triangleVertexIndexCount),
        outputAcceptedIds,
        outputAcceptedIdCapacity < 0 ? 0u : static_cast<uint32_t>(outputAcceptedIdCapacity),
        outputTriangleWords,
        outputTriangleWordCapacity < 0 ? 0u : static_cast<uint32_t>(outputTriangleWordCapacity),
        outRecordTrace,
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorAcceptedListConsumerPreprocessIteration(
    uint32_t sourceKind,
    uint32_t ownerContextToken,
    uint32_t vertexStreamToken,
    const uint16_t* sourceTriangleIds,
    int sourceCount,
    int sourceIndex,
    const uint16_t* triangleVertexIndices,
    int triangleVertexIndexCount,
    WoWCollision::SelectorAcceptedListConsumerPreprocessTrace* outTrace)
{
    return WoWCollision::EvaluateSelectorAcceptedListConsumerPreprocessIteration(
        sourceKind == 0u ? WoWCollision::SelectorAcceptedListConsumerPreprocessSource::Pending
                         : WoWCollision::SelectorAcceptedListConsumerPreprocessSource::Accepted,
        ownerContextToken,
        vertexStreamToken,
        sourceTriangleIds,
        sourceCount < 0 ? 0u : static_cast<uint32_t>(sourceCount),
        sourceIndex < 0 ? 0u : static_cast<uint32_t>(sourceIndex),
        triangleVertexIndices,
        triangleVertexIndexCount < 0 ? 0u : static_cast<uint32_t>(triangleVertexIndexCount),
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorAcceptedListConsumerPreprocessLoop(
    uint32_t sourceKind,
    bool preprocessEnabled,
    uint32_t ownerContextToken,
    uint32_t vertexStreamToken,
    const uint16_t* sourceTriangleIds,
    int sourceCount,
    const uint16_t* triangleVertexIndices,
    int triangleVertexIndexCount,
    WoWCollision::SelectorAcceptedListConsumerPreprocessTrace* outIterationTraces,
    int maxIterationTraces,
    WoWCollision::SelectorAcceptedListConsumerPreprocessLoopTrace* outTrace)
{
    return WoWCollision::EvaluateSelectorAcceptedListConsumerPreprocessLoop(
        sourceKind == 0u ? WoWCollision::SelectorAcceptedListConsumerPreprocessSource::Pending
                         : WoWCollision::SelectorAcceptedListConsumerPreprocessSource::Accepted,
        preprocessEnabled,
        ownerContextToken,
        vertexStreamToken,
        sourceTriangleIds,
        sourceCount < 0 ? 0u : static_cast<uint32_t>(sourceCount),
        triangleVertexIndices,
        triangleVertexIndexCount < 0 ? 0u : static_cast<uint32_t>(triangleVertexIndexCount),
        outIterationTraces,
        maxIterationTraces < 0 ? 0u : static_cast<uint32_t>(maxIterationTraces),
        outTrace);
}

}
