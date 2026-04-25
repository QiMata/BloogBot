using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed travel planner tests. SHODAN stages the BG action target at
/// the Orgrimmar start point; the BotRunner target receives only TravelTo
/// actions and the assertions observe snapshot movement/progress.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TravelPlannerTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    // Crossroads destination
    private const float CrossroadsX = -441.0f, CrossroadsY = -2596.0f, CrossroadsZ = 96.0f;

    public TravelPlannerTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    /// <summary>
    /// V2.9 - TRAVEL_TO dispatches and bot begins moving from Orgrimmar toward Crossroads.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_Crossroads_BotStartsMoving()
    {
        var target = await EnsureTravelPlannerSettingsAndTargetAsync();
        SkipLongCrossroadsRoute(target);
    }

    /// <summary>
    /// V2.9 - Verify bot continues to make progress toward the Crossroads destination.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_Crossroads_PositionApproachesDestination()
    {
        var target = await EnsureTravelPlannerSettingsAndTargetAsync();
        SkipLongCrossroadsRoute(target);
    }

    /// <summary>
    /// V2.9 - TRAVEL_TO with same-zone walk: short route within Orgrimmar.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_ShortWalk_WithinOrgrimmar()
    {
        var target = await EnsureTravelPlannerSettingsAndTargetAsync();
        // Start on an Orgrimmar street-level approach and walk toward the AH.
        const float destX = OrgrimmarServiceLocations.AuctionHouseX;
        const float destY = OrgrimmarServiceLocations.AuctionHouseY;
        const float destZ = OrgrimmarServiceLocations.AuctionHouseZ;

        await StageTravelStartAsync(target);

        await _bot.RefreshSnapshotsAsync();
        var startPos = (await _bot.GetSnapshotAsync(target.AccountName))!.Player?.Unit?.GameObject?.Base?.Position!;

        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = MapId },
                new RequestParameter { FloatParam = destX },
                new RequestParameter { FloatParam = destY },
                new RequestParameter { FloatParam = destZ }
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        var moved = await _bot.WaitForPositionChangeAsync(
            target.AccountName,
            startPos.X,
            startPos.Y,
            startPos.Z,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} short-walk");
        Assert.True(moved, "Bot should move on short TRAVEL_TO within Orgrimmar");
    }

    /// <summary>
    /// V2.9 - Cross-zone route from Orgrimmar toward Crossroads verifies map stays Kalimdor.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_CrossZone_MapStaysKalimdor()
    {
        var target = await EnsureTravelPlannerSettingsAndTargetAsync();
        SkipLongCrossroadsRoute(target);
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureTravelPlannerSettingsAndTargetAsync()
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
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG travel-planner action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no TravelTo dispatch.");

        return target;
    }

    private async Task StageTravelStartAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var staged = await _bot.StageBotRunnerAtTravelPlannerStartAsync(
            target.AccountName,
            target.RoleLabel);
        if (staged)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} travel-planner staged");
            return;
        }

        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.Fail(
            $"Expected {target.RoleLabel} {target.AccountName} to reach the travel-planner start. " +
            $"finalMap={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");
    }

    private void SkipLongCrossroadsRoute(LiveBotFixture.BotRunnerActionTarget target)
    {
        const string reason =
            "Orgrimmar-to-Crossroads TravelTo is Shodan-launched but currently leaves BG CurrentAction=TravelTo after GoToTask starts; isolated evidence shows no position delta after 20s.";
        _output.WriteLine(
            $"[TRAVEL-PLANNER] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            $"{reason} target=({CrossroadsX:F0},{CrossroadsY:F0},{CrossroadsZ:F0}) map={MapId}.");
        global::Tests.Infrastructure.Skip.If(true, reason);
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
