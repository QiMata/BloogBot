#include "SelectorObjectConsumers.h"

#include <array>

// Write scope for selector object post-traversal consumer parity work.
// New implementations for the accepted-list consumer path should live here instead of
// PhysicsEngine.cpp or SelectorObjectTraversal.cpp.

namespace
{
    constexpr uint32_t kWoWSelectorQueuedVisitedBit = 0x80u;
    constexpr uint32_t kWoWSelectorRasterGateMask = 0x000F0000u;
    constexpr uint32_t kWoWSelectorStateStrideBytes = 2u;
    constexpr uint32_t kWoWSelectorConsumerPendingGateMask = 0x00200000u;
    constexpr uint32_t kWoWSelectorConsumerOverflowFlag = 0x00000001u;
    constexpr uint32_t kWoWSelectorConsumerMaxRecordCount = 0x20u;
    constexpr uint32_t kWoWSelectorConsumerMaxAcceptedIds = 0x2000u;
    constexpr uint32_t kWoWSelectorConsumerMaxTriangleWords = 0xC000u;
    constexpr uint32_t kWoWSelectorConsumerMaxAcceptedIdSlots = 0x4000u;
    constexpr uint32_t kWoWSelectorConsumerTriangleWordStride = 3u;
    constexpr uint32_t kWoWSelectorConsumerOwnerPayloadOffset = 0x94u;
    constexpr uint32_t kWoWSelectorConsumerZeroInitDwordCount = 8u;
    constexpr uint32_t kWoWSelectorPendingPreprocessColorToken = 0x7FFF0000u;
    constexpr uint32_t kWoWSelectorAcceptedPreprocessColorToken = 0x7F00FF00u;
    constexpr uint32_t kWoWSelectorVertexStreamStrideBytes = sizeof(float) * 3u;
    constexpr std::array<uint32_t, 3> kWoWSelectorPendingPreprocessLocalSlotOffsets =
    {
        0x1Cu,
        0x28u,
        0x34u,
    };
    constexpr std::array<uint32_t, 3> kWoWSelectorAcceptedPreprocessLocalSlotOffsets =
    {
        0x34u,
        0x28u,
        0x1Cu,
    };

    struct SelectorAcceptedListConsumerReservation
    {
        bool recordReserved = false;
        bool triangleWordSpanReserved = false;
        bool acceptedIdSpanReserved = false;
        uint32_t reservedTriangleWordStart = 0u;
        uint32_t reservedTriangleWordCount = 0u;
        uint32_t reservedAcceptedIdStart = 0u;
        uint32_t reservedAcceptedIdCount = 0u;
    };

    SelectorAcceptedListConsumerReservation ReserveSelectorAcceptedListConsumerSpans(
        uint32_t acceptedCount,
        uint32_t& ioRecordReservationCount,
        uint32_t& ioTriangleWordCount,
        uint32_t& ioAcceptedIdCount,
        uint32_t& ioOutputQueueFlags)
    {
        SelectorAcceptedListConsumerReservation reservation{};
        if ((ioRecordReservationCount + 1u) >= kWoWSelectorConsumerMaxRecordCount) {
            ioOutputQueueFlags |= kWoWSelectorConsumerOverflowFlag;
            return reservation;
        }

        reservation.recordReserved = true;
        ++ioRecordReservationCount;

        reservation.reservedTriangleWordStart = ioTriangleWordCount;
        reservation.reservedTriangleWordCount = acceptedCount * kWoWSelectorConsumerTriangleWordStride;
        if ((ioTriangleWordCount + reservation.reservedTriangleWordCount) < kWoWSelectorConsumerMaxTriangleWords) {
            ioTriangleWordCount += reservation.reservedTriangleWordCount;
            reservation.triangleWordSpanReserved = true;
        } else {
            ioOutputQueueFlags |= kWoWSelectorConsumerOverflowFlag;
        }

        reservation.reservedAcceptedIdStart = ioAcceptedIdCount;
        reservation.reservedAcceptedIdCount = acceptedCount;
        if ((ioAcceptedIdCount + acceptedCount) < kWoWSelectorConsumerMaxAcceptedIdSlots) {
            ioAcceptedIdCount += acceptedCount;
            reservation.acceptedIdSpanReserved = true;
        } else {
            ioOutputQueueFlags |= kWoWSelectorConsumerOverflowFlag;
        }

        return reservation;
    }

