// PhysicsEngine.cpp - Simplified physics tuned toward vanilla 1.12.1 feel

#include "PhysicsEngine.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "MapLoader.h"
#include "Navigation.h"
#include "VMapLog.h"
#include "CylinderCollision.h" // for CylinderHelpers walkable config
#include "ModelInstance.h"     // for debug diagnostics on model collisions

#include <algorithm>
#include <filesystem>
#include <iostream>
#include <iomanip>
#include <cfloat>
#include <chrono>

using namespace PhysicsConstants;
using namespace VMAP;

// Global physics logging configuration (defaults)
int gPhysLogLevel = 3;           // 0=ERR,1=INFO,2=DBG,3=TRACE
uint32_t gPhysLogMask = PHYS_ALL; // enable everything initially

static uint64_t gPhysFrameCounter = 0;

// Debug: force step-up onto collided model tops to verify step logic
static bool gForceRideTop = true;

// Helper: pick final Z by raycasting down from above a reference height (server-like)
static float RaycastDownFrom(VMAP::VMapManager2* vm, uint32_t mapId, float x, float y, float refZ)
{
    if (!vm) return refZ;
    // Start slightly above refZ and search downward a few meters
    float z = vm->getHeight(mapId, x, y, refZ + 0.5f, 6.0f);
    if (z > PhysicsConstants::INVALID_HEIGHT)
        return z;
    return refZ;
}

// Helper: try to find a nearby placement on top surface using contact data
static bool TryFindStepUpPlacement(VMAP::VMapManager2* vm, uint32_t mapId,
    float baseX, float baseY, float refZ,
    const G3D::Vector3& contactPoint, const G3D::Vector3& contactNormal,
    const G3D::Vector3& intentDir, float radius, float height,
    float& outX, float& outY, float& outZ)
{
    if (!vm) return false;

    struct Probe { float x, y; };
    std::vector<Probe> probes;

    // p0: at contact point XY
    probes.push_back({ contactPoint.x, contactPoint.y });

    // p1: push slightly inward from the surface (towards -normal XY)
    G3D::Vector3 nxy(contactNormal.x, contactNormal.y, 0.0f);
    if (nxy.magnitude() > 0.0001f)
    {
        G3D::Vector3 inward = (-nxy) / nxy.magnitude();
        probes.push_back({ contactPoint.x + inward.x * radius * 0.6f,
                           contactPoint.y + inward.y * radius * 0.6f });
    }

    // p2: slightly forward along movement direction
    if (intentDir.x != 0.0f || intentDir.y != 0.0f)
    {
        probes.push_back({ baseX + intentDir.x * radius * 0.6f,
                           baseY + intentDir.y * radius * 0.6f });
    }

    // p3: original base XY (fallback)
    probes.push_back({ baseX, baseY });

    for (const auto& p : probes)
    {
        // Query supportive height at probe XY using cylinder-aware height
        float h = vm->GetCylinderHeight(mapId, p.x, p.y, refZ + 0.5f, radius, height, 4.0f);
        PHYS_TRACE(PHYS_SURF, "probeXY x=" << p.x << " y=" << p.y << " refZ=" << refZ << " h=" << h);
        if (h <= PhysicsConstants::INVALID_HEIGHT)
            continue;

        VMAP::Cylinder fitCyl(G3D::Vector3(p.x, p.y, h), G3D::Vector3(0,0,1), radius, height);
        if (!vm->CanCylinderFitAtPosition(mapId, fitCyl, 0.02f))
            continue;

        outX = p.x; outY = p.y; outZ = h;
        return true;
    }

    return false;
}

const char* PhysCatName(uint32_t cat)
{
    switch (cat) {
    case PHYS_MOVE: return "MOVE"; case PHYS_SURF: return "SURF"; case PHYS_HEAD: return "HEAD"; case PHYS_CYL: return "CYL"; case PHYS_STEP: return "STEP"; case PHYS_WALL: return "WALL"; case PHYS_PERF: return "PERF"; default: return "?"; }
}
const char* PhysLevelName(int lvl)
{
    switch (lvl) { case 0: return "ERR"; case 1: return "INF"; case 2: return "DBG"; case 3: return "TRC"; default: return "?"; }
}

// =====================================================================================
// Singleton
// =====================================================================================
PhysicsEngine* PhysicsEngine::s_instance = nullptr;

PhysicsEngine::PhysicsEngine()
    : m_vmapManager(nullptr),
      m_initialized(false),
      m_walkableCosMin(DEFAULT_WALKABLE_MIN_NORMAL_Z)
{
    // Ensure helpers see initial value
    VMAP::CylinderHelpers::SetWalkableCosMin(m_walkableCosMin);
}

PhysicsEngine::~PhysicsEngine()
{
    Shutdown();
}

PhysicsEngine* PhysicsEngine::Instance()
{
    if (!s_instance)
        s_instance = new PhysicsEngine();
    return s_instance;
}

void PhysicsEngine::Destroy()
{
    delete s_instance;
    s_instance = nullptr;
}

// =====================================================================================
// Configuration
// =====================================================================================
void PhysicsEngine::SetWalkableCosMin(float cosMin)
{
    // Clamp to [0,1]
    if (cosMin < 0.0f) cosMin = 0.0f; else if (cosMin > 1.0f) cosMin = 1.0f;
    m_walkableCosMin = cosMin;
    VMAP::CylinderHelpers::SetWalkableCosMin(m_walkableCosMin);
}

void PhysicsEngine::SetWalkableSlopeDegrees(float degrees)
{
    // convert degrees to radians, take cos
    float radians = degrees * (3.14159265358979323846f / 180.0f);
    SetWalkableCosMin(std::cos(radians));
}

float PhysicsEngine::GetWalkableCosMin() const
{
    return m_walkableCosMin;
}

// =====================================================================================
// Initialization / Shutdown
// =====================================================================================
void PhysicsEngine::Initialize()
{
    if (m_initialized)
        return;

    // Re-apply helper threshold in case external users configured before init
    VMAP::CylinderHelpers::SetWalkableCosMin(m_walkableCosMin);

    m_mapLoader = std::make_unique<MapLoader>();
    std::vector<std::string> paths = { "maps/", "Data/maps/", "../Data/maps/" };
    for (auto& p : paths)
    {
        if (std::filesystem::exists(p) && m_mapLoader->Initialize(p))
            break;
    }

    try
    {
        m_vmapManager = static_cast<VMAP::VMapManager2*>(VMAP::VMapFactory::createOrGetVMapManager());
        if (m_vmapManager)
        {
            VMAP::VMapFactory::initialize();
            std::vector<std::string> vps = { "vmaps/", "Data/vmaps/", "../Data/vmaps/" };
            for (auto& vp : vps)
            {
                if (std::filesystem::exists(vp))
                {
                    m_vmapManager->setBasePath(vp);
                    break;
                }
            }
        }
    }
    catch (...)
    {
        m_vmapManager = nullptr;
    }

    m_initialized = true;
    PHYS_INFO(PHYS_MOVE, "Initialize done");
}

