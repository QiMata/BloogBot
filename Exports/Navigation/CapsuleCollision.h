// CapsuleCollision.h - Header-only C++17 capsule collision utilities (WickedEngine-style)
// Standalone, no STL containers, only <cmath>. All functions inline.
//
// This module provides:
//  - Basic math types (Vec3) and operations
//  - Triangle helpers (closest points, plane)
//  - AABB helpers and broad-phase utilities
//  - Discrete intersection tests: Sphere-Triangle, Capsule-Triangle, Capsule-Capsule
//  - Resolution helpers (slide and pop-out)
//  - Simple scene query interface to integrate with an external mesh provider
//
// Notes:
//  - Robustness: guard divides by zero, use small epsilons, normalizeSafe with fallback {0,1,0}
//  - No heap allocations. No STL containers. No external libraries.
//  - Intended for character controller capsule vs triangle meshes similar to WickedEngine approach.
//
// Usage:
//  - Include this header in a translation unit. Implement your own TriangleMeshView.
//  - Call sceneIntersectCapsuleDiscrete() to test discrete collisions, or moveCapsuleWithCCD() for simple CCD stepping.
//
#ifndef CAPSULE_COLLISION_H
#define CAPSULE_COLLISION_H

#include <cmath>
#include <cstdint>
#include "Vector3.h" // expose engine Vector3 in Wicked helpers

namespace CapsuleCollision
{
    // Small constants for numerical stability
    static const float EPSILON = 1e-6f;
    static const float LARGE_EPS = 1e-4f;
    // Treat touching as overlap tolerance (about 1 mm in world units)
    static const float TOUCH_EPS = 1e-3f;

    // Basic math helpers without STL
    inline float cc_min(float a, float b) { return a < b ? a : b; }
    inline float cc_max(float a, float b) { return a > b ? a : b; }
    inline float cc_clamp(float x, float a, float b) { return x < a ? a : (x > b ? b : x); }
    inline float cc_abs(float x) { return x < 0.0f ? -x : x; }
    inline float cc_sqrt(float x) { return x <= 0.0f ? 0.0f : (float)std::sqrt((double)x); }

    struct Vec3
    {
        float x, y, z;

        inline Vec3() : x(0), y(0), z(0) {}
        inline Vec3(float X, float Y, float Z) : x(X), y(Y), z(Z) {}

        inline Vec3 operator+(const Vec3& o) const { return Vec3(x + o.x, y + o.y, z + o.z); }
        inline Vec3 operator-(const Vec3& o) const { return Vec3(x - o.x, y - o.y, z - o.z); }
        inline Vec3 operator*(float s) const { return Vec3(x * s, y * s, z * s); }
        inline Vec3 operator/(float s) const { float inv = cc_abs(s) > EPSILON ? (1.0f / s) : 0.0f; return Vec3(x * inv, y * inv, z * inv); }
        inline Vec3& operator+=(const Vec3& o) { x += o.x; y += o.y; z += o.z; return *this; }
        inline Vec3& operator-=(const Vec3& o) { x -= o.x; y -= o.y; z -= o.z; return *this; }
        inline Vec3 operator-() const { return Vec3(-x, -y, -z); } // unary minus operator

        inline float length2() const { return x * x + y * y + z * z; }
        inline float length() const { return cc_sqrt(length2()); }

        static inline float dot(const Vec3& a, const Vec3& b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        static inline Vec3 cross(const Vec3& a, const Vec3& b) { return Vec3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }

        static inline Vec3 normalizeSafe(const Vec3& v, const Vec3& fallback = Vec3(0, 1, 0))
        {
            float l2 = v.length2();
            if (l2 > EPSILON * EPSILON)
            {
                float invL = 1.0f / cc_sqrt(l2);
                return Vec3(v.x * invL, v.y * invL, v.z * invL);
            }
            return fallback;
        }
    };

    inline Vec3 operator*(float s, const Vec3& v) { return v * s; }

    struct Capsule { Vec3 p0; Vec3 p1; float r; };
    struct Triangle { Vec3 a, b, c; bool doubleSided = false; uint32_t collisionMask = 0xFFFFFFFFu; };

