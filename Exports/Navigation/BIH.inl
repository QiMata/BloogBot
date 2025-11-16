#pragma once
#include "VMapDefinitions.h"
#include "VMapLog.h"
#include <algorithm>
#include <iostream>
#include <string>
#include <iomanip>
#include <cmath>

// Ray intersection template implementation
template<typename RayCallback>
void BIH::intersectRay(const G3D::Ray& r, RayCallback& intersectCallback,
    float& maxDist, bool stopAtFirstHit, bool ignoreM2Model) const
{
    if (tree.empty() || objects.empty())
    {
        PHYS_TRACE(PHYS_CYL, "[BIH][Ray] early-exit empty tree (nodes="<<tree.size()<<" objs="<<objects.size()<<") maxDist="<<maxDist);
        return;
    }

    float intervalMin = -1.f;
    float intervalMax = -1.f;
    const G3D::Vector3& org = r.origin();
    const G3D::Vector3& dir = r.direction();
    const G3D::Vector3& invDir = r.invDirection();

    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] enter org=("<<org.x<<","<<org.y<<","<<org.z<<") dir=("<<dir.x<<","<<dir.y<<","<<dir.z<<") maxDist="<<maxDist
        <<" boundsLo=("<<bounds.low().x<<","<<bounds.low().y<<","<<bounds.low().z<<") boundsHi=("<<bounds.high().x<<","<<bounds.high().y<<","<<bounds.high().z<<") nodes="<<tree.size()<<")");

    // Calculate initial ray-box intersection with overall bounds
    for (int i = 0; i < 3; ++i)
    {
        if (G3D::fuzzyNe(dir[i], 0.0f))
        {
            float t1 = (bounds.low()[i] - org[i]) * invDir[i];
            float t2 = (bounds.high()[i] - org[i]) * invDir[i];
            if (t1 > t2)
                std::swap(t1, t2);
            if (t1 > intervalMin)
                intervalMin = t1;
            if (t2 < intervalMax || intervalMax < 0.f)
                intervalMax = t2;

            PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] axis="<<i<<" t1="<<t1<<" t2="<<t2<<" intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);

            if (intervalMax <= 0 || intervalMin >= maxDist)
            {
                PHYS_TRACE(PHYS_CYL, "[BIH][Ray] early-exit ray misses global bounds intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);
                return;
            }
        }
    }

    if (intervalMin > intervalMax)
    {
        PHYS_TRACE(PHYS_CYL, "[BIH][Ray] early-exit intervalMin>intervalMax intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);
        return;
    }

    intervalMin = std::max(intervalMin, 0.f);
    intervalMax = std::min(intervalMax, maxDist);

    // Compute custom offsets from direction sign bit
    uint32_t offsetFront[3], offsetBack[3];
    uint32_t offsetFront3[3], offsetBack3[3];

    for (int i = 0; i < 3; ++i)
    {
        offsetFront[i] = VMAP::floatToRawIntBits(dir[i]) >> 31;
        offsetBack[i] = offsetFront[i] ^ 1;
        offsetFront3[i] = offsetFront[i] * 3;
        offsetBack3[i] = offsetBack[i] * 3;
        ++offsetFront[i];
        ++offsetBack[i];
    }

    // Stack for tree traversal
    StackNode stack[MAX_STACK_SIZE];
    int stackPos = 0;
    int node = 0;
    int nodesVisited = 0;
    int leavesProcessed = 0;
    int objectsTested = 0;
    int hitsAccepted = 0;

    while (true)
    {
        while (true)
        {
            nodesVisited++;
            uint32_t tn = tree[node];
            uint32_t axis = (tn >> 30) & 3;
            bool BVH2 = tn & (1 << 29);
            int offset = tn & ~(7 << 29);

            if (!BVH2)
            {
                if (axis < 3)
                {
                    // "normal" interior node
                    float tf = (VMAP::intBitsToFloat(tree[node + offsetFront[axis]]) - org[axis]) * invDir[axis];
                    float tb = (VMAP::intBitsToFloat(tree[node + offsetBack[axis]]) - org[axis]) * invDir[axis];
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] node="<<node<<" axis="<<axis<<" tf="<<tf<<" tb="<<tb<<" intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);

                    // ray passes between clip zones
                    if (tf < intervalMin && tb > intervalMax)
                    {
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] between-skip node="<<node);
                        break;
                    }

                    int back = offset + offsetBack3[axis];
                    node = back;

                    // ray passes through far node only
                    if (tf < intervalMin)
                    {
                        intervalMin = (tb >= intervalMin) ? tb : intervalMin;
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] far-only advance node="<<node<<" new intervalMin="<<intervalMin<<" maxDist="<<maxDist);
                        continue;
                    }

                    node = offset + offsetFront3[axis]; // front

                    // ray passes through near node only
                    if (tb > intervalMax)
                    {
                        intervalMax = (tf <= intervalMax) ? tf : intervalMax;
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] near-only advance node="<<node<<" new intervalMax="<<intervalMax<<" maxDist="<<maxDist);
                        continue;
                    }

                    // push back node
                    if (stackPos < MAX_STACK_SIZE)
                    {
                        stack[stackPos].node = back;
                        stack[stackPos].tnear = (tb >= intervalMin) ? tb : intervalMin;
                        stack[stackPos].tfar = intervalMax;
                        ++stackPos;
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] push backNode="<<back<<" tnear="<<stack[stackPos-1].tnear<<" tfar="<<stack[stackPos-1].tfar<<" maxDist="<<maxDist);
                    }
                    else
                    {
                        PHYS_TRACE(PHYS_CYL, "[BIH][Ray] stack overflow abort nodesVisited="<<nodesVisited<<" maxDist="<<maxDist);
                        return;
                    }

                    // update ray interval for front node
                    intervalMax = (tf <= intervalMax) ? tf : intervalMax;
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] descend front node="<<node<<" intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);
                    continue;
                }
                else
                {
                    // leaf - test some objects
                    leavesProcessed++;
                    int n = tree[node + 1];
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] leaf node="<<node<<" objCount="<<n<<" maxDist="<<maxDist);

                    while (n > 0)
                    {
                        objectsTested++;
                        uint32_t srcIdx = objects[offset];
                        uint32_t objIdx = mapObjectIndex(srcIdx);
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray]   test obj srcIdx="<<srcIdx<<" mapped="<<objIdx);
                        if (objIdx != 0xFFFFFFFFu)
                        {
                            bool hit = intersectCallback(r, objIdx, maxDist, stopAtFirstHit, ignoreM2Model);
                            if (hit)
                            {
                                ++hitsAccepted;
                                PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray]   HIT obj="<<objIdx<<" maxDistNow="<<maxDist);
                            }
                            if (stopAtFirstHit && hit)
                            {
                                PHYS_TRACE(PHYS_CYL, "[BIH][Ray] early terminate first-hit obj="<<objIdx<<" maxDist="<<maxDist);
                                PHYS_TRACE(PHYS_CYL, "[BIH][Ray] exit nodesVisited="<<nodesVisited<<" leaves="<<leavesProcessed<<" objsTested="<<objectsTested<<" hits="<<hitsAccepted<<" maxDist="<<maxDist);
                                return;
                            }
                        }
                        --n;
                        ++offset;
                    }
                    break;
                }
            }
            else  // BVH2 node
            {
                if (axis > 2)
                {
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] BVH2 terminal invalid axis node="<<node<<" maxDist="<<maxDist);
                    return;
                }

                float tf = (VMAP::intBitsToFloat(tree[node + offsetFront[axis]]) - org[axis]) * invDir[axis];
                float tb = (VMAP::intBitsToFloat(tree[node + offsetBack[axis]]) - org[axis]) * invDir[axis];
                PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] BVH2 node="<<node<<" axis="<<axis<<" tf="<<tf<<" tb="<<tb<<" intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);

                node = offset;
                intervalMin = (tf >= intervalMin) ? tf : intervalMin;
                intervalMax = (tb <= intervalMax) ? tb : intervalMax;

                if (intervalMin > intervalMax)
                {
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] BVH2 prune intervalMin>intervalMax node="<<node<<" maxDist="<<maxDist);
                    break;
                }

                continue;
            }
        } // traversal loop

        do
        {
            if (stackPos == 0)
            {
                PHYS_TRACE(PHYS_CYL, "[BIH][Ray] exit nodesVisited="<<nodesVisited<<" leaves="<<leavesProcessed<<" objsTested="<<objectsTested<<" hits="<<hitsAccepted<<" maxDist="<<maxDist);
                return;
            }
            --stackPos;
            intervalMin = stack[stackPos].tnear;
            if (maxDist < intervalMin)
            {
                PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] pop skip far stackPos="<<stackPos<<" tnear="<<intervalMin<<" maxDist="<<maxDist);
                continue;
            }
            node = stack[stackPos].node;
            intervalMax = stack[stackPos].tfar;
            PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Ray] pop node="<<node<<" intervalMin="<<intervalMin<<" intervalMax="<<intervalMax<<" maxDist="<<maxDist);
            break;
        } while (true);
    }
}

