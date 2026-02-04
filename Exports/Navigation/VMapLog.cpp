#include "VMapLog.h"
#include "PhysicsDiagnosticsHelpers.h"
#include "SceneQuery.h"
#include "Vector3.h"
#include <cstdlib>
#include <string>
#include <sstream>
#include <iostream>
#include "PhysicsBridge.h" // for MOVEFLAG_* constants
#include "VMapDefinitions.h"

// Default logging configuration
int gPhysLogLevel = 3;           // 0=ERR,1=INFO,2=DBG,3=TRACE
uint32_t gPhysLogMask = PHYS_ALL;

static int ParseLevel(const char* s)
{
    if (!s) return gPhysLogLevel;
    try {
        int v = std::stoi(std::string(s));
        if (v < 0) v = 0;
        return v;
    } catch (...) {
        return gPhysLogLevel;
    }
}

void LogStepInputSummary(const PhysicsInput& input, float dt)
{
    PHYS_INFO(PHYS_MOVE,
        std::string("[StepV2] InputSummary\n")
        << "  frame=" << input.frameCounter << " map=" << input.mapId << " dt=" << dt << "\n"
        << "  pos=(" << input.x << "," << input.y << "," << input.z << ")\n"
        << "  velIn=(" << input.vx << "," << input.vy << "," << input.vz << ")\n"
        << "  flags=" << FormatMoveFlags(input.moveFlags) << " (0x" << std::hex << input.moveFlags << std::dec << ")\n"
        << "  orient=" << input.orientation << " pitch=" << input.pitch << "\n"
        << "  size: radius=" << input.radius << " height=" << input.height << "\n"
        << "  speeds[wlk=" << input.walkSpeed << " run=" << input.runSpeed << " back=" << input.runBackSpeed
        << " swim=" << input.swimSpeed << " swimBack=" << input.swimBackSpeed << " fly=" << input.flightSpeed << "]\n"
        << "  fallTime=" << input.fallTime << " transportGuid=" << input.transportGuid << "\n"
        << "  spline=" << (input.hasSplinePath?1:0) << " splineSpeed=" << input.splineSpeed << " curSplineIdx=" << input.currentSplineIndex);
}

void LogSweepDiagnostics(const PhysicsInput& input,
                         float stX,
                         float stY,
                         float stZ,
                         const SceneQuery::SweepResults& diag,
                         const G3D::Vector3& moveDir,
                         float intendedDist,
                         bool isSwimming,
                         float moveSpeed)
{
    std::ostringstream oss;
    oss << "[SweepDiag] Combined\n"
        << "  map=" << input.mapId << " pos=(" << stX << "," << stY << "," << stZ << ") r=" << input.radius << " h=" << input.height << "\n"
        << "  moveDir=(" << moveDir.x << "," << moveDir.y << "," << moveDir.z << ") dist=" << intendedDist << "\n"
        << "  counts: vmap=" << diag.vmapHitCount << " adtPen=" << diag.adtPenetratingHitCount << " sweepCombined=" << diag.hitCount << "\n"
        << "  ordered: pen=" << diag.penCount << " nonPen=" << diag.nonPenCount << "\n"
        << "  VMAP OverlapHits: nonPen=" << diag.vmapNonPenCount << " pen=" << diag.vmapPenCount
        << " earliestNP=" << diag.vmapEarliestNonPen << " zRange=[" << diag.vmapHitMinZ << "," << diag.vmapHitMaxZ << "] walkableNP=" << diag.vmapWalkableNonPen << " instances=" << diag.vmapUniqueInstanceCount << "\n"
        << "  ADT Triangles: count=" << diag.terrainTriCount << " zRange=[" << diag.terrainMinZ << "," << diag.terrainMaxZ << "]"
        << "  ADT OverlapHits: count=" << diag.adtPenetratingHitCount << " zRange=[" << diag.adtHitMinZ << "," << diag.adtHitMaxZ << "]\n"
        << "  Selection: standFound=" << (diag.standFound ? 1 : 0) << " standZ=" << diag.standZ
        << " source=" << (diag.standSource == SceneQuery::SweepResults::StandSource::VMAP ? "VMAP" : diag.standSource == SceneQuery::SweepResults::StandSource::ADT ? "ADT" : "None") << "\n"
        << "  Manifold: planes=" << diag.planes.size() << " walkable=" << diag.walkablePlanes.size() << " hasPrimary=" << (diag.hasPrimaryPlane ? 1 : 0);
    if (diag.hasPrimaryPlane) {
        oss << " primaryN=(" << diag.primaryPlane.normal.x << "," << diag.primaryPlane.normal.y << "," << diag.primaryPlane.normal.z << ")"
            << " primaryP=(" << diag.primaryPlane.point.x << "," << diag.primaryPlane.point.y << "," << diag.primaryPlane.point.z << ")"
            << " walkable=" << (diag.primaryPlane.walkable ? 1 : 0) << " penetrating=" << (diag.primaryPlane.penetrating ? 1 : 0);
    }
    oss << "\n"
        << "    slideDirValid=" << (diag.slideDirValid ? 1 : 0) << " slideDir=(" << diag.slideDir.x << "," << diag.slideDir.y << "," << diag.slideDir.z << ")"
        << " minTOI=" << diag.minTOI << " depenMag=" << diag.depenetrationMagnitude;
    {
        const char* lStartName = VMAP::GetLiquidTypeName(diag.liquidStartType);
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
        // Predict final position purely from diagnostics (for logging only)
        G3D::Vector3 finalPos(stX, stY, (!isSwimming && diag.standFound) ? diag.standZ : stZ);
        if (!isSwimming && diag.slideDirValid && intendedDist > 0.0f) {
            G3D::Vector3 s = PhysicsDiag::DirectionOrZero(diag.slideDir);
            finalPos.x += s.x * intendedDist;
            finalPos.y += s.y * intendedDist;
        }
        if (!isSwimming && diag.hasPrimaryPlane) {
            G3D::Vector3 n = PhysicsDiag::DirectionOrZero(diag.primaryPlane.normal);
            finalPos.z = PhysicsDiag::PlaneZAtXY(n, diag.primaryPlane.point, finalPos.x, finalPos.y, finalPos.z);
        }
        oss << "\n" << "  FinalPos: (" << finalPos.x << "," << finalPos.y << "," << finalPos.z << ")";
        // Intended velocity uses speed-scaled direction (not distance)
        G3D::Vector3 intendedVel = (moveDir.magnitude() > 1e-6f) ? (PhysicsDiag::DirectionOrZero(moveDir) * moveSpeed) : G3D::Vector3(0,0,0);
        // Ending velocity: use plane-projected horizontal direction at same horizontal speed as intended
        G3D::Vector3 endingVel(0,0,0);
        if (!isSwimming) {
            G3D::Vector3 s = diag.slideDirValid ? PhysicsDiag::DirectionOrZero(diag.slideDir) : PhysicsDiag::DirectionOrZero(moveDir);
            s.z = 0.0f; // ground slide reports horizontal velocity
            endingVel = s.directionOrZero() * moveSpeed;
        }
        // Overall velocity will be computed after movement; log placeholder using endingVel for now
        G3D::Vector3 overallVel = endingVel;
        oss << "\n" << "  Velocities: intended=(" << intendedVel.x << "," << intendedVel.y << "," << intendedVel.z
            << ") ending=(" << endingVel.x << "," << endingVel.y << "," << endingVel.z
            << ") overall=(" << overallVel.x << "," << overallVel.y << "," << overallVel.z << ")";
    }
    PHYS_INFO(PHYS_SURF, oss.str());
}
std::string FormatMoveFlags(uint32_t flags)
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

