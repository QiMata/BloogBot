using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.11: Taxi tests. Teleport near Org flight master, VISIT_FLIGHT_MASTER,
/// SELECT_TAXI_NODE, verify position changes over time.
///
/// Run: dotnet test --filter "FullyQualifiedName~TaxiTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TaxiTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    // Near Orgrimmar flight master (Doras)
    private const float OrgFmX = 1676.25f, OrgFmY = -4313.45f, OrgFmZ = 64.72f;
    private const int OrgrimmarTaxiNodeId = 23;
    private const int CrossroadsTaxiNodeId = 25;
    private const int GadgetzanTaxiNodeId = 40; // Horde-side Gadgetzan route node
    // Crossroads approximate landing position
    private const float CrossroadsX = -441.0f, CrossroadsY = -2596.0f;

    public TaxiTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// V2.11: Bot at Orgrimmar flight master. VISIT_FLIGHT_MASTER discovers taxi nodes.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_HordeDiscovery()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgFmX, OrgFmY, OrgFmZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgFmX, OrgFmY);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: "BG taxi-setup");

        // Find flight master NPC
        var fmUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: "BG flight-master-lookup");
        Assert.NotNull(fmUnit);
        _output.WriteLine($"[TEST] Found flight master: {fmUnit!.GameObject?.Name}, guid=0x{fmUnit.GameObject?.Base?.Guid:X}");

        // Visit flight master to discover nodes
        var visitResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        _output.WriteLine($"[TEST] VISIT_FLIGHT_MASTER result: {visitResult}");
        Assert.Equal(ResponseResult.Success, visitResult);

        // Wait for interaction to complete
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] Taxi discovery complete -- flight master visited");
    }

    /// <summary>
    /// V2.11: Bot at Orgrimmar, takes taxi to Crossroads. Position changes over time.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_HordeRide_OrgToXroads()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.EnsureTaxiNodesEnabledAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgFmX, OrgFmY, OrgFmZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgFmX, OrgFmY);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: "BG taxi-ride-setup");

        var fmUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: "BG taxi-ride-fm");
        Assert.NotNull(fmUnit);
        var fmGuid = fmUnit!.GameObject?.Base?.Guid ?? 0UL;
        Assert.NotEqual(0UL, fmGuid);

        // Visit flight master first to open taxi map
        var visitResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        Assert.Equal(ResponseResult.Success, visitResult);
        await Task.Delay(2000);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startSnap);
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1})");

        // Select Crossroads taxi node (node index varies; use SELECT_TAXI_NODE)
        var selectResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SelectTaxiNode,
            Parameters =
            {
                new RequestParameter { LongParam = unchecked((long)fmGuid) },
                new RequestParameter { IntParam = OrgrimmarTaxiNodeId },
                new RequestParameter { IntParam = CrossroadsTaxiNodeId }
            }
        });
        _output.WriteLine($"[TEST] SELECT_TAXI_NODE result: {selectResult}");

        var moved = await WaitForTaxiDepartureAsync(account, startSnap!.CurrentMapId, startPos,
            timeoutMs: 180000, progressLabel: "BG taxi-ride-org-xroads");
        _output.WriteLine($"[TEST] Position changed during taxi ride: {moved}");
        Assert.True(moved, "Bot position should change during taxi flight");

        await _bot.RefreshSnapshotsAsync();
        var endSnap = await _bot.GetSnapshotAsync(account);
        var endPos = endSnap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[TEST] End position: ({endPos?.X:F1}, {endPos?.Y:F1})");
    }

    /// <summary>
    /// V2.11: Alliance taxi ride placeholder -- requires Alliance character.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_AllianceRide()
    {
        var account = _bot.BgAccountName!;

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Character: {snap!.CharacterName}, MapId={snap.CurrentMapId}");
        // Alliance taxi tests require an Alliance character -- validate fixture is ready
        Assert.NotNull(snap.Player);
    }

    /// <summary>
    /// V2.11: Multi-hop taxi from Org to Gadgetzan.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_MultiHop_OrgToGadgetzan()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.EnsureTaxiNodesEnabledAsync(account, "BG");
        await _bot.BotTeleportAsync(account, MapId, OrgFmX, OrgFmY, OrgFmZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgFmX, OrgFmY);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: "BG multihop-setup");

        var fmUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: "BG multihop-fm");
        Assert.NotNull(fmUnit);
        var fmGuid = fmUnit!.GameObject?.Base?.Guid ?? 0UL;
        Assert.NotEqual(0UL, fmGuid);

        // Visit flight master
        var visitResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        Assert.Equal(ResponseResult.Success, visitResult);
        await Task.Delay(2000);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startSnap);
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1})");

        // Select Gadgetzan taxi node
        var selectResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SelectTaxiNode,
            Parameters =
            {
                new RequestParameter { LongParam = unchecked((long)fmGuid) },
                new RequestParameter { IntParam = OrgrimmarTaxiNodeId },
                new RequestParameter { IntParam = GadgetzanTaxiNodeId }
            }
        });
        _output.WriteLine($"[TEST] SELECT_TAXI_NODE (Gadgetzan) result: {selectResult}");

        var moved = await WaitForTaxiDepartureAsync(account, startSnap!.CurrentMapId, startPos,
            timeoutMs: 60000, progressLabel: "BG taxi-multihop");
        Assert.True(moved, "Bot should depart on multi-hop taxi flight");
    }

    private Task<bool> WaitForTaxiDepartureAsync(
        string account,
        uint startMapId,
        Game.Position startPos,
        int timeoutMs,
        string progressLabel)
    {
        return _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot =>
            {
                var movement = snapshot?.MovementData;
                var position = movement?.Position;
                if (snapshot == null || movement == null || position == null)
                    return false;

                var moved = LiveBotFixture.Distance2D(position.X, position.Y, startPos.X, startPos.Y) >= 3f
                    || Math.Abs(position.Z - startPos.Z) >= 2f;
                var onTransport = movement.TransportGuid != 0
                    || (((MovementFlags)movement.MovementFlags) & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;
                return moved || onTransport || snapshot.CurrentMapId != startMapId;
            },
            TimeSpan.FromMilliseconds(timeoutMs),
            pollIntervalMs: 500,
            progressLabel: progressLabel);
    }
}
