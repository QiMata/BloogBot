using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Authoritative dual-bot fishing live validation.
///
/// The fixture uses a single FG+BG+Shodan roster launch. Shodan stages a
/// pier-reachable Ratchet pool, then FG and BG stay idle until the test
/// explicitly dispatches <c>ActionType.StartFishing</c> for each phase. Once
/// dispatched, everything from <c>FishingTask activity_start</c> to
/// <c>FishingTask fishing_loot_success</c> remains owned by
/// <see cref="BotRunner.Tasks.FishingTask"/>.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class FishingProfessionTests
{
    private static readonly TimeSpan FishingLootDeadline = TimeSpan.FromMinutes(3);
    private const string RatchetLocation = "Ratchet";
    private const int RatchetMasterPoolId = 2628;
    private const int KalimdorMapId = 1;
    private const float RatchetLandingX = -956.7f;
    private const float RatchetLandingY = -3754.7f;
    private const float RatchetLandingZ = 5.3f;
    private const string LootSuccessMarker = "[TASK] FishingTask fishing_loot_success";
    private const string PoolAcquiredMarker = "[TASK] FishingTask pool_acquired";
    private const string ActivityStartMarker = "[TASK] FishingTask activity_start";

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public FishingProfessionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Fishing_CatchFish_BgAndFg_RatchetStagedPool()
    {
        var fishingSettingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Fishing.config.json");

        await _bot.EnsureSettingsAsync(fishingSettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");

        var shodanAccount = _bot.ShodanAccountName;
        var fgAccount = _bot.FgAccountName;
        var bgAccount = _bot.BgAccountName;

        Assert.False(
            string.IsNullOrWhiteSpace(shodanAccount),
            "Shodan admin bot was not launched by Fishing.config.json.");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot account not available.");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG bot account not available.");

        await _bot.EnsureShodanAdminLoadoutAsync(shodanAccount!, _bot.ShodanCharacterName);

        var fgStageReady = await _bot.EnsureCloseFishingPoolActiveNearAsync(
            shodanAccount!,
            KalimdorMapId,
            RatchetLandingX,
            RatchetLandingY,
            stagingZ: RatchetLandingZ + 2f,
            acceptDistance: 55f,
            rotateRadius: 200f,
            respawnLimit: 5,
            maxIterations: 5);
        Assert.True(
            fgStageReady,
            "[FISHING] Shodan could not surface a close Ratchet pool before the FG phase.");

        var fgDispatchResult = await _bot.SendActionAsync(fgAccount!, CreateStartFishingAction());
        Assert.Equal(ResponseResult.Success, fgDispatchResult);

        _output.WriteLine(
            $"[FISHING] Waiting up to {FishingLootDeadline.TotalMinutes:F0}m for FG ('{fgAccount}') " +
            "to report FishingTask fishing_loot_success via an action-dispatched FishingTask.");

        var fgResult = await WaitForSingleLootSuccessAsync(fgAccount!, "FG", FishingLootDeadline);

        AssertFishingSuccess("FG", fgResult);

        var bgStageReady = await _bot.EnsureCloseFishingPoolActiveNearAsync(
            shodanAccount!,
            KalimdorMapId,
            RatchetLandingX,
            RatchetLandingY,
            stagingZ: RatchetLandingZ + 2f,
            acceptDistance: 55f,
            rotateRadius: 200f,
            respawnLimit: 5,
            maxIterations: 5);
        Assert.True(
            bgStageReady,
            "[FISHING] Shodan could not surface a close Ratchet pool before the BG phase.");

        var bgDispatchResult = await _bot.SendActionAsync(bgAccount!, CreateStartFishingAction());
        Assert.Equal(ResponseResult.Success, bgDispatchResult);

        _output.WriteLine(
            $"[FISHING] Waiting up to {FishingLootDeadline.TotalMinutes:F0}m for BG ('{bgAccount}') " +
            "to report FishingTask fishing_loot_success via an action-dispatched FishingTask.");

        var bgResult = await WaitForSingleLootSuccessAsync(bgAccount!, "BG", FishingLootDeadline);

        AssertFishingSuccess("BG", bgResult);

        _output.WriteLine(
            $"[FISHING] Both roles reported fishing_loot_success without roster restarts. FG last loot: '{fgResult.LastLootLine}' | BG last loot: '{bgResult.LastLootLine}'");
    }

    private async Task<SingleFishingPollResult> WaitForSingleLootSuccessAsync(string accountName, string roleLabel, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var sawActivity = false;
        var sawPool = false;
        var sawLoot = false;
        string lastLoot = string.Empty;
        IReadOnlyList<string> recentChat = [];
        var lastProgressLog = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = _bot.AllBots.FirstOrDefault(bot =>
                string.Equals(bot.AccountName, accountName, StringComparison.OrdinalIgnoreCase));

            recentChat = snap?.RecentChatMessages.ToArray() ?? (IReadOnlyList<string>)[];

            sawActivity |= recentChat.Any(m => m.Contains(ActivityStartMarker, StringComparison.Ordinal));
            sawPool |= recentChat.Any(m => m.Contains(PoolAcquiredMarker, StringComparison.Ordinal));

            var loot = recentChat.LastOrDefault(m => m.Contains(LootSuccessMarker, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(loot))
            {
                sawLoot = true;
                lastLoot = loot;
            }

            if (sawLoot)
                break;

            if (DateTime.UtcNow - lastProgressLog >= TimeSpan.FromSeconds(15))
            {
                lastProgressLog = DateTime.UtcNow;
                _output.WriteLine(
                    $"[FISHING] Polling {roleLabel}... present={snap != null} activity={sawActivity} pool={sawPool} loot={sawLoot}");
            }

            await Task.Delay(1000);
        }

        return new SingleFishingPollResult(
            SawActivityStart: sawActivity,
            SawPoolAcquired: sawPool,
            SawLootSuccess: sawLoot,
            LastLootLine: lastLoot,
            RecentChat: recentChat);
    }

    private static string FormatChatTail(IReadOnlyList<string> messages, int tailCount = 10)
    {
        if (messages.Count == 0)
            return "recentChat=(empty)";

        var tail = messages
            .TakeLast(tailCount)
            .Select(m => m.Length > 140 ? m[..140] + "..." : m);
        return "recentChat=[" + string.Join(" || ", tail) + "]";
    }

    private static ActionMessage CreateStartFishingAction()
    {
        return new ActionMessage
        {
            ActionType = ActionType.StartFishing,
            Parameters =
            {
                new RequestParameter { StringParam = RatchetLocation },
                new RequestParameter { IntParam = 1 },
                new RequestParameter { IntParam = RatchetMasterPoolId },
            }
        };
    }

    private static void AssertFishingSuccess(string roleLabel, SingleFishingPollResult result)
    {
        Assert.True(result.SawActivityStart,
            $"[{roleLabel}] Activity start diagnostic '{ActivityStartMarker}' never appeared; action dispatch never started FishingTask. " +
            $"{FormatChatTail(result.RecentChat)}");
        Assert.True(result.SawPoolAcquired,
            $"[{roleLabel}] FishingTask never acquired a pool; task started but no pool entered cast range. " +
            $"{FormatChatTail(result.RecentChat)}");
        Assert.True(result.SawLootSuccess,
            $"[{roleLabel}] FishingTask never reached fishing_loot_success within {FishingLootDeadline.TotalMinutes:F0}m. " +
            $"{FormatChatTail(result.RecentChat)}");
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

    private sealed record SingleFishingPollResult(
        bool SawActivityStart,
        bool SawPoolAcquired,
        bool SawLootSuccess,
        string LastLootLine,
        IReadOnlyList<string> RecentChat);
}
