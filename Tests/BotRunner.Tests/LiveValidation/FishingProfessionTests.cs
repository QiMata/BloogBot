using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tests.Infrastructure;
using WoWStateManager.Settings;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Authoritative dual-bot fishing live validation.
///
/// The fixture uses the dedicated GM admin bot Shodan to stage a pier-reachable
/// Ratchet pool before the fishing bots launch. The test first runs a
/// Shodan-only settings file, equips Shodan, rotates/respawns Barrens master
/// pool 2628 until a close pool is visible from the Ratchet landing, then
/// restarts into <c>Fishing.config.json</c> where TESTBOT1 (FG) and TESTBOT2
/// (BG) auto-run <c>Fishing[Ratchet]</c>. Everything from the fishing bots'
/// world-entry to <c>FishingTask fishing_loot_success</c> remains owned by
/// <see cref="BotRunner.Tasks.FishingTask"/> and the
/// <see cref="BotRunner.Activities.ActivityResolver"/>.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class FishingProfessionTests
{
    private static readonly TimeSpan FishingLootDeadline = TimeSpan.FromMinutes(3);
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
        var shodanOnlySettingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Fishing.ShodanOnly.config.json");
        var (fgOnlySettingsPath, bgOnlySettingsPath) = CreateSingleBotFishingSettings();

        await PrepareShodanStagedPoolAsync(shodanOnlySettingsPath);

        await _bot.EnsureSettingsAsync(fgOnlySettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");

        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot account not available.");

        _output.WriteLine(
            $"[FISHING] Waiting up to {FishingLootDeadline.TotalMinutes:F0}m for FG ('{fgAccount}') " +
            "to report FishingTask fishing_loot_success via the Fishing[Ratchet] activity.");

        var fgResult = await WaitForSingleLootSuccessAsync(fgAccount!, "FG", FishingLootDeadline);

        Assert.True(fgResult.SawActivityStart,
            $"[FG] Activity start diagnostic '{ActivityStartMarker}' never appeared; activity did not dispatch. " +
            $"{FormatChatTail(fgResult.RecentChat)}");
        Assert.True(fgResult.SawPoolAcquired,
            $"[FG] FishingTask never acquired a pool; activity moved to fish phase but no pool entered cast range. " +
            $"{FormatChatTail(fgResult.RecentChat)}");
        Assert.True(fgResult.SawLootSuccess,
            $"[FG] FishingTask never reached fishing_loot_success within {FishingLootDeadline.TotalMinutes:F0}m. " +
            $"{FormatChatTail(fgResult.RecentChat)}");

        await PrepareShodanStagedPoolAsync(shodanOnlySettingsPath);

        await _bot.EnsureSettingsAsync(bgOnlySettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");

        var bgAccount = _bot.BgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG bot account not available.");

        _output.WriteLine(
            $"[FISHING] Waiting up to {FishingLootDeadline.TotalMinutes:F0}m for BG ('{bgAccount}') " +
            "to report FishingTask fishing_loot_success via the Fishing[Ratchet] activity.");

        var bgResult = await WaitForSingleLootSuccessAsync(bgAccount!, "BG", FishingLootDeadline);

        Assert.True(bgResult.SawActivityStart,
            $"[BG] Activity start diagnostic '{ActivityStartMarker}' never appeared; activity did not dispatch. " +
            $"{FormatChatTail(bgResult.RecentChat)}");
        Assert.True(bgResult.SawPoolAcquired,
            $"[BG] FishingTask never acquired a pool; activity moved to fish phase but no pool entered cast range. " +
            $"{FormatChatTail(bgResult.RecentChat)}");
        Assert.True(bgResult.SawLootSuccess,
            $"[BG] FishingTask never reached fishing_loot_success within {FishingLootDeadline.TotalMinutes:F0}m. " +
            $"{FormatChatTail(bgResult.RecentChat)}");

        _output.WriteLine(
            $"[FISHING] Both roles reported fishing_loot_success in isolated runs. FG last loot: '{fgResult.LastLootLine}' | BG last loot: '{bgResult.LastLootLine}'");
    }

    private async Task PrepareShodanStagedPoolAsync(string shodanOnlySettingsPath)
    {
        await _bot.EnsureSettingsAsync(shodanOnlySettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");

        const int kalimdorMapId = 1;
        const float ratchetLandingX = -956.7f;
        const float ratchetLandingY = -3754.7f;
        const float ratchetLandingZ = 5.3f;

        var shodanAccount = _bot.ShodanAccountName;
        Assert.False(
            string.IsNullOrWhiteSpace(shodanAccount),
            "Shodan admin bot was not launched by Fishing.ShodanOnly.config.json.");

        await _bot.EnsureShodanAdminLoadoutAsync(shodanAccount!, _bot.ShodanCharacterName);

        var poolReady = await _bot.EnsureCloseFishingPoolActiveNearAsync(
            shodanAccount!,
            kalimdorMapId,
            ratchetLandingX,
            ratchetLandingY,
            stagingZ: ratchetLandingZ + 2f,
            acceptDistance: 55f,
            rotateRadius: 200f,
            respawnLimit: 5,
            maxIterations: 5);

        Assert.True(
            poolReady,
            "[FISHING] Shodan could not surface a close Ratchet pool before the fishing bot launched.");
        _output.WriteLine("[FISHING] Pre-run pool setup via Shodan: poolReady=True.");
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

    private static (string FgOnlySettingsPath, string BgOnlySettingsPath) CreateSingleBotFishingSettings()
    {
        var fishingRoster = CoordinatorFixtureBase.LoadCharacterSettingsFromConfig("Fishing.config.json");

        var fgOnlyRoster = fishingRoster
            .Where(settings => settings.RunnerType == BotRunnerType.Foreground)
            .ToArray();
        var bgOnlyRoster = fishingRoster
            .Where(settings => settings.RunnerType == BotRunnerType.Background
                && !string.Equals(settings.AccountName, "SHODAN", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Single(fgOnlyRoster);
        Assert.Single(bgOnlyRoster);

        var fgOnlySettingsPath = CoordinatorFixtureBase.WriteSettingsFile(
            fgOnlyRoster,
            "Fishing.FgOnly.runtime.config.json");
        var bgOnlySettingsPath = CoordinatorFixtureBase.WriteSettingsFile(
            bgOnlyRoster,
            "Fishing.BgOnly.runtime.config.json");

        return (fgOnlySettingsPath, bgOnlySettingsPath);
    }

    private sealed record SingleFishingPollResult(
        bool SawActivityStart,
        bool SawPoolAcquired,
        bool SawLootSuccess,
        string LastLootLine,
        IReadOnlyList<string> RecentChat);
}
