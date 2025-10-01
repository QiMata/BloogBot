// CylinderCollision.cpp - Complete cylinder collision implementation
#include "CylinderCollision.h"
#include "VMapLog.h" // for PHYS_* logging
#include <limits>

namespace VMAP
{
    // Helper: Project point onto line segment
    G3D::Vector3 CylinderCollision::ClosestPointOnSegment(
        const G3D::Vector3& point,
        const G3D::Vector3& segStart,
        const G3D::Vector3& segEnd)
    {
        G3D::Vector3 segment = segEnd - segStart;
        float segLengthSq = segment.squaredMagnitude();

        if (segLengthSq < 0.0001f) {
            return segStart; // Degenerate segment
        }

        float t = (point - segStart).dot(segment) / segLengthSq;
        t = std::max(0.0f, std::min(1.0f, t));

        return segStart + segment * t;
    }

    // Helper: Distance from point to line segment
    float CylinderCollision::DistanceToSegment(
        const G3D::Vector3& point,
        const G3D::Vector3& segStart,
        const G3D::Vector3& segEnd)
    {
        G3D::Vector3 closest = ClosestPointOnSegment(point, segStart, segEnd);
        return (point - closest).magnitude();
    }

    // Helper: Check if point is inside triangle (2D test, ignoring Z)
    bool CylinderCollision::PointInTriangle2D(
        const G3D::Vector3& p,
        const G3D::Vector3& v0,
        const G3D::Vector3& v1,
        const G3D::Vector3& v2)
    {
        // Use barycentric coordinates
        float denominator = ((v1.y - v2.y) * (v0.x - v2.x) +
            (v2.x - v1.x) * (v0.y - v2.y));

        if (std::abs(denominator) < 0.0001f) {
            return false; // Degenerate triangle
        }

        float a = ((v1.y - v2.y) * (p.x - v2.x) +
            (v2.x - v1.x) * (p.y - v2.y)) / denominator;
        float b = ((v2.y - v0.y) * (p.x - v2.x) +
            (v0.x - v2.x) * (p.y - v2.y)) / denominator;
        float c = 1 - a - b;

        return (a >= 0 && a <= 1 && b >= 0 && b <= 1 && c >= 0 && c <= 1);
    }

    // Test cylinder caps against triangle
    bool CylinderCollision::IntersectCylinderCapsWithTriangle(
        const Cylinder& cyl,
        const G3D::Vector3& v0,
        const G3D::Vector3& v1,
        const G3D::Vector3& v2,
        const G3D::Vector3& triNormal,
        CylinderIntersection& result)
    {
        // Test bottom cap
        G3D::Vector3 capCenter = cyl.base;

        // Project cap center onto triangle plane
        float distToPlane = (capCenter - v0).dot(triNormal);
        G3D::Vector3 projectedPoint = capCenter - triNormal * distToPlane;

        // Check if projected point is within cylinder radius of triangle
        bool bottomHit = false;

        if (PointInTriangle2D(projectedPoint, v0, v1, v2)) {
            // Point is inside triangle
            if (std::abs(distToPlane) <= 0.1f) { // Small tolerance
                bottomHit = true;
                result.hit = true;
                result.contactPoint = projectedPoint;
                result.contactHeight = projectedPoint.z;
                result.contactNormal = triNormal;
                result.penetrationDepth = std::abs(distToPlane);
            }
        }
        else {
            // Check distance to triangle edges
            float dist0 = DistanceToSegment(projectedPoint, v0, v1);
            float dist1 = DistanceToSegment(projectedPoint, v1, v2);
            float dist2 = DistanceToSegment(projectedPoint, v2, v0);
            float minDist = std::min({ dist0, dist1, dist2 });

            if (minDist <= cyl.radius && std::abs(distToPlane) <= 0.1f) {
                bottomHit = true;
                result.hit = true;

                // Find closest point on triangle perimeter
                G3D::Vector3 closestPoint;
                if (minDist == dist0) {
                    closestPoint = ClosestPointOnSegment(projectedPoint, v0, v1);
                }
                else if (minDist == dist1) {
                    closestPoint = ClosestPointOnSegment(projectedPoint, v1, v2);
                }
                else {
                    closestPoint = ClosestPointOnSegment(projectedPoint, v2, v0);
                }

                result.contactPoint = closestPoint;
                result.contactHeight = closestPoint.z;
                result.contactNormal = triNormal;
                result.penetrationDepth = cyl.radius - minDist;
            }
        }

        // Test top cap similarly
        capCenter = cyl.getTop();
        distToPlane = (capCenter - v0).dot(triNormal);
        projectedPoint = capCenter - triNormal * distToPlane;

        if (PointInTriangle2D(projectedPoint, v0, v1, v2)) {
            if (std::abs(distToPlane) <= 0.1f) {
                result.hit = true;
                // Only update if this is a better (higher) contact point
                if (projectedPoint.z > result.contactHeight || !bottomHit) {
                    result.contactPoint = projectedPoint;
                    result.contactHeight = projectedPoint.z;
                    result.contactNormal = triNormal;
                    result.penetrationDepth = std::abs(distToPlane);
                }
                return true;
            }
        }
        else {
            float dist0 = DistanceToSegment(projectedPoint, v0, v1);
            float dist1 = DistanceToSegment(projectedPoint, v1, v2);
            float dist2 = DistanceToSegment(projectedPoint, v2, v0);
            float minDist = std::min({ dist0, dist1, dist2 });

            if (minDist <= cyl.radius && std::abs(distToPlane) <= 0.1f) {
                result.hit = true;

                G3D::Vector3 closestPoint;
                if (minDist == dist0) {
                    closestPoint = ClosestPointOnSegment(projectedPoint, v0, v1);
                }
                else if (minDist == dist1) {
                    closestPoint = ClosestPointOnSegment(projectedPoint, v1, v2);
                }
                else {
                    closestPoint = ClosestPointOnSegment(projectedPoint, v2, v0);
                }

                // Only update if this is a better (higher) contact point
                if (closestPoint.z > result.contactHeight || !bottomHit) {
                    result.contactPoint = closestPoint;
                    result.contactHeight = closestPoint.z;
                    result.contactNormal = triNormal;
                    result.penetrationDepth = cyl.radius - minDist;
                }
                return true;
            }
        }

        return bottomHit;
    }

