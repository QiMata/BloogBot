#include "Navigation.h"
#include "MoveMap.h"
#include "PathFinder.h"
#include <vector>
#include <iostream>
#include <fstream>
#include <filesystem>
#include <new>
#include <stdio.h>
#include <cstdlib>
#include <cstring>

#if defined(_WIN32)
#define NOMINMAX
#include <windows.h>
#endif

#include "DetourCommon.h"
using namespace std;

#if defined(_WIN32)
EXTERN_C IMAGE_DOS_HEADER __ImageBase;
#endif

Navigation* Navigation::s_singletonInstance = NULL;

Navigation* Navigation::GetInstance()
{
	if (s_singletonInstance == NULL)
		s_singletonInstance = new Navigation();
	return s_singletonInstance;
}

void Navigation::Initialize()
{
	dtAllocSetCustom(dtCustomAlloc, dtCustomFree);

	MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();

	InitializeMapsForContinent(manager, 0);
	InitializeMapsForContinent(manager, 1);
	InitializeMapsForContinent(manager, 389);
}

void Navigation::Release()
{
	MMAP::MMapFactory::createOrGetMMapManager()->~MMapManager();
}

void Navigation::FreePathArr(XYZ* pathArr)
{
	delete[] pathArr;
}

const dtNavMeshQuery* Navigation::GetQueryForMap(uint32_t mapId)
{
	MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();
	InitializeMapsForContinent(manager, mapId);

	const dtNavMeshQuery* query = manager->GetNavMeshQuery(mapId, 0);

	return query;
}

XYZ* Navigation::CalculatePath(unsigned int mapId, XYZ start, XYZ end, bool smoothPath, int* length)
{
	if (length)
		*length = 0;

	m_lastOverlayRepairedSegment = {};

	MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();

	InitializeMapsForContinent(manager, mapId);

	PathFinder pathFinder(mapId, 1);
	// Public/native callers pass "smoothPath" semantics:
	// true  => Detour smooth path
	// false => straight corner path
	pathFinder.setUseStrightPath(!smoothPath);
	pathFinder.calculate(start.X, start.Y, start.Z, end.X, end.Y, end.Z);
	m_lastOverlayRepairedSegment.segmentIndex = pathFinder.getOverlayBlockedSegmentIndex();
	m_lastOverlayRepairedSegment.blockingInstanceId = pathFinder.getOverlayBlockingInstanceId();
	m_lastOverlayRepairedSegment.blockingGuid = pathFinder.getOverlayBlockingGuid();
	m_lastOverlayRepairedSegment.blockingDisplayId = pathFinder.getOverlayBlockingDisplayId();

	PointsArray pointPath = pathFinder.getPath();
	if (pointPath.empty())
	{
		return nullptr;
	}

	if (length)
		*length = static_cast<int>(pointPath.size());

	XYZ* pathArr = new (std::nothrow) XYZ[pointPath.size()];
	if (pathArr == nullptr)
	{
		if (length)
			*length = 0;
		return nullptr;
	}

	for (unsigned int i = 0; i < pointPath.size(); i++)
	{
		pathArr[i].X = pointPath[i].x;
		pathArr[i].Y = pointPath[i].y;
		pathArr[i].Z = pointPath[i].z;
	}

	return pathArr;
}

