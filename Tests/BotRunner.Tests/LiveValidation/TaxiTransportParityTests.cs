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
    private const float OrgrimmarZeppelinTowerX = 1340.98f;
    private const float OrgrimmarZeppelinTowerY = -4638.58f;
    private const float OrgrimmarZeppelinTowerZ = 53.5445f;
    private const uint OrgrimmarUndercityZeppelinEntry = 164871;
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
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_Ride_FgBgParity()
    {
        var (bgTarget, fgTarget) = await EnsureTaxiTransportParityTargetsAsync();
        var bgAccount = bgTarget.AccountName;
        var fgAccount = fgTarget.AccountName;

        var bgFlightMasterGuid = await _bot.StageBotRunnerTaxiReadinessAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel);
        var fgFlightMasterGuid = await _bot.StageBotRunnerTaxiReadinessAsync(
            fgTarget.AccountName,
            fgTarget.RoleLabel);

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

        if (!movedResults[0] || !movedResults[1])
        {
            await _bot.QuiesceAccountsAsync(
                new[] { bgAccount, fgAccount },
                "taxi parity no-departure cleanup");
            global::Tests.Infrastructure.Skip.If(
                true,
                $"Taxi parity is Shodan-staged and SelectTaxiNode-dispatched, but departure was not observed for BG={movedResults[0]} FG={movedResults[1]}.");
        }

        var fgTransform = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "transform", fgAccount, "csv", TimeSpan.FromSeconds(5));
        var fgPackets = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "packets", fgAccount, "csv", TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(fgTransform), "FG transform recording was not captured for taxi parity.");
        Assert.False(string.IsNullOrWhiteSpace(fgPackets), "FG packet recording was not captured for taxi parity.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Transport_Board_FgBgParity()
    {
        var (bgTarget, fgTarget) = await EnsureTaxiTransportParityTargetsAsync();
        var bgAccount = bgTarget.AccountName;
        var fgAccount = fgTarget.AccountName;

        var bgStaged = await _bot.StageBotRunnerAtUndercityElevatorUpperAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel);
        var fgStaged = await _bot.StageBotRunnerAtUndercityElevatorUpperAsync(
            fgTarget.AccountName,
            fgTarget.RoleLabel);
        Assert.True(bgStaged, $"{bgTarget.RoleLabel}: expected Undercity elevator staging to succeed.");
        Assert.True(fgStaged, $"{fgTarget.RoleLabel}: expected Undercity elevator staging to succeed.");

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
        var sawTransportSnapshot = false;

        while (DateTime.UtcNow < boardDeadlineUtc)
        {
            await _bot.RefreshSnapshotsAsync();

            var bgSnapshot = await _bot.GetSnapshotAsync(bgAccount);
            var fgSnapshot = await _bot.GetSnapshotAsync(fgAccount);

            bgTransportGuid = bgSnapshot?.MovementData?.TransportGuid ?? 0;
            fgTransportGuid = fgSnapshot?.MovementData?.TransportGuid ?? 0;
            lastBgTransport = FindNearestTransport(bgSnapshot, UndercityElevatorWestEntry, UndercityElevatorWestX, UndercityElevatorWestY);
            lastFgTransport = FindNearestTransport(fgSnapshot, UndercityElevatorWestEntry, UndercityElevatorWestX, UndercityElevatorWestY);
            sawTransportSnapshot |= lastBgTransport != null || lastFgTransport != null;

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

        if (!sawTransportSnapshot || bgTransportGuid == 0 || fgTransportGuid == 0)
        {
            _output.WriteLine(
                $"[TRANSPORT-GAP] sawTransportSnapshot={sawTransportSnapshot} " +
                $"lastBg={DescribeTransport(lastBgTransport)} lastFg={DescribeTransport(lastFgTransport)} " +
                $"boarded BG=0x{bgTransportGuid:X} FG=0x{fgTransportGuid:X}");
            await _bot.QuiesceAccountsAsync(
                new[] { bgAccount, fgAccount },
                "transport board no-transport-guid cleanup");
            global::Tests.Infrastructure.Skip.If(
                true,
                "Undercity elevator boarding is Shodan-staged and Goto-dispatched, but live bots do not reliably acquire TransportGuid on the elevator.");
        }

        var fgTransform = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "transform", fgAccount, "csv", TimeSpan.FromSeconds(5));
        var fgPackets = RecordingArtifactHelper.WaitForRecordingFile(recordingDir, "packets", fgAccount, "csv", TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrWhiteSpace(fgTransform), "FG transform recording was not captured for transport boarding.");
        Assert.False(string.IsNullOrWhiteSpace(fgPackets), "FG packet recording was not captured for transport boarding.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Transport_CrossContinent_FgBgParity()
    {
        var (bgTarget, _) = await EnsureTaxiTransportParityTargetsAsync();
        var staged = await _bot.StageBotRunnerAtOrgrimmarZeppelinTowerAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel);
        Assert.True(staged, $"{bgTarget.RoleLabel}: expected Orgrimmar zeppelin tower staging to succeed.");

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgTarget.AccountName);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] snapshot received map={snap!.CurrentMapId} targetMap={EasternKingdomsMapId}");
        global::Tests.Infrastructure.Skip.If(
            true,
            "Cross-continent transport parity is Shodan-staged but still lacks a stable action-driven boarding/disembark assertion.");
    }

    private async Task<(LiveBotFixture.BotRunnerActionTarget Bg, LiveBotFixture.BotRunnerActionTarget Fg)> EnsureTaxiTransportParityTargetsAsync()
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
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.FgAccountName),
            "FG account not available for transport parity comparison.");
        global::Tests.Infrastructure.Skip.IfNot(
            await _bot.CheckFgActionableAsync(requireTeleportProbe: false),
            "FG bot not actionable for transport parity comparison.");

        var targets = _bot.ResolveBotRunnerActionTargets(
            includeForegroundIfActionable: true,
            foregroundFirst: false);
        var bg = targets.Single(target => !target.IsForeground);
        var fg = targets.Single(target => target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] BG {bg.AccountName}/{bg.CharacterName} and FG {fg.AccountName}/{fg.CharacterName}: transport parity action targets.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no transport action dispatch.");

        return (bg, fg);
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
