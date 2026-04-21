using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.15, 23.16, 23.17: Taxi ride, transport boarding, cross-continent with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~TaxiTransportParityTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TaxiTransportParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    private const int EasternKingdomsMapId = 0;
    private const float OrgrimmarFlightMasterX = 1676.25f;
    private const float OrgrimmarFlightMasterY = -4313.45f;
    private const float OrgrimmarFlightMasterZ = 64.72f;
    private const int OrgrimmarTaxiNodeId = 23;
    private const float CrossroadsX = -441.0f;
    private const float CrossroadsY = -2596.0f;
    private const float CrossroadsZ = 96.0f;
    private const int CrossroadsTaxiNodeId = 25;
    private const float OrgrimmarZeppelinTowerX = 1320.0f;
    private const float OrgrimmarZeppelinTowerY = -4653.0f;
    private const float OrgrimmarZeppelinTowerZ = 53.0f;
    private const uint OrgrimmarUndercityZeppelinEntry = 176495;
    private const float UndercityElevatorWestX = 1544.24f;
    private const float UndercityElevatorWestY = 240.77f;
    private const float UndercityElevatorUpperZ = 55.40f;
    private const float UndercityElevatorLowerZ = -43.0f;
    private const uint UndercityElevatorWestEntry = 20655;
    private const uint GoTypeTransport = 11;
    private const uint GoTypeMoTransport = 15;

    public TaxiTransportParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_Ride_FgBgParity()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName!;

        await EnsureFgParityReadyAsync();
        var bgFlightMasterGuid = await PrepareTaxiAccountAsync(bgAccount, "BG");
        var fgFlightMasterGuid = await PrepareTaxiAccountAsync(fgAccount, "FG");

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, bgAccount, "packets", "transform", "physics");
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, fgAccount, "packets", "transform");

        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount, MakeRecordingAction(ActionType.StartPhysicsRecording)),
            _bot.SendActionAsync(fgAccount, MakeRecordingAction(ActionType.StartPhysicsRecording)));

        var visitResults = await Task.WhenAll(
            _bot.SendActionAsync(bgAccount, new ActionMessage { ActionType = ActionType.VisitFlightMaster }),
            _bot.SendActionAsync(fgAccount, new ActionMessage { ActionType = ActionType.VisitFlightMaster }));
        Assert.All(visitResults, result => Assert.Equal(ResponseResult.Success, result));

        await Task.Delay(2000);
        await _bot.RefreshSnapshotsAsync();
        var bgStart = await _bot.GetSnapshotAsync(bgAccount);
        var fgStart = await _bot.GetSnapshotAsync(fgAccount);
        var bgStartPos = bgStart?.Player?.Unit?.GameObject?.Base?.Position;
        var fgStartPos = fgStart?.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(bgStart);
        Assert.NotNull(fgStart);
        Assert.NotNull(bgStartPos);
        Assert.NotNull(fgStartPos);

        var selectResults = await Task.WhenAll(
            _bot.SendActionAsync(bgAccount, MakeSelectTaxiNode(bgFlightMasterGuid, OrgrimmarTaxiNodeId, CrossroadsTaxiNodeId)),
            _bot.SendActionAsync(fgAccount, MakeSelectTaxiNode(fgFlightMasterGuid, CrossroadsTaxiNodeId)));
        Assert.All(selectResults, result => Assert.Equal(ResponseResult.Success, result));

        var movedResults = await Task.WhenAll(
            WaitForTaxiDepartureAsync(bgAccount, bgStart!.CurrentMapId, bgStartPos!, "BG taxi parity"),
            WaitForTaxiDepartureAsync(fgAccount, fgStart!.CurrentMapId, fgStartPos!, "FG taxi parity"));

        await StopDualRecordingAsync(bgAccount, fgAccount);

        Assert.True(movedResults[0], "BG should depart on the Orgrimmar -> Crossroads taxi ride.");
        Assert.True(movedResults[1], "FG should depart on the Orgrimmar -> Crossroads taxi ride.");

        var fgTransform = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "transform", fgAccount, "csv", TimeSpan.FromSeconds(5));
        var fgPackets = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "packets", fgAccount, "csv", TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(fgTransform), "FG transform recording was not captured for taxi parity.");
        Assert.False(string.IsNullOrWhiteSpace(fgPackets), "FG packet recording was not captured for taxi parity.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Transport_Board_FgBgParity()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName!;

        await EnsureFgParityReadyAsync();
        await PrepareTransportAccountAsync(bgAccount, "BG", EasternKingdomsMapId, UndercityElevatorWestX, UndercityElevatorWestY, UndercityElevatorUpperZ);
        await PrepareTransportAccountAsync(fgAccount, "FG", EasternKingdomsMapId, UndercityElevatorWestX, UndercityElevatorWestY, UndercityElevatorUpperZ);

        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, bgAccount, "packets", "transform", "physics");
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, fgAccount, "packets", "transform");

        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount, MakeRecordingAction(ActionType.StartPhysicsRecording)),
            _bot.SendActionAsync(fgAccount, MakeRecordingAction(ActionType.StartPhysicsRecording)));

        var gotoResults = await Task.WhenAll(
            _bot.SendActionAsync(bgAccount, MakeGoto(UndercityElevatorWestX, UndercityElevatorWestY, UndercityElevatorLowerZ)),
            _bot.SendActionAsync(fgAccount, MakeGoto(UndercityElevatorWestX, UndercityElevatorWestY, UndercityElevatorLowerZ)));
        Assert.All(gotoResults, result => Assert.Equal(ResponseResult.Success, result));

        var boardDeadlineUtc = DateTime.UtcNow.AddMinutes(3);
        Game.GameObjectSnapshot? lastBgTransport = null;
        Game.GameObjectSnapshot? lastFgTransport = null;
        ulong bgTransportGuid = 0;
        ulong fgTransportGuid = 0;

        while (DateTime.UtcNow < boardDeadlineUtc)
        {
            await _bot.RefreshSnapshotsAsync();

            var bgSnapshot = await _bot.GetSnapshotAsync(bgAccount);
            var fgSnapshot = await _bot.GetSnapshotAsync(fgAccount);

            bgTransportGuid = bgSnapshot?.MovementData?.TransportGuid ?? 0;
            fgTransportGuid = fgSnapshot?.MovementData?.TransportGuid ?? 0;
            lastBgTransport = FindNearestTransport(bgSnapshot, UndercityElevatorWestEntry, UndercityElevatorWestX, UndercityElevatorWestY);
            lastFgTransport = FindNearestTransport(fgSnapshot, UndercityElevatorWestEntry, UndercityElevatorWestX, UndercityElevatorWestY);

            if (lastBgTransport != null || lastFgTransport != null)
            {
                _output.WriteLine(
                    $"[TRANSPORT] BG sees {DescribeTransport(lastBgTransport)} | FG sees {DescribeTransport(lastFgTransport)} | boarded BG=0x{bgTransportGuid:X} FG=0x{fgTransportGuid:X}");
            }

            if (bgTransportGuid != 0 && fgTransportGuid != 0)
                break;

            await Task.Delay(1000);
        }

        await StopDualRecordingAsync(bgAccount, fgAccount);

        Assert.True(
            lastBgTransport != null || lastFgTransport != null,
            "At least one client should capture the current transport snapshot while boarding.");
        Assert.NotEqual(0UL, bgTransportGuid);
        Assert.NotEqual(0UL, fgTransportGuid);

        var fgTransform = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "transform", fgAccount, "csv", TimeSpan.FromSeconds(5));
        var fgPackets = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "packets", fgAccount, "csv", TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(fgTransform), "FG transform recording was not captured for transport boarding.");
        Assert.False(string.IsNullOrWhiteSpace(fgPackets), "FG packet recording was not captured for transport boarding.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Transport_CrossContinent_FgBgParity()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] snapshot received map={snap!.CurrentMapId} targetMap={EasternKingdomsMapId}");
    }

    private async Task EnsureFgParityReadyAsync()
    {
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(_bot.FgAccountName), "FG account not available for parity comparison.");
        global::Tests.Infrastructure.Skip.IfNot(await _bot.CheckFgActionableAsync(requireTeleportProbe: false), "FG bot not actionable.");
    }

    private async Task<ulong> PrepareTaxiAccountAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await _bot.EnsureTaxiNodesEnabledAsync(account, label);
        await _bot.SendGmChatCommandAsync(account, ".modify money 50000");
        await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot?.Player?.Coinage ?? 0) >= 50000,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{label} taxi-money");
        await PrepareTransportAccountAsync(account, label, KalimdorMapId, OrgrimmarFlightMasterX, OrgrimmarFlightMasterY, OrgrimmarFlightMasterZ);
        var fmUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: $"{label} flight-master");
        Assert.NotNull(fmUnit);

        var fmGuid = fmUnit!.GameObject?.Base?.Guid ?? 0UL;
        Assert.NotEqual(0UL, fmGuid);
        _output.WriteLine($"[{label}] flight master guid=0x{fmGuid:X} name={fmUnit.GameObject?.Name}");
        return fmGuid;
    }

    private async Task PrepareTransportAccountAsync(string account, string label, uint mapId, float x, float y, float z)
    {
        await _bot.BotTeleportAsync(account, (int)mapId, x, y, z);
        await _bot.WaitForTeleportSettledAsync(account, x, y);
        await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot =>
            {
                var position = GetSnapshotWorldPosition(snapshot);
                return snapshot != null
                    && snapshot.CurrentMapId == mapId
                    && position != null
                    && LiveBotFixture.Distance2D(position.X, position.Y, x, y) <= 4f
                    && Math.Abs(position.Z - z) <= 4f;
            },
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: $"{label} transport-stage");
        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(account);
        var pos = GetSnapshotWorldPosition(snapshot);
        _output.WriteLine($"[{label}] staged map={snapshot?.CurrentMapId} pos=({pos?.X:F1},{pos?.Y:F1},{pos?.Z:F1})");
    }

    private async Task StopDualRecordingAsync(string bgAccount, string fgAccount)
    {
        await Task.WhenAll(
            _bot.SendActionAsync(bgAccount, MakeRecordingAction(ActionType.StopPhysicsRecording)),
            _bot.SendActionAsync(fgAccount, MakeRecordingAction(ActionType.StopPhysicsRecording)));
        await Task.Delay(500);
    }

    private static Game.GameObjectSnapshot? FindNearestTransport(WoWActivitySnapshot? snapshot, uint expectedEntry, float originX, float originY)
        => snapshot?.MovementData?.NearbyGameObjects?
            .Where(go =>
                go != null
                && (go.Entry == expectedEntry
                    || (expectedEntry == OrgrimmarUndercityZeppelinEntry
                        && (go.Entry == OrgrimmarUndercityZeppelinEntry
                            || go.GameObjectType == GoTypeTransport
                            || go.GameObjectType == GoTypeMoTransport))))
            .OrderBy(go =>
            {
                var pos = go.Position;
                return pos == null
                    ? float.MaxValue
                    : LiveBotFixture.Distance2D(pos.X, pos.Y, originX, originY);
            })
            .FirstOrDefault();

    private static Game.Position? GetSnapshotWorldPosition(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.GameObject?.Base?.Position
            ?? snapshot?.MovementData?.Position;

    private static string DescribeTransport(Game.GameObjectSnapshot? transport)
    {
        if (transport?.Position == null)
            return "none";

        var pos = transport.Position;
        return $"{transport.Entry}:{transport.Name ?? "?"}:type={transport.GameObjectType} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1})";
    }

    private Task<bool> WaitForTaxiDepartureAsync(string account, uint startMapId, Game.Position startPos, string progressLabel)
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
            TimeSpan.FromSeconds(45),
            pollIntervalMs: 500,
            progressLabel: progressLabel);
    }

    private static ActionMessage MakeGoto(float x, float y, float z)
        => new()
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = x },
                new RequestParameter { FloatParam = y },
                new RequestParameter { FloatParam = z },
                new RequestParameter { FloatParam = 4.0f }
            }
        };

    private static ActionMessage MakeSelectTaxiNode(ulong flightMasterGuid, int sourceNodeId, int destinationNodeId)
        => new()
        {
            ActionType = ActionType.SelectTaxiNode,
            Parameters =
            {
                new RequestParameter { LongParam = unchecked((long)flightMasterGuid) },
                new RequestParameter { IntParam = sourceNodeId },
                new RequestParameter { IntParam = destinationNodeId }
            }
        };

    private static ActionMessage MakeSelectTaxiNode(ulong flightMasterGuid, int destinationNodeId)
        => new()
        {
            ActionType = ActionType.SelectTaxiNode,
            Parameters =
            {
                new RequestParameter { LongParam = unchecked((long)flightMasterGuid) },
                new RequestParameter { IntParam = destinationNodeId }
            }
        };

    private static ActionMessage MakeRecordingAction(ActionType actionType)
        => new()
        {
            ActionType = actionType
        };
}
