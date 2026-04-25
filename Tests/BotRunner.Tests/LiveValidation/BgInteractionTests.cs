using System;
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
/// Shodan-directed BG economy/NPC interaction smoke coverage.
/// SHODAN owns world/loadout/mail staging; the BG BotRunner target receives
/// only the behavior action under test.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BgInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint WornMaceItemId = 36;
    private const uint MailboxGoType = 19;
    private const long FlightMasterSetupCopper = 50000;
    private static int s_correlationSequence;

    public BgInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Bank_DepositItem_MovesToBankSlot()
    {
        var target = await EnsureBgInteractionTargetAsync();

        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            itemsToAdd: [new LiveBotFixture.ItemDirective(WornMaceItemId, 1)],
            cleanSlate: true,
            clearInventoryFirst: true);

        var staged = await _bot.StageBotRunnerAtOrgrimmarBankAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: false);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar bank staging with visible nearby units.");

        var bankerGuid = await AssertNpcNearbyAsync(
            target,
            (uint)NPCFlags.UNIT_NPC_FLAG_BANKER,
            "banker");

        await DispatchInteractWithAsync(target, bankerGuid, "banker");

        global::Tests.Infrastructure.Skip.If(
            true,
            "Bank deposit ActionType surface is not implemented yet; Shodan item/location staging and banker InteractWith are migrated.");
    }

    [SkippableFact]
    public async Task AuctionHouse_InteractWithAuctioneer()
    {
        var target = await EnsureBgInteractionTargetAsync();

        var staged = await _bot.StageBotRunnerAtOrgrimmarAuctionHouseAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar auction-house staging with visible nearby units.");

        var auctioneerGuid = await AssertNpcNearbyAsync(
            target,
            (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER,
            "auctioneer");

        await DispatchInteractWithAsync(target, auctioneerGuid, "auctioneer");
    }

    [SkippableFact]
    public async Task Mail_SendGoldAndCollect_CoinageChanges()
    {
        var target = await EnsureBgInteractionTargetAsync();

        var staged = await _bot.StageBotRunnerAtOrgrimmarMailboxAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar mailbox staging with visible mailbox object.");

        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(target.AccountName);
        var coinageBefore = snapBefore?.Player?.Coinage ?? 0;
        _output.WriteLine($"[{target.RoleLabel}] Coinage before mail collection: {coinageBefore}");

        await _bot.StageBotRunnerMailboxMoneyAsync(target.AccountName, target.RoleLabel, copper: 10000);

        var mailbox = await FindMailboxAsync(target);
        Assert.NotNull(mailbox);

        var guid = mailbox!.Base?.Guid ?? 0UL;
        Assert.NotEqual(0UL, guid);
        _output.WriteLine(
            $"[{target.RoleLabel}] Found mailbox: type={mailbox.GameObjectType} name='{mailbox.Name}' GUID=0x{guid:X}");

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.CheckMail,
            Parameters = { new RequestParameter { LongParam = (long)guid } }
        });
        _output.WriteLine($"[{target.RoleLabel}] CheckMail dispatched (result={result})");
        Assert.Equal(ResponseResult.Success, result);

        var coinageIncreased = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            s => (s.Player?.Coinage ?? 0) > coinageBefore,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} mail-coinage-increase");
        Assert.True(coinageIncreased, $"{target.RoleLabel}: coinage should increase after collecting staged mail.");

        await _bot.RefreshSnapshotsAsync();
        var snapAfter = await _bot.GetSnapshotAsync(target.AccountName);
        var coinageAfter = snapAfter?.Player?.Coinage ?? 0;
        _output.WriteLine($"[{target.RoleLabel}] Coinage after: {coinageAfter} (delta={coinageAfter - coinageBefore})");
    }

    [SkippableFact]
    public async Task FlightMaster_DiscoverAndTakeFlight()
    {
        var target = await EnsureBgInteractionTargetAsync();

        await _bot.StageBotRunnerCoinageAsync(target.AccountName, target.RoleLabel, FlightMasterSetupCopper);

        var staged = await _bot.StageBotRunnerAtOrgrimmarFlightMasterAsync(
            target.AccountName,
            target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar flight-master staging to succeed.");

        var fmGuid = await AssertNpcNearbyAsync(
            target,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            "flight master");
        Assert.NotEqual(0UL, fmGuid);

        await SendActionAndAssertCompletedAsync(target, ActionType.VisitFlightMaster, "VisitFlightMaster");
        _output.WriteLine($"[{target.RoleLabel}] Flight master visit completed.");
    }

    [SkippableFact]
    public Task DeeprunTram_RideTransport_ArrivesAtDestination()
    {
        global::Tests.Infrastructure.Skip.If(
            true,
            "Test roster is Horde for this economy smoke suite; Deeprun Tram transport validation belongs to the dedicated transport slice.");

        return Task.CompletedTask;
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureBgInteractionTargetAsync()
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
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: BG interaction action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no BG interaction action dispatch.");

        return target;
    }

    private async Task<ulong> AssertNpcNearbyAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        uint npcFlag,
        string npcType)
    {
        var npc = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            npcFlag,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} {npcType} lookup");
        Assert.NotNull(npc);

        var guid = npc!.GameObject?.Base?.Guid ?? 0UL;
        Assert.NotEqual(0UL, guid);

        _output.WriteLine(
            $"[{target.RoleLabel}] Found {npcType}: {npc.GameObject?.Name} GUID=0x{guid:X} flags={npc.NpcFlags}");

        return guid;
    }

    private async Task DispatchInteractWithAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ulong guid,
        string targetLabel)
    {
        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)guid } }
        });
        _output.WriteLine($"[{target.RoleLabel}] InteractWith {targetLabel} dispatched (result={result})");
        Assert.Equal(ResponseResult.Success, result);
    }

    private async Task<Game.WoWGameObject?> FindMailboxAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var objects = snap?.NearbyObjects?.ToList() ?? [];

            var mailbox = objects.FirstOrDefault(go => go.GameObjectType == MailboxGoType)
                ?? objects.FirstOrDefault(go => (go.Name ?? string.Empty)
                    .Contains("mail", StringComparison.OrdinalIgnoreCase));
            if (mailbox != null)
                return mailbox;

            await Task.Delay(200);
        }

        await _bot.RefreshSnapshotsAsync();
        var latest = await _bot.GetSnapshotAsync(target.AccountName);
        var nearby = latest?.NearbyObjects?.Take(10).ToList() ?? [];
        foreach (var go in nearby)
        {
            var goGuid = go.Base?.Guid ?? 0UL;
            _output.WriteLine($"[{target.RoleLabel}] nearby object: GUID=0x{goGuid:X} type={go.GameObjectType} name='{go.Name}'");
        }

        return null;
    }

    private async Task SendActionAndAssertCompletedAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ActionType actionType,
        string stepName,
        int timeoutSeconds = 12)
    {
        var correlationId = $"bg-interaction:{target.AccountName}:{Interlocked.Increment(ref s_correlationSequence)}";
        var action = new ActionMessage
        {
            ActionType = actionType,
            CorrelationId = correlationId,
        };

        var result = await _bot.SendActionAsync(target.AccountName, action);
        _output.WriteLine($"[BG-INTERACTION] {target.RoleLabel} {stepName} dispatch result: {result}");
        Assert.Equal(ResponseResult.Success, result);

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
}
