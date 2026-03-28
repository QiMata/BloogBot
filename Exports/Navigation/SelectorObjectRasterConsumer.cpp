#include "SelectorObjectRasterConsumer.h"

#include <algorithm>
#include <cmath>
#include <cstdint>

namespace
{
    constexpr uint32_t kSelectorObjectRasterModeGateBit = 0x00001000u;
    constexpr uint32_t kSelectorObjectRasterQueueEntryLimit = 0x20u;
    constexpr uint32_t kSelectorObjectRasterScratchWordLimit = 0xC000u;
    constexpr std::array<uint32_t, 4> kSelectorObjectRasterModeMaskTable =
    {
        0x00010000u,
        0x00020000u,
        0x00040000u,
        0x00080000u,
    };

    int32_t QuantizeSelectorObjectRasterAxis(float value, float quantizeScale)
    {
        return static_cast<int32_t>(std::floor(static_cast<double>(value) * static_cast<double>(quantizeScale)));
    }

    void BuildSelectorObjectRasterRawWindow(const G3D::Vector3& boundsMin,
                                            const G3D::Vector3& boundsMax,
                                            float quantizeScale,
                                            WoWCollision::SelectorObjectRasterWindow& outWindow)
    {
        // 0x6BB7D0..0x6BB85C quantizes max first, then min, producing
        // [rowMin, columnMin, rowMax, columnMax] in the local stack window.
        outWindow.rowMin = QuantizeSelectorObjectRasterAxis(boundsMax.x, quantizeScale);
        outWindow.columnMin = QuantizeSelectorObjectRasterAxis(boundsMax.y, quantizeScale);
        outWindow.rowMax = QuantizeSelectorObjectRasterAxis(boundsMin.x, quantizeScale);
        outWindow.columnMax = QuantizeSelectorObjectRasterAxis(boundsMin.y, quantizeScale);
    }

    bool IsSelectorObjectRasterWindowInsideBounds(const WoWCollision::SelectorObjectRasterWindow& window,
                                                  int32_t rasterRowCount,
                                                  int32_t rasterColumnCount)
    {
        if (rasterRowCount <= 0 || rasterColumnCount <= 0) {
            return false;
        }

        if (window.rowMin < 0 || window.columnMin < 0) {
            return false;
        }

        if (window.rowMax >= rasterRowCount || window.columnMax >= rasterColumnCount) {
            return false;
        }

        return true;
    }

    void ClampSelectorObjectRasterWindow(const WoWCollision::SelectorObjectRasterWindow& window,
                                         int32_t rasterRowCount,
                                         int32_t rasterColumnCount,
                                         WoWCollision::SelectorObjectRasterWindow& outWindow)
    {
        outWindow.rowMin = std::clamp(window.rowMin, 0, std::max(rasterRowCount - 1, 0));
        outWindow.columnMin = std::clamp(window.columnMin, 0, std::max(rasterColumnCount - 1, 0));
        outWindow.rowMax = std::clamp(window.rowMax, 0, std::max(rasterRowCount - 1, 0));
        outWindow.columnMax = std::clamp(window.columnMax, 0, std::max(rasterColumnCount - 1, 0));
    }

    uint32_t ComputeSelectorObjectRasterInclusiveCount(int32_t minValue, int32_t maxValue, int32_t extraPoints)
    {
        const int64_t inclusiveSpan = static_cast<int64_t>(maxValue) - static_cast<int64_t>(minValue) + 1ll + static_cast<int64_t>(extraPoints);
        if (inclusiveSpan <= 0ll) {
            return 0u;
        }

        return static_cast<uint32_t>(inclusiveSpan);
    }

    uint32_t ComputeSelectorObjectRasterScratchByteCount(const WoWCollision::SelectorObjectRasterWindow& window)
    {
        const uint32_t pointCountY = ComputeSelectorObjectRasterInclusiveCount(window.rowMin, window.rowMax, 1);
        const uint32_t pointCountX = ComputeSelectorObjectRasterInclusiveCount(window.columnMin, window.columnMax, 1);
        if (pointCountX == 0u || pointCountY == 0u) {
            return 0u;
        }

        const uint64_t scratchBytes = static_cast<uint64_t>(pointCountX) * static_cast<uint64_t>(pointCountY) * sizeof(uint32_t);
        const uint64_t alignedScratchBytes = (scratchBytes + 3ull) & ~3ull;
        return static_cast<uint32_t>(std::min<uint64_t>(alignedScratchBytes, 0xFFFFFFFFull));
    }

