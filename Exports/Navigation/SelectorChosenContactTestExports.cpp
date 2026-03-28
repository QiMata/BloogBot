#include "SelectorChosenContact.h"

#include <vector>

namespace
{
    struct ExportTriangle
    {
        G3D::Vector3 a;
        G3D::Vector3 b;
        G3D::Vector3 c;
    };

    struct ExportAABBContact
    {
        G3D::Vector3 point;
        G3D::Vector3 normal;
        G3D::Vector3 rawNormal;
        G3D::Vector3 triangleA;
        G3D::Vector3 triangleB;
        G3D::Vector3 triangleC;
        float planeDistance;
        float distance;
        uint32_t instanceId;
        uint32_t sourceType;
        uint32_t walkable;
    };

    struct ExportSelectorSupportPlane
    {
        G3D::Vector3 normal;
        float planeDistance;
    };

    struct ExportSelectorCandidateRecord
    {
        ExportSelectorSupportPlane filterPlane;
        G3D::Vector3 point0;
        G3D::Vector3 point1;
        G3D::Vector3 point2;
    };
}

extern "C" {

__declspec(dllexport) bool EvaluateWoWSelectorChosenPairForwarding(
    const ExportTriangle* triangle,
    const G3D::Vector3* contactNormal,
    const G3D::Vector3* currentPosition,
    float requestedDistance,
    const G3D::Vector3* inputMove,
    bool useStandardWalkableThreshold,
    bool directionRankingAccepted,
    int selectedIndex,
    int selectedCount,
    bool hasNegativeDiagonalCandidate,
    float airborneTimeScalar,
    float elapsedTimeScalar,
    float horizontalSpeedScale,
    bool hasUnitZCandidate,
    const WoWCollision::SelectorPair* directPair,
    const WoWCollision::SelectorPair* alternatePair,
    WoWCollision::SelectorPairForwardingTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorPairForwardingTrace{};
    }

    if (!triangle || !contactNormal || !currentPosition || !inputMove || !directPair || !alternatePair || selectedCount < 0) {
        return false;
    }

    SceneQuery::AABBContact contact{};
    contact.normal = contactNormal->directionOrZero();
    contact.rawNormal = *contactNormal;
    contact.triangleA = triangle->a;
    contact.triangleB = triangle->b;
    contact.triangleC = triangle->c;
    contact.planeDistance = contact.normal.magnitude() > PhysicsConstants::VECTOR_EPSILON
        ? -contact.normal.dot(contact.triangleA)
        : 0.0f;

    return WoWCollision::EvaluateSelectorChosenPairForwarding(
        contact,
        *currentPosition,
        requestedDistance,
        *inputMove,
        useStandardWalkableThreshold,
        directionRankingAccepted,
        selectedIndex,
        static_cast<uint32_t>(selectedCount),
        hasNegativeDiagonalCandidate,
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        hasUnitZCandidate,
        *directPair,
        *alternatePair,
        outTrace);
}

__declspec(dllexport) int32_t EvaluateWoWSelectorChosenIndexPairBridge(
    const ExportTriangle* selectedTriangles,
    const G3D::Vector3* contactNormals,
    int selectedContactCount,
    const WoWCollision::SelectorPair* directPairs,
    int directPairCount,
    const WoWCollision::SelectorSupportPlane* candidatePlanes,
    int candidatePlaneCount,
    const G3D::Vector3* currentPosition,
    float requestedDistance,
    const G3D::Vector3* inputMove,
    bool useStandardWalkableThreshold,
    bool directionRankingAccepted,
    int selectedIndex,
    float airborneTimeScalar,
    float elapsedTimeScalar,
    float horizontalSpeedScale,
    const WoWCollision::SelectorPair* alternatePair,
    WoWCollision::SelectorPair* outPair,
    uint32_t* outDirectStateDword,
    uint32_t* outAlternateUnitZStateDword,
    WoWCollision::SelectorChosenIndexPairBridgeTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairBridgeTrace{};
    }

    if (outPair) {
        *outPair = WoWCollision::SelectorPair{};
    }

    if (outDirectStateDword) {
        *outDirectStateDword = 0u;
    }

    if (outAlternateUnitZStateDword) {
        *outAlternateUnitZStateDword = 0u;
    }

    if (!currentPosition || !inputMove || !alternatePair || !outPair ||
        !outDirectStateDword || !outAlternateUnitZStateDword ||
        selectedContactCount < 0 || directPairCount < 0 || candidatePlaneCount < 0) {
        return 0;
    }

    std::vector<SceneQuery::AABBContact> contacts;
    if (selectedTriangles != nullptr && contactNormals != nullptr && selectedContactCount > 0) {
        contacts.resize(static_cast<size_t>(selectedContactCount));
        for (int contactIndex = 0; contactIndex < selectedContactCount; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.normal = contactNormals[contactIndex].directionOrZero();
            contact.rawNormal = contactNormals[contactIndex];
            contact.triangleA = selectedTriangles[contactIndex].a;
            contact.triangleB = selectedTriangles[contactIndex].b;
            contact.triangleC = selectedTriangles[contactIndex].c;
            contact.planeDistance = contact.normal.magnitude() > PhysicsConstants::VECTOR_EPSILON
                ? -contact.normal.dot(contact.triangleA)
                : 0.0f;
        }
    }

    return WoWCollision::EvaluateSelectorChosenIndexPairBridge(
        contacts.empty() ? nullptr : contacts.data(),
        static_cast<uint32_t>(selectedContactCount),
        directPairs,
        static_cast<uint32_t>(directPairCount),
        candidatePlanes,
        static_cast<uint32_t>(candidatePlaneCount),
        *currentPosition,
        requestedDistance,
        *inputMove,
        useStandardWalkableThreshold,
        directionRankingAccepted,
        selectedIndex,
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        *alternatePair,
        *outPair,
        *outDirectStateDword,
        *outAlternateUnitZStateDword,
        outTrace);
}

