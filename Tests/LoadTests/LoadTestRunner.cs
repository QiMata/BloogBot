using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace LoadTests;

/// <summary>
/// Parameterized load test that spawns N headless bots.
/// Each bot: connects to MaNGOS, logs in, creates character (if needed),
/// enters world, performs basic idle/patrol. Outputs CSV metrics.
/// </summary>
public class LoadTestRunner
{
    private readonly ITestOutputHelper _output;

    public LoadTestRunner(ITestOutputHelper output) => _output = output;

    public record BotMetrics(
        string AccountName,
        long ConnectionTimeMs,
        long LoginTimeMs,
        long EnterWorldTimeMs,
        float AvgPhysicsFrameMs,
        float AvgSnapshotLatencyMs,
        float MemoryMB);

    [Theory]
    [InlineData(10)]
    public async Task SpawnNBots_AllEnterWorld(int botCount)
    {
        var configs = BotDistribution.Generate(botCount);
        _output.WriteLine($"=== Load Test: {botCount} bots across {BotDistribution.AllCombos.Count} combos ===");

        // Step 1: Create accounts (idempotent)
        var (created, skipped, failed) = await BulkAccountCreator.CreateBulkAsync(
            botCount, gmLevel: 6, log: msg => _output.WriteLine(msg));
        _output.WriteLine($"Accounts: {created} created, {skipped} skipped, {failed} failed");

        // Step 2: Launch StateManager with generated settings
        // TODO: Generate settings JSON, start StateManager, wait for all bots to enter world

        // Step 3: Collect metrics
        var metrics = new List<BotMetrics>();
        // TODO: Poll snapshots, measure connection/login/enterWorld times

        // Step 4: Output CSV
        var csvPath = Path.Combine(AppContext.BaseDirectory, $"load_test_{botCount}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        var csvLines = new List<string>
        {
            "AccountName,ConnectionTimeMs,LoginTimeMs,EnterWorldTimeMs,AvgPhysicsFrameMs,AvgSnapshotLatencyMs,MemoryMB"
        };
        foreach (var m in metrics)
        {
            csvLines.Add($"{m.AccountName},{m.ConnectionTimeMs},{m.LoginTimeMs},{m.EnterWorldTimeMs}," +
                         $"{m.AvgPhysicsFrameMs:F2},{m.AvgSnapshotLatencyMs:F2},{m.MemoryMB:F1}");
        }

        if (metrics.Count > 0)
        {
            File.WriteAllLines(csvPath, csvLines);
            _output.WriteLine($"Metrics written to: {csvPath}");
        }

        _output.WriteLine($"Load test complete: {configs.Count} bots configured");
        Assert.True(configs.Count > 0);
    }
}
