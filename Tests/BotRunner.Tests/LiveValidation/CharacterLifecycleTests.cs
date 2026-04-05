using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Snapshot validation for the lowest-level inventory add-item path that many
/// other live tests depend on.
/// See docs/CharacterLifecycleTests.md for the owning production code paths.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class CharacterLifecycleTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public CharacterLifecycleTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Equipment_AddItemToInventory()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.FgAccountName!, "FG");
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed;
        bool fgPassed = false;
        var hasFg = _bot.IsFgActionable;

        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG add-item scenarios in parallel.");

            var bgTask = RunAddItemScenarioAsync(bgAccount, "BG", LiveBotFixture.TestItems.LinenCloth, 1, "Linen Cloth");
            var fgTask = RunAddItemScenarioAsync(fgAccount, "FG", LiveBotFixture.TestItems.LinenCloth, 1, "Linen Cloth");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunAddItemScenarioAsync(bgAccount, "BG", LiveBotFixture.TestItems.LinenCloth, 1, "Linen Cloth");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: expected Linen Cloth to appear in bag snapshot after .additem.");
        if (hasFg)
        {
            Assert.True(fgPassed, "FG bot: expected Linen Cloth to appear in bag snapshot after .additem. " +
                "If FG ObjectManager item enumeration is broken, fix it instead of hiding the failure.");
        }
    }

    private async Task<bool> RunAddItemScenarioAsync(
        string account,
        string label,
        uint itemId,
        int count,
        string itemName)
    {
        await _bot.EnsureStrictAliveAsync(account, label);
        await _bot.RefreshSnapshotsAsync();

        var baseline = await _bot.GetSnapshotAsync(account);
        if (baseline?.Player == null)
            return false;

        var beforeSlotsForItem = CountBagSlotsForItem(baseline.Player, itemId);
        var totalBagSlots = baseline.Player.BagContents.Count;
        _output.WriteLine($"  [{label}] {itemName} slots before setup: {beforeSlotsForItem}, bag entries: {totalBagSlots}");

        if (beforeSlotsForItem > 0 || totalBagSlots >= 15)
        {
            _output.WriteLine($"  [{label}] Clearing backpack for deterministic {itemName} add verification.");
            await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();
            baseline = await _bot.GetSnapshotAsync(account);
            if (baseline?.Player == null)
                return false;

            beforeSlotsForItem = CountBagSlotsForItem(baseline.Player, itemId);
            Assert.Equal(0, beforeSlotsForItem);
            _output.WriteLine($"  [{label}] {itemName} slots after cleanup: {beforeSlotsForItem}");
        }

        var trace = await _bot.SendGmChatCommandTrackedAsync(
            account,
            $".additem {itemId} {count}",
            captureResponse: true,
            delayMs: 1200);
        AssertCommandSucceeded(trace, label, $".additem {itemId} {count}");

        var appeared = await WaitForBagItemPresenceAsync(account, itemId, TimeSpan.FromSeconds(10));
        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var afterSlotsForItem = CountBagSlotsForItem(after?.Player, itemId);

        _output.WriteLine($"  [{label}] {itemName} slots before/after: {beforeSlotsForItem} -> {afterSlotsForItem}");
        return appeared && afterSlotsForItem > 0;
    }

    private async Task<bool> WaitForBagItemPresenceAsync(string account, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var hasItem = snap?.Player?.BagContents?.Values.Any(v => v == itemId) == true;
            if (hasItem)
                return true;

            await Task.Delay(400);
        }

        return false;
    }

    private static int CountBagSlotsForItem(Game.WoWPlayer? player, uint itemId)
        => player?.BagContents?.Values.Count(v => v == itemId) ?? 0;

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
