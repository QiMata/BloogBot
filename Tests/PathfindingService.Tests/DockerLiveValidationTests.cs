using BotCommLayer;
using GameData.Core.Models;
using Google.Protobuf;
using Pathfinding;
using System.Diagnostics;
using System.Net.Sockets;
using Xunit;

namespace PathfindingService.Tests;

// Validation harness that hits the live Docker wwow-pathfinding container
// on 127.0.0.1:5001 to confirm production behaviour. NOT auto-run — guard
// with WWOW_RUN_DOCKER_VALIDATION=1.
[Trait("Category", "DockerLive")]
public sealed class DockerLiveValidationTests
{
    private const string DockerHost = "127.0.0.1";
    private const int DockerPort = 5001;

    [SkippableFact]
    public async Task DockerPathfinding_ShortDurotarRoute_RespondsWithinBudget()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("WWOW_RUN_DOCKER_VALIDATION") == "1", "WWOW_RUN_DOCKER_VALIDATION not set");

        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = -562.225f, Y = -4189.092f, Z = 70.789f },
                End = new Game.Position { X = -568.0f, Y = -4180.0f, Z = 70.789f },
                Straight = false,
            }
        };

        var sw = Stopwatch.StartNew();
        var response = await SendRequestAsync(request);
        sw.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.NotEmpty(response.Path.Corners);
        Console.Error.WriteLine($"[DOCKER-VALIDATE] short-durotar map=1 corners={response.Path.Corners.Count} elapsed={sw.ElapsedMilliseconds}ms result={response.Path.Result}");
        Assert.True(sw.Elapsed.TotalSeconds < 5, $"short-route response took {sw.Elapsed.TotalSeconds:F1}s; expected <5s");
    }

    [SkippableFact]
    public async Task DockerPathfinding_LongDurotarToCrossroads_RespondsWithinBudget()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("WWOW_RUN_DOCKER_VALIDATION") == "1", "WWOW_RUN_DOCKER_VALIDATION not set");

        // 500y Durotar→Crossroads — the canonical long-route stress case
        // from project_pfs_calculatepath_hang_durotar. Pre-fixes this
        // hung indefinitely; post-iter-14 the C# 30s budget caps it.
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

        var sw = Stopwatch.StartNew();
        var response = await SendRequestAsync(request);
        sw.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Console.Error.WriteLine($"[DOCKER-VALIDATE] long-durotar→crossroads map=1 corners={response.Path.Corners.Count} elapsed={sw.ElapsedMilliseconds}ms result={response.Path.Result}");
        Assert.True(sw.Elapsed.TotalSeconds < 15, $"long-route response took {sw.Elapsed.TotalSeconds:F1}s; expected <15s");
    }

    [SkippableFact]
    public async Task DockerPathfinding_FirstQueryWarmStartup_RespondsQuickly()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("WWOW_RUN_DOCKER_VALIDATION") == "1", "WWOW_RUN_DOCKER_VALIDATION not set");

        // After PRELOAD_COMPLETE the .scene file should already be loaded.
        // A fresh path query should return in <1s for a short route on a
        // warmed-up service. Pre-fix (no .scene file) the cold first call
        // was ~30s while VMAP init ran.
        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = -8949.95f, Y = -132.493f, Z = 83.531f },
                End = new Game.Position { X = -8939.79f, Y = -114.61f, Z = 82.69f },
                Straight = false,
            }
        };

        var sw = Stopwatch.StartNew();
        var response = await SendRequestAsync(request);
        sw.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Console.Error.WriteLine($"[DOCKER-VALIDATE] first-query-warm map=0 corners={response.Path.Corners.Count} elapsed={sw.ElapsedMilliseconds}ms result={response.Path.Result}");
        Assert.True(sw.Elapsed.TotalSeconds < 5, $"first warm query took {sw.Elapsed.TotalSeconds:F1}s; expected <5s with .scene loaded");
    }

    private static async Task<PathfindingResponse> SendRequestAsync(PathfindingRequest request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(DockerHost, DockerPort);
        using var stream = client.GetStream();

        // Wire format matches ProtobufSocketClient: ProtobufCompression.Encode
        // produces a single byte array containing [4-byte length][payload].
        byte[] encoded = ProtobufCompression.Encode(request.ToByteArray());
        await stream.WriteAsync(encoded);
        await stream.FlushAsync();

        var responseLengthBytes = new byte[4];
        await ReadExactAsync(stream, responseLengthBytes, 4);
        var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);

        var wirePayload = new byte[responseLength];
        await ReadExactAsync(stream, wirePayload, responseLength);
        byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);

        return PathfindingResponse.Parser.ParseFrom(protobufBytes);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset));
            if (read == 0) throw new IOException("Stream closed before all bytes read");
            offset += read;
        }
    }
}
