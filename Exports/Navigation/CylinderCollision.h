// CylinderCollision.h - Complete cylinder collision implementation
#pragma once

#include "Vector3.h"
#include "AABox.h"
#include <cmath>
#include <vector>
#include <algorithm>
#include "QueryHit.h"

namespace VMAP
{
    // Simple cylinder definition for collision
    struct Cylinder
    {
        G3D::Vector3 base;     // Bottom center of cylinder
        G3D::Vector3 axis;     // Normalized axis (usually (0,0,1))
        float radius;
        float height;

        Cylinder(const G3D::Vector3& b, float r, float h)
            : base(b), axis(0, 0, 1), radius(r), height(h) {
        }

        Cylinder(const G3D::Vector3& b, const G3D::Vector3& a, float r, float h)
            : base(b), radius(r), height(h) {
            float axisLength = a.magnitude();
            if (axisLength > 0.0001f) {
                axis = a / axisLength;
            }
            else {
                axis = G3D::Vector3(0, 0, 1);
            }
        }

        G3D::Vector3 getTop() const { return base + axis * height; }
        G3D::Vector3 getCenter() const { return base + axis * (height * 0.5f); }

        // Get bounding box for broad phase collision
        G3D::AABox getBounds() const {
            G3D::Vector3 radiusVec(radius, radius, radius);
            G3D::Vector3 top = getTop();
            G3D::Vector3 minPoint = base.min(top) - radiusVec;
            G3D::Vector3 maxPoint = base.max(top) + radiusVec;
            return G3D::AABox(minPoint, maxPoint);
        }
    };

    // Result of cylinder intersection with a surface
    struct CylinderIntersection
    {
        bool hit;
        float contactHeight;        // Z coordinate of contact point
        G3D::Vector3 contactPoint;  // 3D contact point
        G3D::Vector3 contactNormal; // Surface normal at contact
        float penetrationDepth;     // How deep the cylinder penetrates
        // Source identifiers for diagnostics
        uint32_t triIndex;          // Triangle index within the tested mesh/model (if available)
        uint32_t instanceId;        // Source model instance id (if available)

        CylinderIntersection()
            : hit(false), contactHeight(-200000.0f),
            contactPoint(0, 0, 0), contactNormal(0, 0, 1),
            penetrationDepth(0), triIndex(0), instanceId(0) {
        }
    };

    // Collection of surface hits for cylinder sweep
    struct CylinderSweepHit
    {
        // Unified query hit info (TOI in distance when sweeping)
        QueryHit q;
        // Convenience legacy fields (height/position/walkable)
        float height = -G3D::inf();
        G3D::Vector3 normal = G3D::Vector3(0,0,1);
        G3D::Vector3 position = G3D::Vector3(0,0,0);
        bool walkable = false;
        uint32_t triangleIndex = 0;

        // Sorting by earliest Time Of Impact (ascending distance)
        bool operator<(const CylinderSweepHit& other) const {
            return q.distance < other.q.distance;
        }

        // Implicit conversion to unified hit
        operator QueryHit() const { return q; }
    };

    // Main cylinder collision class
    class CylinderCollision
    {
    public:
        // Core cylinder-triangle intersection test
        static CylinderIntersection IntersectCylinderTriangle(
            const Cylinder& cyl,
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2);

        // Swept cylinder test - finds all surfaces a cylinder intersects
        // when moving from start to end position
        static std::vector<CylinderSweepHit> SweepCylinder(
            const Cylinder& cyl,
            const G3D::Vector3& sweepDir,
            float sweepDistance,
            const std::vector<G3D::Vector3>& vertices,
            const std::vector<uint32_t>& indices);

        // Find the best walkable surface for a cylinder at given position
        static bool FindBestWalkableSurface(
            const Cylinder& cyl,
            const std::vector<CylinderSweepHit>& hits,
            float currentHeight,
            float maxStepUp,
            float maxStepDown,
            float& outHeight,
            G3D::Vector3& outNormal);

        // Find the best step-up surface from a set of hits
        static bool FindBestStepUpSurface(
            const std::vector<CylinderSweepHit>& hits,
            float currentHeight,
            float maxStepUp,
            float& outHeight,
            G3D::Vector3& outNormal);

    private:
        // Helper: Project point onto line segment
        static G3D::Vector3 ClosestPointOnSegment(
            const G3D::Vector3& point,
            const G3D::Vector3& segStart,
            const G3D::Vector3& segEnd);

        // Helper: Distance from point to line segment
        static float DistanceToSegment(
            const G3D::Vector3& point,
            const G3D::Vector3& segStart,
            const G3D::Vector3& segEnd);

        // Helper: Check if point is inside triangle (2D test)
        static bool PointInTriangle2D(
            const G3D::Vector3& p,
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2);

        // Helper: Intersect cylinder with triangle edge
        static bool IntersectCylinderEdge(
            const Cylinder& cyl,
            const G3D::Vector3& edgeStart,
            const G3D::Vector3& edgeEnd,
            CylinderIntersection& result);

