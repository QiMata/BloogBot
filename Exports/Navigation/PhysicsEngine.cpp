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
	float capTop = st.z + height - radius;
	cap.p0 = CapsuleCollision::Vec3(st.x, st.y, capBottom);
	cap.p1 = CapsuleCollision::Vec3(st.x, st.y, capTop);
	cap.r = radius;
	std::vector<SceneHit> hits;
	if (m_vmapManager)
		hits = m_vmapManager->SweepCapsuleAll(input.mapId, cap, moveDir, intendedDist);
	if (!hits.empty()) {
		const SceneHit& hit = hits.front();
		float travel = std::max(0.0f, hit.distance);
		st.x += moveDir.x * travel;
		st.y += moveDir.y * travel;
		st.groundNormal = hit.normal;
		float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
		if (std::abs(hit.normal.z) >= walkableCosMin) {
			st.isGrounded = true;
			st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep: grounded, travel=" << travel);
			st.z = hit.point.z;
		}
		else {
			st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep: not walkable, velocity zeroed");
		}
	}
	else {
		st.x += moveDir.x * intendedDist;
		st.y += moveDir.y * intendedDist;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule sweep: no collision, moved full distance");
		// Query both VMAP and ADT terrain heights
		float vmapZ = INVALID_HEIGHT;
		if (m_vmapManager)
			vmapZ = m_vmapManager->getHeight(input.mapId, st.x, st.y, st.z + height, STEP_HEIGHT + STEP_DOWN_HEIGHT + height);
		float adtZ = GetTerrainHeight(input.mapId, st.x, st.y);
		float bestZ = st.z;
		bool found = false;
		// Step limits
		float stepUpLimit = STEP_HEIGHT;
		float stepDownLimit = STEP_DOWN_HEIGHT;
		// Check VMAP height
		if (vmapZ > INVALID_HEIGHT) {
			float diff = vmapZ - st.z;
			if ((diff >= 0.0f && diff <= stepUpLimit) || (diff < 0.0f && diff >= -stepDownLimit)) {
				bestZ = vmapZ;
				found = true;
				PHYS_INFO(PHYS_MOVE, "[GroundMove] VMAP height accepted: z=" << vmapZ);
			}
		}
		// Check ADT height
		if (adtZ > INVALID_HEIGHT) {
			float diff = adtZ - st.z;
			if (((diff >= 0.0f && diff <= stepUpLimit) || (diff < 0.0f && diff >= -stepDownLimit)) && (!found || adtZ > bestZ)) {
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
	float liquidLevel = QueryLiquidLevel(input.mapId, st.x, st.y, st.z, liquidType);
	bool isSwimming = false;
	if (liquidLevel > PhysicsConstants::INVALID_HEIGHT && st.z < liquidLevel + PhysicsConstants::WATER_LEVEL_DELTA)
	{
		isSwimming = true;
		st.isSwimming = true;
	}

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

	// Output final state
	out.x = st.x;
	out.y = st.y;
	out.z = st.z;
	out.orientation = st.orientation;
	out.pitch = st.pitch;
	out.vx = st.vx;
	out.vy = st.vy;
	out.vz = st.vz;
	out.moveFlags = input.moveFlags;
	return out;
}