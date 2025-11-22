// WorldModel.h - Complete with cylinder collision support
#pragma once

#include <vector>
#include <memory>
#include <string>
#include "Vector3.h"
#include "AABox.h"
#include "Ray.h"
#include "BIH.h"
#include "G3D/BoundsTrait.h"
#include "CoordinateTransforms.h"

namespace VMAP
{
    // Forward declarations
    struct AreaInfo
    {
        bool result;
        float ground_Z;
        uint32_t flags;
        int32_t adtId;
        int32_t rootId;
        int32_t groupId;

        AreaInfo() : result(false), ground_Z(-G3D::inf()), flags(0),
            adtId(-1), rootId(-1), groupId(-1) {
        }
    };

    struct GroupLocationInfo
    {
        const class GroupModel* hitModel;
        int32_t rootId;

        GroupLocationInfo() : hitModel(nullptr), rootId(-1) {}
    };

    // MeshTriangle structure for collision detection
    struct MeshTriangle
    {
        uint32_t idx0;
        uint32_t idx1;
        uint32_t idx2;
    };

    // WmoLiquid class for liquid handling
    class WmoLiquid
    {
    private:
        uint32_t iTilesX;
        uint32_t iTilesY;
        G3D::Vector3 iCorner;
        uint32_t iType;
        float* iHeight;
        uint8_t* iFlags;

    public:
        WmoLiquid(uint32_t width, uint32_t height, const G3D::Vector3& corner, uint32_t type);
        WmoLiquid(const WmoLiquid& other);
        ~WmoLiquid();

        WmoLiquid& operator=(const WmoLiquid& other);

        bool GetLiquidHeight(const G3D::Vector3& pos, float& liqHeight) const;
        uint32_t GetType() const { return iType; }

        static bool readFromFile(FILE* rf, WmoLiquid*& liquid);

    private:
        WmoLiquid() : iTilesX(0), iTilesY(0), iCorner(), iType(0), iHeight(nullptr), iFlags(nullptr) {}
    };

    // GroupModel class definition with cylinder collision support
    class GroupModel
    {
    private:
        G3D::AABox iBound;
        uint32_t iMogpFlags;
        uint32_t iGroupWMOID;
        std::vector<G3D::Vector3> vertices;
        std::vector<MeshTriangle> triangles;
        BIH meshTree;
        WmoLiquid* iLiquid;

        GroupModel(const GroupModel& other) = delete;
        GroupModel& operator=(const GroupModel& other) = delete;

        // Last triangle index hit by the most recent IntersectRay call (local to this group's triangles)
        mutable int m_lastHitTriangle = -1;

    public:
        GroupModel() : iMogpFlags(0), iGroupWMOID(0), iLiquid(nullptr) {}
        GroupModel(uint32_t mogpFlags, uint32_t groupWMOID, const G3D::AABox& bound)
            : iBound(bound), iMogpFlags(mogpFlags), iGroupWMOID(groupWMOID), iLiquid(nullptr) {
        }

        // Move constructor
        GroupModel(GroupModel&& other) noexcept
            : iBound(std::move(other.iBound)),
            iMogpFlags(other.iMogpFlags),
            iGroupWMOID(other.iGroupWMOID),
            vertices(std::move(other.vertices)),
            triangles(std::move(other.triangles)),
            meshTree(std::move(other.meshTree)),
            iLiquid(other.iLiquid)
        {
            other.iLiquid = nullptr;
        }

        ~GroupModel() { delete iLiquid; }

        void setLiquidData(WmoLiquid* liquid) { iLiquid = liquid; }
        uint32_t IntersectRay(const G3D::Ray& ray, float& distance, bool stopAtFirstHit, bool ignoreM2Model) const;
        bool IsInsideObject(const G3D::Vector3& pos, const G3D::Vector3& down, float& z_dist) const;
        bool GetLiquidLevel(const G3D::Vector3& pos, float& liqHeight) const;
        uint32_t GetLiquidType() const;
        bool readFromFile(FILE* rf);
        const G3D::AABox& GetBound() const { return iBound; }
        uint32_t GetMogpFlags() const { return iMogpFlags; }
        uint32_t GetWmoID() const { return iGroupWMOID; }

