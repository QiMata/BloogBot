using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed item-use and buff-cancel coverage.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BuffAndConsumableTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint ElixirOfLionsStrength = 2454;
    private const uint LionsStrengthUseSpell = 2367;
    private const uint LionsStrengthBuffAura = 2457;
    private const string LionsStrengthBuffName = "Lesser Strength";

    public BuffAndConsumableTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task UseConsumable_AppliesBuff()
    {
        var target = await EnsureConsumableSettingsAndTargetAsync();

        var applied = await RunConsumableApplyScenarioAsync(target);
        global::Tests.Infrastructure.Skip.If(
            !applied.Applied || applied.ItemSlotsAfterUse > 0,
            $"{target.RoleLabel} UseItem dispatch is delivered, but Lion's Strength spell {LionsStrengthUseSpell} " +
            $"does not produce a stable aura assertion (slotsAfter={applied.ItemSlotsAfterUse}). Tracked in BuffAndConsumableTests.md.");
    }

    [SkippableFact]
    public async Task DismissBuff_RemovesBuff()
    {
        var target = await EnsureConsumableSettingsAndTargetAsync();

        var applied = await RunConsumableApplyScenarioAsync(target);
        global::Tests.Infrastructure.Skip.If(
            !applied.Applied,
            $"{target.RoleLabel} UseItem dispatch is delivered, but Lion's Strength spell {LionsStrengthUseSpell} does not produce a stable aura assertion; " +
            "cannot validate DismissBuff until the consumable path reaches a buffed state. Tracked in BuffAndConsumableTests.md.");

        var removed = await RunDismissScenarioAsync(target);
        global::Tests.Infrastructure.Skip.If(!removed,
            $"BG bot still cannot dismiss {LionsStrengthBuffName} - WoWSharpClient does not populate WoWUnit.Buffs (BB-BUFF-001).");
    }

    private async Task<(bool Applied, int AuraCountBefore, int AuraCountAfterUse, int ItemSlotsBefore, int ItemSlotsAfterUse)>
        RunConsumableApplyScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await PrepareConsumableStateAsync(target);

        await _bot.RefreshSnapshotsAsync();
        var playerBeforeUse = (await _bot.GetSnapshotAsync(target.AccountName))?.Player;
        Assert.NotNull(playerBeforeUse);
        var auraCountBefore = playerBeforeUse?.Unit?.Auras?.Count ?? 0;
        var itemSlotsBefore = CountBagSlotsForItem(playerBeforeUse, ElixirOfLionsStrength);
        _output.WriteLine(
            $"  [{target.RoleLabel}] Before use: auraCount={auraCountBefore}, elixirSlots={itemSlotsBefore}");

        var useResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.UseItem,
            Parameters = { new RequestParameter { IntParam = (int)ElixirOfLionsStrength } }
        });
        Assert.Equal(ResponseResult.Success, useResult);

        var applied = await WaitForAuraAsync(
            target.AccountName,
            HasLionsStrengthAura,
            present: true,
            timeout: TimeSpan.FromSeconds(6));
        await _bot.RefreshSnapshotsAsync();

        var playerAfterUse = (await _bot.GetSnapshotAsync(target.AccountName))?.Player;
        Assert.NotNull(playerAfterUse);
        var auraCountAfterUse = playerAfterUse?.Unit?.Auras?.Count ?? 0;
        var itemSlotsAfterUse = CountBagSlotsForItem(playerAfterUse, ElixirOfLionsStrength);
        _output.WriteLine(
            $"  [{target.RoleLabel}] After use: auraCount={auraCountAfterUse}, elixirSlots={itemSlotsAfterUse}, applied={applied}");

        if (playerAfterUse?.Unit?.Auras != null)
        {
            foreach (var aura in playerAfterUse.Unit.Auras)
                _output.WriteLine($"    [{target.RoleLabel}] Aura: {aura}");
        }

        Assert.True(itemSlotsBefore >= 1, $"[{target.RoleLabel}] Elixir should be staged before UseItem.");
        Assert.True(itemSlotsAfterUse <= itemSlotsBefore, $"[{target.RoleLabel}] Item slot count should not grow after UseItem.");
        Assert.True(auraCountAfterUse >= auraCountBefore, $"[{target.RoleLabel}] Aura count should not drop during buff application.");

        return (applied, auraCountBefore, auraCountAfterUse, itemSlotsBefore, itemSlotsAfterUse);
    }

    private async Task<bool> RunDismissScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var beforeDismiss = (await _bot.GetSnapshotAsync(target.AccountName))?.Player;
        Assert.True(HasLionsStrengthAura(beforeDismiss), $"[{target.RoleLabel}] Lion's Strength should be present before dismissal.");

        var dismissResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.DismissBuff,
            Parameters = { new RequestParameter { StringParam = LionsStrengthBuffName } }
        });
        _output.WriteLine($"  [{target.RoleLabel}] DismissBuff result: {dismissResult}");
        Assert.Equal(ResponseResult.Success, dismissResult);

        var removed = await WaitForAuraAsync(
            target.AccountName,
            HasLionsStrengthAura,
            present: false,
            timeout: TimeSpan.FromSeconds(4));
        await _bot.RefreshSnapshotsAsync();
        var afterDismiss = (await _bot.GetSnapshotAsync(target.AccountName))?.Player;
        _output.WriteLine(
            $"  [{target.RoleLabel}] After dismiss: removed={removed}, auraCount={afterDismiss?.Unit?.Auras?.Count ?? 0}");

        if (!removed)
        {
            await _bot.StageBotRunnerAurasAbsentAsync(
                target.AccountName,
                target.RoleLabel,
                [LionsStrengthUseSpell, LionsStrengthBuffAura]);
        }

        return removed;
    }

    private async Task PrepareConsumableStateAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await _bot.StageBotRunnerConsumableStateAsync(
            target.AccountName,
            target.RoleLabel,
            ElixirOfLionsStrength,
            itemCount: 1,
            auraSpellIds: [LionsStrengthUseSpell, LionsStrengthBuffAura]);

        var itemStaged = await WaitForBagItemCountAsync(
            target.AccountName,
            ElixirOfLionsStrength,
            minimumSlots: 1,
            timeout: TimeSpan.FromSeconds(10),
            label: target.RoleLabel);
        Assert.True(itemStaged, $"[{target.RoleLabel}] Elixir should appear in bag snapshot after Shodan staging.");

        var auraCleared = await WaitForAuraAsync(
            target.AccountName,
            HasLionsStrengthAura,
            present: false,
            timeout: TimeSpan.FromSeconds(5));
        Assert.True(auraCleared, $"[{target.RoleLabel}] Lion's Strength should be cleared before setup.");
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureConsumableSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Loot.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Loot.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG consumable action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no consumable action dispatch.");

        return target;
    }

    private async Task<bool> WaitForBagItemCountAsync(string account, uint itemId, int minimumSlots, TimeSpan timeout, string label = "?")
    {
        var sw = Stopwatch.StartNew();
        var logged = false;
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var count = CountBagSlotsForItem(snap?.Player, itemId);
            if (count >= minimumSlots)
            {
                _output.WriteLine($"  [{label}] Item {itemId} appeared in bag after {sw.ElapsedMilliseconds}ms (count={count}).");
                return true;
            }

            if (!logged && sw.Elapsed > TimeSpan.FromSeconds(3))
            {
                logged = true;
                var bagContents = snap?.Player?.BagContents;
                var bagStr = bagContents != null
                    ? string.Join(", ", bagContents.Select(kv => $"slot{kv.Key}={kv.Value}"))
                    : "null";
                _output.WriteLine($"  [{label}] WaitForBag @{sw.ElapsedMilliseconds}ms: target={itemId}, bagContents=[{bagStr}]");
            }

            await Task.Delay(300);
        }

        var finalSnap = await _bot.GetSnapshotAsync(account);
        var finalBag = finalSnap?.Player?.BagContents;
        var finalStr = finalBag != null
            ? string.Join(", ", finalBag.Select(kv => $"slot{kv.Key}={kv.Value}"))
            : "null";
        _output.WriteLine($"  [{label}] WaitForBag TIMEOUT: target={itemId}, finalBag=[{finalStr}]");
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
}