__declspec(dllexport) int32_t EvaluateWoWSelectorChosenIndexPairSelectedContactContainerTransaction(
    const G3D::Vector3* overridePosition,
    const G3D::Vector3* projectedPosition,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3* cachedBoundsMin,
    const G3D::Vector3* cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const ExportAABBContact* existingContacts,
    const WoWCollision::SelectorPair* existingPairs,
    int existingCount,
    const ExportAABBContact* queryContacts,
    const WoWCollision::SelectorPair* queryPairs,
    int queryCount,
    bool queryDispatchSucceeded,
    ExportAABBContact* outContacts,
    WoWCollision::SelectorPair* outPairs,
    int maxOutputCount,
    WoWCollision::SelectorChosenIndexPairSelectedContactContainerTransactionTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairSelectedContactContainerTransactionTrace{};
    }

    if (!projectedPosition || !cachedBoundsMin || !cachedBoundsMax || !outContacts || !outPairs || !outTrace ||
        existingCount < 0 || queryCount < 0 || maxOutputCount < 0 ||
        ((existingCount != 0) && (!existingContacts || !existingPairs)) ||
        ((queryCount != 0) && (!queryContacts || !queryPairs))) {
        return 0;
    }

    auto buildContacts = [](const ExportAABBContact* exportContacts, int count) {
        std::vector<SceneQuery::AABBContact> contacts(static_cast<size_t>(count));
        for (int contactIndex = 0; contactIndex < count; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.point = exportContacts[contactIndex].point;
            contact.normal = exportContacts[contactIndex].normal;
            contact.rawNormal = exportContacts[contactIndex].rawNormal;
            contact.triangleA = exportContacts[contactIndex].triangleA;
            contact.triangleB = exportContacts[contactIndex].triangleB;
            contact.triangleC = exportContacts[contactIndex].triangleC;
            contact.planeDistance = exportContacts[contactIndex].planeDistance;
            contact.distance = exportContacts[contactIndex].distance;
            contact.instanceId = exportContacts[contactIndex].instanceId;
            contact.sourceType = exportContacts[contactIndex].sourceType;
            contact.walkable = exportContacts[contactIndex].walkable != 0u;
        }

        return contacts;
    };

    std::vector<SceneQuery::AABBContact> existingContactBuffer =
        existingCount > 0 ? buildContacts(existingContacts, existingCount) : std::vector<SceneQuery::AABBContact>{};
    std::vector<SceneQuery::AABBContact> queryContactBuffer =
        queryCount > 0 ? buildContacts(queryContacts, queryCount) : std::vector<SceneQuery::AABBContact>{};

    std::vector<SceneQuery::AABBContact> outputContactBuffer;
    std::vector<WoWCollision::SelectorPair> outputPairBuffer;
    const bool result = WoWCollision::EvaluateSelectorChosenIndexPairSelectedContactContainerTransaction(
        overridePosition,
        *projectedPosition,
        collisionRadius,
        boundingHeight,
        *cachedBoundsMin,
        *cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingCount > 0 ? existingContactBuffer.data() : nullptr,
        existingPairs,
        static_cast<uint32_t>(existingCount),
        queryCount > 0 ? queryContactBuffer.data() : nullptr,
        queryPairs,
        static_cast<uint32_t>(queryCount),
        queryDispatchSucceeded,
        outputContactBuffer,
        outputPairBuffer,
        outTrace);
    if (!result) {
        return 0;
    }

    const int outputCount = std::min<int>(static_cast<int>(outputContactBuffer.size()), maxOutputCount);
    for (int contactIndex = 0; contactIndex < outputCount; ++contactIndex) {
        const SceneQuery::AABBContact& contact = outputContactBuffer[static_cast<size_t>(contactIndex)];
        outContacts[contactIndex].point = contact.point;
        outContacts[contactIndex].normal = contact.normal;
        outContacts[contactIndex].rawNormal = contact.rawNormal;
        outContacts[contactIndex].triangleA = contact.triangleA;
        outContacts[contactIndex].triangleB = contact.triangleB;
        outContacts[contactIndex].triangleC = contact.triangleC;
        outContacts[contactIndex].planeDistance = contact.planeDistance;
        outContacts[contactIndex].distance = contact.distance;
        outContacts[contactIndex].instanceId = contact.instanceId;
        outContacts[contactIndex].sourceType = contact.sourceType;
        outContacts[contactIndex].walkable = contact.walkable ? 1u : 0u;
        outPairs[contactIndex] = outputPairBuffer[static_cast<size_t>(contactIndex)];
    }

    return outputCount;
}

