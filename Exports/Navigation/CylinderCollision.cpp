// CylinderCollision.cpp - Complete cylinder collision implementation
#include "CylinderCollision.h"
#include "VMapLog.h" // for PHYS_* logging
#include <limits>

namespace VMAP
{
    // Local small epsilons
    static inline float CC_EPS() { return 1e-6f; }

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

    // New helper: closest point on triangle in 3D using barycentric regions (Ericson)
    static inline G3D::Vector3 ClosestPointOnTriangle3D(
        const G3D::Vector3& p,
        const G3D::Vector3& a,
        const G3D::Vector3& b,
        const G3D::Vector3& c)
    {
        G3D::Vector3 ab = b - a;
        G3D::Vector3 ac = c - a;
        G3D::Vector3 ap = p - a;
        float d1 = ab.dot(ap);
        float d2 = ac.dot(ap);
        if (d1 <= 0.0f && d2 <= 0.0f) return a; // bary (1,0,0)

        G3D::Vector3 bp = p - b;
        float d3 = ab.dot(bp);
        float d4 = ac.dot(bp);
        if (d3 >= 0.0f && d4 <= d3) return b; // bary (0,1,0)

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            float v = d1 / (d1 - d3 + (std::abs(d1 - d3) <= CC_EPS() ? CC_EPS() : 0.0f));
            return a + ab * v; // bary (1-v, v, 0)
        }