void PhysicsEngine::Shutdown()
{
    PHYS_INFO(PHYS_MOVE, "Shutdown");
    m_vmapManager = nullptr;
    m_mapLoader.reset();
    m_initialized = false;
}

// =====================================================================================
// Core helpers
// =====================================================================================
void PhysicsEngine::EnsureMapLoaded(uint32_t mapId)
{
    if (m_vmapManager && !m_vmapManager->isMapInitialized(mapId)) {
        m_vmapManager->initializeMap(mapId);
        PHYS_DBG(PHYS_MOVE, "Map initialized id=" << mapId);
    }
}

float PhysicsEngine::GetTerrainHeight(uint32_t mapId, float x, float y)
{
    if (!m_mapLoader || !m_mapLoader->IsInitialized())
        return INVALID_HEIGHT;
    return m_mapLoader->GetHeight(mapId, x, y);
}

float PhysicsEngine::GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType)
{
    if (m_mapLoader && m_mapLoader->IsInitialized())
    {
        float level = m_mapLoader->GetLiquidLevel(mapId, x, y);
        if (level > INVALID_HEIGHT)
        {
            liquidType = m_mapLoader->GetLiquidType(mapId, x, y);
            return level;
        }
    }

    if (m_vmapManager)
    {
        float level, floor; uint32_t type;
        if (m_vmapManager->GetLiquidLevel(mapId, x, y, z, 0xFF, level, floor, type))
        {
            liquidType = type;
            return level;
        }
    }

    return INVALID_HEIGHT;
}

Cylinder PhysicsEngine::CreatePlayerCylinder(float x, float y, float z, float radius, float height) const
{
    return Cylinder(G3D::Vector3(x, y, z), G3D::Vector3(0, 0, 1), radius, height);
}

G3D::Vector3 PhysicsEngine::ComputeTerrainNormal(uint32_t mapId, float x, float y)
{
    const float s = 0.75f;
    float hL = GetTerrainHeight(mapId, x - s, y);
    float hR = GetTerrainHeight(mapId, x + s, y);
    float hD = GetTerrainHeight(mapId, x, y - s);
    float hU = GetTerrainHeight(mapId, x, y + s);
    if (hL <= INVALID_HEIGHT || hR <= INVALID_HEIGHT || hD <= INVALID_HEIGHT || hU <= INVALID_HEIGHT)
        return { 0, 0, 1 };
    G3D::Vector3 dx(2 * s, 0, hR - hL);
    G3D::Vector3 dy(0, 2 * s, hU - hD);
    G3D::Vector3 n = dx.cross(dy);
    float len = n.magnitude();
    return (len < 0.0001f) ? G3D::Vector3(0, 0, 1) : n / len;
}

G3D::Vector3 PhysicsEngine::ApproximateVMapNormal(uint32_t mapId, float x, float y, float referenceZ,
                                                  float cylinderRadius, float cylinderHeight)
{
    if (!m_vmapManager)
        return { 0, 0, 1 };

    float centerH = m_vmapManager->GetCylinderHeight(mapId, x, y, referenceZ + 0.5f,
                                                     cylinderRadius, cylinderHeight, 2.0f);
    if (centerH <= INVALID_HEIGHT)
        return { 0, 0, 1 };

    float offset = std::min(0.5f, cylinderRadius * 0.5f);
    auto sample = [&](float sx, float sy) -> float
    {
        float h = m_vmapManager->GetCylinderHeight(mapId, sx, sy, referenceZ + 0.5f,
                                                   cylinderRadius, cylinderHeight, 2.0f);
        return (h > INVALID_HEIGHT) ? h : centerH;
    };

    float hXp = sample(x + offset, y);
    float hXn = sample(x - offset, y);
    float hYp = sample(x, y + offset);
    float hYn = sample(x, y - offset);

    G3D::Vector3 dxv(2 * offset, 0, hXp - hXn);
    G3D::Vector3 dyv(0, 2 * offset, hYp - hYn);
    G3D::Vector3 n = dxv.cross(dyv);
    float len = n.magnitude();
    G3D::Vector3 out = (len < 0.0001f) ? G3D::Vector3(0, 0, 1) : n / len;
    PHYS_TRACE(PHYS_SURF, "normalApprox samples center=" << centerH
        << " x+=" << hXp << " x-=" << hXn << " y+=" << hYp << " y-=" << hYn
        << " nZ=" << out.z);
    return out;
}

G3D::Vector3 PhysicsEngine::ProjectOntoGroundPlane(const G3D::Vector3& desired, const G3D::Vector3& normal) const
{
    float d = desired.dot(normal);
    G3D::Vector3 p = desired - normal * d;
    float len = p.magnitude();
    return (len < 0.0001f) ? G3D::Vector3(0, 0, 0) : p / len;
}

