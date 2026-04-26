using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed indoor/outdoor mount validation. SHODAN stages the BG
/// action target with riding skill, mount spell, and coordinate teleports; the
/// BotRunner target receives only the mount-cast action under test.
/// </summary>
[Collection(LiveValidationCollection.Name)]
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

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public MountEnvironmentTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Snapshot_OutdoorLocation_ReportsNotIndoors()
    {
        var target = await EnsureMountEnvironmentSettingsAndTargetAsync();

        await StageLocationAsync(target, OutdoorLocation, cleanSlate: true);
        await _bot.StageBotRunnerUnmountedAsync(target.AccountName, target.RoleLabel, TestMountSpellId);

        var snapshot = await WaitForIndoorStateAsync(
            target.AccountName,
            expectedIndoors: false,
            phaseName: "MOUNT-ENV:OutdoorFlag");
        _output.WriteLine(
            $"[MOUNT-ENV:OutdoorFlag] account={target.AccountName}, location={OutdoorLocation.Label}, indoors={snapshot.IsIndoors}, mountDisplayId={snapshot.Player?.Unit?.MountDisplayId ?? 0}");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Snapshot_IndoorLocation_ReportsIsIndoors()
    {
        var target = await EnsureMountEnvironmentSettingsAndTargetAsync();

        await StageLocationAsync(target, IndoorLocation, cleanSlate: true);
        await _bot.StageBotRunnerUnmountedAsync(target.AccountName, target.RoleLabel, TestMountSpellId);

        var snapshot = await WaitForIndoorStateAsync(
            target.AccountName,
            expectedIndoors: true,
            phaseName: "MOUNT-ENV:IndoorFlag");
        _output.WriteLine(
            $"[MOUNT-ENV:IndoorFlag] account={target.AccountName}, location={IndoorLocation.Label}, indoors={snapshot.IsIndoors}, mountDisplayId={snapshot.Player?.Unit?.MountDisplayId ?? 0}");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MountSpell_OutdoorLocation_Mounts()
    {
        var target = await EnsureMountEnvironmentSettingsAndTargetAsync();

        await PrepareMountReadyBotAsync(target);
        await StageLocationAsync(target, OutdoorLocation);
        await WaitForIndoorStateAsync(
            target.AccountName,
            expectedIndoors: false,
            phaseName: "MOUNT-ENV:OutdoorSetup");

        await CastMountSpellAsync(target.AccountName);
        var mounted = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => (snapshot.Player?.Unit?.MountDisplayId ?? 0) != 0,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: "MOUNT-ENV:OutdoorMount");
        Assert.True(mounted, $"Expected {target.RoleLabel} to mount outdoors.");

        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snapshot);
        Assert.False(snapshot!.IsIndoors, "Outdoor mount check should stay outdoors.");
        Assert.NotEqual(0u, snapshot.Player?.Unit?.MountDisplayId ?? 0);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MountSpell_IndoorLocation_DoesNotMount()
    {
        var target = await EnsureMountEnvironmentSettingsAndTargetAsync();

        await PrepareMountReadyBotAsync(target);
        await StageLocationAsync(target, IndoorLocation);
        var setupSnapshot = await WaitForIndoorStateAsync(
            target.AccountName,
            expectedIndoors: true,
            phaseName: "MOUNT-ENV:IndoorSetup");
        var baselineChats = setupSnapshot.RecentChatMessages.ToArray();
        var baselineErrors = setupSnapshot.RecentErrors.ToArray();

        await CastMountSpellAsync(target.AccountName);

        var rejectionEvidence = await WaitForIndoorMountRejectionEvidenceAsync(
            target.AccountName,
            baselineChats,
            baselineErrors,
            TimeSpan.FromSeconds(10),
            phaseName: "MOUNT-ENV:IndoorBlock");
        Assert.NotNull(rejectionEvidence);
        _output.WriteLine($"[MOUNT-ENV:IndoorBlock] {rejectionEvidence}");

        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsIndoors, "Indoor mount check should stay indoors.");
        Assert.Equal(0u, snapshot.Player?.Unit?.MountDisplayId ?? 0);
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureMountEnvironmentSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG mount environment action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no mount action dispatch.");

        return target;
    }

    private async Task PrepareMountReadyBotAsync(LiveBotFixture.BotRunnerActionTarget target)
        => await _bot.StageBotRunnerMountLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            RidingSkillId,
            ridingValue: 150,
            ApprenticeRidingSpellId,
            TestMountSpellId);

    private async Task StageLocationAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        TestLocation location,
        bool cleanSlate = false)
    {
        var staged = await _bot.StageBotRunnerAtMountEnvironmentLocationAsync(
            target.AccountName,
            target.RoleLabel,
            location.Label,
            (int)location.MapId,
            location.X,
            location.Y,
            location.Z,
            cleanSlate);
        if (staged)
            return;

        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.Fail(
            $"Expected {target.RoleLabel} {target.AccountName} to reach {location.Label} before continuing. " +
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

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }

    private readonly record struct TestLocation(uint MapId, float X, float Y, float Z, string Label);
}
