using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Tasks.Battlegrounds;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Live Warsong Gulch objective coverage:
/// 1. prove a Horde carrier can pick up the Alliance flag and complete one capture cycle.
/// 2. script three Horde flag captures and wait for the battleground to end.
/// </summary>
public abstract class WsgObjectiveTestBase
{
    private const uint AllianceFlagEntry = 179830;
    private const uint HordeFlagEntry = 179831;
    // Read-only DB evidence:
    // - mangos.gameobject_template entry=179830 (Silverwing Flag) type=24 references spell 23335
    // - spell_template entry=23335 = "Silverwing Flag" carrier aura for the Horde flag runner
    private const uint AllianceFlagCarrierAuraSpellId = 23335;

    // Live server read-only lookup: mangos.game_tele name='WarsongGulch' on map 489.
    private const float MidfieldHoldingX = 1235.54f;
    private const float MidfieldHoldingY = 1427.10f;
    private const float MidfieldHoldingZ = 309.72f;

    private const float AllianceFlagObserverX = 1532.5f;
    private const float AllianceFlagObserverY = 1481.32f;
    private const float AllianceFlagObserverZ = 351.83f;

    // Live server read-only lookup: mangos.game_tele name='WarsongGulchHorde' on map 489.
    private const float HordeCaptureX = 930.85f;
    private const float HordeCaptureY = 1431.57f;
    private const float HordeCaptureZ = 345.54f;
    // Live server read-only lookup: mangos.areatrigger_template id=3647 build=5875 map=489
    // "Warsong Gulch, Warsong Lumber Mill - Horde Flag Capture Point"
    private const float HordeCaptureTriggerX = 918.50f;
    private const float HordeCaptureTriggerY = 1434.04f;
    private const float HordeCaptureTriggerZ = 346.05f;
    private const float HordeCaptureCrossingOvershoot = 4f;

    // The flag carrier must stay connected for the entire capture cycle. Foreground
    // bots (WSGBOT1 = Horde raid leader) are known to drop during BG map transfers
    // (packet hook instability), which causes capture scripting to fail in an
    // unrelated way. Use a Background bot so the carrier stays alive end-to-end.
    private const string HordeCarrierAccount = "WSGBOT4";
    private const string AllianceFlagObserverAccount = "WSGBOT2";
    private const string HordeFlagObserverAccount = "WSGBOT3";

    private static readonly IReadOnlyList<string> AllTrackedAccounts = Enumerable.Range(1, WarsongGulchFixture.HordeBotCount)
        .Select(index => $"WSGBOT{index}")
        .Concat(Enumerable.Range(1, WarsongGulchFixture.AllianceBotCount).Select(index => $"WSGBOTA{index}"))
        .ToArray();
    private static readonly IReadOnlyList<string> HordeTrackedAccounts = Enumerable.Range(1, WarsongGulchFixture.HordeBotCount)
        .Select(index => $"WSGBOT{index}")
        .ToArray();
    private static readonly IReadOnlyList<string> AllianceTrackedAccounts = WarsongGulchFixture.AllianceAccountsOrdered
        .ToArray();

    protected readonly WarsongGulchFixture _bot;
    protected readonly ITestOutputHelper _output;

