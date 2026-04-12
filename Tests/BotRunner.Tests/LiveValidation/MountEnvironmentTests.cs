using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Scene-driven indoor/outdoor mount validation using a single background bot.
/// The behavior under test is environment classification plus mount allow/block,
/// not battleground-specific flow or multi-bot coordination.
/// </summary>
[Collection(SingleBotValidationCollection.Name)]
public class MountEnvironmentTests
{
    private const uint RidingSkillId = 762;
    private const uint ApprenticeRidingSpellId = 33389;
    private const uint TestMountSpellId = 23509; // Frostwolf Howler; proven in Horde live setup.

    private static readonly TestLocation OutdoorLocation = new(
        MapId: 1,
        X: -618.518f,
        Y: -4251.67f,
        Z: 38.718f,
        Label: "Valley of Trials");

    // Use a clearly indoor instance point so this stays a general single-bot
    // environment check without battleground or multi-bot coupling.
    private static readonly TestLocation IndoorLocation = new(
        MapId: 389,
        X: 3f,
        Y: -11f,
        Z: -18f,
        Label: "Ragefire Chasm interior");

    private readonly SingleBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public MountEnvironmentTests(SingleBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Snapshot_OutdoorLocation_ReportsNotIndoors()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG", teleportToSafeZone: false);
        await EnsureUnmountedAsync(account, "MOUNT-ENV:OutdoorFlagUnmounted");
        await TeleportAsync(account, OutdoorLocation);

        var snapshot = await WaitForIndoorStateAsync(account, expectedIndoors: false, phaseName: "MOUNT-ENV:OutdoorFlag");
        _output.WriteLine(
            $"[MOUNT-ENV:OutdoorFlag] account={account}, location={OutdoorLocation.Label}, indoors={snapshot.IsIndoors}, mountDisplayId={snapshot.Player?.Unit?.MountDisplayId ?? 0}");
    }

    [SkippableFact]
    public async Task Snapshot_IndoorLocation_ReportsIsIndoors()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG", teleportToSafeZone: false);
        await EnsureUnmountedAsync(account, "MOUNT-ENV:IndoorFlagUnmounted");
        await TeleportAsync(account, IndoorLocation);