// =====================================================================================
// Head Clearance
// =====================================================================================
bool PhysicsEngine::HasHeadClearance(uint32_t mapId, float x, float y, float newZ, float radius, float height)
{
    if (!m_vmapManager)
        return true;

    const float baseAllowance = 0.06f;
    const float inflateTol    = 0.02f;
    const float headStartFrac = 0.35f;
    const float minSliceH     = 0.30f;

    if (height <= 0.1f)
        return true;

    // Phase 0
    {
        float liftedBase = newZ + baseAllowance;
        float liftedHeight = std::max(0.0f, height - baseAllowance);
        Cylinder cylLift = CreatePlayerCylinder(x, y, liftedBase, radius * 0.998f, liftedHeight);
        bool fit = m_vmapManager->CanCylinderFitAtPosition(mapId, cylLift, inflateTol);
        PHYS_TRACE(PHYS_HEAD, "PH0 liftedBase=" << liftedBase << " h=" << liftedHeight << " fit=" << (fit?1:0));
        if (fit)
            return true;
    }

    // Phase 1
    float headBase = newZ + height * headStartFrac;
    float headHeight = height - height * headStartFrac;
    if (headHeight > 0.05f)
    {
        Cylinder cylHead = CreatePlayerCylinder(x, y, headBase, radius * 0.995f, headHeight - 0.01f);
        bool headFit = m_vmapManager->CanCylinderFitAtPosition(mapId, cylHead, inflateTol);
        PHYS_TRACE(PHYS_HEAD, "PH1 headBase=" << headBase << " h=" << headHeight << " fit=" << (headFit?1:0));
        if (headFit)
        {
            PHYS_INFO(PHYS_HEAD, "HEAD-ONLY success map=" << mapId);
            return true;
        }
    }

    // Phase 2: require continuous clearance for the entire upper segment
    float segmentTop   = newZ + height;
    float sliceFloor   = newZ + std::max(baseAllowance, height * headStartFrac);
    float upperHeight  = std::max(0.0f, segmentTop - sliceFloor);
    if (upperHeight > minSliceH * 0.5f)
    {
        Cylinder contCyl = CreatePlayerCylinder(x, y, sliceFloor, radius * 0.99f, upperHeight - 0.01f);
        bool contFit = m_vmapManager->CanCylinderFitAtPosition(mapId, contCyl, inflateTol);
        PHYS_TRACE(PHYS_HEAD, "PH2-CONT base=" << sliceFloor << " h=" << upperHeight << " fit=" << (contFit?1:0));
        if (contFit)
        {
            PHYS_INFO(PHYS_HEAD, "UPPER-CONTINUOUS success map=" << mapId);
            return true;
        }
    }

    // Diagnostics
    Cylinder full = CreatePlayerCylinder(x, y, newZ, radius, height);
    bool fullFit = m_vmapManager->CanCylinderFitAtPosition(mapId, full, 0.01f);
    float topProbeHeight = std::min(0.6f, height * 0.6f);
    Cylinder top = CreatePlayerCylinder(x, y, newZ + height - topProbeHeight, radius * 0.98f, topProbeHeight - 0.01f);
    bool topFit = m_vmapManager->CanCylinderFitAtPosition(mapId, top, 0.015f);
    PHYS_INFO(PHYS_HEAD, "FAIL map=" << mapId << " fullFit=" << (fullFit?1:0) << " topFit=" << (topFit?1:0));
    return false;
}

// =====================================================================================
// Walkable surface search
// =====================================================================================
PhysicsEngine::WalkableSurface PhysicsEngine::FindWalkableSurfaceWithCylinder(uint32_t mapId, float x, float y,
    float currentZ, float maxStepUp, float maxStepDown, float radius, float height)
{
    auto t0 = std::chrono::high_resolution_clock::now();
    WalkableSurface best{ false, INVALID_HEIGHT, SurfaceSource::NONE, { 0, 0, 1 } };

    if (m_vmapManager)
    {
        EnsureMapLoaded(mapId);
        const float GRID = 533.33333f;
        const float MID = 32.0f * GRID;
        int tileX = int((MID - y) / GRID);
        int tileY = int((MID - x) / GRID);
        m_vmapManager->loadMap(nullptr, mapId, tileX, tileY);
    }

    struct Candidate { float h; SurfaceSource src; G3D::Vector3 n; };
    std::vector<Candidate> baseCandidates;
    std::vector<Candidate> upwardCandidates;

    float cosMin = VMAP::CylinderHelpers::GetWalkableCosMin();

    float tH = GetTerrainHeight(mapId, x, y);
    if (tH > INVALID_HEIGHT)
    {
        float diff = tH - currentZ;
        if (diff >= -(maxStepDown + GROUND_HEIGHT_TOLERANCE) && diff <= maxStepUp + GROUND_HEIGHT_TOLERANCE)
        {
            G3D::Vector3 n = ComputeTerrainNormal(mapId, x, y);
            if (n.z >= cosMin)
                baseCandidates.push_back({ tH, SurfaceSource::TERRAIN, n });
        }
    }

    if (m_vmapManager)
    {
        float queryZ = currentZ + maxStepUp * 0.5f;
        float vh = m_vmapManager->GetCylinderHeight(mapId, x, y, queryZ, radius, height, 4.0f);
        if (vh > INVALID_HEIGHT)
        {
            float diff = vh - currentZ;
            if (diff >= -(maxStepDown + GROUND_HEIGHT_TOLERANCE) && diff <= maxStepUp + GROUND_HEIGHT_TOLERANCE)
            {
                G3D::Vector3 n = ApproximateVMapNormal(mapId, x, y, vh, radius, height);
                if (n.z >= cosMin)
                    baseCandidates.push_back({ vh, SurfaceSource::VMAP, n });
            }
        }
        const float inc = 0.25f;
        for (float raised = inc; raised <= maxStepUp + GROUND_HEIGHT_TOLERANCE; raised += inc)
        {
            float queryZ2 = currentZ + raised;
            float vh2 = m_vmapManager->GetCylinderHeight(mapId, x, y, queryZ2, radius, height, 4.0f);
            if (vh2 <= INVALID_HEIGHT) continue;
            float diff = vh2 - currentZ;
            if (diff < -GROUND_HEIGHT_TOLERANCE || diff > maxStepUp + GROUND_HEIGHT_TOLERANCE) continue;
            G3D::Vector3 n2 = ApproximateVMapNormal(mapId, x, y, vh2, radius, height);
            if (n2.z < cosMin) continue;
            if (!HasHeadClearance(mapId, x, y, vh2, radius, height)) continue;
            upwardCandidates.push_back({ vh2, SurfaceSource::VMAP, n2 });
            if (diff <= inc + GROUND_HEIGHT_TOLERANCE) break;
        }
    }

    auto pickBetter = [&](const Candidate& a, const Candidate& b)
    {
        float da = a.h - currentZ; float db = b.h - currentZ;
        if (da >= 0 && db < 0) return true;
        if (db >= 0 && da < 0) return false;
        if (da >= 0 && db >= 0) return da < db;
        return a.h > b.h;
    };

    if (!upwardCandidates.empty())
    {
        Candidate chosen = upwardCandidates.front();
        for (auto& c : upwardCandidates)
            if ((c.h - currentZ) < (chosen.h - currentZ))
                chosen = c;
        best = { true, chosen.h, chosen.src, chosen.n };
    }
    else
    {
        for (auto& c : baseCandidates)
        {
            if (!best.found) { best = { true, c.h, c.src, c.n }; continue; }
            Candidate cur{ best.height, best.source, best.normal };
            if (pickBetter(c, cur)) { best.height = c.h; best.source = c.src; best.normal = c.n; }
        }
    }

    auto t1 = std::chrono::high_resolution_clock::now();
    auto us = std::chrono::duration_cast<std::chrono::microseconds>(t1 - t0).count();
    if (best.found)
        PHYS_TRACE(PHYS_SURF, "surface h=" << best.height << " src=" << (best.source==SurfaceSource::TERRAIN?"terrain":"vmap") << " nZ=" << best.normal.z << " dt_us=" << us << " cosMin=" << cosMin);
    else
        PHYS_TRACE(PHYS_SURF, "surface none dt_us=" << us);
    return best;
}

