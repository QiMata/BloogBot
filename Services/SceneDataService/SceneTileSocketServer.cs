using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BotCommLayer;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using SceneData;

namespace SceneDataService;

/// <summary>
/// TCP server that serves pre-split .scenetile files by (mapId, tileX, tileY).
/// Each tile is 533.33y × 533.33y matching WoW's ADT grid.
///
/// On startup: loads all .scenetile files into memory.
/// On request: returns the pre-loaded tile data immediately (no extraction needed).
///
/// This replaces the AABB-based SceneDataSocketServer which had triangle cap and
/// cache replacement issues at tile boundaries.
/// </summary>
public sealed class SceneTileSocketServer : ProtobufSocketServer<SceneTileRequest, SceneTileResponse>
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, SceneTileResponse> _tileCache = new();
    private readonly ConcurrentDictionary<string, string> _tilePaths = new();
    private readonly ConcurrentDictionary<uint, string> _scenePaths = new();
    private readonly ConcurrentDictionary<string, object> _requestLocks = new();
    private bool _initialized;
    private readonly object _initLock = new();
    private string? _scenesDirectory;

    public SceneTileSocketServer(string ipAddress, int port, ILogger logger)
        : base(ipAddress, port, logger, startImmediately: false)
    {
        _logger = logger;
    }

    public const float TILE_SIZE = 533.33333f;

    public static string TileKey(uint mapId, uint tileX, uint tileY) => $"{mapId}_{tileX}_{tileY}";
    public static float TileMinX(int tileX) => (32 - tileX) * TILE_SIZE;
    public static float TileMinY(int tileY) => (32 - tileY) * TILE_SIZE;
    public static float TileMaxX(int tileX) => TileMinX(tileX) + TILE_SIZE;
    public static float TileMaxY(int tileY) => TileMinY(tileY) + TILE_SIZE;

    /// <summary>
    /// Pre-load all .scenetile files from the tiles directory into memory.
    /// Each file is read once, parsed into a SceneTileResponse, and cached.
    /// </summary>
    public void LoadTiles(string tilesDirectory)
    {
        lock (_initLock)
        {
            if (_initialized) return;

            _scenesDirectory = ResolveScenesDirectory(tilesDirectory);
            IndexSceneFiles(_scenesDirectory);

            if (!Directory.Exists(tilesDirectory))
            {
                _logger.LogWarning("[SceneTileServer] Tiles directory not found: {Dir}", tilesDirectory);
                _initialized = true;
                return;
            }

            var files = Directory.GetFiles(tilesDirectory, "*.scenetile");
            _logger.LogInformation("[SceneTileServer] Indexing {Count} .scenetile files from {Dir}...", files.Length, tilesDirectory);

            int indexed = 0;
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('_');
                if (parts.Length != 3
                    || !uint.TryParse(parts[0], out var mapId)
                    || !uint.TryParse(parts[1], out var tileX)
                    || !uint.TryParse(parts[2], out var tileY))
                {
                    _logger.LogWarning("[SceneTileServer] Skipping invalid tile filename: {File}", fileName);
                    continue;
                }

                var key = TileKey(mapId, tileX, tileY);
                _tilePaths[key] = file;
                indexed++;
            }

            _initialized = true;
            _logger.LogInformation("[SceneTileServer] Indexed {Count}/{Total} tiles. Ready for on-demand loads.", indexed, files.Length);
        }
    }

    protected override SceneTileResponse HandleRequest(SceneTileRequest request)
    {
        var key = TileKey(request.MapId, request.TileX, request.TileY);

        if (_tileCache.TryGetValue(key, out var cached))
            return cached;

        var requestLock = _requestLocks.GetOrAdd(key, _ => new object());
        lock (requestLock)
        {
            if (_tileCache.TryGetValue(key, out cached))
                return cached;

            if (_tilePaths.TryGetValue(key, out var path))
            {
                try
                {
                    var loaded = LoadTileFile(path, request.MapId, request.TileX, request.TileY);
                    if (loaded != null)
                        return _tileCache.GetOrAdd(key, loaded);

                    return CreateFailureResponse(
                        request.MapId,
                        request.TileX,
                        request.TileY,
                        $"Tile {key} failed to load");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SceneTileServer] Failed to load tile {Key} on demand", key);
                    return CreateFailureResponse(
                        request.MapId,
                        request.TileX,
                        request.TileY,
                        $"Tile {key} failed to load");
                }
            }

            var extracted = TryExtractTileFromSceneSource(request.MapId, request.TileX, request.TileY);
            if (extracted != null)
            {
                return _tileCache.GetOrAdd(key, extracted);
            }

            return CreateFailureResponse(
                request.MapId,
                request.TileX,
                request.TileY,
                $"Tile {key} not found");
        }
    }

    /// <summary>
    /// Load a .scenetile file and parse it into a SceneTileResponse.
    /// The .scenetile format is the same as .scene (SceneCache binary format).
    /// We read the triangles and pack them into the protobuf response.
    /// </summary>
    private SceneTileResponse? LoadTileFile(string path, uint mapId, uint tileX, uint tileY)
    {
        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);

        // Read header (64 bytes)
        uint magic = reader.ReadUInt32();
        if (magic != 0x454E4353) // "SCNE"
        {
            _logger.LogWarning("[SceneTileServer] Invalid magic in {Path}: 0x{Magic:X8}", path, magic);
            return null;
        }

        uint version = reader.ReadUInt32();
        uint fileMapId = reader.ReadUInt32();
        if (version != 1u && version != 2u)
        {
            _logger.LogWarning("[SceneTileServer] Unsupported version in {Path}: {Version}", path, version);
            return null;
        }

        if (fileMapId != mapId)
        {
            _logger.LogWarning("[SceneTileServer] Tile header map mismatch in {Path}: expected {ExpectedMapId}, got {ActualMapId}",
                path, mapId, fileMapId);
            return null;
        }

        uint triCount = reader.ReadUInt32();
        float cellSize = reader.ReadSingle();
        uint cellsX = reader.ReadUInt32();
        uint cellsY = reader.ReadUInt32();
        uint triIdxCount = reader.ReadUInt32();
        float liquidCellSize = reader.ReadSingle();
        uint liquidCellsX = reader.ReadUInt32();
        uint liquidCellsY = reader.ReadUInt32();
        float minX = reader.ReadSingle();
        float minY = reader.ReadSingle();
        float maxX = reader.ReadSingle();
        float maxY = reader.ReadSingle();
        uint reserved = reader.ReadUInt32();

        // Read SceneTri payloads: 9 vertex floats plus sourceType + instanceId.
        var response = new SceneTileResponse
        {
            MapId = mapId,
            TileX = tileX,
            TileY = tileY,
            TriangleCount = triCount,
            Success = true,
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
        };

        // Read all vertex floats into a raw byte buffer, then gzip compress.
        // 9 floats per triangle × 4 bytes = 36 bytes per triangle.
        int floatCount = checked((int)triCount * 9);
        var rawBytes = new byte[floatCount * sizeof(float)];
        var metadataBytes = new byte[checked((int)triCount * 3 * sizeof(uint))];
        int byteOffset = 0;
        int metadataByteOffset = 0;
        var sourceTypes = new uint[checked((int)triCount)];
        var instanceIds = new uint[checked((int)triCount)];

        for (int i = 0; i < (int)triCount; i++)
        {
            for (int f = 0; f < 9; f++)
            {
                float val = reader.ReadSingle();
                BitConverter.TryWriteBytes(rawBytes.AsSpan(byteOffset), val);
                byteOffset += sizeof(float);
            }

            sourceTypes[i] = reader.ReadUInt32();
            instanceIds[i] = reader.ReadUInt32();
        }

        if (version >= 2u)
        {
            for (int i = 0; i < (int)triCount; i++)
            {
                uint sourceType = reader.ReadUInt32();
                uint instanceId = reader.ReadUInt32();
                reader.ReadUInt32(); // instanceFlags
                reader.ReadUInt32(); // modelFlags
                uint groupFlags = reader.ReadUInt32();
                reader.ReadInt32();  // rootId
                reader.ReadInt32();  // groupId

                BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataByteOffset), sourceType);
                metadataByteOffset += sizeof(uint);
                BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataByteOffset), instanceId);
                metadataByteOffset += sizeof(uint);
                BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataByteOffset), groupFlags);
                metadataByteOffset += sizeof(uint);
            }
        }
        else
        {
            for (int i = 0; i < (int)triCount; i++)
            {
                BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataByteOffset), sourceTypes[i]);
                metadataByteOffset += sizeof(uint);
                BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataByteOffset), instanceIds[i]);
                metadataByteOffset += sizeof(uint);
                BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataByteOffset), 0u);
                metadataByteOffset += sizeof(uint);
            }
        }

        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(rawBytes, 0, rawBytes.Length);
        }

        response.TriangleDataCompressed = ByteString.CopyFrom(compressedStream.GetBuffer(), 0, (int)compressedStream.Length);

        if (metadataBytes.Length > 0)
        {
            using var compressedMetadataStream = new MemoryStream();
            using (var gzip = new GZipStream(compressedMetadataStream, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(metadataBytes, 0, metadataBytes.Length);
            }

            response.TriangleMetadataCompressed = ByteString.CopyFrom(
                compressedMetadataStream.GetBuffer(),
                0,
                (int)compressedMetadataStream.Length);
        }

        return response;
    }

    private void IndexSceneFiles(string scenesDirectory)
    {
        if (!Directory.Exists(scenesDirectory))
            return;

        var files = Directory.GetFiles(scenesDirectory, "*.scene");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!uint.TryParse(fileName, out var mapId))
            {
                _logger.LogWarning("[SceneTileServer] Skipping invalid scene filename: {File}", fileName);
                continue;
            }

            _scenePaths[mapId] = file;
        }
    }

    private SceneTileResponse? TryExtractTileFromSceneSource(uint mapId, uint tileX, uint tileY)
    {
        if (!_scenePaths.TryGetValue(mapId, out var scenePath) || !File.Exists(scenePath))
            return null;

        try
        {
            return ExtractTileFromSceneFile(scenePath, mapId, tileX, tileY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[SceneTileServer] Failed to synthesize tile ({TileX},{TileY}) for map {MapId} from {ScenePath}",
                tileX, tileY, mapId, scenePath);
            return null;
        }
    }

    private SceneTileResponse? ExtractTileFromSceneFile(string scenePath, uint mapId, uint tileX, uint tileY)
    {
        using var stream = File.OpenRead(scenePath);
        using var reader = new BinaryReader(stream);

        if (!TryReadSceneHeader(reader, scenePath, out var header))
            return null;

        if (header.MapId != mapId)
        {
            _logger.LogWarning("[SceneTileServer] Scene header map mismatch in {Path}: expected {ExpectedMapId}, got {ActualMapId}",
                scenePath, mapId, header.MapId);
            return null;
        }

        float tileMinX = TileMinX((int)tileX);
        float tileMinY = TileMinY((int)tileY);
        float tileMaxX = TileMaxX((int)tileX);
        float tileMaxY = TileMaxY((int)tileY);

        if (!BoundsOverlap(tileMinX, tileMinY, tileMaxX, tileMaxY, header.MinX, header.MinY, header.MaxX, header.MaxY))
        {
            _logger.LogDebug("[SceneTileServer] Tile ({TileX},{TileY}) for map {MapId} is outside scene bounds; returning empty success",
                tileX, tileY, mapId);
            return CreateEmptySuccessResponse(mapId, tileX, tileY, tileMinX, tileMinY, tileMaxX, tileMaxY);
        }

        var selectedTriangles = new List<float[]>();
        var selectedTriangleLookup = new Dictionary<int, int>();
        var sourceTypes = new List<uint>();
        var instanceIds = new List<uint>();
        var groupFlags = new List<uint>();

        for (int i = 0; i < (int)header.TriangleCount; i++)
        {
            float ax = reader.ReadSingle();
            float ay = reader.ReadSingle();
            float az = reader.ReadSingle();
            float bx = reader.ReadSingle();
            float by = reader.ReadSingle();
            float bz = reader.ReadSingle();
            float cx = reader.ReadSingle();
            float cy = reader.ReadSingle();
            float cz = reader.ReadSingle();
            uint sourceType = reader.ReadUInt32();
            uint instanceId = reader.ReadUInt32();

            if (!TriangleOverlapsTile(ax, ay, bx, by, cx, cy, tileMinX, tileMinY, tileMaxX, tileMaxY))
                continue;

            selectedTriangleLookup[i] = selectedTriangles.Count;
            selectedTriangles.Add([ax, ay, az, bx, by, bz, cx, cy, cz]);
            sourceTypes.Add(sourceType);
            instanceIds.Add(instanceId);
            groupFlags.Add(0u);
        }

        if (header.Version >= 2u)
        {
            for (int i = 0; i < (int)header.TriangleCount; i++)
            {
                uint sourceType = reader.ReadUInt32();
                uint instanceId = reader.ReadUInt32();
                reader.ReadUInt32(); // instanceFlags
                reader.ReadUInt32(); // modelFlags
                uint tileGroupFlags = reader.ReadUInt32();
                reader.ReadInt32();  // rootId
                reader.ReadInt32();  // groupId

                if (selectedTriangleLookup.TryGetValue(i, out var selectedIndex))
                {
                    sourceTypes[selectedIndex] = sourceType;
                    instanceIds[selectedIndex] = instanceId;
                    groupFlags[selectedIndex] = tileGroupFlags;
                }
            }
        }

        if (selectedTriangles.Count == 0)
        {
            _logger.LogDebug("[SceneTileServer] Tile ({TileX},{TileY}) for map {MapId} had no geometry in source scene; returning empty success",
                tileX, tileY, mapId);
            return CreateEmptySuccessResponse(mapId, tileX, tileY, tileMinX, tileMinY, tileMaxX, tileMaxY);
        }

        _logger.LogInformation("[SceneTileServer] Synthesized tile ({TileX},{TileY}) for map {MapId} from {ScenePath} with {TriangleCount} triangles",
            tileX, tileY, mapId, scenePath, selectedTriangles.Count);

        return CreateCompressedResponse(
            mapId,
            tileX,
            tileY,
            tileMinX,
            tileMinY,
            tileMaxX,
            tileMaxY,
            selectedTriangles,
            sourceTypes,
            instanceIds,
            groupFlags);
    }

    private bool TryReadSceneHeader(BinaryReader reader, string path, out SceneHeader header)
    {
        header = default;

        uint magic = reader.ReadUInt32();
        if (magic != 0x454E4353)
        {
            _logger.LogWarning("[SceneTileServer] Invalid magic in {Path}: 0x{Magic:X8}", path, magic);
            return false;
        }

        uint version = reader.ReadUInt32();
        uint mapId = reader.ReadUInt32();
        if (version != 1u && version != 2u)
        {
            _logger.LogWarning("[SceneTileServer] Unsupported version in {Path}: {Version}", path, version);
            return false;
        }

        uint triangleCount = reader.ReadUInt32();
        reader.ReadSingle(); // cellSize
        reader.ReadUInt32(); // cellsX
        reader.ReadUInt32(); // cellsY
        reader.ReadUInt32(); // triIdxCount
        reader.ReadSingle(); // liquidCellSize
        reader.ReadUInt32(); // liquidCellsX
        reader.ReadUInt32(); // liquidCellsY
        float minX = reader.ReadSingle();
        float minY = reader.ReadSingle();
        float maxX = reader.ReadSingle();
        float maxY = reader.ReadSingle();
        reader.ReadUInt32(); // reserved

        header = new SceneHeader(version, mapId, triangleCount, minX, minY, maxX, maxY);
        return true;
    }

    private static SceneTileResponse CreateCompressedResponse(
        uint mapId,
        uint tileX,
        uint tileY,
        float minX,
        float minY,
        float maxX,
        float maxY,
        IReadOnlyList<float[]> triangles,
        IReadOnlyList<uint> sourceTypes,
        IReadOnlyList<uint> instanceIds,
        IReadOnlyList<uint> groupFlags)
    {
        int triangleCount = triangles.Count;
        var response = new SceneTileResponse
        {
            MapId = mapId,
            TileX = tileX,
            TileY = tileY,
            TriangleCount = (uint)triangleCount,
            Success = true,
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
        };

        if (triangleCount == 0)
            return response;

        var rawBytes = new byte[triangleCount * 9 * sizeof(float)];
        var metadataBytes = new byte[triangleCount * 3 * sizeof(uint)];
        int rawOffset = 0;
        int metadataOffset = 0;

        for (int i = 0; i < triangleCount; i++)
        {
            var vertices = triangles[i];
            for (int f = 0; f < vertices.Length; f++)
            {
                BitConverter.TryWriteBytes(rawBytes.AsSpan(rawOffset), vertices[f]);
                rawOffset += sizeof(float);
            }

            BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataOffset), sourceTypes[i]);
            metadataOffset += sizeof(uint);
            BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataOffset), instanceIds[i]);
            metadataOffset += sizeof(uint);
            BitConverter.TryWriteBytes(metadataBytes.AsSpan(metadataOffset), groupFlags[i]);
            metadataOffset += sizeof(uint);
        }

        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(rawBytes, 0, rawBytes.Length);
        }

        response.TriangleDataCompressed = ByteString.CopyFrom(compressedStream.GetBuffer(), 0, (int)compressedStream.Length);

        using var compressedMetadataStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedMetadataStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(metadataBytes, 0, metadataBytes.Length);
        }

        response.TriangleMetadataCompressed = ByteString.CopyFrom(
            compressedMetadataStream.GetBuffer(),
            0,
            (int)compressedMetadataStream.Length);

        return response;
    }

    private static SceneTileResponse CreateEmptySuccessResponse(
        uint mapId,
        uint tileX,
        uint tileY,
        float minX,
        float minY,
        float maxX,
        float maxY)
        => new()
        {
            MapId = mapId,
            TileX = tileX,
            TileY = tileY,
            Success = true,
            TriangleCount = 0,
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
        };

    private static SceneTileResponse CreateFailureResponse(uint mapId, uint tileX, uint tileY, string errorMessage)
        => new()
        {
            MapId = mapId,
            TileX = tileX,
            TileY = tileY,
            Success = false,
            TriangleCount = 0,
            ErrorMessage = errorMessage,
        };

    private static bool TriangleOverlapsTile(
        float ax,
        float ay,
        float bx,
        float by,
        float cx,
        float cy,
        float tileMinX,
        float tileMinY,
        float tileMaxX,
        float tileMaxY)
    {
        float triangleMinX = Math.Min(ax, Math.Min(bx, cx));
        float triangleMaxX = Math.Max(ax, Math.Max(bx, cx));
        float triangleMinY = Math.Min(ay, Math.Min(by, cy));
        float triangleMaxY = Math.Max(ay, Math.Max(by, cy));

        return BoundsOverlap(tileMinX, tileMinY, tileMaxX, tileMaxY, triangleMinX, triangleMinY, triangleMaxX, triangleMaxY);
    }

    private static bool BoundsOverlap(
        float minX,
        float minY,
        float maxX,
        float maxY,
        float otherMinX,
        float otherMinY,
        float otherMaxX,
        float otherMaxY)
        => maxX >= otherMinX
            && minX <= otherMaxX
            && maxY >= otherMinY
            && minY <= otherMaxY;

    private static string ResolveScenesDirectory(string tilesDirectory)
    {
        var directoryInfo = new DirectoryInfo(tilesDirectory);
        if (string.Equals(directoryInfo.Name, "tiles", StringComparison.OrdinalIgnoreCase)
            && directoryInfo.Parent != null)
        {
            return directoryInfo.Parent.FullName;
        }

        return tilesDirectory;
    }

    private readonly record struct SceneHeader(
        uint Version,
        uint MapId,
        uint TriangleCount,
        float MinX,
        float MinY,
        float MaxX,
        float MaxY);
}
