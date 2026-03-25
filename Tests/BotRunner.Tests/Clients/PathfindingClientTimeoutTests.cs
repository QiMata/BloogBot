using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Pathfinding;

namespace BotRunner.Tests.Clients;

public sealed class PathfindingClientTimeoutTests
{
    [Fact]
    public async Task GetPath_Succeeds_WhenResponseExceedsPhysicsTimeoutButFitsPathTimeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            await ReadMessageAsync(stream);
            await Task.Delay(150);

            var response = new PathfindingResponse
            {
                Path = new CalculatePathResponse
                {
                    Result = "native_path",
                    RawCornerCount = 2
                }
            };
            response.Path.Corners.Add(new Game.Position { X = 1f, Y = 2f, Z = 3f });
            response.Path.Corners.Add(new Game.Position { X = 4f, Y = 5f, Z = 6f });

            await WriteMessageAsync(stream, response);
        });

        using var client = new PathfindingClient(
            "127.0.0.1",
            port,
            NullLogger<PathfindingClient>.Instance,
            pathRequestTimeoutMs: 500,
            queryTimeoutMs: 100,
            physicsTimeoutMs: 50);

        var path = client.GetPath(1, new Position(0f, 0f, 0f), new Position(10f, 10f, 10f));

        Assert.Equal(2, path.Length);
        Assert.Equal(1f, path[0].X);
        Assert.Equal(6f, path[1].Z);

        await serverTask;
    }

    [Fact]
    public void PhysicsStep_UsesConfiguredPhysicsTimeout()
    {
        var client = new TimeoutCapturingPathfindingClient(
            pathRequestTimeoutMs: 500,
            queryTimeoutMs: 100,
            physicsTimeoutMs: 50,
            responseFactory: request => new PathfindingResponse
            {
                Step = new PhysicsOutput
                {
                    NewPosX = 1f,
                    NewPosY = 2f,
                    NewPosZ = 3f,
                }
            });

        _ = client.PhysicsStep(new PhysicsInput
        {
            PosX = 0f,
            PosY = 0f,
            PosZ = 0f,
            RunSpeed = 7f,
            RunBackSpeed = 4.5f,
            DeltaTime = 0.016f,
            MovementFlags = (uint)MovementFlags.MOVEFLAG_NONE,
        });

        Assert.Equal(50, client.LastTimeoutMs);
    }

    [Fact]
    public void GetPath_UsesConfiguredPathTimeout()
    {
        var client = new TimeoutCapturingPathfindingClient(
            pathRequestTimeoutMs: 500,
            queryTimeoutMs: 100,
            physicsTimeoutMs: 50,
            responseFactory: request => new PathfindingResponse
            {
                Path = new CalculatePathResponse
                {
                    Result = "native_path",
                    RawCornerCount = 0
                }
            });

        _ = client.GetPath(1, new Position(0f, 0f, 0f), new Position(10f, 10f, 10f));

        Assert.Equal(500, client.LastTimeoutMs);
    }

    [Fact]
    public async Task PhysicsStep_ReconnectBudget_StaysWithinPhysicsTimeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var firstRequestHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using var acceptedClient = await listener.AcceptTcpClientAsync();
            using var stream = acceptedClient.GetStream();

            await ReadMessageAsync(stream);
            await WriteMessageAsync(stream, new PathfindingResponse
            {
                Step = new PhysicsOutput
                {
                    NewPosX = 10f,
                    NewPosY = 20f,
                    NewPosZ = 30f,
                    MovementFlags = (uint)MovementFlags.MOVEFLAG_FORWARD,
                }
            });

            firstRequestHandled.SetResult();
        });

        using var client = new PathfindingClient(
            "127.0.0.1",
            port,
            NullLogger<PathfindingClient>.Instance,
            pathRequestTimeoutMs: 1000,
            queryTimeoutMs: 1000,
            physicsTimeoutMs: 500);

        var firstOutput = client.PhysicsStep(new PhysicsInput
        {
            PosX = 1f,
            PosY = 2f,
            PosZ = 3f,
            RunSpeed = 7f,
            RunBackSpeed = 4.5f,
            DeltaTime = 0.016f,
            MovementFlags = (uint)MovementFlags.MOVEFLAG_FORWARD,
        });

        Assert.Equal(10f, firstOutput.NewPosX);
        await firstRequestHandled.Task;
        await serverTask;

        listener.Stop();

        var reconnectStopwatch = Stopwatch.StartNew();
        var fallbackOutput = client.PhysicsStep(new PhysicsInput
        {
            PosX = 11f,
            PosY = 12f,
            PosZ = 13f,
            RunSpeed = 7f,
            RunBackSpeed = 4.5f,
            DeltaTime = 0.016f,
            MovementFlags = (uint)MovementFlags.MOVEFLAG_FORWARD,
        });
        reconnectStopwatch.Stop();

        Assert.InRange(reconnectStopwatch.ElapsedMilliseconds, 0, 3000);
        Assert.Equal(11f, fallbackOutput.NewPosX);
        Assert.Equal(12f, fallbackOutput.NewPosY);
        Assert.Equal(13f, fallbackOutput.NewPosZ);
        Assert.False(client.IsAvailable);
    }

    [Fact]
    public async Task GetPath_ReconnectBudget_StaysWithinPathTimeout_AndReturnsEmptyPathOnFailure()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var firstRequestHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using var acceptedClient = await listener.AcceptTcpClientAsync();
            using var stream = acceptedClient.GetStream();

            await ReadMessageAsync(stream);
            var response = new PathfindingResponse
            {
                Path = new CalculatePathResponse
                {
                    Result = "native_path",
                    RawCornerCount = 1
                }
            };
            response.Path.Corners.Add(new Game.Position { X = 4f, Y = 5f, Z = 6f });
            await WriteMessageAsync(stream, response);
            firstRequestHandled.SetResult();
        });

        using var client = new PathfindingClient(
            "127.0.0.1",
            port,
            NullLogger<PathfindingClient>.Instance,
            pathRequestTimeoutMs: 500,
            queryTimeoutMs: 1000,
            physicsTimeoutMs: 1000);

        var firstPath = client.GetPath(1, new Position(0f, 0f, 0f), new Position(10f, 10f, 10f));
        Assert.Single(firstPath);
        await firstRequestHandled.Task;
        await serverTask;

        listener.Stop();

        var reconnectStopwatch = Stopwatch.StartNew();
        var fallbackPath = client.GetPath(1, new Position(1f, 2f, 3f), new Position(4f, 5f, 6f));
        reconnectStopwatch.Stop();

        Assert.InRange(reconnectStopwatch.ElapsedMilliseconds, 0, 3000);
        Assert.Empty(fallbackPath);
        Assert.False(client.IsAvailable);
    }

    [Fact]
    public async Task IsInLineOfSight_ReconnectBudget_StaysWithinQueryTimeout_AndReturnsFalseOnFailure()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var firstRequestHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using var acceptedClient = await listener.AcceptTcpClientAsync();
            using var stream = acceptedClient.GetStream();

            await ReadMessageAsync(stream);
            await WriteMessageAsync(stream, new PathfindingResponse
            {
                Los = new LineOfSightResponse
                {
                    InLos = true
                }
            });
            firstRequestHandled.SetResult();
        });

        using var client = new PathfindingClient(
            "127.0.0.1",
            port,
            NullLogger<PathfindingClient>.Instance,
            pathRequestTimeoutMs: 1000,
            queryTimeoutMs: 500,
            physicsTimeoutMs: 1000);

        Assert.True(client.IsInLineOfSight(1, new Position(0f, 0f, 0f), new Position(1f, 1f, 1f)));
        await firstRequestHandled.Task;
        await serverTask;

        listener.Stop();

        var reconnectStopwatch = Stopwatch.StartNew();
        var fallbackLos = client.IsInLineOfSight(1, new Position(2f, 2f, 2f), new Position(3f, 3f, 3f));
        reconnectStopwatch.Stop();

        Assert.InRange(reconnectStopwatch.ElapsedMilliseconds, 0, 3000);
        Assert.False(fallbackLos);
        Assert.False(client.IsAvailable);
    }

    private static async Task ReadMessageAsync(NetworkStream stream)
    {
        var lengthBytes = await ReadExactAsync(stream, 4);
        var length = BitConverter.ToInt32(lengthBytes, 0);
        _ = await ReadExactAsync(stream, length);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset));
            if (read == 0)
                throw new IOException("Unexpected EOF while reading test message.");

            offset += read;
        }

        return buffer;
    }

    private static async Task WriteMessageAsync(NetworkStream stream, PathfindingResponse response)
    {
        var payload = response.ToByteArray();
        var length = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(length);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private sealed class TimeoutCapturingPathfindingClient(
        int pathRequestTimeoutMs,
        int queryTimeoutMs,
        int physicsTimeoutMs,
        Func<PathfindingRequest, PathfindingResponse> responseFactory) : PathfindingClient(
            pathRequestTimeoutMs,
            queryTimeoutMs,
            physicsTimeoutMs)
    {
        public int? LastTimeoutMs { get; private set; }

        protected override PathfindingResponse SendRequest(PathfindingRequest request, int timeoutMs)
        {
            LastTimeoutMs = timeoutMs;
            return responseFactory(request);
        }
    }
}
