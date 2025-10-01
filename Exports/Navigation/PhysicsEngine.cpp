// PhysicsEngine.cpp - Simplified physics tuned toward vanilla 1.12.1 feel

#include "PhysicsEngine.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "MapLoader.h"
#include "Navigation.h"
#include "VMapLog.h"

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
      m_initialized(false)
{
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
// Initialization / Shutdown
// =====================================================================================
void PhysicsEngine::Initialize()
{
    if (m_initialized)
        return;

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
    return (len < 0.0001f) ? G3D::Vector3(0, 0, 1) : n / len;
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
        if (fit) return true;
    }

    // Phase 1
    float headBase = newZ + height * headStartFrac;
    float headHeight = height - height * headStartFrac;
    if (headHeight > 0.05f)
    {
        Cylinder cylHead = CreatePlayerCylinder(x, y, headBase, radius * 0.995f, headHeight - 0.01f);
        bool headFit = m_vmapManager->CanCylinderFitAtPosition(mapId, cylHead, inflateTol);
        PHYS_TRACE(PHYS_HEAD, "PH1 headBase=" << headBase << " h=" << headHeight << " fit=" << (headFit?1:0));
        if (headFit) { PHYS_INFO(PHYS_HEAD, "HEAD-ONLY success map=" << mapId); return true; }
    }

    // Phase 2 slices
    bool anyUpperSlice = false;
    float segmentTop   = newZ + height;
    float sliceFloor   = newZ + std::max(baseAllowance, height * headStartFrac);
    for (float zTop = segmentTop; zTop - minSliceH > sliceFloor; zTop -= minSliceH * 0.75f)
    {
        float sliceHeight = std::min(minSliceH, zTop - sliceFloor);
        float sliceBase   = zTop - sliceHeight;
        Cylinder sliceCyl = CreatePlayerCylinder(x, y, sliceBase, radius * 0.99f, sliceHeight - 0.01f);
        bool fit = m_vmapManager->CanCylinderFitAtPosition(mapId, sliceCyl, inflateTol);
        PHYS_TRACE(PHYS_HEAD, "PH2 slice base=" << sliceBase << " h=" << sliceHeight << " fit=" << (fit?1:0));
        if (fit) { anyUpperSlice = true; break; }
    }
    if (anyUpperSlice) { PHYS_INFO(PHYS_HEAD, "SLICES success map=" << mapId); return true; }

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

    float tH = GetTerrainHeight(mapId, x, y);
    if (tH > INVALID_HEIGHT)
    {
        float diff = tH - currentZ;
        if (diff >= -(maxStepDown + GROUND_HEIGHT_TOLERANCE) && diff <= maxStepUp + GROUND_HEIGHT_TOLERANCE)
            baseCandidates.push_back({ tH, SurfaceSource::TERRAIN, ComputeTerrainNormal(mapId, x, y) });
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
                if (n.z >= WALKABLE_MIN_NORMAL_Z)
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
            if (n2.z < WALKABLE_MIN_NORMAL_Z) continue;
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
        PHYS_TRACE(PHYS_SURF, "surface h=" << best.height << " src=" << (best.source==SurfaceSource::TERRAIN?"terrain":"vmap") << " nZ=" << best.normal.z << " dt_us=" << us);
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

    auto tryAdvance = [&](float& tx, float& ty) -> bool
    {
        if (ValidateCylinderPosition(input.mapId, tx, ty, st.z + 0.01f, 0.02f, radius, height)) return true;
        PHYS_TRACE(PHYS_MOVE, "blocked initial advance");
        AttemptWallSlide(input, intent, st, dt, speed, radius, height);
        tx = st.x + st.vx * dt; ty = st.y + st.vy * dt;
        if (ValidateCylinderPosition(input.mapId, tx, ty, st.z + 0.01f, 0.02f, radius, height)) return true;
        G3D::Vector3 mv(st.vx * dt, st.vy * dt, 0); float len = mv.magnitude();
        if (len > 0.0001f)
        {
            G3D::Vector3 step = mv * (-1.0f / std::max(4.0f, len / 0.1f)); float pX = tx; float pY = ty;
            for (int i=0;i<8;++i){ pX += step.x; pY += step.y; if (ValidateCylinderPosition(input.mapId, pX, pY, st.z + 0.01f, 0.02f, radius, height)) { tx=pX; ty=pY; PHYS_TRACE(PHYS_MOVE, "backoff success i="<<i); return true; } }
        }
        st.vx = st.vy = 0; PHYS_TRACE(PHYS_MOVE, "advance failed zero velocity"); return false; };

    if (!tryAdvance(newX, newY)) return;

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
            if (diff < -GROUND_HEIGHT_TOLERANCE)
            {
                if (!HasHeadClearance(input.mapId, newX, newY, surface.height, radius, height)) { st.x = newX; st.y = newY; PHYS_TRACE(PHYS_HEAD, "headBlocked downSnap diff="<<diff); return; }
            }
            Cylinder fit = CreatePlayerCylinder(newX, newY, surface.height, radius, height);
            if (!m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f)) {
                if (diff < -GROUND_HEIGHT_TOLERANCE) { st.x = newX; st.y = newY; PHYS_TRACE(PHYS_CYL, "downMoveKeepZ fitReject surfaceHeight="<<surface.height); return; }
                if (diff > GROUND_HEIGHT_TOLERANCE) { // upward small step fallback
                    float probeRefZ = surface.height + 0.05f;
                    float supportH = m_vmapManager ? m_vmapManager->GetCylinderHeight(input.mapId, newX, newY, probeRefZ, radius, height, 1.0f) : INVALID_HEIGHT;
                    if (supportH > INVALID_HEIGHT && std::fabs(supportH - surface.height) <= 0.35f) {
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
    if (!intent.hasInput || !m_vmapManager) return; if (state.vx == 0.0f && state.vy == 0.0f) return;
    G3D::Vector3 move(state.vx, state.vy, 0); float moveLen = move.magnitude(); if (moveLen < 0.0001f) return;
    G3D::Vector3 dir = move / moveLen; float probeDist = std::min(0.5f, moveLen * dt + radius * 0.5f); G3D::Vector3 right(-dir.y, dir.x, 0); float lateral = radius * 0.6f;
    auto sampleHeight = [&](const G3D::Vector3& offset) -> float { return m_vmapManager->GetCylinderHeight(input.mapId, state.x + offset.x, state.y + offset.y, state.z + STEP_HEIGHT * 0.5f, radius, height, 4.0f); };
    float forwardH = m_vmapManager->GetCylinderHeight(input.mapId, state.x + dir.x * probeDist, state.y + dir.y * probeDist, state.z + STEP_HEIGHT * 0.5f, radius, height, 4.0f); if (forwardH <= INVALID_HEIGHT) return;
    float leftH = sampleHeight(dir * probeDist + right * -lateral); float rightHval = sampleHeight(dir * probeDist + right * lateral); float dH = rightHval - leftH;
    G3D::Vector3 approxNormal(right.x * dH, right.y * dH, lateral * 2.0f); if (approxNormal.magnitude() < 0.0001f) approxNormal = G3D::Vector3(-dir.x, -dir.y, 0); else approxNormal = approxNormal / approxNormal.magnitude();
    G3D::Vector3 vel(state.vx, state.vy, 0); float into = vel.dot(approxNormal);
    if (into < 0.0f) { G3D::Vector3 slide = vel - approxNormal * into; if (slide.magnitude() < 0.05f) { state.vx = state.vy = 0.0f; PHYS_TRACE(PHYS_WALL, "wallStop"); } else { state.vx = slide.x; state.vy = slide.y; PHYS_TRACE(PHYS_WALL, "wallSlide newVx="<<state.vx<<" newVy="<<state.vy); } }
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