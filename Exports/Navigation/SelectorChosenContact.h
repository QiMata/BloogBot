#pragma once

#include "PhysicsEngine.h"

namespace WoWCollision
{
    enum SelectorPairSource : uint32_t
    {
        SELECTOR_PAIR_SOURCE_NONE = 0,
        SELECTOR_PAIR_SOURCE_PRESERVED_INPUT = 1,
        SELECTOR_PAIR_SOURCE_RANKING_FAILURE = 2,
        SELECTOR_PAIR_SOURCE_DIRECT = 3,
        SELECTOR_PAIR_SOURCE_DIRECT_ZERO = 4,
        SELECTOR_PAIR_SOURCE_ALTERNATE_UNIT_Z_ZERO = 5,
        SELECTOR_PAIR_SOURCE_ALTERNATE = 6,
    };

    struct SelectorPairForwardingTrace
    {
        uint32_t directGateAccepted = 0;
        uint32_t alternateUnitZFallbackGateAccepted = 0;
        uint32_t currentPositionInsidePrism = 0;
        uint32_t projectedPositionInsidePrism = 0;
        uint32_t thresholdSensitive = 0;
        float normalZ = 0.0f;
        uint32_t pairSource = SELECTOR_PAIR_SOURCE_NONE;
        SelectorPairConsumerTrace consumerTrace{};
    };

    enum SelectorPairPostForwardingDispatchKind : uint32_t
    {
        SELECTOR_PAIR_POST_FORWARDING_FAILURE = 0,
        SELECTOR_PAIR_POST_FORWARDING_ALTERNATE_UNIT_Z = 1,
        SELECTOR_PAIR_POST_FORWARDING_DIRECT = 2,
        SELECTOR_PAIR_POST_FORWARDING_NON_STATEFUL = 3,
    };

    struct SelectorPairPostForwardingTrace
    {
        int32_t pairForwardReturnCode = 0;
        uint32_t directStateBit = 0;
        uint32_t alternateUnitZStateBit = 0;
        uint32_t usedWindowAdjustment = 0;
        uint32_t outputMagnitudeWritten = 0;
        uint32_t dispatchKind = SELECTOR_PAIR_POST_FORWARDING_FAILURE;
        float inputWindowSpanScalar = 0.0f;
        float outputWindowScalar = 0.0f;
        float outputMoveMagnitude = 0.0f;
        G3D::Vector3 outputMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
    };

    struct SelectorAlternateUnitZStateTrace
    {
        uint32_t inputMovementFlags = 0;
        uint32_t outputMovementFlags = 0;
        uint32_t setFallingFarFlag = 0;
        uint32_t clearedFallTime = 0;
        uint32_t zeroedVerticalSpeed = 0;
        uint32_t copiedPositionZToFallStartZ = 0;
        float inputPositionZ = 0.0f;
        uint32_t outputFallTime = 0;
        float outputFallStartZ = 0.0f;
        float outputVerticalSpeed = 0.0f;
    };

    struct SelectorDirectStateTrace
    {
        uint32_t inputMovementFlags = 0;
        uint32_t outputMovementFlags = 0;
        uint32_t jumpingBitWasSet = 0;
        uint32_t clearedJumpingBit = 0;
        uint32_t copiedPosition = 0;
        uint32_t copiedFacing = 0;
        uint32_t copiedPitch = 0;
        uint32_t zeroedMoveTimestamp = 0;
        uint32_t wroteScalar84 = 0;
        G3D::Vector3 inputStartPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 inputCachedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 outputCachedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float inputFacing = 0.0f;
        float inputCachedFacing = 0.0f;
        float outputCachedFacing = 0.0f;
        float inputPitch = 0.0f;
        float inputCachedPitch = 0.0f;
        float outputCachedPitch = 0.0f;
        uint32_t inputMoveTimestamp = 0;
        uint32_t outputMoveTimestamp = 0;
        float inputScalar84 = 0.0f;
        float recomputedScalar84 = 0.0f;
        float outputScalar84 = 0.0f;
    };

    struct SelectorChosenIndexPairBridgeTrace
    {
        uint32_t selectedIndexInRange = 0;
        uint32_t loadedSelectedContact = 0;
        uint32_t loadedDirectPair = 0;
        uint32_t negativeDiagonalCandidateFound = 0;
        uint32_t unitZCandidateFound = 0;
        uint32_t alternateUnitZFallbackGateAccepted = 0;
        uint32_t currentPositionInsidePrism = 0;
        uint32_t projectedPositionInsidePrism = 0;
        uint32_t thresholdSensitive = 0;
        float selectedNormalZ = 0.0f;
        uint32_t pairSource = SELECTOR_PAIR_SOURCE_NONE;
        SelectorPairConsumerTrace consumerTrace{};
    };