__declspec(dllexport) int32_t EvaluateWoWSelectorChosenIndexPairVariableContainerTransaction(
    const G3D::Vector3* defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3* projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3* testPoint,
    const G3D::Vector3* candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3* cachedBoundsMin,
    const G3D::Vector3* cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const ExportAABBContact* existingContacts,
    const WoWCollision::SelectorPair* existingPairs,
    int existingCount,
    const ExportAABBContact* queryContacts,
    const WoWCollision::SelectorPair* queryPairs,
    int queryCount,
    bool queryDispatchSucceeded,
    bool rankingAccepted,
    uint32_t rankingCandidateCount,
    int32_t rankingSelectedRecordIndex,
    float rankingReportedBestRatio,
    ExportAABBContact* outContacts,
    WoWCollision::SelectorPair* outPairs,
    int maxOutputCount,
    float* outReportedBestRatio,
    WoWCollision::SelectorChosenIndexPairVariableContainerTransactionTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairVariableContainerTransactionTrace{};
    }

    if (outReportedBestRatio) {
        *outReportedBestRatio = 0.0f;
    }

    if (!defaultPosition || !projectedPosition || !testPoint || !candidateDirection ||
        !cachedBoundsMin || !cachedBoundsMax || !outContacts || !outPairs || !outReportedBestRatio || !outTrace ||
        existingCount < 0 || queryCount < 0 || maxOutputCount < 0 ||
        ((existingCount != 0) && (!existingContacts || !existingPairs)) ||
        ((queryCount != 0) && (!queryContacts || !queryPairs))) {
        return 0;
    }

    auto buildContacts = [](const ExportAABBContact* exportContacts, int count) {
        std::vector<SceneQuery::AABBContact> contacts(static_cast<size_t>(count));
        for (int contactIndex = 0; contactIndex < count; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.point = exportContacts[contactIndex].point;
            contact.normal = exportContacts[contactIndex].normal;
            contact.rawNormal = exportContacts[contactIndex].rawNormal;
            contact.triangleA = exportContacts[contactIndex].triangleA;
            contact.triangleB = exportContacts[contactIndex].triangleB;
            contact.triangleC = exportContacts[contactIndex].triangleC;
            contact.planeDistance = exportContacts[contactIndex].planeDistance;
            contact.distance = exportContacts[contactIndex].distance;
            contact.instanceId = exportContacts[contactIndex].instanceId;
            contact.sourceType = exportContacts[contactIndex].sourceType;
            contact.walkable = exportContacts[contactIndex].walkable != 0u;
        }

        return contacts;
    };

    std::vector<SceneQuery::AABBContact> existingContactBuffer =
        existingCount > 0 ? buildContacts(existingContacts, existingCount) : std::vector<SceneQuery::AABBContact>{};
    std::vector<SceneQuery::AABBContact> queryContactBuffer =
        queryCount > 0 ? buildContacts(queryContacts, queryCount) : std::vector<SceneQuery::AABBContact>{};

    std::vector<SceneQuery::AABBContact> outputContactBuffer;
    std::vector<WoWCollision::SelectorPair> outputPairBuffer;
    const bool result = WoWCollision::EvaluateSelectorChosenIndexPairVariableContainerTransaction(
        *defaultPosition,
        overridePosition,
        *projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        *testPoint,
        *candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        *cachedBoundsMin,
        *cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingCount > 0 ? existingContactBuffer.data() : nullptr,
        existingPairs,
        static_cast<uint32_t>(existingCount),
        queryCount > 0 ? queryContactBuffer.data() : nullptr,
        queryPairs,
        static_cast<uint32_t>(queryCount),
        queryDispatchSucceeded,
        rankingAccepted,
        rankingCandidateCount,
        rankingSelectedRecordIndex,
        rankingReportedBestRatio,
        outputContactBuffer,
        outputPairBuffer,
        *outReportedBestRatio,
        outTrace);
    if (!result) {
        return 0;
    }

    const int outputCount = std::min<int>(static_cast<int>(outputContactBuffer.size()), maxOutputCount);
    for (int contactIndex = 0; contactIndex < outputCount; ++contactIndex) {
        const SceneQuery::AABBContact& contact = outputContactBuffer[static_cast<size_t>(contactIndex)];
        outContacts[contactIndex].point = contact.point;
        outContacts[contactIndex].normal = contact.normal;
        outContacts[contactIndex].rawNormal = contact.rawNormal;
        outContacts[contactIndex].triangleA = contact.triangleA;
        outContacts[contactIndex].triangleB = contact.triangleB;
        outContacts[contactIndex].triangleC = contact.triangleC;
        outContacts[contactIndex].planeDistance = contact.planeDistance;
        outContacts[contactIndex].distance = contact.distance;
        outContacts[contactIndex].instanceId = contact.instanceId;
        outContacts[contactIndex].sourceType = contact.sourceType;
        outContacts[contactIndex].walkable = contact.walkable ? 1u : 0u;
        outPairs[contactIndex] = outputPairBuffer[static_cast<size_t>(contactIndex)];
    }

    return outputCount;
}