// =====================================================================================
// Movement helpers
// =====================================================================================
PhysicsEngine::MovementIntent PhysicsEngine::BuildMovementIntent(const PhysicsInput& input, float orientation) const
{
    MovementIntent intent{}; float c = std::cos(orientation); float s = std::sin(orientation);
    float dirX = 0.0f, dirY = 0.0f;
    if (input.moveFlags & MOVEFLAG_FORWARD)      { dirX += c;  dirY += s; }
    if (input.moveFlags & MOVEFLAG_BACKWARD)     { dirX -= c;  dirY -= s; }
    if (input.moveFlags & MOVEFLAG_STRAFE_LEFT)  { dirX += s;  dirY -= c; }
    if (input.moveFlags & MOVEFLAG_STRAFE_RIGHT) { dirX -= s;  dirY += c; }
    float mag = std::sqrt(dirX*dirX + dirY*dirY);
    if (mag > 0.0001f) { dirX /= mag; dirY /= mag; intent.hasInput = true; }
    intent.dir = G3D::Vector3(dirX, dirY, 0.0f);
    intent.jumpRequested = (input.moveFlags & MOVEFLAG_JUMPING) != 0;
    return intent;
}

PhysicsEngine::WalkableSurface PhysicsEngine::QueryGroundSurface(uint32_t mapId, float x, float y, float z, float r, float h) const
{
    return const_cast<PhysicsEngine*>(this)->FindWalkableSurfaceWithCylinder(mapId, x, y, z,
        STEP_HEIGHT, STEP_DOWN_HEIGHT, r, h);
}

float PhysicsEngine::QueryLiquidLevel(uint32_t mapId, float x, float y, float z, uint32_t& liquidType) const
{
    return const_cast<PhysicsEngine*>(this)->GetLiquidHeight(mapId, x, y, z, liquidType);
}

void PhysicsEngine::ResolveGroundAttachment(MovementState& st, const WalkableSurface& surf,
                                            float stepUpLimit, float stepDownLimit, float)
{
    if (surf.found)
    {
        float diff = surf.height - st.z;
        if ((diff >= 0.0f && diff <= stepUpLimit + GROUND_HEIGHT_TOLERANCE) ||
            (diff < 0.0f && diff >= -stepDownLimit - GROUND_HEIGHT_TOLERANCE))
        { st.z = surf.height; st.vz = 0; st.isGrounded = true; st.groundNormal = surf.normal; return; }
    }
    st.isGrounded = false; st.groundNormal = { 0, 0, 1 };
}

float PhysicsEngine::CalculateMoveSpeed(const PhysicsInput& input, bool swim)
{
    if (swim) return input.swimSpeed;
    if (input.moveFlags & MOVEFLAG_WALK_MODE) return input.walkSpeed;
    if (input.moveFlags & MOVEFLAG_BACKWARD)  return input.runBackSpeed;
    return input.runSpeed;
}

void PhysicsEngine::ApplyGravity(MovementState& st, float dt)
{
    st.vz -= GRAVITY * dt; if (st.vz < -60.0f) st.vz = -60.0f;
}

bool PhysicsEngine::ValidateCylinderPosition(uint32_t mapId, float x, float y, float z,
                                             float tolerance, float radius, float height)
{
    if (!m_vmapManager) return true;
    Cylinder cyl = CreatePlayerCylinder(x, y, z, radius, height);
    bool ok = m_vmapManager->CanCylinderMoveAtPosition(mapId, cyl, tolerance);
    PHYS_TRACE(PHYS_MOVE, "Validate x=" << x << " y=" << y << " z=" << z << " r=" << radius << " h=" << height << " ok=" << (ok?1:0));
    return ok;
}