        // Helper: Test cylinder caps against triangle
        static bool IntersectCylinderCapsWithTriangle(
            const Cylinder& cyl,
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2,
            const G3D::Vector3& triNormal,
            CylinderIntersection& result);

        // Helper: Test cylinder side against triangle
        static bool IntersectCylinderSideWithTriangle(
            const Cylinder& cyl,
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2,
            const G3D::Vector3& triNormal,
            CylinderIntersection& result);
    };

    // Helper functions that were in the original implementation
    namespace CylinderHelpers
    {
        // Global configurable walkable slope threshold (cosine of max slope angle)
        inline float& WalkableCosMinRef() {
            static float s_walkableCosMin = 0.7071f; // default cos(45deg)
            return s_walkableCosMin;
        }
        inline void SetWalkableCosMin(float v) { WalkableCosMinRef() = v; }
        inline float GetWalkableCosMin() { return WalkableCosMinRef(); }

        // Strategy for computing triangle normals relative to world up (Z)
        enum class TriangleNormalMode { Raw = 0, Upward = 1, DetourXY = 2 };
        inline TriangleNormalMode& TriangleNormalModeRef() {
            // Default to upward-oriented hemisphere alignment (modern engine style)
            static TriangleNormalMode s_mode = TriangleNormalMode::Upward;
            return s_mode;
        }
        inline void SetTriangleNormalMode(TriangleNormalMode m) { TriangleNormalModeRef() = m; }
        inline TriangleNormalMode GetTriangleNormalMode() { return TriangleNormalModeRef(); }

        // Check if surface is walkable (used in physics engine)
        inline bool IsWalkableSurface(const G3D::Vector3& normal) {
            return normal.z >= GetWalkableCosMin();
        }

        // Raw triangle normal (right-handed), robust with double precision
        inline G3D::Vector3 CalculateTriangleNormalRaw(
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2)
        {
            // Use double for robustness on nearly-degenerate triangles
            double e1x = static_cast<double>(v1.x - v0.x);
            double e1y = static_cast<double>(v1.y - v0.y);
            double e1z = static_cast<double>(v1.z - v0.z);
            double e2x = static_cast<double>(v2.x - v0.x);
            double e2y = static_cast<double>(v2.y - v0.y);
            double e2z = static_cast<double>(v2.z - v0.z);

            // Cross product e1 x e2
            double nx = e1y * e2z - e1z * e2y;
            double ny = e1z * e2x - e1x * e2z;
            double nz = e1x * e2y - e1y * e2x;

            double len = std::sqrt(nx*nx + ny*ny + nz*nz);
            if (len > 1e-8) {
                float inv = static_cast<float>(1.0 / len);
                return G3D::Vector3(static_cast<float>(nx) * inv,
                                     static_cast<float>(ny) * inv,
                                     static_cast<float>(nz) * inv);
            }
            // Fallback: vertical up (prevents NaNs)
            return G3D::Vector3(0,0,1);
        }

        // Upward-oriented normal: flip to ensure dot(up) >= 0 (Z-up hemisphere)
        inline G3D::Vector3 CalculateTriangleNormalUpward(
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2)
        {
            G3D::Vector3 n = CalculateTriangleNormalRaw(v0, v1, v2);
            if (n.z < 0.0f) n = -n;
            return n;
        }

        // Detour-style orientation: historically uses XZ for Y-up.
        // We are Z-up, so project to XY for winding, then hemisphere-align upward.
        inline G3D::Vector3 CalculateTriangleNormalDetourXY(
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2)
        {
            // Compute raw normal
            G3D::Vector3 n = CalculateTriangleNormalRaw(v0, v1, v2);
            // XY signed area (2x area), positive for CCW in XY (Z-up right-handed)
            float area2 = (v1.x - v0.x) * (v2.y - v0.y) - (v2.x - v0.x) * (v1.y - v0.y);
            // For degenerate projections, just fall back to hemisphere alignment
            if (std::abs(area2) < 1e-8f) {
                if (n.z < 0.0f) n = -n;
                return n;
            }
            // Hemisphere alignment like modern engines (ensures upward-facing)
            if (n.z < 0.0f) n = -n;
            return n;
        }

        // Unified function selecting strategy
        inline G3D::Vector3 CalculateTriangleNormalOriented(
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2)
        {
            switch (GetTriangleNormalMode())
            {
            case TriangleNormalMode::Raw:      return CalculateTriangleNormalRaw(v0, v1, v2);
            case TriangleNormalMode::DetourXY: return CalculateTriangleNormalDetourXY(v0, v1, v2);
            case TriangleNormalMode::Upward:
            default:                           return CalculateTriangleNormalUpward(v0, v1, v2);
            }
        }

        // Backwards-compat: existing name maps to oriented strategy
        inline G3D::Vector3 CalculateTriangleNormal(
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2)
        {
            return CalculateTriangleNormalOriented(v0, v1, v2);
        }
    }
}