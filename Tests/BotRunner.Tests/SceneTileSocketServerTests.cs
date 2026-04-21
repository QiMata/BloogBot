using System.Collections.Concurrent;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using SceneData;
using SceneDataService;

namespace BotRunner.Tests;

public sealed class SceneTileSocketServerTests : IDisposable
{
    private static readonly MethodInfo HandleRequestMethod = typeof(SceneTileSocketServer).GetMethod(
        "HandleRequest",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SceneTileSocketServer.HandleRequest not found.");

    private static readonly FieldInfo TilePathsField = typeof(SceneTileSocketServer).GetField(
        "_tilePaths",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SceneTileSocketServer._tilePaths not found.");

    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        nameof(SceneTileSocketServerTests),
        Guid.NewGuid().ToString("N"));

    public SceneTileSocketServerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void LoadTiles_IndexesOnlyValidTileFilenames()
    {
        WriteSceneTile(
            Path.Combine(_tempDirectory, "1_29_41.scenetile"),
            version: 2,
            fileMapId: 1,
            triangles: [CreateTriangle(7u, 9001u, 0xAA05u)]);
        File.WriteAllText(Path.Combine(_tempDirectory, "not_a_tile.scenetile"), "invalid");

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var tilePaths = (ConcurrentDictionary<string, string>)TilePathsField.GetValue(server)!;
        Assert.Single(tilePaths);
        Assert.True(tilePaths.ContainsKey(SceneTileSocketServer.TileKey(1, 29, 41)));
    }

    [Fact]
    public void HandleRequest_Version1Tile_CompressesVerticesAndSynthesizesMetadata()
    {
        var path = Path.Combine(_tempDirectory, "1_29_41.scenetile");
        var triangle = CreateTriangle(7u, 9001u, 0u);
        WriteSceneTile(path, version: 1, fileMapId: 1, triangles: [triangle], minX: 10f, minY: 20f, maxX: 30f, maxY: 40f);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var response = InvokeHandleRequest(server, 1, 29, 41);

        Assert.True(response.Success);
        Assert.Equal(1u, response.TriangleCount);
        Assert.Equal(10f, response.MinX);
        Assert.Equal(20f, response.MinY);
        Assert.Equal(30f, response.MaxX);
        Assert.Equal(40f, response.MaxY);
        Assert.Empty(response.TriangleData);
        Assert.NotEmpty(response.TriangleDataCompressed);
        Assert.NotEmpty(response.TriangleMetadataCompressed);

        Assert.Equal(triangle.Vertices, DecompressFloats(response.TriangleDataCompressed, 9));
        Assert.Equal([triangle.SourceType, triangle.InstanceId, 0u], DecompressUInts(response.TriangleMetadataCompressed, 3));
    }

    [Fact]
    public void HandleRequest_Version2Tile_PreservesGroupFlagsInMetadata()
    {
        var triangle = CreateTriangle(11u, 4242u, 0x0000AA05u);
        WriteSceneTile(
            Path.Combine(_tempDirectory, "1_29_41.scenetile"),
            version: 2,
            fileMapId: 1,
            triangles: [triangle]);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var response = InvokeHandleRequest(server, 1, 29, 41);

        Assert.True(response.Success);
        Assert.Equal([triangle.SourceType, triangle.InstanceId, triangle.GroupFlags], DecompressUInts(response.TriangleMetadataCompressed, 3));
    }

    [Fact]
    public void HandleRequest_MapHeaderMismatch_ReturnsFailure()
    {
        WriteSceneTile(
            Path.Combine(_tempDirectory, "1_29_41.scenetile"),
            version: 2,
            fileMapId: 30,
            triangles: [CreateTriangle(7u, 9001u, 0u)]);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var response = InvokeHandleRequest(server, 1, 29, 41);

        Assert.False(response.Success);
        Assert.Equal(0u, response.TriangleCount);
        Assert.Equal("Tile 1_29_41 failed to load", response.ErrorMessage);
    }

    [Fact]
    public void HandleRequest_DoesNotCacheNegativeLoadResults()
    {
        var path = Path.Combine(_tempDirectory, "1_29_41.scenetile");
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var failed = InvokeHandleRequest(server, 1, 29, 41);
        Assert.False(failed.Success);

        var triangle = CreateTriangle(7u, 9001u, 0u);
        WriteSceneTile(path, version: 2, fileMapId: 1, triangles: [triangle]);

        var recovered = InvokeHandleRequest(server, 1, 29, 41);
        Assert.True(recovered.Success);
        Assert.Equal([triangle.SourceType, triangle.InstanceId, triangle.GroupFlags], DecompressUInts(recovered.TriangleMetadataCompressed, 3));
    }

    [Fact]
    public void HandleRequest_CachesSuccessfulLoads()
    {
        var path = Path.Combine(_tempDirectory, "1_29_41.scenetile");
        var original = CreateTriangle(3u, 77u, 0x10u);
        WriteSceneTile(path, version: 2, fileMapId: 1, triangles: [original]);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var first = InvokeHandleRequest(server, 1, 29, 41);
        Assert.True(first.Success);

        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);

        var second = InvokeHandleRequest(server, 1, 29, 41);
        Assert.True(second.Success);
        Assert.Equal(DecompressUInts(first.TriangleMetadataCompressed, 3), DecompressUInts(second.TriangleMetadataCompressed, 3));
    }

