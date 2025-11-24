#pragma once

// Centralized world <-> internal coordinate conversions
// Internal space matches VMAP conventions: X/Y inverted around map mid, Z untouched.
// Direction/normal conversions only invert X and Y.

#include "Vector3.h"

namespace NavCoord
{
    inline constexpr float MapMid()
    {
        return 0.5f * 64.0f * 533.33333333f;
    }

    // Position conversions
    inline G3D::Vector3 WorldToInternal(const G3D::Vector3& w)
    {
        const float MID = MapMid();
        return G3D::Vector3(MID - w.x, MID - w.y, w.z);
    }

    inline G3D::Vector3 InternalToWorld(const G3D::Vector3& i)
    {
        const float MID = MapMid();
        return G3D::Vector3(MID - i.x, MID - i.y, i.z);
    }

    inline G3D::Vector3 WorldToInternal(float x, float y, float z)
    {
        const float MID = MapMid();
        return G3D::Vector3(MID - x, MID - y, z);
    }

    // Direction/normal conversions (no translation)
    inline G3D::Vector3 WorldDirToInternal(const G3D::Vector3& d)
    {
        return G3D::Vector3(-d.x, -d.y, d.z);
    }

    inline G3D::Vector3 InternalDirToWorld(const G3D::Vector3& d)
    {
        return G3D::Vector3(-d.x, -d.y, d.z);
    }
}
