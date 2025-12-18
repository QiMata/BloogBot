// PhysicsEngine.cpp - Simplified physics tuned toward vanilla 1.12.1 feel

#include "PhysicsEngine.h"
#include "Navigation.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "ModelInstance.h"     // for debug diagnostics on model collisions
#include "CapsuleCollision.h"  // added for debug distance computation
#include "PhysicsBridge.h"     // ensure movement flag constants (added for swimming flag update)
#include "VMapDefinitions.h"   // for GetLiquidNameUnified
#include "PhysicsHelpers.h"    // pure helpers for intent
#include "PhysicsLiquidHelpers.h" // for liquid evaluation
#include "PhysicsDiagnosticsHelpers.h"   // pure diagnostics helpers
#include "PhysicsShapeHelpers.h"         // small capsule builders
#include "PhysicsSelectHelpers.h"         // hit selection helpers
#include "SceneQuery.h"
#include "PhysicsTolerances.h"

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

// =====================================================================================
// Singleton
// =====================================================================================
PhysicsEngine* PhysicsEngine::s_instance = nullptr;

PhysicsEngine::PhysicsEngine()
	: m_initialized(false) {
}

bool PhysicsEngine::PerformVerticalPlacementOrFall(const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float r,
    float h,
    float dt,
    float moveSpeed,
    const char* reasonLog)
{
    if (reasonLog && *reasonLog)
        PHYS_INFO(PHYS_MOVE, std::string("[StepV2] VerticalPlacement: ") << reasonLog);

    bool snapped = TryDownwardStepSnap(input, st, r, h);
    if (!snapped) {
        st.isGrounded = false;
        if (st.vz >= 0.0f) st.vz = -0.1f;
        PHYS_INFO(PHYS_MOVE, std::string("[StepV2] VerticalPlacement: no ground; start falling vz=") << st.vz);
        ProcessAirMovement(input, intent, st, dt, moveSpeed);
        return false;
    }
    return true;
}

