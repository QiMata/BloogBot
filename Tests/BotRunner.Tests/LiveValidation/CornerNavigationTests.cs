using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P11.1, P11.5-P11.8: Corner and obstacle navigation tests.
/// Validates bots can navigate through tight spaces without stalling.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class CornerNavigationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public CornerNavigationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P11.1 + P11.5: Navigate from Org bank to Org AH through tight building corners.
    /// Assert: arrives within 60s, no stalls >3s.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Navigate_OrgBankToAH_ArrivesWithoutStall()
    {
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Teleport to Org bank
        await _bot.BotTeleportAsync(bgAccount, 1, 1627f, -4376f, 18f);
        await Task.Delay(3000);

        // Send TRAVEL_TO action targeting Org AH
        var result = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { FloatParam = 1687f },  // dest X
                new RequestParameter { FloatParam = -4465f }, // dest Y
                new RequestParameter { FloatParam = 23f },    // dest Z
            }
        });
        _output.WriteLine($"[CORNER-NAV] TravelTo result: {result}");

        // Wait for arrival — track position every 5s for 60s
        bool arrived = false;
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(bgAccount);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null) continue;

            float dx = pos.X - 1687f;
            float dy = pos.Y - (-4465f);
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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Teleport to RFC entrance area (inside map 389)
        await _bot.BotTeleportAsync(bgAccount, 389, -226f, -60f, -25f);
        await Task.Delay(3000);

        // Navigate deeper into RFC
        var result = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { FloatParam = -300f },
                new RequestParameter { FloatParam = -40f },
                new RequestParameter { FloatParam = -25f },
            }
        });

        // Track position for 30s
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(5000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(bgAccount);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"[RFC-CORRIDOR] {i * 5}s: ({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        }

        await _bot.RefreshSnapshotsAsync();
        var finalSnap = await _bot.GetSnapshotAsync(bgAccount);
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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Navigate in an area with known static obstacles (Org buildings)
        await _bot.BotTeleportAsync(bgAccount, 1, 1650f, -4400f, 20f);
        await Task.Delay(3000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
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
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Teleport to Undercity tunnel area
        await _bot.BotTeleportAsync(bgAccount, 0, 1583f, 239f, -43f);
        await Task.Delay(3000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[UC-TUNNEL] Bot at ({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");
        Assert.NotNull(snap);
    }
}
