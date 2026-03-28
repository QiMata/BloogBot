#include "SelectorChosenContact.h"

#include <algorithm>
#include <cmath>
#include <vector>

namespace
{
    constexpr uint32_t WOW_MOVEMENTFLAG_SWIMMING = 0x00200000u;
    constexpr float WOW_SELECTOR_DIRECTION_SETUP_EPSILON = 0.0013888889225199819f; // 0x8026BC

    uint32_t DetermineSelectorPairSource(const WoWCollision::SelectorPairConsumerTrace& consumerTrace)
    {
        if (consumerTrace.preservedInputMove != 0u) {
            return WoWCollision::SELECTOR_PAIR_SOURCE_PRESERVED_INPUT;
        }

        if (consumerTrace.zeroedMoveOnRankingFailure != 0u) {
            return WoWCollision::SELECTOR_PAIR_SOURCE_RANKING_FAILURE;
        }

        if (consumerTrace.returnedDirectPair != 0u) {
            return WoWCollision::SELECTOR_PAIR_SOURCE_DIRECT;
        }

        if (consumerTrace.directGateState != 0u && consumerTrace.returnedZeroPair != 0u) {
            return WoWCollision::SELECTOR_PAIR_SOURCE_DIRECT_ZERO;
        }

        if (consumerTrace.alternateUnitZState != 0u && consumerTrace.returnedZeroPair != 0u) {
            return WoWCollision::SELECTOR_PAIR_SOURCE_ALTERNATE_UNIT_Z_ZERO;
        }

        if (consumerTrace.returnedAlternatePair != 0u) {
            return WoWCollision::SELECTOR_PAIR_SOURCE_ALTERNATE;
        }

        return WoWCollision::SELECTOR_PAIR_SOURCE_NONE;
    }

    void CopySelectorPairsFromTerrainQueryPayloads(const std::vector<WoWCollision::TerrainQueryPairPayload>& inputPairs,
                                                   std::vector<WoWCollision::SelectorPair>& outPairs)
    {
        outPairs.resize(inputPairs.size());
        for (size_t pairIndex = 0; pairIndex < inputPairs.size(); ++pairIndex) {
            outPairs[pairIndex].first = inputPairs[pairIndex].first;
            outPairs[pairIndex].second = inputPairs[pairIndex].second;
        }
    }

    void CopyTerrainQueryPayloadsFromSelectorPairs(const WoWCollision::SelectorPair* inputPairs,
                                                   uint32_t inputCount,
                                                   std::vector<WoWCollision::TerrainQueryPairPayload>& outPairs)
    {
        outPairs.resize(inputCount);
        for (uint32_t pairIndex = 0u; pairIndex < inputCount; ++pairIndex) {
            outPairs[static_cast<size_t>(pairIndex)] = WoWCollision::TerrainQueryPairPayload{
                inputPairs != nullptr ? inputPairs[pairIndex].first : 0.0f,
                inputPairs != nullptr ? inputPairs[pairIndex].second : 0.0f,
            };
        }
    }
}

bool WoWCollision::EvaluateSelectorChosenPairForwarding(const SceneQuery::AABBContact& selectedContact,
                                                        const G3D::Vector3& currentPosition,
                                                        float requestedDistance,
                                                        const G3D::Vector3& inputMove,
                                                        bool useStandardWalkableThreshold,
                                                        bool directionRankingAccepted,
                                                        int32_t selectedIndex,
                                                        uint32_t selectedCount,
                                                        bool hasNegativeDiagonalCandidate,
                                                        float airborneTimeScalar,
                                                        float elapsedTimeScalar,
                                                        float horizontalSpeedScale,
                                                        bool hasUnitZCandidate,
                                                        const SelectorPair& directPair,
                                                        const SelectorPair& alternatePair,
                                                        SelectorPairForwardingTrace* outTrace)
{
    SelectorPairForwardingTrace trace{};

    const G3D::Vector3 scaledMove = inputMove * requestedDistance;
    const G3D::Vector3 projectedPosition = currentPosition + scaledMove;

    const SelectedContactThresholdGateResult thresholdGate = EvaluateSelectedContactThresholdGate(
        selectedContact,
        currentPosition,
        projectedPosition,
        useStandardWalkableThreshold);

    trace.directGateAccepted = thresholdGate.wouldUseDirectPair ? 1u : 0u;
    trace.currentPositionInsidePrism = thresholdGate.currentPositionInsidePrism ? 1u : 0u;
    trace.projectedPositionInsidePrism = thresholdGate.projectedPositionInsidePrism ? 1u : 0u;
    trace.thresholdSensitive = thresholdGate.thresholdSensitive ? 1u : 0u;
    trace.normalZ = thresholdGate.normalZ;

    const bool alternateUnitZFallbackGateAccepted = EvaluateSelectorAlternateUnitZFallbackGate(
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        requestedDistance);
    trace.alternateUnitZFallbackGateAccepted = alternateUnitZFallbackGateAccepted ? 1u : 0u;

    EvaluateSelectorPairConsumer(
        requestedDistance,
        inputMove,
        directionRankingAccepted,
        selectedIndex,
        selectedCount,
        thresholdGate.wouldUseDirectPair,
        hasNegativeDiagonalCandidate,
        alternateUnitZFallbackGateAccepted,
        hasUnitZCandidate,
        directPair,
        alternatePair,
        trace.consumerTrace);

    trace.pairSource = DetermineSelectorPairSource(trace.consumerTrace);

    if (outTrace) {
        *outTrace = trace;
    }

    return true;
}

