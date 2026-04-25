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
/// Shodan-directed AH parity baselines. Current implemented coverage verifies
/// FG/BG staging and auctioneer detection; post/buy/cancel remain explicit
/// tracked skips until BotRunner exposes those action surfaces.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AuctionHouseParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint LinenClothItemId = 2589;

    public AuctionHouseParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_Search_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredParityTargets();

        foreach (var target in targets)
        {
            await StageAtAuctionHouseAsync(target);
            var auctioneerGuid = await FindAuctioneerGuidAsync(target, $"{target.RoleLabel} auctioneer-search");
            Assert.NotEqual(0UL, auctioneerGuid);
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_PostAndBuy_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredParityTargets();
        var seller = targets.First(target => target.IsForeground);
        var buyer = targets.First(target => !target.IsForeground);

        await _bot.StageBotRunnerLoadoutAsync(
            seller.AccountName,
            seller.RoleLabel,
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(LinenClothItemId, 1) });
        await StageAtAuctionHouseAsync(seller, cleanSlate: false);
        await StageAtAuctionHouseAsync(buyer);

        await _bot.RefreshSnapshotsAsync();
        var sellerSnap = await _bot.GetSnapshotAsync(seller.AccountName);
        Assert.NotNull(sellerSnap);
        var itemCount = sellerSnap!.Player?.BagContents?.Values.Count(itemId => itemId == LinenClothItemId) ?? 0;
        Assert.True(itemCount > 0, "FG seller should have Linen Cloth after Shodan loadout staging.");
        _output.WriteLine($"[AH-PARITY] {seller.RoleLabel} seller has {itemCount} Linen Cloth item(s).");

        global::Tests.Infrastructure.Skip.If(
            true,
            "Auction post/buy ActionType surface is not implemented yet; Shodan loadout/location staging is migrated.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_Cancel_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredParityTargets();

        foreach (var target in targets)
        {
            await StageAtAuctionHouseAsync(target);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(snap);
            Assert.True(snap!.Player?.Unit?.MaxHealth > 0, $"{target.RoleLabel}: bot must be alive.");
            _output.WriteLine(
                $"[AH-PARITY] {target.RoleLabel} alive, HP={snap.Player?.Unit?.Health}/{snap.Player?.Unit?.MaxHealth}");
        }

        global::Tests.Infrastructure.Skip.If(
            true,
            "Auction cancel ActionType surface is not implemented yet; Shodan location staging is migrated.");
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
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no auction parity action dispatch.");

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: stage at Orgrimmar AH.");
        }

        global::Tests.Infrastructure.Skip.If(
            !targets.Any(target => target.IsForeground),
            "FG bot not actionable for auction-house parity.");

        return targets;
    }

    private async Task StageAtAuctionHouseAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        bool cleanSlate = true)
    {
        var staged = await _bot.StageBotRunnerAtOrgrimmarAuctionHouseAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate);

        Assert.True(staged, $"{target.RoleLabel}: expected to stage at Orgrimmar auction house with nearby units.");
    }

    private async Task<ulong> FindAuctioneerGuidAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        string progressLabel)
    {
        var auctioneer = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER,
            timeoutMs: 15000,
            progressLabel: progressLabel);

        Assert.NotNull(auctioneer);
        return auctioneer!.GameObject?.Base?.Guid ?? 0;
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
