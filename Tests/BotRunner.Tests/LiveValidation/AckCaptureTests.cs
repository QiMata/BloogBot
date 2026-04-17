using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