    struct SelectorChosenIndexPairProducerTransactionTrace
    {
        SelectorTriangleSourceVariableTransactionTrace variableTrace{};
        TerrainQuerySelectedContactContainerTrace containerTrace{};
        SelectorChosenIndexPairBridgeTrace bridgeTrace{};
        uint32_t usedAmbientCachedContainerWithoutQuery = 0;
        uint32_t usedProducedSelectedContactContainer = 0;
        uint32_t containerInvoked = 0;
        uint32_t bridgeInvoked = 0;
        uint32_t zeroedOutputsOnVariableFailure = 0;
        uint32_t zeroedOutputsOnContainerFailure = 0;
        uint32_t bridgeSelectedContactCount = 0;
        int32_t returnCode = 0;
        float outputReportedBestRatio = 0.0f;
    };

    struct SelectorChosenIndexPairSelectedContactContainerTransactionTrace
    {
        TerrainQuerySelectedContactContainerTrace containerTrace{};
        uint32_t usedAmbientCachedContainerWithoutQuery = 0;
        uint32_t usedProducedSelectedContactContainer = 0;
        uint32_t containerInvoked = 0;
        uint32_t zeroedOutputsOnContainerFailure = 0;
        uint32_t outputSelectedContactCount = 0;
        uint32_t returnedSuccess = 0;
    };

    struct SelectorChosenIndexPairVariableContainerTransactionTrace
    {
        SelectorTriangleSourceVariableTransactionTrace variableTrace{};
        SelectorChosenIndexPairSelectedContactContainerTransactionTrace selectedContactContainerTrace{};
        uint32_t zeroedOutputsOnVariableFailure = 0;
        uint32_t zeroedOutputsOnContainerFailure = 0;
        uint32_t outputSelectedContactCount = 0;
        uint32_t returnedSuccess = 0;
        float outputReportedBestRatio = 0.0f;
    };

    struct SelectorChosenIndexPairCallerTransactionTrace
    {
        SelectorTriangleSourceVariableTransactionTrace variableTrace{};
        uint32_t supportPlaneInitCount = 0;
        uint32_t validationPlaneInitCount = 0;
        uint32_t scratchPointZeroCount = 0;
        uint32_t usedOverridePosition = 0;
        uint32_t variableInvoked = 0;
        uint32_t zeroedOutputsOnVariableFailure = 0;
        G3D::Vector3 selectedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 testPoint = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 candidateDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float initialBestRatio = 0.0f;
        float outputReportedBestRatio = 0.0f;
    };

    struct SelectorChosenIndexPairDirectionSetupTrace
    {
        SelectorDirectionRankingTrace rankingTrace{};
        uint32_t supportPlaneInitCount = 0;
        uint32_t scratchPointZeroCount = 0;
        uint32_t directionCandidatePlaneInitCount = 0;
        uint32_t usedOverridePosition = 0;
        uint32_t appliedSwimVerticalOffsetScale = 0;
        uint32_t requestedDistanceClamped = 0;
        uint32_t zeroDistanceEarlySuccess = 0;
        uint32_t rankingInvoked = 0;
        uint32_t rankingAccepted = 0;
        G3D::Vector3 selectedPosition = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 scaledCandidateDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float inputReportedBestRatioSeed = 0.0f;
        float inputVerticalOffset = 0.0f;
        float outputVerticalOffset = 0.0f;
        float inputRequestedDistance = 0.0f;
        float requestedDistanceClamp = 0.0f;
        float clampedRequestedDistance = 0.0f;
        uint32_t outputCandidateCount = 0;
        int32_t outputSelectedRecordIndex = -1;
        float outputReportedBestRatio = 0.0f;
    };

    struct SelectorChosenIndexPairDirectionSetupProducerTransactionTrace
    {
        SelectorTriangleSourceVariableTransactionTrace variableTrace{};
        SelectorChosenIndexPairDirectionSetupTrace directionSetupTrace{};
        TerrainQuerySelectedContactContainerTrace containerTrace{};
        SelectorChosenIndexPairBridgeTrace bridgeTrace{};
        uint32_t usedAmbientCachedContainerWithoutQuery = 0;
        uint32_t usedProducedSelectedContactContainer = 0;
        uint32_t containerInvoked = 0;
        uint32_t bridgeInvoked = 0;
        uint32_t zeroedOutputsOnVariableFailure = 0;
        uint32_t zeroedOutputsOnDirectionSetupFailure = 0;
        uint32_t zeroedOutputsOnContainerFailure = 0;
        uint32_t bridgeSelectedContactCount = 0;
        int32_t returnCode = 0;
        float outputReportedBestRatio = 0.0f;
    };

