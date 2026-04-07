using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotCommLayer;
using Microsoft.Extensions.Logging.Abstractions;
using SceneData;
using WoWSharpClient.Movement;
using Xunit;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Performance tests for the tile-based SceneDataService.
/// Simulates N concurrent bots teleporting around the world,
/// each requesting 3x3 tile neighborhoods from the service.
///
/// Requires SceneDataService running on port 5003 (Docker).
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~SceneDataTileLoadTests" --configuration Release -v n
/// </summary>
public sealed class SceneDataTileLoadTests
{
    private readonly ITestOutputHelper _output;
    private const string ServiceIp = "127.0.0.1";
    private const int ServicePort = 5003;
    private const float TileSize = 533.33333f;

    // Known locations across Kalimdor (map 1) and Eastern Kingdoms (map 0)
    private static readonly (uint MapId, float X, float Y, string Name)[] Locations =
    {
        (1, 1629f, -4373f, "Orgrimmar"),
        (1, -259f, -4350f, "Valley of Trials"),
        (1, -442f, -2595f, "Crossroads"),
        (1, -1021f, -3544f, "Ratchet"),
        (1, 1337f, -4633f, "Durotar Coast"),
        (0, -8920f, -130f, "Elwynn Forest"),
        (0, -4917f, -940f, "Westfall"),
        (1, 295f, -4828f, "Razor Hill"),
        (1, -854f, -4920f, "Sen'jin Village"),
        (1, 1843f, -4419f, "Org Valley of Honor"),
    };

    public SceneDataTileLoadTests(ITestOutputHelper output) => _output = output;

    private static bool IsServiceAvailable()
    {
        try
        {
            using var client = new ProtobufSocketClient<SceneTileRequest, SceneTileResponse>(
                ServiceIp, ServicePort, NullLogger.Instance);
            var response = client.SendMessage(
                new SceneTileRequest { MapId = 1, TileX = 29, TileY = 41 });
            return response.Success;
        }
        catch { return false; }
    }

