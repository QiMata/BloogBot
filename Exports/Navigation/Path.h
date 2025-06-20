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

#ifndef MANGOSSERVER_PATH_H
#define MANGOSSERVER_PATH_H

#include <deque>

struct PathNode
{
    PathNode() : x(0.0f), y(0.0f), z(0.0f) {}
    PathNode(float _x, float _y, float _z) : x(_x), y(_y), z(_z) {}
    float x, y, z;
};

template < typename PathElem, typename PathNode = PathElem >
class Path
{
public:
    size_t size() const { return i_nodes.size(); }
    bool empty() const { return i_nodes.empty(); }
    void resize(unsigned int sz) { i_nodes.resize(sz); }
    void crop(unsigned int start, unsigned int end)
    {
        while (start && !i_nodes.empty())
        {
            i_nodes.pop_front();
            --start;
        }

        while (end && !i_nodes.empty())
        {
            i_nodes.pop_back();
            --end;
        }
    }

    void clear() { i_nodes.clear(); }

    float GetTotalLength(uint32 start, uint32 end) const
    {
        float len = 0.0f;
        for (unsigned int idx = start + 1; idx < end; ++idx)
        {
            PathNode const& node = i_nodes[idx];
            PathNode const& prev = i_nodes[idx - 1];
            float xd = node.x - prev.x;
            float yd = node.y - prev.y;
            float zd = node.z - prev.z;
            len += sqrtf(xd * xd + yd * yd + zd * zd);
        }
        return len;
    }

    float GetTotalLength() const { return GetTotalLength(0, size()); }

    float GetPassedLength(uint32 curnode, float x, float y, float z) const
    {
        float len = GetTotalLength(0, curnode);

        if (curnode > 0)
        {
            PathNode const& node = i_nodes[curnode - 1];
            float xd = x - node.x;
            float yd = y - node.y;
            float zd = z - node.z;
            len += sqrtf(xd * xd + yd * yd + zd * zd);
        }

        return len;
    }

    PathNode& operator[](size_t idx) { return i_nodes[idx]; }
    PathNode const& operator[](size_t idx) const { return i_nodes[idx]; }

    void set(size_t idx, PathElem elem) { i_nodes[idx] = elem; }

protected:
    std::deque<PathElem> i_nodes;
};

typedef Path<PathNode> PointPath;

#endif