bool Navigation::RaycastToWmoMesh(unsigned int mapId, float startX, float startY, float startZ,
	float endX, float endY, float endZ,
	float* hitX, float* hitY, float* hitZ)
{
	MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();
	InitializeMapsForContinent(manager, mapId);

	const dtNavMesh* mesh = manager->GetNavMesh(mapId);
	const dtNavMeshQuery* query = manager->GetNavMeshQuery(mapId, 0);

	if (!mesh || !query)
	{
		return false;
	}

	// Detour navmeshes use (X, Y, Z) = (X, Z, Y) in WoW coordinates.
	float start[3] = { startX, startZ, startY };
	float end[3] = { endX,   endZ,   endY };
	float extents[3];
	dtQueryFilter filter;
	filter.setIncludeFlags(0xFFFF);

	const float expansions[][3] = {
		{0.5f, 10.0f, 0.5f},
		{1.0f, 20.0f, 1.0f},
		{1.5f, 40.0f, 1.5f},
		{2.0f, 80.0f, 2.0f},
		{2.5f, 160.0f, 2.5f}
	};

	dtPolyRef startRef = 0;
	float nearest[3] = { 0.0f, 0.0f, 0.0f };
	dtStatus nearestStatus = 0;
	bool foundPoly = false;
	for (size_t i = 0; i < sizeof(expansions) / sizeof(expansions[0]); ++i)
	{
		memcpy(extents, expansions[i], sizeof(extents));
		nearestStatus = query->findNearestPoly(start, extents, &filter, &startRef, nearest);
		if (!dtStatusFailed(nearestStatus) && startRef != 0) {
			foundPoly = true;
			break;
		}
	}
	if (!foundPoly) {
		return false;
	}

	// If possible, print details about the polygon itself
	const dtMeshTile* tile = nullptr;
	const dtPoly* poly = nullptr;
	mesh->getTileAndPolyByRef(startRef, &tile, &poly);
	if (tile && poly) {
		for (unsigned int i = 0; i < poly->vertCount; ++i) {
			const unsigned short vi = poly->verts[i];
			const float* v = &tile->verts[vi * 3];
		}
	}

	dtRaycastHit hit;
	memset(&hit, 0, sizeof(hit));
	dtStatus rayStatus = query->raycast(startRef, start, end, &filter, DT_RAYCAST_USE_COSTS, &hit);

	if (!dtStatusFailed(rayStatus) && hit.t >= 0.0f && hit.t < 1.0f && !isnan(hit.t))
	{
		float intersection[3];
		dtVlerp(intersection, start, end, hit.t);
		*hitX = intersection[0];
		*hitY = intersection[2];
		*hitZ = intersection[1];

		return true;
	}

	// If the raycast didn't hit, but we have a valid poly, use nearest[2] as Z
	*hitX = nearest[0];
	*hitY = nearest[2];
	*hitZ = nearest[1];

	return true;
}


void Navigation::InitializeMapsForContinent(MMAP::MMapManager* manager, unsigned int mapId)
{
	if (!manager->zoneMap.contains(mapId))
	{
		const auto mmapsPath = Navigation::GetMmapsPath();
		if (!std::filesystem::exists(mmapsPath))
		{
			printf("[Navigation] mmaps path does not exist: %s\n", mmapsPath.c_str());
			return;
		}

		// Set the base path on MMapManager so loadMapData/loadMap use the correct directory
		if (manager->getMmapsBasePath().empty())
			manager->setMmapsBasePath(mmapsPath);

		printf("[Navigation] Loading map %u tiles from: %s\n", mapId, mmapsPath.c_str());
		int tileCount = 0;

		for (auto& p : std::filesystem::directory_iterator(mmapsPath))
		{
			if (!p.is_regular_file())
				continue;

			const auto& filePath = p.path();
			if (filePath.extension() != ".mmtile")
				continue;

			string filename = filePath.filename().string();
			if (filename.size() < 7)
				continue;

			std::string mapIdString;
			if (mapId < 10)
				mapIdString = "00" + std::to_string(mapId);
			else if (mapId < 100)
				mapIdString = "0" + std::to_string(mapId);
			else
				mapIdString = std::to_string(mapId);

			if (filename.rfind(mapIdString, 0) == 0)
			{
				int xTens = filename[3] - '0';
				int xOnes = filename[4] - '0';
				int yTens = filename[5] - '0';
				int yOnes = filename[6] - '0';
				if (xTens < 0 || xTens > 9 || xOnes < 0 || xOnes > 9 ||
					yTens < 0 || yTens > 9 || yOnes < 0 || yOnes > 9)
				{
					continue;
				}

				int x = (xTens * 10) + xOnes;
				int y = (yTens * 10) + yOnes;
				if (manager->loadMap(mapId, x, y))
					tileCount++;
			}
		}

		printf("[Navigation] Map %u: loaded %d tiles\n", mapId, tileCount);
		manager->zoneMap.insert(std::pair<unsigned int, bool>(mapId, true));
	}
}
bool Navigation::IsLineOfSight(unsigned int mapId,
	const XYZ& s, const XYZ& e)
{
	MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();
	InitializeMapsForContinent(manager, mapId);

	const auto* query = manager->GetNavMeshQuery(mapId, 0);
	if (!query) return false;

	const float ext[3] = { 2.f, 4.f, 2.f };
	dtQueryFilter  f;    f.setIncludeFlags(0xFFFF);

	float spos[3] = { s.Y, s.Z, s.X };
	float epos[3] = { e.Y, e.Z, e.X };

	dtPolyRef sRef = 0;
	query->findNearestPoly(spos, ext, &f, &sRef, nullptr);
	if (!sRef) return false;                       // off‑mesh → blocked

	dtRaycastHit hit{};
	if (dtStatusFailed(query->raycast(sRef, spos, epos, &f, 0, &hit)))
		return true;                               // raycast failed, assume clear

	return hit.t >= 1.0f;                          // t<1 => obstruction
}

