/**
 * MaNGOS is a full featured server for World of Warcraft, supporting
 * the following clients: 1.12.x, 2.4.3, 3.3.5a, 4.3.4a and 5.4.8
 *
 * Copyright (C) 2005-2025 MaNGOS <https://www.getmangos.eu>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 * World of Warcraft, and all World of Warcraft or Warcraft art, images,
 * and lore are copyrighted by Blizzard Entertainment, Inc.
 */

#ifndef MANGOS_H_MODELINSTANCE
#define MANGOS_H_MODELINSTANCE

#include <cstdio>
#include <string>
#include "Vec3Ray.h"

namespace VMAP
{
    class WorldModel;
    struct AreaInfo;
    struct LocationInfo;

    /**
     * @brief Enumeration for model flags.
     */
    enum ModelFlags
    {
        MOD_M2 = 1,
        MOD_WORLDSPAWN = 1 << 1,
        MOD_HAS_BOUND = 1 << 2
    };

    /**
     * @brief Class representing a model spawn.
     */
    class ModelSpawn
    {
    public:
        // mapID, tileX, tileY, Flags, ID, Pos, Rot, Scale, Bound_lo, Bound_hi, name
        unsigned int flags; /**< Model flags. */
        unsigned short adtId; /**< ADT ID. */
        unsigned int ID; /**< Model ID. */
        Vec3 iPos; /**< Position of the model. */
        Vec3 iRot; /**< Rotation of the model. */
        float iScale; /**< Scale of the model. */
        AABox iBound; /**< Bounding box of the model. */
        std::string name; /**< Name of the model. */

        /**
         * @brief Equality operator for ModelSpawn.
         *
         * @param other The other ModelSpawn to compare with.
         * @return bool True if the ModelSpawns are equal, false otherwise.
         */
        bool operator==(const ModelSpawn& other) const { return ID == other.ID; }

        /**
         * @brief Gets the bounding box of the model.
         *
         * @return const AABox& The bounding box of the model.
         */
        const AABox& getBounds() const { return iBound; }

        /**
         * @brief Reads a ModelSpawn from a file.
         *
         * @param rf The file to read from.
         * @param spawn The ModelSpawn to read into.
         * @return bool True if the read was successful, false otherwise.
         */
        static bool ReadFromFile(FILE* rf, ModelSpawn& spawn);

        /**
         * @brief Writes a ModelSpawn to a file.
         *
         * @param rw The file to write to.
         * @param spawn The ModelSpawn to write.
         * @return bool True if the write was successful, false otherwise.
         */
        static bool WriteToFile(FILE* rw, const ModelSpawn& spawn);
    };

    /**
     * @brief Class representing a model instance.
     */
    class ModelInstance : public ModelSpawn
    {
    public:
        /**
         * @brief Default constructor for ModelInstance.
         */
        ModelInstance() : iInvRot(Matrix3::identity()), iInvScale(1.0f), iModel(nullptr) {}

        /**
         * @brief Constructor for ModelInstance.
         *
         * @param spawn The model spawn data.
         * @param model The world model.
         */
        ModelInstance(const ModelSpawn& spawn, WorldModel* model);

        /**
         * @brief Sets the model instance as unloaded.
         */
        void setUnloaded() { iModel = 0; }

        /**
         * @brief Intersects a ray with the model instance.
         *
         * @param pRay The ray to intersect.
         * @param pMaxDist The maximum distance to check.
         * @param pStopAtFirstHit Whether to stop at the first hit.
         * @return bool True if an intersection is found, false otherwise.
         */
        bool intersectRay(const Ray& pRay, float& pMaxDist, bool pStopAtFirstHit) const;

        /**
         * @brief Retrieves area information for a given position.
         *
         * @param p The position to check.
         * @param info The area information.
         */
        void GetAreaInfo(const Vec3& p, AreaInfo& info) const;

        /**
         * @brief Retrieves location information for a given position.
         *
         * @param p The position to check.
         * @param info The location information.
         * @return bool True if location information was found, false otherwise.
         */
        bool GetLocationInfo(const Vec3& p, LocationInfo& info) const;

        /**
         * @brief Retrieves the liquid level at a given position.
         *
         * @param p The position to check.
         * @param info The location information.
         * @param liqHeight The liquid height.
         * @return bool True if the liquid level was found, false otherwise.
         */
        bool GetLiquidLevel(const Vec3& p, LocationInfo& info, float& liqHeight) const;
    protected:
        Matrix3 iInvRot; /**< Inverse rotation matrix. */
        float iInvScale; /**< Inverse scale. */
        WorldModel* iModel; /**< Pointer to the world model. */

#ifdef MMAP_GENERATOR
    public:
        /**
         * @brief Gets the world model.
         *
         * @return WorldModel* Pointer to the world model.
         */
        WorldModel* const getWorldModel();
#endif
    };
} // namespace VMAP

#endif // MANGOS_H_MODELINSTANCE