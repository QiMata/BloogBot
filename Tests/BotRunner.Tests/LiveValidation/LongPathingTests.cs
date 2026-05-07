using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Focused staged long-pathing validation. Setup is fixture-owned through
/// Shodan/test-director helpers; the BotRunner target receives only TravelTo.
/// </summary>
[Collection(LongPathingValidationCollection.Name)]
public class LongPathingTests
{
    private const int CrossroadsMapId = 1;
    private const float CrossroadsTaxiNodeX = -441.8f;
    private const float CrossroadsTaxiNodeY = -2596.08f;
    private const float CrossroadsFlightMasterX = -437.137f;
    private const float CrossroadsFlightMasterY = -2596.0f;
    private const float CrossroadsFlightMasterZ = 95.8708f;
    private const int OrgrimmarMapId = 1;
    private const float OrgrimmarTaxiX = 1677.0f;
    private const float OrgrimmarTaxiY = -4315.0f;
    // Phase 5.3.5: anchored to Zeppelin Master Frezza (NPC 9564) on the OG
    // upper-platform deck — same Z tier as BoardingPosition (z≈53.6), not the
    // prior wrong-tier city-ground point (z=51.6).
    private const float OrgrimmarZeppelinRouteTargetX = 1331.11f;
    private const float OrgrimmarZeppelinRouteTargetY = -4649.45f;
    private const float OrgrimmarZeppelinRouteTargetZ = 53.6269f;
    private const float OrgrimmarZeppelinX = 1320.142944f;
    private const float OrgrimmarZeppelinY = -4653.158691f;
    private const float OrgrimmarZeppelinBoardingX = 1320.142944f;
    private const float OrgrimmarZeppelinBoardingY = -4653.158691f;
    private const float OrgrimmarZeppelinBoardingZ = 53.891945f;
    private const float OrgrimmarZeppelinBoardingFallZTolerance = 12f;
    private const float OrgrimmarUndercityZeppelinDeckOffsetX = -12.580913f;
    private const float OrgrimmarUndercityZeppelinDeckOffsetY = -7.983256f;
    private const float OrgrimmarUndercityZeppelinDeckOffsetZ = -16.398277f;
    private const float OrgrimmarUndercityZeppelinDeckOffsetXYTolerance = 3f;
    private const float OrgrimmarUndercityZeppelinDeckOffsetZTolerance = 2f;
    private const int UndercityMapId = 0;
    private const float UndercityZeppelinTowerX = 2066.911377f;
    private const float UndercityZeppelinTowerY = 290.113708f;
    private const float UndercityTargetX = 1584.07f;
    private const float UndercityTargetY = 241.987f;
    private const float UndercityTargetZ = -52.1534f;
    private const uint NpcFlagFlightMaster = (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER;
    private const long TravelCopper = 50000;
    private const float LongTravelStallMovementYards = 1.5f;
    private const string LongPathingConfigFileName = "LongPathing.config.json";
    private const string ExpectedTargetRace = "Tauren";
    private const string ExpectedTargetGender = "Male";
    private const string InjectionDisablePacketHooksEnvVar = "Injection__DisablePacketHooks";
    private const string DisablePacketHooksEnvVar = "WWOW_DISABLE_PACKET_HOOKS";
    private const string ManualZeppelinCoordCaptureEnvVar = "WWOW_TEST_MANUAL_ZEPPELIN_COORD_CAPTURE";
    private const string LongPathingTimelineEnvVar = "WWOW_LONG_PATHING_TIMELINE";
    private const string OgDeckAnchorVerifyEnvVar = "WWOW_OG_DECK_ANCHOR_VERIFY";
    private const string OgRampClimbEnvVar = "WWOW_OG_RAMP_CLIMB_TEST";
    // Phase 5.3.6 cadence diagnostic (PFS-OVERHAUL-006). Read by BotRunner's
    // NavigationPathFactory.Create — when set to a positive int N,
    // NavigationPath emits [TRAVEL_WAYPOINT_REACHED] every Nth advance.
    private const string WaypointCadenceEnvVar = "WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS";
    private const int TimelinePollSampleEveryN = 10;
    private const int OrgrimmarUndercityZeppelinTripSeconds = 300;
    private const int OrgrimmarUndercityZeppelinDockWaitSeconds = 120;
    private static readonly TimeSpan LongTravelStallTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan KnownBlockerDwellTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ZeppelinTransferEvidenceTimeout = TimeSpan.FromSeconds(
        OrgrimmarUndercityZeppelinTripSeconds + OrgrimmarUndercityZeppelinDockWaitSeconds);

    private readonly LongPathingFixture _bot;
    private readonly ITestOutputHelper _output;

    public LongPathingTests(LongPathingFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task OrgrimmarToUndercityZeppelin_BoardsAndDeplanes()
    {
        using var packetHookScope = DisableForegroundPacketHooksForCrossMapTransfers();
        var target = await EnsureLongPathingTargetAsync();

        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            OrgrimmarMapId,
            OrgrimmarZeppelinBoardingX,
            OrgrimmarZeppelinBoardingY,
            OrgrimmarZeppelinBoardingZ,
            "Orgrimmar zeppelin boarding point",
            cleanSlate: true,
            xyToleranceYards: 12f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar zeppelin boarding staging to settle.");

        await _bot.QuiesceAccountsAsync([target.AccountName], $"{target.RoleLabel} zeppelin boarding start");
        await _bot.RefreshSnapshotsAsync();
        var diagnosticBaseline = (await _bot.GetSnapshotAsync(target.AccountName))?.RecentChatMessages.ToArray()
            ?? Array.Empty<string>();

        if (IsManualZeppelinCoordCapture())
        {
            _output.WriteLine(
                $"[MANUAL] {target.RoleLabel} staged at Orgrimmar zeppelin boarding point. " +
                "No TravelTo action will be dispatched while manual coordinate capture is enabled.");

            await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                _ => false,
                GetZeppelinTransferEvidenceTimeout(),
                pollIntervalMs: 5000,
                progressLabel: $"{target.RoleLabel} manual zeppelin coordinate capture");
            return;
        }

        var dispatch = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = UndercityMapId },
                new RequestParameter { FloatParam = UndercityTargetX },
                new RequestParameter { FloatParam = UndercityTargetY },
                new RequestParameter { FloatParam = UndercityTargetZ }
            }
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        var startedZeppelinLeg = await WaitForTravelDiagnosticAsync(
            target.AccountName,
            message => message.Contains("[TRAVEL_LEG] start", StringComparison.Ordinal)
                && message.Contains("type=Zeppelin", StringComparison.Ordinal),
            diagnosticBaseline,
            TimeSpan.FromSeconds(45),
            $"{target.RoleLabel} direct zeppelin leg start");
        await AssertOrScreenshotAsync(
            startedZeppelinLeg,
            target.AccountName,
            "Expected direct Orgrimmar staging to start the Orgrimmar -> Undercity zeppelin leg.");