static uint32_t ParseMask(const char* s)
{
    if (!s) return gPhysLogMask;
    std::string str(s);
    try {
        // allow 0x hex or decimal
        size_t idx = 0;
        unsigned long v = std::stoul(str, &idx, 0);
        return static_cast<uint32_t>(v);
    } catch (...) {
        return gPhysLogMask;
    }
}

// Static initializer reads optional environment variables to override defaults.
struct VMapLogInit
{
    VMapLogInit()
    {
        const char* lvl = std::getenv("VMAP_PHYS_LOG_LEVEL");
        if (lvl) gPhysLogLevel = ParseLevel(lvl);
        const char* mask = std::getenv("VMAP_PHYS_LOG_MASK");
        if (mask) gPhysLogMask = ParseMask(mask);

        // Force-enable TRACE level and cylinder category to ensure diagnostics are visible during tests.
        // This overrides runtime env vars if they would disable the important TRACE output we added.
        if (gPhysLogLevel < 3) gPhysLogLevel = 3;
        gPhysLogMask |= PHYS_CYL; // ensure cylinder logs are always included

        // Echo the runtime settings to stdout so tests can observe them (from main)
        std::ostringstream ss;
        ss << "[PHYS][INFO][INIT] gPhysLogLevel=" << gPhysLogLevel << " gPhysLogMask=0x" << std::hex << gPhysLogMask << std::dec;
        std::cout << ss.str() << std::endl;
    }
};

static VMapLogInit s_vmapLogInit;

const char* PhysLevelName(int lvl)
{
    switch (lvl)
    {
    case 0: return "ERR";
    case 1: return "INF";
    case 2: return "DBG";
    case 3: return "TRC";
    default: return "UNK";
    }
}

const char* PhysCatName(uint32_t cat)
{
    // Prefer single-category names; if multiple bits set, return a short combined label.
    if (cat == PHYS_MOVE) return "MOVE";
    if (cat == PHYS_SURF) return "SURF";
    if (cat == PHYS_HEAD) return "HEAD";
    if (cat == PHYS_CYL)  return "CYL";
    if (cat == PHYS_STEP) return "STEP";

    // fallback: build short name for multiple bits
    if (cat & PHYS_CYL) return "CYL";
    if (cat & PHYS_MOVE) return "MOVE";
    if (cat & PHYS_SURF) return "SURF";
    if (cat & PHYS_HEAD) return "HEAD";
    if (cat & PHYS_STEP) return "STEP";
    return "GEN";
}
