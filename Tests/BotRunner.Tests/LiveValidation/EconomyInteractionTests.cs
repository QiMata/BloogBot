using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed economy interaction baselines for banker, auctioneer, and
/// mailbox access. SHODAN owns world/mail staging; FG/BG receive only
/// InteractWith or CheckMail action dispatches from the test body.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class EconomyInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public EconomyInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Bank_OpenAndDeposit()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveEconomyTargets();

        foreach (var target in targets)
        {
            var staged = await _bot.StageBotRunnerAtOrgrimmarBankAsync(
                target.AccountName,
                target.RoleLabel);
            Assert.True(staged, $"{target.RoleLabel}: expected bank staging with visible nearby units.");

            var ok = await InteractWithNpcTypeAsync(
                target,
                (uint)NPCFlags.UNIT_NPC_FLAG_BANKER,
                "Banker");
            Assert.True(ok, $"{target.RoleLabel} should find/interact with a banker.");
        }
    }

    [SkippableFact]
    public async Task AuctionHouse_OpenAndList()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveEconomyTargets();

        foreach (var target in targets)
        {
            var staged = await _bot.StageBotRunnerAtOrgrimmarAuctionHouseAsync(
                target.AccountName,
                target.RoleLabel);
            Assert.True(staged, $"{target.RoleLabel}: expected auction-house staging with visible nearby units.");

            var ok = await InteractWithNpcTypeAsync(
                target,
                (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER,
                "Auctioneer");
            Assert.True(ok, $"{target.RoleLabel} should find/interact with an auctioneer.");
        }
    }

    [SkippableFact]
    public async Task Mail_OpenMailbox()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveEconomyTargets();

        foreach (var target in targets)
        {
            var staged = await _bot.StageBotRunnerAtOrgrimmarMailboxAsync(
                target.AccountName,
                target.RoleLabel);
            Assert.True(staged, $"{target.RoleLabel}: expected mailbox staging with visible mailbox object.");

            await _bot.RefreshSnapshotsAsync();
            var before = await _bot.GetSnapshotAsync(target.AccountName);
            var coinageBefore = before?.Player?.Coinage ?? 0;
            _output.WriteLine($"  [{target.RoleLabel}] Coinage before mail collection: {coinageBefore}");

            await _bot.StageBotRunnerMailboxMoneyAsync(target.AccountName, target.RoleLabel, copper: 10000);

            var ok = await CollectMailFromMailboxAsync(target);
            Assert.True(ok, $"{target.RoleLabel} should find/interact with a mailbox and collect mail.");

            var coinageIncreased = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snap => (snap.Player?.Coinage ?? 0) > coinageBefore,
                TimeSpan.FromSeconds(target.IsForeground ? 12 : 8),
                pollIntervalMs: 300,
                progressLabel: $"{target.RoleLabel} mail-coinage-increase");
            Assert.True(coinageIncreased, $"{target.RoleLabel}: coinage should increase after collecting staged mail.");

            await _bot.RefreshSnapshotsAsync();
            var after = await _bot.GetSnapshotAsync(target.AccountName);
            var coinageAfter = after?.Player?.Coinage ?? 0;
            _output.WriteLine($"  [{target.RoleLabel}] Coinage after mail collection: {coinageAfter} (delta={coinageAfter - coinageBefore})");
        }
    }

    private async Task<bool> CollectMailFromMailboxAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var mailbox = await FindMailboxAsync(target);
        if (mailbox == null)
            return false;

        var guid = mailbox.Base?.Guid ?? 0UL;
        if (guid == 0)
        {
            _output.WriteLine($"  [{target.RoleLabel}] Mailbox had no valid GUID.");
            return false;
        }

        _output.WriteLine(
            $"  [{target.RoleLabel}] Mailbox found: type={mailbox.GameObjectType} name='{mailbox.Name}' GUID={guid:X}");

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.CheckMail,
            Parameters = { new RequestParameter { LongParam = (long)guid } }
        });
        _output.WriteLine($"  [{target.RoleLabel}] CheckMail sent (result={result})");
        return result == ResponseResult.Success;
    }

    private async Task<Game.WoWGameObject?> FindMailboxAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        Game.WoWGameObject? mailbox = null;
        var discoverSw = Stopwatch.StartNew();
        while (discoverSw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var objects = snap?.NearbyObjects?.ToList() ?? [];

            const uint MailboxGoType = 19;
            mailbox = objects.FirstOrDefault(go => go.GameObjectType == MailboxGoType)
                ?? objects.FirstOrDefault(go => (go.Name ?? string.Empty).Contains("mail", StringComparison.OrdinalIgnoreCase));

            if (mailbox != null)
                break;

            await Task.Delay(200);
        }

        if (mailbox == null)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var objects = snap?.NearbyObjects?.ToList() ?? [];
            _output.WriteLine($"  [{target.RoleLabel}] No mailbox found (type=19 or name). Nearby objects:");
            foreach (var go in objects.Take(10))
            {
                var goGuid = go.Base?.Guid ?? 0;
                var pos = go.Base?.Position;
                _output.WriteLine($"    [{goGuid:X8}] type={go.GameObjectType} name='{go.Name}' ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
            }
        }

        return mailbox;
    }

    private async Task<bool> InteractWithNpcTypeAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        uint npcFlag,
        string npcType)
    {
        var npc = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            npcFlag,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} {npcType}");

        if (npc == null)
        {
            _output.WriteLine($"  [{target.RoleLabel}] No {npcType} found nearby.");
            return false;
        }

        var npcGuid = npc.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"  [{target.RoleLabel}] Found {npcType}: {npc.GameObject?.Name} GUID={npcGuid:X}");

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)npcGuid } }
        });
        _output.WriteLine($"  [{target.RoleLabel}] Interaction sent (result={result})");
        return result == ResponseResult.Success;
    }

    private async Task EnsureEconomySettingsAsync()
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
    }

    private IReadOnlyList<LiveBotFixture.BotRunnerActionTarget> ResolveEconomyTargets()
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no economy action dispatch.");

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: stage economy service location and dispatch InteractWith/CheckMail.");
        }

        return targets;
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
