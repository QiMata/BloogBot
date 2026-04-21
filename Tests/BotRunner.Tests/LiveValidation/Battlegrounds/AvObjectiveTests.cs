using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Tasks.Battlegrounds;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Live Alterac Valley objective coverage:
/// 1. prove a Horde assaulter can burn an Alliance bunker.
/// 2. prove a Horde assaulter can capture neutral Snowfall Graveyard.
/// 3. script a Horde tower-burn into Vanndar kill and wait for AV completion.
/// </summary>
[Collection(AlteracValleyObjectiveCollection.Name)]
public class AvObjectiveTests
{
    private static readonly TimeSpan AlteracValleyPrepWindow = TimeSpan.FromSeconds(130);
    private const int MinimumReadyHordeCount = 24;
    private const int MinimumReadyAllianceCount = 24;
    private const int MinimumReadyOnMapCount = 56;

    private static readonly AvObjectiveCapture SnowfallGraveyardObjective =
        new("AVBOT5", "Snowfall Graveyard", 180418, -202.581f, -112.730f, 78.488f, 1342, 1344);

    private static readonly IReadOnlyList<AvObjectiveCapture> AllianceBunkerAssaultPlan =
    [
        new("AVBOT1", "Stonehearth Bunker", 178925, -152.434f, -441.615f, 40.397f, 1373, 1381),
        new("AVBOT2", "Icewing Bunker", 178925, 203.238f, -360.264f, 56.386f, 1372, 1380),
        new("AVBOT3", "Dun Baldar South Bunker", 178925, 553.779f, -78.657f, 51.938f, 1370, 1378),
        new("AVBOT4", "Dun Baldar North Bunker", 178925, 674.001f, -143.125f, 63.662f, 1371, 1379),
    ];

    private const string VanndarStormpikeName = "Vanndar Stormpike";
    private const float VanndarStormpikeX = 722.430f;
    private const float VanndarStormpikeY = -10.998f;
    private const float VanndarStormpikeZ = 50.705f;

    private static readonly IReadOnlyList<string> AllTrackedAccounts = AlteracValleyFixture.HordeAccountsOrdered
        .Concat(AlteracValleyFixture.AllianceAccountsOrdered)
        .ToArray();
    private static readonly IReadOnlyList<string> HordeTrackedAccounts = AlteracValleyFixture.HordeAccountsOrdered
        .ToArray();

    private readonly AlteracValleyObjectiveFixture _bot;
    private readonly ITestOutputHelper _output;

