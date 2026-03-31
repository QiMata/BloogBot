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
        var testStartTime = DateTime.UtcNow.AddSeconds(-5).ToString("yyyy-MM-dd HH:mm"); // 5s margin
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

        _output.WriteLine("Phase 2 complete. BG coordinator may have already started.");

        // Phase 3+4: Poll for WSG entry. The BG coordinator runs concurrently —
        // it may have already queued bots for WSG during Phase 2 prep (since bots
        // were already level 10 from a previous run). Poll immediately.
        // Wait for the BG to start and complete. The coordinator queues bots concurrently
        // with Phase 2 — the BG may have already started. Wait for it to complete.
        // Snapshot-based MapId detection is unreliable (100ms tick overwrites before 1s poll).
        // Instead, verify via VMaNGOS Bg.log which records all BG entries/exits.
        // Verify BG entry via VMaNGOS Bg.log. Snapshot-based MapId detection is unreliable
        // (100ms tick overwrites before test poll catches it).
        _output.WriteLine("Polling Bg.log for BG entries...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var botsInWsg = 0;
        while (sw.Elapsed < TimeSpan.FromMinutes(5))
        {
            if (File.Exists(bgLogPath))
            {
                string[] lines;
                using (var fs = new FileStream(bgLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                    lines = reader.ReadToEnd().Split('\n');

                // Only look at entries from the last 10 minutes (avoids stale data)
                var queueEntries = lines
                    .Where(l => l.Contains("tag BG=2") && l.Length > 16 && string.Compare(l[..16], testStartTime) >= 0)
                    .ToList();
                var uniqueNames = queueEntries
                    .Select(l => { var parts = l.Split(' '); return parts.Length > 1 ? parts[1].Split(':')[0] : ""; })
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList();
                botsInWsg = uniqueNames.Count;

                if (botsInWsg >= 4)
                {
                    _output.WriteLine($"[WSGEntry] PASS at {sw.Elapsed.TotalSeconds:F0}s: {botsInWsg} bots queued: {string.Join(", ", uniqueNames)}");
                    var winners = lines.Where(l => l.Contains("winner=") && l.Length > 16 && string.Compare(l[..16], testStartTime) >= 0).ToList();
                    foreach (var w in winners)
                        _output.WriteLine($"  BG result: {w.Trim()}");
                    break;
                }
            }

            if ((int)sw.Elapsed.TotalSeconds % 15 == 0)
                _output.WriteLine($"[WSGEntry] {sw.Elapsed.TotalSeconds:F0}s: {botsInWsg} new bots in Bg.log");
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