    // Simple query filter for channel-based collision masking
    struct QueryFilter {
        uint32_t includeMask = 0xFFFFFFFFu;
        uint32_t excludeMask = 0u;
        inline bool Allow(const Triangle& t) const {
            return (t.collisionMask & includeMask) && ((t.collisionMask & excludeMask) == 0);
        }
    };

    struct AABB { Vec3 min, max; };
    struct Hit { bool hit = false; float depth = 0; Vec3 normal = Vec3(0, 1, 0); Vec3 point = Vec3(0, 0, 0); int triIndex = -1; bool startPenetrating = false; };

    // -- Geometry helpers --

    // Closest point on segment AB from point P, returns the point and optionally tOut in [0,1]
    inline Vec3 closestPointOnSegment(Vec3 a, Vec3 b, Vec3 p, float* tOut = nullptr)
    {
        Vec3 ab = b - a;
        float ab2 = ab.length2();
        float t = 0.0f;
        if (ab2 > EPSILON)
        {
            t = Vec3::dot(p - a, ab) / ab2;
            t = cc_clamp(t, 0.0f, 1.0f);
        }
        if (tOut) *tOut = t;
        return a + ab * t;
    }

    // Compute triangle plane (normalized N) and plane constant d so that N·X + d = 0
    inline void trianglePlane(const Triangle& T, Vec3& N, float& d)
    {
        Vec3 ab = T.b - T.a;
        Vec3 ac = T.c - T.a;
        Vec3 n = Vec3::cross(ab, ac);
        float n2 = n.length2();
        if (n2 <= EPSILON * EPSILON)
        {
            N = Vec3(0, 1, 0);
            d = -Vec3::dot(N, T.a);
            return;
        }
        N = Vec3::normalizeSafe(n);
        d = -Vec3::dot(N, T.a);
    }

    inline float signedDistanceToPlane(const Vec3& p, const Vec3& N, float d)
    {
        return Vec3::dot(N, p) + d;
    }

    // Closest point on triangle ABC to point P using barycentric technique (Ericson 5.1.5)
    inline Vec3 closestPointOnTriangle(const Triangle& T, const Vec3& p, float* u = nullptr, float* v = nullptr, float* w = nullptr)
    {
        const Vec3& a = T.a;
        const Vec3& b = T.b;
        const Vec3& c = T.c;
        // Check vertex regions against A, B, C
        Vec3 ab = b - a;
        Vec3 ac = c - a;
        Vec3 ap = p - a;
        float d1 = Vec3::dot(ab, ap);
        float d2 = Vec3::dot(ac, ap);
        if (d1 <= 0.0f && d2 <= 0.0f)
        {
            if (u) *u = 1.0f; if (v) *v = 0.0f; if (w) *w = 0.0f; return a; // bary (1,0,0)
        }

        // Check vertex region B
        Vec3 bp = p - b;
        float d3 = Vec3::dot(ab, bp);
        float d4 = Vec3::dot(ac, bp);
        if (d3 >= 0.0f && d4 <= d3)
        {
            if (u) *u = 0.0f; if (v) *v = 1.0f; if (w) *w = 0.0f; return b; // bary (0,1,0)
        }

        // Check edge region AB
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            float v_ab = d1 / (d1 - d3 + (cc_abs(d1 - d3) <= EPSILON ? EPSILON : 0.0f));
            Vec3 q = a + ab * v_ab; // bary (1-v_ab, v_ab, 0)
            if (u) *u = 1.0f - v_ab; if (v) *v = v_ab; if (w) *w = 0.0f;
            return q;
        }

        // Check vertex region C
        Vec3 cp = p - c;
        float d5 = Vec3::dot(ab, cp);
        float d6 = Vec3::dot(ac, cp);
        if (d6 >= 0.0f && d5 <= d6)
        {
            if (u) *u = 0.0f; if (v) *v = 0.0f; if (w) *w = 1.0f; return c; // bary (0,0,1)
        }

        // Check edge region AC
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            float denom_ac = (d2 - d6);
            denom_ac = cc_abs(denom_ac) <= EPSILON ? (denom_ac < 0.0f ? -EPSILON : EPSILON) : denom_ac;
            float w_ac = d2 / denom_ac;
            Vec3 q = a + ac * w_ac; // bary (1-w_ac, 0, w_ac)
            if (u) *u = 1.0f - w_ac; if (v) *v = 0.0f; if (w) *w = w_ac;
            return q;
        }

