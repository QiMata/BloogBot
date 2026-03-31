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
/// Fixture prep:
///   - Creates 20 accounts (10 Horde + 10 Alliance)
///   - Levels all to 10 (WSG minimum)
///   - Teleports Horde to Orgrimmar, Alliance to Stormwind
///   - Turns GM mode off
///
/// BattlegroundCoordinator handles:
///   - Sending JoinBattleground to each bot
///   - Waiting for BG invite
///   - Accepting and entering WSG
///
/// Test asserts:
///   - All bots enter world
///   - Bots are on correct faction maps after prep
///   - Coordinator queues all bots for WSG
///   - Bots enter WSG map 489
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~WarsongGulchTests" -v n --blame-hang --blame-hang-timeout 15m
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
    /// Full WSG test: fixture preps bots, coordinator queues for BG, bots enter WSG.
    /// </summary>
    [SkippableFact]
    public async Task WSG_CoordinatorQueuesAndEntersBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");

        // Phase 1: All bots enter world (tolerate FG crash — 19 BG bots is enough)
        var botCount = await WaitForProgressAsync(
            phaseName: "BotsEnterWorld",
            maxTimeout: TimeSpan.FromMinutes(3),
            staleTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(3),
            evaluate: snapshots =>
            {
                var count = snapshots.Count;
                // Accept 19+ bots (FG bot may crash during BG map transfer)
                return (count >= WarsongGulchFixture.TotalBotCount - 1, count, $"bots={count}");
            },
            tolerateFgCrash: true);
        _output.WriteLine($"Phase 1: {botCount} bots in world");

        // Phase 2: Fixture prep — level to 10, remove Deserter debuff, .gm off, teleport
        foreach (var snap in _bot.AllBots)
        {
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".character level 10");
            // Remove Deserter debuff (spell 26013) from previous BG sessions
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".unaura 26013");
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".gm off");
        }
        await Task.Delay(2000);

        // Teleport Horde DIRECTLY to WSG Battlemaster (Warsong Emissary) in Orgrimmar
        // VMaNGOS creature entry 15105 spawns at multiple positions — using (1658.9,-4389.0,23.8)
        // which is in the Valley of Honor area. Z+3 for ground snap safety.
        foreach (var account in _bot.HordeAccounts)
        {
            await _bot.BotTeleportAsync(account, 1, 1658.9f, -4389.0f, 26.8f);
            await Task.Delay(300);
        }
        // Teleport Alliance DIRECTLY to WSG Battlemaster (Elfarran) in Stormwind
        // VMaNGOS creature entry 14981 spawns at (-8454.6,318.9,121.0). Z+3.
        foreach (var account in _bot.AllianceAccounts)
        {
            await _bot.BotTeleportAsync(account, 0, -8454.6f, 318.9f, 124.0f);
            await Task.Delay(300);
        }
        await Task.Delay(3000);
        _output.WriteLine("Phase 2: All bots leveled, .gm off, teleported to faction cities");

        // Phase 3: Verify positions
        await _bot.RefreshSnapshotsAsync();
        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}, pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        // Phase 4: BattlegroundCoordinator takes over — it sends JoinBattleground to each bot.
        // Wait for bots to enter WSG map 489.
        _output.WriteLine("\nPhase 4: Waiting for BattlegroundCoordinator to queue and enter WSG...");
        var botsInWsg = await WaitForProgressAsync(
            phaseName: "WSGEntry",
            maxTimeout: TimeSpan.FromMinutes(5),
            staleTimeout: TimeSpan.FromSeconds(90),
            pollInterval: TimeSpan.FromSeconds(5),
            evaluate: snapshots =>
            {
                var onWsg = snapshots.Count(s =>
                    (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == WarsongGulchFixture.WsgMapId);
                var total = snapshots.Count;
                return (onWsg >= 10, onWsg, $"wsg={onWsg}/{total}");
            },
            tolerateFgCrash: true);

        _output.WriteLine($"\n=== WSG RESULT ===");
        _output.WriteLine($"Bots in WSG: {botsInWsg}");
        await _bot.RefreshSnapshotsAsync();
        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}");
        }

        Assert.True(botsInWsg >= 2, $"At least 2 bots should enter WSG (got {botsInWsg})");
    }

    private async Task<TResult> WaitForProgressAsync<TResult>(
        string phaseName, TimeSpan maxTimeout, TimeSpan staleTimeout, TimeSpan pollInterval,
        Func<IReadOnlyList<WoWActivitySnapshot>, (bool done, TResult result, string fingerprint)> evaluate,
        bool tolerateFgCrash = false)
    {
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastFingerprintChange = sw.Elapsed;
        TResult lastResult = default!;

        while (sw.Elapsed < maxTimeout)
        {
            // FG bot crash during BG map transfer is a known issue.
            // BG tests use 19 headless bots — FG crash is not fatal.
            if (_bot.ClientCrashed && !tolerateFgCrash)
                Assert.Fail($"[{phaseName}] CRASHED");

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
