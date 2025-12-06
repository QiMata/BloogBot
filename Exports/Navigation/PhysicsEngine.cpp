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
#include "VMapDefinitions.h"   // for GetLiquidNameUnified

#include <algorithm>
#include <filesystem>
#include <iostream>
#include <iomanip>
#include <cfloat>
#include <chrono>
#include <set>
#include <sstream>

using namespace PhysicsConstants;
using namespace VMAP;

// Global physics logging configuration (defaults)
int gPhysLogLevel = 3;           // 0=ERR,1=INFO,2=DBG,3=TRACE
uint32_t gPhysLogMask = PHYS_ALL; // enable everything initially

// Human-readable movement flags
static std::string FormatMoveFlags(uint32_t flags)
{
	if (flags == MOVEFLAG_NONE) return "NONE";
	std::string s;
	auto add = [&](uint32_t bit, const char* name) {
		if (flags & bit) { if (!s.empty()) s += "|"; s += name; }
	};
	add(MOVEFLAG_FORWARD, "FORWARD");
	add(MOVEFLAG_BACKWARD, "BACKWARD");
	add(MOVEFLAG_STRAFE_LEFT, "STRAFE_LEFT");
	add(MOVEFLAG_STRAFE_RIGHT, "STRAFE_RIGHT");
	add(MOVEFLAG_TURN_LEFT, "TURN_LEFT");
	add(MOVEFLAG_TURN_RIGHT, "TURN_RIGHT");
	add(MOVEFLAG_PITCH_UP, "PITCH_UP");
	add(MOVEFLAG_PITCH_DOWN, "PITCH_DOWN");
	add(MOVEFLAG_WALK_MODE, "WALK_MODE");
	add(MOVEFLAG_UNUSED10, "UNUSED10");
	add(MOVEFLAG_LEVITATING, "LEVITATING");
	add(MOVEFLAG_FIXED_Z, "FIXED_Z");
	add(MOVEFLAG_ROOT, "ROOT");
	add(MOVEFLAG_JUMPING, "JUMPING");
	add(MOVEFLAG_FALLINGFAR, "FALLINGFAR");
	add(MOVEFLAG_PENDING_STOP, "PENDING_STOP");
	add(MOVEFLAG_PENDING_UNSTRAFE, "PENDING_UNSTRAFE");
	add(MOVEFLAG_PENDING_FORWARD, "PENDING_FORWARD");
	add(MOVEFLAG_PENDING_BACKWARD, "PENDING_BACKWARD");
	add(MOVEFLAG_PENDING_STR_LEFT, "PENDING_STR_LEFT");
	add(MOVEFLAG_PENDING_STR_RGHT, "PENDING_STR_RGHT");
	add(MOVEFLAG_SWIMMING, "SWIMMING");
	add(MOVEFLAG_SPLINE_ENABLED, "SPLINE_ENABLED");
	add(MOVEFLAG_MOVED, "MOVED");
	add(MOVEFLAG_FLYING, "FLYING");
	add(MOVEFLAG_ONTRANSPORT, "ONTRANSPORT");
	add(MOVEFLAG_SPLINE_ELEVATION, "SPLINE_ELEVATION");
	add(MOVEFLAG_UNUSED28, "UNUSED28");
	add(MOVEFLAG_WATERWALKING, "WATERWALKING");
	add(MOVEFLAG_SAFE_FALL, "SAFE_FALL");
	add(MOVEFLAG_HOVER, "HOVER");
	add(MOVEFLAG_UNUSED32, "UNUSED32");
	return s.empty() ? std::string("0") : s;
}

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
	m_initialized(false) {
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

// Expose MapLoader for read-only terrain queries
MapLoader* PhysicsEngine::GetMapLoader() const
{
    return m_mapLoader.get();
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

float PhysicsEngine::GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType)
{
	if (m_mapLoader && m_mapLoader->IsInitialized())
	{
		float level = m_mapLoader->GetLiquidLevel(mapId, x, y);
		if (VMAP::IsValidLiquidLevel(level))
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

	return VMAP::VMAP_INVALID_LIQUID_HEIGHT;
}

float PhysicsEngine::QueryLiquidLevel(uint32_t mapId, float x, float y, float z, uint32_t& liquidType) const
{
    return const_cast<PhysicsEngine*>(this)->GetLiquidHeight(mapId, x, y, z, liquidType);
}

PhysicsEngine::LiquidInfo PhysicsEngine::EvaluateLiquidAt(uint32_t mapId, float x, float y, float z) const
{
    LiquidInfo info{};
    float adtLevel = VMAP_INVALID_LIQUID_HEIGHT; uint32_t adtType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
    if (m_mapLoader && m_mapLoader->IsInitialized()) {
        adtLevel = m_mapLoader->GetLiquidLevel(mapId, x, y);
        if (MapFormat::IsValidLiquidLevel(adtLevel))
            adtType = m_mapLoader->GetLiquidType(mapId, x, y);
    }
    float vmapLevel = VMAP_INVALID_LIQUID_HEIGHT; uint32_t vmapType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
    bool vmapHasLevel = false;
    if (m_vmapManager) {
        float level, floor; uint32_t type;
        if (m_vmapManager->GetLiquidLevel(mapId, x, y, z + 2.0f, VMAP::MAP_LIQUID_TYPE_ALL_LIQUIDS, level, floor, type)) {
            vmapLevel = level; vmapType = type; vmapHasLevel = true;
        }
    }
    info.fromVmap = vmapHasLevel;
    info.level = vmapHasLevel ? vmapLevel : adtLevel;
    uint32_t unifiedType = vmapHasLevel ? GetLiquidEnumUnified(vmapType, true) : GetLiquidEnumUnified(adtType, false);
    info.type = unifiedType;
    info.hasLevel = VMAP::IsValidLiquidLevel(info.level);
    if (info.hasLevel) {
        float immersion = info.level - z;
        info.isSwimming = immersion > 0.0f && unifiedType == LIQUID_TYPE_WATER;
    }
    return info;
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
namespace {
    static inline G3D::Vector3 DirectionOrFallback(const G3D::Vector3& v, const G3D::Vector3& fallback)
    {
        float m = v.magnitude();
        return (m > 1e-5f) ? (v * (1.0f / m)) : fallback;
    }
}

void PhysicsEngine::ProcessGroundMovement(const PhysicsInput& input, const MovementIntent& intent,
	MovementState& st, float dt, float speed,
	float radius, float height)
{
	// Suppress GroundMove-specific InputSummary; consolidated one is emitted in Step
	/*PHYS_INFO(PHYS_MOVE,
		std::string("[GroundMove] InputSummary\n")
		<< "  map=" << input.mapId << " dt=" << dt << "\n"
		<< "  pos=(" << st.x << "," << st.y << "," << st.z << ")\n"
		<< "  velIn=(" << input.vx << "," << input.vy << "," << input.vz << ")\n"
		<< "  intentV=(" << st.vx << "," << st.vy << ") intendedDist=" << intendedDist << "\n"
		<< "  flags=" << FormatMoveFlags(input.moveFlags) << " (0x" << std::hex << input.moveFlags << std::dec << ")\n"
		<< "  orient=" << input.orientation << " pitch=" << input.pitch << "\n"
		<< "  size: radius=" << radius << " height=" << height << "\n"
		<< "  speeds[wlk=" << input.walkSpeed << " run=" << input.runSpeed << " back=" << input.runBackSpeed
		<< " swim=" << input.swimSpeed << " swimBack=" << input.swimBackSpeed << " fly=" << input.flightSpeed << "]\n"
		<< "  fallTime=" << input.fallTime << " transportGuid=" << input.transportGuid << "\n"
		<< "  spline=" << (input.hasSplinePath?1:0) << " splineSpeed=" << input.splineSpeed << " curSplineIdx=" << input.currentSplineIndex);*/

	// PHYS_INFO(PHYS_MOVE, "[GroundMove] Start pos=" << st.x << "," << st.y << "," << st.z << " vel=" << st.vx << "," << st.vy << " dt=" << dt);

	// Track previous position for actual velocity reporting in summary
	G3D::Vector3 prevPos(st.x, st.y, st.z);

	// Global step limits and thresholds used across branches
	const float stepUpLimit = STEP_HEIGHT;
	const float stepDownLimit = STEP_DOWN_HEIGHT;
	const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
	const float TOL = 1e-5f;

	// --- Intent & early-outs ---
	if (intent.jumpRequested) {
		st.vz = JUMP_VELOCITY;
		st.isGrounded = false;
		st.fallTime = 0;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=Jump vz=" << st.vz);
		return;
	}
	if (!intent.hasInput) {
		st.vx = st.vy = 0.0f;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=NoInput vx/vy=0 (will perform downward settle)");

		// Build a normal-sized capsule spanning from max step-up down to step-down
		CapsuleCollision::Capsule cap;
		float capBottom = (st.z + radius) - stepDownLimit; // extend below feet
		float capTop    = (st.z + radius) + stepUpLimit;   // extend above feet
		// Ensure segment length does not exceed full capsule height
		float fullSegLen = (height - 2.0f * radius);
		float desiredSegLen = capTop - capBottom;
		if (desiredSegLen > fullSegLen) {
			float overflow = desiredSegLen - fullSegLen;
			// Center segment around feet by trimming equally from top and bottom
			capBottom += overflow * 0.5f;
			capTop    -= overflow * 0.5f;
		}
		cap.p0 = CapsuleCollision::Vec3(st.x, st.y, capBottom);
		cap.p1 = CapsuleCollision::Vec3(st.x, st.y, capTop);
		cap.r = radius;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] SettleCapsule p0=(" << cap.p0.x << "," << cap.p0.y << "," << cap.p0.z << ") p1=(" << cap.p1.x << "," << cap.p1.y << "," << cap.p1.z << ") r=" << cap.r << " spanUp=" << stepUpLimit << " spanDown=" << stepDownLimit);

		// Downward sweep to find closest walkable surface
		G3D::Vector3 downDir(0, 0, -1);
		float settleDist = std::max(3.0f, height + 2.0f);
		std::vector<SceneHit> downHits;
		if (m_vmapManager)
			downHits = m_vmapManager->SweepCapsuleAll(input.mapId, cap, downDir, settleDist);
		PHYS_INFO(PHYS_MOVE, "[GroundMove] DownwardSweep count=" << downHits.size() << " dist=" << settleDist);

		 // Single-pass evaluation: track earliest valid non-penetrating walkable and best penetrating surface simultaneously.
		const SceneHit* bestNP = nullptr; // earliest non-penetrating walkable hit
		const SceneHit* bestPen = nullptr; // highest penetrating upward-facing contact
		float bestPenZ = -FLT_MAX;
		for (size_t i = 0; i < downHits.size(); ++i) {
			const auto& h = downHits[i];
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownHit idx=" << i << " startPen=" << (h.startPenetrating?1:0) << " dist=" << h.distance << " nZ=" << h.normal.z << " pZ=" << h.point.z);
			if (h.startPenetrating) {
				// penetrating: consider for bestPen if upward facing
				if (h.normal.z >= 0.0f && h.point.z > bestPenZ) {
					bestPenZ = h.point.z; bestPen = &h; PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownPenCandidate idx=" << i << " pZ=" << h.point.z);
				}
				continue; // cannot be bestNP
			}
			// non-penetrating candidate for walkable surface
			if (h.normal.z < walkableCosMin) { PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownReject idx=" << i << " reason=Unwalkable nZ=" << h.normal.z); continue; }
			bestNP = &h; // earliest due to input ordering
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownSelectNonPen idx=" << i);
			break; // earliest acceptable
		}

		if (bestNP) {
			// Snap to non-penetrating walkable surface (do not allow upward beyond step height here since sweep already constrained)
			st.z = bestNP->point.z;
			st.isGrounded = true;
			st.groundNormal = bestNP->normal.directionOrZero();
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Settle result: NonPenetrating z=" << st.z << " nZ=" << st.groundNormal.z);
			return;
		}
		if (bestPen) {
			// Penetrating fallback: adjust up to highest penetrating upward-facing contact
			st.z = bestPen->point.z;
			st.isGrounded = true;
			st.groundNormal = bestPen->normal.directionOrZero();
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Settle result: Penetrating adjust to z=" << st.z << " nZ=" << st.groundNormal.z);
			return;
		}

		// Nothing found: start falling
		st.isGrounded = false;
		if (st.vz >= 0.0f) st.vz = -0.1f;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Settle result: No ground, start falling vz=" << st.vz);
		return;
	}
	st.vx = intent.dir.x * speed;
	st.vy = intent.dir.y * speed;
	// PHYS_INFO(PHYS_MOVE, "Intent input vx=" << st.vx << " vy=" << st.vy);

	G3D::Vector3 moveDir(intent.dir.x, intent.dir.y, 0.0f);
	float intendedDist = std::sqrt(st.vx * st.vx + st.vy * st.vy) * dt;
	// PHYS_INFO(PHYS_MOVE, "intendedDist=" << intendedDist);

	// Consolidated input summary (replaces individual logs above)
	/*PHYS_INFO(PHYS_MOVE,
		std::string("[GroundMove] InputSummary\n")
		<< "  map=" << input.mapId << " dt=" << dt << "\n"
		<< "  pos=(" << st.x << "," << st.y << "," << st.z << ")\n"
		<< "  velIn=(" << input.vx << "," << input.vy << "," << input.vz << ")\n"
		<< "  intentV=(" << st.vx << "," << st.vy << ") intendedDist=" << intendedDist << "\n"
		<< "  flags=" << FormatMoveFlags(input.moveFlags) << " (0x" << std::hex << input.moveFlags << std::dec << ")\n"
		<< "  orient=" << input.orientation << " pitch=" << input.pitch << "\n"
		<< "  size: radius=" << radius << " height=" << height << "\n"
		<< "  speeds[wlk=" << input.walkSpeed << " run=" << input.runSpeed << " back=" << input.runBackSpeed
		<< " swim=" << input.swimSpeed << " swimBack=" << input.swimBackSpeed << " fly=" << input.flightSpeed << "]\n"
		<< "  fallTime=" << input.fallTime << " transportGuid=" << input.transportGuid << "\n"
		<< "  spline=" << (input.hasSplinePath?1:0) << " splineSpeed=" << input.splineSpeed << " curSplineIdx=" << input.currentSplineIndex);*/

	if (intendedDist <= 0.0f) {
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=ZeroDistance");
		return;
	}

	// --- Build foot capsule spanning step-up to step-down ---
	CapsuleCollision::Capsule cap;
	// use existing stepUpLimit and stepDownLimit defined earlier in this scope
	float capBottom = (st.z + radius) - stepDownLimit;
	float capTop    = (st.z + radius) + stepUpLimit;
	float fullSegLen = (height - 2.0f * radius);
	float desiredSegLen = capTop - capBottom;
	if (desiredSegLen > fullSegLen) {
		float overflow = desiredSegLen - fullSegLen;
		capBottom += overflow * 0.5f;
		capTop    -= overflow * 0.5f;
	}
	cap.p0 = CapsuleCollision::Vec3(st.x, st.y, capBottom);
	cap.p1 = CapsuleCollision::Vec3(st.x, st.y, capTop);
	cap.r = radius;
	PHYS_INFO(PHYS_MOVE, "[GroundMove] Capsule p0=(" << cap.p0.x << "," << cap.p0.y << "," << cap.p0.z << ") p1=(" << cap.p1.x << "," << cap.p1.y << "," << cap.p1.z << ") r=" << cap.r << " spanUp=" << stepUpLimit << " spanDown=" << stepDownLimit << " fullSegLen=" << fullSegLen);

	std::vector<SceneHit> hits;
	if (m_vmapManager)
		hits = m_vmapManager->SweepCapsuleAll(input.mapId, cap, moveDir, intendedDist);
	// PHYS_INFO(PHYS_MOVE, "[GroundMove] SweepCapsuleAll count=" << hits.size()); // commented out per request

	// Build a human-readable multi-line string summarizing all hits for later decision summary
	std::string hitsSummary;
	{
		std::ostringstream oss;
		oss << "hits=" << hits.size();
		for (size_t i = 0; i < hits.size(); ++i)
		{
			const auto& h = hits[i];
			oss << "\n  [" << i
				<< "] tri=" << h.triIndex
				<< " instId=" << h.instanceId
				<< " startPen=" << (h.startPenetrating?1:0)
				<< " dist=" << h.distance << "\n"
				<< "     n=(" << h.normal.x << "," << h.normal.y << "," << h.normal.z << ")"
				<< " p=(" << h.point.x << "," << h.point.y << "," << h.point.z << ")";
		}
		hitsSummary = oss.str();
	}
	auto LogDecisionSummary = [&](const char* decision)
	{
		// Compute actual velocity from prevPos to current pos (horizontal only for ground unless falling/jumping)
		G3D::Vector3 curPos(st.x, st.y, st.z);
		G3D::Vector3 actualV = (curPos - prevPos) * (dt > 0.0f ? (1.0f / dt) : 0.0f);
		// Suppress vertical component for ground movement (ramp/step adjustments) unless we are leaving ground (fall) or a jump was requested earlier in frame
		if (st.isGrounded && st.vz == 0.0f && !intent.jumpRequested) {
			actualV.z = 0.0f;
		}
		PHYS_INFO(PHYS_MOVE,
			std::string("[GroundMove] Summary\n") << hitsSummary << "\n"
			<< "decision=" << decision << "\n"
			<< "pos=(" << st.x << "," << st.y << "," << st.z << ")\n"
			<< "groundNormal=(" << st.groundNormal.x << "," << st.groundNormal.y << "," << st.groundNormal.z << ")\n"
			<< "intentVel=(" << (intent.hasInput ? intent.dir.x * speed : 0.0f) << "," << (intent.hasInput ? intent.dir.y * speed : 0.0f) << ")"
			<< " actualVel=(" << actualV.x << "," << actualV.y << "," << actualV.z << ")");
	};

	// --- Step limits and thresholds ---
	// (already defined at function start)

	// Helper lambda: attempt step-up from penetration when all hits are penetrating
	auto TryStepUpFromPenetration = [&](const std::vector<SceneHit>& allHits) -> bool {
		// Find best candidate point within stepUpLimit above current feet
		const SceneHit* best = nullptr;
		float bestDz = FLT_MAX;
		for (const auto& h : allHits) {
			if (!h.startPenetrating) continue; // only consider penetration contacts
			float dz = h.point.z - st.z;
			if (dz < 0.0f || dz > stepUpLimit + 0.01f) continue;
			// Prefer surfaces with a reasonably upward facing normal
			if (h.normal.z < 0.0f) continue; // ignore upside-down faces
			// record smallest upward move (closest step) so we do not over-ascend
			if (dz < bestDz) { bestDz = dz; best = &h; }
		}
		if (!best) return false;
		// Perform horizontal advance (full intendedDist) while stepping up
		G3D::Vector3 moveDirN = DirectionOrFallback(moveDir, G3D::Vector3(1,0,0));
		st.x += moveDirN.x * intendedDist;
		st.y += moveDirN.y * intendedDist;
		st.z = best->point.z; // snap to candidate surface height
		st.isGrounded = true;
		st.vx = st.vy = 0.0f;
		st.groundNormal = best->normal.z > 0.0f ? best->normal.directionOrZero() : G3D::Vector3(0,0,1);
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=StepUpPen tri=" << best->triIndex << " dz=" << bestDz << " newPos=(" << st.x << "," << st.y << "," << st.z << ")");
		LogDecisionSummary("StepUpPen");
		return true;
	};

	if (!hits.empty()) {
		// 1) If overlapping a walkable surface at start, perform a simple slide along its plane.
		const SceneHit& firstHit = hits.front();
		// PHYS_INFO(PHYS_MOVE, "[GroundMove] FirstHit tri=" << firstHit.triIndex << " instId=" << firstHit.instanceId << " startPen=" << (firstHit.startPenetrating?1:0) << " dist=" << firstHit.distance << " n=(" << firstHit.normal.x << "," << firstHit.normal.y << "," << firstHit.normal.z << ") p=(" << firstHit.point.x << "," << firstHit.point.y << "," << firstHit.point.z << ")"); // commented out per request
		float nZ = firstHit.normal.z; // use signed Z now
		bool walkableStartPen = firstHit.startPenetrating && nZ >= walkableCosMin;
		if (walkableStartPen) {
			 // If both walkable and unwalkable penetrating planes are present, prefer stepping up onto the walkable surface.
			bool hasUnwalkablePen = false;
			const SceneHit* bestWalkPen = nullptr;
			float bestWalkDz = FLT_MAX;
			for (const auto& h : hits) {
				if (!h.startPenetrating) continue;
				if (h.normal.z >= walkableCosMin) {
					float dz = h.point.z - st.z;
					if (dz >= 0.0f && dz <= stepUpLimit + 0.01f) {
						if (dz < bestWalkDz) { bestWalkDz = dz; bestWalkPen = &h; }
					}
				} else {
					hasUnwalkablePen = true;
				}
			}
			if (bestWalkPen && hasUnwalkablePen) {
				// Step up onto the walkable penetrating contact and advance horizontally.
				G3D::Vector3 moveDirN = DirectionOrFallback(moveDir, G3D::Vector3(1,0,0));
				st.x += moveDirN.x * intendedDist;
				st.y += moveDirN.y * intendedDist;
				st.z = bestWalkPen->point.z;
				st.isGrounded = true;
				st.vx = st.vy = 0.0f;
				st.groundNormal = bestWalkPen->normal.directionOrZero();
				LogDecisionSummary("StepUpPen(Start)");
				return;
			}

			// Default behavior: slide along the walkable plane when only walkable penetration exists.
			G3D::Vector3 n = firstHit.normal.directionOrZero();
			if (n.magnitude() < TOL) n = G3D::Vector3(0,0,1);
			G3D::Vector3 moveDirN = DirectionOrFallback(moveDir, G3D::Vector3(1,0,0));
			G3D::Vector3 slideDir = (moveDirN - n * moveDirN.dot(n)).directionOrZero();
			float travel = intendedDist;
			float newX = st.x + slideDir.x * travel;
			float newY = st.y + slideDir.y * travel;
			float D = -n.dot(firstHit.point);
			float newZ = st.z;
			if (std::fabs(n.z) > TOL) {
				newZ = (-D - n.x * newX - n.y * newY) / n.z;
			}
			float dzSlide = newZ - st.z;
			if (dzSlide > stepUpLimit) newZ = st.z + stepUpLimit; else if (dzSlide < -stepDownLimit) newZ = st.z - stepDownLimit;
			st.x = newX; st.y = newY; st.z = newZ; st.isGrounded = true; st.groundNormal = n; st.vx = st.vy = 0.0f;
			LogDecisionSummary("SlideStartPen");
			return;
		}

		// 2) Find earliest walkable non-penetrating step candidate (positive Z normal only)
		const SceneHit* chosenWalkable = nullptr;
		for (size_t i = 0; i < hits.size(); ++i) {
			const auto& h = hits[i];
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] EvalHit idx=" << i << " tri=" << h.triIndex << " startPen=" << (h.startPenetrating?1:0) << " dist=" << h.distance << " nZ=" << h.normal.z << " pZ=" << h.point.z);
			if (h.startPenetrating) {
				PHYS_TRACE(PHYS_MOVE, "[GroundMove] Reject idx=" << i << " reason=StartPenetrating");
				continue;
			}
			if (h.distance <= 1e-4f) {
				PHYS_TRACE(PHYS_MOVE, "[GroundMove] Reject idx=" << i << " reason=ZeroOrTinyDistance dist=" << h.distance);
				continue;
			}
			if (h.normal.z < walkableCosMin) {
				PHYS_TRACE(PHYS_MOVE, "[GroundMove] Reject idx=" << i << " reason=UnwalkableNormal nZ=" << h.normal.z << " thresh=" << walkableCosMin);
				continue;
			}
			float dz = h.point.z - st.z;
			if (!(dz >= 0.0f && dz <= stepUpLimit)) {
				PHYS_TRACE(PHYS_MOVE, "[GroundMove] Reject idx=" << i << " reason=StepUpRange dz=" << dz << " limit=" << stepUpLimit);
				continue;
			}
			chosenWalkable = &h; break;
		}
		if (chosenWalkable) {
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=StepUp tri=" << chosenWalkable->triIndex << " dist=" << chosenWalkable->distance << " targetZ=" << chosenWalkable->point.z);
			G3D::Vector3 oldPos(st.x, st.y, st.z);
			float travel = std::max(0.0f, chosenWalkable->distance);
			G3D::Vector3 moveDirN = DirectionOrFallback(moveDir, G3D::Vector3(1,0,0));
			G3D::Vector3 newPos = oldPos + moveDirN * travel;
			G3D::Vector3 steppedPoint(chosenWalkable->point.x, chosenWalkable->point.y, chosenWalkable->point.z);
			G3D::Vector3 up(0,0,1);
			G3D::Vector3 along = (steppedPoint - oldPos);
			G3D::Vector3 side = DirectionOrFallback(moveDirN.cross(up), G3D::Vector3(0,1,0));
			G3D::Vector3 rampN = along.cross(side).directionOrZero();
			if (rampN.magnitude() < TOL) rampN = DirectionOrFallback(chosenWalkable->normal, up);
			if (rampN.z < 0.0f) rampN = -rampN;
			float rampD = -rampN.dot(oldPos);
			float interpZ = (-rampD - rampN.x * newPos.x - rampN.y * newPos.y) / (std::fabs(rampN.z) > TOL ? rampN.z : 1.0f);
			float targetZ = steppedPoint.z;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Ramp calc travel=" << travel << " newXY=(" << newPos.x << "," << newPos.y << ") interpZ=" << interpZ << " targetZ=" << targetZ << " rampN=(" << rampN.x << "," << rampN.y << "," << rampN.z << ")");
			if ((interpZ > oldPos.z && interpZ < targetZ) || std::fabs(interpZ - targetZ) < 0.01f)
				st.z = interpZ;
			else
				st.z = targetZ;
			st.x = newPos.x; st.y = newPos.y;
			st.groundNormal = rampN;
			st.isGrounded = true;
			st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Result StepUp pos=(" << st.x << "," << st.y << "," << st.z << ") rampActive=0");
			LogDecisionSummary("StepUp");
			return;
		}

		// 2b) Fallback: all penetrating contacts, try penetration-based step up.
		bool allPenetrating = true;
		for (const auto& h : hits) { if (!h.startPenetrating) { allPenetrating = false; break; } }
		if (allPenetrating && firstHit.distance <= 1e-4f) {
			if (TryStepUpFromPenetration(hits)) {
				return; // stepped up successfully
			}
		}

		// 3) No step candidate: obstruction branch
		const SceneHit& hit = hits.front();
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=Obstruction tri=" << hit.triIndex << " dist=" << hit.distance << " nZ=" << hit.normal.z << " pZ=" << hit.point.z);
		float travel = std::max(0.0f, hit.distance);
		st.x += moveDir.x * travel;
		st.y += moveDir.y * travel;
		st.groundNormal = hit.normal;
		if (hit.normal.z >= walkableCosMin) {
			float dz = hit.point.z - st.z;
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] ObstructionEval dz=" << dz << " stepUpLimit=" << stepUpLimit << " stepDownLimit=" << stepDownLimit);
			if ((dz >= 0.0f && dz <= stepUpLimit) || (dz < 0.0f && -dz <= stepDownLimit)) {
				st.z = hit.point.z;
				st.isGrounded = true;
			} else {
				PHYS_TRACE(PHYS_MOVE, "[GroundMove] ObstructionReject reason=OutOfRange dz=" << dz);
			}
			st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Result Obstruction walkable=1 travel=" << travel << " newPos=(" << st.x << "," << st.y << "," << st.z << ")");
			LogDecisionSummary("Obstruction");
		} else {
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] ObstructionReject reason=UnwalkableNormal nZ=" << hit.normal.z);
			st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Result Obstruction walkable=0 travel=" << travel << " newPos=(" << st.x << "," << st.y << "," << st.z << ")");
			LogDecisionSummary("Obstruction");
		}
		return;
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
	// Suppress per-mode input logs; provide a consolidated input summary
	/*PHYS_TRACE(PHYS_MOVE, " map=" << input.mapId << " dt=" << dt << "\n"
		<< "  Input pos=(" << input.x << "," << input.y << "," << input.z << ") vel=(" << input.vx << "," << input.vy << "," << input.vz << ")\n"
		<< "  flags=" << FormatMoveFlags(input.moveFlags) << " (0x" << std::hex << input.moveFlags << std::dec << ")\n"
		<< "  orient=" << input.orientation << " pitch=" << input.pitch << "\n"
		<< "  size: radius=" << input.radius << " height=" << input.height << "\n"
		<< "  speeds[wlk=" << input.walkSpeed << " run=" << input.runSpeed << " back=" << input.runBackSpeed
		<< " swim=" << input.swimSpeed << " swimBack=" << input.swimBackSpeed << " fly=" << input.flightSpeed << "]\n"
		<< "  fallTime=" << input.fallTime);*/

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

	// Consolidated InputSummary (independent of movement mode)
	{
		float moveSpeed = CalculateMoveSpeed(input, false);
		G3D::Vector3 intentV = intent.hasInput ? G3D::Vector3(intent.dir.x * moveSpeed, intent.dir.y * moveSpeed, 0.0f) : G3D::Vector3(0,0,0);
		float intendedDist = (intent.hasInput ? std::sqrt(intentV.x * intentV.x + intentV.y * intentV.y) * dt : 0.0f);
		PHYS_INFO(PHYS_MOVE,
			std::string("[Step] InputSummary\n")
			<< "  map=" << input.mapId << " dt=" << dt << "\n"
			<< "  pos=(" << st.x << "," << st.y << "," << st.z << ")\n"
			<< "  velIn=(" << input.vx << "," << input.vy << "," << input.vz << ")\n"
			<< "  intentV=(" << intentV.x << "," << intentV.y << ") intendedDist=" << intendedDist << "\n"
			<< "  flags=" << FormatMoveFlags(input.moveFlags) << " (0x" << std::hex << input.moveFlags << std::dec << ")\n"
			<< "  orient=" << input.orientation << " pitch=" << input.pitch << "\n"
			<< "  size: radius=" << r << " height=" << h << "\n"
			<< "  speeds[wlk=" << input.walkSpeed << " run=" << input.runSpeed << " back=" << input.runBackSpeed
			<< " swim=" << input.swimSpeed << " swimBack=" << input.swimBackSpeed << " fly=" << input.flightSpeed << "]\n"
			<< "  fallTime=" << input.fallTime << " transportGuid=" << input.transportGuid << "\n"
			<< "  spline=" << (input.hasSplinePath?1:0) << " splineSpeed=" << input.splineSpeed << " curSplineIdx=" << input.currentSplineIndex);
	}

	// Capture previous position to compute actual velocity at end of step
	G3D::Vector3 prevPos(st.x, st.y, st.z);
	float prevZ = st.z;

	// Evaluate liquid at current position
	LiquidInfo liq = EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	bool isSwimming = liq.isSwimming;
	bool liquidFromVmap = liq.fromVmap;

	// Build diagnostic summary of capsule sweep (VMAP) and ADT terrain triangles (capsule AABB)
	float diagSpeed = CalculateMoveSpeed(input, isSwimming);
	G3D::Vector3 diagMoveDir = intent.hasInput ? G3D::Vector3(intent.dir.x, intent.dir.y, 0.0f) : G3D::Vector3(0,0,0);
	float diagIntendedDist = (diagMoveDir.magnitude() > 0.0f) ? (diagSpeed * dt) : 0.0f;

	SweepDiagnostics sweepDiag = ComputeCapsuleSweepDiagnostics(input.mapId, st.x, st.y, st.z, r, h, diagMoveDir, diagIntendedDist);

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

	// Compute actual velocity based on position delta over dt for this step
	G3D::Vector3 curPos(st.x, st.y, st.z);
	G3D::Vector3 actualV(0,0,0);
	if (dt > 0.0f)
		actualV = (curPos - prevPos) * (1.0f / dt);
	// Zero out vertical component unless airborne (falling/jumping) or swimming (vertical motion intentional)
	bool airborne = (!st.isGrounded) || (st.vz != 0.0f);
	if (!airborne && !isSwimming) {
		actualV.z = 0.0f;
	}

	// Re-evaluate liquid at final position
	LiquidInfo finalLiq = EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	uint32_t finalLiquidType = finalLiq.type;
	float finalLiquidLevel = finalLiq.level;
	bool finalSwimming = finalLiq.isSwimming;
	bool finalVmapHasLevel = finalLiq.fromVmap;

	// Output final state
	out.x = st.x;
	out.y = st.y;
	out.z = st.z;
	out.orientation = st.orientation;
	out.pitch = st.pitch;
	out.vx = actualV.x;
	out.vy = actualV.y;
	out.vz = actualV.z;
	out.moveFlags = input.moveFlags;
	if (finalSwimming)
	{
		const uint32_t incompatibleSwim =
			MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR | MOVEFLAG_FLYING | MOVEFLAG_ROOT |
			MOVEFLAG_PENDING_STOP | MOVEFLAG_PENDING_UNSTRAFE | MOVEFLAG_PENDING_FORWARD |
			MOVEFLAG_PENDING_BACKWARD | MOVEFLAG_PENDING_STR_LEFT | MOVEFLAG_PENDING_STR_RGHT;
		out.moveFlags |= MOVEFLAG_SWIMMING;
		out.moveFlags &= ~incompatibleSwim;
		if (intent.hasInput && !(out.moveFlags & (MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD | MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT)))
			out.moveFlags |= MOVEFLAG_FORWARD;
	}
	else
	{
		out.moveFlags &= ~MOVEFLAG_SWIMMING;
	}
	out.groundZ = st.z;
	out.liquidZ = finalLiquidLevel;
	out.liquidType = finalLiquidType;
	const char* liquidName = VMAP::GetLiquidTypeName(out.liquidType);
	const char* primarySrc = liquidFromVmap ? "VMAP" : "ADT";
	const char* finalSrc = finalVmapHasLevel ? "VMAP" : "ADT";
	PHYS_INFO(PHYS_MOVE,
		std::string("[Step] OutputSummary\n")
		<< "  pos=" << "(" << out.x << "," << out.y << "," << out.z << ")\n"
		<< "  vel=" << "(" << out.vx << "," << out.vy << "," << out.vz << ")\n"
		<< "  flags=" << FormatMoveFlags(out.moveFlags) << " (0x" << std::hex << out.moveFlags << std::dec << ")\n"
		<< "  orient=" << out.orientation << " pitch=" << out.pitch << "\n"
		<< "  groundZ=" << out.groundZ << " groundN=(" << out.groundNx << "," << out.groundNy << "," << out.groundNz << ")\n"
		<< "  liquidZ=" << out.liquidZ << " type=" << liquidName
		<< " primarySrc=" << primarySrc
		<< " finalSrc=" << finalSrc << " normalized=" << out.liquidType);
	return out;
}

// =====================================================================================
// Diagnostic helpers
// =====================================================================================
PhysicsEngine::SweepDiagnostics PhysicsEngine::ComputeCapsuleSweepDiagnostics(
    uint32_t mapId,
    float x,
    float y,
    float z,
    float r,
    float h,
    const G3D::Vector3& moveDir,
    float intendedDist)
{
    SweepDiagnostics diag{};

    // Build diagnostic capsule spanning step up/down around feet
    float capBottom = (z + r);
    float capTop = (z - r) + h;
    CapsuleCollision::Capsule diagCap;
    diagCap.p0 = CapsuleCollision::Vec3(x, y, capBottom);
    diagCap.p1 = CapsuleCollision::Vec3(x, y, capTop);
    diagCap.r = r + 1e-6f;

    // Evaluate liquid at start to determine swimming; used to suppress ground constraints
    LiquidInfo liqStart = EvaluateLiquidAt(mapId, x, y, z);
    bool startSwimming = liqStart.isSwimming;

    // Special case: no input / zero intended distance -> prefer downward sweep (only if not swimming)
    const bool noInput = intendedDist <= 0.0f || moveDir.magnitude() <= 1e-6f;
    std::vector<SceneHit> vmapHits;
    if (m_vmapManager && !noInput && intendedDist > 0.0f && !startSwimming)
        vmapHits = m_vmapManager->SweepCapsuleAll(mapId, diagCap, moveDir, intendedDist);

    // If no input, gather top-down VMAP contacts using downward sweep (only if not swimming)
    std::vector<SceneHit> vmapDownHits;
    if (m_vmapManager && noInput && !startSwimming) {
        G3D::Vector3 downDir(0,0,-1);
        float settleDist = std::max(3.0f, h + 2.0f);
        vmapDownHits = m_vmapManager->SweepCapsuleAll(mapId, diagCap, downDir, settleDist);
        if (!vmapDownHits.empty()) {
            vmapHits = vmapDownHits;
        }
    }

    // Populate VMAP-only summary stats (counts and z-range, instances)
    {
        diag.vmapHitCount = vmapHits.size();
        size_t vPen = 0, vNonPen = 0, vWalkNP = 0; float vEarliestNP = FLT_MAX;
        float vMinZ = FLT_MAX, vMaxZ = -FLT_MAX; std::set<uint32_t> vInst;
        for (const auto& h : vmapHits) {
            if (h.startPenetrating) vPen++; else { vNonPen++; vEarliestNP = std::min(vEarliestNP, h.distance); if (h.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) vWalkNP++; }
            vMinZ = std::min(vMinZ, h.point.z); vMaxZ = std::max(vMaxZ, h.point.z); vInst.insert(h.instanceId);
        }
        diag.vmapPenCount = vPen; diag.vmapNonPenCount = vNonPen; diag.vmapWalkableNonPen = vWalkNP;
        diag.vmapEarliestNonPen = (vEarliestNP == FLT_MAX) ? -1.0f : vEarliestNP;
        if (diag.vmapHitCount == 0) { vMinZ = 0.0f; vMaxZ = 0.0f; }
        diag.vmapHitMinZ = vMinZ; diag.vmapHitMaxZ = vMaxZ; diag.vmapUniqueInstanceCount = vInst.size();
    }

    // Normalize VMAP hit time of impact (TOI in [0,1]) for continuous collision processing
    if (!noInput && intendedDist > 0.0f) {
        for (auto& h : vmapHits) {
            float toi = (h.distance <= 0.0f) ? 0.0f : (h.distance / intendedDist);
            if (toi < 0.0f) toi = 0.0f; else if (toi > 1.0f) toi = 1.0f;
            h.time = toi;
        }
        std::stable_sort(vmapHits.begin(), vmapHits.end(), [](const SceneHit& a, const SceneHit& b) {
            if (a.startPenetrating != b.startPenetrating)
                return a.startPenetrating > b.startPenetrating;
            return a.time < b.time;
        });
    } else if (noInput) {
        std::stable_sort(vmapHits.begin(), vmapHits.end(), [](const SceneHit& a, const SceneHit& b) {
            if (std::fabs(a.point.z - b.point.z) > 1e-4f) return a.point.z > b.point.z;
            return a.triIndex < b.triIndex;
        });
    }

    // ADT terrain triangles within swept AABB (only when moving)
    float endX = x + moveDir.x * intendedDist;
    float endY = y + moveDir.y * intendedDist;
    float minX = std::min(x, endX) - r;
    float minY = std::min(y, endY) - r;
    float maxX = std::max(x, endX) + r;
    float maxY = std::max(y, endY) + r;
    std::vector<MapFormat::TerrainTriangle> triBuf;
    if (m_mapLoader && m_mapLoader->IsInitialized()) {
        m_mapLoader->GetTerrainTriangles(mapId, minX, minY, maxX, maxY, triBuf);
    }
    {
        diag.terrainTriCount = triBuf.size();
        float tMinZ = FLT_MAX, tMaxZ = -FLT_MAX;
        for (const auto& tw : triBuf) {
            tMinZ = std::min(tMinZ, std::min(tw.az, std::min(tw.bz, tw.cz)));
            tMaxZ = std::max(tMaxZ, std::max(tw.az, std::max(tw.bz, tw.cz)));
        }
        if (diag.terrainTriCount == 0) { tMinZ = 0.0f; tMaxZ = 0.0f; }
        diag.terrainMinZ = tMinZ; diag.terrainMaxZ = tMaxZ;
    }

    // Liquid diagnostics at start/end of sweep
    {
        diag.liquidStartHasLevel = liqStart.hasLevel;
        diag.liquidStartLevel = liqStart.level;
        diag.liquidStartType = liqStart.type;
        diag.liquidStartFromVmap = liqStart.fromVmap;
        diag.liquidStartSwimming = liqStart.isSwimming;

        LiquidInfo liqEnd = noInput ? liqStart : EvaluateLiquidAt(mapId, endX, endY, z);
        diag.liquidEndHasLevel = liqEnd.hasLevel;
        diag.liquidEndLevel = liqEnd.level;
        diag.liquidEndType = liqEnd.type;
        diag.liquidEndFromVmap = liqEnd.fromVmap;
        diag.liquidEndSwimming = liqEnd.isSwimming;
    }

    // Evaluate ADT triangles against the diagnostic capsule and append penetrating hits
    std::vector<SceneHit> adtHits;
    if (!triBuf.empty() && !startSwimming)
    {
        CapsuleCollision::Capsule Cw; Cw.p0 = { x, y, capBottom }; Cw.p1 = { x, y, capTop }; Cw.r = r;
        for (size_t tIdx = 0; tIdx < triBuf.size(); ++tIdx)
        {
            const auto& tw = triBuf[tIdx];
            CapsuleCollision::Triangle Tterrain; Tterrain.a = { tw.ax, tw.ay, tw.az }; Tterrain.b = { tw.bx, tw.by, tw.bz }; Tterrain.c = { tw.cx, tw.cy, tw.cz }; Tterrain.doubleSided = false; Tterrain.collisionMask = 0xFFFFFFFFu;
            CapsuleCollision::Hit chW;
            if (CapsuleCollision::intersectCapsuleTriangle(Cw, Tterrain, chW))
            {
                G3D::Vector3 wA(tw.ax, tw.ay, tw.az), wB(tw.bx, tw.by, tw.bz), wC(tw.cx, tw.cy, tw.cz);
                G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
                if (wN.z < 0.0f) wN = -wN;
                G3D::Vector3 wPoint(chW.point.x, chW.point.y, chW.point.z);
                SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = wN; h.point = wPoint; h.triIndex = (int)tIdx; h.instanceId = 0; h.startPenetrating = true; h.penetrationDepth = chW.depth; h.normalFlipped = false;
                adtHits.push_back(h);
            }
        }
    }
    {
        diag.adtPenetratingHitCount = adtHits.size();
        float aMinZ = FLT_MAX, aMaxZ = -FLT_MAX;
        for (const auto& h : adtHits) { aMinZ = std::min(aMinZ, h.point.z); aMaxZ = std::max(aMaxZ, h.point.z); }
        if (diag.adtPenetratingHitCount == 0) { aMinZ = 0.0f; aMaxZ = 0.0f; }
        diag.adtHitMinZ = aMinZ; diag.adtHitMaxZ = aMaxZ;
    }

    // Combined hits for aggregate stats (skip if swimming to avoid ground resolution)
    std::vector<SceneHit> sweepHits;
    if (!startSwimming) {
        sweepHits.reserve(vmapHits.size() + adtHits.size());
        sweepHits.insert(sweepHits.end(), vmapHits.begin(), vmapHits.end());
        sweepHits.insert(sweepHits.end(), adtHits.begin(), adtHits.end());
    }

    std::vector<SceneHit> orderedNonPen;
    std::vector<SceneHit> orderedPen;
    if (!startSwimming) {
        orderedNonPen.reserve(sweepHits.size());
        orderedPen.reserve(sweepHits.size());
        for (const auto& h : sweepHits) {
            if (h.startPenetrating) orderedPen.push_back(h); else orderedNonPen.push_back(h);
        }
        if (!noInput) {
            std::stable_sort(orderedNonPen.begin(), orderedNonPen.end(), [](const SceneHit& a, const SceneHit& b) { return a.time < b.time; });
        } else {
            orderedNonPen.clear();
        }
    }

    // Combined stats for sweepHits
    if (!startSwimming) {
        diag.hitCount = sweepHits.size();
        float hitMinZ = FLT_MAX, hitMaxZ = -FLT_MAX;
        size_t penCount = 0, nonPenCount = 0, walkableNP = 0; float earliestNP = FLT_MAX; std::set<uint32_t> uniqueInst;
        for (const auto& hHit : sweepHits) {
            if (hHit.startPenetrating) penCount++; else nonPenCount++;
            if (!hHit.startPenetrating) {
                earliestNP = std::min(earliestNP, hHit.distance);
                if (hHit.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) walkableNP++;
            }
            hitMinZ = std::min(hitMinZ, hHit.point.z);
            hitMaxZ = std::max(hitMaxZ, hHit.point.z);
            uniqueInst.insert(hHit.instanceId);
        }
        if (earliestNP == FLT_MAX) earliestNP = -1.0f;
        if (diag.hitCount == 0) { hitMinZ = 0.0f; hitMaxZ = 0.0f; }
        diag.penCount = penCount;
        diag.nonPenCount = nonPenCount;
        diag.walkableNonPen = walkableNP;
        diag.earliestNonPen = earliestNP;
        diag.hitMinZ = hitMinZ;
        diag.hitMaxZ = hitMaxZ;
        diag.uniqueInstanceCount = uniqueInst.size();
    } else {
        // Swimming: clear combined stats
        diag.hitCount = 0;
        diag.penCount = 0;
        diag.nonPenCount = 0;
        diag.walkableNonPen = 0;
        diag.earliestNonPen = -1.0f;
        diag.hitMinZ = 0.0f;
        diag.hitMaxZ = 0.0f;
        diag.uniqueInstanceCount = 0;
    }

    // Continuous collision detection & depenetration
    {
        float minTOI = FLT_MAX;
        G3D::Vector3 depen(0,0,0);
        float maxPenDepth = 0.0f;
        if (!startSwimming) {
            for (const auto& h : sweepHits) {
                if (!h.startPenetrating) {
                    if (!noInput) {
                        minTOI = std::min(minTOI, h.time);
                    }
                } else {
                    float d = std::max(0.0f, h.penetrationDepth);
                    depen += h.normal.directionOrZero() * d;
                    if (d > maxPenDepth) maxPenDepth = d;
                }
            }
        }
        if (minTOI == FLT_MAX) minTOI = -1.0f;
        diag.minTOI = minTOI;
        diag.depenetration = depen;
        diag.depenetrationMagnitude = depen.magnitude();
        const float baseSkin = std::max(0.001f, r * 0.02f);
        const float depthSkin = (maxPenDepth > 0.0f) ? std::min(maxPenDepth * 0.5f, r * 0.25f) : 0.0f;
        diag.suggestedSkinWidth = baseSkin + depthSkin;
    }

    if (!noInput) {
        std::stable_sort(vmapHits.begin(), vmapHits.end(), [](const SceneHit& a, const SceneHit& b) {
            if (a.startPenetrating != b.startPenetrating)
                return a.startPenetrating > b.startPenetrating;
            return a.distance < b.distance;
        });
    }

    // Build movement manifold from collective triangle hits (VMAP preferred, ADT supplemental)
    auto accumulatePlanes = [&](const std::vector<SceneHit>& hits, SweepDiagnostics::StandSource src) {
        for (const auto& h : hits) {
            SweepDiagnostics::ContactPlane cp;
            cp.normal = h.normal.directionOrZero();
            if (cp.normal.magnitude() <= 1e-5f) cp.normal = G3D::Vector3(0,0,1);
            cp.point = h.point;
            cp.walkable = (cp.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z);
            cp.penetrating = h.startPenetrating;
            cp.source = src;
            diag.planes.push_back(cp);
            if (cp.walkable) diag.walkablePlanes.push_back(cp);
        }
    };
    if (!startSwimming) {
        accumulatePlanes(vmapHits, SweepDiagnostics::StandSource::VMAP);
        accumulatePlanes(adtHits, SweepDiagnostics::StandSource::ADT);
    }

    // Deduplicate nearly-coplanar planes (by normal/point within epsilon)
    auto approximatelyEqual = [](float a, float b, float eps) { return std::fabs(a-b) <= eps; };
    auto normalsClose = [&](const G3D::Vector3& n0, const G3D::Vector3& n1, float epsN) {
        return approximatelyEqual(n0.x, n1.x, epsN) && approximatelyEqual(n0.y, n1.y, epsN) && approximatelyEqual(n0.z, n1.z, epsN);
    };
    const float skin = (diag.suggestedSkinWidth > 0.0f ? diag.suggestedSkinWidth : std::max(0.001f, r * 0.02f));
    const float base = std::max(0.001f, r * 0.01f);
    const float normalEps = 1e-3f;
    const float pointZEps = std::max(1e-3f, skin * 0.5f);
    const float pointXYEps = std::max(1e-3f, base * 0.5f);
    std::vector<SweepDiagnostics::ContactPlane> dedup;
    dedup.reserve(diag.planes.size());
    for (const auto& cp : diag.planes) {
        bool found = false;
        for (auto& d : dedup) {
            if (normalsClose(cp.normal, d.normal, normalEps)) {
                float dx = std::fabs(cp.point.x - d.point.x);
                float dy = std::fabs(cp.point.y - d.point.y);
                float dz = std::fabs(cp.point.z - d.point.z);
                if (dx <= pointXYEps && dy <= pointXYEps && dz <= pointZEps) {
                    d.walkable = d.walkable || cp.walkable;
                    d.penetrating = d.penetrating || cp.penetrating;
                    found = true; break;
                }
            }
        }
        if (!found) dedup.push_back(cp);
    }
    diag.planes = dedup;
    diag.walkablePlanes.clear();
    for (const auto& cp : diag.planes) if (cp.walkable) diag.walkablePlanes.push_back(cp);

    // Choose a primary plane following ProcessGroundMovement preferences
    auto choosePrimary = [&]() {
        if (startSwimming) { diag.hasPrimaryPlane = false; return; }
        for (const auto& cp : diag.planes) {
            if (cp.penetrating && cp.walkable) { diag.primaryPlane = cp; diag.hasPrimaryPlane = true; return; }
        }
        if (!noInput) {
            for (const auto& cp : diag.planes) {
                if (!cp.penetrating && cp.walkable) { diag.primaryPlane = cp; diag.hasPrimaryPlane = true; return; }
            }
            for (const auto& cp : diag.planes) {
                if (cp.walkable) { diag.primaryPlane = cp; diag.hasPrimaryPlane = true; return; }
            }
        }
        float bestZ = -FLT_MAX; size_t bestIdx = (size_t)-1;
        for (size_t i = 0; i < diag.planes.size(); ++i) {
            const auto& cp = diag.planes[i];
            if (cp.penetrating && cp.point.z > bestZ) { bestZ = cp.point.z; bestIdx = i; }
        }
        if (bestIdx != (size_t)-1) { diag.primaryPlane = diag.planes[bestIdx]; diag.hasPrimaryPlane = true; }
    };
    choosePrimary();

    // Compute slide direction only if there is input
    diag.slideDirValid = false;
    diag.hasIntersectionLine = false;
    if (diag.hasPrimaryPlane) {
        G3D::Vector3 n = diag.primaryPlane.normal.directionOrZero();
        float dPlane = -(n.dot(diag.primaryPlane.point));
        float clampZSurface = z;
        if (std::fabs(n.z) > 1e-6f) {
            clampZSurface = (-dPlane - n.x * x - n.y * y) / n.z;
        }
        // Previously: subtract capsule radius to compute stand Z. Diagnostics should reflect plane Z directly.
        float standZNoPen = clampZSurface;
        if (!diag.standFound || diag.primaryPlane.penetrating) {
            diag.standFound = true;
            diag.standZ = standZNoPen;
            diag.standSource = diag.primaryPlane.source;
        }
    }

    if (!noInput && diag.hasPrimaryPlane) {
        G3D::Vector3 n0 = diag.primaryPlane.normal.directionOrZero();
        G3D::Vector3 mv = moveDir.magnitude() > 1e-6f ? (moveDir * (1.0f / moveDir.magnitude())) : G3D::Vector3(1,0,0);
        const SweepDiagnostics::ContactPlane* secondary = nullptr;
        for (const auto& cp : diag.walkablePlanes) {
            G3D::Vector3 n1 = cp.normal.directionOrZero();
            float dotN = std::fabs(n0.dot(n1));
            if (dotN < 0.995f) { secondary = &cp; break; }
        }
        if (secondary) {
            G3D::Vector3 n1 = secondary->normal.directionOrZero();
            G3D::Vector3 lineDir = n0.cross(n1).directionOrZero();
            if (lineDir.magnitude() > 1e-6f) {
                diag.intersectionLineDir = lineDir;
                diag.hasIntersectionLine = true;
                G3D::Vector3 slide = (lineDir * (mv.dot(lineDir))).directionOrZero();
                diag.slideDir = slide;
                diag.slideDirValid = slide.magnitude() > 1e-6f;
            }
        }
        if (!diag.slideDirValid) {
            G3D::Vector3 slide = (mv - n0 * mv.dot(n0)).directionOrZero();
            diag.slideDir = slide;
            diag.slideDirValid = slide.magnitude() > 1e-6f;
        }
    }

    float horizReduction = 1.0f;
    float suggestedXYDist = intendedDist;
    if (!noInput && diag.slideDirValid) {
        G3D::Vector3 s = diag.slideDir.directionOrZero();
        float sMag = s.magnitude();
        if (sMag > 1e-6f) {
            float horizLen = std::sqrt(s.x * s.x + s.y * s.y);
            float ratio = (sMag > 0.0f) ? (horizLen / sMag) : 1.0f;
            horizReduction = ratio;
            suggestedXYDist = intendedDist * horizReduction;
        }
    }
    diag.xyReduction = horizReduction;
    diag.suggestedXYDist = suggestedXYDist;
    diag.constraintIterations = 3;
    diag.slopeClampThresholdZ = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;

    // If swimming, clear stand selection to avoid snapping to ground while in liquid
    if (startSwimming) {
        diag.standFound = false;
        diag.standZ = z; // keep current z; Step will handle swim motion
        diag.standSource = SweepDiagnostics::StandSource::None;
        diag.hasPrimaryPlane = false;
        diag.slideDirValid = false;
        diag.hasIntersectionLine = false;
    }

    // Consolidated end-of-diagnostics log
    {
        std::ostringstream oss;
        oss << "[SweepDiag] Combined\n"
            << "  map=" << mapId << " pos=(" << x << "," << y << "," << z << ") r=" << r << " h=" << h << "\n"
            << "  moveDir=(" << moveDir.x << "," << moveDir.y << "," << moveDir.z << ") dist=" << intendedDist << "\n"
            << "  counts: vmap=" << diag.vmapHitCount << " adtPen=" << diag.adtPenetratingHitCount << " sweepCombined=" << diag.hitCount << "\n"
            << "  ordered: pen=" << diag.penCount << " nonPen=" << diag.nonPenCount << "\n"
            << "  VMAP OverlapHits: nonPen=" << diag.vmapNonPenCount << " pen=" << diag.vmapPenCount
            << " earliestNP=" << diag.vmapEarliestNonPen << " zRange=[" << diag.vmapHitMinZ << "," << diag.vmapHitMaxZ << "] walkableNP=" << diag.vmapWalkableNonPen << " instances=" << diag.vmapUniqueInstanceCount << "\n"
            << "  ADT Triangles: count=" << diag.terrainTriCount << " zRange=[" << diag.terrainMinZ << "," << diag.terrainMaxZ << "]"
            << "  ADT OverlapHits: count=" << diag.adtPenetratingHitCount << " zRange=[" << diag.adtHitMinZ << "," << diag.adtHitMaxZ << "]\n"
            << "  Selection: standFound=" << (diag.standFound ? 1 : 0) << " standZ=" << diag.standZ
            << " source=" << (diag.standSource == SweepDiagnostics::StandSource::VMAP ? "VMAP" : diag.standSource == SweepDiagnostics::StandSource::ADT ? "ADT" : "None") << "\n"
            << "  Manifold: planes=" << diag.planes.size() << " walkable=" << diag.walkablePlanes.size() << " hasPrimary=" << (diag.hasPrimaryPlane ? 1 : 0);
        if (diag.hasPrimaryPlane) {
            oss << " primaryN=(" << diag.primaryPlane.normal.x << "," << diag.primaryPlane.normal.y << "," << diag.primaryPlane.normal.z << ")"
                << " primaryP=(" << diag.primaryPlane.point.x << "," << diag.primaryPlane.point.y << "," << diag.primaryPlane.point.z << ")"
                << " walkable=" << (diag.primaryPlane.walkable ? 1 : 0) << " penetrating=" << (diag.primaryPlane.penetrating ? 1 : 0);
        }
        oss << "\n"
            << "    slideDirValid=" << (diag.slideDirValid ? 1 : 0) << " slideDir=(" << diag.slideDir.x << "," << diag.slideDir.y << "," << diag.slideDir.z << ")"
            << " horizReduction=" << diag.xyReduction << " suggestedXYDist=" << diag.suggestedXYDist
            << " minTOI=" << diag.minTOI << " depenMag=" << diag.depenetrationMagnitude << " skin=" << diag.suggestedSkinWidth;
        {
            const char* lStartName = VMAP::GetLiquidTypeName(diag.liquidStartType);
            LiquidInfo liqEnd = noInput ? liqStart : EvaluateLiquidAt(mapId, endX, endY, z);
            const char* lEndName = VMAP::GetLiquidTypeName(diag.liquidEndType);
            oss << "\n"
                << "  Liquid: start has=" << (diag.liquidStartHasLevel?1:0)
                << " z=" << diag.liquidStartLevel
                << " type=" << lStartName
                << " src=" << (diag.liquidStartFromVmap?"VMAP":"ADT")
                << " swim=" << (diag.liquidStartSwimming?1:0)
                << " | end has=" << (diag.liquidEndHasLevel?1:0)
                << " z=" << diag.liquidEndLevel
                << " type=" << lEndName
                << " src=" << (diag.liquidEndFromVmap?"VMAP":"ADT")
                << " swim=" << (diag.liquidEndSwimming?1:0);
        }
        {
            G3D::Vector3 finalPos(x, y, (!startSwimming && diag.standFound) ? diag.standZ : z);
            if (!startSwimming && diag.slideDirValid && diag.suggestedXYDist > 0.0f) {
                G3D::Vector3 s = diag.slideDir.directionOrZero();
                finalPos.x += s.x * diag.suggestedXYDist;
                finalPos.y += s.y * diag.suggestedXYDist;
            }
            if (!startSwimming && diag.hasPrimaryPlane) {
                G3D::Vector3 n = diag.primaryPlane.normal.directionOrZero();
                float dPlane = -(n.dot(diag.primaryPlane.point));
                float clampZAtNewXY = finalPos.z;
                if (std::fabs(n.z) > 1e-6f) {
                    clampZAtNewXY = (-dPlane - n.x * finalPos.x - n.y * finalPos.y) / n.z;
                }
                // Diagnostics: report final Z at plane height, no radius subtraction
                finalPos.z = clampZAtNewXY;
            }
            oss << "\n" << "  FinalPos: (" << finalPos.x << "," << finalPos.y << "," << finalPos.z << ")";
            G3D::Vector3 intendedVel = moveDir * intendedDist;
            G3D::Vector3 endingVel(0,0,0);
            if (!startSwimming && diag.slideDirValid) {
                G3D::Vector3 s = diag.slideDir.directionOrZero();
                endingVel = s * diag.suggestedXYDist;
            }
            G3D::Vector3 overallVel = endingVel;
            oss << "\n" << "  Velocities: intended=(" << intendedVel.x << "," << intendedVel.y << "," << intendedVel.z
                << ") ending=(" << endingVel.x << "," << endingVel.y << "," << endingVel.z
                << ") overall=(" << overallVel.x << "," << overallVel.y << "," << overallVel.z << ")";
        }
        PHYS_INFO(PHYS_SURF, oss.str());
    }

    return diag;
}