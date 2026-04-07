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
        : base(ipAddress, port, logger)
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

        // Read triangles (SceneTri = 36 bytes: 9 floats + sourceType + instanceId)
        // But SceneTri is actually: ax,ay,az,bx,by,bz,cx,cy,cz (9 floats) + sourceType(u32) + instanceId(u32) = 44 bytes
        const int SCENE_TRI_SIZE = 44; // 9 * 4 + 4 + 4
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
        int floatCount = (int)triCount * 9;
        var rawBytes = new byte[floatCount * 4];
        int byteOffset = 0;

        for (uint i = 0; i < triCount; i++)
        {
            // Read 9 vertex floats + skip sourceType(4) + instanceId(4)
            for (int f = 0; f < 9; f++)
            {
                float val = reader.ReadSingle();
                BitConverter.TryWriteBytes(rawBytes.AsSpan(byteOffset), val);
                byteOffset += 4;
            }
            reader.ReadUInt32(); // sourceType (skip)
            reader.ReadUInt32(); // instanceId (skip)
        }

        // GZip compress
        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(rawBytes, 0, rawBytes.Length);
        }

        response.TriangleDataCompressed = ByteString.CopyFrom(compressedStream.GetBuffer(), 0, (int)compressedStream.Length);

        return response;
    }
}
