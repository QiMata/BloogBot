using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Arathi Basin and Alterac Valley entry tests.
///
/// Run:
///   dotnet test --filter "Namespace~Battlegrounds" --configuration Release -v n --blame-hang --blame-hang-timeout 60m
/// </summary>

[Collection(ArathiBasinCollection.Name)]
public class ArathiBasinTests
{
    private readonly ArathiBasinFixture _bot;
    private readonly ITestOutputHelper _output;

    public ArathiBasinTests(ArathiBasinFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task AB_AllBotsEnterWorld()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB");
    }

    [SkippableFact]
    public async Task AB_QueueAndEnterBattleground()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB");
        await _bot.ReprepareAsync();
        Assert.Equal(ResponseResult.Success, await _bot.SetRuntimeCoordinatorEnabledAsync(true));
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, ArathiBasinFixture.AbMapId, ArathiBasinFixture.TotalBotCount, "AB");
        await BgTestHelper.WaitForTrackedAccountsOnMapStableAsync(
            _bot,
            _output,
            ArathiBasinFixture.HordeAccountsOrdered.Concat(ArathiBasinFixture.AllianceAccountsOrdered).ToArray(),
            ArathiBasinFixture.AbMapId,
            phaseName: "AB:BG-Stable",
            stableWindow: TimeSpan.FromSeconds(20),
            maxTimeout: TimeSpan.FromMinutes(4),
            queryTrackedAccountsIndividually: true);
    }
}

[Collection(AlteracValleyCollection.Name)]
public class AlteracValleyTests
{
    private readonly AlteracValleyFixture _bot;
    private readonly ITestOutputHelper _output;

    public AlteracValleyTests(AlteracValleyFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// Single AV integration test: enter world → prep loadout → queue individually → enter BG →
    /// mount → reach first objective positions. Covers the full 80-bot (40v40) pipeline.
    /// </summary>
    [SkippableFact]
    public async Task AV_FullMatch_EnterPrepQueueMountAndReachObjective()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        // Phase 1: All 80 bots enter world
        await BgTestHelper.WaitForBotsAsync(_bot, _output, AlteracValleyFixture.TotalBotCount, "AV");

        // Phase 2: Loadout prep (level, gear, elixirs, mount item)
        await _bot.EnsureObjectivePreparedAsync();

        // Phase 3: Queue and enter AV instance (individual queue — no group queue to avoid anticheat)
        // Keep queue fill above the minimum objective roster before disabling coordinator push.
        var minBotsOnMap = 70;
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, AlteracValleyFixture.AvMapId, minBotsOnMap, "AV");
        var settledOnBg = await BgTestHelper.WaitForBgEntrySettlingAsync(
            _bot,
            _output,
            AlteracValleyFixture.AvMapId,
            targetOnMap: 76,
            bgName: "AV",
            settleWindow: TimeSpan.FromSeconds(45));

        // Phase 4: Keep coordinator active briefly so stragglers can still consume invite retries, then mount up.
        await _bot.MountRaidForFirstObjectiveAsync();
        await BgTestHelper.WaitForAccountsMountedAsync(
            _bot,
            _output,
            AlteracValleyFixture.HordeAccountsOrdered.Concat(AlteracValleyFixture.AllianceAccountsOrdered),
            expectedMounted: Math.Min(minBotsOnMap, settledOnBg),
            phaseName: "AV:Mount");

        // Vanilla AV keeps both teams in preparation caves before the gates open.
        // Dispatching long objective routes before that often burns movement tasks on cave gates.
        _output.WriteLine("[AV:PrepWindow] waiting 130s for AV gates to open before objective dispatch");
        await Task.Delay(TimeSpan.FromSeconds(130));
        Assert.Equal(ResponseResult.Success, await _bot.SetCoordinatorEnabledForObjectivePushAsync(false));
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Phase 5: Move to first objective positions
        var objectiveSnapshots = await _bot.QueryAllSnapshotsAsync();
        var assignments = _bot.BuildAdaptiveFirstObjectiveAssignments(
            objectiveSnapshots,
            log => _output.WriteLine($"[AV:Objectives] {log}"));
        foreach (var account in AlteracValleyFixture.HordeAccountsOrdered.Concat(AlteracValleyFixture.AllianceAccountsOrdered))
        {
            var target = assignments[account];
            var dispatchResult = await _bot.SendActionAsync(account, BgTestHelper.MakeGoto(target.X, target.Y, target.Z, stopDistance: 12f));
            Assert.Equal(ResponseResult.Success, dispatchResult);
            await Task.Delay(40);
        }

        // AV uses individual queue (no party groups). Bots form raid automatically inside AV.
        // Use relaxed thresholds: no leader requirement, no grouping requirement.
        // Only check that bots reached their objective positions.
        await BgTestHelper.WaitForAccountsNearTargetsAsync(
            _bot,
            _output,
            AlteracValleyFixture.HordeAccountsOrdered,
            assignments,
            leaderAccount: null,
            minReached: 25,
            minGroupedToLeader: 0,
            phaseName: "AV:HordeObjective",
            maxDistance: 60f,
            redispatchLaggingGoto: true,
            redispatchInterval: TimeSpan.FromSeconds(20),
            redispatchTargetFactory: BgTestHelper.BuildIncrementalRedispatchTarget,
            maxTimeout: TimeSpan.FromMinutes(6),
            staleTimeout: TimeSpan.FromMinutes(2));

        await BgTestHelper.WaitForAccountsNearTargetsAsync(
            _bot,
            _output,
            AlteracValleyFixture.AllianceAccountsOrdered,
            assignments,
            leaderAccount: null,
            minReached: 25,
            minGroupedToLeader: 0,
            phaseName: "AV:AllianceObjective",
            redispatchLaggingGoto: true,
            redispatchInterval: TimeSpan.FromSeconds(20),
            redispatchTargetFactory: BgTestHelper.BuildIncrementalRedispatchTarget,
            maxTimeout: TimeSpan.FromMinutes(6),
            staleTimeout: TimeSpan.FromMinutes(2));
    }
}

