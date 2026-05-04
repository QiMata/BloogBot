using BotCommLayer;
using GameData.Core.Enums;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Pathfinding;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PathfindingService.Tests;

public sealed class PathfindingSocketServerIntegrationTests
{
    [Fact]
    public async Task HandlePath_LiveCorpseRunRoute_ReturnsValidatedPathWithinBudget()
    {
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

        var stopwatch = Stopwatch.StartNew();
        var responseTask = SendRequestAsync(port, request);
        var completedTask = await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(10)));
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
        Assert.True(stopwatch.ElapsedMilliseconds < 10_000, $"Socket path response took {stopwatch.ElapsedMilliseconds}ms.");

        var first = response.Path.Corners[0];
        var last = response.Path.Corners[^1];
        Assert.InRange(Distance2D(first.X, first.Y, request.Path.Start.X, request.Path.Start.Y), 0f, 10f);
        Assert.InRange(Distance2D(last.X, last.Y, request.Path.End.X, request.Path.End.Y), 0f, 12f);
    }

    [Fact]
    public async Task HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract()
    {
        var port = GetFreePort();
        using var server = new PathfindingSocketServer("127.0.0.1", port, NullLogger<PathfindingSocketServer>.Instance);
        server.InitializeNavigation();

        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = 1677.6f, Y = -4315.7f, Z = 61.2f },
                End = new Game.Position { X = 1320.142944f, Y = -4653.158691f, Z = 53.891945f },
                Straight = true,
                Race = (uint)Race.Tauren,
                Gender = (uint)Gender.Male,
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var responseTask = SendRequestAsync(port, request);
        var completedTask = await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(5)));
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
            stopwatch.ElapsedMilliseconds < 5_000,
            $"Cached route-pack response took {stopwatch.ElapsedMilliseconds}ms.");

        var recoveryRequest = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = 1363.9f, Y = -4377.8f, Z = 26.1f },
                End = new Game.Position { X = 1320.142944f, Y = -4653.158691f, Z = 53.891945f },
                Straight = true,
                Race = (uint)Race.Tauren,
                Gender = (uint)Gender.Male,
            }
        };

        stopwatch.Restart();
        responseTask = SendRequestAsync(port, recoveryRequest);
        completedTask = await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(responseTask, completedTask);

        response = await responseTask;
        stopwatch.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.Equal("route_pack_main_path", response.Path.Result);
        Assert.Equal("none", response.Path.BlockedReason);
        Assert.False(response.Path.HasBlockedSegment);
        Assert.True(response.Path.PathSupported);
        Assert.True(response.Path.Corners.Count >= 3);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5_000,
            $"Cached lower-incline recovery route-pack response took {stopwatch.ElapsedMilliseconds}ms.");
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
}
