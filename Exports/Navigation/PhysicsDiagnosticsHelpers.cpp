#include "PhysicsDiagnosticsHelpers.h"
#include <cmath>

namespace PhysicsDiag
{
    static inline float dot3(const G3D::Vector3& a, const G3D::Vector3& b) { return a.x*b.x + a.y*b.y + a.z*b.z; }
    static inline float mag3(const G3D::Vector3& v) { return std::sqrt(v.x*v.x + v.y*v.y + v.z*v.z); }

    G3D::Vector3 DirectionOrZero(const G3D::Vector3& v)
    {
        float m = mag3(v);
        if (m <= 1e-6f) return G3D::Vector3(0,0,0);
        return G3D::Vector3(v.x/m, v.y/m, v.z/m);
    }

    G3D::Vector3 ProjectOnPlane(const G3D::Vector3& v, const G3D::Vector3& n)
    {
        float d = dot3(v, n);
        return G3D::Vector3(v.x - n.x * d, v.y - n.y * d, v.z - n.z * d);
    }

    float PlaneZAtXY(const G3D::Vector3& planeNormal,
                     const G3D::Vector3& planePoint,
                     float x,
                     float y,
                     float currentZ)
    {
        G3D::Vector3 n = planeNormal;
        float D = -dot3(n, planePoint);
        if (std::fabs(n.z) > 1e-6f)
            return (-D - n.x * x - n.y * y) / n.z;
        return currentZ;
    }

    static inline bool ApproximatelyEqual(float a, float b, float eps) { return std::fabs(a-b) <= eps; }
    static inline bool NormalsClose(const G3D::Vector3& n0, const G3D::Vector3& n1, float epsN)
    {
        return ApproximatelyEqual(n0.x, n1.x, epsN) && ApproximatelyEqual(n0.y, n1.y, epsN) && ApproximatelyEqual(n0.z, n1.z, epsN);
    }

    std::vector<ContactPlane> DeduplicatePlanes(const std::vector<ContactPlane>& planes,
                                                float normalEps,
                                                float pointXYEps,
                                                float pointZEps)
    {
        std::vector<ContactPlane> dedup;
        dedup.reserve(planes.size());
        for (const auto& cp : planes) {
            bool found = false;
            for (auto& d : dedup) {
                if (NormalsClose(cp.normal, d.normal, normalEps)) {
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
        return dedup;
    }

    std::pair<bool, ContactPlane> ChoosePrimaryPlane(const std::vector<ContactPlane>& planes,
                                                     bool moving,
                                                     bool startSwimming)
    {
        if (startSwimming) return {false, ContactPlane{}};
        // Prefer penetrating+walkable, then non-penetrating+walkable, else any walkable, else highest penetrating
        for (const auto& cp : planes) {
            if (cp.penetrating && cp.walkable) return {true, cp};
        }
        if (moving) {
            for (const auto& cp : planes) {
                if (!cp.penetrating && cp.walkable) return {true, cp};
            }
            for (const auto& cp : planes) {
                if (cp.walkable) return {true, cp};
            }
        }
        float bestZ = -FLT_MAX; size_t bestIdx = (size_t)-1;
        for (size_t i = 0; i < planes.size(); ++i) {
            const auto& cp = planes[i];
            if (cp.penetrating && cp.point.z > bestZ) { bestZ = cp.point.z; bestIdx = i; }
        }
        if (bestIdx != (size_t)-1) return {true, planes[bestIdx]};
        return {false, ContactPlane{}};
    }

    std::pair<bool, G3D::Vector3> ComputeSlideDir(const ContactPlane& primary,
                                                  const std::vector<ContactPlane>& walkablePlanes,
                                                  const G3D::Vector3& moveDir)
    {
        auto directionOrZero = [](const G3D::Vector3& v) {
            float m = std::sqrt(v.x*v.x + v.y*v.y + v.z*v.z);
            if (m <= 1e-6f) return G3D::Vector3(0,0,0);
            return G3D::Vector3(v.x/m, v.y/m, v.z/m);
        };
        auto dot = [](const G3D::Vector3& a, const G3D::Vector3& b){ return a.x*b.x + a.y*b.y + a.z*b.z; };
        auto cross = [](const G3D::Vector3& a, const G3D::Vector3& b){ return G3D::Vector3(a.y*b.z - a.z*b.y, a.z*b.x - a.x*b.z, a.x*b.y - a.y*b.x); };

        G3D::Vector3 n0 = directionOrZero(primary.normal);
        G3D::Vector3 mv = directionOrZero(moveDir);

        // Try intersection line with a secondary plane
        for (const auto& cp : walkablePlanes) {
            G3D::Vector3 n1 = directionOrZero(cp.normal);
            float dotN = std::fabs(dot(n0, n1));
            if (dotN < 0.995f) {
                G3D::Vector3 lineDir = directionOrZero(cross(n0, n1));
                float proj = dot(mv, lineDir);
                G3D::Vector3 slide = directionOrZero(G3D::Vector3(lineDir.x*proj, lineDir.y*proj, lineDir.z*proj));
                if (std::sqrt(slide.x*slide.x + slide.y*slide.y + slide.z*slide.z) > 1e-6f)
                    return {true, slide};
            }
        }

        // Fallback: project move onto primary plane
        G3D::Vector3 slide = directionOrZero(G3D::Vector3(mv.x - n0.x * dot(mv, n0), mv.y - n0.y * dot(mv, n0), mv.z - n0.z * dot(mv, n0)));
        bool ok = std::sqrt(slide.x*slide.x + slide.y*slide.y + slide.z*slide.z) > 1e-6f;
        return {ok, slide};
    }

    float ClampZToPlane(const G3D::Vector3& planeNormal,
                        const G3D::Vector3& planePoint,
                        float x,
                        float y,
                        float currentZ,
                        float stepUpLimit,
                        float stepDownLimit)
    {
        auto dot = [](const G3D::Vector3& a, const G3D::Vector3& b){ return a.x*b.x + a.y*b.y + a.z*b.z; };
        G3D::Vector3 n = planeNormal; // assume normalized or close
        float D = -dot(n, planePoint);
        float clampZ = currentZ;
        if (std::fabs(n.z) > 1e-6f) {
            clampZ = (-D - n.x * x - n.y * y) / n.z;
        }
        float dzClamp = clampZ - currentZ;
        if (dzClamp > stepUpLimit) return currentZ + stepUpLimit;
        if (dzClamp < -stepDownLimit) return currentZ - stepDownLimit;
        return clampZ;
    }
}
