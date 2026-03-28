#include "SelectorObjectRasterConsumer.h"

struct ExportSelectorObjectRasterPayload
{
    WoWCollision::SelectorSupportPlane planes[WoWCollision::SELECTOR_OBJECT_RASTER_PREFIX_PLANE_COUNT];
    G3D::Vector3 supportPoints[WoWCollision::SELECTOR_OBJECT_RASTER_PREFIX_POINT_COUNT];
    G3D::Vector3 anchorPoint0;
    G3D::Vector3 anchorPoint1;
};

struct ExportSelectorObjectRasterWindow
{
    int32_t rowMin;
    int32_t columnMin;
    int32_t rowMax;
    int32_t columnMax;
};

struct ExportSelectorObjectRasterPrefixTrace
{
    uint32_t modeGateAccepted;
    uint32_t quantizedWindowAccepted;
    uint32_t scratchAllocationRequired;
    uint32_t enteredPrepassPointLoops;
    uint32_t enteredRasterCellLoops;
    float quantizeScale;
    G3D::Vector3 appliedTranslation;
    G3D::Vector3 translatedSupportPointMin;
    G3D::Vector3 translatedSupportPointMax;
    G3D::Vector3 translatedAnchorPoint0;
    G3D::Vector3 translatedAnchorPoint1;
    float translatedFirstPlaneDistance;
    ExportSelectorObjectRasterWindow rawWindow;
    ExportSelectorObjectRasterWindow clippedWindow;
    uint32_t scratchByteCount;
    uint32_t prepassPointCountX;
    uint32_t prepassPointCountY;
    uint32_t rasterCellCountX;
    uint32_t rasterCellCountY;
    int32_t pointStartIndex;
    int32_t pointRowAdvance;
};

struct ExportSelectorObjectRasterQueueEntry
{
    uint32_t allocated;
    uint32_t callerContextToken;
    uint32_t rasterSourceToken;
    uint32_t scratchWordStart;
    uint32_t scratchWordReserved;
    uint16_t appendedWordCount;
    uint16_t appendedTriangleCount;
    uint16_t minAppendedWord;
    uint16_t maxAppendedWord;
    uint32_t scratchBufferPresent;
};

struct ExportSelectorObjectRasterBodyTrace
{
    ExportSelectorObjectRasterPrefixTrace prefix;
    uint32_t returnedAnyCandidate;
    uint32_t prepassPointWrites;
    uint32_t visitedRasterCellCount;
    uint32_t skippedByCellModeValue;
    uint32_t skippedByCellModeMask;
    uint32_t rejectedTriangleCount;
    uint32_t acceptedTriangleCount;
    uint32_t queueCountBefore;
    uint32_t queueCountAfter;
    uint32_t scratchWordsBefore;
    uint32_t scratchWordsAfter;
    uint32_t queueLimitOverflowed;
    uint32_t scratchOverflowed;
    uint32_t appendedWordCount;
    uint32_t appendedTriangleCount;
    uint32_t finalQueueEntryListSpliceLinked;
    uint32_t normalCleanupLinked;
    uint32_t failureCleanupExecuted;
    uint32_t failureCleanupDestroyedPayloadBlocks;
};

struct ExportSelectorObjectRasterPrefixFailureCleanupTrace
{
    uint32_t prefixAccepted;
    uint32_t modeGateAccepted;
    uint32_t failureCleanupExecuted;
    uint32_t failureCleanupDestroyedPayloadBlocks;
    uint32_t returnedBeforePrepass;
};

struct ExportSelectorObjectRasterPrepassTrace
{
    uint32_t pointWriteCount;
    uint32_t outputWriteCount;
    uint32_t pointIndexOutOfRangeCount;
    uint32_t firstPointIndex;
    uint32_t lastPointIndex;
};

struct ExportSelectorObjectRasterPrepassCompositionTrace
{
    ExportSelectorObjectRasterPrepassTrace prepass;
    G3D::Vector3 translatedAnchorPoint0;
    G3D::Vector3 translatedAnchorPoint1;
    float translatedFirstPlaneDistance;
};

struct ExportSelectorObjectRasterCellIterationTrace
{
    uint32_t visitedRasterCell;
    uint32_t skippedByCellModeValue;
    uint32_t skippedByCellModeMask;
    uint32_t returnedAnyCandidate;
    uint32_t rejectedTriangleCount;
    uint32_t acceptedTriangleCount;
    uint32_t queueCountBefore;
    uint32_t queueCountAfter;
    uint32_t scratchWordsBefore;
    uint32_t scratchWordsAfter;
    uint32_t queueLimitOverflowed;
    uint32_t scratchOverflowed;
    uint32_t entryAllocatedBefore;
    uint32_t entryAllocatedAfter;
    uint32_t cellIndex;
    uint32_t cellModeNibble;
    uint32_t localPointBase;
    uint32_t worldPointBase;
    uint32_t appendedWordCountBefore;
    uint32_t appendedWordCountAfter;
    uint32_t appendedTriangleCountBefore;
    uint32_t appendedTriangleCountAfter;
    uint32_t triangleRejectMask;
    uint32_t triangleAcceptMask;
};

struct ExportSelectorObjectRasterAggregationTrace
{
    ExportSelectorObjectRasterQueueEntry entry;
    uint32_t returnedAnyCandidate;
    uint32_t visitedRasterCellCount;
    uint32_t skippedByCellModeValue;
    uint32_t skippedByCellModeMask;
    uint32_t rejectedTriangleCount;
    uint32_t acceptedTriangleCount;
    uint32_t queueCountBefore;
    uint32_t queueCountAfter;
    uint32_t scratchWordsBefore;
    uint32_t scratchWordsAfter;
    uint32_t queueLimitOverflowed;
    uint32_t scratchOverflowed;
    uint32_t appendedWordCount;
    uint32_t appendedTriangleCount;
    uint32_t finalQueueEntryListSpliceLinked;
    uint32_t normalCleanupLinked;
};