    [Fact]
    public void HandleRequest_MissingTileWithoutSceneSource_ReturnsFailure()
    {
        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var response = InvokeHandleRequest(server, 1, 29, 41);

        Assert.False(response.Success);
        Assert.Equal(0u, response.TriangleCount);
        Assert.Equal("Tile 1_29_41 not found", response.ErrorMessage);
    }

    [Fact]
    public void HandleRequest_MissingTileWithSceneSource_ExtractsAndCachesResponse()
    {
        var sourceTriangle = CreateTriangleInTile(29, 41, 17u, 8181u, 0x0000AA05u);
        var scenePath = Path.Combine(_tempDirectory, "1.scene");
        WriteSceneFile(scenePath, version: 2, fileMapId: 1, triangles: [sourceTriangle]);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var first = InvokeHandleRequest(server, 1, 29, 41);
        Assert.True(first.Success);
        Assert.Equal(1u, first.TriangleCount);
        Assert.Equal([sourceTriangle.SourceType, sourceTriangle.InstanceId, sourceTriangle.GroupFlags], DecompressUInts(first.TriangleMetadataCompressed, 3));

        File.WriteAllBytes(scenePath, [0x00, 0x01, 0x02, 0x03]);

        var second = InvokeHandleRequest(server, 1, 29, 41);
        Assert.True(second.Success);
        Assert.Equal(1u, second.TriangleCount);
        Assert.Equal(DecompressUInts(first.TriangleMetadataCompressed, 3), DecompressUInts(second.TriangleMetadataCompressed, 3));
    }