    // Test cylinder side against triangle edges
    bool CylinderCollision::IntersectCylinderEdge(
        const Cylinder& cyl,
        const G3D::Vector3& edgeStart,
        const G3D::Vector3& edgeEnd,
        CylinderIntersection& result)
    {
        // Find closest point on cylinder axis to edge
        G3D::Vector3 cylBottom = cyl.base;
        G3D::Vector3 cylTop = cyl.getTop();

        // Find closest points between two line segments
        G3D::Vector3 d1 = cylTop - cylBottom;
        G3D::Vector3 d2 = edgeEnd - edgeStart;
        G3D::Vector3 r = cylBottom - edgeStart;

        float a = d1.dot(d1);
        float b = d1.dot(d2);
        float c = d1.dot(r);
        float e = d2.dot(d2);
        float f = d2.dot(r);

        float denom = a * e - b * b;

        float s, t;
        if (std::abs(denom) < 0.0001f) {
            // Parallel lines
            s = 0;
            t = f / e;
        }
        else {
            s = (b * f - c * e) / denom;
            t = (a * f - b * c) / denom;
        }

        // Clamp parameters to segment bounds
        s = std::max(0.0f, std::min(1.0f, s));
        t = std::max(0.0f, std::min(1.0f, t));

        G3D::Vector3 closestOnCylAxis = cylBottom + d1 * s;
        G3D::Vector3 closestOnEdge = edgeStart + d2 * t;

        float distance = (closestOnCylAxis - closestOnEdge).magnitude();

        if (distance <= cyl.radius) {
            result.hit = true;
            result.contactPoint = closestOnEdge;
            result.contactHeight = closestOnEdge.z;

            // Calculate normal from cylinder axis to edge
            G3D::Vector3 toEdge = closestOnEdge - closestOnCylAxis;
            float toEdgeLength = toEdge.magnitude();
            if (toEdgeLength > 0.0001f) {
                result.contactNormal = toEdge / toEdgeLength;
            }
            else {
                result.contactNormal = G3D::Vector3(0, 0, 1);
            }

            result.penetrationDepth = cyl.radius - distance;
            return true;
        }

        return false;
    }