    public AvObjectiveTests(AlteracValleyObjectiveFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task AV_TowerAssault_HordeBurnsStonehearthBunker()
    {
        await PrepareAndEnterBattlegroundAsync([AllianceBunkerAssaultPlan[0].Account]);
        await AssaultObjectiveAsync(
            AllianceBunkerAssaultPlan[0],
            phaseName: "AV:StonehearthBunker",
            waitForControl: true,
            controlTimeout: TimeSpan.FromMinutes(6));
    }

    [SkippableFact]
    public async Task AV_GraveyardCapture_HordeCapturesSnowfallGraveyard()
    {
        await PrepareAndEnterBattlegroundAsync([SnowfallGraveyardObjective.Account]);
        await AssaultObjectiveAsync(
            SnowfallGraveyardObjective,
            phaseName: "AV:SnowfallGraveyard",
            waitForControl: true,
            controlTimeout: TimeSpan.FromMinutes(6));
    }

    [SkippableFact]
    public async Task AV_FullGame_HordeBurnsAllianceTowersAndKillsVanndar()
    {
        var requiredAccounts = AllianceBunkerAssaultPlan
            .Select(objective => objective.Account)
            .ToArray();
        var readyRoster = await PrepareAndEnterBattlegroundAsync(requiredAccounts);
        var availableHordeAccounts = SelectAvailableHordeAccounts(
            readyRoster,
            minimumCount: MinimumReadyHordeCount,
            requiredAccounts: requiredAccounts,
            phaseName: "AV:HordeCombatants");
        var vanndarAttackAccounts = availableHordeAccounts
            .Take(24)
            .ToArray();
        var baselineMarkCounts = BgTestHelper.CaptureTrackedBagItemCounts(
            await _bot.QueryAllSnapshotsAsync(),
            availableHordeAccounts,
            BgRewardCollectionTask.AvMarkOfHonor);

        foreach (var objective in AllianceBunkerAssaultPlan)
        {
            await AssaultObjectiveAsync(
                objective,
                phaseName: $"AV:{BuildPhaseToken(objective.Name)}",
                waitForControl: false);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        await WaitForAllianceBunkerBurnsAsync();
        await StageRaidAtVanndarAsync(vanndarAttackAccounts);
        await AttackVanndarAsync(vanndarAttackAccounts);

        await BgTestHelper.WaitForBattlegroundCompletionAsync(
            _bot,
            _output,
            availableHordeAccounts,
            AlteracValleyFixture.AvMapId,
            phaseName: "AV:Completion",
            queryTrackedAccountsIndividually: true,
            maxTimeout: TimeSpan.FromMinutes(20),
            completionChatPredicate: ContainsBattlegroundResultMessage);

        var rewardCounts = await BgTestHelper.WaitForTrackedBagItemIncreaseAsync(
            _bot,
            _output,
            baselineMarkCounts,
            BgRewardCollectionTask.AvMarkOfHonor,
            phaseName: "AV:Rewards",
            minimumAccountsWithIncrease: 10,
            queryTrackedAccountsIndividually: true,
            maxTimeout: TimeSpan.FromMinutes(2));
        var rewardedAccounts = BgTestHelper.FindAccountsWithBagItemIncrease(baselineMarkCounts, rewardCounts);
        Assert.Contains(vanndarAttackAccounts[0], rewardedAccounts);
    }

    private async Task<ReadyRoster> PrepareAndEnterBattlegroundAsync(IReadOnlyCollection<string> requiredAccounts)
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        var readyRoster = await WaitForObjectiveReadyRosterAsync(
            requiredAccounts,
            minimumReadyBots: AlteracValleyObjectiveFixture.ObjectiveReadyMinimumBotCount,
            minimumReadyHorde: MinimumReadyHordeCount,
            minimumReadyAlliance: MinimumReadyAllianceCount,
            phaseName: "AV:ReadyRoster");
        await _bot.EnsureObjectivePreparedAsync();

        var minBotsOnMap = Math.Min(MinimumReadyOnMapCount, readyRoster.TotalReadyBots);
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, AlteracValleyFixture.AvMapId, minBotsOnMap, "AV");
        var settledOnBg = await BgTestHelper.WaitForBgEntrySettlingAsync(
            _bot,
            _output,
            AlteracValleyFixture.AvMapId,
            targetOnMap: Math.Max(minBotsOnMap, readyRoster.TotalReadyBots - 4),
            bgName: "AV",
            settleWindow: TimeSpan.FromSeconds(45));

        await _bot.MountRaidForFirstObjectiveAsync();
        await BgTestHelper.WaitForAccountsMountedAsync(
            _bot,
            _output,
            readyRoster.ReadyAccounts,
            expectedMounted: Math.Min(minBotsOnMap, settledOnBg),
            phaseName: "AV:Mount");

        WriteProgress($"[AV:PrepWindow] waiting {AlteracValleyPrepWindow.TotalSeconds:F0}s for gates to open before scripted objectives");
        await Task.Delay(AlteracValleyPrepWindow);
        Assert.Equal(ResponseResult.Success, await _bot.SetCoordinatorEnabledForObjectivePushAsync(false));
        await _bot.QuiesceAccountsAsync(readyRoster.ReadyAccounts, "AV:QuiesceAfterPrep");
        return readyRoster;
    }