// =====================================================================================
// Ground movement with slope and step fallbacks
// =====================================================================================
void PhysicsEngine::ProcessGroundMovementWithCylinder(const PhysicsInput& input, const MovementIntent& intent,
                                                      MovementState& st, float dt, float speed,
                                                      float radius, float height)
{
    if (intent.jumpRequested) { st.vz = JUMP_VELOCITY; st.isGrounded = false; st.fallTime = 0; PHYS_INFO(PHYS_MOVE, "jump vz=" << st.vz); return; }

    if (intent.hasInput) { st.vx = intent.dir.x * speed; st.vy = intent.dir.y * speed; } else { st.vx = st.vy = 0; }

    float newX = st.x + st.vx * dt; float newY = st.y + st.vy * dt;

    // Diagnostics helper to inspect nearby VMAP models and walkable surface at a location
    auto diagModels = [&](float dx, float dy, float dz, const char* tag)
    {
        if (!m_vmapManager) return;
        Cylinder cylHere = CreatePlayerCylinder(dx, dy, dz, radius, height);
        std::vector<ModelInstance*> instances;
        m_vmapManager->GetCylinderCollisionCandidates(input.mapId, cylHere, instances);
        PHYS_INFO(PHYS_STEP, std::string("[DIAG]") << tag << " pos=" << dx << "," << dy << "," << dz << " instCount=" << instances.size());
        size_t limit = std::min<size_t>(instances.size(), 5);
        for (size_t i = 0; i < limit; ++i)
        {
            const ModelInstance* mi = instances[i];
            G3D::AABox b = mi->getBounds();
            G3D::Vector3 lo = b.low(); G3D::Vector3 hi = b.high();
            PHYS_INFO(PHYS_STEP, "  inst[" << i << "] name='" << mi->name << "' id=" << mi->ID << " adt=" << mi->adtId
                << " boundsLo=" << lo.x << "," << lo.y << "," << lo.z
                << " hi=" << hi.x << "," << hi.y << "," << hi.z);
        }
        float outH = INVALID_HEIGHT; G3D::Vector3 outN(0,0,1);
        bool found = m_vmapManager->FindCylinderWalkableSurface(input.mapId, cylHere, dz, STEP_HEIGHT, STEP_DOWN_HEIGHT, outH, outN);
        PHYS_INFO(PHYS_STEP, "  walkable found=" << (found?1:0) << " h=" << outH << " nZ=" << outN.z);
        if (found)
        {
            Cylinder fit = CreatePlayerCylinder(dx, dy, outH, radius, height);
            bool fitOk = m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f);
            PHYS_INFO(PHYS_CYL, "  fitAtH=" << (fitOk?1:0));
        }
    };

    // Helper: when blocked, log the exact collided model instance (name + id) if available
    auto logBlockingInstance = [&](float px, float py, float pz, const char* tag)
    {
        if (!m_vmapManager) return;
        float ch = 0.0f; G3D::Vector3 n(0,0,1); ModelInstance* hit = nullptr;
        Cylinder c = CreatePlayerCylinder(px, py, pz, radius, height);
        if (m_vmapManager->CheckCylinderCollision(input.mapId, c, ch, n, &hit) && hit)
        {
            PHYS_INFO(PHYS_CYL, std::string("[HIT]") << tag << " name='" << hit->name << "' id=" << hit->ID
                << " adt=" << hit->adtId << " contactH=" << ch << " nZ=" << n.z);
        }
    };

    auto tryAdvance = [&](float& tx, float& ty) -> bool
    {
        if (ValidateCylinderPosition(input.mapId, tx, ty, st.z + 0.01f, 0.02f, radius, height)) return true;
        PHYS_TRACE(PHYS_MOVE, "blocked initial advance");

        // Try to immediately ride on top of the blocking model at its contact height (debug)
        if (gForceRideTop && m_vmapManager)
        {
            float ch = INVALID_HEIGHT; G3D::Vector3 n(0,0,1); ModelInstance* hit = nullptr;
            Cylinder c = CreatePlayerCylinder(tx, ty, st.z + 0.01f, radius, height);
            if (m_vmapManager->CheckCylinderCollision(input.mapId, c, ch, n, &hit) && ch > INVALID_HEIGHT)
            {
                float placeX = tx, placeY = ty, placeZ = ch;
                // Use triangle contact to find a stable placement on the top surface
                if (TryFindStepUpPlacement(m_vmapManager, input.mapId, tx, ty, ch, /*contactPoint*/ G3D::Vector3(tx,ty,ch), n, intent.dir, radius, height, placeX, placeY, placeZ))
                {
                    Cylinder fit = CreatePlayerCylinder(placeX, placeY, placeZ, radius, height);
                    bool headOK = HasHeadClearance(input.mapId, placeX, placeY, placeZ, radius, height);
                    if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f) && headOK)
                    {
                        float finalZ = RaycastDownFrom(m_vmapManager, input.mapId, placeX, placeY, placeZ);
                        st.x = placeX; st.y = placeY; st.z = finalZ; st.groundNormal = n;
                        newX = st.x; newY = st.y;
                        PHYS_INFO(PHYS_STEP, "[FORCE] rideTop(atAdvance) place (" << placeX << "," << placeY << ") ch=" << ch << " rayZ=" << finalZ);
                        return true;
                    }
                }
            }
        }

        logBlockingInstance(tx, ty, st.z + 0.01f, "advance");
        diagModels(tx, ty, st.z, "blocked");
        AttemptWallSlide(input, intent, st, dt, speed, radius, height);
        tx = st.x + st.vx * dt; ty = st.y + st.vy * dt;
        if (ValidateCylinderPosition(input.mapId, tx, ty, st.z + 0.01f, 0.02f, radius, height)) return true;
        logBlockingInstance(tx, ty, st.z + 0.01f, "afterSlide");
        G3D::Vector3 mv(st.vx * dt, st.vy * dt, 0); float len = mv.magnitude();
        if (len > 0.0001f)
        {
            G3D::Vector3 step = mv * (-1.0f / std::max(4.0f, len / 0.1f)); float pX = tx; float pY = ty;
            for (int i=0;i<8;++i){ pX += step.x; pY += step.y; if (ValidateCylinderPosition(input.mapId, pX, pY, st.z + 0.01f, 0.02f, radius, height)) { tx=pX; ty=pY; PHYS_TRACE(PHYS_MOVE, "backoff success i="<<i); return true; } }
        }
        st.vx = st.vy = 0; PHYS_TRACE(PHYS_MOVE, "advance failed zero velocity"); logBlockingInstance(tx, ty, st.z + 0.01f, "advanceFail"); diagModels(tx, ty, st.z, "advanceFail"); return false; };

    if (!tryAdvance(newX, newY))
    {
        // Attempt a step-up when the initial horizontal move is blocked
        const float inc = 0.25f;
        float raised = 0.0f;
        while (raised < STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE)
        {
            raised += inc;
            float probeBaseZ = st.z + raised;
            if (!HasHeadClearance(input.mapId, newX, newY, probeBaseZ, radius, height))
            {
                PHYS_TRACE(PHYS_HEAD, "blockedStep headBlock raised="<<raised);
                break; // cannot step further up if head is blocked
            }

            // Check we can actually place the cylinder at the raised Z at the new XY
            if (!ValidateCylinderPosition(input.mapId, newX, newY, probeBaseZ + 0.01f, 0.02f, radius, height))
            {
                PHYS_TRACE(PHYS_STEP, "blockedStep raise="<<raised<<" movePos invalid");
                logBlockingInstance(newX, newY, probeBaseZ + 0.01f, "stepRaise");
                continue;
            }

            // Find a supporting walkable surface from the raised base (only allow remaining step-up)
            WalkableSurface probe = FindWalkableSurfaceWithCylinder(input.mapId, newX, newY, probeBaseZ,
                STEP_HEIGHT - raised, STEP_DOWN_HEIGHT, radius, height);
            if (!probe.found)
            {
                PHYS_TRACE(PHYS_STEP, "blockedStep raise="<<raised<<" no surface");
                continue;
            }

            float totalRaise = probe.height - st.z;
            if (totalRaise >= -GROUND_HEIGHT_TOLERANCE && totalRaise <= STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE)
            {
                Cylinder fit = CreatePlayerCylinder(newX, newY, probe.height, radius, height);
                if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f))
                {
                    // Commit the step-up
                    st.x = newX; st.y = newY; st.z = probe.height; st.groundNormal = probe.normal;
                    PHYS_TRACE(PHYS_STEP, "blockedStepUp success height="<<probe.height<<" totalRaise="<<totalRaise);
                    return; // done for this frame
                }
                PHYS_TRACE(PHYS_CYL, "blockedStep fitReject h="<<probe.height);
                logBlockingInstance(newX, newY, probe.height + 0.01f, "fitReject");
                diagModels(newX, newY, probe.height, "fitReject");
            }
            else
            {
                PHYS_TRACE(PHYS_STEP, "blockedStep raise="<<raised<<" totalRaiseOutOfRange="<<totalRaise);
            }
        }

        // Forced step-up (debug): ride along the top of whatever we collided with
        if (gForceRideTop && m_vmapManager)
        {
            // 1) Prefer exact contact height from a direct collision probe
            float ch = INVALID_HEIGHT; G3D::Vector3 n(0,0,1); ModelInstance* hit = nullptr;
            Cylinder ctest = CreatePlayerCylinder(newX, newY, st.z + 0.01f, radius, height);
            if (m_vmapManager->CheckCylinderCollision(input.mapId, ctest, ch, n, &hit) && ch > INVALID_HEIGHT)
            {
                float placeX = newX, placeY = newY, placeZ = ch;
                if (TryFindStepUpPlacement(m_vmapManager, input.mapId, newX, newY, ch, G3D::Vector3(newX,newY,ch), n, intent.dir, radius, height, placeX, placeY, placeZ))
                {
                    Cylinder fit = CreatePlayerCylinder(placeX, placeY, placeZ, radius, height);
                    bool headOK = HasHeadClearance(input.mapId, placeX, placeY, placeZ, radius, height);
                    if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f) && headOK)
                    {
                        float finalZ = RaycastDownFrom(m_vmapManager, input.mapId, placeX, placeY, placeZ);
                        st.x = placeX; st.y = placeY; st.z = finalZ; st.groundNormal = n;
                        PHYS_INFO(PHYS_STEP, "[FORCE] rideTop(contactH) place (" << placeX << "," << placeY << ") ch=" << ch << " rayZ=" << finalZ);
                        return;
                    }
                }
            }

            // 2) Fallback to searching a larger upward range for walkable surface
            Cylinder cylProbe = CreatePlayerCylinder(newX, newY, st.z, radius, height);
            float forcedH = INVALID_HEIGHT; G3D::Vector3 forcedN(0,0,1);
            bool found = m_vmapManager->FindCylinderWalkableSurface(
                input.mapId, cylProbe, st.z, /*maxStepUp*/ 6.0f, /*maxStepDown*/ 0.0f, forcedH, forcedN);
            if (found && forcedH > INVALID_HEIGHT)
            {
                Cylinder fit2 = CreatePlayerCylinder(newX, newY, forcedH, radius, height);
                bool headOK2 = HasHeadClearance(input.mapId, newX, newY, forcedH, radius, height);
                if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit2, 0.02f) && headOK2)
                {
                    float finalZ = RaycastDownFrom(m_vmapManager, input.mapId, newX, newY, forcedH);
                    st.x = newX; st.y = newY; st.z = finalZ; st.groundNormal = forcedN;
                    PHYS_INFO(PHYS_STEP, "[FORCE] rideTop(search) baseH=" << forcedH << " rayZ=" << finalZ << " nZ=" << forcedN.z);
                    return;
                }
                else
                {
                    PHYS_INFO(PHYS_STEP, "[FORCE] rideTop(search) candidate rejected fit=" << (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit2, 0.02f)?1:0)
                        << " headOK=" << (headOK2?1:0) << " h=" << forcedH);
                }
            }
            else
            {
                PHYS_TRACE(PHYS_STEP, "[FORCE] rideTop no surface found");
            }
        }

        return; // No valid step-up found; stay in place after zeroing velocity in tryAdvance
    }

    WalkableSurface surface = FindWalkableSurfaceWithCylinder(input.mapId, newX, newY, st.z,
        STEP_HEIGHT, STEP_DOWN_HEIGHT, radius, height);

    auto applySurface = [&](const WalkableSurface& ws)
    { st.x = newX; st.y = newY; st.z = ws.height; st.groundNormal = ws.normal; };

    if (surface.found)
    {
        float diff = surface.height - st.z;
        PHYS_TRACE(PHYS_SURF, "surfaceFound diff=" << diff << " curZ=" << st.z << " targetZ=" << surface.height);
        if (diff <= STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE && diff >= -STEP_DOWN_HEIGHT - GROUND_HEIGHT_TOLERANCE)
        {
            // For pure downward snap, do not require head clearance; rely on fit check only
            Cylinder fit = CreatePlayerCylinder(newX, newY, surface.height, radius, height);
            if (!m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f)) {
                if (diff < -GROUND_HEIGHT_TOLERANCE) { st.x = newX; st.y = newY; PHYS_TRACE(PHYS_CYL, "downMoveKeepZ fitReject surfaceHeight="<<surface.height); return; }
                if (diff > GROUND_HEIGHT_TOLERANCE) { // upward small step fallback
                    float probeRefZ = surface.height + 0.05f;
                    float supportH = m_vmapManager ? m_vmapManager->GetCylinderHeight(input.mapId, newX, newY, probeRefZ, radius, height, 1.0f) : INVALID_HEIGHT;
                    if (supportH > INVALID_HEIGHT && std::fabs(supportH - surface.height) <= 0.15f) {
                        if (HasHeadClearance(input.mapId, newX, newY, surface.height, radius, height)) {
                            applySurface(surface); PHYS_TRACE(PHYS_STEP, "fallbackStepUp success h="<<surface.height<<" diff="<<diff<<" supportH="<<supportH); return; }
                    }
                }
                PHYS_TRACE(PHYS_CYL, "fitReject surfaceHeight="<<surface.height); return; }
            applySurface(surface); PHYS_TRACE(PHYS_SURF, "snapZ newZ="<<st.z); return;
        }

        if (diff > STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE)
        {
            const float inc = 0.25f; float raised = 0.0f; WalkableSurface stepSurf{ false, 0, SurfaceSource::NONE, { 0, 0, 1 } };
            while (raised < STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE)
            {
                raised += inc; float probeBaseZ = st.z + raised;
                if (!HasHeadClearance(input.mapId, newX, newY, probeBaseZ, radius, height)) { PHYS_TRACE(PHYS_HEAD, "stepProbe headBlock raised="<<raised); break; }
                WalkableSurface probe = FindWalkableSurfaceWithCylinder(input.mapId, newX, newY, probeBaseZ,
                    STEP_HEIGHT - raised, STEP_DOWN_HEIGHT, radius, height);
                if (!probe.found) continue;
                float totalRaise = probe.height - st.z;
                PHYS_TRACE(PHYS_STEP, "probeRaise="<<raised<<" totalRaise="<<totalRaise);
                if (totalRaise >= -GROUND_HEIGHT_TOLERANCE && totalRaise <= STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE)
                {
                    Cylinder fit = CreatePlayerCylinder(newX, newY, probe.height, radius, height);
                    if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f)) { stepSurf = probe; PHYS_TRACE(PHYS_STEP, "stepSuccess height="<<probe.height); }
                    break;
                }
            }
            if (stepSurf.found) { applySurface(stepSurf); st.x += intent.dir.x * 0.02f; st.y += intent.dir.y * 0.02f; return; }
            AttemptWallSlide(input, intent, st, dt, speed, radius, height);
            newX = st.x + st.vx * dt; newY = st.y + st.vy * dt;
            if (st.vx != 0 || st.vy != 0)
            {
                WalkableSurface slide = FindWalkableSurfaceWithCylinder(input.mapId, newX, newY, st.z,
                    STEP_HEIGHT, STEP_DOWN_HEIGHT, radius, height);
                if (slide.found)
                {
                    float sdiff = slide.height - st.z;
                    if (sdiff <= STEP_HEIGHT + GROUND_HEIGHT_TOLERANCE && sdiff >= -STEP_DOWN_HEIGHT - GROUND_HEIGHT_TOLERANCE)
                    {
                        Cylinder fit2 = CreatePlayerCylinder(newX, newY, slide.height, radius, height);
                        if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit2, 0.02f)) { applySurface(slide); return; }
                    }
                }
            }
            st.vx = st.vy = 0; return;
        }
        st.x = newX; st.y = newY; return; // large drop beyond step-down
    }

    // If no surface was found at the target location, still advance horizontally and let next frame resolve grounding
    st.x = newX;
    st.y = newY;
}

