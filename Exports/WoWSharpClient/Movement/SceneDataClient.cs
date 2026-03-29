using System;
using System.Runtime.InteropServices;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using SceneData;

namespace WoWSharpClient.Movement;

/// <summary>
/// Client that requests scene collision data from SceneDataService and injects it
/// into the local Navigation.dll SceneCache. Bots call RequestSceneGrid() when
/// moving to a new area — the service extracts triangles from VMAP + ADT and
/// sends them back for local physics use.
/// </summary>
public sealed class SceneDataClient : ProtobufSocketClient<SceneGridRequest, SceneGridResponse>, IDisposable
{
    private readonly ILogger _logger;
    private readonly HashSet<string> _loadedGrids = new();
    private const float GridSize = 200f; // yards per grid tile

    public SceneDataClient(string ipAddress, int port, ILogger logger)
        : base(ipAddress, port, logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Request scene data for the grid tile containing the given position.
    /// If the grid is already loaded, returns immediately.
    /// </summary>
    public bool EnsureSceneDataAt(uint mapId, float x, float y)
    {
        // Quantize position to grid tile
        float gridMinX = MathF.Floor(x / GridSize) * GridSize;
        float gridMinY = MathF.Floor(y / GridSize) * GridSize;
        float gridMaxX = gridMinX + GridSize;
        float gridMaxY = gridMinY + GridSize;

        string gridKey = $"{mapId}_{gridMinX:F0}_{gridMinY:F0}";
        if (_loadedGrids.Contains(gridKey))
            return true;

        try
        {
            var request = new SceneGridRequest
            {
                MapId = mapId,
                MinX = gridMinX,
                MinY = gridMinY,
                MaxX = gridMaxX,
                MaxY = gridMaxY,
            };

            var response = SendMessage(request);

            if (!response.Success || response.TriangleCount == 0)
            {
                _logger.LogWarning("[SceneData] No triangles for grid {Key}: {Error}",
                    gridKey, response.ErrorMessage ?? "empty");
                return false;
            }

            // Inject triangles into local Navigation.dll SceneCache
            InjectTrianglesIntoLocalCache(mapId, gridMinX, gridMinY, gridMaxX, gridMaxY, response);
            _loadedGrids.Add(gridKey);

            _logger.LogInformation("[SceneData] Loaded grid {Key}: {Count} triangles",
                gridKey, response.TriangleCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SceneData] Failed to request grid {Key}", gridKey);
            return false;
        }
    }

    /// <summary>
    /// Request scene data for the area around a position (current grid + neighbors).
    /// </summary>
    public void EnsureSceneDataAround(uint mapId, float x, float y)
    {
        // Load current grid + 8 neighbors (3x3 grid)
        for (float dx = -GridSize; dx <= GridSize; dx += GridSize)
            for (float dy = -GridSize; dy <= GridSize; dy += GridSize)
                EnsureSceneDataAt(mapId, x + dx, y + dy);
    }

    private void InjectTrianglesIntoLocalCache(uint mapId, float minX, float minY, float maxX, float maxY,
        SceneGridResponse response)
    {
        int count = (int)response.TriangleCount;
        if (count == 0) return;

        var triangles = new NativePhysics.InjectedTriangle[count];
        for (int i = 0; i < count; i++)
        {
            int tBase = i * 9;
            int nBase = i * 3;
            triangles[i] = new NativePhysics.InjectedTriangle
            {
                V0X = response.TriangleData[tBase + 0],
                V0Y = response.TriangleData[tBase + 1],
                V0Z = response.TriangleData[tBase + 2],
                V1X = response.TriangleData[tBase + 3],
                V1Y = response.TriangleData[tBase + 4],
                V1Z = response.TriangleData[tBase + 5],
                V2X = response.TriangleData[tBase + 6],
                V2Y = response.TriangleData[tBase + 7],
                V2Z = response.TriangleData[tBase + 8],
                NX = response.NormalData[nBase + 0],
                NY = response.NormalData[nBase + 1],
                NZ = response.NormalData[nBase + 2],
            };
        }

        var handle = GCHandle.Alloc(triangles, GCHandleType.Pinned);
        try
        {
            NativePhysics.InjectSceneTriangles(mapId, minX, minY, maxX, maxY,
                handle.AddrOfPinnedObject(), count);
        }
        finally
        {
            handle.Free();
        }
    }
}
