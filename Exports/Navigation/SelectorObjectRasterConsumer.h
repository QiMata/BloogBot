#pragma once

#include "PhysicsEngine.h"

// Write scope for selector object raster/quantized consumer parity work.
// The large 0x6BB6B0 body should live here instead of PhysicsEngine.cpp.

namespace WoWCollision
{
    constexpr uint32_t SELECTOR_OBJECT_RASTER_PREFIX_PLANE_COUNT = 6u;
    constexpr uint32_t SELECTOR_OBJECT_RASTER_PREFIX_POINT_COUNT = 8u;

    struct SelectorObjectRasterPayload
    {
        std::array<SelectorSupportPlane, SELECTOR_OBJECT_RASTER_PREFIX_PLANE_COUNT> planes{};
        std::array<G3D::Vector3, SELECTOR_OBJECT_RASTER_PREFIX_POINT_COUNT> supportPoints{};
        G3D::Vector3 anchorPoint0 = G3D::Vector3::zero();
        G3D::Vector3 anchorPoint1 = G3D::Vector3::zero();
    };

    struct SelectorObjectRasterWindow
    {
        int32_t rowMin = 0;
        int32_t columnMin = 0;
        int32_t rowMax = -1;
        int32_t columnMax = -1;
    };

    struct SelectorObjectRasterPrefixTrace
    {
        uint32_t modeGateAccepted = 0u;
        uint32_t quantizedWindowAccepted = 0u;
        uint32_t scratchAllocationRequired = 0u;
        uint32_t enteredPrepassPointLoops = 0u;
        uint32_t enteredRasterCellLoops = 0u;
        float quantizeScale = 0.0f;
        G3D::Vector3 appliedTranslation = G3D::Vector3::zero();
        G3D::Vector3 translatedSupportPointMin = G3D::Vector3::zero();
        G3D::Vector3 translatedSupportPointMax = G3D::Vector3::zero();
        G3D::Vector3 translatedAnchorPoint0 = G3D::Vector3::zero();
        G3D::Vector3 translatedAnchorPoint1 = G3D::Vector3::zero();
        float translatedFirstPlaneDistance = 0.0f;
        SelectorObjectRasterWindow rawWindow{};
        SelectorObjectRasterWindow clippedWindow{};
        uint32_t scratchByteCount = 0u;
        uint32_t prepassPointCountX = 0u;
        uint32_t prepassPointCountY = 0u;
        uint32_t rasterCellCountX = 0u;
        uint32_t rasterCellCountY = 0u;
        int32_t pointStartIndex = 0;
        int32_t pointRowAdvance = 0;
    };

    struct SelectorObjectRasterQueueEntry
    {
        uint32_t allocated = 0u;
        uint32_t callerContextToken = 0u;
        uint32_t rasterSourceToken = 0u;
        uint32_t scratchWordStart = 0u;
        uint32_t scratchWordReserved = 0u;
        uint16_t appendedWordCount = 0u;
        uint16_t appendedTriangleCount = 0u;
        uint16_t minAppendedWord = 0xFFFFu;
        uint16_t maxAppendedWord = 0u;
        uint32_t scratchBufferPresent = 0u;
    };

    struct SelectorObjectRasterBodyTrace
    {
        SelectorObjectRasterPrefixTrace prefix{};
        uint32_t returnedAnyCandidate = 0u;
        uint32_t prepassPointWrites = 0u;
        uint32_t visitedRasterCellCount = 0u;
        uint32_t skippedByCellModeValue = 0u;
        uint32_t skippedByCellModeMask = 0u;
        uint32_t rejectedTriangleCount = 0u;
        uint32_t acceptedTriangleCount = 0u;
        uint32_t queueCountBefore = 0u;
        uint32_t queueCountAfter = 0u;
        uint32_t scratchWordsBefore = 0u;
        uint32_t scratchWordsAfter = 0u;
        uint32_t queueLimitOverflowed = 0u;
        uint32_t scratchOverflowed = 0u;
        uint32_t appendedWordCount = 0u;
        uint32_t appendedTriangleCount = 0u;
        uint32_t finalQueueEntryListSpliceLinked = 0u;
        uint32_t normalCleanupLinked = 0u;
        uint32_t failureCleanupExecuted = 0u;
        uint32_t failureCleanupDestroyedPayloadBlocks = 0u;
    };

    struct SelectorObjectRasterPrefixFailureCleanupTrace
    {
        uint32_t prefixAccepted = 0u;
        uint32_t modeGateAccepted = 0u;
        uint32_t failureCleanupExecuted = 0u;
        uint32_t failureCleanupDestroyedPayloadBlocks = 0u;
        uint32_t returnedBeforePrepass = 0u;
    };

    struct SelectorObjectRasterPrepassTrace
    {
        uint32_t pointWriteCount = 0u;
        uint32_t outputWriteCount = 0u;
        uint32_t pointIndexOutOfRangeCount = 0u;
        uint32_t firstPointIndex = 0u;
        uint32_t lastPointIndex = 0u;
    };

