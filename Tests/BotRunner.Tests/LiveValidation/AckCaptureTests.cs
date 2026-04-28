using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed ACK corpus capture probes. SHODAN owns launch/positioning
/// setup; the foreground BotRunner target performs the capture-triggering hop
/// or configured command because the injected client is the corpus source.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public sealed class AckCaptureTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 34.2f;

    private const int EasternKingdomsMapId = 0;
    private const float IronforgeX = -4838f;
    private const float IronforgeY = -1317f;
    private const float IronforgeZ = 505f;

    private const int KnockbackCombatLevel = 20;
    private const int StormscaleWaveRiderEntry = 2179;
    private const int LordSinslayerEntry = 7017;
    private const int TaragamanTheHungererEntry = 11520;
    private const int KnockbackStageMapId = 389;
    private const float KnockbackStageX = -252.0f;
    private const float KnockbackStageY = 150.0f;
    private const float KnockbackStageZ = -18.8f;

    public AckCaptureTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled()
    {
        var target = await EnsureAckCaptureForegroundTargetAsync();
        _output.WriteLine(
            $"=== FG ACK Capture: worldport hop with {target.AccountName}/{target.CharacterName} ===");

        var startSettled = await StageForegroundCapturePointAsync(
            target,
            KalimdorMapId,
            OrgX,
            OrgY,
            OrgZ,
            "ack-capture Orgrimmar start",
            cleanSlate: true,
            xyToleranceYards: 10f);
        Assert.True(startSettled, "FG bot should settle in Orgrimmar before the cross-map capture hop.");

        var worldportWindowDir = ResolvePostTeleportWindowDirectory();
        var returnedToOrg = false;

        try
        {
            _output.WriteLine("[FG-ACK] Moving FG from Kalimdor to Ironforge to force SMSG_NEW_WORLD -> MSG_MOVE_WORLDPORT_ACK.");

            var settled = await StageForegroundCapturePointAsync(
                target,
                EasternKingdomsMapId,
                IronforgeX,
                IronforgeY,
                IronforgeZ,
                "ack-capture Ironforge hop",
                cleanSlate: false,
                xyToleranceYards: 25f);
            Assert.True(settled, "FG bot should settle in Ironforge after the cross-map capture hop.");

            await _bot.RefreshSnapshotsAsync();
            var fgSnap = _bot.ForegroundBot;
            Assert.NotNull(fgSnap);
            Assert.Equal((uint)EasternKingdomsMapId, fgSnap!.Player?.Unit?.GameObject?.Base?.MapId ?? 0U);

            var corpusRoot = ResolveCorpusOutputDirectory();
            var corpusBaseline = corpusRoot != null
                ? CountFixtures(Path.Combine(corpusRoot, nameof(Opcode.MSG_MOVE_WORLDPORT_ACK)))
                : 0;
            var worldportWindowBaseline = worldportWindowDir != null
                ? CountForegroundWindowsWithPacket(
                    worldportWindowDir,
                    nameof(Opcode.MSG_MOVE_WORLDPORT_ACK),
                    "Send")
                : 0;

            _output.WriteLine("[FG-ACK] Returning FG from Ironforge to Orgrimmar to capture the foreground worldport ACK window.");
            var returned = await StageForegroundCapturePointAsync(
                target,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "ack-capture Orgrimmar return",
                cleanSlate: false,
                xyToleranceYards: 10f);
            Assert.True(returned, "FG bot should settle in Orgrimmar after the cross-map return hop.");
            returnedToOrg = true;

            await _bot.RefreshSnapshotsAsync();
            fgSnap = _bot.ForegroundBot;
            Assert.NotNull(fgSnap);
            Assert.Equal((uint)KalimdorMapId, fgSnap!.Player?.Unit?.GameObject?.Base?.MapId ?? 0U);

            if (corpusRoot != null)
            {
                var worldportDir = Path.Combine(corpusRoot, nameof(Opcode.MSG_MOVE_WORLDPORT_ACK));
                var captured = await WaitForFixtureCountAsync(
                    worldportDir,
                    corpusBaseline + 1,
                    TimeSpan.FromSeconds(10));
                Assert.True(captured,
                    $"Expected {nameof(Opcode.MSG_MOVE_WORLDPORT_ACK)} fixture under '{worldportDir}' when WWOW_CAPTURE_ACK_CORPUS=1.");
            }

            if (worldportWindowDir != null)
            {
                var captured = await WaitForForegroundWindowWithPacketCountAsync(
                    worldportWindowDir,
                    nameof(Opcode.MSG_MOVE_WORLDPORT_ACK),
                    "Send",
                    worldportWindowBaseline + 1,
                    TimeSpan.FromSeconds(15));
                Assert.True(captured,
                    $"Expected a foreground packet-window fixture containing {nameof(Opcode.MSG_MOVE_WORLDPORT_ACK)} under '{worldportWindowDir}' " +
                    "when WWOW_CAPTURE_POST_TELEPORT_WINDOW=1.");
            }
        }
        finally
        {
            if (!returnedToOrg)
            {
                await StageForegroundCapturePointAsync(
                    target,
                    KalimdorMapId,
                    OrgX,
                    OrgY,
                    OrgZ,
                    "ack-capture Orgrimmar return",
                    cleanSlate: false,
                    xyToleranceYards: 10f);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task ForegroundAndBackground_Knockback_CapturesPacketWindows()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable("WWOW_CAPTURE_POST_TELEPORT_WINDOW"),
                "1",
                StringComparison.Ordinal)
            && string.Equals(
                Environment.GetEnvironmentVariable("WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW"),
                "1",
                StringComparison.Ordinal),
            "WWOW_CAPTURE_POST_TELEPORT_WINDOW=1 and WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1 are required.");

        var fgTarget = await EnsureAckCaptureForegroundTargetAsync();

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.BgAccountName),
            "BG bot not available for knockback packet-window capture.");

        var bgTarget = _bot
            .ResolveBotRunnerActionTargets(includeForegroundIfActionable: false, foregroundFirst: false)
            .FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "BG bot required for knockback packet-window capture.");

        _output.WriteLine(
            $"=== FG/BG knockback packet-window capture: FG {fgTarget.AccountName}/{fgTarget.CharacterName}, " +
            $"BG {bgTarget.AccountName}/{bgTarget.CharacterName} ===");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {fgTarget.AccountName}/{fgTarget.CharacterName}: knockback binary oracle capture target.");
        _output.WriteLine(
            $"[ACTION-PLAN] {bgTarget.RoleLabel} {bgTarget.AccountName}/{bgTarget.CharacterName}: BG knockback parity capture target.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no knockback dispatch.");

        var fgWindowDir = ResolvePostTeleportWindowDirectory();
        var bgWindowDir = ResolveBackgroundPostTeleportWindowDirectory();
        Assert.NotNull(fgWindowDir);
        Assert.NotNull(bgWindowDir);
        Directory.CreateDirectory(fgWindowDir!);
        Directory.CreateDirectory(bgWindowDir!);

        await PrepareKnockbackCombatTargetAsync(fgTarget);
        var fgPreStageCount = CountForegroundFixtures(fgWindowDir!);
        var fgStaged = await StageForegroundCapturePointAsync(
            fgTarget,
            KnockbackStageMapId,
            KnockbackStageX,
            KnockbackStageY,
            KnockbackStageZ,
            "knockback-capture Taragaman FG stage",
            cleanSlate: false,
            xyToleranceYards: 25f);
        Assert.True(fgStaged, "FG bot should settle near a real knockback creature before knockback capture.");
        var fgStageWindowClosed = await WaitForForegroundFixtureCountAsync(
            fgWindowDir!,
            fgPreStageCount + 1,
            TimeSpan.FromSeconds(5));
        Assert.True(fgStageWindowClosed,
            $"Expected FG staging packet-window fixture under '{fgWindowDir}' before knockback capture.");

        await PrepareKnockbackCombatTargetAsync(bgTarget);
        var bgPreStageCount = CountBackgroundFixtures(bgWindowDir!);
        var bgStaged = await _bot.StageBotRunnerAtNavigationPointAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel,
            KnockbackStageMapId,
            KnockbackStageX,
            KnockbackStageY,
            KnockbackStageZ,
            "knockback-capture Taragaman BG stage",
            cleanSlate: false,
            xyToleranceYards: 25f,
            zStabilizationWaitMs: 1000);
        Assert.True(bgStaged, "BG bot should settle near a real knockback creature before knockback capture.");
        var bgStageWindowClosed = await WaitForBackgroundFixtureCountAsync(
            bgWindowDir!,
            bgPreStageCount + 1,
            TimeSpan.FromSeconds(5));
        Assert.True(bgStageWindowClosed,
            $"Expected BG staging packet-window fixture under '{bgWindowDir}' before knockback capture.");

        try
        {
            await TriggerCreatureKnockbackAsync(fgTarget);
            var fgCaptured = await WaitForForegroundScenarioFixtureCountAsync(
                fgWindowDir!,
                "knockback_packet_window",
                CountWindowFixturesByScenario(fgWindowDir!, "foreground_*.json", "knockback_packet_window") + 1,
                TimeSpan.FromSeconds(30));
            Assert.True(fgCaptured,
                $"Expected new FG knockback packet-window fixture under '{fgWindowDir}' after real knockback-creature combat.");

            await StopAttackIfPossibleAsync(fgTarget);
            await StageForegroundCapturePointAsync(
                fgTarget,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "knockback-capture Orgrimmar FG isolate",
                cleanSlate: false,
                xyToleranceYards: 10f);

            await TriggerCreatureKnockbackAsync(bgTarget);
            var bgCaptured = await WaitForScenarioFixtureCountAsync(
                bgWindowDir!,
                "background_*.json",
                "knockback_packet_window",
                CountWindowFixturesByScenario(bgWindowDir!, "background_*.json", "knockback_packet_window") + 1,
                TimeSpan.FromSeconds(30));
            Assert.True(bgCaptured,
                $"Expected new BG knockback packet-window fixture under '{bgWindowDir}' after real knockback-creature combat.");
        }
        finally
        {
            await StopAttackIfPossibleAsync(fgTarget);
            await StopAttackIfPossibleAsync(bgTarget);

            await StageForegroundCapturePointAsync(
                fgTarget,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "knockback-capture Orgrimmar FG return",
                cleanSlate: false,
                xyToleranceYards: 10f);

            await _bot.StageBotRunnerAtNavigationPointAsync(
                bgTarget.AccountName,
                bgTarget.RoleLabel,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "knockback-capture Orgrimmar BG return",
                cleanSlate: false,
                xyToleranceYards: 10f,
                zStabilizationWaitMs: 1000);
        }
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task Foreground_VerticalDropTeleport_CapturesTeleportAckAndSnapWindow()
    {
        var target = await EnsureAckCaptureForegroundTargetAsync();
        _output.WriteLine(
            $"=== FG ACK Capture: vertical-drop teleport with {target.AccountName}/{target.CharacterName} ===");

        // Stage on the flat Durotar road (matches BgPostTeleportStabilizationTests so the
        // FG capture is directly comparable to the existing BG snapshot regression).
        const int DurotarMapId = 1;
        const float DurotarX = -460f;
        const float DurotarY = -4760f;
        const float DurotarGroundZ = 38f;
        const float DurotarTeleportZ = DurotarGroundZ + 10f;

        var staged = await StageForegroundCapturePointAsync(
            target,
            DurotarMapId,
            DurotarX,
            DurotarY,
            DurotarGroundZ,
            "ack-capture Durotar ground stage",
            cleanSlate: true,
            xyToleranceYards: 8f);
        Assert.True(staged, "FG bot should settle on Durotar road before the vertical-drop trigger.");

        var corpusRoot = ResolveCorpusOutputDirectory();
        var teleportAckBaseline = corpusRoot != null
            ? CountFixtures(Path.Combine(corpusRoot, nameof(Opcode.MSG_MOVE_TELEPORT_ACK)))
            : 0;

        var windowDir = ResolvePostTeleportWindowDirectory();
        var windowBaseline = windowDir != null ? CountFixtures(windowDir) : 0;

        try
        {
            _output.WriteLine(
                $"[FG-ACK] Triggering same-map teleport to ({DurotarX:F1},{DurotarY:F1},{DurotarTeleportZ:F1}) " +
                "to force inbound MSG_MOVE_TELEPORT and outbound MSG_MOVE_TELEPORT_ACK + snap-window packets.");

            var triggered = await StageForegroundCapturePointAsync(
                target,
                DurotarMapId,
                DurotarX,
                DurotarY,
                DurotarTeleportZ,
                "ack-capture vertical-drop trigger",
                cleanSlate: false,
                xyToleranceYards: 8f);
            Assert.True(triggered, "FG bot should settle after the vertical-drop teleport trigger.");

            if (corpusRoot != null)
            {
                var teleportAckDir = Path.Combine(corpusRoot, nameof(Opcode.MSG_MOVE_TELEPORT_ACK));
                var captured = await WaitForFixtureCountAsync(
                    teleportAckDir,
                    teleportAckBaseline + 1,
                    TimeSpan.FromSeconds(10));
                Assert.True(captured,
                    $"Expected new {nameof(Opcode.MSG_MOVE_TELEPORT_ACK)} fixture under '{teleportAckDir}' " +
                    $"after vertical-drop teleport (baseline={teleportAckBaseline}).");
            }

            if (windowDir != null)
            {
                var captured = await WaitForFixtureCountAsync(
                    windowDir,
                    windowBaseline + 1,
                    TimeSpan.FromSeconds(10));
                Assert.True(captured,
                    $"Expected new post-teleport packet window fixture under '{windowDir}' " +
                    $"after vertical-drop teleport (baseline={windowBaseline}).");
            }
        }
        finally
        {
            await StageForegroundCapturePointAsync(
                target,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "ack-capture Orgrimmar return",
                cleanSlate: false,
                xyToleranceYards: 10f);
        }
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task Background_VerticalDropTeleport_CapturesPostTeleportWindow()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable("WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW"),
                "1",
                StringComparison.Ordinal),
            "WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1 not set; BG post-teleport window capture is opt-in.");

        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");
        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.BgAccountName),
            "BG bot not available for post-teleport window capture.");

        var bgTarget = _bot
            .ResolveBotRunnerActionTargets(includeForegroundIfActionable: false, foregroundFirst: false)
            .FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "BG bot required for post-teleport window capture.");

        _output.WriteLine(
            $"=== BG post-teleport window capture: vertical-drop teleport with {bgTarget.AccountName}/{bgTarget.CharacterName} ===");
        _output.WriteLine(
            $"[ACTION-PLAN] {bgTarget.RoleLabel} {bgTarget.AccountName}/{bgTarget.CharacterName}: BG post-teleport capture target.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no BG capture dispatch.");

        const int DurotarMapId = 1;
        const float DurotarX = -460f;
        const float DurotarY = -4760f;
        const float DurotarGroundZ = 38f;
        const float DurotarTeleportZ = DurotarGroundZ + 10f;

        await _bot.EnsureCleanSlateAsync(bgTarget.AccountName, bgTarget.RoleLabel);

        var staged = await _bot.StageBotRunnerAtNavigationPointAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel,
            DurotarMapId,
            DurotarX,
            DurotarY,
            DurotarGroundZ,
            "bg-post-teleport ground stage",
            cleanSlate: false,
            xyToleranceYards: 8f,
            zStabilizationWaitMs: 1000);
        Assert.True(staged, "BG bot should settle on Durotar road before vertical-drop trigger.");

        var windowDir = ResolveBackgroundPostTeleportWindowDirectory();
        Assert.NotNull(windowDir);
        var baselineCount = CountBackgroundFixtures(windowDir!);

        try
        {
            _output.WriteLine(
                $"[BG-WINDOW] Triggering same-map teleport to ({DurotarX:F1},{DurotarY:F1},{DurotarTeleportZ:F1}) " +
                "to force inbound MSG_MOVE_TELEPORT_ACK + outbound snap-window packets.");

            var triggered = await _bot.StageBotRunnerAtNavigationPointAsync(
                bgTarget.AccountName,
                bgTarget.RoleLabel,
                DurotarMapId,
                DurotarX,
                DurotarY,
                DurotarTeleportZ,
                "bg-post-teleport vertical-drop trigger",
                cleanSlate: false,
                xyToleranceYards: 8f,
                zStabilizationWaitMs: 1000);
            Assert.True(triggered, "BG bot should settle after vertical-drop teleport trigger.");

            var captured = await WaitForBackgroundFixtureCountAsync(
                windowDir!,
                baselineCount + 1,
                TimeSpan.FromSeconds(15));
            Assert.True(captured,
                $"Expected new BG post-teleport packet window fixture under '{windowDir}' " +
                $"after vertical-drop teleport (baseline={baselineCount}).");
        }
        finally
        {
            await _bot.StageBotRunnerAtNavigationPointAsync(
                bgTarget.AccountName,
                bgTarget.RoleLabel,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "bg-post-teleport Orgrimmar return",
                cleanSlate: false,
                xyToleranceYards: 10f,
                zStabilizationWaitMs: 1000);
        }
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task Background_CrossMapTeleport_CapturesPostTeleportWindow()
    {
        global::Tests.Infrastructure.Skip.IfNot(
            string.Equals(
                Environment.GetEnvironmentVariable("WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW"),
                "1",
                StringComparison.Ordinal),
            "WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1 not set; BG post-teleport window capture is opt-in.");

        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");
        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.BgAccountName),
            "BG bot not available for post-teleport window capture.");

        var bgTarget = _bot
            .ResolveBotRunnerActionTargets(includeForegroundIfActionable: false, foregroundFirst: false)
            .FirstOrDefault(t => !t.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bgTarget.AccountName),
            "BG bot required for post-teleport window capture.");

        _output.WriteLine(
            $"=== BG post-teleport window capture: cross-map teleport with {bgTarget.AccountName}/{bgTarget.CharacterName} ===");
        _output.WriteLine(
            $"[ACTION-PLAN] {bgTarget.RoleLabel} {bgTarget.AccountName}/{bgTarget.CharacterName}: BG cross-map post-teleport capture target.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no BG capture dispatch.");

        var windowDir = ResolveBackgroundPostTeleportWindowDirectory();
        Assert.NotNull(windowDir);
        Directory.CreateDirectory(windowDir!);

        await _bot.EnsureCleanSlateAsync(bgTarget.AccountName, bgTarget.RoleLabel);

        var preStageCount = CountBackgroundFixtures(windowDir!);
        var startSettled = await _bot.StageBotRunnerAtNavigationPointAsync(
            bgTarget.AccountName,
            bgTarget.RoleLabel,
            KalimdorMapId,
            OrgX,
            OrgY,
            OrgZ,
            "bg-post-teleport Orgrimmar cross-map start",
            cleanSlate: false,
            xyToleranceYards: 10f,
            zStabilizationWaitMs: 1000);
        Assert.True(startSettled, "BG bot should settle in Orgrimmar before the cross-map capture hop.");

        var stagingWindowClosed = await WaitForBackgroundFixtureCountAsync(
            windowDir!,
            preStageCount + 1,
            TimeSpan.FromSeconds(5));
        Assert.True(stagingWindowClosed,
            $"Expected BG setup teleport window fixture under '{windowDir}' before starting the cross-map capture.");
        var baselineCount = CountBackgroundFixtures(windowDir!);

        try
        {
            _output.WriteLine(
                "[BG-WINDOW] Moving BG from Kalimdor to Ironforge to force SMSG_TRANSFER_PENDING/SMSG_NEW_WORLD + BG worldport ACK.");

            var settled = await _bot.StageBotRunnerAtNavigationPointAsync(
                bgTarget.AccountName,
                bgTarget.RoleLabel,
                EasternKingdomsMapId,
                IronforgeX,
                IronforgeY,
                IronforgeZ,
                "bg-post-teleport Ironforge cross-map hop",
                cleanSlate: false,
                xyToleranceYards: 25f,
                zStabilizationWaitMs: 1000);
            Assert.True(settled, "BG bot should settle in Ironforge after the cross-map capture hop.");

            await _bot.RefreshSnapshotsAsync();
            var bgSnap = await _bot.GetSnapshotAsync(bgTarget.AccountName);
            Assert.NotNull(bgSnap);
            Assert.Equal((uint)EasternKingdomsMapId, bgSnap!.Player?.Unit?.GameObject?.Base?.MapId ?? 0U);

            var captured = await WaitForBackgroundFixtureCountAsync(
                windowDir!,
                baselineCount + 1,
                TimeSpan.FromSeconds(15));
            Assert.True(captured,
                $"Expected new BG cross-map post-teleport packet window fixture under '{windowDir}' " +
                $"after Kalimdor -> Eastern Kingdoms teleport (baseline={baselineCount}).");
        }
        finally
        {
            await _bot.StageBotRunnerAtNavigationPointAsync(
                bgTarget.AccountName,
                bgTarget.RoleLabel,
                KalimdorMapId,
                OrgX,
                OrgY,
                OrgZ,
                "bg-post-teleport Orgrimmar return",
                cleanSlate: false,
                xyToleranceYards: 10f,
                zStabilizationWaitMs: 1000);
        }
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled()
    {
        var command = Environment.GetEnvironmentVariable("WWOW_ACK_CAPTURE_GM_COMMAND");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(command), "WWOW_ACK_CAPTURE_GM_COMMAND not set");

        var prepCommands = ParseCommands(Environment.GetEnvironmentVariable("WWOW_ACK_CAPTURE_PREP_GM_COMMANDS"));
        var resetCommand = Environment.GetEnvironmentVariable("WWOW_ACK_CAPTURE_RESET_GM_COMMAND");
        var target = await EnsureAckCaptureForegroundTargetAsync();

        var settled = await StageForegroundCapturePointAsync(
            target,
            KalimdorMapId,
            OrgX,
            OrgY,
            OrgZ,
            "ack-capture command Orgrimmar start",
            cleanSlate: true,
            xyToleranceYards: 10f);
        Assert.True(settled, "FG bot should settle in Orgrimmar before the ACK probe command.");

        foreach (var prepCommand in prepCommands)
        {
            _output.WriteLine($"[FG-ACK-PROBE] Prep command: {prepCommand}");
            var prepTrace = await _bot.StageBotRunnerAckCaptureCommandAsync(
                target.AccountName,
                target.RoleLabel,
                prepCommand,
                captureResponse: true);
            Assert.Equal(ResponseResult.Success, prepTrace.DispatchResult);
        }

        var corpusRoot = ResolveCorpusOutputDirectory();
        var baselineCounts = corpusRoot != null
            ? CaptureFixtureCounts(corpusRoot)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            _output.WriteLine($"[FG-ACK-PROBE] Sending command: {command}");
            var trace = await _bot.StageBotRunnerAckCaptureCommandAsync(
                target.AccountName,
                target.RoleLabel,
                command!,
                captureResponse: true);
            Assert.Equal(ResponseResult.Success, trace.DispatchResult);

            foreach (var chat in trace.ChatMessages)
                _output.WriteLine($"[FG-ACK-PROBE] [CHAT] {chat}");

            foreach (var error in trace.ErrorMessages)
                _output.WriteLine($"[FG-ACK-PROBE] [ERROR] {error}");

            if (corpusRoot == null)
                return;

            var deltaCounts = await WaitForNewFixturesAsync(corpusRoot, baselineCounts, TimeSpan.FromSeconds(10));
            var capturedOpcodes = deltaCounts
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.True(
                capturedOpcodes.Length > 0,
                $"Expected at least one new ACK fixture under '{corpusRoot}' after '{command}', but none appeared. " +
                $"Chat=[{string.Join(" | ", trace.ChatMessages)}] Errors=[{string.Join(" | ", trace.ErrorMessages)}]");

            _output.WriteLine("[FG-ACK-PROBE] New ACK fixtures:");
            foreach (var pair in capturedOpcodes)
                _output.WriteLine($"[FG-ACK-PROBE]   {pair.Key}: +{pair.Value}");

            var expectedOpcodes = ParseExpectedAckOpcodes();
            foreach (var opcode in expectedOpcodes)
            {
                Assert.True(
                    deltaCounts.TryGetValue(opcode, out var count) && count > 0,
                    $"Expected '{opcode}' fixture(s) after '{command}', but captured [{string.Join(", ", capturedOpcodes.Select(p => p.Key))}].");
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(resetCommand))
            {
                _output.WriteLine($"[FG-ACK-PROBE] Resetting command state via: {resetCommand}");
                await _bot.StageBotRunnerAckCaptureCommandAsync(
                    target.AccountName,
                    target.RoleLabel,
                    resetCommand,
                    captureResponse: false);
            }
        }
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureAckCaptureForegroundTargetAsync()
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
            "FG bot not available for ACK corpus capture.");
        global::Tests.Infrastructure.Skip.IfNot(
            await _bot.CheckFgActionableAsync(requireTeleportProbe: false),
            "FG bot not actionable for ACK corpus capture.");

        var targets = _bot.ResolveBotRunnerActionTargets(
            includeForegroundIfActionable: true,
            foregroundFirst: true);
        var target = targets.Single(candidate => candidate.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] FG {target.AccountName}/{target.CharacterName}: ACK corpus capture target.");
        _output.WriteLine(
            $"[ACTION-PLAN] BG {_bot.BgAccountName}/{_bot.BgCharacterName}: launched idle for Shodan topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no ACK capture dispatch.");

        return target;
    }

    private async Task<bool> StageForegroundCapturePointAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        int mapId,
        float x,
        float y,
        float z,
        string label,
        bool cleanSlate,
        float xyToleranceYards)
        => await _bot.StageBotRunnerAtNavigationPointAsync(
            target.AccountName,
            target.RoleLabel,
            mapId,
            x,
            y,
            z,
            label,
            cleanSlate,
            xyToleranceYards,
            zStabilizationWaitMs: 1000);

    private async Task PrepareKnockbackCombatTargetAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: true,
            clearInventoryFirst: false,
            levelTo: KnockbackCombatLevel);
    }

    private async Task TriggerCreatureKnockbackAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var targetVisible = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => FindKnockbackCreatureGuid(snapshot) != 0UL,
            TimeSpan.FromSeconds(20),
            pollIntervalMs: 500,
            progressLabel: $"{target.RoleLabel} knockback creature visibility");
        if (!targetVisible)
        {
            await _bot.RefreshSnapshotsAsync();
            var missedSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
            _output.WriteLine(
                $"[KNOCKBACK-CAPTURE] {target.RoleLabel} nearby units before failure: " +
                DescribeNearbyUnits(missedSnapshot));
        }

        Assert.True(targetVisible,
            $"{target.RoleLabel} should see a living knockback creature before knockback capture.");

        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var creatureGuid = FindKnockbackCreatureGuid(snapshot);
        Assert.NotEqual(0UL, creatureGuid);

        var creature = snapshot?.NearbyUnits?.FirstOrDefault(
            unit => (unit.GameObject?.Base?.Guid ?? 0UL) == creatureGuid);
        _output.WriteLine(
            $"[KNOCKBACK-CAPTURE] {target.RoleLabel} engaging " +
            $"{creature?.GameObject?.Name ?? "knockback creature"} " +
            $"entry={creature?.GameObject?.Entry ?? 0} GUID=0x{creatureGuid:X} " +
            $"HP={creature?.Health ?? 0}/{creature?.MaxHealth ?? 0}.");

        var attack = await _bot.SendActionAsync(
            target.AccountName,
            new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)creatureGuid } }
            });
        Assert.Equal(ResponseResult.Success, attack);
    }

    private async Task StopAttackIfPossibleAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.AccountName))
            return;

        await _bot.SendActionAsync(
            target.AccountName,
            new ActionMessage { ActionType = ActionType.StopAttack });
    }

    private static ulong FindKnockbackCreatureGuid(WoWActivitySnapshot? snapshot)
    {
        var playerPosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        if (playerPosition == null)
            return 0UL;

        var candidate = snapshot?.NearbyUnits?
            .Where(unit =>
            {
                var guid = unit.GameObject?.Base?.Guid ?? 0UL;
                if (guid == 0UL)
                    return false;

                if (unit.Health == 0 || unit.MaxHealth == 0)
                    return false;

                if (unit.NpcFlags != 0)
                    return false;

                var entry = unit.GameObject?.Entry ?? 0;
                return entry == StormscaleWaveRiderEntry
                    || entry == LordSinslayerEntry
                    || entry == TaragamanTheHungererEntry;
            })
            .Select(unit =>
            {
                var position = unit.GameObject?.Base?.Position;
                var distance = position == null
                    ? float.MaxValue
                    : LiveBotFixture.Distance2D(playerPosition.X, playerPosition.Y, position.X, position.Y);
                return new
                {
                    Guid = unit.GameObject?.Base?.Guid ?? 0UL,
                    Distance = distance
                };
            })
            .OrderBy(unit => unit.Distance)
            .FirstOrDefault();

        return candidate?.Guid ?? 0UL;
    }

    private static string DescribeNearbyUnits(WoWActivitySnapshot? snapshot)
    {
        var units = snapshot?.NearbyUnits?
            .Take(12)
            .Select(unit =>
                $"{unit.GameObject?.Name ?? "?"}" +
                $" entry={unit.GameObject?.Entry ?? 0}" +
                $" guid=0x{unit.GameObject?.Base?.Guid ?? 0UL:X}" +
                $" hp={unit.Health}/{unit.MaxHealth}" +
                $" npcFlags={unit.NpcFlags}")
            .ToArray();

        return units is { Length: > 0 }
            ? string.Join("; ", units)
            : "(none)";
    }

    private static string? ResolvePostTeleportWindowDirectory()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("WWOW_CAPTURE_POST_TELEPORT_WINDOW"),
                "1",
                StringComparison.Ordinal))
            return null;

        var explicitPath = Environment.GetEnvironmentVariable("WWOW_POST_TELEPORT_WINDOW_OUTPUT");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var repoRoot = Environment.GetEnvironmentVariable("WWOW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(repoRoot) && File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
        {
            return Path.Combine(
                Path.GetFullPath(repoRoot),
                "Tests",
                "WoWSharpClient.Tests",
                "Fixtures",
                "post_teleport_packet_window");
        }

        return null;
    }

    private static int CountFixtures(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, "*.json").Count();
    }

    private static int CountForegroundFixtures(string directory)
        => CountFixtures(directory, "foreground_*.json");

    private static int CountFixtures(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, searchPattern).Count();
    }

    private static string? ResolveBackgroundPostTeleportWindowDirectory()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW"),
                "1",
                StringComparison.Ordinal))
            return null;

        var explicitPath = Environment.GetEnvironmentVariable("WWOW_BG_POST_TELEPORT_OUTPUT");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var repoRoot = Environment.GetEnvironmentVariable("WWOW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(repoRoot) && File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
        {
            return Path.Combine(
                Path.GetFullPath(repoRoot),
                "Tests",
                "WoWSharpClient.Tests",
                "Fixtures",
                "post_teleport_packet_window");
        }

        return null;
    }

    private static int CountBackgroundFixtures(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, "background_*.json").Count();
    }

    private static async Task<bool> WaitForBackgroundFixtureCountAsync(string directory, int target, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountBackgroundFixtures(directory) >= target)
                return true;

            await Task.Delay(250);
        }

        return CountBackgroundFixtures(directory) >= target;
    }

    private static async Task<bool> WaitForForegroundFixtureCountAsync(string directory, int target, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountForegroundFixtures(directory) >= target)
                return true;

            await Task.Delay(250);
        }

        return CountForegroundFixtures(directory) >= target;
    }

    private static async Task<bool> WaitForForegroundScenarioFixtureCountAsync(
        string directory,
        string scenario,
        int minimumCount,
        TimeSpan timeout)
        => await WaitForScenarioFixtureCountAsync(
            directory,
            "foreground_*.json",
            scenario,
            minimumCount,
            timeout);

    private static async Task<bool> WaitForScenarioFixtureCountAsync(
        string directory,
        string searchPattern,
        string scenario,
        int minimumCount,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountWindowFixturesByScenario(directory, searchPattern, scenario) >= minimumCount)
                return true;

            await Task.Delay(250);
        }

        return CountWindowFixturesByScenario(directory, searchPattern, scenario) >= minimumCount;
    }

    private static async Task<bool> WaitForForegroundWindowWithPacketCountAsync(
        string directory,
        string opcodeName,
        string direction,
        int minimumCount,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountForegroundWindowsWithPacket(directory, opcodeName, direction) >= minimumCount)
                return true;

            await Task.Delay(250);
        }

        return CountForegroundWindowsWithPacket(directory, opcodeName, direction) >= minimumCount;
    }

    private static int CountForegroundWindowsWithPacket(string directory, string opcodeName, string direction)
        => CountWindowFixturesWithPacket(directory, "foreground_*.json", opcodeName, direction);

    private static int CountWindowFixturesByScenario(string directory, string searchPattern, string scenario)
    {
        if (!Directory.Exists(directory))
            return 0;

        var count = 0;
        foreach (var path in Directory.EnumerateFiles(directory, searchPattern))
        {
            if (FixtureScenarioEquals(path, scenario))
                count++;
        }

        return count;
    }

    private static int CountWindowFixturesWithPacket(
        string directory,
        string searchPattern,
        string opcodeName,
        string direction)
    {
        if (!Directory.Exists(directory))
            return 0;

        var count = 0;
        foreach (var path in Directory.EnumerateFiles(directory, searchPattern))
        {
            if (FixtureContainsPacket(path, opcodeName, direction))
                count++;
        }

        return count;
    }

    private static bool FixtureScenarioEquals(string path, string scenario)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("CaptureScenario", out var property)
                && string.Equals(property.GetString(), scenario, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool FixtureContainsPacket(string path, string opcodeName, string direction)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("Packets", out var packets)
                || packets.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var packet in packets.EnumerateArray())
            {
                if (!packet.TryGetProperty("OpcodeName", out var packetOpcode)
                    || !packet.TryGetProperty("Direction", out var packetDirection))
                {
                    continue;
                }

                if (string.Equals(packetOpcode.GetString(), opcodeName, StringComparison.Ordinal)
                    && string.Equals(packetDirection.GetString(), direction, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForFixtureCountAsync(string directory, int target, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CountFixtures(directory) >= target)
                return true;

            await Task.Delay(250);
        }

        return CountFixtures(directory) >= target;
    }

    private static string? ResolveCorpusOutputDirectory()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WWOW_CAPTURE_ACK_CORPUS"), "1", StringComparison.Ordinal))
            return null;

        var explicitPath = Environment.GetEnvironmentVariable("WWOW_ACK_CORPUS_OUTPUT");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        var repoRoot = Environment.GetEnvironmentVariable("WWOW_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(repoRoot) && File.Exists(Path.Combine(repoRoot, "WestworldOfWarcraft.sln")))
        {
            return Path.Combine(
                Path.GetFullPath(repoRoot),
                "Tests",
                "WoWSharpClient.Tests",
                "Fixtures",
                "ack_golden_corpus");
        }

        return null;
    }

    private static IReadOnlyList<string> ParseExpectedAckOpcodes()
    {
        var raw = Environment.GetEnvironmentVariable("WWOW_ACK_CAPTURE_EXPECTED_OPCODES");
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseCommands(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, int> CaptureFixtureCounts(string corpusRoot)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(corpusRoot))
            return counts;

        foreach (var directory in Directory.EnumerateDirectories(corpusRoot))
        {
            counts[Path.GetFileName(directory)] = Directory.EnumerateFiles(directory, "*.json").Count();
        }

        return counts;
    }

    private static async Task<Dictionary<string, int>> WaitForNewFixturesAsync(
        string corpusRoot,
        IReadOnlyDictionary<string, int> baselineCounts,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var deltas = ComputeFixtureDeltas(corpusRoot, baselineCounts);
            if (deltas.Values.Any(count => count > 0))
                return deltas;

            await Task.Delay(500);
        }

        return ComputeFixtureDeltas(corpusRoot, baselineCounts);
    }

    private static Dictionary<string, int> ComputeFixtureDeltas(
        string corpusRoot,
        IReadOnlyDictionary<string, int> baselineCounts)
    {
        var currentCounts = CaptureFixtureCounts(corpusRoot);
        var deltas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in currentCounts)
        {
            baselineCounts.TryGetValue(pair.Key, out var baselineCount);
            deltas[pair.Key] = pair.Value - baselineCount;
        }

        foreach (var pair in baselineCounts)
        {
            if (!deltas.ContainsKey(pair.Key))
                deltas[pair.Key] = -pair.Value;
        }

        return deltas;
    }

    private static async Task<bool> WaitForFixtureAsync(string directory, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json").Any())
                return true;

            await Task.Delay(500);
        }

        return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json").Any();
    }

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve repository path for {Path.Combine(segments)} from {AppContext.BaseDirectory}.");
    }
}
