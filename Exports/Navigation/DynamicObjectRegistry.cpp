#include "DynamicObjectRegistry.h"
#include "WorldModel.h"
#include <cmath>
#include <cstdio>
#include <algorithm>
#include <filesystem>
#include <iostream>
#include <limits>

namespace
{
bool SegmentTriangleIntersectionT(
    const G3D::Vector3& p0,
    const G3D::Vector3& p1,
    const G3D::Vector3& ta,
    const G3D::Vector3& tb,
    const G3D::Vector3& tc,
    float* outT)
{
    const float eps = 1e-8f;
    const G3D::Vector3 dir = p1 - p0;
    const G3D::Vector3 e1 = tb - ta;
    const G3D::Vector3 e2 = tc - ta;
    const G3D::Vector3 h = dir.cross(e2);
    const float a = e1.dot(h);
    if (fabsf(a) < eps)
        return false;

    const float f = 1.0f / a;
    const G3D::Vector3 s = p0 - ta;
    const float u = f * s.dot(h);
    if (u < 0.0f || u > 1.0f)
        return false;

    const G3D::Vector3 q = s.cross(e1);
    const float v = f * dir.dot(q);
    if (v < 0.0f || u + v > 1.0f)
        return false;

    const float t = f * e2.dot(q);
    if (t < 0.0f || t > 1.0f)
        return false;

    if (outT)
        *outT = t;

    return true;
}
}

DynamicObjectRegistry* DynamicObjectRegistry::s_instance = nullptr;

DynamicObjectRegistry* DynamicObjectRegistry::Instance()
{
    if (!s_instance)
        s_instance = new DynamicObjectRegistry();
    return s_instance;
}

uint32_t DynamicObjectRegistry::AllocateRuntimeInstanceId()
{
    if (m_nextRuntimeInstanceId < 0x80000001u)
        m_nextRuntimeInstanceId = 0x80000001u;

    uint32_t instanceId = m_nextRuntimeInstanceId++;
    if (m_nextRuntimeInstanceId == 0)
        m_nextRuntimeInstanceId = 0x80000001u;

    return instanceId;
}

// ==========================================================================
// DisplayId mapping (from temp_gameobject_models index file)
// ==========================================================================

bool DynamicObjectRegistry::LoadDisplayIdMapping(const std::string& vmapsBasePath)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_mappingLoaded) return true;
    m_vmapsBasePath = vmapsBasePath;

    // The index file maps displayId → model name + bounding box.
    // Format per entry: uint32 displayId, uint32 nameLen, char[nameLen] name, 6 floats bbox
    std::string indexPath = vmapsBasePath + "temp_gameobject_models";
    if (!std::filesystem::exists(indexPath))
    {
        std::cerr << "[DynObjReg] Index file not found: " << indexPath << "\n";
        return false;
    }

    FILE* rf = fopen(indexPath.c_str(), "rb");
    if (!rf) return false;

    int count = 0;
    while (true)
    {
        uint32_t displayId, nameLen;
        if (fread(&displayId, sizeof(uint32_t), 1, rf) != 1) break;
        if (fread(&nameLen, sizeof(uint32_t), 1, rf) != 1) break;

        if (nameLen == 0 || nameLen > 500)
        {
            std::cerr << "[DynObjReg] Bad nameLen=" << nameLen << " at entry " << count << "\n";
            break;
        }

        std::vector<char> nameBuf(nameLen + 1, 0);
        if (fread(nameBuf.data(), 1, nameLen, rf) != nameLen) break;

        float bbox[6];
        if (fread(bbox, sizeof(float), 6, rf) != 6) break;

        DisplayIdEntry entry;
        entry.modelName = std::string(nameBuf.data());
        entry.bounds = G3D::AABox(
            G3D::Vector3(bbox[0], bbox[1], bbox[2]),
            G3D::Vector3(bbox[3], bbox[4], bbox[5]));

        m_displayIdMap[displayId] = std::move(entry);
        ++count;
    }

    fclose(rf);
    m_mappingLoaded = true;
    std::cout << "[DynObjReg] Loaded " << count << " displayId mappings from " << indexPath << "\n";
    return count > 0;
}

// ==========================================================================
// Model loading (from .vmo files)
// ==========================================================================