        var snapshot = await WaitForIndoorStateAsync(account, expectedIndoors: true, phaseName: "MOUNT-ENV:IndoorFlag");
        _output.WriteLine(
            $"[MOUNT-ENV:IndoorFlag] account={account}, location={IndoorLocation.Label}, indoors={snapshot.IsIndoors}, mountDisplayId={snapshot.Player?.Unit?.MountDisplayId ?? 0}");
    }

    [SkippableFact]
    public async Task MountSpell_OutdoorLocation_Mounts()
    {
        var account = _bot.BgAccountName!;

        await PrepareMountReadyBotAsync(account);
        await TeleportAsync(account, OutdoorLocation);
        await WaitForIndoorStateAsync(account, expectedIndoors: false, phaseName: "MOUNT-ENV:OutdoorSetup");

        await CastMountSpellAsync(account);
        var mounted = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.Unit?.MountDisplayId ?? 0) != 0,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: "MOUNT-ENV:OutdoorMount");
        Assert.True(mounted, "Expected the single bot to mount outdoors.");

        var snapshot = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snapshot);
        Assert.False(snapshot!.IsIndoors, "Outdoor mount check should stay outdoors.");
        Assert.NotEqual(0u, snapshot.Player?.Unit?.MountDisplayId ?? 0);
    }

    [SkippableFact]
    public async Task MountSpell_IndoorLocation_DoesNotMount()
    {
        var account = _bot.BgAccountName!;

        await PrepareMountReadyBotAsync(account);
        await TeleportAsync(account, IndoorLocation);
        var setupSnapshot = await WaitForIndoorStateAsync(account, expectedIndoors: true, phaseName: "MOUNT-ENV:IndoorSetup");
        var baselineChats = setupSnapshot.RecentChatMessages.ToArray();
        var baselineErrors = setupSnapshot.RecentErrors.ToArray();

        await CastMountSpellAsync(account);

        var rejectionEvidence = await WaitForIndoorMountRejectionEvidenceAsync(
            account,
            baselineChats,
            baselineErrors,
            TimeSpan.FromSeconds(10),
            phaseName: "MOUNT-ENV:IndoorBlock");
        Assert.NotNull(rejectionEvidence);
        _output.WriteLine($"[MOUNT-ENV:IndoorBlock] {rejectionEvidence}");

        await Task.Delay(1500);

        var snapshot = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsIndoors, "Indoor mount check should stay indoors.");
        Assert.Equal(0u, snapshot.Player?.Unit?.MountDisplayId ?? 0);
    }

    private async Task PrepareMountReadyBotAsync(string account)
    {
        await _bot.EnsureCleanSlateAsync(account, "BG", teleportToSafeZone: false);

        var snapshot = await _bot.GetSnapshotAsync(account);
        var knownSpells = snapshot?.Player?.SpellList;
        var skillInfo = snapshot?.Player?.SkillInfo;

        if (knownSpells?.Contains(ApprenticeRidingSpellId) != true)
            await _bot.BotLearnSpellAsync(account, ApprenticeRidingSpellId);

        if (skillInfo == null
            || !skillInfo.TryGetValue(RidingSkillId, out var ridingSkillValue)
            || ridingSkillValue < 150)
        {
            await _bot.BotSetSkillAsync(account, RidingSkillId, currentValue: 150, maxValue: 150);
        }

        if (knownSpells?.Contains(TestMountSpellId) != true)
            await _bot.BotLearnSpellAsync(account, TestMountSpellId);

        await EnsureUnmountedAsync(account, "MOUNT-ENV:Unmounted");
    }

    private async Task EnsureUnmountedAsync(string account, string phaseName)
    {
        var baseline = await _bot.GetSnapshotAsync(account);
        if ((baseline?.Player?.Unit?.MountDisplayId ?? 0) == 0)
            return;

        await _bot.SendGmChatCommandAsync(account, ".dismount");
        await _bot.SendGmChatCommandAsync(account, $".unaura {TestMountSpellId}");

        var unmounted = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.Unit?.MountDisplayId ?? 0) == 0,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: phaseName);

        var snapshot = await _bot.GetSnapshotAsync(account);
        if (snapshot == null)
            Assert.True(unmounted, $"Expected {account} to be unmounted before staging, but no snapshot was available.");

        if (!unmounted)
        {
            var mountDisplayId = snapshot?.Player?.Unit?.MountDisplayId ?? 0;
            var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine(
                $"[{phaseName}] finalSnapshot account={account} mountDisplayId={mountDisplayId} " +
                $"map={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) indoors={snapshot?.IsIndoors}");
        }

        Assert.True(unmounted, "Expected the single bot to be unmounted before staging.");
    }

    private async Task TeleportAsync(string account, TestLocation location)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await _bot.BotTeleportAsync(account, (int)location.MapId, location.X, location.Y, location.Z);

            var phasePrefix = $"MOUNT-ENV:Teleport:{location.Label}:attempt{attempt}";
            var settled = await WaitForLocationStableAsync(account, location, TimeSpan.FromSeconds(10), phasePrefix);
            if (!settled)
                continue;

            await _bot.WaitForZStabilizationAsync(account, waitMs: 2000);

            var remainedSettled = await WaitForLocationStableAsync(
                account,
                location,
                TimeSpan.FromSeconds(4),
                $"{phasePrefix}:post-z");
            if (remainedSettled)
                return;
        }

        var snapshot = await _bot.GetSnapshotAsync(account);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.Fail(
            $"Expected {account} to reach {location.Label} before continuing. " +
            $"finalMap={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) indoors={snapshot?.IsIndoors}");
    }

    private async Task<WoWActivitySnapshot> WaitForIndoorStateAsync(string account, bool expectedIndoors, string phaseName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var consecutiveMatches = 0;
        var lastProgressLog = TimeSpan.Zero;
        WoWActivitySnapshot? snapshot = null;
        var stopwatch = Stopwatch.StartNew();

        while (!cts.IsCancellationRequested)
        {
            snapshot = await _bot.GetSnapshotAsync(account);
            if (snapshot != null
                && snapshot.IsObjectManagerValid
                && snapshot.IsIndoors == expectedIndoors)
            {
                consecutiveMatches++;
                if (consecutiveMatches >= 3)
                    return snapshot;
            }
            else
            {
                consecutiveMatches = 0;
            }

            if (stopwatch.Elapsed - lastProgressLog >= TimeSpan.FromSeconds(5))
            {
                lastProgressLog = stopwatch.Elapsed;
                _output.WriteLine($"  [{phaseName}] Still waiting... {stopwatch.Elapsed.TotalSeconds:F0}s / 15s elapsed");
            }

            await Task.Delay(300);
        }

        snapshot = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snapshot);
        var position = snapshot!.Player?.Unit?.GameObject?.Base?.Position;
        var mountDisplayId = snapshot.Player?.Unit?.MountDisplayId ?? 0;
        _output.WriteLine(
            $"[{phaseName}] finalSnapshot account={account} indoors={snapshot.IsIndoors} objMgr={snapshot.IsObjectManagerValid} " +
            $"mountDisplayId={mountDisplayId} map={snapshot.CurrentMapId} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");

        Assert.Fail($"Expected {account} indoors={expectedIndoors} during {phaseName}.");
        return snapshot;
    }

    private async Task<bool> WaitForLocationStableAsync(
        string account,
        TestLocation location,
        TimeSpan timeout,
        string phaseName)
    {
        using var cts = new CancellationTokenSource(timeout);
        var consecutiveMatches = 0;
        var lastProgressLog = TimeSpan.Zero;
        var stopwatch = Stopwatch.StartNew();

        while (!cts.IsCancellationRequested)
        {
            var snapshot = await _bot.GetSnapshotAsync(account);
            if (snapshot != null
                && snapshot.IsObjectManagerValid
                && IsSnapshotNearLocation(snapshot, location))
            {
                consecutiveMatches++;
                if (consecutiveMatches >= 3)
                    return true;
            }
            else
            {
                consecutiveMatches = 0;
            }

            if (stopwatch.Elapsed - lastProgressLog >= TimeSpan.FromSeconds(5))
            {
                lastProgressLog = stopwatch.Elapsed;
                var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
                _output.WriteLine(
                    $"  [{phaseName}] Still waiting... {stopwatch.Elapsed.TotalSeconds:F0}s / {timeout.TotalSeconds:F0}s elapsed " +
                    $"currentMap={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");
            }

            await Task.Delay(300);
        }

        return false;
    }

    private static bool IsSnapshotNearLocation(WoWActivitySnapshot snapshot, TestLocation location)
    {
        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId != location.MapId)
            return false;

        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        if (position == null)
            return false;

        var dx = position.X - location.X;
        var dy = position.Y - location.Y;
        var distance2D = MathF.Sqrt((dx * dx) + (dy * dy));
        return distance2D <= 50f;
    }

    private async Task CastMountSpellAsync(string account)
    {
        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters =
            {
                new RequestParameter { IntParam = (int)TestMountSpellId }
            }
        });

        Assert.Equal(ResponseResult.Success, result);
    }

    private async Task<string?> WaitForIndoorMountRejectionEvidenceAsync(
        string account,
        IReadOnlyList<string> baselineChats,
        IReadOnlyList<string> baselineErrors,
        TimeSpan timeout,
        string phaseName)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastProgressLog = TimeSpan.Zero;

        while (stopwatch.Elapsed < timeout)
        {
            var snapshot = await _bot.GetSnapshotAsync(account);
            if (snapshot != null)
            {
                var chatDelta = GetDeltaMessages(baselineChats, snapshot.RecentChatMessages);
                var chatMatch = chatDelta.LastOrDefault(
                    message => message.Contains($"[MOUNT-BLOCK] spell={TestMountSpellId}", StringComparison.Ordinal));
                if (chatMatch != null)
                    return chatMatch;

                var errorDelta = GetDeltaMessages(baselineErrors, snapshot.RecentErrors);
                var errorMatch = errorDelta.LastOrDefault(
                    message => message.Contains("ONLY_OUTDOORS", StringComparison.Ordinal));
                if (errorMatch != null)
                    return errorMatch;
            }

            if (stopwatch.Elapsed - lastProgressLog >= TimeSpan.FromSeconds(5))
            {
                lastProgressLog = stopwatch.Elapsed;
                _output.WriteLine($"  [{phaseName}] Still waiting... {stopwatch.Elapsed.TotalSeconds:F0}s / {timeout.TotalSeconds:F0}s elapsed");
            }

            await Task.Delay(300);
        }

        var finalSnapshot = await _bot.GetSnapshotAsync(account);
        if (finalSnapshot != null)
        {
            var recentChatTail = string.Join(" || ", finalSnapshot.RecentChatMessages.TakeLast(6));
            var recentErrorTail = string.Join(" || ", finalSnapshot.RecentErrors.TakeLast(6));
            _output.WriteLine($"[{phaseName}] recentChat={recentChatTail} recentErrors={recentErrorTail}");
        }

        return null;
    }

    private async Task<string?> WaitForRecentChatDeltaAsync(
        string account,
        IReadOnlyList<string> baselineMessages,
        Func<string, bool> predicate,
        TimeSpan timeout,
        string phaseName)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastProgressLog = TimeSpan.Zero;

        while (stopwatch.Elapsed < timeout)
        {
            var snapshot = await _bot.GetSnapshotAsync(account);
            if (snapshot != null)
            {
                var deltaMessages = GetDeltaMessages(baselineMessages, snapshot.RecentChatMessages);
                var match = deltaMessages.LastOrDefault(predicate);
                if (match != null)
                    return match;
            }

            if (stopwatch.Elapsed - lastProgressLog >= TimeSpan.FromSeconds(5))
            {
                lastProgressLog = stopwatch.Elapsed;
                _output.WriteLine($"  [{phaseName}] Still waiting... {stopwatch.Elapsed.TotalSeconds:F0}s / {timeout.TotalSeconds:F0}s elapsed");
            }

            await Task.Delay(300);
        }

        var finalSnapshot = await _bot.GetSnapshotAsync(account);
        if (finalSnapshot != null)
        {
            var recentChatTail = string.Join(" || ", finalSnapshot.RecentChatMessages.TakeLast(6));
            var recentErrorTail = string.Join(" || ", finalSnapshot.RecentErrors.TakeLast(6));
            _output.WriteLine($"[{phaseName}] recentChat={recentChatTail} recentErrors={recentErrorTail}");
        }

        return null;
    }

    private static List<string> GetDeltaMessages(IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var remainingBaseline = new List<string>(baseline);
        var delta = new List<string>();

        foreach (var message in current)
        {
            var index = remainingBaseline.IndexOf(message);
            if (index >= 0)
                remainingBaseline.RemoveAt(index);
            else
                delta.Add(message);
        }

        return delta;
    }

    private readonly record struct TestLocation(uint MapId, float X, float Y, float Z, string Label);
}