    uint32_t ComputeSelectorObjectRasterReservedScratchWords(uint32_t rasterCellCountX, uint32_t rasterCellCountY)
    {
        return rasterCellCountX * rasterCellCountY * 6u;
    }

    uint32_t ComputeSelectorObjectRasterIndex(int32_t rowIndex, int32_t columnIndex, int32_t rowStride)
    {
        return static_cast<uint32_t>((rowIndex * rowStride) + columnIndex);
    }

    uint32_t LinkSelectorObjectRasterCleanupQueueEntry(bool deferredCleanupListPresent,
                                                       const WoWCollision::SelectorObjectRasterQueueEntry& entry)
    {
        return deferredCleanupListPresent && entry.allocated != 0u ? 1u : 0u;
    }
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterConsumerPrefix(uint32_t modeWord,
                                                                  int32_t rasterRowCount,
                                                                  int32_t rasterColumnCount,
                                                                  int32_t rasterRowStride,
                                                                  float quantizeScale,
                                                                  const G3D::Vector3& objectTranslation,
                                                                  const SelectorObjectRasterPayload& sourcePayload,
                                                                  SelectorObjectRasterPrefixTrace* outTrace)
{
    SelectorObjectRasterPrefixTrace trace{};
    if ((modeWord & kSelectorObjectRasterModeGateBit) == 0u) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    trace.modeGateAccepted = 1u;
    trace.quantizeScale = quantizeScale;
    trace.appliedTranslation = -objectTranslation;

    SelectorObjectRasterPayload translatedPayload = sourcePayload;
    TranslateSelectorSourceGeometry(
        trace.appliedTranslation,
        translatedPayload.planes.data(),
        static_cast<uint32_t>(translatedPayload.planes.size()),
        translatedPayload.supportPoints.data(),
        static_cast<uint32_t>(translatedPayload.supportPoints.size()),
        &translatedPayload.anchorPoint0,
        &translatedPayload.anchorPoint1);

    trace.translatedAnchorPoint0 = translatedPayload.anchorPoint0;
    trace.translatedAnchorPoint1 = translatedPayload.anchorPoint1;
    trace.translatedFirstPlaneDistance = translatedPayload.planes[0].planeDistance;

    BuildSelectorSupportPointBounds(
        translatedPayload.supportPoints.data(),
        static_cast<uint32_t>(translatedPayload.supportPoints.size()),
        trace.translatedSupportPointMin,
        trace.translatedSupportPointMax);

    BuildSelectorObjectRasterRawWindow(
        trace.translatedSupportPointMin,
        trace.translatedSupportPointMax,
        quantizeScale,
        trace.rawWindow);

    if (!IsSelectorObjectRasterWindowInsideBounds(trace.rawWindow, rasterRowCount, rasterColumnCount)) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    trace.quantizedWindowAccepted = 1u;
    ClampSelectorObjectRasterWindow(trace.rawWindow, rasterRowCount, rasterColumnCount, trace.clippedWindow);

    trace.prepassPointCountX = ComputeSelectorObjectRasterInclusiveCount(trace.clippedWindow.columnMin, trace.clippedWindow.columnMax, 1);
    trace.prepassPointCountY = ComputeSelectorObjectRasterInclusiveCount(trace.clippedWindow.rowMin, trace.clippedWindow.rowMax, 1);
    trace.rasterCellCountX = ComputeSelectorObjectRasterInclusiveCount(trace.clippedWindow.columnMin, trace.clippedWindow.columnMax, 0);
    trace.rasterCellCountY = ComputeSelectorObjectRasterInclusiveCount(trace.clippedWindow.rowMin, trace.clippedWindow.rowMax, 0);
    trace.scratchByteCount = ComputeSelectorObjectRasterScratchByteCount(trace.clippedWindow);
    trace.scratchAllocationRequired = trace.scratchByteCount != 0u ? 1u : 0u;

    trace.pointStartIndex = (rasterRowStride * trace.clippedWindow.rowMin) + trace.clippedWindow.columnMin;
    trace.pointRowAdvance = rasterRowStride - static_cast<int32_t>(trace.prepassPointCountX);

    trace.enteredPrepassPointLoops =
        (trace.scratchAllocationRequired != 0u &&
         trace.prepassPointCountX != 0u &&
         trace.prepassPointCountY != 0u) ? 1u : 0u;

    trace.enteredRasterCellLoops =
        (trace.scratchAllocationRequired != 0u &&
         trace.rasterCellCountX != 0u &&
         trace.rasterCellCountY != 0u) ? 1u : 0u;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return 1u;
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterCellIteration(const SelectorObjectRasterPrefixTrace& prefix,
                                                                 uint32_t localRow,
                                                                 uint32_t localColumn,
                                                                 int32_t pointGridRowStride,
                                                                 int32_t cellModeRowStride,
                                                                 uint32_t cellModeMaskFlags,
                                                                 uint32_t callerContextToken,
                                                                 uint32_t rasterSourceToken,
                                                                 uint32_t inputQueueEntryCount,
                                                                 uint32_t inputScratchWordCount,
                                                                 const uint8_t* cellModes,
                                                                 uint32_t cellModeCount,
                                                                 const uint32_t* pointOutcodes,
                                                                 uint32_t pointOutcodeCapacity,
                                                                 uint16_t* outScratchWords,
                                                                 uint32_t outScratchWordCapacity,
                                                                 SelectorObjectRasterQueueEntry& ioEntry,
                                                                 SelectorObjectRasterCellIterationTrace* outTrace)
{
    SelectorObjectRasterCellIterationTrace trace{};
    trace.queueCountBefore = inputQueueEntryCount;
    trace.queueCountAfter = inputQueueEntryCount + (ioEntry.allocated != 0u ? 1u : 0u);
    trace.scratchWordsBefore = inputScratchWordCount;
    trace.scratchWordsAfter = inputScratchWordCount +
        (ioEntry.scratchBufferPresent != 0u ? ioEntry.scratchWordReserved : 0u);
    trace.entryAllocatedBefore = ioEntry.allocated;
    trace.entryAllocatedAfter = ioEntry.allocated;
    trace.appendedWordCountBefore = ioEntry.appendedWordCount;
    trace.appendedWordCountAfter = ioEntry.appendedWordCount;
    trace.appendedTriangleCountBefore = ioEntry.appendedTriangleCount;
    trace.appendedTriangleCountAfter = ioEntry.appendedTriangleCount;

    if (localRow >= prefix.rasterCellCountY ||
        localColumn >= prefix.rasterCellCountX ||
        pointGridRowStride <= 0 ||
        cellModeRowStride <= 0) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    trace.visitedRasterCell = 1u;

    const int32_t globalRow = prefix.clippedWindow.rowMin + static_cast<int32_t>(localRow);
    const int32_t globalColumn = prefix.clippedWindow.columnMin + static_cast<int32_t>(localColumn);
    trace.cellIndex = ComputeSelectorObjectRasterIndex(globalRow, globalColumn, cellModeRowStride);
    const uint8_t cellMode = trace.cellIndex < cellModeCount ? cellModes[trace.cellIndex] : 0x0Fu;
    trace.cellModeNibble = static_cast<uint32_t>(cellMode & 0x0Fu);

    if (trace.cellModeNibble == 0x0Fu) {
        trace.skippedByCellModeValue = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    if ((kSelectorObjectRasterModeMaskTable[trace.cellModeNibble & 0x3u] & cellModeMaskFlags) == 0u) {
        trace.skippedByCellModeMask = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    trace.localPointBase = (localRow * prefix.prepassPointCountX) + localColumn;
    trace.worldPointBase = ComputeSelectorObjectRasterIndex(globalRow, globalColumn, pointGridRowStride);

    const uint32_t reservedScratchWords = ComputeSelectorObjectRasterReservedScratchWords(
        prefix.rasterCellCountX,
        prefix.rasterCellCountY);
    const uint32_t localPointStride = prefix.prepassPointCountX;
    const std::array<std::array<uint32_t, 3>, 2> scratchTriangleOffsets =
    {{
        {{ 0u, localPointStride, localPointStride + 1u }},
        {{ 0u, 1u, localPointStride + 1u }},
    }};
    const std::array<std::array<uint32_t, 3>, 2> pointTriangleOffsets =
    {{
        {{ 0u, static_cast<uint32_t>(pointGridRowStride + 1), static_cast<uint32_t>(pointGridRowStride) }},
        {{ 0u, 1u, static_cast<uint32_t>(pointGridRowStride + 1) }},
    }};

    const auto loadOutcode = [&](uint32_t scratchIndex) -> uint32_t
    {
        if (pointOutcodes == nullptr || scratchIndex >= pointOutcodeCapacity) {
            return 0u;
        }

        return pointOutcodes[scratchIndex];
    };

    for (uint32_t triangleIndex = 0u; triangleIndex < scratchTriangleOffsets.size(); ++triangleIndex) {
        const uint32_t outcode0 = loadOutcode(trace.localPointBase + scratchTriangleOffsets[triangleIndex][0]);
        const uint32_t outcode1 = loadOutcode(trace.localPointBase + scratchTriangleOffsets[triangleIndex][1]);
        const uint32_t outcode2 = loadOutcode(trace.localPointBase + scratchTriangleOffsets[triangleIndex][2]);
        if (TriangleSharesSelectorPlaneOutcodeReject(outcode0, outcode1, outcode2)) {
            ++trace.rejectedTriangleCount;
            trace.triangleRejectMask |= (1u << triangleIndex);
            continue;
        }

        ++trace.acceptedTriangleCount;
        trace.returnedAnyCandidate = 1u;
        trace.triangleAcceptMask |= (1u << triangleIndex);

        if (ioEntry.allocated == 0u) {
            if ((inputQueueEntryCount + 1u) >= kSelectorObjectRasterQueueEntryLimit) {
                trace.queueLimitOverflowed = 1u;
            } else {
                trace.queueCountAfter = inputQueueEntryCount + 1u;
                ioEntry.allocated = 1u;
                ioEntry.callerContextToken = callerContextToken;
                ioEntry.rasterSourceToken = rasterSourceToken;

                if ((inputScratchWordCount + reservedScratchWords) >= kSelectorObjectRasterScratchWordLimit ||
                    (inputScratchWordCount + reservedScratchWords) > outScratchWordCapacity) {
                    trace.scratchOverflowed = 1u;
                } else {
                    ioEntry.scratchWordStart = inputScratchWordCount;
                    ioEntry.scratchWordReserved = reservedScratchWords;
                    ioEntry.scratchBufferPresent = 1u;
                    trace.scratchWordsAfter = inputScratchWordCount + reservedScratchWords;
                }
            }
        }

        if (ioEntry.allocated == 0u || ioEntry.scratchBufferPresent == 0u || outScratchWords == nullptr) {
            continue;
        }

        for (uint32_t vertexIndex = 0u; vertexIndex < 3u; ++vertexIndex) {
            const uint16_t appendedWord = static_cast<uint16_t>(trace.worldPointBase + pointTriangleOffsets[triangleIndex][vertexIndex]);
            const uint32_t scratchWriteIndex = ioEntry.scratchWordStart + ioEntry.appendedWordCount;
            if (scratchWriteIndex < outScratchWordCapacity) {
                outScratchWords[scratchWriteIndex] = appendedWord;
            }

            ++ioEntry.appendedWordCount;
            ioEntry.minAppendedWord = std::min(ioEntry.minAppendedWord, appendedWord);
            ioEntry.maxAppendedWord = std::max(ioEntry.maxAppendedWord, appendedWord);
        }

        ++ioEntry.appendedTriangleCount;
    }

    trace.entryAllocatedAfter = ioEntry.allocated;
    trace.appendedWordCountAfter = ioEntry.appendedWordCount;
    trace.appendedTriangleCountAfter = ioEntry.appendedTriangleCount;
    trace.scratchWordsAfter = inputScratchWordCount +
        (ioEntry.scratchBufferPresent != 0u ? ioEntry.scratchWordReserved : 0u);

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.returnedAnyCandidate;
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterPrepassOutcodeLoop(const SelectorObjectRasterPrefixTrace& prefix,
                                                                      int32_t pointGridRowStride,
                                                                      const SelectorObjectRasterPayload& translatedPayload,
                                                                      const G3D::Vector3* pointGrid,
                                                                      uint32_t pointGridPointCount,
                                                                      uint32_t* outPointOutcodes,
                                                                      uint32_t outPointOutcodeCapacity,
                                                                      SelectorObjectRasterPrepassTrace* outTrace)
{
    SelectorObjectRasterPrepassTrace trace{};
    if (pointGrid == nullptr || pointGridRowStride <= 0) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    for (uint32_t localRow = 0u; localRow < prefix.prepassPointCountY; ++localRow) {
        const int32_t globalRow = prefix.clippedWindow.rowMin + static_cast<int32_t>(localRow);
        for (uint32_t localColumn = 0u; localColumn < prefix.prepassPointCountX; ++localColumn) {
            const int32_t globalColumn = prefix.clippedWindow.columnMin + static_cast<int32_t>(localColumn);
            const uint32_t pointIndex = ComputeSelectorObjectRasterIndex(globalRow, globalColumn, pointGridRowStride);
            if (trace.pointWriteCount == 0u) {
                trace.firstPointIndex = pointIndex;
            }

            trace.lastPointIndex = pointIndex;

            uint32_t outcode = 0u;
            if (pointIndex < pointGridPointCount) {
                outcode = BuildSelectorSourcePlaneOutcode(
                    translatedPayload.planes.data(),
                    static_cast<uint32_t>(translatedPayload.planes.size()),
                    pointGrid[pointIndex]);
            } else {
                ++trace.pointIndexOutOfRangeCount;
            }

            const uint32_t localIndex = (localRow * prefix.prepassPointCountX) + localColumn;
            if (outPointOutcodes != nullptr && localIndex < outPointOutcodeCapacity) {
                outPointOutcodes[localIndex] = outcode;
                ++trace.outputWriteCount;
            }

            ++trace.pointWriteCount;
        }
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.pointWriteCount;
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterPrepassComposition(const SelectorObjectRasterPrefixTrace& prefix,
                                                                      const SelectorObjectRasterPayload& sourcePayload,
                                                                      int32_t pointGridRowStride,
                                                                      const G3D::Vector3* pointGrid,
                                                                      uint32_t pointGridPointCount,
                                                                      uint32_t* outPointOutcodes,
                                                                      uint32_t outPointOutcodeCapacity,
                                                                      SelectorObjectRasterPrepassCompositionTrace* outTrace)
{
    SelectorObjectRasterPrepassCompositionTrace trace{};
    if (pointGrid == nullptr || pointGridRowStride <= 0) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    SelectorObjectRasterPayload translatedPayload = sourcePayload;
    TranslateSelectorSourceGeometry(
        prefix.appliedTranslation,
        translatedPayload.planes.data(),
        static_cast<uint32_t>(translatedPayload.planes.size()),
        translatedPayload.supportPoints.data(),
        static_cast<uint32_t>(translatedPayload.supportPoints.size()),
        &translatedPayload.anchorPoint0,
        &translatedPayload.anchorPoint1);

    trace.translatedAnchorPoint0 = translatedPayload.anchorPoint0;
    trace.translatedAnchorPoint1 = translatedPayload.anchorPoint1;
    trace.translatedFirstPlaneDistance = translatedPayload.planes[0].planeDistance;
    EvaluateSelectorObjectRasterPrepassOutcodeLoop(
        prefix,
        pointGridRowStride,
        translatedPayload,
        pointGrid,
        pointGridPointCount,
        outPointOutcodes,
        outPointOutcodeCapacity,
        &trace.prepass);

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.prepass.pointWriteCount;
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterConsumerBody(uint32_t modeWord,
                                                                int32_t rasterRowCount,
                                                                int32_t rasterColumnCount,
                                                                int32_t pointGridRowStride,
                                                                int32_t cellModeRowStride,
                                                                float quantizeScale,
                                                                uint32_t cellModeMaskFlags,
                                                                uint32_t callerContextToken,
                                                                uint32_t rasterSourceToken,
                                                                uint32_t inputQueueEntryCount,
                                                                uint32_t inputScratchWordCount,
                                                                bool deferredCleanupListPresent,
                                                                const G3D::Vector3& objectTranslation,
                                                                const SelectorObjectRasterPayload& sourcePayload,
                                                                const G3D::Vector3* pointGrid,
                                                                uint32_t pointGridPointCount,
                                                                const uint8_t* cellModes,
                                                                uint32_t cellModeCount,
                                                                uint32_t* outPointOutcodes,
                                                                uint32_t outPointOutcodeCapacity,
                                                                uint16_t* outScratchWords,
                                                                uint32_t outScratchWordCapacity,
                                                                SelectorObjectRasterQueueEntry* outEntry,
                                                                SelectorObjectRasterBodyTrace* outTrace)
{
    SelectorObjectRasterBodyTrace trace{};
    trace.queueCountBefore = inputQueueEntryCount;
    trace.queueCountAfter = inputQueueEntryCount;
    trace.scratchWordsBefore = inputScratchWordCount;
    trace.scratchWordsAfter = inputScratchWordCount;

    SelectorObjectRasterQueueEntry entry{};

    SelectorObjectRasterPrefixTrace prefix{};
    const uint32_t prefixResult = EvaluateSelectorObjectRasterConsumerPrefix(
        modeWord,
        rasterRowCount,
        rasterColumnCount,
        cellModeRowStride,
        quantizeScale,
        objectTranslation,
        sourcePayload,
        &prefix);
    trace.prefix = prefix;

    SelectorObjectRasterPrefixFailureCleanupTrace prefixFailureTrace{};
    if (EvaluateSelectorObjectRasterPrefixFailureCleanupExit(prefixResult, prefix, &prefixFailureTrace) == 0u) {
        trace.failureCleanupExecuted = prefixFailureTrace.failureCleanupExecuted;
        trace.failureCleanupDestroyedPayloadBlocks = prefixFailureTrace.failureCleanupDestroyedPayloadBlocks;

        if (outEntry != nullptr) {
            *outEntry = entry;
        }

        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    if (pointGrid == nullptr ||
        cellModes == nullptr ||
        pointGridRowStride <= 0 ||
        cellModeRowStride <= 0) {
        if (outEntry != nullptr) {
            *outEntry = entry;
        }

        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    SelectorObjectRasterPrepassCompositionTrace prepassCompositionTrace{};
    trace.prepassPointWrites = EvaluateSelectorObjectRasterPrepassComposition(
        prefix,
        sourcePayload,
        pointGridRowStride,
        pointGrid,
        pointGridPointCount,
        outPointOutcodes,
        outPointOutcodeCapacity,
        &prepassCompositionTrace);

    SelectorObjectRasterAggregationTrace aggregationTrace{};
    trace.returnedAnyCandidate = EvaluateSelectorObjectRasterCellLoopAggregation(
        prefix,
        pointGridRowStride,
        cellModeRowStride,
        cellModeMaskFlags,
        callerContextToken,
        rasterSourceToken,
        inputQueueEntryCount,
        inputScratchWordCount,
        deferredCleanupListPresent,
        cellModes,
        cellModeCount,
        outPointOutcodes,
        outPointOutcodeCapacity,
        outScratchWords,
        outScratchWordCapacity,
        &aggregationTrace);
    entry = aggregationTrace.entry;
    trace.visitedRasterCellCount = aggregationTrace.visitedRasterCellCount;
    trace.skippedByCellModeValue = aggregationTrace.skippedByCellModeValue;
    trace.skippedByCellModeMask = aggregationTrace.skippedByCellModeMask;
    trace.rejectedTriangleCount = aggregationTrace.rejectedTriangleCount;
    trace.acceptedTriangleCount = aggregationTrace.acceptedTriangleCount;
    trace.queueCountAfter = aggregationTrace.queueCountAfter;
    trace.scratchWordsAfter = aggregationTrace.scratchWordsAfter;
    trace.queueLimitOverflowed = aggregationTrace.queueLimitOverflowed;
    trace.scratchOverflowed = aggregationTrace.scratchOverflowed;
    trace.appendedWordCount = aggregationTrace.appendedWordCount;
    trace.appendedTriangleCount = aggregationTrace.appendedTriangleCount;
    trace.finalQueueEntryListSpliceLinked = aggregationTrace.finalQueueEntryListSpliceLinked;
    trace.normalCleanupLinked = aggregationTrace.normalCleanupLinked;

    if (outEntry != nullptr) {
        *outEntry = entry;
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.returnedAnyCandidate;
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterPrefixFailureCleanupExit(
    uint32_t prefixAccepted,
    const SelectorObjectRasterPrefixTrace& prefix,
    SelectorObjectRasterPrefixFailureCleanupTrace* outTrace)
{
    SelectorObjectRasterPrefixFailureCleanupTrace trace{};
    trace.prefixAccepted = prefixAccepted != 0u ? 1u : 0u;
    trace.modeGateAccepted = prefix.modeGateAccepted;
    if (trace.prefixAccepted == 0u) {
        trace.returnedBeforePrepass = 1u;
        if (trace.modeGateAccepted != 0u) {
            trace.failureCleanupExecuted = 1u;
            trace.failureCleanupDestroyedPayloadBlocks = 6u;
        }
    }

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.prefixAccepted;
}

uint32_t WoWCollision::EvaluateSelectorObjectRasterCellLoopAggregation(const SelectorObjectRasterPrefixTrace& prefix,
                                                                       int32_t pointGridRowStride,
                                                                       int32_t cellModeRowStride,
                                                                       uint32_t cellModeMaskFlags,
                                                                       uint32_t callerContextToken,
                                                                       uint32_t rasterSourceToken,
                                                                       uint32_t inputQueueEntryCount,
                                                                       uint32_t inputScratchWordCount,
                                                                       bool deferredCleanupListPresent,
                                                                       const uint8_t* cellModes,
                                                                       uint32_t cellModeCount,
                                                                       const uint32_t* pointOutcodes,
                                                                       uint32_t pointOutcodeCapacity,
                                                                       uint16_t* outScratchWords,
                                                                       uint32_t outScratchWordCapacity,
                                                                       SelectorObjectRasterAggregationTrace* outTrace)
{
    SelectorObjectRasterAggregationTrace trace{};
    trace.queueCountBefore = inputQueueEntryCount;
    trace.queueCountAfter = inputQueueEntryCount;
    trace.scratchWordsBefore = inputScratchWordCount;
    trace.scratchWordsAfter = inputScratchWordCount;

    for (uint32_t localRow = 0u; localRow < prefix.rasterCellCountY; ++localRow) {
        for (uint32_t localColumn = 0u; localColumn < prefix.rasterCellCountX; ++localColumn) {
            SelectorObjectRasterCellIterationTrace cellTrace{};
            trace.returnedAnyCandidate |= EvaluateSelectorObjectRasterCellIteration(
                prefix,
                localRow,
                localColumn,
                pointGridRowStride,
                cellModeRowStride,
                cellModeMaskFlags,
                callerContextToken,
                rasterSourceToken,
                inputQueueEntryCount,
                inputScratchWordCount,
                cellModes,
                cellModeCount,
                pointOutcodes,
                pointOutcodeCapacity,
                outScratchWords,
                outScratchWordCapacity,
                trace.entry,
                &cellTrace);
            trace.visitedRasterCellCount += cellTrace.visitedRasterCell;
            trace.skippedByCellModeValue += cellTrace.skippedByCellModeValue;
            trace.skippedByCellModeMask += cellTrace.skippedByCellModeMask;
            trace.rejectedTriangleCount += cellTrace.rejectedTriangleCount;
            trace.acceptedTriangleCount += cellTrace.acceptedTriangleCount;
            trace.queueLimitOverflowed |= cellTrace.queueLimitOverflowed;
            trace.scratchOverflowed |= cellTrace.scratchOverflowed;
            trace.queueCountAfter = cellTrace.queueCountAfter;
            trace.scratchWordsAfter = cellTrace.scratchWordsAfter;
        }
    }

    trace.appendedWordCount = trace.entry.appendedWordCount;
    trace.appendedTriangleCount = trace.entry.appendedTriangleCount;
    trace.finalQueueEntryListSpliceLinked = LinkSelectorObjectRasterCleanupQueueEntry(
        deferredCleanupListPresent,
        trace.entry);
    trace.normalCleanupLinked = trace.finalQueueEntryListSpliceLinked;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.returnedAnyCandidate;
}