        // Mesh data access for external collision testing
        const std::vector<G3D::Vector3>& GetVertices() const { return vertices; }
        const std::vector<MeshTriangle>& GetTriangles() const { return triangles; }

        static bool IntersectTriangle(const MeshTriangle& tri,
            std::vector<G3D::Vector3>::const_iterator vertices,
            const G3D::Ray& ray, float& distance);

        struct GModelRayCallback
        {
            GModelRayCallback(std::vector<MeshTriangle> const& tris, std::vector<G3D::Vector3> const& vert, GroupModel* parentPtr) :
                vertices(vert.begin()), triangles(tris.begin()), hit(0), parent(parentPtr), lastHitIndex(-1) {
            }

            bool operator()(G3D::Ray const& ray, uint32_t entry, float& distance, bool /*stopAtFirstHit*/, bool /*ignoreM2Model*/)
            {
                // Log ray and triangle entry
                LOG_INFO("[GModelRayCallback] Ray origin=(" << ray.origin().x << "," << ray.origin().y << "," << ray.origin().z << ") dir=(" << ray.direction().x << "," << ray.direction().y << "," << ray.direction().z << ")");
                LOG_INFO("[GModelRayCallback] Testing triangle entry " << entry << " with distance " << distance);

                const MeshTriangle& mt = triangles[entry];
                G3D::Vector3 mv0 = vertices[mt.idx0];
                G3D::Vector3 mv1 = vertices[mt.idx1];
                G3D::Vector3 mv2 = vertices[mt.idx2];
                G3D::Vector3 iv0 = mv0;
                G3D::Vector3 iv1 = mv1;
                G3D::Vector3 iv2 = mv2;
                G3D::Vector3 wv0 = NavCoord::InternalToWorld(iv0);
                G3D::Vector3 wv1 = NavCoord::InternalToWorld(iv1);
                G3D::Vector3 wv2 = NavCoord::InternalToWorld(iv2);

                // Compute triangle normal and area
                G3D::Vector3 triNormal = (mv1 - mv0).cross(mv2 - mv0);
                float triArea = triNormal.magnitude() * 0.5f;
                if (triArea > 0.00001f) triNormal = triNormal / (2.0f * triArea);
                else triNormal = {0,0,0};

                // Log triangle details
                LOG_INFO("[GModelRayCallback] Triangle " << entry << " v0_local=(" << mv0.x << "," << mv0.y << "," << mv0.z << ") v1_local=(" << mv1.x << "," << mv1.y << "," << mv1.z << ") v2_local=(" << mv2.x << "," << mv2.y << "," << mv2.z << ")");
                LOG_INFO("[GModelRayCallback] Triangle " << entry << " v0_internal=(" << iv0.x << "," << iv0.y << "," << iv0.z << ") v1_internal=(" << iv1.x << "," << iv1.y << "," << iv1.z << ") v2_internal=(" << iv2.x << "," << iv2.y << "," << iv2.z << ")");
                LOG_INFO("[GModelRayCallback] Triangle " << entry << " v0_world=(" << wv0.x << "," << wv0.y << "," << wv0.z << ") v1_world=(" << wv1.x << "," << wv1.y << "," << wv1.z << ") v2_world=(" << wv2.x << "," << wv2.y << "," << wv2.z << ")");
                LOG_INFO("[GModelRayCallback] Triangle " << entry << " normal=(" << triNormal.x << "," << triNormal.y << "," << triNormal.z << ") area=" << triArea);

                bool result = GroupModel::IntersectTriangle(mt, vertices, ray, distance);

                if (result)
                {
                    ++hit;
                    lastHitIndex = (int)entry;
                    if (parent) parent->m_lastHitTriangle = lastHitIndex;

                    // Compute intersection point and barycentric coordinates
                    G3D::Vector3 hitPoint = ray.origin() + ray.direction() * distance;
                    // Barycentric coordinates calculation
                    G3D::Vector3 v0v1 = mv1 - mv0;
                    G3D::Vector3 v0v2 = mv2 - mv0;
                    G3D::Vector3 v0p = hitPoint - mv0;
                    float d00 = v0v1.dot(v0v1);
                    float d01 = v0v1.dot(v0v2);
                    float d11 = v0v2.dot(v0v2);
                    float d20 = v0p.dot(v0v1);
                    float d21 = v0p.dot(v0v2);
                    float denom = d00 * d11 - d01 * d01;
                    float v = (d11 * d20 - d01 * d21) / denom;
                    float w = (d00 * d21 - d01 * d20) / denom;
                    float u = 1.0f - v - w;

                    LOG_INFO("[GModelRayCallback] Triangle " << entry << " HIT! Total hits: " << hit
                        << " New distance: " << distance
                        << " GroupWMO=" << (parent ? parent->GetWmoID() : 0) << " TriLocal=" << entry
                        << " hitPoint=(" << hitPoint.x << "," << hitPoint.y << "," << hitPoint.z << ")"
                        << " barycentric=(u=" << u << ", v=" << v << ", w=" << w << ")"
                        << " normal=(" << triNormal.x << "," << triNormal.y << "," << triNormal.z << ") area=" << triArea);
                }
                else
                {
                    LOG_TRACE("[GModelRayCallback] Triangle " << entry << " miss");
                }

                return result;
            }

