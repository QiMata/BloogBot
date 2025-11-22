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
        MapRayCallback(ModelInstance* val) : prims(val), hit(false) {
            hitCount = 0;
            closestDist = std::numeric_limits<float>::max();
            closestModel = nullptr;
        }
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
                ++hitCount;
                if (distance < closestDist)
                {
                    closestDist = distance;
                    closestModel = &prims[entry];
                }
            }
            return result;
        }
        bool didHit() const { return hit; }
        int getHitCount() const { return hitCount; }
        const ModelInstance* getClosestModel() const { return closestModel; }
        float getClosestDist() const { return closestDist; }
    protected:
        ModelInstance* prims; bool hit; bool los;
        int hitCount;
        float closestDist;
        const ModelInstance* closestModel;
    };

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

    bool StaticMapTree::isInLineOfSight(const G3D::Vector3& pos1, const G3D::Vector3& pos2, bool ignoreM2Model) const
    { if(!iTreeValues || iNTreeValues==0) return true; float maxDist=(pos2-pos1).magnitude(); if(maxDist < 0.001f) return true; G3D::Ray ray = G3D::Ray::fromOriginAndDirection(pos1,(pos2-pos1)/maxDist); float intersectDist = maxDist; bool hit = getIntersectionTime(ray, intersectDist, true, ignoreM2Model); return !hit; }

    bool StaticMapTree::getObjectHitPos(const G3D::Vector3& pos1, const G3D::Vector3& pos2, G3D::Vector3& resultHitPos, float modifyDist) const
    { if(!iTreeValues || iNTreeValues==0){ resultHitPos = pos2; return false; } float maxDist = (pos2-pos1).magnitude(); if (maxDist < 0.001f){ resultHitPos=pos2; return false; } G3D::Vector3 dir=(pos2-pos1)/maxDist; G3D::Ray ray=G3D::Ray::fromOriginAndDirection(pos1,dir); float distance=maxDist; if(getIntersectionTime(ray,distance,true,false)){ resultHitPos=pos1+dir*distance; if(modifyDist>0 && distance > modifyDist) resultHitPos = pos1 + dir * (distance - modifyDist); return true; } resultHitPos=pos2; return false; }

    float StaticMapTree::getHeight(const G3D::Vector3& pos, float maxSearchDist) const
    {
        // WoW emulator style: single downward raycast, return closest hit Z below query point
        if (!iTreeValues || iNTreeValues == 0)
            return -std::numeric_limits<float>::infinity();
        G3D::Ray ray(pos, G3D::Vector3(0,0,-1));
        float distance = maxSearchDist * 2;
        if (getIntersectionTime(ray, distance, false, false))
        {
            // Return Z of hit point (internal coordinates)
            return pos.z - distance;
        }
        return -std::numeric_limits<float>::infinity();
    }

    bool StaticMapTree::getAreaInfo(G3D::Vector3& pos, uint32_t& flags, int32_t& adtId, int32_t& rootId, int32_t& groupId) const
    { class AreaInfoCallback { public: AreaInfoCallback(ModelInstance* val):prims(val){} void operator()(const G3D::Vector3& point,uint32_t entry){ if(!prims || !prims[entry].iModel) return; prims[entry].intersectPoint(point,aInfo);} ModelInstance* prims; AreaInfo aInfo; }; AreaInfoCallback cb(iTreeValues); iTree.intersectPoint(pos, cb); if (cb.aInfo.result){ flags=cb.aInfo.flags; adtId=cb.aInfo.adtId; rootId=cb.aInfo.rootId; groupId=cb.aInfo.groupId; pos.z=cb.aInfo.ground_Z; return true; } return false; }

    bool StaticMapTree::GetLocationInfo(const G3D::Vector3& pos, LocationInfo& info) const
    { if(!iTreeValues || iNTreeValues==0) return false; class LocationInfoCallback { public: LocationInfoCallback(ModelInstance* val):prims(val),found(false){} void operator()(const G3D::Vector3& point,uint32_t entry){ if(!prims || !prims[entry].iModel) return; if(prims[entry].GetLocationInfo(point,tempInfo)) found=true; } ModelInstance* prims; LocationInfo tempInfo; bool found; }; LocationInfoCallback cb(iTreeValues); iTree.intersectPoint(pos, cb); if(cb.found){ info = cb.tempInfo; return true; } return false; }

    bool StaticMapTree::getIntersectionTime(G3D::Ray const& pRay, float& pMaxDist, bool pStopAtFirstHit, bool ignoreM2Model) const
    {
        float distance = pMaxDist;
        MapRayCallback cb(iTreeValues);
        iTree.intersectRay(pRay, cb, distance, pStopAtFirstHit, ignoreM2Model);
        if(cb.didHit()) pMaxDist = distance;
        // Summary log: number of hits and closest model
        if (cb.getHitCount() > 0 && cb.getClosestModel()) {
            PHYS_TRACE(PHYS_CYL, "[RaycastSummary] hits=" << cb.getHitCount() << " closest='" << cb.getClosestModel()->name << "' id=" << cb.getClosestModel()->ID << " dist=" << cb.getClosestDist());
        }
        return cb.didHit();
    }

    // Removed extended getIntersectionTime overload for WoW emulator compatibility

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