        // Check edge region BC
        Vec3 bc = c - b;
        float va = d3 * d6 - d5 * d4;
        if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
        {
            float denom_bc = (d4 - d3) + (d5 - d6);
            denom_bc = cc_abs(denom_bc) <= EPSILON ? (denom_bc < 0.0f ? -EPSILON : EPSILON) : denom_bc;
            float w_bc = (d4 - d3) / denom_bc;
            Vec3 q = b + bc * w_bc; // bary (0, 1-w_bc, w_bc)
            if (u) *u = 0.0f; if (v) *v = 1.0f - w_bc; if (w) *w = w_bc;
            return q;
        }

        // Inside face region. Compute using barycentrics
        float sum = va + vb + vc;
        if (cc_abs(sum) <= EPSILON)
        {
            if (u) *u = 1.0f; if (v) *v = 0.0f; if (w) *w = 0.0f; return a; // fallback to A
        }
        float denom = 1.0f / sum;
        float v_bary = vb * denom;
        float w_bary = vc * denom;
        float u_bary = 1.0f - v_bary - w_bary;
        if (u) *u = u_bary; if (v) *v = v_bary; if (w) *w = w_bary;
        return a * u_bary + b * v_bary + c * w_bary;
    }

    inline AABB aabbMerge(const AABB& A, const AABB& B)
    {
        AABB R;
        R.min.x = cc_min(A.min.x, B.min.x);
        R.min.y = cc_min(A.min.y, B.min.y);
        R.min.z = cc_min(A.min.z, B.min.z);
        R.max.x = cc_max(A.max.x, B.max.x);
        R.max.y = cc_max(A.max.y, B.max.y);
        R.max.z = cc_max(A.max.z, B.max.z);
        return R;
    }

    inline bool aabbOverlaps(const AABB& A, const AABB& B)
    {
        if (A.max.x < B.min.x || A.min.x > B.max.x) return false;
        if (A.max.y < B.min.y || A.min.y > B.max.y) return false;
        if (A.max.z < B.min.z || A.min.z > B.max.z) return false;
        return true;
    }

    inline AABB aabbFromCapsule(const Capsule& C)
    {
        AABB box;
        box.min.x = cc_min(C.p0.x, C.p1.x) - C.r;
        box.min.y = cc_min(C.p0.y, C.p1.y) - C.r;
        box.min.z = cc_min(C.p0.z, C.p1.z) - C.r;
        box.max.x = cc_max(C.p0.x, C.p1.x) + C.r;
        box.max.y = cc_max(C.p0.y, C.p1.y) + C.r;
        box.max.z = cc_max(C.p0.z, C.p1.z) + C.r;
        return box;
    }

    inline AABB aabbFromCapsuleSwept(const Capsule& start, const Capsule& end)
    {
        // Tight AABB covering both start and end capsule positions (union of their individual AABBs).
        // This avoids performing multiple external inflations when used with MapMeshView which already
        // applies a small padding internally.
        return aabbMerge(aabbFromCapsule(start), aabbFromCapsule(end));
    }

    inline void aabbInflate(AABB& B, float amount)
    {
        if (amount <= 0.0f) return;
        B.min.x -= amount; B.min.y -= amount; B.min.z -= amount;
        B.max.x += amount; B.max.y += amount; B.max.z += amount;
    }

    // -- Primitive tests --

    // Closest points between two segments P1(s) = p1 + s*(q1-p1), s in [0,1] and P2(t) = p2 + t*(q2-p2), t in [0,1]
    inline void closestPointsBetweenSegments(const Vec3& p1, const Vec3& q1, const Vec3& p2, const Vec3& q2,
                                             float& sOut, float& tOut, Vec3& c1, Vec3& c2)
    {
        Vec3 d1 = q1 - p1; // Direction vector of segment S1
        Vec3 d2 = q2 - p2; // Direction vector of segment S2
        Vec3 r = p1 - p2;
        float a = Vec3::dot(d1, d1); // Squared length of segment S1
        float e = Vec3::dot(d2, d2); // Squared length of segment S2
        float f = Vec3::dot(d2, r);

        float s = 0.0f;
        float t = 0.0f;

        if (a <= EPSILON && e <= EPSILON)
        {
            // Both segments are points
            s = 0.0f; t = 0.0f;
            c1 = p1; c2 = p2;
            sOut = s; tOut = t; return;
        }
        if (a <= EPSILON)
        {
            // First segment is a point
            s = 0.0f;
            if (e <= EPSILON)
            {
                t = 0.0f;
            }
            else
            {
                t = cc_clamp(f / e, 0.0f, 1.0f);
            }
        }
        else
        {
            float c = Vec3::dot(d1, r);
            if (e <= EPSILON)
            {
                // Second segment is a point
                t = 0.0f;
                s = cc_clamp(-c / a, 0.0f, 1.0f);
            }
            else
            {
                float b = Vec3::dot(d1, d2);
                float denom = a * e - b * b;
                if (cc_abs(denom) > EPSILON)
                {
                    s = cc_clamp((b * f - c * e) / denom, 0.0f, 1.0f);
                }
                else
                {
                    s = 0.0f; // Parallel, choose zero
                }
                t = (b * s + f) / e;
                if (t < 0.0f)
                {
                    t = 0.0f; s = cc_clamp(-c / a, 0.0f, 1.0f);
                }
                else if (t > 1.0f)
                {
                    t = 1.0f; s = cc_clamp((b - c) / a, 0.0f, 1.0f);
                }
            }
        }
        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
        sOut = s;
        tOut = t;
    }

    // Sphere-triangle intersection via closest point on triangle
    inline bool intersectSphereTriangle(const Vec3& center, float radius, const Triangle& T, Hit& out)
    {
        // Plane cull: if sphere center farther than radius from triangle plane, it cannot overlap the triangle plane
        Vec3 Ntri; float dtri; trianglePlane(T, Ntri, dtri);
        float signedDist = signedDistanceToPlane(center, Ntri, dtri);
        if (cc_abs(signedDist) > radius + TOUCH_EPS)
            return false;

        float u, v, w;
        Vec3 q = closestPointOnTriangle(T, center, &u, &v, &w);
        Vec3 d = center - q;
        float dist2 = d.length2();
        float rEff = radius + TOUCH_EPS;
        float r2 = rEff * rEff;
        if (dist2 > r2)
            return false;

        float dist = cc_sqrt(dist2);
        Vec3 n = dist > EPSILON ? (d / (dist > EPSILON ? dist : 1.0f)) : Ntri;
        if (T.doubleSided)
        {
            // Orient normal from triangle towards sphere center when double-sided
            if (Vec3::dot(Ntri, n) < 0.0f) Ntri = Ntri * -1.0f;
        }
        // If very close, prefer triangle normal
        if (dist <= LARGE_EPS)
            n = Ntri;

        out.hit = true;
        // Keep true geometric penetration depth without TOUCH_EPS bias
        out.depth = radius - dist;
        out.normal = Vec3::normalizeSafe(n);
        out.point = q;
        return true;
    }

    // Compute closest points between a segment and a triangle; returns onSeg and onTri
    inline bool closestPoints_Segment_Triangle(const Vec3& s0, const Vec3& s1, const Triangle& T, Vec3& onSeg, Vec3& onTri)
    {
        // 1) Check if segment intersects the triangle plane inside the triangle
        Vec3 N; float d;
        trianglePlane(T, N, d);
        Vec3 dir = s1 - s0;
        float denom = Vec3::dot(N, dir);
        bool found = false;
        float bestDist2 = 1e30f;

        if (cc_abs(denom) > EPSILON)
        {
            float t = -(Vec3::dot(N, s0) + d) / denom; // param along segment
            if (t >= 0.0f && t <= 1.0f)
            {
                Vec3 p = s0 + dir * t; // intersection point with plane
                // Check if p is inside triangle via barycentric
                float u, v, w;
                Vec3 q = closestPointOnTriangle(T, p, &u, &v, &w);
                // If p is inside, closest is zero distance on plane
                Vec3 diff = p - q;
                if (diff.length2() <= LARGE_EPS * LARGE_EPS)
                {
                    onSeg = p;
                    onTri = q;
                    return true;
                }
            }
        }

        // 2) Consider segment endpoints to triangle interior
        Vec3 q0 = closestPointOnTriangle(T, s0);
        Vec3 d0 = s0 - q0;
        float dist2_0 = d0.length2();
        if (dist2_0 < bestDist2)
        {
            bestDist2 = dist2_0;
            onSeg = s0; onTri = q0; found = true;
        }
        Vec3 q1 = closestPointOnTriangle(T, s1);
        Vec3 d1 = s1 - q1;
        float dist2_1 = d1.length2();
        if (dist2_1 < bestDist2)
        {
            bestDist2 = dist2_1; onSeg = s1; onTri = q1; found = true;
        }

        // 3) Segment to triangle edges distances
        float s, t;
        Vec3 c1, c2;
        // Edge AB
        closestPointsBetweenSegments(s0, s1, T.a, T.b, s, t, c1, c2);
        Vec3 diff = c1 - c2; float dist2 = diff.length2();
        if (dist2 < bestDist2)
        {
            bestDist2 = dist2; onSeg = c1; onTri = c2; found = true;
        }
        // Edge BC
        closestPointsBetweenSegments(s0, s1, T.b, T.c, s, t, c1, c2);
        diff = c1 - c2; dist2 = diff.length2();
        if (dist2 < bestDist2)
        {
            bestDist2 = dist2; onSeg = c1; onTri = c2; found = true;
        }
        // Edge CA
        closestPointsBetweenSegments(s0, s1, T.c, T.a, s, t, c1, c2);
        diff = c1 - c2; dist2 = diff.length2();
        if (dist2 < bestDist2)
        {
            bestDist2 = dist2; onSeg = c1; onTri = c2; found = true;
        }

        return found;
    }

    // -- Capsule tests --

    inline bool intersectCapsuleTriangle(const Capsule& C, const Triangle& T, Hit& out)
    {
        // Quick plane cull against the capsule central axis (useful for vertical walls):
        // If the capsule axis is parallel to the triangle plane and the axis-to-plane distance > radius,
        // then the capsule cannot overlap that triangle.
        Vec3 Ntri; float dtri; trianglePlane(T, Ntri, dtri);
        Vec3 axis = C.p1 - C.p0;
        float axisLen2 = axis.length2();
        Vec3 dir = axisLen2 > EPSILON*EPSILON ? (axis / cc_sqrt(axisLen2)) : Vec3(0, 1, 0);
        float denom = Vec3::dot(Ntri, dir);
        if (cc_abs(denom) <= EPSILON)
        {
            // Parallel: distance from any axis point to plane is constant
            float lineDist = cc_abs(signedDistanceToPlane(C.p0, Ntri, dtri));
            if (lineDist > C.r)
                return false;
        }

        Vec3 onSeg, onTri;
        if (!closestPoints_Segment_Triangle(C.p0, C.p1, T, onSeg, onTri))
            return false;
        Vec3 d = onSeg - onTri;
        float dist2 = d.length2();
        float r = C.r;
        float r2 = r * r;
        if (dist2 > r2)
            return false;

        float dist = cc_sqrt(dist2);
        Vec3 n;
        if (dist > EPSILON)
        {
            n = d / (dist > 0.0f ? dist : 1.0f);
        }
        else
        {
            // If extremely close, fall back to triangle normal
            n = Ntri;
        }
        // If double sided, make sure normal points from triangle towards capsule
        if (T.doubleSided)
        {
            if (Vec3::dot(n, Ntri) < 0.0f) n = n * -1.0f;
        }

        out.hit = true;
        out.depth = r - dist;
        out.normal = Vec3::normalizeSafe(n);
        out.point = onTri; // Closest point on triangle
        return true;
    }

    inline bool intersectCapsuleCapsule(const Capsule& A, const Capsule& B, Hit& out)
    {
        float s, t; Vec3 cA, cB;
        closestPointsBetweenSegments(A.p0, A.p1, B.p0, B.p1, s, t, cA, cB);
        Vec3 d = cA - cB;
        float dist2 = d.length2();
        float rsum = A.r + B.r;
        if (dist2 > rsum * rsum)
            return false;
        float dist = cc_sqrt(dist2);
        Vec3 n = dist > EPSILON ? (d / (dist > 0.0f ? dist : 1.0f)) : Vec3(0, 1, 0);
        out.hit = true;
        out.depth = rsum - dist;
        out.normal = Vec3::normalizeSafe(n);
        out.point = (cA + cB) * 0.5f;
        return true;
    }

    // -- Resolution helpers --

    struct ResolveConfig { float penetrationSlack = 1e-4f; float groundCosMin = 0.3f; Vec3 up = Vec3(0, 1, 0); float contactOffset = 0.02f; };

    inline Vec3 projectAndSlide(Vec3 v, Vec3 n)
    {
        // Simple projection: remove the component along normal without preserving magnitude
        n = Vec3::normalizeSafe(n);
        float vn = Vec3::dot(v, n);
        return v - n * vn;
    }

    // Add a contact normal to a small manifold (unique up to a cosine similarity threshold). Returns new count.
    inline int manifoldAddNormal(Vec3* normals, int count, int maxCount, const Vec3& n, float cosThreshold = 0.98f)
    {
        if (maxCount <= 0) return 0;
        Vec3 nn = Vec3::normalizeSafe(n);
        for (int i = 0; i < count; ++i)
        {
            Vec3 ni = Vec3::normalizeSafe(normals[i]);
            float c = Vec3::dot(nn, ni);
            if (cc_abs(c) >= cosThreshold)
                return count; // too similar, skip
        }
        if (count < maxCount)
        {
            normals[count] = nn;
            return count + 1;
        }
        return count;
    }

    // Iterative orthogonal projection onto the intersection of contact planes.
    // Only removes components moving into a plane (vn < 0). Optionally preserves magnitude.
    inline Vec3 projectVelocityAgainstNormals(Vec3 v, const Vec3* normals, int count, int iterations = 3, bool preserveMagnitude = true)
    {
        if (count <= 0) return v;
        float targetLen = v.length();
        for (int it = 0; it < iterations; ++it)
        {
            for (int i = 0; i < count; ++i)
            {
                Vec3 n = Vec3::normalizeSafe(normals[i]);
                float vn = Vec3::dot(v, n);
                if (vn < 0.0f)
                {
                    v = v - n * vn; // project out into-plane component
                }
            }
        }
        if (preserveMagnitude)
        {
            float l = v.length();
            if (l > EPSILON && targetLen > EPSILON)
            {
                v = v * (targetLen / l);
            }
        }
        return v;
    }

    inline bool resolveCapsuleHit(Capsule& C, const Hit& h, Vec3& inOutVelocity, const ResolveConfig& cfg)
    {
        if (!h.hit)
            return false;
        Vec3 n = Vec3::normalizeSafe(h.normal, cfg.up);
        // Compute correction distance to guarantee a minimum separation (contactOffset) when penetrating.
        float pop = 0.0f;
        if (h.depth > 0.0f)
        {
            // Penetrating: push out by penetration depth plus desired contact offset and small slack
            pop = h.depth + cfg.contactOffset + cfg.penetrationSlack;
        }
        else
        {
            // Speculative (no penetration) contact: only apply tiny slack if any
            pop = cfg.penetrationSlack;
        }
        if (pop > 0.0f)
        {
            Vec3 correction = n * pop;
            C.p0 += correction;
            C.p1 += correction;
        }
        // Slide velocity along contact plane
        inOutVelocity = projectAndSlide(inOutVelocity, n);
        return true;
    }

    // -- Mesh interface --

    struct TriangleMeshView {
        virtual void query(const AABB& box, int* outIndices, int& count, int maxCount) const = 0;
        virtual const Triangle& tri(int idx) const = 0;
        virtual int triangleCount() const = 0;
        virtual ~TriangleMeshView() = default;
    };

    // -- Scene queries --

    inline bool sceneIntersectCapsuleDiscrete(const Capsule& C, const TriangleMeshView& mesh, Hit& out, int* triScratch, int triCap)
    {
        out = Hit();
        AABB box = aabbFromCapsule(C);
        aabbInflate(box, 0.01f);
        int count = 0;
        mesh.query(box, triScratch, count, triCap);
        bool any = false;
        float bestDepth = -1.0f;
        for (int i = 0; i < count; ++i)
        {
            int idx = triScratch[i];
            const Triangle& T = mesh.tri(idx);
            Hit h;
            if (intersectCapsuleTriangle(C, T, h))
            {
                if (h.depth > bestDepth)
                {
                    bestDepth = h.depth;
                    any = true;
                    out = h;
                    out.triIndex = idx;
                }
            }
        }
        return any;
    }

    // Forward declaration for analytic per-triangle sweep
    inline bool capsuleTriangleSweep(const Capsule& start, const Vec3& vel, const Triangle& T,
                                     float& toi, Vec3& normal, Vec3& impactPoint);

    // Analytic sweep test: moving capsule (translation only) vs single triangle.
    // Returns true if collision occurs for param t in [0,1]. Outputs time of impact (toi), contact normal and impact point on triangle.
    // Handles planar face, edge and vertex cases via decomposition to segment/segment and segment/point sweeps.
    inline bool capsuleTriangleSweep(const Capsule& start, const Vec3& vel, const Triangle& T,
                                     float& toi, Vec3& normal, Vec3& impactPoint)
    {
        toi = 1.0f;
        bool hit = false;
        normal = Vec3(0, 1, 0);
        impactPoint = T.a;

        // Early out: zero velocity
        if (vel.length2() <= EPSILON * EPSILON)
        {
            Hit h;
            if (intersectCapsuleTriangle(start, T, h)) {
                toi = 0.0f; normal = h.normal; impactPoint = h.point; return true;
            }
            return false;
        }

        // If initially overlapping, report t=0
        {
            Hit h;
            if (intersectCapsuleTriangle(start, T, h)) {
                toi = 0.0f; normal = h.normal; impactPoint = h.point; return true;
            }
        }

        // Helper: point-in-triangle (barycentric)
        auto pointInTriangle = [&](const Vec3& p) -> bool {
            Vec3 v0 = T.b - T.a, v1 = T.c - T.a, v2 = p - T.a;
            float d00 = Vec3::dot(v0, v0), d01 = Vec3::dot(v0, v1), d11 = Vec3::dot(v1, v1);
            float d20 = Vec3::dot(v2, v0), d21 = Vec3::dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (cc_abs(denom) <= EPSILON) return false;
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;
            const float tol = -LARGE_EPS * 10.0f;
            return (u >= tol && v >= tol && w >= tol);
        };

        // 1. Face contact: sweep segment against triangle plane, check if intersection is inside triangle
        Vec3 Ntri; float dtri; trianglePlane(T, Ntri, dtri);
        float seg0_dist = signedDistanceToPlane(start.p0, Ntri, dtri);
        float seg1_dist = signedDistanceToPlane(start.p1, Ntri, dtri);
        float vel_dot_n = Vec3::dot(Ntri, vel);

        // Only consider if moving towards the plane
        if (cc_abs(vel_dot_n) > EPSILON) {
            // Find t where either endpoint touches the plane (expand by radius)
            float t0 = (start.r - seg0_dist) / vel_dot_n;
            float t1 = (start.r - seg1_dist) / vel_dot_n;
            float t2 = (-start.r - seg0_dist) / vel_dot_n;
            float t3 = (-start.r - seg1_dist) / vel_dot_n;
            float t_candidates[4] = { t0, t1, t2, t3 };
            for (int i = 0; i < 4; ++i) {
                float t = t_candidates[i];
                if (t < 0.0f || t > 1.0f) continue;
                Vec3 p0 = start.p0 + vel * t;
                Vec3 p1 = start.p1 + vel * t;
                // For each t, check if the segment between p0 and p1 intersects the triangle
                // Project both endpoints onto the plane
                float d0 = signedDistanceToPlane(p0, Ntri, dtri);
                float d1 = signedDistanceToPlane(p1, Ntri, dtri);
                Vec3 q0 = p0 - Ntri * d0;
                Vec3 q1 = p1 - Ntri * d1;
                // Check if either endpoint is inside triangle
                if (pointInTriangle(q0) || pointInTriangle(q1)) {
                    if (t < toi) {
                        toi = t;
                        normal = Ntri;
                        impactPoint = pointInTriangle(q0) ? q0 : q1;
                        hit = true;
                    }
                }
            }
        }

        // 2. Edge contact: sweep capsule segment against each triangle edge (segment-segment sweep)
        auto segmentSegmentSweep = [](const Vec3& p0, const Vec3& p1, const Vec3& q0, const Vec3& q1, const Vec3& v, float radius, float& outT, Vec3& outN, Vec3& outP) -> bool {
            // Relative motion: move q0-q1 by -v, p0-p1 is stationary
            Vec3 r = q0 - p0;
            Vec3 d1 = p1 - p0;
            Vec3 d2 = q1 - q0;
            Vec3 v_rel = -v;
            float t = 0.0f;
            float s, tseg;
            Vec3 c1, c2;
            // Use conservative advancement (subdivide [0,1])
            const int steps = 8;
            bool found = false;
            float bestT = 1.0f;
            for (int i = 0; i <= steps; ++i) {
                float alpha = (float)i / steps;
                Vec3 q0m = q0 + v_rel * alpha;
                Vec3 q1m = q1 + v_rel * alpha;
                closestPointsBetweenSegments(p0, p1, q0m, q1m, s, tseg, c1, c2);
                Vec3 diff = c1 - c2;
                float dist2 = diff.length2();
                if (dist2 <= (radius + EPSILON) * (radius + EPSILON)) {
                    if (alpha < bestT) {
                        bestT = alpha;
                        outT = alpha;
                        outN = Vec3::normalizeSafe(diff);
                        outP = c2;
                        found = true;
                    }
                }
            }
            return found;
        };

        Vec3 triEdges[3][2] = { {T.a, T.b}, {T.b, T.c}, {T.c, T.a} };
        for (int i = 0; i < 3; ++i) {
            float tEdge; Vec3 nEdge, pEdge;
            if (segmentSegmentSweep(start.p0, start.p1, triEdges[i][0], triEdges[i][1], vel, start.r, tEdge, nEdge, pEdge)) {
                if (tEdge < toi) {
                    toi = tEdge;
                    normal = nEdge;
                    impactPoint = pEdge;
                    hit = true;
                }
            }
        }

        // 3. Vertex contact: sweep capsule segment against each triangle vertex (point-segment sweep)
        auto pointSegmentSweep = [](const Vec3& seg0, const Vec3& seg1, const Vec3& v, const Vec3& pt, float radius, float& outT, Vec3& outN, Vec3& outP) -> bool {
            // Move pt by -v, segment is stationary
            Vec3 v_rel = -v;
            const int steps = 8;
            bool found = false;
            float bestT = 1.0f;
            for (int i = 0; i <= steps; ++i) {
                float alpha = (float)i / steps;
                Vec3 ptm = pt + v_rel * alpha;
                float tseg;
                Vec3 c = closestPointOnSegment(seg0, seg1, ptm, &tseg);
                Vec3 diff = c - ptm;
                float dist2 = diff.length2();
                if (dist2 <= (radius + EPSILON) * (radius + EPSILON)) {
                    if (alpha < bestT) {
                        bestT = alpha;
                        outT = alpha;
                        outN = Vec3::normalizeSafe(diff);
                        outP = ptm;
                        found = true;
                    }
                }
            }
            return found;
        };

        Vec3 triVerts[3] = { T.a, T.b, T.c };
        for (int i = 0; i < 3; ++i) {
            float tVert; Vec3 nVert, pVert;
            if (pointSegmentSweep(start.p0, start.p1, vel, triVerts[i], start.r, tVert, nVert, pVert)) {
                if (tVert < toi) {
                    toi = tVert;
                    normal = nVert;
                    impactPoint = pVert;
                    hit = true;
                }
            }
        }

        // Finalize
        if (hit && toi >= 0.0f && toi <= 1.0f) {
            // Orient normal to oppose motion
            if (Vec3::dot(normal, vel) > 0.0f) normal = normal * -1.0f;
            normal = Vec3::normalizeSafe(normal, Ntri);
            return true;
        }
        return false;
    }

    // BEGIN: Wicked helpers
    using Vector3 = G3D::Vector3; // re-expose engine Vector3 without changing ABI

    inline float Dot(const Vector3& a, const Vector3& b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    inline Vector3 Cross(const Vector3& a, const Vector3& b)
    {
        return Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }
    inline float LengthSq(const Vector3& v) { return Dot(v, v); }
    inline float Length(const Vector3& v) { return cc_sqrt(LengthSq(v)); }
    inline Vector3 NormalizeSafe(const Vector3& v, const Vector3& fallback = Vector3(0, 1, 0))
    {
        float l2 = LengthSq(v);
        if (l2 > EPSILON * EPSILON)
        {
            float invL = 1.0f / cc_sqrt(l2);
            return v * invL; // relies on Vector3::operator*(float)
        }
        return fallback;
    }
    // END: Wicked helpers
}

#endif // CAPSULE_COLLISION_H
