// PhysicsEngine.cpp - Simplified physics tuned toward vanilla 1.12.1 feel

#include "PhysicsEngine.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "MapLoader.h"
#include "Navigation.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "CylinderCollision.h" // for CylinderHelpers walkable config
#include "ModelInstance.h"     // for debug diagnostics on model collisions
#include "CapsuleCollision.h"  // added for debug distance computation

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
        // Removed noisy per-map initialized debug log
        // PHYS_DBG(PHYS_MOVE, "Map initialized id=" << mapId);
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

    // No horizontal movement -> nothing to sweep
    G3D::Vector3 moveDir(intent.dir.x, intent.dir.y, 0.0f);
    float intendedDist = std::sqrt(st.vx * st.vx + st.vy * st.vy) * dt;
    if (intendedDist <= 0.0f)
        return;

    // Build capsule in world space (base at feet)
    CapsuleCollision::Capsule cap;
    cap.p0 = CapsuleCollision::Vec3(st.x, st.y, st.z);
    cap.p1 = CapsuleCollision::Vec3(st.x, st.y, st.z + height);
    cap.r = radius;

    // If no vmap manager available, just advance
    if (!m_vmapManager)
    {
        st.x += moveDir.x * intendedDist;
        st.y += moveDir.y * intendedDist;
        return;
    }

    // Perform broad analytic sweep using SceneQuery wrapper
    std::vector<SceneHit> hits = m_vmapManager->SweepCapsuleAll(input.mapId, cap, moveDir, intendedDist);
    if (hits.empty())
    {
        // Nothing hit: advance fully
        st.x += moveDir.x * intendedDist;
        st.y += moveDir.y * intendedDist;
        return;
    }

    // SweepCapsuleAll returns earliest-cohort hits (or start-penetrating hits). Pick the first hit as representative
    const SceneHit& hit = hits.front();

    // If starting penetrating, zero horizontal motion and leave
    if (hit.startPenetrating)
    {
        st.vx = st.vy = 0.0f;
        PHYS_TRACE(PHYS_CYL, "Start-penetrating during advance map=" << input.mapId << " tri=" << hit.triIndex);
        return;
    }

    // Move up to contact point
    float travel = std::max(0.0f, hit.distance);
    st.x += moveDir.x * travel;
    st.y += moveDir.y * travel;

    // Evaluate surface normal
    st.groundNormal = hit.normal;
    // If normal is walkable, consider grounded for this frame
    if (hit.normal.z >= VMAP::CylinderHelpers::GetWalkableCosMin())
    {
        st.isGrounded = true;
        st.vx = st.vy = 0.0f; // stop horizontal motion when contacting walkable surface
        PHYS_TRACE(PHYS_STEP, "Advance hit walkable tri=" << hit.triIndex << " normalZ=" << hit.normal.z);
        return;
    }

    // Non-walkable hit (wall/steep). Try wall-slide first which may modify vx/vy
    AttemptWallSlide(input, intent, st, dt, speed, radius, height);

    // Try to advance with possibly modified velocities after slide
    float newX = st.x + st.vx * dt; float newY = st.y + st.vy * dt;
    if (ValidateCylinderPosition(input.mapId, newX, newY, st.z + 0.01f, 0.02f, radius, height))
    {
        st.x = newX; st.y = newY; return;
    }

    // If blocked, attempt a step-up placement using contact info (best-effort)
    {
        float placeX = st.x, placeY = st.y, placeZ = st.z;
        if (TryFindStepUpPlacement(m_vmapManager, input.mapId, st.x, st.y, st.z, hit.point, hit.normal, intent.dir, radius, height, placeX, placeY, placeZ))
        {
            Cylinder fit = CreatePlayerCylinder(placeX, placeY, placeZ, radius, height);
            bool headOK = HasHeadClearance(input.mapId, placeX, placeY, placeZ, radius, height);
            if (m_vmapManager->CanCylinderFitAtPosition(input.mapId, fit, 0.02f) && headOK)
            {
                float finalZ = RaycastDownFrom(m_vmapManager, input.mapId, placeX, placeY, placeZ);
                st.x = placeX; st.y = placeY; st.z = finalZ; st.groundNormal = hit.normal; st.vx = st.vy = 0.0f; st.isGrounded = (hit.normal.z >= VMAP::CylinderHelpers::GetWalkableCosMin());
                PHYS_INFO(PHYS_STEP, "[STEP] placed at (" << placeX << "," << placeY << ") rayZ=" << finalZ << " tri=" << hit.triIndex);
                return;
            }
        }
    }

    // As a last resort, zero horizontal velocity and leave in place
    st.vx = st.vy = 0.0f;
    PHYS_TRACE(PHYS_MOVE, "Advance blocked after sweep; zeroing velocity");
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
    /*WalkableSurface ground = FindWalkableSurfaceWithCylinder(input.mapId, st.x, st.y, st.z,
        STEP_HEIGHT, DEFAULT_HEIGHT_SEARCH, input.radius, input.height);
    if (st.vz <= 0 && ground.found) { float diff = ground.height - st.z; if (diff >= -STEP_DOWN_HEIGHT - GROUND_HEIGHT_TOLERANCE && diff <= GROUND_HEIGHT_TOLERANCE) { st.z = ground.height; st.vz = 0; st.isGrounded = true; st.fallTime = 0; st.groundNormal = ground.normal; PHYS_TRACE(PHYS_MOVE, "land diff="<<diff); } }*/
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
    PHYS_TRACE(PHYS_MOVE, "[Step] frame="<<gPhysFrameCounter
        <<" map="<<input.mapId
        <<" pos="<<input.x<<","<<input.y<<","<<input.z
        <<" vel="<<input.vx<<","<<input.vy<<","<<input.vz
        <<" dt="<<dt);

    // Ensure all walkable surface queries in this step use the configured slope threshold.
    VMAP::CylinderHelpers::WalkableCosScope walkableScope(m_walkableCosMin);

    PhysicsOutput out{};
    if (!m_initialized)
    {
        out.x = input.x; out.y = input.y; out.z = input.z; out.orientation = input.orientation; out.pitch = input.pitch; out.vx = input.vx; out.vy = input.vy; out.vz = input.vz; out.moveFlags = input.moveFlags; return out;
    }

    float r = input.radius; float h = input.height;
    MovementState st{}; st.x = input.x; st.y = input.y; st.z = input.z; st.orientation = input.orientation; st.pitch = input.pitch; st.vx = input.vx; st.vy = input.vy; st.vz = input.vz; st.fallTime = input.fallTime; st.groundNormal = { 0, 0, 1 };

    MovementIntent intent = BuildMovementIntent(input, st.orientation);

    WalkableSurface surf{ false, INVALID_HEIGHT, SurfaceSource::NONE, {0,0,1} };
    if (m_vmapManager)
    {
        EnsureMapLoaded(input.mapId);
        Cylinder curCyl = CreatePlayerCylinder(st.x, st.y, st.z, r, h);

        // First try: use configured downward sweep to gather walkable hits, then pick best
        {
            float sweepDist = std::max(0.25f, STEP_HEIGHT + STEP_DOWN_HEIGHT);
            CapsuleCollision::Capsule cap;
            // Bottom sphere at st.z (feet) and top sphere at st.z + height (top of cylinder)
            cap.p0 = CapsuleCollision::Vec3(st.x, st.y, st.z);
            cap.p1 = CapsuleCollision::Vec3(st.x, st.y, st.z + h);
            cap.r = r;

            auto hits = m_vmapManager->SweepCapsuleAll(input.mapId, cap, G3D::Vector3(0,0,-1), sweepDist);

            // Diagnostic raycast: from top-sphere center straight down through the capsule
            {
                G3D::Vector3 topCenter(st.x, st.y, st.z + h);
                SceneHit topHit;
                float topRayDist = r + h; // see straight down through capsule
                G3D::Vector3 rayDirW(0,0,-1);
                G3D::Vector3 iOrigin = NavCoord::WorldToInternal(topCenter);
                G3D::Vector3 iDir = NavCoord::WorldDirToInternal(rayDirW);
                bool topHitAny = m_vmapManager->RaycastSingle(input.mapId, topCenter, rayDirW, topRayDist, topHit);
                if (topHitAny)
                {
                    G3D::Vector3 wp = topHit.point;
                    G3D::Vector3 wn = topHit.normal;
                    G3D::Vector3 iP = NavCoord::WorldToInternal(wp);
                    G3D::Vector3 iN = NavCoord::WorldDirToInternal(wn);
                    PHYS_TRACE(PHYS_SURF, "[TopRay] hit=1 dist=" << topHit.distance << " time=" << topHit.time
                        << " originW=(" << topCenter.x << "," << topCenter.y << "," << topCenter.z << ")"
                        << " originI=(" << iOrigin.x << "," << iOrigin.y << "," << iOrigin.z << ")"
                        << " dirW=(" << rayDirW.x << "," << rayDirW.y << "," << rayDirW.z << ")"
                        << " dirI=(" << iDir.x << "," << iDir.y << "," << iDir.z << ")"
                        << " pointW=(" << wp.x << "," << wp.y << "," << wp.z << ")"
                        << " normalW=(" << wn.x << "," << wn.y << "," << wn.z << ")"
                        << " pointI=(" << iP.x << "," << iP.y << "," << iP.z << ")"
                        << " normalI=(" << iN.x << "," << iN.y << "," << iN.z << ")"
                        << " inst=" << topHit.instanceId << " tri=" << topHit.triIndex);

                    // Extra diagnostics: dump local surface patch around the TopRay hit for triangle centroid/verts and instance ids
                    if (m_vmapManager)
                    {
                        PHYS_TRACE(PHYS_SURF, "[TopRay][DumpSurfacePatch] dumping nearby triangles around hit point (world) = (" << wp.x << "," << wp.y << "," << wp.z << ")");
                        // Patch half extents in world coords: XY and Z, and cap logging to a reasonable number
                        const float patchHalfXY = 0.6f; // 60 cm radius
                        const float patchHalfZ  = 0.3f; // 30 cm vertical
                        const int maxTrianglesToLog = 24;
                        // DumpSurfacePatch logs triangle verts and instance info via PHYS_TRACE internally
                        m_vmapManager->DumpSurfacePatch(input.mapId, wp.x, wp.y, wp.z, patchHalfXY, patchHalfZ, maxTrianglesToLog);

                        // Also run a tiny cylinder collision probe centered at the hit point to see if discrete collision reports it
                        {
                            float probeRadius = 0.02f; // 2 cm probe
                            float probeHeight = 0.02f;
                            Cylinder probeCyl(G3D::Vector3(wp.x, wp.y, wp.z), G3D::Vector3(0,0,1), probeRadius, probeHeight);
                            float outContactH = 0.0f; G3D::Vector3 outContactN(0,0,1); ModelInstance* hitInst = nullptr;
                            bool probeHit = m_vmapManager->CheckCylinderCollision(input.mapId, probeCyl, outContactH, outContactN, &hitInst);
                            PHYS_TRACE(PHYS_SURF, "[TopRay][DumpSurfacePatch] CheckCylinderCollision probeHit=" << (probeHit?1:0)
                                << " inst=" << (hitInst?hitInst->ID:0) << " contactH=" << outContactH
                                << " contactN=(" << outContactN.x << "," << outContactN.y << "," << outContactN.z << ")");
                        }
                    }

                    // Debug: compute distance from the ray hit point to the capsule segment used by the sweep
                    {
                        // Recreate the world-space capsule endpoints used by SweepCapsuleAll (including inflation/nudge)
                        G3D::Vector3 capW_p0(st.x, st.y, st.z);
                        G3D::Vector3 capW_p1(st.x, st.y, st.z + h);
                        G3D::Vector3 sweepDir = G3D::Vector3(0,0,-1);
                        // QueryParams default inflation used by SweepCapsuleAll is 0.02f
                        const float dbgInflation = 0.02f;
                        G3D::Vector3 dirN = sweepDir; // already unit
                        G3D::Vector3 adjust = dirN * dbgInflation;
                        G3D::Vector3 wP0_adj = capW_p0 + adjust;
                        G3D::Vector3 wP1_adj = capW_p1 + adjust;

                        // Convert to internal space the same way SweepCapsuleAll does
                        G3D::Vector3 iP0_adj = NavCoord::WorldToInternal(wP0_adj);
                        G3D::Vector3 iP1_adj = NavCoord::WorldToInternal(wP1_adj);

                        CapsuleCollision::Vec3 c0p0 = { iP0_adj.x, iP0_adj.y, iP0_adj.z };
                        CapsuleCollision::Vec3 c0p1 = { iP1_adj.x, iP1_adj.y, iP1_adj.z };

                        // Hit point in internal space
                        G3D::Vector3 hitI = NavCoord::WorldToInternal(wp);
                        CapsuleCollision::Vec3 hitInternal = { hitI.x, hitI.y, hitI.z };

                        float tOnSeg = 0.0f;
                        CapsuleCollision::Vec3 segClosest = CapsuleCollision::closestPointOnSegment(c0p0, c0p1, hitInternal, &tOnSeg);
                        CapsuleCollision::Vec3 diff = { hitInternal.x - segClosest.x, hitInternal.y - segClosest.y, hitInternal.z - segClosest.z };
                        float distI = std::sqrt(diff.x*diff.x + diff.y*diff.y + diff.z*diff.z);
                        float radiusUsed = r; // capsule radius used in sweep (no speculative inflation here)
                        bool within = (distI <= radiusUsed + 1e-4f);
                        PHYS_TRACE(PHYS_SURF, "[TopRay][DebugDist] hit_to_capsuleSeg distI=" << distI << " withinRadius=" << (within?1:0)
                            << " tOnSeg=" << tOnSeg
                            << " segClosestI=(" << segClosest.x << "," << segClosest.y << "," << segClosest.z << ")"
                            << " capP0I=(" << c0p0.x << "," << c0p0.y << "," << c0p0.z << ") capP1I=(" << c0p1.x << "," << c0p1.y << "," << c0p1.z << ")"
                            << " hitI=(" << hitInternal.x << "," << hitInternal.y << "," << hitInternal.z << ")");
                    }
                }
                else
                {
                    G3D::Vector3 iOrigin = NavCoord::WorldToInternal(topCenter);
                    G3D::Vector3 iDir = NavCoord::WorldDirToInternal(G3D::Vector3(0,0,-1));
                    PHYS_TRACE(PHYS_SURF, "[TopRay] hit=0 maxDist=" << topRayDist
                        << " originW=(" << topCenter.x << "," << topCenter.y << "," << topCenter.z << ")"
                        << " originI=(" << iOrigin.x << "," << iOrigin.y << "," << iOrigin.z << ")"
                        << " dirW=(0,0,-1) dirI=(" << iDir.x << "," << iDir.y << "," << iDir.z << ")");
                }
            }

            // Diagnostic: log all SceneHit candidates returned by SweepCapsuleAll
            PHYS_TRACE(PHYS_SURF, "[SweepCapsuleAll->SweepForWalkableSurfaces] SceneHit candidates count=" << hits.size());
            for (size_t hi = 0; hi < hits.size(); ++hi)
            {
                const SceneHit& sh = hits[hi];
                G3D::Vector3 wP = sh.point;
                G3D::Vector3 wN = sh.normal;
                G3D::Vector3 iP = NavCoord::WorldToInternal(wP);
                G3D::Vector3 iN = NavCoord::WorldDirToInternal(wN);
                PHYS_TRACE(PHYS_SURF, "  hit[" << hi << "] instId=" << sh.instanceId << " triIndex=" << sh.triIndex
                    << " startPen=" << (sh.startPenetrating?1:0)
                    << " time=" << sh.time << " dist=" << sh.distance
                    << " pointW=(" << wP.x << "," << wP.y << "," << wP.z << ")"
                    << " normalW=(" << wN.x << "," << wN.y << "," << wN.z << ")"
                    << " pointI=(" << iP.x << "," << iP.y << "," << iP.z << ")"
                    << " normalI=(" << iN.x << "," << iN.y << "," << iN.z << ")");
            }
        }
    }

    // Continue with the rest of the function logic...
    return out;
}