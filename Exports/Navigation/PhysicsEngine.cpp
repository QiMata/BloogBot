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
	PHYS_INFO(PHYS_MOVE, "[GroundMove] Start pos=" << st.x << "," << st.y << "," << st.z << " vel=" << st.vx << "," << st.vy << " dt=" << dt);

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

		// Prefer the earliest non-penetrating walkable surface
		const SceneHit* bestNP = nullptr;
		for (size_t i = 0; i < downHits.size(); ++i) {
			const auto& h = downHits[i];
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownHit idx=" << i << " startPen=" << (h.startPenetrating?1:0) << " dist=" << h.distance << " nZ=" << h.normal.z << " pZ=" << h.point.z);
			if (h.startPenetrating) { PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownReject idx=" << i << " reason=StartPenetrating"); continue; }
			if (h.normal.z < walkableCosMin) { PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownReject idx=" << i << " reason=Unwalkable nZ=" << h.normal.z); continue; }
			bestNP = &h;
			break;
		}

		// Otherwise, if penetrating at start, choose highest up-facing contact
		const SceneHit* bestPen = nullptr; float bestPenZ = -FLT_MAX;
		for (size_t i = 0; i < downHits.size(); ++i) {
			const auto& h = downHits[i];
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownHitPen idx=" << i << " startPen=" << (h.startPenetrating?1:0) << " nZ=" << h.normal.z << " pZ=" << h.point.z);
			if (!h.startPenetrating) continue;
			if (h.normal.z < 0.0f) { PHYS_TRACE(PHYS_MOVE, "[GroundMove] DownReject idx=" << i << " reason=PenetratingDownFacing"); continue; }
			if (h.point.z > bestPenZ) { bestPenZ = h.point.z; bestPen = &h; }
		}
		if (bestPen) {
			st.z = bestPen->point.z;
			st.isGrounded = true;
			st.groundNormal = bestPen->normal.directionOrZero();
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Settle result: Penetrating adjust to z=" << st.z << " nZ=" << st.groundNormal.z);
			return;
		}

		// ADT terrain fallback
		float adtZ = GetTerrainHeight(input.mapId, st.x, st.y);
		if (adtZ > PhysicsConstants::INVALID_HEIGHT) {
			st.z = adtZ;
			st.isGrounded = true;
			st.groundNormal = G3D::Vector3(0, 0, 1);
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Settle result: ADT fallback z=" << st.z);
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
	PHYS_INFO(PHYS_MOVE, "Intent input vx=" << st.vx << " vy=" << st.vy);

	G3D::Vector3 moveDir(intent.dir.x, intent.dir.y, 0.0f);
	float intendedDist = std::sqrt(st.vx * st.vx + st.vy * st.vy) * dt;
	PHYS_INFO(PHYS_MOVE, "intendedDist=" << intendedDist);
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
	PHYS_INFO(PHYS_MOVE, "[GroundMove] SweepCapsuleAll count=" << hits.size());

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
		return true;
	};

	if (!hits.empty()) {
		// 1) If overlapping a walkable surface at start, perform a simple slide along its plane.
		const SceneHit& firstHit = hits.front();
		PHYS_INFO(PHYS_MOVE, "[GroundMove] FirstHit tri=" << firstHit.triIndex << " instId=" << firstHit.instanceId << " startPen=" << (firstHit.startPenetrating?1:0) << " dist=" << firstHit.distance << " n=(" << firstHit.normal.x << "," << firstHit.normal.y << "," << firstHit.normal.z << ") p=(" << firstHit.point.x << "," << firstHit.point.y << "," << firstHit.point.z << ")");
		float nZ = firstHit.normal.z; // use signed Z now
		bool walkableStartPen = firstHit.startPenetrating && nZ >= walkableCosMin;
		if (walkableStartPen) {
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=SlideStartPen walkableN=1 nZ=" << nZ);
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
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Slide calc travel=" << travel << " slideDir=(" << slideDir.x << "," << slideDir.y << "," << slideDir.z << ") newXY=(" << newX << "," << newY << ") newZ=" << newZ << " dzSlide=" << dzSlide);
			if (dzSlide > stepUpLimit) newZ = st.z + stepUpLimit; else if (dzSlide < -stepDownLimit) newZ = st.z - stepDownLimit;
			st.x = newX; st.y = newY; st.z = newZ; st.isGrounded = true; st.groundNormal = n; st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Result SlideStartPen pos=(" << st.x << "," << st.y << "," << st.z << ")");
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
			st.rampActive = true;
			st.rampN = rampN; st.rampD = rampD;
			st.rampStart = oldPos; st.rampEnd = steppedPoint; st.rampDir = moveDirN;
			st.rampLength = (steppedPoint - oldPos).dot(moveDirN);
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Result StepUp pos=(" << st.x << "," << st.y << "," << st.z << ") rampActive=1 length=" << st.rampLength);
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
		} else {
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] ObstructionReject reason=UnwalkableNormal nZ=" << hit.normal.z);
			st.vx = st.vy = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundMove] Result Obstruction walkable=0 travel=" << travel << " newPos=(" << st.x << "," << st.y << "," << st.z << ")");
		}
		return;
	}

	// --- No hits: move full horizontal distance and try ADT height fallback ---
	PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=NoHits moveIntendedDist=" << intendedDist);
	st.x += moveDir.x * intendedDist;
	st.y += moveDir.y * intendedDist;
	PHYS_INFO(PHYS_MOVE, "[GroundMove] Result NoHits newXY=(" << st.x << "," << st.y << ")");
	float vmapZ = INVALID_HEIGHT;
	if (m_vmapManager) { vmapZ = INVALID_HEIGHT; }
	float adtZ = GetTerrainHeight(input.mapId, st.x, st.y);
	if (vmapZ > INVALID_HEIGHT)
	{
		PHYS_INFO(PHYS_MOVE, std::string("[GroundMove] VMAP height probed (diagnostic only): z=") << vmapZ);
	}
	float bestZ = st.z; bool found = false;
	if (adtZ > INVALID_HEIGHT) {
		float diff = adtZ - st.z;
		PHYS_TRACE(PHYS_MOVE, "[GroundMove] ADT snap eval diff=" << diff << " stepUpLimit=" << stepUpLimit << " stepDownLimit=" << stepDownLimit);
		if (((diff >= 0.0f && diff <= stepUpLimit) || (diff < 0.0f && diff >= -stepDownLimit))) {
			bestZ = adtZ; found = true; PHYS_INFO(PHYS_MOVE, "[GroundMove] Decision=ADTHeightSnap z=" << adtZ);
		} else {
			PHYS_TRACE(PHYS_MOVE, "[GroundMove] ADT snap reject reason=OutOfRange diff=" << diff);
		}
	}
	if (found) {
		st.z = bestZ;
		st.groundNormal = G3D::Vector3(0, 0, 1);
		st.isGrounded = true;
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Result ADTHeightSnap newZ=" << st.z);
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

	// Query for all liquid types immediately after intent is built
	uint32_t liquidType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
	float liquidLevel = QueryLiquidLevel(input.mapId, st.x, st.y, st.z, liquidType);

	// 2. Query surface and liquid state
	// Capture raw ADT and VMAP liquid levels for diagnostics before merged query
	float adtLiquidLevel = INVALID_HEIGHT; uint32_t adtLiquidType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
	if (m_mapLoader && m_mapLoader->IsInitialized()) {
		adtLiquidLevel = m_mapLoader->GetLiquidLevel(input.mapId, st.x, st.y);
		if (adtLiquidLevel > INVALID_HEIGHT)
			adtLiquidType = m_mapLoader->GetLiquidType(input.mapId, st.x, st.y);
	}
	float vmapLiquidLevel = INVALID_HEIGHT; uint32_t vmapLiquidType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
	if (m_vmapManager) {
		float level, floor; uint32_t type;
		if (m_vmapManager->GetLiquidLevel(input.mapId, st.x, st.y, st.z + 2.0f, VMAP::MAP_LIQUID_TYPE_ALL_LIQUIDS, level, floor, type)) {
			vmapLiquidLevel = level; vmapLiquidType = type;
		}
	}
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
	PHYS_INFO(PHYS_MOVE, std::string("[Step] WaterDiag posZ=") << st.z
		<< " radius=" << r
		<< " refZ=" << (st.z + r)
		<< " adtTerrainZ=" << adtTerrainZ
		<< " adtWaterLevel=" << adtLiquidLevel << " (type=" << (unsigned)adtLiquidType << ")"
		<< " vmapWaterLevel=" << vmapLiquidLevel << " (type=" << (unsigned)vmapLiquidType << ")"
		<< " chosenWater=" << liquidLevel << " (type=" << (unsigned)liquidType << ")"
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

	// Re-query liquid at the final position to report current standing liquid type/level
	uint32_t finalLiquidType = VMAP::MAP_LIQUID_TYPE_NO_WATER;
	float finalLiquidLevel = QueryLiquidLevel(input.mapId, st.x, st.y, st.z, finalLiquidType);

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
	// Provide liquid diagnostics (from final position)
	out.liquidZ = finalLiquidLevel;
	out.liquidType = finalLiquidType;
	// Initialize ground identification defaults
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