    [Fact]
    public void HandleRequest_MissingTileWithSceneSourceAndNoGeometry_ReturnsEmptySuccess()
    {
        var sourceTriangle = CreateTriangleInTile(29, 41, 17u, 8181u, 0x0000AA05u);
        var scenePath = Path.Combine(_tempDirectory, "1.scene");
        WriteSceneFile(scenePath, version: 2, fileMapId: 1, triangles: [sourceTriangle]);

        using var server = CreateServer();
        server.LoadTiles(_tempDirectory);

        var response = InvokeHandleRequest(server, 1, 30, 41);

        Assert.True(response.Success);
        Assert.Equal(0u, response.TriangleCount);
        Assert.Empty(response.ErrorMessage);
        Assert.Empty(response.TriangleDataCompressed);
        Assert.Empty(response.TriangleMetadataCompressed);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
        }
    }

    private static SceneTileSocketServer CreateServer()
        => new("127.0.0.1", 0, NullLogger.Instance);

    private static SceneTileResponse InvokeHandleRequest(SceneTileSocketServer server, uint mapId, uint tileX, uint tileY)
        => (SceneTileResponse)HandleRequestMethod.Invoke(
            server,
            [new SceneTileRequest { MapId = mapId, TileX = tileX, TileY = tileY }])!;

    private static float[] DecompressFloats(ByteString compressed, int count)
    {
        var bytes = DecompressBytes(compressed);
        Assert.Equal(count * sizeof(float), bytes.Length);

        var values = new float[count];
        for (int i = 0; i < count; i++)
            values[i] = BitConverter.ToSingle(bytes, i * sizeof(float));

        return values;
    }

    private static uint[] DecompressUInts(ByteString compressed, int count)
    {
        var bytes = DecompressBytes(compressed);
        Assert.Equal(count * sizeof(uint), bytes.Length);

        var values = new uint[count];
        for (int i = 0; i < count; i++)
            values[i] = BitConverter.ToUInt32(bytes, i * sizeof(uint));

        return values;
    }

    private static byte[] DecompressBytes(ByteString compressed)
    {
        using var source = new MemoryStream(compressed.ToByteArray());
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var destination = new MemoryStream();
        gzip.CopyTo(destination);
        return destination.ToArray();
    }

    private static TriangleFixture CreateTriangle(uint sourceType, uint instanceId, uint groupFlags)
        => new(
            [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f],
            sourceType,
            instanceId,
            groupFlags);

    private static TriangleFixture CreateTriangleInTile(uint tileX, uint tileY, uint sourceType, uint instanceId, uint groupFlags)
    {
        float minX = SceneTileSocketServer.TileMinX((int)tileX);
        float minY = SceneTileSocketServer.TileMinY((int)tileY);
        return new TriangleFixture(
            [minX + 10f, minY + 10f, 3f, minX + 30f, minY + 10f, 3f, minX + 10f, minY + 30f, 3f],
            sourceType,
            instanceId,
            groupFlags);
    }

    private static void WriteSceneFile(
        string path,
        uint version,
        uint fileMapId,
        TriangleFixture[] triangles)
    {
        float minX = triangles.Min(t => Math.Min(t.Vertices[0], Math.Min(t.Vertices[3], t.Vertices[6])));
        float minY = triangles.Min(t => Math.Min(t.Vertices[1], Math.Min(t.Vertices[4], t.Vertices[7])));
        float maxX = triangles.Max(t => Math.Max(t.Vertices[0], Math.Max(t.Vertices[3], t.Vertices[6])));
        float maxY = triangles.Max(t => Math.Max(t.Vertices[1], Math.Max(t.Vertices[4], t.Vertices[7])));
        WriteSceneTile(path, version, fileMapId, triangles, minX, minY, maxX, maxY);
    }

    private static void WriteSceneTile(
        string path,
        uint version,
        uint fileMapId,
        TriangleFixture[] triangles,
        float minX = 100f,
        float minY = 200f,
        float maxX = 300f,
        float maxY = 400f)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(0x454E4353u);
        writer.Write(version);
        writer.Write(fileMapId);
        writer.Write((uint)triangles.Length);
        writer.Write(4.0f);
        writer.Write(1u);
        writer.Write(1u);
        writer.Write(0u);
        writer.Write(4.17f);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(minX);
        writer.Write(minY);
        writer.Write(maxX);
        writer.Write(maxY);
        writer.Write(0u);

        foreach (var triangle in triangles)
        {
            foreach (var value in triangle.Vertices)
                writer.Write(value);

            writer.Write(triangle.SourceType);
            writer.Write(triangle.InstanceId);
        }

        if (version >= 2u)
        {
            foreach (var triangle in triangles)
            {
                writer.Write(triangle.SourceType);
                writer.Write(triangle.InstanceId);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(triangle.GroupFlags);
                writer.Write(-1);
                writer.Write(-1);
            }
        }
    }

    private sealed record TriangleFixture(float[] Vertices, uint SourceType, uint InstanceId, uint GroupFlags);
}