std::shared_ptr<DynamicObjectRegistry::CachedModel>
DynamicObjectRegistry::LoadModel(const std::string& modelName)
{
    // Check cache first
    auto it = m_modelCache.find(modelName);
    if (it != m_modelCache.end())
        return it->second;

    // Build the .vmo file path. Models are stored as "{modelName}.vmo" in vmaps root.
    std::string vmoPath = m_vmapsBasePath + modelName + ".vmo";
    if (!std::filesystem::exists(vmoPath))
    {
        // Try lowercase
        std::string lowerName = modelName;
        std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(), ::tolower);
        vmoPath = m_vmapsBasePath + lowerName + ".vmo";
    }

    if (!std::filesystem::exists(vmoPath))
    {
        // Try case-insensitive scan
        std::string targetLower = modelName;
        std::transform(targetLower.begin(), targetLower.end(), targetLower.begin(), ::tolower);
        targetLower += ".vmo";
        vmoPath.clear();

        for (const auto& entry : std::filesystem::directory_iterator(m_vmapsBasePath))
        {
            if (!entry.is_regular_file()) continue;
            std::string fn = entry.path().filename().string();
            std::string fnLower = fn;
            std::transform(fnLower.begin(), fnLower.end(), fnLower.begin(), ::tolower);
            if (fnLower == targetLower)
            {
                vmoPath = entry.path().string();
                break;
            }
        }
    }

    if (vmoPath.empty() || !std::filesystem::exists(vmoPath))
    {
        std::cerr << "[DynObjReg] Model file not found for: " << modelName << "\n";
        m_modelCache[modelName] = nullptr; // cache the miss
        return nullptr;
    }

    // Load the WorldModel
    VMAP::WorldModel wm;
    if (!wm.readFile(vmoPath))
    {
        std::cerr << "[DynObjReg] Failed to load model: " << vmoPath << "\n";
        m_modelCache[modelName] = nullptr;
        return nullptr;
    }

    // Extract all mesh data
    auto cached = std::make_shared<CachedModel>();
    cached->modelName = modelName;

    if (!wm.GetAllMeshData(cached->localVertices, cached->localIndices))
    {
        std::cerr << "[DynObjReg] No mesh data in model: " << vmoPath << "\n";
        m_modelCache[modelName] = nullptr;
        return nullptr;
    }

    // Compute local bounds
    if (!cached->localVertices.empty())
    {
        G3D::Vector3 bmin = cached->localVertices[0], bmax = cached->localVertices[0];
        for (size_t i = 1; i < cached->localVertices.size(); ++i)
        {
            bmin = bmin.min(cached->localVertices[i]);
            bmax = bmax.max(cached->localVertices[i]);
        }
        cached->localBounds = G3D::AABox(bmin, bmax);
    }

    std::cout << "[DynObjReg] Loaded model '" << modelName << "': "
              << cached->localVertices.size() << " vertices, "
              << (cached->localIndices.size() / 3) << " triangles\n";

    m_modelCache[modelName] = cached;
    return cached;
}

// ==========================================================================
// Registration
// ==========================================================================

/// Check if a model name contains "door" (case-insensitive).
static bool IsDoorModel(const std::string& modelName)
{
    std::string lower = modelName;
    std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
    return lower.find("door") != std::string::npos;
}

bool DynamicObjectRegistry::EnsureRegistered(
    uint64_t guid, uint32_t displayId, uint32_t mapId, float scale)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Already registered?
    if (m_objects.count(guid) > 0)
        return true;

    // Look up model name from displayId
    auto mapIt = m_displayIdMap.find(displayId);
    if (mapIt == m_displayIdMap.end())
        return false;  // Unknown displayId — silently skip (avoid spam)

    // Load the model mesh (cached)
    auto model = LoadModel(mapIt->second.modelName);
    if (!model)
        return false;

    DynamicObject obj;
    obj.guid = guid;
    obj.entry = 0;
    obj.displayId = displayId;
    obj.mapId = mapId;
    obj.runtimeInstanceId = AllocateRuntimeInstanceId();
    obj.scale = scale;
    obj.model = model;
    obj.isDoorModel = IsDoorModel(mapIt->second.modelName);

    m_instanceIdToGuid[obj.runtimeInstanceId] = guid;
    m_objects[guid] = std::move(obj);
    return true;
}

bool DynamicObjectRegistry::RegisterObject(
    uint64_t guid, uint32_t entry, uint32_t displayId,
    uint32_t mapId, float scale)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Look up model name from displayId
    auto mapIt = m_displayIdMap.find(displayId);
    if (mapIt == m_displayIdMap.end())
    {
        std::cerr << "[DynObjReg] Unknown displayId " << displayId << " for entry " << entry << "\n";
        return false;
    }

    // Load the model mesh (cached)
    auto model = LoadModel(mapIt->second.modelName);
    if (!model)
        return false;

    DynamicObject obj;
    obj.guid = guid;
    obj.entry = entry;
    obj.displayId = displayId;
    obj.mapId = mapId;
    obj.runtimeInstanceId = AllocateRuntimeInstanceId();
    obj.scale = scale;
    obj.model = model;
    obj.isDoorModel = IsDoorModel(mapIt->second.modelName);

    m_instanceIdToGuid[obj.runtimeInstanceId] = guid;
    m_objects[guid] = std::move(obj);
    return true;
}

