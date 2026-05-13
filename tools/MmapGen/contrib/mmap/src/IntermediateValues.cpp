/*
 * This file is part of the CMaNGOS Project. See AUTHORS file for Copyright information
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
 */

#include "IntermediateValues.h"
#include <cstring>

namespace MMAP
{
    IntermediateValues::~IntermediateValues()
    {
        rcFreeCompactHeightfield(compactHeightfield);
        rcFreeHeightField(heightfield);
        rcFreeContourSet(contours);
        rcFreePolyMesh(polyMesh);
        rcFreePolyMeshDetail(polyMeshDetail);
    }

    void IntermediateValues::writeIV(uint32 mapID, uint32 tileX, uint32 tileY)
    {
        char fileName[255];
        char tileString[25];
        sprintf(tileString, "[%02u,%02u]: ", tileX, tileY);

        printf("%sWriting debug output...                       \r", tileString);

        string name("meshes/%03u%02i%02i.");

#define DEBUG_WRITE(fileExtension,data) \
        do { \
            sprintf(fileName, (name + fileExtension).c_str(), mapID, tileY, tileX); \
            FILE* file = fopen(fileName, "wb"); \
            if (!file) \
            { \
                char message[1024]; \
                sprintf(message, "%sFailed to open %s for writing!\n",  tileString, fileName); \
                perror(message); \
            } \
            else \
                debugWrite(file, data); \
            if(file) fclose(file); \
            printf("%sWriting debug output...                       \r", tileString); \
        } while (false)

        if (heightfield)
            DEBUG_WRITE("hf", heightfield);
        if (compactHeightfield)
            DEBUG_WRITE("chf", compactHeightfield);
        if (contours)
            DEBUG_WRITE("cs", contours);
        if (polyMesh)
            DEBUG_WRITE("pmesh", polyMesh);
        if (polyMeshDetail)
            DEBUG_WRITE("dmesh", polyMeshDetail);

#undef DEBUG_WRITE
    }

    void IntermediateValues::debugWrite(FILE* file, const rcHeightfield* mesh)
    {
        if (!file || !mesh)
            return;

        fwrite(&(mesh->cs), sizeof(float), 1, file);
        fwrite(&(mesh->ch), sizeof(float), 1, file);
        fwrite(&(mesh->width), sizeof(int), 1, file);
        fwrite(&(mesh->height), sizeof(int), 1, file);
        fwrite(mesh->bmin, sizeof(float), 3, file);
        fwrite(mesh->bmax, sizeof(float), 3, file);

        for (int y = 0; y < mesh->height; ++y)
            for (int x = 0; x < mesh->width; ++x)
            {
                rcSpan* span = mesh->spans[x + y * mesh->width];

                // first, count the number of spans
                int spanCount = 0;
                while (span)
                {
                    spanCount++;
                    span = span->next;
                }

                // write the span count
                fwrite(&spanCount, sizeof(int), 1, file);

                // write the spans
                span = mesh->spans[x + y * mesh->width];
                while (span)
                {
                    fwrite(span, sizeof(rcSpan), 1, file);
                    span = span->next;
                }
            }
    }

    void IntermediateValues::debugWrite(FILE* file, const rcCompactHeightfield* chf)
    {
        if (!file || !chf)
            return;

        fwrite(&(chf->width), sizeof(chf->width), 1, file);
        fwrite(&(chf->height), sizeof(chf->height), 1, file);
        fwrite(&(chf->spanCount), sizeof(chf->spanCount), 1, file);

        fwrite(&(chf->walkableHeight), sizeof(chf->walkableHeight), 1, file);
        fwrite(&(chf->walkableClimb), sizeof(chf->walkableClimb), 1, file);

        fwrite(&(chf->maxDistance), sizeof(chf->maxDistance), 1, file);
        fwrite(&(chf->maxRegions), sizeof(chf->maxRegions), 1, file);

        fwrite(chf->bmin, sizeof(chf->bmin), 1, file);
        fwrite(chf->bmax, sizeof(chf->bmax), 1, file);

        fwrite(&(chf->cs), sizeof(chf->cs), 1, file);
        fwrite(&(chf->ch), sizeof(chf->ch), 1, file);

        int tmp = 0;
        if (chf->cells) tmp |= 1;
        if (chf->spans) tmp |= 2;
        if (chf->dist) tmp |= 4;
        if (chf->areas) tmp |= 8;

        fwrite(&tmp, sizeof(tmp), 1, file);

        if (chf->cells)
            fwrite(chf->cells, sizeof(rcCompactCell), chf->width * chf->height, file);
        if (chf->spans)
            fwrite(chf->spans, sizeof(rcCompactSpan), chf->spanCount, file);
        if (chf->dist)
            fwrite(chf->dist, sizeof(unsigned short), chf->spanCount, file);
        if (chf->areas)
            fwrite(chf->areas, sizeof(unsigned char), chf->spanCount, file);
    }

