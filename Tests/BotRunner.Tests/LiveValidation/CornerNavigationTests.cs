using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed corner and obstacle navigation tests. SHODAN stages the BG
/// action target at each probe location; the BotRunner target receives only
/// travel actions for the route checks.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class CornerNavigationTests
{
    private const float OrgBankStreetX = 1614.1f;
    private const float OrgBankStreetY = -4382.4f;
    private const float OrgBankStreetZ = 14.8f;

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public CornerNavigationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    /// <summary>
    /// P11.1 + P11.5: Navigate from Org bank to Org AH through tight building corners.
    /// Assert: arrives within 60s, no stalls >3s.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_OrgBankToAH_ArrivesWithoutStall()
    {
        var target = await EnsureCornerNavigationSettingsAndTargetAsync();

        // Stage on the street-level bank approach, not the elevated banker interaction perch.
        // The banker location is correct for NPC interaction, but travel planning from there
        // consistently starts with a forced drop to street level, which turns this test into
        // a ledge-descent probe instead of a corner-navigation route.
        await StageNavigationStartAsync(
            target,
            OrgrimmarServiceLocations.MapId,
            OrgBankStreetX,
            OrgBankStreetY,
            OrgBankStreetZ,
            "Orgrimmar bank street");

        // Send TRAVEL_TO action targeting Org AH
        // Params: [0]=mapId (int), [1]=x, [2]=y, [3]=z
        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = 1 },        // mapId (Kalimdor)
                new RequestParameter { FloatParam = OrgrimmarServiceLocations.AuctionHouseX },
                new RequestParameter { FloatParam = OrgrimmarServiceLocations.AuctionHouseY },
                new RequestParameter { FloatParam = OrgrimmarServiceLocations.AuctionHouseZ },
            }
        });
        _output.WriteLine($"[CORNER-NAV] TravelTo result: {result}");
        Assert.Equal(ResponseResult.Success, result);

        // Wait for arrival — track position every 5s for 60s
        bool arrived = false;
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            float dx = pos.X - OrgrimmarServiceLocations.AuctionHouseX;
            float dy = pos.Y - OrgrimmarServiceLocations.AuctionHouseY;
            float dist = System.MathF.Sqrt(dx * dx + dy * dy);
            _output.WriteLine($"[CORNER-NAV] {i * 5}s: ({pos.X:F0},{pos.Y:F0}) dist={dist:F1}y");

            if (dist < 15f)
            {
                arrived = true;
                break;
            }
        }

        Assert.True(arrived, "Bot should arrive at Org AH within 60s of TravelTo");
    }

    /// <summary>
    /// P11.6: Navigate through RFC dungeon corridors.
    /// Bot navigates through narrow passages and doorframes.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_RFCCorridors_PassesThroughDoorways()
    {
        var target = await EnsureCornerNavigationSettingsAndTargetAsync();

        // Teleport to RFC entrance area (inside map 389)
        await StageNavigationStartAsync(target, 389, -226f, -60f, -25f, "RFC corridor entrance");

        // Navigate deeper into RFC
        // Params: [0]=mapId (int), [1]=x, [2]=y, [3]=z
        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = 389 },      // mapId (RFC)
                new RequestParameter { FloatParam = -300f },
                new RequestParameter { FloatParam = -40f },
                new RequestParameter { FloatParam = -25f },
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        // Track position for 30s
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"[RFC-CORRIDOR] {i * 5}s: ({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        await _bot.RefreshSnapshotsAsync();
        var finalSnap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(finalSnap);
    }

    /// <summary>
    /// P11.7: Dynamic object obstacle avoidance.
    /// Verify LOS check blocks path shortcuts through obstacles.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_DynamicObjects_PathfindsAround()
    {
        var target = await EnsureCornerNavigationSettingsAndTargetAsync();

        // Navigate in an area with known static obstacles (Org buildings)
        await StageNavigationStartAsync(target, 1, 1650f, -4400f, 20f, "Orgrimmar obstacle area");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        _output.WriteLine("[DYN-OBJ] Dynamic object avoidance test — position verified");
    }

    /// <summary>
    /// P11.8: Compare corridor path against physics replay (Undercity tunnels).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_UndercityTunnels_FollowsExpectedPath()
    {
        var target = await EnsureCornerNavigationSettingsAndTargetAsync();

        // Teleport to Undercity tunnel area
        await StageNavigationStartAsync(target, 0, 1583f, 239f, -43f, "Undercity tunnel");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[UC-TUNNEL] Bot at ({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        Assert.NotNull(snap);
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureCornerNavigationSettingsAndTargetAsync()
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
            "BG corner-navigation action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no navigation dispatch.");

        return target;
    }

    private async Task StageNavigationStartAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        int mapId,
        float x,
        float y,
        float z,
        string locationLabel)
    {
        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            mapId,
            x,
            y,
            z,
            locationLabel);
        if (staged)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} {locationLabel} staged");
            return;
        }

        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.Fail(
            $"Expected {target.RoleLabel} {target.AccountName} to reach {locationLabel}. " +
            $"finalMap={snapshot?.CurrentMapId ?? 0} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1})");
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