// ==========================================================================
// Position update
// ==========================================================================

void DynamicObjectRegistry::UpdatePosition(
    uint64_t guid, float x, float y, float z, float orientation,
    uint32_t goState)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_objects.find(guid);
    if (it == m_objects.end()) return;

    auto& obj = it->second;
    obj.posX = x;
    obj.posY = y;
    obj.posZ = z;
    obj.orientation = orientation;
    obj.goState = goState;
    obj.RebuildWorldTriangles();
}

void DynamicObjectRegistry::DynamicObject::RebuildWorldTriangles()
{
    worldTriangles.clear();
    if (!model || model->localIndices.empty()) return;

    // Build world vertices: scale → rotate around Z → translate
    float cosO = cosf(orientation);
    float sinO = sinf(orientation);

    const auto& localVerts = model->localVertices;
    std::vector<G3D::Vector3> worldVerts(localVerts.size());
    for (size_t i = 0; i < localVerts.size(); ++i)
    {
        float sx = localVerts[i].x * scale;
        float sy = localVerts[i].y * scale;
        float sz = localVerts[i].z * scale;
        float wx = sx * cosO - sy * sinO + posX;
        float wy = sx * sinO + sy * cosO + posY;
        float wz = sz + posZ;
        worldVerts[i] = G3D::Vector3(wx, wy, wz);
    }

    // Build AABB
    G3D::Vector3 bmin = worldVerts[0], bmax = worldVerts[0];
    for (size_t i = 1; i < worldVerts.size(); ++i)
    {
        bmin = bmin.min(worldVerts[i]);
        bmax = bmax.max(worldVerts[i]);
    }
    worldBounds = G3D::AABox(bmin, bmax);

    // Build triangles
    const auto& indices = model->localIndices;
    int triCount = (int)indices.size() / 3;
    worldTriangles.reserve(triCount);
    for (int t = 0; t < triCount; ++t)
    {
        uint32_t ia = indices[t * 3 + 0];
        uint32_t ib = indices[t * 3 + 1];
        uint32_t ic = indices[t * 3 + 2];

        CapsuleCollision::Triangle tri;
        tri.a = { worldVerts[ia].x, worldVerts[ia].y, worldVerts[ia].z };
        tri.b = { worldVerts[ib].x, worldVerts[ib].y, worldVerts[ib].z };
        tri.c = { worldVerts[ic].x, worldVerts[ic].y, worldVerts[ic].z };
        tri.doubleSided = false;
        tri.collisionMask = 0xFFFFFFFFu;
        worldTriangles.push_back(tri);
    }
}

// ==========================================================================
// Removal
// ==========================================================================

void DynamicObjectRegistry::Unregister(uint64_t guid)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    auto it = m_objects.find(guid);
    if (it != m_objects.end())
    {
        m_instanceIdToGuid.erase(it->second.runtimeInstanceId);
        m_objects.erase(it);
    }
}

void DynamicObjectRegistry::ClearMap(uint32_t mapId)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    for (auto it = m_objects.begin(); it != m_objects.end(); )
    {
        if (it->second.mapId == mapId)
        {
            m_instanceIdToGuid.erase(it->second.runtimeInstanceId);
            it = m_objects.erase(it);
        }
        else
            ++it;
    }
}

void DynamicObjectRegistry::ClearAll()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    m_objects.clear();
    m_instanceIdToGuid.clear();
}

// ==========================================================================
// Query
// ==========================================================================

void DynamicObjectRegistry::QueryTriangles(
    uint32_t mapId, const G3D::AABox& worldAABB,
    std::vector<CapsuleCollision::Triangle>& outTriangles,
    std::vector<uint32_t>* outInstanceIds) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    for (const auto& [guid, obj] : m_objects)
    {
        if (obj.mapId != mapId) continue;
        if (obj.worldTriangles.empty()) continue;

        // Door models in Active state (goState=0 = open/used): skip from collision.
        // The .vmo mesh represents the default (closed) pose. When Active (open),
        // the door has been animated to a different position we can't replicate.
        // Ready (goState=1) = closed = mesh matches reality = KEEP in collision.
        if (obj.isDoorModel && obj.goState == 0)
            continue;

        // AABB overlap test
        if (obj.worldBounds.high().x < worldAABB.low().x ||
            obj.worldBounds.low().x > worldAABB.high().x ||
            obj.worldBounds.high().y < worldAABB.low().y ||
            obj.worldBounds.low().y > worldAABB.high().y ||
            obj.worldBounds.high().z < worldAABB.low().z ||
            obj.worldBounds.low().z > worldAABB.high().z)
            continue;

        outTriangles.insert(outTriangles.end(),
            obj.worldTriangles.begin(), obj.worldTriangles.end());
        if (outInstanceIds)
            outInstanceIds->insert(outInstanceIds->end(), obj.worldTriangles.size(), obj.runtimeInstanceId);
    }
}

