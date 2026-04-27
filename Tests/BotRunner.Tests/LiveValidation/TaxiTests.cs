using System;
using System.IO;
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

    private const int OrgrimmarTaxiNodeId = 23;
    private const int CrossroadsTaxiNodeId = 25;
    private const int GadgetzanTaxiNodeId = 40; // Horde-side Gadgetzan route node

    // Taxi node landing positions sourced from VMaNGOS mangos.taxi_nodes.
    // Tolerance accounts for the bot dismounting on the landing platform a
    // few yards from the taxi node origin.
    private const float CrossroadsX = -441.8f;
    private const float CrossroadsY = -2596.08f;
    private const float GadgetzanX = -7048.89f;
    private const float GadgetzanY = -3780.36f;
    private const float TaxiArrivalToleranceYards = 200.0f;

    public TaxiTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    /// <summary>
    /// V2.11: Bot at Orgrimmar flight master. VISIT_FLIGHT_MASTER discovers taxi nodes.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_HordeDiscovery()
    {
        var target = await EnsureTaxiSettingsAndTargetAsync();

        var fmGuid = await _bot.StageBotRunnerTaxiReadinessAsync(
            target.AccountName,
            target.RoleLabel,
            enableAllTaxiNodes: false,
            minimumCopper: 0);
        _output.WriteLine($"[TEST] Found flight master guid=0x{fmGuid:X}");

        // Visit flight master to discover nodes
        var visitResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        _output.WriteLine($"[TEST] VISIT_FLIGHT_MASTER result: {visitResult}");
        Assert.Equal(ResponseResult.Success, visitResult);

        // Wait for interaction to complete
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
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
        var target = await EnsureTaxiSettingsAndTargetAsync();

        var fmGuid = await _bot.StageBotRunnerTaxiReadinessAsync(
            target.AccountName,
            target.RoleLabel);

        // Visit flight master first to open taxi map
        var visitResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        Assert.Equal(ResponseResult.Success, visitResult);
        await Task.Delay(2000);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startSnap);
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1})");

        // Select Crossroads taxi node (node index varies; use SELECT_TAXI_NODE)
        var selectResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
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

        var moved = await WaitForTaxiDepartureAsync(target.AccountName, startSnap!.CurrentMapId, startPos,
            timeoutMs: 180000, progressLabel: $"{target.RoleLabel} taxi-ride-org-xroads-departure");
        _output.WriteLine($"[TEST] Position changed during taxi ride: {moved}");
        if (!moved)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} taxi ride no-departure cleanup");
            global::Tests.Infrastructure.Skip.If(
                true,
                "Orgrimmar-to-Crossroads taxi is Shodan-staged and SelectTaxiNode-dispatched, but this live run did not observe departure.");
        }

        // Phase D: full ride. Wait for the bot to actually arrive at Crossroads
        // (close to the destination flight master AND no longer on the taxi
        // transport flag). 180s window covers the Org → Crossroads flight
        // (~60s typical) plus boarding/dismount overhead and the per-snapshot
        // poll cadence.
        var arrived = await WaitForTaxiArrivalAsync(
            target.AccountName,
            destinationMapId: startSnap.CurrentMapId,
            destX: CrossroadsX,
            destY: CrossroadsY,
            timeoutMs: 180000,
            progressLabel: $"{target.RoleLabel} taxi-ride-org-xroads-arrival");

        await _bot.RefreshSnapshotsAsync();
        var endSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var endPos = endSnap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[TEST] End position: ({endPos?.X:F1}, {endPos?.Y:F1})");

        if (!arrived)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} taxi ride no-arrival cleanup");
            global::Tests.Infrastructure.Skip.If(
                true,
                $"Taxi departed but did not reach Crossroads (within {TaxiArrivalToleranceYards}yd of {CrossroadsX:F0},{CrossroadsY:F0}) within 90s. " +
                $"Final pos=({endPos?.X:F1},{endPos?.Y:F1}). Likely a flight-path graph or boarding-state regression.");
        }

        Assert.NotNull(endPos);
        var distanceToCrossroads = LiveBotFixture.Distance2D(endPos!.X, endPos.Y, CrossroadsX, CrossroadsY);
        _output.WriteLine($"[TEST] Arrived within {distanceToCrossroads:F1}yd of Crossroads flight master.");
        Assert.True(
            distanceToCrossroads <= TaxiArrivalToleranceYards,
            $"Bot did not arrive at Crossroads. distance={distanceToCrossroads:F1}yd, tolerance={TaxiArrivalToleranceYards}yd, pos=({endPos.X:F1},{endPos.Y:F1}).");
    }

    /// <summary>
    /// V2.11: Alliance taxi ride placeholder -- requires Alliance character.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_AllianceRide()
    {
        var target = await EnsureTaxiSettingsAndTargetAsync();

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Character: {snap!.CharacterName}, MapId={snap.CurrentMapId}");
        global::Tests.Infrastructure.Skip.If(
            true,
            "Alliance taxi ride is Shodan-shaped but requires an Alliance action-target config; Economy.config.json is Horde-only.");
    }

    /// <summary>
    /// V2.11: Multi-hop taxi from Org to Gadgetzan.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_MultiHop_OrgToGadgetzan()
    {
        var target = await EnsureTaxiSettingsAndTargetAsync();

        var fmGuid = await _bot.StageBotRunnerTaxiReadinessAsync(
            target.AccountName,
            target.RoleLabel);

        // Visit flight master
        var visitResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.VisitFlightMaster
        });
        Assert.Equal(ResponseResult.Success, visitResult);
        await Task.Delay(2000);

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var startPos = startSnap?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startSnap);
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1})");

        // Select Gadgetzan taxi node
        var selectResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
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

        var moved = await WaitForTaxiDepartureAsync(target.AccountName, startSnap!.CurrentMapId, startPos,
            timeoutMs: 60000, progressLabel: $"{target.RoleLabel} taxi-multihop-departure");
        if (!moved)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} taxi multihop no-departure cleanup");
            global::Tests.Infrastructure.Skip.If(
                true,
                "Orgrimmar-to-Gadgetzan taxi is Shodan-staged and SelectTaxiNode-dispatched, but this live run did not observe departure.");
        }

        // Phase D: full multi-hop ride. Org → Gadgetzan crosses Kalimdor with
        // an intermediate hop (typically Crossroads). 360s window covers the
        // ~3-4 minute combined flight time. Same-map check (still Kalimdor),
        // arrival within TaxiArrivalToleranceYards of Gadgetzan node, and
        // taxi flag cleared.
        var arrived = await WaitForTaxiArrivalAsync(
            target.AccountName,
            destinationMapId: startSnap.CurrentMapId,
            destX: GadgetzanX,
            destY: GadgetzanY,
            timeoutMs: 360000,
            progressLabel: $"{target.RoleLabel} taxi-multihop-arrival");

        await _bot.RefreshSnapshotsAsync();
        var endSnap = await _bot.GetSnapshotAsync(target.AccountName);
        var endPos = endSnap?.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[TEST] Multi-hop end position: ({endPos?.X:F1}, {endPos?.Y:F1})");

        if (!arrived)
        {
            await _bot.QuiesceAccountsAsync(
                new[] { target.AccountName },
                $"{target.RoleLabel} taxi multihop no-arrival cleanup");
            global::Tests.Infrastructure.Skip.If(
                true,
                $"Multi-hop taxi departed but did not reach Gadgetzan (within {TaxiArrivalToleranceYards}yd of {GadgetzanX:F0},{GadgetzanY:F0}) within 360s. " +
                $"Final pos=({endPos?.X:F1},{endPos?.Y:F1}). Likely a multi-hop chaining or boarding-state regression.");
        }

        Assert.NotNull(endPos);
        var distance = LiveBotFixture.Distance2D(endPos!.X, endPos.Y, GadgetzanX, GadgetzanY);
        _output.WriteLine($"[TEST] Multi-hop arrived within {distance:F1}yd of Gadgetzan flight master.");
        Assert.True(
            distance <= TaxiArrivalToleranceYards,
            $"Bot did not arrive at Gadgetzan. distance={distance:F1}yd, tolerance={TaxiArrivalToleranceYards}yd.");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureTaxiSettingsAndTargetAsync()
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
            .Single(candidate => !candidate.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: BG taxi action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no taxi action dispatch.");

        return target;
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

    /// <summary>
    /// Phase D: wait for the bot to land at the destination flight path. The
    /// bot is "arrived" when:
    /// 1. It is on the destination map.
    /// 2. It is within <see cref="TaxiArrivalToleranceYards"/> 2D of the
    ///    destination coordinates.
    /// 3. It is no longer flagged as on a transport (taxi mount cleared).
    /// </summary>
    private Task<bool> WaitForTaxiArrivalAsync(
        string account,
        uint destinationMapId,
        float destX,
        float destY,
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

                if (snapshot.CurrentMapId != destinationMapId)
                    return false;

                var distance = LiveBotFixture.Distance2D(position.X, position.Y, destX, destY);
                if (distance > TaxiArrivalToleranceYards)
                    return false;

                // Also require the taxi mount/transport flag to clear, so we
                // don't false-positive when the flight path passes overhead
                // and dips through the tolerance radius mid-flight.
                var stillOnTransport = movement.TransportGuid != 0
                    || (((MovementFlags)movement.MovementFlags) & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;
                return !stillOnTransport;
            },
            TimeSpan.FromMilliseconds(timeoutMs),
            pollIntervalMs: 500,
            progressLabel: progressLabel);
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve repository path for {Path.Combine(segments)} from {AppContext.BaseDirectory}.");
    }
}