// =====================================================================================
// Air / Swim movement
// =====================================================================================
void PhysicsEngine::ProcessAirMovement(const PhysicsInput& input, const MovementIntent& intent,
                                       MovementState& st, float dt, float speed)
{
    st.fallTime += dt; ApplyGravity(st, dt);
    float curX = st.vx, curY = st.vy, tgtX = curX, tgtY = curY;
    if (intent.hasInput) { tgtX = intent.dir.x * speed; tgtY = intent.dir.y * speed; }
    float dX = tgtX - curX, dY = tgtY - curY; float len = std::sqrt(dX*dX + dY*dY);
    if (len > 0.0001f) { float maxChange = AIR_ACCEL * dt; if (len > maxChange) { float scale = maxChange / len; dX *= scale; dY *= scale; } curX += dX; curY += dY; }
    st.vx = curX; st.vy = curY; st.x += st.vx * dt; st.y += st.vy * dt; st.z += st.vz * dt;
    WalkableSurface ground = FindWalkableSurfaceWithCylinder(input.mapId, st.x, st.y, st.z,
        STEP_HEIGHT, DEFAULT_HEIGHT_SEARCH, input.radius, input.height);
    if (st.vz <= 0 && ground.found) { float diff = ground.height - st.z; if (diff >= -STEP_DOWN_HEIGHT - GROUND_HEIGHT_TOLERANCE && diff <= GROUND_HEIGHT_TOLERANCE) { st.z = ground.height; st.vz = 0; st.isGrounded = true; st.fallTime = 0; st.groundNormal = ground.normal; PHYS_TRACE(PHYS_MOVE, "land diff="<<diff); } }
}

