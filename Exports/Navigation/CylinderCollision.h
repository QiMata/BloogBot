// CylinderCollision.h - Complete cylinder collision implementation
#pragma once

#include "Vector3.h"
#include "AABox.h"
#include <cmath>
#include <vector>
#include <algorithm>

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

        CylinderIntersection()
            : hit(false), contactHeight(-200000.0f),
            contactPoint(0, 0, 0), contactNormal(0, 0, 1),
            penetrationDepth(0) {
        }
    };

    // Collection of surface hits for cylinder sweep
    struct CylinderSweepHit
    {
        float height;               // Ground height at this point
        G3D::Vector3 normal;        // Surface normal
        G3D::Vector3 position;      // Position on ground
        bool walkable;              // Is surface walkable based on slope
        uint32_t triangleIndex;     // Which triangle was hit

        // For sorting by height (highest first)
        bool operator<(const CylinderSweepHit& other) const {
            return height > other.height;
        }
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
        // Check if surface is walkable (used in physics engine)
        inline bool IsWalkableSurface(const G3D::Vector3& normal) {
            return normal.z >= 0.6428f; // cos(50°)
        }

        // Calculate triangle normal
        inline G3D::Vector3 CalculateTriangleNormal(
            const G3D::Vector3& v0,
            const G3D::Vector3& v1,
            const G3D::Vector3& v2)
        {
            G3D::Vector3 edge1 = v1 - v0;
            G3D::Vector3 edge2 = v2 - v0;
            G3D::Vector3 normal = edge1.cross(edge2);
            float length = normal.magnitude();

            if (length > 0.0001f)
                normal /= length;

            return normal;
        }
    }
}