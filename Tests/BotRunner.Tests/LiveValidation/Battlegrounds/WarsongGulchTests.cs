using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Warsong Gulch 20-bot battleground integration test.
///
/// Launches 20 bots: 10 Horde (1 FG + 9 BG) + 10 Alliance (10 BG).
/// Both sides form raid, queue for WSG at their faction's battlemaster,
/// accept invite, and enter WSG (mapId=489).
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~WarsongGulchTests" --configuration Release -v n --blame-hang --blame-hang-timeout 30m
/// </summary>
[Collection(WarsongGulchCollection.Name)]
public class WarsongGulchTests
{
    private readonly WarsongGulchFixture _bot;
    private readonly ITestOutputHelper _output;

    public WarsongGulchTests(WarsongGulchFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Phase 1: All 20 bots enter world.
    /// </summary>
    [SkippableFact]
    public async Task WSG_AllBotsEnterWorld()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");

        await WaitForProgressAsync(
            phaseName: "BotsEnterWorld",
            maxTimeout: TimeSpan.FromMinutes(3),
            staleTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(3),
            evaluate: snapshots =>
            {
                var count = snapshots.Count;
                return (count >= WarsongGulchFixture.TotalBotCount, count, $"bots={count}");
            });

        await _bot.RefreshSnapshotsAsync();
        _output.WriteLine($"All {_bot.AllBots.Count} bots entered world");
        Assert.True(_bot.AllBots.Count >= WarsongGulchFixture.TotalBotCount,
            $"Expected {WarsongGulchFixture.TotalBotCount} bots, got {_bot.AllBots.Count}");
    }

    /// <summary>
    /// Phase 2: Both factions form raid groups and queue for WSG.
    /// Validates: groups formed, BG queue initiated, bots enter WSG map 489.
    /// </summary>
    [SkippableFact]
    public async Task WSG_QueueAndEnterBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");

        // Wait for bots to enter world first
        await WaitForProgressAsync(
            phaseName: "BotsEnterWorld",
            maxTimeout: TimeSpan.FromMinutes(3),
            staleTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(3),
            evaluate: snapshots =>
            {
                var count = snapshots.Count;
                return (count >= WarsongGulchFixture.TotalBotCount, count, $"bots={count}");
            });

        // Wait for coordinator to form groups, queue BG, and enter WSG
        var botsOnWsg = await WaitForProgressAsync(
            phaseName: "WSGEntry",
            maxTimeout: TimeSpan.FromMinutes(10),
            staleTimeout: TimeSpan.FromSeconds(90),
            pollInterval: TimeSpan.FromSeconds(5),
            evaluate: snapshots =>
            {
                var grouped = snapshots.Count(s => s.PartyLeaderGuid != 0);
                var onWsg = snapshots.Count(s => (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == WarsongGulchFixture.WsgMapId);

                var posHash = string.Join("|", snapshots.Select(s =>
                {
                    var p = s.Player?.Unit?.GameObject?.Base?.Position;
                    var m = s.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                    return $"{m}:{p?.X:F0},{p?.Y:F0}";
                }));

                var fingerprint = $"grp={grouped},wsg={onWsg},pos={posHash.GetHashCode():X8}";
                return (onWsg >= 10, onWsg, fingerprint);  // Need at least 10 (5v5 minimum) for BG to start
            });

        await _bot.RefreshSnapshotsAsync();
        _output.WriteLine($"\n=== WSG ENTRY SUMMARY ===");
        _output.WriteLine($"Bots on WSG map: {botsOnWsg}");

        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}, pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        Assert.True(botsOnWsg >= 10, $"At least 10 bots must enter WSG (got {botsOnWsg})");
    }

    private async Task<TResult> WaitForProgressAsync<TResult>(
        string phaseName,
        TimeSpan maxTimeout,
        TimeSpan staleTimeout,
        TimeSpan pollInterval,
        Func<IReadOnlyList<WoWActivitySnapshot>, (bool done, TResult result, string fingerprint)> evaluate)
    {
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastFingerprintChange = sw.Elapsed;
        TResult lastResult = default!;

        while (sw.Elapsed < maxTimeout)
        {
            if (_bot.ClientCrashed)
            {
                Assert.Fail($"[{phaseName}] CRASHED — {_bot.CrashMessage ?? "process exited"}");
            }

            await _bot.RefreshSnapshotsAsync();
            var (done, result, fingerprint) = evaluate(_bot.AllBots);
            lastResult = result;

            if (done)
            {
                _output.WriteLine($"[{phaseName}] Complete at {sw.Elapsed.TotalSeconds:F0}s");
                return result;
            }

            if (fingerprint != lastFingerprint)
            {
                _output.WriteLine($"[{phaseName}] Progress at {sw.Elapsed.TotalSeconds:F0}s: {fingerprint}");
                lastFingerprint = fingerprint;
                lastFingerprintChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastFingerprintChange > staleTimeout)
                Assert.Fail($"[{phaseName}] STALE — no progress for {(sw.Elapsed - lastFingerprintChange).TotalSeconds:F0}s");

            await Task.Delay(pollInterval);
        }

        Assert.Fail($"[{phaseName}] TIMEOUT — {maxTimeout.TotalSeconds:F0}s elapsed");
        return lastResult;
    }
}