    // Test cylinder side against triangle interior
    bool CylinderCollision::IntersectCylinderSideWithTriangle(
        const Cylinder& cyl,
        const G3D::Vector3& v0,
        const G3D::Vector3& v1,
        const G3D::Vector3& v2,
        const G3D::Vector3& triNormal,
        CylinderIntersection& result)
    {
        bool hit = false;

        // Test each triangle edge against cylinder
        CylinderIntersection edgeResult;

        if (IntersectCylinderEdge(cyl, v0, v1, edgeResult)) {
            if (!hit || edgeResult.contactHeight > result.contactHeight) {
                result = edgeResult;
                hit = true;
            }
        }

        if (IntersectCylinderEdge(cyl, v1, v2, edgeResult)) {
            if (!hit || edgeResult.contactHeight > result.contactHeight) {
                result = edgeResult;
                hit = true;
            }
        }

        if (IntersectCylinderEdge(cyl, v2, v0, edgeResult)) {
            if (!hit || edgeResult.contactHeight > result.contactHeight) {
                result = edgeResult;
                hit = true;
            }
        }

        // Also check if cylinder axis passes through triangle
        G3D::Vector3 cylBottom = cyl.base;
        G3D::Vector3 cylTop = cyl.getTop();

        // Ray-triangle intersection for cylinder axis
        G3D::Vector3 edge1 = v1 - v0;
        G3D::Vector3 edge2 = v2 - v0;
        G3D::Vector3 h = cyl.axis.cross(edge2);
        float a = edge1.dot(h);

        if (std::abs(a) > 0.0001f) {
            float f = 1.0f / a;
            G3D::Vector3 s = cylBottom - v0;
            float u = f * s.dot(h);

            if (u >= 0.0f && u <= 1.0f) {
                G3D::Vector3 q = s.cross(edge1);
                float v = f * cyl.axis.dot(q);

                if (v >= 0.0f && u + v <= 1.0f) {
                    float t = f * edge2.dot(q);

                    if (t >= 0.0f && t <= cyl.height) {
                        // Axis intersects triangle
                        G3D::Vector3 intersectionPoint = cylBottom + cyl.axis * t;

                        if (!hit || intersectionPoint.z > result.contactHeight) {
                            result.hit = true;
                            result.contactPoint = intersectionPoint;
                            result.contactHeight = intersectionPoint.z;
                            result.contactNormal = triNormal;
                            result.penetrationDepth = 0; // Axis passes through
                            hit = true;
                        }
                    }
                }
            }
        }

        return hit;
    }

    // Main cylinder-triangle intersection test
    CylinderIntersection CylinderCollision::IntersectCylinderTriangle(
        const Cylinder& cyl,
        const G3D::Vector3& v0,
        const G3D::Vector3& v1,
        const G3D::Vector3& v2)
    {
        CylinderIntersection result;

        // Calculate triangle normal
        G3D::Vector3 triNormal = CylinderHelpers::CalculateTriangleNormal(v0, v1, v2);

        // Quick reject: Check if cylinder bounds intersect triangle bounds
        G3D::AABox cylBounds = cyl.getBounds();
        G3D::AABox triBounds(v0, v0);
        triBounds.merge(v1);
        triBounds.merge(v2);

        if (!cylBounds.intersects(triBounds)) {
            return result; // No intersection possible
        }

        // Test cylinder caps against triangle
        CylinderIntersection capsResult;
        bool capsHit = IntersectCylinderCapsWithTriangle(cyl, v0, v1, v2, triNormal, capsResult);

        // Test cylinder side against triangle
        CylinderIntersection sideResult;
        bool sideHit = IntersectCylinderSideWithTriangle(cyl, v0, v1, v2, triNormal, sideResult);

        // Return the best (highest) contact point
        if (capsHit && sideHit) {
            result = (capsResult.contactHeight > sideResult.contactHeight) ? capsResult : sideResult;
        }
        else if (capsHit) {
            result = capsResult;
        }
        else if (sideHit) {
            result = sideResult;
        }

        if (result.hit) {
            PHYS_TRACE(PHYS_CYL, "triangle hit contactZ=" << result.contactHeight << " normalZ=" << result.contactNormal.z << " pen=" << result.penetrationDepth);
        }
        return result;
    }

