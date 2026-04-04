using System;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.9: Travel planner tests. Bot in Orgrimmar sends TRAVEL_TO with Crossroads
/// destination params, verifies position changes over time.
///
/// Run: dotnet test --filter "FullyQualifiedName~TravelPlannerTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TravelPlannerTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    // Orgrimmar starting position
    private const float OrgX = 1676.0f, OrgY = -4315.0f, OrgZ = 64.0f;
    // Crossroads destination
    private const float CrossroadsX = -441.0f, CrossroadsY = -2596.0f, CrossroadsZ = 96.0f;

    public TravelPlannerTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// V2.9 - TRAVEL_TO dispatches and bot begins moving from Orgrimmar toward Crossroads.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_Crossroads_BotStartsMoving()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgX, OrgY, OrgZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgX, OrgY);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(startSnap);
        var startPos = startSnap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1}, {startPos.Z:F1})");

        // Send TRAVEL_TO with Crossroads destination
        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { FloatParam = CrossroadsX },
                new RequestParameter { FloatParam = CrossroadsY },
                new RequestParameter { FloatParam = CrossroadsZ },
                new RequestParameter { FloatParam = MapId }
            }
        });
        _output.WriteLine($"[TEST] TRAVEL_TO dispatch result: {result}");
        Assert.Equal(ResponseResult.Success, result);

        // Verify position changes over time
        var moved = await _bot.WaitForPositionChangeAsync(account, startPos.X, startPos.Y, startPos.Z,
            timeoutMs: 30000, progressLabel: "BG travel-to-crossroads");
        Assert.True(moved, "Bot should start moving after TRAVEL_TO dispatch");
    }

    /// <summary>
    /// V2.9 - Verify bot continues to make progress toward the Crossroads destination.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_Crossroads_PositionApproachesDestination()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgX, OrgY, OrgZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgX, OrgY);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        var startPos = startSnap!.Player?.Unit?.GameObject?.Base?.Position!;
        var initialDist = LiveBotFixture.Distance2D(startPos.X, startPos.Y, CrossroadsX, CrossroadsY);
        _output.WriteLine($"[TEST] Initial distance to Crossroads: {initialDist:F0}y");

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { FloatParam = CrossroadsX },
                new RequestParameter { FloatParam = CrossroadsY },
                new RequestParameter { FloatParam = CrossroadsZ },
                new RequestParameter { FloatParam = MapId }
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        // Wait 20s and check that distance decreased
        await Task.Delay(20000);
        await _bot.RefreshSnapshotsAsync();
        var afterSnap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(afterSnap);
        var afterPos = afterSnap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(afterPos);

        var finalDist = LiveBotFixture.Distance2D(afterPos!.X, afterPos.Y, CrossroadsX, CrossroadsY);
        _output.WriteLine($"[TEST] Distance after 20s: {finalDist:F0}y (was {initialDist:F0}y)");
        Assert.True(finalDist < initialDist, $"Bot should be closer to Crossroads after 20s. Initial={initialDist:F0}, Final={finalDist:F0}");
    }

    /// <summary>
    /// V2.9 - TRAVEL_TO with same-zone walk: short route within Orgrimmar.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_ShortWalk_WithinOrgrimmar()
    {
        var account = _bot.BgAccountName!;
        // Start in Org and walk to a nearby point
        const float destX = 1630.0f, destY = -4260.0f, destZ = 62.0f;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgX, OrgY, OrgZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgX, OrgY);

        await _bot.RefreshSnapshotsAsync();
        var startPos = (await _bot.GetSnapshotAsync(account))!.Player?.Unit?.GameObject?.Base?.Position!;

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { FloatParam = destX },
                new RequestParameter { FloatParam = destY },
                new RequestParameter { FloatParam = destZ },
                new RequestParameter { FloatParam = MapId }
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        var moved = await _bot.WaitForPositionChangeAsync(account, startPos.X, startPos.Y, startPos.Z,
            timeoutMs: 15000, progressLabel: "BG short-walk");
        Assert.True(moved, "Bot should move on short TRAVEL_TO within Orgrimmar");
    }

    /// <summary>
    /// V2.9 - Cross-zone route from Orgrimmar toward Crossroads verifies map stays Kalimdor.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task TravelTo_CrossZone_MapStaysKalimdor()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgX, OrgY, OrgZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgX, OrgY);

        var result = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { FloatParam = CrossroadsX },
                new RequestParameter { FloatParam = CrossroadsY },
                new RequestParameter { FloatParam = CrossroadsZ },
                new RequestParameter { FloatParam = MapId }
            }
        });
        Assert.Equal(ResponseResult.Success, result);

        // Wait and verify mapId stays as Kalimdor (1)
        await Task.Delay(10000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] CurrentMapId after travel: {snap!.CurrentMapId}");
        Assert.Equal((uint)MapId, snap.CurrentMapId);
    }
}