__declspec(dllexport) bool EvaluateWoWSelectorChosenIndexPairSelectedRecordLoadTransaction(
    int32_t selectedRecordIndex,
    const ExportAABBContact* selectedContacts,
    int selectedContactCount,
    const WoWCollision::SelectorPair* directPairs,
    int directPairCount,
    ExportAABBContact* outChosenContact,
    WoWCollision::SelectorPair* outChosenPair,
    WoWCollision::SelectorChosenIndexPairSelectedRecordLoadTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairSelectedRecordLoadTrace{};
    }

    if (outChosenContact) {
        *outChosenContact = ExportAABBContact{};
    }

    if (outChosenPair) {
        *outChosenPair = WoWCollision::SelectorPair{};
    }

    if (!outChosenContact || !outChosenPair || !outTrace ||
        selectedContactCount < 0 || directPairCount < 0 ||
        ((selectedContactCount != 0) && !selectedContacts) ||
        ((directPairCount != 0) && !directPairs)) {
        return false;
    }

    auto buildContacts = [](const ExportAABBContact* exportContacts, int count) {
        std::vector<SceneQuery::AABBContact> contacts(static_cast<size_t>(count));
        for (int contactIndex = 0; contactIndex < count; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.point = exportContacts[contactIndex].point;
            contact.normal = exportContacts[contactIndex].normal;
            contact.rawNormal = exportContacts[contactIndex].rawNormal;
            contact.triangleA = exportContacts[contactIndex].triangleA;
            contact.triangleB = exportContacts[contactIndex].triangleB;
            contact.triangleC = exportContacts[contactIndex].triangleC;
            contact.planeDistance = exportContacts[contactIndex].planeDistance;
            contact.distance = exportContacts[contactIndex].distance;
            contact.instanceId = exportContacts[contactIndex].instanceId;
            contact.sourceType = exportContacts[contactIndex].sourceType;
            contact.walkable = exportContacts[contactIndex].walkable != 0u;
        }

        return contacts;
    };

    std::vector<SceneQuery::AABBContact> selectedContactBuffer =
        selectedContactCount > 0 ? buildContacts(selectedContacts, selectedContactCount) : std::vector<SceneQuery::AABBContact>{};

    SceneQuery::AABBContact chosenContact{};
    WoWCollision::SelectorPair chosenPair{};
    const bool result = WoWCollision::EvaluateSelectorChosenIndexPairSelectedRecordLoadTransaction(
        selectedRecordIndex,
        selectedContactBuffer.empty() ? nullptr : selectedContactBuffer.data(),
        static_cast<uint32_t>(selectedContactCount),
        directPairs,
        static_cast<uint32_t>(directPairCount),
        chosenContact,
        chosenPair,
        outTrace);
    if (!result) {
        return false;
    }

    outChosenContact->point = chosenContact.point;
    outChosenContact->normal = chosenContact.normal;
    outChosenContact->rawNormal = chosenContact.rawNormal;
    outChosenContact->triangleA = chosenContact.triangleA;
    outChosenContact->triangleB = chosenContact.triangleB;
    outChosenContact->triangleC = chosenContact.triangleC;
    outChosenContact->planeDistance = chosenContact.planeDistance;
    outChosenContact->distance = chosenContact.distance;
    outChosenContact->instanceId = chosenContact.instanceId;
    outChosenContact->sourceType = chosenContact.sourceType;
    outChosenContact->walkable = chosenContact.walkable ? 1u : 0u;
    *outChosenPair = chosenPair;
    return true;
}