bool DynamicObjectRegistry::TryGetLocalPoint(
    uint32_t instanceId, const G3D::Vector3& worldPoint, G3D::Vector3& outLocalPoint) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    auto guidIt = m_instanceIdToGuid.find(instanceId);
    if (guidIt == m_instanceIdToGuid.end())
        return false;

    auto objIt = m_objects.find(guidIt->second);
    if (objIt == m_objects.end())
        return false;

    const auto& obj = objIt->second;
    const float scale = std::fabs(obj.scale) > 1e-6f ? obj.scale : 1.0f;
    const float cosO = std::cos(obj.orientation);
    const float sinO = std::sin(obj.orientation);
    const float dx = worldPoint.x - obj.posX;
    const float dy = worldPoint.y - obj.posY;
    const float dz = worldPoint.z - obj.posZ;

    outLocalPoint.x = (dx * cosO + dy * sinO) / scale;
    outLocalPoint.y = (-dx * sinO + dy * cosO) / scale;
    outLocalPoint.z = dz / scale;
    return true;
}

bool DynamicObjectRegistry::FindFirstIntersectingObject(
    uint32_t mapId,
    const G3D::Vector3& start,
    const G3D::Vector3& end,
    uint32_t* outInstanceId,
    uint64_t* outGuid,
    uint32_t* outDisplayId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    const float pad = 0.5f;
    const G3D::AABox segBox(
        G3D::Vector3(
            std::min(start.x, end.x) - pad,
            std::min(start.y, end.y) - pad,
            std::min(start.z, end.z) - pad),
        G3D::Vector3(
            std::max(start.x, end.x) + pad,
            std::max(start.y, end.y) + pad,
            std::max(start.z, end.z) + pad));

    bool found = false;
    float bestT = std::numeric_limits<float>::infinity();
    uint32_t bestInstanceId = 0;
    uint64_t bestGuid = 0;
    uint32_t bestDisplayId = 0;

    for (const auto& [guid, obj] : m_objects)
    {
        if (obj.mapId != mapId)
            continue;

        if (obj.worldTriangles.empty())
            continue;

        if (obj.isDoorModel && obj.goState == 0)
            continue;

        if (obj.worldBounds.high().x < segBox.low().x ||
            obj.worldBounds.low().x > segBox.high().x ||
            obj.worldBounds.high().y < segBox.low().y ||
            obj.worldBounds.low().y > segBox.high().y ||
            obj.worldBounds.high().z < segBox.low().z ||
            obj.worldBounds.low().z > segBox.high().z)
        {
            continue;
        }

        for (const auto& tri : obj.worldTriangles)
        {
            const G3D::Vector3 ta(tri.a.x, tri.a.y, tri.a.z);
            const G3D::Vector3 tb(tri.b.x, tri.b.y, tri.b.z);
            const G3D::Vector3 tc(tri.c.x, tri.c.y, tri.c.z);
            float hitT = 0.0f;
            if (!SegmentTriangleIntersectionT(start, end, ta, tb, tc, &hitT))
                continue;

            if (!found ||
                hitT < bestT - 1e-6f ||
                (fabsf(hitT - bestT) <= 1e-6f && obj.runtimeInstanceId < bestInstanceId))
            {
                found = true;
                bestT = hitT;
                bestInstanceId = obj.runtimeInstanceId;
                bestGuid = guid;
                bestDisplayId = obj.displayId;
            }
        }
    }

    if (!found)
        return false;

    if (outInstanceId)
        *outInstanceId = bestInstanceId;

    if (outGuid)
        *outGuid = bestGuid;

    if (outDisplayId)
        *outDisplayId = bestDisplayId;

    return true;
}

int DynamicObjectRegistry::Count() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return (int)m_objects.size();
}

int DynamicObjectRegistry::CachedModelCount() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    int valid = 0;
    for (const auto& [name, ptr] : m_modelCache)
        if (ptr) ++valid;
    return valid;
}

bool DynamicObjectRegistry::HasDisplayId(uint32_t displayId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_displayIdMap.count(displayId) > 0;
}