void PhysicsEngine::ProcessSwimMovement(const PhysicsInput& input, const MovementIntent& intent,
                                        MovementState& st, float dt, float speed)
{
    if (intent.hasInput) { st.vx = intent.dir.x * speed; st.vy = intent.dir.y * speed; } else { st.vx = st.vy = 0; }
    float desiredVz = 0.0f; if (intent.hasInput && (input.moveFlags & MOVEFLAG_FORWARD)) desiredVz = std::sin(st.pitch) * speed; st.vz = desiredVz;
    st.x += st.vx * dt; st.y += st.vy * dt; st.z += st.vz * dt;
}

// =====================================================================================
// Wall slide
// =====================================================================================
void PhysicsEngine::AttemptWallSlide(const PhysicsInput& input, const MovementIntent& intent,
                                     MovementState& state, float dt, float /*speed*/,
                                     float radius, float height)
{
    if (!intent.hasInput || !m_vmapManager) return;
    if (state.vx == 0.0f && state.vy == 0.0f) return;

    G3D::Vector3 vel(state.vx, state.vy, 0.0f);
    float moveLen = vel.magnitude();
    if (moveLen < 0.0001f) return;

    // Build movement-aligned basis
    G3D::Vector3 dir = vel / moveLen;            // forward axis in XY
    G3D::Vector3 right(-dir.y, dir.x, 0.0f);     // lateral axis in XY

    // First, prefer collision normal from VMAP if available (more stable than gradient on near-flat surfaces)
    auto tryCollisionNormal = [&](G3D::Vector3& outN) -> bool
    {
        float ch = INVALID_HEIGHT; G3D::Vector3 hitN(0,0,1); ModelInstance* hit = nullptr;
        // Probe at current and slightly advanced position
        float ahead = std::max(0.2f, radius * 0.4f);
        struct Probe { float x,y; } probes[2] = { { state.x, state.y }, { state.x + dir.x * ahead, state.y + dir.y * ahead } };
        for (auto& p : probes)
        {
            Cylinder c = CreatePlayerCylinder(p.x, p.y, state.z + 0.01f, radius, height);
            if (m_vmapManager->CheckCylinderCollision(input.mapId, c, ch, hitN, &hit))
            {
                // Build horizontal-only wall normal from collision normal to avoid tilting due to small z
                G3D::Vector3 nxy(hitN.x, hitN.y, 0.0f);
                float nxyLen = nxy.magnitude();
                if (nxyLen > 0.0001f)
                {
                    nxy = nxy / nxyLen;
                    // Ensure normal faces against movement direction in XY to avoid adding speed
                    if (nxy.dot(G3D::Vector3(dir.x, dir.y, 0.0f)) > 0.0f)
                        nxy = -nxy;
                    outN = nxy; // use pure horizontal normal for slide projection
                    PHYS_TRACE(PHYS_WALL, "slide using collision normal n=[" << outN.x << "," << outN.y << "," << outN.z << "]");
                    return true;
                }
            }
        }
        return false;
    };

    // Prefer collision-derived normal
    G3D::Vector3 n;
    if (!tryCollisionNormal(n))
    {
        // Sampling distances
        float forwardDist = std::min(0.5f, moveLen * dt + radius * 0.5f);
        float lateralDist = std::max(0.1f, radius * 0.6f);

        auto sampleHeightAt = [&](const G3D::Vector3& offset) -> float
        {
            return m_vmapManager->GetCylinderHeight(
                input.mapId,
                state.x + offset.x,
                state.y + offset.y,
                state.z + STEP_HEIGHT * 0.5f,
                radius,
                height,
                4.0f);
        };

        // Center reference height and neighbor samples (use center fallback for invalids)
        float hC = sampleHeightAt({0,0,0});
        if (hC <= INVALID_HEIGHT)
            return; // no reference surface ahead; abort slide

        float hF = sampleHeightAt(dir * forwardDist);
        float hB = sampleHeightAt(dir * -forwardDist);
        float hR = sampleHeightAt(right * lateralDist);
        float hL = sampleHeightAt(right * -lateralDist);

        if (hF <= INVALID_HEIGHT) hF = hC;
        if (hB <= INVALID_HEIGHT) hB = hC;
        if (hR <= INVALID_HEIGHT) hR = hC;
        if (hL <= INVALID_HEIGHT) hL = hC;

        // Build gradient-aligned basis vectors including vertical deltas, then cross for normal
        G3D::Vector3 vForward(2.0f * forwardDist * dir.x,  2.0f * forwardDist * dir.y,  hF - hB);
        G3D::Vector3 vLateral(2.0f * lateralDist * right.x, 2.0f * lateralDist * right.y, hR - hL);

        n = vForward.cross(vLateral);
        float nLen = n.magnitude();

        if (nLen < 0.0001f)
        {
            // Degenerate gradient; approximate with horizontal normal opposing motion
            n = G3D::Vector3(-dir.x, -dir.y, 0.0f);
            nLen = n.magnitude();
            if (nLen < 0.0001f) return;
        }

        n = n / nLen;

        // Ensure normal faces against movement direction in XY to avoid adding speed
        if (n.dot(G3D::Vector3(dir.x, dir.y, 0.0f)) > 0.0f)
            n = -n;

        // If the computed normal is mostly upward, don't treat it as a wall normal
        if (n.z > 0.8f)
            return;
    }

    // Project current velocity onto tangent plane defined by the normal (project-and-slide)
    float into = vel.dot(n);
    if (into < 0.0f)
    {
        G3D::Vector3 slide = vel - n * into; // remove into-normal component only
        if (slide.magnitude() < 0.05f)
        {
            state.vx = 0.0f;
            state.vy = 0.0f;
            PHYS_TRACE(PHYS_WALL, "wallStop");
        }
        else
        {
            state.vx = slide.x;
            state.vy = slide.y;
            PHYS_TRACE(PHYS_WALL, "wallSlide vx=" << state.vx << " vy=" << state.vy << " n=[" << n.x << "," << n.y << "," << n.z << "]");
        }
    }
}