__declspec(dllexport) bool EvaluateWoWSelectorChosenIndexPairPreBridgeTransaction(
    const ExportSelectorCandidateRecord* records,
    int recordCount,
    const G3D::Vector3* defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3* projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3* testPoint,
    const G3D::Vector3* candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3* cachedBoundsMin,
    const G3D::Vector3* cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const ExportAABBContact* existingContacts,
    const WoWCollision::SelectorPair* existingPairs,
    int existingCount,
    const ExportAABBContact* queryContacts,
    const WoWCollision::SelectorPair* queryPairs,
    int queryCount,
    bool queryDispatchSucceeded,
    float inputVerticalOffset,
    float swimVerticalOffsetScale,
    bool selectorBaseMatchesSwimReference,
    float requestedDistanceClamp,
    float requestedDistance,
    float horizontalRadius,
    WoWCollision::SelectorSupportPlane* outCandidatePlanes,
    int maxCandidatePlanes,
    uint32_t* outCandidatePlaneCount,
    int32_t* outSelectedRecordIndex,
    uint32_t* outDirectionRankingAccepted,
    ExportAABBContact* outChosenContact,
    WoWCollision::SelectorPair* outChosenPair,
    ExportAABBContact* outContacts,
    WoWCollision::SelectorPair* outPairs,
    int maxOutputCount,
    float* outReportedBestRatio,
    WoWCollision::SelectorChosenIndexPairPreBridgeTransactionTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairPreBridgeTransactionTrace{};
    }

    if (outCandidatePlaneCount) {
        *outCandidatePlaneCount = 0u;
    }

    if (outSelectedRecordIndex) {
        *outSelectedRecordIndex = -1;
    }

    if (outDirectionRankingAccepted) {
        *outDirectionRankingAccepted = 0u;
    }

    if (outChosenContact) {
        *outChosenContact = ExportAABBContact{};
    }

    if (outChosenPair) {
        *outChosenPair = WoWCollision::SelectorPair{};
    }

    if (outReportedBestRatio) {
        *outReportedBestRatio = 0.0f;
    }

    if (!defaultPosition || !projectedPosition || !testPoint || !candidateDirection ||
        !cachedBoundsMin || !cachedBoundsMax || !outCandidatePlanes || !outCandidatePlaneCount ||
        !outSelectedRecordIndex || !outDirectionRankingAccepted || !outChosenContact || !outChosenPair ||
        !outContacts || !outPairs || !outReportedBestRatio || !outTrace ||
        recordCount < 0 || existingCount < 0 || queryCount < 0 || maxCandidatePlanes < 0 || maxOutputCount < 0 ||
        ((recordCount != 0) && !records) ||
        ((existingCount != 0) && (!existingContacts || !existingPairs)) ||
        ((queryCount != 0) && (!queryContacts || !queryPairs))) {
        return false;
    }

    auto buildContacts = [](const ExportAABBContact* exportContacts, int count) {
        std::vector<SceneQuery::AABBContact> contacts(static_cast<size_t>(count));
        for (int contactIndex = 0; contactIndex < count; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.point = exportContacts[contactIndex].point;
            contact.normal = exportContacts[contactIndex].normal;
            contact.rawNormal = exportContacts[contactIndex].rawNormal;
            contact.triangleA = exportContacts[contactIndex].triangleA;
            contact.triangleB = exportContacts[contactIndex].triangleB;
            contact.triangleC = exportContacts[contactIndex].triangleC;
            contact.planeDistance = exportContacts[contactIndex].planeDistance;
            contact.distance = exportContacts[contactIndex].distance;
            contact.instanceId = exportContacts[contactIndex].instanceId;
            contact.sourceType = exportContacts[contactIndex].sourceType;
            contact.walkable = exportContacts[contactIndex].walkable != 0u;
        }

        return contacts;
    };

    std::vector<WoWCollision::SelectorCandidateRecord> recordBuffer(static_cast<size_t>(recordCount));
    for (int recordIndex = 0; recordIndex < recordCount; ++recordIndex) {
        WoWCollision::SelectorCandidateRecord& record = recordBuffer[static_cast<size_t>(recordIndex)];
        record.filterPlane.normal = records[recordIndex].filterPlane.normal;
        record.filterPlane.planeDistance = records[recordIndex].filterPlane.planeDistance;
        record.points[0] = records[recordIndex].point0;
        record.points[1] = records[recordIndex].point1;
        record.points[2] = records[recordIndex].point2;
    }

    std::vector<SceneQuery::AABBContact> existingContactBuffer =
        existingCount > 0 ? buildContacts(existingContacts, existingCount) : std::vector<SceneQuery::AABBContact>{};
    std::vector<SceneQuery::AABBContact> queryContactBuffer =
        queryCount > 0 ? buildContacts(queryContacts, queryCount) : std::vector<SceneQuery::AABBContact>{};

    std::vector<SceneQuery::AABBContact> outputContactBuffer;
    std::vector<WoWCollision::SelectorPair> outputPairBuffer;
    SceneQuery::AABBContact chosenContact{};
    WoWCollision::SelectorPair chosenPair{};
    bool directionRankingAccepted = false;
    const bool result = WoWCollision::EvaluateSelectorChosenIndexPairPreBridgeTransaction(
        recordBuffer.empty() ? nullptr : recordBuffer.data(),
        static_cast<uint32_t>(recordCount),
        *defaultPosition,
        overridePosition,
        *projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        *testPoint,
        *candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        *cachedBoundsMin,
        *cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingCount > 0 ? existingContactBuffer.data() : nullptr,
        existingPairs,
        static_cast<uint32_t>(existingCount),
        queryCount > 0 ? queryContactBuffer.data() : nullptr,
        queryPairs,
        static_cast<uint32_t>(queryCount),
        queryDispatchSucceeded,
        inputVerticalOffset,
        swimVerticalOffsetScale,
        selectorBaseMatchesSwimReference,
        requestedDistanceClamp,
        requestedDistance,
        horizontalRadius,
        outCandidatePlanes,
        static_cast<uint32_t>(maxCandidatePlanes),
        *outCandidatePlaneCount,
        *outSelectedRecordIndex,
        directionRankingAccepted,
        chosenContact,
        chosenPair,
        outputContactBuffer,
        outputPairBuffer,
        *outReportedBestRatio,
        outTrace);

    *outDirectionRankingAccepted = directionRankingAccepted ? 1u : 0u;
    outChosenContact->point = chosenContact.point;
    outChosenContact->normal = chosenContact.normal;
    outChosenContact->rawNormal = chosenContact.rawNormal;
    outChosenContact->triangleA = chosenContact.triangleA;
    outChosenContact->triangleB = chosenContact.triangleB;
    outChosenContact->triangleC = chosenContact.triangleC;
    outChosenContact->planeDistance = chosenContact.planeDistance;
    outChosenContact->distance = chosenContact.distance;
    outChosenContact->instanceId = chosenContact.instanceId;
    outChosenContact->sourceType = chosenContact.sourceType;
    outChosenContact->walkable = chosenContact.walkable ? 1u : 0u;
    *outChosenPair = chosenPair;

    const int copiedCandidatePlaneCount = std::min<int>(static_cast<int>(*outCandidatePlaneCount), maxCandidatePlanes);
    for (int planeIndex = 0; planeIndex < copiedCandidatePlaneCount; ++planeIndex) {
        outCandidatePlanes[planeIndex].normal = outCandidatePlanes[planeIndex].normal;
        outCandidatePlanes[planeIndex].planeDistance = outCandidatePlanes[planeIndex].planeDistance;
    }

    const int outputCount = std::min<int>(static_cast<int>(outputContactBuffer.size()), maxOutputCount);
    for (int contactIndex = 0; contactIndex < outputCount; ++contactIndex) {
        const SceneQuery::AABBContact& contact = outputContactBuffer[static_cast<size_t>(contactIndex)];
        outContacts[contactIndex].point = contact.point;
        outContacts[contactIndex].normal = contact.normal;
        outContacts[contactIndex].rawNormal = contact.rawNormal;
        outContacts[contactIndex].triangleA = contact.triangleA;
        outContacts[contactIndex].triangleB = contact.triangleB;
        outContacts[contactIndex].triangleC = contact.triangleC;
        outContacts[contactIndex].planeDistance = contact.planeDistance;
        outContacts[contactIndex].distance = contact.distance;
        outContacts[contactIndex].instanceId = contact.instanceId;
        outContacts[contactIndex].sourceType = contact.sourceType;
        outContacts[contactIndex].walkable = contact.walkable ? 1u : 0u;
        outPairs[contactIndex] = outputPairBuffer[static_cast<size_t>(contactIndex)];
    }

    return result;
}

