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
    private bool _initialized;
    private readonly object _initLock = new();

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

            if (!Directory.Exists(tilesDirectory))
            {
                _logger.LogWarning("[SceneTileServer] Tiles directory not found: {Dir}", tilesDirectory);
                _initialized = true;
                return;
            }

            var files = Directory.GetFiles(tilesDirectory, "*.scenetile");
            _logger.LogInformation("[SceneTileServer] Loading {Count} .scenetile files from {Dir}...", files.Length, tilesDirectory);

            int loaded = 0;
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

                try
                {
                    var response = LoadTileFile(file, mapId, tileX, tileY);
                    if (response != null)
                    {
                        var key = TileKey(mapId, tileX, tileY);
                        _tileCache[key] = response;
                        loaded++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SceneTileServer] Failed to load tile {File}", fileName);
                }
            }

            _initialized = true;
            _logger.LogInformation("[SceneTileServer] Loaded {Count}/{Total} tiles. Ready.", loaded, files.Length);
        }
    }

    protected override SceneTileResponse HandleRequest(SceneTileRequest request)
    {
        var key = TileKey(request.MapId, request.TileX, request.TileY);

        if (_tileCache.TryGetValue(key, out var cached))
            return cached;

        return new SceneTileResponse
        {
            MapId = request.MapId,
            TileX = request.TileX,
            TileY = request.TileY,
            Success = false,
            TriangleCount = 0,
            ErrorMessage = $"Tile {key} not found",
        };
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
}