// =====================================================================================
// Step entry point
// =====================================================================================
PhysicsOutput PhysicsEngine::Step(const PhysicsInput& input, float dt)
{
    ++gPhysFrameCounter;
    PhysicsOutput out{};
    if (!m_initialized)
    {
        out.x = input.x; out.y = input.y; out.z = input.z; out.orientation = input.orientation; out.pitch = input.pitch; out.vx = input.vx; out.vy = input.vy; out.vz = input.vz; out.moveFlags = input.moveFlags; return out;
    }

    float r = input.radius; float h = input.height;
    MovementState st{}; st.x = input.x; st.y = input.y; st.z = input.z; st.orientation = input.orientation; st.pitch = input.pitch; st.vx = input.vx; st.vy = input.vy; st.vz = input.vz; st.fallTime = input.fallTime; st.groundNormal = { 0, 0, 1 };

    MovementIntent intent = BuildMovementIntent(input, st.orientation);
    WalkableSurface surf = QueryGroundSurface(input.mapId, st.x, st.y, st.z, r, h);
    uint32_t liquidType = 0; float liquidLevel = QueryLiquidLevel(input.mapId, st.x, st.y, st.z, liquidType);
    ResolveGroundAttachment(st, surf, STEP_HEIGHT, STEP_DOWN_HEIGHT, dt);

    MovementMode mode = MovementMode::Air; bool inWater = false;
    if (liquidLevel > INVALID_HEIGHT) { float thresh = liquidLevel - h * 0.75f; inWater = st.z < thresh; }
    if (inWater && !st.isGrounded) mode = MovementMode::Swim; else if (st.isGrounded) mode = MovementMode::Ground;

    // Fallback: if no surface was detected but we have no vertical motion and have input,
    // treat as ground for this frame to allow step-up and wall-slide logic to engage.
    bool groundFallback = (!st.isGrounded && !inWater && st.vz == 0.0f && intent.hasInput);
    if (groundFallback) {
        mode = MovementMode::Ground;
        PHYS_DBG(PHYS_MOVE, "groundFallback engaged (no surf, vz=0, input)");
    }

    float baseSpeed = CalculateMoveSpeed(input, mode == MovementMode::Swim);
    PHYS_TRACE(PHYS_MOVE, "frame="<<gPhysFrameCounter<<" mode="<<(mode==MovementMode::Ground?"G":(mode==MovementMode::Swim?"S":"A"))<<" pos="<<st.x<<","<<st.y<<","<<st.z<<" vel="<<st.vx<<","<<st.vy<<","<<st.vz);

    switch (mode)
    {
    case MovementMode::Swim: ProcessSwimMovement(input, intent, st, dt, baseSpeed); break;
    case MovementMode::Ground: ProcessGroundMovementWithCylinder(input, intent, st, dt, baseSpeed, r, h); break;
    case MovementMode::Air: default: ProcessAirMovement(input, intent, st, dt, baseSpeed); break;
    }

    st.z = std::max(-MAX_HEIGHT, std::min(MAX_HEIGHT, st.z));

    out.x = st.x; out.y = st.y; out.z = st.z; out.orientation = st.orientation; out.pitch = st.pitch; out.vx = st.vx; out.vy = st.vy; out.vz = (mode == MovementMode::Ground || mode == MovementMode::Swim) ? 0.0f : st.vz; out.fallTime = (mode == MovementMode::Swim) ? 0.0f : st.fallTime; out.moveFlags = input.moveFlags;
    if (mode == MovementMode::Swim) out.moveFlags |= MOVEFLAG_SWIMMING; else out.moveFlags &= ~MOVEFLAG_SWIMMING;
    if (mode == MovementMode::Ground) { out.moveFlags &= ~MOVEFLAG_JUMPING; out.moveFlags &= ~MOVEFLAG_FALLINGFAR; }
    else if (mode == MovementMode::Air && st.vz < 0) { out.moveFlags |= MOVEFLAG_FALLINGFAR; }

    return out;
}