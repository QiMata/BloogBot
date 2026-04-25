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
/// Shodan-directed bank interaction baseline.
/// SHODAN stages BotRunner targets at the Orgrimmar bank and supplies test
/// items; FG/BG receive only InteractWith actions from the test body.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BankInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint LinenClothItemId = 2589;
    private const float BankerMaxDistance = 20f;

    public BankInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_NavigateToBanker_FindsBankerNpc()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveBankTargets();

        foreach (var target in targets)
        {
            await StageAtBankAsync(target);
            var bankerGuid = await AssertBankerNearbyAsync(target, $"{target.RoleLabel} banker");
            Assert.NotEqual(0UL, bankerGuid);
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Bank_DepositAndWithdraw_ItemPreserved()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveBankTargets();

        foreach (var target in targets)
        {
            await _bot.StageBotRunnerLoadoutAsync(
                target.AccountName,
                target.RoleLabel,
                itemsToAdd: new[] { new LiveBotFixture.ItemDirective(LinenClothItemId, 1) });
            await StageAtBankAsync(target, cleanSlate: false);

            await _bot.RefreshSnapshotsAsync();
            var beforeSnap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(beforeSnap);
            var itemCount = beforeSnap!.Player?.BagContents?.Values.Count(itemId => itemId == LinenClothItemId) ?? 0;
            _output.WriteLine($"[BANK] {target.RoleLabel} Linen Cloth before bank interaction: {itemCount}");
            Assert.True(itemCount > 0, $"{target.RoleLabel}: Linen Cloth should be staged before bank interaction.");

            var bankerGuid = await AssertBankerNearbyAsync(target, $"{target.RoleLabel} banker-deposit");
            var interactResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)bankerGuid } }
            });
            _output.WriteLine($"[BANK] {target.RoleLabel} InteractWith banker result: {interactResult}");
            Assert.Equal(ResponseResult.Success, interactResult);
        }

        global::Tests.Infrastructure.Skip.If(
            true,
            "Bank deposit/withdraw ActionType surface is not implemented yet; Shodan item/location staging and banker InteractWith are migrated.");
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

    private IReadOnlyList<LiveBotFixture.BotRunnerActionTarget> ResolveBankTargets()
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no bank action dispatch.");

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: stage at Orgrimmar bank and dispatch InteractWith where required.");
        }

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

    private async Task<ulong> AssertBankerNearbyAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        string progressLabel)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[BANK] {target.RoleLabel} position: ({pos!.X:F0},{pos.Y:F0},{pos.Z:F0})");

        var banker = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_BANKER,
            timeoutMs: 15000,
            progressLabel: progressLabel);

        Assert.NotNull(banker);
        var bankerPos = banker!.GameObject?.Base?.Position;
        _output.WriteLine($"[BANK] {target.RoleLabel} found banker at ({bankerPos?.X:F0},{bankerPos?.Y:F0})");

        var bankerDist = pos != null && bankerPos != null
            ? MathF.Sqrt(MathF.Pow(pos.X - bankerPos.X, 2) + MathF.Pow(pos.Y - bankerPos.Y, 2))
            : float.MaxValue;
        Assert.True(
            bankerDist < BankerMaxDistance,
            $"{target.RoleLabel}: banker should be within {BankerMaxDistance:F0}y, was {bankerDist:F1}y");

        return banker.GameObject?.Base?.Guid ?? 0;
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
