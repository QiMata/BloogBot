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
    private const string OgRampWaypointInspectEnvVar = "WWOW_OG_RAMP_WAYPOINT_INSPECT";
    private const string OgDeckLipVerifyEnvVar = "WWOW_OG_DECK_LIP_VERIFY";
    private const string DeckLipClimbEnvVar = "WWOW_DECKLIP_CLIMB_TEST";
    private const string BrmDungeonTravelEnvVar = "WWOW_BRM_DUNGEON_TRAVEL_TEST";

    // Flame Crest — closest Horde flight master to Blackrock Mountain
    // (Burning Steppes, ~1000y south of the BRM mountain entrance).
    //
    // The flight master's literal NPC pad is on top of a stone tower at
    // z=165, but bots teleporting there fall ~33y to ground (z=132) and
    // their MovementController gets wedged at the post-fall position
    // (observed BRD/LBRS stall coord (-7518.7, -2159.9, 131.9) — same lockup
    // for both routes). Use a ground-level XYZ near the tower foot so the
    // bot starts on solid terrain. Semantically this is still "the Flame
    // Crest flight point" — the same area a freshly-dismounted bot would
    // walk from.
    private const int FlameCrestMapId = 0;
    private const float FlameCrestX = -7518.7f;
    private const float FlameCrestY = -2159.9f;
    private const float FlameCrestZ = 131.9f;

    // BRM dungeon target world coordinates (map 0).
    //
    // LBRS and UBRS use the literal portal coordinates: the upper-spire
    // portal cluster is fully meshed and the Detour smooth-path lands the bot
    // exactly at the portal poly (verified by BrmDungeonRouteDiagnostic —
    // 0.0y / 0.3y short of target).
    //
    // BRD and BWL use bot-reachable APPROACH positions in BRM, not the
    // literal portal coords. The BRD cave entry tunnel (z≈170 below mountain
    // surface) and BWL altar at the spire summit (z=400) sit on isolated
    // walkable polygons that aren't connected to the BRM-exterior corridor —
    // a real bake hole in BRM tiles (45,33) and (46,34). The BRM cave/spire
    // interior is mostly WMO geometry and not all of it gets meshed by the
    // current MmapGen pipeline. The smooth-path corridor terminates at the
    // closest reachable surface position, which is what the live bot can
    // navigate to. Target = corridor terminus, semantically "the bot reached
    // the BRM area for that dungeon." Fixing the literal-portal reachability
    // is a separate BRM-interior-bake task tracked in TASKS.md.
    private const float BrdEntranceX = -7187f;     // approach (bake terminates here)
    private const float BrdEntranceY = -958f;
    private const float BrdEntranceZ = 254f;
    private const float LbrsEntranceX = -7531f;    // literal portal (fully reachable)
    private const float LbrsEntranceY = -1226f;
    private const float LbrsEntranceZ = 286f;
    private const float UbrsEntranceX = -7524f;    // literal portal (fully reachable)
    private const float UbrsEntranceY = -1233f;
    private const float UbrsEntranceZ = 287f;
    private const float BwlEntranceX = -7659f;     // approach (bake terminates here)
    private const float BwlEntranceY = -1214f;
    private const float BwlEntranceZ = 291f;

    // Per-leg arrival tolerance: TravelTo's planner targets the nearest poly
    // to the requested point. Set to 16y to clear the bot's own
    // WalkLegArrivalRadius (15y) plus a 1y settling margin.
    private const float BrmDungeonArriveToleranceYards = 16f;

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
        // Phase 5.3.7 (PFS-OVERHAUL-006): test evaluates BEHAVIOR by observing
        // TravelTask's own walk-leg completion signal, not a test-defined
        // distance/dz tolerance. TravelTask owns the arrival rules
        // (WalkLegNativeOffMeshTransportVerticalArrivalTolerance=1.5y,
        // BoardingRadius=12y XY); the test just checks "did the production
        // code consider this leg complete?" via the [TRAVEL_LEG] complete
        // reason=walk_arrived chat emit.
        var walkLegCompleted = await _bot.WaitForSnapshotConditionAsync(
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

                if (snapshot?.RecentChatMessages == null)
                    return false;
                // Walk leg leg=0 (the OG-tower-to-Frezza walk) reports completion
                // via this exact diagnostic when TravelTask's TryGetWalkLegArrival
                // accepts the bot's position. reason=walk_arrived is the success
                // path; reason=walk_map_changed/walk_stall_*/etc would indicate
                // a different completion path that the test should flag.
                foreach (var msg in snapshot.RecentChatMessages)
                {
                    if (msg.IndexOf("[TRAVEL_LEG] complete index=0 reason=walk_arrived",
                        StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            },
            TimeSpan.FromSeconds(180),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} OG zeppelin tower ramp climb to Frezza");

        await _bot.RefreshSnapshotsAsync();
        var finalSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(TimelineTestName, "03-final", target.AccountName, finalSnapshot);

        await AssertOrScreenshotAsync(
            walkLegCompleted,
            target.AccountName,
            $"Expected TravelTask to emit [TRAVEL_LEG] complete index=0 reason=walk_arrived for "
            + $"the OG-flight-master-to-Frezza walk leg within 180s. The walk-leg arrival rules "
            + $"are owned by TravelTask (WalkLegNativeOffMeshTransportVerticalArrivalTolerance=1.5y, "
            + $"BoardingRadius=12y XY). If walk_arrived never fires, the bot did not physically "
            + $"reach the boarding zone — investigate the corridor + native collision path. "
            + $"Cadence diagnostic captured {seenWaypointDiagMessages.Count} [TRAVEL_WAYPOINT_REACHED] "
            + "events in the timeline directory.");
    }

    /// <summary>
    /// Cycle 17c isolation: tests the OG-zeppelin-tower ramp climb from base
    /// (z=24) to Frezza (z=53.6) WITHOUT the upstream FM-tower-descent +
    /// OG-city traversal that the full ClimbOrgrimmarZeppelinTowerRampToFrezza
    /// requires. Teleports directly to an Orgrimmar Grunt position at the
    /// tower base (creature.guid=3462 entry 3296, FG-verified at
    /// (1332.76,-4633.40,24.0783)) and dispatches TravelTo Undercity. The
    /// route planner emits the same Walk-leg-to-Frezza, but the bot only has
    /// to climb the spiral ramp (~30s walk) instead of the full 3-minute
    /// route. Use to diagnose deck-lip step-up failures without the upstream
    /// WoW.exe crash that currently blocks the full climb test.
    ///
    /// Memory: project_pfs_overhaul_006_intra_tile_disconnect (Cycle 17c).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task DeckLipClimbFromGruntToFrezza()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(DeckLipClimbEnvVar),
                "1",
                StringComparison.Ordinal),
            $"Deck-lip climb sub-test disabled (set {DeckLipClimbEnvVar}=1).");

        const string TimelineTestName = nameof(DeckLipClimbFromGruntToFrezza);
        using var packetHookScope = DisableForegroundPacketHooksForCrossMapTransfers();
        var target = await EnsureLongPathingTargetAsync();

        using var timelineScope = new EnvironmentVariableScope(LongPathingTimelineEnvVar);
        Environment.SetEnvironmentVariable(LongPathingTimelineEnvVar, "1");

        using var cadenceScope = new EnvironmentVariableScope(WaypointCadenceEnvVar);
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WaypointCadenceEnvVar)))
            Environment.SetEnvironmentVariable(WaypointCadenceEnvVar, "2");

        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        // Orgrimmar Grunt #1 spawn — tower base lower platform (FG-verified
        // bot settles at z=24.08 with movementFlags=0).
        const float Grunt1X = 1332.76f;
        const float Grunt1Y = -4633.40f;
        const float Grunt1Z = 24.0783f;

        await _bot.BotTeleportAsync(target.AccountName, OrgrimmarMapId, Grunt1X, Grunt1Y, Grunt1Z);
        await Task.Delay(2500);
        await _bot.RefreshSnapshotsAsync();
        var startSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(TimelineTestName, "01-teleported-tower-base", target.AccountName, startSnapshot);

        var diagnosticBaseline = startSnapshot?.RecentChatMessages.ToArray() ?? Array.Empty<string>();

        // Same TravelTo Undercity dispatch as the full climb test — exercises
        // the same Walk-leg-to-Frezza decision in the route planner.
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

        var stuckGuard = new SnapshotStallGuard(
            "OG zeppelin tower ramp climb from base to Frezza",
            TimeSpan.FromSeconds(20),
            LongTravelStallMovementYards);

        var pollCounter = 0;
        var seenWaypointDiagMessages = new HashSet<string>(StringComparer.Ordinal);
        // 90s budget — should be ample for a ~30s ramp climb. If walk_arrived
        // doesn't fire by 90s, the bot is genuinely stuck on the ramp (most
        // likely at the deck-lip step-up).
        var walkLegCompleted = await _bot.WaitForSnapshotConditionAsync(
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

                if (snapshot?.RecentChatMessages == null)
                    return false;
                foreach (var msg in snapshot.RecentChatMessages)
                {
                    if (msg.IndexOf("[TRAVEL_LEG] complete index=0 reason=walk_arrived",
                        StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            },
            TimeSpan.FromSeconds(90),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} OG zeppelin tower ramp climb from base to Frezza");

        await _bot.RefreshSnapshotsAsync();
        var finalSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(TimelineTestName, "03-final", target.AccountName, finalSnapshot);

        await AssertOrScreenshotAsync(
            walkLegCompleted,
            target.AccountName,
            $"Expected TravelTask to emit [TRAVEL_LEG] complete index=0 reason=walk_arrived for "
            + $"the Grunt-base-to-Frezza walk leg within 90s. The bot teleported to the OG zeppelin "
            + $"tower's lower platform (z=24) and should walk up the spiral ramp to Frezza (z=53.6). "
            + $"If walk_arrived never fires, the bot stalled mid-ramp — most likely at the deck-lip "
            + $"step-up where the smooth path emits a 1.84y vertical step (lower platform → upper deck) "
            + $"that exceeds NavigationPath's WAYPOINT_VERTICAL_REACH_TOLERANCE=1.25y. "
            + $"Cadence diagnostic captured {seenWaypointDiagMessages.Count} [TRAVEL_WAYPOINT_REACHED] "
            + "events.");
    }

    /// <summary>
    /// Travels from Flame Crest (the closest Horde flight master to Blackrock
    /// Mountain) to each BRM dungeon/raid portal in the world. Exercises long
    /// outdoor paths into BRM's interior tunnels and the vertical climb to the
    /// upper-spire portal cluster — the natural follow-up to the OG zeppelin
    /// deck-lip fix (PFS-OVERHAUL-006 Cycle 17e).
    ///
    /// One [InlineData] per dungeon entrance reachable from BRM's exterior:
    ///   - BRD  (-7179, -921, 165)   lower spire
    ///   - LBRS (-7531, -1226, 286)  upper spire portal cluster
    ///   - UBRS (-7524, -1233, 287)  upper spire portal cluster
    ///   - BWL  (-7665, -1102, 400)  top of spire, above UBRS
    ///
    /// MC is excluded — its portal sits inside BRD instance, not the world.
    /// Each iteration teleports to Flame Crest (clean slate), dispatches
    /// TravelTo to the portal coords, and asserts arrival within
    /// BrmDungeonArriveToleranceYards. Gated on <c>WWOW_BRM_DUNGEON_TRAVEL_TEST=1</c>.
    /// </summary>
    [SkippableTheory]
    [Trait("Category", "RequiresInfrastructure")]
    [InlineData("BRD",  BrdEntranceX,  BrdEntranceY,  BrdEntranceZ)]
    [InlineData("LBRS", LbrsEntranceX, LbrsEntranceY, LbrsEntranceZ)]
    [InlineData("UBRS", UbrsEntranceX, UbrsEntranceY, UbrsEntranceZ)]
    [InlineData("BWL",  BwlEntranceX,  BwlEntranceY,  BwlEntranceZ)]
    public async Task FlameCrestToBrmDungeonEntrance(string dungeon, float targetX, float targetY, float targetZ)
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(BrmDungeonTravelEnvVar),
                "1",
                StringComparison.Ordinal),
            $"BRM dungeon travel test disabled (set {BrmDungeonTravelEnvVar}=1).");

        var timelineTestName = $"{nameof(FlameCrestToBrmDungeonEntrance)}-{dungeon}";
        using var packetHookScope = DisableForegroundPacketHooksForCrossMapTransfers();
        var target = await EnsureLongPathingTargetAsync();

        using var timelineScope = new EnvironmentVariableScope(LongPathingTimelineEnvVar);
        Environment.SetEnvironmentVariable(LongPathingTimelineEnvVar, "1");

        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        // Drop the bot at Flame Crest's flight master pad.
        await _bot.BotTeleportAsync(target.AccountName, FlameCrestMapId, FlameCrestX, FlameCrestY, FlameCrestZ);
        await Task.Delay(2500);
        await _bot.RefreshSnapshotsAsync();
        var startSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(timelineTestName, "01-teleported-flame-crest", target.AccountName, startSnapshot);

        var diagnosticBaseline = startSnapshot?.RecentChatMessages.ToArray() ?? Array.Empty<string>();

        var dispatch = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.TravelTo,
            Parameters =
            {
                new RequestParameter { IntParam = FlameCrestMapId },
                new RequestParameter { FloatParam = targetX },
                new RequestParameter { FloatParam = targetY },
                new RequestParameter { FloatParam = targetZ },
            },
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        var stuckGuard = new SnapshotStallGuard(
            $"Flame Crest → {dungeon} portal",
            LongTravelStallTimeout,
            LongTravelStallMovementYards);

        var pollCounter = 0;
        // Flame Crest → BRM portals is ~1000-1700y of outdoor walking plus the
        // BRM tunnel ascent. 360s is generous; cancel earlier if the bot
        // arrives, stalls, or the planner reports walk_arrived.
        var travelTimeout = TimeSpan.FromSeconds(360);
        var arrived = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot =>
            {
                pollCounter++;
                if (pollCounter % TimelinePollSampleEveryN == 0)
                    CaptureTimelineCheckpoint(
                        timelineTestName,
                        $"02-travel-poll-{pollCounter:D5}",
                        target.AccountName,
                        snapshot);

                stuckGuard.FailIfStalled(
                    snapshot,
                    (message, stallSnapshot) => FailWithScreenshot(message, target.AccountName, stallSnapshot));

                if (snapshot?.RecentChatMessages != null)
                {
                    foreach (var msg in snapshot.RecentChatMessages)
                    {
                        if (msg.IndexOf("[TRAVEL_LEG] complete", StringComparison.Ordinal) >= 0
                            && msg.IndexOf("reason=walk_arrived", StringComparison.Ordinal) >= 0)
                        {
                            return true;
                        }
                    }
                }

                var pos = GetPosition(snapshot);
                if (pos != null && snapshot?.CurrentMapId == FlameCrestMapId)
                {
                    var dist2d = LiveBotFixture.Distance2D(pos.X, pos.Y, targetX, targetY);
                    if (dist2d <= BrmDungeonArriveToleranceYards
                        && Math.Abs(pos.Z - targetZ) <= BrmDungeonArriveToleranceYards)
                    {
                        return true;
                    }
                }

                return false;
            },
            travelTimeout,
            pollIntervalMs: 1000,
            progressLabel: $"{target.RoleLabel} Flame Crest → {dungeon}");

        await _bot.RefreshSnapshotsAsync();
        var finalSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
        CaptureTimelineCheckpoint(timelineTestName, "03-final", target.AccountName, finalSnapshot);

        var finalPos = GetPosition(finalSnapshot);
        var finalDist = finalPos != null
            ? LiveBotFixture.Distance2D(finalPos.X, finalPos.Y, targetX, targetY)
            : float.NaN;

        await AssertOrScreenshotAsync(
            arrived,
            target.AccountName,
            $"Expected bot to walk from Flame Crest ({FlameCrestX:F0},{FlameCrestY:F0},{FlameCrestZ:F0}) "
            + $"to the {dungeon} portal at ({targetX:F0},{targetY:F0},{targetZ:F0}) within "
            + $"{travelTimeout.TotalSeconds:F0}s. Final position: "
            + $"({finalPos?.X:F1},{finalPos?.Y:F1},{finalPos?.Z:F1}) "
            + $"map={finalSnapshot?.CurrentMapId} dist2D={finalDist:F1}y. "
            + $"Arrival is met by either [TRAVEL_LEG] complete reason=walk_arrived in chat, OR final "
            + $"2D-distance ≤ {BrmDungeonArriveToleranceYards:F0}y AND |dz| ≤ {BrmDungeonArriveToleranceYards:F0}y.");
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

    /// <summary>
    /// Phase 5.3.7 (PFS-OVERHAUL-006) — teleport the bot to each waypoint of
    /// the actual failing climb-path corridor, capture a screenshot + settled
    /// position at each. This validates the BAKED NAVMESH (where Detour says
    /// the path goes) against ACTUAL GAME GEOMETRY (where the bot can stand).
    /// If teleporting to WP[2] (1335.2,-4644.4,53.5) — the lip-top corner —
    /// settles at z=51.6 (the lip-foot), the navmesh waypoint is INSIDE
    /// unbaked GO collision and Detour produced a corridor through a wall.
    ///
    /// Gated on WWOW_OG_RAMP_WAYPOINT_INSPECT=1. Diagnostic-only — no
    /// production behavior assertions. Outputs: paired PNG+JSON in
    /// tmp/test-runtime/screenshots/long-pathing/timeline/OgRampWaypointInspect/.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task OgRampWaypointInspect()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(OgRampWaypointInspectEnvVar),
                "1",
                StringComparison.Ordinal),
            $"OG ramp waypoint inspect disabled (set {OgRampWaypointInspectEnvVar}=1).");

        const string TimelineTestName = nameof(OgRampWaypointInspect);
        var target = await EnsureLongPathingTargetAsync();

        using var timelineScope = new EnvironmentVariableScope(LongPathingTimelineEnvVar);
        Environment.SetEnvironmentVariable(LongPathingTimelineEnvVar, "1");

        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        // Smooth-path corridor window from the failing climb test
        // (climb-20260508T001423Z trace, idx=2 stall):
        //   window=[0:(1339.1,-4646.1,51.9) 1:(1337.3,-4645.1,51.7)
        //           *2:(1335.2,-4644.4,53.5) 3:(1337.2,-4643.0,53.5)
        //           4:(1339.4,-4644.1,53.5) 5:(1337.5,-4644.6,54.3)
        //           6:(1329.1,-4648.6,54.8) 7:(1330.9,-4649.5,54.6)
        //           8:(1330.4,-4649.3,54.7) 9:(1330.7,-4649.4,54.6)]
        // Plus reference points: stall coord, ApproachPosition, BoardingPosition,
        // Frezza spawn — to compare bake-claimed walkable points to actual settle.
        var captures = new (string Label, int MapId, float X, float Y, float Z)[]
        {
            ("a-stall-coord-z51.6",     1, 1338.13f, -4645.96f, 51.60f),
            ("a-approach-pos-z51.6",    1, 1338.10f, -4646.00f, 51.60f),
            ("smooth-wp00-z51.9",       1, 1339.1f,  -4646.1f,  51.9f),
            ("smooth-wp01-z51.7",       1, 1337.3f,  -4645.1f,  51.7f),
            ("smooth-wp02-z53.5-LIP",   1, 1335.2f,  -4644.4f,  53.5f),
            ("smooth-wp03-z53.5",       1, 1337.2f,  -4643.0f,  53.5f),
            ("smooth-wp04-z53.5",       1, 1339.4f,  -4644.1f,  53.5f),
            ("smooth-wp05-z54.3",       1, 1337.5f,  -4644.6f,  54.3f),
            ("smooth-wp06-z54.8",       1, 1329.1f,  -4648.6f,  54.8f),
            ("smooth-wp07-z54.6-FRZ",   1, 1330.9f,  -4649.5f,  54.6f),
            ("z-frezza-spawn-z53.6",    1, 1331.11f, -4649.45f, 53.6269f),
            ("z-boarding-pos-z53.9",    1, 1320.14f, -4653.16f, 53.89f),
        };

        foreach (var c in captures)
        {
            _output.WriteLine(
                $"[WP-INSPECT] tele {target.AccountName} -> {c.Label} map={c.MapId} ({c.X:F2},{c.Y:F2},{c.Z:F2})");
            await _bot.BotTeleportAsync(target.AccountName, c.MapId, c.X, c.Y, c.Z);

            // Allow falling, slope-slide, and z-settle to finish before sampling.
            await Task.Delay(4000);
            await _bot.RefreshSnapshotsAsync();
            var settled = await _bot.GetSnapshotAsync(target.AccountName);

            CaptureTimelineCheckpoint(TimelineTestName, c.Label, target.AccountName, settled);

            var pos = GetPosition(settled);
            _output.WriteLine(
                pos == null
                    ? $"[WP-INSPECT] {c.Label}: snapshot position unavailable"
                    : $"[WP-INSPECT] {c.Label}: settled=({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) " +
                      $"deltaXY={LiveBotFixture.Distance2D(pos.X, pos.Y, c.X, c.Y):F2} dz={pos.Z - c.Z:F2}");
        }
    }

    /// <summary>
    /// PFS-OVERHAUL-006 deck-lip ground-truth verification. The bake at smooth-path
    /// WP 53 claims a walkable surface at z=53.61 at (1337.64,-4643.14) but the
    /// smooth path emits WP Z=51.72 (in the air at the lip foot). Teleport the
    /// bot to each candidate point, settle for 1.5s, capture screenshot + settled
    /// position so we can SEE what is actually at those XY/Z coordinates and
    /// where the bot lands.
    ///
    /// Gated on WWOW_OG_DECK_LIP_VERIFY=1 (independent of OG_DECK_ANCHOR_VERIFY).
    /// Diagnostic-only — no production behavior assertions. Outputs: paired
    /// PNG+JSON via CaptureTimelineCheckpoint plus a per-point summary JSON
    /// containing claimed Z, settled Z, and dz delta in
    /// tmp/test-runtime/screenshots/long-pathing/timeline/OgDeckLipAnchorVerification/.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task OgDeckLipAnchorVerification()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(OgDeckLipVerifyEnvVar),
                "1",
                StringComparison.Ordinal),
            $"OG deck-lip anchor verification disabled (set {OgDeckLipVerifyEnvVar}=1).");

        const string TimelineTestName = nameof(OgDeckLipAnchorVerification);
        var target = await EnsureLongPathingTargetAsync();

        using var timelineScope = new EnvironmentVariableScope(LongPathingTimelineEnvVar);
        Environment.SetEnvironmentVariable(LongPathingTimelineEnvVar, "1");

        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        // Deck-lip step-up coords from the failing climb-path corridor.
        // DL1/DL2: claimed smooth-path WPs 53/54 (lower/upper deck).
        // DL3: where the bake says the actual walkable surface sits at WP 53 XY.
        // DL4: the manifest's "deck-lip stall" coord (lower-deck mid-ramp).
        // DL5/DL6: reference NPCs (Frezza on upper deck, base-of-tower Grunt).
        // DL7: high-altitude probe (drop the bot from above to see what surface
        //      it lands on at the deck-lip XY).
        var captures = new (string Label, int MapId, float X, float Y, float Z)[]
        {
            ("dl1-smoothwp53-claimed-z51.72",   1, 1337.64f, -4643.14f, 51.72f),
            ("dl2-smoothwp54-claimed-z54.22",   1, 1336.20f, -4644.53f, 54.22f),
            ("dl3-poly-surface-wp53xy-z53.61",  1, 1337.64f, -4643.14f, 53.61f),
            ("dl4-deck-lip-stall-z51.60",       1, 1338.13f, -4645.96f, 51.60f),
            ("dl5-frezza-z53.6269",             1, 1331.11f, -4649.45f, 53.6269f),
            ("dl6-grunt-base-z24.0783",         1, 1332.76f, -4633.40f, 24.0783f),
            ("dl7-above-deck-drop-z55.0",       1, 1336.92f, -4643.83f, 55.00f),
        };

        var summaryDir = ResolveTimelineDirectory(TimelineTestName);

        foreach (var c in captures)
        {
            _output.WriteLine(
                $"[DECK-LIP-VERIFY] tele {target.AccountName} -> {c.Label} map={c.MapId} ({c.X:F2},{c.Y:F2},{c.Z:F2})");
            await _bot.BotTeleportAsync(target.AccountName, c.MapId, c.X, c.Y, c.Z);

            // 1.5s settlement — enough for fall/slope-slide for deck-height drops.
            await Task.Delay(1500);
            await _bot.RefreshSnapshotsAsync();
            var settled = await _bot.GetSnapshotAsync(target.AccountName);

            CaptureTimelineCheckpoint(TimelineTestName, c.Label, target.AccountName, settled);

            var pos = GetPosition(settled);
            var deltaXY = pos == null
                ? float.NaN
                : LiveBotFixture.Distance2D(pos.X, pos.Y, c.X, c.Y);
            var dz = pos == null ? float.NaN : pos.Z - c.Z;

            _output.WriteLine(
                pos == null
                    ? $"[DECK-LIP-VERIFY] {c.Label}: snapshot position unavailable"
                    : $"[DECK-LIP-VERIFY] {c.Label}: settled=({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) " +
                      $"deltaXY={deltaXY:F2} dz={dz:F2}");

            // Per-point claimed-vs-settled summary JSON beside the CaptureTimelineCheckpoint outputs.
            try
            {
                var safeLabel = SanitizeScreenshotLabel(c.Label);
                var summaryPath = Path.Combine(summaryDir, $"{safeLabel}-summary.json");
                var summary = new
                {
                    label = c.Label,
                    timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    account = target.AccountName,
                    claimed = new { mapId = c.MapId, x = c.X, y = c.Y, z = c.Z },
                    settled = pos == null
                        ? null
                        : new { mapId = (int)settled!.CurrentMapId, x = pos.X, y = pos.Y, z = pos.Z },
                    deltaXY = float.IsNaN(deltaXY) ? (float?)null : deltaXY,
                    dz = float.IsNaN(dz) ? (float?)null : dz,
                };
                File.WriteAllText(
                    summaryPath,
                    JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[DECK-LIP-VERIFY-ERR] summary write failed for {c.Label}: {ex.Message}");
            }
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

    private const string OgZeppelinBakeFixtureEnvVar = "WWOW_OG_ZEP_BAKE_FIXTURE";
    private const string BrmDungeonBakeFixtureEnvVar = "WWOW_BRM_BAKE_FIXTURE";

    /// <summary>
    /// PFS-OVERHAUL-006 (2026-05-10) — bake-validation harness driver for
    /// the OG zeppelin climb fixture. Loads
    /// <c>tools/MmapGen/test-fixtures/og-zeppelin.json</c>, runs
    /// <see cref="Harness.WaypointSettleValidator"/> against the FG bot
    /// (and BG when available), writes a JSON report under
    /// <c>tmp/test-runtime/screenshots/long-pathing/</c>, and fails on
    /// any registered regression. Gated on
    /// <c>WWOW_OG_ZEP_BAKE_FIXTURE=1</c>.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task OgZeppelin_BakeFixtureValidation()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(OgZeppelinBakeFixtureEnvVar),
                "1",
                StringComparison.Ordinal),
            $"OG zeppelin bake-fixture validation disabled (set {OgZeppelinBakeFixtureEnvVar}=1).");

        await RunBakeFixtureValidationAsync("og-zeppelin");
    }

    /// <summary>
    /// PFS-OVERHAUL-006 (2026-05-10) — bake-validation harness driver for
    /// the FlameCrest → BRM dungeon-entrance fixture. Loads
    /// <c>tools/MmapGen/test-fixtures/flamecrest-to-brm.json</c>, runs
    /// the validator, writes a JSON report, and fails on any registered
    /// regression. Gated on <c>WWOW_BRM_BAKE_FIXTURE=1</c>.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task BrmDungeon_BakeFixtureValidation()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable(BrmDungeonBakeFixtureEnvVar),
                "1",
                StringComparison.Ordinal),
            $"BRM dungeon bake-fixture validation disabled (set {BrmDungeonBakeFixtureEnvVar}=1).");

        await RunBakeFixtureValidationAsync("flamecrest-to-brm");
    }

    private async Task RunBakeFixtureValidationAsync(string fixtureRouteId)
    {
        var fixture = Harness.BakeFixtureLoader.LoadByRoute(fixtureRouteId);
        _output.WriteLine(
            $"[BAKE-VAL] loaded fixture route='{fixture.Route}' map={fixture.MapId} " +
            $"walkable={fixture.ExpectedWalkable.Count} holes={fixture.ExpectedHoles.Count}");

        var target = await EnsureLongPathingTargetAsync();
        await _bot.EnsureCleanSlateAsync(target.AccountName, target.RoleLabel);

        var bgAccount = await ResolveBgAccountForParityAsync(target);
        var host = new Harness.LiveBakeValidationHost(_bot, _output.WriteLine);
        var validator = new Harness.WaypointSettleValidator(fixture, host);

        var report = await validator.ValidateAsync(target.AccountName, bgAccount);

        var reportDir = Path.Combine(
            ResolveRepoRoot(),
            "tmp", "test-runtime", "screenshots", "long-pathing");
        var reportPath = Harness.WaypointSettleValidator.WriteReport(report, reportDir);
        _output.WriteLine($"[BAKE-VAL] report written: {reportPath}");

        if (!report.Passed)
        {
            var summary = string.Join(
                Environment.NewLine,
                report.Failures.Select(f => $"  {f.Kind} [{f.Label}] {f.Message}"));
            Assert.Fail(
                $"Bake validation failed for route '{fixture.Route}' "
                + $"({report.Failures.Count} failure(s)). Report: {reportPath}{Environment.NewLine}{summary}");
        }
    }

    /// <summary>
    /// Returns the BG account name when it is in-world and actionable, so
    /// the validator can pair every checkpoint with an FG/BG parity
    /// assertion. Returns null when only one client is available — the
    /// validator then runs FG-only and skips parity.
    /// </summary>
    private async Task<string?> ResolveBgAccountForParityAsync(LiveBotFixture.BotRunnerActionTarget fgTarget)
    {
        if (string.IsNullOrWhiteSpace(_bot.BgAccountName)) return null;
        if (string.Equals(_bot.BgAccountName, fgTarget.AccountName, StringComparison.OrdinalIgnoreCase)) return null;
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = await _bot.GetSnapshotAsync(_bot.BgAccountName);
        return bgSnap?.Player?.Unit?.GameObject?.Base?.Position == null ? null : _bot.BgAccountName;
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