    void IntermediateValues::debugWrite(FILE* file, const rcContourSet* cs)
    {
        if (!file || !cs)
            return;

        fwrite(&(cs->cs), sizeof(float), 1, file);
        fwrite(&(cs->ch), sizeof(float), 1, file);
        fwrite(cs->bmin, sizeof(float), 3, file);
        fwrite(cs->bmax, sizeof(float), 3, file);
        fwrite(&(cs->nconts), sizeof(int), 1, file);
        for (int i = 0; i < cs->nconts; ++i)
        {
            fwrite(&cs->conts[i].area, sizeof(unsigned char), 1, file);
            fwrite(&cs->conts[i].reg, sizeof(unsigned short), 1, file);
            fwrite(&cs->conts[i].nverts, sizeof(int), 1, file);
            fwrite(cs->conts[i].verts, sizeof(int), cs->conts[i].nverts * 4, file);
            fwrite(&cs->conts[i].nrverts, sizeof(int), 1, file);
            fwrite(cs->conts[i].rverts, sizeof(int), cs->conts[i].nrverts * 4, file);
        }
    }

    void IntermediateValues::debugWrite(FILE* file, const rcPolyMesh* mesh)
    {
        if (!file || !mesh)
            return;

        fwrite(&(mesh->cs), sizeof(float), 1, file);
        fwrite(&(mesh->ch), sizeof(float), 1, file);
        fwrite(&(mesh->nvp), sizeof(int), 1, file);
        fwrite(mesh->bmin, sizeof(float), 3, file);
        fwrite(mesh->bmax, sizeof(float), 3, file);
        fwrite(&(mesh->nverts), sizeof(int), 1, file);
        fwrite(mesh->verts, sizeof(unsigned short), mesh->nverts * 3, file);
        fwrite(&(mesh->npolys), sizeof(int), 1, file);
        fwrite(mesh->polys, sizeof(unsigned short), mesh->npolys * mesh->nvp * 2, file);
        fwrite(mesh->flags, sizeof(unsigned short), mesh->npolys, file);
        fwrite(mesh->areas, sizeof(unsigned char), mesh->npolys, file);
        fwrite(mesh->regs, sizeof(unsigned short), mesh->npolys, file);
    }

    void IntermediateValues::debugWrite(FILE* file, const rcPolyMeshDetail* mesh)
    {
        if (!file || !mesh)
            return;

        fwrite(&(mesh->nverts), sizeof(int), 1, file);
        fwrite(mesh->verts, sizeof(float), mesh->nverts * 3, file);
        fwrite(&(mesh->ntris), sizeof(int), 1, file);
        fwrite(mesh->tris, sizeof(char), mesh->ntris * 4, file);
        fwrite(&(mesh->nmeshes), sizeof(int), 1, file);
        fwrite(mesh->meshes, sizeof(int), mesh->nmeshes * 4, file);
    }

