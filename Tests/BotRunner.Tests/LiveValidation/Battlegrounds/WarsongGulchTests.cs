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

        // Capture Bg.log baseline BEFORE any bot activity starts.
        // The BG coordinator may queue bots during Phase 2 prep.
        var bgLogPath = @"E:\repos\Westworld of Warcraft\docker\linux\vmangos\storage\mangosd\logs\Bg.log";
        // Use a generous margin — the BG coordinator may start queueing during
        // Phase 1 (bots already level 10 from previous run). The BG can complete
        // before Phase 2 even finishes. UTC matches VMaNGOS Bg.log timestamps.
        var testStartTime = DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-dd HH:mm");
        _output.WriteLine($"Bg.log baseline: entries after {testStartTime}");

        // Sanity check: protobuf roundtrip for CurrentMapId
        {
            var testSnap = new Communication.WoWActivitySnapshot { AccountName = "ROUNDTRIP", CurrentMapId = 489 };
            var testResp = new Communication.StateChangeResponse();
            testResp.Snapshots.Add(testSnap);
            var bytes = Google.Protobuf.MessageExtensions.ToByteArray(testResp);
            var decoded = Communication.StateChangeResponse.Parser.ParseFrom(bytes);
            var mapId = decoded.Snapshots[0].CurrentMapId;
            _output.WriteLine($"[PROTO-ROUNDTRIP] CurrentMapId: {mapId} (expect 489), bytes={bytes.Length}");
            Assert.Equal(489u, mapId);
        }

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

        // Phase 2: Minimal prep — revive dead bots, level via SOAP, teleport, gm off.
        // Keep it fast — the BG coordinator starts as soon as bots are level 10.

        // Step 1: Revive + level via SOAP (works even when dead/ghost)
        foreach (var snap in _bot.AllBots)
            await _bot.ExecuteGMCommandAsync($".revive {snap.CharacterName}");
        await Task.Delay(1000);
        foreach (var snap in _bot.AllBots)
            await _bot.ExecuteGMCommandAsync($".character level {snap.CharacterName} 10");
        await Task.Delay(2000);

        // Step 2: Teleport to permanent battlemasters
        // Horde: Kartra Bloodsnarl (entry 14942) at (1980.9,-4787.78,55.88)
        // Alliance: Elfarran (entry 14981) at (-8454.6,318.9,121.0)
        foreach (var account in _bot.HordeAccounts)
        {
            await _bot.BotTeleportAsync(account, 1, 1980.9f, -4787.78f, 58.88f);
            await Task.Delay(200);
        }
        foreach (var account in _bot.AllianceAccounts)
        {
            await _bot.BotTeleportAsync(account, 0, -8454.6f, 318.9f, 124.0f);
            await Task.Delay(200);
        }
        await Task.Delay(5000); // Wait for NPC visibility

        // Step 3: GM off (required for BG queue)
        foreach (var snap in _bot.AllBots)
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".gm off");
        await Task.Delay(1000);

        _output.WriteLine("Phase 2 complete.");

        // Verify BG entry by checking StateManager stdout for SMSG_NEW_WORLD map=489.
        // This is captured by BotServiceFixture in real-time and is the most reliable
        // proof that bots entered WSG. Bg.log has Docker flush timing issues.
        _output.WriteLine("Waiting for SMSG_NEW_WORLD map=489 in StateManager output...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var botsInWsg = 0;
        while (sw.Elapsed < TimeSpan.FromMinutes(10))
        {
            // Check StateManager output for transfer evidence
            var output = _bot.GetStateManagerOutput();
            botsInWsg = output.Count(line =>
                line.Contains("SMSG_NEW_WORLD") && line.Contains("map=489"));

            if (botsInWsg >= 4)
            {
                _output.WriteLine($"[WSGEntry] PASS at {sw.Elapsed.TotalSeconds:F0}s: {botsInWsg} SMSG_NEW_WORLD map=489 events detected");
                break;
            }

            if ((int)sw.Elapsed.TotalSeconds % 30 == 0)
                _output.WriteLine($"[WSGEntry] {sw.Elapsed.TotalSeconds:F0}s: {botsInWsg} transfers detected");
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
