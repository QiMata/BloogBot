using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Dungeons;

/// <summary>
/// Wailing Caverns 10-bot dungeoneering integration test.
///
/// Launches 10 bots (1 FG + 9 BG) via DungeonInstanceFixture.
/// Tests: group formation, travel to WC entrance (Barrens), meeting stone summoning,
/// instance entry (mapId=43), and basic dungeon progress.
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~WailingCavernsTests" --configuration Release -v n --blame-hang --blame-hang-timeout 20m
/// </summary>
[Collection(WailingCavernsCollection.Name)]
public class WailingCavernsTests
{
    private readonly WailingCavernsFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint WcMapId = 43;
    private const int ExpectedBotCount = 10;

    public WailingCavernsTests(WailingCavernsFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Full coordinator-driven dungeon entry.
    /// Validates: all bots enter world, group forms, bots travel to WC entrance,
    /// at least 2 bots transition to instance map 43.
    /// </summary>
    [SkippableFact]
    public async Task WC_GroupFormAndEnterDungeon()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        // Phase 1: Bots enter world
        await WaitForProgressAsync(
            phaseName: "BotsEnterWorld",
            maxTimeout: TimeSpan.FromMinutes(2),
            staleTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(3),
            evaluate: snapshots =>
            {
                var count = snapshots.Count;
                return (count == ExpectedBotCount, count, $"bots={count}");
            });

        Assert.Equal(ExpectedBotCount, _bot.AllBots.Count);

        // Phase 2: Coordinator pipeline — group, travel, enter dungeon
        var botsOnWcMap = await WaitForProgressAsync(
            phaseName: "CoordinatorPrep",
            maxTimeout: TimeSpan.FromMinutes(8),
            staleTimeout: TimeSpan.FromSeconds(60),
            pollInterval: TimeSpan.FromSeconds(5),
            evaluate: snapshots =>
            {
                var grouped = snapshots.Count(s => s.PartyLeaderGuid != 0);
                var onWc = snapshots.Count(s => (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == WcMapId);
                var totalSpells = snapshots.Sum(s => s.Player?.SpellList?.Count ?? 0);

                var posHash = string.Join("|", snapshots.Select(s =>
                {
                    var p = s.Player?.Unit?.GameObject?.Base?.Position;
                    var m = s.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                    return $"{m}:{p?.X:F0},{p?.Y:F0}";
                }));

                var fingerprint = $"grp={grouped},wc={onWc},spells={totalSpells},pos={posHash.GetHashCode():X8}";
                return (onWc >= 2, onWc, fingerprint);
            });

        Assert.True(botsOnWcMap >= 2, $"At least 2 bots must enter WC instance map (got {botsOnWcMap})");

        // Phase 3: Verify group formation
        await _bot.RefreshSnapshotsAsync();
        var finalBots = _bot.AllBots;
        var groupedBots = finalBots.Where(s => s.PartyLeaderGuid != 0).ToList();

        _output.WriteLine($"\n=== WC DUNGEON ENTRY SUMMARY ===");
        _output.WriteLine($"Total bots: {finalBots.Count}");
        _output.WriteLine($"Grouped: {groupedBots.Count}");
        _output.WriteLine($"On WC map: {botsOnWcMap}");

        foreach (var snap in finalBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            var hp = snap.Player?.Unit?.Health ?? 0;
            var maxHp = snap.Player?.Unit?.MaxHealth ?? 1;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}, HP={hp * 100 / maxHp}%, pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        Assert.Equal(ExpectedBotCount, finalBots.Count);
        Assert.Equal(ExpectedBotCount, groupedBots.Count);
    }

    /// <summary>
    /// Snapshot-based progress poller. Same pattern as RagefireChasmTests.
    /// </summary>
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
                var msg = $"[{phaseName}] CRASHED — {_bot.CrashMessage ?? "child process exited unexpectedly"}.";
                _output.WriteLine(msg);
                Assert.Fail(msg);
            }

            await _bot.RefreshSnapshotsAsync();
            var snapshots = _bot.AllBots;
            var (done, result, fingerprint) = evaluate(snapshots);
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
            {
                var msg = $"[{phaseName}] STALE — no progress for {(sw.Elapsed - lastFingerprintChange).TotalSeconds:F0}s. Last: {lastFingerprint}";
                _output.WriteLine(msg);
                Assert.Fail(msg);
            }

            await Task.Delay(pollInterval);
        }

        Assert.Fail($"[{phaseName}] TIMEOUT — {maxTimeout.TotalSeconds:F0}s elapsed. Last: {lastFingerprint}");
        return lastResult;
    }
}