    struct SelectorChosenIndexPairPreBridgeTransactionTrace
    {
        SelectorTriangleSourceVariableTransactionTrace variableTrace{};
        SelectorChosenIndexPairDirectionSetupTrace directionSetupTrace{};
        SelectorChosenIndexPairSelectedContactContainerTransactionTrace selectedContactContainerTrace{};
        uint32_t zeroedOutputsOnVariableFailure = 0;
        uint32_t zeroedOutputsOnDirectionSetupFailure = 0;
        uint32_t zeroedOutputsOnContainerFailure = 0;
        uint32_t outputDirectionRankingAccepted = 0;
        uint32_t outputCandidatePlaneCount = 0;
        int32_t outputSelectedRecordIndex = -1;
        uint32_t outputSelectedContactCount = 0;
        uint32_t selectedIndexInRange = 0;
        uint32_t loadedChosenContact = 0;
        uint32_t loadedChosenPair = 0;
        float outputReportedBestRatio = 0.0f;
    };

    struct SelectorChosenIndexPairSelectedRecordLoadTrace
    {
        int32_t inputSelectedRecordIndex = -1;
        uint32_t inputSelectedContactCount = 0;
        uint32_t inputDirectPairCount = 0;
        uint32_t selectedIndexUnset = 0;
        uint32_t selectedIndexMatchesContactCountSentinel = 0;
        uint32_t selectedIndexInRange = 0;
        uint32_t selectedIndexPastEndMismatch = 0;
        uint32_t loadedChosenContact = 0;
        uint32_t loadedChosenPair = 0;
    };

    bool EvaluateSelectorChosenPairForwarding(const SceneQuery::AABBContact& selectedContact,
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
                                              SelectorPairForwardingTrace* outTrace = nullptr);

    int32_t EvaluateSelectorChosenIndexPairBridge(const SceneQuery::AABBContact* selectedContacts,
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
                                                  SelectorChosenIndexPairBridgeTrace* outTrace = nullptr);

    int32_t EvaluateSelectorChosenIndexPairProducerTransaction(const G3D::Vector3& defaultPosition,
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
                                                               SelectorChosenIndexPairProducerTransactionTrace* outTrace = nullptr);

    bool EvaluateSelectorChosenIndexPairSelectedContactContainerTransaction(
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
        SelectorChosenIndexPairSelectedContactContainerTransactionTrace* outTrace = nullptr);

    bool EvaluateSelectorChosenIndexPairVariableContainerTransaction(
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
        SelectorChosenIndexPairVariableContainerTransactionTrace* outTrace = nullptr);

    int32_t EvaluateSelectorChosenIndexPairDirectionSetupProducerTransaction(
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
        SelectorChosenIndexPairDirectionSetupProducerTransactionTrace* outTrace = nullptr);

    bool EvaluateSelectorChosenIndexPairPreBridgeTransaction(
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
        SelectorChosenIndexPairPreBridgeTransactionTrace* outTrace = nullptr);

    bool EvaluateSelectorChosenIndexPairSelectedRecordLoadTransaction(
        int32_t selectedRecordIndex,
        const SceneQuery::AABBContact* selectedContacts,
        uint32_t selectedContactCount,
        const SelectorPair* directPairs,
        uint32_t directPairCount,
        SceneQuery::AABBContact& outChosenContact,
        SelectorPair& outChosenPair,
        SelectorChosenIndexPairSelectedRecordLoadTrace* outTrace = nullptr);

    bool EvaluateSelectorChosenIndexPairCallerTransaction(const G3D::Vector3& defaultPosition,
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
                                                          SelectorChosenIndexPairCallerTransactionTrace* outTrace = nullptr);

    bool EvaluateSelectorChosenIndexPairDirectionSetupTransaction(const SelectorCandidateRecord* records,
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
                                                                  SelectorChosenIndexPairDirectionSetupTrace* outTrace = nullptr);

    SelectorPairPostForwardingTrace EvaluateSelectorPairPostForwardingDispatch(int32_t pairForwardReturnCode,
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
                                                                               float positionZ);

    SelectorAlternateUnitZStateTrace EvaluateSelectorAlternateUnitZStateHandler(uint32_t movementFlags,
                                                                                float positionZ);

    SelectorDirectStateTrace EvaluateSelectorDirectStateHandler(uint32_t movementFlags,
                                                                const G3D::Vector3& startPosition,
                                                                float facing,
                                                                float pitch,
                                                                const G3D::Vector3& cachedPosition,
                                                                float cachedFacing,
                                                                float cachedPitch,
                                                                uint32_t cachedMoveTimestamp,
                                                                float cachedScalar84,
                                                                float recomputedScalar84);
}
