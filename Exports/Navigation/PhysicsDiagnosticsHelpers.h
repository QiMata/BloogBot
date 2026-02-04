#pragma once
#include "Vector3.h"
#include <vector>

namespace PhysicsDiag
{
    struct ContactPlane
    {
        G3D::Vector3 normal;
        G3D::Vector3 point;
        bool walkable = false;
        bool penetrating = false;
    };

    // Normalize vector or return zero when magnitude is too small
    G3D::Vector3 DirectionOrZero(const G3D::Vector3& v);

    // Project vector v onto plane with normal n: v - n*dot(v,n)
    G3D::Vector3 ProjectOnPlane(const G3D::Vector3& v, const G3D::Vector3& n);

    // Compute plane Z at given XY for plane defined by normal and point; fallback to currentZ when n.z ~ 0
    float PlaneZAtXY(const G3D::Vector3& planeNormal,
                     const G3D::Vector3& planePoint,
                     float x,
                     float y,
                     float currentZ);

    // Deduplicate nearly-coplanar planes (pure helper)
    std::vector<ContactPlane> DeduplicatePlanes(const std::vector<ContactPlane>& planes,
                                                float normalEps,
                                                float pointXYEps,
                                                float pointZEps);

    // Choose a primary plane from manifold (pure selection logic)
    // Returns pair(hasPrimary, primaryPlane)
    std::pair<bool, ContactPlane> ChoosePrimaryPlane(const std::vector<ContactPlane>& planes,
                                                     bool moving,
                                                     bool startSwimming);

    // Compute slide direction given primary plane normal and intended move dir (pure)
    // If a secondary plane is available, prefer intersection line; otherwise project onto primary.
    std::pair<bool, G3D::Vector3> ComputeSlideDir(const ContactPlane& primary,
                                                  const std::vector<ContactPlane>& walkablePlanes,
                                                  const G3D::Vector3& moveDir);

    // Clamp Z to the plane at given XY, honoring step up/down limits.
    float ClampZToPlane(const G3D::Vector3& planeNormal,
                        const G3D::Vector3& planePoint,
                        float x,
                        float y,
                        float currentZ,
                        float stepUpLimit,
                        float stepDownLimit);
}