int32_t WoWCollision::EvaluateSelectorChosenIndexPairBridge(const SceneQuery::AABBContact* selectedContacts,
                                                            uint32_t selectedContactCount,
                                                            const SelectorPair* directPairs,
                                                            uint32_t directPairCount,
                                                            const SelectorSupportPlane* candidatePlanes,
                                                            uint32_t candidatePlaneCount,
                                                            const G3D::Vector3& currentPosition,
                                                            float requestedDistance,
                                                            const G3D::Vector3& inputMove,
                                                            bool useStandardWalkableThreshold,
                                                            bool directionRankingAccepted,
                                                            int32_t selectedIndex,
                                                            float airborneTimeScalar,
                                                            float elapsedTimeScalar,
                                                            float horizontalSpeedScale,
                                                            const SelectorPair& alternatePair,
                                                            SelectorPair& outPair,
                                                            uint32_t& outDirectStateDword,
                                                            uint32_t& outAlternateUnitZStateDword,
                                                            SelectorChosenIndexPairBridgeTrace* outTrace)
{
    SelectorChosenIndexPairBridgeTrace trace{};
    trace.selectedIndexInRange =
        (selectedIndex >= 0 && static_cast<uint32_t>(selectedIndex) < selectedContactCount) ? 1u : 0u;
    trace.negativeDiagonalCandidateFound =
        HasSelectorCandidateWithNegativeDiagonalZ(candidatePlanes, candidatePlaneCount) ? 1u : 0u;
    trace.unitZCandidateFound =
        HasSelectorCandidateWithUnitZ(candidatePlanes, candidatePlaneCount) ? 1u : 0u;

    bool directGateAccepted = false;
    SelectorPair directPair{};
    if (trace.selectedIndexInRange != 0u && selectedContacts != nullptr) {
        trace.loadedSelectedContact = 1u;

        const SelectedContactThresholdGateResult thresholdGate = EvaluateSelectedContactThresholdGate(
            selectedContacts[selectedIndex],
            currentPosition,
            currentPosition + (inputMove * requestedDistance),
            useStandardWalkableThreshold);

        directGateAccepted = thresholdGate.wouldUseDirectPair;
        trace.currentPositionInsidePrism = thresholdGate.currentPositionInsidePrism ? 1u : 0u;
        trace.projectedPositionInsidePrism = thresholdGate.projectedPositionInsidePrism ? 1u : 0u;
        trace.thresholdSensitive = thresholdGate.thresholdSensitive ? 1u : 0u;
        trace.selectedNormalZ = thresholdGate.normalZ;
    }

    if (selectedIndex >= 0 &&
        directPairs != nullptr &&
        static_cast<uint32_t>(selectedIndex) < directPairCount) {
        directPair = directPairs[selectedIndex];
        trace.loadedDirectPair = 1u;
    }

    const bool alternateUnitZFallbackGateAccepted = EvaluateSelectorAlternateUnitZFallbackGate(
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        requestedDistance);
    trace.alternateUnitZFallbackGateAccepted = alternateUnitZFallbackGateAccepted ? 1u : 0u;

    EvaluateSelectorPairConsumer(
        requestedDistance,
        inputMove,
        directionRankingAccepted,
        selectedIndex,
        selectedContactCount,
        directGateAccepted,
        trace.negativeDiagonalCandidateFound != 0u,
        alternateUnitZFallbackGateAccepted,
        trace.unitZCandidateFound != 0u,
        directPair,
        alternatePair,
        trace.consumerTrace);
    trace.pairSource = DetermineSelectorPairSource(trace.consumerTrace);

    outPair = trace.consumerTrace.outputPair;
    outDirectStateDword = trace.consumerTrace.directGateState;
    outAlternateUnitZStateDword = trace.consumerTrace.alternateUnitZState;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.consumerTrace.returnCode;
}

