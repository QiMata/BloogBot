// StaticMapTree.cpp - Enhanced with cylinder collision support + detailed SweepCylinder logging
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "VMapManager2.h"
#include "VMapDefinitions.h"
#include <fstream>
#include <iostream>
#include <algorithm>
#include <cstring>
#include <filesystem>
#include "VMapLog.h"
#include <vector>
#include "CapsuleCollision.h"
#include "CylinderCollision.h" // for CylinderHelpers walkable config
#include "CoordinateTransforms.h"

namespace VMAP
{
    // Helper: squared distance from point to AABox
    static inline float PointToAABBDistSq(const G3D::Vector3& p, const G3D::AABox& box)
    {
        const G3D::Vector3 lo = box.low();
        const G3D::Vector3 hi = box.high();
        float d = 0.0f;
        if (p.x < lo.x) { float t = lo.x - p.x; d += t * t; }
        else if (p.x > hi.x) { float t = p.x - hi.x; d += t * t; }
        if (p.y < lo.y) { float t = lo.y - p.y; d += t * t; }
        else if (p.y > hi.y) { float t = p.y - hi.y; d += t * t; }
        if (p.z < lo.z) { float t = lo.z - p.z; d += t * t; }
        else if (p.z > hi.z) { float t = p.z - hi.z; d += t * t; }
        return d;
    }

    // Helper: approximate minimal squared distance between segment [a,b] and AABox using ternary search on t in [0,1]
    static inline float SegmentAABBDistSq(const G3D::Vector3& a, const G3D::Vector3& b, const G3D::AABox& box)
    {
        // If segment intersects box, distance is zero. Quick slab test.
        G3D::Vector3 dir = b - a;
        G3D::Vector3 invDir(0,0,0);
        invDir.x = (std::abs(dir.x) > 1e-9f) ? 1.0f / dir.x : 0.0f;
        invDir.y = (std::abs(dir.y) > 1e-9f) ? 1.0f / dir.y : 0.0f;
        invDir.z = (std::abs(dir.z) > 1e-9f) ? 1.0f / dir.z : 0.0f;

        float tmin = 0.0f, tmax = 1.0f;
        for (int i = 0; i < 3; ++i)
        {
            float origin = (&a.x)[i];
            float d = (&dir.x)[i];
            float lo = (&box.low().x)[i];
            float hi = (&box.high().x)[i];
            if (std::abs(d) < 1e-9f)
            {
                if (origin < lo || origin > hi) { tmin = 1.0f; tmax = 0.0f; break; }
                continue;
            }
            float t1 = (lo - origin) / d;
            float t2 = (hi - origin) / d;
            if (t1 > t2) std::swap(t1, t2);
            tmin = std::max(tmin, t1);
            tmax = std::min(tmax, t2);
            if (tmin > tmax) break;
        }
        if (tmin <= tmax && tmin <= 1.0f && tmax >= 0.0f)
            return 0.0f; // intersects

        // Ternary search over t in [0,1] for minimal distance squared
        float loT = 0.0f, hiT = 1.0f;
        float best = PointToAABBDistSq(a, box);
        const int ITER = 20;
        for (int it = 0; it < ITER; ++it)
        {
            float t1 = loT + (hiT - loT) / 3.0f;
            float t2 = hiT - (hiT - loT) / 3.0f;
            G3D::Vector3 p1 = a + dir * t1;
            G3D::Vector3 p2 = a + dir * t2;
            float d1 = PointToAABBDistSq(p1, box);
            float d2 = PointToAABBDistSq(p2, box);
            best = std::min(best, std::min(d1, d2));
            if (d1 > d2) loT = t1; else hiT = t2;
        }
        return best;
    }

    class MapRayCallback
    {
    public:
        MapRayCallback(ModelInstance* val) : prims(val), hit(false) {}
        bool operator()(G3D::Ray const& ray, uint32_t entry, float& distance, bool pStopAtFirstHit = true, bool ignoreM2Model = false)
        {
            if (!prims)
                return false;
            if (!prims[entry].iModel)
                return false;
            bool result = prims[entry].intersectRay(ray, distance, pStopAtFirstHit, ignoreM2Model);
            if (result)
            {
                hit = true;
                const ModelInstance& mi = prims[entry];
                G3D::Vector3 hitI = ray.origin() + ray.direction() * distance;
                G3D::Vector3 hitW = NavCoord::InternalToWorld(hitI);
                G3D::Vector3 instPosW = NavCoord::InternalToWorld(mi.iPos);
                const G3D::Vector3& rotDeg = mi.ModelSpawn::iRot;
                PHYS_TRACE(PHYS_CYL, "Raycast hit model='" << mi.name << "' id=" << mi.ID
                    << " adt=" << mi.adtId
                    << " dist=" << distance
                    << " hitW=(" << hitW.x << "," << hitW.y << "," << hitW.z << ")"
                    << " instPosW=(" << instPosW.x << "," << instPosW.y << "," << instPosW.z << ")"
                    << " rotEulerDeg=(" << rotDeg.x << "," << rotDeg.y << "," << rotDeg.z << ")"
                    << " scale=" << mi.iScale);
            }
            return result;
        }
        bool didHit() const { return hit; }
    protected:
        ModelInstance* prims; bool hit; bool los; };

