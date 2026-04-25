using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed bank parity baselines. Current implemented coverage
/// verifies FG/BG staging, item setup, banker detection, and banker
/// InteractWith dispatch; deposit/withdraw and slot purchase remain explicit
/// tracked skips until BotRunner exposes those action surfaces.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BankParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint LinenClothItemId = 2589;

    public BankParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_DepositWithdraw_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredParityTargets();

        foreach (var target in targets)
        {
            await _bot.StageBotRunnerLoadoutAsync(
                target.AccountName,
                target.RoleLabel,
                itemsToAdd: new[] { new LiveBotFixture.ItemDirective(LinenClothItemId, 1) });
            await StageAtBankAsync(target, cleanSlate: false);

            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(snap);
            var itemCount = snap!.Player?.BagContents?.Values.Count(itemId => itemId == LinenClothItemId) ?? 0;
            Assert.True(itemCount > 0, $"{target.RoleLabel}: Linen Cloth should be staged before bank parity.");
            _output.WriteLine($"[BANK-PARITY] {target.RoleLabel} has {itemCount} Linen Cloth item(s).");

            var bankerGuid = await FindBankerGuidAsync(target, $"{target.RoleLabel} banker-deposit-parity");
            var interactResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)bankerGuid } }
            });
            _output.WriteLine($"[BANK-PARITY] {target.RoleLabel} InteractWith banker result: {interactResult}");
            Assert.Equal(ResponseResult.Success, interactResult);
        }

        global::Tests.Infrastructure.Skip.If(
            true,
            "Bank deposit/withdraw ActionType surface is not implemented yet; Shodan item/location staging and banker InteractWith are migrated.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_PurchaseSlot_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredParityTargets();

        foreach (var target in targets)
        {
            await StageAtBankAsync(target);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(snap);
            Assert.True(snap!.Player?.Unit?.MaxHealth > 0, $"{target.RoleLabel}: bot must be alive.");
            _output.WriteLine(
                $"[BANK-PARITY] {target.RoleLabel} alive, HP={snap.Player?.Unit?.Health}/{snap.Player?.Unit?.MaxHealth}");

            var bankerGuid = await FindBankerGuidAsync(target, $"{target.RoleLabel} banker-slot-parity");
            Assert.NotEqual(0UL, bankerGuid);
        }

        global::Tests.Infrastructure.Skip.If(
            true,
            "Bank slot-purchase ActionType surface is not implemented yet; Shodan location staging is migrated.");
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

    private IReadOnlyList<LiveBotFixture.BotRunnerActionTarget> ResolveRequiredParityTargets()
    {
        var targets = _bot.ResolveBotRunnerActionTargets(includeForegroundIfActionable: true, foregroundFirst: false);
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no bank parity action dispatch.");

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: stage at Orgrimmar bank.");
        }

        global::Tests.Infrastructure.Skip.If(
            !targets.Any(target => target.IsForeground),
            "FG bot not actionable for bank parity.");

        return targets;
    }

    private async Task StageAtBankAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        bool cleanSlate = true)
    {
        var staged = await _bot.StageBotRunnerAtOrgrimmarBankAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate);

        Assert.True(staged, $"{target.RoleLabel}: expected to stage at Orgrimmar bank with nearby units.");
    }

    private async Task<ulong> FindBankerGuidAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        string progressLabel)
    {
        var banker = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_BANKER,
            timeoutMs: 15000,
            progressLabel: progressLabel);

        Assert.NotNull(banker);
        return banker!.GameObject?.Base?.Guid ?? 0;
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