        var boardedOrTransferred = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                FailIfZeppelinBoardingLost(snapshot, target.AccountName, diagnosticBaseline);
                return snapshot.CurrentMapId == UndercityMapId || IsOnOrgrimmarUndercityZeppelinDeck(snapshot);
            },
            GetZeppelinTransferEvidenceTimeout(),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} direct zeppelin boarding");
        await AssertOrScreenshotAsync(
            boardedOrTransferred,
            target.AccountName,
            "Expected the staged bot to board the Orgrimmar -> Undercity zeppelin.");

        var deplaned = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
                snapshot.CurrentMapId == UndercityMapId
                && !IsOnTransport(snapshot)
                && (IsNear(snapshot, UndercityMapId, UndercityZeppelinTowerX, UndercityZeppelinTowerY, 90f)
                    || HasTransportArrivedDiagnostic(snapshot)),
            GetZeppelinTransferEvidenceTimeout(),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} direct zeppelin deplane");
        await AssertOrScreenshotAsync(
            deplaned,
            target.AccountName,
            "Expected the staged bot to deplane at the Undercity zeppelin tower.");

        await _bot.QuiesceAccountsAsync([target.AccountName], $"{target.RoleLabel} direct zeppelin deplaned");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task CrossroadsToUndercity_UsesFlightAndZeppelin()
    {
        const string TimelineTestName = nameof(CrossroadsToUndercity_UsesFlightAndZeppelin);
        using var packetHookScope = DisableForegroundPacketHooksForCrossMapTransfers();
        var target = await EnsureLongPathingTargetAsync();

        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            CrossroadsMapId,
            CrossroadsFlightMasterX,
            CrossroadsFlightMasterY,
            CrossroadsFlightMasterZ,
            "Crossroads flight master",
            cleanSlate: true,
            xyToleranceYards: 20f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, $"{target.RoleLabel}: expected Crossroads staging to settle.");

        await _bot.StageBotRunnerCoinageAsync(target.AccountName, target.RoleLabel, TravelCopper);
        await _bot.EnsureTaxiNodesEnabledAsync(target.AccountName, target.RoleLabel);
        await _bot.QuiesceAccountsAsync([target.AccountName], $"{target.RoleLabel} Crossroads long-path start");
        await _bot.RefreshSnapshotsAsync();
        var diagnosticBaseline = (await _bot.GetSnapshotAsync(target.AccountName))?.RecentChatMessages.ToArray()
            ?? Array.Empty<string>();

        var flightMaster = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            NpcFlagFlightMaster,
            timeoutMs: 15000,
            progressLabel: $"{target.RoleLabel} Crossroads flight-master");
        Assert.NotNull(flightMaster);

        await _bot.RefreshSnapshotsAsync();
        var start = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.True(IsNear(start, CrossroadsMapId, CrossroadsTaxiNodeX, CrossroadsTaxiNodeY, 80f), DescribeSnapshot(start, "start"));
        CaptureTimelineCheckpoint(TimelineTestName, "01-flight-master-discovered", target.AccountName, start);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: TravelTo Crossroads -> Undercity staged route.");
        var idleRole = target.IsForeground ? "BG" : "FG";
        var idleAccount = target.IsForeground ? _bot.BgAccountName : _bot.FgAccountName;
        var idleCharacter = target.IsForeground ? _bot.BgCharacterName : _bot.FgCharacterName;
        _output.WriteLine(
            $"[ACTION-PLAN] {idleRole} {idleAccount}/{idleCharacter}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no TravelTo dispatch.");

        var dispatch = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = UndercityMapId },
                new RequestParameter { FloatParam = UndercityTargetX },
                new RequestParameter { FloatParam = UndercityTargetY },
                new RequestParameter { FloatParam = UndercityTargetZ }
            }
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        var planSeen = await WaitForTravelDiagnosticAsync(
            target.AccountName,
            message => message.Contains("[TRAVEL_PLAN]", StringComparison.Ordinal)
                && message.Contains("FlightPath(25->23)", StringComparison.Ordinal)
                && message.Contains("Zeppelin", StringComparison.Ordinal),
            diagnosticBaseline,
            TimeSpan.FromSeconds(45),
            $"{target.RoleLabel} staged travel plan");
        await AssertOrScreenshotAsync(
            planSeen,
            target.AccountName,
            "TravelTo should emit a staged plan containing Crossroads taxi 25->23 and a zeppelin leg.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "02-plan-seen",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var taxiDeparted = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                var position = GetPosition(snapshot);
                if (snapshot == null || position == null)
                    return false;

                var movedFromCrossroads = LiveBotFixture.Distance2D(position.X, position.Y, CrossroadsTaxiNodeX, CrossroadsTaxiNodeY) >= 80f;
                return movedFromCrossroads || IsOnTransport(snapshot) || IsNear(snapshot, OrgrimmarMapId, OrgrimmarTaxiX, OrgrimmarTaxiY, 250f);
            },
            TimeSpan.FromMinutes(3),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} Crossroads taxi departure");
        await AssertOrScreenshotAsync(
            taxiDeparted,
            target.AccountName,
            "Expected taxi departure or movement away from Crossroads.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "03-taxi-departed",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var reachedOrgrimmarTaxi = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => IsNear(snapshot, OrgrimmarMapId, OrgrimmarTaxiX, OrgrimmarTaxiY, 35f) && !IsOnTransport(snapshot),
            TimeSpan.FromMinutes(4),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} Orgrimmar taxi arrival");
        await AssertOrScreenshotAsync(
            reachedOrgrimmarTaxi,
            target.AccountName,
            "Expected the Crossroads -> Orgrimmar taxi leg to land near the Orgrimmar flight master.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "04-reached-orgrimmar-taxi",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var flightLegCompleted = await WaitForTravelDiagnosticAsync(
            target.AccountName,
            IsCrossroadsToOrgrimmarFlightCompleteDiagnostic,
            diagnosticBaseline,
            TimeSpan.FromSeconds(150),
            $"{target.RoleLabel} Crossroads -> Orgrimmar flight completion");
        await AssertOrScreenshotAsync(
            flightLegCompleted,
            target.AccountName,
            "Expected TravelTask to complete the Crossroads -> Orgrimmar flight leg before starting the Orgrimmar walk.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "05-flight-leg-completed",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var sawOrgrimmarPathfindingWalk = await WaitForTravelDiagnosticAsync(
            target.AccountName,
            message => IsPathfindingWalkDiagnosticFor(
                message,
                OrgrimmarZeppelinRouteTargetX,
                OrgrimmarZeppelinRouteTargetY,
                OrgrimmarZeppelinRouteTargetZ),
            diagnosticBaseline,
            TimeSpan.FromSeconds(90),
            $"{target.RoleLabel} Orgrimmar pathfinding walk");
        await AssertOrScreenshotAsync(
            sawOrgrimmarPathfindingWalk,
            target.AccountName,
            "Expected TravelTask to use a PathfindingService-generated route from Orgrimmar flight-master area to the zeppelin tower.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "06-saw-orgrimmar-pathfinding-walk",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var zeppelinStallGuard = new SnapshotStallGuard(
            "Orgrimmar flight master -> zeppelin tower",
            LongTravelStallTimeout,
            LongTravelStallMovementYards);
        var zeppelinBlockerGuard = new LongPathingRouteBlockerGuard(
            KnownBlockerDwellTimeout,
            LongTravelStallMovementYards);
        var orgWalkPollCounter = 0;
        var reachedZeppelinTower = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                orgWalkPollCounter++;
                if (orgWalkPollCounter % TimelinePollSampleEveryN == 0)
                    CaptureTimelineCheckpoint(
                        TimelineTestName,
                        $"07a-orgrimmar-walk-poll-{orgWalkPollCounter:D5}",
                        target.AccountName,
                        snapshot);

                if (snapshot?.CurrentMapId == OrgrimmarMapId && !IsOnTransport(snapshot))
                {
                    zeppelinBlockerGuard.FailIfBlocked(
                        snapshot,
                        (message, blockerSnapshot) =>
                        {
                            CaptureTimelineCheckpoint(
                                TimelineTestName,
                                "07b-orgrimmar-walk-blocker-fire",
                                target.AccountName,
                                blockerSnapshot);
                            FailWithScreenshot(message, target.AccountName, blockerSnapshot);
                        });

                    if (IsNearZeppelinDeckApproach(snapshot))
                        return true;

                    zeppelinStallGuard.FailIfStalled(
                        snapshot,
                        (message, stallSnapshot) =>
                        {
                            CaptureTimelineCheckpoint(
                                TimelineTestName,
                                "07c-orgrimmar-walk-stall-fire",
                                target.AccountName,
                                stallSnapshot);
                            FailWithScreenshot(message, target.AccountName, stallSnapshot);
                        });
                }
                else
                {
                    zeppelinBlockerGuard.Reset();
                    zeppelinStallGuard.Reset();
                }

                return false;
            },
            TimeSpan.FromMinutes(4),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} Orgrimmar zeppelin staging");
        await AssertOrScreenshotAsync(
            reachedZeppelinTower,
            target.AccountName,
            "Expected normal walking from Orgrimmar flight master to the zeppelin tower.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "07-reached-zeppelin-tower",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var startedZeppelinLeg = await WaitForTravelDiagnosticAsync(
            target.AccountName,
            message => message.Contains("[TRAVEL_LEG] start", StringComparison.Ordinal)
                && message.Contains("type=Zeppelin", StringComparison.Ordinal),
            diagnosticBaseline,
            TimeSpan.FromMinutes(3),
            $"{target.RoleLabel} zeppelin leg start");
        await AssertOrScreenshotAsync(
            startedZeppelinLeg,
            target.AccountName,
            "Expected TravelTask to finish tower approach and start the Orgrimmar -> Undercity zeppelin leg.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "08-started-zeppelin-leg",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        // Phase 5.3.5: tight fail-fast stuck-detection during boarding evidence
        // poll. Prior runs wasted 7 minutes per cycle waiting for a docked
        // zeppelin while the bot sat stationary because of a navmesh
        // corner-cutting issue. The boardingStuckGuard fires after 30s of
        // sub-yard movement, captures a screenshot via FailWithScreenshot,
        // and aborts. Movement threshold 1.5y matches LongTravelStallMovementYards.
        var boardingStuckGuard = new SnapshotStallGuard(
            "Orgrimmar zeppelin boarding window (stuck on tower deck)",
            TimeSpan.FromSeconds(30),
            LongTravelStallMovementYards);
        var zeppelinEvidencePollCounter = 0;
        var sawZeppelinEvidence = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                zeppelinEvidencePollCounter++;
                if (zeppelinEvidencePollCounter % TimelinePollSampleEveryN == 0)
                    CaptureTimelineCheckpoint(
                        TimelineTestName,
                        $"09a-zeppelin-evidence-poll-{zeppelinEvidencePollCounter:D5}",
                        target.AccountName,
                        snapshot);

                FailIfZeppelinBoardingLost(snapshot, target.AccountName, diagnosticBaseline);

                // If the bot is stuck (no XY movement) AND not yet on the
                // transport, fail fast with a screenshot. Once attached
                // (isOnTransport=true), the bot is supposed to stay stationary
                // relative to world coords during the ride, so the guard is
                // skipped post-attachment.
                if (!IsOnTransport(snapshot))
                    boardingStuckGuard.FailIfStalled(
                        snapshot,
                        (message, stallSnapshot) => FailWithScreenshot(message, target.AccountName, stallSnapshot));
                else
                    boardingStuckGuard.Reset();

                return snapshot?.CurrentMapId == UndercityMapId
                    || IsOnTransport(snapshot);
            },
            ZeppelinTransferEvidenceTimeout,
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} zeppelin transfer evidence");
        await AssertOrScreenshotAsync(
            sawZeppelinEvidence,
            target.AccountName,
            "Expected the bot to board the Orgrimmar -> Undercity zeppelin or complete the cross-map transfer.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "09-saw-zeppelin-evidence",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var arrivedEasternKingdoms = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => snapshot?.CurrentMapId == UndercityMapId,
            TimeSpan.FromMinutes(4),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} Eastern Kingdoms arrival");
        await AssertOrScreenshotAsync(
            arrivedEasternKingdoms,
            target.AccountName,
            "Expected the Orgrimmar -> Undercity zeppelin to transfer to Eastern Kingdoms.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "10-arrived-eastern-kingdoms",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var sawUndercityPathfindingWalk = await WaitForTravelDiagnosticAsync(
            target.AccountName,
            message => IsPathfindingWalkDiagnosticFor(
                message,
                UndercityTargetX,
                UndercityTargetY,
                UndercityTargetZ),
            diagnosticBaseline,
            TimeSpan.FromSeconds(45),
            $"{target.RoleLabel} Undercity pathfinding walk");
        await AssertOrScreenshotAsync(
            sawUndercityPathfindingWalk,
            target.AccountName,
            "Expected TravelTask to use a PathfindingService-generated route from Undercity zeppelin arrival to the Undercity target.");
        await _bot.RefreshSnapshotsAsync();
        CaptureTimelineCheckpoint(
            TimelineTestName,
            "11-saw-undercity-pathfinding-walk",
            target.AccountName,
            await _bot.GetSnapshotAsync(target.AccountName));

        var undercityStallGuard = new SnapshotStallGuard(
            "Undercity zeppelin tower -> final destination",
            LongTravelStallTimeout,
            LongTravelStallMovementYards);
        var arrivedUndercity = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                if (IsNear(snapshot, UndercityMapId, UndercityTargetX, UndercityTargetY, 80f))
                    return true;

                if (snapshot.CurrentMapId == UndercityMapId && !IsOnTransport(snapshot))
                    undercityStallGuard.FailIfStalled(
                        snapshot,
                        (message, stallSnapshot) => FailWithScreenshot(message, target.AccountName, stallSnapshot));
                else
                    undercityStallGuard.Reset();

                return false;
            },
            TimeSpan.FromMinutes(5),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} Undercity final arrival");

        await _bot.RefreshSnapshotsAsync();
        var finalSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        if (!arrivedUndercity)
            FailWithScreenshot(DescribeSnapshot(finalSnapshot, "final Undercity target"), target.AccountName, finalSnapshot);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task ClimbOrgrimmarZeppelinTowerRampToFrezza()
    {
        // Phase 5.3.5 focused sub-test (PFS-OVERHAUL-005). Isolates the
        // "Going up zeppelin tower" phase from the full Crossroads→Undercity
        // pipeline. Teleports the bot to the OG flight master tower top
        // (where a real test would arrive after the flight leg) and dispatches
        // a TravelTo to the Undercity destination. Asserts the bot's walk leg
        // climbs the wooden ramp UP to within 12y of Zeppelin Master Frezza
        // (1331.11,-4649.45,53.6269) within a 180-second budget (Phase 5.3.6:
        // budget bumped from 90s after the FindPathCornersForAgent diagnostic
        // proved Detour serves a 96+ corner path that descends through OG
        // city sea level before climbing the OG zeppelin tower's external
        // spiral ramp; 90s was insufficient simply for traversal time, before
        // any corner-completion problems). Fails fast with a screenshot if
        // the bot stalls anywhere along the climb — this is the failing
        // slice from the full live test, isolated for fast diagnostic cycles.
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(OgRampClimbEnvVar),
                "1",
                StringComparison.Ordinal),
            $"OG ramp climb sub-test disabled (set {OgRampClimbEnvVar}=1).");

        const string TimelineTestName = nameof(ClimbOrgrimmarZeppelinTowerRampToFrezza);
        using var packetHookScope = DisableForegroundPacketHooksForCrossMapTransfers();
        var target = await EnsureLongPathingTargetAsync();

        using var timelineScope = new EnvironmentVariableScope(LongPathingTimelineEnvVar);
        Environment.SetEnvironmentVariable(LongPathingTimelineEnvVar, "1");

        // Phase 5.3.6 cadence diagnostic (PFS-OVERHAUL-006). Default to "2"
        // (every 2nd waypoint advance emits [TRAVEL_WAYPOINT_REACHED]) only when
        // the caller hasn't already set a value — lets the run command override.
        // Note: BotRunner reads this env var at process start (or at NavigationPath
        // construction). Setting it here only affects the test process; for
        // BotRunner subprocesses launched before this point, the existing
        // ambient env var (set in the test launcher / shell) is what counts.
        using var cadenceScope = new EnvironmentVariableScope(WaypointCadenceEnvVar);
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WaypointCadenceEnvVar)))
            Environment.SetEnvironmentVariable(WaypointCadenceEnvVar, "2");

        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        // Frezza spawn (Zeppelin Master, NPC 9564) on the OG upper-platform deck.
        const float FrezzaX = 1331.11f;
        const float FrezzaY = -4649.45f;
        const float FrezzaZ = 53.6269f;
        const float FrezzaArrivalRadius = 12f;

        // Teleport the bot to the OG flight master tower top — the natural
        // post-flight starting position for the OG→UC zeppelin walk leg.
        await _bot.BotTeleportAsync(target.AccountName, OrgrimmarMapId, OrgrimmarTaxiX, OrgrimmarTaxiY, 62.0f);
        await Task.Delay(2500);
        await _bot.RefreshSnapshotsAsync();
        var startSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(TimelineTestName, "01-teleported-flight-master", target.AccountName, startSnapshot);

        var diagnosticBaseline = startSnapshot?.RecentChatMessages.ToArray() ?? Array.Empty<string>();

        // Dispatch TravelTo Undercity destination — same as the full test, so
        // the route planner emits the same Walk-leg-to-Frezza we're isolating.
        var dispatch = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = UndercityMapId },
                new RequestParameter { FloatParam = UndercityTargetX },
                new RequestParameter { FloatParam = UndercityTargetY },
                new RequestParameter { FloatParam = UndercityTargetZ },
            },
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        // Tight stuck guard: 20s of sub-1.5y movement → fail fast with screenshot.
        var stuckGuard = new SnapshotStallGuard(
            "OG zeppelin tower ramp climb (stalled before reaching Frezza)",
            TimeSpan.FromSeconds(20),
            LongTravelStallMovementYards);

        var pollCounter = 0;
        var seenWaypointDiagMessages = new HashSet<string>(StringComparer.Ordinal);
        var reachedFrezza = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                pollCounter++;
                if (pollCounter % TimelinePollSampleEveryN == 0)
                    CaptureTimelineCheckpoint(
                        TimelineTestName,
                        $"02-climb-poll-{pollCounter:D5}",
                        target.AccountName,
                        snapshot);

                // Phase 5.3.6 (PFS-OVERHAUL-006): one PNG+JSON per
                // [TRAVEL_WAYPOINT_REACHED] BotRunner emits — cadence is
                // gated by WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS in the
                // BotRunner process. Label uses the adv= counter so traces
                // align with NavigationPath's advance count.
                foreach (var msg in GetDeltaMessages(diagnosticBaseline, snapshot?.RecentChatMessages))
                {
                    if (msg.IndexOf("[TRAVEL_WAYPOINT_REACHED]", StringComparison.Ordinal) < 0)
                        continue;
                    if (!seenWaypointDiagMessages.Add(msg))
                        continue;
                    var advMatch = System.Text.RegularExpressions.Regex.Match(msg, @"adv=(\d+)");
                    var label = advMatch.Success
                        ? $"wp-{int.Parse(advMatch.Groups[1].Value):D5}"
                        : $"wp-msg-{seenWaypointDiagMessages.Count:D5}";
                    CaptureTimelineCheckpoint(TimelineTestName, label, target.AccountName, snapshot);
                }

                stuckGuard.FailIfStalled(
                    snapshot,
                    (message, stallSnapshot) => FailWithScreenshot(message, target.AccountName, stallSnapshot));

                var pos = GetPosition(snapshot);
                if (pos == null)
                    return false;

                var dx = pos.X - FrezzaX;
                var dy = pos.Y - FrezzaY;
                var dz = MathF.Abs(pos.Z - FrezzaZ);
                var dist2D = MathF.Sqrt(dx * dx + dy * dy);
                // Phase 5.3.6 (PFS-OVERHAUL-006): tightened dz<=6 → dz<=2 to enforce
                // same-deck arrival. Cycle 3 full test exposed that dz<=6 false-positives
                // when the bot stops at z≈50 (lower spiral coil) — XY-close to Frezza
                // but on the wrong vertical layer for boarding. dz<=2 forces the bot
                // to actually crest the upper deck (z≈53.6) where Frezza & BoardingPosition live.
                return snapshot?.CurrentMapId == OrgrimmarMapId
                    && dist2D <= FrezzaArrivalRadius
                    && dz <= 2f;
            },
            TimeSpan.FromSeconds(180),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} OG zeppelin tower ramp climb to Frezza");

        await _bot.RefreshSnapshotsAsync();
        var finalSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(TimelineTestName, "03-final", target.AccountName, finalSnapshot);

        await AssertOrScreenshotAsync(
            reachedFrezza,
            target.AccountName,
            $"Expected bot to climb the OG zeppelin tower ramp to within {FrezzaArrivalRadius}y of "
            + $"Zeppelin Master Frezza ({FrezzaX},{FrezzaY},{FrezzaZ}) within 180s. "
            + "If bot stalls early, check NavigationPath corner-completion logic — Phase 5.3.6 "
            + "candidate fix is Facing-based waypoint completion instead of pure radius check. "
            + $"Cadence diagnostic captured {seenWaypointDiagMessages.Count} [TRAVEL_WAYPOINT_REACHED] "
            + "events in the timeline directory.");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task OgZeppelinDeckAnchorVerification()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(OgDeckAnchorVerifyEnvVar),
                "1",
                StringComparison.Ordinal),
            $"Phase 5.3.1 anchor verification disabled (set {OgDeckAnchorVerifyEnvVar}=1).");

        const string TimelineTestName = nameof(OgZeppelinDeckAnchorVerification);
        var target = await EnsureLongPathingTargetAsync();

        using var timelineScope = new EnvironmentVariableScope(LongPathingTimelineEnvVar);
        Environment.SetEnvironmentVariable(LongPathingTimelineEnvVar, "1");

        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        var captures = new (string Label, int MapId, float X, float Y, float Z)[]
        {
            ("c1-upper-platform-z96",    1, 1330.66f,  -4656.03f, 96.29f),
            ("c2-gangplank-end-z98",     1, 1315.33f,  -4650.00f, 98.54f),
            ("c3-approach-z51",          1, 1338.10f,  -4646.00f, 51.60f),
            ("c4-boarding-z53",          1, 1320.14f,  -4653.16f, 53.89f),
            ("c5-zeppelin-model-z71",    1, 1318.107f, -4658.047f, 71.86f),
            ("c6-ramp-mid-probe-z65",    1, 1325.00f,  -4649.00f, 65.00f),
            ("c7-stair-top-probe-z70",   1, 1322.00f,  -4651.00f, 70.00f),
        };

        foreach (var c in captures)
        {
            _output.WriteLine(
                $"[ANCHOR-VERIFY] tele {target.AccountName} -> {c.Label} map={c.MapId} ({c.X:F2},{c.Y:F2},{c.Z:F2})");
            await _bot.BotTeleportAsync(target.AccountName, c.MapId, c.X, c.Y, c.Z);

            // Allow falling, slope-slide, and z-settle to finish before sampling.
            await Task.Delay(4000);
            await _bot.RefreshSnapshotsAsync();
            var settled = await _bot.GetSnapshotAsync(target.AccountName);

            CaptureTimelineCheckpoint(TimelineTestName, c.Label, target.AccountName, settled);

            var pos = GetPosition(settled);
            _output.WriteLine(
                pos == null
                    ? $"[ANCHOR-VERIFY] {c.Label}: snapshot position unavailable"
                    : $"[ANCHOR-VERIFY] {c.Label}: settled=({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) " +
                      $"deltaXY={LiveBotFixture.Distance2D(pos.X, pos.Y, c.X, c.Y):F2} dz={pos.Z - c.Z:F2}");
        }
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureLongPathingTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", LongPathingConfigFileName);

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            $"Shodan director was not launched by {LongPathingConfigFileName}.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: true,
                foregroundFirst: true)
            .Single(target => target.IsForeground);

        AssertConfiguredTaurenMaleTarget(settingsPath, target.AccountName);
        _output.WriteLine(
            $"[LONG-PATHING-TARGET] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: {ExpectedTargetRace} {ExpectedTargetGender} foreground target.");
        return target;
    }

    private static EnvironmentVariableScope DisableForegroundPacketHooksForCrossMapTransfers()
    {
        var scope = new EnvironmentVariableScope(
            InjectionDisablePacketHooksEnvVar,
            DisablePacketHooksEnvVar);

        // Foreground packet hooks are unstable during world transfers; keep this
        // active for the full test so any StateManager relaunch inherits it.
        Environment.SetEnvironmentVariable(InjectionDisablePacketHooksEnvVar, "true");
        Environment.SetEnvironmentVariable(DisablePacketHooksEnvVar, "1");
        return scope;
    }

    private Task<bool> WaitForTravelDiagnosticAsync(
        string account,
        Func<string, bool> predicate,
        IReadOnlyList<string> baselineMessages,
        TimeSpan timeout,
        string progressLabel)
        => _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => GetDeltaMessages(baselineMessages, snapshot?.RecentChatMessages).Any(predicate),
            timeout,
            pollIntervalMs: 500,
            progressLabel: progressLabel);

    private async Task AssertOrScreenshotAsync(bool condition, string account, string message)
    {
        if (condition)
            return;

        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(account);
        FailWithScreenshot(message, account, snapshot);
    }

    private void FailWithScreenshot(string message, string account, WoWActivitySnapshot? snapshot)
    {
        var screenshotEvidence = CaptureFailureScreenshot(message, account);
        Assert.Fail($"{message}{Environment.NewLine}{DescribeSnapshot(snapshot, "failure")}{Environment.NewLine}Screenshot evidence: {screenshotEvidence}");
    }

    private string CaptureFailureScreenshot(string label, string account)
    {
        var repoRoot = ResolveRepoRoot();
        var screenshotDir = Path.Combine(repoRoot, "tmp", "test-runtime", "screenshots", "long-pathing");
        Directory.CreateDirectory(screenshotDir);

        var safeLabel = SanitizeScreenshotLabel(label);
        var targetPid = ResolveManagedWowProcessId(account);
        if (targetPid == null)
            return $"no managed WoW PID found for account {account}; dir={screenshotDir}";

        var script = $$"""
        $outDir = '{{EscapePowerShellSingleQuotedString(screenshotDir)}}'
        $label = '{{EscapePowerShellSingleQuotedString(safeLabel)}}'
        $account = '{{EscapePowerShellSingleQuotedString(account)}}'
        $targetPid = {{targetPid.Value.ToString(CultureInfo.InvariantCulture)}}
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
        Add-Type -AssemblyName System.Windows.Forms
        Add-Type -AssemblyName System.Drawing
        Add-Type @'
        using System;
        using System.Runtime.InteropServices;
        public static class Win32Shot {
            [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
            public sealed class WindowInfo {
                public IntPtr Handle;
                public string Title = "";
                public string ClassName = "";
                public RECT Rect;
            }
            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
            [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
            [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
            [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
            [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder text, int count);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
            public static WindowInfo[] GetTopLevelWindowsForProcess(int pid) {
                var windows = new System.Collections.Generic.List<WindowInfo>();
                EnumWindows((hWnd, lParam) => {
                    uint windowPid;
                    GetWindowThreadProcessId(hWnd, out windowPid);
                    if (windowPid != (uint)pid || !IsWindowVisible(hWnd)) return true;
                    RECT rect;
                    if (!GetWindowRect(hWnd, out rect)) return true;
                    if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return true;
                    var title = new System.Text.StringBuilder(256);
                    var className = new System.Text.StringBuilder(128);
                    GetWindowText(hWnd, title, title.Capacity);
                    GetClassName(hWnd, className, className.Capacity);
                    windows.Add(new WindowInfo { Handle = hWnd, Title = title.ToString(), ClassName = className.ToString(), Rect = rect });
                    return true;
                }, IntPtr.Zero);
                return windows.ToArray();
            }
        }
        '@
        $captured = @()
        $metadata = @()
        $proc = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
        if ($null -eq $proc -or $proc.ProcessName -ne 'WoW') {
          "SCREENSHOT_ERROR: target process for account '$account' is not an active WoW.exe pid=$targetPid"
          exit 2
        }
        $clientWindows = @([Win32Shot]::GetTopLevelWindowsForProcess($proc.Id) | Where-Object {
          $_.ClassName -eq 'GxWindowClassD3d' -or $_.Title -eq 'World of Warcraft'
        } | Sort-Object @{ Expression = { if ($_.ClassName -eq 'GxWindowClassD3d') { 0 } else { 1 } } }, @{ Expression = { $_.Title } })
        if ($clientWindows.Count -eq 0) {
          "SCREENSHOT_ERROR: no visible WoW client window found for account '$account' pid=$targetPid"
          exit 3
        }
        $HWND_TOPMOST = [IntPtr]::new(-1)
        $HWND_NOTOPMOST = [IntPtr]::new(-2)
        $SW_RESTORE = 9
        $SWP_NOMOVE = 0x0002
        $SWP_NOSIZE = 0x0001
        $SWP_SHOWWINDOW = 0x0040
        $windowIndex = 0
        foreach ($window in $clientWindows) {
          $rect = $window.Rect
          $width = [int]($rect.Right - $rect.Left)
          $height = [int]($rect.Bottom - $rect.Top)
          if ($width -le 0 -or $height -le 0) { continue }
          [Win32Shot]::ShowWindow($window.Handle, $SW_RESTORE) | Out-Null
          [Win32Shot]::SetWindowPos($window.Handle, $HWND_TOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_SHOWWINDOW) | Out-Null
          [Win32Shot]::SetForegroundWindow($window.Handle) | Out-Null
          [Win32Shot]::SetWindowPos($window.Handle, $HWND_NOTOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_SHOWWINDOW) | Out-Null
          $bmp = New-Object System.Drawing.Bitmap $width, $height
          $gfx = [System.Drawing.Graphics]::FromImage($bmp)
          try {
              $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
              $path = Join-Path $outDir ("$label-$account-client-$($proc.Id)-win$windowIndex-$timestamp.png")
              $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
              $resolved = (Resolve-Path -LiteralPath $path).Path
              $captured += $resolved
              $metadata += "$resolved account='$account' pid=$($proc.Id) title='$($window.Title)' class='$($window.ClassName)' rect=($($rect.Left),$($rect.Top),$width,$height)"
          }
          finally {
              $gfx.Dispose()
              $bmp.Dispose()
          }
          $windowIndex++
        }
        if ($captured.Count -eq 0) {
          "SCREENSHOT_ERROR: no captureable WoW client window found for account '$account' pid=$targetPid"
          exit 4
        }
        $captured | ForEach-Object { "SCREENSHOT: $_" }
        $metadata | ForEach-Object { "SCREENSHOT_META: $_" }
        """;

        try
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            using var process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-OutputFormat");
            process.StartInfo.ArgumentList.Add("Text");
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
            process.StartInfo.ArgumentList.Add("-EncodedCommand");
            process.StartInfo.ArgumentList.Add(encoded);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            if (!process.WaitForExit(15000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort cleanup for the short-lived screenshot process we started.
                }

                return $"screenshot capture timed out in {screenshotDir}";
            }

            var stdout = process.StandardOutput.ReadToEnd().Trim();
            var stderr = process.StandardError.ReadToEnd().Trim();
            if (!string.IsNullOrWhiteSpace(stdout))
                _output.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
                _output.WriteLine($"[SCREENSHOT-ERR] {stderr}");

            var paths = stdout.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("SCREENSHOT:", StringComparison.Ordinal))
                .Select(line => line["SCREENSHOT:".Length..].Trim())
                .ToArray();

            return paths.Length == 0
                ? $"no screenshot path reported; exit={process.ExitCode}; dir={screenshotDir}"
                : string.Join(", ", paths);
        }
        catch (Exception ex)
        {
            return $"screenshot capture failed: {ex.Message}; dir={screenshotDir}";
        }
    }

    private int? ResolveManagedWowProcessId(string account)
    {
        if (string.IsNullOrWhiteSpace(account))
            return null;

        var escapedAccount = Regex.Escape(account);
        var patterns = new[]
        {
            new Regex($@"WoW\.exe started for account\s+{escapedAccount}\s+\(Process ID:\s*(\d+)", RegexOptions.IgnoreCase),
            new Regex($@"Added\s+{escapedAccount}\s+to managed services with PID\s+(\d+)", RegexOptions.IgnoreCase),
        };

        foreach (var line in _bot.GetStateManagerOutput().Reverse())
        {
            foreach (var pattern in patterns)
            {
                var match = pattern.Match(line);
                if (match.Success
                    && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                {
                    return pid;
                }
            }
        }

        return null;
    }

    private static void AssertConfiguredTaurenMaleTarget(string settingsPath, string account)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var target = document.RootElement.EnumerateArray()
            .SingleOrDefault(element =>
                element.TryGetProperty("AccountName", out var accountProperty)
                && string.Equals(accountProperty.GetString(), account, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(JsonValueKind.Object, target.ValueKind);
        Assert.Equal(ExpectedTargetRace, target.GetProperty("CharacterRace").GetString());
        Assert.Equal(ExpectedTargetGender, target.GetProperty("CharacterGender").GetString());
    }

    private static bool IsNear(WoWActivitySnapshot? snapshot, uint mapId, float x, float y, float radius)
    {
        var position = GetPosition(snapshot);
        return snapshot?.CurrentMapId == mapId
            && position != null
            && LiveBotFixture.Distance2D(position.X, position.Y, x, y) <= radius;
    }

    private static bool IsNearZeppelinDeckApproach(WoWActivitySnapshot? snapshot)
    {
        var position = GetPosition(snapshot);
        return snapshot?.CurrentMapId == OrgrimmarMapId
            && position != null
            && position.Z >= OrgrimmarZeppelinRouteTargetZ - 10f
            && LiveBotFixture.Distance2D(position.X, position.Y, OrgrimmarZeppelinX, OrgrimmarZeppelinY) <= 90f;
    }

    private static bool IsOnTransport(WoWActivitySnapshot? snapshot)
    {
        var movement = snapshot?.MovementData;
        if (movement == null)
            return false;

        return movement.TransportGuid != 0
            || (((MovementFlags)movement.MovementFlags) & MovementFlags.MOVEFLAG_ONTRANSPORT) != 0;
    }

    private static bool IsOnOrgrimmarUndercityZeppelinDeck(WoWActivitySnapshot? snapshot)
    {
        var movement = snapshot?.MovementData;
        if (movement == null || !IsOnTransport(snapshot))
            return false;

        var dx = movement.TransportOffsetX - OrgrimmarUndercityZeppelinDeckOffsetX;
        var dy = movement.TransportOffsetY - OrgrimmarUndercityZeppelinDeckOffsetY;
        var xy = MathF.Sqrt((dx * dx) + (dy * dy));
        var dz = MathF.Abs(movement.TransportOffsetZ - OrgrimmarUndercityZeppelinDeckOffsetZ);
        return xy <= OrgrimmarUndercityZeppelinDeckOffsetXYTolerance
            && dz <= OrgrimmarUndercityZeppelinDeckOffsetZTolerance;
    }

    private static bool HasMissedBoardingDiagnostic(
        WoWActivitySnapshot? snapshot,
        IReadOnlyList<string> baselineMessages)
        => GetDeltaMessages(baselineMessages, snapshot?.RecentChatMessages).Any(message =>
            message.Contains("[TRAVEL_TRANSPORT_MISSED_BOARDING]", StringComparison.Ordinal));

    private static bool HasTransportArrivedDiagnostic(WoWActivitySnapshot? snapshot)
        => snapshot?.RecentChatMessages.Any(message =>
            message.Contains("[TRAVEL_LEG] complete", StringComparison.Ordinal)
            && message.Contains("transport_arrived", StringComparison.Ordinal)) == true;

    private static TimeSpan GetZeppelinTransferEvidenceTimeout()
        => IsManualZeppelinCoordCapture()
            ? System.Threading.Timeout.InfiniteTimeSpan
            : ZeppelinTransferEvidenceTimeout;

    private static bool IsManualZeppelinCoordCapture()
        => string.Equals(
            Environment.GetEnvironmentVariable(ManualZeppelinCoordCaptureEnvVar),
            "1",
            StringComparison.Ordinal);

    private void FailIfZeppelinBoardingLost(
        WoWActivitySnapshot snapshot,
        string account,
        IReadOnlyList<string> diagnosticBaseline)
    {
        if (IsManualZeppelinCoordCapture())
            return;

        if (HasMissedBoardingDiagnostic(snapshot, diagnosticBaseline))
        {
            FailWithScreenshot(
                "The Orgrimmar -> Undercity zeppelin was detected at the dock, but the bot missed boarding before the transport left.",
                account,
                snapshot);
        }

        var position = GetPosition(snapshot);
        if (snapshot.CurrentMapId == OrgrimmarMapId
            && !IsOnTransport(snapshot)
            && position != null
            && position.Z < OrgrimmarZeppelinBoardingZ - OrgrimmarZeppelinBoardingFallZTolerance)
        {
            FailWithScreenshot(
                "The bot fell below the Orgrimmar zeppelin deck during the boarding attempt.",
                account,
                snapshot);
        }
    }

    private static bool IsPathfindingWalkDiagnosticFor(string message, float x, float y, float z)
        => message.Contains("[TRAVEL_WALK_NAV]", StringComparison.Ordinal)
            && message.Contains("nav=True", StringComparison.Ordinal)
            && message.Contains("agent=Tauren/Male", StringComparison.Ordinal)
            && message.Contains("capsule=(0.975,2.625)", StringComparison.Ordinal)
            && message.Contains($"target=({x:F1},{y:F1},{z:F1})", StringComparison.Ordinal)
            && !message.Contains("planned=[]", StringComparison.Ordinal);

    private static bool IsCrossroadsToOrgrimmarFlightCompleteDiagnostic(string message)
        => (message.Contains("[TRAVEL_LEG] complete index=0", StringComparison.Ordinal)
                && message.Contains("flight_arrived", StringComparison.Ordinal))
            || (message.Contains("[TRAVEL_LEG] start index=1", StringComparison.Ordinal)
                && message.Contains("type=Walk", StringComparison.Ordinal))
            || IsPathfindingWalkDiagnosticFor(
                message,
                OrgrimmarZeppelinRouteTargetX,
                OrgrimmarZeppelinRouteTargetY,
                OrgrimmarZeppelinRouteTargetZ);

    private static IReadOnlyList<string> GetDeltaMessages(
        IReadOnlyList<string> baseline,
        IEnumerable<string>? currentMessages)
    {
        var current = currentMessages?.ToArray() ?? Array.Empty<string>();
        if (baseline.Count == 0)
            return current;

        var maxOverlap = Math.Min(baseline.Count, current.Length);
        for (var overlap = maxOverlap; overlap > 0; overlap--)
        {
            var baselineStart = baseline.Count - overlap;
            var matches = true;
            for (var i = 0; i < overlap; i++)
            {
                if (!string.Equals(baseline[baselineStart + i], current[i], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return current.Skip(overlap).ToArray();
        }

        return current;
    }

    private static Game.Position? GetPosition(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.GameObject?.Base?.Position
            ?? snapshot?.MovementData?.Position;

    private static string DescribeSnapshot(WoWActivitySnapshot? snapshot, string label)
    {
        var position = GetPosition(snapshot);
        if (snapshot == null)
            return $"{label}: snapshot=null";

        var distance = position == null
            ? float.NaN
            : LiveBotFixture.Distance2D(position.X, position.Y, UndercityTargetX, UndercityTargetY);

        return $"{label}: map={snapshot.CurrentMapId} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) " +
            $"distToUndercity={distance:F1} transport=0x{snapshot.MovementData?.TransportGuid ?? 0:X} " +
            $"offset=({snapshot.MovementData?.TransportOffsetX:F1},{snapshot.MovementData?.TransportOffsetY:F1},{snapshot.MovementData?.TransportOffsetZ:F1}) " +
            $"current={snapshot.CurrentAction?.ActionType.ToString() ?? "null"}";
    }

    private static string SanitizeScreenshotLabel(string label)
    {
        var sanitized = Regex.Replace(label, "[^A-Za-z0-9._-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "long-pathing-failure";

        return sanitized.Length <= 80 ? sanitized : sanitized[..80];
    }

    private static string EscapePowerShellSingleQuotedString(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
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

    private static bool IsLongPathingTimelineEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable(LongPathingTimelineEnvVar),
            "1",
            StringComparison.Ordinal);

    private static string ResolveTimelineDirectory(string testName)
    {
        var repoRoot = ResolveRepoRoot();
        var dir = Path.Combine(
            repoRoot,
            "tmp",
            "test-runtime",
            "screenshots",
            "long-pathing",
            "timeline",
            testName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void CaptureTimelineCheckpoint(
        string testName,
        string phase,
        string account,
        WoWActivitySnapshot? snapshot)
    {
        if (!IsLongPathingTimelineEnabled())
            return;

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var safePhase = SanitizeScreenshotLabel(phase);
            var dir = ResolveTimelineDirectory(testName);
            var pngPath = Path.Combine(dir, $"{safePhase}-{account}-{timestamp}.png");
            var jsonPath = Path.Combine(dir, $"{safePhase}-{account}-{timestamp}.json");

            var pid = ResolveManagedWowProcessId(account);
            var screenshotOk = false;
            string? screenshotError = null;
            if (pid.HasValue)
            {
                try
                {
                    var hwnd = WindowCapture.FindWoWClientWindow(pid.Value);
                    screenshotOk = WindowCapture.CaptureWindow(hwnd, pngPath);
                    if (!screenshotOk)
                        screenshotError = "PrintWindow + desktop fallback both failed";
                }
                catch (Exception ex)
                {
                    screenshotError = ex.Message;
                }
            }
            else
            {
                screenshotError = "no managed WoW pid resolvable for account";
            }

            var position = GetPosition(snapshot);
            var movement = snapshot?.MovementData;
            var recentChat = (snapshot?.RecentChatMessages ?? Enumerable.Empty<string>())
                .TakeLast(5)
                .ToArray();

            var record = new
            {
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                testName,
                phase,
                account,
                pid,
                screenshot = new
                {
                    saved = screenshotOk,
                    path = screenshotOk ? pngPath : null,
                    error = screenshotError,
                },
                snapshot = snapshot == null ? null : new
                {
                    currentMapId = snapshot.CurrentMapId,
                    position = position == null ? null : new { x = position.X, y = position.Y, z = position.Z },
                    facing = movement?.Facing,
                    currentSpeed = movement?.CurrentSpeed,
                    runSpeed = movement?.RunSpeed,
                    movementFlags = movement == null ? (uint?)null : movement.MovementFlags,
                    isOnTransport = IsOnTransport(snapshot),
                    transportGuid = movement?.TransportGuid ?? 0UL,
                    transportOffset = movement == null ? null : new
                    {
                        x = movement.TransportOffsetX,
                        y = movement.TransportOffsetY,
                        z = movement.TransportOffsetZ,
                    },
                    fallTime = movement?.FallTime,
                    splineFlags = movement?.SplineFlags,
                    currentAction = snapshot.CurrentAction?.ActionType.ToString(),
                    recentChat,
                },
            };

            File.WriteAllText(
                jsonPath,
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            try
            {
                _output.WriteLine($"[TIMELINE-ERR] phase={phase} account={account}: {ex.Message}");
            }
            catch
            {
                // _output may be unavailable late in the test lifecycle; swallow.
            }
        }
    }

    private sealed class SnapshotStallGuard(string label, TimeSpan timeout, float movementThresholdYards)
    {
        private bool _hasAnchor;
        private uint _anchorMapId;
        private float _anchorX;
        private float _anchorY;
        private float _anchorZ;
        private DateTime _anchorUtc;

        public void Reset()
        {
            _hasAnchor = false;
            _anchorUtc = DateTime.MinValue;
        }

        public void FailIfStalled(WoWActivitySnapshot snapshot, Action<string, WoWActivitySnapshot?> fail)
        {
            var position = GetPosition(snapshot);
            if (position == null)
            {
                Reset();
                return;
            }

            var now = DateTime.UtcNow;
            var moved = _hasAnchor
                ? LiveBotFixture.Distance2D(position.X, position.Y, _anchorX, _anchorY)
                : float.MaxValue;

            if (!_hasAnchor || snapshot.CurrentMapId != _anchorMapId || moved > movementThresholdYards)
            {
                _hasAnchor = true;
                _anchorMapId = snapshot.CurrentMapId;
                _anchorX = position.X;
                _anchorY = position.Y;
                _anchorZ = position.Z;
                _anchorUtc = now;
                return;
            }

            if (now - _anchorUtc < timeout)
                return;

            fail(
                $"Long-travel stall before {label}; likely wall/ceiling collision needs investigation. " +
                $"map={snapshot.CurrentMapId} anchor=({_anchorX:F1},{_anchorY:F1},{_anchorZ:F1}) " +
                $"current=({position.X:F1},{position.Y:F1},{position.Z:F1}) moved={moved:F1} " +
                $"flags=0x{snapshot.MovementData?.MovementFlags ?? 0:X} " +
                $"transport=0x{snapshot.MovementData?.TransportGuid ?? 0:X}.",
                snapshot);
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly (string Name, string? Value)[] _originalValues;

        public EnvironmentVariableScope(params string[] names)
        {
            _originalValues = names
                .Select(name => (name, Environment.GetEnvironmentVariable(name)))
                .ToArray();
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
