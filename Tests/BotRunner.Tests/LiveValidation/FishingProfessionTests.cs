using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Authoritative dual-bot fishing live validation.
///
/// The test does nothing directly: no GM chat commands, no teleport helpers,
/// no fixture prep methods. It points <see cref="LiveBotFixture"/> at
/// <c>Fishing.config.json</c> — which assigns TESTBOT1 (FG) and TESTBOT2 (BG)
/// the <c>Fishing[Ratchet]</c> activity with <c>UseGmCommands=true</c> —
/// then polls each bot's snapshot until the <c>FishingTask fishing_loot_success</c>
/// diagnostic appears. Everything between "bot entered world" and "caught a fish"
/// is owned by <see cref="BotRunner.Tasks.FishingTask"/>, which the
/// <see cref="BotRunner.Activities.ActivityResolver"/> instantiates directly with
/// <c>location="Ratchet"</c>. The task itself drives the GM-command outfit
/// setup and the <c>.tele name &lt;character&gt; Ratchet</c> travel — there is
/// no per-(activity, location) wrapper class.
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
        var fishingSettingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Fishing.config.json");

        await _bot.EnsureSettingsAsync(fishingSettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");

        var fgAccount = _bot.FgAccountName;
        var bgAccount = _bot.BgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG bot account not available.");
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG bot account not available.");

        // Force-respawn every fishing pool near the Ratchet pier before dispatching the
        // activity. `.pool update` schedules new pool members with a fresh respawn delay
        // (PoolManager::Spawn1Object with instantly=false), so a freshly harvested master
        // pool 2628 won't present visible pools for several minutes. Teleporting through
        // each DB-recorded spawn location and issuing `.gobject select` + `.gobject
        // respawn` bypasses the delay and puts the closest pool into visibility for the
        // fishing task. We run this on Shodan (the dedicated GM-admin character, female
        // Gnome Mage) so the teleports don't race with the fishing task's own `.tele
        // Ratchet` on the test bots.
        const int kalimdorMapId = 1;
        const float ratchetLandingX = -956.7f;
        const float ratchetLandingY = -3754.7f;
        const float ratchetLandingZ = 5.3f;
        const float poolSearchRadius = 100f;

        var shodanAccount = _bot.ShodanAccountName;
        if (!string.IsNullOrWhiteSpace(shodanAccount))
        {
            await _bot.EnsureShodanLoadoutAsync(shodanAccount!, _bot.ShodanCharacterName);

            var respawned = await _bot.RespawnFishingPoolsNearAsync(
                shodanAccount!,
                kalimdorMapId,
                ratchetLandingX,
                ratchetLandingY,
                poolSearchRadius,
                stagingZ: ratchetLandingZ + 2f,
                stagingX: ratchetLandingX,
                stagingY: ratchetLandingY,
                maxLocations: 5);
            _output.WriteLine(
                $"[FISHING] Pre-test respawned {respawned} fishing pool spawn locations around the Ratchet pier via Shodan.");
        }
        else
        {
            _output.WriteLine("[FISHING] Shodan admin bot not available; skipping pre-test pool respawn.");
        }

        _output.WriteLine(
            $"[FISHING] Waiting up to {FishingLootDeadline.TotalMinutes:F0}m for both FG ('{fgAccount}') and BG ('{bgAccount}') " +
            "to report FishingTask fishing_loot_success via the Fishing[Ratchet] activity.");

        var result = await WaitForDualLootSuccessAsync(fgAccount!, bgAccount!, FishingLootDeadline);

        Assert.True(result.FgSawActivityStart,
            $"[FG] Activity start diagnostic '{ActivityStartMarker}' never appeared; activity did not dispatch. " +
            $"{FormatChatTail(result.FgRecentChat)}");
        Assert.True(result.BgSawActivityStart,
            $"[BG] Activity start diagnostic '{ActivityStartMarker}' never appeared; activity did not dispatch. " +
            $"{FormatChatTail(result.BgRecentChat)}");

        Assert.True(result.FgSawPoolAcquired,
            $"[FG] FishingTask never acquired a pool; activity moved to fish phase but no pool entered cast range. " +
            $"{FormatChatTail(result.FgRecentChat)}");
        Assert.True(result.BgSawPoolAcquired,
            $"[BG] FishingTask never acquired a pool; activity moved to fish phase but no pool entered cast range. " +
            $"{FormatChatTail(result.BgRecentChat)}");

        Assert.True(result.FgSawLootSuccess,
            $"[FG] FishingTask never reached fishing_loot_success within {FishingLootDeadline.TotalMinutes:F0}m. " +
            $"{FormatChatTail(result.FgRecentChat)}");
        Assert.True(result.BgSawLootSuccess,
            $"[BG] FishingTask never reached fishing_loot_success within {FishingLootDeadline.TotalMinutes:F0}m. " +
            $"{FormatChatTail(result.BgRecentChat)}");

        _output.WriteLine(
            $"[FISHING] Both bots reported fishing_loot_success. FG last loot: '{result.FgLastLootLine}' | BG last loot: '{result.BgLastLootLine}'");
    }

    private async Task<DualFishingPollResult> WaitForDualLootSuccessAsync(string fgAccount, string bgAccount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var fgSawActivity = false;
        var bgSawActivity = false;
        var fgSawPool = false;
        var bgSawPool = false;
        var fgSawLoot = false;
        var bgSawLoot = false;
        string fgLastLoot = string.Empty;
        string bgLastLoot = string.Empty;
        IReadOnlyList<string> fgRecentChat = [];
        IReadOnlyList<string> bgRecentChat = [];
        var lastProgressLog = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            await _bot.RefreshSnapshotsAsync();
            var fgSnap = _bot.AllBots.FirstOrDefault(snap =>
                string.Equals(snap.AccountName, fgAccount, StringComparison.OrdinalIgnoreCase));
            var bgSnap = _bot.AllBots.FirstOrDefault(snap =>
                string.Equals(snap.AccountName, bgAccount, StringComparison.OrdinalIgnoreCase));

            fgRecentChat = fgSnap?.RecentChatMessages.ToArray() ?? (IReadOnlyList<string>)[];
            bgRecentChat = bgSnap?.RecentChatMessages.ToArray() ?? (IReadOnlyList<string>)[];

            fgSawActivity |= fgRecentChat.Any(m => m.Contains(ActivityStartMarker, StringComparison.Ordinal));
            bgSawActivity |= bgRecentChat.Any(m => m.Contains(ActivityStartMarker, StringComparison.Ordinal));
            fgSawPool |= fgRecentChat.Any(m => m.Contains(PoolAcquiredMarker, StringComparison.Ordinal));
            bgSawPool |= bgRecentChat.Any(m => m.Contains(PoolAcquiredMarker, StringComparison.Ordinal));

            var fgLoot = fgRecentChat.LastOrDefault(m => m.Contains(LootSuccessMarker, StringComparison.Ordinal));
            var bgLoot = bgRecentChat.LastOrDefault(m => m.Contains(LootSuccessMarker, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(fgLoot)) { fgSawLoot = true; fgLastLoot = fgLoot; }
            if (!string.IsNullOrEmpty(bgLoot)) { bgSawLoot = true; bgLastLoot = bgLoot; }

            if (fgSawLoot && bgSawLoot)
                break;

            if (DateTime.UtcNow - lastProgressLog >= TimeSpan.FromSeconds(15))
            {
                lastProgressLog = DateTime.UtcNow;
                _output.WriteLine(
                    $"[FISHING] Polling... FG present={fgSnap != null} activity={fgSawActivity} pool={fgSawPool} loot={fgSawLoot} | " +
                    $"BG present={bgSnap != null} activity={bgSawActivity} pool={bgSawPool} loot={bgSawLoot}");
            }

            await Task.Delay(1000);
        }

        return new DualFishingPollResult(
            FgSawActivityStart: fgSawActivity,
            BgSawActivityStart: bgSawActivity,
            FgSawPoolAcquired: fgSawPool,
            BgSawPoolAcquired: bgSawPool,
            FgSawLootSuccess: fgSawLoot,
            BgSawLootSuccess: bgSawLoot,
            FgLastLootLine: fgLastLoot,
            BgLastLootLine: bgLastLoot,
            FgRecentChat: fgRecentChat,
            BgRecentChat: bgRecentChat);
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

    private sealed record DualFishingPollResult(
        bool FgSawActivityStart,
        bool BgSawActivityStart,
        bool FgSawPoolAcquired,
        bool BgSawPoolAcquired,
        bool FgSawLootSuccess,
        bool BgSawLootSuccess,
        string FgLastLootLine,
        string BgLastLootLine,
        IReadOnlyList<string> FgRecentChat,
        IReadOnlyList<string> BgRecentChat);
}
