using BotCommLayer;
using GameData.Core.Enums;
using GameData.Core.Models;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Pathfinding;
using PathfindingService.Repository;
using PathfindingService.RoutePacks;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace PathfindingService.Tests;

[Collection(NavigationCollection.Name)]
public sealed class PathfindingSocketServerIntegrationTests(NavigationFixture fixture)
{
    private readonly NavigationFixture _fixture = fixture;

    [Fact]
    public async Task HandlePath_LiveCorpseRunRoute_ReturnsValidatedPathWithinBudget()
    {
        _ = _fixture.Navigation;
        var port = GetFreePort();
        using var server = new PathfindingSocketServer("127.0.0.1", port, NullLogger<PathfindingSocketServer>.Instance);
        server.InitializeNavigation();

        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = 1177.8f, Y = -4464.2f, Z = 21.4f },
                End = new Game.Position { X = 1629.4f, Y = -4373.4f, Z = 31.3f },
                Straight = false,
            }
        };

        var responseBudget = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();
        var responseTask = SendRequestAsync(port, request);
        var completedTask = await Task.WhenAny(responseTask, Task.Delay(responseBudget));
        Assert.Same(responseTask, completedTask);

        var response = await responseTask;
        stopwatch.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.NotEqual("no_path", response.Path.Result);
        Assert.Equal("none", response.Path.BlockedReason);
        Assert.False(response.Path.HasBlockedSegment);
        Assert.True(response.Path.MaxSlopeAngleDeg > 0f);
        Assert.True(
            response.Path.Corners.Count >= 3,
            $"Expected a real corpse-run waypoint chain, got {response.Path.Corners.Count} corners with result '{response.Path.Result}'.");
        Assert.True(stopwatch.Elapsed < responseBudget, $"Socket path response took {stopwatch.ElapsedMilliseconds}ms.");

        var first = response.Path.Corners[0];
        var last = response.Path.Corners[^1];
        Assert.InRange(Distance2D(first.X, first.Y, request.Path.Start.X, request.Path.Start.Y), 0f, 10f);
        Assert.InRange(Distance2D(last.X, last.Y, request.Path.End.X, request.Path.End.Y), 0f, 12f);
    }

    [Fact]
    public async Task HandlePath_DeckLipGruntNpcToLiteralFrezza_ReturnsCurrentServicePathThroughIsolatedPort()
    {
        _ = _fixture.Navigation;
        var port = GetFreePort();
        using var server = new PathfindingSocketServer("127.0.0.1", port, NullLogger<PathfindingSocketServer>.Instance);
        server.InitializeNavigation();

        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = 1332.76f, Y = -4633.40f, Z = 24.0783f },
                End = new Game.Position { X = 1331.11f, Y = -4649.45f, Z = 53.6269f },
                Straight = true,
                Race = (uint)Race.Tauren,
                Gender = (uint)Gender.Male,
            }
        };

        var responseBudget = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();
        var responseTask = SendRequestAsync(port, request);
        var completedTask = await Task.WhenAny(responseTask, Task.Delay(responseBudget));
        Assert.Same(responseTask, completedTask);

        var response = await responseTask;
        stopwatch.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.Equal("raw_detour", response.Path.Result);
        Assert.True(response.Path.HasBlockedSegment);
        Assert.InRange(response.Path.BlockedSegmentIndex, 90, 110);
        Assert.StartsWith("interior_projection:", response.Path.BlockedReason, StringComparison.Ordinal);
        Assert.True(
            response.Path.Corners.Count >= 120,
            $"Expected the current promoted Grunt #1 -> Frezza service path to preserve the long tower climb; got {response.Path.Corners.Count} corners.");
        Assert.True(stopwatch.Elapsed < responseBudget, $"Socket path response took {stopwatch.ElapsedMilliseconds}ms.");

        var first = response.Path.Corners[0];
        var last = response.Path.Corners[^1];
        var startDistance2D = Distance2D(first.X, first.Y, request.Path.Start.X, request.Path.Start.Y);
        var finalDistance2D = Distance2D(last.X, last.Y, request.Path.End.X, request.Path.End.Y);
        var finalDeltaZ = MathF.Abs(last.Z - request.Path.End.Z);

        Console.WriteLine(
            $"Socket literal Frezza path: result={response.Path.Result} len={response.Path.Corners.Count} blockedSeg={(response.Path.HasBlockedSegment ? response.Path.BlockedSegmentIndex.ToString() : "null")} blockedReason={response.Path.BlockedReason} firstDist2D={startDistance2D:F2} final=({last.X:F2},{last.Y:F2},{last.Z:F2}) dist2D={finalDistance2D:F2} dz={finalDeltaZ:F2}");

        Assert.InRange(startDistance2D, 0f, 10f);
        Assert.True(
            finalDistance2D <= 4f,
            $"Expected the socket Grunt #1 -> Frezza path to finish near the literal Frezza spawn; final 2D distance was {finalDistance2D:F2}y.");
        Assert.True(
            finalDeltaZ <= 1f,
            $"Expected the socket Grunt #1 -> Frezza path to finish on Frezza's upper-deck Z layer; final |dz| was {finalDeltaZ:F2}y.");
    }

    [Fact]
    public async Task HandlePath_RepeatedStaticRequest_UsesServiceRouteCacheThroughNormalContract()
    {
        using var overlayEnv = new EnvironmentVariableScope("WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY", null);
        var port = GetFreePort();
        using var server = new PathfindingSocketServer("127.0.0.1", port, NullLogger<PathfindingSocketServer>.Instance);
        var seed = new StaticRoutePackSeed(
            Id: "socket_cache_contract",
            MapId: 1,
            StartAnchor: new XYZ(0f, 0f, 0f),
            EndAnchor: new XYZ(20f, 0f, 0f),
            Race: Race.Tauren,
            Gender: Gender.Male,
            SmoothPath: true,
            RoutePolicy: StaticRoutePackCache.DefaultRoutePolicy,
            AllowsDynamicOverlay: false,
            StartAnchorRadius: 1f,
            EndAnchorRadius: 1f,
            CorridorProjectionRadius: 1f,
            MaxProjectionZDrift: 1f,
            MaxSegmentLength: 12f);
        var generatedPath = new[]
        {
            seed.StartAnchor,
            new XYZ(10f, 0f, 0f),
            seed.EndAnchor,
        };
        var routePackCache = new StaticRoutePackCache(
            [seed],
            new FixedSignatureProvider("socket-test-navsig"),
            _ => new NavigationPathResult(generatedPath, generatedPath, "native_path", null, "none"),
            SegmentAlwaysSupported);
        Assert.True(routePackCache.WarmUp(seed));
        SetPrivateField(server, "_mainPathCache", routePackCache);
        SetPrivateField(server, "_isInitialized", true);

        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = seed.StartAnchor.X, Y = seed.StartAnchor.Y, Z = seed.StartAnchor.Z },
                End = new Game.Position { X = seed.EndAnchor.X, Y = seed.EndAnchor.Y, Z = seed.EndAnchor.Z },
                Straight = true,
                Race = (uint)seed.Race,
                Gender = (uint)seed.Gender,
            }
        };
        request.Path.NearbyObjects.Add(new DynamicObjectProto
        {
            Guid = 0x1001,
            Entry = 17001,
            DisplayId = 17,
            X = 10f,
            Y = 0f,
            Z = 0f,
            Scale = 1f,
        });

        var stopwatch = Stopwatch.StartNew();
        var responseTask = SendRequestAsync(port, request);
        var completedTask = await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(responseTask, completedTask);

        var response = await responseTask;
        stopwatch.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.Equal("route_pack_main_path", response.Path.Result);
        Assert.Equal("none", response.Path.BlockedReason);
        Assert.False(response.Path.HasBlockedSegment);
        Assert.True(response.Path.PathSupported);
        Assert.True(response.Path.Corners.Count >= 3);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 2_000,
            $"Initial route-pack socket response took {stopwatch.ElapsedMilliseconds}ms.");

        stopwatch.Restart();
        responseTask = SendRequestAsync(port, request);
        completedTask = await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(responseTask, completedTask);

        response = await responseTask;
        stopwatch.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.Equal("route_pack_main_path", response.Path.Result);
        Assert.True(server.RouteCacheStats.HitCount >= 1);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 2_000,
            $"Service route-cache response took {stopwatch.ElapsedMilliseconds}ms.");
    }

    private static async Task<PathfindingResponse> SendRequestAsync(int port, PathfindingRequest request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);

        using var stream = client.GetStream();
        stream.ReadTimeout = 30_000;
        stream.WriteTimeout = 30_000;

        var payload = request.ToByteArray();
        var payloadLength = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(payloadLength);
        await stream.WriteAsync(payload);

        var responseLengthBytes = await ReadExactAsync(stream, 4);
        var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
        var responsePayload = await ReadExactAsync(stream, responseLength);

        var response = new PathfindingResponse();
        response.MergeFrom(ProtobufCompression.Decode(responsePayload));
        return response;
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, length - offset));
            if (bytesRead == 0)
            {
                throw new IOException("Unexpected EOF while reading socket response.");
            }

            offset += bytesRead;
        }

        return buffer;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool SegmentAlwaysSupported(uint mapId, XYZ from, XYZ to, float radius, float height) => true;

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
            => Environment.SetEnvironmentVariable(_name, _previousValue);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private sealed class FixedSignatureProvider(string signature) : INavigationDataSignatureProvider
    {
        public string GetSignature(uint mapId) => signature;
    }
}