    /// <summary>
    /// Baseline: single bot requests 3x3 neighborhood at one location.
    /// Measures latency per tile request and total neighborhood load time.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void SingleBot_3x3Neighborhood_MeasureLatency()
    {
        Skip.IfNot(IsServiceAvailable(), "SceneDataService not running on port 5003");

        var (mapId, x, y, name) = Locations[0]; // Orgrimmar
        var (centerTX, centerTY) = SceneDataClient.WorldToTile(x, y);
        var sw = Stopwatch.StartNew();
        int totalTriangles = 0;
        var tileTimes = new List<(uint TX, uint TY, long Ms, int Tris)>();

        using var client = new ProtobufSocketClient<SceneTileRequest, SceneTileResponse>(
            ServiceIp, ServicePort, NullLogger.Instance);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                uint tx = (uint)((int)centerTX + dx);
                uint ty = (uint)((int)centerTY + dy);
                var tileSw = Stopwatch.StartNew();

                var response = client.SendMessage(
                    new SceneTileRequest { MapId = mapId, TileX = tx, TileY = ty });

                tileSw.Stop();
                int tris = (int)response.TriangleCount;
                totalTriangles += tris;
                tileTimes.Add((tx, ty, tileSw.ElapsedMilliseconds, tris));
            }
        }

        sw.Stop();
        _output.WriteLine($"=== Single Bot 3x3 at {name} ({centerTX},{centerTY}) ===");
        foreach (var (tx, ty, ms, tris) in tileTimes)
            _output.WriteLine($"  Tile ({tx},{ty}): {ms}ms, {tris} triangles");

        _output.WriteLine($"\n  Total: {sw.ElapsedMilliseconds}ms, {totalTriangles} triangles");
        _output.WriteLine($"  Avg per tile: {tileTimes.Average(t => t.Ms):F0}ms");
        _output.WriteLine($"  Max per tile: {tileTimes.Max(t => t.Ms)}ms");
    }

    /// <summary>
    /// Simulate N bots teleporting simultaneously to different locations.
    /// Each bot requests its 3x3 neighborhood concurrently.
    /// </summary>
    [SkippableTheory]
    [Trait("Category", "RequiresInfrastructure")]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void ConcurrentBots_TeleportToLocations_MeasureThroughput(int botCount)
    {
        Skip.IfNot(IsServiceAvailable(), "SceneDataService not running on port 5003");

        var results = new ConcurrentBag<(int BotId, string Location, long TotalMs, int Tiles, int Triangles)>();
        var errors = new ConcurrentBag<(int BotId, string Error)>();
        var allReady = new ManualResetEventSlim(false);

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, botCount).Select(botId => Task.Run(() =>
        {
            allReady.Wait(); // Synchronized start

            var loc = Locations[botId % Locations.Length];
            var (centerTX, centerTY) = SceneDataClient.WorldToTile(loc.X, loc.Y);

            try
            {
                using var client = new ProtobufSocketClient<SceneTileRequest, SceneTileResponse>(
                    ServiceIp, ServicePort, NullLogger.Instance);

                var botSw = Stopwatch.StartNew();
                int tiles = 0, triangles = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        uint tx = (uint)((int)centerTX + dx);
                        uint ty = (uint)((int)centerTY + dy);

                        var response = client.SendMessage(
                            new SceneTileRequest { MapId = loc.MapId, TileX = tx, TileY = ty });

                        tiles++;
                        triangles += (int)response.TriangleCount;
                    }
                }

                botSw.Stop();
                results.Add((botId, loc.Name, botSw.ElapsedMilliseconds, tiles, triangles));
            }
            catch (Exception ex)
            {
                errors.Add((botId, ex.Message));
            }
        })).ToArray();

        // Release all bots simultaneously
        allReady.Set();
        Task.WaitAll(tasks);
        sw.Stop();

        // Report
        _output.WriteLine($"=== {botCount} Concurrent Bots — Tile Load Performance ===");
        _output.WriteLine($"  Wall clock: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Successful: {results.Count}/{botCount}");
        _output.WriteLine($"  Errors: {errors.Count}");

        if (results.Count > 0)
        {
            var times = results.Select(r => r.TotalMs).OrderBy(t => t).ToList();
            _output.WriteLine($"\n  Per-bot latency (3x3 neighborhood):");
            _output.WriteLine($"    Min:  {times.First()}ms");
            _output.WriteLine($"    P50:  {times[times.Count / 2]}ms");
            _output.WriteLine($"    P95:  {times[(int)(times.Count * 0.95)]}ms");
            _output.WriteLine($"    P99:  {times[Math.Min(times.Count - 1, (int)(times.Count * 0.99))]}ms");
            _output.WriteLine($"    Max:  {times.Last()}ms");
            _output.WriteLine($"    Avg:  {times.Average():F0}ms");

            int totalTiles = results.Sum(r => r.Tiles);
            int totalTris = results.Sum(r => r.Triangles);
            double tilesPerSec = totalTiles / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"\n  Throughput:");
            _output.WriteLine($"    Total tiles served: {totalTiles}");
            _output.WriteLine($"    Total triangles: {totalTris:N0}");
            _output.WriteLine($"    Tiles/sec: {tilesPerSec:F0}");
            _output.WriteLine($"    Effective bots/sec: {tilesPerSec / 9:F1} (9 tiles per bot)");
        }

        foreach (var (botId, error) in errors.Take(5))
            _output.WriteLine($"  [Bot {botId}] ERROR: {error}");

        // Assert: at least 90% of bots should succeed
        Assert.True(results.Count >= botCount * 0.9,
            $"Too many failures: {errors.Count}/{botCount}. First: {errors.FirstOrDefault().Error}");
    }

    /// <summary>
    /// Simulate bots teleporting in sequence — each bot moves to a new location,
    /// loading tiles for the new position. Measures sustained throughput.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void SustainedTeleport_10Bots_5Jumps_MeasureThroughput()
    {
        Skip.IfNot(IsServiceAvailable(), "SceneDataService not running on port 5003");

        const int botCount = 10;
        const int jumpsPerBot = 5;
        var results = new ConcurrentBag<(int BotId, int Jump, string Location, long Ms, int Tris)>();
        var errors = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, botCount).Select(botId => Task.Run(() =>
        {
            try
            {
                using var client = new ProtobufSocketClient<SceneTileRequest, SceneTileResponse>(
                    ServiceIp, ServicePort, NullLogger.Instance);

                for (int jump = 0; jump < jumpsPerBot; jump++)
                {
                    var loc = Locations[(botId + jump) % Locations.Length];
                    var (centerTX, centerTY) = SceneDataClient.WorldToTile(loc.X, loc.Y);
                    var jumpSw = Stopwatch.StartNew();
                    int triangles = 0;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            uint tx = (uint)((int)centerTX + dx);
                            uint ty = (uint)((int)centerTY + dy);

                            var response = client.SendMessage(
                                new SceneTileRequest { MapId = loc.MapId, TileX = tx, TileY = ty });

                            triangles += (int)response.TriangleCount;
                        }
                    }

                    jumpSw.Stop();
                    results.Add((botId, jump, loc.Name, jumpSw.ElapsedMilliseconds, triangles));
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Bot {botId}: {ex.Message}");
            }
        })).ToArray();

        Task.WaitAll(tasks);
        sw.Stop();

        _output.WriteLine($"=== Sustained Teleport: {botCount} bots x {jumpsPerBot} jumps ===");
        _output.WriteLine($"  Wall clock: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Total teleports: {results.Count}");
        _output.WriteLine($"  Errors: {errors.Count}");

        if (results.Count > 0)
        {
            var times = results.Select(r => r.Ms).OrderBy(t => t).ToList();
            _output.WriteLine($"\n  Per-teleport latency (3x3 load):");
            _output.WriteLine($"    Min: {times.First()}ms");
            _output.WriteLine($"    P50: {times[times.Count / 2]}ms");
            _output.WriteLine($"    P95: {times[(int)(times.Count * 0.95)]}ms");
            _output.WriteLine($"    Max: {times.Last()}ms");

            int totalTiles = results.Count * 9;
            double tilesPerSec = totalTiles / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"\n  Throughput:");
            _output.WriteLine($"    Tiles/sec: {tilesPerSec:F0}");
            _output.WriteLine($"    Teleports/sec: {tilesPerSec / 9:F1}");
            _output.WriteLine($"    Total triangles: {results.Sum(r => r.Tris):N0}");
        }

        foreach (var err in errors.Take(3))
            _output.WriteLine($"  ERROR: {err}");

        Assert.True(errors.Count == 0, $"Errors during sustained teleport: {errors.FirstOrDefault()}");
    }
}
