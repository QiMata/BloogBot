// CylinderCollision.h - Cleaned version with only necessary functionality
#pragma once

#include "Vector3.h"
#include "AABox.h"
#include <cmath>

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
            : base(b), axis(a), radius(r), height(h) {
        }

        G3D::Vector3 getTop() const { return base + axis * height; }
        G3D::Vector3 getCenter() const { return base + axis * (height * 0.5f); }
    };

    // Helper functions that are actually used
    namespace CylinderHelpers
    {
        // Check if surface is walkable (used in physics engine)
        inline bool IsWalkableSurface(const G3D::Vector3& normal) {
            return normal.z >= 0.6428f; // cos(50°)
        }

        // Step height validation (actively used)
        enum StepResult {
            STEP_BLOCKED,
            STEP_UP,
            STEP_DOWN,
            STEP_FALL
        };

        inline StepResult CheckStepHeight(
            float currentHeight,
            float newHeight,
            float stepUpMax = 2.3f,
            float stepDownMax = 4.0f)
        {
            float heightDiff = newHeight - currentHeight;

            if (heightDiff > 0)
            {
                if (heightDiff <= stepUpMax)
                    return STEP_UP;
                else
                    return STEP_BLOCKED;
            }
            else
            {
                float dropDistance = -heightDiff;
                if (dropDistance <= stepDownMax)
                    return STEP_DOWN;
                else
                    return STEP_FALL;
            }
        }

        // Calculate triangle normal (might be used for ground detection)
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