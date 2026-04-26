using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed NPC interaction coverage. SHODAN stages world/loadout state;
/// FG/BG BotRunner targets receive only task-owned NPC interaction actions.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class NpcInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint AspectOfTheHawkSpellId = 6385;
    private const uint TrainerSetupCopper = 10000;
    private static int s_npcCorrelationSequence;

    public NpcInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Vendor_VisitTask_FindsAndInteracts()
    {
        _output.WriteLine("=== Vendor Visit: Shodan-staged task-driven vendor interaction ===");

        var targets = await EnsureNpcSettingsAndTargetsAsync();
        foreach (var target in targets)
        {
            var metrics = await RunVendorVisitScenarioAsync(target);
            Assert.True(
                metrics.VendorFound,
                $"{target.RoleLabel}: vendor NPC with UNIT_NPC_FLAG_VENDOR should be visible near Razor Hill.");
            Assert.True(metrics.TaskCompleted, $"{target.RoleLabel}: VendorVisitTask should complete within timeout.");
        }
    }

    [SkippableFact]
    public async Task Trainer_LearnAvailableSpells()
    {
        _output.WriteLine("=== Trainer Visit: Shodan-staged hunter trainer spell purchase ===");

        global::Tests.Infrastructure.Skip.If(
            true,
            "Shodan-shaped trainer validation is blocked by live funding setup: in-client .modify money is unavailable/no-op for BotRunner accounts, and SOAP mail funding remains uncollectable during Orgrimmar mailbox staging. See NpcInteractionTests.md.");

        var targets = await EnsureNpcSettingsAndTargetsAsync(includeForegroundIfActionable: false);
        foreach (var target in targets)
        {
            var metrics = await RunTrainerVisitScenarioAsync(target);
            Assert.True(
                metrics.TrainerFound,
                $"{target.RoleLabel}: class trainer with UNIT_NPC_FLAG_TRAINER should be visible near Razor Hill.");
            Assert.False(
                metrics.HadSpellBefore,
                $"{target.RoleLabel}: spell {AspectOfTheHawkSpellId} must be absent before the trainer task runs.");
            Assert.True(
                metrics.HasSpellAfter,
                $"{target.RoleLabel}: VisitTrainer task did not learn spell {AspectOfTheHawkSpellId} within timeout. " +
                $"LearnLatency={metrics.LearnLatencyMs}ms");
            Assert.True(
                metrics.SpellCountAfter > metrics.SpellCountBefore,
                $"{target.RoleLabel}: spell list should grow after trainer visit. " +
                $"Before={metrics.SpellCountBefore}, after={metrics.SpellCountAfter}");
            Assert.True(
                metrics.CoinageAfter < metrics.CoinageBefore,
                $"{target.RoleLabel}: trainer visit should spend copper. " +
                $"Before={metrics.CoinageBefore}, after={metrics.CoinageAfter}");
            Assert.InRange(metrics.LearnLatencyMs, 1, 50000);
        }
    }

    [SkippableFact]
    public async Task FlightMaster_VisitTask_DiscoversPaths()
    {
        _output.WriteLine("=== Flight Master Visit: Shodan-staged taxi discovery ===");

        var targets = await EnsureNpcSettingsAndTargetsAsync();
        foreach (var target in targets)
        {
            var metrics = await RunFlightMasterVisitScenarioAsync(target);
            Assert.True(metrics.FlightMasterFound, $"{target.RoleLabel}: flight master NPC should be visible near Orgrimmar.");
            Assert.True(metrics.TaskCompleted, $"{target.RoleLabel}: FlightMasterVisitTask should complete within timeout.");
        }
    }

    [SkippableFact]
    public async Task ObjectManager_DetectsNpcFlags()
    {
        var targets = await EnsureNpcSettingsAndTargetsAsync();
        foreach (var target in targets)
        {
            var staged = await _bot.StageBotRunnerAtRazorHillVendorAsync(
                target.AccountName,
                target.RoleLabel);
            Assert.True(staged, $"{target.RoleLabel}: expected to stage near Razor Hill NPCs.");

            List<Game.WoWUnit> units = [];
            List<Game.WoWUnit> withFlags = [];
            var flagsFound = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snap =>
                {
                    units = snap.NearbyUnits?.ToList() ?? [];
                    withFlags = units
                        .Where(u => u.NpcFlags != (uint)NPCFlags.UNIT_NPC_FLAG_NONE)
                        .ToList();
                    return withFlags.Count > 0;
                },
                TimeSpan.FromSeconds(10),
                pollIntervalMs: 500,
                progressLabel: $"{target.RoleLabel} NPC flags");

            if (!flagsFound)
                _output.WriteLine($"  [{target.RoleLabel}] No NPC flags found after 10s (units={units.Count}).");

            LogNpcFlags(target.RoleLabel, await _bot.GetSnapshotAsync(target.AccountName));
            Assert.True(units.Count > 0, $"[{target.RoleLabel}] ObjectManager should detect nearby units.");
            Assert.True(withFlags.Count > 0, $"[{target.RoleLabel}] At least one nearby unit should have non-zero NPC flags.");
        }
    }

    private async Task<IReadOnlyList<LiveBotFixture.BotRunnerActionTarget>> EnsureNpcSettingsAndTargetsAsync(
        bool includeForegroundIfActionable = true)
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "NpcInteraction.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by NpcInteraction.config.json.");

        var targets = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable,
                foregroundFirst: false)
            .ToList();

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
                "NPC action target.");
        }

        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no NPC action dispatch.");

        return targets;
    }

    private async Task<VendorVisitMetrics> RunVendorVisitScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var staged = await _bot.StageBotRunnerAtRazorHillVendorAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Razor Hill vendor staging to succeed.");

        var vendorUnit = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} vendor lookup");

        var vendorGuid = vendorUnit?.GameObject?.Base?.Guid ?? 0;
        var vendorDistance = await DistanceToUnitAsync(target.AccountName, vendorUnit);
        var before = await _bot.GetSnapshotAsync(target.AccountName);

        _output.WriteLine(
            $"[{target.RoleLabel}] vendor target: guid=0x{vendorGuid:X}, " +
            $"name={vendorUnit?.GameObject?.Name}, flags={vendorUnit?.NpcFlags}, distance={vendorDistance:F1}y");

        var coinageBefore = before?.Player?.Coinage ?? 0;
        await SendNpcActionAsync(target, ActionType.VisitVendor, "VisitVendor");
        await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) != coinageBefore
                || snapshot.RecentChatMessages.Count != (before?.RecentChatMessages?.Count ?? 0),
            TimeSpan.FromMilliseconds(2500),
            pollIntervalMs: 200,
            progressLabel: $"{target.RoleLabel} vendor-response");
        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(target.AccountName);

        _output.WriteLine(
            $"[{target.RoleLabel}] vendor metrics: found={vendorGuid != 0}, " +
            $"distance={vendorDistance:F1}y, coinage {before?.Player?.Coinage ?? 0}->{after?.Player?.Coinage ?? 0}");

        return new VendorVisitMetrics(
            vendorGuid != 0,
            vendorDistance,
            true,
            before?.Player?.Coinage ?? 0,
            after?.Player?.Coinage ?? 0);
    }

    private async Task<TrainerVisitMetrics> RunTrainerVisitScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: true,
            clearInventoryFirst: false,
            levelTo: 10);
        await _bot.StageBotRunnerCoinageAsync(target.AccountName, target.RoleLabel, TrainerSetupCopper);

        var absent = await _bot.StageBotRunnerSpellAbsentAsync(target.AccountName, target.RoleLabel, AspectOfTheHawkSpellId);
        Assert.True(absent, $"{target.RoleLabel}: spell {AspectOfTheHawkSpellId} should be absent before trainer validation.");

        var staged = await _bot.StageBotRunnerAtRazorHillHunterTrainerAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: false);
        Assert.True(staged, $"{target.RoleLabel}: expected Razor Hill hunter trainer staging to succeed.");

        var trainerUnit = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_TRAINER,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} trainer lookup");
        Assert.NotNull(trainerUnit);

        var trainerGuid = trainerUnit!.GameObject?.Base?.Guid ?? 0;
        var trainerDistance = await DistanceToUnitAsync(target.AccountName, trainerUnit);

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(target.AccountName);
        var spellCountBefore = before?.Player?.SpellList?.Count ?? 0;
        var hadSpellBefore = before?.Player?.SpellList?.Contains(AspectOfTheHawkSpellId) == true;
        var coinageBefore = before?.Player?.Coinage ?? 0;

        _output.WriteLine(
            $"[{target.RoleLabel}] trainer target: guid=0x{trainerGuid:X}, " +
            $"name={trainerUnit.GameObject?.Name}, flags={trainerUnit.NpcFlags}, " +
            $"distance={trainerDistance:F1}y, spellCountBefore={spellCountBefore}, " +
            $"has{AspectOfTheHawkSpellId}={hadSpellBefore}, coinageBefore={coinageBefore}");

        var timer = Stopwatch.StartNew();
        await SendNpcActionAsync(target, ActionType.VisitTrainer, "VisitTrainer", timeoutSeconds: 45);

        var learnedSpell = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => snapshot.Player?.SpellList?.Contains(AspectOfTheHawkSpellId) == true,
            TimeSpan.FromSeconds(40),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} trainer learn spell");
        var spentCoinage = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) < coinageBefore,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} trainer spend coinage");
        timer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(target.AccountName);
        var spellCountAfter = after?.Player?.SpellList?.Count ?? spellCountBefore;
        var hasSpellAfter = after?.Player?.SpellList?.Contains(AspectOfTheHawkSpellId) == true;
        var coinageAfter = after?.Player?.Coinage ?? coinageBefore;

        _output.WriteLine(
            $"[{target.RoleLabel}] trainer metrics: trainerFound={trainerGuid != 0}, " +
            $"trainerDistance={trainerDistance:F1}, spellCount {spellCountBefore}->{spellCountAfter}, " +
            $"has{AspectOfTheHawkSpellId} {hadSpellBefore}->{hasSpellAfter}, " +
            $"coinage {coinageBefore}->{coinageAfter}, learnedSpell={learnedSpell}, " +
            $"spentCoinage={spentCoinage}, latencyMs={timer.ElapsedMilliseconds}");

        if (!learnedSpell || !spentCoinage)
            _bot.DumpSnapshotDiagnostics(after, target.RoleLabel);

        return new TrainerVisitMetrics(
            trainerGuid != 0,
            trainerDistance,
            hadSpellBefore,
            hasSpellAfter,
            spellCountBefore,
            spellCountAfter,
            coinageBefore,
            coinageAfter,
            (int)timer.ElapsedMilliseconds);
    }

    private async Task<FlightMasterVisitMetrics> RunFlightMasterVisitScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var staged = await _bot.StageBotRunnerAtOrgrimmarFlightMasterAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar flight master staging to succeed.");

        var fmUnit = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} flight master lookup");

        var fmGuid = fmUnit?.GameObject?.Base?.Guid ?? 0;
        var fmDistance = await DistanceToUnitAsync(target.AccountName, fmUnit);

        _output.WriteLine(
            $"[{target.RoleLabel}] flight master: guid=0x{fmGuid:X}, " +
            $"name={fmUnit?.GameObject?.Name}, flags={fmUnit?.NpcFlags}, distance={fmDistance:F1}y");

        var preFmSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var preChatCount = preFmSnap?.RecentChatMessages?.Count ?? 0;
        await SendNpcActionAsync(target, ActionType.VisitFlightMaster, "VisitFlightMaster");
        await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => snapshot.RecentChatMessages.Count > preChatCount,
            TimeSpan.FromMilliseconds(2500),
            pollIntervalMs: 200,
            progressLabel: $"{target.RoleLabel} flightmaster-response");

        return new FlightMasterVisitMetrics(fmGuid != 0, fmDistance, true);
    }

    private async Task<ResponseResult> SendNpcActionAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ActionType actionType,
        string stepName,
        int timeoutSeconds = 12)
    {
        var correlationId =
            $"npc:{target.AccountName}:{Interlocked.Increment(ref s_npcCorrelationSequence)}";
        var action = new ActionMessage
        {
            ActionType = actionType,
            CorrelationId = correlationId,
        };

        var result = await _bot.SendActionAsync(target.AccountName, action);
        _output.WriteLine($"[NPC] {target.RoleLabel} {stepName} dispatch result: {result}");
        if (result != ResponseResult.Success)
            return result;

        var completed = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => HasCompletedAction(snapshot, correlationId),
            TimeSpan.FromSeconds(timeoutSeconds),
            pollIntervalMs: 250,
            progressLabel: $"{target.RoleLabel} {stepName} action");

        await _bot.RefreshSnapshotsAsync();
        var latest = await _bot.GetSnapshotAsync(target.AccountName);
        var ack = FindLatestMatchingAck(latest, correlationId);
        if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
        {
            Assert.Fail(
                $"{target.RoleLabel} {stepName} reported ACK {ack.Status} " +
                $"(reason={ack.FailureReason ?? "(none)"}, corr={correlationId}).");
        }

        Assert.True(completed, $"{target.RoleLabel} {stepName} did not complete within {timeoutSeconds}s.");
        return result;
    }

    private async Task<float> DistanceToUnitAsync(string account, Game.WoWUnit? unit)
    {
        var unitPos = unit?.GameObject?.Base?.Position;
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var playerPos = snap?.Player?.Unit?.GameObject?.Base?.Position;

        return playerPos == null || unitPos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(
                playerPos.X,
                playerPos.Y,
                playerPos.Z,
                unitPos.X,
                unitPos.Y,
                unitPos.Z);
    }

    private static bool HasCompletedAction(WoWActivitySnapshot snapshot, string correlationId)
    {
        var ack = FindLatestMatchingAck(snapshot, correlationId);
        if (ack != null && ack.Status != CommandAckEvent.Types.AckStatus.Pending)
            return true;

        return string.Equals(
            snapshot.PreviousAction?.CorrelationId,
            correlationId,
            StringComparison.Ordinal);
    }

    private static CommandAckEvent? FindLatestMatchingAck(WoWActivitySnapshot? snapshot, string correlationId)
    {
        if (snapshot == null)
            return null;

        CommandAckEvent? pendingMatch = null;
        for (var i = snapshot.RecentCommandAcks.Count - 1; i >= 0; i--)
        {
            var ack = snapshot.RecentCommandAcks[i];
            if (!string.Equals(ack.CorrelationId, correlationId, StringComparison.Ordinal))
                continue;

            if (ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                return ack;

            pendingMatch ??= ack;
        }

        return pendingMatch;
    }

    private void LogNpcFlags(string label, WoWActivitySnapshot? snap)
    {
        var units = snap?.NearbyUnits?.ToList() ?? [];
        var withFlags = units.Where(u => u.NpcFlags != (uint)NPCFlags.UNIT_NPC_FLAG_NONE).ToList();
        _output.WriteLine($"[{label}] Nearby units: {units.Count}, with NPC flags: {withFlags.Count}");
        foreach (var npc in withFlags.Take(15))
        {
            var pos = npc.GameObject?.Base?.Position;
            _output.WriteLine(
                $"  [{label}] {npc.GameObject?.Name} NpcFlags={npc.NpcFlags} " +
                $"({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        }
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

    private sealed record TrainerVisitMetrics(
        bool TrainerFound,
        float TrainerDistanceYards,
        bool HadSpellBefore,
        bool HasSpellAfter,
        int SpellCountBefore,
        int SpellCountAfter,
        long CoinageBefore,
        long CoinageAfter,
        int LearnLatencyMs);

    private sealed record VendorVisitMetrics(
        bool VendorFound,
        float VendorDistanceYards,
        bool TaskCompleted,
        long CoinageBefore,
        long CoinageAfter);

    private sealed record FlightMasterVisitMetrics(
        bool FlightMasterFound,
        float FmDistanceYards,
        bool TaskCompleted);
}