__declspec(dllexport) int32_t EvaluateWoWSelectorChosenIndexPairProducerTransaction(
    const G3D::Vector3* defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3* projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3* testPoint,
    const G3D::Vector3* candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3* cachedBoundsMin,
    const G3D::Vector3* cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const ExportAABBContact* existingContacts,
    const WoWCollision::SelectorPair* existingPairs,
    int existingCount,
    const ExportAABBContact* queryContacts,
    const WoWCollision::SelectorPair* queryPairs,
    int queryCount,
    bool queryDispatchSucceeded,
    bool rankingAccepted,
    uint32_t rankingCandidateCount,
    int32_t rankingSelectedRecordIndex,
    float rankingReportedBestRatio,
    const WoWCollision::SelectorSupportPlane* candidatePlanes,
    int candidatePlaneCount,
    const G3D::Vector3* currentPosition,
    float requestedDistance,
    const G3D::Vector3* inputMove,
    bool useStandardWalkableThreshold,
    float airborneTimeScalar,
    float elapsedTimeScalar,
    float horizontalSpeedScale,
    const WoWCollision::SelectorPair* alternatePair,
    WoWCollision::SelectorPair* outPair,
    uint32_t* outDirectStateDword,
    uint32_t* outAlternateUnitZStateDword,
    float* outReportedBestRatio,
    WoWCollision::SelectorChosenIndexPairProducerTransactionTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairProducerTransactionTrace{};
    }

    if (outPair) {
        *outPair = WoWCollision::SelectorPair{};
    }

    if (outDirectStateDword) {
        *outDirectStateDword = 0u;
    }

    if (outAlternateUnitZStateDword) {
        *outAlternateUnitZStateDword = 0u;
    }

    if (outReportedBestRatio) {
        *outReportedBestRatio = 0.0f;
    }

    if (!defaultPosition || !projectedPosition || !testPoint || !candidateDirection ||
        !cachedBoundsMin || !cachedBoundsMax || !currentPosition || !inputMove ||
        !alternatePair || !outPair || !outDirectStateDword || !outAlternateUnitZStateDword ||
        !outReportedBestRatio || existingCount < 0 || queryCount < 0 || candidatePlaneCount < 0 ||
        ((existingCount != 0) && (!existingContacts || !existingPairs)) ||
        ((queryCount != 0) && (!queryContacts || !queryPairs))) {
        return 0;
    }

    auto buildContacts = [](const ExportAABBContact* exportContacts, int count) {
        std::vector<SceneQuery::AABBContact> contacts(static_cast<size_t>(count));
        for (int contactIndex = 0; contactIndex < count; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.point = exportContacts[contactIndex].point;
            contact.normal = exportContacts[contactIndex].normal;
            contact.rawNormal = exportContacts[contactIndex].rawNormal;
            contact.triangleA = exportContacts[contactIndex].triangleA;
            contact.triangleB = exportContacts[contactIndex].triangleB;
            contact.triangleC = exportContacts[contactIndex].triangleC;
            contact.planeDistance = exportContacts[contactIndex].planeDistance;
            contact.distance = exportContacts[contactIndex].distance;
            contact.instanceId = exportContacts[contactIndex].instanceId;
            contact.sourceType = exportContacts[contactIndex].sourceType;
            contact.walkable = exportContacts[contactIndex].walkable != 0u;
        }

        return contacts;
    };

    std::vector<SceneQuery::AABBContact> existingContactBuffer =
        existingCount > 0 ? buildContacts(existingContacts, existingCount) : std::vector<SceneQuery::AABBContact>{};
    std::vector<SceneQuery::AABBContact> queryContactBuffer =
        queryCount > 0 ? buildContacts(queryContacts, queryCount) : std::vector<SceneQuery::AABBContact>{};

    return WoWCollision::EvaluateSelectorChosenIndexPairProducerTransaction(
        *defaultPosition,
        overridePosition,
        *projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        *testPoint,
        *candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        *cachedBoundsMin,
        *cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingCount > 0 ? existingContactBuffer.data() : nullptr,
        existingPairs,
        static_cast<uint32_t>(existingCount),
        queryCount > 0 ? queryContactBuffer.data() : nullptr,
        queryPairs,
        static_cast<uint32_t>(queryCount),
        queryDispatchSucceeded,
        rankingAccepted,
        rankingCandidateCount,
        rankingSelectedRecordIndex,
        rankingReportedBestRatio,
        candidatePlanes,
        static_cast<uint32_t>(candidatePlaneCount),
        *currentPosition,
        requestedDistance,
        *inputMove,
        useStandardWalkableThreshold,
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        *alternatePair,
        *outPair,
        *outDirectStateDword,
        *outAlternateUnitZStateDword,
        *outReportedBestRatio,
        outTrace);
}