/// <summary>Shared BG test helper methods.</summary>
internal static class BgTestHelper
{
    public static ActionMessage MakeGoto(float x, float y, float z, float stopDistance = 3f)
        => new()
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = x },
                new RequestParameter { FloatParam = y },
                new RequestParameter { FloatParam = z },
                new RequestParameter { FloatParam = stopDistance }
            }
        };

    public static async Task WaitForBotsAsync(LiveBotFixture bot, ITestOutputHelper output, int expected, string bgName)
    {
        var sw = Stopwatch.StartNew();
        var lastCount = -1;
        var lastRawCount = -1;
        var lastChange = sw.Elapsed;
        var lastProgressSignature = string.Empty;

        while (sw.Elapsed < TimeSpan.FromMinutes(12))
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{bgName}:Enter] CRASHED");

            await bot.RefreshSnapshotsAsync();
            var count = bot.AllBots.Count;
            var snapshots = await bot.QueryAllSnapshotsAsync();
            var rawCount = snapshots.Count;
            var progressSignature = string.Join(
                "; ",
                snapshots
                    .GroupBy(snapshot =>
                    {
                        var screen = string.IsNullOrWhiteSpace(snapshot.ScreenState) ? "-" : snapshot.ScreenState;
                        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                        return $"{screen}|map={mapId}|objMgr={(snapshot.IsObjectManagerValid ? 1 : 0)}";
                    })
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => $"{group.Key}:{group.Count()}"));

            if (count == expected)
            {
                output.WriteLine(
                    $"[{bgName}:Enter] All {count}/{expected} bots entered world at {sw.Elapsed.TotalSeconds:F0}s (rawSnapshots={rawCount})");
                Assert.Equal(expected, count);
                return;
            }

            if (count != lastCount
                || rawCount != lastRawCount
                || !string.Equals(progressSignature, lastProgressSignature, StringComparison.Ordinal))
            {
                var hydratedAccounts = bot.AllBots
                    .Select(snapshot => snapshot.AccountName)
                    .Where(accountName => !string.IsNullOrWhiteSpace(accountName))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingHydratedAccounts = snapshots
                    .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
                    .Where(snapshot => !hydratedAccounts.Contains(snapshot.AccountName))
                    .Select(snapshot =>
                    {
                        var accountName = snapshot.AccountName!;
                        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                        var health = snapshot.Player?.Unit?.Health ?? 0;
                        var maxHealth = snapshot.Player?.Unit?.MaxHealth ?? 0;
                        var recentError = snapshot.RecentErrors.LastOrDefault() ?? "-";
                        return $"{accountName}(screen={snapshot.ScreenState}, map={mapId}, objMgr={snapshot.IsObjectManagerValid}, char={snapshot.CharacterName}, hp={health}/{maxHealth}, err={recentError})";
                    })
                    .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                output.WriteLine(
                    $"[{bgName}:Enter] hydrated={count}/{expected}, raw={rawCount} at {sw.Elapsed.TotalSeconds:F0}s");
                if (!string.IsNullOrWhiteSpace(progressSignature))
                    output.WriteLine($"[{bgName}:Enter] progress={progressSignature}");
                if (missingHydratedAccounts.Length > 0)
                    output.WriteLine($"[{bgName}:Enter] missingHydrated={string.Join(", ", missingHydratedAccounts)}");
                lastCount = count;
                lastRawCount = rawCount;
                lastChange = sw.Elapsed;
                lastProgressSignature = progressSignature;
            }

            if (sw.Elapsed - lastChange > TimeSpan.FromMinutes(3))
                Assert.Fail($"[{bgName}:Enter] STALE — stuck at hydrated={count}/{expected}, raw={rawCount}");

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"[{bgName}:Enter] TIMEOUT — only hydrated={lastCount}/{expected}, raw={lastRawCount}");
    }

    internal static int CountBotsOnMap(IReadOnlyList<WoWActivitySnapshot> snapshots, uint mapId)
    {
        return snapshots.Count(snapshot =>
        {
            var nestedMapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            return snapshot.CurrentMapId == mapId || nestedMapId == mapId;
        });
    }

    internal static int CountTrackedAccountsOnMap(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        uint mapId)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        return snapshots.Count(snapshot =>
        {
            if (string.IsNullOrWhiteSpace(snapshot.AccountName) || !accountSet.Contains(snapshot.AccountName))
                return false;

            var nestedMapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            return snapshot.CurrentMapId == mapId || nestedMapId == mapId;
        });
    }

    internal static IReadOnlyList<string> DescribeTrackedAccountsOffMap(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        uint mapId)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .Where(snapshot => accountSet.Contains(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var offMapAccounts = new List<string>(accountSet.Count);

        foreach (var account in accountSet.OrderBy(account => account, StringComparer.OrdinalIgnoreCase))
        {
            if (!snapshotLookup.TryGetValue(account, out var snapshot))
            {
                offMapAccounts.Add($"{account}(missing)");
                continue;
            }

            var nestedMapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            if (snapshot.CurrentMapId == mapId || nestedMapId == mapId)
                continue;

            var snapshotMapId = nestedMapId != 0 ? nestedMapId : snapshot.CurrentMapId;
            offMapAccounts.Add(
                $"{account}(screen={snapshot.ScreenState}, map={snapshotMapId}, current={snapshot.CurrentMapId}, objMgr={snapshot.IsObjectManagerValid})");
        }

        return offMapAccounts;
    }

    internal static IReadOnlyList<string> FindTrackedChatMatches(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        Func<string, bool> predicate)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        return snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .Where(snapshot => accountSet.Contains(snapshot.AccountName))
            .SelectMany(snapshot =>
                snapshot.RecentChatMessages
                    .Where(predicate)
                    .Select(message => $"{snapshot.AccountName}: {message}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(message => message, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyDictionary<string, int> CaptureTrackedBagItemCounts(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        uint itemId)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .Where(snapshot => accountSet.Contains(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in accountSet.OrderBy(account => account, StringComparer.OrdinalIgnoreCase))
        {
            snapshotLookup.TryGetValue(account, out var snapshot);
            counts[account] = snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;
        }

        return counts;
    }

    internal static IReadOnlyDictionary<string, int> CaptureTrackedChatMatchCounts(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        Func<string, bool> predicate)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .Where(snapshot => accountSet.Contains(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in accountSet.OrderBy(account => account, StringComparer.OrdinalIgnoreCase))
        {
            snapshotLookup.TryGetValue(account, out var snapshot);
            counts[account] = snapshot?.RecentChatMessages.Count(predicate) ?? 0;
        }

        return counts;
    }

    internal static IReadOnlyList<string> FindAccountsWithBagItemIncrease(
        IReadOnlyDictionary<string, int> baselineCounts,
        IReadOnlyDictionary<string, int> currentCounts)
    {
        return baselineCounts.Keys
            .Where(account =>
                currentCounts.TryGetValue(account, out var currentCount)
                && currentCount > baselineCounts[account])
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<string> FindAccountsWithChatMatchIncrease(
        IReadOnlyDictionary<string, int> baselineCounts,
        IReadOnlyDictionary<string, int> currentCounts)
    {
        return baselineCounts.Keys
            .Where(account =>
                currentCounts.TryGetValue(account, out var currentCount)
                && currentCount > baselineCounts[account])
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static int SumTrackedBagItemCounts(IReadOnlyDictionary<string, int> itemCounts)
        => itemCounts.Values.Sum();

    internal static int SumTrackedChatMatchCounts(IReadOnlyDictionary<string, int> chatCounts)
        => chatCounts.Values.Sum();

    private static void WriteProgress(ITestOutputHelper output, string message)
    {
        output.WriteLine(message);
        Console.WriteLine(message);
    }

    private static async Task<IReadOnlyList<WoWActivitySnapshot>> QueryTrackedSnapshotsAsync(
        LiveBotFixture bot,
        IReadOnlyCollection<string> trackedAccounts)
    {
        var snapshots = new List<WoWActivitySnapshot>(trackedAccounts.Count);

        foreach (var account in trackedAccounts.OrderBy(account => account, StringComparer.OrdinalIgnoreCase))
        {
            var snapshot = await bot.GetSnapshotAsync(account);
            if (snapshot != null)
                snapshots.Add(snapshot);
        }

        return snapshots;
    }

    internal static global::Game.GameObjectSnapshot? FindNearestNearbyGameObject(
        WoWActivitySnapshot? snapshot,
        Func<global::Game.GameObjectSnapshot, bool> predicate)
    {
        return snapshot?.MovementData?.NearbyGameObjects?
            .Where(predicate)
            .OrderBy(gameObject => gameObject.DistanceToPlayer)
            .FirstOrDefault();
    }

    public static async Task<global::Game.GameObjectSnapshot> WaitForNearbyGameObjectAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        string account,
        uint expectedMapId,
        Func<global::Game.GameObjectSnapshot, bool> predicate,
        string phaseName,
        TimeSpan? maxTimeout = null)
    {
        var effectiveTimeout = maxTimeout ?? TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();
        var lastDiag = string.Empty;

        while (sw.Elapsed < effectiveTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            await bot.RefreshSnapshotsAsync();
            var snapshot = await bot.GetSnapshotAsync(account);
            var mapId = snapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot?.CurrentMapId ?? 0;
            var match = FindNearestNearbyGameObject(snapshot, predicate);
            if (mapId == expectedMapId && match != null)
            {
                output.WriteLine(
                    $"[{phaseName}] found entry={match.Entry} guid=0x{match.Guid:X} name='{match.Name}' dist={match.DistanceToPlayer:F1} at {sw.Elapsed.TotalSeconds:F0}s");
                return match;
            }

            var nearby = snapshot?.MovementData?.NearbyGameObjects?
                .Take(8)
                .Select(gameObject => $"{gameObject.Entry}:{gameObject.Name}@{gameObject.DistanceToPlayer:F1}")
                .ToArray()
                ?? Array.Empty<string>();
            var diag = $"map={mapId}, nearby=[{string.Join(", ", nearby)}]";
            if (!string.Equals(diag, lastDiag, StringComparison.Ordinal))
            {
                output.WriteLine($"[{phaseName}] {diag} at {sw.Elapsed.TotalSeconds:F0}s");
                lastDiag = diag;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Fail($"[{phaseName}] TIMEOUT - no matching nearby gameobject for account {account}");
        return null!;
    }

    public static async Task WaitForNearbyGameObjectAbsentAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        string account,
        uint expectedMapId,
        Func<global::Game.GameObjectSnapshot, bool> predicate,
        string phaseName,
        TimeSpan? maxTimeout = null)
    {
        var effectiveTimeout = maxTimeout ?? TimeSpan.FromSeconds(20);
        var sw = Stopwatch.StartNew();
        var lastDiag = string.Empty;

        while (sw.Elapsed < effectiveTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            await bot.RefreshSnapshotsAsync();
            var snapshot = await bot.GetSnapshotAsync(account);
            var mapId = snapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot?.CurrentMapId ?? 0;
            var match = FindNearestNearbyGameObject(snapshot, predicate);
            if (mapId == expectedMapId && match == null)
            {
                output.WriteLine($"[{phaseName}] matching nearby gameobject is absent at {sw.Elapsed.TotalSeconds:F0}s");
                return;
            }

            var nearby = snapshot?.MovementData?.NearbyGameObjects?
                .Take(8)
                .Select(gameObject => $"{gameObject.Entry}:{gameObject.Name}@{gameObject.DistanceToPlayer:F1}")
                .ToArray()
                ?? Array.Empty<string>();
            var diag = $"map={mapId}, nearby=[{string.Join(", ", nearby)}]";
            if (!string.Equals(diag, lastDiag, StringComparison.Ordinal))
            {
                output.WriteLine($"[{phaseName}] {diag} at {sw.Elapsed.TotalSeconds:F0}s");
                lastDiag = diag;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Fail($"[{phaseName}] TIMEOUT - matching nearby gameobject never disappeared for account {account}");
    }

    public static Task WaitForAccountNearTargetAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        string account,
        AlteracValleyLoadoutPlan.ObjectiveTarget target,
        string phaseName,
        float maxDistance,
        bool redispatchLaggingGoto = false,
        float redispatchStopDistance = 12f,
        TimeSpan? redispatchInterval = null,
        Func<IReadOnlyList<WoWActivitySnapshot>, string, AlteracValleyLoadoutPlan.ObjectiveTarget, AlteracValleyLoadoutPlan.ObjectiveTarget>? redispatchTargetFactory = null,
        TimeSpan? maxTimeout = null,
        TimeSpan? staleTimeout = null)
    {
        return WaitForAccountsNearTargetsAsync(
            bot,
            output,
            [account],
            new Dictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget>(StringComparer.OrdinalIgnoreCase)
            {
                [account] = target
            },
            leaderAccount: null,
            minReached: 1,
            minGroupedToLeader: 0,
            phaseName: phaseName,
            maxDistance: maxDistance,
            redispatchLaggingGoto: redispatchLaggingGoto,
            redispatchStopDistance: redispatchStopDistance,
            redispatchInterval: redispatchInterval,
            redispatchTargetFactory: redispatchTargetFactory,
            maxTimeout: maxTimeout,
            staleTimeout: staleTimeout);
    }

    public static async Task<IReadOnlyDictionary<string, int>> WaitForTrackedBagItemIncreaseAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        IReadOnlyDictionary<string, int> baselineCounts,
        uint itemId,
        string phaseName,
        int minimumAccountsWithIncrease = 1,
        bool queryTrackedAccountsIndividually = false,
        TimeSpan? maxTimeout = null)
    {
        var effectiveTimeout = maxTimeout ?? TimeSpan.FromMinutes(2);
        var trackedAccounts = baselineCounts.Keys.ToArray();
        var baselineTotal = SumTrackedBagItemCounts(baselineCounts);
        var sw = Stopwatch.StartNew();
        var lastSummary = string.Empty;

        while (sw.Elapsed < effectiveTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = queryTrackedAccountsIndividually
                ? await QueryTrackedSnapshotsAsync(bot, trackedAccounts)
                : await bot.QueryAllSnapshotsAsync();
            var currentCounts = CaptureTrackedBagItemCounts(snapshots, trackedAccounts, itemId);
            var increasedAccounts = FindAccountsWithBagItemIncrease(baselineCounts, currentCounts);
            var currentTotal = SumTrackedBagItemCounts(currentCounts);

            if (increasedAccounts.Count >= minimumAccountsWithIncrease && currentTotal > baselineTotal)
            {
                WriteProgress(output,
                    $"[{phaseName}] reward item={itemId} increased for {increasedAccounts.Count}/{trackedAccounts.Length} tracked accounts at {sw.Elapsed.TotalSeconds:F0}s");
                WriteProgress(output,
                    $"[{phaseName}] totalMarks {baselineTotal}->{currentTotal}; rewarded={string.Join(", ", increasedAccounts.Take(10))}");
                return currentCounts;
            }

            var summary =
                $"rewarded={increasedAccounts.Count}/{trackedAccounts.Length}, total={baselineTotal}->{currentTotal}";
            if (!string.Equals(summary, lastSummary, StringComparison.Ordinal))
            {
                WriteProgress(output, $"[{phaseName}] {summary} at {sw.Elapsed.TotalSeconds:F0}s");
                lastSummary = summary;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        var finalSnapshots = queryTrackedAccountsIndividually
            ? await QueryTrackedSnapshotsAsync(bot, trackedAccounts)
            : await bot.QueryAllSnapshotsAsync();
        var finalCounts = CaptureTrackedBagItemCounts(finalSnapshots, trackedAccounts, itemId);
        var finalRewardedAccounts = FindAccountsWithBagItemIncrease(baselineCounts, finalCounts);
        var finalTotal = SumTrackedBagItemCounts(finalCounts);
        Assert.Fail(
            $"[{phaseName}] TIMEOUT - reward item={itemId}, rewarded={finalRewardedAccounts.Count}/{trackedAccounts.Length}, total={baselineTotal}->{finalTotal}");
        return finalCounts;
    }

    public static async Task<IReadOnlyDictionary<string, int>> WaitForTrackedChatIncreaseAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        IReadOnlyDictionary<string, int> baselineCounts,
        IReadOnlyCollection<string> trackedAccounts,
        Func<string, bool> predicate,
        string phaseName,
        int minimumAccountsWithIncrease = 1,
        TimeSpan? maxTimeout = null)
    {
        var effectiveTimeout = maxTimeout ?? TimeSpan.FromSeconds(30);
        var baselineTotal = SumTrackedChatMatchCounts(baselineCounts);
        var trackedArray = trackedAccounts.ToArray();
        var sw = Stopwatch.StartNew();
        var lastLoggedAt = TimeSpan.MinValue;

        while (sw.Elapsed < effectiveTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = await bot.QueryAllSnapshotsAsync();
            var currentCounts = CaptureTrackedChatMatchCounts(snapshots, trackedArray, predicate);
            var increasedAccounts = FindAccountsWithChatMatchIncrease(baselineCounts, currentCounts);
            var currentTotal = SumTrackedChatMatchCounts(currentCounts);

            if (increasedAccounts.Count >= minimumAccountsWithIncrease)
            {
                WriteProgress(output,
                    $"[{phaseName}] chat increase observed on {increasedAccounts.Count}/{trackedArray.Length} accounts " +
                    $"at {sw.Elapsed.TotalSeconds:F0}s; total={baselineTotal}->{currentTotal}");
                if (increasedAccounts.Count > 0)
                    WriteProgress(output, $"[{phaseName}] accounts={string.Join(", ", increasedAccounts.Take(6))}");
                return currentCounts;
            }

            if (lastLoggedAt == TimeSpan.MinValue || sw.Elapsed - lastLoggedAt >= TimeSpan.FromSeconds(5))
            {
                WriteProgress(output,
                    $"[{phaseName}] waiting for tracked chat increase: " +
                    $"accounts={increasedAccounts.Count}/{minimumAccountsWithIncrease}, total={baselineTotal}->{currentTotal} " +
                    $"at {sw.Elapsed.TotalSeconds:F0}s");
                lastLoggedAt = sw.Elapsed;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        var finalSnapshots = await bot.QueryAllSnapshotsAsync();
        var finalCounts = CaptureTrackedChatMatchCounts(finalSnapshots, trackedArray, predicate);
        var finalIncreasedAccounts = FindAccountsWithChatMatchIncrease(baselineCounts, finalCounts);
        var finalTotal = SumTrackedChatMatchCounts(finalCounts);
        Assert.Fail(
            $"[{phaseName}] TIMEOUT - tracked chat increase accounts={finalIncreasedAccounts.Count}/{minimumAccountsWithIncrease}, " +
            $"total={baselineTotal}->{finalTotal}");
        return finalCounts;
    }

    public static async Task WaitForBattlegroundCompletionAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        IReadOnlyCollection<string> trackedAccounts,
        uint bgMapId,
        string phaseName,
        bool queryTrackedAccountsIndividually = false,
        TimeSpan? maxTimeout = null,
        Func<string, bool>? completionChatPredicate = null)
    {
        var effectiveTimeout = maxTimeout ?? TimeSpan.FromMinutes(15);
        var sw = Stopwatch.StartNew();
        var lastLoggedAt = TimeSpan.MinValue;
        var sawTrackedBotOnMap = false;

        while (sw.Elapsed < effectiveTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = queryTrackedAccountsIndividually
                ? await QueryTrackedSnapshotsAsync(bot, trackedAccounts)
                : await bot.QueryAllSnapshotsAsync();
            var onBg = CountTrackedAccountsOnMap(snapshots, trackedAccounts, bgMapId);
            sawTrackedBotOnMap |= onBg > 0;

            var completionChats = completionChatPredicate == null
                ? Array.Empty<string>()
                : FindTrackedChatMatches(snapshots, trackedAccounts, completionChatPredicate).ToArray();

            if (sawTrackedBotOnMap && onBg == 0)
            {
                WriteProgress(output,
                    $"[{phaseName}] battleground complete at {sw.Elapsed.TotalSeconds:F0}s; completionChats={completionChats.Length}");
                if (completionChats.Length > 0)
                    WriteProgress(output, $"[{phaseName}] chats={string.Join(" || ", completionChats.Take(6))}");
                return;
            }

            if (lastLoggedAt == TimeSpan.MinValue || sw.Elapsed - lastLoggedAt >= TimeSpan.FromSeconds(30))
            {
                WriteProgress(output,
                    $"[{phaseName}] onBg={onBg}/{trackedAccounts.Count}, completionChats={completionChats.Length} at {sw.Elapsed.TotalSeconds:F0}s");
                if (completionChats.Length > 0)
                    WriteProgress(output, $"[{phaseName}] chats={string.Join(" || ", completionChats.Take(6))}");
                lastLoggedAt = sw.Elapsed;
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        var finalSnapshots = queryTrackedAccountsIndividually
            ? await QueryTrackedSnapshotsAsync(bot, trackedAccounts)
            : await bot.QueryAllSnapshotsAsync();
        var finalOnBg = CountTrackedAccountsOnMap(finalSnapshots, trackedAccounts, bgMapId);
        var finalChats = completionChatPredicate == null
            ? Array.Empty<string>()
            : FindTrackedChatMatches(finalSnapshots, trackedAccounts, completionChatPredicate).ToArray();
        Assert.Fail(
            $"[{phaseName}] TIMEOUT - still onBg={finalOnBg}/{trackedAccounts.Count}, completionChats={finalChats.Length}");
    }

    public static async Task WaitForBgEntryAsync(LiveBotFixture bot, ITestOutputHelper output, uint bgMapId, int expectedOnMap, string bgName)
    {
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastChange = sw.Elapsed;

        while (sw.Elapsed < TimeSpan.FromMinutes(15))
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{bgName}:BG] CRASHED");

            var snapshots = await bot.QueryAllSnapshotsAsync(logDiagnostics: true);
            var onBg = CountBotsOnMap(snapshots, bgMapId);
            var grouped = snapshots.Count(s => s.PartyLeaderGuid != 0);
            var fingerprint = $"bg={onBg},grp={grouped},all={snapshots.Count}";

            if (onBg >= expectedOnMap)
            {
                output.WriteLine($"[{bgName}:BG] {onBg}/{expectedOnMap} bots on BG map at {sw.Elapsed.TotalSeconds:F0}s");
                var offBgAccounts = snapshots
                    .Where(snapshot => (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId) != bgMapId)
                    .Select(snapshot =>
                    {
                        var accountName = string.IsNullOrWhiteSpace(snapshot.AccountName) ? "(blank)" : snapshot.AccountName;
                        var snapshotMapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                        return $"{accountName}(screen={snapshot.ScreenState}, map={snapshotMapId}, current={snapshot.CurrentMapId}, objMgr={snapshot.IsObjectManagerValid})";
                    })
                    .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (offBgAccounts.Length > 0)
                    output.WriteLine($"[{bgName}:BG] offBgAtSuccess={string.Join(", ", offBgAccounts)}");
                Assert.True(onBg >= expectedOnMap, $"Expected at least {expectedOnMap} bots on map, got {onBg}");
                return;
            }

            if (fingerprint != lastFingerprint)
            {
                output.WriteLine($"[{bgName}:BG] {fingerprint} at {sw.Elapsed.TotalSeconds:F0}s");
                var onBgAccounts = snapshots
                    .Where(snapshot => (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId) == bgMapId)
                    .Select(snapshot => snapshot.AccountName)
                    .Where(accountName => !string.IsNullOrWhiteSpace(accountName))
                    .OrderBy(accountName => accountName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var offBgAccounts = snapshots
                    .Where(snapshot => (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId) != bgMapId)
                    .Select(snapshot =>
                    {
                        var accountName = string.IsNullOrWhiteSpace(snapshot.AccountName) ? "(blank)" : snapshot.AccountName;
                        var snapshotMapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                        return $"{accountName}(screen={snapshot.ScreenState}, map={snapshotMapId}, current={snapshot.CurrentMapId}, objMgr={snapshot.IsObjectManagerValid})";
                    })
                    .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                output.WriteLine($"[{bgName}:BG] onBg={string.Join(", ", onBgAccounts)}");
                output.WriteLine($"[{bgName}:BG] offBg={string.Join(", ", offBgAccounts)}");
                lastFingerprint = fingerprint;
                lastChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastChange > TimeSpan.FromMinutes(2))
                Assert.Fail($"[{bgName}:BG] STALE — {fingerprint}");

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        Assert.Fail($"[{bgName}:BG] TIMEOUT");
    }

    public static async Task<int> WaitForBgEntrySettlingAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        uint bgMapId,
        int targetOnMap,
        string bgName,
        TimeSpan settleWindow)
    {
        var sw = Stopwatch.StartNew();
        var bestOnBg = 0;
        var lastFingerprint = "";

        while (sw.Elapsed < settleWindow)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{bgName}:BG-SETTLE] CRASHED");

            var snapshots = await bot.QueryAllSnapshotsAsync();
            var onBg = CountBotsOnMap(snapshots, bgMapId);
            if (onBg > bestOnBg)
            {
                bestOnBg = onBg;
                output.WriteLine($"[{bgName}:BG-SETTLE] bestOnBg={bestOnBg} at {sw.Elapsed.TotalSeconds:F0}s");
            }

            var offCount = Math.Max(0, snapshots.Count - onBg);
            var fingerprint = $"bg={onBg},off={offCount}";
            if (fingerprint != lastFingerprint)
            {
                output.WriteLine($"[{bgName}:BG-SETTLE] {fingerprint} at {sw.Elapsed.TotalSeconds:F0}s");
                lastFingerprint = fingerprint;
            }

            if (onBg >= targetOnMap)
            {
                output.WriteLine($"[{bgName}:BG-SETTLE] reached targetOnMap={targetOnMap} at {sw.Elapsed.TotalSeconds:F0}s");
                return onBg;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        output.WriteLine(
            $"[{bgName}:BG-SETTLE] settle window elapsed ({settleWindow.TotalSeconds:F0}s), bestOnBg={bestOnBg}, targetOnMap={targetOnMap}");
        return bestOnBg;
    }

    public static async Task WaitForTrackedAccountsOnMapStableAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        IReadOnlyCollection<string> trackedAccounts,
        uint bgMapId,
        string phaseName,
        TimeSpan stableWindow,
        TimeSpan? maxTimeout = null,
        bool queryTrackedAccountsIndividually = false)
    {
        var trackedArray = trackedAccounts
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var effectiveTimeout = maxTimeout ?? TimeSpan.FromMinutes(5);
        var sw = Stopwatch.StartNew();
        var stableSince = TimeSpan.MinValue;
        var lastFingerprint = string.Empty;
        var lastStableCountdownBucket = int.MinValue;

        while (sw.Elapsed < effectiveTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = queryTrackedAccountsIndividually
                ? await QueryTrackedSnapshotsAsync(bot, trackedArray)
                : await bot.QueryAllSnapshotsAsync();
            var onBg = CountTrackedAccountsOnMap(snapshots, trackedArray, bgMapId);
            var offBgAccounts = DescribeTrackedAccountsOffMap(snapshots, trackedArray, bgMapId);
            var fingerprint = $"onBg={onBg}/{trackedArray.Length}, off={offBgAccounts.Count}";

            if (!string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
            {
                WriteProgress(output, $"[{phaseName}] {fingerprint} at {sw.Elapsed.TotalSeconds:F0}s");
                if (offBgAccounts.Count > 0)
                    WriteProgress(output, $"[{phaseName}] offBg={string.Join(", ", offBgAccounts.Take(10))}");
                lastFingerprint = fingerprint;
            }

            if (offBgAccounts.Count == 0 && onBg == trackedArray.Length)
            {
                if (stableSince == TimeSpan.MinValue)
                {
                    stableSince = sw.Elapsed;
                    lastStableCountdownBucket = int.MinValue;
                    WriteProgress(
                        output,
                        $"[{phaseName}] all tracked accounts reached map {bgMapId} at {sw.Elapsed.TotalSeconds:F0}s; waiting {stableWindow.TotalSeconds:F0}s for stability");
                }

                var stableElapsed = sw.Elapsed - stableSince;
                if (stableElapsed >= stableWindow)
                {
                    WriteProgress(
                        output,
                        $"[{phaseName}] stable on map {bgMapId} for {stableElapsed.TotalSeconds:F0}s");
                    return;
                }

                var remainingSeconds = Math.Max(0, (int)Math.Ceiling((stableWindow - stableElapsed).TotalSeconds));
                var bucket = remainingSeconds / 5;
                if (bucket != lastStableCountdownBucket)
                {
                    WriteProgress(output, $"[{phaseName}] stable countdown {remainingSeconds}s remaining");
                    lastStableCountdownBucket = bucket;
                }
            }
            else if (stableSince != TimeSpan.MinValue)
            {
                WriteProgress(
                    output,
                    $"[{phaseName}] stability reset after {(sw.Elapsed - stableSince).TotalSeconds:F0}s");
                stableSince = TimeSpan.MinValue;
                lastStableCountdownBucket = int.MinValue;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        var finalSnapshots = queryTrackedAccountsIndividually
            ? await QueryTrackedSnapshotsAsync(bot, trackedArray)
            : await bot.QueryAllSnapshotsAsync();
        var finalOnBg = CountTrackedAccountsOnMap(finalSnapshots, trackedArray, bgMapId);
        var finalOffBg = DescribeTrackedAccountsOffMap(finalSnapshots, trackedArray, bgMapId);
        Assert.Fail(
            $"[{phaseName}] TIMEOUT - onBg={finalOnBg}/{trackedArray.Length}, offBg={string.Join(", ", finalOffBg.Take(10))}");
    }

    public static async Task WaitForAccountsMountedAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        IEnumerable<string> accounts,
        int expectedMounted,
        string phaseName)
    {
        var trackedAccounts = accounts.ToArray();
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastChange = sw.Elapsed;

        while (sw.Elapsed < TimeSpan.FromMinutes(2))
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = await bot.QueryAllSnapshotsAsync();
            var mounted = CountMountedAccounts(snapshots, trackedAccounts);
            var mountedAccounts = snapshots
                .Where(snapshot => trackedAccounts.Contains(snapshot.AccountName, StringComparer.OrdinalIgnoreCase))
                .Where(IsMounted)
                .Select(snapshot => snapshot.AccountName)
                .Where(accountName => !string.IsNullOrWhiteSpace(accountName))
                .OrderBy(accountName => accountName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var fingerprint = $"{mounted}/{expectedMounted}";

            if (mounted >= expectedMounted)
            {
                output.WriteLine($"[{phaseName}] mounted={mounted}/{expectedMounted} at {sw.Elapsed.TotalSeconds:F0}s");
                return;
            }

            if (fingerprint != lastFingerprint)
            {
                var unmounted = trackedAccounts
                    .Except(mountedAccounts, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(accountName => accountName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                output.WriteLine($"[{phaseName}] mounted={mounted}/{expectedMounted} at {sw.Elapsed.TotalSeconds:F0}s");
                output.WriteLine($"[{phaseName}] unmounted={string.Join(", ", unmounted.Take(12))}");
                lastFingerprint = fingerprint;
                lastChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastChange > TimeSpan.FromSeconds(45))
                Assert.Fail($"[{phaseName}] STALE - mounted={mounted}/{expectedMounted}");

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"[{phaseName}] TIMEOUT - failed to mount {expectedMounted} accounts");
    }

    public static async Task WaitForAccountsNearTargetsAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        IReadOnlyCollection<string> accounts,
        IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget> targets,
        string? leaderAccount,
        int minReached,
        int minGroupedToLeader,
        string phaseName,
        float maxDistance = 40f,
        bool redispatchLaggingGoto = false,
        float redispatchStopDistance = 12f,
        TimeSpan? redispatchInterval = null,
        Func<IReadOnlyList<WoWActivitySnapshot>, string, AlteracValleyLoadoutPlan.ObjectiveTarget, AlteracValleyLoadoutPlan.ObjectiveTarget>? redispatchTargetFactory = null,
        TimeSpan? maxTimeout = null,
        TimeSpan? staleTimeout = null)
    {
        var trackedAccounts = accounts.OrderBy(account => account, StringComparer.OrdinalIgnoreCase).ToArray();
        var effectiveMaxTimeout = maxTimeout ?? TimeSpan.FromMinutes(4);
        var effectiveStaleTimeout = staleTimeout ?? TimeSpan.FromMinutes(1);
        var effectiveRedispatchInterval = redispatchInterval ?? TimeSpan.FromSeconds(20);
        var sw = Stopwatch.StartNew();
        var lastFingerprint = "";
        var lastChange = sw.Elapsed;
        var lastRedispatch = TimeSpan.Zero;

        while (sw.Elapsed < effectiveMaxTimeout)
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            var snapshots = await bot.QueryAllSnapshotsAsync();
            var reached = CountAccountsNearTargets(snapshots, trackedAccounts, targets, maxDistance: maxDistance);
            var grouped = leaderAccount != null ? CountAccountsGroupedToLeader(snapshots, trackedAccounts, leaderAccount) : 0;
            var leaderNear = leaderAccount != null && IsAccountNearAssignedTarget(snapshots, leaderAccount, targets, maxDistance: maxDistance);
            var onMap = trackedAccounts.Count(account =>
                targets.TryGetValue(account, out var target)
                && IsAccountOnMap(snapshots, account, target.MapId));
            var laggingAccounts = trackedAccounts
                .Where(account => !IsAccountNearAssignedTarget(snapshots, account, targets, maxDistance: maxDistance))
                .ToArray();
            var singleAccountProgressFingerprint = trackedAccounts.Length == 1
                ? BuildSingleAccountProgressFingerprint(snapshots, trackedAccounts[0], targets[trackedAccounts[0]])
                : string.Empty;
            var fingerprint = trackedAccounts.Length == 1
                ? $"near={reached},grouped={grouped},leaderNear={leaderNear},map={onMap},progress={singleAccountProgressFingerprint}"
                : $"near={reached},grouped={grouped},leaderNear={leaderNear},map={onMap}";

            if (reached >= minReached && grouped >= minGroupedToLeader && (leaderNear || leaderAccount == null))
            {
                output.WriteLine($"[{phaseName}] {fingerprint} at {sw.Elapsed.TotalSeconds:F0}s");
                return;
            }

            if (redispatchLaggingGoto && sw.Elapsed - lastRedispatch >= effectiveRedispatchInterval)
            {
                var redispatched = 0;
                var adaptiveTargets = 0;
                foreach (var account in laggingAccounts)
                {
                    if (!IsAccountOnMap(snapshots, account, targets[account].MapId))
                        continue;

                    var baseTarget = targets[account];
                    var target = redispatchTargetFactory?.Invoke(snapshots, account, baseTarget) ?? baseTarget;
                    if (!AreTargetsEquivalent(target, baseTarget))
                        adaptiveTargets++;

                    var dispatch = await bot.SendActionAsync(account, MakeGoto(target.X, target.Y, target.Z, stopDistance: redispatchStopDistance));
                    if (dispatch == ResponseResult.Success)
                        redispatched++;
                }

                output.WriteLine(
                    $"[{phaseName}] redispatch goto for {redispatched} lagging on-map accounts " +
                    $"(adaptiveTargets={adaptiveTargets}) at {sw.Elapsed.TotalSeconds:F0}s");
                lastRedispatch = sw.Elapsed;
            }

            if (fingerprint != lastFingerprint)
            {
                output.WriteLine($"[{phaseName}] {fingerprint} at {sw.Elapsed.TotalSeconds:F0}s");
                var lagging = laggingAccounts
                    .Select(account =>
                    {
                        var snapshot = snapshots.LastOrDefault(s => string.Equals(s.AccountName, account, StringComparison.OrdinalIgnoreCase));
                        var mapId = snapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot?.CurrentMapId ?? 0;
                        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
                        var target = targets[account];
                        var distance = position == null ? float.NaN : Distance2D(position.X, position.Y, target.X, target.Y);
                        var currentPos = position == null
                            ? "(?,?,?)"
                            : $"({position.X.ToString("F0", CultureInfo.InvariantCulture)}," +
                              $"{position.Y.ToString("F0", CultureInfo.InvariantCulture)}," +
                              $"{position.Z.ToString("F0", CultureInfo.InvariantCulture)})";
                        var targetPos =
                            $"({target.X.ToString("F0", CultureInfo.InvariantCulture)}," +
                            $"{target.Y.ToString("F0", CultureInfo.InvariantCulture)}," +
                            $"{target.Z.ToString("F0", CultureInfo.InvariantCulture)})";
                        return
                            $"{account}(map={mapId},dist={(float.IsNaN(distance) ? "?" : distance.ToString("F0", CultureInfo.InvariantCulture))}," +
                            $"pos={currentPos},target={targetPos})";
                    })
                    .Take(12)
                    .ToArray();
                output.WriteLine($"[{phaseName}] lagging={string.Join(", ", lagging)}");
                lastFingerprint = fingerprint;
                lastChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastChange > effectiveStaleTimeout)
                Assert.Fail($"[{phaseName}] STALE - {fingerprint}");

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        Assert.Fail($"[{phaseName}] TIMEOUT");
    }

    internal static AlteracValleyLoadoutPlan.ObjectiveTarget BuildIncrementalRedispatchTarget(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        string account,
        AlteracValleyLoadoutPlan.ObjectiveTarget objectiveTarget)
        => BuildAdaptiveRedispatchTarget(
            snapshots,
            account,
            objectiveTarget,
            minimumStep: 18f,
            maximumStep: 54f,
            highVerticalDeltaStepCap: 28f);

    internal static AlteracValleyLoadoutPlan.ObjectiveTarget BuildMediumRangeRedispatchTarget(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        string account,
        AlteracValleyLoadoutPlan.ObjectiveTarget objectiveTarget)
        => BuildAdaptiveRedispatchTarget(
            snapshots,
            account,
            objectiveTarget,
            minimumStep: 10f,
            maximumStep: 20f,
            highVerticalDeltaStepCap: 12f);

    internal static AlteracValleyLoadoutPlan.ObjectiveTarget BuildCloseRangeRedispatchTarget(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        string account,
        AlteracValleyLoadoutPlan.ObjectiveTarget objectiveTarget)
        => BuildAdaptiveRedispatchTarget(
            snapshots,
            account,
            objectiveTarget,
            minimumStep: 6f,
            maximumStep: 12f,
            highVerticalDeltaStepCap: 8f);

    private static AlteracValleyLoadoutPlan.ObjectiveTarget BuildAdaptiveRedispatchTarget(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        string account,
        AlteracValleyLoadoutPlan.ObjectiveTarget objectiveTarget,
        float minimumStep,
        float maximumStep,
        float highVerticalDeltaStepCap)
    {
        var snapshot = snapshots.LastOrDefault(candidate =>
            string.Equals(candidate.AccountName, account, StringComparison.OrdinalIgnoreCase));
        if (snapshot == null)
            return objectiveTarget;

        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        if (position == null)
            return objectiveTarget;

        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId != objectiveTarget.MapId)
            return objectiveTarget;

        var dx = objectiveTarget.X - position.X;
        var dy = objectiveTarget.Y - position.Y;
        var distanceToTarget = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distanceToTarget <= 1f)
            return objectiveTarget;

        var requestedStep = MathF.Min(maximumStep, distanceToTarget * 0.45f);
        var step = MathF.Min(distanceToTarget, MathF.Max(requestedStep, minimumStep));
        if (MathF.Abs(position.Z - objectiveTarget.Z) >= 24f)
            step = MathF.Min(step, highVerticalDeltaStepCap);

        if (step >= distanceToTarget - 0.25f)
            return objectiveTarget;

        var nx = dx / distanceToTarget;
        var ny = dy / distanceToTarget;
        return objectiveTarget with
        {
            X = position.X + (nx * step),
            Y = position.Y + (ny * step),
            Z = position.Z,
        };
    }

    internal static int CountMountedAccounts(IReadOnlyList<WoWActivitySnapshot> snapshots, IReadOnlyCollection<string> accounts)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        return snapshots.Count(snapshot =>
            !string.IsNullOrWhiteSpace(snapshot.AccountName)
            && accountSet.Contains(snapshot.AccountName)
            && IsMounted(snapshot));
    }

    internal static int CountAccountsNearTargets(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget> targets,
        float maxDistance)
    {
        return accounts.Count(account => IsAccountNearAssignedTarget(snapshots, account, targets, maxDistance));
    }

    internal static int CountAccountsGroupedToLeader(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts,
        string leaderAccount)
    {
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        if (!snapshotLookup.TryGetValue(leaderAccount, out var leaderSnapshot))
            return 0;

        var leaderGuid = leaderSnapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
        if (leaderGuid == 0)
            return 0;

        return accounts.Count(account =>
            snapshotLookup.TryGetValue(account, out var snapshot)
            && snapshot.PartyLeaderGuid == leaderGuid);
    }

    private static bool IsMounted(WoWActivitySnapshot snapshot)
        => (snapshot.Player?.Unit?.MountDisplayId ?? 0) != 0;

    private static IReadOnlyList<string> GetMountedAccounts(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        IReadOnlyCollection<string> accounts)
    {
        var accountSet = new HashSet<string>(accounts, StringComparer.OrdinalIgnoreCase);
        return snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .Where(snapshot => accountSet.Contains(snapshot.AccountName))
            .Where(IsMounted)
            .Select(snapshot => snapshot.AccountName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsAccountNearAssignedTarget(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        string account,
        IReadOnlyDictionary<string, AlteracValleyLoadoutPlan.ObjectiveTarget> targets,
        float maxDistance)
    {
        var snapshot = snapshots.LastOrDefault(candidate => string.Equals(candidate.AccountName, account, StringComparison.OrdinalIgnoreCase));
        if (snapshot == null || !targets.TryGetValue(account, out var target))
            return false;

        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId != target.MapId)
            return false;

        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        if (position == null)
            return false;

        return Distance2D(position.X, position.Y, target.X, target.Y) <= maxDistance;
    }

    private static bool IsAccountOnMap(IReadOnlyList<WoWActivitySnapshot> snapshots, string account, uint mapId)
    {
        var snapshot = snapshots.LastOrDefault(candidate => string.Equals(candidate.AccountName, account, StringComparison.OrdinalIgnoreCase));
        if (snapshot == null)
            return false;

        return (snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId) == mapId;
    }

    private static string BuildSingleAccountProgressFingerprint(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        string account,
        AlteracValleyLoadoutPlan.ObjectiveTarget target)
    {
        var snapshot = snapshots.LastOrDefault(candidate =>
            string.Equals(candidate.AccountName, account, StringComparison.OrdinalIgnoreCase));
        if (snapshot?.Player?.Unit?.GameObject?.Base?.Position == null)
            return "missing";

        var position = snapshot.Player.Unit.GameObject.Base.Position;
        var mapId = snapshot.Player.Unit.GameObject.Base.MapId;
        var distance = Distance2D(position.X, position.Y, target.X, target.Y);
        return
            $"{mapId}:{position.X.ToString("F0", CultureInfo.InvariantCulture)}," +
            $"{position.Y.ToString("F0", CultureInfo.InvariantCulture)}:" +
            $"{distance.ToString("F0", CultureInfo.InvariantCulture)}";
    }

    internal static float Distance2D(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool AreTargetsEquivalent(
        AlteracValleyLoadoutPlan.ObjectiveTarget left,
        AlteracValleyLoadoutPlan.ObjectiveTarget right)
    {
        return left.MapId == right.MapId
            && MathF.Abs(left.X - right.X) < 0.5f
            && MathF.Abs(left.Y - right.Y) < 0.5f
            && MathF.Abs(left.Z - right.Z) < 0.5f;
    }
}
