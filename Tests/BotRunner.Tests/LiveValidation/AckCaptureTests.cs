using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            if (corpusRoot != null)
            {
                var worldportDir = Path.Combine(corpusRoot, nameof(Opcode.MSG_MOVE_WORLDPORT_ACK));
                var captured = await WaitForFixtureAsync(worldportDir, TimeSpan.FromSeconds(10));
                Assert.True(captured,
                    $"Expected {nameof(Opcode.MSG_MOVE_WORLDPORT_ACK)} fixture under '{worldportDir}' when WWOW_CAPTURE_ACK_CORPUS=1.");
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