int32_t WoWCollision::EvaluateSelectorChosenIndexPairProducerTransaction(const G3D::Vector3& defaultPosition,
                                                                         const G3D::Vector3* overridePosition,
                                                                         const G3D::Vector3& projectedPosition,
                                                                         uint32_t supportPlaneInitCount,
                                                                         uint32_t validationPlaneInitCount,
                                                                         uint32_t scratchPointZeroCount,
                                                                         const G3D::Vector3& testPoint,
                                                                         const G3D::Vector3& candidateDirection,
                                                                         float initialBestRatio,
                                                                         float collisionRadius,
                                                                         float boundingHeight,
                                                                         const G3D::Vector3& cachedBoundsMin,
                                                                         const G3D::Vector3& cachedBoundsMax,
                                                                         bool modelPropertyFlagSet,
                                                                         uint32_t movementFlags,
                                                                         float field20Value,
                                                                         bool rootTreeFlagSet,
                                                                         bool childTreeFlagSet,
                                                                         const SceneQuery::AABBContact* existingContacts,
                                                                         const SelectorPair* existingPairs,
                                                                         uint32_t existingCount,
                                                                         const SceneQuery::AABBContact* queryContacts,
                                                                         const SelectorPair* queryPairs,
                                                                         uint32_t queryCount,
                                                                         bool queryDispatchSucceeded,
                                                                         bool rankingAccepted,
                                                                         uint32_t rankingCandidateCount,
                                                                         int32_t rankingSelectedRecordIndex,
                                                                         float rankingReportedBestRatio,
                                                                         const SelectorSupportPlane* candidatePlanes,
                                                                         uint32_t candidatePlaneCount,
                                                                         const G3D::Vector3& currentPosition,
                                                                         float requestedDistance,
                                                                         const G3D::Vector3& inputMove,
                                                                         bool useStandardWalkableThreshold,
                                                                         float airborneTimeScalar,
                                                                         float elapsedTimeScalar,
                                                                         float horizontalSpeedScale,
                                                                         const SelectorPair& alternatePair,
                                                                         SelectorPair& outPair,
                                                                         uint32_t& outDirectStateDword,
                                                                         uint32_t& outAlternateUnitZStateDword,
                                                                         float& outReportedBestRatio,
                                                                         SelectorChosenIndexPairProducerTransactionTrace* outTrace)
{
    SelectorChosenIndexPairProducerTransactionTrace trace{};
    outPair = SelectorPair{};
    outDirectStateDword = 0u;
    outAlternateUnitZStateDword = 0u;
    outReportedBestRatio = 0.0f;

    std::vector<SceneQuery::AABBContact> selectedContacts;
    std::vector<SelectorPair> directPairs;
    SelectorChosenIndexPairVariableContainerTransactionTrace variableContainerTrace{};
    const bool variableContainerSucceeded = EvaluateSelectorChosenIndexPairVariableContainerTransaction(
        defaultPosition,
        overridePosition,
        projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        testPoint,
        candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingContacts,
        existingPairs,
        existingCount,
        queryContacts,
        queryPairs,
        queryCount,
        queryDispatchSucceeded,
        rankingAccepted,
        rankingCandidateCount,
        rankingSelectedRecordIndex,
        rankingReportedBestRatio,
        selectedContacts,
        directPairs,
        outReportedBestRatio,
        &variableContainerTrace);
    trace.variableTrace = variableContainerTrace.variableTrace;
    trace.containerTrace = variableContainerTrace.selectedContactContainerTrace.containerTrace;
    trace.usedAmbientCachedContainerWithoutQuery =
        variableContainerTrace.selectedContactContainerTrace.usedAmbientCachedContainerWithoutQuery;
    trace.usedProducedSelectedContactContainer =
        variableContainerTrace.selectedContactContainerTrace.usedProducedSelectedContactContainer;
    trace.containerInvoked = variableContainerTrace.selectedContactContainerTrace.containerInvoked;
    trace.zeroedOutputsOnVariableFailure = variableContainerTrace.zeroedOutputsOnVariableFailure;
    trace.zeroedOutputsOnContainerFailure = variableContainerTrace.zeroedOutputsOnContainerFailure;
    trace.outputReportedBestRatio = outReportedBestRatio;
    if (!variableContainerSucceeded) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0;
    }

    trace.bridgeInvoked = 1u;
    trace.bridgeSelectedContactCount = static_cast<uint32_t>(selectedContacts.size());
    trace.returnCode = EvaluateSelectorChosenIndexPairBridge(
        selectedContacts.empty() ? nullptr : selectedContacts.data(),
        static_cast<uint32_t>(selectedContacts.size()),
        directPairs.empty() ? nullptr : directPairs.data(),
        static_cast<uint32_t>(directPairs.size()),
        candidatePlanes,
        candidatePlaneCount,
        currentPosition,
        requestedDistance,
        inputMove,
        useStandardWalkableThreshold,
        rankingAccepted,
        rankingSelectedRecordIndex,
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        alternatePair,
        outPair,
        outDirectStateDword,
        outAlternateUnitZStateDword,
        &trace.bridgeTrace);

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.returnCode;
}

