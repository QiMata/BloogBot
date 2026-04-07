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
    private const int SceneDataConnectTimeoutMs = 1500;
    private const int SceneDataReadTimeoutMs = 30000;
    private const int SceneDataWriteTimeoutMs = 10000;
    private static readonly TimeSpan SceneDataRetryDelay = TimeSpan.FromSeconds(2);
    private readonly ILogger _logger;
    private const float GridSize = 200f; // yards per grid tile
    private readonly Dictionary<uint, string> _loadedRegionKeys = new();
    internal static Func<uint, float, float, bool>? TestEnsureSceneDataAroundOverride { get; set; }
    internal static Func<SceneGridRequest, SceneGridResponse>? TestSendRequestOverride { get; set; }
    internal static Func<DateTime>? TestUtcNowOverride { get; set; }
    /// <summary>Test override: captures injected triangles instead of P/Invoking InjectSceneTriangles.</summary>
    internal static Func<uint, float, float, float, float, NativePhysics.InjectedTriangle[], bool>? TestInjectOverride { get; set; }
    private DateTime _nextRetryUtc = DateTime.MinValue;

    public SceneDataClient(string ipAddress, int port, ILogger logger)
        : base(ipAddress, port, logger, connectImmediately: false)
    {
        _logger = logger;
    }

    internal SceneDataClient(ILogger logger)
        : base()
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

        string gridKey = $"{mapId}_{gridMinX:F0}_{gridMinY:F0}_{gridMaxX:F0}_{gridMaxY:F0}";
        return EnsureSceneDataBounds(mapId, gridMinX, gridMinY, gridMaxX, gridMaxY, gridKey);
    }

    /// <summary>
    /// Request scene data for the area around a position in one bounded swap.
    /// The local Navigation scene cache is replaced with a 3x3 neighborhood so the
    /// bot keeps only nearby collision data resident.
    /// </summary>
    public bool EnsureSceneDataAround(uint mapId, float x, float y)
    {
        if (TestEnsureSceneDataAroundOverride != null)
            return TestEnsureSceneDataAroundOverride(mapId, x, y);

        float gridMinX = MathF.Floor(x / GridSize) * GridSize;
        float gridMinY = MathF.Floor(y / GridSize) * GridSize;
        float minX = gridMinX - GridSize;
        float minY = gridMinY - GridSize;
        float maxX = gridMinX + (2 * GridSize);
        float maxY = gridMinY + (2 * GridSize);
        string regionKey = $"{mapId}_{minX:F0}_{minY:F0}_{maxX:F0}_{maxY:F0}";

        return EnsureSceneDataBounds(mapId, minX, minY, maxX, maxY, regionKey);
    }

    private bool EnsureSceneDataBounds(uint mapId, float minX, float minY, float maxX, float maxY, string regionKey)
    {
        if (_loadedRegionKeys.TryGetValue(mapId, out var loadedRegionKey)
            && string.Equals(loadedRegionKey, regionKey, StringComparison.Ordinal))
        {
            return true;
        }

        var now = GetUtcNow();
        if (now < _nextRetryUtc)
        {
            _logger.LogDebug("[SceneData] Skipping region {Key} until retry window opens at {RetryUtc:O}",
                regionKey, _nextRetryUtc);
            return false;
        }

        try
        {
            var request = new SceneGridRequest
            {
                MapId = mapId,
                MinX = minX,
                MinY = minY,
                MaxX = maxX,
                MaxY = maxY,
            };

            _logger.LogInformation("[SceneData] Requesting region {Key} from SceneDataService...", regionKey);

            var response = TestSendRequestOverride != null
                ? TestSendRequestOverride(request)
                : SendMessage(request, SceneDataReadTimeoutMs, SceneDataWriteTimeoutMs, SceneDataConnectTimeoutMs);

            _logger.LogInformation("[SceneData] Response for {Key}: success={Success}, triangles={Count}, error={Error}",
                regionKey, response.Success, response.TriangleCount, response.ErrorMessage ?? "none");

            if (!response.Success || response.TriangleCount == 0)
            {
                if (!response.Success)
                    MarkRetryAfterFailure(now);

                _logger.LogWarning("[SceneData] No triangles for region {Key}: {Error}",
                    regionKey, response.ErrorMessage ?? "empty");
                return false;
            }

            // Inject triangles into local Navigation.dll SceneCache
            InjectTrianglesIntoLocalCache(mapId, minX, minY, maxX, maxY, response);
            _loadedRegionKeys[mapId] = regionKey;
            _nextRetryUtc = DateTime.MinValue;

            _logger.LogInformation("[SceneData] Injected {Count} triangles for region {Key}",
                response.TriangleCount, regionKey);
            return true;
        }
        catch (Exception ex)
        {
            MarkRetryAfterFailure(now);
            _logger.LogError(ex, "[SceneData] FAILED to request region {Key}", regionKey);
            return false;
        }
    }

    private void MarkRetryAfterFailure(DateTime now)
    {
        _nextRetryUtc = now + SceneDataRetryDelay;
    }

    private static DateTime GetUtcNow()
        => TestUtcNowOverride?.Invoke() ?? DateTime.UtcNow;

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

        if (TestInjectOverride != null)
        {
            TestInjectOverride(mapId, minX, minY, maxX, maxY, triangles);
            return;
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
