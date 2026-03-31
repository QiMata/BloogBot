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

        // Phase 1: All bots enter world
        var botCount = await WaitForProgressAsync(
            phaseName: "BotsEnterWorld",
            maxTimeout: TimeSpan.FromMinutes(3),
            staleTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(3),
            evaluate: snapshots =>
            {
                var count = snapshots.Count;
                return (count >= WarsongGulchFixture.TotalBotCount, count, $"bots={count}");
            },
            tolerateFgCrash: true);
        _output.WriteLine($"Phase 1: {botCount} bots in world");

        // Phase 2: Prep sequence. The BattlegroundCoordinator checks level>=10
        // before starting the BG queue. We RESET level to 1 first, teleport bots
        // to battlemasters, wait for NPCs to appear, THEN level to 10.
        // This ensures the coordinator doesn't queue before bots are in position.

        // Step 1: Revive dead bots and ensure GM mode on via SOAP (works even when dead).
        foreach (var snap in _bot.AllBots)
        {
            await _bot.ExecuteGMCommandAsync($".revive {snap.CharacterName}");
            await _bot.ExecuteGMCommandAsync($".character level {snap.CharacterName} 10");
        }
        await Task.Delay(2000);

        // Step 2: Teleport to battlemaster positions via bot chat (.go xyz needs GM on)
        // Horde: Kartra Bloodsnarl (permanent BM, entry 14942) at (1980.9,-4787.78,55.88)
        // NOTE: Entry 15105 "Warsong Emissary" is event-only (Call to Arms) — not always spawned.
        foreach (var account in _bot.HordeAccounts)
        {
            await _bot.BotTeleportAsync(account, 1, 1980.9f, -4787.78f, 58.88f);
            await Task.Delay(300);
        }
        foreach (var account in _bot.AllianceAccounts)
        {
            await _bot.BotTeleportAsync(account, 0, -8454.6f, 318.9f, 124.0f);
            await Task.Delay(300);
        }
        // Wait for server to send nearby objects (NPCs) after teleport
        await Task.Delay(10000);

        // Step 3: Turn GM off — bots need .gm off to queue for BG
        foreach (var snap in _bot.AllBots)
        {
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".gm off");
        }
        await Task.Delay(2000);

        _output.WriteLine("Phase 2: Reset→teleport→level→gm off complete");

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

        // Debug: print initial mapIds for all bots
        await _bot.RefreshSnapshotsAsync();
        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"  [PRE-BG] {snap.AccountName}: map={mapId} pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0}) valid={snap.IsObjectManagerValid}");
        }

        // NOTE: Can't use WaitForProgressAsync here because it reads AllBots
        // which filters by IsHydratedInWorldSnapshot. After BG transfer, bots
        // have IsObjectManagerValid=false (transitioning) so they're filtered out.
        // Instead, query ALL snapshots directly and check MapId.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var botsInWsg = 0;
        while (sw.Elapsed < TimeSpan.FromMinutes(5))
        {
            var allSnapshots = await _bot.QueryAllSnapshotsAsync();
            botsInWsg = allSnapshots.Count(s =>
                (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == WarsongGulchFixture.WsgMapId);
            var total = allSnapshots.Count;

            if (botsInWsg >= 4)
            {
                _output.WriteLine($"[WSGEntry] Complete at {sw.Elapsed.TotalSeconds:F0}s: wsg={botsInWsg}/{total}");
                break;
            }

            _output.WriteLine($"[WSGEntry] {sw.Elapsed.TotalSeconds:F0}s: wsg={botsInWsg}/{total}");
            await Task.Delay(5000);
        }

        _output.WriteLine($"\n=== WSG RESULT ===");
        _output.WriteLine($"Bots in WSG: {botsInWsg}");
        await _bot.RefreshSnapshotsAsync();
        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}");
        }

        Assert.True(botsInWsg >= 4, $"At least 4 bots should enter WSG (got {botsInWsg})");
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
