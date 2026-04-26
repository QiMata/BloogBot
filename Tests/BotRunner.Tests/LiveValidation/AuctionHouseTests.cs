using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed auction house interaction baseline.
/// SHODAN stages BotRunner targets at the Orgrimmar auction house; FG/BG
/// receive only InteractWith actions from the test body.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AuctionHouseTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float AuctioneerMaxDistance = 30f;

    public AuctionHouseTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_NavigateToAuctioneer_SnapshotShowsNearbyNpc()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveAuctionHouseTargets();

        foreach (var target in targets)
        {
            await StageAtAuctionHouseAsync(target);
            var auctioneerGuid = await AssertAuctioneerNearbyAsync(target, "auctioneer");
            Assert.NotEqual(0UL, auctioneerGuid);
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AH_InteractWithAuctioneer_OpensAhFrame()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveAuctionHouseTargets();

        foreach (var target in targets)
        {
            await StageAtAuctionHouseAsync(target);
            var auctioneerGuid = await AssertAuctioneerNearbyAsync(target, $"{target.RoleLabel} auctioneer-interact");

            var interactResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)auctioneerGuid } }
            });
            _output.WriteLine($"[AH] {target.RoleLabel} InteractWith result: {interactResult}");
            Assert.Equal(ResponseResult.Success, interactResult);

            var verifyGuid = await AssertAuctioneerNearbyAsync(target, $"{target.RoleLabel} auctioneer-verify");
            Assert.NotEqual(0UL, verifyGuid);
        }
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

    private IReadOnlyList<LiveBotFixture.BotRunnerActionTarget> ResolveAuctionHouseTargets()
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no auction-house action dispatch.");

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: stage at Orgrimmar AH and dispatch InteractWith where required.");
        }

        return targets;
    }

    private async Task StageAtAuctionHouseAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var staged = await _bot.StageBotRunnerAtOrgrimmarAuctionHouseAsync(
            target.AccountName,
            target.RoleLabel);

        Assert.True(staged, $"{target.RoleLabel}: expected to stage at Orgrimmar auction house with nearby units.");
    }

    private async Task<ulong> AssertAuctioneerNearbyAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        string progressLabel)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[AH] {target.RoleLabel} position: ({pos!.X:F0},{pos.Y:F0},{pos.Z:F0})");

        var auctioneer = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_AUCTIONEER,
            timeoutMs: 15000,
            progressLabel: progressLabel);

        Assert.NotNull(auctioneer);
        var auctPos = auctioneer!.GameObject?.Base?.Position;
        _output.WriteLine($"[AH] {target.RoleLabel} found auctioneer at ({auctPos?.X:F0},{auctPos?.Y:F0})");

        var auctDist = pos != null && auctPos != null
            ? MathF.Sqrt(MathF.Pow(pos.X - auctPos.X, 2) + MathF.Pow(pos.Y - auctPos.Y, 2))
            : float.MaxValue;
        Assert.True(
            auctDist < AuctioneerMaxDistance,
            $"{target.RoleLabel}: auctioneer should be within {AuctioneerMaxDistance:F0}y, was {auctDist:F1}y");

        return auctioneer.GameObject?.Base?.Guid ?? 0;
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