        G3D::Vector3 cp = p - c;
        float d5 = ab.dot(cp);
        float d6 = ac.dot(cp);
        if (d6 >= 0.0f && d5 <= d6) return c; // bary (0,0,1)

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            float denom_ac = (d2 - d6);
            denom_ac = std::abs(denom_ac) <= CC_EPS() ? (denom_ac < 0.0f ? -CC_EPS() : CC_EPS()) : denom_ac;
            float w = d2 / denom_ac;
            return a + ac * w; // bary (1-w, 0, w)
        }

        G3D::Vector3 bc = c - b;
        float va = d3 * d6 - d5 * d4;
        if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
        {
            float denom_bc = (d4 - d3) + (d5 - d6);
            denom_bc = std::abs(denom_bc) <= CC_EPS() ? (denom_bc < 0.0f ? -CC_EPS() : CC_EPS()) : denom_bc;
            float w = (d4 - d3) / denom_bc;
            return b + bc * w; // bary (0,1-w,w)
        }

        // Inside face
        float sum = va + vb + vc;
        if (std::abs(sum) <= CC_EPS()) return a; // fallback
        float inv = 1.0f / sum;
        float v = vb * inv;
        float w = vc * inv;
        return a * (1.0f - v - w) + b * v + c * w;
    }

    // New helper: sphere vs triangle (3D). Writes candidate if closer.
    static inline bool IntersectSphereTriangle3D(
        const G3D::Vector3& center,
        float radius,
        const G3D::Vector3& v0,
        const G3D::Vector3& v1,
        const G3D::Vector3& v2,
        const G3D::Vector3& triNormalOriented,
        CylinderIntersection& out)
    {
        G3D::Vector3 q = ClosestPointOnTriangle3D(center, v0, v1, v2);
        G3D::Vector3 d = center - q;
        float dist2 = d.squaredMagnitude();
        float r2 = radius * radius;
        if (dist2 > r2)
            return false;
        float dist = std::sqrt(std::max(dist2, 0.0f));
        G3D::Vector3 n;
        if (dist > 1e-6f)
            n = d / dist;
        else
            n = triNormalOriented;

        out.hit = true;
        out.contactPoint = q;
        out.contactHeight = q.z;
        out.contactNormal = n;
        out.penetrationDepth = radius - dist;
        return true;
    }

    // Test cylinder caps against triangle (use proper sphere-triangle tests for top & bottom caps)
    bool CylinderCollision::IntersectCylinderCapsWithTriangle(
        const Cylinder& cyl,
        const G3D::Vector3& v0,
        const G3D::Vector3& v1,
        const G3D::Vector3& v2,
        const G3D::Vector3& triNormal,
        CylinderIntersection& result)
    {
        bool any = false;
        CylinderIntersection best;

        // Bottom cap as sphere
        {
            CylinderIntersection tmp;
            if (IntersectSphereTriangle3D(cyl.base, cyl.radius, v0, v1, v2, (triNormal.z < 0.0f ? -triNormal : triNormal), tmp))
            {
                best = tmp;
                any = true;
            }
        }
        // Top cap as sphere
        {
            CylinderIntersection tmp;
            G3D::Vector3 topCenter = cyl.getTop();
            if (IntersectSphereTriangle3D(topCenter, cyl.radius, v0, v1, v2, (triNormal.z < 0.0f ? -triNormal : triNormal), tmp))
            {
                if (!any || tmp.contactHeight > best.contactHeight)
                {
                    best = tmp;
                    any = true;
                }
            }
        }

        if (any)
        {
            result = best;
            return true;
        }
        return false;
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
            t = e > 0.0001f ? (f / e) : 0.0f;
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
                            // Ensure upward-facing for contact
                            result.contactNormal = triNormal.z < 0.0f ? -triNormal : triNormal;
                            result.penetrationDepth = 0; // Axis passes through
                            hit = true;
                        }
                    }
                }
            }
        }

        (void)cylTop; // silence unused in some builds
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

        // Calculate triangle normal (use oriented strategy)
        G3D::Vector3 triNormalRaw = CylinderHelpers::CalculateTriangleNormalRaw(v0, v1, v2);
        G3D::Vector3 triNormal = CylinderHelpers::CalculateTriangleNormalOriented(v0, v1, v2);

        // Quick reject: Check if cylinder bounds intersect triangle bounds
        G3D::AABox cylBounds = cyl.getBounds();
        G3D::AABox triBounds(v0, v0);
        triBounds.merge(v1);
        triBounds.merge(v2);

        if (!cylBounds.intersects(triBounds)) {
            return result; // No intersection possible
        }

        // Test cylinder caps against triangle (using sphere-triangle for both caps)
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

        // Handle zero distance as a pure overlap query
        G3D::Vector3 sweepVec = sweepDir * sweepDistance;
        float sweepLen = sweepVec.magnitude();
        float invSweepLen = (sweepLen > 0.0f ? 1.0f / sweepLen : 0.0f);

        // Swept AABB broadphase (start + end merged)
        G3D::AABox sweepBounds = cyl.getBounds();
        if (sweepLen > 0.0f)
        {
            Cylinder endCyl(cyl.base + sweepVec, cyl.axis, cyl.radius, cyl.height);
            sweepBounds.merge(endCyl.getBounds());
        }

        size_t triangleCount = indices.size() / 3;
        if (triangleCount == 0)
            return hits;

        // Parameters for CCD refinement
        const int kCoarseMax = 16;               // max coarse samples
        const int kRefineIter = 8;               // binary search iterations
        const float kMinSegLen = 0.25f;          // target coarse segment length
        const float kPenEps = 1e-5f;             // small epsilon

        for (size_t i = 0; i < triangleCount; ++i)
        {
            const G3D::Vector3& v0 = vertices[indices[i * 3 + 0]];
            const G3D::Vector3& v1 = vertices[indices[i * 3 + 1]];
            const G3D::Vector3& v2 = vertices[indices[i * 3 + 2]];

            // Triangle bounds vs swept bounds
            G3D::AABox triBox(v0, v0); triBox.merge(v1); triBox.merge(v2);
            auto tLo = triBox.low(); auto tHi = triBox.high();
            auto sLo = sweepBounds.low(); auto sHi = sweepBounds.high();

            // Helper lambda to test at param t in [0,1]
            auto testAt = [&](float t, CylinderIntersection& outI) -> bool
            {
                if (t <= 0.0f)
                {
                    outI = IntersectCylinderTriangle(cyl, v0, v1, v2);
                    return outI.hit;
                }
                Cylinder cur(cyl.base + sweepVec * t, cyl.axis, cyl.radius, cyl.height);
                outI = IntersectCylinderTriangle(cur, v0, v1, v2);
                return outI.hit;
            };

            // Check initial overlap first (TOI = 0)
            CylinderIntersection startHit;
            if (testAt(0.0f, startHit))
            {
                CylinderSweepHit h; h.q.hit = true; h.q.distance = 0.0f; h.q.point = startHit.contactPoint; h.q.normal = startHit.contactNormal; h.height = startHit.contactHeight; h.normal = startHit.contactNormal; h.position = startHit.contactPoint; h.walkable = CylinderHelpers::IsWalkableSurface(startHit.contactNormal); h.triangleIndex = (uint32_t)i; h.q.triIndex = h.triangleIndex; hits.push_back(h);
                continue; // Already penetrating; earliest possible
            }

            if (sweepLen <= 0.0f)
            {
                continue; // no movement and not overlapping
            }

            // Determine coarse sample count
            int coarseCount = (int)std::ceil(sweepLen / kMinSegLen);
            if (coarseCount < 1) coarseCount = 1; else if (coarseCount > kCoarseMax) coarseCount = kCoarseMax;

            float tLow = 0.0f;
            float tHigh = -1.0f; // sentinel meaning not found
            CylinderIntersection highHit; // store first colliding intersection

            // Coarse forward march to bracket collision
            for (int s = 1; s <= coarseCount; ++s)
            {
                float t = (float)s / (float)coarseCount; // in (0,1]
                CylinderIntersection isect;
                if (testAt(t, isect))
                {
                    tHigh = t;
                    highHit = isect;
                    break;
                }
            }

            if (tHigh < 0.0f)
            {
                continue; // no hit along path
            }

            // Binary search refine earliest TOI between tLow (free) and tHigh (hit)
            for (int it = 0; it < kRefineIter; ++it)
            {
                float tMid = 0.5f * (tLow + tHigh);
                CylinderIntersection midHit;
                if (testAt(tMid, midHit))
                {
                    tHigh = tMid; // still colliding earlier
                    highHit = midHit;
                }
                else
                {
                    tLow = tMid; // move bracket up
                }
            }

            // Ensure final contact info at refined tHigh
            CylinderIntersection finalHit;
            testAt(tHigh, finalHit); // should be colliding
            if (!finalHit.hit)
            {
                // Fallback to stored highHit if numeric issue
                finalHit = highHit;
            }

            // Build sweep hit
            float toiDist = tHigh * sweepDistance;
            CylinderSweepHit h; h.q.hit = true; h.q.distance = toiDist; h.q.point = finalHit.contactPoint; h.q.normal = finalHit.contactNormal; h.height = finalHit.contactHeight; h.normal = finalHit.contactNormal; h.position = finalHit.contactPoint; h.walkable = CylinderHelpers::IsWalkableSurface(finalHit.contactNormal); h.triangleIndex = (uint32_t)i; h.q.triIndex = h.triangleIndex;
            hits.push_back(h);
        }

        // Suppress CCD summary log; ModelInstance emits final summary
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
        // Select the hit whose contact XY is closest to the cylinder base XY,
        // while still respecting walkable slope and step limits.
        float bestDist2 = std::numeric_limits<float>::max();
        int bestIdx = -1;
        bool foundSurface = false;

        for (size_t i = 0; i < hits.size(); ++i) {
            const auto& hit = hits[i];
            // Skip non-walkable surfaces
            if (!hit.walkable) {
                PHYS_TRACE(PHYS_SURF, "reject hit tri=" << hit.triangleIndex
                    << " inst=" << hit.q.instanceId
                    << " nZ=" << hit.normal.z << " walkable=0");
                continue;
            }

            // Check if surface is within step limits
            float heightDiff = hit.height - currentHeight;

            if (heightDiff > maxStepUp) {
                PHYS_TRACE(PHYS_SURF, "reject hit tri=" << hit.triangleIndex
                    << " inst=" << hit.q.instanceId
                    << " heightDiff=" << heightDiff << " > maxStepUp");
                continue; // Too high to step up
            }

            if (heightDiff < -maxStepDown) {
                PHYS_TRACE(PHYS_SURF, "reject hit tri=" << hit.triangleIndex
                    << " inst=" << hit.q.instanceId
                    << " heightDiff=" << heightDiff << " < -maxStepDown");
                continue; // Too far to step down
            }

            // Compute planar distance^2 from cylinder base XY to hit XY
            float dx = hit.position.x - cyl.base.x;
            float dy = hit.position.y - cyl.base.y;
            float d2 = dx * dx + dy * dy;

            // Prefer the smallest XY distance; tie-breaker prefers higher height
            if (d2 < bestDist2 || (std::abs(d2 - bestDist2) <= 1e-6f && bestIdx >= 0 && hit.height > hits[(size_t)bestIdx].height)) {
                bestDist2 = d2;
                outHeight = hit.height;
                outNormal = hit.normal;
                bestIdx = (int)i;
                foundSurface = true;
            }
        }

        if (foundSurface) {
            const auto& h = hits[(size_t)bestIdx];
            PHYS_TRACE(PHYS_SURF, "best walkable h=" << outHeight
                << " nZ=" << outNormal.z
                << " tri=" << h.triangleIndex
                << " inst=" << h.q.instanceId
                << " dXY2=" << bestDist2);
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
        int bestIdx = -1;
        bool foundSurface = false;

        for (size_t i = 0; i < hits.size(); ++i) {
            const auto& hit = hits[i];
            if (!hit.walkable) {
                PHYS_TRACE(PHYS_SURF, "reject step tri=" << hit.triangleIndex
                    << " inst=" << hit.q.instanceId
                    << " walkable=0");
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
                    bestIdx = (int)i;
                    foundSurface = true;
                }
            } else {
                PHYS_TRACE(PHYS_SURF, "reject step tri=" << hit.triangleIndex
                    << " inst=" << hit.q.instanceId
                    << " heightDiff=" << heightDiff << " not in (0,maxStepUp]");
            }
        }

        if (foundSurface) {
            const auto& h = hits[(size_t)bestIdx];
            PHYS_TRACE(PHYS_SURF, "best stepUp h=" << outHeight
                << " nZ=" << outNormal.z
                << " tri=" << h.triangleIndex
                << " inst=" << h.q.instanceId);
        }

        return foundSurface;
    }
} // namespace VMAP