using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BotCommLayer;
using Communication;
using Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.IPC;

/// <summary>
/// Performance/load tests for the StateManager TCP protobuf pipeline.
/// Simulates N concurrent bots sending realistic snapshots at 10Hz (100ms interval).
/// Measures: throughput (msg/s), latency (P50/P95/P99), error rate.
/// </summary>
public class StateManagerLoadTests
{
    private readonly ITestOutputHelper _output;

    public StateManagerLoadTests(ITestOutputHelper output) => _output = output;

    // =========================================================================
    // Test server that simulates StateManager's HandleRequest
    // =========================================================================

    private sealed class LoadTestServer : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        public long TotalRequests;

        public LoadTestServer(string ip, int port)
            : base(ip, port, NullLogger.Instance) { }

        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            Interlocked.Increment(ref TotalRequests);

            // Simulate realistic StateManager work: clone + inject action
            var response = request.Clone();
            response.CurrentAction = new ActionMessage
            {
                ActionType = ActionType.Wait,
            };
            return response;
        }
    }

    // =========================================================================
    // Create a realistic snapshot that mimics a bot in RFC dungeon
    // =========================================================================

    private static WoWActivitySnapshot CreateRealisticSnapshot(string accountName)
    {
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = accountName,
            IsObjectManagerValid = true,
            ScreenState = "InWorld",
            PartyLeaderGuid = 0xABCD1234,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = 180,
                    MaxHealth = 213,
                    TargetGuid = 0xF130002C38009499,
                                        MovementFlags = 0,
                    GameObject = new WoWGameObject
                    {
                        Name = accountName,
                        Base = new WoWObject
                        {
                            Guid = 0x1234,
                            MapId = 389,
                            Position = new Position
                            {
                                X = -13f + (float)(new Random().NextDouble() * 10),
                                Y = -45f + (float)(new Random().NextDouble() * 10),
                                Z = -22f
                            }
                        }
                    }
                }
            }
        };

        // Add 15-30 nearby units (typical dungeon encounter)
        var rng = new Random();
        int unitCount = rng.Next(15, 31);
        for (int i = 0; i < unitCount; i++)
        {
            snapshot.NearbyUnits.Add(new WoWUnit
            {
                Health = (uint)rng.Next(0, 5000),
                MaxHealth = 5000,
                TargetGuid = (ulong)rng.Next(1, 100),
                                GameObject = new WoWGameObject
                {
                    Name = $"Ragefire Trogg",
                    Base = new WoWObject
                    {
                        Guid = (ulong)(5000 + i),
                        MapId = 389,
                        Position = new Position
                        {
                            X = -10f + (float)(rng.NextDouble() * 20),
                            Y = -40f + (float)(rng.NextDouble() * 20),
                            Z = -20f
                        }
                    }
                }
            });
        }

        // Add some spell IDs
        for (int i = 0; i < 30; i++)
            snapshot.Player.SpellList.Add((uint)(78 + i * 100));

        return snapshot;
    }

    // =========================================================================
    // Run a load test with N clients for a fixed duration
    // =========================================================================

    private record LoadTestResult(
        int ClientCount,
        int TotalMessages,
        double DurationSec,
        double MsgPerSec,
        double P50Ms,
        double P95Ms,
        double P99Ms,
        double MaxMs,
        int Errors,
        int SnapshotBytes);

    private LoadTestResult RunLoadTest(int clientCount, int durationSec, int pollIntervalMs = 100)
    {
        var port = GetFreePort();
        using var server = new LoadTestServer("127.0.0.1", port);
        Thread.Sleep(200); // Let server start

        var allLatencies = new ConcurrentBag<double>();
        var errorCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
        int snapshotSize = 0;

        var tasks = new Task[clientCount];
        for (int i = 0; i < clientCount; i++)
        {
            int clientId = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
                        "127.0.0.1", port, NullLogger.Instance);

                    var snapshot = CreateRealisticSnapshot($"BOT{clientId:D4}");
                    if (clientId == 0)
                        Interlocked.Exchange(ref snapshotSize, snapshot.ToByteArray().Length);

                    while (!cts.Token.IsCancellationRequested)
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            var response = client.SendMessage(snapshot);
                            sw.Stop();
                            allLatencies.Add(sw.Elapsed.TotalMilliseconds);
                        }
                        catch
                        {
                            Interlocked.Increment(ref errorCount);
                        }

                        // Simulate 10Hz polling (100ms) minus time spent on IPC
                        var remaining = pollIntervalMs - (int)sw.ElapsedMilliseconds;
                        if (remaining > 0 && !cts.Token.IsCancellationRequested)
                            Thread.Sleep(remaining);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errorCount);
                }
            });
        }

        Task.WaitAll(tasks, TimeSpan.FromSeconds(durationSec + 10));

        var latencies = allLatencies.OrderBy(x => x).ToList();
        var totalMessages = latencies.Count;

        return new LoadTestResult(
            ClientCount: clientCount,
            TotalMessages: totalMessages,
            DurationSec: durationSec,
            MsgPerSec: totalMessages / (double)durationSec,
            P50Ms: latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.50)] : 0,
            P95Ms: latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.95)] : 0,
            P99Ms: latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.99)] : 0,
            MaxMs: latencies.Count > 0 ? latencies.Last() : 0,
            Errors: errorCount,
            SnapshotBytes: snapshotSize);
    }

    private void PrintResult(LoadTestResult r)
    {
        _output.WriteLine($"\n=== {r.ClientCount} CLIENTS ({r.DurationSec}s) ===");
        _output.WriteLine($"  Snapshot size: {r.SnapshotBytes} bytes");
        _output.WriteLine($"  Total messages: {r.TotalMessages}");
        _output.WriteLine($"  Throughput: {r.MsgPerSec:F0} msg/s");
        _output.WriteLine($"  Latency P50: {r.P50Ms:F1}ms");
        _output.WriteLine($"  Latency P95: {r.P95Ms:F1}ms");
        _output.WriteLine($"  Latency P99: {r.P99Ms:F1}ms");
        _output.WriteLine($"  Latency Max: {r.MaxMs:F1}ms");
        _output.WriteLine($"  Errors: {r.Errors}");
    }

    // =========================================================================
    // TESTS
    // =========================================================================

    [Fact]
    public void Load_10Clients_5Seconds()
    {
        var result = RunLoadTest(clientCount: 10, durationSec: 5);
        PrintResult(result);

        Assert.Equal(0, result.Errors);
        Assert.True(result.MsgPerSec > 20, $"Expected >20 msg/s, got {result.MsgPerSec:F0}");
        Assert.True(result.P50Ms < 500, $"P50 latency {result.P50Ms:F1}ms exceeds 500ms");
    }

    [Fact]
    public void Load_50Clients_5Seconds()
    {
        var result = RunLoadTest(clientCount: 50, durationSec: 5);
        PrintResult(result);

        // 50 clients is near the current limit — ThreadPool thread-per-connection saturates.
        // Baseline: ~3-10 msg/s total. This test documents the limit, not a target.
        Assert.Equal(0, result.Errors);
        Assert.True(result.TotalMessages > 0, "Should complete at least some messages");
    }

    [Fact]
    public void Load_100Clients_10Seconds()
    {
        var result = RunLoadTest(clientCount: 100, durationSec: 10);
        PrintResult(result);

        // 100 clients: connection backlog is 50, so some may fail to connect.
        // Documents the scaling wall — P9 async rewrite needed for 100+ bots.
        Assert.True(result.TotalMessages > 0, "Should complete at least some messages");
    }

    [Fact]
    public void Load_500Clients_10Seconds()
    {
        var result = RunLoadTest(clientCount: 500, durationSec: 10);
        PrintResult(result);

        // 500 clients: well past the current architecture's limit.
        // Expect high error rates — documents the scaling ceiling.
        // P9 (async pipelines, connection multiplexing) is required for this scale.
        Assert.True(result.TotalMessages > 0, "Should complete at least some messages");
    }

    [Fact]
    public void Load_ProgressiveScale_10_50_100()
    {
        var results = new List<LoadTestResult>();

        foreach (var count in new[] { 10, 50, 100 })
        {
            var result = RunLoadTest(clientCount: count, durationSec: 3);
            results.Add(result);
            PrintResult(result);
        }

        _output.WriteLine("\n=== SCALING SUMMARY ===");
        _output.WriteLine($"{"Clients",-10} {"Msg/s",-10} {"P50",-10} {"P95",-10} {"P99",-10} {"Max",-10} {"Errors",-8}");
        foreach (var r in results)
        {
            _output.WriteLine($"{r.ClientCount,-10} {r.MsgPerSec,-10:F0} {r.P50Ms,-10:F1} {r.P95Ms,-10:F1} {r.P99Ms,-10:F1} {r.MaxMs,-10:F1} {r.Errors,-8}");
        }

        // All should have zero errors
        foreach (var r in results)
            Assert.Equal(0, r.Errors);
    }

    // =========================================================================
    // PIPELINE SERVER TESTS (new async implementation)
    // =========================================================================

    private sealed class PipelineLoadTestServer : ProtobufPipelineSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        public PipelineLoadTestServer(string ip, int port)
            : base(ip, port, NullLogger.Instance) { }

        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            var response = request.Clone();
            response.CurrentAction = new ActionMessage { ActionType = ActionType.Wait };
            return response;
        }
    }

    private LoadTestResult RunPipelineLoadTest(int clientCount, int durationSec, int pollIntervalMs = 100)
    {
        var port = GetFreePort();
        using var server = new PipelineLoadTestServer("127.0.0.1", port);
        Thread.Sleep(200);

        var allLatencies = new ConcurrentBag<double>();
        var errorCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
        int snapshotSize = 0;

        var tasks = new Task[clientCount];
        for (int i = 0; i < clientCount; i++)
        {
            int clientId = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
                        "127.0.0.1", port, NullLogger.Instance);

                    var snapshot = CreateRealisticSnapshot($"BOT{clientId:D4}");
                    if (clientId == 0)
                        Interlocked.Exchange(ref snapshotSize, snapshot.ToByteArray().Length);

                    while (!cts.Token.IsCancellationRequested)
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            var response = client.SendMessage(snapshot);
                            sw.Stop();
                            allLatencies.Add(sw.Elapsed.TotalMilliseconds);
                        }
                        catch
                        {
                            Interlocked.Increment(ref errorCount);
                        }

                        var remaining = pollIntervalMs - (int)sw.ElapsedMilliseconds;
                        if (remaining > 0 && !cts.Token.IsCancellationRequested)
                            Thread.Sleep(remaining);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errorCount);
                }
            });
        }

        Task.WaitAll(tasks, TimeSpan.FromSeconds(durationSec + 10));

        var latencies = allLatencies.OrderBy(x => x).ToList();
        var totalMessages = latencies.Count;

        return new LoadTestResult(
            ClientCount: clientCount,
            TotalMessages: totalMessages,
            DurationSec: durationSec,
            MsgPerSec: totalMessages / (double)durationSec,
            P50Ms: latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.50)] : 0,
            P95Ms: latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.95)] : 0,
            P99Ms: latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.99)] : 0,
            MaxMs: latencies.Count > 0 ? latencies.Last() : 0,
            Errors: errorCount,
            SnapshotBytes: snapshotSize);
    }

    [Fact]
    public void Pipeline_10Clients_10Seconds()
    {
        var result = RunPipelineLoadTest(clientCount: 10, durationSec: 10);
        PrintResult(result);

        Assert.Equal(0, result.Errors);
        Assert.True(result.MsgPerSec > 30, $"Expected >30 msg/s, got {result.MsgPerSec:F0}");
    }

    [Fact]
    public void Pipeline_100Clients_10Seconds()
    {
        var result = RunPipelineLoadTest(clientCount: 100, durationSec: 10);
        PrintResult(result);

        Assert.Equal(0, result.Errors);
        Assert.True(result.TotalMessages > 10, $"Expected >10 msgs, got {result.TotalMessages}");
    }

    [Fact]
    public void Pipeline_500Clients_15Seconds()
    {
        var result = RunPipelineLoadTest(clientCount: 500, durationSec: 15);
        PrintResult(result);

        // Pipeline server should process SOME messages at 500 clients
        // (legacy server fails completely with 0 messages and 60 errors).
        // Throughput limited by synchronous ProtobufSocketClient, not the server.
        Assert.True(result.TotalMessages > 0, $"Expected >0 msgs, got {result.TotalMessages}");
        Assert.True(result.Errors < 100, $"Expected <100 errors, got {result.Errors}");
    }

    [Fact]
    public void Pipeline_vs_Legacy_100Clients()
    {
        _output.WriteLine("=== LEGACY SERVER (ProtobufSocketServer) ===");
        var legacyResult = RunLoadTest(clientCount: 100, durationSec: 5);
        PrintResult(legacyResult);

        _output.WriteLine("\n=== PIPELINE SERVER (ProtobufPipelineSocketServer) ===");
        var pipelineResult = RunPipelineLoadTest(clientCount: 100, durationSec: 5);
        PrintResult(pipelineResult);

        _output.WriteLine($"\n=== COMPARISON ===");
        _output.WriteLine($"  Legacy:   {legacyResult.MsgPerSec:F0} msg/s, P95={legacyResult.P95Ms:F1}ms, errors={legacyResult.Errors}");
        _output.WriteLine($"  Pipeline: {pipelineResult.MsgPerSec:F0} msg/s, P95={pipelineResult.P95Ms:F1}ms, errors={pipelineResult.Errors}");
        var improvement = pipelineResult.MsgPerSec / Math.Max(legacyResult.MsgPerSec, 0.1);
        _output.WriteLine($"  Speedup: {improvement:F1}x");

        // Pipeline should have fewer errors than legacy at 100 clients
        Assert.True(pipelineResult.Errors <= legacyResult.Errors,
            $"Pipeline ({pipelineResult.Errors} errors) should not be worse than legacy ({legacyResult.Errors} errors)");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