extern "C" {

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterConsumerPrefix(
    uint32_t modeWord,
    int rasterRowCount,
    int rasterColumnCount,
    int rasterRowStride,
    float quantizeScale,
    const G3D::Vector3* objectTranslation,
    const ExportSelectorObjectRasterPayload* sourcePayload,
    ExportSelectorObjectRasterPrefixTrace* outTrace)
{
    if (objectTranslation == nullptr || sourcePayload == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterPrefixTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPayload nativePayload{};
    for (size_t index = 0; index < nativePayload.planes.size(); ++index) {
        nativePayload.planes[index] = sourcePayload->planes[index];
    }

    for (size_t index = 0; index < nativePayload.supportPoints.size(); ++index) {
        nativePayload.supportPoints[index] = sourcePayload->supportPoints[index];
    }

    nativePayload.anchorPoint0 = sourcePayload->anchorPoint0;
    nativePayload.anchorPoint1 = sourcePayload->anchorPoint1;

    WoWCollision::SelectorObjectRasterPrefixTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterConsumerPrefix(
        modeWord,
        rasterRowCount < 0 ? 0 : rasterRowCount,
        rasterColumnCount < 0 ? 0 : rasterColumnCount,
        rasterRowStride,
        quantizeScale,
        *objectTranslation,
        nativePayload,
        &nativeTrace);

    if (outTrace != nullptr) {
        outTrace->modeGateAccepted = nativeTrace.modeGateAccepted;
        outTrace->quantizedWindowAccepted = nativeTrace.quantizedWindowAccepted;
        outTrace->scratchAllocationRequired = nativeTrace.scratchAllocationRequired;
        outTrace->enteredPrepassPointLoops = nativeTrace.enteredPrepassPointLoops;
        outTrace->enteredRasterCellLoops = nativeTrace.enteredRasterCellLoops;
        outTrace->quantizeScale = nativeTrace.quantizeScale;
        outTrace->appliedTranslation = nativeTrace.appliedTranslation;
        outTrace->translatedSupportPointMin = nativeTrace.translatedSupportPointMin;
        outTrace->translatedSupportPointMax = nativeTrace.translatedSupportPointMax;
        outTrace->translatedAnchorPoint0 = nativeTrace.translatedAnchorPoint0;
        outTrace->translatedAnchorPoint1 = nativeTrace.translatedAnchorPoint1;
        outTrace->translatedFirstPlaneDistance = nativeTrace.translatedFirstPlaneDistance;
        outTrace->rawWindow.rowMin = nativeTrace.rawWindow.rowMin;
        outTrace->rawWindow.columnMin = nativeTrace.rawWindow.columnMin;
        outTrace->rawWindow.rowMax = nativeTrace.rawWindow.rowMax;
        outTrace->rawWindow.columnMax = nativeTrace.rawWindow.columnMax;
        outTrace->clippedWindow.rowMin = nativeTrace.clippedWindow.rowMin;
        outTrace->clippedWindow.columnMin = nativeTrace.clippedWindow.columnMin;
        outTrace->clippedWindow.rowMax = nativeTrace.clippedWindow.rowMax;
        outTrace->clippedWindow.columnMax = nativeTrace.clippedWindow.columnMax;
        outTrace->scratchByteCount = nativeTrace.scratchByteCount;
        outTrace->prepassPointCountX = nativeTrace.prepassPointCountX;
        outTrace->prepassPointCountY = nativeTrace.prepassPointCountY;
        outTrace->rasterCellCountX = nativeTrace.rasterCellCountX;
        outTrace->rasterCellCountY = nativeTrace.rasterCellCountY;
        outTrace->pointStartIndex = nativeTrace.pointStartIndex;
        outTrace->pointRowAdvance = nativeTrace.pointRowAdvance;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterConsumerBody(
    uint32_t modeWord,
    int rasterRowCount,
    int rasterColumnCount,
    int pointGridRowStride,
    int cellModeRowStride,
    float quantizeScale,
    uint32_t cellModeMaskFlags,
    uint32_t callerContextToken,
    uint32_t rasterSourceToken,
    uint32_t inputQueueEntryCount,
    uint32_t inputScratchWordCount,
    uint32_t deferredCleanupListPresent,
    const G3D::Vector3* objectTranslation,
    const ExportSelectorObjectRasterPayload* sourcePayload,
    const G3D::Vector3* pointGrid,
    int pointGridPointCount,
    const uint8_t* cellModes,
    int cellModeCount,
    uint32_t* outPointOutcodes,
    int outPointOutcodeCapacity,
    uint16_t* outScratchWords,
    int outScratchWordCapacity,
    ExportSelectorObjectRasterQueueEntry* outEntry,
    ExportSelectorObjectRasterBodyTrace* outTrace)
{
    if (objectTranslation == nullptr || sourcePayload == nullptr) {
        if (outEntry != nullptr) {
            *outEntry = ExportSelectorObjectRasterQueueEntry{};
        }

        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterBodyTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPayload nativePayload{};
    for (size_t index = 0; index < nativePayload.planes.size(); ++index) {
        nativePayload.planes[index] = sourcePayload->planes[index];
    }

    for (size_t index = 0; index < nativePayload.supportPoints.size(); ++index) {
        nativePayload.supportPoints[index] = sourcePayload->supportPoints[index];
    }

    nativePayload.anchorPoint0 = sourcePayload->anchorPoint0;
    nativePayload.anchorPoint1 = sourcePayload->anchorPoint1;

    WoWCollision::SelectorObjectRasterQueueEntry nativeEntry{};
    WoWCollision::SelectorObjectRasterBodyTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterConsumerBody(
        modeWord,
        rasterRowCount < 0 ? 0 : rasterRowCount,
        rasterColumnCount < 0 ? 0 : rasterColumnCount,
        pointGridRowStride,
        cellModeRowStride,
        quantizeScale,
        cellModeMaskFlags,
        callerContextToken,
        rasterSourceToken,
        inputQueueEntryCount,
        inputScratchWordCount,
        deferredCleanupListPresent != 0u,
        *objectTranslation,
        nativePayload,
        pointGrid,
        pointGridPointCount < 0 ? 0u : static_cast<uint32_t>(pointGridPointCount),
        cellModes,
        cellModeCount < 0 ? 0u : static_cast<uint32_t>(cellModeCount),
        outPointOutcodes,
        outPointOutcodeCapacity < 0 ? 0u : static_cast<uint32_t>(outPointOutcodeCapacity),
        outScratchWords,
        outScratchWordCapacity < 0 ? 0u : static_cast<uint32_t>(outScratchWordCapacity),
        outEntry != nullptr ? &nativeEntry : nullptr,
        &nativeTrace);

    if (outEntry != nullptr) {
        outEntry->allocated = nativeEntry.allocated;
        outEntry->callerContextToken = nativeEntry.callerContextToken;
        outEntry->rasterSourceToken = nativeEntry.rasterSourceToken;
        outEntry->scratchWordStart = nativeEntry.scratchWordStart;
        outEntry->scratchWordReserved = nativeEntry.scratchWordReserved;
        outEntry->appendedWordCount = nativeEntry.appendedWordCount;
        outEntry->appendedTriangleCount = nativeEntry.appendedTriangleCount;
        outEntry->minAppendedWord = nativeEntry.minAppendedWord;
        outEntry->maxAppendedWord = nativeEntry.maxAppendedWord;
        outEntry->scratchBufferPresent = nativeEntry.scratchBufferPresent;
    }

    if (outTrace != nullptr) {
        outTrace->prefix.modeGateAccepted = nativeTrace.prefix.modeGateAccepted;
        outTrace->prefix.quantizedWindowAccepted = nativeTrace.prefix.quantizedWindowAccepted;
        outTrace->prefix.scratchAllocationRequired = nativeTrace.prefix.scratchAllocationRequired;
        outTrace->prefix.enteredPrepassPointLoops = nativeTrace.prefix.enteredPrepassPointLoops;
        outTrace->prefix.enteredRasterCellLoops = nativeTrace.prefix.enteredRasterCellLoops;
        outTrace->prefix.quantizeScale = nativeTrace.prefix.quantizeScale;
        outTrace->prefix.appliedTranslation = nativeTrace.prefix.appliedTranslation;
        outTrace->prefix.translatedSupportPointMin = nativeTrace.prefix.translatedSupportPointMin;
        outTrace->prefix.translatedSupportPointMax = nativeTrace.prefix.translatedSupportPointMax;
        outTrace->prefix.translatedAnchorPoint0 = nativeTrace.prefix.translatedAnchorPoint0;
        outTrace->prefix.translatedAnchorPoint1 = nativeTrace.prefix.translatedAnchorPoint1;
        outTrace->prefix.translatedFirstPlaneDistance = nativeTrace.prefix.translatedFirstPlaneDistance;
        outTrace->prefix.rawWindow.rowMin = nativeTrace.prefix.rawWindow.rowMin;
        outTrace->prefix.rawWindow.columnMin = nativeTrace.prefix.rawWindow.columnMin;
        outTrace->prefix.rawWindow.rowMax = nativeTrace.prefix.rawWindow.rowMax;
        outTrace->prefix.rawWindow.columnMax = nativeTrace.prefix.rawWindow.columnMax;
        outTrace->prefix.clippedWindow.rowMin = nativeTrace.prefix.clippedWindow.rowMin;
        outTrace->prefix.clippedWindow.columnMin = nativeTrace.prefix.clippedWindow.columnMin;
        outTrace->prefix.clippedWindow.rowMax = nativeTrace.prefix.clippedWindow.rowMax;
        outTrace->prefix.clippedWindow.columnMax = nativeTrace.prefix.clippedWindow.columnMax;
        outTrace->prefix.scratchByteCount = nativeTrace.prefix.scratchByteCount;
        outTrace->prefix.prepassPointCountX = nativeTrace.prefix.prepassPointCountX;
        outTrace->prefix.prepassPointCountY = nativeTrace.prefix.prepassPointCountY;
        outTrace->prefix.rasterCellCountX = nativeTrace.prefix.rasterCellCountX;
        outTrace->prefix.rasterCellCountY = nativeTrace.prefix.rasterCellCountY;
        outTrace->prefix.pointStartIndex = nativeTrace.prefix.pointStartIndex;
        outTrace->prefix.pointRowAdvance = nativeTrace.prefix.pointRowAdvance;
        outTrace->returnedAnyCandidate = nativeTrace.returnedAnyCandidate;
        outTrace->prepassPointWrites = nativeTrace.prepassPointWrites;
        outTrace->visitedRasterCellCount = nativeTrace.visitedRasterCellCount;
        outTrace->skippedByCellModeValue = nativeTrace.skippedByCellModeValue;
        outTrace->skippedByCellModeMask = nativeTrace.skippedByCellModeMask;
        outTrace->rejectedTriangleCount = nativeTrace.rejectedTriangleCount;
        outTrace->acceptedTriangleCount = nativeTrace.acceptedTriangleCount;
        outTrace->queueCountBefore = nativeTrace.queueCountBefore;
        outTrace->queueCountAfter = nativeTrace.queueCountAfter;
        outTrace->scratchWordsBefore = nativeTrace.scratchWordsBefore;
        outTrace->scratchWordsAfter = nativeTrace.scratchWordsAfter;
        outTrace->queueLimitOverflowed = nativeTrace.queueLimitOverflowed;
        outTrace->scratchOverflowed = nativeTrace.scratchOverflowed;
        outTrace->appendedWordCount = nativeTrace.appendedWordCount;
        outTrace->appendedTriangleCount = nativeTrace.appendedTriangleCount;
        outTrace->finalQueueEntryListSpliceLinked = nativeTrace.finalQueueEntryListSpliceLinked;
        outTrace->normalCleanupLinked = nativeTrace.normalCleanupLinked;
        outTrace->failureCleanupExecuted = nativeTrace.failureCleanupExecuted;
        outTrace->failureCleanupDestroyedPayloadBlocks = nativeTrace.failureCleanupDestroyedPayloadBlocks;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterPrefixFailureCleanupExit(
    uint32_t prefixAccepted,
    const ExportSelectorObjectRasterPrefixTrace* prefix,
    ExportSelectorObjectRasterPrefixFailureCleanupTrace* outTrace)
{
    if (prefix == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterPrefixFailureCleanupTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPrefixTrace nativePrefix{};
    nativePrefix.modeGateAccepted = prefix->modeGateAccepted;
    nativePrefix.quantizedWindowAccepted = prefix->quantizedWindowAccepted;
    nativePrefix.scratchAllocationRequired = prefix->scratchAllocationRequired;
    nativePrefix.enteredPrepassPointLoops = prefix->enteredPrepassPointLoops;
    nativePrefix.enteredRasterCellLoops = prefix->enteredRasterCellLoops;
    nativePrefix.quantizeScale = prefix->quantizeScale;
    nativePrefix.appliedTranslation = prefix->appliedTranslation;
    nativePrefix.translatedSupportPointMin = prefix->translatedSupportPointMin;
    nativePrefix.translatedSupportPointMax = prefix->translatedSupportPointMax;
    nativePrefix.translatedAnchorPoint0 = prefix->translatedAnchorPoint0;
    nativePrefix.translatedAnchorPoint1 = prefix->translatedAnchorPoint1;
    nativePrefix.translatedFirstPlaneDistance = prefix->translatedFirstPlaneDistance;
    nativePrefix.rawWindow.rowMin = prefix->rawWindow.rowMin;
    nativePrefix.rawWindow.columnMin = prefix->rawWindow.columnMin;
    nativePrefix.rawWindow.rowMax = prefix->rawWindow.rowMax;
    nativePrefix.rawWindow.columnMax = prefix->rawWindow.columnMax;
    nativePrefix.clippedWindow.rowMin = prefix->clippedWindow.rowMin;
    nativePrefix.clippedWindow.columnMin = prefix->clippedWindow.columnMin;
    nativePrefix.clippedWindow.rowMax = prefix->clippedWindow.rowMax;
    nativePrefix.clippedWindow.columnMax = prefix->clippedWindow.columnMax;
    nativePrefix.scratchByteCount = prefix->scratchByteCount;
    nativePrefix.prepassPointCountX = prefix->prepassPointCountX;
    nativePrefix.prepassPointCountY = prefix->prepassPointCountY;
    nativePrefix.rasterCellCountX = prefix->rasterCellCountX;
    nativePrefix.rasterCellCountY = prefix->rasterCellCountY;
    nativePrefix.pointStartIndex = prefix->pointStartIndex;
    nativePrefix.pointRowAdvance = prefix->pointRowAdvance;

    WoWCollision::SelectorObjectRasterPrefixFailureCleanupTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterPrefixFailureCleanupExit(
        prefixAccepted,
        nativePrefix,
        &nativeTrace);

    if (outTrace != nullptr) {
        outTrace->prefixAccepted = nativeTrace.prefixAccepted;
        outTrace->modeGateAccepted = nativeTrace.modeGateAccepted;
        outTrace->failureCleanupExecuted = nativeTrace.failureCleanupExecuted;
        outTrace->failureCleanupDestroyedPayloadBlocks = nativeTrace.failureCleanupDestroyedPayloadBlocks;
        outTrace->returnedBeforePrepass = nativeTrace.returnedBeforePrepass;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterPrepassOutcodeLoop(
    const ExportSelectorObjectRasterPrefixTrace* prefix,
    int pointGridRowStride,
    const G3D::Vector3* objectTranslation,
    const ExportSelectorObjectRasterPayload* sourcePayload,
    const G3D::Vector3* pointGrid,
    int pointGridPointCount,
    uint32_t* outPointOutcodes,
    int outPointOutcodeCapacity,
    ExportSelectorObjectRasterPrepassTrace* outTrace)
{
    if (prefix == nullptr || objectTranslation == nullptr || sourcePayload == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterPrepassTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPrefixTrace nativePrefix{};
    nativePrefix.modeGateAccepted = prefix->modeGateAccepted;
    nativePrefix.quantizedWindowAccepted = prefix->quantizedWindowAccepted;
    nativePrefix.scratchAllocationRequired = prefix->scratchAllocationRequired;
    nativePrefix.enteredPrepassPointLoops = prefix->enteredPrepassPointLoops;
    nativePrefix.enteredRasterCellLoops = prefix->enteredRasterCellLoops;
    nativePrefix.quantizeScale = prefix->quantizeScale;
    nativePrefix.appliedTranslation = prefix->appliedTranslation;
    nativePrefix.translatedSupportPointMin = prefix->translatedSupportPointMin;
    nativePrefix.translatedSupportPointMax = prefix->translatedSupportPointMax;
    nativePrefix.translatedAnchorPoint0 = prefix->translatedAnchorPoint0;
    nativePrefix.translatedAnchorPoint1 = prefix->translatedAnchorPoint1;
    nativePrefix.translatedFirstPlaneDistance = prefix->translatedFirstPlaneDistance;
    nativePrefix.rawWindow.rowMin = prefix->rawWindow.rowMin;
    nativePrefix.rawWindow.columnMin = prefix->rawWindow.columnMin;
    nativePrefix.rawWindow.rowMax = prefix->rawWindow.rowMax;
    nativePrefix.rawWindow.columnMax = prefix->rawWindow.columnMax;
    nativePrefix.clippedWindow.rowMin = prefix->clippedWindow.rowMin;
    nativePrefix.clippedWindow.columnMin = prefix->clippedWindow.columnMin;
    nativePrefix.clippedWindow.rowMax = prefix->clippedWindow.rowMax;
    nativePrefix.clippedWindow.columnMax = prefix->clippedWindow.columnMax;
    nativePrefix.scratchByteCount = prefix->scratchByteCount;
    nativePrefix.prepassPointCountX = prefix->prepassPointCountX;
    nativePrefix.prepassPointCountY = prefix->prepassPointCountY;
    nativePrefix.rasterCellCountX = prefix->rasterCellCountX;
    nativePrefix.rasterCellCountY = prefix->rasterCellCountY;
    nativePrefix.pointStartIndex = prefix->pointStartIndex;
    nativePrefix.pointRowAdvance = prefix->pointRowAdvance;

    WoWCollision::SelectorObjectRasterPayload nativePayload{};
    for (size_t index = 0; index < nativePayload.planes.size(); ++index) {
        nativePayload.planes[index] = sourcePayload->planes[index];
    }

    for (size_t index = 0; index < nativePayload.supportPoints.size(); ++index) {
        nativePayload.supportPoints[index] = sourcePayload->supportPoints[index];
    }

    nativePayload.anchorPoint0 = sourcePayload->anchorPoint0;
    nativePayload.anchorPoint1 = sourcePayload->anchorPoint1;

    WoWCollision::TranslateSelectorSourceGeometry(
        -*objectTranslation,
        nativePayload.planes.data(),
        static_cast<uint32_t>(nativePayload.planes.size()),
        nativePayload.supportPoints.data(),
        static_cast<uint32_t>(nativePayload.supportPoints.size()),
        &nativePayload.anchorPoint0,
        &nativePayload.anchorPoint1);

    WoWCollision::SelectorObjectRasterPrepassTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterPrepassOutcodeLoop(
        nativePrefix,
        pointGridRowStride,
        nativePayload,
        pointGrid,
        pointGridPointCount < 0 ? 0u : static_cast<uint32_t>(pointGridPointCount),
        outPointOutcodes,
        outPointOutcodeCapacity < 0 ? 0u : static_cast<uint32_t>(outPointOutcodeCapacity),
        &nativeTrace);

    if (outTrace != nullptr) {
        outTrace->pointWriteCount = nativeTrace.pointWriteCount;
        outTrace->outputWriteCount = nativeTrace.outputWriteCount;
        outTrace->pointIndexOutOfRangeCount = nativeTrace.pointIndexOutOfRangeCount;
        outTrace->firstPointIndex = nativeTrace.firstPointIndex;
        outTrace->lastPointIndex = nativeTrace.lastPointIndex;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterPrepassComposition(
    const ExportSelectorObjectRasterPrefixTrace* prefix,
    const ExportSelectorObjectRasterPayload* sourcePayload,
    const G3D::Vector3* pointGrid,
    int pointGridPointCount,
    uint32_t* outPointOutcodes,
    int outPointOutcodeCapacity,
    ExportSelectorObjectRasterPrepassCompositionTrace* outTrace)
{
    if (prefix == nullptr || sourcePayload == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterPrepassCompositionTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPrefixTrace nativePrefix{};
    nativePrefix.modeGateAccepted = prefix->modeGateAccepted;
    nativePrefix.quantizedWindowAccepted = prefix->quantizedWindowAccepted;
    nativePrefix.scratchAllocationRequired = prefix->scratchAllocationRequired;
    nativePrefix.enteredPrepassPointLoops = prefix->enteredPrepassPointLoops;
    nativePrefix.enteredRasterCellLoops = prefix->enteredRasterCellLoops;
    nativePrefix.quantizeScale = prefix->quantizeScale;
    nativePrefix.appliedTranslation = prefix->appliedTranslation;
    nativePrefix.translatedSupportPointMin = prefix->translatedSupportPointMin;
    nativePrefix.translatedSupportPointMax = prefix->translatedSupportPointMax;
    nativePrefix.translatedAnchorPoint0 = prefix->translatedAnchorPoint0;
    nativePrefix.translatedAnchorPoint1 = prefix->translatedAnchorPoint1;
    nativePrefix.translatedFirstPlaneDistance = prefix->translatedFirstPlaneDistance;
    nativePrefix.rawWindow.rowMin = prefix->rawWindow.rowMin;
    nativePrefix.rawWindow.columnMin = prefix->rawWindow.columnMin;
    nativePrefix.rawWindow.rowMax = prefix->rawWindow.rowMax;
    nativePrefix.rawWindow.columnMax = prefix->rawWindow.columnMax;
    nativePrefix.clippedWindow.rowMin = prefix->clippedWindow.rowMin;
    nativePrefix.clippedWindow.columnMin = prefix->clippedWindow.columnMin;
    nativePrefix.clippedWindow.rowMax = prefix->clippedWindow.rowMax;
    nativePrefix.clippedWindow.columnMax = prefix->clippedWindow.columnMax;
    nativePrefix.scratchByteCount = prefix->scratchByteCount;
    nativePrefix.prepassPointCountX = prefix->prepassPointCountX;
    nativePrefix.prepassPointCountY = prefix->prepassPointCountY;
    nativePrefix.rasterCellCountX = prefix->rasterCellCountX;
    nativePrefix.rasterCellCountY = prefix->rasterCellCountY;
    nativePrefix.pointStartIndex = prefix->pointStartIndex;
    nativePrefix.pointRowAdvance = prefix->pointRowAdvance;

    WoWCollision::SelectorObjectRasterPayload nativePayload{};
    for (size_t index = 0; index < nativePayload.planes.size(); ++index) {
        nativePayload.planes[index] = sourcePayload->planes[index];
    }

    for (size_t index = 0; index < nativePayload.supportPoints.size(); ++index) {
        nativePayload.supportPoints[index] = sourcePayload->supportPoints[index];
    }

    nativePayload.anchorPoint0 = sourcePayload->anchorPoint0;
    nativePayload.anchorPoint1 = sourcePayload->anchorPoint1;

    WoWCollision::SelectorObjectRasterPrepassCompositionTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterPrepassComposition(
        nativePrefix,
        nativePayload,
        prefix->pointRowAdvance + static_cast<int32_t>(prefix->prepassPointCountX),
        pointGrid,
        pointGridPointCount < 0 ? 0u : static_cast<uint32_t>(pointGridPointCount),
        outPointOutcodes,
        outPointOutcodeCapacity < 0 ? 0u : static_cast<uint32_t>(outPointOutcodeCapacity),
        &nativeTrace);

    if (outTrace != nullptr) {
        outTrace->prepass.pointWriteCount = nativeTrace.prepass.pointWriteCount;
        outTrace->prepass.outputWriteCount = nativeTrace.prepass.outputWriteCount;
        outTrace->prepass.pointIndexOutOfRangeCount = nativeTrace.prepass.pointIndexOutOfRangeCount;
        outTrace->prepass.firstPointIndex = nativeTrace.prepass.firstPointIndex;
        outTrace->prepass.lastPointIndex = nativeTrace.prepass.lastPointIndex;
        outTrace->translatedAnchorPoint0 = nativeTrace.translatedAnchorPoint0;
        outTrace->translatedAnchorPoint1 = nativeTrace.translatedAnchorPoint1;
        outTrace->translatedFirstPlaneDistance = nativeTrace.translatedFirstPlaneDistance;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterCellIteration(
    const ExportSelectorObjectRasterPrefixTrace* prefix,
    uint32_t localRow,
    uint32_t localColumn,
    int pointGridRowStride,
    int cellModeRowStride,
    uint32_t cellModeMaskFlags,
    uint32_t callerContextToken,
    uint32_t rasterSourceToken,
    uint32_t inputQueueEntryCount,
    uint32_t inputScratchWordCount,
    const uint8_t* cellModes,
    int cellModeCount,
    const uint32_t* pointOutcodes,
    int pointOutcodeCapacity,
    uint16_t* outScratchWords,
    int outScratchWordCapacity,
    ExportSelectorObjectRasterQueueEntry* ioEntry,
    ExportSelectorObjectRasterCellIterationTrace* outTrace)
{
    if (prefix == nullptr || ioEntry == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterCellIterationTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPrefixTrace nativePrefix{};
    nativePrefix.modeGateAccepted = prefix->modeGateAccepted;
    nativePrefix.quantizedWindowAccepted = prefix->quantizedWindowAccepted;
    nativePrefix.scratchAllocationRequired = prefix->scratchAllocationRequired;
    nativePrefix.enteredPrepassPointLoops = prefix->enteredPrepassPointLoops;
    nativePrefix.enteredRasterCellLoops = prefix->enteredRasterCellLoops;
    nativePrefix.quantizeScale = prefix->quantizeScale;
    nativePrefix.appliedTranslation = prefix->appliedTranslation;
    nativePrefix.translatedSupportPointMin = prefix->translatedSupportPointMin;
    nativePrefix.translatedSupportPointMax = prefix->translatedSupportPointMax;
    nativePrefix.translatedAnchorPoint0 = prefix->translatedAnchorPoint0;
    nativePrefix.translatedAnchorPoint1 = prefix->translatedAnchorPoint1;
    nativePrefix.translatedFirstPlaneDistance = prefix->translatedFirstPlaneDistance;
    nativePrefix.rawWindow.rowMin = prefix->rawWindow.rowMin;
    nativePrefix.rawWindow.columnMin = prefix->rawWindow.columnMin;
    nativePrefix.rawWindow.rowMax = prefix->rawWindow.rowMax;
    nativePrefix.rawWindow.columnMax = prefix->rawWindow.columnMax;
    nativePrefix.clippedWindow.rowMin = prefix->clippedWindow.rowMin;
    nativePrefix.clippedWindow.columnMin = prefix->clippedWindow.columnMin;
    nativePrefix.clippedWindow.rowMax = prefix->clippedWindow.rowMax;
    nativePrefix.clippedWindow.columnMax = prefix->clippedWindow.columnMax;
    nativePrefix.scratchByteCount = prefix->scratchByteCount;
    nativePrefix.prepassPointCountX = prefix->prepassPointCountX;
    nativePrefix.prepassPointCountY = prefix->prepassPointCountY;
    nativePrefix.rasterCellCountX = prefix->rasterCellCountX;
    nativePrefix.rasterCellCountY = prefix->rasterCellCountY;
    nativePrefix.pointStartIndex = prefix->pointStartIndex;
    nativePrefix.pointRowAdvance = prefix->pointRowAdvance;

    WoWCollision::SelectorObjectRasterQueueEntry nativeEntry{};
    nativeEntry.allocated = ioEntry->allocated;
    nativeEntry.callerContextToken = ioEntry->callerContextToken;
    nativeEntry.rasterSourceToken = ioEntry->rasterSourceToken;
    nativeEntry.scratchWordStart = ioEntry->scratchWordStart;
    nativeEntry.scratchWordReserved = ioEntry->scratchWordReserved;
    nativeEntry.appendedWordCount = ioEntry->appendedWordCount;
    nativeEntry.appendedTriangleCount = ioEntry->appendedTriangleCount;
    nativeEntry.minAppendedWord = ioEntry->minAppendedWord;
    nativeEntry.maxAppendedWord = ioEntry->maxAppendedWord;
    nativeEntry.scratchBufferPresent = ioEntry->scratchBufferPresent;

    WoWCollision::SelectorObjectRasterCellIterationTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterCellIteration(
        nativePrefix,
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
        cellModeCount < 0 ? 0u : static_cast<uint32_t>(cellModeCount),
        pointOutcodes,
        pointOutcodeCapacity < 0 ? 0u : static_cast<uint32_t>(pointOutcodeCapacity),
        outScratchWords,
        outScratchWordCapacity < 0 ? 0u : static_cast<uint32_t>(outScratchWordCapacity),
        nativeEntry,
        &nativeTrace);

    ioEntry->allocated = nativeEntry.allocated;
    ioEntry->callerContextToken = nativeEntry.callerContextToken;
    ioEntry->rasterSourceToken = nativeEntry.rasterSourceToken;
    ioEntry->scratchWordStart = nativeEntry.scratchWordStart;
    ioEntry->scratchWordReserved = nativeEntry.scratchWordReserved;
    ioEntry->appendedWordCount = nativeEntry.appendedWordCount;
    ioEntry->appendedTriangleCount = nativeEntry.appendedTriangleCount;
    ioEntry->minAppendedWord = nativeEntry.minAppendedWord;
    ioEntry->maxAppendedWord = nativeEntry.maxAppendedWord;
    ioEntry->scratchBufferPresent = nativeEntry.scratchBufferPresent;

    if (outTrace != nullptr) {
        outTrace->visitedRasterCell = nativeTrace.visitedRasterCell;
        outTrace->skippedByCellModeValue = nativeTrace.skippedByCellModeValue;
        outTrace->skippedByCellModeMask = nativeTrace.skippedByCellModeMask;
        outTrace->returnedAnyCandidate = nativeTrace.returnedAnyCandidate;
        outTrace->rejectedTriangleCount = nativeTrace.rejectedTriangleCount;
        outTrace->acceptedTriangleCount = nativeTrace.acceptedTriangleCount;
        outTrace->queueCountBefore = nativeTrace.queueCountBefore;
        outTrace->queueCountAfter = nativeTrace.queueCountAfter;
        outTrace->scratchWordsBefore = nativeTrace.scratchWordsBefore;
        outTrace->scratchWordsAfter = nativeTrace.scratchWordsAfter;
        outTrace->queueLimitOverflowed = nativeTrace.queueLimitOverflowed;
        outTrace->scratchOverflowed = nativeTrace.scratchOverflowed;
        outTrace->entryAllocatedBefore = nativeTrace.entryAllocatedBefore;
        outTrace->entryAllocatedAfter = nativeTrace.entryAllocatedAfter;
        outTrace->cellIndex = nativeTrace.cellIndex;
        outTrace->cellModeNibble = nativeTrace.cellModeNibble;
        outTrace->localPointBase = nativeTrace.localPointBase;
        outTrace->worldPointBase = nativeTrace.worldPointBase;
        outTrace->appendedWordCountBefore = nativeTrace.appendedWordCountBefore;
        outTrace->appendedWordCountAfter = nativeTrace.appendedWordCountAfter;
        outTrace->appendedTriangleCountBefore = nativeTrace.appendedTriangleCountBefore;
        outTrace->appendedTriangleCountAfter = nativeTrace.appendedTriangleCountAfter;
        outTrace->triangleRejectMask = nativeTrace.triangleRejectMask;
        outTrace->triangleAcceptMask = nativeTrace.triangleAcceptMask;
    }

    return result;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorObjectRasterCellLoopAggregation(
    const ExportSelectorObjectRasterPrefixTrace* prefix,
    int pointGridRowStride,
    int cellModeRowStride,
    uint32_t cellModeMaskFlags,
    uint32_t callerContextToken,
    uint32_t rasterSourceToken,
    uint32_t inputQueueEntryCount,
    uint32_t inputScratchWordCount,
    uint32_t deferredCleanupListPresent,
    const uint8_t* cellModes,
    int cellModeCount,
    const uint32_t* pointOutcodes,
    int pointOutcodeCapacity,
    uint16_t* outScratchWords,
    int outScratchWordCapacity,
    ExportSelectorObjectRasterAggregationTrace* outTrace)
{
    if (prefix == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = ExportSelectorObjectRasterAggregationTrace{};
        }

        return 0u;
    }

    WoWCollision::SelectorObjectRasterPrefixTrace nativePrefix{};
    nativePrefix.modeGateAccepted = prefix->modeGateAccepted;
    nativePrefix.quantizedWindowAccepted = prefix->quantizedWindowAccepted;
    nativePrefix.scratchAllocationRequired = prefix->scratchAllocationRequired;
    nativePrefix.enteredPrepassPointLoops = prefix->enteredPrepassPointLoops;
    nativePrefix.enteredRasterCellLoops = prefix->enteredRasterCellLoops;
    nativePrefix.quantizeScale = prefix->quantizeScale;
    nativePrefix.appliedTranslation = prefix->appliedTranslation;
    nativePrefix.translatedSupportPointMin = prefix->translatedSupportPointMin;
    nativePrefix.translatedSupportPointMax = prefix->translatedSupportPointMax;
    nativePrefix.translatedAnchorPoint0 = prefix->translatedAnchorPoint0;
    nativePrefix.translatedAnchorPoint1 = prefix->translatedAnchorPoint1;
    nativePrefix.translatedFirstPlaneDistance = prefix->translatedFirstPlaneDistance;
    nativePrefix.rawWindow.rowMin = prefix->rawWindow.rowMin;
    nativePrefix.rawWindow.columnMin = prefix->rawWindow.columnMin;
    nativePrefix.rawWindow.rowMax = prefix->rawWindow.rowMax;
    nativePrefix.rawWindow.columnMax = prefix->rawWindow.columnMax;
    nativePrefix.clippedWindow.rowMin = prefix->clippedWindow.rowMin;
    nativePrefix.clippedWindow.columnMin = prefix->clippedWindow.columnMin;
    nativePrefix.clippedWindow.rowMax = prefix->clippedWindow.rowMax;
    nativePrefix.clippedWindow.columnMax = prefix->clippedWindow.columnMax;
    nativePrefix.scratchByteCount = prefix->scratchByteCount;
    nativePrefix.prepassPointCountX = prefix->prepassPointCountX;
    nativePrefix.prepassPointCountY = prefix->prepassPointCountY;
    nativePrefix.rasterCellCountX = prefix->rasterCellCountX;
    nativePrefix.rasterCellCountY = prefix->rasterCellCountY;
    nativePrefix.pointStartIndex = prefix->pointStartIndex;
    nativePrefix.pointRowAdvance = prefix->pointRowAdvance;

    WoWCollision::SelectorObjectRasterAggregationTrace nativeTrace{};
    const uint32_t result = WoWCollision::EvaluateSelectorObjectRasterCellLoopAggregation(
        nativePrefix,
        pointGridRowStride,
        cellModeRowStride,
        cellModeMaskFlags,
        callerContextToken,
        rasterSourceToken,
        inputQueueEntryCount,
        inputScratchWordCount,
        deferredCleanupListPresent != 0u,
        cellModes,
        cellModeCount < 0 ? 0u : static_cast<uint32_t>(cellModeCount),
        pointOutcodes,
        pointOutcodeCapacity < 0 ? 0u : static_cast<uint32_t>(pointOutcodeCapacity),
        outScratchWords,
        outScratchWordCapacity < 0 ? 0u : static_cast<uint32_t>(outScratchWordCapacity),
        &nativeTrace);

    if (outTrace != nullptr) {
        outTrace->entry.allocated = nativeTrace.entry.allocated;
        outTrace->entry.callerContextToken = nativeTrace.entry.callerContextToken;
        outTrace->entry.rasterSourceToken = nativeTrace.entry.rasterSourceToken;
        outTrace->entry.scratchWordStart = nativeTrace.entry.scratchWordStart;
        outTrace->entry.scratchWordReserved = nativeTrace.entry.scratchWordReserved;
        outTrace->entry.appendedWordCount = nativeTrace.entry.appendedWordCount;
        outTrace->entry.appendedTriangleCount = nativeTrace.entry.appendedTriangleCount;
        outTrace->entry.minAppendedWord = nativeTrace.entry.minAppendedWord;
        outTrace->entry.maxAppendedWord = nativeTrace.entry.maxAppendedWord;
        outTrace->entry.scratchBufferPresent = nativeTrace.entry.scratchBufferPresent;
        outTrace->returnedAnyCandidate = nativeTrace.returnedAnyCandidate;
        outTrace->visitedRasterCellCount = nativeTrace.visitedRasterCellCount;
        outTrace->skippedByCellModeValue = nativeTrace.skippedByCellModeValue;
        outTrace->skippedByCellModeMask = nativeTrace.skippedByCellModeMask;
        outTrace->rejectedTriangleCount = nativeTrace.rejectedTriangleCount;
        outTrace->acceptedTriangleCount = nativeTrace.acceptedTriangleCount;
        outTrace->queueCountBefore = nativeTrace.queueCountBefore;
        outTrace->queueCountAfter = nativeTrace.queueCountAfter;
        outTrace->scratchWordsBefore = nativeTrace.scratchWordsBefore;
        outTrace->scratchWordsAfter = nativeTrace.scratchWordsAfter;
        outTrace->queueLimitOverflowed = nativeTrace.queueLimitOverflowed;
        outTrace->scratchOverflowed = nativeTrace.scratchOverflowed;
        outTrace->appendedWordCount = nativeTrace.appendedWordCount;
        outTrace->appendedTriangleCount = nativeTrace.appendedTriangleCount;
        outTrace->finalQueueEntryListSpliceLinked = nativeTrace.finalQueueEntryListSpliceLinked;
        outTrace->normalCleanupLinked = nativeTrace.normalCleanupLinked;
    }

    return result;
}

}
