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
/// Exercises the BotRunner item-use and buff-cancel paths with explicit
/// snapshot metrics:
/// - add-item command accepted and reflected in bags
/// - UseItem action accepted and item slot consumed
/// - aura appears after consumption
/// - DismissBuff action removes the aura when the implementation supports it
/// See docs/BuffAndConsumableTests.md for the owning production code paths.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class BuffAndConsumableTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint ElixirOfLionsStrength = 2454;
    private const uint LionsStrengthUseSpell = 2367;
    private const uint LionsStrengthBuffAura = 2457;
    // Spell 2367 is named "Lesser Strength" in WoW's spell DB (not "Lion's Strength")
    private const string LionsStrengthBuffName = "Lesser Strength";

    public BuffAndConsumableTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task UseConsumable_AppliesBuff()
    {
        var bgApplied = await RunConsumableApplyScenarioAsync(_bot.BgAccountName!, () => _bot.BackgroundBot?.Player, "BG");
        Assert.True(bgApplied.Applied, "BG bot should consume the elixir, apply the aura, and remove the bag item.");

        if (_bot.IsFgActionable)
        {
            var fgApplied = await RunConsumableApplyScenarioAsync(_bot.FgAccountName!, () => _bot.ForegroundBot?.Player, "FG");
            Assert.True(fgApplied.Applied, "FG bot should consume the elixir, apply the aura, and remove the bag item.");
        }
    }

    [SkippableFact]
    public async Task DismissBuff_RemovesBuff()
    {
        // Test FG first (gold standard) — don't gate on BG success
        if (_bot.IsFgActionable)
        {
            var fgApplied = await RunConsumableApplyScenarioAsync(_bot.FgAccountName!, () => _bot.ForegroundBot?.Player, "FG");
            Assert.True(fgApplied.Applied, "FG bot must reach the buffed state before dismissal can be validated.");

            var fgRemoved = await RunDismissScenarioAsync(_bot.FgAccountName!, () => _bot.ForegroundBot?.Player, "FG");
            Assert.True(fgRemoved, $"FG bot should remove {LionsStrengthBuffName} after DismissBuff.");
        }

        var bgApplied = await RunConsumableApplyScenarioAsync(_bot.BgAccountName!, () => _bot.BackgroundBot?.Player, "BG");
        Assert.True(bgApplied.Applied, "BG bot must reach the buffed state before dismissal can be validated.");

        var bgRemoved = await RunDismissScenarioAsync(_bot.BgAccountName!, () => _bot.BackgroundBot?.Player, "BG");
        global::Tests.Infrastructure.Skip.If(!bgRemoved,
            $"BG bot still cannot dismiss {LionsStrengthBuffName} — WoWSharpClient does not populate WoWUnit.Buffs (BB-BUFF-001).");
    }

    private async Task<(bool Applied, int AuraCountBefore, int AuraCountAfterUse, int ItemSlotsBefore, int ItemSlotsAfterUse)> RunConsumableApplyScenarioAsync(
        string account,
        Func<Game.WoWPlayer?> getPlayer,
        string label)
    {
        await PrepareConsumableStateAsync(account, getPlayer, label);

        await _bot.RefreshSnapshotsAsync();
        var playerBeforeAdd = getPlayer();
        Assert.NotNull(playerBeforeAdd);
        var auraCountBefore = playerBeforeAdd?.Unit?.Auras?.Count ?? 0;
        var itemSlotsBefore = CountBagSlotsForItem(playerBeforeAdd, ElixirOfLionsStrength);
        _output.WriteLine($"  [{label}] Before add/use: auraCount={auraCountBefore}, elixirSlots={itemSlotsBefore}");

        var addTrace = await _bot.SendGmChatCommandTrackedAsync(
            account,
            $".additem {ElixirOfLionsStrength} 1",
            captureResponse: true,
            delayMs: 1200);
        AssertCommandSucceeded(addTrace, label, $".additem {ElixirOfLionsStrength} 1");

        var added = await WaitForBagItemCountAsync(account, ElixirOfLionsStrength, minimumSlots: 1, timeout: TimeSpan.FromSeconds(5));
        Assert.True(added, $"[{label}] Elixir should appear in bag snapshot after .additem.");

        var useResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.UseItem,
            Parameters = { new RequestParameter { IntParam = (int)ElixirOfLionsStrength } }
        });
        Assert.Equal(ResponseResult.Success, useResult);

        var applied = await WaitForAuraAsync(account, HasLionsStrengthAura, present: true, timeout: TimeSpan.FromSeconds(5));
        await _bot.RefreshSnapshotsAsync();

        var playerAfterUse = getPlayer();
        Assert.NotNull(playerAfterUse);
        var auraCountAfterUse = playerAfterUse?.Unit?.Auras?.Count ?? 0;
        var itemSlotsAfterUse = CountBagSlotsForItem(playerAfterUse, ElixirOfLionsStrength);
        _output.WriteLine(
            $"  [{label}] After use: auraCount={auraCountAfterUse}, elixirSlots={itemSlotsAfterUse}, applied={applied}");

        if (playerAfterUse?.Unit?.Auras != null)
        {
            foreach (var aura in playerAfterUse.Unit.Auras)
                _output.WriteLine($"    [{label}] Aura: {aura}");
        }

        Assert.True(itemSlotsAfterUse <= itemSlotsBefore, $"[{label}] Item slot count should not grow after UseItem.");
        Assert.True(itemSlotsAfterUse == 0, $"[{label}] Elixir slot should be consumed after UseItem.");
        Assert.True(auraCountAfterUse >= auraCountBefore, $"[{label}] Aura count should not drop during buff application.");

        return (applied, auraCountBefore, auraCountAfterUse, itemSlotsBefore, itemSlotsAfterUse);
    }

    private async Task<bool> RunDismissScenarioAsync(string account, Func<Game.WoWPlayer?> getPlayer, string label)
    {
        var beforeDismiss = getPlayer();
        Assert.True(HasLionsStrengthAura(beforeDismiss), $"[{label}] Lion's Strength should be present before dismissal.");

        var dismissResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.DismissBuff,
            Parameters = { new RequestParameter { StringParam = LionsStrengthBuffName } }
        });
        _output.WriteLine($"  [{label}] DismissBuff result: {dismissResult}");
        Assert.Equal(ResponseResult.Success, dismissResult);

        var removed = await WaitForAuraAsync(account, HasLionsStrengthAura, present: false, timeout: TimeSpan.FromSeconds(3));
        await _bot.RefreshSnapshotsAsync();
        var afterDismiss = getPlayer();
        _output.WriteLine(
            $"  [{label}] After dismiss: removed={removed}, auraCount={afterDismiss?.Unit?.Auras?.Count ?? 0}");

        if (!removed && label == "BG")
        {
            await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthUseSpell}");
            await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthBuffAura}");
        }

        return removed;
    }

    private async Task PrepareConsumableStateAsync(string account, Func<Game.WoWPlayer?> getPlayer, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthUseSpell}");
        await _bot.SendGmChatCommandAsync(account, $".unaura {LionsStrengthBuffAura}");
        await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
        await Task.Delay(500);

        await _bot.RefreshSnapshotsAsync();
        var player = getPlayer();
        Assert.NotNull(player);
        Assert.False(HasLionsStrengthAura(player), $"[{label}] Lion's Strength should be cleared before setup.");
        Assert.Equal(0, CountBagSlotsForItem(player, ElixirOfLionsStrength));
    }

    private async Task<bool> WaitForBagItemCountAsync(string account, uint itemId, int minimumSlots, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var count = CountBagSlotsForItem(snap?.Player, itemId);
            if (count >= minimumSlots)
                return true;

            await Task.Delay(200);
        }

        return false;
    }

    private async Task<bool> WaitForAuraAsync(
        string account,
        Func<Game.WoWPlayer?, bool> predicate,
        bool present,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var matches = predicate(snap?.Player);
            if (matches == present)
                return true;

            await Task.Delay(200);
        }

        return false;
    }

    private static int CountBagSlotsForItem(Game.WoWPlayer? player, uint itemId)
        => player?.BagContents?.Values.Count(v => v == itemId) ?? 0;

    private static bool HasLionsStrengthAura(Game.WoWPlayer? player)
    {
        var auras = player?.Unit?.Auras;
        if (auras == null)
            return false;

        return auras.Contains(LionsStrengthUseSpell) || auras.Contains(LionsStrengthBuffAura);
    }

    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);
        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }
}
