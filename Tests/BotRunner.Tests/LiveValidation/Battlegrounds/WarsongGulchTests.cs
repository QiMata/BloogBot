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
/// 10 Horde (1 FG + 9 BG) + 10 Alliance (10 BG).
/// Flow:
///   1. All bots enter world
///   2. Horde teleport to Orgrimmar BG master area
///   3. Alliance teleport to Stormwind BG master area
///   4. Both sides queue for WSG
///   5. Accept invite, enter WSG (mapId=489)
///   6. Idle in BG (no objectives yet)
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~WarsongGulchTests" -v n --blame-hang --blame-hang-timeout 15m
/// </summary>
[Collection(WarsongGulchCollection.Name)]
public class WarsongGulchTests
{
    private readonly WarsongGulchFixture _bot;
    private readonly ITestOutputHelper _output;

    // Horde BG master area (Orgrimmar, Hall of the Brave)
    private const float HordeBgX = 1702f;
    private const float HordeBgY = -4422f;
    private const float HordeBgZ = 25f;

    // Alliance BG master area (Stormwind, Command Center)
    private const float AllianceBgX = -8757f;
    private const float AllianceBgY = 400f;
    private const float AllianceBgZ = 105f;

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
    /// Phase 2: Teleport Horde to Orgrimmar BG area, Alliance to Stormwind BG area.
    /// Then queue for WSG at respective BG masters.
    /// </summary>
    [SkippableFact]
    public async Task WSG_TeleportToFactionCities()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");

        // Wait for bots to enter world
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

        // Level all bots to 10 (WSG minimum) and ensure GM mode OFF
        foreach (var snap in _bot.AllBots)
        {
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".character level 10");
            await _bot.SendGmChatCommandAsync(snap.AccountName, ".gm off");
        }
        await Task.Delay(2000);

        // Teleport Horde bots to Orgrimmar BG master area
        foreach (var account in _bot.HordeAccounts)
        {
            _output.WriteLine($"Teleporting {account} to Orgrimmar BG area");
            await _bot.BotTeleportAsync(account, 1, HordeBgX, HordeBgY, HordeBgZ);
            await Task.Delay(500);
        }

        // Teleport Alliance bots to Stormwind BG area
        foreach (var account in _bot.AllianceAccounts)
        {
            _output.WriteLine($"Teleporting {account} to Stormwind BG area");
            await _bot.BotTeleportAsync(account, 0, AllianceBgX, AllianceBgY, AllianceBgZ);
            await Task.Delay(500);
        }

        // Wait for teleports to settle
        await Task.Delay(3000);

        await _bot.RefreshSnapshotsAsync();
        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}, pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        // Verify Horde bots are on Kalimdor (map 1)
        var hordeOnMap1 = _bot.AllBots.Count(s =>
            _bot.HordeAccounts.Contains(s.AccountName) &&
            (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == 1);
        _output.WriteLine($"Horde bots on Kalimdor: {hordeOnMap1}/{_bot.HordeAccounts.Length}");

        // Verify Alliance bots are on Eastern Kingdoms (map 0)
        var allyOnMap0 = _bot.AllBots.Count(s =>
            _bot.AllianceAccounts.Contains(s.AccountName) &&
            (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == 0);
        _output.WriteLine($"Alliance bots on Eastern Kingdoms: {allyOnMap0}/{_bot.AllianceAccounts.Length}");

        // Send JoinBattleground action to all bots
        // WSG type=2, expected map=489
        foreach (var snap in _bot.AllBots)
        {
            _output.WriteLine($"Sending JoinBattleground to {snap.AccountName}");
            var action = new Communication.ActionMessage
            {
                ActionType = Communication.ActionType.JoinBattleground,
            };
            action.Parameters.Add(new Communication.RequestParameter { IntParam = 2 }); // WSG
            action.Parameters.Add(new Communication.RequestParameter { IntParam = 489 }); // expected map
            await _bot.SendActionAsync(snap.AccountName, action);
            await Task.Delay(200);
        }

        // Wait for bots to enter WSG (map 489)
        _output.WriteLine("\nWaiting for bots to enter WSG...");
        var botsInWsg = await WaitForProgressAsync(
            phaseName: "WSGEntry",
            maxTimeout: TimeSpan.FromMinutes(5),
            staleTimeout: TimeSpan.FromSeconds(60),
            pollInterval: TimeSpan.FromSeconds(5),
            evaluate: snapshots =>
            {
                var onWsg = snapshots.Count(s =>
                    (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == WarsongGulchFixture.WsgMapId);
                var fingerprint = $"wsg={onWsg}/{snapshots.Count}";
                return (onWsg >= 10, onWsg, fingerprint); // Need at least 10 for BG to start
            });

        _output.WriteLine($"\n=== WSG ENTRY RESULT ===");
        _output.WriteLine($"Bots in WSG: {botsInWsg}");
        foreach (var snap in _bot.AllBots)
        {
            var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            _output.WriteLine($"  {snap.AccountName}: map={mapId}");
        }
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