    protected WsgObjectiveTestBase(WarsongGulchFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    protected async Task RunSingleCaptureScenarioAsync()
    {
        await PrepareAndEnterBattlegroundAsync();
        // The single-cycle proof is "pickup -> carry -> capture/reset chat", not
        // observer-side flag model visibility after the capture animation settles.
        await CaptureAllianceFlagAsHordeAsync(
            captureIndex: 1,
            waitForMatchCompletion: false,
            requireObserverRespawn: false);
    }

    protected async Task RunFullGameScenarioAsync()
    {
        await PrepareAndEnterBattlegroundAsync();
        var baselineMarkCounts = BgTestHelper.CaptureTrackedBagItemCounts(
            await _bot.QueryAllSnapshotsAsync(),
            HordeTrackedAccounts,
            BgRewardCollectionTask.WsgMarkOfHonor);

        for (var captureIndex = 1; captureIndex <= 3; captureIndex++)
        {
            await CaptureAllianceFlagAsHordeAsync(
                captureIndex,
                waitForMatchCompletion: captureIndex == 3,
                requireObserverRespawn: false);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        await BgTestHelper.WaitForBattlegroundCompletionAsync(
            _bot,
            _output,
            AllTrackedAccounts,
            WarsongGulchFixture.WsgMapId,
            phaseName: "WSG:Completion",
            maxTimeout: TimeSpan.FromMinutes(15),
            completionChatPredicate: ContainsBattlegroundResultMessage);

        var rewardCounts = await BgTestHelper.WaitForTrackedBagItemIncreaseAsync(
            _bot,
            _output,
            baselineMarkCounts,
            BgRewardCollectionTask.WsgMarkOfHonor,
            phaseName: "WSG:Rewards",
            minimumAccountsWithIncrease: 3,
            maxTimeout: TimeSpan.FromMinutes(2));
        var rewardedAccounts = BgTestHelper.FindAccountsWithBagItemIncrease(baselineMarkCounts, rewardCounts);
        Assert.Contains(HordeCarrierAccount, rewardedAccounts);
    }

    private async Task PrepareAndEnterBattlegroundAsync()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        // Fixture init (PrepareDuringInitialization=true) already waited for all bots
        // in-world, ran loadout prep, and staged factions at their battlemasters, so
        // WaitForBotsAsync / EnsureLoadoutPreparedAsync are no longer needed here.
        await _bot.ResetTrackedBattlegroundStateAsync("ObjectiveFreshStart");
        // Restage between test methods in case a prior method in the same fixture
        // collection left bots elsewhere; suppress the runtime coordinator while the
        // teleport settles to avoid group-formation churn mid-restage.
        Assert.Equal(ResponseResult.Success, await _bot.SetRuntimeCoordinatorEnabledAsync(false));
        await _bot.ReprepareAsync();
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, WarsongGulchFixture.WsgMapId, WarsongGulchFixture.TotalBotCount, "WSG");

        // Phase B: poll for the "battle has begun" chat broadcast instead of
        // blind-waiting 95s; fall back to the original wall-clock if the
        // marker is missed.
        await BgTestHelper.WaitForBattlegroundStartAsync(_bot, _output, "WSG", TimeSpan.FromSeconds(95));
        Assert.Equal(ResponseResult.Success, await _bot.SetRuntimeCoordinatorEnabledAsync(false));
        await _bot.QuiesceAccountsAsync(AllTrackedAccounts, "WSG:QuiesceAfterPrep");
    }