__declspec(dllexport) int32_t EvaluateWoWSelectorChosenIndexPairDirectionSetupProducerTransaction(
    const ExportSelectorCandidateRecord* records,
    int recordCount,
    const G3D::Vector3* defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3* projectedPosition,
    uint32_t supportPlaneInitCount,
    uint32_t validationPlaneInitCount,
    uint32_t scratchPointZeroCount,
    const G3D::Vector3* testPoint,
    const G3D::Vector3* candidateDirection,
    float initialBestRatio,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3* cachedBoundsMin,
    const G3D::Vector3* cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const ExportAABBContact* existingContacts,
    const WoWCollision::SelectorPair* existingPairs,
    int existingCount,
    const ExportAABBContact* queryContacts,
    const WoWCollision::SelectorPair* queryPairs,
    int queryCount,
    bool queryDispatchSucceeded,
    float inputVerticalOffset,
    float swimVerticalOffsetScale,
    bool selectorBaseMatchesSwimReference,
    float requestedDistanceClamp,
    float horizontalRadius,
    const G3D::Vector3* currentPosition,
    float requestedDistance,
    const G3D::Vector3* inputMove,
    bool useStandardWalkableThreshold,
    float airborneTimeScalar,
    float elapsedTimeScalar,
    float horizontalSpeedScale,
    const WoWCollision::SelectorPair* alternatePair,
    WoWCollision::SelectorPair* outPair,
    uint32_t* outDirectStateDword,
    uint32_t* outAlternateUnitZStateDword,
    float* outReportedBestRatio,
    WoWCollision::SelectorChosenIndexPairDirectionSetupProducerTransactionTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairDirectionSetupProducerTransactionTrace{};
    }

    if (outPair) {
        *outPair = WoWCollision::SelectorPair{};
    }

    if (outDirectStateDword) {
        *outDirectStateDword = 0u;
    }

    if (outAlternateUnitZStateDword) {
        *outAlternateUnitZStateDword = 0u;
    }

    if (outReportedBestRatio) {
        *outReportedBestRatio = 0.0f;
    }

    if (!defaultPosition || !projectedPosition || !testPoint || !candidateDirection ||
        !cachedBoundsMin || !cachedBoundsMax || !currentPosition || !inputMove ||
        !alternatePair || !outPair || !outDirectStateDword || !outAlternateUnitZStateDword ||
        !outReportedBestRatio || recordCount < 0 || existingCount < 0 || queryCount < 0 ||
        ((recordCount != 0) && !records) ||
        ((existingCount != 0) && (!existingContacts || !existingPairs)) ||
        ((queryCount != 0) && (!queryContacts || !queryPairs))) {
        return 0;
    }

    auto buildContacts = [](const ExportAABBContact* exportContacts, int count) {
        std::vector<SceneQuery::AABBContact> contacts(static_cast<size_t>(count));
        for (int contactIndex = 0; contactIndex < count; ++contactIndex) {
            SceneQuery::AABBContact& contact = contacts[static_cast<size_t>(contactIndex)];
            contact.point = exportContacts[contactIndex].point;
            contact.normal = exportContacts[contactIndex].normal;
            contact.rawNormal = exportContacts[contactIndex].rawNormal;
            contact.triangleA = exportContacts[contactIndex].triangleA;
            contact.triangleB = exportContacts[contactIndex].triangleB;
            contact.triangleC = exportContacts[contactIndex].triangleC;
            contact.planeDistance = exportContacts[contactIndex].planeDistance;
            contact.distance = exportContacts[contactIndex].distance;
            contact.instanceId = exportContacts[contactIndex].instanceId;
            contact.sourceType = exportContacts[contactIndex].sourceType;
            contact.walkable = exportContacts[contactIndex].walkable != 0u;
        }

        return contacts;
    };

    std::vector<WoWCollision::SelectorCandidateRecord> recordBuffer(static_cast<size_t>(recordCount));
    for (int recordIndex = 0; recordIndex < recordCount; ++recordIndex) {
        WoWCollision::SelectorCandidateRecord& record = recordBuffer[static_cast<size_t>(recordIndex)];
        record.filterPlane.normal = records[recordIndex].filterPlane.normal;
        record.filterPlane.planeDistance = records[recordIndex].filterPlane.planeDistance;
        record.points[0] = records[recordIndex].point0;
        record.points[1] = records[recordIndex].point1;
        record.points[2] = records[recordIndex].point2;
    }

    std::vector<SceneQuery::AABBContact> existingContactBuffer =
        existingCount > 0 ? buildContacts(existingContacts, existingCount) : std::vector<SceneQuery::AABBContact>{};
    std::vector<SceneQuery::AABBContact> queryContactBuffer =
        queryCount > 0 ? buildContacts(queryContacts, queryCount) : std::vector<SceneQuery::AABBContact>{};

    return WoWCollision::EvaluateSelectorChosenIndexPairDirectionSetupProducerTransaction(
        recordBuffer.empty() ? nullptr : recordBuffer.data(),
        static_cast<uint32_t>(recordCount),
        *defaultPosition,
        overridePosition,
        *projectedPosition,
        supportPlaneInitCount,
        validationPlaneInitCount,
        scratchPointZeroCount,
        *testPoint,
        *candidateDirection,
        initialBestRatio,
        collisionRadius,
        boundingHeight,
        *cachedBoundsMin,
        *cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        existingCount > 0 ? existingContactBuffer.data() : nullptr,
        existingPairs,
        static_cast<uint32_t>(existingCount),
        queryCount > 0 ? queryContactBuffer.data() : nullptr,
        queryPairs,
        static_cast<uint32_t>(queryCount),
        queryDispatchSucceeded,
        inputVerticalOffset,
        swimVerticalOffsetScale,
        selectorBaseMatchesSwimReference,
        requestedDistanceClamp,
        horizontalRadius,
        *currentPosition,
        requestedDistance,
        *inputMove,
        useStandardWalkableThreshold,
        airborneTimeScalar,
        elapsedTimeScalar,
        horizontalSpeedScale,
        *alternatePair,
        *outPair,
        *outDirectStateDword,
        *outAlternateUnitZStateDword,
        *outReportedBestRatio,
        outTrace);
}

__declspec(dllexport) bool EvaluateWoWSelectorChosenIndexPairCallerTransaction(
    const G3D::Vector3* defaultPosition,
    const G3D::Vector3* overridePosition,
    const G3D::Vector3* projectedPosition,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3* cachedBoundsMin,
    const G3D::Vector3* cachedBoundsMax,
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
    float* outReportedBestRatio,
    WoWCollision::SelectorChosenIndexPairCallerTransactionTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairCallerTransactionTrace{};
    }

    if (outReportedBestRatio) {
        *outReportedBestRatio = 0.0f;
    }

    if (!defaultPosition || !projectedPosition || !cachedBoundsMin || !cachedBoundsMax || !outReportedBestRatio) {
        return false;
    }

    return WoWCollision::EvaluateSelectorChosenIndexPairCallerTransaction(
        *defaultPosition,
        overridePosition,
        *projectedPosition,
        collisionRadius,
        boundingHeight,
        *cachedBoundsMin,
        *cachedBoundsMax,
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
        *outReportedBestRatio,
        outTrace);
}

