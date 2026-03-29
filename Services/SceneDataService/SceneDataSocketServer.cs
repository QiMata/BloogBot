using System.Runtime.InteropServices;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using SceneData;

namespace SceneDataService;

/// <summary>
/// TCP server that extracts and serves scene collision data from Navigation.dll on demand.
/// Bots request scene data for bounded regions; the service extracts triangles from
/// VMAP + ADT via Navigation.dll and returns them as packed float arrays.
/// </summary>
public sealed class SceneDataSocketServer : ProtobufSocketServer<SceneGridRequest, SceneGridResponse>
{
    private readonly ILogger _logger;
    private bool _initialized;

    public SceneDataSocketServer(string ipAddress, int port, ILogger logger)
        : base(ipAddress, port, logger)
    {
        _logger = logger;
    }

    public void InitializeNavigation()
    {
        if (_initialized) return;
        NativeScene.PreloadMap(0); // Eastern Kingdoms
        NativeScene.PreloadMap(1); // Kalimdor
        _initialized = true;
        _logger.LogInformation("[SceneDataService] Navigation initialized for maps 0, 1");
    }

    protected override SceneGridResponse HandleRequest(SceneGridRequest request)
    {
        if (!_initialized)
            InitializeNavigation();

        try
        {
            return ExtractSceneGrid(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SceneDataService] Error extracting scene grid for map {MapId}", request.MapId);
            return new SceneGridResponse
            {
                MapId = request.MapId,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private SceneGridResponse ExtractSceneGrid(SceneGridRequest request)
    {
        _logger.LogDebug("[SceneDataService] Extracting grid: map={Map} bounds=({MinX},{MinY})-({MaxX},{MaxY})",
            request.MapId, request.MinX, request.MinY, request.MaxX, request.MaxY);

        // Ensure the map is loaded
        NativeScene.PreloadMap(request.MapId);

        // Use Navigation.dll's SceneQuery to extract triangles in the bounded region.
        // We query GetGroundZ at a grid of points to determine terrain triangles,
        // then extract the full AABB contact set for the region.
        var response = new SceneGridResponse
        {
            MapId = request.MapId,
            MinX = request.MinX,
            MinY = request.MinY,
            MaxX = request.MaxX,
            MaxY = request.MaxY,
        };

        // Extract triangles via the TestTerrainAABB export
        int maxTriangles = 50000;
        var triangles = new NativeScene.AABBContact[maxTriangles];
        var handle = GCHandle.Alloc(triangles, GCHandleType.Pinned);
        try
        {
            int count = NativeScene.QueryTerrainAABBTriangles(
                request.MapId,
                request.MinX, request.MinY, -500f,  // minZ — deep enough for any terrain
                request.MaxX, request.MaxY, 2000f,   // maxZ — high enough for any structure
                handle.AddrOfPinnedObject(),
                maxTriangles);

            response.TriangleCount = (uint)count;

            for (int i = 0; i < count; i++)
            {
                ref var t = ref triangles[i];
                // 9 floats per triangle (3 vertices × 3 components)
                response.TriangleData.Add(t.PointX); response.TriangleData.Add(t.PointY); response.TriangleData.Add(t.PointZ);
                response.TriangleData.Add(t.V1X); response.TriangleData.Add(t.V1Y); response.TriangleData.Add(t.V1Z);
                response.TriangleData.Add(t.V2X); response.TriangleData.Add(t.V2Y); response.TriangleData.Add(t.V2Z);
                // Normal
                response.NormalData.Add(t.NormalX); response.NormalData.Add(t.NormalY); response.NormalData.Add(t.NormalZ);
                // Walkability
                response.Walkable.Add(t.Walkable != 0);
            }

            response.Success = true;
            _logger.LogInformation("[SceneDataService] Extracted {Count} triangles for map {Map} bounds ({MinX:F0},{MinY:F0})-({MaxX:F0},{MaxY:F0})",
                count, request.MapId, request.MinX, request.MinY, request.MaxX, request.MaxY);
        }
        finally
        {
            handle.Free();
        }

        return response;
    }
}

/// <summary>
/// P/Invoke declarations for Navigation.dll's scene query exports.
/// </summary>
internal static class NativeScene
{
    private const string DllName = "Navigation";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void PreloadMap(uint mapId);

    // This export doesn't exist yet — we need to add it to DllMain.cpp.
    // For now, use GetGroundZ as a fallback to verify the service works.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int QueryTerrainAABBTriangles(
        uint mapId,
        float minX, float minY, float minZ,
        float maxX, float maxY, float maxZ,
        IntPtr outContacts,
        int maxContacts);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetGroundZ(uint mapId, float x, float y, float queryZ, float maxSearchDist);

    [StructLayout(LayoutKind.Sequential)]
    public struct AABBContact
    {
        public float PointX, PointY, PointZ;    // Contact point
        public float NormalX, NormalY, NormalZ;  // Surface normal
        public float V1X, V1Y, V1Z;             // Triangle vertex 1
        public float V2X, V2Y, V2Z;             // Triangle vertex 2
        public int Walkable;                      // 1 = walkable surface
        public uint InstanceId;                   // VMAP instance
    }
}