bool WoWCollision::EvaluateSelectorChosenIndexPairVariableContainerTransaction(
    const G3D::Vector3& defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3& projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3& testPoint,
    const G3D::Vector3& candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3& cachedBoundsMin,
    const G3D::Vector3& cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const SceneQuery::AABBContact* existingContacts,
    const SelectorPair* existingPairs,
    uint32_t existingCount,
    const SceneQuery::AABBContact* queryContacts,
    const SelectorPair* queryPairs,
    uint32_t queryCount,
    bool queryDispatchSucceeded,
    bool rankingAccepted,
    uint32_t rankingCandidateCount,
    int32_t rankingSelectedRecordIndex,
    float rankingReportedBestRatio,
    std::vector<SceneQuery::AABBContact>& outSelectedContacts,
    std::vector<SelectorPair>& outSelectedPairs,
    float& outReportedBestRatio,
    SelectorChosenIndexPairVariableContainerTransactionTrace* outTrace)
{
    SelectorChosenIndexPairVariableContainerTransactionTrace trace{};
    outSelectedContacts.clear();
    outSelectedPairs.clear();
    outReportedBestRatio = 0.0f;

    const bool variableSucceeded = EvaluateSelectorTriangleSourceVariableTransaction(
        defaultPosition,
        overridePosition,
        projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        testPoint,
        candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        queryDispatchSucceeded,
        rankingAccepted,
        rankingCandidateCount,
        rankingSelectedRecordIndex,
        rankingReportedBestRatio,
        trace.variableTrace);
    outReportedBestRatio = trace.variableTrace.outputReportedBestRatio;
    trace.outputReportedBestRatio = outReportedBestRatio;
    if (!variableSucceeded) {
        trace.zeroedOutputsOnVariableFailure = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    const bool containerSucceeded = EvaluateSelectorChosenIndexPairSelectedContactContainerTransaction(
        overridePosition,
        projectedPosition,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingContacts,
        existingPairs,
        existingCount,
        queryContacts,
        queryPairs,
        queryCount,
        queryDispatchSucceeded,
        outSelectedContacts,
        outSelectedPairs,
        &trace.selectedContactContainerTrace);
    trace.outputSelectedContactCount = static_cast<uint32_t>(outSelectedContacts.size());
    if (!containerSucceeded) {
        trace.zeroedOutputsOnContainerFailure = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    trace.returnedSuccess = 1u;
    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return true;
}

int32_t WoWCollision::EvaluateSelectorChosenIndexPairDirectionSetupProducerTransaction(
    const SelectorCandidateRecord* records,
    uint32_t recordCount,
    const G3D::Vector3& defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3& projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3& testPoint,
    const G3D::Vector3& candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3& cachedBoundsMin,
    const G3D::Vector3& cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const SceneQuery::AABBContact* existingContacts,
    const SelectorPair* existingPairs,
    uint32_t existingCount,
    const SceneQuery::AABBContact* queryContacts,
    const SelectorPair* queryPairs,
    uint32_t queryCount,
    bool queryDispatchSucceeded,
    float inputVerticalOffset,
    float swimVerticalOffsetScale,
    bool selectorBaseMatchesSwimReference,
    float requestedDistanceClamp,
    float horizontalRadius,
    const G3D::Vector3& currentPosition,
    float requestedDistance,
    const G3D::Vector3& inputMove,
    bool useStandardWalkableThreshold,
    float airborneTimeScalar,
    float elapsedTimeScalar,
    float horizontalSpeedScale,
    const SelectorPair& alternatePair,
    SelectorPair& outPair,
    uint32_t& outDirectStateDword,
    uint32_t& outAlternateUnitZStateDword,
    float& outReportedBestRatio,
    SelectorChosenIndexPairDirectionSetupProducerTransactionTrace* outTrace)
{
    SelectorChosenIndexPairDirectionSetupProducerTransactionTrace trace{};
    outPair = SelectorPair{};
    outDirectStateDword = 0u;
    outAlternateUnitZStateDword = 0u;
    outReportedBestRatio = 0.0f;

    std::array<SelectorSupportPlane, 5> candidatePlanes{};
    uint32_t candidatePlaneCount = 0u;
    int32_t selectedRecordIndex = -1;
    bool directionRankingAccepted = false;
    SceneQuery::AABBContact chosenContact{};
    SelectorPair chosenPair{};
    std::vector<SceneQuery::AABBContact> selectedContacts;
    std::vector<SelectorPair> directPairs;
    SelectorChosenIndexPairPreBridgeTransactionTrace preBridgeTrace{};
    const bool preBridgeSucceeded = EvaluateSelectorChosenIndexPairPreBridgeTransaction(
        records,
        recordCount,
        defaultPosition,
        overridePosition,
        projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        testPoint,
        candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingContacts,
        existingPairs,
        existingCount,
        queryContacts,
        queryPairs,
        queryCount,
        queryDispatchSucceeded,
        inputVerticalOffset,
        swimVerticalOffsetScale,
        selectorBaseMatchesSwimReference,
        requestedDistanceClamp,
        requestedDistance,
        horizontalRadius,
        candidatePlanes.data(),
        static_cast<uint32_t>(candidatePlanes.size()),
        candidatePlaneCount,
        selectedRecordIndex,
        directionRankingAccepted,
        chosenContact,
        chosenPair,
        selectedContacts,
        directPairs,
        outReportedBestRatio,
        &preBridgeTrace);
    trace.variableTrace = preBridgeTrace.variableTrace;
    trace.directionSetupTrace = preBridgeTrace.directionSetupTrace;
    trace.containerTrace = preBridgeTrace.selectedContactContainerTrace.containerTrace;
    trace.usedAmbientCachedContainerWithoutQuery =
        preBridgeTrace.selectedContactContainerTrace.usedAmbientCachedContainerWithoutQuery;
    trace.usedProducedSelectedContactContainer =
        preBridgeTrace.selectedContactContainerTrace.usedProducedSelectedContactContainer;
    trace.containerInvoked = preBridgeTrace.selectedContactContainerTrace.containerInvoked;
    trace.zeroedOutputsOnVariableFailure = preBridgeTrace.zeroedOutputsOnVariableFailure;
    trace.zeroedOutputsOnDirectionSetupFailure = preBridgeTrace.zeroedOutputsOnDirectionSetupFailure;
    trace.zeroedOutputsOnContainerFailure = preBridgeTrace.zeroedOutputsOnContainerFailure;
    trace.outputReportedBestRatio = outReportedBestRatio;
    if (!preBridgeSucceeded) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0;
    }

    trace.bridgeInvoked = 1u;
    trace.bridgeSelectedContactCount = static_cast<uint32_t>(selectedContacts.size());
    trace.returnCode = EvaluateSelectorChosenIndexPairBridge(
        selectedContacts.empty() ? nullptr : selectedContacts.data(),
        static_cast<uint32_t>(selectedContacts.size()),
        directPairs.empty() ? nullptr : directPairs.data(),
        static_cast<uint32_t>(directPairs.size()),
        candidatePlanes.data(),
        candidatePlaneCount,
        currentPosition,
        requestedDistance,
        inputMove,
        useStandardWalkableThreshold,
        directionRankingAccepted,
        selectedRecordIndex,
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        alternatePair,
        outPair,
        outDirectStateDword,
        outAlternateUnitZStateDword,
        &trace.bridgeTrace);

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.returnCode;
}

bool WoWCollision::EvaluateSelectorChosenIndexPairSelectedContactContainerTransaction(
    const G3D::Vector3* overridePosition,
    const G3D::Vector3& projectedPosition,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3& cachedBoundsMin,
    const G3D::Vector3& cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const SceneQuery::AABBContact* existingContacts,
    const SelectorPair* existingPairs,
    uint32_t existingCount,
    const SceneQuery::AABBContact* queryContacts,
    const SelectorPair* queryPairs,
    uint32_t queryCount,
    bool queryDispatchSucceeded,
    std::vector<SceneQuery::AABBContact>& outSelectedContacts,
    std::vector<SelectorPair>& outSelectedPairs,
    SelectorChosenIndexPairSelectedContactContainerTransactionTrace* outTrace)
{
    SelectorChosenIndexPairSelectedContactContainerTransactionTrace trace{};
    outSelectedContacts.clear();
    outSelectedPairs.clear();

    if ((existingCount != 0u && (existingContacts == nullptr || existingPairs == nullptr)) ||
        (queryCount != 0u && (queryContacts == nullptr || queryPairs == nullptr))) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    if (overridePosition != nullptr) {
        trace.usedAmbientCachedContainerWithoutQuery = 1u;
        if (existingCount != 0u) {
            outSelectedContacts.assign(existingContacts, existingContacts + existingCount);
            outSelectedPairs.assign(existingPairs, existingPairs + existingCount);
        }
        trace.outputSelectedContactCount = existingCount;
        trace.returnedSuccess = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return true;
    }

    trace.containerInvoked = 1u;

    std::vector<TerrainQueryPairPayload> existingPairPayloads;
    std::vector<TerrainQueryPairPayload> queryPairPayloads;
    CopyTerrainQueryPayloadsFromSelectorPairs(existingPairs, existingCount, existingPairPayloads);
    CopyTerrainQueryPayloadsFromSelectorPairs(queryPairs, queryCount, queryPairPayloads);

    std::vector<SceneQuery::AABBContact> producedContacts;
    std::vector<TerrainQueryPairPayload> producedPairPayloads;
    const bool containerSucceeded = EvaluateTerrainQuerySelectedContactContainerTransaction(
        projectedPosition,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingContacts,
        existingCount != 0u ? existingPairPayloads.data() : nullptr,
        existingCount,
        queryContacts,
        queryCount != 0u ? queryPairPayloads.data() : nullptr,
        queryCount,
        queryDispatchSucceeded,
        producedContacts,
        producedPairPayloads,
        trace.containerTrace);
    if (!containerSucceeded) {
        trace.zeroedOutputsOnContainerFailure = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    CopySelectorPairsFromTerrainQueryPayloads(producedPairPayloads, outSelectedPairs);
    outSelectedContacts = std::move(producedContacts);
    trace.usedProducedSelectedContactContainer = 1u;
    trace.outputSelectedContactCount = static_cast<uint32_t>(outSelectedContacts.size());
    trace.returnedSuccess = 1u;
    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return true;
}

bool WoWCollision::EvaluateSelectorChosenIndexPairPreBridgeTransaction(
    const SelectorCandidateRecord* records,
    uint32_t recordCount,
    const G3D::Vector3& defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3& projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3& testPoint,
    const G3D::Vector3& candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3& cachedBoundsMin,
    const G3D::Vector3& cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const SceneQuery::AABBContact* existingContacts,
    const SelectorPair* existingPairs,
    uint32_t existingCount,
    const SceneQuery::AABBContact* queryContacts,
    const SelectorPair* queryPairs,
    uint32_t queryCount,
    bool queryDispatchSucceeded,
    float inputVerticalOffset,
    float swimVerticalOffsetScale,
    bool selectorBaseMatchesSwimReference,
    float requestedDistanceClamp,
    float requestedDistance,
    float horizontalRadius,
    SelectorSupportPlane* outCandidatePlanes,
    uint32_t maxCandidatePlanes,
    uint32_t& outCandidatePlaneCount,
    int32_t& outSelectedRecordIndex,
    bool& outDirectionRankingAccepted,
    SceneQuery::AABBContact& outChosenContact,
    SelectorPair& outChosenPair,
    std::vector<SceneQuery::AABBContact>& outSelectedContacts,
    std::vector<SelectorPair>& outDirectPairs,
    float& outReportedBestRatio,
    SelectorChosenIndexPairPreBridgeTransactionTrace* outTrace)
{
    SelectorChosenIndexPairPreBridgeTransactionTrace trace{};
    outCandidatePlaneCount = 0u;
    outSelectedRecordIndex = -1;
    outDirectionRankingAccepted = false;
    outChosenContact = SceneQuery::AABBContact{};
    outChosenPair = SelectorPair{};
    outSelectedContacts.clear();
    outDirectPairs.clear();
    outReportedBestRatio = 0.0f;

    const bool variableSucceeded = EvaluateSelectorTriangleSourceVariableTransaction(
        defaultPosition,
        overridePosition,
        projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        testPoint,
        candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        queryDispatchSucceeded,
        true,
        0u,
        -1,
        initialBestRatio,
        trace.variableTrace);
    outReportedBestRatio = trace.variableTrace.outputReportedBestRatio;
    trace.outputReportedBestRatio = outReportedBestRatio;
    if (!variableSucceeded) {
        trace.zeroedOutputsOnVariableFailure = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    const bool directionSetupSucceeded = EvaluateSelectorChosenIndexPairDirectionSetupTransaction(
        records,
        recordCount,
        defaultPosition,
        overridePosition,
        outReportedBestRatio,
        inputVerticalOffset,
        swimVerticalOffsetScale,
        selectorBaseMatchesSwimReference,
        movementFlags,
        requestedDistance,
        requestedDistanceClamp,
        testPoint,
        candidateDirection,
        horizontalRadius,
        outCandidatePlanes,
        maxCandidatePlanes,
        outCandidatePlaneCount,
        outSelectedRecordIndex,
        outReportedBestRatio,
        &trace.directionSetupTrace);
    trace.outputReportedBestRatio = outReportedBestRatio;
    if (!directionSetupSucceeded) {
        trace.zeroedOutputsOnDirectionSetupFailure = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    outDirectionRankingAccepted = trace.directionSetupTrace.rankingAccepted != 0u;
    trace.outputDirectionRankingAccepted = outDirectionRankingAccepted ? 1u : 0u;
    trace.outputCandidatePlaneCount = outCandidatePlaneCount;
    trace.outputSelectedRecordIndex = outSelectedRecordIndex;

    const bool containerSucceeded = EvaluateSelectorChosenIndexPairSelectedContactContainerTransaction(
        overridePosition,
        projectedPosition,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingContacts,
        existingPairs,
        existingCount,
        queryContacts,
        queryPairs,
        queryCount,
        queryDispatchSucceeded,
        outSelectedContacts,
        outDirectPairs,
        &trace.selectedContactContainerTrace);
    trace.outputSelectedContactCount = static_cast<uint32_t>(outSelectedContacts.size());
    if (!containerSucceeded) {
        trace.zeroedOutputsOnContainerFailure = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    SelectorChosenIndexPairSelectedRecordLoadTrace selectedRecordLoadTrace{};
    if (!EvaluateSelectorChosenIndexPairSelectedRecordLoadTransaction(
            outSelectedRecordIndex,
            outSelectedContacts.empty() ? nullptr : outSelectedContacts.data(),
            static_cast<uint32_t>(outSelectedContacts.size()),
            outDirectPairs.empty() ? nullptr : outDirectPairs.data(),
            static_cast<uint32_t>(outDirectPairs.size()),
            outChosenContact,
            outChosenPair,
            &selectedRecordLoadTrace)) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }
    trace.selectedIndexInRange = selectedRecordLoadTrace.selectedIndexInRange;
    trace.loadedChosenContact = selectedRecordLoadTrace.loadedChosenContact;
    trace.loadedChosenPair = selectedRecordLoadTrace.loadedChosenPair;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return true;
}

bool WoWCollision::EvaluateSelectorChosenIndexPairSelectedRecordLoadTransaction(
    int32_t selectedRecordIndex,
    const SceneQuery::AABBContact* selectedContacts,
    uint32_t selectedContactCount,
    const SelectorPair* directPairs,
    uint32_t directPairCount,
    SceneQuery::AABBContact& outChosenContact,
    SelectorPair& outChosenPair,
    SelectorChosenIndexPairSelectedRecordLoadTrace* outTrace)
{
    SelectorChosenIndexPairSelectedRecordLoadTrace trace{};
    trace.inputSelectedRecordIndex = selectedRecordIndex;
    trace.inputSelectedContactCount = selectedContactCount;
    trace.inputDirectPairCount = directPairCount;

    outChosenContact = SceneQuery::AABBContact{};
    outChosenPair = SelectorPair{};

    if ((selectedContactCount != 0u && selectedContacts == nullptr) ||
        (directPairCount != 0u && directPairs == nullptr)) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    if (selectedRecordIndex < 0) {
        trace.selectedIndexUnset = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return true;
    }

    const uint32_t selectedIndex = static_cast<uint32_t>(selectedRecordIndex);
    if (selectedIndex == selectedContactCount) {
        trace.selectedIndexMatchesContactCountSentinel = 1u;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return true;
    }

    if (selectedIndex < selectedContactCount) {
        trace.selectedIndexInRange = 1u;
        outChosenContact = selectedContacts[selectedIndex];
        trace.loadedChosenContact = 1u;
        if (selectedIndex < directPairCount) {
            outChosenPair = directPairs[selectedIndex];
            trace.loadedChosenPair = 1u;
        }

        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return true;
    }

    trace.selectedIndexPastEndMismatch = 1u;
    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return true;
}

bool WoWCollision::EvaluateSelectorChosenIndexPairCallerTransaction(const G3D::Vector3& defaultPosition,
                                                                    const G3D::Vector3* overridePosition,
                                                                    const G3D::Vector3& projectedPosition,
                                                                    float collisionRadius,
                                                                    float boundingHeight,
                                                                    const G3D::Vector3& cachedBoundsMin,
                                                                    const G3D::Vector3& cachedBoundsMax,
                                                                    bool modelPropertyFlagSet,
                                                                    uint32_t movementFlags,
                                                                    float field20Value,
                                                                    bool rootTreeFlagSet,
                                                                    bool childTreeFlagSet,
                                                                    bool queryDispatchSucceeded,
                                                                    bool rankingAccepted,
                                                                    uint32_t rankingCandidateCount,
                                                                    int32_t rankingSelectedRecordIndex,
                                                                    float rankingReportedBestRatio,
                                                                    float& outReportedBestRatio,
                                                                    SelectorChosenIndexPairCallerTransactionTrace* outTrace)
{
    SelectorChosenIndexPairCallerTransactionTrace trace{};
    trace.supportPlaneInitCount = 7u;
    trace.validationPlaneInitCount = 9u;
    trace.scratchPointZeroCount = 9u;
    trace.usedOverridePosition = (overridePosition != nullptr) ? 1u : 0u;
    trace.selectedPosition = (overridePosition != nullptr) ? *overridePosition : defaultPosition;
    trace.variableInvoked = 1u;

    InitializeSelectorTriangleSourceWrapperSeeds(
        trace.testPoint,
        trace.candidateDirection,
        trace.initialBestRatio);

    outReportedBestRatio = 0.0f;
    const bool variableSucceeded = EvaluateSelectorTriangleSourceVariableTransaction(
        defaultPosition,
        overridePosition,
        projectedPosition,
        trace.supportPlaneInitCount,
        trace.validationPlaneInitCount,
        trace.scratchPointZeroCount,
        trace.testPoint,
        trace.candidateDirection,
        trace.initialBestRatio,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        queryDispatchSucceeded,
        rankingAccepted,
        rankingCandidateCount,
        rankingSelectedRecordIndex,
        rankingReportedBestRatio,
        trace.variableTrace);
    outReportedBestRatio = trace.variableTrace.outputReportedBestRatio;
    trace.outputReportedBestRatio = outReportedBestRatio;
    trace.zeroedOutputsOnVariableFailure = variableSucceeded ? 0u : 1u;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return variableSucceeded;
}

bool WoWCollision::EvaluateSelectorChosenIndexPairDirectionSetupTransaction(
    const SelectorCandidateRecord* records,
    uint32_t recordCount,
    const G3D::Vector3& defaultPosition,
    const G3D::Vector3* overridePosition,
    float inputReportedBestRatioSeed,
    float inputVerticalOffset,
    float swimVerticalOffsetScale,
    bool selectorBaseMatchesSwimReference,
    uint32_t movementFlags,
    float requestedDistance,
    float requestedDistanceClamp,
    const G3D::Vector3& testPoint,
    const G3D::Vector3& candidateDirection,
    float horizontalRadius,
    SelectorSupportPlane* outCandidatePlanes,
    uint32_t maxCandidatePlanes,
    uint32_t& outCandidateCount,
    int32_t& outSelectedRecordIndex,
    float& outReportedBestRatio,
    SelectorChosenIndexPairDirectionSetupTrace* outTrace)
{
    SelectorChosenIndexPairDirectionSetupTrace trace{};
    trace.supportPlaneInitCount = 9u;
    trace.scratchPointZeroCount = 9u;
    trace.directionCandidatePlaneInitCount = 5u;
    trace.usedOverridePosition = (overridePosition != nullptr) ? 1u : 0u;
    trace.selectedPosition = (overridePosition != nullptr) ? *overridePosition : defaultPosition;
    trace.inputReportedBestRatioSeed = inputReportedBestRatioSeed;
    trace.inputVerticalOffset = inputVerticalOffset;
    trace.outputVerticalOffset = inputVerticalOffset;
    trace.inputRequestedDistance = requestedDistance;
    trace.requestedDistanceClamp = requestedDistanceClamp;
    trace.clampedRequestedDistance = requestedDistance;

    outCandidateCount = 0u;
    outSelectedRecordIndex = -1;
    outReportedBestRatio = inputReportedBestRatioSeed;

    if ((records == nullptr && recordCount != 0u) ||
        (outCandidatePlanes == nullptr && maxCandidatePlanes != 0u) ||
        maxCandidatePlanes < 5u) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return false;
    }

    for (uint32_t candidateIndex = 0u; candidateIndex < maxCandidatePlanes; ++candidateIndex) {
        InitializeSelectorSupportPlane(outCandidatePlanes[candidateIndex]);
    }

    if (std::fabs(requestedDistance) <= WOW_SELECTOR_DIRECTION_SETUP_EPSILON) {
        trace.zeroDistanceEarlySuccess = 1u;
        trace.outputReportedBestRatio = outReportedBestRatio;

        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return true;
    }

    if ((movementFlags & WOW_MOVEMENTFLAG_SWIMMING) != 0u && selectorBaseMatchesSwimReference) {
        trace.appliedSwimVerticalOffsetScale = 1u;
        trace.outputVerticalOffset *= swimVerticalOffsetScale;
    }

    trace.clampedRequestedDistance = std::min(requestedDistance, requestedDistanceClamp);
    trace.requestedDistanceClamped = (trace.clampedRequestedDistance != requestedDistance) ? 1u : 0u;
    trace.scaledCandidateDirection = candidateDirection * trace.clampedRequestedDistance;

    std::array<SelectorSupportPlane, 9> supportPlanes{};
    for (SelectorSupportPlane& plane : supportPlanes) {
        InitializeSelectorSupportPlane(plane);
    }

    std::array<G3D::Vector3, 9> points{};
    std::array<uint8_t, 32> selectorIndices{};
    BuildSelectorSupportPlanes(
        trace.selectedPosition,
        trace.outputVerticalOffset,
        horizontalRadius,
        supportPlanes);
    BuildSelectorNeighborhood(
        trace.selectedPosition,
        trace.outputVerticalOffset,
        horizontalRadius,
        points,
        selectorIndices);

    std::array<SelectorSupportPlane, 5> bestCandidates{};
    for (SelectorSupportPlane& plane : bestCandidates) {
        InitializeSelectorSupportPlane(plane);
    }

    float bestRatio = inputReportedBestRatioSeed;
    float reportedBestRatio = inputReportedBestRatioSeed;
    uint32_t candidateCount = 0u;
    uint32_t bestRecordIndex = 0xFFFFFFFFu;

    trace.rankingInvoked = 1u;
    const bool rankingAccepted = EvaluateSelectorDirectionRanking(
        records,
        recordCount,
        testPoint,
        trace.scaledCandidateDirection,
        points,
        supportPlanes,
        selectorIndices,
        bestCandidates,
        candidateCount,
        bestRatio,
        reportedBestRatio,
        bestRecordIndex,
        &trace.rankingTrace);
    trace.rankingAccepted = rankingAccepted ? 1u : 0u;

    for (uint32_t candidateIndex = 0u; candidateIndex < candidateCount && candidateIndex < maxCandidatePlanes; ++candidateIndex) {
        outCandidatePlanes[candidateIndex] = bestCandidates[candidateIndex];
    }

    outCandidateCount = candidateCount;
    outSelectedRecordIndex = (bestRecordIndex != 0xFFFFFFFFu) ? static_cast<int32_t>(bestRecordIndex) : -1;
    outReportedBestRatio = reportedBestRatio;

    trace.outputCandidateCount = outCandidateCount;
    trace.outputSelectedRecordIndex = outSelectedRecordIndex;
    trace.outputReportedBestRatio = outReportedBestRatio;

    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return true;
}

WoWCollision::SelectorPairPostForwardingTrace WoWCollision::EvaluateSelectorPairPostForwardingDispatch(
    int32_t pairForwardReturnCode,
    bool directStateBit,
    bool alternateUnitZStateBit,
    float windowSpanScalar,
    float windowStartScalar,
    const G3D::Vector3& moveVector,
    float horizontalReferenceMagnitude,
    uint32_t movementFlags,
    float verticalSpeed,
    float horizontalSpeedScale,
    float referenceZ,
    float positionZ)
{
    SelectorPairPostForwardingTrace trace{};
    trace.pairForwardReturnCode = pairForwardReturnCode;
    trace.directStateBit = directStateBit ? 1u : 0u;
    trace.alternateUnitZStateBit = alternateUnitZStateBit ? 1u : 0u;
    trace.inputWindowSpanScalar = windowSpanScalar;
    trace.outputWindowScalar = windowSpanScalar;
    trace.outputMove = moveVector;

    if (pairForwardReturnCode == 2) {
        trace.dispatchKind = SELECTOR_PAIR_POST_FORWARDING_FAILURE;
        return trace;
    }

    if (pairForwardReturnCode == 1) {
        float outputMoveMagnitude = std::numeric_limits<float>::max();
        trace.usedWindowAdjustment = 1u;
        trace.outputWindowScalar = EvaluateSelectorPairWindowAdjustment(
            windowSpanScalar,
            windowStartScalar,
            trace.outputMove,
            &outputMoveMagnitude,
            alternateUnitZStateBit,
            horizontalReferenceMagnitude,
            movementFlags,
            verticalSpeed,
            horizontalSpeedScale,
            referenceZ,
            positionZ);

        if (outputMoveMagnitude != std::numeric_limits<float>::max()) {
            trace.outputMagnitudeWritten = 1u;
            trace.outputMoveMagnitude = outputMoveMagnitude;
        }
    }

    if (alternateUnitZStateBit) {
        trace.dispatchKind = SELECTOR_PAIR_POST_FORWARDING_ALTERNATE_UNIT_Z;
    }
    else if (directStateBit) {
        trace.dispatchKind = SELECTOR_PAIR_POST_FORWARDING_DIRECT;
    }
    else {
        trace.dispatchKind = SELECTOR_PAIR_POST_FORWARDING_NON_STATEFUL;
    }

    return trace;
}

WoWCollision::SelectorAlternateUnitZStateTrace WoWCollision::EvaluateSelectorAlternateUnitZStateHandler(
    uint32_t movementFlags,
    float positionZ)
{
    SelectorAlternateUnitZStateTrace trace{};
    trace.inputMovementFlags = movementFlags;
    trace.outputMovementFlags = movementFlags | MOVEFLAG_FALLINGFAR;
    trace.setFallingFarFlag = 1u;
    trace.clearedFallTime = 1u;
    trace.zeroedVerticalSpeed = 1u;
    trace.copiedPositionZToFallStartZ = 1u;
    trace.inputPositionZ = positionZ;
    trace.outputFallTime = 0u;
    trace.outputFallStartZ = positionZ;
    trace.outputVerticalSpeed = 0.0f;
    return trace;
}

WoWCollision::SelectorDirectStateTrace WoWCollision::EvaluateSelectorDirectStateHandler(
    uint32_t movementFlags,
    const G3D::Vector3& startPosition,
    float facing,
    float pitch,
    const G3D::Vector3& cachedPosition,
    float cachedFacing,
    float cachedPitch,
    uint32_t cachedMoveTimestamp,
    float cachedScalar84,
    float recomputedScalar84)
{
    SelectorDirectStateTrace trace{};
    trace.inputMovementFlags = movementFlags;
    trace.outputMovementFlags = movementFlags;
    trace.inputStartPosition = startPosition;
    trace.inputCachedPosition = cachedPosition;
    trace.outputCachedPosition = cachedPosition;
    trace.inputFacing = facing;
    trace.inputCachedFacing = cachedFacing;
    trace.outputCachedFacing = cachedFacing;
    trace.inputPitch = pitch;
    trace.inputCachedPitch = cachedPitch;
    trace.outputCachedPitch = cachedPitch;
    trace.inputMoveTimestamp = cachedMoveTimestamp;
    trace.outputMoveTimestamp = cachedMoveTimestamp;
    trace.inputScalar84 = cachedScalar84;
    trace.recomputedScalar84 = recomputedScalar84;
    trace.outputScalar84 = cachedScalar84;

    if ((movementFlags & MOVEFLAG_JUMPING) == 0u) {
        return trace;
    }

    trace.jumpingBitWasSet = 1u;
    trace.clearedJumpingBit = 1u;
    trace.copiedPosition = 1u;
    trace.copiedFacing = 1u;
    trace.copiedPitch = 1u;
    trace.zeroedMoveTimestamp = 1u;
    trace.wroteScalar84 = 1u;
    trace.outputMovementFlags = movementFlags & ~MOVEFLAG_JUMPING;
    trace.outputCachedPosition = startPosition;
    trace.outputCachedFacing = facing;
    trace.outputCachedPitch = pitch;
    trace.outputMoveTimestamp = 0u;
    trace.outputScalar84 = recomputedScalar84;
    return trace;
}