            std::vector<G3D::Vector3>::const_iterator vertices;
            std::vector<MeshTriangle>::const_iterator triangles;
            uint32_t hit;
            int lastHitIndex;
            GroupModel* parent;
        };

        int GetLastHitTriangle() const { return m_lastHitTriangle; }
    };

    // WorldModel class with cylinder collision support
    class WorldModel
    {
    public:
        WorldModel() : RootWMOID(0), modelFlags(0) {}

        void setRootWmoID(uint32_t id) { RootWMOID = id; }
        bool IntersectRay(const G3D::Ray& ray, float& distance, bool stopAtFirstHit, bool ignoreM2Model) const;
        bool IntersectPoint(const G3D::Vector3& p, const G3D::Vector3& down, float& dist, AreaInfo& info) const;
        bool IsUnderObject(const G3D::Vector3& p, const G3D::Vector3& up, bool m2,
            float* outDist = nullptr, float* inDist = nullptr) const;
        bool GetLocationInfo(const G3D::Vector3& p, const G3D::Vector3& down, float& dist,
            GroupLocationInfo& info) const;
        bool readFile(const std::string& filename);
        void setModelFlags(uint32_t newFlags) { modelFlags = newFlags; }
        uint32_t getModelFlags() const { return modelFlags; }

        // Mesh data extraction for external use
        bool GetAllMeshData(std::vector<G3D::Vector3>& outVertices,
            std::vector<uint32_t>& outIndices) const;
        bool GetMeshDataInBounds(const G3D::AABox& bounds,
            std::vector<G3D::Vector3>& outVertices,
            std::vector<uint32_t>& outIndices) const;

        // Access a specific group model (read-only) for triangle enrichment
        inline const GroupModel* GetGroupModel(uint32_t index) const
        {
            return (index < groupModels.size()) ? &groupModels[index] : nullptr;
        }

    protected:
        uint32_t RootWMOID;
        std::vector<GroupModel> groupModels;
        BIH groupTree;
        uint32_t modelFlags;
    };
} // namespace VMAP

template<> struct BoundsTrait<VMAP::GroupModel>
{
    static void getBounds(const VMAP::GroupModel& obj, G3D::AABox& out)
    {
        out = obj.GetBound();
    }
};