// Point intersection template implementation
template<typename IsectCallback>
void BIH::intersectPoint(const G3D::Vector3& p, IsectCallback& intersectCallback) const
{
    if (!bounds.contains(p))
    {
        PHYS_TRACE(PHYS_CYL, "[BIH][Point] early-exit outside bounds p=("<<p.x<<","<<p.y<<","<<p.z<<")");
        return;
    }

    StackNode stack[MAX_STACK_SIZE];
    int stackPos = 0;
    int node = 0;
    int nodesVisited = 0;
    int leavesChecked = 0;
    int objectsTested = 0;

    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] enter p=("<<p.x<<","<<p.y<<","<<p.z<<") boundsLo=("<<bounds.low().x<<","<<bounds.low().y<<","<<bounds.low().z<<") boundsHi=("<<bounds.high().x<<","<<bounds.high().y<<","<<bounds.high().z<<")");

    while (true)
    {
        while (true)
        {
            nodesVisited++;
            uint32_t tn = tree[node];
            uint32_t axis = (tn >> 30) & 3;
            bool const BVH2 = tn & (1 << 29);
            int offset = tn & ~(7 << 29);

            if (!BVH2)
            {
                if (axis < 3)
                {
                    float tl = VMAP::intBitsToFloat(tree[node + 1]);
                    float tr = VMAP::intBitsToFloat(tree[node + 2]);
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] node="<<node<<" axis="<<axis<<" tl="<<tl<<" tr="<<tr<<" pAxis="<<p[axis]);

                    if (tl < p[axis] && tr > p[axis])
                    {
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] between skip node="<<node);
                        break;
                    }

                    int right = offset + 3;
                    node = right;
                    if (tl < p[axis])
                    {
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] go right only node="<<node);
                        continue;
                    }
                    node = offset; // left
                    if (tr > p[axis])
                    {
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] go left only node="<<node);
                        continue;
                    }
                    if (stackPos < MAX_STACK_SIZE)
                    {
                        stack[stackPos].node = right;
                        ++stackPos;
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] push right node="<<right);
                    }
                    else
                    {
                        PHYS_TRACE(PHYS_CYL, "[BIH][Point] stack overflow abort nodesVisited="<<nodesVisited);
                        return;
                    }
                    continue;
                }
                else
                {
                    leavesChecked++;
                    int n = tree[node + 1];
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] leaf node="<<node<<" objCount="<<n);
                    uint32_t off = offset;
                    while (n > 0)
                    {
                        objectsTested++;
                        uint32_t srcIdx = objects[off];
                        uint32_t objIdx = mapObjectIndex(srcIdx);
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point]   test obj srcIdx="<<srcIdx<<" mapped="<<objIdx);
                        if (objIdx != 0xFFFFFFFFu)
                        {
                            intersectCallback(p, objIdx);
                        }
                        --n; ++off;
                    }
                    break;
                }
            }
            else // BVH2
            {
                if (axis > 2)
                {
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] BVH2 terminal invalid axis node="<<node);
                    return;
                }
                float tl = VMAP::intBitsToFloat(tree[node + 1]);
                float tr = VMAP::intBitsToFloat(tree[node + 2]);
                PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] BVH2 node="<<node<<" axis="<<axis<<" tl="<<tl<<" tr="<<tr<<" pAxis="<<p[axis]);
                node = offset;
                if (tl > p[axis] || tr < p[axis])
                {
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] BVH2 prune node="<<node);
                    break;
                }
                continue;
            }
        }

        if (stackPos == 0)
        {
            PHYS_TRACE(PHYS_CYL, "[BIH][Point] exit nodesVisited="<<nodesVisited<<" leaves="<<leavesChecked<<" objsTested="<<objectsTested);
            return;
        }
        --stackPos;
        node = stack[stackPos].node;
        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][Point] pop node="<<node<<" stackPos="<<stackPos);
    }
}