    struct SelectorObjectRasterPrepassCompositionTrace
    {
        SelectorObjectRasterPrepassTrace prepass{};
        G3D::Vector3 translatedAnchorPoint0 = G3D::Vector3::zero();
        G3D::Vector3 translatedAnchorPoint1 = G3D::Vector3::zero();
        float translatedFirstPlaneDistance = 0.0f;
    };

    struct SelectorObjectRasterCellIterationTrace
    {
        uint32_t visitedRasterCell = 0u;
        uint32_t skippedByCellModeValue = 0u;
        uint32_t skippedByCellModeMask = 0u;
        uint32_t returnedAnyCandidate = 0u;
        uint32_t rejectedTriangleCount = 0u;
        uint32_t acceptedTriangleCount = 0u;
        uint32_t queueCountBefore = 0u;
        uint32_t queueCountAfter = 0u;
        uint32_t scratchWordsBefore = 0u;
        uint32_t scratchWordsAfter = 0u;
        uint32_t queueLimitOverflowed = 0u;
        uint32_t scratchOverflowed = 0u;
        uint32_t entryAllocatedBefore = 0u;
        uint32_t entryAllocatedAfter = 0u;
        uint32_t cellIndex = 0u;
        uint32_t cellModeNibble = 0u;
        uint32_t localPointBase = 0u;
        uint32_t worldPointBase = 0u;
        uint32_t appendedWordCountBefore = 0u;
        uint32_t appendedWordCountAfter = 0u;
        uint32_t appendedTriangleCountBefore = 0u;
        uint32_t appendedTriangleCountAfter = 0u;
        uint32_t triangleRejectMask = 0u;
        uint32_t triangleAcceptMask = 0u;
    };

    struct SelectorObjectRasterAggregationTrace
    {
        SelectorObjectRasterQueueEntry entry{};
        uint32_t returnedAnyCandidate = 0u;
        uint32_t visitedRasterCellCount = 0u;
        uint32_t skippedByCellModeValue = 0u;
        uint32_t skippedByCellModeMask = 0u;
        uint32_t rejectedTriangleCount = 0u;
        uint32_t acceptedTriangleCount = 0u;
        uint32_t queueCountBefore = 0u;
        uint32_t queueCountAfter = 0u;
        uint32_t scratchWordsBefore = 0u;
        uint32_t scratchWordsAfter = 0u;
        uint32_t queueLimitOverflowed = 0u;
        uint32_t scratchOverflowed = 0u;
        uint32_t appendedWordCount = 0u;
        uint32_t appendedTriangleCount = 0u;
        uint32_t finalQueueEntryListSpliceLinked = 0u;
        uint32_t normalCleanupLinked = 0u;
    };

    uint32_t EvaluateSelectorObjectRasterConsumerPrefix(uint32_t modeWord,
                                                        int32_t rasterRowCount,
                                                        int32_t rasterColumnCount,
                                                        int32_t rasterRowStride,
                                                        float quantizeScale,
                                                        const G3D::Vector3& objectTranslation,
                                                        const SelectorObjectRasterPayload& sourcePayload,
                                                        SelectorObjectRasterPrefixTrace* outTrace);

    uint32_t EvaluateSelectorObjectRasterConsumerBody(uint32_t modeWord,
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
                                                      SelectorObjectRasterBodyTrace* outTrace);

    uint32_t EvaluateSelectorObjectRasterPrefixFailureCleanupExit(
        uint32_t prefixAccepted,
        const SelectorObjectRasterPrefixTrace& prefix,
        SelectorObjectRasterPrefixFailureCleanupTrace* outTrace);

    uint32_t EvaluateSelectorObjectRasterPrepassOutcodeLoop(const SelectorObjectRasterPrefixTrace& prefix,
                                                            int32_t pointGridRowStride,
                                                            const SelectorObjectRasterPayload& translatedPayload,
                                                            const G3D::Vector3* pointGrid,
                                                            uint32_t pointGridPointCount,
                                                            uint32_t* outPointOutcodes,
                                                            uint32_t outPointOutcodeCapacity,
                                                            SelectorObjectRasterPrepassTrace* outTrace);

    uint32_t EvaluateSelectorObjectRasterPrepassComposition(const SelectorObjectRasterPrefixTrace& prefix,
                                                            const SelectorObjectRasterPayload& sourcePayload,
                                                            int32_t pointGridRowStride,
                                                            const G3D::Vector3* pointGrid,
                                                            uint32_t pointGridPointCount,
                                                            uint32_t* outPointOutcodes,
                                                            uint32_t outPointOutcodeCapacity,
                                                            SelectorObjectRasterPrepassCompositionTrace* outTrace);

    uint32_t EvaluateSelectorObjectRasterCellIteration(const SelectorObjectRasterPrefixTrace& prefix,
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
                                                       SelectorObjectRasterCellIterationTrace* outTrace);

    uint32_t EvaluateSelectorObjectRasterCellLoopAggregation(const SelectorObjectRasterPrefixTrace& prefix,
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
                                                             SelectorObjectRasterAggregationTrace* outTrace);
}