    uint16_t SaturateToUInt16(uint32_t value)
    {
        return value > 0xFFFFu ? 0xFFFFu : static_cast<uint16_t>(value);
    }

    uint32_t ComputeSelectorAcceptedListConsumerBufferToken(uint32_t baseToken, uint32_t reservedStart)
    {
        return baseToken + (reservedStart * sizeof(uint16_t));
    }

    uint32_t ComputeSelectorAcceptedListConsumerVertexToken(uint32_t vertexStreamToken, uint16_t vertexIndex)
    {
        return vertexStreamToken + (static_cast<uint32_t>(vertexIndex) * kWoWSelectorVertexStreamStrideBytes);
    }

    void ComputeSelectorAcceptedListConsumerRecordMinMax(const uint16_t* acceptedIds,
                                                         uint32_t acceptedCount,
                                                         const uint16_t* triangleVertexIndices,
                                                         uint32_t triangleVertexIndexCount,
                                                         uint16_t& outMinTriangleVertexIndex,
                                                         uint16_t& outMaxTriangleVertexIndex)
    {
        outMinTriangleVertexIndex = 0xFFFFu;
        outMaxTriangleVertexIndex = 0u;

        if (acceptedIds == nullptr || triangleVertexIndices == nullptr) {
            return;
        }

        bool foundTriangleWord = false;
        for (uint32_t acceptedIndex = 0u; acceptedIndex < acceptedCount; ++acceptedIndex) {
            const uint32_t triangleWordBase =
                static_cast<uint32_t>(acceptedIds[acceptedIndex]) * kWoWSelectorConsumerTriangleWordStride;
            for (uint32_t localWordIndex = 0u; localWordIndex < kWoWSelectorConsumerTriangleWordStride; ++localWordIndex) {
                const uint32_t sourceWordIndex = triangleWordBase + localWordIndex;
                if (sourceWordIndex >= triangleVertexIndexCount) {
                    continue;
                }

                const uint16_t triangleWord = triangleVertexIndices[sourceWordIndex];
                outMinTriangleVertexIndex = std::min(outMinTriangleVertexIndex, triangleWord);
                outMaxTriangleVertexIndex = std::max(outMaxTriangleVertexIndex, triangleWord);
                foundTriangleWord = true;
            }
        }

        if (!foundTriangleWord) {
            outMinTriangleVertexIndex = 0xFFFFu;
            outMaxTriangleVertexIndex = 0u;
        }
    }
}

