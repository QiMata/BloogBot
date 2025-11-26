// PhysicsEngine.cpp - Simplified physics tuned toward vanilla 1.12.1 feel

#include "PhysicsEngine.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "MapLoader.h"
#include "Navigation.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "ModelInstance.h"     // for debug diagnostics on model collisions
#include "CapsuleCollision.h"  // added for debug distance computation
#include "PhysicsBridge.h"     // ensure movement flag constants (added for swimming flag update)

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
	case PHYS_MOVE: return "MOVE"; case PHYS_SURF: return "SURF"; case PHYS_HEAD: return "HEAD"; case PHYS_CYL: return "CYL"; case PHYS_STEP: return "STEP"; case PHYS_WALL: return "WALL"; case PHYS_PERF: return "PERF"; default: return "?";
	}
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
	m_walkableCosMin(DEFAULT_WALKABLE_MIN_NORMAL_Z) {
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
// Movement helpers
// =====================================================================================
PhysicsEngine::MovementIntent PhysicsEngine::BuildMovementIntent(const PhysicsInput& input, float orientation) const
{
	MovementIntent intent{}; float c = std::cos(orientation); float s = std::sin(orientation);
	float dirX = 0.0f, dirY = 0.0f;
	if (input.moveFlags & MOVEFLAG_FORWARD) { dirX += c;  dirY += s; }
	if (input.moveFlags & MOVEFLAG_BACKWARD) { dirX -= c;  dirY -= s; }
	if (input.moveFlags & MOVEFLAG_STRAFE_LEFT) { dirX += s;  dirY -= c; }
	if (input.moveFlags & MOVEFLAG_STRAFE_RIGHT) { dirX -= s;  dirY += c; }
	float mag = std::sqrt(dirX * dirX + dirY * dirY);
	if (mag > 0.0001f) { dirX /= mag; dirY /= mag; intent.hasInput = true; }
	intent.dir = G3D::Vector3(dirX, dirY, 0.0f);
	intent.jumpRequested = (input.moveFlags & MOVEFLAG_JUMPING) != 0;
	return intent;
}

float PhysicsEngine::QueryLiquidLevel(uint32_t mapId, float x, float y, float z, uint32_t& liquidType) const
{
	return const_cast<PhysicsEngine*>(this)->GetLiquidHeight(mapId, x, y, z, liquidType);
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

// =====================================================================================
// Ground movement with slope and step fallbacks
// =====================================================================================
void PhysicsEngine::ProcessGroundMovement(const PhysicsInput& input, const MovementIntent& intent,
	MovementState& st, float dt, float speed,
	float radius, float height)
{
	PHYS_INFO(PHYS_MOVE, "[GroundMove] Start pos=" << st.x << "," << st.y << "," << st.z << " vel=" << st.vx << "," << st.vy << " dt=" << dt);
	if (intent.jumpRequested) {
		st.vz = JUMP_VELOCITY;
		st.isGrounded = false;
		st.fallTime = 0;
		PHYS_INFO(PHYS_MOVE, "jump vz=" << st.vz);
		return;
	}
	if (intent.hasInput) {
		st.vx = intent.dir.x * speed;
		st.vy = intent.dir.y * speed;
		PHYS_INFO(PHYS_MOVE, "Intent input vx=" << st.vx << " vy=" << st.vy);
	}
	else {
		st.vx = st.vy = 0;
		PHYS_INFO(PHYS_MOVE, "No input, vx/vy zeroed");
		return;
	}
	G3D::Vector3 moveDir(intent.dir.x, intent.dir.y, 0.0f);
	float intendedDist = std::sqrt(st.vx * st.vx + st.vy * st.vy) * dt;
	PHYS_INFO(PHYS_MOVE, "intendedDist=" << intendedDist);
	if (intendedDist <= 0.0f)
		return;
	CapsuleCollision::Capsule cap;
	float capBottom = st.z + radius;
	// ORIGINAL full-height sweep: capTop = st.z + height - radius;
	// We only need to sweep near the feet to acquire the walkable triangle we are moving over.
	// Limit vertical segment portion of the capsule to step height (plus small safety) so we still catch
	// potential step-up geometry but ignore higher obstructions above the character waist.
	float fullSegLen = (height - 2.0f * radius); // original central segment length
	float sweepSegmentHeight = std::max(0.1f, std::min(fullSegLen, STEP_HEIGHT + 0.25f)); // clamp; keep >0
	float capTop = capBottom + sweepSegmentHeight; // reduced top
	cap.p0 = CapsuleCollision::Vec3(st.x, st.y, capBottom);
	cap.p1 = CapsuleCollision::Vec3(st.x, st.y, capTop);
	cap.r = radius;
	std::vector<SceneHit> hits;
	if (m_vmapManager)
		hits = m_vmapManager->SweepCapsuleAll(input.mapId, cap, moveDir, intendedDist);

	// Removed temporary override: now process sweep hits for stepping / collision response
	bool ignoreSweep = false;

	// Common step limits (up/down) for both sweep and fallback height adjustment
	float stepUpLimit = STEP_HEIGHT;
	float stepDownLimit = STEP_DOWN_HEIGHT;
	float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
	// (future) walkableCosMin could be configurable; already accessible via GetWalkableCosMin()

	if (!hits.empty() && !ignoreSweep) {
		 // First: if we are already overlapping (startPenetrating) a walkable surface, slide instead of stepping.
        const SceneHit& firstHit = hits.front();
        float nZMag = std::fabs(firstHit.normal.z);
        bool walkableStartPen = firstHit.startPenetrating && nZMag >= walkableCosMin; // allow either orientation
        if (walkableStartPen) {
            // Use upward-oriented normal for plane math
            G3D::Vector3 n = (firstHit.normal.z < 0.0f ? -firstHit.normal : firstHit.normal).directionOrZero();
            if (n.magnitude() < 1e-5f) n = G3D::Vector3(0,0,1);
            G3D::Vector3 tangent = (moveDir - n * moveDir.dot(n));
            if (tangent.magnitude() < 1e-5f) {
                tangent = G3D::Vector3(-n.y, n.x, 0.0f);
            }
            G3D::Vector3 slideDir = tangent.directionOrZero();
            float slideDist = intendedDist;
            float newX = st.x + slideDir.x * slideDist;
            float newY = st.y + slideDir.y * slideDist;
            float refZPoint = firstHit.point.z;
            float footBottom = st.z + radius;
            bool pointValid = true;
            if ((footBottom - refZPoint) > (stepDownLimit + 1.0f)) {
                pointValid = false;
            }
            G3D::Vector3 planePoint(pointValid ? firstHit.point.x : st.x,
                                     pointValid ? firstHit.point.y : st.y,
                                     pointValid ? refZPoint : footBottom);
            float D = -n.dot(planePoint);
            float newZ = st.z;
            if (std::fabs(n.z) > 1e-5f) {
                newZ = (-D - n.x * newX - n.y * newY) / n.z;
            }
            float dzSlide = newZ - st.z;
            if (dzSlide > stepUpLimit) newZ = st.z + stepUpLimit; else if (dzSlide < -stepDownLimit) newZ = st.z - stepDownLimit;
            st.x = newX; st.y = newY; st.z = newZ; st.isGrounded = true; st.groundNormal = n; st.vx = st.vy = 0.0f;
            PHYS_INFO(PHYS_MOVE, "[GroundMove] Sliding along walkable surface startPen dist=" << slideDist
                << " slideDir=" << slideDir.x << "," << slideDir.y << "," << slideDir.z
                << " newPos=" << st.x << "," << st.y << "," << st.z
                << (pointValid ? " planePointValid" : " planePointFallback")
                << " dzSlide=" << dzSlide);
            return;
        }
        // Find earliest walkable hit we can step onto (exclude startPenetrating or zero-distance hits)
        const SceneHit* chosenWalkable = nullptr;
        for (const auto& h : hits) {
            if (h.startPenetrating) continue;
            if (h.distance <= 1e-4f) continue;
            float dz = h.point.z - st.z;
            float hNZMag = std::fabs(h.normal.z);
            if (hNZMag >= walkableCosMin && dz >= 0.0f && dz <= stepUpLimit) { chosenWalkable = &h; break; }
        }
        if (chosenWalkable) {
            // Build a temporary support ramp plane from previous position (st.x,st.y,st.z) to chosenWalkable->point
			G3D::Vector3 oldPos(st.x, st.y, st.z);
			float travel = std::max(0.0f, chosenWalkable->distance);
			G3D::Vector3 moveDirN = moveDir.directionOrZero();
			if (moveDirN.magnitude() < 1e-5f) moveDirN = G3D::Vector3(1,0,0);
			// Advance horizontally first (no vertical snap yet)
			G3D::Vector3 horizAdvance = moveDirN * travel;
			G3D::Vector3 steppedPoint(chosenWalkable->point.x, chosenWalkable->point.y, chosenWalkable->point.z);
			G3D::Vector3 newPos = oldPos + horizAdvance; // provisional horizontal target
			// Side vector to form plane basis (ensure non-collinear with vertical)
			G3D::Vector3 side = moveDirN.cross(G3D::Vector3(0,0,1));
			if (side.magnitude() < 1e-5f) side = G3D::Vector3(0,1,0);
			side = side.directionOrZero();
			// Plane through oldPos and steppedPoint using side (oldPos + side)
			G3D::Vector3 p2 = oldPos + side * 0.5f; // arbitrary non-collinear point
			G3D::Vector3 rampN = (steppedPoint - oldPos).cross(p2 - oldPos).directionOrZero();
			if (rampN.magnitude() < 1e-5f) rampN = chosenWalkable->normal; // fallback to hit normal
			if (rampN.z < 0.0f) rampN = -rampN; // ensure upward
			float rampD = -rampN.dot(oldPos);
			// Interpolate vertical using ramp plane at new horizontal position
			float interpZ = (-rampD - rampN.x * newPos.x - rampN.y * newPos.y) / (rampN.z != 0.0f ? rampN.z : 1.0f);
			// Clamp interpolation between oldZ and stepped Z to avoid overshoot on steep normals
			float targetZ = steppedPoint.z;
			if ((interpZ > oldPos.z && interpZ < targetZ) || std::fabs(interpZ - targetZ) < 0.01f)
				st.z = interpZ;
			else
				st.z = targetZ;
			st.x = newPos.x;
			st.y = newPos.y;
			st.groundNormal = rampN; // use ramp normal during transition
			st.isGrounded = true;
			st.vx = st.vy = 0.0f; // stop horizontal velocity after step
			// Store ramp data for continued interpolation in subsequent steps until fully reached target
			st.rampActive = true;
			st.rampN = rampN;
			st.rampD = rampD;
			st.rampStart = oldPos;
			st.rampEnd = steppedPoint;
			st.rampDir = moveDirN;
			st.rampLength = (steppedPoint - oldPos).dot(moveDirN);
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Stepped up via capsule sweep travel=" << travel << " newZ=" << st.z << " hitTri=" << chosenWalkable->triIndex << " rampActive=1 rampLength=" << st.rampLength << " rampN=" << rampN.x << "," << rampN.y << "," << rampN.z << " sweepSegH=" << sweepSegmentHeight << "/" << fullSegLen);
			return;
        }
        // If no walkable step candidate, treat first hit as obstruction (horizontal movement stops before wall)
        const SceneHit& hit = hits.front();
        float travel = std::max(0.0f, hit.distance);
        st.x += moveDir.x * travel;
        st.y += moveDir.y * travel;
        st.groundNormal = hit.normal;
        if (hit.normal.z >= walkableCosMin) {
            float dz = hit.point.z - st.z;
            if ((dz >= 0.0f && dz <= stepUpLimit) || (dz < 0.0f && -dz <= stepDownLimit)) {
                st.z = hit.point.z;
                st.isGrounded = true;
            }
            st.vx = st.vy = 0.0f;
            PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep: grounded travel=" << travel << " newZ=" << st.z << " sweepSegH=" << sweepSegmentHeight);
        }
        else {
            st.vx = st.vy = 0.0f; // stop due to wall impact
            PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep: non-walkable obstruction, horizontal velocity zeroed travel=" << travel << " sweepSegH=" << sweepSegmentHeight);
        }
        return;
	}
	else {
		if (!hits.empty()) {
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep ignored flag set or empty processing path (should be false) hits=" << hits.size() << " sweepSegH=" << sweepSegmentHeight);
		}
		st.x += moveDir.x * intendedDist;
		st.y += moveDir.y * intendedDist;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep: no collision, moved full distance sweepSegH=" << sweepSegmentHeight);
		// Query both VMAP and ADT terrain heights
		float vmapZ = INVALID_HEIGHT;
		if (m_vmapManager) {
			// vmapZ = m_vmapManager->getHeight(input.mapId, st.x, st.y, st.z + height, stepUpLimit + stepDownLimit + height); // disabled: no longer needed during update
			vmapZ = INVALID_HEIGHT;
		}
		float adtZ = GetTerrainHeight(input.mapId, st.x, st.y);
		// Log VMap direct height but do not use it to set final Z (diagnostic only)
		if (vmapZ > INVALID_HEIGHT)
		{
			PHYS_INFO(PHYS_MOVE, std::string("[GroundMove] VMAP height probed (diagnostic only): z=") << vmapZ);
		}
		float bestZ = st.z;
		bool found = false;
		// Only use ADT height for fallback snapping
		if (adtZ > INVALID_HEIGHT) {
			float diff = adtZ - st.z;
			if (((diff >= 0.0f && diff <= stepUpLimit) || (diff < 0.0f && diff >= -stepDownLimit))) {
				bestZ = adtZ;
				found = true;
				PHYS_INFO(PHYS_MOVE, "[GroundMove] ADT height accepted: z=" << adtZ);
			}
		}
		if (found) {
			st.z = bestZ;
			st.groundNormal = G3D::Vector3(0, 0, 1);
			st.isGrounded = true;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Final ground z set to " << st.z);
		}
	}
}

// =====================================================================================
// Air movement
// =====================================================================================
void PhysicsEngine::ProcessAirMovement(const PhysicsInput& input, const MovementIntent& intent,
	MovementState& st, float dt, float speed)
{
	// Handles air movement: gravity, air control
	st.fallTime += dt;
	ApplyGravity(st, dt);
	float curX = st.vx, curY = st.vy;
	float tgtX = curX, tgtY = curY;
	if (intent.hasInput) {
		tgtX = intent.dir.x * speed;
		tgtY = intent.dir.y * speed;
	}
	float dX = tgtX - curX, dY = tgtY - curY;
	float len = std::sqrt(dX * dX + dY * dY);
	if (len > 0.0001f) {
		float maxChange = AIR_ACCEL * dt;
		if (len > maxChange) {
			float scale = maxChange / len;
			dX *= scale;
			dY *= scale;
		}
		curX += dX;
		curY += dY;
	}
	st.vx = curX;
	st.vy = curY;
	st.x += st.vx * dt;
	st.y += st.vy * dt;
	st.z += st.vz * dt;
}

// =====================================================================================
// Swim movement
// =====================================================================================
void PhysicsEngine::ProcessSwimMovement(const PhysicsInput& input, const MovementIntent& intent,
	MovementState& st, float dt, float speed)
{
	// Handles swim movement: horizontal and vertical (pitch) control
	if (intent.hasInput) {
		st.vx = intent.dir.x * speed;
		st.vy = intent.dir.y * speed;
	}
	else {
		st.vx = st.vy = 0;
	}
	float desiredVz = 0.0f;
	// Only apply vertical movement if moving forward
	if (intent.hasInput && (input.moveFlags & MOVEFLAG_FORWARD))
		desiredVz = std::sin(st.pitch) * speed;
	st.vz = desiredVz;
	st.x += st.vx * dt;
	st.y += st.vy * dt;
	st.z += st.vz * dt;
}

// =====================================================================================
// Step entry point
// =====================================================================================
PhysicsOutput PhysicsEngine::Step(const PhysicsInput& input, float dt)
{
	++gPhysFrameCounter;
	PHYS_TRACE(PHYS_MOVE, "[Step] frame=" << gPhysFrameCounter
		<< " map=" << input.mapId
		<< " pos=" << input.x << "," << input.y << "," << input.z
		<< " vel=" << input.vx << "," << input.vy << "," << input.vz
		<< " dt=" << dt);

	PhysicsOutput out{};
	if (!m_initialized)
	{
		out.x = input.x; out.y = input.y; out.z = input.z; out.orientation = input.orientation; out.pitch = input.pitch; out.vx = input.vx; out.vy = input.vy; out.vz = input.vz; out.moveFlags = input.moveFlags; return out;
	}

	float r = input.radius;
	float h = input.height;

	// 1. Build movement intent
	MovementState st{};
	st.x = input.x;
	st.y = input.y;
	st.z = input.z;
	st.orientation = input.orientation;
	st.pitch = input.pitch;
	st.vx = input.vx;
	st.vy = input.vy;
	st.vz = input.vz;
	st.fallTime = input.fallTime;
	st.groundNormal = { 0, 0, 1 };
	MovementIntent intent = BuildMovementIntent(input, st.orientation);

	// 2. Query surface and liquid state
	uint32_t liquidType = 0;
	// Capture raw ADT and VMAP liquid levels for diagnostics before merged query
	float adtLiquidLevel = INVALID_HEIGHT; uint32_t adtLiquidType = 0;
	if (m_mapLoader && m_mapLoader->IsInitialized()) {
		adtLiquidLevel = m_mapLoader->GetLiquidLevel(input.mapId, st.x, st.y);
		if (adtLiquidLevel > INVALID_HEIGHT)
			adtLiquidType = m_mapLoader->GetLiquidType(input.mapId, st.x, st.y);
	}
	float vmapLiquidLevel = INVALID_HEIGHT; uint32_t vmapLiquidType = 0;
	if (m_vmapManager) {
		float level, floor; uint32_t type;
		if (m_vmapManager->GetLiquidLevel(input.mapId, st.x, st.y, st.z, 0xFF, level, floor, type)) {
			vmapLiquidLevel = level; vmapLiquidType = type;
		}
	}
	float liquidLevel = QueryLiquidLevel(input.mapId, st.x, st.y, st.z, liquidType);
	bool isSwimming = false;
	float swimImmersion = -9999.0f; // diagnostic: liquidLevel - (feet + radius)
	const float swimImmersionThreshold = 1.0f; // new threshold for entering swim state
	if (liquidLevel > PhysicsConstants::INVALID_HEIGHT)
	{
		float refZ = st.z + r; // reference point (top of lower sphere)
		swimImmersion = liquidLevel - refZ;
		if (swimImmersion > swimImmersionThreshold)
		{
			isSwimming = true;
			st.isSwimming = true;
		}
	}
	// NEW: capture ADT terrain height for diagnostics
	float adtTerrainZ = GetTerrainHeight(input.mapId, st.x, st.y);
	PHYS_INFO(PHYS_MOVE, "[Step] WaterDiag posZ=" << st.z
		<< " radius=" << r
		<< " refZ=" << (st.z + r)
		<< " adtTerrainZ=" << adtTerrainZ
		<< " adtWaterLevel=" << adtLiquidLevel
		<< " vmapWaterLevel=" << vmapLiquidLevel
		<< " chosenWater=" << liquidLevel
		<< " immersion=" << swimImmersion
		<< " immersionThreshold=" << swimImmersionThreshold
		<< " prevDeltaConst=" << PhysicsConstants::WATER_LEVEL_DELTA
		<< " willSwim=" << (isSwimming ? 1 : 0));

	// 3. Delegate movement to the appropriate helper method
	float moveSpeed = CalculateMoveSpeed(input, isSwimming);
	if (isSwimming)
	{
		PHYS_INFO(PHYS_MOVE, "[Step] Movement: Swim");
		ProcessSwimMovement(input, intent, st, dt, moveSpeed);
	}
	else if (st.vz != 0)
	{
		PHYS_INFO(PHYS_MOVE, "[Step] Movement: Air");
		ProcessAirMovement(input, intent, st, dt, moveSpeed);
	}
	else
	{
		PHYS_INFO(PHYS_MOVE, "[Step] Movement: Ground");
		ProcessGroundMovement(input, intent, st, dt, moveSpeed, r, h);
	}

	// Diagnostic: VMAP direct height probe and triangle under feet using downward capsule sweep.
	if (m_vmapManager)
	{
		float maxSearch = STEP_HEIGHT + STEP_DOWN_HEIGHT + h;
		// float vmapHeightDirect = m_vmapManager->getHeight(input.mapId, st.x, st.y, st.z + h, maxSearch); // disabled: not calculating during update
		float vmapHeightDirect = INVALID_HEIGHT;
		// Downward capsule sweep to gather triangles below/at feet
		CapsuleCollision::Capsule footCaps; // small vertical span near feet
		float footBottom = st.z + r; // top of lower sphere
		float footTop = footBottom + 0.25f; // small span
		footCaps.p0 = CapsuleCollision::Vec3(st.x, st.y, footBottom);
		footCaps.p1 = CapsuleCollision::Vec3(st.x, st.y, footTop);
		footCaps.r = r;
		G3D::Vector3 downDir(0,0,-1);
		float downDist = STEP_DOWN_HEIGHT + 2.0f; // probe a bit further than snap limit
		std::vector<SceneHit> groundHits = m_vmapManager->SweepCapsuleAll(input.mapId, footCaps, downDir, downDist);
		const SceneHit* bestHit = nullptr;
		float walkableCos = GetWalkableCosMin();
		float bestZ = -FLT_MAX;
		for (const auto& hHit : groundHits)
		{
			// Accept walkable triangles, below or slightly above foot bottom (allow tiny penetration)
			if (!hHit.hit) continue;
			if (hHit.normal.z < walkableCos) continue;
			float hz = hHit.point.z;
			// We want highest surface not above a small epsilon from footBottom
			if (hz <= footBottom + 0.05f && hz > bestZ)
			{
				bestZ = hz;
				bestHit = &hHit;
			}
		}
		if (bestHit)
		{
			PHYS_INFO(PHYS_CYL, "[VMapGetHeight] directZ=" << vmapHeightDirect
				<< " footBottom=" << footBottom
				<< " bestHitZ=" << bestHit->point.z
				<< " triIndex=" << bestHit->triIndex
				<< " instId=" << bestHit->instanceId
				<< " normal=(" << bestHit->normal.x << "," << bestHit->normal.y << "," << bestHit->normal.z << ")"
				<< " penetrationDepth=" << bestHit->penetrationDepth
				<< " hitsTotal=" << groundHits.size());
			// Update output ground identification using best hit
			out.groundTriIndex = bestHit->triIndex;
			out.groundInstanceId = bestHit->instanceId;
			out.groundNx = bestHit->normal.x;
			out.groundNy = bestHit->normal.y;
			out.groundNz = bestHit->normal.z;
			out.groundZ = bestHit->point.z;
		}
		else
		{
			PHYS_INFO(PHYS_CYL, "[VMapGetHeight] directZ=" << vmapHeightDirect
				<< " footBottom=" << footBottom
				<< " noWalkableTriangle hitsTotal=" << groundHits.size());
		}
	}

	// If a ramp is active, update interpolation / deactivate when traversed
	if (st.rampActive)
	{
		G3D::Vector3 curPos(st.x, st.y, st.z);
		float along = (curPos - st.rampStart).dot(st.rampDir);
		if (along < st.rampLength + 0.001f)
		{
			// Recompute Z from plane to smooth out incremental movement (if still below end)
			float planeZ = (-st.rampD - st.rampN.x * curPos.x - st.rampN.y * curPos.y) / (st.rampN.z != 0.0f ? st.rampN.z : 1.0f);
			if (planeZ > st.z && planeZ <= st.rampEnd.z + 0.02f)
			{
				st.z = planeZ;
				PHYS_TRACE(PHYS_STEP, "[Ramp] Interp planeZ=" << planeZ << " along=" << along << "/" << st.rampLength);
			}
		}
		else
		{
			// Reached end; finalize and switch to end normal
			st.z = st.rampEnd.z;
			st.groundNormal = st.rampN; // could switch to final surface normal if stored separately
			st.rampActive = false;
			PHYS_INFO(PHYS_STEP, "[Ramp] Completed ramp traversal finalZ=" << st.z);
		}
	}

	// Output final state
	out.x = st.x;
	out.y = st.y;
	out.z = st.z;
	out.orientation = st.orientation;
	out.pitch = st.pitch;
	out.vx = st.vx;
	out.vy = st.vy;
	out.vz = st.vz;
	out.moveFlags = input.moveFlags; // start from input flags
	// Set / clear swimming flag based on physics decision
	if (isSwimming)
		out.moveFlags |= MOVEFLAG_SWIMMING;
	else
		out.moveFlags &= ~MOVEFLAG_SWIMMING;
	// Propagate ramp state diagnostics (extend PhysicsOutput later if needed)
	out.isGrounded = st.isGrounded;
	out.groundZ = st.z; // simplification
	// Initialize ground identification defaults
	out.groundTriIndex = input.prevGroundTriIndex;
	out.groundInstanceId = input.prevGroundInstanceId;
	out.groundNx = st.groundNormal.x;
	out.groundNy = st.groundNormal.y;
	out.groundNz = st.groundNormal.z;
	// If a recent ground hit was found in this frame, prefer it
	// Note: we infer latest contact from rampEnd when rampActive toggled or from bestHit in downward probe
	// The downward probe above logged bestHit; here we only set output using stable normal/state already in st.
	// Ramp persistence
	out.rampActive = st.rampActive;
	out.rampStartX = st.rampStart.x;
	out.rampStartY = st.rampStart.y;
	out.rampStartZ = st.rampStart.z;
	out.rampEndX = st.rampEnd.x;
	out.rampEndY = st.rampEnd.y;
	out.rampEndZ = st.rampEnd.z;
	out.rampDirX = st.rampDir.x;
	out.rampDirY = st.rampDir.y;
	out.rampDirZ = st.rampDir.z;
	out.rampN_X = st.rampN.x;
	out.rampN_Y = st.rampN.y;
	out.rampN_Z = st.rampN.z;
	out.rampD = st.rampD;
	out.rampLength = st.rampLength;
	
	return out;
}