std::vector<NavPoly> Navigation::CapsuleOverlap(uint32_t mapId,
	const XYZ& p,
	float r, float h)
{
	std::vector<NavPoly> out;

	MMAP::MMapManager* manager = MMAP::MMapFactory::createOrGetMMapManager();
	InitializeMapsForContinent(manager, mapId);

	const auto* query = manager->GetNavMeshQuery(mapId, 0);

	if (!query) return out;

	float centre[3] = { p.Y, p.Z + h * 0.5f, p.X };
	float ext[3] = { r,   h * 0.5f,       r };

	dtQueryFilter f;
	f.setIncludeFlags(0xFFFF);

	dtPolyRef refs[128];
	int n = 0;
	dtStatus status = query->queryPolygons(centre, ext, &f, refs, &n, 128);

	if (dtStatusFailed(status)) return out;

	const dtNavMesh* mesh = query->getAttachedNavMesh();

	for (int i = 0; i < n; ++i)
	{
		const dtPoly* poly = nullptr;
		const dtMeshTile* tile = nullptr;

		if (dtStatusFailed(mesh->getTileAndPolyByRef(refs[i], &tile, &poly))) continue;

		NavPoly np{};
		np.refId = refs[i];
		np.area = poly->getArea();
		np.flags = poly->flags;
		np.vertCount = poly->vertCount;

		for (int v = 0; v < poly->vertCount; ++v)
		{
			const float* vt = &tile->verts[poly->verts[v] * 3];
			np.verts[v] = { vt[2], vt[0], vt[1] };  // Detour → WoW
		}

		out.push_back(np);
	}

	return out;
}

std::vector<NavPoly> Navigation::CapsuleOverlapSweep(uint32_t mapId,
	const XYZ& p0,
	const XYZ& p1,
	float r, float h,
	float step /* =0.3f */)
{
	std::vector<NavPoly> out;
	XYZ   cur = p0;
	XYZ   delta = { p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z };
	const float len = std::sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);

	if (len < 1e-4f)                               // standing still → single query
		return CapsuleOverlap(mapId, p0, r, h);

	const int steps = std::max<int>(1, int(len / step));
	const float inv = 1.0f / steps;

	for (int i = 0; i <= steps; ++i)
	{
		cur = { p0.X + delta.X * i * inv,
				p0.Y + delta.Y * i * inv,
				p0.Z + delta.Z * i * inv };

		auto part = CapsuleOverlap(mapId, cur, r, h);
		out.insert(out.end(), part.begin(), part.end());
	}
	return out;           // duplicates are fine – caller only needs “any clash”
}

string Navigation::GetMmapsPath()
{
	const auto sep = std::filesystem::path::preferred_separator;
	const auto normalizeWithSeparator = [sep](std::string value)
	{
		if (!value.empty() && value.back() != '/' && value.back() != '\\')
			value.push_back(sep);
		return value;
	};

	// Check WWOW_DATA_DIR first.
	if (const char* envDataRoot = std::getenv("WWOW_DATA_DIR"))
	{
		std::string dataRoot = normalizeWithSeparator(envDataRoot);
		std::string mmapsPath = dataRoot + "mmaps";
		mmapsPath = normalizeWithSeparator(mmapsPath);
		if (std::filesystem::exists(mmapsPath))
			return mmapsPath;
	}

	// Prefer current working directory if mmaps is present there.
	{
		std::string cwdMmaps = (std::filesystem::current_path() / "mmaps").string();
		cwdMmaps = normalizeWithSeparator(cwdMmaps);
		if (std::filesystem::exists(cwdMmaps))
			return cwdMmaps;
	}

#if defined(_WIN32)
	// Fall back to DLL-relative path on Windows.
	WCHAR dllPath[MAX_PATH] = { 0 };
	GetModuleFileNameW((HINSTANCE)&__ImageBase, dllPath, _countof(dllPath));
	std::wstring ws(dllPath);
	std::string pathAndFile(ws.begin(), ws.end());
	char* c = const_cast<char*>(pathAndFile.c_str());
	int strLength = strlen(c);
	int lastOccur = 0;
	for (int i = 0; i < strLength; i++)
	{
		if (c[i] == '\\') lastOccur = i;
	}

	string pathToMmap = pathAndFile.substr(0, lastOccur + 1);
	pathToMmap.append("mmaps\\");
	return pathToMmap;
#else
	return std::string("mmaps") + sep;
#endif
}