__declspec(dllexport) bool EvaluateWoWSelectorChosenIndexPairDirectionSetupTransaction(
    const ExportSelectorCandidateRecord* records,
    int recordCount,
    const G3D::Vector3* defaultPosition,
    const G3D::Vector3* overridePosition,
    float inputReportedBestRatioSeed,
    float inputVerticalOffset,
    float swimVerticalOffsetScale,
    bool selectorBaseMatchesSwimReference,
    uint32_t movementFlags,
    float requestedDistance,
    float requestedDistanceClamp,
    const G3D::Vector3* testPoint,
    const G3D::Vector3* candidateDirection,
    float horizontalRadius,
    WoWCollision::SelectorSupportPlane* outCandidatePlanes,
    int maxCandidatePlanes,
    uint32_t* outCandidateCount,
    int32_t* outSelectedRecordIndex,
    float* outReportedBestRatio,
    WoWCollision::SelectorChosenIndexPairDirectionSetupTrace* outTrace)
{
    if (outTrace) {
        *outTrace = WoWCollision::SelectorChosenIndexPairDirectionSetupTrace{};
    }

    if (outCandidateCount) {
        *outCandidateCount = 0u;
    }

    if (outSelectedRecordIndex) {
        *outSelectedRecordIndex = -1;
    }

    if (outReportedBestRatio) {
        *outReportedBestRatio = inputReportedBestRatioSeed;
    }

    if (!defaultPosition || !testPoint || !candidateDirection || !outCandidateCount || !outSelectedRecordIndex ||
        !outReportedBestRatio || recordCount < 0 || maxCandidatePlanes < 0 ||
        ((recordCount != 0) && !records) ||
        ((maxCandidatePlanes != 0) && !outCandidatePlanes)) {
        return false;
    }

    std::vector<WoWCollision::SelectorCandidateRecord> recordBuffer(static_cast<size_t>(recordCount));
    for (int recordIndex = 0; recordIndex < recordCount; ++recordIndex) {
        WoWCollision::SelectorCandidateRecord& record = recordBuffer[static_cast<size_t>(recordIndex)];
        record.filterPlane.normal = records[recordIndex].filterPlane.normal;
        record.filterPlane.planeDistance = records[recordIndex].filterPlane.planeDistance;
        record.points[0] = records[recordIndex].point0;
        record.points[1] = records[recordIndex].point1;
        record.points[2] = records[recordIndex].point2;
    }

    return WoWCollision::EvaluateSelectorChosenIndexPairDirectionSetupTransaction(
        recordBuffer.empty() ? nullptr : recordBuffer.data(),
        static_cast<uint32_t>(recordCount),
        *defaultPosition,
        overridePosition,
        inputReportedBestRatioSeed,
        inputVerticalOffset,
        swimVerticalOffsetScale,
        selectorBaseMatchesSwimReference,
        movementFlags,
        requestedDistance,
        requestedDistanceClamp,
        *testPoint,
        *candidateDirection,
        horizontalRadius,
        outCandidatePlanes,
        static_cast<uint32_t>(maxCandidatePlanes),
        *outCandidateCount,
        *outSelectedRecordIndex,
        *outReportedBestRatio,
        outTrace);
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorPairPostForwardingDispatch(
    int pairForwardReturnCode,
    uint32_t directStateBit,
    uint32_t alternateUnitZStateBit,
    float windowSpanScalar,
    float windowStartScalar,
    const G3D::Vector3* moveVector,
    float horizontalReferenceMagnitude,
    uint32_t movementFlags,
    float verticalSpeed,
    float horizontalSpeedScale,
    float referenceZ,
    float positionZ,
    WoWCollision::SelectorPairPostForwardingTrace* outTrace)
{
    if (!moveVector) {
        if (outTrace) {
            *outTrace = WoWCollision::SelectorPairPostForwardingTrace{};
        }
        return WoWCollision::SELECTOR_PAIR_POST_FORWARDING_FAILURE;
    }

    const WoWCollision::SelectorPairPostForwardingTrace trace = WoWCollision::EvaluateSelectorPairPostForwardingDispatch(
        pairForwardReturnCode,
        directStateBit != 0u,
        alternateUnitZStateBit != 0u,
        windowSpanScalar,
        windowStartScalar,
        *moveVector,
        horizontalReferenceMagnitude,
        movementFlags,
        verticalSpeed,
        horizontalSpeedScale,
        referenceZ,
        positionZ);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.dispatchKind;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorAlternateUnitZStateHandler(
    uint32_t movementFlags,
    float positionZ,
    WoWCollision::SelectorAlternateUnitZStateTrace* outTrace)
{
    const WoWCollision::SelectorAlternateUnitZStateTrace trace =
        WoWCollision::EvaluateSelectorAlternateUnitZStateHandler(movementFlags, positionZ);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.outputMovementFlags;
}

__declspec(dllexport) uint32_t EvaluateWoWSelectorDirectStateHandler(
    uint32_t movementFlags,
    const G3D::Vector3* startPosition,
    float facing,
    float pitch,
    const G3D::Vector3* cachedPosition,
    float cachedFacing,
    float cachedPitch,
    uint32_t cachedMoveTimestamp,
    float cachedScalar84,
    float recomputedScalar84,
    WoWCollision::SelectorDirectStateTrace* outTrace)
{
    if (!startPosition || !cachedPosition) {
        if (outTrace) {
            *outTrace = WoWCollision::SelectorDirectStateTrace{};
        }
        return 0u;
    }

    const WoWCollision::SelectorDirectStateTrace trace = WoWCollision::EvaluateSelectorDirectStateHandler(
        movementFlags,
        *startPosition,
        facing,
        pitch,
        *cachedPosition,
        cachedFacing,
        cachedPitch,
        cachedMoveTimestamp,
        cachedScalar84,
        recomputedScalar84);

    if (outTrace) {
        *outTrace = trace;
    }

    return trace.outputMovementFlags;
}

}
