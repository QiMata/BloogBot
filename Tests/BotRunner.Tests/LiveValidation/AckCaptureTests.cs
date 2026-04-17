using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

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
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "AckCaptureLive")]
    public async Task Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled()
    {
        var fgAccount = _bot.FgAccountName!;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot not available");

        await _bot.EnsureCleanSlateAsync(fgAccount, "FG");

        await _bot.BotTeleportAsync(fgAccount, KalimdorMapId, OrgX, OrgY, OrgZ);
        var startSettled = await _bot.WaitForTeleportSettledAsync(
            fgAccount,
            OrgX,
            OrgY,
            timeoutMs: 10000,
            progressLabel: "FG ack-capture-org",
            xyToleranceYards: 10f);
        Assert.True(startSettled, "FG bot should settle in Orgrimmar before the cross-map capture hop.");

        try
        {
            _output.WriteLine("[FG-ACK] Teleporting FG from Kalimdor to Ironforge to force SMSG_NEW_WORLD -> MSG_MOVE_WORLDPORT_ACK.");

            await _bot.BotTeleportAsync(fgAccount, EasternKingdomsMapId, IronforgeX, IronforgeY, IronforgeZ);
            var settled = await _bot.WaitForTeleportSettledAsync(
                fgAccount,
                IronforgeX,
                IronforgeY,
                timeoutMs: 15000,
                progressLabel: "FG ack-capture-ironforge",
                xyToleranceYards: 25f);
            Assert.True(settled, "FG bot should settle in Ironforge after the cross-map teleport.");

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
            await _bot.BotTeleportAsync(fgAccount, KalimdorMapId, OrgX, OrgY, OrgZ);
            await _bot.WaitForTeleportSettledAsync(
                fgAccount,
                OrgX,
                OrgY,
                timeoutMs: 15000,
                progressLabel: "FG ack-capture-return",
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

        var fgAccount = _bot.FgAccountName!;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot not available");

        await _bot.EnsureCleanSlateAsync(fgAccount, "FG");

        await _bot.BotTeleportAsync(fgAccount, KalimdorMapId, OrgX, OrgY, OrgZ);
        var settled = await _bot.WaitForTeleportSettledAsync(
            fgAccount,
            OrgX,
            OrgY,
            timeoutMs: 10000,
            progressLabel: "FG ack-probe-org",
            xyToleranceYards: 10f);
        Assert.True(settled, "FG bot should settle in Orgrimmar before the ACK probe command.");

        foreach (var prepCommand in prepCommands)
        {
            _output.WriteLine($"[FG-ACK-PROBE] Prep GM command: {prepCommand}");
            var prepTrace = await _bot.SendGmChatCommandTrackedAsync(fgAccount, prepCommand, captureResponse: true);
            Assert.Equal(ResponseResult.Success, prepTrace.DispatchResult);
        }

        var corpusRoot = ResolveCorpusOutputDirectory();
        var baselineCounts = corpusRoot != null
            ? CaptureFixtureCounts(corpusRoot)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            _output.WriteLine($"[FG-ACK-PROBE] Sending GM command: {command}");
            var trace = await _bot.SendGmChatCommandTrackedAsync(fgAccount, command!, captureResponse: true);
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
                _output.WriteLine($"[FG-ACK-PROBE] Resetting GM command state via: {resetCommand}");
                await _bot.SendGmChatCommandTrackedAsync(fgAccount, resetCommand, captureResponse: false);
            }
        }
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
}