uint32_t WoWCollision::EvaluateSelectorAcceptedListConsumerPreprocessIteration(
    SelectorAcceptedListConsumerPreprocessSource sourceKind,
    uint32_t ownerContextToken,
    uint32_t vertexStreamToken,
    const uint16_t* sourceTriangleIds,
    uint32_t sourceCount,
    uint32_t sourceIndex,
    const uint16_t* triangleVertexIndices,
    uint32_t triangleVertexIndexCount,
    SelectorAcceptedListConsumerPreprocessTrace* outTrace)
{
    SelectorAcceptedListConsumerPreprocessTrace trace{};
    trace.sourceKind = static_cast<uint32_t>(sourceKind);

    const bool pendingSource = sourceKind == SelectorAcceptedListConsumerPreprocessSource::Pending;
    trace.debugColorToken = pendingSource ? kWoWSelectorPendingPreprocessColorToken
                                          : kWoWSelectorAcceptedPreprocessColorToken;
    trace.ownerPayloadToken = ownerContextToken + kWoWSelectorConsumerOwnerPayloadOffset;

    const std::array<uint32_t, 3>& localSlotOffsets =
        pendingSource ? kWoWSelectorPendingPreprocessLocalSlotOffsets
                      : kWoWSelectorAcceptedPreprocessLocalSlotOffsets;
    for (uint32_t slotIndex = 0u; slotIndex < localSlotOffsets.size(); ++slotIndex) {
        trace.localSlotOffsets[slotIndex] = localSlotOffsets[slotIndex];
    }

    if (sourceTriangleIds == nullptr || sourceIndex >= sourceCount) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    trace.executed = 1u;
    trace.helper6acdd0CallCount = 1u;
    trace.helper7bca80CallCount = 3u;
    trace.helper6bce50CallCount = 1u;
    trace.helper6a98e0CallCount = 1u;
    trace.normalizeHelperZeroArg = 1u;
    trace.sourceTriangleIndex = sourceTriangleIds[sourceIndex];
    trace.sourceTriangleWordBase = trace.sourceTriangleIndex * kWoWSelectorConsumerTriangleWordStride;

    for (uint32_t slotIndex = 0u; slotIndex < 3u; ++slotIndex) {
        const uint32_t sourceWordIndex =
            trace.sourceTriangleWordBase + (kWoWSelectorConsumerTriangleWordStride - 1u - slotIndex);
        if (triangleVertexIndices == nullptr || sourceWordIndex >= triangleVertexIndexCount) {
            continue;
        }

        const uint16_t supportVertexIndex = triangleVertexIndices[sourceWordIndex];
        trace.supportVertexIndices[slotIndex] = supportVertexIndex;
        trace.supportVertexTokens[slotIndex] =
            ComputeSelectorAcceptedListConsumerVertexToken(vertexStreamToken, supportVertexIndex);
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.executed;
}

uint32_t WoWCollision::EvaluateSelectorAcceptedListConsumerPreprocessLoop(
    SelectorAcceptedListConsumerPreprocessSource sourceKind,
    bool preprocessEnabled,
    uint32_t ownerContextToken,
    uint32_t vertexStreamToken,
    const uint16_t* sourceTriangleIds,
    uint32_t sourceCount,
    const uint16_t* triangleVertexIndices,
    uint32_t triangleVertexIndexCount,
    SelectorAcceptedListConsumerPreprocessTrace* outIterationTraces,
    uint32_t maxIterationTraces,
    SelectorAcceptedListConsumerPreprocessLoopTrace* outTrace)
{
    SelectorAcceptedListConsumerPreprocessLoopTrace trace{};
    trace.preprocessEnabled = preprocessEnabled ? 1u : 0u;
    trace.sourceKind = static_cast<uint32_t>(sourceKind);

    const bool pendingSource = sourceKind == SelectorAcceptedListConsumerPreprocessSource::Pending;
    trace.debugColorToken = pendingSource ? kWoWSelectorPendingPreprocessColorToken
                                          : kWoWSelectorAcceptedPreprocessColorToken;
    trace.ownerPayloadToken = ownerContextToken + kWoWSelectorConsumerOwnerPayloadOffset;
    trace.sourceCount = sourceCount;

    if (!preprocessEnabled || sourceTriangleIds == nullptr || sourceCount == 0u) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    for (uint32_t sourceIndex = 0u; sourceIndex < sourceCount; ++sourceIndex) {
        SelectorAcceptedListConsumerPreprocessTrace iterationTrace{};
        const uint32_t iterationExecuted = EvaluateSelectorAcceptedListConsumerPreprocessIteration(
            sourceKind,
            ownerContextToken,
            vertexStreamToken,
            sourceTriangleIds,
            sourceCount,
            sourceIndex,
            triangleVertexIndices,
            triangleVertexIndexCount,
            &iterationTrace);

        trace.executedIterationCount += iterationExecuted;
        trace.helper6acdd0CallCount += iterationTrace.helper6acdd0CallCount;
        trace.helper7bca80CallCount += iterationTrace.helper7bca80CallCount;
        trace.helper6bce50CallCount += iterationTrace.helper6bce50CallCount;
        trace.helper6a98e0CallCount += iterationTrace.helper6a98e0CallCount;

        if (outIterationTraces != nullptr && sourceIndex < maxIterationTraces) {
            outIterationTraces[sourceIndex] = iterationTrace;
            ++trace.storedIterationCount;
        }
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.executedIterationCount;
}

uint32_t WoWCollision::EvaluateSelectorObjectConsumerDispatch(uint32_t inputFlags,
                                                              uint32_t queueMutationCountBefore,
                                                              uint32_t queueMutationCountAfterConsumers,
                                                              uint16_t* pendingIds,
                                                              uint32_t pendingIdCapacity,
                                                              uint32_t& ioPendingCount,
                                                              uint32_t& ioAcceptedCount,
                                                              uint8_t* stateBytes,
                                                              uint32_t stateByteCount,
                                                              SelectorObjectConsumerDispatchTrace* outTrace)
{
    SelectorObjectConsumerDispatchTrace trace{};
    trace.calledTraversal = 1u;
    trace.calledAcceptedListConsumer = 1u;
    trace.calledRasterConsumer = (inputFlags & kWoWSelectorRasterGateMask) != 0u ? 1u : 0u;
    trace.inputFlags = inputFlags;
    trace.pendingCountBeforeCleanup = ioPendingCount;
    trace.acceptedCountBeforeCleanup = ioAcceptedCount;

    const uint32_t pendingCountToClear = std::min(ioPendingCount, pendingIdCapacity);
    for (uint32_t reverseIndex = pendingCountToClear; reverseIndex > 0u; --reverseIndex) {
        const uint16_t queuedTriangleIndex = pendingIds[reverseIndex - 1u];
        const uint32_t stateByteIndex = static_cast<uint32_t>(queuedTriangleIndex) * kWoWSelectorStateStrideBytes;
        if (stateBytes == nullptr || stateByteIndex >= stateByteCount) {
            continue;
        }

        stateBytes[stateByteIndex] &= static_cast<uint8_t>(~kWoWSelectorQueuedVisitedBit);
        ++trace.clearedQueuedVisitedBits;
    }

    ioPendingCount = 0u;
    ioAcceptedCount = 0u;
    trace.pendingCountAfterCleanup = ioPendingCount;
    trace.acceptedCountAfterCleanup = ioAcceptedCount;
    trace.queueMutationObserved = queueMutationCountAfterConsumers != queueMutationCountBefore ? 1u : 0u;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.queueMutationObserved;
}

uint32_t WoWCollision::EvaluateSelectorAcceptedListConsumerVisibleBody(uint32_t globalFlags,
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
                                                                       SelectorAcceptedListConsumerTrace* outTrace)
{
    SelectorAcceptedListConsumerTrace trace{};
    trace.outputQueueFlags = inputQueueFlags | inputConsumerFlags;
    trace.minTriangleVertexIndex = 0xFFFFu;

    SelectorAcceptedListConsumerPreprocessLoopTrace pendingLoopTrace{};
    const uint32_t pendingIterations = EvaluateSelectorAcceptedListConsumerPreprocessLoop(
        SelectorAcceptedListConsumerPreprocessSource::Pending,
        (globalFlags & kWoWSelectorConsumerPendingGateMask) != 0u,
        0u,
        0u,
        pendingIds,
        pendingCount,
        triangleVertexIndices,
        triangleVertexIndexCount,
        nullptr,
        0u,
        &pendingLoopTrace);
    trace.preprocessedPendingQueue = pendingIterations != 0u ? 1u : 0u;
    trace.pendingPreprocessIterations = pendingIterations;
    trace.helper6acdd0CallCount += pendingLoopTrace.helper6acdd0CallCount;
    trace.helper7bca80CallCount += pendingLoopTrace.helper7bca80CallCount;
    trace.helper6bce50CallCount += pendingLoopTrace.helper6bce50CallCount;
    trace.helper6a98e0CallCount += pendingLoopTrace.helper6a98e0CallCount;

    SelectorAcceptedListConsumerPreprocessLoopTrace acceptedLoopTrace{};
    const uint32_t acceptedIterations = EvaluateSelectorAcceptedListConsumerPreprocessLoop(
        SelectorAcceptedListConsumerPreprocessSource::Accepted,
        true,
        0u,
        0u,
        acceptedIds,
        acceptedCount,
        triangleVertexIndices,
        triangleVertexIndexCount,
        nullptr,
        0u,
        &acceptedLoopTrace);
    trace.preprocessedAcceptedQueue = acceptedIterations != 0u ? 1u : 0u;
    trace.acceptedPreprocessIterations = acceptedIterations;
    trace.helper6acdd0CallCount += acceptedLoopTrace.helper6acdd0CallCount;
    trace.helper7bca80CallCount += acceptedLoopTrace.helper7bca80CallCount;
    trace.helper6bce50CallCount += acceptedLoopTrace.helper6bce50CallCount;
    trace.helper6a98e0CallCount += acceptedLoopTrace.helper6a98e0CallCount;

    if (acceptedCount == 0u || acceptedCount > kWoWSelectorConsumerMaxAcceptedIds) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    SelectorAcceptedListConsumerReservation reservation = ReserveSelectorAcceptedListConsumerSpans(
        acceptedCount,
        ioRecordReservationCount,
        ioTriangleWordCount,
        ioAcceptedIdCount,
        trace.outputQueueFlags);

    trace.recordSlotReserved = reservation.recordReserved ? 1u : 0u;
    trace.triangleWordSpanReserved = reservation.triangleWordSpanReserved ? 1u : 0u;
    trace.acceptedIdSpanReserved = reservation.acceptedIdSpanReserved ? 1u : 0u;
    trace.reservedTriangleWordStart = reservation.reservedTriangleWordStart;
    trace.reservedTriangleWordCount = reservation.reservedTriangleWordCount;
    trace.reservedAcceptedIdStart = reservation.reservedAcceptedIdStart;
    trace.reservedAcceptedIdCount = reservation.reservedAcceptedIdCount;

    if (!reservation.recordReserved) {
        trace.recordOverflowFlagSet = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    if (!reservation.triangleWordSpanReserved) {
        trace.triangleWordOverflowFlagSet = 1u;
    }

    if (!reservation.acceptedIdSpanReserved) {
        trace.acceptedIdOverflowFlagSet = 1u;
    }

    for (uint32_t acceptedIndex = 0u; acceptedIndex < acceptedCount; ++acceptedIndex) {
        const uint16_t acceptedTriangleIndex = acceptedIds[acceptedIndex];

        if (reservation.acceptedIdSpanReserved && outputAcceptedIds != nullptr) {
            const uint32_t outputAcceptedIndex = reservation.reservedAcceptedIdStart + acceptedIndex;
            if (outputAcceptedIndex < outputAcceptedIdCapacity) {
                outputAcceptedIds[outputAcceptedIndex] = acceptedTriangleIndex;
                ++trace.copiedAcceptedIdCount;
            }
        }

        if (!reservation.triangleWordSpanReserved || outputTriangleWords == nullptr || triangleVertexIndices == nullptr) {
            continue;
        }

        const uint32_t triangleWordBase = static_cast<uint32_t>(acceptedTriangleIndex) * kWoWSelectorConsumerTriangleWordStride;
        for (uint32_t localWordIndex = 0u; localWordIndex < kWoWSelectorConsumerTriangleWordStride; ++localWordIndex) {
            const uint32_t sourceWordIndex = triangleWordBase + localWordIndex;
            const uint32_t outputWordIndex = reservation.reservedTriangleWordStart + (acceptedIndex * kWoWSelectorConsumerTriangleWordStride) + localWordIndex;
            if (sourceWordIndex >= triangleVertexIndexCount || outputWordIndex >= outputTriangleWordCapacity) {
                continue;
            }

            const uint16_t triangleWord = triangleVertexIndices[sourceWordIndex];
            outputTriangleWords[outputWordIndex] = triangleWord;
            ++trace.copiedTriangleWordCount;
            trace.minTriangleVertexIndex = std::min<uint32_t>(trace.minTriangleVertexIndex, triangleWord);
            trace.maxTriangleVertexIndex = std::max<uint32_t>(trace.maxTriangleVertexIndex, triangleWord);
        }
    }

    if (trace.copiedTriangleWordCount == 0u) {
        trace.minTriangleVertexIndex = 0xFFFFu;
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.recordSlotReserved;
}

uint32_t WoWCollision::EvaluateSelectorAcceptedListConsumerRecordWrite(uint32_t globalFlags,
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
                                                                       SelectorAcceptedListConsumerTrace* outTrace)
{
    SelectorAcceptedListConsumerTrace trace{};
    SelectorAcceptedListConsumerRecordSlotTrace recordTrace{};
    const uint32_t recordIndexBeforeReserve = ioRecordReservationCount;

    const uint32_t result = EvaluateSelectorAcceptedListConsumerVisibleBody(
        globalFlags,
        inputQueueFlags,
        inputConsumerFlags,
        ioRecordReservationCount,
        ioTriangleWordCount,
        ioAcceptedIdCount,
        pendingIds,
        pendingCount,
        acceptedIds,
        acceptedCount,
        triangleVertexIndices,
        triangleVertexIndexCount,
        outputAcceptedIds,
        outputAcceptedIdCapacity,
        outputTriangleWords,
        outputTriangleWordCapacity,
        &trace);

    if (trace.recordSlotReserved != 0u) {
        recordTrace.recordReserved = 1u;
        recordTrace.zeroInitializedDwordCount = kWoWSelectorConsumerZeroInitDwordCount;
        recordTrace.recordIndex = recordIndexBeforeReserve;
        recordTrace.ownerPayloadToken = ownerContextToken + kWoWSelectorConsumerOwnerPayloadOffset;
        recordTrace.vertexStreamToken = vertexStreamToken;
        recordTrace.metadataToken = metadataToken;
        recordTrace.ownerContextToken = ownerContextToken;

        if (trace.triangleWordSpanReserved != 0u) {
            recordTrace.triangleWordBufferToken = ComputeSelectorAcceptedListConsumerBufferToken(
                outputTriangleWordBaseToken,
                trace.reservedTriangleWordStart);
            recordTrace.triangleWordCountField = SaturateToUInt16(trace.reservedTriangleWordCount);
            ComputeSelectorAcceptedListConsumerRecordMinMax(
                acceptedIds,
                acceptedCount,
                triangleVertexIndices,
                triangleVertexIndexCount,
                recordTrace.minTriangleVertexIndex,
                recordTrace.maxTriangleVertexIndex);
        } else {
            recordTrace.minTriangleVertexIndex = 0xFFFFu;
        }

        if (trace.acceptedIdSpanReserved != 0u) {
            recordTrace.acceptedIdBufferToken = ComputeSelectorAcceptedListConsumerBufferToken(
                outputAcceptedIdBaseToken,
                trace.reservedAcceptedIdStart);
            recordTrace.acceptedIdCountField = SaturateToUInt16(acceptedCount);
        }
    }

    if (outRecordTrace != nullptr) {
        *outRecordTrace = recordTrace;
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return result;
}