    private async Task AssaultObjectiveAsync(
        AvObjectiveCapture objective,
        string phaseName,
        bool waitForControl,
        TimeSpan? controlTimeout = null)
    {
        WriteProgress(
            $"[{phaseName}] teleporting {objective.Account} to {objective.Name} at " +
            $"({objective.X:F1},{objective.Y:F1},{objective.Z:F1})");
        await _bot.BotTeleportAsync(
            objective.Account,
            (int)AlteracValleyFixture.AvMapId,
            objective.X,
            objective.Y,
            objective.Z);

        var banner = await BgTestHelper.WaitForNearbyGameObjectAsync(
            _bot,
            _output,
            objective.Account,
            AlteracValleyFixture.AvMapId,
            gameObject => gameObject.Entry == objective.InitialBannerEntry,
            phaseName: $"{phaseName}:FindBanner",
            maxTimeout: TimeSpan.FromSeconds(20));

        var trackedAccounts = new[] { objective.Account };
        var baselineSnapshots = await _bot.QueryAllSnapshotsAsync();
        var baselineAssaultCounts = BgTestHelper.CaptureTrackedChatMatchCounts(
            baselineSnapshots,
            trackedAccounts,
            message => MatchesWorldStateMessage(message, objective.HordeAssaultWorldStateId, 1));
        var baselineControlCounts = BgTestHelper.CaptureTrackedChatMatchCounts(
            baselineSnapshots,
            trackedAccounts,
            message => MatchesWorldStateMessage(message, objective.HordeControlWorldStateId, 1));

        var interactResult = await _bot.SendActionAsync(
            objective.Account,
            new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)banner.Guid } }
            });
        Assert.Equal(ResponseResult.Success, interactResult);
        WriteProgress(
            $"[{phaseName}] {objective.Account} interacted with {objective.Name} banner guid=0x{banner.Guid:X}");

        await BgTestHelper.WaitForNearbyGameObjectAbsentAsync(
            _bot,
            _output,
            objective.Account,
            AlteracValleyFixture.AvMapId,
            gameObject => gameObject.Entry == objective.InitialBannerEntry,
            phaseName: $"{phaseName}:BannerStateChange",
            maxTimeout: TimeSpan.FromSeconds(20));

        await BgTestHelper.WaitForTrackedChatIncreaseAsync(
            _bot,
            _output,
            baselineAssaultCounts,
            trackedAccounts,
            message => MatchesWorldStateMessage(message, objective.HordeAssaultWorldStateId, 1),
            phaseName: $"{phaseName}:AssaultWorldState",
            minimumAccountsWithIncrease: 1,
            maxTimeout: TimeSpan.FromSeconds(45));

        if (!waitForControl)
            return;

        await BgTestHelper.WaitForTrackedChatIncreaseAsync(
            _bot,
            _output,
            baselineControlCounts,
            trackedAccounts,
            message => MatchesWorldStateMessage(message, objective.HordeControlWorldStateId, 1),
            phaseName: $"{phaseName}:ControlWorldState",
            minimumAccountsWithIncrease: 1,
            maxTimeout: controlTimeout ?? TimeSpan.FromMinutes(6));
    }

    private async Task WaitForAllianceBunkerBurnsAsync()
    {
        var trackedAccounts = AllianceBunkerAssaultPlan
            .Select(objective => objective.Account)
            .ToArray();
        var baselineCounts = BgTestHelper.CaptureTrackedChatMatchCounts(
            await _bot.QueryAllSnapshotsAsync(),
            trackedAccounts,
            message => AllianceBunkerAssaultPlan.Any(objective =>
                MatchesWorldStateMessage(message, objective.HordeControlWorldStateId, 1)));

        foreach (var objective in AllianceBunkerAssaultPlan)
        {
            await BgTestHelper.WaitForTrackedChatIncreaseAsync(
                _bot,
                _output,
                baselineCounts,
                trackedAccounts,
                message => MatchesWorldStateMessage(message, objective.HordeControlWorldStateId, 1),
                phaseName: $"AV:{BuildPhaseToken(objective.Name)}:Burn",
                minimumAccountsWithIncrease: 1,
                maxTimeout: TimeSpan.FromMinutes(6));
        }
    }

    private async Task StageRaidAtVanndarAsync(IReadOnlyList<string> attackerAccounts)
    {
        var stagedTargets = new Dictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < attackerAccounts.Count; index++)
        {
            var account = attackerAccounts[index];
            var target = BuildVanndarStagingTarget(index);
            stagedTargets[account] = target;
            await _bot.BotTeleportAsync(account, (int)target.MapId, target.X, target.Y, target.Z);
            await Task.Delay(75);
        }

        await BgTestHelper.WaitForAccountsNearTargetsAsync(
            _bot,
            _output,
            attackerAccounts,
            stagedTargets,
            leaderAccount: null,
            minReached: Math.Max(16, attackerAccounts.Count - 4),
            minGroupedToLeader: 0,
            phaseName: "AV:VanndarStage",
            maxDistance: 18f,
            redispatchLaggingGoto: false,
            maxTimeout: TimeSpan.FromMinutes(2),
            staleTimeout: TimeSpan.FromSeconds(45));
    }

    private async Task AttackVanndarAsync(IReadOnlyList<string> attackerAccounts)
    {
        var visibleAccounts = await WaitForAccountsSeeingUnitAsync(
            attackerAccounts,
            VanndarStormpikeName,
            minimumVisible: 16,
            phaseName: "AV:VanndarVisible",
            maxTimeout: TimeSpan.FromSeconds(30));
        Assert.NotEmpty(visibleAccounts);

        var leaderSnapshot = await _bot.GetSnapshotAsync(visibleAccounts[0]);
        var vanndar = FindNearbyUnitByName(leaderSnapshot, VanndarStormpikeName);
        Assert.NotNull(vanndar);
        var vanndarGuid = vanndar!.GameObject?.Base?.Guid ?? 0UL;
        Assert.NotEqual(0UL, vanndarGuid);
        WriteProgress($"[AV:VanndarAttack] visibleAccounts={visibleAccounts.Count}, guid=0x{vanndarGuid:X}");

        foreach (var account in visibleAccounts)
        {
            var dispatchResult = await _bot.SendActionAsync(
                account,
                new ActionMessage
                {
                    ActionType = ActionType.StartMeleeAttack,
                    Parameters = { new RequestParameter { LongParam = (long)vanndarGuid } }
                });
            Assert.Equal(ResponseResult.Success, dispatchResult);
            await Task.Delay(40);
        }

        var engaged = await _bot.WaitForSnapshotConditionAsync(
            visibleAccounts[0],
            snapshot => snapshot?.Player?.Unit?.TargetGuid == vanndarGuid,
            TimeSpan.FromSeconds(20),
            progressLabel: "AV:VanndarTargetLock");
        Assert.True(engaged, $"[AV:VanndarAttack] {visibleAccounts[0]} never targeted Vanndar 0x{vanndarGuid:X}.");
    }

    private async Task<IReadOnlyList<string>> WaitForAccountsSeeingUnitAsync(
        IReadOnlyCollection<string> accounts,
        string unitName,
        int minimumVisible,
        string phaseName,
        TimeSpan maxTimeout)
    {
        var sw = Stopwatch.StartNew();
        var lastSummary = string.Empty;

        while (sw.Elapsed < maxTimeout)
        {
            if (_bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = await _bot.QueryAllSnapshotsAsync();
            var visibleAccounts = snapshots
                .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
                .Where(snapshot => accounts.Contains(snapshot.AccountName, StringComparer.OrdinalIgnoreCase))
                .Where(snapshot => FindNearbyUnitByName(snapshot, unitName) != null)
                .Select(snapshot => snapshot.AccountName!)
                .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (visibleAccounts.Length >= minimumVisible)
            {
                WriteProgress(
                    $"[{phaseName}] visible={visibleAccounts.Length}/{accounts.Count} at {sw.Elapsed.TotalSeconds:F0}s; " +
                    $"accounts={string.Join(", ", visibleAccounts.Take(10))}");
                return visibleAccounts;
            }

            var summary =
                $"visible={visibleAccounts.Length}/{minimumVisible}, accounts={(visibleAccounts.Length == 0 ? "(none)" : string.Join(", ", visibleAccounts.Take(8)))}";
            if (!string.Equals(summary, lastSummary, StringComparison.Ordinal))
            {
                WriteProgress($"[{phaseName}] {summary} at {sw.Elapsed.TotalSeconds:F0}s");
                lastSummary = summary;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var finalSnapshots = await _bot.QueryAllSnapshotsAsync();
        var finalVisibleAccounts = finalSnapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .Where(snapshot => accounts.Contains(snapshot.AccountName, StringComparer.OrdinalIgnoreCase))
            .Where(snapshot => FindNearbyUnitByName(snapshot, unitName) != null)
            .Select(snapshot => snapshot.AccountName!)
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Fail(
            $"[{phaseName}] TIMEOUT - visible={finalVisibleAccounts.Length}/{minimumVisible}, " +
            $"accounts={(finalVisibleAccounts.Length == 0 ? "(none)" : string.Join(", ", finalVisibleAccounts.Take(10)))}");
        return finalVisibleAccounts;
    }

    private static global::Game.WoWUnit? FindNearbyUnitByName(WoWActivitySnapshot? snapshot, string unitName)
    {
        return snapshot?.NearbyUnits?
            .FirstOrDefault(unit =>
            {
                var name = unit.GameObject?.Name;
                return !string.IsNullOrWhiteSpace(name)
                    && name.Contains(unitName, StringComparison.OrdinalIgnoreCase)
                    && unit.Health > 0;
            });
    }

    private static AlteracValleyLoadoutPlan.ObjectiveTarget BuildVanndarStagingTarget(int index)
    {
        var ring = index / 8;
        var slot = index % 8;
        var radius = 6f + (ring * 4f);
        var angle = slot * (MathF.PI / 4f);
        return new AlteracValleyLoadoutPlan.ObjectiveTarget(
            AlteracValleyFixture.AvMapId,
            VanndarStormpikeX + (MathF.Cos(angle) * radius),
            VanndarStormpikeY + (MathF.Sin(angle) * radius),
            VanndarStormpikeZ);
    }

    private void WriteProgress(string message)
    {
        _output.WriteLine(message);
        Console.WriteLine(message);
    }

    private IReadOnlyList<string> SelectAvailableHordeAccounts(
        ReadyRoster readyRoster,
        int minimumCount,
        IReadOnlyCollection<string> requiredAccounts,
        string phaseName)
    {
        var readyAccountSet = readyRoster.ReadyAccounts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingRequired = requiredAccounts
            .Where(account => !readyAccountSet.Contains(account))
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.True(
            missingRequired.Length == 0,
            $"[{phaseName}] missing required ready Horde accounts: {string.Join(", ", missingRequired)}");

        var availableAccounts = requiredAccounts
            .Concat(HordeTrackedAccounts.Where(account => readyAccountSet.Contains(account)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.True(
            availableAccounts.Length >= minimumCount,
            $"[{phaseName}] only {availableAccounts.Length} ready Horde accounts; need at least {minimumCount}.");
        WriteProgress(
            $"[{phaseName}] ready Horde accounts={availableAccounts.Length}; first={string.Join(", ", availableAccounts.Take(10))}");
        return availableAccounts;
    }

    private async Task<ReadyRoster> WaitForObjectiveReadyRosterAsync(
        IReadOnlyCollection<string> requiredAccounts,
        int minimumReadyBots,
        int minimumReadyHorde,
        int minimumReadyAlliance,
        string phaseName)
    {
        var sw = Stopwatch.StartNew();
        var lastSummary = string.Empty;

        while (sw.Elapsed < TimeSpan.FromMinutes(10))
        {
            if (_bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            await _bot.RefreshSnapshotsAsync();
            var snapshots = await _bot.QueryAllSnapshotsAsync();
            var readyAccounts = snapshots
                .Where(snapshot => snapshot.IsObjectManagerValid)
                .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
                .Select(snapshot => snapshot.AccountName!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var readyAccountSet = readyAccounts.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var readyHorde = HordeTrackedAccounts.Count(account => readyAccountSet.Contains(account));
            var readyAlliance = AlteracValleyFixture.AllianceAccountsOrdered.Count(account => readyAccountSet.Contains(account));
            var missingRequired = requiredAccounts
                .Where(account => !readyAccountSet.Contains(account))
                .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (readyAccounts.Length >= minimumReadyBots
                && readyHorde >= minimumReadyHorde
                && readyAlliance >= minimumReadyAlliance
                && missingRequired.Length == 0)
            {
                WriteProgress(
                    $"[{phaseName}] ready={readyAccounts.Length}, horde={readyHorde}, alliance={readyAlliance} at {sw.Elapsed.TotalSeconds:F0}s");
                return new ReadyRoster(readyAccounts.Length, readyHorde, readyAlliance, readyAccounts);
            }

            var summary =
                $"ready={readyAccounts.Length}/{minimumReadyBots}, horde={readyHorde}/{minimumReadyHorde}, alliance={readyAlliance}/{minimumReadyAlliance}, " +
                $"missingRequired={(missingRequired.Length == 0 ? "(none)" : string.Join(", ", missingRequired))}";
            if (!string.Equals(summary, lastSummary, StringComparison.Ordinal))
            {
                WriteProgress($"[{phaseName}] {summary} at {sw.Elapsed.TotalSeconds:F0}s");
                lastSummary = summary;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"[{phaseName}] TIMEOUT waiting for objective-ready AV roster.");
        return null!;
    }

    private static string BuildPhaseToken(string name)
        => name.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static bool MatchesWorldStateMessage(string message, int worldStateId, int expectedValue)
        => message.Contains($"/{worldStateId}={expectedValue}", StringComparison.Ordinal);

    private static bool ContainsBattlegroundResultMessage(string message)
    {
        return message.Contains("wins", StringComparison.OrdinalIgnoreCase)
            || message.Contains("victory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("defeat", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AvObjectiveCapture(
        string Account,
        string Name,
        uint InitialBannerEntry,
        float X,
        float Y,
        float Z,
        int HordeAssaultWorldStateId,
        int HordeControlWorldStateId);

    private sealed record ReadyRoster(
        int TotalReadyBots,
        int ReadyHordeBots,
        int ReadyAllianceBots,
        IReadOnlyList<string> ReadyAccounts);
}