    private async Task CaptureAllianceFlagAsHordeAsync(
        int captureIndex,
        bool waitForMatchCompletion,
        bool requireObserverRespawn)
    {
        var phasePrefix = $"WSG:Capture{captureIndex}";
        _output.WriteLine($"[{phasePrefix}] staging carrier={HordeCarrierAccount} observer={AllianceFlagObserverAccount}");

        await StageAccountsAsync(
            AllianceTrackedAccounts,
            MidfieldHoldingX,
            MidfieldHoldingY,
            MidfieldHoldingZ,
            progressPrefix: $"{phasePrefix}:AllianceMidfield",
            xyToleranceYards: 12f,
            minimumSettledCount: AllianceTrackedAccounts.Count - 1);
        await StageAccountAsync(
            AllianceFlagObserverAccount,
            AllianceFlagObserverX,
            AllianceFlagObserverY,
            AllianceFlagObserverZ,
            progressLabel: $"{phasePrefix}:ObserverStage",
            xyToleranceYards: 8f);
        await StageAccountAsync(
            HordeFlagObserverAccount,
            HordeCaptureX,
            HordeCaptureY,
            HordeCaptureZ,
            progressLabel: $"{phasePrefix}:HordeObserverStage",
            xyToleranceYards: 8f);

        var allianceFlag = await BgTestHelper.WaitForNearbyGameObjectAsync(
            _bot,
            _output,
            AllianceFlagObserverAccount,
            WarsongGulchFixture.WsgMapId,
            gameObject => gameObject.Entry == AllianceFlagEntry,
            phaseName: $"{phasePrefix}:ObserverFindAllianceFlag",
            maxTimeout: TimeSpan.FromSeconds(30));
        _output.WriteLine(
            $"[{phasePrefix}] observer located Alliance flag guid=0x{allianceFlag.Guid:X} dist={allianceFlag.DistanceToPlayer:F1} pos=({allianceFlag.Position?.X:F1},{allianceFlag.Position?.Y:F1},{allianceFlag.Position?.Z:F1})");
        var hordeFlag = await BgTestHelper.WaitForNearbyGameObjectAsync(
            _bot,
            _output,
            HordeFlagObserverAccount,
            WarsongGulchFixture.WsgMapId,
            gameObject => gameObject.Entry == HordeFlagEntry,
            phaseName: $"{phasePrefix}:ObserverFindHordeFlag",
            maxTimeout: TimeSpan.FromSeconds(20));
        _output.WriteLine(
            $"[{phasePrefix}] horde observer located home flag guid=0x{hordeFlag.Guid:X} dist={hordeFlag.DistanceToPlayer:F1} pos=({hordeFlag.Position?.X:F1},{hordeFlag.Position?.Y:F1},{hordeFlag.Position?.Z:F1})");

        var allianceFlagPosition = allianceFlag.Position ?? throw new InvalidOperationException(
            $"[{phasePrefix}] Alliance flag snapshot missing position.");
        var hordeFlagPosition = hordeFlag.Position ?? throw new InvalidOperationException(
            $"[{phasePrefix}] Horde flag snapshot missing position.");

        await StageAccountAsync(
            HordeCarrierAccount,
            allianceFlagPosition.X,
            allianceFlagPosition.Y,
            allianceFlagPosition.Z,
            progressLabel: $"{phasePrefix}:CarrierStage",
            xyToleranceYards: 6f);

        var interactResult = await _bot.SendActionAsync(
            HordeCarrierAccount,
            new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)allianceFlag.Guid } }
            });
        Assert.Equal(ResponseResult.Success, interactResult);
        _output.WriteLine(
            $"[{phasePrefix}] {HordeCarrierAccount} picked up Alliance flag guid=0x{allianceFlag.Guid:X}");

        await BgTestHelper.WaitForNearbyGameObjectAbsentAsync(
            _bot,
            _output,
            AllianceFlagObserverAccount,
            WarsongGulchFixture.WsgMapId,
            gameObject => gameObject.Entry == AllianceFlagEntry,
            phaseName: $"{phasePrefix}:AllianceFlagRemoved",
            maxTimeout: TimeSpan.FromSeconds(15));

        Assert.True(
            await _bot.WaitForSnapshotConditionAsync(
                HordeCarrierAccount,
                snapshot => snapshot.Player?.Unit?.Auras?.Contains(AllianceFlagCarrierAuraSpellId) == true,
                TimeSpan.FromSeconds(10),
                progressLabel: $"{phasePrefix}:CarrierAuraApplied"),
            $"[{phasePrefix}] {HordeCarrierAccount} never gained Alliance flag carrier aura {AllianceFlagCarrierAuraSpellId} after pickup.");
        await LogCarrierStateAsync($"{phasePrefix}:CarrierAfterPickup", HordeCarrierAccount);
        var baselineResetChatCounts = BgTestHelper.CaptureTrackedChatMatchCounts(
            await _bot.QueryAllSnapshotsAsync(),
            AllTrackedAccounts,
            ContainsFlagsResetMessage);

        _output.WriteLine(
            $"[{phasePrefix}] {HordeCarrierAccount} carrying Alliance flag back to Horde home flag (guid=0x{hordeFlag.Guid:X}, flag=({hordeFlagPosition.X:F1},{hordeFlagPosition.Y:F1},{hordeFlagPosition.Z:F1}), trigger=({HordeCaptureTriggerX:F1},{HordeCaptureTriggerY:F1},{HordeCaptureTriggerZ:F1}))");

        var homeFlagTarget = new AlteracValleyLoadoutPlan.ObjectiveTarget(
            WarsongGulchFixture.WsgMapId,
            hordeFlagPosition.X,
            hordeFlagPosition.Y,
            hordeFlagPosition.Z);
        var captureTriggerTarget = new AlteracValleyLoadoutPlan.ObjectiveTarget(
            WarsongGulchFixture.WsgMapId,
            HordeCaptureTriggerX,
            HordeCaptureTriggerY,
            HordeCaptureTriggerZ);
        var eastApproachTarget = new AlteracValleyLoadoutPlan.ObjectiveTarget(
            WarsongGulchFixture.WsgMapId,
            HordeCaptureX,
            HordeCaptureY,
            HordeCaptureZ);
        var westCrossTarget = new AlteracValleyLoadoutPlan.ObjectiveTarget(
            WarsongGulchFixture.WsgMapId,
            hordeFlagPosition.X - HordeCaptureCrossingOvershoot,
            hordeFlagPosition.Y,
            hordeFlagPosition.Z);

        // Keep this live slice focused on objective completion. AB objective coverage
        // already stages attackers directly onto banners; WSG uses the same pattern
        // after pickup so the assertion surface is "picked up -> still carrying ->
        // returned to Horde base -> captured", not Alliance room-exit pathing.
        await StageCarrierAsync(
            homeFlagTarget,
            progressLabel: $"{phasePrefix}:CarrierHomeStage",
            stateLabel: $"{phasePrefix}:CarrierAtHomeFlag");

        var auraClearedAtTrigger = await _bot.WaitForSnapshotConditionAsync(
            HordeCarrierAccount,
            snapshot =>
            {
                var auras = snapshot.Player?.Unit?.Auras;
                return auras != null && !auras.Contains(AllianceFlagCarrierAuraSpellId);
            },
            TimeSpan.FromSeconds(3),
            progressLabel: $"{phasePrefix}:CarrierAuraClearedAtTrigger");

        if (!auraClearedAtTrigger)
        {
            auraClearedAtTrigger = await TryCrossHordeCaptureTriggerAsync(
                eastApproachTarget,
                westCrossTarget,
                stopDistance: 4f,
                phasePrefix: phasePrefix,
                attemptName: "CarrierTriggerCrossWest");
        }

        if (!auraClearedAtTrigger)
        {
            auraClearedAtTrigger = await TryCrossHordeCaptureTriggerAsync(
                eastApproachTarget,
                homeFlagTarget,
                stopDistance: 2.5f,
                phasePrefix: phasePrefix,
                attemptName: "CarrierTriggerCrossStand");
        }

        if (!auraClearedAtTrigger)
        {
            await StageCarrierAsync(
                captureTriggerTarget,
                progressLabel: $"{phasePrefix}:CarrierTriggerStage",
                stateLabel: $"{phasePrefix}:CarrierAtCaptureTrigger");

            auraClearedAtTrigger = await _bot.WaitForSnapshotConditionAsync(
                HordeCarrierAccount,
                snapshot =>
                {
                    var auras = snapshot.Player?.Unit?.Auras;
                    return auras != null && !auras.Contains(AllianceFlagCarrierAuraSpellId);
                },
                TimeSpan.FromSeconds(3),
                progressLabel: $"{phasePrefix}:CarrierAuraClearedAtTriggerStage");
        }

        if (!auraClearedAtTrigger)
        {
            await StageCarrierAsync(
                homeFlagTarget,
                progressLabel: $"{phasePrefix}:CarrierHomeRestage",
                stateLabel: $"{phasePrefix}:CarrierAtHomeFlagRestage");

            interactResult = await _bot.SendActionAsync(
                HordeCarrierAccount,
                new ActionMessage
                {
                    ActionType = ActionType.InteractWith,
                    Parameters = { new RequestParameter { LongParam = (long)hordeFlag.Guid } }
                });
            Assert.Equal(ResponseResult.Success, interactResult);
            _output.WriteLine(
                $"[{phasePrefix}] {HordeCarrierAccount} interacted with Horde home flag guid=0x{hordeFlag.Guid:X} after reaching the capture trigger");
        }

        Assert.True(
            await _bot.WaitForSnapshotConditionAsync(
                HordeCarrierAccount,
                snapshot =>
                {
                    var auras = snapshot.Player?.Unit?.Auras;
                    return auras != null && !auras.Contains(AllianceFlagCarrierAuraSpellId);
                },
                TimeSpan.FromSeconds(waitForMatchCompletion ? 20 : 15),
                progressLabel: $"{phasePrefix}:CarrierAuraCleared"),
            $"[{phasePrefix}] {HordeCarrierAccount} never cleared Alliance flag carrier aura {AllianceFlagCarrierAuraSpellId} after reaching the Horde capture trigger/home flag.");
        await LogCarrierStateAsync($"{phasePrefix}:CarrierAfterTrigger", HordeCarrierAccount);

        if (requireObserverRespawn)
        {
            await StageAccountAsync(
                AllianceFlagObserverAccount,
                AllianceFlagObserverX,
                AllianceFlagObserverY,
                AllianceFlagObserverZ,
                progressLabel: $"{phasePrefix}:ObserverRestage",
                xyToleranceYards: 8f);

            await BgTestHelper.WaitForNearbyGameObjectAsync(
                _bot,
                _output,
                AllianceFlagObserverAccount,
                WarsongGulchFixture.WsgMapId,
                gameObject => gameObject.Entry == AllianceFlagEntry,
                phaseName: $"{phasePrefix}:AllianceFlagRespawned",
                maxTimeout: TimeSpan.FromSeconds(waitForMatchCompletion ? 30 : 20));
        }
        else if (!waitForMatchCompletion)
        {
            var resetChatCounts = await BgTestHelper.WaitForTrackedChatIncreaseAsync(
                _bot,
                _output,
                baselineResetChatCounts,
                AllTrackedAccounts,
                ContainsFlagsResetMessage,
                phaseName: $"{phasePrefix}:FlagsReset",
                minimumAccountsWithIncrease: 1,
                maxTimeout: TimeSpan.FromSeconds(30));
            var resetAccounts = BgTestHelper.FindAccountsWithChatMatchIncrease(baselineResetChatCounts, resetChatCounts);
            if (resetAccounts.Count > 0)
                _output.WriteLine($"[{phasePrefix}] resetChats={string.Join(", ", resetAccounts.Take(6))}");
        }

        var captureChats = (await _bot.QueryAllSnapshotsAsync())
            .Where(snapshot => AllTrackedAccounts.Contains(snapshot.AccountName, StringComparer.OrdinalIgnoreCase))
            .SelectMany(snapshot => snapshot.RecentChatMessages.Select(message => $"{snapshot.AccountName}: {message}"))
            .Where(message =>
                message.Contains("captured the", StringComparison.OrdinalIgnoreCase)
                || message.Contains("flag was picked up", StringComparison.OrdinalIgnoreCase)
                || message.Contains("flag returned", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        if (captureChats.Length > 0)
            _output.WriteLine($"[{phasePrefix}] chats={string.Join(" || ", captureChats)}");
    }

    private static bool ContainsBattlegroundResultMessage(string message)
    {
        return message.Contains("wins", StringComparison.OrdinalIgnoreCase)
            || message.Contains("victory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("defeat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsFlagsResetMessage(string message)
        => message.Contains("flags are now placed at their bases", StringComparison.OrdinalIgnoreCase);

    private async Task StageAccountsAsync(
        IReadOnlyCollection<string> accounts,
        float x,
        float y,
        float z,
        string progressPrefix,
        float xyToleranceYards,
        int? minimumSettledCount = null)
    {
        var settledCount = 0;

        foreach (var account in accounts)
        {
            var settled = await TryStageAccountAsync(
                account,
                x,
                y,
                z,
                progressLabel: $"{progressPrefix}:{account}",
                xyToleranceYards: xyToleranceYards);
            if (settled)
                settledCount++;
        }

        var requiredSettled = minimumSettledCount ?? accounts.Count;
        Assert.True(
            settledCount >= requiredSettled,
            $"[{progressPrefix}] only {settledCount}/{accounts.Count} accounts settled at ({x:F1},{y:F1},{z:F1}); required {requiredSettled}.");
    }

    private async Task StageAccountAsync(
        string account,
        float x,
        float y,
        float z,
        string progressLabel,
        float xyToleranceYards)
    {
        Assert.True(
            await TryStageAccountAsync(account, x, y, z, progressLabel, xyToleranceYards),
            $"[{progressLabel}] {account} did not settle at ({x:F1},{y:F1},{z:F1}).");
    }

    private async Task<bool> TryStageAccountAsync(
        string account,
        float x,
        float y,
        float z,
        string progressLabel,
        float xyToleranceYards)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await _bot.BotTeleportAsync(
                account,
                (int)WarsongGulchFixture.WsgMapId,
                x,
                y,
                z);
            if (await _bot.WaitForTeleportSettledAsync(
                    account,
                    x,
                    y,
                    timeoutMs: 10000,
                    progressLabel: $"{progressLabel}:Attempt{attempt}",
                    xyToleranceYards: xyToleranceYards))
            {
                return true;
            }

            var snapshot = await _bot.GetSnapshotAsync(account);
            var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine(
                $"[{progressLabel}] attempt {attempt} did not settle; current=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) map={snapshot?.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot?.CurrentMapId ?? 0}");
        }

        return false;
    }

    private async Task LogCarrierStateAsync(string phaseName, string account)
    {
        var snapshot = await _bot.GetSnapshotAsync(account);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var auras = snapshot?.Player?.Unit?.Auras?.Take(12).ToArray() ?? Array.Empty<uint>();
        var chats = snapshot?.RecentChatMessages?.TakeLast(4).ToArray() ?? Array.Empty<string>();
        var currentAction = snapshot?.CurrentAction?.ActionType.ToString() ?? "none";
        var previousAction = snapshot?.PreviousAction?.ActionType.ToString() ?? "none";
        _output.WriteLine(
            $"[{phaseName}] pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) action={currentAction}/{previousAction} auras=[{string.Join(",", auras)}] chats={(chats.Length == 0 ? "(none)" : string.Join(" || ", chats))}");
    }

    private async Task StageCarrierAsync(
        AlteracValleyLoadoutPlan.ObjectiveTarget target,
        string progressLabel,
        string stateLabel)
    {
        await StageAccountAsync(
            HordeCarrierAccount,
            target.X,
            target.Y,
            target.Z,
            progressLabel: progressLabel,
            xyToleranceYards: 8f);
        await LogCarrierStateAsync(stateLabel, HordeCarrierAccount);
    }

    private async Task<bool> TryCrossHordeCaptureTriggerAsync(
        AlteracValleyLoadoutPlan.ObjectiveTarget stageTarget,
        AlteracValleyLoadoutPlan.ObjectiveTarget crossTarget,
        float stopDistance,
        string phasePrefix,
        string attemptName)
    {
        await StageCarrierAsync(
            stageTarget,
            progressLabel: $"{phasePrefix}:{attemptName}:Stage",
            stateLabel: $"{phasePrefix}:{attemptName}:StageState");

        var dispatchResult = await _bot.SendActionAsync(
            HordeCarrierAccount,
            BgTestHelper.MakeGoto(crossTarget.X, crossTarget.Y, crossTarget.Z, stopDistance));
        Assert.Equal(ResponseResult.Success, dispatchResult);
        _output.WriteLine(
            $"[{phasePrefix}] {HordeCarrierAccount} crossing Horde trigger via {attemptName}: " +
            $"stage=({stageTarget.X:F1},{stageTarget.Y:F1},{stageTarget.Z:F1}) -> " +
            $"target=({crossTarget.X:F1},{crossTarget.Y:F1},{crossTarget.Z:F1}) stop={stopDistance:F1}");

        var sw = Stopwatch.StartNew();
        var lastRedispatch = TimeSpan.Zero;
        var lastProgressLog = TimeSpan.FromSeconds(-5);

        while (sw.Elapsed < TimeSpan.FromSeconds(25))
        {
            var snapshots = await _bot.QueryAllSnapshotsAsync();
            var carrierSnapshot = snapshots.LastOrDefault(snapshot =>
                string.Equals(snapshot.AccountName, HordeCarrierAccount, StringComparison.OrdinalIgnoreCase));
            if (CarrierAuraCleared(carrierSnapshot))
            {
                await LogCarrierStateAsync($"{phasePrefix}:{attemptName}:AuraCleared", HordeCarrierAccount);
                return true;
            }

            var position = carrierSnapshot?.Player?.Unit?.GameObject?.Base?.Position;
            var distanceToCrossTarget = position == null
                ? float.NaN
                : Distance2D(position.X, position.Y, crossTarget.X, crossTarget.Y);
            if (!float.IsNaN(distanceToCrossTarget) && distanceToCrossTarget <= 5f)
            {
                _output.WriteLine(
                    $"[{phasePrefix}] {HordeCarrierAccount} reached {attemptName} target without clearing flag aura " +
                    $"(dist={distanceToCrossTarget:F1})");
                await LogCarrierStateAsync($"{phasePrefix}:{attemptName}:ReachedTarget", HordeCarrierAccount);
                return false;
            }

            if (sw.Elapsed - lastRedispatch >= TimeSpan.FromSeconds(6))
            {
                var adaptiveTarget = BgTestHelper.BuildCloseRangeRedispatchTarget(
                    snapshots,
                    HordeCarrierAccount,
                    crossTarget);
                dispatchResult = await _bot.SendActionAsync(
                    HordeCarrierAccount,
                    BgTestHelper.MakeGoto(adaptiveTarget.X, adaptiveTarget.Y, adaptiveTarget.Z, stopDistance));
                Assert.Equal(ResponseResult.Success, dispatchResult);
                _output.WriteLine(
                    $"[{phasePrefix}] redispatch {attemptName}: " +
                    $"target=({adaptiveTarget.X:F1},{adaptiveTarget.Y:F1},{adaptiveTarget.Z:F1})");
                lastRedispatch = sw.Elapsed;
            }

            if (sw.Elapsed - lastProgressLog >= TimeSpan.FromSeconds(5))
            {
                var positionSummary = position == null
                    ? "(?,?,?)"
                    : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";
                _output.WriteLine(
                    $"[{phasePrefix}] {attemptName} in progress at {sw.Elapsed.TotalSeconds:F0}s: " +
                    $"pos={positionSummary}, dist={(float.IsNaN(distanceToCrossTarget) ? "?" : distanceToCrossTarget.ToString("F1"))}");
                lastProgressLog = sw.Elapsed;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        await LogCarrierStateAsync($"{phasePrefix}:{attemptName}:TimedOut", HordeCarrierAccount);
        return false;
    }

    private static bool CarrierAuraCleared(WoWActivitySnapshot? snapshot)
    {
        var auras = snapshot?.Player?.Unit?.Auras;
        return auras != null && !auras.Contains(AllianceFlagCarrierAuraSpellId);
    }

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}

[Collection(WarsongGulchFlagCaptureObjectiveCollection.Name)]
public sealed class WsgFlagCaptureObjectiveTests : WsgObjectiveTestBase
{
    public WsgFlagCaptureObjectiveTests(WarsongGulchFlagCaptureObjectiveFixture bot, ITestOutputHelper output)
        : base(bot, output)
    {
    }

    [SkippableFact]
    public Task WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle()
        => RunSingleCaptureScenarioAsync();
}

[Collection(WarsongGulchFullGameObjectiveCollection.Name)]
public sealed class WsgFullGameObjectiveTests : WsgObjectiveTestBase
{
    public WsgFullGameObjectiveTests(WarsongGulchFullGameObjectiveFixture bot, ITestOutputHelper output)
        : base(bot, output)
    {
    }

    [SkippableFact]
    public Task WSG_FullGame_CompletesToVictoryOrDefeat()
        => RunFullGameScenarioAsync();
}