    class StaticMeshView : public CapsuleCollision::TriangleMeshView
    {
    public:
        StaticMeshView(const BIH* tree, const ModelInstance* instances, uint32_t instanceCount)
            : m_tree(tree), m_instances(instances), m_instanceCount(instanceCount) { m_cache.reserve(1024); }
        void query(const CapsuleCollision::AABB& box, int* outIndices, int& count, int maxCount) const override
        {
            count = 0; m_cache.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
                return;
            G3D::Vector3 qlo(box.min.x, box.min.y, box.min.z);
            G3D::Vector3 qhi(box.max.x, box.max.y, box.max.z);
            const G3D::Vector3 qInflate(0.02f,0.02f,0.02f);
            G3D::AABox queryBox(qlo - qInflate, qhi + qInflate);
            const uint32_t cap = std::min<uint32_t>(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap); uint32_t instCount = 0;
            if (!m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap) || instCount == 0)
                return;
            for (uint32_t k=0;k<instCount;++k)
            {
                uint32_t idx = instIdx[k]; if (idx >= m_instanceCount) continue; const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) continue; if (!inst.iBound.intersects(queryBox)) continue;
                G3D::Vector3 wLo = queryBox.low(); G3D::Vector3 wHi = queryBox.high();
                G3D::Vector3 corners[8] = { {wLo.x,wLo.y,wLo.z},{wHi.x,wLo.y,wLo.z},{wLo.x,wHi.y,wLo.z},{wHi.x,wHi.y,wLo.z},{wLo.x,wLo.y,wHi.z},{wHi.x,wLo.y,wHi.z},{wLo.x,wHi.y,wHi.z},{wHi.x,wHi.y,wHi.z} };
                G3D::Vector3 c0 = inst.iInvRot * ((corners[0]-inst.iPos) * inst.iInvScale);
                G3D::AABox modelBox(c0,c0);
                for (int ci=1;ci<8;++ci) modelBox.merge(inst.iInvRot * ((corners[ci]-inst.iPos) * inst.iInvScale));
                const G3D::Vector3 mInflate(0.02f,0.02f,0.02f); modelBox = G3D::AABox(modelBox.low()-mInflate, modelBox.high()+mInflate);
                std::vector<G3D::Vector3> vertices; std::vector<uint32_t> indices; bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                if (!haveBoundsData) { if (!inst.iModel->GetAllMeshData(vertices, indices)) continue; }
                auto emitTriangle = [&](const G3D::Vector3& a,const G3D::Vector3& b,const G3D::Vector3& c)
                { G3D::Vector3 wa=(a*inst.iScale)*inst.iRot+inst.iPos; G3D::Vector3 wb=(b*inst.iScale)*inst.iRot+inst.iPos; G3D::Vector3 wc=(c*inst.iScale)*inst.iRot+inst.iPos; CapsuleCollision::Triangle T; T.a={wa.x,wa.y,wa.z}; T.b={wb.x,wb.y,wb.z}; T.c={wc.x,wc.y,wc.z}; T.doubleSided=true; int triIndex=(int)m_cache.size(); m_cache.push_back(T); if (count < maxCount) outIndices[count++] = triIndex; };
                size_t triCount = indices.size()/3; for (size_t t=0;t<triCount;++t)
                { uint32_t i0=indices[t*3+0], i1=indices[t*3+1], i2=indices[t*3+2]; if (i0>=vertices.size()||i1>=vertices.size()||i2>=vertices.size()) continue; const G3D::Vector3& a=vertices[i0]; const G3D::Vector3& b=vertices[i1]; const G3D::Vector3& c=vertices[i2]; if (!haveBoundsData){ G3D::Vector3 lo=a.min(b).min(c); G3D::Vector3 hi=a.max(b).max(c); if(!G3D::AABox(lo,hi).intersects(modelBox)) continue; } emitTriangle(a,b,c); if (count >= maxCount) break; }
                if (count >= maxCount) break;
            }
        }
        const CapsuleCollision::Triangle& tri(int idx) const override { return m_cache[idx]; }
        int triangleCount() const override { return (int)m_cache.size(); }
    private: const BIH* m_tree; const ModelInstance* m_instances; uint32_t m_instanceCount; mutable std::vector<CapsuleCollision::Triangle> m_cache; };

    StaticMapTree::StaticMapTree(uint32_t mapId, const std::string& basePath)
        : iMapID(mapId), iBasePath(basePath), iIsTiled(false), iTreeValues(nullptr), iNTreeValues(0)
    { if (!iBasePath.empty() && iBasePath.back() != '/' && iBasePath.back() != '\\') iBasePath += "/"; }
    StaticMapTree::~StaticMapTree() { UnloadMap(nullptr); delete[] iTreeValues; }

    bool StaticMapTree::InitMap(const std::string& fname, VMapManager2* vm)
    {
        bool success = true; std::string fullPath = iBasePath + fname; FILE* rf = fopen(fullPath.c_str(), "rb"); if (!rf) return false; char chunk[8];
        if (!readChunk(rf, chunk, VMAP_MAGIC, 8)) success = false; char tiled=0; if (success && fread(&tiled,sizeof(char),1,rf)!=1) success=false; iIsTiled=bool(tiled);
        if (success && !readChunk(rf, chunk, "NODE", 4)) success=false; if (success) success = iTree.readFromFile(rf);
        if (success){ iNTreeValues = iTree.primCount(); if (iNTreeValues > 0) iTreeValues = new ModelInstance[iNTreeValues]; }
        if (success && !readChunk(rf, chunk, "GOBJ", 4)) success=false;
        if (success && !iIsTiled)
        {
            ModelSpawn spawn; while (ModelSpawn::readFromFile(rf, spawn))
            {
                std::shared_ptr<WorldModel> model = nullptr; if (!spawn.name.empty()){ model = vm->acquireModelInstance(iBasePath, spawn.name); if (model) model->setModelFlags(spawn.flags); }
                uint32_t referencedVal; if (fread(&referencedVal,sizeof(uint32_t),1,rf)!=1) break; uint32_t mapped = iTree.mapObjectIndex(referencedVal); if (mapped == 0xFFFFFFFFu) continue; if (!iLoadedSpawns.count(mapped)){ if (mapped >= iNTreeValues) continue; iTreeValues[mapped] = ModelInstance(spawn, model); iLoadedSpawns[mapped] = 1; } else { ++iLoadedSpawns[mapped]; }
            }
        }
        fclose(rf); if (success && iIsTiled) { PreloadAllTiles(vm); } return success; }

    bool StaticMapTree::PreloadAllTiles(VMapManager2* vm)
    { if (!iIsTiled) return true; int tilesLoaded=0, tilesFailed=0; for (uint32_t x=0;x<64;++x) for(uint32_t y=0;y<64;++y){ std::string tilefile=getTileFileName(iMapID,x,y); std::string fullPath=iBasePath+tilefile; if (std::filesystem::exists(fullPath)){ if (LoadMapTile(x,y,vm)) tilesLoaded++; else { tilesFailed++; std::cerr << "[StaticMapTree] Failed to load tile " << tilefile << std::endl; } } } size_t totalModels=0; for (uint32_t i=0;i<iNTreeValues;++i) if (iTreeValues[i].iModel) totalModels++; return tilesFailed==0; }

    bool StaticMapTree::LoadMapTile(uint32_t tileX, uint32_t tileY, VMapManager2* vm)
    { if (!iIsTiled){ iLoadedTiles[packTileID(tileX,tileY)] = false; return true; } uint32_t tileID = packTileID(tileX,tileY); if (iLoadedTiles.find(tileID)!=iLoadedTiles.end()) return true; std::string tilefile=getTileFileName(iMapID,tileX,tileY); std::string fullPath=iBasePath+tilefile; bool fileExists=std::filesystem::exists(fullPath); if (!fileExists){ iLoadedTiles[tileID]=false; return true; } auto fileSize = std::filesystem::file_size(fullPath); FILE* rf=fopen(fullPath.c_str(),"rb"); if(!rf){ iLoadedTiles[tileID]=false; return false; } bool success=true; char chunk[8]; if(!readChunk(rf,chunk,VMAP_MAGIC,8)) success=false; if(success){ uint32_t numSpawns; if (fread(&numSpawns,sizeof(uint32_t),1,rf)!=1) success=false; else { for (uint32_t i=0; i<numSpawns && success; ++i){ ModelSpawn spawn; if(!ModelSpawn::readFromFile(rf,spawn)){ success=false; break; } uint32_t referencedVal; if (fread(&referencedVal,sizeof(uint32_t),1,rf)!=1){ success=false; break; } if(!iTreeValues){ success=false; break; } uint32_t mapped=iTree.mapObjectIndex(referencedVal); if (mapped==0xFFFFFFFFu) continue; if (mapped >= iNTreeValues) continue; if(!iLoadedSpawns.count(mapped)){ std::shared_ptr<WorldModel> model=nullptr; if(!spawn.name.empty()){ model=vm->acquireModelInstance(iBasePath,spawn.name); if(model) model->setModelFlags(spawn.flags); } iTreeValues[mapped]=ModelInstance(spawn,model); iLoadedSpawns[mapped]=1; } else { ++iLoadedSpawns[mapped]; } } } }
        fclose(rf); if(success){ iLoadedTiles[tileID]=true; int totalLoadedModels=0; for(uint32_t i=0;i<iNTreeValues;++i) if(iTreeValues[i].iModel) totalLoadedModels++; } return success; }

    void StaticMapTree::UnloadMapTile(uint32_t tileX, uint32_t tileY, VMapManager2* vm)
    { if(!iIsTiled) return; uint32_t tileID=packTileID(tileX,tileY); auto itr=iLoadedTiles.find(tileID); if(itr==iLoadedTiles.end()) return; iLoadedTiles.erase(itr); }
    void StaticMapTree::UnloadMap(VMapManager2* vm)
    { if(iTreeValues){ for(uint32_t i=0;i<iNTreeValues;++i) iTreeValues[i].setUnloaded(); } iLoadedTiles.clear(); iLoadedSpawns.clear(); }

    CylinderIntersection StaticMapTree::IntersectCylinder(const Cylinder& cyl) const
    { CylinderIntersection result; if (!iTreeValues || iNTreeValues == 0) return result; MapCylinderCallback callback(iTreeValues, cyl); iTree.intersectPoint(cyl.getCenter(), callback); return callback.bestIntersection; }

    // Enhanced logging version
    std::vector<CylinderSweepHit> StaticMapTree::SweepCylinder(const Cylinder& cyl, const G3D::Vector3& sweepDir, float sweepDistance) const
    {
        std::vector<CylinderSweepHit> allHits; if (!iTreeValues || iNTreeValues == 0){ return allHits; }
        G3D::AABox sweepBounds = cyl.getBounds(); Cylinder endCyl(cyl.base + sweepDir * sweepDistance, cyl.axis, cyl.radius, cyl.height); sweepBounds.merge(endCyl.getBounds());
        const uint32_t cap = std::min<uint32_t>(iNTreeValues, 8192); std::vector<uint32_t> indices(cap); uint32_t count=0;

        bool any = iTree.QueryAABB(sweepBounds, indices.data(), count, cap);
        if(!any || count==0){ return allHits; }

        // Detailed candidate logging to help diagnose why BIH returned candidates but no hits
        PHYS_TRACE(PHYS_CYL, "[MapTree::Sweep] BIH candidates=" << count << " sweepBoundsLo=(" << sweepBounds.low().x << "," << sweepBounds.low().y << "," << sweepBounds.low().z << ") sweepBoundsHi=(" << sweepBounds.high().x << "," << sweepBounds.high().y << "," << sweepBounds.high().z << ")");
        size_t toLog = std::min<uint32_t>(count, 8u);
        for (size_t i=0;i<toLog;++i)
        {
            uint32_t idx = indices[i];
            if (idx >= iNTreeValues)
            {
                PHYS_TRACE(PHYS_CYL, "  cand["<<i<<"] idx="<<idx<<" (OOB)");
                continue;
            }
            const ModelInstance& inst = iTreeValues[idx];
            G3D::AABox b = inst.getBounds();
            G3D::Vector3 lo = b.low(); G3D::Vector3 hi = b.high();
            PHYS_TRACE(PHYS_CYL, "  cand["<<i<<"] idx="<<idx<<" name='"<<inst.name<<"' id="<<inst.ID<<" adt="<<inst.adtId
                << " loaded=" << (inst.iModel?1:0)
                << " bLo=("<<lo.x<<","<<lo.y<<","<<lo.z<<") bHi=("<<hi.x<<","<<hi.y<<","<<hi.z<<")");
        }

        MapCylinderSweepCallback callback(iTreeValues, cyl, sweepDir, sweepDistance);
        uint32_t processed=0;
        G3D::Vector3 segA = cyl.base;
        G3D::Vector3 segB = cyl.base + sweepDir * sweepDistance;
        const float eps = 0.02f; // small epsilon
        for (uint32_t i=0;i<count;++i){
            uint32_t idx=indices[i];
            if(idx>=iNTreeValues) continue;
            ModelInstance& inst = iTreeValues[idx];
            if(!inst.iModel) continue;

            // Fast reject: compute minimal distance between instance AABB and sweep segment
            G3D::AABox ib = inst.getBounds();
            float minDistSq = SegmentAABBDistSq(segA, segB, ib);
            float radiusLimit = (cyl.radius + eps);
            if (minDistSq > radiusLimit * radiusLimit)
            {
                // skip this instance - its bounds are farther than the capsule radius + eps from the sweep segment
                continue;
            }

            size_t prev = callback.allHits.size();
            callback(cyl.base, idx);
            size_t added = callback.allHits.size() - prev;
            (void)added; // suppress unused warning if compiled without logs
            ++processed;
        }
        allHits = std::move(callback.allHits); std::sort(allHits.begin(), allHits.end());
        // Single synopsis
        PHYS_TRACE(PHYS_CYL, "[MapTree::Sweep] hits="<<allHits.size()<<" processed="<<processed);
        return allHits;
    }

    bool StaticMapTree::CheckCylinderCollision(const Cylinder& cyl, float& outContactHeight, G3D::Vector3& outContactNormal, ModelInstance** outHitInstance) const
    {
        // Initial guards and basic info
        if (!iTreeValues || iNTreeValues == 0)
        {
            PHYS_TRACE(PHYS_CYL, "[CylCol] abort: no instances (iNTreeValues=" << iNTreeValues << ")");
            return false;
        }

        // Log cylinder parameters and broad-phase bounds
        G3D::AABox bounds = cyl.getBounds();
        PHYS_TRACE(PHYS_CYL, "[CylCol] centerI=(" << cyl.getCenter().x << "," << cyl.getCenter().y << "," << cyl.getCenter().z
            << ") baseI=(" << cyl.base.x << "," << cyl.base.y << "," << cyl.base.z
            << ") axis=(" << cyl.axis.x << "," << cyl.axis.y << "," << cyl.axis.z
            << ") r=" << cyl.radius << " h=" << cyl.height
            << " boundsLo=(" << bounds.low().x << "," << bounds.low().y << "," << bounds.low().z
            << ") hi=(" << bounds.high().x << "," << bounds.high().y << "," << bounds.high().z << ") entries=" << iNTreeValues);

        // Gather BIH candidates using bounds to understand empty hits
        const uint32_t cap = std::min<uint32_t>(iNTreeValues, 8192);
        std::vector<uint32_t> indices(cap); uint32_t count = 0;
        bool any = iTree.QueryAABB(bounds, indices.data(), count, cap);
        if (!any || count == 0)
        {
            PHYS_TRACE(PHYS_CYL, "[CylCol] BIH AABB query returned 0 candidates");
        }
        else
        {
            PHYS_TRACE(PHYS_CYL, "[CylCol] BIH candidates=" << count);
            size_t toLog = std::min<size_t>(count, 8);
            for (size_t i = 0; i < toLog; ++i)
            {
                uint32_t idx = indices[i];
                if (idx >= iNTreeValues) { PHYS_TRACE(PHYS_CYL, "  cand["<<i<<"] idx="<<idx<<" (OOB)"); continue; }
                const ModelInstance& inst = iTreeValues[idx];
                G3D::Vector3 lo = inst.getBounds().low();
                G3D::Vector3 hi = inst.getBounds().high();
                PHYS_TRACE(PHYS_CYL, "  cand["<<i<<"] idx="<<idx<<" name='"<<inst.name<<"' id="<<inst.ID<<" adt="<<inst.adtId
                    << " loaded=" << (inst.iModel?1:0)
                    << " bLo=("<<lo.x<<","<<lo.y<<","<<lo.z<<") bHi=("<<hi.x<<","<<hi.y<<","<<hi.z<<")");
            }
        }

        // Run the existing point-based traversal
        MapCylinderCallback callback(iTreeValues, cyl);
        iTree.intersectPoint(cyl.getCenter(), callback);

        if (callback.bestIntersection.hit)
        {
            outContactHeight = callback.bestIntersection.contactHeight;
            outContactNormal = callback.bestIntersection.contactNormal;
            if (outHitInstance) *outHitInstance = callback.hitInstance;

            const ModelInstance* mi = callback.hitInstance;
            PHYS_TRACE(PHYS_CYL, "[CylCol] HIT h=" << outContactHeight
                << " n=(" << outContactNormal.x << "," << outContactNormal.y << "," << outContactNormal.z << ")"
                << (mi? (std::string(" inst='") + mi->name + "' id=" + std::to_string(mi->ID)).c_str() : " inst=null"));
            return true;
        }

        // If no hit, optionally try per-candidate detailed checks to understand why
        if (any && count > 0)
        {
            int tested = 0, localHits = 0; float bestH = -G3D::inf(); G3D::Vector3 bestN(0,0,1); const ModelInstance* bestInst = nullptr;
            size_t toTry = std::min<uint32_t>(count, 16u);
            for (size_t i = 0; i < toTry; ++i)
            {
                uint32_t idx = indices[i]; if (idx >= iNTreeValues) continue;
                ModelInstance& inst = iTreeValues[idx]; if (!inst.iModel) continue;
                ++tested;
                CylinderIntersection r = inst.IntersectCylinder(cyl);
                if (r.hit)
                {
                    ++localHits;
                    if (r.contactHeight > bestH) { bestH = r.contactHeight; bestN = r.contactNormal; bestInst = &inst; }
                    PHYS_TRACE(PHYS_CYL, "  perInstHit idx="<<idx<<" id="<<inst.ID<<" h="<<r.contactHeight<<" nZ="<<r.contactNormal.z);
                }
                else
                {
                    PHYS_TRACE(PHYS_CYL, "  perInstNoHit idx="<<idx<<" id="<<inst.ID);
                }
            }
            if (localHits > 0)
            {
                PHYS_TRACE(PHYS_CYL, "[CylCol] WARN: intersectPoint reported 0 but per-instance tests found hits="<<localHits
                    << " bestH="<<bestH<<" bestN.z="<<bestN.z<<" inst="<<(bestInst?bestInst->ID:0));
            }
            else
            {
                PHYS_TRACE(PHYS_CYL, "[CylCol] No per-instance hits among first "<<toTry<<" candidates");
            }
        }
        else
        {
            PHYS_TRACE(PHYS_CYL, "[CylCol] No candidates to test per-instance");
        }

        return false;
    }

    bool StaticMapTree::CanCylinderFitAtPosition(const Cylinder& cyl, float tolerance) const
    { if (!iTreeValues || iNTreeValues == 0) return true; const float FOOT_ALLOW=0.20f; const float HEAD_CLEAR_MARGIN=0.30f; const float walkableCosMin = VMAP::CylinderHelpers::GetWalkableCosMin(); Cylinder broad(cyl.base, cyl.axis, cyl.radius + tolerance, cyl.height); CylinderIntersection quickHit = IntersectCylinder(broad); if(!quickHit.hit) return true; float sweepDist = cyl.height + FOOT_ALLOW + 0.10f; Cylinder sweepCyl(G3D::Vector3(cyl.base.x,cyl.base.y,cyl.base.z + cyl.height + 0.05f), cyl.axis, cyl.radius + tolerance * 0.5f, cyl.height); std::vector<CylinderSweepHit> hits = SweepCylinder(sweepCyl, G3D::Vector3(0,0,-1), sweepDist); bool hasAcceptableFloor=false; bool blockingCeiling=false; float nearestCeilingRel=9999.0f; float baseZ=cyl.base.z; for(const auto& h : hits){ float rel = h.height - baseZ; if(rel < -0.05f) continue; if(rel > cyl.height + 0.05f) continue; if(rel <= FOOT_ALLOW && h.walkable && h.normal.z >= walkableCosMin){ hasAcceptableFloor=true; continue; } if(rel >= cyl.height - HEAD_CLEAR_MARGIN && h.normal.z <= 0.3f){ blockingCeiling=true; nearestCeilingRel = std::min(nearestCeilingRel, rel); } }
        if(!hasAcceptableFloor && quickHit.hit){ float qRel = quickHit.contactHeight - baseZ; if(quickHit.contactNormal.z >= walkableCosMin && qRel >= -0.25f && qRel <= cyl.height * 0.6f){ hasAcceptableFloor=true; } else if(qRel >= cyl.height - HEAD_CLEAR_MARGIN && quickHit.contactNormal.z <= 0.3f){ blockingCeiling=true; nearestCeilingRel = std::min(nearestCeilingRel, qRel); } }
        if(!hasAcceptableFloor && !blockingCeiling && hits.empty()) hasAcceptableFloor = true; bool fit = hasAcceptableFloor && !blockingCeiling; LOG_INFO("CanCylinderFitAtPosition SWEEP baseZ="<<baseZ<<" floor="<<(hasAcceptableFloor?1:0)<<" blockCeil="<<(blockingCeiling?1:0)<<" nearestCeilRel="<<(nearestCeilingRel==9999.0f?-1.0f:nearestCeilingRel)<<" h="<<cyl.height<<" r="<<cyl.radius<<" quickRel="<<(quickHit.hit?(quickHit.contactHeight - baseZ):-999.0f)<<" quickNz="<<(quickHit.hit?quickHit.contactNormal.z:-1.0f)<<" hits="<<hits.size()); return fit; }

    bool StaticMapTree::FindCylinderWalkableSurface(const Cylinder& cyl, float currentHeight, float maxStepUp, float maxStepDown, float& outHeight, G3D::Vector3& outNormal) const
    { if (!iTreeValues || iNTreeValues == 0) return false; G3D::Vector3 sweepDir(0,0,-1); float sweepDistance = maxStepUp + maxStepDown; Cylinder sweepCyl(G3D::Vector3(cyl.base.x,cyl.base.y,currentHeight + maxStepUp), cyl.axis, cyl.radius, cyl.height); std::vector<CylinderSweepHit> hits = SweepCylinder(sweepCyl, sweepDir, sweepDistance); return CylinderCollision::FindBestWalkableSurface(cyl, hits, currentHeight, maxStepUp, maxStepDown, outHeight, outNormal); }

    void StaticMapTree::GetCylinderCollisionCandidates(const Cylinder& cyl, std::vector<ModelInstance*>& outInstances) const
    { outInstances.clear(); if (!iTreeValues || iNTreeValues == 0) return; G3D::AABox bounds = cyl.getBounds(); const uint32_t cap = std::min<uint32_t>(iNTreeValues, 8192); std::vector<uint32_t> indices(cap); uint32_t count=0; bool any = iTree.QueryAABB(bounds, indices.data(), count, cap); if(!any || count==0) return; outInstances.reserve(count); for (uint32_t i=0;i<count;++i){ uint32_t idx=indices[i]; if(idx >= iNTreeValues) continue; if(!iTreeValues[idx].iModel) continue; if(!iTreeValues[idx].getBounds().intersects(bounds)) continue; outInstances.push_back(&iTreeValues[idx]); } }

    bool StaticMapTree::isInLineOfSight(const G3D::Vector3& pos1, const G3D::Vector3& pos2, bool ignoreM2Model) const
    { if(!iTreeValues || iNTreeValues==0) return true; float maxDist=(pos2-pos1).magnitude(); if(maxDist < 0.001f) return true; G3D::Ray ray = G3D::Ray::fromOriginAndDirection(pos1,(pos2-pos1)/maxDist); float intersectDist = maxDist; bool hit = getIntersectionTime(ray, intersectDist, true, ignoreM2Model); return !hit; }

    bool StaticMapTree::getObjectHitPos(const G3D::Vector3& pos1, const G3D::Vector3& pos2, G3D::Vector3& resultHitPos, float modifyDist) const
    { if(!iTreeValues || iNTreeValues==0){ resultHitPos = pos2; return false; } float maxDist = (pos2-pos1).magnitude(); if (maxDist < 0.001f){ resultHitPos=pos2; return false; } G3D::Vector3 dir=(pos2-pos1)/maxDist; G3D::Ray ray=G3D::Ray::fromOriginAndDirection(pos1,dir); float distance=maxDist; if(getIntersectionTime(ray,distance,true,false)){ resultHitPos=pos1+dir*distance; if(modifyDist>0 && distance > modifyDist) resultHitPos = pos1 + dir * (distance - modifyDist); return true; } resultHitPos=pos2; return false; }

    float StaticMapTree::getHeight(const G3D::Vector3& pos, float maxSearchDist) const
    { float height = -G3D::inf(); if(!iTreeValues || iNTreeValues==0) return height; int loadedCount=0; for(uint32_t i=0;i<iNTreeValues;++i) if(iTreeValues[i].iModel) loadedCount++; G3D::Vector3 rayStart=pos; G3D::Ray ray(rayStart, G3D::Vector3(0,0,-1)); float distance = maxSearchDist * 2; if(getIntersectionTime(ray,distance,false,false)) height = pos.z - distance; return height; }

    bool StaticMapTree::getAreaInfo(G3D::Vector3& pos, uint32_t& flags, int32_t& adtId, int32_t& rootId, int32_t& groupId) const
    { class AreaInfoCallback { public: AreaInfoCallback(ModelInstance* val):prims(val){} void operator()(const G3D::Vector3& point,uint32_t entry){ if(!prims || !prims[entry].iModel) return; prims[entry].intersectPoint(point,aInfo);} ModelInstance* prims; AreaInfo aInfo; }; AreaInfoCallback cb(iTreeValues); iTree.intersectPoint(pos, cb); if (cb.aInfo.result){ flags=cb.aInfo.flags; adtId=cb.aInfo.adtId; rootId=cb.aInfo.rootId; groupId=cb.aInfo.groupId; pos.z=cb.aInfo.ground_Z; return true; } return false; }

    bool StaticMapTree::GetLocationInfo(const G3D::Vector3& pos, LocationInfo& info) const
    { if(!iTreeValues || iNTreeValues==0) return false; class LocationInfoCallback { public: LocationInfoCallback(ModelInstance* val):prims(val),found(false){} void operator()(const G3D::Vector3& point,uint32_t entry){ if(!prims || !prims[entry].iModel) return; if(prims[entry].GetLocationInfo(point,tempInfo)) found=true; } ModelInstance* prims; LocationInfo tempInfo; bool found; }; LocationInfoCallback cb(iTreeValues); iTree.intersectPoint(pos, cb); if(cb.found){ info = cb.tempInfo; return true; } return false; }

    bool StaticMapTree::getIntersectionTime(G3D::Ray const& pRay, float& pMaxDist, bool pStopAtFirstHit, bool ignoreM2Model) const
    { float distance = pMaxDist; MapRayCallback cb(iTreeValues); iTree.intersectRay(pRay, cb, distance, pStopAtFirstHit, ignoreM2Model); if(cb.didHit()) pMaxDist = distance; return cb.didHit(); }

    uint32_t StaticMapTree::packTileID(uint32_t tileX, uint32_t tileY) { return (tileX << 16) | tileY; }
    void StaticMapTree::unpackTileID(uint32_t ID, uint32_t& tileX, uint32_t& tileY) { tileX=(ID>>16); tileY=(ID & 0xFFFF); }
    std::string StaticMapTree::getTileFileName(uint32_t mapID, uint32_t tileX, uint32_t tileY) { char buffer[256]; snprintf(buffer,sizeof(buffer),"%03u_%02u_%02u.vmtile", mapID, tileX, tileY); return std::string(buffer); }
    bool StaticMapTree::CanLoadMap(const std::string& vmapPath, uint32_t mapID, uint32_t tileX, uint32_t tileY) { std::string fileName = vmapPath + getTileFileName(mapID, tileX, tileY); FILE* rf=fopen(fileName.c_str(),"rb"); if(!rf) return false; fclose(rf); return true; }
    bool StaticMapTree::isTiled() const { return iIsTiled; }
    uint32_t StaticMapTree::numLoadedTiles() const { return iLoadedTiles.size(); }

    bool StaticMapTree::isUnderModel(G3D::Vector3& pos, float* outDist, float* inDist) const
    { if(!iTreeValues || iNTreeValues==0) return false; G3D::Ray ray(pos, G3D::Vector3(0,0,1)); float maxDist=100.0f; auto callback=[this](const G3D::Ray& r,uint32_t idx,float& d,bool stopAtFirst,bool ignoreM2){ if(!iTreeValues || idx>=iNTreeValues || !iTreeValues[idx].iModel) return false; return iTreeValues[idx].intersectRay(r,d,stopAtFirst,ignoreM2); }; float distance=maxDist; iTree.intersectRay(ray, callback, distance, true, false); if(distance < maxDist){ if(outDist) *outDist=distance; if(inDist) *inDist=0.0f; return true; } return false; }

    ModelInstance* StaticMapTree::FindCollisionModel(const G3D::Vector3& pos1, const G3D::Vector3& pos2)
    { if(!iTreeValues || iNTreeValues==0) return nullptr; float maxDist=(pos2-pos1).magnitude(); if(maxDist < 0.001f) return nullptr; G3D::Ray ray=G3D::Ray::fromOriginAndDirection(pos1,(pos2-pos1)/maxDist); ModelInstance* hitModel=nullptr; float closestDist=maxDist; for(uint32_t i=0;i<iNTreeValues;++i){ if(iTreeValues[i].iModel){ float dist=maxDist; if(iTreeValues[i].intersectRay(ray, dist, true, false) && dist < closestDist){ closestDist=dist; hitModel=&iTreeValues[i]; } } } return hitModel; }

#ifdef MMAP_GENERATOR
    void StaticMapTree::getModelInstances(ModelInstance*& models, uint32_t& count){ models=iTreeValues; count=iNTreeValues; }
#endif
}