    void IntermediateValues::generateObjFile(uint32 mapID, uint32 tileX, uint32 tileY, MeshData& meshData)
    {
        char objFileName[255];
        sprintf(objFileName, "map%03u%02u%02u", mapID, tileY, tileX);
        generateObjFile(objFileName, meshData);
    }
    void IntermediateValues::generateObjFile(std::string filename, MeshData& meshData)
    {
        std::string realFileName = "meshes/" + filename + ".obj";
        FILE* objFile = fopen(realFileName.c_str(), "wb");
        if (!objFile)
        {
            char message[1024];
            sprintf(message, "Failed to open %s for writing!\n", realFileName.c_str());
            perror(message);
            return;
        }

        std::string mtlFileName = "meshes/" + filename + ".mtl";
        FILE* mtlFile = fopen(mtlFileName.c_str(), "wb");
        if (mtlFile)
        {
            fprintf(mtlFile, "newmtl terrain\nKd 0.42 0.72 0.34\nKa 0.10 0.10 0.10\n\n");
            fprintf(mtlFile, "newmtl vmap\nKd 0.55 0.55 0.85\nKa 0.10 0.10 0.10\n\n");
            fprintf(mtlFile, "newmtl gameobject\nKd 0.95 0.55 0.22\nKa 0.10 0.10 0.10\n\n");
            fprintf(mtlFile, "newmtl liquid\nKd 0.20 0.55 0.90\nKa 0.10 0.10 0.10\n\n");
            fclose(mtlFile);
        }

        std::string csvFileName = "meshes/" + filename + ".source_triangles.csv";
        FILE* csvFile = fopen(csvFileName.c_str(), "wb");
        if (csvFile)
            fprintf(csvFile, "faceIndex,source,meshTriIndex,minX,minY,minZ,maxX,maxY,maxZ\n");

        fprintf(objFile, "# MmapGen source geometry with triangle-source tags.\n");
        fprintf(objFile, "# Coordinates are generator/Recast coordinates, not viewer-remapped WoW XYZ.\n");
        fprintf(objFile, "mtllib %s.mtl\n", filename.c_str());
        fprintf(objFile, "o source_geometry\n");

        float* liquidVerts = meshData.liquidVerts.getCArray();
        int* liquidTris = meshData.liquidTris.getCArray();
        float* solidVerts = meshData.solidVerts.getCArray();
        int* solidTris = meshData.solidTris.getCArray();

        const int liquidVertCount = meshData.liquidVerts.size() / 3;
        const int liquidTriCount = meshData.liquidTris.size() / 3;
        const int solidVertCount = meshData.solidVerts.size() / 3;
        const int solidTriCount = meshData.solidTris.size() / 3;

        for (int i = 0; i < liquidVertCount; i++)
            fprintf(objFile, "v %f %f %f\n", liquidVerts[i * 3], liquidVerts[i * 3 + 1], liquidVerts[i * 3 + 2]);
        for (int i = 0; i < solidVertCount; i++)
            fprintf(objFile, "v %f %f %f\n", solidVerts[i * 3], solidVerts[i * 3 + 1], solidVerts[i * 3 + 2]);

        auto writeCsvRow = [csvFile](int faceIndex, const char* source, int meshTriIndex, const float* verts, const int* tris)
        {
            if (!csvFile)
                return;

            const int i0 = tris[meshTriIndex * 3];
            const int i1 = tris[meshTriIndex * 3 + 1];
            const int i2 = tris[meshTriIndex * 3 + 2];
            float minX = verts[i0 * 3];
            float minY = verts[i0 * 3 + 1];
            float minZ = verts[i0 * 3 + 2];
            float maxX = minX;
            float maxY = minY;
            float maxZ = minZ;
            const int indices[3] = { i0, i1, i2 };
            for (int c = 1; c < 3; ++c)
            {
                const float x = verts[indices[c] * 3];
                const float y = verts[indices[c] * 3 + 1];
                const float z = verts[indices[c] * 3 + 2];
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (z < minZ) minZ = z;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;
            }

            fprintf(csvFile, "%d,%s,%d,%f,%f,%f,%f,%f,%f\n",
                    faceIndex, source, meshTriIndex, minX, minY, minZ, maxX, maxY, maxZ);
        };

        if (liquidTriCount > 0)
        {
            fprintf(objFile, "g liquid\nusemtl liquid\n");
            for (int i = 0; i < liquidTriCount; i++)
            {
                fprintf(objFile, "f %i %i %i\n",
                        liquidTris[i * 3] + 1,
                        liquidTris[i * 3 + 1] + 1,
                        liquidTris[i * 3 + 2] + 1);
                writeCsvRow(i, "liquid", i, liquidVerts, liquidTris);
            }
        }

        const char* activeSource = "";
        for (int i = 0; i < solidTriCount; i++)
        {
            const char* source = meshData.SourceNameForTriangle(i);
            if (strcmp(activeSource, source) != 0)
            {
                activeSource = source;
                fprintf(objFile, "g %s\nusemtl %s\n", source, source);
            }

            fprintf(objFile, "f %i %i %i\n",
                    solidTris[i * 3] + liquidVertCount + 1,
                    solidTris[i * 3 + 1] + liquidVertCount + 1,
                    solidTris[i * 3 + 2] + liquidVertCount + 1);
            writeCsvRow(liquidTriCount + i, source, i, solidVerts, solidTris);
        }

        if (csvFile)
            fclose(csvFile);

        fclose(objFile);

        #if 0
        printf("%sWriting debug output...                       \r", filename.c_str());

        realFileName = "meshes/" + filename + ".map";

        objFile = fopen(realFileName.c_str(), "wb");
        if (!objFile)
        {
            char message[1024];
            sprintf(message, "Failed to open %s for writing!\n", realFileName.c_str());
            perror(message);
            return;
        }

        char b = '\0';
        fwrite(&b, sizeof(char), 1, objFile);
        fclose(objFile);

        realFileName = "meshes/" + filename + ".mesh";
        objFile = fopen(realFileName.c_str(), "wb");
        if (!objFile)
        {
            char message[1024];
            sprintf(message, "Failed to open %s for writing!\n", realFileName.c_str());
            perror(message);
            return;
        }

        int vertCount = allVerts.size() / 3;

        fwrite(&vertCount, sizeof(int), 1, objFile);
        fwrite(verts, sizeof(float), vertCount * 3, objFile);
        fflush(objFile);

        int triCount = allTris.size() / 3;

        fwrite(&triCount, sizeof(int), 1, objFile);
        fwrite(tris, sizeof(int), triCount * 3, objFile);
        fflush(objFile);

        fclose(objFile);
        #endif
    }
}
