using BotCommLayer;
using GameData.Core.Models;
using Google.Protobuf;
using Pathfinding;
using System.Diagnostics;
using System.Net.Sockets;
using Xunit;

namespace PathfindingService.Tests;

// Validation harness that hits the live Docker wwow-pathfinding container
// on 127.0.0.1:9002 to confirm production behaviour. NOT auto-run — guard
// with WWOW_RUN_DOCKER_VALIDATION=1.
[Trait("Category", "DockerLive")]
public sealed class DockerLiveValidationTests
{
    private const string DockerHost = "127.0.0.1";
    private const int DockerPort = 9002;

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

    [SkippableTheory]
    [InlineData("tower_underpass", 1357.20f, -4516.20f, 32.00f)]
    [InlineData("bridge_side", 1337.20f, -4654.80f, 49.80f)]
    [InlineData("tower_base_live_vertical", 1342.40f, -4652.10f, 24.60f)]
    [InlineData("exterior_steep_incline", 1381.00f, -4380.90f, 26.00f)]
    public async Task DockerPathfinding_OgZepClosureRoute_TraversesOffMesh(
        string label, float startX, float startY, float startZ)
    {
        // PFS-OVERHAUL-006 loop-24 A5.5 + A5.6 + A5.7 close-out validation.
        // For each of the 4 routes whose CriticalWalkLegs tests closed
        // (19/4 -> 23/0), confirm the LIVE wwow-pathfinding docker
        // container produces a path that uses the deployed off-mesh
        // connection (single-corner-pair teleport hop), proving the new
        // off-mesh entries are live, not just bake-time artifacts.
        Skip.IfNot(Environment.GetEnvironmentVariable("WWOW_RUN_DOCKER_VALIDATION") == "1", "WWOW_RUN_DOCKER_VALIDATION not set");

        var request = new PathfindingRequest
        {
            Path = new CalculatePathRequest
            {
                MapId = 1,
                Start = new Game.Position { X = startX, Y = startY, Z = startZ },
                End = new Game.Position { X = 1320.14f, Y = -4653.16f, Z = 53.89f },
                Straight = false,
            }
        };

        var sw = Stopwatch.StartNew();
        var response = await SendRequestAsync(request);
        sw.Stop();

        Assert.Equal(PathfindingResponse.PayloadOneofCase.Path, response.PayloadCase);
        Assert.NotEmpty(response.Path.Corners);

        // Find the largest single-segment teleport jump in the result —
        // an off-mesh teleport produces a single ~20-25y horizontal hop
        // between consecutive corners that's far above the smooth-path's
        // typical 0.5y-2y step. If the path took the off-mesh shortcut,
        // we'll see a >= 15y jump somewhere.
        float maxSegmentJump = 0f;
        for (int i = 0; i < response.Path.Corners.Count - 1; i++)
        {
            var c0 = response.Path.Corners[i];
            var c1 = response.Path.Corners[i + 1];
            var dx = c1.X - c0.X;
            var dy = c1.Y - c0.Y;
            var horizontal = MathF.Sqrt(dx * dx + dy * dy);
            if (horizontal > maxSegmentJump) maxSegmentJump = horizontal;
        }

        Console.Error.WriteLine(
            $"[DOCKER-VALIDATE] og-zep-route label={label} corners={response.Path.Corners.Count} " +
            $"elapsed={sw.ElapsedMilliseconds}ms result={response.Path.Result} " +
            $"maxSegmentJump={maxSegmentJump:F1}y");

        Assert.True(sw.Elapsed.TotalSeconds < 10, $"{label} response took {sw.Elapsed.TotalSeconds:F1}s; expected <10s");
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