// AABB query implementation
inline bool BIH::QueryAABB(const G3D::AABox& query, uint32_t* outIndices, uint32_t& outCount, uint32_t maxCount) const
{
    outCount = 0;

    // Validate inputs and early outs
    if (tree.empty() || objects.empty()) {
        PHYS_TRACE(PHYS_CYL, "[BIH][AABB] early-exit: empty tree or objects (treeNodes="<<tree.size()<<" objects="<<objects.size()<<")");
        return false; }
    if (!bounds.intersects(query)) {
        auto gapAxis = [&](int axis)->float { float qLo = (&query.low().x)[axis]; float qHi = (&query.high().x)[axis]; float tLo = (&bounds.low().x)[axis]; float tHi = (&bounds.high().x)[axis]; if (qLo > tHi) return qLo - tHi; if (tLo > qHi) return tLo - qHi; return 0.0f; };
        PHYS_TRACE(PHYS_CYL, "[BIH][AABB] early-exit: query !intersect tree (qLo=("<<query.low().x<<","<<query.low().y<<","<<query.low().z<<") qHi=("<<query.high().x<<","<<query.high().y<<","<<query.high().z<<") treeLo=("<<bounds.low().x<<","<<bounds.low().y<<","<<bounds.low().z<<") treeHi=("<<bounds.high().x<<","<<bounds.high().y<<","<<bounds.high().z<<") gap=("<<gapAxis(0)<<","<<gapAxis(1)<<","<<gapAxis(2)<<"))");
        return false; }
    if (maxCount == 0 || outIndices == nullptr) {
        PHYS_TRACE(PHYS_CYL, "[BIH][AABB] early-exit: invalid out buffer (maxCount="<<maxCount<<" outIndicesNull="<<(outIndices==nullptr)<<")");
        return false; }

    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] enter qLo=("<<query.low().x<<","<<query.low().y<<","<<query.low().z
        <<") qHi=("<<query.high().x<<","<<query.high().y<<","<<query.high().z<<") treeLo=("<<bounds.low().x<<","<<bounds.low().y<<","<<bounds.low().z
        <<") treeHi=("<<bounds.high().x<<","<<bounds.high().y<<","<<bounds.high().z<<") primCount="<<primCount()<<" objectsSize="<<objects.size()<<" maxCount="<<maxCount<<")");

    // Traversal stack
    StackNode stack[MAX_STACK_SIZE];
    int stackPos = 0;
    uint32_t node = 0;

    // Diagnostics counters
    int nodesVisited = 0;
    int leavesVisited = 0;
    int objectsEnumerated = 0;

    while (true)
    {
        while (true)
        {
            ++nodesVisited;
            uint32_t tn = tree[node];
            uint32_t axis = (tn >> 30) & 3;
            bool const BVH2 = tn & (1 << 29);
            uint32_t offset = tn & ~(7u << 29);

            if (!BVH2)
            {
                if (axis < 3)
                {
                    // interior node with clipping planes
                    float tl = VMAP::intBitsToFloat(tree[node + 1]);
                    float tr = VMAP::intBitsToFloat(tree[node + 2]);

                    bool goLeft = query.low()[axis] <= tr;   // overlaps left if min <= right clip
                    bool goRight = query.high()[axis] >= tl; // overlaps right if max >= left clip

                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] node="<<node<<" axis="<<axis<<" tl="<<tl<<" tr="<<tr
                            <<" qMin="<<query.low()[axis]<<" qMax="<<query.high()[axis]
                            <<" goL="<<(goLeft?1:0)<<" goR="<<(goRight?1:0));

                    if (goLeft && goRight)
                    {
                        uint32_t right = offset + 3;
                        if (stackPos >= MAX_STACK_SIZE)
                            return outCount > 0; // avoid overflow
                        stack[stackPos++].node = right;
                        node = offset; // left child
                        continue;
                    }
                    else if (goLeft)
                    {
                        node = offset;
                        continue;
                    }
                    else if (goRight)
                    {
                        node = offset + 3;
                        continue;
                    }
                    else
                    {
                        uint32_t right = offset + 3;
                        if (stackPos < MAX_STACK_SIZE)
                        {
                            stack[stackPos++].node = right;
                            node = offset;
                            PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB][FALLBACK] node="<<node<<" axis="<<axis<<" tl="<<tl<<" tr="<<tr
                                <<" qMin="<<query.low()[axis]<<" qMax="<<query.high()[axis]<<" -> descend BOTH");
                            continue;
                        }
                        else
                        {
                            PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB][FALLBACK] stack overflow abort outCount="<<outCount);
                            return outCount > 0;
                        }
                    }
                }
                else
                {
                    ++leavesVisited;
                    uint32_t n = tree[node + 1];
                    uint32_t off = offset;
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] leaf node="<<node<<" count="<<n);
                    while (n > 0)
                    {
                        uint32_t srcIdx = objects[off];
                        uint32_t objIdx = mapObjectIndex(srcIdx);
                        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB]   obj srcIdx="<<srcIdx<<" mapped="<<objIdx);
                        if (objIdx != 0xFFFFFFFFu)
                        {
                            if (outCount < maxCount)
                            {
                                outIndices[outCount++] = objIdx;
                            }
                            else
                            {
                                PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] cap reached outCount="<<outCount);
                                return true;
                            }
                        }
                        ++objectsEnumerated;
                        ++off;
                        --n;
                    }
                    break;
                }
            }
            else
            {
                if (axis > 2)
                {
                    PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] BVH2 terminal invalid axis node="<<node);
                    return outCount > 0;
                }

                float tl = VMAP::intBitsToFloat(tree[node + 1]);
                float tr = VMAP::intBitsToFloat(tree[node + 2]);

                PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] BVH2 node="<<node<<" axis="<<axis<<" tl="<<tl<<" tr="<<tr
                        <<" qMin="<<query.low()[axis]<<" qMax="<<query.high()[axis]);

                if (query.low()[axis] <= tr && query.high()[axis] >= tl)
                {
                    node = offset;
                    continue;
                }
                else
                {
                    break;
                }
            }
        }

        if (stackPos == 0)
            break;
        node = stack[--stackPos].node;
        PHYS_TRACE_DEEP(PHYS_CYL, "[BIH][AABB] pop node="<<node<<" stackPos="<<stackPos);
    }

    if (outCount == 0)
        PHYS_TRACE(PHYS_CYL, "[BIH][AABB] exit: NO candidates nodesVisited="<<nodesVisited<<" leavesVisited="<<leavesVisited<<" objectsEnum="<<objectsEnumerated);
    else
        PHYS_TRACE(PHYS_CYL, "[BIH][AABB] exit: candidates="<<outCount<<" nodesVisited="<<nodesVisited<<" leavesVisited="<<leavesVisited<<" objectsEnum="<<objectsEnumerated);

    return outCount > 0;
}