    // Sweep cylinder through mesh to find all intersections
    std::vector<CylinderSweepHit> CylinderCollision::SweepCylinder(
        const Cylinder& cyl,
        const G3D::Vector3& sweepDir,
        float sweepDistance,
        const std::vector<G3D::Vector3>& vertices,
        const std::vector<uint32_t>& indices)
    {
        std::vector<CylinderSweepHit> hits;

        // Create swept bounds for broad phase
        G3D::AABox sweepBounds = cyl.getBounds();
        Cylinder endCyl(cyl.base + sweepDir * sweepDistance, cyl.axis, cyl.radius, cyl.height);
        sweepBounds.merge(endCyl.getBounds());

        // Test each triangle
        size_t triangleCount = indices.size() / 3;
        for (size_t i = 0; i < triangleCount; ++i) {
            const G3D::Vector3& v0ref = vertices[indices[i * 3]];
            const G3D::Vector3& v1ref = vertices[indices[i * 3 + 1]];
            const G3D::Vector3& v2ref = vertices[indices[i * 3 + 2]];

            // Quick bounds check
            G3D::AABox triBounds(v0ref, v0ref);
            triBounds.merge(v1ref);
            triBounds.merge(v2ref);

            if (!sweepBounds.intersects(triBounds)) {
                continue;
            }

            // Test multiple positions along sweep
            const int sweepSteps = std::max(1, (int)(sweepDistance / 0.5f));
            for (int step = 0; step <= sweepSteps; ++step) {
                float t = (float)step / sweepSteps;
                Cylinder testCyl(cyl.base + sweepDir * (t * sweepDistance),
                    cyl.axis, cyl.radius, cyl.height);

                CylinderIntersection intersection = IntersectCylinderTriangle(testCyl, v0ref, v1ref, v2ref);

                if (intersection.hit) {
                    CylinderSweepHit hit;
                    hit.height = intersection.contactHeight;
                    hit.normal = intersection.contactNormal;
                    hit.position = intersection.contactPoint;
                    hit.walkable = CylinderHelpers::IsWalkableSurface(intersection.contactNormal);
                    hit.triangleIndex = (uint32_t)i;
                    hits.push_back(hit);
                    break; // Found hit for this triangle, move to next
                }
            }
        }

        // Sort hits by height (highest first)
        std::sort(hits.begin(), hits.end());

        return hits;
    }

    // Find best walkable surface from sweep results
    bool CylinderCollision::FindBestWalkableSurface(
        const Cylinder& cyl,
        const std::vector<CylinderSweepHit>& hits,
        float currentHeight,
        float maxStepUp,
        float maxStepDown,
        float& outHeight,
        G3D::Vector3& outNormal)
    {
        float bestHeight = -std::numeric_limits<float>::max();
        bool foundSurface = false;

        for (const auto& hit : hits) {
            // Skip non-walkable surfaces
            if (!hit.walkable) {
                continue;
            }

            // Check if surface is within step limits
            float heightDiff = hit.height - currentHeight;

            if (heightDiff > maxStepUp) {
                continue; // Too high to step up
            }

            if (heightDiff < -maxStepDown) {
                continue; // Too far to step down
            }

            // Check if cylinder can fit at this height
            // (Additional validation could go here)

            // Prefer the highest valid surface (most likely to be ground)
            if (hit.height > bestHeight) {
                bestHeight = hit.height;
                outHeight = hit.height;
                outNormal = hit.normal;
                foundSurface = true;
            }
        }

        return foundSurface;
    }

    // Find best step-up surface from a set of hits
    bool CylinderCollision::FindBestStepUpSurface(
        const std::vector<CylinderSweepHit>& hits,
        float currentHeight,
        float maxStepUp,
        float& outHeight,
        G3D::Vector3& outNormal)
    {
        float bestStepHeight = std::numeric_limits<float>::max();
        bool foundSurface = false;

        for (const auto& hit : hits) {
            if (!hit.walkable) {
                continue;
            }

            float heightDiff = hit.height - currentHeight;

            // Check if it's a valid step up
            if (heightDiff > 0.1f && heightDiff <= maxStepUp) {
                // We want the lowest step-up surface
                if (hit.height < bestStepHeight) {
                    bestStepHeight = hit.height;
                    outHeight = hit.height;
                    outNormal = hit.normal;
                    foundSurface = true;
                }
            }
        }

        return foundSurface;
    }
} // namespace VMAP