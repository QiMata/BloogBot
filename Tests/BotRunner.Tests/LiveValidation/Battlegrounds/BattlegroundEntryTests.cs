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
        await _bot.EnsurePreparedAsync();
        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB");
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, ArathiBasinFixture.AbMapId, ArathiBasinFixture.TotalBotCount, "AB");
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
        var lastCount = 0;
        var lastChange = sw.Elapsed;

        while (sw.Elapsed < TimeSpan.FromMinutes(5))
        {
            if (bot.ClientCrashed)
                Assert.Fail($"[{bgName}:Enter] CRASHED");

            await bot.RefreshSnapshotsAsync();
            var count = bot.AllBots.Count;

            if (count == expected)
            {
                output.WriteLine($"[{bgName}:Enter] All {count}/{expected} bots entered world at {sw.Elapsed.TotalSeconds:F0}s");
                Assert.Equal(expected, count);
                return;
            }

            if (count != lastCount)
            {
                output.WriteLine($"[{bgName}:Enter] {count}/{expected} bots at {sw.Elapsed.TotalSeconds:F0}s");
                lastCount = count;
                lastChange = sw.Elapsed;
            }

            if (sw.Elapsed - lastChange > TimeSpan.FromSeconds(60))
                Assert.Fail($"[{bgName}:Enter] STALE — stuck at {count}/{expected}");

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Assert.Fail($"[{bgName}:Enter] TIMEOUT — only {lastCount}/{expected} bots entered");
    }

    internal static int CountBotsOnMap(IReadOnlyList<WoWActivitySnapshot> snapshots, uint mapId)
    {
        return snapshots.Count(snapshot =>
        {
            var nestedMapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            return snapshot.CurrentMapId == mapId || nestedMapId == mapId;
        });
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
            var onMap = trackedAccounts.Count(account => IsAccountOnMap(snapshots, account, AlteracValleyFixture.AvMapId));
            var fingerprint = $"near={reached},grouped={grouped},leaderNear={leaderNear},map={onMap}";
            var laggingAccounts = trackedAccounts
                .Where(account => !IsAccountNearAssignedTarget(snapshots, account, targets, maxDistance: maxDistance))
                .ToArray();

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

                    var dispatch = await bot.SendActionAsync(account, MakeGoto(target.X, target.Y, target.Z, stopDistance: 12f));
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

        var requestedStep = MathF.Min(54f, distanceToTarget * 0.45f);
        var step = MathF.Min(distanceToTarget, MathF.Max(requestedStep, 18f));
        if (MathF.Abs(position.Z - objectiveTarget.Z) >= 24f)
            step = MathF.Min(step, 28f);

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