void PhysicsEngine::GroundMoveElevatedSweep(const PhysicsInput& input,
    const SceneQuery::SweepResults& diag,
    const MovementIntent& intent,
    MovementState& st,
    float r,
    float h,
    const G3D::Vector3& moveDir,
    float intendedDist,
    float dt,
    float moveSpeed)
{
    PHYS_INFO(PHYS_MOVE, "[StepV2] Path=GROUND (elevated horizontal sweep + downward probe)");

    const float stepOffset = PhysicsConstants::STEP_HEIGHT;
    const float snapDownDistance = PhysicsConstants::STEP_DOWN_HEIGHT;
    const float skin = diag.suggestedSkinWidth > 0.0f ? diag.suggestedSkinWidth : PhysicsTol::BaseSkin(r);

    G3D::Vector3 dirN = moveDir.directionOrZero();
    dirN.z = 0.0f;
    float dist = intendedDist;
    if (dirN.magnitude() <= 1e-6f || dist <= 1e-6f) {
        PerformVerticalPlacementOrFall(input, intent, st, r, h, dt, moveSpeed, "ground path: no horizontal movement");
        return;
    }

    // Diagnostic: log facing vs primary plane normal if present
    if (diag.hasPrimaryPlane) {
        G3D::Vector3 n = diag.primaryPlane.normal.directionOrZero();
        float d = dirN.dot(n);
        float dc = (d < -1.0f) ? -1.0f : (d > 1.0f ? 1.0f : d);
        float angleRad = std::acos(dc);
        float angleDeg = angleRad * (180.0f / 3.14159265358979323846f);
        std::ostringstream oss;
        oss << "[StepV2] PrimaryPlaneFacing walkable=" << (diag.primaryPlane.walkable ? 1 : 0)
            << " n=(" << n.x << "," << n.y << "," << n.z << ")"
            << " dir=(" << dirN.x << "," << dirN.y << "," << dirN.z << ")"
            << " dot=" << d << " angleDeg=" << angleDeg;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }

    // Diagnostic: dump manifold plane normals and walkable flags
    {
        std::ostringstream oss;
        oss << "[StepV2] ManifoldPlanes count=" << diag.planes.size() << " walkableCount=" << diag.walkablePlanes.size();
        for (size_t i = 0; i < diag.planes.size() && i < 8; ++i) {
            const auto& p = diag.planes[i];
            oss << "\n  plane[" << i << "] n=(" << p.normal.x << "," << p.normal.y << "," << p.normal.z << ") walkable=" << (p.walkable?1:0);
        }
        PHYS_INFO(PHYS_MOVE, oss.str());
    }

    // If the diagnostic primary plane is unwalkable and we are heading into it head-on,
    // negate horizontal movement entirely for this step (treat as solid barrier).
    if (diag.hasPrimaryPlane && !diag.primaryPlane.walkable) {
        const float headOnDotThresh = -0.70710678f; // cos(135deg)
        float facing = dirN.dot(diag.primaryPlane.normal.directionOrZero());
        if (facing <= headOnDotThresh) {
            PHYS_INFO(PHYS_MOVE, "[StepV2] Blocked: head-on into unwalkable surface; negating horizontal movement");
            // Attempt to remain grounded via a vertical placement; otherwise begin falling.
            if (!TryDownwardStepSnap(input, st, r, h)) {
                st.isGrounded = false;
                if (st.vz >= 0.0f) st.vz = -0.1f;
                ProcessAirMovement(input, intent, st, dt, moveSpeed);
            }
            // Zero horizontal velocity on block
            st.vx = 0.0f; st.vy = 0.0f;
            return;
        }
    }

    // Additional blocking: if any unwalkable contact plane is encountered head-on (using horizontal normal), block movement
    {
        const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
        const float headOnHorizDotThresh = -0.5f; // cos ~120deg for horizontal component
        bool blockedByAny = false; G3D::Vector3 blockN;
        for (const auto& p : diag.planes) {
            if (p.walkable) continue; // only consider unwalkable planes
            G3D::Vector3 n = p.normal.directionOrZero();
            // Project normal to horizontal to evaluate approach angle independent of slope
            G3D::Vector3 nHoriz(n.x, n.y, 0.0f);
            nHoriz = nHoriz.directionOrZero();
            if (nHoriz.magnitude() <= 1e-6f) continue;
            float d = dirN.dot(nHoriz);
            if (d <= headOnHorizDotThresh) { blockedByAny = true; blockN = n; break; }
        }
        if (blockedByAny) {
            std::ostringstream oss; oss << "[StepV2] BlockedByAny: head-on into unwalkable plane n=(" << blockN.x << "," << blockN.y << "," << blockN.z << ")";
            PHYS_INFO(PHYS_MOVE, oss.str());
            if (!TryDownwardStepSnap(input, st, r, h)) {
                st.isGrounded = false;
                if (st.vz >= 0.0f) st.vz = -0.1f;
                ProcessAirMovement(input, intent, st, dt, moveSpeed);
            }
            st.vx = 0.0f; st.vy = 0.0f;
            return;
        }
    }

    // Build capsule at elevated Z
    CapsuleCollision::Capsule capStart = PhysShapes::BuildFullHeightCapsule(
        st.x, st.y, st.z + stepOffset, r, h);

    // Sweep horizontally
    std::vector<SceneHit> hHits;
    SceneQuery::SweepCapsule(input.mapId, capStart, dirN, dist, hHits);

    // Find earliest non-penetrating hit
    const SceneHit* earliest = nullptr;
    float minDist = FLT_MAX;
    for (const auto& hh : hHits) {
        if (!hh.hit || hh.startPenetrating) continue;
        if (hh.distance < 1e-6f) continue;
        if (hh.distance < minDist) { minDist = hh.distance; earliest = &hh; }
    }

    float advance = dist;
    float angleScale = 1.0f;
    if (earliest) advance = std::max(0.0f, minDist - skin);

    // Angle-based reduction when encountering non-walkable surfaces: find the most opposing unwalkable plane
    {
        float worstDot = 1.0f; G3D::Vector3 worstN(0,0,0);
        for (const auto& p : diag.planes) {
            if (p.walkable) continue;
            G3D::Vector3 nH(p.normal.x, p.normal.y, 0.0f);
            nH = nH.directionOrZero();
            if (nH.magnitude() <= 1e-6f) continue;
            float d = dirN.dot(nH); // [-1,1], negative = head-on
            if (d < worstDot) { worstDot = d; worstN = p.normal.directionOrZero(); }
        }
        if (worstDot < 0.0f) {
            // Scale advance: s = d + 1 maps [-1,0] -> [0,1]
            float scale = std::max(0.0f, worstDot + 1.0f);
            angleScale = scale;
            float angleDeg = std::acos(std::max(-1.0f, std::min(1.0f, worstDot))) * (180.0f / 3.14159265358979323846f);
            std::ostringstream oss; oss << "[StepV2] NonWalkableAngleReduce dotH=" << worstDot << " angleDeg=" << angleDeg << " scale=" << scale;
            PHYS_INFO(PHYS_MOVE, oss.str());
            advance *= scale;
        }
    }

    // Advance horizontally at original Z
    st.x += dirN.x * advance;
    st.y += dirN.y * advance;

    // Downward probe from elevated origin
    CapsuleCollision::Capsule capProbe = PhysShapes::BuildFullHeightCapsule(
        st.x, st.y, st.z + stepOffset, r, h);
    std::vector<SceneHit> downHits;
    SceneQuery::SweepCapsule(input.mapId, capProbe, G3D::Vector3(0,0,-1), stepOffset + snapDownDistance, downHits);

    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    const SceneHit* bestNP = PhysSelect::FindEarliestWalkableNonPen(downHits, walkableCosMin);

    bool snapped = false;
    if (bestNP) {
        float targetZ = bestNP->point.z;
        float dz = targetZ - st.z;
        // Z smoothing
        const float smoothWindow = 0.08f; // seconds
        float alpha = (smoothWindow > 0.0f) ? std::min(1.0f, dt / smoothWindow) : 1.0f;
        if (std::fabs(dz) > (stepOffset + 0.01f)) st.z = targetZ; else st.z = st.z + dz * alpha;
        st.isGrounded = true; st.vz = 0.0f; st.groundNormal = bestNP->normal.directionOrZero();
        snapped = true;
        PHYS_INFO(PHYS_MOVE, std::string("[StepV2] ElevatedDownProbe targetZ=") << targetZ << " smoothedZ=" << st.z);
    } else if (!downHits.empty()) {
        // Fallback: highest upward-facing penetrating contact
        const SceneHit* bestPen = nullptr; float bestZ = -FLT_MAX;
        for (const auto& hh : downHits) {
            if (!hh.startPenetrating) continue;
            if (hh.normal.z < 0.0f) continue;
            if (hh.point.z > bestZ) { bestZ = hh.point.z; bestPen = &hh; }
        }
        if (bestPen) {
            float targetZ = bestPen->point.z;
            float dz = targetZ - st.z;
            const float smoothWindow = 0.08f;
            float alpha = (smoothWindow > 0.0f) ? std::min(1.0f, dt / smoothWindow) : 1.0f;
            if (std::fabs(dz) > (stepOffset + 0.01f)) st.z = targetZ; else st.z = st.z + dz * alpha;
            st.isGrounded = true; st.vz = 0.0f; st.groundNormal = bestPen->normal.directionOrZero();
            snapped = true;
            PHYS_INFO(PHYS_MOVE, std::string("[StepV2] ElevatedDownProbe PenClamp targetZ=") << targetZ << " smoothedZ=" << st.z);
        }
    }

    if (!snapped) {
        // No ground found within range: start falling
        st.isGrounded = false;
        if (st.vz >= 0.0f) st.vz = -0.1f;
        PHYS_INFO(PHYS_MOVE, "[StepV2] Elevated sweep found no ground; start falling");
        ProcessAirMovement(input, intent, st, dt, moveSpeed);
    } else {
        // Set horizontal velocity along intended direction on ground
        G3D::Vector3 vProj = dirN.directionOrZero() * (moveSpeed * angleScale); vProj.z = 0.0f;
        st.vx = vProj.x; st.vy = vProj.y; st.vz = 0.0f;
    }
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

// MapLoader exposure removed; SceneQuery handles terrain queries directly

// =====================================================================================
// Initialization / Shutdown
// =====================================================================================
void PhysicsEngine::Initialize()
{
	if (m_initialized)
		return;

	SceneQuery::Initialize();

	m_initialized = true;
	PHYS_INFO(PHYS_MOVE, "Initialize done");
}

void PhysicsEngine::Shutdown()
{
	PHYS_INFO(PHYS_MOVE, "Shutdown");
	m_initialized = false;
}

// =====================================================================================
// Movement helpers
// =====================================================================================
PhysicsEngine::MovementIntent PhysicsEngine::BuildMovementIntent(const PhysicsInput& input, float orientation) const
{
	// Delegate to pure helper to compute directional intent and jump flag.
	auto pure = PhysicsHelpers::BuildMovementIntent(input.moveFlags, orientation);
	MovementIntent intent{};
	intent.dir = pure.dir;
	intent.hasInput = pure.hasInput;
	intent.jumpRequested = pure.jumpRequested;
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

bool PhysicsEngine::TryDownwardStepSnap(const PhysicsInput& input,
	MovementState& st,
	float r,
	float h)
{
	bool snapped = false;
	{
		CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
		PHYS_INFO(PHYS_MOVE, std::string("[StepV2] DownwardSweepCapsule ")
			<< "p0=(" << cap.p0.x << "," << cap.p0.y << "," << cap.p0.z << ") p1=(" << cap.p1.x << "," << cap.p1.y << "," << cap.p1.z << ") r=" << cap.r
			<< " fullHeightSegLen=" << (cap.p1.z - cap.p0.z));
		G3D::Vector3 downDir(0, 0, -1);
		float settleDist = std::max(3.0f, h + 2.0f);
		std::vector<SceneHit> downHits;
		SceneQuery::SweepCapsule(input.mapId, cap, downDir, settleDist, downHits);
		const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
		const float stepDownLimit = PhysicsConstants::STEP_DOWN_HEIGHT;
		PHYS_INFO(PHYS_MOVE, std::string("[StepV2] DownwardSweep hits=") << downHits.size() << " dist=" << settleDist);

		const SceneHit* bestNP = PhysSelect::FindEarliestWalkableNonPen(downHits, walkableCosMin);
		if (bestNP) {
			float dz = bestNP->point.z - st.z;
			if (dz <= 0.0f && -dz <= stepDownLimit + 1e-4f) {
				float snapZ = bestNP->point.z;
				st.z = snapZ;
				st.isGrounded = true;
				st.vz = 0.0f;
				st.groundNormal = bestNP->normal.directionOrZero();
				snapped = true;
				PHYS_INFO(PHYS_MOVE, std::string("[StepV2] StepDown snap z=") << st.z << " nZ=" << st.groundNormal.z);
			}
			else {
				PHYS_INFO(PHYS_MOVE, std::string("[StepV2] StepDown reject dz=") << dz << " limit=" << stepDownLimit);
			}
		}
		else {
			PHYS_INFO(PHYS_MOVE, "[StepV2] StepDown no walkable non-penetrating hit");
		}

		if (!snapped) {
			// Fallback: allow clamping to highest upward-facing penetrating contact to remain grounded.
			const SceneHit* bestPen = nullptr; float bestZ = -FLT_MAX;
			for (const auto& hhit : downHits) {
				if (!hhit.startPenetrating) continue;
				if (hhit.normal.z < walkableCosMin) continue; // only consider upward faces within walkable slope
				// Prefer the highest contact near current position
				if (hhit.point.z > bestZ) { bestZ = hhit.point.z; bestPen = &hhit; }
			}
			if (bestPen) {
				st.z = bestPen->point.z;
				st.isGrounded = true;
				st.vz = 0.0f;
				st.groundNormal = bestPen->normal.directionOrZero();
				snapped = true;
				PHYS_INFO(PHYS_MOVE, std::string("[StepV2] StepDown PenetratingClamp z=") << st.z << " nZ=" << st.groundNormal.z);
			} else {
				PHYS_INFO(PHYS_MOVE, "[StepV2] StepDown penetrating contacts present but snap is disallowed; will fall");
			}
		}
	}
	return snapped;
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
	// Predict next position
	G3D::Vector3 startPos(st.x, st.y, st.z);
	G3D::Vector3 endPos = startPos + G3D::Vector3(st.vx * dt, st.vy * dt, st.vz * dt);
	// Update horizontal now; vertical may be clamped by collision below
	st.x = endPos.x;
	st.y = endPos.y;
	st.z = endPos.z;

	// Continuous collision: prevent tunneling through ground when falling
	{
		const float r = input.radius;
		const float h = input.height;
		const float stepDownLimit = PhysicsConstants::STEP_DOWN_HEIGHT;
		// Build a full-height capsule centered on XY using feet Z
		CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(startPos.x, startPos.y, startPos.z, r, h);
		G3D::Vector3 downDir(0, 0, -1);
		float fallDist = std::max(0.0f, startPos.z - endPos.z);
		float sweepDist = fallDist + stepDownLimit; // allow a bit extra to catch ground within range
		std::vector<SceneHit> downHits;
		SceneQuery::SweepCapsule(input.mapId, cap, downDir, sweepDist, downHits);
		PHYS_INFO(PHYS_MOVE, std::string("[Air] DownwardSweep hits=") << downHits.size() << " dist=" << sweepDist);
		const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
		const SceneHit* bestNP = nullptr;
		for (size_t i = 0; i < downHits.size(); ++i) {
			const auto& hhit = downHits[i];
			if (hhit.startPenetrating) continue;
			if (hhit.normal.z < walkableCosMin) continue;
			bestNP = &hhit; break; // earliest acceptable
		}
		if (bestNP) {
			// Check if the hit occurs within our predicted fall distance
			float toiDist = bestNP->distance;
			if (toiDist <= sweepDist + 1e-4f) {
				// Snap just above the surface using skin and stop falling
                const float skin = PhysicsTol::BaseSkin(r);
				st.z = bestNP->point.z + skin;
				st.vz = 0.0f;
				st.isGrounded = true;
				st.groundNormal = bestNP->normal.directionOrZero();
				PHYS_INFO(PHYS_MOVE, std::string("[Air] SnapToGround z=") << st.z << " nZ=" << st.groundNormal.z);
			}
		}
		else if (!downHits.empty()) {
			// Fallback: highest upward-facing penetrating contact within sweep range
			const SceneHit* bestPen = nullptr;
			float bestZ = -FLT_MAX;
			for (const auto& hhit : downHits) {
				if (!hhit.startPenetrating) continue;
				if (hhit.normal.z < 0.0f) continue; // ignore upside-down faces
				if (hhit.distance > sweepDist + 1e-4f) continue;
				if (hhit.point.z > bestZ) { bestZ = hhit.point.z; bestPen = &hhit; }
			}
			if (bestPen) {
                const float skin = PhysicsTol::BaseSkin(r);
				st.z = bestPen->point.z + skin;
				st.vz = 0.0f;
				st.isGrounded = true;
				st.groundNormal = bestPen->normal.directionOrZero();
				PHYS_INFO(PHYS_MOVE, std::string("[Air] PenetratingClamp z=") << st.z << " nZ=" << st.groundNormal.z);
			}
		}
	}
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
// StepV2 entry point
// =====================================================================================
PhysicsOutput PhysicsEngine::StepV2(const PhysicsInput& input, float dt)
{
	// Log input at the beginning
	LogStepInputSummary(input, dt);
    PHYS_INFO(PHYS_MOVE, std::string("[StepV2] Begin dt=") << dt);

	PhysicsOutput out{};
	if (!m_initialized) {
		out.x = input.x;
		out.y = input.y;
		out.z = input.z;
		out.orientation = input.orientation;
		out.pitch = input.pitch;
		out.vx = input.vx;
		out.vy = input.vy;
		out.vz = input.vz;
		out.moveFlags = input.moveFlags;
        PHYS_INFO(PHYS_MOVE, "[StepV2] EarlyExit: engine not initialized");
		return out;
	}

	float r = input.radius;
	float h = input.height;
    PHYS_INFO(PHYS_MOVE, std::string("[StepV2] Params r=") << r << " h=" << h);

	MovementState st{};
	st.x = input.x; st.y = input.y; st.z = input.z;
	st.orientation = input.orientation; st.pitch = input.pitch;
	st.vx = input.vx; st.vy = input.vy; st.vz = input.vz; st.fallTime = input.fallTime;
	st.groundNormal = { 0,0,1 };

	// Track previous position for actual velocity computation
	G3D::Vector3 prevPos(st.x, st.y, st.z);

	MovementIntent intent = BuildMovementIntent(input, st.orientation);

	// Evaluate liquid to decide swim vs ground/air (use SceneQuery directly)
	auto liq = SceneQuery::EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	bool isSwimming = liq.isSwimming;
    PHYS_INFO(PHYS_MOVE, std::string("[StepV2] Liquid isSwimming=") << (isSwimming ? 1 : 0) << " level=" << liq.level << " hasLevel=" << (liq.hasLevel ? 1 : 0));

	float moveSpeed = CalculateMoveSpeed(input, isSwimming);
	G3D::Vector3 moveDir = intent.hasInput ? G3D::Vector3(intent.dir.x, intent.dir.y, 0.0f) : G3D::Vector3(0, 0, 0);
	float intendedDist = (intent.hasInput ? moveSpeed * dt : 0.0f);

	SceneQuery::SweepResults diag = SceneQuery::ComputeCapsuleSweep(input.mapId, st.x, st.y, st.z, r, h, moveDir, intendedDist);
    {
        std::ostringstream oss;
        oss << "[StepV2] Diag hitCount=" << diag.hitCount
            << " hasPrimary=" << (diag.hasPrimaryPlane ? 1 : 0)
            << " walkableCount=" << diag.walkablePlanes.size()
            << " standFound=" << (diag.standFound ? 1 : 0)
            << " standZ=" << diag.standZ
            << " skin=" << diag.suggestedSkinWidth
            << " intendedDist=" << intendedDist;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }

	if (isSwimming) {
        PHYS_INFO(PHYS_MOVE, "[StepV2] Path=SWIM");
		ProcessSwimMovement(input, intent, st, dt, moveSpeed);
	}
	else if (intent.jumpRequested) {
		// Immediate jump
        PHYS_INFO(PHYS_MOVE, "[StepV2] Path=JUMP (jump requested)");
		st.vz = PhysicsConstants::JUMP_VELOCITY;
		st.isGrounded = false;
		ProcessAirMovement(input, intent, st, dt, moveSpeed);
	}
	else {
		// Ground/air resolution: if there's horizontal input, perform elevated ground move regardless of diag contacts
        bool performedElevatedSweep = false;
        if (intendedDist > 0.0f) {
            performedElevatedSweep = true;
            GroundMoveElevatedSweep(input, diag, intent, st, r, h, moveDir, intendedDist, dt, moveSpeed);
        } else {
            // No horizontal input; settle or fall based on vertical placement
            if (diag.hitCount == 0) {
                const char* reason = "no contacts";
                PerformVerticalPlacementOrFall(input, intent, st, r, h, dt, moveSpeed, reason);
            }
            else if (diag.hasPrimaryPlane) {
                // Even without horizontal motion, prefer to remain grounded if we have a primary plane
                if (!TryDownwardStepSnap(input, st, r, h)) {
                    st.isGrounded = false;
                    if (st.vz >= 0.0f) st.vz = -0.1f;
                    PHYS_INFO(PHYS_MOVE, "[StepV2] No-input with primary plane but no snap; start falling vz=" << st.vz);
                    ProcessAirMovement(input, intent, st, dt, moveSpeed);
                }
            }
            else {
                // Contacts but no primary plane: treat as obstruction; remain grounded if possible
                PHYS_INFO(PHYS_MOVE, std::string("[StepV2] No-input: contacts without primary plane; walkables=")
                    << diag.walkablePlanes.size() << " standFound=" << (diag.standFound ? 1 : 0));
                if (diag.walkablePlanes.empty() && !diag.standFound) {
                    st.isGrounded = false;
                    if (st.vz >= 0.0f) st.vz = -0.1f;
                    PHYS_INFO(PHYS_MOVE, "[StepV2] No-input: contacts with no walkable floor; start falling vz=" << st.vz);
                    ProcessAirMovement(input, intent, st, dt, moveSpeed);
                } else {
                    st.isGrounded = true;
                    st.vx = 0.0f; st.vy = 0.0f; st.vz = 0.0f;
                }
            }
        }

		// Step-down if a valid stand was found and we are above it (skip if elevated sweep handled it)
		if (!performedElevatedSweep && !isSwimming && diag.standFound) {
			float dz = diag.standZ - st.z;
			if (dz < 0.0f && -dz <= PhysicsConstants::STEP_DOWN_HEIGHT) {
				st.z = diag.standZ;
				st.isGrounded = true;
                PHYS_INFO(PHYS_MOVE, std::string("[StepV2] SnapToStand standZ=") << diag.standZ);
			}
		}

        // No separate idle settle phase; vertical placement is handled directly when no horizontal movement.
	}

	// Compute actual velocity based on position delta over dt for this step
	G3D::Vector3 curPos(st.x, st.y, st.z);
	G3D::Vector3 actualV(0, 0, 0);
	if (dt > 0.0f)
		actualV = (curPos - prevPos) * (1.0f / dt);
    else
        PHYS_INFO(PHYS_MOVE, "[StepV2] Non-positive dt; skipping velocity calc");

	// Suppress vertical component unless airborne or swimming
	bool airborne = (!st.isGrounded) || (st.vz != 0.0f);
	if (!airborne && !isSwimming) {
		actualV.z = 0.0f;
	}

	// Output
	out.x = st.x; out.y = st.y; out.z = st.z;
	out.orientation = st.orientation; out.pitch = st.pitch;
	out.vx = actualV.x; out.vy = actualV.y; out.vz = actualV.z;
	out.moveFlags = input.moveFlags;
	if (isSwimming) out.moveFlags |= MOVEFLAG_SWIMMING; else out.moveFlags &= ~MOVEFLAG_SWIMMING;

	// Update movement flags for V2
	// Clear JUMPING unless jump was requested this frame
	if (!intent.jumpRequested) {
		out.moveFlags &= ~MOVEFLAG_JUMPING;
	}
	// Mark falling when not grounded and vertical velocity negative
	if (!st.isGrounded && st.vz < 0.0f) {
		out.moveFlags |= MOVEFLAG_FALLINGFAR; // use existing flag to indicate falling
	}
	else {
		out.moveFlags &= ~MOVEFLAG_FALLINGFAR;
	}
	// Mark MOVED if position changed this step
	if (dt > 0.0f) {
		float dx = st.x - input.x;
		float dy = st.y - input.y;
		float dz = st.z - input.z;
		if ((dx * dx + dy * dy + dz * dz) > 1e-6f) {
			out.moveFlags |= MOVEFLAG_MOVED;
		}
		else {
			out.moveFlags &= ~MOVEFLAG_MOVED;
		}
	}
	out.groundZ = st.z;
	// Use unified liquid mapping consistent with GameData.Core.Enums.LiquidType
	SceneQuery::LiquidInfo finalLiq{};
	{
		auto liq = SceneQuery::EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
		finalLiq.level = liq.level;
		finalLiq.type = liq.type;
		finalLiq.fromVmap = liq.fromVmap;
		finalLiq.hasLevel = liq.hasLevel;
		finalLiq.isSwimming = liq.isSwimming;
	}
	out.liquidZ = finalLiq.level;
	out.liquidType = finalLiq.type;
	// Sync SWIMMING flag with final liquid evaluation
	if (finalLiq.isSwimming) {
		const uint32_t incompatibleSwim =
			MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR | MOVEFLAG_FLYING | MOVEFLAG_ROOT |
			MOVEFLAG_PENDING_STOP | MOVEFLAG_PENDING_UNSTRAFE | MOVEFLAG_PENDING_FORWARD |
			MOVEFLAG_PENDING_BACKWARD | MOVEFLAG_PENDING_STR_LEFT | MOVEFLAG_PENDING_STR_RGHT;
		out.moveFlags |= MOVEFLAG_SWIMMING;
		out.moveFlags &= ~incompatibleSwim;
		if (intent.hasInput && !(out.moveFlags & (MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD | MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT)))
			out.moveFlags |= MOVEFLAG_FORWARD;
	}
	else {
		out.moveFlags &= ~MOVEFLAG_SWIMMING;
	}
	// Suppress per-step V2 OutputSummary here to avoid redundant logs; comparison is logged from Step
	{
		std::ostringstream oss;
		oss << "[StepV2] OutputSummary\n"
			<< "  pos=(" << out.x << "," << out.y << "," << out.z << ")\n"
			<< "  velOut=(" << out.vx << "," << out.vy << "," << out.vz << ")\n"
			<< "  flags=0x" << std::hex << out.moveFlags << std::dec << " "
			<< (isSwimming ? "SWIMMING " : (airborne ? "AIRBORNE" : "GROUNDED")) << "\n"
			<< "  groundZ=" << out.groundZ << " liquidZ=" << out.liquidZ << " liquidType=" << static_cast<int>(out.liquidType);
		PHYS_INFO(PHYS_MOVE, oss.str());
	}
	return out;
}